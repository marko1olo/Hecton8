using System;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Quest
{
    internal static class QuestDagRuntimeLayout
    {
        public const int NodeStrideBytes = 32;
        public const int TriggerVolumeStrideBytes = 64;
        public const int NodeRuntimeStrideBytes = 64;
        public const int QuestStateStrideBytes = 16;
        public const int QuestDependencyLinkStrideBytes = 16;
        public const int TelemetryEntryStrideBytes = 64;
        public const int StateChangedSignalStrideBytes = 32;
        public const int MockStoryEventSignalStrideBytes = 32;
        public const int MockPlayerPositionSignalStrideBytes = 64;
        public const int MockItemAcquiredSignalStrideBytes = 32;
        public const int LoadStatsStrideBytes = 64;
    }

    /// <summary>
    /// Fixed binary quest node consumed by the Burst DAG resolver.
    /// </summary>
    /// <remarks>
    /// Layout: 0 uint node, 4 uint state/lore hash, 8 ulong prerequisite,
    /// 16 ulong completion, 24/28 explicit uint padding. Total 32 bytes.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.NodeStrideBytes)]
    public struct QuestNodeDTO
    {
        [FieldOffset(0)]
        public uint NodeHash;

        [FieldOffset(4)]
        public uint RequiredStateHash;

        [FieldOffset(8)]
        public ulong PrerequisiteMask;

        [FieldOffset(16)]
        public ulong CompletionMask;

        [FieldOffset(24)]
        public uint _pad0;

        [FieldOffset(28)]
        public uint _pad1;
    }

    /// <summary>
    /// AUP-space narrative trigger volume. Absolute doubles stay intact until the local delta is formed.
    /// </summary>
    /// <remarks>
    /// Layout: 0 double3 AUP, 24 float radius, 28 uint node hash, 32/36 padding, 40/48/56 tail padding. Total 64 bytes.
    /// </remarks>
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.TriggerVolumeStrideBytes)]
    public struct TriggerVolumeDTO
    {
        [FieldOffset(0)]
        public double3 AUP;

        [FieldOffset(24)]
        public float Radius;

        [FieldOffset(28)]
        public uint RequiredNodeHash;

        [FieldOffset(32)]
        public uint _pad0;

        [FieldOffset(36)]
        public uint _pad1;

        [FieldOffset(40)]
        private ulong _pad2;

        [FieldOffset(48)]
        private ulong _pad3;

        [FieldOffset(56)]
        private ulong _pad4;
    }

    /// <summary>
    /// Parallel metadata for <see cref="QuestNodeDTO"/>. Kept outside the 32-byte OSHINO node payload.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.NodeRuntimeStrideBytes)]
    public struct QuestNodeRuntimeDTO
    {
        [FieldOffset(0)]
        public ulong TargetTimestamp;

        [FieldOffset(8)]
        public float ReputationDelta;

        [FieldOffset(12)]
        public float ReputationThreshold;

        [FieldOffset(16)]
        public int StateChunk;

        [FieldOffset(20)]
        public int TriggerIndex;

        [FieldOffset(24)]
        public int RequiredItemStart;

        [FieldOffset(28)]
        public int RequiredItemCount;

        [FieldOffset(32)]
        public ushort FactionId;

        [FieldOffset(34)]
        public ushort Flags;

        [FieldOffset(36)]
        public uint _pad0;

        [FieldOffset(40)]
        private ulong _pad1;

        [FieldOffset(48)]
        private ulong _pad2;

        [FieldOffset(56)]
        private ulong _pad3;
    }

    /// <summary>
    /// HUD-facing unmanaged quest state used by the Zeigarnik overlap pass.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.QuestStateStrideBytes)]
    public struct QuestStateDTO
    {
        [FieldOffset(0)]
        public uint ActiveQuestHashID;

        [FieldOffset(4)]
        public float CompletionProgress;

        [FieldOffset(8)]
        public uint InjectedSubQuestHashID;

        [FieldOffset(12)]
        public uint StateFlags;
    }

    /// <summary>
    /// Sorted parent-to-child edge table for branch-light quest overlap resolution.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.QuestDependencyLinkStrideBytes)]
    public struct QuestDependencyLinkDTO
    {
        [FieldOffset(0)]
        public uint ParentQuestHashID;

        [FieldOffset(4)]
        public uint ChildQuestHashID;

        [FieldOffset(8)]
        public uint Flags;

        [FieldOffset(12)]
        public uint _pad0;
    }

    /// <summary>
    /// Fixed black-box sample for the last 300 resolver frames.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.TelemetryEntryStrideBytes)]
    public struct QuestDagTelemetryEntry
    {
        [FieldOffset(0)]
        public double ResolverComputeTimeMs;

        [FieldOffset(8)]
        public ulong BitsFlipped;

        [FieldOffset(16)]
        public uint Frame;

        [FieldOffset(20)]
        public int ActiveNodesEvaluated;

        [FieldOffset(24)]
        public ushort Iterations;

        [FieldOffset(26)]
        public ushort Flags;

        [FieldOffset(28)]
        public uint DeadlockNodeHash;

        [FieldOffset(32)]
        public uint PlayerCellHash;

        [FieldOffset(36)]
        public uint StateHash;

        [FieldOffset(40)]
        public int SpatialCandidateCount;

        [FieldOffset(44)]
        public uint _pad0;

        [FieldOffset(48)]
        private ulong _pad1;

        [FieldOffset(56)]
        private ulong _pad2;
    }

    /// <summary>
    /// Typed unmanaged state-change signal emitted after old/new mask XOR.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.StateChangedSignalStrideBytes)]
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
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.MockStoryEventSignalStrideBytes)]
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
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.MockPlayerPositionSignalStrideBytes)]
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
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.MockItemAcquiredSignalStrideBytes)]
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
    [StructLayout(LayoutKind.Explicit, Size = QuestDagRuntimeLayout.LoadStatsStrideBytes)]
    public struct QuestDagLoadStats
    {
        [FieldOffset(0)]
        public ulong AllDoneMask;

        [FieldOffset(8)]
        public uint GraphHash;

        [FieldOffset(12)]
        public uint NodeCount;

        [FieldOffset(16)]
        public uint TriggerCount;

        [FieldOffset(20)]
        public uint EdgeCount;

        [FieldOffset(24)]
        public uint TotalBytes;

        [FieldOffset(28)]
        public uint Flags;

        [FieldOffset(32)]
        public uint SourceHash;

        [FieldOffset(36)]
        public uint _pad0;

        [FieldOffset(40)]
        public uint _pad1;

        [FieldOffset(44)]
        private uint _pad2;

        [FieldOffset(48)]
        private ulong _pad3;

        [FieldOffset(56)]
        private ulong _pad4;
    }

    /// <summary>
    /// Vault handles for every persistent quest DAG buffer.
    /// </summary>
    public struct QuestDagBufferHandles
    {
        public VaultGenerationHandle<ulong> GlobalStateMasks;
        public VaultGenerationHandle<ulong> OldStateMasks;
        public VaultGenerationHandle<QuestNodeDTO> Nodes;
        public VaultGenerationHandle<QuestNodeRuntimeDTO> NodeRuntime;
        public VaultGenerationHandle<TriggerVolumeDTO> TriggerVolumes;
        public VaultGenerationHandle<uint> RequiredItemHashes;
        public VaultGenerationHandle<int> RequiredItemQuantities;
        public VaultGenerationHandle<uint> PlayerItemHashes;
        public VaultGenerationHandle<int> PlayerItemQuantities;
        public VaultGenerationHandle<float> FactionStandings;
        public VaultGenerationHandle<QuestDagTelemetryEntry> TelemetryRing;
        public VaultGenerationHandle<int> TelemetryCursor;
        public VaultGenerationHandle<int> Counters;
        public VaultGenerationHandle<int> TriggerNodeIndices;
        public VaultGenerationHandle<int> NoTriggerNodeIndices;
        public VaultGenerationHandle<QuestStateDTO> QuestStates;
        public VaultGenerationHandle<QuestDependencyLinkDTO> DependencyLinks;
        public VaultGenerationHandle<long> CsvMonitor;
        public int NodeCapacity;
        public int TriggerCapacity;
        public int StateChunkCount;
        public int ItemLinkCapacity;
        public int PlayerItemCapacity;
        public int FactionCapacity;
        public int QuestStateCapacity;
        public int DependencyLinkCapacity;
    }

    /// <summary>
    /// Resolved NativeArray views. The struct is frame-local; persistent storage remains in GlobalDataVault.
    /// </summary>
    public ref struct QuestDagBuffers
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
        public NativeArray<QuestStateDTO> QuestStates;
        public NativeArray<QuestDependencyLinkDTO> DependencyLinks;
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
        public const int DefaultQuestStateCapacity = 64;
        public const int DefaultDependencyLinkCapacity = 10000;
        public const int TelemetryCapacity = 300;
        public const int CounterCount = 20;
        public const int SpatialCellSizeMeters = 100;
        public const int SpatialHashCellsPerTriggerBudget = 27;
        public const int SpatialHashMaxInsertedCellRadius = 1;
        public const int NoTriggerNodesPerPassBudget = 256;
        public const int MaxFixedPointIterations = 5;
        public const int ToasterTickModulo = 15;
        public const float ZeigarnikPreCompletionProgressThreshold = 0.95f;
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
            NoTriggerCursor = 15,
            QuestStateCount = 16,
            DependencyLinkCount = 17,
            ZeigarnikInjectedCount = 18,
            ZeigarnikFailClosedCount = 19
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

        public const int TriggerVolumeDTOSize = 64;
        public const int TriggerVolumeDTO_AUP = 0;
        public const int TriggerVolumeDTO_Radius = 24;
        public const int TriggerVolumeDTO_RequiredNodeHash = 28;
        public const int TriggerVolumeDTO_Pad0 = 32;
        public const int TriggerVolumeDTO_Pad1 = 36;
        public const int TriggerVolumeDTO_Pad2 = 40;
        public const int TriggerVolumeDTO_Pad3 = 48;
        public const int TriggerVolumeDTO_Pad4 = 56;

        public const int QuestNodeRuntimeDTOSize = 64;
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
        public const int QuestNodeRuntimeDTO_Pad1 = 40;
        public const int QuestNodeRuntimeDTO_Pad2 = 48;
        public const int QuestNodeRuntimeDTO_Pad3 = 56;

        public const int QuestStateDTOSize = 16;
        public const int QuestStateDTO_ActiveQuestHashID = 0;
        public const int QuestStateDTO_CompletionProgress = 4;
        public const int QuestStateDTO_InjectedSubQuestHashID = 8;
        public const int QuestStateDTO_StateFlags = 12;

        public const int QuestDependencyLinkDTOSize = 16;
        public const int QuestDependencyLinkDTO_ParentQuestHashID = 0;
        public const int QuestDependencyLinkDTO_ChildQuestHashID = 4;
        public const int QuestDependencyLinkDTO_Flags = 8;
        public const int QuestDependencyLinkDTO_Pad0 = 12;

        public const int QuestDagTelemetryEntrySize = 64;
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
        public const int QuestDagTelemetryEntry_Pad1 = 48;
        public const int QuestDagTelemetryEntry_Pad2 = 56;
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
        NoTriggerBudgeted = 1 << 3,
        ZeigarnikInjected = 1 << 4,
        ZeigarnikFailClosed = 1 << 5
    }

    [Flags]
    public enum QuestStateFlags : uint
    {
        None = 0,
        ZeigarnikProgressArmed = 1u << 0,
        ZeigarnikInjected = 1u << 1,
        ZeigarnikDependencyMissing = 1u << 2
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
