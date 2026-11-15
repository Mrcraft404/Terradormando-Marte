using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Base.Ship
{
    /// <summary>
    /// Inspección del cohete nodriza con cámara libre (tecla K, junto a la nave o en cualquier sitio):
    ///  • Vista exterior completa al entrar; ratón izquierdo = rotar, derecho = desplazar, rueda = zoom.
    ///  • WASD mueve el punto de mira, Q/E lo bajan/suben, Mayús lo acelera: se puede entrar en las salas.
    ///  • 1–5 seleccionan un nivel (se ocultan los superiores; con Mayús se aísla solo ese nivel), 0 = todos.
    ///  • C abre el casco (corte): se ocultan los sectores que miran a la cámara.
    ///  • L muestra/oculta las etiquetas de las salas; M las cotas (altura, diámetros, altura de nivel).
    ///  • K o Esc salen y devuelven el control al robot.
    /// Mientras tanto el robot no recibe teclas (RobotInput.Suspended).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipInspector : MonoBehaviour
    {
        private ShipModel _model;
        private Camera _cam;
        private Camera _previousCam;
        private bool _savedFog;
        private float _yaw = 35f, _pitch = 12f, _dist = 75f;
        private Vector3 _target;
        private int _selected;      // 0 = todos los niveles
        private bool _isolate;
        private bool _cut, _labels = true, _dims = true;
        private readonly List<LineRenderer> _lines = new List<LineRenderer>();
        private Material _lineMat;
        private GUIStyle _label, _panel;
        private Rect _panelRect;

        public bool IsActive { get; private set; }

        private void Awake()
        {
            _model = GetComponent<ShipModel>();
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            if (IsActive) Exit();
            GameServices.Unregister(this);
        }

        private void Update()
        {
            if (!IsActive)
            {
                RobotController rc;
                bool robotFree = GameServices.TryGet(out rc) && rc.isActiveAndEnabled && rc.InputEnabled && !RobotInput.Suspended;
                if (robotFree && Input.GetKeyDown(KeyCode.K)) Enter();
                return;
            }

            if (Input.GetKeyDown(KeyCode.K) || Input.GetKeyDown(KeyCode.Escape))
            {
                Exit();
                return;
            }
            HandleKeys();
            HandleCamera();
            ApplyVisibility();
        }

        // ------------------------------------------------------------------
        // Entrar / salir
        // ------------------------------------------------------------------

        public void Enter()
        {
            if (IsActive) return;
            IsActive = true;
            RobotInput.Suspended = true;

            _previousCam = Camera.main;
            if (_previousCam != null) _previousCam.enabled = false;
            var go = new GameObject("Cámara de inspección");
            _cam = go.AddComponent<Camera>();
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 3000f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.62f, 0.47f, 0.36f);

            _savedFog = RenderSettings.fog;
            RenderSettings.fog = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            _target = transform.position + Vector3.up * 26f;
            _dist = 75f;
            _selected = 0;
            _isolate = false;
            _cut = false;
            for (int i = 0; i < _model.LevelLights.Length; i++) if (_model.LevelLights[i] != null) _model.LevelLights[i].enabled = true;
            BuildDimensionLines();
        }

        public void Exit()
        {
            if (!IsActive) return;
            IsActive = false;
            RobotInput.Suspended = false;
            if (_cam != null) Destroy(_cam.gameObject);
            if (_previousCam != null) _previousCam.enabled = true;
            RenderSettings.fog = _savedFog;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Todo visible de nuevo y luces interiores apagadas (ahorro).
            foreach (ShipModel.HullSector s in _model.HullSectors) s.go.SetActive(true);
            for (int i = 0; i < _model.Levels.Length; i++)
            {
                _model.Levels[i].gameObject.SetActive(true);
                if (_model.LevelLights[i] != null) _model.LevelLights[i].enabled = false;
            }
            foreach (LineRenderer l in _lines) if (l != null) Destroy(l.gameObject);
            _lines.Clear();
        }

        // ------------------------------------------------------------------
        // Controles
        // ------------------------------------------------------------------

        private void HandleKeys()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Alpha0)) SelectLevel(0, false);
            for (int i = 1; i <= ShipModel.LevelCount; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha0 + i)) SelectLevel(i, shift);
            }
            if (Input.GetKeyDown(KeyCode.C)) _cut = !_cut;
            if (Input.GetKeyDown(KeyCode.L)) _labels = !_labels;
            if (Input.GetKeyDown(KeyCode.M))
            {
                _dims = !_dims;
                foreach (LineRenderer l in _lines) l.enabled = _dims;
            }
        }

        public void SelectLevel(int level, bool isolate)
        {
            _selected = level;
            _isolate = isolate && level > 0;
            if (level == 0)
            {
                _target = transform.position + Vector3.up * 26f;
                _dist = 75f;
                _pitch = 12f;
                return;
            }
            // Encuadre del nivel: corte abierto y cámara algo elevada mirando al suelo.
            _cut = true;
            _target = transform.TransformPoint(new Vector3(0f, ShipModel.LevelFloorY(level - 1) + 1.2f, 0f));
            _dist = 14f;
            _pitch = 32f;
            BuildDimensionLines();
        }

        private void HandleCamera()
        {
            float dt = Time.unscaledDeltaTime;
            bool overPanel = _panelRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            if (!overPanel)
            {
                if (Input.GetMouseButton(0))
                {
                    _yaw += Input.GetAxis("Mouse X") * 4f;
                    _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * 3f, -80f, 85f);
                }
                if (Input.GetMouseButton(1))
                {
                    Transform c = _cam.transform;
                    float k = _dist * 0.02f;
                    _target -= c.right * Input.GetAxis("Mouse X") * k + c.up * Input.GetAxis("Mouse Y") * k;
                }
                float wheel = Input.GetAxis("Mouse ScrollWheel");
                if (Mathf.Abs(wheel) > 0.0001f) _dist = Mathf.Clamp(_dist * (1f - wheel * 1.5f), 1.5f, 220f);
            }

            float speed = (Input.GetKey(KeyCode.LeftShift) ? 18f : 6f) * dt * Mathf.Max(1f, _dist / 15f);
            Quaternion yawRot = Quaternion.Euler(0f, _yaw, 0f);
            Vector3 move = Vector3.zero;
            if (Input.GetKey(KeyCode.W)) move += yawRot * Vector3.forward;
            if (Input.GetKey(KeyCode.S)) move -= yawRot * Vector3.forward;
            if (Input.GetKey(KeyCode.D)) move += yawRot * Vector3.right;
            if (Input.GetKey(KeyCode.A)) move -= yawRot * Vector3.right;
            if (Input.GetKey(KeyCode.E)) move += Vector3.up;
            if (Input.GetKey(KeyCode.Q)) move -= Vector3.up;
            _target += move * speed;

            Quaternion rot = Quaternion.Euler(_pitch, _yaw, 0f);
            _cam.transform.position = _target - rot * Vector3.forward * _dist;
            _cam.transform.rotation = rot;
        }

        // ------------------------------------------------------------------
        // Visibilidad: niveles y corte del casco
        // ------------------------------------------------------------------

        private void ApplyVisibility()
        {
            for (int i = 0; i < _model.Levels.Length; i++)
            {
                bool visible = _selected == 0 || (_isolate ? i == _selected - 1 : i <= _selected - 1);
                if (_model.Levels[i].gameObject.activeSelf != visible) _model.Levels[i].gameObject.SetActive(visible);
            }

            // Sectores del casco que miran a la cámara se ocultan para ver el interior.
            Vector3 local = transform.InverseTransformPoint(_cam.transform.position);
            float camAz = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            foreach (ShipModel.HullSector s in _model.HullSectors)
            {
                bool hide = _cut && Mathf.Abs(Mathf.DeltaAngle(camAz, s.centerDeg)) < 80f;
                if (s.go.activeSelf == hide) s.go.SetActive(!hide);
            }
        }

        // ------------------------------------------------------------------
        // Cotas
        // ------------------------------------------------------------------

        private sealed class Dim
        {
            public Vector3 a, b;
            public string text;
        }

        private readonly List<Dim> _dimTexts = new List<Dim>();

        private void BuildDimensionLines()
        {
            foreach (LineRenderer l in _lines) if (l != null) Destroy(l.gameObject);
            _lines.Clear();
            _dimTexts.Clear();
            if (_lineMat == null)
            {
                Shader sh = Shader.Find("Sprites/Default");
                if (sh == null) sh = Shader.Find("Unlit/Color");
                _lineMat = new Material(sh) { color = new Color(1f, 0.8f, 0.2f) };
            }

            float r = ShipModel.OuterRadius;
            AddDim(new Vector3(r + 3f, 0f, 0f), new Vector3(r + 3f, ShipModel.TotalHeight, 0f), "Altura 52,5 m");
            AddDim(new Vector3(-r, 0.4f, r + 2f), new Vector3(r, 0.4f, r + 2f), "Ø exterior 9 m");
            AddDim(new Vector3(-r - 2.5f, 0f, 0f), new Vector3(-r - 2.5f, ShipModel.FirstLevelFloor, 0f), "Tanques y motores 0–24,5 m");
            if (_selected > 0)
            {
                float y = ShipModel.LevelFloorY(_selected - 1);
                AddDim(new Vector3(r + 1.2f, y, 0f), new Vector3(r + 1.2f, y + ShipModel.LevelHeight, 0f), "3,2 m entre niveles");
                AddDim(new Vector3(-ShipModel.InnerRadius, y + 0.15f, 0f), new Vector3(ShipModel.InnerRadius, y + 0.15f, 0f), "Ø interior 8,2 m");
            }
            foreach (LineRenderer l in _lines) l.enabled = _dims;
        }

        private void AddDim(Vector3 a, Vector3 b, string text)
        {
            var go = new GameObject("Cota: " + text);
            go.transform.SetParent(transform, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.sharedMaterial = _lineMat;
            lr.widthMultiplier = 0.08f;
            lr.startColor = lr.endColor = new Color(1f, 0.8f, 0.2f);
            // Línea con marcas en los extremos.
            Vector3 dir = (b - a).normalized;
            Vector3 side = Vector3.Cross(dir, Vector3.forward).sqrMagnitude > 0.01f ? Vector3.Cross(dir, Vector3.forward).normalized : Vector3.right;
            lr.positionCount = 6;
            lr.SetPositions(new[] { a + side * 0.5f, a - side * 0.5f, a, b, b + side * 0.5f, b - side * 0.5f });
            _lines.Add(lr);
            _dimTexts.Add(new Dim { a = a, b = b, text = text });
        }

        // ------------------------------------------------------------------
        // Interfaz: panel, etiquetas y textos de cotas
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!IsActive || _cam == null) return;
            GUI.depth = -10; // por encima del resto del HUD
            if (_label == null)
            {
                _label = new GUIStyle(GUI.skin.box) { fontSize = 12, alignment = TextAnchor.MiddleCenter, wordWrap = false };
                _label.normal.textColor = Color.white;
                _panel = new GUIStyle(GUI.skin.label) { fontSize = 13, richText = true, wordWrap = true };
                _panel.normal.textColor = Color.white;
            }

            if (_labels) DrawLabels();
            if (_dims) DrawDimTexts();

            _panelRect = new Rect(Screen.width - 380, Screen.height - 360, 370, 350);
            GUI.Box(_panelRect, "");
            GUILayout.BeginArea(new Rect(_panelRect.x + 10, _panelRect.y + 8, _panelRect.width - 20, _panelRect.height - 16));
            GUILayout.Label("<b>Inspección del cohete nodriza</b>  (52,5 × Ø 9 m)", _panel);
            GUILayout.Label(_selected == 0 ? "Vista: todos los niveles" : "Vista: " + ShipModel.LevelNames[_selected - 1] + (_isolate ? " (aislado)" : ""), _panel);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Todos")) SelectLevel(0, false);
            for (int i = 1; i <= ShipModel.LevelCount; i++) if (GUILayout.Button("N" + i)) SelectLevel(i, false);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            _cut = GUILayout.Toggle(_cut, " Corte (C)");
            _labels = GUILayout.Toggle(_labels, " Etiquetas (L)");
            bool d = GUILayout.Toggle(_dims, " Cotas (M)");
            if (d != _dims)
            {
                _dims = d;
                foreach (LineRenderer l in _lines) l.enabled = _dims;
            }
            GUILayout.EndHorizontal();
            GUILayout.Label("Ratón izq.: rotar · der.: desplazar · rueda: zoom\nWASD mover · Q/E bajar/subir · Mayús rápido\n1–5 nivel (Mayús+n: aislar) · 0 todos · K/Esc salir\n\n<i>Distribución interior ficticia para el juego: la nave real no tiene estas plantas.</i>", _panel);
            GUILayout.EndArea();
        }

        private void DrawLabels()
        {
            foreach (ShipModel.RoomLabel l in _model.Labels)
            {
                bool show;
                if (l.level == 0) show = _cut || _selected == 0;
                else if (_selected == 0) show = false; // en la vista general solo los nombres de nivel
                else show = _isolate ? l.level == _selected : l.level == _selected;
                if (!show) continue;
                DrawWorldLabel(transform.TransformPoint(l.localPos), l.text, 70f);
            }
            if (_selected == 0)
            {
                for (int i = 0; i < ShipModel.LevelCount; i++)
                {
                    Vector3 p = transform.TransformPoint(new Vector3(-ShipModel.OuterRadius - 0.5f, ShipModel.LevelFloorY(i) + 1.6f, 0f));
                    DrawWorldLabel(p, ShipModel.LevelNames[i], 400f);
                }
            }
        }

        private void DrawDimTexts()
        {
            foreach (Dim dim in _dimTexts)
            {
                Vector3 mid = transform.TransformPoint((dim.a + dim.b) * 0.5f);
                DrawWorldLabel(mid, dim.text, 400f);
            }
        }

        private void DrawWorldLabel(Vector3 world, string text, float maxDistance)
        {
            Vector3 sp = _cam.WorldToScreenPoint(world);
            if (sp.z <= 0f || sp.z > maxDistance) return;
            Vector2 size = _label.CalcSize(new GUIContent(text));
            GUI.Box(new Rect(sp.x - size.x * 0.5f - 4, Screen.height - sp.y - size.y * 0.5f, size.x + 8, size.y + 2), text, _label);
        }
    }
}
