using UnityEngine;

namespace MarsRobot.Interaction
{
    /// <summary>
    /// Punto de mira en el centro de la pantalla: blanco normalmente, verde sobre algo
    /// interactuable, gris si no se puede usar ahora. Debajo, el texto de la acción
    /// (GetInteractionPrompt) y una barra para las interacciones mantenidas.
    /// Interfaz provisional con IMGUI.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractionUI : MonoBehaviour
    {
        [SerializeField] private Color idleColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color activeColor = new Color(0.35f, 1f, 0.45f, 0.95f);
        [SerializeField] private Color blockedColor = new Color(0.6f, 0.6f, 0.6f, 0.8f);
        [SerializeField, Min(2f)] private float crosshairSize = 14f;

        private PlayerInteractor _interactor;
        private GUIStyle _prompt;

        private void Awake()
        {
            _interactor = GetComponent<PlayerInteractor>();
        }

        private void OnGUI()
        {
            if (_interactor == null || !_interactor.isActiveAndEnabled) return;
            if (Cursor.lockState != CursorLockMode.Locked) return; // con menús abiertos no se dibuja

            if (_prompt == null)
            {
                _prompt = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter, fontSize = 16, richText = true };
                _prompt.normal.textColor = Color.white;
            }

            IInteractable t = _interactor.Target;
            Color c = t == null ? idleColor : (t.CanInteract() ? activeColor : blockedColor);
            float cx = Screen.width * 0.5f, cy = Screen.height * 0.5f;
            float s = t == null ? crosshairSize : crosshairSize * 1.4f;

            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(cx - s * 0.5f, cy - 1, s, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(cx - 1, cy - s * 0.5f, 2, s), Texture2D.whiteTexture);
            GUI.color = prev;

            if (t != null)
            {
                GUI.Label(new Rect(cx - 250, cy + 18, 500, 26), t.GetInteractionPrompt(), _prompt);
                float p = _interactor.HoldProgress;
                if (p > 0f)
                {
                    GUI.color = new Color(0f, 0f, 0f, 0.6f);
                    GUI.DrawTexture(new Rect(cx - 60, cy + 46, 120, 6), Texture2D.whiteTexture);
                    GUI.color = activeColor;
                    GUI.DrawTexture(new Rect(cx - 60, cy + 46, 120 * p, 6), Texture2D.whiteTexture);
                    GUI.color = prev;
                }
            }

            string fb = _interactor.Feedback;
            if (fb.Length > 0) GUI.Label(new Rect(cx - 300, cy + 60, 600, 26), "<b>" + fb + "</b>", _prompt);
        }
    }
}
