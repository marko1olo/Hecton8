using System.Runtime.InteropServices;
using Unity.Mathematics;

namespace Hecton8.AI.Cognition
{
    public static class AnxietyDecayConstants
    {
        public const int TelemetryFrames = 300;
        public const int MaxProfiles = 128;
        public const int ShelterSdfVoxels = 32768;
        public const int CsvScratchBytes = 8192;
        public const float DefaultFearDecayRate = 0.72f;
        public const float DefaultAggressionDecayRate = 0.28f;
        public const float DefaultCalmingThreshold = 0.025f;
        public const float DefaultShelterCoolingMultiplier = 1.85f;
        public const float DefaultLinearDecayScale = 0.68f;
        public const float FaultMicroseconds = 500f;
        public const uint AgentHash = 0x53333132u;
        public const uint DumpMagic = 0x41333132u;
    }

    public static class AnxietyDecayFlags
    {
        public const uint Active = 1u << 0;
        public const uint Agitated = 1u << 8;
        public const uint ShelterSampled = 1u << 9;
        public const uint UsedLinearApproximation = 1u << 10;
        public const uint NonFiniteInput = 1u << 11;
        public const uint Fault = 1u << 12;
        public const uint EmergencyMock = 1u << 13;
    }

    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct AnxietyProfileDTO
    {
        [FieldOffset(0)] public float FearDecayRate;
        [FieldOffset(4)] public float AggressionDecayRate;
        [FieldOffset(8)] public float CalmingThreshold;
        [FieldOffset(12)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnxietyRuntimeTuningDTO
    {
        [FieldOffset(0)] public float BaseFearDecayRate;
        [FieldOffset(4)] public float BaseAggressionDecayRate;
        [FieldOffset(8)] public float CalmingThreshold;
        [FieldOffset(12)] public float ShelterCoolingMultiplier;
        [FieldOffset(16)] public float LinearDecayScale;
        [FieldOffset(20)] public float SimulationDeltaSeconds;
        [FieldOffset(24)] public float GlobalQualityWeight;
        [FieldOffset(28)] public float ThermalPressure01;
        [FieldOffset(32)] public float ExactExpWeight01;
        [FieldOffset(36)] public float FaultMicroseconds;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint LastCsvHash;
        [FieldOffset(48)] public uint CsvReloadVersion;
        [FieldOffset(52)] public uint ActiveProfileCount;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnxietyDecayScratchDTO
    {
        [FieldOffset(0)] public float Fear01;
        [FieldOffset(4)] public float Aggression01;
        [FieldOffset(8)] public float ShelterMultiplier;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public uint StateHash;
        [FieldOffset(20)] public uint EntityHash;
        [FieldOffset(24)] private ulong _pad0;
        [FieldOffset(32)] private ulong _pad1;
        [FieldOffset(40)] private ulong _pad2;
        [FieldOffset(48)] private ulong _pad3;
        [FieldOffset(56)] private ulong _pad4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnxietyTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveDecayCount;
        [FieldOffset(8)] public uint ShelterMultiplierCount;
        [FieldOffset(12)] public uint NonFiniteCount;
        [FieldOffset(16)] public uint FaultFlags;
        [FieldOffset(20)] public float AverageFear01;
        [FieldOffset(24)] public float AverageAggression01;
        [FieldOffset(28)] public float AverageShelterMultiplier;
        [FieldOffset(32)] public float BurstMicroseconds;
        [FieldOffset(36)] public float GlobalQualityWeight;
        [FieldOffset(40)] public float ExactExpWeight01;
        [FieldOffset(44)] public float ThermalPressure01;
        [FieldOffset(48)] public uint StateHashFold;
        [FieldOffset(52)] public uint ProfileHashFold;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AnxietyDumpHeaderDTO
    {
        [FieldOffset(0)] public uint Magic;
        [FieldOffset(4)] public uint EndianMarker;
        [FieldOffset(8)] public uint Version;
        [FieldOffset(12)] public uint Frame;
        [FieldOffset(16)] public uint EntryCount;
        [FieldOffset(20)] public uint EntrySizeBytes;
        [FieldOffset(24)] public uint Cursor;
        [FieldOffset(28)] public uint AgentHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct AnxietyShelterSdfHeaderDTO
    {
        [FieldOffset(0)] public double3 OriginAUP;
        [FieldOffset(24)] public int3 Dimensions;
        [FieldOffset(36)] public float VoxelSizeMeters;
        [FieldOffset(40)] public float SolidThreshold;
        [FieldOffset(44)] public float SdfRangeMeters;
        [FieldOffset(48)] public uint Version;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] private ulong _pad0;
    }

    public static class AnxietyDecayDefaults
    {
        public static AnxietyProfileDTO BuildProfile()
        {
            AnxietyProfileDTO profile = default;
            profile.FearDecayRate = AnxietyDecayConstants.DefaultFearDecayRate;
            profile.AggressionDecayRate = AnxietyDecayConstants.DefaultAggressionDecayRate;
            profile.CalmingThreshold = AnxietyDecayConstants.DefaultCalmingThreshold;
            return profile;
        }

        public static AnxietyRuntimeTuningDTO BuildTuning()
        {
            AnxietyRuntimeTuningDTO tuning = default;
            tuning.BaseFearDecayRate = AnxietyDecayConstants.DefaultFearDecayRate;
            tuning.BaseAggressionDecayRate = AnxietyDecayConstants.DefaultAggressionDecayRate;
            tuning.CalmingThreshold = AnxietyDecayConstants.DefaultCalmingThreshold;
            tuning.ShelterCoolingMultiplier = AnxietyDecayConstants.DefaultShelterCoolingMultiplier;
            tuning.LinearDecayScale = AnxietyDecayConstants.DefaultLinearDecayScale;
            tuning.SimulationDeltaSeconds = 1f / 30f;
            tuning.GlobalQualityWeight = 1f;
            tuning.ExactExpWeight01 = 1f;
            tuning.FaultMicroseconds = AnxietyDecayConstants.FaultMicroseconds;
            tuning.ActiveProfileCount = 1u;
            tuning.Flags = AnxietyDecayFlags.Active;
            return tuning;
        }

        public static AnxietyShelterSdfHeaderDTO BuildShelterHeader()
        {
            AnxietyShelterSdfHeaderDTO header = default;
            header.OriginAUP = new double3(-64.0, -64.0, -64.0);
            header.Dimensions = new int3(32, 32, 32);
            header.VoxelSizeMeters = 4f;
            header.SolidThreshold = -0.05f;
            header.SdfRangeMeters = 4f;
            header.Version = 1u;
            header.Flags = AnxietyDecayFlags.Active;
            return header;
        }
    }
}
