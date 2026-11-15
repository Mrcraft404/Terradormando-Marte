using UnityEngine;
using UnityEngine.Rendering;

namespace MarsRobot.World.TerrainSystem
{
    /// <summary>
    /// Un trozo del terreno. Los chunks NO se crean ni destruyen al moverse el robot:
    /// el <see cref="TerrainManager"/> los saca de un pool, les asigna una coordenada y
    /// les sube una malla nueva. Cada chunk conserva su propio objeto Mesh y lo reutiliza.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TerrainChunk : MonoBehaviour
    {
        private MeshFilter _filter;
        private MeshRenderer _renderer;
        private MeshCollider _collider;
        private Mesh _mesh;
        private bool _colliderWanted;

        /// <summary>Coordenada del chunk en la cuadrícula.</summary>
        public Vector2Int Coord { get; private set; }

        /// <summary>True mientras pertenece al conjunto activo (fuera del pool).</summary>
        public bool IsActive { get; private set; }

        /// <summary>LOD de la malla que se ve ahora mismo (−1 si aún no tiene malla).</summary>
        public int AppliedLod { get; private set; }

        /// <summary>LOD al que queremos llegar (el último pedido).</summary>
        public int TargetLod { get; set; }

        /// <summary>LOD pendiente en la cola de construcción (−1 = nada pendiente).</summary>
        public int QueuedLod { get; set; }

        /// <summary>Evita meter el mismo chunk dos veces en la lista de la cola.</summary>
        public bool InQueue { get; set; }

        /// <summary>
        /// Se incrementa en cada petición de malla. Un resultado que llega con una versión
        /// antigua (porque el chunk se descargó o cambió de LOD) se descarta.
        /// </summary>
        public int BuildVersion { get; set; }

        /// <summary>Distancia (en chunks) al robot calculada en el último refresco; sirve de prioridad.</summary>
        public float LastDistance { get; set; }

        /// <summary>True si tiene malla y, si se pidió, colisionador listo.</summary>
        public bool HasCollider
        {
            get { return _collider != null && _collider.enabled && _collider.sharedMesh != null; }
        }

        /// <summary>Crea los componentes. Lo llama el TerrainManager al instanciar el chunk.</summary>
        public void Setup(Material material, int layer)
        {
            gameObject.layer = layer;
            _filter = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _renderer.sharedMaterial = material;
            _renderer.shadowCastingMode = ShadowCastingMode.On;
            _collider = gameObject.AddComponent<MeshCollider>();
            _collider.enabled = false;

            _mesh = new Mesh();
            _mesh.name = "TerrainChunkMesh";
            _mesh.MarkDynamic(); // se reescribe al cambiar de LOD
            _filter.sharedMesh = _mesh;
            AppliedLod = -1;
            QueuedLod = -1;
        }

        /// <summary>Saca el chunk del pool y lo coloca en su posición del mundo.</summary>
        public void Activate(Vector2Int coord, float chunkSize)
        {
            Coord = coord;
            IsActive = true;
            AppliedLod = -1;
            TargetLod = -1;
            QueuedLod = -1;
            _colliderWanted = false;
            transform.localPosition = new Vector3(coord.x * chunkSize, 0f, coord.y * chunkSize);
            gameObject.name = "Chunk_" + coord.x + "_" + coord.y;
            // El renderer queda oculto hasta que llega la primera malla.
            _renderer.enabled = false;
            gameObject.SetActive(true);
        }

        /// <summary>Devuelve el chunk al pool liberando la memoria de su malla.</summary>
        public void Deactivate()
        {
            IsActive = false;
            QueuedLod = -1;
            BuildVersion++; // invalida cualquier trabajo en curso
            AppliedLod = -1;
            _colliderWanted = false;
            _collider.enabled = false;
            _collider.sharedMesh = null;
            _mesh.Clear(false); // libera vértices/índices: clave con poca RAM
            gameObject.SetActive(false);
        }

        /// <summary>Sube los datos generados a la malla de Unity (solo hilo principal).</summary>
        public void ApplyMesh(ChunkMeshData data, int[] triangles, float chunkSize, float maxHeight)
        {
            _mesh.Clear(false);
            _mesh.indexFormat = data.Vertices.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            _mesh.vertices = data.Vertices;
            _mesh.normals = data.Normals;
            _mesh.uv = data.Uvs;
            _mesh.triangles = triangles;
            // Límites conocidos: evitamos RecalculateBounds (más barato).
            _mesh.bounds = new Bounds(
                new Vector3(chunkSize * 0.5f, maxHeight * 0.5f, chunkSize * 0.5f),
                new Vector3(chunkSize, maxHeight + 20f, chunkSize));

            AppliedLod = data.Lod;
            _renderer.enabled = true;

            // Si este chunk necesita física, se vuelve a "cocinar" el colisionador con la malla nueva.
            if (_colliderWanted) RefreshCollider();
        }

        /// <summary>Activa o desactiva el colisionador (solo los chunks cercanos al robot lo tienen).</summary>
        public void SetColliderWanted(bool wanted)
        {
            if (wanted == _colliderWanted) return;
            _colliderWanted = wanted;
            if (wanted)
            {
                if (AppliedLod >= 0) RefreshCollider();
            }
            else
            {
                _collider.enabled = false;
                _collider.sharedMesh = null;
            }
        }

        private void RefreshCollider()
        {
            // Asignar null y después la malla obliga a Unity a recalcular la forma física.
            _collider.sharedMesh = null;
            _collider.sharedMesh = _mesh;
            _collider.enabled = true;
        }

        private void OnDestroy()
        {
            if (_mesh != null) Destroy(_mesh);
        }
    }
}
