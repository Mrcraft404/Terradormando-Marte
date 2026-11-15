#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MarsRobot.Items;
using MarsRobot.UI;
using UnityEditor;
using UnityEngine;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Exporta todos los iconos de objetos en una sola imagen (Logs/IconSheet.png), sobre el mismo
    /// fondo de casilla del inventario, para revisarlos de un vistazo. Menú Marte ▸ Exportar hoja de iconos.
    /// También sirve como punto de partida para pintar iconos propios (Resources/ItemIcons/&lt;itemID&gt;.png).
    /// </summary>
    public static class ItemIconSheet
    {
        [MenuItem("Marte/Exportar hoja de iconos")]
        public static void Export()
        {
            List<ItemData> items = ItemDatabase.All.Where(i => i != null).OrderBy(i => i.itemCategory).ThenBy(i => i.itemName).ToList();
            const int cell = 140, cols = 6, icon = 112;
            int rows = Mathf.CeilToInt(items.Count / (float)cols);
            var sheet = new Texture2D(cols * cell, rows * cell, TextureFormat.RGBA32, false);
            var slot = new Color(0.1f, 0.18f, 0.27f, 1f);
            var px = new Color[sheet.width * sheet.height];
            for (int i = 0; i < px.Length; i++) px[i] = new Color(0.04f, 0.09f, 0.16f, 1f);

            var log = new System.Text.StringBuilder("[Iconos] Orden (de arriba a abajo, de izquierda a derecha):\n");
            for (int n = 0; n < items.Count; n++)
            {
                int cx = n % cols, cy = rows - 1 - n / cols;
                int ox = cx * cell, oy = cy * cell;
                for (int y = 4; y < cell - 4; y++)
                for (int x = 4; x < cell - 4; x++)
                    px[(oy + y) * sheet.width + ox + x] = slot;

                Texture2D t = ItemIconPainter.For(items[n]);
                log.AppendLine("  " + (n + 1) + ". " + items[n].itemName + " (" + items[n].itemID + ")" + (t == null ? "  ¡SIN ICONO!" : ""));
                if (t == null) continue;
                int off = (cell - icon) / 2;
                for (int y = 0; y < icon; y++)
                for (int x = 0; x < icon; x++)
                {
                    Color c = t.GetPixelBilinear((x + 0.5f) / icon, (y + 0.5f) / icon);
                    int i = (oy + off + y) * sheet.width + ox + off + x;
                    px[i] = Color.Lerp(px[i], new Color(c.r, c.g, c.b, 1f), c.a);
                }
            }
            sheet.SetPixels(px);
            sheet.Apply();
            string path = Path.Combine(Directory.GetCurrentDirectory(), "Logs", "IconSheet.png");
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, sheet.EncodeToPNG());
            Object.DestroyImmediate(sheet);
            Debug.Log(log + "Hoja guardada en " + path);
        }
    }
}
#endif
