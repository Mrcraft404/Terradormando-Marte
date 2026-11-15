using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Volumen de aire interior (módulo, pasillo, esclusa o la propia nave). Es una caja orientada
    /// en el espacio de su objeto, SIN colisionador (no estorba a raycasts ni a la cámara): el
    /// soporte vital del astronauta pregunta <see cref="FindAt"/> en qué volumen está.
    ///
    /// La presión y el oxígeno los da una función (el controlador del hábitat, el pasillo…), así
    /// el volumen siempre refleja el estado real: si el módulo se despresuriza, deja de ser respirable.
    /// </summary>
    public sealed class PressurizedVolume : MonoBehaviour
    {
        /// <summary>Presión total mínima para respirar sin traje (kPa).</summary>
        public const float MinBreathablePressureKPa = 30f;

        /// <summary>Presión parcial de oxígeno mínima (kPa). A nivel del mar es ~21 kPa; 13 kPa ≈ 3 500 m de altitud.</summary>
        public const float MinOxygenPartialKPa = 13f;

        private static readonly List<PressurizedVolume> Registry = new List<PressurizedVolume>();

        [SerializeField] private Vector3 center;
        [SerializeField] private Vector3 size = Vector3.one;
        [SerializeField] private string label = "Interior";

        private Func<float> _pressureKPa;
        private Func<float> _oxygenFraction;

        public string Label
        {
            get { return label; }
        }

        /// <summary>Presión total actual (kPa).</summary>
        public float PressureKPa
        {
            get { return _pressureKPa != null ? Mathf.Max(0f, _pressureKPa()) : 0f; }
        }

        /// <summary>Presión parcial de oxígeno (kPa).</summary>
        public float OxygenPartialKPa
        {
            get { return PressureKPa * (_oxygenFraction != null ? _oxygenFraction() : 0.21f); }
        }

        public bool IsBreathable
        {
            get { return PressureKPa >= MinBreathablePressureKPa && OxygenPartialKPa >= MinOxygenPartialKPa; }
        }

        /// <summary>Crea (o reutiliza) el volumen en <paramref name="host"/>.</summary>
        public static PressurizedVolume Attach(GameObject host, string label, Vector3 localCenter, Vector3 localSize,
                                               Func<float> pressureKPa, Func<float> oxygenFraction)
        {
            var v = host.GetComponent<PressurizedVolume>();
            if (v == null) v = host.AddComponent<PressurizedVolume>();
            v.label = label;
            v.center = localCenter;
            v.size = localSize;
            v._pressureKPa = pressureKPa;
            v._oxygenFraction = oxygenFraction;
            return v;
        }

        /// <summary>Cambia de dónde salen la presión y el oxígeno (p. ej. al reconectar un pasillo).</summary>
        public void SetSource(Func<float> pressureKPa, Func<float> oxygenFraction)
        {
            _pressureKPa = pressureKPa;
            _oxygenFraction = oxygenFraction;
        }

        public bool Contains(Vector3 worldPoint)
        {
            Vector3 p = transform.InverseTransformPoint(worldPoint) - center;
            Vector3 h = size * 0.5f;
            return Mathf.Abs(p.x) <= h.x && Mathf.Abs(p.y) <= h.y && Mathf.Abs(p.z) <= h.z;
        }

        /// <summary>
        /// Volumen que contiene el punto. Si hay varios (una puerta entre módulo y pasillo), gana el
        /// respirable con más presión: así no se "pierde el aire" justo en el umbral.
        /// </summary>
        public static PressurizedVolume FindAt(Vector3 worldPoint)
        {
            PressurizedVolume best = null;
            for (int i = 0; i < Registry.Count; i++)
            {
                PressurizedVolume v = Registry[i];
                if (v == null || !v.isActiveAndEnabled || !v.Contains(worldPoint)) continue;
                if (best == null || (v.IsBreathable && !best.IsBreathable) ||
                    (v.IsBreathable == best.IsBreathable && v.PressureKPa > best.PressureKPa))
                {
                    best = v;
                }
            }
            return best;
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
        }

        private void OnDisable()
        {
            Registry.Remove(this);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Registry.Clear();
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = IsBreathable ? new Color(0.3f, 0.8f, 1f, 0.6f) : new Color(1f, 0.3f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
