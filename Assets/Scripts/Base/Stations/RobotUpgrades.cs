using System;
using System.Collections.Generic;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Qué cambia una mejora.</summary>
    public enum UpgradeEffect
    {
        BatteryCapacity,  // +Wh de batería por nivel
        SolarPanelArea,   // +m² de panel por nivel
        MotorSpeed,       // +m/s de velocidad máxima por nivel
        WheelArmor,       // ruedas más resistentes
        ChassisArmor,     // chasis y panel más resistentes
        ShieldedCables,   // menos cortes de cable
        PanelBrush        // cepillo: cada limpieza manual (C) quita más polvo
    }

    /// <summary>Definición de una mejora del taller.</summary>
    [Serializable]
    public class RobotUpgradeDefinition
    {
        public string id;
        public string name;
        [TextArea] public string description;
        public UpgradeEffect effect;
        [Tooltip("Cuánto cambia por nivel (Wh, m², m/s, o fracción de reducción de daño).")]
        public float valuePerLevel;
        [Min(1)] public int maxLevel = 3;
        [Tooltip("Coste del nivel 1. El nivel N cuesta N veces esto.")]
        public ResourceAmount[] baseCost;

        public RobotUpgradeDefinition(string id, string name, string description, UpgradeEffect effect,
            float valuePerLevel, int maxLevel, params ResourceAmount[] baseCost)
        {
            this.id = id;
            this.name = name;
            this.description = description;
            this.effect = effect;
            this.valuePerLevel = valuePerLevel;
            this.maxLevel = maxLevel;
            this.baseCost = baseCost;
        }
    }

    /// <summary>
    /// Mejoras instaladas en el robot. Vive en el robot (las mejoras viajan con él) y aplica los
    /// efectos sobre batería, paneles, motores y blindajes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotUpgrades : MonoBehaviour
    {
        private readonly Dictionary<string, int> _levels = new Dictionary<string, int>();

        /// <summary>Mejora instalada: definición y nuevo nivel.</summary>
        public event Action<RobotUpgradeDefinition, int> Installed;

        public int GetLevel(RobotUpgradeDefinition def)
        {
            int l;
            return _levels.TryGetValue(def.id, out l) ? l : 0;
        }

        public bool IsMaxed(RobotUpgradeDefinition def)
        {
            return GetLevel(def) >= def.maxLevel;
        }

        /// <summary>Cobra el siguiente nivel al almacén de la base y lo instala.</summary>
        public bool TryInstall(RobotUpgradeDefinition def, BaseInventory inventory)
        {
            if (IsMaxed(def)) return false;
            int next = GetLevel(def) + 1;
            if (!inventory.TrySpend(def.baseCost, next)) return false;

            _levels[def.id] = next;
            Apply(def, next);
            if (Installed != null) Installed(def, next);
            return true;
        }

        /// <summary>Aplica el efecto de pasar a <paramref name="level"/>.</summary>
        private void Apply(RobotUpgradeDefinition def, int level)
        {
            float v = def.valuePerLevel;
            var battery = GetComponent<MarsRobot.Energy.BatterySystem>();
            var dusting = GetComponent<MarsRobot.Energy.DustingSystem>();
            var controller = GetComponent<RobotController>();
            var health = GetComponent<RobotHealthSystem>();

            switch (def.effect)
            {
                case UpgradeEffect.BatteryCapacity:
                    if (battery != null) battery.AddCapacity(v);
                    break;
                case UpgradeEffect.SolarPanelArea:
                    if (dusting != null) dusting.PanelAreaM2 += v;
                    break;
                case UpgradeEffect.MotorSpeed:
                    if (controller != null) controller.MaxSpeed += v;
                    break;
                case UpgradeEffect.WheelArmor:
                    if (health != null)
                    {
                        // Cada nivel: menos daño recibido y más integridad máxima.
                        health.SetArmor(ModuleCategory.Locomotion, Mathf.Pow(1f - v, level));
                        health.SetMaxIntegrityMultiplier(ModuleCategory.Locomotion, 1f + 0.25f * level);
                    }
                    break;
                case UpgradeEffect.ChassisArmor:
                    if (health != null)
                    {
                        float armor = Mathf.Pow(1f - v, level);
                        health.SetArmor(RobotModuleId.Chassis, armor);
                        health.SetArmor(RobotModuleId.SolarPanel, armor);
                        health.SetMaxIntegrityMultiplier(ModuleCategory.Structure, 1f + 0.25f * level);
                    }
                    break;
                case UpgradeEffect.PanelBrush:
                    if (dusting != null) dusting.ManualCleanAmount += v;
                    break;
                case UpgradeEffect.ShieldedCables:
                    if (health != null)
                    {
                        health.CableCutChanceMultiplier = Mathf.Pow(1f - v, level);
                        health.SetArmor(ModuleCategory.Wiring, Mathf.Pow(1f - v, level));
                    }
                    break;
            }
        }

        /// <summary>Mejoras disponibles por defecto en el taller.</summary>
        public static List<RobotUpgradeDefinition> DefaultCatalog()
        {
            return new List<RobotUpgradeDefinition>
            {
                new RobotUpgradeDefinition("battery", "Batería ampliada",
                    "+1000 Wh por nivel: más autonomía lejos de la base.",
                    UpgradeEffect.BatteryCapacity, 1000f, 3,
                    new ResourceAmount(ResourceType.MetalParts, 2), new ResourceAmount(ResourceType.Wiring, 2)),
                new RobotUpgradeDefinition("panels", "Paneles plegables",
                    "+1 m² de panel solar por nivel: carga más rápido al sol.",
                    UpgradeEffect.SolarPanelArea, 1f, 3,
                    new ResourceAmount(ResourceType.SolarCells, 2), new ResourceAmount(ResourceType.MetalParts, 1)),
                new RobotUpgradeDefinition("motors", "Motores reforzados",
                    "+1 m/s de velocidad máxima por nivel.",
                    UpgradeEffect.MotorSpeed, 1f, 2,
                    new ResourceAmount(ResourceType.MetalParts, 3), new ResourceAmount(ResourceType.Wiring, 1)),
                new RobotUpgradeDefinition("wheels", "Ruedas reforzadas",
                    "-25 % de daño y +25 % de resistencia en ruedas por nivel.",
                    UpgradeEffect.WheelArmor, 0.25f, 3,
                    new ResourceAmount(ResourceType.MetalParts, 3)),
                new RobotUpgradeDefinition("chassis", "Blindaje de chasis",
                    "-25 % de daño en chasis y panel por nivel.",
                    UpgradeEffect.ChassisArmor, 0.25f, 2,
                    new ResourceAmount(ResourceType.MetalParts, 4)),
                new RobotUpgradeDefinition("brush", "Cepillo de paneles",
                    "+15 % de polvo quitado en cada limpieza manual (tecla C) por nivel.",
                    UpgradeEffect.PanelBrush, 0.15f, 3,
                    new ResourceAmount(ResourceType.MetalParts, 1), new ResourceAmount(ResourceType.Wiring, 1)),
                new RobotUpgradeDefinition("cables", "Cables blindados",
                    "Mitad de probabilidad de corte de cable por nivel.",
                    UpgradeEffect.ShieldedCables, 0.5f, 2,
                    new ResourceAmount(ResourceType.Wiring, 3), new ResourceAmount(ResourceType.MetalParts, 1)),
            };
        }
    }
}
