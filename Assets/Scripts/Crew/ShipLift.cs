using MarsRobot.Base.Ship;
using UnityEngine;

namespace MarsRobot.Crew
{
    /// <summary>
    /// Montacargas del núcleo de la nave (uno por nivel): el astronauta elige el nivel y viaja a él.
    /// </summary>
    public sealed class ShipLift : MonoBehaviour, ICrewInteractable
    {
        public static bool MenuOpen { get; private set; }

        private Transform _ship;
        private int _level;
        private bool _open;
        private Astronaut _user;
        private float _fade;
        private GUIStyle _button, _title;

        public static void CreateAll(Transform ship)
        {
            for (int i = 0; i < ShipModel.LevelCount; i++)
            {
                var go = new GameObject("Montacargas nivel " + (i + 1));
                go.transform.SetParent(ship, false);
                go.transform.localPosition = ShipLayout.OnLevel(i, ShipLayout.LiftDoor);
                var lift = go.AddComponent<ShipLift>();
                lift._ship = ship;
                lift._level = i;
            }
        }

        private void OnEnable()
        {
            CrewInteractables.Register(this);
        }

        private void OnDisable()
        {
            CrewInteractables.Unregister(this);
            if (_open) MenuOpen = false;
        }

        public string CrewPrompt
        {
            get { return "F: montacargas (estás en el nivel " + (_level + 1) + ")"; }
        }

        public Vector3 CrewPoint
        {
            get { return transform.position + Vector3.up; }
        }

        public float CrewRange
        {
            get { return 1.4f; }
        }

        public bool CrewAvailable
        {
            get { return true; }
        }

        public void CrewInteract(Astronaut astronaut)
        {
            _user = astronaut;
            _open = true;
            MenuOpen = true;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Close()
        {
            _open = false;
            MenuOpen = false;
        }

        private void Update()
        {
            if (_fade > 0f) _fade = Mathf.Max(0f, _fade - Time.unscaledDeltaTime * 2f);
            if (_open && Input.GetKeyDown(KeyCode.Escape)) Close();
        }

        private void Travel(int level)
        {
            Vector3 local = ShipLayout.OnLevel(level, ShipLayout.LiftDoor + new Vector3(0f, 0.1f, 0.4f));
            _user.Teleport(_ship.TransformPoint(local), _ship.rotation);
            _fade = 1f;
            Close();
        }

        private void OnGUI()
        {
            if (_fade > 0f)
            {
                GUI.color = new Color(0f, 0f, 0f, _fade);
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
                GUI.color = Color.white;
            }
            if (!_open) return;
            if (_button == null)
            {
                _button = new GUIStyle(GUI.skin.button) { fontSize = 17, alignment = TextAnchor.MiddleLeft, padding = new RectOffset(16, 8, 4, 4) };
                _title = new GUIStyle(GUI.skin.label) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _title.normal.textColor = new Color(0.75f, 0.95f, 1f);
            }
            var r = new Rect(Screen.width * 0.5f - 230, Screen.height * 0.5f - 210, 460, 420);
            GUI.color = new Color(0.03f, 0.1f, 0.15f, 0.94f);
            GUI.DrawTexture(r, Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(r.x, r.y + 10, r.width, 30), "MONTACARGAS · elige nivel", _title);
            for (int i = ShipModel.LevelCount - 1; i >= 0; i--)
            {
                int row = ShipModel.LevelCount - 1 - i;
                var br = new Rect(r.x + 20, r.y + 52 + row * 52, r.width - 40, 44);
                string label = ShipModel.LevelNames[i] + (i == _level ? "   ◄ aquí" : "");
                GUI.enabled = i != _level;
                if (GUI.Button(br, label, _button)) Travel(i);
                GUI.enabled = true;
            }
            if (GUI.Button(new Rect(r.x + 20, r.yMax - 52, r.width - 40, 40), "Cerrar (Esc)")) Close();
        }
    }
}
