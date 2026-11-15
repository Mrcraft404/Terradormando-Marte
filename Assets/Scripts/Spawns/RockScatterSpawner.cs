using System.Collections.Generic;
using MarsRobot.Base;
using MarsRobot.Core;
using MarsRobot.Interaction;
using MarsRobot.Robot;
using MarsRobot.World.TerrainSystem;
using UnityEngine;

namespace MarsRobot.Spawns
{
    /// <summary>
    /// Reparte rocas minables (ResourceNode) por el terreno cercano al rover.
    ///  • Solo en los chunks a 2 de distancia del rover (5×5): así hay pocas rocas con colisión a la vez.
    ///  • Deterministas: el mismo chunk genera siempre las mismas rocas (semilla = zona + chunk).
    ///  • Las agotadas se recuerdan y no vuelven a aparecer.
    ///  • Cantidad según la densidad de rocas de la zona; nunca dentro de la base.
    ///  • Las rocas densas son afiladas: dañan más al chocar (SurfaceHazard).
    /// </summary>
    public sealed class RockScatterSpawner : MonoBehaviour
    {
        [SerializeField, Min(0)] private int radiusInChunks = 2;
        [SerializeField, Min(0f)] private float maxRocksPerChunk = 14f;
        [SerializeField, Min(0f)] private float baseExclusionRadius = 32f;
        [SerializeField, Min(0.1f)] private float refreshSeconds = 0.5f;

        private readonly Dictionary<Vector2Int, List<ResourceNode>> _spawned = new Dictionary<Vector2Int, List<ResourceNode>>();
        private readonly HashSet<string> _depleted = new HashSet<string>();
        private readonly List<Vector2Int> _toRemove = new List<Vector2Int>();
        private TerrainManager _terrain;
        private Transform _target;
        private LandingZoneData _zone;
        private Material _looseMat, _denseMat;
        private float _timer;

        public void Setup(TerrainManager terrain, Transform target, LandingZoneData zone)
        {
            _terrain = terrain;
            _target = target;
            _zone = zone;
        }

        private void OnEnable()
        {
            ResourceNode.Depleted += OnDepleted;
        }

        private void OnDisable()
        {
            ResourceNode.Depleted -= OnDepleted;
        }

        private void OnDepleted(ResourceNode node)
        {
            if (!string.IsNullOrEmpty(node.PersistentId)) _depleted.Add(node.PersistentId);
        }

        private void Update()
        {
            if (_terrain == null || !_terrain.IsMapReady) return;
            // Sigue a lo que siga el terreno (el rover, o el coche si vas conduciendo).
            Transform follow = _target != null && _target.gameObject.activeInHierarchy ? _target : null;
            if (follow == null)
            {
                RobotController rc;
                if (GameServices.TryGet(out rc) && rc.gameObject.activeInHierarchy) follow = rc.transform;
            }
            if (follow == null) return;

            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = refreshSeconds;

            Vector2Int center = _terrain.WorldToChunk(follow.position);
            int mapSize = _terrain.Settings.mapSizeInChunks;

            // Quitar los lejanos.
            _toRemove.Clear();
            foreach (var kv in _spawned)
            {
                if (Mathf.Abs(kv.Key.x - center.x) > radiusInChunks + 1 || Mathf.Abs(kv.Key.y - center.y) > radiusInChunks + 1) _toRemove.Add(kv.Key);
            }
            for (int i = 0; i < _toRemove.Count; i++)
            {
                foreach (ResourceNode n in _spawned[_toRemove[i]]) if (n != null) Destroy(n.gameObject);
                _spawned.Remove(_toRemove[i]);
            }

            // Crear los cercanos (uno por ciclo para no dar tirones).
            for (int dz = -radiusInChunks; dz <= radiusInChunks; dz++)
            {
                for (int dx = -radiusInChunks; dx <= radiusInChunks; dx++)
                {
                    var c = new Vector2Int(center.x + dx, center.y + dz);
                    if (c.x < 0 || c.y < 0 || c.x >= mapSize || c.y >= mapSize || _spawned.ContainsKey(c)) continue;
                    _spawned[c] = SpawnChunk(c);
                    return;
                }
            }
        }

        private List<ResourceNode> SpawnChunk(Vector2Int coord)
        {
            var list = new List<ResourceNode>();
            if (_looseMat == null)
            {
                _looseMat = Mat(new Color(0.48f, 0.27f, 0.17f));
                _denseMat = Mat(new Color(0.28f, 0.2f, 0.18f));
            }

            float density = _zone != null ? _zone.rockDensity : 0.3f;
            int seed = (_zone != null ? _zone.terrain.seed : 1) * 73856093 ^ coord.x * 19349663 ^ coord.y * 83492791;
            var rng = new System.Random(seed);
            int count = Mathf.RoundToInt(density * maxRocksPerChunk * (0.6f + (float)rng.NextDouble() * 0.8f));
            float size = _terrain.Settings.chunkWorldSize;

            RocketBase rocketBase;
            GameServices.TryGet(out rocketBase);
            RoverToolController tools = null;
            RobotController rc;
            if (GameServices.TryGet(out rc)) tools = rc.GetComponent<RoverToolController>();

            for (int i = 0; i < count; i++)
            {
                float lx = (float)rng.NextDouble() * size, lz = (float)rng.NextDouble() * size;
                float scale = Mathf.Lerp(0.6f, 2.4f, (float)Mathf.Pow((float)rng.NextDouble(), 2f));
                bool dense = rng.NextDouble() < 0.35 + density * 0.3;
                string id = coord.x + "_" + coord.y + "_" + i;
                if (_depleted.Contains(id)) continue;

                Vector3 world = _terrain.transform.TransformPoint(new Vector3(coord.x * size + lx, 0f, coord.y * size + lz));
                if (rocketBase != null)
                {
                    Vector3 d = world - rocketBase.transform.position;
                    d.y = 0f;
                    if (d.magnitude < baseExclusionRadius) continue;
                }
                world.y = _terrain.GetHeightAt(world) - scale * 0.25f; // medio enterrada

                GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.name = dense ? "Roca densa" : "Roca suelta";
                go.transform.SetParent(transform, true);
                go.transform.position = world;
                go.transform.rotation = Quaternion.Euler((float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 360f);
                go.transform.localScale = new Vector3(scale * (0.8f + (float)rng.NextDouble() * 0.5f), scale * (0.5f + (float)rng.NextDouble() * 0.4f), scale);
                go.GetComponent<Renderer>().sharedMaterial = dense ? _denseMat : _looseMat;

                if (dense)
                {
                    var hazard = go.AddComponent<SurfaceHazard>();
                    hazard.impactMultiplier = 1.8f; // aristas afiladas
                }

                // Composición a partir del suelo del lugar (CRISM real o estimación de la zona).
                RoverToolController.SoilReading s = tools != null ? tools.ReadSoil(world) : default(RoverToolController.SoilReading);
                var node = go.AddComponent<ResourceNode>();
                node.PersistentId = id;
                node.Setup(dense ? RockHardness.Dense : RockHardness.Loose,
                    Mathf.RoundToInt(scale * (dense ? 4f : 2.5f)),
                    Mathf.Clamp01(s.iron + (dense ? 0.15f : 0f)), s.clays, s.sulfates, s.ice,
                    dense ? new Color(0.28f, 0.2f, 0.18f) : new Color(0.48f, 0.27f, 0.17f));
                list.Add(node);
            }
            return list;
        }

        private static Material Mat(Color c)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            var m = new Material(shader) { color = c };
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.1f);
            return m;
        }
    }
}
