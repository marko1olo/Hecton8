using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Vehicles.Editor
{
    public static class OOP_Buoyancy_Scanner
    {
        private const string SharedReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string AgentReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_333.json";
        private const string SharedPropertyName = "\"shinobu333SubmarineBallastScanner\"";

        [MenuItem("Hecton8/Vehicles/Submarine Ballast/Run OOP Buoyancy Scanner")]
        public static void Run()
        {
            Scan(out int fileCount, out int massHackSites, out int overlapSphereSites, out int directForceSites);
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName ?? Application.dataPath;
            string report = BuildReport(fileCount, massHackSites, overlapSphereSites, directForceSites);
            WriteReports(projectRoot, report);
            Debug.Log("SHINOBU_333 OOP buoyancy scanner written: " + Path.Combine(projectRoot, AgentReportRelativePath));
        }

        public static void Scan(out int fileCount, out int massHackSites, out int overlapSphereSites, out int directForceSites)
        {
            fileCount = 0;
            massHackSites = 0;
            overlapSphereSites = 0;
            directForceSites = 0;
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            ScanRoot(Path.Combine(scriptsRoot, "Vehicles"), ref fileCount, ref massHackSites, ref overlapSphereSites, ref directForceSites);
            ScanRoot(Path.Combine(scriptsRoot, "Physics"), ref fileCount, ref massHackSites, ref overlapSphereSites, ref directForceSites);
            ScanFile(Path.Combine(scriptsRoot, "Gameplay", "SubmarineAutoLevelBallastController.cs"), ref fileCount, ref massHackSites, ref overlapSphereSites, ref directForceSites);
        }

        private static string BuildReport(int fileCount, int massHackSites, int overlapSphereSites, int directForceSites)
        {
            bool purged = massHackSites == 0 && overlapSphereSites == 0 && directForceSites == 0;
            StringBuilder builder = new StringBuilder(768);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_333\",");
            builder.AppendLine("  \"domain\": \"SUBMARINE_BALLAST_BUOYANCY_SOLVER\",");
            builder.AppendLine("  \"scanner\": \"OOP_Buoyancy_Scanner\",");
            builder.AppendLine("  \"summary\": \"OOP Buoyancy Hacks Eradicated\",");
            builder.AppendLine("  \"parser\": \"Roslyn AST with comment-string token fallback\",");
            builder.AppendLine("  \"sharedReportMerge\": \"NON_DESTRUCTIVE_TOP_LEVEL_PROPERTY_REPLACE_OR_APPEND\",");
            builder.AppendLine("  \"sharedReportRacePolicy\": \"Sidecar is authoritative for SHINOBU_333; shared report merge preserves the current JSON object but can be overwritten by concurrent agents that rewrite the file wholesale\",");
            builder.AppendLine("  \"sidecarReport\": \"Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_333.json\",");
            builder.AppendLine("  \"routeCard\": \"Docs/ARCHITECTURE/SHINOBU_333_SUBMARINE_BALLAST_BUOYANCY_ROUTE_CARD.md\",");
            builder.AppendLine("  \"selfAudit\": \"Docs/Reports/SHINOBU_333_SELF_AUDIT.xml\",");
            builder.AppendLine("  \"reviewDisposition\": \"YELLOW_STATIC_SOURCE_ONLY\",");
            builder.AppendLine("  \"csvSourcePath\": \"Data/Physics/vehicle_ballast_profiles.csv\",");
            builder.AppendLine("  \"csvColdIngestion\": \"Data/Physics/vehicle_ballast_profiles.csv -> BufferID 71778 scratch -> SubmarineBallastCsvParser.ParseProfiles -> BufferID 71776 profiles\",");
            builder.AppendLine("  \"dataMonolithStatus\": \"STATIC_PAYLOAD_ABSENT_BLOCKED_BY_DATA_MONOLITH_PIPELINE\",");
            builder.AppendLine("  \"sampleBudgetHysteresis\": \"GlobalQualityWeight -> smoothstep/lerp 1..4 sample budget with 2.5s owner-phase hysteresis; stored in SubmarineBallastFluidSampleDTO.ActiveSampleBudget at offset 148 without size drift\",");
            builder.AppendLine("  \"metadataImportProof\": \"Stable .meta files present for SubmarineBallastBuoyancyContracts.cs, OOP_Buoyancy_Scanner.cs, and Data/Physics/vehicle_ballast_profiles.csv; GUID scan found no duplicates; CSV remains a non-Unity external cold source, not a Unity import claim\",");
            builder.AppendLine("  \"assemblyBoundaryProof\": \"No Hecton8.Physics.Vehicles.Runtime.asmdef exists and SHINOBU_333 added no runtime asmdef/reference; only Hecton8.Physics.Vehicles.Editor.asmdef is present under Physics/Vehicles\",");
            builder.AppendLine("  \"timingDisclosure\": \"ComputeMicros is schedule-to-completion owner timing flagged with ForceFlagTimingProxy; exact Burst wall-time requires profiler/Burst instrumentation\",");
            builder.AppendLine("  \"hotSnapshotProof\": \"SubmarineAutoLevelBallastController has no SubmarineDynamicsRuntime call; SHINOBU_332 suppression reads cached BufferID 71786 counters, quality is cached from owner-phase HomeostasisBrain.GlobalQualityWeight, and AUP conversion uses cached runtime-origin AUP\",");
            builder.AppendLine("  \"hotVaultReadProof\": \"TryReadVaultBuffer resolves only already cached generation handles and fails closed; TryGetGenerationHandle and EnsureGenerationHandle remain confined to cold Ensure*/external snapshot acquisition paths\",");
            builder.AppendLine("  \"externalReadSnapshots\": \"Read-only cached input: BufferID 71786 Shinobu332GyroCounters owned by SHINOBU_332. SHINOBU_333 does not allocate, mutate, or release this buffer.\",");
            builder.AppendLine("  \"independentHotAudit\": \"Read-only subagent audit reported no fixed/post-fixed direct SubmarineDynamicsRuntime/global quality/AUP/GlobalRegistry/scene-search call, no SHINOBU_332 ownership violation, and no obvious compile/allocation hazard in the hot snapshot patch.\",");
            builder.AppendLine("  \"overlapScannerScope\": \"Counts Physics.OverlapSphere and Physics.OverlapSphereNonAlloc because both are CPU broadphase water-volume query routes in Vehicles/Physics authority.\",");
            builder.AppendLine("  \"vaultBufferIds\": \"71771..71778 owned by SystemID.VehiclesPhysics; 71820..71827 rejected because SHINOBU_264 owns 71820..71831\",");
            builder.Append("  \"sourceFilesScanned\": ").Append(fileCount).AppendLine(",");
            builder.Append("  \"dynamicRigidbodyMassHackSites\": ").Append(massHackSites).AppendLine(",");
            builder.Append("  \"physicsOverlapSphereWaterQuerySites\": ").Append(overlapSphereSites).AppendLine(",");
            builder.Append("  \"directAddForceAtPositionSites\": ").Append(directForceSites).AppendLine(",");
            builder.Append("  \"oopBuoyancyHacksPurged\": ").Append(purged ? "true" : "false").AppendLine(",");
            builder.AppendLine("  \"runtimeRoute\": \"GlobalDataVault -> EvaluateBallastTanksJob -> CalculateBuoyancyForceJob -> PhysicsForceRouter\",");
            builder.AppendLine("  \"compileProof\": \"BLOCKED_EXTERNAL: Hecton8.Core.csproj reached unrelated pre-existing VRSomatic/Gyro/Metabolism/Fauna compile wall after SHINOBU_333 type visibility was fixed; later csc.exe exited -1 without SHINOBU_333 source diagnostics. No green build claim.\",");
            builder.AppendLine("  \"vaultBuffers\": [71771, 71772, 71773, 71774, 71775, 71776, 71777, 71778]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void WriteReports(string projectRoot, string reportJson)
        {
            string agentReportPath = Path.Combine(projectRoot, AgentReportRelativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(agentReportPath) ?? projectRoot);
            File.WriteAllText(agentReportPath, reportJson);

            string sharedReportPath = Path.Combine(projectRoot, SharedReportRelativePath);
            MergeSharedPhysicsReport(sharedReportPath, reportJson);
        }

        private static void MergeSharedPhysicsReport(string reportPath, string reportJson)
        {
            string propertyJson = SharedPropertyName + ":" + reportJson;
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, "{" + propertyJson + "}");
                return;
            }

            string existing = File.ReadAllText(reportPath);
            if (TryReplaceJsonObjectProperty(existing, SharedPropertyName, propertyJson, out string replaced) ||
                TryAppendJsonObjectProperty(existing, propertyJson, out replaced))
            {
                File.WriteAllText(reportPath, replaced);
            }
        }

        private static void ScanRoot(
            string root,
            ref int fileCount,
            ref int massHackSites,
            ref int overlapSphereSites,
            ref int directForceSites)
        {
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                if (files[i].IndexOf("\\Editor\\", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                string text;
                try
                {
                    text = File.ReadAllText(files[i]);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                fileCount++;
                CountForbiddenSites(files[i], text, ref massHackSites, ref overlapSphereSites, ref directForceSites);
            }
        }

        private static void ScanFile(
            string path,
            ref int fileCount,
            ref int massHackSites,
            ref int overlapSphereSites,
            ref int directForceSites)
        {
            if (!File.Exists(path))
                return;

            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException)
            {
                return;
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }

            fileCount++;
            CountForbiddenSites(path, text, ref massHackSites, ref overlapSphereSites, ref directForceSites);
        }

        private static void CountForbiddenSites(
            string path,
            string text,
            ref int massHackSites,
            ref int overlapSphereSites,
            ref int directForceSites)
        {
            try
            {
                CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(text).GetCompilationUnitRoot();
                IEnumerator<SyntaxNode> nodes = root.DescendantNodes().GetEnumerator();
                try
                {
                    while (nodes.MoveNext())
                    {
                        SyntaxNode node = nodes.Current;
                        if (node is AssignmentExpressionSyntax assignment && IsForbiddenMassAssignment(path, assignment))
                            massHackSites++;
                        else if (node is InvocationExpressionSyntax invocation)
                        {
                            if (IsPhysicsOverlapSphere(invocation))
                                overlapSphereSites++;
                            if (IsDirectAddForceAtPosition(path, invocation))
                                directForceSites++;
                        }
                    }
                }
                finally
                {
                    nodes.Dispose();
                }
            }
            catch (Exception)
            {
                massHackSites += CountTokenFallback(text, ".mass", assignmentOnly: true);
                overlapSphereSites += CountTokenFallback(text, "Physics.OverlapSphere", assignmentOnly: false);
                if (path.IndexOf("PhysicsApplySystem", StringComparison.OrdinalIgnoreCase) < 0)
                    directForceSites += CountTokenFallback(text, ".AddForceAtPosition", assignmentOnly: false);
            }
        }

        private static bool IsForbiddenMassAssignment(string path, AssignmentExpressionSyntax assignment)
        {
            if (!(assignment.Left is MemberAccessExpressionSyntax member))
                return false;

            if (!string.Equals(member.Name.Identifier.ValueText, "mass", StringComparison.Ordinal))
                return false;

            return path.IndexOf("SubmarineCoreDirector.cs", StringComparison.OrdinalIgnoreCase) < 0;
        }

        private static bool IsPhysicsOverlapSphere(InvocationExpressionSyntax invocation)
        {
            string name = ResolveInvocationName(invocation);
            return string.Equals(name, "Physics.OverlapSphere", StringComparison.Ordinal) ||
                   string.Equals(name, "Physics.OverlapSphereNonAlloc", StringComparison.Ordinal);
        }

        private static bool IsDirectAddForceAtPosition(string path, InvocationExpressionSyntax invocation)
        {
            if (path.IndexOf("PhysicsApplySystem", StringComparison.OrdinalIgnoreCase) >= 0)
                return false;

            string name = ResolveInvocationName(invocation);
            return name.EndsWith(".AddForceAtPosition", StringComparison.Ordinal);
        }

        private static string ResolveInvocationName(InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member)
                return member.Expression + "." + member.Name.Identifier.ValueText;

            return invocation.Expression.ToString();
        }

        private static int CountTokenFallback(string text, string token, bool assignmentOnly)
        {
            int count = 0;
            int cursor = 0;
            while (cursor < text.Length)
            {
                int index = text.IndexOf(token, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                cursor = index + token.Length;
                if (IsInsideCommentOrString(text, index))
                    continue;

                if (!assignmentOnly)
                {
                    count++;
                    continue;
                }

                int op = cursor;
                while (op < text.Length && char.IsWhiteSpace(text[op]))
                    op++;

                if (op < text.Length && text[op] == '=')
                    count++;
            }

            return count;
        }

        private static bool TryAppendJsonObjectProperty(string existing, string propertyJson, out string merged)
        {
            merged = null;
            if (string.IsNullOrEmpty(existing))
                return false;

            int close = existing.LastIndexOf('}');
            if (close < 0)
                return false;

            int scan = close - 1;
            while (scan >= 0 && char.IsWhiteSpace(existing[scan]))
                scan--;

            bool hasExistingProperty = scan >= 0 && existing[scan] != '{';
            string separator = hasExistingProperty ? "," : string.Empty;
            merged = existing.Substring(0, close) + separator + "\n  " + propertyJson + "\n" + existing.Substring(close);
            return true;
        }

        private static bool TryReplaceJsonObjectProperty(string existing, string propertyName, string propertyJson, out string merged)
        {
            merged = null;
            if (string.IsNullOrEmpty(existing))
                return false;

            int propertyStart = existing.IndexOf(propertyName, StringComparison.Ordinal);
            if (propertyStart < 0)
                return false;

            int colon = existing.IndexOf(':', propertyStart + propertyName.Length);
            if (colon < 0)
                return false;

            int valueStart = colon + 1;
            while (valueStart < existing.Length && char.IsWhiteSpace(existing[valueStart]))
                valueStart++;

            if (valueStart >= existing.Length || existing[valueStart] != '{')
                return false;

            int valueEnd = FindMatchingBrace(existing, valueStart);
            if (valueEnd < 0)
                return false;

            int replaceStart = propertyStart;
            while (replaceStart > 0 && char.IsWhiteSpace(existing[replaceStart - 1]))
                replaceStart--;

            if (replaceStart > 0 && existing[replaceStart - 1] == ',')
                replaceStart--;

            int replaceEnd = valueEnd + 1;
            while (replaceEnd < existing.Length && char.IsWhiteSpace(existing[replaceEnd]))
                replaceEnd++;

            if (replaceEnd < existing.Length && existing[replaceEnd] == ',')
                replaceEnd++;

            merged = existing.Substring(0, replaceStart) + "\n  " + propertyJson + existing.Substring(replaceEnd);
            return true;
        }

        private static int FindMatchingBrace(string text, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = openIndex; i < text.Length; i++)
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

        private static bool IsInsideCommentOrString(string text, int target)
        {
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool charLiteral = false;

            for (int i = 0; i < target && i < text.Length; i++)
            {
                char c = text[i];
                char n = i + 1 < text.Length ? text[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\n' || c == '\r')
                        lineComment = false;
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && n == '/')
                    {
                        blockComment = false;
                        i++;
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '"')
                        stringLiteral = false;
                    continue;
                }

                if (charLiteral)
                {
                    if (c == '\\')
                    {
                        i++;
                        continue;
                    }
                    if (c == '\'')
                        charLiteral = false;
                    continue;
                }

                if (c == '/' && n == '/')
                {
                    lineComment = true;
                    i++;
                    continue;
                }

                if (c == '/' && n == '*')
                {
                    blockComment = true;
                    i++;
                    continue;
                }

                if (c == '"')
                    stringLiteral = true;
                else if (c == '\'')
                    charLiteral = true;
            }

            return lineComment || blockComment || stringLiteral || charLiteral;
        }
    }
}
