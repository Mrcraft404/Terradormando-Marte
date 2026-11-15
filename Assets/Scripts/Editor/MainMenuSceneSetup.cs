#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Mantiene el menú principal como primera escena de Build Settings (índice 0) y la escena de
    /// juego detrás, para que una build arranque en el menú. Se comprueba al compilar y desde el menú
    /// Marte ▸ Configurar menú principal en Build Settings.
    /// </summary>
    [InitializeOnLoad]
    public static class MainMenuSceneSetup
    {
        private const string MenuScene = "Assets/Scenes/MenuPrincipal.unity";
        private const string GameScene = "Assets/Scenes/Prueba.unity";

        static MainMenuSceneSetup()
        {
            EditorApplication.delayCall += () => Ensure(false);
        }

        [MenuItem("Marte/Configurar menú principal en Build Settings")]
        public static void EnsureFromMenu()
        {
            Ensure(true);
        }

        private static void Ensure(bool log)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScene) == null) return;

            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Count > 0 && scenes[0].path == MenuScene && scenes.Exists(s => s.path == GameScene))
            {
                if (log) UnityEngine.Debug.Log("[Menú] Build Settings ya está bien: " + MenuScene + " es la escena 0.");
                return;
            }

            scenes.RemoveAll(s => s.path == MenuScene);
            scenes.Insert(0, new EditorBuildSettingsScene(MenuScene, true));
            if (!scenes.Exists(s => s.path == GameScene) && AssetDatabase.LoadAssetAtPath<SceneAsset>(GameScene) != null)
                scenes.Add(new EditorBuildSettingsScene(GameScene, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            UnityEngine.Debug.Log("[Menú] Build Settings: " + MenuScene + " es ahora la escena 0 (arranque del juego).");
        }
    }
}
#endif
