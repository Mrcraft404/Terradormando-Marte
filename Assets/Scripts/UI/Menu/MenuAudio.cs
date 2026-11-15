using UnityEngine;

namespace MarsRobot.UI.Menu
{
    /// <summary>Sonidos del menú generados por código: clic, paso del ratón y viento de ambiente.</summary>
    public sealed class MenuAudio : MonoBehaviour
    {
        private AudioSource _sfx, _ambient;
        private AudioClip _click, _hover;
        private float _lastHover;

        private void Awake()
        {
            _sfx = gameObject.AddComponent<AudioSource>();
            _sfx.playOnAwake = false;
            _ambient = gameObject.AddComponent<AudioSource>();
            _ambient.playOnAwake = false;
            _ambient.loop = true;
            _ambient.volume = 0.35f;
            _click = Tone("clic", 0.09f, 820f, 420f, 0.5f);
            _hover = Tone("hover", 0.035f, 1500f, 1300f, 0.18f);
            _ambient.clip = Wind(8f);
            _ambient.Play();
        }

        public void Click()
        {
            _sfx.PlayOneShot(_click, 0.7f);
        }

        public void Hover()
        {
            if (Time.unscaledTime - _lastHover < 0.05f) return;
            _lastHover = Time.unscaledTime;
            _sfx.PlayOneShot(_hover, 0.5f);
        }

        public void FadeOutAmbient(float seconds)
        {
            StartCoroutine(Fade(seconds));
        }

        private System.Collections.IEnumerator Fade(float seconds)
        {
            float start = _ambient.volume;
            for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
            {
                _ambient.volume = Mathf.Lerp(start, 0f, t / seconds);
                yield return null;
            }
            _ambient.volume = 0f;
        }

        private static AudioClip Tone(string name, float seconds, float f0, float f1, float amp)
        {
            const int rate = 44100;
            int n = Mathf.CeilToInt(seconds * rate);
            var data = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)n;
                phase += 2f * Mathf.PI * Mathf.Lerp(f0, f1, t) / rate;
                float env = Mathf.Exp(-t * 6f) * Mathf.Clamp01(i / 60f);
                data[i] = (Mathf.Sin(phase) + 0.3f * Mathf.Sin(phase * 2f)) * env * amp;
            }
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Viento marciano: ruido marrón filtrado con ráfagas, en bucle sin cortes.</summary>
        private static AudioClip Wind(float seconds)
        {
            const int rate = 22050;
            int n = Mathf.CeilToInt(seconds * rate);
            int fade = rate / 2;
            var raw = new float[n + fade];
            var rnd = new System.Random(7);
            float b = 0f, b2 = 0f;
            for (int i = 0; i < raw.Length; i++)
            {
                float white = (float)rnd.NextDouble() * 2f - 1f;
                b = (b + 0.02f * white) / 1.02f;
                b2 += (b - b2) * 0.08f;
                float t = i / (float)n;
                float gust = 0.55f + 0.3f * Mathf.Sin(t * 4f * Mathf.PI) + 0.15f * Mathf.Sin(t * 10f * Mathf.PI + 1.3f);
                raw[i] = b2 * 9f * gust;
            }
            var data = new float[n];
            for (int i = 0; i < n; i++) data[i] = raw[i];
            for (int i = 0; i < fade; i++) data[i] = Mathf.Lerp(raw[n + i], raw[i], i / (float)fade);
            AudioClip clip = AudioClip.Create("viento", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
