#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    public static class OOP_Voice_Scanner_X_011
    {
        private const string ReportPath = "Docs/Reports/UX_OPTIMIZATION_REPORT_X_011.json";

        private static readonly string[] Roots =
        {
            "Assets/_Project/Scripts/Audio",
            "Assets/_Project/Scripts/UI",
            "Assets/_Project/Scripts/Narrative"
        };

        private static readonly RoutePattern[] ForbiddenRuntimePatterns =
        {
            new RoutePattern("native_min_heap", "NativeMinHeap", "fatal", "Use VocalWarningPriorityWordOps and VwsPriorityWord."),
            new RoutePattern("vws_heap_ops", "VocalWarningHeapOps", "fatal", "Use VocalWarningPriorityWordOps."),
            new RoutePattern("priority_queue", "PriorityQueue<", "fatal", "Use the 64-bit priority word route."),
            new RoutePattern("sorted_set_queue", "SortedSet<", "fatal", "Use the 64-bit priority word route."),
            new RoutePattern("managed_string_subtitle_queue", "struct SubtitleRequest", "fatal", "Use BufferedSubtitleRequest with pooled char buffers."),
            new RoutePattern("managed_string_subtitle_queue_array", "private readonly SubtitleRequest[]", "fatal", "Use BufferedSubtitleRequest with pooled char buffers."),
            new RoutePattern("string_queue_field", "_stringQueue", "fatal", "Use the pooled buffered subtitle ring."),
            new RoutePattern("tmp_text_assignment", ".text =", "warning", "Use SetCharArray through ApplySubtitleBuffer."),
            new RoutePattern("new_string", "new string", "warning", "Use ReadOnlySpan<char> and pooled char storage."),
            new RoutePattern("string_format", "string.Format", "warning", "Use preformatted DTO fields or span formatting outside hot paths.")
        };

        [MenuItem("Hecton8/Audio/Scan UX VWS Subtitles X_011")]
        public static void Scan()
        {
            ScanResult result = ScanProject();
            WriteReport(result);
            AssetDatabase.Refresh();
            Hecton8.Core.H8Debug.Log("X_011 UX scanner wrote " + ReportPath + " with " + result.Findings.Count + " findings.");
        }

        internal static ScanResult ScanProject()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ScanResult result = new ScanResult
            {
                Findings = new List<Finding>(32)
            };

            for (int i = 0; i < Roots.Length; i++)
            {
                string absoluteRoot = Path.Combine(projectRoot, Roots[i]);
                if (!Directory.Exists(absoluteRoot))
                    continue;

                string[] files = Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories);
                for (int fileIndex = 0; fileIndex < files.Length; fileIndex++)
                {
                    string absolutePath = files[fileIndex];
                    string relativePath = ToProjectPath(projectRoot, absolutePath);
                    if (IsEditorPath(relativePath) || !IsRouteFile(relativePath))
                        continue;

                    result.FilesScanned++;
                    ScanFile(relativePath, absolutePath, result);
                }
            }

            string vwsPath = Path.Combine(projectRoot, "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs");
            string subtitlePath = Path.Combine(projectRoot, "Assets/_Project/Scripts/UI/SubtitleManager.cs");
            string babelPath = Path.Combine(projectRoot, "Assets/_Project/Scripts/UI/BabelSubtitleSyncRuntime.cs");
            string subtitleSignalPath = Path.Combine(projectRoot, "Assets/_Project/Scripts/Core/Contracts/Signals/SubtitleCueSignal.cs");
            string vwsText = File.Exists(vwsPath) ? File.ReadAllText(vwsPath) : string.Empty;
            string subtitleText = File.Exists(subtitlePath) ? File.ReadAllText(subtitlePath) : string.Empty;
            string babelText = File.Exists(babelPath) ? File.ReadAllText(babelPath) : string.Empty;
            string subtitleSignalText = File.Exists(subtitleSignalPath) ? File.ReadAllText(subtitleSignalPath) : string.Empty;

            result.PriorityWordImplemented =
                vwsText.IndexOf("VwsPriorityWord", StringComparison.Ordinal) >= 0 &&
                vwsText.IndexOf("VocalWarningPriorityWordOps", StringComparison.Ordinal) >= 0 &&
                vwsText.IndexOf("NativeMinHeap", StringComparison.Ordinal) < 0;
            result.SubtitleSignal64Bytes =
                subtitleSignalText.IndexOf("public struct SubtitleCueSignal", StringComparison.Ordinal) >= 0 &&
                subtitleSignalText.IndexOf("StructLayout(LayoutKind.Explicit, Size = 64)", StringComparison.Ordinal) >= 0 &&
                babelText.IndexOf("public struct SubtitleCueSignal", StringComparison.Ordinal) < 0 &&
                babelText.IndexOf("UnsafeUtility.SizeOf<SubtitleCueSignal>() == 64", StringComparison.Ordinal) >= 0;
            result.StringQueueRemoved =
                subtitleText.IndexOf("SubtitleRequest", StringComparison.Ordinal) < 0 &&
                subtitleText.IndexOf("_stringQueue", StringComparison.Ordinal) < 0 &&
                subtitleText.IndexOf("ShowImmediate(string", StringComparison.Ordinal) < 0;
            result.BlackBoxDumpRoutePresent =
                vwsText.IndexOf("Dump_X_011.bin", StringComparison.Ordinal) >= 0 &&
                vwsText.IndexOf("VwsTelemetryEntry", StringComparison.Ordinal) >= 0 &&
                vwsText.IndexOf("TelemetryCapacity = 300", StringComparison.Ordinal) >= 0;
            result.Pass = result.Findings.Count == 0 &&
                          result.PriorityWordImplemented &&
                          result.SubtitleSignal64Bytes &&
                          result.StringQueueRemoved &&
                          result.BlackBoxDumpRoutePresent;
            return result;
        }

        private static void ScanFile(string relativePath, string absolutePath, ScanResult result)
        {
            string[] lines = File.ReadAllLines(absolutePath);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int patternIndex = 0; patternIndex < ForbiddenRuntimePatterns.Length; patternIndex++)
                {
                    RoutePattern pattern = ForbiddenRuntimePatterns[patternIndex];
                    if (line.IndexOf(pattern.Needle, StringComparison.Ordinal) < 0)
                        continue;

                    result.Findings.Add(new Finding
                    {
                        Id = pattern.Id,
                        Severity = pattern.Severity,
                        File = relativePath,
                        Line = lineIndex + 1,
                        Needle = pattern.Needle,
                        Remediation = pattern.Remediation
                    });
                }
            }
        }

        private static bool IsRouteFile(string relativePath)
        {
            string fileName = Path.GetFileName(relativePath);
            return fileName.IndexOf("VocalWarning", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   fileName.IndexOf("Subtitle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   fileName.IndexOf("Babel", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   fileName.IndexOf("Dialogue", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsEditorPath(string relativePath)
        {
            return relativePath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   relativePath.IndexOf("\\Editor\\", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ToProjectPath(string projectRoot, string absolutePath)
        {
            string fullRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string fullPath = Path.GetFullPath(absolutePath);
            if (fullPath.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase))
                return fullPath.Substring(fullRoot.Length + 1).Replace('\\', '/');

            return fullPath.Replace('\\', '/');
        }

        private static void WriteReport(ScanResult result)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string absolutePath = Path.Combine(projectRoot, ReportPath);
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, BuildJson(result), Encoding.UTF8);
        }

        private static string BuildJson(ScanResult result)
        {
            StringBuilder builder = new StringBuilder(4096);
            builder.Append("{\n");
            AppendProperty(builder, "agent", "X_011", 2, true);
            AppendProperty(builder, "role", "VOCAL_WARNING_AND_SUBTITLE_STREAMLINER", 2, true);
            AppendProperty(builder, "report", "UX_OPTIMIZATION_REPORT_X_011", 2, true);
            AppendProperty(builder, "status", result.Pass ? "PASS_PENDING_COMPILE" : "FAIL_STATIC_FINDINGS", 2, true);
            AppendProperty(builder, "filesScanned", result.FilesScanned, 2, true);
            AppendProperty(builder, "priorityWordImplemented", result.PriorityWordImplemented, 2, true);
            AppendProperty(builder, "subtitleSignal64Bytes", result.SubtitleSignal64Bytes, 2, true);
            AppendProperty(builder, "stringQueueRemoved", result.StringQueueRemoved, 2, true);
            AppendProperty(builder, "blackBoxDumpRoutePresent", result.BlackBoxDumpRoutePresent, 2, true);
            builder.Append("  \"findings\": [\n");
            for (int i = 0; i < result.Findings.Count; i++)
            {
                Finding finding = result.Findings[i];
                builder.Append("    {\n");
                AppendProperty(builder, "id", finding.Id, 6, true);
                AppendProperty(builder, "severity", finding.Severity, 6, true);
                AppendProperty(builder, "file", finding.File, 6, true);
                AppendProperty(builder, "line", finding.Line, 6, true);
                AppendProperty(builder, "needle", finding.Needle, 6, true);
                AppendProperty(builder, "remediation", finding.Remediation, 6, false);
                builder.Append("    }");
                if (i + 1 < result.Findings.Count)
                    builder.Append(',');
                builder.Append('\n');
            }
            builder.Append("  ]\n");
            builder.Append("}\n");
            return builder.ToString();
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, int indent, bool comma)
        {
            builder.Append(' ', indent);
            builder.Append('"').Append(Escape(name)).Append("\": \"").Append(Escape(value)).Append('"');
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendProperty(StringBuilder builder, string name, int value, int indent, bool comma)
        {
            builder.Append(' ', indent);
            builder.Append('"').Append(Escape(name)).Append("\": ").Append(value);
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static void AppendProperty(StringBuilder builder, string name, bool value, int indent, bool comma)
        {
            builder.Append(' ', indent);
            builder.Append('"').Append(Escape(name)).Append("\": ").Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.Append('\n');
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        internal struct ScanResult
        {
            public int FilesScanned;
            public bool PriorityWordImplemented;
            public bool SubtitleSignal64Bytes;
            public bool StringQueueRemoved;
            public bool BlackBoxDumpRoutePresent;
            public bool Pass;
            public List<Finding> Findings;
        }

        private readonly struct RoutePattern
        {
            public RoutePattern(string id, string needle, string severity, string remediation)
            {
                Id = id;
                Needle = needle;
                Severity = severity;
                Remediation = remediation;
            }

            public readonly string Id;
            public readonly string Needle;
            public readonly string Severity;
            public readonly string Remediation;
        }

        internal struct Finding
        {
            public string Id;
            public string Severity;
            public string File;
            public int Line;
            public string Needle;
            public string Remediation;
        }
    }
}
#endif
