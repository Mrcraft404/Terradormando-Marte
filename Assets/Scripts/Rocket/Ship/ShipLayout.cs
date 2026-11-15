using UnityEngine;

namespace MarsRobot.Base.Ship
{
    /// <summary>
    /// Posiciones de las máquinas dentro de la nave, compartidas por el modelo visual
    /// (ShipInteriorBuilder) y por los componentes que funcionan (RocketBase). Coordenadas locales
    /// de cada nivel: suelo en y = 0, +Z = frente (puerta de carga), núcleo de escalera y
    /// montacargas detrás (x ±1,1, z −3,9…−2,3).
    /// </summary>
    public static class ShipLayout
    {
        // --- Nivel 1 · Hangar del robot ---
        /// <summary>Plaza donde se acopla el robot (y donde aparece al empezar).</summary>
        public static readonly Vector3 RobotDock = new Vector3(0f, 0f, 0.6f);
        /// <summary>Pórtico del taller de modificaciones (cambio de módulos del robot).</summary>
        public static readonly Vector3 AssemblyBay = new Vector3(0f, 0f, -1.4f);
        /// <summary>Bodega (estanterías de la derecha): el robot entrega aquí su carga.</summary>
        public static readonly Vector3 Hold = new Vector3(3.3f, 0f, 0f);
        /// <summary>Terminal del cohete (banco de la izquierda).</summary>
        public static readonly Vector3 Terminal = new Vector3(-3.3f, 0f, 0f);

        // --- Nivel 2 · Talleres de fabricación (astronauta) ---
        public static readonly Vector3 Fabricator = new Vector3(-2.75f, 0f, 1.55f);
        public static readonly Vector3 Smelter = new Vector3(-3.0f, 0f, -1.15f);
        public static readonly Vector3 Refinery = new Vector3(3.0f, 0f, -1.15f);
        public static readonly Vector3 Workbench = new Vector3(2.75f, 0f, 1.7f);
        /// <summary>Armarios del almacén de la base (pared delantera).</summary>
        public static readonly Vector3 Lockers = new Vector3(0f, 0f, 3.75f);

        /// <summary>Hacia dónde mira el frente de cada máquina (grados, giro en Y).</summary>
        public const float FabricatorFacing = 60f;
        public const float SmelterFacing = 90f;
        public const float RefineryFacing = -90f;
        public const float WorkbenchFacing = -60f;

        // --- Niveles 3–6: máquinas contra la pared, mirando al centro (que queda libre para caminar) ---

        /// <summary>Posición en la pared de la nave: ángulo (0° = puerta, +90° = derecha) y distancia al eje.</summary>
        public struct Spot
        {
            public float Deg, Radius;

            public Spot(float deg, float radius)
            {
                Deg = deg;
                Radius = radius;
            }

            public Vector3 Position
            {
                get { return new Vector3(Mathf.Sin(Deg * Mathf.Deg2Rad) * Radius, 0f, Mathf.Cos(Deg * Mathf.Deg2Rad) * Radius); }
            }

            /// <summary>Giro para que el frente (+Z) mire al centro del nivel.</summary>
            public float FacingCenter
            {
                get { return Deg + 180f; }
            }
        }

        // Nivel 3 · Industria
        public static readonly Spot Printer3D = new Spot(-60f, 3.35f);
        public static readonly Spot ElectronicsBench = new Spot(60f, 3.4f);
        public static readonly Spot MineralBins = new Spot(0f, 3.95f);
        public static readonly Spot IndustryShelfL = new Spot(-118f, 3.95f);
        public static readonly Spot IndustryShelfR = new Spot(118f, 3.95f);
        // Nivel 4 · Ciencia y agricultura
        public static readonly Spot GardenA = new Spot(-105f, 3.75f);
        public static readonly Spot GardenB = new Spot(-45f, 3.75f);
        public static readonly Spot Laboratory = new Spot(55f, 3.45f);
        public static readonly Spot Germination = new Spot(115f, 3.9f);
        public static readonly Spot SeedShelf = new Spot(0f, 4.0f);
        // Nivel 5 · Recursos y supervivencia
        public static readonly Spot WaterPlant = new Spot(-55f, 3.45f);
        public static readonly Spot OxygenPlant = new Spot(-112f, 3.55f);
        public static readonly Spot Kitchen = new Spot(55f, 3.45f);
        public static readonly Spot Pantry = new Spot(112f, 3.75f);
        public static readonly Spot WaterTanks = new Spot(0f, 3.95f);
        // Nivel 6 · Control
        public static readonly Spot Servers = new Spot(-105f, 3.75f);
        public static readonly Spot Comms = new Spot(105f, 3.75f);

        /// <summary>Plantas de una bandeja del huerto (coordenadas de la estantería: x, altura de la bandeja).</summary>
        public static readonly float[] GardenTrayHeights = { 0.5f, 1.2f, 1.9f };
        public static readonly float[] GardenPlantX = { -0.72f, 0f, 0.72f };

        /// <summary>Delante de la puerta del montacargas (núcleo), en cualquier nivel.</summary>
        public static readonly Vector3 LiftDoor = new Vector3(0.5f, 0f, -1.7f);
        /// <summary>Dónde aparece el astronauta al empezar (nivel de talleres).</summary>
        public static readonly Vector3 CrewSpawn = new Vector3(0f, 0f, 0.6f);

        /// <summary>Convierte una posición de un nivel a coordenadas locales de la nave.</summary>
        public static Vector3 OnLevel(int level, Vector3 local)
        {
            return local + Vector3.up * ShipModel.LevelFloorY(level);
        }

        /// <summary>Nivel (0..5) que contiene una altura local de la nave, o −1 si está fuera.</summary>
        public static int LevelAt(float localY)
        {
            for (int i = 0; i < ShipModel.LevelCount; i++)
            {
                float y0 = ShipModel.LevelFloorY(i);
                if (localY >= y0 - 0.5f && localY < y0 + ShipModel.LevelHeight - 0.5f) return i;
            }
            return -1;
        }
    }
}
