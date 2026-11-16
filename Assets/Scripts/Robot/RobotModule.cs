using System;
using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>Familia de un módulo: determina qué efecto tiene su daño.</summary>
    public enum ModuleCategory
    {
        /// <summary>Ruedas o patas: reducen la movilidad.</summary>
        Locomotion,
        /// <summary>Cables: provocan fallos de interfaz (HUD, minimapa, sensores).</summary>
        Wiring,
        /// <summary>Chasis: reparte daño y amortigua; si cae, todo rinde menos.</summary>
        Structure,
        /// <summary>Paneles solares y bus de energía (conecta con DustingSystem).</summary>
        Power
    }

    /// <summary>Identificador de cada módulo físico del robot.</summary>
    public enum RobotModuleId
    {
        WheelFrontLeft,
        WheelFrontRight,
        WheelRearLeft,
        WheelRearRight,
        CableHud,
        CableMinimap,
        CableSensors,
        Chassis,
        SolarPanel
    }

    /// <summary>Estado según la integridad restante.</summary>
    public enum ModuleState
    {
        Operational, // ≥ 75 %
        Damaged,     // < 75 %
        Critical,    // < 35 %
        Broken       // 0 % (rueda bloqueada, cable cortado…)
    }

    /// <summary>Herramientas de reparación manual.</summary>
    public enum RepairToolType
    {
        None,
        Wrench,   // llave: ruedas, patas y chasis
        CableKit, // kit de empalme: cables
        Welder,   // soldador: chasis y estructura
        ServoKit  // kit de servos: ruedas/patas (más rápido que la llave)
    }

    /// <summary>Lado del robot, para el desvío de dirección cuando las ruedas de un lado fallan.</summary>
    public enum RobotSide
    {
        Center,
        Left,
        Right
    }

    /// <summary>Origen del daño, útil para efectos de sonido, registro y logros.</summary>
    public enum DamageSourceType
    {
        Impact,
        RoughTerrain,
        Storm,
        Environment,
        Scripted
    }

    /// <summary>Resultado de un intento de reparación.</summary>
    public enum RepairResult
    {
        Repaired,       // ha subido la integridad
        FullyRepaired,  // ha llegado al 100 %
        WrongTool,      // herramienta no válida para este módulo
        AlreadyFull,    // no necesitaba reparación
        InvalidModule
    }

    /// <summary>Información de un golpe o desgaste.</summary>
    public struct DamageInfo
    {
        public float Amount;
        public DamageSourceType Source;
        public Vector3 WorldPoint;
        public float ImpactSpeed;

        public DamageInfo(float amount, DamageSourceType source, Vector3 worldPoint, float impactSpeed)
        {
            Amount = amount;
            Source = source;
            WorldPoint = worldPoint;
            ImpactSpeed = impactSpeed;
        }
    }

    /// <summary>Definición editable de un módulo (vive en <see cref="RobotHealthConfig"/>).</summary>
    [Serializable]
    public class RobotModuleDefinition
    {
        public RobotModuleId id;
        public ModuleCategory category;
        public string displayName = Tr.T("Módulo");
        [Min(1f)] public float maxIntegrity = 100f;
        public RobotSide side = RobotSide.Center;

        [Tooltip("Punto de montaje en espacio local del robot. Un golpe cerca de este punto daña más este módulo.")]
        public Vector3 localMountPoint;

        [Tooltip("Multiplicador de daño recibido (blindaje < 1, frágil > 1).")]
        [Min(0f)] public float damageMultiplier = 1f;

        [Tooltip("Herramientas que sirven para repararlo y su velocidad relativa.")]
        public RepairToolType[] validTools = { RepairToolType.Wrench };

        [Tooltip("Integridad que recupera por segundo con la herramienta adecuada.")]
        [Min(0.1f)] public float repairPerSecond = 12f;
    }

    /// <summary>
    /// Estado en tiempo de ejecución de un módulo. Solo lo modifica <see cref="RobotHealthSystem"/>.
    /// </summary>
    public sealed class RobotModule
    {
        public const float DamagedThreshold = 0.75f;
        public const float CriticalThreshold = 0.35f;

        public RobotModuleDefinition Definition { get; private set; }
        public float Integrity { get; private set; }
        public ModuleState State { get; private set; }

        private float _maxMultiplier = 1f;

        /// <summary>Integridad máxima actual (la del diseño × mejoras del taller).</summary>
        public float MaxIntegrity
        {
            get { return Definition.maxIntegrity * _maxMultiplier; }
        }

        /// <summary>Multiplicador de daño por mejoras de blindaje (1 = sin blindaje extra).</summary>
        public float ArmorMultiplier { get; internal set; } = 1f;

        /// <summary>Cambia la integridad máxima conservando el porcentaje actual.</summary>
        internal void SetMaxIntegrityMultiplier(float multiplier)
        {
            float ratio = Integrity01;
            _maxMultiplier = Mathf.Max(0.1f, multiplier);
            Integrity = ratio * MaxIntegrity;
        }

        public RobotModuleId Id
        {
            get { return Definition.id; }
        }

        public ModuleCategory Category
        {
            get { return Definition.category; }
        }

        public float Integrity01
        {
            get { return Integrity / MaxIntegrity; }
        }

        public bool IsBroken
        {
            get { return State == ModuleState.Broken; }
        }

        public bool NeedsRepair
        {
            get { return Integrity < MaxIntegrity - 0.01f; }
        }

        /// <summary>
        /// Rendimiento del módulo (0..1). Hasta el 75 % funciona al 100 %; por debajo baja
        /// de forma lineal hasta el 30 %, y si se rompe vale 0.
        /// </summary>
        public float Efficiency
        {
            get
            {
                if (State == ModuleState.Broken) return 0f;
                float i = Integrity01;
                if (i >= DamagedThreshold) return 1f;
                return Mathf.Lerp(0.3f, 1f, i / DamagedThreshold);
            }
        }

        public RobotModule(RobotModuleDefinition definition)
        {
            Definition = definition;
            Integrity = definition.maxIntegrity;
            State = ModuleState.Operational;
        }

        /// <summary>Cambia la integridad y devuelve el estado anterior.</summary>
        internal ModuleState SetIntegrity(float value)
        {
            ModuleState previous = State;
            Integrity = Mathf.Clamp(value, 0f, MaxIntegrity);
            State = EvaluateState(Integrity01);
            return previous;
        }

        public bool AcceptsTool(RepairToolType tool)
        {
            if (Definition.validTools == null) return false;
            for (int i = 0; i < Definition.validTools.Length; i++)
            {
                if (Definition.validTools[i] == tool) return true;
            }
            return false;
        }

        public static ModuleState EvaluateState(float integrity01)
        {
            if (integrity01 <= 0.0001f) return ModuleState.Broken;
            if (integrity01 < CriticalThreshold) return ModuleState.Critical;
            if (integrity01 < DamagedThreshold) return ModuleState.Damaged;
            return ModuleState.Operational;
        }
    }
}
