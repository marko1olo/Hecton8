using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Quest
{
    /// <summary>
    /// Fixed binary quest node consumed by the Burst DAG resolver.
    /// </summary>
    /// <remarks>
    /// Layout: 0 uint node, 4 uint state/lore hash, 8 ulong prerequisite,
    /// 16 ulong completion, 24/28 explicit uint padding. Total 32 bytes.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Size = 32)]
    public struct QuestNodeDTO
    {
        public uint NodeHash;
        public uint RequiredStateHash;
        public ulong PrerequisiteMask;
        public ulong CompletionMask;
        public uint _pad0;
        public uint _pad1;
    }

    /// <summary>
    /// AUP-space narrative trigger volume. Absolute doubles stay intact until the local delta is formed.
    /// </summary>
    /// <remarks>
    /// Layout: 0 double3 AUP, 24 float radius, 28 uint node hash, 32/36 padding. Total 40 bytes.
    /// </remarks>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct TriggerVolumeDTO
    {
        public double3 AUP;
        public float Radius;
        public uint RequiredNodeHash;
        public uint _pad0;
        public uint _pad1;
    }

    /// <summary>
    /// Parallel metadata for <see cref="QuestNodeDTO"/>. Kept outside the 32-byte OSHINO node payload.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 40)]
    public struct QuestNodeRuntimeDTO
    {
        public ulong TargetTimestamp;
        public float ReputationDelta;
        public float ReputationThreshold;
        public int StateChunk;
        public int TriggerIndex;
        public int RequiredItemStart;
        public int RequiredItemCount;
        public ushort FactionId;
        public ushort Flags;
        public uint _pad0;
    }

    /// <summary>
    /// Fixed black-box sample for the last 300 resolver frames.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct QuestDagTelemetryEntry
    {
        public double ResolverComputeTimeMs;
        public ulong BitsFlipped;
        public uint Frame;
        public int ActiveNodesEvaluated;
        public ushort Iterations;
        public ushort Flags;
        public uint DeadlockNodeHash;
        public uint PlayerCellHash;
        public uint StateHash;
        public int SpatialCandidateCount;
        public uint _pad0;
    }

    /// <summary>
    /// Typed unmanaged state-change signal emitted after old/new mask XOR.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct StateChangedSignal : ISignal
    {
        [FieldOffset(0)] public ulong FlippedMask;
        [FieldOffset(8)] public ulong NewMask;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public int ChunkIndex;
        [FieldOffset(24)] public ushort Flags;
        [FieldOffset(26)] public ushort Sequence;
        [FieldOffset(28)] public uint SourceHash;
    }

    /// <summary>
    /// Local mock story event for resolver tests when upstream narrative buses are absent.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct MockStoryEventSignal : ISignal
    {
        [FieldOffset(0)] public ulong Timestamp;
        [FieldOffset(8)] public uint EventHash;
        [FieldOffset(12)] public uint NodeHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    /// <summary>
    /// Local mock player position signal used by blind DAG tests. Size: 64 bytes.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public partial struct MockPlayerPositionSignal : ISignal
    {
        [FieldOffset(0)] public double3 AUP;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Seed;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint _pad0;
        [FieldOffset(40)] public ulong _pad1;
        [FieldOffset(48)] public ulong _pad2;
        [FieldOffset(56)] public ulong _pad3;
    }

    /// <summary>
    /// Local mock inventory signal used by blind DAG tests.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public partial struct QuestDagMockItemAcquiredSignal : ISignal
    {
        [FieldOffset(0)] public ulong Timestamp;
        [FieldOffset(8)] public uint ItemHash;
        [FieldOffset(12)] public int Quantity;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] public uint _pad0;
        [FieldOffset(28)] public uint _pad1;
    }

    /// <summary>
    /// Cold binary-load result for OSHINO narrative archaeology.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Size = 48)]
    public struct QuestDagLoadStats
    {
        public ulong AllDoneMask;
        public uint GraphHash;
        public uint NodeCount;
        public uint TriggerCount;
        public uint EdgeCount;
        public uint TotalBytes;
        public uint Flags;
        public uint SourceHash;
        public uint _pad0;
        public uint _pad1;
    }

    /// <summary>
    /// Vault handles for every persistent quest DAG buffer.
    /// </summary>
    public struct QuestDagBufferHandles
    {
        public VaultBufferHandle<ulong> GlobalStateMasks;
        public VaultBufferHandle<ulong> OldStateMasks;
        public VaultBufferHandle<QuestNodeDTO> Nodes;
        public VaultBufferHandle<QuestNodeRuntimeDTO> NodeRuntime;
        public VaultBufferHandle<TriggerVolumeDTO> TriggerVolumes;
        public VaultBufferHandle<uint> RequiredItemHashes;
        public VaultBufferHandle<int> RequiredItemQuantities;
        public VaultBufferHandle<uint> PlayerItemHashes;
        public VaultBufferHandle<int> PlayerItemQuantities;
        public VaultBufferHandle<float> FactionStandings;
        public VaultBufferHandle<QuestDagTelemetryEntry> TelemetryRing;
        public VaultBufferHandle<int> TelemetryCursor;
        public VaultBufferHandle<int> Counters;
        public VaultBufferHandle<int> TriggerNodeIndices;
        public VaultBufferHandle<int> NoTriggerNodeIndices;
        public VaultBufferHandle<long> CsvMonitor;
    }

    /// <summary>
    /// Resolved NativeArray views. The struct is frame-local; persistent storage remains in GlobalDataVault.
    /// </summary>
    public struct QuestDagBuffers
    {
        public NativeArray<ulong> GlobalStateMasks;
        public NativeArray<ulong> OldStateMasks;
        public NativeArray<QuestNodeDTO> Nodes;
        public NativeArray<QuestNodeRuntimeDTO> NodeRuntime;
        public NativeArray<TriggerVolumeDTO> TriggerVolumes;
        public NativeArray<uint> RequiredItemHashes;
        public NativeArray<int> RequiredItemQuantities;
        public NativeArray<uint> PlayerItemHashes;
        public NativeArray<int> PlayerItemQuantities;
        public NativeArray<float> FactionStandings;
        public NativeArray<QuestDagTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public NativeArray<int> Counters;
        public NativeArray<int> TriggerNodeIndices;
        public NativeArray<int> NoTriggerNodeIndices;
        public NativeArray<long> CsvMonitor;
    }

    /// <summary>
    /// Constants and counter slots for the bitmask DAG runtime.
    /// </summary>
    public static class QuestDagRuntimeConstants
    {
        public const int DefaultNodeCapacity = 10000;
        public const int DefaultTriggerCapacity = 10000;
        public const int DefaultStateChunkCount = 120;
        public const int DefaultItemLinkCapacity = 10000;
        public const int DefaultPlayerItemCapacity = 512;
        public const int DefaultFactionCapacity = 256;
        public const int TelemetryCapacity = 300;
        public const int CounterCount = 16;
        public const int SpatialCellSizeMeters = 100;
        public const int SpatialHashCellsPerTriggerBudget = 27;
        public const int SpatialHashMaxInsertedCellRadius = 1;
        public const int NoTriggerNodesPerPassBudget = 256;
        public const int MaxFixedPointIterations = 5;
        public const int ToasterTickModulo = 15;
        public const uint SignalSourceHash = 0x51444147u; // QDAG
        public const uint OshinoBinarySourceHash = 0x4F534849u; // OSHI
        public const uint EmergencyMockSourceHash = 0x4D4F434Bu; // MOCK
        public const uint CsvOverrideSourceHash = 0x43535651u; // CSVQ
        public const string DeadlockDumpPath = "Docs/AgentLogs/Dump_QUEST_DAG.bin";
        public const string DeadlockH8DumpPath = "Docs/AgentLogs/Dump_QUEST_DAG.h8dump";
        public const string DefaultBinaryPath = "Data/Narrative/First_Hour_Quests.h8qdag.bin";
        public const string DefaultCsvOverridePath = "Data/Narrative/quest_logic_overrides.csv";

        public enum CounterSlot : int
        {
            NodeCount = 0,
            TriggerCount = 1,
            NoTriggerNodeCount = 2,
            PlayerItemCount = 3,
            StateChunkCount = 4,
            LastDeadlockFrame = 5,
            LastBitsFlippedLow = 6,
            LastBitsFlippedHigh = 7,
            LastEvaluatedNodes = 8,
            LastIterations = 9,
            LastLoadSourceHash = 10,
            LastCsvRowsApplied = 11,
            SpatialHashVersion = 12,
            SpatialHashRebuildCount = 13,
            PendingScheduleDropCount = 14,
            NoTriggerCursor = 15
        }
    }

    /// <summary>
    /// Source-level layout proof for DTOs used by the Burst resolver and black-box dump.
    /// </summary>
    public static class QuestDagLayoutAudit
    {
        public const int QuestNodeDTOSize = 32;
        public const int QuestNodeDTO_NodeHash = 0;
        public const int QuestNodeDTO_RequiredStateHash = 4;
        public const int QuestNodeDTO_PrerequisiteMask = 8;
        public const int QuestNodeDTO_CompletionMask = 16;
        public const int QuestNodeDTO_Pad0 = 24;
        public const int QuestNodeDTO_Pad1 = 28;

        public const int TriggerVolumeDTOSize = 40;
        public const int TriggerVolumeDTO_AUP = 0;
        public const int TriggerVolumeDTO_Radius = 24;
        public const int TriggerVolumeDTO_RequiredNodeHash = 28;
        public const int TriggerVolumeDTO_Pad0 = 32;
        public const int TriggerVolumeDTO_Pad1 = 36;

        public const int QuestNodeRuntimeDTOSize = 40;
        public const int QuestNodeRuntimeDTO_TargetTimestamp = 0;
        public const int QuestNodeRuntimeDTO_ReputationDelta = 8;
        public const int QuestNodeRuntimeDTO_ReputationThreshold = 12;
        public const int QuestNodeRuntimeDTO_StateChunk = 16;
        public const int QuestNodeRuntimeDTO_TriggerIndex = 20;
        public const int QuestNodeRuntimeDTO_RequiredItemStart = 24;
        public const int QuestNodeRuntimeDTO_RequiredItemCount = 28;
        public const int QuestNodeRuntimeDTO_FactionId = 32;
        public const int QuestNodeRuntimeDTO_Flags = 34;
        public const int QuestNodeRuntimeDTO_Pad0 = 36;

        public const int QuestDagTelemetryEntrySize = 48;
        public const int QuestDagTelemetryEntry_ResolverComputeTimeMs = 0;
        public const int QuestDagTelemetryEntry_BitsFlipped = 8;
        public const int QuestDagTelemetryEntry_Frame = 16;
        public const int QuestDagTelemetryEntry_ActiveNodesEvaluated = 20;
        public const int QuestDagTelemetryEntry_Iterations = 24;
        public const int QuestDagTelemetryEntry_Flags = 26;
        public const int QuestDagTelemetryEntry_DeadlockNodeHash = 28;
        public const int QuestDagTelemetryEntry_PlayerCellHash = 32;
        public const int QuestDagTelemetryEntry_StateHash = 36;
        public const int QuestDagTelemetryEntry_SpatialCandidateCount = 40;
        public const int QuestDagTelemetryEntry_Pad0 = 44;
    }

    [Flags]
    public enum QuestDagNodeFlags : ushort
    {
        None = 0,
        RequiresTrigger = 1 << 0,
        RequiresInventory = 1 << 1,
        RequiresFactionThreshold = 1 << 2,
        AppliesFactionDelta = 1 << 3,
        RequiresTimestamp = 1 << 4
    }

    [Flags]
    public enum QuestDagTelemetryFlags : ushort
    {
        None = 0,
        FixedPointLimitHit = 1 << 0,
        InvalidAup = 1 << 1,
        ToasterDilated = 1 << 2,
        NoTriggerBudgeted = 1 << 3
    }

    [Flags]
    public enum QuestDagLoadFlags : uint
    {
        None = 0,
        BinaryLoaded = 1u << 0,
        EmergencyMockGenerated = 1u << 1,
        BinaryMissing = 1u << 2,
        BinaryUnreadable = 1u << 3
    }
}
