using System.Collections.Generic;
using MarsRobot.Items;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Fundición del cohete: procesa las menas y el regolito que trae el rover y deja el resultado
    /// en el almacén de la base. Consume energía de la red mientras trabaja.
    ///  • Mena de hierro → 3 de hierro (reducción del óxido de hierro).
    ///  • 5 de regolito → 1 bloque sinterizado.
    ///  • Regolito con percloratos → regolito limpio y algo de oxígeno (los percloratos lo liberan al calentarse).
    ///  • Arcillas → silicio. Sulfatos → muestras para el laboratorio. Hielo → agua.
    /// </summary>
    public sealed class SmelterStation : BaseStation
    {
        [SerializeField, Min(0f)] private float powerWatts = 2000f;
        [SerializeField, Min(0.1f)] private float secondsPerItem = 2.5f;

        private readonly ItemContainer _queue = new ItemContainer(0, 0f, 0f);
        private float _progress;
        private int _regolithBuffer;
        private int _perchlorateBuffer;
        private string _lastAction = "";

        public bool IsWorking
        {
            get { return _queue.Slots.Count > 0; }
        }

        private void Update()
        {
            if (Owner == null) return;
            bool working = _queue.Slots.Count > 0;
            Owner.Grid.SetExternalDemand(this, working ? powerWatts : 0f);
            if (!working)
            {
                _progress = 0f;
                return;
            }

            // Con falta de energía en la red, la fundición va más lenta.
            _progress += Time.deltaTime / secondsPerItem * Owner.Grid.SupplyRatio;
            if (_progress < 1f) return;
            _progress = 0f;

            ItemStack s = _queue.Slots[0];
            ItemData item = s.item;
            _queue.RemoveFromSlot(s, 1);
            Process(item);
        }

        private void OnDestroy()
        {
            if (Owner != null && Owner.Grid != null) Owner.Grid.SetExternalDemand(this, 0f);
        }

        private void Process(ItemData item)
        {
            BaseInventory inv = Owner.Inventory;
            switch (item.itemID)
            {
                case ItemDatabase.IronOre:
                    inv.Add(ResourceType.Iron, 3);
                    break;
                case ItemDatabase.Regolith:
                    if (++_regolithBuffer >= 5)
                    {
                        _regolithBuffer = 0;
                        inv.Add(ResourceType.RegolithBricks, 1);
                    }
                    break;
                case ItemDatabase.PerchlorateRegolith:
                    inv.Add(ResourceType.Regolith, 1);
                    if (++_perchlorateBuffer >= 2)
                    {
                        _perchlorateBuffer = 0;
                        inv.Add(ResourceType.Oxygen, 1);
                    }
                    break;
                case ItemDatabase.Clays:
                    inv.Add(ResourceType.Silicon, 1);
                    break;
                case ItemDatabase.Sulfates:
                    inv.Add(ResourceType.Samples, 1);
                    break;
                case ItemDatabase.Ice:
                    inv.Add(ResourceType.Water, 1);
                    break;
                default:
                    if (item.convertsToBaseResource) inv.Add(item.baseResource, item.baseResourcePerUnit);
                    break;
            }
        }

        public override void DrawMenu(RobotBaseLink robot)
        {
            GUILayout.Label("Funde menas y procesa regolito. El resultado va al almacén de la base. Gasta " +
                            powerWatts.ToString("0") + " W de la red mientras trabaja.");
            GUILayout.Label("Mena de hierro → 3 hierro · 5 regolito → 1 bloque · percloratos → regolito + oxígeno · " +
                            "arcillas → silicio · sulfatos → muestras · hielo → agua");

            var inv = robot.GetComponent<RobotInventory>();
            if (inv != null && GUILayout.Button("Transferir todos los minerales a la fundición", GUILayout.Width(360)))
            {
                int moved = 0;
                var list = inv.Bay.Slots;
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (i >= list.Count) continue;
                    ItemStack s = list[i];
                    if (s.item.itemCategory != ItemCategory.Mineral && s.item.itemCategory != ItemCategory.Volatile) continue;
                    moved += inv.Bay.MoveTo(s, _queue, s.count);
                }
                _lastAction = moved > 0 ? "Cargadas " + moved + " unidades en la tolva." : "No llevas minerales.";
            }

            GUILayout.Space(4);
            int pending = 0;
            var queued = new Dictionary<string, int>();
            foreach (ItemStack s in _queue.Slots)
            {
                pending += s.count;
                int n;
                queued.TryGetValue(s.item.itemName, out n);
                queued[s.item.itemName] = n + s.count;
            }
            GUILayout.Label("<b>En la tolva:</b> " + (pending == 0 ? "vacía" : pending + " unidades"));
            foreach (var kv in queued) GUILayout.Label("  · " + kv.Key + " ×" + kv.Value);
            if (IsWorking)
            {
                GUILayout.Label("Procesando… " + Mathf.RoundToInt(_progress * 100f) + " %" +
                                (Owner.Grid.SupplyRatio < 0.99f ? "  <color=#ff8080>(falta energía en la red)</color>" : ""));
            }
            if (_lastAction.Length > 0) GUILayout.Label("<b>" + _lastAction + "</b>");
        }
    }
}
