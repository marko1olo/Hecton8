#if UNITY_EDITOR
using System;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace Hecton8.Optimization.Editor
{
    public static class VRAMTextureFootprintScanner1617
    {
        private const string ProjectAssetRoot = "Assets/_Project";
        private const string ReportRelativePath = "Docs/AgentLogs/VRAM_Texture_Ledger_1617.md";
        private const long TextureBudgetBytes = 900L * 1024L * 1024L;
        private const long Mx350WarnBytes = 1600L * 1024L * 1024L;
        private const int MaxRows = 512;

        private static readonly string[] s_projectRoots = { ProjectAssetRoot };

        [MenuItem("Hecton8/Validation/Agent 1617/Scan Texture VRAM Footprint")]
        public static void ScanTextureFootprint()
        {
            TextureScanSummary summary = ScanTextures(out StringBuilder report);
            WriteReport(report);
            Hecton8.Core.H8Debug.Log(
                "[VRAMTextureFootprintScanner1617] Scanned textures=" +
                summary.TextureCount.ToString(CultureInfo.InvariantCulture) +
                " bytes=" +
                summary.TotalTextureBytes.ToString(CultureInfo.InvariantCulture) +
                " largest=" +
                summary.LargestPath);
        }

        private static TextureScanSummary ScanTextures(out StringBuilder report)
        {
            string[] textureGuids = AssetDatabase.FindAssets("t:Texture", s_projectRoots);
            report = new StringBuilder(65536);
            report.AppendLine("# VRAM Texture Ledger 1617");
            report.AppendLine();
            report.AppendLine("Budget: texture storage <= 900 MB, compact total VRAM warning <= 1600 MB.");
            report.AppendLine();
            report.AppendLine("| Path | Bytes | MB | Width | Height | Format | Mips | Streaming |");
            report.AppendLine("|---|---:|---:|---:|---:|---|---:|---|");

            TextureScanSummary summary = default;
            int rows = 0;
            for (int i = 0; i < textureGuids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(textureGuids[i]);
                Texture texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
                if (texture == null)
                    continue;

                long bytes = Profiler.GetRuntimeMemorySizeLong(texture);
                if (bytes <= 0L)
                    bytes = EstimateTextureBytes(texture);

                summary.TextureCount++;
                summary.TotalTextureBytes += bytes;
                if (bytes > summary.LargestBytes)
                {
                    summary.LargestBytes = bytes;
                    summary.LargestPath = path;
                }

                if (rows >= MaxRows)
                    continue;

                AppendTextureRow(report, path, texture, bytes);
                rows++;
            }

            report.AppendLine();
            report.AppendLine("## Summary");
            report.AppendLine();
            report.AppendLine("- Texture count: " + summary.TextureCount.ToString(CultureInfo.InvariantCulture));
            report.AppendLine("- Texture bytes: " + summary.TotalTextureBytes.ToString(CultureInfo.InvariantCulture));
            report.AppendLine("- Texture MB: " + BytesToMegabytes(summary.TotalTextureBytes).ToString("F2", CultureInfo.InvariantCulture));
            report.AppendLine("- Texture budget MB: " + BytesToMegabytes(TextureBudgetBytes).ToString("F2", CultureInfo.InvariantCulture));
            report.AppendLine("- MX350 warning MB: " + BytesToMegabytes(Mx350WarnBytes).ToString("F2", CultureInfo.InvariantCulture));
            report.AppendLine("- Largest texture: " + (summary.LargestPath ?? string.Empty));
            report.AppendLine("- Rows emitted: " + rows.ToString(CultureInfo.InvariantCulture));
            return summary;
        }

        private static void AppendTextureRow(StringBuilder report, string path, Texture texture, long bytes)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            int mipCount = texture is Texture2D texture2D ? texture2D.mipmapCount : 1;
            string streaming = importer != null && importer.streamingMipmaps ? "yes" : "no";
            string format = texture is Texture2D texture2DFormat
                ? texture2DFormat.format.ToString()
                : texture.graphicsFormat.ToString();

            report.Append("| ");
            report.Append(path.Replace("|", "_"));
            report.Append(" | ");
            report.Append(bytes.ToString(CultureInfo.InvariantCulture));
            report.Append(" | ");
            report.Append(BytesToMegabytes(bytes).ToString("F2", CultureInfo.InvariantCulture));
            report.Append(" | ");
            report.Append(texture.width.ToString(CultureInfo.InvariantCulture));
            report.Append(" | ");
            report.Append(texture.height.ToString(CultureInfo.InvariantCulture));
            report.Append(" | ");
            report.Append(format);
            report.Append(" | ");
            report.Append(mipCount.ToString(CultureInfo.InvariantCulture));
            report.Append(" | ");
            report.Append(streaming);
            report.AppendLine(" |");
        }

        private static long EstimateTextureBytes(Texture texture)
        {
            long pixels = (long)Math.Max(1, texture.width) * Math.Max(1, texture.height);
            return pixels * 4L;
        }

        private static float BytesToMegabytes(long bytes)
        {
            return bytes / (1024f * 1024f);
        }

        private static void WriteReport(StringBuilder report)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string path = Path.Combine(projectRoot, ReportRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, report.ToString());
        }

        private struct TextureScanSummary
        {
            public int TextureCount;
            public long TotalTextureBytes;
            public long LargestBytes;
            public string LargestPath;
        }
    }
}
#endif
