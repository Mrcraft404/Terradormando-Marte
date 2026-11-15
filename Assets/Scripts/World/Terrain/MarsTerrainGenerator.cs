using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using MarsRobot.Core;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.World.TerrainSystem
{
    /// <summary>
    /// Lee el mapa de Marte preprocesado por <c>Tools/MapProcessor/generate_mars_chunks.py</c>
    /// (carpeta <c>StreamingAssets/MarsMap/&lt;zona&gt;/</c>) y lo conecta con el <see cref="TerrainManager"/>.
    ///
    /// Qué hace:
    ///  1. Lee en un hilo de fondo el manifest, las alturas (.heightmap, uint16), el micro-relieve
    ///     HiRISE (.micro, int8), la máscara de rocas (.rocks, uint8) y los JSON de minerales.
    ///  2. Une todos los chunks de datos en un único <see cref="HeightField"/> a escala REAL
    ///     (metros de MOLA; la altura 0 del mundo = la cota más baja de la zona).
    ///  3. Cose las texturas CTX en una sola imagen orbital (comprimida a DXT1, ~2 MB).
    ///  4. Arranca el TerrainManager con ese campo. El LOD lo sigue haciendo el TerrainManager:
    ///     cerca del robot, malla de 1 m (se ve el micro-relieve HiRISE); en anillos medios,
    ///     mallas más gruesas (CTX + MOLA); en el horizonte, solo la forma MOLA.
    ///  5. Alrededor del robot coloca rocas afiladas (según la máscara HiRISE) y vetas de
    ///     minerales (según CRISM), con pool y en celdas, para no gastar memoria lejos del robot.
    ///  6. Aplica la gravedad de Marte (0,38 g).
    ///
    /// Si no hay datos o algo falla, <see cref="LastLoadSucceeded"/> queda en false y el
    /// LandingZoneSpawner usa el terreno procedural de siempre.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsTerrainGenerator : MonoBehaviour
    {
        /// <summary>Gravedad superficial media de Marte (m/s²) = 0,38 g.</summary>
        public const float MarsGravity = 3.721f;

        [Header("Datos")]
        [Tooltip("Subcarpeta de StreamingAssets con una carpeta por zona.")]
        [SerializeField] private string mapFolder = "MarsMap";
        [Tooltip("Tamaño objetivo (m) de los chunks de render del TerrainManager.")]
        [SerializeField, Min(16f)] private float renderChunkTargetSize = 64f;
        [Tooltip("Chunks de datos cuya textura CTX se cose por frame (evita tirones).")]
        [SerializeField, Min(1)] private int albedoChunksPerFrame = 8;

        [Header("Física")]
        [SerializeField] private bool applyMarsGravity = true;

        [Header("Decoración (rocas y vetas)")]
        [SerializeField] private bool spawnDecorations = true;
        [SerializeField, Min(16f)] private float decorationRadius = 120f;
        [SerializeField, Min(8f)] private float cellSize = 32f;
        [SerializeField, Min(0)] private int maxRocksPerCell = 10;
        [SerializeField, Min(1)] private int maxCellsPerFrame = 2;
        [Tooltip("Las rocas por debajo de este tamaño (m) no tienen colisionador (ahorra física).")]
        [SerializeField, Min(0f)] private float minColliderRockSize = 0.35f;
        [SerializeField, Min(0f)] private float sharpRockImpactMultiplier = 1.6f;
        [SerializeField, Min(0f)] private float unitsPerVein = 50f;

        // --- Estado de la zona cargada ---
        private readonly Dictionary<Vector2Int, MarsChunkData> _chunks = new Dictionary<Vector2Int, MarsChunkData>();
        private TerrainManager _terrain;

        // --- Decoración ---
        private readonly Dictionary<Vector2Int, List<GameObject>> _cells = new Dictionary<Vector2Int, List<GameObject>>();
        private readonly Stack<GameObject> _rockPool = new Stack<GameObject>();
        private readonly Stack<GameObject> _veinPool = new Stack<GameObject>();
        private readonly List<Vector2Int> _cellsToRemove = new List<Vector2Int>();
        private readonly List<Vector2Int> _cellsToAdd = new List<Vector2Int>();
        private Mesh[] _rockMeshes;
        private Mesh _crystalMesh;
        private Material _rockMaterial;
        private Material[] _mineralMaterials;
        private Transform _decorRoot;
        private float _decorTimer;

        public MarsZoneManifest Manifest { get; private set; }
        public bool IsLoaded { get; private set; }
        public bool LastLoadSucceeded { get; private set; }
        public string LastError { get; private set; }
        public Texture2D OrbitalAlbedo { get; private set; }

        /// <summary>Elevación real (m sobre el datum de Marte) que corresponde a y = 0 del terreno.</summary>
        public float DatumOffsetMeters { get; private set; }

        public int SpawnedDecorationCount { get; private set; }

        private void Awake()
        {
            GameServices.Register(this);
            if (applyMarsGravity) Physics.gravity = new Vector3(0f, -MarsGravity, 0f);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
            if (OrbitalAlbedo != null) Destroy(OrbitalAlbedo);
            if (_rockMeshes != null) foreach (Mesh m in _rockMeshes) Destroy(m);
            if (_crystalMesh != null) Destroy(_crystalMesh);
        }

        // ------------------------------------------------------------------
        // Rutas
        // ------------------------------------------------------------------

        public string GetZoneFolder(string zoneId)
        {
            return Path.Combine(Path.Combine(Application.streamingAssetsPath, mapFolder), zoneId);
        }

        /// <summary>True si existe StreamingAssets/MarsMap/&lt;zona&gt;/manifest.json.</summary>
        public bool HasDataFor(string zoneId)
        {
            // Nota: en Android/WebGL StreamingAssets va dentro del paquete y File no sirve
            // (habría que usar UnityWebRequest). Este proyecto apunta a Windows.
            return !string.IsNullOrEmpty(zoneId) && File.Exists(Path.Combine(GetZoneFolder(zoneId), "manifest.json"));
        }

        // ------------------------------------------------------------------
        // Carga
        // ------------------------------------------------------------------

        /// <summary>
        /// Corrutina: carga la zona y arranca el TerrainManager con ella.
        /// <paramref name="baseSettings"/> aporta material/semilla; tamaño y altura se sacan de los datos.
        /// </summary>
        public IEnumerator LoadZone(string zoneId, TerrainMapSettings baseSettings, TerrainManager terrain)
        {
            IsLoaded = false;
            LastLoadSucceeded = false;
            LastError = null;
            ClearDecorations();
            _chunks.Clear();

            string folder = GetZoneFolder(zoneId);
            Task<LoadResult> task = Task.Run(() => LoadFilesInBackground(folder));
            while (!task.IsCompleted) yield return null;

            if (task.IsFaulted || task.Result == null)
            {
                Exception e = task.Exception != null ? task.Exception.GetBaseException() : null;
                LastError = e != null ? e.Message : "resultado vacío";
                Debug.LogError("[MarsTerrainGenerator] No se pudo cargar '" + zoneId + "': " + LastError);
                yield break;
            }

            LoadResult result = task.Result;
            Manifest = result.Manifest;
            DatumOffsetMeters = result.MinMeters;
            foreach (MarsChunkData chunk in result.Chunks) _chunks[chunk.Coord] = chunk;

            // Textura orbital: decodificar PNG exige el hilo principal; se reparte en varios frames.
            yield return StartCoroutine(BuildOrbitalAlbedo(result));

            // Ajustes del TerrainManager a partir de los datos reales.
            TerrainMapSettings settings = baseSettings != null ? baseSettings.Clone() : new TerrainMapSettings();
            settings.heightmap = null;
            int renderChunks = Mathf.Max(1, Mathf.RoundToInt(Manifest.zoneSizeMeters / renderChunkTargetSize));
            settings.mapSizeInChunks = renderChunks;
            settings.chunkWorldSize = Manifest.zoneSizeMeters / renderChunks;
            settings.heightScale = result.Field.HeightScale;

            terrain.Initialize(settings, result.Field);
            if (OrbitalAlbedo != null)
            {
                MarsGroundMaterialFactory.ApplyOrbitalAlbedo(terrain.TerrainMaterial, OrbitalAlbedo,
                    terrain.UvTileSize, Manifest.zoneSizeMeters);
            }
            _terrain = terrain;

            IsLoaded = true;
            LastLoadSucceeded = true;
            Debug.Log("[MarsTerrainGenerator] Zona '" + Manifest.displayName + "' cargada: " +
                      Manifest.chunksX + "×" + Manifest.chunksZ + " chunks de datos, " +
                      Manifest.zoneSizeMeters.ToString("0") + " m de lado, desnivel " +
                      settings.heightScale.ToString("0") + " m. Elevación: " + Manifest.sources.elevation +
                      " · Textura: " + Manifest.sources.albedo);
        }

        private sealed class LoadResult
        {
            public MarsZoneManifest Manifest;
            public HeightField Field;
            public float MinMeters;
            public List<MarsChunkData> Chunks = new List<MarsChunkData>();
        }

        /// <summary>Lectura de disco y montaje del campo de alturas. Sin API de Unity salvo JsonUtility (es segura en hilos).</summary>
        private static LoadResult LoadFilesInBackground(string folder)
        {
            var manifest = JsonUtility.FromJson<MarsZoneManifest>(File.ReadAllText(Path.Combine(folder, "manifest.json")));
            if (manifest == null || manifest.chunksX < 1 || manifest.verticesPerChunk < 2)
                throw new InvalidDataException("manifest.json no válido");
            if (manifest.chunksX != manifest.chunksZ)
                throw new InvalidDataException("solo se admiten zonas cuadradas (chunksX == chunksZ)");

            int n = manifest.chunksX;
            int v = manifest.verticesPerChunk;
            int res = n * (v - 1) + 1;
            float[] heights = new float[res * res]; // metros; fila 0 = sur
            float hMin = manifest.heightMinMeters;
            float hRange = manifest.heightMaxMeters - manifest.heightMinMeters;
            float micro = manifest.microScaleMeters / 127f;

            var result = new LoadResult { Manifest = manifest };

            for (int cz = 0; cz < n; cz++)
            {
                for (int cx = 0; cx < n; cx++)
                {
                    string baseName = Path.Combine(folder, "chunk_" + cx + "_" + cz);

                    byte[] hm = File.ReadAllBytes(baseName + ".heightmap");
                    if (hm.Length != v * v * 2)
                        throw new InvalidDataException("tamaño incorrecto en chunk_" + cx + "_" + cz + ".heightmap");

                    byte[] mic = File.Exists(baseName + ".micro") ? File.ReadAllBytes(baseName + ".micro") : null;
                    if (mic != null && mic.Length != v * v) mic = null;

                    int originX = cx * (v - 1);
                    int originZ = cz * (v - 1);
                    for (int z = 0; z < v; z++)
                    {
                        int row = (originZ + z) * res + originX;
                        for (int x = 0; x < v; x++)
                        {
                            int i = z * v + x;
                            int raw = hm[i * 2] | (hm[i * 2 + 1] << 8); // uint16 little-endian
                            float h = hMin + raw / 65535f * hRange;
                            if (mic != null) h += (sbyte)mic[i] * micro;
                            heights[row + x] = h; // los bordes compartidos reciben el mismo valor
                        }
                    }

                    var chunk = new MarsChunkData { Coord = new Vector2Int(cx, cz) };
                    if (File.Exists(baseName + ".rocks"))
                    {
                        chunk.RockMask = File.ReadAllBytes(baseName + ".rocks");
                        chunk.RockMaskSize = manifest.rockMaskSize;
                        if (chunk.RockMask.Length != manifest.rockMaskSize * manifest.rockMaskSize) chunk.RockMask = null;
                    }
                    string json = baseName + "_minerals.json";
                    if (File.Exists(json)) chunk.Metadata = JsonUtility.FromJson<MarsChunkMetadata>(File.ReadAllText(json));
                    string png = baseName + "_albedo.png";
                    if (File.Exists(png)) chunk.AlbedoPng = File.ReadAllBytes(png);
                    result.Chunks.Add(chunk);
                }
            }

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < heights.Length; i++)
            {
                if (heights[i] < min) min = heights[i];
                if (heights[i] > max) max = heights[i];
            }
            result.MinMeters = min;
            result.Field = HeightField.FromMeters(heights, res, manifest.zoneSizeMeters, min, max);
            return result;
        }

        /// <summary>Cose las texturas CTX de todos los chunks en una sola textura (DXT1).</summary>
        private IEnumerator BuildOrbitalAlbedo(LoadResult result)
        {
            int n = result.Manifest.chunksX;
            int a = result.Manifest.albedoSize;
            if (a < 4 || result.Chunks.Count == 0 || result.Chunks[0].AlbedoPng == null) yield break;

            if (OrbitalAlbedo != null) Destroy(OrbitalAlbedo);
            var big = new Texture2D(n * a, n * a, TextureFormat.RGB24, true);
            big.name = "Marte_CTX_" + result.Manifest.zoneId;
            var tmp = new Texture2D(2, 2, TextureFormat.RGB24, false);

            int done = 0;
            foreach (MarsChunkData chunk in result.Chunks)
            {
                if (chunk.AlbedoPng != null && tmp.LoadImage(chunk.AlbedoPng) && tmp.width == a && tmp.height == a)
                {
                    // LoadImage deja la fila superior del PNG (norte) arriba: coincide con +Z.
                    big.SetPixels32(chunk.Coord.x * a, chunk.Coord.y * a, a, a, tmp.GetPixels32());
                }
                chunk.AlbedoPng = null; // liberar el PNG en memoria
                if (++done % albedoChunksPerFrame == 0) yield return null;
            }
            Destroy(tmp);

            big.Apply(true, false);
            big.Compress(true);              // RGB24 (12 MB a 2048²) → DXT1 (~2 MB)
            big.Apply(false, true);          // sube a la GPU y libera la copia en RAM
            big.wrapMode = TextureWrapMode.Clamp;
            big.anisoLevel = 4;
            OrbitalAlbedo = big;
        }

        // ------------------------------------------------------------------
        // Consultas para el juego
        // ------------------------------------------------------------------

        /// <summary>Datos del chunk de datos (≈1 km) bajo una posición del mundo, o null.</summary>
        public MarsChunkData GetChunkAt(Vector3 worldPosition)
        {
            if (!IsLoaded || _terrain == null) return null;
            Vector3 local = _terrain.transform.InverseTransformPoint(worldPosition);
            var coord = new Vector2Int(Mathf.FloorToInt(local.x / Manifest.chunkSizeMeters),
                                       Mathf.FloorToInt(local.z / Manifest.chunkSizeMeters));
            MarsChunkData chunk;
            return _chunks.TryGetValue(coord, out chunk) ? chunk : null;
        }

        /// <summary>Elevación real respecto al datum de Marte (m), como la daría MOLA.</summary>
        public float GetAreoidElevation(Vector3 worldPosition)
        {
            if (_terrain == null) return 0f;
            return _terrain.GetHeightAt(worldPosition) - _terrain.transform.position.y + DatumOffsetMeters;
        }

        // ------------------------------------------------------------------
        // Decoración por celdas alrededor del robot
        // ------------------------------------------------------------------

        private void Update()
        {
            if (!IsLoaded || !spawnDecorations || _terrain == null || _terrain.Target == null) return;

            _decorTimer -= Time.deltaTime;
            if (_decorTimer > 0f) return;
            _decorTimer = 0.4f;
            RefreshDecorations(_terrain.transform.InverseTransformPoint(_terrain.Target.position));
        }

        private void RefreshDecorations(Vector3 localTarget)
        {
            EnsureDecorAssets();
            float zone = Manifest.zoneSizeMeters;
            int r = Mathf.CeilToInt(decorationRadius / cellSize);
            var center = new Vector2Int(Mathf.FloorToInt(localTarget.x / cellSize), Mathf.FloorToInt(localTarget.z / cellSize));

            // Quitar celdas lejanas (con margen de una celda: histéresis).
            _cellsToRemove.Clear();
            foreach (var pair in _cells)
            {
                if (CellDistance(pair.Key, localTarget) > decorationRadius + cellSize) _cellsToRemove.Add(pair.Key);
            }
            foreach (Vector2Int c in _cellsToRemove) DespawnCell(c);

            // Añadir las cercanas que falten (las más próximas primero, pocas por refresco).
            _cellsToAdd.Clear();
            for (int dz = -r; dz <= r; dz++)
            {
                for (int dx = -r; dx <= r; dx++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dz);
                    if (_cells.ContainsKey(c)) continue;
                    if (c.x < 0 || c.y < 0 || c.x * cellSize >= zone || c.y * cellSize >= zone) continue;
                    if (CellDistance(c, localTarget) > decorationRadius) continue;
                    _cellsToAdd.Add(c);
                }
            }
            _cellsToAdd.Sort((p, q) => CellDistance(p, localTarget).CompareTo(CellDistance(q, localTarget)));
            for (int i = 0; i < _cellsToAdd.Count && i < maxCellsPerFrame; i++) SpawnCell(_cellsToAdd[i]);
        }

        private float CellDistance(Vector2Int c, Vector3 local)
        {
            float cx = (c.x + 0.5f) * cellSize - local.x;
            float cz = (c.y + 0.5f) * cellSize - local.z;
            return Mathf.Sqrt(cx * cx + cz * cz);
        }

        private void SpawnCell(Vector2Int cell)
        {
            var list = new List<GameObject>();
            _cells[cell] = list;
            unchecked
            {
                // Semilla estable por celda y zona: la misma celda siempre tiene las mismas rocas.
                int zoneHash = 17;
                foreach (char ch in Manifest.zoneId) zoneHash = zoneHash * 31 + ch;
                var rng = new System.Random(cell.x * 73856093 ^ cell.y * 19349663 ^ zoneHash);
                float chunkSize = Manifest.chunkSizeMeters;

                // --- Rocas: cantidad según la máscara HiRISE en el centro de la celda ---
                float cxm = (cell.x + 0.5f) * cellSize, czm = (cell.y + 0.5f) * cellSize;
                MarsChunkData chunk = ChunkAtLocal(cxm, czm);
                float density = chunk != null
                    ? chunk.SampleRockDensity((cxm % chunkSize) / chunkSize, (czm % chunkSize) / chunkSize)
                    : 0.2f;
                int count = Mathf.RoundToInt(density * maxRocksPerCell + (float)rng.NextDouble());

                for (int i = 0; i < count; i++)
                {
                    float lx = (cell.x + (float)rng.NextDouble()) * cellSize;
                    float lz = (cell.y + (float)rng.NextDouble()) * cellSize;
                    // Ley de potencia: muchas piedras pequeñas y pocas grandes.
                    float t = (float)rng.NextDouble();
                    float size = 0.15f + 1.6f * t * t * t;
                    list.Add(PlaceRock(lx, lz, size, rng));
                }

                // --- Vetas: las que el script de Python situó dentro de esta celda ---
                if (chunk != null && chunk.Metadata != null && chunk.Metadata.veins != null)
                {
                    foreach (MarsMineralVein vein in chunk.Metadata.veins)
                    {
                        float vx = (chunk.Coord.x + vein.u) * chunkSize;
                        float vz = (chunk.Coord.y + vein.v) * chunkSize;
                        if (Mathf.FloorToInt(vx / cellSize) != cell.x || Mathf.FloorToInt(vz / cellSize) != cell.y) continue;
                        list.Add(PlaceVein(vx, vz, vein, rng));
                    }
                }
            }
            SpawnedDecorationCount += list.Count;
        }

        private void DespawnCell(Vector2Int cell)
        {
            List<GameObject> list;
            if (!_cells.TryGetValue(cell, out list)) return;
            foreach (GameObject go in list)
            {
                if (go == null) continue;
                go.SetActive(false);
                if (go.GetComponent<MineralDeposit>() != null) _veinPool.Push(go);
                else _rockPool.Push(go);
            }
            SpawnedDecorationCount -= list.Count;
            _cells.Remove(cell);
        }

        private void ClearDecorations()
        {
            _cellsToRemove.Clear();
            foreach (var pair in _cells) _cellsToRemove.Add(pair.Key);
            foreach (Vector2Int c in _cellsToRemove) DespawnCell(c);
        }

        private MarsChunkData ChunkAtLocal(float x, float z)
        {
            var coord = new Vector2Int(Mathf.FloorToInt(x / Manifest.chunkSizeMeters), Mathf.FloorToInt(z / Manifest.chunkSizeMeters));
            MarsChunkData chunk;
            return _chunks.TryGetValue(coord, out chunk) ? chunk : null;
        }

        private Vector3 GroundPoint(float lx, float lz)
        {
            Vector3 world = _terrain.transform.TransformPoint(new Vector3(lx, 0f, lz));
            world.y = _terrain.GetHeightAt(world);
            return world;
        }

        private GameObject PlaceRock(float lx, float lz, float size, System.Random rng)
        {
            GameObject go = _rockPool.Count > 0 ? _rockPool.Pop() : CreateRockObject();
            go.GetComponent<MeshFilter>().sharedMesh = _rockMeshes[rng.Next(_rockMeshes.Length)];
            var scale = new Vector3(size * Lerp(rng, 0.8f, 1.3f), size * Lerp(rng, 0.45f, 0.9f), size * Lerp(rng, 0.8f, 1.3f));
            go.transform.localScale = scale;
            go.transform.rotation = Quaternion.Euler(Lerp(rng, -12f, 12f), Lerp(rng, 0f, 360f), Lerp(rng, -12f, 12f));
            // Medio enterrada, como las rocas reales sobre el regolito.
            go.transform.position = GroundPoint(lx, lz) + Vector3.up * (scale.y * 0.2f);
            go.GetComponent<Collider>().enabled = size >= minColliderRockSize;
            go.SetActive(true);
            return go;
        }

        private GameObject PlaceVein(float lx, float lz, MarsMineralVein vein, System.Random rng)
        {
            GameObject go = _veinPool.Count > 0 ? _veinPool.Pop() : CreateVeinObject();
            var deposit = go.GetComponent<MineralDeposit>();
            deposit.mineral = MineralDeposit.Parse(vein.mineral);
            deposit.richness = Mathf.Clamp01(vein.richness);
            deposit.remainingUnits = unitsPerVein * (0.5f + deposit.richness);

            Material mat = _mineralMaterials[(int)deposit.mineral];
            Transform t = go.transform;
            for (int i = 0; i < t.childCount; i++)
            {
                Transform crystal = t.GetChild(i);
                crystal.GetComponent<MeshRenderer>().sharedMaterial = mat;
                float s = 0.2f + 0.5f * deposit.richness;
                crystal.localPosition = new Vector3(Lerp(rng, -0.6f, 0.6f), 0f, Lerp(rng, -0.6f, 0.6f));
                crystal.localRotation = Quaternion.Euler(Lerp(rng, -35f, 35f), Lerp(rng, 0f, 360f), Lerp(rng, -35f, 35f));
                crystal.localScale = new Vector3(s * 0.35f, s * Lerp(rng, 1f, 2.2f), s * 0.35f);
            }
            go.transform.position = GroundPoint(lx, lz);
            go.name = "Veta_" + MineralDeposit.DisplayName(deposit.mineral);
            go.SetActive(true);
            return go;
        }

        private static float Lerp(System.Random rng, float a, float b)
        {
            return a + (b - a) * (float)rng.NextDouble();
        }

        // ------------------------------------------------------------------
        // Recursos de decoración (mallas y materiales creados por código)
        // ------------------------------------------------------------------

        private void EnsureDecorAssets()
        {
            if (_rockMeshes != null) return;
            _decorRoot = new GameObject("Decoracion (rocas y vetas)").transform;
            _decorRoot.SetParent(transform, false);

            // Mallas base de las primitivas de Unity (sin depender del nombre interno del recurso).
            Mesh sphere = PrimitiveMesh(PrimitiveType.Sphere);
            _rockMeshes = new Mesh[5];
            for (int i = 0; i < _rockMeshes.Length; i++) _rockMeshes[i] = BuildFacetedRock(sphere, 1000 + i * 37);
            _crystalMesh = Instantiate(PrimitiveMesh(PrimitiveType.Cube));

            _rockMaterial = MakeMaterial(new Color(0.27f, 0.17f, 0.13f), 0.15f);
            _mineralMaterials = new Material[4];
            _mineralMaterials[(int)MineralType.Clays] = MakeMaterial(new Color(0.55f, 0.6f, 0.48f), 0.2f);
            _mineralMaterials[(int)MineralType.Sulfates] = MakeMaterial(new Color(0.93f, 0.91f, 0.86f), 0.35f);
            _mineralMaterials[(int)MineralType.IronOxides] = MakeMaterial(new Color(0.42f, 0.13f, 0.1f), 0.55f);
            _mineralMaterials[(int)MineralType.SubsurfaceIce] = MakeMaterial(new Color(0.8f, 0.9f, 1f), 0.85f);
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject tmp = GameObject.CreatePrimitive(type);
            Mesh mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            DestroyImmediate(tmp);
            return mesh;
        }

        /// <summary>
        /// Roca angulosa: una esfera recortada por planos al azar (como una piedra fracturada).
        /// Cada vértice se acerca al centro hasta quedar dentro de todos los planos.
        /// </summary>
        private static Mesh BuildFacetedRock(Mesh sphere, int seed)
        {
            var rng = new System.Random(seed);
            const int planeCount = 9;
            var normals = new Vector3[planeCount];
            var offsets = new float[planeCount];
            for (int i = 0; i < planeCount; i++)
            {
                normals[i] = new Vector3(Lerp(rng, -1f, 1f), Lerp(rng, -0.6f, 1f), Lerp(rng, -1f, 1f)).normalized;
                offsets[i] = Lerp(rng, 0.28f, 0.45f); // la esfera base tiene radio 0,5
            }

            Vector3[] verts = sphere.vertices;
            for (int v = 0; v < verts.Length; v++)
            {
                Vector3 p = verts[v];
                float scale = 1f;
                for (int i = 0; i < planeCount; i++)
                {
                    float d = Vector3.Dot(p, normals[i]);
                    if (d > offsets[i]) scale = Mathf.Min(scale, offsets[i] / d);
                }
                verts[v] = p * scale;
            }

            var mesh = new Mesh { name = "RocaAfilada_" + seed };
            mesh.vertices = verts;
            mesh.triangles = sphere.triangles;
            mesh.uv = sphere.uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private GameObject CreateRockObject()
        {
            var go = new GameObject("Roca");
            go.transform.SetParent(_decorRoot, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>().sharedMaterial = _rockMaterial;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.4f;
            var hazard = go.AddComponent<SurfaceHazard>();
            hazard.impactMultiplier = sharpRockImpactMultiplier;
            return go;
        }

        private GameObject CreateVeinObject()
        {
            var go = new GameObject("Veta");
            go.transform.SetParent(_decorRoot, false);
            go.AddComponent<MineralDeposit>();
            var trigger = go.AddComponent<SphereCollider>();
            trigger.isTrigger = true; // detección para la futura extracción
            trigger.radius = 1.5f;
            for (int i = 0; i < 5; i++)
            {
                var crystal = new GameObject("Cristal");
                crystal.transform.SetParent(go.transform, false);
                crystal.AddComponent<MeshFilter>().sharedMesh = _crystalMesh;
                crystal.AddComponent<MeshRenderer>();
            }
            return go;
        }

        private static Material MakeMaterial(Color color, float smoothness)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var mat = new Material(shader);
            mat.color = color;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
            mat.enableInstancing = true;
            return mat;
        }
    }
}
