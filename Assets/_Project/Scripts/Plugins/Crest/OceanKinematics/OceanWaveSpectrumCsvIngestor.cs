using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    /// <summary>
    /// Cold-boot, allocation-free CSV parser for unmanaged Gerstner wave spectra.
    /// Expected columns: state,dirX,dirZ,amplitude,steepness,frequency,wavelength,phase.
    /// </summary>
    public static class OceanWaveSpectrumCsvIngestor
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static bool TryParse(ReadOnlySpan<byte> csvBytes, NativeArray<GerstnerWaveDTO> waves, out int waveCount)
        {
            waveCount = 0;
            if (csvBytes.Length == 0 || !waves.IsCreated || waves.Length == 0)
                return false;

            int index = 0;
            while (index < csvBytes.Length && waveCount < waves.Length)
            {
                ReadLine(csvBytes, ref index, out int lineStart, out int lineLength);
                if (lineLength <= 0)
                    continue;

                ReadOnlySpan<byte> line = csvBytes.Slice(lineStart, lineLength);
                if (IsCommentOrHeader(line))
                    continue;

                if (!TryParseLine(line, waveCount, out GerstnerWaveDTO wave))
                    continue;

                waves[waveCount] = wave;
                waveCount++;
            }

            return waveCount > 0;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, int rowIndex, out GerstnerWaveDTO wave)
        {
            wave = default;
            int cursor = 0;
            uint stateHash = ParseStateHash(line, ref cursor);
            if (stateHash == 0u)
                return false;

            float dirX = ParseFloat(line, ref cursor, ResolveFallbackDirection(rowIndex).x);
            float dirZ = ParseFloat(line, ref cursor, ResolveFallbackDirection(rowIndex).y);
            float amplitude = ParseFloat(line, ref cursor, 0f);
            float steepness = ParseFloat(line, ref cursor, 0f);
            float frequency = ParseFloat(line, ref cursor, 0.0001f);
            float wavelength = ParseFloat(line, ref cursor, 1f);
            float phase = ParseFloat(line, ref cursor, 0f);

            float2 direction = new float2(dirX, dirZ);
            float lenSq = math.lengthsq(direction);
            if (lenSq <= 0.000001f || !math.isfinite(lenSq))
                direction = ResolveFallbackDirection(rowIndex);
            else
                direction *= math.rsqrt(math.max(lenSq, 0.0001f));

            wave.DirectionXZ = direction;
            wave.Amplitude = math.max(0f, SanitizeFinite(amplitude, 0f));
            wave.Steepness = math.saturate(SanitizeFinite(steepness, 0f));
            wave.Frequency = math.max(0.0001f, SanitizeFinite(frequency, 0.0001f));
            wave.PhaseOffset = SanitizeFinite(phase, 0f);
            wave.Wavelength = math.max(0.001f, SanitizeFinite(wavelength, 1f));
            wave.StateHash = stateHash;
            wave.Flags = OceanKinematicsConstants.FlagActive;
            return true;
        }

        private static void ReadLine(ReadOnlySpan<byte> bytes, ref int index, out int start, out int length)
        {
            start = index;
            while (index < bytes.Length && bytes[index] != (byte)'\n' && bytes[index] != (byte)'\r')
                index++;

            length = index - start;
            while (index < bytes.Length && (bytes[index] == (byte)'\n' || bytes[index] == (byte)'\r'))
                index++;
        }

        private static bool IsCommentOrHeader(ReadOnlySpan<byte> line)
        {
            int index = 0;
            SkipWhitespace(line, ref index);
            if (index >= line.Length)
                return true;

            return line[index] == (byte)'#' || IsHeaderToken(line, index);
        }

        private static bool IsHeaderToken(ReadOnlySpan<byte> line, int index)
        {
            return index + 5 <= line.Length &&
                   ToLower(line[index]) == (byte)'s' &&
                   ToLower(line[index + 1]) == (byte)'t' &&
                   ToLower(line[index + 2]) == (byte)'a' &&
                   ToLower(line[index + 3]) == (byte)'t' &&
                   ToLower(line[index + 4]) == (byte)'e' &&
                   (index + 5 == line.Length || line[index + 5] == (byte)',' || line[index + 5] <= (byte)' ');
        }

        private static uint ParseStateHash(ReadOnlySpan<byte> line, ref int cursor)
        {
            uint hash = FnvOffset;
            bool consumed = false;
            SkipWhitespace(line, ref cursor);
            while (cursor < line.Length && line[cursor] != (byte)',')
            {
                byte value = ToLower(line[cursor]);
                if (value > (byte)' ')
                {
                    hash ^= value;
                    hash *= FnvPrime;
                    consumed = true;
                }

                cursor++;
            }

            SkipDelimiter(line, ref cursor);
            return consumed ? math.select(1u, hash, hash != 0u) : 0u;
        }

        private static float ParseFloat(ReadOnlySpan<byte> line, ref int cursor, float fallback)
        {
            SkipWhitespace(line, ref cursor);
            int sign = 1;
            if (cursor < line.Length && line[cursor] == (byte)'-')
            {
                sign = -1;
                cursor++;
            }
            else if (cursor < line.Length && line[cursor] == (byte)'+')
            {
                cursor++;
            }

            float value = 0f;
            bool consumed = false;
            while (cursor < line.Length && IsDigit(line[cursor]))
            {
                value = value * 10f + (line[cursor] - (byte)'0');
                cursor++;
                consumed = true;
            }

            if (cursor < line.Length && line[cursor] == (byte)'.')
            {
                cursor++;
                float place = 0.1f;
                while (cursor < line.Length && IsDigit(line[cursor]))
                {
                    value += (line[cursor] - (byte)'0') * place;
                    place *= 0.1f;
                    cursor++;
                    consumed = true;
                }
            }

            if (cursor < line.Length && (line[cursor] == (byte)'e' || line[cursor] == (byte)'E'))
                value *= ParseExponentScale(line, ref cursor);

            while (cursor < line.Length && line[cursor] != (byte)',')
                cursor++;

            SkipDelimiter(line, ref cursor);
            return consumed ? value * sign : fallback;
        }

        private static float ParseExponentScale(ReadOnlySpan<byte> line, ref int cursor)
        {
            cursor++;
            int sign = 1;
            if (cursor < line.Length && line[cursor] == (byte)'-')
            {
                sign = -1;
                cursor++;
            }
            else if (cursor < line.Length && line[cursor] == (byte)'+')
            {
                cursor++;
            }

            int exponent = 0;
            while (cursor < line.Length && IsDigit(line[cursor]))
            {
                exponent = exponent * 10 + (line[cursor] - (byte)'0');
                cursor++;
            }

            exponent = math.clamp(exponent, 0, 12);
            float scale = 1f;
            for (int i = 0; i < exponent; i++)
                scale *= 10f;

            return sign < 0 ? math.rcp(scale) : scale;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SkipDelimiter(ReadOnlySpan<byte> line, ref int cursor)
        {
            if (cursor < line.Length && line[cursor] == (byte)',')
                cursor++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void SkipWhitespace(ReadOnlySpan<byte> line, ref int cursor)
        {
            while (cursor < line.Length && line[cursor] <= (byte)' ')
                cursor++;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsDigit(byte value)
        {
            return value >= (byte)'0' && value <= (byte)'9';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static byte ToLower(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float SanitizeFinite(float value, float fallback)
        {
            return math.select(fallback, value, math.isfinite(value));
        }

        private static float2 ResolveFallbackDirection(int rowIndex)
        {
            switch (rowIndex & 3)
            {
                case 0: return new float2(1f, 0f);
                case 1: return new float2(0.70710677f, 0.70710677f);
                case 2: return new float2(-0.3826834f, 0.9238795f);
                default: return new float2(-0.8660254f, -0.5f);
            }
        }
    }
}
