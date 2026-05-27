using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Mathematics;

namespace Hecton8.World
{
    public static class VegetationMemorySovereigntyConstants
    {
        public const int TelemetryFrameCount = 300;
        public const int TelemetryEntryStrideBytes = 64;
        public const int HashPairStrideBytes = 8;
        public const int CounterStrideBytes = 16;
        public const ulong DumpMagic = 0x313331365F564547UL;
        public const int DumpVersion = 1;
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_1316_Vegetation.bin";
        public const SystemID OwnerSystemId = SystemID.WorldSargassum;
        public const BufferID TelemetryRingBufferId = BufferID.VegetationMemoryPoolTelemetryRing;
        public const BufferID TelemetryCursorBufferId = BufferID.VegetationMemoryPoolTelemetryCursor;
        public const uint FlagColdBoot = 1u << 0;
        public const uint FlagDefrag = 1u << 1;
        public const uint FlagLockContention = 1u << 2;
        public const uint FlagStaleHandle = 1u << 3;
        public const uint FlagNan = 1u << 4;
        public const uint FlagCapacity = 1u << 5;
        public const uint FlagCompactionFence = 1u << 6;
    }

    public enum VegetationMemoryTelemetryCode : ushort
    {
        None = 0,
        ColdBootRegistered = 1,
        DefragScheduled = 2,
        DefragCompleted = 3,
        VaultResolveFailed = 4,
        WriteLockContention = 5,
        NaNDetected = 6,
        ShutdownReleased = 7,
        StagingCapacityExceeded = 8,
        CompactionFenceActive = 9
    }

    public enum VegetationMemoryTelemetryPhase : ushort
    {
        Unknown = 0,
        ColdBoot = 1,
        SlowTick = 2,
        VisualSync = 3,
        Defrag = 4,
        Shutdown = 5
    }

    [StructLayout(LayoutKind.Explicit, Size = VegetationMemorySovereigntyConstants.TelemetryEntryStrideBytes)]
    public struct VegetationMemoryTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint BufferId;
        [FieldOffset(12)] public uint Generation;
        [FieldOffset(16)] public uint Frame;
        [FieldOffset(20)] public int ExpectedLength;
        [FieldOffset(24)] public int ActualLength;
        [FieldOffset(28)] public int CulledInstances;
        [FieldOffset(32)] public float JobMicroseconds;
        [FieldOffset(36)] public float QualityWeight;
        [FieldOffset(40)] public ushort FailureCode;
        [FieldOffset(42)] public ushort Phase;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public float3 Position;
        [FieldOffset(60)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = VegetationMemorySovereigntyConstants.HashPairStrideBytes)]
    public struct VegetationMemoryHashPair
    {
        [FieldOffset(0)] public int Key;
        [FieldOffset(4)] public int Value;
    }

    [StructLayout(LayoutKind.Explicit, Size = VegetationMemorySovereigntyConstants.CounterStrideBytes)]
    public struct VegetationMemoryCounter
    {
        [FieldOffset(0)] public int Count;
        [FieldOffset(4)] public int Capacity;
        [FieldOffset(8)] public uint Generation;
        [FieldOffset(12)] public uint Flags;
    }
}
