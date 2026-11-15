using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Vehicles
{
    /// <summary>
    /// Asiento del vehículo: el robot sube y baja con F.
    ///  • Al subir: el robot "entra" (se oculta), se activa el CarController, se enciende la
    ///    cámara de persecución y el terreno empieza a cargarse alrededor del coche.
    ///  • Al bajar: el robot aparece al lado izquierdo del coche, en el suelo.
    ///  • Aparcado (sin robot): freno de mano para que no se deslice por las pendientes.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class VehicleSeat : MonoBehaviour
    {
        [Tooltip("Distancia (m) desde la que el robot puede subir.")]
        [SerializeField, Min(1f)] private float boardRadius = 4f;

        [Header("Cámara de persecución")]
        [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 3.2f, -8f);
        [SerializeField, Min(0.1f)] private float cameraSmoothing = 6f;

        private CarController _car;
        private Rigidbody _rb;
        private Camera _camera;
        private AudioListener _listener;
        private RobotController _robot;
        private GUIStyle _style;

        /// <summary>True mientras el robot va conduciendo.</summary>
        public bool IsOccupied { get; private set; }

        public void Setup(CarController car, Camera cam, AudioListener listener)
        {
            _car = car;
            _rb = car.carBody;
            _camera = cam;
            _listener = listener;
        }

        private void Update()
        {
            if (_robot == null && !GameServices.TryGet(out _robot)) return;

            if (IsOccupied)
            {
                if (RobotInput.InteractPressed) Exit();
                return;
            }

            if (RobotInput.InteractPressed && RobotIsClose() && !RobotIsUsingStation()) Enter();
        }

        private void FixedUpdate()
        {
            if (IsOccupied || _rb == null) return;
            // Freno de mano: anula el movimiento horizontal; la gravedad sigue actuando.
            Vector3 v = GetVelocity();
            SetVelocity(new Vector3(0f, Mathf.Min(v.y, 0f), 0f));
            _rb.angularVelocity = Vector3.zero;
        }

        private void LateUpdate()
        {
            if (!IsOccupied || _camera == null) return;

            // Cámara detrás del coche, siguiendo solo su giro horizontal (no se marea en baches).
            Quaternion yaw = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Vector3 desired = transform.position + yaw * cameraOffset;
            float t = 1f - Mathf.Exp(-cameraSmoothing * Time.deltaTime);
            Transform cam = _camera.transform;
            cam.position = Vector3.Lerp(cam.position, desired, t);
            cam.rotation = Quaternion.LookRotation(transform.position + Vector3.up * 1.2f - cam.position, Vector3.up);
        }

        private bool RobotIsClose()
        {
            if (!_robot.gameObject.activeInHierarchy) return false;
            Vector3 d = _robot.transform.position - transform.position;
            d.y = 0f;
            return d.magnitude <= boardRadius;
        }

        private bool RobotIsUsingStation()
        {
            var link = _robot.GetComponent<RobotBaseLink>();
            return link != null && link.OpenStation != null;
        }

        private void Enter()
        {
            IsOccupied = true;
            _robot.gameObject.SetActive(false); // el robot va dentro (su cámara se apaga con él)
            _car.enabled = true;

            Transform cam = _camera.transform;
            cam.position = transform.position + Quaternion.Euler(0f, transform.eulerAngles.y, 0f) * cameraOffset;
            _camera.enabled = true;
            _listener.enabled = true;

            TerrainManager terrain;
            if (GameServices.TryGet(out terrain)) terrain.SetTarget(transform);
        }

        private void Exit()
        {
            // Bajar por el lado izquierdo, sobre el terreno.
            Vector3 exit = transform.position - transform.right * 2.8f;
            TerrainManager terrain;
            bool hasTerrain = GameServices.TryGet(out terrain);
            if (hasTerrain) exit.y = terrain.GetHeightAt(exit) + 0.3f;
            else exit.y += 0.5f;

            IsOccupied = false;
            _car.enabled = false;
            _camera.enabled = false;
            _listener.enabled = false;

            Transform robot = _robot.transform;
            robot.SetPositionAndRotation(exit, Quaternion.Euler(0f, transform.eulerAngles.y, 0f));
            var robotBody = _robot.GetComponent<Rigidbody>();
            _robot.gameObject.SetActive(true);
            if (robotBody != null)
            {
#if UNITY_6000_0_OR_NEWER
                robotBody.linearVelocity = Vector3.zero;
#else
                robotBody.velocity = Vector3.zero;
#endif
            }
            if (hasTerrain) terrain.SetTarget(robot);
        }

        private void OnGUI()
        {
            if (_robot == null) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, richText = true };
            }

            string text = null;
            if (IsOccupied)
            {
                float kmh = Mathf.Abs(_car.ForwardSpeed) * 3.6f;
                text = "MarteCar  " + kmh.ToString("0") + " km/h   ·   W/S acelerar · A/D girar · F bajar";
            }
            else if (RobotIsClose() && !RobotIsUsingStation())
            {
                text = "F: subir al MarteCar";
            }
            if (text != null)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 260, Screen.height - 140, 520, 40), text, _style);
            }
        }

        private Vector3 GetVelocity()
        {
#if UNITY_6000_0_OR_NEWER
            return _rb.linearVelocity;
#else
            return _rb.velocity;
#endif
        }

        private void SetVelocity(Vector3 v)
        {
#if UNITY_6000_0_OR_NEWER
            _rb.linearVelocity = v;
#else
            _rb.velocity = v;
#endif
        }
    }
}
