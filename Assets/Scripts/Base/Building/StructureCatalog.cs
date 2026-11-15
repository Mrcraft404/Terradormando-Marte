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
        CommsAntenna         // Antena: mensajes de la Tierra y objetivos
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
                case StructureCategory.Energy: return "Energía";
                case StructureCategory.LifeSupport: return "Aire y agua";
                case StructureCategory.Construction: return "Construcción";
                default: return "Ciencia";
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
                    id = "solar", name = "Panel solar fijo", kind = StructureKind.SolarArray, category = StructureCategory.Energy,
                    description = "Genera hasta 600 W con sol directo. Se ensucia de polvo: límpialo con F.",
                    size = new Vector3(4f, 1.6f, 3f), generationWatts = 600f, maxSlopeDegrees = 20f,
                    cost = new[] { R(ResourceType.SolarCells, 3), R(ResourceType.MetalParts, 2) }
                },
                new StructureDefinition
                {
                    id = "battery", name = "Batería de la base", kind = StructureKind.BaseBattery, category = StructureCategory.Energy,
                    description = "Guarda 5000 Wh para la noche y las tormentas.",
                    size = new Vector3(2f, 1.6f, 2f), storageWh = 5000f,
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.Wiring, 2) }
                },
                new StructureDefinition
                {
                    id = "reactor", name = "Reactor de fisión pequeño", kind = StructureKind.FissionReactor, category = StructureCategory.Energy,
                    description = "8 kW constantes, de día, de noche y con tormenta. Caro y requiere investigación.",
                    size = new Vector3(3f, 3.5f, 3f), generationWatts = 8000f, researchCost = 30,
                    cost = new[] { R(ResourceType.MetalParts, 10), R(ResourceType.Wiring, 4), R(ResourceType.RegolithBricks, 6) }
                },

                // ---------------- Aire y agua ----------------
                new StructureDefinition
                {
                    id = "oxygen", name = "Extractor de oxígeno", kind = StructureKind.OxygenExtractor, category = StructureCategory.LifeSupport,
                    description = "Separa el oxígeno del CO₂ de la atmósfera (como el experimento real MOXIE). Gasta mucha energía.",
                    size = new Vector3(2.5f, 2.2f, 2.5f), consumptionWatts = 800f,
                    produces = new[] { new ResourceRate(ResourceType.Oxygen, 4f) },
                    cost = new[] { R(ResourceType.MetalParts, 4), R(ResourceType.Wiring, 2) }
                },
                new StructureDefinition
                {
                    id = "icedrill", name = "Taladro de hielo", kind = StructureKind.IceDrill, category = StructureCategory.LifeSupport,
                    description = "Perfora el hielo del subsuelo y lo funde en agua. Rinde según el hielo de la zona.",
                    size = new Vector3(2.5f, 4f, 2.5f), consumptionWatts = 600f,
                    produces = new[] { new ResourceRate(ResourceType.Water, 3f) },
                    cost = new[] { R(ResourceType.MetalParts, 5), R(ResourceType.Wiring, 1) }
                },
                new StructureDefinition
                {
                    id = "condenser", name = "Condensador atmosférico", kind = StructureKind.AtmosphericCondenser, category = StructureCategory.LifeSupport,
                    description = "Saca un poco de agua del aire en cualquier zona. Rinde muy poco con tormenta.",
                    size = new Vector3(2f, 2.5f, 2f), consumptionWatts = 500f, researchCost = 10,
                    produces = new[] { new ResourceRate(ResourceType.Water, 0.8f) },
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.Wiring, 2), R(ResourceType.SolarCells, 1) }
                },
                new StructureDefinition
                {
                    id = "o2tank", name = "Tanque de oxígeno", kind = StructureKind.OxygenTank, category = StructureCategory.LifeSupport,
                    description = "+200 de capacidad de oxígeno.",
                    size = new Vector3(2f, 3f, 2f), storageBonus = R(ResourceType.Oxygen, 200),
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.RegolithBricks, 4) }
                },
                new StructureDefinition
                {
                    id = "watertank", name = "Tanque de agua", kind = StructureKind.WaterTank, category = StructureCategory.LifeSupport,
                    description = "+200 de capacidad de agua.",
                    size = new Vector3(2.4f, 2.4f, 2.4f), storageBonus = R(ResourceType.Water, 200),
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.RegolithBricks, 4) }
                },

                // ---------------- Construcción ----------------
                new StructureDefinition
                {
                    id = "printer", name = "Impresora 3D de regolito", kind = StructureKind.RegolithPrinter, category = StructureCategory.Construction,
                    description = "Convierte regolito en bloques sola. El regolito se apelmaza: a veces la tolva se atasca y hay que desatascarla con F.",
                    size = new Vector3(3f, 2.5f, 3f), consumptionWatts = 400f,
                    consumes = new[] { new ResourceRate(ResourceType.Regolith, 10f) },
                    produces = new[] { new ResourceRate(ResourceType.RegolithBricks, 2f) },
                    cost = new[] { R(ResourceType.MetalParts, 4), R(ResourceType.Wiring, 2) }
                },
                new StructureDefinition
                {
                    id = "wall", name = "Muro de regolito", kind = StructureKind.RegolithWall, category = StructureCategory.Construction,
                    description = "Protege del viento y delimita la base.",
                    size = new Vector3(4f, 2.5f, 0.6f), maxSlopeDegrees = 25f,
                    cost = new[] { R(ResourceType.RegolithBricks, 3) }
                },
                new StructureDefinition
                {
                    id = "floor", name = "Suelo de regolito", kind = StructureKind.FloorPlate, category = StructureCategory.Construction,
                    description = "Plataforma plana para caminar y construir encima.",
                    size = new Vector3(4f, 0.2f, 4f), solid = false,
                    cost = new[] { R(ResourceType.RegolithBricks, 2) }
                },
                new StructureDefinition
                {
                    id = "shelter", name = "Refugio contra radiación", kind = StructureKind.RadiationShelter, category = StructureCategory.Construction,
                    description = "Bóveda cubierta con una capa gruesa de regolito. Imprescindible para los colonos.",
                    size = new Vector3(6f, 3.5f, 6f), maxSlopeDegrees = 12f,
                    cost = new[] { R(ResourceType.RegolithBricks, 12), R(ResourceType.MetalParts, 2) }
                },
                new StructureDefinition
                {
                    id = "pad", name = "Pista de aterrizaje nivelada", kind = StructureKind.LandingPad, category = StructureCategory.Construction,
                    description = "Donde aterrizará la nave con la primera tripulación. Necesita terreno casi llano.",
                    size = new Vector3(16f, 0.3f, 16f), solid = false, maxSlopeDegrees = 8f,
                    cost = new[] { R(ResourceType.RegolithBricks, 20), R(ResourceType.MetalParts, 4) }
                },

                // ---------------- Ciencia ----------------
                new StructureDefinition
                {
                    id = "lab", name = "Laboratorio de muestras", kind = StructureKind.SampleLab, category = StructureCategory.Science,
                    description = "Analiza las muestras del taladro del robot y da puntos de investigación para desbloquear máquinas.",
                    size = new Vector3(4f, 2.6f, 3f), consumptionWatts = 400f,
                    consumes = new[] { new ResourceRate(ResourceType.Samples, 2f) },
                    produces = new[] { new ResourceRate(ResourceType.Research, 6f) },
                    cost = new[] { R(ResourceType.MetalParts, 4), R(ResourceType.Wiring, 3), R(ResourceType.SolarCells, 2) }
                },
                new StructureDefinition
                {
                    id = "antenna", name = "Antena de comunicaciones", kind = StructureKind.CommsAntenna, category = StructureCategory.Science,
                    description = "Recibe mensajes de la Tierra (con unos minutos de retraso) y la lista de preparación para la tripulación.",
                    size = new Vector3(2f, 6f, 2f), consumptionWatts = 150f,
                    cost = new[] { R(ResourceType.MetalParts, 3), R(ResourceType.Wiring, 3) }
                },
            };
        }
    }
}
