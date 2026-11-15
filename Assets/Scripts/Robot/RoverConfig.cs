using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>
    /// Dimensiones y restricciones físicas del rover, a escala de un rover clase Curiosity /
    /// Perseverance (unos 3 m de largo, 2,7 m de ancho, 2,2 m de alto hasta el mástil y unos
    /// 900–1025 kg). Crear con Assets ▸ Create ▸ Marte ▸ Robot ▸ Configuración del rover; si no se
    /// asigna ninguna, se usan estos valores por defecto.
    /// </summary>
    [CreateAssetMenu(fileName = "RoverConfig", menuName = "Marte/Robot/Configuración del rover")]
    public class RoverConfig : ScriptableObject
    {
        [Header("Dimensiones (m)")]
        public float length = 3.0f;
        public float width = 2.7f;
        public float height = 2.2f;
        [Tooltip("Altura de la cámara (mástil).")]
        public float mastHeight = 2.1f;
        [Tooltip("Centro de masa local: bajo y centrado para no volcar en pendientes.")]
        public Vector3 centerOfMass = new Vector3(0f, 0.45f, 0f);

        [Header("Masa")]
        [Tooltip("Masa del chasis sin módulos ni carga (kg). Curiosity: ~899 kg.")]
        public float chassisMassKg = 899f;

        [Header("Ranuras de equipamiento")]
        [Tooltip("Brazo delantero: 1 herramienta (taladro, pala o soplador).")]
        [Min(1)] public int frontArmSlots = 1;
        [Tooltip("Cubierta superior: antena de alta ganancia o paneles solares extra.")]
        [Min(0)] public int topDeckSlots = 1;
        [Tooltip("Compartimentos internos: almacenamiento, baterías, contenedores…")]
        [Min(0)] public int internalBaySlots = 3;

        [Header("Penalización por masa de módulos (gravedad 0,38 g)")]
        [Tooltip("Kg de módulos que restarían toda la velocidad (se limita al 55 %).")]
        [Min(1f)] public float massForZeroSpeedKg = 400f;
        [Tooltip("Kg de módulos que duplicarían el consumo de los motores.")]
        [Min(1f)] public float massForDoublePowerKg = 250f;

        private static RoverConfig _default;

        /// <summary>Configuración por defecto (en memoria) si no hay asset.</summary>
        public static RoverConfig Default
        {
            get
            {
                if (_default == null)
                {
                    _default = CreateInstance<RoverConfig>();
                    _default.name = "RoverConfig (por defecto)";
                }
                return _default;
            }
        }
    }
}
