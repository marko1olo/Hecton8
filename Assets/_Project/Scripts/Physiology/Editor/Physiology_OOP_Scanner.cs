#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    internal static class Physiology_OOP_Scanner
    {
        private const string Summary = "OOP Physiology Triggers Purged";
        private const string SharedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_272.json";
        private static readonly string[] s_roots =
        {
            "Assets/_Project/Scripts/Physiology",
            "Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs"
        };

        private static readonly string[] s_forbidden =
        {
            "UnityEngine.Random",
            "HectonPlayerHealth",
            ".TakeDamage(",
            "ReceiveDamage(",
            "CrushDepth",
            "pressureDamage",
            "oxygenDamage",
            "depthDamage"
        };

        [MenuItem("Hecton8/Physiology/Run Physiology OOP Scanner")]
        private static void RunMenu()
        {
            int findingCount = RunStaticScan(Application.dataPath);
            if (findingCount == 0)
                Debug.Log("[SHINOBU_272] OOP Physiology Triggers Purged");
            else
                Debug.LogError("[SHINOBU_272] Physiology OOP scanner found forbidden triggers: " + findingCount);
        }

        internal static int RunStaticScan(string assetsPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(assetsPath, ".."));
            List<Finding> findings = new List<Finding>(16);
            for (int i = 0; i < s_roots.Length; i++)
                ScanRoot(projectRoot, s_roots[i], findings);

            string report = BuildReport(findings);
            WriteText(Path.Combine(projectRoot, SidecarReportPath), report);
            UpsertSharedReport(Path.Combine(projectRoot, SharedReportPath), BuildSharedSection(findings));
            return findings.Count;
        }

        private static void ScanRoot(string projectRoot, string relativeRoot, List<Finding> findings)
        {
            string root = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(root))
            {
                ScanFile(projectRoot, root, findings);
                return;
            }

            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string normalized = files[i].Replace('\\', '/');
                if (normalized.EndsWith("/Editor/Physiology_OOP_Scanner.cs", StringComparison.Ordinal))
                    continue;
                if (normalized.Contains("/Physiology/Editor/"))
                    continue;
                ScanFile(projectRoot, files[i], findings);
            }
        }

        private static void ScanFile(string projectRoot, string path, List<Finding> findings)
        {
            string relative = MakeRelative(projectRoot, path);
            string[] lines = File.ReadAllLines(path);
            bool isPhysiologyRuntime = relative.Replace('\\', '/').Contains("/Physiology/");
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int patternIndex = 0; patternIndex < s_forbidden.Length; patternIndex++)
                {
                    string pattern = s_forbidden[patternIndex];
                    if (line.IndexOf(pattern, StringComparison.Ordinal) < 0)
                        continue;
                    if (!isPhysiologyRuntime && pattern != "UnityEngine.Random" && pattern != "CrushDepth" && pattern != "pressureDamage" && pattern != "oxygenDamage" && pattern != "depthDamage")
                        continue;

                    findings.Add(new Finding(relative, lineIndex + 1, pattern));
                }
            }
        }

        private static string BuildReport(List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_272\",");
            builder.AppendLine("  \"scanner\": \"Physiology_OOP_Scanner\",");
            builder.Append("  \"summary\": \"").Append(Summary).AppendLine("\",");
            builder.Append("  \"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.AppendLine("  \"generatedWithoutBuild\": true,");
            builder.AppendLine("  \"findings\": [");
            AppendFindings(builder, findings, 4);
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildSharedSection(List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("  \"shinobu272PhysiologyOopScanner\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_272\",");
            builder.AppendLine("    \"scanner\": \"Physiology_OOP_Scanner\",");
            builder.Append("    \"summary\": \"").Append(Summary).AppendLine("\",");
            builder.Append("    \"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.AppendLine("    \"findings\": [");
            AppendFindings(builder, findings, 6);
            builder.AppendLine("    ]");
            builder.Append("  }");
            return builder.ToString();
        }

        private static void AppendFindings(StringBuilder builder, List<Finding> findings, int indent)
        {
            string pad = new string(' ', indent);
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                builder.Append(pad).Append("{ \"path\": \"").Append(EscapeJson(finding.Path)).Append("\", \"line\": ")
                    .Append(finding.Line).Append(", \"pattern\": \"").Append(EscapeJson(finding.Pattern)).Append("\" }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }
        }

        private static void UpsertSharedReport(string path, string section)
        {
            if (!File.Exists(path))
            {
                WriteText(path, "{\n" + section + "\n}\n");
                return;
            }

            string existing = File.ReadAllText(path);
            int key = existing.IndexOf("\"shinobu272PhysiologyOopScanner\"", StringComparison.Ordinal);
            if (key >= 0)
                return;

            int insert = existing.LastIndexOf('}');
            if (insert < 0)
            {
                WriteText(path, "{\n" + section + "\n}\n");
                return;
            }

            string prefix = existing.Substring(0, insert).TrimEnd();
            string suffix = existing.Substring(insert);
            string separator = prefix.EndsWith("{", StringComparison.Ordinal) ? "\n" : ",\n";
            WriteText(path, prefix + separator + section + "\n" + suffix);
        }

        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string MakeRelative(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? path : path + Path.DirectorySeparatorChar;
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private readonly struct Finding
        {
            public readonly string Path;
            public readonly int Line;
            public readonly string Pattern;

            public Finding(string path, int line, string pattern)
            {
                Path = path;
                Line = line;
                Pattern = pattern;
            }
        }
    }
}
#endif
