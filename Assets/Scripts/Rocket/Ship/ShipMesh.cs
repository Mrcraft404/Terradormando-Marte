using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base.Ship
{
    /// <summary>
    /// Utilidades para construir la nave por código:
    ///  • Lathe: superficie de revolución (casco, nariz, tanques, toberas) a partir de un perfil
    ///    (radio, altura), opcionalmente solo en un sector angular y con cara interior (doble cara)
    ///    para que se vea por dentro al hacer el corte.
    ///  • Piezas primitivas, barras entre dos puntos, fusión de mallas por material y materiales/texturas.
    /// Ejes: Y arriba, ángulo 0° = +Z (frente de la nave, lado de la puerta), creciendo hacia +X.
    /// </summary>
    public static class ShipMesh
    {
        /// <summary>Superficie de revolución del perfil entre dos ángulos (grados).</summary>
        public static Mesh Lathe(IList<Vector2> profile, float fromDeg, float toDeg, int segments, bool doubleSided, float wallThickness = 0.08f, float uvRepeatsAround = 1f)
        {
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            int rings = profile.Count;
            float totalLen = 0f;
            var vCoord = new float[rings];
            for (int i = 1; i < rings; i++)
            {
                totalLen += Vector2.Distance(profile[i - 1], profile[i]);
                vCoord[i] = totalLen;
            }

            for (int side = 0; side < (doubleSided ? 2 : 1); side++)
            {
                int start = verts.Count;
                float inset = side == 0 ? 0f : wallThickness;
                for (int s = 0; s <= segments; s++)
                {
                    float a = Mathf.Lerp(fromDeg, toDeg, (float)s / segments) * Mathf.Deg2Rad;
                    Vector3 dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    for (int i = 0; i < rings; i++)
                    {
                        float r = Mathf.Max(0f, profile[i].x - inset);
                        verts.Add(dir * r + Vector3.up * profile[i].y);
                        // Normal del perfil: perpendicular a la tangente en el plano (r, y).
                        Vector2 t = profile[Mathf.Min(i + 1, rings - 1)] - profile[Mathf.Max(i - 1, 0)];
                        Vector2 n2 = new Vector2(t.y, -t.x).normalized;
                        Vector3 n = (dir * n2.x + Vector3.up * n2.y).normalized;
                        norms.Add(side == 0 ? n : -n);
                        uvs.Add(new Vector2((float)s / segments * uvRepeatsAround, vCoord[i] / Mathf.Max(0.01f, totalLen) * (totalLen / 4f)));
                    }
                }
                for (int s = 0; s < segments; s++)
                {
                    for (int i = 0; i < rings - 1; i++)
                    {
                        int a0 = start + s * rings + i, a1 = a0 + 1, b0 = a0 + rings, b1 = b0 + 1;
                        // El ángulo crece hacia +X: vista desde fuera, (a0, b0, a1) va en sentido
                        // horario, que es la cara visible en Unity. La cara interior va al revés.
                        if (side == 0)
                        {
                            tris.Add(a0); tris.Add(b0); tris.Add(a1);
                            tris.Add(b0); tris.Add(b1); tris.Add(a1);
                        }
                        else
                        {
                            tris.Add(a0); tris.Add(a1); tris.Add(b0);
                            tris.Add(b0); tris.Add(a1); tris.Add(b1);
                        }
                    }
                }
            }

            var mesh = new Mesh { name = "Torno" };
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Objeto con una malla y un material.</summary>
        public static GameObject MeshObject(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // ------------------------------------------------------------------
        // Primitivas
        // ------------------------------------------------------------------

        public static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        public static Transform Box(Transform p, Vector3 pos, Vector3 size, Material m, float yaw = 0f)
        {
            return Prim(PrimitiveType.Cube, p, pos, size, Quaternion.Euler(0f, yaw, 0f), m);
        }

        /// <summary>Cilindro vertical de radio y altura dados (base en pos.y − h/2).</summary>
        public static Transform Cyl(Transform p, Vector3 pos, float radius, float height, Material m, Vector3 euler = default(Vector3))
        {
            return Prim(PrimitiveType.Cylinder, p, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), Quaternion.Euler(euler), m);
        }

        public static Transform Beam(Transform p, Vector3 a, Vector3 b, float radius, Material m)
        {
            Vector3 d = b - a;
            Quaternion rot = d.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(Vector3.up, d.normalized) : Quaternion.identity;
            return Prim(PrimitiveType.Cylinder, p, (a + b) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f), rot, m);
        }

        /// <summary>Fusiona en una malla por material todas las piezas sin hijos que cuelgan directamente de root.</summary>
        public static void Combine(Transform root)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toDelete = new List<GameObject>();
            Matrix4x4 toLocal = root.worldToLocalMatrix;
            for (int c = 0; c < root.childCount; c++)
            {
                Transform child = root.GetChild(c);
                if (child.childCount > 0 || child.name.StartsWith("Fusionado")) continue;
                var mf = child.GetComponent<MeshFilter>();
                var r = child.GetComponent<MeshRenderer>();
                if (mf == null || r == null || mf.sharedMesh == null) continue;
                List<CombineInstance> list;
                if (!groups.TryGetValue(r.sharedMaterial, out list)) groups[r.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
                toDelete.Add(child.gameObject);
            }
            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = "Fusionado " + kv.Key.name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                MeshObject(root, "Fusionado " + kv.Key.name, mesh, kv.Key);
            }
            for (int i = 0; i < toDelete.Count; i++) Object.DestroyImmediate(toDelete[i]);
        }

        // ------------------------------------------------------------------
        // Materiales y texturas procedurales
        // ------------------------------------------------------------------

        public static Material Mat(string name, Color c, float metallic, float smooth, Texture2D tex = null, Vector2 tiling = default(Vector2), Color emission = default(Color))
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(urp != null ? urp : Shader.Find("Standard")) { name = name, color = c };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = tiling == default(Vector2) ? Vector2.one : tiling;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }

        /// <summary>Acero inoxidable cepillado con soldaduras de anillos cada ~1,8 m.</summary>
        public static Texture2D StainlessTexture()
        {
            const int s = 256;
            var px = new Color32[s * s];
            var rng = new System.Random(4);
            float[] col = new float[s];
            for (int x = 0; x < s; x++) col[x] = (float)rng.NextDouble();
            for (int y = 0; y < s; y++)
            {
                bool weld = y % 64 < 2;
                for (int x = 0; x < s; x++)
                {
                    float v = 0.88f + col[x] * 0.07f + (float)rng.NextDouble() * 0.03f; // cepillado vertical
                    if (weld) v = 0.7f;
                    if (x % 128 < 1) v = 0.75f; // costura vertical
                    byte b = (byte)(Mathf.Clamp01(v) * 255);
                    px[y * s + x] = new Color32(b, b, b, 255);
                }
            }
            return Tex(px, s, "Acero inoxidable");
        }

        /// <summary>Losetas hexagonales negras del escudo térmico.</summary>
        public static Texture2D HeatTileTexture()
        {
            const int s = 256;
            var px = new Color32[s * s];
            const float size = 16f;
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    // Distancia al borde de la celda hexagonal más cercana (aprox. con rejilla desplazada).
                    float row = y / (size * 0.866f);
                    int ry = Mathf.FloorToInt(row);
                    float off = (ry % 2) * size * 0.5f;
                    float fx = Mathf.Repeat(x + off, size) / size - 0.5f;
                    float fy = (row - ry) - 0.5f;
                    float edge = Mathf.Max(Mathf.Abs(fx) * 1.15f, Mathf.Abs(fy));
                    byte v = edge > 0.46f ? (byte)55 : (byte)(28 + ((x * 7 + y * 13) % 9));
                    px[y * s + x] = new Color32(v, v, v, 255);
                }
            }
            return Tex(px, s, "Escudo térmico");
        }

        /// <summary>Suelo de rejilla metálica industrial.</summary>
        public static Texture2D GratingTexture()
        {
            const int s = 128;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool bar = x % 16 < 3 || y % 32 < 2;
                    px[y * s + x] = bar ? new Color32(150, 152, 158, 255) : new Color32(55, 57, 62, 255);
                }
            }
            return Tex(px, s, "Rejilla");
        }

        /// <summary>Señalización: franjas amarillas y negras.</summary>
        public static Texture2D HazardTexture()
        {
            const int s = 64;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    px[y * s + x] = ((x + y) / 16) % 2 == 0 ? new Color32(240, 190, 30, 255) : new Color32(25, 25, 25, 255);
                }
            }
            return Tex(px, s, "Señalización");
        }

        private static Texture2D Tex(Color32[] px, int s, string name)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }
    }
}
