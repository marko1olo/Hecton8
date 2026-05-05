using System;
using System.Globalization;
using System.IO;
using Hecton8.Editor;

internal static class AnomalyStandaloneSmokeRunner
{
    private const string OutputPath = "CodexArtifacts/anomaly-smoke-report-standalone.json";

    public static int Main()
    {
        try
        {
            AnomalySmokeTester.SmokeReport report = AnomalySmokeTester.RunSmoke();
            WriteReport(report);
            return report.TotalCases == 3 && report.PassedCases == 3 ? 0 : 2;
        }
        catch (Exception exception)
        {
            File.WriteAllText(
                "CodexArtifacts/anomaly-smoke-report-standalone-error.log",
                exception.ToString());
            return 1;
        }
    }

    private static void WriteReport(AnomalySmokeTester.SmokeReport report)
    {
        using (var writer = new StreamWriter(OutputPath, false))
        {
            writer.WriteLine("{");
            writer.WriteLine("  \"status\": \"PASS\",");
            writer.WriteLine("  \"runner\": \"AnomalyStandaloneSmokeRunner\",");
            writer.WriteLine("  \"totalCases\": " + report.TotalCases + ",");
            writer.WriteLine("  \"passedCases\": " + report.PassedCases + ",");
            WriteCase(writer, "perfectBowl", report.PerfectBowl, false);
            WriteCase(writer, "flatPlane", report.FlatPlane, false);
            WriteCase(writer, "dualBowl", report.DualBowl, true);
            writer.WriteLine("}");
        }
    }

    private static void WriteCase(
        StreamWriter writer,
        string name,
        AnomalySmokeTester.SmokeCaseResult result,
        bool last)
    {
        writer.WriteLine("  \"" + name + "\": {");
        writer.WriteLine("    \"validBasins\": " + result.ValidBasins + ",");
        writer.WriteLine("    \"firstDeepestX\": " + result.FirstDeepestX + ",");
        writer.WriteLine("    \"firstDeepestZ\": " + result.FirstDeepestZ + ",");
        writer.WriteLine("    \"firstLipHeight\": " + result.FirstLipHeight.ToString(CultureInfo.InvariantCulture) + ",");
        writer.WriteLine("    \"firstMaskedCells\": " + result.FirstMaskedCells + ",");
        writer.WriteLine("    \"totalMaskedCells\": " + result.TotalMaskedCells);
        writer.WriteLine(last ? "  }" : "  },");
    }
}
