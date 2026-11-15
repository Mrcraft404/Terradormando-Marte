using System;
using UnityEngine;

namespace MarsRobot.World.TerrainSystem
{
    /// <summary>
    /// Definición del mapa estático de una zona. Se guarda dentro de cada
    /// <c>LandingZoneData</c> para que cada zona de aterrizaje tenga su propio relieve.
    /// </summary>
    [Serializable]
    public class TerrainMapSettings
    {
        [Tooltip("Mapa de alturas en escala de grises (canal R). Activa 'Read/Write' en el importador. " +
                 "Si se deja vacío se genera un mapa procedural determinista con la semilla.")]
        public Texture2D heightmap;

        [Tooltip("Número de chunks por lado. El mapa es cuadrado.")]
        [Min(1)] public int mapSizeInChunks = 32;

        [Tooltip("Tamaño de cada chunk en metros.")]
        [Min(8f)] public float chunkWorldSize = 64f;

        [Tooltip("Altura máxima en metros (valor 1 del mapa de alturas).")]
        [Min(1f)] public float heightScale = 120f;

        [Header("Mapa procedural de reserva")]
        [Tooltip("Muestras por lado del mapa procedural. 1025 ≈ 4 MB en memoria (float).")]
        [Range(129, 2049)] public int proceduralResolution = 1025;

        public int seed = 1234;

        [Tooltip("0 = llanura suave; 1 = terreno muy accidentado.")]
        [Range(0f, 1f)] public float roughness = 0.4f;

        [Tooltip("Número de cráteres que se estampan sobre el ruido.")]
        [Range(0, 80)] public int craterCount = 25;

        /// <summary>Lado del mapa en metros.</summary>
        public float WorldSize
        {
            get { return mapSizeInChunks * chunkWorldSize; }
        }

        /// <summary>Copia superficial, útil para modificar valores en tiempo de ejecución sin tocar el asset.</summary>
        public TerrainMapSettings Clone()
        {
            return (TerrainMapSettings)MemberwiseClone();
        }
    }
}
