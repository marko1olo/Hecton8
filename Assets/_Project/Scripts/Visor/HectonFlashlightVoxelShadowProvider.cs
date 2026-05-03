using Hecton8.Core;
using Hecton8.Gameplay;
using Unity.Collections;
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
        private const float DefaultShadowBias = 0.06f;
        private const float DefaultShadowFloor = 0.08f;
        private const float DefaultShadowMinStep = 0.12f;
        private const float DefaultShadowSoftness = 6.5f;
        private const float DefaultShadowStepCount = 24f;
        private const float LightResolveRetryIntervalSeconds = 0.5f;

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

        [Header("── Diagnostics ─────────────────")]
        [SerializeField] private bool _debugHasValidVolume;
        [SerializeField] private int _debugSliceCursor;
        [SerializeField] private Vector3 _debugPublishedHalfExtents;
        [SerializeField] private float _debugPublishedSdfRange;

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
                    BeginScan(
                        desiredCenterWs,
                        desiredRotationWs,
                        desiredHalfExtents,
                        desiredCellSize,
                        desiredCellDiagonal,
                        desiredSdfRange);
                    _restartQueued = false;
                }
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
            int clampedResolution = Mathf.Clamp(voxelResolution, 12, 20);
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
            // COLD ALLOC: NativeArray<byte>[voxelCount] — flashlight-local signed-distance texture payload — owner: HectonFlashlightVoxelShadowProvider
            _sdfVolume = new NativeArray<byte>(voxelCount, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
            float range = Mathf.Max(0.1f, light.range);
            float coneHalfAngleRadians = Mathf.Max(1f, light.spotAngle * 0.5f) * Mathf.Deg2Rad;
            float coneRadius = Mathf.Max(0.35f, Mathf.Tan(coneHalfAngleRadians) * range * coneRadiusPadding);

            rotationWs = lightTransform.rotation;
            halfExtents = new Vector3(coneRadius, coneRadius, range * 0.5f);
            centerWs = lightTransform.position + lightTransform.forward * halfExtents.z;
            cellSize = new Vector3(
                (halfExtents.x * 2f) / Mathf.Max(1, _resolutionRuntime),
                (halfExtents.y * 2f) / Mathf.Max(1, _resolutionRuntime),
                (halfExtents.z * 2f) / Mathf.Max(1, _resolutionRuntime));
            cellDiagonal = cellSize.magnitude;
            sdfRange = Mathf.Max(cellDiagonal * Mathf.Max(1f, sdfRangeInCellDiagonals), cellDiagonal);
        }

        private bool RequiresRefresh(Vector3 desiredCenterWs, Quaternion desiredRotationWs, Vector3 desiredHalfExtents)
        {
            if (!_hasValidPublishedVolume)
                return true;

            if ((_publishedCenterWs - desiredCenterWs).sqrMagnitude > positionRefreshThreshold * positionRefreshThreshold)
                return true;

            float rotationDot = Mathf.Abs(Quaternion.Dot(_publishedRotationWs, desiredRotationWs));
            rotationDot = Mathf.Clamp(rotationDot, -1f, 1f);
            float rotationAngle = Mathf.Acos(rotationDot) * 2f * Mathf.Rad2Deg;
            if (rotationAngle > rotationRefreshThresholdDegrees)
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
            _scanCellHalfExtents = cellSize * (0.5f * Mathf.Clamp(occupancyPadding, 0.5f, 1f));
            _scanCellDiagonal = cellDiagonal;
            _scanSdfRange = sdfRange;
            _scanLocalToWorld = Matrix4x4.TRS(centerWs, rotationWs, Vector3.one);
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
            _voxelDensityTexture.SetPixelData(_sdfVolume, 0);
            _voxelDensityTexture.Apply(false, false);

            _publishedCenterWs = _scanCenterWs;
            _publishedRotationWs = _scanRotationWs;
            _publishedHalfExtents = _scanHalfExtents;
            _publishedSdfRange = _scanSdfRange;
            _publishedWorldToLocal = _scanLocalToWorld.inverse;
            _hasValidPublishedVolume = true;
            _restartQueued = false;
            _scanInProgress = false;
            _scanSliceCursor = 0;
            Shader.SetGlobalTexture(_VoxelDensityTexId, _voxelDensityTexture);
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

        private void PublishLightGlobals(bool hasVoxelVolume)
        {
            if (_flashlightLight == null)
            {
                PublishInactiveGlobals();
                return;
            }

            float outerAngleRadians = Mathf.Max(1f, _flashlightLight.spotAngle * 0.5f) * Mathf.Deg2Rad;
            float innerAngleRadians = outerAngleRadians * 0.76f;
            float outerCos = Mathf.Cos(outerAngleRadians);
            float innerCos = Mathf.Cos(innerAngleRadians);
            Vector3 lightPositionWs = _flashlightLight.transform.position;
            Vector3 lightDirectionWs = _flashlightLight.transform.forward;
            Color lightColor = _flashlightLight.color;
            float lightRange = Mathf.Max(0.1f, _flashlightLight.range);

            Shader.SetGlobalFloat(_FlashlightActiveId, 1f);
            Shader.SetGlobalFloat(_FlashlightVoxelActiveId, hasVoxelVolume ? 1f : 0f);
            Shader.SetGlobalFloat(_FlashlightShadowStepsId, DefaultShadowStepCount);
            Shader.SetGlobalFloat(_FlashlightShadowSoftnessId, DefaultShadowSoftness);
            Shader.SetGlobalFloat(_FlashlightShadowMinStepId, DefaultShadowMinStep);
            Shader.SetGlobalFloat(_FlashlightShadowBiasId, DefaultShadowBias);
            Shader.SetGlobalFloat(_FlashlightShadowFloorId, DefaultShadowFloor);
            Shader.SetGlobalVector(
                _FlashlightPositionWsId,
                new Vector4(lightPositionWs.x, lightPositionWs.y, lightPositionWs.z, lightRange));
            Shader.SetGlobalVector(
                _FlashlightDirectionWsId,
                new Vector4(lightDirectionWs.x, lightDirectionWs.y, lightDirectionWs.z, innerCos));
            Shader.SetGlobalVector(
                _FlashlightColorId,
                new Vector4(lightColor.r, lightColor.g, lightColor.b, Mathf.Max(0f, _flashlightLight.intensity)));
            Shader.SetGlobalVector(
                _FlashlightConeDataId,
                new Vector4(
                    outerCos,
                    1f,
                    lightRange > 0.0001f ? 1f / lightRange : 0f,
                    DefaultShadowFloor));

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
