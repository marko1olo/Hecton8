#if UNITY_EDITOR
using System;
using System.IO;
using Hecton8.Dev;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class SpaceEngine098TerrainSmokeTestRunner
    {
        private const string OutputRelativePath = "Library/SpaceEngine098TerrainSmokeTester.json";

        public static void Run()
        {
            bool passed = false;
            string json;

            try
            {
                passed = SpaceEngine098TerrainSmokeTester.Run(out json);
            }
            catch (Exception ex)
            {
                json = "{\"status\":\"EXCEPTION\",\"exception\":\"" +
                       EscapeJson(ex.GetType().FullName) +
                       "\",\"message\":\"" +
                       EscapeJson(ex.Message) +
                       "\"}";
            }

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string outputPath = Path.Combine(projectRoot, OutputRelativePath);
            string outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            File.WriteAllText(outputPath, json);
            Debug.Log("[SpaceEngine098TerrainSmokeTestRunner] " + json);

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
