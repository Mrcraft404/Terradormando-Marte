using System;
using System.Collections.Generic;
using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Dialogue
{
    /// <summary>
    /// Gestor global de diálogos (singleton que se crea solo al primer uso).
    ///  • Muestra un nodo cada vez, con efecto de máquina de escribir y tecleo de terminal.
    ///  • Si el nodo lo pide, pausa el juego (Time.timeScale = 0) y bloquea el rover.
    ///  • Respuestas con los números 1–4 o con clic; Intro/Espacio avanza o completa el texto.
    ///  • Al terminar un nodo ejecuta sus eventos (misión, reparar, desbloquear, recursos…).
    ///  • Si llega un diálogo mientras otro está abierto, se pone en cola.
    /// </summary>
    public sealed class DialogueManager : MonoBehaviour
    {
        private static DialogueManager _instance;

        [SerializeField, Min(5f)] private float charsPerSecond = 45f;
        [SerializeField, Min(1)] private int blipEveryChars = 3;

        private readonly Queue<DialogueNode> _queue = new Queue<DialogueNode>();
        private DialogueNode _current;
        private float _shownChars;
        private bool _paused;
        private float _prevTimeScale = 1f;
        private int _blipCounter;
        private bool _hasVoice;
        private float _cps = 45f;
        private AudioSource _audio, _staticSource;
        private AudioClip _blip, _staticClip;
        private GUIStyle _textStyle, _speakerStyle;

        public static DialogueManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("Gestor de diálogos");
                    _instance = go.AddComponent<DialogueManager>();
                }
                return _instance;
            }
        }

        public bool IsShowing
        {
            get { return _current != null; }
        }

        /// <summary>Se lanza al cerrar el último diálogo de una secuencia.</summary>
        public event Action DialogueClosed;

        /// <summary>Muestra un diálogo (o lo encola si ya hay otro).</summary>
        public static void Play(DialogueNode node)
        {
            if (node == null) return;
            DialogueManager m = Instance;
            if (m._current != null) m._queue.Enqueue(node);
            else m.Show(node);
        }

        /// <summary>Muestra un diálogo por su nodeID (Resources/Dialogue o los de por defecto).</summary>
        public static void Play(string nodeId)
        {
            Play(DialogueLibrary.Get(nodeId));
        }

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.ignoreListenerPause = true;
            _staticSource = gameObject.AddComponent<AudioSource>();
            _staticSource.loop = true;
            _staticSource.volume = 0.08f;
            _staticSource.ignoreListenerPause = true;
            _blip = NotificationSystem.SynthTone(1600f, 0.012f, 1);
            _staticClip = MakeStatic();
        }

        private void OnDestroy()
        {
            if (_paused) Time.timeScale = _prevTimeScale;
            if (_instance == this) _instance = null;
        }

        private void Show(DialogueNode node)
        {
            _current = node;
            _shownChars = 0f;
            if (node.pauseGame && !_paused)
            {
                _paused = true;
                _prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
            }
            SetRobotControl(false);

            // Voz: la del nodo, o la voz de Marti para su frase (si está generada).
            _audio.Stop();
            AudioClip voice = node.voiceClip;
            if (voice == null && node.IsMascot) voice = MarsRobot.UI.MascotGuide.VoiceFor(node.text);
            _hasVoice = voice != null;
            if (_hasVoice) _audio.PlayOneShot(voice);
            int len = (node.LocalizedText ?? "").Length;
            _cps = _hasVoice && voice.length > 0.5f ? Mathf.Clamp(len / (voice.length * 0.9f), 10f, charsPerSecond) : charsPerSecond;
            if (node.roboticStatic)
            {
                _staticSource.clip = _staticClip;
                _staticSource.Play();
            }
            else
            {
                _staticSource.Stop();
            }
            if (node.addToLog) LogTerminal.Add(node.SpeakerName, node.LocalizedText, LogCategory.Message);
        }

        private void Update()
        {
            if (_current == null) return;

            // Efecto máquina de escribir (tiempo real: funciona aunque el juego esté en pausa).
            // Se cuenta el texto TRADUCIDO (no la clave): si no, se cortaba a la longitud de la clave.
            string full = _current.LocalizedText ?? "";
            int total = full.Length;
            if (_shownChars < total)
            {
                int before = (int)_shownChars;
                _shownChars = Mathf.Min(total, _shownChars + _cps * Time.unscaledDeltaTime);
                int after = (int)_shownChars;
                for (int i = before; i < after; i++)
                {
                    if (!_hasVoice && !char.IsWhiteSpace(full[i]) && ++_blipCounter % blipEveryChars == 0)
                    {
                        _audio.PlayOneShot(_blip, 0.25f);
                    }
                }
            }

            bool typing = _shownChars < total;
            if (RobotInput.DialogueAdvanceAny)
            {
                if (typing) _shownChars = total;
                else if (_current.choices.Count == 0) Advance(_current.next);
            }
            if (!typing && _current.choices.Count > 0)
            {
                for (int i = 0; i < _current.choices.Count && i < 9; i++)
                {
                    if (RobotInput.ChoicePressed(i)) Advance(_current.choices[i].next);
                }
            }
        }

        /// <summary>Prueba automática: pasa el diálogo actual (elige la primera respuesta).</summary>
        public static void DebugSkip()
        {
            DialogueManager m = _instance;
            if (m == null || m._current == null) return;
            if (m._current.choices.Count > 0) m.Advance(m._current.choices[0].next);
            else m.Advance(m._current.next);
        }

        private void Advance(DialogueNode next)
        {
            DialogueNode finished = _current;
            _current = null;
            _audio.Stop();
            DialogueEffects.Run(finished);

            if (next != null)
            {
                Show(next);
                return;
            }
            if (_queue.Count > 0)
            {
                Show(_queue.Dequeue());
                return;
            }

            _staticSource.Stop();
            if (_paused)
            {
                _paused = false;
                Time.timeScale = _prevTimeScale;
            }
            SetRobotControl(true);
            if (DialogueClosed != null) DialogueClosed();
        }

        private static void SetRobotControl(bool enabled)
        {
            RobotController rc;
            if (!GameServices.TryGet(out rc)) return;
            // Con el astronauta al mando, el robot sigue aparcado al cerrar el diálogo.
            var crew = MarsRobot.Crew.CrewControl.Instance;
            if (enabled && crew != null && crew.AstronautActive)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                return;
            }
            rc.InputEnabled = enabled;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        /// <summary>¿Hay un diálogo en pantalla? (los consejos de Marti esperan a que termine).</summary>
        public static bool AnyShowing
        {
            get { return _instance != null && _instance._current != null; }
        }

        private GUIStyle _choiceStyle, _hintStyle;

        private void OnGUI()
        {
            if (_current == null) return;
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, wordWrap = true, richText = true };
                _textStyle.normal.textColor = new Color(0.12f, 0.1f, 0.14f);
                _speakerStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold };
                _speakerStyle.normal.textColor = new Color(0.85f, 0.32f, 0.12f);
                _choiceStyle = new GUIStyle(GUI.skin.button) { fontSize = 22, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft, wordWrap = true };
                _choiceStyle.padding = new RectOffset(18, 12, 6, 6);
                _hintStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleRight };
                _hintStyle.normal.textColor = new Color(0.45f, 0.4f, 0.45f);
            }

            // Lienzo virtual 1600×900: se ve igual en cualquier pantalla.
            MarsRobot.UI.HudTheme.BeginScaled();
            bool mascot = _current.IsMascot;
            string text = _current.LocalizedText ?? "";
            int n = Mathf.Clamp((int)_shownChars, 0, text.Length);
            bool typing = n < text.Length;
            int choices = !typing ? _current.choices.Count : 0;

            // Altura del texto completo (no del trozo ya escrito): la caja no cambia de tamaño
            // mientras escribe y el final nunca queda cortado.
            const float textW = 1200f - 56f;
            float textH = Mathf.Max(64f, _textStyle.CalcHeight(new GUIContent(text), textW) + 8f);
            float h = 54f + textH + 56f + choices * 58f;
            if (h > 640f) h = 640f;
            var box = new Rect(240f, 900f - h - 24f, 1200f, h);

            // Fondo oscuro suave detrás para que el texto se lea sobre cualquier escena.
            MarsRobot.UI.HudTheme.Fill(new Rect(0f, box.y - 40f, 1600f, 900f - box.y + 40f), new Color(0f, 0f, 0f, 0.35f));
            // Bocadillo claro, como en un cuento.
            MarsRobot.UI.HudTheme.Frame(box, new Color(1f, 0.97f, 0.92f, 0.98f),
                mascot ? new Color(0.85f, 0.32f, 0.12f) : new Color(0.25f, 0.55f, 0.85f), 4f);

            // Retrato: Marti (con saltitos al hablar) o una placa con el nombre.
            var face = new Rect(box.x - 220f, box.yMax - 230f, 220f, 220f);
            if (mascot) MarsRobot.UI.MascotGuide.DrawPortrait(face, typing);
            else
            {
                var plate = new Rect(face.x + 30f, face.y + 50f, 160f, 160f);
                MarsRobot.UI.HudTheme.Frame(plate, new Color(0.12f, 0.2f, 0.3f, 0.95f), new Color(0.25f, 0.55f, 0.85f), 4f);
                string tag = _current.speaker == DialogueSpeaker.ProbeArchive ? Tr.T("SONDA") : Tr.T("TIERRA");
                var plateStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 30, fontStyle = FontStyle.Bold };
                plateStyle.normal.textColor = new Color(0.75f, 0.9f, 1f);
                GUI.Label(plate, tag, plateStyle);
            }

            GUI.Label(new Rect(box.x + 28f, box.y + 14f, box.width - 56f, 36f), _current.SpeakerName, _speakerStyle);
            GUI.Label(new Rect(box.x + 28f, box.y + 54f, textW, textH), text.Substring(0, n), _textStyle);

            if (choices > 0)
            {
                float y = box.y + 54f + textH + 6f;
                for (int i = 0; i < choices; i++)
                {
                    if (GUI.Button(new Rect(box.x + 28f, y, box.width - 56f, 50f), (i + 1) + "   " + _current.choices[i].LocalizedText, _choiceStyle))
                    {
                        MarsRobot.UI.HudTheme.EndScaled();
                        Advance(_current.choices[i].next);
                        return;
                    }
                    y += 58f;
                }
            }
            else
            {
                // Flechita que parpadea: "pulsa para seguir".
                bool blink = Mathf.Repeat(Time.unscaledTime, 0.8f) < 0.5f;
                string hint = typing ? MarsRobot.Localization.Localizer.L("Intro: ver todo", "Enter: show all")
                    : (blink ? "▼  " : "    ") + MarsRobot.Localization.Localizer.L("Intro o clic para seguir", "Enter or click to continue");
                GUI.Label(new Rect(box.x + 28f, box.yMax - 44f, box.width - 56f, 30f), hint, _hintStyle);
                if (!typing && Event.current.type == EventType.MouseDown && box.Contains(Event.current.mousePosition))
                {
                    MarsRobot.UI.HudTheme.EndScaled();
                    Advance(_current.next);
                    return;
                }
            }
            MarsRobot.UI.HudTheme.EndScaled();
        }

        /// <summary>Estática de radio: ruido filtrado a bajo volumen.</summary>
        private static AudioClip MakeStatic()
        {
            const int rate = 22050;
            var data = new float[rate];
            var rng = new System.Random(3);
            float last = 0f;
            for (int i = 0; i < data.Length; i++)
            {
                float white = (float)(rng.NextDouble() * 2.0 - 1.0);
                last = last * 0.7f + white * 0.3f;
                data[i] = last * 0.5f;
            }
            AudioClip clip = AudioClip.Create("Estática", data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _instance = null;
        }
    }

    /// <summary>Ejecuta los eventos de un nodo al terminarlo.</summary>
    public static class DialogueEffects
    {
        public static void Run(DialogueNode node)
        {
            if (node == null || node.onFinish == null) return;
            for (int i = 0; i < node.onFinish.Count; i++) Run(node.onFinish[i]);
        }

        public static void Run(DialogueEvent e)
        {
            switch (e.type)
            {
                case DialogueEventType.StartMission:
                    LogTerminal.Add(Tr.T("Misión"), e.stringParam, LogCategory.Mission);
                    NotificationSystem.Notify(Tr.T("Nueva misión: ") + e.stringParam, NotificationLevel.Info, 7f);
                    break;

                case DialogueEventType.RepairModule:
                {
                    RobotHealthSystem health;
                    if (!GameServices.TryGet(out health)) break;
                    if (e.stringParam == "all") health.RepairAll();
                    else
                    {
                        RobotModuleId id;
                        if (Enum.TryParse(e.stringParam, out id)) health.RepairModule(id);
                    }
                    NotificationSystem.Notify(Tr.T("Reparación remota completada."), NotificationLevel.Info);
                    break;
                }

                case DialogueEventType.UnlockTechnology:
                {
                    RocketBase b;
                    if (!GameServices.TryGet(out b)) break;
                    b.Research.ForceUnlock(e.stringParam);
                    StructureDefinition def = StructureCatalog.Find(e.stringParam);
                    NotificationSystem.Notify(Tr.T("Tecnología desbloqueada: ") + (def != null ? def.name : e.stringParam), NotificationLevel.Info, 6f);
                    break;
                }

                case DialogueEventType.GiveResource:
                {
                    RocketBase b;
                    ResourceType type;
                    if (!GameServices.TryGet(out b) || !Enum.TryParse(e.stringParam, out type)) break;
                    int added = b.Inventory.Add(type, e.intParam);
                    if (added > 0) NotificationSystem.Notify("+" + added + " " + BaseInventory.Label(type) + Tr.T(" en el almacén"), NotificationLevel.Info);
                    break;
                }

                case DialogueEventType.AddLogEntry:
                    LogTerminal.Add(Tr.T("Registro"), e.stringParam, LogCategory.Discovery);
                    break;

                case DialogueEventType.Notify:
                    NotificationSystem.Notify(e.stringParam, NotificationLevel.Info);
                    break;
            }
        }
    }
}
