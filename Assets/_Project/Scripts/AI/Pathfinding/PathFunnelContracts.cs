using System.Runtime.InteropServices;
using Hecton8.Core.Contracts;
using Unity.Mathematics;

namespace Hecton8.AI.Pathfinding
{
    /// <summary>
    /// Math LOD used by the funnel smoother. Higher tiers inspect more portals per smoothing pass.
    /// </summary>
    public enum PathFunnelMathLod : byte
    {
        Stressed = 0,
        Low = 1,
        Middle = 2,
        High = 3,
        Ultra = 4
    }

    /// <summary>
    /// Shared constants for the Burst funnel smoother and its WFC invalidation owner.
    /// </summary>
    public static class PathFunnelConstants
    {
        public const int TelemetryFrames = 300;
        public const int WfcOutpostCellCount = WfcOutpostPersistenceConstants.CellCount;
        public const int WfcCellMaskWordCount = (WfcOutpostCellCount + 63) / 64;
        public const float Epsilon = 0.00001f;
        public const byte PortalFlagWfcDoor = 1 << 0;
        public const byte PortalFlagNoRadiusShrink = 1 << 1;
        public const byte WfcDoorOpenFlag = (byte)WfcOutpostCellStateFlags.DoorOpen;
        public const byte WfcMutableFlagMask = WfcOutpostPersistenceConstants.MutableFlagMask;
        public const uint SourceHash = 0x50464E4Cu; // PFNL
    }

    /// <summary>
    /// One corridor portal edge in sector-local meters. Left and right are ordered around the path corridor.
    /// ClearanceMeters is pre-eroded SDF clearance from the navgrid owner; zero means unknown.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 36, Pack = 1)]
    public struct NavPortal
    {
        [FieldOffset(0)] public float3 Left;
        [FieldOffset(12)] public float3 Right;
        [FieldOffset(24)] public float ClearanceMeters;
        [FieldOffset(28)] public ushort LeftCellIndex;
        [FieldOffset(30)] public ushort RightCellIndex;
        [FieldOffset(32)] public byte Flags;
        [FieldOffset(33)] public byte Reserved0;
        [FieldOffset(34)] public ushort Reserved1;
    }

    /// <summary>
    /// Status codes returned by <see cref="FunnelSmoothingJob"/>.
    /// </summary>
    public static class PathFunnelStatus
    {
        public const byte None = 0;
        public const byte Complete = 1;
        public const byte PartialLookAhead = 2;
        public const byte BlockedByWfcDoor = 3;
        public const byte FallbackRaw = 4;
        public const byte InvalidInput = 5;
        public const byte OutputCapacityExceeded = 6;
    }

    /// <summary>
    /// Bit flags emitted with a funnel result.
    /// </summary>
    public static class PathFunnelResultFlags
    {
        public const uint NonFiniteInput = 1u << 0;
        public const uint CollinearPortal = 1u << 1;
        public const uint NarrowPortalClamped = 1u << 2;
        public const uint WfcDoorBlocked = 1u << 3;
        public const uint IterationGuardTripped = 1u << 4;
        public const uint OutputOverflow = 1u << 5;
        public const uint AupFallback = 1u << 6;
        public const uint PartialLookAhead = 1u << 7;
        public const uint SdfClearanceClamped = 1u << 8;
        public const uint InvalidMathLod = 1u << 9;
        public const uint InvalidWfcCell = 1u << 10;
        public const uint PortalInputClamped = 1u << 11;
        public const uint AupOutputClamped = 1u << 12;
        public const uint AgentRadiusClamped = 1u << 13;
    }

    /// <summary>
    /// Single-slot result payload written by the Burst funnel job.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32, Pack = 1)]
    public struct PathFunnelResult
    {
        [FieldOffset(0)] public int WaypointCount;
        [FieldOffset(4)] public int ProcessedPortalCount;
        [FieldOffset(8)] public int Iterations;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public byte Status;
        [FieldOffset(17)] public byte MathLod;
        [FieldOffset(18)] public ushort BlockedCellIndex;
        [FieldOffset(20)] public uint CorridorHash;
        [FieldOffset(24)] public uint Frame;
        [FieldOffset(28)] public uint Reserved0;
    }

    /// <summary>
    /// Fixed active-path record used by the WFC door invalidation owner.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32, Pack = 1)]
    public struct PathFunnelActivePath
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint PathId;
        [FieldOffset(12)] public uint CorridorHash;
        [FieldOffset(16)] public ushort CellCount;
        [FieldOffset(18)] public ushort Flags;
        [FieldOffset(20)] public uint LastTouchedFrame;
        [FieldOffset(24)] public uint InvalidatedFrame;
        [FieldOffset(28)] public uint Reserved0;
    }

    /// <summary>
    /// Active-path record flags.
    /// </summary>
    public static class PathFunnelActivePathFlags
    {
        public const ushort InUse = 1 << 0;
        public const ushort Invalidated = 1 << 1;
    }

    /// <summary>
    /// Bounded invalidation payload for consumers polling the runtime owner.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32, Pack = 1)]
    public struct PathFunnelInvalidation
    {
        [FieldOffset(0)] public ulong SectorHash;
        [FieldOffset(8)] public uint PathId;
        [FieldOffset(12)] public uint CorridorHash;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public ushort CellIndex;
        [FieldOffset(22)] public ushort Flags;
        [FieldOffset(24)] public byte PreviousCellFlags;
        [FieldOffset(25)] public byte CurrentCellFlags;
        [FieldOffset(26)] public ushort Reserved0;
        [FieldOffset(28)] public uint Reserved1;
    }

    /// <summary>
    /// Fixed black-box entry. The runtime writes one entry per late-frame flush.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 48, Pack = 1)]
    public struct PathFunnelTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint PathInvalidationCount;
        [FieldOffset(8)] public ulong LastSectorHash;
        [FieldOffset(16)] public uint LastPathId;
        [FieldOffset(20)] public uint LastCorridorHash;
        [FieldOffset(24)] public ushort LastCellIndex;
        [FieldOffset(26)] public ushort ActivePathCount;
        [FieldOffset(28)] public ushort InvalidatedPathCount;
        [FieldOffset(30)] public ushort Flags;
        [FieldOffset(32)] public float Stress01;
        [FieldOffset(36)] public uint Reserved0;
        [FieldOffset(40)] public ulong Reserved1;
    }

    /// <summary>
    /// Runtime telemetry status bits for the path funnel blackbox.
    /// </summary>
    public static class PathFunnelTelemetryFlags
    {
        public const ushort BlackBoxDumpFailed = 1 << 0;
        public const ushort WfcVaultSignalMismatch = 1 << 1;
        public const ushort TransientFrameMask = WfcVaultSignalMismatch;
    }

    /// <summary>
    /// Vault-resident mutable runtime counters for WFC path invalidation.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 64, Pack = 1)]
    public struct PathFunnelRuntimeState
    {
        [FieldOffset(0)] public int ActivePathCount;
        [FieldOffset(4)] public int InvalidationReadCursor;
        [FieldOffset(8)] public int InvalidationWriteCursor;
        [FieldOffset(12)] public int TelemetryCursor;
        [FieldOffset(16)] public uint PathInvalidationCount;
        [FieldOffset(20)] public uint LastPathId;
        [FieldOffset(24)] public uint LastCorridorHash;
        [FieldOffset(28)] public ushort LastCellIndex;
        [FieldOffset(30)] public ushort InvalidatedPathCount;
        [FieldOffset(32)] public ulong LastSectorHash;
        [FieldOffset(40)] public ushort TelemetryFlags;
        [FieldOffset(42)] public byte DumpRequested;
        [FieldOffset(43)] public byte BuffersReady;
        [FieldOffset(44)] public uint VaultGeneration;
        [FieldOffset(48)] public ulong Reserved0;
        [FieldOffset(56)] public ulong Reserved1;
    }
}
