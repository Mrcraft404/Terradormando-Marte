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
        public const float BermRadius = 8.5f; // fuera de las patas (5,7 m) y aletas (6 m) de la nave
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

            // Bodega del cohete, al pie de la rampa (a la derecha) y terminal (a la izquierda).
            Hold = rocketBase.AddStation<StorageContainer>("Bodega del cohete", new Vector3(5f, 0f, 11.5f), 2.8f);
            Hold.Configure(80, 0f, 0f, true, false);
            Prop(Hold.transform, new Vector3(0f, 0.7f, 0f), new Vector3(1.8f, 1.4f, 1.4f), new Color(0.55f, 0.56f, 0.6f));
            Prop(Hold.transform, new Vector3(0f, 1.45f, 0f), new Vector3(1.9f, 0.1f, 1.5f), new Color(0.95f, 0.5f, 0.1f));

            var terminal = rocketBase.AddStation<RocketTerminalStation>("Terminal del cohete", new Vector3(-5f, 0f, 11.5f), 2.5f);
            Prop(terminal.transform, new Vector3(0f, 0.6f, 0f), new Vector3(0.6f, 1.2f, 0.4f), new Color(0.2f, 0.2f, 0.22f));
            Prop(terminal.transform, new Vector3(0f, 1.35f, 0.05f), new Vector3(0.9f, 0.55f, 0.08f), new Color(0.2f, 0.8f, 1f));

            // Fundición (detrás del cohete, a la izquierda).
            Smelter = rocketBase.AddStation<SmelterStation>("Fundición", new Vector3(-10.5f, 0f, -10.5f), 4f);
            Prop(Smelter.transform, new Vector3(0f, 1.1f, 0f), new Vector3(2.4f, 2.2f, 2.4f), new Color(0.35f, 0.33f, 0.32f));
            Prop(Smelter.transform, new Vector3(0f, 2.9f, 0f), new Vector3(0.6f, 1.6f, 0.6f), new Color(0.25f, 0.24f, 0.24f)); // chimenea
            Prop(Smelter.transform, new Vector3(0f, 0.8f, 1.21f), new Vector3(1f, 0.6f, 0.04f), new Color(1f, 0.45f, 0.1f), true); // boca del horno

            // Refinería ISRU (detrás del cohete, a la derecha): es una estructura más de la red de energía.
            StructureDefinition refineryDef = StructureCatalog.Find("refinery");
            if (refineryDef != null)
            {
                Vector3 pos = rocketBase.transform.TransformPoint(new Vector3(10.5f, 0f, -10.5f));
                Refinery = rocketBase.PlaceStructure<ISRU_FuelRefinery>(refineryDef, pos, rocketBase.transform.rotation);
            }

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
            marker.Setup(MinimapMarkerType.Rocket, false, "Cohete nodriza");
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
            return "Berma de blindaje: " + done + "/" + _berm.Count + " tramos completos · " +
                   Mathf.RoundToInt(Shielding01 * 100f) + " % · −" + Mathf.RoundToInt(RadiationReduction01 * 100f) + " % de radiación en la base";
        }
    }
}
