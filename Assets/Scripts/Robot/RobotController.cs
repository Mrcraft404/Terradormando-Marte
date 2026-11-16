using MarsRobot.Core;
using MarsRobot.Energy;
using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>
    /// Controlador en primera persona del robot explorador (basado en Rigidbody).
    ///
    ///  • La velocidad se multiplica por la movilidad de <see cref="RobotHealthSystem"/>.
    ///  • Si las ruedas de un lado están dañadas, el robot se desvía solo hacia ese lado.
    ///  • Los choques fuertes y el terreno empinado dañan los módulos (ver ApplyImpact / ApplyWear).
    ///  • Moverse gasta batería; sin batería el robot se arrastra con la reserva mínima.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(RobotHealthSystem))]
    [DisallowMultipleComponent]
    public sealed class RobotController : MonoBehaviour, IPowerConsumer
    {
        [Header("Cámara")]
        [Tooltip("Pivote de la cámara (cabeza del robot). Gira en vertical; el cuerpo gira en horizontal.")]
        [SerializeField] private Transform cameraPivot;
        [SerializeField, Min(0.01f)] private float mouseSensitivity = 2f;

        /// <summary>Sensibilidad del ratón (la cambian los Ajustes).</summary>
        public float MouseSensitivity
        {
            get { return mouseSensitivity; }
            set { mouseSensitivity = Mathf.Clamp(value, 0.1f, 10f); }
        }
        [SerializeField] private float minPitch = -60f;
        [SerializeField] private float maxPitch = 70f;
        [Tooltip("Grados por segundo al girar con Q/E.")]
        [SerializeField, Min(0f)] private float keyboardTurnSpeed = 90f;

        [Header("Movimiento")]
        [SerializeField, Min(0f)] private float maxSpeed = 4f;
        [Tooltip("Velocidad lateral (A izquierda / D derecha) respecto a la de avance. 1 = igual de rápido, " +
                 "como en los controles clásicos W adelante, A izquierda, S atrás, D derecha.")]
        [SerializeField, Range(0f, 1f)] private float strafeFactor = 1f;
        [SerializeField, Min(0f)] private float acceleration = 6f;
        [Tooltip("Pendiente máxima (grados) que el robot puede subir.")]
        [SerializeField, Range(0f, 89f)] private float maxClimbSlope = 35f;

        [Header("Suelo")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField, Min(0.05f)] private float groundCheckRadius = 0.4f;
        [SerializeField, Min(0.05f)] private float groundCheckDistance = 0.6f;

        [Header("Energía")]
        [Tooltip("Potencia de los motores a velocidad máxima (W).")]
        [SerializeField, Min(0f)] private float drivePowerWatts = 250f;
        [Tooltip("Potencia en reposo: ordenador, sensores, calefacción (W).")]
        [SerializeField, Min(0f)] private float idlePowerWatts = 40f;
        [Tooltip("Fracción de velocidad disponible con la batería a cero.")]
        [SerializeField, Range(0f, 1f)] private float depletedSpeedFactor = 0.1f;
        [Tooltip("Batería a usar. Si está vacío se busca un BatterySystem en el mismo objeto.")]
        [SerializeField] private BatterySystem battery;

        private Rigidbody _rb;
        private RobotHealthSystem _health;
        private RobotModuleSystem _modules;
        private float _pitch;
        private float _pendingYaw;
        private Vector2 _moveInput;
        private bool _grounded;
        private Vector3 _groundNormal = Vector3.up;
        private SurfaceHazard _currentSurface;
        private bool _inputEnabled = true;

        /// <summary>Pivote de la cámara (se puede asignar por código).</summary>
        public Transform CameraPivot
        {
            get { return cameraPivot; }
            set { cameraPivot = value; }
        }

        /// <summary>Velocidad máxima en m/s (la sube el taller con motores mejorados).</summary>
        public float MaxSpeed
        {
            get { return maxSpeed; }
            set { maxSpeed = Mathf.Max(0.5f, value); }
        }

        /// <summary>Velocidad horizontal actual (m/s).</summary>
        public float CurrentSpeed { get; private set; }

        /// <summary>Potencia consumida ahora (W), para el HUD de energía.</summary>
        public float CurrentPowerDrawWatts { get; private set; }

        public bool IsGrounded
        {
            get { return _grounded; }
        }

        /// <summary>Activa o desactiva el control (menús, cinemáticas, modo reparación).</summary>
        public bool InputEnabled
        {
            get { return _inputEnabled; }
            set { _inputEnabled = value; }
        }

        // ------------------------------------------------------------------
        // Modificadores externos (carga, módulos, clima espacial…)
        // ------------------------------------------------------------------

        private readonly System.Collections.Generic.List<IMovementModifier> _modifiers = new System.Collections.Generic.List<IMovementModifier>();
        private float _speedMod = 1f, _powerMod = 1f, _wearMod = 1f;

        /// <summary>Multiplicador de velocidad combinado de todos los modificadores (para el HUD).</summary>
        public float SpeedModifier
        {
            get { return _speedMod; }
        }

        /// <summary>Multiplica todos los IMovementModifier del robot (se buscan cada paso: son pocos).</summary>
        private void CollectModifiers()
        {
            GetComponents(_modifiers);
            _speedMod = 1f;
            _powerMod = 1f;
            _wearMod = 1f;
            for (int i = 0; i < _modifiers.Count; i++)
            {
                if (_modifiers[i] is Behaviour b && !b.isActiveAndEnabled) continue;
                _speedMod *= _modifiers[i].SpeedMultiplier;
                _powerMod *= _modifiers[i].PowerMultiplier;
                _wearMod *= _modifiers[i].WearMultiplier;
            }
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _health = GetComponent<RobotHealthSystem>();
            _modules = GetComponent<RobotModuleSystem>();
            if (battery == null) battery = GetComponent<BatterySystem>();

            _rb.interpolation = RigidbodyInterpolation.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            _rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        private void Start()
        {
            LockCursor(true);
        }

        private void Update()
        {
            if (RobotInput.ReleaseCursorPressed) LockCursor(false);
            // Con un menú abierto (_inputEnabled = false) el clic es para los botones, no para capturar el ratón.
            else if (_inputEnabled && RobotInput.ClickPressed && Cursor.lockState != CursorLockMode.Locked) LockCursor(true);

            if (!_inputEnabled)
            {
                _moveInput = Vector2.zero;
                return;
            }

            // El teclado funciona SIEMPRE (antes solo con el ratón capturado, y por eso el robot
            // parecía no moverse hasta hacer clic en la vista Game).
            _moveInput = Vector2.ClampMagnitude(RobotInput.Move, 1f);

            // Giro con teclado (Q/E): alternativa al ratón.
            _pendingYaw += RobotInput.KeyboardTurn * keyboardTurnSpeed * Time.deltaTime;

            // Mirar con el ratón solo cuando está capturado (si no, movería la vista al usar el editor).
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                // El cuerpo gira en horizontal (se aplica en FixedUpdate con MoveRotation, para no
                // pelear con la física) y el pivote de cámara en vertical.
                Vector2 look = RobotInput.Look * mouseSensitivity;
                _pendingYaw += look.x;
                _pitch = Mathf.Clamp(_pitch - look.y, minPitch, maxPitch);
            }
            if (cameraPivot != null) cameraPivot.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
        }

        /// <summary>Última entrada de movimiento leída (para el panel de depuración).</summary>
        public Vector2 MoveInput
        {
            get { return _moveInput; }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            CheckGround();

            // --- Energía disponible ---
            float throttle = _moveInput.magnitude;
            CollectModifiers();
            float wantedWatts = idlePowerWatts + drivePowerWatts * throttle * _powerMod;
            float energyFactor = 1f;
            if (battery != null)
            {
                float wantedWh = MarsTime.WattsToWh(wantedWatts, dt * MarsTime.TimeScale);
                float gotWh = battery.ConsumeUpTo(wantedWh);
                float ratio = wantedWh > 0f ? gotWh / wantedWh : 1f;
                energyFactor = Mathf.Lerp(depletedSpeedFactor, 1f, ratio);
                CurrentPowerDrawWatts = wantedWatts * ratio;
            }
            else
            {
                CurrentPowerDrawWatts = wantedWatts;
            }

            // --- Velocidad objetivo ---
            float surfaceFactor = _currentSurface != null ? _currentSurface.speedMultiplier : 1f;
            float speedLimit = maxSpeed * _health.Mobility * energyFactor * surfaceFactor * _speedMod;

            Vector3 wish = transform.forward * _moveInput.y + transform.right * (_moveInput.x * strafeFactor);
            Vector3 targetVelocity = wish * speedLimit;

            // Sobre el suelo, la velocidad sigue la pendiente.
            float slope = Vector3.Angle(_groundNormal, Vector3.up);
            if (_grounded)
            {
                targetVelocity = Vector3.ProjectOnPlane(targetVelocity, _groundNormal);
                // Demasiado empinado: no se puede subir (sí bajar).
                // Las orugas suben más; las ruedas rápidas, menos.
                float climbLimit = maxClimbSlope + (_modules != null ? _modules.ClimbBonusDegrees : 0f);
                if (slope > climbLimit && targetVelocity.y > 0f) targetVelocity = Vector3.zero;
            }

            Vector3 velocity = GetVelocity();
            Vector3 horizontal = new Vector3(velocity.x, 0f, velocity.z);
            Vector3 targetHorizontal = new Vector3(targetVelocity.x, 0f, targetVelocity.z);
            Vector3 newHorizontal = Vector3.MoveTowards(horizontal, targetHorizontal, acceleration * dt);
            if (_grounded || throttle > 0f)
            {
                SetVelocity(new Vector3(newHorizontal.x, velocity.y, newHorizontal.z));
            }
            CurrentSpeed = newHorizontal.magnitude;

            // --- Giro: ratón + desvío por ruedas dañadas (este último solo si avanza) ---
            float yaw = _pendingYaw;
            _pendingYaw = 0f;
            if (_grounded && CurrentSpeed > 0.1f && Mathf.Abs(_health.SteeringBias) > 0.01f)
            {
                float speed01 = Mathf.Clamp01(CurrentSpeed / Mathf.Max(0.01f, maxSpeed));
                yaw += _health.SteeringBias * _health.Config.maxSteeringPullDegPerSec * speed01 * dt;
            }
            if (Mathf.Abs(yaw) > 0.0001f)
            {
                _rb.MoveRotation(_rb.rotation * Quaternion.Euler(0f, yaw, 0f));
            }

            // --- Desgaste por terreno difícil ---
            if (_grounded && CurrentSpeed > 0.2f)
            {
                float speed01 = Mathf.Clamp01(CurrentSpeed / Mathf.Max(0.01f, maxSpeed));
                RobotHealthConfig cfg = _health.Config;
                if (slope > cfg.roughSlopeDegrees)
                {
                    float severity = Mathf.InverseLerp(cfg.roughSlopeDegrees, 90f, slope);
                    _health.ApplyWear(ModuleCategory.Locomotion,
                        cfg.roughTerrainWearPerSecond * (1f + severity * 3f) * speed01 * dt * _wearMod,
                        DamageSourceType.RoughTerrain);
                }
                if (_currentSurface != null && _currentSurface.wearPerSecond > 0f)
                {
                    _health.ApplyWear(ModuleCategory.Locomotion, _currentSurface.wearPerSecond * speed01 * dt,
                        DamageSourceType.RoughTerrain);
                }
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.contactCount == 0) return;
            ContactPoint contact = collision.GetContact(0);

            // Solo cuenta la componente del choque perpendicular a la superficie.
            float impactSpeed = Mathf.Abs(Vector3.Dot(collision.relativeVelocity, contact.normal));
            var hazard = collision.collider.GetComponentInParent<SurfaceHazard>();
            float multiplier = hazard != null ? hazard.impactMultiplier : 1f;
            _health.ApplyImpact(contact.point, impactSpeed, multiplier);
        }

        private void CheckGround()
        {
            RaycastHit hit;
            Vector3 origin = transform.position + Vector3.up * groundCheckRadius;
            _grounded = Physics.SphereCast(origin, groundCheckRadius, Vector3.down, out hit,
                groundCheckDistance + groundCheckRadius, groundMask, QueryTriggerInteraction.Ignore);

            if (_grounded)
            {
                _groundNormal = hit.normal;
                _currentSurface = hit.collider.GetComponentInParent<SurfaceHazard>();
            }
            else
            {
                _groundNormal = Vector3.up;
                _currentSurface = null;
            }
        }

        private static void LockCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        // Unity 6 renombró Rigidbody.velocity a linearVelocity.
        private Vector3 GetVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return _rb.linearVelocity;
#else
            return _rb.velocity;
#endif
        }

        private void SetVelocity(Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = v;
#else
            _rb.velocity = v;
#endif
        }
    }
}
