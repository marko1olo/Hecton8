// ╔══════════════════════════════════════════════════════════════════════════════╗
// ║  HectonVoxelVolume.cs — Project HECTON-8 Voxel Volume Component         ║
// ║  Unity 6 | Simple component for cave volumes                             ║
// ║  v1.0 — Basic volume marker                                              ║
// ╚══════════════════════════════════════════════════════════════════════════════╝

using System;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Hecton8.Core;
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
    /// Runtime physics/collider bake gate for voxel chunk interaction safety.
    /// </summary>
    public enum VoxelBakeState : byte
    {
        Idle = 0,
        Pending = 1,
        Baking = 2,
        Complete = 3
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
        private const int MaxPlasmaCutSteps = 24;
        private const int MaxQueuedRebuildPassesPerKick = 4;
        private const float MinPlasmaCutPower = 0.02f;
        private const byte DefaultDeltaMaterialId = 0;

        private HectonVoxelEngine _engine;
        private VoxelDeltaProcessor _deltaProcessor;
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
        private Vector3 _generationAbsoluteUniversePosition;
        private int[] _terrainHoleHandles = Array.Empty<int>();
        private int _terrainHoleHandleCount;
        private Transform _colliderChunkRoot;
        private MeshCollider[] _colliderChunkColliders = Array.Empty<MeshCollider>();
        private Mesh[] _colliderChunkMeshes = Array.Empty<Mesh>();
        private MeshRenderer _meshRenderer;
        private MeshCollider _rootMeshCollider;
        private VoxelBakeState _bakeState;

        /// <summary>Reference to the cave instance key for cleanup.</summary>
        public long caveKey;

        /// <summary>World position where this volume was generated.</summary>
        public Vector3 generationPosition;

        /// <summary>Cave preset used to generate this volume.</summary>
        public CavePreset preset;

        /// <summary>Deterministic seed used to generate this volume.</summary>
        public uint Seed => _seed;

        /// <summary>Absolute-universe center captured when this volume payload was built.</summary>
        public Vector3 GenerationAbsoluteUniversePosition => _generationAbsoluteUniversePosition;

        /// <summary>Voxel grid resolution used by this runtime volume.</summary>
        public int GridDimension => _gridDimension;

        /// <summary>Voxel step size used by this runtime volume.</summary>
        public float VoxelSize => _voxelSize;

        /// <summary>
        /// Resolves the nearest voxel-corner world position for a raycast hit on this volume.
        /// The dominant hit-normal axis is preserved so cable bends snap onto the struck voxel face
        /// instead of drifting across the polygon midpoint.
        /// </summary>
        public bool TryResolveNearestVoxelCorner(Vector3 worldPosition, Vector3 worldNormal, out Vector3 cornerWorld)
        {
            cornerWorld = worldPosition;
            if (_gridDimension <= 0 || _voxelSize <= 0f)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            Transform cachedTransform = transform;
            Vector3 localPoint = cachedTransform.InverseTransformPoint(worldPosition);
            Vector3 localNormal = cachedTransform.InverseTransformDirection(worldNormal);
            float voxelStep = Mathf.Max(0.0001f, _voxelSize);
            Vector3 relative = (localPoint - localBounds.min) / voxelStep;
            int dominantAxis = ResolveDominantAxis(localNormal);

            float cornerX = ResolveCornerCoordinate(relative.x, dominantAxis == 0 ? localNormal.x : 0f, _gridDimension);
            float cornerY = ResolveCornerCoordinate(relative.y, dominantAxis == 1 ? localNormal.y : 0f, _gridDimension);
            float cornerZ = ResolveCornerCoordinate(relative.z, dominantAxis == 2 ? localNormal.z : 0f, _gridDimension);

            Vector3 localCorner = localBounds.min + new Vector3(
                cornerX * voxelStep,
                cornerY * voxelStep,
                cornerZ * voxelStep);
            cornerWorld = cachedTransform.TransformPoint(localCorner);
            return true;
        }

        /// <summary>
        /// Tentacle / appendage helper alias for the nearest voxel-corner query.
        /// Kept on the runtime volume owner so gameplay code does not reach into voxel build internals.
        /// </summary>
        public bool TryGetNearestCorner(Vector3 worldPosition, Vector3 worldNormal, out Vector3 cornerWorld)
        {
            return TryResolveNearestVoxelCorner(worldPosition, worldNormal, out cornerWorld);
        }

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

        /// <summary>Current bake gate state used for collider and interaction locking.</summary>
        public VoxelBakeState BakeState => _bakeState;

        private static int ResolveDominantAxis(Vector3 normal)
        {
            Vector3 absNormal = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            if (absNormal.x >= absNormal.y && absNormal.x >= absNormal.z)
                return 0;

            return absNormal.y >= absNormal.z ? 1 : 2;
        }

        private static float ResolveCornerCoordinate(float coordinate, float signedFaceAxis, int gridDimension)
        {
            float cornerIndex;
            if (signedFaceAxis > 0.0001f)
            {
                cornerIndex = Mathf.Ceil(coordinate);
            }
            else if (signedFaceAxis < -0.0001f)
            {
                cornerIndex = Mathf.Floor(coordinate);
            }
            else
            {
                cornerIndex = Mathf.Round(coordinate);
            }

            return Mathf.Clamp(cornerIndex, 0f, gridDimension);
        }

        /// <summary>
        /// Resets cave-owned runtime children so pooled volumes do not leak
        /// previous cave dressing or entrance readability state into the next build.
        /// </summary>
        public void PrepareForReuse()
        {
            _deltaProcessor?.UnregisterVolume(this);
            UnregisterTerrainHoles();
            ResetColliderChunks(false);
            caveKey = 0L;
            generationPosition = Vector3.zero;
            preset = null;
            _engine = null;
            _deltaProcessor = null;
            _generationAbsoluteUniversePosition = Vector3.zero;
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
            CacheRuntimeComponents();
            SetBakeState(VoxelBakeState.Idle);

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

                bool shouldBeActive = i < clampedActive;
                if (collider.gameObject.activeSelf != shouldBeActive)
                    collider.gameObject.SetActive(shouldBeActive);
            }

            if (_colliderChunkRoot != null)
                _colliderChunkRoot.gameObject.SetActive(clampedActive > 0);

            RefreshBakePresentation();
        }

        /// <summary>
        /// Captures the immutable cave generation payload needed to rebuild this
        /// pooled volume after runtime SDF edits such as crater carving.
        /// </summary>
        public void ConfigureRuntimeData(
            HectonVoxelEngine engine,
            uint seed,
            Vector3 worldCenter,
            Vector3 absoluteUniverseOffset,
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
            _deltaProcessor = engine != null ? engine.DeltaProcessor : null;
            _seed = seed;
            generationPosition = worldCenter;
            _generationAbsoluteUniversePosition = worldCenter + absoluteUniverseOffset;
            preset = cavePreset;
            _gridDimension = gridDimension;
            _voxelSize = voxelSize;
            _lodLevel = Mathf.Max(0, lodLevel);
            _caveParams = caveParams;
            _buildCollider = buildCollider;

            // COLD ALLOC: CaveNode[nodes.Length] - runtime room graph snapshot for crater rebuilds - owner: HectonVoxelVolume
            _nodes = new CaveNode[nodes.Length];
            for (int i = 0; i < nodes.Length; i++)
            {
                CaveNode snapshot = nodes[i];
                snapshot.position += (Unity.Mathematics.float3)absoluteUniverseOffset;
                _nodes[i] = snapshot;
            }

            // COLD ALLOC: CaveTunnel[tunnels.Length] - runtime tunnel graph snapshot for crater rebuilds - owner: HectonVoxelVolume
            _tunnels = new CaveTunnel[tunnels.Length];
            for (int i = 0; i < tunnels.Length; i++)
            {
                CaveTunnel snapshot = tunnels[i];
                snapshot.pointA += (Unity.Mathematics.float3)absoluteUniverseOffset;
                snapshot.pointB += (Unity.Mathematics.float3)absoluteUniverseOffset;
                _tunnels[i] = snapshot;
            }

            // COLD ALLOC: CaveEntrance[entrances.Length] - runtime entrance snapshot for terrain-hole/skirt rebuilds - owner: HectonVoxelVolume
            _entrances = new CaveEntrance[entrances.Length];
            for (int i = 0; i < entrances.Length; i++)
            {
                CaveEntrance snapshot = entrances[i];
                snapshot.surfacePosition += (Unity.Mathematics.float3)absoluteUniverseOffset;
                _entrances[i] = snapshot;
            }

            // COLD ALLOC: CaveStructure[structures.Length] - runtime structure snapshot for crater rebuilds - owner: HectonVoxelVolume
            _structures = new CaveStructure[structures.Length];
            for (int i = 0; i < structures.Length; i++)
            {
                CaveStructure snapshot = structures[i];
                snapshot.position += (Unity.Mathematics.float3)absoluteUniverseOffset;
                snapshot.pointB += (Unity.Mathematics.float3)absoluteUniverseOffset;
                _structures[i] = snapshot;
            }

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
            CacheRuntimeComponents();
            SetBakeState(VoxelBakeState.Complete);
            _deltaProcessor?.RegisterVolume(this);
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

            if (_deltaProcessor != null)
            {
                SetBakeState(VoxelBakeState.Pending);
                _deltaProcessor.ApplyImmediateCrater(this, pos, radius, DefaultDeltaMaterialId);
                return;
            }

            Vector3 absolutePosition = HectonFloatingOrigin.ToAbsoluteUniversePosition(pos);
            AppendCraterStamp(absolutePosition, radius, true);
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
        /// Marches a bounded DDA cut path through the runtime voxel volume and converts the traversed cells
        /// into subtractive crater stamps owned by the authoritative rebuild pipeline.
        /// </summary>
        /// <param name="absoluteHitPoint">Absolute-universe entry point on the volume surface.</param>
        /// <param name="direction">Runtime beam direction.</param>
        /// <param name="normalizedPower">Normalized beam power [0..1].</param>
        /// <param name="maxDistance">Maximum authored beam range.</param>
        /// <returns>True when at least one voxel cell was converted into a crater stamp.</returns>
        public bool ApplyPlasmaCutDda(
            Vector3 absoluteHitPoint,
            Vector3 direction,
            float normalizedPower,
            float maxDistance)
        {
            if (!_runtimeDataReady || _gridDimension <= 0 || _voxelSize <= 0f || _bakeState != VoxelBakeState.Complete)
                return false;

            if (!CaveRuntimeBoundsUtility.TryResolveLocalVolumeBounds(this, preset, out Bounds localBounds))
                return false;

            float clampedPower = Mathf.Clamp01(normalizedPower);
            if (clampedPower < MinPlasmaCutPower)
                return false;

            Vector3 runtimeHitPoint = HectonFloatingOrigin.ToRuntimePosition(absoluteHitPoint);
            Transform cachedTransform = transform;
            Vector3 localDirection = cachedTransform.InverseTransformDirection(direction);
            if (localDirection.sqrMagnitude < 0.0001f)
                return false;

            localDirection.Normalize();

            Vector3 localStart = cachedTransform.InverseTransformPoint(runtimeHitPoint) + localDirection * (_voxelSize * 0.55f);
            if (!localBounds.Contains(localStart))
            {
                localStart += localDirection * (_voxelSize * 0.55f);
                if (!localBounds.Contains(localStart))
                    return false;
            }

            Vector3 relative = localStart - localBounds.min;
            int3 voxel = (int3)math.floor(new float3(relative.x, relative.y, relative.z) / _voxelSize);
            if (!IsVoxelIndexInBounds(voxel))
                return false;

            int3 step = new int3(
                ResolveStep(localDirection.x),
                ResolveStep(localDirection.y),
                ResolveStep(localDirection.z));
            float3 start = new float3(localStart.x, localStart.y, localStart.z);
            float3 dir = new float3(localDirection.x, localDirection.y, localDirection.z);
            float3 tMax = new float3(
                ResolveBoundaryDistance(localBounds.min.x, start.x, dir.x, voxel.x, step.x, _voxelSize),
                ResolveBoundaryDistance(localBounds.min.y, start.y, dir.y, voxel.y, step.y, _voxelSize),
                ResolveBoundaryDistance(localBounds.min.z, start.z, dir.z, voxel.z, step.z, _voxelSize));
            float3 tDelta = new float3(
                ResolveDeltaDistance(dir.x, _voxelSize),
                ResolveDeltaDistance(dir.y, _voxelSize),
                ResolveDeltaDistance(dir.z, _voxelSize));

            float travel = 0f;
            float maxTravel = Mathf.Max(_voxelSize, Mathf.Min(maxDistance, _voxelSize * MaxPlasmaCutSteps));
            float remainingPower = clampedPower;
            float stampRadius = Mathf.Max(_voxelSize * 0.6f, _voxelSize * Mathf.Lerp(0.75f, 1.1f, clampedPower));
            Vector3 committedOffset = HectonFloatingOrigin.CurrentTotalOffset;
            bool modified = false;

            if (_deltaProcessor != null)
                SetBakeState(VoxelBakeState.Pending);

            for (int stepIndex = 0; stepIndex < MaxPlasmaCutSteps; stepIndex++)
            {
                if (!IsVoxelIndexInBounds(voxel) || remainingPower < MinPlasmaCutPower || travel > maxTravel)
                    break;

                Vector3 localCenter = localBounds.min + new Vector3(
                    (voxel.x + 0.5f) * _voxelSize,
                    (voxel.y + 0.5f) * _voxelSize,
                    (voxel.z + 0.5f) * _voxelSize);
                Vector3 worldCenter = cachedTransform.TransformPoint(localCenter);
                Vector3 absoluteCenter = worldCenter + committedOffset;
                if (_deltaProcessor != null)
                {
                    _deltaProcessor.ApplyImmediateAbsoluteCrater(this, absoluteCenter, stampRadius * remainingPower, DefaultDeltaMaterialId);
                    modified = true;
                }
                else
                {
                    modified |= AppendCraterStamp(absoluteCenter, stampRadius * remainingPower, false);
                }

                float nextTravel;
                int axis = ResolveMarchAxis(tMax, out nextTravel);
                float segmentLength = Mathf.Max(_voxelSize * 0.25f, nextTravel - travel);
                remainingPower *= Mathf.Exp(-segmentLength);
                travel = nextTravel;
                if (travel > maxTravel)
                    break;

                switch (axis)
                {
                    case 0:
                        voxel.x += step.x;
                        tMax.x += tDelta.x;
                        break;
                    case 1:
                        voxel.y += step.y;
                        tMax.y += tDelta.y;
                        break;
                    default:
                        voxel.z += step.z;
                        tMax.z += tDelta.z;
                        break;
                }
            }

            if (modified && _deltaProcessor == null)
                QueueRebuild();

            return modified;
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

        internal void RequestDeltaRebuild()
        {
            if (!_runtimeDataReady)
                return;

            QueueRebuild();
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

        private void CacheRuntimeComponents()
        {
            if (_meshRenderer == null)
                TryGetComponent(out _meshRenderer);

            if (_rootMeshCollider == null)
                TryGetComponent(out _rootMeshCollider);
        }

        private void SetBakeState(VoxelBakeState state)
        {
            if (_bakeState == state)
                return;

            _bakeState = state;
            RefreshBakePresentation();
        }

        private void RefreshBakePresentation()
        {
            CacheRuntimeComponents();

            bool interactionAllowed = _bakeState == VoxelBakeState.Complete;
            if (_meshRenderer != null)
            {
                Material targetMaterial = null;
                if (_engine != null && interactionAllowed)
                    targetMaterial = _engine.voxelMaterial;
                else if (_engine != null)
                    targetMaterial = _engine.ResolvedVoxelBakeGhostMaterial != null
                        ? _engine.ResolvedVoxelBakeGhostMaterial
                        : _engine.voxelMaterial;

                if (targetMaterial != null && _meshRenderer.sharedMaterial != targetMaterial)
                    _meshRenderer.sharedMaterial = targetMaterial;
            }

            if (_rootMeshCollider != null)
                _rootMeshCollider.enabled = interactionAllowed && _rootMeshCollider.sharedMesh != null;

            for (int i = 0; i < _colliderChunkColliders.Length; i++)
            {
                MeshCollider collider = _colliderChunkColliders[i];
                if (collider == null)
                    continue;

                collider.enabled = interactionAllowed && collider.sharedMesh != null && collider.gameObject.activeSelf;
            }
        }

        private void QueueRebuild()
        {
            _rebuildQueued = true;
            VoxelDynamicNavGridRuntime.QueueDirtyVolume(this);
            if (_bakeState == VoxelBakeState.Complete || _bakeState == VoxelBakeState.Idle)
                SetBakeState(VoxelBakeState.Pending);

            if (_rebuildRunning)
                return;

            _ = ProcessQueuedRebuildsAsync(_runtimeStamp);
        }

        private async Awaitable ProcessQueuedRebuildsAsync(int expectedRuntimeStamp)
        {
            if (_rebuildRunning)
                return;

            bool rescheduleNextFrame = false;
            _rebuildRunning = true;
            try
            {
                int rebuildWatchdog = MaxQueuedRebuildPassesPerKick;
                while (_rebuildQueued &&
                       MatchesRuntimeStamp(expectedRuntimeStamp) &&
                       rebuildWatchdog-- > 0)
                {
                    _rebuildQueued = false;
                    SetBakeState(VoxelBakeState.Baking);
                    HectonVoxelEngine engine = _engine != null ? _engine : HectonVoxelEngine.ActiveRuntimeInstance;
                    if (engine == null)
                        return;

                    if (!await engine.RebuildVolumeAsync(this, expectedRuntimeStamp))
                        return;

                    SetBakeState(_rebuildQueued ? VoxelBakeState.Pending : VoxelBakeState.Complete);
                }

                if (_rebuildQueued && MatchesRuntimeStamp(expectedRuntimeStamp))
                {
                    SetBakeState(VoxelBakeState.Pending);
                    await Awaitable.NextFrameAsync();
                    rescheduleNextFrame = true;
                }
            }
            catch (Exception ex)
            {
                SetBakeState(VoxelBakeState.Pending);
                Debug.LogError($"[HectonVoxelVolume] Crater rebuild failed on '{name}': {ex.Message}", this);
            }
            finally
            {
                _rebuildRunning = false;
            }

            if (rescheduleNextFrame && MatchesRuntimeStamp(_runtimeStamp))
                _ = ProcessQueuedRebuildsAsync(_runtimeStamp);
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
            _deltaProcessor?.UnregisterVolume(this);
            VoxelDynamicNavGridRuntime.UnregisterVolume(this);
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

        private static int ResolveStep(float axis)
        {
            if (axis > 0.0001f)
                return 1;

            return axis < -0.0001f ? -1 : 0;
        }

        private static float ResolveBoundaryDistance(float min, float start, float direction, int voxelIndex, int step, float voxelSize)
        {
            if (step == 0 || Mathf.Abs(direction) < 0.0001f)
                return float.PositiveInfinity;

            float nextBoundary = min + ((step > 0 ? voxelIndex + 1 : voxelIndex) * voxelSize);
            return (nextBoundary - start) / direction;
        }

        private static float ResolveDeltaDistance(float direction, float voxelSize)
        {
            if (Mathf.Abs(direction) < 0.0001f)
                return float.PositiveInfinity;

            return voxelSize / Mathf.Abs(direction);
        }

        private static int ResolveMarchAxis(float3 tMax, out float nextTravel)
        {
            if (tMax.x <= tMax.y && tMax.x <= tMax.z)
            {
                nextTravel = tMax.x;
                return 0;
            }

            if (tMax.y <= tMax.z)
            {
                nextTravel = tMax.y;
                return 1;
            }

            nextTravel = tMax.z;
            return 2;
        }

        private bool IsVoxelIndexInBounds(int3 voxel)
        {
            return voxel.x >= 0 && voxel.x < _gridDimension &&
                   voxel.y >= 0 && voxel.y < _gridDimension &&
                   voxel.z >= 0 && voxel.z < _gridDimension;
        }

        private bool AppendCraterStamp(Vector3 absolutePosition, float radius, bool queueRebuild)
        {
            if (!_runtimeDataReady || radius <= 0f)
                return false;

            float clampedRadius = Mathf.Max(_voxelSize * 1.25f, radius);
            float blendRadius = Mathf.Max(_voxelSize, clampedRadius * 0.35f);

            for (int i = 0; i < _craterStampCount; i++)
            {
                VoxelCraterStamp existing = _craterStamps[i];
                float mergeDistance = existing.radius + clampedRadius * 0.35f;
                if ((existing.position - absolutePosition).sqrMagnitude > mergeDistance * mergeDistance)
                    continue;

                existing.position = Vector3.Lerp(existing.position, absolutePosition, 0.5f);
                existing.radius = Mathf.Max(existing.radius, clampedRadius);
                existing.blendRadius = Mathf.Max(existing.blendRadius, blendRadius);
                _craterStamps[i] = existing;

                if (queueRebuild)
                    QueueRebuild();

                return true;
            }

            if (_craterStampCount >= MaxCraterStampCount)
            {
                for (int i = 1; i < _craterStampCount; i++)
                    _craterStamps[i - 1] = _craterStamps[i];

                _craterStampCount = MaxCraterStampCount - 1;
            }

            _craterStamps[_craterStampCount++] = new VoxelCraterStamp
            {
                position = absolutePosition,
                radius = clampedRadius,
                blendRadius = blendRadius
            };

            if (queueRebuild)
                QueueRebuild();

            return true;
        }
    }
}
