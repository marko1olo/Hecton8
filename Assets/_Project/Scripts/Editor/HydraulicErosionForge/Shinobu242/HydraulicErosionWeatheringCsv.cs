#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor.HydraulicErosionForge
{
    internal static unsafe class HydraulicErosionWeatheringCsv
    {
        private const string NativeMemoryOwner = nameof(HydraulicErosionWeatheringCsv);
        private const string CsvBytesLabel = "WeatheringCsvBytes";
        private const int MaximumCsvBytes = 2 * 1024 * 1024;

        public static void LoadProfiles(List<WeatheringProfileDTO> profiles)
        {
            if (profiles == null)
                throw new ArgumentNullException(nameof(profiles));

            profiles.Clear();
            string path = Path.Combine(ProjectRoot(), HydraulicErosionForgeConstants.WeatheringCsvPath);
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
                    if (length64 <= 0L || length64 > MaximumCsvBytes)
                        throw new InvalidDataException("terrain_weathering_profiles.csv invalid size.");

                    int length = (int)length64;
                    bytes = AllocateTrackedArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory, CsvBytesLabel, NativeAllocationLifetime.Temp);
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

                    if (read != length)
                        throw new InvalidDataException("terrain_weathering_profiles.csv unstable read.");

                    int offset = Utf8BomOffset(ptr, read);
                    byte* csv = ptr + offset;
                    int csvLength = read - offset;
                    ValidateHeader(csv, csvLength);
                    int cursor = 0;
                    SkipLine(csv, csvLength, ref cursor);
                    int row = 2;
                    while (cursor < csvLength)
                    {
                        if (TryReadProfile(csv, csvLength, row, ref cursor, out WeatheringProfileDTO profile))
                            profiles.Add(profile);
                        row++;
                    }
                }
            }
            finally
            {
                DisposeTrackedArray(ref bytes);
            }

            if (profiles.Count == 0)
                profiles.Add(DefaultProfile());
        }

        public static WeatheringProfileDTO DefaultProfile()
        {
            WeatheringProfileDTO profile = default;
            profile.Name = new FixedString64Bytes("Abyssal_Basalt_Rain");
            profile.RainRate = 1f;
            profile.EvaporationSpeed = 0.015f;
            profile.SedimentCapacity = 4f;
            profile.ErosionAggressiveness = 0.35f;
            profile.RegionBlendWeight = 1f;
            profile.SeedSalt = 0x53483234u;
            return profile;
        }

        private static bool TryReadProfile(byte* bytes, int length, int row, ref int cursor, out WeatheringProfileDTO profile)
        {
            profile = default;
            SkipBlank(bytes, length, ref cursor);
            if (cursor >= length)
                return false;

            profile.Name = ReadFixedString(bytes, length, ref cursor);
            profile.RainRate = RequireFinitePositive(ReadFloat(bytes, length, row, 2, ref cursor), row, 2);
            profile.EvaporationSpeed = math.saturate(ReadFloat(bytes, length, row, 3, ref cursor));
            profile.SedimentCapacity = RequireFinitePositive(ReadFloat(bytes, length, row, 4, ref cursor), row, 4);
            profile.ErosionAggressiveness = math.saturate(ReadFloat(bytes, length, row, 5, ref cursor));
            profile.RegionBlendWeight = math.saturate(ReadFloat(bytes, length, row, 6, ref cursor));
            profile.SeedSalt = ReadUInt(bytes, length, row, 7, ref cursor);
            if (profile.Name.Length == 0)
                profile.Name = new FixedString64Bytes("Unnamed_Weathering");
            return true;
        }

        private static void ValidateHeader(byte* bytes, int length)
        {
            int cursor = 0;
            RequireToken(bytes, length, ref cursor, "name", 1);
            RequireToken(bytes, length, ref cursor, "rain_rate", 2);
            RequireToken(bytes, length, ref cursor, "evaporation_speed", 3);
            RequireToken(bytes, length, ref cursor, "sediment_capacity", 4);
            RequireToken(bytes, length, ref cursor, "erosion_aggressiveness", 5);
            RequireToken(bytes, length, ref cursor, "region_blend_weight", 6);
            RequireToken(bytes, length, ref cursor, "seed_salt", 7);
        }

        private static void RequireToken(byte* bytes, int length, ref int cursor, string expected, int column)
        {
            int start = cursor;
            while (cursor < length && bytes[cursor] != ',' && bytes[cursor] != '\n' && bytes[cursor] != '\r')
                cursor++;
            int tokenLength = cursor - start;
            if (tokenLength != expected.Length)
                throw new InvalidDataException("terrain_weathering_profiles.csv header mismatch column " + column + ".");
            for (int i = 0; i < tokenLength; i++)
            {
                if (bytes[start + i] != expected[i])
                    throw new InvalidDataException("terrain_weathering_profiles.csv header mismatch column " + column + ".");
            }

            ConsumeTerminator(bytes, length, ref cursor, 1, column);
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

        private static float ReadFloat(byte* bytes, int length, int row, int column, ref int cursor)
        {
            double value = ReadDouble(bytes, length, row, column, ref cursor);
            float result = (float)value;
            if (!math.isfinite(result))
                throw new InvalidDataException("terrain_weathering_profiles.csv non-finite at row " + row + ", column " + column + ".");
            return result;
        }

        private static double ReadDouble(byte* bytes, int length, int row, int column, ref int cursor)
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
                throw new InvalidDataException("terrain_weathering_profiles.csv malformed number at row " + row + ", column " + column + ".");

            ConsumeTerminator(bytes, length, ref cursor, row, column);
            return negative ? -value : value;
        }

        private static uint ReadUInt(byte* bytes, int length, int row, int column, ref int cursor)
        {
            SkipColumnWhitespace(bytes, length, ref cursor);
            ulong value = 0UL;
            bool hasDigit = false;
            while (cursor < length && bytes[cursor] >= '0' && bytes[cursor] <= '9')
            {
                hasDigit = true;
                ulong digit = (ulong)(bytes[cursor] - '0');
                if (value > (uint.MaxValue - digit) / 10UL)
                    throw new InvalidDataException("terrain_weathering_profiles.csv uint overflow at row " + row + ", column " + column + ".");
                value = value * 10UL + digit;
                cursor++;
            }

            if (!hasDigit)
                throw new InvalidDataException("terrain_weathering_profiles.csv malformed uint at row " + row + ", column " + column + ".");

            ConsumeTerminator(bytes, length, ref cursor, row, column);
            return (uint)value;
        }

        private static float RequireFinitePositive(float value, int row, int column)
        {
            if (!math.isfinite(value) || value <= 0f)
                throw new InvalidDataException("terrain_weathering_profiles.csv expected positive value at row " + row + ", column " + column + ".");
            return value;
        }

        private static void ConsumeTerminator(byte* bytes, int length, ref int cursor, int row, int column)
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

            throw new InvalidDataException("terrain_weathering_profiles.csv invalid terminator at row " + row + ", column " + column + ".");
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

        private static void SkipColumnWhitespace(byte* bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
        }

        private static void ConsumeLineBreakRemainder(byte* bytes, int length, ref int cursor)
        {
            if (cursor > 0 && cursor < length && bytes[cursor - 1] == '\r' && bytes[cursor] == '\n')
                cursor++;
        }

        private static int Utf8BomOffset(byte* bytes, int length)
        {
            return length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF ? 3 : 0;
        }

        private static NativeArray<T> AllocateTrackedArray<T>(
            int length,
            Allocator allocator,
            NativeArrayOptions options,
            string label,
            NativeAllocationLifetime lifetime) where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, allocator, options);
            if (!array.IsCreated)
                throw new InvalidOperationException("[HydraulicErosionWeatheringCsv] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[HydraulicErosionWeatheringCsv] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static void DisposeTrackedArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            try
            {
                NativeMemorySentinel.UnregisterNativeArray(array);
            }
            finally
            {
                array.Dispose();
                array = default;
            }
        }

        private static string ProjectRoot()
        {
            return Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
        }
    }
}
#endif
