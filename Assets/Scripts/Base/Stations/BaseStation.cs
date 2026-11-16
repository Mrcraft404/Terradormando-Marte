using MarsRobot.Interaction;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Máquina de la base con la que el robot interactúa al acercarse (tecla F).
    /// Cada máquina dibuja su propio menú. De momento con IMGUI (sin paquetes extra);
    /// cuando haya interfaz definitiva se cambia solo el dibujo, no la lógica.
    /// </summary>
    public abstract class BaseStation : MonoBehaviour, IInteractable
    {
        [SerializeField] private string displayName = Tr.T("Máquina");
        [Tooltip("Distancia (m) desde la que el robot puede usarla.")]
        [SerializeField, Min(0.5f)] private float interactRadius = 3.5f;

        protected RocketBase Owner { get; private set; }

        public string DisplayName
        {
            get { return displayName; }
        }

        public float InteractRadius
        {
            get { return interactRadius; }
        }

        /// <summary>False para máquinas pasivas (como la plataforma de carga) que no abren menú.</summary>
        public virtual bool HasMenu
        {
            get { return true; }
        }

        /// <summary>Texto que se muestra al acercarse.</summary>
        public virtual string Prompt
        {
            get { return Tr.T("F: usar ") + displayName; }
        }

        /// <summary>Lo llama la base al crear la máquina.</summary>
        public void Init(RocketBase owner, string name, float radius)
        {
            Owner = owner;
            displayName = name;
            interactRadius = radius;
        }

        /// <summary>Distancia horizontal al robot (la altura no cuenta: el terreno no es plano).</summary>
        public float HorizontalDistance(Vector3 worldPos)
        {
            Vector3 d = worldPos - transform.position;
            // Las máquinas están dentro de la nave, a distintas alturas: otro nivel no cuenta como cerca.
            if (Mathf.Abs(d.y) > 2.5f) return float.MaxValue;
            d.y = 0f;
            return d.magnitude;
        }

        /// <summary>Se llama cada frame mientras el robot está dentro del radio (abra o no el menú).</summary>
        public virtual void WhileRobotNearby(RobotBaseLink robot, float deltaTime) { }

        // ------------------------------------------------------------------
        // IInteractable: también se puede usar apuntándole con el punto de mira
        // ------------------------------------------------------------------

        public virtual bool CanInteract()
        {
            return HasMenu;
        }

        public virtual string GetInteractionPrompt()
        {
            return Prompt;
        }

        public float HoldDuration
        {
            get { return 0f; }
        }

        public void Interact(Interactor interactor)
        {
            if (HasMenu && interactor.BaseLink != null) interactor.BaseLink.OpenMenuFor(this);
        }

        /// <summary>Dibuja el contenido del menú (dentro de una ventana GUILayout).</summary>
        public abstract void DrawMenu(RobotBaseLink robot);

        /// <summary>Si es true, la máquina dibuja su propia pantalla completa (DrawFullScreen) en vez de la ventana.</summary>
        public virtual bool DrawsFullScreen
        {
            get { return false; }
        }

        /// <summary>Pantalla completa propia (tarjetas con dibujos, pensada para niños).</summary>
        public virtual void DrawFullScreen(RobotBaseLink robot) { }
    }
}
