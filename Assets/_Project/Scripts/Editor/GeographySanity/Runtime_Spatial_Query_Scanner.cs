#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.GeographySanity
{
    public static class Runtime_Spatial_Query_Scanner
    {
        private static readonly Encoding ReportEncoding = new UTF8Encoding(false);

        [MenuItem("Tools/Hecton8/World Sanity Checker/Run Runtime Spatial Query Scanner")]
        public static void RunMenu()
        {
            int findings = RunAndWriteReport();
            Debug.Log("Runtime_Spatial_Query_Scanner findings: " + findings + ". Report: " + GeographySanityConstants.OptimizationReportPathAgent);
        }

        public static int RunAndWriteReport()
        {
            string projectRoot = ResolveProjectRoot();
            string scriptsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            List<SpatialQueryFinding> findings = new List<SpatialQueryFinding>(128);
            int scannedFiles = 0;
            int requestedScopeFiles = 0;
            int[] safeSpawnLines = new int[8];
            PendingFinding[] pending = new PendingFinding[32];
            if (Directory.Exists(scriptsRoot))
            {
                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string normalized = Normalize(file);
                    if (ShouldExclude(normalized))
                        continue;

                    scannedFiles++;
                    if (IsRequestedScope(normalized))
                        requestedScopeFiles++;
                    ScanFile(projectRoot, file, normalized, findings, safeSpawnLines, pending);
                }
            }

            string report = BuildReport(projectRoot, scannedFiles, requestedScopeFiles, findings);
            string agentReportPath = Path.Combine(projectRoot, GeographySanityConstants.OptimizationReportPathAgent);
            string directory = Path.GetDirectoryName(agentReportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(agentReportPath, report, ReportEncoding);
            string sharedReportPath = Path.Combine(projectRoot, GeographySanityConstants.OptimizationReportPath);
            if (CanWriteSharedReport(sharedReportPath))
                File.WriteAllText(sharedReportPath, report, ReportEncoding);

            AssetDatabase.Refresh();
            return findings.Count;
        }

        private static bool CanWriteSharedReport(string path)
        {
            if (!File.Exists(path))
                return true;

            try
            {
                using (StreamReader reader = new StreamReader(path, ReportEncoding, detectEncodingFromByteOrderMarks: true))
                {
                    for (int i = 0; i < 16; i++)
                    {
                        string line = reader.ReadLine();
                        if (line == null)
                            break;

                        if (line.IndexOf("\"agent\"", StringComparison.Ordinal) < 0)
                            continue;

                        return ContainsQuotedToken(line.AsSpan(), GeographySanityConstants.AgentId);
                    }
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }

            return false;
        }

        private static void ScanFile(
            string projectRoot,
            string file,
            string normalized,
            List<SpatialQueryFinding> findings,
            int[] safeSpawnLines,
            PendingFinding[] pending)
        {
            string method = string.Empty;
            int methodBraceDepth = 0;
            bool methodHot = false;
            bool methodAwaitingBrace = false;
            int lineNumber = 0;
            int safeSpawnLineCount = 0;
            int pendingCount = 0;
            using (StreamReader reader = new StreamReader(file, ReportEncoding, detectEncodingFromByteOrderMarks: true))
            {
                string rawLine;
                while ((rawLine = reader.ReadLine()) != null)
                {
                    lineNumber++;
                    ReadOnlySpan<char> rawSpan = rawLine.AsSpan();
                    if (IsSafeSpawnText(rawSpan))
                    {
                        safeSpawnLines[safeSpawnLineCount & 7] = lineNumber;
                        safeSpawnLineCount++;
                        MarkPendingSafeSpawn(pending, pendingCount, lineNumber);
                    }

                    ReadOnlySpan<char> line = StripLineComment(rawSpan);
                    string candidate = ResolveMethodName(line);
                    if (candidate.Length > 0)
                    {
                        method = candidate;
                        methodHot = IsHotOrInitMethod(candidate);
                        methodBraceDepth = CountChar(line, '{') - CountChar(line, '}');
                        methodAwaitingBrace = line.IndexOf('{') < 0;
                        if (!methodAwaitingBrace && methodBraceDepth <= 0)
                            methodBraceDepth = 1;
                    }
                    else if (methodAwaitingBrace)
                    {
                        int braceDelta = CountChar(line, '{') - CountChar(line, '}');
                        if (line.IndexOf('{') >= 0)
                        {
                            methodBraceDepth = braceDelta <= 0 ? 1 : braceDelta;
                            methodAwaitingBrace = false;
                        }
                        else if (line.IndexOf(';') >= 0)
                        {
                            method = string.Empty;
                            methodHot = false;
                            methodAwaitingBrace = false;
                        }
                    }
                    else if (methodBraceDepth > 0)
                    {
                        methodBraceDepth += CountChar(line, '{') - CountChar(line, '}');
                        if (methodBraceDepth <= 0)
                        {
                            method = string.Empty;
                            methodHot = false;
                            methodAwaitingBrace = false;
                        }
                    }

                    string pattern = ResolveForbiddenPattern(line);
                    if (pattern.Length > 0)
                    {
                        bool requestedScope = IsRequestedScope(normalized);
                        bool blocking = methodHot || requestedScope || HasRecentSafeSpawn(safeSpawnLines, safeSpawnLineCount, lineNumber);
                        SpatialQueryFinding finding = default;
                        finding.Path = ToProjectRelative(projectRoot, file);
                        finding.Line = lineNumber;
                        finding.Pattern = pattern;
                        finding.Method = method.Length == 0 ? "unknown" : method;
                        finding.Scope = requestedScope ? "requested_scope" : "project_runtime_scope";
                        finding.Blocking = blocking;
                        finding.Context = Trim(line);
                        if (blocking)
                            findings.Add(finding);
                        else
                            AddPending(pending, ref pendingCount, finding, findings);
                    }

                    FlushPending(pending, ref pendingCount, lineNumber, findings, false);
                }
            }

            FlushPending(pending, ref pendingCount, lineNumber, findings, true);
        }

        private static string ResolveForbiddenPattern(ReadOnlySpan<char> line)
        {
            if (ContainsOrdinal(line, "Physics.SphereCast")) return "Physics.SphereCast";
            if (ContainsOrdinal(line, "Physics.CheckBox")) return "Physics.CheckBox";
            if (ContainsOrdinal(line, "Physics.CheckSphere")) return "Physics.CheckSphere";
            if (ContainsOrdinal(line, "Physics.CheckCapsule")) return "Physics.CheckCapsule";
            if (ContainsOrdinal(line, "Renderer.bounds")) return "Renderer.bounds";
            if (ContainsOrdinal(line, ".bounds.Intersects")) return "bounds.Intersects";
            if (ContainsOrdinal(line, "Bounds.Intersects")) return "Bounds.Intersects";
            return string.Empty;
        }

        private static string ResolveMethodName(ReadOnlySpan<char> line)
        {
            if (line.IndexOf('(') < 0 || line.IndexOf(')') < 0 || line.IndexOf(';') >= 0)
                return string.Empty;

            ReadOnlySpan<char> trimmed = TrimWhitespace(line);
            if (StartsWithOrdinal(trimmed, "if") ||
                StartsWithOrdinal(trimmed, "for") ||
                StartsWithOrdinal(trimmed, "while") ||
                StartsWithOrdinal(trimmed, "switch") ||
                StartsWithOrdinal(trimmed, "catch"))
            {
                return string.Empty;
            }

            int paren = trimmed.IndexOf('(');
            int end = paren - 1;
            while (end >= 0 && char.IsWhiteSpace(trimmed[end]))
                end--;

            int start = end;
            while (start >= 0 && (char.IsLetterOrDigit(trimmed[start]) || trimmed[start] == '_'))
                start--;

            return start < end ? new string(trimmed.Slice(start + 1, end - start)) : string.Empty;
        }

        private static bool IsHotOrInitMethod(string method)
        {
            return method == "Awake" ||
                   method == "Start" ||
                   method == "OnEnable" ||
                   method == "Update" ||
                   method == "FixedUpdate" ||
                   method == "LateUpdate" ||
                   method == "Tick" ||
                   method == "FixedTick" ||
                   method == "SlowTick";
        }

        private static bool IsSafeSpawnText(ReadOnlySpan<char> line)
        {
            return ContainsAsciiIgnoreCase(line, "SafeSpawn") ||
                   ContainsAsciiIgnoreCase(line, "safe spawn") ||
                   ContainsAsciiIgnoreCase(line, "safe-spawn");
        }

        private static bool HasRecentSafeSpawn(int[] safeSpawnLines, int safeSpawnLineCount, int lineNumber)
        {
            int count = Math.Min(safeSpawnLineCount, safeSpawnLines.Length);
            for (int i = 0; i < count; i++)
            {
                int safeLine = safeSpawnLines[i];
                if (safeLine > 0 && lineNumber - safeLine <= 8)
                    return true;
            }

            return false;
        }

        private static void AddPending(PendingFinding[] pending, ref int pendingCount, SpatialQueryFinding finding, List<SpatialQueryFinding> findings)
        {
            if (pendingCount >= pending.Length)
            {
                findings.Add(finding);
                return;
            }

            pending[pendingCount].Line = finding.Line;
            pending[pendingCount].Finding = finding;
            pendingCount++;
        }

        private static void MarkPendingSafeSpawn(PendingFinding[] pending, int pendingCount, int lineNumber)
        {
            for (int i = 0; i < pendingCount; i++)
            {
                if (lineNumber - pending[i].Line > 8)
                    continue;

                SpatialQueryFinding finding = pending[i].Finding;
                finding.Blocking = true;
                pending[i].Finding = finding;
            }
        }

        private static void FlushPending(PendingFinding[] pending, ref int pendingCount, int lineNumber, List<SpatialQueryFinding> findings, bool flushAll)
        {
            int write = 0;
            for (int i = 0; i < pendingCount; i++)
            {
                PendingFinding candidate = pending[i];
                if (flushAll || lineNumber - candidate.Line > 8 || candidate.Finding.Blocking)
                {
                    findings.Add(candidate.Finding);
                    continue;
                }

                pending[write++] = candidate;
            }

            pendingCount = write;
        }

        private static bool ShouldExclude(string normalized)
        {
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith("/Editor.cs", StringComparison.OrdinalIgnoreCase) ||
                   normalized.IndexOf("SmokeTester", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsRequestedScope(string normalized)
        {
            return normalized.IndexOf("/Assets/_Project/Scripts/Environment/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Assets/_Project/Scripts/WorldGeneration/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildReport(string projectRoot, int scannedFiles, int requestedScopeFiles, List<SpatialQueryFinding> findings)
        {
            int blocking = 0;
            int requestedScopeFindings = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Blocking)
                    blocking++;
                if (findings[i].Scope == "requested_scope")
                    requestedScopeFindings++;
            }

            StringBuilder builder = new StringBuilder(8192);
            builder.Append("{\n");
            AppendJson(builder, "schema", "hecton8.world_optimization_report.v1", 1).Append(",\n");
            AppendJson(builder, "agent", GeographySanityConstants.AgentId, 1).Append(",\n");
            AppendJson(builder, "scanner", nameof(Runtime_Spatial_Query_Scanner), 1).Append(",\n");
            AppendJson(builder, "summary", blocking == 0 ? "Runtime Safe-Spawn Checks Eradicated" : "Runtime Safe-Spawn Checks NOT Eradicated", 1).Append(",\n");
            AppendJson(builder, "projectRoot", projectRoot, 1).Append(",\n");
            builder.Append("  \"scannedFiles\": ").Append(scannedFiles).Append(",\n");
            builder.Append("  \"requestedScopeFiles\": ").Append(requestedScopeFiles).Append(",\n");
            builder.Append("  \"requestedScopeFindings\": ").Append(requestedScopeFindings).Append(",\n");
            builder.Append("  \"blockingFindings\": ").Append(blocking).Append(",\n");
            builder.Append("  \"allFindings\": ").Append(findings.Count).Append(",\n");
            builder.Append("  \"findings\": [\n");
            for (int i = 0; i < findings.Count; i++)
            {
                SpatialQueryFinding f = findings[i];
                builder.Append("    {\n");
                AppendJson(builder, "scope", f.Scope, 3).Append(",\n");
                AppendJson(builder, "path", f.Path, 3).Append(",\n");
                builder.Append("      \"line\": ").Append(f.Line).Append(",\n");
                AppendJson(builder, "method", f.Method, 3).Append(",\n");
                AppendJson(builder, "pattern", f.Pattern, 3).Append(",\n");
                builder.Append("      \"blocking\": ").Append(f.Blocking ? "true" : "false").Append(",\n");
                AppendJson(builder, "context", f.Context, 3).Append("\n");
                builder.Append("    }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.Append('\n');
            }

            builder.Append("  ]\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static StringBuilder AppendJson(StringBuilder builder, string key, string value, int indent)
        {
            builder.Append(' ', indent * 2);
            builder.Append('"').Append(key).Append("\": \"");
            if (!string.IsNullOrEmpty(value))
            {
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (c == '\\' || c == '"')
                        builder.Append('\\').Append(c);
                    else if (c == '\n' || c == '\r' || c == '\t')
                        builder.Append(' ');
                    else if (c < 32 || c > 126)
                        AppendUnicodeEscape(builder, c);
                    else
                        builder.Append(c);
                }
            }

            builder.Append('"');
            return builder;
        }

        private static StringBuilder AppendUnicodeEscape(StringBuilder builder, char value)
        {
            builder.Append("\\u");
            int scalar = value;
            builder.Append(ToHexNibble((scalar >> 12) & 0xF));
            builder.Append(ToHexNibble((scalar >> 8) & 0xF));
            builder.Append(ToHexNibble((scalar >> 4) & 0xF));
            builder.Append(ToHexNibble(scalar & 0xF));
            return builder;
        }

        private static char ToHexNibble(int value)
        {
            return (char)(value < 10 ? '0' + value : 'A' + value - 10);
        }

        private static ReadOnlySpan<char> StripLineComment(ReadOnlySpan<char> line)
        {
            int idx = IndexOfOrdinal(line, "//");
            return idx >= 0 ? line.Slice(0, idx) : line;
        }

        private static int CountChar(ReadOnlySpan<char> line, char c)
        {
            int count = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == c)
                    count++;
            }

            return count;
        }

        private static string Trim(ReadOnlySpan<char> line)
        {
            ReadOnlySpan<char> value = TrimWhitespace(line);
            if (value.Length > 180)
                value = value.Slice(0, 180);

            return value.Length == 0 ? string.Empty : new string(value);
        }

        private static string ToProjectRelative(string projectRoot, string path)
        {
            string root = Normalize(projectRoot);
            string normalized = Normalize(path);
            return HasRootPrefix(normalized.AsSpan(), root.AsSpan())
                ? new string(normalized.AsSpan(root.Length + 1))
                : normalized;
        }

        private static ReadOnlySpan<char> TrimWhitespace(ReadOnlySpan<char> value)
        {
            int start = 0;
            int end = value.Length - 1;
            while (start <= end && char.IsWhiteSpace(value[start]))
                start++;
            while (end >= start && char.IsWhiteSpace(value[end]))
                end--;

            return start <= end ? value.Slice(start, end - start + 1) : ReadOnlySpan<char>.Empty;
        }

        private static bool StartsWithOrdinal(ReadOnlySpan<char> source, string token)
        {
            if (source.Length < token.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (source[i] != token[i])
                    return false;
            }

            return true;
        }

        private static bool ContainsOrdinal(ReadOnlySpan<char> source, string token)
        {
            return IndexOfOrdinal(source, token) >= 0;
        }

        private static int IndexOfOrdinal(ReadOnlySpan<char> source, string token)
        {
            if (token.Length == 0)
                return 0;
            if (source.Length < token.Length)
                return -1;

            int last = source.Length - token.Length;
            char first = token[0];
            for (int i = 0; i <= last; i++)
            {
                if (source[i] != first)
                    continue;

                int j = 1;
                while (j < token.Length && source[i + j] == token[j])
                    j++;

                if (j == token.Length)
                    return i;
            }

            return -1;
        }

        private static bool ContainsQuotedToken(ReadOnlySpan<char> source, string token)
        {
            int requiredLength = token.Length + 2;
            if (source.Length < requiredLength)
                return false;

            int last = source.Length - requiredLength;
            for (int i = 0; i <= last; i++)
            {
                if (source[i] != '"' || source[i + requiredLength - 1] != '"')
                    continue;

                int j = 0;
                while (j < token.Length && source[i + 1 + j] == token[j])
                    j++;

                if (j == token.Length)
                    return true;
            }

            return false;
        }

        private static bool ContainsAsciiIgnoreCase(ReadOnlySpan<char> source, string token)
        {
            if (token.Length == 0)
                return true;
            if (source.Length < token.Length)
                return false;

            int last = source.Length - token.Length;
            char first = ToUpperAscii(token[0]);
            for (int i = 0; i <= last; i++)
            {
                if (ToUpperAscii(source[i]) != first)
                    continue;

                int j = 1;
                while (j < token.Length && ToUpperAscii(source[i + j]) == ToUpperAscii(token[j]))
                    j++;

                if (j == token.Length)
                    return true;
            }

            return false;
        }

        private static char ToUpperAscii(char value)
        {
            return value >= 'a' && value <= 'z' ? (char)(value - 32) : value;
        }

        private static bool HasRootPrefix(ReadOnlySpan<char> normalized, ReadOnlySpan<char> root)
        {
            return normalized.Length > root.Length &&
                   normalized[root.Length] == '/' &&
                   EqualsOrdinalIgnoreCase(normalized.Slice(0, root.Length), root);
        }

        private static bool EqualsOrdinalIgnoreCase(ReadOnlySpan<char> left, ReadOnlySpan<char> right)
        {
            if (left.Length != right.Length)
                return false;

            for (int i = 0; i < left.Length; i++)
            {
                char a = left[i];
                char b = right[i];
                if (a == b)
                    continue;

                if (char.ToUpperInvariant(a) != char.ToUpperInvariant(b))
                    return false;
            }

            return true;
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo parent = Directory.GetParent(Application.dataPath);
            return parent != null ? parent.FullName : Directory.GetCurrentDirectory();
        }

        private struct SpatialQueryFinding
        {
            public string Scope;
            public string Path;
            public int Line;
            public string Method;
            public string Pattern;
            public bool Blocking;
            public string Context;
        }

        private struct PendingFinding
        {
            public int Line;
            public SpatialQueryFinding Finding;
        }
    }
}
#endif
