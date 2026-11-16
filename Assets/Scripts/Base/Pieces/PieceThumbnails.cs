using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Fotos (128 × 128) de las piezas para el menú de construcción. Cada modelo se monta en un
    /// "estudio" muy por debajo del terreno, con su cámara y su luz, se renderiza una vez a una
    /// textura y se destruye. Las fotos se guardan en memoria (≈ 64 KB cada una) y se piden por clave.
    /// Se generan pocas por frame para no dar tirones.
    /// </summary>
    public static class PieceThumbnails
    {
        public const int Size = 128;
        private static readonly Vector3 StudioPosition = new Vector3(0f, -6000f, 0f);

        private static readonly Dictionary<string, Texture2D> Icons = new Dictionary<string, Texture2D>();
        private static readonly Queue<KeyValuePair<string, Action<Transform>>> Pending = new Queue<KeyValuePair<string, Action<Transform>>>();
        private static readonly HashSet<string> Requested = new HashSet<string>();
        private static Camera _cam;
        private static Light _light;
        private static RenderTexture _rt;

        /// <summary>Foto ya hecha, o null si aún no está (se pide y llegará en unos frames).</summary>
        public static Texture2D Get(string key, Action<Transform> buildModel)
        {
            Texture2D tex;
            if (Icons.TryGetValue(key, out tex) && tex != null) return tex;
            if (Requested.Add(key)) Pending.Enqueue(new KeyValuePair<string, Action<Transform>>(key, buildModel));
            return null;
        }

        /// <summary>Hace hasta <paramref name="count"/> fotos pendientes. Llamar una vez por frame mientras el menú esté abierto.</summary>
        public static void Process(int count)
        {
            for (int i = 0; i < count && Pending.Count > 0; i++)
            {
                KeyValuePair<string, Action<Transform>> job = Pending.Dequeue();
                Icons[job.Key] = Render(job.Value);
            }
        }

        private static void EnsureStudio()
        {
            if (_cam != null) return;
            var studio = new GameObject("Estudio de miniaturas de construcción");
            studio.transform.position = StudioPosition;
            studio.hideFlags = HideFlags.DontSave;

            var camGo = new GameObject("Cámara");
            camGo.transform.SetParent(studio.transform, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.enabled = false;                  // solo renderiza cuando se le pide
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0.1f, 0.18f, 0.27f, 1f);
            _cam.fieldOfView = 30f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 200f;

            var lightGo = new GameObject("Luz");
            lightGo.transform.SetParent(studio.transform, false);
            lightGo.transform.rotation = Quaternion.Euler(40f, -30f, 0f);
            _light = lightGo.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.range = 120f;
            _light.intensity = 2.5f;

            _rt = new RenderTexture(Size, Size, 16, RenderTextureFormat.ARGB32) { antiAliasing = 2, name = "Miniaturas" };
        }

        private static Texture2D Render(Action<Transform> buildModel)
        {
            EnsureStudio();
            var model = new GameObject("Modelo");
            model.transform.position = StudioPosition;
            Texture2D tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                if (buildModel != null) buildModel(model.transform);

                // Encuadre automático: vista de 3/4 que abarca todo el modelo.
                var bounds = new Bounds(StudioPosition, Vector3.one * 0.5f);
                foreach (Renderer r in model.GetComponentsInChildren<Renderer>(true)) bounds.Encapsulate(r.bounds);
                float radius = Mathf.Max(0.5f, bounds.extents.magnitude);
                float dist = radius / Mathf.Sin(_cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * 1.05f;
                Vector3 dir = Quaternion.Euler(28f, -35f, 0f) * Vector3.back;
                _cam.transform.position = bounds.center + dir * dist;
                _cam.transform.LookAt(bounds.center);
                _cam.farClipPlane = dist + radius * 2f;
                _light.transform.position = bounds.center + Quaternion.Euler(50f, -60f, 0f) * Vector3.back * dist;

                _cam.targetTexture = _rt;
                _cam.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = _rt;
                tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                tex.Apply(false, false);
                RenderTexture.active = previous;
                _cam.targetTexture = null;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Construcción] No se pudo hacer la miniatura: " + e.Message);
            }
            finally
            {
                // El modelo y sus mallas generadas se liberan (los materiales son compartidos).
                foreach (MeshFilter mf in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.sharedMesh != null && mf.sharedMesh.name.Length > 0 && !mf.sharedMesh.name.StartsWith("Cube") &&
                        !mf.sharedMesh.name.StartsWith("Cylinder") && !mf.sharedMesh.name.StartsWith("Sphere") &&
                        !mf.sharedMesh.name.StartsWith("Capsule"))
                        UnityEngine.Object.Destroy(mf.sharedMesh);
                }
                UnityEngine.Object.Destroy(model);
            }
            return tex;
        }
    }
}
