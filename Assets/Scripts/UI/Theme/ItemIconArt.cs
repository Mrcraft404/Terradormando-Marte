using System.Collections.Generic;
using MarsRobot.Items;
using UnityEngine;

namespace MarsRobot.UI
{
    /// <summary>
    /// Ilustraciones 2D de cada objeto (128 × 128, fondo transparente), pintadas por código con
    /// relieve, luz desde arriba a la izquierda, brillos especulares, texturas (grano, vetas,
    /// cristal) y sombra suave. Cada objeto tiene su propio dibujo reconocible: lingote, oblea de
    /// silicio, bombonas con su franja de color, placa de circuito, bobina de cable, rueda…
    /// Para usar imágenes propias, pon un PNG en Assets/Resources/ItemIcons/&lt;itemID&gt;.png.
    /// </summary>
    public static class ItemIconArt
    {
        private const int N = 128;
        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();
        private static Color[] _c;
        private static readonly Vector3 Light = new Vector3(-0.55f, 0.62f, 0.56f).normalized;

        private delegate float Sdf(float x, float y);
        private delegate Color Albedo(float x, float y);

        /// <summary>Icono del objeto, o null si no tiene ilustración propia.</summary>
        public static Texture2D Get(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return null;
            Texture2D t;
            if (Cache.TryGetValue(itemId, out t)) return t;
            t = Paint(itemId);
            Cache[itemId] = t;
            return t;
        }

        private static Texture2D Paint(string id)
        {
            _c = new Color[N * N];
            switch (id)
            {
                case ItemDatabase.Regolith: Mound(new Color(0.66f, 0.33f, 0.18f), new Color(0.45f, 0.2f, 0.1f), false); break;
                case ItemDatabase.PerchlorateRegolith: Mound(new Color(0.8f, 0.5f, 0.35f), new Color(0.55f, 0.28f, 0.16f), true); break;
                case ItemDatabase.IronOre: IronOre(); break;
                case ItemDatabase.Ice: IceChunk(); break;
                case ItemDatabase.Clays: Clay(); break;
                case ItemDatabase.Sulfates: Crystals(new Color(0.95f, 0.93f, 0.85f), 0.55f); break;
                case ItemDatabase.Sample: SampleTube(); break;
                case ItemDatabase.AuxBattery: BatteryPack(); break;
                case ItemDatabase.SpareParts: PartsBin(); break;
                case ItemDatabase.IronIngot: Ingot(new Color(0.42f, 0.38f, 0.37f), new Color(0.55f, 0.32f, 0.22f)); break;
                case ItemDatabase.Steel: SteelPlates(); break;
                case ItemDatabase.SiliconRefined: Wafer(); break;
                case ItemDatabase.Glass: GlassPane(); break;
                case ItemDatabase.Sulfur: Crystals(new Color(0.98f, 0.85f, 0.15f), 0.7f); break;
                case ItemDatabase.Carbon: Pellets(new Color(0.12f, 0.12f, 0.13f), 0.9f, 7f); break;
                case ItemDatabase.Co2: GasTank(new Color(0.55f, 0.57f, 0.6f), new Color(0.2f, 0.2f, 0.22f), false); break;
                case ItemDatabase.Water: WaterBottle(); break;
                case ItemDatabase.Oxygen: GasTank(new Color(0.9f, 0.92f, 0.94f), new Color(0.15f, 0.65f, 0.3f), false); break;
                case ItemDatabase.Methane: GasTank(new Color(0.9f, 0.4f, 0.12f), new Color(0.95f, 0.85f, 0.2f), true); break;
                case ItemDatabase.Polymer: Pellets(new Color(0.93f, 0.92f, 0.86f), 0.5f, 6f); break;
                case ItemDatabase.MetalParts: GearAndBolt(); break;
                case ItemDatabase.Wiring: WireSpool(); break;
                case ItemDatabase.Electronics: CircuitBoard(); break;
                case ItemDatabase.SolarCell: SolarCell(); break;
                case ItemDatabase.RegolithBrick: Brick(); break;
                case ItemDatabase.SealTape: TapeRoll(); break;
                case ItemDatabase.ResearchData: DataChip(); break;
                case ItemDatabase.RepairKit: RepairKit(); break;
                case ItemDatabase.SpareWheel: Wheel(); break;
                case ItemDatabase.CleaningBrush: Brush(); break;
                case ItemDatabase.PowerCell: PowerCell(); break;
                // Huerto y comida.
                case "semillas_lechuga": SeedBag(new Color(0.25f, 0.2f, 0.16f), 0, new Color(0.55f, 0.2f, 0.3f)); break;
                case "semillas_rabano": SeedBag(new Color(0.5f, 0.3f, 0.18f), 1, new Color(0.85f, 0.2f, 0.3f)); break;
                case "semillas_tomate": SeedBag(new Color(0.88f, 0.8f, 0.58f), 2, new Color(0.9f, 0.2f, 0.15f)); break;
                case "semillas_patata": SeedBag(new Color(0.72f, 0.56f, 0.34f), 3, new Color(0.65f, 0.5f, 0.3f)); break;
                case "semillas_trigo": SeedBag(new Color(0.85f, 0.66f, 0.3f), 4, new Color(0.9f, 0.75f, 0.3f)); break;
                case "semillas_soja": SeedBag(new Color(0.92f, 0.84f, 0.48f), 5, new Color(0.45f, 0.65f, 0.25f)); break;
                case "lechuga": Lettuce(); break;
                case "rabano": Radish(); break;
                case "tomate": Tomatoes(); break;
                case "patata": Potatoes(); break;
                case "trigo": Wheat(); break;
                case "soja": SoyPods(); break;
                case ItemDatabase.Nutrients: NutrientBottle(); break;
                case ItemDatabase.Flour: FlourSack(); break;
                case ItemDatabase.Ration: RationPack(); break;
                case ItemDatabase.Salad: SaladBowl(); break;
                // Piezas intercambiables del robot (taller de modificaciones).
                case "rover_PercussionDrill": Drill(); break;
                case "rover_Shovel": Shovel(); break;
                case "rover_DustBlower": Brush(); break;
                case "rover_HighGainAntenna": Antenna(); break;
                case "rover_ExtraSolarPanels": SolarCell(); break;
                case "rover_StorageBay": PartsBin(); break;
                case "rover_AuxBattery": BatteryPack(); break;
                case "rover_CryoContainer": IceChunk(); break;
                case "rover_SealedContainer": GasTank(new Color(0.6f, 0.62f, 0.66f), new Color(0.95f, 0.55f, 0.15f), false); break;
                case "rover_DualArmAdapter": GearAndBolt(); break;
                case "rover_StandardWheels": Wheel(); break;
                case "rover_AllTerrainTracks": Tracks(); break;
                case "rover_FastWheels": FastWheel(); break;
                case "rover_MineralScanner": Scanner(); break;
                case "rover_CargoArm": CargoArm(); break;
                default: return null;
            }
            var t = new Texture2D(N, N, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.SetPixels(_c);
            t.Apply(true, false);
            t.hideFlags = HideFlags.HideAndDontSave;
            t.name = "Icono " + id;
            return t;
        }

        // ==================================================================
        // Motor de pintado
        // ==================================================================

        /// <summary>
        /// Pinta una pieza con relieve: la altura sube desde el borde (bisel redondeado) más un
        /// relieve de ruido; la normal sale del gradiente de esa altura y se ilumina (difusa +
        /// especular). El borde se oscurece un poco para que se lea bien en tamaño pequeño.
        /// </summary>
        private static void Part(Sdf sdf, Albedo albedo, float bevel = 8f, float spec = 0.3f, float shine = 24f,
            float bump = 0f, float bumpScale = 0.15f, float alpha = 1f, float outline = 0.75f)
        {
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float d = sdf(px, py);
                float cov = Mathf.Clamp01(0.5f - d);
                if (cov <= 0f) continue;

                float hl = Height(sdf, px - 1f, py, bevel, bump, bumpScale), hr = Height(sdf, px + 1f, py, bevel, bump, bumpScale);
                float hd = Height(sdf, px, py - 1f, bevel, bump, bumpScale), hu = Height(sdf, px, py + 1f, bevel, bump, bumpScale);
                Vector3 n = new Vector3(hl - hr, hd - hu, 2f).normalized;
                float diff = Mathf.Max(0f, Vector3.Dot(n, Light));
                Vector3 refl = 2f * Vector3.Dot(n, Light) * n - Light;
                float s = spec * Mathf.Pow(Mathf.Max(0f, refl.z), shine);

                Color a = albedo(px, py);
                Color col = a * (0.38f + 0.78f * diff) + Color.white * s;
                float edge = Mathf.Clamp01(1f - (-d) / 1.8f);
                col = Color.Lerp(col, a * 0.3f, edge * outline);
                col.a = 1f;
                Blend(x, y, col, cov * alpha * a.a);
            }
        }

        private static float Height(Sdf sdf, float x, float y, float bevel, float bump, float scale)
        {
            float d = sdf(x, y);
            float h = bevel > 0f ? Mathf.Sqrt(Mathf.Clamp01(-d / bevel)) * bevel : 0f;
            if (bump > 0f) h += bump * Fbm(x * scale, y * scale);
            return h;
        }

        /// <summary>Pinta sin relieve (detalles planos: franjas, texto, líneas).</summary>
        private static void Flat(Sdf sdf, Color color, float alpha = 1f)
        {
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cov = Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f));
                if (cov > 0f) Blend(x, y, color, cov * alpha * color.a);
            }
        }

        /// <summary>Sombra suave en el suelo, debajo del objeto.</summary>
        private static void Shadow(float cx, float cy, float rx, float ry, float strength = 0.45f)
        {
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f - cx) / rx, dy = (y + 0.5f - cy) / ry;
                float e = dx * dx + dy * dy;
                if (e >= 1f) continue;
                Blend(x, y, Color.black, (1f - e) * (1f - e) * strength);
            }
        }

        /// <summary>Brillo puntual (reflejo de luz).</summary>
        private static void Glint(float cx, float cy, float r, float strength = 0.8f)
        {
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float d = new Vector2(x + 0.5f - cx, y + 0.5f - cy).magnitude / r;
                if (d < 1f) Blend(x, y, Color.white, (1f - d) * (1f - d) * strength);
            }
        }

        private static void Blend(int x, int y, Color src, float a)
        {
            if (a <= 0f) return;
            int i = y * N + x;
            Color dst = _c[i];
            float outA = a + dst.a * (1f - a);
            if (outA < 1e-4f) return;
            Color o = (src * a + dst * dst.a * (1f - a)) / outA;
            o.a = outA;
            _c[i] = o;
        }

        // ---- Distancias con signo ----

        private static float Circle(float x, float y, float cx, float cy, float r)
        {
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy)) - r;
        }

        private static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float dx = (x - cx) / rx, dy = (y - cy) / ry;
            float k = Mathf.Sqrt(dx * dx + dy * dy);
            return (k - 1f) * Mathf.Min(rx, ry);
        }

        private static float Box(float x, float y, float cx, float cy, float hx, float hy, float r = 0f, float angleDeg = 0f)
        {
            float px = x - cx, py = y - cy;
            if (angleDeg != 0f)
            {
                float a = -angleDeg * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
                float rx = px * ca - py * sa, ry = px * sa + py * ca;
                px = rx;
                py = ry;
            }
            float qx = Mathf.Abs(px) - hx + r, qy = Mathf.Abs(py) - hy + r;
            return Mathf.Min(Mathf.Max(qx, qy), 0f) + new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude - r;
        }

        private static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        /// <summary>Distancia con signo a un polígono (vértices en orden).</summary>
        private static float Poly(float x, float y, Vector2[] v)
        {
            var p = new Vector2(x, y);
            float d = Vector2.Dot(p - v[0], p - v[0]);
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                Vector2 e = v[j] - v[i], w = p - v[i];
                Vector2 b = w - e * Mathf.Clamp01(Vector2.Dot(w, e) / Vector2.Dot(e, e));
                d = Mathf.Min(d, Vector2.Dot(b, b));
                bool c1 = p.y >= v[i].y, c2 = p.y < v[j].y, c3 = e.x * w.y > e.y * w.x;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        /// <summary>Contorno irregular de roca (radio con ruido según el ángulo).</summary>
        private static float Blob(float x, float y, float cx, float cy, float r, float rough, int seed)
        {
            float dx = x - cx, dy = y - cy;
            float a = Mathf.Atan2(dy, dx);
            float k = 0f;
            for (int o = 1; o <= 4; o++) k += Mathf.Sin(a * (o * 2 + 1) + seed * 1.7f * o) / (o * 1.3f);
            return Mathf.Sqrt(dx * dx + dy * dy) - r * (1f + rough * k * 0.25f);
        }

        private static Vector2 V(float x, float y)
        {
            return new Vector2(x, y);
        }

        // ---- Ruido ----

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

        private static float Noise(float x, float y)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        private static float Fbm(float x, float y)
        {
            return Noise(x, y) * 0.55f + Noise(x * 2.1f, y * 2.1f) * 0.3f + Noise(x * 4.3f, y * 4.3f) * 0.15f;
        }

        private static Albedo Solid(Color c)
        {
            return (x, y) => c;
        }

        /// <summary>Color con grano (tierra, roca, metal fundido).</summary>
        private static Albedo Grain(Color a, Color b, float scale, float speckle = 0f, Color speck = default(Color))
        {
            return (x, y) =>
            {
                Color c = Color.Lerp(a, b, Fbm(x * scale, y * scale));
                if (speckle > 0f && Hash((int)x, (int)y * 3 + 7) < speckle) c = Color.Lerp(c, speck, 0.7f);
                return c;
            };
        }

        // ==================================================================
        // Minerales y materias primas
        // ==================================================================

        private static void Mound(Color light, Color dark, bool salts)
        {
            Shadow(64, 28, 58, 13);
            Sdf mound = (x, y) => Mathf.Max(Ellipse(x, y, 64, 28, 58, 70), 28f - y);
            Part(mound, Grain(light, dark, 0.22f, 0.06f, salts ? new Color(1f, 0.97f, 0.9f) : dark * 0.6f), 26f, 0.12f, 10f, 3f, 0.25f);
            // Guijarros.
            float[,] pebbles = { { 36, 42, 7 }, { 84, 50, 6 }, { 60, 74, 5 }, { 96, 36, 5 }, { 52, 34, 4 }, { 72, 90, 4 } };
            for (int i = 0; i < pebbles.GetLength(0); i++)
            {
                float cx = pebbles[i, 0], cy = pebbles[i, 1], r = pebbles[i, 2];
                int seed = i;
                Part((x, y) => Blob(x, y, cx, cy, r, 0.6f, seed), Grain(dark * 1.1f, dark * 0.7f, 0.5f), 3f, 0.25f, 16f);
            }
            if (salts)
            {
                // Señal de aviso: tóxico (percloratos).
                Part((x, y) => Poly(x, y, new[] { V(98, 120), V(122, 80), V(74, 80) }), Solid(new Color(1f, 0.82f, 0.1f)), 3f, 0.3f, 20f);
                Flat((x, y) => Box(x, y, 98, 100, 2.2f, 9f), new Color(0.1f, 0.08f, 0.05f));
                Flat((x, y) => Circle(x, y, 98, 87, 2.4f), new Color(0.1f, 0.08f, 0.05f));
            }
        }

        private static void IronOre()
        {
            Shadow(64, 26, 48, 11);
            Sdf rock = (x, y) => Blob(x, y, 64, 62, 44, 1.2f, 3);
            Albedo hematite = (x, y) =>
            {
                Color c = Color.Lerp(new Color(0.45f, 0.18f, 0.12f), new Color(0.28f, 0.12f, 0.1f), Fbm(x * 0.08f, y * 0.08f));
                float vein = Mathf.Abs(Mathf.Sin((x * 0.11f + y * 0.05f + Fbm(x * 0.05f, y * 0.05f) * 4f)));
                if (vein < 0.12f) c = Color.Lerp(c, new Color(0.62f, 0.6f, 0.62f), 0.7f); // vetas metálicas
                return c;
            };
            Part(rock, hematite, 16f, 0.45f, 30f, 5f, 0.12f);
            Glint(48, 82, 9f, 0.5f);
            Glint(80, 70, 5f, 0.4f);
        }

        private static void IceChunk()
        {
            Shadow(64, 24, 46, 10, 0.3f);
            var outline = new[] { V(24, 32), V(42, 18), V(86, 20), V(106, 40), V(100, 86), V(70, 108), V(34, 96), V(18, 66) };
            Albedo ice = (x, y) =>
            {
                Color c = Color.Lerp(new Color(0.55f, 0.8f, 0.95f), new Color(0.88f, 0.97f, 1f), Mathf.Clamp01((y - 20f) / 90f + Fbm(x * 0.06f, y * 0.06f) * 0.3f));
                return c;
            };
            Part((x, y) => Poly(x, y, outline), ice, 6f, 0.9f, 60f, 1f, 0.1f, 0.88f, 0.4f);
            // Facetas y grietas.
            Flat((x, y) => Poly(x, y, new[] { V(42, 18), V(86, 20), V(70, 56), V(48, 52) }), new Color(1f, 1f, 1f, 0.28f));
            Flat((x, y) => Poly(x, y, new[] { V(70, 56), V(106, 40), V(100, 86) }), new Color(0.3f, 0.55f, 0.75f, 0.25f));
            Flat((x, y) => Segment(x, y, 48, 52, 36, 86, 0.8f), new Color(1f, 1f, 1f, 0.7f));
            Flat((x, y) => Segment(x, y, 70, 56, 76, 92, 0.7f), new Color(1f, 1f, 1f, 0.6f));
            Glint(44, 88, 10f, 0.9f);
        }

        private static void Clay()
        {
            Shadow(64, 28, 50, 11);
            Sdf lump = (x, y) => Blob(x, y, 64, 60, 46, 0.6f, 7);
            Albedo bands = (x, y) =>
            {
                float t = Mathf.Sin((y + Fbm(x * 0.05f, y * 0.05f) * 10f) * 0.35f) * 0.5f + 0.5f;
                return Color.Lerp(new Color(0.48f, 0.55f, 0.42f), new Color(0.66f, 0.68f, 0.55f), t) * (0.9f + 0.2f * Fbm(x * 0.3f, y * 0.3f));
            };
            Part(lump, bands, 20f, 0.15f, 12f, 2f, 0.2f);
        }

        private static void Crystals(Color tint, float shine)
        {
            Shadow(64, 22, 46, 10, 0.35f);
            // Base de roca.
            Part((x, y) => Blob(x, y, 64, 28, 38, 0.5f, 2) , Grain(new Color(0.45f, 0.38f, 0.32f), new Color(0.3f, 0.25f, 0.22f), 0.3f), 8f, 0.1f);
            float[,] prisms = { { 64, 30, 10, 0, 64 }, { 44, 30, 8, 18, 46 }, { 84, 30, 8, -16, 50 }, { 30, 28, 6, 30, 30 }, { 98, 28, 6, -30, 32 } };
            for (int i = 0; i < prisms.GetLength(0); i++)
            {
                float bx = prisms[i, 0], by = prisms[i, 1], w = prisms[i, 2], ang = prisms[i, 3] * Mathf.Deg2Rad, len = prisms[i, 4];
                Vector2 dir = new Vector2(-Mathf.Sin(ang), Mathf.Cos(ang)), side = new Vector2(dir.y, -dir.x);
                Vector2 b0 = new Vector2(bx, by) + side * w, b1 = new Vector2(bx, by) - side * w;
                Vector2 t0 = b0 + dir * len, t1 = b1 + dir * len, tip = new Vector2(bx, by) + dir * (len + w * 1.4f);
                var pts = new[] { b0, t0, tip, t1, b1 };
                Albedo cr = (x, y) => Color.Lerp(tint * 0.75f, tint, Mathf.Clamp01(((x - bx) * side.x + (y - by) * side.y) / w * 0.5f + 0.5f));
                Part((x, y) => Poly(x, y, pts), cr, 3f, shine, 50f, 0f, 0f, 0.95f, 0.6f);
                Flat((x, y) => Segment(x, y, bx, by, tip.x, tip.y, 0.6f), new Color(1f, 1f, 1f, 0.35f));
            }
        }

        private static void SampleTube()
        {
            Shadow(64, 18, 30, 7, 0.35f);
            const float ang = -30f;
            Sdf tube = (x, y) => Box(x, y, 64, 62, 13, 46, 12, ang);
            Part((x, y) => Box(x, y, 64, 56, 9, 34, 8, ang), Grain(new Color(0.55f, 0.36f, 0.24f), new Color(0.35f, 0.22f, 0.15f), 0.25f), 6f, 0.15f); // testigo de roca
            Part(tube, Solid(new Color(0.85f, 0.95f, 1f)), 10f, 1.2f, 80f, 0f, 0f, 0.35f, 0.3f);                                                   // cristal
            Part((x, y) => Box(x, y, 64 - 46 * Mathf.Sin(ang * Mathf.Deg2Rad), 62 + 46 * Mathf.Cos(ang * Mathf.Deg2Rad), 15, 7, 3, ang), Solid(new Color(0.9f, 0.5f, 0.12f)), 4f, 0.4f, 30f); // tapón
            Flat((x, y) => Box(x, y, 58, 66, 2.2f, 34, 2, ang), new Color(1f, 1f, 1f, 0.55f));
        }

        // ==================================================================
        // Materiales procesados
        // ==================================================================

        private static void Ingot(Color metal, Color tint)
        {
            Shadow(64, 30, 54, 11);
            var front = new[] { V(14, 40), V(114, 40), V(102, 70), V(26, 70) };
            var top = new[] { V(26, 70), V(102, 70), V(92, 88), V(36, 88) };
            Albedo m = (x, y) => Color.Lerp(metal, tint, Fbm(x * 0.07f, y * 0.07f) * 0.6f);
            Part((x, y) => Poly(x, y, front), m, 6f, 0.6f, 30f, 0.6f, 0.4f);
            Part((x, y) => Poly(x, y, top), (x, y) => m(x, y) * 1.25f, 4f, 0.9f, 40f, 0.4f, 0.4f);
            Flat((x, y) => Segment(x, y, 40, 84, 88, 84, 1.2f), new Color(1f, 1f, 1f, 0.35f));
        }

        private static void SteelPlates()
        {
            Shadow(64, 26, 54, 10);
            for (int k = 2; k >= 0; k--)
            {
                float oy = 30 + k * 16, ox = k * 4;
                var top = new[] { V(14 + ox, oy + 10), V(98 + ox, oy + 10), V(116 + ox, oy + 30), V(32 + ox, oy + 30) };
                var front = new[] { V(14 + ox, oy + 4), V(98 + ox, oy + 4), V(98 + ox, oy + 10), V(14 + ox, oy + 10) };
                Albedo brushed = (x, y) => new Color(0.66f, 0.69f, 0.73f) * (0.92f + 0.12f * Noise(x * 0.05f, y * 2.5f));
                Part((x, y) => Poly(x, y, front), Solid(new Color(0.4f, 0.42f, 0.45f)), 1f, 0.2f);
                Part((x, y) => Poly(x, y, top), brushed, 2f, 0.9f, 40f);
                float hx = 30 + ox, hy = oy + 20;
                Flat((x, y) => Ellipse(x, y, hx, hy, 3, 1.6f), new Color(0.2f, 0.2f, 0.22f));
                Flat((x, y) => Ellipse(x, y, hx + 64, hy, 3, 1.6f), new Color(0.2f, 0.2f, 0.22f));
            }
        }

        private static void Wafer()
        {
            Shadow(64, 22, 50, 9, 0.35f);
            Sdf disc = (x, y) => Mathf.Max(Ellipse(x, y, 64, 64, 52, 52), y - 110f);
            Albedo irid = (x, y) =>
            {
                float t = Mathf.Sin((x + y) * 0.05f) * 0.5f + 0.5f;
                Color c = Color.Lerp(new Color(0.25f, 0.3f, 0.6f), new Color(0.55f, 0.35f, 0.65f), t);
                bool grid = Mathf.Repeat(x - 4, 13f) < 1.2f || Mathf.Repeat(y - 4, 13f) < 1.2f;
                return grid ? c * 0.55f : c;
            };
            Part(disc, irid, 3f, 1.1f, 70f);
            Glint(44, 86, 16f, 0.45f);
        }

        private static void GlassPane()
        {
            Shadow(64, 20, 46, 8, 0.25f);
            var pane = new[] { V(28, 20), V(100, 30), V(104, 108), V(24, 100) };
            Part((x, y) => Poly(x, y, pane), Solid(new Color(0.6f, 0.88f, 0.9f)), 3f, 1.4f, 90f, 0f, 0f, 0.45f, 0.9f);
            Flat((x, y) => Segment(x, y, 40, 40, 62, 92, 3f), new Color(1f, 1f, 1f, 0.5f));
            Flat((x, y) => Segment(x, y, 56, 36, 74, 76, 1.6f), new Color(1f, 1f, 1f, 0.4f));
        }

        private static void Pellets(Color c, float shine, float r)
        {
            Shadow(64, 26, 50, 10, 0.35f);
            var rnd = new System.Random(5);
            for (int i = 0; i < 46; i++)
            {
                float a = (float)rnd.NextDouble() * Mathf.PI * 2f, rr = Mathf.Sqrt((float)rnd.NextDouble());
                float px = 64 + Mathf.Cos(a) * rr * 44f, py = 30 + (float)rnd.NextDouble() * 50f * (1f - Mathf.Abs(px - 64) / 60f);
                float pr = r * (0.8f + 0.4f * (float)rnd.NextDouble());
                Color pc = c * (0.85f + 0.3f * (float)rnd.NextDouble());
                pc.a = 1f;
                Part((x, y) => Ellipse(x, y, px, py, pr * 1.1f, pr), Solid(pc), pr, shine, 30f);
            }
        }

        private static void GearAndBolt()
        {
            Shadow(64, 22, 50, 10);
            Albedo steel = (x, y) => new Color(0.68f, 0.7f, 0.74f) * (0.95f + 0.08f * Noise(x * 0.3f, y * 0.3f));
            Sdf gear = (x, y) =>
            {
                float dx = x - 54, dy = y - 66;
                float r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                float teeth = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Mathf.Cos(a * 10f) - 0.1f) * 3f));
                return Mathf.Max(r - (34f + 8f * teeth), 12f - r);
            };
            Part(gear, steel, 6f, 0.8f, 35f);
            // Tornillo y tuerca.
            Part((x, y) => Segment(x, y, 86, 34, 104, 82, 5f), Solid(new Color(0.6f, 0.62f, 0.66f)), 4f, 0.9f, 40f);
            for (int k = 0; k < 6; k++)
            {
                float t = 0.15f + k * 0.13f, sx = Mathf.Lerp(86, 104, t), sy = Mathf.Lerp(34, 82, t);
                Flat((x, y) => Segment(x, y, sx - 5, sy + 2, sx + 5, sy - 2, 0.7f), new Color(0.3f, 0.3f, 0.32f));
            }
            var hex = new Vector2[6];
            for (int i = 0; i < 6; i++) hex[i] = new Vector2(106 + Mathf.Cos(i * Mathf.PI / 3f) * 13f, 88 + Mathf.Sin(i * Mathf.PI / 3f) * 13f);
            Part((x, y) => Poly(x, y, hex), steel, 4f, 0.9f, 40f);
        }

        private static void WireSpool()
        {
            Shadow(64, 22, 46, 9);
            Part((x, y) => Ellipse(x, y, 64, 64, 46, 46), Solid(new Color(0.35f, 0.35f, 0.38f)), 4f, 0.3f);     // carrete
            Albedo copper = (x, y) =>
            {
                float r = new Vector2(x - 64, y - 64).magnitude;
                float ring = Mathf.Sin(r * 1.6f) * 0.5f + 0.5f;
                return Color.Lerp(new Color(0.72f, 0.38f, 0.18f), new Color(0.95f, 0.62f, 0.35f), ring);
            };
            Part((x, y) => Mathf.Max(Circle(x, y, 64, 64, 40), 14f - new Vector2(x - 64, y - 64).magnitude), copper, 5f, 1f, 40f);
            Part((x, y) => Circle(x, y, 64, 64, 13), Solid(new Color(0.2f, 0.2f, 0.22f)), 3f, 0.2f);
            Part((x, y) => Segment(x, y, 100, 46, 118, 22, 3.5f), Solid(new Color(0.9f, 0.2f, 0.15f)), 3f, 0.5f, 25f); // extremo aislado
            Flat((x, y) => Circle(x, y, 118, 22, 2.2f), new Color(0.95f, 0.62f, 0.35f));
        }

        private static void CircuitBoard()
        {
            Shadow(64, 20, 52, 9, 0.35f);
            var board = new[] { V(14, 30), V(108, 22), V(116, 96), V(22, 106) };
            Part((x, y) => Poly(x, y, board), Solid(new Color(0.08f, 0.42f, 0.2f)), 2f, 0.5f, 30f);
            for (int k = 0; k < 7; k++)
            {
                float y0 = 36 + k * 9;
                Flat((x, y) => Segment(x, y, 26, y0, 104, y0 - 6 + (k % 2) * 4, 0.7f), new Color(0.75f, 0.65f, 0.2f, 0.8f));
            }
            Part((x, y) => Box(x, y, 62, 66, 18, 14, 2), Solid(new Color(0.1f, 0.1f, 0.11f)), 2f, 0.6f, 40f);  // chip principal
            for (int k = -3; k <= 3; k++)
            {
                float kx = 62 + k * 4.8f;
                Flat((x, y) => Box(x, y, kx, 49, 1.2f, 3f), new Color(0.82f, 0.82f, 0.85f));
                Flat((x, y) => Box(x, y, kx, 83, 1.2f, 3f), new Color(0.82f, 0.82f, 0.85f));
            }
            Part((x, y) => Box(x, y, 96, 76, 7, 5, 1), Solid(new Color(0.15f, 0.15f, 0.17f)), 2f, 0.5f);
            Part((x, y) => Circle(x, y, 34, 84, 6), Solid(new Color(0.15f, 0.3f, 0.75f)), 3f, 0.8f, 40f);    // condensador
            for (int k = 0; k < 8; k++)
            {
                float gx = 30 + k * 9.5f;
                Flat((x, y) => Box(x, y, gx, 31 - k * 0.8f, 3f, 3.5f), new Color(0.95f, 0.75f, 0.25f));     // contactos dorados
            }
        }

        private static void SolarCell()
        {
            Shadow(64, 20, 52, 9, 0.35f);
            var cell = new[] { V(16, 34), V(100, 22), V(114, 96), V(28, 108) };
            Albedo blue = (x, y) => Color.Lerp(new Color(0.08f, 0.15f, 0.42f), new Color(0.15f, 0.28f, 0.62f), Fbm(x * 0.04f, y * 0.04f));
            Part((x, y) => Poly(x, y, cell), blue, 2f, 1.1f, 60f);
            for (int k = 1; k <= 3; k++)
            {
                float t = k / 4f;
                Vector2 a = Vector2.Lerp(V(16, 34), V(100, 22), t), b = Vector2.Lerp(V(28, 108), V(114, 96), t);
                Flat((x, y) => Segment(x, y, a.x, a.y, b.x, b.y, 1.1f), new Color(0.85f, 0.87f, 0.9f));
            }
            for (int k = 1; k < 12; k++)
            {
                float t = k / 12f;
                Vector2 a = Vector2.Lerp(V(16, 34), V(28, 108), t), b = Vector2.Lerp(V(100, 22), V(114, 96), t);
                Flat((x, y) => Segment(x, y, a.x, a.y, b.x, b.y, 0.35f), new Color(0.7f, 0.75f, 0.85f, 0.6f));
            }
            Glint(44, 86, 14f, 0.35f);
        }

        private static void Brick()
        {
            Shadow(64, 24, 54, 10);
            var front = new[] { V(14, 30), V(84, 30), V(84, 70), V(14, 70) };
            var side = new[] { V(84, 30), V(114, 46), V(114, 86), V(84, 70) };
            var top = new[] { V(14, 70), V(84, 70), V(114, 86), V(44, 86) };
            Albedo clay = Grain(new Color(0.7f, 0.36f, 0.22f), new Color(0.5f, 0.24f, 0.15f), 0.2f, 0.08f, new Color(0.85f, 0.7f, 0.55f));
            Part((x, y) => Poly(x, y, front), clay, 3f, 0.1f, 10f, 1.5f, 0.3f);
            Part((x, y) => Poly(x, y, side), (x, y) => clay(x, y) * 0.75f, 3f, 0.1f, 10f, 1.5f, 0.3f);
            Part((x, y) => Poly(x, y, top), (x, y) => clay(x, y) * 1.15f, 3f, 0.1f, 10f, 1.5f, 0.3f);
        }

        private static void TapeRoll()
        {
            Shadow(64, 24, 46, 10);
            Part((x, y) => Ellipse(x, y, 64, 62, 44, 40), Solid(new Color(0.92f, 0.55f, 0.15f)), 6f, 0.6f, 25f);
            Part((x, y) => Ellipse(x, y, 64, 62, 24, 22), Solid(new Color(0.75f, 0.68f, 0.55f)), 3f, 0.2f);     // núcleo de cartón
            Flat((x, y) => Ellipse(x, y, 64, 62, 18, 16), new Color(0f, 0f, 0f, 0f));
            for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
                if (Ellipse(x + 0.5f, y + 0.5f, 64, 62, 17, 15) < 0f) _c[y * N + x] = new Color(0, 0, 0, 0); // hueco
            Part((x, y) => Box(x, y, 104, 40, 14, 4, 1, -20f), Solid(new Color(0.92f, 0.55f, 0.15f)), 2f, 0.5f); // tira despegada
        }

        private static void DataChip()
        {
            Shadow(64, 22, 42, 9, 0.35f);
            var card = new[] { V(28, 22), V(92, 22), V(104, 34), V(104, 106), V(28, 106) };
            Part((x, y) => Poly(x, y, card), Solid(new Color(0.15f, 0.17f, 0.2f)), 3f, 0.6f, 40f);
            Part((x, y) => Box(x, y, 64, 70, 22, 22, 3), Solid(new Color(0.25f, 0.85f, 0.45f)), 2f, 0.8f, 40f);
            for (int k = 0; k < 6; k++)
            {
                float gx = 38 + k * 10;
                Flat((x, y) => Box(x, y, gx, 30, 3.2f, 6f), new Color(0.95f, 0.78f, 0.3f));
            }
            Glint(52, 82, 10f, 0.5f);
        }

        // ==================================================================
        // Contenedores de gas y líquido
        // ==================================================================

        private static void GasTank(Color body, Color band, bool flame)
        {
            Shadow(64, 14, 30, 7);
            Sdf tank = (x, y) => Box(x, y, 64, 58, 26, 46, 24);
            Albedo b = (x, y) => body * (0.9f + 0.15f * Noise(x * 0.1f, y * 0.4f));
            Part(tank, b, 24f, 0.8f, 40f);
            Part((x, y) => Mathf.Max(tank(x, y), Mathf.Abs(y - 80f) - 7f), Solid(band), 24f, 0.6f, 40f);    // franja de color
            Part((x, y) => Box(x, y, 64, 108, 8, 6, 2), Solid(new Color(0.55f, 0.56f, 0.6f)), 3f, 0.9f, 40f); // válvula
            Part((x, y) => Box(x, y, 64, 117, 13, 3, 2), Solid(new Color(0.25f, 0.25f, 0.28f)), 2f, 0.5f);
            if (flame)
            {
                Part((x, y) => Mathf.Max(Circle(x, y, 64, 44, 10), -Circle(x, y, 70, 52, 8)), Solid(new Color(1f, 0.9f, 0.3f)), 3f, 0.3f);
                Flat((x, y) => Poly(x, y, new[] { V(58, 44), V(64, 66), V(70, 44) }), new Color(1f, 0.85f, 0.25f));
            }
            Glint(50, 70, 9f, 0.55f);
        }

        private static void WaterBottle()
        {
            Shadow(64, 14, 32, 7, 0.35f);
            Sdf bottle = (x, y) => Mathf.Min(Box(x, y, 64, 56, 28, 44, 14), Box(x, y, 64, 104, 12, 10, 4));
            Part(bottle, Solid(new Color(0.75f, 0.88f, 1f)), 10f, 1.2f, 70f, 0f, 0f, 0.45f, 0.5f);           // plástico transparente
            Part((x, y) => Mathf.Max(Box(x, y, 64, 52, 25, 38, 12), y - 78f), Solid(new Color(0.2f, 0.5f, 0.95f)), 8f, 0.9f, 50f, 0f, 0f, 0.8f); // agua
            Part((x, y) => Box(x, y, 64, 116, 13, 6, 3), Solid(new Color(0.15f, 0.35f, 0.8f)), 3f, 0.6f, 30f); // tapón
            Flat((x, y) => Box(x, y, 64, 58, 22, 9, 2), new Color(1f, 1f, 1f, 0.85f));                        // etiqueta
            Flat((x, y) => Mathf.Max(Circle(x, y, 64, 57, 5), -Circle(x, y, 64, 64, 2)), new Color(0.2f, 0.5f, 0.95f)); // gota
            Flat((x, y) => Poly(x, y, new[] { V(60, 58), V(64, 66), V(68, 58) }), new Color(0.2f, 0.5f, 0.95f));
            Flat((x, y) => Box(x, y, 46, 62, 3, 28, 2), new Color(1f, 1f, 1f, 0.45f));
        }

        // ==================================================================
        // Componentes y consumibles
        // ==================================================================

        private static void BatteryPack()
        {
            Shadow(64, 22, 50, 10);
            var front = new[] { V(16, 28), V(96, 28), V(96, 82), V(16, 82) };
            var side = new[] { V(96, 28), V(114, 40), V(114, 94), V(96, 82) };
            var top = new[] { V(16, 82), V(96, 82), V(114, 94), V(34, 94) };
            Part((x, y) => Poly(x, y, front), Solid(new Color(0.18f, 0.55f, 0.3f)), 3f, 0.4f, 30f);
            Part((x, y) => Poly(x, y, side), Solid(new Color(0.12f, 0.4f, 0.22f)), 3f, 0.3f);
            Part((x, y) => Poly(x, y, top), Solid(new Color(0.25f, 0.65f, 0.38f)), 3f, 0.5f, 30f);
            Part((x, y) => Box(x, y, 42, 92, 7, 5, 1), Solid(new Color(0.85f, 0.2f, 0.15f)), 2f, 0.6f, 30f);
            Part((x, y) => Box(x, y, 78, 92, 7, 5, 1), Solid(new Color(0.2f, 0.2f, 0.22f)), 2f, 0.6f, 30f);
            Flat((x, y) => Mathf.Min(Box(x, y, 56, 55, 14, 3), Box(x, y, 56, 55, 3, 14)), new Color(1f, 1f, 1f, 0.85f));
        }

        private static void PartsBin()
        {
            Shadow(64, 22, 52, 10);
            var bin = new[] { V(18, 26), V(110, 26), V(118, 70), V(10, 70) };
            Part((x, y) => Poly(x, y, bin), Solid(new Color(0.25f, 0.42f, 0.7f)), 4f, 0.4f, 30f);
            // Piezas asomando: engranaje pequeño, muelle y tornillos.
            Part((x, y) => { float dx = x - 46, dy = y - 74, r = Mathf.Sqrt(dx * dx + dy * dy); return Mathf.Max(r - 18f - 4f * Mathf.Clamp01(Mathf.Cos(Mathf.Atan2(dy, dx) * 8f) * 3f), 6f - r); },
                Solid(new Color(0.7f, 0.72f, 0.76f)), 4f, 0.8f, 35f);
            Part((x, y) => Segment(x, y, 78, 66, 96, 100, 4f), Solid(new Color(0.65f, 0.66f, 0.7f)), 3f, 0.9f, 40f);
            for (int k = 0; k < 5; k++)
            {
                float sy = 72 + k * 6;
                Flat((x, y) => Segment(x, y, 84, sy, 96, sy + 3, 1.2f), new Color(0.82f, 0.62f, 0.25f));
            }
            Part((x, y) => Poly(x, y, new[] { V(10, 70), V(118, 70), V(114, 60), V(14, 60) }), Solid(new Color(0.2f, 0.35f, 0.6f)), 2f, 0.3f);
        }

        private static void RepairKit()
        {
            Shadow(64, 20, 52, 10);
            Part((x, y) => Box(x, y, 64, 58, 50, 36, 8), Solid(new Color(0.92f, 0.5f, 0.12f)), 8f, 0.5f, 30f);
            Part((x, y) => Box(x, y, 64, 99, 18, 6, 4), Solid(new Color(0.25f, 0.25f, 0.27f)), 3f, 0.5f);           // asa
            Flat((x, y) => Box(x, y, 64, 72, 50, 1.2f), new Color(0.45f, 0.22f, 0.05f));                              // junta de la tapa
            Flat((x, y) => Mathf.Min(Box(x, y, 64, 54, 18, 6, 1), Box(x, y, 64, 54, 6, 18, 1)), new Color(1f, 1f, 1f));
            Part((x, y) => Box(x, y, 24, 58, 4, 8, 1), Solid(new Color(0.75f, 0.76f, 0.8f)), 2f, 0.9f, 40f);         // cierres
            Part((x, y) => Box(x, y, 104, 58, 4, 8, 1), Solid(new Color(0.75f, 0.76f, 0.8f)), 2f, 0.9f, 40f);
        }

        private static void Wheel()
        {
            Shadow(64, 12, 50, 8);
            Albedo alu = (x, y) => new Color(0.72f, 0.74f, 0.78f) * (0.9f + 0.15f * Noise(x * 0.2f, y * 0.2f));
            Sdf tire = (x, y) =>
            {
                float dx = x - 64, dy = y - 64, r = Mathf.Sqrt(dx * dx + dy * dy), a = Mathf.Atan2(dy, dx);
                float grouser = Mathf.Clamp01((Mathf.Cos(a * 24f) - 0.3f) * 4f) * 4f;      // garras de la rueda
                return Mathf.Max(r - (50f + grouser), 38f - r);
            };
            Part(tire, Solid(new Color(0.3f, 0.31f, 0.34f)), 6f, 0.5f, 30f);
            Part((x, y) => Mathf.Max(Circle(x, y, 64, 64, 39), 34f - new Vector2(x - 64, y - 64).magnitude), alu, 3f, 0.8f, 40f); // llanta
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3f + 0.3f;
                float ex = 64 + Mathf.Cos(a) * 34f, ey = 64 + Mathf.Sin(a) * 34f;
                Part((x, y) => Segment(x, y, 64, 64, ex, ey, 3.2f), alu, 2f, 0.8f, 40f);                         // radios flexibles
            }
            Part((x, y) => Circle(x, y, 64, 64, 12), Solid(new Color(0.4f, 0.4f, 0.44f)), 5f, 0.9f, 40f);         // buje
            Part((x, y) => Circle(x, y, 64, 64, 5), Solid(new Color(0.85f, 0.5f, 0.15f)), 2f, 0.6f);
        }

        private static void Brush()
        {
            Shadow(64, 18, 50, 8, 0.35f);
            const float ang = 35f;
            Part((x, y) => Box(x, y, 50, 52, 9, 40, 8, ang), Solid(new Color(0.2f, 0.45f, 0.85f)), 6f, 0.7f, 40f);   // mango
            Part((x, y) => Box(x, y, 28, 86, 7, 5, 2, ang), Solid(new Color(0.15f, 0.15f, 0.17f)), 2f, 0.3f);       // agujero para colgar
            Part((x, y) => Box(x, y, 84, 38, 26, 12, 4, ang), Solid(new Color(0.35f, 0.38f, 0.42f)), 4f, 0.7f, 40f); // cabeza
            for (int k = -5; k <= 5; k++)
            {
                float a = ang * Mathf.Deg2Rad;
                float bx = 84 + Mathf.Cos(a) * k * 4.4f + Mathf.Sin(a) * 12f, by = 38 + Mathf.Sin(a) * k * 4.4f - Mathf.Cos(a) * 12f;
                float tx = bx + Mathf.Sin(a) * 16f, ty = by - Mathf.Cos(a) * 16f;
                Part((x, y) => Segment(x, y, bx, by, tx, ty, 1.5f), Solid(new Color(0.75f, 0.92f, 1f)), 1f, 0.6f, 30f); // cerdas
            }
        }

        // ==================================================================
        // Piezas del robot
        // ==================================================================

        private static readonly Color RoverOrange = new Color(0.95f, 0.55f, 0.15f);
        private static readonly Color RoverSteel = new Color(0.7f, 0.72f, 0.76f);

        /// <summary>Taladro: cuerpo naranja y broca en espiral apuntando abajo a la derecha.</summary>
        private static void Drill()
        {
            Shadow(64, 16, 48, 8);
            Part((x, y) => Box(x, y, 46, 78, 26, 18, 8, -30f), Solid(RoverOrange), 7f, 0.6f, 30f);            // motor
            Part((x, y) => Box(x, y, 30, 96, 10, 8, 3, -30f), Solid(new Color(0.25f, 0.25f, 0.28f)), 3f, 0.4f); // tapa
            Part((x, y) => Segment(x, y, 66, 64, 108, 22, 9f), Solid(RoverSteel), 4f, 0.9f, 40f);              // broca
            for (int k = 0; k < 6; k++)
            {
                float t = 0.12f + k * 0.14f, cx = Mathf.Lerp(66, 108, t), cy = Mathf.Lerp(64, 22, t);
                Flat((x, y) => Segment(x, y, cx - 7, cy - 5, cx + 5, cy + 7, 1.6f), new Color(0.35f, 0.36f, 0.4f));
            }
            Part((x, y) => Poly(x, y, new[] { V(102, 30), V(116, 14), V(112, 26) }), Solid(RoverSteel), 2f, 0.9f, 40f);
        }

        /// <summary>Pala: mango y cuchara metálica.</summary>
        private static void Shovel()
        {
            Shadow(64, 16, 46, 8);
            Part((x, y) => Segment(x, y, 30, 104, 70, 56, 5f), Solid(new Color(0.55f, 0.35f, 0.2f)), 4f, 0.4f);
            Part((x, y) => Box(x, y, 26, 108, 12, 5, 2, -50f), Solid(new Color(0.2f, 0.2f, 0.22f)), 3f, 0.3f);
            Part((x, y) => Poly(x, y, new[] { V(62, 62), V(84, 76), V(110, 40), V(96, 16), V(66, 34) }), Solid(RoverSteel), 6f, 1f, 45f);
            Glint(86, 44, 10f, 0.5f);
        }

        /// <summary>Antena parabólica en un mástil.</summary>
        private static void Antenna()
        {
            Shadow(64, 14, 40, 8);
            Part((x, y) => Box(x, y, 64, 22, 26, 8, 3), Solid(new Color(0.3f, 0.32f, 0.36f)), 3f, 0.5f);
            Part((x, y) => Segment(x, y, 64, 26, 64, 62, 5f), Solid(RoverSteel), 3f, 0.8f, 40f);
            Part((x, y) => Mathf.Max(Ellipse(x, y, 64, 82, 46, 30), -Ellipse(x, y, 64, 92, 40, 24)),
                Solid(new Color(0.92f, 0.93f, 0.95f)), 6f, 0.9f, 40f);
            Part((x, y) => Segment(x, y, 64, 70, 64, 100, 2.5f), Solid(new Color(0.4f, 0.4f, 0.44f)), 2f, 0.6f);
            Part((x, y) => Circle(x, y, 64, 104, 6f), Solid(new Color(0.2f, 0.85f, 1f)), 3f, 0.9f, 40f);
        }

        /// <summary>Orugas: banda dentada alrededor de tres ruedas.</summary>
        private static void Tracks()
        {
            Shadow(64, 18, 56, 8);
            Sdf band = (x, y) => Mathf.Max(Box(x, y, 64, 56, 54, 28, 26), -Box(x, y, 64, 56, 44, 18, 18));
            Part(band, (x, y) => Mathf.Repeat(x * 0.35f, 2f) < 1f ? new Color(0.25f, 0.26f, 0.28f) : new Color(0.33f, 0.34f, 0.37f), 5f, 0.5f, 30f);
            for (int k = 0; k < 3; k++)
            {
                float cx = 32 + k * 32;
                Part((x, y) => Circle(x, y, cx, 56, 15f), Solid(RoverSteel), 4f, 0.9f, 40f);
                Part((x, y) => Circle(x, y, cx, 56, 5f), Solid(RoverOrange), 2f, 0.6f);
            }
        }

        /// <summary>Rueda rápida: rueda fina azul con rayas de velocidad.</summary>
        private static void FastWheel()
        {
            Shadow(70, 12, 44, 7);
            Part((x, y) => Mathf.Max(Circle(x, y, 74, 64, 44), 34f - new Vector2(x - 74, y - 64).magnitude), Solid(new Color(0.18f, 0.18f, 0.2f)), 5f, 0.6f, 30f);
            Part((x, y) => Mathf.Max(Circle(x, y, 74, 64, 34), 12f - new Vector2(x - 74, y - 64).magnitude), Solid(new Color(0.2f, 0.75f, 1f)), 3f, 1f, 50f);
            Part((x, y) => Circle(x, y, 74, 64, 10), Solid(RoverSteel), 3f, 0.9f, 40f);
            for (int k = 0; k < 3; k++)
            {
                float ly = 44 + k * 20, lx = 26 - k * 4;
                Flat((x, y) => Segment(x, y, 6, ly, lx, ly, 3f), new Color(1f, 0.85f, 0.2f, 0.9f));
            }
        }

        /// <summary>Escáner: pantalla verde con ondas y un punto amarillo (veta encontrada).</summary>
        private static void Scanner()
        {
            Shadow(64, 16, 46, 8);
            Part((x, y) => Box(x, y, 64, 60, 42, 36, 10), Solid(new Color(0.22f, 0.24f, 0.28f)), 6f, 0.6f, 35f);
            Part((x, y) => Box(x, y, 64, 62, 32, 26, 6), Solid(new Color(0.05f, 0.25f, 0.18f)), 2f, 1f, 60f);
            for (int r = 8; r <= 24; r += 8)
            {
                float rr = r;
                Flat((x, y) => Mathf.Abs(Circle(x, y, 64, 62, rr)) - 1.2f, new Color(0.3f, 1f, 0.55f, 0.75f));
            }
            Flat((x, y) => Circle(x, y, 74, 70, 4f), new Color(1f, 0.85f, 0.2f));
            Part((x, y) => Segment(x, y, 96, 94, 108, 116, 3f), Solid(RoverSteel), 2f, 0.8f);
            Part((x, y) => Circle(x, y, 108, 116, 5f), Solid(new Color(0.9f, 0.2f, 0.15f)), 2f, 0.8f);
        }

        /// <summary>Brazo de carga: grúa naranja con gancho y una caja.</summary>
        private static void CargoArm()
        {
            Shadow(64, 14, 50, 8);
            Part((x, y) => Box(x, y, 30, 22, 18, 8, 3), Solid(new Color(0.3f, 0.32f, 0.36f)), 3f, 0.5f);
            Part((x, y) => Segment(x, y, 30, 26, 44, 86, 7f), Solid(RoverOrange), 5f, 0.6f, 30f);
            Part((x, y) => Segment(x, y, 44, 86, 96, 96, 6f), Solid(RoverOrange), 5f, 0.6f, 30f);
            Part((x, y) => Circle(x, y, 44, 86, 7f), Solid(RoverSteel), 3f, 0.9f);
            Flat((x, y) => Segment(x, y, 96, 96, 96, 62, 1.2f), new Color(0.2f, 0.2f, 0.22f));
            Part((x, y) => Box(x, y, 96, 42, 20, 18, 3), Grain(new Color(0.75f, 0.6f, 0.38f), new Color(0.6f, 0.45f, 0.28f), 0.15f), 4f, 0.3f);
            Flat((x, y) => Box(x, y, 96, 42, 20, 2), new Color(0.45f, 0.32f, 0.2f));
        }

        // ==================================================================
        // Huerto y comida
        // ==================================================================

        /// <summary>Bolsa de cierre zip transparente con semillas de la forma de cada especie y etiqueta de color.</summary>
        private static void SeedBag(Color seed, int shape, Color label)
        {
            Shadow(64, 14, 40, 7, 0.35f);
            Part((x, y) => Box(x, y, 64, 60, 38, 48, 6), Solid(new Color(0.85f, 0.92f, 0.95f)), 4f, 1f, 70f, 0f, 0f, 0.35f, 0.25f); // bolsa
            var rnd = new System.Random(shape * 13 + 3);
            for (int i = 0; i < 40; i++)
            {
                float sx = 32 + (float)rnd.NextDouble() * 64f, sy = 18 + (float)rnd.NextDouble() * 34f * (1f - Mathf.Abs(sx - 64) / 70f);
                float ang = (float)rnd.NextDouble() * 180f;
                Color c = seed * (0.85f + 0.3f * (float)rnd.NextDouble());
                c.a = 1f;
                switch (shape)
                {
                    case 0: Part((x, y) => Box(x, y, sx, sy, 1.4f, 3.6f, 1.4f, ang), Solid(c), 1f, 0.3f); break;            // lechuga: alargadas y oscuras
                    case 1: Part((x, y) => Circle(x, y, sx, sy, 3f), Solid(c), 2f, 0.4f, 30f); break;                      // rábano: redondas
                    case 2: Part((x, y) => Ellipse(x, y, sx, sy, 3.6f, 2.6f), Solid(c), 1.5f, 0.3f); break;                // tomate: planas
                    case 3: if (i < 6) { Part((x, y) => Ellipse(x, y, sx, sy + 4, 10f, 7.5f), Solid(c), 6f, 0.2f, 12f, 1f, 0.3f);    // patata de siembra con brotes
                            Part((x, y) => Segment(x, y, sx + 2, sy + 10, sx + 4, sy + 15, 1.2f), Solid(new Color(0.75f, 0.85f, 0.6f)), 1f, 0.2f); } break;
                    case 4: Part((x, y) => Box(x, y, sx, sy, 2.4f, 4.2f, 2.4f, ang), Solid(c), 2f, 0.4f, 25f);                // trigo: granos
                            Flat((x, y) => Segment(x, y, sx, sy - 3, sx, sy + 3, 0.4f), new Color(0.5f, 0.35f, 0.15f, 0.7f)); break;
                    default: Part((x, y) => Ellipse(x, y, sx, sy, 4.6f, 4f), Solid(c), 3f, 0.5f, 30f); break;               // soja: alubias
                }
            }
            Flat((x, y) => Box(x, y, 64, 102, 36, 2.2f), new Color(0.25f, 0.45f, 0.85f, 0.9f));                              // cierre zip
            Part((x, y) => Box(x, y, 64, 82, 26, 10, 2), Solid(label), 2f, 0.3f);                                             // etiqueta
            Flat((x, y) => Mathf.Max(Ellipse(x, y, 58, 82, 6, 3.5f), -Ellipse(x, y, 60, 84, 3, 1.5f)), new Color(1f, 1f, 1f, 0.9f)); // hoja dibujada
            Flat((x, y) => Segment(x, y, 64, 82, 76, 82, 1f), new Color(1f, 1f, 1f, 0.7f));
            Flat((x, y) => Box(x, y, 46, 60, 3, 36, 2), new Color(1f, 1f, 1f, 0.35f));
        }

        private static void Lettuce()
        {
            Shadow(64, 24, 50, 10);
            for (int i = 11; i >= 0; i--)
            {
                float a = i * 137.5f * Mathf.Deg2Rad, r = 10f + (i % 4) * 7f;
                float cx = 64 + Mathf.Cos(a) * r, cy = 58 + Mathf.Sin(a) * r * 0.7f;
                bool red = i % 2 == 0;
                Color baseCol = red ? new Color(0.5f, 0.12f, 0.2f) : new Color(0.42f, 0.66f, 0.22f);
                Albedo leaf = (x, y) =>
                {
                    float vein = Mathf.Abs(Mathf.Sin(Mathf.Atan2(y - cy, x - cx) * 6f));
                    Color c = Color.Lerp(baseCol, baseCol * 1.35f, Fbm(x * 0.15f, y * 0.15f));
                    return vein < 0.12f ? Color.Lerp(c, new Color(0.85f, 0.9f, 0.7f), 0.5f) : c;
                };
                Part((x, y) => Blob(x, y, cx, cy, 20f - (i % 3) * 2f, 1.4f, i), leaf, 6f, 0.25f, 18f, 2f, 0.3f);
            }
        }

        private static void Radish()
        {
            Shadow(64, 18, 46, 8);
            for (int k = 0; k < 2; k++)
            {
                float cx = 50 + k * 30, cy = 40 + k * 6;
                for (int l = 0; l < 4; l++)
                {
                    float lx = cx - 12 + l * 8, ly = cy + 46 + (l % 2) * 8;
                    Part((x, y) => Ellipse(x, y, lx, ly, 6f, 18f), Solid(new Color(0.3f, 0.58f, 0.2f)), 3f, 0.2f);
                    Flat((x, y) => Segment(x, y, cx, cy + 12, lx, ly, 1.2f), new Color(0.65f, 0.25f, 0.35f));
                }
                Part((x, y) => Segment(x, y, cx, cy - 12, cx + 6, cy - 30, 1.6f), Solid(new Color(0.95f, 0.92f, 0.88f)), 1f, 0.3f); // raíz fina
                Part((x, y) => Ellipse(x, y, cx, cy, 17f, 15f), Solid(new Color(0.85f, 0.1f, 0.28f)), 10f, 0.8f, 40f);
                Glint(cx - 6, cy + 5, 6f, 0.6f);
            }
        }

        private static void Tomatoes()
        {
            Shadow(64, 20, 50, 9);
            float[,] t = { { 46, 52, 26 }, { 86, 46, 22 } };
            for (int k = 0; k < 2; k++)
            {
                float cx = t[k, 0], cy = t[k, 1], r = t[k, 2];
                Part((x, y) => Ellipse(x, y, cx, cy, r, r * 0.9f), Solid(new Color(0.88f, 0.12f, 0.08f)), r * 0.8f, 0.9f, 45f);
                var star = new Vector2[10];
                for (int i = 0; i < 10; i++)
                {
                    float a = i * Mathf.PI / 5f, rr = i % 2 == 0 ? r * 0.45f : r * 0.15f;
                    star[i] = new Vector2(cx + Mathf.Cos(a) * rr, cy + r * 0.75f + Mathf.Sin(a) * rr * 0.5f);
                }
                Part((x, y) => Poly(x, y, star), Solid(new Color(0.25f, 0.55f, 0.18f)), 2f, 0.3f);              // cáliz
                Glint(cx - r * 0.35f, cy + r * 0.3f, r * 0.35f, 0.7f);
            }
        }

        private static void Potatoes()
        {
            Shadow(64, 22, 50, 9);
            float[,] p = { { 48, 50, 28, 20, 10 }, { 84, 42, 24, 17, -15 } };
            for (int k = 0; k < 2; k++)
            {
                float cx = p[k, 0], cy = p[k, 1], rx = p[k, 2], ry = p[k, 3], ang = p[k, 4];
                Albedo skin = Grain(new Color(0.76f, 0.58f, 0.36f), new Color(0.58f, 0.42f, 0.24f), 0.2f, 0.04f, new Color(0.4f, 0.28f, 0.15f));
                Part((x, y) => Box(x, y, cx, cy, rx, ry, ry * 0.9f, ang), skin, ry * 0.8f, 0.25f, 15f, 1.5f, 0.3f);
                for (int e = 0; e < 4; e++)
                {
                    float ex = cx - rx * 0.5f + e * rx * 0.35f, ey = cy + ((e % 2) == 0 ? 5f : -4f);
                    Flat((x, y) => Ellipse(x, y, ex, ey, 2.2f, 1.4f), new Color(0.35f, 0.24f, 0.12f, 0.9f)); // ojos
                }
            }
        }

        private static void Wheat()
        {
            Shadow(64, 16, 40, 7, 0.35f);
            for (int k = 0; k < 3; k++)
            {
                float bx = 50 + k * 14, ang = (k - 1) * 14f * Mathf.Deg2Rad;
                Vector2 top = new Vector2(bx + Mathf.Sin(ang) * 70f, 20 + Mathf.Cos(ang) * 70f);
                Flat((x, y) => Segment(x, y, bx, 18, top.x, top.y - 30, 1.6f), new Color(0.78f, 0.62f, 0.28f));
                for (int g = 0; g < 8; g++)
                {
                    float t = g / 8f;
                    Vector2 c = Vector2.Lerp(new Vector2(top.x - Mathf.Sin(ang) * 30f, top.y - 30f), top, t);
                    for (int s = -1; s <= 1; s += 2)
                    {
                        float gx = c.x + s * 4f, gy = c.y;
                        Part((x, y) => Box(x, y, gx, gy, 3.6f, 5.5f, 3f, s * 25f + ang * Mathf.Rad2Deg), Solid(new Color(0.9f, 0.72f, 0.32f)), 3f, 0.4f, 25f);
                        Flat((x, y) => Segment(x, y, gx, gy + 3, gx + s * 6, gy + 16, 0.35f), new Color(0.85f, 0.7f, 0.35f, 0.8f)); // barbas
                    }
                }
            }
        }

        private static void SoyPods()
        {
            Shadow(64, 20, 50, 9);
            for (int k = 0; k < 3; k++)
            {
                float cx = 40 + k * 24, cy = 46 + (k % 2) * 16, ang = -35f + k * 30f;
                Part((x, y) => Box(x, y, cx, cy, 30f, 10f, 9f, ang), Grain(new Color(0.6f, 0.7f, 0.32f), new Color(0.48f, 0.58f, 0.25f), 0.3f), 6f, 0.3f, 18f);
                float a = ang * Mathf.Deg2Rad;
                for (int b = -1; b <= 1; b++)
                {
                    float bx = cx + Mathf.Cos(a) * b * 16f, by = cy + Mathf.Sin(a) * b * 16f;
                    Part((x, y) => Circle(x, y, bx, by, 7.5f), Solid(new Color(0.66f, 0.76f, 0.36f)), 5f, 0.4f, 20f); // granos marcados
                }
            }
        }

        private static void NutrientBottle()
        {
            Shadow(64, 14, 30, 7, 0.35f);
            Part((x, y) => Box(x, y, 64, 54, 24, 40, 10), Solid(new Color(0.8f, 0.92f, 0.85f)), 8f, 1.1f, 60f, 0f, 0f, 0.5f, 0.5f);
            Part((x, y) => Mathf.Max(Box(x, y, 64, 50, 21, 34, 8), y - 70f), Solid(new Color(0.2f, 0.7f, 0.35f)), 6f, 0.9f, 50f, 0f, 0f, 0.85f);
            Part((x, y) => Box(x, y, 64, 100, 8, 8, 2), Solid(new Color(0.15f, 0.15f, 0.17f)), 3f, 0.5f);
            Part((x, y) => Mathf.Min(Segment(x, y, 64, 108, 64, 118, 3f), Circle(x, y, 64, 120, 5f)), Solid(new Color(0.15f, 0.15f, 0.17f)), 3f, 0.6f); // cuentagotas
            Flat((x, y) => Box(x, y, 64, 50, 18, 10, 2), new Color(1f, 1f, 1f, 0.9f));
            Flat((x, y) => Mathf.Max(Ellipse(x, y, 60, 50, 8, 4), -Ellipse(x, y, 62, 52, 4, 2)), new Color(0.2f, 0.6f, 0.3f));
            Flat((x, y) => Box(x, y, 48, 56, 2.5f, 26, 2), new Color(1f, 1f, 1f, 0.4f));
        }

        private static void FlourSack()
        {
            Shadow(64, 18, 48, 9);
            var sack = new[] { V(26, 20), V(102, 20), V(108, 46), V(96, 96), V(82, 104), V(64, 98), V(46, 104), V(32, 96), V(20, 46) };
            Albedo burlap = (x, y) =>
            {
                bool weave = (Mathf.Repeat(x, 4f) < 1f) ^ (Mathf.Repeat(y, 4f) < 1f);
                return new Color(0.82f, 0.72f, 0.55f) * (weave ? 0.92f : 1.02f);
            };
            Part((x, y) => Poly(x, y, sack), burlap, 12f, 0.1f, 8f, 1f, 0.3f);
            Part((x, y) => Ellipse(x, y, 64, 98, 20, 7), Solid(new Color(0.97f, 0.95f, 0.9f)), 4f, 0.1f);                     // harina asomando
            Flat((x, y) => Box(x, y, 64, 88, 34, 2.5f), new Color(0.55f, 0.4f, 0.25f));                                      // cuerda
            for (int k = 0; k < 3; k++)                                                                                      // espiga estampada
            {
                float ex = 64, ey = 46 + k * 9;
                Flat((x, y) => Mathf.Min(Ellipse(x, y, ex - 5, ey, 4, 2.5f), Ellipse(x, y, ex + 5, ey, 4, 2.5f)), new Color(0.6f, 0.45f, 0.25f, 0.85f));
            }
            Flat((x, y) => Segment(x, y, 64, 36, 64, 70, 1.2f), new Color(0.6f, 0.45f, 0.25f, 0.85f));
        }

        private static void RationPack()
        {
            Shadow(64, 18, 50, 9);
            Albedo foil = (x, y) => new Color(0.75f, 0.77f, 0.8f) * (0.85f + 0.3f * Fbm(x * 0.12f, y * 0.12f));
            Part((x, y) => Box(x, y, 64, 62, 46, 34, 4, -8f), foil, 10f, 1.1f, 60f, 4f, 0.15f);                                 // envase al vacío arrugado
            Part((x, y) => Box(x, y, 62, 64, 28, 14, 2, -8f), Solid(new Color(0.95f, 0.55f, 0.15f)), 2f, 0.3f);                // etiqueta
            Flat((x, y) => Mathf.Min(Segment(x, y, 52, 56, 54, 72, 1.2f), Segment(x, y, 70, 55, 72, 71, 1.4f)), new Color(1f, 1f, 1f, 0.95f)); // tenedor y cuchara
            Flat((x, y) => Circle(x, y, 72, 72, 3f), new Color(1f, 1f, 1f, 0.95f));
            Flat((x, y) => Box(x, y, 66, 91, 44, 2f, 1, -8f), new Color(0.4f, 0.4f, 0.42f, 0.7f));                              // soldadura térmica
        }

        private static void SaladBowl()
        {
            Shadow(64, 18, 50, 9);
            for (int i = 0; i < 10; i++)
            {
                float a = i * 0.7f, cx = 64 + Mathf.Cos(a * 2.3f) * 24f, cy = 64 + Mathf.Abs(Mathf.Sin(a * 1.7f)) * 12f;
                bool red = i % 3 == 0;
                Part((x, y) => Blob(x, y, cx, cy, 13f, 1.2f, i), Solid(red ? new Color(0.5f, 0.12f, 0.2f) : new Color(0.42f, 0.68f, 0.22f)), 4f, 0.3f, 18f);
            }
            for (int i = 0; i < 3; i++)
            {
                float cx = 46 + i * 18, cy = 66 + (i % 2) * 6;
                Part((x, y) => Mathf.Max(Circle(x, y, cx, cy, 7f), cy - 2f - y), Solid(new Color(0.88f, 0.15f, 0.1f)), 4f, 0.8f, 40f); // tomate
            }
            Part((x, y) => Mathf.Max(Ellipse(x, y, 64, 56, 50, 34), y - 58f), Solid(new Color(0.92f, 0.92f, 0.9f)), 8f, 0.8f, 40f);  // cuenco
            Flat((x, y) => Mathf.Abs(Ellipse(x, y, 64, 58, 50, 6)) - 1.2f, new Color(0.7f, 0.7f, 0.72f));
        }

        private static void PowerCell()
        {
            Shadow(64, 14, 30, 7);
            Part((x, y) => Box(x, y, 64, 58, 24, 44, 10), Solid(new Color(0.18f, 0.62f, 0.32f)), 18f, 0.8f, 40f);
            Part((x, y) => Mathf.Max(Box(x, y, 64, 58, 24, 44, 10), y - 30f), Solid(new Color(0.15f, 0.16f, 0.18f)), 18f, 0.6f); // base
            Part((x, y) => Box(x, y, 64, 106, 9, 5, 2), Solid(new Color(0.78f, 0.8f, 0.84f)), 3f, 0.9f, 40f);    // borne +
            Flat((x, y) => Poly(x, y, new[] { V(68, 92), V(52, 62), V(64, 62), V(58, 38), V(76, 70), V(64, 70) }), new Color(1f, 0.92f, 0.2f)); // rayo
            Glint(52, 78, 9f, 0.5f);
        }
    }
}
