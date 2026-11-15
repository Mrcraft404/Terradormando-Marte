using System;

namespace MarsRobot.Energy
{
    /// <summary>
    /// Almacén de energía (batería del robot y, más adelante, baterías de la base).
    /// Unidades: vatios-hora (Wh).
    /// </summary>
    public interface IEnergyStorage
    {
        float CapacityWh { get; }
        float CurrentWh { get; }
        float Charge01 { get; }
        bool IsDepleted { get; }

        /// <summary>Intenta gastar energía. Devuelve false (y no gasta nada) si no hay suficiente.</summary>
        bool TryConsume(float wh);

        /// <summary>Gasta lo que pueda hasta <paramref name="wh"/> y devuelve cuánto ha gastado.</summary>
        float ConsumeUpTo(float wh);

        /// <summary>Añade energía y devuelve la que realmente cabía.</summary>
        float AddEnergy(float wh);

        /// <summary>Carga 0..1 tras cada cambio relevante.</summary>
        event Action<float> ChargeChanged;

        /// <summary>La batería llegó a 0.</summary>
        event Action Depleted;
    }

    /// <summary>
    /// Lo que el entorno marciano le dice al sistema de energía y polvo. Lo implementa el
    /// sistema de clima (<c>MarsEnvironmentSystem</c>) y se podrá sustituir por otro (p. ej. en tests).
    /// </summary>
    public interface ISolarEnvironment
    {
        /// <summary>Irradiancia solar actual en superficie (W/m²), ya con noche y tormenta aplicadas.</summary>
        float SolarIrradiance { get; }

        /// <summary>Fracción de panel que se cubre de polvo por hora (0..1) en este momento.</summary>
        float DustDepositionPerHour { get; }

        /// <summary>True durante una tormenta de polvo.</summary>
        bool IsDustStormActive { get; }

        /// <summary>Ráfaga o remolino de polvo que limpia parte de los paneles (0..1 de limpieza).</summary>
        event Action<float> CleaningGust;
    }

    /// <summary>Cualquier sistema que consuma energía de forma continua (motores, sensores, calefacción).</summary>
    public interface IPowerConsumer
    {
        /// <summary>Potencia que pide ahora mismo (W).</summary>
        float CurrentPowerDrawWatts { get; }
    }
}
