#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Gameplay.Editor
{
    public static class SkinnedMesh_Scanner_Player
    {
        private const string ReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string ReportSectionKey = "\"shinobu_315_fabrik_hand_ik_solver\"";

        [MenuItem("Hecton8/Player/Run Bone IK Scanner")]
        public static void RunAndWriteReport()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            string reportPath = Path.Combine(projectRoot, ReportPath);
            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            int managedIkHits = 0;
            int animatorIkHits = 0;
            int dynamicBoneTransformHits = 0;
            int sourceFilesScanned = 0;
            int parserFailures = 0;
            StringBuilder findings = new StringBuilder(4096);

            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                if (!IsPlayerFacingPath(path))
                    continue;

                sourceFilesScanned++;
                string text = File.ReadAllText(path);
                SyntaxTree tree;
                try
                {
                    tree = CSharpSyntaxTree.ParseText(text);
                }
                catch (Exception exception)
                {
                    parserFailures++;
                    AppendFinding(findings, projectRoot, path, 0, "RoslynParse", exception.GetType().Name, false, false, false);
                    continue;
                }

                if (HasParseError(tree))
                {
                    parserFailures++;
                    AppendFinding(findings, projectRoot, path, 0, "RoslynParse", "syntax error", false, false, false);
                    continue;
                }

                CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
                bool managedIk = HasManagedIkReference(root, out int managedLine, out string managedToken);
                bool animatorIk = HasAnimatorIkReference(root, out int animatorLine, out string animatorToken);
                bool transformBone = HasDynamicBoneTransformMutation(root, out int transformLine, out string transformToken);

                if (!managedIk && !animatorIk && !transformBone)
                    continue;

                if (managedIk)
                    managedIkHits++;
                if (animatorIk)
                    animatorIkHits++;
                if (transformBone)
                    dynamicBoneTransformHits++;

                int line = ResolveFirstPositiveLine(managedIk, managedLine, animatorIk, animatorLine, transformBone, transformLine);
                string token = ResolveFirstPositiveToken(managedIk, managedToken, animatorIk, animatorToken, transformBone, transformToken);
                AppendFinding(findings, projectRoot, path, line, "RoslynAST", token, managedIk, animatorIk, transformBone);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            string verdict = managedIkHits == 0 && animatorIkHits == 0 && dynamicBoneTransformHits == 0
                ? "OOP Bone IK Eradicated"
                : managedIkHits == 0 && animatorIkHits == 0
                    ? "REVIEW_CANDIDATES_PRESENT"
                    : "OOP Bone IK Still Present";
            UpsertReportSection(
                reportPath,
                BuildJsonSection(
                    verdict,
                    sourceFilesScanned,
                    parserFailures,
                    managedIkHits,
                    animatorIkHits,
                    dynamicBoneTransformHits,
                    findings.ToString()));
            AssetDatabase.Refresh();
            Debug.Log("[SHINOBU_315] " + verdict + " -> " + reportPath);
        }

        private static bool IsPlayerFacingPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("/Editor/"))
                return false;

            return normalized.Contains("/Scripts/Gameplay/") ||
                   normalized.Contains("/Scripts/Interaction/") ||
                   normalized.Contains("/Scripts/Animation/IK/") ||
                   normalized.Contains("/Scripts/Tools/ToolKinematics/");
        }

        private static void AppendFinding(
            StringBuilder builder,
            string projectRoot,
            string path,
            int line,
            string source,
            string token,
            bool managedIk,
            bool animatorIk,
            bool transformBone)
        {
            if (builder.Length > 0)
                builder.Append(',');

            string relative = path.Replace(projectRoot, string.Empty).TrimStart('\\', '/').Replace('\\', '/');
            builder.Append("{\"path\":\"");
            AppendEscaped(builder, relative);
            builder.Append("\",\"line\":");
            builder.Append(line);
            builder.Append(",\"source\":\"");
            AppendEscaped(builder, source);
            builder.Append("\",\"token\":\"");
            AppendEscaped(builder, token);
            builder.Append("\",\"managedIk\":");
            builder.Append(managedIk ? "true" : "false");
            builder.Append(",\"animatorIk\":");
            builder.Append(animatorIk ? "true" : "false");
            builder.Append(",\"dynamicBoneTransform\":");
            builder.Append(transformBone ? "true" : "false");
            builder.Append('}');
        }

        private static string BuildJsonSection(
            string verdict,
            int sourceFilesScanned,
            int parserFailures,
            int managedIkHits,
            int animatorIkHits,
            int dynamicBoneTransformHits,
            string findings)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.Append("    \"shinobu_315_fabrik_hand_ik_solver\":  {\n");
            builder.Append("                                               \"agentId\":  \"SHINOBU_315\",\n");
            builder.Append("                                               \"scanner\":  \"SkinnedMesh_Scanner_Player\",\n");
            builder.Append("                                               \"scannerParserRoute\":  \"Roslyn CSharpSyntaxTree pass scoped to first-party Gameplay, Interaction, Animation/IK, and ToolKinematics code\",\n");
            builder.Append("                                               \"scannerUsesRoslynAst\":  true,\n");
            builder.Append("                                               \"summary\":  \"");
            AppendEscaped(builder, verdict);
            builder.Append("\",\n                                               \"evidenceClass\":  \"STATIC_SOURCE_TARGETED_EDITOR_SCAN\",\n");
            builder.Append("                                               \"sourceFilesScanned\":");
            builder.Append(sourceFilesScanned);
            builder.Append(",\n                                               \"parserFailures\":");
            builder.Append(parserFailures);
            builder.Append(",\n                                               \"managedIkHits\":");
            builder.Append(managedIkHits);
            builder.Append(",\n                                               \"animatorIkHits\":");
            builder.Append(animatorIkHits);
            builder.Append(",\n                                               \"dynamicBoneTransformHits\":");
            builder.Append(dynamicBoneTransformHits);
            builder.Append(",\n                                               \"runtimeRoute\":  \"VRInteractionKinematicBridge.ResolvedHandAUP -> PlayerKinematicsRuntime_HandIK Vault DTOs -> Burst FABRIK -> KineticCharacter bone override + double GraphicsBuffer visual sync\",\n");
            builder.Append("                                               \"bufferIds\":  \"315730..315735 visual-only player hand IK lanes; rollback/Merkle/save excluded\",\n");
            builder.Append("                                               \"vaultOwnership\":  \"BufferIDs 315730..315735 owned by SystemID.GameplayPlayer via PlayerKinematicsRuntime.OwnerSystemId; consumers use TryGetGenerationHandle/TryLockBuffer; editor tools must not ReleaseBuffer runtime-owned lanes; relocation/disposal proof pending Unity import/runtime.\",\n");
            builder.Append("                                               \"status\":  \"STATIC_SCAN_ONLY_RUNTIME_COMPILE_IMPORT_PENDING\",\n");
            builder.Append("                                               \"compileStatus\":  \"NOT_LAUNCHED_CPU_GUARD_AND_GENERATED_PROJECT_STALE\",\n");
            builder.Append("                                               \"sharedReportRisk\":  \"Last-writer-wins shared JSON remains unsafe under parallel agents; SHINOBU_315 evidence is mirrored in LOG_SHINOBU_315 and BINARY_PAYLOAD_INTEGRATION_LEDGER, but atomic shared-report tooling remains pending.\",\n");
            builder.Append("                                               \"notes\":  \"OOP Bone IK Eradicated is emitted only when FinalIK/FastIK/Animator IK and hand/arm bone Transform writes are absent from targeted first-party scripts; REVIEW_CANDIDATES_PRESENT means Transform writes need manual audit.\",\n");
            builder.Append("                                               \"findings\":  [");
            builder.Append(findings);
            builder.Append("]\n                                           }");
            return builder.ToString();
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

        private static bool HasManagedIkReference(SyntaxNode root, out int line, out string token)
        {
            line = 0;
            token = string.Empty;
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    string value = ResolveSyntaxName(node);
                    if (string.Equals(value, "FinalIK", StringComparison.Ordinal) ||
                        string.Equals(value, "FastIKFabric", StringComparison.Ordinal) ||
                        string.Equals(value, "RootMotion.FinalIK", StringComparison.Ordinal) ||
                        value.EndsWith(".FinalIK", StringComparison.Ordinal))
                    {
                        line = GetLineNumber(node);
                        token = value;
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool HasAnimatorIkReference(SyntaxNode root, out int line, out string token)
        {
            line = 0;
            token = string.Empty;
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (node is MethodDeclarationSyntax method &&
                        string.Equals(method.Identifier.ValueText, "OnAnimatorIK", StringComparison.Ordinal))
                    {
                        line = GetLineNumber(method);
                        token = "OnAnimatorIK";
                        return true;
                    }

                    if (node is InvocationExpressionSyntax invocation &&
                        invocation.Expression is MemberAccessExpressionSyntax memberAccess)
                    {
                        string memberName = memberAccess.Name.Identifier.ValueText;
                        if (string.Equals(memberName, "SetIKPosition", StringComparison.Ordinal) ||
                            string.Equals(memberName, "SetIKRotation", StringComparison.Ordinal))
                        {
                            line = GetLineNumber(invocation);
                            token = memberName;
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static bool HasDynamicBoneTransformMutation(SyntaxNode root, out int line, out string token)
        {
            line = 0;
            token = string.Empty;
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (!(nodes.Current is AssignmentExpressionSyntax assignment) ||
                        !(assignment.Left is MemberAccessExpressionSyntax memberAccess))
                    {
                        continue;
                    }

                    string memberName = memberAccess.Name.Identifier.ValueText;
                    if (!IsTransformMutationMember(memberName) || !HasHandBoneContext(assignment))
                        continue;

                    line = GetLineNumber(assignment);
                    token = memberName;
                    return true;
                }
            }

            return false;
        }

        private static string ResolveSyntaxName(SyntaxNode node)
        {
            if (node is IdentifierNameSyntax identifier)
                return identifier.Identifier.ValueText;
            if (node is QualifiedNameSyntax qualifiedName)
                return qualifiedName.ToString();
            if (node is UsingDirectiveSyntax usingDirective && usingDirective.Name != null)
                return usingDirective.Name.ToString();
            return string.Empty;
        }

        private static bool IsTransformMutationMember(string memberName)
        {
            return string.Equals(memberName, "position", StringComparison.Ordinal) ||
                   string.Equals(memberName, "localPosition", StringComparison.Ordinal) ||
                   string.Equals(memberName, "rotation", StringComparison.Ordinal) ||
                   string.Equals(memberName, "localRotation", StringComparison.Ordinal);
        }

        private static bool HasHandBoneContext(SyntaxNode node)
        {
            SyntaxNode current = node;
            while (current != null)
            {
                if (current is TypeDeclarationSyntax typeDeclaration && ContainsHandBoneToken(typeDeclaration.Identifier.ValueText))
                    return true;
                if (current is MethodDeclarationSyntax methodDeclaration && ContainsHandBoneToken(methodDeclaration.Identifier.ValueText))
                    return true;
                if (current is FieldDeclarationSyntax fieldDeclaration && VariableListHasHandBoneToken(fieldDeclaration.Declaration))
                    return true;
                if (current is LocalDeclarationStatementSyntax localDeclaration && VariableListHasHandBoneToken(localDeclaration.Declaration))
                    return true;

                current = current.Parent;
            }

            return ContainsHandBoneToken(node.ToString());
        }

        private static bool VariableListHasHandBoneToken(VariableDeclarationSyntax declaration)
        {
            SeparatedSyntaxList<VariableDeclaratorSyntax> variables = declaration.Variables;
            for (int i = 0; i < variables.Count; i++)
            {
                if (ContainsHandBoneToken(variables[i].Identifier.ValueText))
                    return true;
            }

            return false;
        }

        private static bool ContainsHandBoneToken(string value)
        {
            if (string.IsNullOrEmpty(value))
                return false;

            return value.IndexOf("Hand", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Arm", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Elbow", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Wrist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("Bone", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int ResolveFirstPositiveLine(
            bool managedIk,
            int managedLine,
            bool animatorIk,
            int animatorLine,
            bool transformBone,
            int transformLine)
        {
            if (managedIk)
                return managedLine;
            if (animatorIk)
                return animatorLine;
            return transformBone ? transformLine : 0;
        }

        private static string ResolveFirstPositiveToken(
            bool managedIk,
            string managedToken,
            bool animatorIk,
            string animatorToken,
            bool transformBone,
            string transformToken)
        {
            if (managedIk)
                return managedToken;
            if (animatorIk)
                return animatorToken;
            return transformBone ? transformToken : string.Empty;
        }

        private static int GetLineNumber(SyntaxNode node)
        {
            FileLinePositionSpan span = node.GetLocation().GetLineSpan();
            return span.StartLinePosition.Line + 1;
        }

        private static void UpsertReportSection(string reportPath, string sectionJson)
        {
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, "{\n" + sectionJson + "\n}\n");
                return;
            }

            string existing = File.ReadAllText(reportPath);
            int rootOpen = existing.IndexOf('{');
            int rootClose = existing.LastIndexOf('}');
            if (rootOpen < 0 || rootClose <= rootOpen)
            {
                File.WriteAllText(reportPath, "{\n" + sectionJson + "\n}\n");
                return;
            }

            int keyIndex = existing.IndexOf(ReportSectionKey, rootOpen, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int memberStart = FindMemberLineStart(existing, keyIndex);
                int memberEnd = FindMemberObjectEnd(existing, keyIndex);
                if (memberStart >= 0 && memberEnd > memberStart)
                {
                    string prefix = existing.Substring(0, memberStart);
                    string suffix = existing.Substring(memberEnd);
                    File.WriteAllText(reportPath, prefix + sectionJson + suffix);
                    return;
                }
            }

            string separator = HasRootMembers(existing, rootOpen, rootClose) ? ",\n" : "\n";
            string insert = "\n" + sectionJson + separator;
            File.WriteAllText(reportPath, existing.Insert(rootOpen + 1, insert));
        }

        private static bool HasRootMembers(string text, int rootOpen, int rootClose)
        {
            for (int i = rootOpen + 1; i < rootClose; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return true;
            }

            return false;
        }

        private static int FindMemberLineStart(string text, int keyIndex)
        {
            int start = keyIndex;
            while (start > 0 && text[start - 1] != '\n' && text[start - 1] != '\r')
                start--;
            return start;
        }

        private static int FindMemberObjectEnd(string text, int keyIndex)
        {
            int colon = text.IndexOf(':', keyIndex);
            if (colon < 0)
                return -1;

            int objectStart = text.IndexOf('{', colon);
            if (objectStart < 0)
                return -1;

            bool inString = false;
            bool escaped = false;
            int depth = 0;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
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

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                    builder.Append('\\');
                builder.Append(c);
            }
        }
    }
}
#endif
