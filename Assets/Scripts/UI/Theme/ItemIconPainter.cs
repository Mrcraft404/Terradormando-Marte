using System.Collections.Generic;
using MarsRobot.Base;
using MarsRobot.Items;
using UnityEngine;

namespace MarsRobot.UI
{
    /// <summary>Formas de icono disponibles.</summary>
    public enum IconShape
    {
        Rock, Crystal, Ingot, Bottle, Canister, Coil, Chip, SolarPanel, Battery, Bag, Brick, Toolbox, Flask, Gear, Snowflake
    }

    /// <summary>
    /// Pinta por código los iconos de los objetos (64 × 64, fondo transparente, con sombreado y
    /// contorno). Así el inventario tiene "imágenes" sin necesidad de archivos. Si un ItemData tiene
    /// su propio icono (Texture2D), se usa ese en lugar del pintado.
    /// Los iconos se generan una vez y se guardan en caché.
    /// </summary>
    public static class ItemIconPainter
    {
        private const int S = 64;
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        public static Texture2D For(ItemData item)
        {
            if (item == null) return null;
            if (item.icon != null) return item.icon;
            return Get(ShapeFor(item.itemID, item.itemCategory), item.iconColor);
        }

        public static Texture2D For(ResourceType type)
        {
            return Get(ShapeFor(type), ItemDatabase.ResourceColor(type));
        }

        public static IconShape ShapeFor(ResourceType t)
        {
            switch (t)
            {
                case ResourceType.Regolith: return IconShape.Bag;
                case ResourceType.Iron: return IconShape.Rock;
                case ResourceType.Silicon: return IconShape.Crystal;
                case ResourceType.Ice: return IconShape.Snowflake;
                case ResourceType.MetalParts: return IconShape.Gear;
                case ResourceType.Wiring: return IconShape.Coil;
                case ResourceType.SolarCells: return IconShape.SolarPanel;
                case ResourceType.RegolithBricks: return IconShape.Brick;
                case ResourceType.Water: return IconShape.Bottle;
                case ResourceType.Oxygen: return IconShape.Bottle;
                case ResourceType.Samples: return IconShape.Flask;
                case ResourceType.Methane: return IconShape.Canister;
                default: return IconShape.Chip;
            }
        }

        private static IconShape ShapeFor(string id, ItemCategory cat)
        {
            if (id.StartsWith("recurso_"))
            {
                ResourceType t;
                if (System.Enum.TryParse(id.Substring(8), out t)) return ShapeFor(t);
            }
            switch (id)
            {
                case ItemDatabase.IronOre: return IconShape.Rock;
                case ItemDatabase.Regolith: return IconShape.Bag;
                case ItemDatabase.PerchlorateRegolith: return IconShape.Bag;
                case ItemDatabase.Ice: return IconShape.Snowflake;
                case ItemDatabase.Clays: return IconShape.Rock;
                case ItemDatabase.Sulfates: return IconShape.Crystal;
                case ItemDatabase.Sample: return IconShape.Flask;
                case ItemDatabase.AuxBattery: return IconShape.Battery;
                case ItemDatabase.SpareParts: return IconShape.Toolbox;
            }
            switch (cat)
            {
                case ItemCategory.Volatile: return IconShape.Bottle;
                case ItemCategory.RobotComponent: return IconShape.Chip;
                case ItemCategory.Tool: return IconShape.Toolbox;
                case ItemCategory.Garden: return IconShape.Bag;
                default: return IconShape.Rock;
            }
        }

        public static Texture2D Get(IconShape shape, Color color)
        {
            string key = shape + "_" + ColorUtility.ToHtmlStringRGB(color);
            Texture2D t;
            if (Cache.TryGetValue(key, out t) && t != null) return t;
            t = Paint(shape, color);
            Cache[key] = t;
            return t;
        }

        // ------------------------------------------------------------------
        // Pintor
        // ------------------------------------------------------------------

        private static Color32[] _px;
        private static Color _base;

        private static Texture2D Paint(IconShape shape, Color c)
        {
            _px = new Color32[S * S];
            _base = c;
            switch (shape)
            {
                case IconShape.Rock:
                    Poly(new[] { V(10, 18), V(22, 10), V(42, 12), V(55, 24), V(52, 42), V(36, 52), V(16, 46), V(8, 32) });
                    Poly(new[] { V(22, 22), V(34, 18), V(40, 30), V(28, 36) }, 1.25f);
                    break;
                case IconShape.Crystal:
                    Poly(new[] { V(32, 6), V(46, 22), V(40, 56), V(24, 56), V(18, 22) });
                    Poly(new[] { V(32, 6), V(36, 24), V(32, 56), V(24, 56), V(18, 22) }, 1.3f);
                    Poly(new[] { V(14, 30), V(20, 26), V(22, 54), V(14, 54) }, 0.9f);
                    break;
                case IconShape.Ingot:
                    Poly(new[] { V(8, 40), V(18, 22), V(54, 22), V(58, 40), V(54, 48), V(10, 48) });
                    Poly(new[] { V(18, 22), V(54, 22), V(50, 30), V(20, 30) }, 1.35f);
                    break;
                case IconShape.Bottle:
                    Rect(24, 4, 16, 8, 0.6f);
                    Capsule(32, 34, 14, 26);
                    Rect(22, 26, 20, 12, 1.4f);
                    break;
                case IconShape.Canister:
                    Poly(new[] { V(14, 14), V(44, 14), V(52, 22), V(52, 58), V(14, 58) });
                    Rect(40, 6, 10, 10, 0.6f);
                    Poly(new[] { V(26, 30), V(36, 30), V(40, 44), V(32, 50), V(24, 44) }, 1.6f);
                    break;
                case IconShape.Coil:
                    for (int i = 0; i < 4; i++) Ring(32, 34, 24 - i * 4, 2.5f, 1f - i * 0.1f);
                    Rect(30, 6, 4, 14, 0.8f);
                    break;
                case IconShape.Chip:
                    Rect(16, 16, 32, 32, 0.45f);
                    for (int i = 0; i < 5; i++)
                    {
                        Rect(18 + i * 6, 8, 3, 8, 1.3f);
                        Rect(18 + i * 6, 48, 3, 8, 1.3f);
                        Rect(8, 18 + i * 6, 8, 3, 1.3f);
                        Rect(48, 18 + i * 6, 8, 3, 1.3f);
                    }
                    Rect(24, 24, 16, 16, 1.2f);
                    break;
                case IconShape.SolarPanel:
                    Poly(new[] { V(6, 20), V(48, 10), V(58, 40), V(16, 52) });
                    for (int i = 1; i < 4; i++) Line(V(6 + i * 3, 20 + i * 8), V(48 + i * 2.5f, 10 + i * 7.5f), 0.6f);
                    for (int i = 1; i < 4; i++) Line(V(6 + i * 10.5f, 20 - i * 2.5f), V(16 + i * 10.5f, 52 - i * 3f), 0.6f);
                    break;
                case IconShape.Battery:
                    Rect(8, 18, 44, 30, 0.75f);
                    Rect(52, 26, 6, 14, 0.5f);
                    Rect(12, 22, 30, 22, 1.5f);
                    break;
                case IconShape.Bag:
                    Poly(new[] { V(18, 18), V(46, 18), V(54, 54), V(10, 54) });
                    Poly(new[] { V(24, 8), V(40, 8), V(46, 18), V(18, 18) }, 0.7f);
                    Rect(24, 30, 16, 10, 1.35f);
                    break;
                case IconShape.Brick:
                    Poly(new[] { V(8, 26), V(40, 18), V(58, 26), V(26, 36) }, 1.3f);
                    Poly(new[] { V(8, 26), V(26, 36), V(26, 52), V(8, 42) }, 0.75f);
                    Poly(new[] { V(26, 36), V(58, 26), V(58, 42), V(26, 52) });
                    break;
                case IconShape.Toolbox:
                    Rect(8, 24, 48, 30, 0.9f);
                    Rect(8, 24, 48, 8, 1.3f);
                    Ring(32, 20, 9, 2.5f, 0.6f);
                    Rect(28, 32, 8, 6, 0.5f);
                    break;
                case IconShape.Flask:
                    Rect(26, 6, 12, 18, 0.9f);
                    Poly(new[] { V(26, 24), V(38, 24), V(54, 56), V(10, 56) });
                    Poly(new[] { V(18, 42), V(46, 42), V(54, 56), V(10, 56) }, 1.4f);
                    break;
                case IconShape.Gear:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Disc(32 + Mathf.Cos(a) * 20, 32 + Mathf.Sin(a) * 20, 6, 1f);
                    }
                    Disc(32, 32, 20, 1f);
                    Disc(32, 32, 8, 0.3f);
                    break;
                case IconShape.Snowflake:
                    for (int i = 0; i < 3; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        Line(V(32 + Mathf.Cos(a) * 24, 32 + Mathf.Sin(a) * 24), V(32 - Mathf.Cos(a) * 24, 32 - Mathf.Sin(a) * 24), 1f, 3f);
                    }
                    Disc(32, 32, 6, 1.3f);
                    break;
            }
            Outline();
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, name = "Icono" };
            tex.SetPixels32(_px);
            tex.Apply(false, true);
            return tex;
        }

        private static Vector2 V(float x, float y)
        {
            return new Vector2(x, y);
        }

        /// <summary>Color sombreado: más claro arriba, más oscuro abajo.</summary>
        private static Color32 Shade(int y, float tone)
        {
            float light = Mathf.Lerp(1.15f, 0.7f, (float)y / S) * tone;
            Color c = _base * light;
            c.a = 1f;
            return c;
        }

        // En la textura, y = 0 es la fila de abajo: invertimos para dibujar con y hacia abajo.
        private static void Put(int x, int y, float tone)
        {
            if (x < 0 || y < 0 || x >= S || y >= S) return;
            _px[(S - 1 - y) * S + x] = Shade(y, tone);
        }

        private static void Rect(float x, float y, float w, float h, float tone)
        {
            for (int yy = (int)y; yy < (int)(y + h); yy++)
            {
                for (int xx = (int)x; xx < (int)(x + w); xx++) Put(xx, yy, tone);
            }
        }

        private static void Disc(float cx, float cy, float r, float tone)
        {
            for (int y = (int)(cy - r); y <= (int)(cy + r); y++)
            {
                for (int x = (int)(cx - r); x <= (int)(cx + r); x++)
                {
                    if ((x - cx) * (x - cx) + (y - cy) * (y - cy) <= r * r) Put(x, y, tone);
                }
            }
        }

        private static void Ring(float cx, float cy, float r, float width, float tone)
        {
            for (int y = (int)(cy - r - width); y <= (int)(cy + r + width); y++)
            {
                for (int x = (int)(cx - r - width); x <= (int)(cx + r + width); x++)
                {
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (Mathf.Abs(d - r) <= width) Put(x, y, tone);
                }
            }
        }

        private static void Capsule(float cx, float cy, float halfW, float halfH)
        {
            Rect(cx - halfW, cy - halfH + halfW, halfW * 2f, (halfH - halfW) * 2f, 1f);
            Disc(cx, cy - halfH + halfW, halfW, 1f);
            Disc(cx, cy + halfH - halfW, halfW, 1f);
        }

        private static void Line(Vector2 a, Vector2 b, float tone, float width = 1.2f)
        {
            int steps = Mathf.CeilToInt(Vector2.Distance(a, b) * 2f);
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, (float)i / steps);
                Disc(p.x, p.y, width * 0.5f, tone);
            }
        }

        private static void Poly(Vector2[] pts, float tone = 1f)
        {
            float minY = float.MaxValue, maxY = float.MinValue;
            foreach (Vector2 p in pts) { minY = Mathf.Min(minY, p.y); maxY = Mathf.Max(maxY, p.y); }
            for (int y = (int)minY; y <= (int)maxY; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    if (Inside(pts, x + 0.5f, y + 0.5f)) Put(x, y, tone);
                }
            }
        }

        private static bool Inside(Vector2[] pts, float x, float y)
        {
            bool inside = false;
            for (int i = 0, j = pts.Length - 1; i < pts.Length; j = i++)
            {
                if ((pts[i].y > y) != (pts[j].y > y) && x < (pts[j].x - pts[i].x) * (y - pts[i].y) / (pts[j].y - pts[i].y) + pts[i].x) inside = !inside;
            }
            return inside;
        }

        /// <summary>Contorno oscuro de 1 píxel alrededor de lo pintado (da relieve al icono).</summary>
        private static void Outline()
        {
            var copy = (Color32[])_px.Clone();
            for (int y = 0; y < S; y++)
            {
                for (int x = 0; x < S; x++)
                {
                    if (copy[y * S + x].a > 0) continue;
                    bool near = false;
                    for (int dy = -1; dy <= 1 && !near; dy++)
                    {
                        for (int dx = -1; dx <= 1; dx++)
                        {
                            int nx = x + dx, ny = y + dy;
                            if (nx < 0 || ny < 0 || nx >= S || ny >= S) continue;
                            if (copy[ny * S + nx].a > 0) { near = true; break; }
                        }
                    }
                    if (near) _px[y * S + x] = new Color32(10, 15, 20, 220);
                }
            }
        }
    }
}
