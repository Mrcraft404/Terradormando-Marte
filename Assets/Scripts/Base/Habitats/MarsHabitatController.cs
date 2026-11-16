using System;
using System.Text;
using MarsRobot.Core;
using MarsRobot.World.TerrainSystem;
using MarsRobot.World.Weather;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>Problemas de integridad de un hábitat (se pueden combinar).</summary>
    [Flags]
    public enum HabitatIssue
    {
        None = 0,
        NoFoundation = 1 << 0,      // falta la Capa 0: sin anclaje, el módulo se asienta y se agrieta
        NoSkeleton = 1 << 1,        // falta la Capa 1: la membrana no tiene forma ni soporte
        Depressurized = 1 << 2,     // falta o está rota la Capa 2: fuga de aire
        RadiationExposed = 1 << 3,  // falta o está rota la Capa 3: radiación y micrometeoritos
        NoInternalSystems = 1 << 4  // falta la Capa 4: el módulo no sirve para su función
    }

    /// <summary>
    /// Validación física de un hábitat construido por capas:
    ///  • Comprueba qué capas existen y funcionan (hijos con <see cref="HabitatLayerNode"/>).
    ///  • Sin Capa 2 → aviso de DESPRESURIZACIÓN y la presión interna cae (en juego).
    ///  • Sin Capa 3 → aviso de vulnerabilidad a la RADIACIÓN y a los micrometeoritos.
    ///  • Calcula presión interna, dosis de radiación dentro y pérdidas de calor.
    /// Funciona también en el Editor ([ExecuteAlways]): si borras una capa en la jerarquía, avisa.
    /// Solo escribe en la consola cuando el estado CAMBIA (no repite avisos cada frame).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public sealed class MarsHabitatController : MonoBehaviour
    {
        [SerializeField] private MarsModuleData data;
        [SerializeField] private bool logToConsole = true;

        [Tooltip("Puertas abiertas (índices de MarsModuleData.GetAnchors) que tienen un pasillo conectado.")]
        [SerializeField] private System.Collections.Generic.List<int> openings = new System.Collections.Generic.List<int>();

        [Header("Simulación de presión (en juego)")]
        [Tooltip("Constante de tiempo de la fuga sin contención (s de juego reales): la presión cae a ~37 % en ese tiempo.")]
        [SerializeField, Min(1f)] private float leakTimeConstant = 45f;
        [Tooltip("Velocidad de represurización con la contención intacta (kPa/s).")]
        [SerializeField, Min(0.01f)] private float repressurizeRate = 3f;

        private HabitatIssue _issues;
        private bool _validatedOnce;
        private int _batchDepth;
        private bool _pendingValidation;
        private float _recheckTimer;

        private static readonly System.Collections.Generic.List<MarsHabitatController> Registry =
            new System.Collections.Generic.List<MarsHabitatController>();

        /// <summary>Todos los hábitats activos de la escena (para encajar módulos sin buscar en toda la escena).</summary>
        public static System.Collections.Generic.IReadOnlyList<MarsHabitatController> All
        {
            get { return Registry; }
        }

        /// <summary>El estado de integridad ha cambiado (nuevo, anterior).</summary>
        public event Action<HabitatIssue, HabitatIssue> IssuesChanged;

        /// <summary>La presión interna ha caído por debajo del 50 % de la de diseño.</summary>
        public event Action Depressurized;

        public MarsModuleData Data
        {
            get { return data; }
        }

        public HabitatIssue Issues
        {
            get { return _issues; }
        }

        public bool IsComplete
        {
            get { return _issues == HabitatIssue.None; }
        }

        /// <summary>True mientras el builder está levantando capas (no se valida ni se avisa).</summary>
        public bool IsUnderConstruction
        {
            get { return _batchDepth > 0; }
        }

        /// <summary>Fase de la obra para la interfaz ("Construyendo Capa 2…", "esperando materiales", "Terminado").</summary>
        public string ConstructionStatus { get; set; } = "";

        /// <summary>Presión de diseño (kPa) para la interfaz.</summary>
        public float DesignPressureKPa
        {
            get { return data != null ? data.designPressureKPa : 0f; }
        }

        /// <summary>Presión interna respecto a la de diseño (0..1), para barras de la interfaz.</summary>
        public float PressureRatio
        {
            get { return DesignPressureKPa > 0f ? Mathf.Clamp01(InternalPressureKPa / DesignPressureKPa) : 0f; }
        }

        public bool IsPressureTight
        {
            get { return IsFunctional(HabitatLayer.PressureShell); }
        }

        public bool IsRadiationShielded
        {
            get { return IsFunctional(HabitatLayer.RegolithShield); }
        }

        /// <summary>Presión interna actual (kPa). En el Editor: la de diseño si es hermético, 0 si no.</summary>
        public float InternalPressureKPa { get; private set; }

        public void Initialize(MarsModuleData moduleData)
        {
            data = moduleData;
            InternalPressureKPa = 0f;
            BindAtmosphere();
        }

        // ------------------------------------------------------------------
        // Puertas y aire
        // ------------------------------------------------------------------

        /// <summary>Puertas abiertas (con pasillo conectado).</summary>
        public System.Collections.Generic.IReadOnlyList<int> Openings
        {
            get { return openings; }
        }

        public bool HasOpening(int anchorIndex)
        {
            return openings.Contains(anchorIndex);
        }

        /// <summary>Marca una puerta como abierta. Devuelve false si ya lo estaba.</summary>
        public bool AddOpening(int anchorIndex)
        {
            if (openings.Contains(anchorIndex)) return false;
            openings.Add(anchorIndex);
            return true;
        }

        /// <summary>Volumen de aire interior del módulo (para el soporte vital del astronauta).</summary>
        public PressurizedVolume Atmosphere { get; private set; }

        /// <summary>True si dentro se puede respirar sin traje (presión total y de oxígeno suficientes).</summary>
        public bool IsBreathable
        {
            get { return Atmosphere != null && Atmosphere.IsBreathable; }
        }

        public float OxygenFraction
        {
            get { return data != null ? data.oxygenFraction : 0.21f; }
        }

        /// <summary>Crea o vuelve a enlazar el volumen de aire con la presión real del módulo.</summary>
        private void BindAtmosphere()
        {
            if (data == null) return;
            Vector3 center, size;
            data.AtmosphereBox(out center, out size);
            Atmosphere = PressurizedVolume.Attach(gameObject, data.displayName, center, size,
                () => InternalPressureKPa, () => OxygenFraction);
        }

        // ------------------------------------------------------------------
        // Capas
        // ------------------------------------------------------------------

        /// <summary>Nodo de la capa (hijo directo), o null si no existe.</summary>
        public HabitatLayerNode GetLayerNode(HabitatLayer layer)
        {
            for (int i = 0; i < transform.childCount; i++)
            {
                var node = transform.GetChild(i).GetComponent<HabitatLayerNode>();
                if (node != null && node.layer == layer) return node;
            }
            return null;
        }

        public bool IsFunctional(HabitatLayer layer)
        {
            HabitatLayerNode node = GetLayerNode(layer);
            return node != null && node.IsFunctional;
        }

        /// <summary>Agrupa varios cambios de capa en una sola validación (lo usa el builder).</summary>
        public void BeginBatch()
        {
            _batchDepth++;
        }

        public void EndBatch()
        {
            _batchDepth = Mathf.Max(0, _batchDepth - 1);
            if (_batchDepth == 0)
            {
                if (Application.isPlaying && IsPressureTight && InternalPressureKPa <= 0f && data != null)
                {
                    InternalPressureKPa = 0f; // se represuriza poco a poco en Update
                }
                Validate(false);
            }
        }

        public void NotifyLayerChanged(HabitatLayer layer)
        {
            RequestValidation();
        }

        /// <summary>Pide revalidar en cuanto sea posible (sin hacerlo varias veces en el mismo frame).</summary>
        public void RequestValidation()
        {
            if (_batchDepth > 0) return;
            _pendingValidation = true;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.EditorApplication.delayCall -= DeferredValidate;
                UnityEditor.EditorApplication.delayCall += DeferredValidate;
            }
#endif
        }

        private void DeferredValidate()
        {
            if (this == null) return; // destruido mientras tanto
            if (_pendingValidation && _batchDepth == 0) Validate(false);
        }

        // ------------------------------------------------------------------
        // Validación
        // ------------------------------------------------------------------

        /// <summary>
        /// Comprueba las 5 capas. Avisa en la consola de lo que haya cambiado desde la última vez
        /// (o de todo, si <paramref name="forceLog"/>). Devuelve los problemas actuales.
        /// </summary>
        [ContextMenu("Validar integridad")]
        public void ValidateFromMenu()
        {
            Validate(true);
        }

        public HabitatIssue Validate(bool forceLog)
        {
            _pendingValidation = false;
            var issues = HabitatIssue.None;
            if (!IsFunctional(HabitatLayer.Foundation)) issues |= HabitatIssue.NoFoundation;
            if (!IsFunctional(HabitatLayer.Skeleton)) issues |= HabitatIssue.NoSkeleton;
            if (!IsFunctional(HabitatLayer.PressureShell)) issues |= HabitatIssue.Depressurized;
            if (!IsFunctional(HabitatLayer.RegolithShield)) issues |= HabitatIssue.RadiationExposed;
            if (!IsFunctional(HabitatLayer.InternalSystems)) issues |= HabitatIssue.NoInternalSystems;

            if (!Application.isPlaying)
            {
                // En el Editor no se simula: hermético = presión de diseño.
                InternalPressureKPa = (issues & HabitatIssue.Depressurized) == 0 && data != null ? data.designPressureKPa : 0f;
            }

            HabitatIssue previous = _issues;
            bool firstTime = !_validatedOnce;
            bool changed = issues != previous || firstTime;
            _issues = issues;
            _validatedOnce = true;

            if (logToConsole && (forceLog || changed))
            {
                // La primera vez (o si se pide) se informa de todo; después, solo de lo nuevo y lo resuelto.
                HabitatIssue appeared = forceLog || firstTime ? issues : issues & ~previous;
                HabitatIssue solved = firstTime ? HabitatIssue.None : previous & ~issues;
                LogIssues(issues, appeared, solved, forceLog);
            }
            if (changed && IssuesChanged != null) IssuesChanged(issues, previous);
            return issues;
        }

        private void LogIssues(HabitatIssue all, HabitatIssue appeared, HabitatIssue solved, bool forceLog)
        {
            string who = "[Base] " + name + ": ";
            float design = data != null ? data.designPressureKPa : 0f;

            if ((appeared & HabitatIssue.Depressurized) != 0)
                Debug.LogWarning(who + "falta o está rota la Capa 2 (contención neumática y térmica). RIESGO DE DESPRESURIZACIÓN: " +
                                 Tr.T("la presión interna caerá desde ") + design.ToString("0.#") + Tr.T(" kPa hasta la de Marte (~0,6 kPa)."), this);
            if ((appeared & HabitatIssue.RadiationExposed) != 0)
                Debug.LogWarning(who + "falta o está rota la Capa 3 (escudo). Interior VULNERABLE a la radiación cósmica galáctica, " +
                                 Tr.T("a las tormentas de partículas solares y a los micrometeoritos."), this);
            if ((appeared & HabitatIssue.NoFoundation) != 0)
                Debug.LogWarning(who + "falta la Capa 0 (cimentación): sin losa ni zapatas el módulo se asentará de forma desigual.", this);
            if ((appeared & HabitatIssue.NoSkeleton) != 0)
                Debug.LogWarning(who + "falta la Capa 1 (estructura esquelética): la contención no tiene soporte.", this);
            if ((appeared & HabitatIssue.NoInternalSystems) != 0 && !forceLog)
                Debug.Log(who + "sin la Capa 4 (sistemas internos): estructura vacía.", this);

            if (solved != HabitatIssue.None)
                Debug.Log(who + "restaurado: " + Describe(solved) + ".", this);
            if (forceLog && all == HabitatIssue.None)
                Debug.Log(who + "las 5 capas están completas. Hermético a " + design.ToString("0.#") + " kPa y protegido frente a la radiación.", this);
            else if (forceLog && (all & HabitatIssue.NoInternalSystems) != 0)
                Debug.Log(who + "sin la Capa 4 (sistemas internos): estructura vacía.", this);
        }

        public static string Describe(HabitatIssue issues)
        {
            if (issues == HabitatIssue.None) return Tr.T("sin problemas");
            var sb = new StringBuilder();
            if ((issues & HabitatIssue.NoFoundation) != 0) sb.Append(Tr.T("cimentación, "));
            if ((issues & HabitatIssue.NoSkeleton) != 0) sb.Append(Tr.T("esqueleto, "));
            if ((issues & HabitatIssue.Depressurized) != 0) sb.Append(Tr.T("contención/presión, "));
            if ((issues & HabitatIssue.RadiationExposed) != 0) sb.Append(Tr.T("escudo de radiación, "));
            if ((issues & HabitatIssue.NoInternalSystems) != 0) sb.Append(Tr.T("sistemas internos, "));
            return sb.ToString(0, sb.Length - 2);
        }

        // ------------------------------------------------------------------
        // Ciclo de vida
        // ------------------------------------------------------------------

        private void OnDisable()
        {
            Registry.Remove(this);
        }

        private void OnEnable()
        {
            if (!Registry.Contains(this)) Registry.Add(this);
            BindAtmosphere(); // las funciones no se guardan con la escena: se enlazan al cargar
            if (Application.isPlaying && data != null && IsFunctional(HabitatLayer.PressureShell))
            {
                InternalPressureKPa = data.designPressureKPa; // los módulos ya colocados empiezan presurizados
            }
            RequestValidation();
        }

        private void OnTransformChildrenChanged()
        {
            RequestValidation(); // se ha añadido o borrado una capa
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            RequestValidation();
        }
#endif

        private void Update()
        {
            if (!Application.isPlaying) return;

            // Revisión periódica barata: detecta capas desactivadas o dañadas desde otros sistemas.
            _recheckTimer -= Time.deltaTime;
            if (_pendingValidation || _recheckTimer <= 0f)
            {
                _recheckTimer = 0.5f;
                if (_batchDepth == 0) Validate(false);
            }
            SimulatePressure(Time.deltaTime);
        }

        private void SimulatePressure(float dt)
        {
            if (data == null) return;
            const float marsAmbientKPa = 0.6f;
            float before = InternalPressureKPa;
            if (IsPressureTight)
            {
                InternalPressureKPa = Mathf.MoveTowards(InternalPressureKPa, data.designPressureKPa, repressurizeRate * dt);
            }
            else
            {
                // Fuga exponencial hacia la presión ambiente de Marte.
                InternalPressureKPa = marsAmbientKPa + (InternalPressureKPa - marsAmbientKPa) * Mathf.Exp(-dt / leakTimeConstant);
            }

            float half = data.designPressureKPa * 0.5f;
            if (before >= half && InternalPressureKPa < half)
            {
                if (logToConsole)
                    Debug.LogWarning("[Base] " + name + ": DESPRESURIZADO (" + InternalPressureKPa.ToString("0.0") + " kPa de " +
                                     data.designPressureKPa.ToString("0.#") + " kPa).", this);
                if (Depressurized != null) Depressurized();
            }
        }

        // ------------------------------------------------------------------
        // Física del hábitat
        // ------------------------------------------------------------------

        /// <summary>
        /// Fracción de la dosis exterior que llega dentro (1 = sin protección).
        /// Las tormentas solares se frenan casi del todo con ~20–30 g/cm²; frente a los rayos
        /// cósmicos el blindaje rinde menos por las partículas secundarias, así que la curva tiene
        /// un suelo del ~30 %. Aproximación de juego, no un cálculo de transporte de radiación.
        /// </summary>
        public float RadiationProtectionFactor
        {
            get
            {
                float areal = 0f;
                HabitatLayerNode shell = GetLayerNode(HabitatLayer.PressureShell);
                HabitatLayerNode shield = GetLayerNode(HabitatLayer.RegolithShield);
                if (shell != null && shell.IsFunctional) areal += shell.arealDensity;
                if (shield != null && shield.IsFunctional) areal += shield.arealDensity;
                return 0.3f + 0.7f * Mathf.Exp(-areal / 40f);
            }
        }

        /// <summary>Dosis exterior en el sitio (mGy/día): datos del mapa de Marte si existen; si no, la media de Curiosity RAD.</summary>
        public float OutsideDoseMilliGyPerDay()
        {
            MarsTerrainGenerator map;
            if (GameServices.TryGet(out map))
            {
                MarsChunkData chunk = map.GetChunkAt(transform.position);
                if (chunk != null && chunk.Metadata != null && chunk.Metadata.environment != null)
                    return chunk.Metadata.environment.radiationMilliGrayPerDay;
            }
            return 0.21f;
        }

        public float InsideDoseMilliGyPerDay()
        {
            return OutsideDoseMilliGyPerDay() * RadiationProtectionFactor;
        }

        /// <summary>Temperatura exterior (°C) del clima de la zona, o −60 °C si no hay clima en la escena.</summary>
        public float OutsideTemperatureC()
        {
            MarsEnvironmentSystem env;
            return GameServices.TryGet(out env) ? env.TemperatureC : -60f;
        }

        /// <summary>
        /// Calor que se escapa por la contención (W) = U · A · ΔT. Sin Capa 2 no hay aislamiento:
        /// devuelve infinito (el interior se iguala al exterior).
        /// </summary>
        public float HeatLossWatts()
        {
            if (data == null) return 0f;
            HabitatLayerNode shell = GetLayerNode(HabitatLayer.PressureShell);
            if (shell == null || !shell.IsFunctional) return float.PositiveInfinity;
            float u = shell.uValue > 0f ? shell.uValue : data.ShellUValue;
            return u * data.ShellArea * Mathf.Max(0f, data.targetTemperatureC - OutsideTemperatureC());
        }

        /// <summary>Resumen legible para paneles de depuración o la interfaz.</summary>
        public string StatusReport()
        {
            var sb = new StringBuilder();
            sb.Append(name).Append(": ");
            sb.Append(IsComplete ? "completo" : Tr.T("problemas: ") + Describe(_issues));
            if (data != null)
            {
                sb.Append(Tr.T(" · presión ")).Append(InternalPressureKPa.ToString("0.0")).Append('/').Append(data.designPressureKPa.ToString("0.#")).Append(" kPa");
            }
            sb.Append(Tr.T(" · radiación dentro ")).Append(InsideDoseMilliGyPerDay().ToString("0.000")).Append(Tr.T(" mGy/día"));
            float heat = HeatLossWatts();
            sb.Append(Tr.T(" · pérdidas ")).Append(float.IsInfinity(heat) ? Tr.T("sin aislamiento") : (heat / 1000f).ToString("0.0") + " kW");
            return sb.ToString();
        }
    }
}
