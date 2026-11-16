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

        // Materiales procesados (fabricación estilo supervivencia).
        public const string IronIngot = "lingote_hierro";
        public const string Steel = "placa_acero";
        public const string SiliconRefined = "silicio";
        public const string Glass = "vidrio";
        public const string Sulfur = "azufre";
        public const string Carbon = "carbono";
        public const string Co2 = "co2";
        public const string Water = "agua";
        public const string Oxygen = "oxigeno";
        public const string Methane = "metano";
        public const string Polymer = "polimero";
        public const string MetalParts = "piezas_metalicas";
        public const string Wiring = "cableado";
        public const string Electronics = "electronica";
        public const string SolarCell = "celula_solar";
        public const string RegolithBrick = "bloque_regolito";
        public const string SealTape = "cinta_sellado";
        // Consumibles con efecto en el robot.
        public const string RepairKit = "kit_reparacion";
        public const string SpareWheel = "rueda_repuesto";
        public const string CleaningBrush = "cepillo_antiestatico";
        public const string PowerCell = "celda_energia";
        public const string ResearchData = "datos_investigacion";
        // Huerto y alimentación.
        public const string Nutrients = "nutrientes";
        public const string Flour = "harina";
        public const string Ration = "racion";
        public const string Salad = "ensalada";

        /// <summary>Especies del huerto hidropónico (ids de la cosecha; las semillas son semillas_X).</summary>
        public static readonly string[] Crops = { "lechuga", "rabano", "tomate", "patata", "trigo", "soja" };

        public static string SeedOf(string crop)
        {
            return "semillas_" + crop;
        }

        /// <summary>Objeto que representa cada recurso del almacén de la base (todo vive en casillas).</summary>
        private static readonly Dictionary<ResourceType, string> ResourceItems = new Dictionary<ResourceType, string>
        {
            { ResourceType.Regolith, Regolith },
            { ResourceType.Iron, IronIngot },
            { ResourceType.Silicon, SiliconRefined },
            { ResourceType.Ice, Ice },
            { ResourceType.MetalParts, MetalParts },
            { ResourceType.Wiring, Wiring },
            { ResourceType.SolarCells, SolarCell },
            { ResourceType.RegolithBricks, RegolithBrick },
            { ResourceType.Water, Water },
            { ResourceType.Oxygen, Oxygen },
            { ResourceType.Samples, Sample },
            { ResourceType.Research, ResearchData },
            { ResourceType.Methane, Methane },
        };

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
            string mapped;
            ItemData d;
            if (ResourceItems.TryGetValue(type, out mapped) && _items.TryGetValue(mapped, out d)) return d;
            string id = "recurso_" + type;
            if (_items.TryGetValue(id, out d)) return d;
            float kg = Mathf.Max(0.05f, BaseInventory.MassPerUnit(type));
            d = Make(id, BaseInventory.Label(type), ItemCategory.RobotComponent, kg, kg * 0.8f, 20, ResourceColor(type),
                Tr.T("Recurso del almacén de la nave preparado para el transporte."), BaseInventory.CategoryLabel(type), type, 1,
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

        /// <summary>Vuelve a crear la base de objetos (p. ej. tras cambiar de idioma, al empezar partida).</summary>
        public static void ResetCache()
        {
            _items = null;
        }

        private static void Ensure()
        {
            if (_items != null) return;
            _items = new Dictionary<string, ItemData>();

            // Valores aproximados: densidad del regolito ~1,5 kg/L, hielo ~0,92 kg/L, hematita ~5 kg/L
            // (la mena va mezclada con roca, de ahí la densidad menor).
            Add(Make(Regolith, Tr.T("Regolito"), ItemCategory.Mineral, 6f, 4f, 20, new Color(0.62f, 0.37f, 0.23f),
                Tr.T("Suelo marciano suelto: polvo y arena basáltica. Materia prima de bloques y blindaje."),
                Tr.T("Basalto con óxidos de hierro (nanofase) que le dan el color rojo."), ResourceType.Regolith, 1));
            Add(Make(PerchlorateRegolith, Tr.T("Regolito con percloratos"), ItemCategory.Mineral, 6f, 4f, 20, new Color(0.75f, 0.45f, 0.3f),
                Tr.T("Regolito con sales de perclorato: tóxico para personas y plantas. Corrosivo si no va sellado."),
                Tr.T("Percloratos de calcio y magnesio (~0,5–1 % en peso, como midió la sonda Phoenix). ") +
                Tr.T("Al calentarlos liberan oxígeno."), ResourceType.Regolith, 1, corrosive: true));
            Add(Make(IronOre, Tr.T("Mena de hierro"), ItemCategory.Mineral, 9f, 3f, 10, new Color(0.45f, 0.2f, 0.15f),
                Tr.T("Roca rica en óxidos de hierro. Se funde en la fundición del cohete."),
                Tr.T("Hematita y magnetita detectadas por espectrometría orbital (CRISM)."), ResourceType.Iron, 2));
            Add(Make(Ice, Tr.T("Hielo de Utopia Planitia"), ItemCategory.Volatile, 4f, 4.3f, 10, new Color(0.75f, 0.9f, 1f),
                Tr.T("Hielo de agua del subsuelo. Se sublima si no viaja en un contenedor criogénico."),
                Tr.T("H₂O con algo de polvo y sales. Firma espectral de hielo a 1,5 y 2,0 µm."), ResourceType.Ice, 1,
                cryo: true, sublimation: 0.08f));
            Add(Make(Clays, Tr.T("Arcillas (filosilicatos)"), ItemCategory.Mineral, 5f, 3.5f, 15, new Color(0.55f, 0.6f, 0.45f),
                Tr.T("Minerales formados con agua líquida. Fuente de silicio y pista de habitabilidad pasada."),
                Tr.T("Esmectitas de hierro/magnesio detectadas por CRISM (banda a 2,3 µm)."), ResourceType.Silicon, 1));
            Add(Make(Sulfates, Tr.T("Sulfatos"), ItemCategory.Mineral, 5f, 3.5f, 15, new Color(0.85f, 0.8f, 0.6f),
                Tr.T("Sales de sulfato. Interesantes para la ciencia y como fertilizante tratado."),
                Tr.T("Kieserita y yeso (CRISM, bandas a 1,9–2,4 µm)."), ResourceType.Samples, 1));
            Add(Make(Sample, Tr.T("Muestra geológica"), ItemCategory.Mineral, 0.5f, 0.4f, 10, new Color(0.9f, 0.85f, 0.4f),
                Tr.T("Muestra seleccionada para el laboratorio. Delicada: se degrada sin aislamiento térmico."),
                Tr.T("Testigo de roca en tubo sellado."), ResourceType.Samples, 1, cryo: true, sublimation: 0.02f));
            Add(Make(AuxBattery, Tr.T("Batería auxiliar"), ItemCategory.RobotComponent, 12f, 8f, 1, new Color(0.3f, 0.7f, 0.3f),
                Tr.T("Batería de repuesto para la bahía interna del rover."), Tr.T("Ion-litio, 1 kWh."), ResourceType.MetalParts, 0, convert: false));
            Add(Make(SpareParts, Tr.T("Piezas de repuesto"), ItemCategory.RobotComponent, 2f, 1.5f, 10, new Color(0.6f, 0.6f, 0.65f),
                Tr.T("Tornillería, servos pequeños y placas para reparaciones."), Tr.T("Aleación de aluminio y acero."), ResourceType.MetalParts, 1));

            AddCraftingItems();
            AddGardenItems();

            // Assets del proyecto (Resources/Items) sustituyen a los de por defecto.
            ItemData[] assets = Resources.LoadAll<ItemData>("Items");
            for (int i = 0; i < assets.Length; i++) Add(assets[i]);
        }

        /// <summary>Recurso del almacén que representa un objeto (para las estadísticas de la base).</summary>
        public static bool TryGetResource(ItemData item, out ResourceType type)
        {
            foreach (var kv in ResourceItems)
            {
                if (item != null && kv.Value == item.itemID)
                {
                    type = kv.Key;
                    return true;
                }
            }
            type = ResourceType.Regolith;
            return false;
        }

        /// <summary>Materiales procesados y consumibles de la fabricación (masa y volumen aproximados).</summary>
        private static void AddCraftingItems()
        {
            const ItemCategory M = ItemCategory.Material, C = ItemCategory.Consumable, V = ItemCategory.Volatile;
            Add(Make(IronIngot, Tr.T("Lingote de hierro"), M, 5f, 0.7f, 50, new Color(0.55f, 0.32f, 0.27f),
                Tr.T("Hierro reducido en la fundición a partir de la mena. Base de casi todo lo metálico."),
                Tr.T("Reducción de hematita (Fe₂O₃) con hidrógeno o CO a ~1000 °C."), ResourceType.Iron, 1));
            Add(Make(Steel, Tr.T("Placa de acero"), M, 8f, 1f, 30, new Color(0.62f, 0.64f, 0.68f),
                Tr.T("Acero laminado: estructuras, chasis y ruedas."), Tr.T("Hierro con ~0,3 % de carbono."), ResourceType.MetalParts, 0, convert: false));
            Add(Make(SiliconRefined, Tr.T("Silicio"), M, 3f, 1.3f, 50, new Color(0.7f, 0.78f, 0.9f),
                Tr.T("Silicio refinado para electrónica y células solares."), Tr.T("Reducción carbotérmica de la sílice (SiO₂) de las arcillas."), ResourceType.Silicon, 1));
            Add(Make(Glass, Tr.T("Vidrio"), M, 2.5f, 1f, 30, new Color(0.65f, 0.85f, 0.85f),
                Tr.T("Vidrio fundido de regolito: ventanas, ópticas y cubiertas de paneles."), Tr.T("El regolito es ~45 % sílice."), ResourceType.Silicon, 0, convert: false));
            Add(Make(Sulfur, Tr.T("Azufre"), M, 2f, 1f, 50, new Color(0.95f, 0.88f, 0.3f),
                Tr.T("Azufre extraído de los sulfatos. Aglomerante del hormigón de azufre."), Tr.T("Hormigón de azufre: no necesita agua, ideal para Marte."), ResourceType.Samples, 0, convert: false));
            Add(Make(Carbon, Tr.T("Carbono"), M, 1f, 0.6f, 50, new Color(0.15f, 0.15f, 0.16f),
                Tr.T("Carbono obtenido del CO₂ atmosférico. Necesario para el acero."), Tr.T("Reacción de Bosch: CO₂ + 2 H₂ → C + 2 H₂O."), ResourceType.Samples, 0, convert: false));
            Add(Make(Co2, Tr.T("Bombona de CO₂"), V, 2f, 2f, 20, new Color(0.75f, 0.75f, 0.8f),
                Tr.T("Dióxido de carbono comprimido, captado de la atmósfera marciana (95 % CO₂)."), Tr.T("Compresión desde 0,6 kPa."), ResourceType.Samples, 0, convert: false));
            Add(Make(Water, Tr.T("Bidón de agua"), V, 1f, 1f, 50, new Color(0.35f, 0.65f, 1f),
                Tr.T("Agua líquida purificada. Soporte vital y materia prima de oxígeno y combustible."), "H₂O.", ResourceType.Water, 1));
            Add(Make(Oxygen, Tr.T("Botella de oxígeno"), V, 1f, 1.2f, 50, new Color(0.45f, 0.75f, 0.95f),
                Tr.T("Oxígeno para el traje y el soporte vital."), Tr.T("Electrólisis del agua (o MOXIE: CO₂ → CO + O)."), ResourceType.Oxygen, 1));
            Add(Make(Methane, Tr.T("Bombona de metano"), V, 0.5f, 1.5f, 100, new Color(0.9f, 0.35f, 0.15f),
                Tr.T("Metano líquido: propelente del cohete y materia prima de polímeros."), "Sabatier: CO₂ + 4 H₂ → CH₄ + 2 H₂O.", ResourceType.Methane, 1));
            Add(Make(Polymer, Tr.T("Polímero"), M, 1f, 1f, 50, new Color(0.9f, 0.9f, 0.8f),
                Tr.T("Plástico técnico hecho de metano: aislantes, juntas y piezas ligeras."), Tr.T("Polietileno a partir de metano."), ResourceType.MetalParts, 0, convert: false));
            Add(Make(MetalParts, Tr.T("Piezas metálicas"), M, 2f, 1f, 50, new Color(0.65f, 0.66f, 0.7f),
                Tr.T("Tornillería, soportes y engranajes."), Tr.T("Mecanizado de lingotes de hierro."), ResourceType.MetalParts, 1));
            Add(Make(Wiring, Tr.T("Cableado"), M, 1f, 0.8f, 50, new Color(0.75f, 0.45f, 0.25f),
                Tr.T("Cable conductor aislado para circuitos y estructuras."), Tr.T("Conductor metálico con funda de polímero."), ResourceType.Wiring, 1));
            Add(Make(Electronics, Tr.T("Electrónica"), M, 0.5f, 0.5f, 30, new Color(0.2f, 0.6f, 0.35f),
                Tr.T("Placas de circuito para máquinas, baterías y sensores."), Tr.T("Obleas de silicio dopado."), ResourceType.MetalParts, 0, convert: false));
            Add(Make(SolarCell, Tr.T("Célula solar"), M, 2f, 2f, 30, new Color(0.2f, 0.3f, 0.6f),
                Tr.T("Célula fotovoltaica para paneles solares."), Tr.T("Silicio cristalino bajo cubierta de vidrio."), ResourceType.SolarCells, 1));
            Add(Make(RegolithBrick, Tr.T("Bloque de regolito"), M, 10f, 5f, 20, new Color(0.7f, 0.42f, 0.28f),
                Tr.T("Bloque de hormigón de azufre y regolito para construir."), Tr.T("Regolito + azufre fundido a ~140 °C."), ResourceType.RegolithBricks, 1));
            Add(Make(SealTape, Tr.T("Cinta de sellado"), M, 0.2f, 0.2f, 50, new Color(0.85f, 0.6f, 0.2f),
                Tr.T("Cinta de polímero para sellar juntas contra el polvo."), Tr.T("Adhesivo apto para −120 °C."), ResourceType.MetalParts, 0, convert: false));
            Add(Make(ResearchData, Tr.T("Datos de investigación"), M, 0.01f, 0.01f, 999, new Color(0.6f, 0.9f, 0.6f),
                Tr.T("Resultados científicos del laboratorio, guardados en memoria."), Tr.T("Puntos de investigación."), ResourceType.Research, 1));

            Add(Make(RepairKit, Tr.T("Kit de reparación"), C, 3f, 2f, 10, new Color(0.9f, 0.55f, 0.15f),
                Tr.T("Repara por completo el módulo más dañado del robot."), Tr.T("Piezas, cableado y polímero."), ResourceType.MetalParts, 0, convert: false));
            Add(Make(SpareWheel, Tr.T("Rueda de repuesto"), C, 12f, 30f, 2, new Color(0.25f, 0.25f, 0.27f),
                Tr.T("Deja todas las ruedas del robot como nuevas."), Tr.T("Rueda de 0,5 m con garras."), ResourceType.MetalParts, 0, convert: false));
            Add(Make(CleaningBrush, Tr.T("Cepillo antiestático"), C, 0.6f, 1f, 5, new Color(0.4f, 0.7f, 0.9f),
                Tr.T("Limpia del todo el polvo de los paneles solares del robot."), Tr.T("Cerdas de polímero con descarga electrostática."), ResourceType.MetalParts, 0, convert: false));
            Add(Make(PowerCell, Tr.T("Celda de energía"), C, 4f, 2f, 5, new Color(0.35f, 0.8f, 0.4f),
                Tr.T("Carga el 50 % de la batería del robot al usarla."), Tr.T("Batería de ion-litio de un solo uso."), ResourceType.MetalParts, 0, convert: false));
        }

        /// <summary>Semillas, cosechas y alimentos del huerto hidropónico.</summary>
        private static void AddGardenItems()
        {
            const ItemCategory G = ItemCategory.Garden;
            string[] names = { Tr.T("Lechuga"), Tr.T("Rábano"), Tr.T("Tomate enano"), Tr.T("Patata"), Tr.T("Trigo enano"), Tr.T("Soja") };
            string[] seedNotes =
            {
                Tr.T("Lactuca sativa roja 'Outredgeous': la primera lechuga que comieron los astronautas en la ISS (Veggie, 2015)."),
                Tr.T("Raphanus sativus: crece en unos 30 días; se cultivó en la ISS (Plant Habitat-02)."),
                Tr.T("Tomate enano 'Red Robin', cultivado en la ISS en 2023 (VEG-05)."),
                Tr.T("Solanum tuberosum: se planta con tubérculos-semilla; muchas calorías por metro cuadrado."),
                Tr.T("Trigo enano 'USU-Apogee', desarrollado con la NASA para sistemas de soporte vital."),
                Tr.T("Glycine max: proteína y aceite; fija nitrógeno."),
            };
            string[] cropDesc =
            {
                Tr.T("Hojas frescas: vitaminas y moral para la tripulación."),
                Tr.T("Raíz picante y hojas comestibles. Cosecha rápida."),
                Tr.T("Tomates pequeños y jugosos, ricos en licopeno."),
                Tr.T("Tubérculos con muchas calorías: la base de la dieta marciana."),
                Tr.T("Grano para moler en harina."),
                Tr.T("Vainas con granos ricos en proteína."),
            };
            Color[] seedColors = { new Color(0.3f, 0.25f, 0.2f), new Color(0.55f, 0.35f, 0.2f), new Color(0.85f, 0.78f, 0.55f),
                new Color(0.75f, 0.6f, 0.35f), new Color(0.85f, 0.68f, 0.3f), new Color(0.9f, 0.82f, 0.45f) };
            Color[] cropColors = { new Color(0.45f, 0.75f, 0.3f), new Color(0.85f, 0.2f, 0.3f), new Color(0.9f, 0.2f, 0.15f),
                new Color(0.75f, 0.58f, 0.35f), new Color(0.9f, 0.75f, 0.35f), new Color(0.55f, 0.7f, 0.3f) };
            float[] cropKg = { 0.3f, 0.1f, 0.15f, 0.25f, 0.2f, 0.15f };
            for (int i = 0; i < Crops.Length; i++)
            {
                Add(Make(SeedOf(Crops[i]), Tr.T("Semillas de ") + names[i].ToLowerInvariant(), G, 0.02f, 0.02f, 50, seedColors[i],
                    Tr.T("Se plantan en una bandeja del huerto hidropónico con agua y solución nutritiva."), seedNotes[i], ResourceType.Samples, 0, convert: false));
                Add(Make(Crops[i], names[i], G, cropKg[i], cropKg[i] * 1.5f, 30, cropColors[i],
                    cropDesc[i] + Tr.T(" En la cocina se convierte en comida; en el laboratorio da semillas nuevas."), Tr.T("Cultivo hidropónico bajo LED."),
                    ResourceType.Samples, 0, convert: false));
            }
            Add(Make(Nutrients, Tr.T("Solución nutritiva"), G, 1f, 1f, 30, new Color(0.3f, 0.75f, 0.45f),
                Tr.T("Abono líquido para el huerto hidropónico, hecho con sulfatos y agua."), Tr.T("Sales de nitrógeno, fósforo, potasio y azufre disueltas."),
                ResourceType.Samples, 0, convert: false));
            Add(Make(Flour, Tr.T("Harina"), G, 1f, 1.5f, 30, new Color(0.95f, 0.92f, 0.85f), Tr.T("Trigo molido para cocinar."), Tr.T("Harina integral de trigo enano."),
                ResourceType.Samples, 0, convert: false));
            Add(Make(Ration, Tr.T("Ración de supervivencia"), ItemCategory.Consumable, 0.6f, 0.6f, 20, new Color(0.75f, 0.76f, 0.8f),
                Tr.T("Comida completa envasada al vacío para la tripulación (unas 2.000 kcal)."), Tr.T("Pan de trigo, soja y patata."),
                ResourceType.Samples, 0, convert: false));
            Add(Make(Salad, Tr.T("Ensalada fresca"), ItemCategory.Consumable, 0.4f, 0.8f, 10, new Color(0.45f, 0.8f, 0.35f),
                Tr.T("Lechuga, tomate y rábano del huerto. Vitaminas frescas."), Tr.T("Se conserva poco tiempo."), ResourceType.Samples, 0, convert: false));
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
