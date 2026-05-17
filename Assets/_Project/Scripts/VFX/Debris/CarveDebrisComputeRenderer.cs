using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Core.Contracts.Signals;
using ScalabilityChangedEvent = Hecton8.Core.Contracts.Signals.ScalabilityChangedEvent;
using Hecton8.Physics;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.VFX.Debris
{
    /// <summary>
    /// GPU-only rock chip feedback for voxel SDF carve events.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CarveDebrisComputeRenderer : MonoBehaviour,
        IUpdatable,
        IDebrisComputeService,
        IGlobalRegistryHotSwapListener,
        IGlobalRegistryHotSwapRefListener,
        IScalabilityChangedEventListener
    {
        private const int MaxCarveDebrisCount = 16384;
        private const int LowTierActiveCarveDebrisCount = 1024;
        private const int MidTierActiveCarveDebrisCount = 4096;
        private const int ThreadGroupFallbackSize = 64;
        private const int BlackBoxCapacity = 300;
        private const int JobStateLength = 5;
        private const int JobStateActiveIndex = 0;
        private const int JobStateInjectedIndex = 1;
        private const int JobStateDirtyMinIndex = 2;
        private const int JobStateDirtyMaxIndex = 3;
        private const int JobStateFlagsIndex = 4;
        private const int LowTierParticlesPerCarve = 16;
        private const int MidTierParticlesPerCarve = 48;
        private const int HighTierParticlesPerCarve = 128;
        private const int MaxCarveSignalsPerFrame = 32;
        private const int MaxCarveSignalScanPerFrame = 64;
        private const int MaxDebrisSpawnSignalScanPerFrame = 64;
        private const int TelemetryPublishStride = 30;
        private const int GlobalSdfRefreshStrideFrames = 4;
        private const int TierSwitchConfirmFrames = 120;
        private const int MissingRegistryRefreshStrideFrames = 30;
        private const float MinimumCarveSpawnRadiusMeters = 0.05f;
        private const float StressRecycleThreshold01 = 0.9f;
        private const float StressRecycleLifetimeMultiplier = 4f;
        private const string DebrisShaderName = "Hecton8/VFX/CarveDebrisIndirect";
#if UNITY_EDITOR
        private const string FluidAdvectionComputeAssetPath = "Assets/_Project/Art/Shaders/Hecton_FluidAdvection.compute";
#endif
        private const uint TelemetryContextHash = 0x56465844u; // VFXD
        private const uint ActiveCountTelemetryHash = 0x43444252u; // CDBR
        private const uint InvalidStateFlag = 1u;
        private const uint LowTierFlag = 1u << 1;
        private const uint SdfActiveFlag = 1u << 2;
        private const uint FlowActiveFlag = 1u << 3;
        private const uint StressRecycleFlag = 1u << 4;
        private const uint WakeActiveFlag = 1u << 5;
        private const string DumpPath = "Docs/AgentLogs/Dump_DEBRIS_PHYSICS_FAKE.bin";

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
        private static readonly int GlobalWakeParamsId = Shader.PropertyToID("_GlobalWakeParams");
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

        private VaultBufferHandle<float4> _debrisPositionsHandle;
        private VaultBufferHandle<float4> _debrisVelocitiesHandle;
        private VaultBufferHandle<CarveDebrisRequest> _carveRequestsHandle;
        private VaultBufferHandle<int> _jobStateHandle;
        private VaultBufferHandle<CarveDebrisTelemetryEntry> _blackBoxHandle;
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
        private Mesh _ownedMesh;
        private Material _ownedMaterial;
        private Material _ownedMaterialSource;
        private Material _boundRenderMaterial;
        private GraphicsBuffer _boundVisibleIndicesBuffer;
        private Vector4 _boundMaterialParams;
        private bool _boundMaterialParamsValid;
        private HectonFluidEngine _fluidEngine;
        private int _advectKernel = -1;
        private int _clearArgsKernel = -1;
        private int _cullKernel = -1;
        private int _threadGroupSize = ThreadGroupFallbackSize;
        private int _maxDispatchGroups = MaxCarveDebrisCount >> 6;
        private int _lowDispatchGroups = LowTierActiveCarveDebrisCount >> 6;
        private int _lastActiveCapacity = MaxCarveDebrisCount;
        private int _nextGlobalSdfRefreshFrame;
        private int _nextMissingRegistryRefreshFrame;
        private int _pendingTierFrames;
        private int _bufferParity;
        private int _activeMirrorCount;
        private int _blackBoxCursor;
        private int _lastTelemetryFrame = -1;
        private uint _lastProcessedAupShiftFrameId;
        private uint _frameSequence;
        private uint _positionVaultGeneration;
        private uint _velocityVaultGeneration;
        private uint _jobStateVaultGeneration;
        private uint _requestVaultGeneration;
        private uint _blackBoxVaultGeneration;
        private uint _cachedDrawIndexCount;
        private uint _cachedDrawIndexStart;
        private uint _cachedDrawBaseVertex;
        private float3 _pendingAupShift;
        private float3 _lastAppliedAupShift;
        private Matrix4x4 _cachedGlobalSdfWorldToLocal = Matrix4x4.identity;
        private Vector4 _cachedGlobalSdfInvDoubleHalfExtents;
        private Mesh _cachedDrawMesh;
        private float _lastDeltaTime = 1f / 60f;
        private float _cachedSystemStress01;
        private float _cachedGlobalSdfActive;
        private bool _registered;
        private bool _gpuReady;
        private bool _blackBoxDumped;
        private bool _materialFallbackAttempted;
        private bool _lastFlowActive;
        private bool _lastSdfActive;
        private bool _lastWakeActive;
        private bool _cachedDrawMeshValid;
        private bool _cachedLowTier = true;
        private bool _pendingLowTier = true;
        private bool _sampledLowTier = true;
        private bool _cachedHighEndTier;
        private bool _pendingHighEndTier;
        private bool _sampledHighEndTier;
        private bool _forceLowMemoryProfile;
        private bool _tierCacheInitialized;
        private bool _hotSwapRegistered;
        private bool _scalabilityEventsRegistered;
        private bool _computeServiceRegistered;

        private void Awake()
        {
            EnsureFallbackRenderResources();
        }

        private void OnEnable()
        {
            EnsureFallbackRenderResources();
            TryRegisterComputeService();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityEvents();
            TryRegisterTick();
            TryEnsureGpuState();
        }

        private void Start()
        {
            TryRegisterComputeService();
            TryRegisterHotSwapListener();
            TryRegisterScalabilityEvents();
            TryRegisterTick();
            TryEnsureGpuState();
        }

        private void OnDisable()
        {
            TryUnregisterComputeService();
            TryUnregisterScalabilityEvents();
            TryUnregisterHotSwapListener();
            if (_registered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registered = false;
            }

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
        public int ActiveParticleCapacity => ResolveActiveCapacity(_cachedLowTier, _cachedHighEndTier);

        /// <inheritdoc />
        public bool IsLowTierActive => _cachedLowTier;

        /// <inheritdoc />
        public void ClearGpuDebris()
        {
            _activeMirrorCount = 0;
            _pendingAupShift = default;
            _lastAppliedAupShift = default;
            _lastFlowActive = false;
            _lastSdfActive = false;
            _lastWakeActive = false;
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

        public void Tick(float deltaTime)
        {
            if (!enabled)
                return;

            if (!TryEnsureGpuState())
                return;

            float dt = math.clamp(deltaTime, 0.0001f, 0.0666667f);
            _lastDeltaTime = dt;
            _cachedSystemStress01 = ResolveSystemStress01();
            bool lowTier = IsLowTier();
            bool highEndTier = _cachedHighEndTier;
            int activeCapacity = ResolveActiveCapacity(lowTier, highEndTier);
            if (!TryResolveVaultBuffers(
                    out var debrisPositions,
                    out var debrisVelocities,
                    out var carveRequests,
                    out var jobState,
                    out var blackBox))
            {
                _gpuReady = false;
                return;
            }

            ApplyCapacityShed(activeCapacity, debrisPositions, debrisVelocities);
            DrainAupShiftSignals(jobState);
            if (_activeMirrorCount > 0)
                AgeMirror(dt, activeCapacity, ResolveLifetimeRcp(), debrisPositions, jobState);
            else
                ResetFrameJobState(activeCapacity, jobState);

            int queuedCarves = DrainCarveSignals(lowTier, highEndTier, activeCapacity, debrisPositions, debrisVelocities, carveRequests, jobState);
            DispatchGpu(dt, lowTier, activeCapacity);
            WriteBlackBox(queuedCarves, jobState.IsCreated ? jobState[JobStateInjectedIndex] : 0, lowTier, jobState, blackBox);
            RenderDebris();
            _frameSequence++;
        }

        private void TryRegisterTick()
        {
            if (_registered)
                return;

            _registered = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);
        }

        private void TryRegisterComputeService()
        {
            IDebrisComputeService registered = GlobalRegistry.DebrisCompute;
            if (registered != null && !ReferenceEquals(registered, this))
                return;

            GlobalRegistry.RegisterDebrisComputeService(this);
            _computeServiceRegistered = ReferenceEquals(GlobalRegistry.DebrisCompute, this);
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
            _registryDataVault = GlobalRegistry.DataVault;
            _fluidEngine = GlobalRegistry.Fluid;
            _nextMissingRegistryRefreshFrame = Time.frameCount + MissingRegistryRefreshStrideFrames;
        }

        private void RefreshMissingRegistryServicesIfNeeded()
        {
            if (_registryDataVault != null && _fluidEngine != null)
                return;

            int frame = Time.frameCount;
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

        void IScalabilityChangedEventListener.OnScalabilityChanged(in ScalabilityChangedEvent _)
        {
            RefreshScalabilityTierCandidate();
        }

        private void TryRegisterScalabilityEvents()
        {
            if (!_scalabilityEventsRegistered)
            {
                ScalabilityEvents.Register(this);
                _scalabilityEventsRegistered = true;
            }

            RefreshScalabilityTierSeed();
        }

        private void TryUnregisterScalabilityEvents()
        {
            if (!_scalabilityEventsRegistered)
                return;

            ScalabilityEvents.Unregister(this);
            _scalabilityEventsRegistered = false;
        }

        private void RefreshScalabilityTierSeed()
        {
            RefreshScalabilityTierCandidate();
            if (_tierCacheInitialized)
                return;

            _cachedLowTier = _sampledLowTier;
            _pendingLowTier = _sampledLowTier;
            _cachedHighEndTier = _sampledHighEndTier;
            _pendingHighEndTier = _sampledHighEndTier;
            _pendingTierFrames = 0;
            _tierCacheInitialized = true;
        }

        private void RefreshScalabilityTierCandidate()
        {
            _forceLowMemoryProfile = GlobalRegistry.H8_LOW_MEMORY_PROFILE;
            QueueScalabilityTierCandidate(
                GlobalRegistry.ScalabilityTierProfileByte,
                GlobalRegistry.ScalabilityTier,
                _forceLowMemoryProfile);
        }

        private void QueueScalabilityTierCandidate(byte tierProfile, HectonQualityTier qualityTier, bool lowMemoryProfile)
        {
            _sampledLowTier = IsLowTierPayload(tierProfile, qualityTier) || lowMemoryProfile;
            _sampledHighEndTier = !_sampledLowTier && IsHighEndPayload(qualityTier);
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
                _fluidEngine = currentService as HectonFluidEngine;
        }

        private bool TryEnsureGpuState()
        {
            if (_gpuReady && IsGpuStateValid())
                return true;

            _gpuReady = false;
            if (fluidAdvectionCompute == null)
                return false;

            RefreshMissingRegistryServicesIfNeeded();
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

            fluidAdvectionCompute.GetKernelThreadGroupSizes(_advectKernel, out uint kernelThreads, out _, out _);
            _threadGroupSize = kernelThreads > 0u ? (int)math.min(kernelThreads, 1024u) : ThreadGroupFallbackSize;
            _maxDispatchGroups = ResolveDispatchGroups(MaxCarveDebrisCount, _threadGroupSize);
            _lowDispatchGroups = ResolveDispatchGroups(LowTierActiveCarveDebrisCount, _threadGroupSize);

            _debrisPositionsHandle = vault.GetBufferHandle<float4>(
                BufferID.CarveDebris,
                MaxCarveDebrisCount,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            _debrisVelocitiesHandle = vault.GetBufferHandle<float4>(
                BufferID.CarveDebrisVelocity,
                MaxCarveDebrisCount,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            _jobStateHandle = vault.GetBufferHandle<int>(
                BufferID.CarveDebrisJobState,
                JobStateLength,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            _blackBoxHandle = vault.GetBufferHandle<CarveDebrisTelemetryEntry>(
                BufferID.CarveDebrisBlackBox,
                BlackBoxCapacity,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);
            _carveRequestsHandle = vault.GetBufferHandle<CarveDebrisRequest>(
                BufferID.CarveDebrisRequests,
                MaxCarveSignalsPerFrame,
                SystemID.Vfx,
                NativeArrayOptions.ClearMemory);

            _dataVault = vault;
            if (!TryResolveVaultBuffers(
                    out var debrisPositions,
                    out var debrisVelocities,
                    out var carveRequests,
                    out var jobState,
                    out var blackBox))
            {
                InvalidateDataVaultLease();
                return false;
            }

            if (!TryCaptureVaultGenerations(vault))
                return false;

            AllocateGraphicsBuffers();
            CreateEmptyResources();
            ClearMirrorsAndUpload(debrisPositions, debrisVelocities, carveRequests, jobState, blackBox);
            _gpuReady = IsGpuStateValid();
            return _gpuReady;
        }

        private static int ResolveKernel(ComputeShader compute, string kernelName)
        {
            return compute != null && compute.HasKernel(kernelName) ? compute.FindKernel(kernelName) : -1;
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

        private bool TryCaptureVaultGenerations(IDataVault vault)
        {
            if (vault == null ||
                !vault.TryGetBufferGeneration(BufferID.CarveDebris, out _positionVaultGeneration) ||
                !vault.TryGetBufferGeneration(BufferID.CarveDebrisVelocity, out _velocityVaultGeneration) ||
                !vault.TryGetBufferGeneration(BufferID.CarveDebrisJobState, out _jobStateVaultGeneration) ||
                !vault.TryGetBufferGeneration(BufferID.CarveDebrisRequests, out _requestVaultGeneration) ||
                !vault.TryGetBufferGeneration(BufferID.CarveDebrisBlackBox, out _blackBoxVaultGeneration))
            {
                InvalidateDataVaultLease();
                return false;
            }

            return true;
        }

        private void InvalidateDataVaultLease()
        {
            _dataVault = null;
            _debrisPositionsHandle = default;
            _debrisVelocitiesHandle = default;
            _carveRequestsHandle = default;
            _jobStateHandle = default;
            _blackBoxHandle = default;
            _positionVaultGeneration = 0u;
            _velocityVaultGeneration = 0u;
            _jobStateVaultGeneration = 0u;
            _requestVaultGeneration = 0u;
            _blackBoxVaultGeneration = 0u;
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

            if (!vault.TryGetBufferHandle(BufferID.CarveDebris, out _debrisPositionsHandle) ||
                !vault.TryGetBufferHandle(BufferID.CarveDebrisVelocity, out _debrisVelocitiesHandle) ||
                !vault.TryGetBufferHandle(BufferID.CarveDebrisRequests, out _carveRequestsHandle) ||
                !vault.TryGetBufferHandle(BufferID.CarveDebrisJobState, out _jobStateHandle) ||
                !vault.TryGetBufferHandle(BufferID.CarveDebrisBlackBox, out _blackBoxHandle))
            {
                return false;
            }

            debrisPositions = _debrisPositionsHandle.Resolve(vault);
            debrisVelocities = _debrisVelocitiesHandle.Resolve(vault);
            carveRequests = _carveRequestsHandle.Resolve(vault);
            jobState = _jobStateHandle.Resolve(vault);
            blackBox = _blackBoxHandle.Resolve(vault);
            return debrisPositions.IsCreated &&
                   debrisVelocities.IsCreated &&
                   carveRequests.IsCreated &&
                   jobState.IsCreated &&
                   blackBox.IsCreated &&
                   debrisPositions.Length >= MaxCarveDebrisCount &&
                   debrisVelocities.Length >= MaxCarveDebrisCount &&
                   carveRequests.Length >= MaxCarveSignalsPerFrame &&
                   jobState.Length >= JobStateLength &&
                   blackBox.Length >= BlackBoxCapacity;
        }

        private bool IsDataVaultLeaseValid()
        {
            if (_dataVault == null ||
                !_debrisPositionsHandle.IsCreated ||
                !_debrisVelocitiesHandle.IsCreated ||
                !_jobStateHandle.IsCreated ||
                !_blackBoxHandle.IsCreated ||
                !_carveRequestsHandle.IsCreated ||
                _debrisPositionsHandle.Length < MaxCarveDebrisCount ||
                _debrisVelocitiesHandle.Length < MaxCarveDebrisCount ||
                _jobStateHandle.Length < JobStateLength ||
                _blackBoxHandle.Length < BlackBoxCapacity ||
                _carveRequestsHandle.Length < MaxCarveSignalsPerFrame ||
                _dataVault.IsCompactionFenceActive)
            {
                return false;
            }

            if (!_dataVault.TryGetBufferGeneration(BufferID.CarveDebris, out uint positionGeneration) ||
                !_dataVault.TryGetBufferGeneration(BufferID.CarveDebrisVelocity, out uint velocityGeneration) ||
                !_dataVault.TryGetBufferGeneration(BufferID.CarveDebrisJobState, out uint jobStateGeneration) ||
                !_dataVault.TryGetBufferGeneration(BufferID.CarveDebrisRequests, out uint requestGeneration) ||
                !_dataVault.TryGetBufferGeneration(BufferID.CarveDebrisBlackBox, out uint blackBoxGeneration) ||
                positionGeneration != _positionVaultGeneration ||
                velocityGeneration != _velocityVaultGeneration ||
                jobStateGeneration != _jobStateVaultGeneration ||
                requestGeneration != _requestVaultGeneration ||
                blackBoxGeneration != _blackBoxVaultGeneration)
            {
                return false;
            }

            return ReferenceEquals(_dataVault, _registryDataVault);
        }

        private void AllocateGraphicsBuffers()
        {
            if (_positionBufferA == null || !_positionBufferA.IsValid())
                _positionBufferA = CreateStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_positionBufferB == null || !_positionBufferB.IsValid())
                _positionBufferB = CreateStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_velocityBufferA == null || !_velocityBufferA.IsValid())
                _velocityBufferA = CreateStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_velocityBufferB == null || !_velocityBufferB.IsValid())
                _velocityBufferB = CreateStructuredBuffer<float4>(MaxCarveDebrisCount);
            if (_visibleIndicesBuffer == null || !_visibleIndicesBuffer.IsValid())
            {
                _visibleIndicesBuffer = CreateStructuredBuffer<uint>(MaxCarveDebrisCount);
                InvalidateRenderMaterialBindings();
            }
            if (_emptyFlowBuffer == null || !_emptyFlowBuffer.IsValid())
                _emptyFlowBuffer = CreateStructuredBuffer<float4>(1);
            if (_indirectArgsBuffer == null || !_indirectArgsBuffer.IsValid())
            {
                _indirectArgsBuffer = new GraphicsBuffer(
                    GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                    1,
                    GraphicsBuffer.IndirectDrawIndexedArgs.size); // COLD ALLOC: GraphicsBuffer[1] - compute-written indirect rock debris args - owner: VFX_SDF_CARVE_DEBRIS
            }
        }

        private static GraphicsBuffer CreateStructuredBuffer<T>(int count)
            where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                math.max(1, count),
                UnsafeUtility.SizeOf<T>()); // COLD ALLOC: GraphicsBuffer[count] - persistent carve debris GPU lane - owner: VFX_SDF_CARVE_DEBRIS
        }

        private void CreateEmptyResources()
        {
            if (_emptyTexture3D != null)
                return;

            _emptyTexture3D = new Texture3D(1, 1, 1, TextureFormat.RGBAFloat, false)
            {
                name = "Hecton Empty CarveDebris 3D Texture",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Point
            }; // COLD ALLOC: Texture3D[1] - zero fallback for unbound SDF/flow 3D textures - owner: VFX_SDF_CARVE_DEBRIS
            _emptyTexture3D.SetPixel(0, 0, 0, Color.clear);
            _emptyTexture3D.Apply(false, true);
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
            empty[0] = default;
            _emptyFlowBuffer.UnlockBufferAfterWrite<float4>(1);
        }

        private void AgeMirror(float dt, int activeCapacity, float lifetimeRcp, NativeArray<float4> debrisPositions, NativeArray<int> jobState)
        {
            if (!debrisPositions.IsCreated || !jobState.IsCreated)
                return;

            float lifeDelta = dt * lifetimeRcp;
            new AgeCarveDebrisMirrorJob
            {
                Positions = debrisPositions,
                Capacity = activeCapacity,
                LifeDelta = lifeDelta,
                JobState = jobState
            }.Run();
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
            bool lowTier,
            bool highEndTier,
            int activeCapacity,
            NativeArray<float4> debrisPositions,
            NativeArray<float4> debrisVelocities,
            NativeArray<CarveDebrisRequest> carveRequests,
            NativeArray<int> jobState)
        {
            ReadOnlySpan<VoxelCarveEvent> carveSignals = SignalBus<VoxelCarveEvent>.GetFrameSnapshot();
            int signalCount = math.min(carveSignals.Length, MaxCarveSignalScanPerFrame);
            int particlesPerCarve = ResolveParticlesPerCarve(lowTier, highEndTier);
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

            if (requestCount <= 0)
                return 0;

            new CarveDebrisInjectBatchJob
            {
                Positions = debrisPositions,
                Velocities = debrisVelocities,
                Requests = carveRequests,
                RequestCount = requestCount,
                Capacity = activeCapacity,
                JobState = jobState
            }.Run();

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

            UploadRange(_positionBufferA, debrisPositions, safeStart, safeCount);
            UploadRange(_positionBufferB, debrisPositions, safeStart, safeCount);
            UploadRange(_velocityBufferA, debrisVelocities, safeStart, safeCount);
            UploadRange(_velocityBufferB, debrisVelocities, safeStart, safeCount);
        }

        private void DispatchGpu(float dt, bool lowTier, int activeCapacity)
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
            int dispatchGroups = ResolveDispatchGroups(activeCapacity, _threadGroupSize);
            Vector4 drawArgs = drawArgsBase;
            drawArgs.w = activeCapacity;
            float3 appliedAupShift = _pendingAupShift;

            BindSharedComputeParams(dt, lowTier, activeCapacity, drawArgs);
            fluidAdvectionCompute.SetBuffer(_clearArgsKernel, CarveDebrisIndirectArgsId, _indirectArgsBuffer);
            fluidAdvectionCompute.Dispatch(_clearArgsKernel, 1, 1, 1);

            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisReadId, positionRead);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisWriteId, positionWrite);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisVelocityReadId, velocityRead);
            fluidAdvectionCompute.SetBuffer(_advectKernel, CarveDebrisVelocityWriteId, velocityWrite);
            fluidAdvectionCompute.Dispatch(_advectKernel, dispatchGroups, 1, 1);

            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisReadId, positionWrite);
            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisVisibleIndicesId, _visibleIndicesBuffer);
            fluidAdvectionCompute.SetBuffer(_cullKernel, CarveDebrisIndirectArgsId, _indirectArgsBuffer);
            fluidAdvectionCompute.Dispatch(_cullKernel, dispatchGroups, 1, 1);

            _bufferParity ^= 1;
            _lastAppliedAupShift = appliedAupShift;
            _pendingAupShift = default;
        }

        private void BindSharedComputeParams(float dt, bool lowTier, int activeCapacity, Vector4 drawArgs)
        {
            Camera camera = renderCamera;
            Vector3 cameraPosition = camera != null ? camera.transform.position : Vector3.zero;
            Vector3 cameraForward = camera != null ? camera.transform.forward : Vector3.zero;
            float cullForwardDot = lowTier ? 0f : -0.25f;
            float renderDistanceSq = camera != null && renderDistanceMeters > 0f ? renderDistanceMeters * renderDistanceMeters : 0f;
            GraphicsBuffer flowBuffer = _emptyFlowBuffer;
            Texture flowTexture = _emptyTexture3D;
            Vector4 gridResolution = Vector4.zero;
            Vector4 flowCenter = Vector4.zero;
            Vector4 flowSpacing = Vector4.zero;
            Vector4 flowTextureParams = Vector4.zero;
            float flowTextureActive = 0f;
            float flowBufferActive = 0f;
            if (!lowTier)
            {
                flowBuffer = ResolveFlowPayload(
                    out flowTexture,
                    out gridResolution,
                    out flowCenter,
                    out flowSpacing,
                    out flowTextureParams,
                    out flowTextureActive,
                    out flowBufferActive);
            }

            Texture sdfTexture = ResolveSdfTexture(lowTier, out Matrix4x4 sdfWorldToLocal, out Vector4 sdfInvDoubleHalfExtents, out float sdfActive);
            float flowActive = flowBufferActive > 0.5f || flowTextureActive > 0.5f ? 1f : 0f;
            _lastFlowActive = flowActive > 0.5f;
            _lastSdfActive = sdfActive > 0.5f;

            fluidAdvectionCompute.SetVector(CarveDebrisCountsId, new Vector4(activeCapacity, _activeMirrorCount, activeCapacity, _frameSequence));
            fluidAdvectionCompute.SetVector(CarveDebrisParamsId, new Vector4(dt, lowTier ? 1f : 0f, sdfActive, dragToFlow));
            float lifetimeRcp = ResolveLifetimeRcp();
            fluidAdvectionCompute.SetVector(CarveDebrisForcesId, new Vector4(gravityMetersPerSecondSq.x, gravityMetersPerSecondSq.y, gravityMetersPerSecondSq.z, lifetimeRcp));
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
            Vector4 globalWakeParams = ResolveGlobalWakeParamsForCompute(lowTier);
            _lastWakeActive = globalWakeParams.x > 0.5f && globalWakeParams.z > 0.5f;
            fluidAdvectionCompute.SetVector(GlobalWakeParamsId, globalWakeParams);
            fluidAdvectionCompute.SetMatrix(VoxelSdfWorldToLocalId, sdfWorldToLocal);
            fluidAdvectionCompute.SetVector(VoxelSdfInvDoubleHalfExtentsId, sdfInvDoubleHalfExtents);
            fluidAdvectionCompute.SetVector(FluidAdvectionParamsId, new Vector4(dt, lowTier ? 1f : 0f, flowActive, sdfActive));
            fluidAdvectionCompute.SetVector(FluidAdvectionSdfParamsId, new Vector4(sdfActive, solidDensityThreshold, 0f, 0f));
        }

        private static Vector4 ResolveGlobalWakeParamsForCompute(bool lowTier)
        {
            return SanitizeGlobalWakeParamsForCompute(Shader.GetGlobalVector(GlobalWakeParamsId), lowTier);
        }

        private static Vector4 SanitizeGlobalWakeParamsForCompute(Vector4 wakeParams, bool lowTier)
        {
            if (!IsFiniteVector(wakeParams))
                return lowTier ? new Vector4(0f, 1f, 0f, 0f) : Vector4.zero;

            float maxSlotLimit = lowTier ? 4f : 16f;
            float slotLimit = math.clamp(wakeParams.x, 0f, maxSlotLimit);
            float activeCount = math.clamp(wakeParams.z, 0f, slotLimit);
            return new Vector4(
                slotLimit,
                lowTier ? 1f : 0f,
                activeCount,
                math.saturate(wakeParams.w));
        }

        private static int ResolveDispatchGroups(int count, int groupSize)
        {
            int safeGroupSize = math.max(1, groupSize);
            if ((safeGroupSize & (safeGroupSize - 1)) == 0)
            {
                int shift = 0;
                int stride = safeGroupSize;
                while (stride > 1)
                {
                    stride >>= 1;
                    shift++;
                }

                return math.max(1, (count + safeGroupSize - 1) >> shift);
            }

            int groups = 0;
            for (int covered = 0; covered < count; covered += safeGroupSize)
                groups++;
            return math.max(1, groups);
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

            HectonFluidEngine fluidEngine = ResolveFluidEngine();
            if (fluidEngine != null &&
                fluidEngine.TryGetGpuAbyssalFlowFieldBuffer(
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

            if (fluidEngine != null &&
                fluidEngine.TryGetGpuAbyssalFlowFieldTexture(
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
                Vector4 globalTextureParams = Shader.GetGlobalVector(AbyssalFlowTextureParamsId);
                Vector4 globalFlowCenter = Shader.GetGlobalVector(AbyssalFlowCenterId);
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

        private HectonFluidEngine ResolveFluidEngine()
        {
            return _fluidEngine;
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

        private Texture ResolveSdfTexture(bool lowTier, out Matrix4x4 sdfWorldToLocal, out Vector4 sdfInvDoubleHalfExtents, out float sdfActive)
        {
            sdfWorldToLocal = Matrix4x4.identity;
            sdfInvDoubleHalfExtents = Vector4.zero;
            sdfActive = 0f;
            if (lowTier)
                return _emptyTexture3D;

            if (voxelSdfTexture3D != null &&
                IsFiniteMatrix(voxelSdfWorldToLocal) &&
                IsValidSdfInvDoubleHalfExtents(voxelSdfInvDoubleHalfExtents))
            {
                sdfWorldToLocal = voxelSdfWorldToLocal;
                sdfInvDoubleHalfExtents = voxelSdfInvDoubleHalfExtents;
                sdfActive = 1f;
                return voxelSdfTexture3D;
            }

            RefreshGlobalSdfCacheIfNeeded();
            if (_cachedGlobalSdfActive > 0.5f && _cachedGlobalSdfTexture != null)
            {
                sdfWorldToLocal = _cachedGlobalSdfWorldToLocal;
                sdfInvDoubleHalfExtents = _cachedGlobalSdfInvDoubleHalfExtents;
                sdfActive = 1f;
                return _cachedGlobalSdfTexture;
            }

            return _emptyTexture3D;
        }

        private void RefreshGlobalSdfCacheIfNeeded()
        {
            int frame = Time.frameCount;
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

        private static int ResolveActiveCapacity(bool lowTier, bool highEndTier)
        {
            if (lowTier)
                return LowTierActiveCarveDebrisCount;

            return highEndTier
                ? MaxCarveDebrisCount
                : MidTierActiveCarveDebrisCount;
        }

        private static int ResolveParticlesPerCarve(bool lowTier, bool highEndTier)
        {
            if (lowTier)
                return LowTierParticlesPerCarve;

            return highEndTier
                ? HighTierParticlesPerCarve
                : MidTierParticlesPerCarve;
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

        private static bool IsHighEndPayload(HectonQualityTier tier)
        {
            return tier == HectonQualityTier.High || tier == HectonQualityTier.Ultra;
        }

        private void ApplyCapacityShed(int activeCapacity, NativeArray<float4> debrisPositions, NativeArray<float4> debrisVelocities)
        {
            int safeCapacity = math.clamp(activeCapacity, LowTierActiveCarveDebrisCount, MaxCarveDebrisCount);
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
            bool highEndTier = _cachedHighEndTier;
            material.SetBuffer(CarveDebrisReadId, currentPositionBuffer);
            material.SetBuffer(CarveDebrisVelocityReadId, currentVelocityBuffer);
            material.SetBuffer(DebrisBufferId, currentPositionBuffer);
            material.SetBuffer(DebrisPhysicsBufferId, currentVelocityBuffer);
            BindStaticRenderMaterialState(material, highEndTier);

            ShadowCastingMode resolvedShadowCastingMode = highEndTier && shadowCastingMode == ShadowCastingMode.Off
                ? ShadowCastingMode.On
                : shadowCastingMode;

            RenderParams renderParams = new RenderParams(material)
            {
                camera = renderCamera,
                worldBounds = drawBounds,
                layer = renderLayer,
                shadowCastingMode = resolvedShadowCastingMode,
                receiveShadows = highEndTier,
                motionVectorMode = MotionVectorGenerationMode.Object
            };
            UnityEngine.Graphics.RenderMeshIndirect(renderParams, mesh, _indirectArgsBuffer, 1, 0);
        }

        private void BindStaticRenderMaterialState(Material material, bool highEndTier)
        {
            bool materialChanged = !ReferenceEquals(_boundRenderMaterial, material);
            if (materialChanged || !ReferenceEquals(_boundVisibleIndicesBuffer, _visibleIndicesBuffer))
            {
                material.SetBuffer(CarveDebrisVisibleIndicesId, _visibleIndicesBuffer);
                _boundRenderMaterial = material;
                _boundVisibleIndicesBuffer = _visibleIndicesBuffer;
                _boundMaterialParamsValid = false;
            }

            Vector4 materialParams = new Vector4(
                minRockScale,
                math.max(minRockScale, maxRockScale),
                particleLifetimeSeconds,
                highEndTier ? 1f : 0f);
            if (!_boundMaterialParamsValid || !AreVector4ExactlyEqual(_boundMaterialParams, materialParams))
            {
                material.SetVector(CarveDebrisMaterialParamsId, materialParams);
                _boundMaterialParams = materialParams;
                _boundMaterialParamsValid = true;
            }

            material.SetVector(
                CarveDebrisMotionParamsId,
                new Vector4(
                    math.max(0.0001f, _lastDeltaTime),
                    _cachedSystemStress01,
                    _cachedLowTier ? 1f : 0f,
                    highEndTier ? 1f : 0f));
        }

        private Mesh ResolveMesh()
        {
            if (debrisMesh != null)
                return debrisMesh;
            if (_ownedMesh != null)
                return _ownedMesh;

            _ownedMesh = BuildOctahedronMesh();
            return _ownedMesh;
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
            if (debrisMesh == null && _ownedMesh == null)
                _ownedMesh = BuildOctahedronMesh();

            EnsureOwnedMaterial();
        }

        private void EnsureOwnedMaterial()
        {
            if (debrisMaterial != null)
            {
                if (_ownedMaterial != null && ReferenceEquals(_ownedMaterialSource, debrisMaterial))
                    return;

                if (IsSupportedDebrisMaterial(debrisMaterial))
                {
                    DestroyOwnedMaterial();
                    _ownedMaterial = new Material(debrisMaterial)
                    {
                        name = debrisMaterial.name + " Runtime Carve Debris Material"
                    }; // COLD ALLOC: Material[1] - private indirect debris material copy, avoids shared material mutation and MPB geometry path - owner: VFX_SDF_CARVE_DEBRIS
                    _ownedMaterialSource = debrisMaterial;
                    _materialFallbackAttempted = false;
                    return;
                }

                if (_ownedMaterial != null && _ownedMaterialSource == null && _materialFallbackAttempted)
                    return;

                DestroyOwnedMaterial();
                _materialFallbackAttempted = false;
            }
            else if (_ownedMaterialSource != null)
            {
                DestroyOwnedMaterial();
                _materialFallbackAttempted = false;
            }

            if (_ownedMaterial != null || _materialFallbackAttempted)
                return;

            _materialFallbackAttempted = true;
            Shader shader = Shader.Find(DebrisShaderName);
            if (shader == null)
                return;

            _ownedMaterial = new Material(shader)
            {
                name = "Hecton Runtime Carve Debris Material"
            }; // COLD ALLOC: Material[1] - fallback first-party indirect debris material - owner: VFX_SDF_CARVE_DEBRIS
        }

        private static bool IsSupportedDebrisMaterial(Material material)
        {
            return material != null &&
                   material.shader != null &&
                   string.Equals(material.shader.name, DebrisShaderName, StringComparison.Ordinal);
        }

        private Material ResolveMaterial()
        {
            EnsureFallbackRenderResources();
            return _ownedMaterial;
        }

        private void DestroyOwnedMaterial()
        {
            InvalidateRenderMaterialBindings();

            if (_ownedMaterial != null)
                DestroyUnityObject(_ownedMaterial);

            _ownedMaterial = null;
            _ownedMaterialSource = null;
        }

        private void InvalidateRenderMaterialBindings()
        {
            _boundRenderMaterial = null;
            _boundVisibleIndicesBuffer = null;
            _boundMaterialParams = default;
            _boundMaterialParamsValid = false;
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

        private bool IsLowTier()
        {
            if (!_tierCacheInitialized)
            {
                _cachedLowTier = _sampledLowTier;
                _pendingLowTier = _sampledLowTier;
                _cachedHighEndTier = _sampledHighEndTier;
                _pendingHighEndTier = _sampledHighEndTier;
                _pendingTierFrames = 0;
                _tierCacheInitialized = true;
                return _cachedLowTier;
            }

            if (_sampledLowTier == _cachedLowTier && _sampledHighEndTier == _cachedHighEndTier)
            {
                _pendingLowTier = _sampledLowTier;
                _pendingHighEndTier = _sampledHighEndTier;
                _pendingTierFrames = 0;
                return _cachedLowTier;
            }

            if (_sampledLowTier != _pendingLowTier || _sampledHighEndTier != _pendingHighEndTier)
            {
                _pendingLowTier = _sampledLowTier;
                _pendingHighEndTier = _sampledHighEndTier;
                _pendingTierFrames = 0;
                return _cachedLowTier;
            }

            _pendingTierFrames++;
            if (_pendingTierFrames >= TierSwitchConfirmFrames)
            {
                _cachedLowTier = _sampledLowTier;
                _cachedHighEndTier = _sampledHighEndTier;
                _pendingTierFrames = 0;
            }

            return _cachedLowTier;
        }

        private static bool IsLowTierPayload(byte tierProfile, HectonQualityTier qualityTier)
        {
            if (qualityTier == HectonQualityTier.Low || qualityTier == HectonQualityTier.Mx350)
                return true;

            if (qualityTier == HectonQualityTier.Unknown)
                return tierProfile == ScalabilityTierProfiles.LowMx350;

            return false;
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
            bool lowTier,
            NativeArray<int> jobState,
            NativeArray<CarveDebrisTelemetryEntry> blackBox)
        {
            if (!blackBox.IsCreated || blackBox.Length == 0 || !jobState.IsCreated)
                return;

            int frame = Time.frameCount;
            if (_lastTelemetryFrame == frame)
                return;

            _lastTelemetryFrame = frame;
            uint flags = (uint)math.max(0, jobState[JobStateFlagsIndex]);
            flags |= lowTier ? LowTierFlag : 0u;
            flags |= _lastSdfActive ? SdfActiveFlag : 0u;
            flags |= _lastFlowActive ? FlowActiveFlag : 0u;
            flags |= _lastWakeActive ? WakeActiveFlag : 0u;
            flags |= _cachedSystemStress01 > StressRecycleThreshold01 ? StressRecycleFlag : 0u;
            float3 appliedAupShift = _lastAppliedAupShift;
            uint hash = BuildTelemetryHash(_activeMirrorCount, queuedCarves, injectedParticles, flags, appliedAupShift);
            blackBox[_blackBoxCursor] = new CarveDebrisTelemetryEntry
            {
                FrameIndex = (uint)frame,
                ActiveCarveDebrisCount = _activeMirrorCount,
                QueuedCarves = queuedCarves,
                InjectedParticles = injectedParticles,
                Flags = flags,
                StateHash = hash,
                AppliedAupShift = appliedAupShift
            };
            _blackBoxCursor = (_blackBoxCursor + 1) % blackBox.Length;

            if (_activeMirrorCount > 0 && (frame % TelemetryPublishStride) == 0)
                GlobalTelemetryBus.PublishPerformanceWarning(ActiveCountTelemetryHash, TelemetryContextHash, _activeMirrorCount);

            if ((flags & InvalidStateFlag) != 0u)
                DumpBlackBoxOnce(flags, blackBox);

            jobState[JobStateFlagsIndex] = 0;
            _lastAppliedAupShift = default;
        }

        private static uint BuildTelemetryHash(int activeCount, int queuedCarves, int injectedParticles, uint flags, float3 appliedAupShift)
        {
            uint hash = 2166136261u;
            hash = (hash ^ (uint)activeCount) * 16777619u;
            hash = (hash ^ (uint)queuedCarves) * 16777619u;
            hash = (hash ^ (uint)injectedParticles) * 16777619u;
            hash = (hash ^ flags) * 16777619u;
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

            _blackBoxDumped = true;
            string path = Path.Combine(Application.dataPath, "..", DumpPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            int entrySize = UnsafeUtility.SizeOf<CarveDebrisTelemetryEntry>();
            Span<byte> header = stackalloc byte[sizeof(uint)];
            header[0] = (byte)reasonFlags;
            header[1] = (byte)(reasonFlags >> 8);
            header[2] = (byte)(reasonFlags >> 16);
            header[3] = (byte)(reasonFlags >> 24);
            void* source = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(blackBox);
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                stream.Write(header);
                stream.Write(new ReadOnlySpan<byte>(source, entrySize * blackBox.Length));
            }
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
            if (_ownedMesh != null)
                DestroyUnityObject(_ownedMesh);
            DestroyOwnedMaterial();
            if (_emptyTexture3D != null)
                DestroyUnityObject(_emptyTexture3D);
            _ownedMesh = null;
            _cachedGlobalSdfTexture = null;
            _cachedGlobalSdfWorldToLocal = Matrix4x4.identity;
            _cachedGlobalSdfInvDoubleHalfExtents = Vector4.zero;
            _cachedGlobalSdfActive = 0f;
            _nextGlobalSdfRefreshFrame = 0;
            _nextMissingRegistryRefreshFrame = 0;
            _pendingTierFrames = 0;
            _cachedLowTier = true;
            _pendingLowTier = true;
            _sampledLowTier = true;
            _cachedHighEndTier = false;
            _pendingHighEndTier = false;
            _sampledHighEndTier = false;
            _forceLowMemoryProfile = false;
            _tierCacheInitialized = false;
            InvalidateDrawMeshCache();
            _lastAppliedAupShift = default;
            _registryDataVault = null;
            _fluidEngine = null;
            _hotSwapRegistered = false;
            InvalidateDataVaultLease();
            _emptyTexture3D = null;
            _gpuReady = false;
            _activeMirrorCount = 0;
            _lastFlowActive = false;
            _lastSdfActive = false;
            _lastWakeActive = false;
            _materialFallbackAttempted = false;
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

        private static Mesh BuildOctahedronMesh()
        {
            Mesh mesh = new Mesh // COLD ALLOC: Mesh[1] - fallback low-poly indirect debris chip mesh - owner: VFX_SDF_CARVE_DEBRIS
            {
                name = "Hecton Carve Debris Octahedron"
            };
            Vector3[] vertices = // COLD ALLOC: Vector3[6] - fallback octahedron vertices - owner: VFX_SDF_CARVE_DEBRIS
            {
                new Vector3(0f, 1f, 0f),
                new Vector3(1f, 0f, 0f),
                new Vector3(0f, 0f, 1f),
                new Vector3(-1f, 0f, 0f),
                new Vector3(0f, 0f, -1f),
                new Vector3(0f, -1f, 0f)
            };
            int[] indices = // COLD ALLOC: int[24] - fallback octahedron index buffer - owner: VFX_SDF_CARVE_DEBRIS
            {
                0, 2, 1,
                0, 3, 2,
                0, 4, 3,
                0, 1, 4,
                5, 1, 2,
                5, 2, 3,
                5, 3, 4,
                5, 4, 1
            };
            mesh.vertices = vertices;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);
            return mesh;
        }

        private static unsafe void UploadRange<T>(GraphicsBuffer destination, NativeArray<T> source, int start, int count)
            where T : struct
        {
            if (destination == null || !destination.IsValid() || !source.IsCreated || count <= 0)
                return;

            int safeStart = math.clamp(start, 0, math.max(0, source.Length - 1));
            int safeCount = math.min(count, math.min(source.Length - safeStart, destination.count - safeStart));
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(safeStart, safeCount);
            void* sourcePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source) + UnsafeUtility.SizeOf<T>() * safeStart;
            void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
            UnsafeUtility.MemCpy(destinationPtr, sourcePtr, UnsafeUtility.SizeOf<T>() * safeCount);
            destination.UnlockBufferAfterWrite<T>(safeCount);
        }

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct AgeCarveDebrisMirrorJob : IJob
        {
            public NativeArray<float4> Positions;
            public int Capacity;
            public float LifeDelta;
            public NativeArray<int> JobState;

            public void Execute()
            {
                int active = 0;
                int flags = JobState[JobStateFlagsIndex];
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

        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
        private struct CarveDebrisInjectBatchJob : IJob
        {
            public NativeArray<float4> Positions;
            public NativeArray<float4> Velocities;
            [ReadOnly] public NativeArray<CarveDebrisRequest> Requests;
            public int RequestCount;
            public int Capacity;
            public NativeArray<int> JobState;

            public void Execute()
            {
                int count = math.min(Capacity, math.min(Positions.Length, Velocities.Length));
                int injectedTotal = 0;
                int active = math.clamp(JobState[JobStateActiveIndex], 0, count);
                int dirtyMin = count;
                int dirtyMax = -1;
                int flags = JobState[JobStateFlagsIndex];
                int safeRequestCount = math.min(math.max(0, RequestCount), Requests.Length);
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

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
        private struct CarveDebrisRequest
        {
            public float3 Center;
            public float3 EjectionAxis;
            public float Radius;
            public int ParticlesToInject;
            public float InitialSpeed;
            public float Life;
            public uint Seed;
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1, Size = 64)]
        private struct CarveDebrisTelemetryEntry
        {
            public uint FrameIndex;
            public int ActiveCarveDebrisCount;
            public int QueuedCarves;
            public int InjectedParticles;
            public uint Flags;
            public uint StateHash;
            public float3 AppliedAupShift;
        }
    }
}
