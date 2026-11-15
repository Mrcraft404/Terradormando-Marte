using MarsRobot.Base;
using MarsRobot.Energy;
using MarsRobot.Items;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Interaction
{
    /// <summary>
    /// Quien interactúa (el robot). Da acceso a sus sistemas a los objetos interactuables:
    /// inventario, herramientas, salud, batería y conexión con la base.
    /// </summary>
    public abstract class Interactor : MonoBehaviour
    {
        /// <summary>El interactor del jugador (lo usan los IInteractable para dar mensajes con contexto).</summary>
        public static Interactor Local { get; protected set; }

        public RobotInventory Inventory { get; private set; }
        public RoverToolController Tools { get; private set; }
        public RobotHealthSystem Health { get; private set; }
        public BatterySystem Battery { get; private set; }
        public RobotController Controller { get; private set; }
        public RobotBaseLink BaseLink { get; private set; }

        protected virtual void Awake()
        {
            Inventory = GetComponent<RobotInventory>();
            Tools = GetComponent<RoverToolController>();
            Health = GetComponent<RobotHealthSystem>();
            Battery = GetComponent<BatterySystem>();
            Controller = GetComponent<RobotController>();
            BaseLink = GetComponent<RobotBaseLink>();
        }

        /// <summary>Mensaje breve en pantalla (resultado de una interacción).</summary>
        public abstract void ShowFeedback(string text);
    }
}
