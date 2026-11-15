using System;
using MarsRobot.Crew;
using MarsRobot.Items;
using UnityEngine;

namespace MarsRobot.Crafting
{
    /// <summary>
    /// Estación de fabricación (Fabricador, Fundición, Refinería química, Banco de trabajo), como en
    /// los juegos de supervivencia: tiene su propio inventario en casillas y fabrica con lo que hay
    /// dentro; lo fabricado aparece en ese mismo inventario. La usa el astronauta con F.
    /// </summary>
    public sealed class CraftingStation : MonoBehaviour, ICrewInteractable
    {
        public const int Slots = 40;

        public string DisplayName { get; private set; }
        public CraftStation Station { get; private set; }
        public ItemContainer Inventory { get; private set; }
        public CraftingQueue Queue { get; private set; }

        public static CraftingStation Create(Transform shipRoot, string name, CraftStation station, Vector3 shipLocalPos, float yaw)
        {
            var go = new GameObject("Estación: " + name);
            go.transform.SetParent(shipRoot, false);
            go.transform.localPosition = shipLocalPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            var s = go.AddComponent<CraftingStation>();
            s.DisplayName = name;
            s.Station = station;
            s.Inventory = new ItemContainer(Slots, 0f, 0f);
            s.Queue = new CraftingQueue { Lanes = station == CraftStation.Garden ? 3 : 1 };
            return s;
        }

        private void OnEnable()
        {
            CrewInteractables.Register(this);
        }

        private void OnDisable()
        {
            CrewInteractables.Unregister(this);
        }

        private void Update()
        {
            if (Queue != null) Queue.Tick(Time.deltaTime, Inventory);
        }

        public string CrewPrompt
        {
            get
            {
                string busy = Queue != null && Queue.Entries.Count > 0 ? " (fabricando…)" : "";
                return "F: usar " + DisplayName + busy;
            }
        }

        /// <summary>Delante de la máquina, a la altura del pecho.</summary>
        public Vector3 CrewPoint
        {
            get { return transform.position + transform.forward * 1.0f + Vector3.up * 1.0f; }
        }

        public float CrewRange
        {
            get { return 1.6f; }
        }

        public bool CrewAvailable
        {
            get { return true; }
        }

        public void CrewInteract(Astronaut astronaut)
        {
            CraftingWindow.OpenStation(astronaut, this);
        }
    }

    /// <summary>Acceso a un inventario sin fabricación (almacén de la base, bahía del robot…).</summary>
    public sealed class ContainerAccess : MonoBehaviour, ICrewInteractable
    {
        private string _title;
        private Func<ItemContainer> _source;
        private Func<bool> _available;
        private Func<Vector3> _point;
        private float _range = 1.8f;

        public static ContainerAccess Create(Transform parent, string title, Vector3 localPos, Func<ItemContainer> source,
            Func<bool> available = null, Func<Vector3> point = null, float range = 1.8f)
        {
            var go = new GameObject("Acceso: " + title);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            var a = go.AddComponent<ContainerAccess>();
            a._title = title;
            a._source = source;
            a._available = available;
            a._point = point;
            a._range = range;
            return a;
        }

        private void OnEnable()
        {
            CrewInteractables.Register(this);
        }

        private void OnDisable()
        {
            CrewInteractables.Unregister(this);
        }

        public string CrewPrompt
        {
            get { return "F: abrir " + _title; }
        }

        public Vector3 CrewPoint
        {
            get { return _point != null ? _point() : transform.position + Vector3.up; }
        }

        public float CrewRange
        {
            get { return _range; }
        }

        public bool CrewAvailable
        {
            get { return _source != null && _source() != null && (_available == null || _available()); }
        }

        public void CrewInteract(Astronaut astronaut)
        {
            CraftingWindow.OpenContainer(astronaut, _title, _source());
        }
    }
}
