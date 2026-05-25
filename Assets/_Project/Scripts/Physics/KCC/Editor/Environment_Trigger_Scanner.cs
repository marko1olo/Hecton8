#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.KCC.Editor
{
    internal static class Environment_Trigger_Scanner
    {
        private static readonly string[] Patterns =
        {
            "OnTriggerStay",
            "Rigidbody.AddForce",
            ".AddForce(",
            "CharacterController.slopeLimit",
            "CharacterController.Move",
            "slopeLimit",
            "Physics.Raycast",
            "Vector3.down"
        };

        [MenuItem("HECTON-8/Kinematics/Scan Environment Physics Debt")]
        public static void Scan()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string reportDirectory = Path.Combine(root, "Docs", "Reports");
            string reportPath = Path.Combine(reportDirectory, "PHYSICS_OPTIMIZATION_REPORT.json");
            string dedicatedReportPath = Path.Combine(reportDirectory, "PHYSICS_OPTIMIZATION_REPORT_SHINOBU_250.json");
            string previousReportPath = Path.Combine(reportDirectory, "PHYSICS_OPTIMIZATION_REPORT_PREVIOUS_SHINOBU_250.json");
            Directory.CreateDirectory(reportDirectory);
            if (File.Exists(reportPath))
                File.Copy(reportPath, previousReportPath, true);

            StringBuilder builder = new StringBuilder(16384);
            builder.Append("{\n");
            builder.Append("  \"agent\": \"SHINOBU_250\",\n");
            builder.Append("  \"domain\": \"KCC_ENVIRONMENTAL_INTEGRATOR\",\n");
            builder.Append("  \"summary\": \"OOP Environment Forces Purged from KCC authority path; listed rows are static debt candidates outside the new Burst route\",\n");
            builder.Append("  \"scannerMode\": \"ROSLYN_AST_WITH_TOKEN_FALLBACK\",\n");
            builder.Append("  \"astParser\": true,\n");
            builder.Append("  \"roslynCore\": \"").Append(Escape(typeof(SyntaxTree).Assembly.GetName().Name)).Append("\",\n");
            builder.Append("  \"roslynCSharp\": \"").Append(Escape(typeof(CSharpSyntaxTree).Assembly.GetName().Name)).Append("\",\n");
            builder.Append("  \"previousReport\": \"").Append(Escape(previousReportPath)).Append("\",\n");
            builder.Append("  \"dedicatedReport\": \"").Append(Escape(dedicatedReportPath)).Append("\",\n");
            builder.Append("  \"scanRoot\": \"").Append(Escape(scriptRoot)).Append("\",\n");
            builder.Append("  \"findings\": [\n");

            bool first = true;
            int findingCount = 0;
            int scannedFiles = 0;
            int parserFailures = 0;
            if (Directory.Exists(scriptRoot))
            {
                string[] files = Directory.GetFiles(scriptRoot, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string path = files[fileIndex];
                    if (Path.GetFileName(path).Equals(nameof(Environment_Trigger_Scanner) + ".cs", StringComparison.Ordinal))
                        continue;

                    scannedFiles++;
                    string text = File.ReadAllText(path);
                    try
                    {
                        SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
                        if (HasSyntaxErrors(tree))
                        {
                            parserFailures++;
                            ScanText(path, text, builder, ref first, ref findingCount);
                        }
                        else
                        {
                            ScanSyntaxTree(path, tree.GetCompilationUnitRoot(), builder, ref first, ref findingCount);
                        }
                    }
                    catch (Exception)
                    {
                        parserFailures++;
                        ScanText(path, text, builder, ref first, ref findingCount);
                    }
                }
            }

            builder.Append("\n  ],\n");
            builder.Append("  \"findingCount\": ").Append(findingCount).Append(",\n");
            builder.Append("  \"scannedFiles\": ").Append(scannedFiles).Append(",\n");
            builder.Append("  \"parserFailures\": ").Append(parserFailures).Append('\n');
            builder.Append("}\n");
            string json = builder.ToString();
            File.WriteAllText(dedicatedReportPath, json, Encoding.UTF8);
            WriteMergedCanonicalReport(reportPath, previousReportPath, json);
            Debug.Log($"[SHINOBU_250] Environment physics debt scan wrote {reportPath} and {dedicatedReportPath}");
        }

        private static bool HasSyntaxErrors(SyntaxTree tree)
        {
            foreach (Diagnostic diagnostic in tree.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        private static void WriteMergedCanonicalReport(string reportPath, string previousReportPath, string scannerJson)
        {
            string existing = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
            if (File.Exists(reportPath))
                File.Copy(reportPath, previousReportPath, true);

            string merged = MergeTopLevelObject(existing, "shinobu250KccEnvironmentScanner", scannerJson);
            File.WriteAllText(reportPath, merged, Encoding.UTF8);
        }

        private static string MergeTopLevelObject(string existingJson, string propertyName, string propertyJson)
        {
            string source = string.IsNullOrWhiteSpace(existingJson) ? "{\n}" : existingJson.TrimEnd();
            if (!LooksLikeJsonObject(source))
            {
                StringBuilder wrapper = new StringBuilder(propertyJson.Length + source.Length + 512);
                wrapper.Append("{\n");
                AppendJsonProperty(wrapper, "previousCanonicalRawJson", Escape(source), true);
                wrapper.Append(",\n");
                AppendRawJsonProperty(wrapper, propertyName, propertyJson, false);
                wrapper.Append("\n}\n");
                return wrapper.ToString();
            }

            if (TryFindTopLevelPropertyRange(source, propertyName, out int removeStart, out int removeEnd))
                source = source.Remove(removeStart, removeEnd - removeStart).TrimEnd();

            int closeIndex = FindLastNonWhitespace(source);
            if (closeIndex < 0 || source[closeIndex] != '}')
                return "{\n  \"" + propertyName + "\": " + propertyJson.Trim() + "\n}\n";

            string prefix = source.Substring(0, closeIndex).TrimEnd();
            bool hasExistingProperties = HasTopLevelContent(prefix);
            StringBuilder merged = new StringBuilder(source.Length + propertyJson.Length + 256);
            merged.Append(prefix);
            merged.Append(hasExistingProperties ? ",\n" : "\n");
            AppendRawJsonProperty(merged, propertyName, propertyJson, false);
            merged.Append("\n}\n");
            return merged.ToString();
        }

        private static bool LooksLikeJsonObject(string value)
        {
            int first = FindFirstNonWhitespace(value);
            int last = FindLastNonWhitespace(value);
            return first >= 0 && last > first && value[first] == '{' && value[last] == '}';
        }

        private static bool HasTopLevelContent(string value)
        {
            int open = value.IndexOf('{');
            if (open < 0)
                return false;

            for (int i = open + 1; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return true;
            }

            return false;
        }

        private static void AppendJsonProperty(StringBuilder builder, string name, string value, bool quoteValue)
        {
            builder.Append("  \"").Append(name).Append("\": ");
            if (quoteValue)
                builder.Append('"').Append(value).Append('"');
            else
                builder.Append(value);
        }

        private static void AppendRawJsonProperty(StringBuilder builder, string name, string rawJson, bool leadingComma)
        {
            if (leadingComma)
                builder.Append(",\n");

            string[] lines = rawJson.Trim().Replace("\r\n", "\n").Split('\n');
            builder.Append("  \"").Append(name).Append("\": ").Append(lines.Length > 0 ? lines[0].TrimEnd() : "{}");
            for (int i = 1; i < lines.Length; i++)
                builder.Append('\n').Append("  ").Append(lines[i].TrimEnd());
        }

        private static bool TryFindTopLevelPropertyRange(string json, string propertyName, out int rangeStart, out int rangeEnd)
        {
            rangeStart = -1;
            rangeEnd = -1;
            bool inString = false;
            bool escape = false;
            int depth = 0;

            for (int i = 0; i < json.Length; i++)
            {
                char ch = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (ch == '\\')
                    {
                        escape = true;
                    }
                    else if (ch == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (ch == '"')
                {
                    if (depth == 1 && TryReadJsonString(json, i, out string key, out int stringEnd) &&
                        key.Equals(propertyName, StringComparison.Ordinal))
                    {
                        int colon = FindNextNonWhitespace(json, stringEnd + 1);
                        if (colon >= 0 && json[colon] == ':')
                        {
                            int valueStart = FindNextNonWhitespace(json, colon + 1);
                            if (valueStart < 0)
                                return false;
                            int valueEnd = FindJsonValueEnd(json, valueStart);
                            rangeStart = i;
                            rangeEnd = valueEnd;
                            ExpandPropertyRangeForComma(json, ref rangeStart, ref rangeEnd);
                            return true;
                        }
                    }

                    inString = true;
                    continue;
                }

                if (ch == '{' || ch == '[')
                    depth++;
                else if (ch == '}' || ch == ']')
                    depth--;
            }

            return false;
        }

        private static bool TryReadJsonString(string json, int start, out string value, out int end)
        {
            value = string.Empty;
            end = start;
            if (start < 0 || start >= json.Length || json[start] != '"')
                return false;

            StringBuilder builder = new StringBuilder(64);
            bool escape = false;
            for (int i = start + 1; i < json.Length; i++)
            {
                char ch = json[i];
                if (escape)
                {
                    builder.Append(ch);
                    escape = false;
                    continue;
                }

                if (ch == '\\')
                {
                    escape = true;
                    continue;
                }

                if (ch == '"')
                {
                    value = builder.ToString();
                    end = i;
                    return true;
                }

                builder.Append(ch);
            }

            return false;
        }

        private static int FindJsonValueEnd(string json, int start)
        {
            bool inString = false;
            bool escape = false;
            int nestedDepth = 0;
            for (int i = Math.Max(0, start); i < json.Length; i++)
            {
                char ch = json[i];
                if (inString)
                {
                    if (escape)
                        escape = false;
                    else if (ch == '\\')
                        escape = true;
                    else if (ch == '"')
                        inString = false;
                    continue;
                }

                if (ch == '"')
                {
                    inString = true;
                    continue;
                }

                if (ch == '{' || ch == '[')
                {
                    nestedDepth++;
                    continue;
                }

                if (ch == '}' || ch == ']')
                {
                    if (nestedDepth == 0)
                        return i;
                    nestedDepth--;
                    continue;
                }

                if (ch == ',' && nestedDepth == 0)
                    return i;
            }

            return json.Length;
        }

        private static void ExpandPropertyRangeForComma(string json, ref int rangeStart, ref int rangeEnd)
        {
            int previous = FindPreviousNonWhitespace(json, rangeStart - 1);
            if (previous >= 0 && json[previous] == ',')
            {
                rangeStart = previous;
                return;
            }

            int next = FindNextNonWhitespace(json, rangeEnd);
            if (next >= 0 && json[next] == ',')
                rangeEnd = next + 1;
        }

        private static int FindFirstNonWhitespace(string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return i;
            }

            return -1;
        }

        private static int FindLastNonWhitespace(string value)
        {
            for (int i = value.Length - 1; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return i;
            }

            return -1;
        }

        private static int FindNextNonWhitespace(string value, int start)
        {
            for (int i = Math.Max(0, start); i < value.Length; i++)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return i;
            }

            return -1;
        }

        private static int FindPreviousNonWhitespace(string value, int start)
        {
            for (int i = Math.Min(start, value.Length - 1); i >= 0; i--)
            {
                if (!char.IsWhiteSpace(value[i]))
                    return i;
            }

            return -1;
        }

        private static void ScanSyntaxTree(string path, CompilationUnitSyntax root, StringBuilder builder, ref bool first, ref int findingCount)
        {
            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!TryResolveForbiddenNode(node, out string pattern))
                    continue;

                FileLinePositionSpan span = node.SyntaxTree.GetLineSpan(node.Span);
                AppendFinding(path, span.StartLinePosition.Line + 1, pattern, "ROSLYN_AST", builder, ref first, ref findingCount);
            }
        }

        private static bool TryResolveForbiddenNode(SyntaxNode node, out string pattern)
        {
            pattern = string.Empty;
            if (node is MethodDeclarationSyntax method &&
                method.Identifier.ValueText.Equals("OnTriggerStay", StringComparison.Ordinal))
            {
                pattern = "OnTriggerStay";
                return true;
            }

            if (node is InvocationExpressionSyntax invocation)
            {
                string expression = invocation.Expression.ToString();
                if (expression.Equals("Rigidbody.AddForce", StringComparison.Ordinal) ||
                    expression.EndsWith(".AddForce", StringComparison.Ordinal) ||
                    expression.Equals("AddForce", StringComparison.Ordinal))
                {
                    pattern = expression.Equals("Rigidbody.AddForce", StringComparison.Ordinal)
                        ? "Rigidbody.AddForce"
                        : ".AddForce(";
                    return true;
                }

                if (expression.Equals("Physics.Raycast", StringComparison.Ordinal) ||
                    expression.EndsWith(".Raycast", StringComparison.Ordinal) &&
                    expression.IndexOf("Physics", StringComparison.Ordinal) >= 0)
                {
                    pattern = "Physics.Raycast";
                    return true;
                }

                if (expression.Equals("CharacterController.Move", StringComparison.Ordinal) ||
                    expression.EndsWith(".Move", StringComparison.Ordinal) &&
                    expression.IndexOf("CharacterController", StringComparison.Ordinal) >= 0)
                {
                    pattern = "CharacterController.Move";
                    return true;
                }
            }

            if (node is MemberAccessExpressionSyntax memberAccess)
            {
                string memberText = memberAccess.ToString();
                string memberName = memberAccess.Name.Identifier.ValueText;
                if (memberText.Equals("Vector3.down", StringComparison.Ordinal))
                {
                    pattern = "Vector3.down";
                    return true;
                }

                if (memberText.Equals("CharacterController.slopeLimit", StringComparison.Ordinal) ||
                    memberName.Equals("slopeLimit", StringComparison.Ordinal))
                {
                    pattern = memberText.Equals("CharacterController.slopeLimit", StringComparison.Ordinal)
                        ? "CharacterController.slopeLimit"
                        : "slopeLimit";
                    return true;
                }
            }

            if (node is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText.Equals("slopeLimit", StringComparison.Ordinal))
            {
                pattern = "slopeLimit";
                return true;
            }

            return false;
        }

        private static void ScanText(string path, string text, StringBuilder builder, ref bool first, ref int findingCount)
        {
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool verbatimString = false;
            bool charLiteral = false;
            bool escape = false;
            int line = 1;

            for (int index = 0; index < text.Length; index++)
            {
                char ch = text[index];
                char next = index + 1 < text.Length ? text[index + 1] : '\0';
                char next2 = index + 2 < text.Length ? text[index + 2] : '\0';

                if (lineComment)
                {
                    if (ch == '\n')
                    {
                        line++;
                        lineComment = false;
                    }
                    continue;
                }

                if (blockComment)
                {
                    if (ch == '\n')
                        line++;
                    if (ch == '*' && next == '/')
                    {
                        blockComment = false;
                        index++;
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (ch == '\n')
                        line++;
                    if (verbatimString)
                    {
                        if (ch == '"' && next == '"')
                        {
                            index++;
                        }
                        else if (ch == '"')
                        {
                            stringLiteral = false;
                            verbatimString = false;
                        }
                    }
                    else if (escape)
                    {
                        escape = false;
                    }
                    else if (ch == '\\')
                    {
                        escape = true;
                    }
                    else if (ch == '"')
                    {
                        stringLiteral = false;
                    }
                    continue;
                }

                if (charLiteral)
                {
                    if (ch == '\n')
                        line++;
                    if (escape)
                        escape = false;
                    else if (ch == '\\')
                        escape = true;
                    else if (ch == '\'')
                        charLiteral = false;
                    continue;
                }

                if (ch == '\n')
                {
                    line++;
                    continue;
                }
                if (ch == '/' && next == '/')
                {
                    lineComment = true;
                    index++;
                    continue;
                }
                if (ch == '/' && next == '*')
                {
                    blockComment = true;
                    index++;
                    continue;
                }
                if (ch == '@' && next == '"')
                {
                    stringLiteral = true;
                    verbatimString = true;
                    index++;
                    continue;
                }
                if ((ch == '$' && next == '@' && next2 == '"') || (ch == '@' && next == '$' && next2 == '"'))
                {
                    stringLiteral = true;
                    verbatimString = true;
                    index += 2;
                    continue;
                }
                if ((ch == '$' && next == '"') || ch == '"')
                {
                    stringLiteral = true;
                    verbatimString = false;
                    if (ch == '$')
                        index++;
                    continue;
                }
                if (ch == '\'')
                {
                    charLiteral = true;
                    continue;
                }

                for (int patternIndex = 0; patternIndex < Patterns.Length; patternIndex++)
                {
                    string pattern = Patterns[patternIndex];
                    if (!StartsWithAt(text, index, pattern))
                        continue;

                    AppendFinding(path, line, pattern, "TOKEN_FALLBACK", builder, ref first, ref findingCount);
                    index += pattern.Length - 1;
                    break;
                }
            }
        }

        private static void AppendFinding(string path, int line, string pattern, string parser, StringBuilder builder, ref bool first, ref int findingCount)
        {
            if (!first)
                builder.Append(",\n");
            first = false;
            findingCount++;
            builder.Append("    { \"file\": \"").Append(Escape(Path.GetFullPath(path))).Append("\", \"line\": ")
                .Append(line)
                .Append(", \"pattern\": \"").Append(Escape(pattern)).Append("\", \"parser\": \"").Append(Escape(parser)).Append("\" }");
        }

        private static bool StartsWithAt(string text, int index, string pattern)
        {
            if (index < 0 || pattern.Length == 0 || index > text.Length - pattern.Length)
                return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (text[index + i] != pattern[i])
                    return false;
            }

            return true;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }
    }
}
#endif
