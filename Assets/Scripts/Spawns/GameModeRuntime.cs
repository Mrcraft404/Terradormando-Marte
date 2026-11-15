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
            yield return new WaitForSecondsRealtime(4f);
            yield return MarsRobot.UI.Menu.DebugScreenshot.Capture(System.IO.Path.Combine(Application.dataPath, "..", "MenuShots", "10_juego.png"));
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
