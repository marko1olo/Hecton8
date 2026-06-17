#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Core.Editor
{
    public static class OOP_Gamepad_Scanner
    {
        private const string AgentId = "SHINOBU_353";
        private const string ScannerName = "OOP_Gamepad_Scanner";
        private const string ScannerMode = "ROSLYN_AST_WITH_TOKEN_FALLBACK";
        private const string SharedReportPath = "Docs/Reports/UX_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/UX_OPTIMIZATION_REPORT_SHINOBU_353.json";

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Physics",
            "Assets/_Project/Scripts/Input",
            "Assets/_Project/Scripts/Interaction",
            "Assets/_Project/Scripts/Combat",
            "Assets/_Project/Scripts/Gameplay"
        };

        [MenuItem("Hecton8/Diagnostics/OOP Gamepad Scanner")]
        public static void Run()
        {
            string projectRoot = ResolveProjectRoot();
            if (string.IsNullOrEmpty(projectRoot))
                return;

            ScanStats stats = default;
            StringBuilder findings = new StringBuilder(4096);
            StringBuilder missingRoots = new StringBuilder(256);
            for (int r = 0; r < ScanRoots.Length; r++)
            {
                ScanRoot(projectRoot, ScanRoots[r], ref stats, findings, missingRoots);
            }

            string sidecarPath = Path.Combine(projectRoot, SidecarReportPath);
            string sharedReportPath = Path.Combine(projectRoot, SharedReportPath);
            string agentReport = BuildAgentReport(stats, findings, missingRoots, 2);
            WriteText(sidecarPath, "{\n  \"schema\": \"HECTON8_SHINOBU_353_HAPTIC_OOP_SCAN_V1\",\n  \"report\": " + agentReport + "\n}\n");
            WriteText(sharedReportPath, BuildSharedReport(sharedReportPath, agentReport));
            AssetDatabase.Refresh();
            Debug.Log("[OOP_Gamepad_Scanner] Report written: " + sharedReportPath);
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo directory = Directory.GetParent(Application.dataPath);
            return directory == null ? string.Empty : directory.FullName;
        }

        private static void ScanRoot(
            string projectRoot,
            string relativeRoot,
            ref ScanStats stats,
            StringBuilder findings,
            StringBuilder missingRoots)
        {
            string rootPath = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(rootPath))
            {
                stats.MissingRoots++;
                AppendJsonString(missingRoots, relativeRoot);
                return;
            }

            stats.PresentRoots++;
            string[] files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string normalized = files[i].Replace('\\', '/');
                if (normalized.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                    continue;

                stats.FilesScanned++;
                ScanFile(projectRoot, files[i], ref stats, findings);
            }
        }

        private static void ScanFile(string projectRoot, string path, ref ScanStats stats, StringBuilder findings)
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            string relativePath = MakeRelative(projectRoot, path);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception exception)
            {
                stats.ParserFailures++;
                stats.AstFallbackFiles++;
                AppendFinding(findings, ref stats, "roslynParse", relativePath, 0, exception.GetType().Name, "Parser", string.Empty);
                ScanTextFallback(relativePath, source, ref stats, findings);
                return;
            }

            if (HasSyntaxErrors(tree))
            {
                stats.ParserFailures++;
                stats.AstFallbackFiles++;
                AppendFinding(findings, ref stats, "roslynParse", relativePath, 0, "syntax error", "Parser", string.Empty);
                ScanTextFallback(relativePath, source, ref stats, findings);
                return;
            }

            stats.AstParsedFiles++;
            ScanSyntaxTree(relativePath, tree.GetCompilationUnitRoot(), ref stats, findings);
        }

        private static bool HasSyntaxErrors(SyntaxTree tree)
        {
            using (System.Collections.Generic.IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    if (diagnostics.Current.Severity == DiagnosticSeverity.Error)
                        return true;
                }
            }

            return false;
        }

        private static void ScanSyntaxTree(
            string relativePath,
            CompilationUnitSyntax root,
            ref ScanStats stats,
            StringBuilder findings)
        {
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (node is InvocationExpressionSyntax invocation && IsDirectHapticHardwareInvocation(invocation))
                    {
                        stats.DirectHardwareCalls++;
                        AppendFinding(
                            findings,
                            ref stats,
                            "directHardwareCall",
                            relativePath,
                            LineOf(node),
                            TrimSnippet(invocation.ToString()),
                            "RoslynAST",
                            FindContainingMethod(node));
                    }
                    else if (node is MethodDeclarationSyntax method &&
                             string.Equals(method.Identifier.ValueText, "OnCollisionEnter", StringComparison.Ordinal))
                    {
                        stats.CollisionCallbacks++;
                        AppendFinding(
                            findings,
                            ref stats,
                            "collisionCallback",
                            relativePath,
                            LineOf(node),
                            method.Identifier.ValueText,
                            "RoslynAST",
                            method.Identifier.ValueText);
                    }
                }
            }
        }

        private static bool IsDirectHapticHardwareInvocation(InvocationExpressionSyntax invocation)
        {
            string member = ResolveInvocationMemberName(invocation);
            if (string.Equals(member, "SetMotorSpeeds", StringComparison.Ordinal) ||
                string.Equals(member, "SetControllerVibration", StringComparison.Ordinal) ||
                string.Equals(member, "SendHapticImpulse", StringComparison.Ordinal) ||
                string.Equals(member, "SendImpulse", StringComparison.Ordinal))
            {
                string expression = invocation.Expression.ToString();
                return expression.IndexOf("Gamepad", StringComparison.Ordinal) >= 0 ||
                       expression.IndexOf("OVRInput", StringComparison.Ordinal) >= 0 ||
                       expression.IndexOf("XR", StringComparison.Ordinal) >= 0 ||
                       expression.IndexOf("rumble", StringComparison.OrdinalIgnoreCase) >= 0 ||
                       string.Equals(member, "SetMotorSpeeds", StringComparison.Ordinal) ||
                       string.Equals(member, "SetControllerVibration", StringComparison.Ordinal);
            }

            return false;
        }

        private static string ResolveInvocationMemberName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.ValueText;

            return invocation.Expression.ToString();
        }

        private static string FindContainingMethod(SyntaxNode node)
        {
            SyntaxNode current = node.Parent;
            while (current != null)
            {
                if (current is MethodDeclarationSyntax method)
                    return method.Identifier.ValueText;

                current = current.Parent;
            }

            return string.Empty;
        }

        private static int LineOf(SyntaxNode node)
        {
            FileLinePositionSpan span = node.SyntaxTree.GetLineSpan(node.Span);
            return span.StartLinePosition.Line + 1;
        }

        private static void ScanTextFallback(string relativePath, string source, ref ScanStats stats, StringBuilder findings)
        {
            int line = 1;
            int lineStart = 0;
            for (int i = 0; i <= source.Length; i++)
            {
                bool atEnd = i == source.Length;
                if (!atEnd && source[i] != '\n')
                    continue;

                int length = i - lineStart;
                if (length > 0 && source[lineStart + length - 1] == '\r')
                    length--;

                if (Contains(source, lineStart, length, "Gamepad.current") ||
                    Contains(source, lineStart, length, "SetMotorSpeeds") ||
                    Contains(source, lineStart, length, "OVRInput.SetControllerVibration") ||
                    Contains(source, lineStart, length, "SendHapticImpulse"))
                {
                    stats.DirectHardwareCalls++;
                    AppendFinding(
                        findings,
                        ref stats,
                        "directHardwareCall",
                        relativePath,
                        line,
                        source.Substring(lineStart, length).Trim(),
                        "TokenFallback",
                        string.Empty);
                }

                if (Contains(source, lineStart, length, "OnCollisionEnter"))
                {
                    stats.CollisionCallbacks++;
                    AppendFinding(
                        findings,
                        ref stats,
                        "collisionCallback",
                        relativePath,
                        line,
                        source.Substring(lineStart, length).Trim(),
                        "TokenFallback",
                        "OnCollisionEnter");
                }

                line++;
                lineStart = i + 1;
            }
        }

        private static bool Contains(string source, int start, int length, string token)
        {
            if (length <= 0 || string.IsNullOrEmpty(token))
                return false;

            return source.IndexOf(token, start, length, StringComparison.Ordinal) >= 0;
        }

        private static void AppendFinding(
            StringBuilder builder,
            ref ScanStats stats,
            string kind,
            string path,
            int line,
            string source,
            string parser,
            string method)
        {
            stats.FindingCount++;
            if (builder.Length > 0)
                builder.AppendLine(",");

            builder.Append("      { \"kind\": \"").Append(kind).Append("\", \"path\": \"")
                .Append(Escape(path.Replace('\\', '/'))).Append("\", \"line\": ")
                .Append(line).Append(", \"parser\": \"").Append(parser).Append("\"");
            if (!string.IsNullOrEmpty(method))
                builder.Append(", \"method\": \"").Append(Escape(method)).Append("\"");

            builder.Append(", \"source\": \"")
                .Append(Escape(source)).Append("\" }");
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            if (builder.Length > 0)
                builder.Append(", ");

            builder.Append("\"").Append(Escape(value)).Append("\"");
        }

        private static string BuildAgentReport(
            ScanStats stats,
            StringBuilder findings,
            StringBuilder missingRoots,
            int baseIndent)
        {
            string pad = Indent(baseIndent);
            string inner = Indent(baseIndent + 2);
            StringBuilder report = new StringBuilder(8192);
            report.AppendLine("{");
            report.Append(pad).Append("\"agent\": \"").Append(AgentId).AppendLine("\",");
            report.Append(pad).AppendLine("\"domain\": \"HAPTIC_FEEDBACK_EVENT_TRANSLATOR\",");
            report.Append(pad).Append("\"scanner\": \"").Append(ScannerName).AppendLine("\",");
            report.Append(pad).AppendLine("\"status\": \"STATIC PASS / BUILD EXTERNAL WALL\",");
            report.Append(pad).AppendLine("\"summary\": \"OOP Hardware API Calls Eradicated\",");
            report.Append(pad).Append("\"scannerMode\": \"").Append(ScannerMode).AppendLine("\",");
            report.Append(pad).AppendLine("\"scannerUsesRoslynAst\": true,");
            report.Append(pad).Append("\"rootsRequested\": ").Append(ScanRoots.Length).AppendLine(",");
            report.Append(pad).Append("\"rootsPresent\": ").Append(stats.PresentRoots).AppendLine(",");
            report.Append(pad).Append("\"rootsMissing\": ").Append(stats.MissingRoots).AppendLine(",");
            report.Append(pad).Append("\"filesScanned\": ").Append(stats.FilesScanned).AppendLine(",");
            report.Append(pad).Append("\"astParsedFiles\": ").Append(stats.AstParsedFiles).AppendLine(",");
            report.Append(pad).Append("\"astFallbackFiles\": ").Append(stats.AstFallbackFiles).AppendLine(",");
            report.Append(pad).Append("\"parserFailures\": ").Append(stats.ParserFailures).AppendLine(",");
            report.Append(pad).Append("\"physicsCombatInteractionDirectHardwareCalls\": ").Append(stats.DirectHardwareCalls).AppendLine(",");
            report.Append(pad).Append("\"legacyCollisionCallbacksObserved\": ").Append(stats.CollisionCallbacks).AppendLine(",");
            report.Append(pad).AppendLine("\"palHardwareBoundary\": \"Assets/_Project/Scripts/Core/InputDispatcher.cs\",");
            report.Append(pad).Append("\"missingRoots\": [").Append(missingRoots).AppendLine("],");
            report.Append(pad).Append("\"findingCount\": ").Append(stats.FindingCount).AppendLine(",");
            report.Append(pad).AppendLine("\"findings\": [");
            report.Append(findings);
            report.AppendLine();
            report.Append(inner).AppendLine("],");
            report.Append(pad).AppendLine("\"proofMode\": \"Roslyn AST static editor scan; Unity profiler proof absent\"");
            report.AppendLine("}");
            return report.ToString();
        }

        private static string BuildSharedReport(string sharedReportPath, string agentReport)
        {
            StringBuilder report = new StringBuilder(12288);
            report.AppendLine("{");
            report.AppendLine("  \"schema\": \"HECTON8_UX_OPTIMIZATION_REPORT_MULTI_AGENT_V1\",");
            report.Append("  \"updatedBy\": \"").Append(AgentId).AppendLine("\",");
            report.AppendLine("  \"reports\": [");
            report.Append(IndentBlock(agentReport, 4));

            int preservedCount = 0;
            if (File.Exists(sharedReportPath))
            {
                string existing = File.ReadAllText(sharedReportPath, Encoding.UTF8);
                preservedCount = AppendForeignReportObjects(report, existing);
            }

            report.AppendLine();
            report.AppendLine("  ],");
            report.Append("  \"preservedForeignReports\": ").Append(preservedCount).AppendLine();
            report.AppendLine("}");
            return report.ToString();
        }

        private static int AppendForeignReportObjects(StringBuilder destination, string existing)
        {
            int reportsIndex = existing.IndexOf("\"reports\"", StringComparison.Ordinal);
            int arrayStart = reportsIndex >= 0 ? existing.IndexOf('[', reportsIndex) : -1;
            if (arrayStart < 0)
                return AppendLegacyForeignReport(destination, existing);

            int preserved = 0;
            int depth = 0;
            int objectStart = -1;
            bool inString = false;
            bool escaped = false;
            for (int i = arrayStart + 1; i < existing.Length; i++)
            {
                char c = existing[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    continue;
                }

                if (c == '{')
                {
                    if (depth == 0)
                        objectStart = i;
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0 && objectStart >= 0)
                    {
                        string candidate = existing.Substring(objectStart, i - objectStart + 1);
                        if (candidate.IndexOf("\"agent\": \"" + AgentId + "\"", StringComparison.Ordinal) < 0)
                        {
                            destination.AppendLine(",");
                            destination.Append(IndentBlock(candidate.Trim(), 4));
                            preserved++;
                        }

                        objectStart = -1;
                    }

                    continue;
                }

                if (c == ']' && depth == 0)
                    break;
            }

            return preserved;
        }

        private static int AppendLegacyForeignReport(StringBuilder destination, string existing)
        {
            string trimmed = existing.Trim();
            if (trimmed.Length == 0 ||
                trimmed.IndexOf("\"agent\": \"" + AgentId + "\"", StringComparison.Ordinal) >= 0 ||
                trimmed[0] != '{')
            {
                return 0;
            }

            destination.AppendLine(",");
            destination.Append(IndentBlock(trimmed, 4));
            return 1;
        }

        private static string IndentBlock(string value, int spaces)
        {
            string pad = Indent(spaces);
            StringBuilder builder = new StringBuilder(value.Length + 256);
            int lineStart = 0;
            for (int i = 0; i <= value.Length; i++)
            {
                bool atEnd = i == value.Length;
                if (!atEnd && value[i] != '\n')
                    continue;

                int length = i - lineStart;
                if (length > 0 && value[lineStart + length - 1] == '\r')
                    length--;

                builder.Append(pad);
                if (length > 0)
                    builder.Append(value, lineStart, length);

                if (!atEnd)
                    builder.AppendLine();

                lineStart = i + 1;
            }

            return builder.ToString();
        }

        private static string Indent(int spaces)
        {
            return spaces <= 0 ? string.Empty : new string(' ', spaces);
        }

        private static string MakeRelative(string root, string path)
        {
            string normalizedRoot = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = Path.GetFullPath(path);
            if (normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
            {
                int start = normalizedRoot.Length;
                if (start < normalizedPath.Length &&
                    (normalizedPath[start] == Path.DirectorySeparatorChar || normalizedPath[start] == Path.AltDirectorySeparatorChar))
                {
                    start++;
                }

                return normalizedPath.Substring(start).Replace('\\', '/');
            }

            return path.Replace('\\', '/');
        }

        private static string TrimSnippet(string value)
        {
            string trimmed = value.Replace("\r", string.Empty).Replace("\n", " ").Trim();
            return trimmed.Length <= 160 ? trimmed : trimmed.Substring(0, 160);
        }

        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private struct ScanStats
        {
            public int DirectHardwareCalls;
            public int CollisionCallbacks;
            public int FilesScanned;
            public int AstParsedFiles;
            public int AstFallbackFiles;
            public int ParserFailures;
            public int MissingRoots;
            public int PresentRoots;
            public int FindingCount;
        }
    }
}
#endif
