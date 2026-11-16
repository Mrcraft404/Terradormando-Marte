using MarsRobot.Core;
using MarsRobot.Energy;
using MarsRobot.Items;
using MarsRobot.Robot;
using MarsRobot.World.SpaceWeather;
using MarsRobot.World.Weather;
using UnityEngine;

namespace MarsRobot.Dialogue
{
    /// <summary>
    /// Vigila los sistemas del rover y lanza avisos rápidos en el HUD:
    /// polvo en paneles, batería baja, cables cortados, módulos rotos, radiación (sensor RAD),
    /// tormentas de polvo, sobrecarga y pérdida de volátiles en la bahía.
    /// Solo escucha eventos y estados públicos: no modifica nada.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotAlertMonitor : MonoBehaviour
    {
        [Tooltip("Polvo (0..1) a partir del cual se avisa.")]
        [SerializeField, Range(0f, 1f)] private float dustAlert = 0.8f;

        [Tooltip("Dosis de fondo en superficie (mGy/día). El instrumento RAD midió ~0,67 en Gale.")]
        [SerializeField, Min(0f)] private float backgroundDoseMilliGray = 0.67f;

        private RobotHealthSystem _health;
        private BatterySystem _battery;
        private DustingSystem _dusting;
        private RobotInventory _inventory;
        private bool _dustWarned, _overloadWarned, _radWarned, _stormWarned;
        private float _timer;

        /// <summary>Dosis de radiación actual estimada (mGy/día), para el HUD.</summary>
        public float DoseMilliGrayPerDay { get; private set; }

        private void Awake()
        {
            _health = GetComponent<RobotHealthSystem>();
            _battery = GetComponent<BatterySystem>();
            _dusting = GetComponent<DustingSystem>();
            _inventory = GetComponent<RobotInventory>();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.CableCut += OnCableCut;
                _health.ModuleStateChanged += OnModuleState;
            }
            if (_battery != null) _battery.LowCharge += OnLowCharge;
            if (_inventory != null) _inventory.ItemLost += OnItemLost;
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.CableCut -= OnCableCut;
                _health.ModuleStateChanged -= OnModuleState;
            }
            if (_battery != null) _battery.LowCharge -= OnLowCharge;
            if (_inventory != null) _inventory.ItemLost -= OnItemLost;
        }

        private void OnCableCut(RobotModule m)
        {
            NotificationSystem.Notify(Tr.T("¡Cable cortado: ") + m.Definition.displayName + Tr.T("! Repáralo con el kit de empalme (R)."), NotificationLevel.Critical, 7f);

            // La primera vez que se corta el de sensores, la IA del cohete ofrece ayuda
            // (diálogo de ejemplo en Assets/Resources/Dialogue).
            if (m.Id == RobotModuleId.CableSensors && !_offeredRemoteRepair)
            {
                _offeredRemoteRepair = true;
                DialogueManager.Play("ejemplo_pregunta");
            }
        }

        private bool _offeredRemoteRepair;

        private void OnModuleState(RobotModule m, ModuleState previous)
        {
            if (m.State == ModuleState.Broken && m.Category != ModuleCategory.Wiring)
            {
                NotificationSystem.Notify(m.Definition.displayName + Tr.T(" inutilizado."), NotificationLevel.Critical, 6f);
            }
            else if (m.State == ModuleState.Critical && previous != ModuleState.Broken)
            {
                NotificationSystem.Notify(m.Definition.displayName + Tr.T(": integridad crítica."), NotificationLevel.Warning);
            }
        }

        private void OnLowCharge()
        {
            NotificationSystem.Notify(Tr.T("Batería baja: vuelve a la plataforma de carga o busca el sol."), NotificationLevel.Warning, 6f);
        }

        private void OnItemLost(ItemData item, int count, string how)
        {
            NotificationSystem.Notify(item.itemName + " ×" + count + " " + how + Tr.T(" (falta contenedor criogénico)."), NotificationLevel.Warning);
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 1f;

            // Polvo en paneles (con histéresis para no repetir).
            if (_dusting != null)
            {
                if (!_dustWarned && _dusting.Dust01 >= dustAlert)
                {
                    _dustWarned = true;
                    NotificationSystem.Notify(Tr.T("Polvo acumulado en paneles solares al ") + Mathf.RoundToInt(_dusting.Dust01 * 100f) + Tr.T(" %. Límpialos (C) o usa el soplador."), NotificationLevel.Warning, 7f);
                }
                else if (_dusting.Dust01 < dustAlert * 0.7f) _dustWarned = false;
            }

            // Sobrecarga.
            if (_inventory != null && _inventory.Bay != null)
            {
                if (!_overloadWarned && _inventory.MassRatio > 0.9f)
                {
                    _overloadWarned = true;
                    NotificationSystem.Notify(Tr.T("Sobrecarga: los servos se calientan. Riesgo de daño en terreno irregular."), NotificationLevel.Warning);
                }
                else if (_inventory.MassRatio < 0.8f) _overloadWarned = false;
            }

            // Radiación: fondo + partículas solares (SEP), atenuada por la berma si estás en la base.
            float dose = backgroundDoseMilliGray * (SpaceWeatherState.SepActive ? 20f * Mathf.Max(0.3f, SpaceWeatherState.SepIntensity) : 1f);
            MarsRobot.Base.RocketBaseManager mgr;
            if (GameServices.TryGet(out mgr) && Vector3.Distance(mgr.transform.position, transform.position) < MarsRobot.Base.RocketBaseManager.BermRadius + 2f)
            {
                dose *= 1f - mgr.RadiationReduction01;
            }
            DoseMilliGrayPerDay = dose;
            if (!_radWarned && dose > backgroundDoseMilliGray * 3f)
            {
                _radWarned = true;
                NotificationSystem.Notify(Tr.T("¡Alerta! Niveles de radiación elevados detectados por el sensor RAD (") + dose.ToString("0.0") + Tr.T(" mGy/día). Busca refugio tras la berma."), NotificationLevel.Critical, 8f);
            }
            else if (dose < backgroundDoseMilliGray * 1.5f) _radWarned = false;

            // Tormentas de polvo.
            MarsEnvironmentSystem env;
            if (GameServices.TryGet(out env))
            {
                if (env.IsDustStormActive && !_stormWarned)
                {
                    _stormWarned = true;
                    NotificationSystem.Notify(Tr.T("Tormenta de polvo: visibilidad y energía solar reducidas."), NotificationLevel.Warning, 7f);
                }
                else if (!env.IsDustStormActive) _stormWarned = false;
            }
        }
    }
}
