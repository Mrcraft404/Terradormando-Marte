using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarsRobot.Core;
using UnityEngine;

namespace MarsRobot.World.TerrainSystem
{
    /// <summary>
    /// Gestor del terreno: mapa estático dividido en una cuadrícula de chunks que se cargan y
    /// descargan alrededor del robot.
    ///
    /// Puntos clave de diseño (pensados para máquinas con poca RAM):
    ///  • Radio de carga y radio de descarga distintos (histéresis): un chunk se carga al entrar
    ///    en <see cref="loadRadiusInChunks"/> y solo se descarga al salir de
    ///    <see cref="unloadRadiusInChunks"/>. Así no hay parpadeo de carga/descarga en el borde.
    ///  • LOD por distancia, también con histéresis, para no reconstruir mallas sin parar.
    ///  • Pool de chunks: los GameObjects y sus Mesh se reutilizan; al volver al pool la malla se
    ///    vacía para liberar memoria. El pool tiene tope (<see cref="maxPooledChunks"/>).
    ///  • Construcción de mallas escalonada: solo se lanzan N trabajos por frame y se suben N
    ///    mallas por frame; el cálculo de vértices va en hilos de fondo (Task) si está activado.
    ///  • Colisionadores solo en los chunks cercanos al robot (cocinar física es caro).
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public sealed class TerrainManager : MonoBehaviour
    {
        /// <summary>Un nivel de detalle: hasta qué distancia se usa y cuántos cuadrados por lado tiene.</summary>
        [Serializable]
        public struct LodLevel
        {
            [Tooltip("Distancia máxima (en chunks, desde el robot hasta el borde del chunk) para este LOD.")]
            public float maxDistanceInChunks;

            [Tooltip("Cuadrados por lado de la malla. Potencias de 2 recomendadas: 64, 32, 16, 8.")]
            [Range(2, 128)] public int resolution;

            public LodLevel(float maxDistance, int res)
            {
                maxDistanceInChunks = maxDistance;
                resolution = res;
            }
        }

        // ------------------------------------------------------------------
        // Configuración
        // ------------------------------------------------------------------

        [Header("Mapa")]
        [SerializeField] private TerrainMapSettings mapSettings = new TerrainMapSettings();
        [SerializeField] private Material terrainMaterial;
        [Tooltip("Objeto que se sigue (el robot). Se puede asignar por código con SetTarget.")]
        [SerializeField] private Transform target;
        [Tooltip("Si está activo, el mapa se crea en Start con 'mapSettings'. Desactívalo si lo inicializa el LandingZoneSpawner.")]
        [SerializeField] private bool initializeOnStart = false;

        [Header("Streaming")]
        [SerializeField, Min(1)] private int loadRadiusInChunks = 6;
        [Tooltip("Debe ser mayor que el radio de carga (histéresis).")]
        [SerializeField, Min(2)] private int unloadRadiusInChunks = 8;
        [Tooltip("Distancia (en chunks) dentro de la cual los chunks tienen colisionador.")]
        [SerializeField, Min(0.5f)] private float colliderDistanceInChunks = 1.5f;
        [Tooltip("Cada cuánto (s) se recalcula el conjunto de chunks visibles.")]
        [SerializeField, Min(0.02f)] private float refreshInterval = 0.2f;

        [Header("LOD")]
        [SerializeField] private LodLevel[] lodLevels =
        {
            new LodLevel(1.5f, 64),
            new LodLevel(3f, 32),
            new LodLevel(5f, 16),
            new LodLevel(999f, 8),
        };
        [Tooltip("Margen extra (en chunks) antes de bajar a un LOD más simple.")]
        [SerializeField, Min(0f)] private float lodHysteresisInChunks = 0.5f;

        [Header("Rendimiento y memoria")]
        [Tooltip("Calcular las mallas en hilos de fondo. Desactívalo en WebGL (no hay hilos).")]
        [SerializeField] private bool useBackgroundThreads = true;
        [SerializeField, Min(1)] private int maxConcurrentJobs = 2;
        [SerializeField, Min(1)] private int maxBuildStartsPerFrame = 2;
        [SerializeField, Min(1)] private int maxMeshAppliesPerFrame = 2;
        [Tooltip("Chunks que se crean por adelantado al iniciar (evita tirones al principio).")]
        [SerializeField, Min(0)] private int poolPrewarmCount = 16;
        [Tooltip("Máximo de chunks inactivos guardados en el pool; el resto se destruye.")]
        [SerializeField, Min(0)] private int maxPooledChunks = 24;
        [Tooltip("Profundidad de la falda que tapa grietas entre LODs (m).")]
        [SerializeField, Min(0f)] private float skirtDepth = 6f;
        [Tooltip("Metros que ocupa una repetición de la textura del suelo.")]
        [SerializeField, Min(0.1f)] private float uvTileSize = 16f;

        // ------------------------------------------------------------------
        // Eventos públicos
        // ------------------------------------------------------------------

        /// <summary>Se lanza cuando el mapa está creado y se puede consultar la altura.</summary>
        public event Action MapReady;

        /// <summary>Un chunk ha recibido su primera malla.</summary>
        public event Action<Vector2Int> ChunkLoaded;

        /// <summary>Un chunk ha vuelto al pool.</summary>
        public event Action<Vector2Int> ChunkUnloaded;

        // ------------------------------------------------------------------
        // Estado interno
        // ------------------------------------------------------------------

        private readonly Dictionary<Vector2Int, TerrainChunk> _active = new Dictionary<Vector2Int, TerrainChunk>();
        private readonly Stack<TerrainChunk> _pool = new Stack<TerrainChunk>();
        private readonly List<TerrainChunk> _buildQueue = new List<TerrainChunk>();
        private readonly ConcurrentQueue<ChunkMeshData> _completed = new ConcurrentQueue<ChunkMeshData>();
        private readonly List<Vector2Int> _toRemove = new List<Vector2Int>();
        private readonly Comparison<TerrainChunk> _byDistance = (a, b) => a.LastDistance.CompareTo(b.LastDistance);

        private TerrainMapSettings _settings;
        private int[][] _trianglesPerLod;
        private ChunkMeshDataPool _meshDataPool;
        private CancellationTokenSource _cts;
        private int _runningJobs;
        private float _refreshTimer;
        private int _chunkCounter;

        /// <summary>Campo de alturas del mapa actual (null hasta inicializar).</summary>
        public HeightField HeightField { get; private set; }

        /// <summary>True cuando el mapa existe y se puede consultar.</summary>
        public bool IsMapReady
        {
            get { return HeightField != null; }
        }

        /// <summary>Ajustes del mapa cargado.</summary>
        /// <summary>Material compartido por todos los chunks (cambiarlo afecta a todo el suelo).</summary>
        public Material TerrainMaterial
        {
            get { return terrainMaterial; }
        }

        /// <summary>Objeto que se sigue (el robot), o null.</summary>
        public Transform Target
        {
            get { return target; }
        }

        /// <summary>Metros que ocupa una repetición de la UV0 del suelo (uv0 = posición / UvTileSize).</summary>
        public float UvTileSize
        {
            get { return uvTileSize; }
        }

        public TerrainMapSettings Settings
        {
            get { return _settings; }
        }

        public int ActiveChunkCount
        {
            get { return _active.Count; }
        }

        public int PooledChunkCount
        {
            get { return _pool.Count; }
        }

        public int PendingBuildCount
        {
            get { return _buildQueue.Count + _runningJobs; }
        }

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            GameServices.Register(this);
#if UNITY_WEBGL && !UNITY_EDITOR
            useBackgroundThreads = false; // WebGL no admite hilos
#endif
        }

        private void Start()
        {
            if (initializeOnStart) Initialize(mapSettings);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
            CancelJobs();
        }

        private void OnValidate()
        {
            if (unloadRadiusInChunks <= loadRadiusInChunks) unloadRadiusInChunks = loadRadiusInChunks + 1;
            if (lodLevels == null || lodLevels.Length == 0) lodLevels = new[] { new LodLevel(999f, 16) };
        }

        /// <summary>Material del suelo. Llamar antes de Initialize (los chunks se crean al inicializar).</summary>
        public void SetMaterial(Material material)
        {
            terrainMaterial = material;
        }

        /// <summary>Cambia el objeto seguido (normalmente el robot) y fuerza un refresco.</summary>
        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            _refreshTimer = 0f;
        }

        /// <summary>
        /// Carga un mapa. Si ya había uno, devuelve todos los chunks al pool primero.
        /// Crear el campo de alturas es la parte cara (se hace una vez, idealmente en la pantalla de carga).
        /// </summary>
        public void Initialize(TerrainMapSettings settings)
        {
            Initialize(settings, null);
        }

        /// <summary>
        /// Igual que <see cref="Initialize(TerrainMapSettings)"/>, pero con un campo de alturas ya
        /// construido (por ejemplo, el de los datos reales de Marte que carga MarsTerrainGenerator).
        /// Si <paramref name="prebuiltField"/> es null, se genera a partir de los ajustes.
        /// El tamaño del mapa (mapSizeInChunks × chunkWorldSize) debe coincidir con field.WorldSize.
        /// </summary>
        public void Initialize(TerrainMapSettings settings, HeightField prebuiltField)
        {
            if (settings == null)
            {
                Debug.LogError("[TerrainManager] Ajustes de mapa nulos.");
                return;
            }

            CancelJobs();
            ReleaseAllChunks();

            _settings = settings;
            HeightField = prebuiltField != null ? prebuiltField : HeightField.Create(settings);

            // Triángulos compartidos y pool de datos por LOD.
            _trianglesPerLod = new int[lodLevels.Length][];
            var resolutions = new int[lodLevels.Length];
            for (int i = 0; i < lodLevels.Length; i++)
            {
                resolutions[i] = Mathf.Max(2, lodLevels[i].resolution);
                _trianglesPerLod[i] = ChunkMeshBuilder.BuildTriangles(resolutions[i]);
            }
            _meshDataPool = new ChunkMeshDataPool(resolutions);
            _cts = new CancellationTokenSource();

            Prewarm(poolPrewarmCount);
            _refreshTimer = 0f;

            Debug.Log("[TerrainManager] Mapa listo: " + settings.mapSizeInChunks + "×" + settings.mapSizeInChunks +
                      " chunks, campo de alturas " + HeightField.Resolution + "² (" +
                      (HeightField.MemoryBytes / (1024 * 1024)) + " MB).");

            if (MapReady != null) MapReady();
        }

        private void Update()
        {
            if (!IsMapReady) return;

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f && target != null)
            {
                _refreshTimer = refreshInterval;
                RefreshChunks(target.position);
            }

            StartPendingBuilds();
            ApplyCompletedMeshes();
        }

        // ------------------------------------------------------------------
        // Consultas públicas (otros sistemas: robot, spawns, rocas…)
        // ------------------------------------------------------------------

        /// <summary>Altura del terreno en una posición del mundo (independiente de si el chunk está cargado).</summary>
        public float GetHeightAt(Vector3 worldPosition)
        {
            if (!IsMapReady) return 0f;
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return HeightField.SampleHeight(local.x, local.z) + transform.position.y;
        }

        /// <summary>Normal del terreno en una posición del mundo.</summary>
        public Vector3 GetNormalAt(Vector3 worldPosition)
        {
            if (!IsMapReady) return Vector3.up;
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            return HeightField.SampleNormal(local.x, local.z, 1f);
        }

        /// <summary>Pendiente en grados (0 = llano).</summary>
        public float GetSlopeDegrees(Vector3 worldPosition)
        {
            return Vector3.Angle(GetNormalAt(worldPosition), Vector3.up);
        }

        /// <summary>Coordenada de chunk que contiene una posición del mundo.</summary>
        public Vector2Int WorldToChunk(Vector3 worldPosition)
        {
            Vector3 local = transform.InverseTransformPoint(worldPosition);
            float size = _settings != null ? _settings.chunkWorldSize : 64f;
            return new Vector2Int(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.z / size));
        }

        /// <summary>True si el chunk bajo esa posición tiene malla y colisionador (el robot ya puede apoyarse).</summary>
        public bool IsColliderReadyAt(Vector3 worldPosition)
        {
            TerrainChunk chunk;
            return _active.TryGetValue(WorldToChunk(worldPosition), out chunk) && chunk.HasCollider;
        }

        /// <summary>Fuerza un refresco inmediato (por ejemplo tras teletransportar al robot).</summary>
        public void ForceRefresh()
        {
            _refreshTimer = 0f;
        }

        // ------------------------------------------------------------------
        // Selección de chunks (carga, descarga, LOD)
        // ------------------------------------------------------------------

        private void RefreshChunks(Vector3 targetWorldPos)
        {
            Vector3 local = transform.InverseTransformPoint(targetWorldPos);
            float size = _settings.chunkWorldSize;
            int mapSize = _settings.mapSizeInChunks;
            Vector2Int center = new Vector2Int(Mathf.FloorToInt(local.x / size), Mathf.FloorToInt(local.z / size));

            // 1) Descarga: fuera del radio de descarga (mayor que el de carga = histéresis).
            _toRemove.Clear();
            foreach (var pair in _active)
            {
                float dist = DistanceToChunk(local, pair.Key, size);
                pair.Value.LastDistance = dist;
                if (dist > unloadRadiusInChunks) _toRemove.Add(pair.Key);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                ReleaseChunk(_toRemove[i]);
            }

            // 2) Carga: chunks dentro del radio de carga que aún no estén activos.
            int r = loadRadiusInChunks;
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var coord = new Vector2Int(center.x + dx, center.y + dz);
                    if (coord.x < 0 || coord.y < 0 || coord.x >= mapSize || coord.y >= mapSize) continue;
                    if (_active.ContainsKey(coord)) continue;

                    float dist = DistanceToChunk(local, coord, size);
                    if (dist > loadRadiusInChunks) continue;

                    TerrainChunk chunk = AcquireChunk();
                    chunk.Activate(coord, size);
                    chunk.LastDistance = dist;
                    _active.Add(coord, chunk);
                }
            }

            // 3) LOD y colisionadores de todos los activos.
            foreach (var pair in _active)
            {
                TerrainChunk chunk = pair.Value;
                float dist = DistanceToChunk(local, pair.Key, size);
                chunk.LastDistance = dist;

                int desired = ComputeLod(dist, chunk.TargetLod);
                if (desired != chunk.TargetLod)
                {
                    chunk.TargetLod = desired;
                    if (desired != chunk.AppliedLod) EnqueueBuild(chunk, desired);
                    else chunk.QueuedLod = -1; // volvió al LOD que ya tenía: cancelar lo pendiente
                }

                chunk.SetColliderWanted(dist <= colliderDistanceInChunks);
            }

            // Lo más cercano primero.
            _buildQueue.Sort(_byDistance);
        }

        /// <summary>
        /// Distancia (en chunks) desde el punto hasta el rectángulo del chunk. Vale 0 si el robot está
        /// dentro. Usar el borde (y no el centro) evita que el chunk vecino tenga poco detalle
        /// cuando el robot está pegado a él.
        /// </summary>
        private static float DistanceToChunk(Vector3 localPos, Vector2Int coord, float size)
        {
            float minX = coord.x * size, maxX = minX + size;
            float minZ = coord.y * size, maxZ = minZ + size;
            float dx = localPos.x < minX ? minX - localPos.x : (localPos.x > maxX ? localPos.x - maxX : 0f);
            float dz = localPos.z < minZ ? minZ - localPos.z : (localPos.z > maxZ ? localPos.z - maxZ : 0f);
            return Mathf.Sqrt(dx * dx + dz * dz) / size;
        }

        /// <summary>LOD por distancia con histéresis: bajar de detalle exige pasar el umbral + margen.</summary>
        private int ComputeLod(float dist, int currentLod)
        {
            int raw = lodLevels.Length - 1;
            for (int i = 0; i < lodLevels.Length; i++)
            {
                if (dist <= lodLevels[i].maxDistanceInChunks)
                {
                    raw = i;
                    break;
                }
            }

            if (currentLod >= 0 && currentLod < lodLevels.Length && raw > currentLod)
            {
                if (dist <= lodLevels[currentLod].maxDistanceInChunks + lodHysteresisInChunks)
                {
                    return currentLod;
                }
            }
            return raw;
        }

        // ------------------------------------------------------------------
        // Cola de construcción escalonada
        // ------------------------------------------------------------------

        private void EnqueueBuild(TerrainChunk chunk, int lod)
        {
            chunk.QueuedLod = lod;
            if (!chunk.InQueue)
            {
                chunk.InQueue = true;
                _buildQueue.Add(chunk);
            }
        }

        private void StartPendingBuilds()
        {
            int started = 0;
            while (_buildQueue.Count > 0 && started < maxBuildStartsPerFrame)
            {
                if (useBackgroundThreads && Volatile.Read(ref _runningJobs) >= maxConcurrentJobs) break;

                TerrainChunk chunk = _buildQueue[0];
                _buildQueue.RemoveAt(0);
                chunk.InQueue = false;

                // El chunk pudo descargarse o su petición cancelarse mientras esperaba.
                if (!chunk.IsActive || chunk.QueuedLod < 0) continue;

                int lod = chunk.QueuedLod;
                chunk.QueuedLod = -1;
                chunk.BuildVersion++;

                ChunkMeshData data = _meshDataPool.Rent(lod);
                data.Chunk = chunk;
                data.Version = chunk.BuildVersion;

                Vector2Int coord = chunk.Coord;
                HeightField field = HeightField;
                float size = _settings.chunkWorldSize;
                float skirt = skirtDepth;
                float tile = uvTileSize;

                if (useBackgroundThreads)
                {
                    Interlocked.Increment(ref _runningJobs);
                    CancellationToken token = _cts.Token;
                    // No pasamos el token a Task.Run: si se cancelara antes de empezar, el 'finally'
                    // no se ejecutaría y el contador de trabajos quedaría descuadrado.
                    Task.Run(() =>
                    {
                        try
                        {
                            if (token.IsCancellationRequested) return;
                            ChunkMeshBuilder.Build(field, coord, size, skirt, tile, data);
                            if (!token.IsCancellationRequested) _completed.Enqueue(data);
                        }
                        catch (Exception e)
                        {
                            Debug.LogException(e); // Debug.Log es seguro desde otros hilos
                        }
                        finally
                        {
                            Interlocked.Decrement(ref _runningJobs);
                        }
                    });
                }
                else
                {
                    ChunkMeshBuilder.Build(field, coord, size, skirt, tile, data);
                    _completed.Enqueue(data);
                }
                started++;
            }
        }

        private void ApplyCompletedMeshes()
        {
            int applied = 0;
            ChunkMeshData data;
            float maxHeight = _settings.heightScale;
            while (applied < maxMeshAppliesPerFrame && _completed.TryDequeue(out data))
            {
                TerrainChunk chunk = data.Chunk;
                bool valid = chunk != null && chunk.IsActive && chunk.BuildVersion == data.Version;
                if (valid)
                {
                    bool firstMesh = chunk.AppliedLod < 0;
                    chunk.ApplyMesh(data, _trianglesPerLod[data.Lod], _settings.chunkWorldSize, maxHeight);
                    applied++;
                    if (firstMesh && ChunkLoaded != null) ChunkLoaded(chunk.Coord);
                }
                _meshDataPool.Return(data);
            }
        }

        private void CancelJobs()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }
            ChunkMeshData dropped;
            while (_completed.TryDequeue(out dropped)) { }
            for (int i = 0; i < _buildQueue.Count; i++)
            {
                _buildQueue[i].InQueue = false;
                _buildQueue[i].QueuedLod = -1;
            }
            _buildQueue.Clear();
        }

        // ------------------------------------------------------------------
        // Pool de chunks
        // ------------------------------------------------------------------

        private void Prewarm(int count)
        {
            int toCreate = Mathf.Min(count, maxPooledChunks) - _pool.Count;
            for (int i = 0; i < toCreate; i++)
            {
                TerrainChunk chunk = CreateChunk();
                chunk.gameObject.SetActive(false);
                _pool.Push(chunk);
            }
        }

        private TerrainChunk AcquireChunk()
        {
            return _pool.Count > 0 ? _pool.Pop() : CreateChunk();
        }

        private TerrainChunk CreateChunk()
        {
            var go = new GameObject("Chunk_" + (_chunkCounter++));
            go.transform.SetParent(transform, false);
            var chunk = go.AddComponent<TerrainChunk>();
            chunk.Setup(terrainMaterial, gameObject.layer);
            return chunk;
        }

        private void ReleaseChunk(Vector2Int coord)
        {
            TerrainChunk chunk;
            if (!_active.TryGetValue(coord, out chunk)) return;
            _active.Remove(coord);
            chunk.Deactivate();

            if (_pool.Count < maxPooledChunks) _pool.Push(chunk);
            else Destroy(chunk.gameObject); // por encima del tope: liberar del todo

            if (ChunkUnloaded != null) ChunkUnloaded(coord);
        }

        private void ReleaseAllChunks()
        {
            _toRemove.Clear();
            foreach (var pair in _active) _toRemove.Add(pair.Key);
            for (int i = 0; i < _toRemove.Count; i++) ReleaseChunk(_toRemove[i]);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_settings == null) return;
            float size = _settings.chunkWorldSize;
            foreach (var pair in _active)
            {
                int lod = pair.Value.AppliedLod;
                Gizmos.color = lod <= 0 ? Color.green : (lod == 1 ? Color.yellow : (lod == 2 ? new Color(1f, 0.5f, 0f) : Color.red));
                Vector3 c = transform.TransformPoint(new Vector3((pair.Key.x + 0.5f) * size, 0f, (pair.Key.y + 0.5f) * size));
                Gizmos.DrawWireCube(c, new Vector3(size, 1f, size));
            }
        }
#endif
    }
}
