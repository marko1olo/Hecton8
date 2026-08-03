using Hecton8.Bootstrap;
using Hecton8.Core;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Hecton8.Biolum
{
    /// <summary>
    /// Publishes a player-centered 3D bioluminescence radiance volume for flora shading.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HectonBiolumDiffusionVolume : MonoBehaviour, ILateFrameTickable, ISlowTickable, IOriginShiftListener, IGlobalRegistryHotSwapListener
    {
        private const int DefaultResolution = 64;
        private const float DefaultVolumeWorldSize = 72f;
        private const ulong MaxPortableComputeThreadsPerGroup = 256ul;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const int MaxTrackedZones = 32;
        private const int MaxGlowShaderPoints = 16;
        private const float MaxBiolumHdrIntensity = 10f;
        private const double CascadeTimeModulo = 65536d;
        private const float GlowPositionHashScale = 20f;
        private const float GlowRangeHashScale = 16f;
        private const float GlowColorHashScale = 255f;
        private const float GlowIntensityHashScale = 128f;
        private const float GlowPointSonarPulseGain = 2.5f;
        private const float HashClampMin = -2147483000f;
        private const float HashClampMax = 2147483000f;
        private const uint FnvOffsetBasis = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const int GlowPointInvalidNumberHash = unchecked((int)0x474C4F57); // "GLOW"
        private static readonly int _VolumeOutputId = Shader.PropertyToID("_HectonBiolumVolumeOutput");
        private static readonly int _VolumeInputId = Shader.PropertyToID("_HectonBiolumVolumeInput");
        private static readonly int _PointBufferId = Shader.PropertyToID("_HectonBiolumPoints");
        private static readonly int _PointCountId = Shader.PropertyToID("_HectonBiolumPointCount");
        private static readonly int _HalfExtentsId = Shader.PropertyToID("_HectonBiolumVolumeHalfExtents");
        private static readonly int _WorldToLocalId = Shader.PropertyToID("_HectonBiolumVolumeWorldToLocal");
        private static readonly int _VolumeParamsId = Shader.PropertyToID("_HectonBiolumVolumeParams");
        private static readonly int _CascadeParamsId = Shader.PropertyToID("_HectonBiolumCascadeParams");
        private static readonly int _TexelSizeId = Shader.PropertyToID("_HectonBiolumVolumeTexelSize");
        private static readonly int _GlobalTextureId = Shader.PropertyToID("_HectonBiolumVolumeTex");
        private static readonly int _GlobalActiveId = Shader.PropertyToID("_HectonBiolumVolumeActive");
        private static readonly int _GlowPointPositionRangeId = Shader.PropertyToID("_HectonGlowPointPositionRange");
        private static readonly int _GlowPointColorIntensityId = Shader.PropertyToID("_HectonGlowPointColorIntensity");
        private static readonly int _GlowPointParamsId = Shader.PropertyToID("_HectonGlowPointParams");

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct BiolumPointGpuData
        {
            [FieldOffset(0)] public Vector4 PositionRange;
            [FieldOffset(16)] public Vector4 ColorIntensity;
        }

        [Header("Compute")]
        [SerializeField]
        [Tooltip("Compute shader used to diffuse nearby biolum zones into a persistent 3D radiance volume.")]
        private ComputeShader biolumDiffusionCompute;

        [SerializeField, Range(32, 64)]
        [Tooltip("Resolution of the player-centered 3D radiance volume.")]
        private int volumeResolution = DefaultResolution;

        [SerializeField, Range(24f, 128f)]
        [Tooltip("World-space coverage size of the biolum diffusion volume around the player.")]
        private float volumeWorldSize = 72f;

        [SerializeField, Range(0.1f, 4f)]
        [Tooltip("Strength multiplier applied to injected zone radiance.")]
        private float injectionStrength = 1.2f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Diffusion blend applied per tick.")]
        private float diffusionStrength = 0.24f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Per-second volume decay applied before each reinjection pass.")]
        private float decayRate = 0.08f;

        [SerializeField, Range(0.05f, 0.95f)]
        [Tooltip("Radiance threshold above which a biolum voxel propagates a local cascade wave into adjacent voxels.")]
        private float cascadeSpikeThreshold = 0.32f;

        [SerializeField, Range(0f, 3f)]
        [Tooltip("Propagation gain applied when adjacent biolum voxels spike above the cascade threshold.")]
        private float cascadePropagationGain = 0.75f;

        [SerializeField, Range(0f, 12f)]
        [Tooltip("Wave speed used to phase-offset the biolum cascade through the player-centered volume.")]
        private float cascadeWaveSpeed = 4.4f;

        [SerializeField, Range(8f, 160f)]
        [Tooltip("Maximum radius used when gathering nearby biolum zone emitters.")]
        private float zoneGatherRadius = 88f;

        [Header("Diagnostics")]
        [SerializeField] private int _debugPointCount;
        [SerializeField] private Vector3 _debugVolumeCenter;

        private bool _registered;
        private bool _registeredSlowTick;
        private bool _registeredHotSwapListener;
        private bool _needsClear = true;
        private bool _hasLastVolumeCenter;
        private bool _supportsComputeShadersCold;
        private bool _dependencyResolveRequested;
        private bool _resourceRefreshRequested;
        private int _clearKernel = -1;
        private int _diffuseKernel = -1;
        private int _injectKernel = -1;
        private uint _clearThreadGroupSizeX;
        private uint _clearThreadGroupSizeY;
        private uint _clearThreadGroupSizeZ;
        private uint _diffuseThreadGroupSizeX;
        private uint _diffuseThreadGroupSizeY;
        private uint _diffuseThreadGroupSizeZ;
        private uint _injectThreadGroupSizeX;
        private uint _injectThreadGroupSizeY;
        private uint _injectThreadGroupSizeZ;
        private Transform _playerTransform;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private HectonBiolumManager _biolumManager;
        private ITickDispatcher _dispatcher;
        private Vector3 _lastVolumeCenter;
        private double _cascadeTimeSeconds;
        private int _lastUploadedPointCount = -1;
        private int _lastPublishedGlowCount = -1;
        private int _lastInvalidGlowTelemetryFrame = -1;
        private uint _pendingPointUploadHash;
        private uint _lastUploadedPointHash;
        private uint _pendingGlowHash;
        private uint _lastPublishedGlowHash;
        private RenderTexture _volumeA;
        private RenderTexture _volumeB;
        private GraphicsBuffer _pointBufferA;
        private GraphicsBuffer _pointBufferB;
        private GraphicsBuffer _activePointBuffer;
        private int _pointBufferWriteIndex;
        private readonly BiolumPointGpuData[] _pointUpload = new BiolumPointGpuData[MaxTrackedZones]; // COLD ALLOC: BiolumPointGpuData[32] — persistent GPU upload staging for biolum diffusion emitters — owner: HectonBiolumDiffusionVolume
        private readonly Vector4[] _glowPointPositionRangeUpload = new Vector4[MaxGlowShaderPoints]; // COLD ALLOC: Vector4[16] - shader-global glow point positions/ranges - owner: HectonBiolumDiffusionVolume
        private readonly Vector4[] _glowPointColorIntensityUpload = new Vector4[MaxGlowShaderPoints]; // COLD ALLOC: Vector4[16] - shader-global glow point colors/intensities - owner: HectonBiolumDiffusionVolume
        private readonly HectonBiolumZone[] _nearbyZones = new HectonBiolumZone[MaxTrackedZones]; // COLD ALLOC: HectonBiolumZone[32] — nearby biolum zone cache for diffusion volume injection — owner: HectonBiolumDiffusionVolume
        private readonly float[] _nearbyZoneWeights = new float[MaxTrackedZones]; // COLD ALLOC: float[32] — zone-weight scratch paired with nearby biolum zone cache — owner: HectonBiolumDiffusionVolume

        private void Awake()
        {
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            ResolveDependencies();
            EnsureResources();
            PublishGlobals();
        }

        private void OnEnable()
        {
            CacheGraphicsCapabilitiesCold();
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            HectonFloatingOrigin.RegisterListener(this);
            TryRegister();
            ResolveDependencies();
            EnsureResources();
            PublishGlobals();
        }

        private void OnDisable()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            ReleaseResources();
            Shader.SetGlobalFloat(_GlobalActiveId, 0f);
            PublishGlowPointGlobals(0, force: true);
        }

        private void OnDestroy()
        {
            HectonFloatingOrigin.UnregisterListener(this);
            TryUnregister();
            TryUnregisterHotSwapListener();
            ReleaseResources();
            PublishGlowPointGlobals(0, force: true);
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.BiolumManagerRuntime:
                    _biolumManager = currentService as HectonBiolumManager;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    CachePlayerTransformCold(_playerRuntimeContext);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _dispatcher = currentService as ITickDispatcher;
                    TryUnregister();
                    if (currentService != null && isActiveAndEnabled)
                        TryRegister();
                    break;
            }
        }

        /// <inheritdoc />
        public void OnOriginShift(in OriginShiftEventData shiftData)
        {
            Vector3 shiftOffset = shiftData.ShiftOffset;
            float shiftSqrMagnitude = shiftOffset.sqrMagnitude;
            if (!isActiveAndEnabled ||
                !MathGuard.IsFinite(shiftOffset) ||
                !MathGuard.IsFinite(shiftSqrMagnitude) ||
                shiftSqrMagnitude <= 0.0001f)
            {
                return;
            }

            _needsClear = true;
            _hasLastVolumeCenter = false;
            _lastVolumeCenter = default;
            _debugPointCount = 0;
            Shader.SetGlobalFloat(_GlobalActiveId, 0f);
            PublishGlowPointGlobals(0, force: true);
        }

        /// <summary>
        /// Updates the persistent 3D radiance volume from nearby biolum zone data.
        /// </summary>
        public void LateFrameTick()
        {
            // L19 hop2 LIVE: batch peel LateFrameTick - biolum diffusion hang headless.
            if (UnityEngine.Application.isBatchMode)
                return;

            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            if (deltaTime < 0f)
                return;

            float safeDeltaTime = SanitizeDelta(deltaTime);
            if (_playerTransform == null ||
                _biolumManager == null ||
                _volumeA == null ||
                _volumeB == null ||
                _activePointBuffer == null ||
                !HasValidKernelState())
            {
                _dependencyResolveRequested |= _playerTransform == null || _biolumManager == null;
                _resourceRefreshRequested |= _volumeA == null || _volumeB == null || _activePointBuffer == null || !HasValidKernelState();
                Shader.SetGlobalFloat(_GlobalActiveId, 0f);
                PublishGlowPointGlobals(0, force: true);
                return;
            }

            Vector3 volumeCenter = ResolveVolumeCenterRuntimePosition();
            if (!MathGuard.IsFinite(volumeCenter))
            {
                ReportInvalidGlowInput();
                Shader.SetGlobalFloat(_GlobalActiveId, 0f);
                PublishGlowPointGlobals(0, force: true);
                return;
            }

            _debugVolumeCenter = volumeCenter;

            int pointCount = CollectNearbyPoints(volumeCenter);
            _debugPointCount = pointCount;
            PublishGlowPointGlobals(pointCount);

            int safeVolumeResolution = math.clamp(volumeResolution, 32, 64);
            float safeVolumeWorldSize = SanitizeGlowPositive(volumeWorldSize, DefaultVolumeWorldSize);
            float invResolution = math.rcp(math.max(1f, (float)safeVolumeResolution));
            float worldTexelSize = safeVolumeWorldSize * invResolution;
            Vector3 centerOffset = _hasLastVolumeCenter ? volumeCenter - _lastVolumeCenter : Vector3.zero;
            float centerDeltaSq = _hasLastVolumeCenter ? centerOffset.sqrMagnitude : 0f;
            float clearDistance = safeVolumeWorldSize * 0.5f;
            if (_hasLastVolumeCenter && centerDeltaSq >= clearDistance * clearDistance)
                _needsClear = true;

            float centerDelta = centerDeltaSq > 0.000001f ? EstimateLength3D(centerOffset) : 0f;
            float motionDecayBoost = math.saturate(centerDelta / math.max(worldTexelSize * 4f, 0.001f));
            float safeDecayRate = math.saturate(SanitizeGlowNonNegative(decayRate, 0.08f));
            float resolvedDecayRate = math.saturate(safeDecayRate + motionDecayBoost * 0.45f);
            float cascadeTime = ResolveCascadeTimeSeconds(safeDeltaTime);
            float safeInjectionStrength = math.min(SanitizeGlowNonNegative(injectionStrength, 1.2f), MaxBiolumHdrIntensity);
            float safeDiffusionStrength = math.saturate(SanitizeGlowNonNegative(diffusionStrength, 0.24f));
            float safeCascadeSpikeThreshold = math.saturate(SanitizeGlowNonNegative(cascadeSpikeThreshold, 0.32f));
            float safeCascadePropagationGain = math.min(SanitizeGlowNonNegative(cascadePropagationGain, 0.75f), MaxBiolumHdrIntensity);
            float safeCascadeWaveSpeed = math.min(SanitizeGlowNonNegative(cascadeWaveSpeed, 4.4f), 64f);

            Matrix4x4 worldToLocal = Matrix4x4.Translate(-volumeCenter);
            Vector4 halfExtents = new Vector4(safeVolumeWorldSize * 0.5f, safeVolumeWorldSize * 0.5f, safeVolumeWorldSize * 0.5f, 0f);
            Vector4 volumeParams = new Vector4(
                safeInjectionStrength,
                safeDiffusionStrength,
                resolvedDecayRate,
                safeDeltaTime);
            Vector4 cascadeParams = new Vector4(
                safeCascadeSpikeThreshold,
                safeCascadePropagationGain,
                safeCascadeWaveSpeed,
                cascadeTime);
            Vector4 texelSize = new Vector4(
                invResolution,
                invResolution,
                invResolution,
                safeVolumeResolution);

            if (_needsClear)
            {
                BindSharedParameters(_clearKernel, halfExtents, worldToLocal, volumeParams, cascadeParams, texelSize, 0);
                biolumDiffusionCompute.SetTexture(_clearKernel, _VolumeOutputId, _volumeA);
                DispatchVolumeKernel(_clearKernel, _clearThreadGroupSizeX, _clearThreadGroupSizeY, _clearThreadGroupSizeZ);
                biolumDiffusionCompute.SetTexture(_clearKernel, _VolumeOutputId, _volumeB);
                DispatchVolumeKernel(_clearKernel, _clearThreadGroupSizeX, _clearThreadGroupSizeY, _clearThreadGroupSizeZ);
                _needsClear = false;
            }

            if (pointCount > 0 && ShouldUploadPointBuffer(pointCount))
            {
                GraphicsBuffer pointWriteBuffer = ResolvePointWriteBuffer();
                if (pointWriteBuffer != null)
                {
                    GraphicsBufferUploadUtility.UploadArray(pointWriteBuffer, _pointUpload, pointCount);
                    _activePointBuffer = pointWriteBuffer;
                    _pointBufferWriteIndex ^= 1;
                }
            }

            BindSharedParameters(_diffuseKernel, halfExtents, worldToLocal, volumeParams, cascadeParams, texelSize, pointCount);
            biolumDiffusionCompute.SetTexture(_diffuseKernel, _VolumeInputId, _volumeA);
            biolumDiffusionCompute.SetTexture(_diffuseKernel, _VolumeOutputId, _volumeB);
            DispatchVolumeKernel(_diffuseKernel, _diffuseThreadGroupSizeX, _diffuseThreadGroupSizeY, _diffuseThreadGroupSizeZ);

            BindSharedParameters(_injectKernel, halfExtents, worldToLocal, volumeParams, cascadeParams, texelSize, pointCount);
            biolumDiffusionCompute.SetTexture(_injectKernel, _VolumeInputId, _volumeB);
            biolumDiffusionCompute.SetTexture(_injectKernel, _VolumeOutputId, _volumeA);
            DispatchVolumeKernel(_injectKernel, _injectThreadGroupSizeX, _injectThreadGroupSizeY, _injectThreadGroupSizeZ);

            PublishGlobals();
            Shader.SetGlobalTexture(_GlobalTextureId, _volumeA);
            Shader.SetGlobalMatrix(_WorldToLocalId, worldToLocal);
            Shader.SetGlobalVector(_HalfExtentsId, halfExtents);
            Shader.SetGlobalVector(_VolumeParamsId, volumeParams);
            Shader.SetGlobalFloat(_GlobalActiveId, pointCount > 0 ? 1f : 0f);
            _lastVolumeCenter = volumeCenter;
            _hasLastVolumeCenter = true;
        }

        public void SlowTick()
        {
            // L19 hop2 LIVE: batch peel SlowTick - biolum diffusion hang headless.
            if (UnityEngine.Application.isBatchMode)
                return;

            if (_dependencyResolveRequested || _playerTransform == null)
            {
                _dependencyResolveRequested = false;
                ResolveDependencies();
            }

            if (_resourceRefreshRequested || _volumeA == null || _volumeB == null || _activePointBuffer == null || !HasValidKernelState())
            {
                EnsureResources();
                if (!HasRequiredResources())
                    return;

                _resourceRefreshRequested = false;
                PublishGlobals();
            }
        }

        private Vector3 ResolveVolumeCenterRuntimePosition()
        {
            return _playerTransform != null ? _playerTransform.position : Vector3.zero;
        }

        private void ResolveDependencies()
        {
            CachePlayerTransformCold(_playerRuntimeContext);
            if (_playerTransform == null && GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform))
                _playerTransform = playerTransform;
        }

        private void CachePlayerTransformCold(IPlayerRuntimeContext playerRuntimeContext)
        {
            if (playerRuntimeContext != null)
                _playerTransform = playerRuntimeContext.PlayerTransform;
        }

        private void EnsureResources()
        {
            if (biolumDiffusionCompute == null || !_supportsComputeShadersCold)
            {
                ResetKernelState();
                return;
            }

            if (_clearKernel < 0 &&
                !TryResolveKernel(
                    "ClearBiolumVolume",
                    out _clearKernel,
                    out _clearThreadGroupSizeX,
                    out _clearThreadGroupSizeY,
                    out _clearThreadGroupSizeZ))
                return;

            if (_diffuseKernel < 0 &&
                !TryResolveKernel(
                    "DiffuseBiolumVolume",
                    out _diffuseKernel,
                    out _diffuseThreadGroupSizeX,
                    out _diffuseThreadGroupSizeY,
                    out _diffuseThreadGroupSizeZ))
                return;

            if (_injectKernel < 0 &&
                !TryResolveKernel(
                    "InjectBiolumPoints",
                    out _injectKernel,
                    out _injectThreadGroupSizeX,
                    out _injectThreadGroupSizeY,
                    out _injectThreadGroupSizeZ))
                return;

            int clampedResolution = Mathf.Clamp(volumeResolution, 32, 64);
            if (_volumeA == null || _volumeA.width != clampedResolution)
            {
                ReleaseVolumeTextures();
                _volumeA = CreateVolumeTexture(clampedResolution, "__HectonBiolumVolumeA");
                _volumeB = CreateVolumeTexture(clampedResolution, "__HectonBiolumVolumeB");
                volumeResolution = clampedResolution;
                _needsClear = true;
            }

            if (_pointBufferA == null)
            {
                _pointBufferA = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BiolumPointGpuData>(MaxTrackedZones); // COLD ALLOC: GraphicsBuffer[32] A - persistent biolum emitter upload buffer for 3D diffusion volume - owner: HectonBiolumDiffusionVolume
            }

            if (_pointBufferB == null)
            {
                _pointBufferB = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<BiolumPointGpuData>(MaxTrackedZones); // COLD ALLOC: GraphicsBuffer[32] B - persistent biolum emitter upload buffer for 3D diffusion volume - owner: HectonBiolumDiffusionVolume
            }

            if (_activePointBuffer == null)
                _activePointBuffer = _pointBufferA;
        }

        private RenderTexture CreateVolumeTexture(int resolution, string textureName)
        {
            RenderTextureDescriptor descriptor = new RenderTextureDescriptor(resolution, resolution)
            {
                dimension = TextureDimension.Tex3D,
                volumeDepth = resolution,
                graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat,
                depthBufferBits = 0,
                msaaSamples = 1,
                useMipMap = false,
                autoGenerateMips = false,
                enableRandomWrite = true,
                sRGB = false
            };

            RenderTexture texture = new RenderTexture(descriptor)
            {
                name = textureName,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            }; // COLD ALLOC: RenderTexture[1] — persistent player-centered 3D biolum diffusion volume — owner: HectonBiolumDiffusionVolume
            texture.Create();
            return texture;
        }

        private int CollectNearbyPoints(Vector3 volumeCenter)
        {
            if (_biolumManager == null)
            {
                _pendingPointUploadHash = 0u;
                _pendingGlowHash = 0u;
                return 0;
            }

            float safeGatherRadius = SanitizeGlowPositive(zoneGatherRadius, 88f);
            int count = _biolumManager.CopyNearbyZonesNonAlloc(volumeCenter, safeGatherRadius, _nearbyZones, _nearbyZoneWeights, includeOcean: true, includeFloor: true);
            int safeCount = math.min(count, MaxTrackedZones);
            int writeCount = 0;
            uint pointHash = FnvOffsetBasis;
            uint glowHash = FnvOffsetBasis;
            for (int i = 0; i < safeCount; i++)
            {
                HectonBiolumZone zone = _nearbyZones[i];
                if (zone == null)
                    continue;

                Vector3 zonePosition = zone.GetZonePosition();
                if (!MathGuard.IsFinite(zonePosition))
                {
                    ReportInvalidGlowInput();
                    continue;
                }

                Color zoneColor = zone.SampleZoneColor().linear;
                float zoneIntensity = SanitizeGlowNonNegative(zone.SampleZoneIntensity());
                float zoneRange = math.max(0.5f, SanitizeGlowNonNegative(zone.SampleZoneRange(), 0.5f));
                float weight = SanitizeGlowNonNegative(_nearbyZoneWeights[i]);
                float weightedIntensity = math.min(SanitizeGlowNonNegative(zoneIntensity * weight), MaxBiolumHdrIntensity);
                Vector4 positionRange = new Vector4(zonePosition.x, zonePosition.y, zonePosition.z, zoneRange);
                Vector4 colorIntensity = new Vector4(
                    math.min(SanitizeGlowNonNegative(zoneColor.r), MaxBiolumHdrIntensity),
                    math.min(SanitizeGlowNonNegative(zoneColor.g), MaxBiolumHdrIntensity),
                    math.min(SanitizeGlowNonNegative(zoneColor.b), MaxBiolumHdrIntensity),
                    weightedIntensity);

                _pointUpload[writeCount] = new BiolumPointGpuData
                {
                    PositionRange = positionRange,
                    ColorIntensity = colorIntensity
                };
                pointHash = MixGlowPointHash(pointHash, positionRange, colorIntensity);

                if (writeCount < MaxGlowShaderPoints)
                {
                    _glowPointPositionRangeUpload[writeCount] = positionRange;
                    _glowPointColorIntensityUpload[writeCount] = colorIntensity;
                    glowHash = MixGlowPointHash(glowHash, positionRange, colorIntensity);
                }

                writeCount++;
            }

            _pendingPointUploadHash = writeCount > 0 ? pointHash : 0u;
            _pendingGlowHash = writeCount > 0 ? glowHash : 0u;
            return writeCount;
        }

        private bool ShouldUploadPointBuffer(int pointCount)
        {
            int safeCount = math.min(math.max(pointCount, 0), MaxTrackedZones);
            uint pointHash = safeCount > 0 ? _pendingPointUploadHash : 0u;
            if (_lastUploadedPointCount == safeCount && _lastUploadedPointHash == pointHash)
                return false;

            _lastUploadedPointCount = safeCount;
            _lastUploadedPointHash = pointHash;
            return safeCount > 0;
        }

        private void PublishGlowPointGlobals(int pointCount, bool force = false)
        {
            int glowCount = Mathf.Clamp(pointCount, 0, MaxGlowShaderPoints);
            uint glowHash = glowCount > 0 ? _pendingGlowHash : 0u;
            if (!force && _lastPublishedGlowCount == glowCount && _lastPublishedGlowHash == glowHash)
                return;

            if (glowCount > 0 && (force || _lastPublishedGlowHash != glowHash))
            {
                Shader.SetGlobalVectorArray(_GlowPointPositionRangeId, _glowPointPositionRangeUpload);
                Shader.SetGlobalVectorArray(_GlowPointColorIntensityId, _glowPointColorIntensityUpload);
            }

            Shader.SetGlobalVector(_GlowPointParamsId, new Vector4(glowCount, GlowPointSonarPulseGain, 0f, 0f));
            _lastPublishedGlowCount = glowCount;
            _lastPublishedGlowHash = glowHash;
        }

        private void ReportInvalidGlowInput()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastInvalidGlowTelemetryFrame == frame)
                return;

            _lastInvalidGlowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishMathGuardInvalidNumber(GlowPointInvalidNumberHash);
        }

        private float SanitizeGlowNonNegative(float value, float fallback = 0f)
        {
            if (math.isfinite(value))
                return math.max(0f, value);

            ReportInvalidGlowInput();
            return math.max(0f, fallback);
        }

        private float SanitizeGlowPositive(float value, float fallback)
        {
            if (math.isfinite(value) && value > 0f)
                return value;

            ReportInvalidGlowInput();
            return math.max(0.001f, fallback);
        }

        private static uint MixGlowPointHash(uint hash, Vector4 positionRange, Vector4 colorIntensity)
        {
            hash = MixHash(hash, QuantizeHashComponent(positionRange.x, GlowPositionHashScale));
            hash = MixHash(hash, QuantizeHashComponent(positionRange.y, GlowPositionHashScale));
            hash = MixHash(hash, QuantizeHashComponent(positionRange.z, GlowPositionHashScale));
            hash = MixHash(hash, QuantizeHashComponent(positionRange.w, GlowRangeHashScale));
            hash = MixHash(hash, QuantizeHashComponent(colorIntensity.x, GlowColorHashScale));
            hash = MixHash(hash, QuantizeHashComponent(colorIntensity.y, GlowColorHashScale));
            hash = MixHash(hash, QuantizeHashComponent(colorIntensity.z, GlowColorHashScale));
            hash = MixHash(hash, QuantizeHashComponent(colorIntensity.w, GlowIntensityHashScale));
            return hash;
        }

        private static int QuantizeHashComponent(float value, float scale)
        {
            if (!math.isfinite(value))
                return 0;

            float scaled = math.clamp(value * scale, HashClampMin, HashClampMax);
            return (int)math.round(scaled);
        }

        private static uint MixHash(uint hash, int value)
        {
            unchecked
            {
                hash ^= (uint)value;
                return hash * FnvPrime;
            }
        }

        private void BindSharedParameters(
            int kernelIndex,
            Vector4 halfExtents,
            Matrix4x4 worldToLocal,
            Vector4 volumeParams,
            Vector4 cascadeParams,
            Vector4 texelSize,
            int pointCount)
        {
            if (biolumDiffusionCompute == null || kernelIndex < 0 || _activePointBuffer == null)
                return;

            biolumDiffusionCompute.SetVector(_HalfExtentsId, halfExtents);
            biolumDiffusionCompute.SetMatrix(_WorldToLocalId, worldToLocal);
            biolumDiffusionCompute.SetVector(_VolumeParamsId, volumeParams);
            biolumDiffusionCompute.SetVector(_CascadeParamsId, cascadeParams);
            biolumDiffusionCompute.SetVector(_TexelSizeId, texelSize);
            biolumDiffusionCompute.SetInt(_PointCountId, pointCount);
            biolumDiffusionCompute.SetBuffer(kernelIndex, _PointBufferId, _activePointBuffer);
        }

        private GraphicsBuffer ResolvePointWriteBuffer()
        {
            GraphicsBuffer writeBuffer = _pointBufferWriteIndex == 0 ? _pointBufferA : _pointBufferB;
            if (writeBuffer != null)
                return writeBuffer;

            return ReferenceEquals(_activePointBuffer, _pointBufferA) ? _pointBufferB : _pointBufferA;
        }

        private void DispatchVolumeKernel(int kernelIndex, uint threadGroupSizeX, uint threadGroupSizeY, uint threadGroupSizeZ)
        {
            if (biolumDiffusionCompute == null || kernelIndex < 0)
                return;

            int safeResolution = math.clamp(volumeResolution, 1, 64);
            int dispatchX = ResolveDispatchCount(safeResolution, threadGroupSizeX);
            int dispatchY = ResolveDispatchCount(safeResolution, threadGroupSizeY);
            int dispatchZ = ResolveDispatchCount(safeResolution, threadGroupSizeZ);
            if (dispatchX <= 0 || dispatchY <= 0 || dispatchZ <= 0)
                return;

            biolumDiffusionCompute.Dispatch(kernelIndex, dispatchX, dispatchY, dispatchZ);
        }

        private bool TryResolveKernel(string kernelName, out int kernelIndex, out uint sizeX, out uint sizeY, out uint sizeZ)
        {
            kernelIndex = -1;
            sizeX = 0u;
            sizeY = 0u;
            sizeZ = 0u;
            if (biolumDiffusionCompute == null || !_supportsComputeShadersCold)
                return false;

            int resolvedKernelIndex = -1;
            try
            {
                if (!biolumDiffusionCompute.HasKernel(kernelName))
                    return false;

                resolvedKernelIndex = biolumDiffusionCompute.FindKernel(kernelName);
                if (resolvedKernelIndex < 0)
                    return false;
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }

            if (!TryCacheKernelThreadGroupSizes(resolvedKernelIndex, out sizeX, out sizeY, out sizeZ))
                return false;

            kernelIndex = resolvedKernelIndex;
            return true;
        }

        private bool TryCacheKernelThreadGroupSizes(int kernelIndex, out uint sizeX, out uint sizeY, out uint sizeZ)
        {
            sizeX = 0u;
            sizeY = 0u;
            sizeZ = 0u;
            if (biolumDiffusionCompute == null || kernelIndex < 0)
                return false;

            try
            {
                if (!biolumDiffusionCompute.IsSupported(kernelIndex))
                    return false;

                biolumDiffusionCompute.GetKernelThreadGroupSizes(kernelIndex, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return false;
            }
            catch (System.InvalidOperationException)
            {
                return false;
            }
            catch (System.ArgumentException)
            {
                return false;
            }
            catch (MissingReferenceException)
            {
                return false;
            }
            catch (UnityException)
            {
                return false;
            }
            if (IsPortableThreadGroup(sizeX, sizeY, sizeZ))
                return true;

            sizeX = 0u;
            sizeY = 0u;
            sizeZ = 0u;
            ReportInvalidGlowInput();
            return false;
        }

        private static int ResolveDispatchCount(int resolution, uint threadGroupSize)
        {
            if (resolution <= 0 || threadGroupSize == 0u || threadGroupSize > int.MaxValue)
                return 0;

            long groups = ((long)resolution + (long)threadGroupSize - 1L) / (long)threadGroupSize;
            if (groups <= 0L || groups > MaxDispatchGroupsPerDimension)
                return 0;

            return (int)groups;
        }

        private static bool IsPortableThreadGroup(uint sizeX, uint sizeY, uint sizeZ)
        {
            if (sizeX == 0u || sizeY == 0u || sizeZ == 0u)
                return false;

            if (sizeX > MaxPortableComputeThreadsPerGroup || sizeY > MaxPortableComputeThreadsPerGroup || sizeZ > MaxPortableComputeThreadsPerGroup)
                return false;

            ulong xy = (ulong)sizeX * sizeY;
            if (xy > MaxPortableComputeThreadsPerGroup)
                return false;

            return xy * sizeZ <= MaxPortableComputeThreadsPerGroup;
        }

        private bool HasValidKernelState()
        {
            return _clearKernel >= 0 &&
                   _diffuseKernel >= 0 &&
                   _injectKernel >= 0 &&
                   IsPortableThreadGroup(_clearThreadGroupSizeX, _clearThreadGroupSizeY, _clearThreadGroupSizeZ) &&
                   IsPortableThreadGroup(_diffuseThreadGroupSizeX, _diffuseThreadGroupSizeY, _diffuseThreadGroupSizeZ) &&
                   IsPortableThreadGroup(_injectThreadGroupSizeX, _injectThreadGroupSizeY, _injectThreadGroupSizeZ);
        }

        private bool HasRequiredResources()
        {
            return _volumeA != null &&
                   _volumeB != null &&
                   _activePointBuffer != null &&
                   HasValidKernelState();
        }

        private void ResetKernelState()
        {
            _clearKernel = -1;
            _diffuseKernel = -1;
            _injectKernel = -1;
            _clearThreadGroupSizeX = 0u;
            _clearThreadGroupSizeY = 0u;
            _clearThreadGroupSizeZ = 0u;
            _diffuseThreadGroupSizeX = 0u;
            _diffuseThreadGroupSizeY = 0u;
            _diffuseThreadGroupSizeZ = 0u;
            _injectThreadGroupSizeX = 0u;
            _injectThreadGroupSizeY = 0u;
            _injectThreadGroupSizeZ = 0u;
        }

        private float ResolveCascadeTimeSeconds(float safeDeltaTime)
        {
            ITickDispatcher dispatcher = _dispatcher;
            if (dispatcher != null)
            {
                H8TimeSnapshot snapshot = dispatcher.TimeSnapshot;
                if (snapshot.Time >= 0d && !double.IsNaN(snapshot.Time) && !double.IsInfinity(snapshot.Time))
                {
                    _cascadeTimeSeconds = snapshot.Time;
                    return (float)(_cascadeTimeSeconds % CascadeTimeModulo);
                }
            }

            _cascadeTimeSeconds += safeDeltaTime;
            if (double.IsNaN(_cascadeTimeSeconds) || double.IsInfinity(_cascadeTimeSeconds) || _cascadeTimeSeconds < 0d)
                _cascadeTimeSeconds = 0d;

            return (float)(_cascadeTimeSeconds % CascadeTimeModulo);
        }

        private static float SanitizeDelta(float deltaTime)
        {
            if (!math.isfinite(deltaTime) || deltaTime <= 0f)
                return 0f;

            return math.min(deltaTime, 0.25f);
        }

        private static float EstimateLength3D(Vector3 value)
        {
            float ax = math.abs(value.x);
            float ay = math.abs(value.y);
            float az = math.abs(value.z);
            float maxAxis = math.max(ax, math.max(ay, az));
            float minAxis = math.min(ax, math.min(ay, az));
            float midAxis = ax + ay + az - maxAxis - minAxis;
            return maxAxis + (midAxis * 0.375f) + (minAxis * 0.25f);
        }

        private void PublishGlobals()
        {
            if (_volumeA != null)
                Shader.SetGlobalTexture(_GlobalTextureId, _volumeA);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || _dispatcher == null)
                return;

            if (!_registered)
                _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (!_registeredSlowTick)
                _registeredSlowTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (_registeredSlowTick)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredSlowTick = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registered = false;
            }
        }

        private void CacheRegistryServicesCold()
        {
            if (_biolumManager == null)
                _biolumManager = GlobalRegistry.BiolumManager;

            if (_dispatcher == null)
                _dispatcher = GlobalRegistry.Dispatcher;

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            CachePlayerTransformCold(_playerRuntimeContext);
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _supportsComputeShadersCold = SystemInfo.supportsComputeShaders;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private void ReleaseResources()
        {
            ReleaseVolumeTextures();
            if (_pointBufferA != null)
            {
                _pointBufferA.Release();
                _pointBufferA = null;
            }

            if (_pointBufferB != null)
            {
                _pointBufferB.Release();
                _pointBufferB = null;
            }

            _activePointBuffer = null;
            _pointBufferWriteIndex = 0;

            _lastUploadedPointCount = -1;
            _lastUploadedPointHash = 0u;
            _hasLastVolumeCenter = false;
            _lastVolumeCenter = default;
        }

        private void ReleaseVolumeTextures()
        {
            if (_volumeA != null)
            {
                _volumeA.Release();
                if (Application.isPlaying)
                    Destroy(_volumeA);
                else
                    DestroyImmediate(_volumeA);
                _volumeA = null;
            }

            if (_volumeB != null)
            {
                _volumeB.Release();
                if (Application.isPlaying)
                    Destroy(_volumeB);
                else
                    DestroyImmediate(_volumeB);
                _volumeB = null;
            }
        }
    }
}
