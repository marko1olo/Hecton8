using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class FloraDearLieVfxProfileCsvImporter
    {
        private const string CsvPath = "Assets/StreamingAssets/Hecton8/flora_vfx_profiles.csv";
        private const string ReportPath = "Docs/Reports/FLORA_VFX_PROFILE_IMPORT.json";

        [MenuItem("Hecton8/Diagnostics/Import Flora Dear Lie VFX CSV")]
        private static void ImportMenu()
        {
            ImportAndWriteReport();
        }

        internal static void ImportAndWriteReport()
        {
            string absoluteCsv = Path.Combine(Directory.GetCurrentDirectory(), CsvPath);
            string absoluteReport = Path.Combine(Directory.GetCurrentDirectory(), ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absoluteReport));

            if (!File.Exists(absoluteCsv))
            {
                File.WriteAllText(absoluteReport, "{ \"agent\": \"SHINOBU_268\", \"status\": \"missing\", \"path\": \"" + CsvPath + "\" }\n");
                AssetDatabase.Refresh();
                return;
            }

            byte[] bytes = ReadAllBytesCold(absoluteCsv);
            ReadOnlySpan<byte> csv = bytes;
            int index = 0;
            SkipLine(csv, ref index);
            int valid = 0;
            int rejected = 0;
            StringBuilder builder = new StringBuilder(4096);
            builder.Append("{\n  \"agent\": \"SHINOBU_268\",\n  \"profiles\": [\n");
            while (index < csv.Length)
            {
                ReadOnlySpan<byte> flora = ReadCell(csv, ref index, out bool rowEnd);
                if (flora.Length == 0 && rowEnd)
                    continue;

                ReadOnlySpan<byte> minQualityCell = rowEnd ? ReadOnlySpan<byte>.Empty : ReadCell(csv, ref index, out rowEnd);
                ReadOnlySpan<byte> maxQuantityCell = rowEnd ? ReadOnlySpan<byte>.Empty : ReadCell(csv, ref index, out rowEnd);
                ReadOnlySpan<byte> colorWeightCell = rowEnd ? ReadOnlySpan<byte>.Empty : ReadCell(csv, ref index, out rowEnd);
                if (!rowEnd)
                    SkipLine(csv, ref index);

                if (!TryParseFloraHash(flora, out uint floraHash) ||
                    !TryParseFloat(minQualityCell, out float minQuality) ||
                    !TryParseFloat(maxQuantityCell, out float maxQuantity) ||
                    !TryParseFloat(colorWeightCell, out float colorWeight))
                {
                    rejected++;
                    continue;
                }

                if (valid > 0)
                    builder.Append(",\n");

                builder.Append("    { \"floraHash\": ");
                builder.Append(floraHash);
                builder.Append(", \"minQuality\": ");
                builder.Append(Mathf.Clamp01(minQuality).ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(", \"maxQuantity\": ");
                builder.Append(Mathf.Max(0f, maxQuantity).ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(", \"colorWeight\": ");
                builder.Append(Mathf.Clamp01(colorWeight).ToString("0.###", CultureInfo.InvariantCulture));
                builder.Append(" }");
                valid++;
            }

            builder.Append("\n  ],\n  \"valid\": ");
            builder.Append(valid);
            builder.Append(",\n  \"rejected\": ");
            builder.Append(rejected);
            builder.Append("\n}\n");
            File.WriteAllText(absoluteReport, builder.ToString());
            AssetDatabase.Refresh();
        }

        private static byte[] ReadAllBytesCold(string path)
        {
            FileInfo info = new FileInfo(path);
            int length = checked((int)Math.Min(Math.Max(0L, info.Length), int.MaxValue));
            byte[] bytes = new byte[length];
            using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                int offset = 0;
                while (offset < bytes.Length)
                {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                        break;

                    offset += read;
                }
            }

            return bytes;
        }

        private static ReadOnlySpan<byte> ReadCell(ReadOnlySpan<byte> data, ref int index, out bool endOfLine)
        {
            endOfLine = false;
            int start = index;
            while (index < data.Length)
            {
                byte value = data[index];
                if (value == (byte)',' || value == (byte)'\n' || value == (byte)'\r')
                    break;

                index++;
            }

            int end = index;
            if (index < data.Length)
            {
                byte delimiter = data[index++];
                if (delimiter == (byte)'\r')
                {
                    if (index < data.Length && data[index] == (byte)'\n')
                        index++;
                    endOfLine = true;
                }
                else if (delimiter == (byte)'\n')
                {
                    endOfLine = true;
                }
            }
            else
            {
                endOfLine = true;
            }

            while (start < end && IsAsciiWhitespace(data[start]))
                start++;
            while (end > start && IsAsciiWhitespace(data[end - 1]))
                end--;
            return data.Slice(start, end - start);
        }

        private static void SkipLine(ReadOnlySpan<byte> data, ref int index)
        {
            while (index < data.Length)
            {
                byte value = data[index++];
                if (value == (byte)'\n')
                    return;
            }
        }

        private static bool TryParseFloraHash(ReadOnlySpan<byte> token, out uint hash)
        {
            hash = 0u;
            if (token.Length == 0)
                return false;

            ReadOnlySpan<byte> hex = token;
            if (hex.Length > 2 && hex[0] == (byte)'0' && (hex[1] == (byte)'x' || hex[1] == (byte)'X'))
                hex = hex.Slice(2);

            if (hex.Length > 0 && hex.Length <= 8 && TryParseHexUInt(hex, out hash))
                return true;

            hash = Fnv1aLower(token);
            return hash != 0u;
        }

        private static bool TryParseHexUInt(ReadOnlySpan<byte> token, out uint value)
        {
            value = 0u;
            for (int i = 0; i < token.Length; i++)
            {
                int nibble = HexNibble(token[i]);
                if (nibble < 0)
                    return false;

                value = (value << 4) | (uint)nibble;
            }

            return true;
        }

        private static bool TryParseFloat(ReadOnlySpan<byte> token, out float value)
        {
            value = 0f;
            if (token.Length == 0)
                return false;

            int index = 0;
            double sign = 1d;
            if (token[index] == (byte)'-' || token[index] == (byte)'+')
            {
                sign = token[index] == (byte)'-' ? -1d : 1d;
                index++;
                if (index >= token.Length)
                    return false;
            }

            double result = 0d;
            bool hasDigit = false;
            while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
            {
                result = (result * 10d) + (token[index] - (byte)'0');
                index++;
                hasDigit = true;
            }

            if (index < token.Length && token[index] == (byte)'.')
            {
                index++;
                double scale = 0.1d;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    result += (token[index] - (byte)'0') * scale;
                    scale *= 0.1d;
                    index++;
                    hasDigit = true;
                }
            }

            if (!hasDigit)
                return false;

            if (index < token.Length && (token[index] == (byte)'e' || token[index] == (byte)'E'))
            {
                index++;
                bool negativeExponent = false;
                if (index < token.Length && (token[index] == (byte)'-' || token[index] == (byte)'+'))
                {
                    negativeExponent = token[index] == (byte)'-';
                    index++;
                }

                int exponent = 0;
                bool exponentDigit = false;
                while (index < token.Length && token[index] >= (byte)'0' && token[index] <= (byte)'9')
                {
                    exponent = (exponent * 10) + (token[index] - (byte)'0');
                    index++;
                    exponentDigit = true;
                }

                if (!exponentDigit)
                    return false;

                result *= Math.Pow(10d, negativeExponent ? -exponent : exponent);
            }

            if (index != token.Length)
                return false;

            value = (float)(result * sign);
            return !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static uint Fnv1aLower(ReadOnlySpan<byte> token)
        {
            uint hash = 2166136261u;
            for (int i = 0; i < token.Length; i++)
            {
                byte value = token[i];
                if (value >= (byte)'A' && value <= (byte)'Z')
                    value = (byte)(value + 32);

                hash ^= value;
                hash *= 16777619u;
            }

            return hash == 0u ? 1u : hash;
        }

        private static int HexNibble(byte value)
        {
            if (value >= (byte)'0' && value <= (byte)'9')
                return value - (byte)'0';
            if (value >= (byte)'a' && value <= (byte)'f')
                return value - (byte)'a' + 10;
            if (value >= (byte)'A' && value <= (byte)'F')
                return value - (byte)'A' + 10;
            return -1;
        }

        private static bool IsAsciiWhitespace(byte value)
        {
            return value == (byte)' ' || value == (byte)'\t';
        }
    }
}
