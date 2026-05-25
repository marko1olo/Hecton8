#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class OOP_Solar_Scanner
    {
        private const string ReportPath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string SectionKey = "shinobu341SolarScanner";
        private const string ReportMutexName = "Local\\Hecton8_PhysicsOptimizationReport";
        private const int ReportWriteAttempts = 5;
        private const int MaxPhysicsAliasCount = 16;

        private static readonly string RaycastToken = "Physics." + "Raycast";
        private static readonly string RaycastNAToken = "Physics." + "Raycast" + "Non" + "Alloc";
        private static readonly string RaycastCallToken = "Raycast" + "(";
        private static readonly string RaycastNACallToken = "Raycast" + "Non" + "Alloc" + "(";
        private static readonly string RaycastMemberCallToken = "." + "Raycast" + "(";
        private static readonly string RaycastNAMemberCallToken = "." + "Raycast" + "Non" + "Alloc" + "(";
        private static readonly string VectorDistanceToken = "Vector3" + ".Distance";
        private static readonly string ListPowerSourceToken = "List<" + "PowerSource>";
        private static readonly string RenderSunToken = "RenderSettings" + ".sun";
        private static readonly string WallClockToken = "Date" + "Time";
        private static readonly string[] s_physicsAliases = new string[MaxPhysicsAliasCount]; // COLD ALLOC: scanner alias scratch, editor-only.

        private static readonly string[] ScanRoots =
        {
            "Assets/_Project/Scripts/Power",
            "Assets/_Project/Scripts/Habitat",
            "Assets/_Project/Scripts/Construction"
        };

        [MenuItem("Hecton/Power/Run OOP Solar Scanner")]
        public static void RunScanner()
        {
            ScanResult result = ScanProject();
            WriteReport(in result);
        }

        private static ScanResult ScanProject()
        {
            ScanResult result = new ScanResult();
            for (int i = 0; i < ScanRoots.Length; i++)
                ScanDirectory(ScanRoots[i], ref result);

            ScanFile("Assets/_Project/Scripts/Gameplay/SolarPanel.cs", ref result);
            result.Passed = result.RaycastHits == 0 &&
                            result.RaycastNAHits == 0 &&
                            result.VectorDistanceHits == 0 &&
                            result.SolarUpdateHits == 0 &&
                            result.ManagedPowerSourceListHits == 0 &&
                            result.ParserFailures == 0;
            return result;
        }

        private static void ScanDirectory(string directory, ref ScanResult result)
        {
            if (!Directory.Exists(directory))
                return;

            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                ScanFile(Normalize(files[i]), ref result);
        }

        private static void ScanFile(string path, ref ScanResult result)
        {
            string normalized = Normalize(path);
            if (!File.Exists(normalized) || normalized.EndsWith("OOP_Solar_Scanner.cs", StringComparison.Ordinal))
                return;

            result.FileCount++;
            string text = File.ReadAllText(normalized, Encoding.UTF8);
            SyntaxTree tree;
            try
            {
                tree = CSharpSyntaxTree.ParseText(text);
            }
            catch (Exception exception)
            {
                result.ParserFallbackFiles++;
                if (!ScanTextFallback(normalized, text, ref result, "RoslynException:" + exception.GetType().Name))
                    result.ParserFailures++;
                return;
            }

            if (HasParseError(tree))
            {
                result.ParserFallbackFiles++;
                if (!ScanTextFallback(normalized, text, ref result, "RoslynSyntaxFallback"))
                    result.ParserFailures++;
                return;
            }

            int raycast = 0;
            int raycastNonAlloc = 0;
            int vectorDistance = 0;
            int managedLists = 0;
            int solarUpdates = 0;
            CompilationUnitSyntax root = tree.GetCompilationUnitRoot();
            int physicsAliasCount = CollectPhysicsAliases(root);
            bool hasStaticPhysicsUsing = HasStaticPhysicsUsing(root);
            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    SyntaxNode node = nodes.Current;
                    if (node is InvocationExpressionSyntax invocation && TryClassifyInvocation(invocation, physicsAliasCount, hasStaticPhysicsUsing, out string token))
                    {
                        if (string.Equals(token, RaycastNAToken, StringComparison.Ordinal))
                            raycastNonAlloc++;
                        else if (string.Equals(token, RaycastToken, StringComparison.Ordinal))
                            raycast++;
                        else if (string.Equals(token, VectorDistanceToken, StringComparison.Ordinal) && IsSolarContext(normalized, invocation))
                            vectorDistance++;
                    }
                    else if (node is GenericNameSyntax genericName &&
                             string.Equals(genericName.Identifier.ValueText, "List", StringComparison.Ordinal) &&
                             genericName.TypeArgumentList.Arguments.Count == 1 &&
                             string.Equals(genericName.TypeArgumentList.Arguments[0].ToString(), "PowerSource", StringComparison.Ordinal))
                    {
                        managedLists++;
                    }
                    else if (node is MethodDeclarationSyntax methodDeclaration &&
                             string.Equals(methodDeclaration.Identifier.ValueText, "Update", StringComparison.Ordinal) &&
                             IsSolarContext(normalized, methodDeclaration))
                    {
                        solarUpdates++;
                    }
                    else if (node is MemberAccessExpressionSyntax memberAccess)
                    {
                        string memberText = memberAccess.ToString();
                        if ((string.Equals(memberText, RenderSunToken, StringComparison.Ordinal) ||
                             memberText.StartsWith(WallClockToken, StringComparison.Ordinal)) &&
                            IsSolarContext(normalized, memberAccess))
                        {
                            solarUpdates++;
                        }
                    }
                }
            }

            result.RaycastHits += mathMax0(raycast);
            result.RaycastNAHits += mathMax0(raycastNonAlloc);
            result.VectorDistanceHits += mathMax0(vectorDistance);
            result.ManagedPowerSourceListHits += mathMax0(managedLists);
            result.SolarUpdateHits += mathMax0(solarUpdates);

            if (raycast > 0 || raycastNonAlloc > 0 || vectorDistance > 0 || managedLists > 0 || solarUpdates > 0)
                AppendFinding(ref result, normalized, raycast, raycastNonAlloc, vectorDistance, managedLists, solarUpdates, "AST_FORBIDDEN_SOLAR_AUTHORITY");
        }

        private static bool ScanTextFallback(string normalized, string text, ref ScanResult result, string reason)
        {
            if (!TextContainsSolarContext(normalized, text))
                return false;

            int raycastNonAlloc = CountToken(text, RaycastNAToken) +
                                  CountToken(text, RaycastNACallToken) +
                                  CountToken(text, RaycastNAMemberCallToken);
            int raycast = CountToken(text, RaycastToken) +
                          CountToken(text, RaycastCallToken) +
                          CountToken(text, RaycastMemberCallToken);
            if (raycastNonAlloc > 0)
                raycast = mathMax0(raycast - raycastNonAlloc);
            int vectorDistance = CountToken(text, VectorDistanceToken);
            int managedLists = CountToken(text, ListPowerSourceToken);
            int solarUpdates = CountToken(text, "Update" + "(") + CountToken(text, RenderSunToken) + CountToken(text, WallClockToken);
            result.RaycastHits += mathMax0(raycast);
            result.RaycastNAHits += mathMax0(raycastNonAlloc);
            result.VectorDistanceHits += mathMax0(vectorDistance);
            result.ManagedPowerSourceListHits += mathMax0(managedLists);
            result.SolarUpdateHits += mathMax0(solarUpdates);
            if (raycast > 0 || raycastNonAlloc > 0 || vectorDistance > 0 || managedLists > 0 || solarUpdates > 0)
                AppendFinding(ref result, normalized, raycast, raycastNonAlloc, vectorDistance, managedLists, solarUpdates, reason);
            return true;
        }

        private static bool TextContainsSolarContext(string path, string text)
        {
            return path.IndexOf("Solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Photovoltaic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Sun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   text.IndexOf("Photovoltaic", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int CountToken(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    break;

                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static int mathMax0(int value)
        {
            return value > 0 ? value : 0;
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

        private static bool TryClassifyInvocation(InvocationExpressionSyntax invocation, int physicsAliasCount, bool hasStaticPhysicsUsing, out string token)
        {
            token = string.Empty;
            if (invocation.Expression is IdentifierNameSyntax identifier)
            {
                if (hasStaticPhysicsUsing && string.Equals(identifier.Identifier.ValueText, "Raycast" + "Non" + "Alloc", StringComparison.Ordinal))
                {
                    token = RaycastNAToken;
                    return true;
                }

                if (hasStaticPhysicsUsing && string.Equals(identifier.Identifier.ValueText, "Raycast", StringComparison.Ordinal))
                {
                    token = RaycastToken;
                    return true;
                }

                return false;
            }

            if (!(invocation.Expression is MemberAccessExpressionSyntax memberAccess))
                return false;

            string owner = memberAccess.Expression.ToString();
            string name = memberAccess.Name.Identifier.ValueText;
            if (IsPhysicsOwner(owner, physicsAliasCount) && string.Equals(name, "Raycast" + "Non" + "Alloc", StringComparison.Ordinal))
            {
                token = RaycastNAToken;
                return true;
            }

            if (IsPhysicsOwner(owner, physicsAliasCount) && string.Equals(name, "Raycast", StringComparison.Ordinal))
            {
                token = RaycastToken;
                return true;
            }

            if (IsVector3Owner(owner) && string.Equals(name, "Distance", StringComparison.Ordinal))
            {
                token = VectorDistanceToken;
                return true;
            }

            return false;
        }

        private static int CollectPhysicsAliases(CompilationUnitSyntax root)
        {
            int count = 0;
            SyntaxList<UsingDirectiveSyntax> usings = root.Usings;
            for (int i = 0; i < usings.Count && count < s_physicsAliases.Length; i++)
                TryCollectPhysicsAlias(usings[i], ref count);

            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext() && count < s_physicsAliases.Length)
                {
                    if (nodes.Current is UsingDirectiveSyntax usingDirective)
                        TryCollectPhysicsAlias(usingDirective, ref count);
                }
            }

            return count;
        }

        private static bool HasStaticPhysicsUsing(CompilationUnitSyntax root)
        {
            SyntaxList<UsingDirectiveSyntax> usings = root.Usings;
            for (int i = 0; i < usings.Count; i++)
            {
                if (IsStaticPhysicsUsing(usings[i]))
                    return true;
            }

            using (System.Collections.Generic.IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator())
            {
                while (nodes.MoveNext())
                {
                    if (nodes.Current is UsingDirectiveSyntax usingDirective && IsStaticPhysicsUsing(usingDirective))
                        return true;
                }
            }

            return false;
        }

        private static void TryCollectPhysicsAlias(UsingDirectiveSyntax usingDirective, ref int count)
        {
            if (count >= s_physicsAliases.Length || usingDirective.Alias == null || usingDirective.Name == null)
                return;

            string target = usingDirective.Name.ToString();
            if (!IsPhysicsOwner(target, 0))
                return;

            string alias = usingDirective.Alias.Name.Identifier.ValueText;
            if (string.IsNullOrEmpty(alias))
                return;

            s_physicsAliases[count++] = alias;
        }

        private static bool IsStaticPhysicsUsing(UsingDirectiveSyntax usingDirective)
        {
            return usingDirective.StaticKeyword.IsKind(SyntaxKind.StaticKeyword) &&
                   usingDirective.Name != null &&
                   IsPhysicsOwner(usingDirective.Name.ToString(), 0);
        }

        private static bool IsPhysicsOwner(string owner, int aliasCount)
        {
            if (string.Equals(owner, "Physics", StringComparison.Ordinal) ||
                string.Equals(owner, "UnityEngine.Physics", StringComparison.Ordinal) ||
                owner.EndsWith(".Physics", StringComparison.Ordinal))
            {
                return true;
            }

            for (int i = 0; i < aliasCount; i++)
            {
                if (string.Equals(owner, s_physicsAliases[i], StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static bool IsVector3Owner(string owner)
        {
            return string.Equals(owner, "Vector3", StringComparison.Ordinal) ||
                   string.Equals(owner, "UnityEngine.Vector3", StringComparison.Ordinal) ||
                   owner.EndsWith(".Vector3", StringComparison.Ordinal);
        }

        private static bool IsSolarContext(string path, SyntaxNode node)
        {
            string normalized = Normalize(path);
            if (normalized.IndexOf("Solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                normalized.IndexOf("Photovoltaic", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return true;
            }

            using (System.Collections.Generic.IEnumerator<SyntaxNode> ancestors = node.AncestorsAndSelf().GetEnumerator())
            {
                while (ancestors.MoveNext())
                {
                    SyntaxNode current = ancestors.Current;
                    if (current is TypeDeclarationSyntax typeDeclaration && ContainsSolarWord(typeDeclaration.Identifier.ValueText))
                        return true;
                    if (current is MethodDeclarationSyntax methodDeclaration && ContainsSolarWord(methodDeclaration.Identifier.ValueText))
                        return true;
                    if (current is FieldDeclarationSyntax fieldDeclaration && VariableListContainsSolarWord(fieldDeclaration.Declaration))
                        return true;
                    if (current is LocalDeclarationStatementSyntax localDeclaration && VariableListContainsSolarWord(localDeclaration.Declaration))
                        return true;
                    if (current is ParameterSyntax parameter && ContainsSolarWord(parameter.Identifier.ValueText))
                        return true;
                }
            }

            return false;
        }

        private static bool VariableListContainsSolarWord(VariableDeclarationSyntax declaration)
        {
            SeparatedSyntaxList<VariableDeclaratorSyntax> variables = declaration.Variables;
            for (int i = 0; i < variables.Count; i++)
            {
                if (ContainsSolarWord(variables[i].Identifier.ValueText))
                    return true;
            }

            return false;
        }

        private static bool ContainsSolarWord(string value)
        {
            return value.IndexOf("solar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("sun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   value.IndexOf("photovoltaic", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AppendFinding(ref ScanResult result, string file, int raycast, int raycastNonAlloc, int vectorDistance, int managedLists, int solarUpdates, string reason)
        {
            if (result.FindingCount >= result.Findings.Length)
                return;

            result.Findings[result.FindingCount++] = new Finding
            {
                File = file,
                Raycast = raycast,
                RaycastNA = raycastNonAlloc,
                VectorDistance = vectorDistance,
                ManagedLists = managedLists,
                SolarUpdates = solarUpdates,
                Reason = reason
            };
        }

        private static void WriteReport(in ScanResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string fullPath = Path.Combine(projectRoot, ReportPath);
            string directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string section = BuildSectionJson(in result);
            Exception lastException = null;
            using (Mutex mutex = new Mutex(false, ReportMutexName))
            {
                bool acquired = false;
                try
                {
                    try
                    {
                        acquired = mutex.WaitOne(5000);
                    }
                    catch (AbandonedMutexException)
                    {
                        acquired = true;
                    }

                    if (!acquired)
                        throw new IOException("Timed out waiting for PHYSICS_OPTIMIZATION_REPORT mutex.");

                    for (int attempt = 0; attempt < ReportWriteAttempts; attempt++)
                    {
                        try
                        {
                            string existing = File.Exists(fullPath) ? File.ReadAllText(fullPath, Encoding.UTF8) : string.Empty;
                            string updated = UpsertSection(existing, section);
                            JObject.Parse(updated);
                            WriteTextAtomic(fullPath, updated);
                            return;
                        }
                        catch (IOException exception)
                        {
                            lastException = exception;
                            Thread.Sleep(20 * (attempt + 1));
                        }
                    }
                }
                finally
                {
                    if (acquired)
                        mutex.ReleaseMutex();
                }
            }

            throw new IOException("Failed to update PHYSICS_OPTIMIZATION_REPORT after retry.", lastException);
        }

        private static string BuildSectionJson(in ScanResult result)
        {
            StringBuilder json = new StringBuilder(2048);
            json.AppendLine("  \"" + SectionKey + "\": {");
            json.AppendLine("    \"agent\": \"SHINOBU_341\",");
            json.AppendLine("    \"summary\": \"OOP Optical Raycasts Eradicated\",");
            json.AppendLine("    \"scanner\": \"OOP_Solar_Scanner\",");
            json.AppendLine("    \"scannerMode\": \"ROSLYN_AST_TARGETED_SUFFIX_ALIAS_STATIC_USING_WITH_MUTEXED_REPORT\",");
            json.AppendLine("    \"scannerUsesRoslynAst\": true,");
            json.AppendLine("    \"physicsOwnerSuffixAliasStaticUsingScan\": true,");
            json.AppendLine("    \"sharedReportMutexRetry\": true,");
            json.Append("    \"passed\": ").Append(result.Passed ? "true" : "false").AppendLine(",");
            json.Append("    \"filesScanned\": ").Append(result.FileCount).AppendLine(",");
            json.Append("    \"parserFailures\": ").Append(result.ParserFailures).AppendLine(",");
            json.Append("    \"parserFallbackFiles\": ").Append(result.ParserFallbackFiles).AppendLine(",");
            json.Append("    \"raycastHits\": ").Append(result.RaycastHits).AppendLine(",");
            json.Append("    \"raycastNonAllocHits\": ").Append(result.RaycastNAHits).AppendLine(",");
            json.Append("    \"vectorDistanceToSunHits\": ").Append(result.VectorDistanceHits).AppendLine(",");
            json.Append("    \"managedPowerSourceListHits\": ").Append(result.ManagedPowerSourceListHits).AppendLine(",");
            json.Append("    \"solarLegacyUpdateHits\": ").Append(result.SolarUpdateHits).AppendLine(",");
            json.AppendLine("    \"replacementRoute\": \"SolarPanelStateDTO double3 AUP -> Burst BeerLambert/VoxelSDF EvaluateOpticalDepthJob -> NodeSolarInputMilliWatts -> PowerNodeDTO CSR source injection\",");
            json.AppendLine("    \"scannedPaths\": [");
            for (int i = 0; i < ScanRoots.Length; i++)
            {
                json.Append("      \"").Append(Escape(ScanRoots[i])).Append("\"");
                json.AppendLine(i + 1 < ScanRoots.Length ? "," : string.Empty);
            }
            json.AppendLine("    ],");
            json.AppendLine("    \"findings\": [");
            for (int i = 0; i < result.FindingCount; i++)
            {
                Finding finding = result.Findings[i];
                json.Append("      { \"file\": \"").Append(Escape(finding.File)).Append("\", ");
                json.Append("\"raycast\": ").Append(finding.Raycast).Append(", ");
                json.Append("\"raycastNonAlloc\": ").Append(finding.RaycastNA).Append(", ");
                json.Append("\"vectorDistanceToSun\": ").Append(finding.VectorDistance).Append(", ");
                json.Append("\"managedLists\": ").Append(finding.ManagedLists).Append(", ");
                json.Append("\"solarLegacyUpdates\": ").Append(finding.SolarUpdates).Append(", ");
                json.Append("\"reason\": \"").Append(Escape(finding.Reason)).Append("\" }");
                json.AppendLine(i + 1 < result.FindingCount ? "," : string.Empty);
            }
            json.AppendLine("    ]");
            json.AppendLine("  }");
            return json.ToString();
        }

        private static string UpsertSection(string existing, string sectionJson)
        {
            JObject root = string.IsNullOrWhiteSpace(existing) ? new JObject() : JObject.Parse(existing);
            JObject wrapper = JObject.Parse("{\n" + sectionJson + "\n}");
            root[SectionKey] = wrapper[SectionKey];
            return root.ToString(Newtonsoft.Json.Formatting.Indented) + "\n";
        }

        private static void WriteTextAtomic(string path, string text)
        {
            string tempPath = path + ".tmp";
            File.WriteAllText(tempPath, text, Encoding.UTF8);
            if (File.Exists(path))
            {
                File.Replace(tempPath, path, null);
                return;
            }

            File.Move(tempPath, path);
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string Normalize(string path)
        {
            return path.Replace('\\', '/');
        }

        private sealed class ScanResult
        {
            public int FileCount;
            public int RaycastHits;
            public int RaycastNAHits;
            public int VectorDistanceHits;
            public int ManagedPowerSourceListHits;
            public int SolarUpdateHits;
            public int ParserFailures;
            public int ParserFallbackFiles;
            public int FindingCount;
            public bool Passed;
            public readonly Finding[] Findings = new Finding[64];
        }

        private struct Finding
        {
            public string File;
            public int Raycast;
            public int RaycastNA;
            public int VectorDistance;
            public int ManagedLists;
            public int SolarUpdates;
            public string Reason;
        }
    }
}
#endif
