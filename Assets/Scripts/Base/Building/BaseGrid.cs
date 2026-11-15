using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Energy;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Red de energía de la base. Sin cables: todo lo construido cerca del cohete comparte la red.
    /// Cada medio segundo:
    ///   1. Suma lo que generan el cohete, los paneles y el reactor.
    ///   2. Suma lo que piden las máquinas que pueden trabajar.
    ///   3. Si sobra, carga las baterías; si falta, tira de ellas; si aun así falta, todas las
    ///      máquinas trabajan más despacio en la misma proporción (apagón parcial).
    ///   4. Hace producir a cada máquina.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BaseGrid : MonoBehaviour
    {
        [Tooltip("Generador interno del cohete (W): permite arrancar las primeras máquinas.")]
        [SerializeField, Min(0f)] private float rocketGeneratorWatts = 1000f;

        [Tooltip("Batería interna del cohete (Wh).")]
        [SerializeField, Min(0f)] private float rocketBatteryWh = 2000f;

        [Tooltip("Segundos reales entre ciclos de simulación.")]
        [SerializeField, Min(0.1f)] private float tickSeconds = 0.5f;

        private readonly List<PlacedStructure> _structures = new List<PlacedStructure>();
        private readonly Dictionary<object, float> _externalDemand = new Dictionary<object, float>();

        /// <summary>Consumo de máquinas que no son estructuras (p. ej. la fundición). 0 para quitarlo.</summary>
        public void SetExternalDemand(object owner, float watts)
        {
            if (watts <= 0f) _externalDemand.Remove(owner);
            else _externalDemand[owner] = watts;
        }

        /// <summary>Multiplicador externo del suministro (tormentas magnéticas). 1 = normal.</summary>
        public float ExternalSupplyMultiplier { get; set; } = 1f;
        private RocketBase _base;
        private ISolarEnvironment _environment;
        private float _timer;

        public float GenerationWatts { get; private set; }

        /// <summary>
        /// Potencia instalada (W): lo que darían todos los generadores a pleno rendimiento.
        /// No sube y baja con el día y la noche, por eso sirve para el objetivo de "energía estable".
        /// </summary>
        public float InstalledWatts
        {
            get
            {
                float w = rocketGeneratorWatts;
                for (int i = 0; i < _structures.Count; i++)
                {
                    if (_structures[i] != null) w += _structures[i].Definition.generationWatts;
                }
                return w;
            }
        }
        public float DemandWatts { get; private set; }
        public float StoredWh { get; private set; }
        public float CapacityWh { get; private set; }

        /// <summary>Reparto de energía del último ciclo (1 = todo funciona a tope).</summary>
        public float SupplyRatio { get; private set; }

        public IReadOnlyList<PlacedStructure> Structures
        {
            get { return _structures; }
        }

        private void Awake()
        {
            _base = GetComponent<RocketBase>();
            StoredWh = rocketBatteryWh * 0.5f;
            SupplyRatio = 1f;
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        public void Register(PlacedStructure s)
        {
            if (!_structures.Contains(s)) _structures.Add(s);
        }

        public void Unregister(PlacedStructure s)
        {
            _structures.Remove(s);
        }

        /// <summary>Cuántas estructuras de un tipo hay construidas.</summary>
        public int Count(StructureKind kind)
        {
            int n = 0;
            for (int i = 0; i < _structures.Count; i++)
            {
                if (_structures[i] != null && _structures[i].Definition.kind == kind) n++;
            }
            return n;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            float realSeconds = tickSeconds - _timer;
            _timer = tickSeconds;
            Simulate(realSeconds);
        }

        private void Simulate(float realSeconds)
        {
            if (_environment == null)
            {
                MarsRobot.World.Weather.MarsEnvironmentSystem env;
                if (GameServices.TryGet(out env)) _environment = env;
            }

            float gameSeconds = realSeconds * MarsTime.TimeScale;
            float gameHours = gameSeconds / MarsTime.HourSeconds;

            // 1-2) Generación, demanda y capacidad.
            float gen = rocketGeneratorWatts, demand = 0f, capacity = rocketBatteryWh;
            for (int i = 0; i < _structures.Count; i++)
            {
                PlacedStructure s = _structures[i];
                float g = s.ComputeGeneration(_environment);
                s.LastGeneration = g;
                gen += g;
                demand += s.ComputeDemand();
                capacity += s.Definition.storageWh;
            }

            foreach (var kv in _externalDemand) demand += kv.Value;
            gen *= Mathf.Clamp01(ExternalSupplyMultiplier);

            // 3) Balance con las baterías.
            float ratio = 1f;
            float netWh = MarsTime.WattsToWh(gen - demand, gameSeconds);
            if (netWh >= 0f)
            {
                StoredWh = Mathf.Min(capacity, StoredWh + netWh);
            }
            else
            {
                float needWh = -netWh;
                if (StoredWh >= needWh)
                {
                    StoredWh -= needWh;
                }
                else
                {
                    // Lo que hay (generación + batería restante) se reparte entre todas.
                    float availableWh = MarsTime.WattsToWh(gen, gameSeconds) + StoredWh;
                    float demandWh = MarsTime.WattsToWh(demand, gameSeconds);
                    ratio = demandWh > 0f ? Mathf.Clamp01(availableWh / demandWh) : 1f;
                    StoredWh = 0f;
                }
            }

            GenerationWatts = gen;
            DemandWatts = demand;
            CapacityWh = capacity;
            SupplyRatio = ratio;

            // 4) Producción.
            float ice = IceFactor();
            for (int i = 0; i < _structures.Count; i++)
            {
                _structures[i].Tick(gameHours, ratio, _environment, ice);
            }
        }

        /// <summary>Cuánto hielo hay bajo la base (0..1) según la zona de aterrizaje.</summary>
        private float IceFactor()
        {
            var zone = _base != null ? _base.Zone : null;
            if (zone == null) return 0.3f;
            return zone.hasAccessibleWaterIce ? Mathf.Max(zone.iceAbundance, 0.5f) : Mathf.Max(zone.iceAbundance, 0.1f);
        }
    }
}
