#if UNITY_EDITOR
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.SaveSystem.Editor
{
    internal static class OOP_VoxelPagingFuzzer1312
    {
        private const int DirectorySlotCount = 252;
        private const int DefaultSampleCount = 2_000_000;
        private const ulong DefaultSeed = 0x1312D17EC70B5EEDUL;
        private const string ReportRelativePath = "Docs/Reports/VOXEL_PAGING_FUZZER_1312.json";

        [MenuItem("Hecton8/Save/OOP Voxel Paging Fuzzer 1312")]
        private static void RunMenu()
        {
            string report = Run(DefaultSampleCount, DefaultSeed);
            UnityEngine.Debug.Log($"[OOP 1312] Voxel paging fuzzer report written: {report}");
        }

        internal static string Run(int sampleCount, ulong seed)
        {
            int safeSamples = Math.Max(DirectorySlotCount, sampleCount);
            int[] counts = new int[DirectorySlotCount];
            ulong value = seed;
            int uniqueSlots = 0;

            Stopwatch stopwatch = Stopwatch.StartNew();
            for (int i = 0; i < safeSamples; i++)
            {
                value = Next(value);
                int slot = ResolveDirectorySlot(unchecked((long)value));
                if (counts[slot] == 0)
                    uniqueSlots++;
                counts[slot]++;
            }

            stopwatch.Stop();
            int min = int.MaxValue;
            int max = int.MinValue;
            long sum = 0L;
            for (int i = 0; i < counts.Length; i++)
            {
                int count = counts[i];
                min = Math.Min(min, count);
                max = Math.Max(max, count);
                sum += count;
            }

            double mean = (double)sum / DirectorySlotCount;
            double variance = 0d;
            for (int i = 0; i < counts.Length; i++)
            {
                double delta = counts[i] - mean;
                variance += delta * delta;
            }

            double stdDev = Math.Sqrt(variance / DirectorySlotCount);
            double relativeSpread = mean > 0d ? (max - min) / mean : 0d;
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string reportPath = Path.Combine(projectRoot, ReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            StringBuilder builder = new StringBuilder(1536);
            builder.AppendLine("{");
            AppendJson(builder, "agent", "1312", comma: true);
            AppendJson(builder, "samples", safeSamples, comma: true);
            AppendJson(builder, "directorySlots", DirectorySlotCount, comma: true);
            AppendJson(builder, "uniqueSlots", uniqueSlots, comma: true);
            AppendJson(builder, "allSlotsReachable", uniqueSlots == DirectorySlotCount, comma: true);
            AppendJson(builder, "slotCollisionFreePossible", false, comma: true);
            AppendJson(builder, "slotCollisionFreeReason", "10000 or more sector hashes cannot map injectively into 252 directory slots.", comma: true);
            AppendJson(builder, "minBucket", min, comma: true);
            AppendJson(builder, "maxBucket", max, comma: true);
            AppendJson(builder, "meanBucket", mean, comma: true);
            AppendJson(builder, "stdDevBucket", stdDev, comma: true);
            AppendJson(builder, "relativeSpread", relativeSpread, comma: true);
            AppendJson(builder, "elapsedMilliseconds", stopwatch.Elapsed.TotalMilliseconds, comma: false);
            builder.AppendLine("}");

            File.WriteAllText(reportPath, builder.ToString());
            return reportPath;
        }

        private static int ResolveDirectorySlot(long sectorHash)
        {
            ulong mixed = unchecked((ulong)sectorHash);
            mixed ^= mixed >> 33;
            mixed *= 0xff51afd7ed558ccdUL;
            mixed ^= mixed >> 33;
            return (int)(mixed % (ulong)DirectorySlotCount);
        }

        private static ulong Next(ulong value)
        {
            value ^= value >> 12;
            value ^= value << 25;
            value ^= value >> 27;
            return value * 0x2545F4914F6CDD1DUL;
        }

        private static void AppendJson(StringBuilder builder, string key, string value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": \"").Append(value.Replace("\\", "\\\\").Replace("\"", "\\\"")).Append('"');
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string key, bool value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value ? "true" : "false");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string key, int value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string key, double value, bool comma)
        {
            builder.Append("  \"").Append(key).Append("\": ").Append(value.ToString("R", CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }
    }
}
#endif
