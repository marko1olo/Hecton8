using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Visor
{
    /// <summary>
    /// Builds a bounded flashlight-aligned voxel SDF volume and publishes it as global shader state.
    /// The volume is refreshed incrementally to avoid synchronous stalls and shadow-map VRAM pressure.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerFlashlight))]
    public sealed class HectonFlashlightVoxelShadowProvider : MonoBehaviour, ITickable, IUpdatable
    {
        private const int MaxOverlapHits = 8;
        private const int TelemetryScanTickOverrunSlack = 2;
        private const float TelemetryWarningIntervalSeconds = 2f;
        private const float DefaultSignalInstabilityStrength = 0.18f;
        private const uint TelemetryWarningOverlapSaturatedHash = 0x8A7D5C21u;
        private const uint TelemetryWarningLongScanHash = 0x6E4B13C2u;
        private const uint TelemetryWarningDegenerateSdfHash = 0xC42A71D9u;
        private const uint TelemetryContextFlashlightVoxelShadowHash = 0xF14C993Au;
        private const float DefaultShadowBias = 0.06f;
        private const float DefaultShadowFloor = 0.08f;
        private const float DefaultShadowMinStep = 0.12f;
        private const float DefaultShadowSoftness = 6.5f;
        private const float DefaultShadowStepCount = 7f;
        private const float LightResolveRetryIntervalSeconds = 0.5f;
        private const float DegreesToRadians = 0.017453292519943295f;
        private const float InvTwoPi = 0.15915494f;

        private static readonly int _FlashlightActiveId = Shader.PropertyToID("_HectonFlashlightActive");
        private static readonly int _FlashlightVoxelActiveId = Shader.PropertyToID("_HectonFlashlightVoxelActive");
        private static readonly int _FlashlightPositionWsId = Shader.PropertyToID("_HectonFlashlightPositionWS");
        private static readonly int _FlashlightDirectionWsId = Shader.PropertyToID("_HectonFlashlightDirectionWS");
        private static readonly int _FlashlightColorId = Shader.PropertyToID("_HectonFlashlightColor");
        private static readonly int _FlashlightConeDataId = Shader.PropertyToID("_HectonFlashlightConeData");
        private static readonly int _FlashlightVoxelWorldToLocalId = Shader.PropertyToID("_HectonFlashlightVoxelWorldToLocal");
        private static readonly int _FlashlightVoxelHalfExtentsId = Shader.PropertyToID("_HectonFlashlightVoxelHalfExtents");
        private static readonly int _FlashlightShadowStepsId = Shader.PropertyToID("_HectonFlashlightShadowSteps");
        private static readonly int _FlashlightShadowSoftnessId = Shader.PropertyToID("_HectonFlashlightShadowSoftness");
        private static readonly int _FlashlightShadowMinStepId = Shader.PropertyToID("_HectonFlashlightShadowMinStep");
        private static readonly int _FlashlightShadowBiasId = Shader.PropertyToID("_HectonFlashlightShadowBias");
        private static readonly int _FlashlightShadowFloorId = Shader.PropertyToID("_HectonFlashlightShadowFloor");
        private static readonly int _VoxelDensityTexId = Shader.PropertyToID("_VoxelDensityTex");

        [Header("── Voxel Grid ──────────────────")]
        [Tooltip("Flashlight-local voxel resolution. Lower keeps CPU cost and VRAM down on MX350.")]
        [SerializeField, Range(12, 20)] private int voxelResolution = 16;

        [Tooltip("Number of z-slices refreshed per tick. Higher refreshes faster but costs more CPU that frame.")]
        [SerializeField, Range(1, 8)] private int slicesPerTick = 4;

        [Tooltip("World layers sampled as occluders while voxelizing the flashlight volume.")]
        [SerializeField] private LayerMask occluderLayers = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        [Tooltip("Physics trigger policy used while voxelizing the flashlight shadow volume.")]
        [SerializeField] private QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

        [Tooltip("Fraction of each voxel cell sampled during the overlap-box occupancy pass.")]
        [SerializeField, Range(0.5f, 1f)] private float occupancyPadding = 0.92f;

        [Header("── Refresh ─────────────────────")]
        [Tooltip("Position drift required before a new voxel sweep is queued.")]
        [SerializeField, Range(0.05f, 1f)] private float positionRefreshThreshold = 0.3f;

        [Tooltip("Angular drift required before a new voxel sweep is queued.")]
        [SerializeField, Range(1f, 12f)] private float rotationRefreshThresholdDegrees = 5f;

        [Tooltip("Extra padding applied to the flashlight cone radius while building the voxel box.")]
        [SerializeField, Range(1f, 1.5f)] private float coneRadiusPadding = 1.08f;

        [Tooltip("Signed-distance clamp expressed in cell diagonals before encoding to the voxel texture.")]
        [SerializeField, Range(2f, 6f)] private float sdfRangeInCellDiagonals = 4f;

        [Header("Noir Signal Instability")]
        [Tooltip("Adds deterministic low-cost flashlight shadow shimmer while voxel occlusion is stale or rebuilding.")]
        [SerializeField] private bool enableNoirSignalInstability = true;

        [Tooltip("Maximum deterministic dimming/shadow-floor shimmer applied while the flashlight SDF is rebuilding.")]
        [SerializeField, Range(0f, 0.35f)] private float noirSignalInstabilityStrength = DefaultSignalInstabilityStrength;

        [Header("── Diagnostics ─────────────────")]
        [SerializeField] private bool _debugHasValidVolume;
        [SerializeField] private int _debugSliceCursor;
        [SerializeField] private Vector3 _debugPublishedHalfExtents;
        [SerializeField] private float _debugPublishedSdfRange;
        [SerializeField] private float _debugSignalInstability01;

        private bool _registered;
        private bool _restartQueued;
        private bool _hasValidPublishedVolume;
        private bool _scanInProgress;
        private PlayerFlashlight _flashlight;
        private Transform _playerRoot;
        private Light _flashlightLight;
        private Texture3D _voxelDensityTexture;
        private NativeArray<byte> _occupancyVolume;
        private NativeArray<byte> _sdfVolume;
        private Collider[] _overlapHits = null;
        private Vector3[] _scanLocalCenters = null;
        private Vector3[] _occupiedCenters = null;
        private Vector3[] _emptyCenters = null;
        private int _scanSliceCursor;
        private int _resolutionRuntime;
        private Vector3 _scanCenterWs;
        private Quaternion _scanRotationWs = Quaternion.identity;
        private Matrix4x4 _scanLocalToWorld = Matrix4x4.identity;
        private Vector3 _scanHalfExtents;
        private Vector3 _scanCellSize;
        private Vector3 _scanCellHalfExtents;
        private float _scanCellDiagonal;
        private float _scanSdfRange;
        private Vector3 _publishedCenterWs;
        private Quaternion _publishedRotationWs = Quaternion.identity;
        private Matrix4x4 _publishedWorldToLocal = Matrix4x4.identity;
        private Vector3 _publishedHalfExtents;
        private float _publishedSdfRange;
        private float _nextLightResolveTime;
        private float _nextTelemetryOverlapSaturationTime;
        private float _nextTelemetryLongScanTime;
        private float _nextTelemetryDegenerateSdfTime;
        private int _scanTickCount;

        private void Awake()
        {
            _flashlight = GetComponent<PlayerFlashlight>();
            _playerRoot = transform.root;
            EnsureResources();
            TryResolveFlashlightLight(force: true);
            PublishInactiveGlobals();
        }

        private void OnEnable()
        {
            TryRegister();
        }

        private void OnDisable()
        {
            TryUnregister();
            PublishInactiveGlobals();
        }

        private void OnDestroy()
        {
            TryUnregister();
            PublishInactiveGlobals();
            ReleaseResources();
        }

        /// <summary>
        /// Updates the incremental flashlight voxel sweep and publishes the active SDF shadow globals.
        /// </summary>
        /// <param name="deltaTime">Tick delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
            EnsureResources();
            if (!TryResolveFlashlightLight())
            {
                PublishInactiveGlobals();
                return;
            }

            if (!IsFlashlightOperational())
            {
                PublishLightGlobals(false);
                return;
            }

            BuildDesiredVolumeDescriptor(
                _flashlightLight,
                out Vector3 desiredCenterWs,
                out Quaternion desiredRotationWs,
                out Vector3 desiredHalfExtents,
                out Vector3 desiredCellSize,
                out float desiredCellDiagonal,
                out float desiredSdfRange);

            bool refreshRequired = RequiresRefresh(desiredCenterWs, desiredRotationWs, desiredHalfExtents);
            if (!_scanInProgress && (!_hasValidPublishedVolume || refreshRequired || _restartQueued))
            {
                BeginScan(
                    desiredCenterWs,
                    desiredRotationWs,
                    desiredHalfExtents,
                    desiredCellSize,
                    desiredCellDiagonal,
                    desiredSdfRange);
            }
            else if (_scanInProgress && refreshRequired)
            {
                _restartQueued = true;
            }

            int remainingSlices = math.max(1, slicesPerTick);
            while (_scanInProgress && remainingSlices > 0 && _scanSliceCursor < _resolutionRuntime)
            {
                ScanSlice(_scanSliceCursor);
                _scanSliceCursor++;
                remainingSlices--;
            }

            if (_scanInProgress && _scanSliceCursor >= _resolutionRuntime)
            {
                bool restartQueued = _restartQueued;
                FinalizeScan();
                if (restartQueued)
                {
                    _restartQueued = false;
                    BeginScan(
                        desiredCenterWs,
                        desiredRotationWs,
                        desiredHalfExtents,
                        desiredCellSize,
                        desiredCellDiagonal,
                        desiredSdfRange);
                }
                else
                {
                    _restartQueued = false;
                }
            }
            else if (_scanInProgress)
            {
                _scanTickCount++;
                PublishLongScanTelemetryIfNeeded();
            }

            PublishLightGlobals(_hasValidPublishedVolume);
            _debugHasValidVolume = _hasValidPublishedVolume;
            _debugSliceCursor = _scanSliceCursor;
            _debugPublishedHalfExtents = _publishedHalfExtents;
            _debugPublishedSdfRange = _publishedSdfRange;
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registered = GlobalRegistry.Updatables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);
            _registered = false;
        }

        private void EnsureResources()
        {
            int clampedResolution = math.clamp(voxelResolution, 12, 20);
            if (_resolutionRuntime == clampedResolution &&
                _occupancyVolume.IsCreated &&
                _sdfVolume.IsCreated &&
                _voxelDensityTexture != null &&
                _scanLocalCenters != null &&
                _scanLocalCenters.Length == clampedResolution * clampedResolution * clampedResolution)
            {
                return;
            }

            ReleaseResources();

            _resolutionRuntime = clampedResolution;
            int voxelCount = clampedResolution * clampedResolution * clampedResolution;
            // COLD ALLOC: NativeArray<byte>[voxelCount] — flashlight-local voxel occupancy volume staging — owner: HectonFlashlightVoxelShadowProvider
            _occupancyVolume = new NativeArray<byte>(voxelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _occupancyVolume,
                nameof(HectonFlashlightVoxelShadowProvider),
                nameof(_occupancyVolume),
                NativeAllocationLifetime.Scene);
            // COLD ALLOC: NativeArray<byte>[voxelCount] — flashlight-local signed-distance texture payload — owner: HectonFlashlightVoxelShadowProvider
            _sdfVolume = new NativeArray<byte>(voxelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
            NativeMemorySentinel.RegisterNativeArray(
                _sdfVolume,
                nameof(HectonFlashlightVoxelShadowProvider),
                nameof(_sdfVolume),
                NativeAllocationLifetime.Scene);
            // COLD ALLOC: Collider[8] — reusable overlap-box hit cache for flashlight voxelization — owner: HectonFlashlightVoxelShadowProvider
            _overlapHits = new Collider[MaxOverlapHits];
            // COLD ALLOC: Vector3[voxelCount] — current sweep voxel-center cache for SDF encoding — owner: HectonFlashlightVoxelShadowProvider
            _scanLocalCenters = new Vector3[voxelCount];
            // COLD ALLOC: Vector3[voxelCount] — occupied-cell center cache for SDF encoding — owner: HectonFlashlightVoxelShadowProvider
            _occupiedCenters = new Vector3[voxelCount];
            // COLD ALLOC: Vector3[voxelCount] — empty-cell center cache for SDF encoding — owner: HectonFlashlightVoxelShadowProvider
            _emptyCenters = new Vector3[voxelCount];

            TextureFormat textureFormat = SystemInfo.SupportsTextureFormat(TextureFormat.R8)
                ? TextureFormat.R8
                : TextureFormat.Alpha8;
            _voxelDensityTexture = new Texture3D(clampedResolution, clampedResolution, clampedResolution, textureFormat, false)
            {
                name = "__HectonFlashlightVoxelDensityTex",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0
            }; // COLD ALLOC: Texture3D[1] — flashlight-local voxel SDF shadow volume — owner: HectonFlashlightVoxelShadowProvider
            _scanSliceCursor = 0;
            _restartQueued = false;
            _hasValidPublishedVolume = false;
            _scanInProgress = false;
            Shader.SetGlobalTexture(_VoxelDensityTexId, _voxelDensityTexture);
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
            _scanLocalCenters = null;
            _occupiedCenters = null;
            _emptyCenters = null;
            _voxelDensityTexture = null;
            _resolutionRuntime = 0;
            _scanSliceCursor = 0;
            _hasValidPublishedVolume = false;
            _scanInProgress = false;
            _restartQueued = false;
            _scanTickCount = 0;
            _debugSignalInstability01 = 0f;
        }

        private bool TryResolveFlashlightLight(bool force = false)
        {
            if (_flashlightLight != null)
                return true;

            if (!force)
            {
                float now = Time.unscaledTime;
                if (now < _nextLightResolveTime)
                    return false;

                _nextLightResolveTime = now + LightResolveRetryIntervalSeconds;
            }

            if (_flashlight == null)
                _flashlight = GetComponent<PlayerFlashlight>();

            Light candidate = FindFirstSpotLightInHierarchy(transform);
            if (candidate != null && candidate.type == LightType.Spot)
            {
                _flashlightLight = candidate;
                return true;
            }

            return false;
        }

        private static Light FindFirstSpotLightInHierarchy(Transform root)
        {
            if (root == null)
                return null;

            if (root.TryGetComponent(out Light directLight) && directLight.type == LightType.Spot)
                return directLight;

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Light childLight = FindFirstSpotLightInHierarchy(root.GetChild(i));
                if (childLight != null)
                    return childLight;
            }

            return null;
        }

        private bool IsFlashlightOperational()
        {
            return _flashlight != null &&
                _flashlightLight != null &&
                _flashlightLight.enabled &&
                _flashlightLight.gameObject.activeInHierarchy &&
                _flashlightLight.intensity > 0.01f &&
                _flashlightLight.range > 0.1f;
        }

        private void BuildDesiredVolumeDescriptor(
            Light light,
            out Vector3 centerWs,
            out Quaternion rotationWs,
            out Vector3 halfExtents,
            out Vector3 cellSize,
            out float cellDiagonal,
            out float sdfRange)
        {
            Transform lightTransform = light.transform;
            float range = math.max(0.1f, light.range);
            float coneHalfAngleRadians = math.max(1f, light.spotAngle * 0.5f) * DegreesToRadians;
            float coneRadius = math.max(0.35f, ApproximateTanPositive(coneHalfAngleRadians) * range * coneRadiusPadding);
            float invResolution = math.rcp(math.max(1, _resolutionRuntime));

            rotationWs = lightTransform.rotation;
            halfExtents = new Vector3(coneRadius, coneRadius, range * 0.5f);
            centerWs = lightTransform.position + lightTransform.forward * halfExtents.z;
            cellSize = new Vector3(
                halfExtents.x * 2f * invResolution,
                halfExtents.y * 2f * invResolution,
                halfExtents.z * 2f * invResolution);
            cellDiagonal = ApproximateMagnitude((float3)cellSize);
            sdfRange = math.max(cellDiagonal * math.max(1f, sdfRangeInCellDiagonals), cellDiagonal);
        }

        private bool RequiresRefresh(Vector3 desiredCenterWs, Quaternion desiredRotationWs, Vector3 desiredHalfExtents)
        {
            if (!_hasValidPublishedVolume)
                return true;

            if ((_publishedCenterWs - desiredCenterWs).sqrMagnitude > positionRefreshThreshold * positionRefreshThreshold)
                return true;

            float rotationDot = math.saturate(math.abs(Quaternion.Dot(_publishedRotationWs, desiredRotationWs)));
            float rotationRefreshHalfRadians = math.max(0f, rotationRefreshThresholdDegrees) * DegreesToRadians * 0.5f;
            if (rotationDot < ApproximateSpotConeCos(rotationRefreshHalfRadians))
                return true;

            if ((_publishedHalfExtents - desiredHalfExtents).sqrMagnitude > 0.01f)
                return true;

            return false;
        }

        private void BeginScan(
            Vector3 centerWs,
            Quaternion rotationWs,
            Vector3 halfExtents,
            Vector3 cellSize,
            float cellDiagonal,
            float sdfRange)
        {
            _scanCenterWs = centerWs;
            _scanRotationWs = rotationWs;
            _scanHalfExtents = halfExtents;
            _scanCellSize = cellSize;
            _scanCellHalfExtents = cellSize * (0.5f * math.clamp(occupancyPadding, 0.5f, 1f));
            _scanCellDiagonal = cellDiagonal;
            _scanSdfRange = sdfRange;
            _scanLocalToWorld = Matrix4x4.TRS(centerWs, rotationWs, Vector3.one);
            _scanSliceCursor = 0;
            _scanInProgress = true;
            _scanTickCount = 0;

            if (!HasPotentialOccluderInCurrentScanVolume())
                FinalizeEmptyScan();
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
                    bool occupied = IsCellOccupied(worldCenter);
                    _occupancyVolume[voxelIndex] = occupied ? byte.MaxValue : byte.MinValue;
                }
            }
        }

        private bool IsCellOccupied(Vector3 worldCenter)
        {
            int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                worldCenter,
                _scanCellHalfExtents,
                _overlapHits,
                _scanRotationWs,
                occluderLayers,
                triggerInteraction);
            if (hitCount >= _overlapHits.Length)
            {
                PublishPerformanceWarningRateLimited(
                    TelemetryWarningOverlapSaturatedHash,
                    ref _nextTelemetryOverlapSaturationTime,
                    hitCount);
            }

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = _overlapHits[hitIndex];
                if (hit == null || !hit.enabled)
                    continue;

                Transform hitRoot = hit.transform.root;
                if (_playerRoot != null && hitRoot == _playerRoot)
                    continue;

                return true;
            }

            return false;
        }

        private void FinalizeScan()
        {
            EncodeSignedDistanceField();
            PublishSdfTextureFromCurrentScan();
        }

        private void FinalizeEmptyScan()
        {
            int voxelCount = _resolutionRuntime * _resolutionRuntime * _resolutionRuntime;
            for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                _sdfVolume[voxelIndex] = byte.MaxValue;

            PublishSdfTextureFromCurrentScan();
            _restartQueued = false;
        }

        private void PublishSdfTextureFromCurrentScan()
        {
            _voxelDensityTexture.SetPixelData(_sdfVolume, 0);
            _voxelDensityTexture.Apply(false, false);

            _publishedCenterWs = _scanCenterWs;
            _publishedRotationWs = _scanRotationWs;
            _publishedHalfExtents = _scanHalfExtents;
            _publishedSdfRange = _scanSdfRange;
            _publishedWorldToLocal = _scanLocalToWorld.inverse;
            _hasValidPublishedVolume = true;
            _scanInProgress = false;
            _scanSliceCursor = 0;
            Shader.SetGlobalTexture(_VoxelDensityTexId, _voxelDensityTexture);
        }

        private bool HasPotentialOccluderInCurrentScanVolume()
        {
            if (_overlapHits == null || _overlapHits.Length == 0)
                return true;

            int hitCount = UnityEngine.Physics.OverlapBoxNonAlloc(
                _scanCenterWs,
                _scanHalfExtents,
                _overlapHits,
                _scanRotationWs,
                occluderLayers,
                triggerInteraction);

            if (hitCount >= _overlapHits.Length)
            {
                PublishPerformanceWarningRateLimited(
                    TelemetryWarningOverlapSaturatedHash,
                    ref _nextTelemetryOverlapSaturationTime,
                    hitCount);
                return true;
            }

            for (int hitIndex = 0; hitIndex < hitCount; hitIndex++)
            {
                Collider hit = _overlapHits[hitIndex];
                if (hit == null || !hit.enabled)
                    continue;

                Transform hitRoot = hit.transform.root;
                if (_playerRoot != null && hitRoot == _playerRoot)
                    continue;

                return true;
            }

            return false;
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
                PublishPerformanceWarningRateLimited(
                    TelemetryWarningDegenerateSdfHash,
                    ref _nextTelemetryDegenerateSdfTime,
                    0f);
                for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                    _sdfVolume[voxelIndex] = byte.MaxValue;
                return;
            }

            if (emptyCount <= 0)
            {
                PublishPerformanceWarningRateLimited(
                    TelemetryWarningDegenerateSdfHash,
                    ref _nextTelemetryDegenerateSdfTime,
                    voxelCount);
                for (int voxelIndex = 0; voxelIndex < voxelCount; voxelIndex++)
                    _sdfVolume[voxelIndex] = byte.MinValue;
                return;
            }

            float boundaryBias = _scanCellDiagonal * 0.5f;
            float inverseSdfRange = _scanSdfRange > 0.0001f ? math.rcp(_scanSdfRange) : 0f;
            float zeroBandDistanceSq = boundaryBias * math.max(_scanSdfRange, 0.0001f);

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
                    {
                        nearestDistanceSq = candidateDistanceSq;
                        if (nearestDistanceSq <= zeroBandDistanceSq)
                            break;
                    }
                }

                float unsignedDistance = nearestDistanceSq < float.MaxValue
                    ? math.max(0f, ApproximateDistanceFromSq(nearestDistanceSq, _scanSdfRange) - boundaryBias)
                    : _scanSdfRange;
                float signedDistance = occupied ? -unsignedDistance : unsignedDistance;
                float encoded = math.saturate((signedDistance * inverseSdfRange) * 0.5f + 0.5f);
                int encodedByte = (int)(encoded * 255f + 0.5f);
                _sdfVolume[voxelIndex] = (byte)math.clamp(encodedByte, 0, 255);
            }
        }

        private void PublishLightGlobals(bool hasVoxelVolume)
        {
            if (_flashlightLight == null)
            {
                PublishInactiveGlobals();
                return;
            }

            float outerAngleRadians = math.max(1f, _flashlightLight.spotAngle * 0.5f) * DegreesToRadians;
            float innerAngleRadians = outerAngleRadians * 0.76f;
            float outerCos = ApproximateSpotConeCos(outerAngleRadians);
            float innerCos = ApproximateSpotConeCos(innerAngleRadians);
            Vector3 lightPositionWs = _flashlightLight.transform.position;
            Vector3 lightDirectionWs = _flashlightLight.transform.forward;
            Color lightColor = _flashlightLight.color;
            float lightRange = math.max(0.1f, _flashlightLight.range);
            float signalInstability01 = ResolveNoirSignalInstability(hasVoxelVolume);
            float shadowFloor = math.saturate(DefaultShadowFloor + signalInstability01 * 0.16f);
            float lightIntensity = math.max(0f, _flashlightLight.intensity * (1f - signalInstability01 * 0.22f));
            _debugSignalInstability01 = signalInstability01;

            Shader.SetGlobalFloat(_FlashlightActiveId, 1f);
            Shader.SetGlobalFloat(_FlashlightVoxelActiveId, hasVoxelVolume ? 1f : 0f);
            Shader.SetGlobalFloat(_FlashlightShadowStepsId, DefaultShadowStepCount);
            Shader.SetGlobalFloat(_FlashlightShadowSoftnessId, DefaultShadowSoftness);
            Shader.SetGlobalFloat(_FlashlightShadowMinStepId, DefaultShadowMinStep);
            Shader.SetGlobalFloat(_FlashlightShadowBiasId, DefaultShadowBias);
            Shader.SetGlobalFloat(_FlashlightShadowFloorId, shadowFloor);
            Shader.SetGlobalVector(
                _FlashlightPositionWsId,
                new Vector4(lightPositionWs.x, lightPositionWs.y, lightPositionWs.z, lightRange));
            Shader.SetGlobalVector(
                _FlashlightDirectionWsId,
                new Vector4(lightDirectionWs.x, lightDirectionWs.y, lightDirectionWs.z, innerCos));
            Shader.SetGlobalVector(
                _FlashlightColorId,
                new Vector4(lightColor.r, lightColor.g, lightColor.b, lightIntensity));
            Shader.SetGlobalVector(
                _FlashlightConeDataId,
                new Vector4(
                    outerCos,
                    1f,
                    lightRange > 0.0001f ? math.rcp(lightRange) : 0f,
                    shadowFloor));

            if (hasVoxelVolume)
            {
                Shader.SetGlobalMatrix(_FlashlightVoxelWorldToLocalId, _publishedWorldToLocal);
                Shader.SetGlobalVector(
                    _FlashlightVoxelHalfExtentsId,
                    new Vector4(
                        _publishedHalfExtents.x,
                        _publishedHalfExtents.y,
                        _publishedHalfExtents.z,
                        _publishedSdfRange));
                Shader.SetGlobalTexture(_VoxelDensityTexId, _voxelDensityTexture);
            }
        }

        private float ResolveNoirSignalInstability(bool hasVoxelVolume)
        {
            if (!enableNoirSignalInstability)
                return 0f;

            float staleSignal = hasVoxelVolume ? 0f : 1f;
            float rebuildSignal = _scanInProgress ? 0.55f : 0f;
            float restartSignal = _restartQueued ? 0.85f : 0f;
            float scanProgress = _resolutionRuntime > 0
                ? math.saturate(_scanSliceCursor * math.rcp((float)_resolutionRuntime))
                : 0f;
            float carrier = EvaluateCheapCarrier01((Time.frameCount * 0.6180339f) + scanProgress * 5.1f);
            float instability = math.max(staleSignal, math.max(rebuildSignal, restartSignal)) * carrier;
            return math.saturate(instability * noirSignalInstabilityStrength);
        }

        private static float ApproximateMagnitude(float3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + midAxis * 0.375f + minAxis * 0.125f;
        }

        private static float ApproximateDistanceFromSq(float distanceSq, float range)
        {
            return distanceSq > 0f
                ? distanceSq * math.rcp(math.max(range, 0.0001f))
                : 0f;
        }

        private static float ApproximateSpotConeCos(float angleRadians)
        {
            float x = math.clamp(angleRadians, 0f, 1.5707964f);
            float x2 = x * x;
            float x4 = x2 * x2;
            return math.saturate(1f - 0.4967f * x2 + 0.03705f * x4);
        }

        private static float ApproximateTanPositive(float angleRadians)
        {
            float x = math.clamp(angleRadians, 0f, 1.4f);
            float x2 = x * x;
            return x * (15f - x2) * math.rcp(math.max(0.0001f, 15f - 6f * x2));
        }

        private static float EvaluateCheapCarrier01(float phase)
        {
            float phase01 = math.frac((phase * InvTwoPi) + 0.25f);
            float triangle = 1f - math.abs(phase01 * 2f - 1f);
            return triangle * triangle;
        }

        private void PublishLongScanTelemetryIfNeeded()
        {
            int safeSlicesPerTick = math.max(1, slicesPerTick);
            int expectedTicks = (_resolutionRuntime + safeSlicesPerTick - 1) / safeSlicesPerTick;
            if (_scanTickCount <= expectedTicks + TelemetryScanTickOverrunSlack)
                return;

            PublishPerformanceWarningRateLimited(
                TelemetryWarningLongScanHash,
                ref _nextTelemetryLongScanTime,
                _scanTickCount);
        }

        private static void PublishPerformanceWarningRateLimited(
            uint warningHash,
            ref float nextWarningTime,
            float scalarValue)
        {
            float now = Time.unscaledTime;
            if (now < nextWarningTime)
                return;

            nextWarningTime = now + TelemetryWarningIntervalSeconds;
            GlobalTelemetryBus.PublishPerformanceWarning(
                warningHash,
                TelemetryContextFlashlightVoxelShadowHash,
                scalarValue);
        }

        private static void PublishInactiveGlobals()
        {
            Shader.SetGlobalFloat(_FlashlightActiveId, 0f);
            Shader.SetGlobalFloat(_FlashlightVoxelActiveId, 0f);
            Shader.SetGlobalFloat(_FlashlightShadowStepsId, DefaultShadowStepCount);
            Shader.SetGlobalFloat(_FlashlightShadowSoftnessId, DefaultShadowSoftness);
            Shader.SetGlobalFloat(_FlashlightShadowMinStepId, DefaultShadowMinStep);
            Shader.SetGlobalFloat(_FlashlightShadowBiasId, DefaultShadowBias);
            Shader.SetGlobalFloat(_FlashlightShadowFloorId, DefaultShadowFloor);
            Shader.SetGlobalVector(_FlashlightPositionWsId, Vector4.zero);
            Shader.SetGlobalVector(_FlashlightDirectionWsId, Vector4.zero);
            Shader.SetGlobalVector(_FlashlightColorId, Vector4.zero);
            Shader.SetGlobalVector(_FlashlightConeDataId, Vector4.zero);
            Shader.SetGlobalVector(_FlashlightVoxelHalfExtentsId, Vector4.zero);
            Shader.SetGlobalMatrix(_FlashlightVoxelWorldToLocalId, Matrix4x4.identity);
        }
    }
}
