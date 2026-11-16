using System.Threading.Tasks;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.UI.Minimap
{
    /// <summary>
    /// Mapa en relieve de TODA la zona, calculado a partir de las alturas reales del terreno
    /// (HeightField): color de Marte según la altura (valles oscuros, mesetas claras) y sombreado
    /// de colinas con el sol al noroeste, como un mapa topográfico.
    ///
    /// Se calcula una sola vez en un hilo de fondo (no congela el juego) y lo usan el minimapa y
    /// el mapa a pantalla completa. Es mucho más barato que renderizar una cámara cenital.
    /// Convenio de la textura: fila 0 = sur (z = 0), columna 0 = oeste (x = 0), como la niebla.
    /// </summary>
    public static class ZoneReliefMap
    {
        public const int Resolution = 512;

        private static Texture2D _texture;
        private static Color32[] _pixels;
        private static Task _task;
        private static TerrainManager _source;

        public static Vector3 Origin { get; private set; }
        public static float WorldSize { get; private set; } = 1f;

        /// <summary>La textura, o null si aún se está calculando.</summary>
        public static Texture2D Texture
        {
            get
            {
                if (_texture == null && _pixels != null && _task != null && _task.IsCompleted)
                {
                    _texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, true)
                    {
                        wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear, anisoLevel = 4,
                        name = "Mapa en relieve de la zona", hideFlags = HideFlags.HideAndDontSave
                    };
                    _texture.SetPixels32(_pixels);
                    _texture.Apply(true, true);
                    _pixels = null;
                }
                return _texture;
            }
        }

        /// <summary>Empieza a calcular el mapa del terreno (si no se está haciendo ya).</summary>
        public static void Ensure(TerrainManager terrain)
        {
            if (terrain == null || !terrain.IsMapReady || terrain.HeightField == null) return;
            if (_source == terrain && (_task != null || _texture != null)) return;
            Clear();
            _source = terrain;
            Origin = terrain.transform.position;
            WorldSize = terrain.HeightField.WorldSize;
            HeightField field = terrain.HeightField;
            _task = Task.Run(() => _pixels = Compute(field));
        }

        /// <summary>Rectángulo UV de un cuadrado del mundo (centro y medio lado en metros).</summary>
        public static Rect UVRect(Vector3 center, float halfSize)
        {
            float u = (center.x - Origin.x - halfSize) / WorldSize;
            float v = (center.z - Origin.z - halfSize) / WorldSize;
            float s = halfSize * 2f / WorldSize;
            return new Rect(u, v, s, s);
        }

        private static Color32[] Compute(HeightField f)
        {
            const int n = Resolution;
            float size = f.WorldSize, cell = size / (n - 1);
            var h = new float[n * n];
            float min = float.MaxValue, max = float.MinValue;
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float v = f.SampleHeight(x * cell, y * cell);
                h[y * n + x] = v;
                if (v < min) min = v;
                if (v > max) max = v;
            }
            float range = Mathf.Max(1f, max - min);

            // Paleta de Marte: valle óxido oscuro → llanura ocre → meseta arena clara.
            var low = new Color(0.36f, 0.17f, 0.11f);
            var mid = new Color(0.74f, 0.42f, 0.24f);
            var high = new Color(0.9f, 0.72f, 0.52f);
            var sun = new Vector3(-0.6f, 0.75f, 0.6f).normalized; // noroeste, alto
            var px = new Color32[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                int i = y * n + x;
                float hx = h[y * n + Mathf.Min(x + 1, n - 1)] - h[y * n + Mathf.Max(x - 1, 0)];
                float hz = h[Mathf.Min(y + 1, n - 1) * n + x] - h[Mathf.Max(y - 1, 0) * n + x];
                Vector3 normal = new Vector3(-hx, 2f * cell, -hz).normalized;
                float shade = Mathf.Clamp01(Vector3.Dot(normal, sun));
                float t = (h[i] - min) / range;
                Color c = t < 0.5f ? Color.Lerp(low, mid, t * 2f) : Color.Lerp(mid, high, (t - 0.5f) * 2f);
                c *= 0.45f + 0.75f * shade;
                // Curvas de nivel suaves cada 1/12 del relieve (aspecto de mapa topográfico).
                float contour = Mathf.Abs(Mathf.Repeat(t * 12f, 1f) - 0.5f);
                if (contour > 0.47f) c *= 0.85f;
                c.a = 1f;
                px[i] = c;
            }
            return px;
        }

        private static void Clear()
        {
            if (_texture != null) Object.Destroy(_texture);
            _texture = null;
            _pixels = null;
            _task = null;
            _source = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _texture = null;
            _pixels = null;
            _task = null;
            _source = null;
        }
    }
}
