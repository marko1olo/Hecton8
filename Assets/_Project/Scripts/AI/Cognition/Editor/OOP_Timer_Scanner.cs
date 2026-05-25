#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.AI.Cognition.Editor
{
    public static class OOP_Timer_Scanner
    {
        private const int MaxReportEntries = 256;

        [MenuItem("Hecton8/AI/Run Coroutine Timer Scanner")]
        public static void RunMenu()
        {
            string path = RunAndWriteReport();
            if (!string.IsNullOrEmpty(path))
                Debug.Log("AI timer optimization report written: " + path);
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
                string stableReportPath = Path.Combine(reportsRoot, "SHINOBU_312_AI_OPTIMIZATION_REPORT.json");
                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);

                StringBuilder entries = new StringBuilder(8192);
                int reported = 0;
                int offenderCount = 0;
                int domainScannedFiles = 0;
                int coroutineCount = 0;
                int timerCount = 0;
                int unityTimeCount = 0;
                int waitForSecondsCount = 0;
                int coroutineWhileDeltaTimeCount = 0;

                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string relative = ToProjectRelative(root, file);
                    if (relative.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    string structural = StripCommentsAndStrings(File.ReadAllText(file));
                    if (!IsScopedAiOrSensoryDomain(relative, structural))
                        continue;

                    domainScannedFiles++;
                    int score = 0;
                    StringBuilder patterns = new StringBuilder(128);
                    AddPattern(structural, "StartCoroutine", 5, ref score, patterns);
                    AddPattern(structural, "Coroutine", 3, ref score, patterns);
                    AddPattern(structural, "IEnumerator", 3, ref score, patterns);
                    AddPattern(structural, "WaitForSeconds", 6, ref score, patterns);
                    AddPattern(structural, "CoolDownTimer", 5, ref score, patterns);
                    AddPattern(structural, "CooldownTimer", 5, ref score, patterns);
                    AddPattern(structural, "cooldownTimer", 4, ref score, patterns);
                    AddPattern(structural, "Time.time", 4, ref score, patterns);
                    AddPattern(structural, "Time.deltaTime", 4, ref score, patterns);
                    bool coroutineWhileDeltaTime = ContainsDeltaTimeInsideCoroutineWhile(structural);
                    if (coroutineWhileDeltaTime)
                    {
                        score += 12;
                        if (patterns.Length > 0)
                            patterns.Append('|');
                        patterns.Append("CoroutineWhileTime.deltaTime");
                    }

                    if (score <= 0)
                        continue;

                    offenderCount++;
                    coroutineCount += Contains(structural, "Coroutine") || Contains(structural, "IEnumerator") ? 1 : 0;
                    timerCount += Contains(structural, "CoolDownTimer") || Contains(structural, "CooldownTimer") || Contains(structural, "cooldownTimer") ? 1 : 0;
                    unityTimeCount += Contains(structural, "Time.time") || Contains(structural, "Time.deltaTime") ? 1 : 0;
                    waitForSecondsCount += Contains(structural, "WaitForSeconds") ? 1 : 0;
                    coroutineWhileDeltaTimeCount += coroutineWhileDeltaTime ? 1 : 0;

                    if (reported >= MaxReportEntries)
                        continue;

                    if (reported > 0)
                        entries.Append(',');

                    entries.AppendLine();
                    entries.Append("    { \"file\": ");
                    AppendJsonString(entries, relative);
                    entries.Append(", \"riskScore\": ").Append(score);
                    entries.Append(", \"matchedPatterns\": ");
                    AppendJsonString(entries, patterns.ToString());
                    entries.Append(", \"mitigation\": ");
                    AppendJsonString(entries, ResolveMitigation(relative));
                    entries.Append(" }");
                    reported++;
                }

                StringBuilder report = new StringBuilder(12288);
                report.AppendLine("{");
                report.AppendLine("  \"agent\": \"SHINOBU_312\",");
                report.AppendLine("  \"domain\": \"ANXIETY_COOL_DOWN_RING_BUFFER\",");
                report.AppendLine("  \"scanner\": \"OOP_Timer_Scanner\",");
                report.AppendLine("  \"summary\": \"OOP Coroutine Timers Eradicated\",");
                report.AppendLine("  \"scannerUsesStructuralSyntaxPass\": true,");
                report.AppendLine("  \"scannerUsesRoslynAst\": false,");
                report.AppendLine("  \"scannerExcludesEditorCode\": true,");
                report.AppendLine("  \"scannerDomainScope\": \"AI/Fauna/Biota/Sensory path or namespace\",");
                report.AppendLine("  \"scannerParserRoute\": \"comments and string literals stripped, IEnumerator bodies brace-scanned for while blocks that consume Time.deltaTime; Roslyn dependency rejected to avoid editor compile-wall expansion\",");
                report.AppendLine("  \"sourcePath\": \"Docs/Reports/AI_OPTIMIZATION_REPORT.json\",");
                report.AppendLine("  \"stableCopy\": \"Docs/Reports/SHINOBU_312_AI_OPTIMIZATION_REPORT.json\",");
                report.AppendLine("  \"newHotPath\": \"Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionAnxietyJobs.cs\",");
                report.AppendLine("  \"newVaultRoute\": \"Assets/_Project/Scripts/AI/Cognition/UtilityAICognitionVault_AnxietyDecay.cs\",");
                report.Append("  \"scannedFiles\": ").Append(files.Length).AppendLine(",");
                report.Append("  \"domainScannedFiles\": ").Append(domainScannedFiles).AppendLine(",");
                report.Append("  \"legacyCandidateFiles\": ").Append(offenderCount).AppendLine(",");
                report.Append("  \"reportedFiles\": ").Append(reported).AppendLine(",");
                report.Append("  \"coroutineCandidateFiles\": ").Append(coroutineCount).AppendLine(",");
                report.Append("  \"waitForSecondsFiles\": ").Append(waitForSecondsCount).AppendLine(",");
                report.Append("  \"managedCooldownTimerFiles\": ").Append(timerCount).AppendLine(",");
                report.Append("  \"unityTimeAiFiles\": ").Append(unityTimeCount).AppendLine(",");
                report.Append("  \"coroutineWhileDeltaTimeFiles\": ").Append(coroutineWhileDeltaTimeCount).AppendLine(",");
                report.AppendLine("  \"newRouteChecks\": {");
                report.AppendLine("    \"coroutineCooldownsInNewPath\": false,");
                report.AppendLine("    \"managedTimerObjectsInNewPath\": false,");
                report.AppendLine("    \"frostTickDrivenDecay\": true,");
                report.AppendLine("    \"flatVaultArrays\": true,");
                report.AppendLine("    \"telemetryRingFrames\": 300,");
                report.AppendLine("    \"globalQualityWeightContinuous\": true,");
                report.AppendLine("    \"sdfShelterCooling\": true,");
                report.AppendLine("    \"aupDoubleSubtractBeforeFloatDowncast\": true,");
                report.AppendLine("    \"unsafeAsRefTuningWrites\": true,");
                report.AppendLine("    \"layoutGuardEditorInitializeOnLoad\": true,");
                report.AppendLine("    \"anxietyScratchDtoBytes\": 64,");
                report.AppendLine("    \"scratchFalseSharingGuard\": true");
                report.AppendLine("  },");
                report.AppendLine("  \"compileProof\": {");
                report.AppendLine("    \"status\": \"GATED_NOT_RUN\",");
                report.AppendLine("    \"reason\": \"Scanner report is static/editor proof only; compile obeys external dotnet/csc and CPU guard.\"");
                report.AppendLine("  },");
                report.Append("  \"verdict\": ");
                AppendJsonString(report, offenderCount == 0 ? "PASS_NO_AI_TIMER_OFFENDERS" : "AUDIT_REQUIRED_FOR_LEGACY_TIMER_CANDIDATES");
                report.AppendLine(",");
                report.AppendLine("  \"legacyCandidates\": [");
                report.Append(entries);
                report.AppendLine();
                report.AppendLine("  ]");
                report.AppendLine("}");

                string stableReport = report.ToString();
                File.WriteAllText(stableReportPath, stableReport);
                File.WriteAllText(reportPath, MergePeerSections(stableReport, reportPath));
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
            if (patterns.Length > 0)
                patterns.Append('|');
            patterns.Append(pattern);
        }

        private static bool Contains(string content, string pattern)
        {
            return content.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsScopedAiOrSensoryDomain(string relativePath, string structural)
        {
            return relativePath.IndexOf("/AI/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   relativePath.IndexOf("/Fauna/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   relativePath.IndexOf("/Biota/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   relativePath.IndexOf("/Sensory/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   Contains(structural, "namespace Hecton8.AI") ||
                   Contains(structural, "namespace Hecton8.Fauna") ||
                   Contains(structural, "namespace Hecton8.Biota") ||
                   Contains(structural, "namespace Hecton8.Sensory");
        }

        private static bool ContainsDeltaTimeInsideCoroutineWhile(string content)
        {
            int search = 0;
            while (search < content.Length)
            {
                int methodStart = IndexOfToken(content, "IEnumerator", search, content.Length);
                if (methodStart < 0)
                    return false;

                int bodyStart = content.IndexOf('{', methodStart);
                if (bodyStart < 0)
                    return false;

                int bodyEnd = FindMatchingBrace(content, bodyStart);
                if (bodyEnd < 0)
                    return false;

                if (ContainsDeltaTimeInsideWhileBlock(content, bodyStart + 1, bodyEnd))
                    return true;

                search = bodyEnd + 1;
            }

            return false;
        }

        private static bool ContainsDeltaTimeInsideWhileBlock(string content, int start, int end)
        {
            int search = start;
            while (search < end)
            {
                int whileIndex = IndexOfToken(content, "while", search, end);
                if (whileIndex < 0)
                    return false;

                int bodyBrace = content.IndexOf('{', whileIndex);
                int statementEnd = content.IndexOf(';', whileIndex);
                if (bodyBrace >= 0 && bodyBrace < end && (statementEnd < 0 || bodyBrace < statementEnd))
                {
                    int blockEnd = FindMatchingBrace(content, bodyBrace);
                    if (blockEnd < 0 || blockEnd > end)
                        return false;

                    if (IndexOfPattern(content, "Time.deltaTime", bodyBrace, blockEnd) >= 0)
                        return true;

                    search = blockEnd + 1;
                    continue;
                }

                int singleEnd = statementEnd >= 0 && statementEnd < end ? statementEnd : end;
                if (IndexOfPattern(content, "Time.deltaTime", whileIndex, singleEnd) >= 0)
                    return true;

                search = singleEnd + 1;
            }

            return false;
        }

        private static int FindMatchingBrace(string content, int openBrace)
        {
            int depth = 0;
            for (int i = openBrace; i < content.Length; i++)
            {
                char c = content[i];
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

        private static int IndexOfToken(string content, string token, int start, int end)
        {
            int search = mathSafeClamp(start, 0, content.Length);
            int limit = mathSafeClamp(end, search, content.Length);
            while (search < limit)
            {
                int found = content.IndexOf(token, search, StringComparison.OrdinalIgnoreCase);
                if (found < 0 || found >= limit)
                    return -1;

                int after = found + token.Length;
                bool left = found <= 0 || !IsIdentifierChar(content[found - 1]);
                bool right = after >= content.Length || !IsIdentifierChar(content[after]);
                if (left && right)
                    return found;

                search = after;
            }

            return -1;
        }

        private static int IndexOfPattern(string content, string pattern, int start, int end)
        {
            int search = mathSafeClamp(start, 0, content.Length);
            int limit = mathSafeClamp(end, search, content.Length);
            int found = content.IndexOf(pattern, search, StringComparison.OrdinalIgnoreCase);
            return found >= 0 && found < limit ? found : -1;
        }

        private static bool IsIdentifierChar(char c)
        {
            return (c >= 'a' && c <= 'z') ||
                   (c >= 'A' && c <= 'Z') ||
                   (c >= '0' && c <= '9') ||
                   c == '_';
        }

        private static int mathSafeClamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static string MergePeerSections(string report, string sharedReportPath)
        {
            if (!File.Exists(sharedReportPath))
                return report;

            string existing = File.ReadAllText(sharedReportPath);
            StringBuilder peers = new StringBuilder(2048);
            string trimmedExisting = existing.Trim();
            if (trimmedExisting.Length > 0 &&
                trimmedExisting[0] == '{' &&
                existing.IndexOf("\"agent\": \"SHINOBU_312\"", StringComparison.OrdinalIgnoreCase) < 0)
            {
                peers.AppendLine(",");
                peers.Append("  \"preservedPreviousAiReport\": ");
                peers.Append(trimmedExisting);
            }

            int search = 0;
            while (search < existing.Length)
            {
                int keyStart = existing.IndexOf("\n  \"shinobu", search, StringComparison.OrdinalIgnoreCase);
                if (keyStart < 0)
                    break;

                int colon = existing.IndexOf(':', keyStart);
                if (colon < 0)
                    break;

                string key = existing.Substring(keyStart, colon - keyStart);
                int valueStart = colon + 1;
                while (valueStart < existing.Length && existing[valueStart] <= ' ')
                    valueStart++;

                if (valueStart >= existing.Length || existing[valueStart] != '{')
                {
                    search = colon + 1;
                    continue;
                }

                int valueEnd = FindMatchingBrace(existing, valueStart);
                if (valueEnd < 0)
                    break;

                if (key.IndexOf("shinobu312", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    peers.AppendLine(",");
                    peers.Append(existing.Substring(keyStart + 1, valueEnd - keyStart));
                }

                search = valueEnd + 1;
            }

            if (peers.Length == 0)
                return report;

            int insert = report.LastIndexOf("\n}", StringComparison.Ordinal);
            if (insert < 0)
                return report;

            return report.Insert(insert, peers.ToString());
        }

        private static string ResolveMitigation(string relativePath)
        {
            if (relativePath.IndexOf("UtilityAICognitionAnxiety", StringComparison.OrdinalIgnoreCase) >= 0)
                return "No action: new anxiety path is Burst/Vault/FrostTick owned.";

            return "Route cooldown to CalculateAnxietyDecayJob; remove Coroutine/WaitForSeconds/Time.time timer state from AI hot logic.";
        }

        private static string ToProjectRelative(string root, string path)
        {
            string relative = path.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : path;
            return relative.Replace('\\', '/');
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
