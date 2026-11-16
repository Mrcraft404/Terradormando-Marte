using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Dialogue
{
    /// <summary>Importancia de una notificación.</summary>
    public enum NotificationLevel
    {
        Info,
        Warning,
        Critical
    }

    /// <summary>
    /// Avisos flotantes del HUD en primera persona (arriba en el centro), p. ej.
    /// "¡Alerta! Niveles de radiación elevados detectados por el sensor RAD".
    /// Los avisos y alertas críticas se guardan también en la bitácora.
    /// Uso: NotificationSystem.Notify("texto", NotificationLevel.Warning);
    /// </summary>
    public sealed class NotificationSystem : MonoBehaviour
    {
        private sealed class Toast
        {
            public string text;
            public NotificationLevel level;
            public float until;
        }

        private const int MaxVisible = 4;
        private static NotificationSystem _instance;
        private readonly List<Toast> _toasts = new List<Toast>();
        private AudioSource _audio;
        private AudioClip _beep, _alarm;
        private GUIStyle _style;

        private static NotificationSystem Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("Notificaciones (HUD)");
                    _instance = go.AddComponent<NotificationSystem>();
                }
                return _instance;
            }
        }

        /// <summary>Muestra un aviso. Los repetidos en pocos segundos se ignoran.</summary>
        public static void Notify(string text, NotificationLevel level = NotificationLevel.Info, float seconds = 5f)
        {
            NotificationSystem n = Instance;
            for (int i = 0; i < n._toasts.Count; i++)
            {
                if (n._toasts[i].text == text) return;
            }
            n._toasts.Add(new Toast { text = text, level = level, until = Time.unscaledTime + seconds });
            if (n._toasts.Count > MaxVisible) n._toasts.RemoveAt(0);
            if (level != NotificationLevel.Info) LogTerminal.Add(Tr.T("Sistema del rover"), text, level == NotificationLevel.Critical ? LogCategory.Alert : LogCategory.Warning);
            n.PlaySound(level);
        }

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.spatialBlend = 0f;
            _audio.ignoreListenerPause = true;
            _beep = SynthTone(1200f, 0.08f, 1);
            _alarm = SynthTone(700f, 0.12f, 3);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void PlaySound(NotificationLevel level)
        {
            if (_audio == null) return;
            _audio.PlayOneShot(level == NotificationLevel.Critical ? _alarm : _beep, level == NotificationLevel.Info ? 0.2f : 0.45f);
        }

        /// <summary>Pitido sintetizado (pulsos de onda cuadrada suave).</summary>
        public static AudioClip SynthTone(float freq, float pulseSeconds, int pulses)
        {
            const int rate = 22050;
            int pulseSamples = (int)(rate * pulseSeconds);
            int gap = pulseSamples / 2;
            int total = pulses * (pulseSamples + gap);
            var data = new float[total];
            for (int p = 0; p < pulses; p++)
            {
                int start = p * (pulseSamples + gap);
                for (int i = 0; i < pulseSamples; i++)
                {
                    float t = (float)i / rate;
                    float env = Mathf.Min(1f, i / 200f) * Mathf.Min(1f, (pulseSamples - i) / 400f);
                    data[start + i] = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * freq * t)) * 0.25f * env;
                }
            }
            AudioClip clip = AudioClip.Create("Tono", total, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void OnGUI()
        {
            float now = Time.unscaledTime;
            _toasts.RemoveAll(t => now > t.until);
            if (_toasts.Count == 0) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter, richText = true, wordWrap = true };
                _style.normal.textColor = Color.white;
            }

            float w = 560f, h = 34f;
            float x = (Screen.width - w) * 0.5f;
            Color prev = GUI.color;
            for (int i = 0; i < _toasts.Count; i++)
            {
                Toast t = _toasts[i];
                Rect r = new Rect(x, 12 + i * (h + 6), w, h);
                // Parpadeo para las críticas.
                float alpha = t.level == NotificationLevel.Critical ? 0.65f + 0.3f * Mathf.Sin(now * 8f) : 0.75f;
                GUI.color = t.level == NotificationLevel.Critical ? new Color(0.6f, 0.05f, 0.05f, alpha)
                    : (t.level == NotificationLevel.Warning ? new Color(0.55f, 0.35f, 0.02f, alpha) : new Color(0.05f, 0.2f, 0.3f, alpha));
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = prev;
                GUI.Label(r, t.text, _style);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _instance = null;
        }
    }
}
