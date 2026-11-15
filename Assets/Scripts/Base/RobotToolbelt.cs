using MarsRobot.Energy;
using MarsRobot.Interaction;
using MarsRobot.Items;
using MarsRobot.Robot;
using MarsRobot.UI.Minimap;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Instrumentos del rover que no dependen del brazo delantero:
    ///  • L — Foco para las noches marcianas.
    ///  • Z — Escáner geológico: lee hielo y minerales del suelo (datos CRISM reales si existen) y
    ///        marca en el minimapa las rocas aprovechables cercanas.
    /// También muestra el resumen de herramienta y carga en el HUD.
    /// (El taladro ahora es una herramienta del brazo: ver RoverToolController; la carga, RobotInventory.)
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotToolbelt : MonoBehaviour
    {
        [Header("Foco")]
        [SerializeField, Min(1f)] private float lightRange = 45f;
        [SerializeField, Min(0f)] private float lightIntensity = 2.5f;

        [Header("Escáner")]
        [SerializeField, Min(0f)] private float scanEnergyWh = 15f;
        [SerializeField, Min(1f)] private float scanRadius = 45f;
        [SerializeField, Min(1f)] private float scanDisplaySeconds = 8f;

        private Light _light;
        private RobotController _controller;
        private BatterySystem _battery;
        private RoverToolController _tools;
        private RobotInventory _inventory;
        private string _scanText = "";
        private float _scanTime = -100f;
        private string _message = "";
        private float _messageTime = -100f;

        private void Awake()
        {
            _controller = GetComponent<RobotController>();
            _battery = GetComponent<BatterySystem>();
            _tools = GetComponent<RoverToolController>();
            _inventory = GetComponent<RobotInventory>();
        }

        private void Update()
        {
            if (_controller != null && !_controller.InputEnabled) return;
            if (RobotInput.FlashlightPressed) ToggleLight();
            if (RobotInput.ScanPressed) Scan();
        }

        private void ToggleLight()
        {
            if (_light == null)
            {
                Transform mount = _controller != null && _controller.CameraPivot != null ? _controller.CameraPivot : transform;
                var go = new GameObject("Foco");
                go.transform.SetParent(mount, false);
                go.transform.localPosition = new Vector3(0f, -0.1f, 0.2f);
                _light = go.AddComponent<Light>();
                _light.type = LightType.Spot;
                _light.range = lightRange;
                _light.spotAngle = 60f;
                _light.intensity = lightIntensity;
                _light.color = new Color(1f, 0.96f, 0.9f);
                _light.shadows = LightShadows.Soft;
                return;
            }
            _light.enabled = !_light.enabled;
        }

        private void Scan()
        {
            if (_battery != null && !_battery.TryConsume(scanEnergyWh))
            {
                Show("Batería insuficiente para escanear.");
                return;
            }
            RoverToolController.SoilReading s = _tools != null ? _tools.ReadSoil(transform.position) : default(RoverToolController.SoilReading);
            int rocks = ResourceNode.RevealAround(transform.position, scanRadius);

            var sb = new System.Text.StringBuilder("<b>Escáner geológico</b>");
            sb.Append(s.realData ? "  (datos orbitales CRISM)\n" : "  (estimación de la zona)\n");
            sb.Append("Hielo bajo el suelo: ").Append(Pct(s.ice)).Append('\n');
            sb.Append("Óxidos de hierro: ").Append(Pct(s.iron)).Append('\n');
            sb.Append("Sulfatos: ").Append(Pct(s.sulfates)).Append("   Arcillas: ").Append(Pct(s.clays)).Append('\n');
            sb.Append("Rocas aprovechables cerca: ").Append(rocks).Append(" (marcadas en el minimapa)\n");
            if (s.ice >= 0.5f) sb.Append("<color=#80d0ff>Buen sitio para un taladro de hielo.</color>");
            else if (s.iron >= 0.45f) sb.Append("<color=#ffb080>Suelo rico en hierro: perfora aquí (X).</color>");
            else sb.Append("Suelo normal: sirve para regolito.");
            _scanText = sb.ToString();
            _scanTime = Time.time;

            if (s.ice >= 0.5f) MinimapObjectMarker.AddPing(transform.position, MinimapMarkerType.Ice, 600f);
        }

        private void Show(string text)
        {
            _message = text;
            _messageTime = Time.time;
        }

        private void OnGUI()
        {
            GUI.skin.label.richText = true;
            var sb = new System.Text.StringBuilder();

            if (_tools != null)
            {
                sb.Append("<b>Herramienta:</b> ").Append(RoverToolController.ToolLabel(_tools.ActiveTool));
                if (_tools.IsWorking) sb.Append("  <color=#ffd060>trabajando… ").Append(Mathf.RoundToInt(_tools.Progress01 * 100f)).Append(" %</color>");
                if (Time.time - _tools.LastMessageTime < 2.5f && _tools.LastMessage.Length > 0) sb.Append("\n").Append(_tools.LastMessage);
            }
            if (_inventory != null && _inventory.Bay != null)
            {
                sb.Append("\n<b>Carga:</b> ").Append(_inventory.Bay.TotalMassKg.ToString("0")).Append('/').Append(_inventory.Bay.MaxMassKg.ToString("0"))
                  .Append(" kg · ").Append(_inventory.Bay.TotalVolumeLiters.ToString("0")).Append('/').Append(_inventory.Bay.MaxVolumeLiters.ToString("0")).Append(" L  ")
                  .Append(_inventory.LoadStatus);
            }
            if (Time.time - _messageTime < 3f) sb.Append("\n<b>").Append(_message).Append("</b>");
            sb.Append("\nX herramienta · F usar · I carga · J bitácora · L foco · Z escáner · B construir");

            float w = 680f;
            GUI.Label(new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 250, w, 110), sb.ToString());

            if (Time.time - _scanTime < scanDisplaySeconds)
            {
                Rect r = new Rect(Screen.width - 330, 420, 320, 150);
                GUI.Box(r, "");
                GUI.Label(new Rect(r.x + 8, r.y + 6, r.width - 16, r.height - 12), _scanText);
            }
        }

        private static string Pct(float v)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(v) * 100f) + " %";
        }
    }
}
