using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using VfxSparkRequestSignal = Hecton8.Tools.ToolKinematics.Contracts.VfxSparkRequestSignal;

namespace Hecton8.VFX.Debris
{
    /// <summary>
    /// GPU-only rock chip feedback for voxel SDF carve events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarveDebrisComputeRenderer : MonoBehaviour,
        ILateFrameTickable,
        ISlowTickable,
        IDebrisComputeService,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener
    {
        private const int MaxCarveDebrisCount = ShinobuDeltaCrusher.MaximumQualityDebrisCap;
        private static readonly int MinQualityActiveCarveDebrisCount = ShinobuDeltaCrusher.ResolveDebrisCap(0f, MaxCarveDebrisCount);
        private const int ThreadGroupPortableMaxSize = 256;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const int BlackBoxCapacity = 300;
        private const int JobStateLength = ShinobuDeltaCrusher.CarveDebrisJobStateLength;
        private const int JobStateActiveIndex = 0;
        private const int JobStateInjectedIndex = 1;
        private const int JobStateDirtyMinIndex = 2;
        private const int JobStateDirtyMaxIndex = 3;
        private const int JobStateFlagsIndex = 4;
        private const int MinQualityParticlesPerCarve = 16;
        private const int MiddleQualityParticlesPerCarve = 48;
        private const int MaxQualityParticlesPerCarve = 128;
        private const int MaxCarveSignalsPerFrame = 32;
        private const int MaxCarveSignalScanPerFrame = 64;
        private const int MaxDebrisSpawnSignalScanPerFrame = 64;
        private const int MaxVfxSparkSignalScanPerFrame = VfxSparkRequestSignal.MaxFrameSignals;
        private const int SparkToolGateSlotCount = 4;
        private const int MinimumSparkParticles = 3;
        // A cutter biting continuously publishes a spark every simulation frame. Emitting one burst per
        // signal would be ~60 bursts/second per tool, which reads as noise and starves carve debris out of
        // the shared particle pool. ~11.8 Hz per tool is a readable spark stream with a bounded pool cost.
        private const float SparkEmitIntervalSeconds = 0.085f;
        private const float SparkMinimumIntensity01 = 0.08f;
        private const float SparkParticleShare = 0.22f;
        private const float MinimumSparkSpawnRadiusMeters = 0.03f;
        private const float MaximumSparkSpawnRadiusMeters = 0.075f;
        private const float SparkSpeedScaleMin = 1.1f;
        private const float SparkSpeedScaleMax = 2.1f;
        private const float SparkLife01 = 0.42f;
        private const int TelemetryPublishStride = 30;
        private const int GlobalSdfRefreshStrideFrames = 4;
        private const int MissingRegistryRefreshStrideFrames = 30;
        private const float MinimumCarveSpawnRadiusMeters = 0.05f;
        private const float StressRecycleThreshold01 = 0.9f;
        private const float StressRecycleLifetimeMultiplier = 4f;
        private const float DefaultCarveDebrisBounce = ShinobuDeltaCrusher.DefaultBounce;
        private const float CarveDebrisSleepSpeedSq = ShinobuDeltaCrusher.DefaultSleepSpeedSq;
        private const string DebrisShaderName = "Hecton8/VFX/CarveDebrisIndirect";
#if UNITY_EDITOR
        private const string FluidAdvectionComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute";
#endif
        private const uint TelemetryContextHash = 0x56465844u; // VFXD
        private const uint DebrisBlackBoxDumpMagic = 0x44584656u; // "VFXD" little-endian
        private const uint ActiveCountTelemetryHash = 0x43444252u; // CDBR
        private const uint InvalidStateFlag = 1u;
        private const uint SdfActiveFlag = 1u << 2;
        private const uint FlowActiveFlag = 1u << 3;
        private const uint StressRecycleFlag = 1u << 4;
        private const uint WakeActiveFlag = 1u << 5;
        private const uint SparkActiveFlag = 1u << 6;
        private const string DumpPath = "Docs/AgentLogs/Dump_SHINOBU_05_DEBRIS_PHYSICS_FAKE.h8dump";
        private const SystemID VaultOwnerSystem = SystemID.Vfx;

        private static readonly int CarveDebrisReadId = Shader.PropertyToID("_CarveDebrisRead");
        private static readonly int CarveDebrisWriteId = Shader.PropertyToID("_CarveDebrisWrite");
        private static readonly int CarveDebrisVelocityReadId = Shader.PropertyToID("_CarveDebrisVelocityRead");
        private static readonly int CarveDebrisVelocityWriteId = Shader.PropertyToID("_CarveDebrisVelocityWrite");
        private static readonly int CarveDebrisVisibleIndicesId = Shader.PropertyToID("_CarveDebrisVisibleIndices");
        private static readonly int CarveDebrisIndirectArgsId = Shader.PropertyToID("_CarveDebrisIndirectArgs");
        private static readonly int CarveDebrisCountsId = Shader.PropertyToID("_CarveDebrisCounts");
        private static readonly int CarveDebrisParamsId = Shader.PropertyToID("_CarveDebrisParams");
        private static readonly int CarveDebrisForcesId = Shader.PropertyToID("_CarveDebrisForces");
        private static readonly int CarveDebrisAupShiftDeltaId = Shader.PropertyToID("_CarveDebrisAupShiftDelta");
        private static readonly int CarveDebrisCameraParamsId = Shader.PropertyToID("_CarveDebrisCameraParams");
        private static readonly int CarveDebrisCullParamsId = Shader.PropertyToID("_CarveDebrisCullParams");
        private static readonly int CarveDebrisDrawArgsParamsId = Shader.PropertyToID("_CarveDebrisDrawArgsParams");
        private static readonly int CarveDebrisMaterialParamsId = Shader.PropertyToID("_CarveDebrisMaterialParams");
        private static readonly int CarveDebrisMotionParamsId = Shader.PropertyToID("_CarveDebrisMotionParams");
        private static readonly int DebrisBufferId = Shader.PropertyToID("_DebrisBuffer");
        private static readonly int DebrisPhysicsBufferId = Shader.PropertyToID("_DebrisPhysicsBuffer");
        private static readonly int AbyssalFlowFieldResultId = Shader.PropertyToID("_AbyssalFlowFieldResult");
        private static readonly int AbyssalFlowFieldTextureId = Shader.PropertyToID("_AbyssalFlowFieldTexture");
        private static readonly int AbyssalGridResolutionId = Shader.PropertyToID("_AbyssalGridResolution");
        private static readonly int AbyssalFlowCenterId = Shader.PropertyToID("_AbyssalFlowCenter");
        private static readonly int AbyssalFlowSpacingId = Shader.PropertyToID("_AbyssalFlowSpacing");
        private static readonly int AbyssalFlowTextureParamsId = Shader.PropertyToID("_AbyssalFlowTextureParams");
        private static readonly int AbyssalFlowTextureActiveId = Shader.PropertyToID("_AbyssalFlowTextureActive");
        private static readonly int DynamicWakesId = Shader.PropertyToID("_DynamicWakes");
        private static readonly int DynamicWakeVectorsId = Shader.PropertyToID("_DynamicWakeVectors");
        private static readonly int DynamicWakeParamsId = Shader.PropertyToID("_DynamicWakeParams");
        private static readonly int VoxelSdfTexture3DId = Shader.PropertyToID("_VoxelSdfTexture3D");
        private static readonly int VoxelSdfWorldToLocalId = Shader.PropertyToID("_VoxelSdfWorldToLocal");
        private static readonly int VoxelSdfInvDoubleHalfExtentsId = Shader.PropertyToID("_VoxelSdfInvDoubleHalfExtents");
        private static readonly int HectonCaveVoxelSdfTexId = Shader.PropertyToID("_HectonCaveVoxelSdfTex");
        private static readonly int HectonCaveVoxelActiveId = Shader.PropertyToID("_HectonCaveVoxelActive");
        private static readonly int HectonCaveVoxelWorldToLocalId = Shader.PropertyToID("_HectonCaveVoxelWorldToLocal");
        private static readonly int HectonCaveVoxelInvDoubleHalfExtentsId = Shader.PropertyToID("_HectonCaveVoxelInvDoubleHalfExtents");
        private static readonly int FluidAdvectionParamsId = Shader.PropertyToID("_FluidAdvectionParams");
        private static readonly int FluidAdvectionSdfParamsId = Shader.PropertyToID("_FluidAdvectionSdfParams");

        [Header("Compute")]
        [SerializeField] private ComputeShader fluidAdvectionCompute;
        [SerializeField, Min(0.1f)] private float particleLifetimeSeconds = 5f;
        [SerializeField, Min(0f)] private float spawnRadiusScale = 0.85f;
        [SerializeField, Min(0f)] private float initialVelocityMetersPerSecond = 4.5f;
        [SerializeField, Min(0f)] private float dragToFlow = 0.18f;
        [SerializeField] private Vector3 gravityMetersPerSecondSq = new Vector3(0f, -5.25f, 0f);

        [Header("SDF / Flow")]
        [SerializeField] private Texture3D voxelSdfTexture3D;
        [SerializeField] private Matrix4x4 voxelSdfWorldToLocal = Matrix4x4.identity;
        [SerializeField] private Vector4 voxelSdfInvDoubleHalfExtents = Vector4.zero;
        [SerializeField, Range(0f, 1f)] private float solidDensityThreshold = 0.5f;
        [SerializeField] private Texture3D abyssalFlowTextureOverride;
        [SerializeField, Tooltip("Authored 1x1x1 clear Texture3D bound when SDF/flow is inactive. Runtime Texture3D synthesis is forbidden.")]
        private Texture3D emptySdfFlowTexture3D;

        [Header("Render")]
        [SerializeField] private Mesh debrisMesh;
        [SerializeField] private Material debrisMaterial;
        [SerializeField] private Camera renderCamera;
        [SerializeField] private Bounds drawBounds = new Bounds(Vector3.zero, new Vector3(400f, 400f, 400f));
        [SerializeField, Min(0f)] private float renderDistanceMeters = 220f;
        [SerializeField, Min(0.01f)] private float minRockScale = 0.035f;
        [SerializeField, Min(0.01f)] private float maxRockScale = 0.18f;
        [SerializeField] private int renderLayer;
        [SerializeField] private ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off;

        private VaultGenerationHandle<float4> _debrisPositionsHandle;
        private VaultGenerationHandle<float4> _debrisVelocitiesHandle;
        private VaultGenerationHandle<CarveDebrisRequest> _carveRequestsHandle;
        private VaultGenerationHandle<int> _jobStateHandle;
        private VaultGenerationHandle<CarveDebrisTelemetryEntry> _blackBoxHandle;
        private IDataVault _registryDataVault;
        private IDataVault _dataVault;
        private GraphicsBuffer _positionBufferA;
        private GraphicsBuffer _positionBufferB;
        private GraphicsBuffer _velocityBufferA;
        private GraphicsBuffer _velocityBufferB;
        private GraphicsBuffer _visibleIndicesBuffer;
        private GraphicsBuffer _indirectArgsBuffer;
        private GraphicsBuffer _emptyFlowBuffer;
        private Texture _cachedGlobalSdfTexture;
        private Texture3D _emptyTexture3D;
        private Material _ownedMaterial;
        private Material _ownedMaterialSource;
        private Material _boundRenderMaterial;
        private GraphicsBuffer _boundVisibleIndicesBuffer;
        private Vector4 _boundMaterialParams;
        private Vector4 _boundMotionParams;
        private bool _boundMaterialParamsValid;
        private bool _boundMotionParamsValid;
        private IAbyssalFlowGpuReadModel _abyssalFlowGpuReadModel;
        private int _advectKernel = -1;
        private int _clearArgsKernel = -1;
        private int _cullKernel = -1;
        private int _advectThreadGroupSize;
        private int _clearArgsThreadGroupSize;
        private int _cullThreadGroupSize;
        private int _lastActiveCapacity = MaxCarveDebrisCount;
        private int _nextGlobalSdfRefreshFrame;
        private int _nextMissingRegistryRefreshFrame;
        private int _bufferParity;
        private int _activeMirrorCount;
        private int _blackBoxCursor;
        private int _lastTelemetryFrame = -1;
        private uint _lastProcessedAupShiftFrameId;
        private uint _frameSequence;
        private uint _cachedDrawIndexCount;
        private uint _cachedDrawIndexStart;
        private uint _cachedDrawBaseVertex;
        private float3 _pendingAupShift;
        private float3 _lastAppliedAupShift;
        private Vector3 _configuredGravityMetersPerSecondSq;
        private Matrix4x4 _cachedGlobalSdfWorldToLocal = Matrix4x4.identity;
        private Vector4 _cachedGlobalSdfInvDoubleHalfExtents;
        private Mesh _cachedDrawMesh;
        private float _lastDeltaTime = 1f / 60f;
        private float _cachedSystemStress01;
        private float _cachedGlobalQualityWeight01 = 1f;
        private float _qualityPressure01;
        private float _visualOverkill01 = 1f;
        private float _cachedGlobalSdfActive;
        private float _configuredDebrisBounce = DefaultCarveDebrisBounce;
        private int _configuredMaxActiveDebris = MaxCarveDebrisCount;
        private bool _gpuReady;
        private bool _blackBoxDumped;
        private bool _lastFlowActive;
        private bool _lastSdfActive;
        private bool _lastWakeActive;
        private bool _cachedDrawMeshValid;
        private bool _hotSwapRegistered;
        private bool _computeServiceRegistered;
        private bool _runtimeOwnerAborted;
        private bool _lateFrameRegistered;
        private bool _slowTickRegistered;
        private bool _pendingVisualSync;
        private bool _pendingDebrisUpload;
        private bool _insideDebrisVisualSync;
        private bool _coldSupportsComputeShaders;
        private bool _fallbackRenderResourceRepairRequested;
        private float _pendingVisualDeltaTime;
        private float _pendingVisualQualityPressure01;
        private int _pendingVisualActiveCapacity;
        private int _pendingDebrisUploadStart;
        private int _pendingDebrisUploadEnd;
        private Vector4 _cachedAbyssalFlowTextureParams;
        private Vector4 _cachedAbyssalFlowCenter;
        // COLD ALLOC: uint[4] - spark rate-gate key per concurrent tool, allocated once at construction - owner: CarveDebrisComputeRenderer
        private readonly uint[] _sparkGateToolHash = new uint[SparkToolGateSlotCount];
        // COLD ALLOC: float[4] - spark rate-gate cooldown seconds per concurrent tool - owner: CarveDebrisComputeRenderer
        private readonly float[] _sparkGateCooldownSeconds = new float[SparkToolGateSlotCount];

        private void Awake()
        {
            if (Application.isPlaying && !TryRegisterComputeService())
                return;

            CacheGraphicsCapabilitiesCold();
            EnsureFallbackRenderResources();
        }

        private void OnEnable()
        {
            if (Application.isPlaying && !TryRegisterComputeService())
                return;

            CacheGraphicsCapabilitiesCold();
            EnsureFallbackRenderResources();
            if (!Application.isPlaying)
                TryRegisterComputeService();
            TryRegisterHotSwapListener();
            TryRegisterLateFrameTick();
            TryRegisterSlowTick();
            TryEnsureGpuState();
        }

        private void Start()
        {
            if (!TryRegisterComputeService())
                return;

            CacheGraphicsCapabilitiesCold();
            TryRegisterHotSwapListener();
            TryRegisterLateFrameTick();
            TryRegisterSlowTick();
            TryEnsureGpuState();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted)
                return;

            TryUnregisterComputeService();
            TryUnregisterHotSwapListener();
            TryUnregisterLateFrameTick();
            TryUnregisterSlowTick();

            ReleaseGpuState();
        }

        private void Reset()
        {
            drawBounds = new Bounds(Vector3.zero, new Vector3(400f, 400f, 400f));
            renderLayer = gameObject.layer;
            InvalidateDrawMeshCache();
#if UNITY_EDITOR
            ResolveEditorAssets();
#endif
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            renderLayer = math.clamp(renderLayer, 0, 31);
            InvalidateDrawMeshCache();
            ResolveEditorAssets();
        }

        private void ResolveEditorAssets()
        {
            if (fluidAdvectionCompute == null)
                fluidAdvectionCompute = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(FluidAdvectionComputeAssetPath);
        }
#endif

        /// <inheritdoc />
        public bool IsInitialized => _gpuReady && IsGpuStateValid();

        /// <inheritdoc />
        public int ActiveDebrisCount => _activeMirrorCount;

        /// <inheritdoc />
        public int ActiveParticleCapacity => ResolveActiveCapacity(_cachedGlobalQualityWeight01, _configuredMaxActiveDebris);

        /// <inheritdoc />
        public float QualityPressure01 => _qualityPressure01;

        /// <inheritdoc />
        public void ClearGpuDebris()
        {
            _activeMirrorCount = 0;
            _pendingAupShift = default;
            _lastAppliedAupShift = default;
            _lastFlowActive = false;
            _lastSdfActive = false;
            _lastWakeActive = false;
            ResetSparkGates();
            if (IsGpuStateValid() &&
                TryResolveVaultBuffers(
                    out var debrisPositions,
                    out var debrisVelocities,
                    out var carveRequests,
                    out var jobState,
                    out var blackBox))
            {
                ClearMirrorsAndUpload(debrisPositions, debrisVelocities, carveRequests, jobState, blackBox);
            }
        }

        private void AdvanceDebrisVisualState(float deltaTime)
        {
            if (!enabled)
                return;

            float dt = math.clamp(deltaTime, 0.0001f, 0.0666667f);
            _lastDeltaTime = dt;
            _cachedSystemStress01 = ResolveSystemStress01();
            _cachedGlobalQualityWeight01 = ResolveGlobalQualityWeight01();
            RefreshQualityPolicy(_cachedGlobalQualityWeight01);
            if (!_gpuReady || !IsGpuStateValid())
            {
                QueueVisualSync(dt, _qualityPressure01, ResolveActiveCapacity(_cachedGlobalQualityWeight01, _configuredMaxActiveDebris));
                return;
            }

            if (!TryResolveVaultBuffers(
                    out var debrisPositions,
                    out var debrisVelocities,
                    out var carveRequests,
                    out var jobState,
                    out var blackBox))
            {
                _gpuReady = false;
                QueueVisualSync(dt, _qualityPressure01, ResolveActiveCapacity(_cachedGlobalQualityWeight01, _configuredMaxActiveDebris));
                return;
            }

            RefreshDebrisTuningFromVault(jobState);
            int activeCapacity = ResolveActiveCapacity(_cachedGlobalQualityWeight01, _configuredMaxActiveDebris);
            ApplyCapacityShed(activeCapacity, debrisPositions, debrisVelocities);
            DrainAupShiftSignals(jobState);
            if (_activeMirrorCount > 0)
                AgeMirror(dt, activeCapacity, ResolveLifetimeRcp(), debrisPositions, jobState);
            else
                ResetFrameJobState(activeCapacity, jobState);

            int queuedCarves = DrainCarveSignals(_cachedGlobalQualityWeight01, activeCapacity, debrisPositions, debrisVelocities, carveRequests, jobState);
            WriteBlackBox(queuedCarves, jobState.IsCreated ? jobState[JobStateInjectedIndex] : 0, _qualityPressure01, jobState, blackBox);
            QueueVisualSync(dt, _qualityPressure01, activeCapacity);
            _frameSequence++;
        }

        public void LateFrameTick()
        {
            AdvanceDebrisVisualState(SystemDispatcher.CurrentFrameDeltaTime);

            if (!_pendingVisualSync)
                return;

            float dt = _pendingVisualDeltaTime;
            float qualityPressure01 = _pendingVisualQualityPressure01;
            int activeCapacity = _pendingVisualActiveCapacity;
            _pendingVisualSync = false;

            if (!_gpuReady || !IsGpuStateValid())
            {
                _gpuReady = false;
                TryUnregisterLateFrameTick();
                return;
            }

            _insideDebrisVisualSync = true;
            FlushPendingDebrisUploads();
            DispatchGpu(dt, qualityPressure01, activeCapacity);
            RenderDebris();
            _insideDebrisVisualSync = false;
            if (!_pendingDebrisUpload && !ShouldKeepDebrisLateFrameRegistered())
                TryUnregisterLateFrameTick();
        }

        private void TryRegisterLateFrameTick()
        {
            if (_lateFrameRegistered)
                return;

            _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterLateFrameTick()
        {
            if (!_lateFrameRegistered)
                return;

            GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
            _lateFrameRegistered = false;
        }

        private void TryRegisterSlowTick()
        {
            if (_slowTickRegistered || !Application.isPlaying)
                return;

            _slowTickRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregisterSlowTick()
        {
            if (!_slowTickRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _slowTickRegistered = false;
        }

        public void SlowTick()
        {
            CacheGraphicsCapabilitiesCold();
            RefreshMissingRegistryServicesIfNeeded();
            RefreshAbyssalFlowOverrideGlobalsCold();
            RefreshGlobalSdfCacheCold();
            FlushFallbackRenderResourceRepairSlow();

            if (!_gpuReady || !IsGpuStateValid())
                TryEnsureGpuState();

            if (_gpuReady && (ShouldKeepDebrisLateFrameRegistered() || _pendingVisualSync || _pendingDebrisUpload))
                TryRegisterLateFrameTick();
        }

        private bool ShouldKeepDebrisLateFrameRegistered()
        {
            return isActiveAndEnabled && _gpuReady && IsGpuStateValid();
        }

        private void QueueVisualSync(float dt, float qualityPressure01, int activeCapacity)
        {
            _pendingVisualDeltaTime = math.clamp(dt, 0.0001f, 0.0666667f);
            _pendingVisualQualityPressure01 = math.saturate(qualityPressure01);
            _pendingVisualActiveCapacity = math.clamp(activeCapacity, MinQualityActiveCarveDebrisCount, MaxCarveDebrisCount);
            _pendingVisualSync = true;
            TryRegisterLateFrameTick();
        }

        private bool TryRegisterComputeService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_computeServiceRegistered)
                return true;

            if (Application.isPlaying && TryAbortForUsableExistingRuntime())
                return false;

            IDebrisComputeService registered = GlobalRegistry.DebrisCompute;
            if (!ReferenceEquals(registered, null) && !ReferenceEquals(registered, this))
            {
                CarveDebrisComputeRenderer staleRenderer = registered as CarveDebrisComputeRenderer;
                if (ReferenceEquals(staleRenderer, null))
                {
                    if (Application.isPlaying)
                    {
                        _runtimeOwnerAborted = true;
                        Destroy(gameObject);
                    }

                    return false;
                }

                GlobalRegistry.UnregisterDebrisComputeService(registered);
                staleRenderer._computeServiceRegistered = false;
            }

            if (Application.isPlaying && TryAbortForUsableExistingRuntime())
                return false;

            GlobalRegistry.RegisterDebrisComputeService(this);
            _computeServiceRegistered = ReferenceEquals(GlobalRegistry.DebrisCompute, this);
            _runtimeOwnerAborted = Application.isPlaying && !_computeServiceRegistered;
            return _computeServiceRegistered;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            IDebrisComputeService registered = GlobalRegistry.DebrisCompute;
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsDebrisComputeRuntimeUsable(registered))
            {
                _runtimeOwnerAborted = true;
                Destroy(gameObject);
                return true;
            }

            CarveDebrisComputeRenderer staleRenderer = registered as CarveDebrisComputeRenderer;
            if (!ReferenceEquals(staleRenderer, null))
            {
                GlobalRegistry.UnregisterDebrisComputeService(registered);
                staleRenderer._computeServiceRegistered = false;
            }

            return false;
        }

        private static bool IsDebrisComputeRuntimeUsable(IDebrisComputeService service)
        {
            if (ReferenceEquals(service, null))
                return false;

            CarveDebrisComputeRenderer renderer = service as CarveDebrisComputeRenderer;
            return ReferenceEquals(renderer, null) ||
                   (renderer != null &&
                    renderer._computeServiceRegistered &&
                    renderer.isActiveAndEnabled &&
                    !renderer._runtimeOwnerAborted);
        }

        private void TryUnregisterComputeService()
        {
            if (!_computeServiceRegistered)
                return;

            GlobalRegistry.UnregisterDebrisComputeService(this);
            _computeServiceRegistered = false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);

            RefreshCachedRegistryServices();
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void RefreshCachedRegistryServices()
        {
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.DataVault, GlobalRegistry.DataVault);
            ApplyRegistryServiceRebind(GlobalRegistryServiceSlot.FluidRuntime, GlobalRegistry.AbyssalFlowGpu);
            _nextMissingRegistryRefreshFrame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex + MissingRegistryRefreshStrideFrames;
        }

        private void RefreshMissingRegistryServicesIfNeeded()
        {
            if (_registryDataVault != null && _abyssalFlowGpuReadModel != null)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < _nextMissingRegistryRefreshFrame)
                return;

            RefreshCachedRegistryServices();
        }

        void IGlobalRegistryHotSwapRefListener.OnGlobalRegistryServiceRebound(GlobalRegistryServiceSlot serviceSlot, ref object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        void IGlobalRegistryHotSwapListener.OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            ApplyRegistryServiceRebind(serviceSlot, currentService);
        }

        private void ApplyRegistryServiceRebind(GlobalRegistryServiceSlot serviceSlot, object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault currentVault = currentService as IDataVault;
                _registryDataVault = currentVault;
                if (!ReferenceEquals(_dataVault, currentVault))
                {
                    InvalidateDataVaultLease();
                    _gpuReady = false;
                }

                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.FluidRuntime)
                _abyssalFlowGpuReadModel = currentService as IAbyssalFlowGpuReadModel;
        }

        private bool TryEnsureGpuState()
        {
            if (_gpuReady && IsGpuStateValid())
                return true;

            _gpuReady = false;
            if (fluidAdvectionCompute == null || !_coldSupportsComputeShaders)
                return false;

            if (emptySdfFlowTexture3D == null)
            {
                UnityEngine.Assertions.Assert.IsNotNull(emptySdfFlowTexture3D, "Fatal: Missing authored neutral CarveDebris SDF/flow Texture3D.");
                return false;
            }

            IDataVault vault = _registryDataVault;
            if (vault == null)
                return false;
            if (vault.IsCompactionFenceActive)
            {
                InvalidateDataVaultLease();
                return false;
            }

            _advectKernel = ResolveKernel(fluidAdvectionCompute, "AdvectCarveDebris");
            _clearArgsKernel = ResolveKernel(fluidAdvectionCompute, "ClearCarveDebrisIndirectArgs");
            _cullKernel = ResolveKernel(fluidAdvectionCompute, "CullCarveDebrisForRender");
            if (_advectKernel < 0 || _clearArgsKernel < 0 || _cullKernel < 0)
                return false;

            if (!TryResolveKernelThreadGroupSizeX(_advectKernel, out _advectThreadGroupSize) ||
                !TryResolveKernelThreadGroupSizeX(_clearArgsKernel, out _clearArgsThreadGroupSize) ||
                !TryResolveKernelThreadGroupSizeX(_cullKernel, out _cullThreadGroupSize))
            {
                return false;
            }

            _dataVault = vault;
            if (!EnsureCarveDebrisVaultBuffer(
                    ref _debrisPositionsHandle,
                    BufferID.CarveDebris,
                    MaxCarveDebrisCount,
                    NativeArrayOptions.ClearMemory,
                    out var debrisPositions) ||
                !EnsureCarveDebrisVaultBuffer(
                    ref _debrisVelocitiesHandle,
                    BufferID.CarveDebrisVelocity,
                    MaxCarveDebrisCount,
                    NativeArrayOptions.ClearMemory,
                    out var debrisVelocities) ||
                !EnsureCarveDebrisVaultBuffer(
                    ref _jobStateHandle,
                    BufferID.CarveDebrisJobState,
                    JobStateLength,
                    NativeArrayOptions.ClearMemory,
                    out var jobState) ||
                !EnsureCarveDebrisVaultBuffer(
                    ref _blackBoxHandle,
                    BufferID.CarveDebrisBlackBox,
                    BlackBoxCapacity,
                    NativeArrayOptions.ClearMemory,
                    out var blackBox) ||
                !EnsureCarveDebrisVaultBuffer(
                    ref _carveRequestsHandle,
                    BufferID.CarveDebrisRequests,
                    MaxCarveSignalsPerFrame,
                    NativeArrayOptions.ClearMemory,
                    out var carveRequests))
            {
                InvalidateDataVaultLease();
                return false;
            }

            AllocateGraphicsBuffers();
            ResolveEmptyResources();
            ClearMirrorsAndUpload(debrisPositions, debrisVelocities, carveRequests, jobState, blackBox);
            _gpuReady = IsGpuStateValid();
            return _gpuReady;
        }

        private int ResolveKernel(ComputeShader compute, string kernelName)
        {
            if (compute == null || !_coldSupportsComputeShaders)
                return -1;

            try
            {
                if (!compute.HasKernel(kernelName))
                    return -1;

                int kernel = compute.FindKernel(kernelName);
                if (kernel < 0)
                    return -1;

                return compute.IsSupported(kernel) ? kernel : -1;
            }
            catch (System.ObjectDisposedException)
            {
                return -1;
            }
            catch (System.InvalidOperationException)
            {
                return -1;
            }
            catch (System.ArgumentException)
            {
                return -1;
            }
            catch (MissingReferenceException)
            {
                return -1;
            }
            catch (UnityException)
            {
                return -1;
            }
        }

        private bool TryResolveKernelThreadGroupSizeX(int kernelIndex, out int groupSizeX)
        {
            groupSizeX = 0;
            if (fluidAdvectionCompute == null ||
                kernelIndex < 0 ||
                !_coldSupportsComputeShaders)
                return false;

            uint x;
            uint y;
            uint z;
            try
            {
                if (!fluidAdvectionCompute.IsSupported(kernelIndex))
                    return false;

                fluidAdvectionCompute.GetKernelThreadGroupSizes(kernelIndex, out x, out y, out z);
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
            ulong totalThreads = (ulong)x * y * z;
            if (x == 0u || y != 1u || z != 1u || totalThreads > ThreadGroupPortableMaxSize || x > 2147483647u)
                return false;

            groupSizeX = (int)x;
            return true;
        }

        private void CacheGraphicsCapabilitiesCold()
        {
            _coldSupportsComputeShaders = SystemInfo.supportsComputeShaders;
        }

        private bool IsGpuStateValid()
        {
            return IsDataVaultLeaseValid() &&
                   _positionBufferA != null && _positionBufferA.IsValid() &&
                   _positionBufferB != null && _positionBufferB.IsValid() &&
                   _velocityBufferA != null && _velocityBufferA.IsValid() &&
                   _velocityBufferB != null && _velocityBufferB.IsValid() &&
                   _visibleIndicesBuffer != null && _visibleIndicesBuffer.IsValid() &&
                   _indirectArgsBuffer != null && _indirectArgsBuffer.IsValid() &&
                   _emptyFlowBuffer != null && _emptyFlowBuffer.IsValid() &&
                   _emptyTexture3D != null;
        }

        private void InvalidateDataVaultLease()
        {
            IDataVault vault = _dataVault;
            ReleaseCarveDebrisVaultHandle(vault, ref _debrisPositionsHandle, BufferID.CarveDebris);
            ReleaseCarveDebrisVaultHandle(vault, ref _debrisVelocitiesHandle, BufferID.CarveDebrisVelocity);
            ReleaseCarveDebrisVaultHandle(vault, ref _carveRequestsHandle, BufferID.CarveDebrisRequests);
            ReleaseCarveDebrisVaultHandle(vault, ref _jobStateHandle, BufferID.CarveDebrisJobState);
            ReleaseCarveDebrisVaultHandle(vault, ref _blackBoxHandle, BufferID.CarveDebrisBlackBox);
            _dataVault = null;
            ResetDataVaultEpochState();
        }

        private bool TryResolveVaultBuffers(
            out NativeArray<float4> debrisPositions,
            out NativeArray<float4> debrisVelocities,
            out NativeArray<CarveDebrisRequest> carveRequests,
            out NativeArray<int> jobState,
            out NativeArray<CarveDebrisTelemetryEntry> blackBox)
        {
            debrisPositions = default;
            debrisVelocities = default;
            carveRequests = default;
            jobState = default;
            blackBox = default;

            IDataVault vault = _dataVault;
            if (vault == null || !ReferenceEquals(vault, _registryDataVault) || vault.IsCompactionFenceActive)
                return false;

            return TryResolveCarveDebrisVaultBuffer(
                       ref _debrisPositionsHandle,
                       BufferID.CarveDebris,
                       MaxCarveDebrisCount,
                       out debrisPositions) &&
                   TryResolveCarveDebrisVaultBuffer(
                       ref _debrisVelocitiesHandle,
                       BufferID.CarveDebrisVelocity,
                       MaxCarveDebrisCount,
                       out debrisVelocities) &&
                   TryResolveCarveDebrisVaultBuffer(
                       ref _carveRequestsHandle,
                       BufferID.CarveDebrisRequests,
                       MaxCarveSignalsPerFrame,
                       out carveRequests) &&
                   TryResolveCarveDebrisVaultBuffer(
                       ref _jobStateHandle,
                       BufferID.CarveDebrisJobState,
                       JobStateLength,
                       out jobState) &&
                   TryResolveCarveDebrisVaultBuffer(
                       ref _blackBoxHandle,
                       BufferID.CarveDebrisBlackBox,
                       BlackBoxCapacity,
                       out blackBox);
        }

        private bool IsDataVaultLeaseValid()
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !ReferenceEquals(vault, _registryDataVault) ||
                vault.IsCompactionFenceActive)
            {
                return false;
            }

            return HasCarveDebrisVaultBuffer(
                       vault,
                       in _debrisPositionsHandle,
                       BufferID.CarveDebris,
                       MaxCarveDebrisCount) &&
                   HasCarveDebrisVaultBuffer(
                       vault,
                       in _debrisVelocitiesHandle,
                       BufferID.CarveDebrisVelocity,
                       MaxCarveDebrisCount) &&
                   HasCarveDebrisVaultBuffer(
                       vault,
                       in _carveRequestsHandle,
                       BufferID.CarveDebrisRequests,
                       MaxCarveSignalsPerFrame) &&
                   HasCarveDebrisVaultBuffer(
                       vault,
                       in _jobStateHandle,
                       BufferID.CarveDebrisJobState,
                       JobStateLength) &&
                   HasCarveDebrisVaultBuffer(
                       vault,
                       in _blackBoxHandle,
                       BufferID.CarveDebrisBlackBox,
                       BlackBoxCapacity);
        }

        private bool EnsureCarveDebrisVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (TryResolveCarveDebrisVaultBuffer(ref handle, bufferId, requiredLength, out buffer))
                return true;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, VaultOwnerSystem, options);
            return TryResolveCarveDebrisVaultBuffer(ref handle, bufferId, requiredLength, out buffer);
        }

        private bool TryResolveCarveDebrisVaultBuffer<T>(
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsCompactionFenceActive || requiredLength <= 0)
                return false;

            if (IsCarveDebrisVaultHandle(in handle, bufferId) &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!vault.TryGetGenerationHandle<T>(bufferId, out handle) ||
                !IsCarveDebrisVaultHandle(in handle, bufferId) ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                handle = default;
                buffer = default;
                return false;
            }

            return true;
        }

        private static bool HasCarveDebrisVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength) where T : struct
        {
            return vault != null &&
                   !vault.IsCompactionFenceActive &&
                   requiredLength > 0 &&
                   IsCarveDebrisVaultHandle(in handle, bufferId) &&
                   vault.TryReadHandle(in handle, out NativeArray<T> buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsCarveDebrisVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)VaultOwnerSystem &&
                   handle.Generation != 0u;
        }

        private static void ReleaseCarveDebrisVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId) where T : struct
        {
            if (vault != null && IsCarveDebrisVaultHandle(in handle, bufferId))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private void ResetDataVaultEpochState()
        {
            _blackBoxCursor = 0;
            _blackBoxDumped = false;
            _lastTelemetryFrame = -1;
        }

        private void AllocateGraphicsBuffers()
        {
            if (_positionBufferA == null || !_positionBufferA.IsValid())
                _positionBufferA = CreateGpuWriteStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_positionBufferB == null || !_positionBufferB.IsValid())
                _positionBufferB = CreateGpuWriteStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_velocityBufferA == null || !_velocityBufferA.IsValid())
                _velocityBufferA = CreateGpuWriteStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_velocityBufferB == null || !_velocityBufferB.IsValid())
                _velocityBufferB = CreateGpuWriteStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_visibleIndicesBuffer == null || !_visibleIndicesBuffer.IsValid())
            {
                _visibleIndicesBuffer = CreateGpuWriteStructuredBuffer<uint>(MaxCarveDebrisCount);
                InvalidateRenderMaterialBindings();
            }
            if (_emptyFlowBuffer == null || !_emptyFlowBuffer.IsValid())
                _emptyFlowBuffer = GraphicsBufferUploadUtility.CreateStructuredLockBuffer<float4>(1);
            if (_indirectArgsBuffer == null || !_indirectArgsBuffer.IsValid())
            {
                _indirectArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - compute-written indirect rock debris args - owner: VFX_SDF_CARVE_DEBRIS
            }
        }

        private static GraphicsBuffer CreateGpuWriteStructuredBuffer<T>(int count)
            where T : struct
        {
            return GraphicsBufferUploadUtility.CreateStructuredCopyDestinationBuffer<T>(math.max(1, count)); // COLD ALLOC: GraphicsBuffer[count] - persistent carve debris GPU-write lane; dirty CPU ranges use SetData fallback because UAV forbids LockBufferForWrite - owner: VFX_SDF_CARVE_DEBRIS
        }

        private void ResolveEmptyResources()
        {
            if (ReferenceEquals(_emptyTexture3D, emptySdfFlowTexture3D))
                return;

            _emptyTexture3D = emptySdfFlowTexture3D;
        }

        private void ClearMirrorsAndUpload(
            NativeArray<float4> debrisPositions,
            NativeArray<float4> debrisVelocities,
            NativeArray<CarveDebrisRequest> carveRequests,
            NativeArray<int> jobState,
            NativeArray<CarveDebrisTelemetryEntry> blackBox)
        {
            for (int i = 0; i < MaxCarveDebrisCount; i++)
            {
                debrisPositions[i] = default;
                debrisVelocities[i] = default;
            }

            jobState[JobStateActiveIndex] = 0;
            jobState[JobStateInjectedIndex] = 0;
            jobState[JobStateDirtyMinIndex] = MaxCarveDebrisCount;
            jobState[JobStateDirtyMaxIndex] = -1;
            jobState[JobStateFlagsIndex] = 0;
            for (int i = 0; i < MaxCarveSignalsPerFrame; i++)
                carveRequests[i] = default;
            for (int i = 0; i < BlackBoxCapacity; i++)
                blackBox[i] = default;

            _blackBoxCursor = 0;
            _lastTelemetryFrame = -1;
            _blackBoxDumped = false;
            _activeMirrorCount = 0;
            UploadRange(_positionBufferA, debrisPositions, 0, MaxCarveDebrisCount);
            UploadRange(_positionBufferB, debrisPositions, 0, MaxCarveDebrisCount);
            UploadRange(_velocityBufferA, debrisVelocities, 0, MaxCarveDebrisCount);
            UploadRange(_velocityBufferB, debrisVelocities, 0, MaxCarveDebrisCount);
            var empty = _emptyFlowBuffer.LockBufferForWrite<float4>(0, 1);
            try
            {
                empty[0] = default;
            }
            finally
            {
                _emptyFlowBuffer.UnlockBufferAfterWrite<float4>(1);
            }
        }

        private void AgeMirror(float dt, int activeCapacity, float lifetimeRcp, NativeArray<float4> debrisPositions, NativeArray<int> jobState)
        {
            if (!debrisPositions.IsCreated || !jobState.IsCreated)
                return;

            float lifeDelta = dt * lifetimeRcp;
            AgeCarveDebrisMirrorJob ageJob = new AgeCarveDebrisMirrorJob
            {
                Positions = debrisPositions,
                Capacity = activeCapacity,
                LifeDelta = lifeDelta,
                JobState = jobState
            };
            ageJob.Execute();
            _activeMirrorCount = math.clamp(jobState[JobStateActiveIndex], 0, activeCapacity);
        }

        private static void ResetFrameJobState(int activeCapacity, NativeArray<int> jobState)
        {
            if (!jobState.IsCreated)
                return;

            jobState[JobStateActiveIndex] = 0;
            jobState[JobStateInjectedIndex] = 0;
            jobState[JobStateDirtyMinIndex] = activeCapacity;
            jobState[JobStateDirtyMaxIndex] = -1;
        }

        private int DrainCarveSignals(
            float globalQualityWeight01,
            int activeCapacity,
            NativeArray<float4> debrisPositions,
            NativeArray<float4> debrisVelocities,
            NativeArray<CarveDebrisRequest> carveRequests,
            NativeArray<int> jobState)
        {
            ReadOnlySpan<VoxelCarveEvent> carveSignals = SignalBus<VoxelCarveEvent>.GetFrameSnapshot();
            int signalCount = math.min(carveSignals.Length, MaxCarveSignalScanPerFrame);
            int particlesPerCarve = ResolveParticlesPerCarve(globalQualityWeight01);
            int queuedCarves = 0;
            int requestCount = 0;
            for (int i = 0; i < signalCount && requestCount < MaxCarveSignalsPerFrame; i++)
            {
                VoxelCarveEvent carveEvent = carveSignals[i];
                if (!TryResolveCarveDebrisRadius(in carveEvent, out float sourceRadius))
                {
                    if (!IsFiniteCarveEvent(in carveEvent) ||
                        !IsSupportedCarveOperation(carveEvent.Operation) ||
                        !IsSupportedCarveShape(carveEvent.Shape))
                    {
                        jobState[JobStateFlagsIndex] |= (int)InvalidStateFlag;
                    }

                    continue;
                }

                float radius = math.max(MinimumCarveSpawnRadiusMeters, sourceRadius * spawnRadiusScale);
                float3 runtimeCenter = AbsoluteUniversePosition.FromAbsolutePosition(
                    ResolveCarveHitPointDouble(in carveEvent)).ToRuntimeFloat3();
                uint seed = BuildStableSeed(_frameSequence, in carveEvent, i);

                carveRequests[requestCount] = new CarveDebrisRequest
                {
                    Center = runtimeCenter,
                    EjectionAxis = ResolveCarveEjectionAxis(in carveEvent),
                    Radius = radius,
                    ParticlesToInject = particlesPerCarve,
                    InitialSpeed = initialVelocityMetersPerSecond,
                    Life = 1f,
                    Seed = seed
                };
                requestCount++;
                queuedCarves++;
            }

            ReadOnlySpan<DebrisSpawnSignal> debrisSignals = SignalBus<DebrisSpawnSignal>.GetFrameSnapshot();
            int debrisSignalCount = math.min(debrisSignals.Length, MaxDebrisSpawnSignalScanPerFrame);
            for (int i = 0; i < debrisSignalCount && requestCount < MaxCarveSignalsPerFrame; i++)
            {
                DebrisSpawnSignal debrisSignal = debrisSignals[i];
                if (!TryBuildComputeShardRequest(in debrisSignal, particlesPerCarve, jobState, out CarveDebrisRequest request))
                    continue;

                carveRequests[requestCount] = request;
                requestCount++;
                queuedCarves++;
            }

            int sparkRequestCount = AppendSparkRequests(particlesPerCarve, requestCount, carveRequests, jobState);
            requestCount += sparkRequestCount;
            queuedCarves += sparkRequestCount;

            if (requestCount <= 0)
                return 0;

            CarveDebrisInjectBatchJob injectJob = new CarveDebrisInjectBatchJob
            {
                Positions = debrisPositions,
                Velocities = debrisVelocities,
                Requests = carveRequests,
                RequestCount = requestCount,
                Capacity = activeCapacity,
                JobState = jobState
            };
            injectJob.Execute();

            int dirtyMin = jobState[JobStateDirtyMinIndex];
            int dirtyMax = jobState[JobStateDirtyMaxIndex];
            if (dirtyMax >= dirtyMin)
                UploadInjectedRange(dirtyMin, dirtyMax - dirtyMin + 1, debrisPositions, debrisVelocities);

            _activeMirrorCount = math.clamp(jobState[JobStateActiveIndex], 0, activeCapacity);
            return queuedCarves;
        }

        private void UploadInjectedRange(int start, int count, NativeArray<float4> debrisPositions, NativeArray<float4> debrisVelocities)
        {
            int safeStart = math.clamp(start, 0, MaxCarveDebrisCount - 1);
            int safeCount = math.clamp(count, 0, MaxCarveDebrisCount - safeStart);
            if (safeCount <= 0)
                return;

            if (!_insideDebrisVisualSync)
            {
                QueueDebrisUploadRange(safeStart, safeCount);
                return;
            }

            UploadRange(_positionBufferA, debrisPositions, safeStart, safeCount);
            UploadRange(_positionBufferB, debrisPositions, safeStart, safeCount);
            UploadRange(_velocityBufferA, debrisVelocities, safeStart, safeCount);
            UploadRange(_velocityBufferB, debrisVelocities, safeStart, safeCount);
        }

        private void QueueDebrisUploadRange(int start, int count)
        {
            int end = math.clamp(start + count - 1, 0, MaxCarveDebrisCount - 1);
            if (!_pendingDebrisUpload)
            {
                _pendingDebrisUploadStart = start;
                _pendingDebrisUploadEnd = end;
                _pendingDebrisUpload = true;
            }
            else
            {
                _pendingDebrisUploadStart = math.min(_pendingDebrisUploadStart, start);
                _pendingDebrisUploadEnd = math.max(_pendingDebrisUploadEnd, end);
            }

            TryRegisterLateFrameTick();
        }

        private void FlushPendingDebrisUploads()
        {
            if (!_pendingDebrisUpload)
                return;

            if (!TryResolveVaultBuffers(
                    out var debrisPositions,
                    out var debrisVelocities,
                    out _,
                    out _,
                    out _))
            {
                return;
            }

            int start = math.clamp(_pendingDebrisUploadStart, 0, MaxCarveDebrisCount - 1);
            int count = math.clamp(_pendingDebrisUploadEnd - start + 1, 0, MaxCarveDebrisCount - start);
            _pendingDebrisUpload = false;
            if (count <= 0)
                return;

            UploadRange(_positionBufferA, debrisPositions, start, count);
            UploadRange(_positionBufferB, debrisPositions, start, count);
            UploadRange(_velocityBufferA, debrisVelocities, start, count);
            UploadRange(_velocityBufferB, debrisVelocities, start, count);
        }

        private void DispatchGpu(float dt, float qualityPressure01, int activeCapacity)
        {
            if (_activeMirrorCount <= 0 && math.lengthsq(_pendingAupShift) <= 0.000001f)
            {
                _lastAppliedAupShift = default;
                _lastFlowActive = false;
                _lastSdfActive = false;
                _lastWakeActive = false;
                return;
            }

            if (!TryResolveDrawMesh(out _, out Vector4 drawArgsBase))
            {
                _lastFlowActive = false;
                _lastSdfActive = false;
                _lastWakeActive = false;
                return;
            }

            bool readA = (_bufferParity & 1) == 0;
            GraphicsBuffer positionRead = readA ? _positionBufferA : _positionBufferB;
            GraphicsBuffer positionWrite = readA ? _positionBufferB : _positionBufferA;
            GraphicsBuffer velocityRead = readA ? _velocityBufferA : _velocityBufferB;
            GraphicsBuffer velocityWrite = readA ? _velocityBufferB : _velocityBufferA;
            int advectDispatchGroups = ResolveDispatchGroups(activeCapacity, _advectThreadGroupSize);
            int clearArgsDispatchGroups = ResolveDispatchGroups(1, _clearArgsThreadGroupSize);
            int cullDispatchGroups = ResolveDispatchGroups(activeCapacity, _cullThreadGroupSize);
            Vector4 drawArgs = drawArgsBase;
            drawArgs.w = activeCapacity;
            float3 appliedAupShift = _pendingAupShift;
            if (clearArgsDispatchGroups <= 0)
                return;

            if (advectDispatchGroups <= 0 || cullDispatchGroups <= 0)
            {
                fluidAdvectionCompute.SetBuffer(_clearArgsKernel, CarveDebrisIndirectArgsId, _indirectArgsBuffer);
                fluidAdvectionCompute.Dispatch(_clearArgsKernel, clearArgsDispatchGroups, 1, 1);
                return;
            }

            BindSharedComputeParams(dt, qualityPressure01, activeCapacity, drawArgs);
            fluidAdvectionCompute.SetBuffer(_clearArgsKernel, CarveDebrisIndirectArgsId, _indirectArgsBuffer);
            fluidAdvectionCompute.Dispatch(_clearArgsKernel, clearArgsDispatchGroups, 1, 1);

            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisReadId, positionRead);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisWriteId, positionWrite);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisVelocityReadId, velocityRead);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisVelocityWriteId, velocityWrite);
            fluidAdvectionCompute.Dispatch(_advectKernel, advectDispatchGroups, 1, 1);

            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisReadId, positionWrite);
            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisVisibleIndicesId, _visibleIndicesBuffer);
            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisIndirectArgsId, _indirectArgsBuffer);
            fluidAdvectionCompute.Dispatch(_cullKernel, cullDispatchGroups, 1, 1);

            _bufferParity ^= 1;
            _lastAppliedAupShift = appliedAupShift;
            _pendingAupShift = default;
        }

        private void BindSharedComputeParams(float dt, float qualityPressure01, int activeCapacity, Vector4 drawArgs)
        {
            float clampedQualityPressure01 = math.saturate(qualityPressure01);
            float flowInfluence01 = math.smoothstep(0.18f, 0.55f, _cachedGlobalQualityWeight01);
            float sdfInfluence01 = math.smoothstep(0.25f, 0.65f, _cachedGlobalQualityWeight01);
            Camera camera = renderCamera;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            Vector3 cameraForward = camera != null ? camera.transform.forward : Vector3.zero;
            float cullForwardDot = math.lerp(-0.25f, 0f, clampedQualityPressure01);
            float renderDistanceSq = camera != null && renderDistanceMeters > 0f ? renderDistanceMeters * renderDistanceMeters : 0f;
            GraphicsBuffer flowBuffer = _emptyFlowBuffer;
            Texture flowTexture = _emptyTexture3D;
            Vector4 gridResolution = Vector4.zero;
            Vector4 flowCenter = Vector4.zero;
            Vector4 flowSpacing = Vector4.zero;
            Vector4 flowTextureParams = Vector4.zero;
            float flowTextureActive = 0f;
            float flowBufferActive = 0f;
            if (flowInfluence01 > 0.0001f)
            {
                flowBuffer = ResolveFlowPayload(
                    out flowTexture,
                    out gridResolution,
                    out flowCenter,
                    out flowSpacing,
                    out flowTextureParams,
                    out flowTextureActive,
                    out flowBufferActive);
                flowTextureActive *= flowInfluence01;
                flowBufferActive *= flowInfluence01;
            }

            Texture sdfTexture = ResolveSdfTexture(sdfInfluence01, out Matrix4x4 sdfWorldToLocal, out Vector4 sdfInvDoubleHalfExtents, out float sdfActive);
            float flowActive = math.max(flowBufferActive, flowTextureActive);
            _lastFlowActive = flowActive > 0.0001f;
            _lastSdfActive = sdfActive > 0.0001f;

            fluidAdvectionCompute.SetVector(CarveDebrisCountsId, new Vector4(activeCapacity, _activeMirrorCount, activeCapacity, _frameSequence));
            fluidAdvectionCompute.SetVector(CarveDebrisParamsId, new Vector4(dt, clampedQualityPressure01, sdfActive, dragToFlow));
            float lifetimeRcp = ResolveLifetimeRcp();
            Vector3 gravity = ResolveConfiguredGravity();
            fluidAdvectionCompute.SetVector(CarveDebrisForcesId, new Vector4(gravity.x, gravity.y, gravity.z, lifetimeRcp));
            fluidAdvectionCompute.SetVector(CarveDebrisAupShiftDeltaId, new Vector4(_pendingAupShift.x, _pendingAupShift.y, _pendingAupShift.z, 0f));
            fluidAdvectionCompute.SetVector(CarveDebrisCameraParamsId, new Vector4(cameraPosition.x, cameraPosition.y, cameraPosition.z, renderDistanceSq));
            fluidAdvectionCompute.SetVector(CarveDebrisCullParamsId, new Vector4(cameraForward.x, cameraForward.y, cameraForward.z, cullForwardDot));
            fluidAdvectionCompute.SetVector(CarveDebrisDrawArgsParamsId, drawArgs);
            fluidAdvectionCompute.SetBuffer(_advectKernel, AbyssalFlowFieldResultId, flowBuffer != null && flowBuffer.IsValid() ? flowBuffer : _emptyFlowBuffer);
            fluidAdvectionCompute.SetTexture(_advectKernel, AbyssalFlowFieldTextureId, flowTexture);
            fluidAdvectionCompute.SetTexture(_advectKernel, VoxelSdfTexture3DId, sdfTexture);
            fluidAdvectionCompute.SetVector(AbyssalGridResolutionId, gridResolution);
            fluidAdvectionCompute.SetVector(AbyssalFlowCenterId, flowCenter);
            fluidAdvectionCompute.SetVector(AbyssalFlowSpacingId, flowSpacing);
            fluidAdvectionCompute.SetVector(AbyssalFlowTextureParamsId, flowTextureParams);
            fluidAdvectionCompute.SetFloat(AbyssalFlowTextureActiveId, flowTextureActive);
            Vector4 dynamicWakeParams = ResolveDynamicWakePayloadForCompute(
                clampedQualityPressure01,
                out GraphicsBuffer dynamicWakeBuffer,
                out GraphicsBuffer dynamicWakeVectorBuffer);
            _lastWakeActive = dynamicWakeParams.x > 0.5f && dynamicWakeParams.z > 0.5f;
            fluidAdvectionCompute.SetBuffer(_advectKernel, DynamicWakesId, dynamicWakeBuffer);
            fluidAdvectionCompute.SetBuffer(_advectKernel, DynamicWakeVectorsId, dynamicWakeVectorBuffer);
            fluidAdvectionCompute.SetVector(DynamicWakeParamsId, dynamicWakeParams);
            fluidAdvectionCompute.SetMatrix(VoxelSdfWorldToLocalId, sdfWorldToLocal);
            fluidAdvectionCompute.SetVector(VoxelSdfInvDoubleHalfExtentsId, sdfInvDoubleHalfExtents);
            fluidAdvectionCompute.SetVector(FluidAdvectionParamsId, new Vector4(dt, clampedQualityPressure01, flowActive, sdfActive));
            fluidAdvectionCompute.SetVector(
                FluidAdvectionSdfParamsId,
                new Vector4(sdfActive, solidDensityThreshold, _configuredDebrisBounce, CarveDebrisSleepSpeedSq));
        }

        private Vector4 ResolveDynamicWakePayloadForCompute(
            float qualityPressure01,
            out GraphicsBuffer dynamicWakeBuffer,
            out GraphicsBuffer dynamicWakeVectorBuffer)
        {
            float clampedQualityPressure01 = math.saturate(qualityPressure01);
            dynamicWakeBuffer = _emptyFlowBuffer;
            dynamicWakeVectorBuffer = _emptyFlowBuffer;

            IAbyssalFlowGpuReadModel abyssalFlow = _abyssalFlowGpuReadModel;
            if (abyssalFlow == null ||
                !abyssalFlow.TryGetDynamicWakeGpuPayload(
                    out GraphicsBuffer publishedWakeBuffer,
                    out GraphicsBuffer publishedWakeVectorBuffer,
                    out Vector4 wakeParams))
            {
                return new Vector4(0f, clampedQualityPressure01, 0f, 0f);
            }

            if (publishedWakeBuffer != null && publishedWakeBuffer.IsValid())
                dynamicWakeBuffer = publishedWakeBuffer;
            if (publishedWakeVectorBuffer != null && publishedWakeVectorBuffer.IsValid())
                dynamicWakeVectorBuffer = publishedWakeVectorBuffer;

            return SanitizeDynamicWakeParamsForCompute(wakeParams, clampedQualityPressure01);
        }

        private static Vector4 SanitizeDynamicWakeParamsForCompute(Vector4 wakeParams, float qualityPressure01)
        {
            float clampedQualityPressure01 = math.saturate(qualityPressure01);
            if (!IsFiniteVector(wakeParams))
                return new Vector4(0f, clampedQualityPressure01, 0f, 0f);

            float maxSlotLimit = math.lerp(16f, 4f, clampedQualityPressure01);
            float slotLimit = math.clamp(wakeParams.x, 0f, maxSlotLimit);
            float activeCount = math.clamp(wakeParams.z, 0f, slotLimit);
            return new Vector4(
                slotLimit,
                clampedQualityPressure01,
                activeCount,
                math.saturate(wakeParams.w));
        }

        private static int ResolveDispatchGroups(int count, int groupSize)
        {
            if (count <= 0 || groupSize <= 0)
                return 0;

            long groups = ((long)count + groupSize - 1L) / groupSize;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private GraphicsBuffer ResolveFlowPayload(
            out Texture flowTexture,
            out Vector4 gridResolution,
            out Vector4 flowCenter,
            out Vector4 flowSpacing,
            out Vector4 flowTextureParams,
            out float flowTextureActive,
            out float flowBufferActive)
        {
            GraphicsBuffer flowBuffer = _emptyFlowBuffer;
            flowTexture = _emptyTexture3D;
            gridResolution = Vector4.zero;
            flowCenter = Vector4.zero;
            flowSpacing = Vector4.zero;
            flowTextureParams = Vector4.zero;
            flowTextureActive = 0f;
            flowBufferActive = 0f;

            IAbyssalFlowGpuReadModel abyssalFlow = ResolveAbyssalFlowReadModel();
            if (abyssalFlow != null &&
                abyssalFlow.TryGetGpuAbyssalFlowFieldBuffer(
                    out GraphicsBuffer publishedFlowBuffer,
                    out Vector4 publishedGridResolution,
                    out Vector4 publishedFlowCenter,
                    out Vector4 publishedFlowSpacing))
            {
                if (IsValidFlowBufferPayload(
                        publishedFlowBuffer,
                        publishedGridResolution,
                        publishedFlowCenter,
                        publishedFlowSpacing))
                {
                    flowBuffer = publishedFlowBuffer;
                    gridResolution = publishedGridResolution;
                    flowCenter = publishedFlowCenter;
                    flowSpacing = publishedFlowSpacing;
                    flowBufferActive = 1f;
                }
            }

            if (abyssalFlow != null &&
                abyssalFlow.TryGetGpuAbyssalFlowFieldTexture(
                    out Texture publishedFlowTexture,
                    out Vector4 publishedTextureResolution,
                    out Vector4 publishedTextureCenter,
                    out Vector4 publishedTextureSpacing))
            {
                if (publishedFlowTexture != null &&
                    IsFiniteVector(publishedTextureCenter) &&
                    TryResolveFlowTextureParams(publishedTextureSpacing, publishedTextureResolution.x, out Vector4 resolvedTextureParams))
                {
                    if (flowBufferActive > 0.5f && !AreFlowCentersCompatible(flowCenter, publishedTextureCenter))
                        DisableFlowBufferFallback(ref flowBuffer, ref gridResolution, ref flowSpacing, ref flowBufferActive);

                    flowTexture = publishedFlowTexture;
                    flowCenter = publishedTextureCenter;
                    flowTextureParams = resolvedTextureParams;
                    flowTextureActive = 1f;
                }
            }
            else if (abyssalFlowTextureOverride != null)
            {
                Vector4 globalTextureParams = _cachedAbyssalFlowTextureParams;
                Vector4 globalFlowCenter = _cachedAbyssalFlowCenter;
                if (IsFiniteVector(globalFlowCenter) &&
                    TryResolveFlowTextureParams(globalTextureParams, globalTextureParams.x, out Vector4 resolvedTextureParams))
                {
                    if (flowBufferActive > 0.5f && !AreFlowCentersCompatible(flowCenter, globalFlowCenter))
                        DisableFlowBufferFallback(ref flowBuffer, ref gridResolution, ref flowSpacing, ref flowBufferActive);

                    flowTexture = abyssalFlowTextureOverride;
                    flowCenter = globalFlowCenter;
                    flowTextureParams = resolvedTextureParams;
                    flowTextureActive = 1f;
                }
            }

            return flowBuffer != null && flowBuffer.IsValid() ? flowBuffer : _emptyFlowBuffer;
        }

        private void RefreshAbyssalFlowOverrideGlobalsCold()
        {
            if (abyssalFlowTextureOverride == null)
            {
                _cachedAbyssalFlowTextureParams = Vector4.zero;
                _cachedAbyssalFlowCenter = Vector4.zero;
                return;
            }

            _cachedAbyssalFlowTextureParams = Shader.GetGlobalVector(AbyssalFlowTextureParamsId);
            _cachedAbyssalFlowCenter = Shader.GetGlobalVector(AbyssalFlowCenterId);
        }

        private void DisableFlowBufferFallback(
            ref GraphicsBuffer flowBuffer,
            ref Vector4 gridResolution,
            ref Vector4 flowSpacing,
            ref float flowBufferActive)
        {
            flowBuffer = _emptyFlowBuffer;
            gridResolution = Vector4.zero;
            flowSpacing = Vector4.zero;
            flowBufferActive = 0f;
        }

        private static bool AreFlowCentersCompatible(Vector4 bufferCenter, Vector4 textureCenter)
        {
            float dx = bufferCenter.x - textureCenter.x;
            float dy = bufferCenter.y - textureCenter.y;
            float dz = bufferCenter.z - textureCenter.z;
            return math.isfinite(dx) &&
                   math.isfinite(dy) &&
                   math.isfinite(dz) &&
                   dx * dx + dy * dy + dz * dz <= 0.0001f;
        }

        private IAbyssalFlowGpuReadModel ResolveAbyssalFlowReadModel()
        {
            return _abyssalFlowGpuReadModel;
        }

        private static bool IsValidFlowBufferPayload(
            GraphicsBuffer flowBuffer,
            Vector4 gridResolution,
            Vector4 flowCenter,
            Vector4 flowSpacing)
        {
            return flowBuffer != null &&
                   flowBuffer.IsValid() &&
                   flowBuffer.count > 0 &&
                   gridResolution.x > 0f &&
                   gridResolution.y > 0f &&
                   gridResolution.z > 0f &&
                   gridResolution.w > 0f &&
                   gridResolution.w <= flowBuffer.count &&
                   flowSpacing.x > 0f &&
                   flowSpacing.y > 0f &&
                   flowSpacing.z > 0f &&
                   IsFiniteVector(gridResolution) &&
                   IsFiniteVector(flowCenter) &&
                   IsFiniteVector(flowSpacing);
        }

        private static bool IsFiniteVector(Vector4 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z) &&
                   math.isfinite(value.w);
        }

        private static bool TryResolveFlowTextureParams(Vector4 sourceParams, float textureResolution, out Vector4 textureParams)
        {
            float worldSize = sourceParams.z > sourceParams.y && sourceParams.z > 1f ? sourceParams.z : sourceParams.y;
            if (!math.isfinite(worldSize) || worldSize <= 0.001f)
            {
                textureParams = Vector4.zero;
                return false;
            }

            float inverseWorldSize = sourceParams.w > 0f && sourceParams.w < 1f
                ? sourceParams.w
                : math.rcp(worldSize);
            float resolution = textureResolution > 0f && math.isfinite(textureResolution) ? textureResolution : sourceParams.x;
            textureParams = new Vector4(resolution, worldSize, sourceParams.z, inverseWorldSize);
            return math.isfinite(textureParams.x) &&
                   math.isfinite(textureParams.y) &&
                   math.isfinite(textureParams.z) &&
                   math.isfinite(textureParams.w);
        }

        private Texture ResolveSdfTexture(float sdfInfluence01, out Matrix4x4 sdfWorldToLocal, out Vector4 sdfInvDoubleHalfExtents, out float sdfActive)
        {
            float clampedSdfInfluence01 = math.saturate(sdfInfluence01);
            sdfWorldToLocal = Matrix4x4.identity;
            sdfInvDoubleHalfExtents = Vector4.zero;
            sdfActive = 0f;
            if (clampedSdfInfluence01 <= 0.0001f)
                return _emptyTexture3D;

            if (voxelSdfTexture3D != null &&
                IsFiniteMatrix(voxelSdfWorldToLocal) &&
                IsValidSdfInvDoubleHalfExtents(voxelSdfInvDoubleHalfExtents))
            {
                sdfWorldToLocal = voxelSdfWorldToLocal;
                sdfInvDoubleHalfExtents = voxelSdfInvDoubleHalfExtents;
                sdfActive = clampedSdfInfluence01;
                return voxelSdfTexture3D;
            }

            if (_cachedGlobalSdfActive > 0.5f && _cachedGlobalSdfTexture != null)
            {
                sdfWorldToLocal = _cachedGlobalSdfWorldToLocal;
                sdfInvDoubleHalfExtents = _cachedGlobalSdfInvDoubleHalfExtents;
                sdfActive = clampedSdfInfluence01;
                return _cachedGlobalSdfTexture;
            }

            return _emptyTexture3D;
        }

        private void RefreshGlobalSdfCacheCold()
        {
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (frame < _nextGlobalSdfRefreshFrame)
                return;

            _nextGlobalSdfRefreshFrame = frame + GlobalSdfRefreshStrideFrames;
            Texture sdfTexture = Shader.GetGlobalTexture(HectonCaveVoxelSdfTexId);
            Vector4 invDoubleHalfExtents = Shader.GetGlobalVector(HectonCaveVoxelInvDoubleHalfExtentsId);
            bool valid = Shader.GetGlobalFloat(HectonCaveVoxelActiveId) > 0.5f &&
                         sdfTexture != null &&
                         IsValidSdfInvDoubleHalfExtents(invDoubleHalfExtents);
            Matrix4x4 worldToLocal = Matrix4x4.identity;
            if (valid)
            {
                worldToLocal = Shader.GetGlobalMatrix(HectonCaveVoxelWorldToLocalId);
                valid = IsFiniteMatrix(worldToLocal);
            }

            if (!valid)
            {
                _cachedGlobalSdfTexture = null;
                _cachedGlobalSdfWorldToLocal = Matrix4x4.identity;
                _cachedGlobalSdfInvDoubleHalfExtents = Vector4.zero;
                _cachedGlobalSdfActive = 0f;
                return;
            }

            _cachedGlobalSdfTexture = sdfTexture;
            _cachedGlobalSdfWorldToLocal = worldToLocal;
            _cachedGlobalSdfInvDoubleHalfExtents = invDoubleHalfExtents;
            _cachedGlobalSdfActive = 1f;
        }

        private static bool IsFiniteMatrix(Matrix4x4 value)
        {
            return math.isfinite(value.m00) &&
                   math.isfinite(value.m01) &&
                   math.isfinite(value.m02) &&
                   math.isfinite(value.m03) &&
                   math.isfinite(value.m10) &&
                   math.isfinite(value.m11) &&
                   math.isfinite(value.m12) &&
                   math.isfinite(value.m13) &&
                   math.isfinite(value.m20) &&
                   math.isfinite(value.m21) &&
                   math.isfinite(value.m22) &&
                   math.isfinite(value.m23) &&
                   math.isfinite(value.m30) &&
                   math.isfinite(value.m31) &&
                   math.isfinite(value.m32) &&
                   math.isfinite(value.m33);
        }

        private static bool IsValidSdfInvDoubleHalfExtents(Vector4 invDoubleHalfExtents)
        {
            return invDoubleHalfExtents.x > 0f &&
                   invDoubleHalfExtents.y > 0f &&
                   invDoubleHalfExtents.z > 0f &&
                   math.isfinite(invDoubleHalfExtents.x) &&
                   math.isfinite(invDoubleHalfExtents.y) &&
                   math.isfinite(invDoubleHalfExtents.z);
        }

        private static int ResolveActiveCapacity(float globalQualityWeight01, int configuredCap)
        {
            int cap = ShinobuDeltaCrusher.ResolveDebrisCap(globalQualityWeight01, configuredCap);
            return math.clamp(cap, MinQualityActiveCarveDebrisCount, MaxCarveDebrisCount);
        }

        private void RefreshDebrisTuningFromVault(NativeArray<int> jobState)
        {
            _configuredGravityMetersPerSecondSq = gravityMetersPerSecondSq;
            _configuredDebrisBounce = DefaultCarveDebrisBounce;
            _configuredMaxActiveDebris = MaxCarveDebrisCount;

            if (!ShinobuDeltaCrusher.TryReadCarveDebrisTuning(jobState, out CarveDebrisTuningDTO dto))
            {
                return;
            }

            if (math.all(math.isfinite(dto.Gravity)))
                _configuredGravityMetersPerSecondSq = new Vector3(dto.Gravity.x, dto.Gravity.y, dto.Gravity.z);

            if (math.isfinite(dto.Bounce))
                _configuredDebrisBounce = math.saturate(dto.Bounce);

            _configuredMaxActiveDebris = math.clamp(
                dto.MaxActiveDebris > 0 ? dto.MaxActiveDebris : MaxCarveDebrisCount,
                MinQualityActiveCarveDebrisCount,
                MaxCarveDebrisCount);
        }

        private Vector3 ResolveConfiguredGravity()
        {
            return math.isfinite(_configuredGravityMetersPerSecondSq.x) &&
                   math.isfinite(_configuredGravityMetersPerSecondSq.y) &&
                   math.isfinite(_configuredGravityMetersPerSecondSq.z)
                ? _configuredGravityMetersPerSecondSq
                : gravityMetersPerSecondSq;
        }

        private static int ResolveParticlesPerCarve(float globalQualityWeight01)
        {
            float quality = ShinobuDeltaCrusher.SmoothQuality01(globalQualityWeight01);
            float midBlend = math.saturate(quality * 2f);
            float highBlend = math.saturate((quality - 0.5f) * 2f);
            float middle = math.lerp(MinQualityParticlesPerCarve, MiddleQualityParticlesPerCarve, midBlend);
            float high = math.lerp(middle, MaxQualityParticlesPerCarve, highBlend);
            return math.clamp((int)math.round(high), MinQualityParticlesPerCarve, MaxQualityParticlesPerCarve);
        }

        private static float ResolveGlobalQualityWeight01()
        {
            float quality = SignalBusRegistry.GlobalQualityWeight01;
            return math.isfinite(quality) ? math.saturate(quality) : 0f;
        }

        private void RefreshQualityPolicy(float globalQualityWeight01)
        {
            float qualityCurve01 = ShinobuDeltaCrusher.SmoothQuality01(globalQualityWeight01);
            _qualityPressure01 = 1f - math.saturate(qualityCurve01);
            _visualOverkill01 = math.smoothstep(0.55f, 1f, math.saturate(globalQualityWeight01));
        }

        private float ResolveLifetimeRcp()
        {
            float multiplier = _cachedSystemStress01 > StressRecycleThreshold01
                ? StressRecycleLifetimeMultiplier
                : 1f;
            return math.rcp(math.max(0.001f, particleLifetimeSeconds)) * multiplier;
        }

        private static float ResolveSystemStress01()
        {
            float stress01 = math.saturate(SignalBusRegistry.SystemStress01);
            ReadOnlySpan<SystemHealthIndexSignal> healthSignals = SignalBus<SystemHealthIndexSignal>.GetFrameSnapshot();
            for (int i = 0; i < healthSignals.Length; i++)
                stress01 = math.max(stress01, math.saturate(healthSignals[i].Pressure01));

            return math.isfinite(stress01) ? stress01 : 1f;
        }

        private void ApplyCapacityShed(int activeCapacity, NativeArray<float4> debrisPositions, NativeArray<float4> debrisVelocities)
        {
            int safeCapacity = math.clamp(activeCapacity, MinQualityActiveCarveDebrisCount, MaxCarveDebrisCount);
            if (safeCapacity >= _lastActiveCapacity)
            {
                _lastActiveCapacity = safeCapacity;
                return;
            }

            if (debrisPositions.IsCreated && debrisVelocities.IsCreated)
            {
                for (int i = safeCapacity; i < _lastActiveCapacity && i < MaxCarveDebrisCount; i++)
                {
                    debrisPositions[i] = default;
                    debrisVelocities[i] = default;
                }

                int clearCount = math.clamp(_lastActiveCapacity - safeCapacity, 0, MaxCarveDebrisCount - safeCapacity);
                if (clearCount > 0)
                    UploadInjectedRange(safeCapacity, clearCount, debrisPositions, debrisVelocities);
            }

            _activeMirrorCount = math.min(_activeMirrorCount, safeCapacity);
            _lastActiveCapacity = safeCapacity;
        }

        private void RenderDebris()
        {
            if (_activeMirrorCount <= 0 ||
                _visibleIndicesBuffer == null ||
                !_visibleIndicesBuffer.IsValid() ||
                _indirectArgsBuffer == null ||
                !_indirectArgsBuffer.IsValid())
            {
                return;
            }

            Material material = ResolveMaterial();
            if (!TryResolveDrawMesh(out Mesh mesh, out _) || material == null)
                return;

            GraphicsBuffer currentPositionBuffer = (_bufferParity & 1) == 0 ? _positionBufferA : _positionBufferB;
            GraphicsBuffer currentVelocityBuffer = (_bufferParity & 1) == 0 ? _velocityBufferA : _velocityBufferB;
            float visualOverkill01 = _visualOverkill01;
            material.SetBuffer(CarveDebrisReadId, currentPositionBuffer);
            material.SetBuffer(CarveDebrisVelocityReadId, currentVelocityBuffer);
            material.SetBuffer(DebrisBufferId, currentPositionBuffer);
            material.SetBuffer(DebrisPhysicsBufferId, currentVelocityBuffer);
            BindStaticRenderMaterialState(material, visualOverkill01);

            bool visualOverkillShadows = visualOverkill01 >= 0.95f;
            ShadowCastingMode resolvedShadowCastingMode = visualOverkillShadows && shadowCastingMode == ShadowCastingMode.Off
                ? ShadowCastingMode.On
                : shadowCastingMode;

            RenderParams renderParams = new RenderParams(material)
            {
                camera = renderCamera,
                worldBounds = drawBounds,
                layer = renderLayer,
                shadowCastingMode = resolvedShadowCastingMode,
                receiveShadows = visualOverkillShadows,
                motionVectorMode = MotionVectorGenerationMode.Object
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _indirectArgsBuffer, 1, 0);
        }

        private void BindStaticRenderMaterialState(Material material, float visualOverkill01)
        {
            float clampedVisualOverkill01 = math.saturate(visualOverkill01);
            bool materialChanged = !ReferenceEquals(_boundRenderMaterial, material);
            if (materialChanged || !ReferenceEquals(_boundVisibleIndicesBuffer, _visibleIndicesBuffer))
            {
                material.SetBuffer(CarveDebrisVisibleIndicesId, _visibleIndicesBuffer);
                _boundRenderMaterial = material;
                _boundVisibleIndicesBuffer = _visibleIndicesBuffer;
                _boundMaterialParamsValid = false;
                _boundMotionParamsValid = false;
            }

            Vector4 materialParams = new Vector4(
                minRockScale,
                math.max(minRockScale, maxRockScale),
                particleLifetimeSeconds,
                clampedVisualOverkill01);
            if (!_boundMaterialParamsValid || !AreVector4ExactlyEqual(_boundMaterialParams, materialParams))
            {
                material.SetVector(CarveDebrisMaterialParamsId, materialParams);
                _boundMaterialParams = materialParams;
                _boundMaterialParamsValid = true;
            }

            Vector4 motionParams = new Vector4(
                math.max(0.0001f, _lastDeltaTime),
                _cachedSystemStress01,
                _qualityPressure01,
                clampedVisualOverkill01);
            if (!_boundMotionParamsValid || !AreVector4ExactlyEqual(_boundMotionParams, motionParams))
            {
                material.SetVector(CarveDebrisMotionParamsId, motionParams);
                _boundMotionParams = motionParams;
                _boundMotionParamsValid = true;
            }
        }

        private Mesh ResolveMesh()
        {
            if (debrisMesh != null)
                return debrisMesh;

            QueueFallbackRenderResourceRepair();
            return null;
        }

        private bool TryResolveDrawMesh(out Mesh mesh, out Vector4 drawArgsBase)
        {
            mesh = ResolveMesh();
            drawArgsBase = Vector4.zero;
            if (mesh == null || mesh.subMeshCount <= 0)
            {
                _cachedDrawMeshValid = false;
                return false;
            }

            if (!ReferenceEquals(mesh, _cachedDrawMesh))
            {
                _cachedDrawMesh = mesh;
                _cachedDrawIndexCount = mesh.GetIndexCount(0);
                _cachedDrawIndexStart = mesh.GetIndexStart(0);
                _cachedDrawBaseVertex = (uint)math.max(0, mesh.GetBaseVertex(0));
                _cachedDrawMeshValid = _cachedDrawIndexCount > 0u;
            }

            if (!_cachedDrawMeshValid)
                return false;

            drawArgsBase = new Vector4(_cachedDrawIndexCount, _cachedDrawIndexStart, _cachedDrawBaseVertex, 0f);
            return true;
        }

        private void EnsureFallbackRenderResources()
        {
            EnsureOwnedMaterial();
            _fallbackRenderResourceRepairRequested = false;
        }

        private void EnsureOwnedMaterial()
        {
            if (debrisMaterial == null || !IsSupportedDebrisMaterial(debrisMaterial))
            {
                DestroyOwnedMaterial();
                return;
            }

            if (ReferenceEquals(_ownedMaterial, debrisMaterial) &&
                ReferenceEquals(_ownedMaterialSource, debrisMaterial))
            {
                return;
            }

            DestroyOwnedMaterial();
            _ownedMaterial = debrisMaterial;
            _ownedMaterialSource = debrisMaterial;
        }

        private static bool IsSupportedDebrisMaterial(Material material)
        {
            return material != null &&
                   material.shader != null &&
                   string.Equals(material.shader.name, DebrisShaderName, StringComparison.Ordinal);
        }

        private Material ResolveMaterial()
        {
            if (_ownedMaterial == null || !ReferenceEquals(_ownedMaterialSource, debrisMaterial))
            {
                QueueFallbackRenderResourceRepair();
                return null;
            }

            return _ownedMaterial;
        }

        private void QueueFallbackRenderResourceRepair()
        {
            _fallbackRenderResourceRepairRequested = true;
        }

        private void FlushFallbackRenderResourceRepairSlow()
        {
            if (!_fallbackRenderResourceRepairRequested)
                return;

            EnsureFallbackRenderResources();
        }

        private void DestroyOwnedMaterial()
        {
            InvalidateRenderMaterialBindings();

            if (_ownedMaterial != null && !ReferenceEquals(_ownedMaterial, _ownedMaterialSource))
                DestroyUnityObject(_ownedMaterial);

            _ownedMaterial = null;
            _ownedMaterialSource = null;
        }

        private void InvalidateRenderMaterialBindings()
        {
            _boundRenderMaterial = null;
            _boundVisibleIndicesBuffer = null;
            _boundMaterialParams = default;
            _boundMotionParams = default;
            _boundMaterialParamsValid = false;
            _boundMotionParamsValid = false;
        }

        private static bool AreVector4ExactlyEqual(Vector4 lhs, Vector4 rhs)
        {
            return lhs.x == rhs.x &&
                   lhs.y == rhs.y &&
                   lhs.z == rhs.z &&
                   lhs.w == rhs.w;
        }

        private void DrainAupShiftSignals(NativeArray<int> jobState)
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                AupShiftSignal signal = shifts[i];
                if (signal.ShiftFrameId == 0u || signal.ShiftFrameId <= _lastProcessedAupShiftFrameId)
                    continue;

                _lastProcessedAupShiftFrameId = signal.ShiftFrameId;
                if (!math.all(math.isfinite(signal.ShiftMeters)))
                {
                    jobState[JobStateFlagsIndex] |= (int)InvalidStateFlag;
                    continue;
                }

                _pendingAupShift += -signal.ShiftMeters;
                if (!math.all(math.isfinite(_pendingAupShift)))
                {
                    jobState[JobStateFlagsIndex] |= (int)InvalidStateFlag;
                    _pendingAupShift = default;
                    continue;
                }

                _nextGlobalSdfRefreshFrame = 0;
            }

            if (_activeMirrorCount <= 0)
                _pendingAupShift = default;
        }

        private bool TryBuildComputeShardRequest(
            in DebrisSpawnSignal signal,
            int particlesPerSignal,
            NativeArray<int> jobState,
            out CarveDebrisRequest request)
        {
            request = default;
            if ((signal.Flags & DebrisSpawnSignal.FlagComputeShard) == 0)
                return false;

            float intensity01 = math.saturate(signal.Intensity01);
            float3 center = signal.PositionAup.ToRuntimeFloat3();
            if (!math.isfinite(intensity01) || !math.all(math.isfinite(center)))
            {
                jobState[JobStateFlagsIndex] |= (int)InvalidStateFlag;
                return false;
            }

            uint seed = signal.SourceEntityId ^
                        (signal.SpeciesHash * 747796405u) ^
                        ((uint)signal.DebrisKind << 24) ^
                        (_frameSequence * 2891336453u);
            seed = seed == 0u ? 1u : seed;
            int quantity = signal.Quantity > 0 ? signal.Quantity : particlesPerSignal;
            request = new CarveDebrisRequest
            {
                Center = center,
                EjectionAxis = BuildSignalEjectionAxis(seed),
                Radius = math.lerp(0.08f, 0.85f, intensity01),
                ParticlesToInject = math.clamp(quantity, 1, particlesPerSignal),
                InitialSpeed = initialVelocityMetersPerSecond * math.lerp(0.65f, 1.45f, intensity01),
                Life = 1f,
                Seed = seed
            };
            return true;
        }

        /// <summary>
        /// Drains the <see cref="VfxSparkRequestSignal"/> lane published by the tool kinematics runtime while
        /// a cutter is biting and turns each accepted request into a GPU debris injection. Rate limited per
        /// tool so a continuously firing tool produces a readable spark stream instead of a per-frame burst
        /// that would evict carve debris from the shared particle pool.
        /// </summary>
        /// <returns>Number of requests appended at <paramref name="existingRequestCount"/>.</returns>
        private int AppendSparkRequests(
            int particlesPerCarve,
            int existingRequestCount,
            NativeArray<CarveDebrisRequest> carveRequests,
            NativeArray<int> jobState)
        {
            AdvanceSparkGates(_lastDeltaTime);
            ReadOnlySpan<VfxSparkRequestSignal> sparkSignals = SignalBus<VfxSparkRequestSignal>.GetFrameSnapshot();
            if (sparkSignals.Length == 0)
                return 0;

            // Spark hit points arrive camera-relative (ToolKinematicsMath.ToLocalFloat3 subtracts the camera
            // anchor position), so the render camera position is the only legal way back to runtime space.
            Camera camera = renderCamera;
            if (camera == null)
                return 0;

            Vector3 cameraRuntimePosition = camera.transform.position;
            float3 cameraOrigin = new float3(cameraRuntimePosition.x, cameraRuntimePosition.y, cameraRuntimePosition.z);
            if (!math.all(math.isfinite(cameraOrigin)))
                return 0;

            int requestLimit = math.min(MaxCarveSignalsPerFrame, carveRequests.IsCreated ? carveRequests.Length : 0);
            int scanCount = math.min(sparkSignals.Length, MaxVfxSparkSignalScanPerFrame);
            int appended = 0;
            for (int i = 0; i < scanCount && existingRequestCount + appended < requestLimit; i++)
            {
                VfxSparkRequestSignal spark = sparkSignals[i];
                if (!math.isfinite(spark.Intensity01) ||
                    !math.all(math.isfinite(spark.HitPoint)) ||
                    !math.all(math.isfinite(spark.Normal)))
                {
                    jobState[JobStateFlagsIndex] |= (int)InvalidStateFlag;
                    continue;
                }

                float intensity01 = math.saturate(spark.Intensity01);
                if (intensity01 < SparkMinimumIntensity01)
                    continue;

                if (!TryOpenSparkGate(spark.ToolHash))
                    continue;

                float3 center = cameraOrigin + spark.HitPoint;
                if (!math.all(math.isfinite(center)))
                {
                    jobState[JobStateFlagsIndex] |= (int)InvalidStateFlag;
                    continue;
                }

                uint seed = BuildSparkSeed(_frameSequence, in spark, i);
                int sparkParticles = math.clamp(
                    (int)math.round(particlesPerCarve * SparkParticleShare * math.lerp(0.45f, 1f, intensity01)),
                    MinimumSparkParticles,
                    math.max(MinimumSparkParticles, particlesPerCarve));
                carveRequests[existingRequestCount + appended] = new CarveDebrisRequest
                {
                    Center = center,
                    EjectionAxis = ResolveSparkEjectionAxis(in spark, seed),
                    Radius = math.lerp(MinimumSparkSpawnRadiusMeters, MaximumSparkSpawnRadiusMeters, intensity01),
                    ParticlesToInject = sparkParticles,
                    InitialSpeed = initialVelocityMetersPerSecond * math.lerp(SparkSpeedScaleMin, SparkSpeedScaleMax, intensity01),
                    Life = SparkLife01,
                    Seed = seed
                };
                appended++;
                jobState[JobStateFlagsIndex] |= (int)SparkActiveFlag;
            }

            return appended;
        }

        private void AdvanceSparkGates(float deltaTimeSeconds)
        {
            float dt = math.isfinite(deltaTimeSeconds) ? math.max(0f, deltaTimeSeconds) : 0f;
            for (int i = 0; i < SparkToolGateSlotCount; i++)
            {
                float remaining = _sparkGateCooldownSeconds[i] - dt;
                _sparkGateCooldownSeconds[i] = remaining > 0f ? remaining : 0f;
            }
        }

        private void ResetSparkGates()
        {
            for (int i = 0; i < SparkToolGateSlotCount; i++)
            {
                _sparkGateToolHash[i] = 0u;
                _sparkGateCooldownSeconds[i] = 0f;
            }
        }

        /// <summary>
        /// Fixed-slot per-tool rate gate. Hash 0 is the free-slot sentinel, so a zero tool hash is folded to 1.
        /// </summary>
        private bool TryOpenSparkGate(uint toolHash)
        {
            uint key = toolHash == 0u ? 1u : toolHash;
            int reusableSlot = -1;
            for (int i = 0; i < SparkToolGateSlotCount; i++)
            {
                if (_sparkGateToolHash[i] == key)
                {
                    if (_sparkGateCooldownSeconds[i] > 0f)
                        return false;

                    _sparkGateCooldownSeconds[i] = SparkEmitIntervalSeconds;
                    return true;
                }

                if (reusableSlot < 0 && (_sparkGateToolHash[i] == 0u || _sparkGateCooldownSeconds[i] <= 0f))
                    reusableSlot = i;
            }

            if (reusableSlot < 0)
                return false;

            _sparkGateToolHash[reusableSlot] = key;
            _sparkGateCooldownSeconds[reusableSlot] = SparkEmitIntervalSeconds;
            return true;
        }

        private static float3 ResolveSparkEjectionAxis(in VfxSparkRequestSignal spark, uint seed)
        {
            float3 normal = spark.Normal;
            float lengthSq = math.lengthsq(normal);
            if (lengthSq > 0.0001f && math.all(math.isfinite(normal)))
                return normal * math.rsqrt(lengthSq);

            return BuildSignalEjectionAxis(seed);
        }

        private static uint BuildSparkSeed(uint frame, in VfxSparkRequestSignal spark, int signalIndex)
        {
            uint hash = 2166136261u;
            hash = (hash ^ frame) * 16777619u;
            hash = (hash ^ (uint)signalIndex) * 16777619u;
            hash = (hash ^ spark.ToolHash) * 16777619u;
            hash = (hash ^ spark.MaterialHash) * 16777619u;
            hash = (hash ^ spark.Frame) * 16777619u;
            hash = (hash ^ math.asuint(spark.HitPoint.x)) * 16777619u;
            hash = (hash ^ math.asuint(spark.HitPoint.y)) * 16777619u;
            hash = (hash ^ math.asuint(spark.HitPoint.z)) * 16777619u;
            hash = (hash ^ math.asuint(spark.Intensity01)) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static float3 BuildSignalEjectionAxis(uint seed)
        {
            float3 axis = new float3(
                HashToSigned01(seed ^ 0x9E3779B9u),
                math.abs(HashToSigned01(seed ^ 0x85EBCA6Bu)) + 0.2f,
                HashToSigned01(seed ^ 0xC2B2AE35u));
            float lengthSq = math.lengthsq(axis);
            return lengthSq > 0.0001f && math.all(math.isfinite(axis))
                ? axis * math.rsqrt(lengthSq)
                : new float3(0f, 1f, 0f);
        }

        private static float HashToSigned01(uint value)
        {
            value ^= value >> 16;
            value *= 2246822519u;
            value ^= value >> 13;
            value *= 3266489917u;
            value ^= value >> 16;
            return ((value & 0x00FFFFFFu) * (1f / 8388607.5f)) - 1f;
        }

        private static bool IsFiniteCarveEvent(in VoxelCarveEvent carveEvent)
        {
            return math.all(math.isfinite(carveEvent.AbsoluteHitPoint)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteSegmentEnd)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteHalfExtents)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteImpulseDirection)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteHitPointDouble)) &&
                   math.all(math.isfinite(carveEvent.AbsoluteSegmentEndDouble)) &&
                   math.isfinite(carveEvent.RadiusMeters) &&
                   math.isfinite(carveEvent.BlendStrengthMeters);
        }

        private static bool TryResolveCarveDebrisRadius(in VoxelCarveEvent carveEvent, out float radiusMeters)
        {
            radiusMeters = 0f;
            if (!IsFiniteCarveEvent(in carveEvent) ||
                !IsSupportedCarveOperation(carveEvent.Operation) ||
                !IsSupportedCarveShape(carveEvent.Shape) ||
                carveEvent.Operation != (byte)VoxelCarveOperationType.Subtract)
            {
                return false;
            }

            float resolvedRadius = math.max(0f, carveEvent.RadiusMeters);
            if (carveEvent.Shape == (byte)VoxelCarveShapeType.Box)
            {
                resolvedRadius = math.max(resolvedRadius, math.cmax(math.abs(carveEvent.AbsoluteHalfExtents)));
            }

            resolvedRadius = math.max(resolvedRadius, math.max(0f, carveEvent.BlendStrengthMeters));
            if (resolvedRadius <= 0f)
                return false;

            radiusMeters = resolvedRadius;
            return true;
        }

        private static bool IsSupportedCarveOperation(byte operation)
        {
            return operation <= (byte)VoxelCarveOperationType.Replace;
        }

        private static bool IsSupportedCarveShape(byte shape)
        {
            return shape <= (byte)VoxelCarveShapeType.Capsule;
        }

        private static uint BuildStableSeed(uint frame, in VoxelCarveEvent carveEvent, int eventIndex)
        {
            uint hash = 2166136261u;
            hash = (hash ^ frame) * 16777619u;
            hash = (hash ^ (uint)eventIndex) * 16777619u;
            hash = (hash ^ (uint)carveEvent.VolumeInstanceId) * 16777619u;
            hash = (hash ^ (uint)((ulong)carveEvent.VolumeInstanceId >> 32)) * 16777619u;
            double3 absoluteHitPoint = ResolveCarveHitPointDouble(in carveEvent);
            double3 absoluteSegmentEnd = ResolveCarveSegmentEndDouble(in carveEvent);
            hash = MixDouble(hash, absoluteHitPoint.x);
            hash = MixDouble(hash, absoluteHitPoint.y);
            hash = MixDouble(hash, absoluteHitPoint.z);
            hash = MixDouble(hash, absoluteSegmentEnd.x);
            hash = MixDouble(hash, absoluteSegmentEnd.y);
            hash = MixDouble(hash, absoluteSegmentEnd.z);
            hash = (hash ^ math.asuint(carveEvent.AbsoluteHalfExtents.x)) * 16777619u;
            hash = (hash ^ math.asuint(carveEvent.AbsoluteHalfExtents.y)) * 16777619u;
            hash = (hash ^ math.asuint(carveEvent.AbsoluteHalfExtents.z)) * 16777619u;
            hash = (hash ^ math.asuint(carveEvent.RadiusMeters)) * 16777619u;
            hash = (hash ^ math.asuint(carveEvent.BlendStrengthMeters)) * 16777619u;
            hash = (hash ^ carveEvent.Operation) * 16777619u;
            hash = (hash ^ carveEvent.Shape) * 16777619u;
            hash = (hash ^ carveEvent.MaterialId) * 16777619u;
            hash = (hash ^ carveEvent.SourceFlags) * 16777619u;
            return hash == 0u ? 1u : hash;
        }

        private static double3 ResolveCarveHitPointDouble(in VoxelCarveEvent carveEvent)
        {
            return ResolveCarveCoordinateDouble(carveEvent.AbsoluteHitPointDouble, carveEvent.AbsoluteHitPoint);
        }

        private static double3 ResolveCarveSegmentEndDouble(in VoxelCarveEvent carveEvent)
        {
            return ResolveCarveCoordinateDouble(carveEvent.AbsoluteSegmentEndDouble, carveEvent.AbsoluteSegmentEnd);
        }

        private static double3 ResolveCarveCoordinateDouble(double3 preciseCoordinate, float3 legacyCoordinate)
        {
            if (math.all(math.isfinite(preciseCoordinate)) &&
                (math.any(preciseCoordinate != double3.zero) || math.all(legacyCoordinate == float3.zero)))
            {
                return preciseCoordinate;
            }

            return new double3(legacyCoordinate.x, legacyCoordinate.y, legacyCoordinate.z);
        }

        private static float3 ResolveCarveEjectionAxis(in VoxelCarveEvent carveEvent)
        {
            float3 impulse = carveEvent.AbsoluteImpulseDirection;
            float impulseLengthSq = math.lengthsq(impulse);
            if (math.all(math.isfinite(impulse)) && impulseLengthSq > 0.0001f)
                return -impulse * math.rsqrt(impulseLengthSq);

            double3 segmentDelta = ResolveCarveHitPointDouble(in carveEvent) - ResolveCarveSegmentEndDouble(in carveEvent);
            if (math.all(math.isfinite(segmentDelta)))
            {
                double segmentLengthSq = math.lengthsq(segmentDelta);
                if (segmentLengthSq > 0.000001)
                {
                    float3 axis = new float3((float)segmentDelta.x, (float)segmentDelta.y, (float)segmentDelta.z);
                    float axisLengthSq = math.lengthsq(axis);
                    if (math.all(math.isfinite(axis)) && axisLengthSq > 0.0001f)
                        return axis * math.rsqrt(axisLengthSq);
                }
            }

            return new float3(0f, 1f, 0f);
        }

        private void InvalidateDrawMeshCache()
        {
            _cachedDrawMesh = null;
            _cachedDrawIndexCount = 0u;
            _cachedDrawIndexStart = 0u;
            _cachedDrawBaseVertex = 0u;
            _cachedDrawMeshValid = false;
        }

        private static uint MixDouble(uint hash, double value)
        {
            long bits = BitConverter.DoubleToInt64Bits(value);
            hash = (hash ^ unchecked((uint)bits)) * 16777619u;
            return (hash ^ unchecked((uint)(bits >> 32))) * 16777619u;
        }

        private void WriteBlackBox(
            int queuedCarves,
            int injectedParticles,
            float qualityPressure01,
            NativeArray<int> jobState,
            NativeArray<CarveDebrisTelemetryEntry> blackBox)
        {
            if (!blackBox.IsCreated || blackBox.Length == 0 || !jobState.IsCreated)
                return;

            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastTelemetryFrame == frame)
                return;

            _lastTelemetryFrame = frame;
            uint flags = (uint)math.max(0, jobState[JobStateFlagsIndex]);
            byte qualityPressureQ8 = EncodeQualityPressureQ8(qualityPressure01);
            flags |= _lastSdfActive ? SdfActiveFlag : 0u;
            flags |= _lastFlowActive ? FlowActiveFlag : 0u;
            flags |= _lastWakeActive ? WakeActiveFlag : 0u;
            flags |= _cachedSystemStress01 > StressRecycleThreshold01 ? StressRecycleFlag : 0u;
            float3 appliedAupShift = _lastAppliedAupShift;
            uint hash = BuildTelemetryHash(_activeMirrorCount, queuedCarves, injectedParticles, flags, qualityPressureQ8, appliedAupShift);
            blackBox[_blackBoxCursor] = new CarveDebrisTelemetryEntry
            {
                FrameIndex = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                ActiveCarveDebrisCount = _activeMirrorCount,
                QueuedCarves = queuedCarves,
                InjectedParticles = injectedParticles,
                Flags = flags,
                StateHash = hash,
                AppliedAupShift = appliedAupShift,
                QualityPressureQ8 = qualityPressureQ8
            };
            _blackBoxCursor = (_blackBoxCursor + 1) % blackBox.Length;

            if (_activeMirrorCount > 0 && (frame % TelemetryPublishStride) == 0)
                GlobalTelemetryBus.PublishPerformanceWarning(ActiveCountTelemetryHash, TelemetryContextHash, _activeMirrorCount);

            if ((flags & InvalidStateFlag) != 0u)
                DumpBlackBoxOnce(flags, blackBox);

            jobState[JobStateFlagsIndex] = 0;
            _lastAppliedAupShift = default;
        }

        private static byte EncodeQualityPressureQ8(float qualityPressure01)
        {
            return (byte)math.clamp((int)math.round(math.saturate(qualityPressure01) * 255f), 0, 255);
        }

        private static uint BuildTelemetryHash(int activeCount, int queuedCarves, int injectedParticles, uint flags, byte qualityPressureQ8, float3 appliedAupShift)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)activeCount) * 16777619u;
            hash = (hash ^ (uint)queuedCarves) * 16777619u;
            hash = (hash ^ (uint)injectedParticles) * 16777619u;
            hash = (hash ^ flags) * 16777619u;
            hash = (hash ^ qualityPressureQ8) * 16777619u;
            uint3 shiftBits = math.asuint(appliedAupShift);
            hash = (hash ^ shiftBits.x) * 16777619u;
            hash = (hash ^ shiftBits.y) * 16777619u;
            hash = (hash ^ shiftBits.z) * 16777619u;
            return hash;
        }

        private unsafe void DumpBlackBoxOnce(uint reasonFlags, NativeArray<CarveDebrisTelemetryEntry> blackBox)
        {
            if (_blackBoxDumped || !blackBox.IsCreated)
                return;

            NativeArray<byte> payload = default;
            int entrySize = UnsafeUtility.SizeOf<CarveDebrisTelemetryEntry>();
            int byteCount = sizeof(uint) * 5 + blackBox.Length * entrySize;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(CarveDebrisComputeRenderer),
                    "CarveDebrisTelemetryDumpPayload");
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                WriteUInt32LittleEndian(target, 0, DebrisBlackBoxDumpMagic);
                WriteUInt32LittleEndian(target, 4, (uint)blackBox.Length);
                WriteUInt32LittleEndian(target, 8, (uint)entrySize);
                WriteUInt32LittleEndian(target, 12, (uint)_blackBoxCursor);
                WriteUInt32LittleEndian(target, 16, reasonFlags);
                void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
                int offset = sizeof(uint) * 5;
                for (int i = 0; i < blackBox.Length; i++)
                {
                    int index = (_blackBoxCursor + i) % blackBox.Length;
                    void* entry = (byte*)source + (entrySize * index);
                    UnsafeUtility.MemCpy(target + offset, entry, entrySize);
                    offset += entrySize;
                }

                _blackBoxDumped = NativeFaultDumpWriter.TryWriteAll(DumpPath, payload, byteCount);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(CarveDebrisComputeRenderer),
                    "CarveDebrisTelemetryDumpPayload");
            }
        }

        private static void WriteUInt32LittleEndian(Span<byte> buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static unsafe void WriteUInt32LittleEndian(byte* buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private void ReleaseGpuState()
        {
            ReleaseBuffer(ref _positionBufferA);
            ReleaseBuffer(ref _positionBufferB);
            ReleaseBuffer(ref _velocityBufferA);
            ReleaseBuffer(ref _velocityBufferB);
            ReleaseBuffer(ref _visibleIndicesBuffer);
            ReleaseBuffer(ref _indirectArgsBuffer);
            ReleaseBuffer(ref _emptyFlowBuffer);
            DestroyOwnedMaterial();
            _cachedGlobalSdfTexture = null;
            _cachedGlobalSdfWorldToLocal = Matrix4x4.identity;
            _cachedGlobalSdfInvDoubleHalfExtents = Vector4.zero;
            _cachedGlobalSdfActive = 0f;
            _nextGlobalSdfRefreshFrame = 0;
            _fallbackRenderResourceRepairRequested = false;
            _nextMissingRegistryRefreshFrame = 0;
            _qualityPressure01 = 0f;
            _visualOverkill01 = 1f;
            InvalidateDrawMeshCache();
            _lastAppliedAupShift = default;
            _registryDataVault = null;
            _abyssalFlowGpuReadModel = null;
            _hotSwapRegistered = false;
            InvalidateDataVaultLease();
            _emptyTexture3D = null;
            _gpuReady = false;
            _activeMirrorCount = 0;
            _lastFlowActive = false;
            _lastSdfActive = false;
            _lastWakeActive = false;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer == null)
                return;

            buffer.Release();
            buffer = null;
        }

        private static void DestroyUnityObject(UnityEngine.Object unityObject)
        {
            if (unityObject == null)
                return;

            if (Application.isPlaying)
                UnityEngine.Object.Destroy(unityObject);
            else
                UnityEngine.Object.DestroyImmediate(unityObject);
        }

        private static void UploadRange<T>(GraphicsBuffer destination, NativeArray<T> source, int start, int count)
            where T : struct
        {
            if (destination == null || !destination.IsValid() || !source.IsCreated || count <= 0)
                return;

            int safeStart = math.clamp(start, 0, math.max(0, source.Length - 1));
            int safeCount = math.min(count, math.min(source.Length - safeStart, destination.count - safeStart));
            if (safeCount <= 0)
                return;

            GraphicsBufferUploadUtility.UploadNativeArraySetDataRange(destination, source, safeStart, safeStart, safeCount);
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AgeCarveDebrisMirrorJob : IJob
        {
            [NoAlias] public NativeArray<float4> Positions;
            public int Capacity;
            public float LifeDelta;
            [NoAlias] public NativeArray<int> JobState;

            public void Execute()
            {
                if (!JobState.IsCreated || JobState.Length <= JobStateFlagsIndex)
                    return;

                int flags = JobState[JobStateFlagsIndex];
                if (!Positions.IsCreated || Capacity <= 0)
                {
                    JobState[JobStateActiveIndex] = 0;
                    JobState[JobStateInjectedIndex] = 0;
                    JobState[JobStateDirtyMinIndex] = math.max(0, Capacity);
                    JobState[JobStateDirtyMaxIndex] = -1;
                    JobState[JobStateFlagsIndex] = flags | (int)InvalidStateFlag;
                    return;
                }

                int active = 0;
                int count = math.min(Capacity, Positions.Length);
                for (int i = 0; i < count; i++)
                {
                    float4 particle = Positions[i];
                    if (particle.w <= 0f)
                        continue;

                    if (!math.all(math.isfinite(particle)))
                    {
                        Positions[i] = default;
                        flags |= (int)InvalidStateFlag;
                        continue;
                    }

                    particle.w = math.max(0f, particle.w - LifeDelta);
                    Positions[i] = particle;
                    active += particle.w > 0f ? 1 : 0;
                }

                JobState[JobStateActiveIndex] = active;
                JobState[JobStateInjectedIndex] = 0;
                JobState[JobStateDirtyMinIndex] = Capacity;
                JobState[JobStateDirtyMaxIndex] = -1;
                JobState[JobStateFlagsIndex] = flags;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CarveDebrisInjectBatchJob : IJob
        {
            [NoAlias] public NativeArray<float4> Positions;
            [NoAlias] public NativeArray<float4> Velocities;
            [ReadOnly, NoAlias] public NativeArray<CarveDebrisRequest> Requests;
            public int RequestCount;
            public int Capacity;
            [NoAlias] public NativeArray<int> JobState;

            public void Execute()
            {
                if (!JobState.IsCreated || JobState.Length <= JobStateFlagsIndex)
                    return;

                int flags = JobState[JobStateFlagsIndex];
                if (!Positions.IsCreated || !Velocities.IsCreated || Capacity <= 0)
                {
                    JobState[JobStateActiveIndex] = 0;
                    JobState[JobStateInjectedIndex] = 0;
                    JobState[JobStateDirtyMinIndex] = math.max(0, Capacity);
                    JobState[JobStateDirtyMaxIndex] = -1;
                    JobState[JobStateFlagsIndex] = flags | (int)InvalidStateFlag;
                    return;
                }

                int count = math.min(Capacity, math.min(Positions.Length, Velocities.Length));
                int injectedTotal = 0;
                int active = math.clamp(JobState[JobStateActiveIndex], 0, count);
                int dirtyMin = count;
                int dirtyMax = -1;
                int safeRequestCount = Requests.IsCreated ? math.min(math.max(0, RequestCount), Requests.Length) : 0;
                int requestedTotal = 0;

                for (int requestIndex = 0; requestIndex < safeRequestCount; requestIndex++)
                {
                    CarveDebrisRequest request = Requests[requestIndex];
                    if (!math.all(math.isfinite(request.Center)) ||
                        !math.all(math.isfinite(request.EjectionAxis)) ||
                        !math.isfinite(request.Radius) ||
                        request.Radius <= 0f ||
                        request.ParticlesToInject <= 0)
                    {
                        flags |= (int)InvalidStateFlag;
                        continue;
                    }

                    requestedTotal = math.min(count, requestedTotal + math.clamp(request.ParticlesToInject, 0, count));
                }

                // GPU advection owns live positions; the CPU upload may only cover one dead contiguous span.
                int bestStart = -1;
                int bestLength = 0;
                int currentStart = -1;
                int currentLength = 0;
                for (int i = 0; i < count && bestLength < requestedTotal; i++)
                {
                    if (Positions[i].w <= 0f)
                    {
                        if (currentLength == 0)
                            currentStart = i;

                        currentLength++;
                        if (currentLength > bestLength)
                        {
                            bestStart = currentStart;
                            bestLength = currentLength;
                        }

                        continue;
                    }

                    currentStart = -1;
                    currentLength = 0;
                }

                if (requestedTotal <= 0 || bestStart < 0 || bestLength <= 0)
                {
                    JobState[JobStateActiveIndex] = active;
                    JobState[JobStateInjectedIndex] = 0;
                    JobState[JobStateDirtyMinIndex] = dirtyMin;
                    JobState[JobStateDirtyMaxIndex] = dirtyMax;
                    JobState[JobStateFlagsIndex] = flags;
                    return;
                }

                int writeIndex = bestStart;
                int writeEnd = math.min(count, bestStart + math.min(bestLength, requestedTotal));
                for (int requestIndex = 0; requestIndex < safeRequestCount && writeIndex < writeEnd; requestIndex++)
                {
                    CarveDebrisRequest request = Requests[requestIndex];
                    if (!math.all(math.isfinite(request.Center)) ||
                        !math.all(math.isfinite(request.EjectionAxis)) ||
                        !math.isfinite(request.Radius) ||
                        request.Radius <= 0f ||
                        request.ParticlesToInject <= 0)
                    {
                        continue;
                    }

                    Unity.Mathematics.Random random = new Unity.Mathematics.Random(request.Seed == 0u ? 1u : request.Seed);
                    int requested = math.clamp(request.ParticlesToInject, 0, count);
                    int injectedForRequest = 0;
                    float safeRadius = math.max(0.025f, request.Radius);
                    float safeSpeed = math.max(0f, request.InitialSpeed);
                    float safeLife = math.max(0.001f, request.Life);
                    float3 ejectionAxis = request.EjectionAxis;
                    float ejectionLengthSq = math.lengthsq(ejectionAxis);
                    ejectionAxis = ejectionLengthSq > 0.0001f ? ejectionAxis * math.rsqrt(ejectionLengthSq) : new float3(0f, 1f, 0f);
                    for (; writeIndex < writeEnd && injectedForRequest < requested; writeIndex++)
                    {
                        if (Positions[writeIndex].w > 0f)
                        {
                            flags |= (int)InvalidStateFlag;
                            break;
                        }

                        float3 raw = new float3(
                            random.NextFloat(-1f, 1f),
                            random.NextFloat(-0.15f, 1f),
                            random.NextFloat(-1f, 1f));
                        float lengthSq = math.lengthsq(raw);
                        float3 randomDirection = lengthSq > 0.0001f ? raw * math.rsqrt(lengthSq) : ejectionAxis;
                        float coneBias = random.NextFloat(0.42f, 0.82f);
                        float3 biasedDirection = randomDirection * (1f - coneBias) + ejectionAxis * coneBias;
                        float biasedLengthSq = math.lengthsq(biasedDirection);
                        float3 direction = biasedLengthSq > 0.0001f ? biasedDirection * math.rsqrt(biasedLengthSq) : ejectionAxis;
                        float radius = safeRadius * random.NextFloat(0.05f, 1f);
                        float speed = safeSpeed * random.NextFloat(0.45f, 1.15f);
                        float3 position = request.Center + direction * radius;
                        float3 velocity = direction * speed + ejectionAxis * (safeSpeed * 0.35f);
                        if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
                        {
                            flags |= (int)InvalidStateFlag;
                            continue;
                        }

                        Positions[writeIndex] = new float4(position, safeLife);
                        Velocities[writeIndex] = new float4(velocity, 0f);
                        dirtyMin = math.min(dirtyMin, writeIndex);
                        dirtyMax = math.max(dirtyMax, writeIndex);
                        injectedForRequest++;
                        injectedTotal++;
                        active = math.min(count, active + 1);
                    }
                }

                JobState[JobStateActiveIndex] = active;
                JobState[JobStateInjectedIndex] = injectedTotal;
                JobState[JobStateDirtyMinIndex] = dirtyMin;
                JobState[JobStateDirtyMaxIndex] = dirtyMax;
                JobState[JobStateFlagsIndex] = flags;
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CarveDebrisRequest
        {
            [FieldOffset(0)]
            public float3 Center;

            [FieldOffset(12)]
            public float3 EjectionAxis;

            [FieldOffset(24)]
            public float Radius;

            [FieldOffset(28)]
            public int ParticlesToInject;

            [FieldOffset(32)]
            public float InitialSpeed;

            [FieldOffset(36)]
            public float Life;

            [FieldOffset(40)]
            public uint Seed;

            [FieldOffset(44)]
            private uint _pad0;

            [FieldOffset(48)]
            private uint _pad1;

            [FieldOffset(52)]
            private uint _pad2;

            [FieldOffset(56)]
            private uint _pad3;

            [FieldOffset(60)]
            private uint _pad4;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct CarveDebrisTelemetryEntry
        {
            [FieldOffset(0)]
            public uint FrameIndex;

            [FieldOffset(4)]
            public int ActiveCarveDebrisCount;

            [FieldOffset(8)]
            public int QueuedCarves;

            [FieldOffset(12)]
            public int InjectedParticles;

            [FieldOffset(16)]
            public uint Flags;

            [FieldOffset(20)]
            public uint StateHash;

            [FieldOffset(24)]
            public float3 AppliedAupShift;

            [FieldOffset(36)]
            public byte QualityPressureQ8;

            [FieldOffset(36)]
            private uint _pad0;

            [FieldOffset(40)]
            private uint _pad1;

            [FieldOffset(44)]
            private uint _pad2;

            [FieldOffset(48)]
            private uint _pad3;

            [FieldOffset(52)]
            private uint _pad4;

            [FieldOffset(56)]
            private uint _pad5;

            [FieldOffset(60)]
            private uint _pad6;
        }
    }
}
