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

            if (node.voiceClip != null) _audio.PlayOneShot(node.voiceClip);
            if (node.roboticStatic)
            {
                _staticSource.clip = _staticClip;
                _staticSource.Play();
            }
            else
            {
                _staticSource.Stop();
            }
            if (node.addToLog) LogTerminal.Add(node.SpeakerName, node.text, LogCategory.Message);
        }

        private void Update()
        {
            if (_current == null) return;

            // Efecto máquina de escribir (tiempo real: funciona aunque el juego esté en pausa).
            int total = _current.text != null ? _current.text.Length : 0;
            if (_shownChars < total)
            {
                int before = (int)_shownChars;
                _shownChars = Mathf.Min(total, _shownChars + charsPerSecond * Time.unscaledDeltaTime);
                int after = (int)_shownChars;
                for (int i = before; i < after; i++)
                {
                    if (_current.voiceClip == null && !char.IsWhiteSpace(_current.text[i]) && ++_blipCounter % blipEveryChars == 0)
                    {
                        _audio.PlayOneShot(_blip, 0.25f);
                    }
                }
            }

            bool typing = _shownChars < total;
            if (RobotInput.DialogueAdvancePressed)
            {
                if (typing) _shownChars = total;
                else if (_current.choices.Count == 0) Advance(_current.next);
            }
            if (!typing && _current.choices.Count > 0)
            {
                for (int i = 0; i < _current.choices.Count && i < 9; i++)
                {
                    if (RobotInput.NumberPressed(i)) Advance(_current.choices[i].next);
                }
            }
        }

        private void Advance(DialogueNode next)
        {
            DialogueNode finished = _current;
            _current = null;
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
            rc.InputEnabled = enabled;
            Cursor.lockState = enabled ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !enabled;
        }

        private void OnGUI()
        {
            if (_current == null) return;
            if (_textStyle == null)
            {
                _textStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, wordWrap = true, richText = true };
                _textStyle.normal.textColor = new Color(0.85f, 1f, 0.9f);
                _speakerStyle = new GUIStyle(GUI.skin.label) { fontSize = 15, fontStyle = FontStyle.Bold };
                _speakerStyle.normal.textColor = new Color(1f, 0.75f, 0.3f);
            }

            float w = Mathf.Min(820f, Screen.width - 40f), h = 200f;
            Rect box = new Rect((Screen.width - w) * 0.5f, Screen.height - h - 20f, w, h);
            Color prev = GUI.color;
            GUI.color = new Color(0f, 0.04f, 0.03f, 0.88f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = new Color(0.2f, 0.9f, 0.6f, 0.6f);
            GUI.DrawTexture(new Rect(box.x, box.y, box.width, 2), Texture2D.whiteTexture);
            GUI.color = prev;

            GUI.Label(new Rect(box.x + 14, box.y + 8, w - 28, 22), "▶ " + _current.SpeakerName, _speakerStyle);
            string text = _current.text ?? "";
            int n = Mathf.Clamp((int)_shownChars, 0, text.Length);
            bool typing = n < text.Length;
            string cursor = typing || Mathf.Repeat(Time.unscaledTime, 1f) < 0.5f ? "█" : "";
            GUI.Label(new Rect(box.x + 14, box.y + 32, w - 28, 100), text.Substring(0, n) + cursor, _textStyle);

            float y = box.y + 136;
            if (!typing && _current.choices.Count > 0)
            {
                for (int i = 0; i < _current.choices.Count; i++)
                {
                    if (GUI.Button(new Rect(box.x + 14, y, w - 28, 24), (i + 1) + ". " + _current.choices[i].text))
                    {
                        Advance(_current.choices[i].next);
                        return;
                    }
                    y += 26;
                }
            }
            else
            {
                GUI.Label(new Rect(box.x + 14, box.y + h - 26, w - 28, 20), typing ? "Intro: mostrar todo" : "Intro o clic: continuar", _speakerStyle);
                if (!typing && Event.current.type == EventType.MouseDown && box.Contains(Event.current.mousePosition))
                {
                    Advance(_current.next);
                }
            }
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
                    LogTerminal.Add("Misión", e.stringParam, LogCategory.Mission);
                    NotificationSystem.Notify("Nueva misión: " + e.stringParam, NotificationLevel.Info, 7f);
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
                    NotificationSystem.Notify("Reparación remota completada.", NotificationLevel.Info);
                    break;
                }

                case DialogueEventType.UnlockTechnology:
                {
                    RocketBase b;
                    if (!GameServices.TryGet(out b)) break;
                    b.Research.ForceUnlock(e.stringParam);
                    StructureDefinition def = StructureCatalog.Find(e.stringParam);
                    NotificationSystem.Notify("Tecnología desbloqueada: " + (def != null ? def.name : e.stringParam), NotificationLevel.Info, 6f);
                    break;
                }

                case DialogueEventType.GiveResource:
                {
                    RocketBase b;
                    ResourceType type;
                    if (!GameServices.TryGet(out b) || !Enum.TryParse(e.stringParam, out type)) break;
                    int added = b.Inventory.Add(type, e.intParam);
                    if (added > 0) NotificationSystem.Notify("+" + added + " " + BaseInventory.Label(type) + " en el almacén", NotificationLevel.Info);
                    break;
                }

                case DialogueEventType.AddLogEntry:
                    LogTerminal.Add("Registro", e.stringParam, LogCategory.Discovery);
                    break;

                case DialogueEventType.Notify:
                    NotificationSystem.Notify(e.stringParam, NotificationLevel.Info);
                    break;
            }
        }
    }
}
