using System;
using System.Collections.Generic;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Infraestructura presurizada de la base.</summary>
    public enum MarsModuleType
    {
        CropDome,         // Domo de cultivo
        CrewHabitat,      // Hábitat de la tripulación
        IsruPlant,        // Planta ISRU (procesado de recursos in situ)
        RoboticsWorkshop  // Taller de robótica
    }

    /// <summary>Forma base del módulo: decide cómo se generan las capas.</summary>
    public enum MarsModuleShape
    {
        Dome,           // Cúpula lisa (semielipsoide), escudo en cúpula
        HorizontalTube, // Cilindro tumbado con extremos semiesféricos, escudo en bóveda
        Box,            // Nave rectangular, escudo en bóveda
        GeodesicDome    // Cúpula geodésica de paneles triangulares (barras + paneles)
    }

    /// <summary>Tipo de estructura esquelética (Capa 1).</summary>
    public enum HabitatSkeletonKind
    {
        TitaniumRibs,   // Costillas o barras rígidas de titanio
        InflatableCore  // Núcleo inflable: anillo de base + cinchas de tensión finas
    }

    /// <summary>Material de la contención (Capa 2).</summary>
    public enum HabitatShellKind
    {
        KevlarAerogel,        // Opaca: sándwich de Kevlar + aerogel (hábitats, talleres)
        TransparentLaminate   // Translúcida: vidrio laminado + ETFE con baja emisividad (invernaderos)
    }

    /// <summary>Material del escudo (Capa 3).</summary>
    public enum HabitatShieldKind
    {
        Regolith,  // Regolito compactado o sinterizado (opaco)
        WaterIce   // Hielo de agua en una membrana (translúcido: deja pasar la luz, frena la radiación)
    }

    /// <summary>Las 5 capas físicas obligatorias, en orden de construcción.</summary>
    public enum HabitatLayer
    {
        Foundation = 0,       // Capa 0: losa, suelo compactado y zapatas
        Skeleton = 1,         // Capa 1: costillas de titanio o núcleo inflable
        PressureShell = 2,    // Capa 2: contención neumática y térmica
        RegolithShield = 3,   // Capa 3: escudo (regolito o hielo) contra radiación y micrometeoritos
        InternalSystems = 4   // Capa 4: racks y sistemas internos
    }

    /// <summary>Cómo se construye una capa: prefab propio o geometría procedural.</summary>
    [Serializable]
    public class HabitatLayerDefinition
    {
        public HabitatLayer layer;

        [Tooltip("Prefab de la capa. Si se deja vacío se genera por código (geometría provisional).")]
        public GameObject prefab;

        [Tooltip("Material que sustituye al material procedural de la capa (opcional).")]
        public Material materialOverride;

        [Tooltip("Segundos que tarda en construirse en tiempo de juego (animación por capas).")]
        [Min(0f)] public float buildSeconds = 3f;

        public HabitatLayerDefinition(HabitatLayer layer, float seconds)
        {
            this.layer = layer;
            buildSeconds = seconds;
        }
    }

    /// <summary>Requisito de ingeniería (no tiene por qué existir como recurso del inventario).</summary>
    [Serializable]
    public struct HabitatMaterialRequirement
    {
        public HabitatLayer layer;
        public string material;
        public float amount;
        public string unit;

        public HabitatMaterialRequirement(HabitatLayer layer, string material, float amount, string unit)
        {
            this.layer = layer;
            this.material = material;
            this.amount = amount;
            this.unit = unit;
        }

        public override string ToString()
        {
            return Tr.T("Capa ") + (int)layer + " · " + material + ": " + amount.ToString("0.#") + " " + unit;
        }
    }

    /// <summary>Punto de conexión de un módulo (para encajar módulos con un pasillo presurizado).</summary>
    public struct HabitatAnchor
    {
        /// <summary>Posición local, a ras de suelo, en el borde exterior del escudo.</summary>
        public Vector3 localPosition;

        /// <summary>Dirección local hacia fuera (horizontal, normalizada).</summary>
        public Vector3 localDirection;

        public HabitatAnchor(Vector3 position, Vector3 direction)
        {
            localPosition = position;
            localDirection = direction.normalized;
        }
    }

    /// <summary>
    /// Configuración de un módulo de la base: forma, dimensiones reales, presión de diseño, capas,
    /// costes y holograma. Crear con Assets ▸ Create ▸ Marte ▸ Base ▸ Módulo de hábitat, o los cuatro
    /// de serie con Marte ▸ Base ▸ Crear catálogo de módulos.
    /// </summary>
    [CreateAssetMenu(fileName = "MarsModule", menuName = "Marte/Base/Módulo de hábitat")]
    public class MarsModuleData : ScriptableObject
    {
        public const int LayerCount = 5;

        // Constantes físicas usadas en las estimaciones.
        public const float RegolithDensityTonPerM3 = 1.6f;   // regolito compactado
        public const float IceDensityTonPerM3 = 0.92f;       // hielo de agua
        public const float TitaniumDensityKgPerM3 = 4500f;
        public const float SinteredRegolithDensityTonPerM3 = 2.2f;

        /// <summary>Reparto del coste del juego entre las capas al construir "por fases".</summary>
        private static readonly float[] LayerCostShare = { 0.15f, 0.2f, 0.2f, 0.35f, 0.1f };

        [Header("Identidad")]
        public string moduleId = "modulo";
        public string displayName = Tr.T("Módulo");
        [TextArea(2, 5)] public string description;
        public MarsModuleType type;
        public MarsModuleShape shape;

        [Header("Dimensiones reales (m)")]
        [Tooltip("Radio de la cúpula o del tubo (diámetro = 2 × radio).")]
        [Min(1f)] public float radius = 6f;
        [Tooltip("Altura de la cúpula sobre la losa. 0 = semiesfera (altura = radio).")]
        [Min(0f)] public float domeHeight;
        [Tooltip("Longitud del tubo o de la nave (eje Z).")]
        [Min(1f)] public float length = 12f;
        [Tooltip("Anchura de la nave rectangular (eje X).")]
        [Min(1f)] public float width = 10f;
        [Tooltip("Altura de la nave rectangular.")]
        [Min(1f)] public float height = 5f;
        [Min(0.1f)] public float foundationThickness = 0.6f;
        [Tooltip("Profundidad de las zapatas de anclaje bajo la losa.")]
        [Min(0f)] public float footingDepth = 1.2f;

        [Header("Ambiente interior de diseño")]
        [Tooltip("Presión interna de diseño. 101,3 kPa = nivel del mar terrestre; 55 kPa es habitual en invernaderos.")]
        [Min(0f)] public float designPressureKPa = 101.3f;
        [Range(0.15f, 0.5f)] public float oxygenFraction = 0.21f;
        public float targetTemperatureC = 21f;

        [Header("Capa 1 · Esqueleto")]
        public HabitatSkeletonKind skeleton = HabitatSkeletonKind.TitaniumRibs;
        [Min(0.02f)] public float ribThickness = 0.15f;
        [Min(0.5f)] public float ribSpacing = 1.5f;
        [Tooltip("Frecuencia de la cúpula geodésica (número de divisiones de cada cara del icosaedro). Par = corte exacto en el ecuador.")]
        [Range(2, 6)] public int geodesicFrequency = 4;

        [Header("Capa 2 · Contención")]
        public HabitatShellKind shellKind = HabitatShellKind.KevlarAerogel;
        [Min(1)] public int kevlarLayers = 4;
        [Tooltip("Espesor de cada capa de Kevlar (mm).")]
        [Min(0.1f)] public float kevlarLayerMm = 1f;
        [Tooltip("Espesor del aislante de aerogel (mm).")]
        [Min(0f)] public float aerogelMm = 25f;

        [Header("Capa 3 · Escudo")]
        public HabitatShieldKind shieldKind = HabitatShieldKind.Regolith;
        [Tooltip("Espesor del escudo (m). 1–3 m de regolito, o ~1 m de hielo de agua.")]
        [Min(0.1f)] public float shieldThickness = 2f;
        [Tooltip("Hueco entre la contención y el escudo (m), para inspección.")]
        [Min(0f)] public float shieldGap = 0.5f;

        [Header("Capas")]
        public HabitatLayerDefinition[] layers = DefaultLayers();

        [Header("Colocación (holograma)")]
        [Tooltip("Holograma propio. Si se deja vacío, se genera a partir de las capas con un material translúcido.")]
        public GameObject ghostPrefab;
        [Tooltip("Pendiente máxima del terreno bajo la losa (grados).")]
        [Range(1f, 30f)] public float maxSlopeDegrees = 8f;
        [Tooltip("Longitud del pasillo presurizado que une este módulo con otro al encajarlo (m).")]
        [Min(1f)] public float connectorLength = 4f;

        [Header("Coste en recursos del juego (inventario de la base)")]
        [Tooltip("Equivalencias con el inventario: regolito → bloques de regolito; titanio → piezas metálicas; " +
                 "aerogel y vidrio → silicio; hielo → hielo.")]
        public ResourceAmount[] gameCost = new ResourceAmount[0];

        [Header("Requisitos extra (se suman a la estimación automática)")]
        public List<HabitatMaterialRequirement> extraRequirements = new List<HabitatMaterialRequirement>();

        // ------------------------------------------------------------------
        // Geometría
        // ------------------------------------------------------------------

        public bool IsDomeLike
        {
            get { return shape == MarsModuleShape.Dome || shape == MarsModuleShape.GeodesicDome; }
        }

        /// <summary>Altura real de la cúpula sobre la losa.</summary>
        public float DomeHeight
        {
            get { return domeHeight > 0f ? domeHeight : radius; }
        }

        /// <summary>Altura libre de la cúpula a una distancia horizontal del centro (semielipsoide).</summary>
        public float DomeClearHeightAt(float horizontalDistance)
        {
            float t = horizontalDistance / radius;
            return t >= 1f ? 0f : DomeHeight * Mathf.Sqrt(1f - t * t);
        }

        public HabitatLayerDefinition GetLayer(HabitatLayer layer)
        {
            if (layers != null)
            {
                for (int i = 0; i < layers.Length; i++)
                {
                    if (layers[i] != null && layers[i].layer == layer) return layers[i];
                }
            }
            return new HabitatLayerDefinition(layer, 3f);
        }

        /// <summary>Dimensiones exteriores de la contención (Capa 2): ancho X, alto Y, largo Z.</summary>
        public Vector3 ShellSize
        {
            get
            {
                if (IsDomeLike) return new Vector3(radius * 2f, DomeHeight, radius * 2f);
                if (shape == MarsModuleShape.HorizontalTube) return new Vector3(radius * 2f, radius * 2f, length + radius * 2f);
                return new Vector3(width, height, length);
            }
        }

        /// <summary>Superficie aproximada de la contención (m²).</summary>
        public float ShellArea
        {
            get
            {
                if (IsDomeLike)
                {
                    // Medio esferoide (aproximación de Knud Thomsen, error < 1,1 %).
                    const float p = 1.6075f;
                    float a = Mathf.Pow(radius, p), c = Mathf.Pow(DomeHeight, p);
                    return 2f * Mathf.PI * Mathf.Pow((a * a + 2f * a * c) / 3f, 1f / p);
                }
                if (shape == MarsModuleShape.HorizontalTube) return 2f * Mathf.PI * radius * length + 4f * Mathf.PI * radius * radius;
                return 2f * (width * height + length * height + width * length);
            }
        }

        /// <summary>Volumen presurizado aproximado (m³).</summary>
        public float PressurizedVolume
        {
            get
            {
                if (IsDomeLike) return 2f / 3f * Mathf.PI * radius * radius * DomeHeight;
                if (shape == MarsModuleShape.HorizontalTube) return Mathf.PI * radius * radius * length + 4f / 3f * Mathf.PI * radius * radius * radius;
                return width * height * length;
            }
        }

        /// <summary>Radio horizontal interior del escudo (cúpula) o radio de la bóveda (tubo/nave), desde el suelo.</summary>
        public float ShieldInnerRadius
        {
            get
            {
                if (IsDomeLike) return radius + foundationThickness + shieldGap;
                if (shape == MarsModuleShape.HorizontalTube) return foundationThickness + radius * 2f + shieldGap;
                float halfW = width * 0.5f, top = foundationThickness + height;
                return Mathf.Sqrt(halfW * halfW + top * top) + shieldGap;
            }
        }

        /// <summary>Altura interior del escudo (en cúpulas puede ser menor que el radio).</summary>
        public float ShieldInnerHeight
        {
            get { return IsDomeLike ? DomeHeight + foundationThickness + shieldGap : ShieldInnerRadius; }
        }

        /// <summary>Semiejes de la huella total del módulo con su escudo (x, altura, z).</summary>
        public Vector3 FootprintHalfExtents
        {
            get
            {
                float ro = ShieldInnerRadius + shieldThickness;
                float top = ShieldInnerHeight + shieldThickness;
                if (IsDomeLike) return new Vector3(ro, top, ro);
                return new Vector3(ro, top, length * 0.5f + ro);
            }
        }

        /// <summary>Densidad del material del escudo (g/cm³).</summary>
        public float ShieldDensity
        {
            get { return shieldKind == HabitatShieldKind.WaterIce ? IceDensityTonPerM3 : RegolithDensityTonPerM3; }
        }

        /// <summary>Densidad superficial del escudo (g/cm²): 1 m de regolito = 160 g/cm²; 1 m de hielo = 92 g/cm².</summary>
        public float ShieldArealDensity
        {
            get { return shieldThickness * 100f * ShieldDensity; }
        }

        /// <summary>
        /// Coeficiente global de transmisión térmica de la contención (W/m²·K).
        /// Kevlar ≈ 0,04 W/m·K; aerogel en la baja presión de Marte ≈ 0,012 W/m·K;
        /// laminado transparente (vidrio triple + ETFE con baja emisividad) ≈ 0,9 W/m²·K.
        /// </summary>
        public float ShellUValue
        {
            get
            {
                if (shellKind == HabitatShellKind.TransparentLaminate) return 0.9f;
                float rKevlar = kevlarLayers * (kevlarLayerMm / 1000f) / 0.04f;
                float rAerogel = (aerogelMm / 1000f) / 0.012f;
                float r = rKevlar + rAerogel + 0.13f;
                return 1f / Mathf.Max(0.01f, r);
            }
        }

        /// <summary>
        /// Puntos de conexión (puertas): 4 alrededor de las cúpulas (pasillos en cruz entre los
        /// racks); en tubos y naves, uno en cada extremo (el pasillo central queda libre).
        /// El índice de la lista identifica la puerta y es estable.
        /// </summary>
        public List<HabitatAnchor> GetAnchors()
        {
            var list = new List<HabitatAnchor>();
            Vector3 fp = FootprintHalfExtents;
            if (IsDomeLike)
            {
                for (int i = 0; i < 4; i++)
                {
                    float a = i * Mathf.PI * 0.5f;
                    var dir = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));
                    list.Add(new HabitatAnchor(dir * fp.x, dir));
                }
            }
            else
            {
                list.Add(new HabitatAnchor(new Vector3(0f, 0f, fp.z), Vector3.forward));
                list.Add(new HabitatAnchor(new Vector3(0f, 0f, -fp.z), Vector3.back));
            }
            return list;
        }

        // ------------------------------------------------------------------
        // Puertas y aire interior
        // ------------------------------------------------------------------

        /// <summary>Ancho y alto libres de las puertas (m).</summary>
        public const float DoorWidth = 2.4f, DoorHeight = 2.5f;

        /// <summary>Altura del suelo interior (sobre el que se camina) respecto a la base del módulo.</summary>
        public float DoorFloorY
        {
            get { return shape == MarsModuleShape.HorizontalTube ? foundationThickness + radius * 0.35f : foundationThickness; }
        }

        /// <summary>Distancia desde el centro hasta la contención en la dirección de una puerta.</summary>
        public float ShellExtentAlong(HabitatAnchor anchor)
        {
            if (IsDomeLike) return radius;
            // Casquete a la altura del suelo interior (35 % del diámetro): √(1 − 0,65²) ≈ 0,76 del radio.
            if (shape == MarsModuleShape.HorizontalTube) return length * 0.5f + radius * 0.76f;
            return length * 0.5f;
        }

        /// <summary>Punto (local) del umbral de una puerta: en la contención y a la altura del suelo interior.</summary>
        public Vector3 DoorPoint(int anchorIndex)
        {
            List<HabitatAnchor> anchors = GetAnchors();
            if (anchorIndex < 0 || anchorIndex >= anchors.Count) return Vector3.zero;
            HabitatAnchor a = anchors[anchorIndex];
            return a.localDirection * ShellExtentAlong(a) + Vector3.up * DoorFloorY;
        }

        /// <summary>
        /// Caja (local) que se "abre" en el esqueleto, la contención y el escudo para una puerta:
        /// desde un poco antes de la contención hasta fuera del escudo, de 2,4 × 2,5 m.
        /// Devuelve centro, semiejes y giro.
        /// </summary>
        public void DoorCutBox(int anchorIndex, out Vector3 center, out Vector3 halfExtents, out Quaternion rotation)
        {
            List<HabitatAnchor> anchors = GetAnchors();
            HabitatAnchor a = anchors[Mathf.Clamp(anchorIndex, 0, anchors.Count - 1)];
            float start = ShellExtentAlong(a) - 1.2f;
            float end = a.localPosition.magnitude + 0.5f;
            float h = DoorHeight + 0.3f;
            center = a.localDirection * ((start + end) * 0.5f) + Vector3.up * (DoorFloorY - 0.25f + h * 0.5f);
            halfExtents = new Vector3(DoorWidth * 0.5f + 0.1f, h * 0.5f, (end - start) * 0.5f);
            rotation = Quaternion.LookRotation(a.localDirection, Vector3.up);
        }

        /// <summary>Caja (local) del aire interior: centro y tamaño. Se usa para saber si el astronauta respira.</summary>
        public void AtmosphereBox(out Vector3 center, out Vector3 size)
        {
            float f = foundationThickness;
            if (IsDomeLike)
            {
                size = new Vector3(radius * 1.7f, DomeHeight * 0.95f, radius * 1.7f);
                center = new Vector3(0f, f + size.y * 0.5f, 0f);
            }
            else if (shape == MarsModuleShape.HorizontalTube)
            {
                size = new Vector3(radius * 1.8f, radius * 1.8f, length + radius * 1.6f);
                center = new Vector3(0f, f + radius, 0f);
            }
            else
            {
                size = new Vector3(width - 0.2f, height - 0.1f, length - 0.2f);
                center = new Vector3(0f, f + size.y * 0.5f, 0f);
            }
        }

        /// <summary>Coste de una capa al construir por fases (parte proporcional del coste total, redondeada hacia arriba).</summary>
        public ResourceAmount[] GetLayerCost(HabitatLayer layer)
        {
            if (gameCost == null || gameCost.Length == 0) return new ResourceAmount[0];
            var result = new List<ResourceAmount>();
            float share = LayerCostShare[(int)layer];
            foreach (ResourceAmount c in gameCost)
            {
                int amount = Mathf.CeilToInt(c.amount * share);
                if (amount > 0) result.Add(new ResourceAmount(c.type, amount));
            }
            return result.ToArray();
        }

        // ------------------------------------------------------------------
        // Estimación de materiales reales por capa (a partir de la geometría)
        // ------------------------------------------------------------------

        public List<HabitatMaterialRequirement> EstimateRequirements()
        {
            var list = new List<HabitatMaterialRequirement>();
            Vector3 shell = ShellSize;
            float footprintArea = IsDomeLike
                ? Mathf.PI * (radius + 1f) * (radius + 1f)
                : (shell.x + 2f) * (shell.z + 2f);

            // Capa 0: losa de regolito sinterizado + zapatas.
            list.Add(new HabitatMaterialRequirement(HabitatLayer.Foundation, Tr.T("Regolito sinterizado (losa)"),
                footprintArea * foundationThickness * SinteredRegolithDensityTonPerM3, "t"));
            list.Add(new HabitatMaterialRequirement(HabitatLayer.Foundation, Tr.T("Zapatas de anclaje"), FootingCount(), "uds"));

            // Capa 1: longitud total de costillas o barras → masa de titanio.
            float ribLength = EstimateRibLength();
            float ribSection = Mathf.PI * Mathf.Pow(ribThickness * 0.5f, 2f) * 0.35f; // perfil hueco (~35 % macizo)
            if (skeleton == HabitatSkeletonKind.InflatableCore)
            {
                list.Add(new HabitatMaterialRequirement(HabitatLayer.Skeleton, Tr.T("Cinchas de Vectran"), ribLength, "m"));
                list.Add(new HabitatMaterialRequirement(HabitatLayer.Skeleton, Tr.T("Titanio (anillo de base)"),
                    2f * Mathf.PI * radius * ribSection * 4f * TitaniumDensityKgPerM3, "kg"));
            }
            else
            {
                string what = shape == MarsModuleShape.GeodesicDome ? Tr.T("Titanio (barras geodésicas)") : Tr.T("Titanio (costillas)");
                list.Add(new HabitatMaterialRequirement(HabitatLayer.Skeleton, what, ribLength * ribSection * TitaniumDensityKgPerM3, "kg"));
            }

            // Capa 2: contención.
            float area = ShellArea;
            if (shellKind == HabitatShellKind.TransparentLaminate)
            {
                list.Add(new HabitatMaterialRequirement(HabitatLayer.PressureShell, Tr.T("Vidrio laminado de silicato (paneles)"), area, "m²"));
                list.Add(new HabitatMaterialRequirement(HabitatLayer.PressureShell, Tr.T("Láminas de ETFE (2 capas)"), area * 2f, "m²"));
                list.Add(new HabitatMaterialRequirement(HabitatLayer.PressureShell, Tr.T("Película de baja emisividad"), area, "m²"));
            }
            else
            {
                list.Add(new HabitatMaterialRequirement(HabitatLayer.PressureShell, Tr.T("Tejido de Kevlar"), area * kevlarLayers, "m²"));
                list.Add(new HabitatMaterialRequirement(HabitatLayer.PressureShell, "Aerogel", area * aerogelMm / 1000f, "m³"));
                list.Add(new HabitatMaterialRequirement(HabitatLayer.PressureShell, Tr.T("Vejiga hermética (poliuretano)"), area, "m²"));
            }

            // Capa 3: escudo.
            float shieldTons = EstimateShieldVolume() * ShieldDensity;
            if (shieldKind == HabitatShieldKind.WaterIce)
            {
                list.Add(new HabitatMaterialRequirement(HabitatLayer.RegolithShield, Tr.T("Hielo de agua"), shieldTons, "t"));
                list.Add(new HabitatMaterialRequirement(HabitatLayer.RegolithShield, Tr.T("Membrana contenedora (ETFE)"), area * 2.2f, "m²"));
            }
            else
            {
                list.Add(new HabitatMaterialRequirement(HabitatLayer.RegolithShield, Tr.T("Regolito compactado"), shieldTons, "t"));
            }

            // Capa 4: equipamiento.
            list.Add(new HabitatMaterialRequirement(HabitatLayer.InternalSystems, InteriorLabel(), 1f, "lote"));

            if (extraRequirements != null) list.AddRange(extraRequirements);
            return list;
        }

        public int FootingCount()
        {
            if (IsDomeLike) return Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / 2.5f));
            Vector3 s = ShellSize;
            return Mathf.Max(4, 2 * (Mathf.RoundToInt(s.x / 3f) + Mathf.RoundToInt(s.z / 3f)));
        }

        private float EstimateRibLength()
        {
            switch (shape)
            {
                case MarsModuleShape.GeodesicDome:
                    // Semiesfera geodésica de frecuencia v: ~15,5·v² barras de longitud media ≈ 1,2·R/v → 18,6·v·R
                    // (comprobado con la malla real: 4V = 250 barras de 0,30·R de media).
                    return 18.6f * geodesicFrequency * radius;
                case MarsModuleShape.Dome:
                    int meridians = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * radius / (ribSpacing * 3f)));
                    return meridians * Mathf.PI * radius * 0.5f + 3f * 2f * Mathf.PI * radius * 0.75f;
                case MarsModuleShape.HorizontalTube:
                    int hoops = Mathf.Max(2, Mathf.RoundToInt(length / ribSpacing) + 1);
                    return hoops * 2f * Mathf.PI * radius + 6f * length;
                default:
                    int frames = Mathf.Max(2, Mathf.RoundToInt(length / ribSpacing) + 1);
                    return frames * (2f * height + width) + 4f * length;
            }
        }

        private float EstimateShieldVolume()
        {
            float ri = ShieldInnerRadius, ro = ri + shieldThickness;
            if (IsDomeLike)
            {
                float hi = ShieldInnerHeight, ho = hi + shieldThickness;
                return 2f / 3f * Mathf.PI * (ro * ro * ho - ri * ri * hi); // semielipsoides
            }
            float sphereShell = 4f / 3f * Mathf.PI * (ro * ro * ro - ri * ri * ri);
            // Bóveda: medio cilindro a lo largo del módulo + dos cuartos de esfera en los extremos.
            return 0.5f * Mathf.PI * (ro * ro - ri * ri) * length + sphereShell * 0.25f;
        }

        private string InteriorLabel()
        {
            switch (type)
            {
                case MarsModuleType.CropDome: return Tr.T("Racks hidropónicos con LED");
                case MarsModuleType.CrewHabitat: return Tr.T("Literas, cocina y estaciones de trabajo");
                case MarsModuleType.IsruPlant: return Tr.T("Tanques, electrolizador y reactor Sabatier");
                default: return Tr.T("Bancos de trabajo, brazo robótico y estanterías");
            }
        }

        // ------------------------------------------------------------------
        // Valores por defecto
        // ------------------------------------------------------------------

        public static HabitatLayerDefinition[] DefaultLayers()
        {
            return new[]
            {
                new HabitatLayerDefinition(HabitatLayer.Foundation, 3f),
                new HabitatLayerDefinition(HabitatLayer.Skeleton, 4f),
                new HabitatLayerDefinition(HabitatLayer.PressureShell, 3f),
                new HabitatLayerDefinition(HabitatLayer.RegolithShield, 5f),
                new HabitatLayerDefinition(HabitatLayer.InternalSystems, 3f),
            };
        }

        private void OnValidate()
        {
            // Siempre 5 capas, una por tipo y en orden: se conserva lo que el usuario haya asignado.
            var fixedLayers = new HabitatLayerDefinition[LayerCount];
            for (int i = 0; i < LayerCount; i++)
            {
                HabitatLayer layer = (HabitatLayer)i;
                HabitatLayerDefinition found = null;
                if (layers != null)
                {
                    foreach (HabitatLayerDefinition def in layers)
                    {
                        if (def != null && def.layer == layer) { found = def; break; }
                    }
                }
                fixedLayers[i] = found ?? new HabitatLayerDefinition(layer, 3f);
            }
            layers = fixedLayers;
            if (geodesicFrequency % 2 == 1) geodesicFrequency++; // par: la cúpula se corta justo en el ecuador
        }

        /// <summary>Crea en memoria uno de los cuatro módulos de serie (lo usa el menú y el builder sin assets).</summary>
        public static MarsModuleData CreateDefault(MarsModuleType type)
        {
            var d = CreateInstance<MarsModuleData>();
            d.type = type;
            switch (type)
            {
                case MarsModuleType.CropDome:
                    d.moduleId = "domo_cultivo";
                    d.displayName = Tr.T("Domo de cultivo");
                    d.description = Tr.T("Cúpula geodésica de 12 m de diámetro y 5,5 m de alto, acristalada y a baja presión ") +
                                    Tr.T("(55 kPa). Un escudo de hielo de agua deja pasar la luz y frena la radiación.");
                    d.shape = MarsModuleShape.GeodesicDome;
                    d.radius = 6f;
                    d.domeHeight = 5.5f;
                    d.geodesicFrequency = 4;
                    d.skeleton = HabitatSkeletonKind.TitaniumRibs;
                    d.ribThickness = 0.09f;
                    d.shellKind = HabitatShellKind.TransparentLaminate;
                    d.shieldKind = HabitatShieldKind.WaterIce;
                    d.shieldThickness = 1f;
                    d.shieldGap = 0.6f;
                    d.designPressureKPa = 55f;
                    d.oxygenFraction = 0.26f;
                    d.targetTemperatureC = 23f;
                    d.gameCost = new[] { new ResourceAmount(ResourceType.RegolithBricks, 25), new ResourceAmount(ResourceType.MetalParts, 18),
                                         new ResourceAmount(ResourceType.Silicon, 12), new ResourceAmount(ResourceType.Ice, 40) };
                    break;
                case MarsModuleType.CrewHabitat:
                    d.moduleId = "habitat_tripulacion";
                    d.displayName = Tr.T("Hábitat principal (tripulación)");
                    d.description = Tr.T("Tubo rígido a presión terrestre (101,3 kPa) con literas, cocina y estaciones de trabajo.");
                    d.shape = MarsModuleShape.HorizontalTube;
                    d.radius = 3f;
                    d.length = 12f;
                    d.skeleton = HabitatSkeletonKind.TitaniumRibs;
                    d.designPressureKPa = 101.3f;
                    d.oxygenFraction = 0.21f;
                    d.shieldThickness = 2.5f;
                    d.gameCost = new[] { new ResourceAmount(ResourceType.RegolithBricks, 50), new ResourceAmount(ResourceType.MetalParts, 25),
                                         new ResourceAmount(ResourceType.Silicon, 8), new ResourceAmount(ResourceType.Wiring, 12) };
                    break;
                case MarsModuleType.IsruPlant:
                    d.moduleId = "planta_isru";
                    d.displayName = Tr.T("Planta ISRU");
                    d.description = Tr.T("Nave de procesado: electrólisis de agua, reactor Sabatier (CO₂ + H₂ → CH₄) y tanques.");
                    d.shape = MarsModuleShape.Box;
                    d.width = 10f;
                    d.length = 14f;
                    d.height = 6f;
                    d.ribSpacing = 2f;
                    d.designPressureKPa = 70.3f;
                    d.shieldThickness = 1.5f;
                    d.gameCost = new[] { new ResourceAmount(ResourceType.RegolithBricks, 45), new ResourceAmount(ResourceType.MetalParts, 30),
                                         new ResourceAmount(ResourceType.Silicon, 6), new ResourceAmount(ResourceType.Wiring, 15) };
                    break;
                default:
                    d.moduleId = "taller_robotica";
                    d.displayName = Tr.T("Taller de robótica");
                    d.description = Tr.T("Nave para reparar y fabricar robots: bancos, brazo robótico y plataforma de carga.");
                    d.shape = MarsModuleShape.Box;
                    d.width = 12f;
                    d.length = 12f;
                    d.height = 6f;
                    d.ribSpacing = 2f;
                    d.designPressureKPa = 70.3f;
                    d.shieldThickness = 1.5f;
                    d.gameCost = new[] { new ResourceAmount(ResourceType.RegolithBricks, 40), new ResourceAmount(ResourceType.MetalParts, 28),
                                         new ResourceAmount(ResourceType.Silicon, 6), new ResourceAmount(ResourceType.Wiring, 18) };
                    break;
            }
            d.name = d.displayName;
            return d;
        }
    }
}
