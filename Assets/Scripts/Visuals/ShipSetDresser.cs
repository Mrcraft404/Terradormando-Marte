using MarsRobot.Base;
using MarsRobot.Base.Ship;
using UnityEngine;

namespace MarsRobot.Visuals
{
    /// <summary>
    /// Reparte atrezo por las mesas y suelos de la nave (hangar y talleres) con variación orgánica
    /// (SetDressing). Lo que va sobre mesas es estático (solo colisionador ajustado); las cajas del
    /// suelo son dinámicas (Rigidbody con masa por volumen): el robot puede empujarlas.
    /// El atrezo de cada nivel cuelga del propio nivel (grupo "Atrezo"), así el inspector de la nave
    /// (K) lo muestra u oculta junto con su nivel.
    /// </summary>
    public static class ShipSetDresser
    {
        public static void Dress(RocketBase rocketBase, int seed)
        {
            if (rocketBase == null || rocketBase.Mothership == null)
            {
                Debug.LogWarning("[ShipSetDresser] No hay nave que decorar.");
                return;
            }
            ShipModel ship = rocketBase.Mothership;
            var rng = new System.Random(seed);

            // ---- Hangar (nivel 1) ----
            Transform hangar = Group(ship, ShipModel.HangarLevel);
            if (hangar != null)
            {
                // Bancos de trabajo traseros (tablero a 0,9 m).
                Surface(hangar, new Vector3(-2.0f, 0.9f, -2.65f), 0f, new Vector2(0.55f, 0.22f), rng,
                    PropKind.Toolbox, PropKind.Tablet, PropKind.Mug, PropKind.CableCoil);
                Surface(hangar, new Vector3(2.0f, 0.9f, -2.65f), 0f, new Vector2(0.55f, 0.22f), rng,
                    PropKind.SampleCase, PropKind.PartsTray, PropKind.GasBottle, PropKind.Binder);
                // Cajas en el suelo, fuera del paso del robot y de la puerta.
                FloorCrate(hangar, new Vector3(-3.2f, 0f, 1.9f), 30f, rng);
                FloorCrate(hangar, new Vector3(-3.6f, 0f, -1.3f), 75f, rng);
                FloorCrate(hangar, new Vector3(3.1f, 0f, 2.1f), -35f, rng);
            }

            // ---- Talleres (nivel 2) ----
            Transform shop = Group(ship, ShipModel.WorkshopLevel);
            if (shop != null)
            {
                // Banco de trabajo (tablero a 0,935 m; la parte derecha tiene la cajonera debajo).
                Surface(shop, ShipLayout.Workbench, ShipLayout.WorkbenchFacing, new Vector3(-0.3f, 0.935f, 0.12f), new Vector2(0.55f, 0.2f), rng,
                    PropKind.Toolbox, PropKind.PartsTray, PropKind.Binder, PropKind.Mug);
                // Encima de los armarios del almacén.
                Surface(shop, ShipLayout.Lockers, 180f, new Vector3(0f, 2.1f, 0f), new Vector2(1.3f, 0.12f), rng,
                    PropKind.Crate, PropKind.SampleCase, PropKind.Crate);
                // Junto a la fundición y la refinería.
                Surface(shop, ShipLayout.Smelter, ShipLayout.SmelterFacing, new Vector3(-0.75f, 0.16f, 0.75f), new Vector2(0.1f, 0.1f), rng, PropKind.GasBottle);
                Surface(shop, ShipLayout.Refinery, ShipLayout.RefineryFacing, new Vector3(0.75f, 0.12f, 0.55f), new Vector2(0.1f, 0.1f), rng, PropKind.GasBottle);
                FloorCrate(shop, new Vector3(-1.95f, 0f, 3.55f), 15f, rng);
                FloorCrate(shop, new Vector3(2.0f, 0f, 3.5f), -20f, rng);
            }
        }

        private static Transform Group(ShipModel ship, int level)
        {
            if (level < 0 || level >= ship.Levels.Length || ship.Levels[level] == null)
            {
                Debug.LogWarning("[ShipSetDresser] Falta el nivel " + (level + 1) + ".");
                return null;
            }
            Transform existing = ship.Levels[level].Find("Atrezo");
            if (existing != null) return existing;
            var g = new GameObject("Atrezo").transform;
            g.SetParent(ship.Levels[level], false);
            return g;
        }

        /// <summary>Superficie en coordenadas del nivel (centro del tablero, sin giro).</summary>
        private static void Surface(Transform group, Vector3 center, float yaw, Vector2 halfExtents, System.Random rng, params PropKind[] kinds)
        {
            Surface(group, center, yaw, Vector3.zero, halfExtents, rng, kinds);
        }

        /// <summary>
        /// Superficie de una máquina: <paramref name="origin"/> y <paramref name="yaw"/> son la posición
        /// y orientación de la máquina; <paramref name="local"/> el centro del tablero en sus coordenadas.
        /// Los objetos se reparten a lo largo del tablero, cada uno con su variación orgánica.
        /// </summary>
        private static void Surface(Transform group, Vector3 origin, float yaw, Vector3 local, Vector2 halfExtents, System.Random rng, params PropKind[] kinds)
        {
            var anchor = new GameObject("Superficie").transform;
            anchor.SetParent(group, false);
            anchor.localPosition = origin;
            anchor.localRotation = Quaternion.Euler(0f, yaw, 0f);
            for (int i = 0; i < kinds.Length; i++)
            {
                float t = kinds.Length == 1 ? 0.5f : i / (float)(kinds.Length - 1);
                float x = Mathf.Lerp(-halfExtents.x, halfExtents.x, t) + (float)(rng.NextDouble() - 0.5) * 0.06f;
                float z = (float)(rng.NextDouble() * 2.0 - 1.0) * halfExtents.y;
                GameObject prop = PropLibrary.Create(kinds[i]);
                SetDressing.Place(prop, anchor, local + new Vector3(x, 0f, z), (float)(rng.NextDouble() * 360.0) * (kinds[i] == PropKind.CableCoil ? 1f : 0f), rng);
                ColliderFitter.FitStatic(prop);
            }
        }

        private static void FloorCrate(Transform group, Vector3 pos, float yaw, System.Random rng)
        {
            GameObject crate = PropLibrary.Create(PropKind.Crate);
            SetDressing.Place(crate, group, pos + Vector3.up * 0.09f, yaw, rng);
            ColliderFitter.FitDynamic(crate, 150f); // contenedor de plástico con material: ~150 kg/m³ → ~14 kg
        }
    }
}
