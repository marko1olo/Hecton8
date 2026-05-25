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

namespace Hecton8.Cartography.Editor
{
    public static class OOP_Map_Scanner
    {
        private const string SectionKey = "shinobu_350_sonar_cartography_fog_of_war";
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/UI",
            "Assets/_Project/Scripts/Cartography"
        };

        [MenuItem("Hecton8/Cartography/OOP Map Scanner")]
        public static void Run()
        {
            ScanResult result = Scan();
            WriteReport(result);
            AssetDatabase.Refresh();
        }

        internal static ScanResult Scan()
        {
            ScanResult result = new ScanResult
            {
                Findings = new List<Finding>(16)
            };

            for (int r = 0; r < ScanRoots.Length; r++)
            {
                string absoluteRoot = Path.Combine(Application.dataPath, "..", ScanRoots[r]);
                if (!Directory.Exists(absoluteRoot))
                    continue;

                string[] files = Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                    ScanFile(files[i], ref result);
            }

            return result;
        }

        private static void ScanFile(string path, ref ScanResult result)
        {
            string normalizedPath = NormalizePath(path);
            if (normalizedPath.EndsWith("OOP_Map_Scanner.cs", StringComparison.Ordinal) ||
                normalizedPath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                !IsCartographyScopePath(normalizedPath))
            {
                return;
            }

            string source = File.ReadAllText(path);
            if (!LooksRelevantToMapOop(source))
                return;

            result.FilesScanned++;
            try
            {
                SyntaxTree tree = CSharpSyntaxTree.ParseText(source);
                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                if (!HasCartographyScope(root, normalizedPath))
                    return;

                result.SyntaxTreesParsed++;
                ScanSyntaxTree(normalizedPath, tree, root, ref result);
            }
            catch (Exception)
            {
                result.ParserFailures++;
                ScanLexicalFallback(normalizedPath, source, ref result);
            }
        }

        private static void ScanSyntaxTree(
            string path,
            SyntaxTree tree,
            CompilationUnitSyntax root,
            ref ScanResult result)
        {
            using (IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    result.SyntaxNodesVisited++;
                    if (TryResolveAstFinding(node, out string pattern))
                        RegisterFinding(path, LineOf(tree, node), pattern, "ROSLYN_AST", ref result);
                }
            }
        }

        private static bool TryResolveAstFinding(SyntaxNode node, out string pattern)
        {
            pattern = null;
            if (node is GenericNameSyntax genericName)
            {
                string identifier = genericName.Identifier.ValueText;
                string typeArguments = genericName.TypeArgumentList.ToString();
                if (identifier == "Dictionary" &&
                    ContainsVector3Type(typeArguments) &&
                    typeArguments.IndexOf("bool", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pattern = "Dictionary<Vector3*, bool>";
                    return true;
                }

                if (identifier == "List" &&
                    ContainsVector3Type(typeArguments) &&
                    IsExplorationContext(node))
                {
                    pattern = "List<Vector3*> exploration-map state";
                    return true;
                }
            }

            if (node is ObjectCreationExpressionSyntax objectCreation)
            {
                string type = objectCreation.Type.ToString();
                if (type.IndexOf("Dictionary", StringComparison.Ordinal) >= 0 &&
                    ContainsVector3Type(type) &&
                    type.IndexOf("bool", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    pattern = "new Dictionary<Vector3*, bool>";
                    return true;
                }

                if (type.IndexOf("GameObject", StringComparison.Ordinal) >= 0 &&
                    IsForbiddenMapObjectText(objectCreation.ToString()))
                {
                    pattern = "new GameObject map voxel/dot/cube";
                    return true;
                }
            }

            if (node is InvocationExpressionSyntax invocation)
            {
                string invocationText = invocation.ToString();
                string expressionText = invocation.Expression.ToString();
                if (expressionText.IndexOf("CreatePrimitive", StringComparison.Ordinal) >= 0 &&
                    invocationText.IndexOf("PrimitiveType.Cube", StringComparison.Ordinal) >= 0)
                {
                    pattern = "GameObject.CreatePrimitive(PrimitiveType.Cube)";
                    return true;
                }

                if (expressionText.IndexOf("Instantiate", StringComparison.Ordinal) >= 0 &&
                    IsForbiddenMapObjectText(invocationText))
                {
                    pattern = "Instantiate map voxel/dot/cube";
                    return true;
                }
            }

            return false;
        }

        private static bool HasCartographyScope(CompilationUnitSyntax root, string path)
        {
            if (IsCartographyScopePath(path))
                return true;

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
                    else if (node is ClassDeclarationSyntax classNode)
                        name = classNode.Identifier.ValueText;

                    if (!string.IsNullOrEmpty(name) && IsCartographyScopeToken(name))
                        return true;
                }
            }

            return false;
        }

        private static void ScanLexicalFallback(string path, string source, ref ScanResult result)
        {
            string[] lines = source.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                if (line.IndexOf("Dictionary<Vector3", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("Dictionary<Vector3Int", StringComparison.Ordinal) >= 0)
                {
                    RegisterFinding(path, i + 1, "Dictionary<Vector3*, bool>:LEXICAL_FALLBACK", "LEXICAL_FALLBACK", ref result);
                }

                if (line.IndexOf("GameObject.CreatePrimitive", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("PrimitiveType.Cube", StringComparison.Ordinal) >= 0 ||
                    (line.IndexOf("Instantiate", StringComparison.Ordinal) >= 0 && IsForbiddenMapObjectText(line)) ||
                    (line.IndexOf("new GameObject", StringComparison.Ordinal) >= 0 && IsForbiddenMapObjectText(line)))
                {
                    RegisterFinding(path, i + 1, "map cube/dot GameObject:LEXICAL_FALLBACK", "LEXICAL_FALLBACK", ref result);
                }
            }
        }

        private static bool LooksRelevantToMapOop(string source)
        {
            return source.IndexOf("Dictionary<", StringComparison.Ordinal) >= 0 ||
                   source.IndexOf("List<", StringComparison.Ordinal) >= 0 ||
                   source.IndexOf("GameObject", StringComparison.Ordinal) >= 0 ||
                   source.IndexOf("Instantiate", StringComparison.Ordinal) >= 0 ||
                   source.IndexOf("PrimitiveType.Cube", StringComparison.Ordinal) >= 0;
        }

        private static bool ContainsVector3Type(string value)
        {
            return value.IndexOf("Vector3", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("Vector3Int", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("UnityEngine.Vector3", StringComparison.Ordinal) >= 0;
        }

        private static bool IsExplorationContext(SyntaxNode node)
        {
            SyntaxNode current = node;
            for (int i = 0; i < 8 && current != null; i++, current = current.Parent)
            {
                string text = current.ToString();
                if (text.IndexOf("explor", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("cartograph", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("fog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("voxel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    text.IndexOf("discovered", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsForbiddenMapObjectText(string value)
        {
            return value.IndexOf("Cube", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Voxel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("MapDot", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Fog", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Explored", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Exploration", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCartographyScopePath(string path)
        {
            return path.IndexOf("/Cartography/", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("PDAMap", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("MapTab", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("Cartography", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("Exploration", StringComparison.Ordinal) >= 0 ||
                   path.IndexOf("Fog", StringComparison.Ordinal) >= 0;
        }

        private static bool IsCartographyScopeToken(string value)
        {
            return value.IndexOf("Cartography", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("PDA", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("Map", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("Exploration", StringComparison.Ordinal) >= 0 ||
                   value.IndexOf("Fog", StringComparison.Ordinal) >= 0;
        }

        private static void RegisterFinding(
            string path,
            int line,
            string pattern,
            string route,
            ref ScanResult result)
        {
            result.Findings.Add(new Finding
            {
                File = path,
                Line = line,
                Pattern = pattern,
                Route = route
            });
        }

        private static int LineOf(SyntaxTree tree, SyntaxNode node)
        {
            FileLinePositionSpan span = tree.GetLineSpan(node.Span);
            return span.StartLinePosition.Line + 1;
        }

        private static void WriteReport(ScanResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string reportPath = Path.Combine(projectRoot, ReportRelativePath);
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string existing = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
            File.WriteAllText(reportPath, UpsertSection(existing, BuildSectionJson(result)));
        }

        private static string BuildSectionJson(ScanResult result)
        {
            StringBuilder json = new StringBuilder(4096);
            json.Append("  \"").Append(SectionKey).Append("\": {\n");
            json.Append("    \"agent\": \"SHINOBU_350\",\n");
            json.Append("    \"summary\": \"")
                .Append(result.Findings.Count == 0 ? "OOP Map Structures Eradicated" : "OOP Map Structures Detected")
                .Append("\",\n");
            json.Append("    \"scanner\": \"Assets/_Project/Scripts/Cartography/Editor/OOP_Map_Scanner.cs\",\n");
            json.Append("    \"scannerParserRoute\": \"Roslyn CSharpSyntaxTree AST primary pass; lexical fallback only on parse exception\",\n");
            json.Append("    \"scannerUsesRoslynAst\": true,\n");
            json.Append("    \"filesScanned\": ").Append(result.FilesScanned).Append(",\n");
            json.Append("    \"syntaxTreesParsed\": ").Append(result.SyntaxTreesParsed).Append(",\n");
            json.Append("    \"syntaxNodesVisited\": ").Append(result.SyntaxNodesVisited).Append(",\n");
            json.Append("    \"parserFailures\": ").Append(result.ParserFailures).Append(",\n");
            json.Append("    \"forbiddenFindingCount\": ").Append(result.Findings.Count).Append(",\n");
            json.Append("    \"scope\": [\n");
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                json.Append("      \"").Append(EscapeJson(ScanRoots[i])).Append("\"");
                json.Append(i + 1 < ScanRoots.Length ? ",\n" : "\n");
            }

            json.Append("    ],\n");
            json.Append("    \"forbiddenPatterns\": [\n");
            json.Append("      \"Dictionary<Vector3*, bool>\",\n");
            json.Append("      \"List<Vector3*> exploration-map state\",\n");
            json.Append("      \"GameObject.CreatePrimitive(PrimitiveType.Cube)\",\n");
            json.Append("      \"new/Instantiate map voxel/dot/cube GameObject\"\n");
            json.Append("    ],\n");
            json.Append("    \"replacementRoute\": \"GlobalDataVault NativeArray<ulong> discovery words -> packed R8 GraphicsBuffer -> Hecton_HologramMap shader; no cartography voxel GameObjects\",\n");
            json.Append("    \"findings\": [");
            for (int i = 0; i < result.Findings.Count; i++)
            {
                Finding finding = result.Findings[i];
                json.Append(i == 0 ? "\n" : ",\n");
                json.Append("      { \"file\": \"").Append(EscapeJson(finding.File))
                    .Append("\", \"line\": ").Append(finding.Line)
                    .Append(", \"pattern\": \"").Append(EscapeJson(finding.Pattern))
                    .Append("\", \"route\": \"").Append(EscapeJson(finding.Route))
                    .Append("\" }");
            }

            if (result.Findings.Count > 0)
                json.Append("\n    ]\n");
            else
                json.Append("]\n");
            json.Append("  }");
            return json.ToString();
        }

        private static string UpsertSection(string existing, string sectionJson)
        {
            string trimmed = string.IsNullOrWhiteSpace(existing) ? "{}" : existing.Trim();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal) || !trimmed.EndsWith("}", StringComparison.Ordinal))
                return "{\n" + sectionJson + "\n}\n";

            string key = "\"" + SectionKey + "\"";
            int keyIndex = trimmed.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int propertyStart = FindPropertyStart(trimmed, keyIndex);
                int objectStart = trimmed.IndexOf('{', keyIndex);
                int objectEnd = FindObjectEnd(trimmed, objectStart);
                if (objectStart >= 0 && objectEnd >= objectStart)
                {
                    int propertyEnd = objectEnd + 1;
                    int next = SkipWhitespace(trimmed, propertyEnd);
                    if (next < trimmed.Length && trimmed[next] == ',')
                    {
                        propertyEnd = next + 1;
                    }
                    else
                    {
                        int previous = propertyStart - 1;
                        while (previous >= 0 && char.IsWhiteSpace(trimmed[previous]))
                            previous--;
                        if (previous >= 0 && trimmed[previous] == ',')
                            propertyStart = previous;
                    }

                    trimmed = trimmed.Remove(propertyStart, propertyEnd - propertyStart);
                }
            }

            int insertIndex = trimmed.LastIndexOf('}');
            string prefix = trimmed.Substring(0, insertIndex).TrimEnd();
            string separator = prefix.Length > 1 ? ",\n" : "\n";
            return prefix + separator + sectionJson + "\n}\n";
        }

        private static int FindPropertyStart(string value, int keyIndex)
        {
            int start = keyIndex;
            while (start > 0 && value[start - 1] != '\n' && value[start - 1] != '\r')
                start--;
            return start;
        }

        private static int SkipWhitespace(string value, int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
            return index;
        }

        private static int FindObjectEnd(string value, int objectStart)
        {
            if (objectStart < 0)
                return -1;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = objectStart; i < value.Length; i++)
            {
                char c = value[i];
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
                    inString = true;
                else if (c == '{')
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

        private static string NormalizePath(string absolutePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.GetFullPath(absolutePath);
            return fullPath.StartsWith(projectRoot, StringComparison.Ordinal)
                ? fullPath.Substring(projectRoot.Length + 1).Replace('\\', '/')
                : fullPath.Replace('\\', '/');
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal struct ScanResult
        {
            public int FilesScanned;
            public int SyntaxTreesParsed;
            public int SyntaxNodesVisited;
            public int ParserFailures;
            public List<Finding> Findings;
        }

        internal struct Finding
        {
            public string File;
            public int Line;
            public string Pattern;
            public string Route;
        }
    }
}
#endif
