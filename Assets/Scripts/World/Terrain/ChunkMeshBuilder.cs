using System.Collections.Concurrent;
using UnityEngine;

namespace MarsRobot.World.TerrainSystem
{
    /// <summary>
    /// Datos de malla de un chunk generados fuera de Unity (arrays puros).
    /// Se reciclan mediante <see cref="ChunkMeshDataPool"/> para no generar basura cada vez
    /// que un chunk se carga o cambia de LOD.
    /// </summary>
    public sealed class ChunkMeshData
    {
        public readonly int Lod;
        public readonly int Resolution;
        public readonly Vector3[] Vertices;
        public readonly Vector3[] Normals;
        public readonly Vector2[] Uvs;

        // Datos del trabajo en curso (los rellena el TerrainManager).
        public TerrainChunk Chunk;
        public int Version;

        public ChunkMeshData(int lod, int resolution)
        {
            Lod = lod;
            Resolution = resolution;
            int count = ChunkMeshBuilder.VertexCount(resolution);
            Vertices = new Vector3[count];
            Normals = new Vector3[count];
            Uvs = new Vector2[count];
        }
    }

    /// <summary>
    /// Pool de <see cref="ChunkMeshData"/> por nivel de LOD. Es seguro entre hilos.
    /// </summary>
    public sealed class ChunkMeshDataPool
    {
        private readonly ConcurrentBag<ChunkMeshData>[] _bags;
        private readonly int[] _resolutions;

        public ChunkMeshDataPool(int[] resolutionsPerLod)
        {
            _resolutions = resolutionsPerLod;
            _bags = new ConcurrentBag<ChunkMeshData>[resolutionsPerLod.Length];
            for (int i = 0; i < _bags.Length; i++)
            {
                _bags[i] = new ConcurrentBag<ChunkMeshData>();
            }
        }

        public ChunkMeshData Rent(int lod)
        {
            ChunkMeshData data;
            if (_bags[lod].TryTake(out data)) return data;
            return new ChunkMeshData(lod, _resolutions[lod]);
        }

        public void Return(ChunkMeshData data)
        {
            if (data == null) return;
            data.Chunk = null;
            // Limitamos lo que se guarda para no retener memoria de más.
            if (_bags[data.Lod].Count < 8) _bags[data.Lod].Add(data);
        }
    }

    /// <summary>
    /// Construye la geometría de un chunk: una cuadrícula de (res+1)² vértices más una
    /// "falda" (skirt) vertical alrededor del borde que tapa las grietas entre chunks
    /// vecinos con distinto LOD. Todo es matemática pura: se puede ejecutar en un hilo de fondo.
    /// </summary>
    public static class ChunkMeshBuilder
    {
        /// <summary>Vértices de la cuadrícula + anillo de falda.</summary>
        public static int VertexCount(int resolution)
        {
            int n = resolution + 1;
            return n * n + 4 * resolution;
        }

        /// <summary>
        /// Índices de triángulos para una resolución. Son iguales para todos los chunks del
        /// mismo LOD, así que el TerrainManager los calcula una sola vez y los comparte.
        /// Orden de vértices en sentido horario visto desde arriba (convención de Unity).
        /// </summary>
        public static int[] BuildTriangles(int resolution)
        {
            int n = resolution + 1;
            int gridTris = resolution * resolution * 6;
            int skirtTris = 4 * resolution * 6;
            var tris = new int[gridTris + skirtTris];
            int t = 0;

            // Cuadrícula principal.
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int a = z * n + x;       // (x,   z)
                    int b = a + 1;           // (x+1, z)
                    int c = a + n;           // (x,   z+1)
                    int d = c + 1;           // (x+1, z+1)
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            }

            // Falda: recorremos el perímetro en sentido antihorario (visto desde arriba);
            // así las caras de la falda miran hacia fuera.
            int perimeter = 4 * resolution;
            int skirtStart = n * n;
            for (int i = 0; i < perimeter; i++)
            {
                int next = (i + 1) % perimeter;
                int p0 = PerimeterIndex(i, resolution);
                int p1 = PerimeterIndex(next, resolution);
                int s0 = skirtStart + i;
                int s1 = skirtStart + next;
                tris[t++] = p0; tris[t++] = p1; tris[t++] = s0;
                tris[t++] = s0; tris[t++] = p1; tris[t++] = s1;
            }
            return tris;
        }

        /// <summary>
        /// Índice del vértice de la cuadrícula que ocupa la posición <paramref name="i"/> del perímetro.
        /// Orden: borde inferior (+X), derecho (+Z), superior (−X), izquierdo (−Z).
        /// </summary>
        public static int PerimeterIndex(int i, int resolution)
        {
            int n = resolution + 1;
            int side = i / resolution;
            int k = i % resolution;
            switch (side)
            {
                case 0: return k;                                   // z = 0,   x = k
                case 1: return k * n + resolution;                  // x = res, z = k
                case 2: return resolution * n + (resolution - k);   // z = res, x = res - k
                default: return (resolution - k) * n;               // x = 0,   z = res - k
            }
        }

        /// <summary>
        /// Rellena <paramref name="data"/> con la geometría del chunk <paramref name="coord"/>.
        /// Los vértices son locales al origen del chunk (esquina inferior izquierda).
        /// </summary>
        public static void Build(HeightField field, Vector2Int coord, float chunkSize,
                                 float skirtDepth, float uvTileSize, ChunkMeshData data)
        {
            int res = data.Resolution;
            int n = res + 1;
            float step = chunkSize / res;
            float originX = coord.x * chunkSize;
            float originZ = coord.y * chunkSize;
            float invTile = uvTileSize > 0.0001f ? 1f / uvTileSize : 1f;

            Vector3[] v = data.Vertices;
            Vector3[] nr = data.Normals;
            Vector2[] uv = data.Uvs;

            for (int z = 0; z < n; z++)
            {
                float lz = z * step;
                float wz = originZ + lz;
                for (int x = 0; x < n; x++)
                {
                    float lx = x * step;
                    float wx = originX + lx;
                    int idx = z * n + x;
                    v[idx] = new Vector3(lx, field.SampleHeight(wx, wz), lz);
                    nr[idx] = field.SampleNormal(wx, wz, step);
                    // UV en espacio mundo: la textura continúa sin cortes entre chunks.
                    uv[idx] = new Vector2(wx * invTile, wz * invTile);
                }
            }

            int skirtStart = n * n;
            int perimeter = 4 * res;
            for (int i = 0; i < perimeter; i++)
            {
                int p = PerimeterIndex(i, res);
                Vector3 top = v[p];
                v[skirtStart + i] = new Vector3(top.x, top.y - skirtDepth, top.z);
                nr[skirtStart + i] = nr[p];
                uv[skirtStart + i] = uv[p];
            }
        }
    }
}
