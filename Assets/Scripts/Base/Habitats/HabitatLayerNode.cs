using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Marca un hijo de un hábitat como una de sus 5 capas y guarda sus propiedades físicas.
    /// <see cref="MarsHabitatController"/> lo usa para comprobar la integridad: si el objeto de
    /// la capa se borra o se desactiva, la capa cuenta como ausente.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HabitatLayerNode : MonoBehaviour
    {
        public HabitatLayer layer;

        [Tooltip("0 = destruida, 1 = intacta. Por debajo de 0,5 la capa no cumple su función (fuga, brecha…).")]
        [Range(0f, 1f)] public float integrity = 1f;

        [Header("Propiedades físicas")]
        [Tooltip("Espesor de la capa (m).")]
        public float thicknessMeters;
        [Tooltip("Coeficiente de transmisión térmica (W/m²·K). Solo la Capa 2.")]
        public float uValue;
        [Tooltip("Densidad superficial frente a la radiación (g/cm²). Sobre todo la Capa 3.")]
        public float arealDensity;
        [Tooltip("Presión que soporta la capa (kPa). Solo la Capa 2.")]
        public float ratedPressureKPa;

        /// <summary>True si la capa existe, está activa y conserva al menos la mitad de su integridad.</summary>
        public bool IsFunctional
        {
            get { return isActiveAndEnabled && gameObject.activeInHierarchy && integrity >= 0.5f; }
        }

        public static string NodeName(HabitatLayer layer)
        {
            switch (layer)
            {
                case HabitatLayer.Foundation: return Tr.T("Capa 0 · Cimentación");
                case HabitatLayer.Skeleton: return Tr.T("Capa 1 · Estructura esquelética");
                case HabitatLayer.PressureShell: return Tr.T("Capa 2 · Contención neumática y térmica");
                case HabitatLayer.RegolithShield: return Tr.T("Capa 3 · Escudo de regolito");
                default: return Tr.T("Capa 4 · Sistemas internos");
            }
        }

        private void OnValidate()
        {
            // Cambiar la integridad desde el Inspector revalida el hábitat.
            var controller = GetComponentInParent<MarsHabitatController>();
            if (controller != null) controller.RequestValidation();
        }
    }
}
