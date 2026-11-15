using MarsRobot.Core;
using MarsRobot.World.GlobalMap;
using UnityEngine;

namespace MarsRobot.Spawns
{
    /// <summary>
    /// Convierte el punto de aparición elegido en el mapa global en una zona de aterrizaje jugable.
    /// - Si el punto cae en una de las zonas predefinidas, usa una copia de esa zona (con sus datos
    ///   reales de la NASA si existen en StreamingAssets/MarsMap).
    /// - Si es un punto libre, copia la zona del catálogo con el clima más parecido y ajusta la
    ///   latitud, la luz, la temperatura, el hielo y el relieve procedural a ese punto.
    /// Después aplica el modo (Creativo / Supervivencia) y la dificultad.
    /// Siempre trabaja sobre copias: nunca modifica los assets.
    /// </summary>
    public static class SpawnZoneFactory
    {
        /// <summary>Distancia (grados) para considerar que un clic está en una zona predefinida.</summary>
        public const float PresetSnapDegrees = 4f;

        /// <summary>Zona predefinida del catálogo cercana al punto (null si no hay).</summary>
        public static LandingZoneData FindPreset(LandingZoneCatalog catalog, float lat, float lonSigned)
        {
            if (catalog == null) return null;
            LandingZoneData best = null;
            float bestD = PresetSnapDegrees;
            for (int i = 0; i < catalog.zones.Count; i++)
            {
                LandingZoneData z = catalog.zones[i];
                if (z == null) continue;
                float d = MarsGlobalMap.AngularDistance(lat, lonSigned, z.latitude, MarsGlobalMap.ToSigned(z.longitudeEast));
                if (d < bestD)
                {
                    bestD = d;
                    best = z;
                }
            }
            return best;
        }

        public static LandingZoneData Build(LandingZoneCatalog catalog, WorldSetup setup)
        {
            LandingZoneData preset = catalog != null && !string.IsNullOrEmpty(setup.presetZoneId) ? catalog.FindById(setup.presetZoneId) : null;
            LandingZoneData zone;

            if (preset != null)
            {
                zone = Object.Instantiate(preset);
                zone.name = preset.name;
                zone.terrain = preset.terrain.Clone();
                zone.terrain.seed = setup.seed;
            }
            else
            {
                zone = BuildCustom(catalog, setup);
            }

            ApplyMode(zone, setup);
            return zone;
        }

        private static LandingZoneData BuildCustom(LandingZoneCatalog catalog, WorldSetup setup)
        {
            float lat = setup.spawnLatitude;
            float lon = MarsGlobalMap.ToSigned(setup.spawnLongitudeEast);
            float absLat = Mathf.Abs(lat);

            // Plantilla: la zona del catálogo con la latitud más parecida (clima y polvo similares).
            LandingZoneData template = null;
            if (catalog != null)
            {
                float bestD = float.MaxValue;
                for (int i = 0; i < catalog.zones.Count; i++)
                {
                    LandingZoneData z = catalog.zones[i];
                    if (z == null) continue;
                    float d = Mathf.Abs(Mathf.Abs(z.latitude) - absLat);
                    if (d < bestD)
                    {
                        bestD = d;
                        template = z;
                    }
                }
            }

            LandingZoneData zone = template != null ? Object.Instantiate(template) : ScriptableObject.CreateInstance<LandingZoneData>();
            zone.terrain = template != null ? template.terrain.Clone() : new World.TerrainSystem.TerrainMapSettings();

            MarsSiteInfo site = MarsGlobalMap.Describe(lat, lon);

            // Identificador propio: no hay datos NASA para este punto, así que el terreno es procedural.
            zone.zoneId = "punto_" + Mathf.RoundToInt((lat + 90f) * 10f) + "_" + Mathf.RoundToInt(setup.spawnLongitudeEast * 10f);
            zone.name = "Zona_" + zone.zoneId;
            zone.displayName = string.IsNullOrEmpty(setup.spawnRegion) ? site.Region : setup.spawnRegion;
            zone.description = site.Description;
            zone.latitude = lat;
            zone.longitudeEast = setup.spawnLongitudeEast;
            zone.difficulty = (ZoneDifficulty)Mathf.Clamp(site.DifficultyIndex, 0, 5);

            // Relieve procedural según el mapa global.
            zone.terrain.heightmap = null;
            zone.terrain.seed = setup.seed ^ (Mathf.RoundToInt(lat * 100f) * 73856093) ^ (Mathf.RoundToInt(lon * 100f) * 19349663);
            zone.terrain.roughness = Mathf.Clamp01(0.2f + site.Roughness01 * 0.7f);
            zone.terrain.craterCount = Mathf.Clamp(Mathf.RoundToInt((lat < 10f ? 30f : 12f) + site.Roughness01 * 25f), 0, 80);
            zone.terrain.heightScale = Mathf.Lerp(90f, 360f, site.Roughness01);
            zone.rockDensity = Mathf.Clamp01(0.2f + site.Roughness01 * 0.5f);
            zone.damageMultiplier = 0.8f + 0.12f * site.DifficultyIndex;

            // Clima y recursos.
            zone.noonIrradiance = Mathf.Max(60f, 590f * 0.9f * site.Sunlight01);
            zone.minTemperatureC = site.MinTempC;
            zone.maxTemperatureC = site.MaxTempC;
            zone.iceAbundance = site.Ice01;
            zone.hasAccessibleWaterIce = site.Ice01 >= 0.35f;
            return zone;
        }

        private static void ApplyMode(LandingZoneData zone, WorldSetup setup)
        {
            if (setup.mode == GameMode.Creative)
            {
                // Creativo: sin daño ni tormentas, polvo mínimo.
                zone.damageMultiplier = 0f;
                zone.stormChancePerSol = 0f;
                zone.dustDepositionPerHour *= 0.1f;
                return;
            }

            float f = setup.HazardFactor;
            zone.damageMultiplier *= f;
            zone.stormChancePerSol = Mathf.Clamp01(zone.stormChancePerSol * f);
            zone.dustDepositionPerHour = Mathf.Min(0.05f, zone.dustDepositionPerHour * f);
        }
    }
}
