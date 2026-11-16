using MarsRobot.Base.Ship;
using MarsRobot.Crew;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Escotilla del hangar (dentro de la nave) que baja a la esclusa de la base.</summary>
    public sealed class ShipAirlockHatch : MonoBehaviour, ICrewInteractable
    {
        private static ShipAirlockHatch _instance;

        public static void Create(RocketBase rocketBase)
        {
            if (_instance != null || rocketBase == null || rocketBase.Mothership == null) return;
            Transform ship = rocketBase.Mothership.transform;
            var go = new GameObject("Escotilla a la base");
            go.transform.SetParent(ship, false);
            go.transform.localPosition = ShipLayout.OnLevel(ShipModel.HangarLevel, RocketAirlock.HangarHatchLocal);
            _instance = go.AddComponent<ShipAirlockHatch>();

            // Trampilla visible en el suelo (sin colisión: se pisa).
            var acc = new HabitatMeshAccumulator();
            acc.AddPrimitive(PrimitiveType.Cylinder, new Vector3(0f, 0.02f, 0f), Quaternion.identity, new Vector3(1.1f, 0.02f, 1.1f), HabitatMaterials.SafetyOrange);
            acc.AddPrimitive(PrimitiveType.Cylinder, new Vector3(0f, 0.03f, 0f), Quaternion.identity, new Vector3(0.8f, 0.02f, 0.8f), HabitatMaterials.Machine);
            foreach (GameObject part in acc.Build(go.transform, "Escotilla"))
                part.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>La nave es respirable por dentro (para el soporte vital del astronauta).</summary>
        public static void EnsureShipAtmosphere(RocketBase rocketBase)
        {
            if (rocketBase == null || rocketBase.Mothership == null) return;
            GameObject ship = rocketBase.Mothership.gameObject;
            if (ship.GetComponent<PressurizedVolume>() != null) return;
            float top = ShipModel.LevelFloorY(ShipModel.LevelCount - 1) + 4f;
            float d = ShipModel.InnerRadius * 2f;
            PressurizedVolume.Attach(ship, "Nave nodriza", new Vector3(0f, top * 0.5f, 0f), new Vector3(d, top + 1f, d),
                                     () => 101.3f, () => 0.21f);
        }

        private void OnEnable()
        {
            CrewInteractables.Register(this);
        }

        private void OnDisable()
        {
            CrewInteractables.Unregister(this);
            if (_instance == this) _instance = null;
        }

        public string CrewPrompt
        {
            get { return Tr.T("F: bajar a la base por la esclusa"); }
        }

        public Vector3 CrewPoint
        {
            get { return transform.position + Vector3.up; }
        }

        public float CrewRange
        {
            get { return 1.3f; }
        }

        public bool CrewAvailable
        {
            get { return RocketAirlock.Instance != null; }
        }

        public void CrewInteract(Astronaut astronaut)
        {
            if (RocketAirlock.Instance != null) RocketAirlock.Instance.ReceiveFromShip(astronaut);
        }
    }
}
