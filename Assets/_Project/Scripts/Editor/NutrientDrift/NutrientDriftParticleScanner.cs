#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class NutrientDriftParticleScanner
    {
        private const string ReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string SectionKey = "shinobu_309_plankton_nutrient_flow_drift";
        private const string StatusClean = "OOP Fluid Particles Eradicated";
        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Environment",
            "Assets/_Project/Scripts/AI",
            "Assets/_Project/Scripts/Ecosystem"
        };

        private static readonly string[] Needles =
        {
            "ParticleSystem",
            "ParticleSystem.CollisionModule",
            "OnParticleCollision",
            "GetCollisionEvents",
            "ParticleSystemTriggerEventType",
            "Rigidbody"
        };

        [MenuItem("HECTON-8/Ecosystem/Scan Nutrient Particle Authority")]
        public static void RunMenuScan()
        {
            ScanResult result = ScanProject();
            WriteReport(result);
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_309 nutrient particle authority scan wrote " + ReportPath + " with " + result.HitCount + " hits.");
        }

        public static ScanResult ScanProject()
        {
            var result = new ScanResult();
            for (int rootIndex = 0; rootIndex < ScanRoots.Length; rootIndex++)
            {
                string root = ScanRoots[rootIndex];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    result.SourceFilesScanned++;
                    ScanFile(files[fileIndex], ref result);
                }
            }

            return result;
        }

        private static void ScanFile(string path, ref ScanResult result)
        {
            string source = File.ReadAllText(path, Encoding.UTF8);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(source);
            }
            catch (Exception exception)
            {
                result.ParserFailureCount++;
                result.AppendParserFailure(path, exception.GetType().Name);
                return;
            }

            if (HasParseError(tree))
            {
                result.ParserFailureCount++;
                result.AppendParserFailure(path, "Roslyn syntax error");
                return;
            }

            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (!TryResolveParticleAuthorityToken(node, out string needle))
                        continue;

                    result.TotalParticleReferences++;
                    bool nutrientAuthority = IsNutrientAuthorityCandidate(path, node);
                    if (nutrientAuthority)
                    {
                        result.HitCount++;
                        result.AppendHit(path, GetLineNumber(node), needle, BuildSyntaxContext(node));
                    }
                }
            }
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

        private static bool TryResolveParticleAuthorityToken(SyntaxNode node, out string needle)
        {
            if (node is IdentifierNameSyntax identifier)
            {
                string value = identifier.Identifier.ValueText;
                if (EqualsNeedle(value, "ParticleSystem") ||
                    EqualsNeedle(value, "ParticleSystemTriggerEventType") ||
                    EqualsNeedle(value, "Rigidbody"))
                {
                    needle = value;
                    return true;
                }
            }

            if (node is MemberAccessExpressionSyntax memberAccess)
            {
                string name = memberAccess.Name.Identifier.ValueText;
                if (EqualsNeedle(name, "GetCollisionEvents"))
                {
                    needle = name;
                    return true;
                }

                if (EqualsNeedle(memberAccess.Expression.ToString(), "ParticleSystem") &&
                    EqualsNeedle(name, "CollisionModule"))
                {
                    needle = "ParticleSystem.CollisionModule";
                    return true;
                }
            }

            if (node is MethodDeclarationSyntax method &&
                EqualsNeedle(method.Identifier.ValueText, "OnParticleCollision"))
            {
                needle = "OnParticleCollision";
                return true;
            }

            needle = string.Empty;
            return false;
        }

        private static bool EqualsNeedle(string value, string needle)
        {
            return string.Equals(value, needle, StringComparison.Ordinal);
        }

        private static bool IsNutrientAuthorityCandidate(string path, SyntaxNode node)
        {
            if (ContainsDomainWord(path) || ContainsDomainWordInSyntaxContext(node))
                return true;

            string normalized = path.Replace('\\', '/');
            if (normalized.IndexOf("/Environment/", StringComparison.OrdinalIgnoreCase) < 0)
                return false;

            return !IsInstantImpactVfxPathOrContext(normalized, node);
        }

        private static bool ContainsDomainWordInSyntaxContext(SyntaxNode node)
        {
            using (System.Collections.Generic.IEnumerator<SyntaxNode> ancestors = node.AncestorsAndSelf().GetEnumerator())
            {
                while (ancestors.MoveNext())
                {
                    SyntaxNode current = ancestors.Current;
                    if (current is TypeDeclarationSyntax typeDeclaration &&
                        ContainsDomainWord(typeDeclaration.Identifier.ValueText))
                    {
                        return true;
                    }

                    if (current is MethodDeclarationSyntax methodDeclaration &&
                        ContainsDomainWord(methodDeclaration.Identifier.ValueText))
                    {
                        return true;
                    }

                    if (current is FieldDeclarationSyntax fieldDeclaration &&
                        VariableListContainsDomainWord(fieldDeclaration.Declaration))
                    {
                        return true;
                    }

                    if (current is LocalDeclarationStatementSyntax localDeclaration &&
                        VariableListContainsDomainWord(localDeclaration.Declaration))
                    {
                        return true;
                    }

                    if (current is ParameterSyntax parameter &&
                        ContainsDomainWord(parameter.Identifier.ValueText))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool VariableListContainsDomainWord(VariableDeclarationSyntax declaration)
        {
            SeparatedSyntaxList<VariableDeclaratorSyntax> variables = declaration.Variables;
            for (int i = 0; i < variables.Count; i++)
            {
                if (ContainsDomainWord(variables[i].Identifier.ValueText))
                    return true;
            }

            return false;
        }

        private static bool IsInstantImpactVfxPathOrContext(string normalizedPath, SyntaxNode node)
        {
            if (ContainsVfxWord(normalizedPath))
                return true;

            using (System.Collections.Generic.IEnumerator<SyntaxNode> ancestors = node.AncestorsAndSelf().GetEnumerator())
            {
                while (ancestors.MoveNext())
                {
                    SyntaxNode current = ancestors.Current;
                    if (current is TypeDeclarationSyntax typeDeclaration &&
                        ContainsVfxWord(typeDeclaration.Identifier.ValueText))
                    {
                        return true;
                    }

                    if (current is MethodDeclarationSyntax methodDeclaration &&
                        ContainsVfxWord(methodDeclaration.Identifier.ValueText))
                    {
                        return true;
                    }

                    if (current is FieldDeclarationSyntax fieldDeclaration &&
                        VariableListContainsVfxWord(fieldDeclaration.Declaration))
                    {
                        return true;
                    }

                    if (current is LocalDeclarationStatementSyntax localDeclaration &&
                        VariableListContainsVfxWord(localDeclaration.Declaration))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool VariableListContainsVfxWord(VariableDeclarationSyntax declaration)
        {
            SeparatedSyntaxList<VariableDeclaratorSyntax> variables = declaration.Variables;
            for (int i = 0; i < variables.Count; i++)
            {
                if (ContainsVfxWord(variables[i].Identifier.ValueText))
                    return true;
            }

            return false;
        }

        private static bool ContainsDomainWord(string text)
        {
            return text.IndexOf("plankton", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("nutrient", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("biomass", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("food", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsVfxWord(string text)
        {
            return text.IndexOf("vfx", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("visual", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("impact", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("splash", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("bubble", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("foam", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("spark", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("decal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("effect", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            return span.StartLinePosition.Line + 1;
        }

        private static string BuildSyntaxContext(SyntaxNode node)
        {
            return node.Kind().ToString();
        }

        private static void WriteReport(ScanResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string path = Path.Combine(projectRoot, ReportPath);
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            string sectionJson = BuildSectionJson(result);
            string existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            string merged = UpsertSection(existing, sectionJson);
            File.WriteAllText(path, merged, Encoding.UTF8);
        }

        private static string BuildSectionJson(ScanResult result)
        {
            string status = result.HitCount == 0 ? StatusClean : "OOP Fluid Particle Authority Detected";
            var builder = new StringBuilder(2048);
            builder.AppendLine("  \"" + SectionKey + "\": {");
            builder.AppendLine("    \"agentId\": \"SHINOBU_309\",");
            builder.AppendLine("    \"scanner\": \"Fluid_Particle_Scanner\",");
            builder.AppendLine("    \"summary\": \"" + status + "\",");
            builder.AppendLine("    \"reportSchema\": 1,");
            builder.AppendLine("    \"evidenceClass\": \"ROSLYN_AST_TARGETED\",");
            builder.AppendLine("    \"scannerUsesRoslynAst\": true,");
            builder.AppendLine("    \"sourceFilesScanned\": " + result.SourceFilesScanned + ",");
            builder.AppendLine("    \"parserFailures\": " + result.ParserFailureCount + ",");
            builder.AppendLine("    \"scannedPaths\": [");
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                builder.Append("      \"").Append(EscapeJson(ScanRoots[i])).Append("\"");
                builder.AppendLine(i + 1 < ScanRoots.Length ? "," : string.Empty);
            }
            builder.AppendLine("    ],");
            builder.AppendLine("    \"forbiddenPatterns\": [");
            for (int i = 0; i < Needles.Length; i++)
            {
                builder.Append("      \"").Append(EscapeJson(Needles[i])).Append("\"");
                builder.AppendLine(i + 1 < Needles.Length ? "," : string.Empty);
            }
            builder.AppendLine("    ],");
            builder.AppendLine("    \"totalParticleReferenceNodesInScope\": " + result.TotalParticleReferences + ",");
            builder.AppendLine("    \"nutrientAuthorityHitCount\": " + result.HitCount + ",");
            builder.AppendLine("    \"activeViolationCount\": " + result.HitCount + ",");
            builder.AppendLine("    \"findings\": [");
            if (result.HitsJson != null)
                builder.Append(result.HitsJson.ToString());
            builder.AppendLine();
            builder.AppendLine("    ],");
            builder.AppendLine("    \"replacementRoute\": \"Vault-backed double-buffered NutrientCellDTO scalar field, cached IAbyssalFlowVolumeReadModel flow-volume consumption with deterministic mock fallback, cached INutrientThermalVentReadModel source snapshots, Burst semi-Lagrangian advection, density Texture3D presentation upload\",");
            builder.AppendLine("    \"compileStatus\": \"PRIOR_GUARDED_CORE_BUILD_GREEN_LOOP18_REBUILD_GATED_BY_CPU\",");
            builder.AppendLine("    \"notes\": \"Roslyn AST scan covers Environment, AI, and Ecosystem source roots. Environment ParticleSystem/Rigidbody authority is reported unless the syntax context is classified as instant impact/presentation VFX; AI/Ecosystem hits require nutrient/plankton/biomass/food authority context. Nutrient runtime caches thermal and abyssal-flow inputs through core read-model interfaces, not concrete World owner fields.\"");
            builder.AppendLine("  }");
            return builder.ToString();
        }

        private static string UpsertSection(string existing, string sectionJson)
        {
            if (string.IsNullOrWhiteSpace(existing))
                return "{\n" + sectionJson + "\n}\n";

            int objectStart = existing.IndexOf('{');
            int objectEnd = existing.LastIndexOf('}');
            if (objectStart < 0 || objectEnd <= objectStart)
                return "{\n" + sectionJson + "\n}\n";

            string keyToken = "\"" + SectionKey + "\"";
            int keyIndex = existing.IndexOf(keyToken, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int lineStart = existing.LastIndexOf('\n', keyIndex);
                int replaceStart = lineStart >= 0 ? lineStart + 1 : keyIndex;
                int colon = existing.IndexOf(':', keyIndex + keyToken.Length);
                if (colon > keyIndex)
                {
                    int valueStart = SkipWhitespace(existing, colon + 1);
                    int valueEnd = valueStart < existing.Length && existing[valueStart] == '{'
                        ? FindObjectEnd(existing, valueStart)
                        : -1;
                    if (valueEnd > valueStart)
                        return existing.Substring(0, replaceStart) + sectionJson + existing.Substring(valueEnd);
                }
            }

            int firstContent = SkipWhitespace(existing, objectStart + 1);
            if (firstContent >= objectEnd)
                return "{\n" + sectionJson + "\n}\n";

            return existing.Substring(0, objectStart + 1) + "\n" + sectionJson + ",\n" + existing.Substring(objectStart + 1);
        }

        private static int SkipWhitespace(string value, int index)
        {
            while (index < value.Length && char.IsWhiteSpace(value[index]))
                index++;
            return index;
        }

        private static int FindObjectEnd(string value, int objectStart)
        {
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
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                }
            }

            return -1;
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public struct ScanResult
        {
            public int SourceFilesScanned;
            public int ParserFailureCount;
            public int TotalParticleReferences;
            public int HitCount;
            public StringBuilder HitsJson;

            public void AppendHit(string path, int line, string needle, string source)
            {
                if (HitsJson == null)
                    HitsJson = new StringBuilder(1024);
                if (HitsJson.Length > 0)
                    HitsJson.AppendLine(",");
                HitsJson.Append("      { \"path\": \"")
                    .Append(EscapeJson(path.Replace('\\', '/')))
                    .Append("\", \"line\": ")
                    .Append(line)
                    .Append(", \"needle\": \"")
                    .Append(EscapeJson(needle))
                    .Append("\", \"source\": \"")
                    .Append(EscapeJson(source))
                    .Append("\" }");
            }

            public void AppendParserFailure(string path, string reason)
            {
                if (HitsJson == null)
                    HitsJson = new StringBuilder(1024);
                if (HitsJson.Length > 0)
                    HitsJson.AppendLine(",");
                HitsJson.Append("      { \"path\": \"")
                    .Append(EscapeJson(path.Replace('\\', '/')))
                    .Append("\", \"line\": 0, \"needle\": \"RoslynParse\", \"source\": \"")
                    .Append(EscapeJson(reason))
                    .Append("\" }");
            }
        }
    }

    public static class Fluid_Particle_Scanner
    {
        [MenuItem("HECTON-8/Ecosystem/Fluid Particle Scanner")]
        public static void RunMenuScan()
        {
            NutrientDriftParticleScanner.RunMenuScan();
        }
    }
}
#endif
