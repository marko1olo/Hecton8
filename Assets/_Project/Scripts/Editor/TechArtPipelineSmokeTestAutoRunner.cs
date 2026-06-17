#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// File-flagged editor runner for tech-art smoke validation under batchmode lock contention.
    /// </summary>
    public static class TechArtPipelineSmokeTestAutoRunner
    {
        private const string RequestRelativePath = "Temp/TechArtPipelineSmokeTester.request";
        private const string OutputRelativePath = "Library/TechArtPipelineSmokeTester.json";
        private const string MirrorOutputRelativePath = "CodexArtifacts/2026-05-05_TECHART_OMEGA_UNITY_AUTORUN.json";

        private static bool s_HasRun;

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            if (s_HasRun)
                return;

            if (!File.Exists(ResolveProjectPath(RequestRelativePath)))
                return;

            s_HasRun = true;
            EditorApplication.delayCall += RunRequestedSmokeTest;
        }

        private static void RunRequestedSmokeTest()
        {
            string requestPath = ResolveProjectPath(RequestRelativePath);
            if (!File.Exists(requestPath))
                return;

            bool passed = false;
            string json = string.Empty;
            try
            {
                passed = TechArtPipelineSmokeTester.Run(out json);
                WriteText(OutputRelativePath, json);
                WriteText(MirrorOutputRelativePath, json);
            }
            finally
            {
                File.Delete(requestPath);
                if (Application.isBatchMode && !passed)
                    EditorApplication.Exit(1);
            }
        }

        private static void WriteText(string relativePath, string text)
        {
            string outputPath = ResolveProjectPath(relativePath);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(outputPath, text, new UTF8Encoding(false));
        }

        private static string ResolveProjectPath(string relativePath)
        {
            return Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")), relativePath);
        }
    }
}
#endif
