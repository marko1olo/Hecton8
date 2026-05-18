using System;
using System.IO;
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
            if (!QuestDagVault.TryResolveBuffers(vault, ref handles, out QuestDagBuffers buffers))
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
                    nodeCount > buffers.Nodes.Length ||
                    triggerCount > buffers.TriggerVolumes.Length)
                {
                    stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                    return false;
                }

                ClearDagBuffers(buffers);
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
                    if ((uint)stateChunk >= (uint)buffers.GlobalStateMasks.Length)
                    {
                        stats.Flags = (uint)QuestDagLoadFlags.BinaryUnreadable;
                        return false;
                    }

                    ulong completionMask = stateMask & 0xAAAAAAAAAAAAAAAAUL;
                    buffers.Nodes[i] = new QuestNodeDTO
                    {
                        NodeHash = nodeHash,
                        RequiredStateHash = loreHash,
                        PrerequisiteMask = prerequisiteMask,
                        CompletionMask = completionMask,
                        _pad0 = slot,
                        _pad1 = ((uint)topoIndex << 16) | prerequisiteCount
                    };
                    buffers.NodeRuntime[i] = new QuestNodeRuntimeDTO
                    {
                        TargetTimestamp = 0UL,
                        ReputationDelta = 0f,
                        ReputationThreshold = 0f,
                        StateChunk = stateChunk,
                        TriggerIndex = triggerSpan > 0 ? i : -1,
                        RequiredItemStart = i,
                        RequiredItemCount = 0,
                        FactionId = ushort.MaxValue,
                        Flags = triggerSpan > 0 ? (ushort)QuestDagNodeFlags.RequiresTrigger : (ushort)QuestDagNodeFlags.None,
                        _pad0 = 0u
                    };
                }

                stream.Position = triggerOffset;
                for (int i = 0; i < triggerCount; i++)
                {
                    uint triggerHash = reader.ReadUInt32();
                    uint triggerTypeHash = reader.ReadUInt32();
                    ulong doneMask = reader.ReadUInt64();
                    int nodeIndex = FindNodeIndexByDoneMask(buffers.Nodes, (int)nodeCount, doneMask);
                    if (nodeIndex < 0)
                        continue;

                    QuestNodeRuntimeDTO runtime = buffers.NodeRuntime[nodeIndex];
                    runtime.TriggerIndex = i;
                    runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresTrigger;
                    if (triggerTypeHash == 0x1D88D039u)
                    {
                        runtime.RequiredItemStart = nodeIndex;
                        runtime.RequiredItemCount = 1;
                        runtime.Flags |= (ushort)QuestDagNodeFlags.RequiresInventory;
                        buffers.RequiredItemHashes[nodeIndex] = triggerHash;
                        buffers.RequiredItemQuantities[nodeIndex] = 1;
                    }

                    buffers.NodeRuntime[nodeIndex] = runtime;
                    buffers.TriggerNodeIndices[i] = nodeIndex;
                    buffers.TriggerVolumes[i] = new TriggerVolumeDTO
                    {
                        AUP = new double3(i * 12.5d, 0d, 0d),
                        Radius = 8f,
                        RequiredNodeHash = buffers.Nodes[nodeIndex].NodeHash,
                        _pad0 = triggerHash,
                        _pad1 = triggerTypeHash
                    };
                }

                int noTriggerCount = PopulateNoTriggerNodeIndices(
                    buffers.NodeRuntime,
                    buffers.NoTriggerNodeIndices,
                    (int)nodeCount);
                WriteCounters(buffers, (int)nodeCount, (int)triggerCount, noTriggerCount);
                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.LastLoadSourceHash] = unchecked((int)QuestDagRuntimeConstants.OshinoBinarySourceHash);

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
            if (!QuestDagVault.TryResolveBuffers(vault, ref handles, out QuestDagBuffers buffers))
                return;

            ClearDagBuffers(buffers);
            int stateNodeCapacity = buffers.GlobalStateMasks.IsCreated ? buffers.GlobalStateMasks.Length << 6 : 0;
            int nodeCount = math.clamp(
                requestedNodeCount,
                1,
                math.min(stateNodeCapacity, math.min(buffers.Nodes.Length, buffers.TriggerVolumes.Length)));
            int noTriggerCount = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                int chunk = i >> 6;
                int bit = i & 63;
                ulong doneMask = 1UL << bit;
                ulong prerequisiteMask = bit > 0 ? 1UL << (bit - 1) : 0UL;
                uint nodeHash = unchecked(0x51000000u + (uint)i);
                bool requiresTrigger = (i & 3) != 3;

                buffers.Nodes[i] = new QuestNodeDTO
                {
                    NodeHash = nodeHash,
                    RequiredStateHash = unchecked(0x71000000u + (uint)i),
                    PrerequisiteMask = prerequisiteMask,
                    CompletionMask = doneMask,
                    _pad0 = 0u,
                    _pad1 = 0u
                };
                buffers.NodeRuntime[i] = new QuestNodeRuntimeDTO
                {
                    TargetTimestamp = (i & 7) == 7 ? (ulong)(120 + i) : 0UL,
                    ReputationDelta = (i & 15) == 0 ? 0.25f : 0f,
                    ReputationThreshold = (i & 15) == 8 ? 1f : 0f,
                    StateChunk = chunk,
                    TriggerIndex = requiresTrigger ? i : -1,
                    RequiredItemStart = i,
                    RequiredItemCount = (i & 5) == 1 ? 1 : 0,
                    FactionId = (i & 15) == 0 || (i & 15) == 8 ? (ushort)0 : ushort.MaxValue,
                    Flags = BuildMockFlags(requiresTrigger, i),
                    _pad0 = 0u
                };

                if (!requiresTrigger)
                {
                    if ((uint)noTriggerCount < (uint)buffers.NoTriggerNodeIndices.Length)
                        buffers.NoTriggerNodeIndices[noTriggerCount] = i;
                    noTriggerCount++;
                }

                buffers.RequiredItemHashes[i] = unchecked(0x49000000u + (uint)(i & 31));
                buffers.RequiredItemQuantities[i] = (i & 5) == 1 ? 1 + (i & 3) : 0;
                buffers.TriggerNodeIndices[i] = i;
                buffers.TriggerVolumes[i] = new TriggerVolumeDTO
                {
                    AUP = new double3((i % 100) * 25d, 0d, (i / 100) * 25d),
                    Radius = 10f,
                    RequiredNodeHash = nodeHash,
                    _pad0 = unchecked(0x54000000u + (uint)i),
                    _pad1 = 0u
                };
            }

            WriteCounters(buffers, nodeCount, nodeCount, noTriggerCount);
            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.LastLoadSourceHash] = unchecked((int)QuestDagRuntimeConstants.EmergencyMockSourceHash);

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

        private static void ClearDagBuffers(QuestDagBuffers buffers)
        {
            Clear(buffers.GlobalStateMasks);
            Clear(buffers.OldStateMasks);
            Clear(buffers.Nodes);
            Clear(buffers.NodeRuntime);
            Clear(buffers.TriggerVolumes);
            Clear(buffers.RequiredItemHashes);
            Clear(buffers.RequiredItemQuantities);
            Clear(buffers.PlayerItemHashes);
            Clear(buffers.PlayerItemQuantities);
            Clear(buffers.FactionStandings);
            Clear(buffers.TriggerNodeIndices);
            Clear(buffers.NoTriggerNodeIndices);
        }

        private static void Clear<T>(NativeArray<T> values)
            where T : struct
        {
            if (!values.IsCreated)
                return;

            for (int i = 0; i < values.Length; i++)
                values[i] = default;
        }

        private static void WriteCounters(QuestDagBuffers buffers, int nodeCount, int triggerCount, int noTriggerCount)
        {
            if (!buffers.Counters.IsCreated || buffers.Counters.Length < QuestDagRuntimeConstants.CounterCount)
                return;

            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.NodeCount] = nodeCount;
            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.TriggerCount] = triggerCount;
            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.NoTriggerNodeCount] = noTriggerCount;
            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.StateChunkCount] = math.max(
                1,
                buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.StateChunkCount]);
            int spatialVersionIndex = (int)QuestDagRuntimeConstants.CounterSlot.SpatialHashVersion;
            buffers.Counters[spatialVersionIndex] = buffers.Counters[spatialVersionIndex] + 1;
        }

        private static int PopulateNoTriggerNodeIndices(
            NativeArray<QuestNodeRuntimeDTO> runtime,
            NativeArray<int> noTriggerNodeIndices,
            int nodeCount)
        {
            int count = 0;
            for (int i = 0; i < nodeCount; i++)
            {
                if ((runtime[i].Flags & (ushort)QuestDagNodeFlags.RequiresTrigger) == 0)
                {
                    if ((uint)count < (uint)noTriggerNodeIndices.Length)
                        noTriggerNodeIndices[count] = i;
                    count++;
                }
            }

            return count;
        }

        private static int FindNodeIndexByDoneMask(NativeArray<QuestNodeDTO> nodes, int nodeCount, ulong doneMask)
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
    public static class QuestDagCsvOverrideIngestor
    {
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
            if (!QuestDagVault.TryResolveBuffers(vault, ref handles, out QuestDagBuffers buffers) ||
                !buffers.CsvMonitor.IsCreated ||
                string.IsNullOrWhiteSpace(path) ||
                !File.Exists(path))
            {
                return false;
            }

            long writeTicks = File.GetLastWriteTimeUtc(path).Ticks;
            if (buffers.CsvMonitor[0] == writeTicks)
                return false;

            string csv = File.ReadAllText(path);
            bool applied = TryApplyOverrides(vault, ref handles, csv.AsSpan(), out appliedRows);
            if (applied)
            {
                buffers.CsvMonitor[0] = writeTicks;
                buffers.CsvMonitor[1] = appliedRows;
            }

            return applied;
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
                !QuestDagVault.TryResolveBuffers(vault, ref handles, out QuestDagBuffers buffers))
            {
                return false;
            }

            int offset = 0;
            int nodeCount = ReadCounter(buffers.Counters, QuestDagRuntimeConstants.CounterSlot.NodeCount);
            nodeCount = math.min(nodeCount, math.min(buffers.Nodes.Length, buffers.NodeRuntime.Length));
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

                int nodeIndex = FindNodeIndex(buffers.Nodes, nodeCount, nodeHash);
                if (nodeIndex < 0)
                    continue;

                QuestNodeRuntimeDTO runtime = buffers.NodeRuntime[nodeIndex];
                if (TryReadField(ref line, out ReadOnlySpan<char> itemField) &&
                    TryParseUInt32(itemField, out uint itemHash) &&
                    itemHash != 0u)
                {
                    int slot = runtime.RequiredItemStart >= 0 ? runtime.RequiredItemStart : nodeIndex;
                    if ((uint)slot < (uint)buffers.RequiredItemHashes.Length &&
                        (uint)slot < (uint)buffers.RequiredItemQuantities.Length)
                    {
                        buffers.RequiredItemHashes[slot] = itemHash;
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
                    if ((uint)slot < (uint)buffers.RequiredItemQuantities.Length)
                        buffers.RequiredItemQuantities[slot] = math.max(1, quantity);
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

                buffers.NodeRuntime[nodeIndex] = runtime;
                appliedRows++;
            }

            buffers.Counters[(int)QuestDagRuntimeConstants.CounterSlot.LastCsvRowsApplied] = appliedRows;
            return appliedRows > 0;
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

        private static int FindNodeIndex(NativeArray<QuestNodeDTO> nodes, int nodeCount, uint nodeHash)
        {
            for (int i = 0; i < nodeCount; i++)
            {
                if (nodes[i].NodeHash == nodeHash)
                    return i;
            }

            return -1;
        }

        private static int ReadCounter(NativeArray<int> counters, QuestDagRuntimeConstants.CounterSlot slot)
        {
            int index = (int)slot;
            return counters.IsCreated && (uint)index < (uint)counters.Length ? counters[index] : 0;
        }
    }
}
