#if UNITY_EDITOR
namespace Hecton8.Tools
{
    using System;
    using Unity.Collections;
    using Unity.Mathematics;

    public static class EquipmentHardwareSpecsCsvParser
    {
        private const uint FnvOffset = 2166136261u;
        private const uint FnvPrime = 16777619u;
        private const uint FaultMalformedRow = 1u << 0;
        private const uint FaultCapacityOverflow = 1u << 1;

        public static EquipmentCsvParseResult Parse(ReadOnlySpan<byte> csv, NativeArray<EquipmentHardwareSpecDTO> specs)
        {
            EquipmentCsvParseResult result = default;
            if (!specs.IsCreated || specs.Length <= 0)
            {
                result.FaultFlags |= FaultCapacityOverflow;
                return result;
            }

            int rowStart = 0;
            int rowIndex = 0;
            int writeIndex = 0;
            for (int i = 0; i <= csv.Length; i++)
            {
                bool end = i == csv.Length;
                byte value = end ? (byte)'\n' : csv[i];
                if (!end && value != (byte)'\n')
                    continue;

                int rowEnd = i;
                if (rowEnd > rowStart && csv[rowEnd - 1] == (byte)'\r')
                    rowEnd--;

                ReadOnlySpan<byte> row = csv.Slice(rowStart, rowEnd - rowStart);
                if (rowIndex > 0 && row.Length > 0)
                {
                    if (writeIndex >= specs.Length)
                    {
                        result.FaultFlags |= FaultCapacityOverflow;
                        result.SkippedRows++;
                    }
                    else if (TryParseRow(row, out EquipmentHardwareSpecDTO spec))
                    {
                        specs[writeIndex++] = spec;
                        result.ParsedRows++;
                        result.LastToolHashID = spec.ToolHashID;
                    }
                    else
                    {
                        result.FaultFlags |= FaultMalformedRow;
                        result.SkippedRows++;
                    }
                }

                rowStart = i + 1;
                rowIndex++;
            }

            for (int i = writeIndex; i < specs.Length; i++)
                specs[i] = default;

            return result;
        }

        private static bool TryParseRow(ReadOnlySpan<byte> row, out EquipmentHardwareSpecDTO spec)
        {
            spec = default;
            int cursor = 0;
            ReadOnlySpan<byte> name = ReadCell(row, ref cursor);
            if (name.Length <= 0)
                return false;

            spec.ToolHashID = ParseToolHash(name);
            spec.BatteryCapacity = ParseFloat(ReadCell(row, ref cursor), 0f);
            spec.ThermalLimit = ParseFloat(ReadCell(row, ref cursor), 1f);
            spec.PowerDrawRate = ParseFloat(ReadCell(row, ref cursor), 0f);
            spec.HeatGenerationRate = ParseFloat(ReadCell(row, ref cursor), 0f);
            spec.CooldownRate = ParseFloat(ReadCell(row, ref cursor), 0f);
            spec.Flags = ParseUInt(ReadCell(row, ref cursor), 0u);
            spec.Reserved0 = 0u;
            return spec.ToolHashID != 0u;
        }

        private static uint ParseToolHash(ReadOnlySpan<byte> value)
        {
            return TryParseUInt(value, out uint parsed) ? parsed : HashTrimmed(value);
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> row, ref int cursor)
        {
            if (cursor >= row.Length)
                return ReadOnlySpan<byte>.Empty;

            int start = cursor;
            while (cursor < row.Length && row[cursor] != (byte)',')
                cursor++;

            int end = cursor;
            if (cursor < row.Length && row[cursor] == (byte)',')
                cursor++;

            while (start < end && IsWhitespace(row[start]))
                start++;
            while (end > start && IsWhitespace(row[end - 1]))
                end--;

            return row.Slice(start, end - start);
        }

        private static uint HashTrimmed(ReadOnlySpan<byte> value)
        {
            uint hash = FnvOffset;
            for (int i = 0; i < value.Length; i++)
            {
                byte b = value[i];
                if (b >= (byte)'A' && b <= (byte)'Z')
                    b = (byte)(b + 32);
                hash ^= b;
                hash *= FnvPrime;
            }

            return hash;
        }

        private static float ParseFloat(ReadOnlySpan<byte> value, float fallback)
        {
            if (value.Length <= 0)
                return fallback;

            int i = 0;
            float sign = 1f;
            if (value[i] == (byte)'-')
            {
                sign = -1f;
                i++;
            }
            else if (value[i] == (byte)'+')
            {
                i++;
            }

            float whole = 0f;
            bool consumed = false;
            while (i < value.Length && value[i] >= (byte)'0' && value[i] <= (byte)'9')
            {
                whole = (whole * 10f) + (value[i] - (byte)'0');
                i++;
                consumed = true;
            }

            float fraction = 0f;
            float scale = 1f;
            if (i < value.Length && value[i] == (byte)'.')
            {
                i++;
                while (i < value.Length && value[i] >= (byte)'0' && value[i] <= (byte)'9')
                {
                    fraction = (fraction * 10f) + (value[i] - (byte)'0');
                    scale *= 10f;
                    i++;
                    consumed = true;
                }
            }

            if (!consumed)
                return fallback;

            float parsed = sign * (whole + (fraction * math.rcp(math.max(1f, scale))));
            return math.isfinite(parsed) ? parsed : fallback;
        }

        private static uint ParseUInt(ReadOnlySpan<byte> value, uint fallback)
        {
            return TryParseUInt(value, out uint parsed) ? parsed : fallback;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> value, out uint parsed)
        {
            parsed = 0u;
            if (value.Length <= 0)
                return false;

            int start = 0;
            int radix = 10;
            if (value.Length > 2 &&
                value[0] == (byte)'0' &&
                (value[1] == (byte)'x' || value[1] == (byte)'X'))
            {
                start = 2;
                radix = 16;
            }

            bool consumed = false;
            for (int i = start; i < value.Length; i++)
            {
                byte b = value[i];
                uint digit;
                if (b >= (byte)'0' && b <= (byte)'9')
                    digit = (uint)(b - (byte)'0');
                else if (radix == 16 && b >= (byte)'a' && b <= (byte)'f')
                    digit = 10u + (uint)(b - (byte)'a');
                else if (radix == 16 && b >= (byte)'A' && b <= (byte)'F')
                    digit = 10u + (uint)(b - (byte)'A');
                else
                    return false;

                if (digit >= (uint)radix)
                    return false;

                uint radixValue = (uint)radix;
                if (parsed > uint.MaxValue / radixValue)
                    return false;

                uint scaled = parsed * radixValue;
                if (digit > uint.MaxValue - scaled)
                    return false;

                parsed = scaled + digit;
                consumed = true;
            }

            return consumed;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }
    }

    public static class IlluminationHardwareProfilesCsvParser
    {
        public static EquipmentCsvParseResult Parse(ReadOnlySpan<byte> csv, NativeArray<EquipmentHardwareSpecDTO> specs)
        {
            return EquipmentHardwareSpecsCsvParser.Parse(csv, specs);
        }
    }
}
#endif
