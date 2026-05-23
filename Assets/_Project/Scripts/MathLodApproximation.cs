using System;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
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
        public const float Epsilon = 0.0001f;

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
            float4 safe = math.min(math.max(new float4(0f), math.select(new float4(0f), value, math.isfinite(value))), new float4(4f));
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
            float safe = math.clamp(FiniteOr(value, 0f), 0f, 40f);
            float segmentDecay = ApproxExpNegPade33Reduced(safe * 0.1f);
            float decay2 = segmentDecay * segmentDecay;
            float decay4 = decay2 * decay2;
            float decay8 = decay4 * decay4;
            float decay10 = decay8 * decay2;
            return math.saturate(math.select(0f, decay10, math.isfinite(decay10)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ApproxExpPositivePade33Reduced(float value)
        {
            float safe = math.clamp(FiniteOr(value, 0f), 0f, 4f);
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
        public static bool ValidateLayouts()
        {
            return UnsafeUtility.SizeOf<MathLodTelemetryEntry>() == TelemetryEntrySizeBytes &&
                   UnsafeUtility.SizeOf<MathLodTortureResult>() == TortureResultSizeBytes;
        }
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
        [FieldOffset(56)] public uint _pad0;
        [FieldOffset(60)] public uint _pad1;
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
                bool finite = math.isfinite(blended) && math.isfinite(neg) && math.isfinite(pos);
                result.NonFiniteCount += finite ? 0u : 1u;
                result.MaxAbsOutput = math.max(result.MaxAbsOutput, math.abs(blended));
                result.MinOutput = math.min(result.MinOutput, blended);
                result.MaxOutput = math.max(result.MaxOutput, blended);
                result.WorstInput = math.select(result.WorstInput, input, !finite);
                result.WorstTemperatureCelsius = math.select(result.WorstTemperatureCelsius, temperature, !finite);
                result.WorstPressureAtm = math.select(result.WorstPressureAtm, pressure, !finite);

                if (TelemetryRing.IsCreated && TelemetryRing.Length > 0)
                {
                    int slot = math.abs(cursor) % TelemetryRing.Length;
                    MathLodTelemetryEntry entry = default;
                    entry.StateHash = 14695981039346656037UL ^ (uint)sample;
                    entry.Frame = Frame;
                    entry.Flags = finite ? 0u : 1u;
                    entry.GlobalQualityWeight = quality;
                    entry.ActiveIterations = math.lerp(2f, 50f, MathLodApproximation.SmoothStep01(quality));
                    entry.ApproxInput = input;
                    entry.ApproxOutput = blended;
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

            result.Flags = result.NonFiniteCount == 0u ? 0u : 1u;
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
