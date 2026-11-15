using System;
using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>Modo de cámara del robot.</summary>
    public enum RobotCameraMode
    {
        FirstPerson,
        ThirdPerson
    }

    /// <summary>
    /// Cámara en primera o tercera persona (tecla V).
    ///
    /// El <see cref="RobotController"/> ya gira el cuerpo (horizontal) y el pivote (vertical);
    /// este componente solo coloca la cámara:
    ///  • Primera persona: en el pivote (la "cabeza" del robot).
    ///  • Tercera persona: detrás y por encima del pivote, girando con él. Si hay terreno o rocas
    ///    entre el robot y la cámara, se acerca para no atravesarlos.
    /// </summary>
    [DefaultExecutionOrder(100)] // después de los efectos de cámara, que calculan su desplazamiento antes
    [DisallowMultipleComponent]
    public sealed class RobotCameraRig : MonoBehaviour
    {
        [SerializeField] private Transform pivot;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private RobotCameraMode mode = RobotCameraMode.FirstPerson;

        [Header("Tercera persona")]
        [SerializeField, Min(1f)] private float distance = 6f;
        [SerializeField, Min(1f)] private float minDistance = 2.5f;
        [SerializeField, Min(1f)] private float maxDistance = 14f;
        [SerializeField] private float heightOffset = 1.2f;
        [Tooltip("Radio de la esfera que comprueba choques de la cámara con el terreno.")]
        [SerializeField, Min(0.05f)] private float collisionRadius = 0.3f;
        [SerializeField] private LayerMask collisionMask = ~0;
        [Tooltip("Suavizado al cambiar de distancia (más alto = más rápido).")]
        [SerializeField, Min(0.1f)] private float smoothing = 12f;

        [Header("Primera persona")]
        [Tooltip("Objetos que se ocultan en primera persona (p. ej. el modelo del cuerpo si tapa la vista).")]
        [SerializeField] private Renderer[] hideInFirstPerson = new Renderer[0];

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private Rigidbody _ownBody;
        private float _currentDistance;

        public RobotCameraMode Mode
        {
            get { return mode; }
        }

        /// <summary>Se lanza al cambiar de modo (para HUD, sonido, etc.).</summary>
        public event Action<RobotCameraMode> ModeChanged;

        /// <summary>Asigna las referencias por código (lo usa TestSceneBootstrap).</summary>
        public void Setup(Transform pivotTransform, Transform cam, Renderer[] firstPersonHidden)
        {
            pivot = pivotTransform;
            cameraTransform = cam;
            hideInFirstPerson = firstPersonHidden ?? new Renderer[0];
            ApplyMode();
        }

        private void Awake()
        {
            _ownBody = GetComponentInParent<Rigidbody>();
            _currentDistance = distance;
        }

        private void Start()
        {
            ApplyMode();
        }

        private void Update()
        {
            if (RobotInput.CameraTogglePressed) Toggle();

            if (mode == RobotCameraMode.ThirdPerson)
            {
                float zoom = RobotInput.Zoom;
                if (Mathf.Abs(zoom) > 0.0001f)
                {
                    distance = Mathf.Clamp(distance - zoom * 8f, minDistance, maxDistance);
                }
            }
        }

        public void Toggle()
        {
            SetMode(mode == RobotCameraMode.FirstPerson ? RobotCameraMode.ThirdPerson : RobotCameraMode.FirstPerson);
        }

        public void SetMode(RobotCameraMode newMode)
        {
            if (newMode == mode) return;
            mode = newMode;
            _currentDistance = distance;
            ApplyMode();
            if (ModeChanged != null) ModeChanged(mode);
        }

        private void ApplyMode()
        {
            bool first = mode == RobotCameraMode.FirstPerson;
            for (int i = 0; i < hideInFirstPerson.Length; i++)
            {
                if (hideInFirstPerson[i] != null) hideInFirstPerson[i].enabled = !first;
            }
            if (first && cameraTransform != null && pivot != null)
            {
                cameraTransform.localPosition = Vector3.zero;
                cameraTransform.localRotation = Quaternion.identity;
            }
        }

        // LateUpdate: después de que el controlador y la física hayan movido al robot.
        private void LateUpdate()
        {
            if (pivot == null || cameraTransform == null) return;

            // Efectos de cámara (balanceo, inercia, sacudidas) sumados encima de la posición normal.
            Vector3 fxPos = MarsRobot.CameraEffects.CameraEffectStack.Position;
            Quaternion fxRot = Quaternion.Euler(MarsRobot.CameraEffects.CameraEffectStack.Euler);

            if (mode == RobotCameraMode.FirstPerson)
            {
                cameraTransform.localPosition = fxPos;
                cameraTransform.localRotation = fxRot;
                return;
            }

            // Punto de mira: un poco por encima del pivote. La cámara se aleja hacia atrás
            // siguiendo la rotación del pivote (que incluye la inclinación vertical del ratón).
            Vector3 target = pivot.position + Vector3.up * heightOffset * 0.5f;
            Vector3 back = -(pivot.rotation * Vector3.forward);
            Vector3 up = pivot.rotation * Vector3.up;
            Vector3 desiredDir = (back * distance + up * heightOffset).normalized;
            float desiredDist = Mathf.Sqrt(distance * distance + heightOffset * heightOffset);

            float allowed = ClosestObstacle(target, desiredDir, desiredDist);
            // Acercarse de golpe si hay obstáculo; alejarse suavemente.
            _currentDistance = allowed < _currentDistance
                ? allowed
                : Mathf.Lerp(_currentDistance, allowed, 1f - Mathf.Exp(-smoothing * Time.deltaTime));

            cameraTransform.position = target + desiredDir * _currentDistance;
            cameraTransform.rotation = Quaternion.LookRotation(target - cameraTransform.position, Vector3.up);
            cameraTransform.position += cameraTransform.rotation * fxPos;
            cameraTransform.rotation *= fxRot;
        }

        /// <summary>Distancia libre hasta el primer obstáculo, ignorando los colisionadores del propio robot.</summary>
        private float ClosestObstacle(Vector3 origin, Vector3 dir, float maxDist)
        {
            int count = Physics.SphereCastNonAlloc(origin, collisionRadius, dir, _hits, maxDist, collisionMask,
                QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < count; i++)
            {
                RaycastHit h = _hits[i];
                if (_ownBody != null && h.rigidbody == _ownBody) continue;
                if (h.distance <= 0f) continue; // empezaba dentro: lo ignoramos
                if (h.distance < best) best = h.distance;
            }
            return Mathf.Max(0.5f, best - 0.1f);
        }
    }
}
