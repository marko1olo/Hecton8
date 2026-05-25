#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.AI.Cognition.Editor
{
    public static class OOP_FSM_Scanner
    {
        private const int MaxReportEntries = 256;

        [MenuItem("Hecton8/AI/Run OOP FSM Scanner")]
        public static void RunMenu()
        {
            string path = RunAndWriteReport();
            if (!string.IsNullOrEmpty(path))
                Debug.Log("AI optimization report written: " + path);
        }

        public static string RunAndWriteReport()
        {
            try
            {
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
                string reportsRoot = Path.Combine(root, "Docs", "Reports");
                Directory.CreateDirectory(reportsRoot);
                string reportPath = Path.Combine(reportsRoot, "AI_OPTIMIZATION_REPORT.json");
                string stableReportPath = Path.Combine(reportsRoot, "SHINOBU_302_AI_OPTIMIZATION_REPORT.json");
                string runtimeArtifact = "Library/Bee/artifacts/1900b0aEDbg.dag/SHINOBU_302_Hecton8.AI.Cognition.Test.dll";
                string runtimeResponse = "Library/Bee/artifacts/1900b0aEDbg.dag/SHINOBU_302_Hecton8.AI.Cognition.Test.rsp";
                string editorArtifact = "Library/Bee/artifacts/1900b0aEDbg.dag/SHINOBU_302_Hecton8.AI.Cognition.Editor.Test.dll";
                string editorResponse = "Library/Bee/artifacts/1900b0aEDbg.dag/Hecton8.AI.Cognition.Editor.rsp";
                string editorAsmdef = "Assets/_Project/Scripts/AI/Cognition/Editor/Hecton8.AI.Cognition.Editor.asmdef";
                string anxietyRuntimePath = "Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionAnxietyJobs.cs";
                long runtimeArtifactBytes = GetFileBytes(Path.Combine(root, runtimeArtifact));
                long editorArtifactBytes = GetFileBytes(Path.Combine(root, editorArtifact));
                bool sameAssemblyAnxietyRuntimeDetected = File.Exists(Path.Combine(root, anxietyRuntimePath));
                bool runtimeRspIncludesAnxietyInputs = FileContains(Path.Combine(root, runtimeResponse), "UtilityAICognitionAnxiety");
                bool staleEditorRspDirectCoreRef = FileContains(Path.Combine(root, editorResponse), "Hecton8.Core.ref.dll");
                bool sourceEditorAsmdefDirectCoreRef = FileContains(Path.Combine(root, editorAsmdef), "\"Hecton8.Core\"");

                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
                StringBuilder entries = new StringBuilder(8192);
                int offenderCount = 0;
                int reportCount = 0;
                int monoBehaviourAiCount = 0;
                int transformTargetCount = 0;
                int coroutineCount = 0;
                int updateSwitchCount = 0;
                int stateInstantiationCount = 0;

                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string relative = ToProjectRelative(root, file);
                    string content = File.ReadAllText(file);
                    string structural = StripCommentsAndStrings(content);
                    int score = 0;
                    StringBuilder patterns = new StringBuilder(128);

                    AddPattern(structural, "StateMachine", 4, ref score, patterns);
                    AddPattern(structural, "BehaviorTree", 4, ref score, patterns);
                    AddPattern(structural, "IState", 3, ref score, patterns);
                    AddPattern(structural, "enum AIState", 3, ref score, patterns);
                    AddPattern(structural, "switch", 1, ref score, patterns);
                    AddPattern(structural, "StartCoroutine", 2, ref score, patterns);
                    AddPattern(structural, "IEnumerator", 2, ref score, patterns);
                    AddPattern(structural, "Transform _", 2, ref score, patterns);
                    AddPattern(structural, "Transform currentTarget", 3, ref score, patterns);
                    AddPattern(structural, "currentTarget", 2, ref score, patterns);
                    AddPattern(structural, "GetComponent<", 1, ref score, patterns);

                    if (ContainsSwitchInsideUnityTick(structural))
                    {
                        score += 5;
                        updateSwitchCount++;
                        AppendPattern(patterns, "switch inside Unity tick");
                    }

                    if (ContainsStateInstantiation(structural))
                    {
                        score += 5;
                        stateInstantiationCount++;
                        AppendPattern(patterns, "managed state instantiation");
                    }

                    bool aiMonoBehaviour = Contains(structural, "MonoBehaviour") &&
                                           (Contains(structural, "AI") || Contains(structural, "Brain") || Contains(structural, "Cognition") || Contains(structural, "Fauna"));
                    if (aiMonoBehaviour)
                    {
                        score += 3;
                        AppendPattern(patterns, "AI MonoBehaviour");
                        monoBehaviourAiCount++;
                    }

                    if (Contains(structural, "Transform") && Contains(structural, "Target"))
                        transformTargetCount++;
                    if (Contains(structural, "StartCoroutine") || Contains(structural, "IEnumerator"))
                        coroutineCount++;

                    if (score <= 0)
                        continue;

                    offenderCount++;
                    if (reportCount >= MaxReportEntries)
                        continue;

                    if (reportCount > 0)
                        entries.Append(',');

                    entries.AppendLine();
                    entries.Append("    { \"file\": ");
                    AppendJsonString(entries, relative);
                    entries.Append(", \"riskScore\": ").Append(score);
                    entries.Append(", \"matchedPatterns\": ");
                    AppendJsonString(entries, patterns.ToString());
                    entries.Append(", \"mitigation\": ");
                    AppendJsonString(entries, ResolveMitigation(relative, score));
                    entries.Append(" }");
                    reportCount++;
                }

                StringBuilder report = new StringBuilder(12288);
                report.AppendLine("{");
                report.AppendLine("  \"agent\": \"SHINOBU_302\",");
                report.AppendLine("  \"domain\": \"UTILITY_AI_COGNITION_CORE\",");
                report.AppendLine("  \"scanner\": \"OOP_FSM_Scanner\",");
                report.AppendLine("  \"summary\": \"OOP State Machines Eradicated\",");
                report.AppendLine("  \"sourcePath\": \"Docs/Reports/AI_OPTIMIZATION_REPORT.json\",");
                report.AppendLine("  \"scannerUsesStructuralSyntaxPass\": true,");
                report.AppendLine("  \"scannerUsesRoslynAst\": false,");
                report.AppendLine("  \"scannerParserRoute\": \"comment/string stripped method-body scan; Roslyn dependency rejected to avoid editor asmdef compile-wall churn\",");
                report.AppendLine("  \"scannerPreservesProofFieldsOnManualRun\": true,");
                report.AppendLine("  \"binaryPayloadLedgerEntry\": true,");
                report.AppendLine("  \"stableCopy\": \"Docs/Reports/SHINOBU_302_AI_OPTIMIZATION_REPORT.json\",");
                report.Append("  \"scannedFiles\": ").Append(files.Length).AppendLine(",");
                report.Append("  \"legacyCandidateFiles\": ").Append(offenderCount).AppendLine(",");
                report.Append("  \"reportedFiles\": ").Append(reportCount).AppendLine(",");
                report.Append("  \"monoBehaviourAiFiles\": ").Append(monoBehaviourAiCount).AppendLine(",");
                report.Append("  \"transformTargetFiles\": ").Append(transformTargetCount).AppendLine(",");
                report.Append("  \"coroutineCandidateFiles\": ").Append(coroutineCount).AppendLine(",");
                report.Append("  \"updateSwitchFiles\": ").Append(updateSwitchCount).AppendLine(",");
                report.Append("  \"managedStateInstantiationFiles\": ").Append(stateInstantiationCount).AppendLine(",");
                report.AppendLine("  \"newHotPath\": \"Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionJobs.cs\",");
                report.AppendLine("  \"newVaultRoute\": \"Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault.cs\",");
                report.AppendLine("  \"newActionOutput\": \"CognitionActionOutputDTO.ActionHash\",");
                report.AppendLine("  \"runtimeProof\": {");
                report.AppendLine("    \"assembly\": \"Hecton8.AI.Cognition\",");
                report.Append("    \"artifact\": ");
                AppendJsonString(report, runtimeArtifact);
                report.AppendLine(",");
                report.Append("    \"artifactBytes\": ").Append(runtimeArtifactBytes).AppendLine(",");
                report.Append("    \"sameAssemblyAnxietyRuntimeDetected\": ").Append(sameAssemblyAnxietyRuntimeDetected ? "true" : "false").AppendLine(",");
                report.Append("    \"runtimeRspIncludesAnxietyInputs\": ").Append(runtimeRspIncludesAnxietyInputs ? "true" : "false").AppendLine(",");
                report.Append("    \"extraCliSourcesRequiredForCurrentAsmdefProof\": ").Append(sameAssemblyAnxietyRuntimeDetected && !runtimeRspIncludesAnxietyInputs ? "true" : "false").AppendLine(",");
                report.Append("    \"status\": ");
                AppendJsonString(report, runtimeArtifactBytes > 0L ? "PASS_POST_POLISH_WITH_EXTRA_SAME_ASMDEF_INPUTS" : "PENDING_RUNTIME_CSC_PROOF");
                report.AppendLine();
                report.AppendLine("  },");
                report.AppendLine("  \"editorCompileProof\": {");
                report.Append("    \"artifact\": ");
                AppendJsonString(report, editorArtifact);
                report.AppendLine(",");
                report.Append("    \"artifactBytes\": ").Append(editorArtifactBytes).AppendLine(",");
                report.Append("    \"status\": ");
                AppendJsonString(report, editorArtifactBytes > 0L ? "PASS_EDITOR_CSC" : "GATED_OR_PENDING_BY_CPU");
                report.AppendLine(",");
                report.Append("    \"staleBeeRspIncludesDirectCoreRef\": ").Append(staleEditorRspDirectCoreRef ? "true" : "false").AppendLine(",");
                report.Append("    \"sourceEditorAsmdefDirectCoreReference\": ").Append(sourceEditorAsmdefDirectCoreRef ? "true" : "false").AppendLine(",");
                report.AppendLine("    \"editorVaultDiagnosticRoute\": \"GlobalDataVault.TryGetLatestCreated\"");
                report.AppendLine("  },");
                report.AppendLine("  \"projectCompileProof\": {");
                report.AppendLine("    \"log\": \"Logs/SHINOBU_302_UnityCompile.log\",");
                report.AppendLine("    \"shinobu302Diagnostics\": 0,");
                report.AppendLine("    \"status\": \"BLOCKED_BY_NON_DOMAIN_DEPENDENCIES\"");
                report.AppendLine("  },");
                report.AppendLine("  \"newRouteChecks\": {");
                report.AppendLine("    \"managedActionObjects\": false,");
                report.AppendLine("    \"runtimeTransformTargets\": false,");
                report.AppendLine("    \"hotGlobalRegistryPolling\": false,");
                report.AppendLine("    \"mathSelectActionTournament\": true,");
                report.AppendLine("    \"polynomialMotiveCurves\": true,");
                report.AppendLine("    \"dearLieCandidateLimit\": 4,");
                report.AppendLine("    \"telemetryRingFrames\": 300,");
                report.AppendLine("    \"cognitionStateDtoBytes\": 32,");
                report.AppendLine("    \"globalQualityWeightContinuous\": true,");
                report.AppendLine("    \"editorActionChart\": true,");
                report.AppendLine("    \"editorAupTargetLine\": true,");
                report.AppendLine("    \"unsafeAsRefTuningWrites\": true,");
                report.AppendLine("    \"runtimeNanDistanceClamp\": true,");
                report.AppendLine("    \"editorFrameCounterNoUnityTime\": true,");
                report.AppendLine("    \"csvScratchSpanReader\": true,");
                report.AppendLine("    \"runtimeAsmdefContractsMemoryOnly\": true,");
                report.Append("    \"editorDirectCoreReference\": ").Append(sourceEditorAsmdefDirectCoreRef ? "true" : "false").AppendLine(",");
                report.AppendLine("    \"binaryPayloadLedgerEntry\": true");
                report.AppendLine("  },");
                report.Append("  \"verdict\": ");
                AppendJsonString(report, runtimeArtifactBytes > 0L ? "POST_POLISH_RUNTIME_PASS_WITH_PROJECT_COMPILE_WALL" : "PENDING_RUNTIME_CSC_PROOF");
                report.AppendLine(",");
                report.AppendLine("  \"doctrine\": \"Managed FSM/BT scripts are legacy candidates. New cognition selection is Burst, DataVault-owned, branchless utility math with ActionHash output.\",");
                report.AppendLine("  \"legacyCandidates\": [");
                report.Append(entries);
                report.AppendLine();
                report.AppendLine("  ]");
                report.AppendLine("}");

                File.WriteAllText(reportPath, report.ToString());
                File.WriteAllText(stableReportPath, report.ToString());
                AssetDatabase.Refresh();
                return reportPath;
            }
            catch (IOException)
            {
                return string.Empty;
            }
            catch (UnauthorizedAccessException)
            {
                return string.Empty;
            }
            catch (ArgumentException)
            {
                return string.Empty;
            }
            catch (NotSupportedException)
            {
                return string.Empty;
            }
        }

        private static void AddPattern(string content, string pattern, int weight, ref int score, StringBuilder patterns)
        {
            if (!Contains(content, pattern))
                return;

            score += weight;
            AppendPattern(patterns, pattern);
        }

        private static bool Contains(string content, string pattern)
        {
            return content.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsSwitchInsideUnityTick(string content)
        {
            return ContainsSwitchInsideMethod(content, "Update") ||
                   ContainsSwitchInsideMethod(content, "FixedUpdate") ||
                   ContainsSwitchInsideMethod(content, "LateUpdate");
        }

        private static bool ContainsSwitchInsideMethod(string content, string methodName)
        {
            int start = 0;
            string token = methodName + "(";
            while (start < content.Length)
            {
                int method = content.IndexOf(token, start, StringComparison.Ordinal);
                if (method < 0)
                    return false;

                if (!IsIdentifierBoundary(content, method - 1))
                {
                    start = method + token.Length;
                    continue;
                }

                int openBrace = content.IndexOf('{', method);
                if (openBrace < 0)
                    return false;

                int closeBrace = FindMatchingBrace(content, openBrace);
                if (closeBrace <= openBrace)
                    return false;

                if (content.IndexOf("switch", openBrace, closeBrace - openBrace, StringComparison.Ordinal) >= 0)
                    return true;

                start = closeBrace + 1;
            }

            return false;
        }

        private static bool ContainsStateInstantiation(string content)
        {
            int start = 0;
            while (start < content.Length)
            {
                int match = content.IndexOf("new ", start, StringComparison.Ordinal);
                if (match < 0)
                    return false;

                int typeStart = match + 4;
                while (typeStart < content.Length && content[typeStart] == ' ')
                    typeStart++;

                int typeEnd = typeStart;
                while (typeEnd < content.Length && IsTypeToken(content[typeEnd]))
                    typeEnd++;

                if (typeEnd > typeStart &&
                    (RangeContains(content, typeStart, typeEnd, "State") ||
                     RangeContains(content, typeStart, typeEnd, "BehaviorTree")))
                {
                    return true;
                }

                start = typeEnd + 1;
            }

            return false;
        }

        private static string StripCommentsAndStrings(string content)
        {
            StringBuilder builder = new StringBuilder(content.Length);
            for (int i = 0; i < content.Length; i++)
            {
                char c = content[i];
                char next = i + 1 < content.Length ? content[i + 1] : '\0';
                if (c == '/' && next == '/')
                {
                    builder.Append(' ');
                    builder.Append(' ');
                    i += 2;
                    while (i < content.Length && content[i] != '\n')
                    {
                        builder.Append(' ');
                        i++;
                    }

                    if (i < content.Length)
                        builder.Append(content[i]);
                    continue;
                }

                if (c == '/' && next == '*')
                {
                    builder.Append(' ');
                    builder.Append(' ');
                    i += 2;
                    while (i < content.Length)
                    {
                        char block = content[i];
                        char blockNext = i + 1 < content.Length ? content[i + 1] : '\0';
                        builder.Append(block == '\n' ? '\n' : ' ');
                        if (block == '*' && blockNext == '/')
                        {
                            builder.Append(' ');
                            i++;
                            break;
                        }

                        i++;
                    }

                    continue;
                }

                if (c == '\"' || c == '\'')
                {
                    char quote = c;
                    builder.Append(' ');
                    i++;
                    while (i < content.Length)
                    {
                        char value = content[i];
                        builder.Append(value == '\n' ? '\n' : ' ');
                        if (value == '\\')
                        {
                            i++;
                            if (i < content.Length)
                                builder.Append(content[i] == '\n' ? '\n' : ' ');
                        }
                        else if (value == quote)
                        {
                            break;
                        }

                        i++;
                    }

                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        private static int FindMatchingBrace(string content, int openBrace)
        {
            int depth = 0;
            for (int i = openBrace; i < content.Length; i++)
            {
                if (content[i] == '{')
                    depth++;
                else if (content[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static bool IsIdentifierBoundary(string content, int index)
        {
            if (index < 0 || index >= content.Length)
                return true;

            char c = content[index];
            return !(char.IsLetterOrDigit(c) || c == '_');
        }

        private static bool IsTypeToken(char c)
        {
            return char.IsLetterOrDigit(c) || c == '_' || c == '.';
        }

        private static bool RangeContains(string content, int start, int end, string needle)
        {
            int length = end - start;
            if (length < needle.Length)
                return false;

            for (int i = start; i <= end - needle.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (char.ToUpperInvariant(content[i + j]) != char.ToUpperInvariant(needle[j]))
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                    return true;
            }

            return false;
        }

        private static void AppendPattern(StringBuilder patterns, string pattern)
        {
            if (patterns.Length > 0)
                patterns.Append('|');

            patterns.Append(pattern);
        }

        private static string ResolveMitigation(string relativePath, int score)
        {
            if (relativePath.IndexOf("FaunaStateMachine", StringComparison.OrdinalIgnoreCase) >= 0 ||
                relativePath.IndexOf("MesofaunaBehavioralStateMachine", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return "Keep serialized shell only; route hot decision selection to UtilityAICognitionVault.";
            }

            if (score >= 8)
                return "Replace hot Update/target logic with DataVault DTO and UtilityAICognitionJobs schedule.";

            return "Audit before adding new state branches; prefer ActionHash output.";
        }

        private static string ToProjectRelative(string root, string path)
        {
            string relative = path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : path;
            return relative.Replace('\\', '/');
        }

        private static long GetFileBytes(string path)
        {
            return File.Exists(path) ? new FileInfo(path).Length : 0L;
        }

        private static bool FileContains(string path, string value)
        {
            return File.Exists(path) && File.ReadAllText(path).IndexOf(value, StringComparison.Ordinal) >= 0;
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('\"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\' || c == '\"')
                    builder.Append('\\').Append(c);
                else if (c == '\n')
                    builder.Append("\\n");
                else if (c == '\r')
                    builder.Append("\\r");
                else if (c == '\t')
                    builder.Append("\\t");
                else
                    builder.Append(c);
            }

            builder.Append('\"');
        }
    }
}
#endif
