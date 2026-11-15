using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Pasillo presurizado entre dos módulos (o entre un módulo y la esclusa del cohete).
    /// Su aire viene de los extremos: es respirable si AL MENOS UNO de los dos lo es (las
    /// escotillas del lado despresurizado se cierran solas, como en los hábitats reales).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HabitatConnection : MonoBehaviour
    {
        private static readonly List<HabitatConnection> Registry = new List<HabitatConnection>();

        [SerializeField] private MarsHabitatController endA;
        [SerializeField] private int anchorA = -1;
        [SerializeField] private MarsHabitatController endB;
        [SerializeField] private int anchorB = -1;
        [SerializeField] private RocketAirlock airlock;
        [SerializeField] private float length;

        public static IReadOnlyList<HabitatConnection> All
        {
            get { return Registry; }
        }

        public MarsHabitatController EndA
        {
            get { return endA; }
        }

        public MarsHabitatController EndB
        {
            get { return endB; }
        }

        public RocketAirlock Airlock
        {
            get { return airlock; }
        }

        public PressurizedVolume Atmosphere { get; private set; }

        public void Setup(MarsHabitatController a, int indexA, MarsHabitatController b, int indexB, RocketAirlock rocketAirlock)
        {
            endA = a;
            anchorA = indexA;
            endB = b;
            anchorB = indexB;
            airlock = rocketAirlock;
            // El pasillo mide lo mismo que su colisión de suelo (eje Z local).
            var floor = GetComponent<BoxCollider>();
            length = floor != null ? floor.size.z : 4f;
            Bind();
        }

        /// <summary>True si este pasillo toca el hábitat dado.</summary>
        public bool Touches(MarsHabitatController habitat)
        {
            return habitat != null && (habitat == endA || habitat == endB);
        }

        private void Bind()
        {
            var center = new Vector3(0f, MarsHabitatBuilder.CorridorAxisHeight + 0.1f, 0f);
            var size = new Vector3(2.5f, 2.7f, length + 1f); // un poco más largo: solapa con los umbrales
            Atmosphere = PressurizedVolume.Attach(gameObject, "Pasillo presurizado", center, size,
                () => { float p, o; BestEnd(out p, out o); return p; },
                () => { float p, o; BestEnd(out p, out o); return o; });
        }

        /// <summary>El extremo con mejor aire: respirable antes que no respirable; a igualdad, más presión.</summary>
        private void BestEnd(out float pressureKPa, out float oxygenFraction)
        {
            pressureKPa = 0f;
            oxygenFraction = 0.21f;
            bool bestBreathable = false;
            Consider(endA != null ? endA.Atmosphere : null, ref pressureKPa, ref oxygenFraction, ref bestBreathable);
            Consider(endB != null ? endB.Atmosphere : null, ref pressureKPa, ref oxygenFraction, ref bestBreathable);
            Consider(airlock != null ? airlock.Atmosphere : null, ref pressureKPa, ref oxygenFraction, ref bestBreathable);
        }

        private static void Consider(PressurizedVolume v, ref float pressure, ref float oxygen, ref bool breathable)
        {
            if (v == null) return;
            bool b = v.IsBreathable;
            float p = v.PressureKPa;
            if ((b && !breathable) || (b == breathable && p > pressure))
            {
                breathable = b;
                pressure = p;
                oxygen = p > 0.01f ? v.OxygenPartialKPa / p : 0.21f;
            }
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
            Bind(); // las funciones no se guardan con la escena
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }
    }
}
