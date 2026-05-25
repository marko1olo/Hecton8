using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;

namespace Hecton8.Core
{
    public static class MathLodApproximation
    {
        public const int TelemetryFrameCount = 300;
        public const int TelemetryEntrySizeBytes = 64;
        public const int TortureResultSizeBytes = 64;
        public const int ConfigSizeBytes = 64;
        public const float Epsilon = 0.0001f;
        public const float PadeReducedMaxAbsError = 0.000000763f;
        public const float BhaskaraMaxAbsError = 0.001633f;
        public const float AtanFastMaxAbsError = 0.004883f;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float FiniteOr(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SaturateFinite(float value, float fallback)
        {
            return math.saturate(FiniteOr(value, fallback));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ClampFiniteWithDirectionalInfinity(float value, float min, float max, float nanFallback)
        {
            float nonFinite = math.select(nanFallback, max, value > 0f);
            nonFinite = math.select(nonFinite, min, value < 0f);
            return math.clamp(math.select(nonFinite, value, math.isfinite(value)), min, max);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float4 ClampFiniteWithDirectionalInfinity(float4 value, float min, float max, float nanFallback)
        {
            float4 min4 = new float4(min);
            float4 max4 = new float4(max);
            float4 nonFinite = math.select(new float4(nanFallback), max4, value > new float4(0f));
            nonFinite = math.select(nonFinite, min4, value < new float4(0f));
            return math.clamp(math.select(nonFinite, value, math.isfinite(value)), min4, max4);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothStep01(float value)
        {
            float t = math.saturate(value);
            return t * t * (3f - (2f * t));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float SmoothRange01(float start, float end, float value)
        {
            float width = math.max(Epsilon, end - start);
            return SmoothStep01((FiniteOr(value, start) - start) * math.rcp(width));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float BlendByQuality(float cheap, float expensive, float globalQualityWeight, float start, float end)
        {
            float blend = SmoothRange01(start, end, SaturateFinite(globalQualityWeight, 1f));
            return math.lerp(cheap, expensive, blend);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float4 ApproxExpNegPade33Reduced(float4 value)
        {
            float4 safe = ClampFiniteWithDirectionalInfinity(value, 0f, 4f, 0f);
            float4 x = safe * 0.25f;
            float4 x2 = x * x;
            float4 x3 = x2 * x;
            float4 numerator = 1f - (0.5f * x) + (0.1f * x2) - ((1f / 120f) * x3);
            float4 denominator = 1f + (0.5f * x) + (0.1f * x2) + ((1f / 120f) * x3);
            float4 baseDecay = numerator * math.rcp(math.max(denominator, new float4(Epsilon)));
            float4 decay2 = baseDecay * baseDecay;
            float4 decay4 = decay2 * decay2;
            return math.saturate(math.select(new float4(0f), decay4, math.isfinite(decay4)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxExpNegPade33Reduced(float value)
        {
            return ApproxExpNegPade33Reduced(new float4(value)).x;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxExpNegPade33Wide40(float value)
        {
            float safe = ClampFiniteWithDirectionalInfinity(value, 0f, 40f, 0f);
            float segmentDecay = ApproxExpNegPade33Reduced(safe * 0.1f);
            float decay2 = segmentDecay * segmentDecay;
            float decay4 = decay2 * decay2;
            float decay8 = decay4 * decay4;
            float decay10 = decay8 * decay2;
            return math.saturate(math.select(0f, decay10, math.isfinite(decay10)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxExpSignedPade33Wide40(float value)
        {
            float safe = ClampFiniteWithDirectionalInfinity(value, -40f, 40f, 0f);
            float decay = ApproxExpNegPade33Wide40(math.abs(safe));
            float growth = math.rcp(math.max(Epsilon, decay));
            float result = math.select(decay, growth, safe >= 0f);
            return math.select(1f, result, math.isfinite(result));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxExpPositivePade33Reduced(float value)
        {
            float safe = ClampFiniteWithDirectionalInfinity(value, 0f, 4f, 0f);
            float decay = ApproxExpNegPade33Reduced(safe);
            float growth = math.rcp(math.max(Epsilon, decay));
            return math.select(1f, growth, math.isfinite(growth));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float OneMinusApproxExpNegPade33Reduced(float value)
        {
            return math.saturate(1f - ApproxExpNegPade33Reduced(value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxSinBhaskara(float radians)
        {
            float angle = FiniteOr(radians, 0f);
            float cycle = angle * 0.15915494309189535f;
            float wrapped = cycle - math.floor(cycle);
            float x = wrapped * (2f * math.PI);
            float mirrored = math.select(x, (2f * math.PI) - x, x > math.PI);
            float sign = math.select(1f, -1f, x > math.PI);
            float shape = mirrored * (math.PI - mirrored);
            float numerator = 16f * shape;
            float denominator = math.max(Epsilon, (5f * math.PI * math.PI) - (4f * shape));
            float sine = sign * numerator * math.rcp(denominator);
            return math.clamp(math.select(0f, sine, math.isfinite(sine)), -1f, 1f);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxCosBhaskara(float radians)
        {
            return ApproxSinBhaskara(radians + (0.5f * math.PI));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ApproxSinCosBhaskara(float radians, out float sine, out float cosine)
        {
            sine = ApproxSinBhaskara(radians);
            cosine = ApproxCosBhaskara(radians);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxTanClamped(float radians, float maxAbs = 4096f)
        {
            ApproxSinCosBhaskara(radians, out float sine, out float cosine);
            float safeMax = math.max(1f, math.abs(FiniteOr(maxAbs, 4096f)));
            float signedDenominator = math.select(-math.max(Epsilon, math.abs(cosine)), math.max(Epsilon, math.abs(cosine)), cosine >= 0f);
            float tangent = sine * math.rcp(signedDenominator);
            return math.clamp(math.select(0f, tangent, math.isfinite(tangent)), -safeMax, safeMax);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxAtanFast(float value)
        {
            float x = FiniteOr(value, 0f);
            float ax = math.abs(x);
            float inv = math.rcp(math.max(ax, Epsilon));
            float reduced = math.select(ax, inv, ax > 1f);
            float reducedSq = reduced * reduced;
            float atanReduced = reduced * math.rcp(1f + (0.280872f * reducedSq));
            float angle = math.select(atanReduced, (0.5f * math.PI) - atanReduced, ax > 1f);
            float signed = math.select(-angle, angle, x >= 0f);
            return math.select(0f, signed, math.isfinite(signed));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxAtan2Fast(float y, float x)
        {
            float safeX = FiniteOr(x, 0f);
            float safeY = FiniteOr(y, 0f);
            float ratio = math.abs(safeY) * math.rcp(math.max(math.abs(safeX), Epsilon));
            float baseAngle = ApproxAtanFast(ratio);
            float angle = math.select(math.PI - baseAngle, baseAngle, safeX >= 0f);
            angle = math.select(angle, -angle, safeY < 0f);
            bool origin = math.abs(safeX) < Epsilon & math.abs(safeY) < Epsilon;
            angle = math.select(angle, 0f, origin);
            return math.select(0f, angle, math.isfinite(angle));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxAcosFast(float value)
        {
            float x = math.clamp(FiniteOr(value, 1f), -1f, 1f);
            float ax = math.abs(x);
            float oneMinus = math.max(0f, 1f - ax);
            float root = oneMinus * math.rsqrt(math.max(oneMinus, 0.000001f));
            float angle = (((-0.0187293f * ax + 0.0742610f) * ax - 0.2121144f) * ax + 1.5707288f) * root;
            angle = math.select(angle, math.PI - angle, x < 0f);
            return math.clamp(math.select(0f, angle, math.isfinite(angle)), 0f, math.PI);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxPow01Curve(float value01, float exponent)
        {
            float x = SaturateFinite(value01, 0f);
            float e = math.clamp(FiniteOr(exponent, 1f), 0.25f, 4f);
            float sqrt1 = math.sqrt(x);
            float sqrt2 = math.sqrt(sqrt1);
            float x2 = x * x;
            float x3 = x2 * x;
            float x4 = x2 * x2;
            float r025To05 = math.lerp(sqrt2, sqrt1, math.saturate((e - 0.25f) * 4f));
            float r05To1 = math.lerp(sqrt1, x, math.saturate((e - 0.5f) * 2f));
            float r1To2 = math.lerp(x, x2, math.saturate(e - 1f));
            float r2To3 = math.lerp(x2, x3, math.saturate(e - 2f));
            float r3To4 = math.lerp(x3, x4, math.saturate(e - 3f));
            float result = r3To4;
            result = math.select(result, r2To3, e < 3f);
            result = math.select(result, r1To2, e < 2f);
            result = math.select(result, r05To1, e < 1f);
            result = math.select(result, r025To05, e < 0.5f);
            return math.saturate(math.select(0f, result, math.isfinite(result)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<MathLodConfigDTO>() == ConfigSizeBytes &&
                   UnsafeUtility.SizeOf<MathLodTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<MathLodTortureResult>() == TortureResultSizeBytes;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = MathLodApproximation.ConfigSizeBytes)]
    public struct MathLodConfigDTO
    {
        [FieldOffset(0)] public float GlobalQualityWeight;
        [FieldOffset(4)] public float FractionalTimeSlice;
        [FieldOffset(8)] public float MinJacobiIterations;
        [FieldOffset(12)] public float MaxJacobiIterations;
        [FieldOffset(16)] public float PadeResidualCeiling;
        [FieldOffset(20)] public float BhaskaraResidualCeiling;
        [FieldOffset(24)] public float MathLodPressure01;
        [FieldOffset(28)] public float ActiveIterationBudget;
        [FieldOffset(32)] public uint Frame;
        [FieldOffset(36)] public uint Flags;
        [FieldOffset(40)] public float LastFrameMs;
        [FieldOffset(44)] public float VramPressure01;
        [FieldOffset(48)] public float ThermalPressure01;
        [FieldOffset(52)] public float ReservedQualityLane0;
        [FieldOffset(56)] public uint StateHash;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = MathLodApproximation.TelemetryEntrySizeBytes)]
    public struct MathLodTelemetryEntry
    {
        [FieldOffset(0)] public ulong StateHash;
        [FieldOffset(8)] public uint Frame;
        [FieldOffset(12)] public uint Flags;
        [FieldOffset(16)] public float GlobalQualityWeight;
        [FieldOffset(20)] public float ActiveIterations;
        [FieldOffset(24)] public float ApproxInput;
        [FieldOffset(28)] public float ApproxOutput;
        [FieldOffset(32)] public float ResidualEstimate;
        [FieldOffset(36)] public float SolverMicroseconds;
        [FieldOffset(40)] public float MaxResidualEstimate;
        [FieldOffset(44)] public float TemperatureCelsius;
        [FieldOffset(48)] public float PressureAtm;
        [FieldOffset(52)] public uint NonFiniteCount;
        [FieldOffset(56)] public uint SampleIndex;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = MathLodApproximation.TortureResultSizeBytes)]
    public struct MathLodTortureResult
    {
        [FieldOffset(0)] public uint SampleCount;
        [FieldOffset(4)] public uint NonFiniteCount;
        [FieldOffset(8)] public uint Flags;
        [FieldOffset(12)] public uint TelemetryEntryBytes;
        [FieldOffset(16)] public float MaxAbsOutput;
        [FieldOffset(20)] public float MaxResidualEstimate;
        [FieldOffset(24)] public float MinOutput;
        [FieldOffset(28)] public float MaxOutput;
        [FieldOffset(32)] public float GlobalQualityWeight;
        [FieldOffset(36)] public float WorstInput;
        [FieldOffset(40)] public float WorstTemperatureCelsius;
        [FieldOffset(44)] public float WorstPressureAtm;
        [FieldOffset(48)] public uint LastFrame;
        [FieldOffset(52)] public uint LastCursor;
        [FieldOffset(56)] public float WorstOutput;
        [FieldOffset(60)] public uint _pad1;
    }

    public static class MathLodRuntimeConfig
    {
        public const uint ConfigFlagSanitized = 1u << 0;
        public const uint ConfigFlagNonFiniteInput = 1u << 1;
        public const uint ConfigFlagMinimumSurvival = 1u << 2;
        public const uint ConfigFlagVisualOverkill = 1u << 3;
        public const uint ConfigFlagExternalPressure = 1u << 4;
        private const int ConfigSingletonLength = 1;
        private const int CursorSingletonLength = 1;
        private const float MinJacobiIterations = 2f;
        private const float MaxJacobiIterations = 50f;
        private const SystemID OwnerSystem = SystemID.HardwareHomeostasis;

        private static IDataVault s_vault;
        private static VaultGenerationHandle<MathLodConfigDTO> s_configHandle;
        private static VaultGenerationHandle<MathLodTelemetryEntry> s_telemetryHandle;
        private static VaultGenerationHandle<int> s_cursorHandle;
        private static bool s_faultDumped;

        public static long ResolveRequestedBytes()
        {
            return UnsafeUtility.SizeOf<MathLodConfigDTO>() +
                   ((long)MathLodApproximation.TelemetryFrameCount * UnsafeUtility.SizeOf<MathLodTelemetryEntry>()) +
                   UnsafeUtility.SizeOf<int>();
        }

        public static bool EnsureRuntimeBuffers(IDataVault vault)
        {
            if (vault == null || vault.IsAllocationLocked)
                return false;

            if (!ReferenceEquals(s_vault, vault))
            {
                ResetHandles();
                s_vault = vault;
            }

            if (!EnsureBuffer(
                    vault,
                    ref s_configHandle,
                    BufferID.ShinobuMathLodConfig,
                    ConfigSingletonLength,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<MathLodConfigDTO> config,
                    out bool configCreated))
            {
                return false;
            }

            if (!EnsureBuffer(
                    vault,
                    ref s_telemetryHandle,
                    BufferID.ShinobuMathLodTelemetryRing,
                    MathLodApproximation.TelemetryFrameCount,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<MathLodTelemetryEntry> telemetry,
                    out bool telemetryCreated))
            {
                return false;
            }

            if (!EnsureBuffer(
                    vault,
                    ref s_cursorHandle,
                    BufferID.ShinobuMathLodTelemetryCursor,
                    CursorSingletonLength,
                    NativeArrayOptions.ClearMemory,
                    out NativeArray<int> cursor,
                    out bool cursorCreated))
            {
                return false;
            }

            if (configCreated)
                config[0] = CreateDefaultConfig();
            if (telemetryCreated)
                ClearNative(telemetry);
            if (cursorCreated)
                cursor[0] = 0;
            return true;
        }

        public static void ReleaseRuntimeBuffers(IDataVault vault)
        {
            if (vault != null)
            {
                if (s_configHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in s_configHandle);
                if (s_telemetryHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in s_telemetryHandle);
                if (s_cursorHandle.BufferID != 0u)
                    vault.ReleaseBuffer(in s_cursorHandle);
            }

            ResetHandles();
        }

        public static bool PublishConfig(
            IDataVault vault,
            uint frame,
            float globalQualityWeight,
            float fractionalTimeSlice,
            float rawFrameMs,
            float vramPressure01,
            float thermalPressure01,
            uint externalFlags,
            out uint faultFlags)
        {
            faultFlags = 0u;
            if (!EnsureRuntimeBuffers(vault))
                return false;

            if (!vault.TryResolveHandle(in s_configHandle, out NativeArray<MathLodConfigDTO> config) ||
                !vault.TryResolveHandle(in s_telemetryHandle, out NativeArray<MathLodTelemetryEntry> telemetry) ||
                !vault.TryResolveHandle(in s_cursorHandle, out NativeArray<int> cursor) ||
                !config.IsCreated ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                config.Length < ConfigSingletonLength ||
                telemetry.Length < MathLodApproximation.TelemetryFrameCount ||
                cursor.Length < CursorSingletonLength)
            {
                return false;
            }

            bool sanitized = false;
            float quality = Sanitize(globalQualityWeight, 1f, ref sanitized);
            quality = math.saturate(quality);
            float timeSlice = Sanitize(fractionalTimeSlice, quality, ref sanitized);
            timeSlice = math.saturate(timeSlice);
            float frameMs = Sanitize(rawFrameMs, 0f, ref sanitized);
            frameMs = math.max(0f, frameMs);
            float vram = math.saturate(Sanitize(vramPressure01, 0f, ref sanitized));
            float thermal = math.saturate(Sanitize(thermalPressure01, 0f, ref sanitized));
            float pressure = math.saturate(1f - quality);
            float activeIterations = ResolveActiveIterationBudget(quality);
            uint flags = externalFlags & ConfigFlagExternalPressure;
            flags |= sanitized ? ConfigFlagSanitized | ConfigFlagNonFiniteInput : 0u;
            flags |= quality <= 0.1001f ? ConfigFlagMinimumSurvival : 0u;
            flags |= quality >= 0.95f ? ConfigFlagVisualOverkill : 0u;
            faultFlags = sanitized ? ConfigFlagNonFiniteInput : 0u;

            MathLodConfigDTO dto = default;
            dto.GlobalQualityWeight = quality;
            dto.FractionalTimeSlice = timeSlice;
            dto.MinJacobiIterations = MinJacobiIterations;
            dto.MaxJacobiIterations = MaxJacobiIterations;
            dto.PadeResidualCeiling = MathLodApproximation.PadeReducedMaxAbsError;
            dto.BhaskaraResidualCeiling = MathLodApproximation.BhaskaraMaxAbsError;
            dto.MathLodPressure01 = pressure;
            dto.ActiveIterationBudget = activeIterations;
            dto.Frame = frame;
            dto.Flags = flags;
            dto.LastFrameMs = frameMs;
            dto.VramPressure01 = vram;
            dto.ThermalPressure01 = thermal;
            dto.ReservedQualityLane0 = 0f;
            dto.StateHash = HashConfig(dto);
            dto._pad0 = 0u;
            config[0] = dto;

            int index = cursor[0];
            if ((uint)index >= (uint)telemetry.Length)
                index = 0;

            MathLodTelemetryEntry entry = default;
            entry.StateHash = dto.StateHash;
            entry.Frame = frame;
            entry.Flags = flags;
            entry.GlobalQualityWeight = quality;
            entry.ActiveIterations = activeIterations;
            entry.ApproxInput = pressure;
            entry.ApproxOutput = quality;
            entry.ResidualEstimate = MathLodApproximation.PadeReducedMaxAbsError;
            entry.SolverMicroseconds = 0f;
            entry.MaxResidualEstimate = math.max(MathLodApproximation.PadeReducedMaxAbsError, MathLodApproximation.BhaskaraMaxAbsError);
            entry.TemperatureCelsius = thermal;
            entry.PressureAtm = vram;
            entry.NonFiniteCount = sanitized ? 1u : 0u;
            entry.SampleIndex = (uint)index;
            telemetry[index] = entry;
            cursor[0] = (index + 1) % telemetry.Length;
            return true;
        }

        public static bool TryReadLatestConfig(out MathLodConfigDTO config)
        {
            config = default;
            IDataVault vault = s_vault;
            if (vault == null || s_configHandle.BufferID == 0u)
                return false;

            if (!vault.TryReadOnlyHandle(in s_configHandle, out NativeArray<MathLodConfigDTO>.ReadOnly configView) ||
                !configView.IsCreated ||
                configView.Length < ConfigSingletonLength)
            {
                return false;
            }

            config = configView[0];
            return true;
        }

        public static bool TryDumpOnFault(string projectRoot)
        {
            if (s_faultDumped)
                return false;

            s_faultDumped = true;
            IDataVault vault = s_vault;
            if (vault == null ||
                s_telemetryHandle.BufferID == 0u ||
                s_cursorHandle.BufferID == 0u ||
                !vault.TryResolveHandle(in s_telemetryHandle, out NativeArray<MathLodTelemetryEntry> telemetry) ||
                !vault.TryResolveHandle(in s_cursorHandle, out NativeArray<int> cursor) ||
                !telemetry.IsCreated ||
                !cursor.IsCreated ||
                cursor.Length <= 0)
            {
                return false;
            }

            return MathLodBlackBoxDumpWriter.TryDump(projectRoot, telemetry, cursor[0]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveActiveIterationBudget(float globalQualityWeight)
        {
            float curve = MathLodApproximation.SmoothStep01(MathLodApproximation.SaturateFinite(globalQualityWeight, 1f));
            return math.round(MinJacobiIterations + ((MaxJacobiIterations - MinJacobiIterations) * curve));
        }

        private static bool EnsureBuffer<T>(
            IDataVault vault,
            ref VaultGenerationHandle<T> handle,
            BufferID bufferId,
            int requiredLength,
            NativeArrayOptions options,
            out NativeArray<T> buffer,
            out bool created) where T : struct
        {
            created = false;
            buffer = default;
            if (vault == null || vault.IsAllocationLocked)
                return false;

            if (handle.BufferID != 0u &&
                vault.TryResolveHandle(in handle, out buffer) &&
                buffer.IsCreated &&
                buffer.Length >= requiredLength)
            {
                return true;
            }

            handle = vault.EnsureGenerationHandle<T>(bufferId, requiredLength, OwnerSystem, options);
            created = true;
            return vault.TryResolveHandle(in handle, out buffer) &&
                   buffer.IsCreated &&
                   buffer.Length >= requiredLength;
        }

        private static MathLodConfigDTO CreateDefaultConfig()
        {
            MathLodConfigDTO dto = default;
            dto.GlobalQualityWeight = 1f;
            dto.FractionalTimeSlice = 1f;
            dto.MinJacobiIterations = MinJacobiIterations;
            dto.MaxJacobiIterations = MaxJacobiIterations;
            dto.PadeResidualCeiling = MathLodApproximation.PadeReducedMaxAbsError;
            dto.BhaskaraResidualCeiling = MathLodApproximation.BhaskaraMaxAbsError;
            dto.MathLodPressure01 = 0f;
            dto.ActiveIterationBudget = MaxJacobiIterations;
            dto.Frame = 0u;
            dto.Flags = ConfigFlagVisualOverkill;
            dto.StateHash = HashConfig(dto);
            return dto;
        }

        private static void ResetHandles()
        {
            s_vault = null;
            s_configHandle = default;
            s_telemetryHandle = default;
            s_cursorHandle = default;
            s_faultDumped = false;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float Sanitize(float value, float fallback, ref bool sanitized)
        {
            bool finite = math.isfinite(value);
            sanitized |= !finite;
            return math.select(fallback, value, finite);
        }

        private static uint HashConfig(MathLodConfigDTO dto)
        {
            uint hash = 2166136261u;
            hash = Mix(hash, math.asuint(dto.GlobalQualityWeight));
            hash = Mix(hash, math.asuint(dto.FractionalTimeSlice));
            hash = Mix(hash, math.asuint(dto.MathLodPressure01));
            hash = Mix(hash, math.asuint(dto.ActiveIterationBudget));
            hash = Mix(hash, dto.Frame);
            hash = Mix(hash, dto.Flags);
            return hash == 0u ? 1u : hash;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static uint Mix(uint hash, uint value)
        {
            return (hash ^ value) * 16777619u;
        }

        private static unsafe void ClearNative<T>(NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated || array.Length <= 0)
                return;

            void* ptr = NativeArrayUnsafeUtility.GetUnsafePtr(array);
            UnsafeUtility.MemClear(ptr, (long)array.Length * UnsafeUtility.SizeOf<T>());
        }
    }

    [BurstCompile(CompileSynchronously = true, FloatMode = FloatMode.Deterministic, FloatPrecision = FloatPrecision.Standard)]
    public struct MathLodTortureJob : IJob
    {
        public NativeArray<MathLodTortureResult> Result;
        public NativeArray<MathLodTelemetryEntry> TelemetryRing;
        public NativeArray<int> TelemetryCursor;
        public float GlobalQualityWeight;
        public uint Frame;

        public void Execute()
        {
            float quality = MathLodApproximation.SaturateFinite(GlobalQualityWeight, 1f);
            MathLodTortureResult result = default;
            result.SampleCount = 16u;
            result.TelemetryEntryBytes = (uint)UnsafeUtility.SizeOf<MathLodTelemetryEntry>();
            result.MinOutput = float.MaxValue;
            result.MaxOutput = -float.MaxValue;
            result.GlobalQualityWeight = quality;
            result.LastFrame = Frame;

            int cursor = TelemetryCursor.IsCreated && TelemetryCursor.Length > 0 ? TelemetryCursor[0] : 0;
            for (int sample = 0; sample < 16; sample++)
            {
                float input = ResolveInput(sample);
                float temperature = ResolveTemperature(sample);
                float pressure = ResolvePressure(sample);
                float neg = MathLodApproximation.ApproxExpNegPade33Wide40(input);
                float pos = MathLodApproximation.ApproxExpPositivePade33Reduced(input);
                float blended = MathLodApproximation.BlendByQuality(neg, math.saturate(pos * 0.018315f), quality, 0.25f, 0.85f);
                float sine = MathLodApproximation.ApproxSinBhaskara(input);
                float cosine = MathLodApproximation.ApproxCosBhaskara(input);
                float tangent = MathLodApproximation.ApproxTanClamped(input);
                float atan = MathLodApproximation.ApproxAtanFast(input);
                float scaledTemperature = MathLodApproximation.FiniteOr(temperature * 0.000001f, 0f);
                float scaledPressure = MathLodApproximation.FiniteOr(pressure * 0.001f, 0f);
                float atan2 = MathLodApproximation.ApproxAtan2Fast(scaledTemperature, scaledPressure);
                float acosInput = math.clamp(scaledPressure - 1f, -1f, 1f);
                float acos = MathLodApproximation.ApproxAcosFast(acosInput);
                float pow = MathLodApproximation.ApproxPow01Curve(quality, math.abs(input));
                bool finite = math.isfinite(blended) &&
                              math.isfinite(neg) &&
                              math.isfinite(pos) &&
                              math.isfinite(sine) &&
                              math.isfinite(cosine) &&
                              math.isfinite(tangent) &&
                              math.isfinite(atan) &&
                              math.isfinite(atan2) &&
                              math.isfinite(acos) &&
                              math.isfinite(pow);
                result.NonFiniteCount += math.select(1u, 0u, finite);
                float safeBlended = MathLodApproximation.FiniteOr(blended, 0f);
                float safeNeg = MathLodApproximation.FiniteOr(neg, 0f);
                float safePos = MathLodApproximation.FiniteOr(pos, 0f);
                float safeSine = MathLodApproximation.FiniteOr(sine, 0f);
                float safeCosine = MathLodApproximation.FiniteOr(cosine, 0f);
                float safeTangent = MathLodApproximation.FiniteOr(tangent, 0f);
                float safeAtan = MathLodApproximation.FiniteOr(atan, 0f);
                float safeAtan2 = MathLodApproximation.FiniteOr(atan2, 0f);
                float safeAcos = MathLodApproximation.FiniteOr(acos, 0f);
                float safePow = MathLodApproximation.FiniteOr(pow, 0f);
                float maxAbsApprox = math.max(math.abs(safeBlended), math.abs(safeNeg));
                maxAbsApprox = math.max(maxAbsApprox, math.abs(safePos));
                maxAbsApprox = math.max(maxAbsApprox, math.abs(safeSine));
                maxAbsApprox = math.max(maxAbsApprox, math.abs(safeCosine));
                maxAbsApprox = math.max(maxAbsApprox, math.abs(safeTangent));
                maxAbsApprox = math.max(maxAbsApprox, math.abs(safeAtan));
                maxAbsApprox = math.max(maxAbsApprox, math.abs(safeAtan2));
                maxAbsApprox = math.max(maxAbsApprox, math.abs(safeAcos));
                maxAbsApprox = math.max(maxAbsApprox, math.abs(safePow));
                result.MaxAbsOutput = math.max(result.MaxAbsOutput, maxAbsApprox);
                float minApprox = math.min(safeBlended, safeNeg);
                minApprox = math.min(minApprox, safePos);
                minApprox = math.min(minApprox, safeSine);
                minApprox = math.min(minApprox, safeCosine);
                minApprox = math.min(minApprox, safeTangent);
                minApprox = math.min(minApprox, safeAtan);
                minApprox = math.min(minApprox, safeAtan2);
                minApprox = math.min(minApprox, safeAcos);
                minApprox = math.min(minApprox, safePow);
                float maxApprox = math.max(safeBlended, safeNeg);
                maxApprox = math.max(maxApprox, safePos);
                maxApprox = math.max(maxApprox, safeSine);
                maxApprox = math.max(maxApprox, safeCosine);
                maxApprox = math.max(maxApprox, safeTangent);
                maxApprox = math.max(maxApprox, safeAtan);
                maxApprox = math.max(maxApprox, safeAtan2);
                maxApprox = math.max(maxApprox, safeAcos);
                maxApprox = math.max(maxApprox, safePow);
                result.MinOutput = math.min(result.MinOutput, minApprox);
                result.MaxOutput = math.max(result.MaxOutput, maxApprox);
                result.WorstInput = math.select(result.WorstInput, input, !finite);
                result.WorstOutput = math.select(result.WorstOutput, maxAbsApprox, !finite);
                result.WorstTemperatureCelsius = math.select(result.WorstTemperatureCelsius, temperature, !finite);
                result.WorstPressureAtm = math.select(result.WorstPressureAtm, pressure, !finite);

                if (TelemetryRing.IsCreated && TelemetryRing.Length > 0)
                {
                    int slot = math.abs(cursor) % TelemetryRing.Length;
                    MathLodTelemetryEntry entry = default;
                    entry.StateHash = 14695981039346656037UL ^ (uint)sample;
                    entry.Frame = Frame;
                    entry.Flags = math.select(1u, 0u, finite);
                    entry.GlobalQualityWeight = quality;
                    entry.ActiveIterations = math.lerp(2f, 50f, MathLodApproximation.SmoothStep01(quality));
                    entry.ApproxInput = input;
                    entry.ApproxOutput = safeBlended;
                    entry.ResidualEstimate = 0f;
                    entry.MaxResidualEstimate = result.MaxResidualEstimate;
                    entry.TemperatureCelsius = temperature;
                    entry.PressureAtm = pressure;
                    entry.NonFiniteCount = result.NonFiniteCount;
                    entry.SampleIndex = (uint)sample;
                    TelemetryRing[slot] = entry;
                    cursor = (cursor + 1) % TelemetryRing.Length;
                }
            }

            result.Flags = math.select(1u, 0u, result.NonFiniteCount == 0u);
            result.LastCursor = (uint)math.max(0, cursor);
            if (TelemetryCursor.IsCreated && TelemetryCursor.Length > 0)
                TelemetryCursor[0] = cursor;
            if (Result.IsCreated && Result.Length > 0)
                Result[0] = result;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveInput(int sample)
        {
            switch (sample)
            {
                case 0: return 0f;
                case 1: return MathLodApproximation.Epsilon;
                case 2: return 0.147871399f;
                case 3: return 1f;
                case 4: return 4f;
                case 5: return 40f;
                case 6: return 1000f;
                case 7: return 1000000f;
                case 8: return -1000f;
                case 9: return float.NaN;
                case 10: return float.PositiveInfinity;
                case 11: return float.NegativeInfinity;
                case 12: return 0.000001f;
                case 13: return 0.25f;
                case 14: return 2f;
                default: return 8f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolveTemperature(int sample)
        {
            switch (sample & 3)
            {
                case 0: return -273.15f;
                case 1: return 37f;
                case 2: return 1000000f;
                default: return -1000000f;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float ResolvePressure(int sample)
        {
            switch (sample & 3)
            {
                case 0: return 0f;
                case 1: return 1f;
                case 2: return 1000f;
                default: return 1000000f;
            }
        }
    }

    public static class MathLodBlackBoxDumpWriter
    {
        public const string DumpRelativePath = "Docs/AgentLogs/Dump_SHINOBU_300_MathLOD.bin";
        public const uint DumpMagic = 0x4D4C4438u; // MLD8
        public const uint DumpVersion = 1u;
        public const int DumpHeaderBytes = 32;

        public static unsafe bool TryDump(string projectRoot, NativeArray<MathLodTelemetryEntry> telemetryRing, int cursor)
        {
            if (!telemetryRing.IsCreated || telemetryRing.Length <= 0)
                return false;

            int entrySize = UnsafeUtility.SizeOf<MathLodTelemetryEntry>();
            if (entrySize != MathLodApproximation.TelemetryEntrySizeBytes)
                return false;

            string root = string.IsNullOrWhiteSpace(projectRoot) ? Directory.GetCurrentDirectory() : projectRoot;
            string path = Path.Combine(root, DumpRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            using FileStream stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            Span<byte> header = stackalloc byte[DumpHeaderBytes];
            WriteUInt32LittleEndian(header.Slice(0, 4), DumpMagic);
            WriteUInt32LittleEndian(header.Slice(4, 4), DumpVersion);
            WriteUInt32LittleEndian(header.Slice(8, 4), (uint)telemetryRing.Length);
            WriteUInt32LittleEndian(header.Slice(12, 4), (uint)math.max(0, cursor));
            WriteUInt32LittleEndian(header.Slice(16, 4), (uint)entrySize);
            WriteUInt32LittleEndian(header.Slice(20, 4), (uint)(entrySize * telemetryRing.Length));
            WriteUInt32LittleEndian(header.Slice(24, 4), 0u);
            WriteUInt32LittleEndian(header.Slice(28, 4), 0u);
            stream.Write(header);

            byte* telemetryPtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(telemetryRing);
            stream.Write(new ReadOnlySpan<byte>(telemetryPtr, telemetryRing.Length * entrySize));
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void WriteUInt32LittleEndian(Span<byte> destination, uint value)
        {
            destination[0] = (byte)value;
            destination[1] = (byte)(value >> 8);
            destination[2] = (byte)(value >> 16);
            destination[3] = (byte)(value >> 24);
        }
    }
}
