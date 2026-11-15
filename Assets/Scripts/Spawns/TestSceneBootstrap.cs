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
            MarsRobot.Core.GameSettings.ReapplySceneSettings(); // brillo y sensibilidad guardados en Ajustes
            while (_spawner != null && !_spawner.IsReady) yield return null;
            if (_spawner == null || _spawner.RocketBase == null) yield break;

            // El jugador empieza como el astronauta dentro de la nave; el robot espera en el hangar.
            MarsRobot.Crew.CrewControl.Setup(_spawner.RocketBase, _controller.gameObject);

            Transform baseRoot = _spawner.RocketBase.transform;
            // MarteCar aparcado junto al pie del ascensor (a la derecha según se sale de la nave).
            Vector3 parking = baseRoot.TransformPoint(new Vector3(8.5f, 0f, 17f));
            parking.y = _terrain.GetHeightAt(parking);
            MarsRobot.Vehicles.MarteCarFactory.Create(parking, baseRoot.rotation).gameObject
                .AddComponent<MarsRobot.UI.Minimap.MinimapObjectMarker>().Setup(MarsRobot.UI.Minimap.MinimapMarkerType.Vehicle, false, "MarteCar");

            // Rocas minables alrededor del rover.
            var rocks = new GameObject("Rocas").AddComponent<RockScatterSpawner>();
            rocks.Setup(_terrain, _controller.transform, _spawner.ActiveZone);

            // Puntos de descubrimiento: restos de sondas antiguas (ficticias) a 70–120 m de la base.
            var rng = new System.Random(_zone.terrain.seed);
            for (int i = 0; i < 2; i++)
            {
                float ang = (float)rng.NextDouble() * 360f;
                float dist = 70f + (float)rng.NextDouble() * 50f;
                Vector3 p = baseRoot.position + Quaternion.Euler(0f, ang, 0f) * Vector3.forward * dist;
                p.y = _terrain.GetHeightAt(p);
                MarsRobot.Dialogue.DiscoveryPoint.Spawn(p, "probe_archive", i == 0 ? "Restos de una sonda antigua" : "Módulo de aterrizaje abandonado");
            }

            // Aspecto: atrezo con colocación orgánica, cielo marciano, sondas de reflejos y una
            // jerarquía ordenada (_Environment, _Structures, _Props, _PhysicsSystem).
            MarsRobot.Visuals.ShipSetDresser.Dress(_spawner.RocketBase, _zone.terrain.seed);
            MarsRobot.Visuals.MarsEnvironment.Setup(_spawner.RocketBase.Mothership);
            MarsRobot.Visuals.SceneRoots.OrganizeGameScene();

            // Bienvenida de la IA del cohete (pausa el juego hasta elegir respuesta).
            yield return new WaitForSeconds(1.5f);
            MarsRobot.Dialogue.DialogueManager.Play("intro_nav");

            // Mensaje de la comandante cuando la base lleve un rato funcionando.
            yield return new WaitForSeconds(480f);
            MarsRobot.Dialogue.DialogueManager.Play("commander_hello");
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

            // Clima espacial (NASA DONKI, con simulación sin conexión) y su HUD.
            var spaceWeather = new GameObject("Clima espacial");
            spaceWeather.AddComponent<MarsRobot.World.SpaceWeather.SpaceWeatherManager>();
            spaceWeather.AddComponent<MarsRobot.World.SpaceWeather.SpaceWeatherHUD>();

            // Minimapa táctico (arriba a la izquierda).
            new GameObject("Minimapa").AddComponent<MarsRobot.UI.Minimap.MarsMinimapController>();

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
            body.mass = 900f; // RobotModuleSystem la ajusta con el chasis y los módulos

            // Rover a escala de un Curiosity/Perseverance (~3 × 2,7 × 2,2 m): ver RoverConfig.
            var capsule = robot.AddComponent<CapsuleCollider>();
            capsule.radius = 1.0f;
            capsule.height = 2.0f;
            capsule.center = new Vector3(0f, 1.0f, 0f);
            capsule.material = CreateSlipperyMaterial();

            // Orden importante: cada Awake busca a los anteriores con GetComponent.
            _health = robot.AddComponent<RobotHealthSystem>();
            _battery = robot.AddComponent<BatterySystem>();
            _dusting = robot.AddComponent<DustingSystem>();
            _controller = robot.AddComponent<RobotController>();
            _repair = robot.AddComponent<RobotRepairController>();
            robot.AddComponent<RobotUpgrades>();        // mejoras instaladas en el taller
            robot.AddComponent<MarsRobot.Items.RobotInventory>(); // bahía de carga con masa y volumen (I)
            robot.AddComponent<RobotModuleSystem>();    // ranuras: brazo, cubierta, bahía interna
            robot.AddComponent<RoverToolController>();  // X: taladro / pala / soplador
            robot.AddComponent<RobotToolbelt>();        // L foco · Z escáner
            robot.AddComponent<BuildModeController>();  // B: construir (antes que RobotBaseLink, que lo busca en Awake)
            robot.AddComponent<MarsBuildingPlacer>();   // N: hábitats por capas con holograma (después de BuildModeController)
            robot.AddComponent<RobotBaseLink>();        // F: máquinas y estructuras de la base (por cercanía)
            robot.AddComponent<MarsRobot.Interaction.PlayerInteractor>(); // F: lo que apunta el punto de mira
            robot.AddComponent<MarsRobot.Interaction.InteractionUI>();    // punto de mira y texto de acción
            robot.AddComponent<MarsRobot.Items.InventoryUI>();            // ventana del inventario
            robot.AddComponent<MarsRobot.Dialogue.LogTerminal>();          // J: bitácora
            robot.AddComponent<MarsRobot.Dialogue.RobotAlertMonitor>();    // avisos del HUD
            robot.AddComponent<MarsRobot.CameraEffects.RobotCameraMotion>(); // balanceo e inercia
            robot.AddComponent<MarsRobot.CameraEffects.CameraShake>();       // sacudidas
            robot.AddComponent<MarsRobot.World.SpaceWeather.SpaceWeatherMotorGlitch>(); // fallos de motor (GST)

            // Modelo 3D del rover según el diseño del usuario (seis ruedas rocker-bogie, mástil con
            // cámaras, brazo robótico, RTG y antenas). Ver Robot/Visual/RoverModelBuilder.
            MarsRobot.Robot.Visual.RoverVisualRig rig = MarsRobot.Robot.Visual.RoverModelBuilder.Build(robot.transform);
            robot.AddComponent<MarsRobot.Robot.Visual.RoverVisualAnimator>().Setup(rig);

            // Pivote de la cámara a la altura de las cámaras del mástil (~2,15 m). El controlador lo
            // inclina con el ratón y el animador copia ese cabeceo en la cabeza del mástil.
            var head = new GameObject("Cabeza");
            head.transform.SetParent(robot.transform, false);
            head.transform.position = rig.cameraMount.position;
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
            camGo.AddComponent<MarsRobot.CameraEffects.RobotDamageVisionEffect>(); // visión dañada (cables / erupciones)

            // En primera persona se oculta la cabeza del mástil (la cámara está dentro de ella).
            _cameraRig = robot.AddComponent<RobotCameraRig>();
            _cameraRig.Setup(head.transform, camGo.transform, rig.headRenderers);

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
            if (Input.GetKeyDown(KeyCode.F11))
            {
                MarsRobot.World.SpaceWeather.SpaceWeatherManager sw;
                if (GameServices.TryGet(out sw))
                {
                    sw.TriggerNextNow();
                    ShowMessage("Prueba: siguiente evento de clima espacial.");
                }
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
            // Con el astronauta al mando no se muestra el panel del robot.
            if (MarsRobot.Crew.CrewControl.Instance != null && MarsRobot.Crew.CrewControl.Instance.AstronautActive) return;
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
            sb.AppendLine("F1–F6 zona · F7 +recursos · F8 golpe · F9 reparar todo · F10 tormenta · F11 clima espacial · H ocultar panel");
            if (Time.time - _messageTime < 4f && _lastMessage.Length > 0) sb.AppendLine("\n<b>" + _lastMessage + "</b>");

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            // Debajo del minimapa (que ocupa la esquina superior izquierda).
            const float top = 294f;
            float h = Mathf.Min(610f, Screen.height - top - 8f);
            GUI.DrawTexture(new Rect(8, top, 640, h), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(16, top + 4, 628, h - 4), sb.ToString(), _style);
        }

        private static string Pct(float v)
        {
            return Mathf.RoundToInt(v * 100f) + " %";
        }
    }
}
