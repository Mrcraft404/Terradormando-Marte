using System;
using System.Collections.Generic;
using MarsRobot.Core;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Recursos que se guardan en la base. Los "brutos" se recogen; el resto se fabrican.</summary>
    public enum ResourceType
    {
        // Brutos (se recogerán con el robot; de momento vienen en la carga del cohete)
        Regolith,     // Regolito (suelo marciano)
        Iron,         // Hierro
        Silicon,      // Silicio
        Ice,          // Hielo de agua
        // Fabricados en la impresora
        MetalParts,   // Piezas metálicas
        Wiring,       // Cableado
        SolarCells,   // Células solares
        RegolithBricks, // Bloques de regolito (para construir)
        Water,        // Agua (huerto, colonos). Limitada por los tanques.
        // Soporte vital y ciencia
        Oxygen,       // Oxígeno (extractor). Limitado por los tanques.
        Samples,      // Muestras geológicas (taladro del robot)
        Research,     // Puntos de investigación (laboratorio)
        Methane       // Metano (propelente) de la refinería Sabatier. Limitado por los tanques del cohete.
    }

    /// <summary>Cantidad de un recurso (para costes y recetas).</summary>
    [Serializable]
    public struct ResourceAmount
    {
        public ResourceType type;
        [Min(0)] public int amount;

        public ResourceAmount(ResourceType type, int amount)
        {
            this.type = type;
            this.amount = amount;
        }
    }

    /// <summary>
    /// Almacén de la base (bodega del cohete). Todas las máquinas leen y gastan de aquí.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BaseInventory : MonoBehaviour
    {
        private readonly Dictionary<ResourceType, int> _stock = new Dictionary<ResourceType, int>();

        /// <summary>Cambio en un recurso: tipo y nueva cantidad.</summary>
        public event Action<ResourceType, int> Changed;

        private void Awake()
        {
            GameServices.Register(this);
        }

        private void OnDestroy()
        {
            GameServices.Unregister(this);
        }

        public int Get(ResourceType type)
        {
            int v;
            return _stock.TryGetValue(type, out v) ? v : 0;
        }

        /// <summary>
        /// Añade recursos. Devuelve cuánto se añadió de verdad (el oxígeno y el agua tienen límite
        /// según los tanques construidos; el resto no tiene límite).
        /// </summary>
        public int Add(ResourceType type, int amount)
        {
            if (amount <= 0) return 0;
            int current = Get(type);
            int cap = GetCapacity(type);
            int added = cap >= 0 ? Mathf.Clamp(cap - current, 0, amount) : amount;
            if (added <= 0) return 0;
            _stock[type] = current + added;
            if (Changed != null) Changed(type, _stock[type]);
            return added;
        }

        // ------------------------------------------------------------------
        // Capacidad (tanques)
        // ------------------------------------------------------------------

        private readonly Dictionary<ResourceType, int> _capacity = new Dictionary<ResourceType, int>
        {
            // Lo que caben en los depósitos internos del cohete.
            { ResourceType.Oxygen, 50 },
            { ResourceType.Water, 50 },
            { ResourceType.Methane, 400 }, // tanques de propelente del cohete
        };

        /// <summary>Capacidad máxima, o −1 si no tiene límite.</summary>
        public int GetCapacity(ResourceType type)
        {
            int c;
            return _capacity.TryGetValue(type, out c) ? c : -1;
        }

        /// <summary>Amplía (o reduce, si es negativo) la capacidad: lo usan los tanques.</summary>
        public void AddCapacity(ResourceType type, int amount)
        {
            int c = GetCapacity(type);
            if (c < 0) return;
            _capacity[type] = Mathf.Max(0, c + amount);
            if (Get(type) > _capacity[type]) _stock[type] = _capacity[type];
            if (Changed != null) Changed(type, Get(type));
        }

        /// <summary>True si el recurso tiene límite y está lleno.</summary>
        public bool IsFull(ResourceType type)
        {
            int c = GetCapacity(type);
            return c >= 0 && Get(type) >= c;
        }

        public bool Has(IList<ResourceAmount> cost, int multiplier = 1)
        {
            for (int i = 0; i < cost.Count; i++)
            {
                if (Get(cost[i].type) < cost[i].amount * multiplier) return false;
            }
            return true;
        }

        /// <summary>Gasta todo el coste o nada.</summary>
        public bool TrySpend(IList<ResourceAmount> cost, int multiplier = 1)
        {
            if (!Has(cost, multiplier)) return false;
            for (int i = 0; i < cost.Count; i++)
            {
                ResourceType t = cost[i].type;
                _stock[t] = Get(t) - cost[i].amount * multiplier;
                if (Changed != null) Changed(t, _stock[t]);
            }
            return true;
        }

        /// <summary>Nombre en español para la interfaz.</summary>
        // ------------------------------------------------------------------
        // Masa (para la capacidad de almacenaje de la nave)
        // ------------------------------------------------------------------

        /// <summary>Capacidad de almacenaje de la nave nodriza (kg).</summary>
        public float MaxMassKg { get; set; } = 5000f;

        /// <summary>Masa total almacenada (kg).</summary>
        public float TotalMassKg
        {
            get
            {
                float m = 0f;
                foreach (var kv in _stock) m += kv.Value * MassPerUnit(kv.Key);
                return m;
            }
        }

        /// <summary>Masa aproximada de una unidad de cada recurso (kg).</summary>
        public static float MassPerUnit(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Regolith: return 6f;
                case ResourceType.Iron: return 5f;
                case ResourceType.Silicon: return 3f;
                case ResourceType.Ice: return 4f;
                case ResourceType.MetalParts: return 2f;
                case ResourceType.Wiring: return 1f;
                case ResourceType.SolarCells: return 2f;
                case ResourceType.RegolithBricks: return 10f;
                case ResourceType.Water: return 1f;
                case ResourceType.Oxygen: return 1f;
                case ResourceType.Samples: return 0.5f;
                case ResourceType.Methane: return 0.45f;
                default: return 0f; // investigación: no ocupa
            }
        }

        /// <summary>Categoría para la interfaz (minerales, metales, agua…).</summary>
        public static string CategoryLabel(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Regolith: case ResourceType.Silicon: case ResourceType.Samples: return "Mineral";
                case ResourceType.Iron: case ResourceType.MetalParts: return "Metal";
                case ResourceType.Ice: case ResourceType.Water: case ResourceType.Oxygen: return "Soporte vital";
                case ResourceType.Methane: return "Combustible";
                case ResourceType.Research: return "Ciencia";
                default: return "Material";
            }
        }

        public static string Label(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Regolith: return "Regolito";
                case ResourceType.Iron: return "Hierro";
                case ResourceType.Silicon: return "Silicio";
                case ResourceType.Ice: return "Hielo";
                case ResourceType.MetalParts: return "Piezas metálicas";
                case ResourceType.Wiring: return "Cableado";
                case ResourceType.SolarCells: return "Células solares";
                case ResourceType.RegolithBricks: return "Bloques de regolito";
                case ResourceType.Water: return "Agua";
                case ResourceType.Oxygen: return "Oxígeno";
                case ResourceType.Samples: return "Muestras";
                case ResourceType.Research: return "Investigación";
                default: return "Metano (propelente)";
            }
        }

        /// <summary>Texto "3 Hierro + 1 Silicio" para mostrar costes.</summary>
        public static string Describe(IList<ResourceAmount> cost, int multiplier = 1)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < cost.Count; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append(cost[i].amount * multiplier).Append(' ').Append(Label(cost[i].type));
            }
            return sb.ToString();
        }
    }
}
