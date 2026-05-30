using System;
using System.IO;
using System.Threading;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Quest
{
    /// <summary>
    /// Cold OSHINO binary reader and emergency mock generator for the quest DAG.
    /// </summary>
    public static class MockQuestDatabase
    {
        private const uint BinaryMagic = 0x47513848u; // H8QG little-endian
        private const ushort BinaryVersion = 1;
        private const ushort BinaryHeaderBytes = 64;
        private const uint DoneBitPattern = 0xAAAAAAAAu;
        private static readonly ulong QuestDagLoadMutationGuardMask =
            QuestDagMutationGuardBit(BufferID.QuestDagGlobalStateMasks) |
            QuestDagMutationGuardBit(BufferID.QuestDagOldStateMasks) |
            QuestDagMutationGuardBit(BufferID.QuestDagNodes) |
            QuestDagMutationGuardBit(BufferID.QuestDagNodeRuntime) |
            QuestDagMutationGuardBit(BufferID.QuestDagTriggerVolumes) |
            QuestDagMutationGuardBit(BufferID.QuestDagRequiredItemHashes) |
            QuestDagMutationGuardBit(BufferID.QuestDagRequiredItemQuantities) |
            QuestDagMutationGuardBit(BufferID.QuestDagPlayerItemHashes) |
            QuestDagMutationGuardBit(BufferID.QuestDagPlayerItemQuantities) |
            QuestDagMutationGuardBit(BufferID.QuestDagFactionStandings) |
            QuestDagMutationGuardBit(BufferID.QuestDagCounters) |
            QuestDagMutationGuardBit(BufferID.QuestDagTriggerNodeIndices) |
            QuestDagMutationGuardBit(BufferID.QuestDagNoTriggerNodeIndices);

        /// <summary>
        /// Attempts to load the current OSHINO binary; falls back to a deterministic mock DAG.
        /// </summary>
        public static bool TryLoadOshinoOrGenerateMock(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            string binaryPath,
            out QuestDagLoadStats stats)
        {
            if (TryLoadOshinoBinary(vault, ref handles, binaryPath, out stats))
                return true;

            QuestDagLoadFlags previousFlags = (QuestDagLoadFlags)stats.Flags;
            GenerateEmergencyMockDAG(vault, ref handles, 64, out stats);
            stats.Flags |= (uint)previousFlags;
            return false;
        }

        /// <summary>
        /// Parses the little-endian H8QG quest graph binary produced by Tools/QuestCompiler.py.
        /// </summary>
        public static bool TryLoadOshinoBinary(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            string binaryPath,
            out QuestDagLoadStats stats)
        {
            stats = default;
            if (vault == null ||
                handles.NodeCapacity <= 0 ||
                handles.TriggerCapacity <= 0 ||
                handles.StateChunkCount <= 0 ||
                handles.ItemLinkCapacity <= 0)
            {
                stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                return false;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(binaryPath) || !File.Exists(binaryPath))
                {
                    stats.Flags = (uint)QuestDagLoadFlags.BinaryMissing;
                    return false;
                }

                using FileStream stream = new FileStream(binaryPath, FileMode.Open, FileAccess.Read, FileShare.Read);
                using BinaryReader reader = new BinaryReader(stream);
                uint magic = reader.ReadUInt32();
                ushort version = reader.ReadUInt16();
                ushort headerBytes = reader.ReadUInt16();
                uint flags = reader.ReadUInt32();
                uint graphHash = reader.ReadUInt32();
                uint nodeCount = reader.ReadUInt32();
                uint maxNodes = reader.ReadUInt32();
                uint nodeOffset = reader.ReadUInt32();
                uint triggerCount = reader.ReadUInt32();
                uint triggerRecordBytes = reader.ReadUInt32();
                uint triggerOffset = reader.ReadUInt32();
                uint edgeCount = reader.ReadUInt32();
                uint edgeRecordBytes = reader.ReadUInt32();
                uint edgeOffset = reader.ReadUInt32();
                uint totalBytes = reader.ReadUInt32();
                ulong allDoneMask = reader.ReadUInt64();

                if (magic != BinaryMagic ||
                    version != BinaryVersion ||
                    headerBytes != BinaryHeaderBytes ||
                    nodeOffset != BinaryHeaderBytes ||
                    triggerRecordBytes != 16u ||
                    edgeRecordBytes != 16u ||
                    totalBytes > stream.Length ||
                    nodeCount == 0u ||
                    nodeCount > maxNodes ||
                    nodeCount > handles.NodeCapacity ||
                    triggerCount > handles.TriggerCapacity ||
                    nodeCount > handles.ItemLinkCapacity)
                {
                    stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                    return false;
                }

                if (!TryAcquireQuestDagLoadBuffers(vault, ref handles, out QuestDagBuffers buffers))
                {
                    stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                    return false;
                }

                try
                {
                    if (!ClearDagBuffers(in buffers))
                    {
                        stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                        return false;
                    }

                    int loadedNodeCount = (int)nodeCount;
                    int loadedTriggerCount = (int)triggerCount;

                    stream.Position = nodeOffset;
                    for (int i = 0; i < loadedNodeCount; i++)
                    {
                        uint nodeHash = reader.ReadUInt32();
                        uint loreHash = reader.ReadUInt32();
                        ulong prerequisiteMask = reader.ReadUInt64();
                        ulong stateMask = reader.ReadUInt64();
                        ushort slot = reader.ReadUInt16();
                        ushort triggerSpan = reader.ReadUInt16();
                        byte topoIndex = reader.ReadByte();
                        byte prerequisiteCount = reader.ReadByte();
                        reader.ReadUInt16();

                        int stateChunk = slot >> 5;
                        if ((uint)stateChunk >= (uint)handles.StateChunkCount)
                        {
                            stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                            return false;
                        }

                        ulong completionMask = stateMask & 0xAAAAAAAAAAAAAAAAUL;
                        QuestNodeDTO node = default;
                        node.NodeHash = nodeHash;
                        node.RequiredStateHash = loreHash;
                        node.PrerequisiteMask = prerequisiteMask;
                        node.CompletionMask = completionMask;
                        node._pad0 = slot;
                        node._pad1 = ((uint)topoIndex << 16) | prerequisiteCount;

                        QuestNodeRuntimeDTO runtime = default;
                        runtime.TargetTimestamp = 0UL;
                        runtime.ReputationDelta = 0f;
                        runtime.ReputationThreshold = 0f;
                        runtime.StateChunk = stateChunk;
                        runtime.TriggerIndex = triggerSpan > 0 ? i : -1;
                        runtime.RequiredItemStart = i;
                        runtime.RequiredItemCount = 0;
                        runtime.FactionId = ushort.MaxValue;
                        runtime.Flags = triggerSpan > 0 ? (ushort)QuestDagNodeFlags.RequiresTrigger : (ushort)QuestDagNodeFlags.None;
                        runtime._pad0 = 0u;

                        buffers.Nodes[i] = node;
                        buffers.NodeRuntime[i] = runtime;
                    }

                    stream.Position = triggerOffset;
                    for (int i = 0; i < loadedTriggerCount; i++)
                    {
                        uint triggerHash = reader.ReadUInt32();
                        uint triggerTypeHash = reader.ReadUInt32();
                        ulong doneMask = reader.ReadUInt64();
                        if (!TryFindNodeIndexByDoneMask(buffers.Nodes, loadedNodeCount, doneMask, out int nodeIndex))
                        {
                            stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                            return false;
                        }

                        if (nodeIndex < 0)
                            continue;

                        if ((uint)nodeIndex >= (uint)buffers.NodeRuntime.Length ||
                            (uint)nodeIndex >= (uint)buffers.Nodes.Length)
                        {
                            stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                            return false;
                        }

                        QuestNodeRuntimeDTO runtime = buffers.NodeRuntime[nodeIndex];
                        QuestNodeDTO node = buffers.Nodes[nodeIndex];

                        runtime.TriggerIndex = i;
                        runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresTrigger;
                        if (triggerTypeHash == 0x1D88D039u)
                        {
                            runtime.RequiredItemStart = nodeIndex;
                            runtime.RequiredItemCount = 1;
                            runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresInventory;
                            if ((uint)nodeIndex >= (uint)buffers.RequiredItemHashes.Length ||
                                (uint)nodeIndex >= (uint)buffers.RequiredItemQuantities.Length)
                            {
                                stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                                return false;
                            }

                            buffers.RequiredItemHashes[nodeIndex] = triggerHash;
                            buffers.RequiredItemQuantities[nodeIndex] = 1;
                        }

                        if ((uint)i >= (uint)buffers.TriggerNodeIndices.Length ||
                            (uint)i >= (uint)buffers.TriggerVolumes.Length)
                        {
                            stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                            return false;
                        }

                        TriggerVolumeDTO volume = default;
                        volume.AUP = new double3(i * 12.5d, 0d, 0d);
                        volume.Radius = 8f;
                        volume.RequiredNodeHash = node.NodeHash;
                        volume._pad0 = triggerHash;
                        volume._pad1 = triggerTypeHash;

                        buffers.NodeRuntime[nodeIndex] = runtime;
                        buffers.TriggerNodeIndices[i] = nodeIndex;
                        buffers.TriggerVolumes[i] = volume;
                    }

                    if (!TryPopulateNoTriggerNodeIndices(in buffers, loadedNodeCount, out int noTriggerCount))
                    {
                        stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                        return false;
                    }

                    WriteCounters(
                        in buffers,
                        loadedNodeCount,
                        loadedTriggerCount,
                        noTriggerCount,
                        unchecked((int)QuestDagRuntimeConstants.OshinoBinarySourceHash));

                    stats = new QuestDagLoadStats
                    {
                        AllDoneMask = allDoneMask,
                        GraphHash = graphHash,
                        NodeCount = nodeCount,
                        TriggerCount = triggerCount,
                        EdgeCount = edgeCount,
                        TotalBytes = totalBytes,
                        Flags = flags | (uint)QuestDagLoadFlags.BinaryLoaded,
                        SourceHash = QuestDagRuntimeConstants.OshinoBinarySourceHash
                    };
                    return true;
                }
                finally
                {
                    ReleaseQuestDagLoadGuard(vault);
                }
            }
            catch (EndOfStreamException)
            {
                stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                return false;
            }
            catch (IOException)
            {
                stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                return false;
            }
        }

        /// <summary>
        /// Generates a deterministic 16-byte-aligned dummy DAG when OSHINO data is unavailable.
        /// </summary>
        public static void GenerateEmergencyMockDAG(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int requestedNodeCount,
            out QuestDagLoadStats stats)
        {
            stats = default;
            if (vault == null ||
                handles.NodeCapacity <= 0 ||
                handles.TriggerCapacity <= 0 ||
                handles.StateChunkCount <= 0 ||
                handles.ItemLinkCapacity <= 0)
                return;

            if (!TryAcquireQuestDagLoadBuffers(vault, ref handles, out QuestDagBuffers buffers))
                return;

            try
            {
                if (!ClearDagBuffers(in buffers))
                    return;

                int stateNodeCapacity = handles.StateChunkCount << 6;
                int nodeCount = math.clamp(
                    requestedNodeCount,
                    1,
                    math.min(stateNodeCapacity, math.min(handles.NodeCapacity, math.min(handles.TriggerCapacity, handles.ItemLinkCapacity))));
                int noTriggerCount = 0;
                for (int i = 0; i < nodeCount; i++)
                {
                    int chunk = i >> 6;
                    int bit = i & 63;
                    ulong doneMask = 1UL << bit;
                    ulong prerequisiteMask = bit > 0 ? 1UL << (bit - 1) : 0UL;
                    uint nodeHash = unchecked(0x51000000u + (uint)i);
                    bool requiresTrigger = (i & 3) != 3;

                    QuestNodeDTO node = default;
                    node.NodeHash = nodeHash;
                    node.RequiredStateHash = unchecked(0x71000000u + (uint)i);
                    node.PrerequisiteMask = prerequisiteMask;
                    node.CompletionMask = doneMask;
                    node._pad0 = 0u;
                    node._pad1 = 0u;

                    QuestNodeRuntimeDTO runtime = default;
                    runtime.TargetTimestamp = (i & 7) == 7 ? (ulong)(120 + i) : 0UL;
                    runtime.ReputationDelta = (i & 15) == 0 ? 0.25f : 0f;
                    runtime.ReputationThreshold = (i & 15) == 8 ? 1f : 0f;
                    runtime.StateChunk = chunk;
                    runtime.TriggerIndex = requiresTrigger ? i : -1;
                    runtime.RequiredItemStart = i;
                    runtime.RequiredItemCount = (i & 5) == 1 ? 1 : 0;
                    runtime.FactionId = (i & 15) == 0 || (i & 15) == 8 ? (ushort)0 : ushort.MaxValue;
                    runtime.Flags = BuildMockFlags(requiresTrigger, i);
                    runtime._pad0 = 0u;

                    if (!requiresTrigger)
                    {
                        if ((uint)noTriggerCount >= (uint)buffers.NoTriggerNodeIndices.Length)
                            return;

                        buffers.NoTriggerNodeIndices[noTriggerCount] = i;
                        noTriggerCount++;
                    }

                    TriggerVolumeDTO volume = default;
                    volume.AUP = new double3((i % 100) * 25d, 0d, (i / 100) * 25d);
                    volume.Radius = 10f;
                    volume.RequiredNodeHash = nodeHash;
                    volume._pad0 = unchecked(0x54000000u + (uint)i);
                    volume._pad1 = 0u;

                    if ((uint)i >= (uint)buffers.Nodes.Length ||
                        (uint)i >= (uint)buffers.NodeRuntime.Length ||
                        (uint)i >= (uint)buffers.RequiredItemHashes.Length ||
                        (uint)i >= (uint)buffers.RequiredItemQuantities.Length ||
                        (uint)i >= (uint)buffers.TriggerNodeIndices.Length ||
                        (uint)i >= (uint)buffers.TriggerVolumes.Length)
                    {
                        return;
                    }

                    buffers.Nodes[i] = node;
                    buffers.NodeRuntime[i] = runtime;
                    buffers.RequiredItemHashes[i] = unchecked(0x49000000u + (uint)(i & 31));
                    buffers.RequiredItemQuantities[i] = (i & 5) == 1 ? 1 + (i & 3) : 0;
                    buffers.TriggerNodeIndices[i] = i;
                    buffers.TriggerVolumes[i] = volume;
                }

                WriteCounters(
                    in buffers,
                    nodeCount,
                    nodeCount,
                    noTriggerCount,
                    unchecked((int)QuestDagRuntimeConstants.EmergencyMockSourceHash));

                stats = new QuestDagLoadStats
                {
                    AllDoneMask = BuildAllDoneMask(nodeCount),
                    GraphHash = 0x4D4F434Bu,
                    NodeCount = (uint)nodeCount,
                    TriggerCount = (uint)nodeCount,
                    EdgeCount = (uint)math.max(0, nodeCount - 1),
                    TotalBytes = (uint)(64 + (nodeCount * 32) + (nodeCount * 16)),
                    Flags = (uint)QuestDagLoadFlags.EmergencyMockGenerated,
                    SourceHash = QuestDagRuntimeConstants.EmergencyMockSourceHash
                };
            }
            finally
            {
                ReleaseQuestDagLoadGuard(vault);
            }
        }

        private static ushort BuildMockFlags(bool requiresTrigger, int index)
        {
            ushort flags = 0;
            if (requiresTrigger)
                flags |= (ushort)QuestDagNodeFlags.RequiresTrigger;
            if ((index & 5) == 1)
                flags |= (ushort)QuestDagNodeFlags.RequiresInventory;
            if ((index & 15) == 0)
                flags |= (ushort)QuestDagNodeFlags.AppliesFactionDelta;
            if ((index & 15) == 8)
                flags |= (ushort)QuestDagNodeFlags.RequiresFactionThreshold;
            if ((index & 7) == 7)
                flags |= (ushort)QuestDagNodeFlags.RequiresTimestamp;
            return flags;
        }

        private static bool TryAcquireQuestDagLoadBuffers(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            out QuestDagBuffers buffers)
        {
            buffers = default;
            if (vault == null ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(QuestDagLoadMutationGuardMask))
            {
                return false;
            }

            if (QuestDagVault.TryResolveBuffers(vault, ref handles, out buffers))
                return true;

            vault.ReleaseMutationGuard(QuestDagLoadMutationGuardMask);
            buffers = default;
            return false;
        }

        private static void ReleaseQuestDagLoadGuard(IDataVault vault)
        {
            vault?.ReleaseMutationGuard(QuestDagLoadMutationGuardMask);
        }

        private static ulong QuestDagMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
        }

        private static bool ClearDagBuffers(in QuestDagBuffers buffers)
        {
            return Clear(buffers.GlobalStateMasks) &&
                   Clear(buffers.OldStateMasks) &&
                   Clear(buffers.Nodes) &&
                   Clear(buffers.NodeRuntime) &&
                   Clear(buffers.TriggerVolumes) &&
                   Clear(buffers.RequiredItemHashes) &&
                   Clear(buffers.RequiredItemQuantities) &&
                   Clear(buffers.PlayerItemHashes) &&
                   Clear(buffers.PlayerItemQuantities) &&
                   Clear(buffers.FactionStandings) &&
                   Clear(buffers.TriggerNodeIndices) &&
                   Clear(buffers.NoTriggerNodeIndices);
        }

        private static bool Clear<T>(NativeArray<T> values)
            where T : struct
        {
            if (!values.IsCreated)
                return false;

            for (int i = 0; i < values.Length; i++)
                values[i] = default;

            return true;
        }

        private static void WriteCounters(
            in QuestDagBuffers buffers,
            int nodeCount,
            int triggerCount,
            int noTriggerCount,
            int sourceHash)
        {
            NativeArray<int> counters = buffers.Counters;
            if (!counters.IsCreated || counters.Length < QuestDagRuntimeConstants.CounterCount)
                return;

            counters[(int)QuestDagRuntimeConstants.CounterSlot.NodeCount] = nodeCount;
            counters[(int)QuestDagRuntimeConstants.CounterSlot.TriggerCount] = triggerCount;
            counters[(int)QuestDagRuntimeConstants.CounterSlot.NoTriggerNodeCount] = noTriggerCount;
            counters[(int)QuestDagRuntimeConstants.CounterSlot.StateChunkCount] = math.max(
                1,
                counters[(int)QuestDagRuntimeConstants.CounterSlot.StateChunkCount]);
            counters[(int)QuestDagRuntimeConstants.CounterSlot.LastLoadSourceHash] = sourceHash;
            int spatialVersionIndex = (int)QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion;
            counters[spatialVersionIndex] = counters[spatialVersionIndex] + 1;
        }

        private static bool TryPopulateNoTriggerNodeIndices(
            in QuestDagBuffers buffers,
            int nodeCount,
            out int count)
        {
            count = 0;
            NativeArray<QuestNodeRuntimeDTO> runtime = buffers.NodeRuntime;
            NativeArray<int> noTriggerNodeIndices = buffers.NoTriggerNodeIndices;
            if (!runtime.IsCreated || !noTriggerNodeIndices.IsCreated)
                return false;

            int limit = math.min(nodeCount, runtime.Length);
            for (int i = 0; i < limit; i++)
            {
                if ((runtime[i].Flags & (ushort)QuestDagNodeFlags.RequiresTrigger) == 0)
                {
                    if ((uint)count >= (uint)noTriggerNodeIndices.Length)
                        return false;

                    noTriggerNodeIndices[count] = i;
                    count++;
                }
            }

            return true;
        }

        private static bool TryFindNodeIndexByDoneMask(
            NativeArray<QuestNodeDTO> nodes,
            int nodeCount,
            ulong doneMask,
            out int nodeIndex)
        {
            nodeIndex = -1;
            if (!nodes.IsCreated)
                return false;

            nodeIndex = FindNodeIndexByDoneMask(nodes, nodeCount, doneMask);
            return true;
        }

        private static int FindNodeIndexByDoneMask(NativeArray<QuestNodeDTO> nodes, int nodeCount, ulong doneMask)
        {
            ulong completionMask = doneMask != 0UL ? doneMask : 0UL;
            int limit = math.min(nodeCount, nodes.Length);
            for (int i = 0; i < limit; i++)
            {
                if (nodes[i].CompletionMask == completionMask)
                    return i;
            }

            return -1;
        }

        private static ulong BuildAllDoneMask(int nodeCount)
        {
            int count = math.min(nodeCount, 64);
            ulong mask = 0UL;
            for (int i = 0; i < count; i++)
                mask |= 1UL << i;

            return mask;
        }
    }

    /// <summary>
    /// Zero-allocation span parser for quest_logic_overrides.csv.
    /// </summary>
    #if UNITY_EDITOR
    public static class QuestDagCsvOverrideIngestor
    {
        private const int CsvScratchCharCapacity = 64 * 1024;
        private const int CsvOverridePatchCapacity = QuestDagRuntimeConstants.DefaultNodeCapacity;
        private static readonly ulong CsvOverrideApplyMutationGuardMask =
            CsvOverrideMutationGuardBit(BufferID.QuestDagNodeRuntime) |
            CsvOverrideMutationGuardBit(BufferID.QuestDagRequiredItemHashes) |
            CsvOverrideMutationGuardBit(BufferID.QuestDagRequiredItemQuantities) |
            CsvOverrideMutationGuardBit(BufferID.QuestDagCounters) |
            CsvOverrideMutationGuardBit(BufferID.QuestDagCsvMonitor);
        private static readonly char[] s_csvScratch = new char[CsvScratchCharCapacity]; // EDITOR COLD ALLOC: char[64k] - quest override CSV scratch, no full-file managed CSV string - owner: QuestDagCsvOverrideIngestor
        private static readonly CsvOverridePatch[] s_patchScratch = new CsvOverridePatch[CsvOverridePatchCapacity]; // EDITOR COLD ALLOC: staged quest CSV patches; avoids parsing under DataVault writer guard
        private static int s_csvScratchBusy;

        /// <summary>
        /// Monitors file timestamp and applies changed override rows. File read is cold/editor; row parser is span-only.
        /// </summary>
        public static bool TryApplyOverridesFromFile(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            string path,
            out int appliedRows)
        {
            appliedRows = 0;
            if (!TryReadCsvMonitorWriteTicks(vault, ref handles, out long previousWriteTicks) ||
                string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
            {
                return false;
            }

            long writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
            if (previousWriteTicks == writeTicks)
                return false;

            if (!TryAcquireCsvScratch())
                return false;

            try
            {
                if (!TryReadCsvIntoScratch(path, out int charCount))
                    return false;

                return TryApplyOverridesInternal(
                    vault,
                    ref handles,
                    new ReadOnlySpan<char>(s_csvScratch, 0, charCount),
                    writeTicks,
                    patchCsvMonitor: true,
                    out appliedRows);
            }
            finally
            {
                ReleaseCsvScratch();
            }
        }

        private static bool TryReadCsvIntoScratch(string path, out int charCount)
        {
            charCount = 0;
            try
            {
                using StreamReader reader = new StreamReader(path);
                while (charCount < s_csvScratch.Length)
                {
                    int delta = reader.Read(s_csvScratch, charCount, s_csvScratch.Length - charCount);
                    if (delta <= 0)
                        return charCount > 0;

                    charCount += delta;
                }

                return reader.Peek() < 0;
            }
            catch (IOException)
            {
                charCount = 0;
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                charCount = 0;
                return false;
            }
        }

        /// <summary>
        /// Parses rows: node_hash,item_hash,quantity,target_timestamp,faction_delta,faction_threshold.
        /// </summary>
        public static bool TryApplyOverrides(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            ReadOnlySpan<char> csv,
            out int appliedRows)
        {
            appliedRows = 0;
            if (!TryAcquireCsvScratch())
                return false;

            try
            {
                return TryApplyOverridesInternal(
                    vault,
                    ref handles,
                    csv,
                    writeTicks: 0L,
                    patchCsvMonitor: false,
                    out appliedRows);
            }
            finally
            {
                ReleaseCsvScratch();
            }
        }

        private static bool TryApplyOverridesInternal(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            ReadOnlySpan<char> csv,
            long writeTicks,
            bool patchCsvMonitor,
            out int appliedRows)
        {
            appliedRows = 0;
            if (csv.Length <= 0)
                return false;

            if (!TryBuildCsvOverridePatches(vault, ref handles, csv, out int patchCount))
                return false;

            if (!TryCommitCsvOverridePatches(vault, ref handles, patchCount, writeTicks, patchCsvMonitor))
            {
                return false;
            }

            appliedRows = patchCount;
            return patchCount > 0;
        }

        private static bool TryBuildCsvOverridePatches(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            ReadOnlySpan<char> csv,
            out int patchCount)
        {
            patchCount = 0;
            if (!QuestDagVault.TryReadQuestDagBuffer(
                    vault,
                    in handles.Counters,
                    BufferID.QuestDagCounters,
                    QuestDagRuntimeConstants.CounterCount,
                    out NativeArray<int>.ReadOnly counters) ||
                !QuestDagVault.TryReadQuestDagBuffer(
                    vault,
                    in handles.Nodes,
                    BufferID.QuestDagNodes,
                    1,
                    out NativeArray<QuestNodeDTO>.ReadOnly nodes) ||
                !QuestDagVault.TryReadQuestDagBuffer(
                    vault,
                    in handles.NodeRuntime,
                    BufferID.QuestDagNodeRuntime,
                    1,
                    out NativeArray<QuestNodeRuntimeDTO>.ReadOnly nodeRuntime))
            {
                return false;
            }

            int nodeCountIndex = (int)QuestDagRuntimeConstants.CounterSlot.NodeCount;
            int nodeCount = math.min(counters[nodeCountIndex], math.min(handles.NodeCapacity, nodes.Length));
            nodeCount = math.min(nodeCount, nodeRuntime.Length);

            int offset = 0;
            while (offset < csv.Length)
            {
                ReadOnlySpan<char> line = NextLine(csv, ref offset).Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                if (line[0] < '0' || line[0] > '9')
                    continue;

                if (!TryReadField(ref line, out ReadOnlySpan<char> nodeField) ||
                    !TryParseUInt32(nodeField, out uint nodeHash))
                {
                    continue;
                }

                int nodeIndex = FindNodeIndexByHash(nodes, nodeCount, nodeHash);
                if (nodeIndex < 0)
                    continue;

                QuestNodeRuntimeDTO runtime = nodeRuntime[nodeIndex];
                CsvOverridePatch patch = default;
                patch.NodeIndex = nodeIndex;
                patch.RequiredItemSlot = -1;
                patch.Runtime = runtime;

                if (TryReadField(ref line, out ReadOnlySpan<char> itemField) &&
                    TryParseUInt32(itemField, out uint itemHash) &&
                    itemHash != 0u)
                {
                    int slot = runtime.RequiredItemStart >= 0 ? runtime.RequiredItemStart : nodeIndex;
                    if ((uint)slot < (uint)handles.ItemLinkCapacity)
                    {
                        patch.RequiredItemSlot = slot;
                        patch.RequiredItemHash = itemHash;
                        patch.WriteRequiredItemHash = 1;
                        runtime.RequiredItemStart = slot;
                        runtime.RequiredItemCount = 1;
                        runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresInventory;
                    }
                }

                if (TryReadField(ref line, out ReadOnlySpan<char> quantityField) &&
                    TryParseInt(quantityField, out int quantity) &&
                    runtime.RequiredItemCount > 0)
                {
                    int slot = runtime.RequiredItemStart;
                    if ((uint)slot < (uint)handles.ItemLinkCapacity)
                    {
                        patch.RequiredItemSlot = slot;
                        patch.RequiredItemQuantity = math.max(1, quantity);
                        patch.WriteRequiredItemQuantity = 1;
                    }
                }

                if (TryReadField(ref line, out ReadOnlySpan<char> timestampField) &&
                    TryParseUInt64(timestampField, out ulong timestamp))
                {
                    runtime.TargetTimestamp = timestamp;
                    if (timestamp != 0UL)
                        runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresTimestamp;
                }

                if (TryReadField(ref line, out ReadOnlySpan<char> deltaField) &&
                    TryParseFloat(deltaField, out float delta))
                {
                    runtime.ReputationDelta = delta;
                    if (math.abs(delta) > 0.0001f)
                    {
                        runtime.FactionId = runtime.FactionId == ushort.MaxValue ? (ushort)0 : runtime.FactionId;
                        runtime.Flags |= (ushort)QuestDagNodeFlags.AppliesFactionDelta;
                    }
                }

                if (TryReadField(ref line, out ReadOnlySpan<char> thresholdField) &&
                    TryParseFloat(thresholdField, out float threshold))
                {
                    runtime.ReputationThreshold = threshold;
                    runtime.FactionId = runtime.FactionId == ushort.MaxValue ? (ushort)0 : runtime.FactionId;
                    runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresFactionThreshold;
                }

                if ((uint)patchCount >= (uint)s_patchScratch.Length)
                    return false;

                patch.Runtime = runtime;
                s_patchScratch[patchCount] = patch;
                patchCount++;
            }

            return true;
        }

        private static int FindNodeIndexByHash(NativeArray<QuestNodeDTO>.ReadOnly nodes, int nodeCount, uint nodeHash)
        {
            int limit = math.min(nodeCount, nodes.Length);
            for (int i = 0; i < limit; i++)
            {
                if (nodes[i].NodeHash == nodeHash)
                    return i;
            }

            return -1;
        }

        private static bool TryCommitCsvOverridePatches(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int patchCount,
            long writeTicks,
            bool patchCsvMonitor)
        {
            if (patchCount < 0)
                return false;

            bool requiresPatchBuffers = patchCount > 0;
            bool requiresCsvMonitor = patchCsvMonitor && patchCount > 0;
            if (!TryAcquireCsvOverrideApplyBuffers(
                    vault,
                    ref handles,
                    requiresPatchBuffers,
                    requiresCsvMonitor,
                    out CsvOverrideApplyBuffers buffers))
            {
                return false;
            }

            try
            {
                if (!TryValidateCsvOverrideApplyBuffers(in buffers, requiresPatchBuffers, requiresCsvMonitor))
                    return false;

                for (int i = 0; i < patchCount; i++)
                {
                    CsvOverridePatch patch = s_patchScratch[i];
                    int nodeIndex = patch.NodeIndex;
                    if ((uint)nodeIndex >= (uint)buffers.NodeRuntime.Length)
                        return false;

                    int slot = patch.RequiredItemSlot;
                    if (patch.WriteRequiredItemHash != 0)
                    {
                        if ((uint)slot >= (uint)buffers.RequiredItemHashes.Length)
                            return false;
                    }

                    if (patch.WriteRequiredItemQuantity != 0)
                    {
                        if ((uint)slot >= (uint)buffers.RequiredItemQuantities.Length)
                            return false;
                    }
                }

                for (int i = 0; i < patchCount; i++)
                {
                    CsvOverridePatch patch = s_patchScratch[i];
                    int slot = patch.RequiredItemSlot;
                    if (patch.WriteRequiredItemHash != 0)
                        buffers.RequiredItemHashes[slot] = patch.RequiredItemHash;

                    if (patch.WriteRequiredItemQuantity != 0)
                        buffers.RequiredItemQuantities[slot] = patch.RequiredItemQuantity;

                    buffers.NodeRuntime[patch.NodeIndex] = patch.Runtime;
                }

                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.LastCsvRowsApplied] = patchCount;
                if (requiresCsvMonitor)
                {
                    buffers.CsvMonitor[0] = writeTicks;
                    buffers.CsvMonitor[1] = patchCount;
                }

                return true;
            }
            finally
            {
                ReleaseCsvOverrideApplyGuard(vault);
            }
        }

        private static bool TryAcquireCsvOverrideApplyBuffers(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            bool requiresPatchBuffers,
            bool requiresCsvMonitor,
            out CsvOverrideApplyBuffers buffers)
        {
            buffers = default;
            if (vault == null ||
                vault.IsAllocationLocked ||
                vault.IsCompactionFenceActive ||
                !vault.TryAcquireMutationGuard(CsvOverrideApplyMutationGuardMask))
            {
                return false;
            }

            bool resolved =
                TryResolveCsvOverrideBuffer(
                        vault,
                        in handles.Counters,
                        BufferID.QuestDagCounters,
                        QuestDagRuntimeConstants.CounterCount,
                        out buffers.Counters) &&
                (!requiresCsvMonitor ||
                    TryResolveCsvOverrideBuffer(
                        vault,
                        in handles.CsvMonitor,
                        BufferID.QuestDagCsvMonitor,
                        2,
                        out buffers.CsvMonitor)) &&
                (!requiresPatchBuffers ||
                    (TryResolveCsvOverrideBuffer(
                            vault,
                            in handles.NodeRuntime,
                            BufferID.QuestDagNodeRuntime,
                            1,
                            out buffers.NodeRuntime) &&
                        TryResolveCsvOverrideBuffer(
                            vault,
                            in handles.RequiredItemHashes,
                            BufferID.QuestDagRequiredItemHashes,
                            1,
                            out buffers.RequiredItemHashes) &&
                        TryResolveCsvOverrideBuffer(
                            vault,
                            in handles.RequiredItemQuantities,
                            BufferID.QuestDagRequiredItemQuantities,
                            1,
                            out buffers.RequiredItemQuantities)));

            if (resolved)
            {
                return true;
            }

            vault.ReleaseMutationGuard(CsvOverrideApplyMutationGuardMask);
            buffers = default;
            return false;
        }

        private static bool TryResolveCsvOverrideBuffer<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            out NativeArray<T> buffer) where T : struct
        {
            buffer = default;
            if (requiredLength <= 0 ||
                handle.BufferID != unchecked((uint)(int)bufferId) ||
                handle.SystemID != (uint)SystemID.QuestDag ||
                handle.Generation == 0u ||
                !vault.TryResolveHandle(in handle, out buffer) ||
                !buffer.IsCreated ||
                buffer.Length < requiredLength)
            {
                buffer = default;
                return false;
            }

            return true;
        }

        private static void ReleaseCsvOverrideApplyGuard(IDataVault vault)
        {
            vault?.ReleaseMutationGuard(CsvOverrideApplyMutationGuardMask);
        }

        private static ulong CsvOverrideMutationGuardBit(BufferID bufferId)
        {
            int bitIndex = unchecked((int)((uint)(int)bufferId & 63u));
            return 1UL << bitIndex;
        }

        private static bool TryValidateCsvOverrideApplyBuffers(
            in CsvOverrideApplyBuffers buffers,
            bool requiresPatchBuffers,
            bool requiresCsvMonitor)
        {
            if (!buffers.Counters.IsCreated)
                return false;

            if (requiresCsvMonitor && !buffers.CsvMonitor.IsCreated)
                return false;

            return !requiresPatchBuffers ||
                   (buffers.NodeRuntime.IsCreated &&
                    buffers.RequiredItemHashes.IsCreated &&
                    buffers.RequiredItemQuantities.IsCreated);
        }

        private static bool TryReadCsvMonitorWriteTicks(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            out long writeTicks)
        {
            writeTicks = 0L;
            if (!QuestDagVault.TryReadQuestDagBuffer(
                    vault,
                    in handles.CsvMonitor,
                    BufferID.QuestDagCsvMonitor,
                    2,
                    out NativeArray<long>.ReadOnly csvMonitor))
            {
                return false;
            }

            writeTicks = csvMonitor[0];
            return true;
        }

        private static bool TryAcquireCsvScratch()
        {
            return Interlocked.CompareExchange(ref s_csvScratchBusy, 1, 0) == 0;
        }

        private static void ReleaseCsvScratch()
        {
            Volatile.Write(ref s_csvScratchBusy, 0);
        }

        private struct CsvOverridePatch
        {
            public int NodeIndex;
            public int RequiredItemSlot;
            public uint RequiredItemHash;
            public int RequiredItemQuantity;
            public QuestNodeRuntimeDTO Runtime;
            public byte WriteRequiredItemHash;
            public byte WriteRequiredItemQuantity;
        }

        private ref struct CsvOverrideApplyBuffers
        {
            public NativeArray<QuestNodeRuntimeDTO> NodeRuntime;
            public NativeArray<uint> RequiredItemHashes;
            public NativeArray<int> RequiredItemQuantities;
            public NativeArray<int> Counters;
            public NativeArray<long> CsvMonitor;
        }

        private static ReadOnlySpan<char> NextLine(ReadOnlySpan<char> csv, ref int offset)
        {
            int start = offset;
            while (offset < csv.Length && csv[offset] != '\n' && csv[offset] != '\r')
                offset++;

            ReadOnlySpan<char> line = csv.Slice(start, offset - start);
            while (offset < csv.Length && (csv[offset] == '\n' || csv[offset] == '\r'))
                offset++;

            return line;
        }

        private static bool TryReadField(ref ReadOnlySpan<char> line, out ReadOnlySpan<char> field)
        {
            line = line.TrimStart();
            if (line.Length <= 0)
            {
                field = default;
                return false;
            }

            int comma = line.IndexOf(',');
            if (comma < 0)
            {
                field = line.Trim();
                line = ReadOnlySpan<char>.Empty;
                return true;
            }

            field = line.Slice(0, comma).Trim();
            line = line.Slice(comma + 1);
            return true;
        }

        private static bool TryParseUInt32(ReadOnlySpan<char> text, out uint value)
        {
            value = 0u;
            if (text.Length <= 0)
                return false;

            int index = 0;
            bool hex = text.Length > 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X');
            if (hex)
                index = 2;

            for (; index < text.Length; index++)
            {
                int digit = ParseDigit(text[index], hex);
                if (digit < 0)
                    return false;

                value = hex ? (value << 4) + (uint)digit : (value * 10u) + (uint)digit;
            }

            return true;
        }

        private static bool TryParseUInt64(ReadOnlySpan<char> text, out ulong value)
        {
            value = 0UL;
            if (text.Length <= 0)
                return false;

            int index = 0;
            bool hex = text.Length > 2 && text[0] == '0' && (text[1] == 'x' || text[1] == 'X');
            if (hex)
                index = 2;

            for (; index < text.Length; index++)
            {
                int digit = ParseDigit(text[index], hex);
                if (digit < 0)
                    return false;

                value = hex ? (value << 4) + (ulong)digit : (value * 10UL) + (ulong)digit;
            }

            return true;
        }

        private static bool TryParseInt(ReadOnlySpan<char> text, out int value)
        {
            value = 0;
            if (text.Length <= 0)
                return false;

            bool negative = text[0] == '-';
            int index = negative ? 1 : 0;
            for (; index < text.Length; index++)
            {
                int digit = ParseDigit(text[index], false);
                if (digit < 0)
                    return false;

                value = (value * 10) + digit;
            }

            if (negative)
                value = -value;
            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<char> text, out float value)
        {
            value = 0f;
            if (text.Length <= 0)
                return false;

            bool negative = text[0] == '-';
            int index = negative ? 1 : 0;
            float scale = 1f;
            bool fractional = false;
            for (; index < text.Length; index++)
            {
                char c = text[index];
                if (c == '.')
                {
                    if (fractional)
                        return false;
                    fractional = true;
                    continue;
                }

                int digit = ParseDigit(c, false);
                if (digit < 0)
                    return false;

                if (fractional)
                {
                    scale *= 0.1f;
                    value += digit * scale;
                }
                else
                {
                    value = (value * 10f) + digit;
                }
            }

            if (negative)
                value = -value;
            return math.isfinite(value);
        }

        private static int ParseDigit(char c, bool hex)
        {
            if (c >= '0' && c <= '9')
                return c - '0';
            if (!hex)
                return -1;
            if (c >= 'a' && c <= 'f')
                return 10 + c - 'a';
            if (c >= 'A' && c <= 'F')
                return 10 + c - 'A';
            return -1;
        }

    }
    #endif
}
