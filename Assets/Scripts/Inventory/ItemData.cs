using MarsRobot.Base;
using UnityEngine;

namespace MarsRobot.Items
{
    /// <summary>Familia del objeto (para filtrar, transferir "todos los minerales", etc.).</summary>
    public enum ItemCategory
    {
        Mineral,
        Volatile,        // Hielo / volátiles
        RobotComponent,  // Componente robótico
        Tool,            // Herramienta
        Garden           // Material de huerto
    }

    /// <summary>
    /// Definición de un objeto con propiedades físicas reales. Se crea con
    /// Assets ▸ Create ▸ Marte ▸ Objeto. Los assets dentro de una carpeta Resources/Items se
    /// cargan solos al empezar (ver <see cref="ItemDatabase"/>) y sustituyen a los de por defecto.
    /// </summary>
    [CreateAssetMenu(fileName = "Item", menuName = "Marte/Objeto")]
    public class ItemData : ScriptableObject
    {
        [Header("Identidad")]
        public string itemID = "item";
        public string itemName = "Objeto";
        [Tooltip("Icono (opcional). Sin icono se dibuja un cuadro de color con las iniciales.")]
        public Texture2D icon;
        public Color iconColor = new Color(0.6f, 0.4f, 0.3f);
        [TextArea(2, 5)] public string description;
        [Tooltip("Composición o notas científicas (p. ej. espectro CRISM).")]
        [TextArea(2, 5)] public string scientificNotes;
        public ItemCategory itemCategory = ItemCategory.Mineral;

        [Header("Física")]
        [Tooltip("Masa de UNA unidad (kg).")]
        [Min(0.01f)] public float massKg = 1f;
        [Tooltip("Volumen de UNA unidad en la bahía de carga (litros).")]
        [Min(0.01f)] public float volumeLiters = 1f;
        [Min(1)] public int maxStackSize = 10;

        [Header("Conservación")]
        [Tooltip("Hielo o volátil: sin módulo criogénico se sublima con el tiempo.")]
        public bool requiresCryoStorage;
        [Tooltip("Fracción de la pila que se pierde por hora de juego sin almacenamiento criogénico.")]
        [Range(0f, 1f)] public float sublimationPerHour = 0.05f;
        [Tooltip("Corrosivo o tóxico (p. ej. percloratos): sin contenedor sellado desgasta la bahía de carga.")]
        public bool isCorrosiveOrToxic;

        [Header("Al descargar en el cohete")]
        [Tooltip("Si está activo, al entregarlo en la bodega del cohete se convierte en un recurso de la base.")]
        public bool convertsToBaseResource = true;
        public ResourceType baseResource = ResourceType.Regolith;
        [Min(0)] public int baseResourcePerUnit = 1;
    }
}
