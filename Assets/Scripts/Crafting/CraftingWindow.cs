using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Crew;
using MarsRobot.Items;
using MarsRobot.UI;
using UnityEngine;

namespace MarsRobot.Crafting
{
    /// <summary>
    /// Ventana de inventario y fabricación (estilo juego de supervivencia, en tres columnas):
    ///  • Izquierda: inventario del astronauta (casillas, peso, buscador).
    ///  • Centro: ficha del objeto o plano seleccionado (materiales tengo/necesito, botones de
    ///    fabricar ×1 / ×5 / máx, usar, dividir) y la cola de fabricación con su progreso.
    ///  • Derecha: la máquina o almacén abierto, con pestañas INVENTARIO y FABRICACIÓN.
    /// Clic: seleccionar · Clic derecho o Mayús+clic: transferir la pila · Arrastrar: mover ·
    /// Doble clic (o clic derecho) en un plano: fabricar 1 · Esc / F / I: cerrar.
    /// </summary>
    public sealed class CraftingWindow : MonoBehaviour
    {
        private enum Mode { Personal, Station, Container }

        private static CraftingWindow _inst;

        public static bool IsOpen
        {
            get { return _inst != null && _inst._open; }
        }

        private bool _open;
        private int _openedFrame;
        private Mode _mode;
        private Astronaut _astro;
        private CraftingStation _station;
        private ItemContainer _other;
        private string _otherTitle = "";
        private int _rightTab; // 0 inventario, 1 fabricación

        private string _searchL = "", _searchR = "";
        private Vector2 _scrollL, _scrollR, _scrollQ;

        private ItemStack _selStack;
        private bool _selLeft;
        private CraftingRecipe _selRecipe;

        private ItemStack _press;
        private bool _pressLeft;
        private Vector2 _pressPos;
        private bool _dragging;
        private Vector2 _mouse;
        private string _hover = "";

        private string _status = "";
        private float _statusTime = -10f;

        private GUIStyle _count, _center, _small, _header, _bigName, _wrap;

        private static readonly Rect PanelL = new Rect(24, 64, 500, 740);
        private static readonly Rect PanelC = new Rect(540, 64, 520, 740);
        private static readonly Rect PanelR = new Rect(1076, 64, 500, 740);
        private const float Slot = 76f, Gap = 4f;
        private const int Cols = 6;

        // ------------------------------------------------------------------
        // Abrir / cerrar
        // ------------------------------------------------------------------

        private static void Ensure()
        {
            if (_inst != null) return;
            _inst = new GameObject("Ventana de inventario y fabricación").AddComponent<CraftingWindow>();
        }

        public static void OpenStation(Astronaut a, CraftingStation s)
        {
            Ensure();
            _inst.Open(a, Mode.Station, s, s.Inventory, s.DisplayName);
        }

        public static void OpenContainer(Astronaut a, string title, ItemContainer container)
        {
            Ensure();
            _inst.Open(a, Mode.Container, null, container, title);
        }

        public static void OpenPersonal(Astronaut a)
        {
            Ensure();
            _inst.Open(a, Mode.Personal, null, null, Tr.T("Fabricación a mano"));
        }

        private void Open(Astronaut a, Mode mode, CraftingStation station, ItemContainer other, string title)
        {
            _astro = a;
            _mode = mode;
            _station = station;
            _other = other;
            _otherTitle = title;
            _rightTab = mode == Mode.Container ? 0 : 1;
            _selStack = null;
            _selRecipe = null;
            _press = null;
            _dragging = false;
            _open = true;
            _openedFrame = Time.frameCount;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close()
        {
            _open = false;
        }

        public static void CloseWindow()
        {
            if (_inst != null) _inst.Close();
        }

        /// <summary>Prueba automática: selecciona un plano de la pestaña de fabricación.</summary>
        public static void DebugSelectRecipe(int index)
        {
            if (_inst == null || !_inst._open) return;
            List<CraftingRecipe> list = _inst.Recipes;
            if (index < 0 || index >= list.Count) return;
            _inst._rightTab = 1;
            _inst._selRecipe = list[index];
            _inst._selStack = null;
        }

        private void Update()
        {
            if (!_open || Time.frameCount == _openedFrame) return;
            if (_astro == null || !_astro.Active)
            {
                Close();
                return;
            }
            bool typing = GUIUtility.keyboardControl != 0;
            if (Input.GetKeyDown(KeyCode.Escape) || (!typing && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.I)))) Close();
        }

        // ------------------------------------------------------------------
        // Datos de la columna derecha
        // ------------------------------------------------------------------

        private CraftingQueue Queue
        {
            get
            {
                if (_mode == Mode.Station) return _station.Queue;
                if (_mode == Mode.Personal) return _astro.HandQueue;
                return null;
            }
        }

        /// <summary>De dónde salen los ingredientes (como en el juego: del inventario de la máquina).</summary>
        private ItemContainer CraftSource
        {
            get { return _mode == Mode.Station ? _station.Inventory : _astro.Inventory; }
        }

        private List<CraftingRecipe> Recipes
        {
            get { return RecipeBook.For(_mode == Mode.Station ? _station.Station : CraftStation.Hand); }
        }

        private bool HasOtherInventory
        {
            get { return _other != null; }
        }

        // ------------------------------------------------------------------
        // Dibujo
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!_open || _astro == null) return;
            GUI.depth = -10;
            EnsureStyles();

            GUI.color = new Color(0f, 0.02f, 0.04f, 0.6f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;

            HudTheme.BeginScaled();
            Event e = Event.current;
            _mouse = e.mousePosition;
            _hover = "";
            HandleDragEvents(e);

            // Título.
            string title = _mode == Mode.Personal ? Tr.T("INVENTARIO PERSONAL") : _otherTitle.ToUpperInvariant();
            GUI.Label(new Rect(24, 12, 1552, 34), title, _header);
            GUI.Label(new Rect(24, 40, 1552, 20),
                Tr.T("Clic: seleccionar · Clic derecho / Mayús+clic: transferir · Arrastrar: mover entre inventarios · Doble clic en un plano: fabricar · Esc: cerrar"),
                _small);

            DrawLeft();
            DrawCenter();
            DrawRight();
            DrawBottomButtons();

            if (Time.unscaledTime - _statusTime < 4f)
                GUI.Label(new Rect(24, 848, 1552, 26), _status, _center);

            if (_dragging && _press != null && _press.item != null)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(_mouse.x - 30, _mouse.y - 30, 60, 60), ItemIconPainter.For(_press.item));
                GUI.color = Color.white;
            }
            else if (!string.IsNullOrEmpty(_hover))
            {
                Vector2 size = _small.CalcSize(new GUIContent(_hover));
                var tr = new Rect(_mouse.x + 16, _mouse.y + 14, size.x + 16, 24);
                HudTheme.Fill(tr, new Color(0f, 0f, 0f, 0.85f));
                GUI.Label(new Rect(tr.x + 8, tr.y, tr.width, tr.height), _hover, _small);
            }
            HudTheme.EndScaled();
        }

        private void HandleDragEvents(Event e)
        {
            if (_press == null) return;
            if (e.type == EventType.MouseDrag && (_mouse - _pressPos).sqrMagnitude > 36f) _dragging = true;
            if (e.type != EventType.MouseUp) return;
            if (_dragging)
            {
                bool overLeft = PanelL.Contains(_mouse), overRight = PanelR.Contains(_mouse);
                if (_pressLeft && overRight) Transfer(_press, true);
                else if (!_pressLeft && overLeft) Transfer(_press, false);
            }
            _press = null;
            _dragging = false;
            e.Use();
        }

        // ---------------- Izquierda: inventario del astronauta ----------------

        private void DrawLeft()
        {
            ItemContainer inv = _astro.Inventory;
            HudTheme.Frame(PanelL, HudTheme.PanelBg, HudTheme.AccentDim);
            GUI.Label(new Rect(PanelL.x + 16, PanelL.y + 8, 400, 32), Tr.T("INVENTARIO · ASTRONAUTA"), HudTheme.Title);
            float ratio = inv.MaxMassKg > 0f ? inv.TotalMassKg / inv.MaxMassKg : 0f;
            HudTheme.Bar(new Rect(PanelL.x + 16, PanelL.y + 46, PanelL.width - 32, 20), ratio, ratio > 0.9f ? HudTheme.Bad : HudTheme.Accent,
                Tr.T("Peso ") + inv.TotalMassKg.ToString("0.0") + " / " + inv.MaxMassKg.ToString("0") + " kg   ·   " + inv.Slots.Count + " / " + inv.MaxSlots + Tr.T(" casillas"));
            _searchL = SearchField(new Rect(PanelL.x + 16, PanelL.y + 74, PanelL.width - 32, 26), _searchL, "izq");
            DrawGrid(new Rect(PanelL.x + 10, PanelL.y + 110, PanelL.width - 20, PanelL.height - 120), inv, true, _searchL, ref _scrollL);
        }

        // ---------------- Derecha: máquina / almacén ----------------

        private void DrawRight()
        {
            HudTheme.Frame(PanelR, HudTheme.PanelBg, HudTheme.AccentDim);
            float y = PanelR.y + 8;
            if (_mode == Mode.Station)
            {
                var t0 = new Rect(PanelR.x + 12, y, (PanelR.width - 30) * 0.5f, 34);
                var t1 = new Rect(t0.xMax + 6, y, t0.width, 34);
                if (GUI.Button(t0, Tr.T("INVENTARIO"), _rightTab == 0 ? HudTheme.TabActive : HudTheme.Tab)) _rightTab = 0;
                if (GUI.Button(t1, Tr.T("FABRICACIÓN"), _rightTab == 1 ? HudTheme.TabActive : HudTheme.Tab)) _rightTab = 1;
            }
            else
            {
                GUI.Label(new Rect(PanelR.x + 16, y, 460, 32), _mode == Mode.Personal ? Tr.T("FABRICACIÓN A MANO") : _otherTitle.ToUpperInvariant(), HudTheme.Title);
            }
            y += 40;

            if (_rightTab == 0 && HasOtherInventory)
            {
                float ratio = _other.MaxSlots > 0 ? _other.Slots.Count / (float)_other.MaxSlots : 0f;
                HudTheme.Bar(new Rect(PanelR.x + 16, y, PanelR.width - 32, 20), ratio, HudTheme.Accent,
                    _other.Slots.Count + " / " + _other.MaxSlots + Tr.T(" casillas   ·   ") + _other.TotalMassKg.ToString("0") + " kg");
                _searchR = SearchField(new Rect(PanelR.x + 16, y + 28, PanelR.width - 32, 26), _searchR, "der");
                DrawGrid(new Rect(PanelR.x + 10, y + 64, PanelR.width - 20, PanelR.yMax - y - 74), _other, false, _searchR, ref _scrollR);
            }
            else
            {
                GUI.Label(new Rect(PanelR.x + 16, y, PanelR.width - 32, 20),
                    Tr.T("Planos de ") + RecipeBook.StationName(_mode == Mode.Station ? _station.Station : CraftStation.Hand) +
                    (_mode == Mode.Station ? Tr.T(" · usa lo que hay en SU inventario") : Tr.T(" · usa tu inventario")), HudTheme.Small);
                _searchR = SearchField(new Rect(PanelR.x + 16, y + 28, PanelR.width - 32, 26), _searchR, "der");
                DrawRecipeGrid(new Rect(PanelR.x + 10, y + 64, PanelR.width - 20, PanelR.yMax - y - 74));
            }
        }

        // ---------------- Centro: ficha y cola ----------------

        private void DrawCenter()
        {
            HudTheme.Frame(PanelC, HudTheme.PanelBg, HudTheme.AccentDim);
            var r = new Rect(PanelC.x + 18, PanelC.y + 14, PanelC.width - 36, 470);

            if (_selStack != null && !StillThere(_selStack)) _selStack = null;

            if (_selRecipe != null) DrawRecipeDetails(r);
            else if (_selStack != null) DrawItemDetails(r);
            else
            {
                GUI.Label(new Rect(r.x, r.y, r.width, 40), Tr.T("DETALLES"), HudTheme.Title);
                GUI.Label(new Rect(r.x, r.y + 50, r.width, 200),
                    Tr.T("Selecciona un objeto para ver su información, o un plano de la pestaña FABRICACIÓN para ver qué materiales necesita.\n\n") +
                    Tr.T("Las máquinas fabrican con lo que hay en SU inventario: arrastra allí los materiales, fabrica y recoge el resultado.\n\n") +
                    Tr.T("Cadena básica: mena de hierro → lingote (Fundición) → piezas (Fabricador). CO₂ (Refinería) + agua → metano → polímero."),
                    _wrap);
            }

            CraftingQueue q = Queue;
            if (q != null) DrawQueue(new Rect(PanelC.x + 12, PanelC.y + 494, PanelC.width - 24, PanelC.height - 504), q);
        }

        private void DrawRecipeDetails(Rect r)
        {
            CraftingRecipe rec = _selRecipe;
            ItemData output = rec.Output;
            ItemContainer src = CraftSource;
            if (output != null) GUI.DrawTexture(new Rect(r.x, r.y, 96, 96), ItemIconPainter.For(output));
            GUI.Label(new Rect(r.x + 110, r.y, r.width - 110, 34), rec.DisplayName, _bigName);
            GUI.Label(new Rect(r.x + 110, r.y + 36, r.width - 110, 22),
                RecipeBook.StationName(rec.Station) + "   ·   " + CraftingQueue.Duration(rec).ToString("0.#") + Tr.T(" s por unidad"), HudTheme.Subtitle);
            if (output != null) GUI.Label(new Rect(r.x + 110, r.y + 60, r.width - 110, 40), output.description, _wrap);

            float y = r.y + 112;
            GUI.Label(new Rect(r.x, y, r.width, 24), Tr.T("MATERIALES NECESARIOS"), HudTheme.Body);
            y += 28;
            if (rec.Ingredients.Length == 0)
            {
                GUI.Label(new Rect(r.x, y, r.width, 24), Tr.T("Ninguno: se capta de la atmósfera."), HudTheme.Small);
                y += 30;
            }
            for (int i = 0; i < rec.Ingredients.Length; i++)
            {
                Ingredient ing = rec.Ingredients[i];
                ItemData item = ing.Item;
                int have = item != null ? src.Count(item) : 0;
                bool ok = have >= ing.Count || GameModeState.IsCreative;
                var row = new Rect(r.x, y, r.width, 40);
                HudTheme.Fill(row, new Color(0.08f, 0.16f, 0.24f, 0.8f));
                if (item != null) GUI.DrawTexture(new Rect(row.x + 4, row.y + 4, 32, 32), ItemIconPainter.For(item));
                GUI.Label(new Rect(row.x + 44, row.y, row.width - 160, 40), item != null ? item.itemName : ing.ItemId, _wrapMid);
                GUI.Label(new Rect(row.xMax - 110, row.y, 100, 40),
                    "<color=" + (ok ? "#7CFC8A" : "#FF6B6B") + ">" + have + " / " + ing.Count + "</color>", _countBig);
                y += 44;
            }

            int max = GameModeState.IsCreative ? 99 : rec.MaxCraftable(src);
            y += 6;
            GUI.Label(new Rect(r.x, y, r.width, 22), max > 0 ? Tr.T("Puedes fabricar ") + max + Tr.T(" ahora mismo.") : Tr.T("Faltan materiales en ") +
                (_mode == Mode.Station ? Tr.T("el inventario de la máquina.") : Tr.T("tu inventario.")), HudTheme.Small);
            y += 28;
            GUI.enabled = max > 0;
            float bw = (r.width - 16) / 3f;
            if (GUI.Button(new Rect(r.x, y, bw, 40), "×1", HudTheme.BigButton)) Craft(rec, 1);
            if (GUI.Button(new Rect(r.x + bw + 8, y, bw, 40), "×5", HudTheme.BigButton)) Craft(rec, 5);
            if (GUI.Button(new Rect(r.x + (bw + 8) * 2, y, bw, 40), Tr.T("MÁX (") + Mathf.Min(max, 99) + ")", HudTheme.BigButton)) Craft(rec, Mathf.Min(max, 99));
            GUI.enabled = true;
        }

        private void DrawItemDetails(Rect r)
        {
            ItemStack s = _selStack;
            ItemData d = s.item;
            GUI.DrawTexture(new Rect(r.x, r.y, 96, 96), ItemIconPainter.For(d));
            GUI.Label(new Rect(r.x + 110, r.y, r.width - 110, 34), d.itemName + "  ×" + s.count, _bigName);
            GUI.Label(new Rect(r.x + 110, r.y + 36, r.width - 110, 22), CategoryLabel(d.itemCategory) +
                "   ·   " + d.massKg.ToString("0.##") + Tr.T(" kg/u   ·   pila máx. ") + d.maxStackSize, HudTheme.Subtitle);
            GUI.Label(new Rect(r.x + 110, r.y + 60, r.width - 110, 60), d.description, _wrap);
            float y = r.y + 130;
            if (!string.IsNullOrEmpty(d.scientificNotes))
            {
                GUI.Label(new Rect(r.x, y, r.width, 60), "<i>" + d.scientificNotes + "</i>", _wrap);
                y += 64;
            }
            GUI.Label(new Rect(r.x, y, r.width, 22), Tr.T("Masa total: ") + s.MassKg.ToString("0.#") + " kg" +
                (s.condition < 0.999f ? Tr.T("   ·   Estado: ") + Mathf.RoundToInt(s.condition * 100f) + " %" : ""), HudTheme.Body);
            y += 34;

            float bw = (r.width - 16) / 3f;
            if (ItemEffects.IsUsable(d))
            {
                if (GUI.Button(new Rect(r.x, y, bw, 40), Tr.T("USAR EN EL ROBOT"), HudTheme.BigButton)) Use(s);
            }
            bool canTransfer = _selLeft ? HasOtherInventory && _rightTab == 0 : true;
            GUI.enabled = canTransfer;
            if (GUI.Button(new Rect(r.x + bw + 8, y, bw, 40), _selLeft ? Tr.T("TRANSFERIR →") : Tr.T("← TOMAR"), HudTheme.BigButton)) Transfer(s, _selLeft);
            GUI.enabled = s.count > 1;
            if (GUI.Button(new Rect(r.x + (bw + 8) * 2, y, bw, 40), Tr.T("DIVIDIR"), HudTheme.BigButton))
            {
                ItemContainer c = _selLeft ? _astro.Inventory : _other;
                if (c != null && !c.Split(s)) SetStatus(Tr.T("No hay casillas libres para dividir la pila."));
            }
            GUI.enabled = true;
        }

        private void DrawQueue(Rect r, CraftingQueue q)
        {
            HudTheme.Fill(r, new Color(0.02f, 0.06f, 0.1f, 0.85f));
            GUI.Label(new Rect(r.x + 10, r.y + 6, r.width - 20, 24), Tr.T("COLA DE FABRICACIÓN") + (q.Entries.Count == 0 ? Tr.T(" · vacía") : ""), HudTheme.Body);
            if (!string.IsNullOrEmpty(q.Status)) GUI.Label(new Rect(r.x + 10, r.y + 28, r.width - 20, 20), "<color=#ffcf66>" + q.Status + "</color>", HudTheme.Small);
            var view = new Rect(r.x + 6, r.y + 50, r.width - 12, r.height - 56);
            var content = new Rect(0, 0, view.width - 18, Mathf.Max(view.height, q.Entries.Count * 50));
            _scrollQ = GUI.BeginScrollView(view, _scrollQ, content);
            for (int i = 0; i < q.Entries.Count; i++)
            {
                CraftingQueue.Entry en = q.Entries[i];
                var row = new Rect(0, i * 50, content.width, 46);
                HudTheme.Fill(row, new Color(0.08f, 0.16f, 0.24f, 0.9f));
                ItemData o = en.Recipe.Output;
                if (o != null) GUI.DrawTexture(new Rect(row.x + 4, row.y + 4, 38, 38), ItemIconPainter.For(o));
                GUI.Label(new Rect(row.x + 50, row.y + 2, row.width - 140, 22), en.Recipe.DisplayName + "  ×" + en.Remaining, HudTheme.Small);
                if (i < q.Lanes)
                    HudTheme.Bar(new Rect(row.x + 50, row.y + 26, row.width - 140, 14), q.ProgressOf(i), HudTheme.Good,
                        (q.Lanes > 1 ? Tr.T("Bandeja ") + (i + 1) + " · " : "") + Mathf.RoundToInt(q.ProgressOf(i) * 100f) + " %");
                else
                    GUI.Label(new Rect(row.x + 50, row.y + 24, row.width - 140, 18), Tr.T("en espera"), HudTheme.Small);
                if (GUI.Button(new Rect(row.xMax - 82, row.y + 8, 76, 30), Tr.T("Cancelar"), HudTheme.Button))
                {
                    q.Cancel(i, CraftSource);
                    SetStatus(Tr.T("Encargo cancelado: materiales devueltos."));
                    break;
                }
            }
            GUI.EndScrollView();
        }

        private void DrawBottomButtons()
        {
            float y = 812;
            if (HasOtherInventory)
            {
                if (GUI.Button(new Rect(PanelL.x, y, PanelL.width, 30), Tr.T("TRANSFERIR TODO →"), HudTheme.Button)) MoveAll(_astro.Inventory, _other);
                if (GUI.Button(new Rect(PanelR.x, y, PanelR.width, 30), Tr.T("← TOMAR TODO"), HudTheme.Button)) MoveAll(_other, _astro.Inventory);
            }
            if (GUI.Button(new Rect(PanelC.x, y, PanelC.width, 30), Tr.T("CERRAR (Esc)"), HudTheme.Button)) Close();
        }

        // ------------------------------------------------------------------
        // Rejillas
        // ------------------------------------------------------------------

        private void DrawGrid(Rect area, ItemContainer c, bool left, string filter, ref Vector2 scroll)
        {
            var visible = new List<ItemStack>();
            for (int i = 0; i < c.Slots.Count; i++)
                if (Matches(c.Slots[i].item, filter)) visible.Add(c.Slots[i]);
            int total = string.IsNullOrEmpty(filter) ? Mathf.Max(c.MaxSlots, visible.Count) : visible.Count;
            int rows = Mathf.CeilToInt(total / (float)Cols);
            var content = new Rect(0, 0, area.width - 18, Mathf.Max(area.height, rows * (Slot + Gap)));
            scroll = GUI.BeginScrollView(area, scroll, content);
            for (int i = 0; i < total; i++)
            {
                var r = new Rect((i % Cols) * (Slot + Gap), (i / Cols) * (Slot + Gap), Slot, Slot);
                if (r.yMax < scroll.y || r.y > scroll.y + area.height) continue;
                ItemStack s = i < visible.Count ? visible[i] : null;
                DrawSlot(r, s, s != null && s == _selStack, false);
                if (s != null) SlotInput(r, s, left);
            }
            GUI.EndScrollView();
        }

        private void DrawSlot(Rect r, ItemStack s, bool selected, bool dim)
        {
            HudTheme.Fill(r, HudTheme.Slot);
            if (selected) HudTheme.Frame(r, new Color(0, 0, 0, 0), HudTheme.Accent, 2f);
            else
            {
                HudTheme.Fill(new Rect(r.x, r.y, r.width, 1), new Color(0.3f, 0.6f, 0.75f, 0.4f));
                HudTheme.Fill(new Rect(r.x, r.yMax - 1, r.width, 1), new Color(0f, 0f, 0f, 0.5f));
            }
            if (s == null || s.item == null) return;
            GUI.color = dim ? new Color(1f, 1f, 1f, 0.3f) : Color.white;
            GUI.DrawTexture(new Rect(r.x + 8, r.y + 6, r.width - 16, r.height - 16), ItemIconPainter.For(s.item));
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x + 2, r.yMax - 22, r.width - 6, 20), "×" + s.count, _count);
            if (s.condition < 0.999f)
                HudTheme.Fill(new Rect(r.x + 4, r.yMax - 4, (r.width - 8) * s.condition, 3), s.condition > 0.5f ? HudTheme.Good : HudTheme.Warn);
        }

        private void SlotInput(Rect r, ItemStack s, bool left)
        {
            Event e = Event.current;
            if (!r.Contains(e.mousePosition)) return;
            _hover = s.item.itemName + "  ×" + s.count + "  (" + s.MassKg.ToString("0.#") + " kg)";
            if (e.type != EventType.MouseDown) return;
            if (e.button == 1 || (e.button == 0 && e.shift))
            {
                Transfer(s, left);
            }
            else if (e.button == 0)
            {
                _selStack = s;
                _selLeft = left;
                _selRecipe = null;
                _press = s;
                _pressLeft = left;
                _pressPos = _mouse;
                _dragging = false;
            }
            e.Use();
        }

        private void DrawRecipeGrid(Rect area)
        {
            List<CraftingRecipe> list = Recipes;
            var visible = new List<CraftingRecipe>();
            for (int i = 0; i < list.Count; i++)
                if (Matches(list[i].Output, _searchR) || (!string.IsNullOrEmpty(_searchR) && list[i].DisplayName.ToLowerInvariant().Contains(_searchR.ToLowerInvariant())))
                    visible.Add(list[i]);
            int rows = Mathf.CeilToInt(visible.Count / (float)Cols);
            var content = new Rect(0, 0, area.width - 18, Mathf.Max(area.height, rows * (Slot + Gap)));
            _scrollR = GUI.BeginScrollView(area, _scrollR, content);
            ItemContainer src = CraftSource;
            for (int i = 0; i < visible.Count; i++)
            {
                CraftingRecipe rec = visible[i];
                var r = new Rect((i % Cols) * (Slot + Gap), (i / Cols) * (Slot + Gap), Slot, Slot);
                int can = GameModeState.IsCreative ? 99 : rec.MaxCraftable(src);
                HudTheme.Fill(r, HudTheme.Slot);
                if (rec == _selRecipe) HudTheme.Frame(r, new Color(0, 0, 0, 0), HudTheme.Accent, 2f);
                ItemData o = rec.Output;
                GUI.color = can > 0 ? Color.white : new Color(1f, 1f, 1f, 0.3f);
                if (o != null) GUI.DrawTexture(new Rect(r.x + 8, r.y + 6, r.width - 16, r.height - 16), ItemIconPainter.For(o));
                GUI.color = Color.white;
                if (rec.OutputCount > 1) GUI.Label(new Rect(r.x + 2, r.yMax - 22, r.width - 6, 20), "×" + rec.OutputCount, _count);
                if (can > 0) GUI.Label(new Rect(r.x + 4, r.y + 2, r.width - 8, 18), "<color=#7CFC8A>" + Mathf.Min(can, 99) + "</color>", _small);

                Event e = Event.current;
                if (!r.Contains(e.mousePosition)) continue;
                _hover = rec.DisplayName + (can > 0 ? "" : Tr.T("  (faltan materiales)"));
                if (e.type == EventType.MouseDown)
                {
                    _selRecipe = rec;
                    _selStack = null;
                    if (e.button == 1 || e.clickCount >= 2) Craft(rec, 1);
                    e.Use();
                }
            }
            GUI.EndScrollView();
        }

        // ------------------------------------------------------------------
        // Acciones
        // ------------------------------------------------------------------

        private void Craft(CraftingRecipe rec, int count)
        {
            CraftingQueue q = Queue;
            if (q == null) return;
            int n = q.Enqueue(rec, count, CraftSource);
            SetStatus(n > 0 ? Tr.T("Encargado: ") + rec.DisplayName + " ×" + n + "." : q.Status);
        }

        private void Transfer(ItemStack s, bool fromLeft)
        {
            ItemContainer from = fromLeft ? _astro.Inventory : _other;
            ItemContainer to = fromLeft ? _other : _astro.Inventory;
            if (from == null || to == null)
            {
                SetStatus(fromLeft ? Tr.T("Abre el inventario de una máquina o almacén para transferir.") : "");
                return;
            }
            int moved = from.MoveTo(s, to, s.count);
            SetStatus(moved > 0 ? Tr.T("Transferido: ") + s.item.itemName + " ×" + moved + "." : Tr.T("No cabe (sin casillas o sobrepeso)."));
        }

        private void MoveAll(ItemContainer from, ItemContainer to)
        {
            int total = 0;
            for (int i = from.Slots.Count - 1; i >= 0; i--)
            {
                if (i >= from.Slots.Count) continue;
                ItemStack s = from.Slots[i];
                total += from.MoveTo(s, to, s.count);
            }
            SetStatus(total > 0 ? Tr.T("Transferidas ") + total + Tr.T(" unidades.") : Tr.T("No se pudo transferir nada."));
        }

        private void Use(ItemStack s)
        {
            if (CrewControl.Instance != null && !CrewControl.Instance.RobotDocked)
            {
                SetStatus(Tr.T("El robot debe estar en el hangar para usar esto con él."));
                return;
            }
            string msg;
            if (ItemEffects.TryUse(s.item, out msg))
            {
                ItemContainer c = _selLeft ? _astro.Inventory : _other;
                if (c != null) c.RemoveFromSlot(s, 1);
            }
            SetStatus(msg);
        }

        private void SetStatus(string text)
        {
            _status = text;
            _statusTime = Time.unscaledTime;
        }

        // ------------------------------------------------------------------
        // Utilidades
        // ------------------------------------------------------------------

        private bool StillThere(ItemStack s)
        {
            ItemContainer c = _selLeft ? _astro.Inventory : _other;
            if (c == null) return false;
            for (int i = 0; i < c.Slots.Count; i++) if (c.Slots[i] == s) return true;
            return false;
        }

        private static bool Matches(ItemData item, string filter)
        {
            if (string.IsNullOrEmpty(filter)) return true;
            return item != null && item.itemName.ToLowerInvariant().Contains(filter.ToLowerInvariant());
        }

        private string SearchField(Rect r, string value, string id)
        {
            GUI.SetNextControlName("buscar_" + id);
            string v = GUI.TextField(r, value);
            if (string.IsNullOrEmpty(v) && GUI.GetNameOfFocusedControl() != "buscar_" + id)
                GUI.Label(new Rect(r.x + 8, r.y, r.width, r.height), Tr.T("Buscar…"), HudTheme.Small);
            return v;
        }

        public static string CategoryLabel(ItemCategory c)
        {
            switch (c)
            {
                case ItemCategory.Mineral: return "Mineral";
                case ItemCategory.Volatile: return Tr.T("Volátil / fluido");
                case ItemCategory.RobotComponent: return Tr.T("Componente del robot");
                case ItemCategory.Tool: return Tr.T("Herramienta");
                case ItemCategory.Garden: return Tr.T("Huerto");
                case ItemCategory.Material: return "Material";
                default: return Tr.T("Consumible");
            }
        }

        private GUIStyle _countBig, _wrapMid;

        private void EnsureStyles()
        {
            if (_count != null) return;
            _count = new GUIStyle(HudTheme.Small) { alignment = TextAnchor.LowerRight, fontStyle = FontStyle.Bold, richText = true };
            _countBig = new GUIStyle(HudTheme.Body) { alignment = TextAnchor.MiddleRight, fontStyle = FontStyle.Bold, fontSize = 17, richText = true };
            _small = new GUIStyle(HudTheme.Small) { richText = true, wordWrap = false };
            _center = new GUIStyle(HudTheme.Body) { alignment = TextAnchor.MiddleCenter, fontSize = 17, richText = true };
            _center.normal.textColor = new Color(1f, 0.9f, 0.6f);
            _header = new GUIStyle(HudTheme.Title) { alignment = TextAnchor.MiddleCenter, fontSize = 28 };
            _header.normal.textColor = HudTheme.Accent;
            _bigName = new GUIStyle(HudTheme.Title) { fontSize = 22, wordWrap = true };
            _wrap = new GUIStyle(HudTheme.Body) { wordWrap = true, richText = true };
            _wrapMid = new GUIStyle(HudTheme.Body) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
        }
    }
}
