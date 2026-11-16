using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Items;
using MarsRobot.UI.Minimap;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Cohete nodriza como estación industrial en la superficie (inspirado en los estudios de
    /// arquitectura de misión de la NASA, DRA 5.0, y en los cohetes grandes reutilizables).
    /// Añade a la base del cohete:
    ///  • Bodega del cohete (StorageContainer): lo que entrega el rover pasa al almacén de la base.
    ///  • Terminal del cohete: clima espacial, propelente, blindaje y bitácora.
    ///  • Fundición (SmelterStation) y refinería ISRU (Sabatier).
    ///  • Berma de blindaje: anillo de terraplenes de regolito alrededor del cohete que el rover
    ///    va levantando. Cuanto más completo, menos radiación recibe la base.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RocketBaseManager : MonoBehaviour
    {
        /// <summary>Radio del anillo de berma (m) y número de tramos.</summary>
        public const float BermRadius = 9.5f; // fuera de las patas (6,4 m) y aletas (6,5 m) de la nave
        public const int BermSegments = 11;

        [Tooltip("Propelente (metano) necesario para el vuelo de regreso / reabastecer la nave.")]
        [SerializeField, Min(1)] private int propellantForLaunch = 400;

        private readonly List<BermSegment> _berm = new List<BermSegment>();
        private RocketBase _base;

        public StorageContainer Hold { get; private set; }
        public SmelterStation Smelter { get; private set; }
        public ISRU_FuelRefinery Refinery { get; private set; }

        public int PropellantForLaunch
        {
            get { return propellantForLaunch; }
        }

        /// <summary>Blindaje radiológico 0..1 (fracción de berma completada).</summary>
        public float Shielding01
        {
            get
            {
                if (_berm.Count == 0) return 0f;
                float sum = 0f;
                for (int i = 0; i < _berm.Count; i++) sum += _berm[i].Fill01;
                return sum / _berm.Count;
            }
        }

        /// <summary>Reducción de dosis en la base (una berma completa de ~1,8 m quita en torno al 60 %).</summary>
        public float RadiationReduction01
        {
            get { return Shielding01 * 0.6f; }
        }

        public float Propellant01
        {
            get { return _base != null ? Mathf.Clamp01(_base.Inventory.Get(ResourceType.Methane) / (float)propellantForLaunch) : 0f; }
        }

        private void Awake()
        {
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        /// <summary>Lo llama RocketBase al crearse.</summary>
        public void Build(RocketBase rocketBase)
        {
            _base = rocketBase;

            // Bodega del hangar: el robot entrega aquí su carga. Comparte las casillas con el almacén
            // de la base (lo que entrega el robot lo ve el astronauta en los talleres).
            int hangar = MarsRobot.Base.Ship.ShipModel.HangarLevel;
            Hold = rocketBase.AddStationInside<StorageContainer>(Tr.T("Bodega del hangar"),
                MarsRobot.Base.Ship.ShipLayout.OnLevel(hangar, MarsRobot.Base.Ship.ShipLayout.Hold), 2.2f);
            Hold.UseContainer(rocketBase.Inventory.Storage);

            // Terminal del cohete, en el banco de trabajo del hangar.
            rocketBase.AddStationInside<RocketTerminalStation>(Tr.T("Terminal del cohete"),
                MarsRobot.Base.Ship.ShipLayout.OnLevel(hangar, MarsRobot.Base.Ship.ShipLayout.Terminal), 1.8f);

            // La fundición y la refinería están ahora dentro de la nave (nivel de talleres, ver
            // CraftingStation). La refinería ISRU exterior se puede seguir construyendo con B.

            // Berma: tramos en arco, dejando libre la rampa (+Z).
            for (int i = 0; i < BermSegments; i++)
            {
                float angle = 30f + i * (300f / (BermSegments - 1));
                Quaternion rot = Quaternion.Euler(0f, angle, 0f);
                Vector3 local = rot * new Vector3(0f, 0f, BermRadius);
                var go = new GameObject("Berma " + (i + 1));
                go.transform.SetParent(rocketBase.transform, false);
                go.transform.position = rocketBase.Ground(rocketBase.transform.TransformPoint(local));
                go.transform.rotation = rocketBase.transform.rotation * rot;
                var seg = go.AddComponent<BermSegment>();
                seg.Init(this, i + 1);
                _berm.Add(seg);
            }

            var marker = gameObject.AddComponent<MinimapObjectMarker>();
            marker.Setup(MinimapMarkerType.Rocket, false, Tr.T("Cohete nodriza"));
        }

        private static void Prop(Transform parent, Vector3 pos, Vector3 scale, Color color, bool glow = false)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { color = color };
            if (glow)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", color * 1.5f);
            }
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        public string DescribeShielding()
        {
            int done = 0;
            for (int i = 0; i < _berm.Count; i++) if (_berm[i].Fill01 >= 0.999f) done++;
            return Tr.T("Berma de blindaje: ") + done + "/" + _berm.Count + Tr.T(" tramos completos · ") +
                   Mathf.RoundToInt(Shielding01 * 100f) + " % · −" + Mathf.RoundToInt(RadiationReduction01 * 100f) + Tr.T(" % de radiación en la base");
        }
    }
}
