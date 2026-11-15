using System;
using MarsRobot.Core;
using UnityEngine;

namespace MarsRobot.Energy
{
    /// <summary>
    /// Batería del robot. Solo guarda y entrega energía; quién la carga (paneles, base) y
    /// quién la gasta (motores, herramientas) son otros sistemas.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BatterySystem : MonoBehaviour, IEnergyStorage
    {
        [SerializeField, Min(1f)] private float capacityWh = 2000f;
        [SerializeField, Range(0f, 1f)] private float startCharge01 = 0.8f;

        [Tooltip("Por debajo de esta carga se lanza LowCharge (aviso en el HUD).")]
        [SerializeField, Range(0f, 1f)] private float lowChargeThreshold = 0.15f;

        [Tooltip("Cambio mínimo de carga (0..1) para lanzar ChargeChanged; evita eventos cada frame.")]
        [SerializeField, Range(0.0001f, 0.05f)] private float notifyStep = 0.002f;

        private float _currentWh;
        private float _lastNotified = -1f;
        private bool _wasLow;
        private bool _wasDepleted;

        public event Action<float> ChargeChanged;
        public event Action Depleted;

        /// <summary>La carga ha bajado del umbral de aviso.</summary>
        public event Action LowCharge;

        public float CapacityWh
        {
            get { return capacityWh; }
        }

        public float CurrentWh
        {
            get { return _currentWh; }
        }

        public float Charge01
        {
            get { return capacityWh > 0f ? _currentWh / capacityWh : 0f; }
        }

        public bool IsDepleted
        {
            get { return _currentWh <= 0.0001f; }
        }

        private void Awake()
        {
            _currentWh = capacityWh * startCharge01;
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        /// <summary>Amplía la capacidad (mejora del taller). La energía guardada no cambia.</summary>
        public void AddCapacity(float wh)
        {
            capacityWh = Mathf.Max(1f, capacityWh + wh);
            _currentWh = Mathf.Min(_currentWh, capacityWh);
            AfterChange();
        }

        public bool TryConsume(float wh)
        {
            if (wh <= 0f) return true;
            if (_currentWh < wh) return false;
            _currentWh -= wh;
            AfterChange();
            return true;
        }

        public float ConsumeUpTo(float wh)
        {
            if (wh <= 0f) return 0f;
            float used = Mathf.Min(wh, _currentWh);
            _currentWh -= used;
            AfterChange();
            return used;
        }

        public float AddEnergy(float wh)
        {
            if (wh <= 0f) return 0f;
            float added = Mathf.Min(wh, capacityWh - _currentWh);
            _currentWh += added;
            AfterChange();
            return added;
        }

        private void AfterChange()
        {
            float c = Charge01;
            if (Mathf.Abs(c - _lastNotified) >= notifyStep || c <= 0f || c >= 1f)
            {
                _lastNotified = c;
                if (ChargeChanged != null) ChargeChanged(c);
            }

            bool low = c <= lowChargeThreshold;
            if (low && !_wasLow && LowCharge != null) LowCharge();
            _wasLow = low;

            bool depleted = IsDepleted;
            if (depleted && !_wasDepleted && Depleted != null) Depleted();
            _wasDepleted = depleted;
        }
    }
}
