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
        public static List<GeologyBakeProfile> LoadProfiles()
        {
            List<GeologyBakeProfile> profiles = new List<GeologyBakeProfile>(16);
            LoadProfiles(profiles);
            return profiles;
        }

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
                using (FileStream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    long length64 = stream.Length;
                    if (length64 <= 0L || length64 > int.MaxValue)
                    {
                        profiles.Add(DefaultProfile());
                        return;
                    }

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

                    bool hasIsoLevel = HeaderHasIsoLevel(ptr, read);
                    if (!HeaderMatchesExpectedSchema(ptr, read, hasIsoLevel))
                        throw new InvalidDataException("Geology profile CSV header mismatch. Expected SHINOBU_208 schema with optional iso_level column.");

                    int cursor = 0;
                    SkipLine(ptr, read, ref cursor);
                    while (cursor < read)
                    {
                        if (TryReadProfile(ptr, read, hasIsoLevel, ref cursor, out GeologyBakeProfile profile))
                            profiles.Add(profile);
                    }
                }
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }

            if (profiles.Count == 0)
                profiles.Add(DefaultProfile());
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

        private static bool TryReadProfile(byte* bytes, int length, bool hasIsoLevel, ref int cursor, out GeologyBakeProfile profile)
        {
            profile = default;
            SkipBlank(bytes, length, ref cursor);
            if (cursor >= length)
                return false;

            profile.Name = ReadFixedString(bytes, length, ref cursor);
            profile.Seed = ReadUInt(bytes, length, ref cursor, 1u);
            profile.Resolution = math.clamp(ReadInt(bytes, length, ref cursor, GeologyForgeConstants.DefaultResolution), GeologyForgeConstants.MinimumResolution, GeologyForgeConstants.MaximumResolution);
            profile.Variations = math.clamp(ReadInt(bytes, length, ref cursor, 1), 1, GeologyForgeConstants.MaximumVariations);
            profile.RadiusMeters = SafePositive(ReadFloat(bytes, length, ref cursor, 2.2f), 2.2f);
            profile.HeightScale = SafePositive(ReadFloat(bytes, length, ref cursor, 1.2f), 1.2f);
            profile.Frequency = SafePositive(ReadFloat(bytes, length, ref cursor, 1f), 1f);
            profile.NoiseAmplitude = math.max(0f, ReadFloat(bytes, length, ref cursor, 0.25f));
            profile.RidgedWeight = math.saturate(ReadFloat(bytes, length, ref cursor, 0.5f));
            profile.VoronoiWeight = math.saturate(ReadFloat(bytes, length, ref cursor, 0.2f));
            profile.Octaves = math.clamp(ReadInt(bytes, length, ref cursor, 4), 1, 8);
            profile.AmbientOcclusionRays = math.clamp(ReadInt(bytes, length, ref cursor, GeologyForgeConstants.DefaultAoRays), 1, GeologyForgeConstants.MaximumAoRays);
            profile.IsoLevel = hasIsoLevel ? math.clamp(ReadFloat(bytes, length, ref cursor, 0f), -0.5f, 0.5f) : 0f;
            profile.GlobalQualityWeight = math.saturate(ReadFloat(bytes, length, ref cursor, 0.5f));
            profile.Lod0Budget = math.max(32, ReadInt(bytes, length, ref cursor, GeologyForgeConstants.Lod0TriangleBudget));
            profile.Lod1Budget = math.max(16, ReadInt(bytes, length, ref cursor, GeologyForgeConstants.Lod1TriangleBudget));
            profile.Lod2Budget = math.max(8, ReadInt(bytes, length, ref cursor, GeologyForgeConstants.Lod2TriangleBudget));
            double sx = ReadFloat(bytes, length, ref cursor, 0f);
            double sy = ReadFloat(bytes, length, ref cursor, 0f);
            double sz = ReadFloat(bytes, length, ref cursor, 0f);
            profile.SectorAup = new double3(sx, sy, sz);
            SkipLine(bytes, length, ref cursor);
            if (profile.Name.Length == 0)
                profile.Name = new FixedString64Bytes("Unnamed_Geology");
            return true;
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

        private static bool HeaderMatchesExpectedSchema(byte* bytes, int length, bool hasIsoLevel)
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
                    return false;

                column++;
                if (cursor < length && bytes[cursor] == ',')
                    cursor++;
            }

            return column == (hasIsoLevel ? 20 : 19);
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

        private static int ReadInt(byte* bytes, int length, ref int cursor, int fallback)
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
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    break;
                hasDigit = true;
                long digit = b - '0';
                value = value <= (limit - digit) / 10L ? (value * 10L) + digit : limit;
                cursor++;
            }

            SkipToNextColumn(bytes, length, ref cursor);
            if (!hasDigit)
                return fallback;
            if (negative)
                return value >= 2147483648L ? int.MinValue : -(int)value;
            return value >= int.MaxValue ? int.MaxValue : (int)value;
        }

        private static uint ReadUInt(byte* bytes, int length, ref int cursor, uint fallback)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            ulong value = 0UL;
            bool hasDigit = false;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    break;
                hasDigit = true;
                ulong digit = (ulong)(b - '0');
                value = value <= (uint.MaxValue - digit) / 10UL ? (value * 10UL) + digit : uint.MaxValue;
                cursor++;
            }

            SkipToNextColumn(bytes, length, ref cursor);
            return hasDigit ? (uint)value : fallback;
        }

        private static float ReadFloat(byte* bytes, int length, ref int cursor, float fallback)
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

            SkipToNextColumn(bytes, length, ref cursor);
            if (!hasDigit)
                return fallback;

            float result = (float)(negative ? -value : value);
            return math.isfinite(result) ? result : fallback;
        }

        private static float SafePositive(float value, float fallback)
        {
            return math.isfinite(value) && value > 0f ? value : fallback;
        }

        private static void SkipColumnWhitespace(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
        }

        private static void SkipToNextColumn(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == ',' || b == '\n')
                    return;
                if (b == '\r')
                {
                    ConsumeLineBreakRemainder(bytes, length, ref cursor);
                    return;
                }
            }
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
