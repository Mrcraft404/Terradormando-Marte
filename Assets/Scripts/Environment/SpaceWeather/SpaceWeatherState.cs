using UnityEngine;

namespace MarsRobot.World.SpaceWeather
{
    /// <summary>
    /// Estado actual del clima espacial que leen los demás sistemas (cámara, minimapa, alertas,
    /// motores…). Lo escribe SpaceWeatherManager. Estático para que ningún sistema dependa de
    /// que el gestor exista: sin gestor, todo vale 0/false.
    /// </summary>
    public static class SpaceWeatherState
    {
        /// <summary>Interferencia electromagnética 0..1 (fulguraciones, FLR).</summary>
        public static float Interference01;

        /// <summary>Radar/minimapa bloqueado por la interferencia.</summary>
        public static bool RadarBlocked;

        /// <summary>Tormenta de polvo global en curso (tras el impacto de una CME, en el juego).</summary>
        public static bool GlobalStormActive;

        /// <summary>Horas de juego hasta el impacto de una CME (−1 si no hay ninguna en camino).</summary>
        public static float CmeCountdownHours = -1f;

        /// <summary>Partículas energéticas solares (SEP) en curso y su intensidad 0..1.</summary>
        public static bool SepActive;
        public static float SepIntensity;

        /// <summary>Tormenta geomagnética (GST) en curso y su intensidad 0..1.</summary>
        public static bool GstActive;
        public static float GstIntensity;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Reset()
        {
            Interference01 = 0f;
            RadarBlocked = false;
            GlobalStormActive = false;
            CmeCountdownHours = -1f;
            SepActive = false;
            SepIntensity = 0f;
            GstActive = false;
            GstIntensity = 0f;
        }
    }
}
