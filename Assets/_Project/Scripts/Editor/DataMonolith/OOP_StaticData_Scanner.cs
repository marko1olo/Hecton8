#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorValidation
{
    public static class OOP_StaticData_Scanner
    {
        private const string AgentId = "X_002";
        private const string AgentId1313 = "1313";
        private const string ReportPath = "Docs/Reports/DATA_PIPELINE_OPTIMIZATION_REPORT_X_002.json";
        private const string ReportPath1313 = "Docs/Reports/DATA_PIPELINE_OPTIMIZATION_REPORT_1313.json";
        private const int MaxFindingsWritten = 512;

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts"
        };

        [MenuItem("Hecton8/Data Monolith/Run Static Data Scanner")]
        public static void RunFromMenu()
        {
            int findings = Run();
            Debug.Log("[OOP_StaticData_Scanner] findings=" + findings + " report=" + ReportPath);
        }

        public static int Run()
        {
            string projectRoot = ResolveProjectRoot();
            ScanStats stats = default;
            StringBuilder findings = new StringBuilder(32768);

            for (int i = 0; i < ScanRoots.Length; i++)
                ScanRoot(projectRoot, ScanRoots[i], ref stats, findings);

            WriteText(Path.Combine(projectRoot, ReportPath), BuildReport(stats, findings, AgentId));
            WriteText(Path.Combine(projectRoot, ReportPath1313), BuildReport(stats, findings, AgentId1313));
            AssetDatabase.Refresh();
            return stats.ProductionFindingCount;
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo directory = Directory.GetParent(Application.dataPath);
            return directory == null ? string.Empty : directory.FullName;
        }

        private static void ScanRoot(string projectRoot, string relativeRoot, ref ScanStats stats, StringBuilder findings)
        {
            string rootPath = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(rootPath))
            {
                stats.MissingRoots++;
                return;
            }

            string[] files = Directory.GetFiles(rootPath, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string relativePath = MakeRelative(projectRoot, files[i]);
                bool editorOnly = IsEditorOrTestPath(relativePath);
                stats.FilesScanned++;
                if (editorOnly)
                    stats.EditorFilesScanned++;
                else
                    stats.ProductionFilesScanned++;

                ScanFile(projectRoot, files[i], relativePath, editorOnly, ref stats, findings);
            }
        }

        private static void ScanFile(
            string projectRoot,
            string path,
            string relativePath,
            bool editorOnly,
            ref ScanStats stats,
            StringBuilder findings)
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception exception)
            {
                stats.ParseFailures++;
                AppendFinding(findings, ref stats, editorOnly, "roslynParseFailure", relativePath, 0, exception.GetType().Name, "Parser");
                ScanTextFallback(source, relativePath, editorOnly, ref stats, findings);
                return;
            }

            bool hasErrors = false;
            using (System.Collections.Generic.IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    if (diagnostics.Current.Severity == DiagnosticSeverity.Error)
                    {
                        hasErrors = true;
                        break;
                    }
                }
            }

            if (hasErrors)
            {
                stats.ParseFailures++;
                AppendFinding(findings, ref stats, editorOnly, "roslynSyntaxError", relativePath, 0, "syntax error", "Parser");
                ScanTextFallback(source, relativePath, editorOnly, ref stats, findings);
                return;
            }

            stats.AstParsedFiles++;
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (node is InvocationExpressionSyntax invocation)
                    {
                        string kind = ClassifyInvocation(invocation);
                        if (!string.IsNullOrEmpty(kind))
                            AppendFinding(findings, ref stats, editorOnly, kind, relativePath, LineOf(node), Trim(invocation.ToString()), "RoslynAST");
                    }
                    else if (node is MethodDeclarationSyntax method)
                    {
                        string kind = ClassifyMethodDeclaration(method);
                        if (!string.IsNullOrEmpty(kind))
                            AppendFinding(findings, ref stats, editorOnly, kind, relativePath, LineOf(node), Trim(method.Identifier.ValueText), "RoslynAST");
                    }
                    else if (node is ObjectCreationExpressionSyntax creation)
                    {
                        string kind = ClassifyObjectCreation(creation);
                        if (!string.IsNullOrEmpty(kind))
                            AppendFinding(findings, ref stats, editorOnly, kind, relativePath, LineOf(node), Trim(creation.ToString()), "RoslynAST");
                    }
                }
            }
        }

        private static string ClassifyInvocation(InvocationExpressionSyntax invocation)
        {
            string expression = invocation.Expression.ToString();
            string member = ResolveInvocationMemberName(invocation);

            if (string.Equals(member, "Parse", StringComparison.Ordinal) ||
                string.Equals(member, "TryParse", StringComparison.Ordinal))
            {
                if (expression.IndexOf("float.", StringComparison.Ordinal) >= 0 ||
                    expression.IndexOf("double.", StringComparison.Ordinal) >= 0 ||
                    expression.IndexOf("int.", StringComparison.Ordinal) >= 0 ||
                    expression.IndexOf("uint.", StringComparison.Ordinal) >= 0 ||
                    expression.IndexOf("long.", StringComparison.Ordinal) >= 0 ||
                    expression.IndexOf("ulong.", StringComparison.Ordinal) >= 0 ||
                    expression.IndexOf("Enum.", StringComparison.Ordinal) >= 0)
                {
                    return "managedScalarParse";
                }
            }

            if (string.Equals(member, "Split", StringComparison.Ordinal))
                return "managedStringSplit";

            if (IsCsvRouteName(member) || IsCsvRouteName(expression))
                return "csvParserRoute";

            if (expression.IndexOf("File.ReadAllText", StringComparison.Ordinal) >= 0 ||
                expression.IndexOf("File.ReadAllLines", StringComparison.Ordinal) >= 0 ||
                expression.IndexOf("File.ReadAllBytes", StringComparison.Ordinal) >= 0)
            {
                return "managedWholeFileRead";
            }

            if (expression.IndexOf("JsonUtility.FromJson", StringComparison.Ordinal) >= 0 ||
                expression.IndexOf("JsonConvert.DeserializeObject", StringComparison.Ordinal) >= 0)
            {
                return "managedJsonDeserialize";
            }

            return string.Empty;
        }

        private static string ClassifyMethodDeclaration(MethodDeclarationSyntax method)
        {
            return IsCsvRouteName(method.Identifier.ValueText) ? "csvParserRouteDeclaration" : string.Empty;
        }

        private static string ClassifyObjectCreation(ObjectCreationExpressionSyntax creation)
        {
            string typeName = creation.Type.ToString();
            if (string.Equals(typeName, "StreamReader", StringComparison.Ordinal) ||
                string.Equals(typeName, "FileStream", StringComparison.Ordinal) ||
                typeName.EndsWith(".StreamReader", StringComparison.Ordinal) ||
                typeName.EndsWith(".FileStream", StringComparison.Ordinal))
            {
                return "managedFileReader";
            }

            return string.Empty;
        }

        private static bool IsCsvRouteName(string value)
        {
            if (string.IsNullOrEmpty(value) ||
                value.IndexOf("Csv", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            return value.IndexOf("TryLoad", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("TryReload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("TryApply", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("TryIngest", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Reload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Load", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Parse", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ScanTextFallback(
            string source,
            string relativePath,
            bool editorOnly,
            ref ScanStats stats,
            StringBuilder findings)
        {
            if (source.IndexOf(".Parse(", StringComparison.Ordinal) >= 0 ||
                source.IndexOf(".TryParse(", StringComparison.Ordinal) >= 0)
            {
                AppendFinding(findings, ref stats, editorOnly, "tokenScalarParse", relativePath, 0, "Parse/TryParse token", "TokenFallback");
            }

            if (source.IndexOf("ReadAllText", StringComparison.Ordinal) >= 0 ||
                source.IndexOf("ReadAllLines", StringComparison.Ordinal) >= 0 ||
                source.IndexOf("ReadAllBytes", StringComparison.Ordinal) >= 0 ||
                source.IndexOf("FromJson", StringComparison.Ordinal) >= 0)
            {
                AppendFinding(findings, ref stats, editorOnly, "tokenWholeFileOrJson", relativePath, 0, "whole-file/json token", "TokenFallback");
            }

            if (IsCsvRouteName(source))
            {
                AppendFinding(findings, ref stats, editorOnly, "tokenCsvLoaderRoute", relativePath, 0, "Csv parser route token", "TokenFallback");
            }
        }

        private static void AppendFinding(
            StringBuilder builder,
            ref ScanStats stats,
            bool editorOnly,
            string kind,
            string path,
            int line,
            string source,
            string parser)
        {
            stats.TotalFindingCount++;
            if (editorOnly)
                stats.EditorFindingCount++;
            else
                stats.ProductionFindingCount++;

            if (stats.WrittenFindingCount >= MaxFindingsWritten)
                return;

            if (builder.Length > 0)
                builder.AppendLine(",");

            builder.Append("    { \"kind\": \"").Append(kind)
                .Append("\", \"path\": \"").Append(Escape(path))
                .Append("\", \"line\": ").Append(line)
                .Append(", \"scope\": \"").Append(editorOnly ? "editor_or_test" : "production")
                .Append("\", \"parser\": \"").Append(parser)
                .Append("\", \"source\": \"").Append(Escape(source)).Append("\" }");
            stats.WrittenFindingCount++;
        }

        private static string BuildReport(ScanStats stats, StringBuilder findings, string agentId)
        {
            StringBuilder report = new StringBuilder(65536);
            report.AppendLine("{");
            report.AppendLine("  \"schema\": \"HECTON8_DATA_PIPELINE_OPTIMIZATION_REPORT_V1\",");
            report.AppendLine("  \"agent\": \"" + agentId + "\",");
            report.AppendLine("  \"scanner\": \"OOP_StaticData_Scanner\",");
            report.AppendLine("  \"mode\": \"Roslyn AST with token fallback\",");
            report.AppendLine("  \"status\": \"STATIC_SOURCE\",");
            report.AppendLine("  \"policy\": \"Production static data parsing must migrate to static_data.h8bin or explicit editor-only bake paths.\",");
            report.AppendLine("  \"filesScanned\": " + stats.FilesScanned + ",");
            report.AppendLine("  \"productionFilesScanned\": " + stats.ProductionFilesScanned + ",");
            report.AppendLine("  \"editorFilesScanned\": " + stats.EditorFilesScanned + ",");
            report.AppendLine("  \"astParsedFiles\": " + stats.AstParsedFiles + ",");
            report.AppendLine("  \"parseFailures\": " + stats.ParseFailures + ",");
            report.AppendLine("  \"missingRoots\": " + stats.MissingRoots + ",");
            report.AppendLine("  \"productionFindingCount\": " + stats.ProductionFindingCount + ",");
            report.AppendLine("  \"editorFindingCount\": " + stats.EditorFindingCount + ",");
            report.AppendLine("  \"totalFindingCount\": " + stats.TotalFindingCount + ",");
            report.AppendLine("  \"writtenFindingLimit\": " + MaxFindingsWritten + ",");
            report.AppendLine("  \"writtenFindingCount\": " + stats.WrittenFindingCount + ",");
            report.AppendLine("  \"findings\": [");
            report.Append(findings);
            report.AppendLine();
            report.AppendLine("  ]");
            report.AppendLine("}");
            return report.ToString();
        }

        private static string ResolveInvocationMemberName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.ValueText;

            return invocation.Expression.ToString();
        }

        private static bool IsEditorOrTestPath(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Tests/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/EditorValidation/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith(".Editor.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static int LineOf(SyntaxNode node)
        {
            FileLinePositionSpan span = node.SyntaxTree.GetLineSpan(node.Span);
            return span.StartLinePosition.Line + 1;
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

        private static string Trim(string value)
        {
            string trimmed = value.Replace("\r", string.Empty).Replace("\n", " ").Trim();
            return trimmed.Length <= 180 ? trimmed : trimmed.Substring(0, 180);
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
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
        }

        private struct ScanStats
        {
            public int FilesScanned;
            public int ProductionFilesScanned;
            public int EditorFilesScanned;
            public int AstParsedFiles;
            public int ParseFailures;
            public int MissingRoots;
            public int ProductionFindingCount;
            public int EditorFindingCount;
            public int TotalFindingCount;
            public int WrittenFindingCount;
        }
    }
}
#endif
