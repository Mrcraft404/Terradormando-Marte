using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.CameraEffects
{
    /// <summary>Un efecto que desplaza o gira la cámara (balanceo, sacudidas…).</summary>
    public interface ICameraEffect
    {
        /// <summary>Desplazamiento local de la cámara este frame (m).</summary>
        Vector3 PositionOffset { get; }

        /// <summary>Rotación local extra este frame (grados: pitch, yaw, roll).</summary>
        Vector3 EulerOffset { get; }
    }

    /// <summary>
    /// Suma todos los efectos de cámara activos. El RobotCameraRig la consulta al final del frame
    /// (LateUpdate) y aplica el resultado encima de la posición normal de la cámara, así los
    /// efectos funcionan igual en primera y en tercera persona y no se pelean entre sí.
    /// </summary>
    public static class CameraEffectStack
    {
        private static readonly List<ICameraEffect> Effects = new List<ICameraEffect>();

        public static void Register(ICameraEffect e)
        {
            if (!Effects.Contains(e)) Effects.Add(e);
        }

        public static void Unregister(ICameraEffect e)
        {
            Effects.Remove(e);
        }

        public static Vector3 Position
        {
            get
            {
                Vector3 p = Vector3.zero;
                for (int i = 0; i < Effects.Count; i++) p += Effects[i].PositionOffset;
                return p;
            }
        }

        public static Vector3 Euler
        {
            get
            {
                Vector3 e = Vector3.zero;
                for (int i = 0; i < Effects.Count; i++) e += Effects[i].EulerOffset;
                return e;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Effects.Clear();
        }
    }
}
