using System;
#if UNITY_EDITOR
using System.IO;
#endif
using Hecton8.Core;
using Hecton8.Core.Contracts.Physics;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    public sealed class SeaglideHydrodynamicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private static int s_x001DirectSignalPushDropCount_SeaglideHydrodynamicsRuntime;

        private const uint JobPinStates = 1u << 0;
        private const uint JobPinRequests = 1u << 1;
        private const uint JobPinForcePackets = 1u << 2;
        private const uint JobPinFlowSamples = 1u << 3;
        private const uint JobPinTuning = 1u << 4;
        private const uint JobPinTelemetryRing = 1u << 5;
        private const uint JobPinTelemetryCursor = 1u << 6;
        private const uint JobPinCounters = 1u << 7;
        private const uint JobPinVisualStates = 1u << 8;
        private const uint JobPinAudioSignals = 1u << 9;
        private const uint JobPinCavitationSignals = 1u << 10;
        private const float MinimumSignalIntensity = 0.01f;
        private const byte ToolAcousticStateSeaglidePropeller = 4;
        private const uint SeaglideFaultEventHash = 0x53474654u; // SGFT
        private const uint SeaglideFaultDumpHash = 0x53474450u; // SGDP
        private static SeaglideHydrodynamicsRuntime s_activeRuntimeInstance;

        private IDataVault _dataVault;
        private IPhysicsService _physicsService;
        private GlobalPhysicsStateManager _bodyResolver;
        private VaultGenerationHandle<SeaglideStateDTO> _statesHandle;
        private VaultGenerationHandle<SeaglidePropulsionRequestDTO> _requestsHandle;
        private VaultGenerationHandle<SeaglideForcePacketDTO> _forcePacketsHandle;
        private VaultGenerationHandle<SeaglideFlowSampleDTO> _flowSamplesHandle;
        private VaultGenerationHandle<SeaglideTuningDTO> _tuningHandle;
        private VaultGenerationHandle<SeaglideTelemetryEntry> _telemetryRingHandle;
        private VaultGenerationHandle<int> _telemetryCursorHandle;
        private VaultGenerationHandle<SeaglideCounterDTO> _countersHandle;
        private VaultGenerationHandle<SeaglideBodyBindingDTO> _bodyBindingsHandle;
        private VaultGenerationHandle<SeaglideVisualStateDTO> _visualStatesHandle;
        private VaultGenerationHandle<SeaglideAudioSignalDTO> _audioSignalsHandle;
        private VaultGenerationHandle<SeaglideCavitationVfxSignalDTO> _cavitationSignalsHandle;
        private JobHandle _pendingHandle;
        private long _scheduleTimestamp;
        private uint _simulationFrame;
        private int _activeRequestCount;
        private IDataVault _jobPinVault;
        private uint _jobPinMask;
        private bool _jobBuffersPinned;
        private float _metabolismAccumulator;
        private float _thrustCadenceAccumulator;
        private bool _jobScheduled;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _coldBootCompleted;
        private bool _dumpedFault;
        private bool _coreBlackboxWarmed;
        private bool _forcePacketsReadyToDrain;
        private bool _mockRequestsActive;
        private bool _runtimeActive;

        public static bool IsRuntimeAvailable => s_activeRuntimeInstance != null;

        public static bool TryGetActiveRuntime(out SeaglideHydrodynamicsRuntime runtime)
        {
            runtime = s_activeRuntimeInstance;
            return runtime != null;
        }

        public bool TryResolveEditorViews(
            out NativeArray<SeaglideTuningDTO>.ReadOnly tuning,
            out NativeArray<SeaglideCounterDTO>.ReadOnly counters,
            out NativeArray<SeaglideTelemetryEntry>.ReadOnly telemetry,
            out NativeArray<int>.ReadOnly cursor,
            out NativeArray<SeaglideAudioSignalDTO>.ReadOnly audio,
            out NativeArray<SeaglideCavitationVfxSignalDTO>.ReadOnly cavitation)
        {
            tuning = default;
            counters = default;
            telemetry = default;
            cursor = default;
            audio = default;
            cavitation = default;
            IDataVault vault = _dataVault;
            if (!TryReadOnlyVaultBuffer(vault, in _tuningHandle, out tuning) ||
                !TryReadOnlyVaultBuffer(vault, in _countersHandle, out counters) ||
                !TryReadOnlyVaultBuffer(vault, in _telemetryRingHandle, out telemetry) ||
                !TryReadOnlyVaultBuffer(vault, in _telemetryCursorHandle, out cursor) ||
                !TryReadOnlyVaultBuffer(vault, in _audioSignalsHandle, out audio) ||
                !TryReadOnlyVaultBuffer(vault, in _cavitationSignalsHandle, out cavitation))
            {
                tuning = default;
                counters = default;
                telemetry = default;
                cursor = default;
                audio = default;
                cavitation = default;
                return false;
            }

            return true;
        }

        public bool TryResolveForcePacketEditorView(out NativeArray<SeaglideForcePacketDTO>.ReadOnly forcePackets)
        {
            forcePackets = default;
            IDataVault vault = _dataVault;
            return TryReadOnlyVaultBuffer(vault, in _forcePacketsHandle, out forcePackets);
        }

        public bool TryApplyEditorTuning(float maxThrustN, float quadraticDragCoefficient, float flowForceCoefficient)
        {
            IDataVault vault = _dataVault;
            if (vault == null ||
                !vault.TryAcquireWriteLock(in _tuningHandle, SystemID.VehiclesPhysics, out NativeArray<SeaglideTuningDTO> tuning))
                return false;

            try
            {
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return false;

                SeaglideTuningDTO dto = tuning[0];
                dto.MaxThrustN = math.max(1f, math.select(dto.MaxThrustN, maxThrustN, math.isfinite(maxThrustN)));
                dto.QuadraticDragCoefficient = math.max(0f, math.select(dto.QuadraticDragCoefficient, quadraticDragCoefficient, math.isfinite(quadraticDragCoefficient)));
                dto.FlowForceCoefficient = math.max(0f, math.select(dto.FlowForceCoefficient, flowForceCoefficient, math.isfinite(flowForceCoefficient)));
                dto.ProfileHash = SeaglideHydrodynamicsConstants.SourceHash;
                tuning[0] = dto;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, SystemID.VehiclesPhysics);
            }
        }

        private void Awake()
        {
            if (!Application.isPlaying)
                return;

            if (s_activeRuntimeInstance == null)
                s_activeRuntimeInstance = this;
            RefreshColdDependencies();
            EnsureColdBooted();
        }

        private void OnEnable()
        {
            _runtimeActive = Application.isPlaying;
            if (!_runtimeActive)
                return;

            if (s_activeRuntimeInstance == null)
                s_activeRuntimeInstance = this;
            CompletePendingSolverForTeardown();
            RefreshColdDependencies();
            EnsureColdBooted();
            WarmCoreBlackboxRoute();
            TryRegister();
        }

        private void OnDisable()
        {
            if (!_runtimeActive)
                return;

            TryUnregister();
            CompletePendingSolverForTeardown();
            _forcePacketsReadyToDrain = false;
            _activeRequestCount = 0;
            _mockRequestsActive = false;
            _coreBlackboxWarmed = false;
            _runtimeActive = false;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            TryUnregister();
            CompletePendingSolverForTeardown();
            ReleaseVaultHandles(_dataVault);
            _coreBlackboxWarmed = false;
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!_runtimeActive || _jobScheduled)
                return;

            if (_forcePacketsReadyToDrain)
            {
                TryWriteTelemetryHeartbeatFromCachedVault(
                    _activeRequestCount,
                    1f,
                    SeaglideHydrodynamicsConstants.FlagTelemetryHeartbeat | SeaglideHydrodynamicsConstants.FlagForceQueued,
                    preserveCounters: true);
                return;
            }

            if (!math.isfinite(fixedDeltaTime) || fixedDeltaTime <= 0f)
            {
                ClearActiveRequestWindow();
                TryWriteTelemetryHeartbeatFromCachedVault(
                    0,
                    1f,
                    SeaglideHydrodynamicsConstants.FlagTelemetryHeartbeat | SeaglideHydrodynamicsConstants.FlagNonFinite);
                return;
            }

            float safeDelta = math.clamp(fixedDeltaTime, 0.0001f, 0.2f);
            if (!TryPrepareRuntimeVault(out IDataVault vault))
            {
                ClearActiveRequestWindow();
                TryWriteTelemetryHeartbeatFromCachedVault(
                    0,
                    1f,
                    SeaglideHydrodynamicsConstants.FlagTelemetryHeartbeat | SeaglideHydrodynamicsConstants.FlagBudgetExceeded);
                return;
            }

            if (!TryPinJobBuffers(vault))
            {
                ClearActiveRequestWindow();
                return;
            }

            bool scheduled = false;
            try
            {
                if (!TryResolveRuntimeBuffers(
                        vault,
                        out NativeArray<SeaglideStateDTO> states,
                        out NativeArray<SeaglidePropulsionRequestDTO> requests,
                        out NativeArray<SeaglideForcePacketDTO> forcePackets,
                        out NativeArray<SeaglideFlowSampleDTO> flowSamples,
                        out NativeArray<SeaglideTuningDTO> tuning,
                        out NativeArray<SeaglideTelemetryEntry> telemetry,
                        out NativeArray<int> telemetryCursor,
                        out NativeArray<SeaglideCounterDTO> counters,
                        out NativeArray<SeaglideVisualStateDTO> visualStates,
                        out NativeArray<SeaglideAudioSignalDTO> audioSignals,
                        out NativeArray<SeaglideCavitationVfxSignalDTO> cavitationSignals))
                {
                    ClearActiveRequestWindow();
                    return;
                }

                IngestPropulsionRequestSignals(states, requests);
                SeaglideTuningDTO tuningDto = tuning[0];
                float quality = ApplyResolvedGlobalQualityWeight(ref tuningDto);
                tuning[0] = tuningDto;
                int activeCount = math.clamp(_activeRequestCount, 0, math.min(states.Length, requests.Length));
                if (activeCount <= 0)
                {
                    _thrustCadenceAccumulator = 0f;
                    WriteTelemetryHeartbeat(
                        telemetry,
                        telemetryCursor,
                        counters,
                        activeCount,
                        quality,
                        SeaglideHydrodynamicsConstants.FlagTelemetryHeartbeat);
                    _simulationFrame++;
                    return;
                }

                _thrustCadenceAccumulator = math.min(_thrustCadenceAccumulator + safeDelta, 0.2f);
                float thrustCadenceSeconds = ResolveThrustCadenceSeconds(safeDelta, in tuningDto, quality);
                if (_thrustCadenceAccumulator + 0.00001f < thrustCadenceSeconds)
                {
                    WriteTelemetryHeartbeat(
                        telemetry,
                        telemetryCursor,
                        counters,
                        activeCount,
                        quality,
                        SeaglideHydrodynamicsConstants.FlagTelemetryHeartbeat | SeaglideHydrodynamicsConstants.FlagCadenceSkipped);
                    _simulationFrame++;
                    return;
                }

                float solverDelta = _thrustCadenceAccumulator;
                _thrustCadenceAccumulator = 0f;
                tuningDto.SectorAUP = requests[0].CurrentAUP;
                tuningDto.ResolvedQualityWeight = quality;
                tuningDto.GlobalQualityWeight = quality;
                tuningDto.SimulationTickDelta = solverDelta;
                tuningDto.ActiveRequestCount = activeCount;
                tuningDto.FrameIndex = _simulationFrame;
                tuning[0] = tuningDto;

                if (!PhysicsApplySystem.TryPrepareSeaglideForcePackets(forcePackets, counters))
                {
                    WriteTelemetryHeartbeat(
                        telemetry,
                        telemetryCursor,
                        counters,
                        activeCount,
                        quality,
                        SeaglideHydrodynamicsConstants.FlagTelemetryHeartbeat | SeaglideHydrodynamicsConstants.FlagBudgetExceeded);
                    _simulationFrame++;
                    return;
                }

                int metabolismEnabled = AdvanceMetabolismCadence(solverDelta, safeDelta, in tuningDto);
                _scheduleTimestamp = Stopwatch.GetTimestamp();
                CalculateSeaglideThrustJob thrustJob = new CalculateSeaglideThrustJob
                {
                    States = states,
                    Requests = requests,
                    FlowSamples = flowSamples,
                    Tuning = tuning,
                    ForcePackets = forcePackets,
                    VisualStates = visualStates,
                    CavitationSignals = cavitationSignals,
                    ActiveRequestCount = activeCount,
                    SimulationFrame = _simulationFrame,
                    SimulationTickDelta = solverDelta,
                    GlobalQualityWeight = quality
                };
                JobHandle thrustHandle = thrustJob.Schedule(activeCount, 64);

                ProcessSeaglideMetabolismJob metabolismJob = new ProcessSeaglideMetabolismJob
                {
                    States = states,
                    ForcePackets = forcePackets,
                    Tuning = tuning,
                    ActiveRequestCount = activeCount,
                    MetabolismEnabled = metabolismEnabled,
                    SimulationTickDelta = solverDelta
                };
                JobHandle metabolismHandle = metabolismJob.Schedule(activeCount, 64, thrustHandle);

                CalculateSeaglideAudioParametersJob audioJob = new CalculateSeaglideAudioParametersJob
                {
                    Requests = requests,
                    ForcePackets = forcePackets,
                    Tuning = tuning,
                    AudioSignals = audioSignals,
                    ActiveRequestCount = activeCount
                };
                JobHandle audioHandle = audioJob.Schedule(activeCount, 64, thrustHandle);
                JobHandle reduceDependency = JobHandle.CombineDependencies(metabolismHandle, audioHandle);

                ReduceSeaglideTelemetryJob reduceJob = new ReduceSeaglideTelemetryJob
                {
                    ForcePackets = forcePackets,
                    States = states,
                    Counters = counters,
                    TelemetryRing = telemetry,
                    TelemetryCursor = telemetryCursor,
                    ActiveRequestCount = activeCount,
                    SimulationFrame = _simulationFrame,
                    GlobalQualityWeight = quality,
                    ComputeMicros = 0f,
                    MetabolismEnabled = metabolismEnabled
                };
                _pendingHandle = reduceJob.Schedule(reduceDependency);
                _jobScheduled = true;
                scheduled = true;
                H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, _pendingHandle);
            }
            finally
            {
                if (!scheduled)
                    UnlockJobBuffers();
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!_runtimeActive)
                return;

            TryFinalizePendingSolverNoWait();
            if (!_forcePacketsReadyToDrain)
                return;

            IDataVault vault = _dataVault;
            if (!TryReadOnlyVaultBuffer(vault, in _forcePacketsHandle, out NativeArray<SeaglideForcePacketDTO>.ReadOnly forcePackets) ||
                !TryReadOnlyVaultBuffer(vault, in _countersHandle, out NativeArray<SeaglideCounterDTO>.ReadOnly counters) ||
                !TryReadOnlyVaultBuffer(vault, in _bodyBindingsHandle, out NativeArray<SeaglideBodyBindingDTO>.ReadOnly bodyBindings))
            {
                _forcePacketsReadyToDrain = false;
                return;
            }

            PhysicsApplySystem.DrainSeaglideForcePackets(
                _physicsService,
                _bodyResolver,
                forcePackets,
                counters,
                bodyBindings,
                SeaglideHydrodynamicsConstants.ForceQueueSoftCapacity,
                out int accepted,
                out int unresolved);
            RecordForceDrainResult(accepted, unresolved);
            _forcePacketsReadyToDrain = false;
        }

        public void LateFrameTick()
        {
            if (_jobScheduled)
                TryFinalizePendingSolverNoWait();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Physics ||
                serviceSlot == GlobalRegistryServiceSlot.PhysicsStateManager)
            {
                RefreshColdDependencies();
                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompletePendingSolverForTeardown();
            IDataVault previousVault = (previousService as IDataVault) ?? _dataVault;
            IDataVault currentVault = currentService as IDataVault;
            if (!ReferenceEquals(previousVault, currentVault))
                ReleaseVaultHandles(previousVault);
            _dataVault = currentVault;
            _coldBootCompleted = false;
            if (currentVault != null && !currentVault.IsAllocationLocked && !currentVault.IsCompactionFenceActive)
                EnsureColdBooted();
        }

#if UNITY_EDITOR
        public bool GenerateMockPropulsionRequests()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || vault.IsCompactionFenceActive || _jobScheduled || !EnsureVaultBuffers())
                return false;

            if (!TryReadOnlyVaultBuffer(vault, in _tuningHandle, out NativeArray<SeaglideTuningDTO>.ReadOnly tuningRead))
                return false;

            SeaglideTuningDTO tuningDto = tuningRead[0];
            int mockCount = math.clamp(
                tuningDto.MockRequestCount > 0 ? tuningDto.MockRequestCount : SeaglideHydrodynamicsConstants.MockRequestCount,
                1,
                math.min(SeaglideHydrodynamicsConstants.StateCapacity, SeaglideHydrodynamicsConstants.MockRequestCount));

            if (!TryPinJobBuffers(vault))
                return false;

            try
            {
                NativeArray<SeaglideStateDTO> states = ResolveVaultBuffer(vault, in _statesHandle);
                NativeArray<SeaglidePropulsionRequestDTO> requests = ResolveVaultBuffer(vault, in _requestsHandle);
                NativeArray<SeaglideTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
                if (!states.IsCreated || !requests.IsCreated || !tuning.IsCreated || tuning.Length <= 0)
                    return false;

                mockCount = math.clamp(mockCount, 1, math.min(states.Length, requests.Length));
                GenerateMockSeaglidePropulsionDataJob job = new GenerateMockSeaglidePropulsionDataJob
                {
                    States = states,
                    Requests = requests,
                    ActiveMockCount = mockCount,
                    OriginAUP = tuningDto.SectorAUP,
                    SimulationFrame = _simulationFrame
                };
                // COLD/EDITOR DIRECT SEED: avoid scheduling a job that is completed in the same method.
                for (int i = 0; i < mockCount; i++)
                    job.Execute(i);
                tuningDto.ActiveRequestCount = mockCount;
                tuningDto.MockRequestCount = mockCount;
                tuning[0] = tuningDto;
                _activeRequestCount = mockCount;
                _mockRequestsActive = true;
                return true;
            }
            finally
            {
                UnlockJobBuffers();
            }
        }
#endif

        private void IngestPropulsionRequestSignals(
            NativeArray<SeaglideStateDTO> states,
            NativeArray<SeaglidePropulsionRequestDTO> requests)
        {
            ReadOnlySpan<SeaglidePropulsionRequestSignal> signals = SignalBus<SeaglidePropulsionRequestSignal>.GetFrameSnapshot();
            if (!states.IsCreated || !requests.IsCreated)
            {
                ClearActiveRequestWindow();
                return;
            }

            if (signals.Length <= 0)
            {
                if (_mockRequestsActive && _activeRequestCount > 0)
                    return;

                ClearActiveRequestWindow();
                return;
            }

            int limit = math.min(signals.Length, math.min(states.Length, requests.Length));
            int accepted = 0;
            for (int i = 0; i < limit; i++)
            {
                if (!TryBuildPropulsionRequestFromSignal(
                        in signals[i],
                        accepted,
                        out SeaglidePropulsionRequestDTO request,
                        out SeaglideStateDTO state))
                {
                    continue;
                }

                requests[accepted] = request;
                states[accepted] = state;
                accepted++;
            }

            _activeRequestCount = accepted;
            _mockRequestsActive = false;
        }

        private void ClearActiveRequestWindow()
        {
            _activeRequestCount = 0;
            _mockRequestsActive = false;
        }

        private bool TryBuildPropulsionRequestFromSignal(
            in SeaglidePropulsionRequestSignal signal,
            int stateIndex,
            out SeaglidePropulsionRequestDTO request,
            out SeaglideStateDTO state)
        {
            request = signal.Request;
            state = default;
            uint targetHash = signal.TargetEntityHash != 0u ? signal.TargetEntityHash : request.TargetEntityHash;
            uint flags = request.Flags | signal.Flags | SeaglideHydrodynamicsConstants.FlagActive;
            if (stateIndex < 0 ||
                targetHash == 0u ||
                !math.all(math.isfinite(request.CurrentAUP)) ||
                !math.all(math.isfinite(request.PreviousAUP)) ||
                !math.all(math.isfinite(request.InputVector)) ||
                !math.all(math.isfinite(request.ForwardVector)) ||
                !math.all(math.isfinite(request.SurfaceNormal)) ||
                !math.all(math.isfinite(signal.Velocity)) ||
                !math.isfinite(request.Throttle01) ||
                !math.isfinite(request.DeltaTime) ||
                request.DeltaTime <= 0f ||
                (!math.isfinite(request.BatteryLevel) && !math.isfinite(signal.BatteryLevel)))
            {
                request = default;
                return false;
            }

            float batteryLevel = math.select(request.BatteryLevel, signal.BatteryLevel, math.isfinite(signal.BatteryLevel));
            float massKg = math.select(SeaglideHydrodynamicsConstants.DefaultBaseMassKg, signal.MassKg, math.isfinite(signal.MassKg) && signal.MassKg > 0f);
            float addedMassKg = math.select(SeaglideHydrodynamicsConstants.DefaultAddedMassKg, signal.AddedMassKg, math.isfinite(signal.AddedMassKg) && signal.AddedMassKg >= 0f);
            request.TargetEntityHash = targetHash;
            request.RequestHash = request.RequestHash != 0u ? request.RequestHash : SeaglideHydrodynamicsConstants.SourceHash;
            request.FrameIndex = _simulationFrame;
            request.Flags = flags;
            request.BatteryLevel = math.saturate(batteryLevel);

            state.CurrentAUP = request.CurrentAUP;
            state.Velocity = signal.Velocity;
            state.BatteryLevel = request.BatteryLevel;
            state.ActiveFlags = request.Flags;
            state.TargetEntityHash = targetHash;
            state.MassKg = math.max(1f, massKg);
            state.AddedMassKg = math.max(0f, addedMassKg);
            state.FrameIndex = _simulationFrame;
            return true;
        }

        private bool EnsureColdBooted()
        {
            if (_coldBootCompleted)
                return true;

            if (!EnsureVaultBuffers())
                return false;

            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryPinJobBuffers(vault))
                return false;

            try
            {
                NativeArray<SeaglideFlowSampleDTO> flowSamples = ResolveVaultBuffer(vault, in _flowSamplesHandle);
                NativeArray<SeaglideTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
                NativeArray<int> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
                NativeArray<SeaglideCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
                NativeArray<SeaglideBodyBindingDTO> bodyBindings = ResolveVaultBuffer(vault, in _bodyBindingsHandle);
                NativeArray<SeaglideVisualStateDTO> visualStates = ResolveVaultBuffer(vault, in _visualStatesHandle);
                NativeArray<SeaglideAudioSignalDTO> audioSignals = ResolveVaultBuffer(vault, in _audioSignalsHandle);
                NativeArray<SeaglideCavitationVfxSignalDTO> cavitationSignals = ResolveVaultBuffer(vault, in _cavitationSignalsHandle);
                InitializeSeaglideColdBuffersJob initJob = new InitializeSeaglideColdBuffersJob
                {
                    FlowSamples = flowSamples,
                    TelemetryRing = telemetry,
                    TelemetryCursor = cursor,
                    Counters = counters,
                    BodyBindings = bodyBindings,
                    VisualStates = visualStates,
                    AudioSignals = audioSignals,
                    CavitationSignals = cavitationSignals
                };
                // COLD BOOT DIRECT CLEAR: one-time Vault reset before runtime scheduling starts.
                initJob.Execute();
            }
            finally
            {
                UnlockJobBuffers();
            }

            EnsureSeaglideSignalLanes();
            SeedDefaultTuningIfNeeded();
#if UNITY_EDITOR
            TryLoadVehicleProfileCsvCold();
#endif
            TryBindPlayerBodyCold();
            _coldBootCompleted = true;
            return true;
        }

        private bool TryPrepareRuntimeVault(out IDataVault vault)
        {
            vault = _dataVault;
            if (!_coldBootCompleted || vault == null)
                return false;

            return true;
        }

        private void RefreshColdDependencies()
        {
            _dataVault = GlobalRegistry.DataVault;
            _physicsService = GlobalRegistry.Physics;
            _bodyResolver = GlobalRegistry.PhysicsStateManager;
            if (_coldBootCompleted)
                TryBindPlayerBodyCold();
        }

        private bool TryBindPlayerBodyCold()
        {
            IDataVault vault = _dataVault;
            GlobalPhysicsStateManager bodyResolver = _bodyResolver;
            if (vault == null ||
                bodyResolver == null ||
                !HasHandle(in _bodyBindingsHandle))
            {
                return false;
            }

            if (!GlobalPhysicsStateManager.TryFindTrackedBodyByFoldedEntityHash(
                    bodyResolver,
                    SeaglideHydrodynamicsConstants.PlayerBodyTargetHash,
                    out _,
                    out int bodyIndex))
            {
                return false;
            }

            if (!TryPinJobBuffers(vault))
                return false;

            try
            {
                NativeArray<SeaglideBodyBindingDTO> bodyBindings = ResolveVaultBuffer(vault, in _bodyBindingsHandle);
                if (!bodyBindings.IsCreated || bodyBindings.Length <= 0)
                    return false;

                SeaglideBodyBindingDTO binding = default;
                binding.TargetEntityHash = SeaglideHydrodynamicsConstants.PlayerBodyTargetHash;
                binding.RigidbodyIndex = bodyIndex;
                binding.Flags = SeaglideHydrodynamicsConstants.FlagActive;

                int bindingCount = math.min(bodyBindings.Length, SeaglideHydrodynamicsConstants.StateCapacity);
                for (int i = 0; i < bindingCount; i++)
                {
                    binding.StateIndex = i;
                    bodyBindings[i] = binding;
                }

                return bindingCount > 0;
            }
            finally
            {
                UnlockJobBuffers();
            }
        }

        private bool EnsureVaultBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            return EnsureVaultDescriptor(vault, ref _statesHandle, SeaglideHydrodynamicsBufferIds.States, SeaglideHydrodynamicsConstants.StateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _requestsHandle, SeaglideHydrodynamicsBufferIds.Requests, SeaglideHydrodynamicsConstants.RequestCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _forcePacketsHandle, SeaglideHydrodynamicsBufferIds.ForcePackets, SeaglideHydrodynamicsConstants.StateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _flowSamplesHandle, SeaglideHydrodynamicsBufferIds.FlowSamples, SeaglideHydrodynamicsConstants.FlowSampleCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _tuningHandle, SeaglideHydrodynamicsBufferIds.Tuning, SeaglideHydrodynamicsConstants.TuningCapacity, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _telemetryRingHandle, SeaglideHydrodynamicsBufferIds.TelemetryRing, SeaglideHydrodynamicsConstants.TelemetryCapacity, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _telemetryCursorHandle, SeaglideHydrodynamicsBufferIds.TelemetryCursor, 1, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _countersHandle, SeaglideHydrodynamicsBufferIds.Counters, 1, NativeArrayOptions.ClearMemory) &&
                   EnsureVaultDescriptor(vault, ref _bodyBindingsHandle, SeaglideHydrodynamicsBufferIds.BodyBindings, SeaglideHydrodynamicsConstants.StateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _visualStatesHandle, SeaglideHydrodynamicsBufferIds.VisualStates, SeaglideHydrodynamicsConstants.StateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _audioSignalsHandle, SeaglideHydrodynamicsBufferIds.AudioSignals, SeaglideHydrodynamicsConstants.StateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _cavitationSignalsHandle, SeaglideHydrodynamicsBufferIds.CavitationSignals, SeaglideHydrodynamicsConstants.StateCapacity, NativeArrayOptions.UninitializedMemory);
        }

        private static bool EnsureVaultDescriptor<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            if (vault == null || requiredLength <= 0)
                return false;

            if (HasHandle(in handle) &&
                vault.TryResolveHandle(in handle, out NativeArray<T> existing) &&
                existing.IsCreated &&
                existing.Length >= requiredLength)
            {
                return true;
            }

            if (vault.TryGetGenerationHandle<T>(bufferId, out VaultGenerationHandle<T> existingHandle) &&
                HasHandle(in existingHandle) &&
                vault.TryResolveHandle(in existingHandle, out NativeArray<T> existingBuffer) &&
                existingBuffer.IsCreated &&
                existingBuffer.Length >= requiredLength)
            {
                handle = existingHandle;
                return true;
            }

            if (vault.IsAllocationLocked || vault.IsCompactionFenceActive)
                return false;

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.VehiclesPhysics, options);
            return HasHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> resolved) &&
                   resolved.IsCreated &&
                   resolved.Length >= requiredLength;
        }

        private bool TryResolveRuntimeBuffers(
            IDataVault vault,
            out NativeArray<SeaglideStateDTO> states,
            out NativeArray<SeaglidePropulsionRequestDTO> requests,
            out NativeArray<SeaglideForcePacketDTO> forcePackets,
            out NativeArray<SeaglideFlowSampleDTO> flowSamples,
            out NativeArray<SeaglideTuningDTO> tuning,
            out NativeArray<SeaglideTelemetryEntry> telemetry,
            out NativeArray<int> telemetryCursor,
            out NativeArray<SeaglideCounterDTO> counters,
            out NativeArray<SeaglideVisualStateDTO> visualStates,
            out NativeArray<SeaglideAudioSignalDTO> audioSignals,
            out NativeArray<SeaglideCavitationVfxSignalDTO> cavitationSignals)
        {
            states = ResolveVaultBuffer(vault, in _statesHandle);
            requests = ResolveVaultBuffer(vault, in _requestsHandle);
            forcePackets = ResolveVaultBuffer(vault, in _forcePacketsHandle);
            flowSamples = ResolveVaultBuffer(vault, in _flowSamplesHandle);
            tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            telemetryCursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            counters = ResolveVaultBuffer(vault, in _countersHandle);
            visualStates = ResolveVaultBuffer(vault, in _visualStatesHandle);
            audioSignals = ResolveVaultBuffer(vault, in _audioSignalsHandle);
            cavitationSignals = ResolveVaultBuffer(vault, in _cavitationSignalsHandle);
            return states.IsCreated &&
                   requests.IsCreated &&
                   forcePackets.IsCreated &&
                   flowSamples.IsCreated &&
                   tuning.IsCreated &&
                   telemetry.IsCreated &&
                   telemetryCursor.IsCreated &&
                   counters.IsCreated &&
                   visualStates.IsCreated &&
                   audioSignals.IsCreated &&
                   cavitationSignals.IsCreated &&
                   tuning.Length > 0 &&
                   telemetry.Length >= SeaglideHydrodynamicsConstants.TelemetryCapacity &&
                   telemetryCursor.Length > 0 &&
                   counters.Length > 0;
        }

        private bool TryFinalizePendingSolverNoWait()
        {
            if (!_jobScheduled)
                return true;

            if (!_pendingHandle.IsCompleted)
                return false;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return false;

            return FinishPendingSolverCompletion();
        }

        private bool CompletePendingSolverForTeardown()
        {
            if (!_jobScheduled)
                return true;

            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                    return false;
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }

            return FinishPendingSolverCompletion();
        }

        private bool FinishPendingSolverCompletion()
        {
            _jobScheduled = false;
            try
            {
                H8Memory.RegisterActiveJob(SystemID.VehiclesPhysics, default);
            }
            finally
            {
                UnlockJobBuffers();
            }

            float micros = ResolveElapsedMicros(_scheduleTimestamp);
            WriteCompletedComputeMicros(micros);
            PublishCompletedPresentationSignals();
            if (!_dumpedFault && TryLatestCounterHasFault())
            {
                DumpBlackBoxOnce();
                _dumpedFault = true;
            }

            _simulationFrame++;
            ClearActiveRequestWindow();
            _forcePacketsReadyToDrain = true;
            return true;
        }

        private static void EnsureSeaglideSignalLanes()
        {
            SignalBus<SeaglidePropulsionRequestSignal>.Configure(
                SeaglidePropulsionRequestSignal.ExpectedCapacity,
                SeaglidePropulsionRequestSignal.MaxFrameSignals,
                SeaglidePropulsionRequestSignal.LowTierFrameSignals,
                SeaglidePropulsionRequestSignal.LaneHash);
            SignalBus<SeaglidePropulsionRequestSignal>.EnsureInitialized();
            SignalBus<ToolAcousticSignal>.Configure(
                ToolAcousticSignal.ExpectedCapacity,
                ToolAcousticSignal.MaxFrameSignals,
                ToolAcousticSignal.LowTierFrameSignals,
                ToolAcousticSignal.LaneHash);
            SignalBus<ToolAcousticSignal>.EnsureInitialized();
            SignalBus<BubbleSpawnSignal>.Configure(
                BubbleSpawnSignal.ExpectedCapacity,
                maxFrameSignals: BubbleSpawnSignal.MaxFrameSignals,
                lowTierFrameSignals: BubbleSpawnSignal.LowTierFrameSignals,
                laneHash: BubbleSpawnSignal.LaneHash);
            SignalBus<BubbleSpawnSignal>.EnsureInitialized();
        }

        private static uint ComputeStableSignalLaneHash(string label)
        {
            const uint fnvOffset = 2166136261u;
            const uint fnvPrime = 16777619u;
            uint hash = fnvOffset;
            if (!string.IsNullOrEmpty(label))
            {
                for (int i = 0; i < label.Length; i++)
                {
                    hash ^= label[i];
                    hash *= fnvPrime;
                }
            }

            return hash != 0u ? hash : 1u;
        }

        private bool TryWriteTelemetryHeartbeatFromCachedVault(int activeCount, float quality, uint flags, bool preserveCounters = false)
        {
            IDataVault vault = _dataVault;
            if (!_coldBootCompleted || vault == null || _jobScheduled)
                return false;

            if (!TryPinJobBuffers(vault))
                return false;

            try
            {
                NativeArray<SeaglideTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
                NativeArray<int> telemetryCursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
                NativeArray<SeaglideCounterDTO> counters = preserveCounters ? default : ResolveVaultBuffer(vault, in _countersHandle);
                if (!telemetry.IsCreated || telemetry.Length <= 0 || !telemetryCursor.IsCreated || telemetryCursor.Length <= 0)
                    return false;

                WriteTelemetryHeartbeat(telemetry, telemetryCursor, counters, activeCount, quality, flags);
                _simulationFrame++;
                return true;
            }
            finally
            {
                UnlockJobBuffers();
            }
        }

        private void WriteTelemetryHeartbeat(
            NativeArray<SeaglideTelemetryEntry> telemetry,
            NativeArray<int> telemetryCursor,
            NativeArray<SeaglideCounterDTO> counters,
            int activeCount,
            float quality,
            uint flags)
        {
            if (!telemetry.IsCreated ||
                telemetry.Length <= 0 ||
                !telemetryCursor.IsCreated ||
                telemetryCursor.Length <= 0)
            {
                return;
            }

            int writeIndex = math.clamp(telemetryCursor[0], 0, telemetry.Length - 1);
            float safeQuality = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            SeaglideTelemetryEntry entry = default;
            entry.FrameIndex = _simulationFrame;
            entry.EvaluatedRequests = math.max(0, activeCount);
            entry.GlobalQualityWeight = safeQuality;
            entry.Flags = flags;
            telemetry[writeIndex] = entry;
            telemetryCursor[0] = (writeIndex + 1) % telemetry.Length;

            if (!counters.IsCreated || counters.Length <= 0)
                return;

            SeaglideCounterDTO counter = default;
            counter.EvaluatedRequests = entry.EvaluatedRequests;
            counter.GlobalQualityWeight = safeQuality;
            counter.Flags = flags;
            counters[0] = counter;
        }

        private void PublishCompletedPresentationSignals()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!TryReadOnlyVaultBuffer(vault, in _countersHandle, out NativeArray<SeaglideCounterDTO>.ReadOnly counters) ||
                !TryReadOnlyVaultBuffer(vault, in _audioSignalsHandle, out NativeArray<SeaglideAudioSignalDTO>.ReadOnly audioSignals) ||
                !TryReadOnlyVaultBuffer(vault, in _cavitationSignalsHandle, out NativeArray<SeaglideCavitationVfxSignalDTO>.ReadOnly cavitationSignals))
            {
                return;
            }

            SeaglideCounterDTO counter = counters[0];
            int packetCount = math.clamp(counter.ForcePackets, 0, math.min(audioSignals.Length, cavitationSignals.Length));
            if (packetCount <= 0)
                return;

            int publishBudget = ResolvePresentationSignalBudget(counter.GlobalQualityWeight, packetCount);
            int scanWindow = math.clamp(counter.EvaluatedRequests, 0, math.min(audioSignals.Length, cavitationSignals.Length));
            if (scanWindow <= 0)
                scanWindow = packetCount;

            int published = 0;
            for (int i = 0; i < scanWindow && published < publishBudget; i++)
            {
                SeaglideAudioSignalDTO audioSignal = audioSignals[i];
                SeaglideCavitationVfxSignalDTO cavitationSignal = cavitationSignals[i];
                bool emitted = PublishAudioSignal(in audioSignal);
                emitted |= PublishBubbleSignal(in cavitationSignal, counter.GlobalQualityWeight);
                if (emitted)
                    published++;
            }
        }

        private static int ResolvePresentationSignalBudget(float quality, int packetCount)
        {
            float smoothQuality = math.saturate(quality);
            smoothQuality = smoothQuality * smoothQuality * (3f - (2f * smoothQuality));
            int maxBudget = 1 + (int)math.floor(smoothQuality * 3.999f);
            return math.clamp(maxBudget, 1, math.max(1, packetCount));
        }

        private static bool PublishAudioSignal(in SeaglideAudioSignalDTO source)
        {
            if (source.SourceHash == 0u ||
                source.TargetEntityHash == 0u ||
                !math.isfinite(source.PitchScalar) ||
                !math.isfinite(source.VolumeScalar) ||
                source.VolumeScalar <= MinimumSignalIntensity)
            {
                return false;
            }

            ToolAcousticSignal signal = default;
            signal.ToolHash = source.SourceHash;
            signal.TargetHash = source.TargetEntityHash;
            signal.Progress01 = math.saturate(source.Cavitation01);
            signal.PitchScale = math.clamp(source.PitchScalar, 0.25f, 4f);
            signal.Intensity01 = math.saturate(source.VolumeScalar);
            signal.Frame = source.FrameIndex;
            signal.State = ToolAcousticStateSeaglidePropeller;
            signal.Flags = ToolAcousticSignal.FlagLooping;
            return SignalBus<ToolAcousticSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_SeaglideHydrodynamicsRuntime);
        }

        private static bool PublishBubbleSignal(in SeaglideCavitationVfxSignalDTO source, float quality)
        {
            float intensity = math.saturate(source.Intensity01 * math.lerp(0.25f, 1f, math.saturate(quality)));
            if (source.SourceHash == 0u ||
                intensity <= MinimumSignalIntensity ||
                !math.all(math.isfinite(source.CurrentAUP)) ||
                !math.all(math.isfinite(source.Direction)) ||
                !math.isfinite(source.RadiusMeters))
            {
                return false;
            }

            BubbleSpawnSignal signal = default;
            signal.PositionAup = Hecton8.World.AbsoluteUniversePosition.FromAbsolutePosition(source.CurrentAUP);
            signal.Direction = SafeSignalDirection(source.Direction);
            signal.Intensity01 = intensity;
            signal.RadiusMeters = math.clamp(source.RadiusMeters, 0.05f, 8f);
            signal.Frame = source.FrameIndex;
            signal.SourceHash = source.SourceHash;
            signal.Flags = BubbleSpawnSignal.FlagEngineVent;
            return SignalBus<BubbleSpawnSignal>.TryPushTracked(in signal, ref s_x001DirectSignalPushDropCount_SeaglideHydrodynamicsRuntime);
        }

        private static float3 SafeSignalDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            return math.select(new float3(0f, 0f, 1f), direction * math.rsqrt(math.max(lengthSq, 0.000001f)), math.isfinite(lengthSq) && lengthSq > 0.000001f);
        }

        private void RecordForceDrainResult(int accepted, int unresolved)
        {
            if (unresolved <= 0)
                return;

            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (TryPinJobBuffers(vault))
            {
                try
                {
                    int evaluatedRequests = accepted + unresolved;
                    NativeArray<SeaglideCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
                    if (counters.IsCreated && counters.Length > 0)
                    {
                        SeaglideCounterDTO counter = counters[0];
                        counter.Flags |= SeaglideHydrodynamicsConstants.FlagBodyBindingUnresolved;
                        counter.EvaluatedRequests = math.max(counter.EvaluatedRequests, evaluatedRequests);
                        counters[0] = counter;
                    }

                    NativeArray<SeaglideTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
                    NativeArray<int> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
                    if (telemetry.IsCreated && telemetry.Length > 0 && cursor.IsCreated && cursor.Length > 0)
                    {
                        int lastIndex = cursor[0] - 1;
                        if (lastIndex < 0)
                            lastIndex += telemetry.Length;
                        if ((uint)lastIndex < (uint)telemetry.Length)
                        {
                            SeaglideTelemetryEntry entry = telemetry[lastIndex];
                            entry.Flags |= SeaglideHydrodynamicsConstants.FlagBodyBindingUnresolved;
                            entry.EvaluatedRequests = math.max(entry.EvaluatedRequests, evaluatedRequests);
                            telemetry[lastIndex] = entry;
                        }
                    }
                }
                finally
                {
                    UnlockJobBuffers();
                }
            }

            if (!_dumpedFault)
            {
                DumpBlackBoxOnce();
                _dumpedFault = true;
            }
        }

        private void WriteCompletedComputeMicros(float micros)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!TryPinJobBuffers(vault))
                return;

            try
            {
                NativeArray<SeaglideCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
                if (counters.IsCreated && counters.Length > 0)
                {
                    SeaglideCounterDTO counter = counters[0];
                    counter.ComputeMicros = micros;
                    counter.Flags |= math.select(0u, SeaglideHydrodynamicsConstants.FlagBudgetExceeded, micros > 500f);
                    counters[0] = counter;
                }

                NativeArray<SeaglideTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
                NativeArray<int> cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
                if (!telemetry.IsCreated || telemetry.Length <= 0 || !cursor.IsCreated || cursor.Length <= 0)
                    return;

                int lastIndex = cursor[0] - 1;
                if (lastIndex < 0)
                    lastIndex += telemetry.Length;
                if ((uint)lastIndex >= (uint)telemetry.Length)
                    return;

                SeaglideTelemetryEntry entry = telemetry[lastIndex];
                entry.ComputeMicros = micros;
                entry.Flags |= math.select(0u, SeaglideHydrodynamicsConstants.FlagBudgetExceeded, micros > 500f);
                telemetry[lastIndex] = entry;
            }
            finally
            {
                UnlockJobBuffers();
            }
        }

        private bool TryLatestCounterHasFault()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            if (!TryReadOnlyVaultBuffer(vault, in _countersHandle, out NativeArray<SeaglideCounterDTO>.ReadOnly counters))
                return false;

            SeaglideCounterDTO counter = counters[0];
            return (counter.Flags & (SeaglideHydrodynamicsConstants.FlagNonFinite | SeaglideHydrodynamicsConstants.FlagBudgetExceeded | SeaglideHydrodynamicsConstants.FlagBodyBindingUnresolved)) != 0u ||
                   counter.NonFiniteCount > 0;
        }

        private void DumpBlackBoxOnce()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!TryReadOnlyVaultBuffer(vault, in _telemetryRingHandle, out NativeArray<SeaglideTelemetryEntry>.ReadOnly telemetry))
                return;

            if (!_coreBlackboxWarmed || GlobalTelemetryBus.BlackboxActiveFrameCount <= 0)
                return;

            int index = (int)(_simulationFrame % (uint)telemetry.Length);
            SeaglideTelemetryEntry entry = telemetry[index];
            float scalar = math.isfinite(entry.MaxForceMagnitude) ? entry.MaxForceMagnitude : 0f;
            GlobalTelemetryBus.PushEvent(SeaglideFaultEventHash, scalar, entry.LastTargetEntityHash);
            _ = GlobalTelemetryBus.TryDumpBlackboxNow(SeaglideFaultDumpHash);
        }

        private void WarmCoreBlackboxRoute()
        {
            if (_coreBlackboxWarmed || !Application.isPlaying)
                return;

            GlobalTelemetryBus.Initialize();
            _coreBlackboxWarmed = GlobalTelemetryBus.BlackboxActiveFrameCount > 0;
        }

        private void SeedDefaultTuningIfNeeded()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            if (!vault.TryAcquireWriteLock(in _tuningHandle, SystemID.VehiclesPhysics, out NativeArray<SeaglideTuningDTO> tuning))
                return;

            try
            {
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return;

                SeaglideTuningDTO value = tuning[0];
                if (!math.isfinite(value.WaterDensityKgPerM3) ||
                    value.WaterDensityKgPerM3 < 1f ||
                    !math.isfinite(value.MaxThrustN) ||
                    value.MaxThrustN <= 0f ||
                    !math.isfinite(value.GlobalQualityWeight))
                {
                    value = SeaglideTuningDTO.Default();
                }

                value.GlobalQualityWeight = ApplyResolvedGlobalQualityWeight(ref value);
                tuning[0] = value;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, SystemID.VehiclesPhysics);
            }
        }

#if UNITY_EDITOR
        private bool TryLoadVehicleProfileCsvCold()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return false;

            string csvPath = ResolveVehicleProfileCsvPath(projectRoot);
            if (string.IsNullOrEmpty(csvPath))
                return false;

            Span<byte> scratch = stackalloc byte[SeaglideHydrodynamicsConstants.CsvScratchBytes];
            int length;
            using (FileStream stream = new FileStream(csvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (stream.Length <= 0L || stream.Length > scratch.Length)
                    return false;

                length = (int)stream.Length;
                Span<byte> destination = scratch.Slice(0, length);
                int total = 0;
                while (total < length)
                {
                    int read = stream.Read(destination.Slice(total));
                    if (read <= 0)
                        return false;

                    total += read;
                }
            }

            if (!TryReadOnlyVaultBuffer(vault, in _tuningHandle, out NativeArray<SeaglideTuningDTO>.ReadOnly tuningRead))
                return false;

            ReadOnlySpan<byte> bytes = scratch.Slice(0, length);
            SeaglideTuningDTO value = tuningRead[0];
            if (!SeaglideVehicleProfileCsv.TryApplyFirstProfile(bytes, ref value))
                return false;

            value.GlobalQualityWeight = ApplyResolvedGlobalQualityWeight(ref value);

            if (!vault.TryAcquireWriteLock(in _tuningHandle, SystemID.VehiclesPhysics, out NativeArray<SeaglideTuningDTO> tuning))
                return false;

            try
            {
                if (!tuning.IsCreated || tuning.Length <= 0)
                    return false;

                tuning[0] = value;
                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in _tuningHandle, SystemID.VehiclesPhysics);
            }
        }

        private static string ResolveVehicleProfileCsvPath(string projectRoot)
        {
            if (string.IsNullOrEmpty(projectRoot))
                return null;

            string primary = Path.Combine(projectRoot, SeaglideHydrodynamicsConstants.CsvRelativePath);
            if (File.Exists(primary))
                return primary;

            string legacy = Path.Combine(projectRoot, SeaglideHydrodynamicsConstants.LegacyCsvRelativePath);
            return File.Exists(legacy) ? legacy : null;
        }
#endif

        private int AdvanceMetabolismCadence(float deltaTime, float fixedDeltaTime, in SeaglideTuningDTO tuning)
        {
            float quality = MathLodApproximation.SaturateFinite(tuning.GlobalQualityWeight, SeaglideSimdMath.AuthoritativeQualityWeight);
            float cadence = ResolveContinuousCadenceSeconds(fixedDeltaTime, in tuning, quality);
            _metabolismAccumulator = math.min(_metabolismAccumulator + math.max(0f, deltaTime), 0.2f);
            if (_metabolismAccumulator + 0.00001f < cadence)
                return 0;

            _metabolismAccumulator = math.clamp(_metabolismAccumulator - cadence, 0f, cadence);
            return 1;
        }

        private static float ResolveThrustCadenceSeconds(float fixedDeltaTime, in SeaglideTuningDTO tuning, float quality)
        {
            return ResolveContinuousCadenceSeconds(fixedDeltaTime, in tuning, quality);
        }

        private static float ResolveContinuousCadenceSeconds(float fixedDeltaTime, in SeaglideTuningDTO tuning, float quality)
        {
            float safeFixedDelta = math.clamp(fixedDeltaTime, 0.0001f, 0.05f);
            float safeQuality = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            float authoredMinCadence = math.select(safeFixedDelta, tuning.MinimumCadenceSeconds, math.isfinite(tuning.MinimumCadenceSeconds));
            float authoredMaxCadence = math.select(authoredMinCadence * 4f, tuning.MaximumCadenceSeconds, math.isfinite(tuning.MaximumCadenceSeconds));
            float minCadence = math.max(safeFixedDelta, authoredMinCadence);
            float compensatedMaxCadence = math.min(0.2f, safeFixedDelta * 4f);
            float maxCadence = math.max(minCadence, math.min(compensatedMaxCadence, authoredMaxCadence));
            float smoothQuality = safeQuality * safeQuality * (3f - (2f * safeQuality));
            return math.lerp(maxCadence, minCadence, smoothQuality);
        }

        private static float ApplyResolvedGlobalQualityWeight(ref SeaglideTuningDTO tuning)
        {
            float homeostasis = HomeostasisBrain.GlobalQualityWeight;
            float quality = math.saturate(math.select(SeaglideSimdMath.AuthoritativeQualityWeight, homeostasis, math.isfinite(homeostasis)));
            if (MathLodRuntimeConfig.TryReadLatestConfig(out MathLodConfigDTO config))
            {
                quality = MathLodApproximation.SaturateFinite(
                    config.GlobalQualityWeight,
                    SeaglideSimdMath.AuthoritativeQualityWeight);
            }

            tuning.GlobalQualityWeight = quality;
            tuning.ResolvedQualityWeight = quality;
            return quality;
        }

        private bool TryPinJobBuffers(IDataVault vault)
        {
            if (vault == null || _jobBuffersPinned || vault.IsCompactionFenceActive)
                return false;

            bool pinned = false;
            try
            {
                _jobPinVault = vault;
                if (!TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.States, JobPinStates) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.Requests, JobPinRequests) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.ForcePackets, JobPinForcePackets) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.FlowSamples, JobPinFlowSamples) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.Tuning, JobPinTuning) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.TelemetryRing, JobPinTelemetryRing) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.TelemetryCursor, JobPinTelemetryCursor) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.Counters, JobPinCounters) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.VisualStates, JobPinVisualStates) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.AudioSignals, JobPinAudioSignals) ||
                    !TryLockJobBuffer(vault, SeaglideHydrodynamicsBufferIds.CavitationSignals, JobPinCavitationSignals))
                {
                    return false;
                }

                if (!TryResolveRuntimeBuffers(
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
                        out _))
                {
                    return false;
                }

                _jobBuffersPinned = true;
                pinned = true;
                return true;
            }
            finally
            {
                if (!pinned)
                    UnlockJobBuffers();
            }
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _jobPinVault;
            uint pinMask = _jobPinMask;
            _jobPinVault = null;
            _jobPinMask = 0u;
            _jobBuffersPinned = false;
            if (vault == null || pinMask == 0u)
                return;

            TryUnlockJobBuffer(vault, pinMask, JobPinCavitationSignals, SeaglideHydrodynamicsBufferIds.CavitationSignals);
            TryUnlockJobBuffer(vault, pinMask, JobPinAudioSignals, SeaglideHydrodynamicsBufferIds.AudioSignals);
            TryUnlockJobBuffer(vault, pinMask, JobPinVisualStates, SeaglideHydrodynamicsBufferIds.VisualStates);
            TryUnlockJobBuffer(vault, pinMask, JobPinCounters, SeaglideHydrodynamicsBufferIds.Counters);
            TryUnlockJobBuffer(vault, pinMask, JobPinTelemetryCursor, SeaglideHydrodynamicsBufferIds.TelemetryCursor);
            TryUnlockJobBuffer(vault, pinMask, JobPinTelemetryRing, SeaglideHydrodynamicsBufferIds.TelemetryRing);
            TryUnlockJobBuffer(vault, pinMask, JobPinTuning, SeaglideHydrodynamicsBufferIds.Tuning);
            TryUnlockJobBuffer(vault, pinMask, JobPinFlowSamples, SeaglideHydrodynamicsBufferIds.FlowSamples);
            TryUnlockJobBuffer(vault, pinMask, JobPinForcePackets, SeaglideHydrodynamicsBufferIds.ForcePackets);
            TryUnlockJobBuffer(vault, pinMask, JobPinRequests, SeaglideHydrodynamicsBufferIds.Requests);
            TryUnlockJobBuffer(vault, pinMask, JobPinStates, SeaglideHydrodynamicsBufferIds.States);
        }

        private bool TryLockJobBuffer(IDataVault vault, BufferID bufferId, uint pinBit)
        {
            if ((_jobPinMask & pinBit) != 0u)
                return true;

            if (vault == null || !vault.TryLockBuffer(bufferId, SystemID.VehiclesPhysics))
                return false;

            _jobPinMask |= pinBit;
            return true;
        }

        private static void TryUnlockJobBuffer(IDataVault vault, uint pinMask, uint pinBit, BufferID bufferId)
        {
            if ((pinMask & pinBit) != 0u)
                vault.TryUnlockBuffer(bufferId, SystemID.VehiclesPhysics);
        }

        private void TryRegister()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (!_registeredFixed)
                _registeredFixed = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
            if (!_registeredPostFixed)
                _registeredPostFixed = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
            if (!_registeredLateFrame)
                _registeredLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Player);
            if (!_registeredHotSwap)
            {
                _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
            }
        }

        private void TryUnregister()
        {
            if (_registeredPostFixed)
            {
                GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
                _registeredPostFixed = false;
            }

            if (_registeredFixed)
            {
                GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
                _registeredFixed = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Player);
                _registeredLateFrame = false;
            }

            if (_registeredHotSwap)
            {
                GlobalRegistry.TryUnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
            ClearActiveRequestWindow();
            _forcePacketsReadyToDrain = false;
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _statesHandle);
            ReleaseVaultHandle(vault, ref _requestsHandle);
            ReleaseVaultHandle(vault, ref _forcePacketsHandle);
            ReleaseVaultHandle(vault, ref _flowSamplesHandle);
            ReleaseVaultHandle(vault, ref _tuningHandle);
            ReleaseVaultHandle(vault, ref _telemetryRingHandle);
            ReleaseVaultHandle(vault, ref _telemetryCursorHandle);
            ReleaseVaultHandle(vault, ref _countersHandle);
            ReleaseVaultHandle(vault, ref _bodyBindingsHandle);
            ReleaseVaultHandle(vault, ref _visualStatesHandle);
            ReleaseVaultHandle(vault, ref _audioSignalsHandle);
            ReleaseVaultHandle(vault, ref _cavitationSignalsHandle);
        }

        private static void ReleaseVaultHandle<T>(IDataVault vault, ref VaultGenerationHandle<T> handle) where T : struct
        {
            if (vault != null && HasHandle(in handle))
                vault.ReleaseBuffer(in handle);

            handle = default;
        }

        private static NativeArray<T> ResolveVaultBuffer<T>(IDataVault vault, in VaultGenerationHandle<T> handle)
            where T : struct
        {
            return vault != null &&
                   HasHandle(in handle) &&
                   vault.TryResolveHandle(in handle, out NativeArray<T> buffer)
                ? buffer
                : default;
        }

        private static bool TryReadOnlyVaultBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            out NativeArray<T>.ReadOnly buffer)
            where T : struct
        {
            buffer = default;
            return vault != null &&
                   HasHandle(in handle) &&
                   vault.TryReadOnlyHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length > 0;
        }

        private static bool HasHandle<T>(in VaultGenerationHandle<T> handle) where T : struct
        {
            return handle.BufferID != 0u && handle.Generation != 0u;
        }

        private static float ResolveElapsedMicros(long startTimestamp)
        {
            long elapsed = Stopwatch.GetTimestamp() - startTimestamp;
            return (float)(elapsed * 1000000.0 / Stopwatch.Frequency);
        }
    }
}
