using System;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Hecton8.Quest;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Tests.Editor
{
    public sealed class QuestDagResolverEditTests
    {
        [Test]
        public void QuestDagDtos_AreArm64Aligned()
        {
            Assert.AreEqual(QuestDagLayoutAudit.QuestNodeDTOSize, UnsafeUtility.SizeOf<QuestNodeDTO>());
            Assert.AreEqual(QuestDagLayoutAudit.TriggerVolumeDTOSize, UnsafeUtility.SizeOf<TriggerVolumeDTO>());
            Assert.AreEqual(QuestDagLayoutAudit.QuestNodeRuntimeDTOSize, UnsafeUtility.SizeOf<QuestNodeRuntimeDTO>());
            Assert.AreEqual(QuestDagLayoutAudit.QuestStateDTOSize, UnsafeUtility.SizeOf<QuestStateDTO>());
            Assert.AreEqual(QuestDagLayoutAudit.QuestDependencyLinkDTOSize, UnsafeUtility.SizeOf<QuestDependencyLinkDTO>());
            Assert.AreEqual(QuestDagLayoutAudit.QuestDagTelemetryEntrySize, UnsafeUtility.SizeOf<QuestDagTelemetryEntry>());
            Assert.AreEqual(0, Marshal.OffsetOf<QuestStateDTO>(nameof(QuestStateDTO.ActiveQuestHashID)).ToInt32());
            Assert.AreEqual(4, Marshal.OffsetOf<QuestStateDTO>(nameof(QuestStateDTO.CompletionProgress)).ToInt32());
            Assert.AreEqual(8, Marshal.OffsetOf<QuestStateDTO>(nameof(QuestStateDTO.InjectedSubQuestHashID)).ToInt32());
            Assert.AreEqual(12, Marshal.OffsetOf<QuestStateDTO>(nameof(QuestStateDTO.StateFlags)).ToInt32());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<StateChangedSignal>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<MockStoryEventSignal>());
            Assert.AreEqual(40, UnsafeUtility.SizeOf<MockPlayerPositionSignal>());
            Assert.AreEqual(32, UnsafeUtility.SizeOf<QuestDagMockItemAcquiredSignal>());
            Assert.AreEqual(64, UnsafeUtility.SizeOf<QuestSaveHeader>());
            Assert.AreEqual(16, Marshal.OffsetOf<QuestSaveHeader>(nameof(QuestSaveHeader.Timestamp)).ToInt32());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<QuestNodeDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<TriggerVolumeDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<QuestStateDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<QuestDependencyLinkDTO>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<QuestDagTelemetryEntry>() & 7);
            Assert.AreEqual(0, UnsafeUtility.SizeOf<QuestSaveHeader>() & 7);
        }

        [Test]
        public void OshinoBinary_LoadsIntoUlongVaultState()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, 16L * 1024L * 1024L))
            {
                QuestDagBufferHandles handles = QuestDagVault.EnsureBuffers(vault, 8, 8, 1, 8, 8, 4);
                bool loaded = MockQuestDatabase.TryLoadOshinoBinary(
                    vault,
                    ref handles,
                    QuestDagRuntimeConstants.DefaultBinaryPath,
                    out QuestDagLoadStats stats);

                Assert.IsTrue(loaded);
                Assert.AreEqual(4u, stats.NodeCount);
                Assert.AreEqual((uint)QuestDagLoadFlags.BinaryLoaded, stats.Flags & (uint)QuestDagLoadFlags.BinaryLoaded);
                ref ulong mask = ref QuestDagVault.GetStateMaskRef(vault, ref handles, 0);
                mask = 0x2UL;
                Assert.AreEqual(0x2UL, mask);
            }
        }

        [Test]
        public void Resolver_CompletesNearbyMockTrigger()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, 32L * 1024L * 1024L))
            using (QuestDagResolverService resolver = new QuestDagResolverService(vault, 128, 128))
            {
                MockQuestDatabase.GenerateEmergencyMockDAG(vault, ref resolver.Handles, 16, out _);

                resolver.Schedule(new double3(0d, 0d, 0d), 999UL, 15u, 0.1f, default);
                resolver.CompleteScheduled();

                ref ulong mask = ref resolver.GetStateMaskRef(0);
                Assert.AreNotEqual(0UL, mask);
            }
        }

        [Test]
        public void Resolver_ReusesSpatialHashUntilTriggerVersionChanges()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, 32L * 1024L * 1024L))
            using (QuestDagResolverService resolver = new QuestDagResolverService(vault, 128, 128))
            {
                MockQuestDatabase.GenerateEmergencyMockDAG(vault, ref resolver.Handles, 16, out _);

                resolver.Schedule(new double3(0d, 0d, 0d), 999UL, 15u, 0.1f, default);
                resolver.CompleteScheduled();
                QuestDagVault.TryResolveBuffers(vault, ref resolver.Handles, out QuestDagBuffers buffers);
                Assert.AreEqual(1, buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.SpatialHashRebuildCount]);

                resolver.Schedule(new double3(25d, 0d, 0d), 1000UL, 16u, 0.1f, default);
                resolver.CompleteScheduled();
                Assert.AreEqual(1, buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.SpatialHashRebuildCount]);

                resolver.InvalidateSpatialHash();
                resolver.Schedule(new double3(50d, 0d, 0d), 1001UL, 17u, 0.1f, default);
                resolver.CompleteScheduled();
                Assert.AreEqual(2, buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.SpatialHashRebuildCount]);
            }
        }

        [Test]
        public void CsvOverride_UpdatesRequiredQuantityWithoutManagedQuestState()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, 16L * 1024L * 1024L))
            {
                QuestDagBufferHandles handles = QuestDagVault.EnsureBuffers(vault, 8, 8, 1, 8, 8, 4);
                MockQuestDatabase.GenerateEmergencyMockDAG(vault, ref handles, 8, out _);

                char[] csv = "0x51000001,0x49000001,10,42,0,0\n".ToCharArray();
                bool applied = QuestDagCsvOverrideIngestor.TryApplyOverrides(vault, ref handles, new ReadOnlySpan<char>(csv), out int rows);

                Assert.IsTrue(applied);
                Assert.AreEqual(1, rows);
                QuestDagVault.TryResolveBuffers(vault, ref handles, out QuestDagBuffers buffers);
                Assert.AreEqual(10, buffers.RequiredItemQuantities[1]);
                Assert.AreEqual(42UL, buffers.NodeRuntime[1].TargetTimestamp);
            }
        }

        [Test]
        public void Resolver_ZeigarnikOverlap_InjectsLinkedQuestHash()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, 16L * 1024L * 1024L))
            using (QuestDagResolverService resolver = new QuestDagResolverService(vault, 16, 16))
            {
                MockQuestDatabase.GenerateEmergencyMockDAG(vault, ref resolver.Handles, 8, out _);
                QuestDagVault.TryResolveBuffers(vault, ref resolver.Handles, out QuestDagBuffers buffers);

                buffers.QuestStates[0] = new QuestStateDTO
                {
                    ActiveQuestHashID = 0x51000000u,
                    CompletionProgress = 0.96f,
                    InjectedSubQuestHashID = 0u,
                    StateFlags = 0u
                };
                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.QuestStateCount] = 1;

                resolver.Schedule(new double3(0d, 0d, 0d), 999UL, 30u, 0.1f, default);
                resolver.CompleteScheduled();

                QuestStateDTO resolved = buffers.QuestStates[0];
                QuestDagTelemetryEntry entry = ReadLastTelemetry(buffers);
                Assert.AreEqual(0x51000001u, resolved.InjectedSubQuestHashID);
                Assert.AreNotEqual(0u, resolved.StateFlags & (uint)QuestStateFlags.ZeigarnikInjected);
                Assert.AreEqual(1, buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.ZeigarnikInjectedCount]);
                Assert.AreNotEqual(0, entry.Flags & (ushort)QuestDagTelemetryFlags.ZeigarnikInjected);
            }
        }

        [Test]
        public void Resolver_TenThousandTriggers_StaysSpatialCandidateBound()
        {
            const int NodeCount = 10000;
            using (GlobalDataVault vault = GlobalDataVault.Create(64, 96L * 1024L * 1024L))
            using (QuestDagResolverService resolver = new QuestDagResolverService(vault, NodeCount, NodeCount))
            {
                QuestDagVault.TryResolveBuffers(vault, ref resolver.Handles, out QuestDagBuffers buffers);
                PopulateFlatSpatialDag(buffers, NodeCount);

                resolver.Schedule(new double3(0d, 0d, 0d), 999UL, 30u, 0.1f, default);
                resolver.CompleteScheduled();

                QuestDagVault.TryResolveBuffers(vault, ref resolver.Handles, out buffers);
                QuestDagTelemetryEntry entry = ReadLastTelemetry(buffers);
                Assert.AreEqual(1UL, buffers.GlobalStateMasks[0] & 1UL);
                Assert.Less(entry.SpatialCandidateCount, 16);
                Assert.Less(entry.ActiveNodesEvaluated, 16);
                Assert.AreNotEqual(0u, entry.PlayerCellHash);
                Assert.AreNotEqual(0u, entry.StateHash);
            }
        }

        [Test]
        public void Resolver_RadiusOverlapAcrossTwoCells_CompletesTrigger()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, 16L * 1024L * 1024L))
            using (QuestDagResolverService resolver = new QuestDagResolverService(vault, 4, 4))
            {
                QuestDagVault.TryResolveBuffers(vault, ref resolver.Handles, out QuestDagBuffers buffers);
                const uint NodeHash = 0x51515545u;
                buffers.Nodes[0] = new QuestNodeDTO
                {
                    NodeHash = NodeHash,
                    RequiredStateHash = 0u,
                    PrerequisiteMask = 0UL,
                    CompletionMask = 1UL,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
                buffers.NodeRuntime[0] = new QuestNodeRuntimeDTO
                {
                    TargetTimestamp = 0UL,
                    ReputationDelta = 0f,
                    ReputationThreshold = 0f,
                    StateChunk = 0,
                    TriggerIndex = 0,
                    RequiredItemStart = -1,
                    RequiredItemCount = 0,
                    FactionId = ushort.MaxValue,
                    Flags = (ushort)QuestDagNodeFlags.RequiresTrigger,
                    _pad0 = 0u
                };
                buffers.TriggerNodeIndices[0] = 0;
                buffers.TriggerVolumes[0] = new TriggerVolumeDTO
                {
                    AUP = new double3(99d, 0d, 0d),
                    Radius = 180f,
                    RequiredNodeHash = NodeHash,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.NodeCount] = 1;
                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.TriggerCount] = 1;
                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.StateChunkCount] = 1;
                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion] =
                    buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion] + 1;

                resolver.Schedule(new double3(250d, 0d, 0d), 1UL, 30u, 0.1f, default);
                resolver.CompleteScheduled();

                Assert.AreEqual(1UL, buffers.GlobalStateMasks[0] & 1UL);
                QuestDagTelemetryEntry entry = ReadLastTelemetry(buffers);
                Assert.Greater(entry.SpatialCandidateCount, 0);
            }
        }

        [Test]
        public void TelemetryDump_WritesHeaderAndChronologicalPayload()
        {
            using (GlobalDataVault vault = GlobalDataVault.Create(64, 16L * 1024L * 1024L))
            {
                QuestDagBufferHandles handles = QuestDagVault.EnsureBuffers(vault, 8, 8, 1, 8, 8, 4);
                QuestDagVault.TryResolveBuffers(vault, ref handles, out QuestDagBuffers buffers);
                buffers.TelemetryRing[0] = new QuestDagTelemetryEntry { Frame = 10u, StateHash = 0x10u };
                buffers.TelemetryRing[1] = new QuestDagTelemetryEntry { Frame = 11u, StateHash = 0x11u };
                buffers.TelemetryCursor[0] = 1;

                string path = Path.Combine(Path.GetTempPath(), "QuestDagDumpTest_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture) + ".bin");
                try
                {
                    QuestDagTelemetryDump.Write(path, buffers.TelemetryRing.AsReadOnly(), buffers.TelemetryCursor[0]);
                    byte[] bytes = File.ReadAllBytes(path);
                    int entrySize = UnsafeUtility.SizeOf<QuestDagTelemetryEntry>();
                    Assert.AreEqual(QuestDagTelemetryDump.DumpMagic, BitConverter.ToInt64(bytes, 0));
                    Assert.AreEqual(buffers.TelemetryRing.Length, BitConverter.ToInt32(bytes, 8));
                    Assert.AreEqual(entrySize, BitConverter.ToInt32(bytes, 12));
                    Assert.AreEqual(1, BitConverter.ToInt32(bytes, 16));
                    Assert.AreEqual(11u, BitConverter.ToUInt32(bytes, QuestDagTelemetryDump.DumpHeaderBytes + 16));
                }
                finally
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
            }
        }

        private static void PopulateFlatSpatialDag(QuestDagBuffers buffers, int nodeCount)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                uint nodeHash = unchecked(0x51000000u + (uint)i);
                buffers.Nodes[i] = new QuestNodeDTO
                {
                    NodeHash = nodeHash,
                    RequiredStateHash = 0u,
                    PrerequisiteMask = 0UL,
                    CompletionMask = 1UL << (i & 63),
                    _pad0 = 0u,
                    _pad1 = 0u
                };
                buffers.NodeRuntime[i] = new QuestNodeRuntimeDTO
                {
                    TargetTimestamp = 0UL,
                    ReputationDelta = 0f,
                    ReputationThreshold = 0f,
                    StateChunk = 0,
                    TriggerIndex = i,
                    RequiredItemStart = -1,
                    RequiredItemCount = 0,
                    FactionId = ushort.MaxValue,
                    Flags = (ushort)QuestDagNodeFlags.RequiresTrigger,
                    _pad0 = 0u
                };
                buffers.TriggerNodeIndices[i] = i;
                buffers.TriggerVolumes[i] = new TriggerVolumeDTO
                {
                    AUP = new double3((i % 100) * 250d, 0d, (i / 100) * 250d),
                    Radius = 5f,
                    RequiredNodeHash = nodeHash,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
            }

            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.NodeCount] = nodeCount;
            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.TriggerCount] = nodeCount;
            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.NoTriggerNodeCount] = 0;
            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.StateChunkCount] = 1;
            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion] =
                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion] + 1;
        }

        private static QuestDagTelemetryEntry ReadLastTelemetry(QuestDagBuffers buffers)
        {
            int cursor = buffers.TelemetryCursor[0] - 1;
            if (cursor < 0)
                cursor += buffers.TelemetryRing.Length;

            return buffers.TelemetryRing[cursor % buffers.TelemetryRing.Length];
        }
    }
}
