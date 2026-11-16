using System;
using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Visuals;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Hueco de la cuadrícula de una estructura. Bordes en forma canónica: side 0 = borde norte
    /// (+Z) de la celda (x, z); side 1 = borde este (+X). En objetos de celda, side = giro (0–3 × 90°).
    /// </summary>
    [Serializable]
    public struct PieceSlot : IEquatable<PieceSlot>
    {
        public PieceSlotType type;
        public int x, z, level;
        public byte side;

        public PieceSlot(PieceSlotType type, int x, int z, int level, int side = 0)
        {
            this.type = type;
            this.x = x;
            this.z = z;
            this.level = level;
            this.side = (byte)side;
        }

        /// <summary>Hueco que ocupa la pieza (los objetos de celda ocupan la celda sea cual sea su giro).</summary>
        public PieceSlot Key
        {
            get { return type == PieceSlotType.CellObject ? new PieceSlot(type, x, z, level, 0) : this; }
        }

        public bool Equals(PieceSlot o)
        {
            return type == o.type && x == o.x && z == o.z && level == o.level && side == o.side;
        }

        public override bool Equals(object obj)
        {
            return obj is PieceSlot && Equals((PieceSlot)obj);
        }

        public override int GetHashCode()
        {
            unchecked { return ((((int)type * 397 ^ x) * 397 ^ z) * 397 ^ level) * 397 ^ side; }
        }

        /// <summary>Borde (canónico) de la celda (x, z) en la dirección dir: 0 N, 1 E, 2 S, 3 O.</summary>
        public static PieceSlot CellEdge(int x, int z, int level, int dir)
        {
            switch (dir)
            {
                case 0: return new PieceSlot(PieceSlotType.Edge, x, z, level, 0);
                case 1: return new PieceSlot(PieceSlotType.Edge, x, z, level, 1);
                case 2: return new PieceSlot(PieceSlotType.Edge, x, z - 1, level, 0);
                default: return new PieceSlot(PieceSlotType.Edge, x - 1, z, level, 1);
            }
        }

        public static Vector2Int Neighbor(int x, int z, int dir)
        {
            switch (dir)
            {
                case 0: return new Vector2Int(x, z + 1);
                case 1: return new Vector2Int(x + 1, z);
                case 2: return new Vector2Int(x, z - 1);
                default: return new Vector2Int(x - 1, z);
            }
        }
    }

    /// <summary>
    /// Construcción por piezas en una cuadrícula de 4 m (cimientos, suelos, paredes, cristales,
    /// puertas, techos, pilares, escaleras), con pisos de 3 m. Cada estructura tiene su propio
    /// origen y giro (el del primer cimiento) y cuelga de _Structures.
    ///
    /// Reglas de apoyo:
    ///   • Cimiento: en el suelo, pegado a otro cimiento (el primero, libre). Pilotes automáticos
    ///     hasta el terreno si el suelo está más bajo.
    ///   • Pared/cristal/puerta: necesita suelo (cimiento o losa) a un lado en su piso.
    ///   • Losa/techo: necesita una pared o un pilar debajo, o una losa al lado.
    ///   • Pilar: suelo en alguna de las 4 celdas de su esquina. Escalera: suelo en su celda.
    ///
    /// Salas herméticas: en cada piso, las celdas con suelo se agrupan (por los bordes sin pared
    /// cerrada). Una sala está sellada si todas sus celdas tienen techo hermético y todos sus bordes
    /// exteriores tienen pared, cristal o puerta CERRADA. Las salas selladas se presurizan
    /// (70 kPa, 30 % O₂: respirable) y las abiertas pierden el aire.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PieceStructure : MonoBehaviour
    {
        public const float RoomPressureKPa = 70f;
        public const float RoomOxygenFraction = 0.3f;

        private static readonly List<PieceStructure> Registry = new List<PieceStructure>();

        private readonly Dictionary<PieceSlot, PlacedPiece> _slots = new Dictionary<PieceSlot, PlacedPiece>();
        private readonly Dictionary<Vector3Int, float> _cellPressure = new Dictionary<Vector3Int, float>();
        private readonly Dictionary<Vector3Int, bool> _cellSealed = new Dictionary<Vector3Int, bool>();
        private readonly Dictionary<Vector3Int, PressurizedVolume> _cellAir = new Dictionary<Vector3Int, PressurizedVolume>();
        private bool _indexDirty = true;

        public static IReadOnlyList<PieceStructure> All
        {
            get { return Registry; }
        }

        public int PieceCount
        {
            get { EnsureIndex(); return _slots.Count; }
        }

        /// <summary>Número de salas selladas (para la interfaz).</summary>
        public int SealedCellCount { get; private set; }

        // ------------------------------------------------------------------
        // Creación y registro
        // ------------------------------------------------------------------

        /// <summary>Crea una estructura nueva con origen en la cara superior de su primer cimiento.</summary>
        public static PieceStructure Create(Vector3 origin, Quaternion rotation)
        {
            Transform parent = SceneRoots.Get(SceneRoot.Structures);
            int n = 1;
            for (int i = 0; i < parent.childCount; i++) if (parent.GetChild(i).GetComponent<PieceStructure>() != null) n++;
            var go = new GameObject("Estructura " + n.ToString("00"));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(origin, rotation);
            return go.AddComponent<PieceStructure>();
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
            _indexDirty = true;
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }

        private void OnTransformChildrenChanged()
        {
            _indexDirty = true; // se ha añadido o borrado una pieza (también desde el editor)
        }

        private void EnsureIndex()
        {
            if (!_indexDirty) return;
            _indexDirty = false;
            _slots.Clear();
            foreach (PlacedPiece p in GetComponentsInChildren<PlacedPiece>(true))
            {
                if (p.transform.parent == transform) _slots[p.slot.Key] = p;
            }
            RecomputeRooms();
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        public PlacedPiece Get(PieceSlot slot)
        {
            EnsureIndex();
            PlacedPiece p;
            return _slots.TryGetValue(slot.Key, out p) && p != null ? p : null;
        }

        public bool IsFree(PieceSlot slot)
        {
            return Get(slot) == null;
        }

        /// <summary>¿Hay suelo (cimiento o losa de cualquier tipo) en la celda y piso dados?</summary>
        public bool HasFloor(int x, int z, int level)
        {
            return level == 0
                ? Get(new PieceSlot(PieceSlotType.Foundation, x, z, 0)) != null
                : Get(new PieceSlot(PieceSlotType.Deck, x, z, level)) != null;
        }

        public bool HasAnyFoundation
        {
            get
            {
                EnsureIndex();
                foreach (PieceSlot s in _slots.Keys) if (s.type == PieceSlotType.Foundation) return true;
                return false;
            }
        }

        /// <summary>Posición y giro LOCALES de una pieza en su hueco.</summary>
        public static void SlotPose(PieceSlot s, out Vector3 position, out Quaternion rotation)
        {
            const float c = PieceGrid.Cell, h = PieceGrid.LevelHeight;
            float y = s.level * h;
            switch (s.type)
            {
                case PieceSlotType.Edge:
                    if (s.side == 0) { position = new Vector3(s.x * c, y, s.z * c + c * 0.5f); rotation = Quaternion.identity; }
                    else { position = new Vector3(s.x * c + c * 0.5f, y, s.z * c); rotation = Quaternion.Euler(0f, 90f, 0f); }
                    return;
                case PieceSlotType.Corner:
                    position = new Vector3((s.x - 0.5f) * c, y, (s.z - 0.5f) * c);
                    rotation = Quaternion.identity;
                    return;
                case PieceSlotType.CellObject:
                    position = new Vector3(s.x * c, y, s.z * c);
                    rotation = Quaternion.Euler(0f, s.side * 90f, 0f);
                    return;
                default:
                    position = new Vector3(s.x * c, y, s.z * c);
                    rotation = Quaternion.identity;
                    return;
            }
        }

        /// <summary>Comprueba las reglas de apoyo. Devuelve null si se puede, o el motivo.</summary>
        public string CheckSupport(BuildPieceDefinition def, PieceSlot s)
        {
            if (!IsFree(s)) return Tr.T("Ese hueco ya está ocupado.");
            switch (s.type)
            {
                case PieceSlotType.Foundation:
                    if (!HasAnyFoundation) return null;
                    for (int d = 0; d < 4; d++)
                    {
                        Vector2Int n = PieceSlot.Neighbor(s.x, s.z, d);
                        if (HasFloor(n.x, n.y, 0)) return null;
                    }
                    return Tr.T("El cimiento debe tocar otro cimiento.");
                case PieceSlotType.Deck:
                    if (s.level < 1) return Tr.T("Las losas van en pisos superiores (usa cimientos en el suelo).");
                    for (int d = 0; d < 4; d++)
                    {
                        PlacedPiece below = Get(PieceSlot.CellEdge(s.x, s.z, s.level - 1, d));
                        if (below != null && below.Definition != null && below.Definition.shape != PieceShape.HalfWall) return null;
                        Vector2Int n = PieceSlot.Neighbor(s.x, s.z, d);
                        if (HasFloor(n.x, n.y, s.level)) return null;
                    }
                    for (int cx = 0; cx <= 1; cx++)
                        for (int cz = 0; cz <= 1; cz++)
                            if (Get(new PieceSlot(PieceSlotType.Corner, s.x + cx, s.z + cz, s.level - 1)) != null) return null;
                    return Tr.T("Necesita una pared o un pilar debajo, o una losa al lado.");
                case PieceSlotType.Edge:
                {
                    Vector2Int other = s.side == 0 ? new Vector2Int(s.x, s.z + 1) : new Vector2Int(s.x + 1, s.z);
                    if (HasFloor(s.x, s.z, s.level) || HasFloor(other.x, other.y, s.level)) return null;
                    return Tr.T("Las paredes se apoyan en un cimiento o una losa.");
                }
                case PieceSlotType.Corner:
                    for (int cx = -1; cx <= 0; cx++)
                        for (int cz = -1; cz <= 0; cz++)
                            if (HasFloor(s.x + cx, s.z + cz, s.level)) return null;
                    return Tr.T("El pilar se apoya en un cimiento o una losa.");
                default:
                    return HasFloor(s.x, s.z, s.level) ? null : Tr.T("La escalera necesita suelo.");
            }
        }

        // ------------------------------------------------------------------
        // Colocar y quitar
        // ------------------------------------------------------------------

        public PlacedPiece Place(BuildPieceDefinition def, PieceSlot slot)
        {
            EnsureIndex();
            var go = new GameObject(def.name);
            go.transform.SetParent(transform, false);
            Vector3 p;
            Quaternion r;
            SlotPose(slot, out p, out r);
            go.transform.localPosition = p;
            go.transform.localRotation = r;

            Transform leaf = PieceModels.Build(def, go.transform, null, true);
            var placed = go.AddComponent<PlacedPiece>();
            placed.pieceId = def.id;
            placed.slot = slot;
            if (def.IsDoor && leaf != null)
            {
                var door = go.AddComponent<PieceDoor>();
                door.Setup(leaf, def.shape == PieceShape.RoverGate);
            }
            if (def.slot == PieceSlotType.Foundation) AddPiles(go.transform);

            _slots[slot.Key] = placed;
            RecomputeRooms();
            return placed;
        }

        public void Remove(PlacedPiece piece)
        {
            if (piece == null) return;
            EnsureIndex();
            _slots.Remove(piece.slot.Key);
            piece.transform.SetParent(null, false);
            HabitatGeometry.SafeDestroy(piece.gameObject);
            RecomputeRooms();
            if (_slots.Count == 0) HabitatGeometry.SafeDestroy(gameObject);
        }

        /// <summary>Pilotes de titanio bajo las esquinas de un cimiento hasta el terreno (si está más bajo).</summary>
        private static void AddPiles(Transform foundation)
        {
            TerrainManager terrain;
            if (!GameServices.TryGet(out terrain) || !terrain.IsMapReady) return;
            const float half = PieceGrid.Cell * 0.5f - 0.3f;
            float bottom = -PieceGrid.FoundationThickness;
            var acc = new HabitatMeshAccumulator();
            var cols = new List<Bounds>();
            foreach (var corner in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1), Vector2.zero })
            {
                var local = new Vector3(corner.x * half, bottom, corner.y * half);
                Vector3 world = foundation.TransformPoint(local);
                float ground = terrain.GetHeightAt(world);
                float length = world.y - ground + 0.3f; // un poco enterrado
                if (length < 0.35f) continue;
                Vector3 groundLocal = foundation.InverseTransformPoint(new Vector3(world.x, ground - 0.3f, world.z));
                acc.AddBeam(groundLocal, local, 0.32f, HabitatMaterials.Titanium);
                cols.Add(new Bounds((groundLocal + local) * 0.5f, new Vector3(0.32f, length, 0.32f)));
            }
            acc.Build(foundation, "Pilotes");
            foreach (Bounds b in cols)
            {
                var bc = foundation.gameObject.AddComponent<BoxCollider>();
                bc.center = b.center;
                bc.size = b.size;
            }
        }

        // ------------------------------------------------------------------
        // Salas herméticas y aire
        // ------------------------------------------------------------------

        /// <summary>Recalcula qué celdas forman salas selladas. Lo llaman las piezas y las puertas.</summary>
        public void RecomputeRooms()
        {
            if (_indexDirty) { EnsureIndex(); return; } // EnsureIndex ya vuelve a llamar aquí
            var floors = new HashSet<Vector3Int>();
            foreach (PieceSlot s in _slots.Keys)
            {
                if (s.type == PieceSlotType.Foundation) floors.Add(new Vector3Int(s.x, s.z, 0));
                else if (s.type == PieceSlotType.Deck) floors.Add(new Vector3Int(s.x, s.z, s.level));
            }

            _cellSealed.Clear();
            var visited = new HashSet<Vector3Int>();
            var region = new List<Vector3Int>();
            var stack = new Stack<Vector3Int>();
            foreach (Vector3Int start in floors)
            {
                if (visited.Contains(start)) continue;
                region.Clear();
                bool leak = false;
                stack.Push(start);
                visited.Add(start);
                while (stack.Count > 0)
                {
                    Vector3Int c = stack.Pop();
                    region.Add(c);
                    // Techo hermético encima.
                    PlacedPiece roof = Get(new PieceSlot(PieceSlotType.Deck, c.x, c.y, c.z + 1));
                    if (roof == null || !roof.SealsNow) leak = true;
                    for (int d = 0; d < 4; d++)
                    {
                        PlacedPiece edge = Get(PieceSlot.CellEdge(c.x, c.y, c.z, d));
                        if (edge != null && edge.SealsNow) continue; // pared o puerta cerrada: el aire no pasa
                        Vector2Int n = PieceSlot.Neighbor(c.x, c.y, d);
                        var nc = new Vector3Int(n.x, n.y, c.z);
                        if (!floors.Contains(nc)) { leak = true; continue; } // da al exterior
                        if (visited.Add(nc)) stack.Push(nc);
                    }
                }
                foreach (Vector3Int c in region) _cellSealed[c] = !leak;
            }

            // Volúmenes de aire: uno por celda con suelo.
            SealedCellCount = 0;
            var stale = new List<Vector3Int>(_cellAir.Keys);
            foreach (var pair in _cellSealed)
            {
                if (pair.Value) SealedCellCount++;
                stale.Remove(pair.Key);
                if (!_cellAir.ContainsKey(pair.Key)) _cellAir[pair.Key] = CreateCellAir(pair.Key);
                if (!_cellPressure.ContainsKey(pair.Key)) _cellPressure[pair.Key] = 0f;
            }
            foreach (Vector3Int gone in stale)
            {
                if (_cellAir[gone] != null) HabitatGeometry.SafeDestroy(_cellAir[gone].gameObject);
                _cellAir.Remove(gone);
                _cellPressure.Remove(gone);
            }
        }

        private PressurizedVolume CreateCellAir(Vector3Int cell)
        {
            var go = new GameObject("Aire " + cell.x + "," + cell.y + " (piso " + (cell.z + 1) + ")");
            go.hideFlags = HideFlags.DontSave; // se recalcula al cargar: no se guarda con la escena
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(cell.x * PieceGrid.Cell, cell.z * PieceGrid.LevelHeight, cell.y * PieceGrid.Cell);
            Vector3Int key = cell;
            return PressurizedVolume.Attach(go, Tr.T("Sala presurizada (") + name + ")",
                new Vector3(0f, PieceGrid.WallHeight * 0.5f, 0f), new Vector3(PieceGrid.Cell, PieceGrid.WallHeight, PieceGrid.Cell),
                () => { float p; return _cellPressure.TryGetValue(key, out p) ? p : 0f; },
                () => RoomOxygenFraction);
        }

        private void Update()
        {
            EnsureIndex();
            if (!Application.isPlaying) return;
            float dt = Time.deltaTime;
            var keys = new List<Vector3Int>(_cellPressure.Keys);
            foreach (Vector3Int k in keys)
            {
                bool sealedCell;
                _cellSealed.TryGetValue(k, out sealedCell);
                float p = _cellPressure[k];
                // Sellada: el soporte vital de la base la presuriza; abierta: el aire escapa en segundos.
                _cellPressure[k] = sealedCell ? Mathf.MoveTowards(p, RoomPressureKPa, 10f * dt) : p * Mathf.Exp(-dt / 1.5f);
            }
        }

        /// <summary>Estructura cuya cuadrícula queda más cerca del punto (dentro de <paramref name="maxDistance"/> m), o null.</summary>
        public static PieceStructure FindNear(Vector3 world, float maxDistance)
        {
            PieceStructure best = null;
            float bestD = maxDistance;
            foreach (PieceStructure s in Registry)
            {
                if (s == null) continue;
                s.EnsureIndex();
                Vector3 local = s.transform.InverseTransformPoint(world);
                foreach (PieceSlot slot in s._slots.Keys)
                {
                    float dx = local.x - slot.x * PieceGrid.Cell, dz = local.z - slot.z * PieceGrid.Cell;
                    float d = Mathf.Sqrt(dx * dx + dz * dz) - PieceGrid.Cell * 0.7f;
                    if (d < bestD) { bestD = d; best = s; }
                }
            }
            return best;
        }
    }
}
