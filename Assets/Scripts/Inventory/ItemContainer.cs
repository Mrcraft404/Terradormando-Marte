using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Items
{
    /// <summary>Una pila de objetos en una ranura.</summary>
    [Serializable]
    public sealed class ItemStack
    {
        public ItemData item;
        public int count;
        [Tooltip("Estado de conservación 0..1 (baja con la sublimación o la corrosión).")]
        public float condition = 1f;
        [NonSerialized] public float lossAccumulator;

        public ItemStack(ItemData item, int count)
        {
            this.item = item;
            this.count = count;
        }

        public float MassKg
        {
            get { return item.massKg * count; }
        }

        public float VolumeLiters
        {
            get { return item.volumeLiters * count; }
        }
    }

    /// <summary>
    /// Contenedor genérico con ranuras y, opcionalmente, límites de masa y volumen. Lo usan la
    /// bahía de carga del robot (RobotInventory) y los almacenes (StorageContainer).
    /// </summary>
    [Serializable]
    public sealed class ItemContainer
    {
        private readonly List<ItemStack> _slots = new List<ItemStack>();

        /// <summary>Ranuras máximas (0 = sin límite).</summary>
        public int MaxSlots;
        /// <summary>Masa máxima en kg (0 = sin límite).</summary>
        public float MaxMassKg;
        /// <summary>Volumen máximo en litros (0 = sin límite).</summary>
        public float MaxVolumeLiters;

        public event Action Changed;

        public IReadOnlyList<ItemStack> Slots
        {
            get { return _slots; }
        }

        public float TotalMassKg { get; private set; }
        public float TotalVolumeLiters { get; private set; }

        public float MassRatio
        {
            get { return MaxMassKg > 0f ? TotalMassKg / MaxMassKg : 0f; }
        }

        public float VolumeRatio
        {
            get { return MaxVolumeLiters > 0f ? TotalVolumeLiters / MaxVolumeLiters : 0f; }
        }

        public ItemContainer(int maxSlots, float maxMassKg, float maxVolumeLiters)
        {
            MaxSlots = maxSlots;
            MaxMassKg = maxMassKg;
            MaxVolumeLiters = maxVolumeLiters;
        }

        public int Count(ItemData item)
        {
            int n = 0;
            for (int i = 0; i < _slots.Count; i++)
            {
                if (_slots[i].item == item) n += _slots[i].count;
            }
            return n;
        }

        /// <summary>Cuántas unidades caben de verdad (por masa, volumen y ranuras).</summary>
        public int HowManyFit(ItemData item, int wanted)
        {
            int fit = wanted;
            if (MaxMassKg > 0f) fit = Mathf.Min(fit, Mathf.FloorToInt((MaxMassKg - TotalMassKg) / item.massKg + 0.0001f));
            if (MaxVolumeLiters > 0f) fit = Mathf.Min(fit, Mathf.FloorToInt((MaxVolumeLiters - TotalVolumeLiters) / item.volumeLiters + 0.0001f));
            if (fit <= 0) return 0;

            if (MaxSlots > 0)
            {
                // Hueco en pilas existentes + ranuras libres.
                int room = 0;
                for (int i = 0; i < _slots.Count; i++)
                {
                    if (_slots[i].item == item) room += item.maxStackSize - _slots[i].count;
                }
                room += (MaxSlots - _slots.Count) * item.maxStackSize;
                fit = Mathf.Min(fit, room);
            }
            return Mathf.Max(0, fit);
        }

        /// <summary>Añade hasta <paramref name="count"/> unidades. Devuelve cuántas entraron.</summary>
        public int Add(ItemData item, int count, float condition = 1f)
        {
            if (item == null || count <= 0) return 0;
            int toAdd = HowManyFit(item, count);
            int left = toAdd;

            // Completar pilas existentes.
            for (int i = 0; i < _slots.Count && left > 0; i++)
            {
                ItemStack s = _slots[i];
                if (s.item != item || s.count >= item.maxStackSize) continue;
                int n = Mathf.Min(left, item.maxStackSize - s.count);
                s.condition = (s.condition * s.count + condition * n) / (s.count + n);
                s.count += n;
                left -= n;
            }
            // Pilas nuevas.
            while (left > 0 && (MaxSlots <= 0 || _slots.Count < MaxSlots))
            {
                int n = Mathf.Min(left, item.maxStackSize);
                _slots.Add(new ItemStack(item, n) { condition = condition });
                left -= n;
            }
            Recalculate();
            return toAdd - left;
        }

        /// <summary>Quita hasta <paramref name="count"/> unidades de un objeto. Devuelve cuántas quitó.</summary>
        public int Remove(ItemData item, int count)
        {
            int left = count;
            for (int i = _slots.Count - 1; i >= 0 && left > 0; i--)
            {
                ItemStack s = _slots[i];
                if (s.item != item) continue;
                int n = Mathf.Min(left, s.count);
                s.count -= n;
                left -= n;
                if (s.count <= 0) _slots.RemoveAt(i);
            }
            Recalculate();
            return count - left;
        }

        /// <summary>Quita unidades de una ranura concreta.</summary>
        public int RemoveFromSlot(ItemStack stack, int count)
        {
            int idx = _slots.IndexOf(stack);
            if (idx < 0) return 0;
            int n = Mathf.Min(count, stack.count);
            stack.count -= n;
            if (stack.count <= 0) _slots.RemoveAt(idx);
            Recalculate();
            return n;
        }

        /// <summary>Divide una pila en dos (la mitad pasa a una ranura nueva) si hay ranura libre.</summary>
        public bool Split(ItemStack stack)
        {
            if (stack.count < 2 || (MaxSlots > 0 && _slots.Count >= MaxSlots)) return false;
            int half = stack.count / 2;
            stack.count -= half;
            _slots.Insert(_slots.IndexOf(stack) + 1, new ItemStack(stack.item, half) { condition = stack.condition });
            Recalculate();
            return true;
        }

        /// <summary>Mueve hasta <paramref name="count"/> unidades de una pila a otro contenedor.</summary>
        public int MoveTo(ItemStack stack, ItemContainer target, int count)
        {
            int moved = target.Add(stack.item, Mathf.Min(count, stack.count), stack.condition);
            if (moved > 0) RemoveFromSlot(stack, moved);
            return moved;
        }

        /// <summary>Recalcula totales y avisa. Llamar tras modificar pilas directamente.</summary>
        public void Recalculate()
        {
            float m = 0f, v = 0f;
            for (int i = 0; i < _slots.Count; i++)
            {
                m += _slots[i].MassKg;
                v += _slots[i].VolumeLiters;
            }
            TotalMassKg = m;
            TotalVolumeLiters = v;
            if (Changed != null) Changed();
        }
    }
}
