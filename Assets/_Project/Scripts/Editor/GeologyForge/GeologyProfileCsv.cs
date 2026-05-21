using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor.GeologyForge
{
    internal static unsafe class GeologyProfileCsv
    {
        private const int CsvErrorMalformedCell = 1001;
        private const int CsvErrorIntegerOverflow = 1002;
        private const int CsvErrorNonFiniteFloat = 1003;
        private const int CsvErrorNonPositiveValue = 1004;
        private const int CsvErrorInvalidTerminator = 1005;
        private const int CsvErrorColumnCount = 1006;
        private const int CsvErrorHeaderSchema = 1007;
        private const int CsvErrorFileSize = 1008;
        private const int CsvErrorNoProfiles = 1009;
        private const int MaximumCsvBytes = 4 * 1024 * 1024;

        public static void LoadProfiles(List<GeologyBakeProfile> profiles)
        {
            if (profiles == null)
                throw new ArgumentNullException(nameof(profiles));

            profiles.Clear();
            string path = Path.Combine(Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length), GeologyForgeConstants.CsvPath);
            if (!File.Exists(path))
            {
                profiles.Add(DefaultProfile());
                return;
            }

            NativeArray<byte> bytes = default;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    long length64 = stream.Length;
                    if (length64 <= 0L || length64 > MaximumCsvBytes)
                        throw new InvalidDataException("Geology profile CSV error " + CsvErrorFileSize + ": invalid file size " + length64 + " bytes.");

                    int length = (int)length64;
                    bytes = new NativeArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                    Span<byte> target = new Span<byte>(ptr, length);
                    int read = 0;
                    while (read < length)
                    {
                        int chunk = stream.Read(target.Slice(read));
                        if (chunk <= 0)
                            break;
                        read += chunk;
                    }

                    if (read != length || stream.Length != length64)
                        throw new InvalidDataException("Geology profile CSV error " + CsvErrorFileSize + ": unstable file size while reading " + read + " of " + length + " bytes.");

                    int dataOffset = Utf8BomOffset(ptr, read);
                    byte* csv = ptr + dataOffset;
                    int csvLength = read - dataOffset;
                    bool hasIsoLevel = HeaderHasIsoLevel(csv, csvLength);
                    ValidateHeaderSchema(csv, csvLength, hasIsoLevel);

                    int cursor = 0;
                    int rowIndex = 2;
                    SkipLine(csv, csvLength, ref cursor);
                    while (cursor < csvLength)
                    {
                        if (TryReadProfile(csv, csvLength, hasIsoLevel, rowIndex, ref cursor, out GeologyBakeProfile profile))
                            profiles.Add(profile);
                        rowIndex++;
                    }
                }
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }

            if (profiles.Count == 0)
                throw new InvalidDataException("Geology profile CSV error " + CsvErrorNoProfiles + ": existing profile file contains no data rows.");
        }

        public static GeologyBakeProfile DefaultProfile()
        {
            GeologyBakeProfile profile = default;
            profile.Name = new FixedString64Bytes("Basalt_Pillar");
            profile.Seed = 0x5348494Eu;
            profile.Resolution = GeologyForgeConstants.DefaultResolution;
            profile.Variations = 4;
            profile.RadiusMeters = 2.2f;
            profile.HeightScale = 1.7f;
            profile.Frequency = 1.15f;
            profile.NoiseAmplitude = 0.28f;
            profile.RidgedWeight = 0.72f;
            profile.VoronoiWeight = 0.35f;
            profile.Octaves = 5;
            profile.AmbientOcclusionRays = GeologyForgeConstants.DefaultAoRays;
            profile.IsoLevel = 0f;
            profile.GlobalQualityWeight = 0.55f;
            profile.Lod0Budget = GeologyForgeConstants.Lod0TriangleBudget;
            profile.Lod1Budget = GeologyForgeConstants.Lod1TriangleBudget;
            profile.Lod2Budget = GeologyForgeConstants.Lod2TriangleBudget;
            profile.SectorAup = double3.zero;
            return profile;
        }

        private static bool TryReadProfile(byte* bytes, int length, bool hasIsoLevel, int rowIndex, ref int cursor, out GeologyBakeProfile profile)
        {
            profile = default;
            SkipBlank(bytes, length, ref cursor);
            if (cursor >= length)
                return false;

            ValidateRowColumnCount(bytes, length, cursor, hasIsoLevel, rowIndex);

            profile.Name = ReadFixedString(bytes, length, ref cursor);
            int column = 2;
            profile.Seed = ReadUInt(bytes, length, ref cursor, rowIndex, column++, "seed");
            profile.Resolution = math.clamp(ReadInt(bytes, length, ref cursor, rowIndex, column++, "resolution"), GeologyForgeConstants.MinimumResolution, GeologyForgeConstants.MaximumResolution);
            profile.Variations = math.clamp(ReadInt(bytes, length, ref cursor, rowIndex, column++, "variations"), 1, GeologyForgeConstants.MaximumVariations);
            profile.RadiusMeters = RequirePositive(ReadFloat(bytes, length, ref cursor, rowIndex, column, "radius"), rowIndex, column++, "radius");
            profile.HeightScale = RequirePositive(ReadFloat(bytes, length, ref cursor, rowIndex, column, "height"), rowIndex, column++, "height");
            profile.Frequency = RequirePositive(ReadFloat(bytes, length, ref cursor, rowIndex, column, "frequency"), rowIndex, column++, "frequency");
            profile.NoiseAmplitude = math.max(0f, ReadFloat(bytes, length, ref cursor, rowIndex, column++, "amplitude"));
            profile.RidgedWeight = math.saturate(ReadFloat(bytes, length, ref cursor, rowIndex, column++, "ridged"));
            profile.VoronoiWeight = math.saturate(ReadFloat(bytes, length, ref cursor, rowIndex, column++, "voronoi"));
            profile.Octaves = math.clamp(ReadInt(bytes, length, ref cursor, rowIndex, column++, "octaves"), 1, 8);
            profile.AmbientOcclusionRays = math.clamp(ReadInt(bytes, length, ref cursor, rowIndex, column++, "ao_rays"), 1, GeologyForgeConstants.MaximumAoRays);
            profile.IsoLevel = hasIsoLevel ? math.clamp(ReadFloat(bytes, length, ref cursor, rowIndex, column++, "iso_level"), -0.5f, 0.5f) : 0f;
            profile.GlobalQualityWeight = math.saturate(ReadFloat(bytes, length, ref cursor, rowIndex, column++, "quality"));
            profile.Lod0Budget = math.max(32, ReadInt(bytes, length, ref cursor, rowIndex, column++, "lod0"));
            profile.Lod1Budget = math.max(16, ReadInt(bytes, length, ref cursor, rowIndex, column++, "lod1"));
            profile.Lod2Budget = math.max(8, ReadInt(bytes, length, ref cursor, rowIndex, column++, "lod2"));
            double sx = ReadDouble(bytes, length, ref cursor, rowIndex, column++, "sector_x");
            double sy = ReadDouble(bytes, length, ref cursor, rowIndex, column++, "sector_y");
            double sz = ReadDouble(bytes, length, ref cursor, rowIndex, column, "sector_z");
            profile.SectorAup = new double3(sx, sy, sz);
            if (profile.Name.Length == 0)
                profile.Name = new FixedString64Bytes("Unnamed_Geology");
            return true;
        }

        private static void ValidateRowColumnCount(byte* bytes, int length, int cursor, bool hasIsoLevel, int rowIndex)
        {
            int expected = hasIsoLevel ? 20 : 19;
            int columns = 1;
            int scan = cursor;
            while (scan < length && bytes[scan] != '\n' && bytes[scan] != '\r')
            {
                if (bytes[scan] == ',')
                    columns++;
                scan++;
            }

            if (columns != expected)
                throw new InvalidDataException("Geology profile CSV error " + CsvErrorColumnCount + ": row " + rowIndex + " column count mismatch; expected " + expected + ", got " + columns + ".");
        }

        private static bool HeaderHasIsoLevel(byte* bytes, int length)
        {
            int end = 0;
            while (end < length && bytes[end] != '\n' && bytes[end] != '\r')
                end++;

            for (int i = 0; i <= end - 9; i++)
            {
                if (bytes[i] == 'i' &&
                    bytes[i + 1] == 's' &&
                    bytes[i + 2] == 'o' &&
                    bytes[i + 3] == '_' &&
                    bytes[i + 4] == 'l' &&
                    bytes[i + 5] == 'e' &&
                    bytes[i + 6] == 'v' &&
                    bytes[i + 7] == 'e' &&
                    bytes[i + 8] == 'l')
                    return true;
            }

            return false;
        }

        private static int Utf8BomOffset(byte* bytes, int length)
        {
            return length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        }

        private static void ValidateHeaderSchema(byte* bytes, int length, bool hasIsoLevel)
        {
            int cursor = 0;
            int column = 0;
            while (cursor < length && bytes[cursor] != '\n' && bytes[cursor] != '\r')
            {
                int start = cursor;
                while (cursor < length && bytes[cursor] != ',' && bytes[cursor] != '\n' && bytes[cursor] != '\r')
                    cursor++;

                string expected = ExpectedHeaderToken(column, hasIsoLevel);
                if (string.IsNullOrEmpty(expected) || !TokenEquals(bytes, start, cursor, expected))
                    ThrowHeaderMismatch(column + 1, string.IsNullOrEmpty(expected) ? "unexpected_column" : expected);

                column++;
                if (cursor < length && bytes[cursor] == ',')
                    cursor++;
            }

            int expectedColumns = hasIsoLevel ? 20 : 19;
            if (column != expectedColumns)
                throw new InvalidDataException("Geology profile CSV error " + CsvErrorHeaderSchema + ": header column count mismatch at row 1, column " + (column + 1) + " (header); expected " + expectedColumns + ", got " + column + ".");
        }

        private static string ExpectedHeaderToken(int column, bool hasIsoLevel)
        {
            switch (column)
            {
                case 0: return "name";
                case 1: return "seed";
                case 2: return "resolution";
                case 3: return "variations";
                case 4: return "radius";
                case 5: return "height";
                case 6: return "frequency";
                case 7: return "amplitude";
                case 8: return "ridged";
                case 9: return "voronoi";
                case 10: return "octaves";
                case 11: return "ao_rays";
                case 12: return hasIsoLevel ? "iso_level" : "quality";
                case 13: return hasIsoLevel ? "quality" : "lod0";
                case 14: return hasIsoLevel ? "lod0" : "lod1";
                case 15: return hasIsoLevel ? "lod1" : "lod2";
                case 16: return hasIsoLevel ? "lod2" : "sector_x";
                case 17: return hasIsoLevel ? "sector_x" : "sector_y";
                case 18: return hasIsoLevel ? "sector_y" : "sector_z";
                case 19: return hasIsoLevel ? "sector_z" : string.Empty;
                default: return string.Empty;
            }
        }

        private static void ThrowHeaderMismatch(int column, string expected)
        {
            throw new InvalidDataException("Geology profile CSV error " + CsvErrorHeaderSchema + ": header mismatch at row 1, column " + column + " (" + expected + ").");
        }

        private static bool TokenEquals(byte* bytes, int start, int end, string expected)
        {
            int length = end - start;
            if (length != expected.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                if (bytes[start + i] != expected[i])
                    return false;
            }

            return true;
        }

        private static FixedString64Bytes ReadFixedString(byte* bytes, int length, ref int cursor)
        {
            FixedString64Bytes value = default;
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == ',' || b == '\n' || b == '\r')
                    break;
                if (value.Length < FixedString64Bytes.UTF8MaxLengthInBytes)
                    value.Add(b);
            }

            ConsumeLineBreakRemainder(bytes, length, ref cursor);
            return value;
        }

        private static int ReadInt(byte* bytes, int length, ref int cursor, int row, int column, string field)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            bool negative = false;
            if (cursor < length && bytes[cursor] == '-')
            {
                negative = true;
                cursor++;
            }

            long limit = negative ? 2147483648L : int.MaxValue;
            long value = 0L;
            bool hasDigit = false;
            bool overflow = false;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    break;
                hasDigit = true;
                long digit = b - '0';
                if (value <= (limit - digit) / 10L)
                    value = (value * 10L) + digit;
                else
                    overflow = true;
                cursor++;
            }

            if (!hasDigit || overflow)
                ThrowInvalidCell(row, column, field, overflow ? CsvErrorIntegerOverflow : CsvErrorMalformedCell);

            ConsumeColumnTerminatorOrThrow(bytes, length, ref cursor, row, column, field);
            if (negative)
                return value == 2147483648L ? int.MinValue : -(int)value;
            return (int)value;
        }

        private static uint ReadUInt(byte* bytes, int length, ref int cursor, int row, int column, string field)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            ulong value = 0UL;
            bool hasDigit = false;
            bool overflow = false;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    break;
                hasDigit = true;
                ulong digit = (ulong)(b - '0');
                if (value <= (uint.MaxValue - digit) / 10UL)
                    value = (value * 10UL) + digit;
                else
                    overflow = true;
                cursor++;
            }

            if (!hasDigit || overflow)
                ThrowInvalidCell(row, column, field, overflow ? CsvErrorIntegerOverflow : CsvErrorMalformedCell);

            ConsumeColumnTerminatorOrThrow(bytes, length, ref cursor, row, column, field);
            return (uint)value;
        }

        private static float ReadFloat(byte* bytes, int length, ref int cursor, int row, int column, string field)
        {
            double value = ReadDouble(bytes, length, ref cursor, row, column, field);
            float result = (float)value;
            if (!math.isfinite(result))
                ThrowInvalidCell(row, column, field, CsvErrorNonFiniteFloat);

            return result;
        }

        private static double ReadDouble(byte* bytes, int length, ref int cursor, int row, int column, string field)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            bool negative = false;
            if (cursor < length && bytes[cursor] == '-')
            {
                negative = true;
                cursor++;
            }

            double value = 0d;
            bool hasDigit = false;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    break;
                hasDigit = true;
                value = value * 10d + (b - '0');
                cursor++;
            }

            if (cursor < length && bytes[cursor] == '.')
            {
                cursor++;
                double scale = 0.1d;
                while (cursor < length)
                {
                    byte b = bytes[cursor];
                    if (b < '0' || b > '9')
                        break;
                    hasDigit = true;
                    value += (b - '0') * scale;
                    scale *= 0.1d;
                    cursor++;
                }
            }

            if (!hasDigit)
                ThrowInvalidCell(row, column, field);

            double result = negative ? -value : value;
            if (result == 0d)
                result = 0d;
            if (!math.isfinite(result))
                ThrowInvalidCell(row, column, field, CsvErrorNonFiniteFloat);

            ConsumeColumnTerminatorOrThrow(bytes, length, ref cursor, row, column, field);
            return result;
        }

        private static float RequirePositive(float value, int row, int column, string field)
        {
            if (!math.isfinite(value) || value <= 0f)
                ThrowInvalidCell(row, column, field, CsvErrorNonPositiveValue);

            return value;
        }

        private static void SkipColumnWhitespace(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
        }

        private static void ConsumeColumnTerminatorOrThrow(byte* bytes, int length, ref int cursor, int row, int column, string field)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            if (cursor >= length)
                return;

            byte b = bytes[cursor++];
            if (b == ',' || b == '\n')
                return;
            if (b == '\r')
            {
                ConsumeLineBreakRemainder(bytes, length, ref cursor);
                return;
            }

            ThrowInvalidCell(row, column, field, CsvErrorInvalidTerminator);
        }

        private static void ThrowInvalidCell(int row, int column, string field, int errorCode = CsvErrorMalformedCell)
        {
            throw new InvalidDataException("Geology profile CSV error " + errorCode + ": invalid value at row " + row + ", column " + column + " (" + field + ").");
        }

        private static void SkipLine(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == '\n')
                    break;
            }
        }

        private static void SkipBlank(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == '\n' || bytes[cursor] == '\r'))
                cursor++;
        }

        private static void ConsumeLineBreakRemainder(byte* bytes, int length, ref int cursor)
        {
            if (cursor > 0 && cursor < length && bytes[cursor - 1] == '\r' && bytes[cursor] == '\n')
                cursor++;
        }
    }
}
