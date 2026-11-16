using System;
using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.UI;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Menú de construcción espacial (tecla N), al estilo de los juegos de supervivencia y
    /// construcción: una cuadrícula con la FOTO de cada pieza, por pestañas:
    ///   Cimientos y suelos · Paredes · Cristal · Puertas · Techos · Estructura ·
    ///   Módulos presurizados (hábitats completos) · Equipos de la base (paneles, tanques… del modo B).
    ///
    /// Al elegir una pieza aparece su holograma, que ENCAJA en la cuadrícula de 4 m de la
    /// estructura más cercana (cimientos junto a cimientos, paredes en los bordes, techos encima…).
    ///   Clic izq.  colocar (se puede seguir colocando la misma pieza)
    ///   Rueda      girar (el primer cimiento de una estructura nueva, y las escaleras)
    ///   Supr       desmontar la pieza a la que se apunta (devuelve la mitad del coste)
    ///   N          volver al menú · Esc salir
    /// Los módulos presurizados pasan al modo de hábitats y los equipos al modo B, ya elegidos.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SpaceBuildMenu : MonoBehaviour
    {
        [SerializeField] private KeyCode menuKey = KeyCode.N;
        [SerializeField, Min(5f)] private float maxDistance = 40f;
        [SerializeField] private Color validColor = new Color(0.25f, 0.8f, 1f, 0.35f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.22f, 0.18f, 0.35f);

        private enum EntryKind { Piece, Module, Equipment }

        private sealed class Entry
        {
            public EntryKind kind;
            public PieceCategory category;
            public string key, name, description;
            public ResourceAmount[] cost;
            public BuildPieceDefinition piece;
            public int moduleIndex;
            public StructureDefinition equipment;
            public Action<Transform> model;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Collider[] _overlaps = new Collider[32];
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private MarsBuildingPlacer _habitats;
        private BuildModeController _equipment;
        private Rigidbody _robotBody;
        private RocketBase _base;
        private TerrainManager _terrain;

        // Menú
        private bool _menuOpen;
        private PieceCategory _tab = PieceCategory.FoundationsAndFloors;
        private Entry _hover;
        private Vector2 _scroll;
        private bool _prevSuspended;

        // Colocación de piezas
        private BuildPieceDefinition _piece;
        private GameObject _ghost;
        private Renderer[] _ghostRenderers = new Renderer[0];
        private Material _ghostOk, _ghostBad;
        private bool _ghostShowsOk;
        private bool _valid;
        private string _reason = "";
        private PieceStructure _target;
        private PieceSlot _slot;
        private bool _newStructure;
        private Vector3 _newOrigin;
        private Quaternion _newRotation;
        private float _yawOffset;
        private int _stairsTurn;
        private bool _wasLocked;
        private string _message = "";
        private float _messageTime = -10f;

        public bool IsMenuOpen
        {
            get { return _menuOpen; }
        }

        public bool IsPlacing
        {
            get { return _piece != null; }
        }

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            _habitats = GetComponent<MarsBuildingPlacer>();
            _equipment = GetComponent<BuildModeController>();
            _robotBody = GetComponent<Rigidbody>();
            _ghostOk = HabitatMaterials.Ghost(validColor);
            _ghostBad = HabitatMaterials.Ghost(invalidColor);
        }

        private void OnDisable()
        {
            if (_menuOpen) CloseMenu(null);
            StopPlacing();
        }

        private void BuildEntries()
        {
            _entries.Clear();
            foreach (BuildPieceDefinition p in BuildPieceCatalog.All)
            {
                BuildPieceDefinition def = p;
                _entries.Add(new Entry
                {
                    kind = EntryKind.Piece, category = def.category, key = "pieza:" + def.id, name = def.name,
                    description = def.description + (def.seals ? "" : Tr.T("  (no hermética)")), cost = def.cost, piece = def,
                    model = t => PieceModels.Build(def, t, null, false)
                });
            }
            if (_habitats != null)
            {
                for (int i = 0; i < _habitats.Modules.Count; i++)
                {
                    MarsModuleData d = _habitats.Modules[i];
                    if (d == null) continue;
                    _entries.Add(new Entry
                    {
                        kind = EntryKind.Module, category = PieceCategory.Modules, key = "modulo:" + d.moduleId, name = d.displayName,
                        description = d.description + "  (" + d.designPressureKPa.ToString("0.#") + Tr.T(" kPa, se construye por capas)"),
                        cost = d.gameCost, moduleIndex = i,
                        model = t => { GameObject g = MarsHabitatBuilder.BuildGhost(d, null); if (g != null) g.transform.SetParent(t, false); }
                    });
                }
            }
            if (_equipment != null)
            {
                foreach (StructureDefinition s in StructureCatalog.All)
                {
                    if (s.hiddenFromBuildMenu) continue;
                    StructureDefinition def = s;
                    _entries.Add(new Entry
                    {
                        kind = EntryKind.Equipment, category = PieceCategory.Equipment, key = "equipo:" + def.id, name = def.name,
                        description = def.description, cost = def.cost, equipment = def,
                        model = t => StructureModels.Build(def, t, null)
                    });
                }
            }
        }

        private void Update()
        {
            if (_base == null) GameServices.TryGet(out _base);
            if (_terrain == null) GameServices.TryGet(out _terrain);
            if (_terrain == null || !_terrain.IsMapReady) return;

#if ENABLE_LEGACY_INPUT_MANAGER
            if (_menuOpen)
            {
                PieceThumbnails.Process(3);
                if (Input.GetKeyDown(menuKey) || Input.GetKeyDown(KeyCode.B) || Input.GetKeyDown(KeyCode.Escape)) CloseMenu(null);
                return;
            }
            if (RobotInput.Suspended) return;

            if (Input.GetKeyDown(menuKey))
            {
                OpenMenu();
                return;
            }
            if (_piece == null) return;

            // Otro modo de construcción activo: este se aparta.
            if ((_habitats != null && _habitats.IsActive) || (_equipment != null && _equipment.IsActive))
            {
                StopPlacing();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape)) { StopPlacing(); return; }

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                if (_piece.slot == PieceSlotType.CellObject) _stairsTurn = (_stairsTurn + (wheel > 0f ? 1 : 3)) % 4;
                else _yawOffset += Mathf.Sign(wheel) * 15f;
            }

            UpdatePlacement();

            bool lockedNow = Cursor.lockState == CursorLockMode.Locked;
            if (Input.GetMouseButtonDown(0) && lockedNow && _wasLocked) TryPlace();
            if (Input.GetKeyDown(KeyCode.Delete)) TryDemolish();
            _wasLocked = lockedNow;
#endif
        }

        // ------------------------------------------------------------------
        // Menú
        // ------------------------------------------------------------------

        /// <summary>Abre el menú de fotos en una pestaña (la tecla B lo abre en "Equipos").</summary>
        public void OpenMenuAt(PieceCategory tab)
        {
            _tab = tab;
            _scroll = Vector2.zero;
            _hover = null;
            OpenMenu();
        }

        private void OpenMenu()
        {
            if (_entries.Count == 0) BuildEntries();
            StopPlacing();
            if (_habitats != null) _habitats.Stop();
            if (_equipment != null) _equipment.Stop();
            _menuOpen = true;
            _prevSuspended = RobotInput.Suspended;
            RobotInput.Suspended = true;           // el robot no se mueve mientras se elige
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void CloseMenu(Entry chosen)
        {
            _menuOpen = false;
            RobotInput.Suspended = _prevSuspended;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _wasLocked = false;                    // el clic que eligió la pieza no la coloca
            if (chosen == null) return;
            switch (chosen.kind)
            {
                case EntryKind.Piece: BeginPiece(chosen.piece); break;
                case EntryKind.Module: if (_habitats != null) _habitats.BeginPlacing(chosen.moduleIndex); break;
                default: if (_equipment != null) _equipment.BeginPlacing(chosen.equipment); break;
            }
        }

        private void OnGUI()
        {
            if (_menuOpen) DrawMenu();
            else if (_piece != null) DrawPlacementHud();
        }

        private void DrawMenu()
        {
            HudTheme.BeginScaled();
            var win = new Rect(150f, 70f, 1300f, 760f);
            HudTheme.Panel(win, MarsRobot.Localization.Localizer.L("CONSTRUCCIÓN", "BUILD"), MarsRobot.Localization.Localizer.L("Elige lo que quieres construir · B, N o Esc para cerrar", "Choose what to build · B, N or Esc to close"));

            // Pestañas.
            float tx = win.x + 18f, ty = win.y + 78f;
            foreach (PieceCategory cat in Enum.GetValues(typeof(PieceCategory)))
            {
                string label = BuildPieceCatalog.CategoryLabel(cat);
                float w = 14f + label.Length * 8.6f;
                if (GUI.Button(new Rect(tx, ty, w, 32f), label, cat == _tab ? HudTheme.TabActive : HudTheme.Tab))
                {
                    _tab = cat;
                    _scroll = Vector2.zero;
                }
                tx += w + 6f;
            }

            // Cuadrícula de piezas con su foto.
            var gridRect = new Rect(win.x + 18f, ty + 46f, win.width - 36f, 470f);
            HudTheme.Fill(gridRect, new Color(0.02f, 0.06f, 0.1f, 0.6f));
            var items = new List<Entry>();
            foreach (Entry e in _entries) if (e.category == _tab) items.Add(e);
            const float slotW = 148f, slotH = 176f, gap = 10f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((gridRect.width - 20f + gap) / (slotW + gap)));
            int rows = Mathf.CeilToInt(items.Count / (float)perRow);
            var view = new Rect(0f, 0f, gridRect.width - 20f, Mathf.Max(gridRect.height - 10f, rows * (slotH + gap)));
            _scroll = GUI.BeginScrollView(new Rect(gridRect.x + 6f, gridRect.y + 6f, gridRect.width - 12f, gridRect.height - 12f), _scroll, view);
            BaseInventory inv = _base != null ? _base.Inventory : null;
            Entry hoveredNow = null;
            for (int i = 0; i < items.Count; i++)
            {
                Entry e = items[i];
                var r = new Rect((i % perRow) * (slotW + gap), (i / perRow) * (slotH + gap), slotW, slotH);
                bool affordable = inv == null || e.cost == null || inv.Has(e.cost);
                bool locked = e.kind == EntryKind.Equipment && _base != null && _base.Research != null && !_base.Research.IsUnlocked(e.equipment);
                bool hover = r.Contains(Event.current.mousePosition);
                if (hover) hoveredNow = e;
                HudTheme.Frame(r, hover ? HudTheme.PanelLight : HudTheme.Slot, hover ? HudTheme.Accent : HudTheme.AccentDim, hover ? 2f : 1f);

                Texture2D icon = StructureArt.ForMenuKey(e.key) ?? PieceThumbnails.Get(e.key, e.model);
                var iconRect = new Rect(r.x + 4f, r.y + 4f, slotW - 8f, slotW - 8f);
                if (icon != null)
                {
                    Color prev = GUI.color;
                    if (!affordable || locked) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                    GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleAndCrop);
                    GUI.color = prev;
                }
                else
                {
                    GUI.Label(iconRect, "…", HudTheme.Subtitle);
                }
                GUI.Label(new Rect(r.x + 6f, r.y + slotW - 8f, slotW - 12f, 40f), e.name, HudTheme.SlotName);
                if (locked) HudTheme.StatePill(new Rect(r.x + 8f, r.y + 8f, 104f, 22f), MarsRobot.Localization.Localizer.L("Investigar", "Research"), HudTheme.Warn);
                else if (!affordable) HudTheme.StatePill(new Rect(r.x + 8f, r.y + 8f, 104f, 22f), MarsRobot.Localization.Localizer.L("Sin material", "Need more"), HudTheme.Bad);

                if (GUI.Button(r, GUIContent.none, GUIStyle.none) && !locked)
                {
                    GUI.EndScrollView();
                    HudTheme.EndScaled();
                    CloseMenu(e);
                    return;
                }
            }
            GUI.EndScrollView();
            if (hoveredNow != null) _hover = hoveredNow;

            // Detalle de la pieza bajo el ratón.
            var info = new Rect(win.x + 18f, gridRect.yMax + 12f, win.width - 36f, win.yMax - gridRect.yMax - 30f);
            HudTheme.Frame(info, new Color(0.03f, 0.08f, 0.14f, 0.9f), HudTheme.AccentDim, 1f);
            if (_hover != null && _hover.category == _tab)
            {
                Texture2D big = StructureArt.ForMenuKey(_hover.key);
                float ix = 14f;
                if (big != null)
                {
                    float side = info.height - 12f;
                    GUI.DrawTexture(new Rect(info.x + 6f, info.y + 6f, side, side), big, ScaleMode.ScaleAndCrop);
                    ix = side + 22f;
                }
                GUI.Label(new Rect(info.x + ix, info.y + 8f, info.width - ix - 14f, 28f), _hover.name, HudTheme.Title);
                GUI.Label(new Rect(info.x + ix, info.y + 42f, info.width - ix - 14f, 48f), _hover.description, HudTheme.Body);
                bool ok = inv == null || _hover.cost == null || inv.Has(_hover.cost);
                string cost = _hover.cost == null || _hover.cost.Length == 0 ? Tr.T("Gratis") : BaseInventory.Describe(_hover.cost);
                Color prev = GUI.contentColor;
                GUI.contentColor = ok ? HudTheme.Good : HudTheme.Bad;
                GUI.Label(new Rect(info.x + ix, info.yMax - 34f, info.width - ix - 14f, 26f), MarsRobot.Localization.Localizer.L("Coste: ", "Cost: ") + cost + (ok ? "" : MarsRobot.Localization.Localizer.L("  (faltan materiales)", "  (need more materials)")), HudTheme.Body);
                GUI.contentColor = prev;
            }
            else
            {
                GUI.Label(new Rect(info.x + 14f, info.y + 14f, info.width - 28f, 60f),
                    Tr.T("Pasa el ratón por una pieza para ver qué es y lo que cuesta. Haz clic para elegirla.\n") +
                    Tr.T("Para hacer una casa: primero el suelo (cimiento), luego paredes, puerta y techo."), HudTheme.Body);
            }
            HudTheme.EndScaled();
        }

        private void DrawPlacementHud()
        {
            HudTheme.BeginScaled();
            var r = new Rect(20f, 900f - 190f, 700f, 170f);
            HudTheme.Frame(r, HudTheme.PanelBg, _valid ? HudTheme.Accent : HudTheme.Bad, 3f);
            Texture2D img = StructureArt.ForPiece(_piece.id);
            float tx = 14f;
            if (img != null)
            {
                GUI.DrawTexture(new Rect(r.x + 8f, r.y + 8f, 154f, 154f), img, ScaleMode.ScaleAndCrop);
                tx = 176f;
            }
            System.Func<string, string, string> L = MarsRobot.Localization.Localizer.L;
            GUI.Label(new Rect(r.x + tx, r.y + 6f, r.width - tx - 14f, 30f), _piece.name, HudTheme.Title);
            GUI.Label(new Rect(r.x + tx, r.y + 40f, r.width - tx - 14f, 24f), L("Cuesta: ", "Costs: ") + BaseInventory.Describe(_piece.cost), HudTheme.Body);
            Color prev = GUI.contentColor;
            GUI.contentColor = _valid ? HudTheme.Good : HudTheme.Bad;
            GUI.Label(new Rect(r.x + tx, r.y + 68f, r.width - tx - 14f, 48f), _valid ? L("¡Aquí cabe! Haz clic para colocarla.", "It fits here! Click to place it.") : _reason, HudTheme.Body);
            GUI.contentColor = prev;
            string help = L("Rueda girar · Supr desmontar · B o N elegir otra · Esc salir", "Wheel rotate · Del remove · B or N pick another · Esc exit");
            GUI.Label(new Rect(r.x + tx, r.y + 126f, r.width - tx - 14f, 36f), Time.time - _messageTime < 3f ? _message : help, HudTheme.Small);
            HudTheme.EndScaled();
        }

        // ------------------------------------------------------------------
        // Colocación de piezas
        // ------------------------------------------------------------------

        private static bool _tipShown;

        private void BeginPiece(BuildPieceDefinition def)
        {
            if (!_tipShown)
            {
                _tipShown = true;
                MascotGuide.Say("marti_build_place", 9f);
            }
            StopPlacing();
            _piece = def;
            _ghost = new GameObject("Holograma: " + def.name);
            PieceModels.Build(def, _ghost.transform, _ghostBad, false);
            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>(true);
            foreach (Renderer rd in _ghostRenderers) rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _ghostShowsOk = false;
            RobotInput.ZoomCaptured = true;
        }

        private void StopPlacing()
        {
            if (_ghost != null)
            {
                foreach (MeshFilter mf in _ghost.GetComponentsInChildren<MeshFilter>(true))
                    if (mf.sharedMesh != null) HabitatGeometry.SafeDestroy(mf.sharedMesh);
                HabitatGeometry.SafeDestroy(_ghost);
            }
            _ghost = null;
            _ghostRenderers = new Renderer[0];
            if (_piece != null) RobotInput.ZoomCaptured = false;
            _piece = null;
        }

        private void UpdatePlacement()
        {
            Camera cam = Camera.main;
            if (cam == null || _ghost == null) return;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));

            Vector3 point;
            Collider hitCollider;
            if (!Aim(ray, out point, out hitCollider))
            {
                _ghost.SetActive(false);
                _valid = false;
                _reason = Tr.T("Apunta al suelo o a tu estructura (máx. ") + maxDistance.ToString("0") + " m).";
                return;
            }
            _ghost.SetActive(true);

            PieceStructure s = hitCollider != null ? hitCollider.GetComponentInParent<PieceStructure>() : null;
            if (s == null) s = PieceStructure.FindNear(point, 3f);
            _target = s;
            _newStructure = s == null;

            if (_newStructure)
            {
                // Pieza suelta: solo un cimiento puede empezar una estructura nueva.
                float yaw = cam.transform.eulerAngles.y + _yawOffset;
                _newRotation = Quaternion.Euler(0f, yaw, 0f);
                _newOrigin = new Vector3(point.x, MaxTerrainOverCell(point, _newRotation), point.z);
                _ghost.transform.SetPositionAndRotation(_newOrigin, _newRotation);
                if (_piece.slot != PieceSlotType.Foundation)
                {
                    SetValid(false, Tr.T("Empieza con un cimiento (o apunta a tu estructura)."));
                    return;
                }
                SetValid(Validate(null, new PieceSlot(PieceSlotType.Foundation, 0, 0, 0), _newOrigin, _newRotation));
                return;
            }

            // Apuntando a una pieza: retrocede un poco para elegir el hueco de este lado.
            Vector3 local = s.transform.InverseTransformPoint(point - ray.direction * 0.15f);
            _slot = ChooseSlot(s, local);
            Vector3 lp;
            Quaternion lr;
            PieceStructure.SlotPose(_slot, out lp, out lr);
            Vector3 wp = s.transform.TransformPoint(lp);
            Quaternion wr = s.transform.rotation * lr;
            _ghost.transform.SetPositionAndRotation(wp, wr);
            SetValid(Validate(s, _slot, wp, wr));
        }

        private PieceSlot ChooseSlot(PieceStructure s, Vector3 local)
        {
            const float c = PieceGrid.Cell, h = PieceGrid.LevelHeight;
            int cx = Mathf.RoundToInt(local.x / c), cz = Mathf.RoundToInt(local.z / c);
            float fx = local.x / c - cx, fz = local.z / c - cz;
            int dir = Mathf.Abs(fx) > Mathf.Abs(fz) ? (fx > 0f ? 1 : 3) : (fz > 0f ? 0 : 2);
            int level = Mathf.Max(0, Mathf.FloorToInt((local.y + 0.4f) / h));

            switch (_piece.slot)
            {
                case PieceSlotType.Foundation:
                {
                    // Sobre un cimiento existente: el siguiente en la dirección a la que apuntas.
                    if (s.HasFloor(cx, cz, 0)) { Vector2Int n = PieceSlot.Neighbor(cx, cz, dir); cx = n.x; cz = n.y; }
                    return new PieceSlot(PieceSlotType.Foundation, cx, cz, 0);
                }
                case PieceSlotType.Deck:
                {
                    int lv = Mathf.Max(1, Mathf.RoundToInt(local.y / h));
                    if (!s.IsFree(new PieceSlot(PieceSlotType.Deck, cx, cz, lv)))
                    {
                        Vector2Int n = PieceSlot.Neighbor(cx, cz, dir);
                        cx = n.x;
                        cz = n.y;
                    }
                    return new PieceSlot(PieceSlotType.Deck, cx, cz, lv);
                }
                case PieceSlotType.Edge:
                    return PieceSlot.CellEdge(cx, cz, level, dir);
                case PieceSlotType.Corner:
                    return new PieceSlot(PieceSlotType.Corner, cx + (fx > 0f ? 1 : 0), cz + (fz > 0f ? 1 : 0), level);
                default:
                    return new PieceSlot(PieceSlotType.CellObject, cx, cz, level, _stairsTurn);
            }
        }

        /// <summary>Comprueba si la pieza se puede colocar. Devuelve null si se puede, o el motivo.</summary>
        private string Validate(PieceStructure s, PieceSlot slot, Vector3 worldPos, Quaternion worldRot)
        {
            // 1) Apoyo y hueco libre.
            if (s != null)
            {
                string support = s.CheckSupport(_piece, slot);
                if (support != null) return support;
            }

            // 2) Cimientos: el terreno no puede sobresalir y los pilotes llegan hasta 4 m.
            if (_piece.slot == PieceSlotType.Foundation)
            {
                float top = worldPos.y, minH = float.MaxValue, maxH = float.MinValue;
                foreach (Vector3 p in CellSamples(worldPos, worldRot))
                {
                    float hgt = _terrain.GetHeightAt(p);
                    minH = Mathf.Min(minH, hgt);
                    maxH = Mathf.Max(maxH, hgt);
                }
                if (maxH > top + 0.15f) return Tr.T("El terreno sobresale por encima del cimiento.");
                if (top - minH > PieceGrid.FoundationThickness + 4f) return Tr.T("Demasiado alto sobre el suelo (pilotes de 4 m como máximo).");
            }

            // 3) Radio de construcción del cohete.
            if (_base != null)
            {
                Vector3 d = worldPos - _base.transform.position;
                d.y = 0f;
                if (d.magnitude > RocketBase.BuildRadius) return Tr.T("Demasiado lejos del cohete (máx. ") + RocketBase.BuildRadius.ToString("0") + " m).";
            }

            // 4) Espacio libre (no cuentan el terreno, el robot ni la propia estructura).
            Bounds b = PieceModels.LocalBounds(_piece);
            Vector3 center = worldPos + worldRot * b.center;
            int n = Physics.OverlapBoxNonAlloc(center, b.extents * 0.9f, _overlaps, worldRot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider col = _overlaps[i];
                if (col == null || col.GetComponent<TerrainChunk>() != null) continue;
                if (_robotBody != null && col.attachedRigidbody == _robotBody) continue;
                if (s != null && col.GetComponentInParent<PieceStructure>() == s) continue;
                return Tr.T("Choca con ") + col.transform.root.name + ".";
            }

            // 5) Materiales.
            BaseInventory inv = _base != null ? _base.Inventory : null;
            if (inv != null && _piece.cost != null && !inv.Has(_piece.cost)) return Tr.T("Faltan materiales: ") + BaseInventory.Describe(_piece.cost);
            return null;
        }

        private void SetValid(string reason)
        {
            SetValid(reason == null, reason ?? "");
        }

        private void SetValid(bool valid, string reason)
        {
            _valid = valid;
            _reason = reason;
            if (valid == _ghostShowsOk) return;
            _ghostShowsOk = valid;
            Material m = valid ? _ghostOk : _ghostBad;
            foreach (Renderer rd in _ghostRenderers) if (rd != null) rd.sharedMaterial = m;
        }

        private void TryPlace()
        {
            if (_piece == null || _ghost == null || !_ghost.activeSelf) return;
            if (!_valid) { Show(_reason); return; }
            BaseInventory inv = _base != null ? _base.Inventory : null;
            if (inv != null && _piece.cost != null && !inv.TrySpend(_piece.cost)) { Show(Tr.T("Faltan materiales.")); return; }

            if (_newStructure)
            {
                PieceStructure created = PieceStructure.Create(_newOrigin, _newRotation);
                created.Place(_piece, new PieceSlot(PieceSlotType.Foundation, 0, 0, 0));
                Show(Tr.T("Nueva estructura: ") + created.name + ".");
            }
            else if (_target != null)
            {
                _target.Place(_piece, _slot);
                Show(_piece.name + Tr.T(" colocado."));
            }
        }

        /// <summary>Supr: desmonta la pieza apuntada y devuelve la mitad de su coste.</summary>
        private void TryDemolish()
        {
            Camera cam = Camera.main;
            if (cam == null) return;
            Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            int n = Physics.RaycastNonAlloc(ray, _hits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            PlacedPiece best = null;
            float bestD = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                PlacedPiece p = _hits[i].collider.GetComponentInParent<PlacedPiece>();
                if (p != null && _hits[i].distance < bestD) { bestD = _hits[i].distance; best = p; }
            }
            if (best == null) { Show(Tr.T("Apunta a una pieza para desmontarla.")); return; }

            BuildPieceDefinition def = best.Definition;
            BaseInventory inv = _base != null ? _base.Inventory : null;
            if (inv != null && def != null && def.cost != null)
                foreach (ResourceAmount c in def.cost) inv.Add(c.type, Mathf.Max(1, c.amount / 2));
            var structure = best.GetComponentInParent<PieceStructure>();
            if (structure != null) structure.Remove(best);
            Show((def != null ? def.name : Tr.T("Pieza")) + Tr.T(" desmontada (se recupera la mitad del material)."));
        }

        // ------------------------------------------------------------------
        // Utilidades
        // ------------------------------------------------------------------

        /// <summary>Punto apuntado: el primer colisionador que no sea el robot ni un holograma; si no, el terreno.</summary>
        private bool Aim(Ray ray, out Vector3 point, out Collider hit)
        {
            int n = Physics.RaycastNonAlloc(ray, _hits, maxDistance, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            hit = null;
            point = Vector3.zero;
            for (int i = 0; i < n; i++)
            {
                Collider c = _hits[i].collider;
                if (_robotBody != null && c.attachedRigidbody == _robotBody) continue;
                if (_hits[i].distance < best) { best = _hits[i].distance; hit = c; point = _hits[i].point; }
            }
            if (hit != null) return true;

            // Lejos del robot el terreno puede no tener colisión: se recorre el rayo.
            Vector3 prev = ray.origin;
            for (float d = 1.5f; d <= maxDistance; d += 1.5f)
            {
                Vector3 p = ray.GetPoint(d);
                if (p.y <= _terrain.GetHeightAt(p))
                {
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
            return false;
        }

        private static IEnumerable<Vector3> CellSamples(Vector3 center, Quaternion rot)
        {
            const float h = PieceGrid.Cell * 0.5f - 0.1f;
            yield return center;
            yield return center + rot * new Vector3(-h, 0f, -h);
            yield return center + rot * new Vector3(h, 0f, -h);
            yield return center + rot * new Vector3(-h, 0f, h);
            yield return center + rot * new Vector3(h, 0f, h);
        }

        /// <summary>Altura máxima del terreno bajo una celda: el cimiento nunca queda enterrado (debajo van pilotes).</summary>
        private float MaxTerrainOverCell(Vector3 center, Quaternion rot)
        {
            float max = float.MinValue;
            foreach (Vector3 p in CellSamples(center, rot)) max = Mathf.Max(max, _terrain.GetHeightAt(p));
            return max;
        }

        private void Show(string text)
        {
            _message = text;
            _messageTime = Time.time;
        }
    }
}
