using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Inventory.Editor
{
    internal static class OOP_Cargo_Scanner
    {
        private const string ReportPath = "Docs/Reports/LOGISTICS_OPTIMIZATION_REPORT.json";

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Logistics",
            "Assets/_Project/Scripts/Vehicles"
        };

        [MenuItem("Hecton8/Inventory/Run OOP Cargo Scanner")]
        private static void RunMenu()
        {
            ScanToReport();
            AssetDatabase.Refresh();
        }

        internal static ScanSummary ScanToReport()
        {
            ScanSummary summary = Scan();
            WriteReport(summary);
            return summary;
        }

        internal static ScanSummary Scan()
        {
            ScanSummary summary = default;
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                string root = ScanRoots[i];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                    ScanFile(files[fileIndex], ref summary);
            }

            return summary;
        }

        private static void ScanFile(string path, ref ScanSummary summary)
        {
            string normalizedPath = path.Replace('\\', '/');
            if (normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            string source = File.ReadAllText(path);
            if (!LooksLikeCargoTransferSource(source))
                return;

            summary.FilesScanned++;
            try
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                ScanSyntaxTree(normalizedPath, tree, root, ref summary);
            }
            catch (Exception)
            {
                summary.ParserFailures++;
                ScanLexicalFallback(normalizedPath, ref summary);
            }
        }

        private static void ScanSyntaxTree(
            string path,
            SyntaxTree tree,
            CompilationUnitSyntax root,
            ref ScanSummary summary)
        {
            bool watchedNamespace = IsWatchedPath(path) || HasWatchedNamespace(root);
            if (!watchedNamespace)
                return;

            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    summary.SyntaxNodesVisited++;
                    if (TryResolveViolation(node, out string pattern))
                    {
                        RegisterViolation(path, LineOf(tree, node), pattern, ref summary);
                    }
                }
            }
        }

        private static bool HasWatchedNamespace(CompilationUnitSyntax root)
        {
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    string name = null;
                    if (node is NamespaceDeclarationSyntax namespaceNode)
                        name = namespaceNode.Name.ToString();
                    else if (node is FileScopedNamespaceDeclarationSyntax fileNamespaceNode)
                        name = fileNamespaceNode.Name.ToString();

                    if (!string.IsNullOrEmpty(name) &&
                        (name.StartsWith("Hecton8.Logistics", StringComparison.Ordinal) ||
                         name.StartsWith("Hecton8.Vehicles", StringComparison.Ordinal) ||
                         name.StartsWith("Hecton8.Inventory", StringComparison.Ordinal)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryResolveViolation(SyntaxNode node, out string pattern)
        {
            pattern = null;
            if (node is InvocationExpressionSyntax invocation)
            {
                string text = invocation.Expression.ToString();
                if (IsAddRangeInvocation(invocation))
                {
                    pattern = "List.AddRange";
                    return true;
                }

                if (text.IndexOf("TransferItems", StringComparison.Ordinal) >= 0)
                {
                    pattern = "TransferItems";
                    return true;
                }

                if (text.IndexOf("Inventory.Sync", StringComparison.Ordinal) >= 0)
                {
                    pattern = "Inventory.Sync";
                    return true;
                }
            }

            if (node is ForEachStatementSyntax forEach &&
                (forEach.Identifier.ValueText.IndexOf("item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                 forEach.Type.ToString().IndexOf("ItemData", StringComparison.Ordinal) >= 0))
            {
                pattern = "foreach-item-transfer";
                return true;
            }

            if (node is BinaryExpressionSyntax binary &&
                IsStringCategoryComparison(binary))
            {
                pattern = "string-category-filter";
                return true;
            }

            return false;
        }

        private static bool IsAddRangeInvocation(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.ValueText == "AddRange";

            return invocation.Expression.ToString().IndexOf(".AddRange", StringComparison.Ordinal) >= 0;
        }

        private static bool IsStringCategoryComparison(BinaryExpressionSyntax binary)
        {
            string left = binary.Left.ToString();
            string right = binary.Right.ToString();
            bool touchesCategory =
                left.IndexOf("Category", StringComparison.Ordinal) >= 0 ||
                right.IndexOf("Category", StringComparison.Ordinal) >= 0;
            if (!touchesCategory)
                return false;

            return binary.Left is LiteralExpressionSyntax ||
                   binary.Right is LiteralExpressionSyntax ||
                   left.IndexOf('"') >= 0 ||
                   right.IndexOf('"') >= 0;
        }

        private static int LineOf(SyntaxTree tree, SyntaxNode node)
        {
            FileLinePositionSpan span = tree.GetLineSpan(node.Span);
            return span.StartLinePosition.Line + 1;
        }

        private static void ScanLexicalFallback(string path, ref ScanSummary summary)
        {
            string[] lines = File.ReadAllLines(path);
            bool watchedNamespace = IsWatchedPath(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.IndexOf("namespace Hecton8.Logistics", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("namespace Hecton8.Vehicles", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("namespace Hecton8.Inventory", StringComparison.Ordinal) >= 0)
                {
                    watchedNamespace = true;
                }

                if (!watchedNamespace)
                    continue;

                if (line.IndexOf(".AddRange(", StringComparison.Ordinal) >= 0)
                    RegisterViolation(path, i + 1, "List.AddRange:LEXICAL_FALLBACK", ref summary);
                if (line.IndexOf("foreach", StringComparison.Ordinal) >= 0 &&
                    (line.IndexOf(" item", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     line.IndexOf("ItemData", StringComparison.Ordinal) >= 0))
                {
                    RegisterViolation(path, i + 1, "foreach-item-transfer:LEXICAL_FALLBACK", ref summary);
                }
            }
        }

        private static bool LooksLikeCargoTransferSource(string source)
        {
            return source.IndexOf("cargo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   source.IndexOf("inventory", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   source.IndexOf("transfer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   source.IndexOf("dock", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsWatchedPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Logistics/", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("/Vehicles/", StringComparison.Ordinal) >= 0 ||
                   normalized.IndexOf("/Inventory/", StringComparison.Ordinal) >= 0;
        }

        private static void RegisterViolation(string path, int line, string pattern, ref ScanSummary summary)
        {
            summary.Violations++;
            if (summary.FirstViolationPath == null)
            {
                summary.FirstViolationPath = path.Replace('\\', '/');
                summary.FirstViolationLine = line;
                summary.FirstViolationPattern = pattern;
            }
        }

        private static void WriteReport(in ScanSummary summary)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
            StringBuilder builder = new StringBuilder(512);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_344\",");
            builder.AppendLine("  \"domain\": \"CARGO_MANIFEST_CONTAINER_INVENTORY_SYNC\",");
            builder.Append("  \"status\": \"");
            builder.Append(summary.Violations == 0 ? "OOP Inventory Merges Eradicated" : "OOP Inventory Merge Violations Found");
            builder.AppendLine("\",");
            builder.Append("  \"filesScanned\": ");
            builder.Append(summary.FilesScanned);
            builder.AppendLine(",");
            builder.Append("  \"parser\": \"ROSLYN_CSHARP_SYNTAX_TREE_WITH_LEXICAL_FALLBACK\",");
            builder.AppendLine();
            builder.Append("  \"parserFailures\": ");
            builder.Append(summary.ParserFailures);
            builder.AppendLine(",");
            builder.Append("  \"syntaxNodesVisited\": ");
            builder.Append(summary.SyntaxNodesVisited);
            builder.AppendLine(",");
            builder.Append("  \"violations\": ");
            builder.Append(summary.Violations);
            builder.AppendLine(",");
            builder.Append("  \"firstViolationPath\": ");
            AppendJsonString(builder, summary.FirstViolationPath);
            builder.AppendLine(",");
            builder.Append("  \"firstViolationLine\": ");
            builder.Append(summary.FirstViolationLine);
            builder.AppendLine(",");
            builder.Append("  \"firstViolationPattern\": ");
            AppendJsonString(builder, summary.FirstViolationPattern);
            builder.AppendLine();
            builder.AppendLine("}");
            File.WriteAllText(ReportPath, builder.ToString(), Encoding.UTF8);
            Debug.Log("OOP_Cargo_Scanner wrote " + ReportPath);
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                builder.Append("null");
                return;
            }

            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                    builder.Append('\\');
                builder.Append(c);
            }
            builder.Append('"');
        }

        internal struct ScanSummary
        {
            public int FilesScanned;
            public int ParserFailures;
            public int SyntaxNodesVisited;
            public int Violations;
            public string FirstViolationPath;
            public int FirstViolationLine;
            public string FirstViolationPattern;
        }
    }
}
