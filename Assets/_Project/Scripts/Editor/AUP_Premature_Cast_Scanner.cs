#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Hecton8.Editor
{
    public struct AupPrecisionScanSummary
    {
        public int FilesScanned;
        public int BlockedPrematureFloatCasts;
        public int BlockedTransformAuthorityReads;
        public int ManualReviewAupCasts;
        public int ApprovedHelperCalls;
        public int TransformPositionAuthorityCandidates;
        public int VectorDistanceCandidates;
        public int LayoutValidationFailures;
        public string ReportPath;
        public string FullReportPath;
    }

    public static class AUP_Premature_Cast_Scanner
    {
        private static readonly Regex DirectAupFloatCast = new Regex(
            @"\(float3\)\s*(?!\()(?<expr>[A-Za-z_][A-Za-z0-9_\.]*(?:AUP|Aup|Absolute|Universe)[A-Za-z0-9_\.]*)",
            RegexOptions.Compiled);

        private static readonly Regex AnyAupFloatCast = new Regex(
            @"\(float3\)\s*[^;]*(?:AUP|Aup|Absolute|Universe|double3)",
            RegexOptions.Compiled);

        private static readonly Regex ComponentAupFloatCast = new Regex(
            @"new\s+(?:float3|Vector3)\s*\([^;\n]*\(float\)\s*[^,;\n]*(?:AUP|Aup|Absolute|Universe)",
            RegexOptions.Compiled);

        private static readonly Regex ApprovedHelper = new Regex(
            @"AupPrecisionMath\.(?:LocalDeltaDouble|LocalDeltaFloat3|LocalDeltaFloat3Clamped|DowncastLocalDelta|DowncastLocalDeltaClamped|DowncastProceduralPhase|DistanceSqSafeDouble|DistanceSqSafeFloat|SafeNormalize|SafeNormalizeLocalDelta|ResolveGateDistanceMeters|ShouldSkipByDistanceSq|CreateOutOfBoundsSentinel)",
            RegexOptions.Compiled);

        private static readonly Regex TransformRead = new Regex(
            @"=\s*[^;]*\.position\b|\.position\b[^=]*\)",
            RegexOptions.Compiled);

        private static readonly Regex DistanceFloat = new Regex(
            @"Vector3\.Distance|math\.distance|math\.distancesq",
            RegexOptions.Compiled);

        private static readonly Regex StrictTransformAuthorityRead = new Regex(
            @"(?:AbsoluteUniversePosition\.FromRuntimePosition|HectonFloatingOrigin\.ToAbsoluteUniversePositionDouble3|Vector3\.Distance|math\.distance|math\.distancesq|\.sqrMagnitude)\s*\([^;\n]*\.position|\([^;\n]*\.position\s*[-+][^;\n]*\.position\)",
            RegexOptions.Compiled);

        [MenuItem("Hecton8/AUP Precision/Run Premature Cast Scan")]
        public static void RunFromMenu()
        {
            AupPrecisionScanSummary summary = RunAndWriteReport();
            string message = $"AUP precision scan wrote {summary.ReportPath}. Full={summary.FullReportPath}. BlockedCasts={summary.BlockedPrematureFloatCasts}, blockedTransformAuthority={summary.BlockedTransformAuthorityReads}, layoutFailures={summary.LayoutValidationFailures}";
            if (summary.BlockedPrematureFloatCasts > 0 || summary.BlockedTransformAuthorityReads > 0 || summary.LayoutValidationFailures > 0)
                Debug.LogError(message);
            else
                Debug.Log(message);
        }

        public static AupPrecisionScanSummary RunAndWriteReport()
        {
            string root = ProjectRootPath();
            string scriptsRoot = Path.Combine(root, "Assets", "_Project", "Scripts");
            string reportPath = Path.Combine(root, "Docs", "Reports", "MATH_OPTIMIZATION_REPORT.json");
            string fullReportPath = Path.Combine(root, "Docs", "Reports", "AUP_PRECISION_SCAN_SHINOBU_205.json");
            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDirectory))
                Directory.CreateDirectory(reportDirectory);

            var blocked = new List<AupPrecisionFinding>(64);
            var review = new List<AupPrecisionFinding>(128);
            var blockedTransformAuthority = new List<AupPrecisionFinding>(128);
            var transformReads = new List<AupPrecisionFinding>(128);
            var distances = new List<AupPrecisionFinding>(128);
            AupPrecisionScanSummary summary = default;
            summary.ReportPath = reportPath;
            summary.FullReportPath = fullReportPath;

            foreach (string file in Directory.EnumerateFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                bool scannerSelfDiagnostic = file.EndsWith("AUP_Premature_Cast_Scanner.cs", StringComparison.OrdinalIgnoreCase);
                bool editorPath = file.IndexOf(Path.DirectorySeparatorChar + "Editor" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) >= 0;
                summary.FilesScanned++;
                int lineNumber = 0;
                foreach (string line in File.ReadLines(file))
                {
                    lineNumber++;
                    if (ApprovedHelper.IsMatch(line))
                        summary.ApprovedHelperCalls++;

                    bool anyAupCast = AnyAupFloatCast.IsMatch(line);
                    if (anyAupCast && line.IndexOf("AupPrecisionMath.", StringComparison.Ordinal) < 0)
                    {
                        summary.BlockedPrematureFloatCasts++;
                        string kind = DirectAupFloatCast.IsMatch(line)
                            ? "PREMATURE_FLOAT3_AUP_CAST"
                            : "UNAPPROVED_FLOAT3_AUP_CAST";
                        blocked.Add(new AupPrecisionFinding(file, lineNumber, kind, line));
                    }

                    if (!scannerSelfDiagnostic &&
                        ComponentAupFloatCast.IsMatch(line) &&
                        line.IndexOf("AupPrecisionMath.", StringComparison.Ordinal) < 0)
                    {
                        if (editorPath)
                        {
                            summary.ManualReviewAupCasts++;
                            review.Add(new AupPrecisionFinding(file, lineNumber, "EDITOR_COMPONENT_FLOAT_AUP_CAST_REVIEW", line));
                        }
                        else
                        {
                            summary.BlockedPrematureFloatCasts++;
                            blocked.Add(new AupPrecisionFinding(file, lineNumber, "COMPONENT_FLOAT3_AUP_CAST", line));
                        }
                    }

                    if (StrictTransformAuthorityRead.IsMatch(line) &&
                        line.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) < 0 &&
                        line.IndexOf("Gizmos", StringComparison.OrdinalIgnoreCase) < 0 &&
                        line.IndexOf("Handles", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        summary.BlockedTransformAuthorityReads++;
                        blockedTransformAuthority.Add(new AupPrecisionFinding(file, lineNumber, "TRANSFORM_POSITION_AUTHORITY_BLOCK", line));
                    }

                    if (TransformRead.IsMatch(line) &&
                        line.IndexOf("Editor", StringComparison.OrdinalIgnoreCase) < 0 &&
                        line.IndexOf("Gizmos", StringComparison.OrdinalIgnoreCase) < 0 &&
                        line.IndexOf("Handles", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        summary.TransformPositionAuthorityCandidates++;
                        transformReads.Add(new AupPrecisionFinding(file, lineNumber, "TRANSFORM_POSITION_AUTHORITY_REVIEW", line));
                    }

                    if (DistanceFloat.IsMatch(line) &&
                        line.IndexOf("AupPrecisionMath.", StringComparison.Ordinal) < 0)
                    {
                        summary.VectorDistanceCandidates++;
                        distances.Add(new AupPrecisionFinding(file, lineNumber, "FLOAT_DISTANCE_REVIEW", line));
                    }
                }
            }

            AupLayoutValidationSummary layout = AupDouble3AlignmentValidator.ValidateLayouts();
            summary.LayoutValidationFailures = layout.FailureCount;
            WriteJsonReport(reportPath, fullReportPath, root, summary, blocked, review, blockedTransformAuthority, transformReads, distances, layout);
            AssetDatabase.Refresh();
            return summary;
        }

        private static string ProjectRootPath()
        {
            DirectoryInfo directory = Directory.GetParent(Application.dataPath);
            return directory != null ? directory.FullName : Application.dataPath;
        }

        private static void WriteJsonReport(
            string path,
            string fullReportPath,
            string root,
            AupPrecisionScanSummary summary,
            List<AupPrecisionFinding> blocked,
            List<AupPrecisionFinding> review,
            List<AupPrecisionFinding> blockedTransformAuthority,
            List<AupPrecisionFinding> transformReads,
            List<AupPrecisionFinding> distances,
            AupLayoutValidationSummary layout)
        {
            var builder = new StringBuilder(32768);
            builder.Append("{\n");
            AppendProperty(builder, "scannerId", "SHINOBU_205_AUP_PRECISION_INSPECTOR", true);
            AppendProperty(builder, "generatedUtc", DateTime.UtcNow.ToString("O"), true);
            AppendProperty(builder, "filesScanned", summary.FilesScanned, true);
            AppendProperty(builder, "blockedPrematureFloatCasts", summary.BlockedPrematureFloatCasts, true);
            AppendProperty(builder, "blockedTransformAuthorityReads", summary.BlockedTransformAuthorityReads, true);
            AppendProperty(builder, "manualReviewAupCasts", summary.ManualReviewAupCasts, true);
            AppendProperty(builder, "approvedHelperCalls", summary.ApprovedHelperCalls, true);
            AppendProperty(builder, "transformPositionAuthorityCandidates", summary.TransformPositionAuthorityCandidates, true);
            AppendProperty(builder, "vectorDistanceCandidates", summary.VectorDistanceCandidates, true);
            AppendProperty(builder, "layoutValidationFailures", summary.LayoutValidationFailures, true);
            AppendProperty(builder, "precisionRule", "subtract in double3 before any float3 downcast", true);
            AppendProperty(builder, "transformRule", "Transform.position is presentation only; AUP/DataVault is spatial authority", true);
            AppendFindings(builder, "blockedFindings", root, blocked, true);
            AppendFindings(builder, "manualReviewFindings", root, review, true);
            AppendFindings(builder, "blockedTransformAuthorityFindings", root, blockedTransformAuthority, true);
            AppendFindings(builder, "transformPositionFindings", root, transformReads, true);
            AppendFindings(builder, "distanceFindings", root, distances, true);
            AppendStringArray(builder, "layoutDetails", layout.Details, false);
            builder.Append("}\n");
            File.WriteAllText(fullReportPath, builder.ToString(), Encoding.UTF8);
            UpsertMathOptimizationSummary(path, summary);
        }

        private static void UpsertMathOptimizationSummary(string path, AupPrecisionScanSummary summary)
        {
            var propertyBuilder = new StringBuilder(1024);
            propertyBuilder.Append("  \"aup_precision_inspector\": {\n");
            AppendProperty(propertyBuilder, "scanner_id", "SHINOBU_205_AUP_PRECISION_INSPECTOR", true, 4);
            AppendProperty(propertyBuilder, "generated_utc", DateTime.UtcNow.ToString("O"), true, 4);
            AppendProperty(propertyBuilder, "full_report", ToForwardSlashes(summary.FullReportPath), true, 4);
            AppendProperty(propertyBuilder, "blocked_premature_float3_aup_casts", summary.BlockedPrematureFloatCasts, true, 4);
            AppendProperty(propertyBuilder, "blocked_transform_authority_reads", summary.BlockedTransformAuthorityReads, true, 4);
            AppendProperty(propertyBuilder, "layout_validation_failures", summary.LayoutValidationFailures, true, 4);
            AppendProperty(propertyBuilder, "precision_rule", "double3 target-observer subtraction before float3 downcast", true, 4);
            AppendProperty(propertyBuilder, "transform_rule", "Transform.position is presentation only; AUP/DataVault is spatial authority", true, 4);
            AppendProperty(propertyBuilder, "owned_vault_ids", "73200..73208", false, 4);
            propertyBuilder.Append("  }");

            string propertyJson = propertyBuilder.ToString();
            string existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            string merged = MergeTopLevelObjectProperty(existing, "aup_precision_inspector", propertyJson);
            File.WriteAllText(path, merged, Encoding.UTF8);
        }

        private static string MergeTopLevelObjectProperty(string existing, string propertyName, string propertyJson)
        {
            if (string.IsNullOrWhiteSpace(existing) || existing.TrimStart()[0] != '{')
                return "{\n" + propertyJson + "\n}\n";

            if (TryReplaceTopLevelObjectProperty(existing, propertyName, propertyJson, out string replaced))
                return replaced;

            int insert = existing.IndexOf('{') + 1;
            int next = insert;
            while (next < existing.Length && char.IsWhiteSpace(existing[next]))
                next++;

            string separator = next < existing.Length && existing[next] != '}' ? ",\n" : "\n";
            return existing.Insert(insert, "\n" + propertyJson + separator);
        }

        private static bool TryReplaceTopLevelObjectProperty(string existing, string propertyName, string propertyJson, out string merged)
        {
            merged = existing;
            string quotedName = "\"" + propertyName + "\"";
            int nameIndex = existing.IndexOf(quotedName, StringComparison.Ordinal);
            if (nameIndex < 0)
                return false;

            int colon = existing.IndexOf(':', nameIndex + quotedName.Length);
            if (colon < 0)
                return false;

            int objectStart = existing.IndexOf('{', colon + 1);
            if (objectStart < 0)
                return false;

            int objectEnd = FindObjectEnd(existing, objectStart);
            if (objectEnd < 0)
                return false;

            int lineStart = nameIndex;
            while (lineStart > 0 && existing[lineStart - 1] != '\n')
                lineStart--;

            int replaceEnd = objectEnd + 1;
            while (replaceEnd < existing.Length && char.IsWhiteSpace(existing[replaceEnd]) && existing[replaceEnd] != '\n')
                replaceEnd++;

            bool hadTrailingComma = replaceEnd < existing.Length && existing[replaceEnd] == ',';
            if (hadTrailingComma)
                replaceEnd++;

            string replacement = propertyJson + (hadTrailingComma ? "," : string.Empty);
            merged = existing.Substring(0, lineStart) + replacement + existing.Substring(replaceEnd);
            return true;
        }

        private static int FindObjectEnd(string value, int objectStart)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < value.Length; i++)
            {
                char c = value[i];
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
                    inString = true;
                else if (c == '{')
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

        private static void AppendProperty(StringBuilder builder, string name, string value, bool comma)
        {
            AppendProperty(builder, name, value, comma, 2);
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, bool comma, int indentSpaces)
        {
            AppendIndent(builder, indentSpaces);
            builder.Append('"').Append(name).Append("\": \"").Append(Escape(value)).Append('"');
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendProperty(StringBuilder builder, string name, int value, bool comma)
        {
            AppendProperty(builder, name, value, comma, 2);
        }

        private static void AppendProperty(StringBuilder builder, string name, int value, bool comma, int indentSpaces)
        {
            AppendIndent(builder, indentSpaces);
            builder.Append('"').Append(name).Append("\": ").Append(value);
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendIndent(StringBuilder builder, int spaces)
        {
            for (int i = 0; i < spaces; i++)
                builder.Append(' ');
        }

        private static void AppendFindings(StringBuilder builder, string name, string root, List<AupPrecisionFinding> findings, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": [\n");
            for (int i = 0; i < findings.Count; i++)
            {
                AupPrecisionFinding finding = findings[i];
                builder.Append("    {\"file\":\"")
                    .Append(Escape(ToRelative(root, finding.File)))
                    .Append("\",\"line\":")
                    .Append(finding.Line)
                    .Append(",\"kind\":\"")
                    .Append(Escape(finding.Kind))
                    .Append("\",\"snippet\":\"")
                    .Append(Escape(finding.Snippet.Trim()))
                    .Append("\"}");
                builder.Append(i + 1 < findings.Count ? ",\n" : "\n");
            }

            builder.Append("  ]");
            builder.Append(comma ? ",\n" : "\n");
        }

        private static void AppendStringArray(StringBuilder builder, string name, List<string> values, bool comma)
        {
            builder.Append("  \"").Append(name).Append("\": [\n");
            for (int i = 0; i < values.Count; i++)
            {
                builder.Append("    \"").Append(Escape(values[i])).Append('"');
                builder.Append(i + 1 < values.Count ? ",\n" : "\n");
            }

            builder.Append("  ]");
            builder.Append(comma ? ",\n" : "\n");
        }

        private static string ToRelative(string root, string file)
        {
            string full = Path.GetFullPath(file);
            string prefix = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
            if (full.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return full.Substring(prefix.Length).Replace('\\', '/');
            return full.Replace('\\', '/');
        }

        private static string ToForwardSlashes(string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : value.Replace('\\', '/');
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t");
        }
    }

    public struct AupLayoutValidationSummary
    {
        public int CheckedCount;
        public int FailureCount;
        public List<string> Details;
    }

    public static class AupDouble3AlignmentValidator
    {
        [MenuItem("Hecton8/AUP Precision/Validate double3 Layouts")]
        public static void ValidateFromMenu()
        {
            AupLayoutValidationSummary summary = ValidateLayouts();
            string message = $"AUP layout validation checked {summary.CheckedCount} layouts, failures={summary.FailureCount}";
            if (summary.FailureCount == 0)
                Debug.Log(message);
            else
                Debug.LogError(message + "\n" + string.Join("\n", summary.Details));
        }

        public static AupLayoutValidationSummary ValidateLayouts()
        {
            AupLayoutValidationSummary summary = default;
            summary.Details = new List<string>(32);

            CheckUnsafe<AUP_StateDTO>(ref summary, "AUP_StateDTO", 64,
                new FieldExpectation(nameof(AUP_StateDTO.GlobalPosition), 0),
                new FieldExpectation(nameof(AUP_StateDTO.LocalPosition), 24),
                new FieldExpectation(nameof(AUP_StateDTO.SectorHash), 36));
            CheckUnsafe<OriginShiftSignalDTO>(ref summary, "OriginShiftSignalDTO", 32,
                new FieldExpectation(nameof(OriginShiftSignalDTO.ShiftDelta), 0),
                new FieldExpectation(nameof(OriginShiftSignalDTO.NewSectorHash), 24));
            CheckUnsafe<MockCameraAUP>(ref summary, "MockCameraAUP", 48,
                new FieldExpectation(nameof(MockCameraAUP.GlobalPosition), 0),
                new FieldExpectation(nameof(MockCameraAUP.LocalPosition), 24));
            CheckUnsafe<AupOriginShiftTelemetryEntry>(ref summary, "AupOriginShiftTelemetryEntry", 128,
                new FieldExpectation(nameof(AupOriginShiftTelemetryEntry.ShiftDelta), 0),
                new FieldExpectation(nameof(AupOriginShiftTelemetryEntry.TotalUniverseOffset), 24),
                new FieldExpectation(nameof(AupOriginShiftTelemetryEntry.CameraLocalPosition), 96));
            CheckUnsafe<AupPrecisionTelemetryEntry>(ref summary, "AupPrecisionTelemetryEntry", 64,
                new FieldExpectation(nameof(AupPrecisionTelemetryEntry.MaxLocalDistanceMeters), 0),
                new FieldExpectation(nameof(AupPrecisionTelemetryEntry.MaxLocalDistanceSq), 8),
                new FieldExpectation(nameof(AupPrecisionTelemetryEntry.PositionHash), 56));
            CheckUnsafe<AupToleranceProfileDTO>(ref summary, "AupToleranceProfileDTO", 64,
                new FieldExpectation(nameof(AupToleranceProfileDTO.SubsystemHash), 0),
                new FieldExpectation(nameof(AupToleranceProfileDTO.GateMinMeters), 8),
                new FieldExpectation(nameof(AupToleranceProfileDTO.Flags), 24));
            CheckUnsafe<AupPrecisionRuntimeStateDTO>(ref summary, "AupPrecisionRuntimeStateDTO", 64,
                new FieldExpectation(nameof(AupPrecisionRuntimeStateDTO.ObserverAup), 0),
                new FieldExpectation(nameof(AupPrecisionRuntimeStateDTO.Frame), 24),
                new FieldExpectation(nameof(AupPrecisionRuntimeStateDTO.TelemetryCursor), 32),
                new FieldExpectation(nameof(AupPrecisionRuntimeStateDTO.Flags), 52));
            CheckUnsafe<AupPrecisionFaultCounter64>(ref summary, "AupPrecisionFaultCounter64", 64,
                new FieldExpectation(nameof(AupPrecisionFaultCounter64.NonFiniteCount), 0),
                new FieldExpectation(nameof(AupPrecisionFaultCounter64.PositionHash), 24));

            CheckReflection(ref summary, "Hecton8.World.AbsoluteUniversePosition", 48,
                new FieldExpectation("GridX", 0),
                new FieldExpectation("GridY", 8),
                new FieldExpectation("GridZ", 16),
                new FieldExpectation("LocalX", 24));
            CheckReflection(ref summary, "Hecton8.World.AbsoluteUniversePositionBlit", 48,
                new FieldExpectation("GridX", 0),
                new FieldExpectation("GridY", 8),
                new FieldExpectation("GridZ", 16),
                new FieldExpectation("Local", 24));
            CheckReflection(ref summary, "Hecton8.World.AbsoluteUniversePositionBlit128", 48,
                new FieldExpectation("GridX", 0),
                new FieldExpectation("GridY", 8),
                new FieldExpectation("GridZ", 16),
                new FieldExpectation("Local", 24));

            if (summary.FailureCount == 0)
                summary.Details.Add("OK: UnsafeUtility sizes and Marshal offsets match AUP double3 alignment expectations.");
            return summary;
        }

        private static void CheckUnsafe<T>(ref AupLayoutValidationSummary summary, string name, int expectedSize, params FieldExpectation[] fields)
            where T : struct
        {
            summary.CheckedCount++;
            int size = UnsafeUtility.SizeOf<T>();
            if (size != expectedSize)
            {
                summary.FailureCount++;
                summary.Details.Add($"{name}: size {size}, expected {expectedSize}");
            }

            Type type = typeof(T);
            for (int i = 0; i < fields.Length; i++)
            {
                FieldExpectation field = fields[i];
                int offset = Marshal.OffsetOf(type, field.Name).ToInt32();
                if (offset != field.Offset)
                {
                    summary.FailureCount++;
                    summary.Details.Add($"{name}.{field.Name}: offset {offset}, expected {field.Offset}");
                }
            }
        }

        private static void CheckReflection(ref AupLayoutValidationSummary summary, string typeName, int expectedSize, params FieldExpectation[] fields)
        {
            Type type = ResolveType(typeName);
            if (type == null)
            {
                summary.FailureCount++;
                summary.Details.Add($"{typeName}: type not found for reflection layout check");
                return;
            }

            summary.CheckedCount++;
            int size = Marshal.SizeOf(type);
            if (size != expectedSize)
            {
                summary.FailureCount++;
                summary.Details.Add($"{typeName}: size {size}, expected {expectedSize}");
            }

            for (int i = 0; i < fields.Length; i++)
            {
                FieldExpectation field = fields[i];
                FieldInfo fieldInfo = type.GetField(field.Name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (fieldInfo == null)
                {
                    summary.FailureCount++;
                    summary.Details.Add($"{typeName}.{field.Name}: field not found");
                    continue;
                }

                int offset = Marshal.OffsetOf(type, field.Name).ToInt32();
                if (offset != field.Offset)
                {
                    summary.FailureCount++;
                    summary.Details.Add($"{typeName}.{field.Name}: offset {offset}, expected {field.Offset}");
                }
            }
        }

        private static Type ResolveType(string typeName)
        {
            Type type = Type.GetType(typeName + ", Hecton8.Core") ??
                        Type.GetType(typeName + ", Hecton8.World") ??
                        Type.GetType(typeName);
            if (type != null)
                return type;

            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (int i = 0; i < assemblies.Length; i++)
            {
                type = assemblies[i].GetType(typeName);
                if (type != null)
                    return type;
            }

            return null;
        }
    }

    internal sealed class AupTelemetryHistogramElement : VisualElement
    {
        private readonly float[] _samples = new float[AupPrecisionMath.TelemetryCapacity];
        private int _sampleCount;

        public AupTelemetryHistogramElement()
        {
            style.height = 84f;
            style.marginTop = 6f;
            style.marginBottom = 6f;
            generateVisualContent += DrawHistogram;
        }

        public void SetSamples(NativeArray<AupPrecisionTelemetryEntry> ring)
        {
            _sampleCount = math.min(ring.IsCreated ? ring.Length : 0, _samples.Length);
            for (int i = 0; i < _sampleCount; i++)
                _samples[i] = math.max(0f, ring[i].KernelMicrosecondsEstimate);
            MarkDirtyRepaint();
        }

        public void ClearSamples()
        {
            _sampleCount = 0;
            MarkDirtyRepaint();
        }

        private void DrawHistogram(MeshGenerationContext context)
        {
            Rect rect = contentRect;
            if (rect.width <= 1f || rect.height <= 1f)
                return;

            Painter2D painter = context.painter2D;
            painter.lineWidth = 1f;
            painter.strokeColor = new Color(0.16f, 0.22f, 0.24f, 1f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(rect.xMin, rect.yMax - 1f));
            painter.LineTo(new Vector2(rect.xMax, rect.yMax - 1f));
            painter.Stroke();

            if (_sampleCount <= 0)
                return;

            float maxValue = 0.001f;
            for (int i = 0; i < _sampleCount; i++)
                maxValue = math.max(maxValue, _samples[i]);

            float widthStep = _sampleCount > 1 ? rect.width / (_sampleCount - 1) : rect.width;
            painter.lineWidth = 1.5f;
            painter.strokeColor = new Color(0.08f, 0.82f, 0.92f, 1f);
            painter.BeginPath();
            for (int i = 0; i < _sampleCount; i++)
            {
                float x = rect.xMin + (widthStep * i);
                float normalized = math.saturate(_samples[i] / maxValue);
                float y = math.lerp(rect.yMax - 2f, rect.yMin + 2f, normalized);
                if (i == 0)
                    painter.MoveTo(new Vector2(x, y));
                else
                    painter.LineTo(new Vector2(x, y));
            }
            painter.Stroke();
        }
    }

    public sealed class AupPrecisionXRayWindow : EditorWindow
    {
        private Label _status;
        private Toggle _gizmoToggle;
        private Toggle _telemetryAutoRefreshToggle;
        private AupTelemetryHistogramElement _histogram;
        private double _nextTelemetryRefreshTime;
        private double3 _observerAup;
        private double3 _sampleAup;
        private float3 _preciseLocal;
        private float3 _earlyFloatLocal;

        [MenuItem("Hecton8/AUP Precision/X-Ray")]
        public static void Open()
        {
            GetWindow<AupPrecisionXRayWindow>("AUP Precision X-Ray");
        }

        public void CreateGUI()
        {
            rootVisualElement.style.paddingLeft = 8;
            rootVisualElement.style.paddingRight = 8;
            rootVisualElement.style.paddingTop = 8;
            rootVisualElement.style.paddingBottom = 8;

            _status = new Label("No scan run.");
            rootVisualElement.Add(new Button(RunScanner) { text = "Run Static AUP Scan" });
            rootVisualElement.Add(new Button(ValidateLayouts) { text = "Validate double3 Layouts" });
            rootVisualElement.Add(new Button(RefreshTelemetryHistogram) { text = "Refresh Telemetry Histogram" });
            rootVisualElement.Add(new Button(GenerateExtremeMock) { text = "Teleport to Edge Mock" });
            _gizmoToggle = new Toggle("Live jitter gizmo");
            _gizmoToggle.RegisterValueChangedCallback(evt => SetGizmo(evt.newValue));
            _telemetryAutoRefreshToggle = new Toggle("Auto telemetry refresh");
            _telemetryAutoRefreshToggle.RegisterValueChangedCallback(evt => SetTelemetryAutoRefresh(evt.newValue));
            _histogram = new AupTelemetryHistogramElement();
            rootVisualElement.Add(_gizmoToggle);
            rootVisualElement.Add(_telemetryAutoRefreshToggle);
            rootVisualElement.Add(_histogram);
            rootVisualElement.Add(_status);
        }

        private void OnDisable()
        {
            SceneView.duringSceneGui -= DrawJitterGizmo;
            EditorApplication.update -= TickTelemetryRefresh;
        }

        private void RunScanner()
        {
            AupPrecisionScanSummary summary = AUP_Premature_Cast_Scanner.RunAndWriteReport();
            _status.text = $"Scan: blockedCasts={summary.BlockedPrematureFloatCasts}, blockedTransformAuthority={summary.BlockedTransformAuthorityReads}, transformReview={summary.TransformPositionAuthorityCandidates}, report={summary.ReportPath}";
        }

        private void ValidateLayouts()
        {
            AupLayoutValidationSummary summary = AupDouble3AlignmentValidator.ValidateLayouts();
            _status.text = $"Layouts: checked={summary.CheckedCount}, failures={summary.FailureCount}";
        }

        private void GenerateExtremeMock()
        {
            bool wroteVault = false;
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null && AupPrecisionVault.EnsureBuffers(vault, 32, out AupPrecisionVaultViews views))
            {
                JobHandle handle = new GenerateMockExtremeAupJob
                {
                    OutputAups = views.TargetAups,
                    PositiveEdgeAup = new double3(100000.003d, 2500.125d, -99999.997d),
                    NegativeEdgeAup = new double3(-100000.002d, -2500.25d, 99999.999d),
                    JitterMeters = 0.002d
                }.Schedule(32, 16);
                handle.Complete();

                _observerAup = new double3(99999.75d, 2500.0d, -100000.125d);
                _sampleAup = views.TargetAups[0];
                _preciseLocal = AupPrecisionMath.LocalDeltaFloat3(_sampleAup, _observerAup, float3.zero);
                _earlyFloatLocal = new float3((float)_sampleAup.x, (float)_sampleAup.y, (float)_sampleAup.z) -
                                   new float3((float)_observerAup.x, (float)_observerAup.y, (float)_observerAup.z);

                int copyCount = math.min(32, views.MockExtremeAups.Length);
                for (int i = 0; i < copyCount; i++)
                    views.MockExtremeAups[i] = views.TargetAups[i];

                AupPrecisionRuntimeStateDTO state = views.RuntimeState[0];
                state.ObserverAup = _observerAup;
                state.ActiveCount = 32;
                state.GlobalQualityWeight = 1f;
                state.GateDistanceMeters = AupPrecisionMath.ResolveGateDistanceMeters(1f);
                state.MaxLocalCastMeters = AupPrecisionMath.DefaultMaxLocalCastMeters;
                state.Flags = AupPrecisionJobs.ResultFlagValid;
                views.RuntimeState[0] = state;
                wroteVault = true;
            }

            if (!wroteVault)
            {
                using (var samples = new NativeArray<double3>(32, Allocator.TempJob, NativeArrayOptions.UninitializedMemory))
                {
                    JobHandle handle = new GenerateMockExtremeAupJob
                    {
                        OutputAups = samples,
                        PositiveEdgeAup = new double3(100000.003d, 2500.125d, -99999.997d),
                        NegativeEdgeAup = new double3(-100000.002d, -2500.25d, 99999.999d),
                        JitterMeters = 0.002d
                    }.Schedule(samples.Length, 16);
                    handle.Complete();

                    _observerAup = new double3(99999.75d, 2500.0d, -100000.125d);
                    _sampleAup = samples[0];
                    _preciseLocal = AupPrecisionMath.LocalDeltaFloat3(_sampleAup, _observerAup, float3.zero);
                    _earlyFloatLocal = new float3((float)_sampleAup.x, (float)_sampleAup.y, (float)_sampleAup.z) -
                                       new float3((float)_observerAup.x, (float)_observerAup.y, (float)_observerAup.z);
                }
            }

            float3 error = _earlyFloatLocal - _preciseLocal;
            string source = wroteVault ? "Vault 73200/73207" : "TempJob fallback";
            _status.text = $"Edge mock {source}: precise={_preciseLocal}, earlyFloat={_earlyFloatLocal}, errorMeters={math.length(error):0.000000}";
            SceneView view = SceneView.lastActiveSceneView;
            if (view != null)
                view.LookAt(new Vector3(_preciseLocal.x, _preciseLocal.y, _preciseLocal.z));
            RefreshTelemetryHistogram(false);
            SceneView.RepaintAll();
        }

        private void RefreshTelemetryHistogram()
        {
            RefreshTelemetryHistogram(true);
        }

        private void RefreshTelemetryHistogram(bool updateStatus)
        {
            IDataVault vault = GlobalRegistry.DataVault;
            if (vault != null &&
                vault.TryGetBuffer<AupPrecisionTelemetryEntry>(AupPrecisionVault.TelemetryRingBuffer, out NativeArray<AupPrecisionTelemetryEntry> ring) &&
                ring.IsCreated)
            {
                _histogram.SetSamples(ring);
                if (updateStatus)
                    _status.text = $"Telemetry: samples={ring.Length}, source=Vault 73204";
                return;
            }

            _histogram.ClearSamples();
            if (updateStatus)
                _status.text = "Telemetry: Vault 73204 unavailable.";
        }

        private void SetTelemetryAutoRefresh(bool enabled)
        {
            EditorApplication.update -= TickTelemetryRefresh;
            if (enabled)
            {
                _nextTelemetryRefreshTime = 0.0d;
                EditorApplication.update += TickTelemetryRefresh;
            }
        }

        private void TickTelemetryRefresh()
        {
            double now = EditorApplication.timeSinceStartup;
            if (now < _nextTelemetryRefreshTime)
                return;

            _nextTelemetryRefreshTime = now + 0.25d;
            RefreshTelemetryHistogram(false);
        }

        private void SetGizmo(bool enabled)
        {
            SceneView.duringSceneGui -= DrawJitterGizmo;
            if (enabled)
                SceneView.duringSceneGui += DrawJitterGizmo;
            SceneView.RepaintAll();
        }

        private void DrawJitterGizmo(SceneView view)
        {
            Vector3 precise = new Vector3(_preciseLocal.x, _preciseLocal.y, _preciseLocal.z);
            Vector3 early = new Vector3(_earlyFloatLocal.x, _earlyFloatLocal.y, _earlyFloatLocal.z);
            Handles.color = Color.cyan;
            Handles.SphereHandleCap(0, precise, Quaternion.identity, 0.35f, EventType.Repaint);
            Handles.Label(precise, "AUP double-subtract local");
            Handles.color = Color.red;
            Handles.SphereHandleCap(0, early, Quaternion.identity, 0.25f, EventType.Repaint);
            Handles.Label(early, "early float cast");
            Handles.DrawLine(precise, early);
        }
    }

    internal readonly struct AupPrecisionFinding
    {
        public readonly string File;
        public readonly int Line;
        public readonly string Kind;
        public readonly string Snippet;

        public AupPrecisionFinding(string file, int line, string kind, string snippet)
        {
            File = file;
            Line = line;
            Kind = kind;
            Snippet = snippet;
        }
    }

    internal readonly struct FieldExpectation
    {
        public readonly string Name;
        public readonly int Offset;

        public FieldExpectation(string name, int offset)
        {
            Name = name;
            Offset = offset;
        }
    }
}
#endif
