using System;
using System.IO;
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

                if (!ClearDagBuffers(vault, ref handles))
                {
                    stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                    return false;
                }

                stream.Position = nodeOffset;
                for (int i = 0; i < nodeCount; i++)
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

                    if (!QuestDagVault.TryWriteQuestDagValue(vault, in handles.Nodes, BufferID.QuestDagNodes, handles.NodeCapacity, i, node) ||
                        !QuestDagVault.TryWriteQuestDagValue(vault, in handles.NodeRuntime, BufferID.QuestDagNodeRuntime, handles.NodeCapacity, i, runtime))
                    {
                        stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                        return false;
                    }
                }

                stream.Position = triggerOffset;
                for (int i = 0; i < triggerCount; i++)
                {
                    uint triggerHash = reader.ReadUInt32();
                    uint triggerTypeHash = reader.ReadUInt32();
                    ulong doneMask = reader.ReadUInt64();
                    if (!TryFindNodeIndexByDoneMask(vault, ref handles, (int)nodeCount, doneMask, out int nodeIndex))
                    {
                        stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                        return false;
                    }

                    if (nodeIndex < 0)
                        continue;

                    if (!QuestDagVault.TryReadQuestDagValue(vault, in handles.NodeRuntime, BufferID.QuestDagNodeRuntime, handles.NodeCapacity, nodeIndex, out QuestNodeRuntimeDTO runtime) ||
                        !QuestDagVault.TryReadQuestDagValue(vault, in handles.Nodes, BufferID.QuestDagNodes, handles.NodeCapacity, nodeIndex, out QuestNodeDTO node))
                    {
                        stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                        return false;
                    }

                    runtime.TriggerIndex = i;
                    runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresTrigger;
                    if (triggerTypeHash == 0x1D88D039u)
                    {
                        runtime.RequiredItemStart = nodeIndex;
                        runtime.RequiredItemCount = 1;
                        runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresInventory;
                        if (!QuestDagVault.TryWriteQuestDagValue(vault, in handles.RequiredItemHashes, BufferID.QuestDagRequiredItemHashes, handles.ItemLinkCapacity, nodeIndex, triggerHash) ||
                            !QuestDagVault.TryWriteQuestDagValue(vault, in handles.RequiredItemQuantities, BufferID.QuestDagRequiredItemQuantities, handles.ItemLinkCapacity, nodeIndex, 1))
                        {
                            stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                            return false;
                        }
                    }

                    TriggerVolumeDTO volume = default;
                    volume.AUP = new double3(i * 12.5d, 0d, 0d);
                    volume.Radius = 8f;
                    volume.RequiredNodeHash = node.NodeHash;
                    volume._pad0 = triggerHash;
                    volume._pad1 = triggerTypeHash;

                    if (!QuestDagVault.TryWriteQuestDagValue(vault, in handles.NodeRuntime, BufferID.QuestDagNodeRuntime, handles.NodeCapacity, nodeIndex, runtime) ||
                        !QuestDagVault.TryWriteQuestDagValue(vault, in handles.TriggerNodeIndices, BufferID.QuestDagTriggerNodeIndices, handles.TriggerCapacity, i, nodeIndex) ||
                        !QuestDagVault.TryWriteQuestDagValue(vault, in handles.TriggerVolumes, BufferID.QuestDagTriggerVolumes, handles.TriggerCapacity, i, volume))
                    {
                        stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                        return false;
                    }
                }

                if (!TryPopulateNoTriggerNodeIndices(vault, ref handles, (int)nodeCount, out int noTriggerCount))
                {
                    stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                    return false;
                }

                WriteCounters(
                    vault,
                    ref handles,
                    (int)nodeCount,
                    (int)triggerCount,
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

            if (!ClearDagBuffers(vault, ref handles))
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
                    if ((uint)noTriggerCount < (uint)handles.NodeCapacity &&
                        !QuestDagVault.TryWriteQuestDagValue(vault, in handles.NoTriggerNodeIndices, BufferID.QuestDagNoTriggerNodeIndices, handles.NodeCapacity, noTriggerCount, i))
                    {
                        return;
                    }

                    noTriggerCount++;
                }

                TriggerVolumeDTO volume = default;
                volume.AUP = new double3((i % 100) * 25d, 0d, (i / 100) * 25d);
                volume.Radius = 10f;
                volume.RequiredNodeHash = nodeHash;
                volume._pad0 = unchecked(0x54000000u + (uint)i);
                volume._pad1 = 0u;

                if (!QuestDagVault.TryWriteQuestDagValue(vault, in handles.Nodes, BufferID.QuestDagNodes, handles.NodeCapacity, i, node) ||
                    !QuestDagVault.TryWriteQuestDagValue(vault, in handles.NodeRuntime, BufferID.QuestDagNodeRuntime, handles.NodeCapacity, i, runtime) ||
                    !QuestDagVault.TryWriteQuestDagValue(vault, in handles.RequiredItemHashes, BufferID.QuestDagRequiredItemHashes, handles.ItemLinkCapacity, i, unchecked(0x49000000u + (uint)(i & 31))) ||
                    !QuestDagVault.TryWriteQuestDagValue(vault, in handles.RequiredItemQuantities, BufferID.QuestDagRequiredItemQuantities, handles.ItemLinkCapacity, i, (i & 5) == 1 ? 1 + (i & 3) : 0) ||
                    !QuestDagVault.TryWriteQuestDagValue(vault, in handles.TriggerNodeIndices, BufferID.QuestDagTriggerNodeIndices, handles.TriggerCapacity, i, i) ||
                    !QuestDagVault.TryWriteQuestDagValue(vault, in handles.TriggerVolumes, BufferID.QuestDagTriggerVolumes, handles.TriggerCapacity, i, volume))
                {
                    return;
                }
            }

            WriteCounters(
                vault,
                ref handles,
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

        private static bool ClearDagBuffers(IDataVault vault, ref QuestDagBufferHandles handles)
        {
            return Clear(vault, in handles.GlobalStateMasks, BufferID.QuestDagGlobalStateMasks, handles.StateChunkCount) &&
                   Clear(vault, in handles.OldStateMasks, BufferID.QuestDagOldStateMasks, handles.StateChunkCount) &&
                   Clear(vault, in handles.Nodes, BufferID.QuestDagNodes, handles.NodeCapacity) &&
                   Clear(vault, in handles.NodeRuntime, BufferID.QuestDagNodeRuntime, handles.NodeCapacity) &&
                   Clear(vault, in handles.TriggerVolumes, BufferID.QuestDagTriggerVolumes, handles.TriggerCapacity) &&
                   Clear(vault, in handles.RequiredItemHashes, BufferID.QuestDagRequiredItemHashes, handles.ItemLinkCapacity) &&
                   Clear(vault, in handles.RequiredItemQuantities, BufferID.QuestDagRequiredItemQuantities, handles.ItemLinkCapacity) &&
                   Clear(vault, in handles.PlayerItemHashes, BufferID.QuestDagPlayerItemHashes, handles.PlayerItemCapacity) &&
                   Clear(vault, in handles.PlayerItemQuantities, BufferID.QuestDagPlayerItemQuantities, handles.PlayerItemCapacity) &&
                   Clear(vault, in handles.FactionStandings, BufferID.QuestDagFactionStandings, handles.FactionCapacity) &&
                   Clear(vault, in handles.TriggerNodeIndices, BufferID.QuestDagTriggerNodeIndices, handles.TriggerCapacity) &&
                   Clear(vault, in handles.NoTriggerNodeIndices, BufferID.QuestDagNoTriggerNodeIndices, handles.NodeCapacity);
        }

        private static bool Clear<T>(
            IDataVault vault,
            in VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength)
            where T : struct
        {
            if (!QuestDagVault.TryAcquireQuestDagWriteBuffer(vault, in handle, bufferId, requiredLength, out NativeArray<T> values))
                return false;

            try
            {
                for (int i = 0; i < values.Length; i++)
                    values[i] = default;

                return true;
            }
            finally
            {
                vault.ReleaseWriteLock(in handle, SystemID.QuestDag);
            }
        }

        private static void WriteCounters(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int nodeCount,
            int triggerCount,
            int noTriggerCount,
            int sourceHash)
        {
            if (!QuestDagVault.TryAcquireQuestDagWriteBuffer(
                    vault,
                    in handles.Counters,
                    BufferID.QuestDagCounters,
                    QuestDagRuntimeConstants.CounterCount,
                    out NativeArray<int> counters))
            {
                return;
            }

            try
            {
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
            finally
            {
                vault.ReleaseWriteLock(in handles.Counters, SystemID.QuestDag);
            }
        }

        private static bool TryPopulateNoTriggerNodeIndices(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int nodeCount,
            out int count)
        {
            count = 0;
            if (!QuestDagVault.TryReadQuestDagBuffer(
                    vault,
                    in handles.NodeRuntime,
                    BufferID.QuestDagNodeRuntime,
                    handles.NodeCapacity,
                    out NativeArray<QuestNodeRuntimeDTO>.ReadOnly runtime))
            {
                return false;
            }

            for (int i = 0; i < nodeCount; i++)
            {
                if ((runtime[i].Flags & (ushort)QuestDagNodeFlags.RequiresTrigger) == 0)
                {
                    if ((uint)count < (uint)handles.NodeCapacity &&
                        !QuestDagVault.TryWriteQuestDagValue(vault, in handles.NoTriggerNodeIndices, BufferID.QuestDagNoTriggerNodeIndices, handles.NodeCapacity, count, i))
                    {
                        return false;
                    }

                    count++;
                }
            }

            return true;
        }

        private static bool TryFindNodeIndexByDoneMask(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int nodeCount,
            ulong doneMask,
            out int nodeIndex)
        {
            nodeIndex = -1;
            if (!QuestDagVault.TryReadQuestDagBuffer(
                    vault,
                    in handles.Nodes,
                    BufferID.QuestDagNodes,
                    handles.NodeCapacity,
                    out NativeArray<QuestNodeDTO>.ReadOnly nodes))
            {
                return false;
            }

            nodeIndex = FindNodeIndexByDoneMask(nodes, nodeCount, doneMask);
            return true;
        }

        private static int FindNodeIndexByDoneMask(NativeArray<QuestNodeDTO>.ReadOnly nodes, int nodeCount, ulong doneMask)
        {
            ulong completionMask = doneMask != 0UL ? doneMask : 0UL;
            for (int i = 0; i < nodeCount; i++)
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
        private static readonly char[] s_csvScratch = new char[CsvScratchCharCapacity]; // EDITOR COLD ALLOC: char[64k] - quest override CSV scratch, no full-file managed CSV string - owner: QuestDagCsvOverrideIngestor

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

            if (!TryReadCsvIntoScratch(path, out int charCount))
                return false;

            bool applied = TryApplyOverrides(vault, ref handles, new ReadOnlySpan<char>(s_csvScratch, 0, charCount), out appliedRows);
            if (applied)
                PatchCsvMonitor(vault, ref handles, writeTicks, appliedRows);

            return applied;
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
            if (csv.Length <= 0 ||
                !QuestDagVault.TryReadQuestDagCounter(
                    vault,
                    ref handles,
                    QuestDagRuntimeConstants.CounterSlot.NodeCount,
                    out int nodeCount))
            {
                return false;
            }

            int offset = 0;
            nodeCount = math.min(nodeCount, handles.NodeCapacity);
            while (offset < csv.Length)
            {
                ReadOnlySpan<char> line = NextLine(csv, ref offset).Trim();
                if (line.Length == 0 || line[0] == '#')
                    continue;
                if ((line[0] < '0' || line[0] > '9') && line[0] != '0')
                    continue;

                if (!TryReadField(ref line, out ReadOnlySpan<char> nodeField) ||
                    !TryParseUInt32(nodeField, out uint nodeHash))
                {
                    continue;
                }

                if (!QuestDagVault.TryFindQuestDagNodeIndex(vault, ref handles, nodeCount, nodeHash, out int nodeIndex))
                    return false;

                if (nodeIndex < 0)
                    continue;

                if (!QuestDagVault.TryReadQuestDagValue(
                        vault,
                        in handles.NodeRuntime,
                        BufferID.QuestDagNodeRuntime,
                        handles.NodeCapacity,
                        nodeIndex,
                        out QuestNodeRuntimeDTO runtime))
                {
                    return false;
                }

                if (TryReadField(ref line, out ReadOnlySpan<char> itemField) &&
                    TryParseUInt32(itemField, out uint itemHash) &&
                    itemHash != 0u)
                {
                    int slot = runtime.RequiredItemStart >= 0 ? runtime.RequiredItemStart : nodeIndex;
                    if ((uint)slot < (uint)handles.ItemLinkCapacity)
                    {
                        if (!QuestDagVault.TryWriteQuestDagValue(
                                vault,
                                in handles.RequiredItemHashes,
                                BufferID.QuestDagRequiredItemHashes,
                                handles.ItemLinkCapacity,
                                slot,
                                itemHash))
                        {
                            return false;
                        }

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
                    if ((uint)slot < (uint)handles.ItemLinkCapacity &&
                        !QuestDagVault.TryWriteQuestDagValue(
                            vault,
                            in handles.RequiredItemQuantities,
                            BufferID.QuestDagRequiredItemQuantities,
                            handles.ItemLinkCapacity,
                            slot,
                            math.max(1, quantity)))
                    {
                        return false;
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

                if (!QuestDagVault.TryWriteQuestDagValue(
                        vault,
                        in handles.NodeRuntime,
                        BufferID.QuestDagNodeRuntime,
                        handles.NodeCapacity,
                        nodeIndex,
                        runtime))
                {
                    return false;
                }

                appliedRows++;
            }

            PatchLastCsvRowsApplied(vault, ref handles, appliedRows);
            return appliedRows > 0;
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

        private static void PatchCsvMonitor(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            long writeTicks,
            int appliedRows)
        {
            if (!QuestDagVault.TryAcquireQuestDagWriteBuffer(
                    vault,
                    in handles.CsvMonitor,
                    BufferID.QuestDagCsvMonitor,
                    2,
                    out NativeArray<long> csvMonitor))
            {
                return;
            }

            try
            {
                csvMonitor[0] = writeTicks;
                csvMonitor[1] = appliedRows;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.CsvMonitor, SystemID.QuestDag);
            }
        }

        private static void PatchLastCsvRowsApplied(
            IDataVault vault,
            ref QuestDagBufferHandles handles,
            int appliedRows)
        {
            if (!QuestDagVault.TryAcquireQuestDagWriteBuffer(
                    vault,
                    in handles.Counters,
                    BufferID.QuestDagCounters,
                    QuestDagRuntimeConstants.CounterCount,
                    out NativeArray<int> counters))
            {
                return;
            }

            try
            {
                counters[(int)QuestDagRuntimeConstants.CounterSlot.LastCsvRowsApplied] = appliedRows;
            }
            finally
            {
                vault.ReleaseWriteLock(in handles.Counters, SystemID.QuestDag);
            }
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
