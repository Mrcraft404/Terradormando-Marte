using UnityEngine;

namespace MarsRobot.Vehicles
{
    /// <summary>
    /// Script de "aire" para el coche principal (venía dentro de WheelScript.cs en el código
    /// original). Está en su propio archivo para que Unity permita añadirlo como componente.
    /// De momento no hace nada activo: solo guarda el umbral de velocidad.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class CarAirScript : MonoBehaviour
    {
        [Tooltip("Velocidad umbral para rebote.")]
        public float speedBounce = 50f;
    }
}
