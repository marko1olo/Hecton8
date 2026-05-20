using System;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.Equipment.Auxiliary
{
    public static class AuxiliaryEquipmentProfilesCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;

        public static bool TryApplyProfilesCsv(
            ReadOnlySpan<byte> bytes,
            NativeArray<AuxiliaryProfileDTO> profiles,
            out AuxiliaryCsvParseResult result)
        {
            result = default;
            if (bytes.Length == 0 || !profiles.IsCreated || profiles.Length == 0)
            {
                result.FaultFlags = AuxiliaryEquipmentFlags.Faulted;
                return false;
            }

            int parsed = 0;
            int skipped = 0;
            int rowStart = 0;
            for (int i = 0; i <= bytes.Length; i++)
            {
                if (i < bytes.Length && bytes[i] != (byte)'\n')
                    continue;

                int rowEnd = i;
                if (rowEnd > rowStart && bytes[rowEnd - 1] == (byte)'\r')
                    rowEnd--;

                if (TryParseLine(bytes, rowStart, rowEnd, out AuxiliaryProfileDTO profile))
                {
                    if (parsed < profiles.Length)
                    {
                        profiles[parsed] = profile;
                        result.LastProfileHash = profile.ProfileHash;
                        parsed++;
                    }
                    else
                    {
                        skipped++;
                    }
                }
                else
                {
                    skipped++;
                }

                rowStart = i + 1;
            }

            result.ParsedRows = parsed;
            result.SkippedRows = math.max(0, skipped - 1);
            result.FaultFlags = parsed > 0 ? 0u : AuxiliaryEquipmentFlags.Faulted;
            return parsed > 0;
        }

        private static bool TryParseLine(ReadOnlySpan<byte> bytes, int start, int end, out AuxiliaryProfileDTO profile)
        {
            profile = default;
            start = SkipWhitespace(bytes, start, end);
            end = TrimEnd(bytes, start, end);
            if (start >= end || bytes[start] == (byte)'#')
                return false;

            int tokenStart = start;
            int tokenEnd = FindComma(bytes, tokenStart, end);
            if (EqualsAsciiInsensitive(bytes, tokenStart, tokenEnd, "name"))
                return false;

            uint profileHash = HashToken(bytes, tokenStart, tokenEnd);
            int cursor = tokenEnd + 1;
            if (!TryReadFloatColumn(bytes, ref cursor, end, out float lifetime))
                return false;
            if (!TryReadFloatColumn(bytes, ref cursor, end, out float scalar0))
                return false;
            if (!TryReadFloatColumn(bytes, ref cursor, end, out float scalar1))
                return false;

            int kindStart = cursor;
            int kindEnd = FindComma(bytes, kindStart, end);
            uint prefabHash = ResolvePrefabHash(bytes, kindStart, kindEnd, tokenStart, tokenEnd);
            if (prefabHash == 0u)
                return false;

            profile.ProfileHash = profileHash;
            profile.PrefabHashID = prefabHash;
            profile.Lifetime = math.max(0.01f, lifetime);
            profile.Scalar0 = math.max(0f, scalar0);
            profile.Scalar1 = math.max(0f, scalar1);
            profile.Flags = AuxiliaryEquipmentMath.ResolveKindFlags(prefabHash);
            return true;
        }

        private static bool TryReadFloatColumn(ReadOnlySpan<byte> bytes, ref int cursor, int end, out float value)
        {
            int tokenStart = cursor;
            int tokenEnd = FindComma(bytes, tokenStart, end);
            cursor = tokenEnd + 1;
            return TryParseFloat(bytes, tokenStart, tokenEnd, out value);
        }

        private static int FindComma(ReadOnlySpan<byte> bytes, int start, int end)
        {
            int i = start;
            while (i < end && bytes[i] != (byte)',')
                i++;
            return i;
        }

        private static int SkipWhitespace(ReadOnlySpan<byte> bytes, int start, int end)
        {
            int i = start;
            while (i < end)
            {
                byte b = bytes[i];
                if (b != (byte)' ' && b != (byte)'\t')
                    break;
                i++;
            }

            return i;
        }

        private static int TrimEnd(ReadOnlySpan<byte> bytes, int start, int end)
        {
            int i = end;
            while (i > start)
            {
                byte b = bytes[i - 1];
                if (b != (byte)' ' && b != (byte)'\t')
                    break;
                i--;
            }

            return i;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            start = SkipWhitespace(bytes, start, end);
            end = TrimEnd(bytes, start, end);
            if (start >= end)
                return false;

            int sign = 1;
            int i = start;
            if (bytes[i] == (byte)'-')
            {
                sign = -1;
                i++;
            }
            else if (bytes[i] == (byte)'+')
            {
                i++;
            }

            double whole = 0.0;
            bool any = false;
            while (i < end)
            {
                byte b = bytes[i];
                if (b < (byte)'0' || b > (byte)'9')
                    break;
                whole = (whole * 10.0) + (b - (byte)'0');
                any = true;
                i++;
            }

            double fraction = 0.0;
            double divisor = 1.0;
            if (i < end && bytes[i] == (byte)'.')
            {
                i++;
                while (i < end)
                {
                    byte b = bytes[i];
                    if (b < (byte)'0' || b > (byte)'9')
                        break;
                    fraction = (fraction * 10.0) + (b - (byte)'0');
                    divisor *= 10.0;
                    any = true;
                    i++;
                }
            }

            if (!any)
                return false;

            i = SkipWhitespace(bytes, i, end);
            if (i < end)
                return false;

            value = (float)(sign * (whole + (fraction / divisor)));
            return math.isfinite(value);
        }

        private static uint ResolvePrefabHash(ReadOnlySpan<byte> bytes, int kindStart, int kindEnd, int nameStart, int nameEnd)
        {
            if (ContainsAsciiInsensitive(bytes, kindStart, kindEnd, "flare") ||
                ContainsAsciiInsensitive(bytes, nameStart, nameEnd, "flare"))
            {
                return AuxiliaryEquipmentConstants.FlarePrefabHash;
            }

            if (ContainsAsciiInsensitive(bytes, kindStart, kindEnd, "ping") ||
                ContainsAsciiInsensitive(bytes, kindStart, kindEnd, "sonar") ||
                ContainsAsciiInsensitive(bytes, kindStart, kindEnd, "sensor") ||
                ContainsAsciiInsensitive(bytes, nameStart, nameEnd, "ping") ||
                ContainsAsciiInsensitive(bytes, nameStart, nameEnd, "sonar") ||
                ContainsAsciiInsensitive(bytes, nameStart, nameEnd, "sensor"))
            {
                return AuxiliaryEquipmentConstants.SensorPingPrefabHash;
            }

            if (ContainsAsciiInsensitive(bytes, kindStart, kindEnd, "tether") ||
                ContainsAsciiInsensitive(bytes, kindStart, kindEnd, "gravity") ||
                ContainsAsciiInsensitive(bytes, nameStart, nameEnd, "tether") ||
                ContainsAsciiInsensitive(bytes, nameStart, nameEnd, "gravity"))
            {
                return AuxiliaryEquipmentConstants.GravityTetherPrefabHash;
            }

            return 0u;
        }

        private static uint HashToken(ReadOnlySpan<byte> bytes, int start, int end)
        {
            uint hash = FnvOffset;
            for (int i = start; i < end; i++)
            {
                byte b = ToLowerAscii(bytes[i]);
                if (b == (byte)' ' || b == (byte)'\t')
                    continue;
                hash = (hash ^ b) * FnvPrime;
            }

            return hash;
        }

        private static bool EqualsAsciiInsensitive(ReadOnlySpan<byte> bytes, int start, int end, string value)
        {
            start = SkipWhitespace(bytes, start, end);
            end = TrimEnd(bytes, start, end);
            if (end - start != value.Length)
                return false;

            for (int i = 0; i < value.Length; i++)
            {
                if (ToLowerAscii(bytes[start + i]) != (byte)value[i])
                    return false;
            }

            return true;
        }

        private static bool ContainsAsciiInsensitive(ReadOnlySpan<byte> bytes, int start, int end, string value)
        {
            start = SkipWhitespace(bytes, start, end);
            end = TrimEnd(bytes, start, end);
            int length = value.Length;
            if (length <= 0 || end - start < length)
                return false;

            for (int i = start; i <= end - length; i++)
            {
                bool match = true;
                for (int j = 0; j < length; j++)
                {
                    if (ToLowerAscii(bytes[i + j]) != (byte)value[j])
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

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z'
                ? (byte)(value + 32)
                : value;
        }
    }
}
