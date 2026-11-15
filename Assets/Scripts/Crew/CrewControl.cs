using MarsRobot.Base;
using MarsRobot.Base.Ship;
using MarsRobot.Crafting;
using MarsRobot.Items;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Crew
{
    /// <summary>
    /// Quién controla el jugador: el astronauta (dentro de la nave) o el robot (que el astronauta
    /// teleopera). Tecla P para cambiar, solo con el robot dentro del hangar (nivel 1).
    /// Con el astronauta activo, el robot queda aparcado: su entrada, cámara y avisos se apagan.
    /// </summary>
    public sealed class CrewControl : MonoBehaviour
    {
        public static CrewControl Instance { get; private set; }

        private Astronaut _astronaut;
        private RobotController _robot;
        private Camera _robotCam;
        private AudioListener _robotListener;
        private Behaviour[] _robotOnly;
        private Transform _ship;
        private ShipElevator _elevator;
        private string _message = "";
        private float _messageTime = -10f;
        private GUIStyle _badge, _msg;

        public bool AstronautActive { get; private set; }

        public Astronaut Astronaut
        {
            get { return _astronaut; }
        }

        /// <summary>Crea al astronauta en el nivel de talleres, los montacargas, el acceso a la bahía del robot y el control.</summary>
        public static CrewControl Setup(RocketBase rocketBase, GameObject robot)
        {
            Transform ship = rocketBase.Mothership.transform;
            ShipLift.CreateAll(ship);

            Vector3 spawn = ship.TransformPoint(ShipLayout.OnLevel(ShipModel.WorkshopLevel, ShipLayout.CrewSpawn) + Vector3.up * 0.1f);
            Astronaut astronaut = Astronaut.Create(spawn, ship.rotation * Quaternion.Euler(0f, 180f, 0f)); // mirando a las máquinas

            var go = new GameObject("Control de la tripulación");
            var c = go.AddComponent<CrewControl>();
            c._astronaut = astronaut;
            c._ship = ship;
            c._elevator = rocketBase.Elevator;
            c._robot = robot.GetComponent<RobotController>();
            c._robotCam = robot.GetComponentInChildren<Camera>(true);
            c._robotListener = c._robotCam != null ? c._robotCam.GetComponent<AudioListener>() : null;
            c._robotOnly = new Behaviour[]
            {
                robot.GetComponent<MarsRobot.Interaction.PlayerInteractor>(),
                robot.GetComponent<MarsRobot.Interaction.InteractionUI>(),
                robot.GetComponent<RobotBaseLink>(),
                robot.GetComponent<RobotToolbelt>(),   // barra de carga y atajos del robot
            };

            // El astronauta puede abrir la bahía de carga del robot cuando está aparcado en el hangar.
            var inv = robot.GetComponent<RobotInventory>();
            if (inv != null)
            {
                ContainerAccess.Create(ship, "bahía de carga del robot", Vector3.zero, () => inv.Bay,
                    () => c.RobotDocked, () => robot.transform.position + Vector3.up, 2.6f);
            }

            c.SetAstronaut(true);
            c.Show("Eres el astronauta. Fabrica en los talleres (nivel 2) y baja al hangar (nivel 1) para pilotar el robot con P.");
            return c;
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            RobotInput.Suspended = false;
        }

        /// <summary>El robot está dentro del hangar (y no en el ascensor): se puede cambiar de control.</summary>
        public bool RobotDocked
        {
            get
            {
                if (_robot == null || _ship == null) return false;
                if (_elevator != null && _elevator.IsMoving) return false;
                return ShipModel.IsInsideHangar(_ship.InverseTransformPoint(_robot.transform.position));
            }
        }

        private void Update()
        {
            if (AstronautActive) RobotInput.Suspended = true; // aunque otro sistema lo reactive
            if (CraftingWindow.IsOpen || ShipLift.MenuOpen || Time.timeScale < 0.01f) return;
            if (!Input.GetKeyDown(KeyCode.P)) return;

            if (!RobotDocked)
            {
                Show(AstronautActive
                    ? "El robot no está en el hangar: solo puedes tomar el control cuando está acoplado dentro."
                    : "Lleva el robot al hangar (sube con el ascensor) para volver al astronauta.");
                return;
            }
            SetAstronaut(!AstronautActive);
            Show(AstronautActive ? "Control: astronauta." : "Control: robot. Sal por la puerta de carga y baja con el ascensor (F).");
        }

        public void SetAstronaut(bool astronaut)
        {
            AstronautActive = astronaut;
            RobotInput.Suspended = astronaut;
            if (_robot != null) _robot.InputEnabled = !astronaut;
            for (int i = 0; i < _robotOnly.Length; i++) if (_robotOnly[i] != null) _robotOnly[i].enabled = !astronaut;
            if (_robotCam != null) _robotCam.enabled = !astronaut;
            if (_robotListener != null) _robotListener.enabled = !astronaut;
            _astronaut.SetActive(astronaut);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Show(string text)
        {
            _message = text;
            _messageTime = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (CraftingWindow.IsOpen) return;
            if (_badge == null)
            {
                _badge = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
                _badge.normal.textColor = Color.white;
                _msg = new GUIStyle(GUI.skin.box) { fontSize = 16, alignment = TextAnchor.MiddleCenter, wordWrap = true };
                _msg.normal.textColor = new Color(1f, 0.9f, 0.7f);
            }
            bool docked = RobotDocked;
            string who = AstronautActive ? "<color=#ffb066>ASTRONAUTA</color>" : "<color=#66d9ff>ROBOT XR-1</color>";
            string hint = docked ? "P: cambiar a " + (AstronautActive ? "robot" : "astronauta") : (AstronautActive ? "robot fuera del hangar" : "vuelve al hangar para cambiar");
            // Abajo en el centro (arriba están el clima espacial y los avisos).
            GUI.Box(new Rect(Screen.width * 0.5f - 230, Screen.height - 40, 460, 30), "Controlas: " + who + "   ·   " + hint, _badge);
            if (Time.unscaledTime - _messageTime < 6f)
                GUI.Box(new Rect(Screen.width * 0.5f - 330, Screen.height - 210, 660, 48), _message, _msg);
        }
    }
}
