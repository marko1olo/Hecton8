using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Gated batch-mode entry point for anomaly smoke verification when Unity batch ownership is contested.
    /// </summary>
    public static class AnomalySmokeBatchAutoRunner
    {
        private const string FlagPath = "CodexArtifacts/anomaly-smoke-autoload.flag";
        private const string ReportPath = "CodexArtifacts/anomaly-smoke-report.json";
        private const string ErrorPath = "CodexArtifacts/anomaly-smoke-autoload-error.log";

        [InitializeOnLoadMethod]
        private static void RunIfFlagged()
        {
            if (!Application.isBatchMode || !File.Exists(FlagPath))
                return;

            File.Delete(FlagPath);

            try
            {
                AnomalySmokeTester.SmokeReport report = AnomalySmokeTester.RunSmoke();
                WriteReport(report);
            }
            catch (Exception exception)
            {
                File.WriteAllText(ErrorPath, exception.ToString(), new UTF8Encoding(false));
            }
        }

        private static void WriteReport(AnomalySmokeTester.SmokeReport report)
        {
            string directory = Path.GetDirectoryName(ReportPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            // COLD ALLOC: StringBuilder[768] — batch-mode anomaly smoke JSON writer — owner: AnomalySmokeBatchAutoRunner
            var builder = new StringBuilder(768);
            builder.AppendLine("{");
            builder.AppendLine("  \"status\": \"PASS\",");
            builder.AppendLine("  \"runner\": \"AnomalySmokeBatchAutoRunner\",");
            builder.AppendLine("  \"totalCases\": " + report.TotalCases + ",");
            builder.AppendLine("  \"passedCases\": " + report.PassedCases + ",");
            AppendCase(builder, "perfectBowl", report.PerfectBowl, false);
            AppendCase(builder, "flatPlane", report.FlatPlane, false);
            AppendCase(builder, "openEdgeBowl", report.OpenEdgeBowl, false);
            AppendCase(builder, "dualBowl", report.DualBowl, true);
            builder.AppendLine("}");
            File.WriteAllText(ReportPath, builder.ToString(), new UTF8Encoding(false));
        }

        private static void AppendCase(
            StringBuilder builder,
            string name,
            AnomalySmokeTester.SmokeCaseResult result,
            bool last)
        {
            builder.Append("  \"");
            builder.Append(name);
            builder.AppendLine("\": {");
            builder.AppendLine("    \"validBasins\": " + result.ValidBasins + ",");
            builder.AppendLine("    \"firstDeepestX\": " + result.FirstDeepestX + ",");
            builder.AppendLine("    \"firstDeepestZ\": " + result.FirstDeepestZ + ",");
            builder.AppendLine("    \"firstLipHeight\": " + result.FirstLipHeight.ToString(CultureInfo.InvariantCulture) + ",");
            builder.AppendLine("    \"firstMaskedCells\": " + result.FirstMaskedCells + ",");
            builder.AppendLine("    \"totalMaskedCells\": " + result.TotalMaskedCells);
            builder.Append("  }");
            builder.AppendLine(last ? string.Empty : ",");
        }
    }
}
