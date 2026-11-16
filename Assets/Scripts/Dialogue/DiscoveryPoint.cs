using MarsRobot.Interaction;
using MarsRobot.UI.Minimap;
using UnityEngine;

namespace MarsRobot.Dialogue
{
    /// <summary>
    /// Punto de descubrimiento (p. ej. restos de una sonda antigua ficticia). Al apuntarle y
    /// mantener F se descarga su registro: se reproduce un diálogo y queda en la bitácora.
    /// Solo se puede leer una vez. Aparece como marcador amarillo en el minimapa.
    /// </summary>
    public sealed class DiscoveryPoint : MonoBehaviour, IInteractable
    {
        [SerializeField] private string dialogueNodeId = "probe_archive";
        [SerializeField] private string displayName = Tr.T("Restos de una sonda antigua");

        private bool _read;

        public static DiscoveryPoint Spawn(Vector3 groundPosition, string nodeId, string name)
        {
            var root = new GameObject(name);
            root.transform.position = groundPosition;
            root.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            // Modelo de la NASA si está importado (InSight para módulos de aterrizaje, Ingenuity para
            // el resto); si no, los restos provisionales de siempre.
            string modelId = name.ToLowerInvariant().Contains("aterrizaje") ? MarsRobot.Visuals.NasaModelLibrary.Lander : MarsRobot.Visuals.NasaModelLibrary.Helicopter;
            GameObject nasa = MarsRobot.Visuals.NasaModelLibrary.Spawn(modelId, root.transform, "Modelo NASA");
            if (nasa != null)
            {
                // Medio hundido e inclinado: lleva tiempo abandonado.
                nasa.transform.localPosition = new Vector3(0f, -0.08f, 0f);
                nasa.transform.localRotation = Quaternion.Euler(4f, 0f, 7f);
                // Colisionador invisible para poder apuntarle (F) y para que el rover choque con él.
                // (se mide sin el giro aleatorio para que la caja quede alineada con el modelo)
                Quaternion yaw = root.transform.rotation;
                root.transform.rotation = Quaternion.identity;
                Bounds b = MarsRobot.Visuals.NasaModelLibrary.WorldBounds(nasa);
                root.transform.rotation = yaw;
                var box = root.AddComponent<BoxCollider>();
                box.center = b.center - groundPosition;
                box.size = new Vector3(Mathf.Max(0.4f, b.size.x), Mathf.Max(0.3f, b.size.y), Mathf.Max(0.4f, b.size.z));
            }
            else
            {
                Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0.35f, 0f), new Vector3(1.2f, 0.6f, 1f), new Vector3(8f, 0f, 14f), new Color(0.75f, 0.72f, 0.65f), true);
                Part(root.transform, PrimitiveType.Cube, new Vector3(1.1f, 0.15f, 0.2f), new Vector3(1.4f, 0.04f, 0.8f), new Vector3(0f, 20f, -12f), new Color(0.15f, 0.2f, 0.35f), false);
                Part(root.transform, PrimitiveType.Cylinder, new Vector3(-0.3f, 0.9f, 0f), new Vector3(0.05f, 0.5f, 0.05f), new Vector3(0f, 0f, 25f), new Color(0.6f, 0.6f, 0.6f), false);
            }

            var d = root.AddComponent<DiscoveryPoint>();
            d.dialogueNodeId = nodeId;
            d.displayName = name;
            root.AddComponent<MinimapObjectMarker>().Setup(MinimapMarkerType.Discovery, true, name);
            return d;
        }

        public float HoldDuration
        {
            get { return 1f; }
        }

        public bool CanInteract()
        {
            return !_read;
        }

        public string GetInteractionPrompt()
        {
            return _read ? displayName + Tr.T(" (registro ya descargado)") : displayName + Tr.T(": descargar registro [mantén F]");
        }

        public void Interact(Interactor interactor)
        {
            if (_read) return;
            _read = true;
            DialogueManager.Play(dialogueNodeId);
            LogTerminal.Add(Tr.T("Descubrimiento"), displayName + Tr.T(" encontrado."), LogCategory.Discovery);
            var marker = GetComponent<MinimapObjectMarker>();
            if (marker != null) marker.Setup(MinimapMarkerType.Discovery, false, displayName);
        }

        private static void Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, Color color, bool collider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            if (!collider) Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.transform.localRotation = Quaternion.Euler(euler);
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            go.GetComponent<Renderer>().sharedMaterial = new Material(shader) { color = color };
        }
    }
}
