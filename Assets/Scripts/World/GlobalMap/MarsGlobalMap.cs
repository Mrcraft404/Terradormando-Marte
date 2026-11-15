using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace MarsRobot.World.GlobalMap
{
    /// <summary>Lugar conocido de Marte que se marca en el mapa global.</summary>
    public struct MarsLandmark
    {
        public string Name;
        public float Latitude;
        /// <summary>Longitud en grados, −180..180 (este positivo).</summary>
        public float Longitude;
        /// <summary>Radio (grados) dentro del cual un clic se considera "en" este lugar.</summary>
        public float Radius;
        public string Description;

        public MarsLandmark(string name, float lat, float lon, float radius, string description)
        {
            Name = name;
            Latitude = lat;
            Longitude = lon;
            Radius = radius;
            Description = description;
        }
    }

    /// <summary>Condiciones aproximadas de un punto, para la ficha del selector de aparición.</summary>
    public struct MarsSiteInfo
    {
        public string Region;
        public string Description;
        public float ElevationKm;
        public float MinTempC;
        public float MaxTempC;
        public float PressureMbar;
        public float Ice01;
        public float Sunlight01;
        public float Roughness01;
        /// <summary>0 fácil … 5 extrema (misma escala que ZoneDifficulty).</summary>
        public int DifficultyIndex;
    }

    /// <summary>
    /// Mapa global de Marte (proyección equirectangular, −180..180 de longitud) generado por código
    /// en un hilo de fondo: dicotomía norte/sur, Tharsis y sus volcanes, Valles Marineris, Hellas,
    /// Argyre, Utopia, casquetes polares y miles de cráteres, todos en sus coordenadas reales.
    /// El relieve es una aproximación jugable, no datos científicos.
    ///
    /// Para usar una imagen real (p. ej. el mosaico MOLA coloreado de la NASA, dominio público),
    /// pon una textura equirectangular en Assets/Resources/MarsGlobalMap.png: se mostrará esa imagen
    /// y el relieve procedural se seguirá usando para las estadísticas.
    /// </summary>
    public static class MarsGlobalMap
    {
        public const int Width = 1280;
        public const int Height = 640;

        private static float[] _elevation;
        private static Color32[] _pixels;
        private static Task _task;
        private static Texture2D _texture;

        public static bool IsReady
        {
            get { return _elevation != null && _pixels != null; }
        }

        public static readonly MarsLandmark[] Landmarks =
        {
            new MarsLandmark("Olympus Mons", 18.65f, -133.8f, 9f, "El volcán más alto del sistema solar (unos 22 km). Vistas increíbles, aire muy fino y laderas enormes."),
            new MarsLandmark("Montes Tharsis", 1.5f, -113f, 8f, "Tres grandes volcanes en fila. Basalto, tubos de lava y mucha radiación por la altitud."),
            new MarsLandmark("Valles Marineris", -13.9f, -59.2f, 10f, "Un cañón de 4.000 km. Las paredes protegen del viento y dejan al descubierto capas de minerales."),
            new MarsLandmark("Cráter Gale", -5.4f, 137.8f, 3.5f, "Cráter con una montaña de capas en el centro. Arcillas y sulfatos; pendientes largas."),
            new MarsLandmark("Cráter Jezero", 18.4f, 77.7f, 3.5f, "Antiguo lago con un delta de río. Arcillas, carbonatos y posibles rastros de vida antigua."),
            new MarsLandmark("Hellas Planitia", -42.4f, 70.5f, 16f, "La cuenca de impacto más profunda: mayor presión atmosférica y hielo cerca del borde."),
            new MarsLandmark("Utopia Planitia", 49.7f, 117.5f, 14f, "Llanura inmensa con grandes depósitos de hielo bajo la superficie. Inviernos muy duros."),
            new MarsLandmark("Elysium Planitia", 4.5f, 135.6f, 5f, "Llanura volcánica suave y segura. Buen lugar para empezar."),
            new MarsLandmark("Elysium Mons", 25f, 147.2f, 5f, "Segundo gran complejo volcánico, rodeado de coladas de lava."),
            new MarsLandmark("Argyre Planitia", -49.7f, -43.4f, 9f, "Cuenca de impacto antigua con hielo enterrado y montañas en su borde."),
            new MarsLandmark("Arabia Terra", 19.8f, 30f, 12f, "Tierras altas muy antiguas y erosionadas, sembradas de cráteres."),
            new MarsLandmark("Acidalia Planitia", 46.7f, -22.2f, 10f, "Llanura oscura del norte; terreno llano ideal para construir, con tormentas frecuentes."),
            new MarsLandmark("Chryse Planitia", 22.5f, -48f, 7f, "Llanura donde desembocaban antiguos canales de inundación."),
            new MarsLandmark("Meridiani Planum", -1.9f, -5.5f, 6f, "Llanura rica en hematites; terreno suave y seguro para aterrizar."),
            new MarsLandmark("Syrtis Major", 8.4f, 69.5f, 6f, "Meseta volcánica oscura, rica en basalto."),
            new MarsLandmark("Casquete polar norte", 85f, 0f, 9f, "Hielo de agua casi ilimitado, pero frío extremo y meses sin sol."),
            new MarsLandmark("Casquete polar sur", -85f, 0f, 9f, "Hielo de agua y de CO₂. El lugar más frío y oscuro del planeta."),
        };

        /// <summary>Empieza a generar el mapa en segundo plano (no bloquea).</summary>
        public static void GenerateAsync()
        {
            if (IsReady || _task != null) return;
            _task = Task.Run(() => Generate());
        }

        /// <summary>Textura del mapa (null mientras se genera).</summary>
        public static Texture2D Texture
        {
            get
            {
                if (!IsReady) return null;
                if (_texture == null)
                {
                    Texture2D custom = Resources.Load<Texture2D>("MarsGlobalMap");
                    if (custom != null)
                    {
                        _texture = custom;
                    }
                    else
                    {
                        _texture = new Texture2D(Width, Height, TextureFormat.RGBA32, true);
                        _texture.wrapMode = TextureWrapMode.Clamp;
                        _texture.filterMode = FilterMode.Trilinear;
                        _texture.anisoLevel = 4;
                        _texture.SetPixels32(_pixels);
                        _texture.Apply(true, false);
                        _texture.hideFlags = HideFlags.HideAndDontSave;
                    }
                }
                return _texture;
            }
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        public static float ElevationAt(float lat, float lon)
        {
            if (!IsReady) return 0f;
            int x = LonToX(lon), y = LatToY(lat);
            return _elevation[y * Width + x];
        }

        /// <summary>Desnivel local (0..1) alrededor del punto: base para la rugosidad del terreno.</summary>
        public static float RoughnessAt(float lat, float lon)
        {
            if (!IsReady) return 0.4f;
            int cx = LonToX(lon), cy = LatToY(lat);
            float min = float.MaxValue, max = float.MinValue;
            for (int dy = -3; dy <= 3; dy++)
            for (int dx = -3; dx <= 3; dx++)
            {
                int x = (cx + dx + Width) % Width;
                int y = Mathf.Clamp(cy + dy, 0, Height - 1);
                float e = _elevation[y * Width + x];
                if (e < min) min = e;
                if (e > max) max = e;
            }
            return Mathf.Clamp01((max - min) / 4f);
        }

        /// <summary>Lugar conocido más cercano dentro de su radio (−1 si no hay ninguno).</summary>
        public static int FindLandmark(float lat, float lon)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < Landmarks.Length; i++)
            {
                float d = AngularDistance(lat, lon, Landmarks[i].Latitude, Landmarks[i].Longitude);
                if (d < Landmarks[i].Radius && d < bestD)
                {
                    bestD = d;
                    best = i;
                }
            }
            return best;
        }

        /// <summary>Ficha con nombre, descripción y condiciones aproximadas del punto.</summary>
        public static MarsSiteInfo Describe(float lat, float lon)
        {
            var info = new MarsSiteInfo();
            float e = ElevationAt(lat, lon);
            float a = Mathf.Abs(lat);
            info.ElevationKm = e;
            info.Roughness01 = RoughnessAt(lat, lon);
            info.MaxTempC = Mathf.Round(5f - 0.95f * a - 1.8f * Mathf.Max(0f, e));
            info.MinTempC = Mathf.Round(info.MaxTempC - 75f - 0.35f * a);
            info.PressureMbar = 6.1f * Mathf.Exp(-e / 11.1f);
            info.Sunlight01 = Mathf.Clamp01(Mathf.Cos(lat * Mathf.Deg2Rad));
            info.Ice01 = Mathf.Clamp01((a - 28f) / 45f);
            if (e < -4f && a > 30f) info.Ice01 = Mathf.Clamp01(info.Ice01 + 0.15f);

            int lm = FindLandmark(lat, lon);
            if (lm >= 0)
            {
                info.Region = Landmarks[lm].Name;
                info.Description = Landmarks[lm].Description;
            }
            else if (a > 75f)
            {
                info.Region = lat > 0f ? "Región polar norte" : "Región polar sur";
                info.Description = "Hielo por todas partes, pero frío extremo y largas noches polares.";
            }
            else if (e > 5f)
            {
                info.Region = "Mesetas volcánicas";
                info.Description = "Terreno alto y volcánico: aire muy fino y más radiación, pero roca rica en metales.";
            }
            else if (e < -2.5f && lat > 15f)
            {
                info.Region = "Llanuras del norte";
                info.Description = "Llanuras bajas y suaves, fáciles para construir. Posible hielo bajo el suelo.";
            }
            else if (lat < -10f)
            {
                info.Region = "Tierras altas del sur";
                info.Description = "Terreno muy antiguo, lleno de cráteres y rocas. Duro para las ruedas.";
            }
            else
            {
                info.Region = "Planicies ecuatoriales";
                info.Description = "Clima más templado y buena luz solar, pero poca agua accesible.";
            }

            // Dificultad 0..5: frío, altura, rugosidad y falta de agua.
            float score = a / 25f + Mathf.Max(0f, e) / 5f + info.Roughness01 * 1.6f + (info.Ice01 < 0.1f ? 0.6f : 0f);
            info.DifficultyIndex = Mathf.Clamp(Mathf.RoundToInt(score), 0, 5);
            return info;
        }

        public static float AngularDistance(float lat1, float lon1, float lat2, float lon2)
        {
            float p1 = lat1 * Mathf.Deg2Rad, p2 = lat2 * Mathf.Deg2Rad;
            float dp = p2 - p1, dl = (lon2 - lon1) * Mathf.Deg2Rad;
            float h = Mathf.Sin(dp * 0.5f) * Mathf.Sin(dp * 0.5f) + Mathf.Cos(p1) * Mathf.Cos(p2) * Mathf.Sin(dl * 0.5f) * Mathf.Sin(dl * 0.5f);
            return 2f * Mathf.Asin(Mathf.Sqrt(Mathf.Clamp01(h))) * Mathf.Rad2Deg;
        }

        /// <summary>Convierte longitud este 0..360 a −180..180.</summary>
        public static float ToSigned(float lonEast)
        {
            lonEast = Mathf.Repeat(lonEast, 360f);
            return lonEast > 180f ? lonEast - 360f : lonEast;
        }

        /// <summary>Convierte −180..180 a longitud este 0..360.</summary>
        public static float ToEast(float lon)
        {
            return Mathf.Repeat(lon, 360f);
        }

        public static string FormatCoords(float lat, float lon)
        {
            return Mathf.Abs(lat).ToString("0.0") + "°" + (lat >= 0f ? "N" : "S") + "   " + ToEast(lon).ToString("0.0") + "°E";
        }

        private static int LonToX(float lon)
        {
            return Mathf.Clamp((int)((lon + 180f) / 360f * Width), 0, Width - 1);
        }

        private static int LatToY(float lat)
        {
            return Mathf.Clamp((int)((lat + 90f) / 180f * Height), 0, Height - 1);
        }

        // ------------------------------------------------------------------
        // Generación (hilo de fondo: solo matemáticas, sin API de Unity que lo prohíba)
        // ------------------------------------------------------------------

        private static readonly Vector2[] VallesPath =
        {
            new Vector2(-6f, -100f), new Vector2(-8f, -88f), new Vector2(-10f, -75f), new Vector2(-12f, -62f),
            new Vector2(-13f, -52f), new Vector2(-9f, -42f), new Vector2(-3f, -35f)
        };

        private static void Generate()
        {
            try
            {
                const int W = Width, H = Height;
                var e = new float[W * H];

                for (int y = 0; y < H; y++)
                {
                    float lat = -90f + (y + 0.5f) * 180f / H;
                    float latR = lat * Mathf.Deg2Rad, cl = Mathf.Cos(latR), sl = Mathf.Sin(latR);
                    for (int x = 0; x < W; x++)
                    {
                        float lon = -180f + (x + 0.5f) * 360f / W;
                        float lonR = lon * Mathf.Deg2Rad;
                        var p = new Vector3(cl * Mathf.Cos(lonR), sl, cl * Mathf.Sin(lonR));

                        float h = Fbm(p * 2.2f, 6, 17) * 2.6f + Fbm(p * 9f, 3, 5) * 0.6f;

                        // Dicotomía: tierras altas al sur, llanuras bajas al norte.
                        float b = 8f + 10f * Mathf.Sin(lonR + 0.8f) + 8f * Fbm(p * 1.5f, 3, 99);
                        h += Mathf.Lerp(1.6f, -3.6f, SmoothStep(b - 10f, b + 10f, lat));

                        // Abultamientos de Tharsis y Elysium.
                        h += 6.5f * Gauss(Dist(lat, lon, 2f, -108f), 28f);
                        h += 3f * Gauss(Dist(lat, lon, 25f, 147f), 14f);

                        // Volcanes.
                        h += Volcano(lat, lon, 18.65f, -133.8f, 5f, 19f);
                        h += Volcano(lat, lon, 11.8f, -104.5f, 3f, 15f);
                        h += Volcano(lat, lon, 1.5f, -113f, 3f, 12f);
                        h += Volcano(lat, lon, -8.3f, -120.1f, 3.5f, 14f);
                        h += Volcano(lat, lon, 25f, 147.2f, 3f, 9f);
                        h += Volcano(lat, lon, 40.5f, -109.6f, 9f, 3f);

                        // Valles Marineris.
                        float dv = DistToPath(lat, lon);
                        if (dv < 8f) h -= 6f * Mathf.Exp(-Sq(dv / 1.4f)) * (1f + 0.3f * Fbm(p * 20f, 2, 7));

                        // Cuencas de impacto.
                        h += Basin(lat, lon, -42.4f, 70.5f, 21f, 7.5f);
                        h += Basin(lat, lon, -49.7f, -43.4f, 11f, 4.5f);
                        h += Basin(lat, lon, 13f, 87f, 9f, 3.5f);
                        h += Basin(lat, lon, 47f, 118f, 22f, 2.2f);

                        // Cúpulas de hielo polares.
                        h += 2.5f * SmoothStep(78f, 88f, lat) + 3f * SmoothStep(78f, 88f, -lat);

                        e[y * W + x] = h;
                    }
                }

                AddCraters(e);

                var col = new Color32[W * H];
                Vector3 light = new Vector3(-0.6f, 0.6f, 0.55f).normalized;
                for (int y = 0; y < H; y++)
                {
                    float lat = -90f + (y + 0.5f) * 180f / H;
                    float latR = lat * Mathf.Deg2Rad, cl = Mathf.Cos(latR), sl = Mathf.Sin(latR);
                    int yu = Mathf.Min(y + 1, H - 1), yd = Mathf.Max(y - 1, 0);
                    for (int x = 0; x < W; x++)
                    {
                        float lon = -180f + (x + 0.5f) * 360f / W;
                        float lonR = lon * Mathf.Deg2Rad;
                        var p = new Vector3(cl * Mathf.Cos(lonR), sl, cl * Mathf.Sin(lonR));
                        int i = y * W + x;
                        float h = e[i];
                        float hl = e[y * W + (x - 1 + W) % W], hr = e[y * W + (x + 1) % W];
                        float hd = e[yd * W + x], hu = e[yu * W + x];

                        Vector3 n = new Vector3((hl - hr) * 0.9f, (hd - hu) * 0.9f, 1f).normalized;
                        float shade = Mathf.Clamp(1f + (Vector3.Dot(n, light) - light.z) * 1.6f, 0.5f, 1.45f);

                        Color c = ElevationColor(h);
                        float albedo = Fbm(p * 5f + new Vector3(3f, 1f, 7f), 4, 23);
                        float dark = Darkness(lat, lon) * (0.6f + 0.8f * (albedo + 0.5f));
                        c *= (1f - Mathf.Clamp(dark, 0f, 0.5f)) * (0.92f + 0.16f * (albedo + 0.5f));
                        c *= shade;

                        float iceNoise = Fbm(p * 6f, 3, 41) * 3f;
                        float ice = Mathf.Max(SmoothStep(77f, 82f, lat + iceNoise), SmoothStep(79f, 84f, -lat + iceNoise));
                        c = Color.Lerp(c, new Color(0.95f, 0.93f, 0.9f) * Mathf.Lerp(1f, shade, 0.5f), ice * 0.92f);

                        c.r = Mathf.Clamp01(c.r);
                        c.g = Mathf.Clamp01(c.g);
                        c.b = Mathf.Clamp01(c.b);
                        c.a = 1f;
                        col[i] = c;
                    }
                }

                _pixels = col;
                _elevation = e;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private static void AddCraters(float[] e)
        {
            const int W = Width, H = Height;
            var rnd = new System.Random(4242);
            var list = new List<Vector4>();   // lat, lon, radio, profundidad
            var mounds = new List<bool>();

            list.Add(new Vector4(-5.4f, 137.8f, 1.4f, 3f)); mounds.Add(true);   // Gale (con el monte Sharp)
            list.Add(new Vector4(18.4f, 77.7f, 0.8f, 1.2f)); mounds.Add(false); // Jezero

            for (int i = 0; i < 1300; i++)
            {
                float lat = Mathf.Asin((float)rnd.NextDouble() * 2f - 1f) * Mathf.Rad2Deg;
                float lon = (float)rnd.NextDouble() * 360f - 180f;
                float u = (float)rnd.NextDouble();
                float r = 0.3f + Mathf.Pow(u, 6f) * 5.5f;
                float boundary = 8f + 10f * Mathf.Sin(lon * Mathf.Deg2Rad + 0.8f);
                if (lat > boundary && rnd.NextDouble() < 0.7) continue; // el norte tiene menos cráteres
                if (Mathf.Abs(lat) > 80f) continue;
                list.Add(new Vector4(lat, lon, r, 0.4f + r * 0.35f));
                mounds.Add(r > 2.5f);
            }

            for (int k = 0; k < list.Count; k++)
            {
                Vector4 c = list[k];
                float cosl = Mathf.Max(0.15f, Mathf.Cos(c.x * Mathf.Deg2Rad));
                float ext = c.z * 1.6f;
                int y0 = LatToY(c.x - ext), y1 = LatToY(c.x + ext);
                int xs = Mathf.CeilToInt(ext / cosl / 360f * W) + 1;
                int xc = LonToX(c.y);
                for (int y = y0; y <= y1; y++)
                {
                    float lat = -90f + (y + 0.5f) * 180f / H;
                    for (int dx = -xs; dx <= xs; dx++)
                    {
                        int x = ((xc + dx) % W + W) % W;
                        float lon = -180f + (x + 0.5f) * 360f / W;
                        float t = Dist(lat, lon, c.x, c.y) / c.z;
                        if (t > 1.6f) continue;
                        float v = t < 1f ? -c.w * (1f - t * t) : 0f;
                        v += c.w * 0.35f * Mathf.Exp(-Sq((t - 1f) / 0.18f));
                        if (mounds[k] && t < 0.35f) v += c.w * 0.9f * (1f - t / 0.35f);
                        e[y * W + x] += v;
                    }
                }
            }
        }

        private static Color ElevationColor(float h)
        {
            if (h < -3f) return Color.Lerp(new Color(0.36f, 0.17f, 0.1f), new Color(0.55f, 0.26f, 0.14f), Mathf.InverseLerp(-8f, -3f, h));
            if (h < 0f) return Color.Lerp(new Color(0.55f, 0.26f, 0.14f), new Color(0.7f, 0.36f, 0.19f), Mathf.InverseLerp(-3f, 0f, h));
            if (h < 4f) return Color.Lerp(new Color(0.7f, 0.36f, 0.19f), new Color(0.8f, 0.47f, 0.27f), Mathf.InverseLerp(0f, 4f, h));
            if (h < 10f) return Color.Lerp(new Color(0.8f, 0.47f, 0.27f), new Color(0.86f, 0.6f, 0.4f), Mathf.InverseLerp(4f, 10f, h));
            return Color.Lerp(new Color(0.86f, 0.6f, 0.4f), new Color(0.93f, 0.8f, 0.65f), Mathf.InverseLerp(10f, 21f, h));
        }

        /// <summary>Regiones oscuras de albedo (Syrtis Major, Acidalia, Mare Erythraeum...).</summary>
        private static float Darkness(float lat, float lon)
        {
            float d = 0f;
            d += 0.35f * Gauss(Dist(lat, lon, 10f, 70f), 10f);
            d += 0.3f * Gauss(Dist(lat, lon, 45f, -25f), 14f);
            d += 0.25f * Gauss(Dist(lat, lon, -25f, -40f), 18f);
            d += 0.3f * Gauss(Dist(lat, lon, -3f, 0f), 10f);
            d += 0.3f * Gauss(Dist(lat, lon, -25f, 145f), 18f);
            d += 0.25f * Gauss(Dist(lat, lon, -18f, 105f), 14f);
            d += 0.25f * Gauss(Dist(lat, lon, -30f, -155f), 15f);
            return d;
        }

        private static float Volcano(float lat, float lon, float lat0, float lon0, float r, float height)
        {
            float d = Dist(lat, lon, lat0, lon0);
            if (d > r * 1.6f) return 0f;
            float t = d / r;
            float cone = height * Mathf.Pow(Mathf.Max(0f, 1f - t), 1.6f) + height * 0.08f * Mathf.Exp(-t * t);
            if (t < 0.12f) cone -= height * 0.15f * (1f - t / 0.12f); // caldera
            return cone;
        }

        private static float Basin(float lat, float lon, float lat0, float lon0, float r, float depth)
        {
            float t = Dist(lat, lon, lat0, lon0) / r;
            if (t > 1.6f) return 0f;
            float bowl = t < 1f ? -depth * (1f - t * t) : 0f;
            return bowl + depth * 0.25f * Mathf.Exp(-Sq((t - 1f) / 0.15f));
        }

        private static float DistToPath(float lat, float lon)
        {
            float best = float.MaxValue;
            var p = new Vector2(lat, lon);
            for (int i = 0; i < VallesPath.Length - 1; i++)
            {
                Vector2 a = VallesPath[i], b = VallesPath[i + 1];
                Vector2 ab = b - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Vector2.Dot(ab, ab));
                float d = (p - (a + ab * t)).magnitude;
                if (d < best) best = d;
            }
            return best;
        }

        /// <summary>Distancia aproximada en grados (plana con corrección de latitud).</summary>
        private static float Dist(float lat, float lon, float lat0, float lon0)
        {
            float dl = lon - lon0;
            if (dl > 180f) dl -= 360f;
            else if (dl < -180f) dl += 360f;
            float dx = dl * Mathf.Cos((lat + lat0) * 0.5f * Mathf.Deg2Rad);
            float dy = lat - lat0;
            return Mathf.Sqrt(dx * dx + dy * dy);
        }

        private static float Gauss(float d, float r)
        {
            return Mathf.Exp(-Sq(d / r));
        }

        private static float Sq(float x)
        {
            return x * x;
        }

        private static float SmoothStep(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        // Ruido de valor 3D (sobre la esfera: sin costuras en la longitud ni distorsión en los polos).
        private static float HashNoise(int x, int y, int z, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 0x8da6b343u ^ (uint)y * 0xd8163841u ^ (uint)z * 0xcb1ab31fu ^ (uint)seed * 0x165667b1u;
                h ^= h >> 15;
                h *= 0x2c1b3c6du;
                h ^= h >> 12;
                h *= 0x297a2d39u;
                h ^= h >> 15;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        private static float ValueNoise(float x, float y, float z, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y), zi = Mathf.FloorToInt(z);
            float fx = x - xi, fy = y - yi, fz = z - zi;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            fz = fz * fz * (3f - 2f * fz);
            float c000 = HashNoise(xi, yi, zi, seed), c100 = HashNoise(xi + 1, yi, zi, seed);
            float c010 = HashNoise(xi, yi + 1, zi, seed), c110 = HashNoise(xi + 1, yi + 1, zi, seed);
            float c001 = HashNoise(xi, yi, zi + 1, seed), c101 = HashNoise(xi + 1, yi, zi + 1, seed);
            float c011 = HashNoise(xi, yi + 1, zi + 1, seed), c111 = HashNoise(xi + 1, yi + 1, zi + 1, seed);
            float x00 = c000 + (c100 - c000) * fx, x10 = c010 + (c110 - c010) * fx;
            float x01 = c001 + (c101 - c001) * fx, x11 = c011 + (c111 - c011) * fx;
            float y0 = x00 + (x10 - x00) * fy, y1 = x01 + (x11 - x01) * fy;
            return y0 + (y1 - y0) * fz;
        }

        private static float Fbm(Vector3 p, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, f = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * (ValueNoise(p.x * f, p.y * f, p.z * f, seed + i * 31) * 2f - 1f);
                f *= 2.02f;
                amp *= 0.5f;
            }
            return sum;
        }
    }
}
