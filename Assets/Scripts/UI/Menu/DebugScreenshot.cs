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

        /// <summary>Renderiza una cámara a PNG (sin interfaz). Solo para pruebas.</summary>
        public static void RenderCamera(Camera cam, string path, int width = 1280, int height = 720)
        {
            var rt = new RenderTexture(width, height, 24);
            RenderTexture prevTarget = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply(false);
            RenderTexture.active = null;
            cam.targetTexture = prevTarget;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            rt.Release();
            Object.Destroy(rt);
        }
    }
}
