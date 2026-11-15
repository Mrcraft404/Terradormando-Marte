using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Receta de la impresora: entra materia prima, sale material de construcción.</summary>
    [Serializable]
    public class FabricationRecipe
    {
        public string name;
        public ResourceAmount[] inputs;
        public ResourceAmount output;
        [Tooltip("Segundos reales que tarda.")]
        [Min(0.1f)] public float seconds = 3f;

        public FabricationRecipe(string name, ResourceAmount output, float seconds, params ResourceAmount[] inputs)
        {
            this.name = name;
            this.output = output;
            this.seconds = seconds;
            this.inputs = inputs;
        }
    }

    /// <summary>
    /// Impresora/fabricador de la base: transforma recursos brutos en materiales para construir
    /// y para mejorar el robot. Fabrica de uno en uno, con barra de progreso.
    /// (La colocación de estructuras con esos materiales será el siguiente paso.)
    /// </summary>
    public sealed class FabricatorStation : BaseStation
    {
        [SerializeField] private List<FabricationRecipe> recipes = DefaultRecipes();

        private FabricationRecipe _current;
        private float _progress;

        /// <summary>Se lanza al terminar una pieza.</summary>
        public event Action<FabricationRecipe> Fabricated;

        public bool IsBusy
        {
            get { return _current != null; }
        }

        public static List<FabricationRecipe> DefaultRecipes()
        {
            return new List<FabricationRecipe>
            {
                new FabricationRecipe("Piezas metálicas", new ResourceAmount(ResourceType.MetalParts, 1), 3f,
                    new ResourceAmount(ResourceType.Iron, 3)),
                new FabricationRecipe("Cableado", new ResourceAmount(ResourceType.Wiring, 2), 3f,
                    new ResourceAmount(ResourceType.Iron, 1), new ResourceAmount(ResourceType.Silicon, 1)),
                new FabricationRecipe("Células solares", new ResourceAmount(ResourceType.SolarCells, 1), 4f,
                    new ResourceAmount(ResourceType.Silicon, 3)),
                new FabricationRecipe("Bloques de regolito", new ResourceAmount(ResourceType.RegolithBricks, 1), 2f,
                    new ResourceAmount(ResourceType.Regolith, 5)),
                new FabricationRecipe("Agua (fundir hielo)", new ResourceAmount(ResourceType.Water, 1), 2f,
                    new ResourceAmount(ResourceType.Ice, 2)),
            };
        }

        private void Update()
        {
            if (_current == null) return;
            _progress += Time.deltaTime / _current.seconds;
            if (_progress < 1f) return;

            Owner.Inventory.Add(_current.output.type, _current.output.amount);
            FabricationRecipe done = _current;
            _current = null;
            _progress = 0f;
            if (Fabricated != null) Fabricated(done);
        }

        public bool TryStart(FabricationRecipe recipe)
        {
            if (_current != null || recipe == null) return false;
            if (Owner.Inventory.IsFull(recipe.output.type)) return false; // p. ej. agua sin sitio en los tanques
            if (!Owner.Inventory.TrySpend(recipe.inputs)) return false;
            _current = recipe;
            _progress = 0f;
            return true;
        }

        public override void DrawMenu(RobotBaseLink robot)
        {
            GUILayout.Label("Transforma recursos brutos en materiales. Fabrica una pieza cada vez.");
            if (_current != null)
            {
                GUILayout.Label("<b>Fabricando:</b> " + _current.name + "  " + Mathf.RoundToInt(_progress * 100f) + " %");
            }
            GUILayout.Space(6);

            for (int i = 0; i < recipes.Count; i++)
            {
                FabricationRecipe r = recipes[i];
                bool affordable = Owner.Inventory.Has(r.inputs);
                GUILayout.BeginHorizontal();
                GUI.enabled = affordable && _current == null;
                if (GUILayout.Button((i + 1) + ". " + r.name, GUILayout.Width(220)) || (GUI.enabled && NumberPressed(i)))
                {
                    TryStart(r);
                }
                GUI.enabled = true;
                GUILayout.Label(BaseInventory.Describe(r.inputs) + "  →  " + r.output.amount + " " +
                                BaseInventory.Label(r.output.type) + "  (" + r.seconds.ToString("0") + " s)" +
                                (affordable ? "" : "  <color=#ff8080>faltan recursos</color>"));
                GUILayout.EndHorizontal();
            }
        }

        private static bool NumberPressed(int index)
        {
            Event e = Event.current;
            return e.type == EventType.KeyDown && e.keyCode == KeyCode.Alpha1 + index;
        }
    }
}
