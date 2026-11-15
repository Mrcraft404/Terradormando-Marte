using MarsRobot.Core;
using MarsRobot.Dialogue;
using MarsRobot.World.SpaceWeather;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Terminal del cohete: estado del clima espacial (DONKI), propelente, blindaje radiológico,
    /// preparación para la tripulación y acceso a la bitácora.
    /// </summary>
    public sealed class RocketTerminalStation : BaseStation
    {
        private int _tab;

        public override void DrawMenu(RobotBaseLink robot)
        {
            _tab = GUILayout.Toolbar(_tab, new[] { "Clima espacial", "Estado de la base", "Bitácora" }, GUILayout.Width(480));
            GUILayout.Space(6);

            if (_tab == 0)
            {
                SpaceWeatherManager sw;
                if (GameServices.TryGet(out sw)) SpaceWeatherHUD.DrawStatusPanel(sw);
                else GUILayout.Label("Sin enlace con el sistema de clima espacial.");
                return;
            }

            if (_tab == 1)
            {
                RocketBaseManager mgr;
                if (GameServices.TryGet(out mgr))
                {
                    GUILayout.Label(mgr.DescribeShielding());
                    GUILayout.Label("Propelente: " + Owner.Inventory.Get(ResourceType.Methane) + " / " + mgr.PropellantForLaunch + " de metano");
                }
                GUILayout.Label("Energía: +" + Owner.Grid.GenerationWatts.ToString("0") + " W / −" + Owner.Grid.DemandWatts.ToString("0") +
                                " W · baterías " + (Owner.Grid.CapacityWh > 0f ? Mathf.RoundToInt(Owner.Grid.StoredWh / Owner.Grid.CapacityWh * 100f) : 0) + " %");
                GUILayout.Label("Preparación para la tripulación: " + ColonyObjectives.CompletedCount(Owner) + "/" + ColonyObjectives.Total +
                                " (detalle en la antena de comunicaciones)");
                GUILayout.Space(6);
                GUILayout.Label("Cierra este menú y pulsa <b>K</b> para inspeccionar la nave: vista de corte, los 5 niveles, etiquetas y cotas.");
                return;
            }

            LogTerminal.DrawEntries(14);
        }
    }
}
