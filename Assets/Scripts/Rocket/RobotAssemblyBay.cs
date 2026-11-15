using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Taller de ensamblaje del cohete (sustituye al antiguo taller de modificaciones).
    /// Dos pestañas:
    ///  • Módulos: cambiar las piezas intercambiables del rover (brazo delantero, cubierta, bahía).
    ///    Solo funciona con el rover aparcado bajo el arco del taller.
    ///  • Mejoras y reparación: lo del taller anterior (niveles de batería, motores, blindajes…).
    /// </summary>
    public sealed class RobotAssemblyBay : UpgradeStation
    {
        private int _tab;
        private string _message = "";
        private Vector2 _scroll;

        public override void DrawMenu(RobotBaseLink robot)
        {
            _tab = GUILayout.Toolbar(_tab, new[] { "Módulos del rover", "Mejoras y reparación" }, GUILayout.Width(420));
            GUILayout.Space(6);
            if (_tab == 1)
            {
                base.DrawMenu(robot);
                return;
            }
            DrawModules(robot);
        }

        private void DrawModules(RobotBaseLink robot)
        {
            var modules = robot.GetComponent<RobotModuleSystem>();
            if (modules == null)
            {
                GUILayout.Label("Este robot no admite módulos.");
                return;
            }

            bool docked = modules.IsDockedAtAssemblyBay();
            GUILayout.Label(docked ? "<color=#80ff80>Rover aparcado en el taller: puedes cambiar módulos.</color>"
                                   : "<color=#ffb060>Aparca el rover bajo el arco del taller para cambiar módulos.</color>");
            GUILayout.Label("Masa: chasis " + modules.Config.chassisMassKg.ToString("0") + " kg + módulos " +
                            modules.ModulesMassKg.ToString("0") + " kg · velocidad ×" + modules.SpeedMultiplier.ToString("0.00") +
                            " · consumo ×" + modules.PowerMultiplier.ToString("0.00"));

            // Ranuras actuales.
            DrawSlots(modules, RoverSlotType.FrontArm, modules.FrontSlotCount);
            DrawSlots(modules, RoverSlotType.TopDeck, modules.GetSlots(RoverSlotType.TopDeck).Length);
            DrawSlots(modules, RoverSlotType.InternalBay, modules.GetSlots(RoverSlotType.InternalBay).Length);

            GUILayout.Space(6);
            GUILayout.Label("<b>Catálogo</b> (la primera vez se fabrica con materiales del almacén; después es gratis):");
            _scroll = GUILayout.BeginScrollView(_scroll, GUILayout.Height(170));
            var cat = RobotModuleSystem.Catalog;
            for (int i = 0; i < cat.Count; i++)
            {
                RoverModuleDefinition d = cat[i];
                bool installed = modules.HasModule(d.type);
                GUILayout.BeginHorizontal();
                GUI.enabled = docked && !installed;
                if (GUILayout.Button("Instalar", GUILayout.Width(80))) _message = modules.TryEquip(d.type, Owner.Inventory);
                GUI.enabled = true;
                string cost = modules.Owns(d.type) ? "<color=#80ff80>en el taller</color>" : BaseInventory.Describe(d.cost);
                GUILayout.Label("<b>" + d.name + "</b> [" + RobotModuleSystem.SlotLabel(d.slot) + ", " + d.massKg.ToString("0") + " kg] — " +
                                d.description + "  (" + cost + ")");
                GUILayout.EndHorizontal();
            }
            GUILayout.EndScrollView();
            if (_message.Length > 0) GUILayout.Label("<b>" + _message + "</b>");
        }

        private void DrawSlots(RobotModuleSystem modules, RoverSlotType type, int count)
        {
            RoverModuleType[] slots = modules.GetSlots(type);
            GUILayout.BeginHorizontal();
            GUILayout.Label(RobotModuleSystem.SlotLabel(type) + ":", GUILayout.Width(130));
            for (int i = 0; i < count && i < slots.Length; i++)
            {
                RoverModuleType m = slots[i];
                RoverModuleDefinition d = RobotModuleSystem.Find(m);
                string label = d != null ? d.name + "  ✕" : "(vacío)";
                GUI.enabled = d != null;
                if (GUILayout.Button(label, GUILayout.Width(190)) && d != null) _message = modules.Unequip(m);
                GUI.enabled = true;
            }
            GUILayout.EndHorizontal();
        }
    }
}
