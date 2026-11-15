#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Fábrica de materiales PBR para el render integrado (shader Standard, flujo metálico).
    /// Lee las texturas de una carpeta (por defecto Assets/Textures/Generated), las agrupa por nombre
    /// (&lt;Juego&gt;_&lt;Canal&gt;) y crea o actualiza un material por juego en
    /// Assets/Resources/GeneratedMaterials/&lt;Juego&gt;.mat (así el juego puede cargarlos en tiempo real).
    ///
    /// Canales reconocidos (sin distinguir mayúsculas):
    ///  • Albedo:   _Albedo, _BaseColor, _BaseMap, _Diffuse, _Color   → _MainTex (+ transparencia si el nombre lleva "_Transparent")
    ///  • Normal:   _Normal, _Nrm, _Bump                                → _BumpMap (importado como Normal map, _BumpScale = 1)
    ///  • Metálico: _MetallicSmoothness, _Metallic, _MetalSmooth        → _MetallicGlossMap (metálico en R, suavidad en A)
    ///  • Rugosidad: _Roughness, _Rough (si no hay suavidad en el alfa)  → se empaqueta como 1 − rugosidad en el alfa
    ///  • Oclusión: _AO, _Occlusion, _AmbientOcclusion                  → _OcclusionMap (_OcclusionStrength = 1)
    /// Sin mapa metálico: _Metallic = 0,85 si el nombre indica metal (Metal, Steel, Acero, Iron…) o 0
    /// si no, y _Glossiness = 0,35 (superficie desgastada por el polvo).
    /// </summary>
    public static class PBRMaterialFactory
    {
        public const string OutputFolder = "Assets/Resources/GeneratedMaterials";

        private sealed class TextureSet
        {
            public string Name;
            public Texture2D Albedo, Normal, Metallic, Roughness, Occlusion;
            public string MetallicPath, RoughnessPath;
        }

        [MenuItem("Marte/Materiales PBR/Crear materiales desde texturas")]
        public static void BuildDefault()
        {
            BuildFromFolder(PBRTextureGenerator.Folder);
        }

        [MenuItem("Marte/Materiales PBR/Crear materiales desde otra carpeta…")]
        public static void BuildFromPickedFolder()
        {
            string abs = EditorUtility.OpenFolderPanel("Carpeta con texturas PBR", Application.dataPath, "");
            if (string.IsNullOrEmpty(abs)) return;
            string rel = ToAssetPath(abs);
            if (rel == null)
            {
                Debug.LogWarning("[PBR] La carpeta debe estar dentro de Assets/: " + abs);
                return;
            }
            BuildFromFolder(rel);
        }

        /// <summary>Genera las texturas procedurales y crea sus materiales (para la línea de comandos).</summary>
        public static void GenerateTexturesAndMaterials()
        {
            PBRTextureGenerator.GenerateAll();
            BuildDefault();
        }

        public static int BuildFromFolder(string folder)
        {
            if (string.IsNullOrEmpty(folder) || !AssetDatabase.IsValidFolder(folder))
            {
                Debug.LogWarning("[PBR] No existe la carpeta de texturas: " + folder);
                return 0;
            }
            Shader standard = Shader.Find("Standard");
            if (standard == null)
            {
                Debug.LogWarning("[PBR] No se encuentra el shader Standard.");
                return 0;
            }
            EnsureFolder(OutputFolder);

            var sets = new Dictionary<string, TextureSet>();
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string file = Path.GetFileNameWithoutExtension(path);
                string channel;
                string setName = SplitChannel(file, out channel);
                if (setName == null)
                {
                    Debug.LogWarning("[PBR] Textura sin canal reconocible (se ignora): " + path);
                    continue;
                }
                TextureSet set;
                if (!sets.TryGetValue(setName, out set)) sets[setName] = set = new TextureSet { Name = setName };
                ConfigureImporter(path, channel);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                switch (channel)
                {
                    case "albedo": set.Albedo = tex; break;
                    case "normal": set.Normal = tex; break;
                    case "metallic": set.Metallic = tex; set.MetallicPath = path; break;
                    case "roughness": set.Roughness = tex; set.RoughnessPath = path; break;
                    default: set.Occlusion = tex; break;
                }
            }

            int built = 0;
            foreach (TextureSet set in sets.Values)
            {
                if (set.Albedo == null)
                {
                    Debug.LogWarning("[PBR] El juego \"" + set.Name + "\" no tiene albedo: no se crea material.");
                    continue;
                }
                if (BuildMaterial(set, standard)) built++;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PBR] Materiales creados o actualizados: " + built + " en " + OutputFolder);
            return built;
        }

        // ------------------------------------------------------------------

        private static bool BuildMaterial(TextureSet set, Shader standard)
        {
            string path = OutputFolder + "/" + set.Name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = mat == null;
            if (created) mat = new Material(standard) { name = set.Name };
            else mat.shader = standard;

            // Albedo.
            mat.SetTexture("_MainTex", set.Albedo);
            mat.SetColor("_Color", Color.white);
            if (set.Name.ToLowerInvariant().Contains("_transparent")) MakeTransparent(mat);

            // Normal.
            if (set.Normal != null)
            {
                mat.SetTexture("_BumpMap", set.Normal);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            else
            {
                mat.SetTexture("_BumpMap", null);
                mat.DisableKeyword("_NORMALMAP");
                Debug.LogWarning("[PBR] " + set.Name + ": sin mapa de normales.");
            }

            // Metálico / suavidad.
            Texture2D metallic = set.Metallic;
            if (metallic != null && set.Roughness != null && !HasUsefulAlpha(set.MetallicPath))
                metallic = PackMetallicRoughness(set);
            else if (metallic == null && set.Roughness != null)
                metallic = PackMetallicRoughness(set);

            if (metallic != null)
            {
                mat.SetTexture("_MetallicGlossMap", metallic);
                mat.EnableKeyword("_METALLICGLOSSMAP");
                mat.SetFloat("_GlossMapScale", 1f);
                mat.SetFloat("_SmoothnessTextureChannel", 0f); // suavidad en el alfa del mapa metálico
            }
            else
            {
                mat.SetTexture("_MetallicGlossMap", null);
                mat.DisableKeyword("_METALLICGLOSSMAP");
                mat.SetFloat("_Metallic", LooksMetallic(set.Name) ? 0.85f : 0f);
                mat.SetFloat("_Glossiness", 0.35f);
                Debug.LogWarning("[PBR] " + set.Name + ": sin mapa metálico; se usan valores base.");
            }

            // Oclusión.
            if (set.Occlusion != null)
            {
                mat.SetTexture("_OcclusionMap", set.Occlusion);
                mat.SetFloat("_OcclusionStrength", 1f);
            }
            else Debug.LogWarning("[PBR] " + set.Name + ": sin mapa de oclusión.");

            if (created) AssetDatabase.CreateAsset(mat, path);
            else EditorUtility.SetDirty(mat);
            return true;
        }

        /// <summary>Crea &lt;Juego&gt;_MetallicSmoothness_Packed.png: metálico en RGB y (1 − rugosidad) en alfa.</summary>
        private static Texture2D PackMetallicRoughness(TextureSet set)
        {
            Texture2D m = set.Metallic != null ? Readable(set.MetallicPath) : null;
            Texture2D r = Readable(set.RoughnessPath);
            if (r == null)
            {
                Debug.LogWarning("[PBR] " + set.Name + ": no se pudo leer la rugosidad.");
                return set.Metallic;
            }
            int w = r.width, h = r.height;
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w, v = (y + 0.5f) / h;
                float metal = m != null ? m.GetPixelBilinear(u, v).r : (LooksMetallic(set.Name) ? 0.85f : 0f);
                float smooth = 1f - r.GetPixelBilinear(u, v).r;
                px[y * w + x] = new Color(metal, metal, metal, smooth);
            }
            var packed = new Texture2D(w, h, TextureFormat.RGBA32, false);
            packed.SetPixels(px);
            packed.Apply();
            string dir = Path.GetDirectoryName(set.RoughnessPath).Replace('\\', '/');
            string path = dir + "/" + set.Name + "_MetallicSmoothness_Packed.png";
            File.WriteAllBytes(path, packed.EncodeToPNG());
            Object.DestroyImmediate(packed);
            AssetDatabase.ImportAsset(path);
            ConfigureImporter(path, "metallic");
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D Readable(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti != null && !ti.isReadable)
            {
                ti.isReadable = true;
                ti.SaveAndReimport();
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static bool HasUsefulAlpha(string path)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            return ti != null && ti.DoesSourceTextureHaveAlpha();
        }

        private static void ConfigureImporter(string path, string channel)
        {
            var ti = AssetImporter.GetAtPath(path) as TextureImporter;
            if (ti == null)
            {
                Debug.LogWarning("[PBR] No es una textura importable: " + path);
                return;
            }
            bool changed = false;
            TextureImporterType type = channel == "normal" ? TextureImporterType.NormalMap : TextureImporterType.Default;
            bool srgb = channel == "albedo";
            if (ti.textureType != type) { ti.textureType = type; changed = true; }
            if (ti.sRGBTexture != srgb && type == TextureImporterType.Default) { ti.sRGBTexture = srgb; changed = true; }
            if (ti.wrapMode != TextureWrapMode.Repeat) { ti.wrapMode = TextureWrapMode.Repeat; changed = true; }
            if (ti.mipmapEnabled != true) { ti.mipmapEnabled = true; changed = true; }
            if (ti.anisoLevel < 4) { ti.anisoLevel = 4; changed = true; }
            if (channel == "metallic" && ti.alphaSource != TextureImporterAlphaSource.FromInput) { ti.alphaSource = TextureImporterAlphaSource.FromInput; changed = true; }
            if (ti.maxTextureSize > 2048) { ti.maxTextureSize = 2048; changed = true; }
            if (changed) ti.SaveAndReimport();
        }

        private static string SplitChannel(string file, out string channel)
        {
            string lower = file.ToLowerInvariant();
            string[][] map =
            {
                new[] { "albedo", "_albedo", "_basecolor", "_basemap", "_diffuse", "_color" },
                new[] { "normal", "_normal", "_nrm", "_bump" },
                new[] { "metallic", "_metallicsmoothness_packed", "_metallicsmoothness", "_metalsmooth", "_metallic" },
                new[] { "roughness", "_roughness", "_rough" },
                new[] { "occlusion", "_ambientocclusion", "_occlusion", "_ao" },
            };
            foreach (string[] entry in map)
            {
                for (int i = 1; i < entry.Length; i++)
                {
                    if (!lower.EndsWith(entry[i])) continue;
                    channel = entry[0];
                    return file.Substring(0, file.Length - entry[i].Length);
                }
            }
            channel = null;
            return null;
        }

        private static bool LooksMetallic(string name)
        {
            string n = name.ToLowerInvariant();
            return n.Contains("metal") || n.Contains("steel") || n.Contains("acero") || n.Contains("iron") || n.Contains("hierro") || n.Contains("alumin");
        }

        private static void MakeTransparent(Material m)
        {
            m.SetFloat("_Mode", 3f);
            m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetInt("_ZWrite", 0);
            m.DisableKeyword("_ALPHATEST_ON");
            m.DisableKeyword("_ALPHABLEND_ON");
            m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }

        private static string ToAssetPath(string absolute)
        {
            string data = Application.dataPath.Replace('\\', '/');
            absolute = absolute.Replace('\\', '/');
            if (!absolute.StartsWith(data)) return null;
            return "Assets" + absolute.Substring(data.Length);
        }
    }
}
#endif
