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
            _tab = GUILayout.Toolbar(_tab, new[] { Tr.T("Clima espacial"), Tr.T("Estado de la base"), Tr.T("Bitácora") }, GUILayout.Width(480));
            GUILayout.Space(6);

            if (_tab == 0)
            {
                SpaceWeatherManager sw;
                if (GameServices.TryGet(out sw)) SpaceWeatherHUD.DrawStatusPanel(sw);
                else GUILayout.Label(Tr.T("Sin enlace con el sistema de clima espacial."));
                return;
            }

            if (_tab == 1)
            {
                RocketBaseManager mgr;
                if (GameServices.TryGet(out mgr))
                {
                    GUILayout.Label(mgr.DescribeShielding());
                    GUILayout.Label(Tr.T("Propelente: ") + Owner.Inventory.Get(ResourceType.Methane) + " / " + mgr.PropellantForLaunch + Tr.T(" de metano"));
                }
                GUILayout.Label(Tr.T("Energía: +") + Owner.Grid.GenerationWatts.ToString("0") + " W / −" + Owner.Grid.DemandWatts.ToString("0") +
                                Tr.T(" W · baterías ") + (Owner.Grid.CapacityWh > 0f ? Mathf.RoundToInt(Owner.Grid.StoredWh / Owner.Grid.CapacityWh * 100f) : 0) + " %");
                GUILayout.Label(Tr.T("Preparación para la tripulación: ") + ColonyObjectives.CompletedCount(Owner) + "/" + ColonyObjectives.Total +
                                Tr.T(" (detalle en la antena de comunicaciones)"));
                GUILayout.Space(6);
                GUILayout.Label(Tr.T("Cierra este menú y pulsa <b>K</b> para inspeccionar la nave: vista de corte, los 5 niveles, etiquetas y cotas."));
                return;
            }

            LogTerminal.DrawEntries(14);
        }
    }
}
