using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.Spawns;
using MarsRobot.World.SpaceWeather;
using UnityEngine;

namespace MarsRobot.UI.Minimap
{
    /// <summary>
    /// Minimapa táctico en la esquina superior izquierda (estilo panel industrial con rejilla y
    /// líneas de barrido). Muestra el terreno real visto desde arriba, la niebla de guerra, el
    /// rover con su cono de visión y los marcadores (cohete, recursos, hielo, balizas, avisos).
    ///
    /// Rendimiento (máquinas con poca RAM):
    ///  • Cámara cenital ortográfica propia, DESACTIVADA: se renderiza a mano cada N frames.
    ///  • Textura de render pequeña (256×256) y sin niebla ni MSAA en ese render.
    ///  • Si el cable del minimapa está cortado o hay interferencias solares, no se renderiza.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsMinimapController : MonoBehaviour
    {
        [Header("Vista")]
        [SerializeField, Min(80f)] private float sizePixels = 230f;
        [Tooltip("Metros del mundo desde el centro hasta el borde del minimapa.")]
        [SerializeField, Min(10f)] private float halfSizeMeters = 75f;
        [SerializeField, Range(64, 1024)] private int textureSize = 256;
        [Tooltip("Renderizar el terreno cada N frames (más alto = más barato).")]
        [SerializeField, Min(1)] private int renderEveryFrames = 4;
        [Tooltip("Cuadrícula táctica cada N metros.")]
        [SerializeField, Min(5f)] private float gridMeters = 25f;

        private Camera _cam;
        private RenderTexture _rt;
        private MinimapFogOfWar _fog;
        private Texture2D _scanlines, _arrow, _cone, _noise;
        private GUIStyle _small, _title, _center;
        private int _frame;
        private MarsRobot.World.TerrainSystem.TerrainManager _terrain;

        /// <summary>El minimapa de la escena (lo usa el mapa a pantalla completa).</summary>
        public static MarsMinimapController Instance { get; private set; }

        /// <summary>Niebla de guerra (zonas exploradas).</summary>
        public MinimapFogOfWar Fog
        {
            get { return _fog; }
        }

        /// <summary>Medio lado del minimapa en metros.</summary>
        public float HalfSizeMeters
        {
            get { return halfSizeMeters; }
        }

        public Texture2D ArrowTexture
        {
            get { return _arrow; }
        }

        public Texture2D ConeTexture
        {
            get { return _cone; }
        }

        /// <summary>¿Hay señal de radar? (cable sano y sin interferencia solar).</summary>
        public bool HasSignal
        {
            get { return !SignalLost && !SpaceWeatherState.RadarBlocked; }
        }
        private RobotHealthSystem _health;
        private RobotModuleSystem _modules;

        /// <summary>Lo que sigue el minimapa: el rover.</summary>
        public static Transform FollowTarget()
        {
            RobotController rc;
            if (GameServices.TryGet(out rc) && rc.gameObject.activeInHierarchy) return rc.transform;
            return rc != null ? rc.transform : null;
        }

        private void Awake()
        {
            Instance = this;
            _fog = gameObject.AddComponent<MinimapFogOfWar>();

            _rt = new RenderTexture(textureSize, textureSize, 16, RenderTextureFormat.Default) { name = "Minimapa", antiAliasing = 1 };
            var camGo = new GameObject("Cámara del minimapa");
            camGo.transform.SetParent(transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.orthographic = true;
            _cam.orthographicSize = halfSizeMeters;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.05f, 0.03f, 0.03f);
            _cam.nearClipPlane = 1f;
            _cam.farClipPlane = 1200f;
            _cam.targetTexture = _rt;
            _cam.allowHDR = false;
            _cam.allowMSAA = false;
            _cam.useOcclusionCulling = false;
            _cam.enabled = false; // se renderiza a mano
            camGo.transform.rotation = Quaternion.Euler(90f, 0f, 0f); // norte (+Z) arriba

            _scanlines = MakeScanlines();
            _arrow = MakeArrow();
            _cone = MakeCone();
            _noise = MakeNoise();
        }

        private void OnDestroy()
        {
            if (_rt != null) _rt.Release();
            Destroy(_rt);
            Destroy(_scanlines);
            Destroy(_arrow);
            Destroy(_cone);
            Destroy(_noise);
        }

        private bool SignalLost
        {
            get { return _health != null && !_health.IsFunctional(RobotModuleId.CableMinimap); }
        }

        private void LateUpdate()
        {
            if (_health == null) GameServices.TryGet(out _health);
            if (_modules == null && _health != null) _modules = _health.GetComponent<RobotModuleSystem>();
            _fog.RevealMultiplier = _modules != null && _modules.HasModule(RoverModuleType.HighGainAntenna) ? 2f : 1f;

            // Mapa en relieve de la zona: cuando está listo, ya no hace falta la cámara cenital.
            if (_terrain == null) GameServices.TryGet(out _terrain);
            if (_terrain != null) ZoneReliefMap.Ensure(_terrain);
            if (ZoneReliefMap.Texture != null) return;

            Transform t = FollowTarget();
            if (t == null || SignalLost || SpaceWeatherState.RadarBlocked) return;
            if (++_frame % renderEveryFrames != 0) return;

            _cam.transform.position = new Vector3(t.position.x, t.position.y + 400f, t.position.z);
            // Sin niebla en la vista cenital (si no, todo saldría del color de la bruma).
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            _cam.Render();
            RenderSettings.fog = fog;
        }

        // ------------------------------------------------------------------
        // Dibujo (IMGUI)
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (MarsMapScreen.IsOpen) return; // el mapa grande ya lo muestra todo
            Transform t = FollowTarget();
            if (t == null) return;
            EnsureStyles();

            const float pad = 12f;
            Rect frame = new Rect(pad, pad, sizePixels + 16, sizePixels + 44);
            Rect map = new Rect(frame.x + 8, frame.y + 22, sizePixels, sizePixels);
            Color prev = GUI.color;

            // Marco metálico industrial.
            bool alert = SpaceWeatherState.CmeCountdownHours >= 0f || SpaceWeatherState.GlobalStormActive;
            float blink = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 7f);
            GUI.color = alert ? Color.Lerp(new Color(0.25f, 0.25f, 0.28f), new Color(0.8f, 0.1f, 0.1f), blink) : new Color(0.22f, 0.23f, 0.26f);
            GUI.DrawTexture(frame, Texture2D.whiteTexture);
            GUI.color = new Color(0.4f, 0.42f, 0.46f);
            GUI.DrawTexture(new Rect(frame.x, frame.y, frame.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.x, frame.y, 2, frame.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.1f, 0.1f, 0.12f);
            GUI.DrawTexture(new Rect(frame.x, frame.yMax - 2, frame.width, 2), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(frame.xMax - 2, frame.y, 2, frame.height), Texture2D.whiteTexture);
            GUI.color = new Color(0.55f, 0.57f, 0.6f); // tornillos
            foreach (Vector2 c in new[] { new Vector2(frame.x + 4, frame.y + 4), new Vector2(frame.xMax - 8, frame.y + 4), new Vector2(frame.x + 4, frame.yMax - 8), new Vector2(frame.xMax - 8, frame.yMax - 8) })
            {
                GUI.DrawTexture(new Rect(c.x, c.y, 4, 4), Texture2D.whiteTexture);
            }
            GUI.color = prev;

            LandingZoneData zone = null;
            MarsRobot.Base.RocketBase rb;
            if (GameServices.TryGet(out rb)) zone = rb.Zone;
            GUI.Label(new Rect(frame.x + 12, frame.y + 3, frame.width - 24, 18), (zone != null ? zone.displayName.ToUpperInvariant() : Tr.T("MARTE")) + Tr.T(" · RADAR TÁCTICO"), _title);

            // Sin señal / interferencia.
            if (SignalLost || SpaceWeatherState.RadarBlocked)
            {
                GUI.DrawTextureWithTexCoords(map, _noise, new Rect(Random.value, Random.value, 2f, 2f));
                GUI.Label(map, SignalLost ? Tr.T("SIN SEÑAL\ncable del minimapa cortado") : Tr.T("INTERFERENCIA SOLAR\nradar bloqueado"), _center);
                DrawFooter(frame, t);
                return;
            }

            // Terreno + niebla + rejilla + líneas de barrido.
            Texture2D relief = ZoneReliefMap.Texture;
            if (relief != null) GUI.DrawTextureWithTexCoords(map, relief, ZoneReliefMap.UVRect(t.position, halfSizeMeters));
            else GUI.DrawTexture(map, _rt, ScaleMode.StretchToFill, false);
            GUI.DrawTextureWithTexCoords(map, _fog.Texture, _fog.UVRect(t.position, halfSizeMeters));
            DrawGrid(map, t.position);
            GUI.color = new Color(1f, 1f, 1f, 0.25f);
            GUI.DrawTextureWithTexCoords(map, _scanlines, new Rect(0, Time.unscaledTime * 0.05f, 1, map.height / 3f));
            GUI.color = prev;

            // Marcadores.
            Vector2 center = map.center;
            float scale = sizePixels * 0.5f / halfSizeMeters;
            foreach (MinimapObjectMarker.Entry e in MinimapObjectMarker.All)
            {
                Vector3 p = e.follow != null ? e.follow.position : e.position;
                Vector2 s = new Vector2(center.x + (p.x - t.position.x) * scale, center.y - (p.z - t.position.z) * scale);
                if (!map.Contains(s)) continue;
                if (e.blink && Mathf.Repeat(Time.unscaledTime, 1f) < 0.35f) continue;
                float size = e.type == MinimapMarkerType.Rocket ? 9f : 6f;
                GUI.color = MinimapObjectMarker.ColorOf(e.type);
                GUI.DrawTexture(new Rect(s.x - size * 0.5f, s.y - size * 0.5f, size, size), Texture2D.whiteTexture);
                if (e.type == MinimapMarkerType.Rocket) GUI.Label(new Rect(s.x - 20, s.y - 20, 40, 14), "▲", _center);
            }

            // Rover: cono de visión y flecha, orientados según la cámara.
            Camera main = Camera.main;
            float yaw = main != null ? main.transform.eulerAngles.y : t.eulerAngles.y;
            Matrix4x4 m = GUI.matrix;
            GUIUtility.RotateAroundPivot(yaw, center);
            GUI.color = new Color(0.5f, 1f, 0.6f, 0.35f);
            GUI.DrawTexture(new Rect(center.x - 32, center.y - 64, 64, 64), _cone);
            GUI.color = new Color(0.6f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(center.x - 7, center.y - 8, 14, 16), _arrow);
            GUI.matrix = m;
            GUI.color = prev;

            DrawFooter(frame, t);
        }

        private void DrawGrid(Rect map, Vector3 center)
        {
            Color prev = GUI.color;
            GUI.color = new Color(0.4f, 1f, 0.6f, 0.12f);
            float scale = sizePixels * 0.5f / halfSizeMeters;
            float startX = Mathf.Ceil((center.x - halfSizeMeters) / gridMeters) * gridMeters;
            for (float x = startX; x < center.x + halfSizeMeters; x += gridMeters)
            {
                float sx = map.center.x + (x - center.x) * scale;
                GUI.DrawTexture(new Rect(sx, map.y, 1, map.height), Texture2D.whiteTexture);
            }
            float startZ = Mathf.Ceil((center.z - halfSizeMeters) / gridMeters) * gridMeters;
            for (float z = startZ; z < center.z + halfSizeMeters; z += gridMeters)
            {
                float sy = map.center.y - (z - center.z) * scale;
                GUI.DrawTexture(new Rect(map.x, sy, map.width, 1), Texture2D.whiteTexture);
            }
            GUI.color = prev;
        }

        private void DrawFooter(Rect frame, Transform t)
        {
            string footer = "X " + t.position.x.ToString("0") + "  Z " + t.position.z.ToString("0") + Tr.T("  ·  explorado ") +
                            Mathf.RoundToInt(_fog.Explored01 * 100f) + " %";
            footer += MarsRobot.Localization.Localizer.L("  ·  M: mapa grande", "  ·  M: big map");
            if (SpaceWeatherState.CmeCountdownHours >= 0f) footer = Tr.T("<color=#ff6060>¡EYECCIÓN DE MASA CORONAL EN CAMINO!</color>  ") + footer;
            GUI.Label(new Rect(frame.x + 10, frame.yMax - 20, frame.width - 20, 18), footer, _small);
        }

        private void EnsureStyles()
        {
            if (_small != null) return;
            _small = new GUIStyle(GUI.skin.label) { fontSize = 11, richText = true };
            _small.normal.textColor = new Color(0.7f, 1f, 0.8f);
            _title = new GUIStyle(GUI.skin.label) { fontSize = 11, fontStyle = FontStyle.Bold };
            _title.normal.textColor = new Color(1f, 0.75f, 0.3f);
            _center = new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            _center.normal.textColor = Color.white;
        }

        // ------------------------------------------------------------------
        // Texturas generadas por código
        // ------------------------------------------------------------------

        private static Texture2D MakeScanlines()
        {
            var t = new Texture2D(1, 3, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            t.SetPixels32(new[] { new Color32(0, 0, 0, 200), new Color32(0, 0, 0, 0), new Color32(0, 0, 0, 0) });
            t.Apply(false, true);
            return t;
        }

        private static Texture2D MakeArrow()
        {
            const int w = 16, h = 16;
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float half = (float)y / h * (w * 0.5f); // fila de arriba (y alto) = punta
                for (int x = 0; x < w; x++)
                {
                    bool inside = Mathf.Abs(x - w * 0.5f + 0.5f) <= (w * 0.5f - half);
                    px[y * w + x] = inside ? new Color32(255, 255, 255, 255) : new Color32(0, 0, 0, 0);
                }
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D MakeCone()
        {
            const int s = 64;
            var t = new Texture2D(s, s, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    // Vértice abajo en el centro; se abre hacia arriba 60°.
                    float dx = x - s * 0.5f, dy = y;
                    float ang = Mathf.Abs(Mathf.Atan2(dx, Mathf.Max(0.001f, dy)) * Mathf.Rad2Deg);
                    float a = ang < 30f ? Mathf.Clamp01(1f - dy / s) : 0f;
                    px[y * s + x] = new Color32(255, 255, 255, (byte)(a * 255));
                }
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D MakeNoise()
        {
            var t = new Texture2D(64, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Point };
            var px = new Color32[64 * 64];
            var rng = new System.Random(5);
            for (int i = 0; i < px.Length; i++)
            {
                byte v = (byte)rng.Next(20, 200);
                px[i] = new Color32(v, v, v, 255);
            }
            t.SetPixels32(px);
            t.Apply(false, true);
            return t;
        }
    }
}
