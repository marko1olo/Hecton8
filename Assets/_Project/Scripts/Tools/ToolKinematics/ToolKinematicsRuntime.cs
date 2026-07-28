using System;
using System.IO;
using System.Threading;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Tools.ToolKinematics.Contracts;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Tools.ToolKinematics
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9917)]
    public sealed class ToolKinematicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, IColdTickable, IGlobalRegistryHotSwapListener
    {
        private static int _signalPushDropCount;

        // Release-audible diagnostics. GlobalTelemetryBus.PublishPerformanceWarning
        // (Core/GlobalTelemetryBus.cs:365) carries no [Conditional] attribute, unlike every
        // H8Debug entry point (Core/H8Debug.cs:63-77), so these reach a shipped player. Every
        // fault this runtime already measured was previously written to a field that nothing read.
        private static readonly uint AbiLayoutFaultWarningHash =
            unchecked((uint)LocHash.Compute("ToolKinematicsRuntime.AbiLayoutFault"));
        private static readonly uint TickRegistrationMissWarningHash =
            unchecked((uint)LocHash.Compute("ToolKinematicsRuntime.TickRegistrationMiss"));
        private static readonly uint ConsumerlessLaneWarningHash =
            unchecked((uint)LocHash.Compute("ToolKinematicsRuntime.SignalLaneHasNoConsumer"));
        private static readonly uint SignalPushDropWarningHash =
            unchecked((uint)LocHash.Compute("ToolKinematicsRuntime.SignalPushDrop"));
        private static readonly uint BlackBoxDumpFaultWarningHash =
            unchecked((uint)LocHash.Compute("ToolKinematicsRuntime.BlackBoxDumpWriteFault"));
        private static readonly uint ToolKinematicsContextHash =
            unchecked((uint)LocHash.Compute("ToolKinematicsRuntime"));

        // Lanes this runtime fills that no consumer drains, re-verified by rg over every SignalBus<T> read
        // entry point (GetFrameSnapshot / GetFrameSnapshotArray / GetSignals / TryConsumeFrame /
        // TryGetLatest / FilterSnapshot / TransformSnapshot): ToolTriggerPullSignal,
        // ToolCarveRequestSignal, ToolHeatSignal and ToolPowerDepletedSignal have zero readers under
        // Assets/. VfxSparkRequestSignal was previously counted here and does NOT belong: it is drained by
        // CarveDebrisComputeRenderer.AppendSparkRequests (VFX/Debris/CarveDebrisComputeRenderer.cs:2098,
        // SignalBus<VfxSparkRequestSignal>.GetFrameSnapshot) and has two further producers
        // (Tools/LaserCutterDodRuntime.cs:753, Construction/DroneFleetManager.cs:6271), so counting it made
        // a fully wired lane raise a release-audible "lane has no consumer" warning on the very first
        // spark. ToolTriggerPullSignal is the mirror error - genuinely undrained and not counted at all.
        // The trigger and heat payloads still reach consumers through the bridges below, which ARE drained:
        // ToolTriggerSignal (Core/Signals/GlobalSignals.LegacyFacade.cs:1141), ToolStateChangedSignal
        // (Visor/VisorHUDController.cs:1459) and ToolAcousticSignal
        // (Core/HectonInputRuntime_HapticSynth.cs:209). So this counter measures a redundant native lane,
        // not lost gameplay data - which is the difference between "stop publishing" and "needs a consumer".
        private const uint ConsumerlessLaneContextHash =
            ToolTriggerPullSignal.LaneHash ^
            ToolCarveRequestSignal.LaneHash ^
            ToolHeatSignal.LaneHash ^
            ToolPowerDepletedSignal.LaneHash;

        private static int _consumerlessLanePublishTotal;
        private static int _consumerlessLaneReported;
        private static int _abiLayoutFaultReported;

        public const int MaxToolCapacity = 8;
        public const int BeamVerticesPerTool = 64;
#if UNITY_EDITOR
        private const int CsvBufferBytes = 4096;
        private const string EquipmentStatsFileName = "equipment_stats.csv";
#endif
        private const int MaxBlackBoxDumpEntries = MaxToolCapacity * ToolKinematicsMath.BlackBoxCapacity;
        private const uint BlackBoxDumpMagic = 0x42424B54u; // TKBB
        private const uint BlackBoxDumpVersion = 1u;
        private const int BlackBoxDumpHeaderBytes = 32;
        private const int BlackBoxDumpEntryBytes = 64;
        private const string BlackBoxDumpRelativePath = "Docs/AgentLogs/Dump_TOOL_KINEMATICS.bin";
        private const string DumpPayloadLabel = "ToolKinematicsTelemetryDumpPayload";
        private const int DumpStateIdle = 0;
        private const int DumpStateSnapshotting = 1;
        private const int DumpStatePending = 2;
        private const int DumpStateWriting = 3;
        private static readonly ulong FrameMutationGuardMask =
            ToolMutationGuardBit(BufferID.ToolKinematicsStates) |
            ToolMutationGuardBit(BufferID.ToolKinematicsFrameInputs) |
            ToolMutationGuardBit(BufferID.ToolKinematicsHitResults) |
            ToolMutationGuardBit(BufferID.ToolKinematicsIkOutputs) |
            ToolMutationGuardBit(BufferID.ToolKinematicsRecoilStates) |
            ToolMutationGuardBit(BufferID.ToolKinematicsTuning) |
            ToolMutationGuardBit(BufferID.ToolKinematicsScreenExports) |
            ToolMutationGuardBit(BufferID.ToolKinematicsTelemetryRing) |
            ToolMutationGuardBit(BufferID.ToolKinematicsTriggerSignals) |
            ToolMutationGuardBit(BufferID.ToolKinematicsCarveRequests) |
            ToolMutationGuardBit(BufferID.ToolKinematicsHeatSignals) |
            ToolMutationGuardBit(BufferID.ToolKinematicsSparkRequests) |
            ToolMutationGuardBit(BufferID.ToolKinematicsBeamVertices) |
            ToolMutationGuardBit(BufferID.ToolKinematicsBeamVertexCounts) |
            ToolMutationGuardBit(BufferID.ToolKinematicsPoseOutputs);

        [SerializeField] private int toolCapacity = 2;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Transform[] controllerSources;
        [SerializeField] private Transform[] shoulderAnchors;
        [SerializeField] private bool useSyntheticInputFallback = true;
        [SerializeField] private bool syntheticTriggerHeld = true;
        [SerializeField, Range(0f, 1f)] private float systemHealthIndex;
        [SerializeField] private float laserRange = 18f;
        [SerializeField] private float heatRampRate = 0.62f;
        [SerializeField] private float coolingRate = 0.38f;
        [SerializeField] private float maxHeat = 1f;
        [SerializeField] private float energyDrainRate = 0.075f;
        [SerializeField] private float recoilStrength = 0.18f;
        [SerializeField] private float springDamping = 12f;
        [SerializeField] private float collisionSpring = 0.42f;
        [SerializeField] private float beamRadius = 0.018f;

        private readonly ToolKinematicsTelemetryEntry[] _blackBoxDumpEntries = new ToolKinematicsTelemetryEntry[MaxBlackBoxDumpEntries]; // COLD ALLOC: ToolKinematicsTelemetryEntry[2400] - fault snapshot handoff buffer - owner: ToolKinematicsRuntime

        private IDataVault _dataVault;
        private IInputService _inputService;
        private VaultGenerationHandle<ToolStateDTO> _statesHandle;
        private VaultGenerationHandle<ToolKinematicsFrameInputDTO> _frameInputsHandle;
        private VaultGenerationHandle<ToolHitResultDTO> _hitResultsHandle;
        private VaultGenerationHandle<ToolIkOutputDTO> _ikOutputsHandle;
        private VaultGenerationHandle<ToolRecoilStateDTO> _recoilStatesHandle;
        private VaultGenerationHandle<ToolKinematicsTuningDTO> _tuningHandle;
        private VaultGenerationHandle<ToolScreenExportDTO> _screenExportsHandle;
        private VaultGenerationHandle<ToolKinematicsTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<ToolTriggerPullSignal> _triggerSignalsHandle;
        private VaultGenerationHandle<ToolCarveRequestSignal> _carveRequestsHandle;
        private VaultGenerationHandle<ToolHeatSignal> _heatSignalsHandle;
        private VaultGenerationHandle<VfxSparkRequestSignal> _sparkRequestsHandle;
        private VaultGenerationHandle<ToolBeamVertexDTO> _beamVerticesHandle;
        private VaultGenerationHandle<int> _beamVertexCountsHandle;
        private VaultGenerationHandle<ToolPoseOutputDTO> _poseOutputsHandle;

        private JobHandle _pendingHandle;
#if UNITY_EDITOR
        private string _equipmentStatsPath;
        private int _csvReadFaultCode;
#endif
        private int _blackBoxDumpState;
        private int _blackBoxDumpEntryCount;
        private int _blackBoxDumpToolCapacity;
        private int _blackBoxDumpTelemetryCursor;
        private int _blackBoxDumpFailureCode;
        private uint _blackBoxDumpFrameIndex;
        private int _tuningDirty = 1;
        private uint _frameIndex;
        private int _activeToolCapacity;
        private int _telemetryCursor;
        private bool _frameScheduled;
        private bool _fixedRegistered;
        private bool _postFixedRegistered;
        private bool _coldRegistered;
        private bool _registeredHotSwap;
        private bool _pendingDataVaultRebind;
        private bool _abiValid;
        private IDataVault _pendingDataVault;
        private int _reportedSignalPushDropCount;
        private int _reportedBlackBoxDumpFailureCode;
        private int _tickRegistrationMissReported;

#if UNITY_EDITOR
        private enum EquipmentCsvKey : uint
        {
            LaserRange = 0x0F503FF1u,
            HeatRampRate = 0x24576089u,
            CoolingRate = 0x05E6524Eu,
            MaxHeat = 0x8E7AE2A7u,
            EnergyDrainRate = 0x8909415Bu,
            RecoilStrength = 0x524FB322u,
            SpringDamping = 0x36F475FAu,
            CollisionSpring = 0x6F367FA6u,
            BeamRadius = 0x6A9B3AB8u,
            SystemHealthIndex = 0x37DFA8CEu,
            SystemHealth = 0x9EE949DAu
        }
#endif

        // Domain reload is disabled (ProjectSettings/EditorSettings.asset:29-30
        // m_EnterPlayModeOptionsEnabled: 1, m_EnterPlayModeOptions: 1), so every static counter below
        // survives from one Play session into the next and would report a stale total on the next run.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticDiagnosticsState()
        {
            Volatile.Write(ref _signalPushDropCount, 0);
            Volatile.Write(ref _consumerlessLanePublishTotal, 0);
            Volatile.Write(ref _consumerlessLaneReported, 0);
            Volatile.Write(ref _abiLayoutFaultReported, 0);
        }

        private void Awake()
        {
            _abiValid = ValidateAbiLayout();
            _activeToolCapacity = math.clamp(toolCapacity, 1, MaxToolCapacity);
#if UNITY_EDITOR
            _equipmentStatsPath = ResolveEquipmentStatsPath();
#endif
            CacheRegistryDependenciesCold();
        }

        private void OnValidate()
        {
            Volatile.Write(ref _tuningDirty, 1);
        }

        private void OnEnable()
        {
            if (!_abiValid)
            {
                ReportAbiLayoutFaultOnce();
                return;
            }

            _activeToolCapacity = math.clamp(toolCapacity, 1, MaxToolCapacity);
            CacheRegistryDependenciesCold();
            TryRegisterHotSwap();
            TryBootstrapRuntime();
            ReportTickRegistrationMissOnce();
        }

        private void OnDisable()
        {
            CompletePendingFrameForTeardown();
            StopBlackBoxDumpWorker();
            TryUnregisterFixed();
            TryUnregisterPostFixed();
            TryUnregisterCold();
            TryUnregisterHotSwap();
            ReleaseVaultHandles();
            ClearHandles();
            ClearLifecycleDiagnostics();
        }

        private void OnDestroy()
        {
            CompletePendingFrameForTeardown();
            StopBlackBoxDumpWorker();
            TryUnregisterFixed();
            TryUnregisterPostFixed();
            TryUnregisterCold();
            TryUnregisterHotSwap();
            ReleaseVaultHandles();
            ClearHandles();
            ClearLifecycleDiagnostics();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!_abiValid || _frameScheduled)
                return;

            float safeDeltaTime = math.clamp(ToolKinematicsMath.ClampPositiveFinite(fixedDeltaTime, 0.0166667f), 0.001f, 0.05f);
            IDataVault frameGuardVault = _dataVault;
            if (!TryAcquireFrameMutationGuard(frameGuardVault))
                return;

            try
            {
                if (!TryResolveAllBuffers(false, out ToolKinematicsBufferSet buffers))
                    return;

                _frameIndex = _frameIndex == uint.MaxValue ? 1u : _frameIndex + 1u;
                WriteTuning(buffers.Tuning);
                PrepareFrameInputs(buffers, safeDeltaTime);

                TwoBoneIKJob ikJob = new TwoBoneIKJob
                {
                    ToolStates = buffers.States,
                    FrameInputs = buffers.FrameInputs,
                    IkOutputs = buffers.IkOutputs
                };

                SdfRaymarchJob raymarchJob = new SdfRaymarchJob
                {
                    ToolStates = buffers.States,
                    RecoilStates = buffers.RecoilStates,
                    FrameInputs = buffers.FrameInputs,
                    Tuning = buffers.Tuning,
                    HitResults = buffers.HitResults,
                    ScreenExports = buffers.ScreenExports,
                    PoseOutputs = buffers.PoseOutputs,
                    HeatSignals = buffers.HeatSignals,
                    SparkRequests = buffers.SparkRequests,
                    TelemetryRing = buffers.TelemetryRing,
                    TelemetryCursor = _telemetryCursor
                };

                ToolCarveRequestJob carveJob = new ToolCarveRequestJob
                {
                    HitResults = buffers.HitResults,
                    ToolStates = buffers.States,
                    FrameInputs = buffers.FrameInputs,
                    ScreenExports = buffers.ScreenExports,
                    CarveRequests = buffers.CarveRequests
                };

                ProceduralBeamMeshJob beamJob = new ProceduralBeamMeshJob
                {
                    HitResults = buffers.HitResults,
                    ToolStates = buffers.States,
                    FrameInputs = buffers.FrameInputs,
                    ScreenExports = buffers.ScreenExports,
                    Tuning = buffers.Tuning,
                    BeamVertices = buffers.BeamVertices,
                    BeamVertexCounts = buffers.BeamVertexCounts,
                    VerticesPerTool = BeamVerticesPerTool
                };

                JobHandle ikHandle = ikJob.Schedule(_activeToolCapacity, 1);
                JobHandle rayHandle = raymarchJob.Schedule(_activeToolCapacity, 1, ikHandle);
                JobHandle carveHandle = carveJob.Schedule(_activeToolCapacity, 1, rayHandle);
                _pendingHandle = beamJob.Schedule(_activeToolCapacity, 1, carveHandle);
                _frameScheduled = true;
                H8Memory.RegisterActiveJob(SystemID.GameplayTools, _pendingHandle);
            }
            finally
            {
                ReleaseFrameMutationGuard(frameGuardVault);
            }
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            TryFinalizePendingFrameNoWait();
        }

        public void ColdTick()
        {
            ApplyPendingDataVaultRebindIfIdle();
            ReportPendingDiagnostics();
        }

        public bool TryReadState(int index, out ToolStateDTO state)
        {
            state = default;
            if ((uint)index >= (uint)_activeToolCapacity ||
                !TryResolveStates(out NativeArray<ToolStateDTO> states))
            {
                return false;
            }

            state = states[index];
            return true;
        }

        public bool TryReadHit(int index, out ToolHitResultDTO hit)
        {
            hit = default;
            if ((uint)index >= (uint)_activeToolCapacity ||
                !TryResolveHits(out NativeArray<ToolHitResultDTO> hits))
            {
                return false;
            }

            hit = hits[index];
            return true;
        }

        internal int LastBlackBoxDumpFailureCode => Volatile.Read(ref _blackBoxDumpFailureCode);

        /// <summary>
        /// Cold-cadence flush of every fault this runtime measures. Reports a delta only, so a healthy
        /// runtime publishes nothing, and reaches a shipped player because the telemetry route is not
        /// compiled out.
        /// </summary>
        private void ReportPendingDiagnostics()
        {
            int drops = Volatile.Read(ref _signalPushDropCount);
            if (drops > _reportedSignalPushDropCount)
            {
                _reportedSignalPushDropCount = drops;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    SignalPushDropWarningHash,
                    ToolKinematicsContextHash,
                    math.max(1, drops));
            }

            int dumpFailureCode = Volatile.Read(ref _blackBoxDumpFailureCode);
            if (dumpFailureCode == 0)
            {
                _reportedBlackBoxDumpFailureCode = 0;
            }
            else if (dumpFailureCode != _reportedBlackBoxDumpFailureCode)
            {
                _reportedBlackBoxDumpFailureCode = dumpFailureCode;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    BlackBoxDumpFaultWarningHash,
                    ToolKinematicsContextHash ^ unchecked((uint)dumpFailureCode),
                    math.max(1, dumpFailureCode));
            }

            int consumerlessPublishTotal = Volatile.Read(ref _consumerlessLanePublishTotal);
            if (consumerlessPublishTotal > 0 &&
                Interlocked.Exchange(ref _consumerlessLaneReported, 1) == 0)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    ConsumerlessLaneWarningHash,
                    ConsumerlessLaneContextHash,
                    math.max(1, consumerlessPublishTotal));
            }
        }

        /// <summary>
        /// An ARM64 DTO layout mismatch disables this runtime completely. It was announced only through
        /// H8Debug.LogError, which is compiled out of a shipped build, so the tool silently stopped
        /// existing. Reported once per process.
        /// </summary>
        private static void ReportAbiLayoutFaultOnce()
        {
            if (Interlocked.Exchange(ref _abiLayoutFaultReported, 1) != 0)
                return;

            GlobalTelemetryBus.PublishPerformanceWarning(
                AbiLayoutFaultWarningHash,
                ToolKinematicsContextHash,
                1f);
        }

        /// <summary>
        /// The dispatcher exists, this component is enabled in a playing session, and at least one of
        /// its three tick lanes still did not arm - so the runtime is authored and inert. A null
        /// dispatcher is normal boot ordering and is covered by the hot-swap listener instead, so it is
        /// deliberately not reported here.
        /// </summary>
        private void ReportTickRegistrationMissOnce()
        {
            if (_tickRegistrationMissReported != 0 ||
                !Application.isPlaying ||
                GlobalRegistry.Dispatcher == null ||
                (_fixedRegistered && _postFixedRegistered && _coldRegistered))
            {
                return;
            }

            _tickRegistrationMissReported = 1;
            uint missingLaneMask =
                (_fixedRegistered ? 0u : 1u) |
                (_postFixedRegistered ? 0u : 2u) |
                (_coldRegistered ? 0u : 4u);
            GlobalTelemetryBus.PublishPerformanceWarning(
                TickRegistrationMissWarningHash,
                ToolKinematicsContextHash ^ missingLaneMask,
                missingLaneMask);
        }

        private void ClearLifecycleDiagnostics()
        {
            _tickRegistrationMissReported = 0;
            _reportedBlackBoxDumpFailureCode = 0;
        }

        private void TryFinalizePendingFrameNoWait()
        {
            if (!_frameScheduled)
                return;

            if (!_pendingHandle.IsCompleted)
                return;

            if (!DispatcherJobFence.TryFinalizeCompleted(ref _pendingHandle))
                return;

            FinishPendingFrameCompletion();
        }

        private void CompletePendingFrameForTeardown()
        {
            if (!_frameScheduled)
                return;

            DispatcherJobFence.BeginPostFixedSwapWindow();
            try
            {
                if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                    return;
            }
            finally
            {
                DispatcherJobFence.EndPostFixedSwapWindow();
            }

            FinishPendingFrameCompletion();
        }

        private void FinishPendingFrameCompletion()
        {
            _frameScheduled = false;
            if (TryResolveAllBuffers(false, out ToolKinematicsBufferSet buffers))
            {
                PublishFrameSignals(buffers);
                if (TelemetryRequiresDump(buffers.TelemetryRing))
                    TryQueueBlackBoxDump(buffers.TelemetryRing);
            }

            _telemetryCursor++;
            if (_telemetryCursor >= ToolKinematicsMath.BlackBoxCapacity)
                _telemetryCursor = 0;
        }

        private void PrepareFrameInputs(ToolKinematicsBufferSet buffers, float safeDeltaTime)
        {
            double3 cameraAup = ResolveCameraAup();
            float stress = ResolveSystemStress(buffers);
            uint frameTriggerFlags = ResolveFrameTriggerFlags();
            for (int i = 0; i < _activeToolCapacity; i++)
            {
                ToolStateDTO state = buffers.States[i];
                if (state.ToolTypeHash == 0u)
                    state.ToolTypeHash = i == 0 ? ToolKinematicsHashes.LaserCutter : ToolKinematicsHashes.Welder;

                float3 controllerLocal = ResolveControllerLocal(i);
                quaternion controllerRotation = ResolveControllerRotation(i);
                float3 shoulderLocal = ResolveShoulderLocal(i);
                uint triggerFlags = frameTriggerFlags;
                triggerFlags |= ResolveToolModeFlag(state.ToolTypeHash);

                state.AUP = cameraAup + new double3(controllerLocal.x, controllerLocal.y, controllerLocal.z);
                if (!math.isfinite(state.MaxEnergyCapacity) || state.MaxEnergyCapacity <= 0.0001f)
                    state.MaxEnergyCapacity = 1f;
                state.EnergyRemaining = math.clamp(math.select(0f, state.EnergyRemaining, math.isfinite(state.EnergyRemaining)), 0f, state.MaxEnergyCapacity);
                state.LastOutputPower01 = 0f;
                state._pad0 = 0u;
                buffers.States[i] = state;
                buffers.FrameInputs[i] = new ToolKinematicsFrameInputDTO
                {
                    CameraAup = cameraAup,
                    ControllerLocalPosition = controllerLocal,
                    ControllerRotation = controllerRotation,
                    ShoulderLocalPosition = shoulderLocal,
                    PoleLocalDirection = new float3(0f, 1f, 0.15f),
                    DeltaTime = safeDeltaTime,
                    SystemHealthIndex = stress,
                    TriggerFlags = triggerFlags,
                    FrameIndex = _frameIndex,
                    _pad0 = 0u
                };

                buffers.TriggerSignals[i] = new ToolTriggerPullSignal
                {
                    ToolSlot = (uint)i,
                    ToolHash = state.ToolTypeHash,
                    Trigger01 = (triggerFlags & ToolKinematicsMath.TriggerPressed) != 0u ? 1f : 0f,
                    Frame = _frameIndex
                };
            }
        }

        private uint ResolveFrameTriggerFlags()
        {
            IInputService inputService = _inputService;
            bool inputInitialized = inputService != null && inputService.IsInitialized;
            bool liveInputAvailable = inputInitialized && inputService.IsPlayerInputEnabled;

            uint liveFlags = 0u;
            if (liveInputAvailable)
            {
                PlayerInputState inputState = inputService.GetState();
                liveFlags = math.select(0u, ToolKinematicsMath.TriggerPressed, inputState.HasAction(PlayerInputAction.PrimaryFire));
            }

            uint syntheticFlags = math.select(0u, ToolKinematicsMath.TriggerPressed, !inputInitialized & useSyntheticInputFallback & syntheticTriggerHeld);
            return math.select(syntheticFlags, liveFlags, liveInputAvailable);
        }

        private float ResolveSystemStress(in ToolKinematicsBufferSet buffers)
        {
            float rawStress = ToolKinematicsMath.Clamp01Finite(systemHealthIndex);
            if (buffers.Tuning.IsCreated && buffers.Tuning.Length > 0)
                rawStress = math.max(rawStress, ToolKinematicsMath.Clamp01Finite(buffers.Tuning[0].SystemHealthIndex));

            return rawStress;
        }

        private void PublishFrameSignals(in ToolKinematicsBufferSet buffers)
        {
            int count = _activeToolCapacity;
            for (int i = 0; i < count; i++)
            {
                ToolTriggerPullSignal trigger = buffers.TriggerSignals[i];
                if (trigger.Frame != 0u && trigger.Trigger01 > 0f)
                {
                    SignalBus<ToolTriggerPullSignal>.TryPushTracked(in trigger, ref _signalPushDropCount);
                    CountConsumerlessLanePublish();
                    PublishGlobalTriggerBridge(in trigger);
                }

                ToolHeatSignal heat = buffers.HeatSignals[i];
                if (heat.Frame != 0u)
                {
                    SignalBus<ToolHeatSignal>.TryPushTracked(in heat, ref _signalPushDropCount);
                    CountConsumerlessLanePublish();
                    PublishToolPowerAndHaptics(in heat);
                    ToolScreenExportDTO screen = (uint)i < (uint)buffers.ScreenExports.Length ? buffers.ScreenExports[i] : default;
                    PublishGlobalToolStateBridge(in heat, in screen);
                    PublishGlobalToolAcousticBridge(in heat, in screen);
                }

                VfxSparkRequestSignal spark = buffers.SparkRequests[i];
                if (spark.Frame != 0u && spark.Intensity01 > 0f)
                {
                    SignalBus<VfxSparkRequestSignal>.TryPushTracked(in spark, ref _signalPushDropCount);
                }

                ToolCarveRequestSignal carve = buffers.CarveRequests[i];
                if (carve.Frame != 0u && carve.MaterialHash != 0u)
                {
                    SignalBus<ToolCarveRequestSignal>.TryPushTracked(in carve, ref _signalPushDropCount);
                    CountConsumerlessLanePublish();
                }
            }
        }

        /// <summary>
        /// Counts one publication into a lane that has no consumer anywhere under Assets/. Main-thread
        /// post-fixed phase only, saturating, no allocation and no branch on managed state.
        /// </summary>
        private static void CountConsumerlessLanePublish()
        {
            int current = _consumerlessLanePublishTotal;
            if (current != int.MaxValue)
                _consumerlessLanePublishTotal = current + 1;
        }

        private static void EnsureSignalLanesReady()
        {
            SignalBus<ToolTriggerPullSignal>.Configure(
                ToolTriggerPullSignal.ExpectedCapacity,
                maxFrameSignals: ToolTriggerPullSignal.MaxFrameSignals,
                lowTierFrameSignals: ToolTriggerPullSignal.LowTierFrameSignals,
                laneHash: ToolTriggerPullSignal.LaneHash);
            SignalBus<ToolTriggerPullSignal>.EnsureInitialized();

            SignalBus<ToolHeatSignal>.Configure(
                ToolHeatSignal.ExpectedCapacity,
                maxFrameSignals: ToolHeatSignal.MaxFrameSignals,
                lowTierFrameSignals: ToolHeatSignal.LowTierFrameSignals,
                laneHash: ToolHeatSignal.LaneHash);
            SignalBus<ToolHeatSignal>.EnsureInitialized();

            SignalBus<VfxSparkRequestSignal>.Configure(
                VfxSparkRequestSignal.ExpectedCapacity,
                maxFrameSignals: VfxSparkRequestSignal.MaxFrameSignals,
                lowTierFrameSignals: VfxSparkRequestSignal.LowTierFrameSignals,
                laneHash: VfxSparkRequestSignal.LaneHash);
            SignalBus<VfxSparkRequestSignal>.EnsureInitialized();

            SignalBus<ToolCarveRequestSignal>.Configure(
                ToolCarveRequestSignal.ExpectedCapacity,
                maxFrameSignals: ToolCarveRequestSignal.MaxFrameSignals,
                lowTierFrameSignals: ToolCarveRequestSignal.LowTierFrameSignals,
                laneHash: ToolCarveRequestSignal.LaneHash);
            SignalBus<ToolCarveRequestSignal>.EnsureInitialized();

            SignalBus<ToolPowerDepletedSignal>.Configure(
                ToolPowerDepletedSignal.ExpectedCapacity,
                maxFrameSignals: ToolPowerDepletedSignal.MaxFrameSignals,
                lowTierFrameSignals: ToolPowerDepletedSignal.LowTierFrameSignals,
                laneHash: ToolPowerDepletedSignal.LaneHash);
            SignalBus<ToolPowerDepletedSignal>.EnsureInitialized();

            SignalCorridorRuntime.EnsureHapticPulseSignalLaneInitialized();
        }

        private static void PublishToolPowerAndHaptics(in ToolHeatSignal heat)
        {
            if ((heat.Flags & (uint)ToolKinematicsFlags.PowerDepletedSignalQueued) != 0u)
            {
                ToolPowerDepletedSignal depleted = new ToolPowerDepletedSignal
                {
                    ToolHash = heat.ToolHash,
                    Frame = heat.Frame,
                    Energy01 = heat.Energy01,
                    Flags = heat.Flags,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
                SignalBus<ToolPowerDepletedSignal>.TryPushTracked(in depleted, ref _signalPushDropCount);
                CountConsumerlessLanePublish();
            }

            if ((heat.Flags & (uint)ToolKinematicsFlags.Active) == 0u)
                return;

            bool lastCharge = (heat.Flags & (uint)ToolKinematicsFlags.LastChargeClutch) != 0u;
            HapticPulseSignal pulse = new HapticPulseSignal
            {
                LowFrequencyMotor01 = lastCharge ? 0.24f : 0.08f,
                HighFrequencyMotor01 = lastCharge ? 0.38f : 0.16f,
                DurationSeconds = lastCharge ? 0.035f : 0.018f,
                PriorityFlags = HapticPulseSignal.PackPriorityAndSourceHash(
                    HapticPulseSignal.PriorityTool,
                    heat.ToolHash)
            };
            SignalBus<HapticPulseSignal>.TryPushTracked(in pulse, ref _signalPushDropCount);
        }

        private static void PublishGlobalTriggerBridge(in ToolTriggerPullSignal trigger)
        {
            ToolTriggerSignal globalTrigger = new ToolTriggerSignal
            {
                Strength = ToolKinematicsMath.Clamp01Finite(trigger.Trigger01),
                SecondaryStrength = 0f,
                Frame = trigger.Frame,
                ControllerMask = 1u << (int)math.min(trigger.ToolSlot, 31u),
                Sequence = (ushort)(trigger.Frame & 0xFFFFu),
                DominantController = (byte)math.min(trigger.ToolSlot, 255u),
                Flags = trigger.Trigger01 > 0.0001f ? (byte)1 : (byte)0
            };
            SignalBus<ToolTriggerSignal>.TryPushTracked(in globalTrigger, ref _signalPushDropCount);
        }

        private static void PublishGlobalToolStateBridge(in ToolHeatSignal heat, in ToolScreenExportDTO screen)
        {
            byte flags = ToolStateChangedSignal.FlagEquipped | ToolStateChangedSignal.FlagVisible;

            ToolStateChangedSignal state = new ToolStateChangedSignal
            {
                ToolHash = heat.ToolHash,
                Frame = heat.Frame,
                Battery01 = ToolKinematicsMath.Clamp01Finite(heat.Energy01),
                Heat01 = ToolKinematicsMath.Clamp01Finite(heat.Heat01),
                DistanceMeters = math.max(0f, screen.HitDistance),
                Durability01 = 1f,
                StatusMask = heat.Flags,
                AmmoUnits = 0,
                Flags = flags,
                ToolTypeId = ResolveToolTypeId(heat.ToolHash)
            };
            SignalBus<ToolStateChangedSignal>.TryPushTracked(in state, ref _signalPushDropCount);
        }

        private static void PublishGlobalToolAcousticBridge(in ToolHeatSignal heat, in ToolScreenExportDTO screen)
        {
            if ((heat.Flags & (uint)ToolKinematicsFlags.Active) == 0u)
                return;

            ToolAcousticSignal acoustic = new ToolAcousticSignal
            {
                ToolHash = heat.ToolHash,
                TargetHash = screen.MaterialHash,
                Progress01 = ToolKinematicsMath.Clamp01Finite(heat.Heat01),
                PitchScale = 1f + ToolKinematicsMath.Clamp01Finite(heat.Heat01) * 0.08f,
                Intensity01 = ToolKinematicsMath.Clamp01Finite(heat.Heat01 + 0.15f),
                Frame = heat.Frame,
                State = ToolAcousticSignal.StateLaserLoop,
                Flags = ToolAcousticSignal.FlagLooping
            };
            SignalBus<ToolAcousticSignal>.TryPushTracked(in acoustic, ref _signalPushDropCount);
        }

        private static byte ResolveToolTypeId(uint toolHash)
        {
            if (toolHash == ToolKinematicsHashes.Scanner)
                return 2;
            if (toolHash == ToolKinematicsHashes.Welder)
                return 3;
            if (toolHash == ToolKinematicsHashes.RivetGun)
                return 4;
            return 1;
        }

        private bool TelemetryRequiresDump(NativeArray<ToolKinematicsTelemetryEntry> telemetryRing)
        {
            if (!telemetryRing.IsCreated)
                return false;

            for (int tool = 0; tool < _activeToolCapacity; tool++)
            {
                int index = tool * ToolKinematicsMath.BlackBoxCapacity + _telemetryCursor;
                if ((uint)index >= (uint)telemetryRing.Length)
                    continue;

                ToolKinematicsTelemetryEntry entry = telemetryRing[index];
                if ((entry.Flags & (uint)ToolKinematicsFlags.Fault) != 0u ||
                    !math.isfinite(entry.ToolHeatLevel) ||
                    !math.isfinite(entry.EnergyRemaining) ||
                    !math.isfinite(entry.HitDistance) ||
                    !math.all(math.isfinite(entry.ToolLocalPosition)) ||
                    !math.all(math.isfinite(entry.HitPoint)))
                {
                    return true;
                }
            }

            return false;
        }

        private bool TryQueueBlackBoxDump(NativeArray<ToolKinematicsTelemetryEntry> telemetryRing)
        {
            if (!telemetryRing.IsCreated)
                return false;

            int max = math.min(telemetryRing.Length, _activeToolCapacity * ToolKinematicsMath.BlackBoxCapacity);
            max = math.min(max, MaxBlackBoxDumpEntries);
            if (max <= 0)
                return false;

            if (Interlocked.CompareExchange(ref _blackBoxDumpState, DumpStateSnapshotting, DumpStateIdle) != DumpStateIdle)
                return false;

            for (int i = 0; i < max; i++)
                _blackBoxDumpEntries[i] = telemetryRing[i];

            _blackBoxDumpFrameIndex = _frameIndex;
            _blackBoxDumpToolCapacity = _activeToolCapacity;
            _blackBoxDumpTelemetryCursor = _telemetryCursor;
            Volatile.Write(ref _blackBoxDumpEntryCount, max);
            Thread.MemoryBarrier();
            Volatile.Write(ref _blackBoxDumpState, DumpStatePending);
            DrainPendingBlackBoxDump();
            return true;
        }

        private void EnsureBlackBoxDumpWorkerCold()
        {
            if (Volatile.Read(ref _blackBoxDumpState) == DumpStateWriting)
                Volatile.Write(ref _blackBoxDumpState, DumpStateIdle);
        }

        private void StopBlackBoxDumpWorker()
        {
            DrainPendingBlackBoxDump();
            Volatile.Write(ref _blackBoxDumpEntryCount, 0);
            Volatile.Write(ref _blackBoxDumpState, DumpStateIdle);
        }

        private void DrainPendingBlackBoxDump()
        {
            if (Interlocked.CompareExchange(ref _blackBoxDumpState, DumpStateWriting, DumpStatePending) != DumpStatePending)
                return;

            bool wrote = false;
            int failureCode = 0;
            try
            {
                wrote = TryWriteQueuedBlackBoxDump();
            }
            catch (IOException)
            {
                failureCode = 2;
            }
            catch (UnauthorizedAccessException)
            {
                failureCode = 3;
            }
            catch (Exception)
            {
                failureCode = 4;
            }
            finally
            {
                if (!wrote)
                {
                    if (failureCode == 0)
                        failureCode = 1;

                    Interlocked.Exchange(ref _blackBoxDumpFailureCode, failureCode);
                }
                else
                {
                    Interlocked.Exchange(ref _blackBoxDumpFailureCode, 0);
                }

                Volatile.Write(ref _blackBoxDumpEntryCount, 0);
                Volatile.Write(ref _blackBoxDumpState, DumpStateIdle);
            }
        }

        private unsafe bool TryWriteQueuedBlackBoxDump()
        {
            int max = math.min(Volatile.Read(ref _blackBoxDumpEntryCount), MaxBlackBoxDumpEntries);
            int entrySize = UnsafeUtility.SizeOf<ToolKinematicsTelemetryEntry>();
            if (entrySize != BlackBoxDumpEntryBytes || max <= 0)
                return false;

            int payloadBytes = max * entrySize;
            int byteCount = BlackBoxDumpHeaderBytes + payloadBytes;
            NativeArray<byte> payload = default;
            try
            {
                payload = NativeFaultDumpWriter.CreateTransientPayload(
                    byteCount,
                    nameof(ToolKinematicsRuntime),
                    DumpPayloadLabel,
                    NativeArrayOptions.UninitializedMemory);

                byte* destination = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(payload);
                int cursor = 0;
                WriteUInt32LittleEndian(destination, ref cursor, BlackBoxDumpMagic);
                WriteUInt32LittleEndian(destination, ref cursor, BlackBoxDumpVersion);
                WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)max));
                WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)entrySize));
                WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)_blackBoxDumpToolCapacity));
                WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)_blackBoxDumpTelemetryCursor));
                WriteUInt32LittleEndian(destination, ref cursor, _blackBoxDumpFrameIndex);
                WriteUInt32LittleEndian(destination, ref cursor, unchecked((uint)payloadBytes));

                for (int i = 0; i < max; i++)
                {
                    int rowEnd = cursor + entrySize;
                    WriteTelemetryEntryLittleEndian(destination, ref cursor, _blackBoxDumpEntries[i]);
                    if (cursor > rowEnd)
                        return false;

                    cursor = rowEnd;
                }

                return cursor == byteCount && NativeFaultDumpWriter.TryWriteAll(BlackBoxDumpRelativePath, payload, cursor);
            }
            finally
            {
                NativeFaultDumpWriter.DisposeTransientPayload(
                    ref payload,
                    nameof(ToolKinematicsRuntime),
                    DumpPayloadLabel);
            }
        }

        private static unsafe void WriteTelemetryEntryLittleEndian(byte* destination, ref int cursor, ToolKinematicsTelemetryEntry entry)
        {
            WriteUInt32LittleEndian(destination, ref cursor, entry.FrameIndex);
            WriteUInt32LittleEndian(destination, ref cursor, entry.ToolHash);
            WriteFloatLittleEndian(destination, ref cursor, entry.ToolHeatLevel);
            WriteFloatLittleEndian(destination, ref cursor, entry.EnergyRemaining);
            WriteFloatLittleEndian(destination, ref cursor, entry.HitDistance);
            WriteInt32LittleEndian(destination, ref cursor, entry.RaymarchStepCount);
            WriteFloatLittleEndian(destination, ref cursor, entry.IkComputeTimeMicroseconds);
            WriteUInt32LittleEndian(destination, ref cursor, entry.Flags);
            WriteFloat3LittleEndian(destination, ref cursor, entry.ToolLocalPosition);
            WriteFloat3LittleEndian(destination, ref cursor, entry.HitPoint);
            WriteUInt32LittleEndian(destination, ref cursor, entry.MaterialHash);
            WriteUInt32LittleEndian(destination, ref cursor, entry._pad0);
        }

        private static unsafe void WriteFloat3LittleEndian(byte* destination, ref int cursor, float3 value)
        {
            WriteFloatLittleEndian(destination, ref cursor, value.x);
            WriteFloatLittleEndian(destination, ref cursor, value.y);
            WriteFloatLittleEndian(destination, ref cursor, value.z);
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
            cursor += sizeof(uint);
        }

        private bool TryResolveAllBuffers(bool allowCreate, out ToolKinematicsBufferSet buffers)
        {
            buffers = default;
            IDataVault vault = _dataVault;
            if (vault == null)
                return false;

            int count = math.clamp(toolCapacity, 1, MaxToolCapacity);
            _activeToolCapacity = count;
            int telemetryLength = count * ToolKinematicsMath.BlackBoxCapacity;
            int beamVertexLength = count * BeamVerticesPerTool;

            bool ok =
                TryResolveVaultView(vault, ref _statesHandle, BufferID.ToolKinematicsStates, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.States) &&
                TryResolveVaultView(vault, ref _frameInputsHandle, BufferID.ToolKinematicsFrameInputs, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.FrameInputs) &&
                TryResolveVaultView(vault, ref _hitResultsHandle, BufferID.ToolKinematicsHitResults, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.HitResults) &&
                TryResolveVaultView(vault, ref _ikOutputsHandle, BufferID.ToolKinematicsIkOutputs, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.IkOutputs) &&
                TryResolveVaultView(vault, ref _recoilStatesHandle, BufferID.ToolKinematicsRecoilStates, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.RecoilStates) &&
                TryResolveVaultView(vault, ref _tuningHandle, BufferID.ToolKinematicsTuning, 1, NativeArrayOptions.ClearMemory, allowCreate, out buffers.Tuning) &&
                TryResolveVaultView(vault, ref _screenExportsHandle, BufferID.ToolKinematicsScreenExports, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.ScreenExports) &&
                TryResolveVaultView(vault, ref _telemetryHandle, BufferID.ToolKinematicsTelemetryRing, telemetryLength, NativeArrayOptions.ClearMemory, allowCreate, out buffers.TelemetryRing) &&
                TryResolveVaultView(vault, ref _triggerSignalsHandle, BufferID.ToolKinematicsTriggerSignals, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.TriggerSignals) &&
                TryResolveVaultView(vault, ref _carveRequestsHandle, BufferID.ToolKinematicsCarveRequests, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.CarveRequests) &&
                TryResolveVaultView(vault, ref _heatSignalsHandle, BufferID.ToolKinematicsHeatSignals, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.HeatSignals) &&
                TryResolveVaultView(vault, ref _sparkRequestsHandle, BufferID.ToolKinematicsSparkRequests, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.SparkRequests) &&
                TryResolveVaultView(vault, ref _beamVerticesHandle, BufferID.ToolKinematicsBeamVertices, beamVertexLength, NativeArrayOptions.ClearMemory, allowCreate, out buffers.BeamVertices) &&
                TryResolveVaultView(vault, ref _beamVertexCountsHandle, BufferID.ToolKinematicsBeamVertexCounts, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.BeamVertexCounts) &&
                TryResolveVaultView(vault, ref _poseOutputsHandle, BufferID.ToolKinematicsPoseOutputs, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.PoseOutputs);

            return ok;
        }

        private bool TryResolveTuning(out NativeArray<ToolKinematicsTuningDTO> tuning)
        {
            IDataVault vault = _dataVault;
            return TryResolveVaultView(vault, ref _tuningHandle, BufferID.ToolKinematicsTuning, 1, NativeArrayOptions.ClearMemory, false, out tuning);
        }

        private bool TryResolveStates(out NativeArray<ToolStateDTO> states)
        {
            IDataVault vault = _dataVault;
            return TryResolveVaultView(vault, ref _statesHandle, BufferID.ToolKinematicsStates, _activeToolCapacity, NativeArrayOptions.ClearMemory, false, out states);
        }

        private bool TryResolveHits(out NativeArray<ToolHitResultDTO> hits)
        {
            IDataVault vault = _dataVault;
            return TryResolveVaultView(vault, ref _hitResultsHandle, BufferID.ToolKinematicsHitResults, _activeToolCapacity, NativeArrayOptions.ClearMemory, false, out hits);
        }

        private void CacheRegistryDependenciesCold()
        {
            if (_dataVault == null)
                _dataVault = GlobalRegistry.DataVault;

            _inputService = GlobalRegistry.Input;
        }

        private bool TryBootstrapRuntime()
        {
            if (!TryResolveAllBuffers(true, out ToolKinematicsBufferSet buffers))
                return false;

#if UNITY_EDITOR
            TryApplyEquipmentStatsCsvCold();
#endif
            WriteTuning(buffers.Tuning);
            SeedEmergencyToolStates(buffers.States, buffers.RecoilStates);
            EnsureSignalLanesReady();
            EnsureBlackBoxDumpWorkerCold();
            TryRegisterFixed();
            TryRegisterPostFixed();
            TryRegisterCold();
            return true;
        }

        private void QueueDataVaultRebind(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            if (_frameScheduled)
            {
                _pendingDataVaultRebind = true;
                _pendingDataVault = vault;
                return;
            }

            ApplyDataVaultRebind(vault);
        }

        private void ApplyPendingDataVaultRebindIfIdle()
        {
            if (!_pendingDataVaultRebind || _frameScheduled)
                return;

            IDataVault vault = _pendingDataVault;
            _pendingDataVaultRebind = false;
            _pendingDataVault = null;
            ApplyDataVaultRebind(vault);
        }

        private void ApplyDataVaultRebind(IDataVault vault)
        {
            if (ReferenceEquals(_dataVault, vault))
                return;

            ReleaseVaultHandles();
            ClearHandles();
            _dataVault = vault;
            if (!isActiveAndEnabled)
                return;

            TryBootstrapRuntime();

            // A hot-swap callback is the only entry point that survives ClearRuntimeBuckets, so it is
            // also the only place a tick lane that failed to come back can still be reported.
            _tickRegistrationMissReported = 0;
            ReportTickRegistrationMissOnce();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Input:
                    _inputService = currentService is IInputService currentInput ? currentInput : GlobalRegistry.Input;
                    break;
                case GlobalRegistryServiceSlot.DataVault:
                    QueueDataVaultRebind(currentService is IDataVault currentVault ? currentVault : null);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    TryUnregisterFixed();
                    TryUnregisterPostFixed();
                    TryUnregisterCold();
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegisterFixed();
                        TryRegisterPostFixed();
                        TryRegisterCold();
                        _tickRegistrationMissReported = 0;
                        ReportTickRegistrationMissOnce();
                    }

                    break;
            }
        }

        private static bool IsOwnedVaultHandle<T>(
            in VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            return handle.BufferID == (uint)expectedBufferId &&
                   handle.Generation != 0u &&
                   handle.SystemID == (uint)SystemID.GameplayTools;
        }

        private static bool TryResolveVaultView<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            bool allowCreate,
            out NativeArray<T> buffer)
            where T : struct
        {
            buffer = default;
            if (vault == null || requiredLength <= 0 || vault.IsCompactionFenceActive)
                return false;

            if (IsOwnedVaultHandle(in handle, bufferId) &&
                !vault.IsCompactionFenceActive &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            if (!IsOwnedVaultHandle(in handle, bufferId))
                handle = default;

            if (!allowCreate)
                return false;

            VaultGenerationHandle<T> acquired = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, SystemID.GameplayTools, options);
            if (!IsOwnedVaultHandle(in acquired, bufferId))
                return false;

            if (vault.IsCompactionFenceActive ||
                !vault.TryResolveHandle(in acquired, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                vault.ReleaseBuffer(in acquired);
                buffer = default;
                return false;
            }

            handle = acquired;
            return true;
        }

        private void WriteTuning(NativeArray<ToolKinematicsTuningDTO> tuning)
        {
            if (!tuning.IsCreated || tuning.Length <= 0)
                return;

#if UNITY_EDITOR
            uint csvFaultFlag = Volatile.Read(ref _csvReadFaultCode) != 0 ? (uint)ToolKinematicsFlags.CsvIoFault : 0u;
#else
            const uint csvFaultFlag = 0u;
#endif
            ToolKinematicsTuningDTO current = tuning[0];
            bool existingValid = current.LaserRange > 0.0001f && current.MaxHeat > 0.0001f;
            uint desiredFlags = (current.Flags & ~(uint)ToolKinematicsFlags.CsvIoFault) | csvFaultFlag;
            if (Volatile.Read(ref _tuningDirty) == 0 && existingValid)
            {
                if (current.Flags == desiredFlags && current._pad0 == 0u)
                    return;

                current.Flags = desiredFlags;
                current._pad0 = 0u;
                tuning[0] = current;
                return;
            }

            tuning[0] = new ToolKinematicsTuningDTO
            {
                LaserRange = math.max(0.1f, laserRange),
                HeatRampRate = math.max(0f, heatRampRate),
                CoolingRate = math.max(0f, coolingRate),
                MaxHeat = math.max(0.1f, maxHeat),
                EnergyDrainRate = math.max(0f, energyDrainRate),
                RecoilStrength = math.max(0f, recoilStrength),
                SpringDamping = math.max(0f, springDamping),
                CollisionSpring = math.max(0f, collisionSpring),
                BeamRadius = math.max(0.002f, beamRadius),
                SystemHealthIndex = ToolKinematicsMath.Clamp01Finite(systemHealthIndex),
                Flags = csvFaultFlag,
                _pad0 = 0u
            };
            Volatile.Write(ref _tuningDirty, 0);
        }

        private static void SeedEmergencyToolStates(NativeArray<ToolStateDTO> states, NativeArray<ToolRecoilStateDTO> recoilStates)
        {
            if (!states.IsCreated)
                return;

            for (int i = 0; i < states.Length; i++)
            {
                ToolStateDTO state = states[i];
                if (state.ToolTypeHash == 0u)
                {
                    state.AUP = default;
                    state.Forward = new float3(0f, 0f, 1f);
                    state.HeatLevel = 0f;
                    state.ToolTypeHash = ResolveSeedToolHash(i);
                    state.EnergyRemaining = 1f;
                    state.MaxEnergyCapacity = 1f;
                    state.StateFlags = 0u;
                    state.LastOutputPower01 = 0f;
                    state._pad0 = 0u;
                    states[i] = state;
                }

                if (recoilStates.IsCreated && (uint)i < (uint)recoilStates.Length)
                {
                    ToolRecoilStateDTO recoil = recoilStates[i];
                    recoil.PivotLocal = new float3(0f, -0.04f, 0.22f);
                    recoilStates[i] = recoil;
                }
            }
        }

        private static uint ResolveSeedToolHash(int index)
        {
            switch (index & 3)
            {
                case 1:
                    return ToolKinematicsHashes.Welder;
                case 2:
                    return ToolKinematicsHashes.Scanner;
                case 3:
                    return ToolKinematicsHashes.RivetGun;
                default:
                    return ToolKinematicsHashes.LaserCutter;
            }
        }

        private static uint ResolveToolModeFlag(uint toolHash)
        {
            if (toolHash == ToolKinematicsHashes.Scanner)
                return ToolKinematicsMath.TriggerScannerMode;
            if (toolHash == ToolKinematicsHashes.Welder)
                return ToolKinematicsMath.TriggerWelderMode;
            return ToolKinematicsMath.TriggerLaserMode;
        }

        private double3 ResolveCameraAup()
        {
            if (cameraAnchor == null)
                return default;

            Vector3 position = cameraAnchor.position;
            return new double3(position.x, position.y, position.z);
        }

        private float3 ResolveControllerLocal(int index)
        {
            if (!useSyntheticInputFallback &&
                controllerSources != null &&
                (uint)index < (uint)controllerSources.Length &&
                controllerSources[index] != null)
            {
                Vector3 local = cameraAnchor != null
                    ? cameraAnchor.InverseTransformPoint(controllerSources[index].position)
                    : controllerSources[index].position;
                return new float3(local.x, local.y, local.z);
            }

            float side = (index & 1) == 0 ? -1f : 1f;
            float wave = ToolKinematicsMath.NoiseSigned(ToolKinematicsMath.Mix(_frameIndex, (uint)index + 17u)) * 0.015f;
            return new float3(side * 0.24f, -0.18f + wave, 0.74f + 0.04f * index);
        }

        private quaternion ResolveControllerRotation(int index)
        {
            if (!useSyntheticInputFallback &&
                controllerSources != null &&
                (uint)index < (uint)controllerSources.Length &&
                controllerSources[index] != null)
            {
                Quaternion rotation = controllerSources[index].rotation;
                return new quaternion(rotation.x, rotation.y, rotation.z, rotation.w);
            }

            float yaw = ((index & 1) == 0 ? -6f : 6f) * 0.017453292f;
            return quaternion.EulerXYZ(new float3(0f, yaw, 0f));
        }

        private float3 ResolveShoulderLocal(int index)
        {
            if (!useSyntheticInputFallback &&
                shoulderAnchors != null &&
                (uint)index < (uint)shoulderAnchors.Length &&
                shoulderAnchors[index] != null)
            {
                Vector3 local = cameraAnchor != null
                    ? cameraAnchor.InverseTransformPoint(shoulderAnchors[index].position)
                    : shoulderAnchors[index].position;
                return new float3(local.x, local.y, local.z);
            }

            float side = (index & 1) == 0 ? -1f : 1f;
            return new float3(side * 0.18f, -0.2f, 0.08f);
        }

#if UNITY_EDITOR
        private void TryApplyEquipmentStatsCsvCold()
        {
            string path = _equipmentStatsPath;
            if (string.IsNullOrEmpty(path))
                return;

            if (!Hecton8.SaveSystem.AsyncWriteManager.TryGetFileLength(path, out long fileLength, out _))
                return;

            long clampedLength = fileLength > CsvBufferBytes ? CsvBufferBytes : fileLength;
            if (clampedLength <= 0L)
                return;

            int bytesRead = (int)clampedLength;
            NativeArray<byte> bytes = new NativeArray<byte>(
                bytesRead,
                Allocator.Temp,
                NativeArrayOptions.UninitializedMemory);
            try
            {
                if (!Hecton8.SaveSystem.AsyncWriteManager.TryCopyFileRangeToNativeArray(path, 0L, bytes, bytesRead, out _))
                {
                    Interlocked.Exchange(ref _csvReadFaultCode, 1);
                    return;
                }

                ParseEquipmentStatsCsv(bytes, bytesRead);
                Interlocked.Exchange(ref _csvReadFaultCode, 0);
            }
            catch (Exception)
            {
                Interlocked.Exchange(ref _csvReadFaultCode, 3);
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }
        }

        private void ParseEquipmentStatsCsv(NativeArray<byte> bytes, int length)
        {
            int lineStart = 0;
            for (int i = 0; i <= length; i++)
            {
                bool lineEnd = i == length || bytes[i] == (byte)'\n' || bytes[i] == (byte)'\r';
                if (!lineEnd)
                    continue;

                ParseEquipmentStatsLine(bytes, lineStart, i);
                if (i + 1 < length && bytes[i] == (byte)'\r' && bytes[i + 1] == (byte)'\n')
                    i++;
                lineStart = i + 1;
            }
        }

        private void ParseEquipmentStatsLine(NativeArray<byte> bytes, int start, int end)
        {
            if (end <= start)
                return;

            int separator = -1;
            for (int i = start; i < end; i++)
            {
                if (bytes[i] == (byte)',' || bytes[i] == (byte)'=' || bytes[i] == (byte)':')
                {
                    separator = i;
                    break;
                }
            }

            if (separator <= start || separator + 1 >= end)
                return;

            uint key = HashCsvKey(bytes, start, separator);
            if (!TryParseFloatAscii(bytes, separator + 1, end, out float value))
                return;

            ApplyEquipmentOverride(key, value);
        }

        private void ApplyEquipmentOverride(uint key, float value)
        {
            if (!math.isfinite(value))
                return;

            switch ((EquipmentCsvKey)key)
            {
                case EquipmentCsvKey.LaserRange:
                    laserRange = math.clamp(value, 0.1f, 60f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.HeatRampRate:
                    heatRampRate = math.clamp(value, 0f, 8f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.CoolingRate:
                    coolingRate = math.clamp(value, 0f, 8f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.MaxHeat:
                    maxHeat = math.clamp(value, 0.1f, 4f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.EnergyDrainRate:
                    energyDrainRate = math.clamp(value, 0f, 4f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.RecoilStrength:
                    recoilStrength = math.clamp(value, 0f, 2f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.SpringDamping:
                    springDamping = math.clamp(value, 0f, 64f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.CollisionSpring:
                    collisionSpring = math.clamp(value, 0f, 8f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.BeamRadius:
                    beamRadius = math.clamp(value, 0.002f, 0.12f);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
                case EquipmentCsvKey.SystemHealth:
                case EquipmentCsvKey.SystemHealthIndex:
                    systemHealthIndex = ToolKinematicsMath.Clamp01Finite(value);
                    Volatile.Write(ref _tuningDirty, 1);
                    break;
            }
        }

        private static uint HashCsvKey(NativeArray<byte> bytes, int start, int end)
        {
            uint hash = 2166136261u;
            for (int i = start; i < end; i++)
            {
                byte b = bytes[i];
                if (b == (byte)' ' || b == (byte)'\t' || b == (byte)'_' || b == (byte)'-')
                    continue;

                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);

                hash ^= b;
                hash *= 16777619u;
            }

            return hash;
        }

        private static bool TryParseFloatAscii(NativeArray<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            while (start < end && (bytes[start] == (byte)' ' || bytes[start] == (byte)'\t'))
                start++;

            bool negative = false;
            if (start < end && (bytes[start] == (byte)'-' || bytes[start] == (byte)'+'))
            {
                negative = bytes[start] == (byte)'-';
                start++;
            }

            float integer = 0f;
            bool sawDigit = false;
            while (start < end && bytes[start] >= (byte)'0' && bytes[start] <= (byte)'9')
            {
                integer = integer * 10f + (bytes[start] - (byte)'0');
                start++;
                sawDigit = true;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (start < end && bytes[start] == (byte)'.')
            {
                start++;
                while (start < end && bytes[start] >= (byte)'0' && bytes[start] <= (byte)'9')
                {
                    fraction = fraction * 10f + (bytes[start] - (byte)'0');
                    divisor *= 10f;
                    start++;
                    sawDigit = true;
                }
            }

            if (!sawDigit)
                return false;

            value = integer + fraction / divisor;
            if (negative)
                value = -value;

            return true;
        }
#endif

        /// <summary>
        /// Re-arms the dispatcher fixed lane. Deliberately NOT gated on <c>_fixedRegistered</c>, and the
        /// flag is only ever RAISED on a successful insert. All three tick lanes are emptied behind this
        /// owner's back by <c>GlobalRegistry.ClearRuntimeBuckets</c> (GlobalRegistry.cs:6984-6997, which
        /// clears <c>_fixedTickables</c>/<c>_coldTickables</c> and calls
        /// <c>SystemDispatcher.ClearAllLanes</c>), reached from any unsuppressed scene unload, and that
        /// path touches no service slot so it notifies nobody. A latched early return therefore made the
        /// drop permanent. The surviving entry point is the hot-swap listener bucket, which
        /// <c>ClearRuntimeBuckets</c> does NOT clear (<c>_hotSwapListeners</c>, GlobalRegistry.cs:311):
        /// a DataVault replacement reaches <c>ApplyDataVaultRebind</c> -> <c>TryBootstrapRuntime</c> ->
        /// here. Repeat attempts are free and cannot double-register - the buckets are
        /// <c>RegistryBucket&lt;T&gt;</c> whose <c>TryRegister</c> rejects a duplicate via
        /// <c>Contains</c> (RegistryBucket.cs:112/115) and rolls the dispatcher lane back on failure
        /// (GlobalRegistry.cs:6515-6519). Assigning the result instead of raising it would clear a flag
        /// that must stay set, stranding the live lane entry with no owner willing to unregister it.
        /// </summary>
        private void TryRegisterFixed()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player))
                _fixedRegistered = true;
        }

        private void TryRegisterPostFixed()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player))
                _postFixedRegistered = true;
        }

        private void TryRegisterCold()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            if (GlobalRegistry.TryRegisterColdTickable(this, PriorityLayer.Player))
                _coldRegistered = true;
        }

        private void TryRegisterHotSwap()
        {
            if (_registeredHotSwap || !Application.isPlaying)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwap()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void TryUnregisterFixed()
        {
            if (!_fixedRegistered)
                return;

            GlobalRegistry.UnregisterFixedTickable(this, PriorityLayer.Player);
            _fixedRegistered = false;
        }

        private void TryUnregisterPostFixed()
        {
            if (!_postFixedRegistered)
                return;

            GlobalRegistry.UnregisterPostFixedTickable(this, PriorityLayer.Player);
            _postFixedRegistered = false;
        }

        private void TryUnregisterCold()
        {
            if (!_coldRegistered)
                return;

            GlobalRegistry.UnregisterColdTickable(this, PriorityLayer.Player);
            _coldRegistered = false;
        }

        /// <summary>
        /// The ARM64 layout gate, and the hardest kill switch in this file: OnEnable returns before every
        /// tick registration when this returns false, so the tool ships authored and inert.
        /// It used to compare <c>UnsafeUtility.SizeOf&lt;T&gt;()</c> against a literal and nothing else.
        /// Every type below is declared <c>[StructLayout(LayoutKind.Explicit, Size = N)]</c>, and that
        /// attribute PINS the size on every platform - so a size-only gate agreed with itself by
        /// construction and could not fail for the ARM64 reason it exists. It also skipped four of the six
        /// SignalBus payloads this runtime configures and pushes, which data.md:123-134 covers explicitly.
        /// What actually varies, and what actually breaks ARM64, is where each field SITS: a 16-byte
        /// <c>float4</c> lane or an 8-byte field parked on a 4-aligned offset is the "misaligned read on
        /// ARM64" of data.md:34. So the offsets are now proven too, using the pattern this project already
        /// established for exactly this in GlobalDataVault.ValidateInternalDtoAbiOffsets
        /// (Core/Memory/GlobalDataVault.cs:1061-1146). Cold, once per component Awake.
        /// </summary>
        private static bool ValidateAbiLayout()
        {
            bool valid = ValidateAbiSizes() && ValidateAbiFieldOffsets();

            if (!valid)
                Hecton8.Core.H8Debug.LogError("[ToolKinematicsRuntime] ARM64 DTO layout mismatch. Runtime disabled.");

            return valid;
        }

        private static bool ValidateAbiSizes()
        {
            return
                UnsafeUtility.SizeOf<ToolStateDTO>() == 64 &&
                UnsafeUtility.SizeOf<ToolHitResultDTO>() == 32 &&
                UnsafeUtility.SizeOf<ToolScreenExportDTO>() == 16 &&
                UnsafeUtility.SizeOf<ToolKinematicsTuningDTO>() == 48 &&
                UnsafeUtility.SizeOf<ToolKinematicsFrameInputDTO>() == 96 &&
                UnsafeUtility.SizeOf<ToolIkOutputDTO>() == 64 &&
                UnsafeUtility.SizeOf<ToolRecoilStateDTO>() == 64 &&
                UnsafeUtility.SizeOf<ToolBeamVertexDTO>() == 32 &&
                UnsafeUtility.SizeOf<ToolPoseOutputDTO>() == 96 &&
                UnsafeUtility.SizeOf<ToolKinematicsTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<ToolTriggerPullSignal>() == 16 &&
                UnsafeUtility.SizeOf<ToolCarveRequestSignal>() == 64 &&
                UnsafeUtility.SizeOf<ToolHeatSignal>() == 32 &&
                UnsafeUtility.SizeOf<ToolPowerDepletedSignal>() == 32 &&
                UnsafeUtility.SizeOf<VfxSparkRequestSignal>() == 64 &&
                UnsafeUtility.SizeOf<HapticPulseSignal>() == 16 &&
                UnsafeUtility.SizeOf<ToolProceduralSdfSample>() == 8 &&

                // Element k of a vault buffer inherits the record's interior alignment only if the stride
                // is a multiple of it. Both records below carry a 16-byte float4 lane, so their strides
                // must stay multiples of 16 or the lane is aligned for element 0 alone. The arena base is
                // 64-aligned (Core/Memory/GlobalDataVault.cs:448 VaultBlockAlignment).
                (UnsafeUtility.SizeOf<ToolKinematicsFrameInputDTO>() & 15) == 0 &&
                (UnsafeUtility.SizeOf<ToolIkOutputDTO>() & 15) == 0;
        }

        /// <summary>
        /// Proves the byte offset of every field of the records whose interior layout is load-bearing:
        /// the two Burst records that carry a 16-byte float4 lane, the only record with an 8-byte field
        /// (<c>ToolStateDTO.AUP</c>), the telemetry entry whose field order IS the on-disk field order of
        /// <c>Docs/AgentLogs/Dump_TOOL_KINEMATICS.bin</c> (WriteTelemetryEntryLittleEndian above walks it
        /// in declaration order against a fixed 64-byte row), and the four SignalBus payloads with 8-byte
        /// padding fields. A wrong <c>[FieldOffset]</c> in any of them is invisible to a size check.
        /// </summary>
        private static unsafe bool ValidateAbiFieldOffsets()
        {
            ToolStateDTO state = default;
            ToolKinematicsFrameInputDTO frameInput = default;
            ToolIkOutputDTO ikOutput = default;
            ToolKinematicsTelemetryEntry telemetry = default;
            ToolCarveRequestSignal carve = default;
            ToolHeatSignal heat = default;
            ToolPowerDepletedSignal depleted = default;
            VfxSparkRequestSignal spark = default;

            byte* stateBase = (byte*)&state;
            byte* frameInputBase = (byte*)&frameInput;
            byte* ikOutputBase = (byte*)&ikOutput;
            byte* telemetryBase = (byte*)&telemetry;
            byte* carveBase = (byte*)&carve;
            byte* heatBase = (byte*)&heat;
            byte* depletedBase = (byte*)&depleted;
            byte* sparkBase = (byte*)&spark;

            return
                ByteOffset(stateBase, &state.AUP) == 0 &&
                ByteOffset(stateBase, &state.Forward) == 24 &&
                ByteOffset(stateBase, &state.HeatLevel) == 36 &&
                ByteOffset(stateBase, &state.ToolTypeHash) == 40 &&
                ByteOffset(stateBase, &state.EnergyRemaining) == 44 &&
                ByteOffset(stateBase, &state.MaxEnergyCapacity) == 48 &&
                ByteOffset(stateBase, &state.StateFlags) == 52 &&
                ByteOffset(stateBase, &state.LastOutputPower01) == 56 &&
                ByteOffset(stateBase, &state._pad0) == 60 &&
                ByteOffset(frameInputBase, &frameInput.CameraAup) == 0 &&
                ByteOffset(frameInputBase, &frameInput.TriggerFlags) == 24 &&
                ByteOffset(frameInputBase, &frameInput.FrameIndex) == 28 &&
                ByteOffset(frameInputBase, &frameInput.ControllerRotation) == 32 &&
                ByteOffset(frameInputBase, &frameInput.ControllerLocalPosition) == 48 &&
                ByteOffset(frameInputBase, &frameInput.DeltaTime) == 60 &&
                ByteOffset(frameInputBase, &frameInput.ShoulderLocalPosition) == 64 &&
                ByteOffset(frameInputBase, &frameInput.SystemHealthIndex) == 76 &&
                ByteOffset(frameInputBase, &frameInput.PoleLocalDirection) == 80 &&
                ByteOffset(frameInputBase, &frameInput._pad0) == 92 &&
                ByteOffset(ikOutputBase, &ikOutput.UpperRotation) == 0 &&
                ByteOffset(ikOutputBase, &ikOutput.Shoulder) == 16 &&
                ByteOffset(ikOutputBase, &ikOutput.Flags) == 28 &&
                ByteOffset(ikOutputBase, &ikOutput.Elbow) == 32 &&
                ByteOffset(ikOutputBase, &ikOutput.ComputeMicrosecondsEstimate) == 44 &&
                ByteOffset(ikOutputBase, &ikOutput.Wrist) == 48 &&
                ByteOffset(ikOutputBase, &ikOutput._pad0) == 60 &&
                ByteOffset(telemetryBase, &telemetry.FrameIndex) == 0 &&
                ByteOffset(telemetryBase, &telemetry.ToolHash) == 4 &&
                ByteOffset(telemetryBase, &telemetry.ToolHeatLevel) == 8 &&
                ByteOffset(telemetryBase, &telemetry.EnergyRemaining) == 12 &&
                ByteOffset(telemetryBase, &telemetry.HitDistance) == 16 &&
                ByteOffset(telemetryBase, &telemetry.RaymarchStepCount) == 20 &&
                ByteOffset(telemetryBase, &telemetry.IkComputeTimeMicroseconds) == 24 &&
                ByteOffset(telemetryBase, &telemetry.Flags) == 28 &&
                ByteOffset(telemetryBase, &telemetry.ToolLocalPosition) == 32 &&
                ByteOffset(telemetryBase, &telemetry.HitPoint) == 44 &&
                ByteOffset(telemetryBase, &telemetry.MaterialHash) == 56 &&
                ByteOffset(telemetryBase, &telemetry._pad0) == 60 &&
                ByteOffset(carveBase, &carve.HitPoint) == 0 &&
                ByteOffset(carveBase, &carve.Normal) == 12 &&
                ByteOffset(carveBase, &carve.ToolHash) == 24 &&
                ByteOffset(carveBase, &carve.MaterialHash) == 28 &&
                ByteOffset(carveBase, &carve.Frame) == 32 &&
                ByteOffset(carveBase, &carve.Power01) == 36 &&
                ByteOffset(carveBase, &carve.Flags) == 40 &&
                ByteOffset(carveBase, &carve._pad0) == 44 &&
                ByteOffset(carveBase, &carve._pad1) == 48 &&
                ByteOffset(carveBase, &carve._pad2) == 56 &&
                ByteOffset(heatBase, &heat.ToolHash) == 0 &&
                ByteOffset(heatBase, &heat.Frame) == 4 &&
                ByteOffset(heatBase, &heat.Heat01) == 8 &&
                ByteOffset(heatBase, &heat.Energy01) == 12 &&
                ByteOffset(heatBase, &heat.Flags) == 16 &&
                ByteOffset(heatBase, &heat._pad0) == 20 &&
                ByteOffset(heatBase, &heat._pad1) == 24 &&
                ByteOffset(depletedBase, &depleted.ToolHash) == 0 &&
                ByteOffset(depletedBase, &depleted.Frame) == 4 &&
                ByteOffset(depletedBase, &depleted.Energy01) == 8 &&
                ByteOffset(depletedBase, &depleted.Flags) == 12 &&
                ByteOffset(depletedBase, &depleted._pad0) == 16 &&
                ByteOffset(depletedBase, &depleted._pad1) == 24 &&
                ByteOffset(sparkBase, &spark.HitPoint) == 0 &&
                ByteOffset(sparkBase, &spark.Normal) == 12 &&
                ByteOffset(sparkBase, &spark.MaterialHash) == 24 &&
                ByteOffset(sparkBase, &spark.ToolHash) == 28 &&
                ByteOffset(sparkBase, &spark.Intensity01) == 32 &&
                ByteOffset(sparkBase, &spark.Frame) == 36 &&
                ByteOffset(sparkBase, &spark._pad0) == 40 &&
                ByteOffset(sparkBase, &spark._pad1) == 48 &&
                ByteOffset(sparkBase, &spark._pad2) == 56;
        }

        private static unsafe int ByteOffset(void* basePtr, void* fieldPtr)
        {
            return (int)((byte*)fieldPtr - (byte*)basePtr);
        }

#if UNITY_EDITOR
        private static string ResolveEquipmentStatsPath()
        {
            return Path.Combine(ResolveProjectRootPath(), EquipmentStatsFileName);
        }
#endif

        private static string ResolveProjectRootPath()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return Directory.GetCurrentDirectory();

            string root = Path.GetDirectoryName(dataPath);
            return string.IsNullOrEmpty(root) ? Directory.GetCurrentDirectory() : root;
        }

        private void ReleaseVaultHandles()
        {
            IDataVault vault = _dataVault;
            if (vault == null)
                return;

            ReleaseVaultHandle(vault, ref _statesHandle, BufferID.ToolKinematicsStates);
            ReleaseVaultHandle(vault, ref _frameInputsHandle, BufferID.ToolKinematicsFrameInputs);
            ReleaseVaultHandle(vault, ref _hitResultsHandle, BufferID.ToolKinematicsHitResults);
            ReleaseVaultHandle(vault, ref _ikOutputsHandle, BufferID.ToolKinematicsIkOutputs);
            ReleaseVaultHandle(vault, ref _recoilStatesHandle, BufferID.ToolKinematicsRecoilStates);
            ReleaseVaultHandle(vault, ref _tuningHandle, BufferID.ToolKinematicsTuning);
            ReleaseVaultHandle(vault, ref _screenExportsHandle, BufferID.ToolKinematicsScreenExports);
            ReleaseVaultHandle(vault, ref _telemetryHandle, BufferID.ToolKinematicsTelemetryRing);
            ReleaseVaultHandle(vault, ref _triggerSignalsHandle, BufferID.ToolKinematicsTriggerSignals);
            ReleaseVaultHandle(vault, ref _carveRequestsHandle, BufferID.ToolKinematicsCarveRequests);
            ReleaseVaultHandle(vault, ref _heatSignalsHandle, BufferID.ToolKinematicsHeatSignals);
            ReleaseVaultHandle(vault, ref _sparkRequestsHandle, BufferID.ToolKinematicsSparkRequests);
            ReleaseVaultHandle(vault, ref _beamVerticesHandle, BufferID.ToolKinematicsBeamVertices);
            ReleaseVaultHandle(vault, ref _beamVertexCountsHandle, BufferID.ToolKinematicsBeamVertexCounts);
            ReleaseVaultHandle(vault, ref _poseOutputsHandle, BufferID.ToolKinematicsPoseOutputs);
        }

        private static void ReleaseVaultHandle<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID expectedBufferId) where T : struct
        {
            if (!IsOwnedVaultHandle(in handle, expectedBufferId))
            {
                handle = default;
                return;
            }

            vault.ReleaseBuffer(in handle);
            handle = default;
        }

        private bool TryAcquireFrameMutationGuard(IDataVault vault)
        {
            if (vault == null ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(FrameMutationGuardMask))
            {
                return false;
            }
            return true;
        }

        private static void ReleaseFrameMutationGuard(IDataVault vault)
        {
            if (vault != null)
                vault.ReleaseMutationGuard(FrameMutationGuardMask);
        }

        private void ClearHandles()
        {
            _statesHandle = default;
            _frameInputsHandle = default;
            _hitResultsHandle = default;
            _ikOutputsHandle = default;
            _recoilStatesHandle = default;
            _tuningHandle = default;
            _screenExportsHandle = default;
            _telemetryHandle = default;
            _triggerSignalsHandle = default;
            _carveRequestsHandle = default;
            _heatSignalsHandle = default;
            _sparkRequestsHandle = default;
            _beamVerticesHandle = default;
            _beamVertexCountsHandle = default;
            _poseOutputsHandle = default;
            _dataVault = null;
            _pendingDataVaultRebind = false;
            _pendingDataVault = null;
        }

        private static ulong ToolMutationGuardBit(BufferID bufferId)
        {
            return 1UL << (unchecked((int)(uint)(int)bufferId) & 31);
        }

        private ref struct ToolKinematicsBufferSet
        {
            public NativeArray<ToolStateDTO> States;
            public NativeArray<ToolKinematicsFrameInputDTO> FrameInputs;
            public NativeArray<ToolHitResultDTO> HitResults;
            public NativeArray<ToolIkOutputDTO> IkOutputs;
            public NativeArray<ToolRecoilStateDTO> RecoilStates;
            public NativeArray<ToolKinematicsTuningDTO> Tuning;
            public NativeArray<ToolScreenExportDTO> ScreenExports;
            public NativeArray<ToolKinematicsTelemetryEntry> TelemetryRing;
            public NativeArray<ToolTriggerPullSignal> TriggerSignals;
            public NativeArray<ToolCarveRequestSignal> CarveRequests;
            public NativeArray<ToolHeatSignal> HeatSignals;
            public NativeArray<VfxSparkRequestSignal> SparkRequests;
            public NativeArray<ToolBeamVertexDTO> BeamVertices;
            public NativeArray<int> BeamVertexCounts;
            public NativeArray<ToolPoseOutputDTO> PoseOutputs;
        }
    }
}
