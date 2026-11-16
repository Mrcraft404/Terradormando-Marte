using System;
using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Items
{
    /// <summary>
    /// Bahía de carga del rover con DOS límites a la vez: masa (kg) y volumen (L).
    ///  • &gt; 75 % de masa: va más lento y los motores gastan más batería.
    ///  • &gt; 90 % de masa: estrés térmico de los servos → más desgaste de ruedas en terreno difícil.
    ///  • 100 % de masa o volumen: no se puede recoger nada más (vacía en el cohete o suelta objetos).
    ///  • Volátiles (hielo, muestras) se pierden poco a poco sin módulo criogénico.
    ///  • Lo corrosivo (percloratos) desgasta el chasis si no hay contenedor sellado.
    /// La capacidad la fijan el chasis, los motores mejorados y los módulos de la bahía interna.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RobotInventory : MonoBehaviour, IMovementModifier
    {
        /// <summary>Gravedad de Marte (m/s²): para mostrar el peso real de la carga.</summary>
        public const float MarsGravity = 3.721f;

        [Header("Capacidad base (sin módulos)")]
        [SerializeField, Min(1f)] private float baseMassCapacityKg = 60f;
        [SerializeField, Min(1f)] private float baseVolumeLiters = 40f;
        [SerializeField, Min(1)] private int slotCount = 24;

        [Header("Efectos de la sobrecarga")]
        [Range(0f, 1f)] [SerializeField] private float slowdownStart = 0.75f;
        [Range(0f, 1f)] [SerializeField] private float thermalStressStart = 0.9f;
        [Tooltip("Velocidad con la carga al 100 %.")]
        [Range(0.1f, 1f)] [SerializeField] private float speedAtFullLoad = 0.55f;
        [Tooltip("Consumo de motores con la carga al 100 %.")]
        [Min(1f)] [SerializeField] private float powerAtFullLoad = 1.6f;

        [Header("Corrosión")]
        [Tooltip("Desgaste del chasis por hora de juego y por kg de material corrosivo sin sellar.")]
        [Min(0f)] [SerializeField] private float corrosionWearPerKgHour = 0.15f;

        private ItemContainer _bay;
        private float _bonusMassKg, _bonusVolumeLiters;
        private float _timer;
        private RobotHealthSystem _health;

        /// <summary>El contenido de la bahía.</summary>
        public ItemContainer Bay
        {
            get { return _bay; }
        }

        /// <summary>Lo ponen los módulos: contenedor criogénico (evita la sublimación).</summary>
        public bool HasCryoStorage { get; set; }

        /// <summary>Lo ponen los módulos: contenedor sellado (evita la corrosión).</summary>
        public bool HasSealedContainer { get; set; }

        /// <summary>Se perdió parte de un objeto (para notificaciones).</summary>
        public event Action<ItemData, int, string> ItemLost;

        public event Action Changed;

        public float MassRatio
        {
            get { return _bay.MassRatio; }
        }

        public float VolumeRatio
        {
            get { return _bay.VolumeRatio; }
        }

        /// <summary>Peso real de la carga en Marte (newtons).</summary>
        public float WeightNewtons
        {
            get { return _bay.TotalMassKg * MarsGravity; }
        }

        public bool IsFull
        {
            get { return _bay.MassRatio >= 0.999f || _bay.VolumeRatio >= 0.999f; }
        }

        private void Awake()
        {
            _bay = new ItemContainer(slotCount, baseMassCapacityKg, baseVolumeLiters);
            _bay.Changed += () => { if (Changed != null) Changed(); };
            _health = GetComponent<RobotHealthSystem>();
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        /// <summary>Lo llama el sistema de módulos al cambiar la configuración del rover.</summary>
        public void SetCapacityBonus(float extraMassKg, float extraVolumeLiters)
        {
            _bonusMassKg = extraMassKg;
            _bonusVolumeLiters = extraVolumeLiters;
            RecalculateCapacity();
        }

        /// <summary>Capacidad de masa: chasis + motores/chasis mejorados en el taller + módulos.</summary>
        public void RecalculateCapacity()
        {
            float upgrades = 0f;
            var up = GetComponent<RobotUpgrades>();
            if (up != null) upgrades = 20f * (up.GetLevel("motors") + up.GetLevel("chassis"));
            _bay.MaxMassKg = baseMassCapacityKg + _bonusMassKg + upgrades;
            _bay.MaxVolumeLiters = baseVolumeLiters + _bonusVolumeLiters;
            _bay.Recalculate();
        }

        /// <summary>Intenta guardar objetos. Devuelve cuántos entraron (puede ser menos de los pedidos).</summary>
        public int TryAdd(ItemData item, int count, float condition = 1f)
        {
            return _bay.Add(item, count, condition);
        }

        // ------------------------------------------------------------------
        // IMovementModifier: efectos de la masa
        // ------------------------------------------------------------------

        public float SpeedMultiplier
        {
            get
            {
                float r = _bay.MassRatio;
                if (r <= slowdownStart) return 1f;
                return Mathf.Lerp(1f, speedAtFullLoad, Mathf.InverseLerp(slowdownStart, 1f, r));
            }
        }

        public float PowerMultiplier
        {
            get
            {
                float r = _bay.MassRatio;
                if (r <= slowdownStart) return 1f;
                return Mathf.Lerp(1f, powerAtFullLoad, Mathf.InverseLerp(slowdownStart, 1f, r));
            }
        }

        public float WearMultiplier
        {
            get
            {
                float r = _bay.MassRatio;
                // Estrés térmico de los servos: hasta ×4 de desgaste al 100 %.
                return r <= thermalStressStart ? 1f : Mathf.Lerp(1f, 4f, Mathf.InverseLerp(thermalStressStart, 1f, r));
            }
        }

        /// <summary>Texto del estado de carga para el HUD.</summary>
        public string LoadStatus
        {
            get
            {
                float r = _bay.MassRatio;
                if (IsFull) return "<color=#ff6060>Bahía llena</color>";
                if (r > thermalStressStart) return "<color=#ff8040>Sobrecarga: estrés térmico en servos</color>";
                if (r > slowdownStart) return "<color=#ffd060>Carga pesada: más lento y más consumo</color>";
                return "";
            }
        }

        // ------------------------------------------------------------------
        // Degradación (volátiles y corrosión), una vez por segundo real
        // ------------------------------------------------------------------

        private void Update()
        {
            _timer += Time.deltaTime;
            if (_timer < 1f) return;
            float gameHours = _timer * MarsTime.TimeScale / MarsTime.HourSeconds;
            _timer = 0f;

            float corrosiveKg = 0f;
            bool changed = false;
            var slots = _bay.Slots;
            for (int i = slots.Count - 1; i >= 0; i--)
            {
                ItemStack s = slots[i];
                if (s.item.isCorrosiveOrToxic) corrosiveKg += s.MassKg;
                if (!s.item.requiresCryoStorage || HasCryoStorage) continue;

                s.lossAccumulator += s.count * s.item.sublimationPerHour * gameHours;
                s.condition = Mathf.Max(0f, s.condition - s.item.sublimationPerHour * gameHours);
                int lost = Mathf.FloorToInt(s.lossAccumulator);
                if (lost <= 0) continue;
                s.lossAccumulator -= lost;
                ItemData item = s.item;
                _bay.RemoveFromSlot(s, lost);
                changed = true;
                if (ItemLost != null) ItemLost(item, lost, item.itemCategory == ItemCategory.Volatile ? Tr.T("se ha sublimado") : Tr.T("se ha degradado"));
            }

            if (corrosiveKg > 0f && !HasSealedContainer && _health != null)
            {
                _health.ApplyWear(ModuleCategory.Structure, corrosiveKg * corrosionWearPerKgHour * gameHours, DamageSourceType.Environment);
            }
            if (changed) _bay.Recalculate();
        }
    }
}
