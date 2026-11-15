using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarsRobot.Base
{
    /// <summary>
    /// Acumula piezas (primitivas transformadas o mallas generadas) agrupadas por material y las
    /// funde en UNA malla por material. Así una capa con cientos de costillas o estantes cuesta
    /// pocas llamadas de dibujo.
    /// </summary>
    public sealed class HabitatMeshAccumulator
    {
        private readonly Dictionary<Material, List<CombineInstance>> _parts = new Dictionary<Material, List<CombineInstance>>();
        private readonly List<Mesh> _temporary = new List<Mesh>();

        public void Add(Mesh mesh, Matrix4x4 matrix, Material material)
        {
            if (mesh == null || material == null) return;
            List<CombineInstance> list;
            if (!_parts.TryGetValue(material, out list))
            {
                list = new List<CombineInstance>();
                _parts.Add(material, list);
            }
            list.Add(new CombineInstance { mesh = mesh, transform = matrix });
        }

        /// <summary>Añade una malla generada que solo sirve para fundirse (se destruye al terminar).</summary>
        public void AddGenerated(Mesh mesh, Matrix4x4 matrix, Material material)
        {
            _temporary.Add(mesh);
            Add(mesh, matrix, material);
        }

        public void AddPrimitive(PrimitiveType type, Vector3 position, Quaternion rotation, Vector3 scale, Material material)
        {
            Add(HabitatGeometry.Primitive(type), Matrix4x4.TRS(position, rotation, scale), material);
        }

        public void AddBox(Vector3 center, Vector3 size, Material material)
        {
            AddPrimitive(PrimitiveType.Cube, center, Quaternion.identity, size, material);
        }

        /// <summary>Viga cilíndrica entre dos puntos.</summary>
        public void AddBeam(Vector3 a, Vector3 b, float thickness, Material material)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return;
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, d / len);
            // El cilindro de Unity mide 2 de alto y 1 de diámetro.
            AddPrimitive(PrimitiveType.Cylinder, (a + b) * 0.5f, rot, new Vector3(thickness, len * 0.5f, thickness), material);
        }

        /// <summary>Crea un hijo por material con la malla fundida. Devuelve los objetos creados.</summary>
        public List<GameObject> Build(Transform parent, string baseName)
        {
            var created = new List<GameObject>();
            foreach (var pair in _parts)
            {
                var mesh = new Mesh { name = baseName + "_" + pair.Key.name };
                mesh.indexFormat = IndexFormat.UInt32; // capas grandes superan los 65 535 vértices
                mesh.CombineMeshes(pair.Value.ToArray(), true, true);
                mesh.RecalculateBounds();

                var go = new GameObject(baseName + " (" + pair.Key.name + ")");
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = pair.Key;
                created.Add(go);
            }
            foreach (Mesh m in _temporary) HabitatGeometry.SafeDestroy(m);
            _parts.Clear();
            _temporary.Clear();
            return created;
        }
    }

    /// <summary>Generadores de geometría para las capas de los módulos.</summary>
    public static class HabitatGeometry
    {
        private static readonly Dictionary<PrimitiveType, Mesh> PrimitiveCache = new Dictionary<PrimitiveType, Mesh>();

        /// <summary>Malla compartida de una primitiva de Unity (sin depender del nombre interno del recurso).</summary>
        public static Mesh Primitive(PrimitiveType type)
        {
            Mesh mesh;
            if (PrimitiveCache.TryGetValue(type, out mesh) && mesh != null) return mesh;
            GameObject tmp = GameObject.CreatePrimitive(type);
            mesh = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.DestroyImmediate(tmp);
            PrimitiveCache[type] = mesh;
            return mesh;
        }

        /// <summary>Destroy en juego, DestroyImmediate en el editor.</summary>
        public static void SafeDestroy(Object obj)
        {
            if (obj == null) return;
            if (Application.isPlaying) Object.Destroy(obj);
            else Object.DestroyImmediate(obj);
        }

        // ------------------------------------------------------------------
        // Superficies de revolución
        // ------------------------------------------------------------------

        /// <summary>
        /// Gira un perfil 2D (x = distancia al eje, y = posición a lo largo del eje) alrededor del
        /// eje Y (cúpulas) o del eje Z (tubos y bóvedas), entre dos ángulos.
        /// Con <paramref name="inward"/> las caras miran hacia dentro (superficie interior).
        /// <paramref name="noise"/> desplaza los vértices (aspecto de regolito apilado).
        /// </summary>
        public static Mesh Revolve(IList<Vector2> profile, bool aroundY, float angleStart, float angleEnd,
                                   int segments, bool inward, float noise, int seed)
        {
            int p = profile.Count;
            int cols = segments + 1;
            var verts = new Vector3[p * cols];
            var uvs = new Vector2[p * cols];
            float noiseOffset = (seed % 1000) * 1.37f;

            for (int i = 0; i < p; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    float t = (float)j / segments;
                    float a = Mathf.Lerp(angleStart, angleEnd, t);
                    float r = profile[i].x;
                    if (noise > 0f && r > 0.01f)
                    {
                        // Ruido periódico en el ángulo (sin costura al cerrar la vuelta).
                        float n = Mathf.PerlinNoise(noiseOffset + Mathf.Cos(a) * 3f + i * 0.21f, noiseOffset + Mathf.Sin(a) * 3f);
                        r += (n - 0.5f) * 2f * noise;
                    }
                    float c = Mathf.Cos(a) * r, s = Mathf.Sin(a) * r;
                    verts[i * cols + j] = aroundY ? new Vector3(c, profile[i].y, s) : new Vector3(c, s, profile[i].y);
                    uvs[i * cols + j] = new Vector2(t * (angleEnd - angleStart) * Mathf.Max(1f, r) * 0.25f, profile[i].y * 0.25f);
                }
            }

            var tris = new int[(p - 1) * segments * 6];
            int k = 0;
            // Con el giro alrededor de Y el orden (a, c, b) apunta hacia fuera; alrededor de Z, al revés.
            bool swap = aroundY ^ inward;
            for (int i = 0; i < p - 1; i++)
            {
                for (int j = 0; j < segments; j++)
                {
                    int a = i * cols + j, b = a + 1, c = a + cols, d = c + 1;
                    if (swap)
                    {
                        tris[k++] = a; tris[k++] = c; tris[k++] = b;
                        tris[k++] = b; tris[k++] = c; tris[k++] = d;
                    }
                    else
                    {
                        tris[k++] = a; tris[k++] = b; tris[k++] = c;
                        tris[k++] = b; tris[k++] = d; tris[k++] = c;
                    }
                }
            }

            var mesh = new Mesh { name = "Revolucion" };
            mesh.indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Perfil de un cuarto de círculo (cúpula): de la base (r, 0) a la cima (0, r).</summary>
        public static List<Vector2> QuarterCircle(float radius, int steps, float verticalScale = 1f)
        {
            var pts = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float a = Mathf.PI * 0.5f * i / steps;
                pts.Add(new Vector2(Mathf.Max(0.001f, Mathf.Cos(a) * radius), Mathf.Sin(a) * radius * verticalScale));
            }
            return pts;
        }

        /// <summary>Perfil de cápsula a lo largo del eje: casquete, tramo recto de <paramref name="straight"/> m y casquete.</summary>
        public static List<Vector2> CapsuleProfile(float radius, float straight, int capSteps)
        {
            var pts = new List<Vector2>();
            float h = straight * 0.5f;
            for (int i = 0; i <= capSteps; i++)
            {
                float a = -Mathf.PI * 0.5f + Mathf.PI * 0.5f * i / capSteps;   // −90° → 0°
                pts.Add(new Vector2(Mathf.Max(0.001f, Mathf.Cos(a) * radius), -h + Mathf.Sin(a) * radius));
            }
            for (int i = 0; i <= capSteps; i++)
            {
                float a = Mathf.PI * 0.5f * i / capSteps;                      // 0° → 90°
                pts.Add(new Vector2(Mathf.Max(0.001f, Mathf.Cos(a) * radius), h + Mathf.Sin(a) * radius));
            }
            return pts;
        }

        // ------------------------------------------------------------------
        // Cúpula geodésica
        // ------------------------------------------------------------------

        /// <summary>Semiesfera geodésica unitaria: vértices, triángulos (hacia fuera) y aristas (barras).</summary>
        public sealed class GeodesicMesh
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();
            public readonly List<Vector2Int> Edges = new List<Vector2Int>();
        }

        /// <summary>
        /// Icosaedro con un vértice arriba (eje de simetría 5), cada cara dividida en
        /// <paramref name="frequency"/>² triángulos y proyectada a la esfera. Con frecuencia par el
        /// corte por el ecuador cae justo sobre aristas, así la base queda plana.
        /// </summary>
        public static GeodesicMesh GeodesicHemisphere(int frequency)
        {
            int f = Mathf.Max(1, frequency);
            float y = 1f / Mathf.Sqrt(5f), r = 2f / Mathf.Sqrt(5f);
            var ico = new List<Vector3> { Vector3.up };
            for (int k = 0; k < 5; k++)
            {
                float a = k * 72f * Mathf.Deg2Rad;
                ico.Add(new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r));          // 1..5 anillo superior
            }
            for (int k = 0; k < 5; k++)
            {
                float a = (k * 72f + 36f) * Mathf.Deg2Rad;
                ico.Add(new Vector3(Mathf.Cos(a) * r, -y, Mathf.Sin(a) * r));         // 6..10 anillo inferior
            }
            ico.Add(Vector3.down);                                                      // 11
            var faces = new List<int[]>();
            for (int k = 0; k < 5; k++)
            {
                int u0 = 1 + k, u1 = 1 + (k + 1) % 5, l0 = 6 + k, l1 = 6 + (k + 1) % 5;
                faces.Add(new[] { 0, u0, u1 });
                faces.Add(new[] { u0, l0, u1 });
                faces.Add(new[] { u1, l0, l1 });
                faces.Add(new[] { 11, l0, l1 });
            }

            var result = new GeodesicMesh();
            var index = new Dictionary<Vector3Int, int>();
            var edgeSet = new HashSet<long>();

            foreach (int[] face in faces)
            {
                Vector3 a = ico[face[0]], b = ico[face[1]], c = ico[face[2]];
                // Rejilla de puntos de la cara subdividida.
                var grid = new int[f + 1, f + 1];
                for (int i = 0; i <= f; i++)
                {
                    for (int j = 0; j <= f - i; j++)
                    {
                        Vector3 p = (a + (b - a) * i / f + (c - a) * j / f).normalized;
                        if (Mathf.Abs(p.y) < 1e-4f) p.y = 0f;
                        var key = new Vector3Int(Mathf.RoundToInt(p.x * 10000f), Mathf.RoundToInt(p.y * 10000f), Mathf.RoundToInt(p.z * 10000f));
                        int id;
                        if (!index.TryGetValue(key, out id))
                        {
                            id = result.Vertices.Count;
                            result.Vertices.Add(p);
                            index.Add(key, id);
                        }
                        grid[i, j] = id;
                    }
                }
                for (int i = 0; i < f; i++)
                {
                    for (int j = 0; j < f - i; j++)
                    {
                        AddGeodesicTriangle(result, edgeSet, grid[i, j], grid[i + 1, j], grid[i, j + 1]);
                        if (j < f - i - 1) AddGeodesicTriangle(result, edgeSet, grid[i + 1, j], grid[i + 1, j + 1], grid[i, j + 1]);
                    }
                }
            }
            return result;
        }

        private static void AddGeodesicTriangle(GeodesicMesh g, HashSet<long> edgeSet, int i0, int i1, int i2)
        {
            Vector3 a = g.Vertices[i0], b = g.Vertices[i1], c = g.Vertices[i2];
            if (a.y < -1e-4f || b.y < -1e-4f || c.y < -1e-4f) return; // solo la mitad superior
            if (a.y + b.y + c.y < 1e-4f) return;                      // triángulos planos en el ecuador
            // Orden de vértices que hace que la cara mire hacia fuera (convención de Unity).
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (Vector3.Dot(n, a + b + c) < 0f) { int t = i1; i1 = i2; i2 = t; }
            g.Triangles.Add(i0); g.Triangles.Add(i1); g.Triangles.Add(i2);
            AddEdge(g, edgeSet, i0, i1);
            AddEdge(g, edgeSet, i1, i2);
            AddEdge(g, edgeSet, i2, i0);
        }

        private static void AddEdge(GeodesicMesh g, HashSet<long> edgeSet, int a, int b)
        {
            int lo = Mathf.Min(a, b), hi = Mathf.Max(a, b);
            long key = ((long)lo << 32) | (uint)hi;
            if (edgeSet.Add(key)) g.Edges.Add(new Vector2Int(lo, hi));
        }

        /// <summary>
        /// Paneles de vidrio de la cúpula geodésica: cada triángulo encogido hacia su centro (deja
        /// ver las barras) y con cara exterior e interior. <paramref name="scale"/> = (radio, alto, radio).
        /// </summary>
        public static Mesh GeodesicPanels(GeodesicMesh g, Vector3 scale, float inset)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            var uvs = new List<Vector2>();
            for (int t = 0; t < g.Triangles.Count; t += 3)
            {
                Vector3 a = Vector3.Scale(g.Vertices[g.Triangles[t]], scale);
                Vector3 b = Vector3.Scale(g.Vertices[g.Triangles[t + 1]], scale);
                Vector3 c = Vector3.Scale(g.Vertices[g.Triangles[t + 2]], scale);
                Vector3 centre = (a + b + c) / 3f;
                a = Vector3.Lerp(centre, a, inset); b = Vector3.Lerp(centre, b, inset); c = Vector3.Lerp(centre, c, inset);
                int s = verts.Count;
                verts.Add(a); verts.Add(b); verts.Add(c);    // cara exterior
                verts.Add(a); verts.Add(c); verts.Add(b);    // cara interior (orden invertido)
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0.5f, 1f));
                uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(0.5f, 1f)); uvs.Add(new Vector2(1f, 0f));
                for (int k = 0; k < 6; k++) tris.Add(s + k);
            }
            var mesh = new Mesh { name = "PanelesGeodesicos" };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------
        // Aberturas (puertas)
        // ------------------------------------------------------------------

        /// <summary>Caja orientada en el espacio local de la malla.</summary>
        public struct CutBox
        {
            public Vector3 Center, HalfExtents;
            public Quaternion Rotation;

            public bool Contains(Vector3 p)
            {
                Vector3 q = Quaternion.Inverse(Rotation) * (p - Center);
                return Mathf.Abs(q.x) <= HalfExtents.x && Mathf.Abs(q.y) <= HalfExtents.y && Mathf.Abs(q.z) <= HalfExtents.z;
            }
        }

        /// <summary>
        /// Quita de la malla (en su sitio) los triángulos cuyo centro cae dentro de alguna caja.
        /// El hueco sigue la teselación de la malla: es la puerta "en bruto"; el pasillo y el marco la tapan.
        /// Devuelve cuántos triángulos se han quitado.
        /// </summary>
        public static int CutTriangles(Mesh mesh, IList<CutBox> boxes)
        {
            if (mesh == null || boxes == null || boxes.Count == 0) return 0;
            Vector3[] v = mesh.vertices;
            int removed = 0;
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                int[] tris = mesh.GetTriangles(sub);
                var kept = new List<int>(tris.Length);
                for (int t = 0; t < tris.Length; t += 3)
                {
                    Vector3 c = (v[tris[t]] + v[tris[t + 1]] + v[tris[t + 2]]) / 3f;
                    bool inside = false;
                    for (int b = 0; b < boxes.Count && !inside; b++) inside = boxes[b].Contains(c);
                    if (inside) { removed++; continue; }
                    kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
                }
                if (kept.Count != tris.Length) mesh.SetTriangles(kept, sub);
            }
            if (removed > 0) mesh.RecalculateBounds();
            return removed;
        }

        /// <summary>Caja hueca con caras hacia dentro o hacia fuera (paredes de la nave).</summary>
        public static Mesh BoxSurface(Vector3 size, bool inward)
        {
            Vector3 h = size * 0.5f;
            Vector3[] corners =
            {
                new Vector3(-h.x, 0f, -h.z), new Vector3(h.x, 0f, -h.z), new Vector3(h.x, size.y, -h.z), new Vector3(-h.x, size.y, -h.z),
                new Vector3(-h.x, 0f, h.z), new Vector3(h.x, 0f, h.z), new Vector3(h.x, size.y, h.z), new Vector3(-h.x, size.y, h.z),
            };
            // Caras (sin el suelo): delante, detrás, izquierda, derecha, techo. Orden horario visto desde fuera.
            int[][] faces =
            {
                new[] { 0, 3, 2, 1 }, new[] { 5, 6, 7, 4 }, new[] { 4, 7, 3, 0 }, new[] { 1, 2, 6, 5 }, new[] { 3, 7, 6, 2 }
            };
            // Cada cara se divide en celdas de ~0,75 m: así se pueden recortar puertas (por triángulos).
            const float cell = 0.75f;
            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            foreach (int[] f in faces)
            {
                Vector3 p0 = corners[f[0]], p1 = corners[f[1]], p3 = corners[f[3]];
                Vector3 du = p3 - p0, dv = p1 - p0;
                int nu = Mathf.Max(1, Mathf.CeilToInt(du.magnitude / cell));
                int nv = Mathf.Max(1, Mathf.CeilToInt(dv.magnitude / cell));
                int start = verts.Count;
                for (int j = 0; j <= nv; j++)
                {
                    for (int i = 0; i <= nu; i++)
                    {
                        verts.Add(p0 + du * ((float)i / nu) + dv * ((float)j / nv));
                        uvs.Add(new Vector2(du.magnitude * i / nu, dv.magnitude * j / nv) * 0.5f);
                    }
                }
                for (int j = 0; j < nv; j++)
                {
                    for (int i = 0; i < nu; i++)
                    {
                        int a = start + j * (nu + 1) + i;   // esquina tipo p0
                        int d = a + 1;                      // hacia p3
                        int b = a + (nu + 1);               // hacia p1
                        int c = b + 1;                      // tipo p2
                        if (inward)
                        {
                            tris.Add(a); tris.Add(c); tris.Add(b);
                            tris.Add(a); tris.Add(d); tris.Add(c);
                        }
                        else
                        {
                            tris.Add(a); tris.Add(b); tris.Add(c);
                            tris.Add(a); tris.Add(c); tris.Add(d);
                        }
                    }
                }
            }
            var mesh = new Mesh { name = "Caja" };
            mesh.indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
