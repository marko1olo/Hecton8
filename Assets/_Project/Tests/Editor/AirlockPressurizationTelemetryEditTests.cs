using System.IO;
using Hecton8.Core.Contracts;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Hecton8.Gameplay.AirlockPressurization;
using Hecton8.World;
using NUnit.Framework;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class AirlockPressurizationTelemetryEditTests
    {
        private const string BaseAirlockPath = "Assets/_Project/Scripts/Gameplay/BaseAirlock.cs";
        private const string BaseModulePath = "Assets/_Project/Scripts/BaseModule.cs";
        private const string ConstructionManagerPath = "Assets/_Project/Scripts/ConstructionManager.cs";
        private const string ContractsPath = "Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationContracts.cs";
        private const string VaultPath = "Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationVault.cs";
        private const string RuntimePath = "Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntime.cs";
        private const string RuntimeOwnerPath = "Assets/_Project/Scripts/Gameplay/AirlockPressurization/AirlockPressurizationRuntimeOwner.cs";
        private const string EditorPath = "Assets/_Project/Scripts/Gameplay/AirlockPressurization/Editor/AirlockPressurizationEditor.cs";
        private const string SignalPayloadsPath = "Assets/_Project/Scripts/Core/Signals/GlobalSignalPayloads.DomainRemainder.cs";
        private const string SignalBusRuntimePath = "Assets/_Project/Scripts/Core/Signals/SignalBusRuntime.cs";
        private const string GlobalSignalsStatePath = "Assets/_Project/Scripts/Core/Signals/GlobalSignals.State.cs";
        private const string GlobalSignalsRuntimePath = "Assets/_Project/Scripts/Core/Signals/GlobalSignals.RuntimeLifecycle.cs";
        private const string FloraInteractionPath = "Assets/_Project/Scripts/World/FloraInteractionManager.cs";
        private const string BiolumManagerPath = "Assets/_Project/Scripts/World/Biolum/HectonBiolumManager.cs";
        private const string AcousticEchoLocationRuntimePath = "Assets/_Project/Scripts/AI/Sensory/AcousticEchoLocationRuntime.cs";

        [Test]
        public void EvaluateJob_GatesOutputSignalsOnValidDoorSource()
        {
            NativeArray<AirlockStateDTO> airlocks = new NativeArray<AirlockStateDTO>(1, Allocator.Temp);
            NativeArray<AirlockTuningDTO> tunings = new NativeArray<AirlockTuningDTO>(1, Allocator.Temp);
            NativeArray<AirlockDoorPoseDTO> doorPoses = new NativeArray<AirlockDoorPoseDTO>(1, Allocator.Temp);
            NativeArray<AirlockEvaluationResultDTO> results = new NativeArray<AirlockEvaluationResultDTO>(1, Allocator.Temp);
            NativeArray<BulkheadContainmentIntentDTO> bulkheadIntents = new NativeArray<BulkheadContainmentIntentDTO>(1, Allocator.Temp);
            NativeArray<BubbleSpawnSignal> vfxSignals = new NativeArray<BubbleSpawnSignal>(1, Allocator.Temp);
            NativeArray<MovementAcousticSignal> acousticSignals = new NativeArray<MovementAcousticSignal>(1, Allocator.Temp);
            NativeArray<AirlockDebugGizmoDTO> debugGizmos = new NativeArray<AirlockDebugGizmoDTO>(1, Allocator.Temp);

            try
            {
                AirlockStateDTO initialState = new AirlockStateDTO
                {
                    InnerRoomHashID = 0xA110u,
                    OuterRoomHashID = 0x0CE0u,
                    CurrentWaterVolumeLiters = 600f,
                    CurrentPressureATM = 8f,
                    CycleStateFlags = AirlockCycleFlags.Pumping,
                    CycleTimer = 1f
                };
                AirlockTuningDTO tuning = new AirlockTuningDTO
                {
                    PumpEvacuationSpeedLps = 260f,
                    MaxWaterVolumeLiters = 1000f,
                    ChamberVolumeLiters = 1600f,
                    EqualizationCurveExponent = AirlockPressurizationConstants.DefaultEqualizationCurveExponent,
                    PowerDrawWatts = AirlockPressurizationConstants.DefaultPowerDrawWatts,
                    AvailablePower01 = 1f,
                    ExternalDepthMeters = 70f,
                    BreachAreaM2 = AirlockPressurizationConstants.DefaultBreachAreaM2,
                    DischargeCoefficient = AirlockPressurizationConstants.DefaultDischargeCoefficient,
                    GlobalQualityWeight = 1f,
                    PressureEqualizedAtm = AirlockPressurizationConstants.PressureEqualizedAtm,
                    WaterEqualizedLiters = AirlockPressurizationConstants.WaterEqualizedLiters,
                    ExternalPressureAtm = 8f,
                    RoomPressureAtm = 1f,
                    Frame = 10u
                };
                AirlockDoorPoseDTO validDoor = new AirlockDoorPoseDTO
                {
                    DoorAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(10d, 20d, 30d)),
                    DoorNormal = new float3(0f, 0f, 1f),
                    WidthMeters = 2.6f,
                    HeightMeters = 3.2f,
                    DoorHashID = 0u,
                    EdgeHashID = 0xE001u,
                    Flags = AirlockDoorPoseFlags.Valid,
                    ExternalDepthMeters = 70f,
                    HeadMeters = 68f,
                    Frame = 10u
                };

                airlocks[0] = initialState;
                tunings[0] = tuning;
                doorPoses[0] = validDoor;
                RunEvaluateJob(
                    airlocks,
                    tunings,
                    doorPoses,
                    results,
                    bulkheadIntents,
                    vfxSignals,
                    acousticSignals,
                    debugGizmos);

                Assert.AreNotEqual(0u, vfxSignals[0].Frame);
                Assert.AreEqual(
                    AirlockPressurizationConstants.HeavyPumpHash ^ validDoor.EdgeHashID,
                    acousticSignals[0].SourceId);
                Assert.AreNotEqual(0u, bulkheadIntents[0].Flags & BulkheadContainmentIntentFlags.Valid);

                airlocks[0] = initialState;
                doorPoses[0] = default;
                results[0] = default;
                bulkheadIntents[0] = default;
                vfxSignals[0] = default;
                acousticSignals[0] = default;
                RunEvaluateJob(
                    airlocks,
                    tunings,
                    doorPoses,
                    results,
                    bulkheadIntents,
                    vfxSignals,
                    acousticSignals,
                    debugGizmos);

                Assert.AreEqual(0u, vfxSignals[0].Frame);
                Assert.AreEqual(0u, acousticSignals[0].SourceId);
                Assert.AreEqual(0u, bulkheadIntents[0].Flags & BulkheadContainmentIntentFlags.Valid);
            }
            finally
            {
                if (airlocks.IsCreated)
                    airlocks.Dispose();
                if (tunings.IsCreated)
                    tunings.Dispose();
                if (doorPoses.IsCreated)
                    doorPoses.Dispose();
                if (results.IsCreated)
                    results.Dispose();
                if (bulkheadIntents.IsCreated)
                    bulkheadIntents.Dispose();
                if (vfxSignals.IsCreated)
                    vfxSignals.Dispose();
                if (acousticSignals.IsCreated)
                    acousticSignals.Dispose();
                if (debugGizmos.IsCreated)
                    debugGizmos.Dispose();
            }
        }

        [Test]
        public void TelemetryJob_CountsActualOutputSignalRows()
        {
            NativeArray<AirlockStateDTO> airlocks = new NativeArray<AirlockStateDTO>(2, Allocator.Temp);
            NativeArray<AirlockEvaluationResultDTO> results = new NativeArray<AirlockEvaluationResultDTO>(2, Allocator.Temp);
            NativeArray<BubbleSpawnSignal> vfxSignals = new NativeArray<BubbleSpawnSignal>(2, Allocator.Temp);
            NativeArray<MovementAcousticSignal> acousticSignals = new NativeArray<MovementAcousticSignal>(2, Allocator.Temp);
            NativeArray<AirlockTelemetryEntry> telemetry = new NativeArray<AirlockTelemetryEntry>(1, Allocator.Temp);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.Temp);
            NativeArray<int> dumpRequested = new NativeArray<int>(1, Allocator.Temp);

            try
            {
                airlocks[0] = new AirlockStateDTO
                {
                    CycleStateFlags = AirlockCycleFlags.Pumping,
                    CurrentPressureATM = 1f
                };
                airlocks[1] = new AirlockStateDTO
                {
                    CycleStateFlags = AirlockCycleFlags.Pumping | AirlockCycleFlags.AcousticPump,
                    CurrentPressureATM = 1f
                };
                results[0] = new AirlockEvaluationResultDTO { VfxIntensity01 = 1f, StateHash = 1u };
                results[1] = new AirlockEvaluationResultDTO { VfxIntensity01 = 1f, StateHash = 2u };
                vfxSignals[1] = new BubbleSpawnSignal { Frame = 42u };
                acousticSignals[0] = new MovementAcousticSignal { SourceId = 7u };

                new RecordAirlockTelemetryJob
                {
                    Airlocks = airlocks,
                    Results = results,
                    VfxSignals = vfxSignals,
                    AcousticSignals = acousticSignals,
                    Telemetry = telemetry,
                    TelemetryCursor = cursor,
                    DumpRequested = dumpRequested,
                    Frame = 99u,
                    TickIntervalSeconds = 0.1f
                }.Execute();

                Assert.AreEqual(1u, telemetry[0].VfxSignals);
                Assert.AreEqual(1u, telemetry[0].AcousticSignals);
                Assert.AreEqual(1, cursor[0]);
            }
            finally
            {
                if (airlocks.IsCreated)
                    airlocks.Dispose();
                if (results.IsCreated)
                    results.Dispose();
                if (vfxSignals.IsCreated)
                    vfxSignals.Dispose();
                if (acousticSignals.IsCreated)
                    acousticSignals.Dispose();
                if (telemetry.IsCreated)
                    telemetry.Dispose();
                if (cursor.IsCreated)
                    cursor.Dispose();
                if (dumpRequested.IsCreated)
                    dumpRequested.Dispose();
            }
        }

        private static void RunEvaluateJob(
            NativeArray<AirlockStateDTO> airlocks,
            NativeArray<AirlockTuningDTO> tunings,
            NativeArray<AirlockDoorPoseDTO> doorPoses,
            NativeArray<AirlockEvaluationResultDTO> results,
            NativeArray<BulkheadContainmentIntentDTO> bulkheadIntents,
            NativeArray<BubbleSpawnSignal> vfxSignals,
            NativeArray<MovementAcousticSignal> acousticSignals,
            NativeArray<AirlockDebugGizmoDTO> debugGizmos)
        {
            new EvaluateAirlockCyclesJob
            {
                Airlocks = airlocks,
                Tunings = tunings,
                DoorPoses = doorPoses,
                Results = results,
                BulkheadIntents = bulkheadIntents,
                VfxSignals = vfxSignals,
                AcousticSignals = acousticSignals,
                DebugGizmos = debugGizmos,
                DeltaTime = 10f,
                GlobalQualityWeight = 1f,
                Frame = 10u
            }.Execute(0);
        }

        [Test]
        public void BulkheadIntentFlushCounters_MergeIntoAirlockTelemetry()
        {
            string contracts = File.ReadAllText(ContractsPath);
            string runtime = File.ReadAllText(RuntimePath);

            StringAssert.Contains("public const uint BulkheadIntentRetry = 1u << 13;", contracts);
            StringAssert.Contains("public const uint BulkheadIntentInvalid = 1u << 14;", contracts);
            StringAssert.Contains("public static uint PushBulkheadIntents", contracts);
            StringAssert.Contains("return PackFlushCounters(publishedCount, retryCount, invalidCount);", contracts);
            StringAssert.Contains("public static void MergeFlushCountersIntoTelemetry", contracts);
            StringAssert.Contains("entry.Reserved0 = packedCounters;", contracts);
            StringAssert.Contains("entry.Flags |= AirlockCycleFlags.BulkheadIntentRetry;", contracts);
            StringAssert.Contains("entry.Flags |= AirlockCycleFlags.BulkheadIntentInvalid;", contracts);
            StringAssert.Contains("uint bulkheadIntentFlushCounters = AirlockPressurizationIntentFlush.PushBulkheadIntents", runtime);
            StringAssert.Contains("AirlockPressurizationIntentFlush.MergeFlushCountersIntoTelemetry", runtime);
        }

        [Test]
        public void OutputSignalFlushCounters_MergeIntoAirlockTelemetry()
        {
            NativeArray<AirlockTelemetryEntry> telemetry = new NativeArray<AirlockTelemetryEntry>(1, Allocator.Temp);
            NativeArray<int> cursor = new NativeArray<int>(1, Allocator.Temp);

            try
            {
                telemetry[0] = new AirlockTelemetryEntry
                {
                    VfxSignals = 5u,
                    AcousticSignals = 2u
                };
                cursor[0] = 1;

                uint packed = 3u | (1u << 8) | (2u << 16) | (1u << 24);
                AirlockPressurizationSignalFlush.MergeSignalFlushCountersIntoTelemetry(telemetry, cursor, packed);

                AirlockTelemetryEntry entry = telemetry[0];
                Assert.AreEqual(5u, AirlockPressurizationSignalFlush.UnpackTelemetrySignalExpectedCount(entry.VfxSignals));
                Assert.AreEqual(3u, AirlockPressurizationSignalFlush.UnpackTelemetrySignalPublishedCount(entry.VfxSignals));
                Assert.AreEqual(1u, AirlockPressurizationSignalFlush.UnpackTelemetrySignalDroppedCount(entry.VfxSignals));
                Assert.AreEqual(2u, AirlockPressurizationSignalFlush.UnpackTelemetrySignalExpectedCount(entry.AcousticSignals));
                Assert.AreEqual(2u, AirlockPressurizationSignalFlush.UnpackTelemetrySignalPublishedCount(entry.AcousticSignals));
                Assert.AreEqual(1u, AirlockPressurizationSignalFlush.UnpackTelemetrySignalDroppedCount(entry.AcousticSignals));
                Assert.AreNotEqual(0u, entry.Flags & AirlockCycleFlags.OutputSignalDropped);
            }
            finally
            {
                if (telemetry.IsCreated)
                    telemetry.Dispose();
                if (cursor.IsCreated)
                    cursor.Dispose();
            }
        }

        [Test]
        public void OutputSignalFlushCounters_AreVisibleInRuntimeAndEditor()
        {
            string contracts = File.ReadAllText(ContractsPath);
            string runtime = File.ReadAllText(RuntimePath);
            string editor = File.ReadAllText(EditorPath);

            StringAssert.Contains("public const uint OutputSignalDropped = 1u << 15;", contracts);
            StringAssert.Contains("public static uint PushFrameSignals", contracts);
            StringAssert.Contains("return PackSignalFlushCounters", contracts);
            StringAssert.Contains("public static void MergeSignalFlushCountersIntoTelemetry", contracts);
            StringAssert.Contains("entry.Flags |= AirlockCycleFlags.OutputSignalDropped;", contracts);
            StringAssert.Contains("public static uint UnpackTelemetrySignalExpectedCount", contracts);
            StringAssert.Contains("uint signalFlushCounters = AirlockPressurizationSignalFlush.PushFrameSignals", runtime);
            StringAssert.Contains("AirlockPressurizationSignalFlush.MergeSignalFlushCountersIntoTelemetry", runtime);
            StringAssert.Contains(".Append(\" | VFX: \")", editor);
            StringAssert.Contains("AirlockPressurizationSignalFlush.UnpackTelemetrySignalDroppedCount", editor);
        }

        [Test]
        public void BulkheadIntentFlushCounterUnpack_DecodesPackedTelemetryLanes()
        {
            uint packed = 3u | (2u << 8) | (1u << 16);

            Assert.AreEqual(3u, AirlockPressurizationIntentFlush.UnpackFlushPublishedCount(packed));
            Assert.AreEqual(2u, AirlockPressurizationIntentFlush.UnpackFlushRetryCount(packed));
            Assert.AreEqual(1u, AirlockPressurizationIntentFlush.UnpackFlushInvalidCount(packed));
        }

        [Test]
        public void BulkheadIntentFlushCounters_AreReadableByEditorDebugUi()
        {
            string contracts = File.ReadAllText(ContractsPath);
            string vault = File.ReadAllText(VaultPath);
            string editor = File.ReadAllText(EditorPath);

            StringAssert.Contains("public static bool TryReadTelemetryCursor", vault);
            StringAssert.Contains("public static uint UnpackFlushPublishedCount", contracts);
            StringAssert.Contains("public static uint UnpackFlushRetryCount", contracts);
            StringAssert.Contains("public static uint UnpackFlushInvalidCount", contracts);
            StringAssert.Contains("AirlockPressurizationVault.TryReadTelemetryCursor", editor);
            StringAssert.Contains("AirlockPressurizationIntentFlush.UnpackFlushPublishedCount", editor);
            StringAssert.Contains("AirlockPressurizationIntentFlush.UnpackFlushRetryCount", editor);
            StringAssert.Contains("AirlockPressurizationIntentFlush.UnpackFlushInvalidCount", editor);
            StringAssert.Contains(".Append(\" | Intents: \")", editor);
        }

        [Test]
        public void BaseAirlockSnapshotBridge_WritesAndClearsMatchingVaultSlot()
        {
            using GlobalDataVault vault = new GlobalDataVault();
            AirlockPressurizationAuthoringSnapshot snapshot = new AirlockPressurizationAuthoringSnapshot
            {
                DoorAup = AbsoluteUniversePosition.FromAbsolutePosition(new double3(1d, 2d, 3d)),
                DoorNormal = new float3(0f, 0f, 1f),
                CurrentWaterVolumeLiters = 25f,
                CurrentPressureAtm = 1.25f,
                CycleTimer = 2f,
                MaxWaterVolumeLiters = 100f,
                ChamberVolumeLiters = 500f,
                PumpEvacuationSpeedLps = 260f,
                EqualizationCurveExponent = 1.75f,
                PowerDrawWatts = 1400f,
                AvailablePower01 = 1f,
                ExternalDepthMeters = 10f,
                BreachAreaM2 = 0.18f,
                DischargeCoefficient = 0.62f,
                GlobalQualityWeight = 1f,
                PressureEqualizedAtm = 0.03f,
                WaterEqualizedLiters = 5f,
                ExternalPressureAtm = 2f,
                RoomPressureAtm = 1f,
                WidthMeters = 2.6f,
                HeightMeters = 3.2f,
                HeadMeters = 10f,
                InnerRoomHashID = 0xA110u,
                OuterRoomHashID = 0x0CE0u,
                DoorHashID = 0xD001u,
                EdgeHashID = 0xE001u,
                CycleStateFlags = AirlockCycleFlags.Equalizing,
                Frame = 42u
            };

            Assert.IsTrue(AirlockPressurizationVault.TryWriteAirlockSnapshot(vault, in snapshot, out int slotIndex));
            Assert.That(slotIndex, Is.GreaterThanOrEqualTo(0));
            Assert.IsTrue(AirlockPressurizationVault.TryReadAirlocks(vault, out NativeArray<AirlockStateDTO>.ReadOnly airlocks));
            Assert.IsTrue(AirlockPressurizationVault.TryReadTuning(vault, out NativeArray<AirlockTuningDTO>.ReadOnly tunings));
            Assert.AreEqual(snapshot.InnerRoomHashID, airlocks[slotIndex].InnerRoomHashID);
            Assert.AreEqual(snapshot.CurrentWaterVolumeLiters, airlocks[slotIndex].CurrentWaterVolumeLiters);
            Assert.AreEqual(snapshot.Frame, tunings[slotIndex].Frame);

            Assert.IsTrue(AirlockPressurizationVault.TryClearAirlockSnapshot(vault, snapshot.EdgeHashID));
            Assert.AreEqual(0u, airlocks[slotIndex].InnerRoomHashID);
            Assert.AreEqual(0u, tunings[slotIndex].Frame);
        }

        [Test]
        public void BaseAirlockSnapshotBridge_IsWiredThroughProducerLifecycle()
        {
            string contracts = File.ReadAllText(ContractsPath);
            string vault = File.ReadAllText(VaultPath);
            string baseAirlock = File.ReadAllText(BaseAirlockPath);

            StringAssert.Contains("public struct AirlockPressurizationAuthoringSnapshot", contracts);
            StringAssert.Contains("public static bool TryWriteAirlockSnapshot", vault);
            StringAssert.Contains("public static bool TryClearAirlockSnapshot", vault);
            StringAssert.Contains("vault.ActiveBurstLockMask != 0u", vault);
            StringAssert.Contains("private static int ResolveSnapshotSlot", vault);
            StringAssert.Contains("private static int FindExactSnapshotSlot", vault);
            StringAssert.Contains("AirlockPressurizationVault.TryWriteAirlockSnapshot", baseAirlock);
            StringAssert.Contains("AirlockPressurizationVault.TryClearAirlockSnapshot", baseAirlock);
            StringAssert.Contains("PublishPressurizationSnapshot();", baseAirlock);
            StringAssert.Contains("_pressurizationPublishPending = true;", baseAirlock);
            StringAssert.Contains("private void RequestRuntimeBridgeRepublish(bool tryImmediate)", baseAirlock);
            StringAssert.Contains("RequestRuntimeBridgeRepublish(currentService != null);", baseAirlock);
            StringAssert.Contains("PublishBulkheadContainmentState(_emergencyLockedDown);", baseAirlock);
            StringAssert.Contains("private const int PressurizationBridgeWarningCooldownFrames = 90;", baseAirlock);
            StringAssert.Contains("private const uint PressurizationSnapshotPoseInvalidWarningHash = 0x41505350u;", baseAirlock);
            StringAssert.Contains("private const uint PressurizationSnapshotWriteFailedWarningHash = 0x41505357u;", baseAirlock);
            StringAssert.Contains("private const uint PressurizationSnapshotClearFailedWarningHash = 0x41505343u;", baseAirlock);
            StringAssert.Contains("private void MarkPressurizationSnapshotStale(uint warningHash, uint edgeHash)", baseAirlock);
            StringAssert.Contains("MarkPressurizationSnapshotStale(PressurizationSnapshotPoseInvalidWarningHash, edgeHash);", baseAirlock);
            StringAssert.Contains("TryClearPressurizationSnapshot();", baseAirlock);
            StringAssert.Contains("private void PublishPressurizationBridgeWarning(uint warningHash, uint edgeHash)", baseAirlock);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning", baseAirlock);
            StringAssert.Contains("AirlockPressurizationConstants.AgentHash", baseAirlock);
            StringAssert.Contains("PublishPressurizationBridgeWarning(PressurizationSnapshotWriteFailedWarningHash, edgeHash);", baseAirlock);
            StringAssert.Contains("PublishPressurizationBridgeWarning(PressurizationSnapshotClearFailedWarningHash, edgeHash);", baseAirlock);
        }

        [Test]
        public void RuntimeOwner_SchedulesAndFlushesAirlockPressurizationRoute()
        {
            string contracts = File.ReadAllText(ContractsPath);
            string runtimeOwner = File.ReadAllText(RuntimeOwnerPath);

            StringAssert.Contains("public const uint SimulationHash", contracts);
            StringAssert.Contains("public const uint PostSimulationHash", contracts);
            StringAssert.Contains("AirlockPressurizationRuntimeOwner : MonoBehaviour, IGlobalRegistryHotSwapListener", runtimeOwner);
            StringAssert.Contains("GlobalRegistry.TryRegisterDispatcherSystem(_simulationPhase)", runtimeOwner);
            StringAssert.Contains("GlobalRegistry.TryRegisterDispatcherSystem(_postSimulationPhase)", runtimeOwner);
            StringAssert.Contains("AirlockPressurizationVault.AcquireHandles", runtimeOwner);
            StringAssert.Contains("AirlockPressurizationVault.ResolveViews", runtimeOwner);
            StringAssert.Contains("AirlockPressurizationVault.AdvanceCadence", runtimeOwner);
            StringAssert.Contains("AirlockPressurizationVault.ScheduleSimulation", runtimeOwner);
            StringAssert.Contains("AirlockPressurizationVault.FlushCompletedOutputs", runtimeOwner);
            StringAssert.Contains("dispatcherCompletionConfirmed: true", runtimeOwner);
            StringAssert.DoesNotContain(".Complete(", runtimeOwner);
        }

        [Test]
        public void RuntimeOwner_EnsuresSignalLanesBeforeFlush()
        {
            string contracts = File.ReadAllText(ContractsPath);
            string runtimeOwner = File.ReadAllText(RuntimeOwnerPath);
            string signalPayloads = File.ReadAllText(SignalPayloadsPath);
            string signalBusRuntime = File.ReadAllText(SignalBusRuntimePath);
            string globalSignalsState = File.ReadAllText(GlobalSignalsStatePath);
            string globalSignals = File.ReadAllText(GlobalSignalsRuntimePath);

            StringAssert.Contains("SignalBus<BubbleSpawnSignal>.TryPushTracked", contracts);
            StringAssert.Contains("SignalBus<MovementAcousticSignal>.TryPushTracked", contracts);
            StringAssert.Contains("private static void EnsureSignalLanes()", runtimeOwner);
            StringAssert.Contains("EnsureSignalLanes();", runtimeOwner);
            StringAssert.Contains("SignalBus<MovementAcousticSignal>.EnsureInitialized();", runtimeOwner);
            StringAssert.Contains("SignalBus<BubbleSpawnSignal>.EnsureInitialized();", runtimeOwner);
            StringAssert.Contains("public const int ExpectedCapacity = 128;", signalPayloads);
            StringAssert.Contains("public const uint LaneHash = 1747418347u;", signalPayloads);
            StringAssert.Contains("if (type == typeof(MovementAcousticSignal))", signalBusRuntime);
            StringAssert.Contains("expectedCapacity = MovementAcousticSignal.ExpectedCapacity;", signalBusRuntime);
            StringAssert.Contains("laneHash = MovementAcousticSignal.LaneHash;", signalBusRuntime);
            StringAssert.Contains("MovementAcousticSignalCapacity = MovementAcousticSignal.ExpectedCapacity", globalSignalsState);
            StringAssert.Contains("laneHash: MovementAcousticSignal.LaneHash", globalSignals);
        }

        [Test]
        public void BubbleSpawnSignal_ReachesProceduralWakeConsumer()
        {
            string contracts = File.ReadAllText(ContractsPath);
            string flora = File.ReadAllText(FloraInteractionPath);
            string globalSignalsState = File.ReadAllText(GlobalSignalsStatePath);
            string globalSignals = File.ReadAllText(GlobalSignalsRuntimePath);

            StringAssert.Contains("NativeArray<BubbleSpawnSignal> vfxSignals", contracts);
            StringAssert.Contains("SignalBus<BubbleSpawnSignal>.TryPushTracked", contracts);
            StringAssert.Contains("BubbleSpawnSignalCapacity = BubbleSpawnSignal.ExpectedCapacity", globalSignalsState);
            StringAssert.Contains("SignalBus<BubbleSpawnSignal>.Configure", globalSignals);
            StringAssert.Contains("laneHash: BubbleSpawnSignal.LaneHash", globalSignals);
            StringAssert.Contains("SignalBus<BubbleSpawnSignal>.EnsureInitialized();", globalSignals);
            StringAssert.Contains("ValidateSignalSize<BubbleSpawnSignal>(128);", globalSignals);
            StringAssert.Contains("private const int MaxBubbleWakeSignalsPerFrame = 32;", flora);
            StringAssert.Contains("private const byte WakeSourceBubble = 4;", flora);
            StringAssert.Contains("private const uint WakeBlackBoxSignalOverflowFlag = 1u << 4;", flora);
            StringAssert.Contains("private const uint WakeBlackBoxBubbleRejectedFlag = 1u << 5;", flora);
            StringAssert.Contains("private uint _pendingWakeTelemetryFlags;", flora);
            StringAssert.Contains("DrainBubbleSpawnSignals();", flora);
            StringAssert.Contains("ReadOnlySpan<BubbleSpawnSignal> signals = SignalBus<BubbleSpawnSignal>.GetFrameSnapshot();", flora);
            StringAssert.Contains("signals.Length > MaxBubbleWakeSignalsPerFrame", flora);
            StringAssert.Contains("_pendingWakeTelemetryFlags |= WakeBlackBoxSignalOverflowFlag;", flora);
            StringAssert.Contains("QueueBubbleWake(in signal);", flora);
            StringAssert.Contains("!IsFiniteAup(in signal.PositionAup)", flora);
            StringAssert.Contains("_pendingWakeTelemetryFlags |= WakeBlackBoxBubbleRejectedFlag;", flora);
            StringAssert.Contains("ResolveBubbleWakeDirection(signal.Direction)", flora);
            StringAssert.Contains("QueueProceduralWake(in wake, radius, intensity);", flora);
            StringAssert.Contains("private void QueueProceduralWake(in WakeGeneratedSignal signal, float radiusOverride = -1f, float intensityOverride = -1f)", flora);
            StringAssert.Contains("PublishWakeFluidImpulse(in signal, radius, budgetedIntensity, signalFrame);", flora);
            StringAssert.Contains("private uint ResolveWakeTelemetryFlags()", flora);
            StringAssert.Contains("_pendingWakeTelemetryFlags == 0u", flora);
            StringAssert.Contains("_pendingWakeTelemetryFlags = 0u;", flora);
        }

        [Test]
        public void MovementAcousticSignal_ReachesBiolumAndSensoryConsumersWithOverflowVisibility()
        {
            string contracts = File.ReadAllText(ContractsPath);
            string runtimeOwner = File.ReadAllText(RuntimeOwnerPath);
            string biolum = File.ReadAllText(BiolumManagerPath);
            string acousticEcho = File.ReadAllText(AcousticEchoLocationRuntimePath);
            string globalSignalsState = File.ReadAllText(GlobalSignalsStatePath);
            string globalSignals = File.ReadAllText(GlobalSignalsRuntimePath);

            StringAssert.Contains("NativeArray<MovementAcousticSignal> acousticSignals", contracts);
            StringAssert.Contains("SignalBus<MovementAcousticSignal>.TryPushTracked", contracts);
            StringAssert.Contains("SignalBus<MovementAcousticSignal>.EnsureInitialized();", runtimeOwner);
            StringAssert.Contains("MovementAcousticSignalCapacity = MovementAcousticSignal.ExpectedCapacity", globalSignalsState);
            StringAssert.Contains("SignalBus<MovementAcousticSignal>.Configure", globalSignals);
            StringAssert.Contains("laneHash: MovementAcousticSignal.LaneHash", globalSignals);
            StringAssert.Contains("private const int MovementSignalMaxDrainPerTick = 32;", biolum);
            StringAssert.Contains("private const int MovementSignalOverflowWarningCooldownFrames = 90;", biolum);
            StringAssert.Contains("private const int TouchRippleSaturationWarningCooldownFrames = 90;", biolum);
            StringAssert.Contains("private const uint MovementAcousticOverflowHash = 0x4D414F56u;", biolum);
            StringAssert.Contains("private const uint TouchRipplePoolSaturatedHash = 0x54525053u;", biolum);
            StringAssert.Contains("DrainMovementAcousticSignals();", biolum);
            StringAssert.Contains("ReadOnlySpan<MovementAcousticSignal> signals = SignalBus<MovementAcousticSignal>.GetFrameSnapshot();", biolum);
            StringAssert.Contains("signals.Length > MovementSignalMaxDrainPerTick", biolum);
            StringAssert.Contains("PublishMovementSignalOverflowWarning(signals.Length);", biolum);
            StringAssert.Contains("int count = math.min(signals.Length, MovementSignalMaxDrainPerTick);", biolum);
            StringAssert.Contains("AddOrRefreshTouchRipple(in signal);", biolum);
            StringAssert.Contains("private void PublishMovementSignalOverflowWarning(int observedCount)", biolum);
            StringAssert.Contains("FindTouchRippleSlot(signal.SourceId, out bool replacedActiveRipple);", biolum);
            StringAssert.Contains("if (replacedActiveRipple)", biolum);
            StringAssert.Contains("PublishTouchRippleSaturationWarning();", biolum);
            StringAssert.Contains("private int FindTouchRippleSlot(uint sourceId, out bool replacedActiveRipple)", biolum);
            StringAssert.Contains("replacedActiveRipple = true;", biolum);
            StringAssert.Contains("private void PublishTouchRippleSaturationWarning()", biolum);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning", biolum);
            StringAssert.Contains("MovementAcousticOverflowHash", biolum);
            StringAssert.Contains("TouchRipplePoolSaturatedHash", biolum);
            StringAssert.Contains("BiolumDirectorContextHash", biolum);
            StringAssert.Contains("observedCount", biolum);
            StringAssert.Contains("_nextMovementSignalOverflowWarningFrame = currentFrame + MovementSignalOverflowWarningCooldownFrames;", biolum);
            StringAssert.Contains("_nextTouchRippleSaturationWarningFrame = currentFrame + TouchRippleSaturationWarningCooldownFrames;", biolum);
            StringAssert.Contains("private const int MovementSignalCapacityWarningCooldownFrames = 90;", acousticEcho);
            StringAssert.Contains("private const uint MovementSignalCapacityWarningHash = 0x41454D4Fu;", acousticEcho);
            StringAssert.Contains("private const uint AcousticEchoContextHash = 0x41454348u;", acousticEcho);
            StringAssert.Contains("private static int _nextMovementSignalCapacityWarningFrame = int.MinValue;", acousticEcho);
            StringAssert.Contains("tapCount = AppendMovementSignals(frameTaps, tapCount, frame, currentTime);", acousticEcho);
            StringAssert.Contains("ReadOnlySpan<MovementAcousticSignal> signals = SignalBus<MovementAcousticSignal>.GetFrameSnapshot();", acousticEcho);
            StringAssert.Contains("int remainingCapacity = math.min(MaxEchoTapsPerFrame, frameTaps.Length) - count;", acousticEcho);
            StringAssert.Contains("signals.Length > math.max(0, remainingCapacity)", acousticEcho);
            StringAssert.Contains("PublishMovementSignalCapacityWarning(frame, signals.Length, remainingCapacity);", acousticEcho);
            StringAssert.Contains("int limit = math.min(signals.Length, math.max(0, remainingCapacity));", acousticEcho);
            StringAssert.Contains("WriteFaultBlackBox(frame, in signal.PositionAup);", acousticEcho);
            StringAssert.Contains("private static void PublishMovementSignalCapacityWarning(int frame, int observedCount, int remainingCapacity)", acousticEcho);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning", acousticEcho);
            StringAssert.Contains("MovementSignalCapacityWarningHash", acousticEcho);
            StringAssert.Contains("AcousticEchoContextHash", acousticEcho);
            StringAssert.Contains("droppedCount", acousticEcho);
            StringAssert.Contains("_nextMovementSignalCapacityWarningFrame = frame + MovementSignalCapacityWarningCooldownFrames;", acousticEcho);
        }

        [Test]
        public void RuntimeOwner_HandlesBootOrderAndDuplicateOwnerFailurePaths()
        {
            string runtimeOwner = File.ReadAllText(RuntimeOwnerPath);

            StringAssert.Contains("private static AirlockPressurizationRuntimeOwner s_activeOwner;", runtimeOwner);
            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]", runtimeOwner);
            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]", runtimeOwner);
            StringAssert.Contains("private static void InstallRuntime()", runtimeOwner);
            StringAssert.Contains("new GameObject(RuntimeObjectName)", runtimeOwner);
            StringAssert.Contains("DontDestroyOnLoad(host)", runtimeOwner);
            StringAssert.Contains("host.AddComponent<AirlockPressurizationRuntimeOwner>()", runtimeOwner);
            StringAssert.Contains("private bool TryClaimActiveOwner()", runtimeOwner);
            StringAssert.Contains("private void ReleaseActiveOwner()", runtimeOwner);
            StringAssert.Contains("if (!Application.isPlaying || !TryClaimActiveOwner())", runtimeOwner);
            StringAssert.Contains("if (!_claimedOwner)", runtimeOwner);
            StringAssert.Contains("if (GlobalRegistry.Dispatcher == null)", runtimeOwner);
            StringAssert.Contains("EnsureHandlesReady();", runtimeOwner);
            StringAssert.Contains("case GlobalRegistryServiceSlot.DataVault:", runtimeOwner);
            StringAssert.Contains("TryRegisterDispatcherPhases();", runtimeOwner);
            StringAssert.DoesNotContain("GlobalRegistry.Dispatcher == null || !EnsureHandlesReady()", runtimeOwner);
        }

        [Test]
        public void RuntimeOwner_PublishesTelemetryForBridgeFailurePaths()
        {
            string runtimeOwner = File.ReadAllText(RuntimeOwnerPath);
            string normalizedRuntimeOwner = runtimeOwner.Replace("\r\n", "\n");

            StringAssert.Contains("private const int RuntimeWarningCooldownFrames = 60;", runtimeOwner);
            StringAssert.Contains("private const uint RuntimeWarningContextHash = 0x4150524Fu;", runtimeOwner);
            StringAssert.Contains("private const uint DuplicateOwnerWarningHash = 0x4150444Fu;", runtimeOwner);
            StringAssert.Contains("private const uint DispatcherMissingWarningHash = 0x41504453u;", runtimeOwner);
            StringAssert.Contains("private const uint DataVaultMissingWarningHash = 0x41504456u;", runtimeOwner);
            StringAssert.Contains("private const uint HandleAcquireWarningHash = 0x41504841u;", runtimeOwner);
            StringAssert.Contains("private const uint SimulationResolveViewsWarningHash = 0x41505253u;", runtimeOwner);
            StringAssert.Contains("private const uint PostResolveViewsWarningHash = 0x41505250u;", runtimeOwner);
            StringAssert.Contains("PublishRuntimeWarning(DuplicateOwnerWarningHash", runtimeOwner);
            StringAssert.Contains("PublishRuntimeWarning(DispatcherMissingWarningHash", runtimeOwner);
            StringAssert.Contains("if (currentService != null)", runtimeOwner);
            StringAssert.Contains("if (!EnsureHandlesReady())", runtimeOwner);
            StringAssert.Contains("private void PublishMissingHandlesWarning(IDataVault vault, uint frame)", runtimeOwner);
            StringAssert.Contains("PublishRuntimeWarning(DataVaultMissingWarningHash", runtimeOwner);
            StringAssert.Contains("PublishRuntimeWarning(HandleAcquireWarningHash", runtimeOwner);
            StringAssert.Contains("PublishRuntimeWarning(SimulationResolveViewsWarningHash", runtimeOwner);
            StringAssert.Contains("PublishRuntimeWarning(PostResolveViewsWarningHash", runtimeOwner);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(warningHash, RuntimeWarningContextHash, scalarValue);", runtimeOwner);
            StringAssert.Contains("nextWarningFrame = frameIndex + RuntimeWarningCooldownFrames;", runtimeOwner);
            StringAssert.Contains("ResetRuntimeWarningCooldowns();", runtimeOwner);
            StringAssert.Contains("if (!_claimedOwner)\n                return;\n\n            TryRegisterHotSwapListener();", normalizedRuntimeOwner);
            StringAssert.Contains("_lastScheduledActiveCount = 0;\n            TryRegisterHotSwapListener();", normalizedRuntimeOwner);
        }

        [Test]
        public void BaseAirlockSnapshotBridge_RehydratesAfterConstructionLoad()
        {
            string baseAirlock = File.ReadAllText(BaseAirlockPath);
            string baseModule = File.ReadAllText(BaseModulePath);
            string constructionManager = File.ReadAllText(ConstructionManagerPath);

            StringAssert.Contains("internal void RequestPressurizationSnapshotRefresh()", baseAirlock);
            StringAssert.Contains("_pressurizationPublishPending = true;", baseAirlock);
            StringAssert.Contains("PublishPressurizationSnapshot();", baseAirlock);
            StringAssert.Contains("RefreshOwnedAirlockPressurizationSnapshots();", baseModule);
            StringAssert.Contains("airlock.RequestPressurizationSnapshotRefresh();", baseModule);
            StringAssert.Contains("serviceSlot == GlobalRegistryServiceSlot.DataVault", baseModule);
            StringAssert.Contains("serviceSlot == GlobalRegistryServiceSlot.Dispatcher && currentService != null", baseModule);
            StringAssert.Contains("BaseModule restoredBaseModule = null;", constructionManager);
            StringAssert.Contains("restoredBaseModule?.RefreshAfterLoad();", constructionManager);
        }
    }
}
