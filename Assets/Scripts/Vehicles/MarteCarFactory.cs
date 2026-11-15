using UnityEngine;

namespace MarsRobot.Vehicles
{
    /// <summary>
    /// Construye por código el MarteCar siguiendo la guía de diseño:
    ///  • Objeto "MarteCar" con Box Collider y Rigidbody de 500 kg, sin resistencia del aire
    ///    (Drag 0,01) y detección de colisiones Continuous.
    ///  • Ruedas "LeftWheel" y "RightWheel" (más dos traseras) solo VISUALES, sin Rigidbody:
    ///    una rueda con Rigidbody propio y sin unión (joint) se separaría de la caja y caería.
    ///  • CarController (W/S acelerar, A/D girar) desactivado hasta que el robot se suba.
    ///  • VehicleSeat: el robot sube y baja con F, con cámara de persecución.
    /// El aspecto es PROVISIONAL (formas básicas), como el robot y la base.
    /// </summary>
    public static class MarteCarFactory
    {
        public static CarController Create(Vector3 groundPosition, Quaternion rotation)
        {
            var root = new GameObject("MarteCar");
            root.transform.SetPositionAndRotation(groundPosition + Vector3.up * 0.3f, rotation);

            // --- Física (Paso 1 de la guía) ---
            var rb = root.AddComponent<Rigidbody>();
            rb.mass = 500f;
#if UNITY_6000_0_OR_NEWER
            rb.linearDamping = 0.01f;
            rb.angularDamping = 0.05f;
#else
            rb.drag = 0.01f;
            rb.angularDrag = 0.05f;
#endif
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.centerOfMass = new Vector3(0f, 0.3f, 0f); // bajo: vuelca menos en las pendientes

            var box = root.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.75f, 0f);
            box.size = new Vector3(2.2f, 0.9f, 3.8f);
            box.material = CreateLowFriction(); // la caja "rueda" deslizando: sin esto apenas avanzaría

            // --- Aspecto provisional ---
            Material paint = Mat(new Color(0.85f, 0.35f, 0.12f));
            Material dark = Mat(new Color(0.12f, 0.12f, 0.13f));
            Material glass = Mat(new Color(0.25f, 0.45f, 0.55f));
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.8f, 0f), new Vector3(2.2f, 0.6f, 3.8f), paint);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.35f, -0.3f), new Vector3(1.8f, 0.6f, 1.8f), glass); // cabina
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 1.15f, 1.5f), new Vector3(2f, 0.15f, 0.6f), dark);    // capó

            Quaternion wheelRot = Quaternion.Euler(0f, 0f, 90f);
            GameObject leftFront = Wheel(root.transform, "LeftWheel", new Vector3(-1.2f, 0.45f, 1.3f), wheelRot, dark);
            GameObject rightFront = Wheel(root.transform, "RightWheel", new Vector3(1.2f, 0.45f, 1.3f), wheelRot, dark);
            GameObject leftRear = Wheel(root.transform, "LeftWheelRear", new Vector3(-1.2f, 0.45f, -1.3f), wheelRot, dark);
            GameObject rightRear = Wheel(root.transform, "RightWheelRear", new Vector3(1.2f, 0.45f, -1.3f), wheelRot, dark);

            // --- Control (Paso 2 de la guía) ---
            var car = root.AddComponent<CarController>();
            car.carBody = rb;
            car.leftWheel = leftFront;
            car.rightWheel = rightFront;
            car.extraWheels = new[] { leftRear, rightRear };
            car.wheelSpinAxis = Vector3.up; // el cilindro está tumbado: su eje de giro es su Y local
            car.speed = 15f;                // 54 km/h: suficiente para el terreno marciano
            car.turnSpeed = 45f;
            car.enabled = false;            // solo responde al teclado cuando el robot va dentro

            // --- Cámara del coche (apagada hasta subir) ---
            var camGo = new GameObject("CamaraCoche");
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 1500f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = RenderSettings.fogColor;
            var listener = camGo.AddComponent<AudioListener>();
            cam.enabled = false;
            listener.enabled = false;

            var seat = root.AddComponent<VehicleSeat>();
            seat.Setup(car, cam, listener);
            return car;
        }

        private static GameObject Wheel(Transform parent, string name, Vector3 pos, Quaternion rot, Material mat)
        {
            Transform t = Part(PrimitiveType.Cylinder, parent, pos, new Vector3(0.9f, 0.18f, 0.9f), mat);
            t.localRotation = rot;
            t.name = name;
            return t.gameObject;
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>()); // la única colisión es la Box Collider del coche
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material Mat(Color color)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            m.color = color;
            return m;
        }

#if UNITY_6000_0_OR_NEWER
        private static PhysicsMaterial CreateLowFriction()
        {
            var m = new PhysicsMaterial("MarteCarDeslizante");
            m.frictionCombine = PhysicsMaterialCombine.Minimum;
#else
        private static PhysicMaterial CreateLowFriction()
        {
            var m = new PhysicMaterial("MarteCarDeslizante");
            m.frictionCombine = PhysicMaterialCombine.Minimum;
#endif
            m.dynamicFriction = 0.05f;
            m.staticFriction = 0.05f;
            return m;
        }
    }
}
