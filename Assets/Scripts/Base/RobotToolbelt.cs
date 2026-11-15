using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Herramientas de mano del robot:
    ///  • L — Foco: luz frontal para las noches marcianas.
    ///  • Z — Escáner geológico: lee hielo y minerales del suelo (datos reales del chunk si existen,
    ///        si no, los de la zona) e indica qué conviene construir aquí.
    ///  • X (mantener, parado) — Taladro de muestras: saca regolito y, según el suelo, hierro,
    ///        silicio, hielo y muestras para el laboratorio. Gasta batería.
    ///  • Bodega: lo recogido se guarda en el robot (capacidad limitada) y se descarga solo al
    ///    volver cerca del cohete.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotToolbelt : MonoBehaviour
    {
        [Header("Foco")]
        [SerializeField, Min(1f)] private float lightRange = 45f;
        [SerializeField, Min(0f)] private float lightIntensity = 2.5f;

        [Header("Taladro")]
        [Tooltip("Segundos reales por cada extracción.")]
        [SerializeField, Min(0.2f)] private float drillSeconds = 1.5f;
        [SerializeField, Min(0f)] private float drillPowerWatts = 300f;
        [SerializeField, Min(1)] private int cargoCapacity = 30;
        [SerializeField, Min(0f)] private float maxSpeedToDrill = 0.3f;

        [Header("Escáner")]
        [SerializeField, Min(0f)] private float scanEnergyWh = 15f;
        [SerializeField, Min(1f)] private float scanDisplaySeconds = 8f;

        [Tooltip("Distancia al cohete (m) para descargar la bodega.")]
        [SerializeField, Min(1f)] private float unloadRadius = 25f;

        private readonly Dictionary<ResourceType, int> _cargo = new Dictionary<ResourceType, int>();
        private Light _light;
        private RobotController _controller;
        private BatterySystem _battery;
        private MarsTerrainGenerator _marsData;
        private bool _searchedMarsData;
        private float _drillProgress;
        private string _scanText = "";
        private float _scanTime = -100f;
        private string _message = "";
        private float _messageTime = -100f;

        public int CargoCount { get; private set; }

        public bool IsDrilling { get; private set; }

        private void Awake()
        {
            _controller = GetComponent<RobotController>();
            _battery = GetComponent<BatterySystem>();
        }

        private void Update()
        {
            if (_controller != null && !_controller.InputEnabled)
            {
                IsDrilling = false; // menús abiertos o aterrizando
                return;
            }

            if (RobotInput.FlashlightPressed) ToggleLight();
            if (RobotInput.ScanPressed) Scan();
            UpdateDrill();
            TryUnload();
        }

        // ------------------------------------------------------------------
        // Foco
        // ------------------------------------------------------------------

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

        // ------------------------------------------------------------------
        // Escáner
        // ------------------------------------------------------------------

        private struct SoilReading
        {
            public float ice, iron, sulfates, clays;
            public bool realData;
        }

        private SoilReading ReadSoil()
        {
            var r = new SoilReading();
            if (!_searchedMarsData)
            {
                _searchedMarsData = true;
                _marsData = Object.FindAnyObjectByType<MarsTerrainGenerator>();
            }
            MarsChunkData chunk = _marsData != null ? _marsData.GetChunkAt(transform.position) : null;
            if (chunk != null && chunk.Metadata != null && chunk.Metadata.minerals != null)
            {
                r.realData = true;
                r.ice = chunk.Metadata.minerals.subsurfaceIce;
                r.iron = chunk.Metadata.minerals.ironOxides;
                r.sulfates = chunk.Metadata.minerals.sulfates;
                r.clays = chunk.Metadata.minerals.clays;
                return r;
            }

            // Sin datos reales: valores de la zona con variación según la posición.
            RocketBase b;
            var zone = GameServices.TryGet(out b) ? b.Zone : null;
            float baseIce = zone != null ? (zone.hasAccessibleWaterIce ? Mathf.Max(zone.iceAbundance, 0.5f) : zone.iceAbundance) : 0.2f;
            Vector3 p = transform.position * 0.01f;
            r.ice = Mathf.Clamp01(baseIce * Mathf.Lerp(0.6f, 1.4f, Mathf.PerlinNoise(p.x, p.z)));
            r.iron = Mathf.Lerp(0.15f, 0.6f, Mathf.PerlinNoise(p.x + 31f, p.z + 7f));
            r.sulfates = Mathf.Lerp(0.05f, 0.4f, Mathf.PerlinNoise(p.x + 11f, p.z + 53f));
            r.clays = Mathf.Lerp(0.05f, 0.4f, Mathf.PerlinNoise(p.x + 71f, p.z + 19f));
            return r;
        }

        private void Scan()
        {
            if (_battery != null && !_battery.TryConsume(scanEnergyWh))
            {
                Show("Batería insuficiente para escanear.");
                return;
            }
            SoilReading s = ReadSoil();
            var sb = new System.Text.StringBuilder("<b>Escáner geológico</b>");
            sb.Append(s.realData ? "  (datos orbitales reales)\n" : "  (estimación de la zona)\n");
            sb.Append("Hielo bajo el suelo: ").Append(Pct(s.ice)).Append('\n');
            sb.Append("Óxidos de hierro: ").Append(Pct(s.iron)).Append('\n');
            sb.Append("Sulfatos: ").Append(Pct(s.sulfates)).Append("   Arcillas: ").Append(Pct(s.clays)).Append('\n');
            if (s.ice >= 0.5f) sb.Append("<color=#80d0ff>Buen sitio para un taladro de hielo.</color>");
            else if (s.iron >= 0.45f) sb.Append("<color=#ffb080>Suelo rico en hierro: taladra aquí (X).</color>");
            else sb.Append("Suelo normal: sirve para regolito.");
            _scanText = sb.ToString();
            _scanTime = Time.time;
        }

        // ------------------------------------------------------------------
        // Taladro de muestras y bodega
        // ------------------------------------------------------------------

        private void UpdateDrill()
        {
            IsDrilling = false;
            if (!RobotInput.DrillHeld) return;

            if (_controller != null && _controller.CurrentSpeed > maxSpeedToDrill)
            {
                Show("Detén el robot para taladrar.");
                return;
            }
            if (CargoCount >= cargoCapacity)
            {
                Show("Bodega llena: vuelve a la base para descargar.");
                return;
            }
            float wh = MarsTime.WattsToWh(drillPowerWatts, Time.deltaTime * MarsTime.TimeScale);
            if (_battery != null && !_battery.TryConsume(wh))
            {
                Show("Batería insuficiente para taladrar.");
                return;
            }

            IsDrilling = true;
            _drillProgress += Time.deltaTime / drillSeconds;
            if (_drillProgress < 1f) return;
            _drillProgress = 0f;

            // Cada extracción: siempre regolito y, con probabilidad según el suelo, algo más.
            SoilReading s = ReadSoil();
            AddCargo(ResourceType.Regolith);
            if (Random.value < 0.15f + s.iron * 0.6f) AddCargo(ResourceType.Iron);
            if (Random.value < 0.12f + s.clays * 0.4f) AddCargo(ResourceType.Silicon);
            if (Random.value < s.ice * 0.5f) AddCargo(ResourceType.Ice);
            if (Random.value < 0.2f + s.sulfates * 0.3f) AddCargo(ResourceType.Samples);
        }

        private void AddCargo(ResourceType t)
        {
            if (CargoCount >= cargoCapacity) return;
            int n;
            _cargo.TryGetValue(t, out n);
            _cargo[t] = n + 1;
            CargoCount++;
        }

        private void TryUnload()
        {
            if (CargoCount == 0) return;
            RocketBase b;
            if (!GameServices.TryGet(out b)) return;
            Vector3 d = transform.position - b.transform.position;
            d.y = 0f;
            if (d.magnitude > unloadRadius) return;

            int unloaded = 0;
            var types = new List<ResourceType>(_cargo.Keys);
            for (int i = 0; i < types.Count; i++)
            {
                ResourceType t = types[i];
                int added = b.Inventory.Add(t, _cargo[t]);
                _cargo[t] -= added;
                unloaded += added;
                if (_cargo[t] <= 0) _cargo.Remove(t);
            }
            CargoCount -= unloaded;
            if (unloaded > 0) Show("Descargadas " + unloaded + " unidades en el almacén de la base.");
        }

        // ------------------------------------------------------------------
        // Interfaz
        // ------------------------------------------------------------------

        private void Show(string text)
        {
            if (text == _message && Time.time - _messageTime < 1.5f) return; // no repetir cada frame
            _message = text;
            _messageTime = Time.time;
        }

        private void OnGUI()
        {
            GUI.skin.label.richText = true;
            var sb = new System.Text.StringBuilder();
            sb.Append("<b>Bodega del robot:</b> ").Append(CargoCount).Append('/').Append(cargoCapacity);
            foreach (var kv in _cargo) sb.Append("  ·  ").Append(BaseInventory.Label(kv.Key)).Append(' ').Append(kv.Value);
            if (CargoCount > 0) sb.Append("   (se descarga sola a menos de ").Append(unloadRadius.ToString("0")).Append(" m del cohete)");
            if (IsDrilling) sb.Append("\n<color=#ffd060>Taladrando… ").Append(Mathf.RoundToInt(_drillProgress * 100f)).Append(" %</color>");
            if (Time.time - _messageTime < 3f) sb.Append("\n<b>").Append(_message).Append("</b>");
            sb.Append("\nL foco · Z escáner · X taladrar (parado) · B construir");

            float w = 640f;
            GUI.Label(new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height - 230, w, 90), sb.ToString());

            if (Time.time - _scanTime < scanDisplaySeconds)
            {
                Rect r = new Rect(Screen.width - 330, 420, 320, 130);
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
