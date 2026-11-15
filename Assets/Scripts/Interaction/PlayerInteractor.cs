using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Interaction
{
    /// <summary>
    /// Lanza un rayo continuo desde la cámara hacia delante y detecta objetos IInteractable.
    ///  • F pulsada: interacción inmediata.
    ///  • F mantenida: interacciones largas (HoldDuration &gt; 0, p. ej. minar una roca).
    /// Va en el robot (no en la cámara) porque la cámara cambia entre 1ª y 3ª persona: el rayo se
    /// lanza desde la cámara activa y la distancia se mide desde el robot.
    /// Se ejecuta antes que RobotBaseLink: si hay objetivo, la F es suya; si no, la F abre la
    /// máquina más cercana como antes.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    [DisallowMultipleComponent]
    public sealed class PlayerInteractor : Interactor
    {
        [Tooltip("Distancia máxima de interacción desde el rover (m). Un rover de 3 m necesita algo más que una persona.")]
        [SerializeField, Min(1f)] private float interactDistance = 5f;
        [SerializeField] private LayerMask mask = ~0;

        private readonly RaycastHit[] _hits = new RaycastHit[12];
        private Rigidbody _ownBody;
        private float _holdTime;
        private string _feedback = "";
        private float _feedbackTime = -100f;

        /// <summary>Objeto apuntado ahora mismo (null si ninguno).</summary>
        public IInteractable Target { get; private set; }

        /// <summary>Punto de impacto del rayo sobre el objetivo.</summary>
        public Vector3 TargetPoint { get; private set; }

        /// <summary>Progreso 0..1 de una interacción mantenida.</summary>
        public float HoldProgress
        {
            get
            {
                if (Target == null || Target.HoldDuration <= 0f) return 0f;
                return Mathf.Clamp01(_holdTime / Target.HoldDuration);
            }
        }

        public bool HasTarget
        {
            get { return Target != null; }
        }

        public string Feedback
        {
            get { return Time.time - _feedbackTime < 2.5f ? _feedback : ""; }
        }

        protected override void Awake()
        {
            base.Awake();
            _ownBody = GetComponent<Rigidbody>();
            Local = this;
        }

        private void OnDisable()
        {
            Target = null;
            _holdTime = 0f;
        }

        private void Update()
        {
            Target = null;
            if (Controller != null && !Controller.InputEnabled) return; // menús abiertos o aterrizando

            Camera cam = Camera.main;
            if (cam == null) return;
            FindTarget(cam);

            if (Target == null)
            {
                _holdTime = 0f;
                return;
            }
            if (!Target.CanInteract())
            {
                _holdTime = 0f;
                if (RobotInput.InteractPressed) ShowFeedback(Target.GetInteractionPrompt());
                return;
            }

            if (Target.HoldDuration <= 0f)
            {
                if (RobotInput.InteractPressed) Target.Interact(this);
                return;
            }

            // Interacción mantenida.
            if (RobotInput.InteractHeld)
            {
                _holdTime += Time.deltaTime;
                if (_holdTime >= Target.HoldDuration)
                {
                    _holdTime = 0f;
                    Target.Interact(this);
                }
            }
            else
            {
                _holdTime = 0f;
            }
        }

        private void FindTarget(Camera cam)
        {
            // En 3ª persona la cámara está lejos: alargamos el rayo esa distancia.
            float camOffset = Vector3.Distance(cam.transform.position, transform.position);
            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            int n = Physics.RaycastNonAlloc(ray, _hits, interactDistance + camOffset + 2f, mask, QueryTriggerInteraction.Collide);

            float best = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                RaycastHit h = _hits[i];
                if (_ownBody != null && h.rigidbody == _ownBody) continue;
                if (h.distance >= best) continue;

                // Lo primero que bloquea el rayo decide (aunque no sea interactuable).
                best = h.distance;
                IInteractable it = h.collider.GetComponentInParent<IInteractable>();
                Vector3 flat = h.point - transform.position;
                if (it != null && flat.magnitude <= interactDistance + 1.5f)
                {
                    Target = it;
                    TargetPoint = h.point;
                }
                else
                {
                    Target = null;
                }
            }
        }

        public override void ShowFeedback(string text)
        {
            _feedback = text;
            _feedbackTime = Time.time;
        }
    }
}
