using MarsRobot.Energy;
using MarsRobot.Items;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Crafting
{
    /// <summary>Efecto de los consumibles al usarlos (todos actúan sobre el robot).</summary>
    public static class ItemEffects
    {
        public static bool IsUsable(ItemData item)
        {
            if (item == null) return false;
            string id = item.itemID;
            return id == ItemDatabase.RepairKit || id == ItemDatabase.SpareWheel || id == ItemDatabase.CleaningBrush || id == ItemDatabase.PowerCell;
        }

        /// <summary>Aplica el efecto. Devuelve false (y el motivo) si no tuvo efecto: así no se gasta.</summary>
        public static bool TryUse(ItemData item, out string message)
        {
            message = "";
            if (!IsUsable(item))
            {
                message = "Este objeto no se puede usar directamente.";
                return false;
            }
            var health = Object.FindAnyObjectByType<RobotHealthSystem>();
            if (health == null)
            {
                message = "No hay robot.";
                return false;
            }

            switch (item.itemID)
            {
                case ItemDatabase.RepairKit:
                {
                    RobotModule worst = null;
                    foreach (RobotModule m in health.Modules)
                        if (worst == null || m.Integrity01 < worst.Integrity01) worst = m;
                    if (worst == null || worst.Integrity01 >= 0.999f)
                    {
                        message = "El robot no tiene daños.";
                        return false;
                    }
                    health.RepairModule(worst.Id);
                    message = "Reparado: " + worst.Definition.displayName + ".";
                    return true;
                }
                case ItemDatabase.SpareWheel:
                {
                    bool any = false;
                    foreach (RobotModule m in health.Modules)
                    {
                        if (m.Category != ModuleCategory.Locomotion || m.Integrity01 >= 0.999f) continue;
                        health.RepairModule(m.Id);
                        any = true;
                    }
                    message = any ? "Ruedas sustituidas: movilidad al 100 %." : "Las ruedas ya están como nuevas.";
                    return any;
                }
                case ItemDatabase.CleaningBrush:
                {
                    var dust = health.GetComponent<DustingSystem>();
                    if (dust == null || dust.Dust01 < 0.01f)
                    {
                        message = "Los paneles ya están limpios.";
                        return false;
                    }
                    dust.CleanPanels(1f);
                    message = "Paneles solares limpios.";
                    return true;
                }
                default:
                {
                    var battery = health.GetComponent<BatterySystem>();
                    if (battery == null || battery.Charge01 > 0.98f)
                    {
                        message = "La batería del robot ya está llena.";
                        return false;
                    }
                    battery.AddEnergy(battery.CapacityWh * 0.5f);
                    message = "Batería del robot al " + Mathf.RoundToInt(battery.Charge01 * 100f) + " %.";
                    return true;
                }
            }
        }
    }
}
