using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using MarsRobot.World.Weather;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MarsRobot.Spawns
{
    /// <summary>
    /// Escena de prueba que se monta sola por código: sol, clima, terreno, robot con cámara y un
    /// panel de depuración (IMGUI, sin paquetes extra). Así la escena .unity solo necesita un objeto
    /// con este componente y no hay que arrastrar referencias a mano.
    ///
    /// Teclas de prueba: F1–F6 cambiar de zona · F8 daño aleatorio · F9 reparar todo ·
    /// F10 tormenta de polvo · (+ los controles normales del robot).
    /// </summary>
    public sealed class TestSceneBootstrap : MonoBehaviour
    {
        [Tooltip("Catálogo con las 6 zonas (F1–F6).")]
        [SerializeField] private LandingZoneCatalog catalog;

        [Tooltip("Zona con la que arranca la prueba si no se eligió otra.")]
        [SerializeField] private LandingZoneData defaultZone;

        [SerializeField] private bool showDebugPanel = true;

        private LandingZoneData _zone;
        private TerrainManager _terrain;
        private MarsEnvironmentSystem _environment;
        private RobotHealthSystem _health;
        private BatterySystem _battery;
        private DustingSystem _dusting;
        private RobotController _controller;
        private RobotRepairController _repair;
        private RobotCameraRig _cameraRig;
        private MarsTerrainGenerator _marsData;
        private LandingZoneSpawner _spawner;

        /// <summary>
        /// Cuando el robot ya ha aterrizado, aparca el MarteCar en la base: a la izquierda de la
        /// plataforma de carga, lejos del radio de las máquinas para que la F no se confunda.
        /// </summary>
        private System.Collections.IEnumerator Start()
        {
            while (_spawner != null && !_spawner.IsReady) yield return null;
            if (_spawner == null || _spawner.RocketBase == null) yield break;

            Transform baseRoot = _spawner.RocketBase.transform;
            // El robot aparece en (0, 0, 11) mirando al cohete (−Z), así que su izquierda es +X.
            Vector3 parking = baseRoot.TransformPoint(new Vector3(7f, 0f, 13f));
            parking.y = _terrain.GetHeightAt(parking);
            MarsRobot.Vehicles.MarteCarFactory.Create(parking, baseRoot.rotation);
            ShowMessage("MarteCar aparcado a la izquierda de la plataforma: acércate y pulsa F.");
        }
        private string _lastMessage = "";
        private float _messageTime;
        private GUIStyle _style;

        private void Awake()
        {
            _zone = LandingZoneCatalog.SelectedZone != null ? LandingZoneCatalog.SelectedZone : defaultZone;
            if (_zone == null)
            {
                // Sin assets (p. ej. si se borraron): zona por defecto en memoria.
                _zone = ScriptableObject.CreateInstance<LandingZoneData>();
                _zone.displayName = "Zona de prueba";
            }

            ConfigureRendering();
            Light sun = CreateSun();
            _environment = new GameObject("Clima").AddComponent<MarsEnvironmentSystem>();
            _environment.SunLight = sun;

            _terrain = new GameObject("Terreno").AddComponent<TerrainManager>();
            _terrain.SetMaterial(MarsGroundMaterialFactory.Create(_zone.terrain.seed));

            Rigidbody body = CreateRobot();

            // Mapa real (NASA) si se generó con Tools/MapProcessor; si no, terreno procedural.
            _marsData = _terrain.gameObject.AddComponent<MarsTerrainGenerator>();

            var spawner = gameObject.AddComponent<LandingZoneSpawner>();
            _spawner = spawner;
            spawner.Configure(_zone, _terrain, _environment, body, _health, _controller, _dusting);
            spawner.SetTerrainGenerator(_marsData);

            _repair.RepairBlocked += (m, reason) => ShowMessage(reason);
            _health.CableCut += m => ShowMessage("¡Cable cortado: " + m.Definition.displayName + "!");
            _dusting.DustWarning += () => ShowMessage("Paneles sucios: pulsa C para limpiarlos.");
        }

        // ------------------------------------------------------------------
        // Construcción de la escena
        // ------------------------------------------------------------------

        private static void ConfigureRendering()
        {
            // Bruma marciana: oculta la carga de chunks lejanos.
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.78f, 0.6f, 0.46f);
            RenderSettings.fogStartDistance = 120f;
            RenderSettings.fogEndDistance = 420f;
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.36f, 0.3f);
        }

        private static Light CreateSun()
        {
            var go = new GameObject("Sol");
            var light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1f, 0.93f, 0.85f);
            light.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(50f, 30f, 0f);
            return light;
        }

        private Rigidbody CreateRobot()
        {
            var robot = new GameObject("Robot");

            var body = robot.AddComponent<Rigidbody>();
            body.mass = 300f;

            var capsule = robot.AddComponent<CapsuleCollider>();
            capsule.radius = 0.6f;
            capsule.height = 1.4f;
            capsule.center = new Vector3(0f, 0.7f, 0f);
            capsule.material = CreateSlipperyMaterial();

            // Orden importante: cada Awake busca a los anteriores con GetComponent.
            _health = robot.AddComponent<RobotHealthSystem>();
            _battery = robot.AddComponent<BatterySystem>();
            _dusting = robot.AddComponent<DustingSystem>();
            _controller = robot.AddComponent<RobotController>();
            _repair = robot.AddComponent<RobotRepairController>();
            robot.AddComponent<RobotUpgrades>();   // mejoras instaladas en el taller
            robot.AddComponent<RobotToolbelt>();   // L foco · Z escáner · X taladro de muestras
            robot.AddComponent<BuildModeController>(); // B: construir (antes que RobotBaseLink, que lo busca en Awake)
            robot.AddComponent<RobotBaseLink>();   // F: usar las máquinas y estructuras de la base

            // Aspecto PROVISIONAL (cuerpo, mástil, cabeza y cuatro ruedas, sin colisionadores propios).
            // Se sustituirá por el modelo del diseño del robot: basta con quitar este bloque y meter
            // el modelo como hijo de "Robot", con la base en y = 0 y mirando hacia +Z.
            var visual = new GameObject("Modelo (provisional)").transform;
            visual.SetParent(robot.transform, false);
            Material hull = CreateMaterial(new Color(0.85f, 0.85f, 0.8f));
            Material dark = CreateMaterial(new Color(0.15f, 0.15f, 0.15f));
            Material accent = CreateMaterial(new Color(0.95f, 0.55f, 0.1f));
            AddVisual(PrimitiveType.Cube, visual, new Vector3(0f, 0.75f, 0f), new Vector3(1.4f, 0.5f, 2f), Quaternion.identity, hull);
            AddVisual(PrimitiveType.Cube, visual, new Vector3(0f, 1.02f, -0.35f), new Vector3(1.6f, 0.05f, 1.1f), Quaternion.identity, dark); // panel solar
            AddVisual(PrimitiveType.Cube, visual, new Vector3(0f, 1.15f, 0.6f), new Vector3(0.15f, 0.6f, 0.15f), Quaternion.identity, dark);  // mástil
            AddVisual(PrimitiveType.Cube, visual, new Vector3(0f, 1.5f, 0.62f), new Vector3(0.5f, 0.22f, 0.25f), Quaternion.identity, accent); // cabeza
            Quaternion wheelRot = Quaternion.Euler(0f, 0f, 90f);
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    AddVisual(PrimitiveType.Cylinder, visual, new Vector3(0.8f * x, 0.4f, 0.75f * z),
                        new Vector3(0.7f, 0.12f, 0.7f), wheelRot, dark);
                }
            }

            // Pivote de la "cabeza": el controlador lo inclina con el ratón.
            var head = new GameObject("Cabeza");
            head.transform.SetParent(robot.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.5f, 0.78f);
            _controller.CameraPivot = head.transform;

            // La cámara es hija del pivote; el rig la coloca en 1ª o 3ª persona (tecla V).
            var camGo = new GameObject("Camara");
            camGo.transform.SetParent(head.transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            camGo.AddComponent<AudioListener>();
            camGo.tag = "MainCamera";

            // En primera persona se oculta la cabeza (está justo delante de la cámara).
            Renderer headRenderer = visual.GetChild(3).GetComponent<Renderer>();
            _cameraRig = robot.AddComponent<RobotCameraRig>();
            _cameraRig.Setup(head.transform, camGo.transform, new[] { headRenderer });

            return body;
        }

        private static void AddVisual(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }

        /// <summary>Material simple compatible con Built-in y con URP.</summary>
        private static Material CreateMaterial(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");
            var mat = new Material(shader);
            mat.color = color;
            return mat;
        }

#if UNITY_6000_0_OR_NEWER
        private static PhysicsMaterial CreateSlipperyMaterial()
        {
            var m = new PhysicsMaterial("RobotSinFriccion");
            m.frictionCombine = PhysicsMaterialCombine.Minimum;
#else
        private static PhysicMaterial CreateSlipperyMaterial()
        {
            var m = new PhysicMaterial("RobotSinFriccion");
            m.frictionCombine = PhysicMaterialCombine.Minimum;
#endif
            // Sin fricción: el controlador ya fija la velocidad; la fricción solo frenaría a tirones.
            m.dynamicFriction = 0f;
            m.staticFriction = 0f;
            return m;
        }

        // ------------------------------------------------------------------
        // Teclas de prueba
        // ------------------------------------------------------------------

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < 6; i++)
            {
                if (Input.GetKeyDown(KeyCode.F1 + i)) LoadZone(i);
            }
            if (Input.GetKeyDown(KeyCode.H))
            {
                // El panel de depuración ocupa mucho en la vista Game pequeña del editor.
                showDebugPanel = !showDebugPanel;
            }
            if (Input.GetKeyDown(KeyCode.F7))
            {
                // Prueba: mientras no exista la recogida de recursos, F7 llena un poco el almacén.
                RocketBase rocketBase;
                if (GameServices.TryGet(out rocketBase))
                {
                    foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType))) rocketBase.Inventory.Add(t, 10);
                    ShowMessage("Prueba: +10 de cada recurso en el almacén.");
                }
            }
            if (Input.GetKeyDown(KeyCode.F8))
            {
                _health.ApplyImpact(_health.transform.position + Random.insideUnitSphere, 9f, 1f);
                ShowMessage("Prueba: golpe fuerte.");
            }
            if (Input.GetKeyDown(KeyCode.F9))
            {
                _health.RepairAll();
                ShowMessage("Prueba: todo reparado.");
            }
            if (Input.GetKeyDown(KeyCode.F10))
            {
                _environment.StartStorm(0.5f);
                ShowMessage("Prueba: tormenta de polvo.");
            }
#endif
        }

        private void LoadZone(int index)
        {
            if (catalog == null || index >= catalog.zones.Count || catalog.zones[index] == null) return;
            LandingZoneCatalog.SelectedZone = catalog.zones[index];
            Scene scene = SceneManager.GetActiveScene();
            if (scene.buildIndex >= 0) SceneManager.LoadScene(scene.buildIndex);
            else ShowMessage("Añade la escena a Build Settings para cambiar de zona.");
        }

        private void ShowMessage(string text)
        {
            _lastMessage = text;
            _messageTime = Time.time;
        }

        // ------------------------------------------------------------------
        // Panel de depuración
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!showDebugPanel || _health == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
                _style.normal.textColor = Color.white;
            }

            // Cable del HUD dañado: el panel parpadea (misma idea que RobotDamageHUD).
            RobotModule hudCable = _health.GetModule(RobotModuleId.CableHud);
            float glitch = hudCable != null ? 1f - hudCable.Efficiency : 0f;
            if (glitch > 0f && Random.value < glitch * 0.6f) return;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<b>" + _zone.displayName + "</b>  (" + LandingZoneData.DifficultyLabel(_zone.difficulty) + ")");
            sb.AppendLine("Batería: " + Pct(_battery.Charge01) + "   Panel: " + _dusting.CurrentOutputWatts.ToString("0") + " W   Polvo: " + Pct(_dusting.Dust01));
            sb.AppendLine("Movilidad: " + Pct(_health.Mobility) + "   Integridad: " + Pct(_health.StructuralIntegrity01) +
                          "   Velocidad: " + _controller.CurrentSpeed.ToString("0.0") + " m/s");
            sb.AppendLine("Hora del sol: " + _environment.SolTime01.ToString("0.00") + "   " +
                          _environment.TemperatureC.ToString("0") + " °C" + (_environment.IsDustStormActive ? "   <color=orange>TORMENTA</color>" : ""));

            if (_health.IsFunctional(RobotModuleId.CableSensors))
            {
                sb.AppendLine("Chunks: " + _terrain.ActiveChunkCount + " activos, " + _terrain.PooledChunkCount +
                              " en pool, " + _terrain.PendingBuildCount + " pendientes");
            }
            else
            {
                sb.AppendLine("<color=red>Sensores sin señal</color>");
            }
            sb.AppendLine(_health.IsFunctional(RobotModuleId.CableMinimap) ? "Minimapa: OK" : "<color=red>Minimapa: SIN SEÑAL</color>");

            // Datos del mapa real (NASA o simulación del script de Python).
            if (_marsData != null && _marsData.IsLoaded && _health.IsFunctional(RobotModuleId.CableSensors))
            {
                Vector3 pos = _controller.transform.position;
                MarsChunkData chunk = _marsData.GetChunkAt(pos);
                sb.AppendLine("Mapa: " + _marsData.Manifest.sources.elevation + " · elevación " +
                              _marsData.GetAreoidElevation(pos).ToString("0") + " m");
                if (chunk != null && chunk.Metadata != null && chunk.Metadata.minerals != null)
                {
                    MarsMineralConcentrations m = chunk.Metadata.minerals;
                    sb.AppendLine("Minerales: arcillas " + Pct(m.clays) + " · sulfatos " + Pct(m.sulfates) +
                                  " · óxidos Fe " + Pct(m.ironOxides) + " · hielo " + Pct(m.subsurfaceIce));
                    if (chunk.Metadata.environment != null)
                    {
                        sb.AppendLine("Radiación: " + chunk.Metadata.environment.radiationMilliGrayPerDay.ToString("0.00") +
                                      " mGy/día · presión " + chunk.Metadata.environment.pressurePa.ToString("0") + " Pa");
                    }
                }
            }
            else if (_marsData != null && !_marsData.IsLoaded)
            {
                sb.AppendLine("Mapa: procedural (no hay datos en StreamingAssets/MarsMap/" + _zone.zoneId + ")");
            }

            sb.AppendLine();
            for (int i = 0; i < _health.Modules.Count; i++)
            {
                RobotModule m = _health.Modules[i];
                string color = m.State == ModuleState.Operational ? "white" : m.State == ModuleState.Damaged ? "yellow" :
                    m.State == ModuleState.Critical ? "orange" : "red";
                string marker = _repair.SelectedModule == m ? "► " : "   ";
                sb.AppendLine(marker + "<color=" + color + ">" + m.Definition.displayName + ": " + Pct(m.Integrity01) + "</color>");
            }

            sb.AppendLine();
            sb.AppendLine("Herramienta: " + _repair.CurrentTool + (_repair.IsRepairing ? "  <color=lime>reparando…</color>" : ""));
            // Diagnóstico de control: si el robot no se mueve, esta línea dice por qué.
            Vector2 mv = _controller.MoveInput;
            sb.AppendLine("Teclas: (" + mv.x.ToString("0") + ", " + mv.y.ToString("0") + ")   En suelo: " +
                          (_controller.IsGrounded ? "sí" : "<color=orange>no</color>") + "   Ratón: " +
                          (Cursor.lockState == CursorLockMode.Locked ? "capturado" : "libre (clic en la vista)") +
                          "   Cámara: " + (_cameraRig.Mode == RobotCameraMode.FirstPerson ? "1ª persona" : "3ª persona"));
            sb.AppendLine("W adelante · S atrás · A izquierda · D derecha · Q/E girar · ratón mirar · V cámara");
            sb.AppendLine("F usar máquina · R reparar · T herramienta · G módulo · C limpiar paneles");
            sb.AppendLine("F1–F6 zona · F7 +recursos · F8 golpe · F9 reparar todo · F10 tormenta · H ocultar panel · Esc soltar ratón");
            if (Time.time - _messageTime < 4f && _lastMessage.Length > 0) sb.AppendLine("\n<b>" + _lastMessage + "</b>");

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(8, 8, 640, 610), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(16, 12, 628, 606), sb.ToString(), _style);
        }

        private static string Pct(float v)
        {
            return Mathf.RoundToInt(v * 100f) + " %";
        }
    }
}
