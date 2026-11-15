namespace MarsRobot.Robot
{
    /// <summary>
    /// Cualquier componente del robot que cambie cómo se mueve: la carga (masa), los módulos
    /// equipados, las tormentas magnéticas… El RobotController multiplica todos los que encuentre.
    /// Así esos sistemas no dependen unos de otros.
    /// </summary>
    public interface IMovementModifier
    {
        /// <summary>Multiplicador de la velocidad máxima (1 = sin cambio).</summary>
        float SpeedMultiplier { get; }

        /// <summary>Multiplicador del consumo de los motores (1 = sin cambio).</summary>
        float PowerMultiplier { get; }

        /// <summary>Multiplicador del desgaste de ruedas en terreno difícil (1 = sin cambio).</summary>
        float WearMultiplier { get; }
    }
}
