using System;
using UnityEngine;

namespace MarsRobot.Core
{
    /// <summary>Modo de juego elegido en el menú principal.</summary>
    public enum GameMode
    {
        Creative,  // Creativo: sin daño, energía y recursos ilimitados, sin tormentas
        Survival   // Supervivencia: el juego normal, con la dificultad elegida
    }

    /// <summary>Dificultad general de la partida (solo en Supervivencia).</summary>
    public enum WorldDifficulty
    {
        Easy,
        Normal,
        Hard
    }

    /// <summary>
    /// Datos del mundo creado en el menú: nombre, semilla, modo, dificultad y punto de aparición
    /// elegido en el mapa de Marte. Pasa de la escena del menú a la escena de juego.
    /// </summary>
    [Serializable]
    public class WorldSetup
    {
        public string worldName = Tr.T("Nueva colonia");
        public int seed = 1234;
        public GameMode mode = GameMode.Survival;
        public WorldDifficulty difficulty = WorldDifficulty.Normal;

        [Header("Punto de aparición")]
        public bool hasSpawn;
        public float spawnLatitude;
        [Tooltip("Longitud este, 0–360 (misma convención que LandingZoneData).")]
        public float spawnLongitudeEast;
        public string spawnRegion = "";
        [Tooltip("Zona predefinida (con datos NASA) si el punto cae dentro de una; vacío si es un punto libre.")]
        public string presetZoneId = "";

        public string createdAt = "";

        public string ModeLabel
        {
            get { return mode == GameMode.Creative ? Tr.T("Creativo") : Tr.T("Supervivencia"); }
        }

        public string DifficultyLabel
        {
            get
            {
                switch (difficulty)
                {
                    case WorldDifficulty.Easy: return Tr.T("Fácil");
                    case WorldDifficulty.Hard: return Tr.T("Difícil");
                    default: return "Normal";
                }
            }
        }

        /// <summary>Multiplicador para daño, tormentas y polvo según la dificultad.</summary>
        public float HazardFactor
        {
            get
            {
                switch (difficulty)
                {
                    case WorldDifficulty.Easy: return 0.6f;
                    case WorldDifficulty.Hard: return 1.5f;
                    default: return 1f;
                }
            }
        }
    }

    /// <summary>Partida activa (null si se entra a la escena de juego sin pasar por el menú).</summary>
    public static class GameModeState
    {
        private const string LastWorldKey = "tm_last_world";

        public static WorldSetup Current { get; set; }

        public static bool IsCreative
        {
            get { return Current != null && Current.mode == GameMode.Creative; }
        }

        public static void SaveLast(WorldSetup setup)
        {
            if (setup == null) return;
            PlayerPrefs.SetString(LastWorldKey, JsonUtility.ToJson(setup));
            PlayerPrefs.Save();
        }

        public static WorldSetup LoadLast()
        {
            string json = PlayerPrefs.GetString(LastWorldKey, "");
            if (string.IsNullOrEmpty(json)) return null;
            try { return JsonUtility.FromJson<WorldSetup>(json); }
            catch (Exception) { return null; }
        }
    }
}
