using System;
using System.Collections;
using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Visuals;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Construcción modular POR CAPAS de los módulos de la base (nada de bloques monolíticos).
    /// Cada módulo es un objeto bajo <c>_Structures</c> con un hijo por capa, en este orden:
    ///
    ///   Capa 0 · Cimentación        losa de regolito sinterizado, suelo compactado, zapatas, BoxCollider
    ///   Capa 1 · Esqueleto          costillas de titanio o núcleo inflable (anillo + cinchas)
    ///   Capa 2 · Contención         membrana de Kevlar + aerogel (cara exterior e interior), MeshCollider
    ///   Capa 3 · Escudo de regolito cúpula o bóveda de 1–3 m de espesor, MeshCollider
    ///   Capa 4 · Sistemas internos  racks hidropónicos, literas, tanques ISRU, bancos de taller
    ///
    /// Uso:
    ///   • Editor: Marte ▸ Base ▸ Construir módulos de prueba, o el menú contextual del componente.
    ///   • Juego:  builder.Build(datos, posición, rotación) anima la obra capa a capa.
    ///   • Comandos: BuildLayer / RemoveLayer / RebuildMissingLayers para construir o romper capas sueltas.
    ///
    /// Si una capa tiene prefab en el <see cref="MarsModuleData"/>, se instancia ese prefab; si no,
    /// se genera la geometría provisional por código (una malla por material, pocas llamadas de dibujo).
    /// </summary>
    public sealed class MarsHabitatBuilder : MonoBehaviour
    {
        [Tooltip("Módulo que construye 'Construir el módulo aquí' y buildOnStart.")]
        [SerializeField] private MarsModuleData module;
        [SerializeField] private bool buildOnStart;
        [Tooltip("En juego, cada capa aparece creciendo durante su buildSeconds. En el Editor siempre es instantáneo.")]
        [SerializeField] private bool animateInPlayMode = true;
        [Tooltip("Apoya el módulo sobre el terreno (TerrainManager) si existe.")]
        [SerializeField] private bool snapToTerrain = true;

        /// <summary>Se ha terminado una capa de un hábitat.</summary>
        public event Action<MarsHabitatController, HabitatLayer> LayerBuilt;

        /// <summary>Las 5 capas de un hábitat están terminadas.</summary>
        public event Action<MarsHabitatController> HabitatCompleted;

        private void Start()
        {
            if (buildOnStart) BuildHere();
        }

        [ContextMenu("Construir el módulo aquí")]
        public void BuildHere()
        {
            Build(module != null ? module : MarsModuleData.CreateDefault(MarsModuleType.CrewHabitat),
                  transform.position, transform.rotation);
        }

        /// <summary>
        /// Construye un módulo. En juego (y con animación) capa a capa; si no, de golpe.
        /// <paramref name="layerGate"/> (opcional, solo en la obra animada) se consulta antes de cada
        /// capa: si devuelve false, la obra espera (p. ej. a que haya materiales) y lo vuelve a intentar.
        /// </summary>
        public MarsHabitatController Build(MarsModuleData data, Vector3 position, Quaternion rotation,
                                           Func<MarsHabitatController, HabitatLayer, bool> layerGate = null)
        {
            if (data == null)
            {
                Debug.LogError("[MarsHabitatBuilder] No hay MarsModuleData que construir.", this);
                return null;
            }
            if (snapToTerrain) position = SnapToTerrain(position);
            MarsHabitatController habitat = CreateHabitatRoot(data, position, rotation);

            if (Application.isPlaying && animateInPlayMode)
            {
                StartCoroutine(BuildSequence(habitat, layerGate));
            }
            else
            {
                BuildAllLayers(habitat);
                if (HabitatCompleted != null) HabitatCompleted(habitat);
            }
            return habitat;
        }

        /// <summary>Construcción inmediata sin instancia del builder (menú del Editor, herramientas).</summary>
        public static MarsHabitatController BuildImmediate(MarsModuleData data, Vector3 position, Quaternion rotation)
        {
            if (data == null) return null;
            MarsHabitatController habitat = CreateHabitatRoot(data, position, rotation);
            BuildAllLayers(habitat);
            return habitat;
        }

        private IEnumerator BuildSequence(MarsHabitatController habitat, Func<MarsHabitatController, HabitatLayer, bool> layerGate)
        {
            // Mientras dura la obra no se valida (si no, avisaría de que "faltan" las capas pendientes).
            habitat.BeginBatch();
            var wait = new WaitForSeconds(1f);
            for (int i = 0; i < MarsModuleData.LayerCount; i++)
            {
                if (habitat == null) yield break; // lo borraron durante la obra
                var layer = (HabitatLayer)i;

                // Obra por fases con recursos: espera hasta poder pagar la capa.
                while (layerGate != null && habitat != null && !layerGate(habitat, layer))
                {
                    habitat.ConstructionStatus = HabitatLayerNode.NodeName(layer) + Tr.T(": esperando materiales");
                    yield return wait;
                }
                if (habitat == null) yield break;
                habitat.ConstructionStatus = Tr.T("Construyendo ") + HabitatLayerNode.NodeName(layer);

                HabitatLayerNode node = BuildLayer(habitat, layer);
                float seconds = habitat.Data.GetLayer(layer).buildSeconds;

                // La capa "crece" desde el suelo durante su tiempo de obra.
                Transform t = node.transform;
                for (float e = 0f; e < seconds && t != null; e += Time.deltaTime)
                {
                    float k = Mathf.SmoothStep(0.02f, 1f, e / seconds);
                    t.localScale = new Vector3(1f, k, 1f);
                    yield return null;
                }
                if (t != null) t.localScale = Vector3.one;
                if (LayerBuilt != null) LayerBuilt(habitat, layer);
            }
            if (habitat == null) yield break;
            habitat.ConstructionStatus = Tr.T("Terminado");
            habitat.EndBatch(); // validación final con las 5 capas
            if (HabitatCompleted != null) HabitatCompleted(habitat);
        }

        private static void BuildAllLayers(MarsHabitatController habitat)
        {
            habitat.BeginBatch();
            for (int i = 0; i < MarsModuleData.LayerCount; i++) BuildLayer(habitat, (HabitatLayer)i);
            habitat.EndBatch();
        }

        // ------------------------------------------------------------------
        // API por capas ("comandos")
        // ------------------------------------------------------------------

        /// <summary>Crea el objeto raíz del módulo bajo _Structures, con su controlador y sin capas.</summary>
        public static MarsHabitatController CreateHabitatRoot(MarsModuleData data, Vector3 position, Quaternion rotation)
        {
            Transform structures = SceneRoots.Get(SceneRoot.Structures);
            var go = new GameObject(UniqueName(structures, data.displayName));
            go.transform.SetParent(structures, false);
            go.transform.SetPositionAndRotation(position, rotation);
            var habitat = go.AddComponent<MarsHabitatController>();
            habitat.Initialize(data);
            return habitat;
        }

        /// <summary>Construye (o reconstruye) una capa concreta del hábitat.</summary>
        public static HabitatLayerNode BuildLayer(MarsHabitatController habitat, HabitatLayer layer)
        {
            if (habitat == null || habitat.Data == null) return null;
            RemoveLayer(habitat, layer, false);

            MarsModuleData data = habitat.Data;
            var go = new GameObject(HabitatLayerNode.NodeName(layer));
            go.transform.SetParent(habitat.transform, false);
            go.transform.SetSiblingIndex(Mathf.Min((int)layer, habitat.transform.childCount - 1));
            var node = go.AddComponent<HabitatLayerNode>();
            node.layer = layer;

            HabitatLayerDefinition def = data.GetLayer(layer);
            if (def.prefab != null)
            {
                GameObject instance = Instantiate(def.prefab, go.transform, false);
                instance.name = def.prefab.name;
                if (def.materialOverride != null)
                {
                    foreach (Renderer r in instance.GetComponentsInChildren<Renderer>(true)) r.sharedMaterial = def.materialOverride;
                }
            }
            else
            {
                GenerateLayer(data, node, def.materialOverride);
                CutDoorways(habitat, node);
            }
            FillPhysicalProperties(data, node);
            habitat.NotifyLayerChanged(layer);
            return node;
        }

        /// <summary>
        /// Abre las puertas conectadas en el esqueleto, la contención y el escudo: quita los
        /// triángulos de las mallas visibles Y de las de colisión, para poder pasar de verdad.
        /// (Las capas hechas con prefab no se recortan: el prefab debe traer sus puertas.)
        /// </summary>
        private static void CutDoorways(MarsHabitatController habitat, HabitatLayerNode node)
        {
            if (habitat.Openings.Count == 0) return;
            if (node.layer != HabitatLayer.Skeleton && node.layer != HabitatLayer.PressureShell &&
                node.layer != HabitatLayer.RegolithShield) return;

            var boxes = new List<HabitatGeometry.CutBox>();
            foreach (int index in habitat.Openings)
            {
                Vector3 c, h;
                Quaternion r;
                habitat.Data.DoorCutBox(index, out c, out h, out r);
                boxes.Add(new HabitatGeometry.CutBox { Center = c, HalfExtents = h, Rotation = r });
            }

            // Las mallas de la capa están en el espacio local del hábitat (el nodo no tiene giro ni desplazamiento).
            foreach (MeshFilter mf in node.GetComponentsInChildren<MeshFilter>(true))
            {
                HabitatGeometry.CutTriangles(mf.sharedMesh, boxes);
            }
            foreach (MeshCollider mc in node.GetComponentsInChildren<MeshCollider>(true))
            {
                Mesh m = mc.sharedMesh;
                if (m == null) continue;
                HabitatGeometry.CutTriangles(m, boxes);
                mc.sharedMesh = null;   // obliga a recalcular la forma física con el hueco
                mc.sharedMesh = m;
            }
        }

        // ------------------------------------------------------------------
        // Conexiones entre módulos
        // ------------------------------------------------------------------

        /// <summary>
        /// Abre la puerta <paramref name="anchorIndex"/> del hábitat y rehace las capas ya
        /// construidas que la cruzan (las que falten se harán ya con la puerta).
        /// </summary>
        public static void OpenDoorway(MarsHabitatController habitat, int anchorIndex)
        {
            if (habitat == null || habitat.Data == null || !habitat.AddOpening(anchorIndex)) return;
            habitat.BeginBatch();
            foreach (HabitatLayer layer in new[] { HabitatLayer.Skeleton, HabitatLayer.PressureShell, HabitatLayer.RegolithShield })
            {
                HabitatLayerNode node = habitat.GetLayerNode(layer);
                if (node == null) continue;
                float integrity = node.integrity;
                HabitatLayerNode rebuilt = BuildLayer(habitat, layer);
                if (rebuilt != null) rebuilt.integrity = integrity; // conserva los daños que tuviera
            }
            habitat.EndBatch();
        }

        /// <summary>
        /// Une dos hábitats por sus puertas con un pasillo presurizado transitable. Abre ambas puertas.
        /// </summary>
        public static HabitatConnection ConnectHabitats(MarsHabitatController a, int anchorA, MarsHabitatController b, int anchorB)
        {
            if (a == null || b == null || a == b) return null;
            OpenDoorway(a, anchorA);
            OpenDoorway(b, anchorB);
            Vector3 from = a.transform.TransformPoint(a.Data.DoorPoint(anchorA));
            Vector3 to = b.transform.TransformPoint(b.Data.DoorPoint(anchorB));
            GameObject corridor = BuildCorridor(from, to);
            if (corridor == null) return null;
            var link = corridor.AddComponent<HabitatConnection>();
            link.Setup(a, anchorA, b, anchorB, null);
            return link;
        }

        /// <summary>Une un hábitat con la esclusa del cohete (la entrada desde la nave).</summary>
        public static HabitatConnection ConnectToRocket(MarsHabitatController habitat, int anchor, RocketAirlock airlock)
        {
            if (habitat == null || airlock == null) return null;
            OpenDoorway(habitat, anchor);
            Vector3 from = habitat.transform.TransformPoint(habitat.Data.DoorPoint(anchor));
            GameObject corridor = BuildCorridor(from, airlock.DoorPoint);
            if (corridor == null) return null;
            var link = corridor.AddComponent<HabitatConnection>();
            link.Setup(habitat, anchor, null, -1, airlock);
            return link;
        }

        /// <summary>Quita una capa (para pruebas de validación, daños o desmontaje).</summary>
        public static void RemoveLayer(MarsHabitatController habitat, HabitatLayer layer, bool notify = true)
        {
            if (habitat == null) return;
            HabitatLayerNode node = habitat.GetLayerNode(layer);
            if (node == null) return;
            node.transform.SetParent(null, false); // fuera de la jerarquía ya, aunque Destroy espere al final del frame
            HabitatGeometry.SafeDestroy(node.gameObject);
            if (notify) habitat.NotifyLayerChanged(layer);
        }

        /// <summary>Construye solo las capas que falten o estén destruidas. Devuelve cuántas.</summary>
        public static int RebuildMissingLayers(MarsHabitatController habitat)
        {
            if (habitat == null) return 0;
            int count = 0;
            habitat.BeginBatch();
            for (int i = 0; i < MarsModuleData.LayerCount; i++)
            {
                var layer = (HabitatLayer)i;
                HabitatLayerNode node = habitat.GetLayerNode(layer);
                if (node == null || !node.IsFunctional)
                {
                    BuildLayer(habitat, layer);
                    count++;
                }
            }
            habitat.EndBatch();
            return count;
        }

        private static Vector3 SnapToTerrain(Vector3 position)
        {
            TerrainManager terrain;
            if (Application.isPlaying && GameServices.TryGet(out terrain) && terrain.IsMapReady)
            {
                position.y = terrain.GetHeightAt(position);
            }
            return position;
        }

        private static string UniqueName(Transform parent, string baseName)
        {
            int n = 1;
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name.StartsWith(baseName, StringComparison.Ordinal)) n++;
            }
            return baseName + " " + n.ToString("00");
        }

        private static void FillPhysicalProperties(MarsModuleData data, HabitatLayerNode node)
        {
            switch (node.layer)
            {
                case HabitatLayer.Foundation:
                    node.thicknessMeters = data.foundationThickness;
                    node.arealDensity = data.foundationThickness * 100f * MarsModuleData.SinteredRegolithDensityTonPerM3;
                    break;
                case HabitatLayer.Skeleton:
                    node.thicknessMeters = data.ribThickness;
                    break;
                case HabitatLayer.PressureShell:
                    node.thicknessMeters = (data.kevlarLayers * data.kevlarLayerMm + data.aerogelMm) / 1000f;
                    node.uValue = data.ShellUValue;
                    // Kevlar ≈ 1,44 g/cm³; aerogel ≈ 0,1 g/cm³.
                    node.arealDensity = data.kevlarLayers * data.kevlarLayerMm * 0.1f * 1.44f + data.aerogelMm * 0.1f * 0.1f;
                    node.ratedPressureKPa = data.designPressureKPa * 4f; // factor de seguridad 4 (habitual en estructuras inflables)
                    break;
                case HabitatLayer.RegolithShield:
                    node.thicknessMeters = data.shieldThickness;
                    node.arealDensity = data.ShieldArealDensity;
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Generación procedural de cada capa
        // ------------------------------------------------------------------

        private static void GenerateLayer(MarsModuleData d, HabitatLayerNode node, Material overrideMat)
        {
            var acc = new HabitatMeshAccumulator();
            Transform parent = node.transform;
            switch (node.layer)
            {
                case HabitatLayer.Foundation:
                    BuildFoundation(d, acc, parent, overrideMat);
                    acc.Build(parent, "Cimentacion");
                    break;
                case HabitatLayer.Skeleton:
                    BuildSkeleton(d, acc, overrideMat);
                    acc.Build(parent, "Esqueleto");
                    break;
                case HabitatLayer.PressureShell:
                    Mesh outer = BuildPressureShell(d, acc, overrideMat);
                    acc.Build(parent, "Contencion");
                    AddMeshCollider(parent, outer, "Colision contencion");
                    break;
                case HabitatLayer.RegolithShield:
                    Mesh shieldOuter = BuildShield(d, acc, overrideMat);
                    acc.Build(parent, "Escudo");
                    AddMeshCollider(parent, shieldOuter, "Colision escudo");
                    break;
                default:
                    BuildInterior(d, acc, parent, overrideMat);
                    acc.Build(parent, "Interior");
                    break;
            }
        }

        private static void AddMeshCollider(Transform parent, Mesh mesh, string name)
        {
            if (mesh == null) return;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var col = go.AddComponent<MeshCollider>();
            col.sharedMesh = mesh;
        }

        // ---------------- Capa 0 · Cimentación ----------------

        private static void BuildFoundation(MarsModuleData d, HabitatMeshAccumulator acc, Transform parent, Material ov)
        {
            Material slab = HabitatMaterials.Resolve(ov, HabitatMaterials.SinteredSlab);
            Material soil = HabitatMaterials.Resolve(ov, HabitatMaterials.CompactedSoil);
            Material steel = HabitatMaterials.Resolve(ov, HabitatMaterials.AnchorSteel);
            float f = d.foundationThickness;
            float shieldOuter = d.ShieldInnerRadius + d.shieldThickness;
            float footingHalf = (f + d.footingDepth) * 0.5f;
            float footingY = (f - d.footingDepth) * 0.5f;
            var box = parent.gameObject.AddComponent<BoxCollider>();

            if (d.IsDomeLike)
            {
                float rs = d.radius + 1f;
                // Losa circular de regolito sinterizado (el cilindro de Unity mide 2 de alto).
                acc.AddPrimitive(PrimitiveType.Cylinder, new Vector3(0f, f * 0.5f, 0f), Quaternion.identity,
                                 new Vector3(rs * 2f, f * 0.5f, rs * 2f), slab);
                // Suelo compactado bajo todo el escudo.
                float soilR = shieldOuter + 1.5f;
                acc.AddPrimitive(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), Quaternion.identity,
                                 new Vector3(soilR * 2f, 0.03f, soilR * 2f), soil);
                // Zapatas de anclaje en el perímetro de la cúpula.
                int n = d.FootingCount();
                for (int i = 0; i < n; i++)
                {
                    float a = i * Mathf.PI * 2f / n;
                    var p = new Vector3(Mathf.Cos(a) * d.radius, footingY, Mathf.Sin(a) * d.radius);
                    acc.AddPrimitive(PrimitiveType.Cylinder, p, Quaternion.identity, new Vector3(0.5f, footingHalf, 0.5f), steel);
                }
                // BoxColliders: cuadrado inscrito en la losa + dos franjas en cruz que llegan hasta
                // las cuatro puertas (sin ellas quedaría un escalón junto al umbral).
                box.center = new Vector3(0f, f * 0.5f, 0f);
                box.size = new Vector3(rs * 1.6f, f, rs * 1.6f);
                var stripX = parent.gameObject.AddComponent<BoxCollider>();
                stripX.center = box.center;
                stripX.size = new Vector3(rs * 2f, f, MarsModuleData.DoorWidth + 0.4f);
                var stripZ = parent.gameObject.AddComponent<BoxCollider>();
                stripZ.center = box.center;
                stripZ.size = new Vector3(MarsModuleData.DoorWidth + 0.4f, f, rs * 2f);
            }
            else
            {
                Vector3 shell = d.ShellSize;
                var slabSize = new Vector3(shell.x + 2f, f, shell.z + 2f);
                acc.AddBox(new Vector3(0f, f * 0.5f, 0f), slabSize, slab);
                acc.AddBox(new Vector3(0f, 0.03f, 0f), new Vector3(shieldOuter * 2f + 3f, 0.06f, d.length + shieldOuter * 2f + 3f), soil);
                // Zapatas repartidas por el perímetro de la losa.
                int alongX = Mathf.Max(2, Mathf.RoundToInt(slabSize.x / 3f));
                int alongZ = Mathf.Max(2, Mathf.RoundToInt(slabSize.z / 3f));
                float hx = slabSize.x * 0.5f - 0.4f, hz = slabSize.z * 0.5f - 0.4f;
                for (int i = 0; i <= alongX; i++)
                {
                    float x = Mathf.Lerp(-hx, hx, (float)i / alongX);
                    acc.AddBox(new Vector3(x, footingY, -hz), new Vector3(0.5f, footingHalf * 2f, 0.5f), steel);
                    acc.AddBox(new Vector3(x, footingY, hz), new Vector3(0.5f, footingHalf * 2f, 0.5f), steel);
                }
                for (int i = 1; i < alongZ; i++)
                {
                    float z = Mathf.Lerp(-hz, hz, (float)i / alongZ);
                    acc.AddBox(new Vector3(-hx, footingY, z), new Vector3(0.5f, footingHalf * 2f, 0.5f), steel);
                    acc.AddBox(new Vector3(hx, footingY, z), new Vector3(0.5f, footingHalf * 2f, 0.5f), steel);
                }
                box.center = new Vector3(0f, f * 0.5f, 0f);
                box.size = slabSize;
            }
        }

        // ---------------- Capa 1 · Estructura esquelética ----------------

        private static void BuildSkeleton(MarsModuleData d, HabitatMeshAccumulator acc, Material ov)
        {
            float f = d.foundationThickness;
            float t = d.ribThickness;
            bool inflatable = d.skeleton == HabitatSkeletonKind.InflatableCore;
            Material rib = HabitatMaterials.Resolve(ov, inflatable ? HabitatMaterials.TensionStrap : HabitatMaterials.Titanium);
            Material ring = HabitatMaterials.Resolve(ov, HabitatMaterials.Titanium);

            switch (d.shape)
            {
                case MarsModuleShape.GeodesicDome:
                {
                    // Barras de titanio en todas las aristas de la geodésica; el anillo de base, más grueso.
                    HabitatGeometry.GeodesicMesh geo = HabitatGeometry.GeodesicHemisphere(d.geodesicFrequency);
                    var scale = new Vector3(d.radius, d.DomeHeight, d.radius);
                    var lift = new Vector3(0f, f, 0f);
                    foreach (Vector2Int e in geo.Edges)
                    {
                        Vector3 a = Vector3.Scale(geo.Vertices[e.x], scale) + lift;
                        Vector3 b = Vector3.Scale(geo.Vertices[e.y], scale) + lift;
                        bool baseRing = geo.Vertices[e.x].y == 0f && geo.Vertices[e.y].y == 0f;
                        acc.AddBeam(a, b, baseRing ? t * 2f : t, baseRing ? ring : rib);
                    }
                    // Nudos en los vértices (uniones de las barras).
                    foreach (Vector3 v in geo.Vertices)
                    {
                        if (v.y < 0f) continue;
                        acc.AddPrimitive(PrimitiveType.Sphere, Vector3.Scale(v, scale) + lift, Quaternion.identity, Vector3.one * t * 1.8f, ring);
                    }
                    break;
                }
                case MarsModuleShape.Dome:
                {
                    float r = d.radius - 0.05f; // justo por dentro de la membrana
                    float vs = d.DomeHeight / d.radius;
                    int meridians = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * d.radius / (d.ribSpacing * 3f)));
                    const int arcSteps = 10;
                    for (int m = 0; m < meridians; m++)
                    {
                        float a = m * Mathf.PI * 2f / meridians;
                        for (int s = 0; s < arcSteps; s++)
                        {
                            acc.AddBeam(DomePoint(r, a, s * 90f / arcSteps, f, vs), DomePoint(r, a, (s + 1) * 90f / arcSteps, f, vs), t, rib);
                        }
                    }
                    // Anillos horizontales; en el núcleo inflable solo el anillo de base (rígido) y el de compresión arriba.
                    float[] rings = inflatable ? new[] { 0f, 80f } : new[] { 0f, 25f, 50f, 70f };
                    foreach (float lat in rings)
                    {
                        float thick = lat == 0f ? t * (inflatable ? 4f : 1.6f) : t;
                        Ring(acc, (a) => DomePoint(r, a, lat, f, vs), 36, thick, ring);
                    }
                    break;
                }
                case MarsModuleShape.HorizontalTube:
                {
                    float r = d.radius - 0.05f, cy = f + d.radius, half = d.length * 0.5f;
                    int hoops = Mathf.Max(2, Mathf.RoundToInt(d.length / d.ribSpacing) + 1);
                    for (int h = 0; h < hoops; h++)
                    {
                        float z = Mathf.Lerp(-half, half, (float)h / (hoops - 1));
                        Ring(acc, (a) => new Vector3(Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, z), 28, t, rib);
                    }
                    // Largueros a lo largo del tubo.
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI * 2f / 6f;
                        var off = new Vector3(Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, 0f);
                        acc.AddBeam(off + Vector3.back * half, off + Vector3.forward * half, t * 0.8f, ring);
                    }
                    // Costillas de los casquetes de los extremos.
                    for (int side = -1; side <= 1; side += 2)
                    {
                        for (int i = 0; i < 6; i++)
                        {
                            float a = i * Mathf.PI * 2f / 6f;
                            for (int s = 0; s < 6; s++)
                            {
                                acc.AddBeam(CapPoint(r, a, s * 15f, cy, half * side, side), CapPoint(r, a, (s + 1) * 15f, cy, half * side, side), t, rib);
                            }
                        }
                    }
                    break;
                }
                default:
                {
                    float hw = d.width * 0.5f - 0.1f, top = f + d.height - 0.1f, half = d.length * 0.5f;
                    int frames = Mathf.Max(2, Mathf.RoundToInt(d.length / d.ribSpacing) + 1);
                    for (int i = 0; i < frames; i++)
                    {
                        float z = Mathf.Lerp(-half, half, (float)i / (frames - 1));
                        var bl = new Vector3(-hw, f, z); var br = new Vector3(hw, f, z);
                        var tl = new Vector3(-hw, top, z); var tr = new Vector3(hw, top, z);
                        var apex = new Vector3(0f, top + 0.6f, z);
                        acc.AddBeam(bl, tl, t * 1.4f, rib);       // pilares
                        acc.AddBeam(br, tr, t * 1.4f, rib);
                        acc.AddBeam(tl, apex, t, rib);            // cercha del techo
                        acc.AddBeam(apex, tr, t, rib);
                        acc.AddBeam(tl, tr, t * 0.7f, rib);       // tirante
                    }
                    foreach (Vector3 corner in new[] { new Vector3(-hw, f, 0f), new Vector3(hw, f, 0f), new Vector3(-hw, top, 0f), new Vector3(hw, top, 0f) })
                    {
                        acc.AddBeam(corner + Vector3.back * half, corner + Vector3.forward * half, t, ring);
                    }
                    break;
                }
            }
        }

        /// <summary>Punto de una cúpula semielipsoide (vScale = altura / radio).</summary>
        private static Vector3 DomePoint(float r, float azimuth, float latitudeDeg, float baseY, float vScale)
        {
            float lat = latitudeDeg * Mathf.Deg2Rad;
            float horizontal = Mathf.Cos(lat) * r;
            return new Vector3(Mathf.Cos(azimuth) * horizontal, baseY + Mathf.Sin(lat) * r * vScale, Mathf.Sin(azimuth) * horizontal);
        }

        private static Vector3 CapPoint(float r, float around, float latitudeDeg, float cy, float zBase, int side)
        {
            float lat = latitudeDeg * Mathf.Deg2Rad;
            float ring = Mathf.Cos(lat) * r;
            return new Vector3(Mathf.Cos(around) * ring, cy + Mathf.Sin(around) * ring, zBase + side * Mathf.Sin(lat) * r);
        }

        private static void Ring(HabitatMeshAccumulator acc, Func<float, Vector3> point, int segments, float thickness, Material mat)
        {
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments, a1 = (i + 1) * Mathf.PI * 2f / segments;
                acc.AddBeam(point(a0), point(a1), thickness, mat);
            }
        }

        // ---------------- Capa 2 · Contención neumática y térmica ----------------

        /// <summary>Genera la membrana (exterior + interior). Devuelve la malla exterior para la colisión.</summary>
        private static Mesh BuildPressureShell(MarsModuleData d, HabitatMeshAccumulator acc, Material ov)
        {
            bool transparent = d.shellKind == HabitatShellKind.TransparentLaminate;
            Material outerMat = HabitatMaterials.Resolve(ov, transparent ? HabitatMaterials.Glass : HabitatMaterials.KevlarOuter);
            Material innerMat = HabitatMaterials.Resolve(ov, transparent ? HabitatMaterials.Glass : HabitatMaterials.InsulationInner);
            float f = d.foundationThickness;
            float thick = Mathf.Max(0.05f, (d.kevlarLayers * d.kevlarLayerMm + d.aerogelMm) / 1000f);
            float vs = d.DomeHeight / d.radius;
            Mesh outer, inner;
            Matrix4x4 m;

            switch (d.shape)
            {
                case MarsModuleShape.GeodesicDome:
                {
                    // Un panel por triángulo, ligeramente encogido para que se vean las barras.
                    HabitatGeometry.GeodesicMesh geo = HabitatGeometry.GeodesicHemisphere(d.geodesicFrequency);
                    m = Matrix4x4.Translate(new Vector3(0f, f, 0f));
                    acc.AddGenerated(HabitatGeometry.GeodesicPanels(geo, new Vector3(d.radius, d.DomeHeight, d.radius), 0.94f), m, outerMat);
                    // La colisión es la cúpula lisa equivalente (más barata que 160 triángulos sueltos).
                    Mesh smooth = HabitatGeometry.Revolve(HabitatGeometry.QuarterCircle(d.radius, 16, vs), true, 0f, Mathf.PI * 2f, 40, false, 0f, 0);
                    Mesh collision = TransformedCopy(smooth, m, "Colision contencion");
                    HabitatGeometry.SafeDestroy(smooth);
                    return collision;
                }
                case MarsModuleShape.Dome:
                    outer = HabitatGeometry.Revolve(HabitatGeometry.QuarterCircle(d.radius, 16, vs), true, 0f, Mathf.PI * 2f, 48, false, 0f, 0);
                    inner = HabitatGeometry.Revolve(HabitatGeometry.QuarterCircle(d.radius - thick, 16, (d.DomeHeight - thick) / (d.radius - thick)), true, 0f, Mathf.PI * 2f, 48, true, 0f, 0);
                    m = Matrix4x4.Translate(new Vector3(0f, f, 0f));
                    break;
                case MarsModuleShape.HorizontalTube:
                    outer = HabitatGeometry.Revolve(HabitatGeometry.CapsuleProfile(d.radius, d.length, 8), false, 0f, Mathf.PI * 2f, 32, false, 0f, 0);
                    inner = HabitatGeometry.Revolve(HabitatGeometry.CapsuleProfile(d.radius - thick, d.length, 8), false, 0f, Mathf.PI * 2f, 32, true, 0f, 0);
                    m = Matrix4x4.Translate(new Vector3(0f, f + d.radius, 0f));
                    break;
                default:
                    Vector3 size = d.ShellSize;
                    outer = HabitatGeometry.BoxSurface(size, false);
                    inner = HabitatGeometry.BoxSurface(size - new Vector3(thick * 2f, thick, thick * 2f), true);
                    m = Matrix4x4.Translate(new Vector3(0f, f, 0f));
                    break;
            }
            acc.AddGenerated(outer, m, outerMat);
            acc.AddGenerated(inner, m, innerMat);

            // Copia de la cara exterior ya colocada, para el MeshCollider.
            return TransformedCopy(outer, m, "Colision contencion");
        }

        // ---------------- Capa 3 · Escudo de regolito ----------------

        private static Mesh BuildShield(MarsModuleData d, HabitatMeshAccumulator acc, Material ov)
        {
            bool ice = d.shieldKind == HabitatShieldKind.WaterIce;
            Material mat = HabitatMaterials.Resolve(ov, ice ? HabitatMaterials.WaterIce : HabitatMaterials.RegolithShield);
            float ri = d.ShieldInnerRadius, ro = ri + d.shieldThickness;
            // Superficie irregular de regolito apilado; el hielo va en una membrana lisa.
            float noise = ice ? 0f : Mathf.Min(0.35f, d.shieldThickness * 0.12f);
            int seed = d.moduleId.Length * 131 + (int)d.type * 17;
            Mesh outer, inner;

            if (d.IsDomeLike)
            {
                // Cúpula gruesa que sigue la altura de la contención (el regolito, algo más achatado).
                float hi = d.ShieldInnerHeight, ho = hi + d.shieldThickness * (ice ? 1f : 0.92f);
                outer = HabitatGeometry.Revolve(HabitatGeometry.QuarterCircle(ro, 14, ho / ro), true, 0f, Mathf.PI * 2f, 56, false, noise, seed);
                inner = HabitatGeometry.Revolve(HabitatGeometry.QuarterCircle(ri, 14, hi / ri), true, 0f, Mathf.PI * 2f, 56, true, 0f, seed);
            }
            else
            {
                // Bóveda: media cápsula con el eje a ras de suelo, cubre el tubo o la nave entera.
                outer = HabitatGeometry.Revolve(HabitatGeometry.CapsuleProfile(ro, d.length, 8), false, 0f, Mathf.PI, 32, false, noise, seed);
                inner = HabitatGeometry.Revolve(HabitatGeometry.CapsuleProfile(ri, d.length, 8), false, 0f, Mathf.PI, 32, true, 0f, seed);
            }
            acc.AddGenerated(outer, Matrix4x4.identity, mat);
            acc.AddGenerated(inner, Matrix4x4.identity, mat);
            return TransformedCopy(outer, Matrix4x4.identity, "Colision escudo");
        }

        private static Mesh TransformedCopy(Mesh source, Matrix4x4 m, string name)
        {
            var copy = new Mesh { name = name };
            copy.indexFormat = source.indexFormat;
            copy.CombineMeshes(new[] { new CombineInstance { mesh = source, transform = m } }, true, true);
            copy.RecalculateBounds();
            return copy;
        }

        // ---------------- Holograma de colocación ----------------

        /// <summary>
        /// Holograma del módulo para previsualizar dónde se construirá: el ghostPrefab del módulo o,
        /// si no hay, la cimentación + (barras geodésicas) + escudo generados por código. Sin
        /// colisiones (no estorba a las comprobaciones de espacio) y con un único material translúcido.
        /// No cuelga de _Structures ni tiene controlador: no es una estructura.
        /// </summary>
        public static GameObject BuildGhost(MarsModuleData d, Material ghostMaterial)
        {
            if (d == null) return null;
            GameObject root;
            if (d.ghostPrefab != null)
            {
                root = Instantiate(d.ghostPrefab);
                root.name = "Holograma " + d.displayName;
            }
            else
            {
                root = new GameObject("Holograma " + d.displayName);
                var layers = new List<HabitatLayer> { HabitatLayer.Foundation, HabitatLayer.RegolithShield };
                if (d.shape == MarsModuleShape.GeodesicDome) layers.Add(HabitatLayer.Skeleton);
                foreach (HabitatLayer layer in layers)
                {
                    var go = new GameObject(HabitatLayerNode.NodeName(layer));
                    go.transform.SetParent(root.transform, false);
                    var node = go.AddComponent<HabitatLayerNode>();
                    node.layer = layer;
                    GenerateLayer(d, node, null);
                    HabitatGeometry.SafeDestroy(node); // el holograma no es una capa real
                }
            }

            foreach (Collider c in root.GetComponentsInChildren<Collider>(true))
            {
                c.enabled = false;                 // fuera ya en este frame
                HabitatGeometry.SafeDestroy(c);
            }
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (ghostMaterial != null) r.sharedMaterial = ghostMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            return root;
        }

        // ---------------- Pasillo presurizado entre módulos ----------------

        /// <summary>Radio interior del tubo de los pasillos y altura de su eje sobre el suelo del pasillo (m).</summary>
        public const float CorridorRadius = 1.3f, CorridorAxisHeight = 1.05f;

        /// <summary>
        /// Pasillo presurizado TRANSITABLE entre dos umbrales de puerta (puntos del suelo, en el mundo):
        /// tubo de Kevlar con aislamiento por dentro, costillas, suelo plano, relleno de regolito
        /// debajo y bóveda de regolito encima. Puede subir o bajar si los suelos están a distinta altura.
        /// Colisión: suelo, paredes y techo por separado (se camina por dentro; los extremos quedan abiertos).
        /// Se coloca bajo _Structures.
        /// </summary>
        public static GameObject BuildCorridor(Vector3 from, Vector3 to)
        {
            Vector3 axis = to - from;
            float len = axis.magnitude;
            if (len < 0.5f) return null;

            Transform structures = SceneRoots.Get(SceneRoot.Structures);
            var root = new GameObject(UniqueName(structures, "Pasillo presurizado"));
            root.transform.SetParent(structures, false);
            // Origen en el punto medio del suelo; +Z a lo largo del pasillo (con su pendiente), +Y hacia "arriba" del pasillo.
            root.transform.SetPositionAndRotation((from + to) * 0.5f, Quaternion.LookRotation(axis / len, Vector3.up));

            const float r = CorridorRadius, cy = CorridorAxisHeight;
            float half = len * 0.5f;
            var acc = new HabitatMeshAccumulator();
            var tube = new List<Vector2> { new Vector2(r, -half), new Vector2(r, half) };
            var tubeIn = new List<Vector2> { new Vector2(r - 0.06f, -half), new Vector2(r - 0.06f, half) };
            Matrix4x4 axisM = Matrix4x4.Translate(new Vector3(0f, cy, 0f));

            // Tubo (cara exterior de Kevlar e interior aislada), abierto por los extremos.
            acc.AddGenerated(HabitatGeometry.Revolve(tube, false, 0f, Mathf.PI * 2f, 24, false, 0f, 0), axisM, HabitatMaterials.KevlarOuter);
            acc.AddGenerated(HabitatGeometry.Revolve(tubeIn, false, 0f, Mathf.PI * 2f, 24, true, 0f, 0), axisM, HabitatMaterials.InsulationInner);
            // Costillas cada 1,5 m.
            int ribs = Mathf.Max(2, Mathf.RoundToInt(len / 1.5f) + 1);
            for (int i = 0; i < ribs; i++)
            {
                float z = Mathf.Lerp(-half + 0.1f, half - 0.1f, (float)i / (ribs - 1));
                Ring(acc, (a) => new Vector3(Mathf.Cos(a) * (r + 0.04f), cy + Mathf.Sin(a) * (r + 0.04f), z), 16, 0.07f, HabitatMaterials.Titanium);
            }
            // Suelo plano (rejilla) y relleno de regolito sinterizado debajo del tubo.
            acc.AddBox(new Vector3(0f, -0.06f, 0f), new Vector3(2f, 0.12f, len), HabitatMaterials.RackFrame);
            acc.AddBox(new Vector3(0f, -1.2f, 0f), new Vector3(r * 2f + 0.8f, 2f, len), HabitatMaterials.SinteredSlab);
            // Bóveda de regolito (1 m) por encima y a los lados; los extremos se meten en los escudos.
            float vIn = r + 0.5f, vOut = vIn + 1f;
            var vault = new List<Vector2> { new Vector2(vOut, -half), new Vector2(vOut, half) };
            var vaultIn = new List<Vector2> { new Vector2(vIn, -half), new Vector2(vIn, half) };
            const float below = 0.35f; // la bóveda baja un poco por debajo del eje hasta el relleno
            acc.AddGenerated(HabitatGeometry.Revolve(vault, false, -below, Mathf.PI + below, 20, false, 0.12f, 7), axisM, HabitatMaterials.RegolithShield);
            acc.AddGenerated(HabitatGeometry.Revolve(vaultIn, false, -below, Mathf.PI + below, 20, true, 0f, 7), axisM, HabitatMaterials.RegolithShield);
            acc.Build(root.transform, "Pasillo");

            // Colisiones: suelo, paredes y techo (el interior queda libre para caminar).
            AddBoxCollider(root, new Vector3(0f, -0.15f, 0f), new Vector3(2.4f, 0.3f, len));
            AddBoxCollider(root, new Vector3(r + 0.1f, cy, 0f), new Vector3(0.2f, r * 2f, len));
            AddBoxCollider(root, new Vector3(-r - 0.1f, cy, 0f), new Vector3(0.2f, r * 2f, len));
            AddBoxCollider(root, new Vector3(0f, cy + r + 0.1f, 0f), new Vector3(r * 2f, 0.2f, len));
            // Por fuera: lomo de la bóveda (para que el robot pueda pasar por encima sin atravesarla).
            AddBoxCollider(root, new Vector3(0f, cy + vOut - 0.3f, 0f), new Vector3(vOut * 1.4f, 0.6f, len));
            return root;
        }

        private static void AddBoxCollider(GameObject go, Vector3 center, Vector3 size)
        {
            var col = go.AddComponent<BoxCollider>();
            col.center = center;
            col.size = size;
        }

        // ---------------- Capa 4 · Sistemas internos ----------------

        private static void BuildInterior(MarsModuleData d, HabitatMeshAccumulator acc, Transform parent, Material ov)
        {
            var colliders = new List<Bounds>();
            switch (d.type)
            {
                case MarsModuleType.CropDome: CropRacks(d, acc, colliders, ov); break;
                case MarsModuleType.CrewHabitat: CrewQuarters(d, acc, colliders, ov); break;
                case MarsModuleType.IsruPlant: IsruEquipment(d, acc, colliders, ov); break;
                default: WorkshopEquipment(d, acc, colliders, ov); break;
            }
            foreach (Bounds b in colliders)
            {
                var col = parent.gameObject.AddComponent<BoxCollider>();
                col.center = b.center;
                col.size = b.size;
            }
        }

        private static void CropRacks(MarsModuleData d, HabitatMeshAccumulator acc, List<Bounds> cols, Material ov)
        {
            Material frame = HabitatMaterials.Resolve(ov, HabitatMaterials.RackFrame);
            Material plants = HabitatMaterials.Resolve(ov, HabitatMaterials.Plants);
            Material led = HabitatMaterials.Resolve(ov, HabitatMaterials.GrowLight);
            float f = d.foundationThickness;
            const float rackH = 2.4f, rackW = 0.6f, unitLen = 2.4f;
            // Solo donde la cúpula deja al menos 2,6 m de altura libre.
            float hRatio = Mathf.Clamp01(2.6f / Mathf.Max(0.1f, d.DomeHeight));
            float usable = d.radius * Mathf.Sqrt(1f - hRatio * hRatio) - 0.5f;

            // Pasillos en cruz (Z y X): unen las cuatro puertas pasando por el centro.
            const float aisleHalf = 0.8f;
            acc.AddBox(new Vector3(0f, f + 0.02f, 0f), new Vector3(1.4f, 0.04f, usable * 2f), frame);
            acc.AddBox(new Vector3(0f, f + 0.02f, 0f), new Vector3(usable * 2f, 0.04f, 1.4f), frame);

            for (float x = -usable + 0.6f; x <= usable - 0.6f; x += 1.8f)
            {
                if (Mathf.Abs(x) < 1.1f) continue; // deja libre el pasillo Z
                float zMax = Mathf.Sqrt(Mathf.Max(0f, usable * usable - x * x));
                int perSide = Mathf.FloorToInt((zMax - aisleHalf) / unitLen);
                if (perSide < 1) continue;
                for (int side = -1; side <= 1; side += 2)
                {
                    float rowLen = perSide * unitLen;
                    cols.Add(new Bounds(new Vector3(x, f + rackH * 0.5f, side * (aisleHalf + rowLen * 0.5f)), new Vector3(rackW, rackH, rowLen)));
                }
                for (int k = 0; k < perSide * 2; k++)
                {
                    int side = k < perSide ? -1 : 1;
                    int u = k % perSide;
                    float z = side * (aisleHalf + (u + 0.5f) * unitLen); // deja libre el pasillo X
                    var c = new Vector3(x, f, z);
                    // Postes.
                    for (int px = -1; px <= 1; px += 2)
                        for (int pz = -1; pz <= 1; pz += 2)
                            acc.AddBox(c + new Vector3(px * rackW * 0.45f, rackH * 0.5f, pz * (unitLen * 0.5f - 0.05f)), new Vector3(0.04f, rackH, 0.04f), frame);
                    // Tres bandejas con cultivo y su barra LED encima.
                    foreach (float shelf in new[] { 0.45f, 1.15f, 1.85f })
                    {
                        acc.AddBox(c + new Vector3(0f, shelf, 0f), new Vector3(rackW, 0.04f, unitLen - 0.1f), frame);
                        acc.AddBox(c + new Vector3(0f, shelf + 0.13f, 0f), new Vector3(rackW * 0.9f, 0.2f, unitLen - 0.2f), plants);
                        acc.AddBox(c + new Vector3(0f, shelf + 0.62f, 0f), new Vector3(0.1f, 0.03f, unitLen - 0.3f), led);
                    }
                }
            }
        }

        private static void CrewQuarters(MarsModuleData d, HabitatMeshAccumulator acc, List<Bounds> cols, Material ov)
        {
            Material frame = HabitatMaterials.Resolve(ov, HabitatMaterials.RackFrame);
            Material furn = HabitatMaterials.Resolve(ov, HabitatMaterials.Furniture);
            Material fabric = HabitatMaterials.Resolve(ov, HabitatMaterials.Fabric);
            Material light = HabitatMaterials.Resolve(ov, HabitatMaterials.StatusLight);
            float f = d.foundationThickness, r = d.radius, half = d.length * 0.5f;
            // Suelo plano dentro del tubo, a un 35 % del diámetro.
            float deckY = f + r * 0.35f;
            float halfChord = Mathf.Sqrt(Mathf.Max(0.1f, r * r - (r - r * 0.35f) * (r - r * 0.35f))) * 0.95f;
            // El suelo entra en los casquetes hasta las puertas de los extremos (si no, habría un hueco).
            float deckLen = d.length + 2f * r * 0.74f;
            acc.AddBox(new Vector3(0f, deckY - 0.05f, 0f), new Vector3(halfChord * 2f, 0.1f, deckLen), frame);
            cols.Add(new Bounds(new Vector3(0f, deckY - 0.05f, 0f), new Vector3(halfChord * 2f, 0.1f, deckLen)));

            float wallX = Mathf.Min(halfChord, r * 0.85f) - 0.55f;
            // Literas dobles en el lado +X.
            for (int i = 0; i < 3; i++)
            {
                float z = -half + 1.6f + i * 2.3f;
                for (int level = 0; level < 2; level++)
                {
                    float y = deckY + 0.4f + level * 1.0f;
                    acc.AddBox(new Vector3(wallX, y, z), new Vector3(0.9f, 0.08f, 2f), frame);
                    acc.AddBox(new Vector3(wallX, y + 0.1f, z), new Vector3(0.85f, 0.14f, 1.9f), fabric);
                }
                cols.Add(new Bounds(new Vector3(wallX, deckY + 0.8f, z), new Vector3(0.9f, 1.6f, 2f)));
            }
            // Taquillas en el lado −X.
            for (int i = 0; i < 4; i++)
            {
                float z = -half + 1.2f + i * 0.9f;
                acc.AddBox(new Vector3(-wallX, deckY + 0.9f, z), new Vector3(0.6f, 1.8f, 0.8f), furn);
            }
            cols.Add(new Bounds(new Vector3(-wallX, deckY + 0.9f, -half + 2.55f), new Vector3(0.6f, 1.8f, 3.6f)));
            // Mesa y sillas hacia el lado −X (el pasillo central de puerta a puerta queda libre).
            float tz = half - 3.5f, tx = -1.0f;
            acc.AddBox(new Vector3(tx, deckY + 0.75f, tz), new Vector3(1f, 0.05f, 1.8f), furn);
            acc.AddBox(new Vector3(tx, deckY + 0.37f, tz), new Vector3(0.1f, 0.74f, 0.1f), frame);
            for (int s = -1; s <= 1; s += 2)
            {
                acc.AddBox(new Vector3(tx + s * 0.8f, deckY + 0.45f, tz - 0.5f), new Vector3(0.45f, 0.06f, 0.45f), fabric);
                acc.AddBox(new Vector3(tx + s * 0.8f, deckY + 0.45f, tz + 0.5f), new Vector3(0.45f, 0.06f, 0.45f), fabric);
            }
            cols.Add(new Bounds(new Vector3(tx, deckY + 0.4f, tz), new Vector3(2.2f, 0.8f, 1.9f)));
            // Cocina al fondo.
            acc.AddBox(new Vector3(-wallX + 0.1f, deckY + 0.45f, half - 1f), new Vector3(0.7f, 0.9f, 1.8f), furn);
            cols.Add(new Bounds(new Vector3(-wallX + 0.1f, deckY + 0.45f, half - 1f), new Vector3(0.7f, 0.9f, 1.8f)));
            // Tira de luz en el techo.
            acc.AddBox(new Vector3(0f, f + r * 1.85f, 0f), new Vector3(0.15f, 0.03f, d.length * 0.9f), light);
        }

        private static void IsruEquipment(MarsModuleData d, HabitatMeshAccumulator acc, List<Bounds> cols, Material ov)
        {
            Material tank = HabitatMaterials.Resolve(ov, HabitatMaterials.Tank);
            Material pipe = HabitatMaterials.Resolve(ov, HabitatMaterials.Pipe);
            Material machine = HabitatMaterials.Resolve(ov, HabitatMaterials.Machine);
            Material orange = HabitatMaterials.Resolve(ov, HabitatMaterials.SafetyOrange);
            Material light = HabitatMaterials.Resolve(ov, HabitatMaterials.StatusLight);
            float f = d.foundationThickness, hx = d.width * 0.5f, hz = d.length * 0.5f;
            float tankH = Mathf.Min(3.5f, d.height - 1f);

            // Tres tanques verticales (H₂O, O₂, CH₄) a lo largo de la pared −X.
            var tankTops = new List<Vector3>();
            for (int i = 0; i < 3; i++)
            {
                var p = new Vector3(-hx + 1.4f, f + tankH * 0.5f, -hz + 2f + i * 2.6f);
                acc.AddPrimitive(PrimitiveType.Cylinder, p, Quaternion.identity, new Vector3(2f, tankH * 0.5f, 2f), tank);
                acc.AddPrimitive(PrimitiveType.Sphere, p + Vector3.up * tankH * 0.5f, Quaternion.identity, new Vector3(2f, 0.8f, 2f), tank);
                tankTops.Add(p + Vector3.up * (tankH * 0.5f + 0.3f));
                cols.Add(new Bounds(p, new Vector3(2f, tankH, 2f)));
            }
            // Reactor Sabatier (cilindro horizontal) y electrolizador.
            var reactor = new Vector3(hx - 1.6f, f + 1.1f, -hz + 3f);
            acc.AddPrimitive(PrimitiveType.Cylinder, reactor, Quaternion.Euler(90f, 0f, 0f), new Vector3(1.6f, 1.6f, 1.6f), machine);
            cols.Add(new Bounds(reactor, new Vector3(1.6f, 1.6f, 3.2f)));
            var electrolyzer = new Vector3(hx - 1.4f, f + 1f, hz - 3f);
            acc.AddBox(electrolyzer, new Vector3(1.8f, 2f, 2.4f), machine);
            acc.AddBox(electrolyzer + new Vector3(-0.91f, 0.5f, 0f), new Vector3(0.02f, 0.2f, 1.6f), light);
            cols.Add(new Bounds(electrolyzer, new Vector3(1.8f, 2f, 2.4f)));
            // Tolva de regolito (para extraer agua del hielo/hidratados).
            var hopper = new Vector3(-hx + 1.5f, f + 1.2f, hz - 2f); // junto a la pared −X: el pasillo central queda libre
            acc.AddBox(hopper, new Vector3(2.2f, 2.4f, 2f), orange);
            cols.Add(new Bounds(hopper, new Vector3(2.2f, 2.4f, 2f)));
            // Tuberías: tanques → colector → reactor y electrolizador.
            float pipeY = f + tankH + 0.6f;
            var manifoldA = new Vector3(-hx + 1.4f, pipeY, -hz + 1f);
            var manifoldB = new Vector3(-hx + 1.4f, pipeY, -hz + 2f + 2 * 2.6f + 1f);
            acc.AddBeam(manifoldA, manifoldB, 0.18f, pipe);
            foreach (Vector3 top in tankTops) acc.AddBeam(top, new Vector3(top.x, pipeY, top.z), 0.14f, pipe);
            acc.AddBeam(manifoldA, new Vector3(reactor.x, pipeY, manifoldA.z), 0.16f, pipe);
            acc.AddBeam(new Vector3(reactor.x, pipeY, manifoldA.z), reactor + Vector3.up * 0.8f, 0.16f, pipe);
            acc.AddBeam(manifoldB, new Vector3(electrolyzer.x, pipeY, manifoldB.z), 0.16f, pipe);
            acc.AddBeam(new Vector3(electrolyzer.x, pipeY, manifoldB.z), electrolyzer + Vector3.up * 1f, 0.16f, pipe);
        }

        private static void WorkshopEquipment(MarsModuleData d, HabitatMeshAccumulator acc, List<Bounds> cols, Material ov)
        {
            Material frame = HabitatMaterials.Resolve(ov, HabitatMaterials.RackFrame);
            Material machine = HabitatMaterials.Resolve(ov, HabitatMaterials.Machine);
            Material orange = HabitatMaterials.Resolve(ov, HabitatMaterials.SafetyOrange);
            Material furn = HabitatMaterials.Resolve(ov, HabitatMaterials.Furniture);
            Material light = HabitatMaterials.Resolve(ov, HabitatMaterials.StatusLight);
            float f = d.foundationThickness, hx = d.width * 0.5f, hz = d.length * 0.5f;

            // Bancos de trabajo a lo largo de las paredes laterales.
            for (int side = -1; side <= 1; side += 2)
            {
                for (int i = 0; i < 2; i++)
                {
                    var c = new Vector3(side * (hx - 0.6f), f, -hz + 2f + i * 3.2f);
                    acc.AddBox(c + Vector3.up * 0.9f, new Vector3(0.9f, 0.06f, 2.6f), furn);
                    for (int lx = -1; lx <= 1; lx += 2)
                        for (int lz = -1; lz <= 1; lz += 2)
                            acc.AddBox(c + new Vector3(lx * 0.38f, 0.45f, lz * 1.2f), new Vector3(0.06f, 0.9f, 0.06f), frame);
                    // Panel de herramientas sobre el banco.
                    acc.AddBox(c + new Vector3(side * 0.4f, 1.7f, 0f), new Vector3(0.04f, 1f, 2.4f), machine);
                    cols.Add(new Bounds(c + Vector3.up * 0.47f, new Vector3(0.9f, 0.95f, 2.6f)));
                }
            }
            // Plataforma de carga para robots, con anillo luminoso.
            var pad = new Vector3(0f, f + 0.05f, hz - 3f);
            acc.AddPrimitive(PrimitiveType.Cylinder, pad, Quaternion.identity, new Vector3(3f, 0.05f, 3f), machine);
            acc.AddPrimitive(PrimitiveType.Cylinder, pad + Vector3.up * 0.06f, Quaternion.identity, new Vector3(2.6f, 0.01f, 2.6f), light);
            // Brazo robótico de taller.
            var armBase = new Vector3(2.2f, f, -1f); // a un lado del pasillo central (de puerta a puerta)
            acc.AddPrimitive(PrimitiveType.Cylinder, armBase + Vector3.up * 0.3f, Quaternion.identity, new Vector3(0.8f, 0.3f, 0.8f), orange);
            Vector3 shoulder = armBase + Vector3.up * 0.7f, elbow = shoulder + new Vector3(0.3f, 1.2f, 0.4f), wrist = elbow + new Vector3(0.2f, -0.4f, 0.9f);
            acc.AddBeam(shoulder, elbow, 0.22f, orange);
            acc.AddBeam(elbow, wrist, 0.16f, orange);
            acc.AddPrimitive(PrimitiveType.Sphere, elbow, Quaternion.identity, Vector3.one * 0.3f, machine);
            acc.AddBox(wrist, new Vector3(0.25f, 0.12f, 0.25f), machine);
            cols.Add(new Bounds(armBase + Vector3.up * 0.3f, new Vector3(0.8f, 0.6f, 0.8f)));
            // Estanterías al fondo.
            for (int i = -1; i <= 1; i += 2) // dos estanterías en las esquinas: la puerta trasera queda libre
            {
                var shelf = new Vector3(i * (hx - 2.2f), f + 1.1f, -hz + 0.5f);
                acc.AddBox(shelf, new Vector3(2f, 2.2f, 0.6f), frame);
                cols.Add(new Bounds(shelf, new Vector3(2f, 2.2f, 0.6f)));
            }
        }
    }
}
