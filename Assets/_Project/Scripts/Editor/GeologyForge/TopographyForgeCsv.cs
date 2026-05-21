#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor.GeologyForge
{
    internal static unsafe class TopographyBiomeCsv
    {
        private const int CsvErrorMalformedCell = 2401001;
        private const int CsvErrorIntegerOverflow = 2401002;
        private const int CsvErrorNonFiniteFloat = 2401003;
        private const int CsvErrorNonPositiveValue = 2401004;
        private const int CsvErrorInvalidTerminator = 2401005;
        private const int CsvErrorColumnCount = 2401006;
        private const int CsvErrorHeaderSchema = 2401007;
        private const int CsvErrorFileSize = 2401008;
        private const int CsvErrorNoRecipes = 2401009;
        private const int MaximumCsvBytes = 2 * 1024 * 1024;
        private const int ExpectedColumns = 19;

        public static void LoadRecipes(ref NativeList<TopographyBiomeRecipeDTO> recipes)
        {
            if (!recipes.IsCreated)
                throw new ArgumentException("Topography biome recipe NativeList is not created.", nameof(recipes));
            recipes.Clear();
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", TopographyForgeConstants.CsvPath));
            if (!File.Exists(path))
            {
                AppendDefaultRecipes(ref recipes);
                return;
            }

            NativeArray<byte> bytes = default;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    long length64 = stream.Length;
                    if (length64 <= 0L || length64 > MaximumCsvBytes)
                        throw new InvalidDataException("Topography biome CSV error " + CsvErrorFileSize + ": invalid file size " + length64 + " bytes.");

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
                        throw new InvalidDataException("Topography biome CSV error " + CsvErrorFileSize + ": unstable file size while reading " + read + " of " + length + " bytes.");

                    int dataOffset = Utf8BomOffset(ptr, read);
                    byte* csv = ptr + dataOffset;
                    int csvLength = read - dataOffset;
                    ValidateHeaderSchema(csv, csvLength);

                    int cursor = 0;
                    int rowIndex = 2;
                    SkipLine(csv, csvLength, ref cursor);
                    while (cursor < csvLength)
                    {
                        if (TryParseRecipe(csv, csvLength, rowIndex, ref cursor, out TopographyBiomeRecipeDTO recipe))
                            recipes.Add(recipe);
                        rowIndex++;
                    }
                }
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }

            if (recipes.Length == 0)
                throw new InvalidDataException("Topography biome CSV error " + CsvErrorNoRecipes + ": existing file contains no data rows.");
        }

        public static void AppendDefaultRecipes(ref NativeList<TopographyBiomeRecipeDTO> recipes)
        {
            recipes.Add(DefaultRecipe("Northern_Ridge_Wastes", new double2(-26000.0, 24000.0), 46000f, 0x9A91E3B1u, 0.00042f, 1.0f, 620f, 0.75f));
            recipes.Add(DefaultRecipe("Equatorial_Canyon_Belt", new double2(8000.0, -3000.0), 52000f, 0x65D2F897u, 0.00028f, 0.82f, 1180f, 0.48f));
            recipes.Add(DefaultRecipe("Southern_Shallow_Plains", new double2(18000.0, -31000.0), 43000f, 0xD4B93C2Du, 0.00018f, 0.55f, 420f, 0.35f));
            recipes.Add(DefaultRecipe("Hadal_Rift_Margins", new double2(-19000.0, -17000.0), 36000f, 0xBA5EBA11u, 0.00034f, 0.93f, 1520f, 0.62f));
        }

        public static TopographyBiomeRecipeDTO DefaultRecipe(
            string name,
            double2 centerAupXZ,
            float radiusMeters,
            uint seed,
            float ridgeFrequency,
            float ridgeAmplitude,
            float warpStrengthMeters,
            float ridgeBlend)
        {
            TopographyBiomeRecipeDTO recipe = default;
            recipe.Name = new FixedString64Bytes(name);
            recipe.CenterAupXZ = centerAupXZ;
            recipe.RadiusMeters = math.max(1f, radiusMeters);
            recipe.Ridge = new FractalParamsDTO
            {
                Frequency = ridgeFrequency,
                Amplitude = ridgeAmplitude,
                Lacunarity = 2.08f,
                Persistence = 0.54f,
                Octaves = 7,
                SeedHash = seed ^ 0x52494447u
            };
            recipe.Warp = new DomainWarpParamsDTO
            {
                Frequency = ridgeFrequency * 0.27f,
                StrengthMeters = warpStrengthMeters,
                Lacunarity = 1.92f,
                Persistence = 0.58f,
                Octaves = 4,
                SeedHash = seed ^ 0x57415250u
            };
            recipe.TerraceSteps = 18f;
            recipe.TerraceStrength = 0.28f;
            recipe.RidgeBlend = math.saturate(ridgeBlend);
            recipe.RiftDepthMeters = 4200f;
            recipe.SeedHash = seed;
            return recipe;
        }

        private static bool TryParseRecipe(byte* bytes, int length, int rowIndex, ref int cursor, out TopographyBiomeRecipeDTO recipe)
        {
            recipe = default;
            SkipBlank(bytes, length, ref cursor);
            if (cursor >= length)
                return false;

            ValidateRowColumnCount(bytes, length, cursor, rowIndex);

            recipe.Name = ConsumeFixedStringCell(bytes, length, ref cursor);
            int column = 2;
            double centerX = ParseDoubleCell(bytes, length, ref cursor, rowIndex, column++, "center_x");
            double centerZ = ParseDoubleCell(bytes, length, ref cursor, rowIndex, column++, "center_z");
            recipe.CenterAupXZ = new double2(centerX, centerZ);
            recipe.RadiusMeters = RequirePositive(ParseFloatCell(bytes, length, ref cursor, rowIndex, column, "radius"), rowIndex, column++, "radius");
            recipe.Ridge.Frequency = RequirePositive(ParseFloatCell(bytes, length, ref cursor, rowIndex, column, "ridge_frequency"), rowIndex, column++, "ridge_frequency");
            recipe.Ridge.Amplitude = math.max(0f, ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "ridge_amplitude"));
            recipe.Ridge.Lacunarity = math.max(1.0001f, ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "ridge_lacunarity"));
            recipe.Ridge.Persistence = math.saturate(ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "ridge_persistence"));
            recipe.Ridge.Octaves = math.clamp(ParseIntCell(bytes, length, ref cursor, rowIndex, column++, "ridge_octaves"), 1, 12);
            recipe.Warp.Frequency = RequirePositive(ParseFloatCell(bytes, length, ref cursor, rowIndex, column, "warp_frequency"), rowIndex, column++, "warp_frequency");
            recipe.Warp.StrengthMeters = math.max(0f, ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "warp_strength"));
            recipe.Warp.Lacunarity = math.max(1.0001f, ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "warp_lacunarity"));
            recipe.Warp.Persistence = math.saturate(ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "warp_persistence"));
            recipe.Warp.Octaves = math.clamp(ParseIntCell(bytes, length, ref cursor, rowIndex, column++, "warp_octaves"), 1, 8);
            recipe.TerraceSteps = math.max(1f, ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "terrace_steps"));
            recipe.TerraceStrength = math.saturate(ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "terrace_strength"));
            recipe.RidgeBlend = math.saturate(ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "ridge_blend"));
            recipe.RiftDepthMeters = math.max(0f, ParseFloatCell(bytes, length, ref cursor, rowIndex, column++, "rift_depth"));
            recipe.SeedHash = ParseUIntCell(bytes, length, ref cursor, rowIndex, column, "seed");
            recipe.Ridge.SeedHash = recipe.SeedHash ^ 0x52494447u;
            recipe.Warp.SeedHash = recipe.SeedHash ^ 0x57415250u;
            if (recipe.Name.Length == 0)
                recipe.Name = new FixedString64Bytes("Unnamed_Topography_Biome");
            return true;
        }

        private static void ValidateRowColumnCount(byte* bytes, int length, int cursor, int rowIndex)
        {
            int columns = 1;
            int scan = cursor;
            while (scan < length && bytes[scan] != '\n' && bytes[scan] != '\r')
            {
                if (bytes[scan] == ',')
                    columns++;
                scan++;
            }

            if (columns != ExpectedColumns)
                throw new InvalidDataException("Topography biome CSV error " + CsvErrorColumnCount + ": row " + rowIndex + " column count mismatch; expected " + ExpectedColumns + ", got " + columns + ".");
        }

        private static int Utf8BomOffset(byte* bytes, int length)
        {
            return length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        }

        private static void ValidateHeaderSchema(byte* bytes, int length)
        {
            int cursor = 0;
            int column = 0;
            while (cursor < length && bytes[cursor] != '\n' && bytes[cursor] != '\r')
            {
                int start = cursor;
                while (cursor < length && bytes[cursor] != ',' && bytes[cursor] != '\n' && bytes[cursor] != '\r')
                    cursor++;

                string expected = ExpectedHeaderToken(column);
                if (string.IsNullOrEmpty(expected) || !TokenEquals(bytes, start, cursor, expected))
                    throw new InvalidDataException("Topography biome CSV error " + CsvErrorHeaderSchema + ": header mismatch at column " + (column + 1) + ".");

                column++;
                if (cursor < length && bytes[cursor] == ',')
                    cursor++;
            }

            if (column != ExpectedColumns)
                throw new InvalidDataException("Topography biome CSV error " + CsvErrorHeaderSchema + ": header column count mismatch; expected " + ExpectedColumns + ", got " + column + ".");
        }

        private static string ExpectedHeaderToken(int column)
        {
            switch (column)
            {
                case 0: return "name";
                case 1: return "center_x";
                case 2: return "center_z";
                case 3: return "radius";
                case 4: return "ridge_frequency";
                case 5: return "ridge_amplitude";
                case 6: return "ridge_lacunarity";
                case 7: return "ridge_persistence";
                case 8: return "ridge_octaves";
                case 9: return "warp_frequency";
                case 10: return "warp_strength";
                case 11: return "warp_lacunarity";
                case 12: return "warp_persistence";
                case 13: return "warp_octaves";
                case 14: return "terrace_steps";
                case 15: return "terrace_strength";
                case 16: return "ridge_blend";
                case 17: return "rift_depth";
                case 18: return "seed";
                default: return string.Empty;
            }
        }

        private static bool TokenEquals(byte* bytes, int start, int end, string expected)
        {
            int length = end - start;
            if (length != expected.Length)
                return false;

            for (int i = 0; i < length; i++)
                if (bytes[start + i] != expected[i])
                    return false;

            return true;
        }

        private static FixedString64Bytes ConsumeFixedStringCell(byte* bytes, int length, ref int cursor)
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

        private static int ParseIntCell(byte* bytes, int length, ref int cursor, int row, int column, string field)
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

        private static uint ParseUIntCell(byte* bytes, int length, ref int cursor, int row, int column, string field)
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

        private static float ParseFloatCell(byte* bytes, int length, ref int cursor, int row, int column, string field)
        {
            double value = ParseDoubleCell(bytes, length, ref cursor, row, column, field);
            float result = (float)value;
            if (!math.isfinite(result))
                ThrowInvalidCell(row, column, field, CsvErrorNonFiniteFloat);

            return result;
        }

        private static double ParseDoubleCell(byte* bytes, int length, ref int cursor, int row, int column, string field)
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
            if (cursor < length && (bytes[cursor] == 'e' || bytes[cursor] == 'E'))
            {
                cursor++;
                bool exponentNegative = false;
                if (cursor < length && (bytes[cursor] == '-' || bytes[cursor] == '+'))
                {
                    exponentNegative = bytes[cursor] == '-';
                    cursor++;
                }

                int exponent = 0;
                bool exponentHasDigit = false;
                bool exponentOverflow = false;
                while (cursor < length)
                {
                    byte b = bytes[cursor];
                    if (b < '0' || b > '9')
                        break;
                    exponentHasDigit = true;
                    int digit = b - '0';
                    if (exponent <= (4096 - digit) / 10)
                        exponent = (exponent * 10) + digit;
                    else
                        exponentOverflow = true;
                    cursor++;
                }

                if (!exponentHasDigit || exponentOverflow)
                    ThrowInvalidCell(row, column, field, exponentOverflow ? CsvErrorIntegerOverflow : CsvErrorMalformedCell);

                int signedExponent = exponentNegative ? -exponent : exponent;
                result *= Math.Pow(10d, signedExponent);
            }

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
            throw new InvalidDataException("Topography biome CSV error " + errorCode + ": invalid value at row " + row + ", column " + column + " (" + field + ").");
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
#endif
