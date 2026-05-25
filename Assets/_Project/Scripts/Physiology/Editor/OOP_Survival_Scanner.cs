#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    internal static class OOP_Survival_Scanner
    {
        private const string Summary = "OOP Survival Timers Audited";
        private const string ScannerMode = "ROSLYN_AST_WITH_TOKEN_FALLBACK";
        private const string SharedReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_320.json";
        private const string SharedSectionKey = "shinobu320SurvivalOopScanner";
        private const string SidecarSectionKey = "survivalOopScanner";

        private static readonly string[] s_roots =
        {
            "Assets/_Project/Scripts/Physiology",
            "Assets/_Project/Scripts/Player",
            "Assets/_Project/Scripts/HectonSurvivalSystem.cs",
            "Assets/_Project/Scripts/Gameplay",
            "Assets/_Project/Scripts/Physics/KCC"
        };

        private static readonly string[] s_forbidden =
        {
            "HungerTimer",
            "DecreaseHunger",
            "ApplyColdDamage",
            "WaitForSeconds",
            "UpdateHungerAndThirst",
            "HungerDrainRate",
            "ThirstDrainRate",
            "internalTemperatureTimeConstantSeconds",
            "StartCoroutine",
            "IEnumerator"
        };

        private static readonly string[] s_survivalIdentifiers =
        {
            "hunger",
            "Hunger",
            "thirst",
            "Thirst",
            "temperature",
            "Temperature",
            "CoreTemperature",
            "internalTemperature"
        };

        [MenuItem("Hecton8/Physiology/Run Survival OOP Scanner")]
        private static void RunMenu()
        {
            int findingCount = RunStaticScan(Application.dataPath);
            if (findingCount == 0)
                Debug.Log("[SHINOBU_320] OOP Survival Timers Eradicated");
            else
                Debug.LogWarning("[SHINOBU_320] OOP survival timer scan found legacy surfaces: " + findingCount);
        }

        internal static int RunStaticScan(string assetsPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(assetsPath, ".."));
            List<Finding> findings = new List<Finding>(32);
            int astParsedFiles = 0;
            int astFallbackFiles = 0;
            for (int i = 0; i < s_roots.Length; i++)
                ScanRoot(projectRoot, s_roots[i], findings, ref astParsedFiles, ref astFallbackFiles);

            UpsertReportSection(
                Path.Combine(projectRoot, SidecarReportPath),
                SidecarSectionKey,
                BuildSidecarSection(findings, astParsedFiles, astFallbackFiles));
            UpsertReportSection(
                Path.Combine(projectRoot, SharedReportPath),
                SharedSectionKey,
                BuildSharedSection(findings, astParsedFiles, astFallbackFiles));
            return findings.Count;
        }

        private static void ScanRoot(
            string projectRoot,
            string relativeRoot,
            List<Finding> findings,
            ref int astParsedFiles,
            ref int astFallbackFiles)
        {
            string root = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(root))
            {
                ScanFile(projectRoot, root, findings, ref astParsedFiles, ref astFallbackFiles);
                return;
            }

            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string normalized = files[i].Replace('\\', '/');
                if (normalized.EndsWith("/Editor/OOP_Survival_Scanner.cs", StringComparison.Ordinal))
                    continue;
                if (normalized.Contains("/Physiology/Editor/", StringComparison.Ordinal))
                    continue;

                ScanFile(projectRoot, files[i], findings, ref astParsedFiles, ref astFallbackFiles);
            }
        }

        private static void ScanFile(
            string projectRoot,
            string path,
            List<Finding> findings,
            ref int astParsedFiles,
            ref int astFallbackFiles)
        {
            string relative = MakeRelative(projectRoot, path);
            string text = File.ReadAllText(path);
            SyntaxTree tree = CSharpSyntaxTree.ParseText(text);
            if (!HasSyntaxErrors(tree))
            {
                astParsedFiles++;
                ScanSyntaxTree(relative, tree.GetCompilationUnitRoot(), findings);
                return;
            }

            astFallbackFiles++;
            string[] lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int patternIndex = 0; patternIndex < s_forbidden.Length; patternIndex++)
                {
                    string pattern = s_forbidden[patternIndex];
                    if (line.IndexOf(pattern, StringComparison.Ordinal) >= 0)
                        findings.Add(new Finding(relative, lineIndex + 1, pattern));
                }
            }
        }

        private static void ScanSyntaxTree(string relative, CompilationUnitSyntax root, List<Finding> findings)
        {
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (!TryResolveForbiddenNode(node, out string pattern))
                        continue;

                    int line = LineOf(node);
                    findings.Add(new Finding(relative, line, pattern));
                }
            }
        }

        private static bool TryResolveForbiddenNode(SyntaxNode node, out string pattern)
        {
            pattern = null;
            if (node is ClassDeclarationSyntax classDeclaration &&
                MatchesForbiddenToken(classDeclaration.Identifier.ValueText, out pattern))
            {
                return true;
            }

            if (node is ObjectCreationExpressionSyntax objectCreation &&
                MatchesForbiddenToken(objectCreation.Type.ToString(), out pattern))
            {
                return true;
            }

            if (node is InvocationExpressionSyntax invocation &&
                MatchesForbiddenToken(GetInvocationName(invocation), out pattern))
            {
                return true;
            }

            if (node is IdentifierNameSyntax identifierName &&
                MatchesForbiddenToken(identifierName.Identifier.ValueText, out pattern))
            {
                return true;
            }

            if (node is MethodDeclarationSyntax method &&
                IsManagedSurvivalUpdate(method, out pattern))
            {
                return true;
            }

            return false;
        }

        private static bool IsManagedSurvivalUpdate(MethodDeclarationSyntax method, out string pattern)
        {
            pattern = null;
            string name = method.Identifier.ValueText;
            if (!string.Equals(name, "Update", StringComparison.Ordinal) &&
                !string.Equals(name, "LateUpdate", StringComparison.Ordinal) &&
                !string.Equals(name, "FixedUpdate", StringComparison.Ordinal))
            {
                return false;
            }

            string body = method.Body != null ? method.Body.ToString() : (method.ExpressionBody != null ? method.ExpressionBody.ToString() : string.Empty);
            for (int i = 0; i < s_survivalIdentifiers.Length; i++)
            {
                if (body.IndexOf(s_survivalIdentifiers[i], StringComparison.Ordinal) < 0)
                    continue;

                pattern = name + "_SURVIVAL_TIMER_BODY";
                return true;
            }

            return false;
        }

        private static bool MatchesForbiddenToken(string token, out string pattern)
        {
            pattern = null;
            if (string.IsNullOrEmpty(token))
                return false;

            for (int i = 0; i < s_forbidden.Length; i++)
            {
                string forbidden = s_forbidden[i];
                if (token.IndexOf(forbidden, StringComparison.Ordinal) < 0)
                    continue;

                pattern = forbidden;
                return true;
            }

            return false;
        }

        private static bool HasSyntaxErrors(SyntaxTree tree)
        {
            IEnumerable<Diagnostic> diagnostics = tree.GetDiagnostics();
            using (IEnumerator<Diagnostic> enumerator = diagnostics.GetEnumerator())
            {
                while (enumerator.MoveNext())
                {
                    if (enumerator.Current.Severity == DiagnosticSeverity.Error)
                        return true;
                }
            }

            return false;
        }

        private static string GetInvocationName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                return memberAccess.Name.Identifier.ValueText;

            return invocation.Expression.ToString();
        }

        private static int LineOf(SyntaxNode node)
        {
            FileLinePositionSpan span = node.SyntaxTree.GetLineSpan(node.Span);
            return span.StartLinePosition.Line + 1;
        }

        private static string BuildSharedSection(List<Finding> findings, int astParsedFiles, int astFallbackFiles)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("  \"").Append(SharedSectionKey).AppendLine("\": {");
            AppendScannerBody(builder, findings, astParsedFiles, astFallbackFiles, 4);
            builder.Append("  }");
            return builder.ToString();
        }

        private static string BuildSidecarSection(List<Finding> findings, int astParsedFiles, int astFallbackFiles)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.Append("  \"").Append(SidecarSectionKey).AppendLine("\": {");
            AppendScannerBody(builder, findings, astParsedFiles, astFallbackFiles, 4);
            builder.Append("  }");
            return builder.ToString();
        }

        private static void AppendScannerBody(StringBuilder builder, List<Finding> findings, int astParsedFiles, int astFallbackFiles, int indent)
        {
            string pad = new string(' ', indent);
            string findingPad = new string(' ', indent + 2);
            builder.Append(pad).AppendLine("\"agent\": \"SHINOBU_320\",");
            builder.Append(pad).AppendLine("\"scanner\": \"OOP_Survival_Scanner\",");
            builder.Append(pad).Append("\"scannerMode\": \"").Append(ScannerMode).AppendLine("\",");
            builder.Append(pad).AppendLine("\"scannerUsesRoslynAst\": true,");
            builder.Append(pad).Append("\"astParsedFiles\": ").Append(astParsedFiles).AppendLine(",");
            builder.Append(pad).Append("\"astFallbackFiles\": ").Append(astFallbackFiles).AppendLine(",");
            builder.Append(pad).Append("\"summary\": \"").Append(Summary).AppendLine("\",");
            builder.Append(pad).Append("\"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.Append(pad).AppendLine("\"findings\": [");
            AppendFindings(builder, findings, indent + 2);
            builder.Append(findingPad).AppendLine("]");
        }

        private static void AppendFindings(StringBuilder builder, List<Finding> findings, int indent)
        {
            string pad = new string(' ', indent);
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                builder.Append(pad)
                    .Append("{ \"path\": \"").Append(EscapeJson(finding.Path)).Append("\", \"line\": ")
                    .Append(finding.Line)
                    .Append(", \"pattern\": \"").Append(EscapeJson(finding.Pattern)).Append("\" }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }
        }

        private static void UpsertReportSection(string path, string key, string section)
        {
            if (!File.Exists(path))
            {
                WriteText(path, "{\n" + section + "\n}\n");
                return;
            }

            string existing = File.ReadAllText(path);
            string quotedKey = "\"" + key + "\"";
            int keyIndex = existing.IndexOf(quotedKey, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int objectStart = existing.IndexOf('{', keyIndex);
                int objectEnd = objectStart >= 0 ? FindJsonObjectEnd(existing, objectStart) : -1;
                int replaceStart = existing.LastIndexOf('\n', keyIndex);
                replaceStart = replaceStart < 0 ? 0 : replaceStart + 1;
                if (objectStart >= 0 && objectEnd >= objectStart)
                {
                    WriteText(path, existing.Substring(0, replaceStart) + section + existing.Substring(objectEnd + 1));
                    return;
                }
            }

            int insert = existing.LastIndexOf('}');
            if (insert < 0)
            {
                WriteText(path, "{\n" + section + "\n}\n");
                return;
            }

            string prefix = existing.Substring(0, insert).TrimEnd();
            string suffix = existing.Substring(insert);
            string separator = prefix.EndsWith("{", StringComparison.Ordinal) ? "\n" : ",\n";
            WriteText(path, prefix + separator + section + "\n" + suffix);
        }

        private static int FindJsonObjectEnd(string text, int objectStart)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < text.Length; i++)
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

        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string MakeRelative(string root, string path)
        {
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private readonly struct Finding
        {
            public readonly string Path;
            public readonly int Line;
            public readonly string Pattern;

            public Finding(string path, int line, string pattern)
            {
                Path = path;
                Line = line;
                Pattern = pattern;
            }
        }
    }
}
#endif
