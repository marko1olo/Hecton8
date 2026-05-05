#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - DocumentationAuthoritySmokeTester.cs
// Editor-only guard for documentation sorting authority.
// ============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton8.Core;
using UnityEditor;
using UnityEngine;

namespace Hecton8.EditorTools
{
    public static class DocumentationAuthoritySmokeTester
    {
        private const string MenuPath = "Hecton/Validation/Validate Documentation Authority";
        private const string StressMenuPath = "Hecton/Validation/Stress Documentation Authority";
        private const int StressPassCount = 3;

        [MenuItem(MenuPath, priority = 260)]
        public static void RunMenuItem()
        {
            bool passed = Run(out string json);
            if (passed)
                Debug.Log(json);
            else
                Debug.LogError(json);
        }

        public static bool Run(out string json)
        {
            DocumentationAuthorityAuditResult result = DocumentationAuthorityAudit.Execute(ResolveProjectRoot());
            DocumentationAuthorityTelemetryReporter.PublishIfFailed(result);
            json = DocumentationAuthorityJsonWriter.Write(result);
            DocumentationAuthorityJsonWriter.TryWriteArtifact(ResolveProjectRoot(), json);
            return result.Passed;
        }

        [MenuItem(StressMenuPath, priority = 261)]
        public static void RunStressMenuItem()
        {
            bool passed = RunStress(out string json);
            if (passed)
                Debug.Log(json);
            else
                Debug.LogError(json);
        }

        public static bool RunStress(out string json)
        {
            string projectRoot = ResolveProjectRoot();
            DocumentationAuthorityStressResult result =
                DocumentationAuthorityStressRunner.Execute(projectRoot, StressPassCount);
            json = DocumentationAuthorityJsonWriter.WriteStress(result);
            DocumentationAuthorityJsonWriter.TryWriteStressArtifact(projectRoot, json);
            return result.Passed;
        }

        public static void RunBatchAll()
        {
            string projectRoot = ResolveProjectRoot();
            DocumentationAuthorityAuditResult auditResult = DocumentationAuthorityAudit.Execute(projectRoot);
            DocumentationAuthorityTelemetryReporter.PublishIfFailed(auditResult);
            string smokeJson = DocumentationAuthorityJsonWriter.Write(auditResult);
            DocumentationAuthorityJsonWriter.TryWriteArtifact(projectRoot, smokeJson);
            DocumentationAuthorityJsonWriter.TryWriteCodexArtifact(projectRoot, smokeJson);

            DocumentationAuthorityStressResult stressResult =
                DocumentationAuthorityStressRunner.Execute(projectRoot, StressPassCount);
            string stressJson = DocumentationAuthorityJsonWriter.WriteStress(stressResult);
            DocumentationAuthorityJsonWriter.TryWriteStressArtifact(projectRoot, stressJson);
            DocumentationAuthorityJsonWriter.TryWriteCodexStressArtifact(projectRoot, stressJson);

            bool passed = auditResult.Passed && stressResult.Passed;
            string batchJson = DocumentationAuthorityJsonWriter.WriteBatch(auditResult, stressResult, passed);
            DocumentationAuthorityJsonWriter.TryWriteBatchArtifact(projectRoot, batchJson);

            if (passed)
                Debug.Log(batchJson);
            else
                Debug.LogError(batchJson);

            EditorApplication.Exit(passed ? 0 : 1);
        }

        private static string ResolveProjectRoot()
        {
            DirectoryInfo dataDirectory = Directory.GetParent(Application.dataPath);
            return dataDirectory == null ? Directory.GetCurrentDirectory() : dataDirectory.FullName;
        }
    }

    internal static class DocumentationAuthorityAudit
    {
        private const int HeaderScanLineLimit = 30;
        private const int MaxAllowedActiveHeaderDebt = 96;
        private const int MaxListedFailures = 24;
        private const string DocsFolderName = "Docs";
        private const string RootLogsArchiveRoot = "Docs/DEPRECATED/External_And_Log_Bundles";

        public static DocumentationAuthorityAuditResult Execute(string projectRoot)
        {
            DocumentationAuthorityAuditResult result = new DocumentationAuthorityAuditResult
            {
                ProjectRoot = projectRoot,
                MaxAllowedActiveHeaderDebt = MaxAllowedActiveHeaderDebt
            };

            CountRootLooseTextLogs(projectRoot, result);
            CountRelocatedRootLogs(projectRoot, result);
            AuditDocumentationHeaders(projectRoot, result);

            result.Passed =
                result.RootLooseTextLogCount == 0 &&
                result.DirectDocsHeaderMissingCount == 0 &&
                result.ArchitectureHeaderMissingCount == 0 &&
                result.ActiveHeaderDebt <= MaxAllowedActiveHeaderDebt;

            return result;
        }

        private static void CountRootLooseTextLogs(string projectRoot, DocumentationAuthorityAuditResult result)
        {
            if (!Directory.Exists(projectRoot))
            {
                AddFailure(result, "Project root missing: " + projectRoot);
                return;
            }

            string[] txtFiles = Directory.GetFiles(projectRoot, "*.txt", SearchOption.TopDirectoryOnly);
            string[] logFiles = Directory.GetFiles(projectRoot, "*.log", SearchOption.TopDirectoryOnly);
            result.RootLooseTextLogCount = txtFiles.Length + logFiles.Length;

            for (int i = 0; i < txtFiles.Length; i++)
                AddFailure(result, "Root loose text file: " + Path.GetFileName(txtFiles[i]));

            for (int i = 0; i < logFiles.Length; i++)
                AddFailure(result, "Root loose log file: " + Path.GetFileName(logFiles[i]));
        }

        private static void CountRelocatedRootLogs(string projectRoot, DocumentationAuthorityAuditResult result)
        {
            string archiveRootPath = Path.Combine(projectRoot, RootLogsArchiveRoot.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(archiveRootPath))
                return;

            string[] bundlePaths = Directory.GetDirectories(archiveRootPath, "Root_Logs_*", SearchOption.TopDirectoryOnly);
            for (int i = 0; i < bundlePaths.Length; i++)
            {
                string[] archiveLogs = Directory.GetFiles(bundlePaths[i], "*.log", SearchOption.TopDirectoryOnly);
                result.RelocatedRootLogCount += archiveLogs.Length;
            }
        }

        private static void AuditDocumentationHeaders(string projectRoot, DocumentationAuthorityAuditResult result)
        {
            string docsRoot = Path.Combine(projectRoot, DocsFolderName);
            if (!Directory.Exists(docsRoot))
            {
                AddFailure(result, "Docs root missing: " + docsRoot);
                return;
            }

            string[] markdownFiles = Directory.GetFiles(docsRoot, "*.md", SearchOption.AllDirectories);
            Array.Sort(markdownFiles, StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < markdownFiles.Length; i++)
            {
                string relativePath = DocumentationAuthorityPathPolicy.NormalizeRelativePath(projectRoot, markdownFiles[i]);
                result.TotalMarkdownCount++;

                if (!DocumentationAuthorityPathPolicy.IsActiveDocumentationPath(relativePath))
                    continue;

                result.ActiveMarkdownCount++;

                HeaderState headerState = ReadHeaderState(markdownFiles[i]);
                bool missingAnyHeader = !headerState.HasDate || !headerState.HasStatus;
                if (missingAnyHeader)
                {
                    result.ActiveHeaderDebt++;
                    if (!headerState.HasDate)
                        result.ActiveMissingDateCount++;
                    if (!headerState.HasStatus)
                        result.ActiveMissingStatusCount++;
                }

                if (DocumentationAuthorityPathPolicy.IsDirectDocsFile(relativePath) && missingAnyHeader)
                {
                    result.DirectDocsHeaderMissingCount++;
                    AddFailure(result, "Direct Docs header missing: " + relativePath);
                }

                if (DocumentationAuthorityPathPolicy.IsArchitectureFile(relativePath) && missingAnyHeader)
                {
                    result.ArchitectureHeaderMissingCount++;
                    AddFailure(result, "Architecture header missing: " + relativePath);
                }
            }

            if (result.ActiveHeaderDebt > MaxAllowedActiveHeaderDebt)
            {
                AddFailure(
                    result,
                    "Active markdown header debt exceeds baseline: " +
                    result.ActiveHeaderDebt +
                    " > " +
                    MaxAllowedActiveHeaderDebt);
            }
        }

        private static HeaderState ReadHeaderState(string path)
        {
            HeaderState state = default;

            using (StreamReader reader = new StreamReader(path, Encoding.UTF8, true))
            {
                for (int lineIndex = 0; lineIndex < HeaderScanLineLimit && !reader.EndOfStream; lineIndex++)
                {
                    string line = reader.ReadLine();
                    if (line == null)
                        break;

                    if (line.StartsWith("Date:", StringComparison.Ordinal))
                        state.HasDate = true;
                    else if (line.StartsWith("Status:", StringComparison.Ordinal))
                        state.HasStatus = true;

                    if (state.HasDate && state.HasStatus)
                        break;
                }
            }

            return state;
        }

        private static void AddFailure(DocumentationAuthorityAuditResult result, string failure)
        {
            result.FailureCount++;
            if (result.Failures.Count < MaxListedFailures)
                result.Failures.Add(failure);
        }

        private struct HeaderState
        {
            public bool HasDate;
            public bool HasStatus;
        }
    }

    internal static class DocumentationAuthorityPathPolicy
    {
        private const string DocsFolderName = "Docs";

        public static bool IsActiveDocumentationPath(string relativePath)
        {
            return relativePath.StartsWith("Docs/", StringComparison.Ordinal) &&
                   !relativePath.StartsWith("Docs/_Archive/", StringComparison.OrdinalIgnoreCase) &&
                   !relativePath.StartsWith("Docs/DEPRECATED/", StringComparison.OrdinalIgnoreCase) &&
                   !relativePath.StartsWith("Docs/ARCHIVARIUS REPORTS/03_OBSOLETE/", StringComparison.OrdinalIgnoreCase) &&
                   !relativePath.StartsWith("Docs/Reports/", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsDirectDocsFile(string relativePath)
        {
            int lastSlash = relativePath.LastIndexOf('/');
            return lastSlash == DocsFolderName.Length;
        }

        public static bool IsArchitectureFile(string relativePath)
        {
            return relativePath.StartsWith("Docs/ARCHITECTURE/", StringComparison.OrdinalIgnoreCase);
        }

        public static string NormalizeRelativePath(string projectRoot, string absolutePath)
        {
            string normalizedRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string normalizedPath = Path.GetFullPath(absolutePath);
            string relative = normalizedPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase)
                ? normalizedPath.Substring(normalizedRoot.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                : normalizedPath;
            return relative.Replace('\\', '/');
        }
    }

    internal static class DocumentationAuthorityTelemetryReporter
    {
        private const uint DocumentationHeaderDebtWarningHash = 0xD0CA4111u;
        private const uint DocumentationAuthorityContextHash = 0xD0CA8008u;

        public static void PublishIfFailed(DocumentationAuthorityAuditResult result)
        {
            if (result.Passed)
                return;

            result.TelemetryWarningRequested = true;
            result.TelemetryRuntimeEligible = Application.isPlaying;
            GlobalTelemetryBus.PublishPerformanceWarning(
                DocumentationHeaderDebtWarningHash,
                DocumentationAuthorityContextHash,
                result.ActiveHeaderDebt);
        }
    }

    internal static class DocumentationAuthorityStressRunner
    {
        public static DocumentationAuthorityStressResult Execute(string projectRoot, int passCount)
        {
            DocumentationAuthorityStressResult stressResult = new DocumentationAuthorityStressResult
            {
                ProjectRoot = projectRoot,
                PassCount = Math.Max(1, passCount)
            };

            DocumentationAuthorityAuditResult baseline = null;
            DocumentationAuthorityAuditResult latest = null;
            for (int i = 0; i < stressResult.PassCount; i++)
            {
                latest = DocumentationAuthorityAudit.Execute(projectRoot);
                if (!latest.Passed)
                    AddFailure(stressResult, "Audit pass failed: " + i);

                if (baseline == null)
                {
                    baseline = latest;
                    continue;
                }

                if (!HasStableCounts(baseline, latest))
                    AddFailure(stressResult, "Audit counts changed between stress passes: " + i);
            }

            stressResult.FinalAudit = latest;
            stressResult.Passed = stressResult.FailureCount == 0 && latest != null && latest.Passed;
            return stressResult;
        }

        private static bool HasStableCounts(
            DocumentationAuthorityAuditResult baseline,
            DocumentationAuthorityAuditResult current)
        {
            return baseline.TotalMarkdownCount == current.TotalMarkdownCount &&
                   baseline.ActiveMarkdownCount == current.ActiveMarkdownCount &&
                   baseline.ActiveHeaderDebt == current.ActiveHeaderDebt &&
                   baseline.ActiveMissingDateCount == current.ActiveMissingDateCount &&
                   baseline.ActiveMissingStatusCount == current.ActiveMissingStatusCount &&
                   baseline.DirectDocsHeaderMissingCount == current.DirectDocsHeaderMissingCount &&
                   baseline.ArchitectureHeaderMissingCount == current.ArchitectureHeaderMissingCount &&
                   baseline.RootLooseTextLogCount == current.RootLooseTextLogCount &&
                   baseline.RelocatedRootLogCount == current.RelocatedRootLogCount;
        }

        private static void AddFailure(DocumentationAuthorityStressResult result, string failure)
        {
            result.FailureCount++;
            if (result.Failures.Count < 24)
                result.Failures.Add(failure);
        }
    }

    internal sealed class DocumentationAuthorityAuditResult
    {
        // COLD ALLOC: List<string>[24] - editor smoke failure sample - owner: DocumentationAuthoritySmokeTester
        public readonly List<string> Failures = new List<string>(24);

        public string ProjectRoot;
        public bool Passed;
        public int TotalMarkdownCount;
        public int ActiveMarkdownCount;
        public int ActiveHeaderDebt;
        public int ActiveMissingDateCount;
        public int ActiveMissingStatusCount;
        public int DirectDocsHeaderMissingCount;
        public int ArchitectureHeaderMissingCount;
        public int RootLooseTextLogCount;
        public int RelocatedRootLogCount;
        public int MaxAllowedActiveHeaderDebt;
        public int FailureCount;
        public bool TelemetryWarningRequested;
        public bool TelemetryRuntimeEligible;
    }

    internal sealed class DocumentationAuthorityStressResult
    {
        // COLD ALLOC: List<string>[24] - editor stress failure sample - owner: DocumentationAuthoritySmokeTester
        public readonly List<string> Failures = new List<string>(24);

        public string ProjectRoot;
        public bool Passed;
        public int PassCount;
        public int FailureCount;
        public DocumentationAuthorityAuditResult FinalAudit;
    }

    internal static class DocumentationAuthorityJsonWriter
    {
        private const string ArtifactPath = "Temp/CodexArtifacts/documentation-authority-smoke.json";
        private const string StressArtifactPath = "Temp/CodexArtifacts/documentation-authority-stress.json";
        private const string CodexArtifactPath = "CodexArtifacts/documentation-authority-smoke.json";
        private const string CodexStressArtifactPath = "CodexArtifacts/documentation-authority-stress.json";
        private const string BatchArtifactPath = "CodexArtifacts/documentation-authority-batch.json";

        public static string Write(DocumentationAuthorityAuditResult result)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            AppendProperty(builder, "status", result.Passed ? "PASS" : "FAIL", comma: true);
            AppendProperty(builder, "projectRoot", result.ProjectRoot, comma: true);
            AppendProperty(builder, "totalMarkdown", result.TotalMarkdownCount, comma: true);
            AppendProperty(builder, "activeMarkdown", result.ActiveMarkdownCount, comma: true);
            AppendProperty(builder, "activeHeaderDebt", result.ActiveHeaderDebt, comma: true);
            AppendProperty(builder, "activeMissingDate", result.ActiveMissingDateCount, comma: true);
            AppendProperty(builder, "activeMissingStatus", result.ActiveMissingStatusCount, comma: true);
            AppendProperty(builder, "directDocsHeaderMissing", result.DirectDocsHeaderMissingCount, comma: true);
            AppendProperty(builder, "architectureHeaderMissing", result.ArchitectureHeaderMissingCount, comma: true);
            AppendProperty(builder, "rootLooseTextLogCount", result.RootLooseTextLogCount, comma: true);
            AppendProperty(builder, "relocatedRootLogCount", result.RelocatedRootLogCount, comma: true);
            AppendProperty(builder, "maxAllowedActiveHeaderDebt", result.MaxAllowedActiveHeaderDebt, comma: true);
            AppendProperty(builder, "failureCount", result.FailureCount, comma: true);
            AppendProperty(builder, "telemetryWarningRequested", result.TelemetryWarningRequested, comma: true);
            AppendProperty(builder, "telemetryRuntimeEligible", result.TelemetryRuntimeEligible, comma: true);
            builder.AppendLine("  \"failures\": [");
            for (int i = 0; i < result.Failures.Count; i++)
            {
                builder.Append("    ");
                AppendJsonString(builder, result.Failures[i]);
                builder.AppendLine(i + 1 < result.Failures.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.Append('}');
            return builder.ToString();
        }

        public static string WriteStress(DocumentationAuthorityStressResult result)
        {
            StringBuilder builder = new StringBuilder(1536);
            DocumentationAuthorityAuditResult finalAudit = result.FinalAudit;
            builder.AppendLine("{");
            AppendProperty(builder, "status", result.Passed ? "PASS" : "FAIL", comma: true);
            AppendProperty(builder, "projectRoot", result.ProjectRoot, comma: true);
            AppendProperty(builder, "passCount", result.PassCount, comma: true);
            AppendProperty(builder, "failureCount", result.FailureCount, comma: true);
            AppendProperty(builder, "finalTotalMarkdown", finalAudit != null ? finalAudit.TotalMarkdownCount : 0, comma: true);
            AppendProperty(builder, "finalActiveMarkdown", finalAudit != null ? finalAudit.ActiveMarkdownCount : 0, comma: true);
            AppendProperty(builder, "finalActiveHeaderDebt", finalAudit != null ? finalAudit.ActiveHeaderDebt : 0, comma: true);
            AppendProperty(builder, "finalRootLooseTextLogCount", finalAudit != null ? finalAudit.RootLooseTextLogCount : 0, comma: true);
            builder.AppendLine("  \"failures\": [");
            for (int i = 0; i < result.Failures.Count; i++)
            {
                builder.Append("    ");
                AppendJsonString(builder, result.Failures[i]);
                builder.AppendLine(i + 1 < result.Failures.Count ? "," : string.Empty);
            }

            builder.AppendLine("  ]");
            builder.Append('}');
            return builder.ToString();
        }

        public static string WriteBatch(
            DocumentationAuthorityAuditResult auditResult,
            DocumentationAuthorityStressResult stressResult,
            bool passed)
        {
            DocumentationAuthorityAuditResult finalAudit = stressResult.FinalAudit;
            builderCache.Clear();
            StringBuilder builder = builderCache;
            builder.AppendLine("{");
            AppendProperty(builder, "status", passed ? "PASS" : "FAIL", comma: true);
            AppendProperty(builder, "smokeStatus", auditResult.Passed ? "PASS" : "FAIL", comma: true);
            AppendProperty(builder, "stressStatus", stressResult.Passed ? "PASS" : "FAIL", comma: true);
            AppendProperty(builder, "projectRoot", auditResult.ProjectRoot, comma: true);
            AppendProperty(builder, "totalMarkdown", auditResult.TotalMarkdownCount, comma: true);
            AppendProperty(builder, "activeMarkdown", auditResult.ActiveMarkdownCount, comma: true);
            AppendProperty(builder, "activeHeaderDebt", auditResult.ActiveHeaderDebt, comma: true);
            AppendProperty(builder, "activeMissingDate", auditResult.ActiveMissingDateCount, comma: true);
            AppendProperty(builder, "activeMissingStatus", auditResult.ActiveMissingStatusCount, comma: true);
            AppendProperty(builder, "directDocsHeaderMissing", auditResult.DirectDocsHeaderMissingCount, comma: true);
            AppendProperty(builder, "architectureHeaderMissing", auditResult.ArchitectureHeaderMissingCount, comma: true);
            AppendProperty(builder, "rootLooseTextLogCount", auditResult.RootLooseTextLogCount, comma: true);
            AppendProperty(builder, "relocatedRootLogCount", auditResult.RelocatedRootLogCount, comma: true);
            AppendProperty(builder, "stressPassCount", stressResult.PassCount, comma: true);
            AppendProperty(builder, "stressFailureCount", stressResult.FailureCount, comma: true);
            AppendProperty(builder, "finalStressTotalMarkdown", finalAudit != null ? finalAudit.TotalMarkdownCount : 0, comma: true);
            AppendProperty(builder, "finalStressActiveHeaderDebt", finalAudit != null ? finalAudit.ActiveHeaderDebt : 0, comma: true);
            AppendProperty(builder, "telemetryWarningRequested", auditResult.TelemetryWarningRequested, comma: true);
            AppendProperty(builder, "telemetryRuntimeEligible", auditResult.TelemetryRuntimeEligible, comma: true);
            AppendProperty(builder, "smokeArtifact", CodexArtifactPath, comma: true);
            AppendProperty(builder, "stressArtifact", CodexStressArtifactPath, comma: false);
            builder.Append('}');
            return builder.ToString();
        }

        public static void TryWriteArtifact(string projectRoot, string json)
        {
            string absolutePath = Path.Combine(projectRoot, ArtifactPath.Replace('/', Path.DirectorySeparatorChar));
            WriteArtifact(absolutePath, json);
        }

        public static void TryWriteStressArtifact(string projectRoot, string json)
        {
            string absolutePath = Path.Combine(projectRoot, StressArtifactPath.Replace('/', Path.DirectorySeparatorChar));
            WriteArtifact(absolutePath, json);
        }

        public static void TryWriteCodexArtifact(string projectRoot, string json)
        {
            string absolutePath = Path.Combine(projectRoot, CodexArtifactPath.Replace('/', Path.DirectorySeparatorChar));
            WriteArtifact(absolutePath, json);
        }

        public static void TryWriteCodexStressArtifact(string projectRoot, string json)
        {
            string absolutePath = Path.Combine(projectRoot, CodexStressArtifactPath.Replace('/', Path.DirectorySeparatorChar));
            WriteArtifact(absolutePath, json);
        }

        public static void TryWriteBatchArtifact(string projectRoot, string json)
        {
            string absolutePath = Path.Combine(projectRoot, BatchArtifactPath.Replace('/', Path.DirectorySeparatorChar));
            WriteArtifact(absolutePath, json);
        }

        private static void WriteArtifact(string absolutePath, string json)
        {
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, json, Encoding.UTF8);
        }

        private static void AppendProperty(StringBuilder builder, string name, string value, bool comma)
        {
            builder.Append("  ");
            AppendJsonString(builder, name);
            builder.Append(": ");
            AppendJsonString(builder, value);
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendProperty(StringBuilder builder, string name, bool value, bool comma)
        {
            builder.Append("  ");
            AppendJsonString(builder, name);
            builder.Append(": ");
            builder.Append(value ? "true" : "false");
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendProperty(StringBuilder builder, string name, int value, bool comma)
        {
            builder.Append("  ");
            AppendJsonString(builder, name);
            builder.Append(": ");
            builder.Append(value);
            if (comma)
                builder.Append(',');
            builder.AppendLine();
        }

        private static void AppendJsonString(StringBuilder builder, string value)
        {
            builder.Append('"');
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                switch (c)
                {
                    case '\\':
                        builder.Append("\\\\");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\n':
                        builder.Append("\\n");
                        break;
                    case '\r':
                        builder.Append("\\r");
                        break;
                    case '\t':
                        builder.Append("\\t");
                        break;
                    default:
                        builder.Append(c);
                        break;
                }
            }

            builder.Append('"');
        }

        // COLD ALLOC: StringBuilder[2048] - editor batch summary writer reuse - owner: DocumentationAuthoritySmokeTester
        private static readonly StringBuilder builderCache = new StringBuilder(2048);
    }
}
#endif
