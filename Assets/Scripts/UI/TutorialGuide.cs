using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Items;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.UI
{
    /// <summary>
    /// Tutorial guiado al empezar: Marti propone una misión cada vez y la siguiente llega sola
    /// en cuanto la cumples (no hay que leer el manual).
    ///  1 pasar al robot (P) · 2 moverse · 3 salir del cohete · 4 mirar el mapa (M) ·
    ///  5 sacar piedras (F) · 6 dejarlas en la bodega · 7 construir algo (B) · 8 ¡listo!
    /// Tarjeta a la izquierda (bajo el minimapa) con "Misión X de 8", el texto y una barra.
    /// Marti dice cada misión en voz alta. F1 oculta o muestra la tarjeta; al terminarlo no
    /// vuelve a salir (se guarda en PlayerPrefs).
    /// </summary>
    public sealed class TutorialGuide : MonoBehaviour
    {
        private const string DoneKey = "tutorial_terminado";
        private static readonly string[] Steps =
        {
            "tut_switch", "tut_move", "tut_exit", "tut_map", "tut_mine", "tut_unload", "tut_build", "tut_done"
        };

        private int _step = -1;
        private bool _hidden;
        private float _stepStart;
        private Vector3 _startPos;
        private float _startBayKg;
        private int _startBase;
        private int _startStructures;
        private bool _sawMap;
        private float _doneAt = -1f;
        private GUIStyle _title, _text, _hint;

        /// <summary>Empieza el tutorial (si el jugador no lo terminó ya antes).</summary>
        public static void StartIfNeeded()
        {
            if (PlayerPrefs.GetInt(DoneKey, 0) == 1) return;
            if (FindAnyObjectByType<TutorialGuide>() != null) return;
            new GameObject("Tutorial (Marti)").AddComponent<TutorialGuide>();
        }

        /// <summary>Vuelve a enseñar el tutorial la próxima partida (botón en Ajustes, por ejemplo).</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(DoneKey);
        }

        private void Update()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F1)) _hidden = !_hidden;
#endif
            // Espera a que termine la bienvenida de Marti.
            if (MarsRobot.Dialogue.DialogueManager.AnyShowing) return;
            if (_step < 0)
            {
                Begin(IsAstronaut() ? 0 : 1);
                return;
            }
            if (MarsRobot.UI.Minimap.MarsMapScreen.IsOpen) _sawMap = true;
            if (Time.unscaledTime - _stepStart < 1.5f) return; // un respiro entre misiones

            if (_step == Steps.Length - 1)
            {
                if (_doneAt < 0f) _doneAt = Time.unscaledTime;
                if (Time.unscaledTime - _doneAt > 12f)
                {
                    PlayerPrefs.SetInt(DoneKey, 1);
                    PlayerPrefs.Save();
                    Destroy(gameObject);
                }
                return;
            }
            if (Completed(_step)) Begin(_step + 1);
        }

        private void Begin(int step)
        {
            _step = step;
            _stepStart = Time.unscaledTime;
            _hidden = false;
            Transform robot = Robot();
            _startPos = robot != null ? robot.position : Vector3.zero;
            _startBayKg = BayKg();
            _startBase = BaseTotal();
            _startStructures = StructureCount();
            _sawMap = false;
            MascotGuide.Say(Steps[step], 9f);
        }

        private bool Completed(int step)
        {
            Transform robot = Robot();
            switch (Steps[step])
            {
                case "tut_switch": return !IsAstronaut();
                case "tut_move": return robot != null && !IsAstronaut() && Flat(robot.position - _startPos) > 4f;
                case "tut_exit": return robot != null && DistanceToRocket(robot.position) > 22f;
                case "tut_map": return _sawMap && !MarsRobot.UI.Minimap.MarsMapScreen.IsOpen;
                case "tut_mine": return BayKg() > _startBayKg + 0.01f;
                case "tut_unload": return BaseTotal() > _startBase;
                case "tut_build": return StructureCount() > _startStructures;
                default: return false;
            }
        }

        // ------------------------------------------------------------------ consultas

        private static bool IsAstronaut()
        {
            var crew = MarsRobot.Crew.CrewControl.Instance;
            return crew != null && crew.AstronautActive;
        }

        private static Transform Robot()
        {
            RobotController rc;
            return GameServices.TryGet(out rc) ? rc.transform : null;
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        private static float DistanceToRocket(Vector3 p)
        {
            RocketBase b;
            return GameServices.TryGet(out b) ? Flat(p - b.transform.position) : 0f;
        }

        private static float BayKg()
        {
            Transform r = Robot();
            RobotInventory inv = r != null ? r.GetComponent<RobotInventory>() : null;
            return inv != null && inv.Bay != null ? inv.Bay.TotalMassKg : 0f;
        }

        private static int BaseTotal()
        {
            RocketBase b;
            if (!GameServices.TryGet(out b) || b.Inventory == null) return 0;
            int n = 0;
            foreach (ResourceType t in System.Enum.GetValues(typeof(ResourceType))) n += b.Inventory.Get(t);
            return n;
        }

        private float _nextCount;
        private int _cachedCount;

        private int StructureCount()
        {
            // Equipos (red de la base) + piezas de casa; se cuenta como mucho dos veces por segundo.
            if (Time.unscaledTime < _nextCount) return _cachedCount;
            _nextCount = Time.unscaledTime + 0.5f;
            RocketBase b;
            int n = GameServices.TryGet(out b) && b.Grid != null ? b.Grid.Structures.Count : 0;
            n += FindObjectsByType<PlacedPiece>(FindObjectsInactive.Exclude).Length;
            _cachedCount = n;
            return n;
        }

        // ------------------------------------------------------------------ tarjeta

        private void OnGUI()
        {
            if (_step < 0 || MarsRobot.Dialogue.DialogueManager.AnyShowing || MarsRobot.UI.Minimap.MarsMapScreen.IsOpen) return;
            if (_title == null)
            {
                _title = new GUIStyle(GUI.skin.label) { fontSize = 17, fontStyle = FontStyle.Bold };
                _title.normal.textColor = new Color(1f, 0.75f, 0.35f);
                _text = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true };
                _text.normal.textColor = Color.white;
                _hint = new GUIStyle(GUI.skin.label) { fontSize = 12 };
                _hint.normal.textColor = new Color(0.75f, 0.8f, 0.85f);
            }
            System.Func<string, string, string> L = MarsRobot.Localization.Localizer.L;
            if (_hidden)
            {
                GUI.Label(new Rect(14f, 304f, 300f, 20f), L("F1: ver la misión", "F1: show the mission"), _hint);
                return;
            }

            bool last = _step == Steps.Length - 1;
            string text = MarsRobot.Localization.Localizer.T(Steps[_step]);
            const float w = 330f;
            float textH = _text.CalcHeight(new GUIContent(text), w - 82f);
            var r = new Rect(12f, 300f, w, Mathf.Max(96f, textH + 70f));
            HudTheme.Frame(r, new Color(0.05f, 0.08f, 0.13f, 0.9f), last ? HudTheme.Good : new Color(1f, 0.6f, 0.2f), 2f);
            MascotGuide.DrawPortrait(new Rect(r.x + 6f, r.y + 8f, 60f, 60f), Time.unscaledTime - _stepStart < 2f);
            GUI.Label(new Rect(r.x + 72f, r.y + 6f, w - 80f, 22f),
                last ? L("¡Tutorial completado!", "Tutorial complete!") : L("Misión ", "Mission ") + (_step + 1) + L(" de ", " of ") + (Steps.Length - 1), _title);
            GUI.Label(new Rect(r.x + 72f, r.y + 30f, w - 82f, textH), text, _text);
            HudTheme.Bar(new Rect(r.x + 72f, r.yMax - 30f, w - 82f, 8f), (float)_step / (Steps.Length - 1), last ? HudTheme.Good : new Color(1f, 0.6f, 0.2f), "");
            GUI.Label(new Rect(r.x + 72f, r.yMax - 20f, w - 82f, 18f), L("F1: ocultar", "F1: hide"), _hint);
        }
    }
}
