using System;
using MarsRobot.Core;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Energy
{
    /// <summary>
    /// Polvo sobre los paneles solares y carga de la batería.
    ///
    /// Cada frame:
    ///   1. El polvo se acumula según el entorno (más durante las tormentas).
    ///   2. Los paneles generan: irradiancia × área × eficiencia × (1 − polvo) × estado del módulo SolarPanel.
    ///   3. La energía va a la batería (<see cref="IEnergyStorage"/>).
    /// El jugador limpia los paneles a mano (<see cref="CleanPanels"/>) y las ráfagas de viento
    /// del entorno los limpian a veces solas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DustingSystem : MonoBehaviour
    {
        [Header("Referencias")]
        [Tooltip("Batería que se carga. Si está vacío se busca un BatterySystem en el mismo objeto.")]
        [SerializeField] private BatterySystem battery;

        [Tooltip("Componente que implementa ISolarEnvironment (normalmente MarsEnvironmentSystem).")]
        [SerializeField] private MonoBehaviour environmentProvider;

        [Tooltip("Opcional: si el panel solar del robot está dañado, genera menos.")]
        [SerializeField] private RobotHealthSystem robotHealth;

        [Header("Paneles")]
        [SerializeField, Min(0.1f)] private float panelAreaM2 = 2.5f;
        [Tooltip("Eficiencia de conversión de las células (0.2–0.3 es realista).")]
        [SerializeField, Range(0.05f, 0.5f)] private float cellEfficiency = 0.25f;

        [Header("Polvo")]
        [SerializeField, Range(0f, 1f)] private float startDust01 = 0f;
        [Tooltip("Por encima de este nivel se avisa al jugador de que limpie.")]
        [SerializeField, Range(0f, 1f)] private float warningDust01 = 0.5f;
        [Tooltip("Polvo que quita una pasada de limpieza manual.")]
        [SerializeField, Range(0f, 1f)] private float manualCleanAmount = 0.2f;

        private ISolarEnvironment _environment;
        private bool _warningRaised;
        private float _lastNotifiedDust = -1f;

        /// <summary>Polvo que quita una limpieza manual (la mejora el cepillo del taller).</summary>
        public float ManualCleanAmount
        {
            get { return manualCleanAmount; }
            set { manualCleanAmount = Mathf.Clamp01(value); }
        }

        /// <summary>Área de paneles en m² (la amplía el taller con paneles plegables).</summary>
        public float PanelAreaM2
        {
            get { return panelAreaM2; }
            set { panelAreaM2 = Mathf.Max(0.1f, value); }
        }

        /// <summary>Cobertura de polvo 0..1 (1 = paneles opacos).</summary>
        public float Dust01 { get; private set; }

        /// <summary>Potencia que generan ahora los paneles (W).</summary>
        public float CurrentOutputWatts { get; private set; }

        /// <summary>Potencia que generarían limpios y sin daño (W), para mostrar la pérdida en el HUD.</summary>
        public float CleanOutputWatts { get; private set; }

        /// <summary>Cambio en la cobertura de polvo (0..1).</summary>
        public event Action<float> DustChanged;

        /// <summary>El polvo ha superado el umbral de aviso.</summary>
        public event Action DustWarning;

        /// <summary>Los paneles se han limpiado (manual o por ráfaga). Parámetro: polvo eliminado.</summary>
        public event Action<float> PanelsCleaned;

        private void Awake()
        {
            if (battery == null) battery = GetComponent<BatterySystem>();
            if (robotHealth == null) robotHealth = GetComponent<RobotHealthSystem>();
            Dust01 = startDust01;
            GameServices.Register(this);
        }

        private void OnEnable()
        {
            SetEnvironment(environmentProvider as ISolarEnvironment);
        }

        private void OnDisable()
        {
            if (_environment != null) _environment.CleaningGust -= OnCleaningGust;
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        /// <summary>Conecta otro entorno en tiempo de ejecución (lo usa el LandingZoneSpawner).</summary>
        public void SetEnvironment(ISolarEnvironment environment)
        {
            if (_environment != null) _environment.CleaningGust -= OnCleaningGust;
            _environment = environment;
            if (_environment != null) _environment.CleaningGust += OnCleaningGust;
        }

        private void Update()
        {
            if (_environment == null) return;

            float gameSeconds = MarsTime.GameDeltaTime; // reloj compartido (MarsTime.TimeScale)
            float gameHours = gameSeconds / MarsTime.HourSeconds;

            // 1) Acumulación de polvo (más lenta cuanto más cubierto está: saturación).
            float deposition = _environment.DustDepositionPerHour * gameHours * (1f - Dust01);
            SetDust(Dust01 + deposition);

            // 2) Generación.
            float panelHealth = robotHealth != null ? robotHealth.GetEfficiency(RobotModuleId.SolarPanel) : 1f;
            CleanOutputWatts = _environment.SolarIrradiance * panelAreaM2 * cellEfficiency;
            CurrentOutputWatts = CleanOutputWatts * (1f - Dust01) * panelHealth;

            // 3) Carga de la batería.
            if (battery != null && CurrentOutputWatts > 0f)
            {
                battery.AddEnergy(MarsTime.WattsToWh(CurrentOutputWatts, gameSeconds));
            }
        }

        /// <summary>Una pasada de limpieza manual (el jugador con el cepillo). Devuelve el polvo quitado.</summary>
        public float CleanPanels()
        {
            return CleanPanels(manualCleanAmount);
        }

        /// <summary>Quita <paramref name="amount"/> de polvo (0..1). Devuelve lo que se ha quitado de verdad.</summary>
        public float CleanPanels(float amount)
        {
            float before = Dust01;
            SetDust(Dust01 - Mathf.Max(0f, amount));
            float removed = before - Dust01;
            if (removed > 0f && PanelsCleaned != null) PanelsCleaned(removed);
            return removed;
        }

        private void OnCleaningGust(float strength01)
        {
            // Una ráfaga quita una fracción del polvo actual, no una cantidad fija.
            CleanPanels(Dust01 * Mathf.Clamp01(strength01));
        }

        private void SetDust(float value)
        {
            Dust01 = Mathf.Clamp01(value);

            if (Mathf.Abs(Dust01 - _lastNotifiedDust) >= 0.005f)
            {
                _lastNotifiedDust = Dust01;
                if (DustChanged != null) DustChanged(Dust01);
            }

            if (Dust01 >= warningDust01 && !_warningRaised)
            {
                _warningRaised = true;
                if (DustWarning != null) DustWarning();
            }
            else if (Dust01 < warningDust01 * 0.8f)
            {
                _warningRaised = false; // histéresis del aviso
            }
        }
    }
}
