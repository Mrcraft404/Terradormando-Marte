#if UNITY_EDITOR
using MarsRobot.Spawns;
using UnityEditor;
using UnityEngine;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Crea los assets de las 6 zonas de aterrizaje y el catálogo con valores iniciales.
    /// Menú: Marte ▸ Crear zonas de aterrizaje por defecto.
    /// (Los .asset no se pueden escribir a mano sin Unity porque dependen del GUID del script.)
    /// Si un asset ya existe, NO se sobrescribe: así no se pierden ajustes hechos a mano.
    /// </summary>
    public static class LandingZonePresetsCreator
    {
        private const string Folder = "Assets/ScriptableObjects/LandingZones";

        [MenuItem("Marte/Crear zonas de aterrizaje por defecto")]
        public static void CreateAll()
        {
            EnsureFolder();
            var catalog = LoadOrCreate<LandingZoneCatalog>(Folder + "/LandingZoneCatalog.asset");
            catalog.zones.Clear();

            // Fácil: llanura volcánica muy lisa cerca del ecuador.
            catalog.zones.Add(Zone("elysium_planitia", "Elysium Planitia", ZoneDifficulty.Easy, 4.5f, 135.6f,
                "Llanura volcánica muy lisa cerca del ecuador. Mucho sol, pocas rocas y clima estable: ideal para aprender.",
                seed: 101, heightScale: 60f, roughness: 0.15f, craters: 12, rocks: 0.15f, damage: 0.7f,
                irradiance: 560f, dust: 0.0015f, storms: 0.01f, ice: false, iceAbundance: 0f, tMin: -95f, tMax: -5f));

            // Baja, con tormentas: llanura del norte con tormentas de polvo frecuentes.
            catalog.zones.Add(Zone("acidalia_planitia", "Acidalia Planitia", ZoneDifficulty.Low, 46.7f, 337.8f,
                "Gran llanura del hemisferio norte. El terreno es amable, pero las tormentas de polvo son frecuentes y cubren los paneles.",
                seed: 202, heightScale: 70f, roughness: 0.25f, craters: 18, rocks: 0.25f, damage: 0.85f,
                irradiance: 470f, dust: 0.0025f, storms: 0.08f, ice: false, iceAbundance: 0.15f, tMin: -105f, tMax: -20f));

            // Media: cráter con antiguo delta fluvial.
            catalog.zones.Add(Zone("jezero", "Cráter Jezero", ZoneDifficulty.Medium, 18.4f, 77.5f,
                "Cráter con un antiguo delta de río. Sedimentos interesantes, bordes del cráter con rocas y desniveles moderados.",
                seed: 303, heightScale: 140f, roughness: 0.4f, craters: 25, rocks: 0.4f, damage: 1f,
                irradiance: 530f, dust: 0.002f, storms: 0.02f, ice: false, iceAbundance: 0.05f, tMin: -90f, tMax: -10f));

            // Moderada-alta: cráter grande con una montaña central.
            catalog.zones.Add(Zone("gale", "Cráter Gale", ZoneDifficulty.ModerateHigh, -5.4f, 137.8f,
                "Cráter enorme con una montaña de capas en el centro. Pendientes largas, arena traicionera y rocas afiladas.",
                seed: 404, heightScale: 320f, roughness: 0.6f, craters: 20, rocks: 0.55f, damage: 1.2f,
                irradiance: 550f, dust: 0.002f, storms: 0.03f, ice: false, iceAbundance: 0.05f, tMin: -85f, tMax: 0f));

            // Alta, con hielo: llanura helada del norte, fría y con menos sol.
            catalog.zones.Add(Zone("utopia_planitia", "Utopia Planitia", ZoneDifficulty.High, 48f, 134f,
                "Llanura del norte con hielo de agua bajo la superficie. Recurso valioso, pero hace mucho frío y hay menos sol.",
                seed: 505, heightScale: 90f, roughness: 0.5f, craters: 30, rocks: 0.6f, damage: 1.3f,
                irradiance: 420f, dust: 0.002f, storms: 0.03f, ice: true, iceAbundance: 0.85f, tMin: -110f, tMax: -30f));

            // Extrema: sistema de cañones gigantes.
            catalog.zones.Add(Zone("valles_marineris", "Valles Marineris", ZoneDifficulty.Extreme, -13.9f, 300.8f,
                "Sistema de cañones de miles de kilómetros y varios de profundidad. Paredes casi verticales, desprendimientos y vientos encajonados.",
                seed: 606, heightScale: 900f, roughness: 0.95f, craters: 8, rocks: 0.85f, damage: 1.6f,
                irradiance: 540f, dust: 0.0025f, storms: 0.04f, ice: false, iceAbundance: 0.1f, tMin: -90f, tMax: 5f));

            EditorUtility.SetDirty(catalog);
            AssetDatabase.SaveAssets();
            Selection.activeObject = catalog;
            Debug.Log("[Marte] Zonas de aterrizaje listas en " + Folder);
        }

        private static LandingZoneData Zone(string id, string name, ZoneDifficulty difficulty, float lat, float lonEast,
            string description, int seed, float heightScale, float roughness, int craters, float rocks, float damage,
            float irradiance, float dust, float storms, bool ice, float iceAbundance, float tMin, float tMax)
        {
            string path = Folder + "/Zone_" + id + ".asset";
            bool existed = AssetDatabase.LoadAssetAtPath<LandingZoneData>(path) != null;
            var z = LoadOrCreate<LandingZoneData>(path);
            if (existed) return z; // respetar ajustes manuales

            z.zoneId = id;
            z.displayName = name;
            z.description = description;
            z.difficulty = difficulty;
            z.latitude = lat;
            z.longitudeEast = lonEast;

            z.terrain.seed = seed;
            z.terrain.heightScale = heightScale;
            z.terrain.roughness = roughness;
            z.terrain.craterCount = craters;
            z.rockDensity = rocks;
            z.damageMultiplier = damage;

            z.noonIrradiance = irradiance;
            z.dustDepositionPerHour = dust;
            z.stormChancePerSol = storms;
            z.hasAccessibleWaterIce = ice;
            z.iceAbundance = iceAbundance;
            z.minTemperatureC = tMin;
            z.maxTemperatureC = tMax;

            EditorUtility.SetDirty(z);
            return z;
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "LandingZones");
        }
    }
}
#endif
