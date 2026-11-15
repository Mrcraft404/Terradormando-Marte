using MarsRobot.Base.Ship;
using MarsRobot.Visuals;
using UnityEngine;

namespace MarsRobot.Crafting
{
    /// <summary>
    /// Muestra en la estantería del huerto lo que crece en cada bandeja: la bandeja i corresponde al
    /// encargo i de la cola (cada encargo es un cultivo). Las plantas crecen con el progreso, florecen o
    /// dan fruto al madurar y desaparecen al cosechar (la cosecha va al inventario de la estación).
    /// </summary>
    [RequireComponent(typeof(CraftingStation))]
    public sealed class GardenVisual : MonoBehaviour
    {
        private CraftingStation _station;
        private string[] _species;
        private PlantInstance[][] _plants;
        private float _next;

        private void Awake()
        {
            _station = GetComponent<CraftingStation>();
            int trays = ShipLayout.GardenTrayHeights.Length;
            _species = new string[trays];
            _plants = new PlantInstance[trays][];
        }

        private void Update()
        {
            if (_station == null || _station.Queue == null || Time.time < _next) return;
            _next = Time.time + 0.25f;
            CraftingQueue q = _station.Queue;
            for (int tray = 0; tray < _species.Length; tray++)
            {
                string species = tray < q.Entries.Count ? q.Entries[tray].Recipe.OutputId : null;
                if (species != _species[tray]) Replant(tray, species);
                if (_plants[tray] == null) continue;
                float g = q.ProgressOf(tray);
                foreach (PlantInstance p in _plants[tray]) PlantModels.SetGrowth(p, g);
            }
        }

        private void Replant(int tray, string species)
        {
            if (_plants[tray] != null)
                foreach (PlantInstance p in _plants[tray])
                    if (p != null && p.Root != null) Destroy(p.Root.gameObject);
            _plants[tray] = null;
            _species[tray] = species;
            if (species == null) return;

            float[] xs = ShipLayout.GardenPlantX;
            _plants[tray] = new PlantInstance[xs.Length];
            for (int i = 0; i < xs.Length; i++)
            {
                var anchor = new GameObject("Bandeja " + (tray + 1) + " · planta " + (i + 1)).transform;
                anchor.SetParent(transform, false);
                anchor.localPosition = new Vector3(xs[i], ShipLayout.GardenTrayHeights[tray] + 0.01f, 0.02f);
                anchor.localRotation = Quaternion.Euler(0f, i * 47f, 0f);
                PlantInstance p = PlantModels.Build(species, anchor, tray * 31 + i * 7 + species.Length);
                p.Root = anchor; // al cosechar se borra el ancla con la planta
                _plants[tray][i] = p;
            }
        }
    }
}
