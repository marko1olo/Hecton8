#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEditor.Build;

namespace Hecton8.Editor
{
    public static class Arm64LayoutSourceFixer
    {
        private const string ReportPath = "Docs/Reports/ARM64_LAYOUT_SOURCE_FIXER_REPORT.txt";
        private static readonly string[] Roots =
        {
            "Assets/_Project/Scripts/Core",
            "Assets/_Project/Scripts/Physics"
        };

        [MenuItem("Hecton8/Diagnostics/Run ARM64 Layout Source Fixer CLI")]
        public static void RunCli()
        {
            string report = RunInternal(strict: true);
            if (report.IndexOf("[BLOCKED]", StringComparison.Ordinal) >= 0)
                throw new BuildFailedException(report);

            UnityEngine.Debug.Log(report);
        }

        [MenuItem("Hecton8/Diagnostics/Run ARM64 Layout Source Fixer Report")]
        public static void RunReportOnly()
        {
            UnityEngine.Debug.Log(RunInternal(strict: false));
        }

        private static string RunInternal(bool strict)
        {
            int filesVisited = 0;
            int packAttributesRemoved = 0;
            int blockedSequential = 0;
            int parserFailures = 0;
            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("ARM64_LAYOUT_SOURCE_FIXER");
            report.AppendLine("Roots: Core, Physics");
            report.AppendLine("Parser=ROSLYN_AST");
            report.AppendLine("SequentialAutoRewrite=BLOCKED_UNLESS_MANUAL_LAYOUT_PROOF_EXISTS");
            AppendRoslynAssemblyReport(report);

            for (int rootIndex = 0; rootIndex < Roots.Length; rootIndex++)
            {
                string root = Roots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string path = files[fileIndex];
                    filesVisited++;
                    string original = File.ReadAllText(path);
                    SyntaxTree tree;
                    try
                    {
                        tree = CSharpSyntaxTree.ParseText(original);
                    }
                    catch (Exception exception)
                    {
                        parserFailures++;
                        report.Append("[BLOCKED] AST_BINDING ");
                        report.Append(path);
                        report.Append(" :: ");
                        report.Append(exception.GetType().FullName);
                        report.Append(": ");
                        report.AppendLine(exception.Message);
                        continue;
                    }

                    CompilationUnitSyntax rootNode = tree.GetCompilationUnitRoot();
                    Arm64LayoutSyntaxRewriter rewriter = new Arm64LayoutSyntaxRewriter(path, report);
                    SyntaxNode fixedRoot = rewriter.Visit(rootNode);
                    packAttributesRemoved += rewriter.PackAttributesRemoved;
                    blockedSequential += rewriter.BlockedSequentialCandidates;

                    if (rewriter.HasChanges && fixedRoot != null)
                    {
                        string fixedText = fixedRoot.ToFullString();
                        if (!string.Equals(original, fixedText, StringComparison.Ordinal))
                            File.WriteAllText(path, fixedText);
                    }
                }
            }

            report.Insert(0, "FilesVisited=" + filesVisited + "\nPackAttributesRemoved=" + packAttributesRemoved + "\n");
            report.AppendLine("ParserFailures=" + parserFailures);
            report.AppendLine("BlockedSequentialCandidates=" + blockedSequential);
            report.AppendLine(strict ? "Mode=STRICT" : "Mode=REPORT_ONLY");

            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(ReportPath, report.ToString());
            return report.ToString();
        }

        private static void AppendRoslynAssemblyReport(StringBuilder report)
        {
            report.Append("RoslynCore=");
            report.Append(typeof(SyntaxTree).Assembly.GetName().Name);
            report.Append(" ");
            report.Append(typeof(SyntaxTree).Assembly.GetName().Version);
            report.AppendLine();
            report.Append("RoslynCSharp=");
            report.Append(typeof(CSharpSyntaxTree).Assembly.GetName().Name);
            report.Append(" ");
            report.Append(typeof(CSharpSyntaxTree).Assembly.GetName().Version);
            report.AppendLine();
        }

        private static bool IsDtoName(string name)
        {
            return name.IndexOf("DTO", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Payload", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Signal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Telemetry", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("Record", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   name.IndexOf("State", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private sealed class Arm64LayoutSyntaxRewriter : CSharpSyntaxRewriter
        {
            private readonly string _path;
            private readonly StringBuilder _report;

            public Arm64LayoutSyntaxRewriter(string path, StringBuilder report)
            {
                _path = path;
                _report = report;
            }

            public int PackAttributesRemoved { get; private set; }
            public int BlockedSequentialCandidates { get; private set; }
            public bool HasChanges { get; private set; }

            public override SyntaxNode VisitAttribute(AttributeSyntax node)
            {
                AttributeSyntax visited = (AttributeSyntax)base.VisitAttribute(node);
                if (!IsStructLayoutAttribute(visited) || !HasLayoutKind(visited, "LayoutKind.Explicit"))
                    return visited;

                AttributeArgumentListSyntax arguments = visited.ArgumentList;
                if (arguments == null || arguments.Arguments.Count == 0)
                    return visited;

                bool removedPackArgument = false;
                List<AttributeArgumentSyntax> kept = null;
                for (int i = 0; i < arguments.Arguments.Count; i++)
                {
                    AttributeArgumentSyntax argument = arguments.Arguments[i];
                    if (IsPackArgument(argument))
                    {
                        PackAttributesRemoved++;
                        removedPackArgument = true;
                        continue;
                    }

                    if (kept == null)
                        kept = new List<AttributeArgumentSyntax>(arguments.Arguments.Count);
                    kept.Add(argument);
                }

                if (!removedPackArgument || kept == null || kept.Count == arguments.Arguments.Count)
                    return visited;

                HasChanges = true;
                return visited.WithArgumentList(arguments.WithArguments(SyntaxFactory.SeparatedList(kept)));
            }

            public override SyntaxNode VisitStructDeclaration(StructDeclarationSyntax node)
            {
                if (HasSequentialStructLayout(node) && IsDtoName(node.Identifier.ValueText))
                {
                    BlockedSequentialCandidates++;
                    _report.Append("[BLOCKED] AST ");
                    _report.Append(_path);
                    _report.Append(" :: ");
                    _report.Append(ResolveFullName(node));
                    _report.Append(" uses LayoutKind.Sequential. Manual Explicit rewrite required; unsafe offset synthesis rejected. InstanceFields=");
                    _report.Append(CountInstanceFields(node));
                    _report.Append(" Properties=");
                    _report.Append(CountProperties(node));
                    _report.AppendLine();
                }

                return base.VisitStructDeclaration(node);
            }

            private static bool HasSequentialStructLayout(StructDeclarationSyntax node)
            {
                for (int listIndex = 0; listIndex < node.AttributeLists.Count; listIndex++)
                {
                    SeparatedSyntaxList<AttributeSyntax> attributes = node.AttributeLists[listIndex].Attributes;
                    for (int attributeIndex = 0; attributeIndex < attributes.Count; attributeIndex++)
                    {
                        AttributeSyntax attribute = attributes[attributeIndex];
                        if (IsStructLayoutAttribute(attribute) && HasLayoutKind(attribute, "LayoutKind.Sequential"))
                            return true;
                    }
                }

                return false;
            }

            private static bool IsStructLayoutAttribute(AttributeSyntax attribute)
            {
                string name = attribute.Name.ToString();
                return string.Equals(name, "StructLayout", StringComparison.Ordinal) ||
                       string.Equals(name, "StructLayoutAttribute", StringComparison.Ordinal) ||
                       name.EndsWith(".StructLayout", StringComparison.Ordinal) ||
                       name.EndsWith(".StructLayoutAttribute", StringComparison.Ordinal);
            }

            private static bool HasLayoutKind(AttributeSyntax attribute, string layoutKind)
            {
                AttributeArgumentListSyntax arguments = attribute.ArgumentList;
                if (arguments == null)
                    return false;

                for (int i = 0; i < arguments.Arguments.Count; i++)
                {
                    if (arguments.Arguments[i].Expression.ToString().IndexOf(layoutKind, StringComparison.Ordinal) >= 0)
                        return true;
                }

                return false;
            }

            private static bool IsPackArgument(AttributeArgumentSyntax argument)
            {
                return argument.NameEquals != null &&
                       string.Equals(argument.NameEquals.Name.Identifier.ValueText, "Pack", StringComparison.Ordinal);
            }

            private static int CountInstanceFields(StructDeclarationSyntax node)
            {
                int count = 0;
                for (int i = 0; i < node.Members.Count; i++)
                {
                    FieldDeclarationSyntax field = node.Members[i] as FieldDeclarationSyntax;
                    if (field == null || HasModifier(field.Modifiers, "static"))
                        continue;

                    count += field.Declaration.Variables.Count;
                }

                return count;
            }

            private static int CountProperties(StructDeclarationSyntax node)
            {
                int count = 0;
                for (int i = 0; i < node.Members.Count; i++)
                {
                    if (node.Members[i] is PropertyDeclarationSyntax)
                        count++;
                }

                return count;
            }

            private static bool HasModifier(SyntaxTokenList modifiers, string modifier)
            {
                for (int i = 0; i < modifiers.Count; i++)
                {
                    if (string.Equals(modifiers[i].ValueText, modifier, StringComparison.Ordinal))
                        return true;
                }

                return false;
            }

            private static string ResolveFullName(StructDeclarationSyntax node)
            {
                string name = node.Identifier.ValueText;
                SyntaxNode current = node.Parent;
                while (current != null)
                {
                    NamespaceDeclarationSyntax namespaceNode = current as NamespaceDeclarationSyntax;
                    if (namespaceNode != null)
                        return namespaceNode.Name + "." + name;

                    FileScopedNamespaceDeclarationSyntax fileNamespaceNode = current as FileScopedNamespaceDeclarationSyntax;
                    if (fileNamespaceNode != null)
                        return fileNamespaceNode.Name + "." + name;

                    current = current.Parent;
                }

                return name;
            }
        }
    }
}
#endif
