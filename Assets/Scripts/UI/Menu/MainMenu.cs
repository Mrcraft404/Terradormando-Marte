using System.Collections;
using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Spawns;
using MarsRobot.World.GlobalMap;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MarsRobot.UI.Menu
{
    /// <summary>
    /// Menú principal del juego (IMGUI, sin paquetes extra), con el estilo de la portada:
    /// fondo de Marte con la base, título naranja y botones de acero JUGAR / OPCIONES / SALIR.
    ///
    /// Flujo: JUGAR ▸ elegir modo (Creativo / Supervivencia) ▸ crear mundo (nombre, semilla,
    /// dificultad) ▸ elegir punto de aparición en el mapa completo de Marte ▸ crear mundo.
    /// El punto se convierte en una zona de aterrizaje (SpawnZoneFactory) y se carga la escena de juego.
    ///
    /// La interfaz se diseña sobre un lienzo virtual de 1920 × 1080 y se escala a la pantalla.
    /// </summary>
    public sealed class MainMenu : MonoBehaviour
    {
        [Tooltip("Catálogo con las zonas predefinidas (las que tienen datos reales de la NASA).")]
        [SerializeField] private LandingZoneCatalog catalog;

        [Tooltip("Escena de juego que se carga al crear el mundo (debe estar en Build Settings).")]
        [SerializeField] private string gameSceneName = "Prueba";

        [SerializeField] private string titleTop = "Terraformando";
        [SerializeField] private string titleBottom = "Marte";

        private enum MenuScreen { Main, Mode, Create, Map, Options, Exit, Loading }

        private const float VW = 1920f, VH = 1080f;
        private const string CommanderKey = "tm_commander";

        private static readonly Color Orange = new Color(1f, 0.58f, 0.2f);
        private static readonly Color OrangeLight = new Color(1f, 0.82f, 0.45f);
        private static readonly Color OrangeDark = new Color(0.85f, 0.3f, 0.06f);
        private static readonly Color TextDark = new Color(0.13f, 0.1f, 0.09f);
        private static readonly Color TextLight = new Color(0.96f, 0.93f, 0.89f);
        private static readonly Color Muted = new Color(0.74f, 0.68f, 0.63f);
        private static readonly Color Cyan = new Color(0.35f, 0.92f, 1f);
        private static readonly Color CreativeColor = new Color(0.3f, 0.8f, 1f);
        private static readonly Color SurvivalColor = new Color(1f, 0.5f, 0.16f);

        private static readonly string[] WorldNames = { "Colonia Ares", "Base Horizonte", "Colonia Aurora", "Puesto Fénix", "Base Olympus", "Colonia Nueva Esperanza" };

        private MenuScreen _screen = MenuScreen.Main;
        private float _fade = 1f;
        private float _scale = 1f;
        private MenuAudio _audio;

        private WorldSetup _draft = new WorldSetup();
        private WorldSetup _lastWorld;
        private string _seedText = "";
        private string _commander = "COMANDANTE";

        // Selección en el mapa
        private bool _hasPick;
        private float _pickLat, _pickLon;
        private MarsSiteInfo _pickInfo;
        private LandingZoneData _pickPreset;

        // Carga
        private string _loadingRegion = "";
        private float _loadProgress;
        private string _error = "";
        private float _errorTime = -10f;

        // Animación de los botones
        private readonly Dictionary<string, float> _hoverAnim = new Dictionary<string, float>();
        private string _hoverId = "";
        private string _hoverIdThisFrame = "";

        // Polvo flotando delante del fondo
        private Vector3[] _dust;

        // Estilos
        private Font _titleFont, _bodyFont;
        private GUIStyle _title, _text, _textCenter, _field, _fieldFocus, _sliderBg, _sliderThumb;
        private GUIStyle _window;

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            Time.timeScale = 1f;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (Camera.main == null)
            {
                var camGo = new GameObject("Camara del menu");
                var cam = camGo.AddComponent<Camera>();
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = Color.black;
                cam.cullingMask = 0;
                camGo.tag = "MainCamera";
                camGo.AddComponent<AudioListener>();
            }

            _audio = gameObject.AddComponent<MenuAudio>();
            _commander = PlayerPrefs.GetString(CommanderKey, "COMANDANTE");
            _lastWorld = GameModeState.LoadLast();

            // El mapa de Marte tarda ~1-2 s: se genera en segundo plano desde ya.
            MarsGlobalMap.GenerateAsync();

            var rnd = new System.Random();
            _dust = new Vector3[70];
            for (int i = 0; i < _dust.Length; i++)
                _dust[i] = new Vector3((float)rnd.NextDouble() * VW, (float)rnd.NextDouble() * VH, 0.3f + (float)rnd.NextDouble());

            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-menuautoshot") >= 0) StartCoroutine(AutoShots());
        }

        /// <summary>Prueba: recorre todas las pantallas y guarda capturas (argumento -menuautoshot).</summary>
        private IEnumerator AutoShots()
        {
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "MenuShots");
            System.IO.Directory.CreateDirectory(dir);
            System.Action<string> shot = n =>
            {
                Debug.Log("[MainMenu] Captura " + n + " (pantalla " + _screen + ", t=" + Time.realtimeSinceStartup.ToString("0.0") + ")");
                StartCoroutine(DebugScreenshot.Capture(System.IO.Path.Combine(dir, n + ".png")));
            };
            Debug.Log("[MainMenu] Recorrido de capturas iniciado, t=" + Time.realtimeSinceStartup.ToString("0.0"));
            var wait = new WaitForSecondsRealtime(1.2f);

            yield return new WaitForSecondsRealtime(2.5f);
            shot("1_menu");
            yield return wait;
            Show(MenuScreen.Mode);
            yield return wait;
            shot("2_modo");
            yield return wait;
            PickMode(GameMode.Survival);
            yield return wait;
            shot("3_crear");
            yield return wait;
            OpenMap();
            while (!MarsGlobalMap.IsReady) yield return null;
            yield return wait;
            shot("4_mapa");
            yield return wait;
            _pickLat = -5.4f;
            _pickLon = 137.8f;
            _hasPick = true;
            RefreshPick();
            yield return wait;
            shot("5_mapa_gale");
            yield return wait;
            _pickLat = 19f;
            _pickLon = -133f;
            RefreshPick();
            yield return wait;
            shot("6_mapa_olympus");
            yield return wait;
            ConfirmPick();
            yield return wait;
            shot("7_crear_listo");
            yield return wait;
            Show(MenuScreen.Options);
            yield return wait;
            shot("8_opciones");
            yield return wait;
            Show(MenuScreen.Exit);
            yield return wait;
            shot("9_salir");
            yield return wait;
            CreateWorld();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _fade = Mathf.Min(1f, _fade + dt * 5f);
            for (int i = 0; i < _dust.Length; i++)
            {
                Vector3 d = _dust[i];
                d.x += (18f + 30f * d.z) * dt;
                d.y += Mathf.Sin(Time.unscaledTime * 0.7f + i) * 6f * dt;
                if (d.x > VW + 10f) d.x = -10f;
                _dust[i] = d;
            }
        }

        private void Show(MenuScreen screen)
        {
            _screen = screen;
            _fade = 0f;
            _clearFocus = true; // se aplica en el próximo OnGUI (GUI solo funciona dentro de OnGUI)
        }

        private bool _clearFocus;

        // ------------------------------------------------------------------
        // Dibujo
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            EnsureStyles();
            Event e = Event.current;
            if (_clearFocus)
            {
                _clearFocus = false;
                GUI.FocusControl(null);
            }

            // Fondo a pantalla completa (sin franjas negras).
            GUI.matrix = Matrix4x4.identity;
            DrawBackground();

            _scale = Mathf.Min(Screen.width / VW, Screen.height / VH);
            float ox = (Screen.width - VW * _scale) * 0.5f, oy = (Screen.height - VH * _scale) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0f), Quaternion.identity, new Vector3(_scale, _scale, 1f));

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Back();
                e.Use();
            }

            if (e.type == EventType.Repaint) _hoverIdThisFrame = "";

            float a = Mathf.SmoothStep(0f, 1f, _fade);
            GUI.color = new Color(1f, 1f, 1f, a);
            switch (_screen)
            {
                case MenuScreen.Main: DrawMain(); break;
                case MenuScreen.Mode: DrawMode(); break;
                case MenuScreen.Create: DrawCreate(); break;
                case MenuScreen.Map: DrawMap(); break;
                case MenuScreen.Options: DrawOptions(); break;
                case MenuScreen.Exit: DrawExit(); break;
                case MenuScreen.Loading: DrawLoading(); break;
            }
            GUI.color = Color.white;

            if (Time.unscaledTime - _errorTime < 5f)
            {
                Fill(new Rect(460, 20, 1000, 56), new Color(0.45f, 0.05f, 0.03f, 0.92f));
                Label(new Rect(460, 20, 1000, 56), _error, _textCenter, 24, TextLight);
            }

            if (e.type == EventType.Repaint) _hoverId = _hoverIdThisFrame;
            GUI.matrix = Matrix4x4.identity;
        }

        private void DrawBackground()
        {
            Texture2D bg = MenuArt.MenuBackground;
            float t = Time.unscaledTime;
            float zoom = 1.04f + 0.02f * Mathf.Sin(t * 0.07f);
            float sw = Screen.width, sh = Screen.height;
            float texAspect = bg.width / (float)bg.height;
            float w = sw, h = sw / texAspect;
            if (h < sh)
            {
                h = sh;
                w = sh * texAspect;
            }
            w *= zoom;
            h *= zoom;
            var r = new Rect((sw - w) * 0.5f + Mathf.Sin(t * 0.05f) * 10f, (sh - h) * 0.5f, w, h);
            GUI.DrawTexture(r, bg, ScaleMode.StretchToFill);

            // Polvo en suspensión.
            float s = Mathf.Max(sw / VW, sh / VH);
            for (int i = 0; i < _dust.Length; i++)
            {
                Vector3 d = _dust[i];
                float size = (2f + 4f * d.z) * s;
                GUI.color = new Color(1f, 0.82f, 0.62f, 0.12f + 0.18f * d.z);
                GUI.DrawTexture(new Rect(d.x * s, d.y * s, size, size), MenuArt.Circle);
            }
            GUI.color = Color.white;
        }

        // ------------------------------------------------------------------
        // Pantalla principal
        // ------------------------------------------------------------------

        private void DrawMain()
        {
            FancyText(new Rect(0, 60, VW, 175), titleTop, 150);
            FancyText(new Rect(0, 210, VW, 175), titleBottom, 150);

            // Adorno bajo el título.
            Tinted(new Rect(560, 392, 800, 6), MenuArt.FadeLine, new Color(1f, 0.45f, 0.12f, 0.95f));
            Tinted(new Rect(948, 386, 24, 24), MenuArt.Circle, new Color(1f, 0.5f, 0.15f, 1f));
            Tinted(new Rect(954, 392, 12, 12), MenuArt.Circle, new Color(1f, 0.85f, 0.55f, 1f));

            if (MetalButton("jugar", new Rect(660, 440, 600, 150), "JUGAR", MenuButtonKind.Orange, MenuArt.IconRover, 66))
                Show(MenuScreen.Mode);
            if (MetalButton("opciones", new Rect(690, 610, 540, 125), "OPCIONES", MenuButtonKind.Steel, MenuArt.IconGear, 54))
                Show(MenuScreen.Options);
            if (MetalButton("salir", new Rect(690, 755, 540, 125), "SALIR", MenuButtonKind.Red, MenuArt.IconExit, 54))
                Show(MenuScreen.Exit);

            var info = new Rect(40, 975, 640, 66);
            Tinted(info, MenuArt.Rounded, new Color(0.04f, 0.04f, 0.06f, 0.72f), 12);
            Label(new Rect(info.x + 24, info.y, info.width - 30, info.height),
                "VERSIÓN " + Application.version + "  |  USUARIO: <b>" + _commander + "</b>", _text, 24, TextLight);

            if (MarsRobot.Visuals.NasaModelLibrary.HasAny)
            {
                _text.alignment = TextAnchor.MiddleRight;
                ShadowLabel(new Rect(VW - 760, 990, 720, 40), MarsRobot.Visuals.NasaModelLibrary.Credit, 20, new Color(1f, 0.9f, 0.8f, 0.85f));
                _text.alignment = TextAnchor.MiddleLeft;
            }
        }

        // ------------------------------------------------------------------
        // Elegir modo
        // ------------------------------------------------------------------

        private void DrawMode()
        {
            Fill(new Rect(-500, -500, VW + 1000, VH + 1000), new Color(0f, 0f, 0f, 0.5f));
            FancyText(new Rect(0, 50, VW, 110), "ELIGE EL MODO DE JUEGO", 84);
            Label(new Rect(0, 160, VW, 40), "Podrás cambiarlo después al crear el mundo", _textCenter, 26, Muted);

            if (ModeCard("card_creativo", new Rect(300, 225, 620, 610), "CREATIVO", MenuArt.IconCube, CreativeColor,
                    "Construye sin límites",
                    new[] { "Recursos y energía ilimitados", "Sin daño ni averías", "Sin tormentas de polvo", "Ideal para diseñar tu colonia" }))
                PickMode(GameMode.Creative);

            if (ModeCard("card_supervivencia", new Rect(1000, 225, 620, 610), "SUPERVIVENCIA", MenuArt.IconHelmet, SurvivalColor,
                    "Sobrevive al planeta rojo",
                    new[] { "Gestiona batería, polvo y averías", "Tormentas y clima espacial", "Recoge, procesa y fabrica", "Prepara Marte para la tripulación" }))
                PickMode(GameMode.Survival);

            float backX = _lastWorld != null ? 420f : 760f;
            if (MetalButton("modo_atras", new Rect(backX, 875, 400, 110), "ATRÁS", MenuButtonKind.Steel, null, 42))
                Show(MenuScreen.Main);

            if (_lastWorld != null)
            {
                string n = _lastWorld.worldName.Length > 16 ? _lastWorld.worldName.Substring(0, 16) + "…" : _lastWorld.worldName;
                if (MetalButton("continuar", new Rect(900, 870, 600, 120), "CONTINUAR: " + n.ToUpperInvariant(), MenuButtonKind.Orange, null, 34))
                    StartGame(_lastWorld);
            }
        }

        private bool ModeCard(string id, Rect r, string name, Texture2D icon, Color accent, string tagline, string[] bullets)
        {
            float anim = HoverAnim(id, r);
            Rect dr = Grow(r, anim * 10f);

            if (anim > 0.01f) Tinted(Grow(dr, 10f), MenuArt.Rounded, new Color(accent.r, accent.g, accent.b, 0.35f * anim), 12);
            Box(dr);
            Tinted(new Rect(dr.x + 14, dr.y + 14, dr.width - 28, 190), MenuArt.Rounded, new Color(accent.r * 0.35f, accent.g * 0.35f, accent.b * 0.35f, 0.55f), 12);
            Tinted(new Rect(dr.center.x - 80, dr.y + 30, 160, 160), icon, Color.Lerp(accent, Color.white, 0.25f));

            FancyText(new Rect(dr.x, dr.y + 210, dr.width, 80), name, 60, Color.Lerp(accent, Color.white, 0.45f), accent * 0.75f);
            Label(new Rect(dr.x, dr.y + 290, dr.width, 40), tagline, _textCenter, 30, TextLight);
            Tinted(new Rect(dr.x + 80, dr.y + 340, dr.width - 160, 4), MenuArt.FadeLine, new Color(accent.r, accent.g, accent.b, 0.8f));

            for (int i = 0; i < bullets.Length; i++)
            {
                float y = dr.y + 352 + i * 46;
                Tinted(new Rect(dr.x + 95, y + 14, 14, 14), MenuArt.Circle, accent);
                Label(new Rect(dr.x + 125, y, dr.width - 160, 44), bullets[i], _text, 27, new Color(0.88f, 0.84f, 0.8f));
            }

            Label(new Rect(dr.x, dr.yMax - 62, dr.width, 44), anim > 0.5f ? "▶  ELEGIR" : "ELEGIR", _textCenter, 28,
                Color.Lerp(Muted, accent, anim));

            return ClickArea(r);
        }

        private void PickMode(GameMode mode)
        {
            _draft = new WorldSetup
            {
                mode = mode,
                difficulty = WorldDifficulty.Normal,
                worldName = WorldNames[Random.Range(0, WorldNames.Length)],
                seed = Random.Range(1, 999999999)
            };
            _seedText = _draft.seed.ToString();
            _hasPick = false;
            _pickPreset = null;
            Show(MenuScreen.Create);
        }

        // ------------------------------------------------------------------
        // Crear mundo
        // ------------------------------------------------------------------

        private void DrawCreate()
        {
            Fill(new Rect(-500, -500, VW + 1000, VH + 1000), new Color(0f, 0f, 0f, 0.55f));
            var win = new Rect(360, 100, 1200, 880);
            Box(win);
            FancyText(new Rect(win.x, win.y + 22, win.width, 80), "CREAR MUNDO", 66);
            bool survival = _draft.mode == GameMode.Survival;
            Label(new Rect(win.x, win.y + 100, win.width, 34), survival ? "Modo Supervivencia" : "Modo Creativo", _textCenter, 26,
                survival ? SurvivalColor : CreativeColor);
            Tinted(new Rect(win.x + 100, win.y + 140, win.width - 200, 4), MenuArt.FadeLine, new Color(1f, 0.5f, 0.15f, 0.7f));

            float lx = win.x + 60, cx = win.x + 380, cw = 760;

            // Nombre
            float y = win.y + 170;
            RowLabel(lx, y, "Nombre del mundo");
            _draft.worldName = TextField("nombre", new Rect(cx, y, cw, 62), _draft.worldName, 28);

            // Semilla
            y += 86;
            RowLabel(lx, y, "Semilla");
            string seed = TextField("semilla", new Rect(cx, y, cw - 240, 62), _seedText, 12);
            _seedText = FilterDigits(seed);
            if (FlatButton("semilla_rnd", new Rect(cx + cw - 225, y, 225, 62), "ALEATORIA", 24))
                _seedText = Random.Range(1, 999999999).ToString();

            // Modo
            y += 86;
            RowLabel(lx, y, "Modo de juego");
            int mode = Segmented("seg_modo", new Rect(cx, y, cw, 62), new[] { "CREATIVO", "SUPERVIVENCIA" }, (int)_draft.mode);
            _draft.mode = (GameMode)mode;

            // Dificultad
            y += 86;
            RowLabel(lx, y, "Dificultad");
            if (_draft.mode == GameMode.Survival)
            {
                _draft.difficulty = (WorldDifficulty)Segmented("seg_dif", new Rect(cx, y, cw, 62), new[] { "FÁCIL", "NORMAL", "DIFÍCIL" }, (int)_draft.difficulty);
            }
            else
            {
                Tinted(new Rect(cx, y, cw, 62), MenuArt.Rounded, new Color(0.12f, 0.12f, 0.14f, 0.8f), 12);
                Label(new Rect(cx + 20, y, cw - 40, 62), "Sin dificultad: sin daño, sin tormentas y recursos ilimitados", _text, 23, Muted);
            }

            // Punto de aparición
            y += 96;
            RowLabel(lx, y + 4, "Punto de aparición");
            var box = new Rect(cx, y, cw, 176);
            Tinted(box, MenuArt.Rounded, new Color(0.1f, 0.09f, 0.1f, 0.92f), 12);
            var thumb = new Rect(box.x + 12, box.y + 12, 304, 152);
            DrawMapTexture(thumb);
            if (_draft.hasSpawn)
            {
                Vector2 p = LatLonToPoint(thumb, _draft.spawnLatitude, MarsGlobalMap.ToSigned(_draft.spawnLongitudeEast));
                float pulse = 16f + 4f * Mathf.Sin(Time.unscaledTime * 5f);
                Tinted(new Rect(p.x - pulse * 0.5f, p.y - pulse * 0.5f, pulse, pulse), MenuArt.Ring, Cyan);
                Tinted(new Rect(p.x - 4, p.y - 4, 8, 8), MenuArt.Circle, Cyan);
            }
            if (ClickArea(thumb)) OpenMap();

            float tx = thumb.xMax + 22, tw = box.xMax - tx - 14;
            if (_draft.hasSpawn)
            {
                Label(new Rect(tx, box.y + 8, tw, 40), _draft.spawnRegion, _text, 30, Orange);
                Label(new Rect(tx, box.y + 46, tw, 30), MarsGlobalMap.FormatCoords(_draft.spawnLatitude, MarsGlobalMap.ToSigned(_draft.spawnLongitudeEast)), _text, 22, TextLight);
                Label(new Rect(tx, box.y + 76, tw, 30), string.IsNullOrEmpty(_draft.presetZoneId) ? "Terreno procedural" : "Zona con datos reales (NASA)", _text, 20, Muted);
            }
            else
            {
                Label(new Rect(tx, box.y + 10, tw, 70), "Aún no has elegido dónde aterrizar", _text, 26, new Color(1f, 0.7f, 0.45f));
            }
            if (FlatButton("elegir_mapa", new Rect(tx, box.yMax - 66, tw, 54), _draft.hasSpawn ? "CAMBIAR EN EL MAPA" : "ELEGIR EN EL MAPA", 23, true))
                OpenMap();

            // Botones
            if (MetalButton("crear_atras", new Rect(win.x + 60, win.yMax - 135, 400, 110), "ATRÁS", MenuButtonKind.Steel, null, 42))
                Show(MenuScreen.Mode);

            bool ready = _draft.hasSpawn && _draft.worldName.Trim().Length > 0;
            var createRect = new Rect(win.xMax - 540, win.yMax - 140, 480, 120);
            if (MetalButton("crear_ok", createRect, "CREAR MUNDO", MenuButtonKind.Orange, null, 46, ready))
                CreateWorld();
            if (!ready && createRect.Contains(Event.current.mousePosition))
                Label(new Rect(win.xMax - 540, win.yMax - 178, 480, 34), _draft.hasSpawn ? "Ponle un nombre al mundo" : "Elige primero un punto de aparición", _textCenter, 22, new Color(1f, 0.7f, 0.45f));
        }

        private void OpenMap()
        {
            _hasPick = _draft.hasSpawn;
            if (_hasPick)
            {
                _pickLat = _draft.spawnLatitude;
                _pickLon = MarsGlobalMap.ToSigned(_draft.spawnLongitudeEast);
                RefreshPick();
            }
            Show(MenuScreen.Map);
        }

        private void CreateWorld()
        {
            int seed;
            if (!int.TryParse(_seedText, out seed))
            {
                seed = string.IsNullOrEmpty(_seedText) ? Random.Range(1, 999999999) : _seedText.GetHashCode();
            }
            _draft.seed = seed;
            _draft.worldName = _draft.worldName.Trim();
            _draft.createdAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            StartGame(_draft);
        }

        // ------------------------------------------------------------------
        // Mapa de Marte: punto de aparición
        // ------------------------------------------------------------------

        private static readonly Rect MapRect = new Rect(240, 135, 1440, 720);

        private void DrawMap()
        {
            Fill(new Rect(-500, -500, VW + 1000, VH + 1000), new Color(0.04f, 0.025f, 0.02f, 0.96f));
            FancyText(new Rect(0, 14, VW, 84), "SELECCIONA TU PUNTO DE APARICIÓN", 60);
            Label(new Rect(0, 92, VW, 34), "Haz clic en cualquier lugar de Marte. Los rombos dorados son zonas con datos reales de la NASA.", _textCenter, 23, Muted);

            Rect m = MapRect;
            Box(Grow(m, 14f));
            DrawMapTexture(m);
            if (!MarsGlobalMap.IsReady)
            {
                DrawLegendAndButtons();
                return;
            }

            DrawGrid(m);
            DrawLandmarks(m);

            Event e = Event.current;
            Vector2 mouse = e.mousePosition;
            bool inMap = m.Contains(mouse);

            // Cursor: líneas de referencia y coordenadas.
            if (inMap)
            {
                Fill(new Rect(m.x, mouse.y, m.width, 1), new Color(1f, 1f, 1f, 0.22f));
                Fill(new Rect(mouse.x, m.y, 1, m.height), new Color(1f, 1f, 1f, 0.22f));
                float lat, lon;
                PointToLatLon(m, mouse, out lat, out lon);
                LandingZoneData preset = SnapPreset(m, mouse);
                string region = preset != null ? preset.displayName : MarsGlobalMap.Describe(lat, lon).Region;
                string tip = region + "\n" + MarsGlobalMap.FormatCoords(lat, lon) + "   ·   " + MarsGlobalMap.ElevationAt(lat, lon).ToString("+0.0;-0.0") + " km";
                var tr = new Rect(mouse.x + 18, mouse.y + 14, 330, 62);
                if (tr.xMax > m.xMax) tr.x = mouse.x - 18 - tr.width;
                if (tr.yMax > m.yMax) tr.y = mouse.y - 14 - tr.height;
                Tinted(tr, MenuArt.Rounded, new Color(0f, 0f, 0f, 0.75f), 12);
                Label(new Rect(tr.x + 12, tr.y + 4, tr.width - 20, tr.height - 8), tip, _text, 19, TextLight);

                if (e.type == EventType.MouseDown && e.button == 0)
                {
                    if (preset != null)
                    {
                        _pickLat = preset.latitude;
                        _pickLon = MarsGlobalMap.ToSigned(preset.longitudeEast);
                    }
                    else
                    {
                        _pickLat = lat;
                        _pickLon = lon;
                    }
                    _hasPick = true;
                    RefreshPick();
                    _audio.Click();
                    e.Use();
                }
            }

            // Marcador del punto elegido.
            if (_hasPick)
            {
                Vector2 p = LatLonToPoint(m, _pickLat, _pickLon);
                float pulse = 40f + 14f * Mathf.Sin(Time.unscaledTime * 4f);
                Tinted(new Rect(p.x - pulse * 0.5f, p.y - pulse * 0.5f, pulse, pulse), MenuArt.Ring, new Color(Cyan.r, Cyan.g, Cyan.b, 0.9f));
                Tinted(new Rect(p.x - 9, p.y - 9, 18, 18), MenuArt.Circle, Color.black);
                Tinted(new Rect(p.x - 7, p.y - 7, 14, 14), MenuArt.Circle, Cyan);
                string name = _pickPreset != null ? _pickPreset.displayName : _pickInfo.Region;
                float w = Mathf.Min(420f, 30f + name.Length * 15f);
                var nr = new Rect(p.x - w * 0.5f, p.y - 66, w, 38);
                nr.x = Mathf.Clamp(nr.x, m.x + 4, m.xMax - w - 4);
                if (nr.y < m.y + 4) nr.y = p.y + 30;
                Tinted(nr, MenuArt.Rounded, new Color(0.02f, 0.12f, 0.15f, 0.88f), 12);
                Label(nr, name, _textCenter, 22, Cyan);
            }

            DrawLegendAndButtons();
        }

        private void DrawLegendAndButtons()
        {
            // Ficha del punto elegido.
            var panel = new Rect(30, 872, 1370, 196);
            Box(panel);
            if (!MarsGlobalMap.IsReady)
            {
                Label(new Rect(panel.x + 40, panel.y, panel.width - 80, panel.height), "Generando el mapa de Marte…", _text, 30, Muted);
            }
            else if (!_hasPick)
            {
                Label(new Rect(panel.x + 40, panel.y + 22, panel.width - 80, 46), "Ningún punto seleccionado", _text, 34, Orange);
                Label(new Rect(panel.x + 40, panel.y + 74, panel.width - 80, 90),
                    "Elige dónde aterrizará tu cohete. Las zonas marcadas con rombo usan relieve y minerales reales; " +
                    "el resto del planeta se genera según su latitud, altura y rugosidad.", _text, 23, Muted);
            }
            else
            {
                string name = _pickPreset != null ? _pickPreset.displayName : _pickInfo.Region;
                Label(new Rect(panel.x + 40, panel.y + 16, 820, 46), name, _text, 36, Orange);

                string tag = _pickPreset != null ? "DATOS REALES · NASA" : "TERRENO PROCEDURAL";
                Color tagColor = _pickPreset != null ? new Color(1f, 0.8f, 0.3f) : new Color(0.6f, 0.75f, 0.85f);
                var tagRect = new Rect(panel.xMax - 340, panel.y + 22, 300, 36);
                Tinted(tagRect, MenuArt.Rounded, new Color(tagColor.r * 0.25f, tagColor.g * 0.25f, tagColor.b * 0.25f, 0.95f), 12);
                Label(tagRect, tag, _textCenter, 19, tagColor);

                int difficulty = _pickPreset != null ? (int)_pickPreset.difficulty : _pickInfo.DifficultyIndex;
                Label(new Rect(panel.xMax - 340, panel.y + 62, 300, 30), "Dificultad: " + LandingZoneData.DifficultyLabel((ZoneDifficulty)difficulty),
                    _textCenter, 21, DifficultyColor(difficulty));

                float minT = _pickPreset != null ? _pickPreset.minTemperatureC : _pickInfo.MinTempC;
                float maxT = _pickPreset != null ? _pickPreset.maxTemperatureC : _pickInfo.MaxTempC;
                float ice = _pickPreset != null ? _pickPreset.iceAbundance : _pickInfo.Ice01;
                Label(new Rect(panel.x + 40, panel.y + 64, 900, 30),
                    MarsGlobalMap.FormatCoords(_pickLat, _pickLon) + "      Elevación " + _pickInfo.ElevationKm.ToString("+0.0;-0.0") + " km",
                    _text, 23, TextLight);
                Label(new Rect(panel.x + 40, panel.y + 96, 1300, 30),
                    "Temperatura " + minT.ToString("0") + " / " + maxT.ToString("0") + " °C   ·   Presión " + _pickInfo.PressureMbar.ToString("0.0") +
                    " mbar   ·   Hielo de agua: " + Level(ice) + "   ·   Luz solar " + Mathf.RoundToInt(_pickInfo.Sunlight01 * 100f) + " %",
                    _text, 21, Muted);
                string desc = _pickPreset != null && !string.IsNullOrEmpty(_pickPreset.description) ? _pickPreset.description : _pickInfo.Description;
                Label(new Rect(panel.x + 40, panel.y + 130, panel.width - 80, 60), "<i>" + desc + "</i>", _text, 21, new Color(0.9f, 0.86f, 0.82f));
            }

            if (MetalButton("mapa_ok", new Rect(1420, 868, 470, 118), "CONFIRMAR", MenuButtonKind.Orange, null, 44, _hasPick && MarsGlobalMap.IsReady))
                ConfirmPick();
            if (MetalButton("mapa_rnd", new Rect(1420, 988, 232, 84), "ALEATORIO", MenuButtonKind.Steel, null, 25, MarsGlobalMap.IsReady))
            {
                _pickLat = Random.Range(-60f, 60f);
                _pickLon = Random.Range(-180f, 180f);
                _hasPick = true;
                RefreshPick();
            }
            if (MetalButton("mapa_cancel", new Rect(1658, 988, 232, 84), "CANCELAR", MenuButtonKind.Steel, null, 25))
                Show(MenuScreen.Create);
        }

        private void RefreshPick()
        {
            if (!MarsGlobalMap.IsReady) return;
            _pickPreset = SpawnZoneFactory.FindPreset(catalog, _pickLat, _pickLon);
            _pickInfo = MarsGlobalMap.Describe(_pickLat, _pickLon);
        }

        private void ConfirmPick()
        {
            _draft.hasSpawn = true;
            _draft.spawnLatitude = _pickLat;
            _draft.spawnLongitudeEast = MarsGlobalMap.ToEast(_pickLon);
            _draft.spawnRegion = _pickPreset != null ? _pickPreset.displayName : _pickInfo.Region;
            _draft.presetZoneId = _pickPreset != null ? _pickPreset.zoneId : "";
            Show(MenuScreen.Create);
        }

        private void DrawMapTexture(Rect r)
        {
            Texture2D map = MarsGlobalMap.Texture;
            if (map != null)
            {
                GUI.DrawTexture(r, map, ScaleMode.StretchToFill);
                return;
            }
            Fill(r, new Color(0.18f, 0.08f, 0.05f, 1f));
            float t = Time.unscaledTime;
            for (int i = 0; i < 8; i++)
            {
                float ang = t * 4f + i * Mathf.PI / 4f;
                float rad = Mathf.Min(r.width, r.height) * 0.08f;
                var c = new Vector2(r.center.x + Mathf.Cos(ang) * rad, r.center.y + Mathf.Sin(ang) * rad);
                float s = Mathf.Max(4f, rad * 0.22f);
                Tinted(new Rect(c.x - s * 0.5f, c.y - s * 0.5f, s, s), MenuArt.Circle, new Color(1f, 0.6f, 0.25f, (i + 1) / 8f));
            }
        }

        private void DrawGrid(Rect m)
        {
            for (int lon = -180; lon <= 180; lon += 30)
            {
                float x = m.x + (lon + 180f) / 360f * m.width;
                Fill(new Rect(x, m.y, 1, m.height), new Color(1f, 1f, 1f, lon == 0 ? 0.28f : 0.1f));
                if (lon > -180 && lon < 180)
                {
                    var lr = new Rect(x - 40, m.yMax - 24, 80, 22);
                    Label(new Rect(lr.x + 1, lr.y + 1, lr.width, lr.height), MarsGlobalMap.ToEast(lon).ToString("0") + "°E", _textCenter, 15, new Color(0f, 0f, 0f, 0.8f));
                    Label(lr, MarsGlobalMap.ToEast(lon).ToString("0") + "°E", _textCenter, 15, new Color(1f, 0.92f, 0.85f, 0.85f));
                }
            }
            for (int lat = -60; lat <= 60; lat += 30)
            {
                float y = m.y + (90f - lat) / 180f * m.height;
                Fill(new Rect(m.x, y, m.width, 1), new Color(1f, 1f, 1f, lat == 0 ? 0.28f : 0.1f));
                Label(new Rect(m.x - 78, y - 11, 62, 22), lat == 0 ? "0°" : Mathf.Abs(lat) + "°" + (lat > 0 ? "N" : "S"), _text, 15, Muted);
            }
        }

        private void DrawLandmarks(Rect m)
        {
            // Lugares conocidos (puntos) que no coinciden con una zona predefinida.
            for (int i = 0; i < MarsGlobalMap.Landmarks.Length; i++)
            {
                MarsLandmark lm = MarsGlobalMap.Landmarks[i];
                if (NearPreset(lm.Latitude, lm.Longitude, 12f)) continue;
                Vector2 p = LatLonToPoint(m, lm.Latitude, lm.Longitude);
                Tinted(new Rect(p.x - 6, p.y - 6, 12, 12), MenuArt.Circle, new Color(0f, 0f, 0f, 0.8f));
                Tinted(new Rect(p.x - 4, p.y - 4, 8, 8), MenuArt.Circle, new Color(1f, 0.9f, 0.75f));
                ShadowLabel(new Rect(p.x + 9, p.y - 12, 260, 24), lm.Name, 16, new Color(1f, 0.93f, 0.85f));
            }

            // Zonas predefinidas con datos reales: rombos dorados.
            if (catalog == null) return;
            for (int i = 0; i < catalog.zones.Count; i++)
            {
                LandingZoneData z = catalog.zones[i];
                if (z == null) continue;
                Vector2 p = LatLonToPoint(m, z.latitude, MarsGlobalMap.ToSigned(z.longitudeEast));
                Tinted(new Rect(p.x - 12, p.y - 12, 24, 24), MenuArt.Diamond, Color.white);
                // Cerca del borde derecho la etiqueta va a la izquierda del rombo.
                bool left = p.x > m.xMax - 200f;
                var lr = left ? new Rect(p.x - 292, p.y - 13, 280, 26) : new Rect(p.x + 14, p.y - 13, 280, 26);
                _text.alignment = left ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
                ShadowLabel(lr, z.displayName, 18, new Color(1f, 0.85f, 0.4f), FontStyle.Bold);
                _text.alignment = TextAnchor.MiddleLeft;
            }
        }

        private bool NearPreset(float lat, float lon, float degrees)
        {
            if (catalog == null) return false;
            for (int i = 0; i < catalog.zones.Count; i++)
            {
                LandingZoneData z = catalog.zones[i];
                if (z != null && MarsGlobalMap.AngularDistance(lat, lon, z.latitude, MarsGlobalMap.ToSigned(z.longitudeEast)) < degrees) return true;
            }
            return false;
        }

        private LandingZoneData SnapPreset(Rect m, Vector2 mouse)
        {
            if (catalog == null) return null;
            LandingZoneData best = null;
            float bestD = 16f;
            for (int i = 0; i < catalog.zones.Count; i++)
            {
                LandingZoneData z = catalog.zones[i];
                if (z == null) continue;
                float d = (LatLonToPoint(m, z.latitude, MarsGlobalMap.ToSigned(z.longitudeEast)) - mouse).magnitude;
                if (d < bestD)
                {
                    bestD = d;
                    best = z;
                }
            }
            return best;
        }

        private static Vector2 LatLonToPoint(Rect m, float lat, float lon)
        {
            return new Vector2(m.x + (lon + 180f) / 360f * m.width, m.y + (90f - lat) / 180f * m.height);
        }

        private static void PointToLatLon(Rect m, Vector2 p, out float lat, out float lon)
        {
            lon = Mathf.Clamp((p.x - m.x) / m.width * 360f - 180f, -180f, 180f);
            lat = Mathf.Clamp(90f - (p.y - m.y) / m.height * 180f, -90f, 90f);
        }

        private static string Level(float v)
        {
            if (v >= 0.7f) return "Muy alto";
            if (v >= 0.4f) return "Alto";
            if (v >= 0.15f) return "Moderado";
            return "Bajo";
        }

        private static Color DifficultyColor(int d)
        {
            if (d <= 1) return new Color(0.45f, 0.9f, 0.45f);
            if (d <= 3) return new Color(1f, 0.82f, 0.3f);
            return new Color(1f, 0.4f, 0.3f);
        }

        // ------------------------------------------------------------------
        // Opciones (usa los Ajustes del juego: Core/GameSettings)
        // ------------------------------------------------------------------

        private void DrawOptions()
        {
            Fill(new Rect(-500, -500, VW + 1000, VH + 1000), new Color(0f, 0f, 0f, 0.55f));
            var win = new Rect(410, 90, 1100, 900);
            Box(win);
            FancyText(new Rect(win.x, win.y + 22, win.width, 80), "OPCIONES", 66);
            Tinted(new Rect(win.x + 100, win.y + 110, win.width - 200, 4), MenuArt.FadeLine, new Color(1f, 0.5f, 0.15f, 0.7f));

            float lx = win.x + 60, cx = win.x + 420, cw = 520;
            float y = win.y + 140;

            RowLabel(lx, y, "Nombre del comandante");
            string name = TextField("comandante", new Rect(cx, y, cw + 100, 60), _commander, 18);
            if (name != _commander) _commander = name.ToUpperInvariant();

            y += 84;
            float master = SliderRow("Volumen general", y, cx, cw, GameSettings.MasterVolume, 0f, 1f, Pct(GameSettings.MasterVolume));
            y += 70;
            float music = SliderRow("Música / ambiente", y, cx, cw, GameSettings.MusicVolume, 0f, 1f, Pct(GameSettings.MusicVolume));
            y += 70;
            float fx = SliderRow("Efectos", y, cx, cw, GameSettings.EffectsVolume, 0f, 1f, Pct(GameSettings.EffectsVolume));
            if (master != GameSettings.MasterVolume || music != GameSettings.MusicVolume || fx != GameSettings.EffectsVolume)
                GameSettings.SetVolumes(master, music, fx);

            y += 70;
            float bright = SliderRow("Brillo", y, cx, cw, GameSettings.Brightness, 0.5f, 1.5f, Pct(GameSettings.Brightness));
            if (bright != GameSettings.Brightness) GameSettings.SetBrightness(bright);

            y += 70;
            float sens = SliderRow("Sensibilidad del ratón", y, cx, cw, GameSettings.MouseSensitivity, 0.2f, 8f, GameSettings.MouseSensitivity.ToString("0.0"));
            if (sens != GameSettings.MouseSensitivity) GameSettings.SetMouseSensitivity(sens);

            y += 80;
            RowLabel(lx, y, "Calidad gráfica");
            int q = Segmented("seg_calidad", new Rect(cx, y, cw + 100, 60), new[] { "BAJA", "MEDIA", "ALTA", "ULTRA" }, GameSettings.Quality);
            if (q != GameSettings.Quality) GameSettings.SetQuality(q);

            y += 84;
            RowLabel(lx, y, "Mostrar FPS");
            int fps = Segmented("seg_fps", new Rect(cx, y, 300, 60), new[] { "NO", "SÍ" }, GameSettings.ShowFps ? 1 : 0);
            if ((fps == 1) != GameSettings.ShowFps) GameSettings.SetShowFps(fps == 1);

            if (MetalButton("opc_reset", new Rect(win.x + 70, win.yMax - 140, 440, 110), "RESTABLECER", MenuButtonKind.Steel, null, 38))
            {
                GameSettings.SetVolumes(0.8f, 0.6f, 0.8f);
                GameSettings.SetBrightness(1f);
                GameSettings.SetMouseSensitivity(2f);
                GameSettings.SetQuality(1);
                GameSettings.SetShowFps(false);
            }
            if (MetalButton("opc_volver", new Rect(win.xMax - 510, win.yMax - 140, 440, 110), "VOLVER", MenuButtonKind.Orange, null, 42))
                CloseOptions();
        }

        private void CloseOptions()
        {
            if (string.IsNullOrEmpty(_commander.Trim())) _commander = "COMANDANTE";
            PlayerPrefs.SetString(CommanderKey, _commander.Trim());
            GameSettings.Save();
            Show(MenuScreen.Main);
        }

        private float SliderRow(string label, float y, float cx, float cw, float value, float min, float max, string valueText)
        {
            RowLabel(cx - 360, y - 6, label);
            var track = new Rect(cx, y + 12, cw, 12);
            float t = Mathf.InverseLerp(min, max, value);
            Tinted(track, MenuArt.Rounded, new Color(0.18f, 0.17f, 0.19f, 1f), 6);
            Tinted(new Rect(track.x, track.y, Mathf.Max(12f, track.width * t), track.height), MenuArt.Rounded, Orange, 6);
            float nv = GUI.HorizontalSlider(new Rect(cx - 14, y, cw + 28, 36), value, min, max, _sliderBg, _sliderThumb);
            float tx = track.x + track.width * Mathf.InverseLerp(min, max, nv);
            Tinted(new Rect(tx - 16, y + 2, 32, 32), MenuArt.Circle, new Color(0.1f, 0.05f, 0.02f, 0.9f));
            Tinted(new Rect(tx - 13, y + 5, 26, 26), MenuArt.Circle, OrangeLight);
            Label(new Rect(cx + cw + 30, y - 6, 130, 48), valueText, _text, 25, TextLight);
            return nv;
        }

        private static string Pct(float v)
        {
            return Mathf.RoundToInt(v * 100f) + " %";
        }

        // ------------------------------------------------------------------
        // Salir y carga
        // ------------------------------------------------------------------

        private void DrawExit()
        {
            Fill(new Rect(-500, -500, VW + 1000, VH + 1000), new Color(0f, 0f, 0f, 0.6f));
            var win = new Rect(560, 320, 800, 440);
            Box(win);
            FancyText(new Rect(win.x, win.y + 30, win.width, 90), "¿SALIR DEL JUEGO?", 64);
            Label(new Rect(win.x + 60, win.y + 140, win.width - 120, 90), "Tu última colonia queda guardada para continuar más tarde.", _textCenter, 28, Muted);
            if (MetalButton("salir_no", new Rect(win.x + 50, win.yMax - 160, 340, 115), "CANCELAR", MenuButtonKind.Steel, null, 38))
                Show(MenuScreen.Main);
            if (MetalButton("salir_si", new Rect(win.xMax - 390, win.yMax - 160, 340, 115), "SALIR", MenuButtonKind.Red, MenuArt.IconExit, 42))
                Quit();
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DrawLoading()
        {
            Fill(new Rect(-500, -500, VW + 1000, VH + 1000), new Color(0f, 0f, 0f, 0.82f));
            Label(new Rect(0, 380, VW, 50), "ATERRIZANDO EN", _textCenter, 36, Muted);
            FancyText(new Rect(0, 430, VW, 110), _loadingRegion, 84);
            var bar = new Rect(560, 590, 800, 20);
            Tinted(bar, MenuArt.Rounded, new Color(0.15f, 0.13f, 0.13f, 1f), 8);
            float p = Mathf.Clamp01(_loadProgress);
            Tinted(new Rect(bar.x, bar.y, Mathf.Max(20f, bar.width * p), bar.height), MenuArt.Rounded, Orange, 8);
            Label(new Rect(0, 630, VW, 40), _draft.mode == GameMode.Creative ? "Modo creativo · recursos ilimitados" : "Modo supervivencia · dificultad " + _draft.DifficultyLabel.ToLowerInvariant(), _textCenter, 24, Muted);
        }

        private void StartGame(WorldSetup setup)
        {
            _draft = setup;
            _loadingRegion = string.IsNullOrEmpty(setup.spawnRegion) ? "Marte" : setup.spawnRegion;
            _loadProgress = 0f;
            Show(MenuScreen.Loading);
            StartCoroutine(LoadGame(setup));
        }

        private IEnumerator LoadGame(WorldSetup setup)
        {
            _audio.FadeOutAmbient(0.8f);
            yield return new WaitForSecondsRealtime(0.35f);

            // Un punto libre necesita el mapa global para calcular su clima y relieve.
            while (string.IsNullOrEmpty(setup.presetZoneId) && !MarsGlobalMap.IsReady) yield return null;

            if (!Application.CanStreamedLevelBeLoaded(gameSceneName))
            {
                ShowError("La escena \"" + gameSceneName + "\" no está en Build Settings.");
                Show(MenuScreen.Create);
                yield break;
            }

            LandingZoneCatalog.SelectedZone = SpawnZoneFactory.Build(catalog, setup);
            GameModeState.Current = setup;
            GameModeState.SaveLast(setup);
            Debug.Log("[MainMenu] Mundo \"" + setup.worldName + "\" (" + setup.ModeLabel + ", " + setup.DifficultyLabel + ") en " +
                      LandingZoneCatalog.SelectedZone.displayName + " [" + LandingZoneCatalog.SelectedZone.zoneId + "], semilla " + setup.seed);

            AsyncOperation op = SceneManager.LoadSceneAsync(gameSceneName);
            while (op != null && !op.isDone)
            {
                _loadProgress = op.progress / 0.9f;
                yield return null;
            }
        }

        private void ShowError(string message)
        {
            _error = message;
            _errorTime = Time.unscaledTime;
            Debug.LogWarning("[MainMenu] " + message);
        }

        private void Back()
        {
            switch (_screen)
            {
                case MenuScreen.Main: Show(MenuScreen.Exit); break;
                case MenuScreen.Mode: Show(MenuScreen.Main); break;
                case MenuScreen.Create: Show(MenuScreen.Mode); break;
                case MenuScreen.Map: Show(MenuScreen.Create); break;
                case MenuScreen.Options: CloseOptions(); break;
                case MenuScreen.Exit: Show(MenuScreen.Main); break;
            }
        }

        // ------------------------------------------------------------------
        // Controles dibujados
        // ------------------------------------------------------------------

        /// <summary>Animación 0..1 al pasar el ratón; suena al entrar.</summary>
        private float HoverAnim(string id, Rect r, bool enabled = true)
        {
            bool hover = enabled && r.Contains(Event.current.mousePosition) && _fade > 0.6f;
            float a;
            _hoverAnim.TryGetValue(id, out a);
            if (Event.current.type == EventType.Repaint)
            {
                a = Mathf.MoveTowards(a, hover ? 1f : 0f, Time.unscaledDeltaTime * 8f);
                _hoverAnim[id] = a;
                if (hover)
                {
                    _hoverIdThisFrame = id;
                    if (_hoverId != id) _audio.Hover();
                }
            }
            return a;
        }

        private bool ClickArea(Rect r)
        {
            if (GUI.Button(r, GUIContent.none, GUIStyle.none))
            {
                _audio.Click();
                return true;
            }
            return false;
        }

        private bool MetalButton(string id, Rect r, string label, MenuButtonKind kind, Texture2D icon, int fontSize, bool enabled = true)
        {
            float anim = HoverAnim(id, r, enabled);
            Rect dr = Scale(r, 1f + 0.04f * anim);
            Color prev = GUI.color;
            if (!enabled) GUI.color = new Color(prev.r * 0.7f, prev.g * 0.7f, prev.b * 0.7f, prev.a * 0.55f);

            GUI.DrawTexture(dr, MenuArt.Button((int)r.width, (int)r.height, kind, anim > 0.5f ? 1 : 0));

            float inset = MenuArt.ButtonPad + Mathf.Clamp(r.height * 0.08f, 5f, 11f);
            Rect textRect = dr;
            if (icon != null)
            {
                float isz = (dr.height - inset * 2f) * 0.92f;
                var ir = new Rect(dr.x + inset + 22f, dr.center.y - isz * 0.5f, isz, isz);
                GUI.DrawTexture(ir, icon);
                textRect = new Rect(ir.xMax, dr.y, dr.xMax - ir.xMax - inset - 10f, dr.height);
            }
            int size = Mathf.RoundToInt(fontSize * (1f + 0.04f * anim));
            float alpha = GUI.color.a;
            GUI.color = Color.white;
            _title.fontSize = size;
            _title.alignment = TextAnchor.MiddleCenter;
            _title.normal.textColor = new Color(1f, 1f, 1f, 0.35f * alpha);
            GUI.Label(new Rect(textRect.x, textRect.y + 2f, textRect.width, textRect.height), label, _title);
            _title.normal.textColor = new Color(TextDark.r, TextDark.g, TextDark.b, alpha);
            GUI.Label(textRect, label, _title);

            GUI.color = prev;
            if (!enabled) return false;
            return ClickArea(r);
        }

        private bool FlatButton(string id, Rect r, string label, int fontSize, bool accent = false)
        {
            float anim = HoverAnim(id, r);
            Color bg = accent ? Color.Lerp(new Color(0.55f, 0.24f, 0.07f), Orange, anim) : Color.Lerp(new Color(0.22f, 0.22f, 0.25f), new Color(0.38f, 0.36f, 0.38f), anim);
            Tinted(r, MenuArt.Rounded, bg, 12);
            Label(r, label, _textCenter, fontSize, accent && anim > 0.5f ? TextDark : TextLight, FontStyle.Bold);
            return ClickArea(r);
        }

        private int Segmented(string id, Rect r, string[] options, int value)
        {
            float w = r.width / options.Length;
            for (int i = 0; i < options.Length; i++)
            {
                var sr = new Rect(r.x + i * w + 3f, r.y, w - 6f, r.height);
                float anim = HoverAnim(id + i, sr);
                bool on = i == value;
                Color bg = on ? Orange : Color.Lerp(new Color(0.17f, 0.17f, 0.19f, 0.95f), new Color(0.3f, 0.29f, 0.31f, 0.95f), anim);
                Tinted(sr, MenuArt.Rounded, bg, 12);
                Label(sr, options[i], _textCenter, 23, on ? TextDark : TextLight, FontStyle.Bold);
                if (ClickArea(sr)) value = i;
            }
            return value;
        }

        private string TextField(string id, Rect r, string value, int maxLength)
        {
            GUI.SetNextControlName(id);
            bool focused = GUI.GetNameOfFocusedControl() == id;
            _field.fontSize = 28;
            _fieldFocus.fontSize = 28;
            string v = GUI.TextField(r, value ?? "", maxLength, focused ? _fieldFocus : _field);
            return v;
        }

        private static string FilterDigits(string s)
        {
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
                if (char.IsDigit(s[i])) sb.Append(s[i]);
            return sb.ToString();
        }

        private void RowLabel(float x, float y, string text)
        {
            Label(new Rect(x, y, 330, 62), text, _text, 27, Muted);
        }

        // ------------------------------------------------------------------
        // Primitivas
        // ------------------------------------------------------------------

        private void Box(Rect r)
        {
            GUI.Box(r, GUIContent.none, _window);
        }

        private static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = new Color(c.r, c.g, c.b, c.a * prev.a);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        private static void Tinted(Rect r, Texture2D tex, Color c, int border = 0)
        {
            Color prev = GUI.color;
            var tint = new Color(c.r, c.g, c.b, c.a * prev.a);
            if (border > 0)
            {
                // Rectángulo redondeado relleno (borderWidth 0 = relleno; el radio redondea las esquinas).
                float radius = Mathf.Min(border, Mathf.Min(r.width, r.height) * 0.5f);
                GUI.color = Color.white;
                GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, tint, 0f, radius);
            }
            else
            {
                GUI.color = tint;
                GUI.DrawTexture(r, tex, ScaleMode.StretchToFill);
            }
            GUI.color = prev;
        }

        private void Label(Rect r, string text, GUIStyle style, int size, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            style.fontSize = size;
            style.fontStyle = fontStyle;
            style.normal.textColor = new Color(color.r, color.g, color.b, color.a * GUI.color.a);
            Color prev = GUI.color;
            GUI.color = Color.white;
            GUI.Label(r, text, style);
            GUI.color = prev;
        }

        private void ShadowLabel(Rect r, string text, int size, Color color, FontStyle fontStyle = FontStyle.Normal)
        {
            Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, _text, size, new Color(0f, 0f, 0f, 0.9f), fontStyle);
            Label(r, text, _text, size, color, fontStyle);
        }

        /// <summary>Texto grande con contorno, sombra y degradado vertical (como el título de la portada).</summary>
        private void FancyText(Rect r, string text, int size)
        {
            FancyText(r, text, size, OrangeLight, OrangeDark);
        }

        private void FancyText(Rect r, string text, int size, Color top, Color bottom)
        {
            float alpha = GUI.color.a;
            Color prev = GUI.color;
            GUI.color = Color.white;
            _title.fontSize = size;
            _title.alignment = TextAnchor.MiddleCenter;

            // Alto real del texto para que el degradado cubra las letras.
            float lineH = _title.CalcHeight(new GUIContent(text), r.width);
            var tr = new Rect(r.x, r.center.y - lineH * 0.5f, r.width, lineH);

            float o = Mathf.Max(2f, size * 0.03f);
            _title.normal.textColor = new Color(0f, 0f, 0f, 0.55f * alpha);
            GUI.Label(new Rect(tr.x, tr.y + size * 0.07f, tr.width, tr.height), text, _title);
            _title.normal.textColor = new Color(0.22f, 0.07f, 0.02f, alpha);
            for (int i = 0; i < 8; i++)
            {
                float ang = i * Mathf.PI / 4f;
                GUI.Label(new Rect(tr.x + Mathf.Cos(ang) * o, tr.y + Mathf.Sin(ang) * o, tr.width, tr.height), text, _title);
            }

            // Degradado: franjas horizontales recortadas con grupos.
            const int bands = 10;
            float top0 = tr.y + lineH * 0.18f, bottom0 = tr.yMax - lineH * 0.18f;
            for (int b = 0; b < bands; b++)
            {
                float y0 = b == 0 ? tr.y : Mathf.Lerp(top0, bottom0, b / (float)bands);
                float y1 = b == bands - 1 ? tr.yMax : Mathf.Lerp(top0, bottom0, (b + 1) / (float)bands);
                Color c = Color.Lerp(top, bottom, (b + 0.5f) / bands);
                _title.normal.textColor = new Color(c.r, c.g, c.b, alpha);
                GUI.BeginGroup(new Rect(tr.x, y0, tr.width, y1 - y0));
                GUI.Label(new Rect(0f, tr.y - y0, tr.width, tr.height), text, _title);
                GUI.EndGroup();
            }
            GUI.color = prev;
        }

        private static Rect Grow(Rect r, float d)
        {
            return new Rect(r.x - d, r.y - d, r.width + d * 2f, r.height + d * 2f);
        }

        private static Rect Scale(Rect r, float s)
        {
            float w = r.width * s, h = r.height * s;
            return new Rect(r.center.x - w * 0.5f, r.center.y - h * 0.5f, w, h);
        }

        private void EnsureStyles()
        {
            if (_title != null) return;

            _titleFont = LoadFont(new[] { "Impact", "Arial Black", "Segoe UI Black", "Arial" }, 64);
            _bodyFont = LoadFont(new[] { "Segoe UI Semibold", "Segoe UI", "Arial" }, 32);

            _title = new GUIStyle(GUI.skin.label) { font = _titleFont, alignment = TextAnchor.MiddleCenter, richText = false, wordWrap = false, clipping = TextClipping.Overflow };
            _text = new GUIStyle(GUI.skin.label) { font = _bodyFont, alignment = TextAnchor.MiddleLeft, richText = true, wordWrap = true };
            _textCenter = new GUIStyle(_text) { alignment = TextAnchor.MiddleCenter };

            _field = new GUIStyle(GUI.skin.textField) { font = _bodyFont, fontSize = 28, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(18, 18, 8, 8) };
            _field.normal.background = _field.hover.background = MenuArt.InputBox(false);
            _field.focused.background = _field.active.background = MenuArt.InputBox(true);
            _field.border = new RectOffset(12, 12, 12, 12);
            _field.normal.textColor = _field.hover.textColor = _field.focused.textColor = _field.active.textColor = TextLight;
            _fieldFocus = new GUIStyle(_field);
            _fieldFocus.normal.background = _fieldFocus.hover.background = MenuArt.InputBox(true);

            _sliderBg = new GUIStyle { fixedHeight = 36 };
            _sliderThumb = new GUIStyle { fixedWidth = 28, fixedHeight = 36 };

            _window = new GUIStyle { border = new RectOffset(30, 30, 30, 30) };
            _window.normal.background = MenuArt.Window;
        }

        private static Font LoadFont(string[] names, int size)
        {
            try
            {
                string[] installed = Font.GetOSInstalledFontNames();
                for (int i = 0; i < names.Length; i++)
                {
                    if (System.Array.IndexOf(installed, names[i]) >= 0) return Font.CreateDynamicFontFromOSFont(names[i], size);
                }
            }
            catch (System.Exception)
            {
                // Sin fuentes del sistema: se usa la de Unity.
            }
            return GUI.skin.font;
        }
    }
}
