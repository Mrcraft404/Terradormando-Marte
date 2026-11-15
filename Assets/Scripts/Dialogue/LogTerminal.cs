using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Dialogue
{
    /// <summary>Tipo de entrada de la bitácora.</summary>
    public enum LogCategory
    {
        Message,    // diálogos y mensajes
        Discovery,  // registros encontrados explorando
        Mission,    // misiones activas
        Warning,
        Alert
    }

    /// <summary>
    /// Bitácora del robot (tecla J, o pestaña en la terminal del cohete): guarda los diálogos,
    /// los registros descubiertos explorando los puntos de aterrizaje, las misiones y las alertas.
    /// </summary>
    public sealed class LogTerminal : MonoBehaviour
    {
        public sealed class Entry
        {
            public int sol;
            public string time;
            public string sender;
            public string text;
            public LogCategory category;
        }

        private static readonly List<Entry> Entries = new List<Entry>();
        private const int MaxEntries = 300;
        private const int WindowId = 41732;

        private Rect _window = new Rect(0, 0, 720, 480);
        private Vector2 _scroll;
        private int _filter = -1; // -1 = todas
        private RobotController _controller;

        public static IReadOnlyList<Entry> All
        {
            get { return Entries; }
        }

        public bool IsOpen { get; private set; }

        /// <summary>Añade una entrada (con fecha en soles marcianos).</summary>
        public static void Add(string sender, string text, LogCategory category)
        {
            float gameSeconds = Time.timeSinceLevelLoad * MarsTime.TimeScale;
            int sol = 1 + Mathf.FloorToInt(gameSeconds / MarsTime.SolSeconds);
            float solFrac = (gameSeconds % MarsTime.SolSeconds) / MarsTime.SolSeconds * 24.66f;
            int h = Mathf.FloorToInt(solFrac);
            int m = Mathf.FloorToInt((solFrac - h) * 60f);
            Entries.Add(new Entry { sol = sol, time = h.ToString("00") + ":" + m.ToString("00"), sender = sender, text = text, category = category });
            if (Entries.Count > MaxEntries) Entries.RemoveAt(0);
        }

        /// <summary>Dibuja las últimas entradas dentro de otro menú (p. ej. la terminal del cohete).</summary>
        public static void DrawEntries(int max)
        {
            if (Entries.Count == 0)
            {
                GUILayout.Label("La bitácora está vacía.");
                return;
            }
            for (int i = Entries.Count - 1, shown = 0; i >= 0 && shown < max; i--, shown++)
            {
                GUILayout.Label(Format(Entries[i]));
            }
        }

        private static string Format(Entry e)
        {
            string color;
            switch (e.category)
            {
                case LogCategory.Alert: color = "#ff7070"; break;
                case LogCategory.Warning: color = "#ffc060"; break;
                case LogCategory.Discovery: color = "#ffe080"; break;
                case LogCategory.Mission: color = "#80ff90"; break;
                default: color = "#a0d8ff"; break;
            }
            return "<color=#888888>Sol " + e.sol + " " + e.time + "</color>  <color=" + color + "><b>" + e.sender + ":</b></color> " + e.text;
        }

        private void Awake()
        {
            _controller = GetComponent<RobotController>();
        }

        private void Update()
        {
            if (RobotInput.LogPressed) SetOpen(!IsOpen);
            else if (IsOpen && RobotInput.ReleaseCursorPressed) SetOpen(false);
        }

        private void SetOpen(bool open)
        {
            IsOpen = open;
            if (_controller != null) _controller.InputEnabled = !open;
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
            if (open)
            {
                _window.x = (Screen.width - _window.width) * 0.5f;
                _window.y = (Screen.height - _window.height) * 0.5f;
            }
        }

        private void OnDisable()
        {
            if (IsOpen) SetOpen(false);
        }

        private void OnGUI()
        {
            if (!IsOpen) return;
            GUI.skin.label.richText = true;
            _window = GUILayout.Window(WindowId, _window, DrawWindow, "Bitácora del rover (J para cerrar)");
        }

        private void DrawWindow(int id)
        {
            string[] tabs = { "Todo", "Mensajes", "Descubrimientos", "Misiones", "Alertas" };
            int sel = GUILayout.Toolbar(_filter + 1, tabs);
            _filter = sel - 1;

            _scroll = GUILayout.BeginScrollView(_scroll);
            for (int i = Entries.Count - 1; i >= 0; i--)
            {
                Entry e = Entries[i];
                if (!Matches(e)) continue;
                GUILayout.Label(Format(e));
            }
            if (Entries.Count == 0) GUILayout.Label("Aún no hay registros. Explora y habla con la IA del cohete.");
            GUILayout.EndScrollView();
            GUI.DragWindow(new Rect(0, 0, 10000, 20));
        }

        private bool Matches(Entry e)
        {
            switch (_filter)
            {
                case 0: return e.category == LogCategory.Message;
                case 1: return e.category == LogCategory.Discovery;
                case 2: return e.category == LogCategory.Mission;
                case 3: return e.category == LogCategory.Warning || e.category == LogCategory.Alert;
                default: return true;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            Entries.Clear();
        }
    }
}
