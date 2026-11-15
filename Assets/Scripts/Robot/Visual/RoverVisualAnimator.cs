using UnityEngine;

namespace MarsRobot.Robot.Visual
{
    /// <summary>
    /// Anima el modelo del rover según lo que hace de verdad:
    ///  • Ruedas: giran según la velocidad real (sin patinar visualmente).
    ///  • Dirección: las 6 ruedas giran hasta 90° hacia la dirección de avance (A/D = de lado),
    ///    y al girar sobre sí mismo las de las esquinas se cruzan (giro en el sitio).
    ///  • Mástil: la cabeza sigue el cabeceo de la cámara.
    ///  • Brazo: se despliega hacia el suelo mientras se usa la herramienta (X) y se pliega al acabar.
    ///  • Antena de alta ganancia: barrido lento. Cubierta solar: visible con el módulo de paneles extra.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RoverVisualAnimator : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float steerSpeedDegPerSec = 180f;
        [SerializeField, Min(0.1f)] private float armSpeed = 1.5f;

        private RoverVisualRig _rig;
        private Rigidbody _rb;
        private RobotController _controller;
        private RoverToolController _tools;
        private RobotModuleSystem _modules;
        private readonly float[] _steer = new float[6];
        private readonly float[] _spin = new float[6];
        private float _arm01;
        private float _lastYaw;

        public RoverVisualRig Rig
        {
            get { return _rig; }
        }

        public void Setup(RoverVisualRig rig)
        {
            _rig = rig;
        }

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _controller = GetComponent<RobotController>();
            _tools = GetComponent<RoverToolController>();
            _modules = GetComponent<RobotModuleSystem>();
            _lastYaw = transform.eulerAngles.y;
        }

        private void OnEnable()
        {
            if (_modules != null) _modules.Changed += RefreshModules;
        }

        private void OnDisable()
        {
            if (_modules != null) _modules.Changed -= RefreshModules;
        }

        private void Start()
        {
            RefreshModules();
        }

        private void RefreshModules()
        {
            if (_rig == null || _rig.solarDeck == null) return;
            _rig.solarDeck.SetActive(_modules != null && _modules.HasModule(RoverModuleType.ExtraSolarPanels));
        }

        private void LateUpdate()
        {
            if (_rig == null) return;
            float dt = Time.deltaTime;

            Vector3 vel = Vector3.zero;
            if (_rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                vel = _rb.linearVelocity;
#else
                vel = _rb.velocity;
#endif
            }
            Vector3 local = transform.InverseTransformDirection(vel);
            Vector2 planar = new Vector2(local.x, local.z);
            float speed = planar.magnitude;
            float yawRate = Mathf.DeltaAngle(_lastYaw, transform.eulerAngles.y) / Mathf.Max(dt, 0.0001f);
            _lastYaw = transform.eulerAngles.y;

            // Ángulo de avance: 0 = recto, ±90 = de lado. Marcha atrás se trata como "recto".
            float travel = speed > 0.15f ? Mathf.Atan2(planar.x, Mathf.Abs(planar.y)) * Mathf.Rad2Deg : 0f;
            bool reverse = planar.y < -0.05f && Mathf.Abs(travel) < 80f;
            if (reverse) travel = -travel;
            bool spinInPlace = speed < 0.3f && Mathf.Abs(yawRate) > 15f;

            for (int i = 0; i < 6; i++)
            {
                if (_rig.wheelSteer[i] == null) continue;
                int row = i % 3; // 0 delantera, 1 central, 2 trasera
                bool left = i < 3;

                float target = travel;
                if (spinInPlace && row != 1)
                {
                    // Giro sobre sí mismo: ruedas de las esquinas en diagonal.
                    target = (row == 0 ? 1f : -1f) * (left ? -45f : 45f);
                }
                _steer[i] = Mathf.MoveTowards(_steer[i], Mathf.Clamp(target, -90f, 90f), steerSpeedDegPerSec * dt);
                _rig.wheelSteer[i].localRotation = Quaternion.Euler(0f, _steer[i], 0f);

                // Rodadura: velocidad a lo largo de la dirección de la rueda (+ el giro en el sitio).
                Vector3 wheelDir = Quaternion.Euler(0f, _steer[i], 0f) * Vector3.forward;
                float along = Vector3.Dot(new Vector3(local.x, 0f, local.z), wheelDir);
                if (spinInPlace) along += yawRate * Mathf.Deg2Rad * 1.15f * (left ? 1f : -1f) * 0.7f;
                _spin[i] += along / _rig.wheelRadius * Mathf.Rad2Deg * dt;
                _rig.wheelSpin[i].localRotation = Quaternion.Euler(_spin[i], 0f, 0f);
            }

            // Cabeza del mástil: copia el cabeceo del pivote de la cámara.
            if (_rig.mastHead != null && _controller != null && _controller.CameraPivot != null)
            {
                float pitch = _controller.CameraPivot.localEulerAngles.x;
                _rig.mastHead.localRotation = Quaternion.Euler(Mathf.DeltaAngle(0f, pitch), 0f, 0f);
            }

            // Brazo: desplegado mientras se trabaja con la herramienta.
            bool working = _tools != null && _tools.IsWorking;
            _arm01 = Mathf.MoveTowards(_arm01, working ? 1f : 0f, armSpeed * dt);
            float a = Mathf.SmoothStep(0f, 1f, _arm01);
            if (_rig.armShoulder != null)
            {
                // Plegado: hacia la izquierda, pegado al frontal. Desplegado: hacia delante y abajo.
                _rig.armShoulder.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-95f, -10f, a), 0f);
                // Desplegado: brazo horizontal, antebrazo 30° hacia abajo y torreta vertical: la broca
                // queda justo sobre el suelo a ~2,4 m por delante.
                _rig.armUpper.localRotation = Quaternion.Euler(Mathf.Lerp(-5f, 0f, a), 0f, 0f);
                _rig.armElbow.localRotation = Quaternion.Euler(Mathf.Lerp(170f, 30f, a), 0f, 0f);
                _rig.armWrist.localRotation = Quaternion.Euler(Mathf.Lerp(-80f, -30f, a), 0f, 0f);
                // Vibración del taladro al trabajar.
                if (working && _arm01 > 0.95f) _rig.armWrist.localPosition = new Vector3(0f, Mathf.Sin(Time.time * 60f) * 0.004f, 0.72f);
                else _rig.armWrist.localPosition = new Vector3(0f, 0f, 0.72f);
            }

            if (_rig.highGainAntenna != null) _rig.highGainAntenna.localRotation = Quaternion.Euler(0f, Mathf.Sin(Time.time * 0.1f) * 40f, 0f);
        }
    }
}
