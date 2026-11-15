using UnityEngine;

namespace MarsRobot.Visuals
{
    /// <summary>
    /// "Set dressing" orgánico: al colocar atrezo se introducen variaciones aleatorias pequeñas y
    /// controladas para que nada quede perfectamente alineado ni simétrico:
    ///  • giro en Y entre −15° y +15°,
    ///  • escala uniforme ±8 %,
    ///  • desplazamiento milimétrico (±4 mm por defecto) en X/Z.
    /// Usa un generador con semilla: el mismo mundo se ve siempre igual.
    /// </summary>
    public static class SetDressing
    {
        public const float MaxYawDegrees = 15f;
        public const float MaxScaleVariation = 0.08f;
        public const float MaxOffsetMeters = 0.004f;

        private static float Range(System.Random rng, float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        /// <summary>Aplica la variación a un objeto ya colocado (local a su padre).</summary>
        public static void Jitter(Transform t, System.Random rng, float maxYaw = MaxYawDegrees, float maxScale = MaxScaleVariation, float maxOffset = MaxOffsetMeters)
        {
            if (t == null || rng == null)
            {
                Debug.LogWarning("[SetDressing] Jitter sin objeto o sin generador aleatorio.");
                return;
            }
            t.localRotation = t.localRotation * Quaternion.Euler(0f, Range(rng, -maxYaw, maxYaw), 0f);
            t.localScale = t.localScale * (1f + Range(rng, -maxScale, maxScale));
            t.localPosition += new Vector3(Range(rng, -maxOffset, maxOffset), 0f, Range(rng, -maxOffset, maxOffset));
        }

        /// <summary>
        /// Coloca un objeto sobre una superficie (posición local del padre, apoyado por su base) y le
        /// aplica la variación orgánica.
        /// </summary>
        public static Transform Place(GameObject prop, Transform parent, Vector3 localPos, float baseYaw, System.Random rng)
        {
            if (prop == null || parent == null)
            {
                Debug.LogWarning("[SetDressing] Place sin objeto o sin padre.");
                return null;
            }
            Transform t = prop.transform;
            t.SetParent(parent, false);
            t.localPosition = localPos;
            t.localRotation = Quaternion.Euler(0f, baseYaw, 0f);
            Jitter(t, rng);
            return t;
        }
    }
}
