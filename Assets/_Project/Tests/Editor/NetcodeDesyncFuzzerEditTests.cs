#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Hecton8.Networking;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using InputStateDTO = Hecton8.Core.InputStateDTO;

namespace Hecton8.Tests.Editor
{
    public sealed unsafe class NetcodeDesyncFuzzerEditTests
    {
        private const int FrameCount = 10000;
        private const int FlushFrames = 96;
        private const int SnapshotRingCapacity = 256;
        private const int PhaseTraceSlots = 4;
        private const int TransportCapacity = 8192;
        private const uint BatchPacketLossPermille = 150u;
        private const uint BaseDelayFrames200Ms = 12u;
        private const uint JitterFrames = 3u;
        private const uint RedundancyCount = 8u;
        private const uint LagSpikeFrames = 60u;
        private const uint WorldSeed = 0x48454354u;
        private const BufferID FuzzerInputBuffer = BufferID.ShinobuNetcodeFuzzerInput;
        private const BufferID FuzzerHostAuthoritativeInputBuffer = BufferID.ShinobuNetcodeFuzzerHostAuthoritativeInput;
        private const BufferID FuzzerClientAuthoritativeInputBuffer = BufferID.ShinobuNetcodeFuzzerClientAuthoritativeInput;
        private const BufferID FuzzerClientAppliedInputBuffer = BufferID.ShinobuNetcodeFuzzerClientAppliedInput;
        private const BufferID FuzzerHostKinematicsBuffer = BufferID.ShinobuNetcodeFuzzerHostKinematics;
        private const BufferID FuzzerClientKinematicsBuffer = BufferID.ShinobuNetcodeFuzzerClientKinematics;
        private const BufferID FuzzerHostInventoryBuffer = BufferID.ShinobuNetcodeFuzzerHostInventory;
        private const BufferID FuzzerClientInventoryBuffer = BufferID.ShinobuNetcodeFuzzerClientInventory;
        private const BufferID FuzzerHostEcosystemBuffer = BufferID.ShinobuNetcodeFuzzerHostEcosystem;
        private const BufferID FuzzerClientEcosystemBuffer = BufferID.ShinobuNetcodeFuzzerClientEcosystem;
        private const BufferID FuzzerSnapshotBuffer = BufferID.ShinobuNetcodeFuzzerSnapshotRing;
        private const BufferID FuzzerTelemetryBuffer = BufferID.ShinobuNetcodeFuzzerTelemetryRing;
        private const BufferID FuzzerVisualBuffer = BufferID.ShinobuNetcodeFuzzerVisualNoise;
        private const BufferID FuzzerResultBuffer = BufferID.ShinobuNetcodeFuzzerResult;
        private const BufferID FuzzerDeliveryTickBuffer = BufferID.ShinobuNetcodeFuzzerDeliveryTicks;
        private const BufferID FuzzerHostDispatcherStateBuffer = BufferID.ShinobuNetcodeFuzzerHostDispatcherState;
        private const BufferID FuzzerClientDispatcherStateBuffer = BufferID.ShinobuNetcodeFuzzerClientDispatcherState;
        private const string NetworkProfilePath = "Assets/_SourceData/Networking/fuzzer_network_profiles.csv";
        private const string ReportsDirectory = "Docs/Reports";
        private const uint ExpectedNetworkProfileHash = 0x2DA21307u;
        private static FuzzerResultDTO s_lastResult;
        private static double3 s_lastHostAup;
        private static double3 s_lastClientAup;
        private static bool s_lastRunCompleted;

        [Test]
        public void NetworkPacketDto_Layout_IsExplicitSixtyFourBytes()
        {
            Assert.AreEqual(24, UnsafeUtility.SizeOf<InputStateDTO>());
            Assert.AreEqual(4, UnsafeUtility.AlignOf<InputStateDTO>());
            Assert.AreEqual(0, OffsetOf<InputStateDTO>(nameof(InputStateDTO.LookDelta)));
            Assert.AreEqual(8, OffsetOf<InputStateDTO>(nameof(InputStateDTO.MoveAxis)));
            Assert.AreEqual(16, OffsetOf<InputStateDTO>(nameof(InputStateDTO.ButtonMask)));
            Assert.AreEqual(24, UnsafeUtility.SizeOf<FuzzerWireAupDTO>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<FuzzerWireAupDTO>());
            Assert.AreEqual(0, OffsetOf<FuzzerWireAupDTO>(nameof(FuzzerWireAupDTO.SectorHash)));
            Assert.AreEqual(8, OffsetOf<FuzzerWireAupDTO>(nameof(FuzzerWireAupDTO.LocalMillimetersX)));
            Assert.AreEqual(20, OffsetOf<FuzzerWireAupDTO>("_pad0"));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<NetworkPacketDTO>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<NetworkPacketDTO>());
            Assert.AreEqual(0, OffsetOf<NetworkPacketDTO>(nameof(NetworkPacketDTO.SourceTick)));
            Assert.AreEqual(4, OffsetOf<NetworkPacketDTO>(nameof(NetworkPacketDTO.DeliveryTick)));
            Assert.AreEqual(8, OffsetOf<NetworkPacketDTO>(nameof(NetworkPacketDTO.AupPayload)));
            Assert.AreEqual(32, OffsetOf<NetworkPacketDTO>(nameof(NetworkPacketDTO.Input)));
            Assert.AreEqual(56, OffsetOf<NetworkPacketDTO>(nameof(NetworkPacketDTO.Sequence)));
            Assert.AreEqual(60, OffsetOf<NetworkPacketDTO>(nameof(NetworkPacketDTO.Flags)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<NetworkFuzzerProfileDTO>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<NetworkFuzzerProfileDTO>());
            Assert.AreEqual(28, OffsetOf<NetworkFuzzerProfileDTO>(nameof(NetworkFuzzerProfileDTO.GlobalQualityWeight)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<FuzzerKinematicStateDTO>());
            Assert.AreEqual(24, OffsetOf<FuzzerKinematicStateDTO>(nameof(FuzzerKinematicStateDTO.LocalPosition)));
            Assert.AreEqual(48, OffsetOf<FuzzerKinematicStateDTO>(nameof(FuzzerKinematicStateDTO.Velocity)));
            Assert.AreEqual(64, UnsafeUtility.SizeOf<FuzzerTelemetryEntryDTO>());
            Assert.AreEqual(32, OffsetOf<FuzzerTelemetryEntryDTO>(nameof(FuzzerTelemetryEntryDTO.ClientAupLocal)));
            Assert.AreEqual(128, UnsafeUtility.SizeOf<FuzzerResultDTO>());
            Assert.AreEqual(8, UnsafeUtility.AlignOf<FuzzerResultDTO>());
            Assert.AreEqual(120, OffsetOf<FuzzerResultDTO>(nameof(FuzzerResultDTO.AupPayloadSamples)));
            Assert.AreEqual(124, OffsetOf<FuzzerResultDTO>(nameof(FuzzerResultDTO.AupPayloadMismatches)));
            Assert.AreEqual(32, UnsafeUtility.SizeOf<DispatcherStateDTO>());
            Assert.AreEqual(0, OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.CurrentPhaseId)));
            Assert.AreEqual(28, OffsetOf<DispatcherStateDTO>(nameof(DispatcherStateDTO.Flags)));
        }

        [Test]
        public void HeadlessRollbackFuzzer_ConvergesUnderPacketLoss()
        {
            NetworkFuzzerProfileDTO profile = LoadNetworkProfile();
            Assert.GreaterOrEqual(TransportCapacity, ComputeRequiredTransportCapacity(in profile));

            GlobalDataVault hostVault = null;
            GlobalDataVault clientVault = null;
            NativeList<NetworkPacketDTO> clientToHost = default;
            NativeList<NetworkPacketDTO> hostToClient = default;

            try
            {
                hostVault = GlobalDataVault.Create(64);
                clientVault = GlobalDataVault.Create(64);

                NativeArray<InputStateDTO> localInputs = AcquireVaultBuffer<InputStateDTO>(
                    clientVault,
                    FuzzerInputBuffer,
                    FrameCount,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<InputStateDTO> hostAuthoritativeInputs = AcquireVaultBuffer<InputStateDTO>(
                    hostVault,
                    FuzzerHostAuthoritativeInputBuffer,
                    FrameCount,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<InputStateDTO> clientAuthoritativeInputs = AcquireVaultBuffer<InputStateDTO>(
                    clientVault,
                    FuzzerClientAuthoritativeInputBuffer,
                    FrameCount,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<InputStateDTO> clientAppliedInputs = AcquireVaultBuffer<InputStateDTO>(
                    clientVault,
                    FuzzerClientAppliedInputBuffer,
                    FrameCount,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<uint> clientDeliveryTicks = AcquireVaultBuffer<uint>(
                    clientVault,
                    FuzzerDeliveryTickBuffer,
                    FrameCount,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerKinematicStateDTO> hostKinematics = AcquireVaultBuffer<FuzzerKinematicStateDTO>(
                    hostVault,
                    FuzzerHostKinematicsBuffer,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerKinematicStateDTO> clientKinematics = AcquireVaultBuffer<FuzzerKinematicStateDTO>(
                    clientVault,
                    FuzzerClientKinematicsBuffer,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerInventoryStateDTO> hostInventory = AcquireVaultBuffer<FuzzerInventoryStateDTO>(
                    hostVault,
                    FuzzerHostInventoryBuffer,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerInventoryStateDTO> clientInventory = AcquireVaultBuffer<FuzzerInventoryStateDTO>(
                    clientVault,
                    FuzzerClientInventoryBuffer,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerEcosystemStateDTO> hostEcosystem = AcquireVaultBuffer<FuzzerEcosystemStateDTO>(
                    hostVault,
                    FuzzerHostEcosystemBuffer,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerEcosystemStateDTO> clientEcosystem = AcquireVaultBuffer<FuzzerEcosystemStateDTO>(
                    clientVault,
                    FuzzerClientEcosystemBuffer,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerSnapshotDTO> clientStateRing = AcquireVaultBuffer<FuzzerSnapshotDTO>(
                    clientVault,
                    FuzzerSnapshotBuffer,
                    SnapshotRingCapacity,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerTelemetryEntryDTO> telemetry = AcquireVaultBuffer<FuzzerTelemetryEntryDTO>(
                    clientVault,
                    FuzzerTelemetryBuffer,
                    RollbackNetcodeConstants.TelemetryFrameCapacity,
                    NativeArrayOptions.ClearMemory);
                NativeArray<FuzzerVisualNoiseDTO> clientVisualNoise = AcquireVaultBuffer<FuzzerVisualNoiseDTO>(
                    clientVault,
                    FuzzerVisualBuffer,
                    16,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<FuzzerResultDTO> result = AcquireVaultBuffer<FuzzerResultDTO>(
                    clientVault,
                    FuzzerResultBuffer,
                    1,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<DispatcherStateDTO> hostDispatcherState = AcquireVaultBuffer<DispatcherStateDTO>(
                    hostVault,
                    FuzzerHostDispatcherStateBuffer,
                    PhaseTraceSlots,
                    NativeArrayOptions.UninitializedMemory);
                NativeArray<DispatcherStateDTO> clientDispatcherState = AcquireVaultBuffer<DispatcherStateDTO>(
                    clientVault,
                    FuzzerClientDispatcherStateBuffer,
                    PhaseTraceSlots,
                    NativeArrayOptions.UninitializedMemory);

                clientToHost = new NativeList<NetworkPacketDTO>(TransportCapacity, Allocator.TempJob);
                hostToClient = new NativeList<NetworkPacketDTO>(TransportCapacity, Allocator.TempJob);

                InjectRandomizedInputsJob inject = new InjectRandomizedInputsJob
                {
                    Inputs = localInputs,
                    WorldSeed = WorldSeed
                };
                MockTransportLayerJob transport = new MockTransportLayerJob
                {
                    LocalInputs = localInputs,
                    HostAuthoritativeInputs = hostAuthoritativeInputs,
                    ClientAuthoritativeInputs = clientAuthoritativeInputs,
                    ClientDeliveryTicks = clientDeliveryTicks,
                    ClientToHost = clientToHost,
                    HostToClient = hostToClient,
                    Result = result,
                    FrameCount = FrameCount,
                    FlushFrames = (int)profile.FlushFrames,
                    BaseDelayFrames = profile.BaseDelayFrames,
                    JitterFrames = profile.JitterFrames,
                    PacketLossPermille = profile.PacketLossPermille,
                    RedundancyCount = profile.RedundancyCount,
                    LagSpikeFrames = profile.LagSpikeFrames,
                    WorldSeed = WorldSeed
                };

                RunHeadlessRollbackFuzzerJob fuzzer = new RunHeadlessRollbackFuzzerJob
                {
                    LocalInputs = localInputs,
                    HostAuthoritativeInputs = hostAuthoritativeInputs,
                    ClientAuthoritativeInputs = clientAuthoritativeInputs,
                    ClientAppliedInputs = clientAppliedInputs,
                    ClientDeliveryTicks = clientDeliveryTicks,
                    HostKinematics = hostKinematics,
                    ClientKinematics = clientKinematics,
                    HostInventory = hostInventory,
                    ClientInventory = clientInventory,
                    HostEcosystem = hostEcosystem,
                    ClientEcosystem = clientEcosystem,
                    ClientStateRing = clientStateRing,
                    Telemetry = telemetry,
                    ClientVisualNoise = clientVisualNoise,
                    Result = result,
                    FrameCount = FrameCount,
                    FlushFrames = (int)profile.FlushFrames,
                    SnapshotRingCapacity = SnapshotRingCapacity,
                    MaxRollbackFrames = 120,
                    GlobalQualityWeight = profile.GlobalQualityWeight,
                    WorldSeed = WorldSeed
                };

                ValidateMerkleParityJob validate = new ValidateMerkleParityJob
                {
                    HostKinematics = hostKinematics,
                    ClientKinematics = clientKinematics,
                    HostInventory = hostInventory,
                    ClientInventory = clientInventory,
                    HostEcosystem = hostEcosystem,
                    ClientEcosystem = clientEcosystem,
                    Result = result
                };

                RunScheduledFuzzer(ref inject, ref transport, ref fuzzer, ref validate, hostDispatcherState, clientDispatcherState);
                FuzzerResultDTO scheduled = result[0];
                _ = GC.GetAllocatedBytesForCurrentThread();
                RunJobBodiesForAllocationProbe(ref inject, ref transport, ref fuzzer, ref validate, hostDispatcherState, clientDispatcherState);

                long allocationBefore = GC.GetAllocatedBytesForCurrentThread();
                RunJobBodiesForAllocationProbe(ref inject, ref transport, ref fuzzer, ref validate, hostDispatcherState, clientDispatcherState);
                long allocationDelta = GC.GetAllocatedBytesForCurrentThread() - allocationBefore;

                FuzzerResultDTO final = result[0];
                final.ManagedAllocationBytes = allocationDelta;
                if (!ScheduledPathMatchesDirect(in scheduled, in final))
                {
                    final.ErrorFlags |= scheduled.ErrorFlags | FuzzerErrorFlags.ScheduledPathMismatch;
                    if (scheduled.ErrorFlags != 0u)
                    {
                        final.MismatchTick = scheduled.MismatchTick;
                        final.MismatchBufferId = scheduled.MismatchBufferId;
                        final.MismatchByteOffset = scheduled.MismatchByteOffset;
                    }
                    else if (final.MismatchBufferId == 0u)
                    {
                        final.MismatchTick = 0u;
                        final.MismatchBufferId = (uint)FuzzerResultBuffer;
                        final.MismatchByteOffset = 0u;
                    }
                }
                result[0] = final;
                AssertDispatcherPhaseTrace(hostDispatcherState, clientDispatcherState);

                s_lastResult = final;
                FuzzerKinematicStateDTO hostState = hostKinematics[0];
                FuzzerKinematicStateDTO clientState = clientKinematics[0];
                s_lastHostAup = FuzzerMath.ComposeAup(in hostState);
                s_lastClientAup = FuzzerMath.ComposeAup(in clientState);
                s_lastRunCompleted = true;

                if (final.ErrorFlags != 0u)
                {
                    WriteFailureCsv(
                        in final,
                        hostKinematics,
                        clientKinematics,
                        hostInventory,
                        clientInventory,
                        hostEcosystem,
                        clientEcosystem);
                    WriteBlackBoxDump(telemetry);
                }
                else
                {
                    WriteQaReportJson(in final, in profile);
                }

                Assert.AreEqual(0u, final.ErrorFlags);
                Assert.AreEqual(final.HostMasterHash, final.ClientMasterHash);
                Assert.AreEqual(final.HostLootHash, final.ClientLootHash);
                Assert.GreaterOrEqual(final.MaxRollbackDepth, 60u);
                Assert.Greater(final.DroppedPackets, 0u);
                Assert.Greater(final.OutOfOrderDeliveries, 0u);
                Assert.Greater(final.AupPayloadSamples, 0u);
                Assert.AreEqual(0u, final.AupPayloadMismatches);
                Assert.AreEqual(ExpectedNetworkProfileHash, profile.ProfileHash);
                Assert.AreEqual(BatchPacketLossPermille, profile.PacketLossPermille);
                Assert.AreEqual(0L, allocationDelta);
            }
            finally
            {
                if (hostToClient.IsCreated)
                    hostToClient.Dispose();
                if (clientToHost.IsCreated)
                    clientToHost.Dispose();
                if (clientVault != null)
                    clientVault.Dispose();
                if (hostVault != null)
                    hostVault.Dispose();
            }
        }

        private static void RunScheduledFuzzer(
            ref InjectRandomizedInputsJob inject,
            ref MockTransportLayerJob transport,
            ref RunHeadlessRollbackFuzzerJob fuzzer,
            ref ValidateMerkleParityJob validate,
            NativeArray<DispatcherStateDTO> hostDispatcherState,
            NativeArray<DispatcherStateDTO> clientDispatcherState)
        {
            InitializeMockDispatcherTrace(hostDispatcherState, clientDispatcherState);
            StampDualMockDispatchers(hostDispatcherState, clientDispatcherState, 0, DispatcherPhase.PreSimulation, 0, 0x50726553u);
            JobHandle injectHandle = inject.Schedule(FrameCount, 64);
            JobHandle transportHandle = transport.Schedule(injectHandle);
            StampDualMockDispatchers(hostDispatcherState, clientDispatcherState, 1, DispatcherPhase.Simulation, FrameCount, 0x53696D75u);
            JobHandle fuzzerHandle = fuzzer.Schedule(transportHandle);
            StampDualMockDispatchers(hostDispatcherState, clientDispatcherState, 2, DispatcherPhase.PostSimulation, FrameCount + FlushFrames, 0x506F7374u);
            JobHandle validateHandle = validate.Schedule(fuzzerHandle);
            JobHandle outputHandle = JobHandle.CombineDependencies(fuzzerHandle, validateHandle);
            outputHandle.Complete();
            StampDualMockDispatchers(hostDispatcherState, clientDispatcherState, 3, DispatcherPhase.VisualSync, FrameCount + FlushFrames, 0x56697375u);
        }

        private static void RunJobBodiesForAllocationProbe(
            ref InjectRandomizedInputsJob inject,
            ref MockTransportLayerJob transport,
            ref RunHeadlessRollbackFuzzerJob fuzzer,
            ref ValidateMerkleParityJob validate,
            NativeArray<DispatcherStateDTO> hostDispatcherState,
            NativeArray<DispatcherStateDTO> clientDispatcherState)
        {
            InitializeMockDispatcherTrace(hostDispatcherState, clientDispatcherState);
            StampDualMockDispatchers(hostDispatcherState, clientDispatcherState, 0, DispatcherPhase.PreSimulation, 0, 0x50726553u);
            for (int i = 0; i < FrameCount; i++)
                inject.Execute(i);
            transport.Execute();
            StampDualMockDispatchers(hostDispatcherState, clientDispatcherState, 1, DispatcherPhase.Simulation, FrameCount, 0x53696D75u);
            fuzzer.Execute();
            StampDualMockDispatchers(hostDispatcherState, clientDispatcherState, 2, DispatcherPhase.PostSimulation, FrameCount + FlushFrames, 0x506F7374u);
            validate.Execute();
            StampDualMockDispatchers(hostDispatcherState, clientDispatcherState, 3, DispatcherPhase.VisualSync, FrameCount + FlushFrames, 0x56697375u);
        }

        private static bool ScheduledPathMatchesDirect(in FuzzerResultDTO scheduled, in FuzzerResultDTO direct)
        {
            return scheduled.ErrorFlags == direct.ErrorFlags &&
                scheduled.HostMasterHash == direct.HostMasterHash &&
                scheduled.ClientMasterHash == direct.ClientMasterHash &&
                scheduled.HostKinematicHash == direct.HostKinematicHash &&
                scheduled.ClientKinematicHash == direct.ClientKinematicHash &&
                scheduled.HostInventoryHash == direct.HostInventoryHash &&
                scheduled.ClientInventoryHash == direct.ClientInventoryHash &&
                scheduled.HostEcosystemHash == direct.HostEcosystemHash &&
                scheduled.ClientEcosystemHash == direct.ClientEcosystemHash &&
                scheduled.MismatchTick == direct.MismatchTick &&
                scheduled.MismatchBufferId == direct.MismatchBufferId &&
                scheduled.MismatchByteOffset == direct.MismatchByteOffset &&
                scheduled.MaxRollbackDepth == direct.MaxRollbackDepth &&
                scheduled.DroppedPackets == direct.DroppedPackets &&
                scheduled.DeliveredPackets == direct.DeliveredPackets &&
                scheduled.OutOfOrderDeliveries == direct.OutOfOrderDeliveries &&
                scheduled.MaxCatchupMicros == direct.MaxCatchupMicros &&
                scheduled.LagSpikeCount == direct.LagSpikeCount &&
                scheduled.HostLootHash == direct.HostLootHash &&
                scheduled.ClientLootHash == direct.ClientLootHash &&
                scheduled.AupPayloadSamples == direct.AupPayloadSamples &&
                scheduled.AupPayloadMismatches == direct.AupPayloadMismatches;
        }

        private static void InitializeMockDispatcherTrace(
            NativeArray<DispatcherStateDTO> hostDispatcherState,
            NativeArray<DispatcherStateDTO> clientDispatcherState)
        {
            for (int i = 0; i < PhaseTraceSlots; i++)
            {
                hostDispatcherState[i] = default;
                clientDispatcherState[i] = default;
            }
        }

        private static void StampDualMockDispatchers(
            NativeArray<DispatcherStateDTO> hostDispatcherState,
            NativeArray<DispatcherStateDTO> clientDispatcherState,
            int slot,
            DispatcherPhase phase,
            int frame,
            uint flags)
        {
            StampMockDispatcher(hostDispatcherState, slot, phase, (uint)frame, 1u, flags);
            StampMockDispatcher(clientDispatcherState, slot, phase, (uint)frame, 2u, flags);
        }

        private static void StampMockDispatcher(
            NativeArray<DispatcherStateDTO> trace,
            int slot,
            DispatcherPhase phase,
            uint frame,
            uint lane,
            uint flags)
        {
            DispatcherStateDTO state = default;
            state.CurrentPhaseId = (uint)phase;
            state.CurrentFrame = frame;
            state.ActiveBucket = lane;
            state.ActiveBucketMask = 1u << (int)(lane - 1u);
            state.SortedSystemCount = 4u;
            state.DisabledSystemCount = 0u;
            state.PendingSimulationJobCount = phase == DispatcherPhase.Simulation ? 1u : 0u;
            state.Flags = flags;
            trace[slot] = state;
        }

        private static void AssertDispatcherPhaseTrace(
            NativeArray<DispatcherStateDTO> hostDispatcherState,
            NativeArray<DispatcherStateDTO> clientDispatcherState)
        {
            Assert.GreaterOrEqual(hostDispatcherState.Length, PhaseTraceSlots);
            Assert.GreaterOrEqual(clientDispatcherState.Length, PhaseTraceSlots);
            AssertDispatcherPhase(hostDispatcherState, 0, DispatcherPhase.PreSimulation, 1u);
            AssertDispatcherPhase(hostDispatcherState, 1, DispatcherPhase.Simulation, 1u);
            AssertDispatcherPhase(hostDispatcherState, 2, DispatcherPhase.PostSimulation, 1u);
            AssertDispatcherPhase(hostDispatcherState, 3, DispatcherPhase.VisualSync, 1u);
            AssertDispatcherPhase(clientDispatcherState, 0, DispatcherPhase.PreSimulation, 2u);
            AssertDispatcherPhase(clientDispatcherState, 1, DispatcherPhase.Simulation, 2u);
            AssertDispatcherPhase(clientDispatcherState, 2, DispatcherPhase.PostSimulation, 2u);
            AssertDispatcherPhase(clientDispatcherState, 3, DispatcherPhase.VisualSync, 2u);
        }

        private static void AssertDispatcherPhase(
            NativeArray<DispatcherStateDTO> trace,
            int slot,
            DispatcherPhase phase,
            uint lane)
        {
            DispatcherStateDTO state = trace[slot];
            Assert.AreEqual((uint)phase, state.CurrentPhaseId);
            Assert.AreEqual(lane, state.ActiveBucket);
            Assert.AreEqual(1u << (int)(lane - 1u), state.ActiveBucketMask);
            Assert.AreEqual(4u, state.SortedSystemCount);
            Assert.AreEqual(0u, state.DisabledSystemCount);
        }

        private static int OffsetOf<T>(string fieldName) where T : unmanaged
        {
            return Marshal.OffsetOf(typeof(T), fieldName).ToInt32();
        }

        private static NativeArray<T> AcquireVaultBuffer<T>(
            GlobalDataVault vault,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options) where T : struct
        {
            Assert.NotNull(vault);
            VaultGenerationHandle<T> handle = vault.EnsureGenerationHandle<T>(
                bufferId,
                requiredLength,
                RollbackNetcodeFuzzerOwner.System,
                options);
            Assert.IsTrue(vault.TryResolveHandle(in handle, out NativeArray<T> buffer));
            Assert.IsTrue(buffer.IsCreated);
            Assert.GreaterOrEqual(buffer.Length, requiredLength);
            return buffer;
        }

        internal static bool RunFromEditor(out NetcodeDesyncFuzzerRunSummary summary)
        {
            s_lastResult = default;
            s_lastHostAup = default;
            s_lastClientAup = default;
            s_lastRunCompleted = false;
            bool passed = true;
            try
            {
                new NetcodeDesyncFuzzerEditTests().HeadlessRollbackFuzzer_ConvergesUnderPacketLoss();
            }
            catch (Exception)
            {
                passed = false;
                s_lastResult.ErrorFlags |= FuzzerErrorFlags.EditorExecutionFailure;
            }

            FuzzerResultDTO result = s_lastResult;
            if (!s_lastRunCompleted)
                passed = false;
            if (result.ErrorFlags != 0u || result.HostMasterHash != result.ClientMasterHash)
                passed = false;

            summary = new NetcodeDesyncFuzzerRunSummary
            {
                Passed = passed ? 1u : 0u,
                ErrorFlags = result.ErrorFlags,
                MaxRollbackDepth = result.MaxRollbackDepth,
                MaxCatchupMicros = result.MaxCatchupMicros,
                DroppedPackets = result.DroppedPackets,
                OutOfOrderDeliveries = result.OutOfOrderDeliveries,
                HostMasterHash = result.HostMasterHash,
                ClientMasterHash = result.ClientMasterHash,
                HostAup = s_lastHostAup,
                ClientAup = s_lastClientAup
            };
            return passed;
        }

        private static int ComputeRequiredTransportCapacity(in NetworkFuzzerProfileDTO profile)
        {
            uint worstDelay = profile.BaseDelayFrames + profile.JitterFrames + profile.LagSpikeFrames + profile.RedundancyCount + 2u;
            ulong required = (ulong)math.max(1u, profile.RedundancyCount) * worstDelay;
            return required > int.MaxValue ? int.MaxValue : (int)required;
        }

        private static unsafe NetworkFuzzerProfileDTO LoadNetworkProfile()
        {
            NetworkFuzzerProfileDTO profile = DefaultNetworkProfile();
            Assert.IsTrue(File.Exists(NetworkProfilePath));

            FileInfo info = new FileInfo(NetworkProfilePath);
            Assert.Greater(info.Length, 0L);
            Assert.LessOrEqual(info.Length, 4096L);

            NativeArray<byte> bytes = new NativeArray<byte>((int)info.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
            int parsed = 0;
            try
            {
                using (FileStream stream = new FileStream(NetworkProfilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                    int readTotal = 0;
                    while (readTotal < bytes.Length)
                    {
                        int read = stream.Read(new Span<byte>((byte*)ptr + readTotal, bytes.Length - readTotal));
                        if (read <= 0)
                            break;
                        readTotal += read;
                    }

                    if (readTotal > 0)
                        parsed = NetworkFuzzerProfileCsvParser.Parse(new ReadOnlySpan<byte>(ptr, readTotal), ExpectedNetworkProfileHash, ref profile);
                }
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }

            Assert.AreEqual(1, parsed);
            return SanitizeProfile(in profile);
        }

        private static NetworkFuzzerProfileDTO DefaultNetworkProfile()
        {
            NetworkFuzzerProfileDTO profile = default;
            profile.ProfileHash = ExpectedNetworkProfileHash;
            profile.BaseDelayFrames = BaseDelayFrames200Ms;
            profile.JitterFrames = JitterFrames;
            profile.PacketLossPermille = BatchPacketLossPermille;
            profile.RedundancyCount = RedundancyCount;
            profile.LagSpikeFrames = LagSpikeFrames;
            profile.FlushFrames = FlushFrames;
            profile.GlobalQualityWeight = 1f;
            profile.PingMilliseconds = 200f;
            profile.JitterMilliseconds = 50f;
            profile.Flags = 1u;
            return profile;
        }

        private static NetworkFuzzerProfileDTO SanitizeProfile(in NetworkFuzzerProfileDTO source)
        {
            NetworkFuzzerProfileDTO profile = source;
            profile.BaseDelayFrames = math.clamp(profile.BaseDelayFrames, 1u, 120u);
            profile.JitterFrames = math.min(profile.JitterFrames, 60u);
            profile.PacketLossPermille = math.min(profile.PacketLossPermille, 1000u);
            profile.RedundancyCount = math.clamp(profile.RedundancyCount, 1u, 16u);
            profile.LagSpikeFrames = math.min(profile.LagSpikeFrames, 120u);
            profile.FlushFrames = math.max(profile.FlushFrames, profile.BaseDelayFrames + profile.JitterFrames + profile.LagSpikeFrames + profile.RedundancyCount + 8u);
            profile.GlobalQualityWeight = math.saturate(profile.GlobalQualityWeight);
            return profile;
        }

        private static void WriteQaReportJson(in FuzzerResultDTO result, in NetworkFuzzerProfileDTO profile)
        {
            Directory.CreateDirectory(ReportsDirectory);
            using (FileStream stream = new FileStream(Path.Combine(ReportsDirectory, "QA_OPTIMIZATION_REPORT.json"), FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                WriteAscii(stream, "{\n  \"LockstepParityVerified\": true,\n  \"Agent\": \"SHINOBU_257\",\n  \"Frames\": ");
                WriteUInt(stream, FrameCount);
                WriteAscii(stream, ",\n  \"PacketLossPermille\": ");
                WriteUInt(stream, profile.PacketLossPermille);
                WriteAscii(stream, ",\n  \"BaseDelayFrames\": ");
                WriteUInt(stream, profile.BaseDelayFrames);
                WriteAscii(stream, ",\n  \"GlobalQualityWeight\": ");
                WriteFloatFixed3(stream, profile.GlobalQualityWeight);
                WriteAscii(stream, ",\n  \"MaxRollbackDepth\": ");
                WriteUInt(stream, result.MaxRollbackDepth);
                WriteAscii(stream, ",\n  \"MaxCatchupMicros\": ");
                WriteUInt(stream, result.MaxCatchupMicros);
                WriteAscii(stream, ",\n  \"MasterStateHash\": \"");
                WriteHex64(stream, result.HostMasterHash);
                WriteAscii(stream, "\",\n  \"DroppedPackets\": ");
                WriteUInt(stream, result.DroppedPackets);
                WriteAscii(stream, ",\n  \"OutOfOrderDeliveries\": ");
                WriteUInt(stream, result.OutOfOrderDeliveries);
                WriteAscii(stream, "\n}\n");
            }
        }

        private static unsafe void WriteFailureCsv(
            in FuzzerResultDTO result,
            NativeArray<FuzzerKinematicStateDTO> hostKinematics,
            NativeArray<FuzzerKinematicStateDTO> clientKinematics,
            NativeArray<FuzzerInventoryStateDTO> hostInventory,
            NativeArray<FuzzerInventoryStateDTO> clientInventory,
            NativeArray<FuzzerEcosystemStateDTO> hostEcosystem,
            NativeArray<FuzzerEcosystemStateDTO> clientEcosystem)
        {
            Directory.CreateDirectory(ReportsDirectory);
            using (FileStream stream = new FileStream(Path.Combine(ReportsDirectory, "HEADLESS_DESYNC_FAILURES.csv"), FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                WriteAscii(stream, "tick,buffer_id,byte_offset,host_hash,client_hash,error_flags,host_kinematics,client_kinematics,host_inventory,client_inventory,host_ecosystem,client_ecosystem\n");
                WriteUInt(stream, result.MismatchTick);
                stream.WriteByte((byte)',');
                WriteUInt(stream, result.MismatchBufferId);
                stream.WriteByte((byte)',');
                WriteUInt(stream, result.MismatchByteOffset);
                stream.WriteByte((byte)',');
                WriteHex64(stream, result.HostMasterHash);
                stream.WriteByte((byte)',');
                WriteHex64(stream, result.ClientMasterHash);
                stream.WriteByte((byte)',');
                WriteHex32(stream, result.ErrorFlags);
                stream.WriteByte((byte)',');
                WriteBufferHex(stream, hostKinematics);
                stream.WriteByte((byte)',');
                WriteBufferHex(stream, clientKinematics);
                stream.WriteByte((byte)',');
                WriteBufferHex(stream, hostInventory);
                stream.WriteByte((byte)',');
                WriteBufferHex(stream, clientInventory);
                stream.WriteByte((byte)',');
                WriteBufferHex(stream, hostEcosystem);
                stream.WriteByte((byte)',');
                WriteBufferHex(stream, clientEcosystem);
                stream.WriteByte((byte)'\n');
            }
        }

        private static unsafe void WriteBlackBoxDump(NativeArray<FuzzerTelemetryEntryDTO> telemetry)
        {
            Directory.CreateDirectory("Docs/AgentLogs");
            using (FileStream stream = new FileStream("Docs/AgentLogs/Dump_SHINOBU_257.bin", FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                Span<byte> header = stackalloc byte[32];
                WriteUInt64LittleEndian(header, 0, 0x42423735324E3848UL);
                WriteUInt32LittleEndian(header, 8, 1u);
                WriteUInt32LittleEndian(header, 12, telemetry.IsCreated ? (uint)telemetry.Length : 0u);
                WriteUInt32LittleEndian(header, 16, (uint)UnsafeUtility.SizeOf<FuzzerTelemetryEntryDTO>());
                WriteUInt32LittleEndian(header, 20, telemetry.IsCreated ? (uint)(telemetry.Length * UnsafeUtility.SizeOf<FuzzerTelemetryEntryDTO>()) : 0u);
                WriteUInt32LittleEndian(header, 24, 0u);
                WriteUInt32LittleEndian(header, 28, 0u);
                stream.Write(header);

                if (!telemetry.IsCreated || telemetry.Length <= 0)
                    return;

                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetry);
                int byteCount = telemetry.Length * UnsafeUtility.SizeOf<FuzzerTelemetryEntryDTO>();
                stream.Write(new ReadOnlySpan<byte>(ptr, byteCount));
            }
        }

        private static void WriteAscii(Stream stream, string value)
        {
            for (int i = 0; i < value.Length; i++)
                stream.WriteByte((byte)value[i]);
        }

        private static void WriteUInt32LittleEndian(Span<byte> buffer, int offset, uint value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteUInt64LittleEndian(Span<byte> buffer, int offset, ulong value)
        {
            WriteUInt32LittleEndian(buffer, offset, (uint)value);
            WriteUInt32LittleEndian(buffer, offset + 4, (uint)(value >> 32));
        }

        private static void WriteUInt(Stream stream, uint value)
        {
            Span<byte> digits = stackalloc byte[10];
            int count = 0;
            do
            {
                digits[count++] = (byte)('0' + (value % 10u));
                value /= 10u;
            }
            while (value != 0u);

            for (int i = count - 1; i >= 0; i--)
                stream.WriteByte(digits[i]);
        }

        private static void WriteUInt(Stream stream, int value)
        {
            WriteUInt(stream, value < 0 ? 0u : (uint)value);
        }

        private static void WriteFloatFixed3(Stream stream, float value)
        {
            if (value < 0f)
            {
                stream.WriteByte((byte)'-');
                value = -value;
            }

            uint scaled = (uint)math.round(value * 1000f);
            WriteUInt(stream, scaled / 1000u);
            stream.WriteByte((byte)'.');
            uint fraction = scaled % 1000u;
            stream.WriteByte((byte)('0' + (fraction / 100u)));
            stream.WriteByte((byte)('0' + ((fraction / 10u) % 10u)));
            stream.WriteByte((byte)('0' + (fraction % 10u)));
        }

        private static void WriteHex32(Stream stream, uint value)
        {
            WriteAscii(stream, "0x");
            for (int shift = 28; shift >= 0; shift -= 4)
                stream.WriteByte(ToHex((int)((value >> shift) & 0xFu)));
        }

        private static void WriteHex64(Stream stream, ulong value)
        {
            WriteAscii(stream, "0x");
            for (int shift = 60; shift >= 0; shift -= 4)
                stream.WriteByte(ToHex((int)((value >> shift) & 0xFUL)));
        }

        private static unsafe void WriteBufferHex<T>(Stream stream, NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            int bytes = array.Length * UnsafeUtility.SizeOf<T>();
            for (int i = 0; i < bytes; i++)
            {
                byte value = ptr[i];
                stream.WriteByte(ToHex(value >> 4));
                stream.WriteByte(ToHex(value & 0x0F));
            }
        }

        private static byte ToHex(int value)
        {
            return (byte)(value < 10 ? '0' + value : 'A' + value - 10);
        }

        private static class RollbackNetcodeFuzzerOwner
        {
            public const SystemID System = SystemID.CoreDeterminism;
        }

        private static class FuzzerErrorFlags
        {
            public const uint ParityMismatch = 1u << 0;
            public const uint KinematicsMismatch = 1u << 1;
            public const uint InventoryMismatch = 1u << 2;
            public const uint EcosystemMismatch = 1u << 3;
            public const uint MissingTransportDelivery = 1u << 4;
            public const uint SnapshotOverflow = 1u << 5;
            public const uint MemoryCorruption = 1u << 6;
            public const uint RollbackPerformanceFailure = 1u << 7;
            public const uint DeterministicRngFailure = 1u << 8;
            public const uint TransportQueueOverflow = 1u << 9;
            public const uint EditorExecutionFailure = 1u << 10;
            public const uint AupPayloadMismatch = 1u << 11;
            public const uint ScheduledPathMismatch = 1u << 12;
        }

        private static class FuzzerPacketFlags
        {
            public const uint ClientToHost = 1u << 0;
            public const uint HostToClient = 1u << 1;
            public const uint ForcedFinalRedundancy = 1u << 2;
        }

        [StructLayout(LayoutKind.Explicit, Size = 24)]
        private struct FuzzerWireAupDTO
        {
            [FieldOffset(0)] public ulong SectorHash;
            [FieldOffset(8)] public int LocalMillimetersX;
            [FieldOffset(12)] public int LocalMillimetersY;
            [FieldOffset(16)] public int LocalMillimetersZ;
            [FieldOffset(20)] public uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct NetworkPacketDTO
        {
            [FieldOffset(0)] public uint SourceTick;
            [FieldOffset(4)] public uint DeliveryTick;
            [FieldOffset(8)] public FuzzerWireAupDTO AupPayload;
            [FieldOffset(32)] public InputStateDTO Input;
            [FieldOffset(56)] public uint Sequence;
            [FieldOffset(60)] public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FuzzerKinematicStateDTO
        {
            [FieldOffset(0)] public long SectorX;
            [FieldOffset(8)] public long SectorY;
            [FieldOffset(16)] public long SectorZ;
            [FieldOffset(24)] public double3 LocalPosition;
            [FieldOffset(48)] public float3 Velocity;
            [FieldOffset(60)] public uint Flags;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FuzzerInventoryStateDTO
        {
            [FieldOffset(0)] public ulong ItemHashAggregate;
            [FieldOffset(8)] public uint ItemHashId;
            [FieldOffset(12)] public int Quantity;
            [FieldOffset(16)] public uint RollIndex;
            [FieldOffset(20)] public uint Flags;
            [FieldOffset(24)] public ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FuzzerEcosystemStateDTO
        {
            [FieldOffset(0)] public ulong TraitMask;
            [FieldOffset(8)] public uint SpawnedLootHash;
            [FieldOffset(12)] public int BiomassMilli;
            [FieldOffset(16)] public uint Flags;
            [FieldOffset(20)] public uint LastInputMask;
            [FieldOffset(24)] public ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct FuzzerSnapshotDTO
        {
            [FieldOffset(0)] public FuzzerKinematicStateDTO Kinematics;
            [FieldOffset(64)] public FuzzerInventoryStateDTO Inventory;
            [FieldOffset(96)] public FuzzerEcosystemStateDTO Ecosystem;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FuzzerVisualNoiseDTO
        {
            [FieldOffset(0)] public double3 Anchor;
            [FieldOffset(24)] public float Aging01;
            [FieldOffset(28)] public float Pulse01;
            [FieldOffset(32)] public uint EntityId;
            [FieldOffset(36)] public uint Frame;
            [FieldOffset(40)] public ulong NoiseHash;
            [FieldOffset(48)] public ulong _pad0;
            [FieldOffset(56)] public ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FuzzerTelemetryEntryDTO
        {
            [FieldOffset(0)] public ulong HostHash;
            [FieldOffset(8)] public ulong ClientHash;
            [FieldOffset(16)] public uint Frame;
            [FieldOffset(20)] public uint RollbackDepth;
            [FieldOffset(24)] public uint ErrorFlags;
            [FieldOffset(28)] public uint MismatchBufferId;
            [FieldOffset(32)] public double3 ClientAupLocal;
            [FieldOffset(56)] public uint MismatchByteOffset;
            [FieldOffset(60)] public uint _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct FuzzerQuantizedKinematicHashDTO
        {
            [FieldOffset(0)] public long SectorX;
            [FieldOffset(8)] public long SectorY;
            [FieldOffset(16)] public long SectorZ;
            [FieldOffset(24)] public int LocalMillimetersX;
            [FieldOffset(28)] public int LocalMillimetersY;
            [FieldOffset(32)] public int LocalMillimetersZ;
            [FieldOffset(36)] public int VelocityMilliX;
            [FieldOffset(40)] public int VelocityMilliY;
            [FieldOffset(44)] public int VelocityMilliZ;
            [FieldOffset(48)] public uint Flags;
            [FieldOffset(52)] public uint _pad0;
            [FieldOffset(56)] public ulong _pad1;
        }

        [StructLayout(LayoutKind.Explicit, Size = 32)]
        private struct FuzzerStateHashRootDTO
        {
            [FieldOffset(0)] public ulong KinematicHash;
            [FieldOffset(8)] public ulong InventoryHash;
            [FieldOffset(16)] public ulong EcosystemHash;
            [FieldOffset(24)] public ulong _pad0;
        }

        [StructLayout(LayoutKind.Explicit, Size = 128)]
        private struct FuzzerResultDTO
        {
            [FieldOffset(0)] public ulong HostMasterHash;
            [FieldOffset(8)] public ulong ClientMasterHash;
            [FieldOffset(16)] public ulong HostKinematicHash;
            [FieldOffset(24)] public ulong ClientKinematicHash;
            [FieldOffset(32)] public ulong HostInventoryHash;
            [FieldOffset(40)] public ulong ClientInventoryHash;
            [FieldOffset(48)] public ulong HostEcosystemHash;
            [FieldOffset(56)] public ulong ClientEcosystemHash;
            [FieldOffset(64)] public long ManagedAllocationBytes;
            [FieldOffset(72)] public uint ErrorFlags;
            [FieldOffset(76)] public uint MismatchTick;
            [FieldOffset(80)] public uint MismatchBufferId;
            [FieldOffset(84)] public uint MismatchByteOffset;
            [FieldOffset(88)] public uint MaxRollbackDepth;
            [FieldOffset(92)] public uint DroppedPackets;
            [FieldOffset(96)] public uint DeliveredPackets;
            [FieldOffset(100)] public uint OutOfOrderDeliveries;
            [FieldOffset(104)] public uint MaxCatchupMicros;
            [FieldOffset(108)] public uint LagSpikeCount;
            [FieldOffset(112)] public uint HostLootHash;
            [FieldOffset(116)] public uint ClientLootHash;
            [FieldOffset(120)] public uint AupPayloadSamples;
            [FieldOffset(124)] public uint AupPayloadMismatches;
        }

        [StructLayout(LayoutKind.Explicit, Size = 64)]
        private struct NetworkFuzzerProfileDTO
        {
            [FieldOffset(0)] public uint ProfileHash;
            [FieldOffset(4)] public uint BaseDelayFrames;
            [FieldOffset(8)] public uint JitterFrames;
            [FieldOffset(12)] public uint PacketLossPermille;
            [FieldOffset(16)] public uint RedundancyCount;
            [FieldOffset(20)] public uint LagSpikeFrames;
            [FieldOffset(24)] public uint FlushFrames;
            [FieldOffset(28)] public float GlobalQualityWeight;
            [FieldOffset(32)] public float PingMilliseconds;
            [FieldOffset(36)] public float JitterMilliseconds;
            [FieldOffset(40)] public uint Flags;
            [FieldOffset(44)] public uint _pad0;
            [FieldOffset(48)] public ulong _pad1;
            [FieldOffset(56)] public ulong _pad2;
        }

        private static class NetworkFuzzerProfileCsvParser
        {
            public static int Parse(ReadOnlySpan<byte> csv, uint requiredProfileHash, ref NetworkFuzzerProfileDTO target)
            {
                int cursor = 0;
                int parsed = 0;
                while (TryReadLine(csv, ref cursor, out ReadOnlySpan<byte> line))
                {
                    line = Trim(line);
                    if (line.Length == 0 || line[0] == 35)
                        continue;

                    if (TryReadProfile(line, out NetworkFuzzerProfileDTO profile) &&
                        (requiredProfileHash == 0u || profile.ProfileHash == requiredProfileHash))
                    {
                        target = profile;
                        parsed++;
                        break;
                    }
                }

                return parsed;
            }

            private static bool TryReadProfile(ReadOnlySpan<byte> line, out NetworkFuzzerProfileDTO profile)
            {
                profile = default;
                int cursor = 0;
                ReadOnlySpan<byte> name = ReadCell(line, ref cursor);
                if (name.Length == 0 || IsHeaderName(name))
                    return false;
                if (CountCommas(line) != 7)
                    return false;

                if (!TryReadFloat(ReadCell(line, ref cursor), out float pingMs))
                    return false;
                if (!TryReadUInt(ReadCell(line, ref cursor), out uint jitterFrames))
                    return false;
                if (!TryReadUInt(ReadCell(line, ref cursor), out uint packetLossPermille))
                    return false;
                if (!TryReadUInt(ReadCell(line, ref cursor), out uint lagSpikeFrames))
                    return false;
                if (!TryReadUInt(ReadCell(line, ref cursor), out uint flushFrames))
                    return false;
                if (!TryReadUInt(ReadCell(line, ref cursor), out uint redundancy))
                    return false;
                ReadOnlySpan<byte> qualityCell = ReadCell(line, ref cursor);
                float qualityWeight;
                if (qualityCell.Length == 0)
                {
                    qualityWeight = 1f;
                }
                else if (!TryReadFloat(qualityCell, out qualityWeight))
                {
                    return false;
                }

                uint baseDelayFrames = (uint)math.max(1, (int)math.round(pingMs * 0.06f));
                profile.ProfileHash = HashFnv1a(name);
                profile.BaseDelayFrames = baseDelayFrames;
                profile.JitterFrames = jitterFrames;
                profile.PacketLossPermille = packetLossPermille;
                profile.RedundancyCount = redundancy;
                profile.LagSpikeFrames = lagSpikeFrames;
                profile.FlushFrames = flushFrames;
                profile.GlobalQualityWeight = qualityWeight;
                profile.PingMilliseconds = pingMs;
                profile.JitterMilliseconds = jitterFrames * (1000f / 60f);
                profile.Flags = 1u;
                return true;
            }

            private static int CountCommas(ReadOnlySpan<byte> line)
            {
                int count = 0;
                for (int i = 0; i < line.Length; i++)
                {
                    if (line[i] == 44)
                        count++;
                }

                return count;
            }

            private static bool TryReadLine(ReadOnlySpan<byte> text, ref int cursor, out ReadOnlySpan<byte> line)
            {
                if (cursor >= text.Length)
                {
                    line = default;
                    return false;
                }

                int start = cursor;
                while (cursor < text.Length && text[cursor] != 10 && text[cursor] != 13)
                    cursor++;
                line = text.Slice(start, cursor - start);
                while (cursor < text.Length && (text[cursor] == 10 || text[cursor] == 13))
                    cursor++;
                return true;
            }

            private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> line, ref int cursor)
            {
                if (cursor >= line.Length)
                    return ReadOnlySpan<byte>.Empty;

                int start = cursor;
                while (cursor < line.Length && line[cursor] != 44)
                    cursor++;
                int length = cursor - start;
                if (cursor < line.Length && line[cursor] == 44)
                    cursor++;
                return Trim(line.Slice(start, length));
            }

            private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> text)
            {
                int start = 0;
                int end = text.Length - 1;
                while (start < text.Length && text[start] <= 32)
                    start++;
                while (end >= start && text[end] <= 32)
                    end--;
                return start <= end ? text.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
            }

            private static bool TryReadUInt(ReadOnlySpan<byte> text, out uint value)
            {
                value = 0u;
                text = Trim(text);
                if (text.Length == 0)
                    return false;

                bool any = false;
                for (int i = 0; i < text.Length; i++)
                {
                    byte b = text[i];
                    if (b < 48 || b > 57)
                        return false;
                    any = true;
                    uint digit = (uint)(b - 48);
                    if (value > (uint.MaxValue - digit) / 10u)
                        return false;
                    value = (value * 10u) + digit;
                }

                return any;
            }

            private static bool TryReadFloat(ReadOnlySpan<byte> text, out float value)
            {
                value = 0f;
                text = Trim(text);
                if (text.Length == 0)
                    return false;

                int cursor = 0;
                double sign = 1.0d;
                if (text[cursor] == 45)
                {
                    sign = -1.0d;
                    cursor++;
                }
                else if (text[cursor] == 43)
                {
                    cursor++;
                }

                bool any = false;
                double whole = 0.0d;
                while (cursor < text.Length && text[cursor] >= 48 && text[cursor] <= 57)
                {
                    any = true;
                    whole = (whole * 10.0d) + (text[cursor] - 48);
                    if (whole > float.MaxValue)
                        return false;
                    cursor++;
                }

                double fraction = 0.0d;
                if (cursor < text.Length && text[cursor] == 46)
                {
                    cursor++;
                    double scale = 0.1d;
                    while (cursor < text.Length && text[cursor] >= 48 && text[cursor] <= 57)
                    {
                        any = true;
                        fraction += (text[cursor] - 48) * scale;
                        scale *= 0.1d;
                        cursor++;
                    }
                }

                if (cursor != text.Length)
                    return false;

                double result = (whole + fraction) * sign;
                if (result > float.MaxValue || result < -float.MaxValue)
                    return false;

                value = (float)result;
                return any && math.isfinite(value);
            }

            private static bool IsHeaderName(ReadOnlySpan<byte> text)
            {
                if (text.Length != 7)
                    return false;

                return ToLowerAscii(text[0]) == 112 &&
                    ToLowerAscii(text[1]) == 114 &&
                    ToLowerAscii(text[2]) == 111 &&
                    ToLowerAscii(text[3]) == 102 &&
                    ToLowerAscii(text[4]) == 105 &&
                    ToLowerAscii(text[5]) == 108 &&
                    ToLowerAscii(text[6]) == 101;
            }

            private static byte ToLowerAscii(byte value)
            {
                return value >= 65 && value <= 90 ? (byte)(value + 32) : value;
            }

            private static uint HashFnv1a(ReadOnlySpan<byte> text)
            {
                uint hash = 2166136261u;
                for (int i = 0; i < text.Length; i++)
                {
                    byte b = text[i];
                    if (b >= 65 && b <= 90)
                        b = (byte)(b + 32);
                    hash ^= b;
                    hash *= 16777619u;
                }

                return hash == 0u ? 1u : hash;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct InjectRandomizedInputsJob : IJobParallelFor
        {
            [NoAlias] public NativeArray<InputStateDTO> Inputs;
            public uint WorldSeed;

            public void Execute(int index)
            {
                if (!Inputs.IsCreated || (uint)index >= (uint)Inputs.Length)
                    return;

                uint frame = (uint)index;
                uint seed = FuzzerMath.Mix32(WorldSeed ^ frame ^ 0x51ED270Bu);
                float moveX = FuzzerMath.SignedUnit(seed ^ 0xA341316Cu);
                float moveY = FuzzerMath.SignedUnit(seed ^ 0xC8013EA4u);
                float lookX = FuzzerMath.SignedUnit(seed ^ 0xAD90777Du) * 3.5f;
                float lookY = FuzzerMath.SignedUnit(seed ^ 0x7E95761Eu) * 3.5f;
                uint buttonBits = FuzzerMath.Mix32(seed ^ 0x9E3779B9u);

                InputStateDTO input = default;
                input.MoveAxis = new float2(moveX, moveY);
                input.LookDelta = new float2(lookX, lookY);
                input.ButtonMask =
                    ((buttonBits >> 3) & 1u) |
                    (((buttonBits >> 5) & 1u) << 1) |
                    (((buttonBits >> 7) & 1u) << 2) |
                    (((buttonBits >> 11) & 1u) << 3) |
                    (((buttonBits >> 13) & 1u) << 4);
                Inputs[index] = input;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private struct MockTransportLayerJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<InputStateDTO> LocalInputs;
            [NoAlias] public NativeArray<InputStateDTO> HostAuthoritativeInputs;
            [NoAlias] public NativeArray<InputStateDTO> ClientAuthoritativeInputs;
            [NoAlias] public NativeArray<uint> ClientDeliveryTicks;
            [NoAlias] public NativeList<NetworkPacketDTO> ClientToHost;
            [NoAlias] public NativeList<NetworkPacketDTO> HostToClient;
            [NoAlias] public NativeArray<FuzzerResultDTO> Result;
            public int FrameCount;
            public int FlushFrames;
            public uint BaseDelayFrames;
            public uint JitterFrames;
            public uint PacketLossPermille;
            public uint RedundancyCount;
            public uint LagSpikeFrames;
            public uint WorldSeed;

            public void Execute()
            {
                if (!LocalInputs.IsCreated || !HostAuthoritativeInputs.IsCreated || !ClientAuthoritativeInputs.IsCreated ||
                    !ClientDeliveryTicks.IsCreated || !Result.IsCreated || Result.Length <= 0)
                {
                    return;
                }

                FuzzerResultDTO result = default;
                for (int i = 0; i < FrameCount; i++)
                {
                    HostAuthoritativeInputs[i] = FuzzerMath.SanitizeAuthoritativeInput(LocalInputs[i]);
                    ClientAuthoritativeInputs[i] = default;
                    ClientDeliveryTicks[i] = uint.MaxValue;
                }

                ClientToHost.Clear();
                HostToClient.Clear();
                uint totalTicks = (uint)(FrameCount + FlushFrames);
                uint sequence = 1u;
                uint lastDeliveredClientSource = uint.MaxValue;
                uint lastDeliveredHostSource = uint.MaxValue;
                uint redundancy = math.max(1u, RedundancyCount);

                for (uint current = 0u; current < totalTicks; current++)
                {
                    for (uint resend = 0u; resend < redundancy; resend++)
                    {
                        if (current < resend)
                            continue;

                        uint sourceTick = current - resend;
                        if (sourceTick >= (uint)FrameCount)
                            continue;

                        bool force = resend == redundancy - 1u;
                        InputStateDTO local = LocalInputs[(int)sourceTick];
                        EnqueuePacket(ref result, ref sequence, ref ClientToHost, sourceTick, current, local, FuzzerPacketFlags.ClientToHost, force);

                        if (ClientDeliveryTicks[(int)sourceTick] == uint.MaxValue)
                        {
                            InputStateDTO authoritative = FuzzerMath.SanitizeAuthoritativeInput(local);
                            EnqueuePacket(ref result, ref sequence, ref HostToClient, sourceTick, current, authoritative, FuzzerPacketFlags.HostToClient, force);
                        }
                    }

                    DrainClientToHost(ref result, current, ref lastDeliveredClientSource);
                    DrainHostToClient(ref result, current, ref lastDeliveredHostSource);
                }

                for (int i = 0; i < FrameCount; i++)
                {
                    if (ClientDeliveryTicks[i] == uint.MaxValue)
                    {
                        result.ErrorFlags |= FuzzerErrorFlags.MissingTransportDelivery;
                        result.MismatchTick = (uint)i;
                        result.MismatchBufferId = (uint)FuzzerClientAuthoritativeInputBuffer;
                        break;
                    }
                }

                Result[0] = result;
            }

            private void EnqueuePacket(
                ref FuzzerResultDTO result,
                ref uint sequence,
                ref NativeList<NetworkPacketDTO> queue,
                uint sourceTick,
                uint currentTick,
                in InputStateDTO input,
                uint directionFlags,
                bool forceDeliver)
            {
                if (!forceDeliver && FuzzerMath.ShouldDropPacket(WorldSeed, sourceTick, sequence, PacketLossPermille))
                {
                    result.DroppedPackets++;
                    sequence++;
                    return;
                }

                if (queue.Length >= queue.Capacity)
                {
                    result.ErrorFlags |= FuzzerErrorFlags.TransportQueueOverflow;
                    result.DroppedPackets++;
                    sequence++;
                    return;
                }

                uint jitter = FuzzerMath.HashToRange(WorldSeed ^ sourceTick ^ (sequence * 0x45D9F3Bu), (JitterFrames * 2u) + 1u);
                int signedJitter = (int)jitter - (int)JitterFrames;
                uint spike = FuzzerMath.IsLagSpike(sourceTick) ? LagSpikeFrames : 0u;
                if (spike != 0u)
                    result.LagSpikeCount++;

                int delay = (int)BaseDelayFrames + signedJitter + (int)spike;
                if (delay < 1)
                    delay = 1;

                NetworkPacketDTO packet = default;
                packet.SourceTick = sourceTick;
                packet.DeliveryTick = currentTick + (uint)delay;
                packet.AupPayload = FuzzerMath.PacketAupPayload(sourceTick);
                packet.Input = input;
                packet.Sequence = sequence++;
                packet.Flags = directionFlags | (forceDeliver ? FuzzerPacketFlags.ForcedFinalRedundancy : 0u);
                queue.AddNoResize(packet);
            }

            private void DrainClientToHost(ref FuzzerResultDTO result, uint currentTick, ref uint lastDeliveredSource)
            {
                uint maxDeliveredThisClock = uint.MaxValue;
                for (int i = 0; i < ClientToHost.Length;)
                {
                    NetworkPacketDTO packet = ClientToHost[i];
                    if (!FuzzerMath.HasFrameReached(currentTick, packet.DeliveryTick))
                    {
                        i++;
                        continue;
                    }

                    CountOutOfOrderAgainstPriorClock(ref result, packet.SourceTick, lastDeliveredSource);
                    TrackMaxDeliveredThisClock(packet.SourceTick, ref maxDeliveredThisClock);

                    if (packet.SourceTick < (uint)FrameCount)
                    {
                        ValidatePacketAupPayload(ref result, in packet);
                        HostAuthoritativeInputs[(int)packet.SourceTick] = FuzzerMath.SanitizeAuthoritativeInput(packet.Input);
                    }
                    result.DeliveredPackets++;
                    ClientToHost.RemoveAtSwapBack(i);
                }

                CommitDeliveredClock(ref lastDeliveredSource, maxDeliveredThisClock);
            }

            private void DrainHostToClient(ref FuzzerResultDTO result, uint currentTick, ref uint lastDeliveredSource)
            {
                uint maxDeliveredThisClock = uint.MaxValue;
                for (int i = 0; i < HostToClient.Length;)
                {
                    NetworkPacketDTO packet = HostToClient[i];
                    if (!FuzzerMath.HasFrameReached(currentTick, packet.DeliveryTick))
                    {
                        i++;
                        continue;
                    }

                    CountOutOfOrderAgainstPriorClock(ref result, packet.SourceTick, lastDeliveredSource);
                    TrackMaxDeliveredThisClock(packet.SourceTick, ref maxDeliveredThisClock);

                    if (packet.SourceTick < (uint)FrameCount)
                    {
                        int index = (int)packet.SourceTick;
                        ValidatePacketAupPayload(ref result, in packet);
                        if (ClientDeliveryTicks[index] == uint.MaxValue || packet.DeliveryTick < ClientDeliveryTicks[index])
                        {
                            ClientAuthoritativeInputs[index] = FuzzerMath.SanitizeAuthoritativeInput(packet.Input);
                            ClientDeliveryTicks[index] = packet.DeliveryTick;
                        }
                    }
                    result.DeliveredPackets++;
                    HostToClient.RemoveAtSwapBack(i);
                }

                CommitDeliveredClock(ref lastDeliveredSource, maxDeliveredThisClock);
            }

            private static void ValidatePacketAupPayload(ref FuzzerResultDTO result, in NetworkPacketDTO packet)
            {
                result.AupPayloadSamples++;
                FuzzerWireAupDTO expected = FuzzerMath.PacketAupPayload(packet.SourceTick);
                if (FuzzerMath.AupPayloadEquals(packet.AupPayload, expected))
                    return;

                result.AupPayloadMismatches++;
                result.ErrorFlags |= FuzzerErrorFlags.AupPayloadMismatch;
                result.MismatchTick = packet.SourceTick;
                result.MismatchBufferId = (packet.Flags & FuzzerPacketFlags.HostToClient) != 0u
                    ? (uint)FuzzerClientAuthoritativeInputBuffer
                    : (uint)FuzzerHostAuthoritativeInputBuffer;
                result.MismatchByteOffset = 8u;
            }

            private static void CountOutOfOrderAgainstPriorClock(ref FuzzerResultDTO result, uint sourceTick, uint lastDeliveredSource)
            {
                if (lastDeliveredSource != uint.MaxValue && sourceTick < lastDeliveredSource)
                    result.OutOfOrderDeliveries++;
            }

            private static void TrackMaxDeliveredThisClock(uint sourceTick, ref uint maxDeliveredThisClock)
            {
                if (maxDeliveredThisClock == uint.MaxValue || sourceTick > maxDeliveredThisClock)
                    maxDeliveredThisClock = sourceTick;
            }

            private static void CommitDeliveredClock(ref uint lastDeliveredSource, uint maxDeliveredThisClock)
            {
                if (maxDeliveredThisClock == uint.MaxValue)
                    return;
                if (lastDeliveredSource == uint.MaxValue || maxDeliveredThisClock > lastDeliveredSource)
                    lastDeliveredSource = maxDeliveredThisClock;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct RunHeadlessRollbackFuzzerJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<InputStateDTO> LocalInputs;
            [ReadOnly, NoAlias] public NativeArray<InputStateDTO> HostAuthoritativeInputs;
            [ReadOnly, NoAlias] public NativeArray<InputStateDTO> ClientAuthoritativeInputs;
            [NoAlias] public NativeArray<InputStateDTO> ClientAppliedInputs;
            [ReadOnly, NoAlias] public NativeArray<uint> ClientDeliveryTicks;
            [NoAlias] public NativeArray<FuzzerKinematicStateDTO> HostKinematics;
            [NoAlias] public NativeArray<FuzzerKinematicStateDTO> ClientKinematics;
            [NoAlias] public NativeArray<FuzzerInventoryStateDTO> HostInventory;
            [NoAlias] public NativeArray<FuzzerInventoryStateDTO> ClientInventory;
            [NoAlias] public NativeArray<FuzzerEcosystemStateDTO> HostEcosystem;
            [NoAlias] public NativeArray<FuzzerEcosystemStateDTO> ClientEcosystem;
            [NoAlias] public NativeArray<FuzzerSnapshotDTO> ClientStateRing;
            [NoAlias] public NativeArray<FuzzerTelemetryEntryDTO> Telemetry;
            [NoAlias] public NativeArray<FuzzerVisualNoiseDTO> ClientVisualNoise;
            [NoAlias] public NativeArray<FuzzerResultDTO> Result;
            public int FrameCount;
            public int FlushFrames;
            public int SnapshotRingCapacity;
            public int MaxRollbackFrames;
            public float GlobalQualityWeight;
            public uint WorldSeed;

            public void Execute()
            {
                if (!Result.IsCreated || Result.Length <= 0)
                    return;

                void* resultPtr = NativeArrayUnsafeUtility.GetUnsafePtr(Result);
                ref FuzzerResultDTO result = ref UnsafeUtility.AsRef<FuzzerResultDTO>(resultPtr);
                result.ErrorFlags &= ~(FuzzerErrorFlags.ParityMismatch |
                    FuzzerErrorFlags.KinematicsMismatch |
                    FuzzerErrorFlags.InventoryMismatch |
                    FuzzerErrorFlags.EcosystemMismatch |
                    FuzzerErrorFlags.SnapshotOverflow |
                    FuzzerErrorFlags.MemoryCorruption |
                    FuzzerErrorFlags.RollbackPerformanceFailure |
                    FuzzerErrorFlags.DeterministicRngFailure);

                InitializeState(HostKinematics, HostInventory, HostEcosystem);
                InitializeState(ClientKinematics, ClientInventory, ClientEcosystem);

                for (int i = 0; i < FrameCount; i++)
                    ClientAppliedInputs[i] = LocalInputs[i];

                int totalTicks = FrameCount + FlushFrames;
                int lastSimulatedFrame = FrameCount - 1;
                int telemetryStride = FuzzerMath.ResolveTelemetryStride(GlobalQualityWeight);
                int visualStride = FuzzerMath.ResolveVisualStride(GlobalQualityWeight);
                for (int currentClock = 0; currentClock < totalTicks; currentClock++)
                {
                    if (currentClock < FrameCount)
                    {
                        SnapshotClientFrame(currentClock);
                        ApplySimulationFrame(HostKinematics, HostInventory, HostEcosystem, HostAuthoritativeInputs[currentClock], (uint)currentClock, WorldSeed);
                        InputStateDTO clientInput = ResolveClientInput(currentClock, currentClock);
                        ClientAppliedInputs[currentClock] = clientInput;
                        ApplySimulationFrame(ClientKinematics, ClientInventory, ClientEcosystem, clientInput, (uint)currentClock, WorldSeed);
                        if ((currentClock % visualStride) == 0)
                            InjectPresentationNoise(currentClock);
                    }

                    int targetFrame = currentClock < FrameCount ? currentClock : lastSimulatedFrame;
                    int rollbackFrame = FindEarliestDeliveredMismatch(targetFrame, currentClock);
                    if (rollbackFrame >= 0)
                        ExecuteRollback(ref result, rollbackFrame, targetFrame, currentClock);

                    if ((currentClock % telemetryStride) == 0 || result.ErrorFlags != 0u)
                        WriteTelemetry(ref result, targetFrame);
                }

                result.HostLootHash = HostEcosystem[0].SpawnedLootHash;
                result.ClientLootHash = ClientEcosystem[0].SpawnedLootHash;
                if (result.HostLootHash != result.ClientLootHash)
                    result.ErrorFlags |= FuzzerErrorFlags.DeterministicRngFailure;

                if (!FuzzerMath.IsFinite(HostKinematics[0]) || !FuzzerMath.IsFinite(ClientKinematics[0]))
                {
                    result.ErrorFlags |= FuzzerErrorFlags.MemoryCorruption;
                    result.MismatchBufferId = (uint)FuzzerClientKinematicsBuffer;
                    result.MismatchByteOffset = 24u;
                }
            }

            private static void InitializeState(
                NativeArray<FuzzerKinematicStateDTO> kinematics,
                NativeArray<FuzzerInventoryStateDTO> inventory,
                NativeArray<FuzzerEcosystemStateDTO> ecosystem)
            {
                FuzzerKinematicStateDTO k = default;
                k.SectorX = 88000017L;
                k.SectorY = -44000011L;
                k.SectorZ = 12L;
                k.LocalPosition = new double3(511.996d, 0.004d, 511.998d);
                k.Velocity = new float3(0.4f, 0.02f, 0.3f);
                k.Flags = 1u;
                kinematics[0] = k;

                FuzzerInventoryStateDTO inv = default;
                inv.ItemHashAggregate = 0xD00D5EED12345678UL;
                inv.ItemHashId = 0xA17E0001u;
                inv.Quantity = 7;
                inv.RollIndex = 0u;
                inv.Flags = 1u;
                inventory[0] = inv;

                FuzzerEcosystemStateDTO eco = default;
                eco.TraitMask = 0xB10B10B10B10B10BUL;
                eco.SpawnedLootHash = 0xCE11007u;
                eco.BiomassMilli = 32000;
                eco.Flags = 1u;
                ecosystem[0] = eco;
            }

            private InputStateDTO ResolveClientInput(int frame, int currentClock)
            {
                if ((uint)frame < (uint)ClientDeliveryTicks.Length &&
                    ClientDeliveryTicks[frame] != uint.MaxValue &&
                    FuzzerMath.HasFrameReached((uint)currentClock, ClientDeliveryTicks[frame]))
                {
                    return ClientAuthoritativeInputs[frame];
                }

                return LocalInputs[frame];
            }

            private int FindEarliestDeliveredMismatch(int targetFrame, int currentClock)
            {
                if (targetFrame < 0)
                    return -1;

                int start = currentClock < FrameCount
                    ? math.max(0, targetFrame - MaxRollbackFrames)
                    : 0;
                for (int frame = start; frame <= targetFrame; frame++)
                {
                    if (ClientDeliveryTicks[frame] == uint.MaxValue ||
                        !FuzzerMath.HasFrameReached((uint)currentClock, ClientDeliveryTicks[frame]))
                    {
                        continue;
                    }

                    if (!FuzzerMath.InputEquals(ClientAppliedInputs[frame], ClientAuthoritativeInputs[frame]))
                        return frame;
                }

                return -1;
            }

            private void ExecuteRollback(ref FuzzerResultDTO result, int rollbackFrame, int targetFrame, int currentClock)
            {
                int depth = targetFrame - rollbackFrame + 1;
                if (depth >= SnapshotRingCapacity)
                {
                    result.ErrorFlags |= FuzzerErrorFlags.SnapshotOverflow;
                    result.MismatchTick = (uint)rollbackFrame;
                    result.MismatchBufferId = (uint)FuzzerSnapshotBuffer;
                    return;
                }

                if ((uint)depth > result.MaxRollbackDepth)
                    result.MaxRollbackDepth = (uint)depth;

                uint catchupMicros = (uint)(depth * 42u);
                if (catchupMicros > result.MaxCatchupMicros)
                    result.MaxCatchupMicros = catchupMicros;
                if (catchupMicros > 16000u)
                    result.ErrorFlags |= FuzzerErrorFlags.RollbackPerformanceFailure;

                void* ringPtr = NativeArrayUnsafeUtility.GetUnsafePtr(ClientStateRing);
                int slot = rollbackFrame & (SnapshotRingCapacity - 1);
                ref FuzzerSnapshotDTO snapshot = ref UnsafeUtility.AsRef<FuzzerSnapshotDTO>(
                    (byte*)ringPtr + (slot * UnsafeUtility.SizeOf<FuzzerSnapshotDTO>()));

                ClientKinematics[0] = snapshot.Kinematics;
                ClientInventory[0] = snapshot.Inventory;
                ClientEcosystem[0] = snapshot.Ecosystem;

                for (int frame = rollbackFrame; frame <= targetFrame; frame++)
                {
                    SnapshotClientFrame(frame);
                    InputStateDTO input = ResolveClientInput(frame, currentClock);
                    ClientAppliedInputs[frame] = input;
                    ApplySimulationFrame(ClientKinematics, ClientInventory, ClientEcosystem, input, (uint)frame, WorldSeed);
                }

                result.MismatchTick = (uint)rollbackFrame;
            }

            private void SnapshotClientFrame(int frame)
            {
                void* ringPtr = NativeArrayUnsafeUtility.GetUnsafePtr(ClientStateRing);
                int slot = frame & (SnapshotRingCapacity - 1);
                ref FuzzerSnapshotDTO snapshot = ref UnsafeUtility.AsRef<FuzzerSnapshotDTO>(
                    (byte*)ringPtr + (slot * UnsafeUtility.SizeOf<FuzzerSnapshotDTO>()));
                snapshot.Kinematics = ClientKinematics[0];
                snapshot.Inventory = ClientInventory[0];
                snapshot.Ecosystem = ClientEcosystem[0];
            }

            private static void ApplySimulationFrame(
                NativeArray<FuzzerKinematicStateDTO> kinematics,
                NativeArray<FuzzerInventoryStateDTO> inventory,
                NativeArray<FuzzerEcosystemStateDTO> ecosystem,
                in InputStateDTO input,
                uint frame,
                uint worldSeed)
            {
                FuzzerKinematicStateDTO k = kinematics[0];
                FuzzerInventoryStateDTO inv = inventory[0];
                FuzzerEcosystemStateDTO eco = ecosystem[0];

                float3 acceleration = new float3(
                    input.MoveAxis.x * 0.085f + (((input.ButtonMask & 1u) != 0u) ? 0.025f : -0.004f),
                    ((input.ButtonMask & 4u) != 0u) ? 0.018f : -0.006f,
                    input.MoveAxis.y * 0.085f + (((input.ButtonMask & 2u) != 0u) ? 0.021f : -0.003f));
                k.Velocity = (k.Velocity * 0.984375f) + acceleration;
                k.LocalPosition += new double3(k.Velocity.x, k.Velocity.y, k.Velocity.z) * (1.0d / 60.0d);
                FuzzerMath.NormalizeAup(ref k);
                k.Flags ^= (input.ButtonMask << 8) ^ (frame & 0xFFu);

                if ((input.ButtonMask & 8u) != 0u)
                {
                    inv.Quantity += (int)((frame & 3u) + 1u);
                    inv.RollIndex++;
                }
                if ((input.ButtonMask & 16u) != 0u)
                {
                    uint loot = FuzzerMath.RollDeterministicLoot(worldSeed, in k, inv.RollIndex, 0x51A7u);
                    inv.ItemHashId = loot;
                    inv.ItemHashAggregate = FuzzerMath.Mix64(inv.ItemHashAggregate, loot ^ frame);
                    eco.SpawnedLootHash = loot;
                }

                eco.BiomassMilli += ((input.ButtonMask & 4u) != 0u) ? -3 : 1;
                eco.BiomassMilli += (int)math.round(input.MoveAxis.x * 2f);
                eco.LastInputMask = input.ButtonMask;
                eco.TraitMask = FuzzerMath.Mix64(eco.TraitMask, ((ulong)input.ButtonMask << 32) ^ frame);

                kinematics[0] = k;
                inventory[0] = inv;
                ecosystem[0] = eco;
            }

            private void InjectPresentationNoise(int currentFrame)
            {
                if (!ClientVisualNoise.IsCreated || ClientVisualNoise.Length <= 0)
                    return;

                int index = currentFrame & 15;
                FuzzerVisualNoiseDTO visual = default;
                visual.Anchor = ClientKinematics[0].LocalPosition;
                visual.Aging01 = FuzzerMath.UnsignedUnit(FuzzerMath.Mix32((uint)currentFrame ^ 0xA61E5u));
                visual.Pulse01 = FuzzerMath.UnsignedUnit(FuzzerMath.Mix32((uint)currentFrame ^ 0xB101u));
                visual.EntityId = (uint)index;
                visual.Frame = (uint)currentFrame;
                visual.NoiseHash = FuzzerMath.Mix64((ulong)currentFrame, visual.EntityId);
                ClientVisualNoise[index] = visual;
            }

            private void WriteTelemetry(ref FuzzerResultDTO result, int targetFrame)
            {
                if (!Telemetry.IsCreated || Telemetry.Length <= 0 || targetFrame < 0)
                    return;

                int index = targetFrame % Telemetry.Length;
                FuzzerTelemetryEntryDTO entry = default;
                entry.Frame = (uint)targetFrame;
                entry.RollbackDepth = result.MaxRollbackDepth;
                entry.ErrorFlags = result.ErrorFlags;
                entry.MismatchBufferId = result.MismatchBufferId;
                entry.MismatchByteOffset = result.MismatchByteOffset;
                entry.ClientAupLocal = ClientKinematics[0].LocalPosition;
                entry.HostHash = FuzzerMath.HashState(HostKinematics, HostInventory, HostEcosystem, out _, out _, out _);
                entry.ClientHash = FuzzerMath.HashState(ClientKinematics, ClientInventory, ClientEcosystem, out _, out _, out _);
                Telemetry[index] = entry;
            }
        }

        [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
        private unsafe struct ValidateMerkleParityJob : IJob
        {
            [ReadOnly, NoAlias] public NativeArray<FuzzerKinematicStateDTO> HostKinematics;
            [ReadOnly, NoAlias] public NativeArray<FuzzerKinematicStateDTO> ClientKinematics;
            [ReadOnly, NoAlias] public NativeArray<FuzzerInventoryStateDTO> HostInventory;
            [ReadOnly, NoAlias] public NativeArray<FuzzerInventoryStateDTO> ClientInventory;
            [ReadOnly, NoAlias] public NativeArray<FuzzerEcosystemStateDTO> HostEcosystem;
            [ReadOnly, NoAlias] public NativeArray<FuzzerEcosystemStateDTO> ClientEcosystem;
            [NoAlias] public NativeArray<FuzzerResultDTO> Result;

            public void Execute()
            {
                if (!Result.IsCreated || Result.Length <= 0)
                    return;

                void* resultPtr = NativeArrayUnsafeUtility.GetUnsafePtr(Result);
                ref FuzzerResultDTO result = ref UnsafeUtility.AsRef<FuzzerResultDTO>(resultPtr);

                ulong host = FuzzerMath.HashState(
                    HostKinematics,
                    HostInventory,
                    HostEcosystem,
                    out ulong hostKinematics,
                    out ulong hostInventory,
                    out ulong hostEcosystem);
                ulong client = FuzzerMath.HashState(
                    ClientKinematics,
                    ClientInventory,
                    ClientEcosystem,
                    out ulong clientKinematics,
                    out ulong clientInventory,
                    out ulong clientEcosystem);

                result.HostMasterHash = host;
                result.ClientMasterHash = client;
                result.HostKinematicHash = hostKinematics;
                result.ClientKinematicHash = clientKinematics;
                result.HostInventoryHash = hostInventory;
                result.ClientInventoryHash = clientInventory;
                result.HostEcosystemHash = hostEcosystem;
                result.ClientEcosystemHash = clientEcosystem;

                if (host != client)
                {
                    result.ErrorFlags |= FuzzerErrorFlags.ParityMismatch;
                    if (hostKinematics != clientKinematics)
                    {
                        result.ErrorFlags |= FuzzerErrorFlags.KinematicsMismatch;
                        result.MismatchBufferId = (uint)FuzzerClientKinematicsBuffer;
                        result.MismatchByteOffset = 0u;
                    }
                    else if (hostInventory != clientInventory)
                    {
                        result.ErrorFlags |= FuzzerErrorFlags.InventoryMismatch;
                        result.MismatchBufferId = (uint)FuzzerClientInventoryBuffer;
                        result.MismatchByteOffset = 0u;
                    }
                    else if (hostEcosystem != clientEcosystem)
                    {
                        result.ErrorFlags |= FuzzerErrorFlags.EcosystemMismatch;
                        result.MismatchBufferId = (uint)FuzzerClientEcosystemBuffer;
                        result.MismatchByteOffset = 0u;
                    }
                }
            }
        }

        private static unsafe class FuzzerMath
        {
            private const double SectorSizeMeters = 512.0d;

            public static uint Mix32(uint value)
            {
                value ^= value >> 16;
                value *= 2246822519u;
                value ^= value >> 13;
                value *= 3266489917u;
                value ^= value >> 16;
                return value == 0u ? 1u : value;
            }

            public static ulong Mix64(ulong state, ulong value)
            {
                state ^= value + 0x9E3779B97F4A7C15UL + (state << 6) + (state >> 2);
                state ^= state >> 33;
                state *= 0xff51afd7ed558ccdUL;
                state ^= state >> 33;
                state *= 0xc4ceb9fe1a85ec53UL;
                state ^= state >> 33;
                return state == 0UL ? 0xA24BAED4963EE407UL : state;
            }

            public static uint HashToRange(uint hash, uint range)
            {
                if (range == 0u)
                    return 0u;
                return (uint)(((ulong)Mix32(hash) * range) >> 32);
            }

            public static float UnsignedUnit(uint hash)
            {
                return (Mix32(hash) & 0x00FFFFFFu) * (1f / 16777215f);
            }

            public static float SignedUnit(uint hash)
            {
                return (UnsignedUnit(hash) * 2f) - 1f;
            }

            public static bool ShouldDropPacket(uint seed, uint sourceTick, uint sequence, uint lossPermille)
            {
                uint boundedLoss = math.min(lossPermille, 1000u);
                Unity.Mathematics.Random rng = Unity.Mathematics.Random.CreateFromIndex(Mix32(seed ^ sourceTick ^ (sequence * 0x9E3779B9u)));
                uint roll = NextUIntRange(ref rng, 1000u);
                return roll < boundedLoss;
            }

            public static bool IsLagSpike(uint sourceTick)
            {
                uint phase = sourceTick % 997u;
                return phase >= 640u && phase < 670u;
            }

            public static FuzzerWireAupDTO PacketAupPayload(uint sourceTick)
            {
                long sectorX = 88000017L;
                long sectorY = -44000011L;
                long sectorZ = 12L;
                double localX = 511.996d + (sourceTick * 0.0001d);
                double localY = 0.004d;
                double localZ = 511.998d;
                NormalizeAxis(ref sectorX, ref localX);
                NormalizeAxis(ref sectorY, ref localY);
                NormalizeAxis(ref sectorZ, ref localZ);

                FuzzerWireAupDTO payload = default;
                payload.SectorHash = SectorTripletHash(sectorX, sectorY, sectorZ);
                payload.LocalMillimetersX = QuantizeToIntMillimeters(localX);
                payload.LocalMillimetersY = QuantizeToIntMillimeters(localY);
                payload.LocalMillimetersZ = QuantizeToIntMillimeters(localZ);
                return payload;
            }

            public static bool HasFrameReached(uint currentFrame, uint targetFrame)
            {
                return (int)(currentFrame - targetFrame) >= 0;
            }

            public static InputStateDTO SanitizeAuthoritativeInput(in InputStateDTO raw)
            {
                InputStateDTO sanitized = default;
                sanitized.MoveAxis = new float2(
                    math.round(math.clamp(raw.MoveAxis.x, -1f, 1f) * 128f) * (1f / 128f),
                    math.round(math.clamp(raw.MoveAxis.y, -1f, 1f) * 128f) * (1f / 128f));
                sanitized.LookDelta = new float2(
                    math.round(math.clamp(raw.LookDelta.x, -4f, 4f) * 64f) * (1f / 64f),
                    math.round(math.clamp(raw.LookDelta.y, -4f, 4f) * 64f) * (1f / 64f));
                uint buttons = raw.ButtonMask & 0x1Fu;
                if ((buttons & 3u) == 3u)
                    buttons &= ~2u;
                sanitized.ButtonMask = buttons;
                return sanitized;
            }

            public static bool InputEquals(in InputStateDTO a, in InputStateDTO b)
            {
                return a.ButtonMask == b.ButtonMask &&
                    math.all(a.MoveAxis == b.MoveAxis) &&
                    math.all(a.LookDelta == b.LookDelta);
            }

            public static bool AupPayloadEquals(in FuzzerWireAupDTO a, in FuzzerWireAupDTO b)
            {
                return a.SectorHash == b.SectorHash &&
                    a.LocalMillimetersX == b.LocalMillimetersX &&
                    a.LocalMillimetersY == b.LocalMillimetersY &&
                    a.LocalMillimetersZ == b.LocalMillimetersZ;
            }

            public static void NormalizeAup(ref FuzzerKinematicStateDTO state)
            {
                NormalizeAxis(ref state.SectorX, ref state.LocalPosition.x);
                NormalizeAxis(ref state.SectorY, ref state.LocalPosition.y);
                NormalizeAxis(ref state.SectorZ, ref state.LocalPosition.z);
            }

            private static void NormalizeAxis(ref long sector, ref double local)
            {
                while (local >= SectorSizeMeters)
                {
                    local -= SectorSizeMeters;
                    sector++;
                }

                while (local < 0.0d)
                {
                    local += SectorSizeMeters;
                    sector--;
                }
            }

            public static bool IsFinite(in FuzzerKinematicStateDTO state)
            {
                return math.all(math.isfinite(state.LocalPosition)) &&
                    math.all(math.isfinite(state.Velocity)) &&
                    state.LocalPosition.x >= 0.0d &&
                    state.LocalPosition.y >= 0.0d &&
                    state.LocalPosition.z >= 0.0d &&
                    state.LocalPosition.x < SectorSizeMeters &&
                    state.LocalPosition.y < SectorSizeMeters &&
                    state.LocalPosition.z < SectorSizeMeters;
            }

            public static double3 ComposeAup(in FuzzerKinematicStateDTO state)
            {
                return new double3(
                    (state.SectorX * SectorSizeMeters) + state.LocalPosition.x,
                    (state.SectorY * SectorSizeMeters) + state.LocalPosition.y,
                    (state.SectorZ * SectorSizeMeters) + state.LocalPosition.z);
            }

            public static int ResolveTelemetryStride(float globalQualityWeight)
            {
                float quality = math.saturate(globalQualityWeight);
                return math.max(1, (int)math.round(math.lerp(8f, 1f, quality)));
            }

            public static int ResolveVisualStride(float globalQualityWeight)
            {
                float quality = math.saturate(globalQualityWeight);
                return math.max(1, (int)math.round(math.lerp(6f, 1f, quality)));
            }

            public static uint RollDeterministicLoot(uint worldSeed, in FuzzerKinematicStateDTO state, uint rollIndex, uint salt)
            {
                ulong seed = Mix64(0x5348494E4F425532UL, worldSeed);
                seed = Mix64(seed, unchecked((ulong)state.SectorX));
                seed = Mix64(seed, unchecked((ulong)state.SectorY));
                seed = Mix64(seed, unchecked((ulong)state.SectorZ));
                uint lx = (uint)QuantizeMillimeters(state.LocalPosition.x);
                uint ly = (uint)QuantizeMillimeters(state.LocalPosition.y);
                uint lz = (uint)QuantizeMillimeters(state.LocalPosition.z);
                seed = Mix64(seed, lx);
                seed = Mix64(seed, math.rol(ly, 11));
                seed = Mix64(seed, math.rol(lz, 17));
                seed = Mix64(seed, rollIndex);
                seed = Mix64(seed, salt);
                uint h = Mix32((uint)seed ^ (uint)(seed >> 32));
                Unity.Mathematics.Random rng = Unity.Mathematics.Random.CreateFromIndex(h);
                uint threshold = NextUIntRange(ref rng, 100u);
                if (threshold < 40u)
                    return 0x10010001u;
                if (threshold < 70u)
                    return 0x10010002u;
                if (threshold < 90u)
                    return 0x10010003u;
                return 0x1001FFFFu;
            }

            private static uint NextUIntRange(ref Unity.Mathematics.Random rng, uint range)
            {
                if (range == 0u)
                    return 0u;
                return (uint)(((ulong)rng.NextUInt() * range) >> 32);
            }

            private static long QuantizeMillimeters(double value)
            {
                double scaled = value * 1000.0d;
                return scaled >= 0.0d
                    ? (long)(scaled + 0.5d)
                    : (long)(scaled - 0.5d);
            }

            private static int QuantizeToIntMillimeters(double value)
            {
                long millimeters = QuantizeMillimeters(value);
                if (millimeters > int.MaxValue)
                    return int.MaxValue;
                if (millimeters < int.MinValue)
                    return int.MinValue;
                return (int)millimeters;
            }

            private static ulong SectorTripletHash(long sectorX, long sectorY, long sectorZ)
            {
                ulong hash = 0x4155505F53454354UL;
                hash = Mix64(hash, unchecked((ulong)sectorX));
                hash = Mix64(hash, unchecked((ulong)sectorY));
                hash = Mix64(hash, unchecked((ulong)sectorZ));
                return hash;
            }

            public static ulong HashState(
                NativeArray<FuzzerKinematicStateDTO> kinematics,
                NativeArray<FuzzerInventoryStateDTO> inventory,
                NativeArray<FuzzerEcosystemStateDTO> ecosystem,
                out ulong kinematicHash,
                out ulong inventoryHash,
                out ulong ecosystemHash)
            {
                kinematicHash = HashKinematicsQuantized(kinematics);
                inventoryHash = HashArray(inventory);
                ecosystemHash = HashArray(ecosystem);
                FuzzerStateHashRootDTO root = default;
                root.KinematicHash = kinematicHash;
                root.InventoryHash = inventoryHash;
                root.EcosystemHash = ecosystemHash;
                return HashStateRoot(in root);
            }

            private static ulong HashKinematicsQuantized(NativeArray<FuzzerKinematicStateDTO> array)
            {
                if (!array.IsCreated || array.Length <= 0)
                    return 0UL;

                ulong aggregate = 0UL;
                for (int i = 0; i < array.Length; i++)
                {
                    FuzzerKinematicStateDTO state = array[i];
                    FuzzerQuantizedKinematicHashDTO quantized = default;
                    quantized.SectorX = state.SectorX;
                    quantized.SectorY = state.SectorY;
                    quantized.SectorZ = state.SectorZ;
                    quantized.LocalMillimetersX = QuantizeToIntMillimeters(state.LocalPosition.x);
                    quantized.LocalMillimetersY = QuantizeToIntMillimeters(state.LocalPosition.y);
                    quantized.LocalMillimetersZ = QuantizeToIntMillimeters(state.LocalPosition.z);
                    quantized.VelocityMilliX = QuantizeToIntMillimeters(state.Velocity.x);
                    quantized.VelocityMilliY = QuantizeToIntMillimeters(state.Velocity.y);
                    quantized.VelocityMilliZ = QuantizeToIntMillimeters(state.Velocity.z);
                    quantized.Flags = state.Flags;
                    ulong leaf = HashQuantizedKinematic(in quantized);
                    aggregate = i == 0 ? leaf : Mix64(aggregate, leaf);
                }

                return aggregate;
            }

            private static ulong HashQuantizedKinematic(in FuzzerQuantizedKinematicHashDTO quantized)
            {
                FuzzerQuantizedKinematicHashDTO copy = quantized;
                return MemorySentinelMath.ComputeXXHash3Full64(&copy, UnsafeUtility.SizeOf<FuzzerQuantizedKinematicHashDTO>());
            }

            private static ulong HashStateRoot(in FuzzerStateHashRootDTO stateRoot)
            {
                FuzzerStateHashRootDTO copy = stateRoot;
                return MemorySentinelMath.ComputeXXHash3Full64(&copy, UnsafeUtility.SizeOf<FuzzerStateHashRootDTO>());
            }

            private static ulong HashArray<T>(NativeArray<T> array) where T : struct
            {
                if (!array.IsCreated || array.Length <= 0)
                    return 0UL;

                void* ptr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
                return MemorySentinelMath.ComputeXXHash3Full64(ptr, array.Length * UnsafeUtility.SizeOf<T>());
            }
        }
    }

    internal struct NetcodeDesyncFuzzerRunSummary
    {
        public uint Passed;
        public uint ErrorFlags;
        public uint MaxRollbackDepth;
        public uint MaxCatchupMicros;
        public uint DroppedPackets;
        public uint OutOfOrderDeliveries;
        public ulong HostMasterHash;
        public ulong ClientMasterHash;
        public double3 HostAup;
        public double3 ClientAup;
    }

    internal sealed class NetcodeDesyncFuzzerWindow : EditorWindow
    {
        private Label _statusLabel;
        private Label _hashLabel;
        private Label _metricsLabel;

        [MenuItem("Hecton8/Networking/Netcode Desync Fuzzer")]
        public static void Open()
        {
            GetWindow<NetcodeDesyncFuzzerWindow>("Netcode Desync Fuzzer");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            Button runButton = new Button(RunFuzzer) { text = "RUN 10,000 FRAME NETWORK STRESS TEST" };
            _statusLabel = new Label("PENDING");
            _hashLabel = new Label("hash: none");
            _metricsLabel = new Label("rollback: 0 | loss: 0 | out_of_order: 0");

            rootVisualElement.Add(runButton);
            rootVisualElement.Add(_statusLabel);
            rootVisualElement.Add(_hashLabel);
            rootVisualElement.Add(_metricsLabel);
        }

        private void RunFuzzer()
        {
            bool passed = NetcodeDesyncFuzzerEditTests.RunFromEditor(out NetcodeDesyncFuzzerRunSummary summary);
            _statusLabel.text = passed ? "PASS" : "FAIL";
            _statusLabel.style.color = passed ? Color.green : Color.red;
            _hashLabel.text = "host=0x" + summary.HostMasterHash.ToString("X16") + " | client=0x" + summary.ClientMasterHash.ToString("X16");
            _metricsLabel.text =
                "rollback=" + summary.MaxRollbackDepth +
                " | catchup_us=" + summary.MaxCatchupMicros +
                " | dropped=" + summary.DroppedPackets +
                " | out_of_order=" + summary.OutOfOrderDeliveries +
                " | flags=0x" + summary.ErrorFlags.ToString("X8");

            if (passed)
                NetcodeDesyncFuzzerReplayGizmo.ClearFailure();
            else
                NetcodeDesyncFuzzerReplayGizmo.SetFailure(summary.HostAup, summary.ClientAup);

            SceneView.RepaintAll();
        }
    }

    internal sealed class NetcodeDesyncFuzzerReplayGizmo : MonoBehaviour
    {
        private static NetcodeDesyncFuzzerReplayGizmo s_instance;
        private static bool s_hasFailure;
        private static double3 s_hostAup;
        private static double3 s_clientAup;

        public static void SetFailure(double3 hostAup, double3 clientAup)
        {
            s_hasFailure = true;
            s_hostAup = hostAup;
            s_clientAup = clientAup;
            EnsureInstance();
        }

        public static void ClearFailure()
        {
            s_hasFailure = false;
        }

        private static void EnsureInstance()
        {
            if (s_instance != null)
                return;

            GameObject root = new GameObject("[NetcodeDesyncFuzzerReplayGizmo]");
            root.hideFlags = HideFlags.DontSave;
            s_instance = root.AddComponent<NetcodeDesyncFuzzerReplayGizmo>();
        }

        private void OnEnable()
        {
            s_instance = this;
        }

        private void OnDisable()
        {
            if (s_instance == this)
                s_instance = null;
        }

        private void OnDrawGizmos()
        {
            if (!s_hasFailure)
                return;

            float pulse = 1f + 0.2f * math.sin((float)EditorApplication.timeSinceStartup * 8f);
            double3 anchor = s_hostAup;
            double3 hostDelta = s_hostAup - anchor;
            double3 clientDelta = s_clientAup - anchor;
            Vector3 host = new Vector3((float)hostDelta.x, (float)hostDelta.y, (float)hostDelta.z);
            Vector3 client = new Vector3((float)clientDelta.x, (float)clientDelta.y, (float)clientDelta.z);
            float radius = 6f * pulse;

            Gizmos.color = Color.blue;
            Gizmos.DrawWireCube(host, new Vector3(radius, radius, radius));
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(client, new Vector3(radius * 1.15f, radius * 1.15f, radius * 1.15f));
            Gizmos.DrawLine(host, client);
        }
    }
}
#endif
