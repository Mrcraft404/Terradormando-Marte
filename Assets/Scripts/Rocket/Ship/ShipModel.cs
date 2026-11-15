using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base.Ship
{
    /// <summary>
    /// Modelo de la nave ya construido (lo crea ShipModelBuilder): referencias a los sectores del
    /// casco (para el corte), a los cinco niveles (para mostrarlos u ocultarlos), a las luces y a
    /// las etiquetas de las salas. Lo usa ShipInspector.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipModel : MonoBehaviour
    {
        // Medidas de diseño (m) — ver la propuesta aprobada.
        public const float OuterRadius = 4.5f;
        public const float InnerRadius = 4.1f;
        public const float HullBottom = 1.5f;
        public const float CylinderTop = 35f;
        public const float TotalHeight = 52.5f;
        public const float FirstLevelFloor = 24.5f;
        public const float LevelHeight = 3.2f;
        public const int LevelCount = 5;

        public static readonly string[] LevelNames =
        {
            "Nivel 1 · Hangar robótico",
            "Nivel 2 · Industria",
            "Nivel 3 · Ciencia y agricultura",
            "Nivel 4 · Recursos y supervivencia",
            "Nivel 5 · Control y comunicaciones",
        };

        /// <summary>Sector del casco (se oculta en la vista de corte si mira hacia la cámara).</summary>
        public sealed class HullSector
        {
            public GameObject go;
            public float centerDeg;
        }

        /// <summary>Etiqueta de una sala o equipo (nivel 0 = zona técnica).</summary>
        public sealed class RoomLabel
        {
            public int level;
            public Vector3 localPos;
            public string text;
        }

        public readonly List<HullSector> HullSectors = new List<HullSector>();
        public readonly List<RoomLabel> Labels = new List<RoomLabel>();
        public Transform[] Levels = new Transform[LevelCount];
        public Light[] LevelLights = new Light[LevelCount];
        public Transform TechnicalInterior;
        public Transform Exterior;

        public static float LevelFloorY(int index)
        {
            return FirstLevelFloor + index * LevelHeight;
        }

        /// <summary>Radio del casco a una altura (cilindro hasta 35 m; nariz ojival encima).</summary>
        public static float HullRadiusAt(float y)
        {
            if (y <= CylinderTop) return OuterRadius;
            float t = Mathf.Clamp01((y - CylinderTop) / (TotalHeight - CylinderTop));
            return OuterRadius * Mathf.Pow(Mathf.Cos(t * Mathf.PI * 0.5f), 0.7f);
        }

        public void AddLabel(int level, Vector3 localPos, string text)
        {
            Labels.Add(new RoomLabel { level = level, localPos = localPos, text = text });
        }
    }
}
