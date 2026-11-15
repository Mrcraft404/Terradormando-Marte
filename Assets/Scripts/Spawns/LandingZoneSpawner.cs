using System.Collections;
using MarsRobot.Base;
using MarsRobot.Energy;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using MarsRobot.World.Weather;
using UnityEngine;

namespace MarsRobot.Spawns
{
    /// <summary>
    /// Arranque de la escena de juego: carga el mapa de la zona elegida, conecta el clima con el
    /// sistema de polvo, aplica la dificultad al robot y lo deja en el suelo cuando el chunk bajo
    /// sus ruedas ya tiene colisionador (así no cae a través del terreno).
    /// </summary>
    public sealed class LandingZoneSpawner : MonoBehaviour
    {
        [Tooltip("Zona usada si no se eligió ninguna en el menú (LandingZoneCatalog.SelectedZone).")]
        [SerializeField] private LandingZoneData fallbackZone;

        [Header("Referencias de escena")]
        [SerializeField] private TerrainManager terrain;
        [SerializeField] private MarsEnvironmentSystem environment;
        [SerializeField] private Rigidbody robotBody;
        [SerializeField] private RobotHealthSystem robotHealth;
        [SerializeField] private RobotController robotController;
        [SerializeField] private DustingSystem dusting;

        [Tooltip("Segundos máximos esperando al terreno antes de soltar el robot igualmente.")]
        [SerializeField, Min(1f)] private float maxWaitSeconds = 15f;

        [Tooltip("Opcional: carga el mapa real de Marte generado por Tools/MapProcessor si existe para la zona.")]
        [SerializeField] private MarsTerrainGenerator terrainGenerator;

        [Tooltip("Crear la base-cohete con sus máquinas en el punto de aterrizaje.")]
        [SerializeField] private bool buildRocketBase = true;

        /// <summary>Base creada al aterrizar (null si buildRocketBase está desactivado).</summary>
        public RocketBase RocketBase { get; private set; }

        /// <summary>Zona activa en esta partida.</summary>
        public LandingZoneData ActiveZone { get; private set; }

        /// <summary>True cuando el robot ya está en el suelo y se puede jugar.</summary>
        public bool IsReady { get; private set; }

        /// <summary>Asigna las referencias por código (lo usa TestSceneBootstrap). Llamar antes de Start.</summary>
        public void Configure(LandingZoneData zone, TerrainManager terrainManager, MarsEnvironmentSystem environmentSystem,
            Rigidbody body, RobotHealthSystem health, RobotController controller, DustingSystem dustingSystem)
        {
            fallbackZone = zone;
            terrain = terrainManager;
            environment = environmentSystem;
            robotBody = body;
            robotHealth = health;
            robotController = controller;
            dusting = dustingSystem;
        }

        /// <summary>Generador de terreno con datos reales (opcional). Llamar antes de Start.</summary>
        public void SetTerrainGenerator(MarsTerrainGenerator generator)
        {
            terrainGenerator = generator;
        }

        private IEnumerator Start()
        {
            ActiveZone = LandingZoneCatalog.SelectedZone != null ? LandingZoneCatalog.SelectedZone : fallbackZone;
            if (ActiveZone == null)
            {
                Debug.LogError("[LandingZoneSpawner] No hay zona de aterrizaje asignada.", this);
                yield break;
            }
            if (terrain == null || robotBody == null)
            {
                Debug.LogError("[LandingZoneSpawner] Faltan referencias (TerrainManager o Rigidbody del robot).", this);
                yield break;
            }

            // 1) Congelar el robot mientras se prepara todo.
            if (robotController != null) robotController.InputEnabled = false;
            robotBody.isKinematic = true;

            // 2) Clima y energía.
            if (environment != null)
            {
                environment.Configure(ActiveZone);
                if (dusting != null) dusting.SetEnvironment(environment);
            }

            // 3) Dificultad aplicada al robot.
            if (robotHealth != null) robotHealth.SetZoneDamageMultiplier(ActiveZone.damageMultiplier);

            // 4) Terreno: datos reales de Marte si existen (StreamingAssets/MarsMap/<zona>), si no procedural.
            terrain.SetTarget(robotBody.transform);
            bool realData = false;
            if (terrainGenerator != null && terrainGenerator.HasDataFor(ActiveZone.zoneId))
            {
                yield return terrainGenerator.LoadZone(ActiveZone.zoneId, ActiveZone.terrain, terrain);
                realData = terrainGenerator.LastLoadSucceeded;
            }
            if (!realData) terrain.Initialize(ActiveZone.terrain);

            Vector3 spawn = terrain.transform.TransformPoint(ActiveZone.GetSpawnLocalXZ(terrain.Settings.WorldSize));
            spawn.y = terrain.GetHeightAt(spawn);
            Quaternion spawnRotation = robotBody.rotation;

            // 4b) Base inicial: el cohete aterrizado en el punto de aterrizaje, con sus máquinas.
            //     El robot aparece sobre la plataforma de carga, mirando al cohete.
            if (buildRocketBase)
            {
                // Suelo nivelado bajo el cohete (patas, ascensor y entorno de la base).
                spawn.y = terrain.FlattenAround(spawn, 22f, 35f); // transición larga: sin paredes verticales en zonas escarpadas
                RocketBase = RocketBase.Create(terrain, spawn, Vector3.forward);
                RocketBase.Zone = ActiveZone; // para saber cuánto hielo hay bajo la base
                // El robot aparece acoplado en el hangar de la nave (en alto): no se baja al terreno.
                spawn = RocketBase.RobotSpawnPosition;
                spawnRotation = RocketBase.RobotSpawnRotation;
            }

            spawn.y += ActiveZone.spawnHeightOffset;
            robotBody.transform.SetPositionAndRotation(spawn, spawnRotation);
            terrain.ForceRefresh();

            // 5) Esperar a que exista el colisionador bajo el robot (carga escalonada).
            float waited = 0f;
            while (!terrain.IsColliderReadyAt(spawn) && waited < maxWaitSeconds)
            {
                waited += Time.deltaTime;
                yield return null;
            }
            if (waited >= maxWaitSeconds)
            {
                Debug.LogWarning("[LandingZoneSpawner] El terreno tardó demasiado; se suelta el robot igualmente.", this);
            }

            // 6) Soltar el robot.
            robotBody.isKinematic = false;
            if (robotController != null) robotController.InputEnabled = true;
            IsReady = true;

            Debug.Log("[LandingZoneSpawner] Aterrizaje en " + ActiveZone.displayName + " (dificultad " +
                      LandingZoneData.DifficultyLabel(ActiveZone.difficulty) + ").");
        }
    }
}
