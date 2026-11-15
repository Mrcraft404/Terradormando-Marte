using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.World.SpaceWeather;
using UnityEngine;

namespace MarsRobot.CameraEffects
{
    /// <summary>
    /// Distorsión de la cámara por fallos críticos, integrada con la salud modular del rover:
    ///  • Cable de sensores o del visor (HUD) en estado crítico o cortado → aberración cromática,
    ///    líneas de barrido, estática y bandas tipo VHS, que no se van hasta reparar a mano.
    ///  • Interferencias externas (erupciones solares del clima espacial) → mismo efecto, temporal.
    ///  • Cortes de señal breves y aleatorios cuando el daño es muy grave.
    /// Va en el objeto de la cámara. Usa el shader "Hidden/MarsRobot/DamageVision" con
    /// OnRenderImage (Built-in Render Pipeline). Si el shader no está o el proyecto usa URP,
    /// dibuja una capa de estática por encima como alternativa.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class RobotDamageVisionEffect : MonoBehaviour
    {
        [Tooltip("Intensidad con un cable en estado crítico.")]
        [SerializeField, Range(0f, 1f)] private float criticalIntensity = 0.45f;
        [Tooltip("Intensidad con el cable cortado.")]
        [SerializeField, Range(0f, 1f)] private float brokenIntensity = 0.9f;
        [Tooltip("Cortes de señal por segundo con daño máximo.")]
        [SerializeField, Min(0f)] private float glitchRate = 0.6f;

        private Material _mat;
        private RobotHealthSystem _health;
        private float _intensity;
        private float _glitch;
        private Texture2D _noise;
        private bool _shaderMissing;

        /// <summary>Intensidad actual 0..1 (otros sistemas pueden leerla).</summary>
        public float Intensity
        {
            get { return _intensity; }
        }

        private void Start()
        {
            Shader sh = Shader.Find("Hidden/MarsRobot/DamageVision");
            if (sh != null && sh.isSupported) _mat = new Material(sh) { hideFlags = HideFlags.HideAndDontSave };
            else _shaderMissing = true;
        }

        private void OnDestroy()
        {
            if (_mat != null) Destroy(_mat);
            if (_noise != null) Destroy(_noise);
        }

        private void Update()
        {
            if (_health == null) GameServices.TryGet(out _health);

            float target = 0f;
            if (_health != null)
            {
                target = Mathf.Max(target, ModuleIntensity(_health.GetModule(RobotModuleId.CableSensors)));
                target = Mathf.Max(target, ModuleIntensity(_health.GetModule(RobotModuleId.CableHud)));
            }
            target = Mathf.Max(target, SpaceWeatherState.Interference01);
            _intensity = Mathf.MoveTowards(_intensity, target, Time.deltaTime * 2f);

            // Cortes de señal: más frecuentes cuanto peor está.
            _glitch = Mathf.MoveTowards(_glitch, 0f, Time.deltaTime * 6f);
            if (_intensity > 0.6f && Random.value < glitchRate * _intensity * Time.deltaTime) _glitch = 1f;
        }

        private float ModuleIntensity(RobotModule m)
        {
            if (m == null) return 0f;
            if (m.State == ModuleState.Broken) return brokenIntensity;
            if (m.State == ModuleState.Critical) return criticalIntensity;
            return 0f;
        }

        // Built-in: postproceso real.
        private void OnRenderImage(RenderTexture src, RenderTexture dst)
        {
            if (_mat == null || (_intensity <= 0.001f && _glitch <= 0.001f))
            {
                Graphics.Blit(src, dst);
                return;
            }
            _mat.SetFloat("_Intensity", _intensity);
            _mat.SetFloat("_Glitch", _glitch);
            Graphics.Blit(src, dst, _mat);
        }

        // Alternativa sin shader (o en URP, donde OnRenderImage no se llama): estática por encima.
        private void OnGUI()
        {
            bool fallback = _shaderMissing || UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
            if (!fallback || (_intensity <= 0.01f && _glitch <= 0.01f)) return;
            if (_noise == null) _noise = MakeNoise();

            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, Mathf.Max(_intensity * 0.35f, _glitch * 0.85f));
            Rect uv = new Rect(Random.value, Random.value, Screen.width / 128f, Screen.height / 128f);
            GUI.DrawTextureWithTexCoords(new Rect(0, 0, Screen.width, Screen.height), _noise, uv);
            GUI.color = prev;
        }

        private static Texture2D MakeNoise()
        {
            var t = new Texture2D(128, 128, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[128 * 128];
            var rng = new System.Random(11);
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)rng.Next(0, 256);
                px[i] = new Color32(v, v, v, (byte)(i / 128 % 3 == 0 ? 255 : 160)); // con líneas de barrido
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
