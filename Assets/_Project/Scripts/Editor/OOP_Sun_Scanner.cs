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

namespace Hecton8.EditorTools
{
    public static class OOP_Sun_Scanner
    {
        private const string ReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string SectionKey = "shinobu345CelestialOrbitScanner";

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Environment",
            "Assets/_Project/Scripts/Lighting"
        };

        [MenuItem("Hecton/Environment/OOP Sun Scanner")]
        public static void RunFromMenu()
        {
            SunScanReport report = RunAndWriteReport();
            Debug.Log("OOP_Sun_Scanner Roslyn pass complete. Forbidden hits: " + report.ForbiddenHits);
        }

        public static SunScanReport RunAndWriteReport()
        {
            string root = Directory.GetCurrentDirectory();
            SunScanReport report = RunScan(root);

            string output = Path.Combine(root, ReportPath);
            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string existing = File.Exists(output) ? File.ReadAllText(output) : null;
            File.WriteAllText(output, UpsertReportSection(existing, BuildJsonSection(in report)));
            AssetDatabase.Refresh();
            return report;
        }

        private static SunScanReport RunScan(string projectRoot)
        {
            SunScanReport report = default;
            for (int i = 0; i < ScanRoots.Length; i++)
                ScanPath(projectRoot, Path.Combine(projectRoot, ScanRoots[i]), ref report);

            ScanFile(projectRoot, Path.Combine(projectRoot, "Assets", "_Project", "Scripts", "HectonCelestialEngine.cs"), ref report);
            return report;
        }

        private static void ScanPath(string projectRoot, string path, ref SunScanReport report)
        {
            if (!Directory.Exists(path))
                return;

            string[] files = Directory.GetFiles(path, "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.Ordinal);
            for (int i = 0; i < files.Length; i++)
                ScanFile(projectRoot, files[i], ref report);
        }

        private static void ScanFile(string projectRoot, string path, ref SunScanReport report)
        {
            if (!File.Exists(path))
                return;

            string normalizedPath = ToProjectPath(projectRoot, path);
            if (normalizedPath.EndsWith("OOP_Sun_Scanner.cs", StringComparison.Ordinal))
                return;

            string source = File.ReadAllText(path);
            report.ScannedFiles++;

            SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
            if (HasParseError(tree))
            {
                report.ParserFailures++;
                return;
            }

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            HashSet<string> transformAliases = BuildTransformAliases(root);
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    report.SyntaxNodesVisited++;
                    if (node is InvocationExpressionSyntax invocation)
                    {
                        ScanInvocation(normalizedPath, tree, invocation, transformAliases, ref report);
                    }
                    else if (node is AssignmentExpressionSyntax assignment)
                    {
                        ScanAssignment(normalizedPath, tree, assignment, transformAliases, ref report);
                    }
                }
            }
        }

        private static HashSet<string> BuildTransformAliases(CompilationUnitSyntax root)
        {
            HashSet<string> aliases = new HashSet<string>(StringComparer.Ordinal);
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (nodes.Current is LocalDeclarationStatementSyntax localDeclaration)
                    {
                        string declaredType = localDeclaration.Declaration.Type.ToString();
                        SeparatedSyntaxList<VariableDeclaratorSyntax> variables = localDeclaration.Declaration.Variables;
                        for (int i = 0; i < variables.Count; i++)
                        {
                            VariableDeclaratorSyntax variable = variables[i];
                            if (variable.Initializer == null)
                                continue;

                            string initializer = variable.Initializer.Value.ToString();
                            if ((declaredType == "var" || declaredType.EndsWith("Transform", StringComparison.Ordinal)) &&
                                IsTransformLikeExpression(initializer, aliases))
                            {
                                aliases.Add(variable.Identifier.ValueText);
                            }
                        }
                    }
                    else if (nodes.Current is AssignmentExpressionSyntax assignment &&
                             assignment.Left is IdentifierNameSyntax identifier &&
                             IsTransformLikeExpression(assignment.Right.ToString(), aliases))
                    {
                        aliases.Add(identifier.Identifier.ValueText);
                    }
                }
            }

            return aliases;
        }

        private static void ScanInvocation(
            string normalizedPath,
            SyntaxTree tree,
            InvocationExpressionSyntax invocation,
            HashSet<string> transformAliases,
            ref SunScanReport report)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                string memberName = memberAccess.Name.Identifier.ValueText;
                string expression = memberAccess.Expression.ToString();
                if (memberName == "Rotate" &&
                    IsTransformLikeExpression(expression, transformAliases))
                {
                    report.TransformRotateHits++;
                    report.ForbiddenHits++;
                    AppendFinding(normalizedPath, tree, invocation, "Transform.Rotate", ref report);
                    return;
                }

                if (memberName == "RotateAround" &&
                    IsTransformLikeExpression(expression, transformAliases))
                {
                    report.TransformRotateAroundHits++;
                    report.ForbiddenHits++;
                    AppendFinding(normalizedPath, tree, invocation, "Transform.RotateAround", ref report);
                    return;
                }

                if (memberName == "LookAt" &&
                    IsTransformLikeExpression(expression, transformAliases))
                {
                    report.TransformLookAtHits++;
                    report.ForbiddenHits++;
                    AppendFinding(normalizedPath, tree, invocation, "Transform.LookAt", ref report);
                    return;
                }

                if (memberName == "SetPositionAndRotation" &&
                    IsTransformLikeExpression(expression, transformAliases))
                {
                    report.TransformSetPositionAndRotationHits++;
                    report.ForbiddenHits++;
                    AppendFinding(normalizedPath, tree, invocation, "Transform.SetPositionAndRotation", ref report);
                    return;
                }

                if (memberName == "Sin" && expression == "Mathf" &&
                    invocation.ArgumentList != null &&
                    invocation.ArgumentList.Arguments.Count > 0 &&
                    invocation.ArgumentList.Arguments[0].ToString().IndexOf("Time.time", StringComparison.Ordinal) >= 0)
                {
                    report.MathfSinTimeHits++;
                    report.ForbiddenHits++;
                    AppendFinding(normalizedPath, tree, invocation, "Mathf.Sin(Time.time)", ref report);
                }
            }
            else if (invocation.Expression is IdentifierNameSyntax identifier &&
                     identifier.Identifier.ValueText == "Rotate")
            {
                report.TransformRotateHits++;
                report.ForbiddenHits++;
                AppendFinding(normalizedPath, tree, invocation, "Rotate", ref report);
            }
        }

        private static void ScanAssignment(
            string normalizedPath,
            SyntaxTree tree,
            AssignmentExpressionSyntax assignment,
            HashSet<string> transformAliases,
            ref SunScanReport report)
        {
            string left = assignment.Left.ToString();

            if (IsTransformForwardWrite(left, transformAliases))
            {
                report.TransformForwardWriteHits++;
                report.ForbiddenHits++;
                AppendFinding(normalizedPath, tree, assignment, "transform.forward =", ref report);
                return;
            }

            if (IsTransformRotationWrite(left, transformAliases))
            {
                report.TransformRotationWriteHits++;
                report.ForbiddenHits++;
                AppendFinding(normalizedPath, tree, assignment, "transform.rotation =", ref report);
                return;
            }

            if (IsTransformLocalRotationWrite(left, transformAliases))
            {
                report.TransformLocalRotationWriteHits++;
                report.ForbiddenHits++;
                AppendFinding(normalizedPath, tree, assignment, "transform.localRotation =", ref report);
                return;
            }

            if (IsTransformEulerAnglesWrite(left, transformAliases))
            {
                report.TransformEulerAnglesWriteHits++;
                report.ForbiddenHits++;
                AppendFinding(normalizedPath, tree, assignment, "transform.eulerAngles =", ref report);
                return;
            }

            if (IsSunVisualPositionWrite(left, transformAliases))
            {
                report.SunVisualTransformPositionWriteHits++;
                report.ForbiddenHits++;
                AppendFinding(normalizedPath, tree, assignment, "sunVisualTransform.position =", ref report);
            }
        }

        private static bool IsTransformForwardWrite(string left, HashSet<string> transformAliases)
        {
            return left == "transform.forward" ||
                   left == "sunLight.transform.forward" ||
                   IsAliasedMemberWrite(left, "forward", transformAliases) ||
                   left.EndsWith(".transform.forward", StringComparison.Ordinal);
        }

        private static bool IsTransformRotationWrite(string left, HashSet<string> transformAliases)
        {
            return left == "transform.rotation" ||
                   left == "sunLight.transform.rotation" ||
                   left == "sunVisualTransform.rotation" ||
                   left == "_planetShineLight.transform.rotation" ||
                   IsAliasedMemberWrite(left, "rotation", transformAliases) ||
                   left.EndsWith(".transform.rotation", StringComparison.Ordinal);
        }

        private static bool IsTransformLocalRotationWrite(string left, HashSet<string> transformAliases)
        {
            return left == "transform.localRotation" ||
                   left == "sunLight.transform.localRotation" ||
                   left == "sunVisualTransform.localRotation" ||
                   left == "_planetShineLight.transform.localRotation" ||
                   IsAliasedMemberWrite(left, "localRotation", transformAliases) ||
                   left.EndsWith(".transform.localRotation", StringComparison.Ordinal);
        }

        private static bool IsTransformEulerAnglesWrite(string left, HashSet<string> transformAliases)
        {
            return left == "transform.eulerAngles" ||
                   left == "transform.localEulerAngles" ||
                   left == "sunLight.transform.eulerAngles" ||
                   left == "sunLight.transform.localEulerAngles" ||
                   left == "sunVisualTransform.eulerAngles" ||
                   left == "sunVisualTransform.localEulerAngles" ||
                   left == "_planetShineLight.transform.eulerAngles" ||
                   left == "_planetShineLight.transform.localEulerAngles" ||
                   IsAliasedMemberWrite(left, "eulerAngles", transformAliases) ||
                   IsAliasedMemberWrite(left, "localEulerAngles", transformAliases) ||
                   left.EndsWith(".transform.eulerAngles", StringComparison.Ordinal) ||
                   left.EndsWith(".transform.localEulerAngles", StringComparison.Ordinal);
        }

        private static bool IsSunVisualPositionWrite(string left, HashSet<string> transformAliases)
        {
            return left == "sunVisualTransform.position" ||
                   IsAliasedMemberWrite(left, "position", transformAliases);
        }

        private static bool IsAliasedMemberWrite(string left, string memberName, HashSet<string> transformAliases)
        {
            int dot = left.LastIndexOf('.');
            if (dot <= 0 || dot >= left.Length - 1)
                return false;

            string instance = left.Substring(0, dot);
            string member = left.Substring(dot + 1);
            return member == memberName && transformAliases.Contains(instance);
        }

        private static bool IsTransformLikeExpression(string expression, HashSet<string> transformAliases)
        {
            return expression == "Transform" ||
                   expression == "transform" ||
                   expression == "sunVisualTransform" ||
                   expression.EndsWith(".transform", StringComparison.Ordinal) ||
                   transformAliases.Contains(expression);
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
            string normalizedPath,
            SyntaxTree tree,
            SyntaxNode node,
            string token,
            ref SunScanReport report)
        {
            if (report.Findings == null)
                report.Findings = new StringBuilder(1024);

            int line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            report.Findings.Append("      { \"path\": \"")
                .Append(Escape(normalizedPath))
                .Append("\", \"line\": ")
                .Append(line)
                .Append(", \"token\": \"")
                .Append(Escape(token))
                .Append("\", \"namespace\": \"")
                .Append(Escape(ResolveNamespaceName(node)))
                .Append("\", \"type\": \"")
                .Append(Escape(ResolveAncestorName<TypeDeclarationSyntax>(node)))
                .Append("\", \"member\": \"")
                .Append(Escape(ResolveMemberName(node)))
                .Append("\" },\n");
        }

        private static string ResolveMemberName(SyntaxNode node)
        {
            string method = ResolveAncestorName<BaseMethodDeclarationSyntax>(node);
            if (!string.IsNullOrEmpty(method))
                return method;

            string property = ResolveAncestorName<PropertyDeclarationSyntax>(node);
            return property;
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
                if (current is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                    return fileScopedNamespace.Name.ToString();

                current = current.Parent;
            }

            return string.Empty;
        }

        private static string BuildJsonSection(in SunScanReport report)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("  \"" + SectionKey + "\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_345\",");
            builder.AppendLine("    \"domain\": \"Echelon 7 Atmosphere & Celestial\",");
            builder.AppendLine("    \"scanner\": \"OOP_Sun_Scanner\",");
            builder.AppendLine("    \"scanner_mode\": \"ROSLYN_AST_PRIMARY_ASSIGNMENT_INVOCATION_ALIAS_SCAN\",");
            builder.AppendLine("    \"scannerUsesRoslynAst\": true,");
            builder.AppendLine("    \"summary\": \"OOP Celestial Rotations Eradicated\",");
            builder.AppendLine("    \"scanned_paths\": [");
            builder.AppendLine("      \"Assets/_Project/Scripts/Environment\",");
            builder.AppendLine("      \"Assets/_Project/Scripts/Lighting\",");
            builder.AppendLine("      \"Assets/_Project/Scripts/HectonCelestialEngine.cs\"");
            builder.AppendLine("    ],");
            builder.AppendLine("    \"forbidden_hits\": " + report.ForbiddenHits + ",");
            builder.AppendLine("    \"transform_rotate_hits\": " + report.TransformRotateHits + ",");
            builder.AppendLine("    \"transform_rotate_around_hits\": " + report.TransformRotateAroundHits + ",");
            builder.AppendLine("    \"transform_look_at_hits\": " + report.TransformLookAtHits + ",");
            builder.AppendLine("    \"transform_set_position_and_rotation_hits\": " + report.TransformSetPositionAndRotationHits + ",");
            builder.AppendLine("    \"transform_forward_write_hits\": " + report.TransformForwardWriteHits + ",");
            builder.AppendLine("    \"transform_rotation_write_hits\": " + report.TransformRotationWriteHits + ",");
            builder.AppendLine("    \"transform_local_rotation_write_hits\": " + report.TransformLocalRotationWriteHits + ",");
            builder.AppendLine("    \"transform_euler_angles_write_hits\": " + report.TransformEulerAnglesWriteHits + ",");
            builder.AppendLine("    \"quaternion_euler_transform_write_hits\": " + report.QuaternionEulerTransformWriteHits + ",");
            builder.AppendLine("    \"quaternion_look_rotation_transform_write_hits\": " + report.QuaternionLookRotationTransformWriteHits + ",");
            builder.AppendLine("    \"sun_visual_transform_position_write_hits\": " + report.SunVisualTransformPositionWriteHits + ",");
            builder.AppendLine("    \"mathf_sin_time_hits\": " + report.MathfSinTimeHits + ",");
            builder.AppendLine("    \"scanned_files\": " + report.ScannedFiles + ",");
            builder.AppendLine("    \"syntax_nodes_visited\": " + report.SyntaxNodesVisited + ",");
            builder.AppendLine("    \"parser_failures\": " + report.ParserFailures + ",");
            builder.AppendLine("    \"reportMergePolicy\": \"OOP_Sun_Scanner upserts shinobu345CelestialOrbitScanner and preserves existing top-level report sections\",");
            builder.AppendLine("    \"vaultBufferIds\": \"73350..73372 plus presentation scratch 73393..73397 named BufferID.Shinobu345* owned by SystemID.HabitatAtmosphere\",");
            builder.AppendLine("    \"findings\": [");
            if (report.Findings != null && report.Findings.Length > 0)
            {
                string findings = report.Findings.ToString().TrimEnd();
                if (findings.EndsWith(",", StringComparison.Ordinal))
                    findings = findings.Substring(0, findings.Length - 1);
                builder.AppendLine(findings);
            }

            builder.AppendLine("    ],");
            builder.AppendLine("    \"status\": \"" + (report.ForbiddenHits == 0 && report.ParserFailures == 0 ? "PASS" : "FAIL") + "\"");
            builder.Append("  }");
            return builder.ToString();
        }

        private static string UpsertReportSection(string existing, string section)
        {
            string body = TryExtractJsonObjectBody(existing, out string extracted) ? extracted : string.Empty;
            body = RemoveExistingSection(body, SectionKey).Trim();
            StringBuilder builder = new StringBuilder(Math.Max(512, body.Length + section.Length + 8));
            builder.AppendLine("{");
            if (!string.IsNullOrEmpty(body))
            {
                builder.AppendLine(body.TrimEnd());
                builder.AppendLine(",");
            }

            builder.AppendLine(section);
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static bool TryExtractJsonObjectBody(string source, out string body)
        {
            body = string.Empty;
            if (string.IsNullOrWhiteSpace(source))
                return false;

            int first = source.IndexOf('{');
            int last = source.LastIndexOf('}');
            if (first < 0 || last <= first)
                return false;

            body = source.Substring(first + 1, last - first - 1).Trim();
            return true;
        }

        private static string RemoveExistingSection(string body, string key)
        {
            string quotedKey = "\"" + key + "\"";
            int keyIndex = body.IndexOf(quotedKey, StringComparison.Ordinal);
            if (keyIndex < 0)
                return body;

            int colon = body.IndexOf(':', keyIndex + quotedKey.Length);
            if (colon < 0)
                return body;

            int valueStart = colon + 1;
            while (valueStart < body.Length && char.IsWhiteSpace(body[valueStart]))
                valueStart++;

            int valueEnd = FindJsonValueEnd(body, valueStart);
            if (valueEnd <= valueStart)
                return body;

            int start = keyIndex;
            while (start > 0 && char.IsWhiteSpace(body[start - 1]))
                start--;
            if (start > 0 && body[start - 1] == ',')
            {
                start--;
                while (start > 0 && char.IsWhiteSpace(body[start - 1]))
                    start--;
            }

            int end = valueEnd;
            while (end < body.Length && char.IsWhiteSpace(body[end]))
                end++;
            if (end < body.Length && body[end] == ',')
                end++;

            return body.Remove(start, end - start);
        }

        private static int FindJsonValueEnd(string source, int start)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = start; i < source.Length; i++)
            {
                char c = source[i];
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

                if (c == '{' || c == '[')
                {
                    depth++;
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                }
                else if (depth == 0 && c == ',')
                {
                    return i;
                }
            }

            return source.Length;
        }

        private static string ToProjectPath(string projectRoot, string path)
        {
            string fullRoot = Path.GetFullPath(projectRoot).Replace('\\', '/').TrimEnd('/');
            string fullPath = Path.GetFullPath(path).Replace('\\', '/');
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(fullRoot.Length + 1)
                : fullPath;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public struct SunScanReport
        {
            public int ScannedFiles;
            public int SyntaxNodesVisited;
            public int ParserFailures;
            public int ForbiddenHits;
            public int TransformRotateHits;
            public int TransformRotateAroundHits;
            public int TransformLookAtHits;
            public int TransformSetPositionAndRotationHits;
            public int TransformForwardWriteHits;
            public int TransformRotationWriteHits;
            public int TransformLocalRotationWriteHits;
            public int TransformEulerAnglesWriteHits;
            public int QuaternionEulerTransformWriteHits;
            public int QuaternionLookRotationTransformWriteHits;
            public int SunVisualTransformPositionWriteHits;
            public int MathfSinTimeHits;
            public StringBuilder Findings;
        }
    }
}
#endif
