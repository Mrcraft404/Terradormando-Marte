using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>
    /// Se coloca en rocas, campos de piedras afiladas, dunas, etc. para indicar cuánto dañan al
    /// robot. Sin este componente, una superficie cuenta como multiplicador 1 y sin desgaste.
    /// </summary>
    public sealed class SurfaceHazard : MonoBehaviour
    {
        [Tooltip("Multiplicador del daño por impacto (roca afilada 1.5–2, arena 0.3).")]
        [Min(0f)] public float impactMultiplier = 1f;

        [Tooltip("Desgaste por segundo a las ruedas mientras se está en contacto y moviéndose.")]
        [Min(0f)] public float wearPerSecond = 0f;

        [Tooltip("Multiplicador de velocidad mientras se está encima (arena blanda < 1).")]
        [Range(0.1f, 1.5f)] public float speedMultiplier = 1f;
    }
}
