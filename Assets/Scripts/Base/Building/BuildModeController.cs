using System.Collections.Generic;
using MarsRobot.Core;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Base
{
    /// <summary>
    /// Modo construcción del robot (tecla B):
    ///  • Tab cambia de categoría y 1–9 elige la estructura.
    ///  • Delante del robot aparece una vista previa: verde si se puede colocar, roja si no
    ///    (y el motivo: faltan materiales, terreno muy inclinado, choca con algo, lejos de la base…).
    ///  • R la gira 90°, clic izquierdo o Intro la construyen, B o Esc salen.
    /// El robot se puede mover mientras tanto para elegir el sitio.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class BuildModeController : MonoBehaviour
    {
        [Tooltip("Distancia extra (m) entre el robot y el borde de la pieza.")]
        [SerializeField, Min(0.5f)] private float placeDistance = 3f;

        private readonly List<StructureDefinition> _inCategory = new List<StructureDefinition>();
        private readonly Collider[] _overlaps = new Collider[16];

        private StructureCategory _category = StructureCategory.Energy;
        private StructureDefinition _selected;
        private GameObject _ghost;
        private Renderer[] _ghostRenderers = new Renderer[0];
        private Material _ghostOk, _ghostBad;
        private int _rotationSteps;
        private bool _valid;
        private string _reason = "";
        private string _message = "";
        private float _messageTime;

        private RocketBase _base;
        private TerrainManager _terrain;
        private RobotRepairController _repair;
        private RobotBaseLink _link;
        private Rigidbody _robotBody;

        public bool IsActive { get; private set; }

        private void Awake()
        {
            _repair = GetComponent<RobotRepairController>();
            _link = GetComponent<RobotBaseLink>();
            _robotBody = GetComponent<Rigidbody>();
        }

        private void Update()
        {
            if (_base == null) GameServices.TryGet(out _base);
            if (_terrain == null) GameServices.TryGet(out _terrain);
            if (_base == null || _terrain == null) return;

            if (RobotInput.BuildModePressed && (_link == null || !_link.IsMenuOpen))
            {
                SetActive(!IsActive);
            }
            if (!IsActive) return;
            if (RobotInput.ReleaseCursorPressed)
            {
                SetActive(false);
                return;
            }

            if (RobotInput.BuildCategoryPressed)
            {
                _category = (StructureCategory)(((int)_category + 1) % 4);
                RefreshCategory();
                Select(_inCategory.Count > 0 ? _inCategory[0] : null);
            }
            for (int i = 0; i < _inCategory.Count; i++)
            {
                if (RobotInput.NumberPressed(i)) Select(_inCategory[i]);
            }
            if (RobotInput.BuildRotatePressed) _rotationSteps = (_rotationSteps + 1) % 4;

            UpdateGhost();

            // Solo si el ratón YA estaba capturado el frame anterior: el clic que sirve para volver a
            // capturarlo (tras pulsar Esc) no debe construir nada por accidente.
            bool lockedNow = Cursor.lockState == CursorLockMode.Locked;
            if (RobotInput.BuildPlacePressed && lockedNow && _wasLocked) TryPlace();
            _wasLocked = lockedNow;
        }

        private bool _wasLocked;

        private void SetActive(bool active)
        {
            IsActive = active;
            // La R sirve para girar piezas: desactivamos la reparación manual mientras tanto.
            if (_repair != null) _repair.enabled = !active;
            if (active)
            {
                RefreshCategory();
                if (_selected == null && _inCategory.Count > 0) Select(_inCategory[0]);
            }
            else
            {
                DestroyGhost();
                _selected = null;
            }
        }

        private void RefreshCategory()
        {
            _inCategory.Clear();
            var all = StructureCatalog.All;
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i].category == _category && !all[i].hiddenFromBuildMenu) _inCategory.Add(all[i]);
            }
        }

        private void Select(StructureDefinition def)
        {
            if (def == _selected && _ghost != null) return;
            _selected = def;
            DestroyGhost();
            if (def == null) return;

            if (_ghostOk == null)
            {
                _ghostOk = StructureModels.GhostMaterial(new Color(0.3f, 1f, 0.4f));
                _ghostBad = StructureModels.GhostMaterial(new Color(1f, 0.25f, 0.2f));
            }
            _ghost = new GameObject("Vista previa: " + def.name);
            StructureModels.Build(def, _ghost.transform, _ghostOk);
            _ghostRenderers = _ghost.GetComponentsInChildren<Renderer>();
        }

        private void DestroyGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostRenderers = new Renderer[0];
        }

        // ------------------------------------------------------------------
        // Colocación
        // ------------------------------------------------------------------

        private void UpdateGhost()
        {
            if (_selected == null || _ghost == null) return;

            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward.Normalize();
            float depth = _selected.size.z * 0.5f;
            if (_rotationSteps % 2 == 1) depth = _selected.size.x * 0.5f;

            Vector3 pos = transform.position + forward * (depth + placeDistance + 0.6f);
            pos.y = _terrain.GetHeightAt(pos);
            Quaternion rot = Quaternion.LookRotation(forward, Vector3.up) * Quaternion.Euler(0f, 90f * _rotationSteps, 0f);
            _ghost.transform.SetPositionAndRotation(pos, rot);

            _valid = Validate(pos, rot, out _reason);
            Material m = _valid ? _ghostOk : _ghostBad;
            for (int i = 0; i < _ghostRenderers.Length; i++)
            {
                if (_ghostRenderers[i] != null) _ghostRenderers[i].sharedMaterial = m;
            }
        }

        private bool Validate(Vector3 pos, Quaternion rot, out string reason)
        {
            StructureDefinition def = _selected;

            if (!_base.Research.IsUnlocked(def))
            {
                reason = "Bloqueado: necesita " + def.researchCost + " puntos de investigación (laboratorio).";
                return false;
            }
            if (!_base.Inventory.Has(def.cost))
            {
                reason = "Faltan materiales: " + BaseInventory.Describe(def.cost);
                return false;
            }

            Vector3 toBase = pos - _base.transform.position;
            toBase.y = 0f;
            if (!def.ignoreBuildRadius && toBase.magnitude > RocketBase.BuildRadius)
            {
                reason = "Demasiado lejos del cohete (máx. " + RocketBase.BuildRadius.ToString("0") + " m).";
                return false;
            }

            // Pendiente: diferencia de altura entre las esquinas.
            float hx = def.size.x * 0.5f, hz = def.size.z * 0.5f;
            float minH = float.MaxValue, maxH = float.MinValue;
            for (int i = 0; i < 5; i++)
            {
                Vector3 local = i == 4 ? Vector3.zero : new Vector3((i & 1) == 0 ? -hx : hx, 0f, (i & 2) == 0 ? -hz : hz);
                float h = _terrain.GetHeightAt(pos + rot * local);
                minH = Mathf.Min(minH, h);
                maxH = Mathf.Max(maxH, h);
            }
            float allowed = Mathf.Tan(def.maxSlopeDegrees * Mathf.Deg2Rad) * Mathf.Max(def.size.x, def.size.z);
            if (maxH - minH > allowed)
            {
                reason = "Terreno demasiado inclinado (máx. " + def.maxSlopeDegrees.ToString("0") + "°).";
                return false;
            }

            // Choques con otras estructuras, el cohete o rocas (no con el terreno ni con el robot).
            Vector3 half = new Vector3(hx * 0.95f, Mathf.Max(0.2f, def.size.y * 0.5f - 0.3f), hz * 0.95f);
            Vector3 center = pos + Vector3.up * (half.y + 0.4f);
            int n = Physics.OverlapBoxNonAlloc(center, half, _overlaps, rot, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                Collider c = _overlaps[i];
                if (c.GetComponent<TerrainChunk>() != null) continue;
                if (_robotBody != null && c.attachedRigidbody == _robotBody) continue;
                reason = "Choca con " + c.transform.root.name + ".";
                return false;
            }

            reason = "";
            return true;
        }

        private void TryPlace()
        {
            if (_selected == null || !_valid)
            {
                Show(_reason.Length > 0 ? _reason : "Elige una estructura con los números.");
                return;
            }
            if (!_base.Inventory.TrySpend(_selected.cost))
            {
                Show("Faltan materiales.");
                return;
            }
            _base.PlaceStructure(_selected, _ghost.transform.position, _ghost.transform.rotation);
            Show(_selected.name + " construido.");
        }

        private void Show(string text)
        {
            _message = text;
            _messageTime = Time.time;
        }

        // ------------------------------------------------------------------
        // Interfaz (IMGUI provisional)
        // ------------------------------------------------------------------

        private void OnGUI()
        {
            if (!IsActive || _base == null) return;
            GUI.skin.label.richText = true;

            var sb = new System.Text.StringBuilder();
            sb.Append("<b>MODO CONSTRUCCIÓN</b>   Categoría: <b>").Append(StructureCatalog.CategoryLabel(_category))
              .Append("</b>  (Tab cambia)\n\n");
            for (int i = 0; i < _inCategory.Count; i++)
            {
                StructureDefinition d = _inCategory[i];
                bool locked = !_base.Research.IsUnlocked(d);
                bool affordable = _base.Inventory.Has(d.cost);
                string color = d == _selected ? "#ffd060" : (locked ? "#888888" : (affordable ? "white" : "#ff9090"));
                sb.Append("<color=").Append(color).Append('>').Append(i + 1).Append(". ").Append(d.name);
                if (locked) sb.Append("  [investigación: ").Append(d.researchCost).Append(']');
                sb.Append("  —  ").Append(BaseInventory.Describe(d.cost)).Append("</color>\n");
            }
            if (_selected != null)
            {
                sb.Append('\n').Append(_selected.description).Append('\n');
                sb.Append(_valid ? "<color=#80ff80>Clic o Intro: construir</color>" : "<color=#ff8080>" + _reason + "</color>");
            }
            sb.Append("\nR girar · B o Esc salir");
            if (Time.time - _messageTime < 3f) sb.Append("\n\n<b>").Append(_message).Append("</b>");

            const float w = 560f, h = 330f;
            Rect r = new Rect(10, Screen.height - h - 10, w, h);
            GUI.Box(r, "");
            GUI.Label(new Rect(r.x + 8, r.y + 6, w - 16, h - 12), sb.ToString());
        }

        private void OnDisable()
        {
            // Por ejemplo, al subir al MarteCar: se sale del modo construcción.
            if (IsActive) SetActive(false);
            DestroyGhost();
        }
    }
}
