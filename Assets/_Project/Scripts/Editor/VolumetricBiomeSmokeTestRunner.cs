#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.World;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    internal static class VolumetricBiomeSmokeTestRunner
    {
        private const string OutputRelativePath = "Library/VolumetricBiomeSmokeTester.json";

        public static void Run()
        {
            bool passed = false;
            string json;

            try
            {
                passed = VolumetricBiomeSmokeTester.RunHeadlessSmokeTest(
                    out VolumetricBiomeSmokeTester.VolumetricBiomeSmokeReport report);
                json = "{"
                       + "\"tester\":\"VolumetricBiomeSmokeTester\","
                       + "\"status\":\"" + (passed ? "PASS" : "FAIL") + "\","
                       + "\"shallowBiomeId\":" + report.ShallowBiomeId + ","
                       + "\"twilightBiomeId\":" + report.TwilightBiomeId + ","
                       + "\"hadalBiomeId\":" + report.HadalBiomeId + ","
                       + "\"twilightFlags\":" + report.TwilightFlags + ","
                       + "\"stressSampleCount\":" + report.StressSampleCount + ","
                       + "\"stressFailureCount\":" + report.StressFailureCount + ","
                       + "\"sentinelBefore\":" + report.SentinelBefore + ","
                       + "\"sentinelAfter\":" + report.SentinelAfter + ","
                       + "\"sentinelDelta\":" + report.SentinelDelta + ","
                       + "\"packedChecksum\":" + report.PackedChecksum
                       + "}";
            }
            catch (Exception ex)
            {
                json = "{\"tester\":\"VolumetricBiomeSmokeTester\",\"status\":\"EXCEPTION\",\"exception\":\""
                       + EscapeJson(ex.GetType().FullName)
                       + "\",\"message\":\""
                       + EscapeJson(ex.Message)
                       + "\"}";
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, OutputRelativePath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            File.WriteAllText(outputPath, json);
            Debug.Log("[VolumetricBiomeSmokeTestRunner] " + json);

            if (Application.isBatchMode)
                EditorApplication.Exit(passed ? 0 : 1);
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }
    }
}
#endif
