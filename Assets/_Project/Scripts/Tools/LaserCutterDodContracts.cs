namespace Hecton8.Tools
{
    using System;
    using System.Runtime.InteropServices;
    using Hecton8.Core.Memory;
    using Unity.Collections.LowLevel.Unsafe;
    using Unity.Mathematics;

    public static class LaserCutterDodConstants
    {
        public const int MaxRequests = 64;
        public const int MaxHitResults = 64;
        public const int BlackBoxFrameCount = 300;
        public const int CsvSpecCapacity = 32;
        public const int CsvScratchByteCapacity = 4096;
        public const int SdfSnapshotByteCapacity = 2 * 1024 * 1024;
        public const int MinCommandsPerJob = 8;
        public const int LowSparkCount = 0;
        public const int MiddleSparkCount = 24;
        public const int HighSparkCount = 64;
        public const int UltraSparkCount = 500;
        public const uint LaserCutterHash = 0x4C435452u; // LCTR
        public const uint SparkSpeciesHash = 0x4C53504Bu; // LSPK
        public const uint LayoutMagic = 0x53484C43u; // SHLC

        public const uint RequestFlagValid = 1u << 0;
        public const uint RequestFlagSuppressedByCooldown = 1u << 1;
        public const uint RequestFlagMock = 1u << 2;
        public const uint ResultFlagHit = 1u << 0;
        public const uint ResultFlagNonFinite = 1u << 1;
        public const uint ResultFlagShaderDentOnly = 1u << 2;
        public const uint ResultFlagGpuSparkOnly = 1u << 3;
        public const uint ResultFlagBatteryDrainQueued = 1u << 4;
        public const uint ResultFlagDecalQueued = 1u << 5;

        public const BufferID RequestsBuffer = (BufferID)71320;
        public const BufferID RequestCountBuffer = (BufferID)71321;
        public const BufferID ReservedLegacyProbeBuffer = (BufferID)71322;
        public const BufferID SdfSnapshotBuffer = (BufferID)71322;
        public const BufferID SdfProbeHitsBuffer = (BufferID)71323;
        public const BufferID HitResultsBuffer = (BufferID)71324;
        public const BufferID DeformationBuffer = (BufferID)71325;
        public const BufferID BatteryDrainBuffer = (BufferID)71326;
        public const BufferID GlowDecalBuffer = (BufferID)71327;
        public const BufferID ImpactVfxBuffer = (BufferID)71328;
        public const BufferID CooldownBuffer = (BufferID)71329;
        public const BufferID TelemetryRingBuffer = (BufferID)71330;
        public const BufferID TelemetryCursorBuffer = (BufferID)71331;
        public const BufferID TuningBuffer = (BufferID)71332;
        public const BufferID SpecBuffer = (BufferID)71333;
        public const BufferID CsvScratchBuffer = (BufferID)71334;
        public const BufferID CountersBuffer = (BufferID)71335;
        public const BufferID RequestMetaBuffer = (BufferID)71336;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LaserCutRequestDTO
    {
        [FieldOffset(0)] public double3 RayOriginAUP;
        [FieldOffset(24)] public float3 RayDirection;
        [FieldOffset(36)] public float CuttingPower;
        [FieldOffset(40)] public float MaximumDistance;
        [FieldOffset(44)] public uint ToolHashID;
        [FieldOffset(48)] public uint ParentEntityID;
        [FieldOffset(52)] public uint _pad0;
        [FieldOffset(56)] public uint _pad1;
        [FieldOffset(60)] public uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LaserCutRequestMetaDTO
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint Flags;
        [FieldOffset(8)] public uint RequestSequence;
        [FieldOffset(12)] public uint CooldownUntilFrame;
        [FieldOffset(16)] public uint LastAppliedFrame;
        [FieldOffset(20)] public uint Reserved0;
        [FieldOffset(24)] public ulong StateHash;
        [FieldOffset(32)] public ulong Reserved1;
        [FieldOffset(40)] public ulong Reserved2;
        [FieldOffset(48)] public ulong Reserved3;
        [FieldOffset(56)] public ulong Reserved4;
    }

    [StructLayout(LayoutKind.Explicit, Size = 96)]
    public struct LaserCutHitDTO
    {
        [FieldOffset(0)] public double3 HitAUP;
        [FieldOffset(24)] public double3 RayOriginAUP;
        [FieldOffset(48)] public float3 Normal;
        [FieldOffset(60)] public float DistanceMeters;
        [FieldOffset(64)] public uint ColliderInstanceID;
        [FieldOffset(68)] public uint MaterialHash;
        [FieldOffset(72)] public uint ToolHashID;
        [FieldOffset(76)] public uint ParentEntityID;
        [FieldOffset(80)] public float CuttingPower;
        [FieldOffset(84)] public float Heat01;
        [FieldOffset(88)] public uint Frame;
        [FieldOffset(92)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LaserCutDeformationStateDTO
    {
        [FieldOffset(0)] public double3 CenterAUP;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float RadiusMeters;
        [FieldOffset(40)] public float DentDepthMeters;
        [FieldOffset(44)] public float Heat01;
        [FieldOffset(48)] public float Progress01;
        [FieldOffset(52)] public uint TargetHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LaserCutBatteryDrainRequest
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public uint ParentEntityID;
        [FieldOffset(8)] public float Watts;
        [FieldOffset(12)] public float Seconds;
        [FieldOffset(16)] public float Progress01;
        [FieldOffset(20)] public uint Frame;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LaserCutGlowDecalRequestDTO
    {
        [FieldOffset(0)] public double3 CenterAUP;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float RadiusMeters;
        [FieldOffset(40)] public float Glow01;
        [FieldOffset(44)] public float LifetimeSeconds;
        [FieldOffset(48)] public uint ToolHashID;
        [FieldOffset(52)] public uint MaterialHash;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LaserCutImpactVfxDTO
    {
        [FieldOffset(0)] public double3 CenterAUP;
        [FieldOffset(24)] public float3 Normal;
        [FieldOffset(36)] public float Intensity01;
        [FieldOffset(40)] public uint SparkCount;
        [FieldOffset(44)] public float Heat01;
        [FieldOffset(48)] public uint ToolHashID;
        [FieldOffset(52)] public uint Frame;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint SpeciesHash;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LaserCutCooldownDTO
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public uint ParentEntityID;
        [FieldOffset(8)] public uint CooldownUntilFrame;
        [FieldOffset(12)] public uint LastAppliedFrame;
        [FieldOffset(16)] public float Accumulator01;
        [FieldOffset(20)] public float CooldownSeconds;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct LaserCutTelemetryEntry
    {
        [FieldOffset(0)] public double3 RayOriginAUP;
        [FieldOffset(24)] public double3 HitAUP;
        [FieldOffset(48)] public float3 RayDirection;
        [FieldOffset(60)] public float DistanceMeters;
        [FieldOffset(64)] public float CuttingPower;
        [FieldOffset(68)] public float QualityWeight;
        [FieldOffset(72)] public uint Frame;
        [FieldOffset(76)] public uint RequestSequence;
        [FieldOffset(80)] public uint ToolHashID;
        [FieldOffset(84)] public uint ParentEntityID;
        [FieldOffset(88)] public uint ColliderInstanceID;
        [FieldOffset(92)] public uint Flags;
        [FieldOffset(96)] public uint SparkCount;
        [FieldOffset(100)] public uint CooldownUntilFrame;
        [FieldOffset(104)] public uint LayoutMagic;
        [FieldOffset(108)] public float Heat01;
        [FieldOffset(112)] public ulong StateHash;
        [FieldOffset(120)] public float BatteryWatts;
        [FieldOffset(124)] public uint BurstWorkEstimateMicros;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LaserCutterTuningDTO
    {
        [FieldOffset(0)] public float MinimumPower01;
        [FieldOffset(4)] public float DefaultMaxDistanceMeters;
        [FieldOffset(8)] public float DentRadiusMinMeters;
        [FieldOffset(12)] public float DentRadiusMaxMeters;
        [FieldOffset(16)] public float GlowLifetimeSeconds;
        [FieldOffset(20)] public float BatteryWattsAtPowerOne;
        [FieldOffset(24)] public float CooldownFrames;
        [FieldOffset(28)] public float SparkIntensityScale;
        [FieldOffset(32)] public float LowSparkCount;
        [FieldOffset(36)] public float UltraSparkCount;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong VersionHash;
        [FieldOffset(56)] public ulong Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct LaserCutterSpecDTO
    {
        [FieldOffset(0)] public uint ToolHashID;
        [FieldOffset(4)] public float BatteryWattsAtPowerOne;
        [FieldOffset(8)] public float MaxDistanceMeters;
        [FieldOffset(12)] public float CutPowerScale;
        [FieldOffset(16)] public float CooldownSeconds;
        [FieldOffset(20)] public float DentRadiusMeters;
        [FieldOffset(24)] public float GlowLifetimeSeconds;
        [FieldOffset(28)] public float SparkScalar;
        [FieldOffset(32)] public uint Flags;
        [FieldOffset(36)] public uint ProfileHash;
        [FieldOffset(40)] public ulong Reserved0;
        [FieldOffset(48)] public ulong Reserved1;
        [FieldOffset(56)] public ulong Reserved2;
    }

#if UNITY_EDITOR
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LaserCutterCsvParseResult
    {
        [FieldOffset(0)] public int ParsedRows;
        [FieldOffset(4)] public int SkippedRows;
        [FieldOffset(8)] public uint FaultFlags;
        [FieldOffset(12)] public uint LastToolHashID;
        [FieldOffset(16)] public uint LastProfileHash;
        [FieldOffset(20)] public uint Reserved0;
        [FieldOffset(24)] public ulong Reserved1;
    }
#endif

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct LaserCutterCountersDTO
    {
        [FieldOffset(0)] public int RequestCount;
        [FieldOffset(4)] public int HitCount;
        [FieldOffset(8)] public int SuppressedCount;
        [FieldOffset(12)] public int NonFiniteCount;
        [FieldOffset(16)] public uint LastFrame;
        [FieldOffset(20)] public uint LastSequence;
        [FieldOffset(24)] public ulong StateHash;
    }

    public static class LaserCutterDodLayoutValidator
    {
        public const uint FaultRequestSize = 1u << 0;
        public const uint FaultOriginOffset = 1u << 1;
        public const uint FaultDirectionOffset = 1u << 2;
        public const uint FaultPowerOffset = 1u << 3;
        public const uint FaultRangeOffset = 1u << 4;
        public const uint FaultToolOffset = 1u << 5;
        public const uint FaultParentOffset = 1u << 6;
        public const uint FaultTelemetryCapacity = 1u << 7;
        public const uint FaultMetaSize = 1u << 8;
        public const uint FaultMetaFrameOffset = 1u << 9;
        public const uint FaultMetaFlagsOffset = 1u << 10;
        public const uint FaultMetaSequenceOffset = 1u << 11;
        public const uint FaultRequestPadOffset = 1u << 12;

        public static bool Validate(out uint faultFlags)
        {
            faultFlags = 0u;
            if (UnsafeUtility.SizeOf<LaserCutRequestDTO>() != 64)
                faultFlags |= FaultRequestSize;
            if (OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO.RayOriginAUP)) != 0)
                faultFlags |= FaultOriginOffset;
            if (OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO.RayDirection)) != 24)
                faultFlags |= FaultDirectionOffset;
            if (OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO.CuttingPower)) != 36)
                faultFlags |= FaultPowerOffset;
            if (OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO.MaximumDistance)) != 40)
                faultFlags |= FaultRangeOffset;
            if (OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO.ToolHashID)) != 44)
                faultFlags |= FaultToolOffset;
            if (OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO.ParentEntityID)) != 48)
                faultFlags |= FaultParentOffset;
            if (OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO._pad0)) != 52 ||
                OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO._pad1)) != 56 ||
                OffsetOf<LaserCutRequestDTO>(nameof(LaserCutRequestDTO._pad2)) != 60)
                faultFlags |= FaultRequestPadOffset;
            if (UnsafeUtility.SizeOf<LaserCutRequestMetaDTO>() != 64)
                faultFlags |= FaultMetaSize;
            if (OffsetOf<LaserCutRequestMetaDTO>(nameof(LaserCutRequestMetaDTO.Frame)) != 0)
                faultFlags |= FaultMetaFrameOffset;
            if (OffsetOf<LaserCutRequestMetaDTO>(nameof(LaserCutRequestMetaDTO.Flags)) != 4)
                faultFlags |= FaultMetaFlagsOffset;
            if (OffsetOf<LaserCutRequestMetaDTO>(nameof(LaserCutRequestMetaDTO.RequestSequence)) != 8)
                faultFlags |= FaultMetaSequenceOffset;
            if (LaserCutterDodConstants.BlackBoxFrameCount != RequiredBlackBoxFrameCount())
                faultFlags |= FaultTelemetryCapacity;

            return faultFlags == 0u;
        }

        private static int RequiredBlackBoxFrameCount()
        {
            return 300;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return (int)Marshal.OffsetOf<T>(fieldName);
        }
    }
}
