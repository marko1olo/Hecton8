using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Determinism;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-8600)]
    public sealed unsafe class MemorySentinelRuntime : MonoBehaviour, IDispatcherSystem, IGlobalRegistryHotSwapListener
    {
        private static int s_x001MemorySentinelRuntimeSignalPushDropCount;
        private const uint SystemHash = 0x53483733u; // SH73
        private const uint PostSimulationSystemHash = 0x5348374Fu; // SH7O
        private const int MaxTargets = 8;
        private const int RollbackByteCapacity = 8192;
        private const int CsvScratchCapacity = 4096;
        private const int RuntimeStateCount = 1;
        private const int AupSnapshotCount = 1;
        private const int MockInventoryCount = 1;
        private const int ModQuarantineSpanCount = 1;
        private const int DefaultTargetBatch = 1;
        private const int DefaultValidationFrequencyHz = 10;
        private const int MinimumValidationFrequencyHz = 1;
        private const float DefaultTeleportToleranceMeters = 50000f;
        private const float DefaultStrictness = 1f;
        private const float DisabledQualityOverride = -1f;
        private const double MaxNonShiftAupDeltaMeters = 50000d;
        private const uint DumpMagic = 0x494E5447u; // INTG
        private const uint DumpVersion = 1u;
        private const string DumpPath = "Docs/AgentLogs/Dump_INTEGRITY_SURGEON.bin";
        private const string DumpPayloadLabel = "memorySentinelTelemetryDumpPayload";
        private const string CsvRootPath = "validation_rules.csv";
        private const string CsvDocsPath = "Docs/Tasks/validation_rules.csv";
        private const SystemID OwnerSystemId = SystemID.CoreDeterminism;
        private const BufferID ValidationStatesBuffer = BufferID.MemorySentinelRuntime_ValidationStatesBuffer;
        private const BufferID TargetsBuffer = BufferID.MemorySentinelRuntime_TargetsBuffer;
        private const BufferID ResultsBuffer = BufferID.MemorySentinelRuntime_ResultsBuffer;
        private const BufferID RollbackBytesBuffer = BufferID.MemorySentinelRuntime_RollbackBytesBuffer;
        private const BufferID MockInventoryBuffer = BufferID.MemorySentinelRuntime_MockInventoryBuffer;
        private const BufferID TelemetryBuffer = BufferID.MemorySentinelRuntime_TelemetryBuffer;
        private const BufferID RuntimeStateBuffer = BufferID.MemorySentinelRuntime_RuntimeStateBuffer;
        private const BufferID AupSnapshotBuffer = BufferID.MemorySentinelRuntime_AupSnapshotBuffer;
        private const BufferID ModQuarantineBuffer = BufferID.MemorySentinelRuntime_ModQuarantineBuffer;

        private const uint TelemetryFlagJobBusy = 1u << 0;
        private const uint TelemetryFlagRollback = 1u << 1;
        private const uint TelemetryFlagFatal = 1u << 2;
        private const uint TelemetryFlagTeleport = 1u << 3;
        private const uint TelemetryFlagMockMutation = 1u << 4;
        private const uint TelemetryFlagCsvLoaded = 1u << 5;
        private const uint TelemetryFlagModQuarantine = 1u << 6;
        private const uint TelemetryFlagExternalCompileWallSafe = 1u << 31;

        private const uint HashValidationFrequency = 0x857535C7u;
        private const uint HashValidationFrequencyHz = 0xCDFC6CEEu;
        private const uint HashAupTeleportTolerance = 0xB0460359u;
        private const uint HashAupTeleportToleranceMeters = 0xAC8BC940u;
        private const uint HashStrictness = 0x1829CC2Fu;
        private const uint HashStrictnessLevel = 0x1451F016u;
        private const uint HashGlobalQualityWeight = 0xB00FB719u;
        private const uint HashModdedGameMask = 0xF5D5E264u;

        private static MemorySentinelRuntime s_active;
        private static uint s_pendingModdedGameMask;
        private static bool s_hasPendingModdedGameMask;

        private IDataVault _dataVault;
        private VaultGenerationHandle<ValidationStateDTO> _statesHandle;
        private VaultGenerationHandle<MemorySentinelTargetDTO> _targetsHandle;
        private VaultGenerationHandle<MemorySentinelResultDTO> _resultsHandle;
        private VaultGenerationHandle<byte> _rollbackHandle;
        private VaultGenerationHandle<MockInventorySpan> _mockInventoryHandle;
        private VaultGenerationHandle<MemorySentinelModQuarantineSpan> _modQuarantineHandle;
        private VaultGenerationHandle<MemorySentinelTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<MemorySentinelRuntimeStateDTO> _runtimeStateHandle;
        private VaultGenerationHandle<MemorySentinelAupSnapshotDTO> _aupSnapshotHandle;
        private IDataVault _targetBufferGuardVault;
        private ulong _targetBufferGuardMask;
        private bool _targetBufferGuardHeld;
        private SimulationPhaseSystem _simulationPhase;
        private PostSimulationPhaseSystem _postSimulationPhase;
        private JobHandle _validationHandle;
        private long _validationScheduleTimestamp;
        private int _targetCount;
        private int _lastTelemetryIndex;
        private uint _lastBytesHashed;
        private uint _lastTelemetryFlags;
        private bool _registeredSimulationDispatcher;
        private bool _registeredPostSimulationDispatcher;
        private bool _jobPending;
        private bool _stateMemoryCleared;
        private bool _runtimeDefaultsWritten;
        private bool _mockSeeded;
        private bool _modQuarantineSeeded;
        private bool _forceValidationNextFrame;
        private bool _registeredHotSwapListener;

        public static bool IsActive => s_active != null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            s_active = null;
            s_pendingModdedGameMask = 0u;
            s_hasPendingModdedGameMask = false;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallRuntime()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            GameObject host = new GameObject("SHINOBU_73_MemorySentinel");
            host.hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
            DontDestroyOnLoad(host);
            s_active = host.AddComponent<MemorySentinelRuntime>();
        }

        private void OnEnable()
        {
            if (s_active != null && !ReferenceEquals(s_active, this))
            {
                enabled = false;
                return;
            }

            s_active = this;
            RefreshVaultDependencyCold();
            ConfigureSignalLanes();
            TryRegisterHotSwapListener();
            RegisterDispatcherPhases();
        }

        private void OnDisable()
        {
            ForceCompleteValidationJobInPostSimulationWindow();
            UnlockTargetBuffers();
            TryUnregisterHotSwapListener();
            UnregisterDispatcherPhases();

            if (ReferenceEquals(s_active, this))
                s_active = null;

            ReleaseVaultHandles(_dataVault);
            _dataVault = null;
        }

        public uint GetSystemIdHash() => SystemHash;

        public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;

        public byte GetBucketId() => byte.MaxValue;

        public int GetDependencyCount() => 0;

        public uint GetDependencyHash(int dependencyIndex) => 0u;

        public void PreSimulationTick(in DispatcherTimingDTO timing)
        {
        }

        public JobHandle ScheduleSimulation(
            in DispatcherTimingDTO timing,
            in DispatcherJobContext context,
            JobHandle dependsOn)
        {
            IDataVault vault = ResolveVault();
            if (vault == null)
                return dependsOn;

            uint frame = context.Frame;
            if (!CompleteValidationJob(forceComplete: false))
            {
                if (TryResolveVaultBuffers(vault))
                    RecordTelemetry(vault, frame, 0u, 0u, 0u, 0u, TelemetryFlagJobBusy, 0f);
                return JobHandle.CombineDependencies(dependsOn, _validationHandle);
            }

            if (!TryResolveVaultBuffers(vault))
                return dependsOn;

            if (!TryResolveRequired(vault, in _runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray))
                return dependsOn;
            MemorySentinelRuntimeStateDTO runtime = OpenRuntimeStateForOwner(runtimeArray);
            ApplyModdedGameMaskSignals(runtimeArray, ref runtime);
            float quality = ResolveGlobalQualityWeight(ref runtime);
            int cadenceFrames = ResolveValidationCadenceFrames(in runtime, quality);
            runtime.GlobalQualityWeight = quality;
            runtime.ValidationCadenceFrames = cadenceFrames;
            runtimeArray[0] = runtime;

            ApplyHashDeltasFromSignals(vault, frame);
            float simulationTickDelta = timing.FixedDelta > 0f ? timing.FixedDelta : timing.FrameDelta;
            CheckAupTeleport(vault, frame, math.max(0.0001f, simulationTickDelta), quality);

            if (!TryResolveRequired(vault, in _statesHandle, MaxTargets, out NativeArray<ValidationStateDTO> states) ||
                !TryResolveRequired(vault, in _targetsHandle, MaxTargets, out NativeArray<MemorySentinelTargetDTO> targets) ||
                !TryResolveRequired(vault, in _resultsHandle, MaxTargets, out NativeArray<MemorySentinelResultDTO> results) ||
                !TryResolveRequired(vault, in _rollbackHandle, RollbackByteCapacity, out NativeArray<byte> rollback))
            {
                return dependsOn;
            }
            RefreshTargetsForOwner(vault, states, targets, rollback, frame, cadenceFrames, runtime.ModdedGameMask);

            if (_targetCount <= 0 || !LockTargetBuffers(vault, targets, _targetCount))
                return dependsOn;

            _validationHandle = new MemorySentinelValidationJob
            {
                States = states,
                Targets = targets,
                Results = results,
                Frame = frame,
                GlobalQualityWeight = quality
            }.Schedule(_targetCount, DefaultTargetBatch, dependsOn);

            _validationScheduleTimestamp = Stopwatch.GetTimestamp();
            _jobPending = true;
            _forceValidationNextFrame = false;
            H8Memory.RegisterActiveJob(OwnerSystemId, _validationHandle);
            return _validationHandle;
        }

        public void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            if (!_jobPending)
                return;

            IDataVault vault = ResolveVault();
            if (!CompleteValidationJob(forceComplete: false) && vault != null && TryResolveVaultBuffers(vault))
                RecordTelemetry(vault, timing.FrameId, 0u, 0u, 0u, 0u, TelemetryFlagJobBusy, 0f);
        }

        public void VisualSyncTick(in DispatcherTimingDTO timing)
        {
        }

        public static bool TryGetTunerSnapshot(out MemorySentinelTunerSnapshotDTO snapshot)
        {
            snapshot = default;
            MemorySentinelRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null)
                return false;

            if (!TryReadRequired(vault, in runtime._runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO>.ReadOnly runtimeArray))
                return false;
            TryReadRequired(vault, in runtime._telemetryHandle, MemorySentinelConstants.TelemetryCapacity, out NativeArray<MemorySentinelTelemetryEntry>.ReadOnly telemetry);

            MemorySentinelRuntimeStateDTO state = runtimeArray[0];
            MemorySentinelTelemetryEntry last = default;
            if (telemetry.IsCreated && telemetry.Length > 0)
                last = telemetry[math.clamp(runtime._lastTelemetryIndex, 0, telemetry.Length - 1)];

            snapshot.ValidationFrequencyHz = math.isfinite(state.ValidationFrequencyHz) ? state.ValidationFrequencyHz : 0f;
            snapshot.AupTeleportToleranceMeters = math.isfinite(state.AupTeleportToleranceMeters) ? state.AupTeleportToleranceMeters : 0f;
            snapshot.Strictness01 = math.saturate(math.isfinite(state.Strictness01) ? state.Strictness01 : 0f);
            snapshot.GlobalQualityWeight = math.saturate(math.isfinite(state.GlobalQualityWeight) ? state.GlobalQualityWeight : 0f);
            snapshot.LastValidationMs = math.isfinite(state.LastValidationMs) ? state.LastValidationMs : 0f;
            snapshot.LastValidationFrame = state.LastValidationFrame;
            snapshot.TargetCount = (uint)math.max(0, state.TargetCount);
            snapshot.LastCorrectedCount = (uint)math.max(0, state.LastCorrectedCount);
            snapshot.LastFatalCount = (uint)math.max(0, state.LastFatalCount);
            snapshot.LastBytesHashed = last.BytesHashedPerFrame;
            snapshot.ModdedGameMask = state.ModdedGameMask;
            snapshot.Flags = state.Flags;
            return true;
        }

        public static bool TrySetTunerParameters(
            float validationFrequencyHz,
            float aupTeleportToleranceMeters,
            float strictness01)
        {
            MemorySentinelRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureVaultBuffers(vault))
                return false;

            if (!TryResolveRequired(vault, in runtime._runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray))
                return false;

            MemorySentinelRuntimeStateDTO state = runtime.OpenRuntimeStateForOwner(runtimeArray);
            state.ValidationFrequencyHz = math.clamp(validationFrequencyHz, MinimumValidationFrequencyHz, DefaultValidationFrequencyHz);
            state.AupTeleportToleranceMeters = math.max(10f, aupTeleportToleranceMeters);
            state.Strictness01 = math.saturate(strictness01);
            runtimeArray[0] = state;
            runtime._forceValidationNextFrame = true;
            return true;
        }

        public static bool TrySetModdedGameMask(uint moddedGameMask)
        {
            s_pendingModdedGameMask = moddedGameMask;
            s_hasPendingModdedGameMask = true;

            MemorySentinelRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureVaultBuffers(vault))
                return false;

            if (!TryResolveRequired(vault, in runtime._runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray))
                return false;

            MemorySentinelRuntimeStateDTO state = runtime.OpenRuntimeStateForOwner(runtimeArray);
            state.ModdedGameMask = moddedGameMask;
            runtimeArray[0] = state;
            runtime._forceValidationNextFrame = true;
            return true;
        }

#if UNITY_EDITOR
        public static bool TryLoadValidationRulesCsv()
        {
            MemorySentinelRuntime runtime = s_active;
            if (runtime == null)
                return false;

            return runtime.LoadValidationRulesCsvInternal();
        }
#endif

        public static bool TrySimulateCheatEngineWrite()
        {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
            return false;
#else
            MemorySentinelRuntime runtime = s_active;
            if (runtime == null)
                return false;

            return runtime.SimulateCheatEngineWriteInternal();
#endif
        }

        public static bool TryDumpBlackBox()
        {
            MemorySentinelRuntime runtime = s_active;
            if (runtime == null)
                return false;

            IDataVault vault = runtime.ResolveVault();
            if (vault == null || !runtime.EnsureVaultBuffers(vault))
                return false;

            runtime.DumpBlackBox(vault);
            return true;
        }

        public static bool PublishHashDelta(
            BufferID bufferId,
            int byteOffset,
            int byteLength,
            uint expectedHash,
            uint flags)
        {
            HashDeltaUpdateSignal signal = default;
            signal.BufferId = (int)bufferId;
            signal.ByteOffset = math.max(0, byteOffset);
            signal.ByteLength = math.max(0, byteLength);
            signal.TargetHash = MemorySentinelMath.ComputeTargetHash((int)bufferId, signal.ByteOffset);
            signal.ExpectedHash = expectedHash;
            signal.StoredHash = expectedHash;
            signal.Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            signal.Flags = flags;
            SignalBus<HashDeltaUpdateSignal>.TryPushTracked(in signal, ref s_x001MemorySentinelRuntimeSignalPushDropCount);

            MemorySentinelRuntime runtime = s_active;
            if (runtime == null)
                return true;

            IDataVault vault = runtime.ResolveVault();
            return vault == null || !runtime.TryResolveVaultBuffers(vault)
                ? true
                : runtime.ApplyHashDelta(vault, in signal);
        }

        private static void ConfigureSignalLanes()
        {
            SignalBus<MemoryDesyncSignal>.Configure(32, maxFrameSignals: 32, lowTierFrameSignals: 8, laneHash: 0x4D445359u);
            SignalBus<MemoryDesyncSignal>.EnsureInitialized();
            SignalBus<HashDeltaUpdateSignal>.Configure(64, maxFrameSignals: 64, lowTierFrameSignals: 16, laneHash: 0x48445550u);
            SignalBus<HashDeltaUpdateSignal>.EnsureInitialized();
            SignalBus<MemorySentinelRollbackSignal>.Configure(32, maxFrameSignals: 32, lowTierFrameSignals: 8, laneHash: 0x4D52424Bu);
            SignalBus<MemorySentinelRollbackSignal>.EnsureInitialized();
            SignalBus<ModdedGameMaskSignal>.Configure(8, maxFrameSignals: 8, lowTierFrameSignals: 2, laneHash: 0x4D4D534Bu);
            SignalBus<ModdedGameMaskSignal>.EnsureInitialized();
        }

        private IDataVault ResolveVault()
        {
            return _dataVault;
        }

        private void RefreshVaultDependencyCold()
        {
            RebindVaultDependencyCold(GlobalRegistry.DataVault);
        }

        private void RebindVaultDependencyCold(IDataVault nextVault)
        {
            if (ReferenceEquals(_dataVault, nextVault))
            {
                if (_dataVault != null)
                    EnsureVaultBuffers(_dataVault);
                return;
            }

            ForceCompleteValidationJobInPostSimulationWindow();
            UnlockTargetBuffers();
            ReleaseVaultHandles(_dataVault);
            _dataVault = nextVault;
            if (_dataVault != null)
                EnsureVaultBuffers(_dataVault);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener)
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

        private void RegisterDispatcherPhases()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (_simulationPhase == null)
                _simulationPhase = new SimulationPhaseSystem(this);
            if (_postSimulationPhase == null)
                _postSimulationPhase = new PostSimulationPhaseSystem(this);

            if (!_registeredSimulationDispatcher)
            {
                if (!GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                    return;
                _registeredSimulationDispatcher = true;
            }

            if (_registeredPostSimulationDispatcher)
                return;

            if (GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
            {
                _registeredPostSimulationDispatcher = true;
                return;
            }

            if (_registeredSimulationDispatcher)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulationDispatcher = false;
            }
        }

        private void UnregisterDispatcherPhases()
        {
            if (_registeredPostSimulationDispatcher)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulationDispatcher = false;
            }

            if (_registeredSimulationDispatcher)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulationDispatcher = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (!isActiveAndEnabled)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    RebindVaultDependencyCold(currentService as IDataVault);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    UnregisterDispatcherPhases();
                    if (currentService != null)
                        RegisterDispatcherPhases();
                    break;
            }
        }

        private static bool TryResolveRequired<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool TryReadRequired<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            int requiredLength,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            return vault != null &&
                   handle.BufferID != 0u &&
                   handle.Generation != 0u &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static bool OpenOrAcquireVaultBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer) where T : struct
        {
            if (TryResolveRequired(vault, in handle, requiredLength, out buffer))
                return true;

            if (vault == null || bufferId == BufferID.Unknown || requiredLength <= 0)
            {
                buffer = default;
                return false;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existing) &&
                TryResolveRequired(vault, in existing, requiredLength, out buffer))
            {
                handle = existing;
                return true;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
            {
                buffer = default;
                return false;
            }

            handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                OwnerSystemId,
                options);
            return TryResolveRequired(vault, in handle, requiredLength, out buffer);
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ReleaseVaultHandle(vault, ref _statesHandle);
            ReleaseVaultHandle(vault, ref _targetsHandle);
            ReleaseVaultHandle(vault, ref _resultsHandle);
            ReleaseVaultHandle(vault, ref _rollbackHandle);
            ReleaseVaultHandle(vault, ref _mockInventoryHandle);
            ReleaseVaultHandle(vault, ref _modQuarantineHandle);
            ReleaseVaultHandle(vault, ref _telemetryHandle);
            ReleaseVaultHandle(vault, ref _runtimeStateHandle);
            ReleaseVaultHandle(vault, ref _aupSnapshotHandle);
            _stateMemoryCleared = false;
            _runtimeDefaultsWritten = false;
            _mockSeeded = false;
            _modQuarantineSeeded = false;
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && handle.BufferID != 0u && handle.Generation != 0u)
            {
                try
                {
                    vault.ReleaseBuffer(in handle);
                }
                catch (Exception)
                {
                }
            }

            handle = default;
        }

        private bool EnsureVaultBuffers(IDataVault vault)
        {
            if (vault == null)
                return false;

            uint previousStatesGeneration = _statesHandle.Generation;
            uint previousTargetsGeneration = _targetsHandle.Generation;
            uint previousResultsGeneration = _resultsHandle.Generation;
            uint previousMockGeneration = _mockInventoryHandle.Generation;
            uint previousQuarantineGeneration = _modQuarantineHandle.Generation;
            uint previousRuntimeGeneration = _runtimeStateHandle.Generation;

            NativeArray<ValidationStateDTO> states = default;
            NativeArray<MemorySentinelTargetDTO> targets = default;
            NativeArray<MemorySentinelResultDTO> results = default;
            bool stateViewsReady =
                OpenOrAcquireVaultBuffer(
                    vault,
                    ref _statesHandle,
                    ValidationStatesBuffer,
                    MaxTargets,
                    NativeArrayOptions.UninitializedMemory,
                    out states) &&
                OpenOrAcquireVaultBuffer(
                    vault,
                    ref _targetsHandle,
                    TargetsBuffer,
                    MaxTargets,
                    NativeArrayOptions.UninitializedMemory,
                    out targets) &&
                OpenOrAcquireVaultBuffer(
                    vault,
                    ref _resultsHandle,
                    ResultsBuffer,
                    MaxTargets,
                    NativeArrayOptions.UninitializedMemory,
                    out results);
            if (!stateViewsReady)
            {
                return false;
            }
            if (_statesHandle.Generation != previousStatesGeneration ||
                _targetsHandle.Generation != previousTargetsGeneration ||
                _resultsHandle.Generation != previousResultsGeneration)
            {
                _stateMemoryCleared = false;
            }

            NativeArray<byte> rollback = default;
            NativeArray<MockInventorySpan> mockInventory = default;
            NativeArray<MemorySentinelModQuarantineSpan> modQuarantine = default;
            NativeArray<MemorySentinelTelemetryEntry> telemetry = default;
            NativeArray<MemorySentinelRuntimeStateDTO> runtimeState = default;
            NativeArray<MemorySentinelAupSnapshotDTO> aupSnapshot = default;

            if (!OpenOrAcquireVaultBuffer(vault, ref _rollbackHandle, RollbackBytesBuffer, RollbackByteCapacity, NativeArrayOptions.UninitializedMemory, out rollback) ||
                !OpenOrAcquireVaultBuffer(vault, ref _mockInventoryHandle, MockInventoryBuffer, MockInventoryCount, NativeArrayOptions.UninitializedMemory, out mockInventory) ||
                !OpenOrAcquireVaultBuffer(vault, ref _modQuarantineHandle, ModQuarantineBuffer, ModQuarantineSpanCount, NativeArrayOptions.UninitializedMemory, out modQuarantine) ||
                !OpenOrAcquireVaultBuffer(vault, ref _telemetryHandle, TelemetryBuffer, MemorySentinelConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory, out telemetry) ||
                !OpenOrAcquireVaultBuffer(vault, ref _runtimeStateHandle, RuntimeStateBuffer, RuntimeStateCount, NativeArrayOptions.ClearMemory, out runtimeState) ||
                !OpenOrAcquireVaultBuffer(vault, ref _aupSnapshotHandle, AupSnapshotBuffer, AupSnapshotCount, NativeArrayOptions.ClearMemory, out aupSnapshot))
            {
                return false;
            }
            if (!rollback.IsCreated ||
                !mockInventory.IsCreated ||
                !modQuarantine.IsCreated ||
                !telemetry.IsCreated ||
                !aupSnapshot.IsCreated)
            {
                return false;
            }
            if (_mockInventoryHandle.Generation != previousMockGeneration)
                _mockSeeded = false;
            if (_modQuarantineHandle.Generation != previousQuarantineGeneration)
                _modQuarantineSeeded = false;
            if (_runtimeStateHandle.Generation != previousRuntimeGeneration)
                _runtimeDefaultsWritten = false;

            if (!_stateMemoryCleared)
            {
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(states), states.Length * UnsafeUtility.SizeOf<ValidationStateDTO>());
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(targets), targets.Length * UnsafeUtility.SizeOf<MemorySentinelTargetDTO>());
                UnsafeUtility.MemClear(NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(results), results.Length * UnsafeUtility.SizeOf<MemorySentinelResultDTO>());
                _stateMemoryCleared = true;
            }

            OpenRuntimeStateForOwner(runtimeState);
            SeedMockInventory(vault);
            SeedModQuarantine(vault);
            return true;
        }

        private bool TryResolveVaultBuffers(IDataVault vault)
        {
            return TryResolveRequired(vault, in _statesHandle, MaxTargets, out NativeArray<ValidationStateDTO> _) &&
                   TryResolveRequired(vault, in _targetsHandle, MaxTargets, out NativeArray<MemorySentinelTargetDTO> _) &&
                   TryResolveRequired(vault, in _resultsHandle, MaxTargets, out NativeArray<MemorySentinelResultDTO> _) &&
                   TryResolveRequired(vault, in _rollbackHandle, RollbackByteCapacity, out NativeArray<byte> _) &&
                   TryResolveRequired(vault, in _mockInventoryHandle, MockInventoryCount, out NativeArray<MockInventorySpan> _) &&
                   TryResolveRequired(vault, in _modQuarantineHandle, ModQuarantineSpanCount, out NativeArray<MemorySentinelModQuarantineSpan> _) &&
                   TryResolveRequired(vault, in _telemetryHandle, MemorySentinelConstants.TelemetryCapacity, out NativeArray<MemorySentinelTelemetryEntry> _) &&
                   TryResolveRequired(vault, in _runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO> _) &&
                   TryResolveRequired(vault, in _aupSnapshotHandle, AupSnapshotCount, out NativeArray<MemorySentinelAupSnapshotDTO> _);
        }

        private MemorySentinelRuntimeStateDTO OpenRuntimeStateForOwner(NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray)
        {
            if (!runtimeArray.IsCreated || runtimeArray.Length <= 0)
                return default;

            MemorySentinelRuntimeStateDTO state = runtimeArray[0];
            uint preservedModdedGameMask = state.ModdedGameMask;
            if (!_runtimeDefaultsWritten ||
                !math.isfinite(state.ValidationFrequencyHz) ||
                state.ValidationFrequencyHz < MinimumValidationFrequencyHz ||
                state.ValidationFrequencyHz > DefaultValidationFrequencyHz ||
                !math.isfinite(state.AupTeleportToleranceMeters) ||
                state.AupTeleportToleranceMeters <= 0f ||
                !math.isfinite(state.Strictness01) ||
                state.Strictness01 < 0f ||
                state.Strictness01 > 1f ||
                !math.isfinite(state.GlobalQualityWeightOverride) ||
                !math.isfinite(state.GlobalQualityWeight))
            {
                state.ValidationFrequencyHz = DefaultValidationFrequencyHz;
                state.AupTeleportToleranceMeters = DefaultTeleportToleranceMeters;
                state.Strictness01 = DefaultStrictness;
                state.GlobalQualityWeightOverride = DisabledQualityOverride;
                state.GlobalQualityWeight = HomeostasisBrain.GlobalQualityWeight;
                state.ValidationCadenceFrames = 6;
                state.ModdedGameMask = _runtimeDefaultsWritten ? preservedModdedGameMask : 0u;
                runtimeArray[0] = state;
                _runtimeDefaultsWritten = true;
            }

            if (s_hasPendingModdedGameMask && state.ModdedGameMask != s_pendingModdedGameMask)
            {
                state.ModdedGameMask = s_pendingModdedGameMask;
                runtimeArray[0] = state;
            }

            return state;
        }

        private void ApplyModdedGameMaskSignals(
            NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray,
            ref MemorySentinelRuntimeStateDTO runtime)
        {
            if (!runtimeArray.IsCreated || runtimeArray.Length <= 0)
                return;

            ReadOnlySpan<ModdedGameMaskSignal> snapshot = SignalBus<ModdedGameMaskSignal>.GetFrameSnapshot();
            if (snapshot.Length <= 0)
                return;

            uint mask = runtime.ModdedGameMask;
            bool hasSignal = false;
            for (int i = 0; i < snapshot.Length; i++)
            {
                ModdedGameMaskSignal signal = snapshot[i];
                mask = signal.ModdedGameMask;
                hasSignal = true;
            }

            if (!hasSignal)
                return;

            s_pendingModdedGameMask = mask;
            s_hasPendingModdedGameMask = true;
            if (runtime.ModdedGameMask == mask)
                return;

            runtime.ModdedGameMask = mask;
            runtimeArray[0] = runtime;
            _forceValidationNextFrame = true;
            _lastTelemetryFlags |= TelemetryFlagModQuarantine;
        }

        private float ResolveGlobalQualityWeight(ref MemorySentinelRuntimeStateDTO runtime)
        {
            float overrideWeight = runtime.GlobalQualityWeightOverride;
            if (overrideWeight >= 0f)
                return math.saturate(overrideWeight);

            return math.saturate(HomeostasisBrain.GlobalQualityWeight);
        }

        private static int ResolveValidationCadenceFrames(in MemorySentinelRuntimeStateDTO runtime, float quality)
        {
            float configuredHz = math.clamp(runtime.ValidationFrequencyHz, MinimumValidationFrequencyHz, DefaultValidationFrequencyHz);
            float strictness = math.saturate(runtime.Strictness01);
            float quality01 = math.saturate(quality);
            float smoothQuality = quality01 * quality01 * (3f - (2f * quality01));
            float weightedHz = math.lerp(MinimumValidationFrequencyHz, configuredHz, math.saturate(smoothQuality * (0.5f + strictness * 0.5f)));
            return math.max(1, (int)math.round(60f / math.max(0.01f, weightedHz)));
        }

        private void SeedMockInventory(IDataVault vault)
        {
            if (_mockSeeded)
                return;

            if (!TryResolveRequired(vault, in _mockInventoryHandle, MockInventoryCount, out NativeArray<MockInventorySpan> mock))
                return;

            MockInventorySpan span = default;
            span.Word0 = 0x484558544F4E3038UL;
            span.Word1 = 0x5348494E4F425537UL;
            span.Word2 = 0x494E565F484F5431UL;
            span.Word3 = 0x444541524C49455FUL;
            span.Word4 = 0xA5A5A5A5A5A5A5A5UL;
            span.Word5 = 0x0102030405060708UL;
            span.Word6 = 0x1112131415161718UL;
            span.Word7 = 0x2122232425262728UL;
            mock[0] = span;
            _mockSeeded = true;
        }

        private void SeedModQuarantine(IDataVault vault)
        {
            if (_modQuarantineSeeded)
                return;

            if (!TryResolveRequired(vault, in _modQuarantineHandle, ModQuarantineSpanCount, out NativeArray<MemorySentinelModQuarantineSpan> quarantine))
                return;

            MemorySentinelModQuarantineSpan span = default;
            span.Prefix = MemorySentinelConstants.ModPrefix32LE;
            span.ModHash = SystemHash;
            span.MutationCounter = 0u;
            span.Flags = MemorySentinelConstants.TargetFlagModQuarantine;
            span.Payload0 = 0x4D4F44505F53414EUL;
            span.Payload1 = 0x44424F585F534837UL;
            span.Payload2 = 0x51554152414E544EUL;
            span.Payload3 = 0x484F544441544130UL;
            span.Payload4 = 0x0000000000000001UL;
            span.Payload5 = 0x0000000000000000UL;
            quarantine[0] = span;
            _modQuarantineSeeded = true;
        }

        private void RefreshTargetsForOwner(
            IDataVault vault,
            NativeArray<ValidationStateDTO> states,
            NativeArray<MemorySentinelTargetDTO> targets,
            NativeArray<byte> rollback,
            uint frame,
            int cadenceFrames,
            uint moddedGameMask)
        {
            _targetCount = 0;
            int rollbackOffset = 0;

            if (TryResolveRequired(vault, in _mockInventoryHandle, MockInventoryCount, out NativeArray<MockInventorySpan> mockInventory))
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mockInventory);
                AppendTarget(
                    vault,
                    states,
                    targets,
                    rollback,
                    ref rollbackOffset,
                    MockInventoryBuffer,
                    ptr,
                    UnsafeUtility.SizeOf<MockInventorySpan>(),
                    MemorySentinelConstants.TargetFlagActive |
                    MemorySentinelConstants.TargetFlagRollback |
                    MemorySentinelConstants.TargetFlagInventory |
                    MemorySentinelConstants.TargetFlagMock,
                    0f,
                    0.4f,
                    _forceValidationNextFrame ? 1u : (uint)math.max(1, cadenceFrames),
                    frame,
                    moddedGameMask);
            }

            if (TryResolveRequired(vault, in _modQuarantineHandle, ModQuarantineSpanCount, out NativeArray<MemorySentinelModQuarantineSpan> modQuarantine))
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(modQuarantine);
                AppendTarget(
                    vault,
                    states,
                    targets,
                    rollback,
                    ref rollbackOffset,
                    ModQuarantineBuffer,
                    ptr,
                    UnsafeUtility.SizeOf<MemorySentinelModQuarantineSpan>(),
                    MemorySentinelConstants.TargetFlagActive |
                    MemorySentinelConstants.TargetFlagRollback |
                    MemorySentinelConstants.TargetFlagAllowModPrefix |
                    MemorySentinelConstants.TargetFlagModQuarantine,
                    0f,
                    0.1f,
                    _forceValidationNextFrame ? 1u : (uint)math.max(1, cadenceFrames),
                    frame,
                    moddedGameMask);
            }

            AppendExistingBuffer<uint>(
                vault,
                states,
                targets,
                rollback,
                ref rollbackOffset,
                BufferID.ShinobuInventoryHashes,
                MemorySentinelConstants.TargetFlagCritical |
                MemorySentinelConstants.TargetFlagRollback |
                MemorySentinelConstants.TargetFlagInventory,
                0f,
                0.85f,
                math.max(1, cadenceFrames / 2),
                frame,
                moddedGameMask);

            AppendExistingBuffer<int>(
                vault,
                states,
                targets,
                rollback,
                ref rollbackOffset,
                BufferID.ShinobuInventoryQuantities,
                MemorySentinelConstants.TargetFlagCritical |
                MemorySentinelConstants.TargetFlagRollback |
                MemorySentinelConstants.TargetFlagInventory,
                0f,
                0.9f,
                math.max(1, cadenceFrames / 2),
                frame,
                moddedGameMask);

            AppendExistingBuffer<float>(
                vault,
                states,
                targets,
                rollback,
                ref rollbackOffset,
                BufferID.ShinobuInventoryDurabilities,
                MemorySentinelConstants.TargetFlagCritical |
                MemorySentinelConstants.TargetFlagRollback |
                MemorySentinelConstants.TargetFlagInventory,
                0.2f,
                0.75f,
                math.max(1, cadenceFrames),
                frame,
                moddedGameMask);

            AppendExistingBuffer<LockstepPlayerKinematicState>(
                vault,
                states,
                targets,
                rollback,
                ref rollbackOffset,
                BufferID.PlayerKinematicState,
                MemorySentinelConstants.TargetFlagCritical |
                MemorySentinelConstants.TargetFlagRollback |
                MemorySentinelConstants.TargetFlagAup,
                0f,
                1f,
                1,
                frame,
                moddedGameMask,
                maxElements: 1);

            AppendExistingBuffer<VaultAup64>(
                vault,
                states,
                targets,
                rollback,
                ref rollbackOffset,
                BufferID.VaultAup64,
                MemorySentinelConstants.TargetFlagRollback |
                MemorySentinelConstants.TargetFlagAup,
                0.5f,
                0.6f,
                math.max(1, cadenceFrames),
                frame,
                moddedGameMask,
                maxElements: 4);

            if (TryResolveRequired(vault, in _runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray))
            {
                MemorySentinelRuntimeStateDTO runtime = OpenRuntimeStateForOwner(runtimeArray);
                runtime.TargetCount = _targetCount;
                runtimeArray[0] = runtime;
            }
        }

        private void AppendExistingBuffer<T>(
            IDataVault vault,
            NativeArray<ValidationStateDTO> states,
            NativeArray<MemorySentinelTargetDTO> targets,
            NativeArray<byte> rollback,
            ref int rollbackOffset,
            BufferID bufferId,
            uint flags,
            float minQuality,
            float criticality,
            int checkInterval,
            uint frame,
            uint moddedGameMask,
            int maxElements = int.MaxValue)
            where T : struct
        {
            if (_targetCount >= MaxTargets ||
                !vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> handle) ||
                !TryResolveRequired(vault, in handle, 1, out NativeArray<T> buffer))
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(buffer);
            if (ptr == null || buffer.Length <= 0)
                return;

            int elementCount = math.min(buffer.Length, maxElements);
            int byteLength = elementCount * UnsafeUtility.SizeOf<T>();
            if (byteLength <= 0)
                return;

            AppendTarget(
                vault,
                states,
                targets,
                rollback,
                ref rollbackOffset,
                bufferId,
                ptr,
                byteLength,
                flags | MemorySentinelConstants.TargetFlagActive,
                minQuality,
                criticality,
                (uint)math.max(1, checkInterval),
                frame,
                moddedGameMask);
        }

        private void AppendTarget(
            IDataVault vault,
            NativeArray<ValidationStateDTO> states,
            NativeArray<MemorySentinelTargetDTO> targets,
            NativeArray<byte> rollback,
            ref int rollbackOffset,
            BufferID bufferId,
            void* ptr,
            int byteLength,
            uint flags,
            float minQuality,
            float criticality,
            uint checkInterval,
            uint frame,
            uint moddedGameMask)
        {
            if (_targetCount >= MaxTargets || ptr == null || byteLength <= 0)
                return;

            int alignedLength = Align16(byteLength);
            if ((flags & MemorySentinelConstants.TargetFlagRollback) != 0u &&
                rollbackOffset + alignedLength > rollback.Length)
            {
                flags &= ~MemorySentinelConstants.TargetFlagRollback;
            }

            int index = _targetCount++;
            int byteOffset = bufferId == BufferID.Unknown ? index : 0;
            uint targetHash = MemorySentinelMath.ComputeTargetHash((int)bufferId, byteOffset);
            MemorySentinelTargetDTO previousTarget = targets[index];
            MemorySentinelTargetDTO target = default;
            target.TargetMemoryPointer = (ulong)ptr;
            target.ByteLength = byteLength;
            target.RollbackByteOffset = (flags & MemorySentinelConstants.TargetFlagRollback) != 0u ? rollbackOffset : -1;
            target.TargetHash = targetHash;
            target.Flags = flags;
            target.CheckInterval = checkInterval;
            target.LastLegalFrame = previousTarget.TargetHash == targetHash ? previousTarget.LastLegalFrame : frame;
            target.MinQualityWeight = math.saturate(minQuality);
            target.Criticality01 = math.saturate(criticality);
            target.ModdedGameMask = moddedGameMask;
            target.BufferId = (int)bufferId;
            target.TargetMemoryFingerprint = MemorySentinelMath.ComputePointerFingerprint((ulong)ptr, byteLength, (int)bufferId);
            targets[index] = target;

            ValidationStateDTO state = states[index];
            bool unseeded = state.ExpectedHash == 0u ||
                            state.TargetMemoryPointer != target.TargetMemoryPointer ||
                            state.CheckInterval == 0u;
            state.TargetMemoryPointer = target.TargetMemoryPointer;
            state.CheckInterval = checkInterval;
            if (unseeded)
            {
                uint hash = MemorySentinelMath.ComputeXXHash3Folded(ptr, byteLength);
                state.ExpectedHash = hash;
                state.StoredHash = hash;
                CopyTargetToRollback(rollback, in target);
            }

            states[index] = state;
            if (target.RollbackByteOffset >= 0)
                rollbackOffset += alignedLength;
        }

        private static int Align16(int value)
        {
            return (value + 15) & ~15;
        }

        private bool LockTargetBuffers(IDataVault vault, NativeArray<MemorySentinelTargetDTO> targets, int targetCount)
        {
            if (_targetBufferGuardHeld)
                return false;

            if (vault == null)
                return false;

            ulong guardMask =
                TargetBufferMutationGuardBit(ValidationStatesBuffer) |
                TargetBufferMutationGuardBit(TargetsBuffer) |
                TargetBufferMutationGuardBit(ResultsBuffer);

            for (int i = 0; i < targetCount; i++)
            {
                BufferID bufferId = (BufferID)targets[i].BufferId;
                if (bufferId == BufferID.Unknown)
                    continue;

                guardMask |= TargetBufferMutationGuardBit(bufferId);
            }

            if (!vault.TryAcquireMutationGuard(guardMask))
                return false;

            _targetBufferGuardVault = vault;
            _targetBufferGuardMask = guardMask;
            _targetBufferGuardHeld = true;
            return true;
        }

        private static ulong TargetBufferMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 63);
        }

        private void UnlockTargetBuffers()
        {
            if (!_targetBufferGuardHeld)
                return;

            IDataVault vault = _targetBufferGuardVault;
            ulong guardMask = _targetBufferGuardMask;
            _targetBufferGuardVault = null;
            _targetBufferGuardMask = 0UL;
            _targetBufferGuardHeld = false;

            if (vault == null)
            {
                _lastTelemetryFlags |= TelemetryFlagFatal;
                return;
            }

            if (vault != null && guardMask != 0UL)
                vault.ReleaseMutationGuard(guardMask);
        }

        private bool CompleteValidationJob(bool forceComplete)
        {
            if (!_jobPending)
                return true;

            if (!forceComplete && !_validationHandle.IsCompleted)
                return false;

            if (forceComplete)
            {
                DispatcherJobFence.BeginPostSimulationSwapWindow();
                try
                {
                    DispatcherJobFence.TryComplete(ref _validationHandle, forceComplete: true);
                }
                finally
                {
                    DispatcherJobFence.EndPostSimulationSwapWindow();
                }
            }
            else if (!DispatcherJobFence.TryFinalizeCompleted(ref _validationHandle))
            {
                return false;
            }

            _jobPending = false;

            IDataVault vault = ResolveVault();
            if (vault == null || !(forceComplete ? EnsureVaultBuffers(vault) : TryResolveVaultBuffers(vault)))
            {
                UnlockTargetBuffers();
                return true;
            }

            float elapsedMs = ResolveElapsedMs(_validationScheduleTimestamp);
            try
            {
                ConsumeResults(vault, elapsedMs);
            }
            finally
            {
                UnlockTargetBuffers();
            }

            return true;
        }

        private bool ForceCompleteValidationJobInPostSimulationWindow()
        {
            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                return CompleteValidationJob(forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }
        }

        private void ConsumeResults(IDataVault vault, float elapsedMs)
        {
            if (!TryResolveRequired(vault, in _statesHandle, MaxTargets, out NativeArray<ValidationStateDTO> states) ||
                !TryResolveRequired(vault, in _targetsHandle, MaxTargets, out NativeArray<MemorySentinelTargetDTO> targets) ||
                !TryResolveRequired(vault, in _resultsHandle, MaxTargets, out NativeArray<MemorySentinelResultDTO> results))
            {
                return;
            }

            uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;
            uint bytesHashed = 0u;
            uint desyncsDetected = 0u;
            uint desyncsCorrected = 0u;
            uint rollbackBytes = 0u;
            uint fatalCount = 0u;
            uint flags = 0u;
            MemorySentinelResultDTO lastMismatch = default;
            MemorySentinelTargetDTO lastTarget = default;

            int count = math.min(_targetCount, math.min(results.Length, targets.Length));
            for (int i = 0; i < count; i++)
            {
                MemorySentinelResultDTO result = results[i];
                MemorySentinelTargetDTO target = targets[i];
                if ((result.Flags & MemorySentinelConstants.ResultFlagHashed) != 0u)
                    bytesHashed += (uint)math.max(0, result.ByteLength);

                if ((result.Flags & MemorySentinelConstants.ResultFlagSkippedModQuarantine) != 0u)
                    flags |= TelemetryFlagModQuarantine;

                bool mismatch = (result.Flags & (
                    MemorySentinelConstants.ResultFlagMismatch |
                    MemorySentinelConstants.ResultFlagPointerMismatch |
                    MemorySentinelConstants.ResultFlagPointerFingerprintMismatch |
                    MemorySentinelConstants.ResultFlagInvalidPointer)) != 0u;
                if (!mismatch)
                    continue;

                desyncsDetected++;
                lastMismatch = result;
                lastTarget = target;

                bool corrected = TryRollbackTarget(vault, in target);
                if (corrected)
                {
                    ValidationStateDTO state = states[i];
                    state.StoredHash = state.ExpectedHash;
                    states[i] = state;
                    desyncsCorrected++;
                    rollbackBytes += (uint)math.max(0, target.ByteLength);
                    flags |= TelemetryFlagRollback;
                    PublishRollback(in target, in state, frame);
                }

                if (corrected)
                    PublishDesync(in target, in result, corrected: true, fatal: false, teleport: false);

                bool critical = (target.Flags & MemorySentinelConstants.TargetFlagCritical) != 0u ||
                                target.Criticality01 >= 0.99f ||
                                (result.Flags & MemorySentinelConstants.ResultFlagInvalidPointer) != 0u;
                if (critical && !corrected)
                {
                    fatalCount++;
                    flags |= TelemetryFlagFatal;
                    PublishDesync(in target, in result, corrected: false, fatal: true, teleport: false);
                    RecordTelemetry(vault, frame, bytesHashed, desyncsCorrected, desyncsDetected, fatalCount, flags, elapsedMs, in lastTarget, in lastMismatch, rollbackBytes);
                    DumpBlackBox(vault);
                    throw new FatalArchitectureException("SHINOBU_73 critical memory tamper is uncorrectable.");
                }

                if (!corrected && !critical)
                    PublishDesync(in target, in result, corrected: false, fatal: false, teleport: false);
            }

            _lastBytesHashed = bytesHashed;
            _lastTelemetryFlags = flags;
            RecordTelemetry(vault, frame, bytesHashed, desyncsCorrected, desyncsDetected, fatalCount, flags, elapsedMs, in lastTarget, in lastMismatch, rollbackBytes);

            if (TryResolveRequired(vault, in _runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray))
            {
                MemorySentinelRuntimeStateDTO runtime = OpenRuntimeStateForOwner(runtimeArray);
                runtime.LastValidationMs = elapsedMs;
                runtime.LastValidationFrame = frame;
                runtime.LastCorrectedCount = (int)desyncsCorrected;
                runtime.LastFatalCount = (int)fatalCount;
                runtime.TargetCount = _targetCount;
                runtime.Flags = flags;
                runtimeArray[0] = runtime;
            }
        }

        private bool TryRollbackTarget(IDataVault vault, in MemorySentinelTargetDTO target)
        {
            if ((target.Flags & MemorySentinelConstants.TargetFlagRollback) == 0u ||
                target.RollbackByteOffset < 0 ||
                target.ByteLength <= 0 ||
                target.TargetMemoryPointer == 0UL)
            {
                return false;
            }

            if (!TryResolveRequired(vault, in _rollbackHandle, RollbackByteCapacity, out NativeArray<byte> rollback) ||
                target.RollbackByteOffset > rollback.Length ||
                target.ByteLength > rollback.Length - target.RollbackByteOffset)
            {
                return false;
            }

            byte* source = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(rollback) + target.RollbackByteOffset;
            return UnsafeMemoryCopyGuard.SafeCopy(
                (void*)target.TargetMemoryPointer,
                target.ByteLength,
                source,
                target.ByteLength);
        }

        private void CopyTargetToRollback(NativeArray<byte> rollback, in MemorySentinelTargetDTO target)
        {
            if (!rollback.IsCreated ||
                target.RollbackByteOffset < 0 ||
                target.ByteLength <= 0 ||
                target.TargetMemoryPointer == 0UL ||
                target.RollbackByteOffset > rollback.Length ||
                target.ByteLength > rollback.Length - target.RollbackByteOffset)
            {
                return;
            }

            byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(rollback) + target.RollbackByteOffset;
            UnsafeMemoryCopyGuard.SafeCopy(
                destination,
                target.ByteLength,
                (void*)target.TargetMemoryPointer,
                target.ByteLength);
        }

        private void ApplyHashDeltasFromSignals(IDataVault vault, uint frame)
        {
            ReadOnlySpan<HashDeltaUpdateSignal> snapshot = SignalBus<HashDeltaUpdateSignal>.GetFrameSnapshot();
            for (int i = 0; i < snapshot.Length; i++)
            {
                HashDeltaUpdateSignal signal = snapshot[i];
                if (signal.Frame == frame || signal.Frame + 1u >= frame)
                    ApplyHashDelta(vault, in signal);
            }
        }

        private bool ApplyHashDelta(IDataVault vault, in HashDeltaUpdateSignal signal)
        {
            if (!TryResolveRequired(vault, in _statesHandle, MaxTargets, out NativeArray<ValidationStateDTO> states) ||
                !TryResolveRequired(vault, in _targetsHandle, MaxTargets, out NativeArray<MemorySentinelTargetDTO> targets) ||
                !TryResolveRequired(vault, in _rollbackHandle, RollbackByteCapacity, out NativeArray<byte> rollback))
            {
                return false;
            }

            int count = math.min(_targetCount, math.min(states.Length, targets.Length));
            for (int i = 0; i < count; i++)
            {
                MemorySentinelTargetDTO target = targets[i];
                if (signal.TargetHash != 0u && signal.TargetHash != target.TargetHash)
                    continue;
                if (signal.TargetHash == 0u && signal.BufferId != target.BufferId)
                    continue;

                ValidationStateDTO state = states[i];
                uint expected = signal.ExpectedHash;
                if (expected == 0u && target.TargetMemoryPointer != 0UL && target.ByteLength > 0)
                    expected = MemorySentinelMath.ComputeXXHash3Folded((void*)target.TargetMemoryPointer, target.ByteLength);

                state.ExpectedHash = expected;
                state.StoredHash = expected;
                state.TargetMemoryPointer = target.TargetMemoryPointer;
                target.LastLegalFrame = signal.Frame;
                states[i] = state;
                targets[i] = target;
                CopyTargetToRollback(rollback, in target);
                return true;
            }

            return false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static double3 ResolvePlayerAbsolute(in LockstepPlayerKinematicState state)
        {
            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            return new double3(
                (state.SectorX * cellSize) + state.LocalPosition.x,
                (state.SectorY * cellSize) + state.LocalPosition.y,
                (state.SectorZ * cellSize) + state.LocalPosition.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WritePlayerAbsolute(ref LockstepPlayerKinematicState state, double3 absolute)
        {
            const double cellSize = HectonPhysicsContract.AupSectorSizeMetersDouble;
            double3 sector = math.floor(absolute / cellSize);
            state.SectorX = (long)sector.x;
            state.SectorY = (long)sector.y;
            state.SectorZ = (long)sector.z;
            state.LocalPosition = new float3(
                (float)(absolute.x - (state.SectorX * cellSize)),
                (float)(absolute.y - (state.SectorY * cellSize)),
                (float)(absolute.z - (state.SectorZ * cellSize)));
        }

        private void CheckAupTeleport(IDataVault vault, uint frame, float deltaSeconds, float quality)
        {
            if (!vault.TryGetGenerationHandle<LockstepPlayerKinematicState>(BufferID.PlayerKinematicState, out VaultGenerationHandle<LockstepPlayerKinematicState> handle) ||
                !TryResolveRequired(vault, in handle, 1, out NativeArray<LockstepPlayerKinematicState> playerStates))
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(playerStates);
            if (ptr == null)
                return;

            if (!TryResolveRequired(vault, in _runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray) ||
                !TryResolveRequired(vault, in _aupSnapshotHandle, AupSnapshotCount, out NativeArray<MemorySentinelAupSnapshotDTO> snapshotArray))
            {
                return;
            }

            MemorySentinelRuntimeStateDTO runtime = OpenRuntimeStateForOwner(runtimeArray);
            ref LockstepPlayerKinematicState player = ref UnsafeUtility.AsRef<LockstepPlayerKinematicState>(ptr);
            double3 current = ResolvePlayerAbsolute(in player);
            MemorySentinelAupSnapshotDTO snapshot = snapshotArray[0];
            if (!math.all(math.isfinite(current)))
            {
                WritePlayerAbsolute(ref player, snapshot.GlobalPosition);
                PublishTeleportDesync(BufferID.PlayerKinematicState, frame, quality, fatal: true);
                runtime.Flags |= TelemetryFlagTeleport | TelemetryFlagFatal;
                runtimeArray[0] = runtime;
                DumpBlackBox(vault);
                throw new FatalArchitectureException("SHINOBU_73 detected non-finite player AUP.");
            }

            if ((snapshot.Flags & 1u) == 0u)
            {
                snapshot.GlobalPosition = current;
                snapshot.Frame = frame;
                snapshot.Flags = 1u;
                snapshot.MaxMetersPerSecond = math.max(1f, runtime.AupTeleportToleranceMeters / math.max(0.016f, deltaSeconds));
                snapshotArray[0] = snapshot;
                return;
            }

            double3 delta = current - snapshot.GlobalPosition;
            double deltaMetersDouble = math.sqrt(math.lengthsq(delta));
            float deltaMeters = (float)math.min(deltaMetersDouble, 1.0e9d);
            float requiredSpeed = deltaMeters / math.max(0.0001f, deltaSeconds);
            float strictness = math.max(0.1f, runtime.Strictness01);
            float maxTolerance = math.max(10f, runtime.AupTeleportToleranceMeters);
            float maxSpeed = math.max(250f, maxTolerance / math.max(0.016f, deltaSeconds)) * strictness;
            bool authorizedShift = HasAupShiftSignal(frame) || HasTransportToleranceSignal(frame);
            bool teleport = (deltaMetersDouble > MaxNonShiftAupDeltaMeters || deltaMeters > maxTolerance || requiredSpeed > maxSpeed) && !authorizedShift;
            if (teleport)
            {
                WritePlayerAbsolute(ref player, snapshot.GlobalPosition);
                snapshot.LastDeltaMeters = deltaMeters;
                snapshot.LastRequiredSpeedMetersPerSecond = requiredSpeed;
                snapshotArray[0] = snapshot;
                PublishTeleportDesync(BufferID.PlayerKinematicState, frame, quality, fatal: false);
                RecordTelemetry(vault, frame, 0u, 1u, 1u, 0u, TelemetryFlagTeleport | TelemetryFlagRollback, 0f);
                return;
            }

            snapshot.GlobalPosition = current;
            snapshot.Frame = frame;
            snapshot.LastDeltaMeters = deltaMeters;
            snapshot.LastRequiredSpeedMetersPerSecond = requiredSpeed;
            snapshot.MaxMetersPerSecond = maxSpeed;
            snapshotArray[0] = snapshot;
        }

        private static bool HasAupShiftSignal(uint frame)
        {
            ReadOnlySpan<AupShiftSignal> shifts = SignalBus<AupShiftSignal>.GetFrameSnapshot();
            for (int i = 0; i < shifts.Length; i++)
            {
                uint shiftFrame = shifts[i].ShiftFrameId;
                if (shiftFrame == frame || shiftFrame + 1u == frame)
                    return true;
            }

            return false;
        }

        private static bool HasTransportToleranceSignal(uint frame)
        {
            ReadOnlySpan<PlayerTransportBailoutSignal> transports = SignalBus<PlayerTransportBailoutSignal>.GetFrameSnapshot();
            for (int i = 0; i < transports.Length; i++)
            {
                uint signalFrame = transports[i].Frame;
                if (signalFrame == frame || signalFrame + 1u == frame)
                    return true;
            }

            return false;
        }

        private void PublishDesync(
            in MemorySentinelTargetDTO target,
            in MemorySentinelResultDTO result,
            bool corrected,
            bool fatal,
            bool teleport)
        {
            MemoryDesyncSignal signal = default;
            signal.TargetHash = target.TargetHash;
            signal.ExpectedHash = result.ExpectedHash;
            signal.CalculatedHash = result.CalculatedHash;
            signal.StoredHash = result.StoredHash;
            signal.Frame = result.Frame;
            signal.BufferId = target.BufferId;
            signal.ByteLength = target.ByteLength;
            signal.TargetMemoryFingerprint = target.TargetMemoryFingerprint;
            signal.FullHash64 = result.FullHash64;
            signal.Severity01 = math.saturate(target.Criticality01);
            signal.GlobalQualityWeight = result.GlobalQualityWeight;
            signal.Flags = 0u;
            if (corrected)
                signal.Flags |= MemoryDesyncSignal.FlagRollbackApplied;
            if (fatal)
                signal.Flags |= MemoryDesyncSignal.FlagFatal;
            if (teleport)
                signal.Flags |= MemoryDesyncSignal.FlagTeleport;
            if ((target.Flags & MemorySentinelConstants.TargetFlagCritical) != 0u)
                signal.Flags |= MemoryDesyncSignal.FlagCritical;
            if ((result.Flags & (
                    MemorySentinelConstants.ResultFlagPointerMismatch |
                    MemorySentinelConstants.ResultFlagPointerFingerprintMismatch |
                    MemorySentinelConstants.ResultFlagInvalidPointer)) != 0u)
            {
                signal.Flags |= MemoryDesyncSignal.FlagPointerMismatch;
            }

            SignalBus<MemoryDesyncSignal>.TryPushTracked(in signal, ref s_x001MemorySentinelRuntimeSignalPushDropCount);
        }

        private static void PublishRollback(in MemorySentinelTargetDTO target, in ValidationStateDTO state, uint frame)
        {
            MemorySentinelRollbackSignal signal = default;
            signal.TargetHash = target.TargetHash;
            signal.Frame = frame;
            signal.ExpectedHash = state.ExpectedHash;
            signal.CorrectedHash = state.ExpectedHash;
            signal.BufferId = target.BufferId;
            signal.ByteLength = target.ByteLength;
            signal.RollbackByteOffset = target.RollbackByteOffset;
            signal.TargetMemoryFingerprint = target.TargetMemoryFingerprint;
            signal.Flags = target.Flags;
            SignalBus<MemorySentinelRollbackSignal>.TryPushTracked(in signal, ref s_x001MemorySentinelRuntimeSignalPushDropCount);
        }

        private static void PublishTeleportDesync(BufferID bufferId, uint frame, float quality, bool fatal)
        {
            MemoryDesyncSignal signal = default;
            signal.TargetHash = MemorySentinelMath.ComputeTargetHash((int)bufferId, 0);
            signal.Frame = frame;
            signal.BufferId = (int)bufferId;
            signal.Flags = MemoryDesyncSignal.FlagTeleport | MemoryDesyncSignal.FlagRollbackApplied;
            if (fatal)
                signal.Flags |= MemoryDesyncSignal.FlagFatal;
            signal.Severity01 = fatal ? 1f : 0.95f;
            signal.GlobalQualityWeight = quality;
            SignalBus<MemoryDesyncSignal>.TryPushTracked(in signal, ref s_x001MemorySentinelRuntimeSignalPushDropCount);
        }

        private void RecordTelemetry(
            IDataVault vault,
            uint frame,
            uint bytesHashed,
            uint corrected,
            uint detected,
            uint fatal,
            uint flags,
            float elapsedMs)
        {
            MemorySentinelTargetDTO target = default;
            MemorySentinelResultDTO result = default;
            RecordTelemetry(vault, frame, bytesHashed, corrected, detected, fatal, flags, elapsedMs, in target, in result, 0u);
        }

        private void RecordTelemetry(
            IDataVault vault,
            uint frame,
            uint bytesHashed,
            uint corrected,
            uint detected,
            uint fatal,
            uint flags,
            float elapsedMs,
            in MemorySentinelTargetDTO lastTarget,
            in MemorySentinelResultDTO lastResult,
            uint rollbackBytes)
        {
            if (!TryResolveRequired(vault, in _telemetryHandle, MemorySentinelConstants.TelemetryCapacity, out NativeArray<MemorySentinelTelemetryEntry> telemetry))
                return;

            int index = (int)(frame % (uint)telemetry.Length);
            _lastTelemetryIndex = index;
            telemetry[index] = new MemorySentinelTelemetryEntry
            {
                Frame = frame,
                BytesHashedPerFrame = bytesHashed,
                DesyncsCorrected = corrected,
                DesyncsDetected = detected,
                ValidationComputeTimeMs = elapsedMs,
                GlobalQualityWeight = HomeostasisBrain.GlobalQualityWeight,
                Flags = flags | TelemetryFlagExternalCompileWallSafe,
                TargetCount = (uint)math.max(0, _targetCount),
                FatalCount = fatal,
                RollbackBytes = rollbackBytes,
                LastTargetHash = lastTarget.TargetHash,
                LastExpectedHash = lastResult.ExpectedHash,
                LastCalculatedHash = lastResult.CalculatedHash,
                ValidationCadenceFrames = ResolveTelemetryCadence(vault)
            };
        }

        private uint ResolveTelemetryCadence(IDataVault vault)
        {
            if (!TryReadRequired(vault, in _runtimeStateHandle, RuntimeStateCount, out NativeArray<MemorySentinelRuntimeStateDTO>.ReadOnly runtimeArray))
                return 0u;

            return (uint)math.max(0, runtimeArray[0].ValidationCadenceFrames);
        }

        private void DumpBlackBox(IDataVault vault)
        {
            if (!TryReadRequired(vault, in _telemetryHandle, MemorySentinelConstants.TelemetryCapacity, out NativeArray<MemorySentinelTelemetryEntry>.ReadOnly telemetry))
                return;

            int headerSize = UnsafeUtility.SizeOf<MemorySentinelDumpHeader>();
            int entrySize = UnsafeUtility.SizeOf<MemorySentinelTelemetryEntry>();
            int byteCount = headerSize + (telemetry.Length * entrySize);
            NativeArray<byte> payload = NativeFaultDumpWriter.CreateTransientPayload(
                byteCount,
                nameof(MemorySentinelRuntime),
                DumpPayloadLabel,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                MemorySentinelDumpHeader header = default;
                header.Magic = DumpMagic;
                header.Version = DumpVersion;
                header.EntrySize = (uint)entrySize;
                header.Capacity = (uint)telemetry.Length;
                header.LastIndex = (uint)math.max(0, _lastTelemetryIndex);
                header.Flags = _lastTelemetryFlags;
                header.LastBytesHashed = _lastBytesHashed;

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                if (!UnsafeMemoryCopyGuard.SafeCopy(destination, byteCount, &header, headerSize))
                    return;

                int writeCursor = headerSize;
                byte* source = (byte*)telemetry.GetUnsafeReadOnlyPtr();
                int start = (_lastTelemetryIndex + 1) % telemetry.Length;
                for (int offset = 0; offset < telemetry.Length; offset++)
                {
                    int index = start + offset;
                    if (index >= telemetry.Length)
                        index -= telemetry.Length;

                    if (!UnsafeMemoryCopyGuard.SafeCopy(
                            destination + writeCursor,
                            byteCount - writeCursor,
                            source + (index * entrySize),
                            entrySize))
                    {
                        return;
                    }

                    writeCursor += entrySize;
                }

                NativeFaultDumpWriter.TryWriteAll(DumpPath, payload, writeCursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(ref payload, nameof(MemorySentinelRuntime), DumpPayloadLabel);
            }
        }

        private bool SimulateCheatEngineWriteInternal()
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultBuffers(vault))
                return false;

            ForceCompleteValidationJobInPostSimulationWindow();
            if (!TryResolveRequired(vault, in _mockInventoryHandle, MockInventoryCount, out NativeArray<MockInventorySpan> mock))
                return false;

            MockInventoryByteMutationJob mutationJob = new MockInventoryByteMutationJob
            {
                MockInventory = mock,
                Frame = Hecton8.Core.SystemDispatcher.CurrentFrameId,
                MutationByteCount = 4
            };
            mutationJob.Execute();

            _forceValidationNextFrame = true;
            _lastTelemetryFlags |= TelemetryFlagMockMutation;
            return true;
        }

#if UNITY_EDITOR
        private bool LoadValidationRulesCsvInternal()
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !EnsureVaultBuffers(vault))
                return false;

            string path = FindValidationRulesCsvPathCold();
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return false;

            int bytesRead;
            Span<byte> scratch = stackalloc byte[CsvScratchCapacity];
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                int readLength = Math.Min(scratch.Length, (int)Math.Min(stream.Length, scratch.Length));
                bytesRead = stream.Read(scratch.Slice(0, readLength));
            }

            bool parsed = ParseCsvBytes(vault, scratch.Slice(0, bytesRead));
            if (parsed)
                _lastTelemetryFlags |= TelemetryFlagCsvLoaded;
            return parsed;
        }

        private static string FindValidationRulesCsvPathCold()
        {
            string projectRoot = Directory.GetCurrentDirectory();
            string rootPath = Path.Combine(projectRoot, CsvRootPath);
            if (File.Exists(rootPath))
                return rootPath;

            string docsPath = Path.Combine(projectRoot, CsvDocsPath);
            return File.Exists(docsPath) ? docsPath : rootPath;
        }

        private bool ParseCsvBytes(IDataVault vault, ReadOnlySpan<byte> bytes)
        {
            if (bytes.Length <= 0 ||
                vault == null ||
                !vault.TryAcquireWriteLock(in _runtimeStateHandle, OwnerSystemId, out NativeArray<MemorySentinelRuntimeStateDTO> runtimeArray))
                return false;

            try
            {
                if (!runtimeArray.IsCreated || runtimeArray.Length < RuntimeStateCount)
                    return false;

                MemorySentinelRuntimeStateDTO runtime = OpenRuntimeStateForOwner(runtimeArray);
                int byteLength = bytes.Length;
                bool changed = false;
                fixed (byte* csvBytes = bytes)
                {
                    int lineStart = 0;
                    for (int i = 0; i <= byteLength; i++)
                    {
                        if (i < byteLength && csvBytes[i] != '\n')
                            continue;

                        int lineEnd = i;
                        if (lineEnd > lineStart && csvBytes[lineEnd - 1] == '\r')
                            lineEnd--;

                        changed |= ParseCsvLine(csvBytes, lineStart, lineEnd, ref runtime);
                        lineStart = i + 1;
                    }
                }

                if (changed)
                {
                    runtimeArray[0] = runtime;
                    _forceValidationNextFrame = true;
                }

                return changed;
            }
            finally
            {
                vault.ReleaseWriteLock(in _runtimeStateHandle, OwnerSystemId);
            }
        }

        private static bool ParseCsvLine(byte* bytes, int lineStart, int lineEnd, ref MemorySentinelRuntimeStateDTO runtime)
        {
            int cursor = SkipWhitespace(bytes, lineStart, lineEnd);
            if (cursor >= lineEnd || bytes[cursor] == '#')
                return false;

            int keyStart = cursor;
            while (cursor < lineEnd && bytes[cursor] != ',' && bytes[cursor] != '=')
                cursor++;

            int keyEnd = TrimEnd(bytes, keyStart, cursor);
            if (cursor >= lineEnd)
                return false;

            cursor++;
            int valueStart = SkipWhitespace(bytes, cursor, lineEnd);
            int valueEnd = TrimEnd(bytes, valueStart, lineEnd);
            uint keyHash = HashAsciiLower(bytes, keyStart, keyEnd);

            if (keyHash == HashValidationFrequency || keyHash == HashValidationFrequencyHz)
            {
                if (!TryParseFloat(bytes, valueStart, valueEnd, out float hz))
                    return false;
                runtime.ValidationFrequencyHz = math.clamp(hz, MinimumValidationFrequencyHz, DefaultValidationFrequencyHz);
                return true;
            }

            if (keyHash == HashAupTeleportTolerance || keyHash == HashAupTeleportToleranceMeters)
            {
                if (!TryParseFloat(bytes, valueStart, valueEnd, out float meters))
                    return false;
                runtime.AupTeleportToleranceMeters = math.max(10f, meters);
                return true;
            }

            if (keyHash == HashStrictness || keyHash == HashStrictnessLevel)
            {
                if (!TryParseFloat(bytes, valueStart, valueEnd, out float strictness))
                    return false;
                runtime.Strictness01 = math.saturate(strictness);
                return true;
            }

            if (keyHash == HashGlobalQualityWeight)
            {
                if (!TryParseFloat(bytes, valueStart, valueEnd, out float weight))
                    return false;
                runtime.GlobalQualityWeightOverride = math.saturate(weight);
                return true;
            }

            if (keyHash == HashModdedGameMask)
            {
                if (!TryParseUInt(bytes, valueStart, valueEnd, out uint mask))
                    return false;
                runtime.ModdedGameMask = mask;
                return true;
            }

            return false;
        }

        private static int SkipWhitespace(byte* bytes, int start, int end)
        {
            int cursor = start;
            while (cursor < end && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
            return cursor;
        }

        private static int TrimEnd(byte* bytes, int start, int end)
        {
            int cursor = end;
            while (cursor > start && (bytes[cursor - 1] == ' ' || bytes[cursor - 1] == '\t'))
                cursor--;
            return cursor;
        }

        private static uint HashAsciiLower(byte* bytes, int start, int end)
        {
            unchecked
            {
                uint hash = 2166136261u;
                for (int i = start; i < end; i++)
                {
                    byte c = bytes[i];
                    if (c >= 'A' && c <= 'Z')
                        c = (byte)(c + 32);
                    hash = (hash ^ c) * 16777619u;
                }

                return hash;
            }
        }

        private static bool TryParseFloat(byte* bytes, int start, int end, out float value)
        {
            value = 0f;
            if (start >= end)
                return false;

            int cursor = start;
            float sign = 1f;
            if (bytes[cursor] == '-')
            {
                sign = -1f;
                cursor++;
            }

            float whole = 0f;
            bool any = false;
            while (cursor < end && bytes[cursor] >= '0' && bytes[cursor] <= '9')
            {
                whole = whole * 10f + (bytes[cursor] - '0');
                cursor++;
                any = true;
            }

            float fraction = 0f;
            float scale = 0.1f;
            if (cursor < end && bytes[cursor] == '.')
            {
                cursor++;
                while (cursor < end && bytes[cursor] >= '0' && bytes[cursor] <= '9')
                {
                    fraction += (bytes[cursor] - '0') * scale;
                    scale *= 0.1f;
                    cursor++;
                    any = true;
                }
            }

            value = (whole + fraction) * sign;
            return any;
        }

        private static bool TryParseUInt(byte* bytes, int start, int end, out uint value)
        {
            value = 0u;
            if (start >= end)
                return false;

            int cursor = start;
            bool hex = end - start > 2 && bytes[start] == '0' && (bytes[start + 1] == 'x' || bytes[start + 1] == 'X');
            if (hex)
                cursor += 2;

            bool any = false;
            while (cursor < end)
            {
                byte c = bytes[cursor++];
                uint digit;
                if (c >= '0' && c <= '9')
                    digit = (uint)(c - '0');
                else if (hex && c >= 'a' && c <= 'f')
                    digit = (uint)(c - 'a' + 10);
                else if (hex && c >= 'A' && c <= 'F')
                    digit = (uint)(c - 'A' + 10);
                else
                    return false;

                value = hex ? (value << 4) | digit : value * 10u + digit;
                any = true;
            }

            return any;
        }
#endif

        private static float ResolveElapsedMs(long startTimestamp)
        {
            if (startTimestamp <= 0L)
                return 0f;

            long ticks = Stopwatch.GetTimestamp() - startTimestamp;
            return (float)(ticks * 1000.0 / Stopwatch.Frequency);
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem, IDispatcherFenceDomainProvider
        {
            private readonly MemorySentinelRuntime _owner;

            public SimulationPhaseSystem(MemorySentinelRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => SystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.Simulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public DispatcherFenceDomain GetFenceDomain() => DispatcherFenceDomain.Simulation;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return _owner != null
                    ? _owner.ScheduleSimulation(in timing, in context, dependsOn)
                    : dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly MemorySentinelRuntime _owner;

            public PostSimulationPhaseSystem(MemorySentinelRuntime owner)
            {
                _owner = owner;
            }

            public uint GetSystemIdHash() => PostSimulationSystemHash;

            public DispatcherPhase GetDispatcherPhase() => DispatcherPhase.PostSimulation;

            public byte GetBucketId() => byte.MaxValue;

            public int GetDependencyCount() => 0;

            public uint GetDependencyHash(int dependencyIndex) => 0u;

            public void PreSimulationTick(in DispatcherTimingDTO timing)
            {
            }

            public JobHandle ScheduleSimulation(
                in DispatcherTimingDTO timing,
                in DispatcherJobContext context,
                JobHandle dependsOn)
            {
                return dependsOn;
            }

            public void PostSimulationTick(in DispatcherTimingDTO timing)
            {
                _owner?.PostSimulationTick(in timing);
            }

            public void VisualSyncTick(in DispatcherTimingDTO timing)
            {
            }
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct MemorySentinelDumpHeader
        {
            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public uint Version;
            [FieldOffset(8)] public uint EntrySize;
            [FieldOffset(12)] public uint Capacity;
            [FieldOffset(16)] public uint LastIndex;
            [FieldOffset(20)] public uint Flags;
            [FieldOffset(24)] public uint LastBytesHashed;
            [FieldOffset(28)] public uint _pad0;
        }
    }
}
