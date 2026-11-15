#if UNITY_EDITOR
using System.Collections.Generic;
using System.Text;
using MarsRobot.Base.Ship;
using UnityEditor;
using UnityEngine;

namespace MarsRobot.EditorTools
{
    /// <summary>
    /// Comprueba por dónde puede caminar el astronauta en cada nivel de la nave: construye la nave
    /// en una escena vacía, prueba la cápsula del astronauta en una rejilla de 10 cm y rellena desde
    /// la salida del montacargas. Informa del % de suelo alcanzable y dibuja un mapa en texto
    /// (· libre alcanzable, # obstáculo, o libre pero encerrado). Menú Marte ▸ Comprobar espacio de la nave.
    /// </summary>
    public static class ShipWalkabilityCheck
    {
        private const float Cell = 0.1f;
        private const float Radius = 0.34f;

        [MenuItem("Marte/Comprobar espacio de la nave")]
        public static void Run()
        {
            var root = new GameObject("Prueba de espacio");
            try
            {
                ShipModel model = ShipModelBuilder.Build(root.transform);
                Physics.SyncTransforms();
                var sb = new StringBuilder("[Espacio] Zona transitable por nivel (astronauta, radio " + Radius + " m):\n");
                for (int level = 0; level < ShipModel.LevelCount; level++) Check(model.transform, level, sb);
                Debug.Log(sb.ToString());
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void Check(Transform ship, int level, StringBuilder sb)
        {
            float y0 = ShipModel.LevelFloorY(level);
            float r = ShipModel.InnerRadius;
            int n = Mathf.CeilToInt(r * 2f / Cell);
            var free = new bool[n, n];
            var inside = new bool[n, n];
            int insideCount = 0;
            for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float x = -r + (i + 0.5f) * Cell, z = -r + (j + 0.5f) * Cell;
                if (x * x + z * z > (r - 0.05f) * (r - 0.05f)) continue;
                inside[i, j] = true;
                insideCount++;
                Vector3 a = ship.TransformPoint(new Vector3(x, y0 + 0.2f + Radius, z));
                Vector3 b = ship.TransformPoint(new Vector3(x, y0 + 1.75f - Radius, z));
                free[i, j] = !Physics.CheckCapsule(a, b, Radius, ~0, QueryTriggerInteraction.Ignore);
            }

            // Relleno desde la salida del montacargas.
            Vector3 start = ShipLayout.LiftDoor + new Vector3(0f, 0f, 0.4f);
            int si = Mathf.Clamp(Mathf.FloorToInt((start.x + r) / Cell), 0, n - 1), sj = Mathf.Clamp(Mathf.FloorToInt((start.z + r) / Cell), 0, n - 1);
            var reach = new bool[n, n];
            int reached = 0, freeCount = 0;
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) if (free[i, j]) freeCount++;
            if (free[si, sj])
            {
                var q = new Queue<Vector2Int>();
                q.Enqueue(new Vector2Int(si, sj));
                reach[si, sj] = true;
                while (q.Count > 0)
                {
                    Vector2Int c = q.Dequeue();
                    reached++;
                    for (int k = 0; k < 4; k++)
                    {
                        int ni = c.x + (k == 0 ? 1 : k == 1 ? -1 : 0), nj = c.y + (k == 2 ? 1 : k == 3 ? -1 : 0);
                        if (ni < 0 || nj < 0 || ni >= n || nj >= n || reach[ni, nj] || !free[ni, nj]) continue;
                        reach[ni, nj] = true;
                        q.Enqueue(new Vector2Int(ni, nj));
                    }
                }
            }
            sb.AppendLine(ShipModel.LevelNames[level] + ": salida del montacargas " + (free[si, sj] ? "libre" : "¡BLOQUEADA!") +
                          " · alcanzable " + Mathf.RoundToInt(100f * reached / insideCount) + " % del suelo (" +
                          Mathf.RoundToInt(100f * (freeCount - reached) / insideCount) + " % libre pero encerrado)");
            // Mapa (cada carácter = 30 cm; +Z, la puerta, arriba).
            for (int j = n - 1; j >= 0; j -= 3)
            {
                var line = new StringBuilder("   ");
                for (int i = 0; i < n; i += 3)
                {
                    if (!inside[i, j]) line.Append(' ');
                    else if (reach[i, j]) line.Append('·');
                    else if (free[i, j]) line.Append('o');
                    else line.Append('#');
                }
                sb.AppendLine(line.ToString());
            }
        }
    }
}
#endif
