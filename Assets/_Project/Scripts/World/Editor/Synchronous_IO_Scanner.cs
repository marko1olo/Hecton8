#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.World.Editor
{
    public static class Synchronous_IO_Scanner
    {
        private const string AgentId = "SHINOBU_245";
        private const string ReportRelativePath = "Docs/Reports/WORLD_OPTIMIZATION_REPORT.json";

        [MenuItem("Tools/Hecton8/World/Run Synchronous IO Scanner")]
        public static void RunMenu()
        {
            int findingCount = RunAndWriteReport();
            Debug.Log("Synchronous_IO_Scanner findings: " + findingCount + ". Report: " + ReportRelativePath);
        }

        public static int RunAndWriteReport()
        {
            string projectRoot = ResolveProjectRoot();
            string worldRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "World");
            List<SyncIoFinding> findings = new List<SyncIoFinding>(64);
            int scannedFiles = 0;
            int allowedFileStreams = 0;

            if (Directory.Exists(worldRoot))
            {
                foreach (string file in Directory.EnumerateFiles(worldRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string normalized = NormalizePath(file);
                    if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    scannedFiles++;
                    ScanFile(projectRoot, file, normalized, findings, ref allowedFileStreams);
                }
            }

            string reportPath = Path.Combine(projectRoot, ReportRelativePath);
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(reportPath, BuildReport(projectRoot, scannedFiles, allowedFileStreams, findings));
            AssetDatabase.Refresh();
            return findings.Count;
        }

        private static void ScanFile(
            string projectRoot,
            string file,
            string normalized,
            List<SyncIoFinding> findings,
            ref int allowedFileStreams)
        {
            string[] lines = File.ReadAllLines(file);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                string pattern = ResolveForbiddenPattern(line);
                if (pattern.Length == 0)
                    continue;

                string context = RequiresStatementContext(pattern)
                    ? BuildStatementContext(lines, lineIndex)
                    : line;
                if (IsAllowedColdOrBackgroundIo(pattern, normalized, context))
                {
                    allowedFileStreams++;
                    continue;
                }

                SyncIoFinding finding = default;
                finding.Path = ToProjectRelative(projectRoot, file);
                finding.Line = lineIndex + 1;
                finding.Pattern = pattern;
                finding.Owner = normalized.EndsWith("/TerrainChunkPagerRuntime.cs", StringComparison.OrdinalIgnoreCase)
                    ? AgentId
                    : "EXTERNAL_WORLD_DEBT";
                finding.Context = TrimForJson(line);
                findings.Add(finding);
            }
        }

        private static string ResolveForbiddenPattern(string line)
        {
            if (line.IndexOf("File.ReadAllBytes", StringComparison.Ordinal) >= 0) return "File.ReadAllBytes";
            if (line.IndexOf("File.ReadAllText", StringComparison.Ordinal) >= 0) return "File.ReadAllText";
            if (line.IndexOf("File.OpenRead", StringComparison.Ordinal) >= 0) return "File.OpenRead";
            if (line.IndexOf("File.Exists", StringComparison.Ordinal) >= 0) return "File.Exists";
            if (line.IndexOf("Directory.Exists", StringComparison.Ordinal) >= 0) return "Directory.Exists";
            if (line.IndexOf("File.Open(", StringComparison.Ordinal) >= 0) return "File.Open";
            if (line.IndexOf("File.Create(", StringComparison.Ordinal) >= 0) return "File.Create";
            if (line.IndexOf("File.Delete(", StringComparison.Ordinal) >= 0) return "File.Delete";
            if (line.IndexOf("File.Move(", StringComparison.Ordinal) >= 0) return "File.Move";
            if (line.IndexOf("new StreamReader", StringComparison.Ordinal) >= 0) return "new StreamReader";
            if (line.IndexOf("new StreamWriter", StringComparison.Ordinal) >= 0) return "new StreamWriter";
            if (line.IndexOf("new FileInfo", StringComparison.Ordinal) >= 0) return "new FileInfo";
            if (line.IndexOf("new DirectoryInfo", StringComparison.Ordinal) >= 0) return "new DirectoryInfo";
            if (line.IndexOf("new FileStream", StringComparison.Ordinal) >= 0) return "new FileStream";
            if (line.IndexOf("stream.Read(", StringComparison.Ordinal) >= 0) return "stream.Read";
            if (line.IndexOf("stream.Write(", StringComparison.Ordinal) >= 0) return "stream.Write";
            if (line.IndexOf("Task.Run", StringComparison.Ordinal) >= 0) return "Task.Run";
            if (line.IndexOf("JsonUtility.FromJson", StringComparison.Ordinal) >= 0) return "JsonUtility.FromJson";
            if (line.IndexOf("IEnumerator", StringComparison.Ordinal) >= 0) return "IEnumerator";
            if (line.IndexOf("yield return", StringComparison.Ordinal) >= 0) return "yield return";
            return string.Empty;
        }

        private static bool RequiresStatementContext(string pattern)
        {
            return pattern == "new FileStream" ||
                   pattern == "File.Open" ||
                   pattern == "File.Create" ||
                   pattern == "stream.Read" ||
                   pattern == "stream.Write";
        }

        private static bool IsAllowedColdOrBackgroundIo(string pattern, string normalizedPath, string context)
        {
            if (context.IndexOf("BACKGROUND_WORKER_IO_SHINOBU_245", StringComparison.Ordinal) >= 0)
                return true;
            if (context.IndexOf("COLD_BOOT_CONFIG_READ_SHINOBU_245", StringComparison.Ordinal) >= 0)
                return true;
            if (context.IndexOf("BLACKBOX_DUMP_SHINOBU_245", StringComparison.Ordinal) >= 0)
                return true;
            if (context.IndexOf("FileOptions.Asynchronous", StringComparison.Ordinal) >= 0)
                return true;

            if (pattern != "new FileStream")
                return false;

            string lowerPath = normalizedPath.ToLowerInvariant();
            string lowerContext = context.ToLowerInvariant();
            if (context.IndexOf("FileMode.Create", StringComparison.Ordinal) >= 0 &&
                (lowerPath.IndexOf("blackbox", StringComparison.Ordinal) >= 0 ||
                 lowerPath.IndexOf("dump", StringComparison.Ordinal) >= 0 ||
                 lowerContext.IndexOf("dump", StringComparison.Ordinal) >= 0 ||
                 lowerContext.IndexOf("telemetry", StringComparison.Ordinal) >= 0))
            {
                return true;
            }

            return false;
        }

        private static string BuildReport(
            string projectRoot,
            int scannedFiles,
            int allowedFileStreams,
            List<SyncIoFinding> findings)
        {
            int pagerOwnedFindings = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Owner == AgentId)
                    pagerOwnedFindings++;
            }

            StringBuilder builder = new StringBuilder(8192);
            builder.Append("{\n");
            AppendJson(builder, "agent", AgentId, 1).Append(",\n");
            AppendJson(builder, "scanner", nameof(Synchronous_IO_Scanner), 1).Append(",\n");
            AppendJson(builder, "summary", pagerOwnedFindings == 0
                ? "Blocking File I/O Eradicated for SHINOBU_245 TerrainChunkPagerRuntime"
                : "Blocking File I/O still present in SHINOBU_245 TerrainChunkPagerRuntime", 1).Append(",\n");
            AppendJson(builder, "scope", "Assets/_Project/Scripts/World excluding Editor folders", 1).Append(",\n");
            builder.Append("  \"scannedFiles\": ").Append(scannedFiles).Append(",\n");
            builder.Append("  \"allowedColdOrBackgroundFileStreams\": ").Append(allowedFileStreams).Append(",\n");
            builder.Append("  \"pagerOwnedFindingCount\": ").Append(pagerOwnedFindings).Append(",\n");
            builder.Append("  \"externalWorldDebtFindingCount\": ").Append(findings.Count - pagerOwnedFindings).Append(",\n");
            AppendJson(builder, "projectRoot", projectRoot, 1).Append(",\n");
            builder.Append("  \"findings\": [\n");
            for (int i = 0; i < findings.Count; i++)
            {
                SyncIoFinding finding = findings[i];
                builder.Append("    {\n");
                AppendJson(builder, "owner", finding.Owner, 3).Append(",\n");
                AppendJson(builder, "path", finding.Path, 3).Append(",\n");
                builder.Append("      \"line\": ").Append(finding.Line).Append(",\n");
                AppendJson(builder, "pattern", finding.Pattern, 3).Append(",\n");
                AppendJson(builder, "context", finding.Context, 3).Append("\n");
                builder.Append("    }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.Append('\n');
            }

            builder.Append("  ]\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static string BuildStatementContext(string[] lines, int center)
        {
            StringBuilder builder = new StringBuilder(512);
            int balance = 0;
            bool sawOpen = false;
            for (int i = center; i < lines.Length && i < center + 16; i++)
            {
                string line = lines[i];
                builder.Append(line).Append('\n');
                for (int c = 0; c < line.Length; c++)
                {
                    char ch = line[c];
                    if (ch == '(')
                    {
                        balance++;
                        sawOpen = true;
                    }
                    else if (ch == ')' && balance > 0)
                    {
                        balance--;
                    }
                }

                if (sawOpen && balance == 0 && line.IndexOf(';') >= 0)
                    break;
            }

            return builder.ToString();
        }

        private static StringBuilder AppendJson(StringBuilder builder, string key, string value, int indent)
        {
            builder.Append(' ', indent * 2);
            builder.Append('"').Append(key).Append("\": \"");
            AppendEscaped(builder, value);
            builder.Append('"');
            return builder;
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\').Append(c);
                else if (c == '\n' || c == '\r' || c == '\t')
                    builder.Append(' ');
                else if (c < 32 || c > 126)
                    builder.Append("\\u").Append(((int)c).ToString("X4"));
                else
                    builder.Append(c);
            }
        }

        private static string TrimForJson(string line)
        {
            string value = line == null ? string.Empty : line.Trim();
            return value.Length <= 180 ? value : value.Substring(0, 180);
        }

        private static string ToProjectRelative(string projectRoot, string path)
        {
            string normalizedRoot = NormalizePath(projectRoot);
            string normalizedPath = NormalizePath(path);
            if (normalizedPath.StartsWith(normalizedRoot + "/", StringComparison.OrdinalIgnoreCase))
                return normalizedPath.Substring(normalizedRoot.Length + 1);
            return normalizedPath;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Application.dataPath;
        }

        private struct SyncIoFinding
        {
            public string Owner;
            public string Path;
            public int Line;
            public string Pattern;
            public string Context;
        }
    }
}
#endif
