#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor.Build
{
    /// <summary>
    /// Guards static Resources.Load literals against Linux/macOS case-sensitive lookup failures.
    /// </summary>
    internal sealed class CaseSensitiveResourceLoadValidator : IPreprocessBuildWithReport
    {
        private const int MaxReportEntries = 32;
        private static readonly Regex s_resourcesLoadLiteral = new Regex(
            @"Resources\s*\.\s*Load(?:<[^>]+>)?\s*\(\s*""([^""]+)""",
            RegexOptions.Compiled);

        public int callbackOrder => -4625;

        public void OnPreprocessBuild(BuildReport report)
        {
            ValidateOrThrow();
        }

        [MenuItem("HECTON-8/Platform/Validate Resources.Load Case")]
        private static void ValidateFromMenu()
        {
            ValidateOrThrow();
            Debug.Log("[PLATFORM] Resources.Load case validation passed.");
        }

        private static void ValidateOrThrow()
        {
            HashSet<string> exactResourceKeys = new HashSet<string>(StringComparer.Ordinal);
            Dictionary<string, string> canonicalByLowerKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            BuildResourceKeyMap(exactResourceKeys, canonicalByLowerKey);

            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            StringBuilder caseReport = null;
            StringBuilder unresolvedReport = null;
            int caseViolationCount = 0;
            int unresolvedCount = 0;

            for (int i = 0; i < assetPaths.Length; i++)
            {
                string sourcePath = assetPaths[i];
                if (!IsFirstPartySource(sourcePath))
                    continue;

                ScanSourceFile(
                    sourcePath,
                    exactResourceKeys,
                    canonicalByLowerKey,
                    ref caseReport,
                    ref unresolvedReport,
                    ref caseViolationCount,
                    ref unresolvedCount);
            }

            if (unresolvedCount > 0 && unresolvedReport != null)
            {
                Debug.LogWarning("[PLATFORM] Static Resources.Load literals without exact Resources asset proof: " +
                                 unresolvedCount +
                                 "\n" +
                                 unresolvedReport);
            }

            if (caseViolationCount <= 0)
                return;

            string message = "Case-sensitive Resources.Load blocker: " +
                             caseViolationCount +
                             " literal path case mismatch(es) detected.\n" +
                             (caseReport != null ? caseReport.ToString() : string.Empty);
            throw new BuildFailedException(message);
        }

        private static void BuildResourceKeyMap(
            HashSet<string> exactResourceKeys,
            Dictionary<string, string> canonicalByLowerKey)
        {
            string[] assetPaths = AssetDatabase.GetAllAssetPaths();
            for (int i = 0; i < assetPaths.Length; i++)
            {
                string assetPath = assetPaths[i];
                if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                    continue;

                string normalized = assetPath.Replace('\\', '/');
                int resourcesMarker = normalized.IndexOf("/Resources/", StringComparison.Ordinal);
                if (resourcesMarker < 0)
                    continue;

                string key = normalized.Substring(resourcesMarker + "/Resources/".Length);
                int extension = key.LastIndexOf('.');
                if (extension > 0)
                    key = key.Substring(0, extension);

                if (string.IsNullOrEmpty(key))
                    continue;

                exactResourceKeys.Add(key);
                if (!canonicalByLowerKey.ContainsKey(key))
                    canonicalByLowerKey.Add(key, key);
            }
        }

        private static void ScanSourceFile(
            string sourcePath,
            HashSet<string> exactResourceKeys,
            Dictionary<string, string> canonicalByLowerKey,
            ref StringBuilder caseReport,
            ref StringBuilder unresolvedReport,
            ref int caseViolationCount,
            ref int unresolvedCount)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(sourcePath);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                MatchCollection matches = s_resourcesLoadLiteral.Matches(lines[lineIndex]);
                for (int matchIndex = 0; matchIndex < matches.Count; matchIndex++)
                {
                    string requestedKey = matches[matchIndex].Groups[1].Value.Replace('\\', '/');
                    if (exactResourceKeys.Contains(requestedKey))
                        continue;

                    if (canonicalByLowerKey.TryGetValue(requestedKey, out string actualKey))
                    {
                        caseViolationCount++;
                        if (caseViolationCount <= MaxReportEntries)
                        {
                            if (caseReport == null)
                                caseReport = new StringBuilder(1024);

                            caseReport.Append("- ")
                                .Append(sourcePath)
                                .Append(':')
                                .Append(lineIndex + 1)
                                .Append(" requests `")
                                .Append(requestedKey)
                                .Append("`, actual Resources key is `")
                                .Append(actualKey)
                                .Append("`.\n");
                        }

                        continue;
                    }

                    unresolvedCount++;
                    if (unresolvedCount <= MaxReportEntries)
                    {
                        if (unresolvedReport == null)
                            unresolvedReport = new StringBuilder(1024);

                        unresolvedReport.Append("- ")
                            .Append(sourcePath)
                            .Append(':')
                            .Append(lineIndex + 1)
                            .Append(" requests `")
                            .Append(requestedKey)
                            .Append("`.\n");
                    }
                }
            }
        }

        private static bool IsFirstPartySource(string assetPath)
        {
            return assetPath.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) &&
                   (assetPath.StartsWith("Assets/_Project/Scripts/", StringComparison.Ordinal) ||
                    assetPath.StartsWith("Assets/_Project/Editor/", StringComparison.Ordinal));
        }
    }
}
#endif
