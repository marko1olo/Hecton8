using System;
using System.IO;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.World;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Physics
{
    [DisallowMultipleComponent]
    public sealed class SeaglideHydrodynamicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        private const int LockStates = 1 << 0;
        private const int LockRequests = 1 << 1;
        private const int LockForcePackets = 1 << 2;
        private const int LockFlowSamples = 1 << 3;
        private const int LockTuning = 1 << 4;
        private const int LockTelemetry = 1 << 5;
        private const int LockTelemetryCursor = 1 << 6;
        private const int LockCounters = 1 << 7;
        private const int LockVisualStates = 1 << 8;
        private const int LockAudioSignals = 1 << 9;
        private const int LockCavitationSignals = 1 << 10;
        private const byte ToolAcousticStateSeaglidePropeller = 2;
        private const float MinimumSignalIntensity = 0.01f;

        private static SeaglideHydrodynamicsRuntime s_activeRuntimeInstance;

        [SerializeField]
        private bool _seedEmergencyMockRequests;

        private IDataVault _dataVault;
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
        private VaultGenerationHandle<byte> _csvScratchHandle;
        private JobHandle _pendingHandle;
        private long _scheduleTimestamp;
        private uint _simulationFrame;
        private int _activeRequestCount;
        private int _lockedBuffers;
        private float _metabolismAccumulator;
        private float _thrustCadenceAccumulator;
        private bool _jobScheduled;
        private bool _registeredFixed;
        private bool _registeredPostFixed;
        private bool _registeredLateFrame;
        private bool _registeredHotSwap;
        private bool _coldBootCompleted;
        private bool _dumpedFault;
        private bool _forcePacketsReadyToDrain;

        public static bool IsRuntimeAvailable => s_activeRuntimeInstance != null;

        public static bool TryGetActiveRuntime(out SeaglideHydrodynamicsRuntime runtime)
        {
            runtime = s_activeRuntimeInstance;
            return runtime != null;
        }

        public static bool TrySubmitPlayerRequest(in SeaglidePropulsionRequestDTO request, in SeaglideStateDTO state)
        {
            SeaglideHydrodynamicsRuntime runtime = EnsureRuntimeInstance();
            return runtime != null && runtime.TrySubmitRequest(0, in request, in state);
        }

        public static SeaglideHydrodynamicsRuntime EnsureRuntimeInstance()
        {
            if (s_activeRuntimeInstance != null)
                return s_activeRuntimeInstance;

            if (!Application.isPlaying)
                return null;

            PhysicsApplySystem physics = PhysicsApplySystem.EnsureRuntimeInstance();
            if (physics == null)
                return null;

            if (!physics.TryGetComponent(out SeaglideHydrodynamicsRuntime runtime))
                runtime = physics.gameObject.AddComponent<SeaglideHydrodynamicsRuntime>(); // COLD ALLOC: SeaglideHydrodynamicsRuntime[1] - attached to physics authority root - owner: SHINOBU_227
            return runtime;
        }

        public bool TryResolveEditorViews(
            out NativeArray<SeaglideTuningDTO> tuning,
            out NativeArray<SeaglideCounterDTO> counters,
            out NativeArray<SeaglideTelemetryEntry> telemetry,
            out NativeArray<int> cursor,
            out NativeArray<SeaglideAudioSignalDTO> audio,
            out NativeArray<SeaglideCavitationVfxSignalDTO> cavitation)
        {
            tuning = default;
            counters = default;
            telemetry = default;
            cursor = default;
            audio = default;
            cavitation = default;
            IDataVault vault = _dataVault;
            if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                vault = latestVault;
            if (vault == null || !EnsureVaultBuffers())
                return false;

            tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            counters = ResolveVaultBuffer(vault, in _countersHandle);
            telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            cursor = ResolveVaultBuffer(vault, in _telemetryCursorHandle);
            audio = ResolveVaultBuffer(vault, in _audioSignalsHandle);
            cavitation = ResolveVaultBuffer(vault, in _cavitationSignalsHandle);
            return tuning.IsCreated && tuning.Length > 0 &&
                   counters.IsCreated && counters.Length > 0 &&
                   telemetry.IsCreated && telemetry.Length > 0 &&
                   cursor.IsCreated && cursor.Length > 0;
        }

        public bool TryResolveForcePacketEditorView(out NativeArray<SeaglideForcePacketDTO> forcePackets)
        {
            forcePackets = default;
            IDataVault vault = _dataVault;
            if (vault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latestVault))
                vault = latestVault;
            if (vault == null || !EnsureVaultBuffers())
                return false;

            forcePackets = ResolveVaultBuffer(vault, in _forcePacketsHandle);
            return forcePackets.IsCreated && forcePackets.Length > 0;
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
            if (!Application.isPlaying)
                return;

            if (s_activeRuntimeInstance == null)
                s_activeRuntimeInstance = this;
            CompletePendingSolverForTeardown();
            RefreshColdDependencies();
            EnsureColdBooted();
            TryRegister();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            TryUnregister();
            CompletePendingSolverForTeardown();
            _forcePacketsReadyToDrain = false;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(s_activeRuntimeInstance, this))
                s_activeRuntimeInstance = null;

            TryUnregister();
            CompletePendingSolverForTeardown();
            ReleaseVaultHandles(_dataVault);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!Application.isPlaying || _jobScheduled || _forcePacketsReadyToDrain || !math.isfinite(fixedDeltaTime) || fixedDeltaTime <= 0f)
                return;

            float safeDelta = math.clamp(fixedDeltaTime, 0.0001f, 0.2f);
            if (!TryPrepareRuntimeVault(out IDataVault vault))
                return;

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
                return;
            }

            if (_seedEmergencyMockRequests && _activeRequestCount <= 0)
                GenerateMockPropulsionRequests();

            int activeCount = math.clamp(_activeRequestCount, 0, math.min(states.Length, requests.Length));
            if (activeCount <= 0)
            {
                _thrustCadenceAccumulator = 0f;
                return;
            }

            SeaglideTuningDTO tuningDto = tuning[0];
            float quality = ResolveGlobalQualityWeight(ref tuningDto);
            _thrustCadenceAccumulator = math.min(_thrustCadenceAccumulator + safeDelta, 0.2f);
            float thrustCadenceSeconds = ResolveThrustCadenceSeconds(safeDelta, quality);
            if (_thrustCadenceAccumulator + 0.00001f < thrustCadenceSeconds)
                return;

            float solverDelta = _thrustCadenceAccumulator;
            _thrustCadenceAccumulator = 0f;
            tuningDto.SectorAUP = ResolveSectorAUP();
            tuningDto.ResolvedQualityWeight = quality;
            tuningDto.GlobalQualityWeight = quality;
            tuningDto.SimulationTickDelta = solverDelta;
            tuningDto.ActiveRequestCount = activeCount;
            tuningDto.FrameIndex = _simulationFrame;
            tuning[0] = tuningDto;

            if (!TryLockJobBuffers(vault))
                return;

            if (!PhysicsApplySystem.TryPrepareSeaglideForcePackets(forcePackets, counters))
            {
                UnlockJobBuffers();
                return;
            }

            int metabolismEnabled = ResolveMetabolismTickEnabled(solverDelta, quality, tuningDto);
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
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            if (!Application.isPlaying)
                return;

            TryFinalizePendingSolverNoWait();
            if (!_forcePacketsReadyToDrain)
                return;

            IDataVault vault = _dataVault;
            if (vault == null ||
                !HasHandle(in _forcePacketsHandle) ||
                !HasHandle(in _countersHandle) ||
                !HasHandle(in _bodyBindingsHandle))
            {
                _forcePacketsReadyToDrain = false;
                return;
            }

            NativeArray<SeaglideForcePacketDTO> forcePackets = ResolveVaultBuffer(vault, in _forcePacketsHandle);
            NativeArray<SeaglideCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
            NativeArray<SeaglideBodyBindingDTO> bodyBindings = ResolveVaultBuffer(vault, in _bodyBindingsHandle);
            PhysicsApplySystem.DrainSeaglideForcePackets(
                forcePackets,
                counters,
                bodyBindings,
                SeaglideHydrodynamicsConstants.ForceQueueSoftCapacity,
                out _,
                out _);
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
            if (serviceSlot != GlobalRegistryServiceSlot.DataVault)
                return;

            CompletePendingSolverForTeardown();
            IDataVault previousVault = (previousService as IDataVault) ?? _dataVault;
            IDataVault currentVault = currentService as IDataVault;
            if (!ReferenceEquals(previousVault, currentVault))
                ReleaseVaultHandles(previousVault);
            _dataVault = currentVault;
            _coldBootCompleted = false;
            if (currentVault != null && !currentVault.IsAllocationLocked)
                EnsureColdBooted();
        }

        public bool GenerateMockPropulsionRequests()
        {
            IDataVault vault = _dataVault;
            if (vault == null || vault.IsAllocationLocked || _jobScheduled || !EnsureVaultBuffers())
                return false;

            NativeArray<SeaglideStateDTO> states = ResolveVaultBuffer(vault, in _statesHandle);
            NativeArray<SeaglidePropulsionRequestDTO> requests = ResolveVaultBuffer(vault, in _requestsHandle);
            NativeArray<SeaglideTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
            if (!states.IsCreated || !requests.IsCreated || !tuning.IsCreated || tuning.Length <= 0)
                return false;

            SeaglideTuningDTO tuningDto = tuning[0];
            int mockCount = math.clamp(
                tuningDto.MockRequestCount > 0 ? tuningDto.MockRequestCount : SeaglideHydrodynamicsConstants.MockRequestCount,
                1,
                math.min(states.Length, math.min(requests.Length, SeaglideHydrodynamicsConstants.MockRequestCount)));
            GenerateMockSeaglidePropulsionDataJob job = new GenerateMockSeaglidePropulsionDataJob
            {
                States = states,
                Requests = requests,
                ActiveMockCount = mockCount,
                OriginAUP = tuningDto.SectorAUP,
                SimulationFrame = _simulationFrame
            };
            JobHandle handle = job.Schedule(mockCount, 64);
            // COLD/EDITOR BLOCKING SYNC: emergency 1000-record data generator is not part of the live frame solver.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            tuningDto.ActiveRequestCount = mockCount;
            tuningDto.MockRequestCount = mockCount;
            tuning[0] = tuningDto;
            _activeRequestCount = mockCount;
            return true;
        }

        private bool TrySubmitRequest(int index, in SeaglidePropulsionRequestDTO requestInput, in SeaglideStateDTO stateInput)
        {
            if (!Application.isPlaying || _jobScheduled || _forcePacketsReadyToDrain || index < 0)
                return false;

            if (_dataVault == null)
                RefreshColdDependencies();
            EnsureColdBooted();
            IDataVault vault = _dataVault;
            if (vault == null || !EnsureVaultBuffers())
                return false;

            NativeArray<SeaglideStateDTO> states = ResolveVaultBuffer(vault, in _statesHandle);
            NativeArray<SeaglidePropulsionRequestDTO> requests = ResolveVaultBuffer(vault, in _requestsHandle);
            if (!states.IsCreated || !requests.IsCreated || (uint)index >= (uint)states.Length || (uint)index >= (uint)requests.Length)
                return false;

            SeaglidePropulsionRequestDTO request = requestInput;
            SeaglideStateDTO state = stateInput;
            request.TargetEntityHash = request.TargetEntityHash != 0u ? request.TargetEntityHash : SeaglideHydrodynamicsConstants.PlayerBodyTargetHash;
            request.RequestHash = request.RequestHash != 0u ? request.RequestHash : SeaglideHydrodynamicsConstants.SourceHash;
            request.FrameIndex = _simulationFrame;
            request.Flags |= SeaglideHydrodynamicsConstants.FlagActive;
            state.TargetEntityHash = request.TargetEntityHash;
            state.FrameIndex = _simulationFrame;
            state.ActiveFlags = request.Flags;
            requests[index] = request;
            states[index] = state;
            _activeRequestCount = math.max(_activeRequestCount, index + 1);
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
            JobHandle handle = initJob.Schedule();
            // COLD BOOT BLOCKING SYNC: one-time Vault clear and layout trap seed before runtime scheduling starts.
            DispatcherJobFence.TryComplete(ref handle, forceComplete: true);
            EnsureSeaglideSignalLanes();
            SeedDefaultTuningIfNeeded();
            _coldBootCompleted = true;
            return true;
        }

        private bool TryPrepareRuntimeVault(out IDataVault vault)
        {
            vault = _dataVault;
            if (vault == null)
            {
                RefreshColdDependencies();
                vault = _dataVault;
            }

            if (vault == null)
                return false;

            if (!EnsureVaultBuffers())
                return false;

            vault = _dataVault;
            return vault != null;
        }

        private void RefreshColdDependencies()
        {
            _dataVault = GlobalRegistry.DataVault;
            if (_dataVault == null && GlobalDataVault.TryGetLatestCreated(out GlobalDataVault latest))
                _dataVault = latest;
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
                   EnsureVaultDescriptor(vault, ref _cavitationSignalsHandle, SeaglideHydrodynamicsBufferIds.CavitationSignals, SeaglideHydrodynamicsConstants.StateCapacity, NativeArrayOptions.UninitializedMemory) &&
                   EnsureVaultDescriptor(vault, ref _csvScratchHandle, SeaglideHydrodynamicsBufferIds.CsvScratch, SeaglideHydrodynamicsConstants.CsvScratchBytes, NativeArrayOptions.UninitializedMemory);
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

            if (vault.IsAllocationLocked)
                return false;

            handle = vault.GetGenerationHandle<T>(bufferId, requiredLength, SystemID.VehiclesPhysics, options);
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

            if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                return false;

            return FinishPendingSolverCompletion();
        }

        private bool FinishPendingSolverCompletion()
        {
            _jobScheduled = false;
            UnlockJobBuffers();
            float micros = ResolveElapsedMicros(_scheduleTimestamp);
            WriteCompletedComputeMicros(micros);
            PublishCompletedPresentationSignals();
            if (!_dumpedFault && TryLatestCounterHasFault())
            {
                DumpBlackBoxOnce();
                _dumpedFault = true;
            }

            _simulationFrame++;
            _activeRequestCount = 0;
            _forcePacketsReadyToDrain = true;
            return true;
        }

        private static void EnsureSeaglideSignalLanes()
        {
            SignalBus<ToolAcousticSignal>.EnsureInitialized();
            SignalBus<BubbleSpawnSignal>.EnsureInitialized();
        }

        private void PublishCompletedPresentationSignals()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<SeaglideCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
            NativeArray<SeaglideAudioSignalDTO> audioSignals = ResolveVaultBuffer(vault, in _audioSignalsHandle);
            NativeArray<SeaglideCavitationVfxSignalDTO> cavitationSignals = ResolveVaultBuffer(vault, in _cavitationSignalsHandle);
            if (!counters.IsCreated ||
                counters.Length <= 0 ||
                !audioSignals.IsCreated ||
                !cavitationSignals.IsCreated)
            {
                return;
            }

            SeaglideCounterDTO counter = counters[0];
            int packetCount = math.clamp(counter.ForcePackets, 0, math.min(audioSignals.Length, cavitationSignals.Length));
            if (packetCount <= 0)
                return;

            int publishBudget = ResolvePresentationSignalBudget(counter.GlobalQualityWeight, packetCount);
            for (int i = 0; i < publishBudget; i++)
            {
                PublishAudioSignal(in audioSignals[i]);
                PublishBubbleSignal(in cavitationSignals[i], counter.GlobalQualityWeight);
            }
        }

        private static int ResolvePresentationSignalBudget(float quality, int packetCount)
        {
            float smoothQuality = math.saturate(quality);
            smoothQuality = smoothQuality * smoothQuality * (3f - (2f * smoothQuality));
            int maxBudget = 1 + (int)math.floor(smoothQuality * 3.999f);
            return math.clamp(maxBudget, 1, math.max(1, packetCount));
        }

        private static void PublishAudioSignal(in SeaglideAudioSignalDTO source)
        {
            if (source.SourceHash == 0u ||
                source.TargetEntityHash == 0u ||
                !math.isfinite(source.PitchScalar) ||
                !math.isfinite(source.VolumeScalar) ||
                source.VolumeScalar <= MinimumSignalIntensity)
            {
                return;
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
            SignalBus<ToolAcousticSignal>.TryPush(in signal);
        }

        private static void PublishBubbleSignal(in SeaglideCavitationVfxSignalDTO source, float quality)
        {
            float intensity = math.saturate(source.Intensity01 * math.lerp(0.25f, 1f, math.saturate(quality)));
            if (source.SourceHash == 0u ||
                intensity <= MinimumSignalIntensity ||
                !math.all(math.isfinite(source.CurrentAUP)) ||
                !math.all(math.isfinite(source.Direction)) ||
                !math.isfinite(source.RadiusMeters))
            {
                return;
            }

            BubbleSpawnSignal signal = default;
            signal.PositionAup = AbsoluteUniversePosition.FromAbsolutePosition(source.CurrentAUP);
            signal.Direction = SafeSignalDirection(source.Direction);
            signal.Intensity01 = intensity;
            signal.RadiusMeters = math.clamp(source.RadiusMeters, 0.05f, 8f);
            signal.Frame = source.FrameIndex;
            signal.SourceHash = source.SourceHash;
            signal.Flags = BubbleSpawnSignal.FlagEngineVent;
            SignalBus<BubbleSpawnSignal>.TryPush(in signal);
        }

        private static float3 SafeSignalDirection(float3 direction)
        {
            float lengthSq = math.lengthsq(direction);
            return math.select(new float3(0f, 0f, 1f), direction * math.rsqrt(math.max(lengthSq, 0.000001f)), math.isfinite(lengthSq) && lengthSq > 0.000001f);
        }

        private void WriteCompletedComputeMicros(float micros)
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

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

        private bool TryLatestCounterHasFault()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            NativeArray<SeaglideCounterDTO> counters = ResolveVaultBuffer(vault, in _countersHandle);
            return counters.IsCreated &&
                   counters.Length > 0 &&
                   ((counters[0].Flags & (SeaglideHydrodynamicsConstants.FlagNonFinite | SeaglideHydrodynamicsConstants.FlagBudgetExceeded)) != 0u ||
                    counters[0].NonFiniteCount > 0);
        }

        private void DumpBlackBoxOnce()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<SeaglideTelemetryEntry> telemetry = ResolveVaultBuffer(vault, in _telemetryRingHandle);
            if (!telemetry.IsCreated || telemetry.Length <= 0)
                return;

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
                return;

            string path = Path.Combine(projectRoot, SeaglideHydrodynamicsConstants.DumpRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (BinaryWriter writer = new BinaryWriter(stream))
            {
                writer.Write(SeaglideHydrodynamicsConstants.SourceHash);
                writer.Write(_simulationFrame);
                writer.Write(telemetry.Length);
                for (int i = 0; i < telemetry.Length; i++)
                {
                    SeaglideTelemetryEntry entry = telemetry[i];
                    writer.Write(entry.FrameIndex);
                    writer.Write(entry.EvaluatedRequests);
                    writer.Write(entry.ForcePackets);
                    writer.Write(entry.NonFiniteCount);
                    writer.Write(entry.TotalThrustForce);
                    writer.Write(entry.TotalDragForce);
                    writer.Write(entry.TotalFlowForce);
                    writer.Write(entry.MaxForceMagnitude);
                    writer.Write(entry.ComputeMicros);
                    writer.Write(entry.GlobalQualityWeight);
                    writer.Write(entry.Flags);
                    writer.Write(entry.LastTargetEntityHash);
                    writer.Write(entry.LastFlowForce.x);
                    writer.Write(entry.LastFlowForce.y);
                    writer.Write(entry.LastFlowForce.z);
                    writer.Write(entry.LastBatteryLevel);
                }
            }
        }

        private void SeedDefaultTuningIfNeeded()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            NativeArray<SeaglideTuningDTO> tuning = ResolveVaultBuffer(vault, in _tuningHandle);
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

            value.GlobalQualityWeight = ResolveGlobalQualityWeight(ref value);
            value.ResolvedQualityWeight = value.GlobalQualityWeight;
            tuning[0] = value;
        }

        private int ResolveMetabolismTickEnabled(float deltaTime, float quality, SeaglideTuningDTO tuning)
        {
            float minCadence = math.max(0.02f, tuning.MinimumCadenceSeconds);
            float maxCadence = math.max(minCadence, tuning.MaximumCadenceSeconds);
            float cadence = math.lerp(maxCadence, minCadence, math.saturate(quality));
            _metabolismAccumulator += deltaTime;
            if (_metabolismAccumulator < cadence)
                return 0;

            _metabolismAccumulator = 0f;
            return 1;
        }

        private static float ResolveThrustCadenceSeconds(float fixedDeltaTime, float quality)
        {
            float safeFixedDelta = math.clamp(fixedDeltaTime, 0.0001f, 0.05f);
            float lowCadence = 0.05f;
            float highCadence = safeFixedDelta;
            float smoothQuality = math.saturate(quality);
            smoothQuality = smoothQuality * smoothQuality * (3f - (2f * smoothQuality));
            return math.lerp(lowCadence, highCadence, smoothQuality);
        }

        private static float ResolveGlobalQualityWeight(ref SeaglideTuningDTO tuning)
        {
            float quality = HomeostasisBrain.GlobalQualityWeight;
            quality = math.saturate(math.select(1f, quality, math.isfinite(quality)));
            tuning.GlobalQualityWeight = quality;
            return quality;
        }

        private static double3 ResolveSectorAUP()
        {
            if (PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext))
                return runtimeContext.MovementState.PredictedAup.ToAbsoluteDouble3();

            return double3.zero;
        }

        private bool TryLockJobBuffers(IDataVault vault)
        {
            _lockedBuffers = 0;
            return TryLock(vault, SeaglideHydrodynamicsBufferIds.States, LockStates) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.Requests, LockRequests) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.ForcePackets, LockForcePackets) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.FlowSamples, LockFlowSamples) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.Tuning, LockTuning) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.TelemetryRing, LockTelemetry) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.TelemetryCursor, LockTelemetryCursor) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.Counters, LockCounters) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.VisualStates, LockVisualStates) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.AudioSignals, LockAudioSignals) &&
                   TryLock(vault, SeaglideHydrodynamicsBufferIds.CavitationSignals, LockCavitationSignals);
        }

        private bool TryLock(IDataVault vault, BufferID bufferId, int bit)
        {
            if (vault != null && vault.TryLockBuffer(bufferId, SystemID.VehiclesPhysics))
            {
                _lockedBuffers |= bit;
                return true;
            }

            UnlockJobBuffers();
            return false;
        }

        private void UnlockJobBuffers()
        {
            IDataVault vault = _dataVault;
            if (vault == null || _lockedBuffers == 0)
            {
                _lockedBuffers = 0;
                return;
            }

            Unlock(vault, SeaglideHydrodynamicsBufferIds.States, LockStates);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.Requests, LockRequests);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.ForcePackets, LockForcePackets);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.FlowSamples, LockFlowSamples);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.Tuning, LockTuning);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.TelemetryRing, LockTelemetry);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.TelemetryCursor, LockTelemetryCursor);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.Counters, LockCounters);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.VisualStates, LockVisualStates);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.AudioSignals, LockAudioSignals);
            Unlock(vault, SeaglideHydrodynamicsBufferIds.CavitationSignals, LockCavitationSignals);
            _lockedBuffers = 0;
        }

        private void Unlock(IDataVault vault, BufferID bufferId, int bit)
        {
            if ((_lockedBuffers & bit) != 0)
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
                GlobalRegistry.RegisterHotSwapListener(this);
                _registeredHotSwap = true;
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
                GlobalRegistry.UnregisterHotSwapListener(this);
                _registeredHotSwap = false;
            }
        }

        private void ReleaseVaultHandles(IDataVault vault)
        {
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
            ReleaseVaultHandle(vault, ref _csvScratchHandle);
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
