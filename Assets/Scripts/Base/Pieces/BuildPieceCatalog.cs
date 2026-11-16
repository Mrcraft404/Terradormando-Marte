using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Categorías (pestañas) del menú de construcción.</summary>
    public enum PieceCategory
    {
        FoundationsAndFloors, // Cimientos y suelos
        Walls,                // Paredes
        Glass,                // Cristal
        Doors,                // Puertas
        Roofs,                // Techos
        Structure,            // Estructura (pilares, escaleras)
        Modules,              // Módulos presurizados (hábitats completos)
        Equipment             // Equipos de la base (paneles, tanques… del modo B)
    }

    /// <summary>Dónde encaja una pieza en la cuadrícula de 4 m.</summary>
    public enum PieceSlotType : byte
    {
        Foundation = 0, // celda a nivel del suelo
        Deck = 1,       // celda en un piso superior: techo del piso de abajo y suelo del de arriba
        Edge = 2,       // borde entre dos celdas (paredes, cristales, puertas)
        Corner = 3,     // esquina de celda (pilares)
        CellObject = 4  // objeto dentro de una celda (escaleras)
    }

    /// <summary>Forma de la pieza: decide su modelo.</summary>
    public enum PieceShape
    {
        Foundation, FloorSlab, Grating, ArmoredRoof, GlassRoof, GlassSkylight,
        Wall, WindowWall, HalfWall, PressurePanel, GlassPanel, Door, RoverGate,
        Pillar, Stairs
    }

    /// <summary>Una pieza de construcción modular.</summary>
    public sealed class BuildPieceDefinition
    {
        public string id;
        public string name;
        public string description;
        public PieceCategory category;
        public PieceShape shape;
        public PieceSlotType slot;
        public ResourceAmount[] cost;

        /// <summary>True si cierra el paso del aire (para saber si una sala queda hermética).</summary>
        public bool seals;

        public bool IsDoor
        {
            get { return shape == PieceShape.Door || shape == PieceShape.RoverGate; }
        }
    }

    /// <summary>
    /// Todas las piezas de construcción espacial. Cuadrícula de 4 × 4 m y pisos de 3 m.
    /// Costes en recursos del inventario de la base (regolito → bloques de regolito,
    /// titanio → piezas metálicas, vidrio/aerogel → silicio, electrónica → cableado).
    /// </summary>
    public static class BuildPieceCatalog
    {
        private static List<BuildPieceDefinition> _all;

        /// <summary>Vuelve a crear el catálogo (p. ej. tras cambiar de idioma, al empezar partida).</summary>
        public static void ResetCache()
        {
            _all = null;
        }

        public static IReadOnlyList<BuildPieceDefinition> All
        {
            get { if (_all == null) _all = Create(); return _all; }
        }

        public static BuildPieceDefinition Find(string id)
        {
            foreach (BuildPieceDefinition p in All) if (p.id == id) return p;
            return null;
        }

        public static string CategoryLabel(PieceCategory c)
        {
            switch (c)
            {
                case PieceCategory.FoundationsAndFloors: return Tr.T("Cimientos y suelos");
                case PieceCategory.Walls: return Tr.T("Paredes");
                case PieceCategory.Glass: return Tr.T("Cristal");
                case PieceCategory.Doors: return Tr.T("Puertas");
                case PieceCategory.Roofs: return Tr.T("Techos");
                case PieceCategory.Structure: return Tr.T("Estructura");
                case PieceCategory.Modules: return Tr.T("Módulos presurizados");
                default: return Tr.T("Equipos de la base");
            }
        }

        private static ResourceAmount R(ResourceType t, int n)
        {
            return new ResourceAmount(t, n);
        }

        private static List<BuildPieceDefinition> Create()
        {
            var b = ResourceType.RegolithBricks;
            var m = ResourceType.MetalParts;
            var s = ResourceType.Silicon;
            var w = ResourceType.Wiring;
            return new List<BuildPieceDefinition>
            {
                // --- Cimientos y suelos ---
                P("cimiento", Tr.T("Cimiento de regolito"), Tr.T("Losa de 4 × 4 m de regolito sinterizado. Si el suelo es irregular, se apoya en pilotes."),
                  PieceCategory.FoundationsAndFloors, PieceShape.Foundation, PieceSlotType.Foundation, true, R(b, 6)),
                P("losa_piso", Tr.T("Losa de piso"), Tr.T("Suelo para un piso superior; también hace de techo del piso de abajo. Hermética."),
                  PieceCategory.FoundationsAndFloors, PieceShape.FloorSlab, PieceSlotType.Deck, true, R(b, 3), R(m, 1)),
                P("rejilla", Tr.T("Rejilla metálica"), Tr.T("Pasarela de titanio: se camina por encima pero el aire pasa (no es hermética)."),
                  PieceCategory.FoundationsAndFloors, PieceShape.Grating, PieceSlotType.Deck, false, R(m, 3)),

                // --- Paredes ---
                P("pared_regolito", Tr.T("Pared de regolito"), Tr.T("Muro de bloques de regolito: hermético y protege de la radiación."),
                  PieceCategory.Walls, PieceShape.Wall, PieceSlotType.Edge, true, R(b, 4)),
                P("pared_ventana", Tr.T("Pared con ventana"), Tr.T("Muro de regolito con una ventana de vidrio laminado."),
                  PieceCategory.Walls, PieceShape.WindowWall, PieceSlotType.Edge, true, R(b, 3), R(s, 2)),
                P("panel_presurizado", Tr.T("Panel presurizado"), Tr.T("Panel ligero de Kevlar y aerogel con marco de titanio. Hermético."),
                  PieceCategory.Walls, PieceShape.PressurePanel, PieceSlotType.Edge, true, R(m, 2), R(s, 1)),
                P("murete", Tr.T("Murete"), Tr.T("Pared baja de 1 m (barandilla o separador). No es hermética."),
                  PieceCategory.Walls, PieceShape.HalfWall, PieceSlotType.Edge, false, R(b, 2)),

                // --- Cristal ---
                P("panel_cristal", Tr.T("Panel de cristal"), Tr.T("Vidrio laminado con marco de titanio: deja pasar la luz y es hermético."),
                  PieceCategory.Glass, PieceShape.GlassPanel, PieceSlotType.Edge, true, R(s, 4), R(m, 1)),
                P("techo_cristal", Tr.T("Techo de cristal"), Tr.T("Cubierta acristalada de 4 × 4 m (invernaderos). Hermética."),
                  PieceCategory.Glass, PieceShape.GlassRoof, PieceSlotType.Deck, true, R(s, 4), R(m, 2)),
                P("claraboya", Tr.T("Claraboya geodésica"), Tr.T("Pequeña cúpula geodésica de cristal sobre una celda. Hermética."),
                  PieceCategory.Glass, PieceShape.GlassSkylight, PieceSlotType.Deck, true, R(s, 5), R(m, 2)),

                // --- Puertas ---
                P("puerta_presurizada", Tr.T("Puerta presurizada"), Tr.T("Puerta corredera hermética de 1,4 m. F para abrir o cerrar (robot o astronauta)."),
                  PieceCategory.Doors, PieceShape.Door, PieceSlotType.Edge, true, R(m, 4), R(w, 2)),
                P("porton_rover", Tr.T("Portón del rover"), Tr.T("Portón enrollable de 3,6 m para que entre el robot. F para abrir o cerrar."),
                  PieceCategory.Doors, PieceShape.RoverGate, PieceSlotType.Edge, true, R(m, 6), R(w, 3)),

                // --- Techos ---
                P("techo_regolito", Tr.T("Techo blindado"), Tr.T("Losa gruesa de regolito: hermética y frena la radiación."),
                  PieceCategory.Roofs, PieceShape.ArmoredRoof, PieceSlotType.Deck, true, R(b, 6)),

                // --- Estructura ---
                P("pilar", Tr.T("Pilar de titanio"), Tr.T("Pilar en una esquina: sostiene techos y pisos sin paredes."),
                  PieceCategory.Structure, PieceShape.Pillar, PieceSlotType.Corner, false, R(m, 2)),
                P("escalera", Tr.T("Escalera"), Tr.T("Escalera de un piso (deja la celda de arriba sin losa para pasar). La rueda la gira."),
                  PieceCategory.Structure, PieceShape.Stairs, PieceSlotType.CellObject, false, R(m, 4)),
            };
        }

        private static BuildPieceDefinition P(string id, string name, string description, PieceCategory category,
                                              PieceShape shape, PieceSlotType slot, bool seals, params ResourceAmount[] cost)
        {
            return new BuildPieceDefinition
            {
                id = id, name = name, description = description, category = category,
                shape = shape, slot = slot, seals = seals, cost = cost
            };
        }
    }
}
