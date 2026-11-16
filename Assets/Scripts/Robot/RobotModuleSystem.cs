using System;
using System.Collections.Generic;
using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Items;
using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>Tipo de ranura del chasis.</summary>
    public enum RoverSlotType
    {
        FrontArm,
        TopDeck,
        InternalBay,
        /// <summary>Tren de rodaje: siempre lleva uno (ruedas de serie, orugas o ruedas rápidas).</summary>
        Wheels
    }

    /// <summary>Para qué sirve una pieza: el taller filtra el catálogo por función.</summary>
    public enum RoverFunction
    {
        Mining,
        Terrain,
        Energy,
        Cargo,
        Exploration
    }

    /// <summary>Módulos intercambiables.</summary>
    public enum RoverModuleType
    {
        None,
        // Brazo delantero
        PercussionDrill,
        Shovel,
        DustBlower,
        // Cubierta superior
        HighGainAntenna,
        ExtraSolarPanels,
        // Bahía interna
        StorageBay,
        AuxBattery,
        CryoContainer,
        SealedContainer,
        DualArmAdapter,
        // Tren de rodaje (añadidos al final para no cambiar los valores anteriores)
        StandardWheels,
        AllTerrainTracks,
        FastWheels,
        // Cubierta superior
        MineralScanner,
        // Bahía interna
        CargoArm
    }

    /// <summary>Definición de un módulo.</summary>
    [Serializable]
    public class RoverModuleDefinition
    {
        public RoverModuleType type;
        public string name;
        [TextArea] public string description;
        public string nameEn;
        public string descriptionEn;
        public RoverSlotType slot;
        public RoverFunction function;

        /// <summary>Nombre en el idioma elegido.</summary>
        public string DisplayName
        {
            get { return MarsRobot.Localization.Localizer.English && !string.IsNullOrEmpty(nameEn) ? nameEn : name; }
        }

        /// <summary>Descripción en el idioma elegido.</summary>
        public string DisplayDescription
        {
            get { return MarsRobot.Localization.Localizer.English && !string.IsNullOrEmpty(descriptionEn) ? descriptionEn : description; }
        }
        public float massKg;
        [Tooltip("Litros extra de bahía de carga.")]
        public float extraVolumeLiters;
        [Tooltip("Kg extra de capacidad de carga.")]
        public float extraPayloadKg;
        [Tooltip("Wh extra de batería.")]
        public float extraBatteryWh;
        [Tooltip("m² extra de panel solar.")]
        public float extraPanelM2;
        [Tooltip("Multiplicador de velocidad máxima (1 = igual).")]
        public float speedMultiplier = 1f;
        [Tooltip("Multiplicador del desgaste de ruedas (1 = igual).")]
        public float wearMultiplier = 1f;
        [Tooltip("Multiplicador del consumo de los motores (1 = igual).")]
        public float powerMultiplier = 1f;
        [Tooltip("Grados extra de pendiente que puede subir.")]
        public float climbBonusDeg;
        [Tooltip("Multiplicador de la probabilidad de encontrar minerales al perforar.")]
        public float oreChanceMultiplier = 1f;
        [Tooltip("Coste de fabricarlo la primera vez (luego se puede quitar y poner gratis).")]
        public ResourceAmount[] cost;
    }

    /// <summary>
    /// Ensamblaje de módulos del rover. Los módulos solo se cambian con el rover aparcado en el
    /// taller de ensamblaje del cohete (RobotAssemblyBay). Cada ranura admite un módulo y el
    /// número de ranuras es limitado: no se puede llevar todo a la vez.
    /// La masa de los módulos penaliza velocidad y consumo (IMovementModifier).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotModuleSystem : MonoBehaviour, IMovementModifier
    {
        [SerializeField] private RoverConfig config;

        private RoverModuleType[] _front;
        private RoverModuleType[] _top;
        private RoverModuleType[] _bay;
        private RoverModuleType[] _wheels;
        private readonly HashSet<RoverModuleType> _owned = new HashSet<RoverModuleType>();
        private float _appliedBatteryWh, _appliedPanelM2;

        public RoverConfig Config
        {
            get { return config != null ? config : RoverConfig.Default; }
        }

        /// <summary>Se lanza al cambiar la configuración (HUD, herramientas, minimapa…).</summary>
        public event Action Changed;

        public float ModulesMassKg { get; private set; }

        /// <summary>Grados extra (o de menos) de pendiente que puede subir según el tren de rodaje.</summary>
        public float ClimbBonusDegrees { get; private set; }

        /// <summary>Multiplicador de hallazgos de minerales (escáner).</summary>
        public float OreChanceMultiplier { get; private set; } = 1f;

        private float _speedFromParts = 1f, _wearFromParts = 1f, _powerFromParts = 1f;

        /// <summary>Coste de montar en el taller una pieza que ya tienes fabricada (mano de obra).</summary>
        public static readonly ResourceAmount[] SwapCost = { new ResourceAmount(ResourceType.MetalParts, 1) };

        /// <summary>Masa total: chasis + módulos + carga.</summary>
        public float TotalMassKg
        {
            get
            {
                var inv = GetComponent<RobotInventory>();
                return Config.chassisMassKg + ModulesMassKg + (inv != null && inv.Bay != null ? inv.Bay.TotalMassKg : 0f);
            }
        }

        private void Awake()
        {
            RoverConfig c = Config;
            _front = new RoverModuleType[c.frontArmSlots + 1]; // +1 reservado para el adaptador doble
            _top = new RoverModuleType[c.topDeckSlots];
            _bay = new RoverModuleType[c.internalBaySlots];
            _wheels = new RoverModuleType[1];

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = c.chassisMassKg;
                rb.centerOfMass = c.centerOfMass;
            }

            // Equipo de serie: taladro percutor y compartimento de almacenamiento.
            _owned.Add(RoverModuleType.PercussionDrill);
            _owned.Add(RoverModuleType.StorageBay);
            _owned.Add(RoverModuleType.StandardWheels);
            _front[0] = RoverModuleType.PercussionDrill;
            _wheels[0] = RoverModuleType.StandardWheels;
            if (_bay.Length > 0) _bay[0] = RoverModuleType.StorageBay;
        }

        private void Start()
        {
            Apply();
        }

        // ------------------------------------------------------------------
        // Catálogo
        // ------------------------------------------------------------------

        private static List<RoverModuleDefinition> _catalog;

        public static IReadOnlyList<RoverModuleDefinition> Catalog
        {
            get
            {
                if (_catalog == null) _catalog = BuildCatalog();
                return _catalog;
            }
        }

        public static RoverModuleDefinition Find(RoverModuleType type)
        {
            var c = Catalog;
            for (int i = 0; i < c.Count; i++)
            {
                if (c[i].type == type) return c[i];
            }
            return null;
        }

        private static ResourceAmount R(ResourceType t, int n)
        {
            return new ResourceAmount(t, n);
        }

        private static List<RoverModuleDefinition> BuildCatalog()
        {
            return new List<RoverModuleDefinition>
            {
                new RoverModuleDefinition { type = RoverModuleType.PercussionDrill, nameEn = "Hammer drill", descriptionEn = "Breaks dense rocks and digs underground: ores, ice and samples. Slow.", function = RoverFunction.Mining, name = "Taladro percutor", slot = RoverSlotType.FrontArm, massKg = 45f,
                    description = "Perfora rocas densas y el subsuelo: menas, hielo y muestras. Lento.", cost = new[] { R(ResourceType.MetalParts, 4) } },
                new RoverModuleDefinition { type = RoverModuleType.Shovel, nameEn = "Shovel", descriptionEn = "Scoops loose soil very fast. It can't break dense rocks.", function = RoverFunction.Mining, name = "Pala", slot = RoverSlotType.FrontArm, massKg = 30f,
                    description = "Recoge regolito suelto muy rápido. No rompe rocas densas.", cost = new[] { R(ResourceType.MetalParts, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.DustBlower, nameEn = "Dust blower", descriptionEn = "Cleans the dust off the robot's solar panels and the base's panels.", function = RoverFunction.Energy, name = "Soplador de polvo", slot = RoverSlotType.FrontArm, massKg = 15f,
                    description = "Limpia los paneles del rover y los paneles fijos de la base.", cost = new[] { R(ResourceType.MetalParts, 1), R(ResourceType.Wiring, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.HighGainAntenna, nameEn = "Big antenna", descriptionEn = "Shows twice as much map around you and warns you earlier about storms from the Sun.", function = RoverFunction.Exploration, name = "Antena de alta ganancia", slot = RoverSlotType.TopDeck, massKg = 20f,
                    description = "Revela el doble de mapa a tu alrededor y recibe avisos de clima espacial con más antelación.", cost = new[] { R(ResourceType.MetalParts, 2), R(ResourceType.Wiring, 3) } },
                new RoverModuleDefinition { type = RoverModuleType.ExtraSolarPanels, nameEn = "Extra solar panels", descriptionEn = "+2 m² of solar panels: more energy from the Sun.", function = RoverFunction.Energy, name = "Paneles solares extra", slot = RoverSlotType.TopDeck, massKg = 25f, extraPanelM2 = 2f,
                    description = "+2 m² de panel solar.", cost = new[] { R(ResourceType.SolarCells, 3), R(ResourceType.MetalParts, 1) } },
                new RoverModuleDefinition { type = RoverModuleType.StorageBay, nameEn = "Cargo box", descriptionEn = "+60 L and +40 kg of cargo.", function = RoverFunction.Cargo, name = "Compartimento de carga", slot = RoverSlotType.InternalBay, massKg = 15f, extraVolumeLiters = 60f, extraPayloadKg = 40f,
                    description = "+60 L y +40 kg de carga.", cost = new[] { R(ResourceType.MetalParts, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.AuxBattery, nameEn = "Extra battery", descriptionEn = "+1000 Wh of energy, but it takes a slot that can't hold cargo.", function = RoverFunction.Energy, name = Tr.T("Batería auxiliar"), slot = RoverSlotType.InternalBay, massKg = 12f, extraBatteryWh = 1000f,
                    description = "+1000 Wh, pero ocupa un compartimento que no servirá para carga.", cost = new[] { R(ResourceType.MetalParts, 1), R(ResourceType.Wiring, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.CryoContainer, nameEn = "Cold box", descriptionEn = "+20 L that stay frozen: ice and samples don't melt.", function = RoverFunction.Cargo, name = "Contenedor criogénico", slot = RoverSlotType.InternalBay, massKg = 20f, extraVolumeLiters = 20f,
                    description = "+20 L aislados: el hielo y las muestras no se pierden.", cost = new[] { R(ResourceType.MetalParts, 2), R(ResourceType.Wiring, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.SealedContainer, nameEn = "Sealed box", descriptionEn = "+10 L closed tight: salty soil no longer damages the robot.", function = RoverFunction.Cargo, name = "Contenedor sellado", slot = RoverSlotType.InternalBay, massKg = 10f, extraVolumeLiters = 10f,
                    description = "+10 L estancos: los percloratos ya no corroen el chasis.", cost = new[] { R(ResourceType.MetalParts, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.DualArmAdapter, nameEn = "Double arm", descriptionEn = "Lets the robot carry a second front tool. Heavy: slower and uses more battery.", function = RoverFunction.Mining, name = "Adaptador de brazo doble", slot = RoverSlotType.InternalBay, massKg = 25f,
                    description = "Permite una segunda herramienta delantera. Mucho peso: más lento y más consumo.", cost = new[] { R(ResourceType.MetalParts, 5), R(ResourceType.Wiring, 2) } },

                // --- Tren de rodaje: se elige según el terreno ---
                new RoverModuleDefinition { type = RoverModuleType.StandardWheels, nameEn = "Normal wheels", descriptionEn = "Balanced: not very fast and not very tough.", function = RoverFunction.Terrain, name = "Ruedas de serie", slot = RoverSlotType.Wheels, massKg = 0f,
                    description = "Equilibradas: ni muy rápidas ni muy resistentes.", cost = new ResourceAmount[0] },
                new RoverModuleDefinition { type = RoverModuleType.AllTerrainTracks, nameEn = "All-terrain tracks", descriptionEn = "For rocks and hills: they climb 10° steeper and wear out half as much, but they are 20% slower.", function = RoverFunction.Terrain, name = "Orugas todoterreno", slot = RoverSlotType.Wheels, massKg = 60f,
                    speedMultiplier = 0.8f, wearMultiplier = 0.45f, climbBonusDeg = 10f,
                    description = "Para rocas y cuestas: suben 10° más y se desgastan la mitad, pero van un 20 % más lentas.",
                    cost = new[] { R(ResourceType.MetalParts, 6), R(ResourceType.Iron, 4) } },
                new RoverModuleDefinition { type = RoverModuleType.FastWheels, nameEn = "Fast wheels", descriptionEn = "For flat ground: 35% faster, but they wear out sooner and climb hills worse.", function = RoverFunction.Terrain, name = "Ruedas rápidas", slot = RoverSlotType.Wheels, massKg = 0f,
                    speedMultiplier = 1.35f, wearMultiplier = 1.6f, powerMultiplier = 1.1f, climbBonusDeg = -5f,
                    description = "Para llanuras: +35 % de velocidad, pero se desgastan más y suben peor las cuestas.",
                    cost = new[] { R(ResourceType.MetalParts, 4), R(ResourceType.Wiring, 2) } },

                // --- Otras funciones ---
                new RoverModuleDefinition { type = RoverModuleType.MineralScanner, nameEn = "Mineral scanner", descriptionEn = "Finds hidden minerals: when you dig you get 50% more iron, ice, clay and samples.", function = RoverFunction.Mining, name = "Escáner de minerales", slot = RoverSlotType.TopDeck, massKg = 10f,
                    oreChanceMultiplier = 1.5f,
                    description = "Detecta vetas: al perforar o minar rocas encuentras un 50 % más de hierro, hielo, arcillas y muestras.",
                    cost = new[] { R(ResourceType.Wiring, 3), R(ResourceType.Silicon, 2), R(ResourceType.MetalParts, 1) } },
                new RoverModuleDefinition { type = RoverModuleType.CargoArm, nameEn = "Cargo arm", descriptionEn = "Folding crane: +120 kg of cargo for heavy things (no extra liters).", function = RoverFunction.Cargo, name = "Brazo de carga", slot = RoverSlotType.InternalBay, massKg = 35f, extraPayloadKg = 120f,
                    description = "Grúa plegable: +120 kg de carga para cosas pesadas (no añade litros).",
                    cost = new[] { R(ResourceType.MetalParts, 4), R(ResourceType.Wiring, 1) } },
            };
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        public bool HasModule(RoverModuleType type)
        {
            return Array.IndexOf(_front, type) >= 0 || Array.IndexOf(_top, type) >= 0 || Array.IndexOf(_bay, type) >= 0 ||
                   Array.IndexOf(_wheels, type) >= 0;
        }

        public bool Owns(RoverModuleType type)
        {
            return _owned.Contains(type);
        }

        public int FrontSlotCount
        {
            get { return Config.frontArmSlots + (HasModule(RoverModuleType.DualArmAdapter) ? 1 : 0); }
        }

        public RoverModuleType[] GetSlots(RoverSlotType slot)
        {
            switch (slot)
            {
                case RoverSlotType.FrontArm: return _front;
                case RoverSlotType.TopDeck: return _top;
                case RoverSlotType.Wheels: return _wheels;
                default: return _bay;
            }
        }

        /// <summary>¿Está aparcado en el taller de ensamblaje del cohete?</summary>
        public bool IsDockedAtAssemblyBay()
        {
            RocketBase b;
            if (!GameServices.TryGet(out b)) return false;
            for (int i = 0; i < b.Stations.Count; i++)
            {
                if (b.Stations[i] is RobotAssemblyBay && b.Stations[i].HorizontalDistance(transform.position) <= b.Stations[i].InteractRadius)
                {
                    return true;
                }
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Equipar / quitar
        // ------------------------------------------------------------------

        /// <summary>Instala un módulo en la primera ranura libre de su tipo. Devuelve un mensaje.</summary>
        public string TryEquip(RoverModuleType type, BaseInventory payFrom)
        {
            if (!IsDockedAtAssemblyBay()) return L("Aparca el robot bajo el arco del taller.", "Park the robot under the workshop arch.");
            RoverModuleDefinition def = Find(type);
            if (def == null) return L("Pieza desconocida.", "Unknown part.");
            if (HasModule(type)) return def.DisplayName + L(" ya está puesta.", " is already on.");

            RoverModuleType[] slots = GetSlots(def.slot);
            int limit = def.slot == RoverSlotType.FrontArm ? FrontSlotCount : slots.Length;
            int free = -1;
            for (int i = 0; i < limit; i++)
            {
                if (slots[i] == RoverModuleType.None) { free = i; break; }
            }
            // El tren de rodaje se cambia directamente: el robot nunca se queda sin ruedas.
            if (free < 0 && def.slot == RoverSlotType.Wheels) free = 0;
            if (free < 0) return L("No queda hueco en ", "No free slot in ") + SlotLabel(def.slot) + L(": quita otra pieza primero.", ": take another part off first.");

            // Primera vez: se fabrica (coste completo). Después: solo la mano de obra del montaje.
            ResourceAmount[] price = InstallCost(type);
            if (price.Length > 0 && (payFrom == null || !payFrom.TrySpend(price)))
            {
                return L("Te faltan materiales: ", "You need more materials: ") + BaseInventory.Describe(price);
            }
            _owned.Add(type);
            slots[free] = type;
            Apply();
            return L("¡Listo! ", "Done! ") + def.DisplayName + L(" puesta.", " is on.");
        }

        /// <summary>Lo que cuesta montar la pieza ahora: fabricarla, o solo la mano de obra si ya la tienes.</summary>
        public ResourceAmount[] InstallCost(RoverModuleType type)
        {
            RoverModuleDefinition def = Find(type);
            if (def == null) return new ResourceAmount[0];
            if (!_owned.Contains(type)) return def.cost ?? new ResourceAmount[0];
            return type == RoverModuleType.StandardWheels ? new ResourceAmount[0] : SwapCost;
        }

        public string Unequip(RoverModuleType type)
        {
            if (!IsDockedAtAssemblyBay()) return L("Aparca el robot bajo el arco del taller.", "Park the robot under the workshop arch.");
            RoverModuleDefinition def = Find(type);
            if (def == null) return "";
            if (def.slot == RoverSlotType.Wheels) return L("El robot necesita ruedas: pon otras para cambiarlas.", "The robot needs wheels: put on others to change them.");
            if (type == RoverModuleType.DualArmAdapter && _front[Config.frontArmSlots] != RoverModuleType.None)
            {
                return L("Quita antes la segunda herramienta delantera.", "Take off the second front tool first.");
            }
            RoverModuleType[] slots = GetSlots(def.slot);
            int idx = Array.IndexOf(slots, type);
            if (idx < 0) return "";
            slots[idx] = RoverModuleType.None;
            Apply();

            // Si la carga ya no cabe tras quitar un compartimento, se avisa (no se pierde nada).
            var inv = GetComponent<RobotInventory>();
            if (inv != null && inv.IsFull) return def.DisplayName + L(" quitada. Ojo: ahora llevas más carga de la que cabe.", " removed. Careful: you now carry more than fits.");
            return def.DisplayName + L(" quitada (se guarda en el taller).", " removed (kept in the workshop).");
        }

        /// <summary>Recalcula y aplica los efectos de todos los módulos.</summary>
        private void Apply()
        {
            float mass = 0f, vol = 0f, payload = 0f, wh = 0f, panel = 0f;
            float speed = 1f, wear = 1f, power = 1f, climb = 0f, ore = 1f;
            bool cryo = false, sealedC = false;
            ForEachEquipped(def =>
            {
                speed *= def.speedMultiplier;
                wear *= def.wearMultiplier;
                power *= def.powerMultiplier;
                climb += def.climbBonusDeg;
                ore *= def.oreChanceMultiplier;
                mass += def.massKg;
                vol += def.extraVolumeLiters;
                payload += def.extraPayloadKg;
                wh += def.extraBatteryWh;
                panel += def.extraPanelM2;
                if (def.type == RoverModuleType.CryoContainer) cryo = true;
                if (def.type == RoverModuleType.SealedContainer) sealedC = true;
            });
            ModulesMassKg = mass;
            _speedFromParts = speed;
            _wearFromParts = wear;
            _powerFromParts = power;
            ClimbBonusDegrees = climb;
            OreChanceMultiplier = ore;

            var inv = GetComponent<RobotInventory>();
            if (inv != null)
            {
                inv.SetCapacityBonus(payload, vol);
                inv.HasCryoStorage = cryo;
                inv.HasSealedContainer = sealedC;
            }
            var battery = GetComponent<BatterySystem>();
            if (battery != null && !Mathf.Approximately(wh, _appliedBatteryWh))
            {
                battery.AddCapacity(wh - _appliedBatteryWh);
                _appliedBatteryWh = wh;
            }
            var dusting = GetComponent<DustingSystem>();
            if (dusting != null && !Mathf.Approximately(panel, _appliedPanelM2))
            {
                dusting.PanelAreaM2 += panel - _appliedPanelM2;
                _appliedPanelM2 = panel;
            }
            var rb = GetComponent<Rigidbody>();
            if (rb != null) rb.mass = Config.chassisMassKg + mass;

            if (Changed != null) Changed();
        }

        private void ForEachEquipped(Action<RoverModuleDefinition> action)
        {
            RoverModuleType[][] all = { _front, _top, _bay, _wheels };
            for (int a = 0; a < all.Length; a++)
            {
                for (int i = 0; i < all[a].Length; i++)
                {
                    if (all[a][i] == RoverModuleType.None) continue;
                    RoverModuleDefinition def = Find(all[a][i]);
                    if (def != null) action(def);
                }
            }
        }

        private static string L(string es, string en)
        {
            return MarsRobot.Localization.Localizer.L(es, en);
        }

        public static string FunctionLabel(RoverFunction f)
        {
            switch (f)
            {
                case RoverFunction.Mining: return L("Minar", "Mining");
                case RoverFunction.Terrain: return L("Terreno", "Terrain");
                case RoverFunction.Energy: return L("Energía", "Energy");
                case RoverFunction.Cargo: return L("Carga", "Cargo");
                default: return L("Explorar", "Explore");
            }
        }

        public static string SlotLabel(RoverSlotType s)
        {
            switch (s)
            {
                case RoverSlotType.FrontArm: return L("brazo delantero", "front arm");
                case RoverSlotType.TopDeck: return L("cubierta superior", "top deck");
                case RoverSlotType.Wheels: return L("ruedas", "wheels");
                default: return L("bahía interna", "inside bay");
            }
        }

        // ------------------------------------------------------------------
        // IMovementModifier: la masa de los módulos pesa (incluso con 0,38 g)
        // ------------------------------------------------------------------

        public float SpeedMultiplier
        {
            get { return Mathf.Clamp(1f - ModulesMassKg / Config.massForZeroSpeedKg, 0.55f, 1f) * _speedFromParts; }
        }

        public float PowerMultiplier
        {
            get { return (1f + ModulesMassKg / Config.massForDoublePowerKg) * _powerFromParts; }
        }

        public float WearMultiplier
        {
            get { return _wearFromParts; }
        }
    }
}
