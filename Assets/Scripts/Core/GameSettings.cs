using UnityEngine;

namespace MarsRobot.Core
{
    /// <summary>
    /// Ajustes del juego, guardados en el equipo (PlayerPrefs) y aplicados al cargar:
    /// calidad gráfica, volúmenes, brillo, sensibilidad del ratón, idioma y contador de FPS.
    /// </summary>
    public static class GameSettings
    {
        public static readonly string[] QualityLabels = { "Baja", "Media", "Alta", "Ultra" };
        public static readonly string[] LanguageLabels = { "Español", "English (próximamente)" };

        public static int Quality { get; private set; } = 1;
        public static float MasterVolume { get; private set; } = 0.8f;
        public static float MusicVolume { get; private set; } = 0.6f;
        public static float EffectsVolume { get; private set; } = 0.8f;
        public static float Brightness { get; private set; } = 1f;
        public static float MouseSensitivity { get; private set; } = 2f;
        public static int Language { get; private set; }
        public static bool ShowFps { get; private set; }

        private static Color? _baseAmbient;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void LoadAndApply()
        {
            _baseAmbient = null;
            Quality = PlayerPrefs.GetInt("cfg_quality", 1);
            MasterVolume = PlayerPrefs.GetFloat("cfg_master", 0.8f);
            MusicVolume = PlayerPrefs.GetFloat("cfg_music", 0.6f);
            EffectsVolume = PlayerPrefs.GetFloat("cfg_fx", 0.8f);
            Brightness = PlayerPrefs.GetFloat("cfg_brightness", 1f);
            MouseSensitivity = PlayerPrefs.GetFloat("cfg_mouse", 2f);
            Language = PlayerPrefs.GetInt("cfg_lang", 0);
            ShowFps = PlayerPrefs.GetInt("cfg_fps", 0) == 1;
            ApplyAll();
        }

        public static void SetQuality(int level)
        {
            Quality = Mathf.Clamp(level, 0, QualityLabels.Length - 1);
            PlayerPrefs.SetInt("cfg_quality", Quality);
            ApplyQuality();
        }

        public static void SetVolumes(float master, float music, float effects)
        {
            MasterVolume = Mathf.Clamp01(master);
            MusicVolume = Mathf.Clamp01(music);
            EffectsVolume = Mathf.Clamp01(effects);
            PlayerPrefs.SetFloat("cfg_master", MasterVolume);
            PlayerPrefs.SetFloat("cfg_music", MusicVolume);
            PlayerPrefs.SetFloat("cfg_fx", EffectsVolume);
            ApplyAudio();
        }

        public static void SetBrightness(float value)
        {
            Brightness = Mathf.Clamp(value, 0.5f, 1.5f);
            PlayerPrefs.SetFloat("cfg_brightness", Brightness);
            ApplyBrightness();
        }

        public static void SetMouseSensitivity(float value)
        {
            MouseSensitivity = Mathf.Clamp(value, 0.2f, 8f);
            PlayerPrefs.SetFloat("cfg_mouse", MouseSensitivity);
            ApplyMouse();
        }

        public static void SetLanguage(int index)
        {
            Language = 0; // de momento solo español; se guarda la preferencia para el futuro
            PlayerPrefs.SetInt("cfg_lang", Mathf.Clamp(index, 0, LanguageLabels.Length - 1));
        }

        public static void SetShowFps(bool show)
        {
            ShowFps = show;
            PlayerPrefs.SetInt("cfg_fps", show ? 1 : 0);
            FpsCounter.SetVisible(show);
        }

        public static void Save()
        {
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------

        private static void ApplyAll()
        {
            ApplyQuality();
            ApplyAudio();
            ApplyBrightness();
            ApplyMouse();
            FpsCounter.SetVisible(ShowFps);
        }

        /// <summary>Busca el nivel de calidad de Unity con ese nombre (Low/Medium/High/Ultra) o uno proporcional.</summary>
        private static void ApplyQuality()
        {
            string[] names = QualitySettings.names;
            if (names == null || names.Length == 0) return;
            string[] wanted = { "Low", "Medium", "High", "Ultra" };
            int index = -1;
            for (int i = 0; i < names.Length; i++)
            {
                if (names[i] == wanted[Quality]) index = i;
            }
            if (index < 0) index = Mathf.RoundToInt(Quality / 3f * (names.Length - 1));
            QualitySettings.SetQualityLevel(index, true);
        }

        /// <summary>
        /// El volumen general y el de efectos se aplican ya (todos los sonidos actuales son efectos).
        /// El de música se guarda para cuando haya música.
        /// </summary>
        private static void ApplyAudio()
        {
            AudioListener.volume = MasterVolume * EffectsVolume;
        }

        private static void ApplyBrightness()
        {
            if (_baseAmbient == null) _baseAmbient = RenderSettings.ambientLight;
            RenderSettings.ambientLight = _baseAmbient.Value * Brightness;
        }

        private static void ApplyMouse()
        {
            MarsRobot.Robot.RobotController rc;
            if (GameServices.TryGet(out rc)) rc.MouseSensitivity = MouseSensitivity;
        }

        /// <summary>Volver a aplicar lo que depende de la escena (tras montarla).</summary>
        public static void ReapplySceneSettings()
        {
            _baseAmbient = null;
            ApplyBrightness();
            ApplyMouse();
        }
    }

    /// <summary>Contador de FPS en la esquina inferior izquierda (se activa en Ajustes).</summary>
    public sealed class FpsCounter : MonoBehaviour
    {
        private static FpsCounter _instance;
        private float _smoothed = 60f;
        private GUIStyle _style;

        public static void SetVisible(bool visible)
        {
            if (visible && _instance == null)
            {
                var go = new GameObject("Contador de FPS");
                Object.DontDestroyOnLoad(go);
                _instance = go.AddComponent<FpsCounter>();
            }
            else if (!visible && _instance != null)
            {
                Object.Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        private void Update()
        {
            float fps = 1f / Mathf.Max(0.0001f, Time.unscaledDeltaTime);
            _smoothed = Mathf.Lerp(_smoothed, fps, 0.1f);
        }

        private void OnGUI()
        {
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold };
                _style.normal.textColor = _smoothed >= 50f ? Color.green : (_smoothed >= 30f ? Color.yellow : Color.red);
            }
            _style.normal.textColor = _smoothed >= 50f ? Color.green : (_smoothed >= 30f ? Color.yellow : Color.red);
            GUI.Label(new Rect(10, Screen.height - 26, 120, 22), Mathf.RoundToInt(_smoothed) + " FPS", _style);
        }
    }
}
