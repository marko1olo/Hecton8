using System.IO;
using System.Runtime.InteropServices;
using Hecton8.World.OfflineHadalTrenchBaker;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.World.OfflineHadalTrenchBaker.Editor
{
    [StructLayout(LayoutKind.Explicit, Size = 128)]
    public struct TectonicRiftProfileDTO
    {
        [FieldOffset(0)] public double3 SectorOriginAUP;
        [FieldOffset(24)] public FixedString64Bytes Name;
        [FieldOffset(88)] public uint Seed;
        [FieldOffset(92)] public float VoronoiCellSizeMeters;
        [FieldOffset(96)] public float TrenchWidthMeters;
        [FieldOffset(100)] public float TrenchDepthMeters;
        [FieldOffset(104)] public float NoiseIntensity;
        [FieldOffset(108)] public float NoiseFrequency;
        [FieldOffset(112)] public float GlobalQualityWeight;
        [FieldOffset(116)] public uint _pad0;
        [FieldOffset(120)] public ulong _pad1;
    }

    internal static unsafe class TectonicRiftProfileCsvParser
    {
        public const string CsvPath = "Assets/_SourceData/HadalTrenches/tectonic_rift_profiles.csv";
        private const int MaximumCsvBytes = 4 * 1024 * 1024;
        private const int MaximumProfiles = 256;
        private const int ExpectedColumns = 11;
        private const double AupBoundMeters = 100000.0d;

        public static void LoadProfiles(NativeList<TectonicRiftProfileDTO> profiles)
        {
            profiles.Clear();
            string path = Path.Combine(Directory.GetCurrentDirectory(), CsvPath);
            if (!File.Exists(path))
            {
                AddProfile(profiles, DefaultProfile(), 0);
                return;
            }

            NativeArray<byte> bytes = default;
            try
            {
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    long length64 = stream.Length;
                    if (length64 <= 0L || length64 > MaximumCsvBytes)
                        throw new InvalidDataException("Tectonic rift CSV invalid file size " + length64 + " bytes.");

                    int length = (int)length64;
                    bytes = new NativeArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                    for (int read = 0; read < length; read++)
                    {
                        int value = stream.ReadByte();
                        if (value < 0)
                            throw new InvalidDataException("Tectonic rift CSV unstable read " + read + " of " + length + " bytes.");
                        ptr[read] = (byte)value;
                    }

                    int offset = Utf8BomOffset(ptr, length);
                    byte* csv = ptr + offset;
                    int csvLength = length - offset;
                    ValidateHeader(csv, csvLength);
                    int cursor = 0;
                    SkipLine(csv, csvLength, ref cursor);
                    int row = 2;
                    while (cursor < csvLength)
                    {
                        if (TryReadProfile(csv, csvLength, row, ref cursor, out TectonicRiftProfileDTO profile))
                            AddProfile(profiles, profile, row);
                        row++;
                    }
                }
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }

            if (profiles.Length == 0)
                AddProfile(profiles, DefaultProfile(), 0);
        }

        public static TectonicRiftProfileDTO DefaultProfile()
        {
            TectonicRiftProfileDTO profile = default;
            profile.Name = new FixedString64Bytes("Mariana_Drop");
            profile.Seed = 0x5348494Eu;
            profile.VoronoiCellSizeMeters = 3200f;
            profile.TrenchWidthMeters = 420f;
            profile.TrenchDepthMeters = 5000f;
            profile.NoiseIntensity = 96f;
            profile.NoiseFrequency = 0.0025f;
            profile.GlobalQualityWeight = 0.7f;
            profile.SectorOriginAUP = new double3(-50000.0d, -6200.0d, -50000.0d);
            profile._pad0 = 0u;
            profile._pad1 = 0ul;
            return profile;
        }

        public static void ApplyToConfig(in TectonicRiftProfileDTO profile, ref HadalTrenchBakeConfigDTO config)
        {
            config.Seed = profile.Seed == 0u ? config.Seed : profile.Seed;
            config.VoronoiCellSizeMeters = math.max(config.VoxelSizeMeters * 16f, profile.VoronoiCellSizeMeters);
            config.DefaultWidthMeters = math.max(config.VoxelSizeMeters * 4f, profile.TrenchWidthMeters);
            config.DefaultDepthMeters = math.max(config.VoxelSizeMeters * 8f, profile.TrenchDepthMeters);
            config.NoiseIntensity = math.clamp(profile.NoiseIntensity, 0f, 512f);
            config.NoiseFrequency = math.clamp(profile.NoiseFrequency, 0.00001f, 0.05f);
            config.GlobalQualityWeight = math.saturate(profile.GlobalQualityWeight);
            config.SectorOriginAUP = profile.SectorOriginAUP;
        }

        private static bool TryReadProfile(byte* bytes, int length, int row, ref int cursor, out TectonicRiftProfileDTO profile)
        {
            profile = default;
            SkipBlank(bytes, length, ref cursor);
            if (cursor >= length)
                return false;

            ValidateColumnCount(bytes, length, cursor, row);
            profile.Name = ReadFixedString(bytes, length, ref cursor);
            int column = 2;
            profile.Seed = ReadUInt(bytes, length, ref cursor, row, column++, "seed");
            profile.VoronoiCellSizeMeters = RequirePositive(ReadFloat(bytes, length, ref cursor, row, column++, "cell_size"), row, column - 1, "cell_size");
            profile.TrenchWidthMeters = RequirePositive(ReadFloat(bytes, length, ref cursor, row, column++, "width"), row, column - 1, "width");
            profile.TrenchDepthMeters = RequirePositive(ReadFloat(bytes, length, ref cursor, row, column++, "depth"), row, column - 1, "depth");
            profile.NoiseIntensity = math.max(0f, ReadFloat(bytes, length, ref cursor, row, column++, "noise"));
            profile.NoiseFrequency = RequirePositive(ReadFloat(bytes, length, ref cursor, row, column++, "frequency"), row, column - 1, "frequency");
            profile.GlobalQualityWeight = math.saturate(ReadFloat(bytes, length, ref cursor, row, column++, "quality"));
            double sx = ReadDouble(bytes, length, ref cursor, row, column++, "sector_x");
            double sy = ReadDouble(bytes, length, ref cursor, row, column++, "sector_y");
            double sz = ReadDouble(bytes, length, ref cursor, row, column, "sector_z");
            profile.SectorOriginAUP = new double3(sx, sy, sz);
            ValidateAupBounds(profile.SectorOriginAUP, row);
            if (profile.Name.Length == 0)
                profile.Name = new FixedString64Bytes("Unnamed_Rift");
            return true;
        }

        private static void AddProfile(NativeList<TectonicRiftProfileDTO> profiles, in TectonicRiftProfileDTO profile, int row)
        {
            if (profiles.Length >= MaximumProfiles)
                throw new InvalidDataException("Tectonic rift CSV profile count exceeds " + MaximumProfiles + " rows at source row " + row + ".");

            if (profiles.Length >= profiles.Capacity)
            {
                int capacity = math.max(1, profiles.Capacity);
                while (capacity <= profiles.Length && capacity < MaximumProfiles)
                    capacity = math.min(MaximumProfiles, capacity << 1);
                profiles.Capacity = capacity;
            }

            profiles.AddNoResize(profile);
        }

        private static void ValidateAupBounds(double3 aup, int row)
        {
            if (math.abs(aup.x) > AupBoundMeters ||
                math.abs(aup.y) > AupBoundMeters ||
                math.abs(aup.z) > AupBoundMeters)
            {
                throw new InvalidDataException("Tectonic rift CSV row " + row + " sector AUP exceeds +/-100000m.");
            }
        }

        private static void ValidateHeader(byte* bytes, int length)
        {
            int cursor = 0;
            for (int column = 0; column < ExpectedColumns; column++)
            {
                int start = cursor;
                while (cursor < length && bytes[cursor] != ',' && bytes[cursor] != '\n' && bytes[cursor] != '\r')
                    cursor++;

                if (!TokenEquals(bytes, start, cursor, ExpectedHeader(column)))
                    throw new InvalidDataException("Tectonic rift CSV header mismatch at column " + (column + 1) + ".");

                if (column < ExpectedColumns - 1)
                {
                    if (cursor >= length || bytes[cursor] != ',')
                        throw new InvalidDataException("Tectonic rift CSV header terminated early.");
                    cursor++;
                }
            }
        }

        private static string ExpectedHeader(int column)
        {
            switch (column)
            {
                case 0: return "name";
                case 1: return "seed";
                case 2: return "cell_size";
                case 3: return "width";
                case 4: return "depth";
                case 5: return "noise";
                case 6: return "frequency";
                case 7: return "quality";
                case 8: return "sector_x";
                case 9: return "sector_y";
                case 10: return "sector_z";
                default: return string.Empty;
            }
        }

        private static void ValidateColumnCount(byte* bytes, int length, int cursor, int row)
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
                throw new InvalidDataException("Tectonic rift CSV row " + row + " column count " + columns + ", expected " + ExpectedColumns + ".");
        }

        private static int Utf8BomOffset(byte* bytes, int length)
        {
            return length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
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

        private static uint ReadUInt(byte* bytes, int length, ref int cursor, int row, int column, string field)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            uint radix = 10u;
            if (cursor + 1 < length && bytes[cursor] == '0' && (bytes[cursor + 1] == 'x' || bytes[cursor + 1] == 'X'))
            {
                radix = 16u;
                cursor += 2;
            }

            ulong value = 0ul;
            bool digit = false;
            while (cursor < length)
            {
                int d = HexDigit(bytes[cursor]);
                if (d < 0 || (uint)d >= radix)
                    break;
                digit = true;
                value = value * radix + (uint)d;
                if (value > uint.MaxValue)
                    ThrowInvalid(row, column, field);
                cursor++;
            }

            if (!digit)
                ThrowInvalid(row, column, field);
            ConsumeColumnTerminator(bytes, length, ref cursor, row, column, field);
            return (uint)value;
        }

        private static float ReadFloat(byte* bytes, int length, ref int cursor, int row, int column, string field)
        {
            double value = ReadDouble(bytes, length, ref cursor, row, column, field);
            float result = (float)value;
            if (!math.isfinite(result))
                ThrowInvalid(row, column, field);
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
            while (cursor < length && bytes[cursor] >= '0' && bytes[cursor] <= '9')
            {
                hasDigit = true;
                value = value * 10d + (bytes[cursor] - '0');
                cursor++;
            }

            if (cursor < length && bytes[cursor] == '.')
            {
                cursor++;
                double scale = 0.1d;
                while (cursor < length && bytes[cursor] >= '0' && bytes[cursor] <= '9')
                {
                    hasDigit = true;
                    value += (bytes[cursor] - '0') * scale;
                    scale *= 0.1d;
                    cursor++;
                }
            }

            if (!hasDigit)
                ThrowInvalid(row, column, field);

            double result = negative ? -value : value;
            if (!math.isfinite(result))
                ThrowInvalid(row, column, field);
            ConsumeColumnTerminator(bytes, length, ref cursor, row, column, field);
            return result;
        }

        private static float RequirePositive(float value, int row, int column, string field)
        {
            if (!math.isfinite(value) || value <= 0f)
                ThrowInvalid(row, column, field);
            return value;
        }

        private static int HexDigit(byte b)
        {
            if (b >= '0' && b <= '9')
                return b - '0';
            if (b >= 'a' && b <= 'f')
                return b - 'a' + 10;
            if (b >= 'A' && b <= 'F')
                return b - 'A' + 10;
            return -1;
        }

        private static bool TokenEquals(byte* bytes, int start, int end, string expected)
        {
            int length = end - start;
            if (length != expected.Length)
                return false;

            for (int i = 0; i < length; i++)
            {
                byte b = bytes[start + i];
                if (b >= 'A' && b <= 'Z')
                    b = (byte)(b + 32);
                if (b != expected[i])
                    return false;
            }

            return true;
        }

        private static void SkipColumnWhitespace(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
        }

        private static void ConsumeColumnTerminator(byte* bytes, int length, ref int cursor, int row, int column, string field)
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

            ThrowInvalid(row, column, field);
        }

        private static void ThrowInvalid(int row, int column, string field)
        {
            throw new InvalidDataException("Tectonic rift CSV invalid value at row " + row + ", column " + column + " (" + field + ").");
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
