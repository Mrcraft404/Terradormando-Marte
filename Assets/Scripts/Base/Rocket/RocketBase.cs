using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Base inicial: el cohete que trajo al robot, aterrizado en el punto de inicio, con sus
    /// máquinas alrededor y el almacén con la carga inicial.
    ///
    /// Distribución (vista desde arriba; el robot aparece en la plataforma y mira al cohete):
    ///
    ///        Fabricador    COHETE    Taller de modificaciones
    ///                        |
    ///                Plataforma de carga (aparición del robot)
    ///
    /// Los modelos son PROVISIONALES (formas básicas de Unity). Para poner modelos definitivos
    /// basta con sustituir los métodos Build* manteniendo los componentes de cada máquina.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RocketBase : MonoBehaviour
    {
        /// <summary>Radio aproximado de la base (para mostrar el almacén al estar cerca).</summary>
        public const float BaseRadius = 25f;

        private readonly List<BaseStation> _stations = new List<BaseStation>();
        private TerrainManager _terrain;

        /// <summary>Distancia máxima al cohete para construir (alcance de la red de energía).</summary>
        public const float BuildRadius = 80f;

        public BaseInventory Inventory { get; private set; }
        public BaseGrid Grid { get; private set; }
        public BaseResearch Research { get; private set; }

        /// <summary>Zona de aterrizaje (para saber cuánto hielo hay, etc.). La asigna el spawner.</summary>
        public MarsRobot.Spawns.LandingZoneData Zone { get; set; }

        // ------------------------------------------------------------------
        // Estructuras construidas
        // ------------------------------------------------------------------

        /// <summary>
        /// Coloca una estructura ya pagada. La altura se ajusta al terreno.
        /// </summary>
        public PlacedStructure PlaceStructure(StructureDefinition def, Vector3 position, Quaternion rotation)
        {
            return PlaceStructure<PlacedStructure>(def, position, rotation);
        }

        /// <summary>Igual, pero con un comportamiento propio (p. ej. la refinería ISRU).</summary>
        public T PlaceStructure<T>(StructureDefinition def, Vector3 position, Quaternion rotation) where T : PlacedStructure
        {
            var go = new GameObject(def.name);
            go.transform.SetParent(transform, true);
            go.transform.SetPositionAndRotation(Ground(position), rotation);

            StructureModels.Build(def, go.transform, null);
            if (def.solid)
            {
                var box = go.AddComponent<BoxCollider>();
                box.center = new Vector3(0f, def.size.y * 0.5f, 0f);
                box.size = def.size;
            }

            var placed = go.AddComponent<T>();
            go.AddComponent<MarsRobot.UI.Minimap.MinimapObjectMarker>().Setup(
                def.kind == StructureKind.CommBeacon ? MarsRobot.UI.Minimap.MinimapMarkerType.Beacon : MarsRobot.UI.Minimap.MinimapMarkerType.Structure,
                false, def.name);
            placed.Setup(this, def);
            _stations.Add(placed);
            Grid.Register(placed);
            if (def.storageBonus.amount > 0) Inventory.AddCapacity(def.storageBonus.type, def.storageBonus.amount);
            return placed;
        }

        /// <summary>Desmonta una estructura y devuelve la mitad de su coste al almacén.</summary>
        public void Dismantle(PlacedStructure s)
        {
            if (s == null) return;
            StructureDefinition def = s.Definition;
            for (int i = 0; i < def.cost.Length; i++)
            {
                Inventory.Add(def.cost[i].type, def.cost[i].amount / 2);
            }
            if (def.storageBonus.amount > 0) Inventory.AddCapacity(def.storageBonus.type, -def.storageBonus.amount);
            _stations.Remove(s);
            Grid.Unregister(s);
            Destroy(s.gameObject);
        }

        public IReadOnlyList<BaseStation> Stations
        {
            get { return _stations; }
        }

        /// <summary>Dónde aparece el robot (sobre la plataforma de carga).</summary>
        public Vector3 RobotSpawnPosition { get; private set; }

        /// <summary>Orientación inicial del robot (mirando al cohete).</summary>
        public Quaternion RobotSpawnRotation { get; private set; }

        private void Awake()
        {
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        // ------------------------------------------------------------------
        // Creación
        // ------------------------------------------------------------------

        /// <summary>
        /// Crea la base centrada en <paramref name="center"/> (posición mundial; la altura se toma
        /// del terreno). <paramref name="facing"/> es hacia dónde mira la plataforma de carga.
        /// </summary>
        public static RocketBase Create(TerrainManager terrain, Vector3 center, Vector3 facing)
        {
            var root = new GameObject("Base (cohete)");
            facing.y = 0f;
            if (facing.sqrMagnitude < 0.01f) facing = Vector3.forward;
            root.transform.SetPositionAndRotation(center, Quaternion.LookRotation(facing.normalized, Vector3.up));

            var rb = root.AddComponent<RocketBase>();
            rb._terrain = terrain;
            rb.Inventory = root.AddComponent<BaseInventory>();
            rb.Grid = root.AddComponent<BaseGrid>();
            rb.Research = root.AddComponent<BaseResearch>();
            rb.LoadInitialCargo();
            rb.BuildAll();
            // Sistemas del cohete nodriza: bodega, terminal, fundición, refinería y berma de blindaje.
            root.AddComponent<RocketBaseManager>().Build(rb);
            return rb;
        }

        /// <summary>Lo que traía el cohete: lo justo para la primera mejora y unas piezas.</summary>
        private void LoadInitialCargo()
        {
            Inventory.Add(ResourceType.Iron, 20);
            Inventory.Add(ResourceType.Silicon, 12);
            Inventory.Add(ResourceType.Regolith, 15);
            Inventory.Add(ResourceType.Ice, 6);
            Inventory.Add(ResourceType.MetalParts, 4);
            Inventory.Add(ResourceType.Wiring, 4);
            Inventory.Add(ResourceType.SolarCells, 2);
        }

        private void BuildAll()
        {
            // Materiales provisionales.
            Material white = Mat(new Color(0.9f, 0.9f, 0.88f), Color.black);
            Material dark = Mat(new Color(0.18f, 0.18f, 0.2f), Color.black);
            Material orange = Mat(new Color(0.95f, 0.5f, 0.1f), Color.black);
            Material metal = Mat(new Color(0.55f, 0.56f, 0.58f), Color.black);
            Material glowYellow = Mat(new Color(1f, 0.85f, 0.2f), new Color(1f, 0.75f, 0.1f) * 1.5f);
            Material glowCyan = Mat(new Color(0.2f, 0.9f, 1f), new Color(0.1f, 0.7f, 1f) * 1.5f);

            // Choques suaves con la base: no queremos que aparcar destroce el robot.
            var hazard = gameObject.AddComponent<SurfaceHazard>();
            hazard.impactMultiplier = 0.1f;

            // Cohete nodriza de 52,5 m (casco de acero, 5 niveles interiores, ascensor del robot).
            // Inspección con cámara libre: tecla K. Ver Rocket/Ship.
            Ship.ShipModelBuilder.Build(transform);
            BuildLandingPad(Vector3.zero, dark);

            var pad = AddStation<ChargingPad>("Plataforma de carga", new Vector3(0f, 0f, 14f), 2.2f);
            BuildChargingPad(pad.transform, metal, glowYellow);

            var fab = AddStation<FabricatorStation>("Fabricador", new Vector3(-12f, 0f, 2f), 4f);
            BuildFabricator(fab.transform, metal, orange, glowCyan);

            var shop = AddStation<RobotAssemblyBay>("Taller de ensamblaje", new Vector3(12f, 0f, 2f), 4f);
            BuildWorkshop(shop.transform, metal, orange, glowCyan);

            // El robot aparece sobre la plataforma de carga, mirando al cohete.
            RobotSpawnPosition = pad.transform.position;
            Vector3 toRocket = transform.position - pad.transform.position;
            toRocket.y = 0f;
            RobotSpawnRotation = Quaternion.LookRotation(toRocket.normalized, Vector3.up);

            // Luz de la base (se ve de noche y ayuda a volver).
            var lightGo = new GameObject("Luz de la base");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 8f, 9f);
            var light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 30f;
            light.intensity = 1.2f;
            light.color = new Color(1f, 0.9f, 0.75f);
        }

        /// <summary>Crea una máquina como hija de la base, apoyada en el terreno.</summary>
        public T AddStation<T>(string name, Vector3 localPos, float radius) where T : BaseStation
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = Ground(transform.TransformPoint(localPos));
            go.transform.localRotation = Quaternion.identity;
            var station = go.AddComponent<T>();
            station.Init(this, name, radius);
            _stations.Add(station);
            return station;
        }

        // ------------------------------------------------------------------
        // Modelos provisionales
        // ------------------------------------------------------------------

        private void BuildRocket(Vector3 localPos, Material body, Material dark, Material accent)
        {
            var rocket = new GameObject("Cohete").transform;
            rocket.SetParent(transform, false);
            rocket.position = Ground(transform.TransformPoint(localPos));

            // Fuselaje (con colisionador) a 2 m del suelo, sobre las patas.
            Part(PrimitiveType.Cylinder, rocket, new Vector3(0f, 10f, 0f), new Vector3(3.6f, 8f, 3.6f), body, true);
            Part(PrimitiveType.Cylinder, rocket, new Vector3(0f, 14.5f, 0f), new Vector3(3.65f, 0.4f, 3.65f), accent, false); // franja
            // Morro: conos aproximados con cilindros cada vez más estrechos.
            Part(PrimitiveType.Cylinder, rocket, new Vector3(0f, 18.6f, 0f), new Vector3(3.0f, 0.6f, 3.0f), body, false);
            Part(PrimitiveType.Cylinder, rocket, new Vector3(0f, 19.6f, 0f), new Vector3(2.2f, 0.5f, 2.2f), body, false);
            Part(PrimitiveType.Sphere, rocket, new Vector3(0f, 20.4f, 0f), new Vector3(1.6f, 1.8f, 1.6f), accent, false);
            // Motor y toberas.
            Part(PrimitiveType.Cylinder, rocket, new Vector3(0f, 1.6f, 0f), new Vector3(2.4f, 0.5f, 2.4f), dark, false);
            // Patas de aterrizaje (4) y escotilla de carga.
            for (int i = 0; i < 4; i++)
            {
                Quaternion rot = Quaternion.Euler(0f, 45f + 90f * i, 0f);
                Vector3 outward = rot * Vector3.forward;
                var leg = Part(PrimitiveType.Cube, rocket, outward * 2.4f + Vector3.up * 1.4f, new Vector3(0.3f, 3.6f, 0.3f), dark, false);
                leg.localRotation = rot * Quaternion.Euler(-30f, 0f, 0f); // pie hacia fuera, extremo superior hacia el fuselaje
                Part(PrimitiveType.Cylinder, rocket, outward * 3.3f + Vector3.up * 0.05f, new Vector3(1.1f, 0.1f, 1.1f), dark, false); // zapata
            }
            Part(PrimitiveType.Cube, rocket, new Vector3(0f, 4.5f, 1.82f), new Vector3(1.6f, 2.2f, 0.1f), accent, false); // escotilla
            // Rampa de la escotilla hasta el suelo.
            // Baja desde el pie de la escotilla (y≈3,4) hasta el suelo con 30° de inclinación.
            var ramp = Part(PrimitiveType.Cube, rocket, new Vector3(0f, 1.7f, 4.85f), new Vector3(1.6f, 0.1f, 6.8f), dark, false);
            ramp.localRotation = Quaternion.Euler(30f, 0f, 0f);
        }

        private void BuildLandingPad(Vector3 localPos, Material mat)
        {
            // Mancha quemada/aplanada bajo el cohete (solo visual, sin colisión: no crea escalones).
            var pad = Part(PrimitiveType.Cylinder, transform, localPos, new Vector3(24f, 0.02f, 24f), mat, false);
            pad.position = Ground(pad.position) + Vector3.up * 0.03f;
        }

        private static void BuildChargingPad(Transform station, Material metal, Material glow)
        {
            Part(PrimitiveType.Cylinder, station, new Vector3(0f, 0.04f, 0f), new Vector3(4f, 0.04f, 4f), metal, false);
            Part(PrimitiveType.Cylinder, station, new Vector3(0f, 0.06f, 0f), new Vector3(3.2f, 0.03f, 3.2f), glow, false);
            Part(PrimitiveType.Cylinder, station, new Vector3(0f, 0.08f, 0f), new Vector3(2.6f, 0.03f, 2.6f), metal, false);
            // Poste con el cable de carga.
            Part(PrimitiveType.Cube, station, new Vector3(2.4f, 1f, 0f), new Vector3(0.3f, 2f, 0.3f), metal, true);
            Part(PrimitiveType.Cube, station, new Vector3(2.4f, 2.05f, 0f), new Vector3(0.5f, 0.15f, 0.5f), glow, false);
        }

        private static void BuildFabricator(Transform station, Material metal, Material accent, Material glow)
        {
            Part(PrimitiveType.Cube, station, new Vector3(0f, 1f, 0f), new Vector3(2.6f, 2f, 2f), metal, true);
            Part(PrimitiveType.Cube, station, new Vector3(0f, 2.1f, 0f), new Vector3(2.7f, 0.2f, 2.1f), accent, false);
            Part(PrimitiveType.Cube, station, new Vector3(0f, 1.1f, 1.01f), new Vector3(1.4f, 0.9f, 0.05f), glow, false); // ventana de impresión
            Part(PrimitiveType.Cylinder, station, new Vector3(-1.6f, 0.9f, 0f), new Vector3(0.9f, 0.9f, 0.9f), metal, true); // tolva de regolito
        }

        private static void BuildWorkshop(Transform station, Material metal, Material accent, Material glow)
        {
            // Arco bajo el que entra el robot.
            Part(PrimitiveType.Cube, station, new Vector3(-1.9f, 1.6f, 0f), new Vector3(0.4f, 3.2f, 0.6f), metal, true);
            Part(PrimitiveType.Cube, station, new Vector3(1.9f, 1.6f, 0f), new Vector3(0.4f, 3.2f, 0.6f), metal, true);
            Part(PrimitiveType.Cube, station, new Vector3(0f, 3.3f, 0f), new Vector3(4.2f, 0.4f, 0.8f), accent, false);
            // Brazos robóticos colgando del arco.
            Part(PrimitiveType.Cube, station, new Vector3(-0.8f, 2.7f, 0f), new Vector3(0.15f, 0.9f, 0.15f), metal, false);
            Part(PrimitiveType.Cube, station, new Vector3(0.8f, 2.7f, 0f), new Vector3(0.15f, 0.9f, 0.15f), metal, false);
            Part(PrimitiveType.Sphere, station, new Vector3(-0.8f, 2.2f, 0f), new Vector3(0.3f, 0.3f, 0.3f), glow, false);
            Part(PrimitiveType.Sphere, station, new Vector3(0.8f, 2.2f, 0f), new Vector3(0.3f, 0.3f, 0.3f), glow, false);
            // Suelo del taller.
            Part(PrimitiveType.Cube, station, new Vector3(0f, 0.03f, 0f), new Vector3(3.4f, 0.04f, 3.4f), metal, false);
        }

        // ------------------------------------------------------------------
        // Utilidades
        // ------------------------------------------------------------------

        /// <summary>Misma posición XZ, altura del terreno.</summary>
        public Vector3 Ground(Vector3 world)
        {
            if (_terrain != null && _terrain.IsMapReady) world.y = _terrain.GetHeightAt(world);
            return world;
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale,
            Material mat, bool keepCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            if (!keepCollider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material Mat(Color color, Color emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            m.color = color;
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }
    }
}
