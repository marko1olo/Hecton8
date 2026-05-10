using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Publishes a bounded player-centered cave SDF volume for ambient darkening and volumetric ray termination.
    /// This is a local lighting proxy, not an authoritative world-voxel streamer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonCaveVoxelLightingVolume : MonoBehaviour, ITickable, IUpdatable
    {
        private const int MaxOverlapHits = 8;
        private const string NativeMemoryOwner = nameof(HectonCaveVoxelLightingVolume);
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Session;
        internal static HectonCaveVoxelLightingVolume ActiveRuntimeInstance { get; private set; }

        private static readonly int _CaveVoxelActiveId = Shader.PropertyToID("_HectonCaveVoxelActive");
        private static readonly int _CaveVoxelWorldToLocalId = Shader.PropertyToID("_HectonCaveVoxelWorldToLocal");
        private static readonly int _CaveVoxelHalfExtentsId = Shader.PropertyToID("_HectonCaveVoxelHalfExtents");
        private static readonly int _CaveVoxelAoParamsId = Shader.PropertyToID("_HectonCaveVoxelAoParams");
        private static readonly int _CaveVoxelSdfTexId = Shader.PropertyToID("_HectonCaveVoxelSdfTex");

        [Header("── Runtime Volume ──────────────────")]
        [SerializeField]
        [Tooltip("Optional explicit follow target. When null, this GameObject transform is used.")]
        private Transform followTarget;

        [SerializeField, Range(12, 24)]
        [Tooltip("Local cave-SDF resolution. Kept low for MX350 CPU and VRAM safety.")]
        private int voxelResolution = 20;

        [SerializeField, Range(1, 8)]
        [Tooltip("Number of Z slices scanned per tick while rebuilding the local cave volume.")]
        private int slicesPerTick = 4;

        [SerializeField]
        [Tooltip("World layers treated as cave-solid occluders for the local SDF volume.")]
        private LayerMask occluderLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [SerializeField]
        [Tooltip("Physics trigger handling used while scanning cave-solid occupancy.")]
        private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [SerializeField]
        [Tooltip("Local half extents of the player-centered cave lighting volume.")]
        private Vector3 volumeHalfExtents = new Vector3(18f, 10f, 18f);

        [SerializeField, Range(0.5f, 1f)]
        [Tooltip("Fraction of each voxel cell used for the occupancy overlap-box query.")]
        private float occupancyPadding = 0.9f;

        [SerializeField, Range(2f, 6f)]
        [Tooltip("Signed-distance clamp expressed in cell diagonals before encoding to R8.")]
        private float sdfRangeInCellDiagonals = 4f;

        [Header("── Refresh ──────────────────")]
        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Rebuild threshold in meters for follow-target drift.")]
        private float positionRefreshThreshold = 1.25f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Optional world-space offset applied to the follow target before centering the volume.")]
        private float verticalCenterOffset = 0f;

        [Header("── Ambient Response ──────────────────")]
        [SerializeField, Range(0.02f, 1.5f)]
        [Tooltip("Signed-distance start radius where ambient darkening begins.")]
        private float aoFadeStartMeters = 0.15f;

        [SerializeField, Range(0.1f, 3f)]
        [Tooltip("Signed-distance end radius where cave ambient fully relaxes back to unoccluded.")]
        private float aoFadeEndMeters = 0.9f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("How aggressively cave proximity darkens ambient lighting.")]
        private float aoIntensity = 0.82f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Minimum surviving ambient factor when the sampled position is inside or hugging solid rock.")]
        private float aoFloor = 0.18f;

        [Header("── Diagnostics ──────────────────")]
        [SerializeField] private bool _debugHasValidVolume;
        [SerializeField] private int _debugSliceCursor;
        [SerializeField] private Vector3 _debugPublishedCenterWs;
        [SerializeField] private float _debugPublishedSdfRange;

        private bool _registered;
        private bool _scanInProgress;
        private bool _restartQueued;
        private bool _hasValidPublishedVolume;
        private int _resolutionRuntime;
        private int _scanSliceCursor;
        private Transform _followTargetRuntime;
        private Transform _excludedRoot;
        private Texture3D _voxelDensityTexture;
        private NativeArray<byte> _occupancyVolume;
        private NativeArray<byte> _sdfVolume;
        private Collider[] _overlapHits;
        private Matrix4x4 _scanLocalToWorld = Matrix4x4.identity;
        private Vector3 _scanCenterWs;
        private Vector3 _scanHalfExtents;
        private Vector3 _scanCellSize;
        private Vector3 _scanCellHalfExtents;
        private float _scanCellDiagonal;
        private float _scanSdfRange;
        private Vector3 _publishedCenterWs;
        private Matrix4x4 _publishedWorldToLocal = Matrix4x4.identity;
        private Vector3 _publishedHalfExtents;
        private float _publishedSdfRange;

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            ResolveFollowTarget();
            EnsureResources();
            PublishInactiveGlobals();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            ResolveFollowTarget();
            TryRegister();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregister();
            PublishInactiveGlobals();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            TryUnregister();
            PublishInactiveGlobals();
            ReleaseResources();
        }

        /// <summary>
        /// Incrementally rebuilds and publishes the local cave-lighting SDF volume.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta.</param>
        public void Tick(float deltaTime)
        {
            ResolveFollowTarget();
            EnsureResources();

            if (_followTargetRuntime == null)
            {
                PublishInactiveGlobals();
                return;
            }

            BuildDesiredVolumeDescriptor(out Vector3 desiredCenterWs, out Vector3 desiredHalfExtents, out Vector3 desiredCellSize, out float desiredCellDiagonal, out float desiredSdfRange);
            bool refreshRequired = RequiresRefresh(desiredCenterWs, desiredHalfExtents);
            if (!_scanInProgress && (!_hasValidPublishedVolume || refreshRequired || _restartQueued))
            {
                BeginScan(desiredCenterWs, desiredHalfExtents, desiredCellSize, desiredCellDiagonal, desiredSdfRange);
            }
            else if (_scanInProgress && refreshRequired)
            {
                _restartQueued = true;
            }

            int remainingSlices = Mathf.Max(1, slicesPerTick);
            while (_scanInProgress && remainingSlices > 0 && _scanSliceCursor < _resolutionRuntime)
            {
                ScanSlice(_scanSliceCursor);
                _scanSliceCursor++;
                remainingSlices--;
            }

            if (_scanInProgress && _scanSliceCursor >= _resolutionRuntime)
            {
                FinalizeScan();
                if (_restartQueued)
                {
                    BeginScan(desiredCenterWs, desiredHalfExtents, desiredCellSize, desiredCellDiagonal, desiredSdfRange);
                    _restartQueued = false;
                }
            }

            PublishGlobals(_hasValidPublishedVolume);
            _debugHasValidVolume = _hasValidPublishedVolume;
            _debugSliceCursor = _scanSliceCursor;
            _debugPublishedCenterWs = _publishedCenterWs;
            _debugPublishedSdfRange = _publishedSdfRange;
        }

        internal bool TryGetPublishedSignedDistanceVoxelPayload(
            out NativeArray<byte> signedDistanceVoxels,
            out Vector3Int gridDimensions,
            out Vector3 gridOrigin,
            out Vector3 voxelCellSize)
        {
            signedDistanceVoxels = _sdfVolume;
            int resolution = _resolutionRuntime;
            gridDimensions = new Vector3Int(resolution, resolution, resolution);
            gridOrigin = _publishedCenterWs - _publishedHalfExtents;
            voxelCellSize = resolution > 0
                ? new Vector3(
                    (_publishedHalfExtents.x * 2f) / resolution,
                    (_publishedHalfExtents.y * 2f) / resolution,
                    (_publishedHalfExtents.z * 2f) / resolution)
                : Vector3.one;
            return _hasValidPublishedVolume &&
                   signedDistanceVoxels.IsCreated &&
                   resolution > 0 &&
                   voxelCellSize.x > 0f &&
                   voxelCellSize.y > 0f &&
                   voxelCellSize.z > 0f;
        }

        internal bool TryGetPublishedGpuSdfPayload(
            out Texture3D sdfTexture,
            out Matrix4x4 worldToLocal,
            out Vector4 halfExtentsAndRange)
        {
            sdfTexture = _voxelDensityTexture;
            worldToLocal = _publishedWorldToLocal;
            halfExtentsAndRange = new Vector4(
                _publishedHalfExtents.x,
                _publishedHalfExtents.y,
                _publishedHalfExtents.z,
                _publishedSdfRange);
            return _hasValidPublishedVolume &&
                   sdfTexture != null &&
                   halfExtentsAndRange.x > 0f &&
                   halfExtentsAndRange.y > 0f &&
                   halfExtentsAndRange.z > 0f &&
                   halfExtentsAndRange.w > 0f;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private void ResolveFollowTarget()
        {
            _followTargetRuntime = followTarget != null ? followTarget : transform;
            _excludedRoot = _followTargetRuntime != null ? _followTargetRuntime.root : null;
        }

        private void EnsureResources()
        {
            int clampedResolution = Mathf.Clamp(voxelResolution, 12, 24);
            int voxelCount = clampedResolution * clampedResolution * clampedResolution;
            if (_resolutionRuntime == clampedResolution &&
                _occupancyVolume.IsCreated &&
                _occupancyVolume.Length == voxelCount &&
                _sdfVolume.IsCreated &&
                _sdfVolume.Length == voxelCount &&
                _voxelDensityTexture != null)
            {
                return;
            }

            ReleaseResources();

            _resolutionRuntime = clampedResolution;
            // COLD ALLOC: NativeArray<byte>[voxelCount] - player-centered cave occupancy staging volume - owner: HectonCaveVoxelLightingVolume
            _occupancyVolume = new NativeArray<byte>(voxelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[voxelCount] - player-centered encoded cave signed-distance volume - owner: HectonCaveVoxelLightingVolume
            _sdfVolume = new NativeArray<byte>(voxelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(_occupancyVolume, NativeMemoryOwner, nameof(_occupancyVolume), NativeMemoryLifetime);
            NativeMemorySentinel.RegisterNativeArray(_sdfVolume, NativeMemoryOwner, nameof(_sdfVolume), NativeMemoryLifetime);
            // COLD ALLOC: Collider[8] - reusable overlap-box hit cache for cave lighting volume voxelization - owner: HectonCaveVoxelLightingVolume
            _overlapHits = new Collider[MaxOverlapHits];
            TextureFormat textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.R8)
                ? TextureFormat.R8
                : TextureFormat.Alpha8;
            _voxelDensityTexture = new Texture3D(clampedResolution, clampedResolution, clampedResolution, textureFormat, false)
            {
                name = "__HectonCaveVoxelSdfTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            }; // COLD ALLOC: Texture3D[1] - player-centered cave lighting SDF volume - owner: HectonCaveVoxelLightingVolume

            _scanSliceCursor = 0;
            _restartQueued = false;
            _hasValidPublishedVolume = false;
            _scanInProgress = false;
            Shader.SetGlobalTexture(_CaveVoxelSdfTexId, _voxelDensityTexture);
        }

        private void ReleaseResources()
        {
            if (_occupancyVolume.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_occupancyVolume);
                _occupancyVolume.Dispose();
            }

            if (_sdfVolume.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_sdfVolume);
                _sdfVolume.Dispose();
            }

            if (_voxelDensityTexture != null)
                Destroy(_voxelDensityTexture);

            _overlapHits = null;
            _voxelDensityTexture = null;
            _resolutionRuntime = 0;
            _scanSliceCursor = 0;
            _hasValidPublishedVolume = false;
            _scanInProgress = false;
            _restartQueued = false;
        }

        private void BuildDesiredVolumeDescriptor(
            out Vector3 centerWs,
            out Vector3 halfExtents,
            out Vector3 cellSize,
            out float cellDiagonal,
            out float sdfRange)
        {
            Vector3 followPosition = _followTargetRuntime != null ? _followTargetRuntime.position : transform.position;
            followPosition.y += verticalCenterOffset;
            centerWs = followPosition;
            halfExtents = new Vector3(
                Mathf.Max(1f, volumeHalfExtents.x),
                Mathf.Max(1f, volumeHalfExtents.y),
                Mathf.Max(1f, volumeHalfExtents.z));
            cellSize = new Vector3(
                (halfExtents.x * 2f) / Mathf.Max(1, _resolutionRuntime),
                (halfExtents.y * 2f) / Mathf.Max(1, _resolutionRuntime),
                (halfExtents.z * 2f) / Mathf.Max(1, _resolutionRuntime));
            cellDiagonal = EstimateLength3D(cellSize);
            sdfRange = Mathf.Max(cellDiagonal * Mathf.Max(1f, sdfRangeInCellDiagonals), cellDiagonal);
        }

        private bool RequiresRefresh(Vector3 desiredCenterWs, Vector3 desiredHalfExtents)
        {
            if (!_hasValidPublishedVolume)
                return true;

            if ((_publishedCenterWs - desiredCenterWs).sqrMagnitude > positionRefreshThreshold * positionRefreshThreshold)
                return true;

            return (_publishedHalfExtents - desiredHalfExtents).sqrMagnitude > 0.01f;
        }

        private void BeginScan(
            Vector3 centerWs,
            Vector3 halfExtents,
            Vector3 cellSize,
            float cellDiagonal,
            float sdfRange)
        {
            _scanCenterWs = centerWs;
            _scanHalfExtents = halfExtents;
            _scanCellSize = cellSize;
            _scanCellHalfExtents = cellSize * (0.5f * Mathf.Clamp(occupancyPadding, 0.5f, 1f));
            _scanCellDiagonal = cellDiagonal;
            _scanSdfRange = sdfRange;
            _scanLocalToWorld = Matrix4x4.TRS(centerWs, Quaternion.identity, Vector3.one);
            _scanSliceCursor = 0;
            _scanInProgress = true;
        }

        private void ScanSlice(int zIndex)
        {
            int resolution = _resolutionRuntime;
            int sliceOffset = zIndex * resolution * resolution;
            float localZ = -_scanHalfExtents.z + (zIndex + 0.5f) * _scanCellSize.z;

            for (int yIndex = 0; yIndex < resolution; yIndex++)
            {
                float localY = -_scanHalfExtents.y + (yIndex + 0.5f) * _scanCellSize.y;

                for (int xIndex = 0; xIndex < resolution; xIndex++)
                {
                    float localX = -_scanHalfExtents.x + (xIndex + 0.5f) * _scanCellSize.x;
                    int voxelIndex = sliceOffset + (yIndex * resolution) + xIndex;
                    Vector3 localCenter = new Vector3(localX, localY, localZ);

                    Vector3 worldCenter = _scanLocalToWorld.MultiplyPoint3x4(localCenter);
                    _occupancyVolume[voxelIndex] = IsCellOccupied(worldCenter) ? byte.MaxValue : byte.MinValue;
                }
            }
        }

        private bool IsCellOccupied(Vector3 worldCenter)
        {
            int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                worldCenter,
                _scanCellHalfExtents,
                _overlapHits,
                Quaternion.identity,
                occluderLayers,
                triggerInteraction);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = _overlapHits[hitIndex];
                if (hit == null || !hit.enabled)
                    continue;

                Transform hitRoot = hit.transform.root;
                if (_excludedRoot != null && hitRoot == _excludedRoot)
                    continue;

                return true;
            }

            return false;
        }

        private void FinalizeScan()
        {
            EncodeSignedDistanceField();
            _voxelDensityTexture.SetPixelData(_sdfVolume, 0);
            _voxelDensityTexture.Apply(false, false);

            _publishedCenterWs = _scanCenterWs;
            _publishedHalfExtents = _scanHalfExtents;
            _publishedSdfRange = _scanSdfRange;
            _publishedWorldToLocal = _scanLocalToWorld.inverse;
            _hasValidPublishedVolume = true;
            _restartQueued = false;
            _scanInProgress = false;
            _scanSliceCursor = 0;
            Shader.SetGlobalTexture(_CaveVoxelSdfTexId, _voxelDensityTexture);
        }

        private void EncodeSignedDistanceField()
        {
            int resolution = _resolutionRuntime;
            int voxelCount = resolution * resolution * resolution;
            if (voxelCount <= 0)
                return;

            bool foundOccupied = false;
            bool foundEmpty = false;
            for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
            {
                if (_occupancyVolume[voxelIndex] > 0)
                    foundOccupied = true;
                else
                    foundEmpty = true;
            }

            if (!foundOccupied || !foundEmpty)
            {
                byte fill = foundOccupied ? byte.MinValue : byte.MaxValue;
                for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                    _sdfVolume[voxelIndex] = fill;
                return;
            }

            for (int zIndex = 0; zIndex < resolution; zIndex++)
            {
                int sliceOffset = zIndex * resolution * resolution;
                for (int yIndex = 0; yIndex < resolution; yIndex++)
                {
                    int rowOffset = sliceOffset + yIndex * resolution;
                    for (int xIndex = 0; xIndex < resolution; xIndex++)
                    {
                        int voxelIndex = rowOffset + xIndex;
                        bool occupied = _occupancyVolume[voxelIndex] > 0;
                        bool directShell = HasOppositeNeighbor(xIndex, yIndex, zIndex, occupied, 1);
                        if (occupied)
                        {
                            _sdfVolume[voxelIndex] = directShell ? (byte)115 : byte.MinValue;
                            continue;
                        }

                        bool wideShell = !directShell && HasOppositeNeighbor(xIndex, yIndex, zIndex, occupied, 2);
                        _sdfVolume[voxelIndex] = directShell
                            ? (byte)140
                            : wideShell
                                ? (byte)166
                                : byte.MaxValue;
                    }
                }
            }
        }

        private bool HasOppositeNeighbor(int x, int y, int z, bool occupied, int radius)
        {
            return IsOccupiedAt(x + radius, y, z) != occupied ||
                   IsOccupiedAt(x - radius, y, z) != occupied ||
                   IsOccupiedAt(x, y + radius, z) != occupied ||
                   IsOccupiedAt(x, y - radius, z) != occupied ||
                   IsOccupiedAt(x, y, z + radius) != occupied ||
                   IsOccupiedAt(x, y, z - radius) != occupied;
        }

        private bool IsOccupiedAt(int x, int y, int z)
        {
            int resolution = _resolutionRuntime;
            if (x < 0 || y < 0 || z < 0 || x >= resolution || y >= resolution || z >= resolution)
                return false;

            int index = x + y * resolution + z * resolution * resolution;
            return _occupancyVolume[index] > 0;
        }

        private void PublishGlobals(bool hasVolume)
        {
            Shader.SetGlobalFloat(_CaveVoxelActiveId, hasVolume ? 1f : 0f);
            Shader.SetGlobalVector(
                _CaveVoxelAoParamsId,
                new Vector4(
                    Mathf.Max(0.001f, aoFadeStartMeters),
                    Mathf.Max(aoFadeStartMeters + 0.001f, aoFadeEndMeters),
                    Mathf.Clamp01(aoIntensity),
                    Mathf.Clamp01(aoFloor)));

            if (!hasVolume)
            {
                Shader.SetGlobalVector(_CaveVoxelHalfExtentsId, Vector4.zero);
                Shader.SetGlobalMatrix(_CaveVoxelWorldToLocalId, Matrix4x4.identity);
                return;
            }

            Shader.SetGlobalMatrix(_CaveVoxelWorldToLocalId, _publishedWorldToLocal);
            Shader.SetGlobalVector(
                _CaveVoxelHalfExtentsId,
                new Vector4(
                    _publishedHalfExtents.x,
                    _publishedHalfExtents.y,
                    _publishedHalfExtents.z,
                    _publishedSdfRange));
            Shader.SetGlobalTexture(_CaveVoxelSdfTexId, _voxelDensityTexture);
        }

        private static void PublishInactiveGlobals()
        {
            Shader.SetGlobalFloat(_CaveVoxelActiveId, 0f);
            Shader.SetGlobalVector(_CaveVoxelHalfExtentsId, Vector4.zero);
            Shader.SetGlobalMatrix(_CaveVoxelWorldToLocalId, Matrix4x4.identity);
        }

        private static float EstimateLength3D(Vector3 value)
        {
            float ax = Mathf.Abs(value.x);
            float ay = Mathf.Abs(value.y);
            float az = Mathf.Abs(value.z);
            float maxAxis = Mathf.Max(ax, Mathf.Max(ay, az));
            float minAxis = Mathf.Min(ax, Mathf.Min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.125f);
        }
    }
}
