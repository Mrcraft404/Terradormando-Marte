using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base.Ship
{
    /// <summary>Paleta compartida de la nave (se crea una vez).</summary>
    public static class ShipMaterials
    {
        public static Material Steel, HeatShield, DarkMetal, Metal, Frost, TankDark, Glass, Floor, Wall, Hazard, LedWhite, Copper, Panel, ScreenBlue, ScreenGreen, Plant, Grow, Furnace, Water, Orange;

        public static void Ensure()
        {
            if (Steel != null) return;
            Steel = ShipMesh.Mat("Acero inoxidable", new Color(0.78f, 0.8f, 0.83f), 0.95f, 0.78f, ShipMesh.StainlessTexture(), new Vector2(1f, 1f));
            HeatShield = ShipMesh.Mat("Escudo térmico", new Color(0.55f, 0.55f, 0.58f), 0.1f, 0.25f, ShipMesh.HeatTileTexture(), new Vector2(6f, 1f));
            DarkMetal = ShipMesh.Mat("Metal oscuro", new Color(0.16f, 0.16f, 0.18f), 0.75f, 0.45f);
            Metal = ShipMesh.Mat("Aluminio", new Color(0.7f, 0.71f, 0.74f), 0.85f, 0.55f);
            Frost = ShipMesh.Mat("Tanque LOX", new Color(0.82f, 0.88f, 0.95f), 0.4f, 0.5f);
            TankDark = ShipMesh.Mat("Tanque CH4", new Color(0.45f, 0.47f, 0.5f), 0.8f, 0.5f);
            Glass = ShipMesh.Mat(Tr.T("Cristal"), new Color(0.05f, 0.08f, 0.12f), 0.3f, 0.95f);
            Floor = ShipMesh.Mat("Suelo de rejilla", Color.white, 0.7f, 0.4f, ShipMesh.GratingTexture(), new Vector2(8f, 8f));
            Wall = ShipMesh.Mat("Panel interior", new Color(0.88f, 0.89f, 0.9f), 0.1f, 0.45f);
            Hazard = ShipMesh.Mat("Señalización", Color.white, 0.1f, 0.3f, ShipMesh.HazardTexture(), new Vector2(2f, 1f));
            LedWhite = ShipMesh.Mat("LED blanco", Color.white, 0f, 0.5f, null, default(Vector2), new Color(1.6f, 1.6f, 1.55f));
            Copper = ShipMesh.Mat("Tubería", new Color(0.72f, 0.45f, 0.28f), 0.9f, 0.55f);
            Panel = ShipMesh.Mat("Consola", new Color(0.24f, 0.25f, 0.28f), 0.5f, 0.5f);
            ScreenBlue = ShipMesh.Mat("Pantalla azul", new Color(0.1f, 0.3f, 0.6f), 0f, 0.9f, null, default(Vector2), new Color(0.2f, 0.6f, 1.4f));
            ScreenGreen = ShipMesh.Mat("Pantalla verde", new Color(0.1f, 0.5f, 0.3f), 0f, 0.9f, null, default(Vector2), new Color(0.2f, 1.2f, 0.5f));
            Plant = ShipMesh.Mat("Plantas", new Color(0.2f, 0.55f, 0.2f), 0f, 0.3f);
            Grow = ShipMesh.Mat("LED de cultivo", new Color(0.8f, 0.3f, 0.8f), 0f, 0.5f, null, default(Vector2), new Color(1.4f, 0.4f, 1.4f));
            Furnace = ShipMesh.Mat("Horno", new Color(0.9f, 0.4f, 0.1f), 0f, 0.6f, null, default(Vector2), new Color(2f, 0.7f, 0.15f));
            Water = ShipMesh.Mat(Tr.T("Agua"), new Color(0.3f, 0.6f, 0.9f), 0.2f, 0.9f);
            Orange = ShipMesh.Mat("Naranja", new Color(0.95f, 0.5f, 0.12f), 0.3f, 0.5f);

            // Materiales PBR generados (texturas con albedo, normal, metálico/suavidad y oclusión) si
            // existen en Resources/GeneratedMaterials; si no, se quedan los procedurales.
            Steel = MarsRobot.Visuals.PBRMaterials.Override("Ship_Steel", Steel);
            HeatShield = MarsRobot.Visuals.PBRMaterials.Override("Ship_HeatShield", HeatShield);
            DarkMetal = MarsRobot.Visuals.PBRMaterials.Override("Ship_DarkMetal", DarkMetal);
            Metal = MarsRobot.Visuals.PBRMaterials.Override("Ship_Metal", Metal);
            Floor = MarsRobot.Visuals.PBRMaterials.Override("Ship_Floor", Floor);
            Wall = MarsRobot.Visuals.PBRMaterials.Override("Ship_Wall", Wall);
            Panel = MarsRobot.Visuals.PBRMaterials.Override("Ship_Panel", Panel);
        }
    }

    /// <summary>
    /// Construye el cohete nodriza (inspirado en las naves de acero reutilizables de 9 m de
    /// diámetro) convertido en base, según la distribución aprobada:
    ///  • Exterior: 52,5 m de alto, Ø 9 m, casco de acero con soldaduras, escudo térmico negro en la
    ///    cara trasera, aletas delanteras y traseras, 3 motores de nivel del mar + 3 de vacío, 6 patas,
    ///    puerta de carga a 24,5 m con plataforma elevadora para el robot y ventanas en la nariz.
    ///  • Interior técnico: tanque de oxígeno líquido, tanque de metano y estructura.
    ///  • Cinco niveles (ShipInteriorBuilder) entre 24,5 y 40,5 m.
    /// El casco está dividido en 24 sectores para poder abrirlo en la vista de corte.
    /// </summary>
    public static class ShipModelBuilder
    {
        private const int Sectors = 24;
        private const float SectorDeg = 360f / Sectors;
        private const float DoorBottom = ShipModel.FirstLevelFloor;
        private const float DoorTop = ShipModel.FirstLevelFloor + 3.0f;

        public static ShipModel Build(Transform parent)
        {
            ShipMaterials.Ensure();
            var root = new GameObject("Cohete nodriza (base)");
            root.transform.SetParent(parent, false);
            var model = root.AddComponent<ShipModel>();

            BuildHull(model);
            model.Exterior = Group(root.transform, "Exterior");
            BuildEngines(model.Exterior);
            BuildLegs(model.Exterior);
            BuildFlaps(model.Exterior);
            BuildElevatorAndDoor(model.Exterior);
            BuildWindows(model.Exterior);
            ShipMesh.Combine(model.Exterior);

            model.TechnicalInterior = Group(root.transform, "Interior técnico");
            BuildTanks(model);
            ShipMesh.Combine(model.TechnicalInterior);

            ShipInteriorBuilder.BuildLevels(model);
            root.AddComponent<ShipInspector>(); // tecla K: vista exterior, corte, niveles, etiquetas y cotas

            // Colisiones: la parte baja (tanques, motores) es maciza hasta el suelo del hangar; los
            // niveles interiores tienen suelos, paredes y muebles con colisión (ShipInteriorBuilder),
            // así el robot entra por la puerta de carga y el astronauta camina por dentro.
            var col = new GameObject("Colisión del casco");
            col.transform.SetParent(root.transform, false);
            var cap = col.AddComponent<CapsuleCollider>();
            cap.radius = ShipModel.OuterRadius;
            cap.height = ShipModel.FirstLevelFloor - ShipModel.HullBottom;
            cap.center = new Vector3(0f, ShipModel.HullBottom + cap.height * 0.5f, 0f);
            // La cápsula es redondeada abajo: este bloque impide que el robot se meta bajo el casco.
            var block = col.AddComponent<BoxCollider>();
            block.size = new Vector3(7.2f, 6.5f, 7.2f);
            block.center = new Vector3(0f, 3.25f, 0f);
            // Bajo el suelo del hangar (entre la cápsula y el primer suelo) y la nariz, macizos.
            var under = col.AddComponent<BoxCollider>();
            under.size = new Vector3(ShipModel.OuterRadius * 1.4f, 3f, ShipModel.OuterRadius * 1.4f);
            under.center = new Vector3(0f, ShipModel.FirstLevelFloor - 1.6f, 0f);
            float top = ShipModel.LevelFloorY(ShipModel.LevelCount - 1) + ShipModel.LevelHeight;
            var nose = col.AddComponent<CapsuleCollider>();
            nose.radius = ShipModel.HullRadiusAt(top + 1f) * 0.9f;
            nose.height = ShipModel.TotalHeight - top;
            nose.center = new Vector3(0f, top + nose.height * 0.5f, 0f);
            return model;
        }

        // ------------------------------------------------------------------
        // Casco (24 sectores de doble cara)
        // ------------------------------------------------------------------

        private static void BuildHull(ShipModel model)
        {
            Transform hull = Group(model.transform, "Casco");
            var nose = new List<Vector2>();
            const int noseSteps = 14;
            for (int i = 0; i <= noseSteps; i++)
            {
                float y = Mathf.Lerp(ShipModel.CylinderTop, ShipModel.TotalHeight, (float)i / noseSteps);
                nose.Add(new Vector2(ShipModel.HullRadiusAt(y), y));
            }

            for (int s = 0; s < Sectors; s++)
            {
                float from = -SectorDeg * 0.5f + s * SectorDeg;
                float center = from + SectorDeg * 0.5f;
                float wrapped = Mathf.DeltaAngle(0f, center);
                bool door = Mathf.Abs(wrapped) < 22.6f;                 // 3 sectores delanteros: hueco de la puerta
                bool belly = Mathf.Abs(wrapped) > 112f;                 // cara trasera: escudo térmico

                var parts = new List<Mesh>();
                if (door)
                {
                    parts.Add(Piece(new[] { V(ShipModel.HullBottom), V(DoorBottom) }, from));
                    parts.Add(Piece(new[] { V(DoorTop), V(ShipModel.CylinderTop) }, from));
                }
                else
                {
                    parts.Add(Piece(new[] { V(ShipModel.HullBottom), V(ShipModel.CylinderTop) }, from));
                }
                parts.Add(Piece(nose.ToArray(), from));

                // Cada tramo es su propio objeto (antes se fusionaban con CombineMeshes, que dejaba la
                // malla vacía: el casco no se veía y el interior quedaba a la vista).
                var go = new GameObject("Sector " + s);
                go.transform.SetParent(hull, false);
                Material mat = belly ? ShipMaterials.HeatShield : ShipMaterials.Steel;
                for (int k = 0; k < parts.Count; k++) ShipMesh.MeshObject(go.transform, Tr.T("Tramo ") + k, parts[k], mat);
                model.HullSectors.Add(new ShipModel.HullSector { go = go, centerDeg = center });
            }
        }

        private static Vector2 V(float y)
        {
            return new Vector2(ShipModel.OuterRadius, y);
        }

        private static Mesh Piece(Vector2[] profile, float from)
        {
            return ShipMesh.Lathe(profile, from, from + SectorDeg, 4, true, 0.08f, 0.25f);
        }

        // ------------------------------------------------------------------
        // Exterior
        // ------------------------------------------------------------------

        private static void BuildEngines(Transform p)
        {
            // Estructura de empuje bajo los tanques.
            ShipMesh.Cyl(p, new Vector3(0f, 4.2f, 0f), ShipModel.OuterRadius - 0.3f, 0.3f, ShipMaterials.DarkMetal);
            for (int i = 0; i < 3; i++)
            {
                // Motores de nivel del mar (centro) y de vacío (anillo exterior, toberas grandes).
                Vector3 sl = Quaternion.Euler(0f, i * 120f, 0f) * new Vector3(0f, 0f, 1.2f);
                Bell(p, sl, 0.65f, 1.9f, 3.7f);
                Vector3 vac = Quaternion.Euler(0f, 60f + i * 120f, 0f) * new Vector3(0f, 0f, 3.0f);
                Bell(p, vac, 1.15f, 1.6f, 3.95f);
            }
        }

        private static void Bell(Transform p, Vector3 pos, float exitRadius, float exitY, float throatY)
        {
            var prof = new[]
            {
                new Vector2(exitRadius, exitY),
                new Vector2(exitRadius * 0.82f, Mathf.Lerp(exitY, throatY, 0.4f)),
                new Vector2(exitRadius * 0.5f, Mathf.Lerp(exitY, throatY, 0.8f)),
                new Vector2(exitRadius * 0.32f, throatY),
            };
            GameObject bell = ShipMesh.MeshObject(p, "Tobera", ShipMesh.Lathe(prof, 0f, 360f, 20, true, 0.03f), ShipMaterials.DarkMetal);
            bell.transform.localPosition = new Vector3(pos.x, 0f, pos.z);
            ShipMesh.Cyl(p, new Vector3(pos.x, throatY + 0.25f, pos.z), 0.3f, 0.5f, ShipMaterials.Metal); // cabezal
        }

        private static void BuildLegs(Transform p)
        {
            for (int i = 0; i < 6; i++)
            {
                Quaternion q = Quaternion.Euler(0f, 30f + i * 60f, 0f);
                Vector3 top = q * new Vector3(0f, 4.0f, 4.8f);
                Vector3 foot = q * new Vector3(0f, 0.25f, 6.4f);
                Vector3 brace = q * new Vector3(0f, 1.8f, 4.95f);
                ShipMesh.Beam(p, top, foot, 0.18f, ShipMaterials.Steel);
                ShipMesh.Beam(p, brace, foot, 0.1f, ShipMaterials.Metal);
                ShipMesh.Cyl(p, new Vector3(foot.x, 0.1f, foot.z), 0.55f, 0.2f, ShipMaterials.DarkMetal);
            }
        }

        private static void BuildFlaps(Transform p)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float angle = side * 100f; // en los bordes del lado del escudo térmico
                Quaternion q = Quaternion.Euler(0f, angle, 0f);
                // Aleta trasera: 3 m de envergadura, 9 m de alto.
                Transform aft = ShipMesh.Box(p, q * new Vector3(0f, 7f, ShipModel.OuterRadius + 1.5f), new Vector3(0.25f, 9f, 3f), ShipMaterials.HeatShield);
                aft.localRotation = q;
                // Aleta delantera, inclinada siguiendo la nariz.
                float y = 41f;
                float r = ShipModel.HullRadiusAt(y);
                Transform fwd = ShipMesh.Box(p, q * new Vector3(0f, y, r + 1.0f), new Vector3(0.2f, 6f, 2f), ShipMaterials.HeatShield);
                fwd.localRotation = q * Quaternion.Euler(-14f, 0f, 0f);
                // Bisagras.
                ShipMesh.Box(p, q * new Vector3(0f, 7f, ShipModel.OuterRadius + 0.1f), new Vector3(0.5f, 8.5f, 0.3f), ShipMaterials.DarkMetal).localRotation = q;
            }
        }

        private static void BuildElevatorAndDoor(Transform p)
        {
            float z = ShipModel.OuterRadius + 0.15f;
            // Raíles del elevador desde el suelo hasta encima de la puerta, y grúa superior.
            for (int s = -1; s <= 1; s += 2)
            {
                ShipMesh.Box(p, new Vector3(s * 1.95f, 14.3f, z), new Vector3(0.22f, 28.6f, 0.22f), ShipMaterials.Metal);
                for (int k = 1; k < 9; k++)
                {
                    ShipMesh.Beam(p, new Vector3(s * 1.95f, k * 3.1f, z), new Vector3(s * 1.95f, k * 3.1f, z - 0.1f), 0.05f, ShipMaterials.DarkMetal);
                }
            }
            ShipMesh.Box(p, new Vector3(0f, 28.7f, z + 0.3f), new Vector3(4.4f, 0.5f, 0.9f), ShipMaterials.DarkMetal);
            // La plataforma móvil la crea ShipElevator (sube y baja con el robot).
            // Marco de la puerta de carga (3,2 m) con señalización.
            ShipMesh.Box(p, new Vector3(0f, DoorTop + 0.1f, ShipModel.OuterRadius - 0.05f), new Vector3(3.6f, 0.2f, 0.25f), ShipMaterials.Hazard);
            ShipMesh.Box(p, new Vector3(-1.7f, (DoorBottom + DoorTop) * 0.5f, ShipModel.OuterRadius - 0.3f), new Vector3(0.2f, 3f, 0.25f), ShipMaterials.Hazard);
            ShipMesh.Box(p, new Vector3(1.7f, (DoorBottom + DoorTop) * 0.5f, ShipModel.OuterRadius - 0.3f), new Vector3(0.2f, 3f, 0.25f), ShipMaterials.Hazard);
            // Hoja de la puerta, abierta hacia arriba.
            ShipMesh.Box(p, new Vector3(0f, DoorTop + 0.45f, ShipModel.OuterRadius + 0.05f), new Vector3(3.4f, 0.5f, 0.12f), ShipMaterials.Steel);
        }

        private static void BuildWindows(Transform p)
        {
            // Ventanas del nivel de control, en la base de la nariz, mirando al frente.
            float y = ShipModel.LevelFloorY(ShipModel.ControlLevel) + 1.6f;
            float r = ShipModel.HullRadiusAt(y) + 0.02f;
            for (int i = -2; i <= 2; i++)
            {
                if (i == 0) continue;
                float a = i * 11f;
                Quaternion q = Quaternion.Euler(0f, a, 0f);
                Transform w = ShipMesh.Box(p, q * new Vector3(0f, y, r), new Vector3(0.75f, 0.6f, 0.06f), ShipMaterials.Glass);
                w.localRotation = q;
            }
        }

        // ------------------------------------------------------------------
        // Interior técnico: tanques y anillos
        // ------------------------------------------------------------------

        private static void BuildTanks(ShipModel model)
        {
            Transform p = model.TechnicalInterior;
            Tank(p, 5.4f, 15.2f, ShipMaterials.Frost, "Tanque de oxígeno líquido");
            Tank(p, 16.2f, 23.2f, ShipMaterials.TankDark, "Tanque de metano");
            // Cúpula técnica / estructura bajo el hangar con cableado.
            ShipMesh.Cyl(p, new Vector3(0f, 23.9f, 0f), 4.3f, 0.6f, ShipMaterials.DarkMetal);
            // Anillos estructurales cada nivel.
            for (int i = 0; i <= ShipModel.LevelCount; i++)
            {
                float y = ShipModel.LevelFloorY(i);
                float r = ShipModel.HullRadiusAt(y) - 0.08f;
                var prof = new[] { new Vector2(r, y - 0.1f), new Vector2(r - 0.3f, y - 0.1f), new Vector2(r - 0.3f, y), new Vector2(r, y) };
                ShipMesh.MeshObject(p, "Anillo", ShipMesh.Lathe(prof, 0f, 360f, 48, true, 0.01f), ShipMaterials.DarkMetal);
            }
            model.AddLabel(0, new Vector3(0f, 10f, 0f), Tr.T("Tanque de oxígeno líquido (LOX) · 5–15 m"));
            model.AddLabel(0, new Vector3(0f, 19.5f, 0f), Tr.T("Tanque de metano (CH₄) · refinería ISRU · 16–23 m"));
            model.AddLabel(0, new Vector3(0f, 3f, 0f), Tr.T("Motores: 3 de nivel del mar + 3 de vacío"));
            model.AddLabel(0, new Vector3(0f, 46f, 0f), Tr.T("Nariz técnica: antenas y tanques de cabecera"));
        }

        private static void Tank(Transform p, float bottom, float top, Material mat, string name)
        {
            float r = ShipModel.InnerRadius + 0.2f;
            var prof = new List<Vector2> { new Vector2(0f, bottom - 1.1f) };
            for (int i = 1; i <= 5; i++)
            {
                float a = i / 5f * Mathf.PI * 0.5f;
                prof.Add(new Vector2(Mathf.Sin(a) * r, bottom - Mathf.Cos(a) * 1.1f));
            }
            prof.Add(new Vector2(r, top));
            for (int i = 1; i <= 5; i++)
            {
                float a = i / 5f * Mathf.PI * 0.5f;
                prof.Add(new Vector2(Mathf.Cos(a) * r, top + Mathf.Sin(a) * 1.1f));
            }
            ShipMesh.MeshObject(p, name, ShipMesh.Lathe(prof, 0f, 360f, 40, false), mat);
        }

        private static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }
    }
}
