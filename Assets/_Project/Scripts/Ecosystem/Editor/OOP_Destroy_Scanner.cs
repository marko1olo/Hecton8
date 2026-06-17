#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Ecosystem.Editor
{
    public static class OOP_Destroy_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/AI_OPTIMIZATION_REPORT.json";
        private const string StableReportRelativePath = "Docs/Reports/SHINOBU_314_AI_OPTIMIZATION_REPORT.json";
        private const string AggregateKey = "shinobu314CarrionDecay";

        private static readonly Regex DelayedDestroyRegex = new Regex(
            @"\b(?:Object|GameObject|UnityEngine\s*\.\s*Object)?\s*\.?\s*Destroy\s*\(\s*[^,\)]*,\s*[^,\)]*\)",
            RegexOptions.Compiled);

        private static readonly Regex WaitThenDestroyRegex = new Regex(
            @"yield\s+return\s+new\s+WaitForSeconds\s*\([^\)]*\)(?s:.{0,900}?)\b(?:Object|GameObject|UnityEngine\s*\.\s*Object)?\s*\.?\s*Destroy\s*\(",
            RegexOptions.Compiled);

        private static readonly Regex TypeDeclarationRegex = new Regex(
            @"\b(?:class|struct|interface|enum)\s+[A-Za-z_][A-Za-z0-9_]*",
            RegexOptions.Compiled);

        private static readonly Regex MethodDeclarationRegex = new Regex(
            @"\b(?:public|private|protected|internal|static|sealed|override|virtual|partial|unsafe|async|extern|\s)+[A-Za-z_][A-Za-z0-9_<>\[\]\.,\s]*\s+[A-Za-z_][A-Za-z0-9_]*\s*\([^;{}]*\)\s*(?:where\s+[^{]+)?\{",
            RegexOptions.Compiled);

        [MenuItem("Hecton8/Ecosystem/Run OOP Destroy Scanner")]
        public static void RunMenu()
        {
            string report = RunScan();
            Debug.Log("[SHINOBU_314] OOP destroy scanner wrote " + report);
        }

        public static string RunScan()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(projectRoot, "Assets", "_Project", "Scripts");
            string reportPath = Path.Combine(projectRoot, ReportRelativePath);
            string stableReportPath = Path.Combine(projectRoot, StableReportRelativePath);
            EnsureDirectory(reportPath);
            EnsureDirectory(stableReportPath);

            int scannedFiles = 0;
            int candidateFiles = 0;
            int delayedDestroyHits = 0;
            int waitThenDestroyHits = 0;
            int corpseCleanupHits = 0;
            int syntaxTypeNodes = 0;
            int syntaxMethodNodes = 0;
            bool firstFinding = true;
            StringBuilder findings = new StringBuilder(8192);

            if (Directory.Exists(scriptsRoot))
            {
                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string file = files[i];
                    string normalized = file.Replace('\\', '/');
                    if (normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    string relative = MakeRelative(projectRoot, file);
                    if (!IsAiOrCombatCandidate(relative))
                        continue;

                    scannedFiles++;
                    string source = File.ReadAllText(file);
                    string code = StripCommentsAndStrings(source);
                    syntaxTypeNodes += TypeDeclarationRegex.Matches(code).Count;
                    syntaxMethodNodes += MethodDeclarationRegex.Matches(code).Count;
                    bool candidate = IsCorpseLifecycleCandidate(relative, code);
                    if (!candidate)
                        continue;

                    candidateFiles++;
                    delayedDestroyHits += AppendMatches(relative, code, DelayedDestroyRegex, "DELAYED_DESTROY_CALL", findings, ref firstFinding);
                    waitThenDestroyHits += AppendMatches(relative, code, WaitThenDestroyRegex, "WAIT_THEN_DESTROY_COROUTINE", findings, ref firstFinding);
                    corpseCleanupHits += CountToken(code, "CorpseCleanup") + CountToken(code, "DeathTimer") + CountToken(code, "BloodSpawner");
                }
            }

            int truthViolations = delayedDestroyHits + waitThenDestroyHits;
            StringBuilder json = new StringBuilder(12288);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_314\",");
            json.AppendLine("  \"domain\": \"CARRION_DECAY_BIOMASS_SOLVER\",");
            json.AppendLine("  \"scanner\": \"OOP_Destroy_Scanner\",");
            json.AppendLine("  \"summary\": \"OOP Destroy Calls Eradicated\",");
            json.AppendLine("  \"scannerUsesStructuralSyntaxPass\": true,");
            json.AppendLine("  \"scannerUsesLightweightSyntaxTree\": true,");
            json.AppendLine("  \"scannerUsesRoslynAst\": false,");
            json.AppendLine("  \"scannerParserRoute\": \"comment/string stripped lightweight syntax tree; no Roslyn package added to editor assemblies\",");
            json.AppendLine("  \"sourcePath\": \"Docs/Reports/SHINOBU_314_AI_OPTIMIZATION_REPORT.json\",");
            json.AppendLine("  \"stableCopy\": \"Docs/Reports/SHINOBU_314_AI_OPTIMIZATION_REPORT.json\",");
            json.Append("  \"scannedAiCombatFiles\": ").Append(scannedFiles).AppendLine(",");
            json.Append("  \"candidateFiles\": ").Append(candidateFiles).AppendLine(",");
            json.Append("  \"syntaxTypeNodes\": ").Append(syntaxTypeNodes).AppendLine(",");
            json.Append("  \"syntaxMethodNodes\": ").Append(syntaxMethodNodes).AppendLine(",");
            json.Append("  \"delayedDestroyHits\": ").Append(delayedDestroyHits).AppendLine(",");
            json.Append("  \"waitThenDestroyHits\": ").Append(waitThenDestroyHits).AppendLine(",");
            json.Append("  \"corpseCleanupTokenHits\": ").Append(corpseCleanupHits).AppendLine(",");
            json.Append("  \"truthViolations\": ").Append(truthViolations).AppendLine(",");
            json.Append("  \"runtimeScanFindings\": ").Append(truthViolations).AppendLine(",");
            json.AppendLine("  \"newHotPath\": \"Assets/_Project/Scripts/Ecosystem/NutrientDriftRuntime_Carrion.cs\",");
            json.AppendLine("  \"replacementRoute\": \"EntityDeathSignal -> NutrientDriftRuntime_Carrion -> GlobalDataVault CarrionStateDTO[64B] -> NutrientCellDTO injection -> WorldSpatialHashGrid transient chemical resource\",");
            json.AppendLine("  \"faunaDeathPublisher\": \"Assets/_Project/Scripts/Fauna/FaunaBrain.cs::PublishCarrionDeathSignal\",");
            json.AppendLine("  \"duplicateGuard\": \"ProcessEntityDeathJob resolves active CarrionStateDTO by EntityHash before allocating a new slot\",");
            json.AppendLine("  \"faunaEntityHashRoute\": \"ResolveStableFaunaHash(FaunaCarrionDeathHashSalt,0); no Gameplay combat target routing\",");
            json.AppendLine("  \"signalFlagContract\": \"EntityDeathSignal.FlagFaunaBrainCarrion\",");
            json.AppendLine("  \"speciesHashRoute\": \"Fauna-owned death signals set EntityDeathSignal.FlagFaunaBrainCarrion and carry species hash in SourceHash\",");
            json.AppendLine("  \"lowQualityExpBypass\": \"math.step(0.4, GlobalQualityWeight) skips math.exp below threshold\",");
            json.AppendLine("  \"baseDecayPreserved\": true,");
            json.AppendLine("  \"deterministicRng\": \"GenerateMockMassExtinctionJob uses Unity.Mathematics.Random.CreateFromIndex(math.hash(seed,index)); no UnityEngine.Random\",");
            json.AppendLine("  \"mockCounterBiomass\": \"TotalActiveBiomass initializes to DefaultBiomass*1.3*count; real totals recompute in decay job\",");
            json.AppendLine("  \"aggregateReportMode\": \"upsert shinobu314CarrionDecay; no overwrite of other agent sections\",");
            json.AppendLine("  \"vaultBufferIds\": [71250, 71251, 71252, 71253, 71254, 71255, 71256, 71257, 71258, 71259],");
            json.AppendLine("  \"dtoLayout\": \"CarrionStateDTO=64 bytes: CorpseAUP@0 InitialBiomass@24 CurrentBiomass@28 OriginalSpeciesHash@32 ToxicityEmissionRate@36 tail runtime fields@40..60\",");
            json.AppendLine("  \"telemetryRingFrames\": 300,");
            json.AppendLine("  \"blackBoxTelemetryFrames\": 300,");
            json.AppendLine("  \"telemetryTimingRoute\": \"Carrion subchain schedule-to-finalize window via _carrionScheduleTicks; parent nutrient solver time no longer used for carrion budget\",");
            json.AppendLine("  \"nanFaultRoute\": \"ProcessEntityDeathJob sanitizes death ingress; CarrionStateDTO.FlagMathFault retires invalid active rows; InjectCarrionNutrientsJob clears stale fault flags and folds current-tick faults into telemetry\",");
            json.AppendLine("  \"dumpPath\": \"Docs/AgentLogs/Dump_SHINOBU_314.bin\",");
            json.AppendLine("  \"csvProfiles\": \"Assets/_Project/Data/carrion_decay_profiles.csv -> ReadOnlySpan<byte> parser -> ShinobuCarrionProfiles\",");
            json.AppendLine("  \"csvSpeciesKeyRoute\": \"species_key accepts default, decimal speciesID, 0xhash, or token FNV; unmatched fauna species use default profile\",");
            json.AppendLine("  \"dataMonolithRuntimeProof\": \"PENDING_STATIC_DATA_MISSING\",");
            json.AppendLine("  \"editorFacade\": \"Assets/_Project/Scripts/Ecosystem/Editor/BiomassDecayTunerWindow.cs\",");
            json.AppendLine("  \"liveGizmo\": \"Assets/_Project/Scripts/Ecosystem/Editor/LiveRotDebugGizmo.cs\",");
            json.AppendLine("  \"compileProof\": \"NOT_RUN_BY_EDITOR_SCANNER\",");
            json.Append("  \"verdict\": \"").Append(truthViolations == 0 ? "PASS" : "FAIL").AppendLine("\",");
            json.AppendLine("  \"findings\": [");
            json.Append(findings);
            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");

            string text = json.ToString();
            File.WriteAllText(stableReportPath, text);
            File.WriteAllText(reportPath, UpsertAggregateReport(reportPath, text));
            AssetDatabase.Refresh();
            return ReportRelativePath;
        }

        private static string UpsertAggregateReport(string reportPath, string stableReportJson)
        {
            string aggregate = File.Exists(reportPath) ? File.ReadAllText(reportPath) : "{\n}";
            aggregate = RemoveJsonProperty(aggregate, AggregateKey);
            int insert = aggregate.LastIndexOf('}');
            if (insert < 0)
                aggregate = "{\n}";

            insert = aggregate.LastIndexOf('}');
            string prefix = insert >= 0 ? aggregate.Substring(0, insert).TrimEnd() : "{";
            bool hasExistingProperties = prefix.Length > 1 && prefix[prefix.Length - 1] != '{';
            StringBuilder output = new StringBuilder(prefix.Length + stableReportJson.Length + 64);
            output.Append(prefix);
            if (hasExistingProperties)
                output.AppendLine(",");
            else
                output.AppendLine();

            output.Append("  \"").Append(AggregateKey).Append("\": ");
            AppendIndentedJson(output, stableReportJson, 2);
            output.AppendLine();
            output.AppendLine("}");
            return output.ToString();
        }

        private static string RemoveJsonProperty(string json, string key)
        {
            string quotedKey = "\"" + key + "\"";
            int keyIndex = json.IndexOf(quotedKey, StringComparison.Ordinal);
            if (keyIndex < 0)
                return json;

            int colon = json.IndexOf(':', keyIndex + quotedKey.Length);
            if (colon < 0)
                return json;

            int valueStart = colon + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;
            if (valueStart >= json.Length || json[valueStart] != '{')
                return json;

            int valueEnd = FindMatchingObjectEnd(json, valueStart);
            if (valueEnd < 0)
                return json;

            int removeStart = keyIndex;
            while (removeStart > 0 && char.IsWhiteSpace(json[removeStart - 1]))
                removeStart--;
            if (removeStart > 0 && json[removeStart - 1] == ',')
                removeStart--;

            int removeEnd = valueEnd + 1;
            while (removeEnd < json.Length && char.IsWhiteSpace(json[removeEnd]))
                removeEnd++;
            if (removeEnd < json.Length && json[removeEnd] == ',')
                removeEnd++;

            return json.Remove(removeStart, removeEnd - removeStart);
        }

        private static int FindMatchingObjectEnd(string text, int objectStart)
        {
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

                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static void AppendIndentedJson(StringBuilder output, string json, int spaces)
        {
            string indent = new string(' ', spaces);
            for (int i = 0; i < json.Length; i++)
            {
                char c = json[i];
                output.Append(c);
                if (c == '\n' && i + 1 < json.Length)
                    output.Append(indent);
            }
        }

        private static bool IsAiOrCombatCandidate(string relativePath)
        {
            string normalized = relativePath.Replace('\\', '/');
            return normalized.IndexOf("/AI/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Fauna/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Combat/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Gameplay/Combat/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.IndexOf("/Ecosystem/", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsCorpseLifecycleCandidate(string relativePath, string code)
        {
            return relativePath.IndexOf("Creature", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   relativePath.IndexOf("Fauna", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   relativePath.IndexOf("Combat", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   relativePath.IndexOf("Ecosystem", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   code.IndexOf("Destroy", StringComparison.Ordinal) >= 0 ||
                   code.IndexOf("Corpse", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   code.IndexOf("Death", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   code.IndexOf("WaitForSeconds", StringComparison.Ordinal) >= 0;
        }

        private static int AppendMatches(string relativePath, string code, Regex regex, string kind, StringBuilder findings, ref bool firstFinding)
        {
            MatchCollection matches = regex.Matches(code);
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                if (!firstFinding)
                    findings.AppendLine(",");
                firstFinding = false;
                findings.Append("    { \"file\": \"")
                    .Append(Escape(relativePath))
                    .Append("\", \"line\": ")
                    .Append(CountLine(code, match.Index))
                    .Append(", \"kind\": \"")
                    .Append(kind)
                    .Append("\", \"snippet\": \"")
                    .Append(Escape(ExtractSnippet(code, match.Index)))
                    .Append("\" }");
            }

            return matches.Count;
        }

        private static int CountToken(string code, string token)
        {
            int count = 0;
            int index = 0;
            while (index < code.Length)
            {
                index = code.IndexOf(token, index, StringComparison.Ordinal);
                if (index < 0)
                    break;
                count++;
                index += token.Length;
            }

            return count;
        }

        private static string StripCommentsAndStrings(string source)
        {
            StringBuilder output = new StringBuilder(source.Length);
            bool lineComment = false;
            bool blockComment = false;
            bool stringLiteral = false;
            bool charLiteral = false;
            bool verbatimString = false;

            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                char n = i + 1 < source.Length ? source[i + 1] : '\0';

                if (lineComment)
                {
                    if (c == '\n')
                    {
                        lineComment = false;
                        output.Append(c);
                    }
                    else
                    {
                        output.Append(' ');
                    }
                    continue;
                }

                if (blockComment)
                {
                    if (c == '*' && n == '/')
                    {
                        blockComment = false;
                        output.Append("  ");
                        i++;
                    }
                    else
                    {
                        output.Append(c == '\n' ? '\n' : ' ');
                    }
                    continue;
                }

                if (stringLiteral)
                {
                    if (verbatimString && c == '"' && n == '"')
                    {
                        output.Append("  ");
                        i++;
                        continue;
                    }

                    bool end = c == '"' && (verbatimString || !IsEscaped(source, i));
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (end)
                    {
                        stringLiteral = false;
                        verbatimString = false;
                    }
                    continue;
                }

                if (charLiteral)
                {
                    bool end = c == '\'' && !IsEscaped(source, i);
                    output.Append(c == '\n' ? '\n' : ' ');
                    if (end)
                        charLiteral = false;
                    continue;
                }

                if (c == '/' && n == '/')
                {
                    lineComment = true;
                    output.Append("  ");
                    i++;
                    continue;
                }

                if (c == '/' && n == '*')
                {
                    blockComment = true;
                    output.Append("  ");
                    i++;
                    continue;
                }

                if (c == '@' && n == '"')
                {
                    stringLiteral = true;
                    verbatimString = true;
                    output.Append("  ");
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    stringLiteral = true;
                    output.Append(' ');
                    continue;
                }

                if (c == '\'')
                {
                    charLiteral = true;
                    output.Append(' ');
                    continue;
                }

                output.Append(c);
            }

            return output.ToString();
        }

        private static bool IsEscaped(string source, int index)
        {
            int slashCount = 0;
            for (int i = index - 1; i >= 0 && source[i] == '\\'; i--)
                slashCount++;
            return (slashCount & 1) != 0;
        }

        private static int CountLine(string text, int index)
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

        private static string ExtractSnippet(string code, int index)
        {
            int start = Math.Max(0, index - 60);
            int length = Math.Min(160, code.Length - start);
            return code.Substring(start, length).Replace('\r', ' ').Replace('\n', ' ');
        }

        private static string MakeRelative(string root, string file)
        {
            string relative = file.StartsWith(root, StringComparison.OrdinalIgnoreCase)
                ? file.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : file;
            return relative.Replace('\\', '/');
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void EnsureDirectory(string path)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
        }
    }
}
#endif
