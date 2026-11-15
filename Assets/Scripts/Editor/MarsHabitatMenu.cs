#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using MarsRobot.Base;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Menú Marte ▸ Base: catálogo de módulos, construcción por capas en la escena y pruebas de
    /// validación (quitar/reconstruir capas del módulo seleccionado, con Deshacer).
    /// </summary>
    public static class MarsHabitatMenu
    {
        private const string Folder = "Assets/ScriptableObjects/Base";

        [MenuItem("Marte/Base/Crear catálogo de módulos")]
        public static void CreateCatalog()
        {
            EnsureFolder();
            var created = new List<string>();
            foreach (MarsModuleType type in System.Enum.GetValues(typeof(MarsModuleType)))
            {
                MarsModuleData defaults = MarsModuleData.CreateDefault(type);
                string path = Folder + "/Modulo_" + defaults.moduleId + ".asset";
                if (AssetDatabase.LoadAssetAtPath<MarsModuleData>(path) != null)
                {
                    Object.DestroyImmediate(defaults); // ya existe: no se sobrescriben ajustes hechos a mano
                    continue;
                }
                AssetDatabase.CreateAsset(defaults, path);
                created.Add(path);
            }
            AssetDatabase.SaveAssets();
            Debug.Log(created.Count > 0
                ? "[Base] Módulos creados:\n" + string.Join("\n", created)
                : "[Base] El catálogo ya existía en " + Folder + " (no se ha sobrescrito nada).");
        }

        [MenuItem("Marte/Base/Construir módulos de prueba en la escena")]
        public static void BuildTestModules()
        {
            Vector3 origin = Vector3.zero;
            if (SceneView.lastActiveSceneView != null)
            {
                origin = SceneView.lastActiveSceneView.pivot;
                origin.y = 0f;
            }

            var built = new List<GameObject>();
            float x = 0f;
            foreach (MarsModuleType type in System.Enum.GetValues(typeof(MarsModuleType)))
            {
                MarsModuleData data = LoadOrDefault(type);
                float halfWidth = data.ShieldInnerRadius + data.shieldThickness + 2f;
                x += halfWidth;
                MarsHabitatController habitat = MarsHabitatBuilder.BuildImmediate(data, origin + new Vector3(x, 0f, 0f), Quaternion.identity);
                x += halfWidth + 4f;
                Undo.RegisterCreatedObjectUndo(habitat.gameObject, "Construir módulos de la base");
                built.Add(habitat.gameObject);
                LogRequirements(habitat);
            }
            Selection.objects = built.ToArray();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Base] " + built.Count + " módulos construidos por capas bajo _Structures.");
        }

        [MenuItem("Marte/Base/Validar todas las estructuras")]
        public static void ValidateAll()
        {
            MarsHabitatController[] all = FindAll();
            if (all.Length == 0)
            {
                Debug.Log("[Base] No hay hábitats en la escena.");
                return;
            }
            int ok = 0;
            foreach (MarsHabitatController h in all)
            {
                if (h.Validate(true) == HabitatIssue.None) ok++;
            }
            Debug.Log("[Base] Validación: " + ok + " de " + all.Length + " hábitats completos.");
        }

        // ---------------- Pruebas sobre el módulo seleccionado ----------------

        [MenuItem("Marte/Base/Prueba: quitar Capa 2 (contención) del seleccionado")]
        public static void RemoveShell()
        {
            RemoveFromSelection(HabitatLayer.PressureShell);
        }

        [MenuItem("Marte/Base/Prueba: quitar Capa 3 (escudo) del seleccionado")]
        public static void RemoveShield()
        {
            RemoveFromSelection(HabitatLayer.RegolithShield);
        }

        [MenuItem("Marte/Base/Reconstruir capas que faltan en el seleccionado")]
        public static void RebuildSelected()
        {
            foreach (MarsHabitatController h in SelectedHabitats())
            {
                Undo.RegisterFullObjectHierarchyUndo(h.gameObject, "Reconstruir capas");
                int n = MarsHabitatBuilder.RebuildMissingLayers(h);
                Debug.Log("[Base] " + h.name + ": " + n + " capa(s) reconstruida(s).", h);
            }
        }

        [MenuItem("Marte/Base/Conectar los dos hábitats seleccionados (pasillo)")]
        public static void ConnectSelected()
        {
            List<MarsHabitatController> sel = SelectedHabitats();
            if (sel.Count != 2)
            {
                Debug.LogWarning("[Base] Selecciona exactamente dos hábitats.");
                return;
            }
            MarsHabitatController a = sel[0], b = sel[1];
            // Las dos puertas libres más cercanas entre sí.
            int bestA = -1, bestB = -1;
            float best = float.MaxValue;
            List<HabitatAnchor> anchorsA = a.Data.GetAnchors(), anchorsB = b.Data.GetAnchors();
            for (int i = 0; i < anchorsA.Count; i++)
            {
                if (a.HasOpening(i)) continue;
                Vector3 pa = a.transform.TransformPoint(anchorsA[i].localPosition);
                for (int j = 0; j < anchorsB.Count; j++)
                {
                    if (b.HasOpening(j)) continue;
                    float d = Vector3.Distance(pa, b.transform.TransformPoint(anchorsB[j].localPosition));
                    if (d < best) { best = d; bestA = i; bestB = j; }
                }
            }
            if (bestA < 0)
            {
                Debug.LogWarning("[Base] No quedan puertas libres en alguno de los dos hábitats.");
                return;
            }
            Undo.RegisterFullObjectHierarchyUndo(a.gameObject, "Conectar hábitats");
            Undo.RegisterFullObjectHierarchyUndo(b.gameObject, "Conectar hábitats");
            HabitatConnection link = MarsHabitatBuilder.ConnectHabitats(a, bestA, b, bestB);
            if (link != null) Undo.RegisterCreatedObjectUndo(link.gameObject, "Conectar hábitats");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("[Base] " + a.name + " y " + b.name + " conectados con un pasillo de " + best.ToString("0.0") + " m.", link);
        }

        [MenuItem("Marte/Base/Conectar los dos hábitats seleccionados (pasillo)", true)]
        private static bool HasTwoSelected()
        {
            return SelectedHabitats().Count == 2;
        }

        [MenuItem("Marte/Base/Prueba: quitar Capa 2 (contención) del seleccionado", true)]
        [MenuItem("Marte/Base/Prueba: quitar Capa 3 (escudo) del seleccionado", true)]
        [MenuItem("Marte/Base/Reconstruir capas que faltan en el seleccionado", true)]
        private static bool HasSelectedHabitat()
        {
            return SelectedHabitats().Count > 0;
        }

        private static void RemoveFromSelection(HabitatLayer layer)
        {
            foreach (MarsHabitatController h in SelectedHabitats())
            {
                HabitatLayerNode node = h.GetLayerNode(layer);
                if (node == null) continue;
                Undo.DestroyObjectImmediate(node.gameObject); // se puede deshacer con Ctrl+Z
                h.Validate(false);                            // el aviso sale en la consola
            }
        }

        private static List<MarsHabitatController> SelectedHabitats()
        {
            var list = new List<MarsHabitatController>();
            foreach (GameObject go in Selection.gameObjects)
            {
                var h = go.GetComponentInParent<MarsHabitatController>();
                if (h != null && !list.Contains(h)) list.Add(h);
            }
            return list;
        }

        // ---------------- Utilidades ----------------

        private static MarsModuleData LoadOrDefault(MarsModuleType type)
        {
            MarsModuleData defaults = MarsModuleData.CreateDefault(type);
            var asset = AssetDatabase.LoadAssetAtPath<MarsModuleData>(Folder + "/Modulo_" + defaults.moduleId + ".asset");
            if (asset == null) return defaults; // sin catálogo: se usan los valores de serie en memoria
            Object.DestroyImmediate(defaults);
            return asset;
        }

        private static void LogRequirements(MarsHabitatController habitat)
        {
            MarsModuleData d = habitat.Data;
            var sb = new StringBuilder();
            sb.Append("[Base] ").Append(habitat.name).Append(" — ").Append(d.designPressureKPa.ToString("0.#"))
              .Append(" kPa, volumen ").Append(d.PressurizedVolume.ToString("0")).Append(" m³, escudo ")
              .Append(d.shieldThickness.ToString("0.0")).Append(" m (").Append(d.ShieldArealDensity.ToString("0")).Append(" g/cm²). Materiales:");
            foreach (HabitatMaterialRequirement r in d.EstimateRequirements()) sb.Append("\n  ").Append(r);
            Debug.Log(sb.ToString(), habitat);
        }

        private static MarsHabitatController[] FindAll()
        {
#if UNITY_2023_1_OR_NEWER
#pragma warning disable 0618 // Unity 6.6 la marca obsoleta, pero la alternativa no existe en versiones anteriores
            return Object.FindObjectsByType<MarsHabitatController>(FindObjectsSortMode.None);
#pragma warning restore 0618
#else
            return Object.FindObjectsOfType<MarsHabitatController>();
#endif
        }

        private static void EnsureFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/ScriptableObjects"))
                AssetDatabase.CreateFolder("Assets", "ScriptableObjects");
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets/ScriptableObjects", "Base");
        }
    }
}
#endif
