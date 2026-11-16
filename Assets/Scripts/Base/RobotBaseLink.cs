using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Conecta el robot con las máquinas de la base:
    ///  • Detecta la máquina más cercana y muestra "F: usar …".
    ///  • F abre/cierra su menú (mientras está abierto el robot no se mueve y el ratón queda libre).
    ///  • Las máquinas pasivas (plataforma de carga) actúan solas al estar encima.
    ///  • Cerca de la base muestra el contenido del almacén.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotBaseLink : MonoBehaviour
    {
        public RobotController Controller { get; private set; }
        public RobotHealthSystem Health { get; private set; }
        public BatterySystem Battery { get; private set; }
        public DustingSystem Dusting { get; private set; }
        public RobotUpgrades Upgrades { get; private set; }

        private const int MenuWindowId = 41730; // identificador fijo de la ventana IMGUI del menú
        private bool _menuOpen;
        private BuildModeController _builder;
        private MarsRobot.Interaction.PlayerInteractor _interactor;
        private int _openedThisFrame = -1;
        private RobotRepairController _repair;
        private RocketBase _base;
        private BaseStation _nearest;
        private BaseStation _open;
        private Rect _window = new Rect(0, 0, 720, 460);
        private GUIStyle _promptStyle;

        /// <summary>Máquina con el menú abierto (null si ninguna).</summary>
        public BaseStation OpenStation
        {
            get { return _open; }
        }

        /// <summary>True si el robot está dentro del radio de la base.</summary>
        public bool IsAtBase { get; private set; }

        private void Awake()
        {
            Controller = GetComponent<RobotController>();
            Health = GetComponent<RobotHealthSystem>();
            Battery = GetComponent<BatterySystem>();
            Dusting = GetComponent<DustingSystem>();
            Upgrades = GetComponent<RobotUpgrades>();
            if (Upgrades == null) Upgrades = gameObject.AddComponent<RobotUpgrades>();
            _repair = GetComponent<RobotRepairController>();
            _builder = GetComponent<BuildModeController>();
            _interactor = GetComponent<MarsRobot.Interaction.PlayerInteractor>();
        }

        /// <summary>True si hay un menú de máquina abierto (el modo construcción no se activa entonces).</summary>
        public bool IsMenuOpen
        {
            get { return _menuOpen; }
        }

        private void Update()
        {
            if (_base == null && !GameServices.TryGet(out _base)) return;

            Vector3 pos = transform.position;
            // "En la base" = dentro del alcance de la red (ahí se muestra el almacén y la energía).
            IsAtBase = HorizontalDistance(pos, _base.transform.position) < RocketBase.BuildRadius;

            // Máquinas al alcance: las pasivas actúan; la más cercana con menú se puede usar.
            _nearest = null;
            float best = float.MaxValue;
            for (int i = 0; i < _base.Stations.Count; i++)
            {
                BaseStation s = _base.Stations[i];
                float d = s.HorizontalDistance(pos);
                if (d > s.InteractRadius) continue;
                s.WhileRobotNearby(this, Time.deltaTime);
                if (d < best)
                {
                    best = d;
                    _nearest = s;
                }
            }

            // Si nos alejamos con el menú abierto, o la máquina se desmontó, se cierra.
            // (Un menú abierto apuntando desde algo más lejos no se cierra hasta alejarse de verdad.)
            if (_menuOpen && (_open == null || _open.HorizontalDistance(pos) > _open.InteractRadius + 5f)) CloseMenu();

            // En modo construcción la F no abre menús (para no mezclar acciones).
            if (_builder != null && _builder.IsActive) return;

            // Si el punto de mira apunta a algo, esa F ya la ha gestionado PlayerInteractor.
            if (_interactor == null) _interactor = GetComponent<MarsRobot.Interaction.PlayerInteractor>();
            bool aimed = _interactor != null && _interactor.HasTarget;
            if (RobotInput.InteractPressed && _openedThisFrame != Time.frameCount && (_menuOpen || !aimed))
            {
                if (_open != null) CloseMenu();
                else if (_nearest != null && _nearest.HasMenu) OpenMenu(_nearest);
            }
            else if (_open != null && RobotInput.ReleaseCursorPressed)
            {
                CloseMenu();
            }
        }

        /// <summary>Abre el menú de una máquina (lo usa la interacción por punto de mira).</summary>
        public void OpenMenuFor(BaseStation station)
        {
            if (station == null || _menuOpen) return;
            OpenMenu(station);
            _openedThisFrame = Time.frameCount;
        }

        private void OpenMenu(BaseStation station)
        {
            _open = station;
            _menuOpen = true;
            if (Controller != null) Controller.InputEnabled = false;
            if (_repair != null) _repair.enabled = false; // la R del menú no debe activar la reparación manual
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _window.x = (Screen.width - _window.width) * 0.5f;
            _window.y = (Screen.height - _window.height) * 0.5f;
        }

        /// <summary>Cierra el menú abierto (botón "Cerrar" de las pantallas propias).</summary>
        public void RequestClose()
        {
            if (_open != null) CloseMenu();
        }

        private void CloseMenu()
        {
            _open = null;
            _menuOpen = false;
            if (Controller != null) Controller.InputEnabled = true;
            if (_repair != null) _repair.enabled = true;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // ------------------------------------------------------------------
        // Interfaz provisional (IMGUI)
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (_base == null) return;
            GUI.skin.label.richText = true;
            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = true };
            }

            // Aviso "F: usar …" o estado de la plataforma.
            if (_open == null && _nearest != null && _nearest.Prompt.Length > 0)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 230, Screen.height - 90, 460, 40), _nearest.Prompt, _promptStyle);
            }

            // Almacén (arriba a la derecha) cuando estamos en la base.
            if (IsAtBase) DrawInventory();

            if (_open != null)
            {
                if (_open.DrawsFullScreen) _open.DrawFullScreen(this);
                else _window = GUILayout.Window(MenuWindowId, _window, DrawWindow, _open.DisplayName);
            }
        }

        private void DrawWindow(int id)
        {
            _open.DrawMenu(this);
            GUILayout.FlexibleSpace();
            GUILayout.Label(Tr.T("F o Esc: cerrar · números: elegir opción"));
            GUI.DragWindow();
        }

        private void DrawInventory()
        {
            BaseInventory inv = _base.Inventory;
            var sb = new System.Text.StringBuilder(Tr.T("<b>Almacén de la base</b>\n"));
            foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
            {
                int n = inv.Get(t);
                int cap = inv.GetCapacity(t);
                if (n <= 0 && cap < 0) continue;
                sb.Append(BaseInventory.Label(t)).Append(": ").Append(n);
                if (cap >= 0) sb.Append(" / ").Append(cap);
                sb.Append('\n');
            }

            // Red de energía y preparación para la tripulación.
            BaseGrid grid = _base.Grid;
            if (grid != null)
            {
                sb.Append(Tr.T("\n<b>Energía</b>\n+")).Append(grid.GenerationWatts.ToString("0")).Append(" W  −")
                  .Append(grid.DemandWatts.ToString("0")).Append(Tr.T(" W\nBaterías: "))
                  .Append(grid.CapacityWh > 0f ? Mathf.RoundToInt(grid.StoredWh / grid.CapacityWh * 100f) : 0).Append(" %");
                if (grid.SupplyRatio < 0.99f) sb.Append(Tr.T("\n<color=#ff8080>¡Falta energía! (")).Append(Mathf.RoundToInt(grid.SupplyRatio * 100f)).Append(" %)</color>");
                if (grid.Count(StructureKind.CommsAntenna) > 0)
                {
                    sb.Append(Tr.T("\n\n<b>Preparación:</b> ")).Append(ColonyObjectives.CompletedCount(_base)).Append('/').Append(ColonyObjectives.Total);
                }
            }

            const float w = 240f;
            GUI.Box(new Rect(Screen.width - w - 10, 10, w, 400), "");
            GUI.Label(new Rect(Screen.width - w, 14, w - 16, 396), sb.ToString());
        }
    }
}
