using MarsRobot.Base;
using UnityEngine;

namespace MarsRobot.Items
{
    /// <summary>
    /// Almacén reutilizable: la bodega del cohete nodriza, la tolva de la fundición y las cajas
    /// exteriores. Se abre con F (apuntando o estando cerca) y permite pasar objetos rápidamente
    /// entre el robot y el almacén. Con la tecla I abierta a la vez se ven las dos partes.
    ///
    /// Si <see cref="convertToBaseResources"/> está activo (bodega del cohete), lo que se entrega y
    /// tiene equivalencia se convierte en recursos del almacén de la base (hierro, regolito…).
    /// </summary>
    public class StorageContainer : BaseStation
    {
        [SerializeField, Min(0)] private int slots = 40;
        [Tooltip("Masa máxima (kg). 0 = sin límite.")]
        [SerializeField, Min(0f)] private float maxMassKg;
        [Tooltip("Volumen máximo (L). 0 = sin límite.")]
        [SerializeField, Min(0f)] private float maxVolumeLiters;
        [Tooltip("Convertir lo entregado en recursos de la base (bodega del cohete).")]
        [SerializeField] private bool convertToBaseResources;
        [Tooltip("Si está activo, solo acepta minerales (p. ej. la tolva de la fundición).")]
        [SerializeField] private bool mineralsOnly;

        private ItemContainer _storage;
        private string _lastAction = "";

        public ItemContainer Storage
        {
            get
            {
                if (_storage == null) _storage = new ItemContainer(slots, maxMassKg, maxVolumeLiters);
                return _storage;
            }
        }

        public bool MineralsOnly
        {
            get { return mineralsOnly; }
        }

        /// <summary>Configura el almacén por código.</summary>
        public void Configure(int slotCount, float massKg, float volumeLiters, bool convertToBase, bool onlyMinerals)
        {
            slots = slotCount;
            maxMassKg = massKg;
            maxVolumeLiters = volumeLiters;
            convertToBaseResources = convertToBase;
            mineralsOnly = onlyMinerals;
            _storage = new ItemContainer(slots, maxMassKg, maxVolumeLiters);
        }

        public bool Accepts(ItemData item)
        {
            return !mineralsOnly || item.itemCategory == ItemCategory.Mineral || item.itemCategory == ItemCategory.Volatile;
        }

        /// <summary>
        /// Usa un contenedor existente (p. ej. el almacén de la base) en vez de uno propio: así la
        /// bodega del hangar y los armarios del taller muestran las mismas casillas. Los objetos
        /// entran tal cual, sin convertirse.
        /// </summary>
        public void UseContainer(ItemContainer shared)
        {
            _storage = shared;
            slots = shared.MaxSlots;
            convertToBaseResources = false;
            mineralsOnly = false;
        }

        /// <summary>
        /// Recibe una pila del robot. En la bodega del cohete, lo convertible pasa al almacén de la base.
        /// Devuelve cuántas unidades se entregaron.
        /// </summary>
        public int Receive(ItemContainer from, ItemStack stack, int count)
        {
            if (!Accepts(stack.item)) return 0;
            count = Mathf.Min(count, stack.count);

            if (convertToBaseResources && stack.item.convertsToBaseResource && Owner != null)
            {
                int perUnit = Mathf.Max(0, stack.item.baseResourcePerUnit);
                // Respeta la capacidad del almacén (agua/oxígeno limitados por tanques).
                int accepted = count;
                int capacity = Owner.Inventory.GetCapacity(stack.item.baseResource);
                if (capacity >= 0 && perUnit > 0)
                {
                    int room = capacity - Owner.Inventory.Get(stack.item.baseResource);
                    accepted = Mathf.Clamp(room / perUnit, 0, count);
                }
                if (accepted <= 0) return 0;
                // El estado de conservación reduce lo que se aprovecha (hielo medio sublimado, etc.).
                int produced = Mathf.RoundToInt(accepted * perUnit * Mathf.Clamp01(stack.condition));
                Owner.Inventory.Add(stack.item.baseResource, produced);
                from.RemoveFromSlot(stack, accepted);
                return accepted;
            }
            return from.MoveTo(stack, Storage, count);
        }

        /// <summary>"Transferir todos los minerales": vacía del robot todo lo aceptado.</summary>
        public int ReceiveAll(ItemContainer from, bool onlyMinerals)
        {
            int total = 0;
            var list = from.Slots;
            for (int i = list.Count - 1; i >= 0; i--)
            {
                if (i >= list.Count) continue;
                ItemStack s = list[i];
                if (onlyMinerals && s.item.itemCategory != ItemCategory.Mineral && s.item.itemCategory != ItemCategory.Volatile) continue;
                total += Receive(from, s, s.count);
            }
            return total;
        }

        public override string Prompt
        {
            get { return Tr.T("F: abrir ") + DisplayName; }
        }

        public override void DrawMenu(RobotBaseLink robot)
        {
            var inv = robot.GetComponent<RobotInventory>();
            if (inv == null)
            {
                GUILayout.Label(Tr.T("Este robot no tiene bahía de carga."));
                return;
            }

            if (convertToBaseResources)
            {
                GUILayout.Label(Tr.T("Lo que entregues aquí se convierte en recursos del almacén de la base ") +
                                Tr.T("(la mena de hierro, en hierro; el hielo, en hielo…)."));
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(Tr.T("Transferir todos los minerales"), GUILayout.Width(260)))
            {
                _lastAction = Tr.T("Transferidas ") + ReceiveAll(inv.Bay, true) + Tr.T(" unidades.");
            }
            if (!mineralsOnly && GUILayout.Button(Tr.T("Transferir todo"), GUILayout.Width(160)))
            {
                _lastAction = Tr.T("Transferidas ") + ReceiveAll(inv.Bay, false) + Tr.T(" unidades.");
            }
            GUILayout.EndHorizontal();
            GUILayout.Label(Tr.T("Pulsa I para ver tu bahía y transferir objetos sueltos."));

            if (!convertToBaseResources)
            {
                GUILayout.Space(4);
                GUILayout.Label(Tr.T("<b>Contenido</b> (") + Storage.TotalMassKg.ToString("0") + " kg, " + Storage.TotalVolumeLiters.ToString("0") + " L)");
                var list = Storage.Slots;
                for (int i = 0; i < list.Count && i < 12; i++)
                {
                    ItemStack s = list[i];
                    GUILayout.BeginHorizontal();
                    GUILayout.Label(s.item.itemName + " ×" + s.count, GUILayout.Width(260));
                    if (GUILayout.Button(Tr.T("Sacar al robot"), GUILayout.Width(140)))
                    {
                        int moved = Storage.MoveTo(s, inv.Bay, s.count);
                        _lastAction = moved > 0 ? Tr.T("Recogidas ") + moved + "." : Tr.T("No cabe en la bahía (masa o volumen).");
                    }
                    GUILayout.EndHorizontal();
                }
                if (list.Count == 0) GUILayout.Label(Tr.T("(vacío)"));
            }
            if (_lastAction.Length > 0) GUILayout.Label("<b>" + _lastAction + "</b>");
        }
    }
}
