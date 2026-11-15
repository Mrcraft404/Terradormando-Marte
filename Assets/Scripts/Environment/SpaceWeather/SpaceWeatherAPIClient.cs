using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace MarsRobot.World.SpaceWeather
{
    /// <summary>Tipos de evento de DONKI que usa el juego.</summary>
    public enum SpaceWeatherEventType
    {
        FLR, // Fulguración solar (Solar Flare)
        CME, // Eyección de masa coronal
        SEP, // Partículas energéticas solares
        GST  // Tormenta geomagnética
    }

    /// <summary>Un evento ya interpretado (real o simulado).</summary>
    [Serializable]
    public class SpaceWeatherEvent
    {
        public SpaceWeatherEventType type;
        public DateTime time;           // hora real (UTC) del evento
        public float intensity01;       // 0..1
        public string label;            // p. ej. "Fulguración X1.2", "CME a 1200 km/s"
        public bool simulated;
    }

    /// <summary>
    /// Cliente de la API pública DONKI de la NASA (Space Weather Database Of Notifications,
    /// Knowledge, Information). Pide los últimos 30 días de FLR, CME, SEP y GST.
    ///  • Peticiones asíncronas con UnityWebRequest (corrutina), con tiempo de espera.
    ///  • Clave por defecto DEMO_KEY (limitada a pocas peticiones por hora; se puede pedir una gratis
    ///    en api.nasa.gov y ponerla en el Inspector).
    ///  • Si no hay conexión o la API falla, genera eventos simulados con frecuencias realistas.
    /// No envía ningún dato del jugador: solo fechas y la clave.
    /// </summary>
    public sealed class SpaceWeatherAPIClient : MonoBehaviour
    {
        private const string BaseUrl = "https://api.nasa.gov/DONKI/";

        [SerializeField] private string apiKey = "DEMO_KEY";
        [SerializeField, Min(1)] private int daysBack = 30;
        [SerializeField, Min(2)] private int timeoutSeconds = 12;
        [Tooltip("No conectar nunca: usar siempre la simulación.")]
        [SerializeField] private bool offlineOnly;

        /// <summary>True si la última carga vino (al menos en parte) de la API real.</summary>
        public bool LastFromApi { get; private set; }

        /// <summary>Mensaje de estado de la última carga (para la terminal).</summary>
        public string LastStatus { get; private set; }

        public void Configure(string key, bool offline)
        {
            if (!string.IsNullOrEmpty(key)) apiKey = key;
            offlineOnly = offline;
        }

        /// <summary>Descarga los cuatro tipos de evento y llama a <paramref name="done"/> con todos.</summary>
        public IEnumerator Fetch(Action<List<SpaceWeatherEvent>> done)
        {
            var all = new List<SpaceWeatherEvent>();
            DateTime end = DateTime.UtcNow.Date;
            DateTime start = end.AddDays(-daysBack);
            int ok = 0;

            foreach (SpaceWeatherEventType type in Enum.GetValues(typeof(SpaceWeatherEventType)))
            {
                string json = null;
                if (!offlineOnly)
                {
                    string url = BaseUrl + type + "?startDate=" + start.ToString("yyyy-MM-dd") + "&endDate=" + end.ToString("yyyy-MM-dd") +
                                 "&api_key=" + UnityWebRequest.EscapeURL(apiKey);
                    using (UnityWebRequest req = UnityWebRequest.Get(url))
                    {
                        req.timeout = timeoutSeconds;
                        yield return req.SendWebRequest();
                        if (req.result == UnityWebRequest.Result.Success) json = req.downloadHandler.text;
                        else Debug.Log("[SpaceWeather] DONKI " + type + " no disponible (" + req.error + "): se usará simulación.");
                    }
                }

                List<SpaceWeatherEvent> parsed = json != null ? Parse(type, json) : null;
                if (parsed != null)
                {
                    ok++;
                    all.AddRange(parsed);
                }
                else
                {
                    all.AddRange(Simulate(type, start, end));
                }
            }

            LastFromApi = ok > 0;
            LastStatus = ok == 4 ? "Datos reales de NASA DONKI (" + start.ToString("dd/MM") + "–" + end.ToString("dd/MM/yyyy") + ")"
                : ok > 0 ? "Datos reales parciales de DONKI + simulación"
                : "Sin conexión con DONKI: clima espacial simulado";
            all.Sort((a, b) => a.time.CompareTo(b.time));
            done(all);
        }

        // ------------------------------------------------------------------
        // Lectura del JSON de DONKI
        // ------------------------------------------------------------------

        [Serializable] private class FlrItem { public string flrID; public string beginTime; public string classType; }
        [Serializable] private class CmeAnalysis { public float speed; public bool isMostAccurate; }
        [Serializable] private class CmeItem { public string activityID; public string startTime; public CmeAnalysis[] cmeAnalyses; }
        [Serializable] private class SepItem { public string sepID; public string eventTime; }
        [Serializable] private class KpItem { public float kpIndex; }
        [Serializable] private class GstItem { public string gstID; public string startTime; public KpItem[] allKpIndex; }
        [Serializable] private class Wrapper<T> { public T[] items; }

        private static List<SpaceWeatherEvent> Parse(SpaceWeatherEventType type, string json)
        {
            try
            {
                // DONKI devuelve un array en la raíz; JsonUtility necesita un objeto.
                string wrapped = "{\"items\":" + (string.IsNullOrWhiteSpace(json) ? "[]" : json) + "}";
                var list = new List<SpaceWeatherEvent>();
                switch (type)
                {
                    case SpaceWeatherEventType.FLR:
                        foreach (FlrItem f in Items<FlrItem>(wrapped))
                        {
                            list.Add(new SpaceWeatherEvent { type = type, time = Date(f.beginTime), intensity01 = FlareIntensity(f.classType), label = "Fulguración " + f.classType });
                        }
                        break;
                    case SpaceWeatherEventType.CME:
                        foreach (CmeItem c in Items<CmeItem>(wrapped))
                        {
                            float speed = 0f;
                            if (c.cmeAnalyses != null)
                            {
                                foreach (CmeAnalysis a in c.cmeAnalyses) if (a.isMostAccurate || speed <= 0f) speed = a.speed;
                            }
                            if (speed <= 0f) speed = 500f;
                            list.Add(new SpaceWeatherEvent { type = type, time = Date(c.startTime), intensity01 = Mathf.InverseLerp(300f, 2500f, speed), label = "CME a " + speed.ToString("0") + " km/s" });
                        }
                        break;
                    case SpaceWeatherEventType.SEP:
                        foreach (SepItem s in Items<SepItem>(wrapped))
                        {
                            list.Add(new SpaceWeatherEvent { type = type, time = Date(s.eventTime), intensity01 = 0.6f, label = "Partículas energéticas (SEP)" });
                        }
                        break;
                    case SpaceWeatherEventType.GST:
                        foreach (GstItem g in Items<GstItem>(wrapped))
                        {
                            float kp = 5f;
                            if (g.allKpIndex != null) foreach (KpItem k in g.allKpIndex) kp = Mathf.Max(kp, k.kpIndex);
                            list.Add(new SpaceWeatherEvent { type = type, time = Date(g.startTime), intensity01 = Mathf.InverseLerp(4f, 9f, kp), label = "Tormenta geomagnética Kp " + kp.ToString("0") });
                        }
                        break;
                }
                return list;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[SpaceWeather] No se pudo leer la respuesta de DONKI (" + type + "): " + e.Message);
                return null;
            }
        }

        private static T[] Items<T>(string wrapped)
        {
            Wrapper<T> w = JsonUtility.FromJson<Wrapper<T>>(wrapped);
            return w != null && w.items != null ? w.items : new T[0];
        }

        private static DateTime Date(string s)
        {
            DateTime d;
            if (!string.IsNullOrEmpty(s) && DateTime.TryParse(s.Replace("Z", ""), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out d)) return d.ToUniversalTime();
            return DateTime.UtcNow;
        }

        /// <summary>Clase de fulguración (A, B, C, M, X + número) → 0..1.</summary>
        private static float FlareIntensity(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return 0.3f;
            float n;
            float.TryParse(cls.Substring(1), NumberStyles.Float, CultureInfo.InvariantCulture, out n);
            switch (char.ToUpperInvariant(cls[0]))
            {
                case 'X': return Mathf.Clamp01(0.8f + n * 0.02f);
                case 'M': return Mathf.Clamp01(0.45f + n * 0.03f);
                case 'C': return Mathf.Clamp01(0.2f + n * 0.02f);
                default: return 0.1f;
            }
        }

        // ------------------------------------------------------------------
        // Simulación (sin conexión): frecuencias mensuales aproximadas en máximo solar
        // ------------------------------------------------------------------

        private static List<SpaceWeatherEvent> Simulate(SpaceWeatherEventType type, DateTime start, DateTime end)
        {
            var rng = new System.Random(Environment.TickCount + (int)type * 977);
            int count;
            switch (type)
            {
                case SpaceWeatherEventType.FLR: count = rng.Next(6, 14); break;  // decenas de clase C/M al mes
                case SpaceWeatherEventType.CME: count = rng.Next(2, 6); break;
                case SpaceWeatherEventType.SEP: count = rng.Next(0, 3); break;
                default: count = rng.Next(1, 4); break;
            }
            var list = new List<SpaceWeatherEvent>();
            double span = (end - start).TotalHours;
            for (int i = 0; i < count; i++)
            {
                var e = new SpaceWeatherEvent { type = type, time = start.AddHours(rng.NextDouble() * span), simulated = true };
                float r = (float)rng.NextDouble();
                switch (type)
                {
                    case SpaceWeatherEventType.FLR:
                        // Distribución tipo ley de potencias: muchas C, algunas M, pocas X.
                        string cls = r < 0.7f ? "C" + (1 + rng.Next(9)) : (r < 0.95f ? "M" + (1 + rng.Next(9)) : "X" + (1 + rng.Next(3)));
                        e.intensity01 = FlareIntensity(cls);
                        e.label = "Fulguración " + cls + " (simulada)";
                        break;
                    case SpaceWeatherEventType.CME:
                        float speed = 400f + r * r * 1800f;
                        e.intensity01 = Mathf.InverseLerp(300f, 2500f, speed);
                        e.label = "CME a " + speed.ToString("0") + " km/s (simulada)";
                        break;
                    case SpaceWeatherEventType.SEP:
                        e.intensity01 = 0.4f + r * 0.5f;
                        e.label = "Partículas energéticas (simuladas)";
                        break;
                    default:
                        float kp = 5f + r * 4f;
                        e.intensity01 = Mathf.InverseLerp(4f, 9f, kp);
                        e.label = "Tormenta geomagnética Kp " + kp.ToString("0") + " (simulada)";
                        break;
                }
                list.Add(e);
            }
            return list;
        }
    }
}
