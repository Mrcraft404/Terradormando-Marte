using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Crew
{
    /// <summary>Algo que el astronauta puede usar con F (máquinas, armarios, montacargas…).</summary>
    public interface ICrewInteractable
    {
        string CrewPrompt { get; }
        /// <summary>Punto al que hay que acercarse (mundo).</summary>
        Vector3 CrewPoint { get; }
        /// <summary>Distancia máxima (horizontal) para usarlo.</summary>
        float CrewRange { get; }
        bool CrewAvailable { get; }
        void CrewInteract(Astronaut astronaut);
    }

    /// <summary>Registro de lo que puede usar el astronauta (sin colisionadores ni capas especiales).</summary>
    public static class CrewInteractables
    {
        private static readonly List<ICrewInteractable> All = new List<ICrewInteractable>();

        public static void Register(ICrewInteractable i)
        {
            if (!All.Contains(i)) All.Add(i);
        }

        public static void Unregister(ICrewInteractable i)
        {
            All.Remove(i);
        }

        /// <summary>El más cercano al que se puede llegar y que queda más o menos delante.</summary>
        public static ICrewInteractable FindBest(Vector3 position, Vector3 forward)
        {
            ICrewInteractable best = null;
            float bestScore = float.MaxValue;
            forward.y = 0f;
            forward.Normalize();
            for (int i = 0; i < All.Count; i++)
            {
                ICrewInteractable it = All[i];
                if (it == null || !it.CrewAvailable) continue;
                Vector3 d = it.CrewPoint - position;
                if (Mathf.Abs(d.y) > 2.2f) continue; // otro nivel de la nave
                d.y = 0f;
                float dist = d.magnitude;
                if (dist > it.CrewRange) continue;
                float facing = dist > 0.3f ? Vector3.Dot(forward, d / dist) : 1f;
                if (facing < -0.2f) continue;
                float score = dist - facing * 0.8f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = it;
                }
            }
            return best;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            All.Clear();
        }
    }
}
