using MarsRobot.Crafting;
using MarsRobot.Items;
using UnityEngine;

namespace MarsRobot.Crew
{
    /// <summary>
    /// El astronauta (el jugador) dentro de la nave nodriza, con su traje espacial. Camina por los
    /// niveles con gravedad marciana, fabrica en las estaciones (F), lleva su propio inventario (I)
    /// y teleopera el robot: con el robot en el hangar, P cambia de uno a otro (ver CrewControl).
    ///
    /// Controles: WASD caminar · Mayús correr · Espacio saltar · ratón mirar (clic para capturarlo,
    /// Esc para soltarlo) · V 1ª/3ª persona · F usar · I inventario y fabricación a mano.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class Astronaut : MonoBehaviour
    {
        public const float MarsGravity = 3.71f;

        private CharacterController _cc;
        private Transform _head;
        private Camera _cam;
        private AudioListener _listener;
        private AstronautRig _rig;
        private float _pitch;
        private float _vy;
        private bool _thirdPerson;
        private float _walkPhase;
        private ICrewInteractable _target;
        private GUIStyle _promptStyle;

        public ItemContainer Inventory { get; private set; }
        public CraftingQueue HandQueue { get; private set; }

        /// <summary>True mientras el jugador controla al astronauta.</summary>
        public bool Active { get; private set; }

        public Camera Camera
        {
            get { return _cam; }
        }

        public static Astronaut Create(Vector3 position, Quaternion rotation)
        {
            var go = new GameObject("Astronauta");
            go.transform.SetPositionAndRotation(position, rotation);
            var cc = go.AddComponent<CharacterController>();
            cc.height = 1.8f;
            cc.radius = 0.33f;
            cc.center = new Vector3(0f, 0.9f, 0f);
            cc.stepOffset = 0.35f;
            cc.slopeLimit = 50f;
            var astronaut = go.AddComponent<Astronaut>();
            go.AddComponent<AstronautLifeSupport>(); // oxígeno del traje y aire de los módulos
            return astronaut;
        }

        private void Awake()
        {
            _cc = GetComponent<CharacterController>();
            Inventory = new ItemContainer(36, 90f, 0f);   // traje + mochila: 90 kg (gravedad marciana)
            HandQueue = new CraftingQueue();
            _rig = AstronautModel.Build(transform);

            _head = new GameObject("Cabeza").transform;
            _head.SetParent(transform, false);
            _head.localPosition = new Vector3(0f, 1.62f, 0f);
            var camGo = new GameObject("Cámara del astronauta");
            camGo.transform.SetParent(_head, false);
            _cam = camGo.AddComponent<Camera>();
            _cam.nearClipPlane = 0.05f;
            _cam.farClipPlane = 1500f;
            _cam.fieldOfView = 70f;
            _cam.clearFlags = CameraClearFlags.SolidColor;
            _cam.backgroundColor = RenderSettings.fogColor;
            camGo.tag = "MainCamera";
            _listener = camGo.AddComponent<AudioListener>();
            SetActive(false);
        }

        /// <summary>Da o quita el control al jugador (cámara, oyente de audio y entrada).</summary>
        public void SetActive(bool active)
        {
            Active = active;
            _cam.enabled = active;
            _listener.enabled = active;
            UpdateCameraMode();
            if (!active) _target = null;
        }

        /// <summary>Cámara en 1ª o 3ª persona (tecla V).</summary>
        public void SetThirdPerson(bool third)
        {
            _thirdPerson = third;
            UpdateCameraMode();
        }

        private bool UiOpen
        {
            get { return CraftingWindow.IsOpen || ShipLift.MenuOpen || Time.timeScale < 0.01f; }
        }

        private void Update()
        {
            HandQueue.Tick(Time.deltaTime, Inventory);
            if (!Active)
            {
                Animate(0f);
                return;
            }

            if (UiOpen)
            {
                Gravity(Vector3.zero);
                Animate(0f);
                return;
            }

            // Ratón: clic captura, Esc suelta.
            if (Input.GetKeyDown(KeyCode.Escape)) Cursor.lockState = CursorLockMode.None;
            if (Input.GetMouseButtonDown(0) && Cursor.lockState != CursorLockMode.Locked) Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = Cursor.lockState != CursorLockMode.Locked;

            if (Cursor.lockState == CursorLockMode.Locked)
            {
                float sens = MarsRobot.Core.GameSettings.MouseSensitivity * 1.2f;
                transform.Rotate(0f, Input.GetAxis("Mouse X") * sens, 0f);
                _pitch = Mathf.Clamp(_pitch - Input.GetAxis("Mouse Y") * sens, -80f, 80f);
            }
            if (Input.GetKeyDown(KeyCode.V))
            {
                _thirdPerson = !_thirdPerson;
                UpdateCameraMode();
            }

            var input = new Vector3((Input.GetKey(KeyCode.D) ? 1f : 0f) - (Input.GetKey(KeyCode.A) ? 1f : 0f), 0f,
                (Input.GetKey(KeyCode.W) ? 1f : 0f) - (Input.GetKey(KeyCode.S) ? 1f : 0f));
            input = Vector3.ClampMagnitude(input, 1f);
            float speed = Input.GetKey(KeyCode.LeftShift) ? 3.4f : 1.9f;
            Vector3 move = transform.TransformDirection(input) * speed;
            if (_cc.isGrounded && Input.GetKeyDown(KeyCode.Space)) _vy = 2.2f; // salto lento de Marte
            Gravity(move);
            Animate(input.magnitude * speed);

            // Interacción.
            _target = CrewInteractables.FindBest(transform.position + Vector3.up * 1f, transform.forward);
            if (_target != null && Input.GetKeyDown(KeyCode.F)) _target.CrewInteract(this);
            if (Input.GetKeyDown(KeyCode.I)) CraftingWindow.OpenPersonal(this);
        }

        private void LateUpdate()
        {
            _head.localRotation = Quaternion.Euler(_pitch, 0f, 0f);
            if (!_thirdPerson || !Active) return;
            // 3ª persona: cámara detrás, sin atravesar paredes.
            Vector3 pivot = _head.position;
            Vector3 back = -_head.forward;
            float dist = 2.4f;
            RaycastHit hit;
            if (Physics.SphereCast(pivot, 0.2f, back, out hit, dist, ~0, QueryTriggerInteraction.Ignore)) dist = Mathf.Max(0.3f, hit.distance - 0.05f);
            _cam.transform.position = pivot + back * dist + Vector3.up * 0.15f;
            _cam.transform.rotation = Quaternion.LookRotation(pivot - _cam.transform.position + _head.forward * 2f);
        }

        private void Gravity(Vector3 horizontal)
        {
            if (_cc.isGrounded && _vy < 0f) _vy = -0.5f;
            _vy -= MarsGravity * Time.deltaTime;
            _cc.Move((horizontal + Vector3.up * _vy) * Time.deltaTime);
        }

        private void UpdateCameraMode()
        {
            if (_cam == null) return;
            if (!_thirdPerson)
            {
                _cam.transform.localPosition = new Vector3(0f, 0.02f, 0.18f); // dentro del casco, delante del visor
                _cam.transform.localRotation = Quaternion.identity;
            }
            // En 1ª persona no se dibuja el propio traje (salvo cuando no lo controlamos: lo ve el robot).
            bool showBody = !Active || _thirdPerson;
            if (_rig != null)
                foreach (Renderer r in _rig.Renderers) r.enabled = showBody;
        }

        private void Animate(float speed)
        {
            if (_rig == null) return;
            _walkPhase += Time.deltaTime * (2.5f + speed * 2.2f);
            float swing = Mathf.Clamp01(speed / 2f) * 28f * Mathf.Sin(_walkPhase);
            _rig.LegL.localRotation = Quaternion.Euler(swing, 0f, 0f);
            _rig.LegR.localRotation = Quaternion.Euler(-swing, 0f, 0f);
            _rig.ArmL.localRotation = Quaternion.Euler(-swing * 0.8f, 0f, 6f);
            _rig.ArmR.localRotation = Quaternion.Euler(swing * 0.8f, 0f, -6f);
        }

        /// <summary>Teletransporta (montacargas): el CharacterController debe apagarse para moverlo.</summary>
        public void Teleport(Vector3 position, Quaternion rotation)
        {
            _cc.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _cc.enabled = true;
            _vy = 0f;
        }

        private void OnGUI()
        {
            if (!Active || UiOpen) return;
            if (_promptStyle == null)
            {
                _promptStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, richText = true };
                _promptStyle.normal.textColor = new Color(0.75f, 0.95f, 1f);
            }
            // Punto de mira.
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            GUI.DrawTexture(new Rect(Screen.width * 0.5f - 2, Screen.height * 0.5f - 2, 4, 4), Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (_target != null)
                GUI.Box(new Rect(Screen.width * 0.5f - 230, Screen.height - 150, 460, 40), _target.CrewPrompt, _promptStyle);
            GUI.Label(new Rect(Screen.width * 0.5f - 300, Screen.height - 104, 600, 22),
                Tr.T("<color=#9fe8ff>WASD</color> caminar · <color=#9fe8ff>Mayús</color> correr · <color=#9fe8ff>F</color> usar · <color=#9fe8ff>I</color> inventario · <color=#9fe8ff>V</color> cámara · <color=#9fe8ff>Esc</color> soltar ratón"),
                new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, richText = true, fontSize = 13 });
        }
    }

    /// <summary>Piezas animadas del modelo del astronauta.</summary>
    public sealed class AstronautRig
    {
        public Transform LegL, LegR, ArmL, ArmR;
        public Renderer[] Renderers;
    }

    /// <summary>Astronauta con traje de superficie: casco con visor dorado, mochila PLSS y franjas naranjas.</summary>
    public static class AstronautModel
    {
        private static Material _suit, _suitDark, _visor, _orange, _boots, _screen;

        public static AstronautRig Build(Transform parent)
        {
            EnsureMaterials();
            var root = new GameObject("Traje espacial").transform;
            root.SetParent(parent, false);
            var rig = new AstronautRig();

            // Torso, cintura y mochila de soporte vital.
            P(PrimitiveType.Capsule, root, new Vector3(0f, 1.24f, 0f), new Vector3(0.5f, 0.38f, 0.34f), Vector3.zero, _suit);
            P(PrimitiveType.Cylinder, root, new Vector3(0f, 0.98f, 0f), new Vector3(0.42f, 0.06f, 0.3f), Vector3.zero, _suitDark);
            P(PrimitiveType.Cube, root, new Vector3(0f, 1.3f, -0.26f), new Vector3(0.44f, 0.58f, 0.24f), Vector3.zero, _suit);
            P(PrimitiveType.Cube, root, new Vector3(0f, 1.3f, -0.39f), new Vector3(0.36f, 0.48f, 0.03f), Vector3.zero, _suitDark);
            P(PrimitiveType.Cube, root, new Vector3(0f, 1.3f, 0.17f), new Vector3(0.26f, 0.16f, 0.05f), Vector3.zero, _suitDark); // módulo de pecho
            P(PrimitiveType.Cube, root, new Vector3(0.06f, 1.32f, 0.2f), new Vector3(0.08f, 0.05f, 0.01f), Vector3.zero, _screen);
            P(PrimitiveType.Cube, root, new Vector3(-0.15f, 1.42f, 0.16f), new Vector3(0.1f, 0.06f, 0.02f), Vector3.zero, _orange); // parche

            // Casco con visor dorado y anillo del cuello.
            P(PrimitiveType.Cylinder, root, new Vector3(0f, 1.5f, 0f), new Vector3(0.26f, 0.04f, 0.26f), Vector3.zero, _suitDark);
            P(PrimitiveType.Sphere, root, new Vector3(0f, 1.66f, 0.01f), new Vector3(0.34f, 0.35f, 0.34f), Vector3.zero, _suit);
            P(PrimitiveType.Sphere, root, new Vector3(0f, 1.67f, 0.1f), new Vector3(0.27f, 0.22f, 0.18f), Vector3.zero, _visor);
            P(PrimitiveType.Cube, root, new Vector3(0f, 1.82f, 0.02f), new Vector3(0.05f, 0.03f, 0.2f), Vector3.zero, _orange); // lámpara

            // Piernas (con pivote en la cadera) y brazos (pivote en el hombro).
            rig.LegL = Limb(root, new Vector3(-0.12f, 0.95f, 0f), true);
            rig.LegR = Limb(root, new Vector3(0.12f, 0.95f, 0f), true);
            rig.ArmL = Limb(root, new Vector3(-0.31f, 1.43f, 0f), false);
            rig.ArmR = Limb(root, new Vector3(0.31f, 1.43f, 0f), false);

            rig.Renderers = root.GetComponentsInChildren<Renderer>();
            return rig;
        }

        private static Transform Limb(Transform root, Vector3 pivotPos, bool leg)
        {
            var pivot = new GameObject(leg ? "Pierna" : "Brazo").transform;
            pivot.SetParent(root, false);
            pivot.localPosition = pivotPos;
            if (leg)
            {
                P(PrimitiveType.Capsule, pivot, new Vector3(0f, -0.24f, 0f), new Vector3(0.17f, 0.24f, 0.17f), Vector3.zero, _suit);
                P(PrimitiveType.Sphere, pivot, new Vector3(0f, -0.47f, 0.03f), new Vector3(0.15f, 0.12f, 0.12f), Vector3.zero, _orange); // rodillera
                P(PrimitiveType.Capsule, pivot, new Vector3(0f, -0.68f, 0f), new Vector3(0.15f, 0.22f, 0.15f), Vector3.zero, _suit);
                P(PrimitiveType.Cube, pivot, new Vector3(0f, -0.9f, 0.04f), new Vector3(0.15f, 0.11f, 0.28f), Vector3.zero, _boots);
            }
            else
            {
                P(PrimitiveType.Capsule, pivot, new Vector3(0f, -0.18f, 0f), new Vector3(0.13f, 0.18f, 0.13f), Vector3.zero, _suit);
                P(PrimitiveType.Cylinder, pivot, new Vector3(0f, -0.2f, 0f), new Vector3(0.135f, 0.02f, 0.135f), Vector3.zero, _orange); // franja
                P(PrimitiveType.Capsule, pivot, new Vector3(0f, -0.48f, 0.02f), new Vector3(0.12f, 0.16f, 0.12f), Vector3.zero, _suit);
                P(PrimitiveType.Sphere, pivot, new Vector3(0f, -0.66f, 0.03f), new Vector3(0.11f, 0.12f, 0.1f), Vector3.zero, _suitDark); // guante
            }
            return pivot;
        }

        private static void P(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        private static void EnsureMaterials()
        {
            if (_suit != null) return;
            _suit = Mat(new Color(0.93f, 0.93f, 0.9f), 0f, 0.35f);
            _suitDark = Mat(new Color(0.38f, 0.4f, 0.43f), 0.4f, 0.4f);
            _visor = Mat(new Color(0.95f, 0.68f, 0.22f), 0.3f, 0.9f, new Color(0.25f, 0.15f, 0.02f)); // dorado (sin sondas de reflejos el metal puro sale negro)
            _orange = Mat(new Color(0.95f, 0.45f, 0.1f), 0.1f, 0.4f);
            _boots = Mat(new Color(0.22f, 0.22f, 0.24f), 0.1f, 0.25f);
            _screen = Mat(new Color(0.2f, 0.9f, 1f), 0f, 0.8f, new Color(0.2f, 0.9f, 1.2f));
        }

        private static Material Mat(Color c, float metallic, float smooth, Color emission = default(Color))
        {
            Shader shader = Shader.Find("Standard");
            var m = new Material(shader) { color = c };
            m.SetFloat("_Metallic", metallic);
            m.SetFloat("_Glossiness", smooth);
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }
    }
}
