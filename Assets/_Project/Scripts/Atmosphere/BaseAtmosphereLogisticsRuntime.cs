// ============================================================================
// HECTON-8 - BaseAtmosphereLogisticsRuntime.cs
// Vault-backed dispatcher runtime for base 3D CSR gas diffusion.
// ============================================================================

using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Atmosphere
{
    public sealed unsafe class BaseAtmosphereLogisticsRuntime : IGlobalRegistryHotSwapListener, IColdTickable
    {
        private const uint SystemHash = 0x53483232u; // SH22
        private const SystemID OwnerSystemId = SystemID.HabitatAtmosphere;
#if UNITY_EDITOR
        private const string CsvRelativePath = "Docs/Atmosphere/gas_diffusion_profiles.csv";
        private const int CsvPollCadenceFrames = 128;
#endif
        private const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_221.bin";
        private const string DumpPayloadLabel = "baseAtmosphereLogisticsTelemetryDumpPayload";
        private const float AuthoritativeQualityWeight = 1f;
        private const int MinQualityDiffusionIterations = 2;
        private const int AuthoritativeDiffusionIterations = 8;
        private const int MaxQualityDiffusionIterations = AuthoritativeDiffusionIterations;
        private static readonly ulong AtmosphereFrameMutationGuardMask =
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.CellsFront) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.CellsBack) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.EdgeOffsets) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.EdgeDestinations) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.EdgeConductance) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.Counters) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.TelemetryRing) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.OxygenDeltaUnits) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.NitrogenDeltaUnits) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.ToxinDeltaUnits) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.TemperatureDeltaMilli) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.GasRemainders) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.ShaderPayload) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.Nodes) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.Consumers) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.ToxicSources) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.Vents) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.Tuning);
#if UNITY_EDITOR
        private static readonly ulong ProfileCsvMutationGuardMask =
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.Profiles) |
            AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.Tuning);
#endif
        private const uint AtmosphereJobPinCellsFront = 1u << 0;
        private const uint AtmosphereJobPinCellsBack = 1u << 1;
        private const uint AtmosphereJobPinNodes = 1u << 2;
        private const uint AtmosphereJobPinEdgeOffsets = 1u << 3;
        private const uint AtmosphereJobPinEdgeDestinations = 1u << 4;
        private const uint AtmosphereJobPinEdgeConductance = 1u << 5;
        private const uint AtmosphereJobPinConsumers = 1u << 6;
        private const uint AtmosphereJobPinToxicSources = 1u << 7;
        private const uint AtmosphereJobPinVents = 1u << 8;
        private const uint AtmosphereJobPinCounters = 1u << 9;
        private const uint AtmosphereJobPinTuning = 1u << 10;
        private const uint AtmosphereJobPinTelemetryRing = 1u << 11;
        private const uint AtmosphereJobPinOxygenDelta = 1u << 12;
        private const uint AtmosphereJobPinCarbonDioxideDelta = 1u << 13;
        private const uint AtmosphereJobPinNitrogenDelta = 1u << 14;
        private const uint AtmosphereJobPinToxinDelta = 1u << 15;
        private const uint AtmosphereJobPinTemperatureDelta = 1u << 16;
        private const uint AtmosphereJobPinGasRemainders = 1u << 17;
        private const uint AtmosphereJobPinShaderPayload = 1u << 18;

        private static readonly int _GasScalarsShaderId = Shader.PropertyToID("_H8BaseAtmosphereGasScalars");
        private static readonly int _GasQualityShaderId = Shader.PropertyToID("_H8BaseAtmosphereQualityWeight");

        private static BaseAtmosphereLogisticsRuntime s_active;
        private static float s_pendingBaseDiffusionRate = 0.35f;
        private static float s_pendingInhalationMultiplier = 1.0f;
        private static float s_pendingToxinDissipationSpeed = 0.005f;

#if UNITY_EDITOR
        private readonly string _csvPath;
#endif
        private readonly PreSimulationPhaseSystem _preSimulationPhase;
        private readonly SimulationPhaseSystem _simulationPhase;
        private readonly PostSimulationPhaseSystem _postSimulationPhase;
        private readonly VisualSyncPhaseSystem _visualSyncPhase;
        private readonly AtmosphereTelemetryEntry[] _dumpSnapshot = new AtmosphereTelemetryEntry[AtmosphereLogisticsConstants.TelemetryRingCapacity];

        private IDataVault _vault;
        private IDataVault _pendingVault;
        private IDataVault _jobPinVault;
        private bool _shutdown;
        private bool _registeredHotSwap;
        private bool _hasPendingVaultRebind;
        private bool _registeredPreSimulation;
        private bool _registeredSimulation;
        private bool _registeredPostSimulation;
        private bool _registeredVisualSync;
        private bool _registeredColdTick;
        private bool _vaultInitialized;
        private bool _layoutChecked;
        private bool _layoutValid;
        private bool _defaultsInitialized;
        private bool _simulationScheduled;
        private bool _vaultRepairRequested;
        private bool _jobBuffersPinned;
        private uint _jobBufferPinMask;
        private bool _dumpWrittenThisFault;
        private uint _lastDispatcherFrame;
        private uint _lastTelemetryHash;
        private long _jobScheduleTimestamp;
        private JobHandle _simulationHandle;
        private float _lastAverageOxygen01 = AtmosphereLogisticsConstants.DefaultOxygen01;
        private float _lastMaxCarbonDioxide01 = AtmosphereLogisticsConstants.DefaultCarbonDioxide01;
        private float _lastMaxToxin01;
        private float _smoothedQualityWeight01 = 1f;
        private int _lastNodeCount;
        private int _lastIterations;
        private int _lastMicros;
        private int _dumpSnapshotCount;
#if UNITY_EDITOR
        private DateTime _csvLastWriteUtc;
#endif

        private VaultGenerationHandle<AtmosphereCellDTO> _frontCells;
        private VaultGenerationHandle<AtmosphereCellDTO> _backCells;
        private VaultGenerationHandle<AtmosphereNodeDTO> _nodes;
        private VaultGenerationHandle<AtmosphereConnectionDTO> _connections;
        private VaultGenerationHandle<int> _edgeOffsets;
        private VaultGenerationHandle<int> _edgeDestinations;
        private VaultGenerationHandle<float> _edgeConductance;
        private VaultGenerationHandle<int> _edgeWriteCursor;
        private VaultGenerationHandle<AtmosphereConsumerDTO> _consumers;
        private VaultGenerationHandle<AtmosphereToxicSourceDTO> _sources;
        private VaultGenerationHandle<AtmosphereVentDTO> _vents;
        private VaultGenerationHandle<AtmosphereGraphCountersDTO> _counters;
        private VaultGenerationHandle<AtmosphereTuningDTO> _tuning;
        private VaultGenerationHandle<AtmosphereTelemetryEntry> _telemetry;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _oxygenDeltaUnits;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _carbonDioxideDeltaUnits;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _nitrogenDeltaUnits;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _toxinDeltaUnits;
        private VaultGenerationHandle<AtmosphereDeltaLane64> _temperatureDeltaMilli;
        private VaultGenerationHandle<AtmosphereGasRemainderDTO> _remainders;
        private VaultGenerationHandle<AtmosphereShaderPayloadDTO> _shaderPayload;
#if UNITY_EDITOR
        private VaultGenerationHandle<AtmosphereGasProfileDTO> _profiles;
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            s_active = null;
            s_pendingBaseDiffusionRate = 0.35f;
            s_pendingInhalationMultiplier = 1.0f;
            s_pendingToxinDissipationSpeed = 0.005f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (!Application.isPlaying || s_active != null)
                return;

            BaseAtmosphereLogisticsRuntime runtime = new BaseAtmosphereLogisticsRuntime();
            s_active = runtime;
            runtime.Initialize();
        }

        private static void ShutdownActive()
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active != null)
                active.Shutdown();
        }

        private BaseAtmosphereLogisticsRuntime()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
#if UNITY_EDITOR
            _csvPath = Path.GetFullPath(Path.Combine(projectRoot, CsvRelativePath));
#endif
            _preSimulationPhase = new PreSimulationPhaseSystem(this);
            _simulationPhase = new SimulationPhaseSystem(this);
            _postSimulationPhase = new PostSimulationPhaseSystem(this);
            _visualSyncPhase = new VisualSyncPhaseSystem(this);
        }

        public static bool TryGetGlobalOxygenSnapshot(out float reserve, out float capacity, out float normalized)
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._lastNodeCount <= 0)
            {
                reserve = 0f;
                capacity = 0f;
                normalized = 1f;
                return false;
            }

            capacity = AtmosphereLogisticsConstants.DefaultOxygen01 * active._lastNodeCount;
            reserve = math.saturate(active._lastAverageOxygen01) * active._lastNodeCount;
            normalized = capacity > 0.0001f ? math.saturate(reserve / capacity) : 1f;
            return true;
        }

        public static void SetEditorTuning(float diffusionRate, float inhalationMultiplier, float toxinDissipationSpeed)
        {
            s_pendingBaseDiffusionRate = math.clamp(FiniteOr(diffusionRate, 0.35f), 0f, 4f);
            s_pendingInhalationMultiplier = math.clamp(FiniteOr(inhalationMultiplier, 1f), 0f, 4f);
            s_pendingToxinDissipationSpeed = math.clamp(FiniteOr(toxinDissipationSpeed, 0.005f), 0f, 1f);

            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._vault == null || active._simulationScheduled)
            {
                return;
            }

            IDataVault vault = active.ResolveVault();
            ulong tuningGuardMask = AtmosphereLogisticsMutationGuardBit(AtmosphereLogisticsBufferIds.Tuning);
            if (vault == null || vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(tuningGuardMask))
                return;

            try
            {
                if (!active.Resolve(in active._tuning, AtmosphereLogisticsBufferIds.Tuning, out NativeArray<AtmosphereTuningDTO> tuningBuffer) ||
                    tuningBuffer.Length == 0)
                {
                    return;
                }

                ref AtmosphereTuningDTO tuning = ref UnsafeUtility.AsRef<AtmosphereTuningDTO>(NativeArrayUnsafeUtility.GetUnsafePtr(tuningBuffer));
                tuning.BaseDiffusionRate = s_pendingBaseDiffusionRate;
                tuning.InhalationMultiplier = s_pendingInhalationMultiplier;
                tuning.ToxinDissipationSpeed = s_pendingToxinDissipationSpeed;
            }
            finally
            {
                vault.ReleaseMutationGuard(tuningGuardMask);
            }
        }

        public static bool TryGetEditorTuning(out AtmosphereTuningDTO tuning)
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._vault == null || active._simulationScheduled ||
                !active.ResolveReadOnly(in active._tuning, AtmosphereLogisticsBufferIds.Tuning, out NativeArray<AtmosphereTuningDTO>.ReadOnly tuningBuffer) ||
                tuningBuffer.Length == 0)
            {
                tuning = default;
                return false;
            }

            tuning = tuningBuffer[0];
            return true;
        }

        public static bool TryGetLatestTelemetry(out AtmosphereTelemetryEntry entry)
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._vault == null || active._simulationScheduled ||
                !active.ResolveReadOnly(in active._telemetry, AtmosphereLogisticsBufferIds.TelemetryRing, out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetry) ||
                !active.ResolveReadOnly(in active._counters, AtmosphereLogisticsBufferIds.Counters, out NativeArray<AtmosphereGraphCountersDTO>.ReadOnly counters) ||
                telemetry.Length == 0 || counters.Length == 0)
            {
                entry = default;
                return false;
            }

            int cursor = counters[0].TelemetryCursor - 1;
            int index = cursor % telemetry.Length;
            if (index < 0)
                index += telemetry.Length;
            entry = telemetry[index];
            return entry.NodeCount > 0;
        }

#if UNITY_EDITOR
        public static bool TryGetTelemetryReadOnly(out NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetry, out int cursor)
        {
            telemetry = default;
            cursor = 0;
            BaseAtmosphereLogisticsRuntime active = s_active;
            NativeArray<AtmosphereTelemetryEntry>.ReadOnly telemetryBuffer;
            if (active == null || active._vault == null || active._simulationScheduled ||
                !active.ResolveReadOnly(in active._telemetry, AtmosphereLogisticsBufferIds.TelemetryRing, out telemetryBuffer) ||
                !active.ResolveReadOnly(in active._counters, AtmosphereLogisticsBufferIds.Counters, out NativeArray<AtmosphereGraphCountersDTO>.ReadOnly counters) ||
                telemetryBuffer.Length == 0 || counters.Length == 0)
            {
                return false;
            }

            telemetry = telemetryBuffer;
            cursor = counters[0].TelemetryCursor;
            return true;
        }
#endif

        public static bool TryGetGizmoCell(int index, out AtmosphereNodeDTO node, out AtmosphereCellDTO cell, out int nodeCount)
        {
            BaseAtmosphereLogisticsRuntime active = s_active;
            if (active == null || active._vault == null || active._simulationScheduled ||
                !active.ResolveReadOnly(in active._nodes, AtmosphereLogisticsBufferIds.Nodes, out NativeArray<AtmosphereNodeDTO>.ReadOnly nodes) ||
                !active.ResolveReadOnly(in active._frontCells, AtmosphereLogisticsBufferIds.CellsFront, out NativeArray<AtmosphereCellDTO>.ReadOnly cells) ||
                !active.ResolveReadOnly(in active._counters, AtmosphereLogisticsBufferIds.Counters, out NativeArray<AtmosphereGraphCountersDTO>.ReadOnly counters) ||
                counters.Length == 0)
            {
                node = default;
                cell = default;
                nodeCount = 0;
                return false;
            }

            nodeCount = math.clamp(counters[0].NodeCount, 0, math.min(nodes.Length, cells.Length));
            if ((uint)index >= (uint)nodeCount)
            {
                node = default;
                cell = default;
                return false;
            }

            node = nodes[index];
            cell = cells[index];
            return true;
        }

        private void Initialize()
        {
            _shutdown = false;
            ApplyVaultRebind(GlobalRegistry.DataVault);
            TryRegisterHotSwapListener();
            SignalBus<FluidIncursionSignal>.EnsureInitialized();
            SignalBus<PlayerBaseEnterSignal>.EnsureInitialized();
            SignalBus<PlayerBaseExitSignal>.EnsureInitialized();
            SignalBus<ReactorDamageSignal>.Configure(
                ReactorDamageSignal.ExpectedCapacity,
                maxFrameSignals: ReactorDamageSignal.MaxFrameSignals,
                lowTierFrameSignals: ReactorDamageSignal.LowTierFrameSignals,
                laneHash: ReactorDamageSignal.LaneHash);
            SignalBus<ReactorDamageSignal>.EnsureInitialized();
            PrepareRuntimeStateCold();
            RegisterDispatcherPhases();
            Application.quitting -= ShutdownActive;
            Application.quitting += ShutdownActive;
        }

        private void Shutdown()
        {
            if (_shutdown)
                return;

            _shutdown = true;
            Application.quitting -= ShutdownActive;
            CompleteSimulationForLifecycle();
            ReleaseJobBufferPins();
            TryUnregisterHotSwapListener();
            UnregisterDispatcherPhases();
            ReleaseVaultHandles(_vault);
            ClearVaultHandles();
            _vault = null;
            _pendingVault = null;
            _jobPinVault = null;
            _hasPendingVaultRebind = false;
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _layoutChecked = false;
            _layoutValid = false;
            _simulationScheduled = false;
            _smoothedQualityWeight01 = 1f;
            if (ReferenceEquals(s_active, this))
                s_active = null;
        }

        private void RegisterDispatcherPhases()
        {
            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredPreSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_preSimulationPhase))
                _registeredPreSimulation = true;
            if (!_registeredSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase))
                _registeredSimulation = true;
            if (!_registeredPostSimulation && GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase))
                _registeredPostSimulation = true;
            if (!_registeredVisualSync && GlobalRegistry.TryRegisterDispatcherSystem(_visualSyncPhase))
                _registeredVisualSync = true;
            if (!_registeredColdTick && GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Environment))
                _registeredColdTick = true;
        }

        private void UnregisterDispatcherPhases()
        {
            if (_registeredColdTick)
            {
                GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Environment);
                _registeredColdTick = false;
            }

            if (_registeredPreSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_preSimulationPhase);
                _registeredPreSimulation = false;
            }

            if (_registeredSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_simulationPhase);
                _registeredSimulation = false;
            }

            if (_registeredPostSimulation)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_postSimulationPhase);
                _registeredPostSimulation = false;
            }

            if (_registeredVisualSync)
            {
                GlobalRegistry.UnregisterDispatcherSystem(_visualSyncPhase);
                _registeredVisualSync = false;
            }
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.DataVault)
            {
                IDataVault nextVault = currentService is IDataVault dataVault ? dataVault : null;
                QueueOrApplyVaultRebind(nextVault);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                UnregisterDispatcherPhases();
                if (currentService != null && !_shutdown)
                    RegisterDispatcherPhases();
            }
        }

        private IDataVault ResolveVault()
        {
            return _vault;
        }

        public void ColdTick()
        {
            if (_shutdown)
                return;

            ApplyPendingVaultRebindIfSafe();
            if (!_vaultRepairRequested && HasVaultStateReady())
                return;

            if (_simulationScheduled || _jobBuffersPinned)
                return;

            PrepareRuntimeStateCold();
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void QueueOrApplyVaultRebind(IDataVault vault)
        {
            if (_simulationScheduled || _jobBuffersPinned)
            {
                _pendingVault = vault;
                _hasPendingVaultRebind = true;
                return;
            }

            ApplyVaultRebind(vault);
        }

        private void ApplyPendingVaultRebindIfSafe()
        {
            if (!_hasPendingVaultRebind || _simulationScheduled || _jobBuffersPinned)
                return;

            ApplyVaultRebind(_pendingVault);
            _pendingVault = null;
            _hasPendingVaultRebind = false;
        }

        private void ApplyVaultRebind(IDataVault vault)
        {
            if (ReferenceEquals(_vault, vault))
                return;

            ReleaseVaultHandles(_vault);
            ClearVaultHandles();
            _vault = vault;
            _vaultInitialized = false;
            _defaultsInitialized = false;
            _layoutChecked = false;
            _layoutValid = false;
            _vaultRepairRequested = true;
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _frontCells);
            ReleaseVaultHandle(vault, ref _backCells);
            ReleaseVaultHandle(vault, ref _nodes);
            ReleaseVaultHandle(vault, ref _connections);
            ReleaseVaultHandle(vault, ref _edgeOffsets);
            ReleaseVaultHandle(vault, ref _edgeDestinations);
            ReleaseVaultHandle(vault, ref _edgeConductance);
            ReleaseVaultHandle(vault, ref _edgeWriteCursor);
            ReleaseVaultHandle(vault, ref _consumers);
            ReleaseVaultHandle(vault, ref _sources);
            ReleaseVaultHandle(vault, ref _vents);
            ReleaseVaultHandle(vault, ref _counters);
            ReleaseVaultHandle(vault, ref _tuning);
            ReleaseVaultHandle(vault, ref _telemetry);
            ReleaseVaultHandle(vault, ref _oxygenDeltaUnits);
            ReleaseVaultHandle(vault, ref _carbonDioxideDeltaUnits);
            ReleaseVaultHandle(vault, ref _nitrogenDeltaUnits);
            ReleaseVaultHandle(vault, ref _toxinDeltaUnits);
            ReleaseVaultHandle(vault, ref _temperatureDeltaMilli);
            ReleaseVaultHandle(vault, ref _remainders);
            ReleaseVaultHandle(vault, ref _shaderPayload);
#if UNITY_EDITOR
            ReleaseVaultHandle(vault, ref _profiles);
#endif
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (IsOwnedVaultHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static bool IsOwnedVaultHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return IsAtmosphereLogisticsBufferId(handle.BufferID) &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)OwnerSystemId;
        }

        private static bool IsOwnedVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == ToHandleBufferId(expectedBufferId) &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)OwnerSystemId;
        }

        private static bool IsAtmosphereLogisticsBufferId(uint bufferId)
        {
            return bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.CellsFront) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.CellsBack) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.Nodes) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.Connections) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.EdgeOffsets) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.EdgeDestinations) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.EdgeConductance) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.EdgeWriteCursor) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.Consumers) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.ToxicSources) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.Vents) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.Counters) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.Tuning) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.TelemetryRing) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.OxygenDeltaUnits) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.NitrogenDeltaUnits) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.ToxinDeltaUnits) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.TemperatureDeltaMilli) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.GasRemainders) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.ShaderPayload) ||
                   bufferId == ToHandleBufferId(AtmosphereLogisticsBufferIds.Profiles);
        }

        private static uint ToHandleBufferId(BufferID bufferId)
        {
            return unchecked((uint)(int)bufferId);
        }

        private static ulong AtmosphereLogisticsMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private void ClearVaultHandles()
        {
            _frontCells = default;
            _backCells = default;
            _nodes = default;
            _connections = default;
            _edgeOffsets = default;
            _edgeDestinations = default;
            _edgeConductance = default;
            _edgeWriteCursor = default;
            _consumers = default;
            _sources = default;
            _vents = default;
            _counters = default;
            _tuning = default;
            _telemetry = default;
            _oxygenDeltaUnits = default;
            _carbonDioxideDeltaUnits = default;
            _nitrogenDeltaUnits = default;
            _toxinDeltaUnits = default;
            _temperatureDeltaMilli = default;
            _remainders = default;
            _shaderPayload = default;
#if UNITY_EDITOR
            _profiles = default;
#endif
        }

        private void PreSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            if (!vault.TryAcquireMutationGuard(AtmosphereFrameMutationGuardMask))
                return;
            try
            {
                ApplyQualityAndEditorTuning(vault, in timing);
                IngestPlayerBreathingSignals(vault);
                IngestExternalGasSignals(vault);
            }
            finally
            {
                vault.ReleaseMutationGuard(AtmosphereFrameMutationGuardMask);
            }
#if UNITY_EDITOR
            if ((_lastDispatcherFrame & (CsvPollCadenceFrames - 1)) == 0u)
                MonitorGasProfileCsv(vault);
#endif
        }

        private JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn)
        {
            if (_simulationScheduled)
            {
                if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                    return JobHandle.CombineDependencies(dependsOn, _simulationHandle);

                _simulationScheduled = false;
                ReleaseJobBufferPins();
                ApplyPendingVaultRebindIfSafe();
            }

            IDataVault vault = ResolveVault();
            if (vault == null || !HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return dependsOn;
            }

            _lastDispatcherFrame = context.Frame;
            if (!TryPinJobBuffers(vault))
                return dependsOn;

            if (!TryResolveSimulationBuffers(
                    vault,
                    out NativeArray<AtmosphereCellDTO> front,
                    out NativeArray<AtmosphereCellDTO> back,
                    out NativeArray<AtmosphereNodeDTO> nodes,
                    out NativeArray<int> edgeOffsets,
                    out NativeArray<int> edgeDestinations,
                    out NativeArray<float> edgeConductance,
                    out NativeArray<AtmosphereConsumerDTO> consumers,
                    out NativeArray<AtmosphereToxicSourceDTO> sources,
                    out NativeArray<AtmosphereVentDTO> vents,
                    out NativeArray<AtmosphereGraphCountersDTO> counters,
                    out NativeArray<AtmosphereTuningDTO> tuning,
                    out NativeArray<AtmosphereTelemetryEntry> telemetry,
                    out NativeArray<AtmosphereDeltaLane64> oxygenDelta,
                    out NativeArray<AtmosphereDeltaLane64> carbonDioxideDelta,
                    out NativeArray<AtmosphereDeltaLane64> nitrogenDelta,
                    out NativeArray<AtmosphereDeltaLane64> toxinDelta,
                    out NativeArray<AtmosphereDeltaLane64> temperatureDelta,
                    out NativeArray<AtmosphereGasRemainderDTO> remainders,
                    out NativeArray<AtmosphereShaderPayloadDTO> shaderPayload))
            {
                ReleaseJobBufferPins();
                return dependsOn;
            }

            bool keepLocksForScheduledJob = false;
            try
            {
                AtmosphereGraphCountersDTO counter = counters[0];
                int nodeCount = math.clamp(counter.NodeCount, 0, math.min(front.Length, back.Length));
                if (nodeCount <= 0)
                {
                    return dependsOn;
                }

                AtmosphereTuningDTO tune = tuning.Length > 0 ? tuning[0] : DefaultTuning();
                float qualityWeight = MathLodApproximation.SaturateFinite(tune.GlobalQualityWeight, AuthoritativeQualityWeight);
                int iterations = ResolveDiffusionIterations(qualityWeight);
                float dt = ResolveSimulationTickDelta(in timing);

                JobHandle handle = new AtmosphereClearDeltaJob
                {
                    OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                    CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                    NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                    ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                    TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta)
                }.Schedule(nodeCount, 64, dependsOn);

                handle = new AtmosphereConsumerBreathingJob
                {
                    Nodes = nodes,
                    Consumers = consumers,
                    OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                    CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                    TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta),
                    NodeCount = nodeCount,
                    ConsumerCount = math.clamp(counter.ConsumerCount, 0, consumers.Length),
                    DeltaTime = dt,
                    InhalationMultiplier = tune.InhalationMultiplier
                }.Schedule(math.max(1, math.clamp(counter.ConsumerCount, 0, consumers.Length)), 32, handle);

                handle = new AtmosphereToxicSourceInjectionJob
                {
                    Nodes = nodes,
                    Sources = sources,
                    OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                    CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                    ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                    TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta),
                    NodeCount = nodeCount,
                    SourceCount = math.clamp(counter.SourceCount, 0, sources.Length),
                    DeltaTime = dt
                }.Schedule(math.max(1, math.clamp(counter.SourceCount, 0, sources.Length)), 32, handle);

                handle = new AtmosphereVentLeakJob
                {
                    Nodes = nodes,
                    Vents = vents,
                    OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                    NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                    ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                    NodeCount = nodeCount,
                    VentCount = math.clamp(counter.VentCount, 0, vents.Length),
                    DeltaTime = dt,
                    LeakDrainMultiplier = tune.LeakDrainMultiplier
                }.Schedule(math.max(1, math.clamp(counter.VentCount, 0, vents.Length)), 32, handle);

                VaultGenerationHandle<AtmosphereCellDTO> frontHandle = _frontCells;
                VaultGenerationHandle<AtmosphereCellDTO> backHandle = _backCells;
                NativeArray<AtmosphereCellDTO> currentFront = front;
                NativeArray<AtmosphereCellDTO> currentBack = back;

                for (int iteration = 0; iteration < iterations; iteration++)
                {
                    handle = new AtmosphereDiffusionSolverJob
                    {
                        Front = (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(currentFront),
                        Back = (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(currentBack),
                        EdgeOffsets = edgeOffsets,
                        EdgeDestinations = edgeDestinations,
                        EdgeConductance = edgeConductance,
                        OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                        CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                        NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                        ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                        TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta),
                        NodeCount = nodeCount,
                        EdgeCount = math.clamp(counter.CsrEdgeCount, 0, edgeDestinations.Length),
                        DeltaTime = dt,
                        BaseDiffusionRate = tune.BaseDiffusionRate,
                        ToxinDissipationSpeed = tune.ToxinDissipationSpeed
                    }.Schedule(nodeCount, 64, handle);

                    handle = new AtmosphereQuantizeGasJob
                    {
                        Cells = currentBack,
                        Remainders = remainders
                    }.Schedule(nodeCount, 64, handle);

                    handle = new AtmosphereConservationCorrectionJob
                    {
                        Front = (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(currentFront),
                        Back = (AtmosphereCellDTO*)NativeArrayUnsafeUtility.GetUnsafePtr(currentBack),
                        OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                        CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                        NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                        ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                        NodeCount = nodeCount
                    }.Schedule(handle);

                    Swap(ref currentFront, ref currentBack);
                    Swap(ref frontHandle, ref backHandle);

                    if (iteration == 0 && iterations > 1)
                    {
                        handle = new AtmosphereClearDeltaJob
                        {
                            OxygenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(oxygenDelta),
                            CarbonDioxideDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(carbonDioxideDelta),
                            NitrogenDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(nitrogenDelta),
                            ToxinDeltaUnits = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(toxinDelta),
                            TemperatureDeltaMilli = (AtmosphereDeltaLane64*)NativeArrayUnsafeUtility.GetUnsafePtr(temperatureDelta)
                        }.Schedule(nodeCount, 64, handle);
                    }
                }

                _frontCells = frontHandle;
                _backCells = backHandle;
                _lastIterations = iterations;
                handle = new AtmosphereTelemetryJob
                {
                    Cells = currentFront,
                    Telemetry = telemetry,
                    Counters = counters,
                    ShaderPayload = shaderPayload,
                    NodeCount = nodeCount,
                    SolverMicros = 0,
                    JacobiIterations = iterations,
                    FrameIndex = unchecked((int)context.Frame)
                }.Schedule(handle);

                _simulationScheduled = true;
                _simulationHandle = handle;
                _jobScheduleTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                H8Memory.RegisterActiveJob(OwnerSystemId, handle);
                keepLocksForScheduledJob = true;
                return handle;
            }
            finally
            {
                if (!keepLocksForScheduledJob)
                    ReleaseJobBufferPins();
            }
        }

        private void PostSimulationTick(in DispatcherTimingDTO timing)
        {
            IDataVault vault = ResolveVault();
            if (vault == null || !_simulationScheduled)
            {
                ReleaseJobBufferPins();
                return;
            }

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _simulationHandle))
                return;

            _simulationScheduled = false;
            _lastMicros = ElapsedMicroseconds(_jobScheduleTimestamp);
            bool writeDumpAfterRelease = false;
            if (Resolve(in _telemetry, AtmosphereLogisticsBufferIds.TelemetryRing, out NativeArray<AtmosphereTelemetryEntry> telemetry) &&
                Resolve(in _counters, AtmosphereLogisticsBufferIds.Counters, out NativeArray<AtmosphereGraphCountersDTO> counters) &&
                telemetry.Length > 0 && counters.Length > 0)
            {
                int cursor = counters[0].TelemetryCursor - 1;
                int index = cursor % telemetry.Length;
                if (index < 0)
                    index += telemetry.Length;
                AtmosphereTelemetryEntry entry = telemetry[index];
                entry.SolverMicros = _lastMicros;
                telemetry[index] = entry;
                _lastAverageOxygen01 = entry.AverageOxygen01;
                _lastMaxCarbonDioxide01 = entry.MaxCarbonDioxide01;
                _lastMaxToxin01 = entry.MaxToxin01;
                _lastNodeCount = entry.NodeCount;
                if ((entry.FaultFlags & AtmosphereFaultFlags.NaNDetected) != 0u && !_dumpWrittenThisFault)
                    writeDumpAfterRelease = TryStageDumpSnapshot(telemetry);
                if ((entry.FaultFlags & AtmosphereFaultFlags.NaNDetected) == 0u)
                    _dumpWrittenThisFault = false;
            }

            ReleaseJobBufferPins();
            if (writeDumpAfterRelease)
                WriteDumpSnapshot();
        }

        private void CompleteSimulationForLifecycle()
        {
            if (!_simulationScheduled)
                return;

            DispatcherJobFence.BeginPostSimulationSwapWindow();
            try
            {
                DispatcherJobFence.TryComplete(ref _simulationHandle, forceComplete: true);
            }
            finally
            {
                DispatcherJobFence.EndPostSimulationSwapWindow();
            }

            _simulationScheduled = false;
            ReleaseJobBufferPins();
        }

        private void VisualSyncTick(in DispatcherTimingDTO timing)
        {
            if (_simulationScheduled)
                return;

            if (!HasVaultStateReady())
            {
                _vaultRepairRequested = true;
                return;
            }

            if (Resolve(in _shaderPayload, AtmosphereLogisticsBufferIds.ShaderPayload, out NativeArray<AtmosphereShaderPayloadDTO> payload) && payload.Length > 0)
            {
                AtmosphereShaderPayloadDTO scalar = payload[0];
                Shader.SetGlobalVector(_GasScalarsShaderId, new Vector4(scalar.Oxygen01, scalar.CarbonDioxide01, scalar.Toxin01, scalar.Flow01));
                Shader.SetGlobalFloat(_GasQualityShaderId, _smoothedQualityWeight01);
            }
        }

        private bool EnsureVaultState(IDataVault vault)
        {
            if (!_vaultInitialized)
            {
                _frontCells = vault.EnsureGenerationHandle<AtmosphereCellDTO>(AtmosphereLogisticsBufferIds.CellsFront, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _backCells = vault.EnsureGenerationHandle<AtmosphereCellDTO>(AtmosphereLogisticsBufferIds.CellsBack, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _nodes = vault.EnsureGenerationHandle<AtmosphereNodeDTO>(AtmosphereLogisticsBufferIds.Nodes, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _connections = vault.EnsureGenerationHandle<AtmosphereConnectionDTO>(AtmosphereLogisticsBufferIds.Connections, AtmosphereLogisticsConstants.MaxMockConnections, OwnerSystemId);
                _edgeOffsets = vault.EnsureGenerationHandle<int>(AtmosphereLogisticsBufferIds.EdgeOffsets, AtmosphereLogisticsConstants.MaxMockNodes + 1, OwnerSystemId);
                _edgeDestinations = vault.EnsureGenerationHandle<int>(AtmosphereLogisticsBufferIds.EdgeDestinations, AtmosphereLogisticsConstants.MaxCsrEdges, OwnerSystemId);
                _edgeConductance = vault.EnsureGenerationHandle<float>(AtmosphereLogisticsBufferIds.EdgeConductance, AtmosphereLogisticsConstants.MaxCsrEdges, OwnerSystemId);
                _edgeWriteCursor = vault.EnsureGenerationHandle<int>(AtmosphereLogisticsBufferIds.EdgeWriteCursor, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _consumers = vault.EnsureGenerationHandle<AtmosphereConsumerDTO>(AtmosphereLogisticsBufferIds.Consumers, AtmosphereLogisticsConstants.MaxConsumers, OwnerSystemId);
                _sources = vault.EnsureGenerationHandle<AtmosphereToxicSourceDTO>(AtmosphereLogisticsBufferIds.ToxicSources, AtmosphereLogisticsConstants.MaxToxicSources, OwnerSystemId);
                _vents = vault.EnsureGenerationHandle<AtmosphereVentDTO>(AtmosphereLogisticsBufferIds.Vents, AtmosphereLogisticsConstants.MaxVents, OwnerSystemId);
                _counters = vault.EnsureGenerationHandle<AtmosphereGraphCountersDTO>(AtmosphereLogisticsBufferIds.Counters, 1, OwnerSystemId);
                _tuning = vault.EnsureGenerationHandle<AtmosphereTuningDTO>(AtmosphereLogisticsBufferIds.Tuning, 1, OwnerSystemId);
                _telemetry = vault.EnsureGenerationHandle<AtmosphereTelemetryEntry>(AtmosphereLogisticsBufferIds.TelemetryRing, AtmosphereLogisticsConstants.TelemetryRingCapacity, OwnerSystemId);
                _oxygenDeltaUnits = vault.EnsureGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.OxygenDeltaUnits, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _carbonDioxideDeltaUnits = vault.EnsureGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _nitrogenDeltaUnits = vault.EnsureGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.NitrogenDeltaUnits, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _toxinDeltaUnits = vault.EnsureGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.ToxinDeltaUnits, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _temperatureDeltaMilli = vault.EnsureGenerationHandle<AtmosphereDeltaLane64>(AtmosphereLogisticsBufferIds.TemperatureDeltaMilli, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _remainders = vault.EnsureGenerationHandle<AtmosphereGasRemainderDTO>(AtmosphereLogisticsBufferIds.GasRemainders, AtmosphereLogisticsConstants.MaxMockNodes, OwnerSystemId);
                _shaderPayload = vault.EnsureGenerationHandle<AtmosphereShaderPayloadDTO>(AtmosphereLogisticsBufferIds.ShaderPayload, 1, OwnerSystemId);
#if UNITY_EDITOR
                _profiles = vault.EnsureGenerationHandle<AtmosphereGasProfileDTO>(AtmosphereLogisticsBufferIds.Profiles, AtmosphereLogisticsConstants.MaxProfiles, OwnerSystemId);
#endif
                _vaultInitialized = true;
            }

            if (!_layoutChecked)
            {
                _layoutValid = AtmosphereLogisticsLayout.ValidateAtmosphereCellLayout() &&
                               AtmosphereLogisticsLayout.ValidateAtmosphereDeltaLaneLayout();
                _layoutChecked = true;
            }

            if (!_defaultsInitialized || !_layoutValid)
            {
                if (!vault.TryAcquireMutationGuard(AtmosphereFrameMutationGuardMask))
                    return false;

                try
                {
                    GenerateEmergencyMockTopology(vault);
                }
                finally
                {
                    vault.ReleaseMutationGuard(AtmosphereFrameMutationGuardMask);
                }
            }

            return _vaultInitialized;
        }

        private void PrepareRuntimeStateCold()
        {
            IDataVault vault = _vault;
            bool ready = vault != null && EnsureVaultState(vault);
            _vaultRepairRequested = !ready;
        }

        private bool HasVaultStateReady()
        {
            IDataVault vault = _vault;
            return vault != null &&
                _vaultInitialized &&
                _defaultsInitialized &&
                TryValidateSimulationBuffers(vault);
        }

        private void GenerateEmergencyMockTopology(IDataVault vault)
        {
            if (!TryResolveInitializationBuffers(
                    out NativeArray<AtmosphereNodeDTO> nodes,
                    out NativeArray<AtmosphereConnectionDTO> connections,
                    out NativeArray<AtmosphereCellDTO> front,
                    out NativeArray<AtmosphereCellDTO> back,
                    out NativeArray<AtmosphereConsumerDTO> consumers,
                    out NativeArray<AtmosphereToxicSourceDTO> sources,
                    out NativeArray<AtmosphereVentDTO> vents,
                    out NativeArray<AtmosphereGraphCountersDTO> counters,
                    out NativeArray<AtmosphereTuningDTO> tuning,
                    out NativeArray<int> edgeOffsets,
                    out NativeArray<int> edgeDestinations,
                    out NativeArray<float> edgeConductance,
                    out NativeArray<int> edgeWriteCursor))
            {
                return;
            }

            tuning[0] = DefaultTuning();
            AtmosphereMockTopologyJob topologyJob = new AtmosphereMockTopologyJob
            {
                Nodes = nodes,
                Connections = connections,
                FrontCells = front,
                BackCells = back,
                Consumers = consumers,
                Sources = sources,
                Vents = vents,
                Counters = counters,
                Tuning = tuning,
                GridOriginAup = double3.zero
            };
            topologyJob.Execute();

            AtmosphereCsrBuildJob csrBuildJob = new AtmosphereCsrBuildJob
            {
                Connections = connections,
                EdgeOffsets = edgeOffsets,
                EdgeDestinations = edgeDestinations,
                EdgeConductance = edgeConductance,
                EdgeWriteCursor = edgeWriteCursor,
                Counters = counters
            };
            csrBuildJob.Execute();

            _lastNodeCount = counters[0].NodeCount;
            _defaultsInitialized = true;
        }

        private void ApplyQualityAndEditorTuning(IDataVault vault, in DispatcherTimingDTO timing)
        {
            if (!Resolve(in _tuning, AtmosphereLogisticsBufferIds.Tuning, out NativeArray<AtmosphereTuningDTO> tuning) || tuning.Length == 0)
                return;

            AtmosphereTuningDTO tune = tuning[0];
            float targetQuality = ResolveVisualQualityWeight();
            _smoothedQualityWeight01 = SmoothQualityWeight(_smoothedQualityWeight01, targetQuality, ResolveSimulationTickDelta(in timing));
            tune.BaseDiffusionRate = math.clamp(FiniteOr(s_pendingBaseDiffusionRate, 0.35f), 0f, 4f);
            tune.InhalationMultiplier = math.clamp(FiniteOr(s_pendingInhalationMultiplier, 1f), 0f, 4f);
            tune.ToxinDissipationSpeed = math.clamp(FiniteOr(s_pendingToxinDissipationSpeed, 0.005f), 0f, 1f);
            tune.GlobalQualityWeight = targetQuality;
            tune.CellSizeMeters = math.clamp(FiniteOr(tune.CellSizeMeters, 2f), AtmosphereLogisticsConstants.MinimumCellSizeMeters, AtmosphereLogisticsConstants.MaximumCellSizeMeters);
            tune.AmbientTemperatureCelsius = FiniteOr(tune.AmbientTemperatureCelsius, AtmosphereLogisticsConstants.DefaultTemperatureCelsius);
            tune.LeakDrainMultiplier = math.clamp(FiniteOr(tune.LeakDrainMultiplier, 1f), 0f, 8f);
            tuning[0] = tune;
        }

        private void IngestExternalGasSignals(IDataVault vault)
        {
            if (!Resolve(in _sources, AtmosphereLogisticsBufferIds.ToxicSources, out NativeArray<AtmosphereToxicSourceDTO> sources) ||
                !Resolve(in _counters, AtmosphereLogisticsBufferIds.Counters, out NativeArray<AtmosphereGraphCountersDTO> counters) ||
                sources.Length == 0 || counters.Length == 0)
            {
                return;
            }

            AtmosphereGraphCountersDTO counter = counters[0];
            int write = math.min(counter.SourceCount > 0 ? 1 : 0, sources.Length);
            ReadOnlySpan<FluidIncursionSignal> incursions = SignalBus<FluidIncursionSignal>.GetFrameSnapshot();
            for (int i = 0; i < incursions.Length && write < sources.Length; i++)
            {
                FluidIncursionSignal signal = incursions[i];
                double3 aup = signal.LeakAup.ToAbsoluteDouble3();
                float flow = math.saturate(signal.FlowRate01);
                sources[write++] = new AtmosphereToxicSourceDTO
                {
                    Aup = aup,
                    ToxinPerSecond01 = 0.000016f * flow,
                    CarbonDioxidePerSecond01 = 0.000012f * flow,
                    OxygenDrainPerSecond01 = 0.00002f * flow,
                    HeatPerSecond = -0.01f * flow,
                    SourceHash = signal.CompartmentId != 0u ? signal.CompartmentId : 0x464C5544u,
                    Flags = AtmosphereCellFlags.Breached,
                    LastNodeIndex = -1,
                    RadiusMeters = 4f
                };
            }

            ReadOnlySpan<ReactorDamageSignal> reactorSignals = SignalBus<ReactorDamageSignal>.GetFrameSnapshot();
            for (int i = 0; i < reactorSignals.Length && write < sources.Length; i++)
            {
                ReactorDamageSignal signal = reactorSignals[i];
                double3 aup = signal.DamageAup;
                float severity = math.max(math.saturate(signal.Damage01), math.saturate(signal.ToxinLeak01));
                sources[write++] = new AtmosphereToxicSourceDTO
                {
                    Aup = aup,
                    ToxinPerSecond01 = 0.00006f * severity,
                    CarbonDioxidePerSecond01 = 0.000018f * severity,
                    OxygenDrainPerSecond01 = 0.000012f * severity,
                    HeatPerSecond = 0.04f * severity,
                    SourceHash = signal.ReactorHash != 0u ? signal.ReactorHash : 0x52454143u,
                    Flags = AtmosphereCellFlags.ReactorLeak,
                    LastNodeIndex = -1,
                    RadiusMeters = 5f
                };
            }

            for (int i = write; i < sources.Length; i++)
                sources[i] = default;

            if (incursions.Length + reactorSignals.Length > sources.Length)
                counter.Flags |= AtmosphereFaultFlags.SourceOverflow;

            counter.SourceCount = write;
            counters[0] = counter;
        }

        private void IngestPlayerBreathingSignals(IDataVault vault)
        {
            if (!Resolve(in _consumers, AtmosphereLogisticsBufferIds.Consumers, out NativeArray<AtmosphereConsumerDTO> consumers) ||
                !Resolve(in _counters, AtmosphereLogisticsBufferIds.Counters, out NativeArray<AtmosphereGraphCountersDTO> counters) ||
                consumers.Length == 0 || counters.Length == 0)
            {
                return;
            }

            AtmosphereGraphCountersDTO counter = counters[0];
            ReadOnlySpan<PlayerBaseEnterSignal> enterSignals = SignalBus<PlayerBaseEnterSignal>.GetFrameSnapshot();
            for (int i = 0; i < enterSignals.Length; i++)
            {
                PlayerBaseEnterSignal signal = enterSignals[i];
                consumers[0] = new AtmosphereConsumerDTO
                {
                    Aup = signal.BaseCenterAup.ToAbsoluteDouble3(),
                    OxygenPerSecond01 = 0.000015f,
                    CarbonDioxidePerSecond01 = 0.000014f,
                    RadiusMeters = 4f,
                    HeatPerSecond = 0.0015f,
                    EntityHash = unchecked((uint)signal.BaseId),
                    Flags = 1u,
                    LastNodeHash = 0u,
                    LastNodeIndex = math.max(0, signal.RoomId)
                };
                counter.ConsumerCount = math.max(counter.ConsumerCount, 1);
            }

            ReadOnlySpan<PlayerBaseExitSignal> exitSignals = SignalBus<PlayerBaseExitSignal>.GetFrameSnapshot();
            for (int i = 0; i < exitSignals.Length; i++)
            {
                AtmosphereConsumerDTO consumer = consumers[0];
                consumer.Flags = 0u;
                consumers[0] = consumer;
                counter.ConsumerCount = math.max(counter.ConsumerCount, 1);
            }

            counters[0] = counter;
        }

#if UNITY_EDITOR
        private void MonitorGasProfileCsv(IDataVault vault)
        {
            if (!File.Exists(_csvPath))
            {
                return;
            }

            DateTime writeUtc = File.GetLastWriteTimeUtc(_csvPath);
            if (writeUtc == _csvLastWriteUtc)
                return;

            _csvLastWriteUtc = writeUtc;
            Span<byte> csvScratch = stackalloc byte[AtmosphereLogisticsConstants.CsvScratchBytes];
            int bytesRead = ReadCsvFileNoStringAlloc(_csvPath, csvScratch);
            if (bytesRead <= 0)
                return;

            ReadOnlySpan<byte> span = csvScratch.Slice(0, math.min(bytesRead, csvScratch.Length));
            Span<AtmosphereGasProfileDTO> stagedProfiles = stackalloc AtmosphereGasProfileDTO[AtmosphereLogisticsConstants.MaxProfiles];
            if (!TryParseGasProfilesCsv(span, stagedProfiles, out int parsed) || parsed <= 0)
                return;

            if (vault == null || vault.IsCompactionFenceActive || !vault.TryAcquireMutationGuard(ProfileCsvMutationGuardMask))
                return;

            try
            {
                if (!Resolve(in _profiles, AtmosphereLogisticsBufferIds.Profiles, out NativeArray<AtmosphereGasProfileDTO> profiles) ||
                    !Resolve(in _tuning, AtmosphereLogisticsBufferIds.Tuning, out NativeArray<AtmosphereTuningDTO> tuning) ||
                    profiles.Length == 0 ||
                    tuning.Length == 0)
                {
                    return;
                }

                int commitCount = math.min(parsed, profiles.Length);
                for (int i = 0; i < commitCount; i++)
                    profiles[i] = stagedProfiles[i];
                for (int i = commitCount; i < profiles.Length; i++)
                    profiles[i] = default;

                AtmosphereTuningDTO tune = tuning[0];
                tune.AmbientTemperatureCelsius = stagedProfiles[0].Temperature;
                tuning[0] = tune;
            }
            finally
            {
                vault.ReleaseMutationGuard(ProfileCsvMutationGuardMask);
            }
        }
#endif

#if UNITY_EDITOR
        public static bool TryParseGasProfilesCsv(ReadOnlySpan<byte> bytes, NativeArray<AtmosphereGasProfileDTO> profiles, out int count)
        {
            count = 0;
            if (!profiles.IsCreated || profiles.Length == 0)
                return false;

            Span<AtmosphereGasProfileDTO> profileSpan = new Span<AtmosphereGasProfileDTO>(
                NativeArrayUnsafeUtility.GetUnsafePtr(profiles),
                profiles.Length);
            return TryParseGasProfilesCsv(bytes, profileSpan, out count);
        }

        public static bool TryParseGasProfilesCsv(ReadOnlySpan<byte> bytes, Span<AtmosphereGasProfileDTO> profiles, out int count)
        {
            count = 0;
            if (profiles.Length == 0)
                return false;

            int cursor = 0;
            bool anyMalformed = false;
            while (cursor < bytes.Length && count < profiles.Length)
            {
                SkipLineTerminators(bytes, ref cursor);
                if (cursor >= bytes.Length)
                    break;

                int lineStart = cursor;
                if (IsGasProfileHeaderLine(bytes, lineStart))
                {
                    SkipLine(bytes, ref cursor);
                    continue;
                }

                if (!TryReadProfileHash(bytes, ref cursor, out uint hash))
                {
                    SkipLine(bytes, ref cursor);
                    anyMalformed = true;
                    continue;
                }

                bool rowMalformed = false;
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float oxygen = ReadFloatOr(bytes, ref cursor, AtmosphereLogisticsConstants.DefaultOxygen01, ref rowMalformed);
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float carbon = ReadFloatOr(bytes, ref cursor, AtmosphereLogisticsConstants.DefaultCarbonDioxide01, ref rowMalformed);
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float nitrogen = ReadFloatOr(bytes, ref cursor, AtmosphereLogisticsConstants.DefaultNitrogen01, ref rowMalformed);
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float toxin = ReadFloatOr(bytes, ref cursor, 0f, ref rowMalformed);
                rowMalformed |= !ConsumeComma(bytes, ref cursor);
                float temperature = ReadFloatOr(bytes, ref cursor, AtmosphereLogisticsConstants.DefaultTemperatureCelsius, ref rowMalformed);
                SkipLine(bytes, ref cursor);
                anyMalformed |= rowMalformed;

                profiles[count++] = new AtmosphereGasProfileDTO
                {
                    ProfileHash = hash,
                    Oxygen01 = math.saturate(oxygen),
                    CarbonDioxide01 = math.saturate(carbon),
                    Nitrogen01 = math.saturate(nitrogen),
                    Toxin01 = math.saturate(toxin),
                    Temperature = math.clamp(FiniteOr(temperature, AtmosphereLogisticsConstants.DefaultTemperatureCelsius), -80f, 250f),
                    Flags = rowMalformed ? AtmosphereFaultFlags.CsvMalformed : 0u
                };
            }

            return count > 0 && !anyMalformed;
        }
#endif

        private bool TryResolveInitializationBuffers(
            out NativeArray<AtmosphereNodeDTO> nodes,
            out NativeArray<AtmosphereConnectionDTO> connections,
            out NativeArray<AtmosphereCellDTO> front,
            out NativeArray<AtmosphereCellDTO> back,
            out NativeArray<AtmosphereConsumerDTO> consumers,
            out NativeArray<AtmosphereToxicSourceDTO> sources,
            out NativeArray<AtmosphereVentDTO> vents,
            out NativeArray<AtmosphereGraphCountersDTO> counters,
            out NativeArray<AtmosphereTuningDTO> tuning,
            out NativeArray<int> edgeOffsets,
            out NativeArray<int> edgeDestinations,
            out NativeArray<float> edgeConductance,
            out NativeArray<int> edgeWriteCursor)
        {
            nodes = default;
            connections = default;
            front = default;
            back = default;
            consumers = default;
            sources = default;
            vents = default;
            counters = default;
            tuning = default;
            edgeOffsets = default;
            edgeDestinations = default;
            edgeConductance = default;
            edgeWriteCursor = default;

            return Resolve(in _nodes, AtmosphereLogisticsBufferIds.Nodes, out nodes) &&
                   Resolve(in _connections, AtmosphereLogisticsBufferIds.Connections, out connections) &&
                   Resolve(in _frontCells, AtmosphereLogisticsBufferIds.CellsFront, out front) &&
                   Resolve(in _backCells, AtmosphereLogisticsBufferIds.CellsBack, out back) &&
                   Resolve(in _consumers, AtmosphereLogisticsBufferIds.Consumers, out consumers) &&
                   Resolve(in _sources, AtmosphereLogisticsBufferIds.ToxicSources, out sources) &&
                   Resolve(in _vents, AtmosphereLogisticsBufferIds.Vents, out vents) &&
                   Resolve(in _counters, AtmosphereLogisticsBufferIds.Counters, out counters) &&
                   Resolve(in _tuning, AtmosphereLogisticsBufferIds.Tuning, out tuning) &&
                   Resolve(in _edgeOffsets, AtmosphereLogisticsBufferIds.EdgeOffsets, out edgeOffsets) &&
                   Resolve(in _edgeDestinations, AtmosphereLogisticsBufferIds.EdgeDestinations, out edgeDestinations) &&
                   Resolve(in _edgeConductance, AtmosphereLogisticsBufferIds.EdgeConductance, out edgeConductance) &&
                   Resolve(in _edgeWriteCursor, AtmosphereLogisticsBufferIds.EdgeWriteCursor, out edgeWriteCursor);
        }

        private bool TryResolveSimulationBuffers(
            IDataVault vault,
            out NativeArray<AtmosphereCellDTO> front,
            out NativeArray<AtmosphereCellDTO> back,
            out NativeArray<AtmosphereNodeDTO> nodes,
            out NativeArray<int> edgeOffsets,
            out NativeArray<int> edgeDestinations,
            out NativeArray<float> edgeConductance,
            out NativeArray<AtmosphereConsumerDTO> consumers,
            out NativeArray<AtmosphereToxicSourceDTO> sources,
            out NativeArray<AtmosphereVentDTO> vents,
            out NativeArray<AtmosphereGraphCountersDTO> counters,
            out NativeArray<AtmosphereTuningDTO> tuning,
            out NativeArray<AtmosphereTelemetryEntry> telemetry,
            out NativeArray<AtmosphereDeltaLane64> oxygenDelta,
            out NativeArray<AtmosphereDeltaLane64> carbonDioxideDelta,
            out NativeArray<AtmosphereDeltaLane64> nitrogenDelta,
            out NativeArray<AtmosphereDeltaLane64> toxinDelta,
            out NativeArray<AtmosphereDeltaLane64> temperatureDelta,
            out NativeArray<AtmosphereGasRemainderDTO> remainders,
            out NativeArray<AtmosphereShaderPayloadDTO> shaderPayload)
        {
            front = default;
            back = default;
            nodes = default;
            edgeOffsets = default;
            edgeDestinations = default;
            edgeConductance = default;
            consumers = default;
            sources = default;
            vents = default;
            counters = default;
            tuning = default;
            telemetry = default;
            oxygenDelta = default;
            carbonDioxideDelta = default;
            nitrogenDelta = default;
            toxinDelta = default;
            temperatureDelta = default;
            remainders = default;
            shaderPayload = default;

            return Resolve(vault, in _frontCells, AtmosphereLogisticsBufferIds.CellsFront, out front) &&
                   Resolve(vault, in _backCells, AtmosphereLogisticsBufferIds.CellsBack, out back) &&
                   Resolve(vault, in _nodes, AtmosphereLogisticsBufferIds.Nodes, out nodes) &&
                   Resolve(vault, in _edgeOffsets, AtmosphereLogisticsBufferIds.EdgeOffsets, out edgeOffsets) &&
                   Resolve(vault, in _edgeDestinations, AtmosphereLogisticsBufferIds.EdgeDestinations, out edgeDestinations) &&
                   Resolve(vault, in _edgeConductance, AtmosphereLogisticsBufferIds.EdgeConductance, out edgeConductance) &&
                   Resolve(vault, in _consumers, AtmosphereLogisticsBufferIds.Consumers, out consumers) &&
                   Resolve(vault, in _sources, AtmosphereLogisticsBufferIds.ToxicSources, out sources) &&
                   Resolve(vault, in _vents, AtmosphereLogisticsBufferIds.Vents, out vents) &&
                   Resolve(vault, in _counters, AtmosphereLogisticsBufferIds.Counters, out counters) &&
                   Resolve(vault, in _tuning, AtmosphereLogisticsBufferIds.Tuning, out tuning) &&
                   Resolve(vault, in _telemetry, AtmosphereLogisticsBufferIds.TelemetryRing, out telemetry) &&
                   Resolve(vault, in _oxygenDeltaUnits, AtmosphereLogisticsBufferIds.OxygenDeltaUnits, out oxygenDelta) &&
                   Resolve(vault, in _carbonDioxideDeltaUnits, AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits, out carbonDioxideDelta) &&
                   Resolve(vault, in _nitrogenDeltaUnits, AtmosphereLogisticsBufferIds.NitrogenDeltaUnits, out nitrogenDelta) &&
                   Resolve(vault, in _toxinDeltaUnits, AtmosphereLogisticsBufferIds.ToxinDeltaUnits, out toxinDelta) &&
                   Resolve(vault, in _temperatureDeltaMilli, AtmosphereLogisticsBufferIds.TemperatureDeltaMilli, out temperatureDelta) &&
                   Resolve(vault, in _remainders, AtmosphereLogisticsBufferIds.GasRemainders, out remainders) &&
                   Resolve(vault, in _shaderPayload, AtmosphereLogisticsBufferIds.ShaderPayload, out shaderPayload);
        }

        private bool Resolve<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer) where T : struct
        {
            return Resolve(ResolveVault(), in handle, expectedBufferId, out buffer);
        }

        private bool Resolve<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T> buffer) where T : struct
        {
            if (vault == null || vault.IsCompactionFenceActive || !IsOwnedVaultHandle(in handle, expectedBufferId))
            {
                buffer = default;
                return false;
            }

            return vault.TryResolveHandle(in handle, out buffer);
        }

        private bool ResolveReadOnly<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId,
            out NativeArray<T>.ReadOnly buffer) where T : struct
        {
            buffer = default;
            IDataVault vault = _vault;
            if (vault == null || vault.IsCompactionFenceActive || !IsOwnedVaultHandle(in handle, expectedBufferId))
            {
                return false;
            }

            return vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated;
        }

        private bool TryPinJobBuffers(IDataVault vault)
        {
            ReleaseJobBufferPins(applyPendingRebind: false);
            if (vault == null || vault.IsCompactionFenceActive || !TryValidateSimulationBuffers(vault))
                return false;

            bool pinned = false;
            try
            {
                _jobPinVault = vault;
                if (!TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.CellsFront, AtmosphereJobPinCellsFront) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.CellsBack, AtmosphereJobPinCellsBack) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.Nodes, AtmosphereJobPinNodes) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.EdgeOffsets, AtmosphereJobPinEdgeOffsets) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.EdgeDestinations, AtmosphereJobPinEdgeDestinations) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.EdgeConductance, AtmosphereJobPinEdgeConductance) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.Consumers, AtmosphereJobPinConsumers) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.ToxicSources, AtmosphereJobPinToxicSources) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.Vents, AtmosphereJobPinVents) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.Counters, AtmosphereJobPinCounters) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.Tuning, AtmosphereJobPinTuning) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.TelemetryRing, AtmosphereJobPinTelemetryRing) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.OxygenDeltaUnits, AtmosphereJobPinOxygenDelta) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits, AtmosphereJobPinCarbonDioxideDelta) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.NitrogenDeltaUnits, AtmosphereJobPinNitrogenDelta) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.ToxinDeltaUnits, AtmosphereJobPinToxinDelta) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.TemperatureDeltaMilli, AtmosphereJobPinTemperatureDelta) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.GasRemainders, AtmosphereJobPinGasRemainders) ||
                    !TryLockAtmosphereJobBuffer(vault, AtmosphereLogisticsBufferIds.ShaderPayload, AtmosphereJobPinShaderPayload))
                {
                    return false;
                }

                if (!TryValidateSimulationBuffers(vault))
                    return false;

                _jobBuffersPinned = true;
                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    ReleaseJobBufferPins(applyPendingRebind: false);
            }
        }

        private bool TryValidateSimulationBuffers(IDataVault vault)
        {
            return TryResolveSimulationBuffers(
                vault,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _,
                out _);
        }

        private void ReleaseJobBufferPins(bool applyPendingRebind = true)
        {
            IDataVault vault = _jobPinVault;
            uint pinMask = _jobBufferPinMask;
            _jobPinVault = null;
            _jobBufferPinMask = 0u;
            _jobBuffersPinned = false;
            if (vault != null && pinMask != 0u)
            {
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinShaderPayload, AtmosphereLogisticsBufferIds.ShaderPayload);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinGasRemainders, AtmosphereLogisticsBufferIds.GasRemainders);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinTemperatureDelta, AtmosphereLogisticsBufferIds.TemperatureDeltaMilli);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinToxinDelta, AtmosphereLogisticsBufferIds.ToxinDeltaUnits);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinNitrogenDelta, AtmosphereLogisticsBufferIds.NitrogenDeltaUnits);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinCarbonDioxideDelta, AtmosphereLogisticsBufferIds.CarbonDioxideDeltaUnits);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinOxygenDelta, AtmosphereLogisticsBufferIds.OxygenDeltaUnits);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinTelemetryRing, AtmosphereLogisticsBufferIds.TelemetryRing);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinTuning, AtmosphereLogisticsBufferIds.Tuning);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinCounters, AtmosphereLogisticsBufferIds.Counters);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinVents, AtmosphereLogisticsBufferIds.Vents);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinToxicSources, AtmosphereLogisticsBufferIds.ToxicSources);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinConsumers, AtmosphereLogisticsBufferIds.Consumers);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinEdgeConductance, AtmosphereLogisticsBufferIds.EdgeConductance);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinEdgeDestinations, AtmosphereLogisticsBufferIds.EdgeDestinations);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinEdgeOffsets, AtmosphereLogisticsBufferIds.EdgeOffsets);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinNodes, AtmosphereLogisticsBufferIds.Nodes);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinCellsBack, AtmosphereLogisticsBufferIds.CellsBack);
                TryUnlockAtmosphereJobBuffer(vault, pinMask, AtmosphereJobPinCellsFront, AtmosphereLogisticsBufferIds.CellsFront);
            }

            if (applyPendingRebind)
                ApplyPendingVaultRebindIfSafe();
        }

        private bool TryLockAtmosphereJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobBufferPinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, OwnerSystemId))
                return false;

            _jobBufferPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockAtmosphereJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, OwnerSystemId);
        }

        private bool TryStageDumpSnapshot(NativeArray<AtmosphereTelemetryEntry> telemetry)
        {
            if (!telemetry.IsCreated || telemetry.Length == 0)
                return false;

            int count = math.min(telemetry.Length, _dumpSnapshot.Length);
            for (int i = 0; i < count; i++)
                _dumpSnapshot[i] = telemetry[i];
            _dumpSnapshotCount = count;
            return true;
        }

        private void WriteDumpSnapshot()
        {
            int count = math.min(_dumpSnapshotCount, _dumpSnapshot.Length);
            if (count <= 0)
                return;

            const int HeaderBytes = 16;
            const int RowBytes = 64;
            int byteCount = HeaderBytes + count * RowBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(BaseAtmosphereLogisticsRuntime),
                    DumpPayloadLabel);
                byte* target = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(payload);
                int cursor = 0;
                WriteUInt64LittleEndian(target, ref cursor, 0x4847415332323144UL);
                WriteInt32LittleEndian(target, ref cursor, 1);
                WriteInt32LittleEndian(target, ref cursor, count);

                uint hash = 2166136261u;
                for (int i = 0; i < count; i++)
                {
                    AtmosphereTelemetryEntry entry = _dumpSnapshot[i];
                    WriteUInt64LittleEndian(target, ref cursor, entry.StateHash);
                    WriteFloatLittleEndian(target, ref cursor, entry.AverageOxygen01);
                    WriteFloatLittleEndian(target, ref cursor, entry.MaxCarbonDioxide01);
                    WriteFloatLittleEndian(target, ref cursor, entry.AverageNitrogen01);
                    WriteFloatLittleEndian(target, ref cursor, entry.MaxToxin01);
                    WriteFloatLittleEndian(target, ref cursor, entry.AverageTemperature);
                    WriteInt32LittleEndian(target, ref cursor, entry.FrameIndex);
                    WriteInt32LittleEndian(target, ref cursor, entry.NodeCount);
                    WriteInt32LittleEndian(target, ref cursor, entry.EdgeCount);
                    WriteInt32LittleEndian(target, ref cursor, entry.ConsumerCount);
                    WriteInt32LittleEndian(target, ref cursor, entry.SourceCount);
                    WriteInt32LittleEndian(target, ref cursor, entry.SolverMicros);
                    WriteInt32LittleEndian(target, ref cursor, entry.JacobiIterations);
                    WriteUInt32LittleEndian(target, ref cursor, entry.FaultFlags);
                    WriteUInt32LittleEndian(target, ref cursor, entry.TotalGasUnits);

                    hash ^= unchecked((uint)entry.StateHash);
                    hash *= 16777619u;
                    hash ^= (uint)entry.FrameIndex;
                    hash *= 16777619u;
                    hash ^= entry.FaultFlags;
                    hash *= 16777619u;
                }

                _lastTelemetryHash = hash;
                _dumpWrittenThisFault = cursor == byteCount &&
                                        NativeFaultDumpWriter.TryWriteAll(DumpRelativePath, payload, cursor);
            }
            catch
            {
                _dumpWrittenThisFault = false;
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(BaseAtmosphereLogisticsRuntime),
                    DumpPayloadLabel);
            }
        }

        private static unsafe void WriteFloatLittleEndian(byte* destination, ref int cursor, float value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, math.asuint(value));
        }

        private static unsafe void WriteInt32LittleEndian(byte* destination, ref int cursor, int value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)value));
        }

        private static unsafe void WriteUInt32LittleEndian(byte* destination, ref int cursor, uint value)
        {
            destination[cursor] = (byte)value;
            destination[cursor + 1] = (byte)(value >> 8);
            destination[cursor + 2] = (byte)(value >> 16);
            destination[cursor + 3] = (byte)(value >> 24);
            cursor += 4;
        }

        private static unsafe void WriteUInt64LittleEndian(byte* destination, ref int cursor, ulong value)
        {
            WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)value));
            WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)(value >> 32)));
        }

        private static AtmosphereTuningDTO DefaultTuning()
        {
            return new AtmosphereTuningDTO
            {
                BaseDiffusionRate = s_pendingBaseDiffusionRate,
                InhalationMultiplier = s_pendingInhalationMultiplier,
                ToxinDissipationSpeed = s_pendingToxinDissipationSpeed,
                GlobalQualityWeight = ResolveVisualQualityWeight(),
                CellSizeMeters = 2f,
                AmbientTemperatureCelsius = AtmosphereLogisticsConstants.DefaultTemperatureCelsius,
                LeakDrainMultiplier = 1f,
                Flags = 0u
            };
        }

#if UNITY_EDITOR
        private static int ReadCsvFileNoStringAlloc(string path, Span<byte> scratch)
        {
            if (scratch.Length == 0)
                return 0;

            using FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            long fileLength = stream.Length;
            int readLength = fileLength <= 0L ? 0 : fileLength > scratch.Length ? scratch.Length : (int)fileLength;
            return stream.Read(scratch.Slice(0, readLength));
        }
#endif

        private static bool IsGasProfileHeaderLine(ReadOnlySpan<byte> bytes, int lineStart)
        {
            if ((uint)lineStart >= (uint)bytes.Length)
                return false;

            int start = lineStart;
            SkipSpaces(bytes, ref start);
            int end = start;
            while (end < bytes.Length && !IsTokenDelimiter(bytes[end]))
                end++;

            TrimToken(bytes, ref start, ref end);
            return TokenEqualsAscii(bytes, start, end, "profile") ||
                   TokenEqualsAscii(bytes, start, end, "profile_hash") ||
                   TokenEqualsAscii(bytes, start, end, "module") ||
                   TokenEqualsAscii(bytes, start, end, "module_type") ||
                   TokenEqualsAscii(bytes, start, end, "name");
        }

        private static void SkipLine(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && bytes[cursor] != (byte)'\n' && bytes[cursor] != (byte)'\r')
                cursor++;
            SkipLineTerminators(bytes, ref cursor);
        }

        private static void SkipLineTerminators(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && (bytes[cursor] == (byte)'\n' || bytes[cursor] == (byte)'\r'))
                cursor++;
        }

        private static bool ConsumeComma(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            SkipSpaces(bytes, ref cursor);
            if (cursor < bytes.Length && bytes[cursor] == (byte)',')
            {
                cursor++;
                return true;
            }

            return false;
        }

        private static bool TryReadProfileHash(ReadOnlySpan<byte> bytes, ref int cursor, out uint value)
        {
            SkipSpaces(bytes, ref cursor);
            int start = cursor;
            while (cursor < bytes.Length && !IsTokenDelimiter(bytes[cursor]))
                cursor++;

            int end = cursor;
            TrimToken(bytes, ref start, ref end);
            value = 0u;
            if (start >= end)
                return false;

            bool numeric = true;
            uint numericValue = 0u;
            for (int i = start; i < end; i++)
            {
                byte c = bytes[i];
                if (c < (byte)'0' || c > (byte)'9')
                {
                    numeric = false;
                    break;
                }

                numericValue = numericValue * 10u + (uint)(c - (byte)'0');
            }

            if (numeric)
            {
                value = numericValue;
                return true;
            }

            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte c = ToLowerAscii(bytes[i]);
                hash = (hash ^ c) * 16777619u;
            }

            value = hash == 0u ? 1u : hash;
            return true;
        }

        private static float ReadFloatOr(ReadOnlySpan<byte> bytes, ref int cursor, float fallback, ref bool malformed)
        {
            SkipSpaces(bytes, ref cursor);
            int sign = 1;
            if (cursor < bytes.Length && bytes[cursor] == (byte)'-')
            {
                sign = -1;
                cursor++;
            }

            double value = 0d;
            bool any = false;
            while (cursor < bytes.Length)
            {
                byte c = bytes[cursor];
                if (c < (byte)'0' || c > (byte)'9')
                    break;
                any = true;
                value = value * 10d + (c - (byte)'0');
                cursor++;
            }

            if (cursor < bytes.Length && bytes[cursor] == (byte)'.')
            {
                cursor++;
                double factor = 0.1d;
                while (cursor < bytes.Length)
                {
                    byte c = bytes[cursor];
                    if (c < (byte)'0' || c > (byte)'9')
                        break;
                    any = true;
                    value += (c - (byte)'0') * factor;
                    factor *= 0.1d;
                    cursor++;
                }
            }

            if (!any)
            {
                malformed = true;
                return fallback;
            }

            return (float)(value * sign);
        }

        private static void SkipSpaces(ReadOnlySpan<byte> bytes, ref int cursor)
        {
            while (cursor < bytes.Length && (bytes[cursor] == (byte)' ' || bytes[cursor] == (byte)'\t'))
                cursor++;
        }

        private static void TrimToken(ReadOnlySpan<byte> bytes, ref int start, ref int end)
        {
            while (start < end && (bytes[start] == (byte)' ' || bytes[start] == (byte)'\t'))
                start++;
            while (end > start && (bytes[end - 1] == (byte)' ' || bytes[end - 1] == (byte)'\t'))
                end--;
        }

        private static bool IsTokenDelimiter(byte value)
        {
            return value == (byte)',' || value == (byte)'\n' || value == (byte)'\r';
        }

        private static bool TokenEqualsAscii(ReadOnlySpan<byte> bytes, int start, int end, string ascii)
        {
            int length = end - start;
            if (length != ascii.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (ToLowerAscii(bytes[start + i]) != (byte)ascii[i])
                    return false;
            }

            return true;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static float ResolveSimulationTickDelta(in DispatcherTimingDTO timing)
        {
            float fixedDelta = FiniteOr(timing.FixedDelta, 0f);
            return fixedDelta > 0.00001f ? math.clamp(fixedDelta, 1f / 240f, 1f / 5f) : 1f / 60f;
        }

        private static float SmoothQualityWeight(float current, float target, float deltaTime)
        {
            float safeCurrent = math.saturate(FiniteOr(current, target));
            float safeTarget = math.saturate(FiniteOr(target, safeCurrent));
            float rate = safeTarget < safeCurrent ? 2.0f : 0.5f;
            float alpha = math.saturate(math.max(0f, deltaTime) * rate);
            float eased = math.smoothstep(0f, 1f, alpha);
            return math.lerp(safeCurrent, safeTarget, eased);
        }

        private static float ResolveVisualQualityWeight()
        {
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
                return MathLodApproximation.SaturateFinite(config.GlobalQualityWeight, AuthoritativeQualityWeight);

            return MathLodApproximation.SaturateFinite(HomeostasisBrain.GlobalQualityWeight, AuthoritativeQualityWeight);
        }

        private static int ResolveDiffusionIterations(float globalQualityWeight)
        {
            float q = MathLodApproximation.SmoothStep01(MathLodApproximation.SaturateFinite(globalQualityWeight, AuthoritativeQualityWeight));
            int iterations = (int)math.round(math.lerp(MinQualityDiffusionIterations, MaxQualityDiffusionIterations, q));
            return math.clamp(iterations, MinQualityDiffusionIterations, MaxQualityDiffusionIterations);
        }

        private static int ElapsedMicroseconds(long startTimestamp)
        {
            if (startTimestamp <= 0)
                return 0;

            long delta = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double micros = (double)delta * 1000000.0 / System.Diagnostics.Stopwatch.Frequency;
            return math.clamp((int)micros, 0, int.MaxValue);
        }

        private static float FiniteOr(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        private static void Swap<T>(ref T a, ref T b)
        {
            T temp = a;
            a = b;
            b = temp;
        }

        private sealed class PreSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly BaseAtmosphereLogisticsRuntime _owner;
            public PreSimulationPhaseSystem(BaseAtmosphereLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53323250u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PreSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { _owner.PreSimulationTick(in timing); }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class SimulationPhaseSystem : IDispatcherSystem
        {
            private readonly BaseAtmosphereLogisticsRuntime _owner;
            public SimulationPhaseSystem(BaseAtmosphereLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53323253u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.Simulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return _owner.ScheduleSimulation(in timing, in context, dependsOn); }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class PostSimulationPhaseSystem : IDispatcherSystem
        {
            private readonly BaseAtmosphereLogisticsRuntime _owner;
            public PostSimulationPhaseSystem(BaseAtmosphereLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x5332324Fu; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.PostSimulation; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { _owner.PostSimulationTick(in timing); }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { }
        }

        private sealed class VisualSyncPhaseSystem : IDispatcherSystem
        {
            private readonly BaseAtmosphereLogisticsRuntime _owner;
            public VisualSyncPhaseSystem(BaseAtmosphereLogisticsRuntime owner) { _owner = owner; }
            public uint GetSystemIdHash() { return 0x53323256u; }
            public DispatcherPhase GetDispatcherPhase() { return DispatcherPhase.VisualSync; }
            public byte GetBucketId() { return byte.MaxValue; }
            public int GetDependencyCount() { return 0; }
            public uint GetDependencyHash(int dependencyIndex) { return 0u; }
            public void PreSimulationTick(in DispatcherTimingDTO timing) { }
            public JobHandle ScheduleSimulation(in DispatcherTimingDTO timing, in DispatcherJobContext context, JobHandle dependsOn) { return dependsOn; }
            public void PostSimulationTick(in DispatcherTimingDTO timing) { }
            public void VisualSyncTick(in DispatcherTimingDTO timing) { _owner.VisualSyncTick(in timing); }
        }
    }
}
