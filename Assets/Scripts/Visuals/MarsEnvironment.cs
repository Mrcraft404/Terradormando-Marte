using System.Collections;
using MarsRobot.Base.Ship;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarsRobot.Visuals
{
    /// <summary>
    /// Entorno visual (render integrado): cielo marciano procedural (polvo color caramelo de día,
    /// atardecer azulado) y sondas de reflejos en tiempo real que se renderizan una vez al montar la
    /// escena. Sin sondas, los metales reflejan "negro"; con ellas, el acero de la nave refleja el
    /// cielo y el interior iluminado, que es lo que más acerca la imagen a un aspecto realista.
    /// El material del cielo se crea en Assets/Resources/Environment/MarsSky.mat
    /// (Marte ▸ Arquitectura de escena ▸ Crear jerarquía y cielo).
    /// </summary>
    public sealed class MarsEnvironment : MonoBehaviour
    {
        public const string SkyResource = "Environment/MarsSky";

        private ReflectionProbe[] _probes = new ReflectionProbe[0];

        /// <summary>Prepara cielo, cámaras y sondas. Llamar cuando la escena ya está montada.</summary>
        public static MarsEnvironment Setup(ShipModel ship)
        {
            var go = new GameObject("Entorno visual marciano");
            go.transform.SetParent(SceneRoots.Get(SceneRoot.Environment), false);
            var env = go.AddComponent<MarsEnvironment>();
            env.ApplySky();
            env.CreateProbes(ship);
            return env;
        }

        private void ApplySky()
        {
            Material sky = Resources.Load<Material>(SkyResource);
            if (sky == null)
            {
                Shader s = Shader.Find("Skybox/Procedural");
                if (s != null)
                {
                    sky = new Material(s) { name = "Cielo marciano (en memoria)" };
                    ConfigureSky(sky);
                }
            }
            if (sky == null)
            {
                Debug.LogWarning("[MarsEnvironment] No hay material de cielo (crea Resources/Environment/MarsSky con Marte ▸ Arquitectura de escena). Se mantiene el fondo de color.");
                return;
            }
            RenderSettings.skybox = sky;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
            RenderSettings.reflectionIntensity = 1f;
            DynamicGI.UpdateEnvironment();
            foreach (Camera c in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None)) UseSky(c);
        }

        /// <summary>Las cámaras del juego pasan de fondo de color a cielo.</summary>
        public static void UseSky(Camera c)
        {
            if (c == null || c.targetTexture != null || RenderSettings.skybox == null) return;
            if (c.clearFlags == CameraClearFlags.SolidColor) c.clearFlags = CameraClearFlags.Skybox;
        }

        /// <summary>Valores del cielo procedural de Unity ajustados a Marte (atmósfera fina y polvorienta).</summary>
        public static void ConfigureSky(Material sky)
        {
            if (sky == null) return;
            if (sky.HasProperty("_SunDisk")) sky.SetFloat("_SunDisk", 2f);
            if (sky.HasProperty("_SunSize")) sky.SetFloat("_SunSize", 0.025f);
            if (sky.HasProperty("_SunSizeConvergence")) sky.SetFloat("_SunSizeConvergence", 6f);
            if (sky.HasProperty("_AtmosphereThickness")) sky.SetFloat("_AtmosphereThickness", 0.55f);
            if (sky.HasProperty("_SkyTint")) sky.SetColor("_SkyTint", new Color(0.86f, 0.62f, 0.42f));
            if (sky.HasProperty("_GroundColor")) sky.SetColor("_GroundColor", new Color(0.42f, 0.24f, 0.15f));
            if (sky.HasProperty("_Exposure")) sky.SetFloat("_Exposure", 1.15f);
        }

        private void CreateProbes(ShipModel ship)
        {
            var list = new System.Collections.Generic.List<ReflectionProbe>();
            if (ship != null)
            {
                // Exterior de la base.
                list.Add(Probe("Sonda de reflejos exterior", ship.transform.TransformPoint(new Vector3(0f, 12f, 0f)), new Vector3(70f, 60f, 70f), false, 128));
                // Una por nivel, con proyección de caja (reflejos coherentes con las paredes).
                for (int i = 0; i < ShipModel.LevelCount; i++)
                {
                    Vector3 c = ship.transform.TransformPoint(new Vector3(0f, ShipModel.LevelFloorY(i) + 1.6f, 0f));
                    float d = ShipModel.InnerRadius * 2f;
                    list.Add(Probe("Sonda de reflejos nivel " + (i + 1), c, new Vector3(d, ShipModel.LevelHeight, d), true, 64));
                }
            }
            _probes = list.ToArray();
            StartCoroutine(RenderProbesSoon());
        }

        private ReflectionProbe Probe(string name, Vector3 position, Vector3 size, bool boxProjection, int resolution)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = position;
            var p = go.AddComponent<ReflectionProbe>();
            p.mode = ReflectionProbeMode.Realtime;
            p.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            p.timeSlicingMode = ReflectionProbeTimeSlicingMode.IndividualFaces;
            p.size = size;
            p.boxProjection = boxProjection;
            p.resolution = resolution;
            p.hdr = true;
            p.intensity = 1f;
            p.nearClipPlane = 0.1f;
            p.farClipPlane = boxProjection ? 12f : 400f;
            return p;
        }

        /// <summary>Espera a que el terreno y la nave estén montados y renderiza las sondas una vez.</summary>
        private IEnumerator RenderProbesSoon()
        {
            for (int i = 0; i < 20; i++) yield return null;
            Refresh();
        }

        /// <summary>Vuelve a renderizar las sondas (p. ej. tras cambios grandes de luz).</summary>
        public void Refresh()
        {
            foreach (ReflectionProbe p in _probes)
                if (p != null) p.RenderProbe();
        }
    }
}
