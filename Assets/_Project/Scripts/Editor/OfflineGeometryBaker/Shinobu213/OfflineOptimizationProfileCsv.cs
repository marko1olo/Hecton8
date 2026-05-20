#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor.OfflineGeometry
{
    internal static class OfflineOptimizationProfileCsv
    {
        internal static unsafe List<OfflineBakeSettings> LoadProfiles()
        {
            var profiles = new List<OfflineBakeSettings>(16);
            string projectRoot = Application.dataPath.Substring(0, Application.dataPath.Length - "/Assets".Length);
            string fullPath = Path.Combine(projectRoot, OfflineGeometryBakerConstants.ProfileCsvPath);
            if (!File.Exists(fullPath))
            {
                profiles.Add(DefaultSettings());
                return profiles;
            }

            NativeArray<byte> bytes = default;
            int length = 0;
            try
            {
                using (FileStream stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (stream.Length <= 0L || stream.Length > int.MaxValue)
                    {
                        profiles.Add(DefaultSettings());
                        return profiles;
                    }

                    length = (int)stream.Length;
                    // COLD ALLOC: NativeArray<byte>[csvLength] - editor CSV staging for LOD optimization profiles - owner: OfflineOptimizationProfileCsv
                    bytes = new NativeArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    byte* ptr = (byte*)NativeArrayUnsafeUtility.GetUnsafePtr(bytes);
                    Span<byte> span = new Span<byte>(ptr, length);
                    int totalRead = 0;
                    while (totalRead < length)
                    {
                        int read = stream.Read(span.Slice(totalRead));
                        if (read <= 0)
                            break;
                        totalRead += read;
                    }

                    length = totalRead;
                }

                int cursor = 0;
                SkipLine(bytes, length, ref cursor);
                while (cursor < length)
                {
                    SkipBlank(bytes, length, ref cursor);
                    if (cursor >= length)
                        break;

                    OfflineBakeSettings settings = DefaultSettings();
                    settings.ProfileName = ReadFixedString(bytes, length, ref cursor, settings.ProfileName);
                    settings.Lod1Ratio = math.saturate(ReadFloat(bytes, length, ref cursor, settings.Lod1Ratio));
                    settings.Lod2Ratio = math.saturate(ReadFloat(bytes, length, ref cursor, settings.Lod2Ratio));
                    settings.PrimitiveTolerance = math.max(0.001f, ReadFloat(bytes, length, ref cursor, settings.PrimitiveTolerance));
                    settings.ConvexHullVertexLimit = math.clamp(ReadInt(bytes, length, ref cursor, settings.ConvexHullVertexLimit), 8, OfflineGeometryBakerConstants.MaxHullVertexCount);
                    settings.Lod0HardBudget = math.max(256, ReadInt(bytes, length, ref cursor, settings.Lod0HardBudget));
                    settings.GlobalQualityWeight = math.saturate(ReadFloat(bytes, length, ref cursor, settings.GlobalQualityWeight));
                    settings.DepthMeters = math.max(0f, ReadFloat(bytes, length, ref cursor, settings.DepthMeters));
                    SkipLine(bytes, length, ref cursor);
                    profiles.Add(settings);
                }
            }
            finally
            {
                if (bytes.IsCreated)
                    bytes.Dispose();
            }

            if (profiles.Count == 0)
                profiles.Add(DefaultSettings());

            return profiles;
        }

        internal static OfflineBakeSettings DefaultSettings()
        {
            OfflineBakeSettings settings = default;
            settings.ProfileName = new FixedString64Bytes("Default_Static_Geometry");
            settings.Lod1Ratio = OfflineGeometryBakerConstants.DefaultLod1Ratio;
            settings.Lod2Ratio = OfflineGeometryBakerConstants.DefaultLod2Ratio;
            settings.PrimitiveTolerance = 0.18f;
            settings.ConvexHullVertexLimit = 32;
            settings.Lod0HardBudget = OfflineGeometryBakerConstants.HardLod0WarningTriangles;
            settings.GlobalQualityWeight = 0.5f;
            settings.DepthMeters = 0f;
            return settings;
        }

        private static FixedString64Bytes ReadFixedString(NativeArray<byte> bytes, int length, ref int cursor, FixedString64Bytes fallback)
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

            if (value.Length == 0)
                value = fallback;
            return value;
        }

        private static int ReadInt(NativeArray<byte> bytes, int length, ref int cursor, int fallback)
        {
            float value = ReadFloat(bytes, length, ref cursor, fallback);
            return math.isfinite(value) ? (int)math.round(value) : fallback;
        }

        private static float ReadFloat(NativeArray<byte> bytes, int length, ref int cursor, float fallback)
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

        private static void SkipColumnWhitespace(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == ' ' || bytes[cursor] == '\t'))
                cursor++;
        }

        private static void SkipToNextColumn(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == ',' || b == '\n')
                    return;
                if (b == '\r')
                {
                    if (cursor < length && bytes[cursor] == '\n')
                        cursor++;
                    return;
                }
            }
        }

        private static void SkipLine(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length)
            {
                byte b = bytes[cursor++];
                if (b == '\n')
                    break;
            }
        }

        private static void SkipBlank(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == '\n' || bytes[cursor] == '\r'))
                cursor++;
        }
    }
}
#endif
