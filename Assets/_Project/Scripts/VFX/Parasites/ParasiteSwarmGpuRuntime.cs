using System;
using System.IO;
using System.Runtime.CompilerServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
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
        [Header("GPU")]
        [SerializeField] private ComputeShader parasiteCompute;
        [SerializeField] private Material parasiteMaterial;
        [SerializeField] private Camera renderCamera;
        [SerializeField] private Texture3D abyssalFlowField;

        [Header("Runtime")]
        [SerializeField] private int configuredMaxParticles = 500000;
        [SerializeField] private float renderBoundsMeters = 96f;
        [SerializeField] private bool forceMockTargets;

        private IDataVault _vault;
        private bool _registered;
        private bool _hotSwapRegistered;
        private bool _initialized;
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
        private bool _targetSelectionPending;
        private bool _targetWriteLocksHeld;
        private JobHandle _targetSelectionHandle;
        private int _lastResolvedTargetCount;
        private IPlayerRuntimeContext _playerContext;
        private bool _renderCameraRuntimeResolved;

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
            CreateGpuResources();
            ResolveComputeKernels();
            InitializeGpuParticles();
            _visualFrameCounter = 0u;
            _registered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);
            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
            _initialized = true;
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
            ReleaseTargetWriteLocks();
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

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                RebindDataVaultForLifecycle(currentService as IDataVault);
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

            int resolvedTargetCount = ResolveCompletedTargetSelection(targetCount);
            bool targetArrayReadable = !_targetSelectionPending;
            if (targetArrayReadable)
                UploadTargets(targets, resolvedTargetCount);

            int dispatchedParticleBudget = resolvedTargetCount > 0 ? particleBudget : 0;
            uint visualFrame = AdvanceVisualFrame(out float visualPhaseRadians);
            uint flags = DispatchAndRender(cameraPosition, dispatchedParticleBudget, resolvedTargetCount, globalQuality, visualPhaseRadians, in tuning);
            if (!gpuParticleCapacityValid)
                flags |= ParasiteSwarmContracts.TelemetryFlagNoCompute;

            int estimatedGpuUs = ParasiteSwarmContracts.EstimateGpuMicroseconds(dispatchedParticleBudget, resolvedTargetCount, globalQuality);
            flags |= ParasiteSwarmContracts.TelemetryFlagTimingEstimated;
            if (estimatedGpuUs > FaultGpuDumpThresholdMicroseconds)
                flags |= ParasiteSwarmContracts.TelemetryFlagGpuBudgetSpike;
            if (_lastCandidateOverflowCount > 0)
                flags |= ParasiteSwarmContracts.TelemetryFlagTargetOverflow;

            if (TryAcquireTelemetryWriteBuffers(out NativeArray<SwarmTelemetryEntry> telemetry, out NativeArray<int> telemetryCursor))
            {
                try
                {
                    if (RecordTelemetry(telemetry, telemetryCursor, targets, targetArrayReadable, visualFrame, resolvedTargetCount, dispatchedParticleBudget, estimatedGpuUs, globalQuality, in tuning, ref flags) &&
                        ((flags & (ParasiteSwarmContracts.TelemetryFlagTargetOverflow | ParasiteSwarmContracts.TelemetryFlagGpuBudgetSpike | ParasiteSwarmContracts.TelemetryFlagInvalidMath)) != 0u) &&
                        !_blackBoxDumped)
                    {
                        _dumpSequence++;
                        int dumpCursor = telemetryCursor.IsCreated && telemetryCursor.Length > 0 ? telemetryCursor[0] : _telemetryCursor;
                        _blackBoxDumped = ParasiteSwarmContracts.TryWriteTelemetryDump(_dumpRootPath, telemetry, dumpCursor);
                    }
                }
                finally
                {
                    _vault.ReleaseWriteLock(in _telemetryCursorHandle, OwnerSystemId);
                    _vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystemId);
                }
            }

            ScheduleTargetExtraction(cameraAup, globalQuality, visualPhaseRadians, in tuning);
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
            _targetSelectionPending = false;
            _targetWriteLocksHeld = false;
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
            ReleaseVaultHandle<byte>(vault, BufferID.ShinobuParasiteCsvScratch);
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

            CompleteTargetSelectionForLifecycle();
            ReleaseTargetWriteLocks();
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

        private void CompleteTargetSelectionForLifecycle()
        {
            if (!_targetSelectionPending)
                return;

            // Lifecycle-only fence. The hot path consumes target extraction one frame late.
            DispatcherJobFence.TryComplete(ref _targetSelectionHandle, forceComplete: true);
            _targetSelectionPending = false;
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

            if (!vault.TryGetGenerationHandle(BufferID.ShinobuParasiteCsvScratch, out VaultGenerationHandle<byte> scratchHandle))
                return;

            if (!IsOwnedHandle(in scratchHandle, BufferID.ShinobuParasiteCsvScratch))
                return;

            if (!vault.TryAcquireWriteLock(in scratchHandle, OwnerSystemId, out NativeArray<byte> scratch))
                return;

            try
            {
                int maxBytes = math.min(scratch.IsCreated ? scratch.Length : 0, ParasiteSwarmContracts.CsvScratchBytes);
                int bytesRead = ReadCsvFileIntoScratch(path, scratch, maxBytes);
                if (bytesRead <= 0)
                    return;

                byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(scratch);
                ParasiteSwarmContracts.LoadProfilesFromCsv(vault, new ReadOnlySpan<byte>(ptr, bytesRead));
            }
            finally
            {
                vault.ReleaseWriteLock(in scratchHandle, OwnerSystemId);
            }
        }

        private static int ReadCsvFileIntoScratch(string path, NativeArray<byte> scratch, int maxBytes)
        {
            if (!scratch.IsCreated || maxBytes <= 0)
                return 0;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, math.max(1, maxBytes), FileOptions.SequentialScan))
            {
                if (stream.Length > maxBytes)
                    return 0;

                int limit = (int)stream.Length;
                int total = 0;
                while (total < limit)
                {
                    int read = stream.Read(new Span<byte>(ptr + total, limit - total));
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
            if (_vault == null || !IsOwnedHandle(in _tuningHandle, BufferID.ShinobuParasiteTuning))
                return;

            if (!_vault.TryAcquireWriteLock(in _tuningHandle, OwnerSystemId, out NativeArray<ParasiteSwarmTuningDTO> tuning))
                return;

            try
            {
                if (tuning.Length > 0 && tuning[0].Version == 0u)
                    tuning[0] = ParasiteSwarmContracts.DefaultTuning();
            }
            finally
            {
                _vault.ReleaseWriteLock(in _tuningHandle, OwnerSystemId);
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
                GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw,
                1,
                UnsafeUtility.SizeOf<ParasiteIndirectArgsDTO>()); // COLD ALLOC: GraphicsBuffer[1] - compute-written parasite indirect args - owner: SHINOBU_313

            if (_emptyFlowTexture == null)
            {
                _emptyFlowTexture = new Texture3D(1, 1, 1, TextureFormat.RGBAFloat, false)
                {
                    name = "H8 Empty Parasite Flow Texture",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Point
                }; // COLD ALLOC: Texture3D[1] - zero flow fallback - owner: SHINOBU_313
                _emptyFlowTexture.SetPixel(0, 0, 0, Color.clear);
                _emptyFlowTexture.Apply(false, true);
            }

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
            if (parasiteCompute == null || !HardwareTierDetector.AllowHighResourceComputeShaders)
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
            if (shader == null || !HardwareTierDetector.AllowHighResourceComputeShaders || !shader.HasKernel(kernelName))
                return -1;

            int kernel = shader.FindKernel(kernelName);
            return kernel >= 0 && shader.IsSupported(kernel) ? kernel : -1;
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

        private int ResolveCompletedTargetSelection(NativeArray<int> targetCount)
        {
            if (!_targetSelectionPending)
                return _lastResolvedTargetCount;

            if (!_targetSelectionHandle.IsCompleted)
                return _lastResolvedTargetCount;

            // Safety fence after IsCompleted, so LateFrame never waits on target extraction.
            DispatcherJobFence.TryFinalizeCompleted(ref _targetSelectionHandle);
            _targetSelectionPending = false;
            ReleaseTargetWriteLocks();
            _lastResolvedTargetCount = targetCount.IsCreated && targetCount.Length > 0
                ? math.clamp(targetCount[0], 0, ParasiteSwarmContracts.MaxTargetCount)
                : 0;
            return _lastResolvedTargetCount;
        }

        private void ScheduleTargetExtraction(
            double3 cameraAup,
            float quality,
            float visualPhaseRadians,
            in ParasiteSwarmTuningDTO tuning)
        {
            if (_targetSelectionPending)
                return;

            if (_targetWriteLocksHeld)
                ReleaseTargetWriteLocks();

            if (!TryAcquireTargetWriteBuffers(
                    out NativeArray<ParasiteTargetDTO> targets,
                    out NativeArray<ParasiteTargetCandidateDTO> candidates,
                    out NativeArray<int> targetCount))
                return;

            bool useMock = forceMockTargets || (tuning.Flags & ParasiteSwarmContracts.TuningFlagMockTargets) != 0u;
            if (useMock)
            {
                _lastCandidateOverflowCount = 0;
                int mockCount = math.min(ParasiteSwarmContracts.MaxTargetCount, targets.Length);
                JobHandle mock = new GenerateMockThermalTargetsJob
                {
                    Targets = targets,
                    Candidates = candidates,
                    TargetCount = targetCount,
                    CameraAup = cameraAup,
                    PhaseRadians = visualPhaseRadians,
                    GlobalQualityWeight = quality,
                    Tuning = tuning
                }.Schedule(mockCount, 4);

                _targetSelectionHandle = new SelectTopParasiteTargetsJob
                {
                    Candidates = candidates,
                    CandidateCount = mockCount,
                    Targets = targets,
                    TargetCount = targetCount,
                    CameraAup = cameraAup,
                    Tuning = tuning
                }.Schedule(mock);
                _targetSelectionPending = true;
                return;
            }

            int candidateCount = StageThermalSourceSignals(cameraAup, in tuning, candidates, out int eligibleSignalCount);
            _lastCandidateOverflowCount = math.max(0, eligibleSignalCount - ParasiteSwarmContracts.MaxTargetCount);
            if (candidateCount <= 0)
            {
                ClearTargets(targets, targetCount);
                _lastResolvedTargetCount = 0;
                ReleaseTargetWriteLocks();
                return;
            }

            JobHandle extraction = new ExtractParasiteTargetsJob
            {
                Candidates = candidates,
                CandidateCount = targetCount,
                StagedCount = candidateCount,
                CameraAup = cameraAup,
                Tuning = tuning
            }.Schedule(candidateCount, 32);

            _targetSelectionHandle = new SelectTopParasiteTargetsJob
            {
                Candidates = candidates,
                CandidateCount = candidateCount,
                Targets = targets,
                TargetCount = targetCount,
                CameraAup = cameraAup,
                Tuning = tuning
            }.Schedule(extraction);
            _targetSelectionPending = true;
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

        private bool TryAcquireTargetWriteBuffers(
            out NativeArray<ParasiteTargetDTO> targets,
            out NativeArray<ParasiteTargetCandidateDTO> candidates,
            out NativeArray<int> targetCount)
        {
            targets = default;
            candidates = default;
            targetCount = default;

            if (_vault == null ||
                !IsOwnedHandle(in _targetsHandle, BufferID.ShinobuParasiteTargets) ||
                !IsOwnedHandle(in _candidatesHandle, BufferID.ShinobuParasiteTargetCandidates) ||
                !IsOwnedHandle(in _targetCountHandle, BufferID.ShinobuParasiteTargetCount))
                return false;

            if (!_vault.TryAcquireWriteLock(in _targetsHandle, OwnerSystemId, out targets))
                return false;

            if (!_vault.TryAcquireWriteLock(in _candidatesHandle, OwnerSystemId, out candidates))
            {
                _vault.ReleaseWriteLock(in _targetsHandle, OwnerSystemId);
                targets = default;
                return false;
            }

            if (!_vault.TryAcquireWriteLock(in _targetCountHandle, OwnerSystemId, out targetCount))
            {
                _vault.ReleaseWriteLock(in _candidatesHandle, OwnerSystemId);
                _vault.ReleaseWriteLock(in _targetsHandle, OwnerSystemId);
                targets = default;
                candidates = default;
                return false;
            }

            if (!targets.IsCreated ||
                !candidates.IsCreated ||
                !targetCount.IsCreated ||
                targets.Length < ParasiteSwarmContracts.MaxTargetCount ||
                candidates.Length < ParasiteSwarmContracts.CandidateCapacity ||
                targetCount.Length <= 0)
            {
                _vault.ReleaseWriteLock(in _targetCountHandle, OwnerSystemId);
                _vault.ReleaseWriteLock(in _candidatesHandle, OwnerSystemId);
                _vault.ReleaseWriteLock(in _targetsHandle, OwnerSystemId);
                targets = default;
                candidates = default;
                targetCount = default;
                return false;
            }

            _targetWriteLocksHeld = true;
            return true;
        }

        private bool TryAcquireTelemetryWriteBuffers(
            out NativeArray<SwarmTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor)
        {
            telemetry = default;
            telemetryCursor = default;

            if (_vault == null ||
                !IsOwnedHandle(in _telemetryHandle, BufferID.ShinobuParasiteTelemetryRing) ||
                !IsOwnedHandle(in _telemetryCursorHandle, BufferID.ShinobuParasiteTelemetryCursor))
                return false;

            if (!_vault.TryAcquireWriteLock(in _telemetryHandle, OwnerSystemId, out telemetry))
                return false;

            if (!_vault.TryAcquireWriteLock(in _telemetryCursorHandle, OwnerSystemId, out telemetryCursor))
            {
                _vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystemId);
                telemetry = default;
                return false;
            }

            if (!telemetry.IsCreated ||
                !telemetryCursor.IsCreated ||
                telemetry.Length < ParasiteSwarmContracts.TelemetryCapacity ||
                telemetryCursor.Length <= 0)
            {
                _vault.ReleaseWriteLock(in _telemetryCursorHandle, OwnerSystemId);
                _vault.ReleaseWriteLock(in _telemetryHandle, OwnerSystemId);
                telemetry = default;
                telemetryCursor = default;
                return false;
            }

            return true;
        }

        private void ReleaseTargetWriteLocks()
        {
            if (!_targetWriteLocksHeld || _vault == null)
                return;

            _vault.ReleaseWriteLock(in _targetCountHandle, OwnerSystemId);
            _vault.ReleaseWriteLock(in _candidatesHandle, OwnerSystemId);
            _vault.ReleaseWriteLock(in _targetsHandle, OwnerSystemId);
            _targetWriteLocksHeld = false;
        }

        private static int StageThermalSourceSignals(
            double3 cameraAup,
            in ParasiteSwarmTuningDTO tuning,
            NativeArray<ParasiteTargetCandidateDTO> candidates,
            out int eligibleSignalCount)
        {
            eligibleSignalCount = 0;
            if (!candidates.IsCreated || candidates.Length <= 0)
                return 0;

            ReadOnlySpan<ThermalSourceSignal> signals = SignalBus<ThermalSourceSignal>.GetFrameSnapshot();
            int written = 0;
            int capacity = candidates.Length;
            for (int i = 0; i < signals.Length; i++)
            {
                ThermalSourceSignal signal = signals[i];
                if (!signal.PositionAup.IsFinite() ||
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
                candidates[written] = new ParasiteTargetCandidateDTO
                {
                    Aup = aup,
                    ThermalSignature = signal.IntensityCelsiusPerSecond,
                    AttractionRadius = math.max(0.25f, signal.RadiusMeters),
                    Velocity = new float3(
                        ParasiteSwarmContracts.FastSinApprox(phase),
                        0f,
                        ParasiteSwarmContracts.FastCosApprox(phase)) * 0.25f,
                    Score = 0f,
                    SourceHash = signal.SourceId == 0u ? (0xA7131300u ^ (uint)i) : signal.SourceId,
                    SourceIndex = (uint)i,
                    Flags = 1u
                };
                written++;
            }

            return written;
        }

        private static void ClearTargets(NativeArray<ParasiteTargetDTO> targets, NativeArray<int> targetCount)
        {
            int targetLimit = math.min(ParasiteSwarmContracts.MaxTargetCount, targets.IsCreated ? targets.Length : 0);
            for (int i = 0; i < targetLimit; i++)
                targets[i] = default;

            if (targetCount.IsCreated && targetCount.Length > 0)
                targetCount[0] = 0;
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
                !HardwareTierDetector.AllowHighResourceComputeShaders ||
                !compute.IsSupported(kernel))
                return 0;

            compute.GetKernelThreadGroupSizes(kernel, out uint sizeX, out uint sizeY, out uint sizeZ);
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
            {
                Destroy(_emptyFlowTexture);
                _emptyFlowTexture = null;
            }

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
