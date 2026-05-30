using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;

namespace Hecton8.Physiology
{
    public static class ShinobuSuitIntegrityConstants
    {
        public const int DefaultEntityCapacity = 64;
        public const int ProfileCapacity = 16;
        public const int TelemetryFrameCount = 300;
        public const int MockPressureSampleCount = 300;
        public const int CsvMaxBytes = 8192;
        public const int FrameJobBatchSize = 16;
        public const uint SourceHash = 0x53333233u; // S323
        public const uint PlayerTargetHash = ShinobuPhysiologyConstants.PlayerTargetHash;
        public const uint StandardSuitHash = 0x53554954u; // SUIT fallback
        public const uint ReinforcedSuitHash = 0x52455354u; // REST
        public const uint ExosuitHash = 0x5052574Eu; // PRWN
        public const uint SubmarineHullHash = 0x48554C4Cu; // HULL
        public const uint CombatDamageTypeBarotraumaImplosion = (1u << 0) | (1u << 7); // Pressure | MicroFracture
        public const uint AcousticSourceMetalGroan = 0x47524F41u; // GROA
        public const float SurfacePressureAtm = 1f;
        public const float AtmPerMeter = 0.1f;
        public const float MinimumSafePressureAtm = 1f;
        public const float DefaultTickBudgetMicroseconds = 100f;
        public const BufferID StateBuffer = BufferID.ShinobuSuitIntegrityStates;
        public const BufferID ProfileBuffer = BufferID.ShinobuSuitIntegrityProfiles;
        public const BufferID TuningBuffer = BufferID.ShinobuSuitIntegrityTuning;
        public const BufferID TelemetryBuffer = BufferID.ShinobuSuitIntegrityTelemetryRing;
        public const BufferID VisualBuffer = BufferID.ShinobuSuitIntegrityVisuals;
        public const BufferID MockAupBuffer = BufferID.ShinobuSuitIntegrityMockAups;
    }

    public static class SuitIntegrityFlags
    {
        public const uint Initialized = 1u << 0;
        public const uint Warning = 1u << 1;
        public const uint Buckling = 1u << 2;
        public const uint Imploded = 1u << 3;
        public const uint NonFinitePressure = 1u << 4;
        public const uint OverBudget = 1u << 5;
        public const uint MockProfile = 1u << 6;
        public const uint CsvProfile = 1u << 7;
        public const uint AcousticGroan = 1u << 8;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SuitIntegrityDTO
    {
        [FieldOffset(0)] public float CurrentIntegrity01;
        [FieldOffset(4)] public float AppliedPressureATM;
        [FieldOffset(8)] public float MicroFractureAccumulation;
        [FieldOffset(12)] public uint EquippedSuitHash;
        [FieldOffset(16)] public uint IntegrityFlags;
        [FieldOffset(20)] private uint _pad0;
        [FieldOffset(24)] private uint _pad1;
        [FieldOffset(28)] private uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SuitPressureProfileDTO
    {
        [FieldOffset(0)] public uint SuitHash;
        [FieldOffset(4)] public float MaxSafePressureATM;
        [FieldOffset(8)] public float YieldConstant;
        [FieldOffset(12)] public float CriticalFractureThreshold;
        [FieldOffset(16)] public float FractureIntegrityDamageRate;
        [FieldOffset(20)] public float VisualBucklingGain;
        [FieldOffset(24)] public float GroanOverpressureThreshold;
        [FieldOffset(28)] public uint Flags;
        [FieldOffset(32)] public float LowTierYieldScale;
        [FieldOffset(36)] public float MiddleTierYieldScale;
        [FieldOffset(40)] public float HighTierYieldScale;
        [FieldOffset(44)] public float UltraTierYieldScale;
        [FieldOffset(48)] public uint ProfileIndex;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private uint _pad1;
        [FieldOffset(60)] private uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SuitIntegrityTuningDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float TickBudgetMicroseconds;
        [FieldOffset(8)] public float WarningOverpressure;
        [FieldOffset(12)] public float BuckleOverpressure;
        [FieldOffset(16)] public float CatastrophicIntegrity01;
        [FieldOffset(20)] public float AcousticIntervalMinSeconds;
        [FieldOffset(24)] public float AcousticIntervalMaxSeconds;
        [FieldOffset(28)] public float VisualDeformationGain;
        [FieldOffset(32)] public float MockMaxDepthMeters;
        [FieldOffset(36)] public float MockDurationSeconds;
        [FieldOffset(40)] public uint DefaultSuitHash;
        [FieldOffset(44)] public uint Version;
        [FieldOffset(48)] public uint Flags;
        [FieldOffset(52)] private uint _pad0;
        [FieldOffset(56)] private uint _pad1;
        [FieldOffset(60)] private uint _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SuitIntegrityVisualDTO
    {
        [FieldOffset(0)] public float AppliedPressureATM;
        [FieldOffset(4)] public float OverpressureScalar;
        [FieldOffset(8)] public float Buckling01;
        [FieldOffset(12)] public float CurrentIntegrity01;
        [FieldOffset(16)] public float MicroFractureAccumulation;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Frame;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SuitHydrostaticMockAupDTO
    {
        [FieldOffset(0)] public Unity.Mathematics.double3 PlayerAup;
        [FieldOffset(24)] public Unity.Mathematics.double3 SeaLevelAup;
        [FieldOffset(48)] public float TimeSeconds;
        [FieldOffset(52)] public float DepthMeters;
        [FieldOffset(56)] public uint Frame;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SuitIntegrityTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint EntityHash;
        [FieldOffset(16)] public float DepthMeters;
        [FieldOffset(20)] public float AppliedPressureATM;
        [FieldOffset(24)] public float OverpressureScalar;
        [FieldOffset(28)] public float MicroFractureAccumulation;
        [FieldOffset(32)] public float CurrentIntegrity01;
        [FieldOffset(36)] public float VisualBuckling01;
        [FieldOffset(40)] public float ExecutionMicroseconds;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public uint EquippedSuitHash;
        [FieldOffset(52)] public float TickIntervalSeconds;
        [FieldOffset(56)] public uint SignalFlags;
        [FieldOffset(60)] private uint _pad0;
    }

    public static class ShinobuSuitIntegrityLayoutGuards
    {
        public static bool ValidateLayouts()
        {
            return ValidateSuitIntegrityDto() &&
                   ValidateProfileDto() &&
                   ValidateTuningDto() &&
                   ValidateVisualDto() &&
                   ValidateMockAupDto() &&
                   ValidateTelemetryDto();
        }

        public static bool ValidateSuitIntegrityDto()
        {
            return UnsafeUtility.SizeOf<SuitIntegrityDTO>() == 32 &&
                   Marshal.OffsetOf<SuitIntegrityDTO>(nameof(SuitIntegrityDTO.CurrentIntegrity01)).ToInt32() == 0 &&
                   Marshal.OffsetOf<SuitIntegrityDTO>(nameof(SuitIntegrityDTO.AppliedPressureATM)).ToInt32() == 4 &&
                   Marshal.OffsetOf<SuitIntegrityDTO>(nameof(SuitIntegrityDTO.MicroFractureAccumulation)).ToInt32() == 8 &&
                   Marshal.OffsetOf<SuitIntegrityDTO>(nameof(SuitIntegrityDTO.EquippedSuitHash)).ToInt32() == 12 &&
                   Marshal.OffsetOf<SuitIntegrityDTO>(nameof(SuitIntegrityDTO.IntegrityFlags)).ToInt32() == 16;
        }

        private static bool ValidateProfileDto()
        {
            return UnsafeUtility.SizeOf<SuitPressureProfileDTO>() == 64 &&
                   Marshal.OffsetOf<SuitPressureProfileDTO>(nameof(SuitPressureProfileDTO.SuitHash)).ToInt32() == 0 &&
                   Marshal.OffsetOf<SuitPressureProfileDTO>(nameof(SuitPressureProfileDTO.MaxSafePressureATM)).ToInt32() == 4 &&
                   Marshal.OffsetOf<SuitPressureProfileDTO>(nameof(SuitPressureProfileDTO.UltraTierYieldScale)).ToInt32() == 44;
        }

        private static bool ValidateTuningDto()
        {
            return UnsafeUtility.SizeOf<SuitIntegrityTuningDTO>() == 64 &&
                   Marshal.OffsetOf<SuitIntegrityTuningDTO>(nameof(SuitIntegrityTuningDTO.GlobalQualityWeight)).ToInt32() == 0 &&
                   Marshal.OffsetOf<SuitIntegrityTuningDTO>(nameof(SuitIntegrityTuningDTO.DefaultSuitHash)).ToInt32() == 40 &&
                   Marshal.OffsetOf<SuitIntegrityTuningDTO>(nameof(SuitIntegrityTuningDTO.Flags)).ToInt32() == 48;
        }

        private static bool ValidateVisualDto()
        {
            return UnsafeUtility.SizeOf<SuitIntegrityVisualDTO>() == 32 &&
                   Marshal.OffsetOf<SuitIntegrityVisualDTO>(nameof(SuitIntegrityVisualDTO.Buckling01)).ToInt32() == 8 &&
                   Marshal.OffsetOf<SuitIntegrityVisualDTO>(nameof(SuitIntegrityVisualDTO.Frame)).ToInt32() == 28;
        }

        private static bool ValidateMockAupDto()
        {
            return UnsafeUtility.SizeOf<SuitHydrostaticMockAupDTO>() == 64 &&
                   Marshal.OffsetOf<SuitHydrostaticMockAupDTO>(nameof(SuitHydrostaticMockAupDTO.PlayerAup)).ToInt32() == 0 &&
                   Marshal.OffsetOf<SuitHydrostaticMockAupDTO>(nameof(SuitHydrostaticMockAupDTO.SeaLevelAup)).ToInt32() == 24 &&
                   Marshal.OffsetOf<SuitHydrostaticMockAupDTO>(nameof(SuitHydrostaticMockAupDTO.DepthMeters)).ToInt32() == 52;
        }

        private static bool ValidateTelemetryDto()
        {
            return UnsafeUtility.SizeOf<SuitIntegrityTelemetryEntry>() == 64 &&
                   Marshal.OffsetOf<SuitIntegrityTelemetryEntry>(nameof(SuitIntegrityTelemetryEntry.StateHash)).ToInt32() == 0 &&
                   Marshal.OffsetOf<SuitIntegrityTelemetryEntry>(nameof(SuitIntegrityTelemetryEntry.AppliedPressureATM)).ToInt32() == 20 &&
                   Marshal.OffsetOf<SuitIntegrityTelemetryEntry>(nameof(SuitIntegrityTelemetryEntry.ExecutionMicroseconds)).ToInt32() == 40 &&
                   Marshal.OffsetOf<SuitIntegrityTelemetryEntry>(nameof(SuitIntegrityTelemetryEntry.SignalFlags)).ToInt32() == 56;
        }
    }
}
