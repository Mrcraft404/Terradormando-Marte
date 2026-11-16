using System.Collections.Generic;
using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Robot;
using MarsRobot.UI;
using MarsRobot.World.TerrainSystem;
using MarsRobot.World.Weather;
using UnityEngine;

namespace MarsRobot.Items
{
    /// <summary>
    /// Panel de gestión del robot (tecla I), estilo inventario de juego de supervivencia:
    ///  • Pestañas: Gestión (3 columnas) · Inventario de la Nave · Inventario del Robot · Estado del Robot · Ajustes.
    ///  • Inventarios separados con capacidad propia (nave 5000 kg, robot según chasis y módulos),
    ///    conectados por transferencia solo cuando el robot está junto a la nave.
    ///  • El robot en 3D, como un personaje, con su ficha (estado, energía, temperatura, ubicación, enlace).
    ///  • Estado por componente con Operativo / Desgaste / Necesita reparación, y Reparar · Diagnóstico · Calibrar.
    /// Interfaz IMGUI dibujada en un lienzo de 1600 × 900 que se escala a la pantalla.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InventoryUI : MonoBehaviour
    {
        public static string RobotName => Tr.T("Robot explorador XR-1");
        private const float LinkRange = 25f;

        private enum Tab { Overview, Ship, Robot, Status, Settings }
        private static string[] TabLabels => new[] { Tr.T("Gestión"), Tr.T("Inventario de la Nave"), Tr.T("Inventario del Robot"), Tr.T("Estado del Robot"), Tr.T("Ajustes") };

        private RobotInventory _inventory;
        private RobotController _controller;
        private RobotHealthSystem _health;
        private BatterySystem _battery;
        private RobotModuleSystem _modules;
        private RoverToolController _tools;
        private RobotPreviewRenderer _preview;

        private Tab _tab = Tab.Overview;
        private bool _hasShipSel;
        private ResourceType _shipSel;
        private ItemStack _robotSel;
        private RobotModule _moduleSel;
        private int _qtyMode = 1; // 0 = 1 u, 1 = 10 u, 2 = todo
        private string _message = "";
        private float _messageTime = -100f;
        private Vector2 _scrollShip, _scrollRobot, _scrollStatus, _scrollControls;
        private int _settingsPage; // 0 = opciones, 1 = controles

        /// <summary>Lista de controles por sección (tecla, acción). Mantener al día al añadir teclas.</summary>
        private static readonly string[][] Controls =
        {
            new[] { "#", Tr.T("Astronauta (dentro de la nave)") },
            new[] { "P", Tr.T("Cambiar entre astronauta y robot (solo con el robot en el hangar)") },
            new[] { Tr.T("WASD · Mayús · Espacio"), Tr.T("Caminar · correr · saltar") },
            new[] { "F", Tr.T("Usar máquina, armario o montacargas") },
            new[] { "I", Tr.T("Inventario personal y fabricación a mano") },
            new[] { "V", Tr.T("Primera / tercera persona") },

            new[] { "#", Tr.T("Movimiento del robot") },
            new[] { "W / S", Tr.T("Adelante / atrás") },
            new[] { "A / D", Tr.T("Izquierda / derecha (de lado, ruedas a 90°)") },
            new[] { "Q / E", Tr.T("Girar el robot") },
            new[] { Tr.T("Ratón"), Tr.T("Mirar (clic en la vista para capturar el ratón)") },
            new[] { "V", Tr.T("Primera / tercera persona") },
            new[] { Tr.T("Rueda del ratón"), Tr.T("Acercar o alejar la cámara (3ª persona)") },
            new[] { "Esc", Tr.T("Soltar el ratón / cerrar menús") },

            new[] { "#", Tr.T("Acciones del robot") },
            new[] { "F", Tr.T("Usar lo que apuntas o la máquina más cercana") },
            new[] { Tr.T("F (en el ascensor)"), Tr.T("Subir al hangar / bajar al suelo · llamar al ascensor") },
            new[] { Tr.T("F (mantener)"), Tr.T("Minar rocas, volcar regolito en la berma, descargar registros") },
            new[] { Tr.T("X (mantener, parado)"), Tr.T("Herramienta del brazo: taladro, pala o soplador") },
            new[] { "Z", Tr.T("Escáner geológico") },
            new[] { "L", Tr.T("Foco") },
            new[] { "C", Tr.T("Limpiar los paneles solares") },
            new[] { Tr.T("R (mantener)"), Tr.T("Reparar a mano el módulo seleccionado") },
            new[] { "T", Tr.T("Cambiar herramienta de reparación") },
            new[] { "G", Tr.T("Elegir el módulo a reparar") },

            new[] { "#", Tr.T("Paneles y menús") },
            new[] { "I", Tr.T("Panel de gestión (este panel)") },
            new[] { "J", Tr.T("Bitácora") },
            new[] { "K", Tr.T("Inspeccionar el cohete nodriza") },
            new[] { "B", Tr.T("Modo construcción") },
            new[] { "1–9", Tr.T("Elegir opción en el menú de una máquina") },

            new[] { "#", Tr.T("Modo construcción (B)") },
            new[] { "Tab", Tr.T("Cambiar de categoría") },
            new[] { "1–9", Tr.T("Elegir estructura") },
            new[] { "R", Tr.T("Girar la pieza 90°") },
            new[] { Tr.T("Clic izquierdo / Intro"), Tr.T("Construir") },
            new[] { "B / Esc", Tr.T("Salir") },

            new[] { "#", Tr.T("Inspección del cohete (K)") },
            new[] { Tr.T("Ratón izquierdo"), Tr.T("Rotar la vista") },
            new[] { Tr.T("Ratón derecho"), Tr.T("Desplazar la vista") },
            new[] { Tr.T("Rueda"), "Zoom" },
            new[] { "WASD / Q / E", Tr.T("Mover la cámara / bajar / subir") },
            new[] { Tr.T("Mayús"), Tr.T("Moverse más rápido") },
            new[] { Tr.T("1–5 / Mayús+1–5 / 0"), Tr.T("Ver hasta ese nivel / solo ese nivel / todos") },
            new[] { "C / L / M", Tr.T("Corte del casco / etiquetas / cotas") },
            new[] { "K / Esc", Tr.T("Salir") },

            new[] { "#", Tr.T("Diálogos") },
            new[] { Tr.T("Intro / Espacio"), Tr.T("Mostrar todo el texto / continuar") },
            new[] { "1–4", Tr.T("Elegir respuesta") },

            new[] { "#", Tr.T("Teclas de prueba (desarrollo)") },
            new[] { "M", Tr.T("Mapa grande (y viajar a otra zona)") },
            new[] { "F7", Tr.T("+10 de cada recurso") },
            new[] { "F8 / F9", Tr.T("Golpe fuerte / reparar todo") },
            new[] { "F10 / F11", Tr.T("Tormenta de polvo / evento de clima espacial") },
            new[] { "H", Tr.T("Ocultar el panel de depuración") },
        };

        private void DrawControls(Rect r)
        {
            const float rowH = 30f;
            Rect view = new Rect(r.x, r.y, r.width, r.height);
            _scrollControls = GUI.BeginScrollView(view, _scrollControls, new Rect(0, 0, view.width - 20, Controls.Length * (rowH + 2) + 10));
            float y = 0f;
            for (int i = 0; i < Controls.Length; i++)
            {
                string key = Controls[i][0], action = Controls[i][1];
                if (key == "#")
                {
                    y += i == 0 ? 0f : 8f;
                    GUI.Label(new Rect(4, y, view.width - 30, rowH), "<color=#33d9ff><b>" + action.ToUpperInvariant() + "</b></color>", HudTheme.Small);
                    y += rowH;
                    continue;
                }
                Rect row = new Rect(0, y, view.width - 24, rowH);
                HudTheme.Fill(row, HudTheme.Slot);
                // "Tecla" como una pastilla, al estilo de los atajos de teclado.
                Rect keyRect = new Rect(row.x + 8, row.y + 4, 230, rowH - 8);
                HudTheme.Frame(keyRect, new Color(0.05f, 0.3f, 0.42f, 0.95f), HudTheme.Accent, 1f);
                GUI.Label(keyRect, "<b>" + key + "</b>", HudTheme.Pill);
                GUI.Label(new Rect(row.x + 252, row.y, row.width - 260, rowH), action, HudTheme.Small);
                y += rowH + 2;
            }
            GUI.EndScrollView();
        }

        public bool IsOpen { get; private set; }

        private void Awake()
        {
            _inventory = GetComponent<RobotInventory>();
            _controller = GetComponent<RobotController>();
            _health = GetComponent<RobotHealthSystem>();
            _battery = GetComponent<BatterySystem>();
            _modules = GetComponent<RobotModuleSystem>();
            _tools = GetComponent<RoverToolController>();
        }

        private void Start()
        {
            var go = new GameObject("Vista previa del robot (panel)");
            _preview = go.AddComponent<RobotPreviewRenderer>();
            _preview.Setup(gameObject);
        }

        private void Update()
        {
            if (RobotInput.InventoryPressed) SetOpen(!IsOpen);
            else if (IsOpen && RobotInput.ReleaseCursorPressed) SetOpen(false);
        }

        public void SetOpen(bool open)
        {
            if (open == IsOpen) return;
            IsOpen = open;
            if (_preview != null) _preview.SetActive(open);
            var link = GetComponent<RobotBaseLink>();
            bool menuOpen = link != null && link.IsMenuOpen;
            if (open)
            {
                if (_controller != null) _controller.InputEnabled = false;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (!menuOpen)
            {
                if (_controller != null) _controller.InputEnabled = true;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                GameSettings.Save();
            }
        }

        private void OnDisable()
        {
            if (IsOpen) SetOpen(false);
        }

        private void Say(string text)
        {
            _message = text;
            _messageTime = Time.unscaledTime;
        }

        // ------------------------------------------------------------------
        // Datos de contexto
        // ------------------------------------------------------------------

        private RocketBase Ship
        {
            get
            {
                RocketBase b;
                return GameServices.TryGet(out b) ? b : null;
            }
        }

        private float DistanceToShip
        {
            get
            {
                RocketBase b = Ship;
                if (b == null) return float.MaxValue;
                Vector3 d = transform.position - b.transform.position;
                d.y = 0f;
                return d.magnitude;
            }
        }

        private bool Linked
        {
            get { return DistanceToShip <= LinkRange; }
        }

        // ------------------------------------------------------------------
        // Dibujo general
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!IsOpen || _inventory == null || _inventory.Bay == null) return;
            GUI.depth = -5;
            HudTheme.BeginScaled();

            // Fondo oscurecido y barra superior.
            HudTheme.Fill(new Rect(-400, -400, 2400, 1700), new Color(0.01f, 0.02f, 0.04f, 0.55f));
            DrawTopBar();
            DrawTabs();

            switch (_tab)
            {
                case Tab.Overview: DrawOverview(); break;
                case Tab.Ship: DrawShipTab(); DrawRobotCard(new Rect(1090, 116, 490, 760)); break;
                case Tab.Robot: DrawRobotTab(); DrawRobotCard(new Rect(1090, 116, 490, 760)); break;
                case Tab.Status: DrawStatusTab(); DrawRobotCard(new Rect(1090, 116, 490, 760)); break;
                default: DrawSettingsTab(); DrawRobotCard(new Rect(1090, 116, 490, 760)); break;
            }

            if (Time.unscaledTime - _messageTime < 4f && _message.Length > 0)
            {
                Rect m = new Rect(400, 846, 800, 34);
                HudTheme.Frame(m, new Color(0.02f, 0.2f, 0.3f, 0.95f), HudTheme.Accent);
                GUI.Label(m, "  " + _message, HudTheme.Small);
            }
            HudTheme.EndScaled();
        }

        private void DrawTopBar()
        {
            Rect bar = new Rect(0, 0, 1600, 56);
            HudTheme.Fill(bar, new Color(0.03f, 0.07f, 0.13f, 0.97f));
            HudTheme.Fill(new Rect(0, 55, 1600, 2), HudTheme.AccentDim);

            MarsEnvironmentSystem env;
            GameServices.TryGet(out env);
            float gameSeconds = Time.timeSinceLevelLoad * MarsTime.TimeScale;
            int sol = 1 + Mathf.FloorToInt(gameSeconds / MarsTime.SolSeconds);
            float hours = env != null ? env.SolTime01 * 24.66f : 12f;
            int hh = Mathf.FloorToInt(hours), mm = Mathf.FloorToInt((hours - hh) * 60f);
            string storm = env != null && env.IsDustStormActive ? Tr.T("FUERTE") : Tr.T("NINGUNA");
            if (MarsRobot.World.SpaceWeather.SpaceWeatherState.GlobalStormActive) storm = "GLOBAL";
            string title = RobotName.ToUpperInvariant() + Tr.T("  |  MARTE — SOL ") + sol + "  |  " + hh.ToString("00") + ":" + mm.ToString("00") + Tr.T("  |  TORMENTA DE POLVO: ") + storm;
            GUI.Label(new Rect(20, 10, 1000, 36), "<b>" + title + "</b>", HudTheme.Subtitle);

            // Contadores de recursos de la nave (como en el diseño).
            RocketBase b = Ship;
            if (b != null)
            {
                BaseInventory inv = b.Inventory;
                int minerals = inv.Get(ResourceType.Regolith) + inv.Get(ResourceType.Silicon) + inv.Get(ResourceType.Samples);
                int metals = inv.Get(ResourceType.Iron) + inv.Get(ResourceType.MetalParts);
                int water = inv.Get(ResourceType.Water) + inv.Get(ResourceType.Ice);
                float energy = b.Grid.CapacityWh > 0f ? b.Grid.StoredWh / b.Grid.CapacityWh : 0f;
                Counter(new Rect(1010, 8, 120, 40), ItemIconPainter.For(ResourceType.Iron), minerals.ToString(), Tr.T("Minerales"));
                Counter(new Rect(1135, 8, 120, 40), ItemIconPainter.For(ResourceType.MetalParts), metals.ToString(), Tr.T("Metales"));
                Counter(new Rect(1260, 8, 110, 40), ItemIconPainter.For(ResourceType.Water), water.ToString(), Tr.T("Agua"));
                Counter(new Rect(1375, 8, 110, 40), ItemIconPainter.For(ResourceType.SolarCells), Mathf.RoundToInt(energy * 100f) + "%", Tr.T("Energía"));
            }
            if (GUI.Button(new Rect(1500, 10, 88, 36), Tr.T("X  Cerrar"), HudTheme.Button)) SetOpen(false);
        }

        private static void Counter(Rect r, Texture2D icon, string value, string label)
        {
            if (icon != null) GUI.DrawTexture(new Rect(r.x, r.y + 2, 36, 36), icon);
            GUI.Label(new Rect(r.x + 40, r.y, r.width - 40, 22), "<b>" + value + "</b>", HudTheme.Small);
            GUI.Label(new Rect(r.x + 40, r.y + 18, r.width - 40, 20), label, HudTheme.Small);
        }

        private void DrawTabs()
        {
            float x = 20f;
            for (int i = 0; i < TabLabels.Length; i++)
            {
                float w = i == 0 ? 150f : (i == 4 ? 140f : 250f);
                if (GUI.Button(new Rect(x, 66, w, 40), TabLabels[i], (int)_tab == i ? HudTheme.TabActive : HudTheme.Tab)) _tab = (Tab)i;
                x += w + 8f;
            }
        }

        // ------------------------------------------------------------------
        // Pestaña "Gestión": tres columnas (nave · estado · robot)
        // ------------------------------------------------------------------

        private void DrawOverview()
        {
            DrawShipList(new Rect(20, 116, 500, 680));
            DrawStatusColumn(new Rect(540, 116, 520, 680));
            DrawRobotList(new Rect(1080, 116, 500, 680));

            // Barra inferior: enlace con la nave.
            Rect bar = new Rect(20, 806, 1560, 34);
            HudTheme.Frame(bar, HudTheme.PanelBg, HudTheme.AccentDim, 1f);
            string link = Linked ? Tr.T("<color=#60ff80>Estado de enlace: estable</color> — sincronizado con la nave nodriza (") + DistanceToShip.ToString("0") + " m)"
                                 : Tr.T("<color=#ff8080>Sin enlace con la nave</color> — acércate a menos de ") + LinkRange.ToString("0") + Tr.T(" m para transferir (") + DistanceToShip.ToString("0") + " m)";
            GUI.Label(new Rect(bar.x + 14, bar.y, bar.width - 28, bar.height), link, HudTheme.Small);
        }

        private void DrawShipList(Rect r)
        {
            HudTheme.Panel(r, Tr.T("Inventario de la Nave"), Tr.T("Recursos almacenados en la nave nodriza"));
            RocketBase b = Ship;
            if (b == null)
            {
                GUI.Label(new Rect(r.x + 20, r.y + 90, r.width - 40, 40), Tr.T("Sin nave."), HudTheme.Body);
                return;
            }
            BaseInventory inv = b.Inventory;
            HudTheme.Bar(new Rect(r.x + 20, r.y + 76, r.width - 40, 24), inv.TotalMassKg / inv.MaxMassKg, HudTheme.Accent,
                Tr.T("Capacidad de almacenaje: ") + inv.TotalMassKg.ToString("0") + " / " + inv.MaxMassKg.ToString("0") + " kg");

            Rect view = new Rect(r.x + 14, r.y + 110, r.width - 28, r.height - 230);
            List<ResourceType> types = ShipTypes(inv);
            _scrollShip = GUI.BeginScrollView(view, _scrollShip, new Rect(0, 0, view.width - 20, types.Count * 56));
            for (int i = 0; i < types.Count; i++)
            {
                ResourceType t = types[i];
                Rect row = new Rect(0, i * 56, view.width - 20, 52);
                bool sel = _hasShipSel && _shipSel == t;
                HudTheme.Fill(row, sel ? new Color(0.1f, 0.4f, 0.5f, 0.9f) : HudTheme.Slot);
                GUI.DrawTexture(new Rect(row.x + 6, row.y + 4, 44, 44), ItemIconPainter.For(t));
                GUI.Label(new Rect(row.x + 58, row.y + 4, 260, 22), "<b>" + BaseInventory.Label(t) + "</b>", HudTheme.Small);
                GUI.Label(new Rect(row.x + 58, row.y + 26, 300, 20), BaseInventory.CategoryLabel(t) + Tr.T(" • Masa: ") + (inv.Get(t) * BaseInventory.MassPerUnit(t)).ToString("0") + " kg", HudTheme.Small);
                GUI.Label(new Rect(row.xMax - 110, row.y + 8, 100, 30), "<size=20><b>" + inv.Get(t) + "</b></size>", HudTheme.Small);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) { _hasShipSel = true; _shipSel = t; }
            }
            GUI.EndScrollView();

            DrawQuantityAndTransferToRobot(new Rect(r.x + 20, r.yMax - 112, r.width - 40, 100));
        }

        private void DrawRobotList(Rect r)
        {
            ItemContainer bay = _inventory.Bay;
            HudTheme.Panel(r, Tr.T("Inventario del Robot"), Tr.T("Carga del rover • ") + bay.TotalMassKg.ToString("0") + " / " + bay.MaxMassKg.ToString("0") + " kg");
            HudTheme.Bar(new Rect(r.x + 20, r.y + 76, r.width - 40, 24), bay.MassRatio, BarColor(bay.MassRatio),
                Mathf.RoundToInt(bay.MassRatio * 100f) + Tr.T("% masa · ") + bay.TotalVolumeLiters.ToString("0") + " / " + bay.MaxVolumeLiters.ToString("0") + " L");

            Rect view = new Rect(r.x + 14, r.y + 110, r.width - 28, r.height - 230);
            int rows = bay.Slots.Count + 1;
            _scrollRobot = GUI.BeginScrollView(view, _scrollRobot, new Rect(0, 0, view.width - 20, rows * 56));
            int y = 0;
            for (int i = 0; i < bay.Slots.Count; i++)
            {
                ItemStack s = bay.Slots[i];
                Rect row = new Rect(0, y, view.width - 20, 52);
                HudTheme.Fill(row, s == _robotSel ? new Color(0.1f, 0.4f, 0.5f, 0.9f) : HudTheme.Slot);
                GUI.DrawTexture(new Rect(row.x + 6, row.y + 4, 44, 44), ItemIconPainter.For(s.item));
                GUI.Label(new Rect(row.x + 58, row.y + 4, 280, 22), "<b>" + s.item.itemName + "</b>", HudTheme.Small);
                GUI.Label(new Rect(row.x + 58, row.y + 26, 300, 20), Tr.T("Peso: ") + s.MassKg.ToString("0.#") + " kg · " + s.VolumeLiters.ToString("0.#") + " L", HudTheme.Small);
                GUI.Label(new Rect(row.xMax - 90, row.y + 8, 80, 30), "<size=20><b>" + s.count + "</b></size>", HudTheme.Small);
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) _robotSel = s;
                y += 56;
            }
            // Herramienta equipada (resaltada, como en el diseño).
            if (_tools != null)
            {
                Rect row = new Rect(0, y, view.width - 20, 52);
                HudTheme.Fill(row, new Color(0.6f, 0.55f, 0.1f, 0.9f));
                GUI.DrawTexture(new Rect(row.x + 6, row.y + 4, 44, 44), ItemIconPainter.Get(IconShape.Toolbox, new Color(0.9f, 0.8f, 0.3f)));
                GUI.Label(new Rect(row.x + 58, row.y + 8, 380, 36), "<b>" + RoverToolController.ToolLabel(_tools.ActiveTool) + Tr.T(" (equipada)</b>\nBrazo delantero"), HudTheme.Small);
            }
            GUI.EndScrollView();

            Rect buttons = new Rect(r.x + 20, r.yMax - 112, r.width - 40, 100);
            GUI.enabled = Linked && _robotSel != null && ContainsStack(_robotSel);
            if (GUI.Button(new Rect(buttons.x, buttons.y, buttons.width, 44), Tr.T("←  Transferir a Nave"), HudTheme.BigButton)) TransferToShip(_robotSel, QtyFor(_robotSel.count));
            GUI.enabled = _robotSel != null && ContainsStack(_robotSel);
            if (GUI.Button(new Rect(buttons.x, buttons.y + 52, buttons.width, 40), Tr.T("Desechar (soltar al suelo)"), HudTheme.Button)) Drop(_robotSel);
            GUI.enabled = true;
        }

        private void DrawStatusColumn(Rect r)
        {
            HudTheme.Panel(r, Tr.T("Estado del Robot"), RobotName + Tr.T(" • Marte • Estado del sistema"));
            if (_preview != null && _preview.Texture != null) DrawPreview(new Rect(r.x + 130, r.y + 70, 260, 200));
            DrawModuleList(new Rect(r.x + 14, r.y + 280, r.width - 28, r.height - 380), true);
            DrawStatusButtons(new Rect(r.x + 20, r.yMax - 92, r.width - 40, 40));
            float charge = _battery != null ? _battery.Charge01 : 0f;
            HudTheme.Bar(new Rect(r.x + 20, r.yMax - 44, r.width - 40, 24), charge, BarColor(1f - charge), Tr.T("Batería: ") + Mathf.RoundToInt(charge * 100f) + "%");
        }

        // ------------------------------------------------------------------
        // Pestaña "Inventario de la Nave" (cuadrícula con iconos)
        // ------------------------------------------------------------------

        private void DrawShipTab()
        {
            Rect r = new Rect(20, 116, 1050, 760);
            RocketBase b = Ship;
            BaseInventory inv = b != null ? b.Inventory : null;
            HudTheme.Panel(r, Tr.T("INVENTARIO DE LA NAVE"), inv != null ? Tr.T("Recursos y materiales — peso total: ") + inv.TotalMassKg.ToString("0") + " / " + inv.MaxMassKg.ToString("0") + " kg" : Tr.T("Sin nave"));
            if (inv == null) return;

            List<ResourceType> types = ShipTypes(inv);
            Rect grid = new Rect(r.x + 24, r.y + 86, r.width - 48, 520);
            const int cols = 5;
            float cw = (grid.width - 4 * 12) / cols, ch = 124f;
            for (int i = 0; i < types.Count; i++)
            {
                ResourceType t = types[i];
                Rect cell = new Rect(grid.x + (i % cols) * (cw + 12), grid.y + (i / cols) * (ch + 10), cw, ch);
                bool sel = _hasShipSel && _shipSel == t;
                if (GridSlot(cell, ItemIconPainter.For(t), BaseInventory.Label(t).ToUpperInvariant(), inv.Get(t).ToString(), sel)) { _hasShipSel = true; _shipSel = t; }
            }

            float used = inv.TotalMassKg / inv.MaxMassKg;
            GUI.Label(new Rect(r.x + 24, r.yMax - 140, 700, 24), Tr.T("CAPACIDAD: ") + (used * 100f).ToString("0.0") + Tr.T("% USADA — ") +
                      (Linked ? "<color=#60ff80>TRANSFERENCIA RÁPIDA DISPONIBLE</color>" : "<color=#ff8080>ROBOT FUERA DE ALCANCE</color>"), HudTheme.Small);
            DrawQuantityAndTransferToRobot(new Rect(r.x + 24, r.yMax - 110, 560, 96));
        }

        private void DrawQuantityAndTransferToRobot(Rect r)
        {
            string selText = _hasShipSel ? BaseInventory.Label(_shipSel) : Tr.T("elige un recurso");
            GUI.Label(new Rect(r.x, r.y, r.width, 22), Tr.T("Seleccionado: <b>") + selText + "</b>", HudTheme.Small);
            _qtyMode = GUI.Toolbar(new Rect(r.x, r.y + 24, Mathf.Min(300, r.width), 26), _qtyMode, new[] { "1 u", "10 u", Tr.T("Todo") }, HudTheme.Tab);
            GUI.enabled = Linked && _hasShipSel;
            if (GUI.Button(new Rect(r.x, r.y + 56, r.width, 44), Tr.T("[ TRANSFERIR A ROBOT >> ]"), HudTheme.BigButton)) TransferToRobot(_shipSel);
            GUI.enabled = true;
        }

        // ------------------------------------------------------------------
        // Pestaña "Inventario del Robot" (cuadrícula + inspección)
        // ------------------------------------------------------------------

        private void DrawRobotTab()
        {
            ItemContainer bay = _inventory.Bay;
            Rect r = new Rect(20, 116, 1050, 760);
            HudTheme.Panel(r, Tr.T("INVENTARIO DEL ROBOT"), Tr.T("Carga del rover — ") + bay.TotalMassKg.ToString("0.#") + " / " + bay.MaxMassKg.ToString("0") + " kg · " +
                                                     bay.TotalVolumeLiters.ToString("0") + " / " + bay.MaxVolumeLiters.ToString("0") + " L");
            HudTheme.Bar(new Rect(r.x + 24, r.y + 78, 490, 22), bay.MassRatio, BarColor(bay.MassRatio), Tr.T("Masa ") + Mathf.RoundToInt(bay.MassRatio * 100f) + Tr.T("%  (peso en Marte ") + _inventory.WeightNewtons.ToString("0") + " N)");
            HudTheme.Bar(new Rect(r.x + 534, r.y + 78, 490, 22), bay.VolumeRatio, BarColor(bay.VolumeRatio), Tr.T("Volumen ") + Mathf.RoundToInt(bay.VolumeRatio * 100f) + "%");

            Rect grid = new Rect(r.x + 24, r.y + 112, 640, 560);
            const int cols = 5;
            float cw = 118f, ch = 110f;
            int total = Mathf.Max(bay.MaxSlots, bay.Slots.Count);
            for (int i = 0; i < total && i < 20; i++)
            {
                Rect cell = new Rect(grid.x + (i % cols) * (cw + 10), grid.y + (i / cols) * (ch + 8), cw, ch);
                ItemStack s = i < bay.Slots.Count ? bay.Slots[i] : null;
                if (s == null)
                {
                    HudTheme.Frame(cell, new Color(0.06f, 0.11f, 0.17f, 0.8f), new Color(0.15f, 0.3f, 0.4f), 1f);
                    continue;
                }
                if (GridSlot(cell, ItemIconPainter.For(s.item), s.item.itemName.ToUpperInvariant(), "×" + s.count, s == _robotSel)) _robotSel = s;
            }

            // Inspección del objeto seleccionado.
            Rect info = new Rect(r.x + 690, r.y + 112, 336, 560);
            HudTheme.Frame(info, HudTheme.PanelLight, HudTheme.AccentDim, 1f);
            GUILayout.BeginArea(new Rect(info.x + 12, info.y + 10, info.width - 24, info.height - 20));
            if (_robotSel == null || !ContainsStack(_robotSel))
            {
                _robotSel = null;
                GUILayout.Label(Tr.T("Selecciona un objeto."), HudTheme.Body);
                GUILayout.Label(_inventory.LoadStatus, HudTheme.Body);
            }
            else
            {
                ItemData d = _robotSel.item;
                GUILayout.Label("<b>" + d.itemName + "</b>  ×" + _robotSel.count, HudTheme.Body);
                GUILayout.Label(d.description, HudTheme.Body);
                GUILayout.Label("<i>" + d.scientificNotes + "</i>", HudTheme.Body);
                GUILayout.Label(Tr.T("Masa del montón: ") + _robotSel.MassKg.ToString("0.#") + Tr.T(" kg · Volumen: ") + _robotSel.VolumeLiters.ToString("0.#") + " L", HudTheme.Body);
                GUILayout.Label(Tr.T("Conservación: ") + Mathf.RoundToInt(_robotSel.condition * 100f) + " %", HudTheme.Body);
                if (d.requiresCryoStorage) GUILayout.Label("<color=#80d0ff>Necesita almacenamiento criogénico.</color>", HudTheme.Body);
                if (d.isCorrosiveOrToxic) GUILayout.Label("<color=#ffb060>Corrosivo / tóxico: guardar sellado.</color>", HudTheme.Body);
                GUILayout.Space(8);
                _qtyMode = GUILayout.Toolbar(_qtyMode, new[] { "1 u", "10 u", Tr.T("Todo") }, HudTheme.Tab);
                GUI.enabled = Linked;
                if (GUILayout.Button(Tr.T("<< TRANSFERIR A NAVE"), HudTheme.BigButton, GUILayout.Height(44))) TransferToShip(_robotSel, QtyFor(_robotSel.count));
                GUI.enabled = true;
                if (GUILayout.Button(Tr.T("Dividir pila"), HudTheme.Button) && !_inventory.Bay.Split(_robotSel)) Say(Tr.T("No hay ranura libre para dividir."));
                if (GUILayout.Button(Tr.T("Soltar al suelo"), HudTheme.Button)) Drop(_robotSel);
            }
            GUILayout.EndArea();
        }

        // ------------------------------------------------------------------
        // Pestaña "Estado del Robot"
        // ------------------------------------------------------------------

        private void DrawStatusTab()
        {
            Rect r = new Rect(20, 116, 1050, 760);
            HudTheme.Panel(r, Tr.T("ESTADO DEL ROBOT"), Tr.T("Integridad por componente — sin barra de salud genérica"));
            DrawModuleList(new Rect(r.x + 20, r.y + 80, 600, 560), false);

            Rect info = new Rect(r.x + 640, r.y + 80, 390, 560);
            HudTheme.Frame(info, HudTheme.PanelLight, HudTheme.AccentDim, 1f);
            GUILayout.BeginArea(new Rect(info.x + 12, info.y + 10, info.width - 24, info.height - 20));
            if (_moduleSel != null)
            {
                GUILayout.Label("<b>" + _moduleSel.Definition.displayName + "</b>", HudTheme.Body);
                GUILayout.Label(Tr.T("Integridad: ") + _moduleSel.Integrity.ToString("0") + " / " + _moduleSel.MaxIntegrity.ToString("0") + " (" + Mathf.RoundToInt(_moduleSel.Integrity01 * 100f) + " %)", HudTheme.Body);
                GUILayout.Label(Tr.T("Rendimiento: ") + Mathf.RoundToInt(_moduleSel.Efficiency * 100f) + " %", HudTheme.Body);
                GUILayout.Label(Tr.T("Se repara con: ") + ToolList(_moduleSel), HudTheme.Body);
                GUILayout.Space(10);
            }
            if (_health != null)
            {
                GUILayout.Label(Tr.T("Movilidad: ") + Mathf.RoundToInt(_health.Mobility * 100f) + " %", HudTheme.Body);
                GUILayout.Label(Tr.T("Integridad estructural: ") + Mathf.RoundToInt(_health.StructuralIntegrity01 * 100f) + " %", HudTheme.Body);
            }
            if (_modules != null)
            {
                GUILayout.Space(8);
                GUILayout.Label("<b>Módulos instalados</b>", HudTheme.Body);
                foreach (RoverSlotType slot in new[] { RoverSlotType.Wheels, RoverSlotType.FrontArm, RoverSlotType.TopDeck, RoverSlotType.InternalBay })
                {
                    var names = new List<string>();
                    foreach (RoverModuleType m in _modules.GetSlots(slot))
                    {
                        RoverModuleDefinition d = RobotModuleSystem.Find(m);
                        if (d != null) names.Add(d.name);
                    }
                    GUILayout.Label(RobotModuleSystem.SlotLabel(slot) + ": " + (names.Count > 0 ? string.Join(", ", names) : Tr.T("(vacío)")), HudTheme.Body);
                }
                GUILayout.Label(Tr.T("Masa total: ") + _modules.TotalMassKg.ToString("0") + " kg", HudTheme.Body);
            }
            GUILayout.EndArea();
            DrawStatusButtons(new Rect(r.x + 20, r.yMax - 100, 600, 44));
        }

        private void DrawModuleList(Rect r, bool compact)
        {
            if (_health == null) return;
            IReadOnlyList<RobotModule> mods = _health.Modules;
            int ok = 0;
            for (int i = 0; i < mods.Count; i++) if (mods[i].State == ModuleState.Operational) ok++;
            GUI.Label(new Rect(r.x, r.y, r.width, 24), "<color=#ffd060>Estado general:</color> <b>" + (ok == mods.Count ? Tr.T("Óptimo") : Tr.T("Parcial")) + "</b> • " + ok + "/" + mods.Count + Tr.T(" sistemas óptimos"), HudTheme.Small);

            float rowH = compact ? 34f : 42f;
            int extra = 2; // batería y herramienta
            Rect view = new Rect(r.x, r.y + 28, r.width, r.height - 28);
            _scrollStatus = GUI.BeginScrollView(view, _scrollStatus, new Rect(0, 0, view.width - 20, (mods.Count + extra) * (rowH + 4)));
            float y = 0f;
            for (int i = 0; i < mods.Count; i++)
            {
                RobotModule m = mods[i];
                Rect row = new Rect(0, y, view.width - 20, rowH);
                HudTheme.Fill(row, m == _moduleSel ? new Color(0.1f, 0.4f, 0.5f, 0.9f) : HudTheme.Slot);
                GUI.Label(new Rect(row.x + 12, row.y, row.width * 0.55f, rowH), StateGlyph(m.State) + "  " + m.Definition.displayName, HudTheme.Small);
                HudTheme.StatePill(new Rect(row.xMax - 200, row.y + 6, 190, rowH - 12), StateText(m.State), StateColor(m.State));
                if (GUI.Button(row, GUIContent.none, GUIStyle.none)) _moduleSel = m;
                y += rowH + 4;
            }
            float charge = _battery != null ? _battery.Charge01 : 0f;
            Rect bat = new Rect(0, y, view.width - 20, rowH);
            HudTheme.Fill(bat, HudTheme.Slot);
            GUI.Label(new Rect(bat.x + 12, bat.y, 300, rowH), Tr.T("●  Batería"), HudTheme.Small);
            HudTheme.StatePill(new Rect(bat.xMax - 200, bat.y + 6, 190, rowH - 12), Mathf.RoundToInt(charge * 100f) + "%", charge > 0.3f ? HudTheme.Good : (charge > 0.15f ? HudTheme.Warn : HudTheme.Bad));
            y += rowH + 4;
            if (_tools != null)
            {
                Rect tool = new Rect(0, y, view.width - 20, rowH);
                HudTheme.Fill(tool, HudTheme.Slot);
                GUI.Label(new Rect(tool.x + 12, tool.y, 340, rowH), "●  " + RoverToolController.ToolLabel(_tools.ActiveTool) + Tr.T(" (equipada)"), HudTheme.Small);
                HudTheme.StatePill(new Rect(tool.xMax - 200, tool.y + 6, 190, rowH - 12), Tr.T("Operativo"), HudTheme.Good);
            }
            GUI.EndScrollView();
        }

        private void DrawStatusButtons(Rect r)
        {
            float w = (r.width - 20f) / 3f;
            if (GUI.Button(new Rect(r.x, r.y, w, r.height), Tr.T("Reparar"), HudTheme.Button)) Repair();
            if (GUI.Button(new Rect(r.x + w + 10, r.y, w, r.height), Tr.T("Diagnóstico"), HudTheme.Button)) Diagnose();
            if (GUI.Button(new Rect(r.x + 2 * (w + 10), r.y, w, r.height), Tr.T("Calibrar"), HudTheme.Button)) Calibrate();
        }

        private void Repair()
        {
            if (_health == null) return;
            if (_modules != null && _modules.IsDockedAtAssemblyBay())
            {
                _health.RepairAll();
                Say(Tr.T("Reparación completa en el taller de ensamblaje (gratis en la base)."));
            }
            else
            {
                Say(Tr.T("Fuera del taller: repara a mano con R y la herramienta adecuada (T cambia de herramienta, G de módulo)."));
            }
        }

        private void Diagnose()
        {
            if (_health == null) return;
            var bad = new List<string>();
            foreach (RobotModule m in _health.Modules)
            {
                if (m.State != ModuleState.Operational) bad.Add(m.Definition.displayName + " " + Mathf.RoundToInt(m.Integrity01 * 100f) + "%");
            }
            string bias = Mathf.Abs(_health.SteeringBias) > 0.05f ? Tr.T(" · desvío de dirección hacia la ") + (_health.SteeringBias < 0f ? "izquierda" : "derecha") : "";
            Say(bad.Count == 0 ? Tr.T("Diagnóstico: todos los sistemas óptimos.") : Tr.T("Diagnóstico: ") + string.Join(", ", bad) + Tr.T(" · movilidad ") + Mathf.RoundToInt(_health.Mobility * 100f) + "%" + bias);
        }

        private void Calibrate()
        {
            if (_health == null || _battery == null) return;
            if (!_battery.TryConsume(20f))
            {
                Say(Tr.T("Batería insuficiente para calibrar (20 Wh)."));
                return;
            }
            int fixedCount = 0;
            foreach (RobotModuleId id in new[] { RobotModuleId.CableSensors, RobotModuleId.CableHud, RobotModuleId.CableMinimap })
            {
                RobotModule m = _health.GetModule(id);
                if (m == null || m.IsBroken || !m.NeedsRepair) continue;
                _health.Repair(id, RepairToolType.CableKit, 1.7f); // ~+10 de integridad por recalibrado
                fixedCount++;
            }
            Say(fixedCount > 0 ? Tr.T("Sensores recalibrados (+10 en cableado no cortado).") : Tr.T("Calibración completa: no había nada que ajustar (los cables cortados se empalman a mano)."));
        }

        // ------------------------------------------------------------------
        // Pestaña "Ajustes"
        // ------------------------------------------------------------------

        private void DrawSettingsTab()
        {
            Rect r = new Rect(20, 116, 1050, 760);
            HudTheme.Panel(r, Tr.T("AJUSTES"), _settingsPage == 0 ? Tr.T("Se guardan en este equipo") : Tr.T("Todas las teclas del juego"));
            _settingsPage = GUI.Toolbar(new Rect(r.xMax - 330, r.y + 18, 310, 34), _settingsPage, new[] { Tr.T("Opciones"), Tr.T("Controles") }, HudTheme.Tab);
            if (_settingsPage == 1)
            {
                DrawControls(new Rect(r.x + 20, r.y + 80, r.width - 40, r.height - 100));
                return;
            }
            float x = r.x + 30, y = r.y + 90, w = 600;

            GUI.Label(new Rect(x, y, 300, 26), Tr.T("Calidad gráfica"), HudTheme.Body);
            int q = GUI.Toolbar(new Rect(x + 260, y, w, 32), GameSettings.Quality, GameSettings.QualityLabels, HudTheme.Tab);
            if (q != GameSettings.Quality) GameSettings.SetQuality(q);
            y += 60;

            float master = Slider(ref y, x, w, Tr.T("Sonido general"), GameSettings.MasterVolume, 0f, 1f, "{0:P0}");
            float music = Slider(ref y, x, w, Tr.T("Música (cuando la haya)"), GameSettings.MusicVolume, 0f, 1f, "{0:P0}");
            float fx = Slider(ref y, x, w, Tr.T("Efectos"), GameSettings.EffectsVolume, 0f, 1f, "{0:P0}");
            if (!Mathf.Approximately(master, GameSettings.MasterVolume) || !Mathf.Approximately(music, GameSettings.MusicVolume) || !Mathf.Approximately(fx, GameSettings.EffectsVolume))
            {
                GameSettings.SetVolumes(master, music, fx);
            }
            float bright = Slider(ref y, x, w, Tr.T("Brillo"), GameSettings.Brightness, 0.5f, 1.5f, "{0:0.00}");
            if (!Mathf.Approximately(bright, GameSettings.Brightness)) GameSettings.SetBrightness(bright);
            float sens = Slider(ref y, x, w, Tr.T("Sensibilidad del ratón"), GameSettings.MouseSensitivity, 0.2f, 8f, "{0:0.0}");
            if (!Mathf.Approximately(sens, GameSettings.MouseSensitivity)) GameSettings.SetMouseSensitivity(sens);

            GUI.Label(new Rect(x, y, 300, 26), Tr.T("Idioma"), HudTheme.Body);
            int lang = GUI.Toolbar(new Rect(x + 260, y, 420, 32), GameSettings.Language, GameSettings.LanguageLabels, HudTheme.Tab);
            if (lang != GameSettings.Language) GameSettings.SetLanguage(lang);
            y += 60;

            if (GUI.Button(new Rect(x, y, 420, 34), MarsRobot.Localization.Localizer.L("Repetir el tutorial de Marti", "Replay Marti's tutorial"), HudTheme.Tab))
            {
                MarsRobot.UI.TutorialGuide.Reset();
                MarsRobot.UI.TutorialGuide.StartIfNeeded();
                Say(MarsRobot.Localization.Localizer.L("¡Marti te vuelve a enseñar! Cierra este menú.", "Marti will teach you again! Close this menu."));
            }
            y += 50;

            bool fps = GUI.Toggle(new Rect(x, y, 400, 30), GameSettings.ShowFps, Tr.T("  Mostrar FPS"));
            if (fps != GameSettings.ShowFps) GameSettings.SetShowFps(fps);
        }

        private static float Slider(ref float y, float x, float w, string label, float value, float min, float max, string format)
        {
            GUI.Label(new Rect(x, y, 260, 26), label, HudTheme.Body);
            float v = GUI.HorizontalSlider(new Rect(x + 260, y + 8, w - 90, 24), value, min, max);
            GUI.Label(new Rect(x + 260 + w - 80, y, 90, 26), string.Format(format, v), HudTheme.Body);
            y += 52;
            return v;
        }

        // ------------------------------------------------------------------
        // Tarjeta del robot (derecha): vista 3D + ficha
        // ------------------------------------------------------------------

        private void DrawRobotCard(Rect r)
        {
            HudTheme.Frame(r, HudTheme.PanelBg, HudTheme.AccentDim);
            GUI.Label(new Rect(r.x + 24, r.y + 14, r.width - 48, 40), "<size=32><b>" + RobotName.ToUpperInvariant().Replace(Tr.T("ROBOT EXPLORADOR "), Tr.T("ROBOT EXPLORADOR\n")) + "</b></size>", HudTheme.Title);
            GUI.Label(new Rect(r.x + 24, r.y + 100, r.width - 48, 24), Tr.T("ROVER DE EXPLORACIÓN — MODELO XR-1"), HudTheme.Subtitle);
            DrawPreview(new Rect(r.x + 20, r.y + 130, r.width - 40, 380));

            Rect card = new Rect(r.x + 20, r.y + 524, r.width - 40, 220);
            HudTheme.Frame(card, new Color(0.02f, 0.06f, 0.1f, 0.95f), HudTheme.AccentDim, 1f);
            float y = card.y + 12;
            GUI.Label(new Rect(card.x + 16, y, card.width - 32, 26), Tr.T("<color=#ffd060>ESTADO:</color>  ") + RobotState(), HudTheme.Small);
            y += 34;
            float charge = _battery != null ? _battery.Charge01 : 0f;
            GUI.Label(new Rect(card.x + 16, y, 200, 24), Tr.T("ENERGÍA: ") + Mathf.RoundToInt(charge * 100f) + Tr.T("% | BATERÍA"), HudTheme.Small);
            HudTheme.Bar(new Rect(card.x + 16, y + 24, card.width - 32, 16), charge, HudTheme.Accent, "");
            y += 50;
            MarsEnvironmentSystem env;
            float temp = GameServices.TryGet(out env) ? env.TemperatureC : -60f;
            GUI.Label(new Rect(card.x + 16, y, card.width - 32, 24), Tr.T("TEMPERATURA EXTERNA: ") + temp.ToString("0") + " °C", HudTheme.Small);
            y += 30;
            GUI.Label(new Rect(card.x + 16, y, card.width - 32, 24), Tr.T("UBICACIÓN: ") + Location(), HudTheme.Small);
            y += 30;
            GUI.Label(new Rect(card.x + 16, y, card.width - 32, 24), Tr.T("VINCULADO A: ") + (Linked ? "<color=#60ff80>BASE DE LA NAVE</color>" : Tr.T("<color=#ff8080>SIN ENLACE (") + DistanceToShip.ToString("0") + " m)</color>"), HudTheme.Small);
        }

        private void DrawPreview(Rect r)
        {
            if (_preview == null || _preview.Texture == null) return;
            GUI.DrawTexture(r, _preview.Texture, ScaleMode.ScaleToFit, true);
            Event e = Event.current;
            if (e.type == EventType.MouseDrag && r.Contains(e.mousePosition))
            {
                _preview.Rotate(e.delta.x * 0.6f);
                e.Use();
            }
        }

        private string RobotState()
        {
            if (_health != null && _health.IsImmobilized) return "<color=#ff6060>● INMOVILIZADO</color>";
            if (_tools != null && _tools.IsWorking) return "<color=#ffd060>● TRABAJANDO</color>";
            if (_controller != null && _controller.CurrentSpeed > 0.2f) return "<color=#60c0ff>● EN MOVIMIENTO</color>";
            return "<color=#60ff80>● EN ESPERA</color>";
        }

        private string Location()
        {
            string zone = Tr.T("Marte");
            RocketBase b = Ship;
            if (b != null && b.Zone != null) zone = b.Zone.displayName;
            TerrainManager terrain;
            if (GameServices.TryGet(out terrain) && terrain.IsMapReady)
            {
                Vector2Int c = terrain.WorldToChunk(transform.position);
                return zone.ToUpperInvariant() + " — SECTOR " + c.x.ToString("00") + "-" + c.y.ToString("00");
            }
            return zone.ToUpperInvariant();
        }

        // ------------------------------------------------------------------
        // Transferencias
        // ------------------------------------------------------------------

        private int QtyFor(int available)
        {
            return _qtyMode == 0 ? Mathf.Min(1, available) : (_qtyMode == 1 ? Mathf.Min(10, available) : available);
        }

        private void TransferToRobot(ResourceType type)
        {
            RocketBase b = Ship;
            if (b == null || !Linked) return;
            int available = b.Inventory.Get(type);
            if (available <= 0)
            {
                Say(Tr.T("No queda ") + BaseInventory.Label(type) + Tr.T(" en la nave."));
                return;
            }
            ItemData item = ItemDatabase.ForResource(type);
            int want = QtyFor(available);
            int fit = _inventory.Bay.HowManyFit(item, want);
            if (fit <= 0)
            {
                Say(Tr.T("No cabe en el robot: masa o volumen al límite."));
                return;
            }
            if (!b.Inventory.TrySpend(new[] { new ResourceAmount(type, fit) })) return;
            int added = _inventory.TryAdd(item, fit);
            if (added < fit) b.Inventory.Add(type, fit - added); // devolver lo que no entró
            Say(Tr.T("Transferidos ") + added + Tr.T(" de ") + BaseInventory.Label(type) + Tr.T(" al robot."));
        }

        private void TransferToShip(ItemStack stack, int count)
        {
            if (stack == null || !Linked) return;
            RocketBaseManager mgr;
            RocketBase b = Ship;
            if (b == null || !GameServices.TryGet(out mgr) || mgr.Hold == null) return;
            float addKg = stack.item.convertsToBaseResource ? count * stack.item.baseResourcePerUnit * BaseInventory.MassPerUnit(stack.item.baseResource) : 0f;
            if (b.Inventory.TotalMassKg + addKg > b.Inventory.MaxMassKg)
            {
                Say(Tr.T("La nave está llena (") + b.Inventory.MaxMassKg.ToString("0") + " kg).");
                return;
            }
            string name = stack.item.itemName;
            int moved = mgr.Hold.Receive(_inventory.Bay, stack, count);
            Say(moved > 0 ? Tr.T("Transferidos ") + moved + Tr.T(" de ") + name + Tr.T(" a la nave.") : Tr.T("La nave no admite más de ese recurso (tanques llenos)."));
        }

        private void Drop(ItemStack stack)
        {
            if (stack == null || !ContainsStack(stack)) return;
            DroppedItem.Spawn(stack.item, stack.count, stack.condition, transform.position + transform.forward * 3f + Vector3.up * 1.5f);
            Say(stack.item.itemName + Tr.T(" soltado delante del rover (se puede recoger con F)."));
            _inventory.Bay.RemoveFromSlot(stack, stack.count);
            _robotSel = null;
        }

        // ------------------------------------------------------------------
        // Utilidades de dibujo
        // ------------------------------------------------------------------

        /// <summary>Casilla de cuadrícula: icono, nombre y cantidad. Devuelve true al hacer clic.</summary>
        private static bool GridSlot(Rect cell, Texture2D icon, string name, string count, bool selected)
        {
            HudTheme.Frame(cell, selected ? new Color(0.1f, 0.38f, 0.48f, 0.95f) : HudTheme.Slot, selected ? HudTheme.Accent : new Color(0.2f, 0.35f, 0.45f), selected ? 2f : 1f);
            if (icon != null) GUI.DrawTexture(new Rect(cell.x + cell.width * 0.5f - 34, cell.y + 8, 68, 68), icon);
            GUI.Label(new Rect(cell.x + 4, cell.y + cell.height - 46, cell.width - 8, 28), name, HudTheme.SlotName);
            GUI.Label(new Rect(cell.x + 4, cell.y + cell.height - 24, cell.width - 10, 22), count, HudTheme.SlotCount);
            return GUI.Button(cell, GUIContent.none, GUIStyle.none);
        }

        private static List<ResourceType> ShipTypes(BaseInventory inv)
        {
            var list = new List<ResourceType>();
            foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType)))
            {
                if (inv.Get(t) > 0 || inv.GetCapacity(t) >= 0) list.Add(t);
            }
            return list;
        }

        private bool ContainsStack(ItemStack s)
        {
            var slots = _inventory.Bay.Slots;
            for (int i = 0; i < slots.Count; i++) if (slots[i] == s) return true;
            return false;
        }

        private static Color BarColor(float ratio)
        {
            return ratio > 0.9f ? HudTheme.Bad : (ratio > 0.75f ? HudTheme.Warn : HudTheme.Accent);
        }

        private static string StateText(ModuleState s)
        {
            switch (s)
            {
                case ModuleState.Operational: return Tr.T("Operativo");
                case ModuleState.Damaged: return Tr.T("Desgaste");
                case ModuleState.Critical: return Tr.T("Necesita reparación");
                default: return Tr.T("Averiado");
            }
        }

        private static Color StateColor(ModuleState s)
        {
            switch (s)
            {
                case ModuleState.Operational: return HudTheme.Good;
                case ModuleState.Damaged: return HudTheme.Warn;
                case ModuleState.Critical: return HudTheme.Bad;
                default: return new Color(0.5f, 0.08f, 0.08f);
            }
        }

        private static string StateGlyph(ModuleState s)
        {
            switch (s)
            {
                case ModuleState.Operational: return "<color=#60ff80>●</color>";
                case ModuleState.Damaged: return "<color=#ffd060>●</color>";
                default: return "<color=#ff6060>●</color>";
            }
        }

        private static string ToolList(RobotModule m)
        {
            var names = new List<string>();
            foreach (RepairToolType t in m.Definition.validTools)
            {
                switch (t)
                {
                    case RepairToolType.Wrench: names.Add("llave"); break;
                    case RepairToolType.CableKit: names.Add(Tr.T("kit de empalme")); break;
                    case RepairToolType.Welder: names.Add("soldador"); break;
                    case RepairToolType.ServoKit: names.Add(Tr.T("kit de servos")); break;
                }
            }
            return string.Join(", ", names);
        }
    }
}
