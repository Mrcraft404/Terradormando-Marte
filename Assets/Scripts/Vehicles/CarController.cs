using UnityEngine;

namespace MarsRobot.Vehicles
{
    /// <summary>
    /// Controlador del coche de pruebas (MarteCar): W/S acelera y frena, A/D gira, como un coche.
    ///
    /// Cambios respecto al código original (para que compile y funcione en Unity 6):
    ///  • 'carBody.angularVelocity += lookRot' sumaba un Quaternion a un Vector3 (error de
    ///    compilación) → ahora el giro se aplica como velocidad angular en el eje Y.
    ///  • La física va en FixedUpdate y el teclado se lee en Update (lo recomendado en Unity).
    ///  • 'speed' antes no se usaba: ahora es la velocidad máxima y 'acceleration' la empuja hasta ahí.
    ///  • Si no se asigna 'carBody', usa el Rigidbody del propio objeto.
    ///  • Las ruedas giran sobre su eje (configurable), no sobre el eje vertical.
    ///
    /// OJO: no pongas este script en el robot. El robot ya usa RobotController (se mueve como una
    /// persona, con A/D hacia los lados) y ambos leerían las mismas teclas.
    /// </summary>
    public class CarController : MonoBehaviour
    {
        [Tooltip("El objeto físico de la caja. Si se deja vacío, se usa el Rigidbody de este objeto.")]
        public Rigidbody carBody;
        public GameObject leftWheel;
        public GameObject rightWheel;
        [Tooltip("Ruedas extra (p. ej. las traseras) que también deben girar al avanzar.")]
        public GameObject[] extraWheels = new GameObject[0];

        [Tooltip("Velocidad máxima (m/s).")]
        public float speed = 25f;

        [Tooltip("Aceleración del motor (m/s²).")]
        public float acceleration = 8f;

        [Tooltip("Velocidad de giro (grados por segundo).")]
        public float turnSpeed = 30f;

        [Tooltip("Agarre lateral (0–1): cuánto deslizamiento de lado se elimina en cada paso. 1 = sin derrape.")]
        [Range(0f, 1f)] public float lateralGrip = 0.85f;

        [Tooltip("Frenado por rodadura (m/s²) cuando no se acelera.")]
        public float rollingResistance = 1.5f;

        [Tooltip("Eje local sobre el que giran visualmente las ruedas (X para cilindros tumbados de lado).")]
        public Vector3 wheelSpinAxis = Vector3.right;

        private float _throttle;
        private float _steer;

        private void Awake()
        {
            if (carBody == null) carBody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            // Leer teclado aquí; aplicar física en FixedUpdate.
            _throttle = Input.GetAxis("Vertical");   // W/S o flechas arriba/abajo
            _steer = Input.GetAxis("Horizontal");    // A/D o flechas izquierda/derecha
        }

        private void FixedUpdate()
        {
            if (carBody == null) return;

            // 1. Mover el coche (fuerza motriz) en la dirección en la que mira.
            if (Mathf.Abs(_throttle) > 0.01f)
            {
                float forwardSpeed = Vector3.Dot(GetVelocity(), transform.forward);
                // Solo empujar si no se ha llegado a la velocidad máxima en ese sentido.
                if (forwardSpeed * Mathf.Sign(_throttle) < speed)
                {
                    carBody.AddForce(transform.forward * _throttle * acceleration, ForceMode.Acceleration);
                }
            }

            // 1b. Agarre lateral (sin él, con poca fricción el coche derrapa en cada curva) y
            //     frenado por rodadura cuando se suelta el acelerador.
            Vector3 vel = GetVelocity();
            Vector3 sideways = Vector3.Project(vel, transform.right);
            vel -= sideways * lateralGrip;
            if (Mathf.Abs(_throttle) <= 0.01f)
            {
                Vector3 along = Vector3.Project(vel, transform.forward);
                float slow = Mathf.Min(along.magnitude, rollingResistance * Time.fixedDeltaTime);
                if (along.sqrMagnitude > 0.0001f) vel -= along.normalized * slow;
            }
            SetVelocity(vel);

            // 2. Girar el coche sobre el eje Y (grados/s → radianes/s).
            Vector3 angular = carBody.angularVelocity;
            angular.y = _steer * turnSpeed * Mathf.Deg2Rad;
            carBody.angularVelocity = angular;

            // 3. Animación visual de las ruedas según la velocidad real.
            float forward = Vector3.Dot(GetVelocity(), transform.forward);
            RotateWheels(forward * 360f * Time.fixedDeltaTime);
        }

        /// <summary>Gira las ruedas visualmente (grados por paso de física).</summary>
        private void RotateWheels(float degrees)
        {
            if (leftWheel != null) leftWheel.transform.Rotate(wheelSpinAxis, degrees, Space.Self);
            if (rightWheel != null) rightWheel.transform.Rotate(wheelSpinAxis, degrees, Space.Self);
            for (int i = 0; i < extraWheels.Length; i++)
            {
                if (extraWheels[i] != null) extraWheels[i].transform.Rotate(wheelSpinAxis, degrees, Space.Self);
            }
        }

        /// <summary>Velocidad de avance actual (m/s), para el velocímetro.</summary>
        public float ForwardSpeed
        {
            get { return carBody != null ? Vector3.Dot(GetVelocity(), transform.forward) : 0f; }
        }

        private void OnCollisionStay(Collision collision)
        {
            // Aquí podrías agregar lógica de "chocar" o frenado por fricción.
        }

        private Vector3 GetVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return carBody.linearVelocity;
#else
            return carBody.velocity;
#endif
        }

        private void SetVelocity(Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            carBody.linearVelocity = v;
#else
            carBody.velocity = v;
#endif
        }
    }
}
