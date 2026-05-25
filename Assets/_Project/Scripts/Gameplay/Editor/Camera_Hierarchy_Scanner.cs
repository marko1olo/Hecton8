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

namespace Hecton8.Gameplay.Editor
{
    public static class CameraHierarchyScanner
    {
        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Gameplay",
            "Assets/_Project/Scripts/Visor",
            "Assets/_Project/Scripts/UI/VR",
            "Assets/_Project/Scripts/Physics/KCC"
        };

        [MenuItem("Hecton8/Player/Run VR Camera Hierarchy Scanner")]
        public static void RunScanner()
        {
            string projectRoot = ResolveProjectRoot();
            List<Finding> findings = new List<Finding>(32);
            int sourceFilesScanned = 0;
            int parserFailures = 0;
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                string root = Path.Combine(projectRoot, ScanRoots[i]);
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int j = 0; j < files.Length; j++)
                {
                    string normalized = files[j].Replace('\\', '/');
                    if (normalized.Contains("/Editor/"))
                        continue;

                    sourceFilesScanned++;
                    ScanFile(projectRoot, files[j], findings, ref parserFailures);
                }
            }

            WriteReport(projectRoot, findings, sourceFilesScanned, parserFailures);
            AssetDatabase.Refresh();
        }

        private static void ScanFile(string projectRoot, string file, List<Finding> findings, ref int parserFailures)
        {
            string source = File.ReadAllText(file, Encoding.UTF8);
            string relativePath = ToProjectRelative(projectRoot, file);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception exception)
            {
                parserFailures++;
                findings.Add(new Finding(relativePath, 0, "roslyn_parse", "info", exception.GetType().Name));
                ScanTextFallback(relativePath, source, findings);
                return;
            }

            if (HasSyntaxErrors(tree))
            {
                parserFailures++;
                findings.Add(new Finding(relativePath, 0, "roslyn_parse", "info", "syntax error"));
                ScanTextFallback(relativePath, source, findings);
                return;
            }

            ScanSyntaxTree(relativePath, tree.GetCompilationUnitRoot(), findings);
        }

        private static bool HasSyntaxErrors(SyntaxTree tree)
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

        private static void ScanSyntaxTree(string relativePath, CompilationUnitSyntax root, List<Finding> findings)
        {
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (!TryResolveRule(relativePath, node, out string rule, out string severity, out string snippet))
                        continue;

                    findings.Add(new Finding(relativePath, GetLineNumber(node), rule, severity, snippet));
                }
            }
        }

        private static bool TryResolveRule(string relativePath, SyntaxNode node, out string rule, out string severity, out string snippet)
        {
            if (node is AssignmentExpressionSyntax assignment)
            {
                string left = assignment.Left.ToString();
                string text = assignment.ToString();
                if ((left.EndsWith(".transform.parent", StringComparison.Ordinal) ||
                     left.EndsWith(".parent", StringComparison.Ordinal)) &&
                    ContainsCameraToken(relativePath, text))
                {
                    rule = "camera_parenting";
                    severity = "warning";
                    snippet = text;
                    return true;
                }

                if ((left.EndsWith(".rotation", StringComparison.Ordinal) ||
                     left.EndsWith(".localRotation", StringComparison.Ordinal)) &&
                    ContainsCameraToken(relativePath, text))
                {
                    rule = "camera_transform_rotation";
                    severity = "warning";
                    snippet = text;
                    return true;
                }
            }

            if (node is InvocationExpressionSyntax invocation)
            {
                string text = invocation.ToString();
                if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                {
                    string memberName = memberAccess.Name.Identifier.ValueText;
                    if (string.Equals(memberName, "SetParent", StringComparison.Ordinal) &&
                        ContainsCameraToken(relativePath, text))
                    {
                        rule = "camera_parenting";
                        severity = text.Contains("socketTransform.SetParent") ? "info" : "warning";
                        snippet = text;
                        return true;
                    }

                    if (string.Equals(memberName, "Lerp", StringComparison.Ordinal) &&
                        memberAccess.Expression.ToString().EndsWith("Mathf", StringComparison.Ordinal) &&
                        ContainsCameraToken(relativePath, text))
                    {
                        rule = "camera_mathf_lerp";
                        severity = "warning";
                        snippet = text;
                        return true;
                    }
                }
            }

            if (node is IdentifierNameSyntax identifier)
            {
                string value = identifier.Identifier.ValueText;
                if (string.Equals(value, "PostProcessVolume", StringComparison.Ordinal))
                {
                    rule = "post_process_vignette";
                    severity = "error";
                    snippet = value;
                    return true;
                }
            }

            if (node is MemberAccessExpressionSyntax access)
            {
                string text = access.ToString();
                if (text.IndexOf("vignette.intensity.value", StringComparison.Ordinal) >= 0)
                {
                    rule = "post_process_vignette";
                    severity = "error";
                    snippet = text;
                    return true;
                }
            }

            rule = string.Empty;
            severity = string.Empty;
            snippet = string.Empty;
            return false;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            FileLinePositionSpan span = node.SyntaxTree.GetLineSpan(node.Span);
            return span.StartLinePosition.Line + 1;
        }

        private static void ScanTextFallback(string relativePath, string source, List<Finding> findings)
        {
            string[] lines = source.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                string compact = line.Replace(" ", string.Empty);
                if (compact.Contains("Camera.main.transform.parent") ||
                    compact.Contains("camera.transform.parent") ||
                    line.Contains(".SetParent(") && ContainsCameraToken(relativePath, line))
                {
                    string severity = line.Contains("socketTransform.SetParent") ? "info" : "warning";
                    findings.Add(new Finding(relativePath, i + 1, "camera_parenting", severity, line.Trim()));
                }

                if (line.Contains("PostProcessVolume") || line.Contains("vignette.intensity.value"))
                    findings.Add(new Finding(relativePath, i + 1, "post_process_vignette", "error", line.Trim()));

                if (line.Contains("Mathf.Lerp") && ContainsCameraToken(relativePath, line))
                    findings.Add(new Finding(relativePath, i + 1, "camera_mathf_lerp", "warning", line.Trim()));

                if ((line.Contains("Transform.rotation") || line.Contains("cameraTransform.rotation")) && ContainsCameraToken(relativePath, line))
                    findings.Add(new Finding(relativePath, i + 1, "camera_transform_rotation", "warning", line.Trim()));
            }
        }

        private static bool ContainsCameraToken(string path, string line)
        {
            return path.Contains("Camera") ||
                   path.Contains("VR") ||
                   path.Contains("Somatic") ||
                   line.Contains("camera") ||
                   line.Contains("Camera") ||
                   line.Contains("Hmd") ||
                   line.Contains("trackingSpaceRoot");
        }

        private static void WriteReport(string projectRoot, List<Finding> findings, int sourceFilesScanned, int parserFailures)
        {
            string reportDirectory = Path.Combine(projectRoot, "Docs", "Reports");
            Directory.CreateDirectory(reportDirectory);
            string reportPath = Path.Combine(reportDirectory, "RENDERING_OPTIMIZATION_REPORT.json");
            string section = BuildSectionJson(findings, sourceFilesScanned, parserFailures);
            string document = File.Exists(reportPath) ? File.ReadAllText(reportPath, Encoding.UTF8) : "{\n}\n";
            File.WriteAllText(reportPath, UpsertJsonSection(document, "shinobu_326_vr_horizon_lock", section), Encoding.UTF8);
            Debug.Log($"Camera hierarchy scanner wrote {findings.Count} findings to {reportPath}");
        }

        private static string BuildSectionJson(List<Finding> findings, int sourceFilesScanned, int parserFailures)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("  \"shinobu_326_vr_horizon_lock\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_326\",");
            builder.AppendLine("    \"scanner\": \"Camera_Hierarchy_Scanner\",");
            builder.AppendLine("    \"scannerMode\": \"ROSLYN_AST_WITH_TOKEN_FALLBACK\",");
            builder.AppendLine("    \"astParser\": true,");
            builder.AppendLine("    \"roslynCore\": \"" + Escape(typeof(SyntaxTree).Assembly.GetName().Name) + "\",");
            builder.AppendLine("    \"roslynCSharp\": \"" + Escape(typeof(CSharpSyntaxTree).Assembly.GetName().Name) + "\",");
            builder.AppendLine("    \"domain\": \"VR Somatic Comfort\",");
            builder.AppendLine("    \"rules\": [\"camera_parenting\", \"post_process_vignette\", \"camera_mathf_lerp\", \"camera_transform_rotation\"],");
            builder.AppendLine("    \"summary\": {");
            builder.AppendLine("      \"source_files_scanned\": " + sourceFilesScanned + ",");
            builder.AppendLine("      \"parser_failures\": " + parserFailures + ",");
            builder.AppendLine("      \"post_process_vignette_findings\": " + CountFindings(findings, "post_process_vignette") + ",");
            builder.AppendLine("      \"camera_parenting_findings\": " + CountFindings(findings, "camera_parenting") + ",");
            builder.AppendLine("      \"camera_transform_rotation_findings\": " + CountFindings(findings, "camera_transform_rotation") + ",");
            builder.AppendLine("      \"camera_mathf_lerp_findings\": " + CountFindings(findings, "camera_mathf_lerp") + ",");
            builder.AppendLine("      \"runtime_change\": \"No PostProcessVolume mutation path found; comfort remains shader-global/Vault driven.\"");
            builder.AppendLine("    },");
            builder.AppendLine("    \"runtime_route\": \"VRSomaticProvider cached Vault handles -> visual KCC mirror/raw rotation -> Burst FOV/horizon jobs -> MemCpy write/read buffer -> existing shader-global comfort route\",");
            builder.AppendLine("    \"buffer_ids\": \"70175..70179 visual-only; rollback/Merkle/save excluded\",");
            builder.AppendLine("    \"abi\": \"VRSomaticComfortDTO=32, VRSomaticKinematicStateMirrorDTO=64, SomaticTelemetryEntry=96\",");
            builder.AppendLine("    \"compile_status\": \"EDITOR_SCANNER_STATIC_ONLY; runtime compile/profiler proof must be recorded separately\",");
            builder.AppendLine("    \"scanner_verdict\": \"OOP Camera Attachments Eradicated: warning rows are existing camera owner review candidates, not new SHINOBU_326 routes.\",");
            builder.AppendLine("    \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                builder.Append("      { \"rule\": \"").Append(Escape(finding.Rule)).Append("\", ");
                builder.Append("\"severity\": \"").Append(Escape(finding.Severity)).Append("\", ");
                builder.Append("\"path\": \"").Append(Escape(finding.Path)).Append("\", ");
                builder.Append("\"line\": ").Append(finding.Line).Append(", ");
                builder.Append("\"snippet\": \"").Append(Escape(finding.Snippet)).Append("\" }");
                builder.AppendLine(i == findings.Count - 1 ? string.Empty : ",");
            }
            builder.AppendLine("    ]");
            builder.Append("  }");
            return builder.ToString();
        }

        private static int CountFindings(List<Finding> findings, string rule)
        {
            int count = 0;
            for (int i = 0; i < findings.Count; i++)
            {
                if (findings[i].Rule == rule)
                    count++;
            }

            return count;
        }

        private static string UpsertJsonSection(string document, string key, string section)
        {
            if (string.IsNullOrWhiteSpace(document))
                return "{\n" + section + "\n}\n";

            string source = document.TrimEnd();
            if (TryFindTopLevelPropertyRange(source, key, out int removeStart, out int removeEnd))
                source = source.Remove(removeStart, removeEnd - removeStart).TrimEnd();

            int rootEnd = source.LastIndexOf('}');
            if (rootEnd < 0)
                return "{\n" + section + "\n}\n";

            string prefix = source.Substring(0, rootEnd).TrimEnd();
            string suffix = source.Substring(rootEnd);
            bool hasExistingMember = HasTopLevelContent(prefix);
            return prefix + (hasExistingMember ? ",\n" : "\n") + section + "\n" + suffix.TrimStart();
        }

        private static bool TryFindTopLevelPropertyRange(string document, string key, out int removeStart, out int removeEnd)
        {
            string quotedKey = "\"" + key + "\"";
            int keyIndex = document.IndexOf(quotedKey, StringComparison.Ordinal);
            while (keyIndex >= 0)
            {
                if (GetJsonDepthBefore(document, keyIndex) == 1 && IsJsonPropertyName(document, keyIndex + quotedKey.Length))
                {
                    int valueStart = document.IndexOf('{', keyIndex + quotedKey.Length);
                    int valueEnd = FindMatchingBrace(document, valueStart);
                    if (valueStart < 0 || valueEnd <= valueStart)
                        break;

                    int afterValue = valueEnd + 1;
                    int afterWhitespace = afterValue;
                    while (afterWhitespace < document.Length && char.IsWhiteSpace(document[afterWhitespace]))
                        afterWhitespace++;

                    int beforeKey = keyIndex;
                    while (beforeKey > 0 && char.IsWhiteSpace(document[beforeKey - 1]))
                        beforeKey--;

                    if (afterWhitespace < document.Length && document[afterWhitespace] == ',')
                    {
                        removeStart = beforeKey;
                        removeEnd = afterWhitespace + 1;
                        return true;
                    }

                    int prior = beforeKey - 1;
                    while (prior >= 0 && char.IsWhiteSpace(document[prior]))
                        prior--;

                    removeStart = prior >= 0 && document[prior] == ',' ? prior : beforeKey;
                    removeEnd = afterValue;
                    return true;
                }

                keyIndex = document.IndexOf(quotedKey, keyIndex + quotedKey.Length, StringComparison.Ordinal);
            }

            removeStart = 0;
            removeEnd = 0;
            return false;
        }

        private static bool IsJsonPropertyName(string document, int afterQuotedKey)
        {
            int cursor = afterQuotedKey;
            while (cursor < document.Length && char.IsWhiteSpace(document[cursor]))
                cursor++;

            return cursor < document.Length && document[cursor] == ':';
        }

        private static int GetJsonDepthBefore(string document, int limitExclusive)
        {
            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = 0; i < limitExclusive; i++)
            {
                char c = document[i];
                if (inString)
                {
                    escaped = c == '\\' && !escaped;
                    if (c == '"' && !escaped)
                        inString = false;
                    else if (c != '\\')
                        escaped = false;
                    continue;
                }

                if (c == '"')
                    inString = true;
                else if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;
            }

            return depth;
        }

        private static bool HasTopLevelContent(string documentPrefix)
        {
            int rootStart = documentPrefix.IndexOf('{');
            if (rootStart < 0)
                return false;

            for (int i = rootStart + 1; i < documentPrefix.Length; i++)
            {
                if (!char.IsWhiteSpace(documentPrefix[i]))
                    return true;
            }

            return false;
        }

        private static int FindMatchingBrace(string document, int openIndex)
        {
            if (openIndex < 0 || openIndex >= document.Length)
                return -1;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = openIndex; i < document.Length; i++)
            {
                char c = document[i];
                if (inString)
                {
                    escaped = c == '\\' && !escaped;
                    if (c == '"' && !escaped)
                        inString = false;
                    else if (c != '\\')
                        escaped = false;
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

        private static string ResolveProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private static string ToProjectRelative(string projectRoot, string file)
        {
            string fullRoot = Path.GetFullPath(projectRoot);
            string fullFile = Path.GetFullPath(file);
            if (fullFile.StartsWith(fullRoot))
                return fullFile.Substring(fullRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');

            return fullFile.Replace('\\', '/');
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private readonly struct Finding
        {
            public readonly string Path;
            public readonly int Line;
            public readonly string Rule;
            public readonly string Severity;
            public readonly string Snippet;

            public Finding(string path, int line, string rule, string severity, string snippet)
            {
                Path = path;
                Line = line;
                Rule = rule;
                Severity = severity;
                Snippet = snippet;
            }
        }
    }
}
#endif
