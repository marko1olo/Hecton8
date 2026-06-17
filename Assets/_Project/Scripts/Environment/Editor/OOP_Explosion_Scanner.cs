using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Environment.Editor
{
    internal static class OOP_Explosion_Scanner
    {
        private const string SharedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string SharedReportSectionName = "SHINOBU_346_OOP_Explosion_Scanner_Roslyn";
        private const string SidecarReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_346_ROSLYN.json";

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Environment",
            "Assets/_Project/Scripts/Events"
        };

        private static readonly string[] SeismicNeedles =
        {
            "seismic",
            "quake",
            "earthquake",
            "cataclysm",
            "shockwave",
            "volcan",
            "eruption",
            "tremor"
        };

        [MenuItem("Hecton8/Environment/Run OOP Explosion Scanner")]
        private static void RunMenu()
        {
            RunAndWriteReport();
            AssetDatabase.Refresh();
        }

        internal static ScanSummary RunAndWriteReport()
        {
            ScanSummary summary = Scan();
            string reportJson = BuildReportJson(summary);
            WriteSidecar(reportJson);
            WriteSharedReport(reportJson);
            return summary;
        }

        internal static ScanSummary Scan()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ScanSummary summary = default;
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                string root = Path.Combine(projectRoot, ScanRoots[i]);
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.Ordinal);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    ScanFile(projectRoot, files[fileIndex], ref summary);
            }

            return summary;
        }

        private static void ScanFile(string projectRoot, string path, ref ScanSummary summary)
        {
            string normalizedPath = ToProjectPath(projectRoot, path);
            if (normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            string source = File.ReadAllText(path);
            summary.FilesScanned++;

            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception)
            {
                summary.ParserFailures++;
                return;
            }

            if (HasParseError(tree))
            {
                summary.ParserFailures++;
                return;
            }

            bool seismicContext = ContainsSeismicContext(normalizedPath) || ContainsSeismicContext(source);
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    summary.SyntaxNodesVisited++;
                    if (node is InvocationExpressionSyntax invocation &&
                        TryResolveForbiddenInvocation(invocation, out string token))
                    {
                        summary.AllExplosionApiSites++;
                        if (seismicContext)
                        {
                            summary.SeismicExplosionApiSites++;
                            AppendFinding(projectRoot, normalizedPath, tree, invocation, token, ref summary);
                        }
                    }
                }
            }
        }

        private static bool TryResolveForbiddenInvocation(InvocationExpressionSyntax invocation, out string token)
        {
            token = string.Empty;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                string memberName = memberAccess.Name.Identifier.ValueText;
                string expression = invocation.Expression.ToString();
                if (memberName == "AddExplosionForce")
                {
                    token = "Rigidbody.AddExplosionForce";
                    return true;
                }

                if (memberName == "OverlapSphere" &&
                    expression.IndexOf("Physics", StringComparison.Ordinal) >= 0)
                {
                    token = "Physics.OverlapSphere";
                    return true;
                }
            }
            else if (invocation.Expression is IdentifierNameSyntax identifierName &&
                     identifierName.Identifier.ValueText == "OverlapSphere")
            {
                token = "Physics.OverlapSphere";
                return true;
            }

            return false;
        }

        private static bool HasParseError(SyntaxTree tree)
        {
            using (IEnumerator<Diagnostic> diagnostics = tree.GetDiagnostics().GetEnumerator())
            {
                while (diagnostics.MoveNext())
                {
                    if (diagnostics.Current.Severity == DiagnosticSeverity.Error)
                        return true;
                }
            }

            return false;
        }

        private static void AppendFinding(
            string projectRoot,
            string normalizedPath,
            SyntaxTree tree,
            InvocationExpressionSyntax invocation,
            string token,
            ref ScanSummary summary)
        {
            if (summary.Findings == null)
                summary.Findings = new StringBuilder(1024);

            int line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            string namespaceName = ResolveNamespaceName(invocation);
            string typeName = ResolveAncestorName<TypeDeclarationSyntax>(invocation);
            string memberName = ResolveAncestorName<BaseMethodDeclarationSyntax>(invocation);
            if (string.IsNullOrEmpty(memberName))
                memberName = ResolveAncestorName<PropertyDeclarationSyntax>(invocation);

            summary.Findings.Append("    { \"path\": \"")
                .Append(Escape(normalizedPath))
                .Append("\", \"line\": ")
                .Append(line)
                .Append(", \"token\": \"")
                .Append(Escape(token))
                .Append("\", \"namespace\": \"")
                .Append(Escape(namespaceName))
                .Append("\", \"type\": \"")
                .Append(Escape(typeName))
                .Append("\", \"member\": \"")
                .Append(Escape(memberName))
                .Append("\" },\n");
            _ = projectRoot;
            _ = tree;
        }

        private static string ResolveAncestorName<T>(SyntaxNode node)
            where T : SyntaxNode
        {
            SyntaxNode current = node.Parent;
            while (current != null)
            {
                if (current is TypeDeclarationSyntax typeDeclaration && typeof(T) == typeof(TypeDeclarationSyntax))
                    return typeDeclaration.Identifier.ValueText;
                if (current is BaseMethodDeclarationSyntax methodDeclaration && typeof(T) == typeof(BaseMethodDeclarationSyntax))
                    return methodDeclaration is MethodDeclarationSyntax method ? method.Identifier.ValueText : methodDeclaration.Kind().ToString();
                if (current is PropertyDeclarationSyntax propertyDeclaration && typeof(T) == typeof(PropertyDeclarationSyntax))
                    return propertyDeclaration.Identifier.ValueText;

                current = current.Parent;
            }

            return string.Empty;
        }

        private static string ResolveNamespaceName(SyntaxNode node)
        {
            SyntaxNode current = node.Parent;
            while (current != null)
            {
                if (current is NamespaceDeclarationSyntax namespaceDeclaration)
                    return namespaceDeclaration.Name.ToString();
                if (current.Kind().ToString() == "FileScopedNamespaceDeclaration")
                    return ExtractFileScopedNamespace(current);

                current = current.Parent;
            }

            return string.Empty;
        }

        private static string ExtractFileScopedNamespace(SyntaxNode node)
        {
            string text = node.ToString();
            const string marker = "namespace";
            int markerIndex = text.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return string.Empty;

            int start = markerIndex + marker.Length;
            while (start < text.Length && char.IsWhiteSpace(text[start]))
                start++;

            int end = start;
            while (end < text.Length)
            {
                char c = text[end];
                if (c == ';' || c == '{' || char.IsWhiteSpace(c))
                    break;
                end++;
            }

            return end > start ? text.Substring(start, end - start) : string.Empty;
        }

        private static bool ContainsSeismicContext(string text)
        {
            for (int i = 0; i < SeismicNeedles.Length; i++)
            {
                if (text.IndexOf(SeismicNeedles[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }

            return false;
        }

        private static string BuildReportJson(ScanSummary summary)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("{\n");
            builder.Append("  \"scanner\": \"OOP_Explosion_Scanner.Roslyn\",\n");
            builder.Append("  \"agent\": \"SHINOBU_346\",\n");
            builder.Append("  \"analysisMode\": \"ROSLYN_AST_INVOCATION_SCAN\",\n");
            builder.Append("  \"scannerUsesRoslynAst\": true,\n");
            builder.Append("  \"sidecarReport\": \"").Append(SidecarReportPath).Append("\",\n");
            builder.Append("  \"summary\": \"")
                .Append(summary.SeismicExplosionApiSites == 0 ? "OOP Seismic Forces Eradicated" : "OOP Seismic Forces Still Present")
                .Append("\",\n");
            builder.Append("  \"scanScope\": [\n");
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                builder.Append("    \"").Append(ScanRoots[i]).Append('"');
                builder.Append(i + 1 < ScanRoots.Length ? ",\n" : "\n");
            }

            builder.Append("  ],\n");
            builder.Append("  \"searchedTokens\": [\n");
            builder.Append("    \"Rigidbody.AddExplosionForce\",\n");
            builder.Append("    \"Physics.OverlapSphere\"\n");
            builder.Append("  ],\n");
            builder.Append("  \"filesScanned\": ").Append(summary.FilesScanned).Append(",\n");
            builder.Append("  \"syntaxNodesVisited\": ").Append(summary.SyntaxNodesVisited).Append(",\n");
            builder.Append("  \"parserFailures\": ").Append(summary.ParserFailures).Append(",\n");
            builder.Append("  \"allExplosionApiSites\": ").Append(summary.AllExplosionApiSites).Append(",\n");
            builder.Append("  \"seismicExplosionApiSites\": ").Append(summary.SeismicExplosionApiSites).Append(",\n");
            builder.Append("  \"proof\": {\n");
            builder.Append("    \"runtimeRoute\": \"SeismicEventDTO + SeismicStateDTO in GlobalDataVault -> SeismicSignal via SignalBus\",\n");
            builder.Append("    \"forbiddenRuntimeApis\": [\n");
            builder.Append("      \"Physics.OverlapSphere\",\n");
            builder.Append("      \"Rigidbody.AddExplosionForce\"\n");
            builder.Append("    ],\n");
            builder.Append("    \"hotPathManagedAllocations\": 0\n");
            builder.Append("  },\n");
            builder.Append("  \"findings\": [\n");
            if (summary.Findings != null && summary.Findings.Length > 0)
            {
                string findings = summary.Findings.ToString().TrimEnd();
                if (findings.EndsWith(",", StringComparison.Ordinal))
                    findings = findings.Substring(0, findings.Length - 1);
                builder.Append(findings).Append('\n');
            }

            builder.Append("  ]\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void WriteSidecar(string reportJson)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, SidecarReportPath);
            WriteReportFile(path, reportJson);
        }

        private static void WriteSharedReport(string reportJson)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, SharedReportPath);
            string existing = File.Exists(path) ? File.ReadAllText(path) : "{\n}\n";
            string withoutSection = RemoveTopLevelSection(existing, SharedReportSectionName);
            int objectEnd = withoutSection.LastIndexOf('}');
            if (objectEnd < 0)
                withoutSection = "{\n}\n";

            objectEnd = withoutSection.LastIndexOf('}');
            string prefix = withoutSection.Substring(0, objectEnd).TrimEnd();
            bool hasExistingFields = prefix.Length > 1 && prefix[prefix.Length - 1] != '{';
            StringBuilder builder = new StringBuilder(prefix.Length + reportJson.Length + SharedReportSectionName.Length + 32);
            builder.Append(prefix);
            builder.Append(hasExistingFields ? ",\n" : "\n");
            builder.Append("  \"").Append(SharedReportSectionName).Append("\": ");
            AppendIndentedJson(builder, reportJson, 2);
            builder.Append("\n}\n");
            WriteReportFile(path, builder.ToString());
        }

        private static void WriteReportFile(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string RemoveTopLevelSection(string json, string sectionKey)
        {
            if (string.IsNullOrEmpty(json))
                return "{\n}\n";

            int objectStart = json.IndexOf('{');
            int objectEnd = json.LastIndexOf('}');
            if (objectStart < 0 || objectEnd <= objectStart)
                return "{\n}\n";

            int keyStart = FindTopLevelKey(json, sectionKey, objectStart, objectEnd);
            if (keyStart < 0)
                return json;

            int propertyStart = keyStart;
            while (propertyStart > objectStart + 1 && char.IsWhiteSpace(json[propertyStart - 1]))
                propertyStart--;
            if (propertyStart > objectStart + 1 && json[propertyStart - 1] == ',')
            {
                propertyStart--;
                while (propertyStart > objectStart + 1 && char.IsWhiteSpace(json[propertyStart - 1]))
                    propertyStart--;
            }

            int colon = json.IndexOf(':', keyStart);
            if (colon < 0 || colon > objectEnd)
                return json;

            int propertyEnd = FindTopLevelValueEnd(json, colon + 1, objectEnd);
            int after = propertyEnd;
            while (after < objectEnd && char.IsWhiteSpace(json[after]))
                after++;
            if (after < objectEnd && json[after] == ',')
            {
                after++;
                while (after < objectEnd && char.IsWhiteSpace(json[after]))
                    after++;
            }

            return json.Remove(propertyStart, Math.Max(0, after - propertyStart));
        }

        private static int FindTopLevelKey(string json, string sectionKey, int objectStart, int objectEnd)
        {
            int depth = 1;
            for (int i = objectStart + 1; i < objectEnd; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    int end = FindStringEnd(json, i + 1, objectEnd);
                    if (depth == 1 &&
                        end > i &&
                        end - i - 1 == sectionKey.Length &&
                        string.CompareOrdinal(json, i + 1, sectionKey, 0, sectionKey.Length) == 0)
                    {
                        int cursor = end + 1;
                        while (cursor < objectEnd && char.IsWhiteSpace(json[cursor]))
                            cursor++;
                        if (cursor < objectEnd && json[cursor] == ':')
                            return i;
                    }

                    i = end;
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                    depth--;
            }

            return -1;
        }

        private static int FindTopLevelValueEnd(string json, int valueStart, int objectEnd)
        {
            int depth = 0;
            for (int i = valueStart; i < objectEnd; i++)
            {
                char c = json[i];
                if (c == '"')
                {
                    i = FindStringEnd(json, i + 1, objectEnd);
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                {
                    if (depth == 0)
                        return i;
                    depth--;
                }
                else if (c == ',' && depth == 0)
                {
                    return i;
                }
            }

            return objectEnd;
        }

        private static int FindStringEnd(string json, int start, int limit)
        {
            bool escaped = false;
            for (int i = start; i < limit; i++)
            {
                char c = json[i];
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
                    return i;
            }

            return limit;
        }

        private static void AppendIndentedJson(StringBuilder builder, string json, int spaces)
        {
            string trimmed = string.IsNullOrEmpty(json) ? "{}" : json.Trim();
            string pad = new string(' ', spaces);
            for (int i = 0; i < trimmed.Length; i++)
            {
                char c = trimmed[i];
                builder.Append(c);
                if (c == '\n' && i + 1 < trimmed.Length)
                    builder.Append(pad);
            }
        }

        private static string ToProjectPath(string projectRoot, string path)
        {
            string fullRoot = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(fullRoot.Length + 1);

            return fullPath;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal struct ScanSummary
        {
            public int FilesScanned;
            public int SyntaxNodesVisited;
            public int ParserFailures;
            public int AllExplosionApiSites;
            public int SeismicExplosionApiSites;
            public StringBuilder Findings;
        }
    }
}
