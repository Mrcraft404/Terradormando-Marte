#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Compila el juego para Windows (64 bits) en Builds/Windows/JUEGO.exe.
    ///   • Menú: Marte ▸ Compilar juego para Windows.
    ///   • Sin abrir Unity (línea de comandos):
    ///       Unity.exe -batchmode -quit -projectPath "…\JUEGO" -buildTarget Win64
    ///                 -executeMethod MarsRobot.EditorTools.MarsBuildTool.BuildWindowsBatch -logFile build.log
    ///
    /// Antes de compilar añade a "Always Included Shaders" todos los shaders que el juego pide por
    /// código con Shader.Find: en una build, Unity solo incluye los shaders que alguna escena o
    /// material referencia, y los materiales creados por código saldrían rosas o invisibles.
    /// </summary>
    public static class MarsBuildTool
    {
        public const string OutputFolder = "Builds/Windows";

        /// <summary>Shaders que los scripts buscan por nombre (Shader.Find).</summary>
        private static readonly string[] RuntimeShaders =
        {
            "Standard",
            "Universal Render Pipeline/Lit", // solo existe si el proyecto usa URP; si no, se ignora
            "Sprites/Default",
            "Legacy Shaders/Diffuse",
            "Unlit/Color",
            "Skybox/Procedural",
            "Skybox/Cubemap",
            "Particles/Standard Unlit",
            "Hidden/MarsRobot/DamageVision",
        };

        [MenuItem("Marte/Compilar juego para Windows")]
        public static void BuildWindowsMenu()
        {
            BuildReport report = BuildWindows();
            if (report != null && report.summary.result == BuildResult.Succeeded)
            {
                EditorUtility.RevealInFinder(report.summary.outputPath);
            }
        }

        /// <summary>Punto de entrada para -executeMethod. Sale con código 0 si va bien y 1 si falla.</summary>
        public static void BuildWindowsBatch()
        {
            BuildReport report = BuildWindows();
            bool ok = report != null && report.summary.result == BuildResult.Succeeded;
            EditorApplication.Exit(ok ? 0 : 1);
        }

        public static BuildReport BuildWindows()
        {
            EnsureAlwaysIncludedShaders();

            string[] scenes = EditorBuildSettings.scenes.Where(s => s.enabled && File.Exists(s.path)).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] No hay escenas activas en Build Settings.");
                return null;
            }

            string exeName = string.IsNullOrEmpty(PlayerSettings.productName) ? "JUEGO" : PlayerSettings.productName;
            string output = Path.Combine(OutputFolder, exeName + ".exe");
            Directory.CreateDirectory(OutputFolder);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = output,
                target = BuildTarget.StandaloneWindows64,
                targetGroup = BuildTargetGroup.Standalone,
                options = BuildOptions.None
            };

            Debug.Log("[Build] Compilando " + scenes.Length + " escena(s): " + string.Join(", ", scenes) + " → " + output);
            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary s = report.summary;
            Debug.Log("[Build] Resultado: " + s.result + " · " + s.totalErrors + " errores · " + s.totalWarnings + " avisos · " +
                      (s.totalSize / (1024 * 1024)) + " MB · " + s.totalTime.TotalMinutes.ToString("0.0") + " min · " +
                      Path.GetFullPath(output));
            return report;
        }

        /// <summary>Añade a Graphics ▸ Always Included Shaders los shaders de RuntimeShaders que falten.</summary>
        public static void EnsureAlwaysIncludedShaders()
        {
            var settings = AssetDatabase.LoadAssetAtPath<GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
            if (settings == null)
            {
                Debug.LogWarning("[Build] No se encontró GraphicsSettings.asset; no se añaden shaders.");
                return;
            }
            var so = new SerializedObject(settings);
            SerializedProperty list = so.FindProperty("m_AlwaysIncludedShaders");
            var present = new HashSet<Shader>();
            for (int i = 0; i < list.arraySize; i++)
            {
                var sh = list.GetArrayElementAtIndex(i).objectReferenceValue as Shader;
                if (sh != null) present.Add(sh);
            }

            var added = new List<string>();
            foreach (string name in RuntimeShaders)
            {
                Shader shader = Shader.Find(name);
                if (shader == null || present.Contains(shader)) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
                present.Add(shader);
                added.Add(name);
            }
            if (added.Count > 0)
            {
                so.ApplyModifiedPropertiesWithoutUndo();
                AssetDatabase.SaveAssets();
                Debug.Log("[Build] Shaders añadidos a Always Included: " + string.Join(", ", added));
            }
        }
    }
}
#endif
