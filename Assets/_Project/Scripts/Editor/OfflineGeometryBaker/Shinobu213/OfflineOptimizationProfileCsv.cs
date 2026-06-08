#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using Hecton8.Core;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Editor.OfflineGeometry
{
    internal static class OfflineOptimizationProfileCsv
    {
        private const string NativeMemoryOwner = nameof(OfflineOptimizationProfileCsv);
        private const string CsvBytesLabel = "OptimizationProfileCsvBytes";
        private const int MaximumProfileCsvBytes = 1048576;
        private const string ExpectedHeader = "profile_name,lod1_ratio,lod2_ratio,primitive_tolerance,convex_hull_vertex_limit,lod0_hard_budget,global_quality_weight,depth_meters";

        internal static unsafe List<OfflineBakeSettings> LoadProfiles()
        {
            var profiles = new List<OfflineBakeSettings>(16);
            string projectRoot = ResolveProjectRoot();
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
                    if (stream.Length <= 0L || stream.Length > MaximumProfileCsvBytes)
                    {
                        profiles.Add(DefaultSettings());
                        return profiles;
                    }

                    int expectedLength = (int)stream.Length;
                    length = expectedLength;
                    // COLD ALLOC: NativeArray<byte>[csvLength] - editor CSV staging for LOD optimization profiles - owner: OfflineOptimizationProfileCsv
                    bytes = AllocateTrackedArray<byte>(length, Allocator.Temp, NativeArrayOptions.UninitializedMemory, CsvBytesLabel, NativeAllocationLifetime.Temp);
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

                    if (totalRead != expectedLength)
                    {
                        profiles.Add(DefaultSettings());
                        return profiles;
                    }
                }

                int cursor = 0;
                SkipUtf8Bom(bytes, length, ref cursor);
                if (!TryConsumeExpectedHeader(bytes, length, ref cursor))
                {
                    profiles.Add(DefaultSettings());
                    return profiles;
                }

                while (cursor < length)
                {
                    SkipBlank(bytes, length, ref cursor);
                    if (cursor >= length)
                        break;

                    if (!TryReadProfileRow(bytes, length, ref cursor, out OfflineBakeSettings settings))
                    {
                        profiles.Clear();
                        profiles.Add(DefaultSettings());
                        return profiles;
                    }

                    profiles.Add(settings);
                }
            }
            finally
            {
                DisposeTrackedArray(ref bytes);
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

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            if (!string.IsNullOrEmpty(dataPath))
            {
                string normalized = dataPath.Replace('\\', '/');
                const string assetsSuffix = "/Assets";
                if (normalized.EndsWith(assetsSuffix, StringComparison.Ordinal) && normalized.Length > assetsSuffix.Length)
                    return normalized.Substring(0, normalized.Length - assetsSuffix.Length);
            }

            return Directory.GetCurrentDirectory();
        }

        private static void SkipUtf8Bom(NativeArray<byte> bytes, int length, ref int cursor)
        {
            if (length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
                cursor = 3;
        }

        private static bool TryConsumeExpectedHeader(NativeArray<byte> bytes, int length, ref int cursor)
        {
            int start = cursor;
            for (int i = 0; i < ExpectedHeader.Length; i++)
            {
                if (cursor >= length || bytes[cursor++] != (byte)ExpectedHeader[i])
                {
                    cursor = start;
                    return false;
                }
            }

            if (cursor >= length)
                return true;

            byte b = bytes[cursor++];
            if (b == '\n')
                return true;

            if (b == '\r')
            {
                if (cursor < length && bytes[cursor] == '\n')
                    cursor++;
                return true;
            }

            cursor = start;
            return false;
        }

        private static bool TryReadProfileRow(NativeArray<byte> bytes, int length, ref int cursor, out OfflineBakeSettings settings)
        {
            settings = DefaultSettings();
            if (!TryReadCell(bytes, length, ref cursor, out int start, out int end, out byte terminator) ||
                terminator != ',' ||
                !TryParseFixedString(bytes, start, end, out settings.ProfileName))
                return false;

            if (!TryReadCell(bytes, length, ref cursor, out start, out end, out terminator) ||
                terminator != ',' ||
                !TryParseFloat(bytes, start, end, out settings.Lod1Ratio))
                return false;
            settings.Lod1Ratio = math.saturate(settings.Lod1Ratio);

            if (!TryReadCell(bytes, length, ref cursor, out start, out end, out terminator) ||
                terminator != ',' ||
                !TryParseFloat(bytes, start, end, out settings.Lod2Ratio))
                return false;
            settings.Lod2Ratio = math.saturate(settings.Lod2Ratio);

            if (!TryReadCell(bytes, length, ref cursor, out start, out end, out terminator) ||
                terminator != ',' ||
                !TryParseFloat(bytes, start, end, out settings.PrimitiveTolerance))
                return false;
            settings.PrimitiveTolerance = math.max(0.001f, settings.PrimitiveTolerance);

            if (!TryReadCell(bytes, length, ref cursor, out start, out end, out terminator) ||
                terminator != ',' ||
                !TryParseInt(bytes, start, end, out settings.ConvexHullVertexLimit))
                return false;
            settings.ConvexHullVertexLimit = math.clamp(settings.ConvexHullVertexLimit, OfflineGeometryBakerConstants.MinHullVertexCount, OfflineGeometryBakerConstants.MaxHullVertexCount);

            if (!TryReadCell(bytes, length, ref cursor, out start, out end, out terminator) ||
                terminator != ',' ||
                !TryParseInt(bytes, start, end, out settings.Lod0HardBudget))
                return false;
            settings.Lod0HardBudget = math.max(256, settings.Lod0HardBudget);

            if (!TryReadCell(bytes, length, ref cursor, out start, out end, out terminator) ||
                terminator != ',' ||
                !TryParseFloat(bytes, start, end, out settings.GlobalQualityWeight))
                return false;
            settings.GlobalQualityWeight = math.saturate(settings.GlobalQualityWeight);

            if (!TryReadCell(bytes, length, ref cursor, out start, out end, out terminator) ||
                terminator == ',' ||
                !TryParseFloat(bytes, start, end, out settings.DepthMeters))
                return false;
            settings.DepthMeters = math.max(0f, settings.DepthMeters);
            return true;
        }

        private static bool TryReadCell(NativeArray<byte> bytes, int length, ref int cursor, out int start, out int end, out byte terminator)
        {
            start = cursor;
            end = cursor;
            terminator = 0;
            while (cursor < length)
            {
                byte b = bytes[cursor];
                if (b == ',' || b == '\n' || b == '\r')
                {
                    end = cursor;
                    terminator = b;
                    cursor++;
                    if (b == '\r' && cursor < length && bytes[cursor] == '\n')
                        cursor++;
                    return true;
                }

                cursor++;
            }

            end = cursor;
            return true;
        }

        private static bool TryParseFixedString(NativeArray<byte> bytes, int start, int end, out FixedString64Bytes value)
        {
            value = default;
            Trim(bytes, ref start, ref end);
            if (start >= end)
                return false;

            for (int i = start; i < end; i++)
            {
                if (value.Length >= FixedString64Bytes.UTF8MaxLengthInBytes)
                    return false;
                value.Add(bytes[i]);
            }

            return value.Length > 0;
        }

        private static bool TryParseInt(NativeArray<byte> bytes, int start, int end, out int value)
        {
            value = 0;
            Trim(bytes, ref start, ref end);
            if (start >= end)
                return false;

            bool negative = false;
            int cursor = start;
            if (bytes[cursor] == '+' || bytes[cursor] == '-')
            {
                negative = bytes[cursor] == '-';
                cursor++;
                if (cursor >= end)
                    return false;
            }

            long result = 0L;
            for (; cursor < end; cursor++)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    return false;
                result = result * 10L + (b - '0');
                if ((!negative && result > int.MaxValue) || (negative && -result < int.MinValue))
                    return false;
            }

            value = negative ? (int)-result : (int)result;
            return true;
        }

        private static bool TryParseFloat(NativeArray<byte> bytes, int start, int end, out float value)
        {
            value = 0f;
            Trim(bytes, ref start, ref end);
            if (start >= end)
                return false;

            bool negative = false;
            int cursor = start;
            if (bytes[cursor] == '+' || bytes[cursor] == '-')
            {
                negative = bytes[cursor] == '-';
                cursor++;
                if (cursor >= end)
                    return false;
            }

            double result = 0d;
            bool hasDigit = false;
            while (cursor < end)
            {
                byte b = bytes[cursor];
                if (b < '0' || b > '9')
                    break;
                hasDigit = true;
                result = result * 10d + (b - '0');
                cursor++;
            }

            if (cursor < end && bytes[cursor] == '.')
            {
                cursor++;
                double scale = 0.1d;
                while (cursor < end)
                {
                    byte b = bytes[cursor];
                    if (b < '0' || b > '9')
                        break;
                    hasDigit = true;
                    result += (b - '0') * scale;
                    scale *= 0.1d;
                    cursor++;
                }
            }

            if (!hasDigit || cursor != end)
                return false;

            value = (float)(negative ? -result : result);
            return math.isfinite(value);
        }

        private static void Trim(NativeArray<byte> bytes, ref int start, ref int end)
        {
            while (start < end && (bytes[start] == ' ' || bytes[start] == '\t'))
                start++;
            while (end > start && (bytes[end - 1] == ' ' || bytes[end - 1] == '\t'))
                end--;
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
                throw new InvalidOperationException("[OfflineOptimizationProfileCsv] NativeArray allocation failed for " + label + ".");

            try
            {
                int sentinelId = NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, lifetime);
                if (sentinelId <= 0)
                    throw new InvalidOperationException("[OfflineOptimizationProfileCsv] NativeMemorySentinel rejected NativeArray registration for " + label + ".");
            }
            catch
            {
                array.Dispose();
                throw;
            }

            return array;
        }

        private static unsafe void DisposeTrackedArray<T>(ref NativeArray<T> array) where T : struct
        {
            if (!array.IsCreated)
                return;

            void* trackedPointer = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(array);
            System.Exception nativeSentinelCleanupException0 = null;

            try
            {
                NativeMemorySentinel.UnregisterPointer(trackedPointer);
            }
            catch (System.Exception nativeSentinelException0)
            {
                nativeSentinelCleanupException0 = nativeSentinelException0;
            }

            try
            {
                array.Dispose();
            }
            catch (System.Exception nativeSentinelException0)
            {
                if (nativeSentinelCleanupException0 == null)
                    nativeSentinelCleanupException0 = nativeSentinelException0;
            }
            finally
            {
                array = default;
            }

            if (nativeSentinelCleanupException0 != null)
                throw nativeSentinelCleanupException0;
        }

        private static void SkipBlank(NativeArray<byte> bytes, int length, ref int cursor)
        {
            while (cursor < length && (bytes[cursor] == '\n' || bytes[cursor] == '\r'))
                cursor++;
        }
    }
}
#endif
