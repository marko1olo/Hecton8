using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;

namespace Hecton8.VFX.Parasites
{
    [DisallowMultipleComponent]
    public sealed unsafe class ParasiteSwarmGpuRuntime : MonoBehaviour, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int ThreadGroupSize = 64;
        private const uint PortableMaxComputeThreadsPerGroup = 256u;
        private const int MaxDispatchGroupsPerDimension = 65535;
        private const uint VisualPhaseTickMask = 4095u;
        private const float FaultGpuDumpThresholdMicroseconds = 1500f;
        private const float SimulationTickDeltaSeconds = 1f / 60f;
        private const float VisualPhaseStepRadians = 6.28318531f / 4096f;
        private const string ComputeSampleName = "H8 Parasite Swarm Compute";
        private const SystemID OwnerSystemId = SystemID.Vfx;
        private static readonly ulong TargetSelectionMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuParasiteTargets) |
            MutationGuardBit(BufferID.ShinobuParasiteTargetCandidates) |
            MutationGuardBit(BufferID.ShinobuParasiteTargetCount);
        private static readonly ulong TelemetryMutationGuardMask =
            MutationGuardBit(BufferID.ShinobuParasiteTelemetryRing) |
            MutationGuardBit(BufferID.ShinobuParasiteTelemetryCursor);
        private static readonly System.Threading.WaitCallback TelemetryDumpWorkerCallback = RunTelemetryDumpWorker;
        [Header("GPU")]
        [SerializeField] private ComputeShader parasiteCompute;
        [SerializeField] private Material parasiteMaterial;
        [SerializeField] private Camera renderCamera;
        [SerializeField] private Texture3D abyssalFlowField;
        [SerializeField, Tooltip("Authored 1x1x1 clear Texture3D bound when parasite flow is inactive. Runtime Texture3D synthesis is forbidden.")]
        private Texture3D emptyFlowTexture;

        [Header("Runtime")]
        [SerializeField] private int configuredMaxParticles = 500000;
        [SerializeField] private float renderBoundsMeters = 96f;
        [SerializeField] private bool forceMockTargets;

        private IDataVault _vault;
        private IDataVault _telemetryGuardVault;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _initialized;
        private bool _missingFlowVolumeAnnounced;
        private bool _blackBoxDumped;
        private int _bufferParity;
        private int _targetBufferParity;
        private int _drawParamsBufferParity;
        private int _frameParamsBufferParity;
        private int _telemetryCursor;
        private int _lastCandidateOverflowCount;
        private uint _dumpSequence;
        private uint _lastRebaseFrame;
        private uint _visualFrameCounter;
        private float3 _pendingAupShift;
        private string _dumpRootPath;
        private string _telemetryDumpRootSnapshot;
        private int _lastResolvedTargetCount;
        private int _telemetryDumpSnapshotCount;
        private int _telemetryDumpCursorSnapshot;
        private int _telemetryDumpInFlight;
        private IPlayerRuntimeContext _playerContext;
        private bool _renderCameraRuntimeResolved;
        private readonly SwarmTelemetryEntry[] _telemetryDumpSnapshot = new SwarmTelemetryEntry[ParasiteSwarmContracts.TelemetryCapacity];
        private readonly ParasiteTargetCandidateDTO[] _targetCandidateScratch = new ParasiteTargetCandidateDTO[ParasiteSwarmContracts.CandidateCapacity];
        private readonly ParasiteTargetDTO[] _targetSelectionScratch = new ParasiteTargetDTO[ParasiteSwarmContracts.MaxTargetCount];

        private int _initKernel = -1;
        private int _clearArgsKernel = -1;
        private int _advectKernel = -1;
        private int _rebaseKernel = -1;
        private int _cullKernel = -1;
        private int _initThreadGroupSizeX;
        private int _clearArgsThreadGroupSizeX;
        private int _advectThreadGroupSizeX;
        private int _rebaseThreadGroupSizeX;
        private int _cullThreadGroupSizeX;

        private GraphicsBuffer _particleBufferA;
        private GraphicsBuffer _particleBufferB;
        private GraphicsBuffer _targetBufferA;
        private GraphicsBuffer _targetBufferB;
        private GraphicsBuffer _visibleIndicesBuffer;
        private GraphicsBuffer _indirectArgsBuffer;
        private GraphicsBuffer _drawParamsBufferA;
        private GraphicsBuffer _drawParamsBufferB;
        private GraphicsBuffer _frameParamsBufferA;
        private GraphicsBuffer _frameParamsBufferB;
        private Texture3D _emptyFlowTexture;
        private CommandBuffer _commandBuffer;
        private Material _boundMaterial;

        private VaultGenerationHandle<ParasiteTargetDTO> _targetsHandle;
        private VaultGenerationHandle<ParasiteTargetCandidateDTO> _candidatesHandle;
        private VaultGenerationHandle<int> _targetCountHandle;
        private VaultGenerationHandle<ParasiteSwarmTuningDTO> _tuningHandle;
        private VaultGenerationHandle<SwarmTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;

        private static readonly int ParasiteReadId = Shader.PropertyToID("_H8ParasiteRead");
        private static readonly int ParasiteReadAId = Shader.PropertyToID("_H8ParasiteReadA");
        private static readonly int ParasiteReadBId = Shader.PropertyToID("_H8ParasiteReadB");
        private static readonly int ParasiteWriteId = Shader.PropertyToID("_H8ParasiteWrite");
        private static readonly int ParasiteTargetsId = Shader.PropertyToID("_H8ParasiteTargets");
        private static readonly int ParasiteVisibleIndicesId = Shader.PropertyToID("_H8ParasiteVisibleIndices");
        private static readonly int ParasiteIndirectArgsId = Shader.PropertyToID("_H8ParasiteIndirectArgs");
        private static readonly int ParasiteDrawParamsId = Shader.PropertyToID("_H8ParasiteDrawParams");
        private static readonly int ParasiteFrameParamsId = Shader.PropertyToID("_H8ParasiteFrameParams");
        private static readonly int ParasiteAupShiftDeltaId = Shader.PropertyToID("_H8ParasiteAupShiftDelta");
        private static readonly int AbyssalFlowFieldId = Shader.PropertyToID("_H8AbyssalFlowField");

        private static ulong MutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!ParasiteSwarmContracts.ValidateRuntimeLayouts(out int failureCode))
            {
                Hecton8.Core.H8Debug.LogError("SHINOBU_313 ParasiteSwarm layout rejection code " + failureCode);
                enabled = false;
                return;
            }

            RebindDataVaultForLifecycle(GlobalRegistry.DataVault);
            if (_vault == null)
                return;

            _dumpRootPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            CachePlayerContext(GlobalRegistry.Player);

#if UNITY_EDITOR
            TryLoadProfilesFromDisk(_vault);
#endif
            _targetBufferParity = 0;
            _drawParamsBufferParity = 0;
            _frameParamsBufferParity = 0;

            // A missing flow volume is SURVIVABLE and must not abort setup. The condition tests BOTH fields
            // because the neutral 1x1x1 asset is only ever the fallback arm of the ternary in
            // DispatchAndRender; with abyssalFlowField authored it is never read at all. See
            // LogMissingFlowVolumeFallback for the two separate defects the old throwing guard carried.
            if (abyssalFlowField == null && emptyFlowTexture == null && !_missingFlowVolumeAnnounced)
            {
                _missingFlowVolumeAnnounced = true;
                LogMissingFlowVolumeFallback();
            }

            CreateGpuResources();
            ResolveComputeKernels();
            InitializeGpuParticles();
            _visualFrameCounter = 0u;
            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
            _initialized = true;
        }

        /// <summary>
        /// Reports a total absence of abyssal-flow volume once per instance. Never latches the swarm off.
        /// </summary>
        /// <remarks>
        /// This replaces a THROWING guard that was wrong in two independent ways.
        ///
        /// SHAPE. It tested the FALLBACK rather than the dependency. DispatchAndRender resolves
        /// <c>abyssalFlowField != null ? abyssalFlowField : _emptyFlowTexture</c>, so with the real flow
        /// volume authored the neutral 1x1x1 asset is never read at all - yet an unassigned neutral asset
        /// aborted OnEnable ahead of CreateGpuResources, ResolveComputeKernels, InitializeGpuParticles,
        /// <c>_visualFrameCounter = 0u</c>, TryRegisterLateFrameTickable, TryRegisterHotSwapListener and
        /// <c>_initialized = true</c>. Seven statements, and the last three are why the component exists:
        /// unregistered it is never late-frame ticked, never rebinds after a GlobalRegistry hot swap, and
        /// LateFrameTick would refuse on <c>_initialized</c> even if something did call it.
        ///
        /// THROW. <c>UnityEngine.Assertions.Assert.IsNotNull</c> THROWS in this project - nothing under
        /// Assets sets <c>Assert.raiseExceptions = false</c> - so the <c>enabled = false;</c> written
        /// directly beneath it was unreachable. The component therefore stayed enabled with
        /// <c>_initialized</c> false and re-threw on every later enable instead of latching off after one.
        ///
        /// A gap is survivable by construction, which is why no permanent-failure latch belongs here.
        /// Hecton_ParasiteSwarm.compute:236-237 reads the volume exactly once and additively -
        /// <c>acceleration += curl + (flow * flowWeight)</c> - so a zero read costs the abyssal-flow
        /// advection term and nothing else: no NaN, no divide, no dead lane. Zero is exactly what the
        /// authored "empty" volume encodes. Runtime Texture3D synthesis stays forbidden, so the fix is
        /// authoring, not code, and the asset already exists on disk.
        /// </remarks>
        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private void LogMissingFlowVolumeFallback()
        {
            Hecton8.Core.H8Debug.LogWarning("[ParasiteSwarmGpuRuntime] Neither 'abyssalFlowField' nor its neutral fallback 'emptyFlowTexture' is assigned. The swarm still boots, registers, ticks and renders; only the abyssal-flow advection term is lost, because the compute shader adds the sampled flow once and an unbound Texture3D reads zero. Runtime Texture3D synthesis is forbidden - assign Assets/_Project/Art/Textures/VFX/ParticulateFlipbooks1728/TX_MarineSnow_EmptyAbyssalFlow_1x1x1.asset to 'emptyFlowTexture', the same authored neutral volume HectonMarineSnowRenderer and SargassumMicroFaunaBoids already bind.", this);
        }

        private void OnDisable()
        {
            if (_registered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registered = false;
            }

            if (_hotSwapRegistered)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _hotSwapRegistered = false;
            }

            CompleteTargetSelectionForLifecycle();
            ReleaseVaultHandles(_vault);
            ClearVaultDescriptors();
            _vault = null;
            ResetVaultEpochState();
            _playerContext = null;
            if (_renderCameraRuntimeResolved)
                renderCamera = null;
            _renderCameraRuntimeResolved = false;
            DisposeGpuResources();
            _initialized = false;
        }

        private void CompleteTargetSelectionForLifecycle()
        {
            // Target selection is currently resolved inline, so lifecycle teardown has no scheduled job to complete.
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVaultForLifecycle(currentService is IDataVault currentVault ? currentVault : null);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Player)
                CachePlayerContext(currentService as IPlayerRuntimeContext);
        }

        public void LateFrameTick()
        {
            if (!_initialized || _vault == null || _vault.IsCompactionFenceActive)
                return;

            ResolveAupShiftSignals();
            if (!TryResolveCameraAup(out double3 cameraAup, out Vector3 cameraPosition))
                return;

            if (!TryResolveOwnBuffers(
                    out NativeArray<ParasiteTargetDTO> targets,
                    out NativeArray<int> targetCount,
                    out NativeArray<ParasiteSwarmTuningDTO> tuningBuffer))
                return;

            ParasiteSwarmTuningDTO tuning = ReadSanitizedTuning(tuningBuffer);
            float globalQuality = ResolveGlobalQuality(in tuning);
            bool gpuParticleCapacityValid = TryResolveGpuParticleCapacity(out int gpuParticleCapacity);
            if (!gpuParticleCapacityValid)
                gpuParticleCapacity = 1;

            int particleBudget = math.min(
                math.min(configuredMaxParticles > 0 ? configuredMaxParticles : ParasiteSwarmContracts.MaxGpuParticleCapacity, gpuParticleCapacity),
                ParasiteSwarmContracts.ResolveParticleBudget(globalQuality, in tuning));

            uint visualFrame = AdvanceVisualFrame(out float visualPhaseRadians);
            int resolvedTargetCount = ResolveTargetSelectionInline(cameraAup, globalQuality, visualPhaseRadians, in tuning);
            UploadTargets(targets, resolvedTargetCount);

            int dispatchedParticleBudget = resolvedTargetCount > 0 ? particleBudget : 0;
            uint flags = DispatchAndRender(cameraPosition, dispatchedParticleBudget, resolvedTargetCount, globalQuality, visualPhaseRadians, in tuning);
            if (!gpuParticleCapacityValid)
                flags |= ParasiteSwarmContracts.TelemetryFlagNoCompute;

            int estimatedGpuUs = ParasiteSwarmContracts.EstimateGpuMicroseconds(dispatchedParticleBudget, resolvedTargetCount, globalQuality);
            flags |= ParasiteSwarmContracts.TelemetryFlagTimingEstimated;
            if (estimatedGpuUs > FaultGpuDumpThresholdMicroseconds)
                flags |= ParasiteSwarmContracts.TelemetryFlagGpuBudgetSpike;
            if (_lastCandidateOverflowCount > 0)
                flags |= ParasiteSwarmContracts.TelemetryFlagTargetOverflow;

            bool shouldStageDump = false;
            if (TryAcquireTelemetryOwnerViews(out NativeArray<SwarmTelemetryEntry> telemetry, out NativeArray<int> telemetryCursor))
            {
                try
                {
                    if (RecordTelemetry(telemetry, telemetryCursor, targets, true, visualFrame, resolvedTargetCount, dispatchedParticleBudget, estimatedGpuUs, globalQuality, in tuning, ref flags) &&
                        ((flags & (ParasiteSwarmContracts.TelemetryFlagTargetOverflow | ParasiteSwarmContracts.TelemetryFlagGpuBudgetSpike | ParasiteSwarmContracts.TelemetryFlagInvalidMath)) != 0u) &&
                        !System.Threading.Volatile.Read(ref _blackBoxDumped))
                    {
                        shouldStageDump = true;
                    }
                }
                finally
                {
                    ReleaseTelemetryOwnerViews();
                }
            }

            bool shouldQueueDump = shouldStageDump && TryStageTelemetryDump();
            if (shouldQueueDump)
                QueueTelemetryDumpWorker();
        }

        private void BindVaultDescriptors(IDataVault vault)
        {
            BindVaultDescriptor(vault, BufferID.ShinobuParasiteTargets, out _targetsHandle);
            BindVaultDescriptor(vault, BufferID.ShinobuParasiteTargetCandidates, out _candidatesHandle);
            BindVaultDescriptor(vault, BufferID.ShinobuParasiteTargetCount, out _targetCountHandle);
            BindVaultDescriptor(vault, BufferID.ShinobuParasiteTuning, out _tuningHandle);
            BindVaultDescriptor(vault, BufferID.ShinobuParasiteTelemetryRing, out _telemetryHandle);
            BindVaultDescriptor(vault, BufferID.ShinobuParasiteTelemetryCursor, out _telemetryCursorHandle);
        }

        private static void BindVaultDescriptor<T>(
            IDataVault vault,
            BufferID bufferId,
            out VaultGenerationHandle<T> handle) where T : struct
        {
            handle = default;
            if (vault == null)
                return;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> candidate) &&
                IsOwnedHandle(in candidate, bufferId))
            {
                handle = candidate;
            }
        }

        private void ClearVaultDescriptors()
        {
            _targetsHandle = default;
            _candidatesHandle = default;
            _targetCountHandle = default;
            _tuningHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
        }

        private void ResetVaultEpochState()
        {
            _lastResolvedTargetCount = 0;
            _telemetryCursor = 0;
            _lastCandidateOverflowCount = 0;
            _blackBoxDumped = false;
            _dumpSequence = 0u;
        }

        private static void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle<ParasiteTargetDTO>(vault, BufferID.ShinobuParasiteTargets);
            ReleaseVaultHandle<ParasiteTargetCandidateDTO>(vault, BufferID.ShinobuParasiteTargetCandidates);
            ReleaseVaultHandle<int>(vault, BufferID.ShinobuParasiteTargetCount);
            ReleaseVaultHandle<ParasiteSwarmTuningDTO>(vault, BufferID.ShinobuParasiteTuning);
            ReleaseVaultHandle<SwarmTelemetryEntry>(vault, BufferID.ShinobuParasiteTelemetryRing);
            ReleaseVaultHandle<int>(vault, BufferID.ShinobuParasiteTelemetryCursor);
            ReleaseVaultHandle<ParasiteBehaviorProfileDTO>(vault, BufferID.ShinobuParasiteProfiles);
            ReleaseVaultHandle<ParasiteScannerSummaryDTO>(vault, BufferID.ShinobuParasiteScannerSummary);
            ReleaseVaultHandle<int>(vault, BufferID.ShinobuParasiteProfileCount);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, BufferID bufferId) where T : struct
        {
            if (vault == null)
                return;

            if (vault.TryGetGenerationHandle(bufferId, out VaultGenerationHandle<T> handle) &&
                IsOwnedHandle(in handle, bufferId))
            {
                vault.ReleaseBuffer(in handle);
            }
        }

        private void RebindDataVaultForLifecycle(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            ReleaseVaultHandles(_vault);
            ClearVaultDescriptors();
            _vault = vault;
            ResetVaultEpochState();

            if (_vault == null)
                return;

            ParasiteSwarmContracts.EnsureVaultBuffers(_vault);
            BindVaultDescriptors(_vault);
            SeedTuningIfEmpty();
        }

#if UNITY_EDITOR
        public static void TryLoadProfilesFromDisk(IDataVault vault)
        {
            if (vault == null)
                return;

            ParasiteSwarmContracts.EnsureVaultBuffers(vault);

            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(root, "Assets", "_Project", "Data", "VFX", "parasite_behavior_profiles.csv");
            if (!File.Exists(path))
                return;

            Span<byte> scratch = stackalloc byte[ParasiteSwarmContracts.CsvScratchBytes];
            int bytesRead = ReadCsvFileIntoScratch(path, scratch);
            if (bytesRead <= 0)
                return;

            ParasiteSwarmContracts.LoadProfilesFromCsv(vault, scratch.Slice(0, bytesRead));
        }

        private static int ReadCsvFileIntoScratch(string path, Span<byte> scratch)
        {
            if (scratch.Length <= 0)
                return 0;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, math.max(1, scratch.Length), FileOptions.SequentialScan))
            {
                if (stream.Length > scratch.Length)
                    return 0;

                int limit = (int)stream.Length;
                int total = 0;
                while (total < limit)
                {
                    int read = stream.Read(scratch.Slice(total, limit - total));
                    if (read <= 0)
                        break;

                    total += read;
                }

                return total;
            }
        }
#endif

        private void SeedTuningIfEmpty()
        {
            IDataVault vault = _vault;
            if (vault == null || !IsOwnedHandle(in _tuningHandle, BufferID.ShinobuParasiteTuning))
                return;

            if (!vault.TryAcquireWriteLock(in _tuningHandle, SystemID.Vfx, out NativeArray<ParasiteSwarmTuningDTO> tuning))
                return;

            try
            {
                if (!tuning.IsCreated)
                    return;

                if (tuning.Length > 0 && tuning[0].Version == 0u)
                    tuning[0] = ParasiteSwarmContracts.DefaultTuning();
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, SystemID.Vfx);
            }
        }

        private void CreateGpuResources()
        {
            int capacity = math.clamp(
                configuredMaxParticles > 0 ? configuredMaxParticles : ParasiteSwarmContracts.MaxGpuParticleCapacity,
                1,
                ParasiteSwarmContracts.MaxGpuParticleCapacity);

            _particleBufferA ??= CreateGpuWriteStructuredBuffer<ParasiteGpuParticleDTO>(capacity);
            _particleBufferB ??= CreateGpuWriteStructuredBuffer<ParasiteGpuParticleDTO>(capacity);
            _targetBufferA ??= CreateStructuredLockBuffer<ParasiteTargetDTO>(ParasiteSwarmContracts.MaxTargetCount);
            _targetBufferB ??= CreateStructuredLockBuffer<ParasiteTargetDTO>(ParasiteSwarmContracts.MaxTargetCount);
            _visibleIndicesBuffer ??= CreateGpuWriteStructuredBuffer<uint>(capacity);
            _drawParamsBufferA ??= CreateStructuredLockBuffer<float4>(1);
            _drawParamsBufferB ??= CreateStructuredLockBuffer<float4>(1);
            _frameParamsBufferA ??= CreateStructuredLockBuffer<ParasiteFrameParamsDTO>(1);
            _frameParamsBufferB ??= CreateStructuredLockBuffer<ParasiteFrameParamsDTO>(1);
            _indirectArgsBuffer ??= new GraphicsBuffer(
                GraphicsBuffer.Target.IndirectArguments,
                1,
                UnsafeUtility.SizeOf<ParasiteIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - compute-written parasite indirect args - owner: SHINOBU_313

            _emptyFlowTexture = emptyFlowTexture;

            if (_commandBuffer == null)
                _commandBuffer = new CommandBuffer { name = "H8 Parasite Swarm" }; // COLD ALLOC: command buffer reused every frame - owner: SHINOBU_313

            Material material = parasiteMaterial;
            BindMaterialBuffers(material);
            WarmupMaterialPassCold(material);
        }

        private void BindMaterialBuffers(Material material)
        {
            if (material == null)
                return;

            if (ReferenceEquals(_boundMaterial, material))
                return;

            material.SetBuffer(ParasiteReadAId, _particleBufferA);
            material.SetBuffer(ParasiteReadBId, _particleBufferB);
            material.SetBuffer(ParasiteVisibleIndicesId, _visibleIndicesBuffer);
            _boundMaterial = material;
        }

        private static void WarmupMaterialPassCold(Material material)
        {
            if (material == null || material.shader == null || !material.shader.isSupported)
                return;

            material.SetPass(0);
        }

        private static GraphicsBuffer CreateGpuWriteStructuredBuffer<T>(int count) where T : unmanaged
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                math.max(1, count),
                UnsafeUtility.SizeOf<T>()); // COLD ALLOC: persistent parasite GPU-write lane - owner: SHINOBU_313
        }

        private static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : unmanaged
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                math.max(1, count),
                UnsafeUtility.SizeOf<T>()); // COLD ALLOC: persistent parasite CPU-upload lane - owner: SHINOBU_313
        }

        private void ResolveComputeKernels()
        {
            ResetComputeKernelState();
            if (parasiteCompute == null || !SystemInfo.supportsComputeShaders)
                return;

            _initKernel = TryFindKernel(parasiteCompute, "CS_InitParasites");
            _clearArgsKernel = TryFindKernel(parasiteCompute, "CS_ClearArgs");
            _advectKernel = TryFindKernel(parasiteCompute, "CS_AdvectParasites");
            _rebaseKernel = TryFindKernel(parasiteCompute, "CS_RebaseParasites");
            _cullKernel = TryFindKernel(parasiteCompute, "CS_CullParasites");
            _initThreadGroupSizeX = ResolveKernelThreadGroupSizeX(parasiteCompute, _initKernel);
            _clearArgsThreadGroupSizeX = ResolveKernelThreadGroupSizeX(parasiteCompute, _clearArgsKernel);
            _advectThreadGroupSizeX = ResolveKernelThreadGroupSizeX(parasiteCompute, _advectKernel);
            _rebaseThreadGroupSizeX = ResolveKernelThreadGroupSizeX(parasiteCompute, _rebaseKernel);
            _cullThreadGroupSizeX = ResolveKernelThreadGroupSizeX(parasiteCompute, _cullKernel);
        }

        private void ResetComputeKernelState()
        {
            _initKernel = -1;
            _clearArgsKernel = -1;
            _advectKernel = -1;
            _rebaseKernel = -1;
            _cullKernel = -1;
            _initThreadGroupSizeX = 0;
            _clearArgsThreadGroupSizeX = 0;
            _advectThreadGroupSizeX = 0;
            _rebaseThreadGroupSizeX = 0;
            _cullThreadGroupSizeX = 0;
        }

        private static int TryFindKernel(ComputeShader shader, string kernelName)
        {
            if (shader == null || !SystemInfo.supportsComputeShaders)
                return -1;

            try
            {
                if (!shader.HasKernel(kernelName))
                    return -1;

                int kernel = shader.FindKernel(kernelName);
                if (kernel < 0)
                    return -1;

                return shader.IsSupported(kernel) ? kernel : -1;
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

        private void InitializeGpuParticles()
        {
            if (parasiteCompute == null || parasiteMaterial == null || renderCamera == null || _initKernel < 0 || _particleBufferA == null || _particleBufferB == null)
                return;

            int capacity = math.min(_particleBufferA.count, _particleBufferB.count);
            int groups = ResolveDispatchGroups(capacity, _initThreadGroupSizeX);
            if (groups <= 0)
                return;

            ParasiteSwarmTuningDTO defaultTuning = ParasiteSwarmContracts.DefaultTuning();
            if (!UploadFrameParams(0f, 0f, capacity, 0, 0f, in defaultTuning, out GraphicsBuffer frameParamsBuffer))
                return;

            _commandBuffer.Clear();
            _commandBuffer.BeginSample(ComputeSampleName);
            _commandBuffer.SetComputeBufferParam(parasiteCompute, _initKernel, ParasiteFrameParamsId, frameParamsBuffer);
            _commandBuffer.SetComputeBufferParam(parasiteCompute, _initKernel, ParasiteWriteId, _particleBufferA);
            _commandBuffer.DispatchCompute(parasiteCompute, _initKernel, groups, 1, 1);
            _commandBuffer.SetComputeBufferParam(parasiteCompute, _initKernel, ParasiteWriteId, _particleBufferB);
            _commandBuffer.DispatchCompute(parasiteCompute, _initKernel, groups, 1, 1);
            _commandBuffer.EndSample(ComputeSampleName);
            UnityEngine.Graphics.ExecuteCommandBuffer(_commandBuffer);
        }

        private bool TryResolveOwnBuffers(
            out NativeArray<ParasiteTargetDTO> targets,
            out NativeArray<int> targetCount,
            out NativeArray<ParasiteSwarmTuningDTO> tuning)
        {
            targets = default;
            targetCount = default;
            tuning = default;
            return TryResolveHandle(
                       in _targetsHandle,
                       BufferID.ShinobuParasiteTargets,
                       ParasiteSwarmContracts.MaxTargetCount,
                       out targets) &&
                   TryResolveHandle(
                       in _targetCountHandle,
                       BufferID.ShinobuParasiteTargetCount,
                       1,
                       out targetCount) &&
                   TryReadHandle(
                       in _tuningHandle,
                       BufferID.ShinobuParasiteTuning,
                       1,
                       out tuning);
        }

        private int ResolveTargetSelectionInline(
            double3 cameraAup,
            float quality,
            float visualPhaseRadians,
            in ParasiteSwarmTuningDTO tuning)
        {
            if (_vault == null)
                return _lastResolvedTargetCount;

            int stagedTargetCount = StageTargetSelectionScratch(
                cameraAup,
                quality,
                visualPhaseRadians,
                in tuning,
                out int stagedCandidateCount,
                out int stagedCandidateOverflowCount);

            return CommitTargetSelectionScratch(stagedCandidateCount, stagedTargetCount, stagedCandidateOverflowCount);
        }

        private int StageTargetSelectionScratch(
            double3 cameraAup,
            float quality,
            float visualPhaseRadians,
            in ParasiteSwarmTuningDTO tuning,
            out int stagedCandidateCount,
            out int stagedCandidateOverflowCount)
        {
            bool useMock = forceMockTargets || (tuning.Flags & ParasiteSwarmContracts.TuningFlagMockTargets) != 0u;
            if (useMock)
            {
                stagedCandidateOverflowCount = 0;
                stagedCandidateCount = StageMockThermalTargetsScratch(cameraAup, quality, visualPhaseRadians, in tuning);
                return SelectTopTargetsFromScratch(stagedCandidateCount, cameraAup, in tuning);
            }

            int candidateCount = StageThermalSourceSignals(cameraAup, in tuning, _targetCandidateScratch, out int eligibleSignalCount);
            stagedCandidateOverflowCount = math.max(0, eligibleSignalCount - ParasiteSwarmContracts.MaxTargetCount);
            stagedCandidateCount = candidateCount;
            if (candidateCount <= 0)
                return 0;

            for (int i = 0; i < candidateCount; i++)
                _targetCandidateScratch[i] = ScoreStagedCandidate(_targetCandidateScratch[i], cameraAup, in tuning);

            return SelectTopTargetsFromScratch(candidateCount, cameraAup, in tuning);
        }

        private int CommitTargetSelectionScratch(int stagedCandidateCount, int stagedTargetCount, int stagedCandidateOverflowCount)
        {
            IDataVault vault = _vault;
            if (vault == null || !vault.TryAcquireMutationGuard(TargetSelectionMutationGuardMask))
                return _lastResolvedTargetCount;

            try
            {
                if (!TryResolveTargetOwnerViews(
                        out NativeArray<ParasiteTargetDTO> targets,
                        out NativeArray<ParasiteTargetCandidateDTO> candidates,
                        out NativeArray<int> targetCount))
                    return _lastResolvedTargetCount;

                int candidateLimit = math.min(
                    math.clamp(stagedCandidateCount, 0, _targetCandidateScratch.Length),
                    candidates.IsCreated ? candidates.Length : 0);
                for (int i = 0; i < candidateLimit; i++)
                    candidates[i] = _targetCandidateScratch[i];

                int targetLimit = math.min(ParasiteSwarmContracts.MaxTargetCount, targets.IsCreated ? targets.Length : 0);
                int selectedTargetCount = math.clamp(stagedTargetCount, 0, targetLimit);
                for (int i = 0; i < targetLimit; i++)
                    targets[i] = i < selectedTargetCount ? _targetSelectionScratch[i] : default;

                if (targetCount.IsCreated && targetCount.Length > 0)
                    targetCount[0] = selectedTargetCount;

                _lastCandidateOverflowCount = stagedCandidateOverflowCount;
                _lastResolvedTargetCount = selectedTargetCount;
                return _lastResolvedTargetCount;
            }
            finally
            {
                vault.ReleaseMutationGuard(TargetSelectionMutationGuardMask);
            }
        }

        private void CachePlayerContext(IPlayerRuntimeContext playerContext)
        {
            _playerContext = playerContext;
            Camera playerCamera = playerContext != null ? playerContext.PlayerCamera : null;
            if (playerCamera == null)
                return;

            if (renderCamera != null && !_renderCameraRuntimeResolved)
                return;

            renderCamera = playerCamera;
            _renderCameraRuntimeResolved = true;
        }

        private bool TryResolveTargetOwnerViews(
            out NativeArray<ParasiteTargetDTO> targets,
            out NativeArray<ParasiteTargetCandidateDTO> candidates,
            out NativeArray<int> targetCount)
        {
            targets = default;
            candidates = default;
            targetCount = default;

            return TryResolveHandle(
                       in _targetsHandle,
                       BufferID.ShinobuParasiteTargets,
                       ParasiteSwarmContracts.MaxTargetCount,
                       out targets) &&
                   TryResolveHandle(
                       in _candidatesHandle,
                       BufferID.ShinobuParasiteTargetCandidates,
                       ParasiteSwarmContracts.CandidateCapacity,
                       out candidates) &&
                   TryResolveHandle(
                       in _targetCountHandle,
                       BufferID.ShinobuParasiteTargetCount,
                       1,
                       out targetCount);
        }

        private bool TryAcquireTelemetryOwnerViews(
            out NativeArray<SwarmTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor)
        {
            telemetry = default;
            telemetryCursor = default;

            IDataVault vault = _vault;
            if (vault == null || !vault.TryAcquireMutationGuard(TelemetryMutationGuardMask))
                return false;

            bool guardTransferred = false;
            try
            {
                if (!TryResolveHandle(
                        in _telemetryHandle,
                        BufferID.ShinobuParasiteTelemetryRing,
                        ParasiteSwarmContracts.TelemetryCapacity,
                        out telemetry) ||
                    !TryResolveHandle(
                        in _telemetryCursorHandle,
                        BufferID.ShinobuParasiteTelemetryCursor,
                        1,
                        out telemetryCursor))
                {
                    telemetry = default;
                    telemetryCursor = default;
                    return false;
                }

                guardTransferred = true;
                _telemetryGuardVault = vault;
                return true;
            }
            finally
            {
                if (!guardTransferred)
                    vault.ReleaseMutationGuard(TelemetryMutationGuardMask);
            }
        }

        private void ReleaseTelemetryOwnerViews()
        {
            IDataVault vault = _telemetryGuardVault;
            _telemetryGuardVault = null;
            vault?.ReleaseMutationGuard(TelemetryMutationGuardMask);
        }

        private bool TryReadTelemetryViews(
            out NativeArray<SwarmTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor)
        {
            telemetry = default;
            telemetryCursor = default;

            return TryReadHandle(
                       in _telemetryHandle,
                       BufferID.ShinobuParasiteTelemetryRing,
                       ParasiteSwarmContracts.TelemetryCapacity,
                       out telemetry) &&
                   TryReadHandle(
                       in _telemetryCursorHandle,
                       BufferID.ShinobuParasiteTelemetryCursor,
                       1,
                       out telemetryCursor);
        }

        private bool TryStageTelemetryDump()
        {
            if (_vault == null ||
                _vault.IsCompactionFenceActive ||
                !TryReadTelemetryViews(out NativeArray<SwarmTelemetryEntry> telemetry, out NativeArray<int> telemetryCursor))
            {
                return false;
            }

            return TryStageTelemetryDumpFromReadOnly(telemetry, telemetryCursor);
        }

        private bool TryStageTelemetryDumpFromReadOnly(NativeArray<SwarmTelemetryEntry> telemetry, NativeArray<int> telemetryCursor)
        {
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return false;

            if (System.Threading.Interlocked.CompareExchange(ref _telemetryDumpInFlight, 1, 0) != 0)
                return false;

            _dumpSequence++;
            int cursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? telemetryCursor[0] : _telemetryCursor;
            int count = math.min(telemetry.Length, _telemetryDumpSnapshot.Length);
            for (int i = 0; i < count; i++)
                _telemetryDumpSnapshot[i] = telemetry[i];

            _telemetryDumpCursorSnapshot = cursor;
            _telemetryDumpRootSnapshot = _dumpRootPath;
            System.Threading.Volatile.Write(ref _telemetryDumpSnapshotCount, count);
            return true;
        }

        private void QueueTelemetryDumpWorker()
        {
            if (!System.Threading.ThreadPool.QueueUserWorkItem(TelemetryDumpWorkerCallback, this))
                System.Threading.Volatile.Write(ref _telemetryDumpInFlight, 0);
        }

        private static void RunTelemetryDumpWorker(object state)
        {
            if (state is ParasiteSwarmGpuRuntime runtime)
                runtime.WriteTelemetryDumpWorker();
        }

        private void WriteTelemetryDumpWorker()
        {
            try
            {
                int snapshotCount = System.Threading.Volatile.Read(ref _telemetryDumpSnapshotCount);
                int cursorSnapshot = System.Threading.Volatile.Read(ref _telemetryDumpCursorSnapshot);
                if (ParasiteSwarmContracts.TryWriteTelemetryDump(
                        _telemetryDumpRootSnapshot,
                        _telemetryDumpSnapshot,
                        snapshotCount,
                        cursorSnapshot))
                {
                    System.Threading.Volatile.Write(ref _blackBoxDumped, true);
                }
            }
            finally
            {
                System.Threading.Volatile.Write(ref _telemetryDumpInFlight, 0);
            }
        }

        private int StageMockThermalTargetsScratch(
            double3 cameraAup,
            float quality,
            float visualPhaseRadians,
            in ParasiteSwarmTuningDTO tuning)
        {
            int limit = math.min(
                ParasiteSwarmContracts.MaxTargetCount,
                math.min(_targetSelectionScratch.Length, _targetCandidateScratch.Length));
            float q = ParasiteSwarmContracts.SmoothQuality01(quality);
            for (int index = 0; index < limit; index++)
            {
                float angle = visualPhaseRadians * (0.73f + (index * 0.037f));
                float radius = math.lerp(3.5f, 18f, q) + (index * 0.17f);
                float3 local;
                local.x = ParasiteSwarmContracts.FastSinApprox(angle + index * 2.31f) * radius;
                local.y = ParasiteSwarmContracts.FastSinApprox((angle * 0.61f) + index) * 2.2f;
                local.z = ParasiteSwarmContracts.FastCosApprox(angle + index * 1.73f) * radius;
                float thermal = tuning.ParasiteAttractionThreshold + 8f + ParasiteSwarmContracts.FastSinApprox(angle * 2.7f) * 3f + (index * 0.35f);
                float3 velocity;
                velocity.x = ParasiteSwarmContracts.FastCosApprox(angle);
                velocity.y = ParasiteSwarmContracts.FastSinApprox(angle * 0.5f) * 0.35f;
                velocity.z = -ParasiteSwarmContracts.FastSinApprox(angle);
                velocity *= 2f + q * 6f;
                float attractionRadius = math.max(0.25f, (2.1f + q * 3.4f) * tuning.AttractionRadiusScale);

                double3 local64;
                local64.x = local.x;
                local64.y = local.y;
                local64.z = local.z;

                ParasiteTargetCandidateDTO candidate = default;
                candidate.Aup = cameraAup + local64;
                candidate.ThermalSignature = thermal;
                candidate.AttractionRadius = attractionRadius;
                candidate.Velocity = velocity;
                candidate.Score = thermal + attractionRadius;
                candidate.SourceHash = 0xFACA313u ^ (uint)index;
                candidate.SourceIndex = (uint)index;
                candidate.Flags = 1u | ParasiteSwarmContracts.TelemetryFlagMockTargets;
                _targetCandidateScratch[index] = candidate;
            }

            return limit;
        }

        private static int StageThermalSourceSignals(
            double3 cameraAup,
            in ParasiteSwarmTuningDTO tuning,
            ParasiteTargetCandidateDTO[] candidates,
            out int eligibleSignalCount)
        {
            eligibleSignalCount = 0;
            if (candidates == null || candidates.Length <= 0)
                return 0;

            ReadOnlySpan<ThermalSourceSignal> signals = SignalBus<ThermalSourceSignal>.GetFrameSnapshot();
            int written = 0;
            int capacity = candidates.Length;
            for (int i = 0; i < signals.Length; i++)
            {
                ThermalSourceSignal signal = signals[i];
                if (!signal.PositionAup.IsFinite() ||
                    !math.isfinite(signal.RadiusMeters) ||
                    signal.RadiusMeters <= 0f ||
                    !math.isfinite(signal.IntensityCelsiusPerSecond) ||
                    signal.IntensityCelsiusPerSecond < tuning.ParasiteAttractionThreshold)
                {
                    continue;
                }

                double3 aup = signal.PositionAup.ToAbsoluteDouble3();
                double3 delta = aup - cameraAup;
                if (!math.all(math.isfinite(delta)) || math.lengthsq(delta) > 25000000.0)
                    continue;

                eligibleSignalCount++;
                if (written >= capacity)
                    continue;

                float phase = ((signal.Frame & 1023u) + (signal.SourceId * 0.073f) + (uint)i) * 0.017453292f;
                float3 velocity;
                velocity.x = ParasiteSwarmContracts.FastSinApprox(phase) * 0.25f;
                velocity.y = 0f;
                velocity.z = ParasiteSwarmContracts.FastCosApprox(phase) * 0.25f;

                ParasiteTargetCandidateDTO candidate = default;
                candidate.Aup = aup;
                candidate.ThermalSignature = signal.IntensityCelsiusPerSecond;
                candidate.AttractionRadius = math.max(0.25f, signal.RadiusMeters);
                candidate.Velocity = velocity;
                candidate.Score = 0f;
                candidate.SourceHash = signal.SourceId == 0u ? (0xA7131300u ^ (uint)i) : signal.SourceId;
                candidate.SourceIndex = (uint)i;
                candidate.Flags = 1u;
                candidates[written] = candidate;
                written++;
            }

            return written;
        }

        private ParasiteTargetCandidateDTO ScoreStagedCandidate(
            ParasiteTargetCandidateDTO staged,
            double3 cameraAup,
            in ParasiteSwarmTuningDTO tuning)
        {
            if ((staged.Flags & 1u) == 0u ||
                !math.all(math.isfinite(staged.Aup)) ||
                !math.isfinite(staged.ThermalSignature) ||
                staged.ThermalSignature < tuning.ParasiteAttractionThreshold)
            {
                return default;
            }

            double3 delta = staged.Aup - cameraAup;
            double distanceSq64 = math.lengthsq(delta);
            if (!math.all(math.isfinite(delta)) || distanceSq64 > 25000000.0)
                return default;

            float radius = math.max(0.25f, staged.AttractionRadius * tuning.AttractionRadiusScale);
            float distanceSq = (float)math.max(0.0001, distanceSq64);
            float distance = distanceSq * math.rsqrt(distanceSq);
            staged.AttractionRadius = radius;
            staged.Score = (staged.ThermalSignature * 3f) + radius - distance * 0.015f;
            staged.Flags |= 1u;
            if (!math.all(math.isfinite(staged.Velocity)))
                staged.Velocity = default;
            return staged;
        }

        private int SelectTopTargetsFromScratch(
            int candidateCount,
            double3 cameraAup,
            in ParasiteSwarmTuningDTO tuning)
        {
            int targetLimit = math.min(ParasiteSwarmContracts.MaxTargetCount, _targetSelectionScratch.Length);
            for (int i = 0; i < targetLimit; i++)
                _targetSelectionScratch[i] = default;

            int selected = 0;
            int scanCount = math.clamp(candidateCount, 0, _targetCandidateScratch.Length);
            float* selectedScores = stackalloc float[ParasiteSwarmContracts.MaxTargetCount];
            for (int i = 0; i < ParasiteSwarmContracts.MaxTargetCount; i++)
                selectedScores[i] = -3.402823e+38f;

            for (int i = 0; i < scanCount; i++)
            {
                ParasiteTargetCandidateDTO candidate = _targetCandidateScratch[i];
                if ((candidate.Flags & 1u) == 0u ||
                    !math.isfinite(candidate.Score) ||
                    candidate.ThermalSignature < tuning.ParasiteAttractionThreshold)
                {
                    continue;
                }

                double3 local64 = candidate.Aup - cameraAup;
                float3 local;
                local.x = (float)local64.x;
                local.y = (float)local64.y;
                local.z = (float)local64.z;

                ParasiteTargetDTO target = default;
                target.LocalPosition = local;
                target.ThermalSignature = candidate.ThermalSignature;
                target.Velocity = candidate.Velocity;
                target.AttractionRadius = math.max(0.25f, candidate.AttractionRadius);

                int slot = selected;
                int compareLimit = math.min(selected, targetLimit);
                for (int j = 0; j < compareLimit; j++)
                {
                    if (candidate.Score > selectedScores[j])
                    {
                        slot = j;
                        break;
                    }
                }

                if (slot >= targetLimit)
                    continue;

                int shiftStart = math.min(selected, targetLimit - 1);
                for (int j = shiftStart; j > slot; j--)
                {
                    _targetSelectionScratch[j] = _targetSelectionScratch[j - 1];
                    selectedScores[j] = selectedScores[j - 1];
                }

                _targetSelectionScratch[slot] = target;
                selectedScores[slot] = candidate.Score;
                selected = math.min(selected + 1, targetLimit);
            }

            return selected;
        }

        private uint DispatchAndRender(Vector3 cameraPosition, int particleBudget, int targetCount, float quality, float visualPhaseRadians, in ParasiteSwarmTuningDTO tuning)
        {
            uint flags = 0u;
            if (parasiteCompute == null ||
                parasiteMaterial == null ||
                renderCamera == null ||
                _commandBuffer == null ||
                _particleBufferA == null ||
                _particleBufferB == null ||
                _targetBufferA == null ||
                _targetBufferB == null ||
                _frameParamsBufferA == null ||
                _frameParamsBufferB == null ||
                _visibleIndicesBuffer == null ||
                _indirectArgsBuffer == null ||
                !_particleBufferA.IsValid() ||
                !_particleBufferB.IsValid() ||
                !_targetBufferA.IsValid() ||
                !_targetBufferB.IsValid() ||
                !_frameParamsBufferA.IsValid() ||
                !_frameParamsBufferB.IsValid() ||
                !_visibleIndicesBuffer.IsValid() ||
                !_indirectArgsBuffer.IsValid() ||
                _advectKernel < 0 ||
                _clearArgsKernel < 0 ||
                _rebaseKernel < 0 ||
                _cullKernel < 0 ||
                _clearArgsThreadGroupSizeX <= 0)
            {
                flags |= ParasiteSwarmContracts.TelemetryFlagNoCompute;
            }

            int clearArgsGroups = ResolveDispatchGroups(1, _clearArgsThreadGroupSizeX);
            if (clearArgsGroups <= 0)
                flags |= ParasiteSwarmContracts.TelemetryFlagNoCompute;

            if ((flags & ParasiteSwarmContracts.TelemetryFlagNoCompute) == 0u)
            {
                bool readA = (_bufferParity & 1) == 0;
                GraphicsBuffer read = readA ? _particleBufferA : _particleBufferB;
                GraphicsBuffer write = readA ? _particleBufferB : _particleBufferA;
                Texture flowTexture = abyssalFlowField != null ? abyssalFlowField : _emptyFlowTexture;

                _commandBuffer.Clear();
                _commandBuffer.BeginSample(ComputeSampleName);
                _commandBuffer.SetComputeBufferParam(parasiteCompute, _clearArgsKernel, ParasiteIndirectArgsId, _indirectArgsBuffer);
                _commandBuffer.DispatchCompute(parasiteCompute, _clearArgsKernel, clearArgsGroups, 1, 1);

                if (targetCount <= 0 || particleBudget <= 0)
                {
                    _commandBuffer.EndSample(ComputeSampleName);
                    UnityEngine.Graphics.ExecuteCommandBuffer(_commandBuffer);
                    return flags;
                }

                int rebaseGroups = ResolveDispatchGroups(particleBudget, _rebaseThreadGroupSizeX);
                int advectGroups = ResolveDispatchGroups(particleBudget, _advectThreadGroupSizeX);
                int cullGroups = ResolveDispatchGroups(particleBudget, _cullThreadGroupSizeX);
                if (advectGroups <= 0 ||
                    cullGroups <= 0 ||
                    (math.lengthsq(_pendingAupShift) > 0.000001f && rebaseGroups <= 0))
                {
                    _commandBuffer.EndSample(ComputeSampleName);
                    UnityEngine.Graphics.ExecuteCommandBuffer(_commandBuffer);
                    return flags | ParasiteSwarmContracts.TelemetryFlagNoCompute;
                }

                if (!UploadFrameParams(SimulationTickDeltaSeconds, visualPhaseRadians, particleBudget, targetCount, quality, in tuning, out GraphicsBuffer frameParamsBuffer))
                {
                    _commandBuffer.EndSample(ComputeSampleName);
                    UnityEngine.Graphics.ExecuteCommandBuffer(_commandBuffer);
                    return flags | ParasiteSwarmContracts.TelemetryFlagNoCompute;
                }

                if (math.lengthsq(_pendingAupShift) > 0.000001f && _rebaseKernel >= 0)
                {
                    _commandBuffer.SetComputeVectorParam(parasiteCompute, ParasiteAupShiftDeltaId, new Vector4(_pendingAupShift.x, _pendingAupShift.y, _pendingAupShift.z, 0f));
                    _commandBuffer.SetComputeBufferParam(parasiteCompute, _rebaseKernel, ParasiteReadId, read);
                    _commandBuffer.SetComputeBufferParam(parasiteCompute, _rebaseKernel, ParasiteWriteId, write);
                    _commandBuffer.SetComputeBufferParam(parasiteCompute, _rebaseKernel, ParasiteFrameParamsId, frameParamsBuffer);
                    _commandBuffer.DispatchCompute(parasiteCompute, _rebaseKernel, rebaseGroups, 1, 1);
                    GraphicsBuffer rebased = write;
                    write = read;
                    read = rebased;
                    _bufferParity ^= 1;
                    _pendingAupShift = default;
                }

                _commandBuffer.SetComputeBufferParam(parasiteCompute, _advectKernel, ParasiteReadId, read);
                _commandBuffer.SetComputeBufferParam(parasiteCompute, _advectKernel, ParasiteWriteId, write);
                _commandBuffer.SetComputeBufferParam(parasiteCompute, _advectKernel, ParasiteTargetsId, ResolveCurrentTargetBuffer());
                _commandBuffer.SetComputeBufferParam(parasiteCompute, _advectKernel, ParasiteFrameParamsId, frameParamsBuffer);
                // Bind only a real texture. Setup no longer aborts when both the authored flow volume and its
                // neutral fallback are unassigned, so this is now reachable with flowTexture null - and a
                // null Texture converts to a RenderTargetIdentifier of BuiltinRenderTextureType.None, which
                // would be a per-frame bad bind in a LateFrameTick-cadence method. Leaving _H8AbyssalFlowField
                // unbound reads zero instead, which is what the authored "empty" volume encodes anyway.
                // One Unity-object null compare per frame, no allocation.
                if (flowTexture != null)
                    _commandBuffer.SetComputeTextureParam(parasiteCompute, _advectKernel, AbyssalFlowFieldId, flowTexture);

                _commandBuffer.DispatchCompute(parasiteCompute, _advectKernel, advectGroups, 1, 1);

                _commandBuffer.SetComputeBufferParam(parasiteCompute, _cullKernel, ParasiteReadId, write);
                _commandBuffer.SetComputeBufferParam(parasiteCompute, _cullKernel, ParasiteVisibleIndicesId, _visibleIndicesBuffer);
                _commandBuffer.SetComputeBufferParam(parasiteCompute, _cullKernel, ParasiteIndirectArgsId, _indirectArgsBuffer);
                _commandBuffer.SetComputeBufferParam(parasiteCompute, _cullKernel, ParasiteFrameParamsId, frameParamsBuffer);
                _commandBuffer.DispatchCompute(parasiteCompute, _cullKernel, cullGroups, 1, 1);
                _commandBuffer.EndSample(ComputeSampleName);
                UnityEngine.Graphics.ExecuteCommandBuffer(_commandBuffer);
                _bufferParity ^= 1;
            }
            else
            {
                return flags;
            }

            GraphicsBuffer current = (_bufferParity & 1) == 0 ? _particleBufferA : _particleBufferB;
            Material material = parasiteMaterial;
            if (material != null &&
                _indirectArgsBuffer != null &&
                _visibleIndicesBuffer != null &&
                _drawParamsBufferA != null &&
                _drawParamsBufferB != null &&
                _particleBufferA != null &&
                _particleBufferB != null &&
                current != null &&
                current.IsValid() &&
                _particleBufferA.IsValid() &&
                _particleBufferB.IsValid() &&
                _visibleIndicesBuffer.IsValid() &&
                _drawParamsBufferA.IsValid() &&
                _drawParamsBufferB.IsValid())
            {
                BindMaterialBuffers(material);
                if (UploadDrawParams(cameraPosition, _bufferParity, out GraphicsBuffer drawParamsBuffer))
                {
                    material.SetBuffer(ParasiteDrawParamsId, drawParamsBuffer);

                    Bounds bounds = new Bounds(cameraPosition, Vector3.one * math.max(1f, renderBoundsMeters));
                    UnityEngine.Graphics.DrawProceduralIndirect(
                        material,
                        bounds,
                        MeshTopology.Triangles,
                        _indirectArgsBuffer,
                        0,
                        renderCamera,
                        null,
                        ShadowCastingMode.Off,
                        false,
                        gameObject.layer);
                }
            }

            return flags;
        }

        private bool UploadFrameParams(
            float tickDeltaSeconds,
            float visualPhaseRadians,
            int particleBudget,
            int targetCount,
            float quality,
            in ParasiteSwarmTuningDTO tuning,
            out GraphicsBuffer frameParamsBuffer)
        {
            frameParamsBuffer = ResolveNextFrameParamsBuffer();
            if (frameParamsBuffer == null || !frameParamsBuffer.IsValid())
                return false;

            bool locked = false;
            bool uploaded = false;
            try
            {
                NativeArray<ParasiteFrameParamsDTO> mapped = frameParamsBuffer.LockBufferForWrite<ParasiteFrameParamsDTO>(0, 1);
                locked = true;
                mapped[0] = new ParasiteFrameParamsDTO
                {
                    Frame0 = new float4(tickDeltaSeconds, visualPhaseRadians, particleBudget, targetCount),
                    Frame1 = new float4(quality, tuning.ThermalAttractionMultiplier, tuning.CurlNoiseFrequency, tuning.SwarmMaxSpeed),
                    Frame2 = new float4(tuning.CurlStrength, tuning.FlowFieldWeight, tuning.AttachmentShellRadius, tuning.TargetVelocityBlend),
                    Reserved = default
                };
                uploaded = true;
            }
            finally
            {
                if (locked)
                    frameParamsBuffer.UnlockBufferAfterWrite<ParasiteFrameParamsDTO>(1);
            }

            if (!uploaded)
                return false;

            _frameParamsBufferParity ^= 1;
            return true;
        }

        private bool UploadDrawParams(Vector3 cameraPosition, int bufferParity, out GraphicsBuffer drawParamsBuffer)
        {
            drawParamsBuffer = ResolveNextDrawParamsBuffer();
            if (drawParamsBuffer == null || !drawParamsBuffer.IsValid())
                return false;

            bool locked = false;
            bool uploaded = false;
            try
            {
                NativeArray<float4> mapped = drawParamsBuffer.LockBufferForWrite<float4>(0, 1);
                locked = true;
                mapped[0] = new float4(cameraPosition.x, cameraPosition.y, cameraPosition.z, (bufferParity & 1) != 0 ? 1f : 0f);
                uploaded = true;
            }
            finally
            {
                if (locked)
                    drawParamsBuffer.UnlockBufferAfterWrite<float4>(1);
            }

            if (!uploaded)
                return false;

            _drawParamsBufferParity ^= 1;
            return true;
        }

        private bool RecordTelemetry(
            NativeArray<SwarmTelemetryEntry> telemetry,
            NativeArray<int> telemetryCursor,
            NativeArray<ParasiteTargetDTO> targets,
            bool targetsReadable,
            uint visualFrame,
            int targetCount,
            int particleBudget,
            int estimatedGpuUs,
            float quality,
            in ParasiteSwarmTuningDTO tuning,
            ref uint flags)
        {
            if (!telemetry.IsCreated || telemetry.Length < ParasiteSwarmContracts.TelemetryCapacity)
                return false;

            int cursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? telemetryCursor[0] : _telemetryCursor;
            cursor = math.clamp(cursor, 0, telemetry.Length - 1);
            float maxThermal = 0f;
            float3 strongest = default;
            int overflowCount = math.max(0, _lastCandidateOverflowCount);
            if (overflowCount > 0)
                flags |= ParasiteSwarmContracts.TelemetryFlagTargetOverflow;

            int readableTargetCount = targetsReadable ? targetCount : 0;
            for (int i = 0; i < readableTargetCount && i < targets.Length; i++)
            {
                ParasiteTargetDTO target = targets[i];
                if (target.ThermalSignature > maxThermal)
                {
                    maxThermal = target.ThermalSignature;
                    strongest = target.LocalPosition;
                }
            }

            if (!math.all(math.isfinite(strongest)) || !math.isfinite(maxThermal))
                flags |= ParasiteSwarmContracts.TelemetryFlagInvalidMath;

            telemetry[cursor] = new SwarmTelemetryEntry
            {
                Frame = visualFrame,
                TargetCount = (uint)math.max(0, targetCount),
                ParticleBudget = (uint)math.max(0, particleBudget),
                EstimatedGpuMicroseconds = (uint)math.max(0, estimatedGpuUs),
                GlobalQualityWeight = quality,
                MaxThermalSignature = maxThermal,
                StateHash = ParasiteSwarmContracts.HashState(visualFrame, targetCount, particleBudget, quality, tuning.Version),
                Flags = flags | (_blackBoxDumped ? ParasiteSwarmContracts.TelemetryFlagDumped : 0u),
                StrongestTargetLocal = strongest,
                OverflowCount = (uint)overflowCount,
                DumpSequence = _dumpSequence,
                ActiveProfileHash = tuning.ActiveProfileHash,
                RebaseFrame = _lastRebaseFrame
            };

            cursor++;
            if (cursor >= telemetry.Length)
                cursor = 0;

            _telemetryCursor = cursor;
            if (telemetryCursor.IsCreated && telemetryCursor.Length > 0)
                telemetryCursor[0] = cursor;

            return true;
        }

        private void UploadTargets(NativeArray<ParasiteTargetDTO> targets, int count)
        {
            GraphicsBuffer targetBuffer = ResolveNextTargetBuffer();
            if (targetBuffer == null || !targetBuffer.IsValid() || !targets.IsCreated)
                return;

            int safeCount = math.clamp(count, 0, math.min(targets.Length, targetBuffer.count));
            if (safeCount <= 0)
                return;

            bool locked = false;
            bool uploaded = false;
            try
            {
                NativeArray<ParasiteTargetDTO> mapped = targetBuffer.LockBufferForWrite<ParasiteTargetDTO>(0, safeCount);
                locked = true;
                void* dst = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                void* src = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(targets);
                UnsafeUtility.MemCpy(dst, src, (long)safeCount * UnsafeUtility.SizeOf<ParasiteTargetDTO>());
                uploaded = true;
            }
            finally
            {
                if (locked)
                    targetBuffer.UnlockBufferAfterWrite<ParasiteTargetDTO>(safeCount);
            }

            if (uploaded)
                _targetBufferParity ^= 1;
        }

        private void ResolveAupShiftSignals()
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                AupShiftSignal signal = shifts[i];
                if (!math.all(math.isfinite(signal.ShiftMeters)))
                    continue;

                _pendingAupShift += signal.ShiftMeters;
                _lastRebaseFrame = signal.ShiftFrameId;
            }
        }

        private bool TryResolveCameraAup(out double3 cameraAup, out Vector3 cameraPosition)
        {
            IPlayerRuntimeContext player = _playerContext;
            if (player != null && player.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot))
            {
                cameraPosition = new Vector3(snapshot.RuntimePosition.x, snapshot.RuntimePosition.y, snapshot.RuntimePosition.z);
                cameraAup = snapshot.Aup.ToAbsoluteDouble3();
                return math.all(math.isfinite(cameraAup)) && math.all(math.isfinite(snapshot.RuntimePosition));
            }

            cameraPosition = default;
            cameraAup = default;
            return false;
        }

        private ParasiteSwarmTuningDTO ReadSanitizedTuning(NativeArray<ParasiteSwarmTuningDTO> tuning)
        {
            ParasiteSwarmTuningDTO value = tuning.IsCreated && tuning.Length > 0 ? tuning[0] : ParasiteSwarmContracts.DefaultTuning();
            return ParasiteSwarmContracts.Sanitize(value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveGlobalQuality(in ParasiteSwarmTuningDTO tuning)
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            return ParasiteSwarmContracts.ResolveQuality01(in tuning, quality);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveKernelThreadGroupSizeX(ComputeShader compute, int kernel)
        {
            if (compute == null ||
                kernel < 0 ||
                !SystemInfo.supportsComputeShaders)
                return 0;

            uint sizeX;
            uint sizeY;
            uint sizeZ;
            try
            {
                if (!compute.IsSupported(kernel))
                    return 0;

                compute.GetKernelThreadGroupSizes(kernel, out sizeX, out sizeY, out sizeZ);
            }
            catch (System.ObjectDisposedException)
            {
                return 0;
            }
            catch (System.InvalidOperationException)
            {
                return 0;
            }
            catch (System.ArgumentException)
            {
                return 0;
            }
            catch (MissingReferenceException)
            {
                return 0;
            }
            catch (UnityException)
            {
                return 0;
            }
            if (sizeX == 0u || sizeY != 1u || sizeZ != 1u || sizeX > int.MaxValue)
                return 0;

            ulong totalThreads = sizeX * (ulong)sizeY * sizeZ;
            return totalThreads <= PortableMaxComputeThreadsPerGroup ? (int)sizeX : 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int ResolveDispatchGroups(int activeCount, int threadGroupSizeX)
        {
            if (activeCount <= 0 || threadGroupSizeX <= 0)
                return 0;

            long groups = ((long)activeCount + threadGroupSizeX - 1L) / threadGroupSizeX;
            return groups <= MaxDispatchGroupsPerDimension ? (int)groups : 0;
        }

        private GraphicsBuffer ResolveCurrentTargetBuffer()
        {
            return (_targetBufferParity & 1) == 0 ? _targetBufferA : _targetBufferB;
        }

        private GraphicsBuffer ResolveNextTargetBuffer()
        {
            return (_targetBufferParity & 1) == 0 ? _targetBufferB : _targetBufferA;
        }

        private GraphicsBuffer ResolveNextDrawParamsBuffer()
        {
            return (_drawParamsBufferParity & 1) == 0 ? _drawParamsBufferB : _drawParamsBufferA;
        }

        private GraphicsBuffer ResolveNextFrameParamsBuffer()
        {
            return (_frameParamsBufferParity & 1) == 0 ? _frameParamsBufferB : _frameParamsBufferA;
        }

        private uint AdvanceVisualFrame(out float visualPhaseRadians)
        {
            uint frame = _visualFrameCounter++;
            visualPhaseRadians = (frame & VisualPhaseTickMask) * VisualPhaseStepRadians;
            return frame;
        }

        private bool TryResolveGpuParticleCapacity(out int capacity)
        {
            capacity = 0;
            if (_particleBufferA == null ||
                _particleBufferB == null ||
                !_particleBufferA.IsValid() ||
                !_particleBufferB.IsValid())
            {
                return false;
            }

            capacity = math.min(_particleBufferA.count, _particleBufferB.count);
            return capacity > 0;
        }

        private bool TryReadHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : unmanaged
        {
            buffer = default;
            return _vault != null &&
                   IsOwnedHandle(in handle, expectedBufferId) &&
                   _vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private bool TryResolveHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            int requiredLength,
            out NativeArray<T> buffer)
            where T : unmanaged
        {
            buffer = default;
            return _vault != null &&
                   IsOwnedHandle(in handle, expectedBufferId) &&
                   _vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool IsOwnedHandle<T>(in VaultGenerationHandle<T> handle, BufferID expectedBufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)expectedBufferId) &&
                   handle.SystemID == (uint)OwnerSystemId &&
                   handle.Generation != 0u;
        }

        private void DisposeGpuResources()
        {
            ReleaseBuffer(ref _particleBufferA);
            ReleaseBuffer(ref _particleBufferB);
            ReleaseBuffer(ref _targetBufferA);
            ReleaseBuffer(ref _targetBufferB);
            ReleaseBuffer(ref _visibleIndicesBuffer);
            ReleaseBuffer(ref _indirectArgsBuffer);
            ReleaseBuffer(ref _drawParamsBufferA);
            ReleaseBuffer(ref _drawParamsBufferB);
            ReleaseBuffer(ref _frameParamsBufferA);
            ReleaseBuffer(ref _frameParamsBufferB);

            if (_commandBuffer != null)
            {
                _commandBuffer.Release();
                _commandBuffer = null;
            }

            if (_emptyFlowTexture != null)
                _emptyFlowTexture = null;

            _boundMaterial = null;
        }

        private static void ReleaseBuffer(ref GraphicsBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }
    }
}
