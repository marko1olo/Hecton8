using System.Runtime.InteropServices;
using Unity.Collections.LowLevel.Unsafe;
#if UNITY_EDITOR
using System.Reflection;
#endif

namespace Hecton8.Construction
{
    public static class HatchLockConstants
    {
        public const int TelemetryFrameCount = 300;
        public const int ProfileCapacity = 32;
        public const int ShaderUploadCapacity = 256;
        public const int CsvScratchBytes = 8192;
        public const int PairedFluidRowsPerHatch = 2;
        public const int MinStructuralScanRows = 32;
        public const uint SourceHash = 0x53333433u;
        public const uint DumpMagic = 0x48333433u;
        public const float DefaultSafePressureDifferentialATM = 0.5f;
        public const float DefaultStructuralJamThreshold01 = 0.3f;
        public const float DefaultCatastrophicPressureDifferentialATM = 0.75f;
        public const float AuthoritativeQualityWeight = 1f;
        public const float UltraTickIntervalSeconds = 0.016f;
        public const float SurvivalTickIntervalSeconds = 0.2f;
        public const float DumpThresholdMicroseconds = 200f;
    }

    public static class HatchFsmStateMask
    {
        public const uint None = 0u;
        public const uint Open = 1u << 0;
        public const uint Closed = 1u << 1;
        public const uint PressureLocked = 1u << 2;
        public const uint StructurallyJammed = 1u << 3;
        public const uint ManualOverride = 1u << 4;
        public const uint CatastrophicFlood = 1u << 5;
        public const uint Active = 1u << 6;
        public const uint AcousticQueued = 1u << 7;
        public const uint MissingCompartment = 1u << 8;
        public const uint NonFinite = 1u << 31;
    }

    public static class HatchTelemetryFlags
    {
        public const uint None = 0u;
        public const uint NonFinite = 1u << 0;
        public const uint DumpRequested = 1u << 1;
        public const uint ScheduleTimeOnly = 1u << 2;
        public const uint SlowTickOverBudget = 1u << 3;
        public const uint MissingCompartment = 1u << 4;
        public const uint CatastrophicFlood = 1u << 5;
    }

    public static class HatchTuningFlags
    {
        public const uint None = 0u;
        public const uint ShaderUploadEnabled = 1u << 0;
        public const uint HardwareProfileEnvelopeApplied = 1u << 1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HatchStateDTO
    {
        [FieldOffset(0)] public uint RoomAHashID;
        [FieldOffset(4)] public uint RoomBHashID;
        [FieldOffset(8)] public float PressureDifferentialATM;
        [FieldOffset(12)] public uint FsmStateMask;
        [FieldOffset(16)] private byte _pad0;
        [FieldOffset(17)] private byte _pad1;
        [FieldOffset(18)] private byte _pad2;
        [FieldOffset(19)] private byte _pad3;
        [FieldOffset(20)] private byte _pad4;
        [FieldOffset(21)] private byte _pad5;
        [FieldOffset(22)] private byte _pad6;
        [FieldOffset(23)] private byte _pad7;
        [FieldOffset(24)] private byte _pad8;
        [FieldOffset(25)] private byte _pad9;
        [FieldOffset(26)] private byte _pad10;
        [FieldOffset(27)] private byte _pad11;
        [FieldOffset(28)] private byte _pad12;
        [FieldOffset(29)] private byte _pad13;
        [FieldOffset(30)] private byte _pad14;
        [FieldOffset(31)] private byte _pad15;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HatchTuningDTO
    {
        [FieldOffset(0)] public float SafePressureDifferentialATM;
        [FieldOffset(4)] public float StructuralJamThreshold01;
        [FieldOffset(8)] public float CatastrophicPressureDifferentialATM;
        [FieldOffset(12)] public float GlobalQualityWeight;
        [FieldOffset(16)] public float TickIntervalSeconds;
        [FieldOffset(20)] public uint ActiveCount;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct HatchHardwareProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float SafePressureDifferentialATM;
        [FieldOffset(8)] public float StructuralJamThreshold01;
        [FieldOffset(12)] public float CatastrophicPressureDifferentialATM;
        [FieldOffset(16)] public float ManualBreakFloodScalar;
        [FieldOffset(20)] public float VisualPulseHz;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public uint Reserved0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct HatchTelemetryEntry
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public uint ActiveCount;
        [FieldOffset(8)] public uint PressureLockedCount;
        [FieldOffset(12)] public uint JammedCount;
        [FieldOffset(16)] public uint CatastrophicFloodCount;
        [FieldOffset(20)] public float MaxPressureDifferentialATM;
        [FieldOffset(24)] public float AveragePressureDifferentialATM;
        [FieldOffset(28)] public float LastScheduleMicroseconds;
        [FieldOffset(32)] public uint StateHash;
        [FieldOffset(36)] public uint LastFaultRoomHash;
        [FieldOffset(40)] public uint Flags;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public float TickIntervalSeconds;
        [FieldOffset(52)] public uint EvaluatedCount;
        [FieldOffset(56)] public uint Reserved0;
        [FieldOffset(60)] public uint Reserved1;
    }

    public static class HatchLockLayoutGuard
    {
        public const int StateSizeBytes = 32;
        public const int TuningSizeBytes = 32;
        public const int ProfileSizeBytes = 32;
        public const int TelemetrySizeBytes = 64;

        public static bool ValidateLayout()
        {
            return ValidateStateLayout() &&
                   ValidateTuningLayout() &&
                   ValidateProfileLayout() &&
                   ValidateTelemetryLayout();
        }

        private static bool ValidateStateLayout()
        {
            if (UnsafeUtility.SizeOf<HatchStateDTO>() != StateSizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<HatchStateDTO>(nameof(HatchStateDTO.RoomAHashID)) == 0 &&
                   GetOffset<HatchStateDTO>(nameof(HatchStateDTO.RoomBHashID)) == 4 &&
                   GetOffset<HatchStateDTO>(nameof(HatchStateDTO.PressureDifferentialATM)) == 8 &&
                   GetOffset<HatchStateDTO>(nameof(HatchStateDTO.FsmStateMask)) == 12 &&
                   GetOffset<HatchStateDTO>("_pad0") == 16 &&
                   GetOffset<HatchStateDTO>("_pad15") == 31;
#else
            return true;
#endif
        }

        private static bool ValidateTuningLayout()
        {
            if (UnsafeUtility.SizeOf<HatchTuningDTO>() != TuningSizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<HatchTuningDTO>(nameof(HatchTuningDTO.SafePressureDifferentialATM)) == 0 &&
                   GetOffset<HatchTuningDTO>(nameof(HatchTuningDTO.StructuralJamThreshold01)) == 4 &&
                   GetOffset<HatchTuningDTO>(nameof(HatchTuningDTO.CatastrophicPressureDifferentialATM)) == 8 &&
                   GetOffset<HatchTuningDTO>(nameof(HatchTuningDTO.GlobalQualityWeight)) == 12 &&
                   GetOffset<HatchTuningDTO>(nameof(HatchTuningDTO.TickIntervalSeconds)) == 16 &&
                   GetOffset<HatchTuningDTO>(nameof(HatchTuningDTO.ActiveCount)) == 20 &&
                   GetOffset<HatchTuningDTO>(nameof(HatchTuningDTO.Flags)) == 24;
#else
            return true;
#endif
        }

        private static bool ValidateProfileLayout()
        {
            if (UnsafeUtility.SizeOf<HatchHardwareProfileDTO>() != ProfileSizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<HatchHardwareProfileDTO>(nameof(HatchHardwareProfileDTO.ProfileHash)) == 0 &&
                   GetOffset<HatchHardwareProfileDTO>(nameof(HatchHardwareProfileDTO.SafePressureDifferentialATM)) == 4 &&
                   GetOffset<HatchHardwareProfileDTO>(nameof(HatchHardwareProfileDTO.StructuralJamThreshold01)) == 8 &&
                   GetOffset<HatchHardwareProfileDTO>(nameof(HatchHardwareProfileDTO.CatastrophicPressureDifferentialATM)) == 12 &&
                   GetOffset<HatchHardwareProfileDTO>(nameof(HatchHardwareProfileDTO.ManualBreakFloodScalar)) == 16 &&
                   GetOffset<HatchHardwareProfileDTO>(nameof(HatchHardwareProfileDTO.VisualPulseHz)) == 20 &&
                   GetOffset<HatchHardwareProfileDTO>(nameof(HatchHardwareProfileDTO.Flags)) == 24;
#else
            return true;
#endif
        }

        private static bool ValidateTelemetryLayout()
        {
            if (UnsafeUtility.SizeOf<HatchTelemetryEntry>() != TelemetrySizeBytes)
                return false;
#if UNITY_EDITOR
            return
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.Frame)) == 0 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.ActiveCount)) == 4 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.PressureLockedCount)) == 8 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.JammedCount)) == 12 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.CatastrophicFloodCount)) == 16 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.MaxPressureDifferentialATM)) == 20 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.AveragePressureDifferentialATM)) == 24 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.LastScheduleMicroseconds)) == 28 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.StateHash)) == 32 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.LastFaultRoomHash)) == 36 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.Flags)) == 40 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.GlobalQualityWeight)) == 44 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.TickIntervalSeconds)) == 48 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.EvaluatedCount)) == 52 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.Reserved0)) == 56 &&
                   GetOffset<HatchTelemetryEntry>(nameof(HatchTelemetryEntry.Reserved1)) == 60;
#else
            return true;
#endif
        }

#if UNITY_EDITOR
        private static int GetOffset<T>(string fieldName)
            where T : struct
        {
            FieldInfo field = typeof(T).GetField(
                fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field != null ? UnsafeUtility.GetFieldOffset(field) : -1;
        }
#endif
    }
}
