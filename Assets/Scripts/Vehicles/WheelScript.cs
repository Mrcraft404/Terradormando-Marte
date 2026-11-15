using UnityEngine;

namespace MarsRobot.Vehicles
{
    /// <summary>
    /// Rueda simple del coche de pruebas (MarteCar). Simula pérdida de velocidad por rodadura y un
    /// pequeño "rebote" cuando va muy rápida.
    ///
    /// Cambios respecto al código original (para que compile y funcione en Unity 6):
    ///  • 'speedBounce' se usaba aquí pero estaba declarado en CarAirScript → ahora es un campo de esta clase.
    ///  • La física va en FixedUpdate (en Update dependía de los FPS: a más FPS, más frenado).
    ///  • Rigidbody.velocity se llama linearVelocity en Unity 6.
    ///  • CarAirScript va en su propio archivo (CarAirScript.cs): Unity solo deja añadir como
    ///    componente una clase cuyo archivo se llame igual que ella.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class WheelScript : MonoBehaviour
    {
        [Tooltip("Velocidad que se conserva en cada paso de física (0,95 = pierde un 5 %).")]
        [Range(0f, 1f)] public float friction = 0.95f;

        [Tooltip("Rebote al saltar/impacto.")]
        public float bounce = 3f;

        [Tooltip("Velocidad (m/s) a partir de la cual se aplica el rebote.")]
        public float speedBounce = 50f;

        private Rigidbody _rb;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody>(); // se busca una vez, no en cada frame
        }

        private void FixedUpdate()
        {
            // Simular rodadura (no la fricción real del suelo, sino pérdida de energía).
            Vector3 v = GetVelocity() * friction;

            // Pequeño impulso extra si la velocidad es alta (efecto visual/sonoro).
            if (v.magnitude > speedBounce)
            {
                v += v.normalized * bounce * 0.1f;
            }
            SetVelocity(v);
        }

        private Vector3 GetVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return _rb.linearVelocity;
#else
            return _rb.velocity;
#endif
        }

        private void SetVelocity(Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = v;
#else
            _rb.velocity = v;
#endif
        }
    }
}
