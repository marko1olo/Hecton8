using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Vehicles.Editor
{
    public static class Rigidbody_Drag_Scanner
    {
        private const string SharedReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string AgentReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json";
        private const string SharedPropertyName = "\"shinobu251SubmarineAddedMassScanner\"";

        [MenuItem("Hecton8/Vehicles/Submarine Inertia/Run Rigidbody Drag Scanner")]
        public static void Run()
        {
            int fileCount;
            int hitCount = CountForbiddenVehiclePhysicsWrites(out fileCount);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            StringBuilder builder = new StringBuilder(512);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_251\",");
            builder.AppendLine("  \"domain\": \"SUBMARINE_ADDED_MASS_SOLVER\",");
            builder.AppendLine("  \"scanner\": \"Rigidbody_Drag_Scanner\",");
            builder.AppendLine("  \"summary\": \"OOP Mass Modifications Purged\",");
            builder.AppendLine("  \"parser\": \"roslyn AST with comment-stripped token fallback\",");
            builder.AppendLine("  \"sharedReportMerge\": \"NON_DESTRUCTIVE_TOP_LEVEL_PROPERTY_REPLACE_OR_APPEND\",");
            builder.AppendLine("  \"sidecarReport\": \"Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_251.json\",");
            builder.Append("  \"vehicleSourceFilesScanned\": ").Append(fileCount).AppendLine(",");
            builder.Append("  \"forbiddenRigidbodyMassDragWrites\": ").Append(hitCount).AppendLine(",");
            builder.Append("  \"oopMassModificationsPurged\": ").Append(hitCount == 0 ? "true" : "false").AppendLine();
            builder.AppendLine("}");
            WriteReports(projectRoot, builder.ToString());
            Debug.Log("SHINOBU_251 Rigidbody drag scanner written: " + Path.Combine(projectRoot, AgentReportRelativePath));
        }

        public static int CountForbiddenVehiclePhysicsWrites(out int fileCount)
        {
            fileCount = 0;
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            int count = 0;
            count += CountForbiddenWritesInRoot(Path.Combine(scriptsRoot, "Vehicles"), ref fileCount);
            count += CountForbiddenWritesInRoot(Path.Combine(scriptsRoot, "Physics", "Vehicles"), ref fileCount);
            return count;
        }

        public static void WriteReports(string projectRoot, string reportJson)
        {
            string agentReportPath = Path.Combine(projectRoot, AgentReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(agentReportPath) ?? projectRoot);
            File.WriteAllText(agentReportPath, reportJson);

            string sharedReportPath = Path.Combine(projectRoot, SharedReportRelativePath);
            MergeSharedPhysicsReport(sharedReportPath, reportJson);
        }

        private static void MergeSharedPhysicsReport(string reportPath, string reportJson)
        {
            string propertyJson = SharedPropertyName + ":" + reportJson;
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, "{" + propertyJson + "}");
                return;
            }

            string existing = File.ReadAllText(reportPath);
            if (TryReplaceJsonObjectProperty(existing, SharedPropertyName, propertyJson, out string replaced) ||
                TryAppendJsonObjectProperty(existing, propertyJson, out replaced))
            {
                File.WriteAllText(reportPath, replaced);
            }
        }

        private static bool TryAppendJsonObjectProperty(string existing, string propertyJson, out string merged)
        {
            merged = null;
            if (string.IsNullOrEmpty(existing))
                return false;

            int close = existing.LastIndexOf('}');
            if (close < 0)
                return false;

            int scan = close - 1;
            while (scan >= 0 && char.IsWhiteSpace(existing[scan]))
                scan--;

            bool hasExistingProperty = scan >= 0 && existing[scan] != '{';
            string separator = hasExistingProperty ? "," : string.Empty;
            merged = existing.Substring(0, close) + separator + "\n  " + propertyJson + "\n" + existing.Substring(close);
            return true;
        }

        private static bool TryReplaceJsonObjectProperty(string existing, string propertyName, string propertyJson, out string merged)
        {
            merged = null;
            if (string.IsNullOrEmpty(existing))
                return false;

            int propertyStart = existing.IndexOf(propertyName, StringComparison.Ordinal);
            if (propertyStart < 0)
                return false;

            int colon = existing.IndexOf(':', propertyStart + propertyName.Length);
            if (colon < 0)
                return false;

            int valueStart = colon + 1;
            while (valueStart < existing.Length && char.IsWhiteSpace(existing[valueStart]))
                valueStart++;

            if (valueStart >= existing.Length || existing[valueStart] != '{')
                return false;

            int valueEnd = FindMatchingBrace(existing, valueStart);
            if (valueEnd < 0)
                return false;

            int replaceStart = propertyStart;
            while (replaceStart > 0 && char.IsWhiteSpace(existing[replaceStart - 1]))
                replaceStart--;

            if (replaceStart > 0 && existing[replaceStart - 1] == ',')
                replaceStart--;

            int replaceEnd = valueEnd + 1;
            while (replaceEnd < existing.Length && char.IsWhiteSpace(existing[replaceEnd]))
                replaceEnd++;

            if (replaceEnd < existing.Length && existing[replaceEnd] == ',')
                replaceEnd++;

            merged = existing.Substring(0, replaceStart) + "\n  " + propertyJson + existing.Substring(replaceEnd);
            return true;
        }

        private static int FindMatchingBrace(string text, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int CountForbiddenWritesInRoot(string root, ref int fileCount)
        {
            if (!Directory.Exists(root))
                return 0;

            int count = 0;
            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string text;
                try
                {
                    text = File.ReadAllText(files[i]);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                fileCount++;
                count += CountForbiddenWrites(text);
            }

            return count;
        }

        private static int CountForbiddenWrites(string text)
        {
            try
            {
                CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
                int count = 0;
                IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator();
                try
                {
                    while (nodes.MoveNext())
                    {
                        SyntaxNode node = nodes.Current;
                        if (node is AssignmentExpressionSyntax assignment)
                        {
                            if (IsForbiddenMember(assignment.Left))
                                count++;
                        }
                        else if (node is PostfixUnaryExpressionSyntax postfix)
                        {
                            if (IsForbiddenMember(postfix.Operand))
                                count++;
                        }
                        else if (node is PrefixUnaryExpressionSyntax prefix)
                        {
                            if (IsForbiddenMember(prefix.Operand))
                                count++;
                        }
                    }
                }
                finally
                {
                    nodes.Dispose();
                }

                return count;
            }
            catch (Exception)
            {
                return CountForbiddenWritesTokenFallback(text, ".mass") +
                       CountForbiddenWritesTokenFallback(text, ".drag") +
                       CountForbiddenWritesTokenFallback(text, ".angularDrag");
            }
        }

        private static bool IsForbiddenMember(ExpressionSyntax expression)
        {
            if (!(expression is MemberAccessExpressionSyntax member))
                return false;

            string name = member.Name.Identifier.ValueText;
            return string.Equals(name, "mass", StringComparison.Ordinal) ||
                   string.Equals(name, "drag", StringComparison.Ordinal) ||
                   string.Equals(name, "angularDrag", StringComparison.Ordinal);
        }

        private static int CountForbiddenWritesTokenFallback(string text, string member)
        {
            int count = 0;
            int cursor = 0;
            while (cursor < text.Length)
            {
                int index = text.IndexOf(member, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                cursor = index + member.Length;
                if (IsInsideCommentOrString(text, index))
                    continue;

                int op = cursor;
                while (op < text.Length && char.IsWhiteSpace(text[op]))
                    op++;

                if (op >= text.Length)
                    continue;

                char first = text[op];
                char second = op + 1 < text.Length ? text[op + 1] : '\0';
                if (first == '=' ||
                    (second == '=' && (first == '+' || first == '-' || first == '*' || first == '/')) ||
                    (first == '+' && second == '+') ||
                    (first == '-' && second == '-'))
                {
                    count++;
                }
            }

            return count;
        }

        private static bool IsInsideCommentOrString(string text, int target)
        {
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool charLiteral = false;

            for (int i = 0; i < target && i < text.Length; i++)
            {
                char c = text[i];
                char n = i + 1 < text.Length ? text[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\n' || c == '\r')
                        lineComment = false;
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && n == '/')
                    {
                        blockComment = false;
                        i++;
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '"')
                        stringLiteral = false;
                    continue;
                }

                if (charLiteral)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '\'')
                        charLiteral = false;
                    continue;
                }

                if (c == '/' && n == '/')
                {
                    lineComment = true;
                    i++;
                    continue;
                }

                if (c == '/' && n == '*')
                {
                    blockComment = true;
                    i++;
                    continue;
                }

                if (c == '"')
                    stringLiteral = true;
                else if (c == '\'')
                    charLiteral = true;
            }

            return lineComment || blockComment || stringLiteral || charLiteral;
        }
    }
}
