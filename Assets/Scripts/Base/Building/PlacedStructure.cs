using MarsRobot.Energy;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Estructura ya construida. Es una BaseStation, así que el robot la usa con F como las máquinas
    /// del cohete (encender/apagar, limpiar paneles, desatascar, desmontar…). La red de energía
    /// (<see cref="BaseGrid"/>) le pide su generación/consumo y le dice cuánto producir.
    /// </summary>
    public class PlacedStructure : BaseStation
    {
        /// <summary>Probabilidad de que la impresora se atasque al terminar cada bloque.</summary>
        private const float PrinterJamChance = 0.12f;

        private float[] _producedAccum = new float[0];
        private float[] _consumedAccum = new float[0];
        private string _lastAction = "";

        public StructureDefinition Definition { get; private set; }

        /// <summary>Encendida por el jugador (las que consumen energía se pueden apagar).</summary>
        public bool IsOn { get; set; }

        /// <summary>Polvo acumulado (solo paneles solares), 0..1.</summary>
        public float Dust01 { get; private set; }

        /// <summary>Quita polvo de un panel solar fijo (soplador del rover o limpieza manual).</summary>
        public void CleanPanel(float amount)
        {
            Dust01 = Mathf.Clamp01(Dust01 - Mathf.Max(0f, amount));
        }

        /// <summary>Degrada el panel (p. ej. partículas solares energéticas): sube el "polvo" efectivo.</summary>
        public void DegradePanel(float amount)
        {
            Dust01 = Mathf.Clamp01(Dust01 + Mathf.Max(0f, amount));
        }

        /// <summary>Impresora atascada: no produce hasta desatascarla.</summary>
        public bool IsJammed { get; private set; }

        /// <summary>Último reparto de energía (0..1): 1 = funciona a tope.</summary>
        public float PowerRatio { get; set; }

        /// <summary>Potencia generada en el último ciclo (W).</summary>
        public float LastGeneration { get; set; }

        /// <summary>Texto de estado para el menú y el HUD.</summary>
        public string Status { get; private set; }

        public void Setup(RocketBase owner, StructureDefinition def)
        {
            Definition = def;
            IsOn = true;
            PowerRatio = 1f;
            Status = Tr.T("Funcionando");
            _producedAccum = new float[def.produces.Length];
            _consumedAccum = new float[def.consumes.Length];
            float radius = Mathf.Max(def.size.x, def.size.z) * 0.5f + 2.5f;
            Init(owner, def.name, radius);
        }

        // ------------------------------------------------------------------
        // Simulación (la llama BaseGrid)
        // ------------------------------------------------------------------

        /// <summary>Generación actual en W según sol, polvo y tormentas.</summary>
        public float ComputeGeneration(ISolarEnvironment env)
        {
            if (!Definition.IsGenerator) return 0f;
            if (Definition.kind == StructureKind.SolarArray)
            {
                float sun = env != null ? Mathf.Clamp01(env.SolarIrradiance / 590f) : 0.5f;
                return Definition.generationWatts * sun * (1f - Dust01);
            }
            return Definition.generationWatts; // reactor: constante
        }

        /// <summary>Potencia que pide ahora (0 si está apagada o no puede trabajar).</summary>
        public float ComputeDemand()
        {
            return Definition.IsConsumer && CanWork() ? Definition.consumptionWatts : 0f;
        }

        /// <summary>¿Tiene todo lo necesario para trabajar (además de energía)?</summary>
        public bool CanWork()
        {
            if (!IsOn)
            {
                Status = Tr.T("Apagada");
                return false;
            }
            if (IsJammed)
            {
                Status = "<color=orange>Tolva atascada: usa F para desatascar</color>";
                return false;
            }
            BaseInventory inv = Owner.Inventory;
            for (int i = 0; i < Definition.consumes.Length; i++)
            {
                if (inv.Get(Definition.consumes[i].type) < 1 && _consumedAccum[i] <= 0f)
                {
                    Status = Tr.T("Falta ") + BaseInventory.Label(Definition.consumes[i].type);
                    return false;
                }
            }
            bool anyRoom = Definition.produces.Length == 0;
            for (int i = 0; i < Definition.produces.Length; i++)
            {
                if (!inv.IsFull(Definition.produces[i].type)) anyRoom = true;
            }
            if (!anyRoom)
            {
                Status = Tr.T("Almacén lleno: construye tanques");
                return false;
            }
            return true;
        }

        /// <summary>Avanza la producción <paramref name="gameHours"/> horas con el reparto de energía dado.</summary>
        public void Tick(float gameHours, float powerRatio, ISolarEnvironment env, float iceFactor)
        {
            PowerRatio = powerRatio;

            // Polvo en paneles fijos (igual que en el robot).
            if (Definition.kind == StructureKind.SolarArray && env != null)
            {
                Dust01 = Mathf.Clamp01(Dust01 + env.DustDepositionPerHour * gameHours * (1f - Dust01));
                Status = Dust01 > 0.4f ? "<color=orange>Panel sucio: F para limpiar</color>" : Tr.T("Generando ") + LastGeneration.ToString("0") + " W";
                return;
            }

            if (!Definition.IsConsumer)
            {
                if (Status == "" || Status == Tr.T("Funcionando")) Status = Tr.T("Funcionando");
                return;
            }
            if (!CanWork()) return;
            if (powerRatio <= 0.01f)
            {
                Status = "<color=#ff8080>Sin energía</color>";
                return;
            }

            // Rendimiento especial según el tipo.
            float efficiency = powerRatio;
            if (Definition.kind == StructureKind.IceDrill) efficiency *= iceFactor;
            if (Definition.kind == StructureKind.AtmosphericCondenser && env != null && env.IsDustStormActive) efficiency *= 0.25f;

            BaseInventory inv = Owner.Inventory;

            // Consumir materias primas.
            for (int i = 0; i < Definition.consumes.Length; i++)
            {
                _consumedAccum[i] += Definition.consumes[i].perHour * gameHours * efficiency;
                int whole = Mathf.FloorToInt(_consumedAccum[i]);
                if (whole > 0)
                {
                    var cost = new[] { new ResourceAmount(Definition.consumes[i].type, whole) };
                    if (!inv.TrySpend(cost)) return; // se acabó a mitad de ciclo
                    _consumedAccum[i] -= whole;
                }
            }

            // Producir.
            for (int i = 0; i < Definition.produces.Length; i++)
            {
                _producedAccum[i] += Definition.produces[i].perHour * gameHours * efficiency;
                int whole = Mathf.FloorToInt(_producedAccum[i]);
                if (whole <= 0) continue;
                inv.Add(Definition.produces[i].type, whole);
                _producedAccum[i] -= whole;

                if (Definition.kind == StructureKind.RegolithPrinter && Random.value < PrinterJamChance * whole)
                {
                    IsJammed = true; // el regolito real se apelmaza y atasca las tolvas
                }
            }

            Status = efficiency >= 0.99f ? Tr.T("Funcionando") : Tr.T("Funcionando al ") + Mathf.RoundToInt(efficiency * 100f) + " %";
            if (Definition.kind == StructureKind.IceDrill && iceFactor < 0.3f) Status += Tr.T(" (poco hielo en esta zona)");
        }

        // ------------------------------------------------------------------
        // Menú (F)
        // ------------------------------------------------------------------

        public override string Prompt
        {
            get { return "F: " + Definition.name + " — " + StripColor(Status); }
        }

        public override void DrawMenu(RobotBaseLink robot)
        {
            GUILayout.Label(Definition.description);
            GUILayout.Space(4);
            GUILayout.Label(Tr.T("<b>Estado:</b> ") + Status);
            if (Definition.IsGenerator) GUILayout.Label(Tr.T("Genera ahora: ") + LastGeneration.ToString("0") + " W");
            if (Definition.IsConsumer) GUILayout.Label(Tr.T("Consume: ") + Definition.consumptionWatts.ToString("0") + Tr.T(" W  (energía recibida ") + Mathf.RoundToInt(PowerRatio * 100f) + " %)");
            if (Definition.storageWh > 0f) GUILayout.Label(Tr.T("Almacena hasta ") + Definition.storageWh.ToString("0") + " Wh");
            if (Definition.storageBonus.amount > 0)
            {
                GUILayout.Label("+" + Definition.storageBonus.amount + Tr.T(" de capacidad de ") + BaseInventory.Label(Definition.storageBonus.type));
            }
            GUILayout.Space(6);

            if (Definition.IsConsumer && GUILayout.Button(IsOn ? Tr.T("Apagar") : Tr.T("Encender"), GUILayout.Width(200)))
            {
                IsOn = !IsOn;
            }
            if (Definition.kind == StructureKind.SolarArray && GUILayout.Button(Tr.T("Limpiar el panel (") + Mathf.RoundToInt(Dust01 * 100f) + Tr.T(" % de polvo)"), GUILayout.Width(300)))
            {
                Dust01 = 0f;
                _lastAction = Tr.T("Panel limpio.");
            }
            if (Definition.kind == StructureKind.RegolithPrinter && IsJammed && GUILayout.Button(Tr.T("Desatascar la tolva"), GUILayout.Width(200)))
            {
                IsJammed = false;
                _lastAction = Tr.T("Tolva desatascada: el regolito vuelve a fluir.");
            }

            // Extras de ciencia.
            if (Definition.kind == StructureKind.SampleLab) ResearchMenu.Draw(Owner);
            if (Definition.kind == StructureKind.CommsAntenna) ColonyObjectives.DrawMenu(Owner);

            GUILayout.Space(8);
            if (GUILayout.Button(Tr.T("Desmontar (recuperas la mitad de los materiales)"), GUILayout.Width(380)))
            {
                Owner.Dismantle(this);
                return;
            }
            if (_lastAction.Length > 0) GUILayout.Label("<b>" + _lastAction + "</b>");
        }

        private static string StripColor(string s)
        {
            return System.Text.RegularExpressions.Regex.Replace(s, "<.*?>", "");
        }
    }
}
