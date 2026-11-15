using UnityEngine;

namespace MarsRobot.Visuals
{
    /// <summary>
    /// Colisionadores ajustados a las mallas reales.
    ///  • Estructuras y estaciones estáticas: solo BoxCollider ajustado a los bounds de sus mallas
    ///    (sin Rigidbody: en Unity un colisionador estático sin Rigidbody es lo más eficiente).
    ///  • Atrezo dinámico: BoxCollider ajustado + Rigidbody con masa calculada por volumen y densidad.
    /// </summary>
    public static class ColliderFitter
    {
        /// <summary>
        /// Calcula la caja que envuelve todas las mallas hijas, en el espacio local de <paramref name="go"/>.
        /// Devuelve false (y avisa) si no hay mallas.
        /// </summary>
        public static bool LocalMeshBounds(GameObject go, out Bounds bounds)
        {
            bounds = new Bounds();
            if (go == null)
            {
                Debug.LogWarning("[ColliderFitter] Objeto nulo.");
                return false;
            }
            bool any = false;
            Matrix4x4 toLocal = go.transform.worldToLocalMatrix;
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Bounds b = mf.sharedMesh.bounds;
                Matrix4x4 m = toLocal * mf.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else bounds.Encapsulate(p);
                }
            }
            if (!any) Debug.LogWarning("[ColliderFitter] " + go.name + " no tiene mallas: no se puede ajustar el colisionador.");
            return any;
        }

        /// <summary>BoxCollider ajustado a las mallas (estructura estática).</summary>
        public static BoxCollider FitStatic(GameObject go, float padding = 0.01f)
        {
            Bounds b;
            if (!LocalMeshBounds(go, out b)) return null;
            var box = go.GetComponent<BoxCollider>();
            if (box == null) box = go.AddComponent<BoxCollider>();
            box.center = b.center;
            box.size = b.size + Vector3.one * padding * 2f;
            return box;
        }

        /// <summary>
        /// BoxCollider ajustado + Rigidbody con masa = volumen × densidad (kg/m³). Densidades típicas:
        /// caja de herramientas llena ≈ 400, contenedor de plástico ≈ 150, bidón lleno ≈ 900.
        /// </summary>
        public static Rigidbody FitDynamic(GameObject go, float densityKgM3)
        {
            BoxCollider box = FitStatic(go, 0f);
            if (box == null) return null;
            var rb = go.GetComponent<Rigidbody>();
            if (rb == null) rb = go.AddComponent<Rigidbody>();
            Vector3 s = Vector3.Scale(box.size, go.transform.lossyScale);
            float volume = Mathf.Abs(s.x * s.y * s.z);
            rb.mass = Mathf.Clamp(volume * densityKgM3, 0.2f, 500f);
            rb.linearDamping = 0.05f;
            rb.angularDamping = 0.2f;
            rb.interpolation = RigidbodyInterpolation.None;
            rb.collisionDetectionMode = CollisionDetectionMode.Discrete;
            rb.isKinematic = false;
            return rb;
        }
    }
}
