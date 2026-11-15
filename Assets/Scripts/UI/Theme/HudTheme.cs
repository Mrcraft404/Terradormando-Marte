using UnityEngine;

namespace MarsRobot.UI
{
    /// <summary>
    /// Tema visual del panel de gestión (estilo terminal de nave: azul marino translúcido, bordes
    /// cian, textos claros). Todo con IMGUI y texturas de 1 píxel, sin paquetes extra.
    /// La interfaz se diseña sobre un lienzo virtual de 1600 × 900 y se escala a la pantalla.
    /// </summary>
    public static class HudTheme
    {
        public const float VirtualWidth = 1600f;
        public const float VirtualHeight = 900f;

        public static readonly Color PanelBg = new Color(0.04f, 0.09f, 0.16f, 0.9f);
        public static readonly Color PanelLight = new Color(0.07f, 0.15f, 0.25f, 0.92f);
        public static readonly Color Slot = new Color(0.1f, 0.18f, 0.27f, 0.95f);
        public static readonly Color Accent = new Color(0.2f, 0.85f, 1f, 1f);
        public static readonly Color AccentDim = new Color(0.15f, 0.5f, 0.65f, 1f);
        public static readonly Color Text = new Color(0.9f, 0.95f, 1f);
        public static readonly Color TextDim = new Color(0.6f, 0.75f, 0.85f);
        public static readonly Color Good = new Color(0.3f, 0.85f, 0.35f);
        public static readonly Color Warn = new Color(0.95f, 0.75f, 0.15f);
        public static readonly Color Bad = new Color(0.9f, 0.25f, 0.25f);

        private static GUIStyle _title, _subtitle, _text, _small, _button, _tab, _tabActive, _pill, _slotName, _slotCount, _big;

        public static GUIStyle Title { get { Ensure(); return _title; } }
        public static GUIStyle Subtitle { get { Ensure(); return _subtitle; } }
        public static GUIStyle Body { get { Ensure(); return _text; } }
        public static GUIStyle Small { get { Ensure(); return _small; } }
        public static GUIStyle Button { get { Ensure(); return _button; } }
        public static GUIStyle Tab { get { Ensure(); return _tab; } }
        public static GUIStyle TabActive { get { Ensure(); return _tabActive; } }
        public static GUIStyle Pill { get { Ensure(); return _pill; } }
        public static GUIStyle SlotName { get { Ensure(); return _slotName; } }
        public static GUIStyle SlotCount { get { Ensure(); return _slotCount; } }
        public static GUIStyle BigButton { get { Ensure(); return _big; } }

        /// <summary>Escala la interfaz a la pantalla manteniendo la proporción (lienzo 1600 × 900).</summary>
        public static void BeginScaled()
        {
            float s = Mathf.Min(Screen.width / VirtualWidth, Screen.height / VirtualHeight);
            float ox = (Screen.width - VirtualWidth * s) * 0.5f, oy = (Screen.height - VirtualHeight * s) * 0.5f;
            GUI.matrix = Matrix4x4.TRS(new Vector3(ox, oy, 0f), Quaternion.identity, new Vector3(s, s, 1f));
        }

        public static void EndScaled()
        {
            GUI.matrix = Matrix4x4.identity;
        }

        public static void Fill(Rect r, Color c)
        {
            Color prev = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = prev;
        }

        /// <summary>Marco con esquinas biseladas (líneas cian) y fondo translúcido.</summary>
        public static void Frame(Rect r, Color bg, Color border, float thickness = 2f)
        {
            Fill(r, bg);
            Fill(new Rect(r.x, r.y, r.width, thickness), border);
            Fill(new Rect(r.x, r.yMax - thickness, r.width, thickness), border);
            Fill(new Rect(r.x, r.y, thickness, r.height), border);
            Fill(new Rect(r.xMax - thickness, r.y, thickness, r.height), border);
            // Remates en las esquinas (detalle "tecnológico").
            Fill(new Rect(r.x, r.y, 22, 4), border);
            Fill(new Rect(r.x, r.y, 4, 22), border);
            Fill(new Rect(r.xMax - 22, r.yMax - 4, 22, 4), border);
            Fill(new Rect(r.xMax - 4, r.yMax - 22, 4, 22), border);
        }

        public static void Panel(Rect r, string title, string subtitle)
        {
            Frame(r, PanelBg, AccentDim);
            GUI.Label(new Rect(r.x + 18, r.y + 10, r.width - 36, 36), title, Title);
            if (!string.IsNullOrEmpty(subtitle)) GUI.Label(new Rect(r.x + 18, r.y + 46, r.width - 36, 22), subtitle, Subtitle);
        }

        /// <summary>Barra de progreso con texto.</summary>
        public static void Bar(Rect r, float ratio, Color color, string text)
        {
            Fill(r, new Color(0.02f, 0.05f, 0.09f, 0.95f));
            Fill(new Rect(r.x + 2, r.y + 2, (r.width - 4) * Mathf.Clamp01(ratio), r.height - 4), color);
            if (!string.IsNullOrEmpty(text)) GUI.Label(new Rect(r.x + 8, r.y, r.width - 16, r.height), text, Small);
        }

        /// <summary>Etiqueta de estado con fondo de color (Operativo / Desgaste / Necesita reparación).</summary>
        public static void StatePill(Rect r, string text, Color c)
        {
            Fill(r, c);
            Color prev = GUI.contentColor;
            GUI.contentColor = c.grayscale > 0.55f ? new Color(0.1f, 0.1f, 0.05f) : Color.white;
            GUI.Label(r, text, Pill);
            GUI.contentColor = prev;
        }

        private static void Ensure()
        {
            if (_title != null) return;
            _title = Make(26, FontStyle.Bold, Text, TextAnchor.UpperLeft);
            _subtitle = Make(15, FontStyle.Normal, TextDim, TextAnchor.UpperLeft);
            _text = Make(15, FontStyle.Normal, Text, TextAnchor.UpperLeft);
            _text.wordWrap = true;
            _small = Make(13, FontStyle.Normal, Text, TextAnchor.MiddleLeft);
            _slotName = Make(11, FontStyle.Bold, Text, TextAnchor.LowerCenter);
            _slotName.wordWrap = true;
            _slotCount = Make(15, FontStyle.Bold, Accent, TextAnchor.LowerRight);
            _pill = Make(13, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            _button = new GUIStyle(GUI.skin.button) { fontSize = 15, fontStyle = FontStyle.Bold };
            _button.normal.background = Solid(new Color(0.08f, 0.3f, 0.45f));
            _button.hover.background = Solid(new Color(0.1f, 0.45f, 0.6f));
            _button.active.background = Solid(new Color(0.05f, 0.6f, 0.8f));
            _button.normal.textColor = _button.hover.textColor = _button.active.textColor = Color.white;

            _big = new GUIStyle(_button) { fontSize = 22 };
            _big.normal.background = Solid(new Color(0.05f, 0.45f, 0.6f));
            _big.hover.background = Solid(new Color(0.1f, 0.6f, 0.8f));

            _tab = new GUIStyle(_button) { fontSize = 17, fontStyle = FontStyle.Normal };
            _tab.normal.background = Solid(new Color(0.06f, 0.12f, 0.2f, 0.95f));
            _tab.normal.textColor = TextDim;
            _tabActive = new GUIStyle(_tab) { fontStyle = FontStyle.Bold };
            _tabActive.normal.background = _tabActive.hover.background = Solid(Accent);
            _tabActive.normal.textColor = _tabActive.hover.textColor = new Color(0.02f, 0.1f, 0.15f);
        }

        private static GUIStyle Make(int size, FontStyle style, Color color, TextAnchor anchor)
        {
            var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = style, alignment = anchor, richText = true };
            s.normal.textColor = color;
            return s;
        }

        private static Texture2D Solid(Color c)
        {
            var t = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            t.SetPixel(0, 0, c);
            t.Apply();
            return t;
        }
    }
}
