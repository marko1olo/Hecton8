#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only source smoke test for SHINOBU acoustic DSP virtualization invariants.
    /// </summary>
    public static class ShinobuAcousticDspSmokeTester
    {
        private const string ContractsPath = "Assets/_Project/Scripts/Audio/Virtualization/Contracts/AudioVirtualizationContracts.cs";
        private const string JobsPath = "Assets/_Project/Scripts/Audio/Virtualization/AudioVirtualizationJobs.cs";
        private const string SpatialAudioPath = "Assets/_Project/Scripts/SpatialAudioManager.cs";
        private const string AcousticAupPath = "Assets/_Project/Scripts/Core/Contracts/AcousticAup.cs";

        [MenuItem("Hecton8/Audio/Run SHINOBU Acoustic DSP Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string report);
            if (passed)
                Debug.Log(report);
            else
                Debug.LogError(report);
        }

        public static bool Run(out string report)
        {
            int failures = 0;
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("[ShinobuAcousticDspSmokeTester]");

            string contracts = ReadAssetText(ContractsPath, builder, ref failures);
            string jobs = ReadAssetText(JobsPath, builder, ref failures);
            string spatial = ReadAssetText(SpatialAudioPath, builder, ref failures);
            string acousticAup = ReadAssetText(AcousticAupPath, builder, ref failures);

            AssertNotContains(contracts, "Pack = 1", "Virtualization contracts use natural packing", builder, ref failures);
            AssertNotContains(contracts, "Pack=1", "Virtualization contracts do not use compact packing syntax", builder, ref failures);
            AssertNotContains(acousticAup, "Pack = 1", "AcousticAup uses natural packing", builder, ref failures);
            AssertNotContains(acousticAup, "Pack=1", "AcousticAup does not use compact packing syntax", builder, ref failures);
            AssertContains(contracts, "[StructLayout(LayoutKind.Sequential, Size = 48)]", "VirtualVoiceDTO remains exact 48 bytes", builder, ref failures);
            AssertContains(contracts, "public double3 AupMeters;", "VirtualVoiceDTO keeps double3 AUP first", builder, ref failures);
            AssertContains(contracts, "[StructLayout(LayoutKind.Sequential, Size = 16)]", "VirtualVoiceSortKey is one 16-byte cache key", builder, ref failures);
            AssertContains(jobs, "public NativeArray<VirtualVoiceSortKey> SortKeys;", "Burst job sorts compact keys, not full voices", builder, ref failures);
            AssertNotContains(jobs, "SortVoicesDescending(NativeArray<VirtualVoice>", "Burst job does not swap 160-byte voice structs", builder, ref failures);
            AssertContains(spatial, "SpatialAudioVirtualVoiceSortKeyPoolBufferId", "Sort key buffer is GlobalDataVault-backed", builder, ref failures);
            AssertContains(spatial, "TryCompleteVirtualVoiceSort(false)", "FastTick uses non-blocking virtual sort completion", builder, ref failures);
            AssertContains(spatial, "TryCompleteVirtualVoiceSort(true)", "Late/structural handoff keeps explicit blocking boundary", builder, ref failures);
            AssertContains(spatial, "VirtualVoiceBlackBoxFrameCount = 300", "Virtual voice blackbox remains 300 frames", builder, ref failures);
            AssertContains(spatial, "Dump_ACOUSTIC_DSP.bin", "Virtual voice fatal dump path is stable", builder, ref failures);

            builder.Append("STATUS: ");
            builder.AppendLine(failures == 0 ? "PASS" : "FAIL");
            report = builder.ToString();
            return failures == 0;
        }

        private static string ReadAssetText(string assetPath, StringBuilder builder, ref int failures)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string absolutePath = root == null
                ? assetPath
                : Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                AppendFailure(builder, ref failures, "Missing asset: " + assetPath);
                return string.Empty;
            }

            return File.ReadAllText(absolutePath);
        }

        private static void AssertContains(string source, string needle, string message, StringBuilder builder, ref int failures)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) >= 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failures, message + " :: missing `" + needle + "`");
        }

        private static void AssertNotContains(string source, string needle, string message, StringBuilder builder, ref int failures)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) < 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failures, message + " :: found forbidden `" + needle + "`");
        }

        private static void AppendFailure(StringBuilder builder, ref int failures, string message)
        {
            failures++;
            builder.Append("[FAIL] ").AppendLine(message);
        }
    }
}
#endif
