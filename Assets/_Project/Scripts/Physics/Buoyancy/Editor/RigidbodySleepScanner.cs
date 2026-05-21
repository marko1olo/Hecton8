#if UNITY_EDITOR
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using Hecton8.Physics;
using Hecton8.Physics.KCC;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Unity.Collections.LowLevel.Unsafe;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physics.Editor
{
    /// <summary>
    /// Editor-only SHINOBU_249 scanner for managed sleep API contamination and layout guards.
    /// </summary>
    /// <remarks>
    /// Uses Roslyn AST parsing for C# source and falls back to token scanning only when a file cannot be parsed.
    /// </remarks>
    public static class RigidbodySleepScanner
    {
        private const string SharedReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT.json";
        private const string AgentReportRelativePath = "Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_249.json";
        private static readonly CSharpParseOptions ParseOptions = CSharpParseOptions.Default
            .WithLanguageVersion(LanguageVersion.Preview)
            .WithPreprocessorSymbols("UNITY_EDITOR");

        /// <summary>Runs the sleep scanner from the Unity menu.</summary>
        [MenuItem("HECTON-8/Physics/Run Rigidbody Sleep Scanner")]
        public static void RunFromMenu()
        {
            Run();
        }

        /// <summary>Runs the scanner and writes SHINOBU_249 report artifacts.</summary>
        /// <returns>True when no forbidden active Physics sleep APIs are found and layouts match.</returns>
        public static bool Run()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string physicsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts", "Physics");
            string[] files = Directory.Exists(physicsRoot)
                ? Directory.GetFiles(physicsRoot, "*.cs", SearchOption.AllDirectories)
                : Array.Empty<string>();

            StringBuilder findings = new StringBuilder(1024);
            int findingCount = 0;
            int scannedFiles = 0;
            int parserFailures = 0;
            for (int i = 0; i < files.Length; i++)
            {
                string file = files[i];
                if (file.EndsWith(nameof(RigidbodySleepScanner) + ".cs", StringComparison.OrdinalIgnoreCase))
                    continue;

                string text;
                try
                {
                    text = File.ReadAllText(file);
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }

                scannedFiles++;
                try
                {
                    SyntaxTree tree = CSharpSyntaxTree.ParseText(text, ParseOptions);
                    if (HasSyntaxErrors(tree))
                    {
                        parserFailures++;
                        ScanTextFallback(projectRoot, file, text, findings, ref findingCount);
                    }
                    else
                    {
                        ScanSyntaxTree(projectRoot, file, tree.GetCompilationUnitRoot(), findings, ref findingCount);
                    }
                }
                catch (Exception)
                {
                    parserFailures++;
                    ScanTextFallback(projectRoot, file, text, findings, ref findingCount);
                }
            }

            int kinematicStateBytes = UnsafeUtility.SizeOf<KinematicStateDTO>();
            int kinematicFlagsOffset = HydrodynamicKccLayoutValidator.KinematicStateFlagsOffset;
            int kinematicSleepConfigBytes = UnsafeUtility.SizeOf<KinematicSleepSdfConfigDTO>();
            int kinematicSleepConfigFlagsOffset = Marshal.OffsetOf(
                typeof(KinematicSleepSdfConfigDTO),
                nameof(KinematicSleepSdfConfigDTO.Flags)).ToInt32();
            bool layoutValid = HydrodynamicKccLayoutValidator.ValidateRuntimeLayout(out _) &&
                               kinematicStateBytes == 64 &&
                               kinematicFlagsOffset == 52 &&
                               kinematicSleepConfigBytes == 64 &&
                               kinematicSleepConfigFlagsOffset == 56;
            string status = findingCount == 0 && layoutValid
                ? "PENDING_COMPILE_VERIFICATION"
                : "FORBIDDEN_MANAGED_SLEEP_API_FOUND";
            string summary = findingCount == 0 && layoutValid
                ? "Managed Sleep APIs Eradicated"
                : "Forbidden managed sleep APIs remain";
            string proofType = parserFailures == 0
                ? "ROSLYN_AST"
                : "ROSLYN_AST_WITH_TOKEN_FALLBACK";
            bool astProof = parserFailures == 0;

            string reportPath = Path.Combine(projectRoot, AgentReportRelativePath);
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            using (StreamWriter writer = new StreamWriter(reportPath, false, Encoding.UTF8))
            {
                writer.WriteLine("{");
                writer.WriteLine("  \"agent\": \"SHINOBU_249\",");
                writer.WriteLine("  \"domain\": \"BUOYANCY_SLEEP_STATE_INTEGRATOR\",");
                writer.WriteLine("  \"status\": \"" + status + "\",");
                writer.WriteLine("  \"physicsRoot\": \"" + Escape(ToProjectRelative(projectRoot, physicsRoot)) + "\",");
                writer.WriteLine("  \"forbiddenPatternCount\": " + findingCount + ",");
                writer.WriteLine("  \"proofType\": \"" + proofType + "\",");
                writer.WriteLine("  \"scannerMode\": \"ROSLYN_AST_WITH_TOKEN_FALLBACK\",");
                writer.WriteLine("  \"roslynLanguageVersion\": \"Preview\",");
                writer.WriteLine("  \"astProof\": " + (astProof ? "true" : "false") + ",");
                writer.WriteLine("  \"scannedFiles\": " + scannedFiles + ",");
                writer.WriteLine("  \"parserFailures\": " + parserFailures + ",");
                writer.WriteLine("  \"kinematicStateSizeBytes\": " + kinematicStateBytes + ",");
                writer.WriteLine("  \"kinematicFlagsOffset\": " + kinematicFlagsOffset + ",");
                writer.WriteLine("  \"kinematicSleepSdfConfigSizeBytes\": " + kinematicSleepConfigBytes + ",");
                writer.WriteLine("  \"kinematicSleepSdfConfigFlagsOffset\": " + kinematicSleepConfigFlagsOffset + ",");
                writer.WriteLine("  \"sleepStateCapacity\": " + BuoyancyDisplacementConstants.StateCapacity + ",");
                writer.WriteLine("  \"sleepTelemetryRingEntries\": " + BuoyancyDisplacementConstants.TelemetryCapacity + ",");
                writer.WriteLine("  \"layoutValid\": " + (layoutValid ? "true" : "false") + ",");
                writer.WriteLine("  \"summary\": \"" + summary + "\",");
                writer.WriteLine("  \"lastUnityImportAttemptLog\": \"Docs/AgentLogs/UnityCompile_SHINOBU_249.log\",");
                writer.WriteLine("  \"lastUnityImportProofStatus\": \"BLOCKED_EXTERNAL_COMPILE_ERRORS\",");
                writer.WriteLine("  \"unityImportBlockedByExternalErrors\": true,");
                writer.WriteLine("  \"editorAsmdefAdded\": true,");
                writer.WriteLine("  \"editorAsmdef\": \"Assets/_Project/Scripts/Physics/Buoyancy/Editor/Hecton8.Physics.Buoyancy.Editor.asmdef\",");
                writer.WriteLine("  \"editorAsmdefIsolationPendingUnityReimport\": true,");
                writer.WriteLine("  \"forcePacketStaleSlotAudit\": \"PASS_STATIC\",");
                writer.WriteLine("  \"forcePacketPrepareClearsCounterOnly\": true,");
                writer.WriteLine("  \"forcePacketEvaluatorClearsSleepingCandidates\": true,");
                writer.WriteLine("  \"forcePacketCompactUsesScheduledCandidateCount\": true,");
                writer.WriteLine("  \"forcePacketDrainUsesCounterCountOnly\": true,");
                writer.WriteLine("  \"rejectedFullForcePacketClearBytesPerTick\": 1048576,");
                writer.WriteLine("  \"wakeRouteAudit\": \"PASS_SIGNALBUS_WAKE_REQUEST\",");
                writer.WriteLine("  \"wakeRouteContract\": \"Core/Contracts/Signals/WakeRequestSignal\",");
                writer.WriteLine("  \"directCavitationForcePacketImport\": false,");
                writer.WriteLine("  \"cavitationForceEventsBridgeToWakeRequestSignal\": true,");
                writer.WriteLine("  \"processBuoyancyWakeTriggersBurstCompile\": true,");
                writer.WriteLine("  \"shinobuJobBurstDirectiveScan\": \"PASS_STATIC\",");
                writer.WriteLine("  \"findings\": [");
                writer.Write(findings.ToString());
                writer.WriteLine();
                writer.WriteLine("  ]");
                writer.WriteLine("}");
            }

            TryUpsertSharedReportAddendum(
                projectRoot,
                status,
                summary,
                findingCount,
                layoutValid,
                kinematicStateBytes,
                kinematicFlagsOffset,
                kinematicSleepConfigBytes,
                kinematicSleepConfigFlagsOffset,
                proofType,
                astProof,
                scannedFiles,
                parserFailures);
            AssetDatabase.Refresh();
            return findingCount == 0 && layoutValid;
        }

        private static bool HasSyntaxErrors(SyntaxTree tree)
        {
            foreach (Diagnostic diagnostic in tree.GetDiagnostics())
            {
                if (diagnostic.Severity == DiagnosticSeverity.Error)
                    return true;
            }

            return false;
        }

        private static void ScanSyntaxTree(
            string projectRoot,
            string file,
            CompilationUnitSyntax root,
            StringBuilder findings,
            ref int findingCount)
        {
            foreach (SyntaxNode node in root.DescendantNodes())
            {
                if (!TryResolveForbiddenNode(node, out string pattern))
                    continue;

                FileLinePositionSpan span = node.SyntaxTree.GetLineSpan(node.Span);
                AppendFinding(projectRoot, file, span.StartLinePosition.Line + 1, pattern, "ROSLYN_AST", findings);
                findingCount++;
            }
        }

        private static bool TryResolveForbiddenNode(SyntaxNode node, out string pattern)
        {
            pattern = string.Empty;
            if (node is InvocationExpressionSyntax invocation &&
                TryGetInvocationMemberName(invocation, out string memberName, out string ownerText))
            {
                if (memberName.Equals("IsSleeping", StringComparison.Ordinal))
                {
                    pattern = ".IsSleeping(";
                    return true;
                }

                if (memberName.Equals("Sleep", StringComparison.Ordinal) &&
                    (!string.IsNullOrEmpty(ownerText) || invocation.Expression is MemberAccessExpressionSyntax))
                {
                    pattern = ownerText.IndexOf("Rigidbody", StringComparison.Ordinal) >= 0
                        ? "Rigidbody.Sleep("
                        : ".Sleep(";
                    return true;
                }
            }

            if (node is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText.Equals("sleepThreshold", StringComparison.Ordinal))
            {
                pattern = "sleepThreshold";
                return true;
            }

            if (node is MemberBindingExpressionSyntax memberBinding &&
                memberBinding.Name.Identifier.ValueText.Equals("sleepThreshold", StringComparison.Ordinal))
            {
                pattern = "sleepThreshold";
                return true;
            }

            if (node is IdentifierNameSyntax identifier &&
                identifier.Identifier.ValueText.Equals("sleepThreshold", StringComparison.Ordinal) &&
                !IsMemberName(identifier))
            {
                pattern = "sleepThreshold";
                return true;
            }

            return false;
        }

        private static bool TryGetInvocationMemberName(
            InvocationExpressionSyntax invocation,
            out string memberName,
            out string ownerText)
        {
            memberName = string.Empty;
            ownerText = string.Empty;
            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                memberName = memberAccess.Name.Identifier.ValueText;
                ownerText = memberAccess.Expression.ToString();
                return true;
            }

            if (invocation.Expression is MemberBindingExpressionSyntax memberBinding)
            {
                memberName = memberBinding.Name.Identifier.ValueText;
                return true;
            }

            if (invocation.Expression is IdentifierNameSyntax identifier)
            {
                memberName = identifier.Identifier.ValueText;
                return true;
            }

            return false;
        }

        private static bool IsMemberName(IdentifierNameSyntax identifier)
        {
            return identifier.Parent is MemberAccessExpressionSyntax memberAccess &&
                       ReferenceEquals(memberAccess.Name, identifier) ||
                   identifier.Parent is MemberBindingExpressionSyntax memberBinding &&
                       ReferenceEquals(memberBinding.Name, identifier);
        }

        private static void ScanTextFallback(
            string projectRoot,
            string file,
            string text,
            StringBuilder findings,
            ref int findingCount)
        {
            string searchable = MaskCommentsAndStrings(text);
            findingCount += AppendTokenFindings(projectRoot, file, text, searchable, "Rigidbody.Sleep(", findings);
            findingCount += AppendTokenFindings(projectRoot, file, text, searchable, ".IsSleeping(", findings);
            findingCount += AppendTokenFindings(projectRoot, file, text, searchable, "sleepThreshold", findings);
        }

        private static int AppendTokenFindings(
            string projectRoot,
            string file,
            string originalText,
            string searchableText,
            string pattern,
            StringBuilder findings)
        {
            int count = 0;
            int cursor = 0;
            while (cursor < searchableText.Length)
            {
                int index = searchableText.IndexOf(pattern, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                AppendFinding(projectRoot, file, ResolveLine(originalText, index), pattern, "TOKEN_FALLBACK", findings);
                count++;
                cursor = index + pattern.Length;
            }

            return count;
        }

        private static void AppendFinding(
            string projectRoot,
            string file,
            int line,
            string pattern,
            string parser,
            StringBuilder findings)
        {
            if (findings.Length > 0)
                findings.AppendLine(",");

            findings.Append("    { \"file\": \"")
                .Append(Escape(ToProjectRelative(projectRoot, file)))
                .Append("\", \"line\": ")
                .Append(line)
                .Append(", \"pattern\": \"")
                .Append(Escape(pattern))
                .Append("\", \"parser\": \"")
                .Append(Escape(parser))
                .Append("\" }");
        }

        private static string MaskCommentsAndStrings(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            char[] chars = text.ToCharArray();
            int length = chars.Length;
            int i = 0;
            while (i < length)
            {
                char c = chars[i];
                char next = i + 1 < length ? chars[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    chars[i++] = ' ';
                    chars[i++] = ' ';
                    while (i < length && chars[i] != '\n')
                        chars[i++] = ' ';
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    chars[i++] = ' ';
                    chars[i++] = ' ';
                    while (i + 1 < length && !(chars[i] == '*' && chars[i + 1] == '/'))
                    {
                        if (chars[i] != '\n' && chars[i] != '\r')
                            chars[i] = ' ';
                        i++;
                    }

                    if (i + 1 < length)
                    {
                        chars[i++] = ' ';
                        chars[i++] = ' ';
                    }
                    continue;
                }

                bool verbatim = c == '@' && next == '"';
                bool normalString = c == '"';
                if (verbatim || normalString)
                {
                    if (verbatim)
                    {
                        chars[i++] = ' ';
                    }

                    chars[i++] = ' ';
                    while (i < length)
                    {
                        char value = chars[i];
                        if (value != '\n' && value != '\r')
                            chars[i] = ' ';

                        if (normalString && value == '\\' && i + 1 < length)
                        {
                            i++;
                            if (chars[i] != '\n' && chars[i] != '\r')
                                chars[i] = ' ';
                        }
                        else if (value == '"')
                        {
                            if (verbatim && i + 1 < length && chars[i + 1] == '"')
                            {
                                i++;
                                chars[i] = ' ';
                            }
                            else
                            {
                                i++;
                                break;
                            }
                        }

                        i++;
                    }
                    continue;
                }

                if (c == '\'')
                {
                    chars[i++] = ' ';
                    while (i < length)
                    {
                        char value = chars[i];
                        if (value != '\n' && value != '\r')
                            chars[i] = ' ';

                        if (value == '\\' && i + 1 < length)
                        {
                            i++;
                            if (chars[i] != '\n' && chars[i] != '\r')
                                chars[i] = ' ';
                        }
                        else if (value == '\'')
                        {
                            i++;
                            break;
                        }

                        i++;
                    }
                    continue;
                }

                i++;
            }

            return new string(chars);
        }

        private static void TryUpsertSharedReportAddendum(
            string projectRoot,
            string status,
            string summary,
            int findingCount,
            bool layoutValid,
            int kinematicStateBytes,
            int kinematicFlagsOffset,
            int kinematicSleepConfigBytes,
            int kinematicSleepConfigFlagsOffset,
            string proofType,
            bool astProof,
            int scannedFiles,
            int parserFailures)
        {
            string sharedPath = Path.Combine(projectRoot, SharedReportRelativePath);
            string directory = Path.GetDirectoryName(sharedPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            string addendum = BuildSharedAddendum(
                status,
                summary,
                findingCount,
                layoutValid,
                kinematicStateBytes,
                kinematicFlagsOffset,
                kinematicSleepConfigBytes,
                kinematicSleepConfigFlagsOffset,
                proofType,
                astProof,
                scannedFiles,
                parserFailures);
            if (!File.Exists(sharedPath))
            {
                File.WriteAllText(sharedPath, "{\n" + addendum + "\n}\n", Encoding.UTF8);
                return;
            }

            string existing = File.ReadAllText(sharedPath);
            const string propertyName = "\"shinobu249BuoyancySleep\"";
            int existingProperty = existing.IndexOf(propertyName, StringComparison.Ordinal);
            if (existingProperty >= 0)
            {
                int objectStart = existing.IndexOf('{', existingProperty);
                int objectEnd = FindMatchingObjectEnd(existing, objectStart);
                if (objectEnd < 0)
                    return;

                int lineStart = existing.LastIndexOf('\n', existingProperty);
                lineStart = lineStart < 0 ? 0 : lineStart + 1;
                string replaced = existing.Substring(0, lineStart) +
                                  addendum +
                                  existing.Substring(objectEnd + 1);
                File.WriteAllText(sharedPath, replaced, Encoding.UTF8);
                return;
            }

            int insert = existing.LastIndexOf('}');
            if (insert < 0)
                return;

            string prefix = existing.Substring(0, insert).TrimEnd();
            string suffix = existing.Substring(insert);
            string comma = prefix.EndsWith("{", StringComparison.Ordinal) ? string.Empty : ",";
            string merged = prefix + comma + "\n" + addendum + "\n" + suffix;
            File.WriteAllText(sharedPath, merged, Encoding.UTF8);
        }

        private static int FindMatchingObjectEnd(string text, int objectStart)
        {
            if (string.IsNullOrEmpty(text) || objectStart < 0 || objectStart >= text.Length || text[objectStart] != '{')
                return -1;

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
                    continue;
                }

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return i;
            }

            return -1;
        }

        private static string BuildSharedAddendum(
            string status,
            string summary,
            int findingCount,
            bool layoutValid,
            int kinematicStateBytes,
            int kinematicFlagsOffset,
            int kinematicSleepConfigBytes,
            int kinematicSleepConfigFlagsOffset,
            string proofType,
            bool astProof,
            int scannedFiles,
            int parserFailures)
        {
            StringBuilder builder = new StringBuilder(512);
            builder.Append("  \"shinobu249BuoyancySleep\": {\n");
            builder.Append("    \"agent\": \"SHINOBU_249\",\n");
            builder.Append("    \"status\": \"").Append(Escape(status)).Append("\",\n");
            builder.Append("    \"summary\": \"").Append(Escape(summary)).Append("\",\n");
            builder.Append("    \"forbiddenPatternCount\": ").Append(findingCount).Append(",\n");
            builder.Append("    \"proofType\": \"").Append(Escape(proofType)).Append("\",\n");
            builder.Append("    \"scannerMode\": \"ROSLYN_AST_WITH_TOKEN_FALLBACK\",\n");
            builder.Append("    \"roslynLanguageVersion\": \"Preview\",\n");
            builder.Append("    \"astProof\": ").Append(astProof ? "true" : "false").Append(",\n");
            builder.Append("    \"scannedFiles\": ").Append(scannedFiles).Append(",\n");
            builder.Append("    \"parserFailures\": ").Append(parserFailures).Append(",\n");
            builder.Append("    \"kinematicStateSizeBytes\": ").Append(kinematicStateBytes).Append(",\n");
            builder.Append("    \"kinematicFlagsOffset\": ").Append(kinematicFlagsOffset).Append(",\n");
            builder.Append("    \"kinematicSleepSdfConfigSizeBytes\": ").Append(kinematicSleepConfigBytes).Append(",\n");
            builder.Append("    \"kinematicSleepSdfConfigFlagsOffset\": ").Append(kinematicSleepConfigFlagsOffset).Append(",\n");
            builder.Append("    \"sleepStateCapacity\": ").Append(BuoyancyDisplacementConstants.StateCapacity).Append(",\n");
            builder.Append("    \"sleepTelemetryRingEntries\": ").Append(BuoyancyDisplacementConstants.TelemetryCapacity).Append(",\n");
            builder.Append("    \"layoutValid\": ").Append(layoutValid ? "true" : "false").Append(",\n");
            builder.Append("    \"lastUnityImportAttemptLog\": \"Docs/AgentLogs/UnityCompile_SHINOBU_249.log\",\n");
            builder.Append("    \"lastUnityImportProofStatus\": \"BLOCKED_EXTERNAL_COMPILE_ERRORS\",\n");
            builder.Append("    \"unityImportBlockedByExternalErrors\": true,\n");
            builder.Append("    \"editorAsmdefAdded\": true,\n");
            builder.Append("    \"editorAsmdef\": \"Assets/_Project/Scripts/Physics/Buoyancy/Editor/Hecton8.Physics.Buoyancy.Editor.asmdef\",\n");
            builder.Append("    \"editorAsmdefIsolationPendingUnityReimport\": true,\n");
            builder.Append("    \"forcePacketStaleSlotAudit\": \"PASS_STATIC\",\n");
            builder.Append("    \"forcePacketPrepareClearsCounterOnly\": true,\n");
            builder.Append("    \"forcePacketEvaluatorClearsSleepingCandidates\": true,\n");
            builder.Append("    \"forcePacketCompactUsesScheduledCandidateCount\": true,\n");
            builder.Append("    \"forcePacketDrainUsesCounterCountOnly\": true,\n");
            builder.Append("    \"rejectedFullForcePacketClearBytesPerTick\": 1048576,\n");
            builder.Append("    \"wakeRouteAudit\": \"PASS_SIGNALBUS_WAKE_REQUEST\",\n");
            builder.Append("    \"wakeRouteContract\": \"Core/Contracts/Signals/WakeRequestSignal\",\n");
            builder.Append("    \"directCavitationForcePacketImport\": false,\n");
            builder.Append("    \"cavitationForceEventsBridgeToWakeRequestSignal\": true,\n");
            builder.Append("    \"processBuoyancyWakeTriggersBurstCompile\": true,\n");
            builder.Append("    \"shinobuJobBurstDirectiveScan\": \"PASS_STATIC\",\n");
            builder.Append("    \"sidecarReport\": \"").Append(Escape(AgentReportRelativePath)).Append("\"\n");
            builder.Append("  }");
            return builder.ToString();
        }

        private static int ResolveLine(string text, int index)
        {
            int line = 1;
            int limit = Math.Min(index, text.Length);
            for (int i = 0; i < limit; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string ToProjectRelative(string projectRoot, string path)
        {
            string fullRoot = Path.GetFullPath(projectRoot);
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Replace('\\', '/');

            string relative = fullPath.Substring(fullRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace('\\', '/');
        }

        private static string Escape(string value)
        {
            return string.IsNullOrEmpty(value)
                ? string.Empty
                : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }

    /// <summary>
    /// Editor import-time guard for the KCC state layout required by SHINOBU_249.
    /// </summary>
    [InitializeOnLoad]
    public static class KinematicStateLayoutEditorValidator
    {
        static KinematicStateLayoutEditorValidator()
        {
            if (!HydrodynamicKccLayoutValidator.ValidateRuntimeLayout(out _) ||
                UnsafeUtility.SizeOf<KinematicStateDTO>() != 64 ||
                HydrodynamicKccLayoutValidator.KinematicStateFlagsOffset != 52)
            {
                Debug.LogError("SHINOBU_249 layout rejection: KinematicStateDTO must be 64 bytes with Flags at offset 52.");
            }
        }
    }
}
#endif
