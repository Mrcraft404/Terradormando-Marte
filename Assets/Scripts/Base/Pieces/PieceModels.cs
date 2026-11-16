using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Medidas de la cuadrícula de construcción (m).</summary>
    public static class PieceGrid
    {
        public const float Cell = 4f;            // lado de una celda
        public const float LevelHeight = 3f;     // de suelo a suelo
        public const float DeckThickness = 0.25f;
        public const float WallThickness = 0.3f;
        public const float FoundationThickness = 0.6f;

        /// <summary>Altura libre de una pared (del suelo a la losa de arriba).</summary>
        public const float WallHeight = LevelHeight - DeckThickness;
    }

    /// <summary>
    /// Modelos (provisionales, hechos por código) de las piezas de construcción. Cada pieza se
    /// construye en su espacio local:
    ///   • celdas (cimiento, losa, techo, escalera): centradas en X/Z, cara superior del suelo en y = 0;
    ///   • bordes (paredes, cristales, puertas): a lo largo de X (−2…+2), desde y = 0 hacia arriba, grosor en Z;
    ///   • esquinas (pilares): un poste desde y = 0.
    /// Con <paramref name="overrideMaterial"/> todo usa ese material (holograma); con
    /// <paramref name="colliders"/> se añaden BoxColliders. Devuelve la hoja de la puerta (si la hay).
    /// </summary>
    public static class PieceModels
    {
        public static Transform Build(BuildPieceDefinition def, Transform parent, Material overrideMaterial, bool colliders)
        {
            var acc = new HabitatMeshAccumulator();
            var cols = new List<Bounds>();
            Transform leaf = null;
            Material M(Material m) { return overrideMaterial != null ? overrideMaterial : m; }

            const float c = PieceGrid.Cell, h = PieceGrid.WallHeight, t = PieceGrid.WallThickness;
            float half = c * 0.5f;

            switch (def.shape)
            {
                case PieceShape.Foundation:
                {
                    float ft = PieceGrid.FoundationThickness;
                    Box(acc, cols, new Vector3(0f, -ft * 0.5f, 0f), new Vector3(c, ft, c), M(HabitatMaterials.SinteredSlab));
                    // Junta perimetral (se ven las losas por separado al unirlas).
                    acc.AddBox(new Vector3(0f, 0.005f, half - 0.04f), new Vector3(c, 0.01f, 0.08f), M(HabitatMaterials.CompactedSoil));
                    acc.AddBox(new Vector3(half - 0.04f, 0.005f, 0f), new Vector3(0.08f, 0.01f, c), M(HabitatMaterials.CompactedSoil));
                    break;
                }
                case PieceShape.FloorSlab:
                {
                    float d = PieceGrid.DeckThickness;
                    Box(acc, cols, new Vector3(0f, -d * 0.5f, 0f), new Vector3(c, d, c), M(HabitatMaterials.SinteredSlab));
                    // Baldosas: líneas en cruz.
                    acc.AddBox(new Vector3(0f, 0.004f, 0f), new Vector3(c, 0.008f, 0.05f), M(HabitatMaterials.Furniture));
                    acc.AddBox(new Vector3(0f, 0.004f, 0f), new Vector3(0.05f, 0.008f, c), M(HabitatMaterials.Furniture));
                    break;
                }
                case PieceShape.Grating:
                {
                    Material metal = M(HabitatMaterials.RackFrame);
                    // Marco y barras (el colisionador es una losa fina para poder pisar).
                    foreach (float s in new[] { -1f, 1f })
                    {
                        acc.AddBox(new Vector3(0f, -0.08f, s * (half - 0.06f)), new Vector3(c, 0.16f, 0.12f), metal);
                        acc.AddBox(new Vector3(s * (half - 0.06f), -0.08f, 0f), new Vector3(0.12f, 0.16f, c), metal);
                    }
                    for (int i = 1; i < 16; i++)
                        acc.AddBox(new Vector3(-half + i * c / 16f, -0.03f, 0f), new Vector3(0.04f, 0.06f, c - 0.2f), metal);
                    if (colliders) cols.Add(new Bounds(new Vector3(0f, -0.08f, 0f), new Vector3(c, 0.16f, c)));
                    break;
                }
                case PieceShape.ArmoredRoof:
                {
                    Box(acc, cols, new Vector3(0f, -0.3f, 0f), new Vector3(c, 0.6f, c), M(HabitatMaterials.RegolithShield));
                    break;
                }
                case PieceShape.GlassRoof:
                {
                    Frame(acc, half, -0.1f, M(HabitatMaterials.Titanium));
                    acc.AddBox(new Vector3(0f, -0.1f, 0f), new Vector3(0.1f, 0.18f, c), M(HabitatMaterials.Titanium));
                    acc.AddBox(new Vector3(0f, -0.12f, 0f), new Vector3(c - 0.1f, 0.05f, c - 0.1f), M(HabitatMaterials.Glass));
                    if (colliders) cols.Add(new Bounds(new Vector3(0f, -0.12f, 0f), new Vector3(c, 0.2f, c)));
                    break;
                }
                case PieceShape.GlassSkylight:
                {
                    Frame(acc, half, -0.1f, M(HabitatMaterials.Titanium));
                    // Anillo de apoyo y pequeña cúpula geodésica de cristal.
                    acc.AddBox(new Vector3(0f, -0.12f, 0f), new Vector3(c - 0.1f, 0.05f, c - 0.1f), M(HabitatMaterials.SinteredSlab));
                    HabitatGeometry.GeodesicMesh geo = HabitatGeometry.GeodesicHemisphere(2);
                    var scale = new Vector3(half * 0.85f, half * 0.6f, half * 0.85f);
                    acc.AddGenerated(HabitatGeometry.GeodesicPanels(geo, scale, 0.93f), Matrix4x4.identity, M(HabitatMaterials.Glass));
                    foreach (Vector2Int e in geo.Edges)
                        acc.AddBeam(Vector3.Scale(geo.Vertices[e.x], scale), Vector3.Scale(geo.Vertices[e.y], scale), 0.05f, M(HabitatMaterials.Titanium));
                    if (colliders) cols.Add(new Bounds(new Vector3(0f, -0.12f, 0f), new Vector3(c, 0.2f, c)));
                    break;
                }
                case PieceShape.Wall:
                {
                    Box(acc, cols, new Vector3(0f, h * 0.5f, 0f), new Vector3(c, h, t), M(HabitatMaterials.RegolithShield));
                    // Hiladas de bloques.
                    for (int i = 1; i < 5; i++)
                        acc.AddBox(new Vector3(0f, i * h / 5f, 0f), new Vector3(c + 0.01f, 0.03f, t + 0.01f), M(HabitatMaterials.SinteredSlab));
                    break;
                }
                case PieceShape.WindowWall:
                {
                    Material wall = M(HabitatMaterials.RegolithShield);
                    const float winW = 1.8f, winBottom = 1f, winTop = 2f;
                    float side = (c - winW) * 0.5f;
                    Box(acc, cols, new Vector3(0f, winBottom * 0.5f, 0f), new Vector3(c, winBottom, t), wall);
                    Box(acc, cols, new Vector3(0f, (winTop + h) * 0.5f, 0f), new Vector3(c, h - winTop, t), wall);
                    Box(acc, cols, new Vector3(-half + side * 0.5f, (winBottom + winTop) * 0.5f, 0f), new Vector3(side, winTop - winBottom, t), wall);
                    Box(acc, cols, new Vector3(half - side * 0.5f, (winBottom + winTop) * 0.5f, 0f), new Vector3(side, winTop - winBottom, t), wall);
                    Box(acc, cols, new Vector3(0f, (winBottom + winTop) * 0.5f, 0f), new Vector3(winW, winTop - winBottom, 0.06f), M(HabitatMaterials.Glass));
                    acc.AddBox(new Vector3(0f, winBottom, 0f), new Vector3(winW + 0.1f, 0.08f, t + 0.1f), M(HabitatMaterials.Titanium));
                    break;
                }
                case PieceShape.HalfWall:
                {
                    Box(acc, cols, new Vector3(0f, 0.5f, 0f), new Vector3(c, 1f, t), M(HabitatMaterials.RegolithShield));
                    acc.AddBox(new Vector3(0f, 1.02f, 0f), new Vector3(c, 0.06f, t + 0.06f), M(HabitatMaterials.Titanium));
                    break;
                }
                case PieceShape.PressurePanel:
                {
                    Box(acc, cols, new Vector3(0f, h * 0.5f, 0f), new Vector3(c - 0.1f, h, 0.14f), M(HabitatMaterials.KevlarOuter));
                    FrameVertical(acc, half, h, M(HabitatMaterials.Titanium));
                    acc.AddBox(new Vector3(0f, h * 0.5f, 0f), new Vector3(0.08f, h, 0.18f), M(HabitatMaterials.Titanium));
                    break;
                }
                case PieceShape.GlassPanel:
                {
                    FrameVertical(acc, half, h, M(HabitatMaterials.Titanium));
                    acc.AddBox(new Vector3(0f, h * 0.5f, 0f), new Vector3(0.08f, h, 0.14f), M(HabitatMaterials.Titanium));   // montante
                    acc.AddBox(new Vector3(0f, h * 0.5f, 0f), new Vector3(c, 0.08f, 0.14f), M(HabitatMaterials.Titanium));   // travesaño
                    Box(acc, cols, new Vector3(0f, h * 0.5f, 0f), new Vector3(c - 0.1f, h, 0.06f), M(HabitatMaterials.Glass));
                    break;
                }
                case PieceShape.Door:
                {
                    const float doorW = 1.4f, doorH = 2.3f;
                    float side = (c - doorW) * 0.5f;
                    Material panel = M(HabitatMaterials.KevlarOuter);
                    Box(acc, cols, new Vector3(-half + side * 0.5f, h * 0.5f, 0f), new Vector3(side, h, t), panel);
                    Box(acc, cols, new Vector3(half - side * 0.5f, h * 0.5f, 0f), new Vector3(side, h, t), panel);
                    Box(acc, cols, new Vector3(0f, (doorH + h) * 0.5f, 0f), new Vector3(doorW, h - doorH, t), panel);
                    // Marco naranja.
                    acc.AddBox(new Vector3(-doorW * 0.5f, doorH * 0.5f, 0f), new Vector3(0.1f, doorH, t + 0.08f), M(HabitatMaterials.SafetyOrange));
                    acc.AddBox(new Vector3(doorW * 0.5f, doorH * 0.5f, 0f), new Vector3(0.1f, doorH, t + 0.08f), M(HabitatMaterials.SafetyOrange));
                    acc.AddBox(new Vector3(0f, doorH, 0f), new Vector3(doorW + 0.2f, 0.1f, t + 0.08f), M(HabitatMaterials.SafetyOrange));
                    leaf = Leaf(parent, "Hoja de la puerta", new Vector3(doorW, doorH, 0.12f), new Vector3(0f, doorH * 0.5f, 0f),
                                M(HabitatMaterials.Machine), M(HabitatMaterials.StatusLight), colliders);
                    break;
                }
                case PieceShape.RoverGate:
                {
                    const float gateW = 3.6f, gateH = 2.6f;
                    Material wall = M(HabitatMaterials.RegolithShield);
                    Box(acc, cols, new Vector3(0f, (gateH + h) * 0.5f, 0f), new Vector3(c, h - gateH, t), wall);
                    Box(acc, cols, new Vector3(-half + 0.1f, gateH * 0.5f, 0f), new Vector3(0.2f, gateH, t), M(HabitatMaterials.SafetyOrange));
                    Box(acc, cols, new Vector3(half - 0.1f, gateH * 0.5f, 0f), new Vector3(0.2f, gateH, t), M(HabitatMaterials.SafetyOrange));
                    leaf = Leaf(parent, "Portón", new Vector3(gateW, gateH, 0.1f), new Vector3(0f, gateH * 0.5f, 0f),
                                M(HabitatMaterials.Titanium), M(HabitatMaterials.SafetyOrange), colliders);
                    break;
                }
                case PieceShape.Pillar:
                {
                    Box(acc, cols, new Vector3(0f, PieceGrid.LevelHeight * 0.5f - 0.1f, 0f), new Vector3(0.35f, PieceGrid.LevelHeight - 0.2f, 0.35f), M(HabitatMaterials.Titanium));
                    acc.AddBox(new Vector3(0f, 0.05f, 0f), new Vector3(0.6f, 0.1f, 0.6f), M(HabitatMaterials.Machine));
                    break;
                }
                case PieceShape.Stairs:
                {
                    // Sube hacia +Z desde el suelo de este piso hasta el del piso de arriba.
                    const int steps = 12;
                    const float width = 1.4f;
                    float run = c - 0.4f, rise = PieceGrid.LevelHeight;
                    for (int i = 0; i < steps; i++)
                    {
                        float y = (i + 1) * rise / steps, z = -run * 0.5f + (i + 0.5f) * run / steps;
                        acc.AddBox(new Vector3(0f, y - 0.06f, z), new Vector3(width, 0.12f, run / steps + 0.02f), M(HabitatMaterials.RackFrame));
                    }
                    foreach (float sx in new[] { -1f, 1f })
                        acc.AddBeam(new Vector3(sx * width * 0.5f, 1f, -run * 0.5f), new Vector3(sx * width * 0.5f, rise + 1f, run * 0.5f), 0.05f, M(HabitatMaterials.SafetyOrange));
                    if (colliders)
                    {
                        // Rampa invisible inclinada para caminar (más fiable que los escalones sueltos).
                        var ramp = new GameObject("Rampa de la escalera");
                        ramp.transform.SetParent(parent, false);
                        float slope = Mathf.Atan2(rise, run) * Mathf.Rad2Deg;
                        ramp.transform.localPosition = new Vector3(0f, rise * 0.5f - 0.05f, 0f);
                        ramp.transform.localRotation = Quaternion.Euler(-slope, 0f, 0f);
                        var rc = ramp.AddComponent<BoxCollider>();
                        rc.size = new Vector3(width, 0.1f, Mathf.Sqrt(rise * rise + run * run));
                    }
                    break;
                }
            }

            acc.Build(parent, def.name);
            if (colliders)
            {
                foreach (Bounds b in cols)
                {
                    var bc = parent.gameObject.AddComponent<BoxCollider>();
                    bc.center = b.center;
                    bc.size = b.size;
                }
            }
            return leaf;
        }

        /// <summary>Aproximación de la caja que ocupa la pieza (local), para comprobar choques.</summary>
        public static Bounds LocalBounds(BuildPieceDefinition def)
        {
            const float c = PieceGrid.Cell, h = PieceGrid.WallHeight;
            switch (def.slot)
            {
                case PieceSlotType.Foundation: return new Bounds(new Vector3(0f, -0.3f, 0f), new Vector3(c, 0.6f, c));
                case PieceSlotType.Deck: return new Bounds(new Vector3(0f, -0.15f, 0f), new Vector3(c, 0.3f, c));
                case PieceSlotType.Edge: return new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(c, h, PieceGrid.WallThickness));
                case PieceSlotType.Corner: return new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(0.4f, h, 0.4f));
                default: return new Bounds(new Vector3(0f, h * 0.5f, 0f), new Vector3(1.6f, h, c - 0.4f));
            }
        }

        private static void Box(HabitatMeshAccumulator acc, List<Bounds> cols, Vector3 center, Vector3 size, Material m)
        {
            acc.AddBox(center, size, m);
            cols.Add(new Bounds(center, size));
        }

        /// <summary>Marco horizontal (techos de cristal) a la altura y.</summary>
        private static void Frame(HabitatMeshAccumulator acc, float half, float y, Material m)
        {
            foreach (float s in new[] { -1f, 1f })
            {
                acc.AddBox(new Vector3(0f, y, s * (half - 0.05f)), new Vector3(half * 2f, 0.18f, 0.1f), m);
                acc.AddBox(new Vector3(s * (half - 0.05f), y, 0f), new Vector3(0.1f, 0.18f, half * 2f), m);
            }
        }

        /// <summary>Marco vertical de un panel de pared.</summary>
        private static void FrameVertical(HabitatMeshAccumulator acc, float half, float h, Material m)
        {
            acc.AddBox(new Vector3(-half + 0.05f, h * 0.5f, 0f), new Vector3(0.1f, h, 0.16f), m);
            acc.AddBox(new Vector3(half - 0.05f, h * 0.5f, 0f), new Vector3(0.1f, h, 0.16f), m);
            acc.AddBox(new Vector3(0f, 0.05f, 0f), new Vector3(half * 2f, 0.1f, 0.16f), m);
            acc.AddBox(new Vector3(0f, h - 0.05f, 0f), new Vector3(half * 2f, 0.1f, 0.16f), m);
        }

        /// <summary>Hoja móvil de una puerta (hijo propio, con su colisionador, para poder moverla).</summary>
        private static Transform Leaf(Transform parent, string name, Vector3 size, Vector3 center, Material body, Material stripe, bool collider)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            var acc = new HabitatMeshAccumulator();
            acc.AddBox(Vector3.zero, size, body);
            acc.AddBox(new Vector3(0f, size.y * 0.25f, 0f), new Vector3(size.x * 0.9f, 0.06f, size.z + 0.02f), stripe);
            acc.AddBox(new Vector3(0f, -size.y * 0.1f, 0f), new Vector3(size.x * 0.9f, 0.06f, size.z + 0.02f), stripe);
            acc.Build(go.transform, name);
            if (collider) go.AddComponent<BoxCollider>().size = size;
            return go.transform;
        }
    }
}
