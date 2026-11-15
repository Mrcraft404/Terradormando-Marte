using UnityEngine;

namespace MarsRobot.Base.Ship
{
    /// <summary>
    /// Los cinco niveles interiores según la distribución aprobada (Ø interior 8,2 m, 3,2 m entre
    /// suelos). Coordenadas locales de cada nivel: suelo en y = 0, +Z = frente (puerta), núcleo de
    /// escalera y montacargas detrás (x ±1,1, z −3,9…−2,3) en todos los niveles.
    /// Todas las piezas son estáticas y se fusionan por material al final de cada nivel.
    /// </summary>
    public static class ShipInteriorBuilder
    {
        private static Transform _p;
        private static ShipModel _model;
        private static int _level;
        private static float _y0;

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

                CommonStructure(i);
                switch (i)
                {
                    case 0: Hangar(); break;
                    case 1: Industry(); break;
                    case 2: Science(); break;
                    case 3: Resources(); break;
                    default: Control(); break;
                }
                ShipMesh.Combine(root);

                // Luz del nivel (apagada: la enciende el inspector al mirar dentro).
                var lightGo = new GameObject("Luz LED");
                lightGo.transform.SetParent(root, false);
                lightGo.transform.localPosition = new Vector3(0f, 2.6f, 0f);
                var light = lightGo.AddComponent<Light>();
                light.type = LightType.Point;
                light.range = 7.5f;
                light.intensity = 1.6f;
                light.color = new Color(0.95f, 0.97f, 1f);
                light.enabled = false;
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
            Label(0f, 2.4f, -3.1f, "Núcleo: escalera de caracol + montacargas");
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
            // Delantal de la puerta de carga.
            B(0f, 0.09f, 3.6f, 3.2f, 0.02f, 0.9f, ShipMaterials.Hazard);

            Label(0f, 0.6f, 0.3f, "Hangar del robot · 4,5 × 5 m (taller integrado)");
            Label(0f, 2.9f, -1.6f, "Pórtico: cambio de ruedas, patas y herramientas");
            Label(-3.1f, 2.2f, 0f, "Estación de carga de baterías");
            Label(3.05f, 2.9f, 0f, "Almacén de repuestos · 1,1 × 2,8 m");
            Label(-2.0f, 1.2f, -2.65f, "Bancos de trabajo");
            Label(0f, 1.6f, 3.7f, "Puerta de carga 3,2 m → plataforma elevadora");
        }

        // ------------------------------------------------------------------
        // Nivel 2 — Industria y procesamiento
        // ------------------------------------------------------------------

        private static void Industry()
        {
            // Horno de fundición (2,8 × 3 m): cuerpo, boca incandescente y conducto de humos.
            B(-2.0f, 0.05f, 0.4f, 2.8f, 0.1f, 3f, ShipMaterials.Hazard);
            ShipMesh.Cyl(_p, new Vector3(-2.2f, 1.0f, 0.4f), 0.95f, 2.0f, ShipMaterials.DarkMetal);
            B(-1.35f, 0.8f, 0.4f, 0.1f, 0.6f, 0.8f, ShipMaterials.Furnace);
            ShipMesh.Beam(_p, new Vector3(-2.2f, 2.0f, 0.4f), new Vector3(-3.0f, 2.95f, 0.4f), 0.2f, ShipMaterials.Metal);
            B(-1.0f, 0.5f, 1.5f, 0.8f, 1.0f, 0.6f, ShipMaterials.Panel); // crisoles / control
            // Almacén de minerales (2,8 × 3 m): contenedores con mena.
            for (int k = 0; k < 4; k++)
            {
                float x = 1.3f + (k % 2) * 1.35f, z = -0.4f + (k / 2) * 1.5f;
                B(x, 0.45f, z, 1.2f, 0.9f, 1.3f, ShipMaterials.Metal);
                B(x, 0.92f, z, 1.0f, 0.06f, 1.1f, k % 2 == 0 ? ShipMaterials.Orange : ShipMaterials.DarkMetal);
            }
            // Clasificación de minerales + mesa de fabricación (compartida, 3 × 1,4 m): cinta y banco.
            B(0f, 0.85f, 2.9f, 3f, 0.08f, 1.2f, ShipMaterials.Metal);
            B(-0.7f, 0.95f, 2.9f, 1.4f, 0.05f, 0.5f, ShipMaterials.DarkMetal); // cinta transportadora
            B(0.8f, 1.2f, 2.9f, 0.5f, 0.5f, 0.5f, ShipMaterials.Panel);           // escáner de minerales
            for (int sx = -1; sx <= 1; sx += 2) B(sx * 1.4f, 0.42f, 2.9f, 0.08f, 0.85f, 1.1f, ShipMaterials.Metal);
            // Impresora 3D (1,75 × 1,6 m) junto al montacargas.
            B(-2.08f, 1.0f, -2.0f, 1.6f, 2.0f, 1.4f, ShipMaterials.Panel);
            B(-2.08f, 1.0f, -1.28f, 1.1f, 1.1f, 0.03f, ShipMaterials.ScreenBlue);
            // Herramientas y fabricación de componentes robóticos (atrás a la derecha).
            B(2.08f, 0.45f, -2.0f, 1.6f, 0.9f, 1.2f, ShipMaterials.Panel);
            B(2.08f, 1.5f, -2.65f, 1.6f, 1.2f, 0.05f, ShipMaterials.Metal);
            B(2.08f, 1.1f, -2.0f, 0.3f, 0.3f, 0.3f, ShipMaterials.Orange); // brazo de ensamblaje

            Label(-2.0f, 2.3f, 0.4f, "Horno de fundición · 2,8 × 3 m");
            Label(2.0f, 1.4f, 0.4f, "Almacén de minerales · 2,8 × 3 m");
            Label(0f, 1.6f, 2.9f, "Clasificación + mesa de fabricación · 3 × 1,4 m");
            Label(-2.08f, 2.2f, -2.0f, "Impresión 3D · 1,75 × 1,6 m");
            Label(2.08f, 2.2f, -2.0f, "Herramientas y componentes robóticos");
        }

        // ------------------------------------------------------------------
        // Nivel 3 — Ciencia y agricultura
        // ------------------------------------------------------------------

        private static void Science()
        {
            // Invernadero experimental (3 × 3,8 m): estantes con cultivos y LED de cultivo.
            B(-0.6f, 1.3f, 0f, 0.04f, 2.6f, 3.8f, ShipMaterials.Glass); // mampara
            for (int row = 0; row < 2; row++)
            {
                float x = -3.1f + row * 1.4f;
                for (int lvl = 0; lvl < 3; lvl++)
                {
                    float y = 0.5f + lvl * 0.8f;
                    B(x, y, 0f, 0.9f, 0.06f, 3.4f, ShipMaterials.Metal);
                    B(x, y + 0.12f, 0f, 0.8f, 0.18f, 3.2f, ShipMaterials.Plant);
                    B(x, y + 0.65f, 0f, 0.7f, 0.03f, 3.2f, ShipMaterials.Grow);
                }
            }
            // Laboratorio de análisis de muestras y geológico (3 × 2 m): mesa, microscopio, espectrómetro.
            B(2.1f, 0.85f, 0.9f, 2.6f, 0.08f, 1.0f, ShipMaterials.Wall);
            B(2.1f, 0.42f, 0.9f, 2.5f, 0.84f, 0.9f, ShipMaterials.Panel);
            ShipMesh.Cyl(_p, new Vector3(1.5f, 1.1f, 0.9f), 0.12f, 0.4f, ShipMaterials.DarkMetal);
            B(2.6f, 1.15f, 0.9f, 0.6f, 0.5f, 0.5f, ShipMaterials.Metal);
            // Laboratorio de biología (3 × 1,6 m): campana de flujo y nevera.
            B(2.1f, 0.9f, -1.1f, 2.4f, 1.8f, 1.0f, ShipMaterials.Wall);
            B(2.1f, 1.3f, -0.58f, 1.6f, 0.6f, 0.03f, ShipMaterials.Glass);
            B(0.75f, 1.3f, 0f, 0.04f, 2.6f, 3.6f, ShipMaterials.Glass); // mampara labs
            // Semillas + cámara de germinación (3 × 1,4 m): armarios refrigerados.
            for (int k = 0; k < 3; k++)
            {
                B(-1.0f + k * 1.0f, 1.0f, 3.0f, 0.9f, 2.0f, 0.8f, ShipMaterials.Wall);
                B(-1.0f + k * 1.0f, 1.2f, 2.58f, 0.6f, 0.9f, 0.03f, k == 2 ? ShipMaterials.Grow : ShipMaterials.ScreenBlue);
            }
            // Almacén de muestras y estanterías de experimentos (atrás).
            B(2.08f, 1.0f, -2.5f, 1.6f, 2.0f, 0.6f, ShipMaterials.Metal);
            B(-2.08f, 1.0f, -2.5f, 1.6f, 2.0f, 0.6f, ShipMaterials.Metal);

            Label(-2.4f, 2.4f, 0f, "Invernadero experimental · 3 × 3,8 m");
            Label(2.1f, 1.7f, 0.9f, "Lab. de análisis de muestras y geológico");
            Label(2.1f, 2.1f, -1.1f, "Laboratorio de biología");
            Label(0f, 2.3f, 3.0f, "Semillas + cámara de germinación");
            Label(2.08f, 2.3f, -2.5f, "Almacén de muestras");
            Label(-2.08f, 2.3f, -2.5f, "Estanterías de experimentos");
        }

        // ------------------------------------------------------------------
        // Nivel 4 — Recursos y supervivencia
        // ------------------------------------------------------------------

        private static void Resources()
        {
            // Extracción de agua (hielo → agua): fusor y tanque.
            ShipMesh.Cyl(_p, new Vector3(-2.6f, 1.1f, 1.0f), 0.7f, 2.2f, ShipMaterials.Metal);
            B(-1.4f, 0.7f, 1.0f, 0.8f, 1.4f, 1.2f, ShipMaterials.Panel);
            // Reciclaje y purificación de agua: filtros en batería.
            for (int k = 0; k < 4; k++) ShipMesh.Cyl(_p, new Vector3(-3.0f + k * 0.6f, 0.9f, -1.0f), 0.22f, 1.8f, ShipMaterials.Wall);
            B(-1.8f, 1.9f, -1.0f, 2.6f, 0.15f, 0.3f, ShipMaterials.Copper);
            // Tratamiento de aire + generador de oxígeno (electrólisis).
            B(2.0f, 1.0f, 1.0f, 2.4f, 2.0f, 1.4f, ShipMaterials.Wall);
            for (int k = 0; k < 3; k++) B(1.3f + k * 0.7f, 1.4f, 0.28f, 0.5f, 0.5f, 0.03f, ShipMaterials.DarkMetal); // rejillas
            ShipMesh.Cyl(_p, new Vector3(3.2f, 1.1f, 1.0f), 0.3f, 2.2f, ShipMaterials.ScreenBlue);
            // Sala de baterías y distribución eléctrica.
            for (int k = 0; k < 4; k++) B(1.1f + k * 0.65f, 1.0f, -1.0f, 0.55f, 2.0f, 1.0f, ShipMaterials.Panel);
            for (int k = 0; k < 4; k++) B(1.1f + k * 0.65f, 1.7f, -0.48f, 0.3f, 0.08f, 0.02f, ShipMaterials.ScreenGreen);
            // Almacén de agua (frente): depósitos.
            for (int k = 0; k < 3; k++) ShipMesh.Cyl(_p, new Vector3(-1.0f + k * 1.0f, 0.9f, 3.0f), 0.42f, 1.8f, ShipMaterials.Water);
            // Depósito de alimentos y cuadro eléctrico (atrás).
            B(-2.08f, 1.0f, -2.5f, 1.6f, 2.0f, 0.6f, ShipMaterials.Wall);
            B(2.08f, 1.1f, -2.6f, 1.4f, 2.2f, 0.4f, ShipMaterials.Panel);
            B(2.08f, 1.4f, -2.38f, 0.8f, 0.6f, 0.02f, ShipMaterials.ScreenBlue);

            Label(-2.2f, 2.4f, 1.0f, "Planta de extracción de agua");
            Label(-1.8f, 2.2f, -1.0f, "Reciclaje y purificación de agua");
            Label(2.0f, 2.3f, 1.0f, "Tratamiento de aire + generador de O₂");
            Label(2.0f, 2.3f, -1.0f, "Baterías y distribución eléctrica");
            Label(0f, 2.0f, 3.0f, "Almacén de agua");
            Label(-2.08f, 2.3f, -2.5f, "Depósito de alimentos");
        }

        // ------------------------------------------------------------------
        // Nivel 5 — Control y comunicaciones (base de la nariz, con ventanas al frente)
        // ------------------------------------------------------------------

        private static void Control()
        {
            // Centro de control (4 × 3 m) mirando a las ventanas: consolas en arco y asientos.
            for (int k = -2; k <= 2; k++)
            {
                Quaternion q = Quaternion.Euler(0f, k * 15f, 0f);
                Transform c = ShipMesh.Box(_p, q * new Vector3(0f, 0.5f, 2.9f), new Vector3(0.8f, 1.0f, 0.6f), ShipMaterials.Panel);
                c.localRotation = q;
                Transform s = ShipMesh.Box(_p, q * new Vector3(0f, 1.1f, 3.15f), new Vector3(0.7f, 0.45f, 0.04f), k % 2 == 0 ? ShipMaterials.ScreenBlue : ShipMaterials.ScreenGreen);
                s.localRotation = q * Quaternion.Euler(-15f, 0f, 0f);
                if (k != 0) B(Mathf.Sin(k * 15f * Mathf.Deg2Rad) * 2.1f, 0.45f, Mathf.Cos(k * 15f * Mathf.Deg2Rad) * 2.1f, 0.5f, 0.9f, 0.5f, ShipMaterials.DarkMetal);
            }
            // Computadora central (racks, izquierda).
            for (int k = 0; k < 4; k++) B(-1.2f - k * 0.62f, 1.0f, -0.9f, 0.55f, 2.0f, 1.0f, ShipMaterials.DarkMetal);
            for (int k = 0; k < 4; k++) B(-1.2f - k * 0.62f, 1.2f, -0.38f, 0.4f, 1.2f, 0.02f, ShipMaterials.ScreenGreen);
            // Laboratorio de electrónica (derecha): banco con osciloscopios.
            B(2.1f, 0.85f, -0.9f, 2.4f, 0.08f, 1.0f, ShipMaterials.Wall);
            B(2.1f, 0.42f, -0.9f, 2.3f, 0.84f, 0.9f, ShipMaterials.Panel);
            B(1.6f, 1.1f, -1.1f, 0.4f, 0.3f, 0.3f, ShipMaterials.ScreenBlue);
            B(2.4f, 1.1f, -1.1f, 0.4f, 0.3f, 0.3f, ShipMaterials.ScreenGreen);
            // Comunicaciones (atrás izquierda) y planificación de expediciones (mesa con mapa, atrás derecha).
            B(-2.0f, 1.0f, -2.5f, 1.4f, 2.0f, 0.6f, ShipMaterials.Panel);
            B(-2.0f, 1.3f, -2.18f, 1.0f, 0.6f, 0.02f, ShipMaterials.ScreenBlue);
            B(2.0f, 0.8f, -2.4f, 1.4f, 0.1f, 0.9f, ShipMaterials.ScreenGreen);
            B(2.0f, 0.4f, -2.4f, 1.2f, 0.8f, 0.7f, ShipMaterials.Panel);
            // Panel de monitoreo de la base en la pared frontal del núcleo.
            B(0f, 1.6f, -2.28f, 2.0f, 1.0f, 0.04f, ShipMaterials.ScreenBlue);

            Label(0f, 2.0f, 2.6f, "Centro de control · 4 × 3 m (ventanas)");
            Label(-2.1f, 2.3f, -0.9f, "Computadora central");
            Label(2.1f, 1.6f, -0.9f, "Laboratorio de electrónica");
            Label(-2.0f, 2.3f, -2.5f, "Estación de comunicaciones");
            Label(2.0f, 1.3f, -2.4f, "Planificación de expediciones");
            Label(0f, 2.4f, -2.2f, "Panel de monitoreo de la base");
        }

        // ------------------------------------------------------------------

        private static void B(float x, float y, float z, float sx, float sy, float sz, Material m)
        {
            ShipMesh.Box(_p, new Vector3(x, y, z), new Vector3(sx, sy, sz), m);
        }

        private static void Label(float x, float y, float z, string text)
        {
            _model.AddLabel(_level, new Vector3(x, _y0 + y, z), text);
        }
    }
}
