// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonVoxelVolume.cs — Project HECTON-8 Voxel Volume Component         ║
// ║  Unity 6 | Simple component for cave volumes                             ║
// ║  v1.0 — Basic volume marker                                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using Unity.Collections;
using UnityEngine;
using Hecton8.World;

namespace Hecton8.Caves
{
    /// <summary>
    /// Runtime subtractive crater stamp applied to the voxel SDF field.
    /// Stored on the generated volume and replayed during async rebuilds.
    /// </summary>
    public struct VoxelCraterStamp
    {
        public Vector3 position;
        public float radius;
        public float blendRadius;
    }

    /// <summary>
    /// Simple component attached to generated cave volume GameObjects.
    /// Provides a way to identify and manage cave volumes in the scene.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonVoxelVolume : MonoBehaviour
    {
        private const string CaveDressingRootName = "_CaveDressing";
        private const string EntranceQualityRootName = "_EntranceQualityZone";
        private const string EntranceMarkersRootName = "_EntranceMarkers";
        private const string ColliderChunkRootName = "_ColliderChunks";
        private const int MaxCraterStampCount = 16;
        private const int MaxTerrainHoleHandleCount = 8;
        private const int MaxColliderChunkCount = 8;

        private HectonVoxelEngine _engine;
        private CaveNode[] _nodes = Array.Empty<CaveNode>();
        private CaveTunnel[] _tunnels = Array.Empty<CaveTunnel>();
        private CaveEntrance[] _entrances = Array.Empty<CaveEntrance>();
        private CaveStructure[] _structures = Array.Empty<CaveStructure>();
        private VoxelCraterStamp[] _craterStamps = Array.Empty<VoxelCraterStamp>();
        private int _craterStampCount;
        private int _runtimeStamp;
        private bool _runtimeDataReady;
        private bool _rebuildQueued;
        private bool _rebuildRunning;
        private uint _seed;
        private int _gridDimension;
        private float _voxelSize;
        private int _lodLevel;
        private bool _buildCollider;
        private CaveGenerationParams _caveParams;
        private int[] _terrainHoleHandles = Array.Empty<int>();
        private int _terrainHoleHandleCount;
        private Transform _colliderChunkRoot;
        private MeshCollider[] _colliderChunkColliders = Array.Empty<MeshCollider>();
        private Mesh[] _colliderChunkMeshes = Array.Empty<Mesh>();

        /// <summary>Reference to the cave instance key for cleanup.</summary>
        public long caveKey;

        /// <summary>World position where this volume was generated.</summary>
        public Vector3 generationPosition;

        /// <summary>Cave preset used to generate this volume.</summary>
        public CavePreset preset;

        /// <summary>Deterministic seed used to generate this volume.</summary>
        public uint Seed => _seed;

        /// <summary>Voxel grid resolution used by this runtime volume.</summary>
        public int GridDimension => _gridDimension;

        /// <summary>Voxel step size used by this runtime volume.</summary>
        public float VoxelSize => _voxelSize;

        /// <summary>LOD level used to build this runtime volume.</summary>
        public int LODLevel => _lodLevel;

        /// <summary>Whether collider rebuilds should be emitted with the mesh.</summary>
        public bool BuildCollider => _buildCollider;

        /// <summary>Current immutable cave-generation parameter snapshot.</summary>
        public CaveGenerationParams CaveParams => _caveParams;

        /// <summary>Captured room graph used for crater rebuilds.</summary>
        public CaveNode[] Nodes => _nodes;

        /// <summary>Captured tunnel graph used for crater rebuilds.</summary>
        public CaveTunnel[] Tunnels => _tunnels;

        /// <summary>Captured entrance graph used for crater rebuilds.</summary>
        public CaveEntrance[] Entrances => _entrances;

        /// <summary>Captured solid cave-structure graph used for crater rebuilds.</summary>
        public CaveStructure[] Structures => _structures;

        /// <summary>Bounded subtractive crater registry replayed during rebuilds.</summary>
        public VoxelCraterStamp[] CraterStamps => _craterStamps;

        /// <summary>Active crater stamp count inside <see cref="CraterStamps"/>.</summary>
        public int CraterStampCount => _craterStampCount;

        /// <summary>Generation stamp used to reject stale async rebuild completions.</summary>
        public int RuntimeStamp => _runtimeStamp;

        /// <summary>Whether this pooled volume currently has enough data to rebuild itself.</summary>
        public bool HasRuntimeData => _runtimeDataReady;

        /// <summary>
        /// Resets cave-owned runtime children so pooled volumes do not leak
        /// previous cave dressing or entrance readability state into the next build.
        /// </summary>
        public void PrepareForReuse()
        {
            UnregisterTerrainHoles();
            ResetColliderChunks(false);
            caveKey = 0L;
            generationPosition = Vector3.zero;
            preset = null;
            _engine = null;
            _nodes = Array.Empty<CaveNode>();
            _tunnels = Array.Empty<CaveTunnel>();
            _entrances = Array.Empty<CaveEntrance>();
            _structures = Array.Empty<CaveStructure>();
            _craterStamps = Array.Empty<VoxelCraterStamp>();
            _craterStampCount = 0;
            _runtimeDataReady = false;
            _rebuildQueued = false;
            _rebuildRunning = false;
            _seed = 0u;
            _gridDimension = 0;
            _voxelSize = 0f;
            _lodLevel = 0;
            _buildCollider = true;
            _caveParams = default;
            _terrainHoleHandles = Array.Empty<int>();
            _terrainHoleHandleCount = 0;
            _runtimeStamp++;

            ToggleChildRoot(CaveDressingRootName, false);
            ToggleChildRoot(EntranceQualityRootName, false);
            ToggleChildRoot(EntranceMarkersRootName, false);
        }

        /// <summary>
        /// Ensures the pooled collider chunk hierarchy exists and can serve the requested chunk count.
        /// </summary>
        public void EnsureColliderChunkCapacity(int chunkCount)
        {
            int clampedCount = Mathf.Clamp(chunkCount, 1, MaxColliderChunkCount);
            _colliderChunkRoot = GetOrCreateRuntimeRoot(ColliderChunkRootName);

            if (_colliderChunkColliders.Length < clampedCount)
            {
                // COLD ALLOC: MeshCollider[clampedCount] - pooled child collider registry for distributed voxel physics - owner: HectonVoxelVolume
                MeshCollider[] newColliders = new MeshCollider[clampedCount];
                // COLD ALLOC: Mesh[clampedCount] - pooled collider meshes for distributed voxel physics - owner: HectonVoxelVolume
                Mesh[] newMeshes = new Mesh[clampedCount];
                for (int i = 0; i < _colliderChunkColliders.Length; i++)
                {
                    newColliders[i] = _colliderChunkColliders[i];
                    newMeshes[i] = _colliderChunkMeshes[i];
                }

                _colliderChunkColliders = newColliders;
                _colliderChunkMeshes = newMeshes;
            }

            for (int i = 0; i < clampedCount; i++)
            {
                if (_colliderChunkColliders[i] != null)
                    continue;

                GameObject childObject = new GameObject($"ColliderChunk_{i:D2}");
                Transform child = childObject.transform;
                child.SetParent(_colliderChunkRoot, false);
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;

                MeshCollider collider = childObject.AddComponent<MeshCollider>();
                collider.enabled = false;
                _colliderChunkColliders[i] = collider;
            }

            if (!_colliderChunkRoot.gameObject.activeSelf)
                _colliderChunkRoot.gameObject.SetActive(true);
        }

        /// <summary>
        /// Returns the pooled child MeshCollider for the requested distributed collision chunk.
        /// </summary>
        public MeshCollider GetColliderChunkCollider(int index)
        {
            if (index < 0 || index >= _colliderChunkColliders.Length)
                return null;

            return _colliderChunkColliders[index];
        }

        /// <summary>
        /// Returns a reusable mesh instance for the requested collider chunk, creating it on first use only.
        /// </summary>
        public Mesh GetOrCreateColliderChunkMesh(int index)
        {
            if (index < 0 || index >= _colliderChunkMeshes.Length)
                return null;

            Mesh mesh = _colliderChunkMeshes[index];
            if (mesh != null)
                return mesh;

            mesh = new Mesh
            {
                name = $"VoxelColliderChunk_{index:D2}_{name}"
            };
            mesh.MarkDynamic();
            _colliderChunkMeshes[index] = mesh;
            return mesh;
        }

        /// <summary>
        /// Clears all pooled collider chunks. When destroyMeshes is true the mesh instances are destroyed permanently.
        /// </summary>
        public void ResetColliderChunks(bool destroyMeshes)
        {
            for (int i = 0; i < _colliderChunkColliders.Length; i++)
            {
                MeshCollider collider = _colliderChunkColliders[i];
                if (collider != null)
                {
                    collider.sharedMesh = null;
                    collider.enabled = false;
                    if (collider.gameObject.activeSelf)
                        collider.gameObject.SetActive(false);
                }

                Mesh mesh = i < _colliderChunkMeshes.Length ? _colliderChunkMeshes[i] : null;
                if (mesh == null)
                    continue;

                if (destroyMeshes)
                {
                    DestroyOwnedObject(mesh);
                    _colliderChunkMeshes[i] = null;
                }
                else
                {
                    mesh.Clear(false);
                }
            }

            if (_colliderChunkRoot != null && _colliderChunkRoot.gameObject.activeSelf)
                _colliderChunkRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// Enables collider chunks in the inclusive range [0, activeCount) and disables the rest.
        /// </summary>
        public void SetActiveColliderChunkCount(int activeCount)
        {
            int clampedActive = Mathf.Clamp(activeCount, 0, _colliderChunkColliders.Length);
            for (int i = 0; i < _colliderChunkColliders.Length; i++)
            {
                MeshCollider collider = _colliderChunkColliders[i];
                if (collider == null)
                    continue;

                if (i >= clampedActive && collider.gameObject.activeSelf)
                    collider.gameObject.SetActive(false);
            }

            if (_colliderChunkRoot != null)
                _colliderChunkRoot.gameObject.SetActive(clampedActive > 0);
        }

        /// <summary>
        /// Captures the immutable cave generation payload needed to rebuild this
        /// pooled volume after runtime SDF edits such as crater carving.
        /// </summary>
        public void ConfigureRuntimeData(
            HectonVoxelEngine engine,
            uint seed,
            Vector3 worldCenter,
            CavePreset cavePreset,
            int gridDimension,
            float voxelSize,
            int lodLevel,
            CaveGenerationParams caveParams,
            NativeArray<CaveNode> nodes,
            NativeArray<CaveTunnel> tunnels,
            NativeArray<CaveEntrance> entrances,
            NativeArray<CaveStructure> structures,
            bool buildCollider)
        {
            _engine = engine;
            _seed = seed;
            generationPosition = worldCenter;
            preset = cavePreset;
            _gridDimension = gridDimension;
            _voxelSize = voxelSize;
            _lodLevel = Mathf.Max(0, lodLevel);
            _caveParams = caveParams;
            _buildCollider = buildCollider;

            // COLD ALLOC: CaveNode[nodes.Length] - runtime room graph snapshot for crater rebuilds - owner: HectonVoxelVolume
            _nodes = new CaveNode[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
                _nodes[i] = nodes[i];

            // COLD ALLOC: CaveTunnel[tunnels.Length] - runtime tunnel graph snapshot for crater rebuilds - owner: HectonVoxelVolume
            _tunnels = new CaveTunnel[tunnels.Length];
            for (int i = 0; i < tunnels.Length; i++)
                _tunnels[i] = tunnels[i];

            // COLD ALLOC: CaveEntrance[entrances.Length] - runtime entrance snapshot for terrain-hole/skirt rebuilds - owner: HectonVoxelVolume
            _entrances = new CaveEntrance[entrances.Length];
            for (int i = 0; i < entrances.Length; i++)
                _entrances[i] = entrances[i];

            // COLD ALLOC: CaveStructure[structures.Length] - runtime structure snapshot for crater rebuilds - owner: HectonVoxelVolume
            _structures = new CaveStructure[structures.Length];
            for (int i = 0; i < structures.Length; i++)
                _structures[i] = structures[i];

            if (_craterStamps.Length != MaxCraterStampCount)
            {
                // COLD ALLOC: VoxelCraterStamp[MaxCraterStampCount] - bounded runtime crater registry - owner: HectonVoxelVolume
                _craterStamps = new VoxelCraterStamp[MaxCraterStampCount];
            }

            if (_terrainHoleHandles.Length != MaxTerrainHoleHandleCount)
            {
                // COLD ALLOC: int[MaxTerrainHoleHandleCount] - stable terrain-hole handle registry for cave entrance lifecycle - owner: HectonVoxelVolume
                _terrainHoleHandles = new int[MaxTerrainHoleHandleCount];
            }

            _craterStampCount = 0;
            _terrainHoleHandleCount = 0;
            _runtimeDataReady = true;
            _rebuildQueued = false;
            _rebuildRunning = false;
            _runtimeStamp++;
        }

        /// <summary>
        /// Returns true when the provided async rebuild token still matches the current pooled runtime payload.
        /// </summary>
        public bool MatchesRuntimeStamp(int stamp)
        {
            return _runtimeStamp == stamp;
        }

        /// <summary>
        /// Adds a subtractive crater stamp to the volume SDF and schedules an async rebuild.
        /// Call this when large fauna or cargo impacts should gouge the cave wall.
        /// </summary>
        public void CarveCrater(Vector3 pos, float radius)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return;

            float clampedRadius = Mathf.Max(_voxelSize * 1.25f, radius);
            float blendRadius = Mathf.Max(_voxelSize, clampedRadius * 0.35f);

            for (int i = 0; i < _craterStampCount; i++)
            {
                VoxelCraterStamp existing = _craterStamps[i];
                float mergeDistance = existing.radius + clampedRadius * 0.35f;
                if ((existing.position - pos).sqrMagnitude > mergeDistance * mergeDistance)
                    continue;

                existing.position = Vector3.Lerp(existing.position, pos, 0.5f);
                existing.radius = Mathf.Max(existing.radius, clampedRadius);
                existing.blendRadius = Mathf.Max(existing.blendRadius, blendRadius);
                _craterStamps[i] = existing;
                QueueRebuild();
                return;
            }

            if (_craterStampCount >= MaxCraterStampCount)
            {
                for (int i = 1; i < _craterStampCount; i++)
                    _craterStamps[i - 1] = _craterStamps[i];

                _craterStampCount = MaxCraterStampCount - 1;
            }

            _craterStamps[_craterStampCount++] = new VoxelCraterStamp
            {
                position = pos,
                radius = clampedRadius,
                blendRadius = blendRadius
            };

            QueueRebuild();
        }

        /// <summary>
        /// Adds a subtractive abyssal crater stamp and queues async mesh rebuild.
        /// Alias kept explicit for gameplay callers that operate in abyssal terms.
        /// </summary>
        public void CarveAbyssalCrater(Vector3 pos, float radius)
        {
            CarveCrater(pos, radius);
        }

        /// <summary>
        /// Tracks a persistent terrain-hole handle so cave unload can restore vegetation generation.
        /// </summary>
        public void TrackTerrainHoleHandle(int holeHandle)
        {
            if (holeHandle <= 0)
                return;

            for (int i = 0; i < _terrainHoleHandleCount; i++)
            {
                if (_terrainHoleHandles[i] == holeHandle)
                    return;
            }

            if (_terrainHoleHandleCount >= MaxTerrainHoleHandleCount)
                return;

            _terrainHoleHandles[_terrainHoleHandleCount++] = holeHandle;
        }

        /// <summary>
        /// Ensures a named direct child root exists and is active.
        /// Reused by cave readability/detail systems to avoid duplicate runtime roots.
        /// </summary>
        public Transform GetOrCreateRuntimeRoot(string childName)
        {
            if (string.IsNullOrEmpty(childName))
                return null;

            Transform child = transform.Find(childName);
            if (child != null)
            {
                if (!child.gameObject.activeSelf)
                    child.gameObject.SetActive(true);
                return child;
            }

            GameObject childObject = new GameObject(childName);
            child = childObject.transform;
            child.SetParent(transform, false);
            return child;
        }

        private void ToggleChildRoot(string childName, bool active)
        {
            if (string.IsNullOrEmpty(childName))
                return;

            Transform child = transform.Find(childName);
            if (child == null || child.gameObject.activeSelf == active)
                return;

            child.gameObject.SetActive(active);
        }

        private void QueueRebuild()
        {
            _rebuildQueued = true;
            if (_rebuildRunning)
                return;

            _ = ProcessQueuedRebuildsAsync(_runtimeStamp);
        }

        private async Awaitable ProcessQueuedRebuildsAsync(int expectedRuntimeStamp)
        {
            if (_rebuildRunning)
                return;

            _rebuildRunning = true;
            try
            {
                while (_rebuildQueued && MatchesRuntimeStamp(expectedRuntimeStamp))
                {
                    _rebuildQueued = false;
                    HectonVoxelEngine engine = _engine != null ? _engine : HectonVoxelEngine.ActiveRuntimeInstance;
                    if (engine == null)
                        return;

                    if (!await engine.RebuildVolumeAsync(this, expectedRuntimeStamp))
                        return;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[HectonVoxelVolume] Crater rebuild failed on '{name}': {ex.Message}", this);
            }
            finally
            {
                _rebuildRunning = false;
            }
        }

        private void UnregisterTerrainHoles()
        {
            if (_terrainHoleHandleCount <= 0)
                return;

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
            {
                _terrainHoleHandleCount = 0;
                return;
            }

            for (int i = 0; i < _terrainHoleHandleCount; i++)
            {
                int holeHandle = _terrainHoleHandles[i];
                if (holeHandle <= 0)
                    continue;

                vegetationBridge.UnregisterTerrainHole(holeHandle);
                _terrainHoleHandles[i] = 0;
            }

            _terrainHoleHandleCount = 0;
        }

        private void OnDestroy()
        {
            UnregisterTerrainHoles();
            ResetColliderChunks(true);
            _runtimeStamp++;
        }

        private static void DestroyOwnedObject(UnityEngine.Object obj)
        {
            if (obj == null)
                return;
#if UNITY_EDITOR
            if (!Application.isPlaying) DestroyImmediate(obj);
            else Destroy(obj);
#else
            Destroy(obj);
#endif
        }
    }
}
