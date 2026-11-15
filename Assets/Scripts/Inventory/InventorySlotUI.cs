using UnityEngine;

namespace MarsRobot.Items
{
    /// <summary>
    /// Dibuja una ranura del inventario: icono (o cuadro de color con iniciales), cantidad y una
    /// barrita de estado de conservación. Interfaz provisional con IMGUI.
    /// </summary>
    public static class InventorySlotUI
    {
        private static GUIStyle _count, _initials;

        /// <summary>Dibuja la ranura y devuelve true si se ha hecho clic en ella.</summary>
        public static bool Draw(Rect r, ItemStack stack, bool selected)
        {
            EnsureStyles();
            Color prev = GUI.color;

            // Marco (resaltado si está seleccionada).
            GUI.color = selected ? new Color(1f, 0.8f, 0.3f) : new Color(0.25f, 0.25f, 0.28f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            Rect inner = new Rect(r.x + 2, r.y + 2, r.width - 4, r.height - 4);
            GUI.color = new Color(0.08f, 0.08f, 0.1f);
            GUI.DrawTexture(inner, Texture2D.whiteTexture);
            GUI.color = prev;

            if (stack != null && stack.item != null)
            {
                Rect icon = new Rect(inner.x + 6, inner.y + 6, inner.width - 12, inner.height - 18);
                if (stack.item.icon != null)
                {
                    GUI.DrawTexture(icon, stack.item.icon, ScaleMode.ScaleToFit);
                }
                else
                {
                    GUI.color = stack.item.iconColor;
                    GUI.DrawTexture(icon, Texture2D.whiteTexture);
                    GUI.color = prev;
                    GUI.Label(icon, Initials(stack.item.itemName), _initials);
                }
                GUI.Label(new Rect(inner.x, inner.y + inner.height - 18, inner.width - 4, 18), "×" + stack.count, _count);

                // Estado de conservación (verde → rojo).
                Rect bar = new Rect(inner.x + 4, inner.y + inner.height - 4, (inner.width - 8) * Mathf.Clamp01(stack.condition), 3);
                GUI.color = Color.Lerp(Color.red, Color.green, stack.condition);
                GUI.DrawTexture(bar, Texture2D.whiteTexture);
                GUI.color = prev;
            }
            return GUI.Button(r, GUIContent.none, GUIStyle.none);
        }

        /// <summary>Barra de progreso con texto (masa o volumen).</summary>
        public static void Bar(Rect r, float ratio, string text)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.1f, 0.1f, 0.12f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = ratio > 0.9f ? new Color(0.9f, 0.3f, 0.2f) : (ratio > 0.75f ? new Color(0.95f, 0.75f, 0.2f) : new Color(0.3f, 0.75f, 0.4f));
            GUI.DrawTexture(new Rect(r.x, r.y, r.width * Mathf.Clamp01(ratio), r.height), Texture2D.whiteTexture);
            GUI.color = prev;
            GUI.Label(new Rect(r.x + 6, r.y, r.width - 12, r.height), text);
        }

        private static string Initials(string name)
        {
            string[] parts = name.Split(' ');
            string s = parts[0].Substring(0, Mathf.Min(2, parts[0].Length));
            return s.ToUpperInvariant();
        }

        private static void EnsureStyles()
        {
            if (_count != null) return;
            _count = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.LowerRight, fontSize = 12, fontStyle = FontStyle.Bold };
            _count.normal.textColor = Color.white;
            _initials = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 16, fontStyle = FontStyle.Bold };
            _initials.normal.textColor = new Color(0f, 0f, 0f, 0.7f);
        }
    }
}
