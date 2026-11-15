using System;
using System.Collections.Generic;
using MarsRobot.Core;
using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>
    /// Integridad estructural del robot por módulos. NO hay barra de vida ni muerte:
    ///  • Ruedas/patas dañadas → menos movilidad y desvío de la dirección hacia el lado roto.
    ///  • Cables cortados → fallos del HUD, del minimapa o de los sensores.
    ///  • Chasis dañado → todo rinde un poco menos.
    ///  • Panel solar dañado → menos carga (lo lee el DustingSystem).
    /// Nada se cura solo: el jugador repara a mano con la herramienta correcta.
    ///
    /// Todos los cambios se publican mediante eventos para que la UI, el audio o el sistema de
    /// energía reaccionen sin que este componente los conozca.
    /// </summary>
    [DefaultExecutionOrder(-100)] // antes que la UI y el controlador, que leen sus módulos en OnEnable/Start
    [DisallowMultipleComponent]
    public sealed class RobotHealthSystem : MonoBehaviour
    {
        [SerializeField] private RobotHealthConfig config;

        [Tooltip("Multiplicador global de daño (lo ajusta la zona de aterrizaje según su dificultad).")]
        [SerializeField, Min(0f)] private float zoneDamageMultiplier = 1f;

        // ------------------------------------------------------------------
        // Eventos
        // ------------------------------------------------------------------

        /// <summary>Un módulo ha perdido integridad.</summary>
        public event Action<RobotModule, DamageInfo> ModuleDamaged;

        /// <summary>Un módulo ha ganado integridad por reparación.</summary>
        public event Action<RobotModule> ModuleRepaired;

        /// <summary>Un módulo ha cambiado de estado. El segundo parámetro es el estado ANTERIOR.</summary>
        public event Action<RobotModule, ModuleState> ModuleStateChanged;

        /// <summary>Un cable acaba de quedar cortado (integridad 0).</summary>
        public event Action<RobotModule> CableCut;

        /// <summary>La movilidad (0..1) ha cambiado.</summary>
        public event Action<float> MobilityChanged;

        /// <summary>La integridad estructural global (0..1) ha cambiado.</summary>
        public event Action<float> StructuralIntegrityChanged;

        // ------------------------------------------------------------------
        // Estado
        // ------------------------------------------------------------------

        private readonly List<RobotModule> _modules = new List<RobotModule>();
        private readonly Dictionary<RobotModuleId, RobotModule> _byId = new Dictionary<RobotModuleId, RobotModule>();
        private readonly List<RobotModule> _intactCables = new List<RobotModule>();
        private RobotHealthConfig _runtimeConfig;
        private System.Random _rng;

        public IReadOnlyList<RobotModule> Modules
        {
            get { return _modules; }
        }

        public RobotHealthConfig Config
        {
            get { return _runtimeConfig; }
        }

        /// <summary>Multiplicador de velocidad (0..1) que aplica el controlador.</summary>
        public float Mobility { get; private set; }

        /// <summary>Desvío de la dirección (−1 tira a la izquierda, +1 a la derecha).</summary>
        public float SteeringBias { get; private set; }

        /// <summary>Media ponderada de integridad de todos los módulos (0..1).</summary>
        public float StructuralIntegrity01 { get; private set; }

        /// <summary>True si el robot no puede moverse (todas las ruedas rotas).</summary>
        public bool IsImmobilized
        {
            get { return Mobility <= 0.01f; }
        }

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void Awake()
        {
            _rng = new System.Random(System.Environment.TickCount);
            if (config != null)
            {
                _runtimeConfig = config;
            }
            else
            {
                // Sin asset: configuración por defecto en memoria (útil para prototipar).
                _runtimeConfig = ScriptableObject.CreateInstance<RobotHealthConfig>();
                _runtimeConfig.name = "RobotHealthConfig (por defecto)";
            }
            BuildModules();
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
            if (config == null && _runtimeConfig != null) Destroy(_runtimeConfig);
        }

        private void BuildModules()
        {
            _modules.Clear();
            _byId.Clear();
            for (int i = 0; i < _runtimeConfig.modules.Count; i++)
            {
                RobotModuleDefinition def = _runtimeConfig.modules[i];
                if (_byId.ContainsKey(def.id))
                {
                    Debug.LogWarning("[RobotHealthSystem] Módulo duplicado ignorado: " + def.id, this);
                    continue;
                }
                var module = new RobotModule(def);
                _modules.Add(module);
                _byId.Add(def.id, module);
            }
            RecalculateAggregates(true);
        }

        // ------------------------------------------------------------------
        // Consultas
        // ------------------------------------------------------------------

        public RobotModule GetModule(RobotModuleId id)
        {
            RobotModule m;
            return _byId.TryGetValue(id, out m) ? m : null;
        }

        /// <summary>True si el módulo existe y no está roto (p. ej. "¿funciona el minimapa?").</summary>
        public bool IsFunctional(RobotModuleId id)
        {
            RobotModule m = GetModule(id);
            return m != null && !m.IsBroken;
        }

        /// <summary>Rendimiento 0..1 de un módulo (1 si no existe, para no penalizar configs sin él).</summary>
        public float GetEfficiency(RobotModuleId id)
        {
            RobotModule m = GetModule(id);
            return m != null ? m.Efficiency : 1f;
        }

        public void SetZoneDamageMultiplier(float multiplier)
        {
            zoneDamageMultiplier = Mathf.Max(0f, multiplier);
        }

        // ------------------------------------------------------------------
        // Daño
        // ------------------------------------------------------------------

        // ------------------------------------------------------------------
        // Mejoras (taller de la base)
        // ------------------------------------------------------------------

        /// <summary>Multiplica la probabilidad de corte de cable (cables blindados &lt; 1).</summary>
        public float CableCutChanceMultiplier { get; set; } = 1f;

        /// <summary>Blindaje: multiplicador de daño para todos los módulos de una categoría.</summary>
        public void SetArmor(ModuleCategory category, float damageMultiplier)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i].Category == category) _modules[i].ArmorMultiplier = Mathf.Max(0f, damageMultiplier);
            }
        }

        /// <summary>Blindaje de un módulo concreto.</summary>
        public void SetArmor(RobotModuleId id, float damageMultiplier)
        {
            RobotModule m = GetModule(id);
            if (m != null) m.ArmorMultiplier = Mathf.Max(0f, damageMultiplier);
        }

        /// <summary>Piezas más resistentes: más integridad máxima (conserva el porcentaje actual).</summary>
        public void SetMaxIntegrityMultiplier(ModuleCategory category, float multiplier)
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i].Category == category) _modules[i].SetMaxIntegrityMultiplier(multiplier);
            }
            RecalculateAggregates(false);
        }

        /// <summary>Aplica daño a un módulo concreto.</summary>
        public void ApplyDamage(RobotModuleId id, DamageInfo info)
        {
            RobotModule module = GetModule(id);
            if (module == null || info.Amount <= 0f || module.Integrity <= 0f) return;

            float amount = info.Amount * module.Definition.damageMultiplier * module.ArmorMultiplier * zoneDamageMultiplier;
            if (amount <= 0f) return;

            ModuleState previous = module.SetIntegrity(module.Integrity - amount);
            info.Amount = amount;
            if (ModuleDamaged != null) ModuleDamaged(module, info);
            NotifyStateChange(module, previous);
            RecalculateAggregates(false);
        }

        /// <summary>
        /// Choque físico. Reparte el daño entre el módulo de locomoción más cercano al punto de
        /// contacto y el chasis, y puede cortar un cable al azar si el golpe es fuerte.
        /// </summary>
        /// <param name="worldPoint">Punto de contacto en el mundo.</param>
        /// <param name="impactSpeed">Velocidad relativa en la dirección de la normal (m/s).</param>
        /// <param name="hazardMultiplier">Peligrosidad de la superficie (rocas afiladas &gt; 1).</param>
        public void ApplyImpact(Vector3 worldPoint, float impactSpeed, float hazardMultiplier)
        {
            RobotHealthConfig c = _runtimeConfig;
            if (impactSpeed < c.minImpactSpeed) return;

            float baseDamage = (impactSpeed - c.minImpactSpeed) * c.damagePerSpeedUnit * hazardMultiplier;
            Vector3 local = transform.InverseTransformPoint(worldPoint);

            RobotModule nearest = FindNearest(ModuleCategory.Locomotion, local);
            if (nearest != null)
            {
                ApplyDamage(nearest.Id, new DamageInfo(baseDamage * c.locomotionShare, DamageSourceType.Impact, worldPoint, impactSpeed));
            }
            float chassisShare = nearest != null ? 1f - c.locomotionShare : 1f;
            ApplyDamage(RobotModuleId.Chassis, new DamageInfo(baseDamage * chassisShare, DamageSourceType.Impact, worldPoint, impactSpeed));

            // Si el golpe es en la parte alta, el panel solar también sufre.
            if (local.y > 0.5f)
            {
                ApplyDamage(RobotModuleId.SolarPanel, new DamageInfo(baseDamage * 0.5f, DamageSourceType.Impact, worldPoint, impactSpeed));
            }

            // Posible corte de cable.
            if (impactSpeed > c.cableCutMinSpeed)
            {
                float chance = Mathf.Clamp01((impactSpeed - c.cableCutMinSpeed) * c.cableCutChancePerSpeedUnit * hazardMultiplier * CableCutChanceMultiplier);
                if (_rng.NextDouble() < chance) CutRandomCable(worldPoint, impactSpeed);
            }
        }

        /// <summary>
        /// Desgaste continuo (terreno difícil, arena, tormenta) repartido entre todos los módulos
        /// de una categoría que no estén rotos.
        /// </summary>
        public void ApplyWear(ModuleCategory category, float amount, DamageSourceType source)
        {
            if (amount <= 0f) return;
            int count = 0;
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i].Category == category && !_modules[i].IsBroken) count++;
            }
            if (count == 0) return;

            float perModule = amount / count;
            for (int i = 0; i < _modules.Count; i++)
            {
                RobotModule m = _modules[i];
                if (m.Category == category && !m.IsBroken)
                {
                    ApplyDamage(m.Id, new DamageInfo(perModule, source, transform.position, 0f));
                }
            }
        }

        private void CutRandomCable(Vector3 worldPoint, float impactSpeed)
        {
            _intactCables.Clear();
            for (int i = 0; i < _modules.Count; i++)
            {
                if (_modules[i].Category == ModuleCategory.Wiring && !_modules[i].IsBroken) _intactCables.Add(_modules[i]);
            }
            if (_intactCables.Count == 0) return;

            RobotModule cable = _intactCables[_rng.Next(_intactCables.Count)];
            // El corte es instantáneo: ignora blindaje y multiplicadores.
            ModuleState previous = cable.SetIntegrity(0f);
            if (ModuleDamaged != null)
            {
                ModuleDamaged(cable, new DamageInfo(cable.MaxIntegrity, DamageSourceType.Impact, worldPoint, impactSpeed));
            }
            NotifyStateChange(cable, previous);
            RecalculateAggregates(false);
        }

        private RobotModule FindNearest(ModuleCategory category, Vector3 localPoint)
        {
            RobotModule best = null;
            float bestDist = float.MaxValue;
            for (int i = 0; i < _modules.Count; i++)
            {
                RobotModule m = _modules[i];
                if (m.Category != category) continue;
                float d = (m.Definition.localMountPoint - localPoint).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = m;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------
        // Reparación
        // ------------------------------------------------------------------

        /// <summary>
        /// Repara un módulo durante <paramref name="deltaTime"/> segundos con una herramienta.
        /// Pensado para llamarse cada frame mientras el jugador mantiene la acción de reparar.
        /// </summary>
        public RepairResult Repair(RobotModuleId id, RepairToolType tool, float deltaTime)
        {
            RobotModule module = GetModule(id);
            if (module == null) return RepairResult.InvalidModule;
            if (!module.NeedsRepair) return RepairResult.AlreadyFull;
            if (!module.AcceptsTool(tool)) return RepairResult.WrongTool;

            // El kit de servos es más rápido en ruedas que la llave.
            float speed = tool == RepairToolType.ServoKit ? 1.5f : 1f;
            float amount = module.Definition.repairPerSecond * speed * deltaTime;

            ModuleState previous = module.SetIntegrity(module.Integrity + amount);
            if (ModuleRepaired != null) ModuleRepaired(module);
            NotifyStateChange(module, previous);
            RecalculateAggregates(false);

            return module.NeedsRepair ? RepairResult.Repaired : RepairResult.FullyRepaired;
        }

        /// <summary>Repara por completo un módulo (eventos de diálogo, misiones…).</summary>
        public void RepairModule(RobotModuleId id)
        {
            RobotModule m = GetModule(id);
            if (m == null) return;
            ModuleState previous = m.SetIntegrity(m.MaxIntegrity);
            if (ModuleRepaired != null) ModuleRepaired(m);
            NotifyStateChange(m, previous);
            RecalculateAggregates(false);
        }

        /// <summary>Restaura todo (debug o al reconstruir el robot en la base).</summary>
        public void RepairAll()
        {
            for (int i = 0; i < _modules.Count; i++)
            {
                RobotModule m = _modules[i];
                ModuleState previous = m.SetIntegrity(m.MaxIntegrity);
                NotifyStateChange(m, previous);
            }
            RecalculateAggregates(false);
        }

        // ------------------------------------------------------------------
        // Valores agregados
        // ------------------------------------------------------------------

        private void NotifyStateChange(RobotModule module, ModuleState previous)
        {
            if (module.State == previous) return;
            if (ModuleStateChanged != null) ModuleStateChanged(module, previous);
            if (module.Category == ModuleCategory.Wiring && module.IsBroken && CableCut != null) CableCut(module);
        }

        private void RecalculateAggregates(bool forceNotify)
        {
            float leftSum = 0f, rightSum = 0f, allSum = 0f;
            int leftCount = 0, rightCount = 0, allCount = 0;
            float weightedIntegrity = 0f, totalWeight = 0f;

            for (int i = 0; i < _modules.Count; i++)
            {
                RobotModule m = _modules[i];
                weightedIntegrity += m.Integrity;
                totalWeight += m.MaxIntegrity;

                if (m.Category != ModuleCategory.Locomotion) continue;
                float e = m.Efficiency;
                allSum += e;
                allCount++;
                if (m.Definition.side == RobotSide.Left) { leftSum += e; leftCount++; }
                else if (m.Definition.side == RobotSide.Right) { rightSum += e; rightCount++; }
            }

            float locomotion = allCount > 0 ? allSum / allCount : 1f;
            // Un chasis muy dañado limita la movilidad (hasta un 50 %).
            float chassis = Mathf.Lerp(0.5f, 1f, GetEfficiency(RobotModuleId.Chassis));
            float mobility = locomotion * chassis;

            float left = leftCount > 0 ? leftSum / leftCount : 1f;
            float right = rightCount > 0 ? rightSum / rightCount : 1f;
            // Si el lado izquierdo rinde menos, el derecho empuja más y el robot gira a la izquierda (valor negativo).
            float bias = Mathf.Clamp(left - right, -1f, 1f);

            float structural = totalWeight > 0f ? weightedIntegrity / totalWeight : 1f;

            bool mobilityChanged = forceNotify || !Mathf.Approximately(mobility, Mobility);
            bool structuralChanged = forceNotify || !Mathf.Approximately(structural, StructuralIntegrity01);

            Mobility = mobility;
            SteeringBias = bias;
            StructuralIntegrity01 = structural;

            if (mobilityChanged && MobilityChanged != null) MobilityChanged(Mobility);
            if (structuralChanged && StructuralIntegrityChanged != null) StructuralIntegrityChanged(StructuralIntegrity01);
        }
    }
}
