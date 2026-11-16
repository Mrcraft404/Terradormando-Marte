using System.Collections.Generic;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Taller de modificaciones: el robot entra bajo el arco y aquí se le instalan mejoras
    /// (pagadas con materiales del almacén) y se le hace una reparación completa gratis.
    /// </summary>
    public class UpgradeStation : BaseStation
    {
        [SerializeField] private List<RobotUpgradeDefinition> catalog = RobotUpgrades.DefaultCatalog();

        private string _lastResult = "";

        public override void DrawMenu(RobotBaseLink robot)
        {
            RobotUpgrades upgrades = robot.Upgrades;
            BaseInventory inv = Owner.Inventory;

            // Reparación completa.
            RobotHealthSystem health = robot.Health;
            if (health != null)
            {
                GUILayout.BeginHorizontal();
                GUI.enabled = health.StructuralIntegrity01 < 0.999f;
                if (GUILayout.Button(Tr.T("R. Reparación completa (gratis en la base)"), GUILayout.Width(330)) ||
                    (GUI.enabled && KeyPressed(KeyCode.R)))
                {
                    health.RepairAll();
                    _lastResult = Tr.T("Robot reparado al 100 %.");
                }
                GUI.enabled = true;
                GUILayout.Label(Tr.T("Integridad: ") + Mathf.RoundToInt(health.StructuralIntegrity01 * 100f) + " %");
                GUILayout.EndHorizontal();
                GUILayout.Space(6);
            }

            GUILayout.Label(Tr.T("<b>Mejoras</b> (el nivel N cuesta N veces el precio base):"));
            for (int i = 0; i < catalog.Count; i++)
            {
                RobotUpgradeDefinition def = catalog[i];
                int level = upgrades.GetLevel(def);
                bool maxed = upgrades.IsMaxed(def);
                bool affordable = !maxed && inv.Has(def.baseCost, level + 1);

                GUILayout.BeginHorizontal();
                GUI.enabled = affordable;
                string label = (i + 1) + ". " + def.name + "  [" + level + "/" + def.maxLevel + "]";
                if (GUILayout.Button(label, GUILayout.Width(260)) || (GUI.enabled && NumberPressed(i)))
                {
                    _lastResult = upgrades.TryInstall(def, inv)
                        ? def.name + Tr.T(" instalada (nivel ") + upgrades.GetLevel(def) + ")."
                        : Tr.T("No se pudo instalar ") + def.name + ".";
                }
                GUI.enabled = true;
                string cost = maxed ? "<color=#80ff80>nivel máximo</color>"
                    : BaseInventory.Describe(def.baseCost, level + 1) + (affordable ? "" : Tr.T("  <color=#ff8080>faltan recursos</color>"));
                GUILayout.Label(def.description + Tr.T("\n   Coste: ") + cost);
                GUILayout.EndHorizontal();
            }

            if (_lastResult.Length > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label("<b>" + _lastResult + "</b>");
            }
        }

        private static bool NumberPressed(int index)
        {
            return KeyPressed(KeyCode.Alpha1 + index);
        }

        private static bool KeyPressed(KeyCode key)
        {
            Event e = Event.current;
            return e.type == EventType.KeyDown && e.keyCode == key;
        }
    }
}
