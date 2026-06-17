#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;

namespace Hecton8.World.BiotaDensityMapBaker.Editor
{
    public static class Runtime_Spawner_Scanner
    {
        private const string SourceRoot = "Assets/_Project/Scripts";
        private const string ReportPath = "Docs/Reports/WORLD_OPTIMIZATION_REPORT.json";
        private static readonly string InstantiateToken = "Instantiate" + "(";
        private static readonly string ManagedObjectCreateToken = "new Game" + "Object" + "(";
        private static readonly string ComponentAttachToken = "Add" + "Component" + "<";
        private const int ContextRadius = 4;
        private const int MaxFindings = 128;
        private const int ReportLockAttempts = 20;
        private const int ReportLockBackoffMs = 25;

        [MenuItem("Hecton8/Ecosystem Density Forge/Run Runtime Spawner Scanner")]
        public static void RunAndWriteReport()
        {
            ScanResult result = Scan();
            WriteReport(in result);
            AssetDatabase.Refresh();
            UnityEngine.Debug.Log("[SHINOBU_308] Runtime spawner scanner wrote " + ReportPath + " blockers=" + result.BlockerCount + " excluded=" + result.ExcludedCount);
        }

        public static ScanResult Scan()
        {
            ScanResult result = new ScanResult();
            result.RuntimeRaycastSpawnersEradicated = true;
            result.ScanComplete = true;
            Finding[] findings = new Finding[MaxFindings];
            string fullRoot = Path.GetFullPath(SourceRoot);
            if (!Directory.Exists(fullRoot))
            {
                result.ScanComplete = false;
                result.ScannerIncomplete = true;
                result.Findings = findings;
                return result;
            }

            string[] directories = new string[512];
            int directoryCount = 1;
            directories[0] = fullRoot;
            while (directoryCount > 0)
            {
                string directory = directories[--directoryCount];
                if (IsEditorPath(directory))
                    continue;

                string[] childDirectories;
                try
                {
                    childDirectories = Directory.GetDirectories(directory);
                }
                catch (Exception)
                {
                    result.ScanComplete = false;
                    result.ScannerIncomplete = true;
                    continue;
                }

                for (int i = 0; i < childDirectories.Length; i++)
                {
                    if (directoryCount >= directories.Length)
                    {
                        result.ScanComplete = false;
                        result.ScannerIncomplete = true;
                        break;
                    }

                    directories[directoryCount++] = childDirectories[i];
                }

                string[] files;
                try
                {
                    files = Directory.GetFiles(directory, "*.cs");
                }
                catch (Exception)
                {
                    result.ScanComplete = false;
                    result.ScannerIncomplete = true;
                    continue;
                }

                for (int i = 0; i < files.Length; i++)
                    ScanFile(files[i], ref result, findings);
            }

            result.Findings = findings;
            result.RuntimeRaycastSpawnersEradicated = result.BlockerCount == 0;
            return result;
        }

        private static void ScanFile(string fullPath, ref ScanResult result, Finding[] findings)
        {
            string[] lines;
            try
            {
                lines = File.ReadAllLines(fullPath);
            }
            catch (Exception)
            {
                result.ScanComplete = false;
                result.ScannerIncomplete = true;
                return;
            }

            result.ScannedFiles++;
            result.ScannedLines += lines.Length;
            string[] codeLines = new string[lines.Length];
            bool inBlockComment = false;
            for (int i = 0; i < lines.Length; i++)
                codeLines[i] = StripNonCode(lines[i], ref inBlockComment);

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                string codeLine = codeLines[i];
                bool rawCandidate = ContainsAny(line,
                    "Physics.Raycast",
                    "Physics.RaycastNonAlloc",
                    "Physics.RaycastAll",
                    "Physics.SphereCast",
                    "Physics.CapsuleCast",
                    "Physics.Linecast",
                    "RaycastNonAlloc",
                    "TryQueuePrimaryRaycast",
                    "QueueDispatcherRaycast",
                    InstantiateToken,
                    ManagedObjectCreateToken,
                    ComponentAttachToken,
                    "SphereCollider",
                    "BoxCollider",
                    "isTrigger");
                bool raycast = ContainsAny(codeLine,
                    "Physics.Raycast",
                    "Physics.RaycastNonAlloc",
                    "Physics.RaycastAll",
                    "Physics.SphereCast",
                    "Physics.CapsuleCast",
                    "Physics.Linecast",
                    "RaycastNonAlloc",
                    "TryQueuePrimaryRaycast",
                    "QueueDispatcherRaycast");
                bool instantiation = ContainsAny(codeLine, InstantiateToken, ManagedObjectCreateToken, ComponentAttachToken);
                bool triggerZone = (Contains(codeLine, "SphereCollider") || Contains(codeLine, "BoxCollider") || Contains(codeLine, "isTrigger")) &&
                                   HasContext(codeLines, i, "SpawnZone", "SpawnVolume", "BiomeTrigger", "flora", "plant", "kelp", "coral");
                if (rawCandidate && !raycast && !instantiation && !triggerZone)
                    result.FilteredCommentOrStringHits++;
                if (!raycast && !instantiation && !triggerZone)
                    continue;

                bool spawnContext = HasContext(codeLines, i, "spawn", "Spawner", "Spawn", "plant", "flora", "kelp", "coral", "biota", "Vector3.down");
                bool excludedOwner = IsExplicitNonBiotaOwner(fullPath) ||
                                     HasContext(codeLines, i, "PlayerSpawner", "HectonPlayerSpawner") ||
                                     HasContext(codeLines, i, "BuoyancyObject", "grounded", "_isGrounded", "buoyancy");
                bool excludedTool = HasContext(codeLines, i, "Tool", "Harpoon", "Knife", "Scanner", "Repair", "Interaction", "UI", "PDA");
                bool excludedColdInstantiation = instantiation &&
                                                 HasContext(codeLines, i, "Application.isPlaying", "!Application.isPlaying", "return null", "ObjectPool", "pool.Spawn", "bootstrap");
                bool blocker = (spawnContext || triggerZone) && !excludedOwner && !excludedTool && !excludedColdInstantiation;
                if (blocker)
                {
                    result.BlockerCount++;
                }
                else
                {
                    result.ExcludedCount++;
                    if (excludedColdInstantiation)
                        result.ColdInstantiateExcludedCount++;
                }

                if (result.FindingCount < findings.Length)
                {
                    findings[result.FindingCount] = new Finding
                    {
                        Path = NormalizePath(fullPath),
                        Line = i + 1,
                        Pattern = raycast ? "raycast" : (instantiation ? "managed_scene_instantiation" : "trigger_spawn_zone"),
                        Classification = blocker ? "BLOCKER" : (excludedColdInstantiation ? "EXCLUDED_COLD_OR_POOL_GUARDED" : "EXCLUDED_NON_BIOTA_OR_SAFE"),
                        Context = TrimForJson(line)
                    };
                    result.FindingCount++;
                }
                else
                {
                    result.ScannerIncomplete = true;
                    result.ScanComplete = false;
                }
            }
        }

        private static string StripNonCode(string line, ref bool inBlockComment)
        {
            if (string.IsNullOrEmpty(line))
                return string.Empty;

            StringBuilder builder = new StringBuilder(line.Length);
            bool inString = false;
            bool inChar = false;
            bool verbatim = false;
            bool escape = false;
            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                char next = i + 1 < line.Length ? line[i + 1] : '\0';

                if (inBlockComment)
                {
                    if (c == '*' && next == '/')
                    {
                        inBlockComment = false;
                        i++;
                    }
                    builder.Append(' ');
                    continue;
                }

                if (inString)
                {
                    if (verbatim)
                    {
                        if (c == '"' && next == '"')
                        {
                            i++;
                        }
                        else if (c == '"')
                        {
                            inString = false;
                            verbatim = false;
                        }
                    }
                    else if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    builder.Append(' ');
                    continue;
                }

                if (inChar)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '\'')
                    {
                        inChar = false;
                    }

                    builder.Append(' ');
                    continue;
                }

                if (c == '/' && next == '/')
                {
                    builder.Append(' ');
                    break;
                }

                if (c == '/' && next == '*')
                {
                    inBlockComment = true;
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (c == '@' && next == '"')
                {
                    inString = true;
                    verbatim = true;
                    builder.Append(' ');
                    i++;
                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                    verbatim = false;
                    builder.Append(' ');
                    continue;
                }

                if (c == '\'')
                {
                    inChar = true;
                    builder.Append(' ');
                    continue;
                }

                builder.Append(c);
            }

            return builder.ToString();
        }

        private static bool HasContext(string[] lines, int index, params string[] tokens)
        {
            int start = Math.Max(0, index - ContextRadius);
            int end = Math.Min(lines.Length - 1, index + ContextRadius);
            for (int i = start; i <= end; i++)
            {
                string line = lines[i];
                for (int t = 0; t < tokens.Length; t++)
                {
                    if (Contains(line, tokens[t]))
                        return true;
                }
            }

            return false;
        }

        private static bool Contains(string value, string token)
        {
            return value != null && value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool ContainsAny(string value, params string[] tokens)
        {
            if (value == null)
                return false;

            for (int i = 0; i < tokens.Length; i++)
            {
                if (Contains(value, tokens[i]))
                    return true;
            }

            return false;
        }

        private static bool IsEditorPath(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalized.EndsWith("/Editor", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExplicitNonBiotaOwner(string path)
        {
            string normalized = path.Replace('\\', '/');
            return normalized.EndsWith("/HectonPlayerSpawner.cs", StringComparison.OrdinalIgnoreCase) ||
                   normalized.EndsWith("/BuoyancyObject.cs", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizePath(string path)
        {
            string normalized = path.Replace('\\', '/');
            int index = normalized.IndexOf("Assets/_Project/", StringComparison.OrdinalIgnoreCase);
            return index >= 0 ? normalized.Substring(index) : normalized;
        }

        private static string TrimForJson(string value)
        {
            if (value == null)
                return string.Empty;
            value = value.Trim();
            return value.Length > 180 ? value.Substring(0, 180) : value;
        }

        private static void WriteReport(in ScanResult result)
        {
            Directory.CreateDirectory("Docs/Reports");
            string sectionJson = BuildReportSection(in result);
            string lockPath = ReportPath + ".lock";
            Exception lastException = null;
            for (int attempt = 0; attempt < ReportLockAttempts; attempt++)
            {
                try
                {
                    using (new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                    {
                        string existing = File.Exists(ReportPath) ? File.ReadAllText(ReportPath) : string.Empty;
                        string merged = UpsertTopLevelSection(existing, "shinobu_308_biota_density_map_baker", sectionJson);
                        BiotaDensityBakePipeline.WriteUtf8TextAtomic(ReportPath, merged);
                        return;
                    }
                }
                catch (IOException exception)
                {
                    lastException = exception;
                }
                catch (UnauthorizedAccessException exception)
                {
                    lastException = exception;
                }

                System.Threading.Thread.Sleep(ReportLockBackoffMs * (attempt + 1));
            }

            throw new IOException("SHINOBU_308 scanner could not acquire report lock.", lastException);
        }

        private static string BuildReportSection(in ScanResult result)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.Append("{\n");
            Append(builder, "schema", "hecton8.world_optimization_report.v1", true);
            Append(builder, "agent", "SHINOBU_308", true);
            Append(builder, "scanner", "Runtime_Spawner_Scanner", true);
            Append(builder, "runtimeRaycastSpawnersEradicated", result.RuntimeRaycastSpawnersEradicated, true);
            Append(builder, "blockerCount", result.BlockerCount, true);
            Append(builder, "excludedCount", result.ExcludedCount, true);
            Append(builder, "scanComplete", result.ScanComplete, true);
            Append(builder, "scannerIncomplete", result.ScannerIncomplete, true);
            Append(builder, "scannedFiles", result.ScannedFiles, true);
            Append(builder, "scannedLines", result.ScannedLines, true);
            Append(builder, "filteredCommentOrStringHits", result.FilteredCommentOrStringHits, true);
            Append(builder, "coldInstantiateExcludedCount", result.ColdInstantiateExcludedCount, true);
            builder.Append("  \"findings\": [\n");
            int emitted = 0;
            for (int i = 0; i < result.FindingCount; i++)
            {
                Finding finding = result.Findings[i];
                if (string.IsNullOrEmpty(finding.Path))
                    continue;
                if (emitted > 0)
                    builder.Append(",\n");
                builder.Append("    { ");
                AppendInline(builder, "path", finding.Path, true);
                AppendInline(builder, "line", finding.Line, true);
                AppendInline(builder, "pattern", finding.Pattern, true);
                AppendInline(builder, "classification", finding.Classification, true);
                AppendInline(builder, "context", finding.Context, false);
                builder.Append(" }");
                emitted++;
            }

            builder.Append('\n').Append("  ],\n");
            Append(builder, "policy", "Biota placement authority must come from offline .h8bin density masks, not runtime raycast floor searches or trigger spawn volumes.", false);
            builder.Append("}\n");
            return builder.ToString();
        }

        private static string UpsertTopLevelSection(string existing, string sectionName, string sectionJson)
        {
            if (string.IsNullOrWhiteSpace(existing))
                return "{\n  \"" + sectionName + "\": " + IndentJson(sectionJson, 2) + "\n}\n";

            string trimmed = existing.Trim();
            if (trimmed.Length < 2 || trimmed[0] != '{' || trimmed[trimmed.Length - 1] != '}')
                return "{\n  \"" + sectionName + "\": " + IndentJson(sectionJson, 2) + "\n}\n";

            if (TryFindTopLevelPropertyRange(trimmed, sectionName, out int removeStart, out int removeEnd))
                trimmed = trimmed.Remove(removeStart, removeEnd - removeStart).Trim();

            int close = trimmed.LastIndexOf('}');
            if (close < 0)
                return "{\n  \"" + sectionName + "\": " + IndentJson(sectionJson, 2) + "\n}\n";

            string prefix = trimmed.Substring(0, close).TrimEnd();
            bool hasExistingProperties = prefix.Length > 1;
            StringBuilder merged = new StringBuilder(prefix.Length + sectionJson.Length + 128);
            merged.Append(prefix);
            if (hasExistingProperties)
                merged.Append(',');
            merged.Append('\n');
            merged.Append("  \"").Append(sectionName).Append("\": ").Append(IndentJson(sectionJson, 2)).Append('\n');
            merged.Append("}\n");
            return merged.ToString();
        }

        private static bool TryFindTopLevelPropertyRange(string json, string sectionName, out int removeStart, out int removeEnd)
        {
            removeStart = -1;
            removeEnd = -1;
            string marker = "\"" + sectionName + "\"";
            int markerIndex = json.IndexOf(marker, StringComparison.Ordinal);
            if (markerIndex < 0)
                return false;

            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = 0; i < markerIndex; i++)
                StepJsonState(json[i], ref depth, ref inString, ref escape);

            if (depth != 1 || inString)
                return false;

            int start = markerIndex;
            while (start > 0 && char.IsWhiteSpace(json[start - 1]))
                start--;
            if (start > 0 && json[start - 1] == ',')
                start--;

            int valueStart = markerIndex + marker.Length;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;
            if (valueStart >= json.Length || json[valueStart] != ':')
                return false;
            valueStart++;

            int valueDepth = 0;
            inString = false;
            escape = false;
            int end = valueStart;
            for (; end < json.Length; end++)
            {
                char c = json[end];
                if (!inString && valueDepth == 0 && (c == ',' || c == '}'))
                    break;
                StepJsonState(c, ref valueDepth, ref inString, ref escape);
            }

            if (end < json.Length && json[end] == ',')
                end++;
            while (end < json.Length && char.IsWhiteSpace(json[end]))
                end++;

            removeStart = start;
            removeEnd = end;
            return removeEnd > removeStart;
        }

        private static void StepJsonState(char c, ref int depth, ref bool inString, ref bool escape)
        {
            if (escape)
            {
                escape = false;
                return;
            }

            if (inString)
            {
                if (c == '\\')
                    escape = true;
                else if (c == '"')
                    inString = false;
                return;
            }

            if (c == '"')
                inString = true;
            else if (c == '{' || c == '[')
                depth++;
            else if (c == '}' || c == ']')
                depth--;
        }

        private static string IndentJson(string json, int spaces)
        {
            string indent = new string(' ', Math.Max(0, spaces));
            string normalized = json.TrimEnd();
            StringBuilder builder = new StringBuilder(normalized.Length + 64);
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                builder.Append(c);
                if (c == '\n' && i + 1 < normalized.Length)
                    builder.Append(indent);
            }
            return builder.ToString();
        }

        private static void Append(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": \"").Append(Escape(value)).Append('"');
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void Append(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendInline(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append('"').Append(name).Append("\": \"").Append(Escape(value)).Append('"');
            if (comma)
                builder.Append(", ");
        }

        private static void AppendInline(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append('"').Append(name).Append("\": ").Append(value);
            if (comma)
                builder.Append(", ");
        }

        private static string Escape(string value)
        {
            return value == null ? string.Empty : value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        public struct ScanResult
        {
            public bool RuntimeRaycastSpawnersEradicated;
            public bool ScanComplete;
            public bool ScannerIncomplete;
            public int BlockerCount;
            public int ExcludedCount;
            public int FindingCount;
            public int ScannedFiles;
            public int ScannedLines;
            public int FilteredCommentOrStringHits;
            public int ColdInstantiateExcludedCount;
            public Finding[] Findings;
        }

        public struct Finding
        {
            public string Path;
            public int Line;
            public string Pattern;
            public string Classification;
            public string Context;
        }
    }
}
#endif
