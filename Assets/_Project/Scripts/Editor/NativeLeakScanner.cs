#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Editor-only source scanner for blunt native collection lifecycle checks.
    /// This is not runtime leak proof; it finds files with native allocations that lack same-file Dispose evidence.
    /// </summary>
    public static class NativeLeakScanner
    {
        private const string ScanRoot = "Assets/_Project/Scripts";
        private const string OutputPath = "CodexArtifacts/native-leak-scanner-results.json";

        [MenuItem("Hecton8/Audit/Native Leak Scanner")]
        public static void RunFromMenu()
        {
            ScanResult result = RunScan();
            WriteJson(result);

            string summary = BuildConsoleSummary(result);
            if (result.StrictViolatorCount > 0)
            {
                Debug.LogError(summary);
                return;
            }

            Debug.Log(summary);
        }

        public static ScanResult RunScan()
        {
            string projectRoot = GetProjectRoot();
            string fullScanRoot = Path.GetFullPath(Path.Combine(projectRoot, ScanRoot));

            var result = new ScanResult();
            result.ScanRoot = ScanRoot;
            result.Findings = Array.Empty<Finding>();

            if (!Directory.Exists(fullScanRoot))
            {
                result.Error = "Scan root not found: " + fullScanRoot;
                return result;
            }

            var findings = new List<Finding>(128); // COLD ALLOC: List<Finding>[128] — editor-only audit result list — owner: NativeLeakScanner

            foreach (string filePath in Directory.EnumerateFiles(fullScanRoot, "*.cs", SearchOption.AllDirectories))
            {
                result.FilesScanned++;
                string text = File.ReadAllText(filePath);
                string codeText = MaskCommentsAndStrings(text);
                int allocationHits = CountNativeAllocations(codeText);
                if (allocationHits == 0)
                    continue;

                bool hasDirectDispose = ContainsCall(codeText, ".Dispose");
                bool hasDisposeHelper = ContainsCallWithIdentifierSuffix(codeText, "DisposeNative");
                bool hasRegister = codeText.IndexOf("NativeMemorySentinel.RegisterNative", StringComparison.Ordinal) >= 0;
                bool hasUnregister =
                    codeText.IndexOf("NativeMemorySentinel.UnregisterNative", StringComparison.Ordinal) >= 0 ||
                    codeText.IndexOf("NativeMemorySentinel.Unregister", StringComparison.Ordinal) >= 0;

                var finding = new Finding();
                finding.Path = ToProjectRelativePath(projectRoot, filePath);
                finding.AllocationHits = allocationHits;
                finding.HasDirectDispose = hasDirectDispose;
                finding.HasDisposeHelper = hasDisposeHelper;
                finding.HasSentinelRegister = hasRegister;
                finding.HasSentinelUnregister = hasUnregister;
                finding.StrictViolation = !hasDirectDispose;

                findings.Add(finding);
                result.AllocationFiles++;
                result.AllocationHits += allocationHits;
                if (finding.StrictViolation)
                    result.StrictViolatorCount++;
                if (!hasDirectDispose && hasDisposeHelper)
                    result.HelperOnlyCount++;
                if (hasRegister && !hasUnregister)
                    result.RegisterWithoutUnregisterCount++;
            }

            result.Findings = findings.ToArray();
            return result;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string ToProjectRelativePath(string projectRoot, string filePath)
        {
            string relative = filePath;
            if (filePath.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase))
                relative = filePath.Substring(projectRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return relative.Replace('\\', '/');
        }

        private static int CountNativeAllocations(string codeText)
        {
            if (string.IsNullOrEmpty(codeText))
                return 0;

            int count = 0;
            int index = 0;
            while (index < codeText.Length)
            {
                int newIndex = codeText.IndexOf("new", index, StringComparison.Ordinal);
                if (newIndex < 0)
                    break;

                index = newIndex + 3;
                if (!HasTokenBoundary(codeText, newIndex - 1) || !HasTokenBoundary(codeText, index))
                    continue;

                int cursor = SkipWhitespace(codeText, index);
                if (IsNativeAllocationType(codeText, cursor, out int typeEnd) &&
                    typeEnd < codeText.Length &&
                    codeText[typeEnd] == '<')
                {
                    count++;
                    index = typeEnd + 1;
                }
            }

            return count;
        }

        private static bool IsNativeAllocationType(string text, int index, out int typeEnd)
        {
            if (StartsWithToken(text, index, "NativeParallelHashMap", out typeEnd))
                return true;
            if (StartsWithToken(text, index, "NativeHashMap", out typeEnd))
                return true;
            if (StartsWithToken(text, index, "NativeReference", out typeEnd))
                return true;
            if (StartsWithToken(text, index, "NativeArray", out typeEnd))
                return true;
            if (StartsWithToken(text, index, "NativeQueue", out typeEnd))
                return true;
            if (StartsWithToken(text, index, "NativeList", out typeEnd))
                return true;

            typeEnd = index;
            return false;
        }

        private static bool StartsWithToken(string text, int index, string token, out int tokenEnd)
        {
            tokenEnd = index + token.Length;
            if (index < 0 || tokenEnd > text.Length)
                return false;

            for (int i = 0; i < token.Length; i++)
            {
                if (text[index + i] != token[i])
                    return false;
            }

            return tokenEnd == text.Length || !IsIdentifierChar(text[tokenEnd]);
        }

        private static bool ContainsCall(string text, string token)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            int index = 0;
            while (index < text.Length)
            {
                index = text.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                int cursor = SkipWhitespace(text, index + token.Length);
                if (cursor < text.Length && text[cursor] == '(')
                    return true;

                index += token.Length;
            }

            return false;
        }

        private static bool ContainsCallWithIdentifierSuffix(string text, string prefix)
        {
            if (string.IsNullOrEmpty(text))
                return false;

            int index = 0;
            while (index < text.Length)
            {
                index = text.IndexOf(prefix, index, StringComparison.Ordinal);
                if (index < 0)
                    return false;

                if (!HasTokenBoundary(text, index - 1))
                {
                    index += prefix.Length;
                    continue;
                }

                int cursor = index + prefix.Length;
                while (cursor < text.Length && IsIdentifierChar(text[cursor]))
                    cursor++;

                cursor = SkipWhitespace(text, cursor);
                if (cursor < text.Length && text[cursor] == '(')
                    return true;

                index += prefix.Length;
            }

            return false;
        }

        private static int SkipWhitespace(string text, int index)
        {
            while (index < text.Length && char.IsWhiteSpace(text[index]))
                index++;

            return index;
        }

        private static bool HasTokenBoundary(string text, int index)
        {
            return index < 0 || index >= text.Length || !IsIdentifierChar(text[index]);
        }

        private static bool IsIdentifierChar(char value)
        {
            return char.IsLetterOrDigit(value) || value == '_';
        }

        private static string MaskCommentsAndStrings(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            char[] buffer = text.ToCharArray(); // COLD ALLOC: char[text.Length] — editor-only source mask — owner: NativeLeakScanner
            int index = 0;
            while (index < buffer.Length)
            {
                char current = buffer[index];
                char next = index + 1 < buffer.Length ? buffer[index + 1] : '\0';

                if (current == '/' && next == '/')
                {
                    index = MaskLineComment(buffer, index);
                    continue;
                }

                if (current == '/' && next == '*')
                {
                    index = MaskBlockComment(buffer, index);
                    continue;
                }

                if (current == '@' && next == '"')
                {
                    index = MaskVerbatimString(buffer, index, index + 1);
                    continue;
                }

                if (current == '$' && next == '@' && index + 2 < buffer.Length && buffer[index + 2] == '"')
                {
                    index = MaskVerbatimString(buffer, index, index + 2);
                    continue;
                }

                if (current == '@' && next == '$' && index + 2 < buffer.Length && buffer[index + 2] == '"')
                {
                    index = MaskVerbatimString(buffer, index, index + 2);
                    continue;
                }

                if (current == '$' && next == '"')
                {
                    index = MaskRegularString(buffer, index, index + 1);
                    continue;
                }

                if (current == '"')
                {
                    index = MaskRegularString(buffer, index, index);
                    continue;
                }

                if (current == '\'')
                {
                    index = MaskCharLiteral(buffer, index);
                    continue;
                }

                index++;
            }

            return new string(buffer); // COLD ALLOC: string[text.Length] — editor-only masked source snapshot — owner: NativeLeakScanner
        }

        private static int MaskLineComment(char[] buffer, int start)
        {
            int index = start;
            while (index < buffer.Length && buffer[index] != '\n')
            {
                buffer[index] = ' ';
                index++;
            }

            return index;
        }

        private static int MaskBlockComment(char[] buffer, int start)
        {
            int index = start;
            while (index < buffer.Length)
            {
                bool end = index + 1 < buffer.Length && buffer[index] == '*' && buffer[index + 1] == '/';
                if (buffer[index] != '\r' && buffer[index] != '\n')
                    buffer[index] = ' ';
                index++;
                if (end)
                {
                    if (index < buffer.Length && buffer[index] != '\r' && buffer[index] != '\n')
                        buffer[index] = ' ';
                    return index + 1;
                }
            }

            return index;
        }

        private static int MaskRegularString(char[] buffer, int start, int quoteIndex)
        {
            for (int i = start; i <= quoteIndex && i < buffer.Length; i++)
                buffer[i] = ' ';

            int index = quoteIndex + 1;
            bool escaped = false;
            while (index < buffer.Length)
            {
                char current = buffer[index];
                if (current != '\r' && current != '\n')
                    buffer[index] = ' ';

                if (!escaped && current == '"')
                    return index + 1;

                escaped = !escaped && current == '\\';
                if (current != '\\')
                    escaped = false;
                index++;
            }

            return index;
        }

        private static int MaskVerbatimString(char[] buffer, int start, int quoteIndex)
        {
            for (int i = start; i <= quoteIndex && i < buffer.Length; i++)
                buffer[i] = ' ';

            int index = quoteIndex + 1;
            while (index < buffer.Length)
            {
                char current = buffer[index];
                if (current != '\r' && current != '\n')
                    buffer[index] = ' ';

                if (current == '"')
                {
                    bool escapedQuote = index + 1 < buffer.Length && buffer[index + 1] == '"';
                    if (escapedQuote)
                    {
                        buffer[index + 1] = ' ';
                        index += 2;
                        continue;
                    }

                    return index + 1;
                }

                index++;
            }

            return index;
        }

        private static int MaskCharLiteral(char[] buffer, int start)
        {
            buffer[start] = ' ';
            int index = start + 1;
            bool escaped = false;
            while (index < buffer.Length)
            {
                char current = buffer[index];
                if (current != '\r' && current != '\n')
                    buffer[index] = ' ';

                if (!escaped && current == '\'')
                    return index + 1;

                escaped = !escaped && current == '\\';
                if (current != '\\')
                    escaped = false;
                index++;
            }

            return index;
        }

        private static void WriteJson(ScanResult result)
        {
            string projectRoot = GetProjectRoot();
            string outputPath = Path.Combine(projectRoot, OutputPath);
            string directory = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(outputPath, BuildJson(result), Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static string BuildConsoleSummary(ScanResult result)
        {
            if (!string.IsNullOrEmpty(result.Error))
                return "[NativeLeakScanner] " + result.Error;

            return "[NativeLeakScanner] files=" + result.FilesScanned +
                   " allocationFiles=" + result.AllocationFiles +
                   " allocationHits=" + result.AllocationHits +
                   " strictViolators=" + result.StrictViolatorCount +
                   " helperOnly=" + result.HelperOnlyCount +
                   " registerWithoutUnregister=" + result.RegisterWithoutUnregisterCount +
                   " output=" + OutputPath;
        }

        private static string BuildJson(ScanResult result)
        {
            var builder = new StringBuilder(8192); // COLD ALLOC: StringBuilder[8192 chars] — editor-only JSON artifact — owner: NativeLeakScanner
            builder.AppendLine("{");
            AppendJsonField(builder, "scanRoot", result.ScanRoot, true);
            AppendJsonField(builder, "error", result.Error, true);
            AppendJsonField(builder, "filesScanned", result.FilesScanned, true);
            AppendJsonField(builder, "allocationFiles", result.AllocationFiles, true);
            AppendJsonField(builder, "allocationHits", result.AllocationHits, true);
            AppendJsonField(builder, "strictViolatorCount", result.StrictViolatorCount, true);
            AppendJsonField(builder, "helperOnlyCount", result.HelperOnlyCount, true);
            AppendJsonField(builder, "registerWithoutUnregisterCount", result.RegisterWithoutUnregisterCount, true);
            builder.AppendLine("  \"findings\": [");

            Finding[] findings = result.Findings;
            for (int i = 0; i < findings.Length; i++)
            {
                Finding finding = findings[i];
                builder.AppendLine("    {");
                AppendJsonField(builder, "path", finding.Path, true, 6);
                AppendJsonField(builder, "allocationHits", finding.AllocationHits, true, 6);
                AppendJsonField(builder, "hasDirectDispose", finding.HasDirectDispose, true, 6);
                AppendJsonField(builder, "hasDisposeHelper", finding.HasDisposeHelper, true, 6);
                AppendJsonField(builder, "hasSentinelRegister", finding.HasSentinelRegister, true, 6);
                AppendJsonField(builder, "hasSentinelUnregister", finding.HasSentinelUnregister, true, 6);
                AppendJsonField(builder, "strictViolation", finding.StrictViolation, false, 6);
                builder.Append(i + 1 < findings.Length ? "    }," : "    }");
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void AppendJsonField(StringBuilder builder, string key, string value, bool comma, int indent = 2)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(EscapeJson(key)).Append("\": ");
            if (value == null)
                builder.Append("null");
            else
                builder.Append('"').Append(EscapeJson(value)).Append('"');
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonField(StringBuilder builder, string key, int value, bool comma, int indent = 2)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(EscapeJson(key)).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonField(StringBuilder builder, string key, bool value, bool comma, int indent = 2)
        {
            AppendIndent(builder, indent);
            builder.Append('"').Append(EscapeJson(key)).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendIndent(StringBuilder builder, int count)
        {
            for (int i = 0; i < count; i++)
                builder.Append(' ');
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public struct Finding
        {
            public string Path;
            public int AllocationHits;
            public bool HasDirectDispose;
            public bool HasDisposeHelper;
            public bool HasSentinelRegister;
            public bool HasSentinelUnregister;
            public bool StrictViolation;
        }

        public struct ScanResult
        {
            public string ScanRoot;
            public string Error;
            public int FilesScanned;
            public int AllocationFiles;
            public int AllocationHits;
            public int StrictViolatorCount;
            public int HelperOnlyCount;
            public int RegisterWithoutUnregisterCount;
            public Finding[] Findings;
        }
    }
}
#endif
