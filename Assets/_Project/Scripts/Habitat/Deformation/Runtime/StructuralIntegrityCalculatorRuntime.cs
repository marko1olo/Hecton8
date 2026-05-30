using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;

namespace Hecton8.Habitat.Deformation
{
    [DisallowMultipleComponent]
    public sealed unsafe partial class StructuralIntegrityCalculatorRuntime : MonoBehaviour, IUpdatable, ILateFrameTickable, IGlobalRegistryHotSwapListener
#if UNITY_EDITOR
        , IColdTickable
#endif
    {
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_218.bin";
        private const string SurgeonDumpRelativePath = "Docs/AgentLogs/Dump_STRUCTURAL_SURGEON.bin";
        private const string DumpPayloadLabel = "structuralIntegrityTelemetryDumpPayload";
        private const string DefaultCsvRelativePath = "Docs/Data/hull_materials.csv";
        private static readonly ProfilerMarker _tickMarker = new ProfilerMarker("H8.Habitat.StructuralIntegrity.Tick");
        private static readonly ProfilerMarker _lateMarker = new ProfilerMarker("H8.Habitat.StructuralIntegrity.LateFrame");
        private static readonly int _stateBufferId = Shader.PropertyToID("_HectonStructuralIntegrityStateBuffer");
        private static readonly int _stateParamsId = Shader.PropertyToID("_HectonStructuralIntegrityParams");
        private const int SolverLockStates = 1 << 0;
        private const int SolverLockNodeAups = 1 << 1;
        private const int SolverLockOffsets = 1 << 2;
        private const int SolverLockDestinations = 1 << 3;
        private const int SolverLockEdgeFlags = 1 << 4;
        private const int SolverLockTelemetry = 1 << 5;
        private const int SolverLockTelemetryCursor = 1 << 6;
        private const int SolverLockTuning = 1 << 7;
        private const int SolverLockSdf = 1 << 8;
        private const int SolverLockMaterials = 1 << 18;
        private const int SolverLockCsvScratch = 1 << 19;
        private static readonly ulong StructuralMutationGuardMask =
            StructuralMutationGuardBit(BufferID.StructuralIntegrityStates) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityNodeAups) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityCsrOffsets) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityCsrDestinations) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityEdgeFlags) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityTelemetryRing) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityTelemetryCursor) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityTuning) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityMaterialStrengths) |
            StructuralMutationGuardBit(BufferID.StructuralIntegrityCsvScratch) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningRawWarnings) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningGroups) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningTimers) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningCounters) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningTelemetryRing) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningTelemetryCursor) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningTuning) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningProfiles) |
            StructuralMutationGuardBit(BufferID.BaseStructuralWarningCsvScratch);
        private static readonly ulong StructuralSolverSdfMutationGuardMask =
            StructuralMutationGuardMask |
            StructuralMutationGuardBit(BufferID.VoxelSdfTexture3D);

        private static StructuralIntegrityCalculatorRuntime s_activeRuntime;

        [Header("Structural Solver")]
        [SerializeField, Range(1, StructuralIntegrityConstants.MaxNodeCapacity)] private int mockNodeCount = 128;
        [SerializeField, Range(0f, 1f)] private float simulationQualityWeight = 1f;
        [SerializeField] private bool generateMockGraphOnEnable = true;
        [SerializeField] private bool uploadStateBufferToShaders = true;
        [SerializeField] private string materialStrengthCsvRelativePath = DefaultCsvRelativePath;

        [Header("Pressure")]
        [SerializeField] private Vector3 seaLevelAup = Vector3.zero;
        [SerializeField, Min(0f)] private float basePressureKPa = 101.325f;
        [SerializeField, Min(0f)] private float pressureGradientKPaPerMeter = 10.05f;
        [SerializeField, Min(0f)] private float pressureToStressScale = 1f;

        [Header("Strength")]
        [SerializeField, Min(0.01f)] private float materialStrengthFactor = 1f;
        [SerializeField, Range(0f, 0.99f)] private float bucklingStart01 = 0.72f;
        [SerializeField, Min(0f)] private float bucklingVisualIntensity = 1f;
        [SerializeField, Min(0f)] private float supportDamping = 0.45f;
        [SerializeField, Min(0.01f)] private float collapseStress01 = 1f;

        [Header("SDF Anchor")]
        [SerializeField] private Vector3 sdfOriginAup = Vector3.zero;
        [SerializeField, Min(0.01f)] private float sdfMetersPerVoxel = 1f;
        [SerializeField, Min(0.01f)] private float sdfRangeMeters = 8f;

        private IDataVault _dataVault;
        private VaultGenerationHandle<IntegrityStateDTO> _statesHandle;
        private VaultGenerationHandle<double3> _nodeAupsHandle;
        private VaultGenerationHandle<int> _csrOffsetsHandle;
        private VaultGenerationHandle<int> _csrDestinationsHandle;
        private VaultGenerationHandle<byte> _edgeFlagsHandle;
        private VaultGenerationHandle<StructuralTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<StructuralTuningDTO> _tuningHandle;
        private VaultGenerationHandle<StructuralMaterialStrengthEntry> _materialsHandle;
        private VaultGenerationHandle<byte> _csvScratchHandle;

        private JobHandle _scheduledHandle;
        private GraphicsBuffer _stateBufferA;
        private GraphicsBuffer _stateBufferB;
        private int _gpuReadIndex;
        private int _initialized;
        private int _registeredUpdate;
        private int _registeredLate;
        private int _registeredCold;
        private int _registeredHotSwap;
        private int _jobScheduled;
        private int _solverLockMask;
        private ulong _solverMutationGuardMask;
        private IDataVault _solverGuardVault;
        private IDataVault _structuralMutationGuardVault;
        private int _activeNodeCount;
        private int _activeEdgeCount;
        private uint _frame;
        private uint _lastDumpedFrame;
        private uint _lastUploadedStateHash;
        private long _lastCsvWriteTicks;
        private int _lastUploadedNodeCount;
        private int _gpuUploadValid;
        private int _materialTableInitialized;
        private uint _glassHash;
        private uint _titaniumHash;
        private uint _plasteelHash;

        public static StructuralIntegrityCalculatorRuntime ActiveRuntime => s_activeRuntime;
        public int ActiveNodeCount => _activeNodeCount;

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            if (TryInitialize())
            {
                s_activeRuntime = this;
                TryRegisterTickables();
            }
            else if (s_activeRuntime == this)
            {
                s_activeRuntime = null;
            }
        }

        private void OnDisable()
        {
            CompleteScheduled(true, true);
            TryUnregisterTickables();
            TryUnregisterHotSwapListener();
            ReleaseGpuBuffers();
            ReleaseVaultHandles();
            if (s_activeRuntime == this)
                s_activeRuntime = null;
            _initialized = 0;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (_initialized == 0 || _jobScheduled != 0)
                return;

            NativeArray<IntegrityStateDTO> states = ResolveVaultBuffer(in _statesHandle);
            NativeArray<double3> aups = ResolveVaultBuffer(in _nodeAupsHandle);
            NativeArray<StructuralTuningDTO> tuningArray = ResolveVaultBuffer(in _tuningHandle);
            if (!states.IsCreated || !aups.IsCreated || !tuningArray.IsCreated || tuningArray.Length == 0)
                return;

            StructuralTuningDTO tuning = tuningArray[0];
            int count = math.min(_activeNodeCount, math.min(states.Length, aups.Length));
            count = math.min(count, 512);
            for (int i = 0; i < count; i++)
            {
                IntegrityStateDTO state = states[i];
                if (state.NodeHash == 0u)
                    continue;

                float stress = math.saturate(math.isfinite(state.CurrentStress) ? state.CurrentStress : 1f);
                Color color = Color.Lerp(Color.green, Color.yellow, math.saturate(stress / 0.8f));
                if (stress >= 0.95f)
                {
                    float pulse = Mathf.PingPong((float)SystemDispatcher.CurrentUnscaledTimeSeconds * 4f, 1f);
                    color = Color.Lerp(Color.red, Color.white, pulse * 0.35f);
                }

                if (!TryBuildEditorRelativePosition(aups[i], tuning.SeaLevelAup, out Vector3 position))
                    continue;

                float size = math.lerp(0.18f, 0.85f, stress);
                Gizmos.color = color;
                Gizmos.DrawWireCube(position, Vector3.one * size);
            }

            DrawBaseStructuralWarningGizmos(tuning.SeaLevelAup);
        }
#endif

        public void Tick(float deltaTime)
        {
            if (_initialized == 0)
                return;

            using (_tickMarker.Auto())
            {
                _frame = AdvanceSimulationFrame(_frame);
                if (_jobScheduled != 0)
                    return;

                float quality = ResolveSimulationQualityWeight();
                int framesBetweenUpdates = ResolveFramesBetweenUpdates(quality);
                if ((_frame % (uint)framesBetweenUpdates) != 0u)
                    return;

                ScheduleSolver(quality, framesBetweenUpdates);
            }
        }

        public void LateFrameTick()
        {
            if (_initialized == 0)
                return;

            using (_lateMarker.Auto())
            {
                if (_jobScheduled != 0)
                {
                    if (!CompleteScheduled(false, false))
                        return;

                    try { AfterSolverComplete(); }
                    finally { UnlockSolverBuffers(); }
                }
            }
        }

#if UNITY_EDITOR
        public void ColdTick()
        {
            if (_initialized == 0)
                return;

            if (_jobScheduled != 0)
                return;

            TryLoadMaterialStrengthCsv();
        }
#endif

        public bool TryGetState(int index, out IntegrityStateDTO state, out double3 aup)
        {
            state = default;
            aup = default;
            if (_initialized == 0 || _jobScheduled != 0 || (uint)index >= (uint)_activeNodeCount)
                return false;

            NativeArray<IntegrityStateDTO> states = ResolveVaultBuffer(in _statesHandle);
            NativeArray<double3> aups = ResolveVaultBuffer(in _nodeAupsHandle);
            if (!states.IsCreated || !aups.IsCreated || index >= states.Length || index >= aups.Length)
                return false;

            state = states[index];
            aup = aups[index];
            return state.NodeHash != 0u;
        }

        public bool TryGetTuning(out StructuralTuningDTO tuning)
        {
            tuning = default;
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            NativeArray<StructuralTuningDTO> tuningArray = ResolveVaultBuffer(in _tuningHandle);
            if (!tuningArray.IsCreated || tuningArray.Length == 0)
                return false;

            tuning = tuningArray[0];
            return true;
        }

        public bool TryGetTelemetrySample(int framesBack, out StructuralTelemetryEntry entry)
        {
            entry = default;
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            NativeArray<StructuralTelemetryEntry> telemetry = ResolveVaultBuffer(in _telemetryHandle);
            NativeArray<int> cursor = ResolveVaultBuffer(in _telemetryCursorHandle);
            if (!telemetry.IsCreated || !cursor.IsCreated || telemetry.Length == 0 || cursor.Length == 0)
                return false;

            int capacity = math.min(telemetry.Length, StructuralIntegrityConstants.TelemetryFrameCapacity);
            int clampedBack = math.clamp(framesBack, 0, capacity - 1);
            int cursorValue = cursor[0];
            if (cursorValue < 0)
                cursorValue = 0;
            cursorValue %= capacity;

            int slot = cursorValue - 1 - clampedBack;
            while (slot < 0)
                slot += capacity;
            slot %= capacity;

            entry = telemetry[slot];
            return entry.Sequence != 0u || entry.Frame != 0u || entry.ActiveNodeCount != 0;
        }

        public void SetTuning(in StructuralTuningDTO tuning)
        {
            if (_initialized == 0 || _jobScheduled != 0)
                return;

            if (!TryAcquireStructuralMutationGuard())
                return;

            try
            {
                NativeArray<StructuralTuningDTO> tuningArray = ResolveVaultBuffer(in _tuningHandle);
                if (!tuningArray.IsCreated || tuningArray.Length == 0)
                    return;

                StructuralTuningDTO sanitized = SanitizeTuning(tuning);
                tuningArray[0] = sanitized;
                simulationQualityWeight = sanitized.GlobalQualityWeight;
                basePressureKPa = sanitized.BasePressureKPa;
                pressureGradientKPaPerMeter = sanitized.PressureGradientKPaPerMeter;
                pressureToStressScale = sanitized.PressureToStressScale;
                materialStrengthFactor = sanitized.MaterialStrengthFactor;
                bucklingStart01 = sanitized.BucklingStart01;
                bucklingVisualIntensity = sanitized.BucklingVisualIntensity;
                supportDamping = sanitized.SupportDamping;
                collapseStress01 = sanitized.CollapseStress01;
                sdfMetersPerVoxel = sanitized.SdfMetersPerVoxel;
                sdfRangeMeters = sanitized.SdfRangeMeters;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }
        }

        public bool RegenerateMockGraph()
        {
            if (_initialized == 0 || _jobScheduled != 0)
                return false;

            return GenerateEmergencyMockStressData();
        }

        public bool GenerateMockStructuralStress()
        {
            return RegenerateMockGraph();
        }

        private bool TryInitialize()
        {
            if (_dataVault == null)
                return false;

            if (!StructuralIntegrityLayout.Validate())
                return false;

            _glassHash = HashLowerAsciiLiteral("glass");
            _titaniumHash = HashLowerAsciiLiteral("titanium");
            _plasteelHash = HashLowerAsciiLiteral("plasteel");

            _statesHandle = _dataVault.EnsureGenerationHandle<IntegrityStateDTO>(
                BufferID.StructuralIntegrityStates,
                StructuralIntegrityConstants.MaxNodeCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _nodeAupsHandle = _dataVault.EnsureGenerationHandle<double3>(
                BufferID.StructuralIntegrityNodeAups,
                StructuralIntegrityConstants.MaxNodeCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _csrOffsetsHandle = _dataVault.EnsureGenerationHandle<int>(
                BufferID.StructuralIntegrityCsrOffsets,
                StructuralIntegrityConstants.MaxNodeCapacity + 1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _csrDestinationsHandle = _dataVault.EnsureGenerationHandle<int>(
                BufferID.StructuralIntegrityCsrDestinations,
                StructuralIntegrityConstants.MaxEdgeCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _edgeFlagsHandle = _dataVault.EnsureGenerationHandle<byte>(
                BufferID.StructuralIntegrityEdgeFlags,
                StructuralIntegrityConstants.MaxEdgeCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _telemetryHandle = _dataVault.EnsureGenerationHandle<StructuralTelemetryEntry>(
                BufferID.StructuralIntegrityTelemetryRing,
                StructuralIntegrityConstants.TelemetryFrameCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _telemetryCursorHandle = _dataVault.EnsureGenerationHandle<int>(
                BufferID.StructuralIntegrityTelemetryCursor,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _tuningHandle = _dataVault.EnsureGenerationHandle<StructuralTuningDTO>(
                BufferID.StructuralIntegrityTuning,
                1,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _materialsHandle = _dataVault.EnsureGenerationHandle<StructuralMaterialStrengthEntry>(
                BufferID.StructuralIntegrityMaterialStrengths,
                StructuralIntegrityConstants.MaterialStrengthCapacity,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            _csvScratchHandle = _dataVault.EnsureGenerationHandle<byte>(
                BufferID.StructuralIntegrityCsvScratch,
                StructuralIntegrityConstants.CsvScratchBytes,
                SystemID.HullIntegrity,
                NativeArrayOptions.UninitializedMemory);
            EnsureBaseStructuralWarningHandles();

            if (!HasRequiredVaultBuffers())
            {
                return FailInitialize();
            }

            SignalBus<BaseIntegrityEventPayload>.Configure(64, 256, 32, StructuralIntegrityConstants.SignalLaneHash);
            SignalBus<BaseIntegrityEventPayload>.EnsureInitialized();
            SignalBus<FluidIncursionSignal>.Configure(
                FluidIncursionSignal.ExpectedCapacity,
                FluidIncursionSignal.MaxFrameSignals,
                FluidIncursionSignal.LowTierFrameSignals,
                FluidIncursionSignal.LaneHash);
            SignalBus<FluidIncursionSignal>.EnsureInitialized();
            SignalBus<BaseModuleCompromisedSignal>.Configure(
                BaseModuleCompromisedSignal.ExpectedCapacity,
                BaseModuleCompromisedSignal.MaxFrameSignals,
                BaseModuleCompromisedSignal.LowTierFrameSignals,
                BaseModuleCompromisedSignal.LaneHash);
            SignalBus<BaseModuleCompromisedSignal>.EnsureInitialized();
            SignalBus<BaseStructuralWarningSignal>.Configure(
                BaseStructuralWarningConstants.SignalCapacity,
                BaseStructuralWarningConstants.MaxFrameSignals,
                BaseStructuralWarningConstants.LowTierFrameSignals,
                BaseStructuralWarningConstants.SignalLaneHash);
            SignalBus<BaseStructuralWarningSignal>.EnsureInitialized();

            if (!ClearBootBuffers())
                return FailInitialize();
            if (!ClearBaseStructuralWarningBootBuffers())
                return FailInitialize();
            if (!WriteDefaultMaterials())
                return FailInitialize();
#if UNITY_EDITOR
            TryLoadMaterialStrengthCsv();
#endif
            _activeNodeCount = math.clamp(mockNodeCount, 1, StructuralIntegrityConstants.MaxNodeCapacity);
            if (!WriteDefaultTuning())
                return FailInitialize();
            if (!WriteDefaultBaseStructuralWarningTuning())
                return FailInitialize();
            if (generateMockGraphOnEnable)
            {
                if (!GenerateEmergencyMockStressData())
                    return FailInitialize();
            }

            EnsureGpuBuffers();
            _initialized = 1;
            return true;
        }

        private void CacheRegistryServicesCold()
        {
            _dataVault = GlobalRegistry.DataVault;
        }

        /// <inheritdoc />
        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            RebindDataVault(currentService as IDataVault);
        }

        private void RebindDataVault(IDataVault dataVault)
        {
            if (ReferenceEquals(_dataVault, dataVault))
                return;

            CompleteScheduled(true, true);
            ReleaseGpuBuffers();
            ReleaseVaultHandles();
            _initialized = 0;
            _dataVault = dataVault;

            if (!isActiveAndEnabled || dataVault == null)
                return;

            if (TryInitialize())
            {
                s_activeRuntime = this;
                TryRegisterTickables();
            }
            else if (s_activeRuntime == this)
            {
                s_activeRuntime = null;
            }
        }

        private bool FailInitialize()
        {
            ReleaseGpuBuffers();
            ReleaseVaultHandles();
            _initialized = 0;
            return false;
        }

        private bool HasRequiredVaultBuffers()
        {
            return TryResolveVaultBuffer(in _statesHandle, out NativeArray<IntegrityStateDTO> states) &&
                   states.Length >= StructuralIntegrityConstants.MaxNodeCapacity &&
                   TryResolveVaultBuffer(in _nodeAupsHandle, out NativeArray<double3> aups) &&
                   aups.Length >= StructuralIntegrityConstants.MaxNodeCapacity &&
                   TryResolveVaultBuffer(in _csrOffsetsHandle, out NativeArray<int> offsets) &&
                   offsets.Length >= StructuralIntegrityConstants.MaxNodeCapacity + 1 &&
                   TryResolveVaultBuffer(in _csrDestinationsHandle, out NativeArray<int> destinations) &&
                   destinations.Length >= StructuralIntegrityConstants.MaxEdgeCapacity &&
                   TryResolveVaultBuffer(in _edgeFlagsHandle, out NativeArray<byte> edgeFlags) &&
                   edgeFlags.Length >= StructuralIntegrityConstants.MaxEdgeCapacity &&
                   TryResolveVaultBuffer(in _telemetryHandle, out NativeArray<StructuralTelemetryEntry> telemetry) &&
                   telemetry.Length >= StructuralIntegrityConstants.TelemetryFrameCapacity &&
                   TryResolveVaultBuffer(in _telemetryCursorHandle, out NativeArray<int> telemetryCursor) &&
                   telemetryCursor.Length >= 1 &&
                   TryResolveVaultBuffer(in _tuningHandle, out NativeArray<StructuralTuningDTO> tuning) &&
                   tuning.Length >= 1 &&
                   TryResolveVaultBuffer(in _materialsHandle, out NativeArray<StructuralMaterialStrengthEntry> materials) &&
                   materials.Length >= StructuralIntegrityConstants.MaterialStrengthCapacity &&
                   TryResolveVaultBuffer(in _csvScratchHandle, out NativeArray<byte> csvScratch) &&
                   csvScratch.Length >= StructuralIntegrityConstants.CsvScratchBytes &&
                   HasRequiredBaseStructuralWarningBuffers();
        }

        private NativeArray<T> ResolveVaultBuffer<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return TryResolveVaultBuffer(in handle, out NativeArray<T> buffer) ? buffer : default;
        }

        private bool TryResolveVaultBuffer<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            if (vault == null || !IsHullIntegrityVaultHandle(in handle))
                return false;

            if (vault.TryResolveHandle(in handle, out buffer) && buffer.IsCreated)
                return true;

            buffer = default;
            return false;
        }

        private bool TryReadBorrowedVaultBuffer<T>(BufferID bufferId, out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            IDataVault vault = _dataVault;
            return vault != null &&
                   vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) &&
                   handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.Generation != 0u &&
                   vault.TryReadHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault != null)
            {
                ReleaseVaultHandle(vault, ref _statesHandle);
                ReleaseVaultHandle(vault, ref _nodeAupsHandle);
                ReleaseVaultHandle(vault, ref _csrOffsetsHandle);
                ReleaseVaultHandle(vault, ref _csrDestinationsHandle);
                ReleaseVaultHandle(vault, ref _edgeFlagsHandle);
                ReleaseVaultHandle(vault, ref _telemetryHandle);
                ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
                ReleaseVaultHandle(vault, ref _tuningHandle);
                ReleaseVaultHandle(vault, ref _materialsHandle);
                ReleaseVaultHandle(vault, ref _csvScratchHandle);
                ReleaseBaseStructuralWarningVaultHandles(vault);
            }

            _statesHandle = default;
            _nodeAupsHandle = default;
            _csrOffsetsHandle = default;
            _csrDestinationsHandle = default;
            _edgeFlagsHandle = default;
            _telemetryHandle = default;
            _telemetryCursorHandle = default;
            _tuningHandle = default;
            _materialsHandle = default;
            _csvScratchHandle = default;
            ClearBaseStructuralWarningHandleState();
            _dataVault = null;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle)
            where T : struct
        {
            if (handle.BufferID != 0u)
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private bool ClearBootBuffers()
        {
            if (!TryPinSolverBuffers(false))
                return false;

            bool cleared = false;
            try
            {
                NativeArray<IntegrityStateDTO> states = ResolveVaultBuffer(in _statesHandle);
                NativeArray<double3> aups = ResolveVaultBuffer(in _nodeAupsHandle);
                NativeArray<int> offsets = ResolveVaultBuffer(in _csrOffsetsHandle);
                NativeArray<int> destinations = ResolveVaultBuffer(in _csrDestinationsHandle);
                NativeArray<byte> flags = ResolveVaultBuffer(in _edgeFlagsHandle);
                NativeArray<StructuralTelemetryEntry> telemetry = ResolveVaultBuffer(in _telemetryHandle);
                NativeArray<int> telemetryCursor = ResolveVaultBuffer(in _telemetryCursorHandle);
                // COLD SYNC JOB: boot-time explicit memclear of Vault buffers acquired with UninitializedMemory.
                JobHandle clearHandle = new StructuralIntegrityClearJob
                {
                    States = states,
                    NodeAups = aups,
                    CsrOffsets = offsets,
                    CsrDestinations = destinations,
                    EdgeFlags = flags,
                    Telemetry = telemetry,
                    TelemetryCursor = telemetryCursor
                }.Schedule();
                H8Memory.RegisterActiveJob(SystemID.HullIntegrity, clearHandle);
                DispatcherJobFence.TryComplete(ref clearHandle, forceComplete: true);
                cleared = true;
            }
            finally
            {
                UnlockSolverBuffers();
            }

            return cleared;
        }

        private bool WriteDefaultTuning()
        {
            if (!TryAcquireStructuralMutationGuard())
                return false;

            bool written = false;
            try
            {
                NativeArray<StructuralTuningDTO> tuning = ResolveVaultBuffer(in _tuningHandle);
                if (!tuning.IsCreated || tuning.Length == 0)
                    return false;

                WriteDefaultTuning(tuning);
                written = true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }

            return written;
        }

        private void WriteDefaultTuning(NativeArray<StructuralTuningDTO> tuning)
        {
            StructuralTuningDTO sanitized = SanitizeTuning(new StructuralTuningDTO
            {
                SeaLevelAup = new double3(seaLevelAup.x, seaLevelAup.y, seaLevelAup.z),
                SdfOriginAup = new double3(sdfOriginAup.x, sdfOriginAup.y, sdfOriginAup.z),
                BasePressureKPa = basePressureKPa,
                PressureGradientKPaPerMeter = pressureGradientKPaPerMeter,
                PressureToStressScale = pressureToStressScale,
                MaterialStrengthFactor = materialStrengthFactor,
                BucklingStart01 = bucklingStart01,
                BucklingVisualIntensity = bucklingVisualIntensity,
                SupportDamping = supportDamping,
                CollapseStress01 = collapseStress01,
                GlobalQualityWeight = simulationQualityWeight,
                SdfMetersPerVoxel = sdfMetersPerVoxel,
                SdfRangeMeters = sdfRangeMeters,
                ActiveNodeCount = _activeNodeCount
            });
            tuning[0] = sanitized;
            simulationQualityWeight = sanitized.GlobalQualityWeight;
        }

        private bool GenerateEmergencyMockStressData()
        {
            if (!TryPinSolverBuffers(false))
                return false;

            bool generated = false;
            try
            {
                NativeArray<IntegrityStateDTO> states = ResolveVaultBuffer(in _statesHandle);
                NativeArray<double3> aups = ResolveVaultBuffer(in _nodeAupsHandle);
                NativeArray<int> offsets = ResolveVaultBuffer(in _csrOffsetsHandle);
                NativeArray<int> destinations = ResolveVaultBuffer(in _csrDestinationsHandle);
                NativeArray<byte> flags = ResolveVaultBuffer(in _edgeFlagsHandle);
                NativeArray<StructuralMaterialStrengthEntry> materials = ResolveVaultBuffer(in _materialsHandle);
                NativeArray<StructuralTuningDTO> tuning = ResolveVaultBuffer(in _tuningHandle);
                if (!states.IsCreated || !aups.IsCreated || !offsets.IsCreated || !destinations.IsCreated || !flags.IsCreated || !materials.IsCreated ||
                    !tuning.IsCreated || tuning.Length == 0)
                {
                    return false;
                }

                _activeNodeCount = math.clamp(mockNodeCount, 1, math.min(StructuralIntegrityConstants.MaxNodeCapacity, states.Length));
                // COLD SYNC JOB: deterministic mock topology generation for isolated profiling and CI fallback data.
                JobHandle mockHandle = new GenerateMockStructuralStressJob
                {
                    States = states,
                    NodeAups = aups,
                    CsrOffsets = offsets,
                    CsrDestinations = destinations,
                    EdgeFlags = flags,
                    Materials = materials,
                    NodeCount = _activeNodeCount,
                    BaseHash = StructuralIntegrityConstants.DefaultBaseHash,
                    SeaLevelAup = new double3(seaLevelAup.x, seaLevelAup.y, seaLevelAup.z),
                    GlassHash = _glassHash,
                    TitaniumHash = _titaniumHash,
                    PlasteelHash = _plasteelHash
                }.Schedule();
                H8Memory.RegisterActiveJob(SystemID.HullIntegrity, mockHandle);
                // COLD SYNC JOB: deterministic mock topology generation for isolated profiling and CI fallback data.
                DispatcherJobFence.TryComplete(ref mockHandle, forceComplete: true);

                _activeEdgeCount = offsets.IsCreated && _activeNodeCount < offsets.Length ? offsets[_activeNodeCount] : 0;
                WriteDefaultTuning(tuning);
                generated = true;
            }
            finally
            {
                UnlockSolverBuffers();
            }

            return generated;
        }

        private void ScheduleSolver(float quality, int framesBetweenUpdates)
        {
            NativeArray<byte> sdf = default;
            bool includeSdfLock = TryReadBorrowedVaultBuffer(BufferID.VoxelSdfTexture3D, out sdf);
            if (!TryPinSolverBuffers(includeSdfLock))
                return;

            NativeArray<IntegrityStateDTO> states = ResolveVaultBuffer(in _statesHandle);
            NativeArray<double3> aups = ResolveVaultBuffer(in _nodeAupsHandle);
            NativeArray<int> offsets = ResolveVaultBuffer(in _csrOffsetsHandle);
            NativeArray<int> destinations = ResolveVaultBuffer(in _csrDestinationsHandle);
            NativeArray<byte> edgeFlags = ResolveVaultBuffer(in _edgeFlagsHandle);
            NativeArray<StructuralTelemetryEntry> telemetry = ResolveVaultBuffer(in _telemetryHandle);
            NativeArray<int> telemetryCursor = ResolveVaultBuffer(in _telemetryCursorHandle);
            NativeArray<StructuralTuningDTO> tuning = ResolveVaultBuffer(in _tuningHandle);
            if (!states.IsCreated || !aups.IsCreated || !offsets.IsCreated || !destinations.IsCreated || !edgeFlags.IsCreated ||
                !telemetry.IsCreated || !telemetryCursor.IsCreated || !tuning.IsCreated || telemetryCursor.Length == 0 || tuning.Length == 0)
            {
                UnlockSolverBuffers();
                return;
            }

            StructuralTuningDTO current = SanitizeTuning(tuning[0]);
            current.GlobalQualityWeight = math.saturate(math.isfinite(quality) ? quality : 1f);
            current.ActiveNodeCount = _activeNodeCount;
            tuning[0] = current;

            if (!includeSdfLock || !TryReadBorrowedVaultBuffer(BufferID.VoxelSdfTexture3D, out sdf))
                sdf = default;
            int sdfDimension = ResolveSdfDimension(sdf);
            int sdfFallback = sdfDimension <= 1 ? 1 : 0;
            int maxNodeCount = math.min(math.min(states.Length, aups.Length), math.max(0, offsets.Length - 1));
            int safeCount = math.clamp(_activeNodeCount, 0, maxNodeCount);
            int batchSize = ResolveBatchSize(quality);
            float estimatedMicroseconds = EstimateMicroseconds(safeCount, _activeEdgeCount, framesBetweenUpdates);

            JobHandle handle = new StructuralDepthPressureJob
            {
                States = states,
                NodeAups = aups,
                Tuning = tuning,
                ActiveNodeCount = safeCount
            }.Schedule(safeCount, batchSize);

            handle = new StructuralSdfAnchorJob
            {
                States = states,
                NodeAups = aups,
                VoxelSdfTexture3D = sdf,
                Tuning = tuning,
                ActiveNodeCount = safeCount,
                SdfDimension = sdfDimension
            }.Schedule(safeCount, batchSize, handle);

            handle = new StructuralGraphStressJob
            {
                States = states,
                CsrOffsets = offsets,
                CsrDestinations = destinations,
                EdgeFlags = edgeFlags,
                Tuning = tuning,
                ActiveNodeCount = safeCount
            }.Schedule(safeCount, batchSize, handle);

            handle = new StructuralCollapseSignalJob
            {
                States = states,
                NodeAups = aups,
                Tuning = tuning,
                IntegrityEvents = SignalBus<BaseIntegrityEventPayload>.ParallelWriter,
                IntegrityEventsBudget = SignalBus<BaseIntegrityEventPayload>.ParallelWriterBudget,
                FluidEvents = SignalBus<FluidIncursionSignal>.ParallelWriter,
                FluidEventsBudget = SignalBus<FluidIncursionSignal>.ParallelWriterBudget,
                CompromisedEvents = SignalBus<BaseModuleCompromisedSignal>.ParallelWriter,
                CompromisedEventsBudget = SignalBus<BaseModuleCompromisedSignal>.ParallelWriterBudget,
                ActiveNodeCount = safeCount,
                Frame = _frame
            }.Schedule(handle);

            handle = ScheduleBaseStructuralWarningDispatcher(
                states,
                aups,
                tuning,
                safeCount,
                quality,
                framesBetweenUpdates,
                handle);

            handle = new StructuralEdgeSeverJob
            {
                States = states,
                CsrOffsets = offsets,
                CsrDestinations = destinations,
                EdgeFlags = edgeFlags,
                ActiveNodeCount = safeCount
            }.Schedule(safeCount, batchSize, handle);

            handle = new StructuralTelemetryJob
            {
                States = states,
                CsrOffsets = offsets,
                Tuning = tuning,
                Telemetry = telemetry,
                TelemetryCursor = telemetryCursor,
                ActiveNodeCount = safeCount,
                Frame = _frame,
                FramesBetweenUpdates = framesBetweenUpdates,
                EstimatedMicroseconds = estimatedMicroseconds,
                BaseHash = StructuralIntegrityConstants.DefaultBaseHash,
                SdfFallback = sdfFallback
            }.Schedule(handle);

            _scheduledHandle = handle;
            H8Memory.RegisterActiveJob(SystemID.HullIntegrity, handle);
            _jobScheduled = 1;
        }

        private bool CompleteScheduled(bool releaseLocks, bool forceComplete)
        {
            if (_jobScheduled == 0)
                return true;

            if (!forceComplete && !_scheduledHandle.IsCompleted)
                return false;

            try
            {
                Hecton8.Core.DispatcherJobFence.TryComplete(ref _scheduledHandle, forceComplete);
            }
            finally
            {
                _jobScheduled = 0;
                if (releaseLocks)
                    UnlockSolverBuffers();
            }

            return true;
        }

        private bool TryPinSolverBuffers(bool includeSdf)
        {
            if (_solverLockMask != 0 || _solverMutationGuardMask != 0UL || _dataVault == null)
                return false;

            IDataVault vault = _dataVault;
            ulong guardMask = includeSdf ? StructuralSolverSdfMutationGuardMask : StructuralMutationGuardMask;
            if (!vault.TryAcquireMutationGuard(guardMask))
                return false;

            _solverGuardVault = vault;
            _solverMutationGuardMask = guardMask;
            int mask = 0;
            if (!TryMarkSolverBuffer(in _statesHandle, BufferID.StructuralIntegrityStates, SolverLockStates, StructuralIntegrityConstants.MaxNodeCapacity, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _nodeAupsHandle, BufferID.StructuralIntegrityNodeAups, SolverLockNodeAups, StructuralIntegrityConstants.MaxNodeCapacity, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _csrOffsetsHandle, BufferID.StructuralIntegrityCsrOffsets, SolverLockOffsets, StructuralIntegrityConstants.MaxNodeCapacity + 1, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _csrDestinationsHandle, BufferID.StructuralIntegrityCsrDestinations, SolverLockDestinations, StructuralIntegrityConstants.MaxEdgeCapacity, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _edgeFlagsHandle, BufferID.StructuralIntegrityEdgeFlags, SolverLockEdgeFlags, StructuralIntegrityConstants.MaxEdgeCapacity, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _telemetryHandle, BufferID.StructuralIntegrityTelemetryRing, SolverLockTelemetry, StructuralIntegrityConstants.TelemetryFrameCapacity, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _telemetryCursorHandle, BufferID.StructuralIntegrityTelemetryCursor, SolverLockTelemetryCursor, 1, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _tuningHandle, BufferID.StructuralIntegrityTuning, SolverLockTuning, 1, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _materialsHandle, BufferID.StructuralIntegrityMaterialStrengths, SolverLockMaterials, StructuralIntegrityConstants.MaterialStrengthCapacity, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (!TryMarkSolverBuffer(in _csvScratchHandle, BufferID.StructuralIntegrityCsvScratch, SolverLockCsvScratch, StructuralIntegrityConstants.CsvScratchBytes, ref mask)) { UnlockSolverBuffers(mask); return false; }
            if (includeSdf)
                mask |= SolverLockSdf;
            if (!TryMarkBaseStructuralWarningBuffers(ref mask)) { UnlockSolverBuffers(mask); return false; }

            _solverLockMask = mask;
            return true;
        }

        private bool TryMarkSolverBuffer<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int bit,
            int requiredLength,
            ref int mask)
            where T : struct
        {
            if (requiredLength <= 0 ||
                !IsHullIntegrityVaultHandle(in handle, bufferId) ||
                !TryResolveVaultBuffer(in handle, out NativeArray<T> buffer) ||
                buffer.Length < requiredLength)
            {
                return false;
            }

            mask |= bit;
            return true;
        }

        private bool TryAcquireStructuralMutationGuard()
        {
            IDataVault vault = _dataVault;
            if (vault == null || !vault.TryAcquireMutationGuard(StructuralMutationGuardMask))
                return false;

            _structuralMutationGuardVault = vault;
            return true;
        }

        private void ReleaseStructuralMutationGuard()
        {
            IDataVault vault = _structuralMutationGuardVault;
            _structuralMutationGuardVault = null;
            vault?.ReleaseMutationGuard(StructuralMutationGuardMask);
        }

        private void UnlockSolverBuffers()
        {
            int mask = _solverLockMask;
            _solverLockMask = 0;
            UnlockSolverBuffers(mask);
        }

        private void UnlockSolverBuffers(int mask)
        {
            ulong guardMask = _solverMutationGuardMask;
            if (mask == 0 && guardMask == 0UL)
                return;

            IDataVault guardVault = _solverGuardVault;
            _solverMutationGuardMask = 0UL;
            _solverGuardVault = null;
            if (guardMask != 0UL)
                guardVault?.ReleaseMutationGuard(guardMask);
        }

        private void AfterSolverComplete()
        {
            NativeArray<IntegrityStateDTO> states = ResolveVaultBuffer(in _statesHandle);
            NativeArray<StructuralTelemetryEntry> telemetry = ResolveVaultBuffer(in _telemetryHandle);
            NativeArray<int> telemetryCursor = ResolveVaultBuffer(in _telemetryCursorHandle);

            StructuralTelemetryEntry entry = default;
            bool hasTelemetryEntry = false;
            if (!telemetry.IsCreated || !telemetryCursor.IsCreated || telemetryCursor.Length == 0)
            {
                if (states.IsCreated && uploadStateBufferToShaders)
                    UploadStatesToGpu(states, 0u, false);
                return;
            }

            int capacity = math.min(telemetry.Length, StructuralIntegrityConstants.TelemetryFrameCapacity);
            if (capacity <= 0)
            {
                if (states.IsCreated && uploadStateBufferToShaders)
                    UploadStatesToGpu(states, 0u, false);
                return;
            }

            int cursorValue = telemetryCursor[0];
            if (cursorValue < 0)
                cursorValue = 0;
            cursorValue %= capacity;

            int slot = cursorValue - 1;
            if (slot < 0)
                slot += capacity;

            entry = telemetry[slot];
            hasTelemetryEntry = true;
            if (states.IsCreated && uploadStateBufferToShaders)
                UploadStatesToGpu(states, entry.StateHash, hasTelemetryEntry);

            if ((entry.FaultFlags & (StructuralIntegrityConstants.TelemetryFlagNonFinite | StructuralIntegrityConstants.TelemetryFlagMassCollapse)) != 0u &&
                entry.Frame != _lastDumpedFrame)
            {
                DumpTelemetry(DumpRelativePath, in entry);
                DumpTelemetry(SurgeonDumpRelativePath, in entry);
                _lastDumpedFrame = entry.Frame;
            }

            AfterBaseStructuralWarningComplete();
        }

        private void UploadStatesToGpu(NativeArray<IntegrityStateDTO> states, uint stateHash, bool canUseStateHash)
        {
            EnsureGpuBuffers();
            if (_stateBufferA == null || _stateBufferB == null || !states.IsCreated)
                return;

            int uploadCount = math.clamp(_activeNodeCount, 0, states.Length);
            if (uploadCount <= 0)
            {
                GraphicsBuffer currentReadBuffer = _gpuReadIndex == 0 ? _stateBufferA : _stateBufferB;
                Shader.SetGlobalBuffer(_stateBufferId, currentReadBuffer);
                Shader.SetGlobalVector(_stateParamsId, new Vector4(0f, _activeEdgeCount, ResolveVisualQualityWeight(), _frame));
                if (canUseStateHash)
                {
                    _lastUploadedStateHash = stateHash;
                    _lastUploadedNodeCount = 0;
                    _gpuUploadValid = 1;
                }
                else
                {
                    _lastUploadedStateHash = 0u;
                    _lastUploadedNodeCount = 0;
                    _gpuUploadValid = 0;
                }

                return;
            }

            if (canUseStateHash &&
                _gpuUploadValid != 0 &&
                _lastUploadedStateHash == stateHash &&
                _lastUploadedNodeCount == uploadCount)
            {
                GraphicsBuffer currentReadBuffer = _gpuReadIndex == 0 ? _stateBufferA : _stateBufferB;
                Shader.SetGlobalBuffer(_stateBufferId, currentReadBuffer);
                Shader.SetGlobalVector(_stateParamsId, new Vector4(uploadCount, _activeEdgeCount, ResolveVisualQualityWeight(), _frame));
                return;
            }

            int writeIndex = 1 - _gpuReadIndex;
            GraphicsBuffer writeBuffer = writeIndex == 0 ? _stateBufferA : _stateBufferB;
            NativeArray<IntegrityStateDTO> mapped = writeBuffer.LockBufferForWrite<IntegrityStateDTO>(0, uploadCount);
            try
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(states);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<IntegrityStateDTO>() * uploadCount);
            }
            finally
            {
                writeBuffer.UnlockBufferAfterWrite<IntegrityStateDTO>(uploadCount);
            }

            _gpuReadIndex = writeIndex;
            if (canUseStateHash)
            {
                _lastUploadedStateHash = stateHash;
                _lastUploadedNodeCount = uploadCount;
                _gpuUploadValid = 1;
            }
            else
            {
                _lastUploadedStateHash = 0u;
                _lastUploadedNodeCount = 0;
                _gpuUploadValid = 0;
            }

            GraphicsBuffer readBuffer = _gpuReadIndex == 0 ? _stateBufferA : _stateBufferB;
            Shader.SetGlobalBuffer(_stateBufferId, readBuffer);
            Shader.SetGlobalVector(_stateParamsId, new Vector4(uploadCount, _activeEdgeCount, ResolveVisualQualityWeight(), _frame));
        }

        private void EnsureGpuBuffers()
        {
            if (!uploadStateBufferToShaders)
                return;

            int stride = UnsafeUtility.SizeOf<IntegrityStateDTO>();
            if (_stateBufferA == null || _stateBufferA.count != StructuralIntegrityConstants.MaxNodeCapacity || _stateBufferA.stride != stride)
            {
                ReleaseGpuBuffers();
                _stateBufferA = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, StructuralIntegrityConstants.MaxNodeCapacity, stride); // COLD ALLOC: GraphicsBuffer[4096] - structural state upload A - owner: SHINOBU_218
                _stateBufferB = new GraphicsBuffer(GraphicsBuffer.Target.Structured, GraphicsBuffer.UsageFlags.LockBufferForWrite, StructuralIntegrityConstants.MaxNodeCapacity, stride); // COLD ALLOC: GraphicsBuffer[4096] - structural state upload B - owner: SHINOBU_218
                _gpuReadIndex = 0;
                _gpuUploadValid = 0;
            }
        }

        private void ReleaseGpuBuffers()
        {
            if (_stateBufferA != null)
            {
                _stateBufferA.Release();
                _stateBufferA = null;
            }

            if (_stateBufferB != null)
            {
                _stateBufferB.Release();
                _stateBufferB = null;
            }

            _lastUploadedStateHash = 0u;
            _lastUploadedNodeCount = 0;
            _gpuUploadValid = 0;
        }

        private void TryRegisterTickables()
        {
            if (_registeredUpdate == 0 && GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment))
                _registeredUpdate = 1;
            if (_registeredLate == 0 && GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment))
                _registeredLate = 1;
#if UNITY_EDITOR
            if (_registeredCold == 0 && GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment))
                _registeredCold = 1;
#endif
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap != 0 || !Application.isPlaying)
                return;

            if (GlobalRegistry.TryRegisterHotSwapListener(this))
                _registeredHotSwap = 1;
        }

        private void TryUnregisterTickables()
        {
            if (_registeredUpdate != 0)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredUpdate = 0;
            }

            if (_registeredLate != 0)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLate = 0;
            }

#if UNITY_EDITOR
            if (_registeredCold != 0)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredCold = 0;
            }
#endif
        }

        private void TryUnregisterHotSwapListener()
        {
            if (_registeredHotSwap == 0)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = 0;
        }

        private bool WriteDefaultMaterials()
        {
            if (!TryAcquireStructuralMutationGuard())
                return false;

            bool written = false;
            try
            {
                NativeArray<StructuralMaterialStrengthEntry> materials = ResolveVaultBuffer(in _materialsHandle);
                if (!materials.IsCreated)
                    return false;

                WriteDefaultMaterials(materials);
                written = true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }

            return written;
        }

        private void WriteDefaultMaterials(NativeArray<StructuralMaterialStrengthEntry> materials)
        {
            for (int i = 0; i < materials.Length; i++)
                materials[i] = default;

            UpsertMaterial(materials, _glassHash, 420f, 0.55f, 1.15f);
            UpsertMaterial(materials, _titaniumHash, 1220f, 0.72f, 1f);
            UpsertMaterial(materials, _plasteelHash, 2100f, 0.82f, 0.85f);
            _materialTableInitialized = 1;
        }

#if UNITY_EDITOR
        private bool TryLoadMaterialStrengthCsv()
        {
            string path = ResolveProjectPath(string.IsNullOrEmpty(materialStrengthCsvRelativePath) ? DefaultCsvRelativePath : materialStrengthCsvRelativePath);
            if (!File.Exists(path))
            {
                if (_materialTableInitialized == 0)
                    return WriteDefaultMaterials();
                return false;
            }

            long ticks = File.GetLastWriteTimeUtc(path).Ticks;
            if (_materialTableInitialized != 0 && ticks == _lastCsvWriteTicks)
                return true;

            if (!TryAcquireStructuralMutationGuard())
                return false;

            bool loaded = false;
            try
            {
                NativeArray<byte> scratch = ResolveVaultBuffer(in _csvScratchHandle);
                NativeArray<StructuralMaterialStrengthEntry> materials = ResolveVaultBuffer(in _materialsHandle);
                if (!scratch.IsCreated || !materials.IsCreated)
                    return false;

                int bytesRead;
                byte* scratchPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(scratch);
                try
                {
                    using (FileStream stream = new FileStream(
                        path,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.ReadWrite,
                        StructuralIntegrityConstants.CsvScratchBytes,
                        FileOptions.SequentialScan))
                    {
                        long length = stream.Length;
                        if (length <= 0L || length > scratch.Length)
                            return false;

                        Span<byte> destination = new Span<byte>(scratchPtr, (int)length);
                        int totalRead = 0;
                        while (totalRead < destination.Length)
                        {
                            int read = stream.Read(destination.Slice(totalRead));
                            if (read <= 0)
                                return false;

                            totalRead += read;
                        }

                        bytesRead = totalRead;
                    }
                }
                catch (IOException)
                {
                    return false;
                }
                catch (UnauthorizedAccessException)
                {
                    return false;
                }

                if (bytesRead <= 0)
                    return false;

                WriteDefaultMaterials(materials);
                ParseMaterialCsv(new ReadOnlySpan<byte>(scratchPtr, bytesRead), materials);
                _lastCsvWriteTicks = ticks;
                _materialTableInitialized = 1;
                loaded = true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }

            if (loaded)
                return ApplyMaterialsToStatesCold();

            return loaded;
        }

        private void ParseMaterialCsv(ReadOnlySpan<byte> bytes, NativeArray<StructuralMaterialStrengthEntry> materials)
        {
            int lineStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                ReadOnlySpan<byte> line = TrimAscii(bytes.Slice(lineStart, i - lineStart));
                lineStart = i + 1;
                if (line.Length == 0 || line[0] == (byte)'#')
                    continue;

                ParseMaterialCsvLine(line, materials);
            }
        }

        private void ParseMaterialCsvLine(ReadOnlySpan<byte> line, NativeArray<StructuralMaterialStrengthEntry> materials)
        {
            int comma0 = IndexOf(line, (byte)',');
            if (comma0 <= 0)
                return;
            int comma1 = IndexOf(line.Slice(comma0 + 1), (byte)',');
            if (comma1 < 0)
                return;
            comma1 += comma0 + 1;
            int comma2 = IndexOf(line.Slice(comma1 + 1), (byte)',');
            if (comma2 >= 0)
                comma2 += comma1 + 1;

            uint materialHash = HashLowerAscii(TrimAscii(line.Slice(0, comma0)));
            if (materialHash == 0u)
                return;

            if (!TryParseAsciiFloat(TrimAscii(line.Slice(comma0 + 1, comma1 - comma0 - 1)), out float baseStrength))
                return;

            float buckling = 0.72f;
            float pressureScale = 1f;
            if (comma2 >= 0)
            {
                TryParseAsciiFloat(TrimAscii(line.Slice(comma1 + 1, comma2 - comma1 - 1)), out buckling);
                TryParseAsciiFloat(TrimAscii(line.Slice(comma2 + 1)), out pressureScale);
            }
            else
            {
                TryParseAsciiFloat(TrimAscii(line.Slice(comma1 + 1)), out buckling);
            }

            UpsertMaterial(materials, materialHash, baseStrength, buckling, pressureScale);
        }

        private bool ApplyMaterialsToStatesCold()
        {
            if (_activeNodeCount <= 0)
                return true;

            if (!TryAcquireStructuralMutationGuard())
                return false;

            bool applied = false;
            try
            {
                NativeArray<IntegrityStateDTO> states = ResolveVaultBuffer(in _statesHandle);
                NativeArray<StructuralMaterialStrengthEntry> materials = ResolveVaultBuffer(in _materialsHandle);
                if (!states.IsCreated || !materials.IsCreated)
                    return false;

                // COLD SYNC JOB: material CSV reload is skipped while the solver fence is alive.
                JobHandle materialHandle = new StructuralMaterialStrengthApplyJob
                {
                    States = states,
                    Materials = materials,
                    ActiveNodeCount = _activeNodeCount,
                    GlassHash = _glassHash,
                    TitaniumHash = _titaniumHash,
                    PlasteelHash = _plasteelHash
                }.Schedule(_activeNodeCount, 64);
                H8Memory.RegisterActiveJob(SystemID.HullIntegrity, materialHandle);
                DispatcherJobFence.TryComplete(ref materialHandle, forceComplete: true);
                applied = true;
            }
            finally
            {
                ReleaseStructuralMutationGuard();
            }

            return applied;
        }
#endif

        private static void UpsertMaterial(NativeArray<StructuralMaterialStrengthEntry> materials, uint hash, float baseStrength, float bucklingStart, float pressureScale)
        {
            if (!materials.IsCreated || materials.Length == 0 || hash == 0u)
                return;

            StructuralMaterialStrengthEntry entry = new StructuralMaterialStrengthEntry
            {
                MaterialHash = hash,
                BaseStrength = math.max(1f, math.isfinite(baseStrength) ? baseStrength : 1f),
                BucklingStart01 = math.saturate(math.isfinite(bucklingStart) ? bucklingStart : 0.72f),
                PressureScale = math.max(0.01f, math.isfinite(pressureScale) ? pressureScale : 1f)
            };

            int start = (int)(hash % (uint)materials.Length);
            int firstEmpty = -1;
            for (int probe = 0; probe < materials.Length; probe++)
            {
                int index = WrapMaterialIndex(start + probe, materials.Length);
                StructuralMaterialStrengthEntry current = materials[index];
                if (current.MaterialHash == hash)
                {
                    materials[index] = entry;
                    return;
                }

                if (firstEmpty < 0 && current.MaterialHash == 0u)
                    firstEmpty = index;
            }

            if (firstEmpty >= 0)
                materials[firstEmpty] = entry;
        }

        private static int WrapMaterialIndex(int value, int length)
        {
            return (length & (length - 1)) == 0 ? (value & (length - 1)) : (value % length);
        }

        private void DumpTelemetry(string relativePath, in StructuralTelemetryEntry faultEntry)
        {
            NativeArray<StructuralTelemetryEntry> telemetry = ResolveVaultBuffer(in _telemetryHandle);
            NativeArray<int> cursor = ResolveVaultBuffer(in _telemetryCursorHandle);
            if (!telemetry.IsCreated)
                return;

            int cursorValue = cursor.IsCreated && cursor.Length > 0 ? cursor[0] : 0;
            StructuralTelemetryDumpHeader header = new StructuralTelemetryDumpHeader
            {
                Magic = StructuralIntegrityConstants.DumpMagic,
                Version = StructuralIntegrityConstants.DumpVersion,
                Frame = faultEntry.Frame,
                EntrySize = (uint)UnsafeUtility.SizeOf<StructuralTelemetryEntry>(),
                EntryCount = telemetry.Length,
                Cursor = cursorValue,
                FaultFlags = faultEntry.FaultFlags,
                StateHash = faultEntry.StateHash
            };

            int headerBytes = UnsafeUtility.SizeOf<StructuralTelemetryDumpHeader>();
            int stride = UnsafeUtility.SizeOf<StructuralTelemetryEntry>();
            int entryBytes = telemetry.Length * stride;
            int totalBytes = headerBytes + entryBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    totalBytes,
                    nameof(StructuralIntegrityCalculatorRuntime),
                    DumpPayloadLabel);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                UnsafeUtility.MemCpy(target, &header, headerBytes);
                byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                UnsafeUtility.MemCpy(target + headerBytes, source, entryBytes);
                NativeFaultDumpWriter.TryWriteAll(relativePath, payload, totalBytes);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(StructuralIntegrityCalculatorRuntime),
                    DumpPayloadLabel);
            }
        }

        private static StructuralTuningDTO SanitizeTuning(in StructuralTuningDTO source)
        {
            StructuralTuningDTO tuning = source;
            tuning.BasePressureKPa = SanitizeNonNegative(tuning.BasePressureKPa, 101.325f);
            tuning.PressureGradientKPaPerMeter = SanitizeNonNegative(tuning.PressureGradientKPaPerMeter, 10.05f);
            tuning.PressureToStressScale = SanitizeNonNegative(tuning.PressureToStressScale, 1f);
            tuning.MaterialStrengthFactor = math.max(0.01f, SanitizeNonNegative(tuning.MaterialStrengthFactor, 1f));
            tuning.BucklingStart01 = math.saturate(math.isfinite(tuning.BucklingStart01) ? tuning.BucklingStart01 : 0.72f);
            tuning.BucklingVisualIntensity = SanitizeNonNegative(tuning.BucklingVisualIntensity, 1f);
            tuning.SupportDamping = SanitizeNonNegative(tuning.SupportDamping, 0.45f);
            tuning.CollapseStress01 = math.max(0.01f, SanitizeNonNegative(tuning.CollapseStress01, 1f));
            tuning.GlobalQualityWeight = math.saturate(math.isfinite(tuning.GlobalQualityWeight) ? tuning.GlobalQualityWeight : 1f);
            tuning.SdfMetersPerVoxel = math.max(0.01f, SanitizeNonNegative(tuning.SdfMetersPerVoxel, 1f));
            tuning.SdfRangeMeters = math.max(0.01f, SanitizeNonNegative(tuning.SdfRangeMeters, 8f));
            tuning.ActiveNodeCount = math.clamp(tuning.ActiveNodeCount, 0, StructuralIntegrityConstants.MaxNodeCapacity);
            if (!math.all(math.isfinite(tuning.SeaLevelAup)))
                tuning.SeaLevelAup = double3.zero;
            if (!math.all(math.isfinite(tuning.SdfOriginAup)))
                tuning.SdfOriginAup = double3.zero;
            return tuning;
        }

        private static float SanitizeNonNegative(float value, float fallback)
        {
            return math.isfinite(value) && value >= 0f ? value : fallback;
        }

        private static int ResolveBatchSize(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            return math.clamp((int)math.lerp(128f, 32f, q), 32, 128);
        }

        private static float EstimateMicroseconds(int nodeCount, int edgeCount, int framesBetweenUpdates)
        {
            float raw = nodeCount * 0.018f + edgeCount * 0.006f + 7f;
            return math.max(1f, raw / math.max(1, framesBetweenUpdates));
        }

        private static int ResolveSdfDimension(NativeArray<byte> sdf)
        {
            if (!sdf.IsCreated || sdf.Length <= 0)
                return 0;

            int length = sdf.Length;
            int dim = 1;
            while (CubeVolume(dim + 1) <= length)
                dim++;

            return dim > 1 && CubeVolume(dim) <= length ? dim : 0;
        }

        private static long CubeVolume(int value)
        {
            return (long)value * value * value;
        }

        private float ResolveSimulationQualityWeight()
        {
            float fallback = math.saturate(math.isfinite(simulationQualityWeight) ? simulationQualityWeight : 1f);
            if (_dataVault == null)
                return fallback;

            NativeArray<StructuralTuningDTO> tuning = ResolveVaultBuffer(in _tuningHandle);
            if (!tuning.IsCreated || tuning.Length == 0)
                return fallback;

            float quality = tuning[0].GlobalQualityWeight;
            simulationQualityWeight = math.saturate(math.isfinite(quality) ? quality : fallback);
            return simulationQualityWeight;
        }

        private static ulong StructuralMutationGuardBit(BufferID bufferId)
        {
            return 1UL << ((int)bufferId & 31);
        }

        private static bool IsHullIntegrityVaultHandle<T>(in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return handle.BufferID != 0u &&
                   handle.SystemID == (uint)SystemID.HullIntegrity &&
                   handle.Generation != 0u;
        }

        private static bool IsHullIntegrityVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID bufferId)
            where T : struct
        {
            return handle.BufferID == unchecked((uint)(int)bufferId) &&
                   handle.SystemID == (uint)SystemID.HullIntegrity &&
                   handle.Generation != 0u;
        }

        private static int ResolveFramesBetweenUpdates(float quality)
        {
            float q = math.saturate(math.isfinite(quality) ? quality : 1f);
            return math.clamp((int)math.lerp(1f, 30f, 1.0f - q), 1, 30);
        }

        private static uint AdvanceSimulationFrame(uint frame)
        {
            uint next = frame + 1u;
            return next != 0u ? next : 1u;
        }

        private static float ResolveVisualQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, 1f);

            float quality = HomeostasisBrain.GlobalQualityWeight;
            return math.saturate(math.isfinite(quality) ? quality : 1f);
        }

        /// <summary>
        /// Converts an AUP delta into a bounded editor presentation position without letting corrupt coordinates overflow to infinity.
        /// </summary>
        /// <param name="aup">Absolute module coordinate in Core-owned AUP meters.</param>
        /// <param name="originAup">Local editor presentation origin, normally sea-level AUP.</param>
        /// <param name="position">Finite, bounded Unity presentation coordinate when conversion succeeds.</param>
        /// <returns>True when the relative coordinate is finite and safe to draw.</returns>
        public static bool TryBuildEditorRelativePosition(double3 aup, double3 originAup, out Vector3 position)
        {
            const double editorGizmoClampMeters = 1000000d;
            position = default;
            double3 relative = aup - originAup;
            if (!math.all(math.isfinite(relative)))
                return false;

            relative = math.clamp(relative, new double3(-editorGizmoClampMeters), new double3(editorGizmoClampMeters));
            float3 local = new float3((float)relative.x, (float)relative.y, (float)relative.z);
            if (!math.all(math.isfinite(local)))
                return false;

            position = new Vector3(local.x, local.y, local.z);
            return true;
        }

        private static string ResolveProjectPath(string relativePath)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string normalized = relativePath.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            return Path.Combine(root, normalized);
        }

#if UNITY_EDITOR
        private static int IndexOf(ReadOnlySpan<byte> span, byte value)
        {
            for (int i = 0; i < span.Length; i++)
            {
                if (span[i] == value)
                    return i;
            }

            return -1;
        }

        private static ReadOnlySpan<byte> TrimAscii(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && span[start] <= 32)
                start++;
            while (end >= start && span[end] <= 32)
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseAsciiFloat(ReadOnlySpan<byte> span, out float result)
        {
            result = 0f;
            span = TrimAscii(span);
            if (span.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (span[index] == (byte)'-' || span[index] == (byte)'+')
            {
                negative = span[index] == (byte)'-';
                index++;
            }

            double value = 0d;
            bool hasDigit = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                value = value * 10d + (span[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                double scale = 0.1d;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    value += (span[index] - (byte)'0') * scale;
                    scale *= 0.1d;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            result = (float)(negative ? -value : value);
            return math.isfinite(result);
        }
#endif

        private static uint HashLowerAsciiLiteral(string value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = (byte)value[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
            }

            return hash;
        }

#if UNITY_EDITOR
        private static uint HashLowerAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            bool any = false;
            for (int i = 0; i < value.Length; i++)
            {
                byte c = value[i];
                if (c <= 32)
                    continue;
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash = (hash ^ c) * 16777619u;
                any = true;
            }

            return any ? hash : 0u;
        }
#endif

#if UNITY_EDITOR
        private void OnValidate()
        {
            mockNodeCount = Mathf.Clamp(mockNodeCount, 1, StructuralIntegrityConstants.MaxNodeCapacity);
            simulationQualityWeight = Mathf.Clamp01(simulationQualityWeight);
            basePressureKPa = Mathf.Max(0f, basePressureKPa);
            pressureGradientKPaPerMeter = Mathf.Max(0f, pressureGradientKPaPerMeter);
            pressureToStressScale = Mathf.Max(0f, pressureToStressScale);
            materialStrengthFactor = Mathf.Max(0.01f, materialStrengthFactor);
            bucklingStart01 = Mathf.Clamp01(bucklingStart01);
            bucklingVisualIntensity = Mathf.Max(0f, bucklingVisualIntensity);
            supportDamping = Mathf.Max(0f, supportDamping);
            collapseStress01 = Mathf.Max(0.01f, collapseStress01);
            sdfMetersPerVoxel = Mathf.Max(0.01f, sdfMetersPerVoxel);
            sdfRangeMeters = Mathf.Max(0.01f, sdfRangeMeters);
        }
#endif
    }
}
