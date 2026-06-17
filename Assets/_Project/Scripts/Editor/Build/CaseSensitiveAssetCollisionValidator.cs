#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Fails builds when asset paths collide on case-insensitive workstations but split on Linux/macOS.
    /// </summary>
    internal sealed class CaseSensitiveAssetCollisionValidator : IPreprocessBuildWithReport
    {
        private const int MaxReportEntries = 24;

        public int callbackOrder => -4630;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateOrThrow();
        }

        [MenuItem("Hecton8/Platform/Validate Case-Sensitive Asset Paths")]
        private static void ValidateFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("[PLATFORM] Case-sensitive asset path validation passed.");
        }

        private static void ValidateOrThrow()
        {
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            Dictionary<string, string> canonicalByPath = new Dictionary<string, string>(assetPaths.Length, StringComparer.OrdinalIgnoreCase);
            StringBuilder report = null;
            int violationCount = 0;

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string path = assetPaths[i];
                if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal))
                    continue;

                string normalized = path.Replace('\\', '/');
                if (!canonicalByPath.TryGetValue(normalized, out string existing))
                {
                    canonicalByPath.Add(normalized, normalized);
                    continue;
                }

                if (string.Equals(existing, normalized, StringComparison.Ordinal))
                    continue;

                violationCount++;
                if (violationCount <= MaxReportEntries)
                {
                    if (report == null)
                        report = new StringBuilder(512);

                    report.Append("- ")
                        .Append(existing)
                        .Append(" <-> ")
                        .Append(normalized)
                        .Append('\n');
                }
            }

            if (violationCount <= 0)
                return;

            string message = "Case-sensitive asset path blocker: " +
                             violationCount +
                             " path collision(s) detected.\n" +
                             (report != null ? report.ToString() : string.Empty);
            throw new BuildFailedException(message);
        }
    }
}
#endif
