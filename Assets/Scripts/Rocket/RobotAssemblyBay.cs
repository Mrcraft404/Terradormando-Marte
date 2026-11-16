using System.Collections.Generic;
using MarsRobot.Robot;
using MarsRobot.UI;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Taller del robot (en el hangar del cohete). Pantalla completa pensada para niños:
    ///  • Izquierda: "Tu robot ahora" con las piezas puestas (dibujo + nombre; la ✕ la quita)
    ///    y barras fáciles de leer: velocidad, batería, resistencia y cuestas.
    ///  • Derecha: botones grandes "¿Para qué quieres el robot?" (Minar, Terreno, Energía,
    ///    Carga, Explorar) y tarjetas con el dibujo de cada pieza. Clic en una tarjeta = ponerla.
    ///  • Abajo: lo que hace la pieza bajo el ratón y lo que cuesta.
    ///  • Pestaña "Mejoras y reparación": el menú del taller anterior (niveles, reparación).
    /// Solo se cambian piezas con el robot aparcado bajo el arco del taller; cuesta materiales.
    /// </summary>
    public sealed class RobotAssemblyBay : UpgradeStation
    {
        private int _tab;
        private int _filter; // 0 = todas; 1.. = RoverFunction + 1
        private string _message = "";
        private float _messageTime = -10f;
        private Vector2 _scroll;
        private RoverModuleDefinition _hover;

        private static string[] FilterLabels
        {
            get
            {
                return Localization.Localizer.English
                    ? new[] { "All", "Mining", "Terrain", "Energy", "Cargo", "Explore" }
                    : new[] { Tr.T("Todo"), "Minar", "Terreno", Tr.T("Energía"), "Carga", "Explorar" };
            }
        }

        private static string L(string es, string en)
        {
            return Localization.Localizer.L(es, en);
        }
        private static GUIStyle _cardName, _big, _info, _closeStyle;

        public override bool DrawsFullScreen
        {
            get { return true; }
        }

        /// <summary>Menú clásico (por compatibilidad: lo usa la pestaña de mejoras).</summary>
        public override void DrawMenu(RobotBaseLink robot)
        {
            base.DrawMenu(robot);
        }

        public static Texture2D IconFor(RoverModuleType type)
        {
            return ItemIconArt.Get("rover_" + type);
        }

        private static bool _greeted;

        public override void DrawFullScreen(RobotBaseLink robot)
        {
            EnsureStyles();
            if (!_greeted && Event.current.type == EventType.Repaint)
            {
                // La primera vez, Marti saluda y explica el taller (con su voz).
                _greeted = true;
                Say(Localization.Localizer.T("marti_workshop"));
                MascotGuide.SpeakOnly("marti_workshop");
            }
            HudTheme.BeginScaled();
            HudTheme.Fill(new Rect(-400f, -200f, 2400f, 1300f), new Color(0f, 0f, 0f, 0.55f));
            var win = new Rect(70f, 40f, 1460f, 820f);
            HudTheme.Panel(win, L("TALLER DEL ROBOT", "ROBOT WORKSHOP"), L("Cambia las piezas de tu robot según lo que quieras hacer", "Change your robot's parts for what you want to do"));

            // Pestañas grandes y botón de cerrar.
            string[] tabs = { L("Piezas del robot", "Robot parts"), L("Mejoras y reparación", "Upgrades and repair") };
            for (int i = 0; i < tabs.Length; i++)
            {
                if (GUI.Button(new Rect(win.x + 760f + i * 250f, win.y + 18f, 240f, 44f), tabs[i], i == _tab ? HudTheme.TabActive : HudTheme.Tab)) _tab = i;
            }
            if (GUI.Button(new Rect(win.xMax - 150f, win.y + 18f, 130f, 44f), L("Cerrar ✕", "Close ✕"), _closeStyle))
            {
                HudTheme.EndScaled();
                robot.RequestClose();
                return;
            }

            var body = new Rect(win.x + 18f, win.y + 80f, win.width - 36f, win.height - 98f);
            if (_tab == 1)
            {
                // El menú clásico de mejoras, dentro de la pantalla nueva.
                HudTheme.Frame(body, new Color(0.03f, 0.08f, 0.14f, 0.9f), HudTheme.AccentDim, 1f);
                GUILayout.BeginArea(new Rect(body.x + 16f, body.y + 12f, body.width - 32f, body.height - 24f));
                base.DrawMenu(robot);
                GUILayout.EndArea();
                HudTheme.EndScaled();
                return;
            }

            var modules = robot.GetComponent<RobotModuleSystem>();
            if (modules == null)
            {
                GUI.Label(body, L("Este robot no admite piezas intercambiables.", "This robot can't change parts."), _big);
                HudTheme.EndScaled();
                return;
            }
            DrawRobotColumn(modules, new Rect(body.x, body.y, 430f, body.height));
            DrawCatalog(modules, new Rect(body.x + 446f, body.y, body.width - 446f, body.height));
            HudTheme.EndScaled();
        }

        // ------------------------------------------------------------------
        // Columna izquierda: el robot ahora
        // ------------------------------------------------------------------

        private void DrawRobotColumn(RobotModuleSystem modules, Rect r)
        {
            HudTheme.Frame(r, new Color(0.03f, 0.08f, 0.14f, 0.9f), HudTheme.AccentDim, 1f);
            bool docked = modules.IsDockedAtAssemblyBay();
            HudTheme.StatePill(new Rect(r.x + 14f, r.y + 12f, r.width - 28f, 34f),
                docked ? L("Robot en el taller: ¡puedes cambiar piezas!", "Robot in the workshop: you can change parts!") : L("Aparca el robot bajo el arco del taller", "Park the robot under the workshop arch"),
                docked ? HudTheme.Good : HudTheme.Warn);

            GUI.Label(new Rect(r.x + 14f, r.y + 54f, r.width - 28f, 30f), L("Tu robot ahora", "Your robot now"), _big);
            float y = r.y + 88f;
            y = DrawSlotRow(modules, RoverSlotType.Wheels, 1, r, y);
            y = DrawSlotRow(modules, RoverSlotType.FrontArm, modules.FrontSlotCount, r, y);
            y = DrawSlotRow(modules, RoverSlotType.TopDeck, modules.GetSlots(RoverSlotType.TopDeck).Length, r, y);
            y = DrawSlotRow(modules, RoverSlotType.InternalBay, modules.GetSlots(RoverSlotType.InternalBay).Length, r, y);

            // Barras fáciles: más llena = mejor.
            y += 8f;
            float speed = modules.SpeedMultiplier;
            float battery = 1f / Mathf.Max(0.1f, modules.PowerMultiplier);
            float tough = 1f / Mathf.Max(0.1f, modules.WearMultiplier);
            float climb = (35f + modules.ClimbBonusDegrees) / 50f;
            y = StatBar(r, y, L("Velocidad", "Speed"), speed / 1.5f, HudTheme.Accent);
            y = StatBar(r, y, L("Ahorro de batería", "Battery saving"), battery / 1.2f, HudTheme.Good);
            y = StatBar(r, y, L("Resistencia de las ruedas", "Wheel toughness"), tough / 2.4f, HudTheme.Warn);
            StatBar(r, y, L("Subir cuestas", "Hill climbing"), climb, new Color(0.85f, 0.45f, 0.95f));
        }

        private float DrawSlotRow(RobotModuleSystem modules, RoverSlotType type, int count, Rect col, float y)
        {
            RoverModuleType[] slots = modules.GetSlots(type);
            GUI.Label(new Rect(col.x + 14f, y, 160f, 22f), Cap(RobotModuleSystem.SlotLabel(type)), HudTheme.Small);
            y += 22f;
            float x = col.x + 14f;
            const float size = 64f;
            for (int i = 0; i < count && i < slots.Length; i++)
            {
                var cell = new Rect(x, y, size, size);
                RoverModuleDefinition d = RobotModuleSystem.Find(slots[i]);
                bool hover = cell.Contains(Event.current.mousePosition);
                HudTheme.Frame(cell, hover && d != null ? HudTheme.PanelLight : HudTheme.Slot, d != null ? HudTheme.Accent : HudTheme.AccentDim, 1f);
                if (d != null)
                {
                    Texture2D icon = IconFor(d.type);
                    if (icon != null) GUI.DrawTexture(new Rect(cell.x + 4f, cell.y + 4f, size - 8f, size - 8f), icon, ScaleMode.ScaleToFit);
                    if (hover) _hover = d;
                    // Las ruedas no se quitan (se cambian por otras); el resto sí.
                    if (type != RoverSlotType.Wheels)
                    {
                        var x2 = new Rect(cell.xMax - 20f, cell.y, 20f, 20f);
                        HudTheme.Fill(x2, HudTheme.Bad);
                        GUI.Label(x2, "✕", _cardName);
                        if (GUI.Button(cell, GUIContent.none, GUIStyle.none)) Say(modules.Unequip(d.type));
                    }
                }
                x += size + 8f;
            }
            return y + size + 8f;
        }

        private float StatBar(Rect col, float y, string label, float ratio, Color c)
        {
            GUI.Label(new Rect(col.x + 14f, y, col.width - 28f, 22f), label, HudTheme.Small);
            HudTheme.Bar(new Rect(col.x + 14f, y + 22f, col.width - 28f, 16f), Mathf.Clamp01(ratio), c, "");
            return y + 46f;
        }

        // ------------------------------------------------------------------
        // Derecha: catálogo en tarjetas
        // ------------------------------------------------------------------

        private void DrawCatalog(RobotModuleSystem modules, Rect r)
        {
            GUI.Label(new Rect(r.x, r.y, r.width, 32f), L("¿Para qué quieres el robot?", "What do you want the robot for?"), _big);
            float fx = r.x;
            for (int i = 0; i < FilterLabels.Length; i++)
            {
                if (GUI.Button(new Rect(fx, r.y + 36f, 148f, 42f), FilterLabels[i], i == _filter ? HudTheme.TabActive : HudTheme.Tab))
                {
                    _filter = i;
                    _scroll = Vector2.zero;
                }
                fx += 156f;
            }

            var grid = new Rect(r.x, r.y + 88f, r.width, r.height - 88f - 150f);
            HudTheme.Fill(grid, new Color(0.02f, 0.06f, 0.1f, 0.6f));
            var items = new List<RoverModuleDefinition>();
            foreach (RoverModuleDefinition d in RobotModuleSystem.Catalog)
            {
                if (_filter == 0 || (int)d.function == _filter - 1) items.Add(d);
            }

            const float cw = 150f, ch = 178f, gap = 10f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((grid.width - 20f + gap) / (cw + gap)));
            int rows = Mathf.CeilToInt(items.Count / (float)perRow);
            var view = new Rect(0f, 0f, grid.width - 20f, Mathf.Max(grid.height - 12f, rows * (ch + gap)));
            bool docked = modules.IsDockedAtAssemblyBay();
            BaseInventory inv = Owner != null ? Owner.Inventory : null;

            _scroll = GUI.BeginScrollView(new Rect(grid.x + 6f, grid.y + 6f, grid.width - 12f, grid.height - 12f), _scroll, view);
            for (int i = 0; i < items.Count; i++)
            {
                RoverModuleDefinition d = items[i];
                var c = new Rect((i % perRow) * (cw + gap), (i / perRow) * (ch + gap), cw, ch);
                bool installed = modules.HasModule(d.type);
                ResourceAmount[] price = modules.InstallCost(d.type);
                bool canPay = price.Length == 0 || (inv != null && inv.Has(price));
                bool hover = c.Contains(Event.current.mousePosition);
                if (hover) _hover = d;

                HudTheme.Frame(c, installed ? new Color(0.08f, 0.25f, 0.14f, 0.95f) : (hover ? HudTheme.PanelLight : HudTheme.Slot),
                    installed ? HudTheme.Good : (hover ? HudTheme.Accent : HudTheme.AccentDim), hover ? 2f : 1f);
                Texture2D icon = IconFor(d.type);
                Color prev = GUI.color;
                if (!installed && !canPay) GUI.color = new Color(1f, 1f, 1f, 0.45f);
                if (icon != null) GUI.DrawTexture(new Rect(c.x + 15f, c.y + 8f, cw - 30f, cw - 30f), icon, ScaleMode.ScaleToFit);
                GUI.color = prev;
                GUI.Label(new Rect(c.x + 6f, c.y + cw - 22f, cw - 12f, 48f), d.DisplayName, _cardName);

                if (installed) HudTheme.StatePill(new Rect(c.x + 8f, c.y + 8f, 78f, 22f), L("PUESTA", "ON"), HudTheme.Good);
                else if (!canPay) HudTheme.StatePill(new Rect(c.x + 8f, c.y + 8f, 104f, 22f), L("Sin material", "Need more"), HudTheme.Bad);
                else if (modules.Owns(d.type)) HudTheme.StatePill(new Rect(c.x + 8f, c.y + 8f, 92f, 22f), L("La tienes", "You have it"), HudTheme.Accent);

                if (!installed && GUI.Button(c, GUIContent.none, GUIStyle.none))
                {
                    Say(docked ? modules.TryEquip(d.type, inv) : L("Primero aparca el robot bajo el arco del taller.", "First park the robot under the workshop arch."));
                }
            }
            GUI.EndScrollView();

            // Información de la pieza bajo el ratón.
            var info = new Rect(r.x, grid.yMax + 10f, r.width, 140f);
            HudTheme.Frame(info, new Color(0.03f, 0.08f, 0.14f, 0.9f), HudTheme.AccentDim, 1f);
            if (Time.unscaledTime - _messageTime < 7f)
            {
                GUI.Label(new Rect(info.x + 16f, info.y + 10f, info.width - 32f, 34f), _message, _big);
            }
            if (_hover != null)
            {
                float top = Time.unscaledTime - _messageTime < 7f ? 46f : 10f;
                Texture2D icon = IconFor(_hover.type);
                if (icon != null && top < 20f) GUI.DrawTexture(new Rect(info.x + 12f, info.y + 12f, 100f, 100f), icon, ScaleMode.ScaleToFit);
                float tx = top < 20f ? 124f : 16f;
                if (top < 20f) GUI.Label(new Rect(info.x + tx, info.y + 8f, info.width - tx - 16f, 32f), _hover.DisplayName + L("   ·   va en: ", "   ·   goes in: ") + RobotModuleSystem.SlotLabel(_hover.slot), _big);
                GUI.Label(new Rect(info.x + tx, info.y + top + 32f, info.width - tx - 16f, 50f), _hover.DisplayDescription, _info);
                ResourceAmount[] price = modules.InstallCost(_hover.type);
                bool ok = price.Length == 0 || (inv != null && inv.Has(price));
                string cost = modules.HasModule(_hover.type) ? L("Ya la tiene puesta", "Already on")
                    : price.Length == 0 ? L("Gratis", "Free")
                    : (modules.Owns(_hover.type) ? L("Montarla cuesta: ", "Putting it on costs: ") : L("Fabricarla cuesta: ", "Making it costs: "))
                      + BaseInventory.Describe(price) + (ok ? "" : L("  (te faltan materiales)", "  (you need more materials)"));
                Color prevC = GUI.contentColor;
                GUI.contentColor = ok ? HudTheme.Good : HudTheme.Bad;
                GUI.Label(new Rect(info.x + tx, info.yMax - 34f, info.width - tx - 16f, 28f), cost, _info);
                GUI.contentColor = prevC;
            }
            else if (Time.unscaledTime - _messageTime >= 7f)
            {
                GUI.Label(new Rect(info.x + 16f, info.y + 14f, info.width - 32f, 110f),
                    L("Elige arriba para qué quieres tu robot y haz clic en una pieza para ponérsela.\nCada pieza ocupa un hueco: ¡no caben todas a la vez!",
                      "Choose above what you want your robot for, and click a part to put it on.\nEach part takes one slot: they don't all fit at once!"), _info);
            }
        }

        private void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            _message = text;
            _messageTime = Time.unscaledTime;
        }

        private static string Cap(string s)
        {
            return string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);
        }

        private static void EnsureStyles()
        {
            if (_cardName != null) return;
            _cardName = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperCenter, wordWrap = true };
            _cardName.normal.textColor = Color.white;
            _big = new GUIStyle(GUI.skin.label) { fontSize = 24, fontStyle = FontStyle.Bold, wordWrap = true };
            _big.normal.textColor = new Color(1f, 0.8f, 0.4f);
            _info = new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true, richText = true };
            _info.normal.textColor = new Color(0.9f, 0.95f, 1f);
            _closeStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold };
        }
    }
}
