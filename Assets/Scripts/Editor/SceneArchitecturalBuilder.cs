#if UNITY_EDITOR
using System.IO;
using MarsRobot.Visuals;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Arquitectura modular de la escena (render integrado):
    ///  • _Environment   → luz global, cielo marciano procedural, niebla, terreno y sondas de reflejos.
    ///  • _Structures    → base, nave nodriza, estaciones científicas y estructuras construidas.
    ///  • _Props         → atrezo y objetos sueltos (cajas, cables, contenedores, vehículos, restos).
    ///  • _PhysicsSystem → controladores y gestores globales (arranque, clima, diálogos, control…).
    /// La escena de juego se monta por código al arrancar; en tiempo de juego SceneRoots coloca cada
    /// objeto creado en su grupo. Este script crea los grupos y el cielo en la escena del editor.
    /// </summary>
    public static class SceneArchitecturalBuilder
    {
        private const string SkyPath = "Assets/Resources/" + MarsEnvironment.SkyResource + ".mat";

        [MenuItem("Marte/Arquitectura de escena/Crear jerarquía y cielo")]
        public static void BuildInActiveScene()
        {
            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.GetActiveScene();
            if (!scene.IsValid())
            {
                Debug.LogWarning("[Arquitectura] No hay escena activa.");
                return;
            }
            Material sky = EnsureSkyMaterial();

            foreach (SceneRoot root in System.Enum.GetValues(typeof(SceneRoot)))
            {
                if (GameObject.Find(SceneRoots.NameOf(root)) != null) continue;
                var go = new GameObject(SceneRoots.NameOf(root));
                Undo.RegisterCreatedObjectUndo(go, "Crear " + go.name);
            }

            // Lo que ya hay en la raíz se reparte por tipo.
            foreach (GameObject go in scene.GetRootGameObjects())
            {
                if (go.name.StartsWith("_")) continue;
                SceneRoot? target = Classify(go);
                if (target == null) continue;
                Undo.SetTransformParent(go.transform, SceneRoots.Get(target.Value), "Ordenar jerarquía");
            }

            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
            }
            EditorSceneManager.MarkSceneDirty(scene);
            Debug.Log("[Arquitectura] Jerarquía lista en " + scene.name + " (_Environment, _Structures, _Props, _PhysicsSystem) y cielo marciano asignado.");
        }

        /// <summary>Línea de comandos: texturas PBR, materiales, cielo y jerarquía de la escena de juego.</summary>
        public static void BatchAll()
        {
            PBRMaterialFactory.GenerateTexturesAndMaterials();
            BuildGameSceneBatch();
        }

        /// <summary>Para la línea de comandos: aplica la arquitectura a la escena de juego y la guarda.</summary>
        public static void BuildGameSceneBatch()
        {
            EditorSceneManager.OpenScene("Assets/Scenes/Prueba.unity", OpenSceneMode.Single);
            BuildInActiveScene();
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        }

        private static SceneRoot? Classify(GameObject go)
        {
            if (go.GetComponent<Light>() != null || go.GetComponent<Terrain>() != null || go.GetComponent<ReflectionProbe>() != null)
                return SceneRoot.Environment;
            if (go.GetComponent<Camera>() != null) return null; // las cámaras se quedan en la raíz
            foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
            {
                if (mb == null) continue;
                string n = mb.GetType().Name;
                if (n.EndsWith("Bootstrap") || n.EndsWith("Manager") || n.EndsWith("System") || n.EndsWith("Controller") || n == "MainMenu")
                    return SceneRoot.PhysicsSystem;
                if (n.EndsWith("Station") || n == "RocketBase") return SceneRoot.Structures;
            }
            if (go.GetComponentInChildren<Rigidbody>() != null) return SceneRoot.Props;
            return null;
        }

        private const string SkyCubePath = "Assets/Resources/Environment/MarsSkyCube.cubemap";

        /// <summary>
        /// Crea (o actualiza) el cielo marciano en Resources: un cubemap pintado con los colores de
        /// Marte (cenit marrón, horizonte color caramelo como la niebla, halo del sol y suelo) y un
        /// material Skybox/Cubemap. El cielo procedural de Unity tiende al azul terrestre.
        /// </summary>
        public static Material EnsureSkyMaterial()
        {
            Shader shader = Shader.Find("Skybox/Cubemap");
            if (shader == null)
            {
                Debug.LogWarning("[Arquitectura] No se encuentra el shader Skybox/Cubemap.");
                return null;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(SkyPath));
            AssetDatabase.Refresh();

            Cubemap cube = BuildSkyCubemap(128);
            var oldCube = AssetDatabase.LoadAssetAtPath<Cubemap>(SkyCubePath);
            if (oldCube != null) AssetDatabase.DeleteAsset(SkyCubePath);
            AssetDatabase.CreateAsset(cube, SkyCubePath);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(SkyPath);
            if (mat != null && mat.shader != shader)
            {
                AssetDatabase.DeleteAsset(SkyPath);
                mat = null;
            }
            if (mat == null)
            {
                mat = new Material(shader) { name = "MarsSky" };
                AssetDatabase.CreateAsset(mat, SkyPath);
            }
            mat.SetTexture("_Tex", cube);
            mat.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            mat.SetFloat("_Exposure", 1f);
            mat.SetFloat("_Rotation", 0f);
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
            return mat;
        }

        private static Cubemap BuildSkyCubemap(int size)
        {
            var cube = new Cubemap(size, TextureFormat.RGBA32, true) { name = "MarsSkyCube" };
            Vector3 sun = (Quaternion.Euler(50f, 30f, 0f) * Vector3.back).normalized; // misma orientación que el Sol del juego
            Color zenith = new Color(0.36f, 0.22f, 0.16f), horizon = new Color(0.8f, 0.6f, 0.45f), ground = new Color(0.46f, 0.3f, 0.21f);
            var faces = new[] { CubemapFace.PositiveX, CubemapFace.NegativeX, CubemapFace.PositiveY, CubemapFace.NegativeY, CubemapFace.PositiveZ, CubemapFace.NegativeZ };
            var px = new Color[size * size];
            foreach (CubemapFace f in faces)
            {
                for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f, v = (y + 0.5f) / size * 2f - 1f;
                    Vector3 d;
                    switch (f)
                    {
                        case CubemapFace.PositiveX: d = new Vector3(1f, -v, -u); break;
                        case CubemapFace.NegativeX: d = new Vector3(-1f, -v, u); break;
                        case CubemapFace.PositiveY: d = new Vector3(u, 1f, v); break;
                        case CubemapFace.NegativeY: d = new Vector3(u, -1f, -v); break;
                        case CubemapFace.PositiveZ: d = new Vector3(u, -v, 1f); break;
                        default: d = new Vector3(-u, -v, -1f); break;
                    }
                    d.Normalize();
                    Color c;
                    if (d.y >= 0f) c = Color.Lerp(horizon, zenith, Mathf.Pow(d.y, 0.55f));
                    else c = Color.Lerp(horizon, ground, Mathf.Clamp01(-d.y * 4f));
                    // Halo del sol: disco pequeño y blanquecino rodeado de polvo azulado-gris (como en las fotos de los rovers).
                    float s = Mathf.Max(0f, Vector3.Dot(d, sun));
                    c += new Color(1f, 0.95f, 0.88f) * Mathf.Pow(s, 900f) * 2.5f;
                    c = Color.Lerp(c, new Color(0.78f, 0.74f, 0.7f), Mathf.Pow(s, 40f) * 0.45f);
                    c += new Color(0.25f, 0.18f, 0.12f) * Mathf.Pow(s, 6f) * 0.35f;
                    c.a = 1f;
                    px[y * size + x] = c;
                }
                cube.SetPixels(px, f);
            }
            cube.Apply(true);
            return cube;
        }

        // ------------------------------------------------------------------
        // Utilidades sobre la selección
        // ------------------------------------------------------------------

        [MenuItem("Marte/Arquitectura de escena/Aplicar variación orgánica a la selección")]
        public static void JitterSelection()
        {
            if (Selection.transforms.Length == 0)
            {
                Debug.LogWarning("[Arquitectura] Selecciona primero los objetos de atrezo.");
                return;
            }
            var rng = new System.Random(System.Environment.TickCount);
            foreach (Transform t in Selection.transforms)
            {
                Undo.RecordObject(t, "Variación orgánica");
                SetDressing.Jitter(t, rng);
            }
        }

        [MenuItem("Marte/Arquitectura de escena/Ajustar colisionadores a la selección (estático)")]
        public static void FitSelectionStatic()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Ajustar colisionador");
                ColliderFitter.FitStatic(go);
            }
        }

        [MenuItem("Marte/Arquitectura de escena/Ajustar colisionadores a la selección (dinámico)")]
        public static void FitSelectionDynamic()
        {
            foreach (GameObject go in Selection.gameObjects)
            {
                Undo.RegisterFullObjectHierarchyUndo(go, "Ajustar colisionador dinámico");
                ColliderFitter.FitDynamic(go, 300f);
            }
        }
    }
}
#endif
