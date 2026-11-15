using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Robot;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MarsRobot.Spawns
{
    /// <summary>
    /// Aplica el modo elegido en el menú dentro de la escena de juego:
    /// - Creativo: batería siempre llena, robot reparado, almacén lleno de recursos.
    /// - Supervivencia: el juego normal (la dificultad ya se aplicó a la zona).
    /// Muestra una etiqueta con el mundo y, con el ratón libre (Esc), un botón "Menú principal".
    /// Se crea solo al cargar una escena con TestSceneBootstrap si se vino desde el menú.
    /// </summary>
    public sealed class GameModeRuntime : MonoBehaviour
    {
        /// <summary>Nombre de la escena del menú principal (debe estar en Build Settings).</summary>
        public const string MenuSceneName = "MenuPrincipal";

        private WorldSetup _setup;
        private LandingZoneSpawner _spawner;
        private RobotHealthSystem _health;
        private BatterySystem _battery;
        private bool _filled;
        private float _nextRepair;
        private GUIStyle _badge, _button;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (GameModeState.Current == null) return;
            if (FindAnyObjectByType<TestSceneBootstrap>() == null) return;
            if (FindAnyObjectByType<GameModeRuntime>() != null) return;
            new GameObject("Modo de juego").AddComponent<GameModeRuntime>();
            Debug.Log("[GameModeRuntime] Modo " + GameModeState.Current.ModeLabel + " activo en la escena " + scene.name + ".");
        }

        private void Awake()
        {
            _setup = GameModeState.Current;
            if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-menuautoshot") >= 0) StartCoroutine(AutoShot());
        }

        /// <summary>Prueba (argumento -menuautoshot): captura la partida recién creada y sale.</summary>
        private System.Collections.IEnumerator AutoShot()
        {
            float t = 0f;
            while ((_spawner == null || !_spawner.IsReady) && t < 40f)
            {
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            string dir = System.IO.Path.Combine(Application.dataPath, "..", "MenuShots");
            System.Func<string, string> P = n => System.IO.Path.Combine(dir, n + ".png");
            yield return new WaitForSecondsRealtime(4f);
            // Cerrar el diálogo de bienvenida (pausa el juego) para poder probar.
            for (int k = 0; k < 6 && Time.timeScale < 0.01f; k++)
            {
                MarsRobot.Dialogue.DialogueManager.DebugSkip();
                yield return new WaitForSecondsRealtime(0.3f);
            }
            yield return MarsRobot.UI.Menu.DebugScreenshot.Capture(P("10_astronauta_1a_persona"));

            var crew = MarsRobot.Crew.CrewControl.Instance;
            var rocketBase = _spawner.RocketBase;
            Transform ship = rocketBase.Mothership.transform;

            var camGo = new GameObject("Camara de prueba");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = RenderSettings.skybox != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            cam.fieldOfView = 75f;
            cam.nearClipPlane = 0.05f;
            cam.enabled = false;
            System.Action<Vector3, Vector3, string> shot = (from, to, name) =>
            {
                camGo.transform.position = ship.TransformPoint(from);
                camGo.transform.LookAt(ship.TransformPoint(to));
                MarsRobot.UI.Menu.DebugScreenshot.RenderCamera(cam, P(name));
            };
            float shopY = MarsRobot.Base.Ship.ShipModel.LevelFloorY(MarsRobot.Base.Ship.ShipModel.WorkshopLevel);
            float hangarY = MarsRobot.Base.Ship.ShipModel.LevelFloorY(MarsRobot.Base.Ship.ShipModel.HangarLevel);
            shot(new Vector3(2.8f, shopY + 2.6f, 3.2f), new Vector3(-1.5f, shopY + 0.6f, -1.0f), "11_talleres_izquierda");
            shot(new Vector3(-2.8f, shopY + 2.6f, 3.2f), new Vector3(1.5f, shopY + 0.6f, -1.0f), "12_talleres_derecha");
            shot(new Vector3(2.5f, hangarY + 2.7f, 3.6f), new Vector3(0f, hangarY + 0.5f, -0.5f), "13_hangar_robot");
            shot(new Vector3(22f, 14f, 30f), new Vector3(0f, 20f, 0f), "14_cohete_exterior");
            for (int lvl = 2; lvl <= 5; lvl++)
            {
                float ly = MarsRobot.Base.Ship.ShipModel.LevelFloorY(lvl);
                shot(new Vector3(0.6f, ly + 2.75f, -1.9f), new Vector3(0f, ly + 0.4f, 2.2f), "20_nivel" + (lvl + 1) + "_desde_montacargas");
            }

            // Huerto: plantar en las 6 bandejas y adelantar el crecimiento para ver las plantas.
            string[][] plan = { new[] { "tomate", "lechuga", "trigo" }, new[] { "patata", "rabano", "soja" } };
            float[][] growth = { new[] { 0.95f, 0.85f, 0.97f }, new[] { 0.8f, 0.92f, 0.88f } };
            int g = 0;
            foreach (var st in FindObjectsByType<MarsRobot.Crafting.CraftingStation>(FindObjectsSortMode.None))
            {
                if (st.Station != MarsRobot.Crafting.CraftStation.Garden || g > 1) continue;
                for (int tr = 0; tr < 3; tr++)
                {
                    MarsRobot.Crafting.CraftingRecipe rec = null;
                    foreach (var r in MarsRobot.Crafting.RecipeBook.For(MarsRobot.Crafting.CraftStation.Garden)) if (r.OutputId == plan[g][tr]) rec = r;
                    st.Inventory.Add(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.SeedOf(plan[g][tr])), 1);
                    st.Inventory.Add(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.Water), 2);
                    st.Inventory.Add(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.Nutrients), 1);
                    st.Queue.Enqueue(rec, 1, st.Inventory);
                    st.Queue.Entries[tr].Progress = MarsRobot.Crafting.CraftingQueue.Duration(rec) * growth[g][tr];
                }
                yield return new WaitForSecondsRealtime(0.6f);
                Transform s = st.transform;
                camGo.transform.position = s.position + s.forward * 2.3f + s.right * 0.3f + Vector3.up * 1.35f;
                camGo.transform.LookAt(s.position + Vector3.up * 1.15f);
                MarsRobot.UI.Menu.DebugScreenshot.RenderCamera(cam, P("21_huerto_" + (g == 0 ? "A" : "B")));
                Debug.Log("[AutoShot] Huerto " + g + ": " + st.Queue.Entries.Count + " bandejas cultivando.");
                g++;
            }
            if (crew != null)
            {
                Transform a = crew.Astronaut.transform;
                crew.Astronaut.SetThirdPerson(true);
                yield return new WaitForSecondsRealtime(0.5f);
                camGo.transform.position = a.position + a.forward * 2.0f + a.right * 0.7f + Vector3.up * 1.4f;
                camGo.transform.LookAt(a.position + Vector3.up * 1.0f);
                MarsRobot.UI.Menu.DebugScreenshot.RenderCamera(cam, P("15_astronauta_traje"));
                yield return MarsRobot.UI.Menu.DebugScreenshot.Capture(P("15b_astronauta_3a_persona"));
                crew.Astronaut.SetThirdPerson(false);

                // Inventario y fabricación: materiales al astronauta y abrir el Fabricador.
                var inv = crew.Astronaut.Inventory;
                inv.Add(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.IronIngot), 6);
                inv.Add(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.Polymer), 3);
                inv.Add(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.IronOre), 4);
                MarsRobot.Crafting.CraftingStation fab = null;
                foreach (var st in FindObjectsByType<MarsRobot.Crafting.CraftingStation>(FindObjectsSortMode.None))
                    if (st.Station == MarsRobot.Crafting.CraftStation.Fabricator) fab = st;
                if (fab != null)
                {
                    fab.Inventory.Add(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.IronIngot), 4);
                    fab.Inventory.Add(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.Polymer), 2);
                    MarsRobot.Crafting.CraftingWindow.OpenStation(crew.Astronaut, fab);
                    yield return null;
                    MarsRobot.Crafting.CraftingWindow.DebugSelectRecipe(2); // cableado
                    fab.Queue.Enqueue(MarsRobot.Crafting.RecipeBook.For(MarsRobot.Crafting.CraftStation.Fabricator)[0], 2, fab.Inventory);
                    yield return new WaitForSecondsRealtime(2.5f);
                    yield return MarsRobot.UI.Menu.DebugScreenshot.Capture(P("16_fabricacion"));
                    MarsRobot.Crafting.CraftingWindow.CloseWindow();
                    yield return new WaitForSecondsRealtime(9f);
                    Debug.Log("[AutoShot] Fabricador tras 9 s: " + fab.Inventory.Count(MarsRobot.Items.ItemDatabase.Get(MarsRobot.Items.ItemDatabase.MetalParts)) + " piezas metálicas.");
                }

                // Cambiar al robot (está acoplado en el hangar).
                Debug.Log("[AutoShot] Robot en el hangar: " + crew.RobotDocked);
                crew.SetAstronaut(false);
                yield return new WaitForSecondsRealtime(1.5f);
                yield return MarsRobot.UI.Menu.DebugScreenshot.Capture(P("17_control_robot"));

                // Ascensor: bajar el robot al suelo.
                if (rocketBase.Elevator != null)
                {
                    yield return rocketBase.Elevator.DebugRide(true);
                    yield return new WaitForSecondsRealtime(1f);
                    Vector3 rl = ship.InverseTransformPoint(FindAnyObjectByType<RobotController>().transform.position);
                    Debug.Log("[AutoShot] Robot tras bajar en el ascensor: y local = " + rl.y.ToString("0.00") + " (suelo del ascensor " + rocketBase.Elevator.BottomY.ToString("0.00") + "), docked=" + crew.RobotDocked);
                    shot(new Vector3(9f, rl.y + 3f, 16f), rl + Vector3.up * 1f, "18_ascensor_abajo");
                    yield return rocketBase.Elevator.DebugRide(false);
                    yield return new WaitForSecondsRealtime(1f);
                    rl = ship.InverseTransformPoint(FindAnyObjectByType<RobotController>().transform.position);
                    Debug.Log("[AutoShot] Robot tras subir: y local = " + rl.y.ToString("0.00"));
                }
            }
            Debug.Log("[GameModeRuntime] Capturas de partida hechas.");
            yield return new WaitForSecondsRealtime(1.5f);
            Application.Quit();
        }

        private void Update()
        {
            if (_setup == null) return;
            if (_spawner == null) _spawner = FindAnyObjectByType<LandingZoneSpawner>();
            if (_spawner == null || !_spawner.IsReady) return;

            if (_setup.mode != GameMode.Creative) return;

            if (_health == null) _health = FindAnyObjectByType<RobotHealthSystem>();
            if (_battery == null) _battery = FindAnyObjectByType<BatterySystem>();

            if (_health != null)
            {
                _health.SetZoneDamageMultiplier(0f);
                if (Time.time >= _nextRepair)
                {
                    _health.RepairAll();
                    _nextRepair = Time.time + 2f;
                }
            }
            if (_battery != null && _battery.Charge01 < 0.999f) _battery.AddEnergy(_battery.CapacityWh);

            RocketBase rocketBase;
            if (GameServices.TryGet(out rocketBase) && rocketBase.Inventory != null)
            {
                // La primera vez llena el almacén; después lo repone poco a poco.
                int amount = _filled ? 5 : 999;
                if (!_filled || Time.frameCount % 120 == 0)
                {
                    foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType))) rocketBase.Inventory.Add(t, amount);
                    _filled = true;
                }
            }
        }

        private void OnGUI()
        {
            if (_setup == null) return;
            if (_badge == null)
            {
                _badge = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight, richText = true };
                _badge.normal.textColor = new Color(1f, 0.85f, 0.65f);
                _button = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            }

            string color = _setup.mode == GameMode.Creative ? "#5fd0ff" : "#ff8a3d";
            string text = "<color=" + color + ">" + _setup.ModeLabel.ToUpperInvariant() + "</color>" +
                          (_setup.mode == GameMode.Survival ? " · " + _setup.DifficultyLabel : "") + "  —  " + _setup.worldName;
            var r = new Rect(Screen.width - 520, Screen.height - 34, 506, 26);
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(new Rect(r.x + 120, r.y, r.width - 120 + 6, r.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(r, text, _badge);

            // Con el ratón libre (Esc) se puede volver al menú principal.
            if (Cursor.lockState != CursorLockMode.Locked)
            {
                if (GUI.Button(new Rect(Screen.width - 190, Screen.height - 78, 176, 38), "Menú principal", _button))
                {
                    Debug.Log("[GameModeRuntime] Volviendo al menú principal.");
                    Time.timeScale = 1f;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                    SceneManager.LoadScene(MenuSceneName);
                }
            }
        }
    }
}
