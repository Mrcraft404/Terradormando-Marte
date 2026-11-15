using MarsRobot.Robot;
using MarsRobot.Robot.Visual;
using UnityEngine;

namespace MarsRobot.UI
{
    /// <summary>
    /// "Robot como personaje": una copia del modelo 3D del rover en un estudio aislado (muy por
    /// debajo del terreno, fuera del alcance de las demás cámaras) con su propia cámara y luces,
    /// renderizada a una textura que el panel de gestión dibuja. Gira sola y se puede girar
    /// arrastrando con el ratón. Solo se renderiza mientras el panel está abierto (ahorra GPU/RAM).
    /// Refleja los módulos instalados (p. ej. la cubierta solar).
    /// </summary>
    public sealed class RobotPreviewRenderer : MonoBehaviour
    {
        private static readonly Vector3 StudioPosition = new Vector3(0f, -5000f, 0f);

        private Camera _cam;
        private RenderTexture _rt;
        private Transform _turntable;
        private RoverVisualRig _rig;
        private RobotModuleSystem _modules;
        private float _yaw = 200f;
        private bool _active;

        public RenderTexture Texture
        {
            get { return _rt; }
        }

        /// <summary>Crea el estudio. <paramref name="robot"/> es el rover real (para leer sus módulos).</summary>
        public void Setup(GameObject robot)
        {
            _modules = robot != null ? robot.GetComponent<RobotModuleSystem>() : null;

            var studio = new GameObject("Estudio de vista previa del robot").transform;
            studio.position = StudioPosition;
            _turntable = new GameObject("Plataforma giratoria").transform;
            _turntable.SetParent(studio, false);
            _rig = RoverModelBuilder.Build(_turntable);

            // Peana holográfica.
            GameObject pedestal = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Destroy(pedestal.GetComponent<Collider>());
            pedestal.transform.SetParent(studio, false);
            pedestal.transform.localPosition = new Vector3(0f, -0.03f, 0f);
            pedestal.transform.localScale = new Vector3(4.2f, 0.03f, 4.2f);
            Shader sh = Shader.Find("Universal Render Pipeline/Lit");
            var mat = new Material(sh != null ? sh : Shader.Find("Standard")) { color = new Color(0.1f, 0.3f, 0.4f) };
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.05f, 0.4f, 0.6f));
            pedestal.GetComponent<Renderer>().sharedMaterial = mat;

            // Iluminación de estudio: principal cálida + contraluz cian.
            AddLight(studio, new Vector3(3f, 4f, 3f), new Color(1f, 0.95f, 0.85f), 2.2f);
            AddLight(studio, new Vector3(-3f, 2.5f, -3f), new Color(0.3f, 0.8f, 1f), 1.6f);

            _rt = new RenderTexture(768, 768, 24, RenderTextureFormat.ARGB32) { name = "Vista previa del robot", antiAliasing = 2 };
            var camGo = new GameObject("Cámara de vista previa");
            camGo.transform.SetParent(studio, false);
            camGo.transform.localPosition = new Vector3(0f, 1.9f, 6.2f);
            camGo.transform.LookAt(studio.position + Vector3.up * 1.0f);
            _cam = camGo.AddComponent<Camera>();
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
            _cam.fieldOfView = 32f;
            _cam.nearClipPlane = 0.1f;
            _cam.farClipPlane = 30f;
            _cam.targetTexture = _rt;
            _cam.enabled = false;
        }

        private static void AddLight(Transform parent, Vector3 pos, Color c, float intensity)
        {
            var go = new GameObject("Luz de estudio");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 12f;
            l.color = c;
            l.intensity = intensity;
        }

        /// <summary>Activa o desactiva el render (lo llama el panel al abrirse y cerrarse).</summary>
        public void SetActive(bool active)
        {
            _active = active;
            if (_cam != null) _cam.enabled = active;
            if (active && _rig != null && _rig.solarDeck != null)
            {
                _rig.solarDeck.SetActive(_modules != null && _modules.HasModule(RoverModuleType.ExtraSolarPanels));
            }
        }

        /// <summary>Girar arrastrando (grados).</summary>
        public void Rotate(float deltaYaw)
        {
            _yaw += deltaYaw;
        }

        private void Update()
        {
            if (!_active || _turntable == null) return;
            _yaw += 12f * Time.unscaledDeltaTime; // giro lento de exposición
            _turntable.localRotation = Quaternion.Euler(0f, _yaw, 0f);
        }

        private void OnDestroy()
        {
            if (_rt != null) _rt.Release();
        }
    }
}
