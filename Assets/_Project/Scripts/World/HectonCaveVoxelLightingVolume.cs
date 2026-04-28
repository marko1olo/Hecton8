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
        private LayerMask occluderLayers = ~0;

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
        private Vector3[] _scanLocalCenters;
        private Vector3[] _occupiedCenters;
        private Vector3[] _emptyCenters;
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

        private void TryRegister()
        {
            if (_registered)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
            _registered = true;
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
                _voxelDensityTexture != null &&
                _scanLocalCenters != null &&
                _scanLocalCenters.Length == voxelCount)
            {
                return;
            }

            ReleaseResources();

            _resolutionRuntime = clampedResolution;
            // COLD ALLOC: NativeArray<byte>[voxelCount] - player-centered cave occupancy staging volume - owner: HectonCaveVoxelLightingVolume
            _occupancyVolume = new NativeArray<byte>(voxelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: NativeArray<byte>[voxelCount] - player-centered encoded cave signed-distance volume - owner: HectonCaveVoxelLightingVolume
            _sdfVolume = new NativeArray<byte>(voxelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            // COLD ALLOC: Collider[8] - reusable overlap-box hit cache for cave lighting volume voxelization - owner: HectonCaveVoxelLightingVolume
            _overlapHits = new Collider[MaxOverlapHits];
            // COLD ALLOC: Vector3[voxelCount] - current voxel-center cache for local cave SDF encoding - owner: HectonCaveVoxelLightingVolume
            _scanLocalCenters = new Vector3[voxelCount];
            // COLD ALLOC: Vector3[voxelCount] - occupied voxel-center cache for local cave SDF encoding - owner: HectonCaveVoxelLightingVolume
            _occupiedCenters = new Vector3[voxelCount];
            // COLD ALLOC: Vector3[voxelCount] - empty voxel-center cache for local cave SDF encoding - owner: HectonCaveVoxelLightingVolume
            _emptyCenters = new Vector3[voxelCount];

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
                _occupancyVolume.Dispose();

            if (_sdfVolume.IsCreated)
                _sdfVolume.Dispose();

            if (_voxelDensityTexture != null)
                Destroy(_voxelDensityTexture);

            _overlapHits = null;
            _scanLocalCenters = null;
            _occupiedCenters = null;
            _emptyCenters = null;
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
            cellDiagonal = cellSize.magnitude;
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
                    _scanLocalCenters[voxelIndex] = localCenter;

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
            int voxelCount = _resolutionRuntime * _resolutionRuntime * _resolutionRuntime;
            int occupiedCount = 0;
            int emptyCount = 0;

            for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
            {
                if (_occupancyVolume[voxelIndex] > 0)
                {
                    _occupiedCenters[occupiedCount] = _scanLocalCenters[voxelIndex];
                    occupiedCount++;
                }
                else
                {
                    _emptyCenters[emptyCount] = _scanLocalCenters[voxelIndex];
                    emptyCount++;
                }
            }

            if (occupiedCount <= 0)
            {
                for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                    _sdfVolume[voxelIndex] = byte.MaxValue;
                return;
            }

            if (emptyCount <= 0)
            {
                for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                    _sdfVolume[voxelIndex] = byte.MinValue;
                return;
            }

            float boundaryBias = _scanCellDiagonal * 0.5f;
            float inverseSdfRange = _scanSdfRange > 0.0001f ? 1f / _scanSdfRange : 0f;

            for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
            {
                bool occupied = _occupancyVolume[voxelIndex] > 0;
                Vector3 origin = _scanLocalCenters[voxelIndex];
                Vector3[] searchSet = occupied ? _emptyCenters : _occupiedCenters;
                int searchCount = occupied ? emptyCount : occupiedCount;
                float nearestDistanceSq = float.MaxValue;

                for (int searchIndex = 0; searchIndex < searchCount; searchIndex++)
                {
                    float candidateDistanceSq = (origin - searchSet[searchIndex]).sqrMagnitude;
                    if (candidateDistanceSq < nearestDistanceSq)
                        nearestDistanceSq = candidateDistanceSq;
                }

                float unsignedDistance = nearestDistanceSq < float.MaxValue
                    ? Mathf.Max(0f, Mathf.Sqrt(nearestDistanceSq) - boundaryBias)
                    : _scanSdfRange;
                float signedDistance = occupied ? -unsignedDistance : unsignedDistance;
                float encoded = Mathf.Clamp01((signedDistance * inverseSdfRange) * 0.5f + 0.5f);
                _sdfVolume[voxelIndex] = (byte)Mathf.RoundToInt(encoded * 255f);
            }
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
    }
}
