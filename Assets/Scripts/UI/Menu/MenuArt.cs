using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.UI.Menu
{
    /// <summary>Variantes de los botones metálicos del menú.</summary>
    public enum MenuButtonKind
    {
        Orange, // JUGAR / acción principal
        Steel,  // OPCIONES / acciones normales
        Red     // SALIR (acero con brillo rojo)
    }

    /// <summary>
    /// Texturas del menú principal generadas por código (no necesita assets): fondo de Marte con
    /// la base, botones de acero con remaches, marcos de ventana e iconos.
    /// Para usar un fondo propio, pon una imagen en Assets/Resources/MenuBackground.png.
    /// </summary>
    public static class MenuArt
    {
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        private static Texture2D Get(string key, Func<Texture2D> make)
        {
            Texture2D t;
            if (!Cache.TryGetValue(key, out t) || t == null)
            {
                t = make();
                t.hideFlags = HideFlags.HideAndDontSave;
                Cache[key] = t;
            }
            return t;
        }

        // ------------------------------------------------------------------
        // Utilidades de dibujo (campos de distancia con antialias)
        // ------------------------------------------------------------------

        public static float SdRoundBox(Vector2 p, Vector2 half, float r)
        {
            float qx = Mathf.Abs(p.x) - half.x + r, qy = Mathf.Abs(p.y) - half.y + r;
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + outside - r;
        }

        private static float SdSegment(Vector2 p, Vector2 a, Vector2 b, float r)
        {
            Vector2 pa = p - a, ba = b - a;
            float h = Mathf.Clamp01(Vector2.Dot(pa, ba) / Vector2.Dot(ba, ba));
            return (pa - ba * h).magnitude - r;
        }

        private static float Cov(float d)
        {
            return Mathf.Clamp01(0.5f - d);
        }

        private static Color Over(Color dst, Color src)
        {
            float a = src.a + dst.a * (1f - src.a);
            if (a <= 0.0001f) return new Color(0f, 0f, 0f, 0f);
            float k = dst.a * (1f - src.a);
            return new Color((src.r * src.a + dst.r * k) / a, (src.g * src.a + dst.g * k) / a,
                (src.b * src.a + dst.b * k) / a, a);
        }

        private static Color WithA(Color c, float a)
        {
            c.a = a;
            return c;
        }

        private static float Hash(int x, int y)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        private static float Sq(float x)
        {
            return x * x;
        }

        private static Texture2D NewTex(int w, int h, Color[] px)
        {
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Bilinear;
            t.SetPixels(px);
            t.Apply(false, false);
            return t;
        }

        // ------------------------------------------------------------------
        // Básicos
        // ------------------------------------------------------------------

        /// <summary>Rectángulo redondeado blanco (se tiñe con GUI.color). Borde 9-slice = 12.</summary>
        public static Texture2D Rounded
        {
            get
            {
                return Get("rounded", () =>
                {
                    const int s = 32;
                    var px = new Color[s * s];
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = SdRoundBox(new Vector2(x + 0.5f - s * 0.5f, y + 0.5f - s * 0.5f), new Vector2(s * 0.5f, s * 0.5f), 10f);
                        px[y * s + x] = WithA(Color.white, Cov(d));
                    }
                    return NewTex(s, s, px);
                });
            }
        }

        /// <summary>Caja oscura con borde de color (campos de texto). Borde 9-slice = 12.</summary>
        public static Texture2D InputBox(bool focused)
        {
            return Get("input" + focused, () =>
            {
                const int s = 32;
                var px = new Color[s * s];
                Color border = focused ? new Color(1f, 0.6f, 0.2f) : new Color(0.45f, 0.47f, 0.5f);
                for (int y = 0; y < s; y++)
                for (int x = 0; x < s; x++)
                {
                    float d = SdRoundBox(new Vector2(x + 0.5f - s * 0.5f, y + 0.5f - s * 0.5f), new Vector2(s * 0.5f, s * 0.5f), 8f);
                    float a = Cov(d);
                    Color c = -d < 2f ? border : new Color(0.07f, 0.065f, 0.07f, 0.95f);
                    px[y * s + x] = WithA(c, a * c.a);
                }
                return NewTex(s, s, px);
            });
        }

        public static Texture2D Circle
        {
            get
            {
                return Get("circle", () =>
                {
                    const int s = 64;
                    var px = new Color[s * s];
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = new Vector2(x + 0.5f - 32f, y + 0.5f - 32f).magnitude - 30f;
                        px[y * s + x] = WithA(Color.white, Cov(d));
                    }
                    return NewTex(s, s, px);
                });
            }
        }

        public static Texture2D Ring
        {
            get
            {
                return Get("ring", () =>
                {
                    const int s = 64;
                    var px = new Color[s * s];
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = Mathf.Abs(new Vector2(x + 0.5f - 32f, y + 0.5f - 32f).magnitude - 27f) - 3f;
                        px[y * s + x] = WithA(Color.white, Cov(d));
                    }
                    return NewTex(s, s, px);
                });
            }
        }

        /// <summary>Rombo dorado con borde oscuro (zonas con datos reales en el mapa).</summary>
        public static Texture2D Diamond
        {
            get
            {
                return Get("diamond", () =>
                {
                    const int s = 32;
                    var px = new Color[s * s];
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        float d = (Mathf.Abs(x + 0.5f - 16f) + Mathf.Abs(y + 0.5f - 16f)) * 0.7071f;
                        float outer = Cov(d - 10.5f), inner = Cov(d - 7f);
                        Color c = Color.Lerp(new Color(0.15f, 0.08f, 0f, 0.95f), new Color(1f, 0.8f, 0.25f, 1f), inner);
                        px[y * s + x] = WithA(c, outer * c.a);
                    }
                    return NewTex(s, s, px);
                });
            }
        }

        /// <summary>Línea horizontal que se desvanece en los extremos (decoración del título).</summary>
        public static Texture2D FadeLine
        {
            get
            {
                return Get("fadeline", () =>
                {
                    const int w = 256, h = 8;
                    var px = new Color[w * h];
                    for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                    {
                        float u = (x + 0.5f) / w;
                        float edge = Mathf.SmoothStep(0f, 1f, Mathf.Min(u, 1f - u) * 3f);
                        float v = 1f - Mathf.Abs((y + 0.5f) / h - 0.5f) * 2f;
                        px[y * w + x] = WithA(Color.white, edge * Mathf.Clamp01(v * 1.6f));
                    }
                    return NewTex(w, h, px);
                });
            }
        }

        /// <summary>Degradado vertical transparente → negro (para oscurecer zonas del fondo).</summary>
        public static Texture2D VerticalShade
        {
            get
            {
                return Get("vshade", () =>
                {
                    const int h = 64;
                    var px = new Color[h];
                    for (int y = 0; y < h; y++) px[y] = new Color(0f, 0f, 0f, Mathf.Pow(1f - y / (h - 1f), 1.5f));
                    return NewTex(1, h, px);
                });
            }
        }

        // ------------------------------------------------------------------
        // Botones de acero
        // ------------------------------------------------------------------

        /// <summary>Margen transparente que deja el botón para su brillo exterior.</summary>
        public const int ButtonPad = 14;

        /// <summary>Botón metálico con placa central, remaches y brillo (estado 0 normal, 1 encima, 2 pulsado).</summary>
        public static Texture2D Button(int w, int h, MenuButtonKind kind, int state)
        {
            return Get("btn" + w + "x" + h + kind + state, () => MakeButton(w, h, kind, state));
        }

        private static Texture2D MakeButton(int w, int h, MenuButtonKind kind, int state)
        {
            Color plateTop, plateBottom, glow;
            switch (kind)
            {
                case MenuButtonKind.Orange:
                    plateTop = new Color(1f, 0.68f, 0.27f);
                    plateBottom = new Color(0.86f, 0.33f, 0.06f);
                    glow = new Color(1f, 0.55f, 0.1f, 0.85f);
                    break;
                case MenuButtonKind.Red:
                    plateTop = new Color(0.8f, 0.82f, 0.85f);
                    plateBottom = new Color(0.47f, 0.5f, 0.54f);
                    glow = new Color(1f, 0.16f, 0.08f, 0.9f);
                    break;
                default:
                    plateTop = new Color(0.8f, 0.82f, 0.85f);
                    plateBottom = new Color(0.47f, 0.5f, 0.54f);
                    glow = new Color(0f, 0f, 0f, 0f);
                    break;
            }
            if (state == 1)
            {
                plateTop = Color.Lerp(plateTop, Color.white, 0.18f);
                plateBottom = Color.Lerp(plateBottom, Color.white, 0.12f);
                glow.a = kind == MenuButtonKind.Steel ? 0.45f : 1f;
                if (kind == MenuButtonKind.Steel) glow = new Color(1f, 0.7f, 0.35f, 0.45f);
            }
            else if (state == 2)
            {
                plateTop *= 0.82f;
                plateBottom *= 0.82f;
            }
            plateTop.a = plateBottom.a = 1f;

            float pad = ButtonPad;
            float frameW = Mathf.Clamp(h * 0.08f, 5f, 11f);
            var px = new Color[w * h];
            var c = new Vector2(w * 0.5f, h * 0.5f);
            var fHalf = new Vector2(w * 0.5f - pad, h * 0.5f - pad);
            Vector2 pHalf = fHalf - new Vector2(frameW, frameW);
            float fr = Mathf.Min(16f, fHalf.y * 0.45f);
            float pr = Mathf.Min(9f, pHalf.y * 0.4f);
            bool rivets = pHalf.y >= 22f;
            Color metalDark = new Color(0.2f, 0.22f, 0.25f), metalLight = new Color(0.74f, 0.77f, 0.8f), edge = new Color(0.06f, 0.07f, 0.08f);

            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f) - c;
                float t = (y + 0.5f) / h;
                var col = new Color(0f, 0f, 0f, 0f);

                float dF = SdRoundBox(p, fHalf, fr);
                if (glow.a > 0f && dF > -2f)
                {
                    float side = Mathf.Clamp01((Mathf.Abs(p.x) - (fHalf.x - 70f)) / 60f);
                    float g = Mathf.Exp(-Mathf.Max(dF, 0f) / 5f) * glow.a * (0.3f + 0.7f * side);
                    col = WithA(glow, Mathf.Clamp01(g));
                }

                float aF = Cov(dF);
                if (aF > 0f)
                {
                    Color m = Color.Lerp(metalDark, metalLight, Mathf.SmoothStep(0f, 1f, t));
                    m *= 0.94f + 0.06f * Hash(x / 3, y); // acero cepillado
                    m = Color.Lerp(m, edge, Mathf.Clamp01(1f - (-dF) / 2.5f));
                    float bevel = Mathf.Clamp01(1f - Mathf.Abs(-dF - 3.5f) / 1.5f) * Mathf.Clamp01(p.y / fHalf.y);
                    m = Color.Lerp(m, Color.white, bevel * 0.35f);
                    col = Over(col, WithA(m, aF));
                }

                float dP = SdRoundBox(p, pHalf, pr);
                float groove = Mathf.Clamp01(1f - Mathf.Abs(dP - 1.5f) / 2.5f);
                if (groove > 0f && aF > 0f) col = Over(col, new Color(0f, 0f, 0f, groove * 0.55f));

                float aP = Cov(dP);
                if (aP > 0f)
                {
                    float tp = Mathf.Clamp01((p.y + pHalf.y) / (2f * pHalf.y));
                    Color pl = Color.Lerp(plateBottom, plateTop, tp);
                    if (p.y > 0f) pl = Color.Lerp(pl, Color.white, 0.1f * (p.y / pHalf.y));
                    pl = Color.Lerp(pl, Color.black, Mathf.Clamp01(1f - (-dP) / 2f) * 0.45f);
                    float hi = Mathf.Clamp01(1f - Mathf.Abs(-dP - 3f) / 1.2f) * Mathf.Clamp01(p.y / pHalf.y);
                    pl = Color.Lerp(pl, Color.white, hi * 0.35f);
                    pl *= 0.96f + 0.04f * Hash(x, y / 2);
                    col = Over(col, WithA(pl, aP));
                }

                if (rivets)
                {
                    for (int sx = -1; sx <= 1; sx += 2)
                    for (int sy = -1; sy <= 1; sy += 2)
                    {
                        var rc = new Vector2(sx * (pHalf.x - 10f), sy * (pHalf.y - 9f));
                        float ar = Cov((p - rc).magnitude - 3.2f);
                        if (ar <= 0f) continue;
                        float sh = Mathf.Clamp01(0.5f + (p.y - rc.y) / 6f - (p.x - rc.x) / 10f);
                        col = Over(col, WithA(Color.Lerp(new Color(0.25f, 0.25f, 0.27f), new Color(0.93f, 0.93f, 0.9f), sh), ar));
                    }
                }
                px[y * w + x] = col;
            }
            return NewTex(w, h, px);
        }

        // ------------------------------------------------------------------
        // Ventanas y tarjetas
        // ------------------------------------------------------------------

        /// <summary>Marco de ventana metálico con fondo oscuro. Usar con GUIStyle.border = 30.</summary>
        public static Texture2D Window
        {
            get
            {
                return Get("window", () =>
                {
                    const int s = 96;
                    var px = new Color[s * s];
                    var c = new Vector2(s * 0.5f, s * 0.5f);
                    for (int y = 0; y < s; y++)
                    for (int x = 0; x < s; x++)
                    {
                        var p = new Vector2(x + 0.5f, y + 0.5f) - c;
                        float d = SdRoundBox(p, new Vector2(s * 0.5f - 1f, s * 0.5f - 1f), 18f);
                        float a = Cov(d);
                        if (a <= 0f) continue;
                        float inside = -d;
                        Color col;
                        if (inside < 9f)
                        {
                            col = Color.Lerp(new Color(0.28f, 0.3f, 0.33f), new Color(0.6f, 0.63f, 0.67f), (p.y / (s * 0.5f)) * 0.5f + 0.5f);
                            col *= 0.94f + 0.06f * Hash(x / 3, y);
                            if (inside < 2f) col = Color.Lerp(col, new Color(0.05f, 0.05f, 0.06f), 1f - inside / 2f);
                            if (inside > 7.5f) col = Color.Lerp(col, Color.black, 0.6f);
                            col.a = 1f;
                        }
                        else
                        {
                            col = new Color(0.075f, 0.065f, 0.07f, 0.94f);
                        }
                        px[y * s + x] = WithA(col, a * col.a);
                    }
                    return NewTex(s, s, px);
                });
            }
        }

        // ------------------------------------------------------------------
        // Iconos (128 × 128)
        // ------------------------------------------------------------------

        private delegate float Sdf(Vector2 p);

        private static void Paint(Color[] px, int s, Sdf sdf, Color fill, Color outline, float ow)
        {
            for (int y = 0; y < s; y++)
            for (int x = 0; x < s; x++)
            {
                var p = new Vector2(x + 0.5f, y + 0.5f);
                float d = sdf(p);
                int i = y * s + x;
                if (ow > 0f)
                {
                    float ao = Cov(d - ow);
                    if (ao > 0f) px[i] = Over(px[i], WithA(outline, ao * outline.a));
                }
                float a = Cov(d);
                if (a > 0f) px[i] = Over(px[i], WithA(fill, a * fill.a));
            }
        }

        /// <summary>Rover (botón JUGAR).</summary>
        public static Texture2D IconRover
        {
            get
            {
                return Get("icon_rover", () =>
                {
                    const int s = 128;
                    var px = new Color[s * s];
                    var dark = new Color(0.16f, 0.12f, 0.1f);
                    Sdf body = p =>
                    {
                        float d = SdRoundBox(p - new Vector2(62f, 66f), new Vector2(36f, 12f), 4f);
                        d = Mathf.Min(d, SdRoundBox(p - new Vector2(50f, 84f), new Vector2(28f, 3f), 1.5f));
                        d = Mathf.Min(d, SdSegment(p, new Vector2(88f, 76f), new Vector2(93f, 104f), 3f));
                        d = Mathf.Min(d, SdRoundBox(p - new Vector2(94f, 108f), new Vector2(11f, 6f), 2f));
                        d = Mathf.Min(d, SdSegment(p, new Vector2(30f, 42f), new Vector2(46f, 62f), 3f));
                        d = Mathf.Min(d, SdSegment(p, new Vector2(62f, 42f), new Vector2(62f, 60f), 3f));
                        d = Mathf.Min(d, SdSegment(p, new Vector2(94f, 42f), new Vector2(80f, 62f), 3f));
                        d = Mathf.Min(d, (p - new Vector2(30f, 38f)).magnitude - 13f);
                        d = Mathf.Min(d, (p - new Vector2(62f, 38f)).magnitude - 13f);
                        d = Mathf.Min(d, (p - new Vector2(94f, 38f)).magnitude - 13f);
                        return d;
                    };
                    Paint(px, s, body, dark, new Color(1f, 0.85f, 0.6f, 0.35f), 2f);
                    Sdf hubs = p => Mathf.Min((p - new Vector2(30f, 38f)).magnitude, Mathf.Min((p - new Vector2(62f, 38f)).magnitude, (p - new Vector2(94f, 38f)).magnitude)) - 4.5f;
                    Paint(px, s, hubs, new Color(0.85f, 0.55f, 0.25f), Color.clear, 0f);
                    return NewTex(s, s, px);
                });
            }
        }

        /// <summary>Engranaje (botón OPCIONES).</summary>
        public static Texture2D IconGear
        {
            get
            {
                return Get("icon_gear", () =>
                {
                    const int s = 128;
                    var px = new Color[s * s];
                    Sdf gear = p =>
                    {
                        Vector2 q = p - new Vector2(64f, 64f);
                        float r = q.magnitude;
                        float ang = Mathf.Atan2(q.y, q.x);
                        float tooth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Cos(ang * 8f) - 0.15f) * 3f));
                        float d = r - (40f + 12f * tooth);
                        return Mathf.Max(d, 16f - r);
                    };
                    Paint(px, s, gear, new Color(1f, 0.7f, 0.2f), new Color(0.2f, 0.1f, 0.03f), 4f);
                    Sdf inner = p => Mathf.Abs((p - new Vector2(64f, 64f)).magnitude - 28f) - 2f;
                    Paint(px, s, inner, new Color(0.75f, 0.42f, 0.08f), Color.clear, 0f);
                    return NewTex(s, s, px);
                });
            }
        }

        /// <summary>Puerta con flecha (botón SALIR).</summary>
        public static Texture2D IconExit
        {
            get
            {
                return Get("icon_exit", () =>
                {
                    const int s = 128;
                    var px = new Color[s * s];
                    Sdf panel = p => SdRoundBox(p - new Vector2(46f, 64f), new Vector2(20f, 36f), 2f);
                    Paint(px, s, panel, new Color(0.45f, 0.06f, 0.04f), Color.clear, 0f);
                    Sdf frame = p =>
                    {
                        float d = Mathf.Abs(SdRoundBox(p - new Vector2(46f, 64f), new Vector2(26f, 42f), 4f)) - 4f;
                        d = Mathf.Min(d, SdSegment(p, new Vector2(56f, 64f), new Vector2(104f, 64f), 6f));
                        d = Mathf.Min(d, SdSegment(p, new Vector2(106f, 64f), new Vector2(88f, 82f), 6f));
                        d = Mathf.Min(d, SdSegment(p, new Vector2(106f, 64f), new Vector2(88f, 46f), 6f));
                        return d;
                    };
                    Paint(px, s, frame, new Color(0.95f, 0.18f, 0.1f), new Color(0.15f, 0.03f, 0.02f), 3f);
                    return NewTex(s, s, px);
                });
            }
        }

        /// <summary>Cubo isométrico (modo Creativo).</summary>
        public static Texture2D IconCube
        {
            get
            {
                return Get("icon_cube", () =>
                {
                    const int s = 128;
                    var px = new Color[s * s];
                    var c = new Vector2(64f, 64f);
                    var v = new Vector2[6];
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (30f + 60f * i) * Mathf.Deg2Rad;
                        v[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 50f;
                    }
                    Sdf cube = p =>
                    {
                        float d = float.MaxValue;
                        for (int i = 0; i < 6; i++) d = Mathf.Min(d, SdSegment(p, v[i], v[(i + 1) % 6], 4f));
                        d = Mathf.Min(d, SdSegment(p, c, v[0], 4f)); // 30°
                        d = Mathf.Min(d, SdSegment(p, c, v[2], 4f)); // 150°
                        d = Mathf.Min(d, SdSegment(p, c, v[4], 4f)); // 270°
                        return d;
                    };
                    Paint(px, s, cube, Color.white, new Color(0f, 0f, 0f, 0.5f), 2f);
                    return NewTex(s, s, px);
                });
            }
        }

        /// <summary>Casco de astronauta (modo Supervivencia).</summary>
        public static Texture2D IconHelmet
        {
            get
            {
                return Get("icon_helmet", () =>
                {
                    const int s = 128;
                    var px = new Color[s * s];
                    Sdf shell = p =>
                    {
                        float d = Mathf.Abs((p - new Vector2(64f, 70f)).magnitude - 42f) - 6f;
                        d = Mathf.Max(d, 30f - p.y);
                        d = Mathf.Min(d, SdRoundBox(p - new Vector2(64f, 24f), new Vector2(38f, 7f), 3f));
                        return d;
                    };
                    Paint(px, s, shell, Color.white, new Color(0f, 0f, 0f, 0.5f), 2f);
                    Sdf visor = p => SdRoundBox(p - new Vector2(64f, 70f), new Vector2(28f, 18f), 14f);
                    Paint(px, s, visor, new Color(1f, 0.62f, 0.25f), new Color(1f, 1f, 1f, 0.9f), 2.5f);
                    return NewTex(s, s, px);
                });
            }
        }

        // ------------------------------------------------------------------
        // Fondo del menú (paisaje marciano con la base)
        // ------------------------------------------------------------------

        public static Texture2D MenuBackground
        {
            get
            {
                return Get("menu_bg", () =>
                {
                    var custom = Resources.Load<Texture2D>("MenuBackground");
                    if (custom != null)
                    {
                        var copy = UnityEngine.Object.Instantiate(custom);
                        return copy;
                    }
                    return MakeBackground();
                });
            }
        }

        private static float Fbm1(float x, float seed)
        {
            return Mathf.PerlinNoise(x, seed) * 0.6f + Mathf.PerlinNoise(x * 2.1f, seed + 3.3f) * 0.3f +
                   Mathf.PerlinNoise(x * 4.3f, seed + 7.7f) * 0.1f - 0.5f;
        }

        private static Texture2D MakeBackground()
        {
            const int W = 960, H = 540;
            var px = new Color[W * H];
            Color zenith = new Color(0.2f, 0.09f, 0.07f), horizon = new Color(0.88f, 0.58f, 0.4f);
            var sun = new Vector2(0.7f * W, 0.66f * H);

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float u = x / (float)W, v = y / (float)H;

                // Cielo con el sol tras la bruma y nubes de polvo.
                float ts = Mathf.Clamp01((v - 0.4f) / 0.6f);
                Color col = Color.Lerp(horizon, zenith, Mathf.Pow(ts, 0.65f));
                float ds = (new Vector2(x, y) - sun).magnitude / H;
                col += new Color(1f, 0.78f, 0.55f) * (Mathf.Exp(-ds * ds / 0.004f) * 0.55f + Mathf.Exp(-ds / 0.22f) * 0.18f);
                float cloud = Mathf.PerlinNoise(u * 2.5f + 3.1f, v * 11f) * Mathf.PerlinNoise(u * 6f, v * 3f + 1.7f);
                col = Color.Lerp(col, new Color(0.95f, 0.72f, 0.55f), Mathf.SmoothStep(0.25f, 0.55f, cloud) * 0.18f * ts);

                // Fobos y Deimos.
                col = Moon(col, x, y, 0.3f * W, 0.86f * H, 11f, sun);
                col = Moon(col, x, y, 0.76f * W, 0.82f * H, 17f, sun);

                // Montañas lejanas, grandes y colinas cercanas.
                float far = 0.5f + 0.07f * Fbm1(u * 3f, 11f);
                float big = 0.43f + 0.26f * Mathf.Exp(-Sq((u - 0.2f) / 0.12f)) * (0.75f + 0.5f * Fbm1(u * 9f, 2f)) +
                            0.14f * Mathf.Exp(-Sq((u - 0.97f) / 0.09f)) + 0.05f * Fbm1(u * 6f, 5f);
                float near = 0.37f + 0.035f * Fbm1(u * 7f, 9f) + 0.05f * Mathf.Exp(-Sq((u - 0.85f) / 0.12f));
                if (v < far)
                {
                    Color m = Color.Lerp(new Color(0.66f, 0.4f, 0.29f), horizon, 0.45f);
                    col = m * (0.92f + 0.1f * Mathf.PerlinNoise(u * 30f, v * 30f));
                }
                if (v < big)
                {
                    float st = Mathf.PerlinNoise(u * 25f, v * 8f + u * 12f);
                    float slope = (Fbm1(u * 9f + 0.05f, 2f) - Fbm1(u * 9f, 2f)) * 6f;
                    Color m = Color.Lerp(new Color(0.4f, 0.18f, 0.11f), new Color(0.7f, 0.35f, 0.2f),
                        Mathf.Clamp01(0.45f + st * 0.4f + slope + (v - 0.4f)));
                    col = Color.Lerp(m, horizon, 0.18f);
                }
                if (v < near)
                {
                    float tg = Mathf.Clamp01(v / near);
                    Color g = Color.Lerp(new Color(0.3f, 0.13f, 0.07f), new Color(0.66f, 0.34f, 0.2f), Mathf.Pow(tg, 1.3f));
                    float n = Mathf.PerlinNoise(u * 80f, v * 160f);
                    float n2 = Mathf.PerlinNoise(u * 14f + 4f, v * 28f);
                    g *= 0.85f + 0.5f * n * n2;
                    if (n > 0.78f) g *= 0.7f;
                    col = g;
                }
                col = Color.Lerp(col, horizon, Mathf.Exp(-Mathf.Abs(v - 0.43f) / 0.04f) * 0.25f);
                px[y * W + x] = col;
            }

            DrawBase(px, W, H);

            for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float dx = (x / (float)W - 0.5f) * 1.2f, dy = y / (float)H - 0.5f;
                Color c = px[y * W + x] * (1f - 0.9f * (dx * dx + dy * dy));
                c.a = 1f;
                px[y * W + x] = c;
            }
            return NewTex(W, H, px);
        }

        private static Color Moon(Color col, int x, int y, float cx, float cy, float r, Vector2 sun)
        {
            float dx = x + 0.5f - cx, dy = y + 0.5f - cy;
            float d = Mathf.Sqrt(dx * dx + dy * dy);
            float a = Mathf.Clamp01(r + 0.5f - d);
            if (a <= 0f) return col;
            Vector2 l = (sun - new Vector2(cx, cy)).normalized;
            float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - Sq(d / r)));
            float lit = Mathf.Clamp01(Vector3.Dot(new Vector3(dx / r, dy / r, nz), new Vector3(l.x, l.y, 0.4f).normalized));
            Color m = new Color(0.78f, 0.7f, 0.62f) * (0.35f + 0.75f * lit) * (0.85f + 0.2f * Mathf.PerlinNoise(x * 0.4f, y * 0.4f));
            m.a = 1f;
            return Color.Lerp(col, m, a * 0.9f);
        }

        private static void Blend(Color[] px, int W, int H, int x, int y, Color c, float a)
        {
            if (x < 0 || y < 0 || x >= W || y >= H || a <= 0f) return;
            int i = y * W + x;
            px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(a));
        }

        /// <summary>Cúpulas, túnel, paneles solares y plataforma de aterrizaje (como en la portada).</summary>
        private static void DrawBase(Color[] px, int W, int H)
        {
            var lightDir = new Vector2(0.6f, 0.8f);

            // Plataforma de aterrizaje.
            for (int y = 50; y < 140; y++)
            for (int x = 150; x < 480; x++)
            {
                float e = Sq((x - 315f) / 125f) + Sq((y - 95f) / 28f);
                if (e > 1.05f) continue;
                float a = Mathf.Clamp01((1.05f - e) * 12f);
                Blend(px, W, H, x, y, new Color(0.45f, 0.41f, 0.39f), a);
                if (e > 0.5f && e < 0.62f) Blend(px, W, H, x, y, new Color(0.85f, 0.7f, 0.35f), a * 0.8f);
            }

            // Sombras de la base sobre el suelo.
            for (int y = 135; y < 165; y++)
            for (int x = 60; x < 330; x++)
            {
                float e = Sq((x - 195f) / 140f) + Sq((y - 148f) / 12f);
                if (e < 1f) Blend(px, W, H, x, y, new Color(0.15f, 0.06f, 0.03f), (1f - e) * 0.5f);
            }

            // Túnel que une las cúpulas.
            for (int y = 150; y < 166; y++)
            for (int x = 100; x < 292; x++)
            {
                float t = (y - 150f) / 16f;
                Color c = new Color(0.78f, 0.75f, 0.73f) * (0.55f + 0.55f * Mathf.Sin(t * Mathf.PI) * (0.6f + 0.4f * t));
                c.a = 1f;
                Blend(px, W, H, x, y, c, 1f);
                if (x % 24 == 0) Blend(px, W, H, x, y, new Color(0.3f, 0.28f, 0.27f), 0.6f);
            }

            // Cúpulas (cx, base, rx, ry).
            float[,] domes = { { 108f, 152f, 32f, 26f }, { 188f, 155f, 54f, 44f }, { 278f, 158f, 40f, 32f } };
            for (int k = 0; k < domes.GetLength(0); k++)
            {
                float cx = domes[k, 0], b = domes[k, 1], rx = domes[k, 2], ry = domes[k, 3];
                // zócalo
                for (int y = (int)b - 8; y < (int)b + 2; y++)
                for (int x = (int)(cx - rx); x <= (int)(cx + rx); x++)
                    Blend(px, W, H, x, y, new Color(0.42f, 0.4f, 0.4f), 1f);
                for (int y = (int)b; y <= (int)(b + ry + 1); y++)
                for (int x = (int)(cx - rx - 1); x <= (int)(cx + rx + 1); x++)
                {
                    float nx = (x - cx) / rx, ny = (y - b) / ry;
                    float e = nx * nx + ny * ny;
                    if (e > 1f) continue;
                    float nz = Mathf.Sqrt(Mathf.Max(0f, 1f - e));
                    float lit = Mathf.Clamp01(0.35f + 0.65f * (nx * lightDir.x * 0.6f + ny * lightDir.y * 0.5f + nz * 0.55f));
                    Color c = new Color(0.86f, 0.84f, 0.82f) * (0.45f + 0.6f * lit);
                    if (Mathf.Abs(ny - 0.35f) < 0.05f) c *= 0.6f;                           // franja
                    if (ny > 0.1f && ny < 0.28f && Mathf.Abs(nx) < 0.75f && Mathf.Repeat(x - cx, 9f) < 4f)
                        c = new Color(0.15f, 0.2f, 0.27f);                                    // ventanas
                    c.a = 1f;
                    Blend(px, W, H, x, y, c, Mathf.Clamp01((1f - e) * rx * 0.5f));
                }
            }

            // Paneles solares inclinados.
            for (int k = 0; k < 2; k++)
            {
                int x0 = 310 + k * 70, y0 = 172;
                for (int y = y0; y < y0 + 26; y++)
                for (int x = x0; x < x0 + 62; x++)
                {
                    int sx = x + (y - y0); // inclinación
                    bool grid = (x - x0) % 12 == 0 || (y - y0) % 8 == 0;
                    Color c = grid ? new Color(0.35f, 0.45f, 0.62f) : new Color(0.1f, 0.15f, 0.3f) * (0.8f + 0.4f * (y - y0) / 26f);
                    c.a = 1f;
                    Blend(px, W, H, sx, y, c, 1f);
                }
                for (int y = y0 - 14; y < y0; y++) Blend(px, W, H, x0 + 30, y, new Color(0.3f, 0.3f, 0.32f), 1f);
            }
        }
    }
}
