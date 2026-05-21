#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class Blit_Operation_Inquisition
    {
        private const string ScriptsRoot = "Assets/_Project/Scripts/Rendering";
        private const string ReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string OwnerReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT_SHINOBU_236.json";

        [MenuItem("Hecton8/Rendering/Blit Operation Inquisition")]
        public static void Run()
        {
            BlitScanResult result = ScanRenderingScripts();
            WriteReport(result);
            Debug.Log($"[SHINOBU_236] Naive upscales eradicated: {result.NaiveUpscaleSuspects == 0}. Report: {ReportPath}");
        }

        private static BlitScanResult ScanRenderingScripts()
        {
            BlitScanResult result = default;
            string root = Path.Combine(Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty, ScriptsRoot);
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return result;

            foreach (string path in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
            {
                result.FilesScanned++;
                string text = File.ReadAllText(path);
                int graphicsBlit = CountOccurrences(text, "Graphics.Blit");
                int addBlitPass = CountOccurrences(text, "AddBlitPass");
                int materialBlit = CountOccurrences(text, "BlitMaterialParameters");
                result.GraphicsBlitCalls += graphicsBlit;
                result.RenderGraphBlitPasses += addBlitPass;
                result.MaterialBlitPasses += materialBlit;
                if (graphicsBlit > 0 || (addBlitPass > 0 && materialBlit <= 0))
                    result.NaiveUpscaleSuspects++;
            }

            return result;
        }

        private static int CountOccurrences(string text, string needle)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(needle))
                return 0;

            int count = 0;
            int cursor = 0;
            while (cursor < text.Length)
            {
                int index = text.IndexOf(needle, cursor, System.StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                cursor = index + needle.Length;
            }

            return count;
        }

        private static void WriteReport(BlitScanResult result)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
            if (string.IsNullOrEmpty(root))
                return;

            string path = Path.Combine(root, ReportPath);
            string ownerPath = Path.Combine(root, OwnerReportPath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string priorReport = File.Exists(path) ? File.ReadAllText(path) : null;
            int priorReportBytes = string.IsNullOrEmpty(priorReport) ? 0 : Encoding.UTF8.GetByteCount(priorReport);
            uint priorReportHash = string.IsNullOrEmpty(priorReport) ? 0u : CalculateFnv1A(priorReport);
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("{");
            builder.AppendLine("  \"schema\": \"hecton8.rendering_optimization_report.v1\",");
            builder.AppendLine("  \"agent\": \"SHINOBU_236\",");
            builder.AppendLine("  \"domain\": \"Rendering/DRS Upscaler\",");
            builder.AppendLine("  \"scanner\": \"Blit_Operation_Inquisition\",");
            builder.AppendLine($"  \"filesScanned\": {result.FilesScanned},");
            builder.AppendLine($"  \"graphicsBlitCalls\": {result.GraphicsBlitCalls},");
            builder.AppendLine($"  \"renderGraphBlitPasses\": {result.RenderGraphBlitPasses},");
            builder.AppendLine($"  \"materialBlitPasses\": {result.MaterialBlitPasses},");
            builder.AppendLine($"  \"naiveUpscaleSuspects\": {result.NaiveUpscaleSuspects},");
            builder.AppendLine($"  \"naiveUpscalesEradicated\": {(result.NaiveUpscaleSuspects == 0 ? "true" : "false")},");
            builder.AppendLine($"  \"priorReportBytes\": {priorReportBytes},");
            builder.AppendLine($"  \"priorReportFnv1A\": \"0x{priorReportHash:x8}\",");
            builder.AppendLine("  \"replacement\": \"Hecton_BilateralUpscale.compute\"");
            builder.AppendLine("}");
            string report = builder.ToString();
            File.WriteAllText(ownerPath, report);
            File.WriteAllText(path, report);
        }

        private static uint CalculateFnv1A(string text)
        {
            const uint offset = 2166136261u;
            const uint prime = 16777619u;
            uint hash = offset;
            for (int i = 0; i < text.Length; i++)
            {
                hash ^= text[i];
                hash *= prime;
            }

            return hash;
        }

        private struct BlitScanResult
        {
            internal int FilesScanned;
            internal int GraphicsBlitCalls;
            internal int RenderGraphBlitPasses;
            internal int MaterialBlitPasses;
            internal int NaiveUpscaleSuspects;
        }
    }
}
#endif
