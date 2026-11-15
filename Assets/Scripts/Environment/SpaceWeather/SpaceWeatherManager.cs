using System;
using System.Collections.Generic;
using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Dialogue;
using MarsRobot.Robot;
using MarsRobot.World.Weather;
using UnityEngine;

namespace MarsRobot.World.SpaceWeather
{
    /// <summary>
    /// Convierte los eventos de DONKI (reales o simulados) en efectos de juego:
    ///  • FLR (fulguración): interferencia en cámara e interfaz y radar/minimapa bloqueado un rato.
    ///  • CME: aviso con cuenta atrás en el HUD; al llegar, tormenta de polvo global (en el juego):
    ///    oscuridad, visibilidad de pocos metros y energía solar anulada.
    ///  • SEP (partículas energéticas): radiación alta y desgaste de los paneles solares (rover y base).
    ///  • GST (tormenta geomagnética): parpadeos de energía en la base y fallos intermitentes de motores.
    /// Los eventos de los últimos 30 días reales se reparten en los próximos soles de juego
    /// (comprimidos), empezando a los pocos minutos para que se noten pronto.
    /// Nota: en el Marte real las tormentas de polvo no las causan las CME; aquí es una licencia de juego.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpaceWeatherManager : MonoBehaviour
    {
        [Header("Calendario")]
        [Tooltip("Soles de juego en los que se reparten los 30 días de eventos reales.")]
        [SerializeField, Min(0.5f)] private float scheduleSols = 6f;
        [Tooltip("Minutos reales hasta el primer evento.")]
        [SerializeField, Min(0f)] private float firstEventRealMinutes = 3f;
        [SerializeField, Min(1)] private int maxFlares = 8;
        [SerializeField, Min(1)] private int maxCmes = 3;
        [SerializeField, Min(1)] private int maxGst = 3;

        [Header("Duraciones (horas de juego)")]
        [SerializeField, Min(0.1f)] private float flareHours = 1.5f;
        [SerializeField, Min(0.1f)] private float cmeStormHours = 6f;
        [SerializeField, Min(0.1f)] private float sepHours = 4f;
        [SerializeField, Min(0.1f)] private float gstHours = 3f;
        [Tooltip("Factor para acortar el viaje real de la CME hasta Marte (días → horas de juego).")]
        [SerializeField, Range(0.01f, 1f)] private float cmeTransitScale = 0.2f;

        [Header("Efectos")]
        [Tooltip("Visibilidad (m) durante la tormenta global.")]
        [SerializeField, Min(1f)] private float stormVisibility = 6f;
        [Tooltip("Pérdida de integridad del panel solar del rover por hora de SEP.")]
        [SerializeField, Min(0f)] private float sepPanelWearPerHour = 3f;

        private class Scheduled
        {
            public SpaceWeatherEvent ev;
            public float triggerGameSeconds;
            public bool fired;
        }

        private class ActiveEffect
        {
            public SpaceWeatherEvent ev;
            public float endGameSeconds;
            public float arrivalGameSeconds = -1f; // CME: impacto
            public bool arrived;
        }

        private readonly List<Scheduled> _schedule = new List<Scheduled>();
        private readonly List<ActiveEffect> _active = new List<ActiveEffect>();
        private readonly List<SpaceWeatherEvent> _history = new List<SpaceWeatherEvent>();
        private SpaceWeatherAPIClient _client;
        private float _gameSeconds;
        private float _flickerTimer;
        private bool _fogSaved;
        private float _savedFogStart, _savedFogEnd;
        private Color _savedAmbient;

        public bool Loaded { get; private set; }

        public string SourceStatus
        {
            get { return _client != null && _client.LastStatus != null ? _client.LastStatus : "Conectando con DONKI…"; }
        }

        public IReadOnlyList<SpaceWeatherEvent> History
        {
            get { return _history; }
        }

        /// <summary>Próximos eventos (para la terminal).</summary>
        public IEnumerable<KeyValuePair<SpaceWeatherEvent, float>> Upcoming()
        {
            foreach (Scheduled s in _schedule)
            {
                if (!s.fired) yield return new KeyValuePair<SpaceWeatherEvent, float>(s.ev, (s.triggerGameSeconds - _gameSeconds) / MarsTime.HourSeconds);
            }
        }

        public IEnumerable<SpaceWeatherEvent> ActiveEvents()
        {
            foreach (ActiveEffect a in _active) yield return a.ev;
        }

        private void Awake()
        {
            GameServices.Register(this);
            SpaceWeatherState.Reset();
            _client = gameObject.AddComponent<SpaceWeatherAPIClient>();
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
            RestoreFog();
            SpaceWeatherState.Reset();
        }

        private void Start()
        {
            StartCoroutine(_client.Fetch(BuildSchedule));
        }

        private void BuildSchedule(List<SpaceWeatherEvent> events)
        {
            // Quedarse con los más relevantes de cada tipo.
            var picked = new List<SpaceWeatherEvent>();
            picked.AddRange(Strongest(events, SpaceWeatherEventType.FLR, maxFlares));
            picked.AddRange(Strongest(events, SpaceWeatherEventType.CME, maxCmes));
            picked.AddRange(Strongest(events, SpaceWeatherEventType.SEP, 3));
            picked.AddRange(Strongest(events, SpaceWeatherEventType.GST, maxGst));
            picked.Sort((a, b) => a.time.CompareTo(b.time));

            if (picked.Count > 0)
            {
                DateTime first = picked[0].time, last = picked[picked.Count - 1].time;
                double span = Math.Max(1.0, (last - first).TotalSeconds);
                float startOffset = firstEventRealMinutes * 60f * MarsTime.TimeScale;
                float window = scheduleSols * MarsTime.SolSeconds;
                foreach (SpaceWeatherEvent e in picked)
                {
                    float f = (float)((e.time - first).TotalSeconds / span);
                    _schedule.Add(new Scheduled { ev = e, triggerGameSeconds = _gameSeconds + startOffset + f * window });
                }
            }
            Loaded = true;
            NotificationSystem.Notify("Enlace de clima espacial: " + SourceStatus, NotificationLevel.Info, 6f);
        }

        private static IEnumerable<SpaceWeatherEvent> Strongest(List<SpaceWeatherEvent> all, SpaceWeatherEventType type, int max)
        {
            var list = all.FindAll(e => e.type == type);
            list.Sort((a, b) => b.intensity01.CompareTo(a.intensity01));
            if (list.Count > max) list.RemoveRange(max, list.Count - max);
            return list;
        }

        // ------------------------------------------------------------------
        // Simulación
        // ------------------------------------------------------------------

        private void Update()
        {
            float dtGame = MarsTime.GameDeltaTime;
            _gameSeconds += dtGame;

            for (int i = 0; i < _schedule.Count; i++)
            {
                Scheduled s = _schedule[i];
                if (!s.fired && _gameSeconds >= s.triggerGameSeconds)
                {
                    s.fired = true;
                    Begin(s.ev);
                }
            }
            UpdateActive(dtGame);
        }

        /// <summary>Lanza el siguiente evento ya (prueba: tecla F11 en la escena de prueba).</summary>
        public void TriggerNextNow()
        {
            for (int i = 0; i < _schedule.Count; i++)
            {
                if (_schedule[i].fired) continue;
                _schedule[i].fired = true;
                Begin(_schedule[i].ev);
                return;
            }
            // Si no queda nada, uno simulado de cada tipo por turnos.
            var t = (SpaceWeatherEventType)(_history.Count % 4);
            Begin(new SpaceWeatherEvent { type = t, time = DateTime.UtcNow, intensity01 = 0.8f, label = t + " de prueba", simulated = true });
        }

        private void Begin(SpaceWeatherEvent ev)
        {
            _history.Add(ev);
            var a = new ActiveEffect { ev = ev };
            switch (ev.type)
            {
                case SpaceWeatherEventType.FLR:
                    a.endGameSeconds = _gameSeconds + flareHours * MarsTime.HourSeconds * Mathf.Lerp(0.5f, 1.5f, ev.intensity01);
                    NotificationSystem.Notify(ev.label + ": interferencias electromagnéticas, radar bloqueado.", ev.intensity01 > 0.7f ? NotificationLevel.Critical : NotificationLevel.Warning, 7f);
                    break;
                case SpaceWeatherEventType.CME:
                {
                    // Tránsito real Sol–Marte (~228 millones de km) a la velocidad de la CME, acortado para el juego.
                    float speed = Mathf.Lerp(300f, 2500f, ev.intensity01);
                    float transitHours = 2.28e8f / speed / 3600f * cmeTransitScale;
                    a.arrivalGameSeconds = _gameSeconds + transitHours * MarsTime.HourSeconds;
                    a.endGameSeconds = a.arrivalGameSeconds + cmeStormHours * MarsTime.HourSeconds;
                    NotificationSystem.Notify("¡ADVERTENCIA: CME detectada! Impacto estimado en " + transitHours.ToString("0") + " h. Busque refugio.", NotificationLevel.Critical, 10f);
                    break;
                }
                case SpaceWeatherEventType.SEP:
                    a.endGameSeconds = _gameSeconds + sepHours * MarsTime.HourSeconds;
                    NotificationSystem.Notify("Tormenta de partículas solares (SEP): radiación alta, los paneles se degradan.", NotificationLevel.Critical, 8f);
                    break;
                default:
                    a.endGameSeconds = _gameSeconds + gstHours * MarsTime.HourSeconds;
                    NotificationSystem.Notify(ev.label + ": fluctuaciones eléctricas en la base y en los motores.", NotificationLevel.Warning, 7f);
                    break;
            }
            _active.Add(a);
        }

        private void UpdateActive(float dtGame)
        {
            float interference = 0f, sep = 0f, gst = 0f;
            bool storm = false;
            float countdown = -1f;

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                ActiveEffect a = _active[i];
                if (_gameSeconds >= a.endGameSeconds)
                {
                    _active.RemoveAt(i);
                    continue;
                }
                switch (a.ev.type)
                {
                    case SpaceWeatherEventType.FLR:
                        interference = Mathf.Max(interference, Mathf.Lerp(0.3f, 0.9f, a.ev.intensity01));
                        break;
                    case SpaceWeatherEventType.CME:
                        if (_gameSeconds < a.arrivalGameSeconds)
                        {
                            float h = (a.arrivalGameSeconds - _gameSeconds) / MarsTime.HourSeconds;
                            countdown = countdown < 0f ? h : Mathf.Min(countdown, h);
                        }
                        else
                        {
                            if (!a.arrived)
                            {
                                a.arrived = true;
                                NotificationSystem.Notify("Impacto de la CME: tormenta de polvo global. Sin energía solar.", NotificationLevel.Critical, 10f);
                            }
                            storm = true;
                        }
                        break;
                    case SpaceWeatherEventType.SEP:
                        sep = Mathf.Max(sep, a.ev.intensity01);
                        break;
                    default:
                        gst = Mathf.Max(gst, a.ev.intensity01);
                        break;
                }
            }

            SpaceWeatherState.Interference01 = interference;
            SpaceWeatherState.RadarBlocked = interference > 0.25f;
            SpaceWeatherState.CmeCountdownHours = countdown;
            SpaceWeatherState.SepActive = sep > 0f;
            SpaceWeatherState.SepIntensity = sep;
            SpaceWeatherState.GstActive = gst > 0f;
            SpaceWeatherState.GstIntensity = gst;
            ApplyGlobalStorm(storm);
            ApplySep(sep, dtGame);
            ApplyGst(gst);
        }

        private void ApplyGlobalStorm(bool on)
        {
            bool was = SpaceWeatherState.GlobalStormActive;
            SpaceWeatherState.GlobalStormActive = on;
            MarsEnvironmentSystem env;
            bool hasEnv = GameServices.TryGet(out env);
            if (hasEnv) env.ExternalLightBlock = on ? 1f : 0f;

            if (on && !was)
            {
                _savedFogStart = RenderSettings.fogStartDistance;
                _savedFogEnd = RenderSettings.fogEndDistance;
                _savedAmbient = RenderSettings.ambientLight;
                _fogSaved = true;
                RenderSettings.fogStartDistance = 0.5f;
                RenderSettings.fogEndDistance = stormVisibility;
                RenderSettings.ambientLight = _savedAmbient * 0.35f;
                if (hasEnv) env.StartStorm(cmeStormHours / 24.66f);
            }
            else if (!on && was)
            {
                RestoreFog();
            }
        }

        private void RestoreFog()
        {
            if (!_fogSaved) return;
            _fogSaved = false;
            RenderSettings.fogStartDistance = _savedFogStart;
            RenderSettings.fogEndDistance = _savedFogEnd;
            RenderSettings.ambientLight = _savedAmbient;
        }

        private void ApplySep(float intensity, float dtGame)
        {
            if (intensity <= 0f) return;
            float hours = dtGame / MarsTime.HourSeconds;
            RobotHealthSystem health;
            if (GameServices.TryGet(out health))
            {
                health.ApplyDamage(RobotModuleId.SolarPanel, new DamageInfo(sepPanelWearPerHour * intensity * hours, DamageSourceType.Environment, health.transform.position, 0f));
            }
            RocketBase b;
            if (GameServices.TryGet(out b))
            {
                for (int i = 0; i < b.Grid.Structures.Count; i++)
                {
                    PlacedStructure s = b.Grid.Structures[i];
                    if (s != null && s.Definition.kind == StructureKind.SolarArray) s.DegradePanel(0.02f * intensity * hours);
                }
            }
        }

        private void ApplyGst(float intensity)
        {
            RocketBase b;
            if (!GameServices.TryGet(out b)) return;
            if (intensity <= 0f)
            {
                b.Grid.ExternalSupplyMultiplier = 1f;
                return;
            }
            // Parpadeos: caídas bruscas de suministro cada pocos segundos reales.
            _flickerTimer -= Time.deltaTime;
            if (_flickerTimer <= 0f)
            {
                _flickerTimer = UnityEngine.Random.Range(0.3f, 2.5f);
                b.Grid.ExternalSupplyMultiplier = UnityEngine.Random.value < 0.35f + intensity * 0.4f ? UnityEngine.Random.Range(0.1f, 0.6f) : 1f;
            }
        }
    }

    /// <summary>
    /// Fallos eléctricos intermitentes en los motores del rover durante tormentas geomagnéticas
    /// (lo lee el RobotController como cualquier otro IMovementModifier).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpaceWeatherMotorGlitch : MonoBehaviour, IMovementModifier
    {
        private float _until;
        private float _timer;

        private void Update()
        {
            if (!SpaceWeatherState.GstActive) return;
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = UnityEngine.Random.Range(1f, 4f);
            if (UnityEngine.Random.value < 0.3f + SpaceWeatherState.GstIntensity * 0.4f) _until = Time.time + UnityEngine.Random.Range(0.3f, 1.2f);
        }

        public float SpeedMultiplier
        {
            get { return Time.time < _until ? 0.25f : 1f; }
        }

        public float PowerMultiplier
        {
            get { return 1f; }
        }

        public float WearMultiplier
        {
            get { return 1f; }
        }
    }
}
