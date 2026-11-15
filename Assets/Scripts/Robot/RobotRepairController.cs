using System;
using System.Collections.Generic;
using MarsRobot.Energy;
using UnityEngine;

namespace MarsRobot.Robot
{
    /// <summary>
    /// Reparación manual. El jugador elige herramienta (T) y módulo dañado (G) y mantiene R.
    /// Solo se puede reparar con el robot casi parado y con la herramienta correcta.
    /// También gestiona la limpieza manual de paneles (C) si hay un DustingSystem.
    /// La UI se entera de todo por eventos.
    /// </summary>
    [RequireComponent(typeof(RobotHealthSystem))]
    [DisallowMultipleComponent]
    public sealed class RobotRepairController : MonoBehaviour
    {
        [Tooltip("Herramientas que lleva el robot. Más adelante vendrán del inventario.")]
        [SerializeField] private List<RepairToolType> tools = new List<RepairToolType>
        {
            RepairToolType.Wrench, RepairToolType.CableKit, RepairToolType.Welder
        };

        [Tooltip("Velocidad máxima (m/s) a la que se permite reparar.")]
        [SerializeField, Min(0f)] private float maxSpeedToRepair = 0.3f;

        [SerializeField] private RobotController controller;
        [SerializeField] private DustingSystem dusting;

        private RobotHealthSystem _health;
        private int _toolIndex;
        private readonly List<RobotModule> _damaged = new List<RobotModule>();
        private RobotModuleId? _selected;

        /// <summary>Herramienta en la mano.</summary>
        public RepairToolType CurrentTool
        {
            get { return tools.Count > 0 ? tools[_toolIndex] : RepairToolType.None; }
        }

        /// <summary>Módulo seleccionado para reparar (null si no hay ninguno dañado).</summary>
        public RobotModule SelectedModule
        {
            get { return _selected.HasValue ? _health.GetModule(_selected.Value) : null; }
        }

        public bool IsRepairing { get; private set; }

        public event Action<RepairToolType> ToolChanged;
        public event Action<RobotModule> SelectionChanged;

        /// <summary>Progreso de la reparación (módulo, integridad 0..1).</summary>
        public event Action<RobotModule, float> RepairProgress;

        /// <summary>Intento fallido: herramienta incorrecta, en movimiento, etc.</summary>
        public event Action<RobotModule, string> RepairBlocked;

        private void Awake()
        {
            _health = GetComponent<RobotHealthSystem>();
            if (controller == null) controller = GetComponent<RobotController>();
            if (dusting == null) dusting = GetComponent<DustingSystem>();
        }

        private void OnEnable()
        {
            _health.ModuleStateChanged += OnModuleStateChanged;
        }

        private void OnDisable()
        {
            _health.ModuleStateChanged -= OnModuleStateChanged;
        }

        private void Update()
        {
            if (RobotInput.NextToolPressed) CycleTool();
            if (RobotInput.NextModulePressed) CycleSelection();
            if (RobotInput.CleanPanelsPressed && dusting != null) dusting.CleanPanels();

            bool wantsRepair = RobotInput.RepairHeld;
            IsRepairing = false;
            if (!wantsRepair) return;

            RobotModule module = SelectedModule;
            if (module == null)
            {
                CycleSelection();
                module = SelectedModule;
                if (module == null) return; // nada que reparar
            }

            if (controller != null && controller.CurrentSpeed > maxSpeedToRepair)
            {
                Blocked(module, "Detén el robot para reparar.");
                return;
            }

            RepairResult result = _health.Repair(module.Id, CurrentTool, Time.deltaTime);
            switch (result)
            {
                case RepairResult.WrongTool:
                    Blocked(module, "Herramienta no válida para " + module.Definition.displayName + ".");
                    break;
                case RepairResult.Repaired:
                    IsRepairing = true;
                    if (RepairProgress != null) RepairProgress(module, module.Integrity01);
                    break;
                case RepairResult.FullyRepaired:
                    if (RepairProgress != null) RepairProgress(module, 1f);
                    CycleSelection(); // pasa al siguiente módulo dañado
                    break;
            }
        }

        public void CycleTool()
        {
            if (tools.Count == 0) return;
            _toolIndex = (_toolIndex + 1) % tools.Count;
            if (ToolChanged != null) ToolChanged(CurrentTool);
        }

        /// <summary>Selecciona el siguiente módulo que necesite reparación (los rotos primero).</summary>
        public void CycleSelection()
        {
            _damaged.Clear();
            IReadOnlyList<RobotModule> modules = _health.Modules;
            for (int i = 0; i < modules.Count; i++)
            {
                if (modules[i].NeedsRepair) _damaged.Add(modules[i]);
            }

            if (_damaged.Count == 0)
            {
                SetSelection(null);
                return;
            }

            _damaged.Sort((a, b) => a.Integrity01.CompareTo(b.Integrity01));
            int current = -1;
            if (_selected.HasValue)
            {
                for (int i = 0; i < _damaged.Count; i++)
                {
                    if (_damaged[i].Id == _selected.Value) { current = i; break; }
                }
            }
            SetSelection(_damaged[(current + 1) % _damaged.Count].Id);
        }

        private void SetSelection(RobotModuleId? id)
        {
            if (Nullable.Equals(_selected, id)) return;
            _selected = id;
            if (SelectionChanged != null) SelectionChanged(SelectedModule);
        }

        private void OnModuleStateChanged(RobotModule module, ModuleState previous)
        {
            // Si se rompe algo y no había nada seleccionado, se selecciona solo.
            if (!_selected.HasValue && module.NeedsRepair) SetSelection(module.Id);
        }

        private void Blocked(RobotModule module, string reason)
        {
            if (RepairBlocked != null) RepairBlocked(module, reason);
        }
    }
}
