using System.Collections;
using System.IO;
using UnityEngine;

namespace MarsRobot.UI.Menu
{
    /// <summary>
    /// Captura de pantalla sin el módulo ScreenCapture (no está instalado): lee el framebuffer al
    /// final del frame (después de dibujar el IMGUI) y lo guarda en PNG. Solo para pruebas.
    /// </summary>
    public static class DebugScreenshot
    {
        public static IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0);
            tex.Apply(false);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}
