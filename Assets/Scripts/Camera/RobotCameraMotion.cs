using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.CameraEffects
{
    /// <summary>
    /// Movimiento procedural de la cámara del rover:
    ///  • Balanceo de rodadura: ondas senoidales en Y (arriba/abajo) y X (lado a lado) cuya
    ///    frecuencia y amplitud crecen con la velocidad.
    ///  • Inercia del chasis: al acelerar la cámara cabecea hacia atrás, al frenar hacia delante,
    ///    y al moverse de lado o girar se inclina (roll), con un retardo suave que da sensación de peso.
    ///
    /// Ajustes: bobAmplitude/bobFrequency (balanceo), tiltPerAccel y maxTilt (inercia),
    /// tiltSmoothing (más bajo = más "pesado").
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotCameraMotion : MonoBehaviour, ICameraEffect
    {
        [Header("Balanceo de rodadura")]
        [Tooltip("Amplitud vertical (m) a velocidad máxima.")]
        [SerializeField, Min(0f)] private float bobAmplitude = 0.035f;
        [Tooltip("Amplitud lateral (m) a velocidad máxima.")]
        [SerializeField, Min(0f)] private float swayAmplitude = 0.02f;
        [Tooltip("Ciclos por segundo a velocidad máxima.")]
        [SerializeField, Min(0f)] private float bobFrequency = 2.2f;

        [Header("Inercia del chasis")]
        [Tooltip("Grados de inclinación por cada m/s² de aceleración.")]
        [SerializeField, Min(0f)] private float tiltPerAccel = 1.4f;
        [SerializeField, Min(0f)] private float maxTilt = 4f;
        [Tooltip("Suavizado (más bajo = cámara más pesada y lenta en reaccionar).")]
        [SerializeField, Min(0.1f)] private float tiltSmoothing = 4f;
        [Tooltip("Grados de inclinación lateral por cada grado/s de giro.")]
        [SerializeField, Min(0f)] private float rollPerTurn = 0.025f;

        private RobotController _controller;
        private Rigidbody _rb;
        private Vector3 _lastLocalVel;
        private float _lastYaw;
        private float _phase;
        private Vector3 _tilt;
        private Vector3 _pos;

        public Vector3 PositionOffset
        {
            get { return _pos; }
        }

        public Vector3 EulerOffset
        {
            get { return _tilt; }
        }

        private void Awake()
        {
            _controller = GetComponent<RobotController>();
            _rb = GetComponent<Rigidbody>();
            _lastYaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            CameraEffectStack.Register(this);
        }

        private void OnDisable()
        {
            CameraEffectStack.Unregister(this);
            _pos = Vector3.zero;
            _tilt = Vector3.zero;
        }

        private void LateUpdate()
        {
            float dt = Mathf.Max(Time.deltaTime, 0.0001f);
            Vector3 vel = _rb != null ?
#if UNITY_6000_0_OR_NEWER
                _rb.linearVelocity
#else
                _rb.velocity
#endif
                : Vector3.zero;
            Vector3 localVel = transform.InverseTransformDirection(vel);
            float maxSpeed = _controller != null ? Mathf.Max(0.5f, _controller.MaxSpeed) : 4f;
            float speed01 = Mathf.Clamp01(new Vector2(localVel.x, localVel.z).magnitude / maxSpeed);
            bool grounded = _controller == null || _controller.IsGrounded;

            // 1) Balanceo: la fase avanza más rápido cuanto más rápido vamos.
            _phase += dt * bobFrequency * Mathf.Lerp(0.4f, 1f, speed01) * 2f * Mathf.PI;
            float amp = grounded ? speed01 : 0f;
            Vector3 targetPos = new Vector3(Mathf.Cos(_phase * 0.5f) * swayAmplitude * amp, Mathf.Sin(_phase) * bobAmplitude * amp, 0f);
            _pos = Vector3.Lerp(_pos, targetPos, 1f - Mathf.Exp(-12f * dt));

            // 2) Inercia: aceleración local (adelante/lado) y velocidad de giro.
            Vector3 accel = (localVel - _lastLocalVel) / dt;
            _lastLocalVel = localVel;
            float yaw = transform.eulerAngles.y;
            float yawRate = Mathf.DeltaAngle(_lastYaw, yaw) / dt;
            _lastYaw = yaw;

            // Acelerar → la cámara "se queda atrás" (pitch negativo); frenar → cabecea adelante.
            float pitch = Mathf.Clamp(-accel.z * tiltPerAccel, -maxTilt, maxTilt);
            float roll = Mathf.Clamp(accel.x * tiltPerAccel * 0.7f - yawRate * rollPerTurn, -maxTilt, maxTilt);
            Vector3 targetTilt = new Vector3(pitch, 0f, roll);
            _tilt = Vector3.Lerp(_tilt, targetTilt, 1f - Mathf.Exp(-tiltSmoothing * dt));
        }
    }
}
