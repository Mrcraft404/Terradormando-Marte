using UnityEngine;

namespace MarsRobot.World.TerrainSystem
{
    /// <summary>Tipos de recurso que salen de los datos CRISM (y del hielo de SHARAD/latitud).</summary>
    public enum MineralType
    {
        Clays,          // arcillas (filosilicatos): agua antigua, útiles para suelo del huerto
        Sulfates,       // sulfatos: agua ligada, yeso
        IronOxides,     // óxidos de hierro (hematites): metal
        SubsurfaceIce   // hielo enterrado: agua y oxígeno
    }

    /// <summary>
    /// Veta extraíble colocada por <see cref="MarsTerrainGenerator"/>. Por ahora solo guarda
    /// qué hay y cuánto; la extracción se implementará con las herramientas del robot.
    /// Tiene un trigger para que el robot la detecte al acercarse.
    /// </summary>
    public sealed class MineralDeposit : MonoBehaviour
    {
        public MineralType mineral;

        [Range(0f, 1f)] public float richness;

        [Tooltip("Unidades que quedan por extraer.")]
        public float remainingUnits;

        public static MineralType Parse(string key)
        {
            switch (key)
            {
                case "clays": return MineralType.Clays;
                case "sulfates": return MineralType.Sulfates;
                case "subsurfaceIce": return MineralType.SubsurfaceIce;
                default: return MineralType.IronOxides;
            }
        }

        public static string DisplayName(MineralType type)
        {
            switch (type)
            {
                case MineralType.Clays: return "Arcillas";
                case MineralType.Sulfates: return "Sulfatos";
                case MineralType.SubsurfaceIce: return "Hielo";
                default: return "Óxidos de hierro";
            }
        }

        /// <summary>Extrae hasta <paramref name="units"/> y devuelve lo extraído.</summary>
        public float Extract(float units)
        {
            float taken = Mathf.Min(units, remainingUnits);
            remainingUnits -= taken;
            return taken;
        }
    }
}
