using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Imágenes 2D (pintadas por capas) de todo lo que se puede construir.
    /// Están en Assets/Resources/StructureArt/&lt;clave&gt;.png y se generan con
    /// Tools/StructureArt/generar_imagenes.py. Claves: pieza_&lt;id&gt;, modulo_&lt;id&gt;, equipo_&lt;id&gt;.
    /// Si falta una imagen, el menú usa la foto 3D de siempre (PieceThumbnails).
    /// </summary>
    public static class StructureArt
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Imagen por clave de imagen (p. ej. "equipo_solar"), o null.</summary>
        public static Texture2D Get(string artKey)
        {
            if (string.IsNullOrEmpty(artKey)) return null;
            Texture2D t;
            if (!Cache.TryGetValue(artKey, out t))
            {
                t = Resources.Load<Texture2D>("StructureArt/" + artKey);
                Cache[artKey] = t;
            }
            return t;
        }

        /// <summary>Imagen para una clave del menú de construcción ("pieza:x", "modulo:x", "equipo:x").</summary>
        public static Texture2D ForMenuKey(string menuKey)
        {
            return string.IsNullOrEmpty(menuKey) ? null : Get(menuKey.Replace(':', '_'));
        }

        public static Texture2D ForEquipment(string id)
        {
            return Get("equipo_" + id);
        }

        public static Texture2D ForPiece(string id)
        {
            return Get("pieza_" + id);
        }

        public static Texture2D ForModule(string id)
        {
            return Get("modulo_" + id);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Cache.Clear();
        }
    }
}
