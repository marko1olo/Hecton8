using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.AI.Pathfinding
{
    /// <summary>
    /// Constants for the SHINOBU_304 voxel SDF A* route.
    /// </summary>
    public static class VoxelAStarConstants
    {
        public const int TelemetryFrames = 300;
        public const int DefaultRequestCapacity = 64;
        public const int DefaultResultCapacity = 64;
        public const int DefaultWaypointCapacity = 4096;
        public const int DefaultRawPathCapacity = 2048;
        public const int DefaultGridX = 32;
        public const int DefaultGridY = 32;
        public const int DefaultGridZ = 32;
        public const float DefaultVoxelSizeMeters = 2f;
        public const float MinimumVoxelSizeMeters = 0.05f;
        public const float MinimumRadiusMeters = 0.05f;
        public const float MaximumRadiusMeters = 32f;
        public const float LineSampleEpsilon = 0.0001f;
        public const uint SourceHash = 0x53333034u; // S304
    }

    /// <summary>
    /// Stable request record consumed by the voxel A* ring.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PathRequestDTO
    {
        [FieldOffset(0)] public double3 StartAUP;
        [FieldOffset(24)] public double3 EndAUP;
        [FieldOffset(48)] public float RequiredRadius;
        [FieldOffset(52)] public uint RequesterEntityHash;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] private uint _pad0;
    }

    /// <summary>
    /// SDF grid metadata. Coordinates are absolute at the origin and local float only after subtraction.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelSdfGridHeader
    {
        [FieldOffset(0)] public double3 OriginAUP;
        [FieldOffset(24)] public int3 Dimensions;
        [FieldOffset(36)] public float VoxelSizeMeters;
        [FieldOffset(40)] public uint GridVersion;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float SolidMarginMeters;
        [FieldOffset(52)] public float MaxDistanceMeters;
        [FieldOffset(56)] private ulong _pad0;
    }

    /// <summary>
    /// Fixed ring state for one producer/one consumer request handoff.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelPathRingState
    {
        [FieldOffset(0)] public int ReadCursor;
        [FieldOffset(4)] public int WriteCursor;
        [FieldOffset(8)] public int Count;
        [FieldOffset(12)] public int Capacity;
        [FieldOffset(16)] public uint DroppedRequests;
        [FieldOffset(20)] public uint AcceptedRequests;
        [FieldOffset(24)] public uint ConsumedRequests;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public ulong Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
        [FieldOffset(48)] public ulong Reserved2;
        [FieldOffset(56)] public ulong Reserved3;
    }

    /// <summary>
    /// Result status values for the voxel A* route.
    /// </summary>
    public static class VoxelPathStatus
    {
        public const byte None = 0;
        public const byte Queued = 1;
        public const byte Searching = 2;
        public const byte RawPathReady = 3;
        public const byte Smoothing = 4;
        public const byte Complete = 5;
        public const byte Partial = 6;
        public const byte Failed = 7;
        public const byte InvalidInput = 8;
        public const byte OutputOverflow = 9;
    }

    /// <summary>
    /// Solver and result flags.
    /// </summary>
    public static class VoxelPathFlags
    {
        public const uint NonFiniteInput = 1u << 0;
        public const uint StartOutOfBounds = 1u << 1;
        public const uint GoalOutOfBounds = 1u << 2;
        public const uint StartBlocked = 1u << 3;
        public const uint GoalBlocked = 1u << 4;
        public const uint OpenSetExhausted = 1u << 5;
        public const uint NodeBudgetYield = 1u << 6;
        public const uint RawPathOverflow = 1u << 7;
        public const uint WaypointOverflow = 1u << 8;
        public const uint SdfMissing = 1u << 9;
        public const uint NaNDetected = 1u << 10;
        public const uint TimeSliceOverBudget = 1u << 11;
        public const uint UsedWeightedHeuristic = 1u << 12;
        public const uint PartialNearestFallback = 1u << 13;
        public const uint MockSdfGenerated = 1u << 14;
        public const uint CsvProfileOverflow = 1u << 15;
    }

    /// <summary>
    /// Per-node state stored in a flat native array. SearchId guards uninitialized memory.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VoxelPathNodeRecord
    {
        [FieldOffset(0)] public float GCost;
        [FieldOffset(4)] public float FCost;
        [FieldOffset(8)] public int ParentIndex;
        [FieldOffset(12)] public uint SearchId;
        [FieldOffset(16)] public uint BestGoalDistanceSqBits;
        [FieldOffset(20)] public int HeapPosition;
        [FieldOffset(24)] public byte Flags;
        [FieldOffset(25)] private byte _pad0;
        [FieldOffset(26)] private ushort _pad1;
        [FieldOffset(28)] private uint _pad2;
    }

    /// <summary>
    /// Binary heap entry for the open set.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct VoxelPathHeapNode
    {
        [FieldOffset(0)] public int NodeIndex;
        [FieldOffset(4)] public float FCost;
        [FieldOffset(8)] public float GCost;
        [FieldOffset(12)] public uint TieBreak;
        [FieldOffset(16)] private ulong _pad0;
    }

    /// <summary>
    /// Persistent solver state for one time-sliced search slot.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 192)]
    public struct VoxelPathSolverState
    {
        [FieldOffset(0)] public PathRequestDTO Request;
        [FieldOffset(64)] public double3 GridOriginAUP;
        [FieldOffset(88)] public int StartIndex;
        [FieldOffset(92)] public int GoalIndex;
        [FieldOffset(96)] public int BestNodeIndex;
        [FieldOffset(100)] public int OpenHeapCount;
        [FieldOffset(104)] public int RawPathCount;
        [FieldOffset(108)] public int WaypointCount;
        [FieldOffset(112)] public int NodesExpandedTotal;
        [FieldOffset(116)] public int NodesExpandedLastFrame;
        [FieldOffset(120)] public uint SearchId;
        [FieldOffset(124)] public uint FrameStarted;
        [FieldOffset(128)] public uint FrameUpdated;
        [FieldOffset(132)] public uint Flags;
        [FieldOffset(136)] public byte Status;
        [FieldOffset(137)] public byte Active;
        [FieldOffset(138)] public ushort ResultIndex;
        [FieldOffset(140)] public float BestGoalDistanceSq;
        [FieldOffset(144)] public float HeuristicWeight;
        [FieldOffset(148)] public float RequiredRadius;
        [FieldOffset(152)] public int3 Dimensions;
        [FieldOffset(164)] public float VoxelSizeMeters;
        [FieldOffset(168)] public uint GridVersion;
        [FieldOffset(172)] public uint Reserved0;
        [FieldOffset(176)] public ulong Reserved1;
        [FieldOffset(184)] public ulong Reserved2;
    }

    /// <summary>
    /// Stable result payload for steering or future rollback snapshots.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct PathResultDTO
    {
        [FieldOffset(0)] public uint RequesterEntityHash;
        [FieldOffset(4)] public uint RequestFlags;
        [FieldOffset(8)] public uint ResultFlags;
        [FieldOffset(12)] public uint FrameCompleted;
        [FieldOffset(16)] public int RawPathCount;
        [FieldOffset(20)] public int WaypointStart;
        [FieldOffset(24)] public int WaypointCount;
        [FieldOffset(28)] public int NodesExpandedTotal;
        [FieldOffset(32)] public int NodesExpandedLastFrame;
        [FieldOffset(36)] public int BestNodeIndex;
        [FieldOffset(40)] public byte Status;
        [FieldOffset(41)] public byte SolverSlot;
        [FieldOffset(42)] public ushort ResultIndex;
        [FieldOffset(44)] public float RequiredRadius;
        [FieldOffset(48)] public float HeuristicWeight;
        [FieldOffset(52)] public float QualityWeight;
        [FieldOffset(56)] public float EstimatedCost;
        [FieldOffset(60)] public uint SearchId;
        [FieldOffset(64)] public double3 StartAUP;
        [FieldOffset(88)] public double3 EndAUP;
        [FieldOffset(112)] public ulong Reserved0;
        [FieldOffset(120)] public ulong Reserved1;
    }

    /// <summary>
    /// Absolute route waypoint. The first 24 bytes are directly memcpy-safe double3 AUP.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VoxelPathWaypointDTO
    {
        [FieldOffset(0)] public double3 PositionAUP;
        [FieldOffset(24)] public uint NodeIndex;
        [FieldOffset(28)] public uint Flags;
    }

    /// <summary>
    /// Runtime tuning values mutated by editor facades and cold bootstrap.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct VoxelAStarTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float MinimumHeuristicWeight;
        [FieldOffset(8)] public float MaximumHeuristicWeight;
        [FieldOffset(12)] public float SmoothingSampleSpacingMeters;
        [FieldOffset(16)] public int MinNodesExpandedPerFrame;
        [FieldOffset(20)] public int MaxNodesExpandedPerFrame;
        [FieldOffset(24)] public int MaxStringPullLookAhead;
        [FieldOffset(28)] public int MaxLineSamplesPerSegment;
        [FieldOffset(32)] public int MaxRawPathNodes;
        [FieldOffset(36)] public int MaxWaypoints;
        [FieldOffset(40)] public float TimeSliceBudgetMs;
        [FieldOffset(44)] public float VerticalPenalty;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private ulong _pad1;

        /// <summary>
        /// Builds the deterministic default tuning payload.
        /// </summary>
        public static VoxelAStarTuningDTO Default()
        {
            VoxelAStarTuningDTO value = default;
            value.GlobalQualityWeight = 1f;
            value.MinimumHeuristicWeight = 1.05f;
            value.MaximumHeuristicWeight = 2.25f;
            value.SmoothingSampleSpacingMeters = 1.5f;
            value.MinNodesExpandedPerFrame = 96;
            value.MaxNodesExpandedPerFrame = 768;
            value.MaxStringPullLookAhead = 32;
            value.MaxLineSamplesPerSegment = 96;
            value.MaxRawPathNodes = VoxelAStarConstants.DefaultRawPathCapacity;
            value.MaxWaypoints = VoxelAStarConstants.DefaultWaypointCapacity;
            value.TimeSliceBudgetMs = 1.5f;
            value.VerticalPenalty = 1.85f;
            return value;
        }
    }

    /// <summary>
    /// Species profile parsed from `fauna_pathfinding_profiles.csv`.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct VoxelPathingProfileDTO
    {
        [FieldOffset(0)] public uint SpeciesHash;
        [FieldOffset(4)] public float RequiredRadiusMeters;
        [FieldOffset(8)] public int MaxNodesExpandedPerFrame;
        [FieldOffset(12)] public float HeuristicWeightScale;
        [FieldOffset(16)] public int MaxStringPullLookAhead;
        [FieldOffset(20)] public uint Flags;
        [FieldOffset(24)] private ulong _pad0;
    }

    /// <summary>
    /// 300-frame black-box entry for voxel A*.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct PathfindingTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint PendingRequests;
        [FieldOffset(8)] public uint AcceptedRequests;
        [FieldOffset(12)] public uint DroppedRequests;
        [FieldOffset(16)] public uint SuccessfulPaths;
        [FieldOffset(20)] public uint FailedPaths;
        [FieldOffset(24)] public uint NodesExpanded;
        [FieldOffset(28)] public uint AverageNodesExpanded;
        [FieldOffset(32)] public uint BurstMicros;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public uint SearchId;
        [FieldOffset(44)] public uint RequesterEntityHash;
        [FieldOffset(48)] public float QualityWeight;
        [FieldOffset(52)] public float HeuristicWeight;
        [FieldOffset(56)] public ushort RawPathCount;
        [FieldOffset(58)] public ushort WaypointCount;
        [FieldOffset(60)] public uint Reserved0;
    }
}
