using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>
    /// Configuración del sistema de integridad: qué módulos tiene el robot y cómo le afectan
    /// los golpes. Crear con: Assets ▸ Create ▸ Marte ▸ Robot ▸ Configuración de integridad.
    /// Si el RobotHealthSystem no tiene ninguna asignada, usa <see cref="CreateDefaultModules"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "RobotHealthConfig", menuName = "Marte/Robot/Configuración de integridad")]
    public class RobotHealthConfig : ScriptableObject
    {
        [Header("Módulos")]
        public List<RobotModuleDefinition> modules = CreateDefaultModules();

        [Header("Impactos")]
        [Tooltip("Velocidad de choque (m/s) por debajo de la cual no hay daño.")]
        [Min(0f)] public float minImpactSpeed = 2.5f;

        [Tooltip("Daño por cada m/s por encima del mínimo.")]
        [Min(0f)] public float damagePerSpeedUnit = 9f;

        [Tooltip("Parte del daño que recibe el módulo de locomoción más cercano al golpe (el resto va al chasis).")]
        [Range(0f, 1f)] public float locomotionShare = 0.7f;

        [Tooltip("Velocidad a partir de la cual un golpe puede cortar un cable.")]
        [Min(0f)] public float cableCutMinSpeed = 4f;

        [Tooltip("Probabilidad de corte de cable por cada m/s por encima del mínimo.")]
        [Range(0f, 1f)] public float cableCutChancePerSpeedUnit = 0.12f;

        [Header("Terreno difícil")]
        [Tooltip("Pendiente (grados) a partir de la cual las ruedas se desgastan al moverse.")]
        [Range(0f, 90f)] public float roughSlopeDegrees = 22f;

        [Tooltip("Desgaste por segundo repartido entre las ruedas en pendiente fuerte, a velocidad máxima.")]
        [Min(0f)] public float roughTerrainWearPerSecond = 0.8f;

        [Header("Dirección")]
        [Tooltip("Grados por segundo de desvío cuando un lado está completamente roto.")]
        [Min(0f)] public float maxSteeringPullDegPerSec = 35f;

        /// <summary>Módulos por defecto: 4 ruedas, 3 mazos de cables, chasis y panel solar.</summary>
        public static List<RobotModuleDefinition> CreateDefaultModules()
        {
            return new List<RobotModuleDefinition>
            {
                Wheel(RobotModuleId.WheelFrontLeft, "Rueda delantera izquierda", RobotSide.Left, new Vector3(-0.8f, -0.4f, 1f)),
                Wheel(RobotModuleId.WheelFrontRight, "Rueda delantera derecha", RobotSide.Right, new Vector3(0.8f, -0.4f, 1f)),
                Wheel(RobotModuleId.WheelRearLeft, "Rueda trasera izquierda", RobotSide.Left, new Vector3(-0.8f, -0.4f, -1f)),
                Wheel(RobotModuleId.WheelRearRight, "Rueda trasera derecha", RobotSide.Right, new Vector3(0.8f, -0.4f, -1f)),
                Cable(RobotModuleId.CableHud, "Cableado de visor (HUD)", new Vector3(0f, 0.6f, 0.6f)),
                Cable(RobotModuleId.CableMinimap, "Cableado de minimapa", new Vector3(0f, 0.4f, 0f)),
                Cable(RobotModuleId.CableSensors, "Cableado de sensores", new Vector3(0f, 0.2f, 1.1f)),
                new RobotModuleDefinition
                {
                    id = RobotModuleId.Chassis,
                    category = ModuleCategory.Structure,
                    displayName = "Chasis",
                    maxIntegrity = 200f,
                    localMountPoint = Vector3.zero,
                    damageMultiplier = 0.6f,
                    validTools = new[] { RepairToolType.Welder, RepairToolType.Wrench },
                    repairPerSecond = 10f
                },
                new RobotModuleDefinition
                {
                    id = RobotModuleId.SolarPanel,
                    category = ModuleCategory.Power,
                    displayName = "Panel solar",
                    maxIntegrity = 80f,
                    localMountPoint = new Vector3(0f, 0.9f, -0.3f),
                    damageMultiplier = 1.2f,
                    validTools = new[] { RepairToolType.Wrench, RepairToolType.Welder },
                    repairPerSecond = 8f
                },
            };
        }

        private static RobotModuleDefinition Wheel(RobotModuleId id, string name, RobotSide side, Vector3 mount)
        {
            return new RobotModuleDefinition
            {
                id = id,
                category = ModuleCategory.Locomotion,
                displayName = name,
                maxIntegrity = 100f,
                side = side,
                localMountPoint = mount,
                damageMultiplier = 1f,
                validTools = new[] { RepairToolType.Wrench, RepairToolType.ServoKit },
                repairPerSecond = 12f
            };
        }

        private static RobotModuleDefinition Cable(RobotModuleId id, string name, Vector3 mount)
        {
            return new RobotModuleDefinition
            {
                id = id,
                category = ModuleCategory.Wiring,
                displayName = name,
                maxIntegrity = 40f,
                localMountPoint = mount,
                damageMultiplier = 1f,
                validTools = new[] { RepairToolType.CableKit },
                repairPerSecond = 6f
            };
        }
    }
}
