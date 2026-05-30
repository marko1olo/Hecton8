using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public static class ShinobuRadiationMutationConstants
    {
        public const int DefaultEntityCapacity = 1;
        public const int TelemetryFrameCount = 300;
        public const int ProfileCapacity = 16;
        public const int CsvMaxBytes = 8192;
        public const int JobBatchSize = 16;
        public const uint SourceHash = 0x53333234u; // S324
        public const uint ToxicBloodSpeciesHash = 0x544F5842u; // TOXB
        public const uint DumpVersion = 1u;
        public const ulong DumpMagic = 0x533332344D555441u; // S324MUTA

        public const BufferID MutationStateBuffer = (BufferID)75320;
        public const BufferID MutationTuningBuffer = (BufferID)75321;
        public const BufferID MutationTelemetryBuffer = (BufferID)75322;
        public const BufferID MutationProfileBuffer = (BufferID)75323;
        public const BufferID MutationMockDoseBuffer = (BufferID)75325;

        public const float DefaultSafeDoseRad = 25f;
        public const float DefaultFatalDoseRad = 850f;
        public const float DefaultMaxStaminaPenaltyPercent = 0.42f;
        public const float DefaultHealingDecayPerSecond = 0.12f;
        public const float DefaultSeverityRisePerSecond = 2.25f;
        public const float DefaultSeverityFallPerSecond = 0.7f;
        public const float DefaultToxicBloodThreshold01 = 0.64f;
        public const float DefaultMockRampSeconds = 18f;
        public const float DefaultMockPeakDoseRad = 950f;
    }

    public static class RadiationMutationFlags
    {
        public const uint None = 0u;
        public const uint Active = 1u << 0;
        public const uint Critical = 1u << 1;
        public const uint Healing = 1u << 2;
        public const uint MockDose = 1u << 3;
        public const uint ToxicBloodVfxRequested = 1u << 4;
        public const uint ComplexNoiseAdmitted = 1u << 5;
        public const uint MetabolicBridgeApplied = 1u << 6;
        public const uint CsvProfile = 1u << 7;
        public const uint NonFiniteSanitized = 1u << 30;
        public const uint OverBudget = 1u << 31;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct MutationStateDTO
    {
        [FieldOffset(0)] public float MutationSeverity01;
        [FieldOffset(4)] public float MaxStaminaPenalty;
        [FieldOffset(8)] public float HealingSuppression01;
        [FieldOffset(12)] public uint MutationFlags;
        [FieldOffset(16)] private uint _pad0;
        [FieldOffset(20)] private uint _pad1;
        [FieldOffset(24)] private uint _pad2;
        [FieldOffset(28)] private uint _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RadiationMutationTuningDTO
    {
        [FieldOffset(0)] public float SafeDoseRad;
        [FieldOffset(4)] public float FatalDoseRad;
        [FieldOffset(8)] public float MaxStaminaPenaltyPercent;
        [FieldOffset(12)] public float HealingDecayPerSecond;
        [FieldOffset(16)] public float SeverityRisePerSecond;
        [FieldOffset(20)] public float SeverityFallPerSecond;
        [FieldOffset(24)] public float ToxicBloodThreshold01;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public float MockRampSeconds;
        [FieldOffset(36)] public float MockPeakDoseRad;
        [FieldOffset(40)] public float ShaderPulseStrength;
        [FieldOffset(44)] public float MetabolicToxicityScale;
        [FieldOffset(48)] public uint Version;
        [FieldOffset(52)] public uint Flags;
        [FieldOffset(56)] private uint _pad0;
        [FieldOffset(60)] private uint _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct RadiationMutationProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float SafeDoseRad;
        [FieldOffset(8)] public float FatalDoseRad;
        [FieldOffset(12)] public float MaxStaminaPenaltyPercent;
        [FieldOffset(16)] public float HealingDecayPerSecond;
        [FieldOffset(20)] public float ToxicBloodThreshold01;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct RadiationMutationTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float CumulativeDoseRad;
        [FieldOffset(20)] public float CurrentExposureRate;
        [FieldOffset(24)] public float AttenuatedDoseRad;
        [FieldOffset(28)] public float MutationSeverity01;
        [FieldOffset(32)] public float MaxStaminaPenalty;
        [FieldOffset(36)] public float HealingSuppression01;
        [FieldOffset(40)] public float GlobalQualityWeight;
        [FieldOffset(44)] public float ExecutionMicroseconds;
        [FieldOffset(48)] public float MetabolicToxicity;
        [FieldOffset(52)] public float VfxIntensity01;
        [FieldOffset(56)] public uint RingCursor;
        [FieldOffset(60)] public uint SourceHash;
    }

    public static class ShinobuRadiationMutationLayoutGuards
    {
        public static bool ValidateMutationLayouts()
        {
            return ValidateStateLayout() &&
                   ValidateTuningLayout() &&
                   ValidateProfileLayout() &&
                   ValidateTelemetryLayout();
        }

        public static bool ValidateStateLayout()
        {
            return UnsafeUtility.SizeOf<MutationStateDTO>() == 32 &&
                   OffsetOf<MutationStateDTO>(nameof(MutationStateDTO.MutationSeverity01)) == 0 &&
                   OffsetOf<MutationStateDTO>(nameof(MutationStateDTO.MaxStaminaPenalty)) == 4 &&
                   OffsetOf<MutationStateDTO>(nameof(MutationStateDTO.HealingSuppression01)) == 8 &&
                   OffsetOf<MutationStateDTO>(nameof(MutationStateDTO.MutationFlags)) == 12 &&
                   OffsetOf<MutationStateDTO>("_pad0") == 16 &&
                   OffsetOf<MutationStateDTO>("_pad1") == 20 &&
                   OffsetOf<MutationStateDTO>("_pad2") == 24 &&
                   OffsetOf<MutationStateDTO>("_pad3") == 28;
        }

        public static bool ValidateTuningLayout()
        {
            return UnsafeUtility.SizeOf<RadiationMutationTuningDTO>() == 64 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.SafeDoseRad)) == 0 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.FatalDoseRad)) == 4 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.MaxStaminaPenaltyPercent)) == 8 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.HealingDecayPerSecond)) == 12 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.SeverityRisePerSecond)) == 16 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.SeverityFallPerSecond)) == 20 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.ToxicBloodThreshold01)) == 24 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.GlobalQualityWeight)) == 28 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.MockRampSeconds)) == 32 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.MockPeakDoseRad)) == 36 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.ShaderPulseStrength)) == 40 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.MetabolicToxicityScale)) == 44 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.Version)) == 48 &&
                   OffsetOf<RadiationMutationTuningDTO>(nameof(RadiationMutationTuningDTO.Flags)) == 52;
        }

        public static bool ValidateProfileLayout()
        {
            return UnsafeUtility.SizeOf<RadiationMutationProfileDTO>() == 32 &&
                   OffsetOf<RadiationMutationProfileDTO>(nameof(RadiationMutationProfileDTO.ProfileHash)) == 0 &&
                   OffsetOf<RadiationMutationProfileDTO>(nameof(RadiationMutationProfileDTO.SafeDoseRad)) == 4 &&
                   OffsetOf<RadiationMutationProfileDTO>(nameof(RadiationMutationProfileDTO.FatalDoseRad)) == 8 &&
                   OffsetOf<RadiationMutationProfileDTO>(nameof(RadiationMutationProfileDTO.MaxStaminaPenaltyPercent)) == 12 &&
                   OffsetOf<RadiationMutationProfileDTO>(nameof(RadiationMutationProfileDTO.HealingDecayPerSecond)) == 16 &&
                   OffsetOf<RadiationMutationProfileDTO>(nameof(RadiationMutationProfileDTO.ToxicBloodThreshold01)) == 20 &&
                   OffsetOf<RadiationMutationProfileDTO>(nameof(RadiationMutationProfileDTO.Flags)) == 24;
        }

        public static bool ValidateTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<RadiationMutationTelemetryEntry>() == 64 &&
                   OffsetOf<RadiationMutationTelemetryEntry>(nameof(RadiationMutationTelemetryEntry.StateHash)) == 0 &&
                   OffsetOf<RadiationMutationTelemetryEntry>(nameof(RadiationMutationTelemetryEntry.Frame)) == 8 &&
                   OffsetOf<RadiationMutationTelemetryEntry>(nameof(RadiationMutationTelemetryEntry.Flags)) == 12 &&
                   OffsetOf<RadiationMutationTelemetryEntry>(nameof(RadiationMutationTelemetryEntry.CumulativeDoseRad)) == 16 &&
                   OffsetOf<RadiationMutationTelemetryEntry>(nameof(RadiationMutationTelemetryEntry.MutationSeverity01)) == 28 &&
                   OffsetOf<RadiationMutationTelemetryEntry>(nameof(RadiationMutationTelemetryEntry.ExecutionMicroseconds)) == 44 &&
                   OffsetOf<RadiationMutationTelemetryEntry>(nameof(RadiationMutationTelemetryEntry.SourceHash)) == 60;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            FieldInfo field = typeof(T).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            return field == null ? -1 : UnsafeUtility.GetFieldOffset(field);
        }
    }

    public static class ShinobuRadiationMutationJobMath
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeFinite(float value, float fallback)
        {
            return math.isfinite(value) ? value : fallback;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SanitizeUnit(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproximateExpNegPositive(float value)
        {
            float x = math.max(0f, SanitizeFinite(value, 0f));
            float d = 1f + x + x * x * 0.48f + x * x * x * 0.235f;
            return math.rcp(math.max(0.0001f, d));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RadiationMutationTuningDTO BuildDefaultTuning()
        {
            RadiationMutationTuningDTO tuning = default;
            tuning.SafeDoseRad = ShinobuRadiationMutationConstants.DefaultSafeDoseRad;
            tuning.FatalDoseRad = ShinobuRadiationMutationConstants.DefaultFatalDoseRad;
            tuning.MaxStaminaPenaltyPercent = ShinobuRadiationMutationConstants.DefaultMaxStaminaPenaltyPercent;
            tuning.HealingDecayPerSecond = ShinobuRadiationMutationConstants.DefaultHealingDecayPerSecond;
            tuning.SeverityRisePerSecond = ShinobuRadiationMutationConstants.DefaultSeverityRisePerSecond;
            tuning.SeverityFallPerSecond = ShinobuRadiationMutationConstants.DefaultSeverityFallPerSecond;
            tuning.ToxicBloodThreshold01 = ShinobuRadiationMutationConstants.DefaultToxicBloodThreshold01;
            tuning.GlobalQualityWeight = 1f;
            tuning.MockRampSeconds = ShinobuRadiationMutationConstants.DefaultMockRampSeconds;
            tuning.MockPeakDoseRad = ShinobuRadiationMutationConstants.DefaultMockPeakDoseRad;
            tuning.ShaderPulseStrength = 0.16f;
            tuning.MetabolicToxicityScale = 1.35f;
            tuning.Version = 1u;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static RadiationMutationTuningDTO SanitizeTuning(RadiationMutationTuningDTO tuning)
        {
            if (tuning.Version == 0u)
                tuning = BuildDefaultTuning();

            tuning.SafeDoseRad = math.clamp(SanitizeFinite(tuning.SafeDoseRad, ShinobuRadiationMutationConstants.DefaultSafeDoseRad), 0f, 10000f);
            tuning.FatalDoseRad = math.max(tuning.SafeDoseRad + 1f, SanitizeFinite(tuning.FatalDoseRad, ShinobuRadiationMutationConstants.DefaultFatalDoseRad));
            tuning.MaxStaminaPenaltyPercent = math.clamp(SanitizeFinite(tuning.MaxStaminaPenaltyPercent, ShinobuRadiationMutationConstants.DefaultMaxStaminaPenaltyPercent), 0f, 0.95f);
            tuning.HealingDecayPerSecond = math.clamp(SanitizeFinite(tuning.HealingDecayPerSecond, ShinobuRadiationMutationConstants.DefaultHealingDecayPerSecond), 0f, 8f);
            tuning.SeverityRisePerSecond = math.clamp(SanitizeFinite(tuning.SeverityRisePerSecond, ShinobuRadiationMutationConstants.DefaultSeverityRisePerSecond), 0.01f, 30f);
            tuning.SeverityFallPerSecond = math.clamp(SanitizeFinite(tuning.SeverityFallPerSecond, ShinobuRadiationMutationConstants.DefaultSeverityFallPerSecond), 0.01f, 30f);
            tuning.ToxicBloodThreshold01 = math.clamp(SanitizeFinite(tuning.ToxicBloodThreshold01, ShinobuRadiationMutationConstants.DefaultToxicBloodThreshold01), 0f, 1f);
            tuning.GlobalQualityWeight = math.saturate(SanitizeFinite(tuning.GlobalQualityWeight, 1f));
            tuning.MockRampSeconds = math.clamp(SanitizeFinite(tuning.MockRampSeconds, ShinobuRadiationMutationConstants.DefaultMockRampSeconds), 1f, 600f);
            tuning.MockPeakDoseRad = math.clamp(SanitizeFinite(tuning.MockPeakDoseRad, ShinobuRadiationMutationConstants.DefaultMockPeakDoseRad), tuning.SafeDoseRad, 50000f);
            tuning.ShaderPulseStrength = math.clamp(SanitizeFinite(tuning.ShaderPulseStrength, 0.16f), 0f, 1f);
            tuning.MetabolicToxicityScale = math.clamp(SanitizeFinite(tuning.MetabolicToxicityScale, 1.35f), 0f, 8f);
            tuning.Version = 1u;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MixStateHash(ulong hash, uint value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }
    }
}
