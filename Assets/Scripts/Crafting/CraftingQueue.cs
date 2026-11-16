using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Items;
using UnityEngine;

namespace MarsRobot.Crafting
{
    /// <summary>
    /// Cola de fabricación de una estación (o del inventario personal): como en los juegos de
    /// supervivencia, los ingredientes se descuentan al encargar y el resultado aparece en el mismo
    /// inventario al terminar cada unidad. Cancelar devuelve los ingredientes de lo no fabricado.
    /// En modo Creativo no gasta ingredientes y fabrica casi al instante.
    /// </summary>
    public sealed class CraftingQueue
    {
        public sealed class Entry
        {
            public CraftingRecipe Recipe;
            public int Remaining;
            public bool Paid;
            public float Progress;
        }

        private readonly List<Entry> _entries = new List<Entry>();

        /// <summary>
        /// Encargos que avanzan a la vez (1 en las máquinas; una por bandeja en el huerto). Con más de
        /// una, cada encargo ocupa su propia bandeja y no se admiten más encargos que bandejas.
        /// </summary>
        public int Lanes { get; set; } = 1;

        public IReadOnlyList<Entry> Entries
        {
            get { return _entries; }
        }

        /// <summary>Progreso 0..1 de la unidad en curso.</summary>
        public float Progress01
        {
            get { return ProgressOf(0); }
        }

        /// <summary>Progreso 0..1 del encargo <paramref name="index"/> (0 si está en espera).</summary>
        public float ProgressOf(int index)
        {
            if (index < 0 || index >= _entries.Count || index >= Lanes) return 0f;
            return Mathf.Clamp01(_entries[index].Progress / Duration(_entries[index].Recipe));
        }

        /// <summary>Último aviso (p. ej. "inventario lleno").</summary>
        public string Status { get; private set; } = "";

        public event System.Action<CraftingRecipe> Crafted;

        public static float Duration(CraftingRecipe r)
        {
            return GameModeState.IsCreative ? 0.3f : r.Seconds;
        }

        /// <summary>Encarga <paramref name="count"/> unidades (las que se puedan pagar). Devuelve cuántas.</summary>
        public int Enqueue(CraftingRecipe recipe, int count, ItemContainer source)
        {
            if (recipe == null || count <= 0) return 0;
            bool free = GameModeState.IsCreative;
            if (Lanes > 1 && _entries.Count >= Lanes)
            {
                Status = Tr.T("Todas las bandejas están ocupadas: espera a la cosecha.");
                return 0;
            }
            int n = free ? count : Mathf.Min(count, recipe.MaxCraftable(source));
            if (n <= 0)
            {
                Status = Tr.T("Faltan materiales para ") + recipe.DisplayName + ".";
                return 0;
            }
            if (!free)
            {
                for (int i = 0; i < recipe.Ingredients.Length; i++)
                    source.Remove(recipe.Ingredients[i].Item, recipe.Ingredients[i].Count * n);
            }
            // Si el último encargo es la misma receta, se suma (como una pila).
            Entry last = _entries.Count > 0 ? _entries[_entries.Count - 1] : null;
            if (Lanes == 1 && last != null && last.Recipe == recipe && last.Paid == !free) last.Remaining += n;
            else _entries.Add(new Entry { Recipe = recipe, Remaining = n, Paid = !free });
            Status = "";
            return n;
        }

        /// <summary>Cancela un encargo y devuelve los ingredientes de lo pendiente.</summary>
        public void Cancel(int index, ItemContainer source)
        {
            if (index < 0 || index >= _entries.Count) return;
            Entry e = _entries[index];
            if (e.Paid)
            {
                for (int i = 0; i < e.Recipe.Ingredients.Length; i++)
                    source.Add(e.Recipe.Ingredients[i].Item, e.Recipe.Ingredients[i].Count * e.Remaining);
            }
            _entries.RemoveAt(index);
        }

        public void Tick(float deltaTime, ItemContainer output)
        {
            int active = Mathf.Min(Lanes, _entries.Count);
            for (int i = active - 1; i >= 0; i--)
            {
                Entry e = _entries[i];
                e.Progress += deltaTime;
                if (e.Progress < Duration(e.Recipe)) continue;

                ItemData item = e.Recipe.Output;
                if (item == null || output.HowManyFit(item, e.Recipe.OutputCount) < e.Recipe.OutputCount)
                {
                    Status = Tr.T("Inventario lleno: saca objetos para seguir.");
                    e.Progress = Duration(e.Recipe); // espera con la unidad terminada
                    continue;
                }
                output.Add(item, e.Recipe.OutputCount);
                Status = "";
                e.Progress = 0f;
                e.Remaining--;
                if (e.Remaining <= 0) _entries.RemoveAt(i);
                if (Crafted != null) Crafted(e.Recipe);
            }
        }
    }
}
