using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.UI
{
    /// <summary>
    /// Efectos de interfaz por cables dañados. Solo escucha eventos de <see cref="RobotHealthSystem"/>;
    /// no sabe nada de física ni de colisiones.
    ///
    ///  • Cable del HUD dañado → el visor parpadea (más cuanto peor está). Cortado → casi apagado
    ///    con destellos ocasionales.
    ///  • Cable del minimapa cortado → el minimapa se apaga y aparece "estática".
    ///  • Cable de sensores cortado → se oculta la telemetría (velocidad, pendiente, temperatura).
    ///
    /// Usa solo CanvasGroup y GameObject (sin TextMeshPro ni paquetes extra). El valor
    /// <see cref="GlitchIntensity"/> puede alimentar un shader de distorsión más adelante.
    /// </summary>
    public sealed class RobotDamageHUD : MonoBehaviour
    {
        [SerializeField] private RobotHealthSystem health;

        [Header("Visor (HUD)")]
        [SerializeField] private CanvasGroup hudGroup;

        [Header("Minimapa")]
        [SerializeField] private GameObject minimapRoot;
        [Tooltip("Imagen de interferencia que se muestra cuando el minimapa no tiene señal.")]
        [SerializeField] private GameObject minimapStaticOverlay;

        [Header("Sensores")]
        [SerializeField] private CanvasGroup sensorsGroup;

        [Header("Parpadeo")]
        [SerializeField, Min(0.01f)] private float flickerInterval = 0.08f;

        private float _hudGlitch;
        private float _minimapGlitch;
        private float _sensorsGlitch;
        private float _flickerTimer;

        /// <summary>0..1, intensidad del fallo del visor (para shaders/post-proceso).</summary>
        public float GlitchIntensity
        {
            get { return _hudGlitch; }
        }

        private void OnEnable()
        {
            if (health == null) health = FindHealthSystem();
            if (health == null) return;
            health.ModuleDamaged += OnModuleDamaged;
            health.ModuleRepaired += OnModuleRepaired;
            health.ModuleStateChanged += OnModuleStateChanged;
            RefreshAll();
        }

        private void OnDisable()
        {
            if (health == null) return;
            health.ModuleDamaged -= OnModuleDamaged;
            health.ModuleRepaired -= OnModuleRepaired;
            health.ModuleStateChanged -= OnModuleStateChanged;
        }

        private static RobotHealthSystem FindHealthSystem()
        {
            RobotHealthSystem found;
            return MarsRobot.Core.GameServices.TryGet(out found) ? found : null;
        }

        private void OnModuleDamaged(RobotModule module, DamageInfo info)
        {
            if (module.Category == ModuleCategory.Wiring) RefreshAll();
        }

        private void OnModuleRepaired(RobotModule module)
        {
            if (module.Category == ModuleCategory.Wiring) RefreshAll();
        }

        private void OnModuleStateChanged(RobotModule module, ModuleState previous)
        {
            if (module.Category == ModuleCategory.Wiring) RefreshAll();
        }

        private void RefreshAll()
        {
            if (health.Modules.Count == 0) return; // aún sin inicializar

            _hudGlitch = GlitchFor(RobotModuleId.CableHud);
            _minimapGlitch = GlitchFor(RobotModuleId.CableMinimap);
            _sensorsGlitch = GlitchFor(RobotModuleId.CableSensors);

            bool minimapAlive = health.IsFunctional(RobotModuleId.CableMinimap);
            if (minimapRoot != null) minimapRoot.SetActive(minimapAlive);
            if (minimapStaticOverlay != null) minimapStaticOverlay.SetActive(!minimapAlive);

            ApplyAlpha(); // estado estable si no hay fallo
        }

        /// <summary>0 = cable perfecto, 1 = cortado.</summary>
        private float GlitchFor(RobotModuleId id)
        {
            RobotModule m = health.GetModule(id);
            if (m == null) return 0f;
            return 1f - m.Efficiency;
        }

        private void Update()
        {
            if (_hudGlitch <= 0f && _sensorsGlitch <= 0f && _minimapGlitch <= 0f) return;

            _flickerTimer -= Time.unscaledDeltaTime;
            if (_flickerTimer > 0f) return;
            _flickerTimer = flickerInterval;
            ApplyAlpha();

            // Minimapa dañado pero no cortado: pierde señal a ratos.
            if (minimapRoot != null && _minimapGlitch > 0f && _minimapGlitch < 1f)
            {
                bool dropout = Random.value < _minimapGlitch * 0.5f;
                minimapRoot.SetActive(!dropout);
                if (minimapStaticOverlay != null) minimapStaticOverlay.SetActive(dropout);
            }
        }

        private void ApplyAlpha()
        {
            if (hudGroup != null) hudGroup.alpha = FlickerAlpha(_hudGlitch);
            if (sensorsGroup != null) sensorsGroup.alpha = FlickerAlpha(_sensorsGlitch);
        }

        /// <summary>
        /// Con fallo leve, caídas de brillo poco frecuentes; con el cable cortado, casi apagado
        /// con destellos breves (simula un cable que hace contacto a ratos).
        /// </summary>
        private static float FlickerAlpha(float glitch)
        {
            if (glitch <= 0f) return 1f;
            if (glitch >= 1f) return Random.value < 0.08f ? 0.6f : 0.05f;
            return Random.value < glitch * 0.6f ? Random.Range(0.1f, 0.6f) : 1f;
        }
    }
}
