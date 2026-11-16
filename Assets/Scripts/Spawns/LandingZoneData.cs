using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Spawns
{
    /// <summary>Nivel de dificultad de una zona de aterrizaje.</summary>
    public enum ZoneDifficulty
    {
        Easy,         // Fácil
        Low,          // Baja
        Medium,       // Media
        ModerateHigh, // Moderada-alta
        High,         // Alta
        Extreme       // Extrema
    }

    /// <summary>
    /// Datos de una zona de aterrizaje. Cada zona es un asset (ScriptableObject).
    /// Para crear las 6 zonas de golpe: menú Marte ▸ Crear zonas de aterrizaje por defecto.
    /// Las coordenadas son aproximadas y solo sirven de referencia/ambientación.
    /// </summary>
    [CreateAssetMenu(fileName = "LandingZone", menuName = "Marte/Zona de aterrizaje")]
    public class LandingZoneData : ScriptableObject
    {
        [Header("Identidad")]
        public string zoneId = "zona";
        public string displayName = Tr.T("Zona");
        [TextArea(3, 6)] public string description;
        public ZoneDifficulty difficulty = ZoneDifficulty.Medium;
        [Tooltip("Latitud aproximada (grados, + norte).")]
        public float latitude;
        [Tooltip("Longitud aproximada (grados este, 0–360).")]
        public float longitudeEast;

        [Header("Terreno")]
        public TerrainMapSettings terrain = new TerrainMapSettings();
        [Tooltip("Densidad de rocas para el futuro RockScatterSpawner (0..1).")]
        [Range(0f, 1f)] public float rockDensity = 0.3f;

        [Header("Daño al robot")]
        [Tooltip("Multiplica todo el daño que recibe el robot en esta zona.")]
        [Min(0f)] public float damageMultiplier = 1f;

        [Header("Clima y energía")]
        [Tooltip("Irradiancia al mediodía con cielo despejado (W/m²). Más baja en latitudes altas.")]
        [Min(0f)] public float noonIrradiance = 500f;
        [Tooltip("Fracción de panel que se cubre de polvo por hora con cielo normal.")]
        [Range(0f, 0.05f)] public float dustDepositionPerHour = 0.002f;
        [Tooltip("Probabilidad de que empiece una tormenta de polvo en cada sol.")]
        [Range(0f, 1f)] public float stormChancePerSol = 0.02f;
        [Tooltip("Duración típica de una tormenta (en soles).")]
        [Min(0.05f)] public float stormDurationSols = 1.5f;
        [Tooltip("Fracción de luz que bloquea una tormenta (0..1).")]
        [Range(0f, 1f)] public float stormLightBlock = 0.7f;
        [Tooltip("Multiplicador de deposición de polvo durante una tormenta.")]
        [Min(1f)] public float stormDustMultiplier = 6f;
        [Tooltip("Ráfagas o remolinos de polvo que limpian paneles, por sol.")]
        [Min(0f)] public float cleaningGustsPerSol = 0.3f;
        public float minTemperatureC = -80f;
        public float maxTemperatureC = -10f;

        [Header("Recursos")]
        public bool hasAccessibleWaterIce;
        [Range(0f, 1f)] public float iceAbundance;

        [Header("Aparición del robot")]
        [Tooltip("Posición de aterrizaje dentro del mapa (0..1 en X y Z).")]
        public Vector2 spawnPointNormalized = new Vector2(0.5f, 0.5f);
        [Tooltip("Altura extra al soltar el robot. Más de ~0,3 m provoca daño por caída.")]
        [Min(0f)] public float spawnHeightOffset = 0.3f;

        /// <summary>Posición de aparición en coordenadas locales del terreno (sin altura).</summary>
        public Vector3 GetSpawnLocalXZ()
        {
            return GetSpawnLocalXZ(terrain.WorldSize);
        }

        /// <summary>Igual, pero para un mapa de otro tamaño (p. ej. el de datos reales de la NASA).</summary>
        public Vector3 GetSpawnLocalXZ(float worldSize)
        {
            return new Vector3(spawnPointNormalized.x * worldSize, 0f, spawnPointNormalized.y * worldSize);
        }

        /// <summary>Nombre de la dificultad en español para la UI.</summary>
        public static string DifficultyLabel(ZoneDifficulty d)
        {
            switch (d)
            {
                case ZoneDifficulty.Easy: return Tr.T("Fácil");
                case ZoneDifficulty.Low: return Tr.T("Baja");
                case ZoneDifficulty.Medium: return Tr.T("Media");
                case ZoneDifficulty.ModerateHigh: return Tr.T("Moderada-alta");
                case ZoneDifficulty.High: return Tr.T("Alta");
                default: return Tr.T("Extrema");
            }
        }
    }
}
