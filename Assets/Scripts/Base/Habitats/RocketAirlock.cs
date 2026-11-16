using MarsRobot.Base.Ship;
using MarsRobot.Crew;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Esclusa de la base al pie del cohete: un vestíbulo presurizado (alimentado por la nave)
    /// con un pozo hasta el hangar. Es el punto donde se conecta la red de módulos con la nave.
    ///
    ///  • Abajo, en el vestíbulo: F → subir a la nave (al hangar, nivel 1).
    ///  • Arriba, en el hangar (junto al montacargas): F → bajar a la base (solo si la esclusa existe).
    ///
    /// Está detrás del cohete (el ascensor del robot queda delante) a 8 m de su eje.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RocketAirlock : MonoBehaviour, ICrewInteractable
    {
        public const float DistanceFromAxis = 8f;
        private const float Slab = 0.3f;
        private static readonly Vector3 VestibuleSize = new Vector3(3f, 2.8f, 3f);
        /// <summary>Posición (local de cada nivel de la nave) de la escotilla del hangar: junto al montacargas.</summary>
        public static readonly Vector3 HangarHatchLocal = new Vector3(-1.75f, 0f, -1.6f);

        public static RocketAirlock Instance { get; private set; }

        [SerializeField] private RocketBase rocket;
        [SerializeField] private bool connected;

        public PressurizedVolume Atmosphere { get; private set; }

        /// <summary>Umbral de la puerta exterior del vestíbulo (suelo, mundo): aquí llega el pasillo.</summary>
        public Vector3 DoorPoint
        {
            get { return transform.TransformPoint(new Vector3(0f, Slab, VestibuleSize.z * 0.5f)); }
        }

        /// <summary>Dirección hacia fuera de la puerta (horizontal).</summary>
        public Vector3 OutwardDirection
        {
            get { return transform.forward; }
        }

        /// <summary>True cuando ya hay un pasillo conectado a la esclusa.</summary>
        public bool IsConnected
        {
            get { return connected; }
        }

        public void MarkConnected()
        {
            connected = true;
        }

        // ------------------------------------------------------------------
        // Posición y creación
        // ------------------------------------------------------------------

        /// <summary>Dónde quedará la puerta de la esclusa (para el holograma, antes de que exista).</summary>
        public static void ComputeDoor(RocketBase rocketBase, out Vector3 doorPoint, out Vector3 outward)
        {
            Transform t = rocketBase.transform;
            outward = -t.forward;
            outward.y = 0f;
            outward.Normalize();
            Vector3 center = rocketBase.Ground(t.position + outward * DistanceFromAxis);
            doorPoint = center + outward * (VestibuleSize.z * 0.5f) + Vector3.up * Slab;
        }

        public static RocketAirlock GetOrCreate(RocketBase rocketBase)
        {
            if (Instance != null) return Instance;
            if (rocketBase == null) return null;

            Transform t = rocketBase.transform;
            Vector3 outward = -t.forward;
            outward.y = 0f;
            outward.Normalize();
            var go = new GameObject("Esclusa de la base");
            go.transform.SetParent(MarsRobot.Visuals.SceneRoots.Get(MarsRobot.Visuals.SceneRoot.Structures), false);
            go.transform.SetPositionAndRotation(rocketBase.Ground(t.position + outward * DistanceFromAxis),
                                                Quaternion.LookRotation(outward, Vector3.up));
            var airlock = go.AddComponent<RocketAirlock>();
            airlock.rocket = rocketBase;
            airlock.BuildGeometry();
            airlock.Bind();
            ShipAirlockHatch.Create(rocketBase);
            return airlock;
        }

        private void BuildGeometry()
        {
            var acc = new HabitatMeshAccumulator();
            Vector3 s = VestibuleSize;
            // Losa, paredes dobles (la delantera, +Z, es la puerta: se recorta) y techo.
            acc.AddBox(new Vector3(0f, Slab * 0.5f, 0f), new Vector3(s.x + 0.6f, Slab, s.z + 0.6f), HabitatMaterials.SinteredSlab);
            Mesh outer = HabitatGeometry.BoxSurface(s, false);
            Mesh inner = HabitatGeometry.BoxSurface(s - new Vector3(0.1f, 0.05f, 0.1f), true);
            var door = new[] { new HabitatGeometry.CutBox { Center = new Vector3(0f, s.y * 0.5f, s.z * 0.5f), HalfExtents = new Vector3(s.x, s.y, 0.3f), Rotation = Quaternion.identity } };
            HabitatGeometry.CutTriangles(outer, door);
            HabitatGeometry.CutTriangles(inner, door);
            Matrix4x4 lift = Matrix4x4.Translate(new Vector3(0f, Slab, 0f));
            acc.AddGenerated(outer, lift, HabitatMaterials.KevlarOuter);
            acc.AddGenerated(inner, lift, HabitatMaterials.InsulationInner);
            // Marco naranja de la puerta y luz de estado.
            float top = Slab + s.y;
            acc.AddBox(new Vector3(-s.x * 0.5f, Slab + s.y * 0.5f, s.z * 0.5f), new Vector3(0.12f, s.y, 0.12f), HabitatMaterials.SafetyOrange);
            acc.AddBox(new Vector3(s.x * 0.5f, Slab + s.y * 0.5f, s.z * 0.5f), new Vector3(0.12f, s.y, 0.12f), HabitatMaterials.SafetyOrange);
            acc.AddBox(new Vector3(0f, top - 0.06f, s.z * 0.5f), new Vector3(s.x, 0.12f, 0.12f), HabitatMaterials.SafetyOrange);
            acc.AddBox(new Vector3(0f, top - 0.3f, s.z * 0.5f - 0.1f), new Vector3(0.6f, 0.05f, 0.05f), HabitatMaterials.StatusLight);

            // Pozo presurizado hasta el hangar y puente hasta el casco (visual: el paso es por la escotilla).
            float hangarY = transform.InverseTransformPoint(rocket.Mothership.transform.TransformPoint(ShipLayout.OnLevel(ShipModel.HangarLevel, Vector3.zero))).y;
            float shaftTop = Mathf.Max(top + 1f, hangarY + 1.4f);
            acc.AddPrimitive(PrimitiveType.Cylinder, new Vector3(0f, (top + shaftTop) * 0.5f, -0.4f), Quaternion.identity,
                             new Vector3(1.6f, (shaftTop - top) * 0.5f, 1.6f), HabitatMaterials.KevlarOuter);
            float bridge = DistanceFromAxis - ShipModel.OuterRadius - 0.4f;
            acc.AddPrimitive(PrimitiveType.Cylinder, new Vector3(0f, shaftTop - 0.2f, -0.4f - bridge * 0.5f), Quaternion.Euler(90f, 0f, 0f),
                             new Vector3(1.4f, bridge * 0.5f, 1.4f), HabitatMaterials.KevlarOuter);
            acc.Build(transform, "Esclusa");

            // Colisiones: suelo, tres paredes y techo (la puerta, +Z, queda abierta).
            AddCollider(new Vector3(0f, Slab * 0.5f, 0f), new Vector3(s.x + 0.6f, Slab, s.z + 0.6f));
            AddCollider(new Vector3(0f, Slab + s.y * 0.5f, -s.z * 0.5f), new Vector3(s.x, s.y, 0.15f));
            AddCollider(new Vector3(-s.x * 0.5f, Slab + s.y * 0.5f, 0f), new Vector3(0.15f, s.y, s.z));
            AddCollider(new Vector3(s.x * 0.5f, Slab + s.y * 0.5f, 0f), new Vector3(0.15f, s.y, s.z));
            AddCollider(new Vector3(0f, top, 0f), new Vector3(s.x, 0.15f, s.z));
        }

        private void AddCollider(Vector3 center, Vector3 size)
        {
            var c = gameObject.AddComponent<BoxCollider>();
            c.center = center;
            c.size = size;
        }

        private void Bind()
        {
            // El vestíbulo comparte el aire de la nave: siempre a presión terrestre.
            Atmosphere = PressurizedVolume.Attach(gameObject, Tr.T("Esclusa de la base"),
                new Vector3(0f, Slab + VestibuleSize.y * 0.5f, 0f), VestibuleSize + new Vector3(0f, 0f, 0.6f),
                () => 101.3f, () => 0.21f);
            ShipAirlockHatch.EnsureShipAtmosphere(rocket);
        }

        private void OnEnable()
        {
            Instance = this;
            CrewInteractables.Register(this);
            if (rocket != null) Bind();
        }

        private void OnDisable()
        {
            if (Instance == this) Instance = null;
            CrewInteractables.Unregister(this);
        }

        // ------------------------------------------------------------------
        // Astronauta: subir a la nave
        // ------------------------------------------------------------------

        public string CrewPrompt
        {
            get { return Tr.T("F: subir a la nave por la esclusa"); }
        }

        public Vector3 CrewPoint
        {
            get { return transform.TransformPoint(new Vector3(0f, Slab + 1f, -0.6f)); }
        }

        public float CrewRange
        {
            get { return 1.8f; }
        }

        public bool CrewAvailable
        {
            get { return rocket != null; }
        }

        public void CrewInteract(Astronaut astronaut)
        {
            if (astronaut == null || rocket == null || rocket.Mothership == null) return;
            Transform ship = rocket.Mothership.transform;
            Vector3 arrival = ship.TransformPoint(ShipLayout.OnLevel(ShipModel.HangarLevel, HangarHatchLocal + new Vector3(0f, 0.1f, 0.7f)));
            astronaut.Teleport(arrival, ship.rotation);
        }

        /// <summary>Punto de llegada al bajar desde la nave: dentro del vestíbulo, mirando a la puerta.</summary>
        public void ReceiveFromShip(Astronaut astronaut)
        {
            if (astronaut == null) return;
            astronaut.Teleport(transform.TransformPoint(new Vector3(0f, Slab + 0.1f, -0.5f)), transform.rotation);
        }
    }
}
