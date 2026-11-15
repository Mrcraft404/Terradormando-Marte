namespace MarsRobot.Interaction
{
    /// <summary>
    /// Cualquier cosa con la que el robot puede interactuar apuntándole con el punto de mira:
    /// rocas, cajas, balizas, el coche, las máquinas del cohete, objetos soltados…
    /// El contexto (inventario, herramienta equipada…) está en <see cref="Interactor.Local"/>.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>¿Se puede usar ahora? (si no, el punto de mira lo muestra en gris).</summary>
        bool CanInteract();

        /// <summary>Texto en pantalla, p. ej. "Recoger mena de hierro [F]".</summary>
        string GetInteractionPrompt();

        /// <summary>Ejecuta la acción.</summary>
        void Interact(Interactor interactor);

        /// <summary>Segundos que hay que mantener la tecla (0 = basta con pulsar).</summary>
        float HoldDuration { get; }
    }
}
