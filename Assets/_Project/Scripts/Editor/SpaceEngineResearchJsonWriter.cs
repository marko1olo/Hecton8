#if UNITY_EDITOR
// ============================================================================
// HECTON-8 - SpaceEngineResearchJsonWriter.cs
// Editor-only JSON artifact writer for SpaceEngine research validation.
// ============================================================================

using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Hecton8.EditorTools
{
    internal static class SpaceEngineResearchJsonWriter
    {
        private static readonly Encoding ArtifactEncoding = new UTF8Encoding(false);

        public static string Write(SpaceEngineResearchAuditResult result)
        {
            StringBuilder builder = new StringBuilder(4096);
            AppendAudit(builder, result);
            return builder.ToString();
        }

        public static string WriteStress(SpaceEngineResearchStressResult result)
        {
            StringBuilder builder = new StringBuilder(8192);
            builder.Append('{');
            AppendProperty(builder, "status", result.Passed ? "OMEGA_VERIFIED" : "FAILED");
            builder.Append(',');
            AppendProperty(builder, "projectRoot", result.ProjectRoot);
            builder.Append(',');
            AppendProperty(builder, "spaceEngineRoot", result.SpaceEngineRoot);
            builder.Append(',');
            AppendProperty(builder, "passCount", result.PassCount);
            builder.Append(',');
            AppendProperty(builder, "failureCount", result.FailureCount);
            builder.Append(',');
            AppendStringArray(builder, "failures", result.Failures);
            builder.Append(',');
            builder.Append("\"finalAudit\":");
            AppendAudit(builder, result.FinalAudit);
            builder.Append('}');
            return builder.ToString();
        }

        public static void TryWriteArtifact(string projectRoot, string json)
        {
            string outputPath = SpaceEngineResearchSmokeTester.ResolveOutputPath(projectRoot);
            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(outputPath, json, ArtifactEncoding);
        }

        private static void AppendAudit(StringBuilder builder, SpaceEngineResearchAuditResult result)
        {
            if (result == null)
            {
                builder.Append("null");
                return;
            }

            builder.Append('{');
            AppendProperty(builder, "status", result.Passed ? "PASS" : "FAIL");
            builder.Append(',');
            AppendProperty(builder, "projectRoot", result.ProjectRoot);
            builder.Append(',');
            AppendProperty(builder, "spaceEngineRoot", result.SpaceEngineRoot);
            builder.Append(',');
            AppendProperty(builder, "reportLineCount", result.ReportLineCount);
            builder.Append(',');
            AppendProperty(builder, "maxReportLineCount", result.MaxReportLineCount);
            builder.Append(',');
            AppendProperty(builder, "referenceKernelFileCount", result.ReferenceKernelFileCount);
            builder.Append(',');
            AppendProperty(builder, "editorValidationFileCount", result.EditorValidationFileCount);
            builder.Append(',');
            AppendProperty(builder, "maxEditorValidationLineCount", result.MaxEditorValidationLineCount);
            builder.Append(',');
            AppendProperty(builder, "noPasswordProbeStatus", result.NoPasswordProbeStatus);
            builder.Append(',');
            AppendProperty(builder, "nativeCollectionTokenCount", result.NativeCollectionTokenCount);
            builder.Append(',');
            AppendProperty(builder, "jobBarrierTokenCount", result.JobBarrierTokenCount);
            builder.Append(',');
            AppendProperty(builder, "staticInstanceTokenCount", result.StaticInstanceTokenCount);
            builder.Append(',');
            AppendProperty(builder, "hotPathStringTokenCount", result.HotPathStringTokenCount);
            builder.Append(',');
            AppendProperty(builder, "recentScopeRuntimeCsFileCount", result.RecentScopeRuntimeCsFileCount);
            builder.Append(',');
            AppendProperty(builder, "recentScopeRuntimeNativeCollectionCount", result.RecentScopeRuntimeNativeCollectionCount);
            builder.Append(',');
            AppendProperty(builder, "telemetryWarningRequested", result.TelemetryWarningRequested);
            builder.Append(',');
            AppendProperty(builder, "telemetryRuntimeEligible", result.TelemetryRuntimeEligible);
            builder.Append(',');
            AppendProperty(builder, "failureCount", result.FailureCount);
            builder.Append(',');
            AppendStringArray(builder, "failures", result.Failures);
            builder.Append(',');
            builder.Append("\"shaderPak\":");
            AppendZipProbe(builder, result.ShaderPak);
            builder.Append(',');
            builder.Append("\"atmospherePak\":");
            AppendZipProbe(builder, result.AtmospherePak);
            builder.Append(',');
            builder.Append("\"catalogPak\":");
            AppendZipProbe(builder, result.CatalogPak);
            builder.Append('}');
        }

        private static void AppendZipProbe(StringBuilder builder, SpaceEngineZipProbeResult result)
        {
            builder.Append('{');
            AppendProperty(builder, "path", result.Path);
            builder.Append(',');
            AppendProperty(builder, "exists", result.Exists);
            builder.Append(',');
            AppendProperty(builder, "entryCount", result.EntryCount);
            builder.Append(',');
            AppendProperty(builder, "encryptedEntryCount", result.EncryptedEntryCount);
            builder.Append(',');
            AppendProperty(builder, "compressedEntryCount", result.CompressedEntryCount);
            builder.Append(',');
            AppendProperty(builder, "storedEntryCount", result.StoredEntryCount);
            builder.Append(',');
            AppendProperty(builder, "expectedEntryCount", result.ExpectedEntryCount);
            builder.Append(',');
            AppendProperty(builder, "expectedFoundCount", result.ExpectedFoundCount);
            builder.Append(',');
            AppendProperty(builder, "expectedMissingCount", result.ExpectedMissingCount);
            builder.Append(',');
            AppendProperty(builder, "parseError", result.ParseError);
            builder.Append('}');
        }

        private static void AppendProperty(StringBuilder builder, string name, string value)
        {
            builder.Append('"');
            builder.Append(name);
            builder.Append("\":\"");
            AppendEscaped(builder, value);
            builder.Append('"');
        }

        private static void AppendProperty(StringBuilder builder, string name, int value)
        {
            builder.Append('"');
            builder.Append(name);
            builder.Append("\":");
            builder.Append(value);
        }

        private static void AppendProperty(StringBuilder builder, string name, bool value)
        {
            builder.Append('"');
            builder.Append(name);
            builder.Append("\":");
            builder.Append(value ? "true" : "false");
        }

        private static void AppendStringArray(StringBuilder builder, string name, List<string> values)
        {
            builder.Append('"');
            builder.Append(name);
            builder.Append("\":[");
            for (int i = 0; i < values.Count; i++)
            {
                if (i > 0)
                    builder.Append(',');

                builder.Append('"');
                AppendEscaped(builder, values[i]);
                builder.Append('"');
            }

            builder.Append(']');
        }

        private static void AppendEscaped(StringBuilder builder, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

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
                else
                    builder.Append(c);
            }
        }
    }
}
#endif
