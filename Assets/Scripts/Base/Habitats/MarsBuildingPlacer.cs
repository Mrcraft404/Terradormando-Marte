using System.Collections.Generic;
using System.Text;
using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Colocación de módulos de la base con holograma (vista previa a escala real), al estilo de
    /// los juegos de supervivencia y construcción:
    ///
    ///   N            abrir / cerrar el modo hábitats (B sigue siendo el de estructuras pequeñas)
    ///   1–4          elegir módulo en la barra · Tab (mantener) menú radial: mueve el ratón y suelta
    ///   Rueda        girar el holograma (pasos de 15°)
    ///   Alt          mantener para NO encajar con los anclajes de otros módulos
    ///   Clic izq.    construir (si el holograma está azul) · Esc salir
    ///
    /// El holograma sigue el punto al que mira la cámara (Raycast contra el terreno; si los
    /// colisionadores del terreno aún no existen tan lejos, avanza por el rayo consultando el
    /// TerrainManager). Se pone ROJO si: faltan materiales, está lejos del cohete, el terreno es
    /// demasiado inclinado (quedaría flotando o enterrado) o choca con algo (OverlapBox).
    /// Cerca de otro módulo se ENCAJA en su anclaje más próximo y, al construir, se crea un
    /// pasillo presurizado entre los dos. La obra la hace <see cref="MarsHabitatBuilder"/> capa a capa;
    /// con payPerLayer cada capa se paga al empezarla (si faltan materiales, la obra espera).
    /// Un dron de construcción podría usar la misma lógica llamando a Build() del builder.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MarsBuildingPlacer : MonoBehaviour
    {
        [Header("Catálogo")]
        [Tooltip("Módulos de la barra (1–4…). Si está vacío se usan los cuatro de serie.")]
        [SerializeField] private List<MarsModuleData> modules = new List<MarsModuleData>();

        [Header("Colocación")]
        [Tooltip("Tecla propia para abrir este modo. Por defecto ninguna: se entra desde el menú de construcción (N).")]
        [SerializeField] private KeyCode toggleKey = KeyCode.None;
        [SerializeField, Min(5f)] private float maxDistance = 120f;
        [Tooltip("Distancia a la que el holograma salta al anclaje de otro módulo.")]
        [SerializeField, Min(0f)] private float snapDistance = 9f;
        [SerializeField, Range(1f, 90f)] private float rotateStepDegrees = 15f;
        [Tooltip("Exigir estar dentro del radio de construcción del cohete (como el modo B).")]
        [SerializeField] private bool requireBaseRadius = true;
        [Tooltip("Pagar cada capa al empezarla (obra por fases). Si no, se paga todo al colocar.")]
        [SerializeField] private bool payPerLayer = true;

        [Header("Holograma")]
        [SerializeField] private Color validColor = new Color(0.25f, 0.8f, 1f, 0.32f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.22f, 0.18f, 0.32f);

        private readonly Collider[] _overlaps = new Collider[32];

        private MarsHabitatBuilder _builder;
        private BuildModeController _smallBuildMode;
        private Rigidbody _robotBody;
        private TerrainManager _terrain;
        private RocketBase _base;

        private int _selected;
        private GameObject _ghost;
        private Renderer[] _ghostRenderers = new Renderer[0];
        private Material _ghostValid, _ghostInvalid;
        private bool _ghostShowsValid;
        private bool _ghostIsGenerated;
        private float _rotationOffset;
        private bool _hasPoint;
        private bool _valid;
        private string _reason = "";
        private bool _wasLocked;

        // Encaje con otro módulo (o con la esclusa del cohete).
        private MarsHabitatController _snapTarget;
        private bool _snapRocket;
        private int _snapOtherAnchor = -1, _snapMyAnchor = -1;

        // Menú radial.
        private bool _radialOpen;
        private Vector2 _radialVector;
        private int _radialHover = -1;

        private string _message = "";
        private float _messageTime;

        public bool IsActive { get; private set; }

        public MarsModuleData SelectedModule
        {
            get { return modules.Count > 0 ? modules[Mathf.Clamp(_selected, 0, modules.Count - 1)] : null; }
        }

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            modules.RemoveAll(m => m == null);
            if (modules.Count == 0)
            {
                foreach (MarsModuleType t in System.Enum.GetValues(typeof(MarsModuleType)))
                    modules.Add(MarsModuleData.CreateDefault(t));
            }
            _builder = GetComponent<MarsHabitatBuilder>();
            if (_builder == null) _builder = gameObject.AddComponent<MarsHabitatBuilder>();
            _smallBuildMode = GetComponent<BuildModeController>();
            _robotBody = GetComponent<Rigidbody>();
            _ghostValid = HabitatMaterials.Ghost(validColor);
            _ghostInvalid = HabitatMaterials.Ghost(invalidColor);
        }

        private void OnDisable()
        {
            if (IsActive) SetActive(false);
        }

        private void Update()
        {
            if (_terrain == null) GameServices.TryGet(out _terrain);
            if (_base == null) GameServices.TryGet(out _base);
            if (_terrain == null || !_terrain.IsMapReady) return;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (RobotInput.Suspended) return;

            // El modo B y este son excluyentes.
            if (_smallBuildMode != null && _smallBuildMode.IsActive)
            {
                if (IsActive) SetActive(false);
                return;
            }
            if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey)) SetActive(!IsActive);
            if (!IsActive) return;
            if (Input.GetKeyDown(KeyCode.Escape)) { SetActive(false); return; }

            HandleSelection();
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) _rotationOffset += Mathf.Sign(wheel) * rotateStepDegrees;

            UpdatePlacement(Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt));

            // El primer clic solo captura el ratón: se construye si ya estaba capturado.
            bool lockedNow = Cursor.lockState == CursorLockMode.Locked;
            if (Input.GetMouseButtonDown(0) && lockedNow && _wasLocked && !_radialOpen) TryPlace();
            _wasLocked = lockedNow;
#endif
        }

        /// <summary>Módulos disponibles (para el menú de construcción).</summary>
        public IReadOnlyList<MarsModuleData> Modules
        {
            get { return modules; }
        }

        /// <summary>Entra en el modo con un módulo ya elegido (desde el menú de construcción).</summary>
        public void BeginPlacing(int moduleIndex)
        {
            if (!IsActive) SetActive(true);
            Select(Mathf.Clamp(moduleIndex, 0, modules.Count - 1));
        }

        /// <summary>Sale del modo (si estaba activo).</summary>
        public void Stop()
        {
            if (IsActive) SetActive(false);
        }

        private void SetActive(bool active)
        {
            IsActive = active;
            RobotInput.ZoomCaptured = active;   // la rueda gira el holograma, no hace zoom
            if (!active)
            {
                RobotInput.LookCaptured = false;
                _radialOpen = false;
                DestroyGhost();
                _snapTarget = null;
            }
            else
            {
                CreateGhost();
                Show(Tr.T("Modo hábitats: 1–4 o Tab para elegir, rueda para girar, clic para construir."));
            }
        }

        // ------------------------------------------------------------------
        // Selección: barra (1–4) y menú radial (Tab)
        // ------------------------------------------------------------------

        private void HandleSelection()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            for (int i = 0; i < modules.Count && i < 9; i++)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) Select(i);
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                _radialOpen = true;
                _radialVector = Vector2.zero;
                _radialHover = -1;
                RobotInput.LookCaptured = true;     // el ratón elige en el menú, no gira la cámara
            }
            if (_radialOpen)
            {
                _radialVector += new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 0.15f;
                _radialVector = Vector2.ClampMagnitude(_radialVector, 1f);
                if (_radialVector.magnitude > 0.35f && modules.Count > 0)
                {
                    // Sector 0 arriba y en el sentido de las agujas del reloj.
                    float angle = Mathf.Atan2(_radialVector.x, _radialVector.y) * Mathf.Rad2Deg;
                    if (angle < 0f) angle += 360f;
                    float sector = 360f / modules.Count;
                    _radialHover = Mathf.FloorToInt((angle + sector * 0.5f) / sector) % modules.Count;
                }
                if (Input.GetKeyUp(KeyCode.Tab))
                {
                    if (_radialHover >= 0) Select(_radialHover);
                    _radialOpen = false;
                    RobotInput.LookCaptured = false;
                }
            }
#endif
        }

        private void Select(int index)
        {
            if (index < 0 || index >= modules.Count || index == _selected && _ghost != null) return;
            _selected = index;
            _rotationOffset = 0f;
            CreateGhost();
        }

        // ------------------------------------------------------------------
        // Holograma
        // ------------------------------------------------------------------

        private void CreateGhost()
        {
            DestroyGhost();
            MarsModuleData data = SelectedModule;
            if (data == null) return;
            _ghost = MarsHabitatBuilder.BuildGhost(data, _ghostInvalid);
            if (_ghost == null) return;
            _ghostIsGenerated = data.ghostPrefab == null;
            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
            _ghostShowsValid = false;
            _ghost.SetActive(false); // se muestra cuando haya un punto válido en el suelo
        }

        private void DestroyGhost()
        {
            if (_ghost != null)
            {
                // Si el holograma se generó por código, sus mallas son propias: se liberan con él.
                // (Las de un ghostPrefab son assets compartidos y no se tocan.)
                if (_ghostIsGenerated)
                {
                    foreach (MeshFilter mf in _ghost.GetComponentsInChildren<MeshFilter>(true))
                    {
                        if (mf != null && mf.sharedMesh != null) HabitatGeometry.SafeDestroy(mf.sharedMesh);
                    }
                }
                HabitatGeometry.SafeDestroy(_ghost);
            }
            _ghost = null;
            _ghostRenderers = new Renderer[0];
        }

        private void SetGhostValid(bool valid)
        {
            if (_ghost == null || valid == _ghostShowsValid) return;
            _ghostShowsValid = valid;
            Material m = valid ? _ghostValid : _ghostInvalid;
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                if (_ghostRenderers[i] != null) _ghostRenderers[i].sharedMaterial = m;
            }
        }

        // ------------------------------------------------------------------
        // Posición, encaje y validación
        // ------------------------------------------------------------------

        private void UpdatePlacement(bool snappingDisabled)
        {
            MarsModuleData data = SelectedModule;
            Camera cam = Camera.main;
            if (data == null || cam == null || _ghost == null) return;

            Vector3 point;
            _hasPoint = AimPoint(cam, out point);
            if (!_hasPoint)
            {
                _ghost.SetActive(false);
                _valid = false;
                _reason = Tr.T("Apunta al suelo (máx. ") + maxDistance.ToString("0") + " m).";
                return;
            }

            // Orientación: mirando hacia la cámara + giro con la rueda.
            float yaw = cam.transform.eulerAngles.y + 180f + _rotationOffset;
            Vector3 pos = point;
            Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
            _snapTarget = null;
            _snapRocket = false;
            if (!snappingDisabled) TrySnap(data, ref pos, ref rot);
            pos.y = _terrain.GetHeightAt(pos);

            _ghost.SetActive(true);
            _ghost.transform.SetPositionAndRotation(pos, rot);
            _valid = Validate(data, pos, rot, out _reason);
            SetGhostValid(_valid);
        }

        /// <summary>Punto del terreno al que mira el centro de la pantalla.</summary>
        private bool AimPoint(Camera cam, out Vector3 point)
        {
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponent<TerrainChunk>() != null)
            {
                point = hit.point;
                return true;
            }

            // Lejos del robot los chunks no tienen colisionador: se recorre el rayo contra la altura del terreno.
            const float step = 1.5f;
            Vector3 prev = ray.origin;
            for (float d = step; d <= maxDistance; d += step)
            {
                Vector3 p = ray.GetPoint(d);
                if (p.y <= _terrain.GetHeightAt(p))
                {
                    // Refinado por bisección entre el último punto por encima y este.
                    Vector3 a = prev, b = p;
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 m = (a + b) * 0.5f;
                        if (m.y <= _terrain.GetHeightAt(m)) b = m; else a = m;
                    }
                    point = b;
                    return true;
                }
                prev = p;
            }
            point = Vector3.zero;
            return false;
        }

        /// <summary>
        /// Busca la puerta LIBRE más cercana de otro módulo (o la esclusa del cohete): gira el
        /// holograma para que una de sus puertas quede enfrentada y lo coloca a la distancia del pasillo.
        /// </summary>
        private void TrySnap(MarsModuleData data, ref Vector3 pos, ref Quaternion rot)
        {
            List<HabitatAnchor> mine = data.GetAnchors();
            float best = snapDistance;
            Vector3 raw = pos;

            IReadOnlyList<MarsHabitatController> all = MarsHabitatController.All;
            for (int h = 0; h < all.Count; h++)
            {
                MarsHabitatController other = all[h];
                if (other == null || other.Data == null) continue;
                Transform ot = other.transform;
                if ((ot.position - raw).sqrMagnitude > Mathf.Pow(snapDistance + 80f, 2f)) continue;

                List<HabitatAnchor> theirs = other.Data.GetAnchors();
                for (int ai = 0; ai < theirs.Count; ai++)
                {
                    if (other.HasOpening(ai)) continue; // esa puerta ya tiene pasillo
                    Vector3 aPos = ot.TransformPoint(theirs[ai].localPosition);
                    Vector3 aDir = ot.TransformDirection(theirs[ai].localDirection);
                    if (SnapToAnchor(data, mine, aPos, aDir, raw, ref best, ref pos, ref rot))
                    {
                        _snapTarget = other;
                        _snapRocket = false;
                        _snapOtherAnchor = ai;
                    }
                }
            }

            // La esclusa del cohete (entrada desde la nave): una sola conexión.
            if (_base != null && (RocketAirlock.Instance == null || !RocketAirlock.Instance.IsConnected))
            {
                Vector3 door, dir;
                RocketAirlock.ComputeDoor(_base, out door, out dir);
                if (SnapToAnchor(data, mine, door, dir, raw, ref best, ref pos, ref rot))
                {
                    _snapTarget = null;
                    _snapRocket = true;
                    _snapOtherAnchor = -1;
                }
            }
        }

        /// <summary>Prueba a enfrentar cada puerta propia con un anclaje ajeno; actualiza la mejor colocación.</summary>
        private bool SnapToAnchor(MarsModuleData data, List<HabitatAnchor> mine, Vector3 aPos, Vector3 aDir, Vector3 raw,
                                  ref float best, ref Vector3 pos, ref Quaternion rot)
        {
            bool improved = false;
            Vector3 meet = aPos + aDir * data.connectorLength;
            for (int bi = 0; bi < mine.Count; bi++)
            {
                HabitatAnchor b = mine[bi];
                // Giro que pone la puerta propia mirando a la otra.
                float yaw = Mathf.Atan2(-aDir.x, -aDir.z) * Mathf.Rad2Deg -
                            Mathf.Atan2(b.localDirection.x, b.localDirection.z) * Mathf.Rad2Deg;
                Quaternion r = Quaternion.Euler(0f, yaw, 0f);
                Vector3 candidate = meet - r * b.localPosition;
                float dist = Vector2.Distance(new Vector2(candidate.x, candidate.z), new Vector2(raw.x, raw.z));
                if (dist < best)
                {
                    best = dist;
                    pos = new Vector3(candidate.x, pos.y, candidate.z);
                    rot = r;
                    _snapMyAnchor = bi;
                    improved = true;
                }
            }
            return improved;
        }

        private bool Validate(MarsModuleData d, Vector3 pos, Quaternion rot, out string reason)
        {
            // 1) Materiales.
            BaseInventory inv = _base != null ? _base.Inventory : null;
            ResourceAmount[] needed = payPerLayer ? d.GetLayerCost(HabitatLayer.Foundation) : d.gameCost;
            if (inv != null && needed != null && needed.Length > 0 && !inv.Has(needed))
            {
                reason = Tr.T("Faltan materiales: ") + BaseInventory.Describe(needed);
                return false;
            }

            Vector3 fp = d.FootprintHalfExtents;

            // 2) Radio de construcción del cohete.
            if (requireBaseRadius && _base != null)
            {
                Vector3 toBase = pos - _base.transform.position;
                toBase.y = 0f;
                if (toBase.magnitude > RocketBase.BuildRadius + Mathf.Max(fp.x, fp.z))
                {
                    reason = Tr.T("Demasiado lejos del cohete (máx. ") + RocketBase.BuildRadius.ToString("0") + " m).";
                    return false;
                }
            }

            // 3) Pendiente: alturas del terreno en el borde de la losa. Si el desnivel supera la
            //    pendiente máxima o lo que cubren losa + zapatas, el módulo quedaría flotando o enterrado.
            Vector3 slabHalf = d.IsDomeLike
                ? new Vector3(d.radius + 1f, 0f, d.radius + 1f)
                : new Vector3(d.ShellSize.x * 0.5f + 1f, 0f, d.ShellSize.z * 0.5f + 1f);
            float minH = _terrain.GetHeightAt(pos), maxH = minH;
            const int samples = 16;
            for (int i = 0; i < samples; i++)
            {
                float a = i * Mathf.PI * 2f / samples;
                Vector3 local = d.IsDomeLike
                    ? new Vector3(Mathf.Cos(a) * slabHalf.x, 0f, Mathf.Sin(a) * slabHalf.z)
                    : new Vector3(Mathf.Clamp(Mathf.Cos(a) * 1.5f, -1f, 1f) * slabHalf.x, 0f, Mathf.Clamp(Mathf.Sin(a) * 1.5f, -1f, 1f) * slabHalf.z);
                float hgt = _terrain.GetHeightAt(pos + rot * local);
                minH = Mathf.Min(minH, hgt);
                maxH = Mathf.Max(maxH, hgt);
            }
            float span = 2f * Mathf.Max(slabHalf.x, slabHalf.z);
            float slopeDeg = Mathf.Atan2(maxH - minH, span) * Mathf.Rad2Deg;
            if (slopeDeg > d.maxSlopeDegrees)
            {
                reason = Tr.T("Terreno demasiado inclinado (") + slopeDeg.ToString("0.0") + Tr.T("°, máx. ") + d.maxSlopeDegrees.ToString("0") + "°).";
                return false;
            }
            if (maxH - minH > d.foundationThickness + d.footingDepth)
            {
                reason = Tr.T("Desnivel de ") + (maxH - minH).ToString("0.0") + Tr.T(" m: la losa quedaría en el aire.");
                return false;
            }

            // 4) Espacio libre: caja de la huella completa (con escudo). No cuentan el terreno ni el robot.
            var half = new Vector3(fp.x * 0.95f, fp.y * 0.5f, fp.z * 0.95f);
            Vector3 center = pos + Vector3.up * (half.y + 0.5f);
            int n = Physics.OverlapBoxNonAlloc(center, half, _overlaps, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = _overlaps[i];
                if (c == null || c.GetComponent<TerrainChunk>() != null) continue;
                if (_robotBody != null && c.attachedRigidbody == _robotBody) continue;
                if (c.transform.IsChildOf(transform)) continue;
                reason = Tr.T("Choca con ") + c.transform.root.name + ".";
                return false;
            }

            reason = "";
            return true;
        }

        // ------------------------------------------------------------------
        // Construcción
        // ------------------------------------------------------------------

        private void TryPlace()
        {
            MarsModuleData data = SelectedModule;
            if (data == null || _ghost == null || !_hasPoint) return;
            if (!_valid)
            {
                Show(_reason);
                return;
            }

            BaseInventory inv = _base != null ? _base.Inventory : null;
            System.Func<MarsHabitatController, HabitatLayer, bool> gate = null;
            if (inv != null)
            {
                if (payPerLayer)
                {
                    // Cada capa se paga al empezarla; si no hay materiales, la obra espera.
                    gate = (habitat, layer) => inv.TrySpend(data.GetLayerCost(layer));
                }
                else if (!inv.TrySpend(data.gameCost))
                {
                    Show(Tr.T("Faltan materiales."));
                    return;
                }
            }

            Vector3 pos = _ghost.transform.position;
            Quaternion rot = _ghost.transform.rotation;
            MarsHabitatController habitat = _builder.Build(data, pos, rot, gate);
            if (habitat == null) return;

            if (_snapRocket && _base != null)
            {
                RocketAirlock airlock = RocketAirlock.GetOrCreate(_base);
                if (MarsHabitatBuilder.ConnectToRocket(habitat, _snapMyAnchor, airlock) != null) airlock.MarkConnected();
                Show(data.displayName + Tr.T(": en obra, unido a la esclusa del cohete. Desde el hangar, F en la escotilla baja a la base."));
            }
            else if (_snapTarget != null)
            {
                MarsHabitatBuilder.ConnectHabitats(_snapTarget, _snapOtherAnchor, habitat, _snapMyAnchor);
                Show(data.displayName + Tr.T(": en obra, unido a ") + _snapTarget.name + Tr.T(" con un pasillo presurizado."));
            }
            else
            {
                Show(data.displayName + Tr.T(": en obra (5 capas)."));
            }
        }

        private void Show(string text)
        {
            _message = text;
            _messageTime = Time.time;
        }

        // ------------------------------------------------------------------
        // Interfaz (IMGUI provisional): barra, menú radial y estado
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!IsActive) return;
            GUI.skin.label.richText = true;
            DrawHotbar();
            DrawInfo();
            if (_radialOpen) DrawRadial();
            DrawConstructionSites();
        }

        private void DrawHotbar()
        {
            const float slotW = 250f, slotH = 76f, gap = 6f;
            float total = modules.Count * slotW + (modules.Count - 1) * gap;
            float x0 = (Screen.width - total) * 0.5f, y = Screen.height - slotH - 12f;
            BaseInventory inv = _base != null ? _base.Inventory : null;
            for (int i = 0; i < modules.Count; i++)
            {
                MarsModuleData m = modules[i];
                var r = new Rect(x0 + i * (slotW + gap), y, slotW, slotH);
                GUI.color = i == _selected ? new Color(1f, 0.85f, 0.35f) : Color.white;
                GUI.Box(r, "");
                GUI.color = Color.white;
                bool affordable = inv == null || inv.Has(payPerLayer ? m.GetLayerCost(HabitatLayer.Foundation) : m.gameCost);
                string col = affordable ? "white" : "#ff9090";
                Texture2D img = StructureArt.ForModule(m.moduleId);
                if (img != null)
                {
                    GUI.DrawTexture(new Rect(r.x + 4, r.y + 4, r.height - 8, r.height - 8), img, ScaleMode.ScaleAndCrop);
                    r = new Rect(r.x + r.height - 4, r.y, r.width - r.height + 4, r.height);
                }
                GUI.Label(new Rect(r.x + 6, r.y + 4, r.width - 12, r.height - 8),
                    "<b>" + (i + 1) + ". " + m.displayName + "</b>\n<color=" + col + ">" +
                    m.designPressureKPa.ToString("0.#") + " kPa · " + DimensionsLabel(m) + "</color>\n<size=10>" +
                    BaseInventory.Describe(m.gameCost) + "</size>");
            }
        }

        private void DrawInfo()
        {
            MarsModuleData d = SelectedModule;
            if (d == null) return;
            var sb = new StringBuilder();
            sb.Append(Tr.T("<b>MODO HÁBITATS</b> — ")).Append(d.displayName).Append("  (").Append(DimensionsLabel(d)).Append(")\n");
            sb.Append(d.description).Append('\n');
            if (_snapRocket) sb.Append(Tr.T("<color=#80d8ff>Encaja con la esclusa del cohete (entrada desde la nave)</color>\n"));
            else if (_snapTarget != null) sb.Append(Tr.T("<color=#80d8ff>Encaja con ")).Append(_snapTarget.name).Append(Tr.T(" (pasillo de "))
                                            .Append(d.connectorLength.ToString("0")).Append(" m)</color>\n");
            else sb.Append(Tr.T("Acércalo a la puerta de otro módulo o a la parte trasera del cohete para conectarlo.\n"));
            sb.Append(_valid ? "<color=#80d8ff>Clic izquierdo: construir</color>" : "<color=#ff8080>" + _reason + "</color>");
            if (payPerLayer) sb.Append(Tr.T("\nObra por fases: cada capa se paga al empezarla."));
            sb.Append(Tr.T("\n1–4 / Tab elegir · Rueda girar · Alt sin encaje · N o Esc salir"));
            if (Time.time - _messageTime < 4f && _message.Length > 0) sb.Append("\n<b>").Append(_message).Append("</b>");
            var r = new Rect(10f, Screen.height - 210f - 84f, 560f, 205f);
            GUI.Box(r, "");
            Texture2D img = StructureArt.ForModule(d.moduleId);
            float tx = 8f;
            if (img != null)
            {
                GUI.DrawTexture(new Rect(r.x + 6, r.y + 6, 150f, 150f), img, ScaleMode.ScaleAndCrop);
                tx = 164f;
            }
            GUI.Label(new Rect(r.x + tx, r.y + 6, r.width - tx - 8, r.height - 12), sb.ToString());
        }

        private void DrawRadial()
        {
            var center = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            const float radius = 150f;
            GUI.Box(new Rect(center.x - 60f, center.y - 18f, 120f, 36f), Tr.T("Suelta Tab"));
            for (int i = 0; i < modules.Count; i++)
            {
                float a = i * Mathf.PI * 2f / modules.Count; // 0 = arriba, sentido horario
                var p = new Vector2(center.x + Mathf.Sin(a) * radius, center.y - Mathf.Cos(a) * radius);
                var r = new Rect(p.x - 85f, p.y - 26f, 170f, 52f);
                GUI.color = i == _radialHover ? new Color(0.4f, 0.9f, 1f) : new Color(1f, 1f, 1f, 0.85f);
                GUI.Box(r, "");
                GUI.color = Color.white;
                GUI.Label(new Rect(r.x + 6, r.y + 4, r.width - 12, r.height - 8),
                    "<b>" + modules[i].displayName + "</b>\n" + modules[i].designPressureKPa.ToString("0.#") + " kPa");
            }
            // Indicador de la dirección elegida con el ratón.
            Vector2 tip = center + new Vector2(_radialVector.x, -_radialVector.y) * 70f;
            GUI.Box(new Rect(tip.x - 5f, tip.y - 5f, 10f, 10f), "");
        }

        private void DrawConstructionSites()
        {
            var sb = new StringBuilder();
            IReadOnlyList<MarsHabitatController> all = MarsHabitatController.All;
            for (int i = 0; i < all.Count; i++)
            {
                MarsHabitatController h = all[i];
                if (h == null) continue;
                if (h.IsUnderConstruction) sb.Append(h.name).Append(": ").Append(h.ConstructionStatus).Append('\n');
                else if (!h.IsComplete) sb.Append("<color=#ffb070>").Append(h.name).Append(": ")
                                         .Append(MarsHabitatController.Describe(h.Issues)).Append("</color>\n");
            }
            if (sb.Length == 0) return;
            var r = new Rect(Screen.width - 420f, 10f, 410f, 22f + 18f * Mathf.Min(8, all.Count));
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x + 8, r.y + 4, r.width - 16, r.height - 8), Tr.T("<b>Obras</b>\n") + sb);
        }

        private static string DimensionsLabel(MarsModuleData d)
        {
            if (d.IsDomeLike)
                return "Ø " + (d.radius * 2f).ToString("0.#") + Tr.T(" m · alto ") + d.DomeHeight.ToString("0.#") + " m";
            Vector3 s = d.ShellSize;
            return s.x.ToString("0.#") + " × " + s.z.ToString("0.#") + Tr.T(" m · alto ") + s.y.ToString("0.#") + " m";
        }
    }
}
