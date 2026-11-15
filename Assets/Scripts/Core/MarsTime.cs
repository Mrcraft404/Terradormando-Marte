using UnityEngine;

namespace MarsRobot.Core
{
    /// <summary>
    /// Reloj y constantes de tiempo marciano compartidos por todos los sistemas
    /// (energía, polvo, clima y, más adelante, agricultura).
    /// </summary>
    public static class MarsTime
    {
        /// <summary>Duración de un sol (día solar marciano) en segundos terrestres: 24 h 39 min 35 s.</summary>
        public const float SolSeconds = 88775f;

        /// <summary>Segundos por hora. Las tasas de energía se expresan en vatios-hora.</summary>
        public const float HourSeconds = 3600f;

        private static float _timeScale = 60f;

        /// <summary>
        /// Segundos de juego por segundo real. 60 = un minuto de juego por segundo
        /// (un sol completo dura unos 25 minutos reales). Afecta a clima, polvo y consumo.
        /// </summary>
        public static float TimeScale
        {
            get { return _timeScale; }
            set { _timeScale = Mathf.Max(0f, value); }
        }

        /// <summary>Segundos de juego transcurridos en este frame.</summary>
        public static float GameDeltaTime
        {
            get { return Time.deltaTime * _timeScale; }
        }

        /// <summary>Convierte una potencia (W) aplicada durante <paramref name="seconds"/> segundos a vatios-hora.</summary>
        public static float WattsToWh(float watts, float seconds)
        {
            return watts * seconds / HourSeconds;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _timeScale = 60f;
        }
    }
}
