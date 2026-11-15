using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.UI.Minimap
{
    /// <summary>
    /// Niebla de guerra del minimapa: al empezar todo el mapa de la zona está oscuro y se va
    /// revelando alrededor del rover (más radio con la antena de alta ganancia) y de las balizas
    /// de comunicaciones construidas. Una textura de 256×256 cubre la zona entera
    /// (con un mapa de 2 km, 8 m por píxel): ocupa 256 KB.
    /// </summary>
    public sealed class MinimapFogOfWar : MonoBehaviour
    {
        [SerializeField, Range(64, 512)] private int resolution = 256;
        [SerializeField, Min(5f)] private float roverRevealRadius = 45f;
        [SerializeField, Min(5f)] private float beaconRevealRadius = 120f;
        [SerializeField, Min(0.05f)] private float updateSeconds = 0.3f;

        private Texture2D _fog;
        private Color32[] _pixels;
        private bool _dirty;
        private float _timer;
        private TerrainManager _terrain;
        private float _worldSize = 1f;
        private Vector3 _origin;
        private int _beaconsRevealed;

        public Texture2D Texture
        {
            get { return _fog; }
        }

        /// <summary>Lo pone el minimapa: radio extra con la antena de alta ganancia.</summary>
        public float RevealMultiplier { get; set; } = 1f;

        /// <summary>Fracción del mapa explorada (0..1).</summary>
        public float Explored01 { get; private set; }

        private void Awake()
        {
            _fog = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "Niebla del minimapa" };
            _pixels = new Color32[resolution * resolution];
            for (int i = 0; i < _pixels.Length; i++) _pixels[i] = new Color32(8, 6, 6, 255);
            _fog.SetPixels32(_pixels);
            _fog.Apply(false);
        }

        private void OnDestroy()
        {
            if (_fog != null) Destroy(_fog);
        }

        /// <summary>Rectángulo UV de la textura que corresponde a un cuadrado del mundo (para dibujarla bajo el mapa).</summary>
        public Rect UVRect(Vector3 center, float halfSize)
        {
            float u = (center.x - _origin.x - halfSize) / _worldSize;
            float v = (center.z - _origin.z - halfSize) / _worldSize;
            float s = halfSize * 2f / _worldSize;
            return new Rect(u, v, s, s);
        }

        public void Reveal(Vector3 world, float radius)
        {
            if (_terrain == null) return;
            float px = (world.x - _origin.x) / _worldSize * resolution;
            float pz = (world.z - _origin.z) / _worldSize * resolution;
            float pr = radius / _worldSize * resolution;
            int x0 = Mathf.Max(0, Mathf.FloorToInt(px - pr)), x1 = Mathf.Min(resolution - 1, Mathf.CeilToInt(px + pr));
            int z0 = Mathf.Max(0, Mathf.FloorToInt(pz - pr)), z1 = Mathf.Min(resolution - 1, Mathf.CeilToInt(pz + pr));
            float r2 = pr * pr;
            for (int z = z0; z <= z1; z++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float dx = x - px, dz = z - pz;
                    float d2 = dx * dx + dz * dz;
                    if (d2 > r2) continue;
                    // Borde suave.
                    byte a = (byte)(255 * Mathf.Clamp01((Mathf.Sqrt(d2) - pr * 0.75f) / (pr * 0.25f)));
                    int idx = z * resolution + x;
                    if (a < _pixels[idx].a)
                    {
                        _pixels[idx].a = a;
                        _dirty = true;
                    }
                }
            }
        }

        private void Update()
        {
            if (_terrain == null)
            {
                if (!GameServices.TryGet(out _terrain) || !_terrain.IsMapReady) return;
                _worldSize = _terrain.Settings.WorldSize;
                _origin = _terrain.transform.position;
            }

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = updateSeconds;

            Transform t = MarsMinimapController.FollowTarget();
            if (t != null) Reveal(t.position, roverRevealRadius * RevealMultiplier);

            // Balizas nuevas.
            RocketBase b;
            if (GameServices.TryGet(out b))
            {
                int count = 0;
                for (int i = 0; i < b.Grid.Structures.Count; i++)
                {
                    PlacedStructure s = b.Grid.Structures[i];
                    if (s == null) continue;
                    if (s.Definition.kind == StructureKind.CommBeacon)
                    {
                        count++;
                        if (count > _beaconsRevealed) Reveal(s.transform.position, beaconRevealRadius);
                    }
                }
                _beaconsRevealed = count;
                if (_beaconsRevealed == 0) Reveal(b.transform.position, 30f);
            }

            if (_dirty)
            {
                _dirty = false;
                _fog.SetPixels32(_pixels);
                _fog.Apply(false);
                int clear = 0;
                for (int i = 0; i < _pixels.Length; i += 7) if (_pixels[i].a < 128) clear++;
                Explored01 = clear / (_pixels.Length / 7f);
            }
        }
    }
}
