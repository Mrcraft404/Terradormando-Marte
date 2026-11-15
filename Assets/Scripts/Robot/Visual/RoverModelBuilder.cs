using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Robot.Visual
{
    /// <summary>Referencias a las piezas móviles del modelo (las anima RoverVisualAnimator).</summary>
    public sealed class RoverVisualRig
    {
        public Transform root;
        public Transform[] wheelSteer = new Transform[6]; // giro de dirección (eje Y)
        public Transform[] wheelSpin = new Transform[6];  // rodadura (eje X)
        public float wheelRadius;
        public Transform mastHead;        // cabeza del mástil (cabeceo)
        public Transform cameraMount;     // donde va el pivote de la cámara en 1ª persona
        public Transform armShoulder;     // brazo robótico: hombro (giro Y)
        public Transform armUpper;        // hombro (cabeceo)
        public Transform armElbow;        // codo
        public Transform armWrist;        // muñeca / torreta
        public Transform highGainAntenna; // antena de alta ganancia (gira despacio)
        public GameObject solarDeck;      // cubierta solar grande (módulo Paneles solares extra)
        public Renderer[] headRenderers = new Renderer[0];
    }

    /// <summary>
    /// Construye por código un rover explorador realista según el diseño del usuario
    /// (estilo rover de seis ruedas de la NASA, ~3 m × 2,7 m × 2,2 m):
    ///  • Suspensión rocker-bogie con seis ruedas de aluminio con garras (grousers) y radios curvos.
    ///  • Cuerpo con paneles remachados, mástil con cabeza de cámaras y lente grande, generador
    ///    RTG con aletas en la popa, antena de alta ganancia y antena UHF.
    ///  • Brazo robótico de 5 articulaciones con torreta (taladro y cámaras).
    ///  • Cubierta solar grande opcional (se ve con el módulo "Paneles solares extra").
    /// Las piezas estáticas se fusionan en pocas mallas por material (menos objetos y draw calls).
    /// Es un modelo procedural: si más adelante hay un .fbx del rover, basta con sustituir Build().
    /// Ejes: +Z delante, +X derecha, Y = 0 en el suelo.
    /// </summary>
    public static class RoverModelBuilder
    {
        // Medidas principales (m).
        private const float WheelRadius = 0.26f;
        private const float WheelWidth = 0.4f;
        private const float TrackHalf = 1.15f;
        private static readonly float[] WheelZ = { 1.05f, 0.0f, -1.0f };

        private static Material _body, _bodyDark, _metal, _darkMetal, _gold, _wheel, _rim, _lens, _solar, _rtg, _copper, _blue;

        public static RoverVisualRig Build(Transform parent)
        {
            EnsureMaterials();
            var rig = new RoverVisualRig { wheelRadius = WheelRadius };
            Transform root = new GameObject("Modelo del rover").transform;
            root.SetParent(parent, false);
            rig.root = root;

            Transform chassis = Group(root, "Chasis (estático)");
            BuildBody(chassis);
            BuildSuspension(chassis);
            BuildDeckInstruments(chassis);
            BuildRtg(chassis);

            for (int side = 0; side < 2; side++)
            {
                for (int i = 0; i < 3; i++)
                {
                    int idx = side * 3 + i;
                    float x = side == 0 ? -TrackHalf : TrackHalf;
                    BuildWheel(root, rig, idx, new Vector3(x, WheelRadius, WheelZ[i]), side == 0);
                }
            }

            BuildMast(root, rig);
            BuildArm(root, rig);
            BuildHighGainAntenna(root, rig);
            BuildSolarDeck(root, rig);

            Combine(chassis);
            return rig;
        }

        // ------------------------------------------------------------------
        // Partes
        // ------------------------------------------------------------------

        private static void BuildBody(Transform p)
        {
            // Caja electrónica principal (WEB) con paneles remachados.
            Box(p, new Vector3(0f, 0.95f, -0.1f), new Vector3(1.6f, 0.55f, 2.0f), _body);
            Box(p, new Vector3(0f, 1.235f, -0.1f), new Vector3(1.66f, 0.03f, 2.06f), _bodyDark);   // tapa
            Box(p, new Vector3(0f, 0.66f, -0.1f), new Vector3(1.5f, 0.04f, 1.9f), _darkMetal);      // fondo
            // Paneles laterales en relieve y radiadores.
            for (int s = -1; s <= 1; s += 2)
            {
                for (int i = 0; i < 3; i++)
                {
                    Box(p, new Vector3(s * 0.81f, 0.98f, 0.55f - i * 0.65f), new Vector3(0.03f, 0.38f, 0.5f), _body);
                }
                Box(p, new Vector3(s * 0.83f, 1.02f, -0.75f), new Vector3(0.02f, 0.28f, 0.42f), _gold); // aislante
                Box(p, new Vector3(s * 0.84f, 0.84f, 0.2f), new Vector3(0.04f, 0.08f, 1.2f), _darkMetal);  // canaleta de cables
            }
            // Frontal: paneles, cámaras de navegación bajas (hazcams) y soporte del brazo.
            Box(p, new Vector3(0f, 0.95f, 0.92f), new Vector3(1.4f, 0.45f, 0.06f), _body);
            for (int s = -1; s <= 1; s += 2)
            {
                Box(p, new Vector3(s * 0.25f, 0.76f, 0.97f), new Vector3(0.14f, 0.08f, 0.06f), _darkMetal);
                Cyl(p, new Vector3(s * 0.25f, 0.76f, 1.01f), 0.025f, 0.03f, new Vector3(90f, 0f, 0f), _lens);
            }
            // Popa: bloques y caja inclinada hacia el RTG.
            Box(p, new Vector3(0f, 1.05f, -1.12f), new Vector3(1.2f, 0.35f, 0.1f), _body);
            Box(p, new Vector3(0.45f, 1.32f, -0.95f), new Vector3(0.3f, 0.12f, 0.35f), _bodyDark);
            // Cableado de cobre visible.
            Beam(p, new Vector3(0.7f, 0.75f, 0.85f), new Vector3(0.7f, 1.15f, 0.85f), 0.012f, _copper);
            Beam(p, new Vector3(-0.7f, 0.8f, -0.9f), new Vector3(-0.7f, 1.15f, -0.4f), 0.012f, _copper);
        }

        private static void BuildSuspension(Transform p)
        {
            for (int s = -1; s <= 1; s += 2)
            {
                float x = s * TrackHalf;
                Vector3 pivot = new Vector3(s * 0.88f, 0.92f, 0.25f);              // diferencial en el cuerpo
                Vector3 rockerFront = new Vector3(x, 0.78f, WheelZ[0]);            // pata delantera
                Vector3 bogiePivot = new Vector3(x, 0.68f, -0.45f);                // eje del bogie
                Vector3 bogieMid = new Vector3(x, 0.6f, WheelZ[1]);
                Vector3 bogieRear = new Vector3(x, 0.6f, WheelZ[2]);

                Beam(p, pivot, new Vector3(x, 0.9f, 0.25f), 0.05f, _metal);       // tubo hasta el lateral
                Cyl(p, pivot, 0.09f, 0.12f, new Vector3(0f, 0f, 90f), _darkMetal); // rótula
                Beam(p, new Vector3(x, 0.9f, 0.25f), rockerFront, 0.055f, _metal);   // rocker delantero
                Beam(p, new Vector3(x, 0.9f, 0.25f), bogiePivot, 0.055f, _metal);    // rocker trasero
                Cyl(p, bogiePivot, 0.07f, 0.1f, new Vector3(0f, 0f, 90f), _darkMetal);
                Beam(p, bogiePivot, bogieMid, 0.045f, _metal);                      // bogie
                Beam(p, bogiePivot, bogieRear, 0.045f, _metal);
                Beam(p, new Vector3(s * 0.8f, 1.05f, 0.25f), new Vector3(s * 0.8f, 1.05f, -0.2f), 0.03f, _darkMetal); // barra diferencial
            }
        }

        private static void BuildDeckInstruments(Transform p)
        {
            // Instrumentos sobre la cubierta (como en el diseño: torretas, cilindros y sensores).
            Cyl(p, new Vector3(-0.55f, 1.42f, 0.55f), 0.11f, 0.32f, Vector3.zero, _metal);
            Cyl(p, new Vector3(-0.55f, 1.6f, 0.55f), 0.12f, 0.04f, Vector3.zero, _darkMetal);
            Box(p, new Vector3(-0.2f, 1.33f, 0.45f), new Vector3(0.2f, 0.16f, 0.2f), _darkMetal);
            Cyl(p, new Vector3(0.05f, 1.36f, 0.4f), 0.04f, 0.22f, Vector3.zero, _metal);
            Cyl(p, new Vector3(0.18f, 1.34f, 0.35f), 0.03f, 0.18f, Vector3.zero, _darkMetal);
            Box(p, new Vector3(-0.45f, 1.32f, -0.3f), new Vector3(0.36f, 0.14f, 0.3f), _bodyDark);
            Box(p, new Vector3(0.1f, 1.3f, -0.45f), new Vector3(0.28f, 0.1f, 0.34f), _gold);
            Cyl(p, new Vector3(0.62f, 1.45f, -0.55f), 0.07f, 0.38f, Vector3.zero, _darkMetal); // antena UHF
            Cyl(p, new Vector3(0.62f, 1.66f, -0.55f), 0.09f, 0.06f, Vector3.zero, _metal);
            // Rampa / entrada de muestras inclinada (detalle amarillo del diseño).
            Transform ramp = Box(p, new Vector3(-0.35f, 1.35f, 0.05f), new Vector3(0.35f, 0.04f, 0.5f), _gold);
            ramp.localRotation = Quaternion.Euler(-25f, 0f, 0f);
        }

        private static void BuildRtg(Transform p)
        {
            // Generador termoeléctrico de radioisótopos (MMRTG) en la popa, inclinado y con aletas.
            Vector3 c = new Vector3(0f, 1.15f, -1.38f);
            Quaternion tilt = Quaternion.Euler(-40f, 0f, 0f);
            Transform core = Cyl(p, c, 0.2f, 0.75f, new Vector3(-40f, 0f, 0f), _rtg);
            for (int i = 0; i < 8; i++)
            {
                Quaternion around = tilt * Quaternion.Euler(0f, i * 45f, 0f);
                Transform fin = Box(p, c + around * new Vector3(0f, 0f, 0.27f), new Vector3(0.02f, 0.72f, 0.16f), _rtg);
                fin.localRotation = around;
            }
            Beam(p, new Vector3(-0.25f, 1.05f, -1.15f), c + tilt * new Vector3(-0.12f, -0.2f, 0f), 0.025f, _darkMetal);
            Beam(p, new Vector3(0.25f, 1.05f, -1.15f), c + tilt * new Vector3(0.12f, -0.2f, 0f), 0.025f, _darkMetal);
            core.name = "RTG";
        }

        private static void BuildWheel(Transform root, RoverVisualRig rig, int idx, Vector3 pos, bool left)
        {
            float s = left ? -1f : 1f;
            // Pata vertical con actuador de dirección.
            Transform steer = Group(root, "Rueda " + (idx + 1) + " (dirección)");
            steer.localPosition = pos;
            Box(steer, new Vector3(0f, 0.47f, 0f), new Vector3(0.12f, 0.1f, 0.12f), _darkMetal);       // actuador
            Cyl(steer, new Vector3(0f, 0.3f, 0f), 0.035f, 0.32f, Vector3.zero, _metal);                 // eje vertical
            Box(steer, new Vector3(-s * 0.08f, 0.12f, 0f), new Vector3(0.05f, 0.18f, 0.1f), _metal);    // horquilla

            Transform spin = Group(steer, "Rueda " + (idx + 1));
            spin.localPosition = new Vector3(s * 0.12f, 0f, 0f);
            Quaternion axle = Quaternion.Euler(0f, 0f, 90f);

            // Llanta: cilindro + aros + garras (grousers) en zigzag + radios curvos + buje dorado.
            Cyl(spin, Vector3.zero, WheelRadius, WheelWidth, axle.eulerAngles, _wheel);
            Cyl(spin, new Vector3(-WheelWidth * 0.48f, 0f, 0f), WheelRadius + 0.01f, 0.02f, axle.eulerAngles, _rim);
            Cyl(spin, new Vector3(WheelWidth * 0.48f, 0f, 0f), WheelRadius + 0.01f, 0.02f, axle.eulerAngles, _rim);
            const int grousers = 20;
            for (int g = 0; g < grousers; g++)
            {
                float a = g * 360f / grousers;
                Quaternion q = Quaternion.Euler(a, 0f, 0f);
                Transform gr = Box(spin, q * new Vector3(0f, WheelRadius + 0.012f, 0f), new Vector3(WheelWidth * 0.95f, 0.025f, 0.035f), _rim);
                gr.localRotation = q * Quaternion.Euler(0f, (g % 2 == 0 ? 8f : -8f), 0f); // patrón en chevrón
            }
            float face = s * (WheelWidth * 0.5f + 0.005f);
            for (int k = 0; k < 6; k++)
            {
                Quaternion q = Quaternion.Euler(k * 60f, 0f, 0f);
                Transform spoke = Box(spin, new Vector3(face, 0f, 0f) + q * new Vector3(0f, WheelRadius * 0.5f, 0.03f),
                    new Vector3(0.012f, WheelRadius * 0.8f, 0.025f), _gold);
                spoke.localRotation = q * Quaternion.Euler(18f, 0f, 0f); // curvado (flexible, como las ruedas reales)
            }
            Cyl(spin, new Vector3(face, 0f, 0f), 0.08f, 0.04f, axle.eulerAngles, _gold);
            Cyl(spin, new Vector3(face + s * 0.02f, 0f, 0f), 0.04f, 0.03f, axle.eulerAngles, _darkMetal);

            Combine(spin);
            rig.wheelSteer[idx] = steer;
            rig.wheelSpin[idx] = spin;
        }

        private static void BuildMast(Transform root, RoverVisualRig rig)
        {
            // Mástil en la parte delantera derecha, como en el diseño.
            Vector3 basePos = new Vector3(0.45f, 1.25f, 0.62f);
            Transform mast = Group(root, "Mástil");
            mast.localPosition = basePos;
            Cyl(mast, new Vector3(0f, 0.08f, 0f), 0.11f, 0.16f, Vector3.zero, _darkMetal);       // base
            Cyl(mast, new Vector3(0f, 0.45f, 0f), 0.055f, 0.6f, Vector3.zero, _body);            // columna
            Cyl(mast, new Vector3(0f, 0.3f, 0f), 0.075f, 0.06f, Vector3.zero, _metal);
            Cyl(mast, new Vector3(0f, 0.62f, 0f), 0.075f, 0.06f, Vector3.zero, _metal);
            Box(mast, new Vector3(0.05f, 0.72f, 0f), new Vector3(0.12f, 0.12f, 0.1f), _darkMetal); // actuador de azimut
            Beam(mast, new Vector3(-0.08f, 0.5f, 0f), new Vector3(-0.3f, 0.55f, -0.05f), 0.012f, _metal); // brazo meteorológico
            Box(mast, new Vector3(-0.32f, 0.55f, -0.05f), new Vector3(0.08f, 0.04f, 0.1f), _darkMetal);

            // Cabeza (cabeceo): caja grande con lente principal y pares de cámaras estéreo.
            Transform head = Group(mast, "Cabeza del mástil");
            head.localPosition = new Vector3(0f, 0.85f, 0.02f);
            rig.mastHead = head;
            var headRenderers = new List<Renderer>();
            headRenderers.Add(Box(head, new Vector3(0f, 0.12f, 0f), new Vector3(0.46f, 0.26f, 0.32f), _body).GetComponent<Renderer>());
            headRenderers.Add(Box(head, new Vector3(0f, -0.04f, 0.02f), new Vector3(0.36f, 0.08f, 0.26f), _darkMetal).GetComponent<Renderer>());
            headRenderers.Add(Cyl(head, new Vector3(0.12f, 0.15f, 0.17f), 0.075f, 0.04f, new Vector3(90f, 0f, 0f), _darkMetal).GetComponent<Renderer>());
            headRenderers.Add(Cyl(head, new Vector3(0.12f, 0.15f, 0.19f), 0.05f, 0.02f, new Vector3(90f, 0f, 0f), _lens).GetComponent<Renderer>());
            for (int s = -1; s <= 1; s += 2)
            {
                headRenderers.Add(Box(head, new Vector3(s * 0.11f, -0.04f, 0.15f), new Vector3(0.09f, 0.07f, 0.06f), _blue).GetComponent<Renderer>());
                headRenderers.Add(Cyl(head, new Vector3(s * 0.11f, -0.04f, 0.185f), 0.022f, 0.02f, new Vector3(90f, 0f, 0f), _lens).GetComponent<Renderer>());
                headRenderers.Add(Cyl(head, new Vector3(s * 0.04f, -0.04f, 0.17f), 0.016f, 0.03f, new Vector3(90f, 0f, 0f), _lens).GetComponent<Renderer>());
            }
            headRenderers.Add(Cyl(head, new Vector3(-0.27f, -0.1f, 0.02f), 0.06f, 0.14f, new Vector3(0f, 0f, 90f), _darkMetal).GetComponent<Renderer>()); // tambor lateral
            rig.headRenderers = headRenderers.ToArray();

            var mount = new GameObject("Montura de cámara").transform;
            mount.SetParent(head, false);
            mount.localPosition = new Vector3(0f, 0.05f, 0.22f);
            rig.cameraMount = mount;
            Combine(mast, head);
        }

        private static void BuildArm(Transform root, RoverVisualRig rig)
        {
            // Brazo de 5 articulaciones montado en el frontal; plegado por defecto.
            Transform shoulder = Group(root, "Brazo: hombro");
            shoulder.localPosition = new Vector3(0.55f, 0.82f, 0.98f);
            Box(shoulder, Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f), _darkMetal);
            rig.armShoulder = shoulder;

            Transform upper = Group(shoulder, "Brazo: superior");
            upper.localPosition = new Vector3(0f, 0f, 0.06f);
            Cyl(upper, Vector3.zero, 0.07f, 0.16f, new Vector3(0f, 0f, 90f), _metal);
            Cyl(upper, new Vector3(0f, 0f, 0.42f), 0.05f, 0.8f, new Vector3(90f, 0f, 0f), _body);
            Beam(upper, new Vector3(0.05f, 0.04f, 0.05f), new Vector3(0.05f, 0.04f, 0.78f), 0.008f, _copper);
            rig.armUpper = upper;

            Transform elbow = Group(upper, "Brazo: codo");
            elbow.localPosition = new Vector3(0f, 0f, 0.84f);
            Cyl(elbow, Vector3.zero, 0.065f, 0.15f, new Vector3(0f, 0f, 90f), _darkMetal);
            Cyl(elbow, new Vector3(0f, 0f, 0.36f), 0.045f, 0.68f, new Vector3(90f, 0f, 0f), _body);
            Beam(elbow, new Vector3(-0.05f, 0.03f, 0.05f), new Vector3(-0.05f, 0.03f, 0.66f), 0.008f, _copper);
            rig.armElbow = elbow;

            Transform wrist = Group(elbow, "Brazo: torreta");
            wrist.localPosition = new Vector3(0f, 0f, 0.72f);
            Cyl(wrist, Vector3.zero, 0.06f, 0.14f, new Vector3(0f, 0f, 90f), _darkMetal);
            Box(wrist, new Vector3(0f, -0.14f, 0.02f), new Vector3(0.22f, 0.2f, 0.2f), _body);       // torreta
            Cyl(wrist, new Vector3(0f, -0.3f, 0.02f), 0.035f, 0.18f, Vector3.zero, _metal);        // taladro
            Cyl(wrist, new Vector3(0f, -0.4f, 0.02f), 0.012f, 0.06f, Vector3.zero, _darkMetal);     // broca
            Box(wrist, new Vector3(0.14f, -0.12f, 0.02f), new Vector3(0.08f, 0.12f, 0.12f), _darkMetal); // cámara de mano
            Cyl(wrist, new Vector3(0.14f, -0.12f, 0.09f), 0.025f, 0.02f, new Vector3(90f, 0f, 0f), _lens);
            Box(wrist, new Vector3(-0.14f, -0.16f, 0.02f), new Vector3(0.08f, 0.1f, 0.1f), _metal);   // espectrómetro
            rig.armWrist = wrist;

            Combine(shoulder, upper);
            Combine(upper, elbow);
            Combine(elbow, wrist);
            Combine(wrist);
        }

        private static void BuildHighGainAntenna(Transform root, RoverVisualRig rig)
        {
            Transform mount = Group(root, "Antena de alta ganancia");
            mount.localPosition = new Vector3(-0.55f, 1.25f, -0.55f);
            Cyl(mount, new Vector3(0f, 0.12f, 0f), 0.04f, 0.24f, Vector3.zero, _metal);
            Transform gimbal = Group(mount, "Cardán");
            gimbal.localPosition = new Vector3(0f, 0.26f, 0f);
            Transform dish = Sphere(gimbal, new Vector3(0f, 0.04f, 0.02f), new Vector3(0.42f, 0.12f, 0.42f), _body);
            dish.localRotation = Quaternion.Euler(-25f, 0f, 0f);
            Beam(gimbal, new Vector3(0f, 0.06f, 0.05f), new Vector3(0.12f, 0.25f, 0.3f), 0.01f, _metal); // alimentador
            Combine(gimbal);
            rig.highGainAntenna = gimbal;
        }

        private static void BuildSolarDeck(Transform root, RoverVisualRig rig)
        {
            // Cubierta solar grande (primer diseño): se activa con el módulo de paneles extra.
            Transform deck = Group(root, "Cubierta solar (módulo)");
            Box(deck, new Vector3(0f, 1.42f, -0.1f), new Vector3(2.6f, 0.035f, 2.3f), _solar);
            Box(deck, new Vector3(0f, 1.42f, 1.08f), new Vector3(1.9f, 0.035f, 0.3f), _solar);
            Transform l = Box(deck, new Vector3(-1.13f, 1.42f, 1.0f), new Vector3(0.5f, 0.035f, 0.32f), _solar);
            l.localRotation = Quaternion.Euler(0f, 35f, 0f);
            Transform r = Box(deck, new Vector3(1.13f, 1.42f, 1.0f), new Vector3(0.5f, 0.035f, 0.32f), _solar);
            r.localRotation = Quaternion.Euler(0f, -35f, 0f);
            Box(deck, new Vector3(0f, 1.395f, -0.1f), new Vector3(2.64f, 0.02f, 2.34f), _gold); // marco
            for (int s = -1; s <= 1; s += 2)
            {
                Beam(deck, new Vector3(s * 0.7f, 1.24f, 0.6f), new Vector3(s * 1.1f, 1.4f, 0.6f), 0.02f, _metal);
                Beam(deck, new Vector3(s * 0.7f, 1.24f, -0.8f), new Vector3(s * 1.1f, 1.4f, -0.8f), 0.02f, _metal);
            }
            Combine(deck);
            deck.gameObject.SetActive(false);
            rig.solarDeck = deck.gameObject;
        }

        // ------------------------------------------------------------------
        // Utilidades de construcción
        // ------------------------------------------------------------------

        private static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        private static Transform Prim(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(go.GetComponent<Collider>()); // la colisión del rover es su cápsula
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Transform Box(Transform p, Vector3 pos, Vector3 size, Material m)
        {
            return Prim(PrimitiveType.Cube, p, pos, size, Quaternion.identity, m);
        }

        private static Transform Sphere(Transform p, Vector3 pos, Vector3 size, Material m)
        {
            return Prim(PrimitiveType.Sphere, p, pos, size, Quaternion.identity, m);
        }

        /// <summary>Cilindro de radio y longitud dados (el primitivo de Unity mide 1 de diámetro y 2 de alto).</summary>
        private static Transform Cyl(Transform p, Vector3 pos, float radius, float length, Vector3 euler, Material m)
        {
            return Prim(PrimitiveType.Cylinder, p, pos, new Vector3(radius * 2f, length * 0.5f, radius * 2f), Quaternion.Euler(euler), m);
        }

        /// <summary>Barra cilíndrica entre dos puntos locales.</summary>
        private static Transform Beam(Transform p, Vector3 a, Vector3 b, float radius, Material m)
        {
            Vector3 d = b - a;
            Quaternion rot = d.sqrMagnitude > 0.0001f ? Quaternion.FromToRotation(Vector3.up, d.normalized) : Quaternion.identity;
            return Prim(PrimitiveType.Cylinder, p, (a + b) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f, radius * 2f), rot, m);
        }

        /// <summary>
        /// Fusiona las piezas que cuelgan DIRECTAMENTE de <paramref name="root"/> en una malla por
        /// material. Los subgrupos (piezas móviles: ruedas, cabeza, articulaciones) no se tocan, y
        /// tampoco lo indicado en <paramref name="keep"/>.
        /// </summary>
        private static void Combine(Transform root, params Transform[] keep)
        {
            var groups = new Dictionary<Material, List<CombineInstance>>();
            var toDelete = new List<GameObject>();
            Matrix4x4 toLocal = root.worldToLocalMatrix;

            for (int c = 0; c < root.childCount; c++)
            {
                Transform child = root.GetChild(c);
                if (child.childCount > 0 || IsUnder(child, keep) || child.name.StartsWith("Fusionado")) continue;
                var mf = child.GetComponent<MeshFilter>();
                var r = child.GetComponent<MeshRenderer>();
                if (mf == null || r == null || mf.sharedMesh == null) continue;
                List<CombineInstance> list;
                if (!groups.TryGetValue(r.sharedMaterial, out list)) groups[r.sharedMaterial] = list = new List<CombineInstance>();
                list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = toLocal * mf.transform.localToWorldMatrix });
                toDelete.Add(mf.gameObject);
            }

            foreach (var kv in groups)
            {
                var mesh = new Mesh { name = "Fusionado " + kv.Key.name };
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject("Fusionado " + kv.Key.name);
                go.transform.SetParent(root, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
            }
            for (int i = 0; i < toDelete.Count; i++) Object.DestroyImmediate(toDelete[i]);
        }

        private static bool IsUnder(Transform t, Transform[] keep)
        {
            for (int i = 0; i < keep.Length; i++)
            {
                if (keep[i] != null && (t == keep[i] || t.IsChildOf(keep[i]))) return true;
            }
            return false;
        }

        // ------------------------------------------------------------------
        // Materiales y texturas procedurales
        // ------------------------------------------------------------------

        private static void EnsureMaterials()
        {
            if (_body != null) return;
            Texture2D panels = PanelTexture();
            _body = Mat("Casco crema", new Color(0.86f, 0.82f, 0.72f), 0.25f, 0.45f, panels, new Vector2(2f, 2f));
            _bodyDark = Mat("Casco gris", new Color(0.62f, 0.6f, 0.56f), 0.3f, 0.4f, panels, new Vector2(1f, 1f));
            _metal = Mat("Aluminio", new Color(0.72f, 0.72f, 0.74f), 0.85f, 0.55f, null, Vector2.one);
            _darkMetal = Mat("Metal oscuro", new Color(0.18f, 0.18f, 0.2f), 0.7f, 0.45f, null, Vector2.one);
            _gold = Mat("Dorado", new Color(0.86f, 0.66f, 0.28f), 1f, 0.7f, null, Vector2.one);
            _wheel = Mat("Rueda", new Color(0.2f, 0.2f, 0.22f), 0.75f, 0.35f, null, Vector2.one);
            _rim = Mat("Garras", new Color(0.32f, 0.32f, 0.34f), 0.85f, 0.4f, null, Vector2.one);
            _lens = Mat("Lente", new Color(0.03f, 0.04f, 0.06f), 0.2f, 0.95f, null, Vector2.one);
            _solar = Mat("Células solares", Color.white, 0.6f, 0.8f, SolarTexture(), new Vector2(6f, 6f));
            _rtg = Mat("RTG", new Color(0.35f, 0.35f, 0.37f), 0.8f, 0.35f, null, Vector2.one);
            _copper = Mat("Cobre", new Color(0.78f, 0.45f, 0.25f), 1f, 0.6f, null, Vector2.one);
            _blue = Mat("Placa azul", new Color(0.25f, 0.4f, 0.65f), 0.4f, 0.6f, null, Vector2.one);
        }

        private static Material Mat(string name, Color c, float metallic, float smooth, Texture2D tex, Vector2 tiling)
        {
            Shader urp = Shader.Find("Universal Render Pipeline/Lit");
            var m = new Material(urp != null ? urp : Shader.Find("Standard")) { name = name, color = c };
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", smooth);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            if (tex != null)
            {
                m.mainTexture = tex;
                m.mainTextureScale = tiling;
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            }
            return m;
        }

        /// <summary>Paneles con juntas y remaches (en gris claro; el color lo pone el material).</summary>
        private static Texture2D PanelTexture()
        {
            const int s = 256;
            var px = new Color32[s * s];
            var rng = new System.Random(21);
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    int gx = x % 64, gy = y % 64;
                    float v = 0.92f + (float)rng.NextDouble() * 0.06f;
                    if (gx < 2 || gy < 2) v = 0.55f;                                  // junta entre paneles
                    else if ((gx == 6 || gx == 58) && gy % 10 == 5) v = 0.7f;          // remaches
                    else if ((gy == 6 || gy == 58) && gx % 10 == 5) v = 0.7f;
                    byte b = (byte)(Mathf.Clamp01(v) * 255);
                    px[y * s + x] = new Color32(b, b, b, 255);
                }
            }
            return MakeTex(px, s, "Paneles remachados");
        }

        /// <summary>Células solares azul oscuro con rejilla plateada.</summary>
        private static Texture2D SolarTexture()
        {
            const int s = 128;
            var px = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool line = x % 32 < 2 || y % 32 < 2;
                    bool finger = x % 8 == 0;
                    px[y * s + x] = line ? new Color32(190, 190, 200, 255) : (finger ? new Color32(70, 80, 120, 255) : new Color32(25, 32, 70, 255));
                }
            }
            return MakeTex(px, s, "Células solares");
        }

        private static Texture2D MakeTex(Color32[] px, int s, string name)
        {
            var t = new Texture2D(s, s, TextureFormat.RGBA32, true) { name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4 };
            t.SetPixels32(px);
            t.Apply(true, true);
            return t;
        }
    }
}
