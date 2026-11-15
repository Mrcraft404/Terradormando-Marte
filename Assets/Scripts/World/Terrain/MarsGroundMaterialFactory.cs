using UnityEngine;

namespace MarsRobot.World.TerrainSystem
{
    /// <summary>
    /// Genera por código el material del suelo marciano (sin archivos de imagen):
    ///  • Albedo 512² que se repite sin costuras: polvo rojizo con manchas claras/oscuras y
    ///    piedrecitas.
    ///  • Mapa de normales a partir del mismo relieve (da volumen a la luz rasante).
    ///  • Textura de detalle fina que se repite a menor escala, para que de cerca no se vea borroso.
    ///
    /// Memoria: unos 3–4 MB en total con mipmaps. Funciona con el shader Standard (Built-in) y,
    /// si el proyecto pasa a URP, con "Universal Render Pipeline/Lit".
    /// Es un sustituto provisional hasta tener texturas pintadas o un shader triplanar propio.
    /// </summary>
    public static class MarsGroundMaterialFactory
    {
        private static readonly Color DustDark = new Color(0.42f, 0.2f, 0.11f);
        private static readonly Color DustMid = new Color(0.66f, 0.36f, 0.2f);
        private static readonly Color DustLight = new Color(0.82f, 0.56f, 0.36f);
        private static readonly Color Basalt = new Color(0.22f, 0.14f, 0.11f);

        /// <summary>
        /// Crea el material. La textura principal se repite cada <c>uvTileSize</c> metros (TerrainManager);
        /// la de detalle, <paramref name="detailRepeats"/> veces dentro de cada repetición.
        /// </summary>
        public static Material Create(int seed, float detailRepeats = 6f)
        {
            const int size = 512;
            float[] height = new float[size * size];
            Texture2D albedo = BuildAlbedo(size, seed, height);
            Texture2D normal = BuildNormal(size, height, 2.5f);
            Texture2D detail = BuildDetail(256, seed + 7);

            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            Shader shader = urp != null ? urp : Shader.Find("Standard");
            var mat = new Material(shader);
            mat.name = "SueloMarte (procedural)";

            if (urp != null)
            {
                mat.SetTexture("_BaseMap", albedo);
                mat.SetColor("_BaseColor", Color.white);
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_DetailAlbedoMap", detail);
                mat.SetTextureScale("_DetailAlbedoMap", new Vector2(detailRepeats, detailRepeats));
                mat.EnableKeyword("_DETAIL_MULX2");
                mat.SetFloat("_Smoothness", 0.08f);
            }
            else
            {
                mat.mainTexture = albedo;
                mat.color = Color.white;
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
                mat.SetTexture("_DetailAlbedoMap", detail);
                mat.SetTextureScale("_DetailAlbedoMap", new Vector2(detailRepeats, detailRepeats));
                mat.EnableKeyword("_DETAIL_MULX2");
                mat.SetFloat("_Glossiness", 0.08f);
                mat.SetFloat("_Metallic", 0f);
            }
            return mat;
        }

        /// <summary>
        /// Pone una imagen orbital (CTX en gris) como textura principal que cubre la zona entera una
        /// sola vez, teñida de color marciano. El relieve fino pasa al canal de "detalle" (que se
        /// sigue repitiendo cada pocos metros), porque el mapa de normales principal compartiría la
        /// escala de la imagen orbital y se estiraría por toda la zona.
        /// </summary>
        /// <param name="uvTileSize">Metros por unidad de UV0 (TerrainManager.UvTileSize).</param>
        /// <param name="zoneSizeMeters">Lado de la zona en metros.</param>
        public static void ApplyOrbitalAlbedo(Material mat, Texture2D orbital, float uvTileSize, float zoneSizeMeters)
        {
            if (mat == null || orbital == null) return;
            bool urp = mat.HasProperty("_BaseMap");
            string mainTex = urp ? "_BaseMap" : "_MainTex";
            string mainColor = urp ? "_BaseColor" : "_Color";

            Texture normal = mat.GetTexture("_BumpMap");
            Vector2 detailScale = mat.GetTextureScale("_DetailAlbedoMap");

            orbital.wrapMode = TextureWrapMode.Clamp;
            mat.SetTexture(mainTex, orbital);
            float tiling = uvTileSize / Mathf.Max(1f, zoneSizeMeters);
            mat.SetTextureScale(mainTex, new Vector2(tiling, tiling));
            mat.SetTextureOffset(mainTex, Vector2.zero);
            // CTX es una imagen en gris: la teñimos con el color medio del regolito.
            mat.SetColor(mainColor, new Color(1.0f, 0.62f, 0.42f));

            // El relieve fino pasa al canal de detalle (usa su propia escala, que se repite).
            // _NORMALMAP sigue activo: sin textura, Unity usa una normal plana y el shader Standard
            // solo aplica la normal de detalle dentro de ese bloque.
            mat.SetTexture("_BumpMap", null);
            if (normal != null)
            {
                mat.SetTexture("_DetailNormalMap", normal);
                mat.SetTextureScale("_DetailNormalMap", detailScale);
                mat.SetFloat("_DetailNormalMapScale", 1f);
            }
            mat.EnableKeyword("_DETAIL_MULX2");
        }

        // ------------------------------------------------------------------
        // Texturas
        // ------------------------------------------------------------------

        private static Texture2D BuildAlbedo(int size, int seed, float[] heightOut)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;

                    // Manchas grandes de color (polvo claro sobre suelo más oscuro).
                    float patches = Fbm(u, v, 4, 4, seed);
                    // Grano medio.
                    float grain = Fbm(u, v, 32, 3, seed + 11);
                    // Piedrecitas: celdas con un punto oscuro aleatorio.
                    float pebble = Pebbles(u, v, 48, seed + 23);

                    Color c = Color.Lerp(DustDark, DustMid, Smooth(0.3f, 0.6f, patches));
                    c = Color.Lerp(c, DustLight, Smooth(0.58f, 0.8f, patches) * 0.8f);
                    c *= Mathf.Lerp(0.82f, 1.12f, grain);
                    c = Color.Lerp(c, Basalt, pebble * 0.85f);
                    c.a = 1f;
                    pixels[y * size + x] = c;

                    // Relieve para el mapa de normales: grano + piedras que sobresalen.
                    heightOut[y * size + x] = patches * 0.3f + grain * 0.5f + pebble * 0.9f;
                }
            }
            return MakeTexture(size, pixels, false, "SueloMarte_Albedo");
        }

        private static Texture2D BuildNormal(int size, float[] h, float strength)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Diferencias centrales con vecinos que dan la vuelta (textura repetible).
                    float l = h[y * size + Wrap(x - 1, size)];
                    float r = h[y * size + Wrap(x + 1, size)];
                    float d = h[Wrap(y - 1, size) * size + x];
                    float u = h[Wrap(y + 1, size) * size + x];
                    var n = new Vector3((l - r) * strength, (d - u) * strength, 1f).normalized;
                    // Formato RGB con A=1: UnpackNormal de Unity lo interpreta bien en todas las plataformas.
                    pixels[y * size + x] = new Color(n.x * 0.5f + 0.5f, n.y * 0.5f + 0.5f, n.z * 0.5f + 0.5f, 1f);
                }
            }
            return MakeTexture(size, pixels, true, "SueloMarte_Normal");
        }

        /// <summary>Detalle fino en gris centrado en 0,5 (con el modo ×2 de Unity, 0,5 = sin cambio).</summary>
        private static Texture2D BuildDetail(int size, int seed)
        {
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size, v = (float)y / size;
                    float g = Fbm(u, v, 16, 3, seed);
                    float p = Pebbles(u, v, 24, seed + 3);
                    float val = Mathf.Lerp(0.42f, 0.58f, g) - p * 0.12f;
                    pixels[y * size + x] = new Color(val, val, val, 1f);
                }
            }
            return MakeTexture(size, pixels, false, "SueloMarte_Detalle");
        }

        private static Texture2D MakeTexture(int size, Color32[] pixels, bool linear, string name)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear);
            tex.name = name;
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4; // nitidez a ras de suelo
            tex.SetPixels32(pixels);
            // makeNoLongerReadable = true: libera la copia en RAM tras subirla a la GPU.
            tex.Apply(true, true);
            return tex;
        }

        // ------------------------------------------------------------------
        // Ruido repetible (periódico): así la textura no tiene costuras al repetirse.
        // ------------------------------------------------------------------

        private static float Fbm(float u, float v, int baseCells, int octaves, int seed)
        {
            float sum = 0f, amp = 1f, norm = 0f;
            int cells = baseCells;
            for (int o = 0; o < octaves; o++)
            {
                sum += amp * ValueNoise(u * cells, v * cells, cells, seed + o * 101);
                norm += amp;
                amp *= 0.5f;
                cells *= 2;
            }
            return sum / norm;
        }

        private static float ValueNoise(float x, float y, int period, int seed)
        {
            int x0 = Mathf.FloorToInt(x), y0 = Mathf.FloorToInt(y);
            float tx = x - x0, ty = y - y0;
            tx = tx * tx * (3f - 2f * tx);
            ty = ty * ty * (3f - 2f * ty);
            float a = Hash01(Wrap(x0, period), Wrap(y0, period), seed);
            float b = Hash01(Wrap(x0 + 1, period), Wrap(y0, period), seed);
            float c = Hash01(Wrap(x0, period), Wrap(y0 + 1, period), seed);
            float d = Hash01(Wrap(x0 + 1, period), Wrap(y0 + 1, period), seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), ty);
        }

        /// <summary>Una piedra (o ninguna) por celda, con tamaño y posición aleatorios. 1 = centro de la piedra.</summary>
        private static float Pebbles(float u, float v, int cells, int seed)
        {
            float x = u * cells, y = v * cells;
            int cx = Mathf.FloorToInt(x), cy = Mathf.FloorToInt(y);
            float best = 0f;
            for (int oy = -1; oy <= 1; oy++)
            {
                for (int ox = -1; ox <= 1; ox++)
                {
                    int gx = cx + ox, gy = cy + oy;
                    int wx = Wrap(gx, cells), wy = Wrap(gy, cells);
                    if (Hash01(wx, wy, seed + 5) > 0.45f) continue; // ~45 % de celdas con piedra
                    float px = gx + Hash01(wx, wy, seed + 1);
                    float py = gy + Hash01(wx, wy, seed + 2);
                    float r = Mathf.Lerp(0.12f, 0.38f, Hash01(wx, wy, seed + 3));
                    float dx = x - px, dy = y - py;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy) / r;
                    if (dist < 1f) best = Mathf.Max(best, 1f - dist * dist);
                }
            }
            return best;
        }

        private static float Hash01(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static int Wrap(int i, int n)
        {
            int r = i % n;
            return r < 0 ? r + n : r;
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
