using System.Collections.Generic;
using MarsRobot.Items;
using MarsRobot.Robot;
using MarsRobot.UI.Minimap;
using UnityEngine;

namespace MarsRobot.Interaction
{
    /// <summary>Dureza de una roca: decide qué herramienta hace falta.</summary>
    public enum RockHardness
    {
        Loose, // suelta: vale la pala o el taladro
        Dense  // densa: solo el taladro percutor
    }

    /// <summary>
    /// Roca marciana minable. Al mantener F con la herramienta adecuada, extrae objetos según los
    /// minerales del lugar (datos CRISM del chunk si existen; si no, los de la zona), los guarda
    /// en la bahía del rover y lanza partículas y sonido de minería. Se agota y desaparece.
    /// </summary>
    public sealed class ResourceNode : MonoBehaviour, IInteractable
    {
        private static readonly List<ResourceNode> Active = new List<ResourceNode>();
        private static AudioClip _drillClip;

        [SerializeField] private RockHardness hardness = RockHardness.Loose;
        [SerializeField, Min(1)] private int units = 4;
        [SerializeField, Min(0.1f)] private float secondsPerExtraction = 1.2f;

        // Composición (0..1), fijada al aparecer a partir del suelo del lugar.
        private float _iron, _clays, _sulfates, _ice;
        private int _initialUnits;
        private Vector3 _initialScale;
        private bool _revealed;
        private Color _color = new Color(0.4f, 0.22f, 0.15f);

        /// <summary>Identificador estable (chunk + índice) para no volver a generar rocas agotadas.</summary>
        public string PersistentId { get; set; }

        /// <summary>Se lanza cuando la roca se agota (el spawner la recuerda).</summary>
        public static event System.Action<ResourceNode> Depleted;

        public RockHardness Hardness
        {
            get { return hardness; }
        }

        public void Setup(RockHardness h, int unitCount, float iron, float clays, float sulfates, float ice, Color color)
        {
            hardness = h;
            units = Mathf.Max(1, unitCount);
            _initialUnits = units;
            _iron = iron;
            _clays = clays;
            _sulfates = sulfates;
            _ice = ice;
            _color = color;
            _initialScale = transform.localScale;
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
            MinimapObjectMarker.Remove(this);
        }

        /// <summary>Marca en el minimapa las rocas aprovechables en un radio (escáner). Devuelve cuántas.</summary>
        public static int RevealAround(Vector3 position, float radius)
        {
            int n = 0;
            float r2 = radius * radius;
            for (int i = 0; i < Active.Count; i++)
            {
                ResourceNode node = Active[i];
                Vector3 d = node.transform.position - position;
                d.y = 0f;
                if (d.sqrMagnitude > r2) continue;
                n++;
                if (node._revealed) continue;
                node._revealed = true;
                MinimapMarkerType type = node._ice > 0.5f ? MinimapMarkerType.Ice : MinimapMarkerType.Resource;
                MinimapObjectMarker.Add(node, node.transform.position, type, node._iron > 0.45f || node._ice > 0.5f);
            }
            return n;
        }

        // ------------------------------------------------------------------
        // IInteractable
        // ------------------------------------------------------------------

        public float HoldDuration
        {
            get { return secondsPerExtraction; }
        }

        public bool CanInteract()
        {
            Interactor who = Interactor.Local;
            if (who == null || who.Inventory == null || who.Inventory.IsFull) return false;
            return ToolAllowed(who.Tools);
        }

        private bool ToolAllowed(RoverToolController tools)
        {
            if (tools == null) return hardness == RockHardness.Loose;
            if (tools.HasTool(RoverModuleType.PercussionDrill)) return true;
            return hardness == RockHardness.Loose && tools.HasTool(RoverModuleType.Shovel);
        }

        public string GetInteractionPrompt()
        {
            Interactor who = Interactor.Local;
            string kind = hardness == RockHardness.Dense ? Tr.T("roca densa") : Tr.T("roca suelta");
            if (who != null && who.Inventory != null && who.Inventory.IsFull) return Tr.T("Bahía llena: vacía la carga en el cohete");
            if (who != null && !ToolAllowed(who.Tools))
            {
                return hardness == RockHardness.Dense
                    ? Tr.T("Roca densa: necesitas el taladro percutor (cámbialo en el taller del cohete)")
                    : Tr.T("Necesitas la pala o el taladro");
            }
            return Tr.T("Minar ") + kind + " (" + MainProduct() + Tr.T(") [mantén F]  ·  quedan ") + units;
        }

        public void Interact(Interactor interactor)
        {
            if (!CanInteract()) return;
            bool drill = interactor.Tools == null || interactor.Tools.HasTool(RoverModuleType.PercussionDrill);

            int got = 0;
            // La pala solo arranca material suelto (regolito); el taladro saca lo que haya en la roca.
            got += Give(interactor, _sulfates > 0.3f ? ItemDatabase.PerchlorateRegolith : ItemDatabase.Regolith);
            if (drill)
            {
                // El escáner de minerales del robot mejora las probabilidades.
                float k = interactor.Tools != null ? interactor.Tools.OreChanceMultiplier : 1f;
                if (Random.value < (0.2f + _iron * 0.8f) * k) got += Give(interactor, ItemDatabase.IronOre);
                if (Random.value < (0.1f + _clays * 0.6f) * k) got += Give(interactor, ItemDatabase.Clays);
                if (Random.value < _sulfates * 0.5f * k) got += Give(interactor, ItemDatabase.Sulfates);
                if (Random.value < _ice * 0.6f * k) got += Give(interactor, ItemDatabase.Ice);
                if (Random.value < 0.15f * k) got += Give(interactor, ItemDatabase.Sample);
            }
            interactor.ShowFeedback(got > 0 ? Tr.T("Extraídas ") + got + Tr.T(" unidades") : Tr.T("No cabe nada más en la bahía."));
            PlayEffects();

            units--;
            float t = (float)units / Mathf.Max(1, _initialUnits);
            transform.localScale = _initialScale * Mathf.Lerp(0.45f, 1f, t);
            if (units <= 0)
            {
                if (Depleted != null) Depleted(this);
                Destroy(gameObject);
            }
        }

        private static int Give(Interactor who, string id)
        {
            ItemData item = ItemDatabase.Get(id);
            return item != null ? who.Inventory.TryAdd(item, 1) : 0;
        }

        private string MainProduct()
        {
            if (_ice > 0.5f) return "hielo";
            if (_iron > 0.45f) return Tr.T("rica en hierro");
            if (_clays > 0.3f) return "arcillas";
            if (_sulfates > 0.3f) return "sulfatos/percloratos";
            return "regolito";
        }

        // ------------------------------------------------------------------
        // Efectos: partículas de polvo y sonido de perforación (generados por código)
        // ------------------------------------------------------------------

        private void PlayEffects()
        {
            var go = new GameObject("Polvo de minería");
            go.transform.position = transform.position + Vector3.up * 0.5f;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = 1.4f;
            main.startSpeed = 2.5f;
            main.startSize = 0.18f;
            main.startColor = Color.Lerp(_color, new Color(0.8f, 0.55f, 0.4f), 0.4f);
            main.gravityModifier = 0.38f; // gravedad de Marte
            main.stopAction = ParticleSystemStopAction.Destroy;
            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 30) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.4f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            Shader sh = Shader.Find("Particles/Standard Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) renderer.material = new Material(sh) { color = main.startColor.color };
            ps.Play();

            if (_drillClip == null) _drillClip = MakeDrillClip();
            AudioSource.PlayClipAtPoint(_drillClip, transform.position, 0.6f);
        }

        /// <summary>Sonido de taladro: ruido con golpes rítmicos y caída de volumen.</summary>
        private static AudioClip MakeDrillClip()
        {
            const int rate = 22050;
            int samples = rate / 2;
            var data = new float[samples];
            var rng = new System.Random(7);
            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / rate;
                float hammer = 0.5f + 0.5f * Mathf.Sign(Mathf.Sin(t * 2f * Mathf.PI * 18f)); // percusión
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                data[i] = noise * hammer * Mathf.Exp(-t * 3f) * 0.5f;
            }
            AudioClip clip = AudioClip.Create(Tr.T("Taladro"), samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
