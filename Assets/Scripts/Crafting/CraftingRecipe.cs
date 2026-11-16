using System.Collections.Generic;
using MarsRobot.Items;

namespace MarsRobot.Crafting
{
    /// <summary>Dónde se fabrica cada receta (como las estaciones de un juego de supervivencia).</summary>
    public enum CraftStation
    {
        Hand,        // Inventario personal del astronauta (a mano)
        Workbench,   // Banco de trabajo
        Fabricator,  // Fabricador
        Smelter,     // Fundición
        Refinery,    // Refinería química (Sabatier, electrólisis, Bosch)
        Printer3D,   // Impresora 3D (nivel 3)
        Electronics, // Taller de electrónica (nivel 3)
        Garden,      // Huerto hidropónico (nivel 4): varias bandejas a la vez
        Lab,         // Laboratorio (nivel 4): análisis, semillas y nutrientes
        WaterPlant,  // Planta de agua (nivel 5)
        OxygenPlant, // Generador de oxígeno (nivel 5)
        Kitchen      // Cocina (nivel 5)
    }

    /// <summary>Ingrediente: objeto y cantidad.</summary>
    public struct Ingredient
    {
        public string ItemId;
        public int Count;

        public Ingredient(string itemId, int count)
        {
            ItemId = itemId;
            Count = count;
        }

        public ItemData Item
        {
            get { return ItemDatabase.Get(ItemId); }
        }
    }

    /// <summary>Plano de fabricación: ingredientes → resultado en un tiempo, en una estación.</summary>
    public sealed class CraftingRecipe
    {
        public readonly string Id;
        public readonly string OutputId;
        public readonly int OutputCount;
        public readonly float Seconds;
        public readonly CraftStation Station;
        public readonly Ingredient[] Ingredients;
        /// <summary>Nombre propio de la receta (si hay varias formas de obtener el mismo objeto).</summary>
        public readonly string Variant;

        public CraftingRecipe(string id, CraftStation station, string outputId, int outputCount, float seconds, string variant, params Ingredient[] ingredients)
        {
            Id = id;
            Station = station;
            OutputId = outputId;
            OutputCount = outputCount;
            Seconds = seconds;
            Variant = variant;
            Ingredients = ingredients;
        }

        public ItemData Output
        {
            get { return ItemDatabase.Get(OutputId); }
        }

        public string DisplayName
        {
            get
            {
                ItemData o = Output;
                string n = o != null ? o.itemName : OutputId;
                if (OutputCount > 1) n += " ×" + OutputCount;
                return string.IsNullOrEmpty(Variant) ? n : n + " (" + Variant + ")";
            }
        }

        /// <summary>Cuántas veces se puede fabricar con lo que hay en el contenedor.</summary>
        public int MaxCraftable(ItemContainer source)
        {
            if (Ingredients.Length == 0) return 99;
            int max = int.MaxValue;
            for (int i = 0; i < Ingredients.Length; i++)
            {
                ItemData item = Ingredients[i].Item;
                if (item == null) return 0;
                max = System.Math.Min(max, source.Count(item) / Ingredients[i].Count);
            }
            return max;
        }
    }

    /// <summary>
    /// Todos los planos del juego (todos disponibles desde el inicio). Cadena de producción marciana:
    /// mena → lingotes → piezas → electrónica, y CO₂ atmosférico → metano → polímero.
    /// </summary>
    public static class RecipeBook
    {
        private static List<CraftingRecipe> _all;

        /// <summary>Vuelve a crear las recetas (p. ej. tras cambiar de idioma, al empezar partida).</summary>
        public static void ResetCache()
        {
            _all = null;
        }

        public static IReadOnlyList<CraftingRecipe> All
        {
            get
            {
                if (_all == null) Build();
                return _all;
            }
        }

        public static string StationName(CraftStation s)
        {
            switch (s)
            {
                case CraftStation.Hand: return Tr.T("Inventario (a mano)");
                case CraftStation.Workbench: return Tr.T("Banco de trabajo");
                case CraftStation.Fabricator: return Tr.T("Fabricador");
                case CraftStation.Smelter: return Tr.T("Fundición");
                case CraftStation.Refinery: return Tr.T("Refinería química");
                case CraftStation.Printer3D: return Tr.T("Impresora 3D");
                case CraftStation.Electronics: return Tr.T("Taller de electrónica");
                case CraftStation.Garden: return Tr.T("Huerto hidropónico");
                case CraftStation.Lab: return Tr.T("Laboratorio");
                case CraftStation.WaterPlant: return Tr.T("Planta de agua");
                case CraftStation.OxygenPlant: return Tr.T("Generador de oxígeno");
                default: return Tr.T("Cocina");
            }
        }

        public static List<CraftingRecipe> For(CraftStation station)
        {
            var list = new List<CraftingRecipe>();
            foreach (CraftingRecipe r in All)
                if (r.Station == station) list.Add(r);
            return list;
        }

        private static Ingredient I(string id, int n)
        {
            return new Ingredient(id, n);
        }

        private static void Build()
        {
            _all = new List<CraftingRecipe>();
            const CraftStation H = CraftStation.Hand, W = CraftStation.Workbench, F = CraftStation.Fabricator,
                S = CraftStation.Smelter, R = CraftStation.Refinery;

            // A mano (inventario del astronauta).
            Add("cinta", H, ItemDatabase.SealTape, 2, 3f, null, I(ItemDatabase.Polymer, 1));
            Add("cepillo", H, ItemDatabase.CleaningBrush, 1, 5f, null, I(ItemDatabase.Polymer, 1), I(ItemDatabase.MetalParts, 1));

            // Fundición: minerales → materiales.
            Add("lingote", S, ItemDatabase.IronIngot, 1, 8f, null, I(ItemDatabase.IronOre, 2));
            Add("silicio_arcilla", S, ItemDatabase.SiliconRefined, 1, 10f, Tr.T("de arcillas"), I(ItemDatabase.Clays, 2));
            Add("silicio_regolito", S, ItemDatabase.SiliconRefined, 1, 14f, Tr.T("de regolito"), I(ItemDatabase.Regolith, 4));
            Add("vidrio", S, ItemDatabase.Glass, 1, 8f, null, I(ItemDatabase.Regolith, 3));
            Add("azufre", S, ItemDatabase.Sulfur, 1, 6f, null, I(ItemDatabase.Sulfates, 2));
            Add("agua_hielo", S, ItemDatabase.Water, 2, 4f, Tr.T("fundir hielo"), I(ItemDatabase.Ice, 1));
            Add("lingote_perclorato", S, ItemDatabase.Oxygen, 1, 10f, Tr.T("de percloratos"), I(ItemDatabase.PerchlorateRegolith, 2));

            // Refinería química: atmósfera y volátiles.
            Add("co2", R, ItemDatabase.Co2, 1, 10f, Tr.T("captar de la atmósfera"));
            Add("oxigeno", R, ItemDatabase.Oxygen, 1, 8f, Tr.T("electrólisis"), I(ItemDatabase.Water, 1));
            Add("metano", R, ItemDatabase.Methane, 2, 12f, "Sabatier", I(ItemDatabase.Co2, 1), I(ItemDatabase.Water, 1));
            Add("polimero", R, ItemDatabase.Polymer, 1, 10f, null, I(ItemDatabase.Methane, 2));
            Add("carbono", R, ItemDatabase.Carbon, 1, 12f, "Bosch", I(ItemDatabase.Co2, 2));

            // Fabricador: piezas y componentes.
            Add("piezas", F, ItemDatabase.MetalParts, 2, 5f, null, I(ItemDatabase.IronIngot, 1));
            Add("acero", F, ItemDatabase.Steel, 1, 8f, null, I(ItemDatabase.IronIngot, 2), I(ItemDatabase.Carbon, 1));
            Add("cableado", F, ItemDatabase.Wiring, 2, 6f, null, I(ItemDatabase.IronIngot, 1), I(ItemDatabase.Polymer, 1));
            Add("electronica", F, ItemDatabase.Electronics, 1, 10f, null, I(ItemDatabase.SiliconRefined, 1), I(ItemDatabase.Wiring, 1), I(ItemDatabase.Polymer, 1));
            Add("celula", F, ItemDatabase.SolarCell, 1, 12f, null, I(ItemDatabase.SiliconRefined, 2), I(ItemDatabase.Glass, 1), I(ItemDatabase.Wiring, 1));
            Add("celda_energia", F, ItemDatabase.PowerCell, 1, 15f, null, I(ItemDatabase.Electronics, 1), I(ItemDatabase.IronIngot, 2), I(ItemDatabase.Polymer, 1));
            Add("bateria_aux", F, ItemDatabase.AuxBattery, 1, 25f, null, I(ItemDatabase.Electronics, 2), I(ItemDatabase.Steel, 2), I(ItemDatabase.Polymer, 2));
            Add("rueda", F, ItemDatabase.SpareWheel, 1, 18f, null, I(ItemDatabase.Steel, 2), I(ItemDatabase.Polymer, 1), I(ItemDatabase.MetalParts, 2));

            // Banco de trabajo: montaje y construcción.
            Add("kit", W, ItemDatabase.RepairKit, 1, 10f, null, I(ItemDatabase.MetalParts, 2), I(ItemDatabase.Wiring, 1), I(ItemDatabase.SealTape, 1));
            Add("bloque", W, ItemDatabase.RegolithBrick, 2, 6f, null, I(ItemDatabase.Regolith, 3), I(ItemDatabase.Sulfur, 1));
            Add("repuestos", W, ItemDatabase.SpareParts, 2, 4f, null, I(ItemDatabase.MetalParts, 1), I(ItemDatabase.SealTape, 1));
            Add("cepillo_banco", W, ItemDatabase.CleaningBrush, 1, 3f, null, I(ItemDatabase.Polymer, 1), I(ItemDatabase.MetalParts, 1));

            // Impresora 3D: piezas de polímero y repuestos del robot.
            const CraftStation P = CraftStation.Printer3D, E = CraftStation.Electronics;
            Add("p3d_repuestos", P, ItemDatabase.SpareParts, 3, 8f, null, I(ItemDatabase.Polymer, 1), I(ItemDatabase.MetalParts, 1));
            Add("p3d_cepillo", P, ItemDatabase.CleaningBrush, 1, 6f, null, I(ItemDatabase.Polymer, 2));
            Add("p3d_rueda", P, ItemDatabase.SpareWheel, 1, 20f, null, I(ItemDatabase.Steel, 1), I(ItemDatabase.Polymer, 3));
            Add("p3d_cinta", P, ItemDatabase.SealTape, 4, 5f, null, I(ItemDatabase.Polymer, 1));
            // Taller de electrónica (mejor rendimiento que el Fabricador).
            Add("e_electronica", E, ItemDatabase.Electronics, 2, 12f, null, I(ItemDatabase.SiliconRefined, 1), I(ItemDatabase.Wiring, 1), I(ItemDatabase.Polymer, 1));
            Add("e_celula", E, ItemDatabase.SolarCell, 2, 14f, null, I(ItemDatabase.SiliconRefined, 2), I(ItemDatabase.Glass, 1), I(ItemDatabase.Wiring, 1));
            Add("e_celda", E, ItemDatabase.PowerCell, 1, 12f, null, I(ItemDatabase.Electronics, 1), I(ItemDatabase.IronIngot, 1), I(ItemDatabase.Polymer, 1));
            Add("e_bateria", E, ItemDatabase.AuxBattery, 1, 22f, null, I(ItemDatabase.Electronics, 2), I(ItemDatabase.Steel, 1), I(ItemDatabase.Polymer, 2));

            // Huerto: semillas + agua + nutrientes -> cosecha (cada bandeja cultiva en paralelo).
            const CraftStation G = CraftStation.Garden;
            float[] grow = { 90f, 70f, 180f, 200f, 160f, 170f };
            int[] harvest = { 2, 3, 4, 5, 4, 3 };
            for (int i = 0; i < ItemDatabase.Crops.Length; i++)
            {
                string crop = ItemDatabase.Crops[i];
                Add("cultivo_" + crop, G, crop, harvest[i], grow[i], null,
                    I(ItemDatabase.SeedOf(crop), 1), I(ItemDatabase.Water, 2), I(ItemDatabase.Nutrients, 1));
            }

            // Laboratorio: ciencia, nutrientes y semillas a partir de la cosecha.
            const CraftStation L = CraftStation.Lab;
            Add("lab_muestra", L, ItemDatabase.ResearchData, 3, 30f, Tr.T("analizar muestra"), I(ItemDatabase.Sample, 1));
            Add("lab_arcillas", L, ItemDatabase.ResearchData, 1, 20f, Tr.T("analizar arcillas"), I(ItemDatabase.Clays, 2));
            Add("lab_nutrientes", L, ItemDatabase.Nutrients, 2, 12f, null, I(ItemDatabase.Sulfates, 1), I(ItemDatabase.Water, 1));
            for (int i = 0; i < ItemDatabase.Crops.Length; i++)
                Add("lab_semillas_" + ItemDatabase.Crops[i], L, ItemDatabase.SeedOf(ItemDatabase.Crops[i]), 3, 15f, null, I(ItemDatabase.Crops[i], 1));

            // Planta de agua y generador de oxígeno.
            Add("agua_planta", CraftStation.WaterPlant, ItemDatabase.Water, 3, 8f, Tr.T("fundir y filtrar hielo"), I(ItemDatabase.Ice, 1));
            Add("agua_perclorato", CraftStation.WaterPlant, ItemDatabase.Water, 1, 20f, Tr.T("extraer del regolito"), I(ItemDatabase.PerchlorateRegolith, 3));
            Add("o2_electrolisis", CraftStation.OxygenPlant, ItemDatabase.Oxygen, 2, 10f, Tr.T("electrólisis"), I(ItemDatabase.Water, 1));
            Add("o2_perclorato", CraftStation.OxygenPlant, ItemDatabase.Oxygen, 1, 14f, Tr.T("de percloratos"), I(ItemDatabase.PerchlorateRegolith, 2));

            // Cocina.
            const CraftStation K = CraftStation.Kitchen;
            Add("harina", K, ItemDatabase.Flour, 1, 10f, null, I("trigo", 3));
            Add("racion", K, ItemDatabase.Ration, 2, 15f, null, I(ItemDatabase.Flour, 1), I("soja", 1), I("patata", 1));
            Add("ensalada", K, ItemDatabase.Salad, 1, 8f, null, I("lechuga", 1), I("tomate", 1), I("rabano", 1));
        }

        private static void Add(string id, CraftStation station, string output, int count, float seconds, string variant, params Ingredient[] ingredients)
        {
            _all.Add(new CraftingRecipe(id, station, output, count, seconds, variant, ingredients));
        }

        [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _all = null;
        }
    }
}
