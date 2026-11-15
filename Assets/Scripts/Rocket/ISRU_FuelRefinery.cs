using MarsRobot.Core;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Planta ISRU con reactor de Sabatier (uso de recursos locales):
    ///   electrólisis   2 H₂O → 2 H₂ + O₂
    ///   Sabatier       CO₂ + 4 H₂ → CH₄ + 2 H₂O
    /// En el juego: 2 de agua → 1 de metano (propelente del cohete) + 2 de oxígeno (respiración y
    /// futuro huerto). El agua sale del hielo que trae el rover o del taladro de hielo.
    /// Funciona como una estructura más de la red de energía (consume 1,5 kW).
    /// </summary>
    public sealed class ISRU_FuelRefinery : PlacedStructure
    {
        public override void DrawMenu(RobotBaseLink robot)
        {
            base.DrawMenu(robot);

            RocketBaseManager mgr;
            if (!GameServices.TryGet(out mgr)) return;
            GUILayout.Space(6);
            int methane = Owner.Inventory.Get(ResourceType.Methane);
            GUILayout.Label("<b>Tanques de propelente:</b> " + methane + " / " + mgr.PropellantForLaunch + " de metano (" +
                            Mathf.RoundToInt(mgr.Propellant01 * 100f) + " %)");
            GUILayout.Label("Con los tanques llenos, el cohete nodriza podría volver a despegar o reabastecer a la nave de la tripulación.");
        }
    }
}
