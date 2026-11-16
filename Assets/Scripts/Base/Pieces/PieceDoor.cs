using MarsRobot.Crew;
using MarsRobot.Interaction;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Puerta de construcción: la hoja se desliza a un lado (puerta presurizada) o sube (portón del
    /// rover). F la abre o la cierra, tanto con el robot (punto de mira) como con el astronauta.
    /// Al abrir o cerrar, la estructura recalcula qué salas quedan herméticas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PieceDoor : MonoBehaviour, IInteractable, ICrewInteractable
    {
        [SerializeField] private Transform leaf;
        [SerializeField] private bool rollUp;
        [SerializeField] private bool isOpen;

        private Vector3 _closedPos;
        private float _t; // 0 cerrada … 1 abierta

        public bool IsOpen
        {
            get { return isOpen; }
        }

        public void Setup(Transform doorLeaf, bool rollUpGate)
        {
            leaf = doorLeaf;
            rollUp = rollUpGate;
            _closedPos = leaf != null ? leaf.localPosition : Vector3.zero;
        }

        private void Awake()
        {
            if (leaf != null) _closedPos = leaf.localPosition;
            _t = isOpen ? 1f : 0f;
        }

        private void OnEnable()
        {
            CrewInteractables.Register(this);
        }

        private void OnDisable()
        {
            CrewInteractables.Unregister(this);
        }

        public void Toggle()
        {
            isOpen = !isOpen;
            var structure = GetComponentInParent<PieceStructure>();
            if (structure != null) structure.RecomputeRooms();
        }

        private void Update()
        {
            if (leaf == null) return;
            _t = Mathf.MoveTowards(_t, isOpen ? 1f : 0f, Time.deltaTime * (rollUp ? 0.6f : 1.5f));
            float k = Mathf.SmoothStep(0f, 1f, _t);
            // Corredera: se esconde dentro de la pared. Enrollable: sube hasta el dintel.
            Vector3 offset = rollUp ? new Vector3(0f, 2.45f * k, 0f) : new Vector3(1.45f * k, 0f, 0f);
            leaf.localPosition = _closedPos + offset;
            if (rollUp) leaf.localScale = new Vector3(1f, Mathf.Lerp(1f, 0.08f, k), 1f);
        }

        // --- Robot (punto de mira) ---

        public bool CanInteract()
        {
            return true;
        }

        public string GetInteractionPrompt()
        {
            return (isOpen ? Tr.T("Cerrar ") : Tr.T("Abrir ")) + (rollUp ? Tr.T("el portón") : Tr.T("la puerta")) + " [F]";
        }

        public void Interact(Interactor interactor)
        {
            Toggle();
        }

        public float HoldDuration
        {
            get { return 0f; }
        }

        // --- Astronauta ---

        public string CrewPrompt
        {
            get { return "F: " + (isOpen ? Tr.T("cerrar ") : Tr.T("abrir ")) + (rollUp ? Tr.T("el portón") : Tr.T("la puerta")); }
        }

        public Vector3 CrewPoint
        {
            get { return transform.position + Vector3.up * 1f; }
        }

        public float CrewRange
        {
            get { return 1.8f; }
        }

        public bool CrewAvailable
        {
            get { return true; }
        }

        public void CrewInteract(Astronaut astronaut)
        {
            Toggle();
        }
    }
}
