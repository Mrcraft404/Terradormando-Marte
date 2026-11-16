using MarsRobot.Interaction;
using MarsRobot.Items;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Un tramo del terraplén (berma) de regolito alrededor del cohete. El rover apunta y pulsa F
    /// para volcar regolito de su bahía; el tramo crece hasta su altura completa.
    /// El regolito es uno de los mejores blindajes disponibles en Marte: no hay que traerlo de la Tierra.
    /// </summary>
    public sealed class BermSegment : MonoBehaviour, IInteractable
    {
        /// <summary>Unidades de regolito para completar un tramo.</summary>
        public const int UnitsNeeded = 8;
        private const float FullHeight = 1.8f;

        private RocketBaseManager _manager;
        private int _index;
        private int _units;
        private Transform _mound;

        public float Fill01
        {
            get { return Mathf.Clamp01(_units / (float)UnitsNeeded); }
        }

        public void Init(RocketBaseManager manager, int index)
        {
            _manager = manager;
            _index = index;

            // Base del tramo: marca en el suelo (siempre visible) y colisionador para apuntarle.
            GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Cube);
            outline.name = "Marca";
            outline.transform.SetParent(transform, false);
            outline.transform.localPosition = new Vector3(0f, 0.05f, 0f);
            outline.transform.localScale = new Vector3(2.7f, 0.1f, 1.2f);
            outline.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.35f, 0.25f, 0.2f));

            GameObject mound = GameObject.CreatePrimitive(PrimitiveType.Cube);
            mound.name = "Terraplén";
            Destroy(mound.GetComponent<Collider>());
            mound.transform.SetParent(transform, false);
            mound.GetComponent<Renderer>().sharedMaterial = Mat(new Color(0.6f, 0.36f, 0.24f));
            _mound = mound.transform;
            UpdateMound();
        }

        private void UpdateMound()
        {
            float h = Mathf.Max(0.01f, FullHeight * Fill01);
            _mound.localScale = new Vector3(2.7f, h, 1.2f);
            _mound.localPosition = new Vector3(0f, h * 0.5f, 0f);
            _mound.gameObject.SetActive(_units > 0);
        }

        public float HoldDuration
        {
            get { return 0.6f; }
        }

        public bool CanInteract()
        {
            if (_units >= UnitsNeeded) return false;
            Interactor who = Interactor.Local;
            return who != null && who.Inventory != null && RegolithInBay(who.Inventory) > 0;
        }

        public string GetInteractionPrompt()
        {
            if (_units >= UnitsNeeded) return Tr.T("Tramo ") + _index + Tr.T(" de la berma completo");
            Interactor who = Interactor.Local;
            int have = who != null && who.Inventory != null ? RegolithInBay(who.Inventory) : 0;
            string head = Tr.T("Berma de blindaje, tramo ") + _index + ": " + _units + "/" + UnitsNeeded;
            return have > 0 ? head + Tr.T(" · volcar regolito [mantén F]") : head + Tr.T(" · trae regolito (pala o taladro)");
        }

        public void Interact(Interactor interactor)
        {
            if (!CanInteract()) return;
            // Primero el regolito normal; si no hay, el de percloratos (aquí no molesta: queda fuera).
            int put = interactor.Inventory.Bay.Remove(ItemDatabase.Get(ItemDatabase.Regolith), 1);
            if (put == 0) put = interactor.Inventory.Bay.Remove(ItemDatabase.Get(ItemDatabase.PerchlorateRegolith), 1);
            if (put == 0) return;
            _units += put;
            UpdateMound();
            interactor.ShowFeedback(_manager != null ? _manager.DescribeShielding() : Tr.T("Regolito volcado."));
        }

        private static int RegolithInBay(RobotInventory inv)
        {
            return inv.Bay.Count(ItemDatabase.Get(ItemDatabase.Regolith)) + inv.Bay.Count(ItemDatabase.Get(ItemDatabase.PerchlorateRegolith));
        }

        private static Material Mat(Color c)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            return new Material(shader) { color = c };
        }
    }
}
