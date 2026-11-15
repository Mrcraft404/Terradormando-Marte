using MarsRobot.Base.Ship;
using UnityEngine;

namespace MarsRobot.Visuals
{
    /// <summary>Tipos de atrezo disponibles.</summary>
    public enum PropKind
    {
        Toolbox, Crate, CableCoil, GasBottle, SampleCase, Tablet, Mug, PartsTray, Binder
    }

    /// <summary>
    /// Atrezo hecho por código (base apoyada en y = 0, frente hacia +Z). Cada pieza se fusiona en
    /// pocas mallas y tiene medidas reales (una caja de herramientas mide ~45 × 22 × 22 cm).
    /// </summary>
    public static class PropLibrary
    {
        private static Material _red, _orangePlastic, _greyPlastic, _rubber, _silver, _mugWhite, _paper;

        public static GameObject Create(PropKind kind)
        {
            ShipMaterials.Ensure();
            EnsureMaterials();
            var root = new GameObject("Atrezo: " + kind);
            Transform t = root.transform;
            switch (kind)
            {
                case PropKind.Toolbox:
                    Box(t, 0f, 0.09f, 0f, 0.45f, 0.18f, 0.22f, _red);
                    Box(t, 0f, 0.19f, 0f, 0.46f, 0.03f, 0.23f, _red);                 // tapa
                    Box(t, 0f, 0.235f, 0f, 0.2f, 0.025f, 0.03f, _rubber);             // asa
                    Box(t, -0.08f, 0.215f, 0f, 0.02f, 0.03f, 0.03f, _rubber);
                    Box(t, 0.08f, 0.215f, 0f, 0.02f, 0.03f, 0.03f, _rubber);
                    Box(t, -0.17f, 0.15f, 0.113f, 0.04f, 0.04f, 0.01f, _silver);       // cierres
                    Box(t, 0.17f, 0.15f, 0.113f, 0.04f, 0.04f, 0.01f, _silver);
                    break;
                case PropKind.Crate:
                    Box(t, 0f, 0.18f, 0f, 0.6f, 0.36f, 0.4f, _greyPlastic);
                    Box(t, 0f, 0.37f, 0f, 0.62f, 0.03f, 0.42f, _orangePlastic);       // tapa
                    for (int k = -1; k <= 1; k++) Box(t, k * 0.18f, 0.18f, 0.205f, 0.03f, 0.34f, 0.01f, _greyPlastic); // nervios
                    Box(t, -0.31f, 0.28f, 0f, 0.02f, 0.05f, 0.16f, _rubber);          // asas
                    Box(t, 0.31f, 0.28f, 0f, 0.02f, 0.05f, 0.16f, _rubber);
                    Box(t, 0f, 0.22f, 0.206f, 0.22f, 0.09f, 0.002f, _paper);          // etiqueta
                    break;
                case PropKind.CableCoil:
                    for (int k = 0; k < 14; k++)
                    {
                        float a0 = k * Mathf.PI * 2f / 14f, a1 = (k + 1) * Mathf.PI * 2f / 14f;
                        for (int layer = 0; layer < 3; layer++)
                        {
                            float r = 0.13f + layer * 0.025f, y = 0.025f + (layer % 2) * 0.02f;
                            ShipMesh.Beam(t, new Vector3(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), new Vector3(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), 0.012f, layer == 1 ? _orangePlastic : _rubber);
                        }
                    }
                    break;
                case PropKind.GasBottle:
                    ShipMesh.Cyl(t, new Vector3(0f, 0.2f, 0f), 0.065f, 0.36f, _silver);
                    ShipMesh.Prim(PrimitiveType.Sphere, t, new Vector3(0f, 0.38f, 0f), new Vector3(0.13f, 0.08f, 0.13f), Quaternion.identity, _silver);
                    ShipMesh.Cyl(t, new Vector3(0f, 0.3f, 0f), 0.067f, 0.04f, ShipMaterials.Orange);
                    ShipMesh.Cyl(t, new Vector3(0f, 0.44f, 0f), 0.02f, 0.06f, _rubber);
                    break;
                case PropKind.SampleCase:
                    Box(t, 0f, 0.06f, 0f, 0.42f, 0.12f, 0.3f, _silver);
                    Box(t, 0f, 0.061f, 0.151f, 0.4f, 0.004f, 0.002f, _rubber);         // junta
                    Box(t, 0f, 0.13f, 0.06f, 0.14f, 0.02f, 0.02f, _rubber);           // asa
                    Box(t, -0.12f, 0.08f, 0.152f, 0.03f, 0.03f, 0.006f, _rubber);
                    Box(t, 0.12f, 0.08f, 0.152f, 0.03f, 0.03f, 0.006f, _rubber);
                    break;
                case PropKind.Tablet:
                    Box(t, 0f, 0.006f, 0f, 0.26f, 0.012f, 0.18f, _rubber);
                    Box(t, 0f, 0.013f, 0f, 0.23f, 0.002f, 0.15f, ShipMaterials.ScreenBlue);
                    break;
                case PropKind.Mug:
                    ShipMesh.Cyl(t, new Vector3(0f, 0.05f, 0f), 0.04f, 0.1f, _mugWhite);
                    ShipMesh.Cyl(t, new Vector3(0f, 0.101f, 0f), 0.034f, 0.002f, _rubber);
                    Box(t, 0.048f, 0.055f, 0f, 0.012f, 0.05f, 0.012f, _mugWhite);
                    break;
                case PropKind.PartsTray:
                    Box(t, 0f, 0.012f, 0f, 0.3f, 0.024f, 0.2f, _orangePlastic);
                    Box(t, 0f, 0.04f, 0.095f, 0.3f, 0.06f, 0.01f, _orangePlastic);
                    Box(t, 0f, 0.04f, -0.095f, 0.3f, 0.06f, 0.01f, _orangePlastic);
                    Box(t, 0.145f, 0.04f, 0f, 0.01f, 0.06f, 0.2f, _orangePlastic);
                    Box(t, -0.145f, 0.04f, 0f, 0.01f, 0.06f, 0.2f, _orangePlastic);
                    for (int k = 0; k < 6; k++) ShipMesh.Cyl(t, new Vector3(-0.1f + (k % 3) * 0.1f, 0.03f, -0.05f + (k / 3) * 0.1f), 0.012f, 0.03f, _silver);
                    break;
                default: // Binder (carpeta de procedimientos)
                    Box(t, 0f, 0.02f, 0f, 0.24f, 0.04f, 0.31f, _orangePlastic);
                    Box(t, 0.005f, 0.02f, 0f, 0.225f, 0.034f, 0.3f, _paper);
                    break;
            }
            ShipMesh.Combine(t);
            return root;
        }

        private static void Box(Transform p, float x, float y, float z, float sx, float sy, float sz, Material m)
        {
            ShipMesh.Box(p, new Vector3(x, y, z), new Vector3(sx, sy, sz), m);
        }

        private static void EnsureMaterials()
        {
            if (_red != null) return;
            _red = ShipMesh.Mat("Atrezo rojo", new Color(0.72f, 0.12f, 0.1f), 0.2f, 0.5f);
            _orangePlastic = ShipMesh.Mat("Plástico naranja", new Color(0.95f, 0.48f, 0.12f), 0f, 0.45f);
            _greyPlastic = ShipMesh.Mat("Plástico gris", new Color(0.42f, 0.44f, 0.47f), 0f, 0.35f);
            _rubber = ShipMesh.Mat("Goma", new Color(0.08f, 0.08f, 0.09f), 0f, 0.25f);
            _silver = ShipMesh.Mat("Aluminio pulido", new Color(0.8f, 0.82f, 0.85f), 0.35f, 0.6f);
            _mugWhite = ShipMesh.Mat("Cerámica", new Color(0.92f, 0.92f, 0.9f), 0f, 0.6f);
            _paper = ShipMesh.Mat("Papel", new Color(0.95f, 0.94f, 0.9f), 0f, 0.1f);
        }
    }
}
