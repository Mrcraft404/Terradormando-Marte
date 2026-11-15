using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Modelos PROVISIONALES de las estructuras con formas básicas de Unity. Todas se construyen
    /// con la base en y = 0, centradas en X/Z y mirando a +Z. Debajo llevan unos cimientos que se
    /// hunden en el terreno para que no se vean huecos en suelos irregulares.
    /// Para usar modelos definitivos, basta con sustituir Build() por un Instantiate del prefab.
    /// </summary>
    public static class StructureModels
    {
        private static Material _metal, _dark, _orange, _regolith, _solar, _glass, _glowCyan, _glowGreen, _white;

        /// <summary>Paleta compartida (se crea una vez: así no se duplican materiales con cada pieza).</summary>
        private static void EnsureMaterials()
        {
            if (_metal != null) return;
            _metal = Mat(new Color(0.58f, 0.6f, 0.62f), Color.black);
            _dark = Mat(new Color(0.15f, 0.15f, 0.17f), Color.black);
            _orange = Mat(new Color(0.95f, 0.5f, 0.1f), Color.black);
            _regolith = Mat(new Color(0.6f, 0.36f, 0.24f), Color.black);
            _solar = Mat(new Color(0.08f, 0.12f, 0.3f), Color.black);
            _glass = Mat(new Color(0.3f, 0.5f, 0.6f), Color.black);
            _white = Mat(new Color(0.9f, 0.9f, 0.88f), Color.black);
            _glowCyan = Mat(new Color(0.2f, 0.9f, 1f), new Color(0.1f, 0.7f, 1f) * 1.4f);
            _glowGreen = Mat(new Color(0.3f, 1f, 0.4f), new Color(0.2f, 0.9f, 0.3f) * 1.4f);
        }

        /// <summary>Material de color uniforme para la vista previa (verde = se puede, rojo = no).</summary>
        public static Material GhostMaterial(Color color)
        {
            return Mat(color, color * 0.6f);
        }

        /// <summary>
        /// Construye el modelo como hijo de <paramref name="parent"/>. Si <paramref name="overrideMat"/>
        /// no es null, todas las piezas lo usan (vista previa). Las piezas no llevan colisión: la
        /// colisión de la estructura es una caja en el objeto raíz.
        /// </summary>
        public static void Build(StructureDefinition def, Transform parent, Material overrideMat)
        {
            EnsureMaterials();
            Vector3 s = def.size;
            Material M(Material m) { return overrideMat != null ? overrideMat : m; }

            // Cimientos (se hunden 1 m) salvo en los suelos, que ya son planos.
            if (def.kind != StructureKind.FloorPlate && def.kind != StructureKind.LandingPad)
            {
                Part(PrimitiveType.Cube, parent, new Vector3(0f, -0.45f, 0f), new Vector3(s.x * 0.9f, 1.1f, s.z * 0.9f), M(_dark));
            }

            switch (def.kind)
            {
                case StructureKind.SolarArray:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.5f, 0f), new Vector3(0.2f, 1f, 0.2f), M(_metal));
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 1.1f, 0f), new Vector3(s.x, 0.08f, s.z), M(_solar))
                        .localRotation = Quaternion.Euler(-20f, 0f, 0f);
                    break;

                case StructureKind.BaseBattery:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, s.y * 0.5f, 0f), new Vector3(s.x, s.y, s.z), M(_white));
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, s.y * 0.6f, s.z * 0.51f), new Vector3(s.x * 0.6f, 0.2f, 0.02f), M(_glowGreen));
                    break;

                case StructureKind.FissionReactor:
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, s.y * 0.5f, 0f), new Vector3(s.x, s.y * 0.5f, s.z), M(_white));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, s.y + 0.1f, 0f), new Vector3(s.x * 0.7f, 0.1f, s.z * 0.7f), M(_glowCyan));
                    for (int i = 0; i < 4; i++) // radiadores
                    {
                        Quaternion r = Quaternion.Euler(0f, 90f * i, 0f);
                        Part(PrimitiveType.Cube, parent, r * new Vector3(0f, s.y * 0.55f, s.z * 0.75f), new Vector3(1.6f, 2f, 0.05f), M(_metal))
                            .localRotation = r;
                    }
                    break;

                case StructureKind.OxygenExtractor:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.9f, 0f), new Vector3(s.x, 1.8f, s.z), M(_metal));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0.6f, 2.0f, 0f), new Vector3(0.5f, 0.4f, 0.5f), M(_orange)); // toma de CO₂
                    Part(PrimitiveType.Sphere, parent, new Vector3(-0.6f, 2.0f, 0f), new Vector3(0.8f, 0.8f, 0.8f), M(_glowCyan));
                    break;

                case StructureKind.IceDrill:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.6f, 0f), new Vector3(s.x, 1.2f, s.z), M(_metal));
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 2.6f, 0f), new Vector3(0.4f, 2.8f, 0.4f), M(_orange)); // torre
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.4f, 0f), new Vector3(0.25f, 1f, 0.25f), M(_dark)); // broca
                    break;

                case StructureKind.AtmosphericCondenser:
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.1f, 0f), new Vector3(s.x * 0.8f, 1.1f, s.z * 0.8f), M(_metal));
                    for (int i = 0; i < 6; i++) // aletas
                    {
                        Quaternion r = Quaternion.Euler(0f, 60f * i, 0f);
                        Part(PrimitiveType.Cube, parent, r * new Vector3(0f, 1.3f, s.z * 0.45f), new Vector3(0.6f, 1.8f, 0.04f), M(_white))
                            .localRotation = r;
                    }
                    break;

                case StructureKind.OxygenTank:
                    Part(PrimitiveType.Capsule, parent, new Vector3(0f, s.y * 0.5f, 0f), new Vector3(s.x, s.y * 0.5f, s.z), M(_white));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, s.y * 0.55f, 0f), new Vector3(s.x * 1.02f, 0.1f, s.z * 1.02f), M(_glowCyan));
                    break;

                case StructureKind.WaterTank:
                    Part(PrimitiveType.Sphere, parent, new Vector3(0f, s.y * 0.5f, 0f), new Vector3(s.x, s.y, s.z), M(_white));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, s.y * 0.5f, 0f), new Vector3(s.x * 1.02f, 0.08f, s.z * 1.02f), M(_glass));
                    break;

                case StructureKind.RegolithPrinter:
                    Part(PrimitiveType.Cube, parent, new Vector3(-s.x * 0.45f, 1.25f, 0f), new Vector3(0.2f, 2.5f, 0.2f), M(_metal));
                    Part(PrimitiveType.Cube, parent, new Vector3(s.x * 0.45f, 1.25f, 0f), new Vector3(0.2f, 2.5f, 0.2f), M(_metal));
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 2.45f, 0f), new Vector3(s.x, 0.15f, 0.3f), M(_orange)); // pórtico
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 0.35f, 0f), new Vector3(s.x * 0.7f, 0.7f, s.z * 0.7f), M(_regolith)); // pieza impresa
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 2.0f, -s.z * 0.35f), new Vector3(0.9f, 0.45f, 0.9f), M(_dark)); // tolva
                    break;

                case StructureKind.RegolithWall:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, s.y * 0.5f, 0f), s, M(_regolith));
                    break;

                case StructureKind.FloorPlate:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, -0.3f, 0f), new Vector3(s.x, 0.8f, s.z), M(_regolith));
                    break;

                case StructureKind.RadiationShelter:
                    Part(PrimitiveType.Sphere, parent, new Vector3(0f, 0f, 0f), new Vector3(s.x, s.y * 2f, s.z), M(_regolith)); // bóveda
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 1f, s.z * 0.5f), new Vector3(1.4f, 2f, 1.2f), M(_metal)); // esclusa
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 1f, s.z * 0.5f + 0.61f), new Vector3(1f, 1.6f, 0.02f), M(_orange));
                    break;

                case StructureKind.LandingPad:
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, -0.35f, 0f), new Vector3(s.x, 0.5f, s.z), M(_dark));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.16f, 0f), new Vector3(s.x * 0.6f, 0.01f, s.z * 0.6f), M(_orange));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 0.17f, 0f), new Vector3(s.x * 0.5f, 0.01f, s.z * 0.5f), M(_dark));
                    for (int i = 0; i < 4; i++) // balizas
                    {
                        Quaternion r = Quaternion.Euler(0f, 45f + 90f * i, 0f);
                        Part(PrimitiveType.Cylinder, parent, r * new Vector3(0f, 0.3f, s.z * 0.48f), new Vector3(0.3f, 0.3f, 0.3f), M(_glowGreen));
                    }
                    break;

                case StructureKind.SampleLab:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 1.2f, 0f), new Vector3(s.x, 2.4f, s.z), M(_white));
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 1.4f, s.z * 0.51f), new Vector3(s.x * 0.7f, 0.7f, 0.02f), M(_glass));
                    Part(PrimitiveType.Sphere, parent, new Vector3(s.x * 0.3f, 2.5f, 0f), new Vector3(0.6f, 0.4f, 0.6f), M(_glowCyan));
                    break;

                case StructureKind.FuelRefinery:
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 1.2f, 0f), new Vector3(s.x, 2.4f, s.z), M(_metal));
                    Part(PrimitiveType.Cylinder, parent, new Vector3(-s.x * 0.3f, 2.9f, 0f), new Vector3(0.9f, 0.7f, 0.9f), M(_white)); // reactor Sabatier
                    Part(PrimitiveType.Cylinder, parent, new Vector3(s.x * 0.3f, 2.9f, 0f), new Vector3(0.7f, 0.7f, 0.7f), M(_orange)); // electrolizador
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 1.4f, s.z * 0.51f), new Vector3(s.x * 0.5f, 0.3f, 0.02f), M(_glowCyan));
                    break;

                case StructureKind.CommBeacon:
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, 1.4f, 0f), new Vector3(0.12f, 1.4f, 0.12f), M(_metal));
                    Part(PrimitiveType.Cube, parent, new Vector3(0f, 1.2f, 0f), new Vector3(0.8f, 0.05f, 0.5f), M(_solar)).localRotation = Quaternion.Euler(-25f, 0f, 0f);
                    Part(PrimitiveType.Sphere, parent, new Vector3(0f, 2.9f, 0f), new Vector3(0.25f, 0.25f, 0.25f), M(_glowGreen));
                    break;

                case StructureKind.CommsAntenna:
                    Part(PrimitiveType.Cylinder, parent, new Vector3(0f, s.y * 0.45f, 0f), new Vector3(0.25f, s.y * 0.45f, 0.25f), M(_metal));
                    Part(PrimitiveType.Sphere, parent, new Vector3(0f, s.y * 0.92f, 0.3f), new Vector3(1.8f, 1.8f, 0.4f), M(_white)) // plato
                        .localRotation = Quaternion.Euler(-30f, 0f, 0f);
                    Part(PrimitiveType.Sphere, parent, new Vector3(0f, s.y, 0f), new Vector3(0.25f, 0.25f, 0.25f), M(_glowGreen));
                    break;
            }
        }

        private static Transform Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            // Inmediato: la vista previa comprueba choques en este mismo frame y no debe chocar consigo misma.
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            return go.transform;
        }

        private static Material Mat(Color color, Color emission)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader);
            m.color = color;
            if (emission.maxColorComponent > 0f)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission);
            }
            return m;
        }
    }
}
