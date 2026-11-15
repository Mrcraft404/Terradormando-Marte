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
        InternalBay
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
        DualArmAdapter
    }

    /// <summary>Definición de un módulo.</summary>
    [Serializable]
    public class RoverModuleDefinition
    {
        public RoverModuleType type;
        public string name;
        [TextArea] public string description;
        public RoverSlotType slot;
        public float massKg;
        [Tooltip("Litros extra de bahía de carga.")]
        public float extraVolumeLiters;
        [Tooltip("Kg extra de capacidad de carga.")]
        public float extraPayloadKg;
        [Tooltip("Wh extra de batería.")]
        public float extraBatteryWh;
        [Tooltip("m² extra de panel solar.")]
        public float extraPanelM2;
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
        private readonly HashSet<RoverModuleType> _owned = new HashSet<RoverModuleType>();
        private float _appliedBatteryWh, _appliedPanelM2;

        public RoverConfig Config
        {
            get { return config != null ? config : RoverConfig.Default; }
        }

        /// <summary>Se lanza al cambiar la configuración (HUD, herramientas, minimapa…).</summary>
        public event Action Changed;

        public float ModulesMassKg { get; private set; }

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

            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.mass = c.chassisMassKg;
                rb.centerOfMass = c.centerOfMass;
            }

            // Equipo de serie: taladro percutor y compartimento de almacenamiento.
            _owned.Add(RoverModuleType.PercussionDrill);
            _owned.Add(RoverModuleType.StorageBay);
            _front[0] = RoverModuleType.PercussionDrill;
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
                new RoverModuleDefinition { type = RoverModuleType.PercussionDrill, name = "Taladro percutor", slot = RoverSlotType.FrontArm, massKg = 45f,
                    description = "Perfora rocas densas y el subsuelo: menas, hielo y muestras. Lento.", cost = new[] { R(ResourceType.MetalParts, 4) } },
                new RoverModuleDefinition { type = RoverModuleType.Shovel, name = "Pala", slot = RoverSlotType.FrontArm, massKg = 30f,
                    description = "Recoge regolito suelto muy rápido. No rompe rocas densas.", cost = new[] { R(ResourceType.MetalParts, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.DustBlower, name = "Soplador de polvo", slot = RoverSlotType.FrontArm, massKg = 15f,
                    description = "Limpia los paneles del rover y los paneles fijos de la base.", cost = new[] { R(ResourceType.MetalParts, 1), R(ResourceType.Wiring, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.HighGainAntenna, name = "Antena de alta ganancia", slot = RoverSlotType.TopDeck, massKg = 20f,
                    description = "Revela el doble de mapa a tu alrededor y recibe avisos de clima espacial con más antelación.", cost = new[] { R(ResourceType.MetalParts, 2), R(ResourceType.Wiring, 3) } },
                new RoverModuleDefinition { type = RoverModuleType.ExtraSolarPanels, name = "Paneles solares extra", slot = RoverSlotType.TopDeck, massKg = 25f, extraPanelM2 = 2f,
                    description = "+2 m² de panel solar.", cost = new[] { R(ResourceType.SolarCells, 3), R(ResourceType.MetalParts, 1) } },
                new RoverModuleDefinition { type = RoverModuleType.StorageBay, name = "Compartimento de carga", slot = RoverSlotType.InternalBay, massKg = 15f, extraVolumeLiters = 60f, extraPayloadKg = 40f,
                    description = "+60 L y +40 kg de carga.", cost = new[] { R(ResourceType.MetalParts, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.AuxBattery, name = "Batería auxiliar", slot = RoverSlotType.InternalBay, massKg = 12f, extraBatteryWh = 1000f,
                    description = "+1000 Wh, pero ocupa un compartimento que no servirá para carga.", cost = new[] { R(ResourceType.MetalParts, 1), R(ResourceType.Wiring, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.CryoContainer, name = "Contenedor criogénico", slot = RoverSlotType.InternalBay, massKg = 20f, extraVolumeLiters = 20f,
                    description = "+20 L aislados: el hielo y las muestras no se pierden.", cost = new[] { R(ResourceType.MetalParts, 2), R(ResourceType.Wiring, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.SealedContainer, name = "Contenedor sellado", slot = RoverSlotType.InternalBay, massKg = 10f, extraVolumeLiters = 10f,
                    description = "+10 L estancos: los percloratos ya no corroen el chasis.", cost = new[] { R(ResourceType.MetalParts, 2) } },
                new RoverModuleDefinition { type = RoverModuleType.DualArmAdapter, name = "Adaptador de brazo doble", slot = RoverSlotType.InternalBay, massKg = 25f,
                    description = "Permite una segunda herramienta delantera. Mucho peso: más lento y más consumo.", cost = new[] { R(ResourceType.MetalParts, 5), R(ResourceType.Wiring, 2) } },
            };
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        public bool HasModule(RoverModuleType type)
        {
            return Array.IndexOf(_front, type) >= 0 || Array.IndexOf(_top, type) >= 0 || Array.IndexOf(_bay, type) >= 0;
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
            return slot == RoverSlotType.FrontArm ? _front : (slot == RoverSlotType.TopDeck ? _top : _bay);
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
            if (!IsDockedAtAssemblyBay()) return "Aparca el rover en el taller de ensamblaje del cohete.";
            RoverModuleDefinition def = Find(type);
            if (def == null) return "Módulo desconocido.";
            if (HasModule(type)) return def.name + " ya está instalado.";

            RoverModuleType[] slots = GetSlots(def.slot);
            int limit = def.slot == RoverSlotType.FrontArm ? FrontSlotCount : slots.Length;
            int free = -1;
            for (int i = 0; i < limit; i++)
            {
                if (slots[i] == RoverModuleType.None) { free = i; break; }
            }
            if (free < 0) return "No hay ranura libre de " + SlotLabel(def.slot) + ": quita otro módulo primero.";

            if (!_owned.Contains(type))
            {
                if (payFrom == null || !payFrom.TrySpend(def.cost)) return "Faltan materiales: " + BaseInventory.Describe(def.cost);
                _owned.Add(type);
            }
            slots[free] = type;
            Apply();
            return def.name + " instalado.";
        }

        public string Unequip(RoverModuleType type)
        {
            if (!IsDockedAtAssemblyBay()) return "Aparca el rover en el taller de ensamblaje del cohete.";
            RoverModuleDefinition def = Find(type);
            if (def == null) return "";
            if (type == RoverModuleType.DualArmAdapter && _front[Config.frontArmSlots] != RoverModuleType.None)
            {
                return "Quita antes la segunda herramienta delantera.";
            }
            RoverModuleType[] slots = GetSlots(def.slot);
            int idx = Array.IndexOf(slots, type);
            if (idx < 0) return "";
            slots[idx] = RoverModuleType.None;
            Apply();

            // Si la carga ya no cabe tras quitar un compartimento, se avisa (no se pierde nada).
            var inv = GetComponent<RobotInventory>();
            if (inv != null && inv.IsFull) return def.name + " retirado. Ojo: la carga supera ahora la capacidad.";
            return def.name + " retirado (se guarda en el taller).";
        }

        /// <summary>Recalcula y aplica los efectos de todos los módulos.</summary>
        private void Apply()
        {
            float mass = 0f, vol = 0f, payload = 0f, wh = 0f, panel = 0f;
            bool cryo = false, sealedC = false;
            ForEachEquipped(def =>
            {
                mass += def.massKg;
                vol += def.extraVolumeLiters;
                payload += def.extraPayloadKg;
                wh += def.extraBatteryWh;
                panel += def.extraPanelM2;
                if (def.type == RoverModuleType.CryoContainer) cryo = true;
                if (def.type == RoverModuleType.SealedContainer) sealedC = true;
            });
            ModulesMassKg = mass;

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
            RoverModuleType[][] all = { _front, _top, _bay };
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

        public static string SlotLabel(RoverSlotType s)
        {
            switch (s)
            {
                case RoverSlotType.FrontArm: return "brazo delantero";
                case RoverSlotType.TopDeck: return "cubierta superior";
                default: return "bahía interna";
            }
        }

        // ------------------------------------------------------------------
        // IMovementModifier: la masa de los módulos pesa (incluso con 0,38 g)
        // ------------------------------------------------------------------

        public float SpeedMultiplier
        {
            get { return Mathf.Clamp(1f - ModulesMassKg / Config.massForZeroSpeedKg, 0.55f, 1f); }
        }

        public float PowerMultiplier
        {
            get { return 1f + ModulesMassKg / Config.massForDoublePowerKg; }
        }

        public float WearMultiplier
        {
            get { return 1f; }
        }
    }
}
