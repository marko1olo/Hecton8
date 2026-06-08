using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public static class AnalyticalGerstnerWaveConstants
    {
        public const int MaxOctaves = 8;
        public const int PackedWaveLanes = 4;
        public const int SpectrumRows = MaxOctaves / PackedWaveLanes;
        public const int SampleCapacity = 50000;
        public const int MacroGridMaxResolution = 64;
        public const int MacroGridMaxCells = MacroGridMaxResolution * MacroGridMaxResolution;
        public const int TelemetryCapacity = 300;
        public const int ProfileCapacity = 32;
#if UNITY_EDITOR
        public const int CsvImportByteCapacity = 65536;
#endif
        public const int CounterCapacity = 4;

        public const int GerstnerWaveParamsBytes = 64;
        public const int TuningBytes = 128;
        public const int RequestBytes = 64;
        public const int ResultBytes = 64;
        public const int TelemetryBytes = 64;
        public const int ProfileBytes = 64;
        public const int CounterLaneBytes = 64;

        public const float TwoPi = 6.28318530718f;
        public const float InvTwoPi = 0.15915494309f;
        public const double PiDouble = 3.14159265358979323846d;
        public const double TwoPiDouble = 6.28318530717958647692d;
        public const double InvTwoPiDouble = 0.15915494309189533577d;
        public const float DefaultLargestWavelengthMeters = 128f;
        public const float DefaultMacroGridCellSizeMeters = 4f;
        public const float DefaultAmplitudeMultiplier = 0.04f;
        public const float DefaultSeaLevelY = 14.02f;
        public const float DefaultDumpThresholdMicros = 1500f;
#if UNITY_EDITOR
        public const string CsvRelativePath = "Data/Physics/ocean_wave_spectra.csv";
#endif

        public const uint FlagActive = 1u << 0;
        public const uint FlagMock = 1u << 1;
        public const uint FlagCoarseGrid = 1u << 2;
        public const uint FlagAnalytical = 1u << 3;
        public const uint FlagDearLie = 1u << 4;
        public const uint FlagStaleOrigin = 1u << 5;
        public const uint FlagNonFinite = 1u << 31;

        public const uint KernelHash = 0x53323633u; // S263

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static float ResolveSeaLevelY(float seaLevelY)
        {
            return math.isfinite(seaLevelY) &&
                   math.abs(seaLevelY) > 0.0001f &&
                   math.abs(seaLevelY) <= 1000f
                ? seaLevelY
                : DefaultSeaLevelY;
        }
    }

    public static class AnalyticalGerstnerWaveBufferIds
    {
        public const BufferID Spectrum = BufferID.Shinobu263WaveSpectrum;
        public const BufferID Tuning = BufferID.Shinobu263WaveTuning;
        public const BufferID Requests = BufferID.Shinobu263WaveRequests;
        public const BufferID Results = BufferID.Shinobu263WaveResults;
        public const BufferID MacroGrid = BufferID.Shinobu263WaveMacroGrid;
        public const BufferID TelemetryRing = BufferID.Shinobu263WaveTelemetryRing;
        public const BufferID TelemetryCursor = BufferID.Shinobu263WaveTelemetryCursor;
        public const BufferID Profiles = BufferID.Shinobu263WaveProfiles;
        public const BufferID Counters = BufferID.Shinobu263WaveCounters;
    }

    [StructLayout(LayoutKind.Explicit, Size = AnalyticalGerstnerWaveConstants.GerstnerWaveParamsBytes)]
    public struct GerstnerWaveParamsDTO
    {
        [FieldOffset(0)] public float4 Wave1;
        [FieldOffset(16)] public float4 Wave2;
        [FieldOffset(32)] public float4 Wave3;
        [FieldOffset(48)] public float4 Wave4;
    }

    [StructLayout(LayoutKind.Explicit, Size = AnalyticalGerstnerWaveConstants.TuningBytes)]
    public struct GerstnerWaveTuningDTO
    {
        [FieldOffset(0)] public double3 LocalOriginAUP;
        [FieldOffset(24)] public float SeaLevelY;
        [FieldOffset(28)] public float GlobalQualityWeight;
        [FieldOffset(32)] public int MaxOctaveLimit;
        [FieldOffset(36)] public int ActiveRequestCount;
        [FieldOffset(40)] public int MacroGridResolution;
        [FieldOffset(44)] public float MacroGridCellSizeMeters;
        [FieldOffset(48)] public float LargestWavelengthMeters;
        [FieldOffset(52)] public float WaveAmplitudeMultiplier;
        [FieldOffset(56)] public float CoarsePriorityThreshold;
        [FieldOffset(60)] public uint FrameIndex;
        [FieldOffset(64)] public float TimeSeconds;
        [FieldOffset(68)] public float MaxSolverMicrosBeforeDump;
        [FieldOffset(72)] public int TotalOctaves;
        [FieldOffset(76)] public int ActiveOctaves;
        [FieldOffset(80)] public float MacroGridOriginX;
        [FieldOffset(84)] public float MacroGridOriginZ;
        [FieldOffset(88)] public float DearLieWeight;
        [FieldOffset(92)] public uint Flags;
        [FieldOffset(96)] public float StormWeight01;
        [FieldOffset(100)] public float WindDirectionRadians;
        [FieldOffset(104)] public float WindSpeedMetersPerSecond;
        [FieldOffset(108)] public uint ProfileHash;
        [FieldOffset(112)] public uint OriginShiftSequence;
        [FieldOffset(116)] public uint OriginShiftFlags;
        [FieldOffset(120)] public double PhaseTimeSeconds;

        public static GerstnerWaveTuningDTO Default()
        {
            GerstnerWaveTuningDTO value = default;
            value.LocalOriginAUP = double3.zero;
            value.SeaLevelY = AnalyticalGerstnerWaveConstants.DefaultSeaLevelY;
            value.GlobalQualityWeight = 1f;
            value.MaxOctaveLimit = AnalyticalGerstnerWaveConstants.MaxOctaves;
            value.ActiveRequestCount = 0;
            value.MacroGridResolution = 32;
            value.MacroGridCellSizeMeters = AnalyticalGerstnerWaveConstants.DefaultMacroGridCellSizeMeters;
            value.LargestWavelengthMeters = AnalyticalGerstnerWaveConstants.DefaultLargestWavelengthMeters;
            value.WaveAmplitudeMultiplier = AnalyticalGerstnerWaveConstants.DefaultAmplitudeMultiplier;
            value.CoarsePriorityThreshold = 64f;
            value.MaxSolverMicrosBeforeDump = AnalyticalGerstnerWaveConstants.DefaultDumpThresholdMicros;
            value.TotalOctaves = AnalyticalGerstnerWaveConstants.MaxOctaves;
            value.ActiveOctaves = AnalyticalGerstnerWaveConstants.MaxOctaves;
            value.DearLieWeight = 1f;
            value.Flags = AnalyticalGerstnerWaveConstants.FlagActive | AnalyticalGerstnerWaveConstants.FlagDearLie;
            value.StormWeight01 = 0.35f;
            value.WindDirectionRadians = 0.62f;
            value.WindSpeedMetersPerSecond = 10f;
            return value;
        }
    }

    [StructLayout(LayoutKind.Explicit, Size = AnalyticalGerstnerWaveConstants.RequestBytes)]
    public struct OceanSampleRequestDTO
    {
        [FieldOffset(0)] public double3 SampleAUP;
        [FieldOffset(24)] public uint EntityHashID;
        [FieldOffset(28)] public byte Priority;
        [FieldOffset(29)] public byte Flags;
        [FieldOffset(30)] public ushort _pad0;
        [FieldOffset(32)] public float MinSpatialLengthMeters;
        [FieldOffset(36)] public float RadiusMeters;
        [FieldOffset(40)] public uint ShiftFrameID;
        [FieldOffset(44)] public uint RequestFrame;
        [FieldOffset(48)] public ulong _pad1;
        [FieldOffset(56)] public ulong _pad2;
    }

    [StructLayout(LayoutKind.Explicit, Size = AnalyticalGerstnerWaveConstants.ResultBytes)]
    public struct OceanSampleResultDTO
    {
        [FieldOffset(0)] public double3 SampleAUP;
        [FieldOffset(24)] public float WaterHeight;
        [FieldOffset(28)] public float3 SurfaceNormal;
        [FieldOffset(40)] public float3 Displacement;
        [FieldOffset(52)] public uint EntityHashID;
        [FieldOffset(56)] public uint Flags;
        [FieldOffset(60)] public uint OriginShiftSequence;
    }

    [StructLayout(LayoutKind.Explicit, Size = AnalyticalGerstnerWaveConstants.TelemetryBytes)]
    public struct WaveMathTelemetryEntry
    {
        [FieldOffset(0)] public uint FrameIndex;
        [FieldOffset(4)] public int EvaluatedCoordinates;
        [FieldOffset(8)] public int ActiveOctaves;
        [FieldOffset(12)] public int CoarseGridSamples;
        [FieldOffset(16)] public float BurstMicros;
        [FieldOffset(20)] public float GlobalQualityWeight;
        [FieldOffset(24)] public uint Flags;
        [FieldOffset(28)] public int NonFiniteCount;
        [FieldOffset(32)] public uint LastEntityHashID;
        [FieldOffset(36)] public float MaxAbsHeight;
        [FieldOffset(40)] public int MacroGridResolution;
        [FieldOffset(44)] public int RequestCount;
        [FieldOffset(48)] public uint KernelHash;
        [FieldOffset(52)] public uint ProfileHash;
        [FieldOffset(56)] public uint OriginShiftSequence;
        [FieldOffset(60)] public uint _pad0;
    }

    [StructLayout(LayoutKind.Explicit, Size = AnalyticalGerstnerWaveConstants.ProfileBytes)]
    public struct WaveSpectrumProfileDTO
    {
        [FieldOffset(0)] public uint StateHash;
        [FieldOffset(4)] public float MinSteepness;
        [FieldOffset(8)] public float MaxSteepness;
        [FieldOffset(12)] public float MinWavelength;
        [FieldOffset(16)] public float MaxWavelength;
        [FieldOffset(20)] public float MinAmplitudeMultiplier;
        [FieldOffset(24)] public float MaxAmplitudeMultiplier;
        [FieldOffset(28)] public float MinSpeed;
        [FieldOffset(32)] public float MaxSpeed;
        [FieldOffset(36)] public float WindDirectionRadians;
        [FieldOffset(40)] public float StormWeight01;
        [FieldOffset(44)] public uint Flags;
        [FieldOffset(48)] public ulong _pad0;
        [FieldOffset(56)] public ulong _pad1;
    }

    [StructLayout(LayoutKind.Explicit, Size = AnalyticalGerstnerWaveConstants.CounterLaneBytes)]
    public struct WaveMathCounterLane
    {
        [FieldOffset(0)] public int Value;
        [FieldOffset(4)] public int _pad0;
        [FieldOffset(8)] public ulong _pad1;
        [FieldOffset(16)] public ulong _pad2;
        [FieldOffset(24)] public ulong _pad3;
        [FieldOffset(32)] public ulong _pad4;
        [FieldOffset(40)] public ulong _pad5;
        [FieldOffset(48)] public ulong _pad6;
        [FieldOffset(56)] public ulong _pad7;
    }

    public static class AnalyticalGerstnerWaveLayout
    {
        private static readonly bool s_validateOnce = ValidateInternal();

        public static bool Validate()
        {
            return s_validateOnce;
        }

        public static int OffsetOf<T>(string fieldName) where T : struct
        {
            return Marshal.OffsetOf<T>(fieldName).ToInt32();
        }

        private static bool ValidateInternal()
        {
            return UnsafeUtility.SizeOf<GerstnerWaveParamsDTO>() == AnalyticalGerstnerWaveConstants.GerstnerWaveParamsBytes &&
                   UnsafeUtility.SizeOf<GerstnerWaveTuningDTO>() == AnalyticalGerstnerWaveConstants.TuningBytes &&
                   UnsafeUtility.SizeOf<OceanSampleRequestDTO>() == AnalyticalGerstnerWaveConstants.RequestBytes &&
                   UnsafeUtility.SizeOf<OceanSampleResultDTO>() == AnalyticalGerstnerWaveConstants.ResultBytes &&
                   UnsafeUtility.SizeOf<WaveMathTelemetryEntry>() == AnalyticalGerstnerWaveConstants.TelemetryBytes &&
                   UnsafeUtility.SizeOf<WaveSpectrumProfileDTO>() == AnalyticalGerstnerWaveConstants.ProfileBytes &&
                   UnsafeUtility.SizeOf<WaveMathCounterLane>() == AnalyticalGerstnerWaveConstants.CounterLaneBytes &&
                   UnsafeUtility.SizeOf<float4>() == 16 &&
                   UnsafeUtility.AlignOf<GerstnerWaveParamsDTO>() > 0 &&
                   OffsetOf<GerstnerWaveParamsDTO>(nameof(GerstnerWaveParamsDTO.Wave1)) == 0 &&
                   OffsetOf<GerstnerWaveParamsDTO>(nameof(GerstnerWaveParamsDTO.Wave2)) == 16 &&
                   OffsetOf<GerstnerWaveParamsDTO>(nameof(GerstnerWaveParamsDTO.Wave3)) == 32 &&
                   OffsetOf<GerstnerWaveParamsDTO>(nameof(GerstnerWaveParamsDTO.Wave4)) == 48 &&
                   (OffsetOf<GerstnerWaveParamsDTO>(nameof(GerstnerWaveParamsDTO.Wave1)) & 15) == 0 &&
                   (OffsetOf<GerstnerWaveParamsDTO>(nameof(GerstnerWaveParamsDTO.Wave2)) & 15) == 0 &&
                   (OffsetOf<GerstnerWaveParamsDTO>(nameof(GerstnerWaveParamsDTO.Wave3)) & 15) == 0 &&
                   (OffsetOf<GerstnerWaveParamsDTO>(nameof(GerstnerWaveParamsDTO.Wave4)) & 15) == 0 &&
                   OffsetOf<GerstnerWaveTuningDTO>(nameof(GerstnerWaveTuningDTO.LocalOriginAUP)) == 0 &&
                   OffsetOf<GerstnerWaveTuningDTO>(nameof(GerstnerWaveTuningDTO.OriginShiftSequence)) == 112 &&
                   OffsetOf<GerstnerWaveTuningDTO>(nameof(GerstnerWaveTuningDTO.PhaseTimeSeconds)) == 120 &&
                   OffsetOf<OceanSampleRequestDTO>(nameof(OceanSampleRequestDTO.SampleAUP)) == 0 &&
                   OffsetOf<OceanSampleRequestDTO>(nameof(OceanSampleRequestDTO.ShiftFrameID)) == 40 &&
                   OffsetOf<OceanSampleResultDTO>(nameof(OceanSampleResultDTO.WaterHeight)) == 24 &&
                   OffsetOf<OceanSampleResultDTO>(nameof(OceanSampleResultDTO.OriginShiftSequence)) == 60 &&
                   OffsetOf<WaveMathTelemetryEntry>(nameof(WaveMathTelemetryEntry.OriginShiftSequence)) == 56 &&
                   OffsetOf<WaveMathTelemetryEntry>(nameof(WaveMathTelemetryEntry._pad0)) == 60 &&
                   OffsetOf<WaveMathCounterLane>(nameof(WaveMathCounterLane.Value)) == 0 &&
                   OffsetOf<WaveMathCounterLane>(nameof(WaveMathCounterLane._pad7)) == 56;
        }
    }

    #if UNITY_EDITOR
    public static class WaveSpectrumProfileCsvParser
    {
        private const byte Comma = (byte)',';
        private const byte CarriageReturn = (byte)'\r';
        private const byte LineFeed = (byte)'\n';
        private const byte Hash = (byte)'#';
        private const byte Space = (byte)' ';
        private const byte Tab = (byte)'\t';

        public static bool TryApply(ReadOnlySpan<byte> csv, NativeArray<WaveSpectrumProfileDTO> output, out int rowsWritten)
        {
            rowsWritten = 0;
            if (csv.Length <= 0 || !output.IsCreated || output.Length <= 0)
                return false;

            for (int i = 0; i < output.Length; i++)
                output[i] = default;

            int cursor = 0;
            while (cursor < csv.Length && rowsWritten < output.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != LineFeed)
                    cursor++;

                int lineEnd = cursor;
                if (cursor < csv.Length)
                    cursor++;
                if (lineEnd > lineStart && csv[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryParseLine(csv.Slice(lineStart, lineEnd - lineStart), out WaveSpectrumProfileDTO row))
                    output[rowsWritten++] = row;
            }

            return rowsWritten > 0;
        }

        public static bool TryApply(ReadOnlySpan<byte> csv, Span<WaveSpectrumProfileDTO> output, out int rowsWritten)
        {
            rowsWritten = 0;
            if (csv.Length <= 0 || output.Length <= 0)
                return false;

            for (int i = 0; i < output.Length; i++)
                output[i] = default;

            int cursor = 0;
            while (cursor < csv.Length && rowsWritten < output.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != LineFeed)
                    cursor++;

                int lineEnd = cursor;
                if (cursor < csv.Length)
                    cursor++;
                if (lineEnd > lineStart && csv[lineEnd - 1] == CarriageReturn)
                    lineEnd--;

                if (TryParseLine(csv.Slice(lineStart, lineEnd - lineStart), out WaveSpectrumProfileDTO row))
                    output[rowsWritten++] = row;
            }

            return rowsWritten > 0;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out WaveSpectrumProfileDTO row)
        {
            row = default;
            line = Trim(line);
            if (line.Length <= 0 || line[0] == Hash)
                return false;

            int c0 = IndexOf(line, Comma, 0);
            if (c0 <= 0)
                return false;

            ReadOnlySpan<byte> state = Trim(line.Slice(0, c0));
            if (EqualsAscii(state, "state") || EqualsAscii(state, "name"))
                return false;

            int cursor = c0 + 1;
            if (!ReadFloat(line, ref cursor, out row.MinSteepness) ||
                !ReadFloat(line, ref cursor, out row.MaxSteepness) ||
                !ReadFloat(line, ref cursor, out row.MinWavelength) ||
                !ReadFloat(line, ref cursor, out row.MaxWavelength) ||
                !ReadFloat(line, ref cursor, out row.MinAmplitudeMultiplier) ||
                !ReadFloat(line, ref cursor, out row.MaxAmplitudeMultiplier) ||
                !ReadFloat(line, ref cursor, out row.MinSpeed) ||
                !ReadFloat(line, ref cursor, out row.MaxSpeed))
            {
                return false;
            }

            float windRadians = 0f;
            float storm = 0.5f;
            ReadFloat(line, ref cursor, out windRadians);
            ReadFloat(line, ref cursor, out storm);

            row.StateHash = Fnv1A32(state);
            row.MinSteepness = math.max(0f, row.MinSteepness);
            row.MaxSteepness = math.max(row.MinSteepness, row.MaxSteepness);
            row.MinWavelength = math.max(0.01f, row.MinWavelength);
            row.MaxWavelength = math.max(row.MinWavelength, row.MaxWavelength);
            row.MinAmplitudeMultiplier = math.max(0f, row.MinAmplitudeMultiplier);
            row.MaxAmplitudeMultiplier = math.max(row.MinAmplitudeMultiplier, row.MaxAmplitudeMultiplier);
            row.MinSpeed = math.max(0.01f, row.MinSpeed);
            row.MaxSpeed = math.max(row.MinSpeed, row.MaxSpeed);
            row.WindDirectionRadians = math.select(0f, windRadians, math.isfinite(windRadians));
            row.StormWeight01 = math.saturate(math.select(0.5f, storm, math.isfinite(storm)));
            row.Flags = AnalyticalGerstnerWaveConstants.FlagActive;
            return row.StateHash != 0u;
        }

        private static bool ReadFloat(ReadOnlySpan<byte> line, ref int cursor, out float value)
        {
            value = 0f;
            if (cursor > line.Length)
                return false;

            int comma = IndexOf(line, Comma, cursor);
            ReadOnlySpan<byte> span = comma >= 0 ? line.Slice(cursor, comma - cursor) : line.Slice(cursor);
            cursor = comma >= 0 ? comma + 1 : line.Length + 1;
            return TryParseFloat(Trim(span), out value);
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && IsWhitespace(value[start]))
                start++;
            while (end >= start && IsWhitespace(value[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : value.Slice(start, end - start + 1);
        }

        private static int IndexOf(ReadOnlySpan<byte> value, byte target, int start)
        {
            for (int i = math.max(0, start); i < value.Length; i++)
            {
                if (value[i] == target)
                    return i;
            }

            return -1;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            if (span.Length <= 0)
                return false;

            int index = 0;
            float sign = 1f;
            if (span[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }
            else if (span[index] == (byte)'+')
            {
                index++;
            }

            float integer = 0f;
            bool hasDigit = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                hasDigit = true;
                integer = integer * 10f + span[index] - (byte)'0';
                index++;
            }

            float fraction = 0f;
            float divisor = 1f;
            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    hasDigit = true;
                    fraction = fraction * 10f + span[index] - (byte)'0';
                    divisor *= 10f;
                    index++;
                }
            }

            if (index != span.Length)
                return false;

            value = sign * (integer + fraction * math.rcp(math.max(1f, divisor)));
            return hasDigit && math.isfinite(value);
        }

        private static uint Fnv1A32(ReadOnlySpan<byte> span)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < span.Length; i++)
            {
                byte c = span[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);
                hash ^= c;
                hash *= 16777619u;
            }

            return math.select(1u, hash, hash != 0u);
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> span, string text)
        {
            if (span.Length != text.Length)
                return false;

            for (int i = 0; i < span.Length; i++)
            {
                byte a = span[i];
                if (a >= (byte)'A' && a <= (byte)'Z')
                    a = (byte)(a + 32);
                if (a != (byte)text[i])
                    return false;
            }

            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsWhitespace(byte value)
        {
            return value == Space || value == Tab || value == CarriageReturn || value == LineFeed;
        }
    }
    #endif
}
