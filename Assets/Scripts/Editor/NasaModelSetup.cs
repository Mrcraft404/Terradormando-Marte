#if UNITY_EDITOR
using System.IO;
using System.Text;
using MarsRobot.Visuals;
using UnityEditor;
using UnityEngine;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Convierte los modelos de la NASA importados (.glb con glTFast) en prefabs listos para el juego.
    ///
    /// Uso:
    /// 1. Descarga el "glTF File (3D Model)" de cada modelo en https://science.nasa.gov/3d-resources/
    /// 2. Ponlo en Assets/Models/NASA/&lt;Id&gt;/ (Id = CuriosityRover, InSightLander o Ingenuity).
    /// 3. Menú Marte ▸ Configurar modelos NASA.
    ///
    /// Por cada modelo: lo escala a su tamaño real, lo gira para que mire a +Z, lo apoya en y = 0
    /// con el centro en el origen, quita colisionadores y guarda Assets/Resources/NASAModels/&lt;Id&gt;.prefab.
    /// Los materiales son los PBR de glTFast (metálico + rugosidad + normal), que ya interpretan bien
    /// las texturas glTF; no hace falta convertirlos a Standard ni invertir la rugosidad a mano.
    /// </summary>
    public static class NasaModelSetup
    {
        private const string ModelsRoot = "Assets/Models/NASA";
        private const string PrefabRoot = "Assets/Resources/" + NasaModelLibrary.ResourcesFolder;
        private const int MaxTextureSize = 2048;

        private struct Entry
        {
            public string Id;
            public float TargetSize;   // metros
            public bool ByHeight;      // true: TargetSize es la altura; false: la medida horizontal más larga
            public Vector3 Euler;      // giro extra (se suma al que ya trae el archivo) para quedar de pie mirando a +Z
            public Entry(string id, float size, bool byHeight, Vector3 euler)
            {
                Id = id;
                TargetSize = size;
                ByHeight = byHeight;
                Euler = euler;
            }
        }

        // Tamaños reales aproximados: Curiosity 2,2 m de alto (hasta la cabeza del mástil);
        // InSight 6,0 m con los paneles abiertos; Ingenuity 1,2 m de diámetro de rotor.
        private static readonly Entry[] Entries =
        {
            new Entry(NasaModelLibrary.Rover, 2.2f, true, Vector3.zero),
            new Entry(NasaModelLibrary.Lander, 6.0f, false, Vector3.zero),
            new Entry(NasaModelLibrary.Helicopter, 1.2f, false, Vector3.zero),
        };

        /// <summary>Para línea de comandos (-executeMethod): configurar, vistas previas y jerarquía.</summary>
        public static void BatchSetupAndPreview()
        {
            SetupAll();
            RenderPreviews();
            DumpHierarchy();
        }

        [MenuItem("Marte/Configurar modelos NASA")]
        public static void SetupAll()
        {
            EnsureFolder(PrefabRoot);
            var report = new StringBuilder("[Modelos NASA]\n");
            int done = 0;
            foreach (Entry e in Entries)
            {
                string source = FindModel(e.Id);
                if (source == null)
                {
                    report.AppendLine("  " + e.Id + ": no hay modelo en " + ModelsRoot + "/" + e.Id + "/ (se usa el procedural)");
                    continue;
                }
                LimitTextures(source, report);
                if (BuildPrefab(e, source, report)) done++;
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            report.AppendLine("  Prefabs creados: " + done);
            Debug.Log(report.ToString());
        }

        private static string FindModel(string id)
        {
            string folder = ModelsRoot + "/" + id;
            if (!AssetDatabase.IsValidFolder(folder)) return null;
            foreach (string guid in AssetDatabase.FindAssets("t:GameObject", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string ext = Path.GetExtension(path).ToLowerInvariant();
                if (ext == ".glb" || ext == ".gltf" || ext == ".fbx") return path;
            }
            return null;
        }

        private static bool BuildPrefab(Entry e, string sourcePath, StringBuilder report)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
            if (model == null)
            {
                report.AppendLine("  " + e.Id + ": Unity no pudo importar " + sourcePath + " (¿está instalado glTFast?)");
                return false;
            }

            var root = new GameObject(e.Id);
            try
            {
                var inner = (GameObject)PrefabUtility.InstantiatePrefab(model);
                inner.name = "Modelo";
                inner.transform.SetParent(root.transform, false);
                // Se COMBINA con el giro y la escala que ya trae el archivo (no se sustituyen).
                inner.transform.localRotation = Quaternion.Euler(e.Euler) * inner.transform.localRotation;

                Bounds b = NasaModelLibrary.WorldBounds(inner);
                float measured = e.ByHeight ? b.size.y : Mathf.Max(b.size.x, b.size.z);
                if (measured < 1e-4f)
                {
                    report.AppendLine("  " + e.Id + ": el modelo no tiene mallas visibles.");
                    return false;
                }
                float scale = e.TargetSize / measured;
                inner.transform.localScale *= scale;

                b = NasaModelLibrary.WorldBounds(inner);
                inner.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z);

                foreach (Collider c in root.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(c);

                Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
                b = NasaModelLibrary.WorldBounds(root);
                string path = PrefabRoot + "/" + e.Id + ".prefab";
                PrefabUtility.SaveAsPrefabAsset(root, path);
                report.AppendLine("  " + e.Id + ": " + path + "  (escala " + scale.ToString("0.####") + ", tamaño " +
                                  b.size.x.ToString("0.00") + " × " + b.size.y.ToString("0.00") + " × " + b.size.z.ToString("0.00") +
                                  " m, " + renderers.Length + " piezas)");
                return true;
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>Limita a 2048 px las texturas que vengan como archivos sueltos junto al modelo.</summary>
        private static void LimitTextures(string modelPath, StringBuilder report)
        {
            string folder = Path.GetDirectoryName(modelPath).Replace('\\', '/');
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                var ti = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as TextureImporter;
                if (ti == null || ti.maxTextureSize <= MaxTextureSize) continue;
                ti.maxTextureSize = MaxTextureSize;
                ti.SaveAndReimport();
                report.AppendLine("  textura limitada a 2048: " + ti.assetPath);
            }
        }

        // ------------------------------------------------------------------
        // Vista previa (comprobar orientación y escala sin entrar en Play)
        // ------------------------------------------------------------------

        /// <summary>
        /// Renderiza cada prefab desde delante (+Z), desde un lado y desde arriba junto a una caja
        /// de 1 m como referencia de escala, y guarda PNG en Logs/NasaPreview/.
        /// </summary>
        [MenuItem("Marte/Vista previa modelos NASA")]
        public static void RenderPreviews()
        {
            string outDir = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "NasaPreview");
            Directory.CreateDirectory(outDir);
            foreach (Entry e in Entries)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRoot + "/" + e.Id + ".prefab");
                if (prefab == null) continue;
                var stage = new GameObject("Vista previa");
                try
                {
                    var inst = (GameObject)Object.Instantiate(prefab, stage.transform);
                    var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cube.transform.SetParent(stage.transform, false);
                    Bounds b = NasaModelLibrary.WorldBounds(inst);
                    cube.transform.position = new Vector3(b.max.x + 0.8f, 0.5f, 0f);

                    var lightGo = new GameObject("Luz");
                    lightGo.transform.SetParent(stage.transform, false);
                    var light = lightGo.AddComponent<Light>();
                    light.type = LightType.Directional;
                    light.intensity = 1.2f;
                    lightGo.transform.rotation = Quaternion.Euler(40f, 150f, 0f);

                    var camGo = new GameObject("Camara");
                    camGo.transform.SetParent(stage.transform, false);
                    var cam = camGo.AddComponent<Camera>();
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.55f, 0.42f, 0.35f);
                    cam.orthographic = true;
                    b = NasaModelLibrary.WorldBounds(stage);
                    cam.orthographicSize = Mathf.Max(b.size.y, b.size.x * 0.6f, b.size.z * 0.6f) * 0.65f;
                    cam.nearClipPlane = 0.01f;
                    cam.farClipPlane = 100f;

                    // Frente: la cámara está en +Z mirando hacia −Z, así que ve la cara delantera del modelo.
                    Render(cam, b.center + Vector3.forward * 20f, Quaternion.Euler(0f, 180f, 0f), Path.Combine(outDir, e.Id + "_frente.png"));
                    Render(cam, b.center + Vector3.right * 20f, Quaternion.Euler(0f, -90f, 0f), Path.Combine(outDir, e.Id + "_lado.png"));
                    Render(cam, b.center + Vector3.up * 20f, Quaternion.Euler(90f, 0f, 0f), Path.Combine(outDir, e.Id + "_arriba.png"));
                }
                finally
                {
                    Object.DestroyImmediate(stage);
                }
            }
            Debug.Log("[Modelos NASA] Vistas previas en " + outDir);
        }

        private static void Render(Camera cam, Vector3 pos, Quaternion rot, string file)
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            var rt = new RenderTexture(768, 768, 24);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
            tex.Apply();
            File.WriteAllBytes(file, tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(tex);
        }

        /// <summary>Lista la jerarquía de cada modelo (para localizar ruedas, mástil, etc.).</summary>
        [MenuItem("Marte/Listar piezas de modelos NASA")]
        public static void DumpHierarchy()
        {
            var sb = new StringBuilder("[Modelos NASA] Jerarquía\n");
            foreach (Entry e in Entries)
            {
                string source = FindModel(e.Id);
                if (source == null) continue;
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(source);
                if (model == null) continue;
                sb.AppendLine(e.Id + " (" + source + ")");
                Dump(model.transform, 1, sb);
            }
            Debug.Log(sb.ToString());
        }

        private static void Dump(Transform t, int depth, StringBuilder sb)
        {
            if (depth > 6) return;
            var r = t.GetComponent<Renderer>();
            sb.Append(' ', depth * 2).Append(t.name);
            if (r != null) sb.Append("  [malla, ").Append(r.sharedMaterials.Length).Append(" mat]");
            sb.AppendLine();
            for (int i = 0; i < t.childCount; i++) Dump(t.GetChild(i), depth + 1, sb);
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
#endif
