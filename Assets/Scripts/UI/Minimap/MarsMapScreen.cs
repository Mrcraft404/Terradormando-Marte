using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.Spawns;
using MarsRobot.World.GlobalMap;
using UnityEngine;

namespace MarsRobot.UI.Minimap
{
    /// <summary>
    /// Mapa a pantalla completa (tecla M). Dos pestañas:
    ///  • "Esta zona": toda la zona en relieve, con lo explorado, la rejilla cada 100 m, el robot,
    ///    la base y los marcadores (con leyenda). Rueda o botones + / − para acercar.
    ///  • "Marte": el planeta entero con las zonas de aterrizaje (los puntos de aparición).
    ///    Clic en una zona = ver su ficha; "Viajar a esta zona" aterriza allí (antes era F1–F6).
    /// Mientras está abierto, el robot no se mueve. M o Esc cierran.
    /// </summary>
    public sealed class MarsMapScreen : MonoBehaviour
    {
        private bool _open;
        private bool _prevSuspended;
        private int _tab;
        private float _zoom = 1f;
        private LandingZoneData _selected;
        private bool _confirm;
        private GUIStyle _label, _small, _big, _tag;

        public static bool IsOpen { get; private set; }

        private static string L(string es, string en)
        {
            return MarsRobot.Localization.Localizer.L(es, en);
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (_open)
            {
                if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Escape)) SetOpen(false);
                float wheel = Input.mouseScrollDelta.y;
                if (_tab == 0 && Mathf.Abs(wheel) > 0.01f) _zoom = Mathf.Clamp(_zoom * (wheel > 0f ? 1.25f : 0.8f), 1f, 8f);
                return;
            }
            if (Input.GetKeyDown(KeyCode.M) && !RobotInput.Suspended && !MarsRobot.Dialogue.DialogueManager.AnyShowing)
            {
                SetOpen(true);
            }
#endif
        }

        private void SetOpen(bool open)
        {
            if (open == _open) return;
            _open = open;
            IsOpen = open;
            if (open)
            {
                _prevSuspended = RobotInput.Suspended;
                RobotInput.Suspended = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _confirm = false;
                MarsGlobalMap.GenerateAsync();
            }
            else
            {
                RobotInput.Suspended = _prevSuspended;
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void OnDisable()
        {
            if (_open) SetOpen(false);
        }

        private void OnGUI()
        {
            if (!_open) return;
            EnsureStyles();
            HudTheme.BeginScaled();
            HudTheme.Fill(new Rect(-400f, -200f, 2400f, 1300f), new Color(0f, 0f, 0f, 0.7f));
            var win = new Rect(30f, 20f, 1540f, 860f);
            HudTheme.Panel(win, L("MAPA", "MAP"), L("M o Esc para cerrar", "M or Esc to close"));

            string[] tabs = { L("Esta zona", "This zone"), L("Marte entero", "Whole Mars") };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (GUI.Button(new Rect(win.x + 900f + i * 210f, win.y + 18f, 200f, 44f), tabs[i], i == _tab ? HudTheme.TabActive : HudTheme.Tab))
                {
                    _tab = i;
                    _confirm = false;
                }
            }
            if (GUI.Button(new Rect(win.xMax - 150f, win.y + 18f, 130f, 44f), L("Cerrar ✕", "Close ✕"), HudTheme.Tab))
            {
                HudTheme.EndScaled();
                SetOpen(false);
                return;
            }

            var body = new Rect(win.x + 18f, win.y + 80f, win.width - 36f, win.height - 98f);
            if (_tab == 0) DrawZone(body);
            else DrawMars(body);
            HudTheme.EndScaled();
        }

        // ------------------------------------------------------------------
        // Pestaña 1: esta zona
        // ------------------------------------------------------------------

        private void DrawZone(Rect body)
        {
            MarsMinimapController mini = MarsMinimapController.Instance;
            Transform robot = MarsMinimapController.FollowTarget();
            var map = new Rect(body.x, body.y, body.height, body.height);
            HudTheme.Frame(map, new Color(0.02f, 0.02f, 0.03f, 1f), HudTheme.AccentDim, 2f);
            var inner = new Rect(map.x + 4f, map.y + 4f, map.width - 8f, map.height - 8f);

            Texture2D relief = ZoneReliefMap.Texture;
            if (relief == null || robot == null)
            {
                GUI.Label(inner, L("Dibujando el mapa…", "Drawing the map…"), _big);
                DrawLegend(new Rect(map.xMax + 20f, body.y, body.xMax - map.xMax - 20f, body.height), robot, mini);
                return;
            }
            if (mini != null && !mini.HasSignal)
            {
                GUI.Label(inner, L("SIN SEÑAL\nRepara el cable del mapa o espera a que pase la tormenta solar.",
                                   "NO SIGNAL\nRepair the map cable or wait for the solar storm to pass."), _big);
                DrawLegend(new Rect(map.xMax + 20f, body.y, body.xMax - map.xMax - 20f, body.height), robot, mini);
                return;
            }

            // Vista: toda la zona (zoom 1) o un trozo centrado en el robot.
            float size = ZoneReliefMap.WorldSize;
            float half = size * 0.5f / _zoom;
            Vector3 o = ZoneReliefMap.Origin;
            Vector3 c = _zoom <= 1.01f ? new Vector3(o.x + size * 0.5f, 0f, o.z + size * 0.5f) : robot.position;
            c.x = Mathf.Clamp(c.x, o.x + half, o.x + size - half);
            c.z = Mathf.Clamp(c.z, o.z + half, o.z + size - half);

            GUI.DrawTextureWithTexCoords(inner, relief, ZoneReliefMap.UVRect(c, half));
            if (mini != null && mini.Fog != null && mini.Fog.Texture != null)
            {
                Color prevF = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, 0.75f); // lo no explorado, oscurecido pero visible
                GUI.DrawTextureWithTexCoords(inner, mini.Fog.Texture, mini.Fog.UVRect(c, half));
                GUI.color = prevF;
            }

            float scale = inner.width * 0.5f / half;
            System.Func<Vector3, Vector2> toScreen = p => new Vector2(inner.center.x + (p.x - c.x) * scale, inner.center.y - (p.z - c.z) * scale);

            // Rejilla cada 100 m.
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.12f);
            float step = 100f;
            for (float x = Mathf.Ceil((c.x - half) / step) * step; x < c.x + half; x += step)
                GUI.DrawTexture(new Rect(toScreen(new Vector3(x, 0, 0)).x, inner.y, 1f, inner.height), Texture2D.whiteTexture);
            for (float z = Mathf.Ceil((c.z - half) / step) * step; z < c.z + half; z += step)
                GUI.DrawTexture(new Rect(inner.x, toScreen(new Vector3(0, 0, z)).y, inner.width, 1f), Texture2D.whiteTexture);
            GUI.color = prev;

            // Marcadores con su nombre (los importantes).
            foreach (MinimapObjectMarker.Entry e in MinimapObjectMarker.All)
            {
                Vector3 p = e.follow != null ? e.follow.position : e.position;
                Vector2 s = toScreen(p);
                if (!inner.Contains(s)) continue;
                bool big = e.type == MinimapMarkerType.Rocket || e.type == MinimapMarkerType.Discovery;
                float sz = big ? 16f : 9f;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(s.x - sz * 0.5f - 2f, s.y - sz * 0.5f - 2f, sz + 4f, sz + 4f), Texture2D.whiteTexture);
                GUI.color = MinimapObjectMarker.ColorOf(e.type);
                GUI.DrawTexture(new Rect(s.x - sz * 0.5f, s.y - sz * 0.5f, sz, sz), Texture2D.whiteTexture);
                GUI.color = prev;
                if (big)
                {
                    string name = e.type == MinimapMarkerType.Rocket ? L("Base (cohete)", "Base (rocket)") : (string.IsNullOrEmpty(e.label) ? "?" : e.label);
                    GUI.Label(new Rect(s.x + 12f, s.y - 12f, 260f, 24f), name, _tag);
                }
            }

            // Robot: cono y flecha.
            if (mini != null)
            {
                Vector2 r = toScreen(robot.position);
                Camera cam = Camera.main;
                float yaw = cam != null ? cam.transform.eulerAngles.y : robot.eulerAngles.y;
                Matrix4x4 m = GUI.matrix;
                GUIUtility.RotateAroundPivot(yaw, r);
                GUI.color = new Color(0.5f, 1f, 0.6f, 0.4f);
                GUI.DrawTexture(new Rect(r.x - 48f, r.y - 96f, 96f, 96f), mini.ConeTexture);
                GUI.color = new Color(0.6f, 1f, 0.7f);
                GUI.DrawTexture(new Rect(r.x - 11f, r.y - 12f, 22f, 24f), mini.ArrowTexture);
                GUI.matrix = m;
                GUI.color = prev;
            }

            // Norte y zoom.
            GUI.Label(new Rect(inner.x + 10f, inner.y + 6f, 60f, 30f), "▲ N", _big);
            if (GUI.Button(new Rect(inner.xMax - 110f, inner.y + 10f, 46f, 40f), "+", HudTheme.Tab)) _zoom = Mathf.Min(8f, _zoom * 1.5f);
            if (GUI.Button(new Rect(inner.xMax - 58f, inner.y + 10f, 46f, 40f), "−", HudTheme.Tab)) _zoom = Mathf.Max(1f, _zoom / 1.5f);

            DrawLegend(new Rect(map.xMax + 20f, body.y, body.xMax - map.xMax - 20f, body.height), robot, mini);
        }

        private void DrawLegend(Rect r, Transform robot, MarsMinimapController mini)
        {
            HudTheme.Frame(r, new Color(0.03f, 0.08f, 0.14f, 0.9f), HudTheme.AccentDim, 1f);
            float y = r.y + 14f;
            MarsRobot.Base.RocketBase rb;
            LandingZoneData zone = GameServices.TryGet(out rb) ? rb.Zone : null;
            GUI.Label(new Rect(r.x + 16f, y, r.width - 32f, 34f), zone != null ? zone.displayName : L("Zona", "Zone"), _big);
            y += 40f;
            if (robot != null)
            {
                GUI.Label(new Rect(r.x + 16f, y, r.width - 32f, 26f), L("Tu robot: ", "Your robot: ") + "X " + robot.position.x.ToString("0") + "  Z " + robot.position.z.ToString("0"), _label);
                y += 30f;
            }
            if (mini != null && mini.Fog != null)
            {
                float ex = mini.Fog.Explored01;
                GUI.Label(new Rect(r.x + 16f, y, r.width - 32f, 26f), L("Explorado: ", "Explored: ") + Mathf.RoundToInt(ex * 100f) + " %", _label);
                HudTheme.Bar(new Rect(r.x + 16f, y + 28f, r.width - 32f, 14f), ex, HudTheme.Accent, "");
                y += 56f;
            }

            GUI.Label(new Rect(r.x + 16f, y, r.width - 32f, 28f), L("Qué significa cada color", "What each color means"), _big);
            y += 36f;
            var counts = new Dictionary<MinimapMarkerType, int>();
            foreach (MinimapObjectMarker.Entry e in MinimapObjectMarker.All)
            {
                int n;
                counts.TryGetValue(e.type, out n);
                counts[e.type] = n + 1;
            }
            foreach (MinimapMarkerType t in System.Enum.GetValues(typeof(MinimapMarkerType)))
            {
                if (t == MinimapMarkerType.Vehicle) continue;
                int n;
                counts.TryGetValue(t, out n);
                HudTheme.Fill(new Rect(r.x + 18f, y + 5f, 16f, 16f), MinimapObjectMarker.ColorOf(t));
                GUI.Label(new Rect(r.x + 44f, y, r.width - 60f, 26f), TypeName(t) + (n > 0 ? "  (" + n + ")" : ""), _label);
                y += 30f;
            }
            HudTheme.Fill(new Rect(r.x + 18f, y + 5f, 16f, 16f), new Color(0.6f, 1f, 0.7f));
            GUI.Label(new Rect(r.x + 44f, y, r.width - 60f, 26f), L("Tú (el robot) y hacia dónde miras", "You (the robot) and where you look"), _label);
            y += 44f;
            GUI.Label(new Rect(r.x + 16f, y, r.width - 32f, 90f),
                L("Lo oscuro es lo que aún no has explorado. Rueda del ratón o + / −: acercar y alejar.\nPestaña \"Marte entero\": otras zonas para aterrizar.",
                  "The dark part is what you haven't explored yet. Mouse wheel or + / −: zoom in and out.\n\"Whole Mars\" tab: other zones to land in."), _small);
        }

        private static string TypeName(MinimapMarkerType t)
        {
            switch (t)
            {
                case MinimapMarkerType.Rocket: return L("Base y cohete", "Base and rocket");
                case MinimapMarkerType.Resource: return L("Rocas con minerales", "Rocks with minerals");
                case MinimapMarkerType.Ice: return L("Hielo", "Ice");
                case MinimapMarkerType.Structure: return L("Tus construcciones", "Your buildings");
                case MinimapMarkerType.Beacon: return L("Antenas y balizas", "Antennas and beacons");
                case MinimapMarkerType.Discovery: return L("Lugares para descubrir", "Places to discover");
                default: return L("Peligro", "Danger");
            }
        }

        // ------------------------------------------------------------------
        // Pestaña 2: Marte entero, con las zonas de aterrizaje
        // ------------------------------------------------------------------

        private void DrawMars(Rect body)
        {
            var m = new Rect(body.x, body.y, body.width, body.width * 0.5f);
            if (m.height > body.height - 150f)
            {
                m.height = body.height - 150f;
                m.width = m.height * 2f;
                m.x = body.x + (body.width - m.width) * 0.5f;
            }
            HudTheme.Frame(new Rect(m.x - 3f, m.y - 3f, m.width + 6f, m.height + 6f), Color.black, HudTheme.AccentDim, 2f);
            Texture2D tex = MarsGlobalMap.Texture;
            if (tex != null) GUI.DrawTexture(m, tex, ScaleMode.StretchToFill);
            else GUI.Label(m, L("Dibujando Marte…", "Drawing Mars…"), _big);

            // Lugares famosos (en pequeño).
            Color prev = GUI.color;
            foreach (MarsLandmark lm in MarsGlobalMap.Landmarks)
            {
                Vector2 p = LatLon(m, lm.Latitude, lm.Longitude);
                GUI.Label(new Rect(p.x - 80f, p.y - 10f, 160f, 20f), lm.Name, _small);
            }

            MarsRobot.Base.RocketBase rb;
            LandingZoneData here = GameServices.TryGet(out rb) ? rb.Zone : null;
            LandingZoneCatalog cat = MarsRobot.Spawns.TestSceneBootstrap.Catalog;
            var zones = new List<LandingZoneData>();
            if (cat != null) foreach (LandingZoneData z in cat.zones) if (z != null) zones.Add(z);
            if (here != null && !zones.Contains(here)) zones.Add(here);

            foreach (LandingZoneData z in zones)
            {
                Vector2 p = LatLon(m, z.latitude, MarsGlobalMap.ToSigned(z.longitudeEast));
                bool isHere = here != null && (z == here || z.zoneId == here.zoneId);
                bool sel = z == _selected;
                float pulse = isHere ? 6f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f)) : 0f;
                float s = (sel ? 26f : 20f) + pulse;
                GUI.color = Color.black;
                GUI.DrawTexture(new Rect(p.x - s * 0.5f - 3f, p.y - s * 0.5f - 3f, s + 6f, s + 6f), Texture2D.whiteTexture);
                GUI.color = isHere ? new Color(0.4f, 1f, 0.5f) : (sel ? new Color(1f, 0.85f, 0.2f) : new Color(1f, 0.55f, 0.2f));
                GUI.DrawTexture(new Rect(p.x - s * 0.5f, p.y - s * 0.5f, s, s), Texture2D.whiteTexture);
                GUI.color = prev;
                GUI.Label(new Rect(p.x + 16f, p.y - 14f, 280f, 28f), z.displayName + (isHere ? L("  (estás aquí)", "  (you are here)") : ""), _tag);
                if (GUI.Button(new Rect(p.x - 18f, p.y - 18f, 36f, 36f), GUIContent.none, GUIStyle.none))
                {
                    _selected = z;
                    _confirm = false;
                }
            }

            // Ficha de la zona elegida.
            var info = new Rect(body.x, m.yMax + 12f, body.width, body.yMax - m.yMax - 12f);
            HudTheme.Frame(info, new Color(0.03f, 0.08f, 0.14f, 0.92f), HudTheme.AccentDim, 1f);
            if (_selected == null)
            {
                GUI.Label(new Rect(info.x + 16f, info.y + 12f, info.width - 32f, info.height - 24f),
                    L("Los cuadrados naranjas son las zonas donde puedes aterrizar. Haz clic en una para ver cómo es y viajar allí.\nEl verde es donde estás ahora.",
                      "The orange squares are the zones where you can land. Click one to see what it's like and travel there.\nThe green one is where you are now."), _label);
                return;
            }
            bool selHere = here != null && (_selected == here || _selected.zoneId == here.zoneId);
            GUI.Label(new Rect(info.x + 16f, info.y + 8f, 700f, 32f), _selected.displayName + "   ·   " + LandingZoneData.DifficultyLabel(_selected.difficulty), _big);
            GUI.Label(new Rect(info.x + 16f, info.y + 42f, info.width - 380f, info.height - 50f), _selected.description, _small);
            if (selHere)
            {
                HudTheme.StatePill(new Rect(info.xMax - 340f, info.y + 30f, 320f, 40f), L("Ya estás en esta zona", "You are already here"), HudTheme.Good);
            }
            else if (!_confirm)
            {
                if (GUI.Button(new Rect(info.xMax - 340f, info.y + 24f, 320f, 56f), L("Viajar a esta zona", "Travel to this zone"), HudTheme.TabActive)) _confirm = true;
            }
            else
            {
                GUI.Label(new Rect(info.xMax - 360f, info.y + 4f, 350f, 44f), L("Empezarás de nuevo allí. ¿Seguro?", "You will start again there. Sure?"), _label);
                if (GUI.Button(new Rect(info.xMax - 340f, info.y + 50f, 150f, 50f), L("Sí, viajar", "Yes, travel"), HudTheme.TabActive))
                {
                    LandingZoneData target = _selected;
                    HudTheme.EndScaled();
                    SetOpen(false);
                    MarsRobot.Spawns.TestSceneBootstrap.TravelTo(target);
                    HudTheme.BeginScaled();
                    return;
                }
                if (GUI.Button(new Rect(info.xMax - 180f, info.y + 50f, 160f, 50f), L("No", "No"), HudTheme.Tab)) _confirm = false;
            }
        }

        private static Vector2 LatLon(Rect m, float lat, float lon)
        {
            return new Vector2(m.x + (lon + 180f) / 360f * m.width, m.y + (90f - lat) / 180f * m.height);
        }

        private void EnsureStyles()
        {
            if (_label != null) return;
            _label = new GUIStyle(GUI.skin.label) { fontSize = 20, wordWrap = true, richText = true };
            _label.normal.textColor = new Color(0.9f, 0.95f, 1f);
            _small = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, alignment = TextAnchor.UpperLeft };
            _small.normal.textColor = new Color(0.8f, 0.85f, 0.9f);
            _big = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, wordWrap = true, alignment = TextAnchor.UpperLeft };
            _big.normal.textColor = new Color(1f, 0.8f, 0.4f);
            _tag = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
            _tag.normal.textColor = Color.white;
        }
    }
}
