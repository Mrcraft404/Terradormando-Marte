using MarsRobot.Base;
using MarsRobot.Base.Ship;
using MarsRobot.Core;
using UnityEngine;

namespace MarsRobot.Crew
{
    /// <summary>
    /// Soporte vital del astronauta:
    ///  • Dentro de un volumen presurizado y respirable (la nave, un módulo, un pasillo, la esclusa)
    ///    respira el aire del módulo y el depósito del traje se rellena.
    ///  • Fuera, o si el módulo se despresuriza, respira del traje: ~8 horas de juego, como el traje
    ///    EMU de la NASA (con MarsTime.TimeScale = 60, unos 8 minutos reales).
    ///  • Avisa al 25 % y al 10 %. Si se agota, el astronauta se desmaya y el equipo lo lleva a la
    ///    nave con un poco de oxígeno (no hay muerte permanente).
    /// La interfaz muestra dónde está, la presión y el oxígeno del traje.
    /// </summary>
    [RequireComponent(typeof(Astronaut))]
    [DisallowMultipleComponent]
    public sealed class AstronautLifeSupport : MonoBehaviour
    {
        [Tooltip("Autonomía del traje (horas de juego).")]
        [SerializeField, Min(0.1f)] private float suitCapacityHours = 8f;
        [Tooltip("Velocidad de recarga en aire respirable (horas de autonomía por hora de juego).")]
        [SerializeField, Min(0.1f)] private float refillPerHour = 24f;
        [Tooltip("Oxígeno con el que se despierta tras el rescate (fracción de la autonomía).")]
        [SerializeField, Range(0f, 1f)] private float rescueRefill = 0.25f;

        private Astronaut _astronaut;
        private RocketBase _rocket;
        private bool _warned25, _warned10;
        private string _message = "";
        private float _messageTime = -10f;
        private float _probeTimer;
        private GUIStyle _style;

        /// <summary>Oxígeno que queda en el traje (horas de juego).</summary>
        public float SuitOxygenHours { get; private set; }

        public float SuitOxygen01
        {
            get { return SuitOxygenHours / suitCapacityHours; }
        }

        /// <summary>Volumen de aire en el que está ahora (null = a la intemperie de Marte).</summary>
        public PressurizedVolume CurrentVolume { get; private set; }

        /// <summary>True si respira aire del módulo (no gasta el traje).</summary>
        public bool BreathingAmbient
        {
            get { return CurrentVolume != null && CurrentVolume.IsBreathable; }
        }

        private void Awake()
        {
            _astronaut = GetComponent<Astronaut>();
            SuitOxygenHours = suitCapacityHours;
        }

        private void Update()
        {
            if (_rocket == null && GameServices.TryGet(out _rocket))
            {
                ShipAirlockHatch.EnsureShipAtmosphere(_rocket); // la nave es respirable por dentro
            }

            // Comprobación del aire 5 veces por segundo (barata: unas pocas cajas).
            _probeTimer -= Time.deltaTime;
            if (_probeTimer <= 0f)
            {
                _probeTimer = 0.2f;
                CurrentVolume = PressurizedVolume.FindAt(transform.position + Vector3.up * 1.4f); // a la altura del casco
            }

            float hours = MarsTime.GameDeltaTime / MarsTime.HourSeconds;
            if (BreathingAmbient)
            {
                SuitOxygenHours = Mathf.Min(suitCapacityHours, SuitOxygenHours + refillPerHour * hours);
                if (SuitOxygen01 > 0.3f) { _warned25 = false; _warned10 = false; }
                return;
            }

            SuitOxygenHours = Mathf.Max(0f, SuitOxygenHours - hours);
            if (!_warned25 && SuitOxygen01 <= 0.25f) { _warned25 = true; Show(Tr.T("Oxígeno del traje al 25 %. Vuelve a un módulo presurizado.")); }
            if (!_warned10 && SuitOxygen01 <= 0.10f) { _warned10 = true; Show(Tr.T("¡OXÍGENO CRÍTICO (10 %)!")); }
            if (SuitOxygenHours <= 0f) Rescue();
        }

        /// <summary>Sin oxígeno: el astronauta se desmaya y aparece en la nave con algo de oxígeno.</summary>
        private void Rescue()
        {
            SuitOxygenHours = suitCapacityHours * rescueRefill;
            _warned25 = _warned10 = false;
            if (_rocket != null && _rocket.Mothership != null)
            {
                Transform ship = _rocket.Mothership.transform;
                _astronaut.Teleport(ship.TransformPoint(ShipLayout.OnLevel(ShipModel.WorkshopLevel, ShipLayout.CrewSpawn) + Vector3.up * 0.1f),
                                    ship.rotation);
            }
            Show(Tr.T("Te desmayaste por falta de oxígeno. El equipo te ha llevado a la nave."));
            Debug.LogWarning("[Soporte vital] El astronauta se quedó sin oxígeno y ha sido rescatado.", this);
        }

        private void Show(string text)
        {
            _message = text;
            _messageTime = Time.time;
        }

        private void OnGUI()
        {
            if (_astronaut == null || !_astronaut.Active) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = Color.white;
            }

            string where;
            if (CurrentVolume == null)
            {
                where = "<color=#ffb070>Exterior de Marte · 0,6 kPa · respirando del traje</color>";
            }
            else if (CurrentVolume.IsBreathable)
            {
                where = CurrentVolume.Label + " · " + CurrentVolume.PressureKPa.ToString("0.#") + " kPa · O₂ " +
                        CurrentVolume.OxygenPartialKPa.ToString("0.#") + Tr.T(" kPa · <color=#80ff80>aire respirable</color>");
            }
            else
            {
                where = "<color=#ff8080>" + CurrentVolume.Label + Tr.T(" DESPRESURIZADO (") + CurrentVolume.PressureKPa.ToString("0.#") +
                        Tr.T(" kPa) · respirando del traje</color>");
            }

            float o2 = SuitOxygen01;
            int minutes = Mathf.FloorToInt(SuitOxygenHours * 60f);
            bool low = !BreathingAmbient && o2 <= 0.25f;
            bool blink = low && Mathf.Repeat(Time.time, 1f) < 0.5f;
            string tank = Tr.T("Traje: O₂ ") + Mathf.RoundToInt(o2 * 100f) + " % (" + (minutes / 60) + " h " + (minutes % 60).ToString("00") + " min)";
            if (blink) tank = "<color=#ff5050><b>" + tank + "</b></color>";

            const float w = 620f, h = 54f;
            var r = new Rect((Screen.width - w) * 0.5f, 8f, w, h);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            // Barra de oxígeno del traje.
            GUI.color = o2 > 0.25f ? new Color(0.3f, 0.75f, 1f, 0.8f) : new Color(1f, 0.3f, 0.25f, 0.85f);
            GUI.DrawTexture(new Rect(r.x + 6f, r.yMax - 8f, (w - 12f) * o2, 4f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y + 2f, w, 24f), where, _style);
            GUI.Label(new Rect(r.x, r.y + 22f, w, 24f), tank, _style);

            if (Time.time - _messageTime < 5f && _message.Length > 0)
            {
                GUI.Label(new Rect((Screen.width - w) * 0.5f, r.yMax + 6f, w, 26f), "<b>" + _message + "</b>", _style);
            }
        }
    }
}
