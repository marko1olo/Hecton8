using System;
using Hecton8.Core.Contracts.Physics;
using Unity.Mathematics;

namespace Hecton8.Physics
{
    public static class SeaglideVehicleProfileCsv
    {
        private const byte Comma = (byte)',';
        private const byte CarriageReturn = (byte)'\r';
        private const byte LineFeed = (byte)'\n';
        private const byte Hash = (byte)'#';

        public static bool TryApplyFirstProfile(ReadOnlySpan<byte> bytes, ref SeaglideTuningDTO tuning)
        {
            int cursor = 0;
            while (TryReadLine(bytes, ref cursor, out ReadOnlySpan<byte> line))
            {
                line = Trim(line);
                if (line.Length == 0 || line[0] == Hash || ContainsAscii(line, "profile"))
                    continue;

                uint profileHash = ReadNameHash(ref line);
                if (!TryReadFloat(ref line, out float maxThrust) ||
                    !TryReadFloat(ref line, out float dragCoefficient) ||
                    !TryReadFloat(ref line, out float currentResistance))
                {
                    continue;
                }

                tuning.ProfileHash = profileHash != 0u ? profileHash : SeaglideHydrodynamicsConstants.SourceHash;
                tuning.MaxThrustN = math.max(1f, maxThrust);
                tuning.QuadraticDragCoefficient = math.max(0f, dragCoefficient);
                tuning.FlowForceCoefficient = math.max(0f, currentResistance);
                return true;
            }

            return false;
        }

        public static uint HashLowerAscii(ReadOnlySpan<byte> text)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                byte c = text[i];
                if (c >= (byte)'A' && c <= (byte)'Z')
                    c = (byte)(c + 32);

                hash ^= c;
                hash *= 16777619u;
            }

            return hash != 0u ? hash : SeaglideHydrodynamicsConstants.SourceHash;
        }

        private static uint ReadNameHash(ref ReadOnlySpan<byte> line)
        {
            ReadOnlySpan<byte> token = ReadToken(ref line);
            return HashLowerAscii(Trim(token));
        }

        private static bool TryReadFloat(ref ReadOnlySpan<byte> line, out float value)
        {
            ReadOnlySpan<byte> token = Trim(ReadToken(ref line));
            value = 0f;
            if (token.Length == 0)
                return false;

            int index = 0;
            bool negative = false;
            if (token[index] == (byte)'-')
            {
                negative = true;
                index++;
            }

            double result = 0d;
            bool hasDigits = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                result = (result * 10d) + (token[index] - (byte)'0');
                hasDigits = true;
                index++;
            }

            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                double scale = 0.1d;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    result += (token[index] - (byte)'0') * scale;
                    scale *= 0.1d;
                    hasDigits = true;
                    index++;
                }
            }

            if (!hasDigits || index != token.Length)
                return false;

            value = (float)(negative ? -result : result);
            return math.isfinite(value);
        }

        private static ReadOnlySpan<byte> ReadToken(ref ReadOnlySpan<byte> line)
        {
            int comma = IndexOf(line, Comma);
            if (comma < 0)
            {
                ReadOnlySpan<byte> token = line;
                line = ReadOnlySpan<byte>.Empty;
                return token;
            }

            ReadOnlySpan<byte> result = line.Slice(0, comma);
            line = line.Slice(comma + 1);
            return result;
        }

        private static int IndexOf(ReadOnlySpan<byte> value, byte target)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (value[i] == target)
                    return i;
            }

            return -1;
        }

        private static bool TryReadLine(ReadOnlySpan<byte> bytes, ref int cursor, out ReadOnlySpan<byte> line)
        {
            line = default;
            if (cursor >= bytes.Length)
                return false;

            int start = cursor;
            while (cursor < bytes.Length && bytes[cursor] != LineFeed)
                cursor++;

            int length = cursor - start;
            if (cursor < bytes.Length && bytes[cursor] == LineFeed)
                cursor++;

            line = bytes.Slice(start, length);
            if (line.Length > 0 && line[line.Length - 1] == CarriageReturn)
                line = line.Slice(0, line.Length - 1);

            return true;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && value[start] <= 32)
                start++;
            while (end >= start && value[end] <= 32)
                end--;
            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static bool ContainsAscii(ReadOnlySpan<byte> value, string needle)
        {
            if (string.IsNullOrEmpty(needle) || value.Length < needle.Length)
                return false;

            for (int i = 0; i <= value.Length - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    byte c = value[i + j];
                    if (c >= (byte)'A' && c <= (byte)'Z')
                        c = (byte)(c + 32);
                    if (c != (byte)needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }
    }
}
