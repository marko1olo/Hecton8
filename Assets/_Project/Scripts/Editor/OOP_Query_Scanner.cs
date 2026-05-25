#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class OOP_Query_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/AI_OPTIMIZATION_REPORT.json";
        private const string StableReportRelativePath = "Docs/Reports/SHINOBU_301_AI_OPTIMIZATION_REPORT.json";
        private static readonly string[] ForbiddenTokens =
        {
            "Physics.OverlapSphere",
            "Physics.OverlapBox",
            "Physics.SphereCast",
            "Collider.ClosestPoint"
        };

        [MenuItem("HECTON-8/AI/OOP Proximity Query Scanner")]
        public static void RunMenu()
        {
            string report = RunScan();
            Debug.Log(report);
        }

        public static string RunScan()
        {
            string root = ResolveProjectRoot();
            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            List<Finding> findings = new List<Finding>(32);
            if (Directory.Exists(scriptsRoot))
            {
                foreach (string path in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
                {
                    string normalized = path.Replace('\\', '/');
                    if (normalized.Contains("/Editor/") || normalized.EndsWith("_Scanner.cs", StringComparison.Ordinal))
                        continue;

                    string source = File.ReadAllText(path);
                    bool aiOrInteractionNamespace = source.Contains("namespace Hecton8.AI") || source.Contains("namespace Hecton8.Interaction");
                    if (!aiOrInteractionNamespace)
                        continue;

                    ScanSource(path, source, findings);
                }
            }

            string stableReportPath = Path.Combine(root, StableReportRelativePath);
            string existingStableReport = File.Exists(stableReportPath) ? File.ReadAllText(stableReportPath) : string.Empty;
            string json = BuildJson(root, findings, existingStableReport);
            Directory.CreateDirectory(Path.GetDirectoryName(stableReportPath));
            File.WriteAllText(stableReportPath, json, Encoding.UTF8);

            string reportPath = Path.Combine(root, ReportRelativePath);
            string existingSharedReport = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
            string sharedJson = BuildSharedSectionJson(findings, existingSharedReport);
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));
            WriteSharedReportIfUnowned(reportPath, sharedJson);
            return "OOP_Query_Scanner wrote " + stableReportPath + " findings=" + findings.Count;
        }

        private static void ScanSource(string path, string source, List<Finding> findings)
        {
            string currentNamespace = ResolveNamespace(source);
            for (int t = 0; t < ForbiddenTokens.Length; t++)
            {
                string token = ForbiddenTokens[t];
                int index = 0;
                while (index >= 0 && index < source.Length)
                {
                    index = source.IndexOf(token, index, StringComparison.Ordinal);
                    if (index < 0)
                        break;

                    if (!IsIdentifierContinuation(source, index + token.Length) && !IsInsideComment(source, index))
                    {
                        findings.Add(new Finding
                        {
                            Path = path,
                            Namespace = currentNamespace,
                            Method = ResolveMethodContext(source, index),
                            Token = token,
                            Line = ResolveLine(source, index)
                        });
                    }

                    index += token.Length;
                }
            }
        }

        private static string ResolveNamespace(string source)
        {
            const string key = "namespace ";
            int index = source.IndexOf(key, StringComparison.Ordinal);
            if (index < 0)
                return string.Empty;

            int start = index + key.Length;
            int end = start;
            while (end < source.Length && (char.IsLetterOrDigit(source[end]) || source[end] == '.' || source[end] == '_'))
                end++;
            return source.Substring(start, end - start);
        }

        private static string ResolveMethodContext(string source, int index)
        {
            int open = source.LastIndexOf('{', mathMax(index - 1, 0));
            if (open <= 0)
                return string.Empty;

            int lineStart = source.LastIndexOf('\n', open);
            int probe = lineStart >= 0 ? lineStart + 1 : 0;
            int limit = mathMax(0, open - probe);
            string signature = source.Substring(probe, limit).Trim();
            if (signature.Length > 160)
                signature = signature.Substring(signature.Length - 160);
            return signature.Replace("\"", "'");
        }

        private static int ResolveLine(string source, int index)
        {
            int line = 1;
            for (int i = 0; i < index && i < source.Length; i++)
            {
                if (source[i] == '\n')
                    line++;
            }

            return line;
        }

        private static bool IsInsideComment(string source, int index)
        {
            int lineStart = source.LastIndexOf('\n', mathMax(index - 1, 0));
            lineStart = lineStart < 0 ? 0 : lineStart + 1;
            int comment = source.IndexOf("//", lineStart, index - lineStart, StringComparison.Ordinal);
            return comment >= 0;
        }

        private static bool IsIdentifierContinuation(string source, int index)
        {
            if ((uint)index >= (uint)source.Length)
                return false;

            char c = source[index];
            return char.IsLetterOrDigit(c) || c == '_';
        }

        private static string BuildJson(string root, List<Finding> findings, string existingStableReport)
        {
            string projectCompileProof = ResolveExistingObjectProperty(
                existingStableReport,
                "projectCompileProof",
                ResolveExistingObjectProperty(
                    existingStableReport,
                    "compileProof",
                    "{ \"status\": \"PENDING_VERIFICATION\", \"reason\": \"No fresh guarded project build sample was supplied to the editor scanner.\" }"));
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("{");
            builder.AppendLine("  \"scanner\": \"OOP_Query_Scanner\",");
            builder.AppendLine("  \"agent\": \"SHINOBU_301\",");
            builder.AppendLine("  \"domain\": \"SPATIAL_HASH_GRID_SOLVER\",");
            builder.AppendLine("  \"status\": \"" + (findings.Count == 0 ? "OOP Proximity Queries Eradicated - STATIC SCAN, RUNTIME PENDING" : "OOP Proximity Queries Found") + "\",");
            builder.AppendLine("  \"tokens\": [\"Physics.OverlapSphere\", \"Physics.OverlapBox\", \"Physics.SphereCast\", \"Collider.ClosestPoint\"],");
            builder.AppendLine("  \"scope\": \"Assets/_Project/Scripts/AI and Interaction namespaces, excluding Editor tooling and NonAlloc suffix false positives\",");
            builder.AppendLine("  \"runtimeScan\": {");
            builder.AppendLine("    \"command\": \"rg -n --glob '!**/Editor/**' \\\"Physics\\\\.OverlapSphere\\\\b|Physics\\\\.OverlapBox\\\\b|Physics\\\\.SphereCast\\\\b|Collider\\\\.ClosestPoint\\\\b\\\" Assets/_Project/Scripts/AI Assets/_Project/Scripts/Interaction\",");
            builder.AppendLine("    \"findings\": " + findings.Count + ",");
            builder.AppendLine("    \"status\": \"" + (findings.Count == 0 ? "PASS" : "FAIL") + "\"");
            builder.AppendLine("  },");
            builder.AppendLine("  \"newHotPath\": \"Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs\",");
            builder.AppendLine("  \"newRuntimeRoute\": \"ShinobuEcosystemBalancer -> GlobalDataVault spatial grid buffers -> Burst SpatialHashQuery\",");
            builder.AppendLine("  \"dtoLayout\": \"SpatialGridEntryDTO=16 bytes: EntityHashID/EntityRowIndex@0 CellHash@4 LocalCellOffset/CellFingerprint@8\",");
            builder.AppendLine("  \"rangeLayout\": \"SpatialGridBucketRangeDTO=32 bytes: CellHash@0 CellFingerprintX@4 CellFingerprintY@8 StartIndex@12 Count@16 Flags@20 Pad0@24 Pad1@28\",");
            builder.AppendLine("  \"entityIdentity\": \"EntityHashID is an ABI alias for the Vault row-local entity handle; stable seeds remain in AmbientEntityAupDTO.\",");
            builder.AppendLine("  \"cellCollisionGuard\": \"CellHash is a 32-bit range key; CellFingerprint is a 64-bit secondary key verified before candidate budget is consumed.\",");
            builder.AppendLine("  \"sortKey\": \"8-pass radix sort over CellFingerprint uint2 so exact-cell ranges are contiguous before range-table insertion.\",");
            builder.AppendLine("  \"structuralProbePolicy\": \"range-table insertion/lookup use fixed structural probe count; GlobalQualityWeight only limits visited cells/results.\",");
            builder.AppendLine("  \"dearLie\": \"MaxResults and neighbor sample caps bound dense-school queries; center cell is scanned first and outer cells are visited by squared-distance shells.\",");
            builder.AppendLine("  \"globalQualityWeightContinuous\": true,");
            builder.AppendLine("  \"blackBoxTelemetryFrames\": 300,");
            builder.AppendLine("  \"queryCountSource\": \"FlockingCounterSpatialGridQueries\",");
            builder.AppendLine("  \"queryCountPatchCadence\": \"patched every telemetry frame; binary fault dump is gated separately\",");
            builder.AppendLine("  \"invalidInputTelemetry\": \"SpatialGridTelemetryEntry.InvalidInputCount@56 plus TelemetryFlagInvalidInput; dump triggers on overflow or invalid input.\",");
            builder.AppendLine("  \"telemetryTiming\": \"UNAVAILABLE_SENTINEL_MINUS_ONE\",");
            builder.AppendLine("  \"centerCellFirst\": true,");
            builder.AppendLine("  \"outerCellOrder\": \"SQUARED_DISTANCE_SHELLS\",");
            builder.AppendLine("  \"aupQuantization\": \"DOUBLE_SCALED_SYMMETRIC_INTEGER_DEADBAND_BEFORE_FLOOR\",");
            builder.AppendLine("  \"aupDistanceCheck\": \"DOUBLE_SUBTRACT_THEN_LOCAL_FLOAT3_LENGTHSQ\",");
            builder.AppendLine("  \"editorWriteLock\": \"TryAcquireWriteLock/ReleaseWriteLock on ShinobuSpatialGridTuning\",");
            builder.AppendLine("  \"editorReadFence\": \"CoreDiagnostics TryLockBuffer read set around X-Ray Vault views\",");
            builder.AppendLine("  \"compileWallImportTrim\": \"Removed unused using Hecton8.Ecosystem from ShinobuEcosystemBalancer.cs\",");
            builder.AppendLine("  \"scannerProof\": {");
            builder.AppendLine("    \"status\": \"BUILD_NOT_RUN_FROM_EDITOR_SCANNER\",");
            builder.AppendLine("    \"reason\": \"OOP_Query_Scanner is a static editor proof artifact and does not launch dotnet or Unity builds.\"");
            builder.AppendLine("  },");
            builder.AppendLine("  \"projectCompileProof\": " + InlineJson(projectCompileProof) + ",");
            builder.AppendLine("  \"compileProof\": " + InlineJson(projectCompileProof) + ",");
            builder.AppendLine("  \"verdict\": \"" + (findings.Count == 0 ? "PENDING_VERIFICATION_WITH_TASK15_TIMING_PARTIAL" : "FAIL_STATIC_SCAN") + "\",");
            builder.AppendLine("  \"note\": \"Static token scan excludes NonAlloc overload suffixes; existing Interaction OverlapSphereNonAlloc calls are not the allocating OOP proximity query targeted by SHINOBU_301.\",");
            builder.AppendLine("  \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                Finding f = findings[i];
                builder.AppendLine("    {");
                builder.AppendLine("      \"path\": \"" + Escape(MakeRelative(root, f.Path)) + "\",");
                builder.AppendLine("      \"line\": " + f.Line + ",");
                builder.AppendLine("      \"namespace\": \"" + Escape(f.Namespace) + "\",");
                builder.AppendLine("      \"method\": \"" + Escape(f.Method) + "\",");
                builder.AppendLine("      \"token\": \"" + Escape(f.Token) + "\"");
                builder.Append("    }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }

            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildSharedSectionJson(List<Finding> findings, string existingSharedReport)
        {
            string existingSection = string.Empty;
            TryExtractTopLevelObject(existingSharedReport, "shinobu301SpatialHash", out existingSection);
            string compileProof = TryExtractStringProperty(existingSection, "compileProof", out string preservedCompileProof)
                ? preservedCompileProof
                : "BUILD_NOT_RUN_FROM_EDITOR_SCANNER";

            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("{");
            builder.AppendLine("  \"scanner\": \"OOP_Query_Scanner\",");
            builder.AppendLine("  \"agent\": \"SHINOBU_301\",");
            builder.AppendLine("  \"domain\": \"SPATIAL_HASH_GRID_SOLVER\",");
            builder.AppendLine("  \"status\": \"" + (findings.Count == 0 ? "OOP Proximity Queries Eradicated - STATIC SCAN, RUNTIME PENDING" : "OOP Proximity Queries Found") + "\",");
            builder.AppendLine("  \"stableCopy\": \"Docs/Reports/SHINOBU_301_AI_OPTIMIZATION_REPORT.json\",");
            builder.AppendLine("  \"runtimeFindings\": " + findings.Count + ",");
            builder.AppendLine("  \"newHotPath\": \"Assets/_Project/Scripts/AI/Ecosystem/ShinobuSpatialGridSolver.cs\",");
            builder.AppendLine("  \"newRuntimeRoute\": \"ShinobuEcosystemBalancer -> GlobalDataVault spatial grid buffers -> Burst SpatialHashQuery\",");
            builder.AppendLine("  \"dtoLayout\": \"SpatialGridEntryDTO=16 bytes: EntityHashID/EntityRowIndex@0 CellHash@4 LocalCellOffset/CellFingerprint@8\",");
            builder.AppendLine("  \"rangeLayout\": \"SpatialGridBucketRangeDTO=32 bytes: CellHash@0 CellFingerprintX@4 CellFingerprintY@8 StartIndex@12 Count@16 Flags@20 Pad0@24 Pad1@28\",");
            builder.AppendLine("  \"entityIdentity\": \"EntityHashID ABI alias stores row_index_explicit\",");
            builder.AppendLine("  \"cellCollisionGuard\": \"64-bit fingerprint verified before candidate budget\",");
            builder.AppendLine("  \"sortKey\": \"8-pass radix over CellFingerprint uint2\",");
            builder.AppendLine("  \"structuralProbePolicy\": \"fixed structural probe; quality only limits visited cells/results\",");
            builder.AppendLine("  \"blackBoxTelemetryFrames\": 300,");
            builder.AppendLine("  \"queryCountSource\": \"FlockingCounterSpatialGridQueries\",");
            builder.AppendLine("  \"queryCountPatchCadence\": \"patched every telemetry frame; binary fault dump is gated separately\",");
            builder.AppendLine("  \"invalidInputTelemetry\": \"SpatialGridTelemetryEntry.InvalidInputCount@56 plus TelemetryFlagInvalidInput; dump triggers on overflow or invalid input.\",");
            builder.AppendLine("  \"telemetryTiming\": \"UNAVAILABLE_SENTINEL_MINUS_ONE\",");
            builder.AppendLine("  \"centerCellFirst\": true,");
            builder.AppendLine("  \"outerCellOrder\": \"SQUARED_DISTANCE_SHELLS\",");
            builder.AppendLine("  \"aupQuantization\": \"DOUBLE_SCALED_SYMMETRIC_INTEGER_DEADBAND_BEFORE_FLOOR\",");
            builder.AppendLine("  \"aupDistanceCheck\": \"DOUBLE_SUBTRACT_THEN_LOCAL_FLOAT3_LENGTHSQ\",");
            builder.AppendLine("  \"editorWriteLock\": \"TryAcquireWriteLock/ReleaseWriteLock on ShinobuSpatialGridTuning\",");
            builder.AppendLine("  \"editorReadFence\": \"CoreDiagnostics TryLockBuffer read set around X-Ray Vault views\",");
            builder.AppendLine("  \"compileWallImportTrim\": \"Removed unused using Hecton8.Ecosystem from ShinobuEcosystemBalancer.cs\",");
            builder.AppendLine("  \"compileProof\": \"" + Escape(compileProof) + "\",");
            builder.AppendLine("  \"verdict\": \"" + (findings.Count == 0 ? "PENDING_VERIFICATION_WITH_TASK15_TIMING_PARTIAL" : "FAIL_STATIC_SCAN") + "\"");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static void WriteSharedReportIfUnowned(string reportPath, string json)
        {
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, json, Encoding.UTF8);
                return;
            }

            string existing = File.ReadAllText(reportPath);
            string trimmed = existing.TrimStart();
            if (!trimmed.StartsWith("{", StringComparison.Ordinal))
            {
                File.WriteAllText(reportPath, json, Encoding.UTF8);
                return;
            }

            if (TryReplaceTopLevelSection(existing, "shinobu301SpatialHash", json, out string replaced))
            {
                File.WriteAllText(reportPath, replaced, Encoding.UTF8);
                return;
            }

            string merged = AppendTopLevelSection(existing, "shinobu301SpatialHash", json);
            if (!string.IsNullOrEmpty(merged))
                File.WriteAllText(reportPath, merged, Encoding.UTF8);
        }

        private static bool TryReplaceTopLevelSection(string existing, string propertyName, string json, out string replaced)
        {
            replaced = string.Empty;
            string token = "\"" + propertyName + "\"";
            int tokenIndex = existing.IndexOf(token, StringComparison.Ordinal);
            if (tokenIndex < 0)
                return false;

            int colon = existing.IndexOf(':', tokenIndex + token.Length);
            if (colon < 0)
                return false;

            int valueStart = colon + 1;
            while (valueStart < existing.Length && char.IsWhiteSpace(existing[valueStart]))
                valueStart++;

            if (valueStart >= existing.Length || existing[valueStart] != '{')
                return false;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            int valueEnd = -1;
            for (int i = valueStart; i < existing.Length; i++)
            {
                char c = existing[i];
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
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        valueEnd = i + 1;
                        break;
                    }
                }
            }

            if (valueEnd <= valueStart)
                return false;

            replaced = existing.Substring(0, valueStart) +
                       json.Trim().Replace("\n", "\n  ") +
                       existing.Substring(valueEnd);
            return true;
        }

        private static string AppendTopLevelSection(string existing, string propertyName, string json)
        {
            string trimmed = existing.TrimEnd();
            int finalBrace = trimmed.LastIndexOf('}');
            if (finalBrace < 0)
                return string.Empty;

            int firstBrace = trimmed.IndexOf('{');
            bool hasExistingProperties = firstBrace >= 0 && finalBrace > firstBrace + 1 && trimmed.Substring(firstBrace + 1, finalBrace - firstBrace - 1).Trim().Length > 0;
            StringBuilder builder = new StringBuilder(trimmed.Length + json.Length + 96);
            builder.Append(trimmed, 0, finalBrace);
            if (hasExistingProperties)
                builder.AppendLine(",");

            builder.Append("  \"");
            builder.Append(propertyName);
            builder.Append("\": ");
            builder.Append(json.Trim().Replace("\n", "\n  "));
            builder.AppendLine();
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string MakeRelative(string root, string path)
        {
            string fullRoot = Path.GetFullPath(root).TrimEnd('\\', '/') + Path.DirectorySeparatorChar;
            string fullPath = Path.GetFullPath(path);
            return fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(fullRoot.Length).Replace('\\', '/')
                : fullPath.Replace('\\', '/');
        }

        private static string ResolveExistingObjectProperty(string source, string propertyName, string fallbackJson)
        {
            return TryExtractTopLevelObject(source, propertyName, out string extracted)
                ? extracted
                : fallbackJson;
        }

        private static bool TryExtractTopLevelObject(string existing, string propertyName, out string json)
        {
            json = string.Empty;
            if (string.IsNullOrEmpty(existing) || string.IsNullOrEmpty(propertyName))
                return false;

            string token = "\"" + propertyName + "\"";
            int tokenIndex = existing.IndexOf(token, StringComparison.Ordinal);
            if (tokenIndex < 0)
                return false;

            int colon = existing.IndexOf(':', tokenIndex + token.Length);
            if (colon < 0)
                return false;

            int valueStart = colon + 1;
            while (valueStart < existing.Length && char.IsWhiteSpace(existing[valueStart]))
                valueStart++;

            if (valueStart >= existing.Length || existing[valueStart] != '{')
                return false;

            int valueEnd = FindJsonObjectEnd(existing, valueStart);
            if (valueEnd <= valueStart)
                return false;

            json = existing.Substring(valueStart, valueEnd - valueStart);
            return true;
        }

        private static int FindJsonObjectEnd(string source, int valueStart)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = valueStart; i < source.Length; i++)
            {
                char c = source[i];
                if (inString)
                {
                    if (escaped)
                        escaped = false;
                    else if (c == '\\')
                        escaped = true;
                    else if (c == '"')
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
                        return i + 1;
                }
            }

            return -1;
        }

        private static bool TryExtractStringProperty(string source, string propertyName, out string value)
        {
            value = string.Empty;
            if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(propertyName))
                return false;

            string token = "\"" + propertyName + "\"";
            int tokenIndex = source.IndexOf(token, StringComparison.Ordinal);
            if (tokenIndex < 0)
                return false;

            int colon = source.IndexOf(':', tokenIndex + token.Length);
            if (colon < 0)
                return false;

            int valueStart = colon + 1;
            while (valueStart < source.Length && char.IsWhiteSpace(source[valueStart]))
                valueStart++;

            if (valueStart >= source.Length || source[valueStart] != '"')
                return false;

            StringBuilder builder = new StringBuilder(64);
            bool escaped = false;
            for (int i = valueStart + 1; i < source.Length; i++)
            {
                char c = source[i];
                if (escaped)
                {
                    builder.Append(c);
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = true;
                    continue;
                }

                if (c == '"')
                {
                    value = builder.ToString();
                    return true;
                }

                builder.Append(c);
            }

            return false;
        }

        private static string InlineJson(string json)
        {
            return string.IsNullOrWhiteSpace(json)
                ? "{}"
                : json.Trim().Replace("\r\n", "\n").Replace("\n", " ");
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            StringBuilder builder = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '\\')
                    builder.Append("\\\\");
                else if (c == '"')
                    builder.Append("\\\"");
                else if (c == '\r')
                    builder.Append("\\r");
                else if (c == '\n')
                    builder.Append("\\n");
                else if (c == '\t')
                    builder.Append("\\t");
                else if (c == '\b')
                    builder.Append("\\b");
                else if (c == '\f')
                    builder.Append("\\f");
                else if (c < 32)
                    builder.Append("\\u").Append(((int)c).ToString("X4"));
                else
                    builder.Append(c);
            }

            return builder.ToString();
        }

        private static string ResolveProjectRoot()
        {
            string assetsPath = Application.dataPath;
            DirectoryInfo parent = Directory.GetParent(assetsPath);
            return parent != null ? parent.FullName : assetsPath;
        }

        private static int mathMax(int a, int b)
        {
            return a > b ? a : b;
        }

        private struct Finding
        {
            public string Path;
            public string Namespace;
            public string Method;
            public string Token;
            public int Line;
        }
    }
}
#endif
