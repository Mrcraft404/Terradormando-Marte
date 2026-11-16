using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base.Ship
{
    /// <summary>
    /// Los seis niveles interiores (Ø interior 9,2 m, 3,2 m entre suelos). Cada nivel tiene
    /// colisiones reales (suelo, pared con el hueco de la puerta en el hangar, núcleo y muebles
    /// grandes) para que el robot entre por la puerta de carga y el astronauta camine por dentro. Coordenadas locales de cada nivel: suelo en y = 0, +Z = frente (puerta), núcleo de
    /// escalera y montacargas detrás (x ±1,1, z −3,9…−2,3) en todos los niveles.
    /// Todas las piezas son estáticas y se fusionan por material al final de cada nivel.
    /// </summary>
    public static class ShipInteriorBuilder
    {
        private static Transform _p;
        private static ShipModel _model;
        private static int _level;
        private static float _y0;
        private static readonly List<Bounds> _solids = new List<Bounds>();

        public static void BuildLevels(ShipModel model)
        {
            _model = model;
            for (int i = 0; i < ShipModel.LevelCount; i++)
            {
                _level = i + 1;
                _y0 = ShipModel.LevelFloorY(i);
                var root = new GameObject(ShipModel.LevelNames[i]).transform;
                root.SetParent(model.transform, false);
                root.localPosition = new Vector3(0f, _y0, 0f);
                _p = root;
                model.Levels[i] = root;

                _solids.Clear();
                CommonStructure(i);
                switch (i)
                {
                    case 0: Hangar(); break;
                    case 1: Workshops(); break;
                    case 2: Industry(); break;
                    case 3: Science(); break;
                    case 4: Resources(); break;
                    default: Control(); break;
                }
                ShipMesh.Combine(root);
                AddColliders(root, i);

                // Luz del nivel (apagada: la enciende el inspector al mirar dentro).
                var lightGo = new GameObject("Luz LED");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = new Vector3(0f, 2.6f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 9f;
                light.intensity = 2.2f;
                light.color = new Color(0.95f, 0.97f, 1f);
                light.enabled = true; // el interior se habita (astronauta): luces encendidas
                light.shadows = LightShadows.None;
                model.LevelLights[i] = light;
            }
        }

        // ------------------------------------------------------------------
        // Estructura común: suelo, LED, núcleo con escalera de caracol y montacargas, tuberías
        // ------------------------------------------------------------------

        private static void CommonStructure(int i)
        {
            float floorR = Mathf.Min(ShipModel.InnerRadius, ShipModel.HullRadiusAt(_y0 + 3f) - 0.2f);
            ShipMesh.Cyl(_p, new Vector3(0f, 0.04f, 0f), floorR, 0.08f, ShipMaterials.Floor);

            // Iluminación LED blanca en el techo (anillo de 10 regletas).
            for (int k = 0; k < 10; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 36f, 0f);
                Transform led = ShipMesh.Box(_p, q * new Vector3(0f, 2.85f, floorR - 0.7f), new Vector3(1.2f, 0.04f, 0.12f), ShipMaterials.LedWhite);
                led.localRotation = q;
            }

            // Núcleo: postes, escalera de caracol (12 peldaños por nivel) y montacargas.
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = 0; sz < 2; sz++)
                {
                    float z = sz == 0 ? -3.85f : -2.35f;
                    B(sx * 1.08f, 1.6f, z, 0.1f, 3.2f, 0.1f, ShipMaterials.Metal);
                }
            }
            Vector3 stairC = new Vector3(-0.5f, 0f, -3.1f);
            ShipMesh.Cyl(_p, stairC + Vector3.up * 1.6f, 0.07f, 3.2f, ShipMaterials.Metal);
            for (int k = 0; k < 12; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 30f, 0f);
                Transform step = ShipMesh.Box(_p, stairC + q * new Vector3(0.3f, 0f, 0f) + Vector3.up * (k * 0.267f + 0.1f), new Vector3(0.6f, 0.05f, 0.24f), ShipMaterials.Metal);
                step.localRotation = q;
            }
            // Hueco del montacargas (cristal) y cabina en el hangar.
            B(0.5f, 1.4f, -2.35f, 0.9f, 2.8f, 0.03f, ShipMaterials.Glass);
            if (i == 0) B(0.5f, 1.1f, -3.1f, 0.9f, 2.2f, 1.2f, ShipMaterials.Panel);
            // Barandilla delantera del núcleo.
            B(-0.5f, 1.0f, -2.3f, 1.1f, 0.05f, 0.05f, ShipMaterials.Orange);

            // Tuberías y cableado organizado bajo el techo (arco de tramos rectos).
            float pr = floorR - 0.25f;
            for (int k = 0; k < 16; k++)
            {
                float a0 = (k * 22.5f + 20f) * Mathf.Deg2Rad, a1 = ((k + 1) * 22.5f + 20f) * Mathf.Deg2Rad;
                Vector3 p0 = new Vector3(Mathf.Sin(a0) * pr, 2.95f, Mathf.Cos(a0) * pr);
                Vector3 p1 = new Vector3(Mathf.Sin(a1) * pr, 2.95f, Mathf.Cos(a1) * pr);
                ShipMesh.Beam(_p, p0, p1, 0.05f, ShipMaterials.Copper);
                ShipMesh.Beam(_p, p0 + Vector3.down * 0.15f, p1 + Vector3.down * 0.15f, 0.035f, ShipMaterials.Metal);
            }
            Label(0f, 2.4f, -3.1f, Tr.T("Núcleo: escalera de caracol + montacargas"));
        }

        // ------------------------------------------------------------------
        // Nivel 1 — Hangar robótico
        // ------------------------------------------------------------------

        private static void Hangar()
        {
            // Plaza del rover 4,5 × 5 m (taller integrado) con marcas de suelo.
            B(0f, 0.09f, -2.2f, 4.5f, 0.02f, 0.12f, ShipMaterials.Hazard);
            B(0f, 0.09f, 2.8f, 4.5f, 0.02f, 0.12f, ShipMaterials.Hazard);
            B(-2.25f, 0.09f, 0.3f, 0.12f, 0.02f, 5f, ShipMaterials.Hazard);
            B(2.25f, 0.09f, 0.3f, 0.12f, 0.02f, 5f, ShipMaterials.Hazard);
            // Pórtico de cambio de ruedas, patas y herramientas: dos columnas, viga y polipasto.
            B(-2.1f, 1.35f, -1.6f, 0.2f, 2.7f, 0.2f, ShipMaterials.Orange);
            B(2.1f, 1.35f, -1.6f, 0.2f, 2.7f, 0.2f, ShipMaterials.Orange);
            B(0f, 2.75f, -1.6f, 4.4f, 0.25f, 0.3f, ShipMaterials.Orange);
            B(0.6f, 2.45f, -1.6f, 0.4f, 0.35f, 0.4f, ShipMaterials.DarkMetal);
            ShipMesh.Beam(_p, new Vector3(0.6f, 2.3f, -1.6f), new Vector3(0.6f, 1.4f, -1.6f), 0.015f, ShipMaterials.DarkMetal);
            B(-1.4f, 1.6f, -1.6f, 0.5f, 0.3f, 0.3f, ShipMaterials.Panel); // brazo de herramientas
            // Estación de carga de baterías (izquierda).
            B(-3.1f, 1.0f, 0f, 1.0f, 2.0f, 2.6f, ShipMaterials.Panel);
            B(-2.58f, 1.2f, 0f, 0.04f, 0.5f, 1.4f, ShipMaterials.ScreenGreen);
            ShipMesh.Cyl(_p, new Vector3(-2.55f, 0.6f, 1.0f), 0.18f, 0.15f, ShipMaterials.DarkMetal, new Vector3(0f, 0f, 90f));
            // Almacén de repuestos (derecha): estantería hasta el techo con cajas.
            B(3.05f, 1.4f, 0f, 1.1f, 0.08f, 2.8f, ShipMaterials.Metal);
            for (int k = 0; k < 4; k++) B(3.05f, 0.3f + k * 0.7f, 0f, 1.1f, 0.05f, 2.8f, ShipMaterials.Metal);
            for (int k = 0; k < 6; k++) B(3.05f, 0.5f + (k % 3) * 0.7f, -0.9f + (k / 3) * 1.6f, 0.8f, 0.35f, 0.7f, ShipMaterials.Orange);
            // Bancos de trabajo traseros.
            B(-2.0f, 0.45f, -2.65f, 1.3f, 0.9f, 0.65f, ShipMaterials.Panel);
            B(2.0f, 0.45f, -2.65f, 1.3f, 0.9f, 0.65f, ShipMaterials.Panel);
            B(-2.0f, 1.3f, -2.95f, 1.2f, 0.8f, 0.05f, ShipMaterials.Metal); // panel de herramientas
            // Delantal de la puerta de carga y umbral hasta la plataforma del ascensor (con colisión).
            B(0f, 0.09f, 3.6f, 3.2f, 0.02f, 0.9f, ShipMaterials.Hazard);
            B(0f, 0.03f, ShipModel.InnerRadius + 0.3f, 3.4f, 0.1f, 1.0f, ShipMaterials.Floor);
            Solid(new Vector3(0f, -0.1f, ShipModel.InnerRadius + 0.3f), new Vector3(3.4f, 0.36f, 1.1f));
            // Estanterías de la bodega (sólidas) y terminal.
            Solid(new Vector3(3.05f, 1.4f, 0f), new Vector3(1.1f, 2.8f, 2.8f));

            Label(0f, 0.6f, 0.3f, Tr.T("Hangar del robot · 4,5 × 5 m (taller integrado)"));
            Label(0f, 2.9f, -1.6f, Tr.T("Pórtico: cambio de ruedas, patas y herramientas"));
            Label(-3.1f, 2.2f, 0f, Tr.T("Estación de carga de baterías"));
            Label(3.05f, 2.9f, 0f, Tr.T("Almacén de repuestos · 1,1 × 2,8 m"));
            Label(-2.0f, 1.2f, -2.65f, Tr.T("Bancos de trabajo"));
            Label(0f, 1.6f, 3.7f, Tr.T("Puerta de carga 3,2 m → plataforma elevadora"));
        }

        // ------------------------------------------------------------------
        // Nivel 2 — Talleres de fabricación (las máquinas que antes estaban fuera del cohete)
        // ------------------------------------------------------------------

        private static Transform _m; // máquina en construcción (coordenadas propias: frente = +Z)

        private static void Workshops()
        {
            // Suelo técnico: pasillo central señalizado.
            B(0f, 0.09f, 0.6f, 1.6f, 0.02f, 4.2f, ShipMaterials.Hazard);

            Fabricator(ShipLayout.Fabricator, ShipLayout.FabricatorFacing);
            Smelter(ShipLayout.Smelter, ShipLayout.SmelterFacing);
            ChemicalRefinery(ShipLayout.Refinery, ShipLayout.RefineryFacing);
            Workbench(ShipLayout.Workbench, ShipLayout.WorkbenchFacing);
            Lockers(ShipLayout.Lockers);

            Label(ShipLayout.Fabricator.x, 2.4f, ShipLayout.Fabricator.z, Tr.T("Fabricador"));
            Label(ShipLayout.Smelter.x, 2.6f, ShipLayout.Smelter.z, Tr.T("Fundición de inducción"));
            Label(ShipLayout.Refinery.x, 2.4f, ShipLayout.Refinery.z, Tr.T("Refinería química (Sabatier · electrólisis · Bosch)"));
            Label(ShipLayout.Workbench.x, 2.2f, ShipLayout.Workbench.z, Tr.T("Banco de trabajo"));
            Label(0f, 2.3f, ShipLayout.Lockers.z, Tr.T("Almacén de la base"));
        }

        private static Transform BeginMachine(string name, Vector3 pos, float yaw)
        {
            _m = new GameObject(name).transform;
            _m.SetParent(_p, false);
            _m.localPosition = pos;
            _m.localRotation = Quaternion.Euler(0f, yaw, 0f);
            return _m;
        }

        private static void EndMachine(Vector3 colliderCenter, Vector3 colliderSize)
        {
            ShipMesh.Combine(_m);
            // Colisionador ajustado a las mallas reales de la máquina; si fallara, la caja de diseño.
            if (MarsRobot.Visuals.ColliderFitter.FitStatic(_m.gameObject) == null)
            {
                var box = _m.gameObject.AddComponent<BoxCollider>();
                box.center = colliderCenter;
                box.size = colliderSize;
            }
            _m = null;
        }

        private static void MB(float x, float y, float z, float sx, float sy, float sz, Material m)
        {
            ShipMesh.Box(_m, new Vector3(x, y, z), new Vector3(sx, sy, sz), m);
        }

        private static void MC(float x, float y, float z, float r, float h, Material m, Vector3 euler = default(Vector3))
        {
            ShipMesh.Cyl(_m, new Vector3(x, y, z), r, h, m, euler);
        }

        /// <summary>Celda de fabricación: cabina cerrada con puerta de cristal, pórtico XYZ y panel.</summary>
        private static void Fabricator(Vector3 pos, float yaw)
        {
            BeginMachine(Tr.T("Fabricador"), pos, yaw);
            MB(0f, 0.06f, 0f, 1.9f, 0.12f, 1.3f, ShipMaterials.DarkMetal);              // zócalo
            MB(0f, 2.0f, 0f, 1.9f, 0.25f, 1.3f, ShipMaterials.Panel);                   // techo técnico
            MB(0f, 1.06f, -0.62f, 1.9f, 1.88f, 0.05f, ShipMaterials.Wall);              // trasera
            for (int s = -1; s <= 1; s += 2)
            {
                MB(s * 0.93f, 1.06f, 0f, 0.05f, 1.88f, 1.3f, ShipMaterials.Wall);       // laterales
                MB(s * 0.9f, 1.06f, 0.63f, 0.1f, 1.88f, 0.08f, ShipMaterials.Orange);   // marco
                MB(s * 0.75f, 1.65f, 0f, 0.05f, 0.06f, 1.1f, ShipMaterials.Metal);      // raíles del pórtico
            }
            MB(0f, 1.05f, 0.64f, 1.65f, 1.45f, 0.03f, ShipMaterials.Glass);             // puerta de cristal
            MB(0f, 1.82f, 0.66f, 1.7f, 0.08f, 0.04f, ShipMaterials.Hazard);
            MB(0f, 0.6f, 0f, 1.25f, 0.06f, 0.85f, ShipMaterials.Metal);                 // mesa de impresión
            MB(0f, 1.65f, 0.05f, 1.5f, 0.07f, 0.1f, ShipMaterials.DarkMetal);           // puente
            MB(0.15f, 1.5f, 0.05f, 0.22f, 0.25f, 0.22f, ShipMaterials.DarkMetal);       // cabezal
            MC(0.15f, 1.27f, 0.05f, 0.03f, 0.2f, ShipMaterials.Copper);                 // boquilla
            MB(0.1f, 0.72f, 0.05f, 0.45f, 0.18f, 0.35f, ShipMaterials.Orange);          // pieza a medio hacer
            MB(1.08f, 1.25f, 0.45f, 0.08f, 0.7f, 0.42f, ShipMaterials.Panel);           // panel lateral
            MB(1.13f, 1.35f, 0.45f, 0.02f, 0.38f, 0.32f, ShipMaterials.ScreenBlue);
            MB(0f, 2.14f, 0.55f, 1.6f, 0.03f, 0.05f, ShipMaterials.ScreenGreen);        // baliza de estado
            ShipMesh.Beam(_m, new Vector3(-0.6f, 2.12f, -0.4f), new Vector3(-0.6f, 2.95f, -0.4f), 0.06f, ShipMaterials.Copper);
            EndMachine(new Vector3(0.05f, 1.06f, 0f), new Vector3(2.15f, 2.12f, 1.35f));
        }

        /// <summary>Horno de inducción basculante con crisol, campana extractora y cuadro de control.</summary>
        private static void Smelter(Vector3 pos, float yaw)
        {
            BeginMachine(Tr.T("Fundición"), pos, yaw);
            MB(0f, 0.08f, 0f, 1.9f, 0.16f, 1.8f, ShipMaterials.DarkMetal);              // plataforma
            MC(0f, 0.85f, -0.1f, 0.62f, 1.3f, ShipMaterials.Metal);                     // cuerpo del horno
            for (int k = 0; k < 4; k++) MC(0f, 0.45f + k * 0.27f, -0.1f, 0.66f, 0.06f, ShipMaterials.Copper); // bobina
            MC(0f, 1.52f, -0.1f, 0.44f, 0.05f, ShipMaterials.Furnace);                  // crisol al rojo
            MC(0f, 1.5f, -0.1f, 0.55f, 0.06f, ShipMaterials.DarkMetal);
            for (int s = -1; s <= 1; s += 2)
            {
                MB(s * 0.78f, 0.95f, -0.1f, 0.12f, 1.6f, 0.14f, ShipMaterials.Orange);  // bastidor basculante
                MC(s * 0.7f, 1.3f, -0.1f, 0.09f, 0.2f, ShipMaterials.DarkMetal, new Vector3(0f, 0f, 90f));
            }
            MC(0f, 2.3f, -0.1f, 0.72f, 0.16f, ShipMaterials.DarkMetal);                 // campana
            ShipMesh.Beam(_m, new Vector3(0f, 2.38f, -0.1f), new Vector3(0f, 2.98f, -0.1f), 0.22f, ShipMaterials.Metal);
            MB(0f, 0.42f, 0.82f, 0.85f, 0.1f, 0.38f, ShipMaterials.DarkMetal);          // lingotera
            for (int k = 0; k < 3; k++) MB(-0.25f + k * 0.25f, 0.49f, 0.82f, 0.18f, 0.04f, 0.28f, ShipMaterials.Furnace);
            MB(0.75f, 0.8f, 0.6f, 0.4f, 1.3f, 0.35f, ShipMaterials.Panel);              // cuadro de control
            MB(0.75f, 1.15f, 0.78f, 0.28f, 0.3f, 0.02f, ShipMaterials.ScreenGreen);
            MB(0.75f, 0.8f, 0.78f, 0.08f, 0.08f, 0.03f, ShipMaterials.Furnace);         // seta de emergencia
            EndMachine(new Vector3(0f, 1.2f, 0f), new Vector3(1.9f, 2.4f, 1.8f));
        }

        /// <summary>Refinería química: depósitos de CO₂ y agua, reactor Sabatier, electrolizador y tuberías.</summary>
        private static void ChemicalRefinery(Vector3 pos, float yaw)
        {
            BeginMachine(Tr.T("Refinería química"), pos, yaw);
            MB(0f, 0.06f, 0f, 1.9f, 0.12f, 1.5f, ShipMaterials.DarkMetal);              // bancada
            MC(-0.55f, 1.0f, -0.35f, 0.32f, 1.7f, ShipMaterials.Frost);                 // depósito de CO₂
            MC(0.55f, 1.0f, -0.35f, 0.32f, 1.7f, ShipMaterials.TankDark);               // depósito de agua/metano
            ShipMesh.Prim(PrimitiveType.Sphere, _m, new Vector3(-0.55f, 1.85f, -0.35f), Vector3.one * 0.64f, Quaternion.identity, ShipMaterials.Frost);
            ShipMesh.Prim(PrimitiveType.Sphere, _m, new Vector3(0.55f, 1.85f, -0.35f), Vector3.one * 0.64f, Quaternion.identity, ShipMaterials.TankDark);
            MC(0f, 0.75f, 0.35f, 0.2f, 1.3f, ShipMaterials.Metal, new Vector3(0f, 0f, 90f)); // reactor Sabatier
            for (int k = 0; k < 3; k++) MC(-0.45f + k * 0.45f, 0.75f, 0.35f, 0.23f, 0.05f, ShipMaterials.Copper, new Vector3(0f, 0f, 90f));
            MB(0f, 1.55f, 0.45f, 0.9f, 0.55f, 0.1f, ShipMaterials.Panel);               // panel de mando
            MB(0f, 1.6f, 0.51f, 0.7f, 0.35f, 0.02f, ShipMaterials.ScreenBlue);
            for (int k = 0; k < 3; k++) MC(-0.3f + k * 0.3f, 1.3f, 0.51f, 0.05f, 0.02f, ShipMaterials.LedWhite, new Vector3(90f, 0f, 0f));
            ShipMesh.Beam(_m, new Vector3(-0.55f, 2.1f, -0.35f), new Vector3(-0.55f, 2.95f, -0.35f), 0.05f, ShipMaterials.Copper);
            ShipMesh.Beam(_m, new Vector3(0.55f, 2.1f, -0.35f), new Vector3(0.55f, 2.95f, -0.35f), 0.05f, ShipMaterials.Copper);
            ShipMesh.Beam(_m, new Vector3(-0.55f, 0.9f, -0.05f), new Vector3(-0.4f, 0.75f, 0.35f), 0.04f, ShipMaterials.Copper);
            ShipMesh.Beam(_m, new Vector3(0.55f, 0.9f, -0.05f), new Vector3(0.4f, 0.75f, 0.35f), 0.04f, ShipMaterials.Copper);
            EndMachine(new Vector3(0f, 1.1f, 0f), new Vector3(1.9f, 2.2f, 1.5f));
        }

        /// <summary>Banco de trabajo con cajonera, panel de herramientas, tornillo y lámpara.</summary>
        private static void Workbench(Vector3 pos, float yaw)
        {
            BeginMachine(Tr.T("Banco de trabajo"), pos, yaw);
            MB(0f, 0.9f, 0f, 1.9f, 0.07f, 0.8f, ShipMaterials.Metal);                   // tablero
            for (int s = -1; s <= 1; s += 2) MB(s * 0.88f, 0.45f, 0f, 0.06f, 0.88f, 0.74f, ShipMaterials.DarkMetal);
            MB(0.55f, 0.45f, 0f, 0.6f, 0.82f, 0.7f, ShipMaterials.Panel);               // cajonera
            for (int k = 0; k < 3; k++) MB(0.55f, 0.2f + k * 0.26f, 0.36f, 0.5f, 0.02f, 0.02f, ShipMaterials.Orange);
            MB(0f, 1.6f, -0.37f, 1.9f, 1.1f, 0.04f, ShipMaterials.Panel);               // panel de herramientas
            for (int k = 0; k < 6; k++)
                ShipMesh.Beam(_m, new Vector3(-0.75f + k * 0.28f, 1.4f, -0.33f), new Vector3(-0.75f + k * 0.28f, 1.75f + (k % 2) * 0.1f, -0.33f), 0.02f, k % 2 == 0 ? ShipMaterials.Orange : ShipMaterials.DarkMetal);
            MB(-0.6f, 1.0f, 0.15f, 0.25f, 0.14f, 0.2f, ShipMaterials.DarkMetal);        // tornillo de banco
            ShipMesh.Beam(_m, new Vector3(0.8f, 0.95f, -0.25f), new Vector3(0.5f, 1.55f, 0.05f), 0.02f, ShipMaterials.Metal); // flexo
            MB(0.45f, 1.52f, 0.1f, 0.18f, 0.06f, 0.12f, ShipMaterials.LedWhite);
            MB(0.05f, 0.95f, 0.15f, 0.35f, 0.02f, 0.25f, ShipMaterials.ScreenBlue);     // tableta
            EndMachine(new Vector3(0f, 0.6f, 0f), new Vector3(1.95f, 1.2f, 0.85f));
        }

        /// <summary>Fila de armarios del almacén de la base contra la pared delantera.</summary>
        private static void Lockers(Vector3 pos)
        {
            BeginMachine(Tr.T("Almacén de la base"), pos, 180f);
            for (int k = 0; k < 5; k++)
            {
                float x = -1.2f + k * 0.6f;
                MB(x, 1.0f, 0f, 0.56f, 2.0f, 0.5f, ShipMaterials.Wall);
                MB(x, 1.0f, 0.26f, 0.02f, 1.9f, 0.02f, ShipMaterials.DarkMetal);
                MB(x + 0.18f, 1.05f, 0.26f, 0.04f, 0.25f, 0.03f, ShipMaterials.Metal);  // asa
                MB(x, 1.75f, 0.26f, 0.3f, 0.12f, 0.02f, k == 2 ? ShipMaterials.ScreenBlue : ShipMaterials.ScreenGreen);
            }
            MB(0f, 2.05f, 0f, 3.05f, 0.1f, 0.55f, ShipMaterials.Orange);
            EndMachine(new Vector3(0f, 1.0f, 0f), new Vector3(3.05f, 2.0f, 0.5f));
        }

        // ------------------------------------------------------------------
        // Colisiones de cada nivel
        // ------------------------------------------------------------------

        private static void AddColliders(Transform root, int level)
        {
            var colRoot = new GameObject("Colisiones").transform;
            colRoot.SetParent(root, false);
            float floorR = Mathf.Min(ShipModel.InnerRadius, ShipModel.HullRadiusAt(_y0 + 3f) - 0.2f);

            // Suelo (prisma convexo de 24 lados; su cara superior coincide con la rejilla).
            var floor = new GameObject("Suelo");
            floor.transform.SetParent(colRoot, false);
            var mc = floor.AddComponent<MeshCollider>();
            mc.sharedMesh = Prism(floorR + 0.1f, -0.25f, 0.08f, 24);
            mc.convex = true;

            // Techo del último nivel.
            if (level == ShipModel.LevelCount - 1)
            {
                var ceil = new GameObject("Techo");
                ceil.transform.SetParent(colRoot, false);
                var cc = ceil.AddComponent<MeshCollider>();
                cc.sharedMesh = Prism(floorR + 0.1f, ShipModel.LevelHeight - 0.2f, ShipModel.LevelHeight + 0.1f, 24);
                cc.convex = true;
            }

            // Pared: anillo de 36 tramos. En el hangar se deja libre la puerta de carga (+Z).
            const int n = 36;
            float wr = floorR + 0.18f;
            float width = 2f * Mathf.PI * wr / n * 1.08f;
            for (int k = 0; k < n; k++)
            {
                float deg = k * 360f / n;
                float signed = Mathf.DeltaAngle(0f, deg);
                if (level == ShipModel.HangarLevel && Mathf.Abs(signed) < 22f) continue;
                var w = new GameObject("Pared " + k);
                w.transform.SetParent(colRoot, false);
                w.transform.localRotation = Quaternion.Euler(0f, deg, 0f);
                w.transform.localPosition = w.transform.localRotation * new Vector3(0f, ShipModel.LevelHeight * 0.5f, wr);
                w.AddComponent<BoxCollider>().size = new Vector3(width, ShipModel.LevelHeight, 0.3f);
            }

            // Núcleo (escalera y montacargas).
            AddBox(colRoot, "Núcleo", new Vector3(0f, 1.6f, -3.1f), new Vector3(2.3f, 3.2f, 1.7f));

            // Muebles grandes.
            for (int i = 0; i < _solids.Count; i++) AddBox(colRoot, "Mueble " + i, _solids[i].center, _solids[i].size);
        }

        private static void AddBox(Transform parent, string name, Vector3 center, Vector3 size)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.AddComponent<BoxCollider>().size = size;
        }

        /// <summary>Prisma vertical de N lados (malla para un colisionador convexo).</summary>
        private static Mesh Prism(float radius, float y0, float y1, int sides)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int k = 0; k < sides; k++)
            {
                float a = k * Mathf.PI * 2f / sides;
                v.Add(new Vector3(Mathf.Sin(a) * radius, y0, Mathf.Cos(a) * radius));
                v.Add(new Vector3(Mathf.Sin(a) * radius, y1, Mathf.Cos(a) * radius));
            }
            for (int k = 0; k < sides; k++)
            {
                int a0 = k * 2, a1 = ((k + 1) % sides) * 2;
                t.Add(a0); t.Add(a1); t.Add(a0 + 1);
                t.Add(a1); t.Add(a1 + 1); t.Add(a0 + 1);
                if (k > 0 && k < sides - 1)
                {
                    t.Add(0); t.Add(a1); t.Add(a0);           // tapa inferior
                    t.Add(1); t.Add(a0 + 1); t.Add(a1 + 1);   // tapa superior
                }
            }
            var m = new Mesh { name = "Prisma" };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        // ------------------------------------------------------------------
        // Niveles 3–6: máquinas contra la pared, mirando al centro. El centro del nivel y la salida
        // del montacargas quedan libres para caminar (comprobado con Marte ▸ Comprobar espacio).
        // ------------------------------------------------------------------

        private static readonly System.Collections.Generic.Dictionary<string, Material> _extra = new System.Collections.Generic.Dictionary<string, Material>();

        private static Material X(string name, Color c, float metal = 0f, float smooth = 0.4f, Color emission = default(Color))
        {
            Material m;
            if (!_extra.TryGetValue(name, out m) || m == null)
            {
                m = ShipMesh.Mat(name, c, metal, smooth, null, default(Vector2), emission);
                _extra[name] = m;
            }
            return m;
        }

        private static void Machine(string name, ShipLayout.Spot spot)
        {
            BeginMachine(name, spot.Position, spot.FacingCenter);
        }

        /// <summary>Estantería poco profunda contra la pared, con cajas.</summary>
        private static void Shelf(ShipLayout.Spot spot, int seed)
        {
            Machine("Estantería", spot);
            for (int s = -1; s <= 1; s += 2) MB(s * 0.78f, 1.1f, 0f, 0.05f, 2.2f, 0.45f, ShipMaterials.Metal);
            var rnd = new System.Random(seed);
            for (int k = 0; k < 4; k++)
            {
                float y = 0.15f + k * 0.62f;
                MB(0f, y, 0f, 1.56f, 0.04f, 0.45f, ShipMaterials.Metal);
                for (int b = 0; b < 3; b++)
                {
                    float w = 0.3f + (float)rnd.NextDouble() * 0.15f, h = 0.2f + (float)rnd.NextDouble() * 0.2f;
                    Material m = b % 2 == 0 ? ShipMaterials.Orange : X("Caja gris", new Color(0.45f, 0.47f, 0.5f));
                    MB(-0.5f + b * 0.5f, y + 0.02f + h * 0.5f, 0.02f, w, h, 0.36f, m);
                }
            }
            EndMachine(new Vector3(0f, 1.1f, 0f), new Vector3(1.6f, 2.2f, 0.45f));
        }

        // ---------------- Nivel 3 · Industria ----------------

        private static void Industry()
        {
            // Impresora 3D industrial: cabina de metacrilato, cama caliente, pórtico y bobinas de filamento.
            Machine(Tr.T("Impresora 3D"), ShipLayout.Printer3D);
            MB(0f, 0.35f, 0f, 1.3f, 0.7f, 1.1f, ShipMaterials.Panel);                         // armario base
            for (int k = 0; k < 2; k++) MB(0f, 0.2f + k * 0.3f, 0.556f, 1.1f, 0.01f, 0.01f, ShipMaterials.DarkMetal); // cajones
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                MB(sx * 0.62f, 1.3f, sz * 0.52f, 0.05f, 1.2f, 0.05f, ShipMaterials.Metal);    // postes
            MB(0f, 1.92f, 0f, 1.3f, 0.06f, 1.1f, ShipMaterials.Panel);
            MB(0f, 1.3f, 0.53f, 1.18f, 1.15f, 0.015f, ShipMaterials.Glass);                   // puerta
            MB(0f, 0.75f, 0f, 0.9f, 0.04f, 0.8f, X("Cama caliente", new Color(0.12f, 0.12f, 0.13f), 0.3f, 0.7f));
            MB(0f, 1.6f, 0f, 1.1f, 0.05f, 0.06f, ShipMaterials.DarkMetal);                     // puente
            MB(0.2f, 1.5f, 0f, 0.14f, 0.18f, 0.14f, ShipMaterials.Orange);                    // extrusor
            MB(-0.15f, 0.82f, 0.05f, 0.3f, 0.1f, 0.25f, X("Pieza impresa", new Color(0.92f, 0.92f, 0.88f)));
            MB(0f, 1.94f, 0.5f, 1.1f, 0.02f, 0.03f, ShipMaterials.LedWhite);
            MB(0.5f, 1.05f, 0.56f, 0.2f, 0.14f, 0.02f, ShipMaterials.ScreenBlue);             // pantalla táctil
            Color[] spools = { new Color(0.95f, 0.45f, 0.1f), new Color(0.92f, 0.92f, 0.9f), new Color(0.2f, 0.4f, 0.85f) };
            for (int k = 0; k < 3; k++)
            {
                MC(0.72f, 1.0f + k * 0.28f, -0.25f + k * 0.25f, 0.12f, 0.08f, X("Filamento " + k, spools[k], 0f, 0.6f), new Vector3(0f, 0f, 90f));
                MC(0.7f, 1.0f + k * 0.28f, -0.25f + k * 0.25f, 0.035f, 0.12f, ShipMaterials.DarkMetal, new Vector3(0f, 0f, 90f));
            }
            EndMachine(Vector3.up, new Vector3(1.5f, 2f, 1.2f));

            // Taller de electrónica: tapete antiestático, osciloscopio, estación de soldadura, microscopio y cajonera.
            Machine(Tr.T("Taller de electrónica"), ShipLayout.ElectronicsBench);
            WorkTable(1.9f, 0.8f, X("Tapete antiestático", new Color(0.12f, 0.3f, 0.55f), 0f, 0.25f));
            MB(-0.6f, 1.08f, -0.18f, 0.42f, 0.28f, 0.3f, ShipMaterials.Panel);                // osciloscopio
            MB(-0.66f, 1.1f, -0.025f, 0.24f, 0.18f, 0.01f, ShipMaterials.ScreenGreen);
            MB(0.05f, 0.99f, -0.2f, 0.22f, 0.1f, 0.18f, ShipMaterials.DarkMetal);              // estación de soldadura
            ShipMesh.Beam(_m, new Vector3(0.1f, 1.05f, -0.1f), new Vector3(0.3f, 1.0f, 0.12f), 0.012f, ShipMaterials.Orange);
            MC(0.55f, 1.0f, -0.15f, 0.08f, 0.04f, ShipMaterials.DarkMetal);                     // microscopio
            ShipMesh.Beam(_m, new Vector3(0.55f, 1.0f, -0.2f), new Vector3(0.55f, 1.32f, -0.2f), 0.02f, ShipMaterials.Metal);
            ShipMesh.Beam(_m, new Vector3(0.55f, 1.32f, -0.2f), new Vector3(0.55f, 1.2f, -0.05f), 0.035f, ShipMaterials.Metal);
            MC(0.2f, 1.45f, 0.05f, 0.11f, 0.02f, ShipMaterials.LedWhite);                       // lupa con luz
            ShipMesh.Beam(_m, new Vector3(0.85f, 0.97f, -0.35f), new Vector3(0.2f, 1.45f, 0.05f), 0.012f, ShipMaterials.Metal);
            for (int r = 0; r < 4; r++)
            for (int c = 0; c < 5; c++)
                MB(-0.75f + c * 0.13f, 1.25f + r * 0.1f, -0.33f, 0.11f, 0.08f, 0.12f, (r + c) % 3 == 0 ? ShipMaterials.Orange : X("Cajetín", new Color(0.8f, 0.8f, 0.78f)));
            EndMachine(new Vector3(0f, 0.8f, 0f), new Vector3(1.95f, 1.6f, 0.85f));

            // Contenedores de minerales contra la pared delantera.
            Machine("Contenedores de minerales", ShipLayout.MineralBins);
            Color[] ores = { new Color(0.45f, 0.2f, 0.14f), new Color(0.62f, 0.35f, 0.22f), new Color(0.55f, 0.6f, 0.45f), new Color(0.88f, 0.85f, 0.7f) };
            for (int k = 0; k < 4; k++)
            {
                float x = -0.96f + k * 0.64f;
                MB(x, 0.4f, 0f, 0.6f, 0.8f, 0.55f, ShipMaterials.Metal);
                MB(x, 0.78f, 0.04f, 0.52f, 0.06f, 0.44f, X("Mineral " + k, ores[k], 0.1f, 0.15f));
                MB(x, 0.55f, 0.28f, 0.3f, 0.12f, 0.005f, k % 2 == 0 ? ShipMaterials.Hazard : ShipMaterials.Orange);
            }
            EndMachine(new Vector3(0f, 0.4f, 0f), new Vector3(2.6f, 0.8f, 0.55f));

            Shelf(ShipLayout.IndustryShelfL, 3);
            Shelf(ShipLayout.IndustryShelfR, 4);

            LabelAt(ShipLayout.Printer3D, 2.2f, Tr.T("Impresora 3D"));
            LabelAt(ShipLayout.ElectronicsBench, 1.8f, Tr.T("Taller de electrónica"));
            LabelAt(ShipLayout.MineralBins, 1.1f, "Contenedores de minerales");
        }

        /// <summary>Mesa de trabajo (tablero a 0,9 m) con patas y balda inferior, en la máquina actual.</summary>
        private static void WorkTable(float w, float d, Material top)
        {
            MB(0f, 0.9f, 0f, w, 0.05f, d, ShipMaterials.Metal);
            MB(0f, 0.93f, 0f, w - 0.06f, 0.012f, d - 0.06f, top);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                MB(sx * (w * 0.5f - 0.05f), 0.45f, sz * (d * 0.5f - 0.05f), 0.05f, 0.9f, 0.05f, ShipMaterials.DarkMetal);
            MB(0f, 0.2f, 0f, w - 0.1f, 0.03f, d - 0.1f, ShipMaterials.Metal);
            MB(0f, 1.55f, -d * 0.5f + 0.02f, w, 1.2f, 0.03f, ShipMaterials.Panel);              // panel trasero
        }

        // ---------------- Nivel 4 · Ciencia y agricultura ----------------

        private static void Science()
        {
            GardenRack(Tr.T("Huerto hidropónico A"), ShipLayout.GardenA);
            GardenRack(Tr.T("Huerto hidropónico B"), ShipLayout.GardenB);

            // Laboratorio: campana de flujo, microscopio, centrifugadora, gradilla de tubos y espectrómetro.
            Machine(Tr.T("Laboratorio"), ShipLayout.Laboratory);
            WorkTable(1.9f, 0.8f, ShipMaterials.Wall);
            MB(-0.45f, 1.45f, -0.05f, 0.95f, 1.05f, 0.65f, X("Campana de flujo", new Color(0.88f, 0.9f, 0.92f), 0f, 0.5f));
            MB(-0.45f, 1.4f, 0.28f, 0.85f, 0.75f, 0.01f, ShipMaterials.Glass);
            MB(-0.45f, 1.92f, 0.29f, 0.85f, 0.05f, 0.02f, ShipMaterials.LedWhite);
            MC(0.25f, 1.02f, -0.1f, 0.14f, 0.16f, ShipMaterials.Wall);                         // centrifugadora
            MC(0.25f, 1.1f, -0.1f, 0.12f, 0.01f, ShipMaterials.ScreenBlue);
            MB(0.62f, 0.98f, 0.1f, 0.25f, 0.06f, 0.1f, ShipMaterials.Metal);                    // gradilla
            Color[] tubes = { new Color(0.9f, 0.3f, 0.3f), new Color(0.3f, 0.8f, 0.4f), new Color(0.3f, 0.5f, 0.95f), new Color(0.95f, 0.85f, 0.3f) };
            for (int k = 0; k < 4; k++) MC(0.53f + k * 0.06f, 1.06f, 0.1f, 0.012f, 0.14f, X("Tubo " + k, tubes[k], 0f, 0.8f));
            MB(0.7f, 1.12f, -0.22f, 0.32f, 0.35f, 0.28f, ShipMaterials.Panel);                  // espectrómetro
            MB(0.7f, 1.18f, -0.075f, 0.2f, 0.12f, 0.01f, ShipMaterials.ScreenGreen);
            EndMachine(new Vector3(0f, 1f, 0f), new Vector3(1.95f, 2f, 0.85f));

            // Cámara de germinación: armario con puerta de cristal, bandejas de plántulas y luz de cultivo.
            Machine("Cámara de germinación", ShipLayout.Germination);
            MB(0f, 1.0f, 0f, 1.2f, 2.0f, 0.6f, ShipMaterials.Wall);
            MB(0f, 1.05f, 0.302f, 1.05f, 1.7f, 0.01f, ShipMaterials.Glass);
            for (int k = 0; k < 4; k++)
            {
                float y = 0.4f + k * 0.42f;
                MB(0f, y, 0.05f, 1.0f, 0.03f, 0.45f, ShipMaterials.Metal);
                for (int p = 0; p < 6; p++) MB(-0.4f + p * 0.16f, y + 0.05f, 0.05f + (p % 2) * 0.08f, 0.07f, 0.07f, 0.07f, ShipMaterials.Plant);
                MB(0f, y + 0.36f, 0.05f, 0.95f, 0.02f, 0.3f, ShipMaterials.Grow);
            }
            MB(0.4f, 1.9f, 0.31f, 0.25f, 0.1f, 0.01f, ShipMaterials.ScreenGreen);
            EndMachine(Vector3.up, new Vector3(1.2f, 2f, 0.6f));

            Shelf(ShipLayout.SeedShelf, 7);

            LabelAt(ShipLayout.GardenA, 2.5f, Tr.T("Huerto hidropónico A"));
            LabelAt(ShipLayout.GardenB, 2.5f, Tr.T("Huerto hidropónico B"));
            LabelAt(ShipLayout.Laboratory, 2.2f, Tr.T("Laboratorio"));
            LabelAt(ShipLayout.Germination, 2.2f, "Cámara de germinación");
        }

        /// <summary>Estantería hidropónica: 3 bandejas con canales, luces LED de cultivo y depósito de nutrientes.</summary>
        private static void GardenRack(string name, ShipLayout.Spot spot)
        {
            Machine(name, spot);
            for (int sx = -1; sx <= 1; sx += 2)
            for (int sz = -1; sz <= 1; sz += 2)
                MB(sx * 1.12f, 1.2f, sz * 0.32f, 0.05f, 2.4f, 0.05f, ShipMaterials.Metal);
            MB(0f, 0.15f, -0.05f, 2.1f, 0.3f, 0.5f, X("Depósito de nutrientes", new Color(0.9f, 0.92f, 0.94f), 0f, 0.6f));
            MB(0.75f, 0.2f, 0.21f, 0.3f, 0.1f, 0.01f, ShipMaterials.ScreenGreen);
            foreach (float y in ShipLayout.GardenTrayHeights)
            {
                MB(0f, y - 0.06f, 0f, 2.2f, 0.12f, 0.6f, X("Bandeja hidropónica", new Color(0.93f, 0.94f, 0.95f), 0f, 0.55f));
                MB(0f, y, 0f, 2.1f, 0.012f, 0.5f, X("Sustrato", new Color(0.25f, 0.18f, 0.12f), 0f, 0.1f));
                MB(0f, y + 0.58f, 0f, 2.1f, 0.03f, 0.35f, ShipMaterials.Grow);                 // LED de cultivo
                MB(0f, y + 0.6f, 0f, 2.15f, 0.02f, 0.4f, ShipMaterials.DarkMetal);
            }
            ShipMesh.Beam(_m, new Vector3(-1.05f, 0.3f, -0.25f), new Vector3(-1.05f, 2.0f, -0.25f), 0.025f, ShipMaterials.Copper);
            ShipMesh.Beam(_m, new Vector3(1.05f, 0.3f, -0.25f), new Vector3(1.05f, 2.0f, -0.25f), 0.025f, ShipMaterials.Copper);
            EndMachine(new Vector3(0f, 1.2f, 0f), new Vector3(2.3f, 2.4f, 0.7f));
        }

        // ---------------- Nivel 5 · Recursos y supervivencia ----------------

        private static void Resources()
        {
            // Planta de agua: fusor de hielo, columnas de filtrado, bomba y panel de control.
            Machine(Tr.T("Planta de agua"), ShipLayout.WaterPlant);
            MB(0f, 0.06f, 0f, 1.9f, 0.12f, 1.2f, ShipMaterials.DarkMetal);
            MC(-0.5f, 0.9f, -0.15f, 0.42f, 1.5f, ShipMaterials.Frost);                         // fusor
            MC(-0.5f, 1.66f, -0.15f, 0.44f, 0.06f, ShipMaterials.Metal);
            for (int k = 0; k < 3; k++) MC(0.2f + k * 0.28f, 0.8f, -0.3f, 0.11f, 1.3f, ShipMaterials.Wall); // filtros
            MB(0.4f, 0.3f, 0.3f, 0.45f, 0.35f, 0.35f, ShipMaterials.Panel);                    // bomba
            MC(0.4f, 0.3f, 0.5f, 0.1f, 0.12f, ShipMaterials.Copper, new Vector3(90f, 0f, 0f));
            ShipMesh.Beam(_m, new Vector3(-0.1f, 1.4f, -0.15f), new Vector3(0.76f, 1.4f, -0.3f), 0.035f, ShipMaterials.Copper);
            ShipMesh.Beam(_m, new Vector3(0.2f, 0.2f, -0.3f), new Vector3(0.4f, 0.3f, 0.15f), 0.03f, ShipMaterials.Copper);
            MB(0.3f, 1.55f, 0.15f, 0.6f, 0.45f, 0.06f, ShipMaterials.Panel);
            MB(0.3f, 1.58f, 0.185f, 0.45f, 0.3f, 0.01f, ShipMaterials.ScreenBlue);
            EndMachine(new Vector3(0f, 0.9f, 0f), new Vector3(1.9f, 1.8f, 1.2f));

            // Generador de oxígeno: pila de electrólisis y depósitos de O2 (verde) y H2 (rojo).
            Machine(Tr.T("Generador de oxígeno"), ShipLayout.OxygenPlant);
            MB(0f, 0.06f, 0f, 1.7f, 0.12f, 1.0f, ShipMaterials.DarkMetal);
            for (int k = 0; k < 12; k++) MB(-0.3f, 0.3f + k * 0.07f, 0.1f, 0.6f, 0.05f, 0.5f, k % 2 == 0 ? ShipMaterials.Metal : ShipMaterials.Copper); // pila
            MB(-0.3f, 1.2f, 0.1f, 0.66f, 0.06f, 0.56f, ShipMaterials.Panel);
            for (int s = 0; s < 2; s++)
            {
                float x = 0.35f + s * 0.38f;
                MC(x, 0.95f, -0.15f, 0.16f, 1.5f, ShipMaterials.Wall);
                MC(x, 1.35f, -0.15f, 0.165f, 0.08f, s == 0 ? X("Franja O2", new Color(0.15f, 0.65f, 0.3f)) : X("Franja H2", new Color(0.8f, 0.15f, 0.12f)));
            }
            MB(-0.3f, 1.5f, 0.32f, 0.5f, 0.35f, 0.05f, ShipMaterials.Panel);
            MB(-0.3f, 1.52f, 0.348f, 0.38f, 0.22f, 0.01f, ShipMaterials.ScreenGreen);
            EndMachine(new Vector3(0f, 0.85f, 0f), new Vector3(1.7f, 1.7f, 1.0f));

            // Cocina: encimera con fregadero, horno, dispensador de agua y armarios altos.
            Machine(Tr.T("Cocina"), ShipLayout.Kitchen);
            MB(0f, 0.45f, 0f, 1.9f, 0.9f, 0.65f, ShipMaterials.Wall);
            MB(0f, 0.92f, 0f, 1.92f, 0.04f, 0.67f, X("Encimera", new Color(0.3f, 0.32f, 0.35f), 0.2f, 0.6f));
            MB(-0.45f, 0.9f, 0.02f, 0.45f, 0.03f, 0.38f, ShipMaterials.DarkMetal);              // fregadero
            ShipMesh.Beam(_m, new Vector3(-0.45f, 0.94f, -0.24f), new Vector3(-0.45f, 1.18f, -0.12f), 0.015f, ShipMaterials.Metal);
            MB(0.5f, 1.12f, -0.1f, 0.5f, 0.36f, 0.42f, ShipMaterials.Panel);                    // horno
            MB(0.45f, 1.12f, 0.115f, 0.32f, 0.24f, 0.01f, ShipMaterials.Glass);
            MB(0.72f, 1.12f, 0.115f, 0.06f, 0.2f, 0.01f, ShipMaterials.ScreenGreen);
            MB(0f, 1.95f, -0.12f, 1.9f, 0.6f, 0.4f, ShipMaterials.Wall);                         // armarios altos
            for (int k = 0; k < 3; k++) MB(-0.62f + k * 0.62f, 1.95f, 0.085f, 0.005f, 0.56f, 0.01f, ShipMaterials.DarkMetal);
            for (int k = 0; k < 4; k++) MB(-0.72f + k * 0.48f, 0.5f, 0.33f, 0.005f, 0.8f, 0.01f, ShipMaterials.DarkMetal);
            MB(0.05f, 0.95f, 0.12f, 0.3f, 0.015f, 0.2f, X("Tabla de cortar", new Color(0.75f, 0.6f, 0.4f)));
            ShipMesh.Prim(PrimitiveType.Sphere, _m, new Vector3(0.0f, 0.99f, 0.12f), Vector3.one * 0.06f, Quaternion.identity, X("Tomate", new Color(0.85f, 0.15f, 0.1f), 0f, 0.7f));
            ShipMesh.Prim(PrimitiveType.Sphere, _m, new Vector3(0.1f, 0.99f, 0.1f), new Vector3(0.1f, 0.07f, 0.1f), Quaternion.identity, ShipMaterials.Plant);
            EndMachine(new Vector3(0f, 1.1f, 0f), new Vector3(1.95f, 2.25f, 0.7f));

            // Despensa: frigorífico con puerta de cristal y raciones.
            Machine("Despensa", ShipLayout.Pantry);
            MB(0f, 1.0f, 0f, 1.0f, 2.0f, 0.65f, ShipMaterials.Wall);
            MB(0f, 1.05f, 0.326f, 0.88f, 1.75f, 0.01f, ShipMaterials.Glass);
            for (int k = 0; k < 4; k++)
            {
                float y = 0.35f + k * 0.42f;
                MB(0f, y, 0.05f, 0.85f, 0.02f, 0.5f, ShipMaterials.Metal);
                for (int p = 0; p < 4; p++) MB(-0.3f + p * 0.2f, y + 0.08f, 0.08f, 0.16f, 0.14f, 0.3f, X("Ración envasada", new Color(0.7f, 0.72f, 0.76f), 0.3f, 0.7f));
            }
            EndMachine(Vector3.up, new Vector3(1.0f, 2f, 0.65f));

            // Depósitos de agua potable en la pared delantera.
            Machine("Depósitos de agua", ShipLayout.WaterTanks);
            for (int k = 0; k < 3; k++)
            {
                MC(-0.7f + k * 0.7f, 0.95f, 0f, 0.3f, 1.8f, ShipMaterials.Water);
                MC(-0.7f + k * 0.7f, 1.88f, 0f, 0.31f, 0.06f, ShipMaterials.Metal);
            }
            ShipMesh.Beam(_m, new Vector3(-0.7f, 1.95f, 0f), new Vector3(0.7f, 1.95f, 0f), 0.03f, ShipMaterials.Copper);
            EndMachine(new Vector3(0f, 0.95f, 0f), new Vector3(2.1f, 1.9f, 0.62f));

            LabelAt(ShipLayout.WaterPlant, 2.0f, Tr.T("Planta de agua"));
            LabelAt(ShipLayout.OxygenPlant, 2.0f, "Generador de oxígeno (electrólisis)");
            LabelAt(ShipLayout.Kitchen, 2.4f, Tr.T("Cocina"));
            LabelAt(ShipLayout.Pantry, 2.2f, "Despensa");
            LabelAt(ShipLayout.WaterTanks, 2.1f, "Agua potable");
        }

        // ---------------- Nivel 6 · Control y comunicaciones ----------------

        private static void Control()
        {
            // Puestos de control mirando a las ventanas delanteras, con asiento.
            for (int k = 0; k < 4; k++)
            {
                float deg = -36f + k * 24f;
                BeginMachine("Puesto de control " + (k + 1), new ShipLayout.Spot(deg, 3.45f).Position, deg);
                MB(0f, 0.38f, 0f, 0.8f, 0.76f, 0.5f, ShipMaterials.Panel);
                MB(0f, 0.8f, 0.02f, 0.82f, 0.06f, 0.55f, ShipMaterials.DarkMetal);
                MB(-0.18f, 1.12f, 0.15f, 0.36f, 0.24f, 0.03f, ShipMaterials.ScreenBlue);
                MB(0.2f, 1.12f, 0.15f, 0.36f, 0.24f, 0.03f, k % 2 == 0 ? ShipMaterials.ScreenGreen : ShipMaterials.ScreenBlue);
                MB(0f, 0.86f, -0.12f, 0.4f, 0.02f, 0.14f, ShipMaterials.DarkMetal);              // teclado
                MB(0f, 0.45f, -0.7f, 0.46f, 0.08f, 0.46f, X("Asiento", new Color(0.18f, 0.2f, 0.24f), 0f, 0.3f));
                MB(0f, 0.8f, -0.92f, 0.46f, 0.62f, 0.07f, X("Asiento", new Color(0.18f, 0.2f, 0.24f), 0f, 0.3f));
                ShipMesh.Beam(_m, new Vector3(0f, 0.05f, -0.7f), new Vector3(0f, 0.42f, -0.7f), 0.04f, ShipMaterials.Metal);
                EndMachine(new Vector3(0f, 0.6f, -0.3f), new Vector3(0.85f, 1.2f, 1.2f));
            }

            // Ordenador central (armarios con LED) y comunicaciones.
            Machine("Ordenador central", ShipLayout.Servers);
            for (int k = 0; k < 3; k++)
            {
                MB(-0.6f + k * 0.6f, 1.0f, 0f, 0.56f, 2.0f, 0.55f, ShipMaterials.DarkMetal);
                for (int l = 0; l < 8; l++) MB(-0.6f + k * 0.6f - 0.18f, 0.4f + l * 0.2f, 0.278f, 0.04f, 0.02f, 0.005f, l % 3 == 0 ? ShipMaterials.ScreenBlue : ShipMaterials.ScreenGreen);
            }
            EndMachine(Vector3.up, new Vector3(1.8f, 2f, 0.55f));

            Machine("Comunicaciones", ShipLayout.Comms);
            WorkTable(1.6f, 0.7f, ShipMaterials.Panel);
            MB(0f, 1.15f, -0.15f, 0.9f, 0.4f, 0.3f, ShipMaterials.Panel);
            MB(0f, 1.18f, 0.005f, 0.7f, 0.28f, 0.01f, ShipMaterials.ScreenBlue);
            MC(0.55f, 1.0f, 0f, 0.06f, 0.12f, ShipMaterials.DarkMetal);
            EndMachine(new Vector3(0f, 1f, 0f), new Vector3(1.65f, 2f, 0.75f));

            Label(0f, 2.0f, 3.0f, "Puestos de control (ventanas)");
            LabelAt(ShipLayout.Servers, 2.2f, "Ordenador central");
            LabelAt(ShipLayout.Comms, 1.9f, "Comunicaciones");
        }

        private static void LabelAt(ShipLayout.Spot spot, float y, string text)
        {
            Vector3 p = spot.Position;
            Label(p.x, y, p.z, text);
        }
        // ------------------------------------------------------------------

        private static void B(float x, float y, float z, float sx, float sy, float sz, Material m)
        {
            ShipMesh.Box(_p, new Vector3(x, y, z), new Vector3(sx, sy, sz), m);
            // Los muebles grandes tienen colisión (los detalles pequeños no, para no atascar).
            if (sy >= 0.7f && Mathf.Max(sx, sz) >= 0.35f) Solid(new Vector3(x, y, z), new Vector3(sx, sy, sz));
        }

        private static void Solid(Vector3 center, Vector3 size)
        {
            _solids.Add(new Bounds(center, size));
        }

        private static void Label(float x, float y, float z, string text)
        {
            _model.AddLabel(_level, new Vector3(x, _y0 + y, z), text);
        }
    }
}
