using System.Collections.Generic;
using MarsRobot.Visuals;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarsRobot.Base
{
    /// <summary>
    /// Paleta PBR de las capas de los hábitats. Funciona con URP ("Universal Render Pipeline/Lit")
    /// y con el pipeline integrado ("Standard"). Si la fábrica de materiales PBR generó un material
    /// con el mismo nombre (Resources/GeneratedMaterials/Habitat_*), se usa ese en su lugar.
    /// </summary>
    public static class HabitatMaterials
    {
        private static readonly Dictionary<string, Material> Cache = new Dictionary<string, Material>();

        // Capa 0
        public static Material SinteredSlab { get { return Get("Habitat_LosaSinterizada", new Color(0.46f, 0.33f, 0.27f), 0.2f, 0f); } }
        public static Material CompactedSoil { get { return Get("Habitat_SueloCompactado", new Color(0.52f, 0.31f, 0.2f), 0.05f, 0f); } }
        public static Material AnchorSteel { get { return Get("Habitat_Zapata", new Color(0.32f, 0.33f, 0.35f), 0.4f, 0.8f); } }
        // Capa 1
        public static Material Titanium { get { return Get("Habitat_Titanio", new Color(0.62f, 0.63f, 0.66f), 0.55f, 0.9f); } }
        public static Material TensionStrap { get { return Get("Habitat_Cincha", new Color(0.85f, 0.78f, 0.55f), 0.3f, 0f); } }
        // Capa 2 (Kevlar exterior blanco tipo "beta cloth"; interior acolchado claro)
        public static Material KevlarOuter { get { return Get("Habitat_KevlarExterior", new Color(0.92f, 0.91f, 0.87f), 0.35f, 0f); } }
        public static Material InsulationInner { get { return Get("Habitat_AislanteInterior", new Color(0.82f, 0.84f, 0.86f), 0.15f, 0f); } }
        // Capa 3
        public static Material RegolithShield { get { return Get("Habitat_EscudoRegolito", new Color(0.58f, 0.35f, 0.23f), 0.05f, 0f); } }
        // Capa 4
        public static Material RackFrame { get { return Get("Habitat_Estructura", new Color(0.75f, 0.76f, 0.78f), 0.5f, 0.7f); } }
        public static Material Plants { get { return Get("Habitat_Plantas", new Color(0.22f, 0.55f, 0.18f), 0.25f, 0f); } }
        public static Material GrowLight { get { return Get("Habitat_LuzCultivo", new Color(0.9f, 0.5f, 1f), 0.6f, 0f, new Color(0.9f, 0.3f, 1f) * 1.5f); } }
        public static Material Furniture { get { return Get("Habitat_Mobiliario", new Color(0.35f, 0.4f, 0.47f), 0.3f, 0f); } }
        public static Material Fabric { get { return Get("Habitat_Tejido", new Color(0.2f, 0.32f, 0.5f), 0.1f, 0f); } }
        public static Material Tank { get { return Get("Habitat_Tanque", new Color(0.88f, 0.88f, 0.85f), 0.6f, 0.5f); } }
        public static Material Pipe { get { return Get("Habitat_Tuberia", new Color(0.8f, 0.55f, 0.15f), 0.5f, 0.7f); } }
        public static Material Machine { get { return Get("Habitat_Maquina", new Color(0.18f, 0.19f, 0.21f), 0.45f, 0.6f); } }
        public static Material SafetyOrange { get { return Get("Habitat_Naranja", new Color(0.95f, 0.45f, 0.08f), 0.4f, 0f); } }
        public static Material StatusLight { get { return Get("Habitat_LuzEstado", new Color(0.3f, 0.9f, 1f), 0.6f, 0f, new Color(0.2f, 0.8f, 1f) * 1.4f); } }

        // Translúcidos (invernadero acristalado y escudo de hielo)
        public static Material Glass { get { return Get("Habitat_VidrioLaminado", new Color(0.78f, 0.9f, 0.95f, 0.22f), 0.95f, 0f, default(Color), TransparencyMode.Glass); } }
        public static Material WaterIce { get { return Get("Habitat_EscudoHielo", new Color(0.72f, 0.86f, 0.95f, 0.38f), 0.85f, 0f, default(Color), TransparencyMode.Glass); } }

        private enum TransparencyMode
        {
            Opaque,
            Glass, // conserva reflejos (premultiplicado)
            Fade   // todo se desvanece con el alfa (hologramas)
        }

        /// <summary>
        /// Material de holograma de construcción: translúcido, sin sombras y con brillo propio.
        /// Se reutiliza por color (verde/azul = válido, rojo = no válido).
        /// </summary>
        public static Material Ghost(Color color)
        {
            string key = "Holograma_" + ColorUtility.ToHtmlStringRGBA(color);
            Color emission = new Color(color.r, color.g, color.b) * 0.8f;
            return Get(key, color, 0.6f, 0f, emission, TransparencyMode.Fade, false);
        }

        /// <summary>Material de una capa: el override del módulo si lo hay, si no el de la paleta.</summary>
        public static Material Resolve(Material overrideMaterial, Material fallback)
        {
            return overrideMaterial != null ? overrideMaterial : fallback;
        }

        private static Material Get(string name, Color color, float smoothness, float metallic, Color emission = default(Color),
                                    TransparencyMode mode = TransparencyMode.Opaque, bool allowPbrOverride = true)
        {
            Material mat;
            if (Cache.TryGetValue(name, out mat) && mat != null) return mat;

            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            bool urp = shader != null;
            if (!urp) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Diffuse");

            mat = new Material(shader) { name = name };
            if (urp)
            {
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Smoothness", smoothness);
            }
            else
            {
                mat.color = color;
                if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
            }
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", metallic);
            if (emission.maxColorComponent > 0f && mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission);
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            mat.enableInstancing = true;
            if (mode != TransparencyMode.Opaque) MakeTransparent(mat, urp, mode == TransparencyMode.Fade);

            if (allowPbrOverride) mat = PBRMaterials.Override(name, mat);
            Cache[name] = mat;
            return mat;
        }

        /// <summary>Configura transparencia en URP/Lit o en Standard (equivale a cambiar "Surface/Rendering Mode" en el Inspector).</summary>
        private static void MakeTransparent(Material mat, bool urp, bool fade)
        {
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
            if (urp)
            {
                mat.SetFloat("_Surface", 1f);                        // Transparent
                mat.SetFloat("_Blend", fade ? 0f : 1f);               // Alpha / Premultiply
                mat.SetFloat("_SrcBlend", fade ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (!fade) mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            else
            {
                mat.SetFloat("_Mode", fade ? 2f : 3f);                // 2 = Fade, 3 = Transparent
                mat.SetFloat("_SrcBlend", fade ? (float)BlendMode.SrcAlpha : (float)BlendMode.One);
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                mat.DisableKeyword("_ALPHATEST_ON");
                if (fade) { mat.EnableKeyword("_ALPHABLEND_ON"); mat.DisableKeyword("_ALPHAPREMULTIPLY_ON"); }
                else { mat.DisableKeyword("_ALPHABLEND_ON"); mat.EnableKeyword("_ALPHAPREMULTIPLY_ON"); }
            }
        }
    }
}
