using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Publishes a bounded player-centered cave SDF volume for ambient darkening and volumetric ray termination.
    /// This is a local lighting proxy, not an authoritative world-voxel streamer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonCaveVoxelLightingVolume : MonoBehaviour, ILateFrameTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001HectonCaveVoxelLightingVolumeSignalPushDropCount;
        private const int MaxOverlapHits = 8;
        private const int LightLevelSignalFrameStride = 6;
        private const SystemID VaultOwnerSystemId = SystemID.WorldStreaming;
        private const BufferID OccupancyVolumeBufferId = BufferID.CaveVoxelLightingOccupancyVolume;
        private const BufferID SdfVolumeBufferId = BufferID.CaveVoxelLightingSdfVolume;
        private const float InvByteMax = 1f / 255f;
        internal static HectonCaveVoxelLightingVolume ActiveRuntimeInstance { get; private set; }

        private static readonly int _CaveVoxelActiveId = Shader.PropertyToID("_HectonCaveVoxelActive");
        private static readonly int _CaveVoxelWorldToLocalId = Shader.PropertyToID("_HectonCaveVoxelWorldToLocal");
        private static readonly int _CaveVoxelHalfExtentsId = Shader.PropertyToID("_HectonCaveVoxelHalfExtents");
        private static readonly int _CaveVoxelInvDoubleHalfExtentsId = Shader.PropertyToID("_HectonCaveVoxelInvDoubleHalfExtents");
        private static readonly int _CaveVoxelAoParamsId = Shader.PropertyToID("_HectonCaveVoxelAoParams");
        private static readonly int _CaveVoxelSdfTexId = Shader.PropertyToID("_HectonCaveVoxelSdfTex");

        [Header("Runtime Volume")]
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

        [Header("Refresh")]
        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Rebuild threshold in meters for follow-target drift.")]
        private float positionRefreshThreshold = 1.25f;

        [SerializeField, Range(0f, 6f)]
        [Tooltip("Optional world-space offset applied to the follow target before centering the volume.")]
        private float verticalCenterOffset = 0f;

        [Header("Ambient Response")]
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

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasValidVolume;
        [SerializeField] private int _debugSliceCursor;
        [SerializeField] private Vector3 _debugPublishedCenterWs;
        [SerializeField] private float _debugPublishedSdfRange;

        private bool _registeredLateFrameTick;
        private bool _registeredSlowTick;
        private bool _hotSwapListenerRegistered;
        private bool _scanInProgress;
        private bool _restartQueued;
        private bool _hasValidPublishedVolume;
        private bool _globalsDirty;
        private bool _pendingHasVolume;
        private bool _textureUploadDirty;
        private bool _textureBindingDirty;
        private bool _resourceRefreshRequested;
        private TextureFormat _coldVoxelDensityTextureFormat = TextureFormat.Alpha8;
        private int _resolutionRuntime;
        private int _scanSliceCursor;
        private int _lastLightLevelSignalFrame = -1;
        private uint _sourceEntityId;
        private Transform _followTargetRuntime;
        private Transform _excludedRoot;
        private Texture3D _voxelDensityTexture;
        private VaultGenerationHandle<byte> _occupancyVolumeHandle;
        private VaultGenerationHandle<byte> _sdfVolumeHandle;
        private IDataVault _dataVault;
        private SpatialQueryHit[] _overlapHits;
        private Matrix4x4 _scanLocalToWorld = Matrix4x4.identity;
        private int _voxelVolumeCapacity;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticRuntimeState()
        {
            ActiveRuntimeInstance = null;
            PredatorCognitionDomain.ClearCaveVoxelLightingSource(null);
        }

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            CacheGraphicsCapabilitiesCold();
            PredatorCognitionDomain.BindCaveVoxelLightingSource(this);
            _sourceEntityId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            ResolveFollowTarget();
            EnsureResources();
            PublishInactiveGlobals();
        }

        private void OnEnable()
        {
            ActiveRuntimeInstance = this;
            CacheGraphicsCapabilitiesCold();
            PredatorCognitionDomain.BindCaveVoxelLightingSource(this);
            ResolveFollowTarget();
            _resourceRefreshRequested = !HasRequiredResources();
            TryRegisterHotSwapListener();
            TryRegister();
        }

        private void OnDisable()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            PredatorCognitionDomain.ClearCaveVoxelLightingSource(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            PublishInactiveGlobals();
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            PredatorCognitionDomain.ClearCaveVoxelLightingSource(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            PublishInactiveGlobals();
            ReleaseResources();
        }

        /// <summary>
        /// Incrementally rebuilds and publishes the local cave-lighting SDF volume.
        /// </summary>
        /// <param name="deltaTime">Dispatcher delta.</param>
        private void AdvanceLightingVolumeState()
        {
            ResolveFollowTarget();
            if (!HasRequiredResources())
            {
                _resourceRefreshRequested = true;
                QueueGlobals(hasVolume: false);
                PublishPlayerLightLevelSignal();
                return;
            }

            if (_followTargetRuntime == null)
            {
                QueueGlobals(hasVolume: false);
                PublishPlayerLightLevelSignal();
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

            QueueGlobals(_hasValidPublishedVolume);
            PublishPlayerLightLevelSignal();
            _debugHasValidVolume = _hasValidPublishedVolume;
            _debugSliceCursor = _scanSliceCursor;
            _debugPublishedCenterWs = _publishedCenterWs;
            _debugPublishedSdfRange = _publishedSdfRange;
        }

        /// <summary>
        /// Uploads the generated cave SDF texture and shader globals after the scan phase has finished.
        /// </summary>
        public void LateFrameTick()
        {
            AdvanceLightingVolumeState();

            if (_textureUploadDirty)
            {
                if (_voxelDensityTexture != null && TryAcquireSdfUploadBuffer(out NativeArray<byte> sdfVolume))
                {
                    try
                    {
                        _voxelDensityTexture.SetPixelData(sdfVolume, 0);
                        _voxelDensityTexture.Apply(false, false);
                        _textureBindingDirty = true;
                    }
                    finally
                    {
                        ReleaseSdfWriteBuffer();
                    }
                }

                _textureUploadDirty = false;
            }

            if (!_globalsDirty && !_textureBindingDirty)
                return;

            FlushGlobals(_pendingHasVolume);
            _globalsDirty = false;
            _textureBindingDirty = false;
        }

        public void SlowTick()
        {
            if (!_resourceRefreshRequested && HasRequiredResources())
                return;

            EnsureResources();
            _resourceRefreshRequested = false;
        }

        internal bool TryGetPublishedSignedDistanceVoxelPayload(
            out NativeArray<byte>.ReadOnly signedDistanceVoxels,
            out Vector3Int gridDimensions,
            out Vector3 gridOrigin,
            out Vector3 voxelCellSize)
        {
            TryReadSdfVolume(out signedDistanceVoxels);
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
            out Vector4 halfExtentsAndRange,
            out Vector4 invDoubleHalfExtents)
        {
            sdfTexture = _voxelDensityTexture;
            worldToLocal = _publishedWorldToLocal;
            halfExtentsAndRange = new Vector4(
                _publishedHalfExtents.x,
                _publishedHalfExtents.y,
                _publishedHalfExtents.z,
                _publishedSdfRange);
            invDoubleHalfExtents = ResolveInvDoubleHalfExtents(_publishedHalfExtents);
            return _hasValidPublishedVolume &&
                   sdfTexture != null &&
                   halfExtentsAndRange.x > 0f &&
                   halfExtentsAndRange.y > 0f &&
                   halfExtentsAndRange.z > 0f &&
                   halfExtentsAndRange.w > 0f;
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredLateFrameTick)
            {
                _registeredLateFrameTick = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredSlowTick)
            {
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }
        }

        private void TryUnregister()
        {
            if (_registeredLateFrameTick)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrameTick = false;
            }

            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault && !ReferenceEquals(previousService, currentService))
            {
                ReleaseResources(previousService as IDataVault);
                _dataVault = currentService as IDataVault;
                _resourceRefreshRequested = isActiveAndEnabled;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null && isActiveAndEnabled)
            {
                _registeredLateFrameTick = false;
                _registeredSlowTick = false;
                TryRegister();
            }
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
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
            if (HasRequiredResources(clampedResolution, voxelCount))
            {
                return;
            }

            IDataVault vault = CacheDataVaultCold();
            if (vault == null)
            {
                ReleaseResources();
                return;
            }

            ReleaseResources(vault);

            _resolutionRuntime = clampedResolution;
            _voxelVolumeCapacity = voxelCount;
            _occupancyVolumeHandle = vault.EnsureGenerationHandle<byte>(
                OccupancyVolumeBufferId,
                voxelCount,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
            _sdfVolumeHandle = vault.EnsureGenerationHandle<byte>(
                SdfVolumeBufferId,
                voxelCount,
                VaultOwnerSystemId,
                NativeArrayOptions.ClearMemory);
            if (!HasVaultVolumeCapacity(voxelCount))
            {
                ReleaseResources(vault);
                return;
            }

            // COLD ALLOC: Collider[8] - reusable overlap-box hit cache for cave lighting volume voxelization - owner: HectonCaveVoxelLightingVolume
            _overlapHits = new SpatialQueryHit[MaxOverlapHits];
            _voxelDensityTexture = new Texture3D(clampedResolution, clampedResolution, clampedResolution, _coldVoxelDensityTextureFormat, false)
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
            _textureBindingDirty = true;
            QueueGlobals(hasVolume: false);
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldVoxelDensityTextureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.R8)
                ? TextureFormat.R8
                : TextureFormat.Alpha8;
        }

        private bool HasRequiredResources()
        {
            int clampedResolution = Mathf.Clamp(voxelResolution, 12, 24);
            int voxelCount = clampedResolution * clampedResolution * clampedResolution;
            return HasRequiredResources(clampedResolution, voxelCount);
        }

        private bool HasRequiredResources(int clampedResolution, int voxelCount)
        {
            return _resolutionRuntime == clampedResolution &&
                   HasVaultVolumeCapacity(voxelCount) &&
                   _voxelDensityTexture != null;
        }

        private bool HasVaultVolumeCapacity(int voxelCount)
        {
            return _voxelVolumeCapacity == voxelCount &&
                   TryReadOccupancyVolume(out NativeArray<byte>.ReadOnly occupancyVolume) &&
                   occupancyVolume.Length == voxelCount &&
                   TryReadSdfVolume(out NativeArray<byte>.ReadOnly sdfVolume) &&
                   sdfVolume.Length == voxelCount;
        }

        private void ReleaseResources()
        {
            ReleaseResources(_dataVault);
        }

        private void ReleaseResources(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _occupancyVolumeHandle);
            ReleaseVaultHandle(vault, ref _sdfVolumeHandle);
            if (_voxelDensityTexture != null)
                Destroy(_voxelDensityTexture);

            _overlapHits = null;
            _voxelDensityTexture = null;
            _resolutionRuntime = 0;
            _voxelVolumeCapacity = 0;
            _scanSliceCursor = 0;
            _hasValidPublishedVolume = false;
            _globalsDirty = false;
            _pendingHasVolume = false;
            _textureUploadDirty = false;
            _textureBindingDirty = false;
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
            if (!TryAcquireOccupancyWriteBuffer(out NativeArray<byte> occupancyVolume))
                return;

            int sliceOffset = zIndex * resolution * resolution;
            float localZ = -_scanHalfExtents.z + (zIndex + 0.5f) * _scanCellSize.z;

            try
            {
                for (int yIndex = 0; yIndex < resolution; yIndex++)
                {
                    float localY = -_scanHalfExtents.y + (yIndex + 0.5f) * _scanCellSize.y;

                    for (int xIndex = 0; xIndex < resolution; xIndex++)
                    {
                        float localX = -_scanHalfExtents.x + (xIndex + 0.5f) * _scanCellSize.x;
                        int voxelIndex = sliceOffset + (yIndex * resolution) + xIndex;
                        Vector3 localCenter = new Vector3(localX, localY, localZ);

                        Vector3 worldCenter = _scanLocalToWorld.MultiplyPoint3x4(localCenter);
                        occupancyVolume[voxelIndex] = IsCellOccupied(worldCenter) ? byte.MaxValue : byte.MinValue;
                    }
                }
            }
            finally
            {
                ReleaseOccupancyWriteBuffer();
            }
        }

        private bool IsCellOccupied(Vector3 worldCenter)
        {
            const SpatialTargetKind kindMask =
                SpatialTargetKind.Resource |
                SpatialTargetKind.Pickup |
                SpatialTargetKind.Scannable |
                SpatialTargetKind.Module;

            float queryRadius = Mathf.Max(0.01f, _scanCellHalfExtents.magnitude);
            int hitCount = WorldSpatialHashGrid.CollectContactsNonAlloc(
                worldCenter,
                queryRadius,
                kindMask,
                _overlapHits);

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                SpatialQueryHit hit = _overlapHits[hitIndex];
                _overlapHits[hitIndex] = default;
                if (!LayerMatchesMask(hit.Layer, occluderLayers))
                    continue;

                Vector3 delta = hit.Position - worldCenter;
                if (Mathf.Abs(delta.x) > _scanCellHalfExtents.x ||
                    Mathf.Abs(delta.y) > _scanCellHalfExtents.y ||
                    Mathf.Abs(delta.z) > _scanCellHalfExtents.z)
                {
                    continue;
                }

                Transform hitRoot = hit.Transform != null ? hit.Transform.root : null;
                if (_excludedRoot != null && hitRoot == _excludedRoot)
                    continue;

                return true;
            }

            return false;
        }

        private static bool LayerMatchesMask(int layer, LayerMask mask)
        {
            return layer >= 0 && layer < 32 && (mask.value & (1 << layer)) != 0;
        }

        private void FinalizeScan()
        {
            EncodeSignedDistanceField();

            _publishedCenterWs = _scanCenterWs;
            _publishedHalfExtents = _scanHalfExtents;
            _publishedSdfRange = _scanSdfRange;
            _publishedWorldToLocal = _scanLocalToWorld.inverse;
            _hasValidPublishedVolume = true;
            _textureUploadDirty = true;
            _restartQueued = false;
            _scanInProgress = false;
            _scanSliceCursor = 0;
            QueueGlobals(hasVolume: true);
        }

        private void EncodeSignedDistanceField()
        {
            if (!TryReadOccupancyVolume(out NativeArray<byte>.ReadOnly occupancyVolume) ||
                !TryAcquireSdfWriteBuffer(out NativeArray<byte> sdfVolume))
            {
                return;
            }

            try
            {
                EncodeSignedDistanceField(occupancyVolume, sdfVolume);
            }
            finally
            {
                ReleaseSdfWriteBuffer();
            }
        }

        private void EncodeSignedDistanceField(
            NativeArray<byte>.ReadOnly occupancyVolume,
            NativeArray<byte> sdfVolume)
        {
            int resolution = _resolutionRuntime;
            int voxelCount = resolution * resolution * resolution;
            if (voxelCount <= 0)
                return;

            bool foundOccupied = false;
            bool foundEmpty = false;
            for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
            {
                if (occupancyVolume[voxelIndex] > 0)
                    foundOccupied = true;
                else
                    foundEmpty = true;
            }

            if (!foundOccupied || !foundEmpty)
            {
                byte fill = foundOccupied ? byte.MinValue : byte.MaxValue;
                for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                    sdfVolume[voxelIndex] = fill;
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
                        bool occupied = occupancyVolume[voxelIndex] > 0;
                        bool directShell = HasOppositeNeighbor(occupancyVolume, xIndex, yIndex, zIndex, occupied, 1);
                        if (occupied)
                        {
                            sdfVolume[voxelIndex] = directShell ? (byte)115 : byte.MinValue;
                            continue;
                        }

                        bool wideShell = !directShell && HasOppositeNeighbor(occupancyVolume, xIndex, yIndex, zIndex, occupied, 2);
                        sdfVolume[voxelIndex] = directShell
                            ? (byte)140
                            : wideShell
                                ? (byte)166
                                : byte.MaxValue;
                    }
                }
            }
        }

        private bool HasOppositeNeighbor(
            NativeArray<byte>.ReadOnly occupancyVolume,
            int x,
            int y,
            int z,
            bool occupied,
            int radius)
        {
            return IsOccupiedAt(occupancyVolume, x + radius, y, z) != occupied ||
                   IsOccupiedAt(occupancyVolume, x - radius, y, z) != occupied ||
                   IsOccupiedAt(occupancyVolume, x, y + radius, z) != occupied ||
                   IsOccupiedAt(occupancyVolume, x, y - radius, z) != occupied ||
                   IsOccupiedAt(occupancyVolume, x, y, z + radius) != occupied ||
                   IsOccupiedAt(occupancyVolume, x, y, z - radius) != occupied;
        }

        private bool IsOccupiedAt(NativeArray<byte>.ReadOnly occupancyVolume, int x, int y, int z)
        {
            int resolution = _resolutionRuntime;
            if (x < 0 || y < 0 || z < 0 || x >= resolution || y >= resolution || z >= resolution)
                return false;

            int index = x + y * resolution + z * resolution * resolution;
            return occupancyVolume[index] > 0;
        }

        private void QueueGlobals(bool hasVolume)
        {
            _pendingHasVolume = hasVolume;
            _globalsDirty = true;
        }

        private void FlushGlobals(bool hasVolume)
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
                Shader.SetGlobalVector(_CaveVoxelInvDoubleHalfExtentsId, Vector4.zero);
                Shader.SetGlobalMatrix(_CaveVoxelWorldToLocalId, Matrix4x4.identity);
                return;
            }

            Shader.SetGlobalMatrix(_CaveVoxelWorldToLocalId, _publishedWorldToLocal);
            Shader.SetGlobalVector(_CaveVoxelInvDoubleHalfExtentsId, ResolveInvDoubleHalfExtents(_publishedHalfExtents));
            Shader.SetGlobalVector(
                _CaveVoxelHalfExtentsId,
                new Vector4(
                    _publishedHalfExtents.x,
                    _publishedHalfExtents.y,
                    _publishedHalfExtents.z,
                    _publishedSdfRange));
            if (_voxelDensityTexture != null)
                Shader.SetGlobalTexture(_CaveVoxelSdfTexId, _voxelDensityTexture);
        }

        private void PublishPlayerLightLevelSignal()
        {
            if (!Application.isPlaying)
                return;

            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastLightLevelSignalFrame >= 0 && frame - _lastLightLevelSignalFrame < LightLevelSignalFrameStride)
                return;

            _lastLightLevelSignalFrame = frame;
            float lightLevel01 = ResolvePlayerLightLevel01();
            LightLevelSignal signal = new LightLevelSignal
            {
                LightLevel01 = lightLevel01,
                Darkness01 = 1f - lightLevel01,
                SourceId = _sourceEntityId,
                Frame = unchecked((uint)frame),
                SampleKind = LightLevelSignalSampleKinds.CaveVoxelSdf,
                Flags = _hasValidPublishedVolume ? LightLevelSignalFlags.ValidSample : (byte)0
            };
            SignalBus<LightLevelSignal>.TryPushTracked(in signal, ref s_x001HectonCaveVoxelLightingVolumeSignalPushDropCount);
        }

        private float ResolvePlayerLightLevel01()
        {
            if (!_hasValidPublishedVolume ||
                !TryReadSdfVolume(out NativeArray<byte>.ReadOnly sdfVolume) ||
                _followTargetRuntime == null ||
                _resolutionRuntime <= 0)
            {
                return 1f;
            }

            Vector3 halfExtents = _publishedHalfExtents;
            if (halfExtents.x <= 0f || halfExtents.y <= 0f || halfExtents.z <= 0f)
                return 1f;

            Vector3 local = _publishedWorldToLocal.MultiplyPoint3x4(_followTargetRuntime.position);
            Vector4 invDoubleHalfExtents = ResolveInvDoubleHalfExtents(halfExtents);
            float normalizedX = (local.x + halfExtents.x) * invDoubleHalfExtents.x;
            float normalizedY = (local.y + halfExtents.y) * invDoubleHalfExtents.y;
            float normalizedZ = (local.z + halfExtents.z) * invDoubleHalfExtents.z;
            if (normalizedX < 0f || normalizedX > 1f ||
                normalizedY < 0f || normalizedY > 1f ||
                normalizedZ < 0f || normalizedZ > 1f)
            {
                return 1f;
            }

            int resolution = _resolutionRuntime;
            int xIndex = Mathf.Clamp((int)(normalizedX * resolution), 0, resolution - 1);
            int yIndex = Mathf.Clamp((int)(normalizedY * resolution), 0, resolution - 1);
            int zIndex = Mathf.Clamp((int)(normalizedZ * resolution), 0, resolution - 1);
            int voxelIndex = (zIndex * resolution * resolution) + (yIndex * resolution) + xIndex;
            if (voxelIndex < 0 || voxelIndex >= sdfVolume.Length)
                return 1f;

            float sdf01 = Mathf.Clamp01(sdfVolume[voxelIndex] * InvByteMax);
            float occlusion01 = 1f - sdf01;
            float darken01 = Mathf.Clamp01(occlusion01 * aoIntensity);
            return Mathf.Clamp01(Mathf.Max(aoFloor, 1f - darken01));
        }

        private IDataVault CacheDataVaultCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            return _dataVault;
        }

        private bool TryReadOccupancyVolume(out NativeArray<byte>.ReadOnly occupancyVolume)
        {
            return TryReadVaultVolume(in _occupancyVolumeHandle, OccupancyVolumeBufferId, out occupancyVolume);
        }

        private bool TryReadSdfVolume(out NativeArray<byte>.ReadOnly sdfVolume)
        {
            return TryReadVaultVolume(in _sdfVolumeHandle, SdfVolumeBufferId, out sdfVolume);
        }

        private bool TryReadVaultVolume(
            in VaultGenerationHandle<byte> handle,
            BufferID expectedBufferId,
            out NativeArray<byte>.ReadOnly volume)
        {
            volume = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   IsExactVaultHandle(in handle, expectedBufferId) &&
                   vault.TryReadOnlyHandle(in handle, out volume) &&
                   volume.IsCreated &&
                   volume.Length >= _voxelVolumeCapacity;
        }

        private bool TryAcquireOccupancyWriteBuffer(out NativeArray<byte> occupancyVolume)
        {
            return TryAcquireVolumeWriteBuffer(in _occupancyVolumeHandle, OccupancyVolumeBufferId, out occupancyVolume);
        }

        private bool TryAcquireSdfWriteBuffer(out NativeArray<byte> sdfVolume)
        {
            return TryAcquireVolumeWriteBuffer(in _sdfVolumeHandle, SdfVolumeBufferId, out sdfVolume);
        }

        private bool TryAcquireSdfUploadBuffer(out NativeArray<byte> sdfVolume)
        {
            return TryAcquireSdfWriteBuffer(out sdfVolume);
        }

        private bool TryAcquireVolumeWriteBuffer(
            in VaultGenerationHandle<byte> handle,
            BufferID expectedBufferId,
            out NativeArray<byte> volume)
        {
            volume = default;
            IDataVault vault = _dataVault;
            if (vault == null ||
                !IsExactVaultHandle(in handle, expectedBufferId) ||
                !vault.TryAcquireWriteLock(in handle, VaultOwnerSystemId, out volume))
            {
                return false;
            }

            if (volume.IsCreated && volume.Length >= _voxelVolumeCapacity)
                return true;

            vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
            volume = default;
            return false;
        }

        private void ReleaseOccupancyWriteBuffer()
        {
            ReleaseVolumeWriteBuffer(in _occupancyVolumeHandle, OccupancyVolumeBufferId);
        }

        private void ReleaseSdfWriteBuffer()
        {
            ReleaseVolumeWriteBuffer(in _sdfVolumeHandle, SdfVolumeBufferId);
        }

        private void ReleaseVolumeWriteBuffer(in VaultGenerationHandle<byte> handle, BufferID expectedBufferId)
        {
            IDataVault vault = _dataVault;
            if (vault != null && IsExactVaultHandle(in handle, expectedBufferId))
                vault.ReleaseWriteLock(in handle, VaultOwnerSystemId);
        }

        private static void ReleaseVaultHandle(IDataVault vault, ref VaultGenerationHandle<byte> handle)
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsExactVaultHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) && handle.Generation != 0u;
        }

        private static void PublishInactiveGlobals()
        {
            Shader.SetGlobalFloat(_CaveVoxelActiveId, 0f);
            Shader.SetGlobalVector(_CaveVoxelHalfExtentsId, Vector4.zero);
            Shader.SetGlobalVector(_CaveVoxelInvDoubleHalfExtentsId, Vector4.zero);
            Shader.SetGlobalMatrix(_CaveVoxelWorldToLocalId, Matrix4x4.identity);
        }

        private static Vector4 ResolveInvDoubleHalfExtents(Vector3 halfExtents)
        {
            return new Vector4(
                0.5f / Mathf.Max(0.001f, halfExtents.x),
                0.5f / Mathf.Max(0.001f, halfExtents.y),
                0.5f / Mathf.Max(0.001f, halfExtents.z),
                0f);
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
