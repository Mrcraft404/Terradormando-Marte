using System;
using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Spawns;
using UnityEngine;

namespace MarsRobot.World.Weather
{
    /// <summary>
    /// Clima simplificado de la zona: ciclo día/noche de un sol, tormentas de polvo aleatorias y
    /// ráfagas que limpian paneles. Implementa <see cref="ISolarEnvironment"/>, que es lo único que
    /// conoce el <see cref="DustingSystem"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsEnvironmentSystem : MonoBehaviour, ISolarEnvironment
    {
        [Tooltip("Zona por defecto si nadie llama a Configure (para probar escenas sueltas).")]
        [SerializeField] private LandingZoneData defaultZone;

        [Tooltip("Hora del sol al empezar (0 = medianoche, 0.5 = mediodía).")]
        [SerializeField, Range(0f, 1f)] private float startTimeOfSol = 0.3f;

        [Tooltip("Opcional: luz direccional que hace de sol.")]
        [SerializeField] private Light sunLight;

        private LandingZoneData _zone;
        private float _solTime01;
        private float _stormRemainingSeconds;
        private System.Random _rng;

        public event Action<float> CleaningGust;

        /// <summary>Empieza (true) o termina (false) una tormenta de polvo.</summary>
        public event Action<bool> DustStormChanged;

        /// <summary>Luz direccional que hace de sol (se puede asignar por código).</summary>
        public Light SunLight
        {
            get { return sunLight; }
            set { sunLight = value; }
        }

        public float SolTime01
        {
            get { return _solTime01; }
        }

        public bool IsDustStormActive
        {
            get { return _stormRemainingSeconds > 0f; }
        }

        /// <summary>Bloqueo de luz impuesto desde fuera (clima espacial): 1 = noche total.</summary>
        public float ExternalLightBlock { get; set; }

        public float SolarIrradiance { get; private set; }

        public float DustDepositionPerHour
        {
            get
            {
                if (_zone == null) return 0f;
                return _zone.dustDepositionPerHour * (IsDustStormActive ? _zone.stormDustMultiplier : 1f);
            }
        }

        /// <summary>Temperatura aproximada según la hora (°C).</summary>
        public float TemperatureC
        {
            get
            {
                if (_zone == null) return -60f;
                float day = Mathf.Clamp01(Mathf.Sin((_solTime01 - 0.25f) * Mathf.PI * 2f) * 0.5f + 0.5f);
                return Mathf.Lerp(_zone.minTemperatureC, _zone.maxTemperatureC, day);
            }
        }

        private void Awake()
        {
            _rng = new System.Random(System.Environment.TickCount);
            _solTime01 = startTimeOfSol;
            GameServices.Register(this);
            if (defaultZone != null) Configure(defaultZone);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        /// <summary>Aplica los parámetros de clima de una zona.</summary>
        public void Configure(LandingZoneData zone)
        {
            _zone = zone;
            _stormRemainingSeconds = 0f;
        }

        private void Update()
        {
            if (_zone == null) return;

            float gameSeconds = MarsTime.GameDeltaTime;
            float solFraction = gameSeconds / MarsTime.SolSeconds;
            _solTime01 = Mathf.Repeat(_solTime01 + solFraction, 1f);

            UpdateStorm(gameSeconds, solFraction);
            UpdateGusts(solFraction);

            // Irradiancia: curva solar (0 de noche) × bloqueo de tormenta.
            float sunHeight = Mathf.Sin((_solTime01 - 0.25f) * Mathf.PI * 2f); // −1..1, 1 a mediodía
            float clear = _zone.noonIrradiance * Mathf.Max(0f, sunHeight);
            float block = IsDustStormActive ? _zone.stormLightBlock : 0f;
            block = Mathf.Max(block, Mathf.Clamp01(ExternalLightBlock)); // p. ej. tormenta global por una CME
            SolarIrradiance = clear * (1f - block);

            if (sunLight != null)
            {
                sunLight.transform.rotation = Quaternion.Euler(_solTime01 * 360f - 90f, 30f, 0f);
                sunLight.intensity = Mathf.Max(0f, sunHeight) * (1f - block * 0.8f);
            }
        }

        private void UpdateStorm(float gameSeconds, float solFraction)
        {
            if (IsDustStormActive)
            {
                _stormRemainingSeconds -= gameSeconds;
                if (!IsDustStormActive && DustStormChanged != null) DustStormChanged(false);
                return;
            }

            // Probabilidad por sol convertida a probabilidad en este frame.
            if (_rng.NextDouble() < _zone.stormChancePerSol * solFraction)
            {
                StartStorm(_zone.stormDurationSols * Mathf.Lerp(0.5f, 1.5f, (float)_rng.NextDouble()));
            }
        }

        private void UpdateGusts(float solFraction)
        {
            if (_rng.NextDouble() < _zone.cleaningGustsPerSol * solFraction && CleaningGust != null)
            {
                CleaningGust(Mathf.Lerp(0.1f, 0.5f, (float)_rng.NextDouble()));
            }
        }

        /// <summary>Lanza una tormenta (útil para eventos de guion o pruebas).</summary>
        public void StartStorm(float durationSols)
        {
            bool wasActive = IsDustStormActive;
            _stormRemainingSeconds = Mathf.Max(_stormRemainingSeconds, durationSols * MarsTime.SolSeconds);
            if (!wasActive && DustStormChanged != null) DustStormChanged(true);
        }
    }
}
