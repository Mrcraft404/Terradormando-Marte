using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Visuals
{
    /// <summary>
    /// Modelos 3D opcionales de la NASA (https://science.nasa.gov/3d-resources/).
    /// Cada modelo es un prefab en Assets/Resources/NASAModels/&lt;Id&gt;.prefab creado por el menú
    /// Marte ▸ Configurar modelos NASA (ya escalado, mirando a +Z y apoyado en el suelo en y = 0).
    /// Si el prefab no existe, Get devuelve null y el juego usa su modelo procedural de siempre.
    /// Los modelos son solo visuales: no llevan colisionadores.
    /// </summary>
    public static class NasaModelLibrary
    {
        /// <summary>Rover Curiosity (NASA/JPL-Caltech): aspecto del rover XR-1.</summary>
        public const string Rover = "CuriosityRover";
        /// <summary>Módulo InSight (NASA/JPL-Caltech): restos de un módulo de aterrizaje.</summary>
        public const string Lander = "InSightLander";
        /// <summary>Helicóptero Ingenuity (NASA/JPL-Caltech): restos de una sonda antigua.</summary>
        public const string Helicopter = "Ingenuity";

        public const string ResourcesFolder = "NASAModels";
        public const string Credit = "Modelos 3D: NASA/JPL-Caltech (science.nasa.gov/3d-resources)";

        private static readonly Dictionary<string, GameObject> Cache = new Dictionary<string, GameObject>();

        /// <summary>Prefab del modelo o null si no está importado.</summary>
        public static GameObject Get(string id)
        {
            GameObject prefab;
            if (!Cache.TryGetValue(id, out prefab))
            {
                prefab = Resources.Load<GameObject>(ResourcesFolder + "/" + id);
                Cache[id] = prefab;
            }
            return prefab;
        }

        public static bool Has(string id)
        {
            return Get(id) != null;
        }

        /// <summary>True si hay al menos un modelo de la NASA importado (para mostrar los créditos).</summary>
        public static bool HasAny
        {
            get { return Has(Rover) || Has(Lander) || Has(Helicopter); }
        }

        /// <summary>Instancia el modelo como hijo de <paramref name="parent"/> (null si no existe).</summary>
        public static GameObject Spawn(string id, Transform parent, string objectName)
        {
            GameObject prefab = Get(id);
            if (prefab == null) return null;
            GameObject go = Object.Instantiate(prefab, parent, false);
            go.name = objectName;
            foreach (Collider c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (Renderer r in go.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                r.receiveShadows = true;
            }
            return go;
        }

        /// <summary>Caja que envuelve todos los renderers (en espacio de mundo).</summary>
        public static Bounds WorldBounds(GameObject go)
        {
            // Se calcula desde las mallas y las matrices actuales (Renderer.bounds puede estar
            // desfasado justo después de mover o escalar un objeto en el editor).
            bool any = false;
            var b = new Bounds(go.transform.position, Vector3.zero);
            foreach (MeshFilter mf in go.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null || mf.GetComponent<Renderer>() == null) continue;
                Encapsulate(ref b, ref any, mf.sharedMesh.bounds, mf.transform.localToWorldMatrix);
            }
            foreach (SkinnedMeshRenderer smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (smr.sharedMesh == null) continue;
                Encapsulate(ref b, ref any, smr.sharedMesh.bounds, smr.transform.localToWorldMatrix);
            }
            return b;
        }

        private static void Encapsulate(ref Bounds b, ref bool any, Bounds local, Matrix4x4 m)
        {
            Vector3 c = local.center, e = local.extents;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3(c.x + ((i & 1) == 0 ? -e.x : e.x), c.y + ((i & 2) == 0 ? -e.y : e.y), c.z + ((i & 4) == 0 ? -e.z : e.z));
                Vector3 p = m.MultiplyPoint3x4(corner);
                if (!any)
                {
                    b = new Bounds(p, Vector3.zero);
                    any = true;
                }
                else
                {
                    b.Encapsulate(p);
                }
            }
        }
    }
}
