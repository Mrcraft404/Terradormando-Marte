using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.CameraEffects
{
    /// <summary>
    /// Sacudidas de cámara:
    ///  • Impactos: TriggerShake(intensidad 0..1, duración) añade "trauma" que decae; la sacudida
    ///    usa ruido Perlin (suave pero violenta), proporcional al trauma al cuadrado.
    ///    Se dispara solo cuando el rover recibe daño estructural.
    ///  • Rodadura: vibración leve constante al avanzar por pendientes, rocas o dunas.
    /// Uso desde cualquier sistema: CameraShake.Shake(0.6f, 0.4f);
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CameraShake : MonoBehaviour, ICameraEffect
    {
        private static CameraShake _instance;

        [Header("Impactos")]
        [Tooltip("Desplazamiento máximo (m) con trauma 1.")]
        [SerializeField, Min(0f)] private float maxOffset = 0.18f;
        [Tooltip("Giro máximo (grados) con trauma 1.")]
        [SerializeField, Min(0f)] private float maxAngle = 6f;
        [Tooltip("Velocidad del ruido (más alto = vibración más rápida).")]
        [SerializeField, Min(0.1f)] private float frequency = 22f;
        [Tooltip("Daño que equivale a una sacudida máxima.")]
        [SerializeField, Min(1f)] private float damageForFullShake = 30f;

        [Header("Rodadura en terreno difícil")]
        [Tooltip("Trauma constante máximo por terreno (0..1).")]
        [SerializeField, Range(0f, 1f)] private float terrainRumble = 0.12f;

        private float _trauma;
        private float _decayPerSecond = 1.5f;
        private float _seed;
        private RobotHealthSystem _health;
        private RobotController _controller;
        private TerrainManager _terrain;
        private Vector3 _pos, _rot;

        public Vector3 PositionOffset
        {
            get { return _pos; }
        }

        public Vector3 EulerOffset
        {
            get { return _rot; }
        }

        /// <summary>Sacudida global (si hay un rover en escena).</summary>
        public static void Shake(float intensity, float duration)
        {
            if (_instance != null) _instance.TriggerShake(intensity, duration);
        }

        /// <summary>Añade una sacudida: intensidad 0..1 y duración aproximada en segundos.</summary>
        public void TriggerShake(float intensity, float duration)
        {
            _trauma = Mathf.Clamp01(Mathf.Max(_trauma, intensity));
            _decayPerSecond = Mathf.Max(0.3f, 1f / Mathf.Max(0.05f, duration));
        }

        private void Awake()
        {
            _instance = this;
            _seed = Random.value * 100f;
            _health = GetComponent<RobotHealthSystem>();
            _controller = GetComponent<RobotController>();
        }

        private void OnEnable()
        {
            CameraEffectStack.Register(this);
            if (_health != null) _health.ModuleDamaged += OnDamaged;
        }

        private void OnDisable()
        {
            CameraEffectStack.Unregister(this);
            if (_health != null) _health.ModuleDamaged -= OnDamaged;
            _pos = Vector3.zero;
            _rot = Vector3.zero;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void OnDamaged(RobotModule module, DamageInfo info)
        {
            // Solo golpes (no el desgaste lento), proporcional al daño.
            if (info.Source != DamageSourceType.Impact) return;
            TriggerShake(Mathf.Clamp01(info.Amount / damageForFullShake), 0.5f);
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _trauma = Mathf.Max(0f, _trauma - _decayPerSecond * dt);

            // Vibración por terreno: pendiente y velocidad.
            float rumble = 0f;
            if (_controller != null && _controller.IsGrounded && _controller.CurrentSpeed > 0.3f)
            {
                if (_terrain == null) GameServices.TryGet(out _terrain);
                float slope = _terrain != null ? _terrain.GetSlopeDegrees(transform.position) : 0f;
                float speed01 = Mathf.Clamp01(_controller.CurrentSpeed / Mathf.Max(0.5f, _controller.MaxSpeed));
                rumble = terrainRumble * speed01 * Mathf.Clamp01(0.3f + slope / 30f);
            }

            float shake = Mathf.Max(_trauma * _trauma, rumble * rumble * 4f);
            if (shake <= 0.0001f)
            {
                _pos = Vector3.zero;
                _rot = Vector3.zero;
                return;
            }
            float t = Time.time * frequency;
            _pos = new Vector3(Noise(t, 0f), Noise(t, 1f), Noise(t, 2f) * 0.5f) * maxOffset * shake;
            _rot = new Vector3(Noise(t, 3f), Noise(t, 4f), Noise(t, 5f)) * maxAngle * shake;
        }

        /// <summary>Ruido Perlin centrado en 0 (−1..1).</summary>
        private float Noise(float t, float channel)
        {
            return Mathf.PerlinNoise(_seed + channel * 17.3f, t) * 2f - 1f;
        }
    }
}
