using MarsRobot.Interaction;
using UnityEngine;

namespace MarsRobot.Items
{
    /// <summary>
    /// Objeto soltado en el suelo: una caja física que se puede volver a recoger con F.
    /// Se crea por código (equivale al "prefab físico" del objeto).
    /// </summary>
    public sealed class DroppedItem : MonoBehaviour, IInteractable
    {
        private ItemData _item;
        private int _count;
        private float _condition = 1f;

        public static DroppedItem Spawn(ItemData item, int count, float condition, Vector3 position)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Objeto: " + item.itemName + " ×" + count;
            // Tamaño según el volumen real (cubo de igual volumen, mínimo 25 cm).
            float side = Mathf.Max(0.25f, Mathf.Pow(item.volumeLiters * count / 1000f, 1f / 3f));
            go.transform.position = position;
            go.transform.localScale = Vector3.one * side;
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Max(1f, item.massKg * count);
            var r = go.GetComponent<Renderer>();
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            r.material = new Material(shader) { color = item.iconColor };

            var d = go.AddComponent<DroppedItem>();
            d._item = item;
            d._count = count;
            d._condition = condition;
            return d;
        }

        public bool CanInteract()
        {
            return _item != null && Interactor.Local != null && Interactor.Local.Inventory != null;
        }

        public string GetInteractionPrompt()
        {
            if (_item == null) return "";
            return "Recoger " + _item.itemName + " ×" + _count + " [F]";
        }

        public float HoldDuration
        {
            get { return 0f; }
        }

        public void Interact(Interactor interactor)
        {
            if (interactor.Inventory == null) return;
            int taken = interactor.Inventory.TryAdd(_item, _count, _condition);
            if (taken <= 0)
            {
                interactor.ShowFeedback("No cabe: bahía llena (masa o volumen).");
                return;
            }
            _count -= taken;
            interactor.ShowFeedback("Recogido: " + _item.itemName + " ×" + taken);
            if (_count <= 0) Destroy(gameObject);
        }
    }
}
