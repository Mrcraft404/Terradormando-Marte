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
        // Medidas de diseño (m). Ampliada a Ø 10 m y 6 niveles: el nivel 2 (Talleres) alberga las
        // máquinas que antes estaban fuera, alrededor del cohete.
        public const float OuterRadius = 5.0f;
        public const float InnerRadius = 4.6f;
        public const float HullBottom = 1.5f;
        public const float CylinderTop = 38.2f;
        public const float TotalHeight = 55.7f;
        public const float FirstLevelFloor = 24.5f;
        public const float LevelHeight = 3.2f;
        public const int LevelCount = 6;

        public const int HangarLevel = 0;
        public const int WorkshopLevel = 1;
        public const int ControlLevel = LevelCount - 1;

        public static readonly string[] LevelNames =
        {
            "Nivel 1 · Hangar robótico",
            "Nivel 2 · Talleres de fabricación",
            "Nivel 3 · Industria",
            "Nivel 4 · Ciencia y agricultura",
            "Nivel 5 · Recursos y supervivencia",
            "Nivel 6 · Control y comunicaciones",
        };

        /// <summary>Altura (local de la nave) del suelo del hangar, donde se acopla el robot.</summary>
        public static float HangarFloorY
        {
            get { return FirstLevelFloor; }
        }

        /// <summary>
        /// True si una posición (local de la nave) está dentro del hangar: es la condición para
        /// cambiar entre el astronauta y el robot.
        /// </summary>
        public static bool IsInsideHangar(Vector3 local)
        {
            float r = new Vector2(local.x, local.z).magnitude;
            return r < InnerRadius - 0.2f && local.y > FirstLevelFloor - 0.6f && local.y < FirstLevelFloor + LevelHeight - 0.2f;
        }

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
            // Max(0, …): cos(90°) da −4·10⁻⁸ y Pow de un negativo es NaN (la nariz no se dibujaba).
            return OuterRadius * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(t * Mathf.PI * 0.5f)), 0.7f);
        }

        public void AddLabel(int level, Vector3 localPos, string text)
        {
            Labels.Add(new RoomLabel { level = level, localPos = localPos, text = text });
        }
    }
}
