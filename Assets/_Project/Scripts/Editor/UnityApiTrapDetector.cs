#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Static editor scan for Unity APIs that allocate hidden managed arrays.
    /// </summary>
    [InitializeOnLoad]
    internal static class UnityApiTrapDetector
    {
        private const string SourceRoot = "Assets/_Project/Scripts";
        private const int MaxConsoleReports = 32;

        private static readonly TrapRule[] _rules =
        {
            new TrapRule("Input.touches", "Input.touches allocates. Use Input.GetTouch(index) with touchCount."),
            new TrapRule(".materials", "Renderer.materials allocates. Use sharedMaterials or cached MaterialPropertyBlock lanes."),
            new TrapRule(".material", "Renderer.material leaks/clones. Use sharedMaterial or cached MaterialPropertyBlock lanes."),
            new TrapRule(".vertices", "Mesh.vertices allocates. Use Mesh.GetVertices(cachedList).")
        };

        static UnityApiTrapDetector()
        {
            EditorApplication.delayCall -= ScanAfterReload;
            EditorApplication.delayCall += ScanAfterReload;
        }

        [MenuItem("Hecton-8/Compliance/Scan Unity API Traps")]
        private static void ScanFromMenu()
        {
            int violations = Scan(reportToConsole: true);
            SessionState.SetInt("UnityApiTrapDetector.Violations", violations);
        }

        private static void ScanAfterReload()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                EditorApplication.delayCall -= ScanAfterReload;
                EditorApplication.delayCall += ScanAfterReload;
                return;
            }

            SessionState.SetInt("UnityApiTrapDetector.Violations", Scan(reportToConsole: false));
        }

        private static int Scan(bool reportToConsole)
        {
            if (!Directory.Exists(SourceRoot))
                return 0;

            List<string> paths = new List<string>(Directory.EnumerateFiles(SourceRoot, "*.cs", SearchOption.AllDirectories));
            paths.Sort(StringComparer.Ordinal);

            int violations = 0;
            int reported = 0;
            for (int pathIndex = 0; pathIndex < paths.Count; pathIndex++)
            {
                string path = paths[pathIndex];
                if (!IsRuntimeScriptPath(path))
                    continue;

                string[] lines = ReadAllLinesSafe(path);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string codeLine = StripLineComment(lines[lineIndex]);
                    for (int ruleIndex = 0; ruleIndex < _rules.Length; ruleIndex++)
                    {
                        TrapRule rule = _rules[ruleIndex];
                        if (codeLine.IndexOf(rule.Needle, StringComparison.Ordinal) < 0)
                            continue;

                        if (IsWhitelistedTrapHit(codeLine, rule.Needle))
                            continue;

                        violations++;
                        if (reportToConsole && reported < MaxConsoleReports)
                        {
                            Debug.LogError(
                                "[UnityApiTrapDetector] " +
                                path +
                                ":" +
                                (lineIndex + 1) +
                                " " +
                                rule.Message);
                            reported++;
                        }
                    }
                }
            }

            return violations;
        }

        private static bool IsRuntimeScriptPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(SourceRoot + "/", StringComparison.Ordinal) &&
                   normalized.IndexOf("/Editor/", StringComparison.Ordinal) < 0;
        }

        private static string[] ReadAllLinesSafe(string path)
        {
            try
            {
                return File.ReadAllLines(path);
            }
            catch (IOException)
            {
                return Array.Empty<string>();
            }
            catch (UnauthorizedAccessException)
            {
                return Array.Empty<string>();
            }
        }

        private static string StripLineComment(string line)
        {
            int commentIndex = line.IndexOf("//", StringComparison.Ordinal);
            return commentIndex >= 0 ? line.Substring(0, commentIndex) : line;
        }

        private static bool IsWhitelistedTrapHit(string codeLine, string needle)
        {
            if (!string.Equals(needle, ".material", StringComparison.Ordinal) &&
                !string.Equals(needle, ".materials", StringComparison.Ordinal))
            {
                return false;
            }

            if (string.Equals(needle, ".material", StringComparison.Ordinal) &&
                codeLine.IndexOf(".materials", StringComparison.Ordinal) >= 0)
            {
                return true;
            }

            return codeLine.IndexOf(".sharedMaterial", StringComparison.Ordinal) >= 0 ||
                   codeLine.IndexOf(".sharedMaterials", StringComparison.Ordinal) >= 0;
        }

        private readonly struct TrapRule
        {
            public readonly string Needle;
            public readonly string Message;

            public TrapRule(string needle, string message)
            {
                Needle = needle;
                Message = message;
            }
        }
    }
}
#endif
