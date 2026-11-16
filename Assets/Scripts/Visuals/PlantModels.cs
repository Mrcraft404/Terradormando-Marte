using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Visuals
{
    /// <summary>Planta construida: raíz (se escala al crecer) y frutos/flores (aparecen al madurar).</summary>
    public sealed class PlantInstance
    {
        public Transform Root;
        public readonly List<GameObject> Fruits = new List<GameObject>();
        public readonly List<Renderer> Ripening = new List<Renderer>(); // cambian de color al madurar
        public Color Unripe, Ripe;
    }

    /// <summary>
    /// Plantas del huerto hidropónico hechas por código, con proporciones reales (≤ 50 cm de alto,
    /// lo que cabe entre bandeja y luces LED). Especies cultivadas de verdad en el espacio: lechuga
    /// roja 'Outredgeous', rábano, tomate enano 'Red Robin', patata, trigo enano 'USU-Apogee' y soja.
    /// </summary>
    public static class PlantModels
    {
        private static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();

        private static Material M(string name, Color c, float smooth = 0.35f)
        {
            Material m;
            if (Mats.TryGetValue(name, out m) && m != null) return m;
            Shader s = Shader.Find("Standard");
            m = new Material(s) { name = name, color = c };
            m.SetFloat("_Glossiness", smooth);
            m.SetFloat("_Metallic", 0f);
            Mats[name] = m;
            return m;
        }

        public static PlantInstance Build(string species, Transform parent, int seed)
        {
            var p = new PlantInstance();
            p.Root = new GameObject("Planta: " + species).transform;
            p.Root.SetParent(parent, false);
            var rnd = new System.Random(seed);
            float R() { return (float)rnd.NextDouble(); }

            switch (species)
            {
                case "lechuga":
                    for (int i = 0; i < 11; i++)
                    {
                        float a = i * 137.5f, tilt = 25f + (i % 3) * 18f, s = 0.75f + R() * 0.4f;
                        bool red = i % 2 == 0;
                        Leaf(p.Root, new Vector3(0f, 0.03f + i * 0.004f, 0f), a, tilt, new Vector3(0.11f, 0.03f, 0.2f) * s,
                            red ? M("Lechuga roja", new Color(0.45f, 0.1f, 0.18f)) : M("Lechuga verde", new Color(0.38f, 0.62f, 0.2f)));
                    }
                    p.Unripe = p.Ripe = Color.white;
                    break;

                case "rabano":
                    for (int i = 0; i < 6; i++)
                        Leaf(p.Root, new Vector3(0f, 0.05f, 0f), i * 60f + R() * 20f, 55f, new Vector3(0.06f, 0.015f, 0.22f), M("Hoja de rábano", new Color(0.3f, 0.55f, 0.18f)));
                    var root = Part(PrimitiveType.Sphere, p.Root, new Vector3(0f, 0.03f, 0f), new Vector3(0.07f, 0.065f, 0.07f), Vector3.zero, M(Tr.T("Rábano"), new Color(0.85f, 0.12f, 0.28f), 0.6f));
                    p.Fruits.Add(root);
                    break;

                case "tomate":
                    Part(PrimitiveType.Cylinder, p.Root, new Vector3(0f, 0.22f, 0f), new Vector3(0.018f, 0.22f, 0.018f), Vector3.zero, M("Tallo", new Color(0.3f, 0.45f, 0.15f)));
                    for (int i = 0; i < 6; i++)
                        Leaf(p.Root, new Vector3(0f, 0.1f + i * 0.06f, 0f), i * 120f + R() * 30f, 60f, new Vector3(0.08f, 0.015f, 0.17f), M("Hoja de tomate", new Color(0.22f, 0.45f, 0.14f)));
                    p.Unripe = new Color(0.35f, 0.6f, 0.2f);
                    p.Ripe = new Color(0.88f, 0.12f, 0.08f);
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        var f = Part(PrimitiveType.Sphere, p.Root, new Vector3(Mathf.Cos(a) * 0.08f, 0.16f + (i % 3) * 0.07f, Mathf.Sin(a) * 0.08f),
                            Vector3.one * (0.06f + R() * 0.015f), Vector3.zero, new Material(M("Tomate", p.Unripe, 0.75f)));
                        p.Fruits.Add(f);
                        p.Ripening.Add(f.GetComponent<Renderer>());
                    }
                    break;

                case "patata":
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * 45f * Mathf.Deg2Rad, r = 0.04f + R() * 0.08f;
                        Part(PrimitiveType.Sphere, p.Root, new Vector3(Mathf.Cos(a) * r, 0.1f + R() * 0.18f, Mathf.Sin(a) * r),
                            new Vector3(0.16f, 0.1f, 0.16f) * (0.8f + R() * 0.4f), new Vector3(0f, R() * 360f, 0f), M("Hoja de patata", new Color(0.2f, 0.4f, 0.15f)));
                    }
                    for (int i = 0; i < 5; i++)
                    {
                        float a = i * 72f * Mathf.Deg2Rad;
                        var flower = Part(PrimitiveType.Sphere, p.Root, new Vector3(Mathf.Cos(a) * 0.07f, 0.32f + R() * 0.05f, Mathf.Sin(a) * 0.07f),
                            Vector3.one * 0.035f, Vector3.zero, M("Flor de patata", new Color(0.95f, 0.93f, 0.85f), 0.5f));
                        p.Fruits.Add(flower);
                    }
                    break;

                case "trigo":
                    p.Unripe = new Color(0.4f, 0.6f, 0.2f);
                    p.Ripe = new Color(0.88f, 0.72f, 0.32f);
                    for (int i = 0; i < 14; i++)
                    {
                        float a = R() * Mathf.PI * 2f, r = R() * 0.06f;
                        Vector3 basePos = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                        Vector3 tilt = new Vector3((R() - 0.5f) * 14f, 0f, (R() - 0.5f) * 14f);
                        float h = 0.36f + R() * 0.1f;
                        Part(PrimitiveType.Cylinder, p.Root, basePos + Vector3.up * h * 0.5f, new Vector3(0.008f, h * 0.5f, 0.008f), tilt, M("Tallo de trigo", new Color(0.45f, 0.6f, 0.22f)));
                        var ear = Part(PrimitiveType.Capsule, p.Root, basePos + Vector3.up * (h + 0.04f), new Vector3(0.022f, 0.045f, 0.022f), tilt, new Material(M("Espiga", p.Unripe)));
                        p.Fruits.Add(ear);
                        p.Ripening.Add(ear.GetComponent<Renderer>());
                    }
                    break;

                default: // soja
                    for (int i = 0; i < 9; i++)
                    {
                        float a = i * 40f, y = 0.08f + (i % 3) * 0.1f;
                        for (int k = 0; k < 3; k++) // hoja trifoliada
                            Leaf(p.Root, new Vector3(0f, y, 0f), a + (k - 1) * 28f, 45f, new Vector3(0.06f, 0.012f, 0.11f), M("Hoja de soja", new Color(0.3f, 0.5f, 0.18f)));
                    }
                    for (int i = 0; i < 7; i++)
                    {
                        float a = i * 51f * Mathf.Deg2Rad;
                        var pod = Part(PrimitiveType.Capsule, p.Root, new Vector3(Mathf.Cos(a) * 0.05f, 0.12f + (i % 3) * 0.08f, Mathf.Sin(a) * 0.05f),
                            new Vector3(0.022f, 0.045f, 0.018f), new Vector3(0f, i * 51f, 70f), M("Vaina de soja", new Color(0.62f, 0.7f, 0.35f)));
                        p.Fruits.Add(pod);
                    }
                    break;
            }
            return p;
        }

        /// <summary>Crecimiento 0..1: tamaño, aparición de frutos y maduración del color.</summary>
        public static void SetGrowth(PlantInstance p, float growth)
        {
            if (p == null || p.Root == null) return;
            float g = Mathf.Clamp01(growth);
            p.Root.localScale = Vector3.one * Mathf.Lerp(0.1f, 1f, Mathf.Pow(g, 0.7f));
            bool fruiting = g > 0.6f;
            foreach (GameObject f in p.Fruits) if (f != null && f.activeSelf != fruiting) f.SetActive(fruiting);
            float ripe = Mathf.InverseLerp(0.7f, 1f, g);
            foreach (Renderer r in p.Ripening) if (r != null) r.material.color = Color.Lerp(p.Unripe, p.Ripe, ripe);
        }

        private static void Leaf(Transform parent, Vector3 basePos, float yawDeg, float tiltDeg, Vector3 size, Material m)
        {
            Quaternion rot = Quaternion.Euler(0f, yawDeg, 0f) * Quaternion.Euler(-tiltDeg, 0f, 0f);
            Vector3 center = basePos + rot * new Vector3(0f, 0f, size.z * 0.45f);
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        private static GameObject Part(PrimitiveType type, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, Material m)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }
    }
}
