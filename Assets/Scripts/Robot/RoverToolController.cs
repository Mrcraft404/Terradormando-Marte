using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Items;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>
    /// Herramienta activa del brazo delantero (tecla X mantenida, con el rover parado):
    ///  • Taladro percutor: perfora el subsuelo → regolito, menas, arcillas, hielo y muestras según
    ///    los minerales reales del lugar (CRISM, si hay datos). Rompe rocas densas.
    ///  • Pala: recoge regolito suelto muy rápido, pero no saca menas ni rompe rocas densas.
    ///  • Soplador: limpia los paneles del rover y los paneles fijos de la base que tenga delante.
    /// Con el adaptador doble se tienen dos herramientas a la vez (X usa la principal).
    /// Para cambiar de herramienta hay que volver al taller de ensamblaje del cohete.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoverToolController : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float maxSpeedToWork = 0.3f;
        [SerializeField, Min(0.2f)] private float drillSeconds = 1.6f;
        [SerializeField, Min(0.2f)] private float shovelSeconds = 0.7f;
        [SerializeField, Min(0f)] private float drillWatts = 300f;
        [SerializeField, Min(0f)] private float shovelWatts = 150f;
        [SerializeField, Min(0f)] private float blowerWatts = 200f;

        private RobotModuleSystem _modules;
        private RobotInventory _inventory;
        private BatterySystem _battery;
        private RobotController _controller;
        private DustingSystem _dusting;
        private MarsTerrainGenerator _marsData;
        private bool _searchedMarsData;
        private float _progress;

        /// <summary>Herramienta principal (primer brazo).</summary>
        public RoverModuleType ActiveTool
        {
            get { return _modules != null ? _modules.GetSlots(RoverSlotType.FrontArm)[0] : RoverModuleType.PercussionDrill; }
        }

        public bool IsWorking { get; private set; }
        public float Progress01
        {
            get { return _progress; }
        }

        /// <summary>Último resultado (para el HUD).</summary>
        public string LastMessage { get; private set; }
        public float LastMessageTime { get; private set; }

        private void Awake()
        {
            _modules = GetComponent<RobotModuleSystem>();
            _inventory = GetComponent<RobotInventory>();
            _battery = GetComponent<BatterySystem>();
            _controller = GetComponent<RobotController>();
            _dusting = GetComponent<DustingSystem>();
            LastMessage = "";
        }

        /// <summary>¿Lleva esta herramienta en alguno de los brazos?</summary>
        public bool HasTool(RoverModuleType tool)
        {
            if (_modules == null) return tool == RoverModuleType.PercussionDrill;
            RoverModuleType[] front = _modules.GetSlots(RoverSlotType.FrontArm);
            int n = _modules.FrontSlotCount;
            for (int i = 0; i < n && i < front.Length; i++)
            {
                if (front[i] == tool) return true;
            }
            return false;
        }

        private void Update()
        {
            IsWorking = false;
            if (_controller != null && !_controller.InputEnabled) return;
            if (!RobotInput.DrillHeld)
            {
                _progress = 0f;
                return;
            }

            RoverModuleType tool = ActiveTool;
            if (tool == RoverModuleType.None)
            {
                Say("No hay herramienta en el brazo delantero (instálala en el taller del cohete).");
                return;
            }
            if (_controller != null && _controller.CurrentSpeed > maxSpeedToWork)
            {
                Say("Detén el rover para usar la herramienta.");
                return;
            }

            float watts = tool == RoverModuleType.PercussionDrill ? drillWatts : (tool == RoverModuleType.Shovel ? shovelWatts : blowerWatts);
            if (_battery != null && !_battery.TryConsume(MarsTime.WattsToWh(watts, Time.deltaTime * MarsTime.TimeScale)))
            {
                Say("Batería insuficiente.");
                return;
            }
            IsWorking = true;

            if (tool == RoverModuleType.DustBlower)
            {
                Blow();
                return;
            }
            if (_inventory != null && _inventory.IsFull)
            {
                Say("Bahía llena: vacía la carga en el cohete o suelta objetos (I).");
                IsWorking = false;
                return;
            }

            float seconds = tool == RoverModuleType.PercussionDrill ? drillSeconds : shovelSeconds;
            _progress += Time.deltaTime / seconds;
            if (_progress < 1f) return;
            _progress = 0f;

            if (tool == RoverModuleType.Shovel) Collect(ItemDatabase.Regolith, 1);
            else DrillGround();
        }

        // ------------------------------------------------------------------
        // Acciones
        // ------------------------------------------------------------------

        /// <summary>Perforación: siempre regolito; según el suelo, algo más.</summary>
        private void DrillGround()
        {
            SoilReading s = ReadSoil(transform.position);
            // Con muchos sulfatos suele haber percloratos (señal de sales oxidantes).
            Collect(s.sulfates > 0.3f ? ItemDatabase.PerchlorateRegolith : ItemDatabase.Regolith, 1);
            if (Random.value < 0.1f + s.iron * 0.6f) Collect(ItemDatabase.IronOre, 1);
            if (Random.value < 0.08f + s.clays * 0.5f) Collect(ItemDatabase.Clays, 1);
            if (Random.value < s.ice * 0.5f) Collect(ItemDatabase.Ice, 1);
            if (Random.value < 0.15f + s.sulfates * 0.3f) Collect(ItemDatabase.Sample, 1);
        }

        private void Blow()
        {
            if (_dusting != null) _dusting.CleanPanels(0.3f * Time.deltaTime);

            // Paneles fijos de la base delante del rover (hasta 6 m).
            RocketBase b;
            if (!GameServices.TryGet(out b)) return;
            for (int i = 0; i < b.Grid.Structures.Count; i++)
            {
                PlacedStructure ps = b.Grid.Structures[i];
                if (ps == null || ps.Definition.kind != StructureKind.SolarArray) continue;
                Vector3 to = ps.transform.position - transform.position;
                to.y = 0f;
                if (to.magnitude < 6f + ps.Definition.size.x * 0.5f && Vector3.Dot(to.normalized, transform.forward) > 0.5f)
                {
                    ps.CleanPanel(0.4f * Time.deltaTime);
                }
            }
            Say("Soplando polvo…");
        }

        /// <summary>Guarda un objeto en la bahía. Devuelve false si no cabe.</summary>
        public bool Collect(string itemId, int count)
        {
            ItemData item = ItemDatabase.Get(itemId);
            if (item == null || _inventory == null) return false;
            int added = _inventory.TryAdd(item, count);
            if (added > 0) Say("+" + added + " " + item.itemName);
            else Say("No cabe " + item.itemName + " (masa o volumen al límite).");
            return added > 0;
        }

        private void Say(string text)
        {
            if (text == LastMessage && Time.time - LastMessageTime < 1.5f) return;
            LastMessage = text;
            LastMessageTime = Time.time;
        }

        // ------------------------------------------------------------------
        // Lectura del suelo (datos reales CRISM si existen; si no, de la zona)
        // ------------------------------------------------------------------

        public struct SoilReading
        {
            public float ice, iron, sulfates, clays;
            public bool realData;
        }

        public SoilReading ReadSoil(Vector3 position)
        {
            var r = new SoilReading();
            if (!_searchedMarsData)
            {
                _searchedMarsData = true;
                _marsData = Object.FindAnyObjectByType<MarsTerrainGenerator>();
            }
            MarsChunkData chunk = _marsData != null ? _marsData.GetChunkAt(position) : null;
            if (chunk != null && chunk.Metadata != null && chunk.Metadata.minerals != null)
            {
                r.realData = true;
                r.ice = chunk.Metadata.minerals.subsurfaceIce;
                r.iron = chunk.Metadata.minerals.ironOxides;
                r.sulfates = chunk.Metadata.minerals.sulfates;
                r.clays = chunk.Metadata.minerals.clays;
                return r;
            }

            RocketBase b;
            var zone = GameServices.TryGet(out b) ? b.Zone : null;
            float baseIce = zone != null ? (zone.hasAccessibleWaterIce ? Mathf.Max(zone.iceAbundance, 0.5f) : zone.iceAbundance) : 0.2f;
            Vector3 p = position * 0.01f;
            r.ice = Mathf.Clamp01(baseIce * Mathf.Lerp(0.6f, 1.4f, Mathf.PerlinNoise(p.x, p.z)));
            r.iron = Mathf.Lerp(0.15f, 0.6f, Mathf.PerlinNoise(p.x + 31f, p.z + 7f));
            r.sulfates = Mathf.Lerp(0.05f, 0.45f, Mathf.PerlinNoise(p.x + 11f, p.z + 53f));
            r.clays = Mathf.Lerp(0.05f, 0.4f, Mathf.PerlinNoise(p.x + 71f, p.z + 19f));
            return r;
        }

        public static string ToolLabel(RoverModuleType t)
        {
            RoverModuleDefinition d = RobotModuleSystem.Find(t);
            return d != null ? d.name : "Ninguna";
        }
    }
}
