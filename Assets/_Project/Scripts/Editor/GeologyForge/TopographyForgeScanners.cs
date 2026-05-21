#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor.GeologyForge
{
    internal static class LegacyMapMagicGraphInquisition
    {
        private static readonly string[] GraphSearchRoots =
        {
            "Assets/_Project/Data/Terrain",
            "Assets/_Project/Data/World/Sandbox",
            "Assets/MapMagic/Map_Graph"
        };

        [MenuItem("HECTON-8/Geology Forge/Topography Forge/Scan Legacy MapMagic Graphs", false, 186)]
        public static void ScanAndWriteReport()
        {
            Directory.CreateDirectory("Docs/Reports");
            int filesScanned = 0;
            int graphFiles = 0;
            int noiseNodes = 0;
            int erosionNodes = 0;
            int terraceNodes = 0;
            int heightOutputs = 0;
            int targetTerrainFindings = 0;
            StringBuilder findings = new StringBuilder(4096); // COLD ALLOC: graph inquisition JSON findings - owner: SHINOBU_240

            for (int r = 0; r < GraphSearchRoots.Length; r++)
            {
                string root = GraphSearchRoots[r];
                if (!Directory.Exists(root))
                    continue;

                string[] files = Directory.GetFiles(root, "*.asset", SearchOption.AllDirectories);
                Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < files.Length; i++)
                {
                    filesScanned++;
                    string text = File.ReadAllText(files[i]);
                    if (!Contains(text, "MapMagic") && !Contains(text, "Noise200") && !Contains(text, "HeightOutput200"))
                        continue;

                    int fileNoise = CountToken(text, "Noise200");
                    int fileErosion = CountToken(text, "Erosion200");
                    int fileTerrace = CountToken(text, "Terrace200");
                    int fileOutput = CountToken(text, "HeightOutput200");
                    graphFiles++;
                    noiseNodes += fileNoise;
                    erosionNodes += fileErosion;
                    terraceNodes += fileTerrace;
                    heightOutputs += fileOutput;
                    if (IsUnder(files[i], "Assets/_Project/Data/Terrain"))
                        targetTerrainFindings++;

                    findings.Append("    { \"path\": \"").Append(Escape(files[i].Replace('\\', '/'))).Append("\", ");
                    findings.Append("\"noise_nodes\": ").Append(fileNoise.ToString(CultureInfo.InvariantCulture)).Append(", ");
                    findings.Append("\"erosion_nodes\": ").Append(fileErosion.ToString(CultureInfo.InvariantCulture)).Append(", ");
                    findings.Append("\"terrace_nodes\": ").Append(fileTerrace.ToString(CultureInfo.InvariantCulture)).Append(", ");
                    findings.Append("\"height_outputs\": ").Append(fileOutput.ToString(CultureInfo.InvariantCulture)).Append(", ");
                    findings.Append("\"discard_reason\": \"legacy generic graph; replaced by SHINOBU_240 offline Burst ridged multifractal/domain warp h8bin bake\" },").AppendLine();
                }
            }

            if (findings.Length > 0)
                findings.Length -= Environment.NewLine.Length + 1;

            StringBuilder report = new StringBuilder(8192); // COLD ALLOC: graph inquisition JSON report - owner: SHINOBU_240
            report.AppendLine("{");
            AppendJson(report, "agent", "SHINOBU_240", true);
            AppendJson(report, "target_root", "Assets/_Project/Data/Terrain", true);
            AppendJson(report, "files_scanned", filesScanned, true);
            AppendJson(report, "target_terrain_findings", targetTerrainFindings, true);
            AppendJson(report, "legacy_graph_files", graphFiles, true);
            AppendJson(report, "noise_nodes", noiseNodes, true);
            AppendJson(report, "erosion_nodes", erosionNodes, true);
            AppendJson(report, "terrace_nodes", terraceNodes, true);
            AppendJson(report, "height_outputs", heightOutputs, true);
            AppendJson(report, "mathematical_failure", "Noise200/Erosion200/Terrace200 graph stacks generate generic Perlin-like volcanic pimples and hide runtime terrain mutation cost.", true);
            report.AppendLine("  \"findings\": [");
            report.Append(findings.ToString());
            report.AppendLine();
            report.AppendLine("  ]");
            report.AppendLine("}");
            File.WriteAllText(TopographyForgeConstants.MapMagicInquisitionReportPath, report.ToString());
            Debug.Log("[LegacyMapMagicGraphInquisition] legacy graphs=" + graphFiles + ", target findings=" + targetTerrainFindings + ".");
        }

        private static bool IsUnder(string path, string root)
        {
            string full = Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
            string rootFull = Path.GetFullPath(root).Replace('\\', '/').TrimEnd('/');
            return full.StartsWith(rootFull + "/", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(full, rootFull, StringComparison.OrdinalIgnoreCase);
        }

        private static bool Contains(string text, string token)
        {
            return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountToken(string text, string token)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.OrdinalIgnoreCase);
                if (found < 0)
                    break;
                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(Escape(value)).Append('"');
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    internal static class Terrain_Runtime_Scanner
    {
        private static readonly string[] ForbiddenInvocationTokens =
        {
            "Mathf.PerlinNoise",
            "SetHeights",
            "SetHeightsDelayLOD",
            "Refresh",
            "TryGetQuantizedHeightmapPayload",
            "SyncHeightmap",
            "ApplyDomainWarp",
            "EvaluateRidged",
            "RidgedMultifractal",
            "DomainWarp"
        };

        private static readonly string[] ForbiddenIdentifierTokens =
        {
            "TerrainData",
            "MapMagicObject",
            "Noise200",
            "HeightOutput200",
            "Erosion200",
            "Terrace200",
            "TerrainChunkGeneratedEvents",
            "TerrainSeamHeightmap"
        };

        [MenuItem("HECTON-8/Geology Forge/Topography Forge/Scan Runtime Terrain Debt", false, 187)]
        public static void ScanAndWriteReport()
        {
            Directory.CreateDirectory("Docs/Reports");
            int filesScanned = 0;
            int findings = 0;
            int editorExcluded = 0;
            int parserFailures = 0;
            int guardedFindings = 0;
            StringBuilder findingJson = new StringBuilder(8192); // COLD ALLOC: runtime terrain scanner findings - owner: SHINOBU_240
            string[] files = Directory.GetFiles("Assets/_Project/Scripts", "*.cs", SearchOption.AllDirectories);
            Array.Sort(files, StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    editorExcluded++;
                    continue;
                }

                filesScanned++;
                string text = File.ReadAllText(files[i]);
                SyntaxTree tree;
                try
                {
                    tree = CSharpSyntaxTree.ParseText(text);
                }
                catch (Exception)
                {
                    parserFailures++;
                    continue;
                }

                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                using (IEnumerator<SyntaxNode> invocationEnumerator = root.DescendantNodes().GetEnumerator())
                {
                    while (invocationEnumerator.MoveNext())
                    {
                        SyntaxNode syntaxNode = invocationEnumerator.Current;
                        if (!(syntaxNode is InvocationExpressionSyntax invocation))
                            continue;

                        if (!TryResolveForbiddenInvocation(invocation, out string token))
                            continue;

                        bool guarded = HasPlayModeFence(invocation);
                        if (guarded)
                            guardedFindings++;
                        AppendFinding(findingJson, path, invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1, token, "invocation", guarded);
                        findings++;
                    }
                }

                using (IEnumerator<SyntaxNode> identifierEnumerator = root.DescendantNodes().GetEnumerator())
                {
                    while (identifierEnumerator.MoveNext())
                    {
                        SyntaxNode syntaxNode = identifierEnumerator.Current;
                        if (!(syntaxNode is IdentifierNameSyntax identifier))
                            continue;

                        if (!TryResolveForbiddenIdentifier(identifier, out string token))
                            continue;

                        bool guarded = HasPlayModeFence(identifier);
                        if (guarded)
                            guardedFindings++;
                        AppendFinding(findingJson, path, identifier.GetLocation().GetLineSpan().StartLinePosition.Line + 1, token, "identifier", guarded);
                        findings++;
                    }
                }
            }

            if (findingJson.Length > 0)
                findingJson.Length -= Environment.NewLine.Length + 1;

            StringBuilder report = new StringBuilder(12288); // COLD ALLOC: runtime terrain scanner JSON report - owner: SHINOBU_240
            report.AppendLine("{");
            AppendJson(report, "agent", "SHINOBU_240", true);
            AppendJson(report, "summary", findings == 0 ? "Runtime Terrain Calculations Eradicated" : "Runtime Terrain Calculations Debt Remains", true);
            AppendJson(report, "files_scanned", filesScanned, true);
            AppendJson(report, "editor_files_excluded", editorExcluded, true);
            AppendJson(report, "parser", "ROSLYN_AST", true);
            AppendJson(report, "parser_failures", parserFailures, true);
            AppendJson(report, "finding_count", findings, true);
            AppendJson(report, "guarded_finding_count", guardedFindings, true);
            AppendJson(report, "report_scope", "SHINOBU_240-owned runtime terrain debt report; does not overwrite other agents' WORLD_OPTIMIZATION_REPORT.json artifacts.", true);
            AppendJson(report, "static_heightmaps_are_rollback_excluded", true, true);
            report.AppendLine("  \"findings\": [");
            report.Append(findingJson.ToString());
            report.AppendLine();
            report.AppendLine("  ]");
            report.AppendLine("}");
            File.WriteAllText(TopographyForgeConstants.RuntimeScannerReportPath, report.ToString());
            Debug.Log("[Terrain_Runtime_Scanner] findings=" + findings + ".");
        }

        private static bool TryResolveForbiddenInvocation(InvocationExpressionSyntax invocation, out string token)
        {
            string expression = invocation.Expression.ToString();
            for (int i = 0; i < ForbiddenInvocationTokens.Length; i++)
            {
                string candidate = ForbiddenInvocationTokens[i];
                if (expression.IndexOf(candidate, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    if (string.Equals(candidate, "Refresh", StringComparison.OrdinalIgnoreCase) &&
                        expression.IndexOf("tile.Refresh", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }

                    token = candidate;
                    return true;
                }
            }

            token = string.Empty;
            return false;
        }

        private static bool TryResolveForbiddenIdentifier(IdentifierNameSyntax identifier, out string token)
        {
            string value = identifier.Identifier.ValueText;
            for (int i = 0; i < ForbiddenIdentifierTokens.Length; i++)
            {
                if (string.Equals(value, ForbiddenIdentifierTokens[i], StringComparison.Ordinal))
                {
                    token = value;
                    return true;
                }
            }

            token = string.Empty;
            return false;
        }

        private static bool HasPlayModeFence(SyntaxNode node)
        {
            SyntaxNode current = node;
            while (current != null)
            {
                StatementSyntax statement = FindStatementInBlock(current);
                if (statement == null)
                    return false;

                BlockSyntax block = statement.Parent as BlockSyntax;
                if (block == null)
                    return false;

                SyntaxList<StatementSyntax> statements = block.Statements;
                for (int i = 0; i < statements.Count; i++)
                {
                    StatementSyntax candidate = statements[i];
                    if (candidate == statement)
                        break;
                    if (IsPlayModeReturnGuard(candidate))
                        return true;
                }

                current = block.Parent;
            }

            return false;
        }

        private static StatementSyntax FindStatementInBlock(SyntaxNode node)
        {
            using (IEnumerator<SyntaxNode> ancestorEnumerator = node.AncestorsAndSelf().GetEnumerator())
            {
                while (ancestorEnumerator.MoveNext())
                {
                    StatementSyntax statement = ancestorEnumerator.Current as StatementSyntax;
                    if (statement != null && statement.Parent is BlockSyntax)
                        return statement;
                }
            }

            return null;
        }

        private static bool IsPlayModeReturnGuard(StatementSyntax statement)
        {
            IfStatementSyntax guard = statement as IfStatementSyntax;
            return guard != null &&
                   IsPositivePlayModeCondition(guard.Condition) &&
                   ContainsDirectReturn(guard.Statement);
        }

        private static bool ContainsDirectReturn(StatementSyntax statement)
        {
            if (statement is ReturnStatementSyntax)
                return true;

            BlockSyntax block = statement as BlockSyntax;
            if (block == null)
                return false;

            SyntaxList<StatementSyntax> statements = block.Statements;
            for (int i = 0; i < statements.Count; i++)
                if (statements[i] is ReturnStatementSyntax)
                    return true;

            return false;
        }

        private static bool IsPositivePlayModeCondition(ExpressionSyntax expression)
        {
            ExpressionSyntax condition = StripParentheses(expression);
            if (IsPlayModeMember(condition))
                return true;

            BinaryExpressionSyntax binary = condition as BinaryExpressionSyntax;
            if (binary == null)
                return false;

            ExpressionSyntax left = StripParentheses(binary.Left);
            ExpressionSyntax right = StripParentheses(binary.Right);
            if (binary.IsKind(SyntaxKind.EqualsExpression))
                return (IsPlayModeMember(left) && IsTrueLiteral(right)) ||
                       (IsTrueLiteral(left) && IsPlayModeMember(right));
            if (binary.IsKind(SyntaxKind.NotEqualsExpression))
                return (IsPlayModeMember(left) && IsFalseLiteral(right)) ||
                       (IsFalseLiteral(left) && IsPlayModeMember(right));

            return false;
        }

        private static ExpressionSyntax StripParentheses(ExpressionSyntax expression)
        {
            ExpressionSyntax current = expression;
            while (current is ParenthesizedExpressionSyntax parenthesized)
                current = parenthesized.Expression;
            return current;
        }

        private static bool IsPlayModeMember(ExpressionSyntax expression)
        {
            MemberAccessExpressionSyntax member = expression as MemberAccessExpressionSyntax;
            string owner = member != null ? member.Expression.ToString() : string.Empty;
            return member != null &&
                   string.Equals(member.Name.Identifier.ValueText, "isPlaying", StringComparison.Ordinal) &&
                   (string.Equals(owner, "Application", StringComparison.Ordinal) ||
                    string.Equals(owner, "UnityEngine.Application", StringComparison.Ordinal) ||
                    string.Equals(owner, "global::UnityEngine.Application", StringComparison.Ordinal));
        }

        private static bool IsTrueLiteral(ExpressionSyntax expression)
        {
            return expression.IsKind(SyntaxKind.TrueLiteralExpression);
        }

        private static bool IsFalseLiteral(ExpressionSyntax expression)
        {
            return expression.IsKind(SyntaxKind.FalseLiteralExpression);
        }

        private static void AppendFinding(StringBuilder builder, string path, int line, string token, string syntaxKind, bool guarded)
        {
            builder.Append("    { \"path\": \"").Append(Escape(path)).Append("\", ");
            builder.Append("\"line\": ").Append(line.ToString(CultureInfo.InvariantCulture)).Append(", ");
            builder.Append("\"token\": \"").Append(Escape(token)).Append("\", ");
            builder.Append("\"syntax\": \"").Append(Escape(syntaxKind)).Append("\", ");
            builder.Append("\"play_mode_fenced\": ").Append(guarded ? "true" : "false").Append(" },").AppendLine();
        }

        private static void AppendJson(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(Escape(value)).Append('"');
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value.ToString(CultureInfo.InvariantCulture));
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static void AppendJson(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            builder.AppendLine(comma ? "," : string.Empty);
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
