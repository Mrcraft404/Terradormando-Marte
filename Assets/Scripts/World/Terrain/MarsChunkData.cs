using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.World.TerrainSystem
{
    // ======================================================================
    // Formato de los archivos que genera Tools/MapProcessor/generate_mars_chunks.py
    // en StreamingAssets/MarsMap/<zona>/. Los nombres de los campos DEBEN coincidir con
    // las claves JSON del script de Python (JsonUtility distingue mayúsculas).
    // ======================================================================

    /// <summary>De dónde salió cada capa de datos ("real: ..." o "synthetic: ...").</summary>
    [Serializable]
    public class MarsMapSources
    {
        public string elevation;
        public string albedo;
        public string microRelief;
        public string rocks;
        public string minerals;
        public string climate;
    }

    /// <summary>Perfil climático y de radiación de la zona (valores medios aproximados).</summary>
    [Serializable]
    public class MarsClimateProfile
    {
        public float meanTemperatureC;
        public float minTemperatureC;
        public float maxTemperatureC;
        public float meanWindSpeed;
        public float gustWindSpeed;
        public float surfacePressurePa;
        public float radiationMilliGrayPerDay;
        public float dustStormChancePerSol;
    }

    /// <summary>manifest.json: describe la zona completa y cómo leer sus chunks.</summary>
    [Serializable]
    public class MarsZoneManifest
    {
        public int formatVersion;
        public string zoneId;
        public string displayName;
        public float centerLat;
        public float centerLonEast;
        public int chunksX;
        public int chunksZ;
        public int verticesPerChunk;     // 256: los chunks vecinos comparten la fila/columna del borde
        public float metersPerSample;
        public float chunkSizeMeters;    // (verticesPerChunk - 1) * metersPerSample
        public float zoneSizeMeters;     // chunksX * chunkSizeMeters
        public float heightMinMeters;    // elevación respecto al datum de Marte (MOLA)
        public float heightMaxMeters;
        public float microScaleMeters;   // ±escala del micro-relieve (int8 / 127 * escala)
        public int albedoSize;           // píxeles por lado de cada chunk_X_Y_albedo.png
        public int rockMaskSize;         // píxeles por lado de cada chunk_X_Y.rocks
        public float gravity;            // m/s² (3,721 en Marte)
        public MarsMapSources sources;
        public MarsClimateProfile climate;
        public string generatedUtc;
    }

    /// <summary>Concentraciones relativas 0..1 (índices de juego, no porcentajes en peso).</summary>
    [Serializable]
    public class MarsMineralConcentrations
    {
        public float clays;
        public float sulfates;
        public float ironOxides;
        public float subsurfaceIce;
    }

    /// <summary>Entorno medio del chunk (ajustado por su altitud).</summary>
    [Serializable]
    public class MarsChunkEnvironment
    {
        public float meanTemperatureC;
        public float windSpeed;
        public float pressurePa;
        public float radiationMilliGrayPerDay;
    }

    /// <summary>Veta de mineral extraíble. u, v en 0..1 dentro del chunk (v = 0 al sur).</summary>
    [Serializable]
    public class MarsMineralVein
    {
        public string mineral;   // "clays" | "sulfates" | "ironOxides" | "subsurfaceIce"
        public float u;
        public float v;
        public float richness;   // 0..1
    }

    /// <summary>chunk_X_Y_minerals.json.</summary>
    [Serializable]
    public class MarsChunkMetadata
    {
        public int chunkX;
        public int chunkZ;
        public float meanElevationMeters;
        public float minElevationMeters;
        public float maxElevationMeters;
        public float rockDensity;
        public int craterCount;
        public int crismObservations;
        public float hiriseCoverage;
        public MarsMineralConcentrations minerals;
        public MarsChunkEnvironment environment;
        public List<MarsMineralVein> veins;
        public string source;
    }

    /// <summary>
    /// Datos en memoria de un chunk de la cuadrícula de datos (≈1 km). Las alturas no se guardan
    /// aquí: se vuelcan directamente al HeightField de la zona para no duplicar memoria.
    /// </summary>
    public sealed class MarsChunkData
    {
        public Vector2Int Coord;
        public MarsChunkMetadata Metadata;

        /// <summary>Máscara de densidad de rocas/cráteres (0..255), fila 0 = sur.</summary>
        public byte[] RockMask;
        public int RockMaskSize;

        /// <summary>PNG de la textura CTX tal cual viene del disco; se decodifica en el hilo principal y se libera.</summary>
        public byte[] AlbedoPng;

        /// <summary>Densidad de rocas 0..1 en (u, v) del chunk, con interpolación bilineal.</summary>
        public float SampleRockDensity(float u, float v)
        {
            if (RockMask == null || RockMaskSize < 2)
            {
                return Metadata != null ? Metadata.rockDensity : 0.2f;
            }
            int last = RockMaskSize - 1;
            float x = Mathf.Clamp01(u) * last;
            float y = Mathf.Clamp01(v) * last;
            int x0 = (int)x, y0 = (int)y;
            int x1 = Mathf.Min(x0 + 1, last), y1 = Mathf.Min(y0 + 1, last);
            float tx = x - x0, ty = y - y0;
            float a = Mathf.Lerp(RockMask[y0 * RockMaskSize + x0], RockMask[y0 * RockMaskSize + x1], tx);
            float b = Mathf.Lerp(RockMask[y1 * RockMaskSize + x0], RockMask[y1 * RockMaskSize + x1], tx);
            return Mathf.Lerp(a, b, ty) / 255f;
        }
    }
}
