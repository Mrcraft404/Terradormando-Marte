using System.Collections;
using MarsRobot.Robot;
using UnityEngine;

namespace MarsRobot.Base.Ship
{
    /// <summary>
    /// Ascensor exterior del robot: una plataforma de 3,6 × 4 m que sube por los raíles del casco
    /// desde el suelo hasta la puerta de carga del hangar (nivel 1).
    ///  • Con el robot encima: F sube o baja.
    ///  • Desde la puerta del hangar o desde el pie de los raíles: F llama a la plataforma.
    /// Durante el viaje el robot va fijado a la plataforma (cinemático) y no se puede mover.
    /// Arriba, una barrera en el borde exterior impide caer; abajo se abre para entrar y salir.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ShipElevator : MonoBehaviour
    {
        private const float Speed = 3f;          // m/s
        private const float HalfWidth = 1.8f;
        private const float HalfDepth = 2.0f;

        private RocketBase _base;
        private Transform _ship;
        private Transform _platform;
        private BoxCollider _outerGate;
        private float _z, _topY, _bottomY, _y;
        private bool _atTop = true;
        private bool _moving;
        private RobotController _robot;
        private string _prompt = "";
        private GUIStyle _style;

        public bool IsMoving
        {
            get { return _moving; }
        }

        public bool IsAtTop
        {
            get { return _atTop && !_moving; }
        }

        public static ShipElevator Create(RocketBase rocketBase, ShipModel model)
        {
            var go = new GameObject("Ascensor del robot");
            go.transform.SetParent(model.transform, false);
            var e = go.AddComponent<ShipElevator>();
            e._base = rocketBase;
            e._ship = model.transform;
            e._z = ShipModel.OuterRadius + 0.15f + HalfDepth;
            e._topY = ShipModel.FirstLevelFloor + 0.08f;
            Vector3 world = model.transform.TransformPoint(new Vector3(0f, 0f, e._z));
            Vector3 ground = rocketBase.Ground(world);
            e._bottomY = model.transform.InverseTransformPoint(ground).y + 0.25f;
            e.BuildPlatform();
            e.SetY(e._topY);
            return e;
        }

        private void BuildPlatform()
        {
            ShipMaterials.Ensure();
            _platform = new GameObject("Plataforma").transform;
            _platform.SetParent(transform, false);
            ShipMesh.Box(_platform, new Vector3(0f, -0.12f, 0f), new Vector3(HalfWidth * 2f, 0.24f, HalfDepth * 2f), ShipMaterials.Metal);
            ShipMesh.Box(_platform, new Vector3(0f, 0.01f, HalfDepth - 0.06f), new Vector3(HalfWidth * 2f, 0.02f, 0.12f), ShipMaterials.Hazard);
            ShipMesh.Box(_platform, new Vector3(0f, 0.01f, -HalfDepth + 0.06f), new Vector3(HalfWidth * 2f, 0.02f, 0.12f), ShipMaterials.Hazard);
            for (int s = -1; s <= 1; s += 2)
            {
                ShipMesh.Box(_platform, new Vector3(s * (HalfWidth - 0.02f), 0.75f, 0f), new Vector3(0.05f, 0.05f, HalfDepth * 2f), ShipMaterials.Orange);
                for (int k = 0; k < 4; k++)
                    ShipMesh.Box(_platform, new Vector3(s * (HalfWidth - 0.02f), 0.38f, -1.5f + k * 1.0f), new Vector3(0.05f, 0.75f, 0.05f), ShipMaterials.Metal);
                // Barandillas con colisión: el robot no se cae por los lados.
                var rail = _platform.gameObject.AddComponent<BoxCollider>();
                rail.center = new Vector3(s * (HalfWidth + 0.05f), 0.6f, 0f);
                rail.size = new Vector3(0.1f, 1.2f, HalfDepth * 2f);
            }
            ShipMesh.Combine(_platform);

            var deck = _platform.gameObject.AddComponent<BoxCollider>();
            deck.center = new Vector3(0f, -0.12f, 0f);
            deck.size = new Vector3(HalfWidth * 2f, 0.24f, HalfDepth * 2f);

            _outerGate = _platform.gameObject.AddComponent<BoxCollider>();
            _outerGate.center = new Vector3(0f, 0.6f, HalfDepth + 0.05f);
            _outerGate.size = new Vector3(HalfWidth * 2f, 1.2f, 0.1f);

            // Luz de aviso en la plataforma.
            var lightGo = new GameObject("Luz del ascensor");
            lightGo.transform.SetParent(_platform, false);
            lightGo.transform.localPosition = new Vector3(0f, 2.5f, 0f);
            var l = lightGo.AddComponent<Light>();
            l.type = LightType.Point;
            l.range = 8f;
            l.intensity = 1.2f;
            l.color = new Color(1f, 0.75f, 0.4f);
        }

        private void SetY(float y)
        {
            _y = y;
            _platform.localPosition = new Vector3(0f, y, _z);
            // La barrera exterior solo está cerrada arriba (o en marcha).
            if (_outerGate != null) _outerGate.enabled = _moving || y > _bottomY + 0.5f;
        }

        private Vector3 RobotLocal()
        {
            return _ship.InverseTransformPoint(_robot.transform.position);
        }

        private bool RobotOnPlatform(Vector3 local)
        {
            return Mathf.Abs(local.x) < HalfWidth + 0.2f && Mathf.Abs(local.z - _z) < HalfDepth + 0.3f &&
                   local.y > _y - 0.6f && local.y < _y + 1.8f;
        }

        private bool RobotAtHangarDoor(Vector3 local)
        {
            return local.y > _topY - 0.6f && local.y < _topY + 2f && local.z > ShipModel.InnerRadius - 1.6f &&
                   local.z < ShipModel.InnerRadius + 1f && Mathf.Abs(local.x) < 2f;
        }

        private bool RobotAtGroundEntrance(Vector3 local)
        {
            return local.y < _bottomY + 2.5f && Mathf.Abs(local.x) < 3f && local.z > _z - HalfDepth && local.z < _z + HalfDepth + 6f;
        }

        private void Update()
        {
            _prompt = "";
            if (_moving) return;
            if (_robot == null) _robot = FindAnyObjectByType<RobotController>();
            if (_robot == null || !_robot.InputEnabled) return;

            Vector3 local = RobotLocal();
            if (RobotOnPlatform(local))
            {
                bool up = !_atTop;
                _prompt = up ? Tr.T("F: subir al hangar") : Tr.T("F: bajar al suelo");
                if (RobotInput.InteractPressed) StartCoroutine(Ride(up ? _topY : _bottomY, true));
            }
            else if (!_atTop && RobotAtHangarDoor(local))
            {
                _prompt = Tr.T("F: llamar al ascensor");
                if (RobotInput.InteractPressed) StartCoroutine(Ride(_topY, false));
            }
            else if (_atTop && RobotAtGroundEntrance(local))
            {
                _prompt = Tr.T("F: llamar al ascensor (está arriba, en el hangar)");
                if (RobotInput.InteractPressed) StartCoroutine(Ride(_bottomY, false));
            }
        }

        /// <summary>Prueba automática: coloca el robot en la plataforma y viaja (true = bajar).</summary>
        public IEnumerator DebugRide(bool down)
        {
            if (_robot == null) _robot = FindAnyObjectByType<RobotController>();
            if (_robot == null) yield break;
            Rigidbody body = _robot.GetComponent<Rigidbody>();
            Vector3 p = _ship.TransformPoint(new Vector3(0f, _y + 0.3f, _z));
            if (body != null)
            {
                body.isKinematic = true;
                body.position = p;
            }
            _robot.transform.position = p;
            yield return new WaitForFixedUpdate();
            if (body != null) body.isKinematic = false;
            yield return new WaitForSeconds(0.5f);
            yield return Ride(down ? _bottomY : _topY, true);
        }

        /// <summary>Altura (local de la nave) del suelo al pie del ascensor.</summary>
        public float BottomY
        {
            get { return _bottomY; }
        }

        private IEnumerator Ride(float targetY, bool carryRobot)
        {
            _moving = true;
            Rigidbody body = carryRobot ? _robot.GetComponent<Rigidbody>() : null;
            if (carryRobot)
            {
                _robot.InputEnabled = false;
                if (body != null)
                {
                    body.linearVelocity = Vector3.zero;
                    body.angularVelocity = Vector3.zero;
                    body.isKinematic = true;
                }
            }
            SetY(_y);
            // Paso a paso de física: el robot tiene interpolación, así que se mueve su Rigidbody
            // (MovePosition) y no su transform, o la interpolación desharía el movimiento.
            var step = new WaitForFixedUpdate();
            while (!Mathf.Approximately(_y, targetY))
            {
                float prev = _y;
                float next = Mathf.MoveTowards(_y, targetY, Speed * Time.fixedDeltaTime);
                SetY(next);
                if (carryRobot)
                {
                    Vector3 delta = _ship.TransformVector(new Vector3(0f, next - prev, 0f));
                    if (body != null) body.MovePosition(body.position + delta);
                    else _robot.transform.position += delta;
                }
                yield return step;
            }
            _atTop = targetY > (_topY + _bottomY) * 0.5f;
            _moving = false;
            SetY(_y);
            if (carryRobot)
            {
                if (body != null) body.isKinematic = false;
                _robot.InputEnabled = true;
            }
        }

        private void OnGUI()
        {
            string text = _moving ? Tr.T("Ascensor en marcha…") : _prompt;
            if (string.IsNullOrEmpty(text)) return;
            if (_style == null)
            {
                _style = new GUIStyle(GUI.skin.box) { fontSize = 18, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
                _style.normal.textColor = new Color(1f, 0.85f, 0.5f);
            }
            GUI.Box(new Rect(Screen.width * 0.5f - 220, Screen.height - 150, 440, 40), text, _style);
        }
    }
}
