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
        private GUIStyle _badge;

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
                ContainerAccess.Create(ship, Tr.T("bahía de carga del robot"), Vector3.zero, () => inv.Bay,
                    () => c.RobotDocked, () => robot.transform.position + Vector3.up, 2.6f);
            }

            c.SetAstronaut(true);
            c.Show("marti_astronaut_start");
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
                    ? "marti_robot_outside"
                    : "marti_robot_to_hangar");
                return;
            }
            SetAstronaut(!AstronautActive);
            Show(AstronautActive ? "marti_now_astronaut" : "marti_now_robot");
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

        /// <summary>Los consejos los da Marti, la mascota guía (bocadillo a la izquierda).</summary>
        private void Show(string text)
        {
            MarsRobot.UI.MascotGuide.Say(text);
        }

        private void OnGUI()
        {
            if (CraftingWindow.IsOpen) return;
            if (_badge == null)
            {
                _badge = new GUIStyle(GUI.skin.box) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
                _badge.normal.textColor = Color.white;
            }
            bool docked = RobotDocked;
            System.Func<string, string, string> L = MarsRobot.Localization.Localizer.L;
            string who = AstronautActive ? "<color=#ffb066>" + L("ASTRONAUTA", "ASTRONAUT") + "</color>" : "<color=#66d9ff>ROBOT XR-1</color>";
            string hint = docked ? L("P: cambiar a ", "P: switch to ") + (AstronautActive ? "robot" : L("astronauta", "astronaut"))
                                 : (AstronautActive ? L("robot fuera del hangar", "robot is outside the hangar") : L("vuelve al hangar para cambiar", "go back to the hangar to switch"));
            // Abajo en el centro (arriba están el clima espacial y los avisos).
            GUI.Box(new Rect(Screen.width * 0.5f - 230, Screen.height - 40, 460, 30), MarsRobot.Localization.Localizer.L("Controlas: ", "You control: ") + who + "   ·   " + hint, _badge);
        }
    }
}
