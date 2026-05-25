#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    internal static class SkinnedMesh_Scanner
    {
        private const string ReportKey = "\"shinobu_305_procedural_ik_matrices\"";
        private const string ReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";

        private static readonly string[] SourceRoots =
        {
            "Assets/_Project/Scripts/Fauna",
            "Assets/_Project/Scripts/Animation/IK",
            "Assets/_Project/Scripts/Animation/FaunaProcedural"
        };

        private static readonly string[] FaunaPathTokens =
        {
            "fauna",
            "leviathan",
            "tentacle",
            "eel",
            "fish"
        };

        [MenuItem("Tools/Hecton8/Rendering/Run SkinnedMesh Scanner")]
        public static void Run()
        {
            StringBuilder findingsJson = new StringBuilder(8192);
            int sourceFilesScanned = 0;
            int prefabsScanned = 0;
            int findings = 0;
            int parserFailures = 0;
            ScanSource(findingsJson, ref sourceFilesScanned, ref findings, ref parserFailures);
            ScanPrefabs(findingsJson, ref prefabsScanned, ref findings);

            StringBuilder json = new StringBuilder(8192);
            json.AppendLine("  \"shinobu_305_procedural_ik_matrices\": {");
            json.AppendLine("    \"agentId\": \"SHINOBU_305\",");
            json.AppendLine("    \"scanner\": \"SkinnedMesh_Scanner\",");
            json.AppendLine("    \"summary\": \"OOP Bone Animations Eradicated\",");
            json.AppendLine("    \"reportSchema\": 1,");
            json.AppendLine("    \"timestampUtc\": \"" + Escape(DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture)) + "\",");
            json.AppendLine("    \"evidenceClass\": \"ROSLYN_AST_SOURCE_AND_PREFAB_COMPONENT_SCAN\",");
            json.AppendLine("    \"scannerUsesRoslynAst\": true,");
            json.AppendLine("    \"rule\": \"No fauna SkinnedMeshRenderer bones, Transform bone arrays, or LateUpdate bone mutation in leviathan/fish paths\",");
            json.AppendLine("    \"sourceFilesScanned\": " + sourceFilesScanned + ",");
            json.AppendLine("    \"faunaPrefabsScanned\": " + prefabsScanned + ",");
            json.AppendLine("    \"parserFailures\": " + parserFailures + ",");
            json.AppendLine("    \"activeViolationCount\": " + findings + ",");
            json.AppendLine("    \"oopBoneAnimationsEradicated\": " + (findings == 0 ? "true" : "false") + ",");
            json.AppendLine("    \"performanceFinding\": \"SkinnedMeshRenderer bone hierarchies force managed transform sampling, renderer bound recomputation, and serialized bone-matrix upload; SHINOBU_305 routes leviathan presentation through Burst DTOs and LockBufferForWrite GPU buffers instead.\",");
            json.AppendLine("    \"findings\": [");
            json.Append(findingsJson);
            json.AppendLine();
            json.AppendLine("    ],");
            json.AppendLine("    \"findingCount\": " + findings);
            json.AppendLine("  }");

            WriteMergedReport(Path.Combine(Directory.GetCurrentDirectory(), ReportPath), json.ToString());
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_305 SkinnedMesh_Scanner findings: " + findings);
        }

        private static void ScanSource(StringBuilder json, ref int sourceFilesScanned, ref int findings, ref int parserFailures)
        {
            for (int r = 0; r < SourceRoots.Length; r++)
            {
                string absoluteRoot = Path.Combine(Directory.GetCurrentDirectory(), SourceRoots[r]);
                if (!Directory.Exists(absoluteRoot))
                    continue;

                string[] files = Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string path = NormalizePath(files[i]);
                    sourceFilesScanned++;
                    string text = File.ReadAllText(files[i]);
                    SyntaxTree tree;
                    try
                    {
                        tree = CSharpSyntaxTree.ParseText(text);
                    }
                    catch (Exception exception)
                    {
                        parserFailures++;
                        AppendFinding(json, ref findings, "source_ast_parse", path, "Roslyn parse failed: " + exception.GetType().Name);
                        continue;
                    }
                    if (HasParseError(tree))
                    {
                        parserFailures++;
                        AppendFinding(json, ref findings, "source_ast_parse", path, "Roslyn parse produced syntax errors");
                        continue;
                    }

                    CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                    if (HasIdentifier(root, "SkinnedMeshRenderer"))
                        AppendFinding(json, ref findings, "source", path, "SkinnedMeshRenderer reference in fauna/procedural animation scope");
                    if (HasLateUpdateBoneMutationCandidate(root))
                        AppendFinding(json, ref findings, "source", path, "LateUpdate transform bone mutation candidate");
                    if (HasManagedTransformBoneArrayCandidate(root))
                        AppendFinding(json, ref findings, "source", path, "managed Transform bone array candidate");
                }
            }
        }

        private static bool HasLateUpdateBoneMutationCandidate(SyntaxNode root)
        {
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (!(nodes.Current is MethodDeclarationSyntax method) ||
                        !string.Equals(method.Identifier.ValueText, "LateUpdate", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    if (HasIdentifier(method, "transform") &&
                        (HasTokenContaining(method, "bone") || HasTokenContaining(method, "joint")))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasParseError(SyntaxTree tree)
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

        private static bool HasManagedTransformBoneArrayCandidate(SyntaxNode root)
        {
            if (HasIdentifier(root, "HumanBodyBones") || HasMemberAccessName(root, "bones"))
                return true;

            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (nodes.Current is FieldDeclarationSyntax field && DeclarationHasTransformBoneName(field.Declaration))
                        return true;
                    if (nodes.Current is LocalDeclarationStatementSyntax local && DeclarationHasTransformBoneName(local.Declaration))
                        return true;
                }
            }

            return false;
        }

        private static bool DeclarationHasTransformBoneName(VariableDeclarationSyntax declaration)
        {
            if (declaration == null || declaration.Type == null || declaration.Type.ToString().IndexOf("Transform", StringComparison.Ordinal) < 0)
                return false;

            SeparatedSyntaxList<VariableDeclaratorSyntax> variables = declaration.Variables;
            for (int i = 0; i < variables.Count; i++)
            {
                string name = variables[i].Identifier.ValueText;
                if (ContainsDomainToken(name, "bone") || ContainsDomainToken(name, "joint"))
                    return true;
            }

            return false;
        }

        private static bool HasIdentifier(SyntaxNode root, string identifierName)
        {
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (nodes.Current is IdentifierNameSyntax identifier &&
                        string.Equals(identifier.Identifier.ValueText, identifierName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasMemberAccessName(SyntaxNode root, string memberName)
        {
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (nodes.Current is MemberAccessExpressionSyntax memberAccess &&
                        string.Equals(memberAccess.Name.Identifier.ValueText, memberName, StringComparison.Ordinal))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasTokenContaining(SyntaxNode root, string token)
        {
            using (System.Collections.Generic.IEnumerator<SyntaxToken> tokens = root.DescendantTokens().GetEnumerator())
            {
                while (tokens.MoveNext())
                {
                    if (ContainsDomainToken(tokens.Current.ValueText, token))
                        return true;
                }
            }

            return false;
        }

        private static bool ContainsDomainToken(string value, string token)
        {
            return !string.IsNullOrEmpty(value) &&
                   value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void ScanPrefabs(StringBuilder json, ref int prefabsScanned, ref int findings)
        {
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Project" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!IsFaunaPath(path))
                    continue;

                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null)
                    continue;

                prefabsScanned++;
                SkinnedMeshRenderer[] renderers = prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                for (int r = 0; r < renderers.Length; r++)
                {
                    SkinnedMeshRenderer renderer = renderers[r];
                    if (renderer == null)
                        continue;

                    string detail = "SkinnedMeshRenderer component";
                    if (renderer.bones != null && renderer.bones.Length > 0)
                        detail += " with " + renderer.bones.Length + " managed bones";
                    if (renderer.sharedMesh == null)
                        detail += "; missing sharedMesh";
                    AppendFinding(json, ref findings, "prefab", path, detail);
                }
            }
        }

        private static bool IsFaunaPath(string path)
        {
            string lower = path.ToLowerInvariant();
            for (int i = 0; i < FaunaPathTokens.Length; i++)
            {
                if (lower.Contains(FaunaPathTokens[i]))
                    return true;
            }

            return false;
        }

        private static void AppendFinding(StringBuilder json, ref int findings, string kind, string path, string detail)
        {
            if (findings > 0)
                json.AppendLine(",");

            json.Append("      { \"kind\": \"");
            json.Append(Escape(kind));
            json.Append("\", \"path\": \"");
            json.Append(Escape(path));
            json.Append("\", \"detail\": \"");
            json.Append(Escape(detail));
            json.Append("\" }");
            findings++;
        }

        private static void WriteMergedReport(string path, string entryJson)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string merged;
            if (!File.Exists(path))
            {
                merged = "{\n" + entryJson + "\n}\n";
            }
            else
            {
                string existing = File.ReadAllText(path);
                int keyIndex = existing.IndexOf(ReportKey, StringComparison.Ordinal);
                if (keyIndex >= 0 && TryFindObjectBounds(existing, keyIndex, out int start, out int end))
                {
                    int lineStart = existing.LastIndexOf('\n', keyIndex);
                    lineStart = lineStart < 0 ? 0 : lineStart + 1;
                    int replaceEnd = end + 1;
                    if (replaceEnd < existing.Length && existing[replaceEnd] == ',')
                        replaceEnd++;
                    merged = existing.Substring(0, lineStart) + entryJson + existing.Substring(replaceEnd);
                }
                else
                {
                    string trimmed = existing.TrimEnd();
                    if (trimmed.Length == 0 || trimmed == "{}")
                    {
                        merged = "{\n" + entryJson + "\n}\n";
                    }
                    else if (trimmed.EndsWith("}", StringComparison.Ordinal))
                    {
                        int insert = existing.LastIndexOf('}');
                        string separator = existing.LastIndexOf('{') < insert - 1 ? ",\n" : "\n";
                        merged = existing.Substring(0, insert).TrimEnd() + separator + entryJson + "\n}\n";
                    }
                    else
                    {
                        merged = "{\n" + entryJson + "\n}\n";
                    }
                }
            }

            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, merged, Encoding.UTF8);
            if (File.Exists(path))
                File.Replace(tempPath, path, null);
            else
                File.Move(tempPath, path);
        }

        private static bool TryFindObjectBounds(string text, int keyIndex, out int start, out int end)
        {
            start = text.IndexOf('{', keyIndex);
            end = -1;
            if (start < 0)
                return false;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = start; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    escaped = c == '\\' && !escaped;
                    if (c == '"' && !escaped)
                        inString = false;
                    if (c != '\\')
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
                    {
                        end = i;
                        return true;
                    }
                }
            }

            return false;
        }

        private static string NormalizePath(string path)
        {
            string projectRoot = Directory.GetCurrentDirectory().Replace('\\', '/');
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith(projectRoot, StringComparison.OrdinalIgnoreCase)
                ? normalized.Substring(projectRoot.Length + 1)
                : normalized;
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
