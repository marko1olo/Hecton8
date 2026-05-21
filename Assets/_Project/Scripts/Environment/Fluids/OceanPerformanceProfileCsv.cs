using System;
using Unity.Collections;

namespace Hecton8.Environment.Fluids
{
    public static class OceanPerformanceProfileCsv
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static int Parse(ReadOnlySpan<byte> csv, NativeArray<OceanPerformanceProfileDTO> profiles)
        {
            if (!profiles.IsCreated || profiles.Length == 0 || csv.Length == 0)
                return 0;

            int cursor = 0;
            int written = 0;
            bool firstLine = true;
            while (cursor < csv.Length && written < profiles.Length)
            {
                int lineStart = cursor;
                while (cursor < csv.Length && csv[cursor] != (byte)'\n')
                    cursor++;

                ReadOnlySpan<byte> line = Trim(csv.Slice(lineStart, cursor - lineStart));
                if (cursor < csv.Length && csv[cursor] == (byte)'\n')
                    cursor++;

                if (line.Length == 0)
                    continue;

                if (firstLine && StartsWithProfileHeader(line))
                {
                    firstLine = false;
                    continue;
                }

                firstLine = false;
                if (TryParseLine(line, out OceanPerformanceProfileDTO profile))
                {
                    profiles[written] = profile;
                    written++;
                }
            }

            return written;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> line, out OceanPerformanceProfileDTO profile)
        {
            profile = default;
            ReadOnlySpan<byte> name = NextField(line, 0, out int next);
            ReadOnlySpan<byte> maxQueries = NextField(line, next, out next);
            ReadOnlySpan<byte> timeout = NextField(line, next, out next);
            ReadOnlySpan<byte> aggression = NextField(line, next, out next);
            ReadOnlySpan<byte> ampMin = NextField(line, next, out next);
            ReadOnlySpan<byte> ampMax = NextField(line, next, out _);

            if (name.Length == 0)
                return false;

            profile.ProfileHash = HashFnv1a(name);
            profile.MaxConcurrentQueries = ParseUInt(maxQueries, 1024u);
            profile.ReadbackTimeoutMilliseconds = ParseFloat(timeout, 1.5f);
            profile.QualityAggression = ParseFloat(aggression, 1f);
            profile.MockAmplitudeMin = ParseFloat(ampMin, 0.025f);
            profile.MockAmplitudeMax = ParseFloat(ampMax, 1.25f);
            profile.Flags = 1u;
            return true;
        }

        private static ReadOnlySpan<byte> NextField(ReadOnlySpan<byte> line, int start, out int next)
        {
            if (start >= line.Length)
            {
                next = line.Length;
                return ReadOnlySpan<byte>.Empty;
            }

            int end = start;
            while (end < line.Length && line[end] != (byte)',')
                end++;

            next = end < line.Length ? end + 1 : end;
            return Trim(line.Slice(start, end - start));
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start < span.Length && IsWhitespace(span[start]))
                start++;
            while (end >= start && IsWhitespace(span[end]))
                end--;
            return start > end ? ReadOnlySpan<byte>.Empty : span.Slice(start, end - start + 1);
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t' || value == (byte)'\r';
        }

        private static bool StartsWithProfileHeader(ReadOnlySpan<byte> span)
        {
            const int profileLength = 7;
            if (span.Length < profileLength)
                return false;

            return ToLowerAscii(span[0]) == (byte)'p' &&
                   ToLowerAscii(span[1]) == (byte)'r' &&
                   ToLowerAscii(span[2]) == (byte)'o' &&
                   ToLowerAscii(span[3]) == (byte)'f' &&
                   ToLowerAscii(span[4]) == (byte)'i' &&
                   ToLowerAscii(span[5]) == (byte)'l' &&
                   ToLowerAscii(span[6]) == (byte)'e';
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z'
                ? (byte)(value + 32)
                : value;
        }

        private static uint HashFnv1a(ReadOnlySpan<byte> span)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < span.Length; i++)
            {
                hash ^= span[i];
                hash *= FnvPrime;
            }

            return hash == 0u ? 1u : hash;
        }

        private static uint ParseUInt(ReadOnlySpan<byte> span, uint fallback)
        {
            uint value = 0u;
            bool any = false;
            for (int i = 0; i < span.Length; i++)
            {
                byte c = span[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return any ? value : fallback;
                value = value * 10u + (uint)(c - (byte)'0');
                any = true;
            }

            return any ? value : fallback;
        }

        private static float ParseFloat(ReadOnlySpan<byte> span, float fallback)
        {
            if (span.Length == 0)
                return fallback;

            int index = 0;
            float sign = 1f;
            if (span[index] == (byte)'-')
            {
                sign = -1f;
                index++;
            }

            float value = 0f;
            bool any = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                value = value * 10f + (span[index] - (byte)'0');
                index++;
                any = true;
            }

            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                float scale = 0.1f;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    value += (span[index] - (byte)'0') * scale;
                    scale *= 0.1f;
                    index++;
                    any = true;
                }
            }

            return any ? value * sign : fallback;
        }
    }
}
