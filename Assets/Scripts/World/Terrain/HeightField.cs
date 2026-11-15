using System;
using UnityEngine;

namespace MarsRobot.World.TerrainSystem
{
    /// <summary>
    /// Campo de alturas estático del mapa, guardado como un array de floats normalizados (0..1).
    ///
    /// Se crea UNA vez en el hilo principal (leer texturas o usar Mathf.PerlinNoise exige el hilo
    /// principal). Después es de solo lectura, así que los métodos <see cref="SampleHeight"/> y
    /// <see cref="SampleNormal"/> se pueden llamar desde hilos de fondo sin bloqueos.
    /// Solo usa matemática gestionada (sin API nativa de Unity) al muestrear.
    /// </summary>
    public sealed class HeightField
    {
        private readonly float[] _heights;

        /// <summary>Muestras por lado.</summary>
        public int Resolution { get; private set; }

        /// <summary>Lado del mapa en metros.</summary>
        public float WorldSize { get; private set; }

        /// <summary>Altura en metros correspondiente al valor 1.</summary>
        public float HeightScale { get; private set; }

        /// <summary>Memoria aproximada que ocupa el campo, en bytes.</summary>
        public long MemoryBytes
        {
            get { return (long)_heights.Length * sizeof(float); }
        }

        private HeightField(float[] heights, int resolution, float worldSize, float heightScale)
        {
            _heights = heights;
            Resolution = resolution;
            WorldSize = worldSize;
            HeightScale = heightScale;
        }

        // ------------------------------------------------------------------
        // Construcción (solo hilo principal)
        // ------------------------------------------------------------------

        /// <summary>Crea el campo a partir de los ajustes: textura si existe, procedural si no.</summary>
        public static HeightField Create(TerrainMapSettings settings)
        {
            if (settings.heightmap != null)
            {
                if (settings.heightmap.isReadable)
                {
                    return FromTexture(settings.heightmap, settings.WorldSize, settings.heightScale);
                }
                Debug.LogWarning("[HeightField] El mapa de alturas '" + settings.heightmap.name +
                                 "' no tiene Read/Write activado. Se usa el mapa procedural.");
            }
            return Procedural(settings);
        }

        /// <summary>
        /// Crea el campo a partir de alturas en METROS (fila 0 = sur, índice z * res + x).
        /// Normaliza IN PLACE el array (para no duplicar memoria): después contiene valores 0..1.
        /// La altura 0 del mundo corresponde a <paramref name="minMeters"/>. Seguro desde hilos de fondo.
        /// </summary>
        public static HeightField FromMeters(float[] heightsMeters, int resolution, float worldSize,
                                             float minMeters, float maxMeters)
        {
            float range = Mathf.Max(0.01f, maxMeters - minMeters);
            float inv = 1f / range;
            for (int i = 0; i < heightsMeters.Length; i++)
            {
                float v = (heightsMeters[i] - minMeters) * inv;
                heightsMeters[i] = v < 0f ? 0f : (v > 1f ? 1f : v);
            }
            return new HeightField(heightsMeters, resolution, worldSize, range);
        }

        /// <summary>
        /// Lee una textura fila a fila para no crear un array gigante de Color temporal
        /// (GetPixels() completo de 2049² ocuparía ~67 MB; así solo una fila cada vez).
        /// </summary>
        public static HeightField FromTexture(Texture2D tex, float worldSize, float heightScale)
        {
            int res = Mathf.Min(tex.width, tex.height);
            var heights = new float[res * res];
            for (int y = 0; y < res; y++)
            {
                Color[] row = tex.GetPixels(0, y, res, 1);
                int offset = y * res;
                for (int x = 0; x < res; x++)
                {
                    heights[offset + x] = row[x].r;
                }
            }
            return new HeightField(heights, res, worldSize, heightScale);
        }

        /// <summary>
        /// Genera un relieve marciano determinista: ruido fBm + cráteres con borde elevado.
        /// La misma semilla produce siempre el mismo mapa (mapa "estático").
        /// </summary>
        public static HeightField Procedural(TerrainMapSettings s)
        {
            int res = Mathf.Clamp(s.proceduralResolution, 129, 2049);
            var heights = new float[res * res];
            var rng = new System.Random(s.seed);

            // Desplazamiento de la semilla dentro del espacio de ruido.
            float ox = (float)rng.NextDouble() * 10000f;
            float oz = (float)rng.NextDouble() * 10000f;

            int octaves = 4 + Mathf.RoundToInt(s.roughness * 3f); // 4..7 octavas
            float baseFreq = 2.5f + s.roughness * 3f;              // ondulaciones por mapa
            float persistence = 0.42f + s.roughness * 0.18f;

            // Cráteres pre-calculados (en coordenadas normalizadas 0..1).
            int craterCount = Mathf.Max(0, s.craterCount);
            var cx = new float[craterCount];
            var cz = new float[craterCount];
            var cr = new float[craterCount];
            var cd = new float[craterCount];
            for (int i = 0; i < craterCount; i++)
            {
                cx[i] = (float)rng.NextDouble();
                cz[i] = (float)rng.NextDouble();
                // Muchos cráteres pequeños y pocos grandes.
                float t = (float)rng.NextDouble();
                cr[i] = Mathf.Lerp(0.01f, 0.08f, t * t);
                cd[i] = cr[i] * Mathf.Lerp(1.2f, 2.2f, (float)rng.NextDouble());
            }

            float inv = 1f / (res - 1);
            for (int z = 0; z < res; z++)
            {
                float nz = z * inv;
                for (int x = 0; x < res; x++)
                {
                    float nx = x * inv;

                    // fBm (suma de octavas de Perlin).
                    float amp = 1f, freq = baseFreq, sum = 0f, norm = 0f;
                    for (int o = 0; o < octaves; o++)
                    {
                        sum += amp * Mathf.PerlinNoise(ox + nx * freq, oz + nz * freq);
                        norm += amp;
                        amp *= persistence;
                        freq *= 2f;
                    }
                    float h = sum / norm; // ~0..1, centrado en 0.5

                    // Cráteres: cuenco parabólico + anillo elevado gaussiano.
                    for (int i = 0; i < craterCount; i++)
                    {
                        float dx = nx - cx[i];
                        float dz = nz - cz[i];
                        float d2 = dx * dx + dz * dz;
                        float r = cr[i];
                        float limit = r * 1.8f;
                        if (d2 > limit * limit) continue;

                        float d = Mathf.Sqrt(d2) / r; // 0 en el centro, 1 en el borde
                        if (d < 1f)
                        {
                            h -= cd[i] * (1f - d * d);
                        }
                        float rim = (d - 1f) / 0.25f;
                        h += cd[i] * 0.35f * Mathf.Exp(-rim * rim);
                    }

                    heights[z * res + x] = Mathf.Clamp01(h);
                }
            }

            return new HeightField(heights, res, s.WorldSize, s.heightScale);
        }

        // ------------------------------------------------------------------
        // Muestreo (seguro desde hilos de fondo)
        // ------------------------------------------------------------------

        /// <summary>Altura en metros en la posición mundial (X,Z), con interpolación bilineal.</summary>
        public float SampleHeight(float worldX, float worldZ)
        {
            int last = Resolution - 1;
            float u = Clamp01(worldX / WorldSize) * last;
            float v = Clamp01(worldZ / WorldSize) * last;

            int x0 = (int)u;
            int z0 = (int)v;
            int x1 = x0 < last ? x0 + 1 : last;
            int z1 = z0 < last ? z0 + 1 : last;
            float tx = u - x0;
            float tz = v - z0;

            float h00 = _heights[z0 * Resolution + x0];
            float h10 = _heights[z0 * Resolution + x1];
            float h01 = _heights[z1 * Resolution + x0];
            float h11 = _heights[z1 * Resolution + x1];

            float a = h00 + (h10 - h00) * tx;
            float b = h01 + (h11 - h01) * tx;
            return (a + (b - a) * tz) * HeightScale;
        }

        /// <summary>
        /// Aplana un círculo a una altura (m), con un borde de transición suave: así una base o un
        /// cohete aterrizado quedan sobre suelo nivelado. Coordenadas locales del terreno.
        /// </summary>
        public void FlattenCircle(float centerX, float centerZ, float radius, float blend, float heightMeters)
        {
            int last = Resolution - 1;
            float cell = WorldSize / last;
            float target = heightMeters / HeightScale;
            float outer = radius + blend;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((centerX - outer) / cell)), x1 = Mathf.Min(last, Mathf.CeilToInt((centerX + outer) / cell));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((centerZ - outer) / cell)), z1 = Mathf.Min(last, Mathf.CeilToInt((centerZ + outer) / cell));
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x * cell - centerX, dz = z * cell - centerZ;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d >= outer) continue;
                    float t = d <= radius ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (d - radius) / Mathf.Max(0.01f, blend));
                    int i = z * Resolution + x;
                    _heights[i] = Mathf.Lerp(_heights[i], target, t);
                }
            }
        }

        /// <summary>
        /// Normal por diferencias centrales. Al usar el campo global (y no la malla del chunk),
        /// los bordes entre chunks quedan con la misma normal y no se notan costuras de luz.
        /// </summary>
        public Vector3 SampleNormal(float worldX, float worldZ, float epsilon)
        {
            float hL = SampleHeight(worldX - epsilon, worldZ);
            float hR = SampleHeight(worldX + epsilon, worldZ);
            float hD = SampleHeight(worldX, worldZ - epsilon);
            float hU = SampleHeight(worldX, worldZ + epsilon);
            var n = new Vector3(hL - hR, 2f * epsilon, hD - hU);
            return n.normalized;
        }

        private static float Clamp01(float v)
        {
            return v < 0f ? 0f : (v > 1f ? 1f : v);
        }
    }
}
