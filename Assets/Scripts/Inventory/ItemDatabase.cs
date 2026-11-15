using System.Collections.Generic;
using MarsRobot.Base;
using UnityEngine;

namespace MarsRobot.Items
{
    /// <summary>
    /// Catálogo de objetos. Al primer uso crea los objetos por defecto en memoria y luego carga los
    /// assets de Resources/Items, que sustituyen a los de igual itemID. Así el juego funciona aunque
    /// no exista ningún asset, y los diseñadores pueden ajustar valores creando assets.
    /// </summary>
    public static class ItemDatabase
    {
        // Identificadores usados por el código.
        public const string Regolith = "regolito";
        public const string PerchlorateRegolith = "regolito_perclorato";
        public const string IronOre = "mena_hierro";
        public const string Ice = "hielo_utopia";
        public const string Clays = "arcillas_crism";
        public const string Sulfates = "sulfatos";
        public const string Sample = "muestra_geologica";
        public const string AuxBattery = "bateria_auxiliar";
        public const string SpareParts = "piezas_repuesto";

        private static Dictionary<string, ItemData> _items;

        public static IEnumerable<ItemData> All
        {
            get
            {
                Ensure();
                return _items.Values;
            }
        }

        /// <summary>
        /// Objeto "transportable" que representa un recurso del almacén de la nave (lingote de hierro,
        /// bidón de agua…). Se crea al vuelo y, al devolverlo a la nave, vuelve a ser el mismo recurso 1:1.
        /// </summary>
        public static ItemData ForResource(ResourceType type)
        {
            Ensure();
            string id = "recurso_" + type;
            ItemData d;
            if (_items.TryGetValue(id, out d)) return d;
            float kg = Mathf.Max(0.05f, BaseInventory.MassPerUnit(type));
            d = Make(id, BaseInventory.Label(type), ItemCategory.RobotComponent, kg, kg * 0.8f, 20, ResourceColor(type),
                "Recurso del almacén de la nave preparado para el transporte.", BaseInventory.CategoryLabel(type), type, 1,
                cryo: type == ResourceType.Ice, sublimation: 0.05f);
            if (type == ResourceType.Regolith || type == ResourceType.Iron || type == ResourceType.Silicon) d.itemCategory = ItemCategory.Mineral;
            if (type == ResourceType.Ice || type == ResourceType.Water || type == ResourceType.Oxygen || type == ResourceType.Methane) d.itemCategory = ItemCategory.Volatile;
            if (type == ResourceType.Ice) d.requiresCryoStorage = true;
            _items[id] = d;
            return d;
        }

        /// <summary>Color identificativo de cada recurso (iconos).</summary>
        public static Color ResourceColor(ResourceType type)
        {
            switch (type)
            {
                case ResourceType.Regolith: return new Color(0.62f, 0.37f, 0.23f);
                case ResourceType.Iron: return new Color(0.55f, 0.3f, 0.25f);
                case ResourceType.Silicon: return new Color(0.7f, 0.78f, 0.9f);
                case ResourceType.Ice: return new Color(0.75f, 0.9f, 1f);
                case ResourceType.MetalParts: return new Color(0.65f, 0.66f, 0.7f);
                case ResourceType.Wiring: return new Color(0.75f, 0.45f, 0.25f);
                case ResourceType.SolarCells: return new Color(0.2f, 0.3f, 0.6f);
                case ResourceType.RegolithBricks: return new Color(0.7f, 0.42f, 0.28f);
                case ResourceType.Water: return new Color(0.35f, 0.65f, 1f);
                case ResourceType.Oxygen: return new Color(0.45f, 0.75f, 0.95f);
                case ResourceType.Samples: return new Color(0.9f, 0.85f, 0.4f);
                case ResourceType.Methane: return new Color(0.9f, 0.35f, 0.15f);
                default: return new Color(0.6f, 0.9f, 0.6f);
            }
        }

        public static ItemData Get(string id)
        {
            Ensure();
            ItemData d;
            return _items.TryGetValue(id, out d) ? d : null;
        }

        private static void Ensure()
        {
            if (_items != null) return;
            _items = new Dictionary<string, ItemData>();

            // Valores aproximados: densidad del regolito ~1,5 kg/L, hielo ~0,92 kg/L, hematita ~5 kg/L
            // (la mena va mezclada con roca, de ahí la densidad menor).
            Add(Make(Regolith, "Regolito", ItemCategory.Mineral, 6f, 4f, 20, new Color(0.62f, 0.37f, 0.23f),
                "Suelo marciano suelto: polvo y arena basáltica. Materia prima de bloques y blindaje.",
                "Basalto con óxidos de hierro (nanofase) que le dan el color rojo.", ResourceType.Regolith, 1));
            Add(Make(PerchlorateRegolith, "Regolito con percloratos", ItemCategory.Mineral, 6f, 4f, 20, new Color(0.75f, 0.45f, 0.3f),
                "Regolito con sales de perclorato: tóxico para personas y plantas. Corrosivo si no va sellado.",
                "Percloratos de calcio y magnesio (~0,5–1 % en peso, como midió la sonda Phoenix). " +
                "Al calentarlos liberan oxígeno.", ResourceType.Regolith, 1, corrosive: true));
            Add(Make(IronOre, "Mena de hierro", ItemCategory.Mineral, 9f, 3f, 10, new Color(0.45f, 0.2f, 0.15f),
                "Roca rica en óxidos de hierro. Se funde en la fundición del cohete.",
                "Hematita y magnetita detectadas por espectrometría orbital (CRISM).", ResourceType.Iron, 2));
            Add(Make(Ice, "Hielo de Utopia Planitia", ItemCategory.Volatile, 4f, 4.3f, 10, new Color(0.75f, 0.9f, 1f),
                "Hielo de agua del subsuelo. Se sublima si no viaja en un contenedor criogénico.",
                "H₂O con algo de polvo y sales. Firma espectral de hielo a 1,5 y 2,0 µm.", ResourceType.Ice, 1,
                cryo: true, sublimation: 0.08f));
            Add(Make(Clays, "Arcillas (filosilicatos)", ItemCategory.Mineral, 5f, 3.5f, 15, new Color(0.55f, 0.6f, 0.45f),
                "Minerales formados con agua líquida. Fuente de silicio y pista de habitabilidad pasada.",
                "Esmectitas de hierro/magnesio detectadas por CRISM (banda a 2,3 µm).", ResourceType.Silicon, 1));
            Add(Make(Sulfates, "Sulfatos", ItemCategory.Mineral, 5f, 3.5f, 15, new Color(0.85f, 0.8f, 0.6f),
                "Sales de sulfato. Interesantes para la ciencia y como fertilizante tratado.",
                "Kieserita y yeso (CRISM, bandas a 1,9–2,4 µm).", ResourceType.Samples, 1));
            Add(Make(Sample, "Muestra geológica", ItemCategory.Mineral, 0.5f, 0.4f, 10, new Color(0.9f, 0.85f, 0.4f),
                "Muestra seleccionada para el laboratorio. Delicada: se degrada sin aislamiento térmico.",
                "Testigo de roca en tubo sellado.", ResourceType.Samples, 1, cryo: true, sublimation: 0.02f));
            Add(Make(AuxBattery, "Batería auxiliar", ItemCategory.RobotComponent, 12f, 8f, 1, new Color(0.3f, 0.7f, 0.3f),
                "Batería de repuesto para la bahía interna del rover.", "Ion-litio, 1 kWh.", ResourceType.MetalParts, 0, convert: false));
            Add(Make(SpareParts, "Piezas de repuesto", ItemCategory.RobotComponent, 2f, 1.5f, 10, new Color(0.6f, 0.6f, 0.65f),
                "Tornillería, servos pequeños y placas para reparaciones.", "Aleación de aluminio y acero.", ResourceType.MetalParts, 1));

            // Assets del proyecto (Resources/Items) sustituyen a los de por defecto.
            ItemData[] assets = Resources.LoadAll<ItemData>("Items");
            for (int i = 0; i < assets.Length; i++) Add(assets[i]);
        }

        private static void Add(ItemData d)
        {
            if (d != null && !string.IsNullOrEmpty(d.itemID)) _items[d.itemID] = d;
        }

        private static ItemData Make(string id, string name, ItemCategory cat, float kg, float liters, int stack, Color color,
            string desc, string science, ResourceType res, int perUnit, bool cryo = false, float sublimation = 0.05f,
            bool corrosive = false, bool convert = true)
        {
            var d = ScriptableObject.CreateInstance<ItemData>();
            d.name = id;
            d.itemID = id;
            d.itemName = name;
            d.itemCategory = cat;
            d.massKg = kg;
            d.volumeLiters = liters;
            d.maxStackSize = stack;
            d.iconColor = color;
            d.description = desc;
            d.scientificNotes = science;
            d.baseResource = res;
            d.baseResourcePerUnit = perUnit;
            d.requiresCryoStorage = cryo;
            d.sublimationPerHour = sublimation;
            d.isCorrosiveOrToxic = corrosive;
            d.convertsToBaseResource = convert;
            return d;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _items = null;
        }
    }
}
