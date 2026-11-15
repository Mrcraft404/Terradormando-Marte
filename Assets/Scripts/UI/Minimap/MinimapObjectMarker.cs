using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.UI.Minimap
{
    /// <summary>Tipo de icono en el minimapa.</summary>
    public enum MinimapMarkerType
    {
        Rocket,     // base / cohete nodriza
        Resource,   // roca con minerales de interés (escaneada)
        Ice,        // hielo subterráneo detectado
        Structure,  // estructura construida
        Beacon,     // baliza de comunicaciones
        Vehicle,    // MarteCar
        Alert,      // aviso (clima espacial, peligro)
        Discovery   // punto de interés con registro (sonda antigua…)
    }

    /// <summary>
    /// Registro de marcadores del minimapa. Se pueden usar de dos formas:
    ///  • Como componente en un objeto del mundo (el marcador sigue al objeto).
    ///  • Con los métodos estáticos Add/AddPing (marcadores ligados a un objeto o temporales en
    ///    una posición, p. ej. hielo detectado por el escáner).
    /// El minimapa (MarsMinimapController) los lee y los dibuja.
    /// </summary>
    public sealed class MinimapObjectMarker : MonoBehaviour
    {
        /// <summary>Datos de un marcador.</summary>
        public sealed class Entry
        {
            public Object owner;            // objeto al que pertenece (puede ser null si es temporal)
            public Transform follow;        // si no es null, la posición se toma de aquí
            public Vector3 position;
            public MinimapMarkerType type;
            public bool blink;
            public float expiresAt = -1f;   // < 0: no caduca
            public string label;
        }

        private static readonly List<Entry> Entries = new List<Entry>();

        [SerializeField] private MinimapMarkerType type = MinimapMarkerType.Structure;
        [SerializeField] private bool blink;
        [SerializeField] private string label = "";

        public static IReadOnlyList<Entry> All
        {
            get
            {
                // Limpiar caducados y destruidos.
                for (int i = Entries.Count - 1; i >= 0; i--)
                {
                    Entry e = Entries[i];
                    bool ownerGone = e.owner is Object && e.owner == null && !ReferenceEquals(e.owner, null);
                    if ((e.expiresAt >= 0f && Time.time > e.expiresAt) || ownerGone) Entries.RemoveAt(i);
                }
                return Entries;
            }
        }

        /// <summary>Configura el marcador al añadirlo por código.</summary>
        public void Setup(MinimapMarkerType markerType, bool blinking, string text)
        {
            Remove(this);
            type = markerType;
            blink = blinking;
            label = text;
            if (isActiveAndEnabled) Register();
        }

        private void OnEnable()
        {
            Register();
        }

        private void OnDisable()
        {
            Remove(this);
        }

        private void Register()
        {
            Entries.Add(new Entry { owner = this, follow = transform, position = transform.position, type = type, blink = blink, label = label });
        }

        /// <summary>Marcador ligado a un objeto (desaparece al destruirlo o con Remove).</summary>
        public static void Add(Object owner, Vector3 position, MinimapMarkerType type, bool blink)
        {
            Entries.Add(new Entry { owner = owner, position = position, type = type, blink = blink });
        }

        /// <summary>Marcador temporal en una posición (p. ej. hielo detectado, alerta).</summary>
        public static void AddPing(Vector3 position, MinimapMarkerType type, float seconds)
        {
            Entries.Add(new Entry { position = position, type = type, blink = true, expiresAt = Time.time + seconds });
        }

        /// <summary>Quita los marcadores de un objeto.</summary>
        public static void Remove(Object owner)
        {
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(Entries[i].owner, owner)) Entries.RemoveAt(i);
            }
        }

        public static Color ColorOf(MinimapMarkerType t)
        {
            switch (t)
            {
                case MinimapMarkerType.Rocket: return new Color(1f, 1f, 1f);
                case MinimapMarkerType.Resource: return new Color(1f, 0.6f, 0.2f);
                case MinimapMarkerType.Ice: return new Color(0.4f, 0.85f, 1f);
                case MinimapMarkerType.Structure: return new Color(0.7f, 0.75f, 0.8f);
                case MinimapMarkerType.Beacon: return new Color(0.4f, 1f, 0.5f);
                case MinimapMarkerType.Vehicle: return new Color(0.95f, 0.5f, 0.15f);
                case MinimapMarkerType.Discovery: return new Color(1f, 0.9f, 0.3f);
                default: return new Color(1f, 0.2f, 0.2f);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Entries.Clear();
        }
    }
}
