#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build;

namespace Hecton8.Editor
{
    public static class Arm64LayoutSourceFixer
    {
        private const string ReportPath = "Docs/Reports/ARM64_LAYOUT_SOURCE_FIXER_REPORT.txt";
        private static readonly string[] Roots =
        {
            "Assets/_Project/Scripts/Core",
            "Assets/_Project/Scripts/Physics"
        };

        private static readonly Regex ExplicitPackRegex = new Regex(
            @"StructLayout\(LayoutKind\.Explicit(?<body>[^\)]*?),\s*Pack\s*=\s*\d+(?<tail>[^\)]*)\)",
            RegexOptions.Compiled);

        private static readonly Regex ExplicitLeadingPackRegex = new Regex(
            @"StructLayout\(LayoutKind\.Explicit,\s*Pack\s*=\s*\d+\s*,\s*(?<tail>[^\)]*)\)",
            RegexOptions.Compiled);

        private static readonly Regex SequentialCandidateRegex = new Regex(
            @"\[StructLayout\(LayoutKind\.Sequential(?<layout>[^\)]*)\)\]\s*(?:\[[^\]]+\]\s*)*(?:public|internal|private)?\s*(?:readonly\s+)?(?:partial\s+)?struct\s+(?<name>\w+)",
            RegexOptions.Compiled | RegexOptions.Singleline);

        [MenuItem("Hecton8/Diagnostics/Run ARM64 Layout Source Fixer CLI")]
        public static void RunCli()
        {
            string report = RunInternal(strict: true);
            if (report.IndexOf("[BLOCKED]", StringComparison.Ordinal) >= 0)
                throw new BuildFailedException(report);

            UnityEngine.Debug.Log(report);
        }

        [MenuItem("Hecton8/Diagnostics/Run ARM64 Layout Source Fixer Report")]
        public static void RunReportOnly()
        {
            UnityEngine.Debug.Log(RunInternal(strict: false));
        }

        private static string RunInternal(bool strict)
        {
            int filesVisited = 0;
            int packAttributesRemoved = 0;
            int blockedSequential = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("ARM64_LAYOUT_SOURCE_FIXER");
            report.AppendLine("Roots: Core, Physics");

            for (int rootIndex = 0; rootIndex < Roots.Length; rootIndex++)
            {
                string root = Roots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string path = files[fileIndex];
                    filesVisited++;
                    string original = File.ReadAllText(path);
                    string fixedText = RemoveExplicitPack(original, ref packAttributesRemoved);
                    if (!string.Equals(original, fixedText, StringComparison.Ordinal))
                        File.WriteAllText(path, fixedText);

                    blockedSequential += ReportSequentialCandidates(path, fixedText, report);
                }
            }

            report.Insert(0, "FilesVisited=" + filesVisited + "\nPackAttributesRemoved=" + packAttributesRemoved + "\n");
            report.AppendLine("BlockedSequentialCandidates=" + blockedSequential);
            report.AppendLine(strict ? "Mode=STRICT" : "Mode=REPORT_ONLY");

            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(ReportPath, report.ToString());
            return report.ToString();
        }

        private static string RemoveExplicitPack(string source, ref int packAttributesRemoved)
        {
            string result = ExplicitLeadingPackRegex.Replace(source, match =>
            {
                packAttributesRemoved++;
                return "StructLayout(LayoutKind.Explicit, " + match.Groups["tail"].Value + ")";
            });

            result = ExplicitPackRegex.Replace(result, match =>
            {
                packAttributesRemoved++;
                return "StructLayout(LayoutKind.Explicit" + match.Groups["body"].Value + match.Groups["tail"].Value + ")";
            });

            return result;
        }

        private static int ReportSequentialCandidates(string path, string source, StringBuilder report)
        {
            int blocked = 0;
            MatchCollection matches = SequentialCandidateRegex.Matches(source);
            for (int i = 0; i < matches.Count; i++)
            {
                string name = matches[i].Groups["name"].Value;
                if (!IsDtoName(name))
                    continue;

                blocked++;
                report.Append("[BLOCKED] ");
                report.Append(path);
                report.Append(" :: ");
                report.Append(name);
                report.AppendLine(" uses LayoutKind.Sequential. Manual Explicit rewrite required; unsafe regex offset synthesis rejected.");
            }

            return blocked;
        }

        private static bool IsDtoName(string name)
        {
            return name.IndexOf("DTO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Payload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Signal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Telemetry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("State", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
#endif
