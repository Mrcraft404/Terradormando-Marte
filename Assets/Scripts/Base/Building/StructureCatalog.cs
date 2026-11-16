using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Tipo de estructura: decide su modelo y su comportamiento especial.</summary>
    public enum StructureKind
    {
        SolarArray,          // Panel solar fijo (se ensucia de polvo)
        BaseBattery,         // Batería de la base
        FissionReactor,      // Reactor pequeño de fisión (energía constante)
        OxygenExtractor,     // CO₂ → O₂ (inspirado en el experimento real MOXIE)
        IceDrill,            // Perfora hielo y lo funde en agua
        AtmosphericCondenser,// Saca algo de agua del aire
        OxygenTank,          // +capacidad de oxígeno
        WaterTank,           // +capacidad de agua
        RegolithPrinter,     // Impresora 3D: regolito → bloques (a veces se atasca)
        RegolithWall,        // Muro de regolito
        FloorPlate,          // Suelo de regolito
        RadiationShelter,    // Refugio contra radiación
        LandingPad,          // Pista de aterrizaje nivelada
        SampleLab,           // Laboratorio: muestras → investigación
        CommsAntenna,        // Antena: mensajes de la Tierra y objetivos
        FuelRefinery,        // Refinería Sabatier (viene con el cohete)
        CommBeacon           // Baliza de comunicaciones (revela el mapa)
    }

    /// <summary>Categoría del menú de construcción.</summary>
    public enum StructureCategory
    {
        Energy,
        LifeSupport,
        Construction,
        Science
    }

    /// <summary>Producción o consumo de un recurso por hora de juego.</summary>
    [Serializable]
    public struct ResourceRate
    {
        public ResourceType type;
        public float perHour;

        public ResourceRate(ResourceType type, float perHour)
        {
            this.type = type;
            this.perHour = perHour;
        }
    }

    /// <summary>Definición de una estructura construible (datos; el comportamiento está en PlacedStructure).</summary>
    [Serializable]
    public class StructureDefinition
    {
        public string id;
        public string name;
        [TextArea] public string description;
        public StructureKind kind;
        public StructureCategory category;
        [Tooltip("Tamaño en metros (ancho X, alto Y, fondo Z).")]
        public Vector3 size = new Vector3(3f, 2f, 3f);
        public ResourceAmount[] cost;

        [Header("Energía (W)")]
        [Tooltip("Potencia generada (+) a pleno rendimiento.")]
        public float generationWatts;
        [Tooltip("Potencia consumida mientras funciona.")]
        public float consumptionWatts;
        [Tooltip("Energía que puede almacenar (Wh).")]
        public float storageWh;

        [Header("Producción (por hora de juego)")]
        public ResourceRate[] produces = new ResourceRate[0];
        public ResourceRate[] consumes = new ResourceRate[0];

        [Header("Otros")]
        [Tooltip("Capacidad extra de un recurso (tanques).")]
        public ResourceAmount storageBonus;
        [Tooltip("Pendiente máxima del terreno (grados) para poder colocarla.")]
        public float maxSlopeDegrees = 15f;
        [Tooltip("Puntos de investigación necesarios para desbloquearla (0 = disponible desde el inicio).")]
        public int researchCost;
        [Tooltip("No aparece en el menú de construcción (p. ej. la refinería que trae el cohete).")]
        public bool hiddenFromBuildMenu;
        [Tooltip("Se puede colocar lejos del cohete (balizas de exploración).")]
        public bool ignoreBuildRadius;

        [Tooltip("Tiene colisión (los suelos y la pista no, para poder pasar por encima).")]
        public bool solid = true;

        public bool IsGenerator
        {
            get { return generationWatts > 0f; }
        }

        public bool IsConsumer
        {
            get { return consumptionWatts > 0f; }
        }
    }

    /// <summary>
    /// Todas las estructuras del juego. Los valores son un primer balance para probar:
    /// con MarsTime.TimeScale = 60, una hora de juego dura un minuto real.
    /// </summary>
    public static class StructureCatalog
    {
        private static List<StructureDefinition> _all;

        /// <summary>Vuelve a crear el catálogo (p. ej. tras cambiar de idioma, al empezar partida).</summary>
        public static void ResetCache()
        {
            _all = null;
        }

        public static IReadOnlyList<StructureDefinition> All
        {
            get
            {
                if (_all == null) _all = Build();
                return _all;
            }
        }

        public static StructureDefinition Find(string id)
        {
            var all = All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].id == id) return all[i];
            }
            return null;
        }

        public static string CategoryLabel(StructureCategory c)
        {
            switch (c)
            {
                case StructureCategory.Energy: return Tr.T("Energía");
                case StructureCategory.LifeSupport: return Tr.T("Aire y agua");
                case StructureCategory.Construction: return Tr.T("Construcción");
                default: return Tr.T("Ciencia");
            }
        }

        private static ResourceAmount R(ResourceType t, int n)
        {
            return new ResourceAmount(t, n);
        }

        private static List<StructureDefinition> Build()
        {
            return new List<StructureDefinition>
            {
                // ---------------- Energía ----------------
                new StructureDefinition
                {
                    id = "solar", name = Tr.T("Panel solar fijo"), kind = StructureKind.SolarArray, category = StructureCategory.Energy,
                    description = Tr.T("Genera hasta 600 W con sol directo. Se ensucia de polvo: límpialo con F."),
                    size = new Vector3(4f, 1.6f, 3f), generationWatts = 600f, maxSlopeDegrees = 20f,
                    cost = new[] { R(ResourceType.SolarCells, 3), R(ResourceType.MetalParts, 2) }
                },
                new StructureDefinition
                {
                    id = "battery", name = Tr.T("Batería de la base"), kind = StructureKind.BaseBattery, category = StructureCategory.Energy,
                    description = Tr.T("Guarda 5000 Wh para la noche y las tormentas."),
                    size = new Vector3(2f, 1.6f, 2f), storageWh = 5000f,
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.Wiring, 2) }
                },
                new StructureDefinition
                {
                    id = "reactor", name = Tr.T("Reactor de fisión pequeño"), kind = StructureKind.FissionReactor, category = StructureCategory.Energy,
                    description = Tr.T("8 kW constantes, de día, de noche y con tormenta. Caro y requiere investigación."),
                    size = new Vector3(3f, 3.5f, 3f), generationWatts = 8000f, researchCost = 30,
                    cost = new[] { R(ResourceType.MetalParts, 10), R(ResourceType.Wiring, 4), R(ResourceType.RegolithBricks, 6) }
                },

                // ---------------- Aire y agua ----------------
                new StructureDefinition
                {
                    id = "oxygen", name = Tr.T("Extractor de oxígeno"), kind = StructureKind.OxygenExtractor, category = StructureCategory.LifeSupport,
                    description = Tr.T("Separa el oxígeno del CO₂ de la atmósfera (como el experimento real MOXIE). Gasta mucha energía."),
                    size = new Vector3(2.5f, 2.2f, 2.5f), consumptionWatts = 800f,
                    produces = new[] { new ResourceRate(ResourceType.Oxygen, 4f) },
                    cost = new[] { R(ResourceType.MetalParts, 4), R(ResourceType.Wiring, 2) }
                },
                new StructureDefinition
                {
                    id = "icedrill", name = Tr.T("Taladro de hielo"), kind = StructureKind.IceDrill, category = StructureCategory.LifeSupport,
                    description = Tr.T("Perfora el hielo del subsuelo y lo funde en agua. Rinde según el hielo de la zona."),
                    size = new Vector3(2.5f, 4f, 2.5f), consumptionWatts = 600f,
                    produces = new[] { new ResourceRate(ResourceType.Water, 3f) },
                    cost = new[] { R(ResourceType.MetalParts, 5), R(ResourceType.Wiring, 1) }
                },
                new StructureDefinition
                {
                    id = "condenser", name = Tr.T("Condensador atmosférico"), kind = StructureKind.AtmosphericCondenser, category = StructureCategory.LifeSupport,
                    description = Tr.T("Saca un poco de agua del aire en cualquier zona. Rinde muy poco con tormenta."),
                    size = new Vector3(2f, 2.5f, 2f), consumptionWatts = 500f, researchCost = 10,
                    produces = new[] { new ResourceRate(ResourceType.Water, 0.8f) },
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.Wiring, 2), R(ResourceType.SolarCells, 1) }
                },
                new StructureDefinition
                {
                    id = "o2tank", name = Tr.T("Tanque de oxígeno"), kind = StructureKind.OxygenTank, category = StructureCategory.LifeSupport,
                    description = Tr.T("+200 de capacidad de oxígeno."),
                    size = new Vector3(2f, 3f, 2f), storageBonus = R(ResourceType.Oxygen, 200),
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.RegolithBricks, 4) }
                },
                new StructureDefinition
                {
                    id = "watertank", name = Tr.T("Tanque de agua"), kind = StructureKind.WaterTank, category = StructureCategory.LifeSupport,
                    description = Tr.T("+200 de capacidad de agua."),
                    size = new Vector3(2.4f, 2.4f, 2.4f), storageBonus = R(ResourceType.Water, 200),
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.RegolithBricks, 4) }
                },

                // ---------------- Construcción ----------------
                new StructureDefinition
                {
                    id = "printer", name = Tr.T("Impresora 3D de regolito"), kind = StructureKind.RegolithPrinter, category = StructureCategory.Construction,
                    description = Tr.T("Convierte regolito en bloques sola. El regolito se apelmaza: a veces la tolva se atasca y hay que desatascarla con F."),
                    size = new Vector3(3f, 2.5f, 3f), consumptionWatts = 400f,
                    consumes = new[] { new ResourceRate(ResourceType.Regolith, 10f) },
                    produces = new[] { new ResourceRate(ResourceType.RegolithBricks, 2f) },
                    cost = new[] { R(ResourceType.MetalParts, 4), R(ResourceType.Wiring, 2) }
                },
                new StructureDefinition
                {
                    id = "wall", name = Tr.T("Muro de regolito"), kind = StructureKind.RegolithWall, category = StructureCategory.Construction,
                    description = Tr.T("Protege del viento y delimita la base."),
                    size = new Vector3(4f, 2.5f, 0.6f), maxSlopeDegrees = 25f,
                    cost = new[] { R(ResourceType.RegolithBricks, 3) }
                },
                new StructureDefinition
                {
                    id = "floor", name = Tr.T("Suelo de regolito"), kind = StructureKind.FloorPlate, category = StructureCategory.Construction,
                    description = Tr.T("Plataforma plana para caminar y construir encima."),
                    size = new Vector3(4f, 0.2f, 4f), solid = false,
                    cost = new[] { R(ResourceType.RegolithBricks, 2) }
                },
                new StructureDefinition
                {
                    id = "shelter", name = Tr.T("Refugio contra radiación"), kind = StructureKind.RadiationShelter, category = StructureCategory.Construction,
                    description = Tr.T("Bóveda cubierta con una capa gruesa de regolito. Imprescindible para los colonos."),
                    size = new Vector3(6f, 3.5f, 6f), maxSlopeDegrees = 12f,
                    cost = new[] { R(ResourceType.RegolithBricks, 12), R(ResourceType.MetalParts, 2) }
                },
                new StructureDefinition
                {
                    id = "pad", name = Tr.T("Pista de aterrizaje nivelada"), kind = StructureKind.LandingPad, category = StructureCategory.Construction,
                    description = Tr.T("Donde aterrizará la nave con la primera tripulación. Necesita terreno casi llano."),
                    size = new Vector3(16f, 0.3f, 16f), solid = false, maxSlopeDegrees = 8f,
                    cost = new[] { R(ResourceType.RegolithBricks, 20), R(ResourceType.MetalParts, 4) }
                },

                // ---------------- Ciencia ----------------
                new StructureDefinition
                {
                    id = "lab", name = Tr.T("Laboratorio de muestras"), kind = StructureKind.SampleLab, category = StructureCategory.Science,
                    description = Tr.T("Analiza las muestras del taladro del robot y da puntos de investigación para desbloquear máquinas."),
                    size = new Vector3(4f, 2.6f, 3f), consumptionWatts = 400f,
                    consumes = new[] { new ResourceRate(ResourceType.Samples, 2f) },
                    produces = new[] { new ResourceRate(ResourceType.Research, 6f) },
                    cost = new[] { R(ResourceType.MetalParts, 4), R(ResourceType.Wiring, 3), R(ResourceType.SolarCells, 2) }
                },
                new StructureDefinition
                {
                    id = "antenna", name = Tr.T("Antena de comunicaciones"), kind = StructureKind.CommsAntenna, category = StructureCategory.Science,
                    description = Tr.T("Recibe mensajes de la Tierra (con unos minutos de retraso) y la lista de preparación para la tripulación."),
                    size = new Vector3(2f, 6f, 2f), consumptionWatts = 150f,
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.Wiring, 3) }
                },

                // ---------------- Cohete nodriza y exploración ----------------
                new StructureDefinition
                {
                    id = "refinery", name = Tr.T("Refinería ISRU (Sabatier)"), kind = StructureKind.FuelRefinery, category = StructureCategory.LifeSupport,
                    description = Tr.T("Electroliza agua y la combina con el CO₂ del aire (reacción de Sabatier): metano para el cohete y oxígeno. 2 agua → 1 metano + 2 oxígeno."),
                    size = new Vector3(4f, 3.5f, 3f), consumptionWatts = 1500f, hiddenFromBuildMenu = true,
                    consumes = new[] { new ResourceRate(ResourceType.Water, 2f) },
                    produces = new[] { new ResourceRate(ResourceType.Methane, 1f), new ResourceRate(ResourceType.Oxygen, 2f) },
                    cost = new[] { R(ResourceType.MetalParts, 8), R(ResourceType.Wiring, 4) }
                },
                new StructureDefinition
                {
                    id = "beacon", name = Tr.T("Baliza de comunicaciones"), kind = StructureKind.CommBeacon, category = StructureCategory.Science,
                    description = Tr.T("Repetidor solar autónomo: revela el minimapa en 120 m a su alrededor. Se puede colocar lejos de la base."),
                    size = new Vector3(1f, 3f, 1f), ignoreBuildRadius = true, maxSlopeDegrees = 30f,
                    cost = new[] { R(ResourceType.MetalParts, 1), R(ResourceType.Wiring, 1) }
                },
            };
        }
    }
}
