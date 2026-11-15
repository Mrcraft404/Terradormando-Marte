#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Genera juegos de texturas PBR repetibles (512 × 512) para la nave en Assets/Textures/Generated:
    /// &lt;Juego&gt;_Albedo, _Normal (de un mapa de alturas, con operador Sobel), _MetallicSmoothness
    /// (metálico en RGB, suavidad en alfa: formato del shader Standard) y _AO (oclusión de cavidades).
    /// Incluyen desgaste y polvo marciano, que baja la suavidad. Después, PBRMaterialFactory las
    /// convierte en materiales. Menú Marte ▸ Materiales PBR ▸ Generar texturas procedurales.
    /// </summary>
    public static class PBRTextureGenerator
    {
        public const string Folder = "Assets/Textures/Generated";
        private const int S = 512;

        private struct Sample
        {
            public Color albedo;
            public float height;    // 0..1
            public float metallic;  // 0..1
            public float smooth;    // 0..1
        }

        private delegate Sample Shader2D(int x, int y);

        [MenuItem("Marte/Materiales PBR/Generar texturas procedurales")]
        public static void GenerateAll()
        {
            Directory.CreateDirectory(Folder);
            Write("Ship_Steel", Steel, 5f);
            Write("Ship_HeatShield", HeatShield, 4f);
            Write("Ship_Floor", Floor, 6f);
            Write("Ship_Panel", Panel, 4f);
            Write("Ship_Wall", Wall, 3f);
            Write("Ship_DarkMetal", DarkMetal, 3f);
            Write("Ship_Metal", Aluminium, 2.5f);
            AssetDatabase.Refresh();
            Debug.Log("[PBR] Texturas generadas en " + Folder + " (7 juegos × 4 mapas).");
        }

        // ------------------------------------------------------------------
        // Escritura: albedo, normal, metálico/suavidad y oclusión
        // ------------------------------------------------------------------

        private static void Write(string set, Shader2D shader, float normalStrength)
        {
            var albedo = new Color[S * S];
            var height = new float[S * S];
            var ms = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                Sample s = shader(x, y);
                int i = y * S + x;
                albedo[i] = new Color(Mathf.Clamp01(s.albedo.r), Mathf.Clamp01(s.albedo.g), Mathf.Clamp01(s.albedo.b), 1f);
                height[i] = Mathf.Clamp01(s.height);
                ms[i] = new Color(s.metallic, s.metallic, s.metallic, Mathf.Clamp01(s.smooth));
            }

            var normal = new Color[S * S];
            var ao = new Color[S * S];
            for (int y = 0; y < S; y++)
            for (int x = 0; x < S; x++)
            {
                // Sobel con repetición (las texturas no tienen costuras).
                float tl = H(height, x - 1, y + 1), t = H(height, x, y + 1), tr = H(height, x + 1, y + 1);
                float l = H(height, x - 1, y), r = H(height, x + 1, y);
                float bl = H(height, x - 1, y - 1), b = H(height, x, y - 1), br = H(height, x + 1, y - 1);
                float dx = (tr + 2f * r + br) - (tl + 2f * l + bl);
                float dy = (tl + 2f * t + tr) - (bl + 2f * b + br);
                Vector3 n = new Vector3(-dx * normalStrength, -dy * normalStrength, 1f).normalized;
                normal[y * S + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);

                // Oclusión: cuánto más bajo está el punto que su entorno (cavidades, juntas).
                float avg = 0f;
                for (int k = -4; k <= 4; k += 2)
                for (int m = -4; m <= 4; m += 2)
                    avg += H(height, x + k, y + m);
                avg /= 25f;
                float occ = Mathf.Clamp01(1f - Mathf.Max(0f, avg - height[y * S + x]) * 3.5f);
                ao[y * S + x] = new Color(occ, occ, occ, 1f);
            }

            Save(set + "_Albedo", albedo);
            Save(set + "_Normal", normal);
            Save(set + "_MetallicSmoothness", ms);
            Save(set + "_AO", ao);
        }

        private static float H(float[] h, int x, int y)
        {
            x = ((x % S) + S) % S;
            y = ((y % S) + S) % S;
            return h[y * S + x];
        }

        private static void Save(string name, Color[] px)
        {
            var t = new Texture2D(S, S, TextureFormat.RGBA32, false);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(Path.Combine(Folder, name + ".png"), t.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(t);
        }

        // ------------------------------------------------------------------
        // Ruido repetible
        // ------------------------------------------------------------------

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 982451653);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFF) / 65535f;
            }
        }

        /// <summary>Ruido de valor periódico: <paramref name="cells"/> celdas por lado de la textura.</summary>
        private static float Noise(float x, float y, int cells, int seed)
        {
            float fx = x / S * cells, fy = y / S * cells;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
            x0 = ((x0 % cells) + cells) % cells;
            y0 = ((y0 % cells) + cells) % cells;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>Ruido periódico con distinto número de celdas en X e Y (vetas, cepillado, arañazos).</summary>
        private static float NoiseXY(float x, float y, int cellsX, int cellsY, int seed)
        {
            float fx = x / S * cellsX, fy = y / S * cellsY;
            int x0 = Mathf.FloorToInt(fx), y0 = Mathf.FloorToInt(fy);
            float tx = fx - x0, ty = fy - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            int x1 = (x0 + 1) % cellsX, y1 = (y0 + 1) % cellsY;
            x0 = ((x0 % cellsX) + cellsX) % cellsX;
            y0 = ((y0 % cellsY) + cellsY) % cellsY;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        private static float Fbm(float x, float y, int cells, int seed)
        {
            return Noise(x, y, cells, seed) * 0.5f + Noise(x, y, cells * 2, seed + 1) * 0.3f + Noise(x, y, cells * 4, seed + 2) * 0.2f;
        }

        /// <summary>Polvo marciano 0..1 (manchas grandes y suaves).</summary>
        private static float Dust(int x, int y, int seed)
        {
            return Mathf.SmoothStep(0.45f, 0.8f, Fbm(x, y, 4, seed));
        }

        private static readonly Color DustColor = new Color(0.62f, 0.38f, 0.24f);

        private static float Gauss(float d, float w)
        {
            return Mathf.Exp(-(d * d) / (w * w));
        }

        /// <summary>Distancia (en píxeles, con repetición) a la línea x = c.</summary>
        private static float WrapDist(float p, float c)
        {
            float d = Mathf.Abs(p - c) % S;
            return Mathf.Min(d, S - d);
        }

        // ------------------------------------------------------------------
        // Juegos de texturas
        // ------------------------------------------------------------------

        /// <summary>Acero inoxidable cepillado con anillos de soldadura y manchas de polvo.</summary>
        private static Sample Steel(int x, int y)
        {
            float brush = NoiseXY(x, y, 4, 160, 11);
            float weldH = Mathf.Max(Gauss(WrapDist(y, 0), 3f), Gauss(WrapDist(y, S / 2), 3f));
            float weldV = Gauss(WrapDist(x, S / 4), 2.5f) * (y < S / 2 ? 1f : 0f) + Gauss(WrapDist(x, S * 3 / 4), 2.5f) * (y >= S / 2 ? 1f : 0f);
            float weld = Mathf.Max(weldH, weldV);
            float heatTint = Mathf.Max(Gauss(WrapDist(y, 0), 12f), Gauss(WrapDist(y, S / 2), 12f)) * (1f - weld);
            float dust = Dust(x, y, 3) * 0.6f;
            Color c = new Color(0.72f, 0.74f, 0.77f) * (0.9f + 0.12f * brush);
            c = Color.Lerp(c, new Color(0.62f, 0.55f, 0.42f), heatTint * 0.35f);   // tinte de calor junto a la soldadura
            c = Color.Lerp(c, new Color(0.5f, 0.5f, 0.52f), weld * 0.7f);
            c = Color.Lerp(c, DustColor, dust * 0.55f);
            return new Sample
            {
                albedo = c,
                height = 0.5f + weld * 0.45f + brush * 0.03f,
                metallic = Mathf.Lerp(0.9f, 0.4f, dust),
                smooth = Mathf.Lerp(0.62f, 0.3f, dust) - weld * 0.2f
            };
        }

        /// <summary>Losetas hexagonales de cerámica negra del escudo térmico.</summary>
        private static Sample HeatShield(int x, int y)
        {
            // Rejilla hexagonal periódica: 10 columnas (par) × 9 filas por textura, sin costuras.
            const int cols = 10, rows = 9;
            float ws = S / (float)cols, hs = S / (float)rows, size = ws / 1.5f;
            Vector2 best = Vector2.zero, bestLocal = Vector2.zero;
            float bestD = float.MaxValue;
            int col = Mathf.RoundToInt(x / ws), row = Mathf.RoundToInt(y / hs);
            for (int dc = -1; dc <= 1; dc++)
            for (int dr = -1; dr <= 1; dr++)
            {
                int cc = col + dc, r = row + dr;
                var center = new Vector2(cc * ws, (r + ((((cc % cols) + cols) % cols & 1) == 0 ? 0f : 0.5f)) * hs);
                float lx = Mathf.Repeat(x - center.x + S * 0.5f, S) - S * 0.5f, ly = Mathf.Repeat(y - center.y + S * 0.5f, S) - S * 0.5f;
                float d = lx * lx + ly * ly;
                if (d < bestD)
                {
                    bestD = d;
                    best = new Vector2(((cc % cols) + cols) % cols, ((r % rows) + rows) % rows);
                    bestLocal = new Vector2(lx, ly * (size * 1.732f / hs));
                }
            }
            Vector2 local = bestLocal;
            float hexD = Mathf.Max(Mathf.Abs(local.y), Mathf.Abs(local.x) * 0.866f + Mathf.Abs(local.y) * 0.5f);
            float edge = Mathf.Clamp01((size * 0.86f - hexD) / 2.5f);           // 0 en la junta
            float tile = Hash(Mathf.RoundToInt(best.x), Mathf.RoundToInt(best.y), 5);
            float dust = Dust(x, y, 8) * 0.35f;
            Color c = new Color(0.07f, 0.07f, 0.08f) * (0.85f + 0.4f * tile) * (0.9f + 0.15f * Noise(x, y, 64, 9));
            c = Color.Lerp(new Color(0.02f, 0.02f, 0.02f), c, edge);
            c = Color.Lerp(c, DustColor * 0.6f, dust);
            return new Sample { albedo = c, height = edge * 0.8f + 0.1f * Noise(x, y, 64, 9), metallic = 0f, smooth = Mathf.Lerp(0.3f, 0.12f, dust) * edge };
        }

        /// <summary>Rejilla de suelo de acero (barras y huecos) con polvo en los rincones.</summary>
        private static Sample Floor(int x, int y)
        {
            const int cell = 32;
            float bx = Mathf.Abs((x % cell) - cell * 0.5f), by = Mathf.Abs((y % cell) - cell * 0.5f);
            float bar = Mathf.Max(Mathf.Clamp01((bx - 11f) / 2f), Mathf.Clamp01((by - 11f) / 2f)); // 1 = barra
            float dust = Dust(x, y, 6) * 0.7f;
            Color metal = new Color(0.44f, 0.45f, 0.47f) * (0.9f + 0.15f * Noise(x, y, 128, 4));
            Color hole = new Color(0.03f, 0.03f, 0.035f);
            Color c = Color.Lerp(hole, metal, bar);
            c = Color.Lerp(c, DustColor, dust * (1f - bar * 0.4f) * 0.6f);
            return new Sample { albedo = c, height = bar, metallic = 0.8f * bar * (1f - dust * 0.6f), smooth = Mathf.Lerp(0.35f, 0.2f, dust) * bar };
        }

        /// <summary>Panel de consola pintado: ranura perimetral, tornillos, rejilla de ventilación y arañazos.</summary>
        private static Sample Panel(int x, int y)
        {
            float u = x / (float)S, v = y / (float)S;
            float border = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
            float groove = Gauss(border - 0.045f, 0.008f);
            float screw = 0f;
            for (int k = 0; k < 4; k++)
            {
                float sx = (k & 1) == 0 ? 0.08f : 0.92f, sy = (k & 2) == 0 ? 0.08f : 0.92f;
                float d = new Vector2(u - sx, v - sy).magnitude;
                screw = Mathf.Max(screw, Mathf.Clamp01((0.022f - d) / 0.004f));
            }
            bool ventZone = u > 0.2f && u < 0.8f && v > 0.12f && v < 0.3f;
            float vent = ventZone && Mathf.Repeat(v * 50f, 1f) < 0.45f ? 1f : 0f;
            float scratch = Mathf.Clamp01((NoiseXY(x, y, 6, 140, 21) - 0.86f) * 8f);
            float dust = Dust(x, y, 14) * 0.4f;
            Color paint = new Color(0.25f, 0.27f, 0.31f) * (0.95f + 0.08f * Noise(x, y, 64, 3));
            Color c = Color.Lerp(paint, new Color(0.65f, 0.66f, 0.68f), scratch);
            c = Color.Lerp(c, new Color(0.04f, 0.04f, 0.05f), Mathf.Max(groove * 0.8f, vent * 0.9f));
            c = Color.Lerp(c, new Color(0.7f, 0.71f, 0.73f), screw);
            c = Color.Lerp(c, DustColor, dust * 0.35f);
            return new Sample
            {
                albedo = c,
                height = 0.6f - groove * 0.4f - vent * 0.4f + screw * 0.25f,
                metallic = Mathf.Max(scratch, screw) * 0.85f + 0.05f,
                smooth = Mathf.Lerp(0.45f, 0.25f, dust) + screw * 0.2f
            };
        }

        /// <summary>Paneles de pared de composite blanco (juntas, fijaciones, textura de pintura).</summary>
        private static Sample Wall(int x, int y)
        {
            float jx = Mathf.Min(WrapDist(x, 0), WrapDist(x, S / 2)), jy = Mathf.Min(WrapDist(y, 0), WrapDist(y, S / 2));
            float joint = Mathf.Max(Gauss(jx, 1.6f), Gauss(jy, 1.6f));
            float fastener = 0f;
            for (int k = 0; k < 4; k++)
            {
                float fx = 24 + (k & 1) * (S / 2 - 48), fy = 24 + ((k >> 1) & 1) * (S / 2 - 48);
                float d = new Vector2(Mathf.Repeat(x, S / 2f) - fx, Mathf.Repeat(y, S / 2f) - fy).magnitude;
                fastener = Mathf.Max(fastener, Mathf.Clamp01((5f - d) / 1.2f));
            }
            float peel = Noise(x, y, 256, 31);
            float dust = Dust(x, y, 2) * 0.25f;
            Color c = new Color(0.86f, 0.87f, 0.88f) * (0.97f + 0.05f * peel);
            c = Color.Lerp(c, new Color(0.35f, 0.36f, 0.38f), joint * 0.8f);
            c = Color.Lerp(c, new Color(0.6f, 0.61f, 0.64f), fastener);
            c = Color.Lerp(c, DustColor, dust * 0.3f);
            return new Sample { albedo = c, height = 0.6f - joint * 0.4f + fastener * 0.15f + peel * 0.02f, metallic = fastener * 0.8f, smooth = 0.35f + fastener * 0.25f };
        }

        /// <summary>Metal oscuro desgastado: bordes pulidos por el uso, arañazos y polvo.</summary>
        private static Sample DarkMetal(int x, int y)
        {
            float wear = Mathf.Clamp01((Fbm(x, y, 8, 41) - 0.62f) * 4f);
            float scratch = Mathf.Clamp01((NoiseXY(x, y, 90, 10, 43) - 0.88f) * 9f);
            float dust = Dust(x, y, 19) * 0.6f;
            Color c = new Color(0.17f, 0.17f, 0.19f) * (0.9f + 0.2f * Noise(x, y, 128, 45));
            c = Color.Lerp(c, new Color(0.45f, 0.45f, 0.47f), Mathf.Max(wear * 0.6f, scratch));
            c = Color.Lerp(c, DustColor * 0.8f, dust * 0.5f);
            return new Sample
            {
                albedo = c,
                height = 0.5f - scratch * 0.15f + Noise(x, y, 128, 45) * 0.05f,
                metallic = Mathf.Lerp(0.85f, 0.45f, dust),            // metal puro, apagado por el polvo
                smooth = Mathf.Lerp(0.35f, 0.18f, dust) + scratch * 0.2f // 0,35: superficie desgastada por polvo marciano
            };
        }

        /// <summary>Aluminio con grano fino y polvo.</summary>
        private static Sample Aluminium(int x, int y)
        {
            float grain = NoiseXY(x, y, 256, 64, 51);
            float dust = Dust(x, y, 23) * 0.5f;
            Color c = new Color(0.68f, 0.69f, 0.72f) * (0.93f + 0.1f * grain);
            c = Color.Lerp(c, DustColor, dust * 0.45f);
            return new Sample { albedo = c, height = 0.5f + grain * 0.04f, metallic = Mathf.Lerp(0.85f, 0.5f, dust), smooth = Mathf.Lerp(0.45f, 0.25f, dust) };
        }
    }
}
#endif
