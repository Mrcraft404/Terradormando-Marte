using MarsRobot.Core;
using UnityEngine;

namespace MarsRobot.World.SpaceWeather
{
    /// <summary>
    /// Interfaz del clima espacial:
    ///  • En el HUD: banda de cuenta atrás de la CME y avisos de estado (interferencias, radiación,
    ///    tormenta geomagnética, tormenta global).
    ///  • DrawStatusPanel: panel completo para la terminal del cohete (origen de los datos, eventos
    ///    activos, próximos y pasados).
    /// Los avisos puntuales los lanza SpaceWeatherManager con NotificationSystem.
    /// </summary>
    public sealed class SpaceWeatherHUD : MonoBehaviour
    {
        private GUIStyle _banner;

        private void OnGUI()
        {
            if (_banner == null)
            {
                _banner = new GUIStyle(GUI.skin.label) { fontSize = 16, alignment = TextAnchor.MiddleCenter, richText = true, fontStyle = FontStyle.Bold };
                _banner.normal.textColor = Color.white;
            }

            string text = null;
            Color bg = new Color(0f, 0f, 0f, 0f);
            float pulse = 0.6f + 0.3f * Mathf.Sin(Time.unscaledTime * 6f);
            if (SpaceWeatherState.CmeCountdownHours >= 0f)
            {
                float h = SpaceWeatherState.CmeCountdownHours;
                int hh = Mathf.FloorToInt(h);
                int mm = Mathf.FloorToInt((h - hh) * 60f);
                text = Tr.T("⚠ EYECCIÓN DE MASA CORONAL EN CAMINO · impacto en ") + hh + " h " + mm.ToString("00") + Tr.T(" min · busque refugio");
                bg = new Color(0.6f, 0.05f, 0.05f, pulse);
            }
            else if (SpaceWeatherState.GlobalStormActive)
            {
                text = Tr.T("TORMENTA GLOBAL · visibilidad mínima · energía solar nula");
                bg = new Color(0.45f, 0.2f, 0.05f, 0.85f);
            }
            if (text != null)
            {
                Rect r = new Rect(Screen.width * 0.5f - 330, 190, 660, 30);
                Color prev = GUI.color;
                GUI.color = bg;
                GUI.DrawTexture(r, Texture2D.whiteTexture);
                GUI.color = prev;
                GUI.Label(r, text, _banner);
            }

            // Indicadores pequeños bajo la banda.
            string flags = "";
            if (SpaceWeatherState.RadarBlocked) flags += Tr.T("<color=#ff8080>INTERFERENCIA POR FULGURACIÓN SOLAR</color>   ");
            if (SpaceWeatherState.SepActive) flags += Tr.T("<color=#ffd060>RADIACIÓN POR PARTÍCULAS SOLARES</color>   ");
            if (SpaceWeatherState.GstActive) flags += "<color=#a0a0ff>TORMENTA GEOMAGNÉTICA</color>";
            if (flags.Length > 0) GUI.Label(new Rect(Screen.width * 0.5f - 330, 222, 660, 22), flags, _banner);
        }

        /// <summary>Panel de estado para la terminal del cohete (dentro de un menú GUILayout).</summary>
        public static void DrawStatusPanel(SpaceWeatherManager m)
        {
            GUILayout.Label(Tr.T("<b>Origen:</b> ") + m.SourceStatus);
            GUILayout.Label(Tr.T("Fuente: base de datos de clima espacial de la NASA (fulguraciones solares, eyecciones de masa coronal, partículas solares energéticas y tormentas geomagnéticas). Los últimos 30 días se reparten en los próximos soles."));

            GUILayout.Space(4);
            GUILayout.Label("<b>Activo ahora</b>");
            bool any = false;
            foreach (SpaceWeatherEvent e in m.ActiveEvents())
            {
                any = true;
                GUILayout.Label("  • " + e.label);
            }
            if (!any) GUILayout.Label(Tr.T("  Sin eventos activos."));
            if (SpaceWeatherState.CmeCountdownHours >= 0f) GUILayout.Label(Tr.T("  <color=#ff8080>Eyección de masa coronal en camino: impacto en ") + SpaceWeatherState.CmeCountdownHours.ToString("0.0") + " h</color>");

            GUILayout.Space(4);
            GUILayout.Label(Tr.T("<b>Previsión</b> (horas de juego)"));
            int shown = 0;
            foreach (var kv in m.Upcoming())
            {
                if (shown++ >= 6) break;
                GUILayout.Label("  • " + kv.Key.label + Tr.T(" — en ") + Mathf.Max(0f, kv.Value).ToString("0.0") + " h");
            }
            if (shown == 0) GUILayout.Label(m.Loaded ? Tr.T("  Nada previsto.") : Tr.T("  Cargando datos…"));

            GUILayout.Space(4);
            GUILayout.Label("<b>Historial</b>");
            for (int i = m.History.Count - 1, n = 0; i >= 0 && n < 5; i--, n++)
            {
                GUILayout.Label("  • " + m.History[i].label);
            }
        }
    }
}
