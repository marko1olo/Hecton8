#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class Stall_Eradication_Scanner
    {
        private const string ScriptsRoot = "Assets/_Project/Scripts";
        private const string ReportPath = "Docs/Reports/DISPATCHER_OPTIMIZATION_REPORT.json";
        [MenuItem("Hecton/Diagnostics/Run Stall Eradication Scanner")]
        public static void Run()
        {
            Directory.CreateDirectory("Docs/Reports");
            ScanResult result = default;
            StringBuilder offenders = new StringBuilder(8192);
            StringBuilder runtimeRuns = new StringBuilder(8192);
            string[] files = Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories);
            for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
            {
                string path = files[fileIndex].Replace('\\', '/');
                string[] lines = File.ReadAllLines(path);
                bool editorFile = path.Contains("/Editor/") ||
                                  path.EndsWith("SmokeTester.cs", StringComparison.Ordinal) ||
                                  HasUnityEditorFileGuard(lines);
                bool[] hotMethodLines = BuildHotMethodLineMap(lines);
                for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    string line = lines[lineIndex];
                    string trimmed = line.TrimStart();
                    if (IsCommentLine(trimmed))
                    {
                        continue;
                    }

                    bool hasComplete = line.Contains(".Complete(") || line.Contains("CompleteAll(");
                    bool hasRun = line.Contains(".Run(") && !line.Contains("Task.Run(");
                    bool hasForcedFence = line.Contains("TryComplete(") &&
                                          (line.Contains("forceComplete: true") || line.Contains(", true)"));
                    if (!hasComplete && !hasRun && !hasForcedFence)
                        continue;

                    result.TotalSyncTokens++;
                    if (editorFile || IsColdAnnotated(lines, lineIndex))
                    {
                        result.ColdOrEditorTokens++;
                        continue;
                    }

                    if (hasRun)
                    {
                        result.RuntimeRunTokens++;
                        AppendOffender(runtimeRuns, path, lineIndex + 1, "Run", line.Trim());
                    }

                    if (hasForcedFence)
                        result.ForcedFenceTokens++;

                    if (hotMethodLines[lineIndex] || IsLikelyHotPathContext(lines, lineIndex))
                    {
                        result.HotPathTokens++;
                        if (hotMethodLines[lineIndex])
                            result.MethodScopedHotPathTokens++;
                        if (hasForcedFence)
                            result.ForcedHotPathTokens++;
                        AppendOffender(offenders, path, lineIndex + 1, ResolveTokenName(hasComplete, hasRun, hasForcedFence), line.Trim());
                    }
                    else
                    {
                        result.UnclassifiedRuntimeTokens++;
                    }
                }
            }

            File.WriteAllText(ReportPath, BuildJson(in result, offenders, runtimeRuns));
            AssetDatabase.Refresh();
        }

        private static bool[] BuildHotMethodLineMap(string[] lines)
        {
            bool[] hot = new bool[lines.Length];
            List<MethodRange> methods = new List<MethodRange>(256);
            DiscoverMethodRanges(lines, methods);

            for (int methodIndex = 0; methodIndex < methods.Count; methodIndex++)
            {
                MethodRange range = methods[methodIndex];
                range.IsHot = IsHotMethodSignature(lines[range.SignatureLine]);
                methods[methodIndex] = range;
            }

            for (int pass = 0; pass < 3; pass++)
            {
                bool changed = false;
                for (int methodIndex = 0; methodIndex < methods.Count; methodIndex++)
                {
                    MethodRange range = methods[methodIndex];
                    if (!range.IsHot)
                        continue;

                    for (int lineIndex = range.StartLine; lineIndex <= range.EndLine; lineIndex++)
                    {
                        string line = lines[lineIndex];
                        if (IsCommentLine(line.TrimStart()))
                            continue;

                        for (int calleeIndex = 0; calleeIndex < methods.Count; calleeIndex++)
                        {
                            MethodRange callee = methods[calleeIndex];
                            if (callee.IsHot || calleeIndex == methodIndex)
                                continue;

                            if (!ContainsCallTo(line, callee.Name))
                                continue;

                            callee.IsHot = true;
                            methods[calleeIndex] = callee;
                            changed = true;
                        }
                    }
                }

                if (!changed)
                    break;
            }

            for (int methodIndex = 0; methodIndex < methods.Count; methodIndex++)
            {
                MethodRange range = methods[methodIndex];
                if (!range.IsHot)
                    continue;

                for (int lineIndex = range.StartLine; lineIndex <= range.EndLine && lineIndex < hot.Length; lineIndex++)
                    hot[lineIndex] = true;
            }

            return hot;
        }

        private static void DiscoverMethodRanges(string[] lines, List<MethodRange> methods)
        {
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                if (!TryExtractMethodName(lines[lineIndex], out string methodName))
                    continue;

                if (!TryResolveMethodBody(lines, lineIndex, out int startLine, out int endLine))
                    continue;

                MethodRange range;
                range.Name = methodName;
                range.SignatureLine = lineIndex;
                range.StartLine = startLine;
                range.EndLine = endLine;
                range.IsHot = false;
                methods.Add(range);
            }
        }

        private static bool TryResolveMethodBody(string[] lines, int signatureLine, out int startLine, out int endLine)
        {
            startLine = -1;
            endLine = -1;
            int depth = 0;
            bool bodyStarted = false;
            for (int scanIndex = signatureLine; scanIndex < lines.Length; scanIndex++)
            {
                string line = lines[scanIndex];
                if (!bodyStarted)
                {
                    int openBefore = CountChar(line, '{');
                    int closeBefore = CountChar(line, '}');
                    if (openBefore <= 0)
                    {
                        if (line.Contains(";"))
                            return false;
                        continue;
                    }

                    bodyStarted = true;
                    startLine = scanIndex;
                    depth += openBefore - closeBefore;
                    if (depth <= 0)
                    {
                        endLine = scanIndex;
                        return true;
                    }

                    continue;
                }

                depth += CountChar(line, '{') - CountChar(line, '}');
                if (depth <= 0)
                {
                    endLine = scanIndex;
                    return true;
                }
            }

            return false;
        }

        private static bool TryExtractMethodName(string line, out string methodName)
        {
            methodName = null;
            string trimmed = line.TrimStart();
            if (IsCommentLine(trimmed) ||
                trimmed.StartsWith("if", StringComparison.Ordinal) ||
                trimmed.StartsWith("for", StringComparison.Ordinal) ||
                trimmed.StartsWith("while", StringComparison.Ordinal) ||
                trimmed.StartsWith("switch", StringComparison.Ordinal) ||
                trimmed.StartsWith("catch", StringComparison.Ordinal) ||
                trimmed.StartsWith("using", StringComparison.Ordinal) ||
                trimmed.StartsWith("lock", StringComparison.Ordinal) ||
                trimmed.StartsWith("return", StringComparison.Ordinal) ||
                trimmed.StartsWith("new ", StringComparison.Ordinal))
            {
                return false;
            }

            int open = line.IndexOf('(');
            if (open <= 0)
                return false;

            int equals = line.IndexOf('=');
            if (equals >= 0 && equals < open)
                return false;

            int cursor = open - 1;
            while (cursor >= 0 && char.IsWhiteSpace(line[cursor]))
                cursor--;
            int end = cursor;
            while (cursor >= 0 && IsIdentifierChar(line[cursor]))
                cursor--;
            int start = cursor + 1;
            if (start > end)
                return false;

            methodName = line.Substring(start, end - start + 1);
            return !IsControlKeyword(methodName);
        }

        private static bool ContainsCallTo(string line, string methodName)
        {
            int index = line.IndexOf(methodName, StringComparison.Ordinal);
            while (index >= 0)
            {
                int before = index - 1;
                int after = index + methodName.Length;
                while (after < line.Length && char.IsWhiteSpace(line[after]))
                    after++;

                bool leftClear = before < 0 || !IsIdentifierChar(line[before]);
                bool rightCall = after < line.Length && line[after] == '(';
                if (leftClear && rightCall)
                    return true;

                index = line.IndexOf(methodName, index + methodName.Length, StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsIdentifierChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static bool IsControlKeyword(string value)
        {
            return value == "if" ||
                   value == "for" ||
                   value == "while" ||
                   value == "switch" ||
                   value == "catch" ||
                   value == "using" ||
                   value == "lock" ||
                   value == "return";
        }

        private static bool IsLikelyHotPathContext(string[] lines, int lineIndex)
        {
            int start = Math.Max(0, lineIndex - 16);
            for (int i = lineIndex; i >= start; i--)
            {
                if (IsCommentLine(lines[i].TrimStart()))
                    continue;

                if (IsHotMethodSignature(lines[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsColdAnnotated(string[] lines, int lineIndex)
        {
            int start = Math.Max(0, lineIndex - 2);
            for (int i = lineIndex; i >= start; i--)
            {
                string line = lines[i];
                if (line.Contains("COLD SYNC JOB") || line.Contains("COLD/EDITOR"))
                    return true;
            }

            return false;
        }

        private static bool HasUnityEditorFileGuard(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();
                if (line.Length == 0)
                    continue;

                if (line.StartsWith("//", StringComparison.Ordinal) ||
                    line.StartsWith("/*", StringComparison.Ordinal) ||
                    line.StartsWith("*", StringComparison.Ordinal))
                {
                    continue;
                }

                return line.StartsWith("#if UNITY_EDITOR", StringComparison.Ordinal) ||
                       line.StartsWith("#if UNITY_EDITOR || DEVELOPMENT_BUILD", StringComparison.Ordinal);
            }

            return false;
        }

        private static bool IsHotMethodSignature(string line)
        {
            return line.Contains(" Tick(") ||
                   line.Contains("Tick(float") ||
                   line.Contains("FixedTick(") ||
                   line.Contains("FastTick(") ||
                   line.Contains("LateFrameTick(") ||
                   line.Contains("ToolTick(") ||
                   line.Contains("PostFixedTick(") ||
                   line.Contains("Update(") ||
                   line.Contains("FixedUpdate(") ||
                   line.Contains("LateUpdate(") ||
                   line.Contains("ScheduleSimulation(") ||
                   line.Contains("PostSimulationTick(") ||
                   line.Contains("VisualSyncTick(");
        }

        private static string ResolveTokenName(bool hasComplete, bool hasRun, bool hasForcedFence)
        {
            if (hasForcedFence)
                return "ForcedFence";
            return hasComplete ? "Complete" : "Run";
        }

        private static bool IsCommentLine(string trimmed)
        {
            return trimmed.StartsWith("//", StringComparison.Ordinal) ||
                   trimmed.StartsWith("*", StringComparison.Ordinal) ||
                   trimmed.StartsWith("/*", StringComparison.Ordinal);
        }

        private static int CountChar(string line, char value)
        {
            int count = 0;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == value)
                    count++;
            }

            return count;
        }

        private static void AppendOffender(StringBuilder builder, string path, int line, string token, string source)
        {
            if (builder.Length > 0)
                builder.Append(',');

            builder.Append("{\"path\":\"");
            AppendEscaped(builder, path);
            builder.Append("\",\"line\":");
            builder.Append(line);
            builder.Append(",\"token\":\"");
            builder.Append(token);
            builder.Append("\",\"source\":\"");
            AppendEscaped(builder, source);
            builder.Append("\"}");
        }

        private static string BuildJson(in ScanResult result, StringBuilder offenders, StringBuilder runtimeRuns)
        {
            StringBuilder json = new StringBuilder(12288);
            json.Append("{\n");
            json.Append("  \"agent\":\"SHINOBU_206\",\n");
            json.Append("  \"status\":\"PENDING_VERIFICATION\",\n");
            json.Append("  \"scope\":\"Assets/_Project/Scripts\",\n");
            json.Append("  \"totalSyncTokens\":").Append(result.TotalSyncTokens).Append(",\n");
            json.Append("  \"coldOrEditorTokens\":").Append(result.ColdOrEditorTokens).Append(",\n");
            json.Append("  \"hotPathTokens\":").Append(result.HotPathTokens).Append(",\n");
            json.Append("  \"methodScopedHotPathTokens\":").Append(result.MethodScopedHotPathTokens).Append(",\n");
            json.Append("  \"runtimeRunTokens\":").Append(result.RuntimeRunTokens).Append(",\n");
            json.Append("  \"forcedFenceTokens\":").Append(result.ForcedFenceTokens).Append(",\n");
            json.Append("  \"forcedHotPathTokens\":").Append(result.ForcedHotPathTokens).Append(",\n");
            json.Append("  \"unclassifiedRuntimeTokens\":").Append(result.UnclassifiedRuntimeTokens).Append(",\n");
            json.Append("  \"synchronousStallsEliminated\":\"STATIC_SOURCE_PENDING_MANUAL_PATCH_COUNT\",\n");
            json.Append("  \"hotPathOffenders\":[");
            json.Append(offenders);
            json.Append("],\n");
            json.Append("  \"runtimeRunTokenSamples\":[");
            json.Append(runtimeRuns);
            json.Append("]\n");
            json.Append("}\n");
            return json.ToString();
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '"')
                    builder.Append('\\');
                builder.Append(c);
            }
        }

        private struct ScanResult
        {
            public int TotalSyncTokens;
            public int ColdOrEditorTokens;
            public int HotPathTokens;
            public int MethodScopedHotPathTokens;
            public int RuntimeRunTokens;
            public int ForcedFenceTokens;
            public int ForcedHotPathTokens;
            public int UnclassifiedRuntimeTokens;
        }

        private struct MethodRange
        {
            public string Name;
            public int SignatureLine;
            public int StartLine;
            public int EndLine;
            public bool IsHot;
        }
    }
}
#endif
