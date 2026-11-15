using UnityEngine;

namespace MarsRobot.Visuals
{
    /// <summary>
    /// Materiales PBR generados por la fábrica de materiales (Marte ▸ Materiales PBR ▸ Crear
    /// materiales desde texturas), guardados en Assets/Resources/GeneratedMaterials/&lt;nombre&gt;.mat.
    /// Si existe uno con el nombre pedido, sustituye al material procedural; si no, se usa el de siempre.
    /// </summary>
    public static class PBRMaterials
    {
        public const string Folder = "GeneratedMaterials";

        /// <summary>
        /// Devuelve una copia del material generado <paramref name="name"/> con la escala de UV del
        /// material procedural (cada malla tiene sus UV pensadas para esa escala), o
        /// <paramref name="fallback"/> si no hay material generado.
        /// </summary>
        public static Material Override(string name, Material fallback)
        {
            Material generated = Resources.Load<Material>(Folder + "/" + name);
            if (generated == null) return fallback;
            var m = new Material(generated) { name = fallback != null ? fallback.name : name };
            if (fallback != null && fallback.HasProperty("_MainTex") && m.HasProperty("_MainTex"))
                m.mainTextureScale = fallback.mainTextureScale == Vector2.zero ? Vector2.one : fallback.mainTextureScale;
            return m;
        }
    }
}
