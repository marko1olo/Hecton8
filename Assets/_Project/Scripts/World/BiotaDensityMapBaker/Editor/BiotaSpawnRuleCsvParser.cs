#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Collections;
using Unity.Mathematics;

namespace Hecton8.World.BiotaDensityMapBaker.Editor
{
    public static class BiotaSpawnRuleCsvParser
    {
        public const int CsvSchemaVersion = 1;
        public const string SchemaColumns = "species,layer,min_depth,max_depth,min_slope,max_slope,biome_hash,preferred_temperature,temperature_tolerance,spawn_weight,silt_affinity,thermal_affinity";
        public const int CsvValidationOk = 0;
        public const int CsvErrorMissing = 1001;
        public const int CsvErrorHeaderMissing = 1002;
        public const int CsvErrorHeaderMismatch = 1003;
        public const int CsvErrorLineOverflow = 1004;
        public const int CsvErrorMalformedRow = 1005;
        public const int CsvErrorNoRules = 1006;
        public const int CsvErrorTooManyRules = 1007;

        public static bool TryLoadRules(
            string path,
            ref FixedList4096Bytes<BiotaSpawnRuleDTO> rules,
            ref FixedList4096Bytes<BiotaRuleWeightDTO> weights,
            out int ruleCount,
            out uint schemaHash,
            out int validationCode)
        {
            ruleCount = 0;
            schemaHash = 2166136261u;
            validationCode = CsvValidationOk;
            rules.Clear();
            weights.Clear();

            if (!File.Exists(path))
            {
                validationCode = CsvErrorMissing;
                return false;
            }

            Span<byte> lineBuffer = stackalloc byte[4096];
            int lineLength = 0;
            bool lineOverflow = false;
            bool overflowObserved = false;
            bool headerSeen = false;
            bool headerRejected = false;
            bool malformedObserved = false;
            bool tooManyRules = false;

            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                while (true)
                {
                    int read = stream.ReadByte();
                    if (read < 0 || read == (byte)'\n')
                    {
                        if (!lineOverflow)
                        {
                            ReadOnlySpan<byte> line = Trim(SkipUtf8Bom(lineBuffer.Slice(0, lineLength)));
                            if (line.Length != 0 && line[0] != (byte)'#')
                            {
                                if (!headerSeen)
                                {
                                    headerSeen = true;
                                    if (!IsSupportedHeader(line))
                                        headerRejected = true;
                                    else
                                        schemaHash = HashLine(line, schemaHash);
                                }
                                else if (!ProcessRuleLine(line, ref rules, ref weights, ref ruleCount, ref schemaHash, ref tooManyRules))
                                {
                                    malformedObserved = true;
                                }
                            }
                        }
                        else
                        {
                            overflowObserved = true;
                        }

                        lineLength = 0;
                        lineOverflow = false;
                        if (read < 0)
                            break;
                        continue;
                    }

                    if (lineLength < lineBuffer.Length)
                    {
                        lineBuffer[lineLength] = (byte)read;
                        lineLength++;
                    }
                    else
                    {
                        lineOverflow = true;
                    }
                }
            }

            if (overflowObserved)
            {
                validationCode = CsvErrorLineOverflow;
                rules.Clear();
                weights.Clear();
                ruleCount = 0;
                return false;
            }

            if (!headerSeen)
            {
                validationCode = CsvErrorHeaderMissing;
                return false;
            }

            if (headerRejected)
            {
                validationCode = CsvErrorHeaderMismatch;
                rules.Clear();
                weights.Clear();
                return false;
            }

            if (tooManyRules)
            {
                validationCode = CsvErrorTooManyRules;
                rules.Clear();
                weights.Clear();
                ruleCount = 0;
                return false;
            }

            if (malformedObserved)
            {
                validationCode = CsvErrorMalformedRow;
                rules.Clear();
                weights.Clear();
                ruleCount = 0;
                return false;
            }

            if (ruleCount <= 0)
            {
                validationCode = CsvErrorNoRules;
                return false;
            }

            validationCode = CsvValidationOk;
            return true;
        }

        private static bool ProcessRuleLine(
            ReadOnlySpan<byte> line,
            ref FixedList4096Bytes<BiotaSpawnRuleDTO> rules,
            ref FixedList4096Bytes<BiotaRuleWeightDTO> weights,
            ref int ruleCount,
            ref uint schemaHash,
            ref bool tooManyRules)
        {
            if (rules.Length >= BiotaDensityBakeConstants.MaxRuleCount ||
                weights.Length >= BiotaDensityBakeConstants.MaxRuleCount)
            {
                tooManyRules = true;
                return false;
            }

            schemaHash = HashLine(line, schemaHash);
            if (!TryParseRule(line, out BiotaSpawnRuleDTO rule, out BiotaRuleWeightDTO weight))
                return false;

            rules.Add(rule);
            weights.Add(weight);
            ruleCount++;
            return true;
        }

        private static bool TryParseRule(
            ReadOnlySpan<byte> line,
            out BiotaSpawnRuleDTO rule,
            out BiotaRuleWeightDTO weight)
        {
            rule = default;
            weight = default;
            ReadOnlySpan<byte> species = GetCell(line, 0);
            if (species.Length == 0 || GetCell(line, 12).Length != 0)
                return false;

            if (!TryParseUInt(GetCell(line, 1), out uint layer) ||
                !TryParseFloat(GetCell(line, 2), out float minDepth) ||
                !TryParseFloat(GetCell(line, 3), out float maxDepth) ||
                !TryParseFloat(GetCell(line, 4), out float minSlope) ||
                !TryParseFloat(GetCell(line, 5), out float maxSlope) ||
                !TryParseBiomeHash(GetCell(line, 6), out uint biomeHash) ||
                !TryParseFloat(GetCell(line, 7), out float preferredTemperature) ||
                !TryParseFloat(GetCell(line, 8), out float temperatureTolerance) ||
                !TryParseFloat(GetCell(line, 9), out float spawnWeight) ||
                !TryParseFloat(GetCell(line, 10), out float siltAffinity) ||
                !TryParseFloat(GetCell(line, 11), out float thermalAffinity))
            {
                return false;
            }

            rule = new BiotaSpawnRuleDTO
            {
                MinDepth = math.max(0f, minDepth),
                MaxDepth = math.max(minDepth, maxDepth),
                MinSlope = math.clamp(minSlope, 0f, 90f),
                MaxSlope = math.clamp(maxSlope, 0f, 90f),
                RequiredBiomeHash = biomeHash,
                PreferredTemperature = preferredTemperature
            };
            weight = new BiotaRuleWeightDTO
            {
                SpeciesHash = HashAscii(species),
                SpawnWeight = math.max(0f, spawnWeight),
                TemperatureTolerance = math.max(0.001f, temperatureTolerance),
                SiltAffinity = math.saturate(siltAffinity),
                ThermalAffinity = math.saturate(thermalAffinity),
                LayerIndex = layer,
                Flags = 0u
            };
            return true;
        }

        private static bool IsSupportedHeader(ReadOnlySpan<byte> value)
        {
            return EqualsAscii(GetCell(value, 0), "species") &&
                   EqualsAscii(GetCell(value, 1), "layer") &&
                   EqualsAscii(GetCell(value, 2), "min_depth") &&
                   EqualsAscii(GetCell(value, 3), "max_depth") &&
                   EqualsAscii(GetCell(value, 4), "min_slope") &&
                   EqualsAscii(GetCell(value, 5), "max_slope") &&
                   EqualsAscii(GetCell(value, 6), "biome_hash") &&
                   EqualsAscii(GetCell(value, 7), "preferred_temperature") &&
                   EqualsAscii(GetCell(value, 8), "temperature_tolerance") &&
                   EqualsAscii(GetCell(value, 9), "spawn_weight") &&
                   EqualsAscii(GetCell(value, 10), "silt_affinity") &&
                   EqualsAscii(GetCell(value, 11), "thermal_affinity") &&
                   GetCell(value, 12).Length == 0;
        }

        private static ReadOnlySpan<byte> GetCell(ReadOnlySpan<byte> line, int targetColumn)
        {
            int column = 0;
            int start = 0;
            for (int i = 0; i <= line.Length; i++)
            {
                if (i != line.Length && line[i] != (byte)',')
                    continue;

                if (column == targetColumn)
                    return Trim(line.Slice(start, i - start));

                start = i + 1;
                column++;
            }

            return ReadOnlySpan<byte>.Empty;
        }

        private static bool TryParseUInt(ReadOnlySpan<byte> span, out uint value)
        {
            value = 0u;
            span = Trim(span);
            if (span.Length == 0)
                return false;

            for (int i = 0; i < span.Length; i++)
            {
                byte c = span[i];
                if (c < (byte)'0' || c > (byte)'9')
                    return false;
                uint digit = (uint)(c - (byte)'0');
                if (value > (uint.MaxValue - digit) / 10u)
                    return false;
                value = value * 10u + digit;
            }

            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> span, out float value)
        {
            value = 0f;
            span = Trim(span);
            if (span.Length == 0)
                return false;

            int index = 0;
            double sign = 1d;
            if (span[index] == (byte)'-' || span[index] == (byte)'+')
            {
                sign = span[index] == (byte)'-' ? -1d : 1d;
                index++;
            }

            double integer = 0d;
            bool any = false;
            while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
            {
                any = true;
                integer = integer * 10d + span[index] - (byte)'0';
                index++;
            }

            double fraction = 0d;
            double divisor = 1d;
            if (index < span.Length && span[index] == (byte)'.')
            {
                index++;
                while (index < span.Length && span[index] >= (byte)'0' && span[index] <= (byte)'9')
                {
                    any = true;
                    fraction = fraction * 10d + span[index] - (byte)'0';
                    divisor *= 10d;
                    index++;
                }
            }

            if (!any || index != span.Length)
                return false;

            double parsed = (integer + fraction / divisor) * sign;
            if (parsed > float.MaxValue || parsed < -float.MaxValue)
                return false;

            value = (float)parsed;
            return math.isfinite(value);
        }

        private static bool TryParseBiomeHash(ReadOnlySpan<byte> span, out uint value)
        {
            span = Trim(span);
            if (span.Length == 0 || EqualsAscii(span, "any") || EqualsAscii(span, "*"))
            {
                value = BiotaDensityBakeConstants.BiomeAny;
                return true;
            }

            if (EqualsAscii(span, "reef"))
            {
                value = 0x52454546u;
                return true;
            }

            if (EqualsAscii(span, "silt"))
            {
                value = 0x53494C54u;
                return true;
            }

            if (EqualsAscii(span, "vent"))
            {
                value = 0x56454E54u;
                return true;
            }

            if (EqualsAscii(span, "hadal") || EqualsAscii(span, "hadl"))
            {
                value = 0x4841444Cu;
                return true;
            }

            if (span.Length > 2 && span[0] == (byte)'0' && ToLowerAscii(span[1]) == (byte)'x')
                return TryParseHex(span.Slice(2), out value);

            value = HashAscii(span);
            return true;
        }

        private static bool TryParseHex(ReadOnlySpan<byte> span, out uint value)
        {
            value = 0u;
            if (span.Length == 0 || span.Length > 8)
                return false;

            for (int i = 0; i < span.Length; i++)
            {
                byte c = ToLowerAscii(span[i]);
                uint digit;
                if (c >= (byte)'0' && c <= (byte)'9')
                    digit = (uint)(c - (byte)'0');
                else if (c >= (byte)'a' && c <= (byte)'f')
                    digit = (uint)(c - (byte)'a' + 10);
                else
                    return false;

                value = (value << 4) | digit;
            }

            return true;
        }

        private static uint HashAscii(ReadOnlySpan<byte> value)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < value.Length; i++)
                hash = BiotaDensityBakeMath.Mix(hash ^ ToLowerAscii(value[i]));
            return hash == 0u ? 1u : hash;
        }

        private static uint HashLine(ReadOnlySpan<byte> value, uint hash)
        {
            for (int i = 0; i < value.Length; i++)
                hash = BiotaDensityBakeMath.Mix(hash ^ value[i]);
            return hash == 0u ? 1u : hash;
        }

        private static bool EqualsAscii(ReadOnlySpan<byte> value, string ascii)
        {
            if (value.Length != ascii.Length)
                return false;
            for (int i = 0; i < ascii.Length; i++)
            {
                if (ToLowerAscii(value[i]) != ToLowerAscii((byte)ascii[i]))
                    return false;
            }
            return true;
        }

        private static byte ToLowerAscii(byte value)
        {
            return value >= (byte)'A' && value <= (byte)'Z' ? (byte)(value + 32) : value;
        }

        private static ReadOnlySpan<byte> Trim(ReadOnlySpan<byte> span)
        {
            int start = 0;
            int end = span.Length - 1;
            while (start <= end && IsWhitespace(span[start]))
                start++;
            while (end >= start && IsWhitespace(span[end]))
                end--;
            return start <= end ? span.Slice(start, end - start + 1) : ReadOnlySpan<byte>.Empty;
        }

        private static ReadOnlySpan<byte> SkipUtf8Bom(ReadOnlySpan<byte> span)
        {
            return span.Length >= 3 &&
                   span[0] == 0xEF &&
                   span[1] == 0xBB &&
                   span[2] == 0xBF
                ? span.Slice(3)
                : span;
        }

        private static bool IsWhitespace(byte value)
        {
            return value == (byte)' ' ||
                   value == (byte)'\t' ||
                   value == (byte)'\r' ||
                   value == (byte)'\n';
        }
    }
}
#endif
