using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.UI
{
    /// <summary>
    /// Marti, el guía del juego: un planeta Marte animado con auriculares que da los consejos.
    ///  • Say(texto): muestra un consejo en un bocadillo (en cola si ya hay otro).
    ///  • Los consejos esperan mientras haya un diálogo abierto, para no taparse.
    ///  • DrawPortrait: dibuja a Marti con un pequeño bote al hablar (lo usa también el diálogo).
    /// La imagen está en Assets/Resources/Mascot/marti.png.
    /// </summary>
    public sealed class MascotGuide : MonoBehaviour
    {
        public const string Name = "Marti";

        private static MascotGuide _instance;
        private static Texture2D _portrait;
        private static bool _portraitLoaded;

        private readonly Queue<string> _queue = new Queue<string>();
        private string _current;
        private float _shownAt;
        private float _duration;
        private GUIStyle _bubbleStyle, _nameStyle;
        private AudioSource _voice;

        /// <summary>La imagen de Marti (null si falta el archivo).</summary>
        public static Texture2D Portrait
        {
            get
            {
                if (!_portraitLoaded)
                {
                    _portraitLoaded = true;
                    _portrait = Resources.Load<Texture2D>("Mascot/marti");
                }
                return _portrait;
            }
        }

        /// <summary>
        /// Voz de Marti para una frase (clave del Localizer), en el idioma elegido:
        /// Resources/Mascot/Voice/es/&lt;clave&gt; o Resources/Mascot/Voice/en/&lt;clave&gt;. Null si no hay.
        /// </summary>
        public static AudioClip VoiceFor(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            string lang = MarsRobot.Localization.Localizer.English ? "en" : "es";
            return Resources.Load<AudioClip>("Mascot/Voice/" + lang + "/" + key);
        }

        /// <summary>Marti dice la frase en voz alta, sin bocadillo (la pantalla ya muestra el texto).</summary>
        public static void SpeakOnly(string key)
        {
            AudioClip clip = VoiceFor(key);
            if (clip == null) return;
            if (_instance == null)
            {
                var go = new GameObject("Marti (guía)");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<MascotGuide>();
                _instance._voice = go.AddComponent<AudioSource>();
                _instance._voice.spatialBlend = 0f;
                _instance._voice.ignoreListenerPause = true;
            }
            _instance._voice.Stop();
            _instance._voice.PlayOneShot(clip);
        }

        /// <summary>
        /// Marti da un consejo (se ve unos segundos; si ya hay otro, espera su turno).
        /// "key" es una clave del Localizer (marti_…): así sale en el idioma elegido y con su voz.
        /// También acepta un texto suelto (sin voz).
        /// </summary>
        public static void Say(string key, float seconds = 7f)
        {
            string text = key;
            if (string.IsNullOrEmpty(text)) return;
            if (_instance == null)
            {
                var go = new GameObject("Marti (guía)");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<MascotGuide>();
                _instance._voice = go.AddComponent<AudioSource>();
                _instance._voice.spatialBlend = 0f;
                _instance._voice.ignoreListenerPause = true;
            }
            // Evita repetir el mismo consejo seguido.
            if (text == _instance._current) return;
            foreach (string q in _instance._queue) if (q == text) return;
            _instance._queue.Enqueue(text);
            _instance._duration = Mathf.Max(4f, seconds);
        }

        /// <summary>¿Hay ahora un diálogo abierto que no debemos tapar?</summary>
        private static bool Blocked()
        {
            return MarsRobot.Dialogue.DialogueManager.AnyShowing;
        }

        private void Update()
        {
            if (Blocked())
            {
                // Pausa el reloj del consejo mientras habla el diálogo.
                if (_current != null) _shownAt += Time.unscaledDeltaTime;
                return;
            }
            // El consejo dura lo que tarda en leerse (y al menos lo que dura su voz).
            if (_current != null && Time.unscaledTime - _shownAt > _length) _current = null;
            if (_current == null && _queue.Count > 0)
            {
                _current = _queue.Dequeue();
                _shownAt = Time.unscaledTime;
                string shown = MarsRobot.Localization.Localizer.T(_current);
                _length = _duration + shown.Length * 0.03f;
                AudioClip clip = VoiceFor(_current);
                if (clip != null && _voice != null)
                {
                    _voice.Stop();
                    _voice.PlayOneShot(clip);
                    _length = Mathf.Max(_length, clip.length + 1.5f);
                }
            }
        }

        private float _length;

        private void OnGUI()
        {
            if (_current == null || Blocked()) return;
            string shown = MarsRobot.Localization.Localizer.T(_current);
            if (_bubbleStyle == null)
            {
                _bubbleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, wordWrap = true, richText = true, alignment = TextAnchor.UpperLeft };
                _bubbleStyle.normal.textColor = new Color(0.12f, 0.1f, 0.14f);
                _nameStyle = new GUIStyle(GUI.skin.label) { fontSize = 18, fontStyle = FontStyle.Bold };
                _nameStyle.normal.textColor = new Color(0.85f, 0.32f, 0.12f);
            }

            HudTheme.BeginScaled();
            float t = Time.unscaledTime - _shownAt;
            float appear = Mathf.Clamp01(t * 4f);
            float slide = (1f - appear) * -260f;

            // Marti a la izquierda, a media altura (no tapa el diálogo ni la barra de abajo).
            var face = new Rect(18f + slide, 540f, 130f, 130f);
            DrawPortrait(face, _voice != null && _voice.isPlaying ? true : t < 2.5f);

            // Bocadillo blanco con el texto.
            const float bubbleW = 560f;
            float textH = _bubbleStyle.CalcHeight(new GUIContent(shown), bubbleW - 30f) + 4f;
            var bubble = new Rect(face.xMax + 8f, 548f, bubbleW, Mathf.Max(116f, textH + 44f));
            Color prev = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, appear);
            HudTheme.Frame(bubble, new Color(1f, 0.97f, 0.92f, 0.97f), new Color(0.85f, 0.32f, 0.12f), 3f);
            // Pico del bocadillo hacia Marti.
            HudTheme.Fill(new Rect(bubble.x - 10f, bubble.y + 44f, 12f, 22f), new Color(1f, 0.97f, 0.92f, 0.97f));
            GUI.Label(new Rect(bubble.x + 16f, bubble.y + 6f, 300f, 24f), Name + MarsRobot.Localization.Localizer.L(" dice:", " says:"), _nameStyle);
            GUI.Label(new Rect(bubble.x + 16f, bubble.y + 32f, bubble.width - 30f, textH), shown, _bubbleStyle);
            GUI.color = prev;
            HudTheme.EndScaled();
        }

        /// <summary>Dibuja a Marti en el rectángulo; si habla, da pequeños saltitos.</summary>
        public static void DrawPortrait(Rect r, bool talking)
        {
            Texture2D tex = Portrait;
            float bob = talking ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 9f)) * 6f : Mathf.Sin(Time.unscaledTime * 2f) * 2f;
            float squash = talking ? 1f + Mathf.Sin(Time.unscaledTime * 18f) * 0.025f : 1f;
            var rr = new Rect(r.x + r.width * (1f - squash) * 0.5f, r.y - bob, r.width * squash, r.height / squash);
            if (tex != null)
            {
                GUI.DrawTexture(rr, tex, ScaleMode.ScaleToFit, true);
            }
            else
            {
                // Sin imagen: un círculo rojo de reserva.
                HudTheme.Fill(rr, new Color(0.85f, 0.32f, 0.12f));
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _instance = null;
            _portrait = null;
            _portraitLoaded = false;
        }
    }
}
