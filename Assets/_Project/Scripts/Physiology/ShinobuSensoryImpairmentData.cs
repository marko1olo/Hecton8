using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Hecton8.Core.Memory;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physiology
{
    public static class ShinobuSensoryImpairmentConstants
    {
        public const int DefaultEntityCapacity = 1;
        public const int TelemetryFrameCount = 300;
        public const int ProfileCapacity = 16;
        public const int CsvMaxBytes = 8192;
        public const int JobBatchSize = 16;
        public const uint SourceHash = 0x53333232u; // S322
        public const uint DumpVersion = 1u;
        public const ulong DumpMagic = 0x533332324859504Fu; // S322HYPO

        public const BufferID SensoryImpairmentBuffer = (BufferID)75220;
        public const BufferID SensoryImpairmentTuningBuffer = (BufferID)75221;
        public const BufferID SensoryImpairmentTelemetryBuffer = (BufferID)75222;
        public const BufferID SensoryImpairmentProfilesBuffer = (BufferID)75223;
        public const BufferID SensoryInputDriftDebugBuffer = (BufferID)75225;

        public const float DefaultHypoxiaCurveExponent = 3f;
        public const float DefaultMaxInputLatencyMilliseconds = 180f;
        public const float DefaultMaxMoveDrift = 0.32f;
        public const float DefaultMaxLookDriftDegrees = 34f;
        public const float DefaultLatencyFrameRate = 60f;
        public const float DefaultCheapDriftFrequency = 0.0475f;
        public const float DefaultComplexDriftScale = 0.65f;
        public const float DefaultMockCycleSeconds = 28f;
        public const float DefaultMockMaxDepthMeters = 90f;
    }

    public static class SensoryImpairmentFlags
    {
        public const uint None = 0u;
        public const uint HypoxiaActive = 1u << 0;
        public const uint NarcosisActive = 1u << 1;
        public const uint LatencyActive = 1u << 2;
        public const uint ComplexNoiseAdmitted = 1u << 3;
        public const uint MockToxicity = 1u << 4;
        public const uint NonFiniteSanitized = 1u << 5;
        public const uint OverBudget = 1u << 6;
        public const uint CsvProfile = 1u << 7;
        public const uint InputCorrupted = 1u << 8;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SensoryImpairmentDTO
    {
        [FieldOffset(0)] public float HypoxiaVignette01;
        [FieldOffset(4)] public float NarcosisDrift01;
        [FieldOffset(8)] public float InputLatencyMilliseconds;
        [FieldOffset(12)] public uint ImpairmentFlags;
        [FieldOffset(16)] private uint _pad0;
        [FieldOffset(20)] private uint _pad1;
        [FieldOffset(24)] private uint _pad2;
        [FieldOffset(28)] private uint _pad3;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SensoryImpairmentTuningDTO
    {
        [FieldOffset(0)] public float HypoxiaPartialPressureAtm;
        [FieldOffset(4)] public float AnoxiaPartialPressureAtm;
        [FieldOffset(8)] public float HypoxiaCurveExponent;
        [FieldOffset(12)] public float NarcosisStartAtm;
        [FieldOffset(16)] public float NarcosisFullAtm;
        [FieldOffset(20)] public float MaxNarcosisDriftScalar;
        [FieldOffset(24)] public float MaxLookDriftDegrees;
        [FieldOffset(28)] public float MaxInputLatencyMilliseconds;
        [FieldOffset(32)] public float LatencyFrameRate;
        [FieldOffset(36)] public float CheapDriftFrequency;
        [FieldOffset(40)] public float ComplexDriftScale;
        [FieldOffset(44)] public float GlobalQualityWeight;
        [FieldOffset(48)] public float MockCycleSeconds;
        [FieldOffset(52)] public float MockMaxDepthMeters;
        [FieldOffset(56)] public uint Version;
        [FieldOffset(60)] public uint Flags;
    }

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct SensoryImpairmentProfileDTO
    {
        [FieldOffset(0)] public uint ProfileHash;
        [FieldOffset(4)] public float HypoxiaPartialPressureAtm;
        [FieldOffset(8)] public float AnoxiaPartialPressureAtm;
        [FieldOffset(12)] public float NarcosisStartAtm;
        [FieldOffset(16)] public float NarcosisFullAtm;
        [FieldOffset(20)] public float MaxInputLatencyMilliseconds;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] private uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SensoryInputDriftDebugDTO
    {
        [FieldOffset(0)] public float2 RawMoveAxis;
        [FieldOffset(8)] public float2 CorruptedMoveAxis;
        [FieldOffset(16)] public float2 RawLookDelta;
        [FieldOffset(24)] public float2 CorruptedLookDelta;
        [FieldOffset(32)] public float HypoxiaVignette01;
        [FieldOffset(36)] public float NarcosisDrift01;
        [FieldOffset(40)] public uint Frame;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong StateHash;
        [FieldOffset(56)] private ulong _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = 64)]
    public struct SensoryImpairmentTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float HypoxiaVignette01;
        [FieldOffset(20)] public float NarcosisDrift01;
        [FieldOffset(24)] public float InputLatencyMilliseconds;
        [FieldOffset(28)] public float OxygenPartialPressureAtm;
        [FieldOffset(32)] public float NitrogenPartialPressureAtm;
        [FieldOffset(36)] public float CarbonDioxidePartialPressureAtm;
        [FieldOffset(40)] public float DepthMeters;
        [FieldOffset(44)] public float MoveDriftMagnitude;
        [FieldOffset(48)] public float LookDriftMagnitude;
        [FieldOffset(52)] public float GlobalQualityWeight;
        [FieldOffset(56)] public float ExecutionMicroseconds;
        [FieldOffset(60)] public uint RingCursor;
    }

    public static class ShinobuSensoryImpairmentLayoutGuards
    {
        public static bool ValidateSensoryLayouts()
        {
            return ValidateSensoryImpairmentLayout() &&
                   ValidateTuningLayout() &&
                   ValidateProfileLayout() &&
                   ValidateDriftDebugLayout() &&
                   ValidateTelemetryLayout() &&
                   ValidateInputInteropLayouts();
        }

        public static bool ValidateSensoryImpairmentLayout()
        {
            return UnsafeUtility.SizeOf<SensoryImpairmentDTO>() == 32 &&
                   OffsetOf<SensoryImpairmentDTO>(nameof(SensoryImpairmentDTO.HypoxiaVignette01)) == 0 &&
                   OffsetOf<SensoryImpairmentDTO>(nameof(SensoryImpairmentDTO.NarcosisDrift01)) == 4 &&
                   OffsetOf<SensoryImpairmentDTO>(nameof(SensoryImpairmentDTO.InputLatencyMilliseconds)) == 8 &&
                   OffsetOf<SensoryImpairmentDTO>(nameof(SensoryImpairmentDTO.ImpairmentFlags)) == 12 &&
                   OffsetOf<SensoryImpairmentDTO>("_pad0") == 16 &&
                   OffsetOf<SensoryImpairmentDTO>("_pad1") == 20 &&
                   OffsetOf<SensoryImpairmentDTO>("_pad2") == 24 &&
                   OffsetOf<SensoryImpairmentDTO>("_pad3") == 28;
        }

        public static bool ValidateTuningLayout()
        {
            return UnsafeUtility.SizeOf<SensoryImpairmentTuningDTO>() == 64 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.HypoxiaPartialPressureAtm)) == 0 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.AnoxiaPartialPressureAtm)) == 4 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.HypoxiaCurveExponent)) == 8 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.NarcosisStartAtm)) == 12 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.NarcosisFullAtm)) == 16 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.MaxNarcosisDriftScalar)) == 20 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.MaxLookDriftDegrees)) == 24 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.MaxInputLatencyMilliseconds)) == 28 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.LatencyFrameRate)) == 32 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.CheapDriftFrequency)) == 36 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.ComplexDriftScale)) == 40 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.GlobalQualityWeight)) == 44 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.MockCycleSeconds)) == 48 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.MockMaxDepthMeters)) == 52 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.Version)) == 56 &&
                   OffsetOf<SensoryImpairmentTuningDTO>(nameof(SensoryImpairmentTuningDTO.Flags)) == 60;
        }

        public static bool ValidateProfileLayout()
        {
            return UnsafeUtility.SizeOf<SensoryImpairmentProfileDTO>() == 32 &&
                   OffsetOf<SensoryImpairmentProfileDTO>(nameof(SensoryImpairmentProfileDTO.ProfileHash)) == 0 &&
                   OffsetOf<SensoryImpairmentProfileDTO>(nameof(SensoryImpairmentProfileDTO.HypoxiaPartialPressureAtm)) == 4 &&
                   OffsetOf<SensoryImpairmentProfileDTO>(nameof(SensoryImpairmentProfileDTO.MaxInputLatencyMilliseconds)) == 20 &&
                   OffsetOf<SensoryImpairmentProfileDTO>(nameof(SensoryImpairmentProfileDTO.Flags)) == 24;
        }

        public static bool ValidateDriftDebugLayout()
        {
            return UnsafeUtility.SizeOf<SensoryInputDriftDebugDTO>() == 64 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.RawMoveAxis)) == 0 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.CorruptedMoveAxis)) == 8 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.RawLookDelta)) == 16 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.CorruptedLookDelta)) == 24 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.HypoxiaVignette01)) == 32 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.NarcosisDrift01)) == 36 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.Frame)) == 40 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.Flags)) == 44 &&
                   OffsetOf<SensoryInputDriftDebugDTO>(nameof(SensoryInputDriftDebugDTO.StateHash)) == 48 &&
                   OffsetOf<SensoryInputDriftDebugDTO>("_pad0") == 56;
        }

        public static bool ValidateTelemetryLayout()
        {
            return UnsafeUtility.SizeOf<SensoryImpairmentTelemetryEntry>() == 64 &&
                   OffsetOf<SensoryImpairmentTelemetryEntry>(nameof(SensoryImpairmentTelemetryEntry.StateHash)) == 0 &&
                   OffsetOf<SensoryImpairmentTelemetryEntry>(nameof(SensoryImpairmentTelemetryEntry.Frame)) == 8 &&
                   OffsetOf<SensoryImpairmentTelemetryEntry>(nameof(SensoryImpairmentTelemetryEntry.Flags)) == 12 &&
                   OffsetOf<SensoryImpairmentTelemetryEntry>(nameof(SensoryImpairmentTelemetryEntry.HypoxiaVignette01)) == 16 &&
                   OffsetOf<SensoryImpairmentTelemetryEntry>(nameof(SensoryImpairmentTelemetryEntry.DepthMeters)) == 40 &&
                   OffsetOf<SensoryImpairmentTelemetryEntry>(nameof(SensoryImpairmentTelemetryEntry.GlobalQualityWeight)) == 52 &&
                   OffsetOf<SensoryImpairmentTelemetryEntry>(nameof(SensoryImpairmentTelemetryEntry.RingCursor)) == 60;
        }

        public static bool ValidateInputInteropLayouts()
        {
            return UnsafeUtility.SizeOf<InputStateDTO>() == 24 &&
                   OffsetOf<InputStateDTO>(nameof(InputStateDTO.LookDelta)) == 0 &&
                   OffsetOf<InputStateDTO>(nameof(InputStateDTO.MoveAxis)) == 8 &&
                   OffsetOf<InputStateDTO>(nameof(InputStateDTO.ButtonMask)) == 16 &&
                   OffsetOf<InputStateDTO>("_pad0") == 20 &&
                   UnsafeUtility.SizeOf<PredictedInputDTO>() == 32 &&
                   OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO.TickNumber)) == 0 &&
                   OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO.LocalMoveVector)) == 4 &&
                   OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO.LookDelta)) == 16 &&
                   OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO.ActionButtonsMask)) == 24 &&
                   OffsetOf<PredictedInputDTO>(nameof(PredictedInputDTO._pad0)) == 28 &&
                   UnsafeUtility.SizeOf<PredictedInputAupTargetDTO>() == 32 &&
                   OffsetOf<PredictedInputAupTargetDTO>(nameof(PredictedInputAupTargetDTO.TargetAupAbsolute)) == 8;
        }

        private static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }
    }

    public static class ShinobuSensoryImpairmentJobMath
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
        public static float FastLengthFromSq(float lengthSq)
        {
            if (!math.isfinite(lengthSq))
                return 0f;

            return lengthSq * math.rsqrt(math.max(lengthSq, 0.0001f));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SensoryImpairmentTuningDTO BuildDefaultTuning()
        {
            SensoryImpairmentTuningDTO tuning = default;
            tuning.HypoxiaPartialPressureAtm = ShinobuPhysiologyConstants.HypoxiaPartialPressureAtm;
            tuning.AnoxiaPartialPressureAtm = ShinobuPhysiologyConstants.AnoxiaPartialPressureAtm;
            tuning.HypoxiaCurveExponent = ShinobuSensoryImpairmentConstants.DefaultHypoxiaCurveExponent;
            tuning.NarcosisStartAtm = 4f;
            tuning.NarcosisFullAtm = 7f;
            tuning.MaxNarcosisDriftScalar = ShinobuSensoryImpairmentConstants.DefaultMaxMoveDrift;
            tuning.MaxLookDriftDegrees = ShinobuSensoryImpairmentConstants.DefaultMaxLookDriftDegrees;
            tuning.MaxInputLatencyMilliseconds = ShinobuSensoryImpairmentConstants.DefaultMaxInputLatencyMilliseconds;
            tuning.LatencyFrameRate = ShinobuSensoryImpairmentConstants.DefaultLatencyFrameRate;
            tuning.CheapDriftFrequency = ShinobuSensoryImpairmentConstants.DefaultCheapDriftFrequency;
            tuning.ComplexDriftScale = ShinobuSensoryImpairmentConstants.DefaultComplexDriftScale;
            tuning.GlobalQualityWeight = 1f;
            tuning.MockCycleSeconds = ShinobuSensoryImpairmentConstants.DefaultMockCycleSeconds;
            tuning.MockMaxDepthMeters = ShinobuSensoryImpairmentConstants.DefaultMockMaxDepthMeters;
            tuning.Version = 1u;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SensoryImpairmentTuningDTO SanitizeTuning(SensoryImpairmentTuningDTO tuning)
        {
            if (tuning.Version == 0u)
                tuning = BuildDefaultTuning();

            tuning.AnoxiaPartialPressureAtm = math.clamp(SanitizeFinite(tuning.AnoxiaPartialPressureAtm, ShinobuPhysiologyConstants.AnoxiaPartialPressureAtm), 0.02f, 0.14f);
            tuning.HypoxiaPartialPressureAtm = math.clamp(SanitizeFinite(tuning.HypoxiaPartialPressureAtm, ShinobuPhysiologyConstants.HypoxiaPartialPressureAtm), tuning.AnoxiaPartialPressureAtm + 0.01f, 0.35f);
            tuning.HypoxiaCurveExponent = math.clamp(SanitizeFinite(tuning.HypoxiaCurveExponent, ShinobuSensoryImpairmentConstants.DefaultHypoxiaCurveExponent), 1f, 5f);
            tuning.NarcosisStartAtm = math.clamp(SanitizeFinite(tuning.NarcosisStartAtm, 4f), 1f, 12f);
            tuning.NarcosisFullAtm = math.max(tuning.NarcosisStartAtm + 0.25f, SanitizeFinite(tuning.NarcosisFullAtm, 7f));
            tuning.MaxNarcosisDriftScalar = math.clamp(SanitizeFinite(tuning.MaxNarcosisDriftScalar, ShinobuSensoryImpairmentConstants.DefaultMaxMoveDrift), 0f, 1f);
            tuning.MaxLookDriftDegrees = math.clamp(SanitizeFinite(tuning.MaxLookDriftDegrees, ShinobuSensoryImpairmentConstants.DefaultMaxLookDriftDegrees), 0f, 90f);
            tuning.MaxInputLatencyMilliseconds = math.clamp(SanitizeFinite(tuning.MaxInputLatencyMilliseconds, ShinobuSensoryImpairmentConstants.DefaultMaxInputLatencyMilliseconds), 0f, 500f);
            tuning.LatencyFrameRate = math.clamp(SanitizeFinite(tuning.LatencyFrameRate, ShinobuSensoryImpairmentConstants.DefaultLatencyFrameRate), 10f, 240f);
            tuning.CheapDriftFrequency = math.clamp(SanitizeFinite(tuning.CheapDriftFrequency, ShinobuSensoryImpairmentConstants.DefaultCheapDriftFrequency), 0.001f, 1f);
            tuning.ComplexDriftScale = math.clamp(SanitizeFinite(tuning.ComplexDriftScale, ShinobuSensoryImpairmentConstants.DefaultComplexDriftScale), 0f, 2f);
            tuning.GlobalQualityWeight = math.saturate(SanitizeFinite(tuning.GlobalQualityWeight, 1f));
            tuning.MockCycleSeconds = math.clamp(SanitizeFinite(tuning.MockCycleSeconds, ShinobuSensoryImpairmentConstants.DefaultMockCycleSeconds), 4f, 240f);
            tuning.MockMaxDepthMeters = math.clamp(SanitizeFinite(tuning.MockMaxDepthMeters, ShinobuSensoryImpairmentConstants.DefaultMockMaxDepthMeters), 1f, 12000f);
            tuning.Version = 1u;
            return tuning;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float EvaluatePolynomial01(float value, float exponent)
        {
            float x = math.saturate(SanitizeFinite(value, 0f));
            float x2 = x * x;
            float x3 = x2 * x;
            float x4 = x2 * x2;
            float x5 = x4 * x;
            float e = math.clamp(SanitizeFinite(exponent, 3f), 1f, 5f);
            float floor = math.floor(e);
            float low = math.select(x, x2, floor >= 2f);
            low = math.select(low, x3, floor >= 3f);
            low = math.select(low, x4, floor >= 4f);
            low = math.select(low, x5, floor >= 5f);
            float high = math.select(x2, x3, e >= 2f);
            high = math.select(high, x4, e >= 3f);
            high = math.select(high, x5, e >= 4f);
            float fraction = math.frac(e);
            return math.saturate(math.lerp(low, high, fraction));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float Smooth01(float value)
        {
            float x = math.saturate(value);
            return x * x * (3f - 2f * x);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveHypoxia01(float oxygenPartialPressureAtm, SensoryImpairmentTuningDTO tuning)
        {
            float ppO2 = math.max(0f, SanitizeFinite(oxygenPartialPressureAtm, ShinobuPhysiologyConstants.SurfaceOxygenPartialPressureAtm));
            float linear = math.saturate((tuning.HypoxiaPartialPressureAtm - ppO2) *
                                         math.rcp(math.max(0.0001f, tuning.HypoxiaPartialPressureAtm - tuning.AnoxiaPartialPressureAtm)));
            return EvaluatePolynomial01(linear, tuning.HypoxiaCurveExponent);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static ulong MixStateHash(ulong hash, uint value)
        {
            hash ^= value;
            return hash * 1099511628211UL;
        }
    }
}
