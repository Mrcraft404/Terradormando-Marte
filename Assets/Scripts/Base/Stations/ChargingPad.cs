using MarsRobot.Core;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Plataforma de carga junto al cohete: si el robot se para encima, recarga la batería con
    /// la energía de la base y un soplador le quita el polvo de los paneles. No abre menú.
    /// </summary>
    public sealed class ChargingPad : BaseStation
    {
        [Tooltip("Potencia de carga (W, en tiempo de juego).")]
        [SerializeField, Min(0f)] private float chargeWatts = 1500f;

        [Tooltip("Polvo que se limpia por segundo real mientras el robot está encima.")]
        [SerializeField, Range(0f, 1f)] private float dustCleanPerSecond = 0.15f;

        [Tooltip("Velocidad máxima (m/s) a la que se considera que el robot está aparcado.")]
        [SerializeField, Min(0f)] private float maxParkedSpeed = 0.6f;

        private string _status = "";

        public override bool HasMenu
        {
            get { return false; }
        }

        public override string Prompt
        {
            get { return _status; }
        }

        public override void WhileRobotNearby(RobotBaseLink robot, float deltaTime)
        {
            if (robot.Controller != null && robot.Controller.CurrentSpeed > maxParkedSpeed)
            {
                _status = "Plataforma de carga: detente encima para cargar.";
                return;
            }

            if (robot.Battery != null)
            {
                robot.Battery.AddEnergy(MarsTime.WattsToWh(chargeWatts, deltaTime * MarsTime.TimeScale));
            }
            if (robot.Dusting != null && robot.Dusting.Dust01 > 0f)
            {
                robot.Dusting.CleanPanels(dustCleanPerSecond * deltaTime);
            }

            float charge = robot.Battery != null ? robot.Battery.Charge01 : 1f;
            _status = charge >= 0.999f
                ? "Plataforma de carga: batería llena."
                : "Cargando batería… " + Mathf.RoundToInt(charge * 100f) + " %";
        }

        public override void DrawMenu(RobotBaseLink robot) { }
    }
}
