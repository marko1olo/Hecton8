using System;
using System.IO;
using System.Threading;
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
    public sealed class ToolKinematicsRuntime : MonoBehaviour, IFixedTickable, IPostFixedTickable, ISlowTickable, IGlobalRegistryHotSwapListener
    {
        private static int _signalPushDropCount;
        public const int MaxToolCapacity = 8;
        public const int BeamVerticesPerTool = 64;
#if UNITY_EDITOR
        private const int CsvBufferBytes = 4096;
        private const string EquipmentStatsFileName = "equipment_stats.csv";
#endif
        private const string BlackBoxDumpFileName = "Dump_13US.bin";
        private const int MaxBlackBoxDumpEntries = MaxToolCapacity * ToolKinematicsMath.BlackBoxCapacity;
        private const int BlackBoxDumpWorkerJoinMilliseconds = 50;
        private const int BlackBoxDumpWorkerPollMilliseconds = 250;
        private const int DumpStateIdle = 0;
        private const int DumpStateSnapshotting = 1;
        private const int DumpStatePending = 2;
        private const int DumpStateWriting = 3;

        [SerializeField] private int toolCapacity = 2;
        [SerializeField] private Transform cameraAnchor;
        [SerializeField] private Transform[] controllerSources;
        [SerializeField] private Transform[] shoulderAnchors;
        [SerializeField] private bool useMockInput = true;
        [SerializeField] private bool mockTriggerHeld = true;
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

#if UNITY_EDITOR
        private readonly byte[] _csvIoBuffer = new byte[CsvBufferBytes]; // COLD ALLOC: byte[4096] - background CSV read buffer - owner: ToolKinematicsRuntime
        private readonly byte[] _csvPendingBuffer = new byte[CsvBufferBytes]; // COLD ALLOC: byte[4096] - worker/main handoff buffer - owner: ToolKinematicsRuntime
        private readonly byte[] _csvConsumeBuffer = new byte[CsvBufferBytes]; // COLD ALLOC: byte[4096] - main-thread parse buffer - owner: ToolKinematicsRuntime
        private readonly object _csvGate = new object(); // COLD ALLOC: object[1] - background-to-main CSV handoff lock - owner: ToolKinematicsRuntime
#endif
        private readonly ToolKinematicsTelemetryEntry[] _blackBoxDumpEntries = new ToolKinematicsTelemetryEntry[MaxBlackBoxDumpEntries]; // COLD ALLOC: ToolKinematicsTelemetryEntry[2400] - fault snapshot handoff buffer - owner: ToolKinematicsRuntime

        private IDataVault _dataVault;
        private VaultGenerationHandle<ToolStateDTO> _statesHandle;
        private VaultGenerationHandle<ToolKinematicsFrameInputDTO> _frameInputsHandle;
        private VaultGenerationHandle<ToolHitResultDTO> _hitResultsHandle;
        private VaultGenerationHandle<ToolIkOutputDTO> _ikOutputsHandle;
        private VaultGenerationHandle<ToolRecoilStateDTO> _recoilStatesHandle;
        private VaultGenerationHandle<ToolKinematicsTuningDTO> _tuningHandle;
        private VaultGenerationHandle<ToolScreenExportDTO> _screenExportsHandle;
        private VaultGenerationHandle<ToolKinematicsTelemetryEntry> _telemetryHandle;
        private VaultGenerationHandle<MockTriggerPullSignal> _mockTriggerSignalsHandle;
        private VaultGenerationHandle<MockCarveRequestSignal> _carveRequestsHandle;
        private VaultGenerationHandle<ToolHeatSignal> _heatSignalsHandle;
        private VaultGenerationHandle<VfxSparkRequestSignal> _sparkRequestsHandle;
        private VaultGenerationHandle<ToolBeamVertexDTO> _beamVerticesHandle;
        private VaultGenerationHandle<int> _beamVertexCountsHandle;
        private VaultGenerationHandle<ToolPoseOutputDTO> _poseOutputsHandle;

        private JobHandle _pendingHandle;
#if UNITY_EDITOR
        private Thread _csvThread;
        private string _equipmentStatsPath;
        private long _equipmentStatsStampUtcTicks;
        private int _csvThreadRun;
        private int _csvPendingBytes;
        private int _csvPendingSequence;
        private int _csvConsumedSequence;
        private int _csvThreadFaultCode;
#endif
        private AutoResetEvent _blackBoxDumpSignal;
        private Thread _blackBoxDumpThread;
        private string _blackBoxDumpPath;
        private int _blackBoxDumpRun;
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
        private bool _slowRegistered;
        private bool _registeredHotSwap;
        private bool _pendingDataVaultRebind;
        private bool _abiValid;
        private IDataVault _pendingDataVault;

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

        private void Awake()
        {
            _abiValid = ValidateAbiLayout();
            _activeToolCapacity = math.clamp(toolCapacity, 1, MaxToolCapacity);
#if UNITY_EDITOR
            _equipmentStatsPath = ResolveEquipmentStatsPath();
#endif
            _blackBoxDumpPath = ResolveBlackBoxDumpPath();
            CacheRegistryDependenciesCold();
        }

        private void OnValidate()
        {
            Volatile.Write(ref _tuningDirty, 1);
        }

        private void OnEnable()
        {
            if (!_abiValid)
                return;

            _activeToolCapacity = math.clamp(toolCapacity, 1, MaxToolCapacity);
            CacheRegistryDependenciesCold();
            TryRegisterHotSwap();
            TryBootstrapRuntime();
        }

        private void OnDisable()
        {
            CompletePendingFrameForTeardown();
            StopBlackBoxDumpWorker();
#if UNITY_EDITOR
            StopCsvWatcher();
#endif
            TryUnregisterFixed();
            TryUnregisterPostFixed();
            TryUnregisterSlow();
            TryUnregisterHotSwap();
            ReleaseVaultHandles();
            ClearHandles();
        }

        private void OnDestroy()
        {
            CompletePendingFrameForTeardown();
            StopBlackBoxDumpWorker();
            TryUnregisterHotSwap();
#if UNITY_EDITOR
            StopCsvWatcher();
#endif
            ReleaseVaultHandles();
            ClearHandles();
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!_abiValid || _frameScheduled)
                return;

            ApplyPendingDataVaultRebindIfIdle();
            float safeDeltaTime = math.clamp(ToolKinematicsMath.ClampPositiveFinite(fixedDeltaTime, 0.0166667f), 0.001f, 0.05f);
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

            MockCarveRequestJob carveJob = new MockCarveRequestJob
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
            H8Memory.RegisterActiveJob(SystemID.GameplayTools, _pendingHandle);
            _frameScheduled = true;
        }

        public void PostFixedTick(float fixedDeltaTime)
        {
            TryFinalizePendingFrameNoWait();
            ApplyPendingDataVaultRebindIfIdle();
        }

        public void SlowTick()
        {
            if (!_abiValid)
                return;

#if UNITY_EDITOR
            TryConsumeEquipmentStatsCsv();
#endif
            if (!TryResolveTuning(out NativeArray<ToolKinematicsTuningDTO> tuning))
                return;

            WriteTuning(tuning);
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

            if (!DispatcherJobFence.TryComplete(ref _pendingHandle, forceComplete: true))
                return;

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
            for (int i = 0; i < _activeToolCapacity; i++)
            {
                ToolStateDTO state = buffers.States[i];
                if (state.ToolTypeHash == 0u)
                    state.ToolTypeHash = i == 0 ? ToolKinematicsHashes.LaserCutter : ToolKinematicsHashes.Welder;

                float3 controllerLocal = ResolveControllerLocal(i);
                quaternion controllerRotation = ResolveControllerRotation(i);
                float3 shoulderLocal = ResolveShoulderLocal(i);
                uint triggerFlags = mockTriggerHeld || !useMockInput ? ToolKinematicsMath.TriggerPressed : 0u;
                triggerFlags |= ResolveToolModeFlag(state.ToolTypeHash);

                state.AUP = cameraAup + ToolKinematicsMath.ToDouble3(controllerLocal);
                state._pad0 = 0u;
                state._pad1 = 0u;
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

                buffers.MockTriggerSignals[i] = new MockTriggerPullSignal
                {
                    ToolSlot = (uint)i,
                    ToolHash = state.ToolTypeHash,
                    Trigger01 = (triggerFlags & ToolKinematicsMath.TriggerPressed) != 0u ? 1f : 0f,
                    Frame = _frameIndex
                };
            }
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
                MockTriggerPullSignal trigger = buffers.MockTriggerSignals[i];
                if (trigger.Frame != 0u && trigger.Trigger01 > 0f)
                {
                    SignalBus<MockTriggerPullSignal>.TryPushTracked(in trigger, ref _signalPushDropCount);
                    PublishGlobalTriggerBridge(in trigger);
                }

                ToolHeatSignal heat = buffers.HeatSignals[i];
                if (heat.Frame != 0u)
                {
                    SignalBus<ToolHeatSignal>.TryPushTracked(in heat, ref _signalPushDropCount);
                    ToolScreenExportDTO screen = (uint)i < (uint)buffers.ScreenExports.Length ? buffers.ScreenExports[i] : default;
                    PublishGlobalToolStateBridge(in heat, in screen);
                    PublishGlobalToolAcousticBridge(in heat, in screen);
                }

                VfxSparkRequestSignal spark = buffers.SparkRequests[i];
                if (spark.Frame != 0u && spark.Intensity01 > 0f)
                    SignalBus<VfxSparkRequestSignal>.TryPushTracked(in spark, ref _signalPushDropCount);

                MockCarveRequestSignal carve = buffers.CarveRequests[i];
                if (carve.Frame != 0u && carve.MaterialHash != 0u)
                    SignalBus<MockCarveRequestSignal>.TryPushTracked(in carve, ref _signalPushDropCount);
            }
        }

        private static void EnsureSignalLanesReady()
        {
            SignalBus<MockTriggerPullSignal>.Configure(
                MockTriggerPullSignal.ExpectedCapacity,
                maxFrameSignals: MockTriggerPullSignal.MaxFrameSignals,
                lowTierFrameSignals: MockTriggerPullSignal.LowTierFrameSignals,
                laneHash: MockTriggerPullSignal.LaneHash);
            SignalBus<MockTriggerPullSignal>.EnsureInitialized();

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

            SignalBus<MockCarveRequestSignal>.Configure(
                MockCarveRequestSignal.ExpectedCapacity,
                maxFrameSignals: MockCarveRequestSignal.MaxFrameSignals,
                lowTierFrameSignals: MockCarveRequestSignal.LowTierFrameSignals,
                laneHash: MockCarveRequestSignal.LaneHash);
            SignalBus<MockCarveRequestSignal>.EnsureInitialized();
        }

        private static void PublishGlobalTriggerBridge(in MockTriggerPullSignal trigger)
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

            AutoResetEvent signal = _blackBoxDumpSignal;
            if (signal == null)
            {
                Volatile.Write(ref _blackBoxDumpState, DumpStateIdle);
                return false;
            }

            try
            {
                signal.Set();
                return true;
            }
            catch (ObjectDisposedException)
            {
                Volatile.Write(ref _blackBoxDumpState, DumpStateIdle);
                return false;
            }
        }

        private void EnsureBlackBoxDumpWorkerCold()
        {
            if (_blackBoxDumpSignal == null)
                _blackBoxDumpSignal = new AutoResetEvent(false); // COLD ALLOC: AutoResetEvent[1] - fault dump worker signal - owner: ToolKinematicsRuntime

            if (_blackBoxDumpThread != null)
            {
                if (_blackBoxDumpThread.IsAlive)
                    return;

                _blackBoxDumpThread = null;
            }

            Volatile.Write(ref _blackBoxDumpRun, 1);
            _blackBoxDumpThread = new Thread(BlackBoxDumpWorkerLoop)
            {
                IsBackground = true,
                Name = "13US_ToolKinematicsDump"
            }; // COLD ALLOC: Thread[1] - black-box dump export worker - owner: ToolKinematicsRuntime
            _blackBoxDumpThread.Start();
        }

        private void StopBlackBoxDumpWorker()
        {
            Volatile.Write(ref _blackBoxDumpRun, 0);
            AutoResetEvent signal = _blackBoxDumpSignal;
            if (signal != null)
            {
                try
                {
                    signal.Set();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            Thread thread = _blackBoxDumpThread;
            if (thread != null && thread.IsAlive)
            {
                thread.Join(BlackBoxDumpWorkerJoinMilliseconds);
                if (thread.IsAlive)
                    return;
            }

            DrainPendingBlackBoxDump();
            _blackBoxDumpThread = null;
            if (signal != null)
                signal.Dispose();
            _blackBoxDumpSignal = null;
            Volatile.Write(ref _blackBoxDumpEntryCount, 0);
            Volatile.Write(ref _blackBoxDumpState, DumpStateIdle);
        }

        private void BlackBoxDumpWorkerLoop()
        {
            while (Volatile.Read(ref _blackBoxDumpRun) != 0)
            {
                AutoResetEvent signal = _blackBoxDumpSignal;
                if (signal == null)
                    return;

                try
                {
                    signal.WaitOne(BlackBoxDumpWorkerPollMilliseconds);
                }
                catch (ObjectDisposedException)
                {
                    return;
                }

                DrainPendingBlackBoxDump();
            }

            DrainPendingBlackBoxDump();
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

        private bool TryWriteQueuedBlackBoxDump()
        {
            string dumpPath = _blackBoxDumpPath;
            if (string.IsNullOrEmpty(dumpPath))
                return false;

            string logDirectory = Path.GetDirectoryName(dumpPath);
            if (!string.IsNullOrEmpty(logDirectory))
                Directory.CreateDirectory(logDirectory);

            using FileStream stream = new FileStream(dumpPath, FileMode.Create, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
            using BinaryWriter writer = new BinaryWriter(stream);
            writer.Write(0x544B4242u);
            writer.Write(_blackBoxDumpFrameIndex);
            writer.Write(_blackBoxDumpToolCapacity);
            writer.Write(_blackBoxDumpTelemetryCursor);
            int max = math.min(Volatile.Read(ref _blackBoxDumpEntryCount), MaxBlackBoxDumpEntries);
            writer.Write(max);
            for (int i = 0; i < max; i++)
                WriteTelemetryEntry(writer, _blackBoxDumpEntries[i]);

            return true;
        }

        private static void WriteTelemetryEntry(BinaryWriter writer, in ToolKinematicsTelemetryEntry entry)
        {
            writer.Write(entry.FrameIndex);
            writer.Write(entry.ToolHash);
            writer.Write(entry.ToolHeatLevel);
            writer.Write(entry.EnergyRemaining);
            writer.Write(entry.HitDistance);
            writer.Write(entry.RaymarchStepCount);
            writer.Write(entry.IkComputeTimeMicroseconds);
            writer.Write(entry.Flags);
            WriteFloat3(writer, entry.ToolLocalPosition);
            WriteFloat3(writer, entry.HitPoint);
            writer.Write(entry.MaterialHash);
            writer.Write(entry._pad0);
        }

        private static void WriteFloat3(BinaryWriter writer, float3 value)
        {
            writer.Write(value.x);
            writer.Write(value.y);
            writer.Write(value.z);
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
                TryResolveVaultView(vault, ref _mockTriggerSignalsHandle, BufferID.ToolKinematicsMockTriggerSignals, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.MockTriggerSignals) &&
                TryResolveVaultView(vault, ref _carveRequestsHandle, BufferID.ToolKinematicsMockCarveRequests, count, NativeArrayOptions.ClearMemory, allowCreate, out buffers.CarveRequests) &&
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
        }

        private bool TryBootstrapRuntime()
        {
            if (!TryResolveAllBuffers(true, out ToolKinematicsBufferSet buffers))
                return false;

            WriteTuning(buffers.Tuning);
            SeedEmergencyMockTools(buffers.States, buffers.RecoilStates);
            EnsureSignalLanesReady();
            EnsureBlackBoxDumpWorkerCold();
#if UNITY_EDITOR
            StartCsvWatcher();
#endif
            TryRegisterFixed();
            TryRegisterPostFixed();
            TryRegisterSlow();
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
            if (isActiveAndEnabled)
                TryBootstrapRuntime();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.DataVault:
                    QueueDataVaultRebind(currentService is IDataVault currentVault ? currentVault : null);
                    break;
                case GlobalRegistryServiceSlot.Dispatcher:
                    _fixedRegistered = false;
                    _postFixedRegistered = false;
                    _slowRegistered = false;
                    if (currentService != null && isActiveAndEnabled)
                    {
                        TryRegisterFixed();
                        TryRegisterPostFixed();
                        TryRegisterSlow();
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
            if (vault == null || requiredLength <= 0)
                return false;

            if (IsOwnedVaultHandle(in handle, bufferId) &&
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

            if (!vault.TryResolveHandle(in acquired, out buffer) ||
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
            uint csvFaultFlag = Volatile.Read(ref _csvThreadFaultCode) != 0 ? (uint)ToolKinematicsFlags.CsvIoFault : 0u;
#else
            const uint csvFaultFlag = 0u;
#endif
            ToolKinematicsTuningDTO current = tuning[0];
            bool existingValid = current.LaserRange > 0.0001f && current.MaxHeat > 0.0001f;
            if (Volatile.Read(ref _tuningDirty) == 0 && existingValid)
            {
                current.Flags = (current.Flags & ~(uint)ToolKinematicsFlags.CsvIoFault) | csvFaultFlag;
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

        private static void SeedEmergencyMockTools(NativeArray<ToolStateDTO> states, NativeArray<ToolRecoilStateDTO> recoilStates)
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
                    state.ToolTypeHash = ResolveMockToolHash(i);
                    state.EnergyRemaining = 1f;
                    state._pad0 = 0u;
                    state._pad1 = 0u;
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

        private static uint ResolveMockToolHash(int index)
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
            if (!useMockInput &&
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
            if (!useMockInput &&
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
            if (!useMockInput &&
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
        private void StartCsvWatcher()
        {
            if (string.IsNullOrEmpty(_equipmentStatsPath))
                return;

            if (_csvThread != null)
            {
                if (_csvThread.IsAlive)
                    return;

                _csvThread = null;
            }

            Volatile.Write(ref _csvThreadRun, 1);
            _csvThread = new Thread(CsvWatcherLoop)
            {
                IsBackground = true,
                Name = "SHINOBU_22_ToolCsvWatcher"
            };
            _csvThread.Start();
        }

        private void StopCsvWatcher()
        {
            Volatile.Write(ref _csvThreadRun, 0);
            Thread thread = _csvThread;
            if (thread != null && thread.IsAlive)
            {
                thread.Join(250);
                if (thread.IsAlive)
                    return;
            }

            _csvThread = null;
        }

        private void CsvWatcherLoop()
        {
            while (Volatile.Read(ref _csvThreadRun) != 0)
            {
                TryReadEquipmentStatsCsvOnWorker();
                Thread.Sleep(250);
            }
        }

        private void TryReadEquipmentStatsCsvOnWorker()
        {
            try
            {
                string path = _equipmentStatsPath;
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    return;

                long stamp = File.GetLastWriteTimeUtc(path).Ticks;
                if (stamp == _equipmentStatsStampUtcTicks)
                    return;

                int bytesRead;
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    bytesRead = stream.Read(_csvIoBuffer, 0, _csvIoBuffer.Length);
                }

                if (bytesRead <= 0)
                    return;

                lock (_csvGate)
                {
                    for (int i = 0; i < bytesRead; i++)
                        _csvPendingBuffer[i] = _csvIoBuffer[i];

                    _csvPendingBytes = bytesRead;
                    _equipmentStatsStampUtcTicks = stamp;
                    _csvPendingSequence++;
                }
            }
            catch (IOException)
            {
                Interlocked.Exchange(ref _csvThreadFaultCode, 1);
            }
            catch (UnauthorizedAccessException)
            {
                Interlocked.Exchange(ref _csvThreadFaultCode, 2);
            }
            catch (Exception)
            {
                Interlocked.Exchange(ref _csvThreadFaultCode, 3);
            }
        }

        private bool TryConsumeEquipmentStatsCsv()
        {
            int pendingSequence = Volatile.Read(ref _csvPendingSequence);
            if (pendingSequence == _csvConsumedSequence)
                return false;

            int bytesRead;
            lock (_csvGate)
            {
                pendingSequence = _csvPendingSequence;
                if (pendingSequence == _csvConsumedSequence)
                    return false;

                bytesRead = math.clamp(_csvPendingBytes, 0, CsvBufferBytes);
                for (int i = 0; i < bytesRead; i++)
                    _csvConsumeBuffer[i] = _csvPendingBuffer[i];

                _csvConsumedSequence = pendingSequence;
            }

            if (bytesRead <= 0)
                return false;

            ParseEquipmentStatsCsv(_csvConsumeBuffer, bytesRead);
            return true;
        }

        private void ParseEquipmentStatsCsv(byte[] bytes, int length)
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

        private void ParseEquipmentStatsLine(byte[] bytes, int start, int end)
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

        private static uint HashCsvKey(byte[] bytes, int start, int end)
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

        private static bool TryParseFloatAscii(byte[] bytes, int start, int end, out float value)
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

        private void TryRegisterFixed()
        {
            if (_fixedRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _fixedRegistered = GlobalRegistry.TryRegisterFixedTickable(this, PriorityLayer.Player);
        }

        private void TryRegisterPostFixed()
        {
            if (_postFixedRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _postFixedRegistered = GlobalRegistry.TryRegisterPostFixedTickable(this, PriorityLayer.Player);
        }

        private void TryRegisterSlow()
        {
            if (_slowRegistered || !Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;

            _slowRegistered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
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

        private void TryUnregisterSlow()
        {
            if (!_slowRegistered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);
            _slowRegistered = false;
        }

        private static bool ValidateAbiLayout()
        {
            bool valid =
                UnsafeUtility.SizeOf<ToolStateDTO>() == 56 &&
                UnsafeUtility.SizeOf<ToolHitResultDTO>() == 32 &&
                UnsafeUtility.SizeOf<ToolScreenExportDTO>() == 16 &&
                UnsafeUtility.SizeOf<ToolKinematicsTuningDTO>() == 48 &&
                UnsafeUtility.SizeOf<ToolKinematicsFrameInputDTO>() == 96 &&
                UnsafeUtility.SizeOf<ToolIkOutputDTO>() == 64 &&
                UnsafeUtility.SizeOf<ToolRecoilStateDTO>() == 64 &&
                UnsafeUtility.SizeOf<ToolBeamVertexDTO>() == 32 &&
                UnsafeUtility.SizeOf<ToolPoseOutputDTO>() == 96 &&
                UnsafeUtility.SizeOf<ToolKinematicsTelemetryEntry>() == 64 &&
                UnsafeUtility.SizeOf<MockSdfSample>() == 8;

            if (!valid)
                Hecton8.Core.H8Debug.LogError("[ToolKinematicsRuntime] ARM64 DTO layout mismatch. Runtime disabled.");

            return valid;
        }

#if UNITY_EDITOR
        private static string ResolveEquipmentStatsPath()
        {
            return Path.Combine(ResolveProjectRootPath(), EquipmentStatsFileName);
        }
#endif

        private static string ResolveBlackBoxDumpPath()
        {
            return Path.Combine(ResolveProjectRootPath(), "Docs", "AgentLogs", BlackBoxDumpFileName);
        }

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
            ReleaseVaultHandle(vault, ref _mockTriggerSignalsHandle, BufferID.ToolKinematicsMockTriggerSignals);
            ReleaseVaultHandle(vault, ref _carveRequestsHandle, BufferID.ToolKinematicsMockCarveRequests);
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
            _mockTriggerSignalsHandle = default;
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
            public NativeArray<MockTriggerPullSignal> MockTriggerSignals;
            public NativeArray<MockCarveRequestSignal> CarveRequests;
            public NativeArray<ToolHeatSignal> HeatSignals;
            public NativeArray<VfxSparkRequestSignal> SparkRequests;
            public NativeArray<ToolBeamVertexDTO> BeamVertices;
            public NativeArray<int> BeamVertexCounts;
            public NativeArray<ToolPoseOutputDTO> PoseOutputs;
        }
    }
}
