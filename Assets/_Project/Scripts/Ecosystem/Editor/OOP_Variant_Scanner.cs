#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Hecton8.Editor
{
    public static class OOP_Variant_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/SHINOBU_306_OOP_VARIANT_SCANNER.json";
        private const string RenderingReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";

        [MenuItem("Hecton8/Ecosystem/Run Fauna OOP Variant Scanner")]
        public static void RunMenu()
        {
            RunAndWriteReport();
        }

        public static void RunAndWriteReport()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            ScannerCounts counts = default;
            ScanDirectory(Path.Combine(Application.dataPath, "_Project/Scripts/Fauna"), ref counts);
            ScanDirectory(Path.Combine(Application.dataPath, "_Project/Scripts/Ecosystem"), ref counts);
            ScanDirectory(Path.Combine(Application.dataPath, "_Project/Scripts/AI/Ecosystem"), ref counts);
            WriteSidecarReport(projectRoot, in counts);
            UpsertRenderingReport(projectRoot, in counts);
            AssetDatabase.Refresh();
        }

        private static void ScanDirectory(string directory, ref ScannerCounts counts)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
                return;

            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
                ScanFile(files[i], ref counts);
        }

        private static void ScanFile(string path, ref ScannerCounts counts)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.IndexOf("/Editor/", StringComparison.Ordinal) >= 0)
                return;

            counts.FileCount++;
            string[] lines = File.ReadAllLines(path);
            bool shinobuBalancer = normalized.EndsWith("/ShinobuEcosystemBalancer.cs", StringComparison.Ordinal);
            for (int i = 0; i < lines.Length; i++)
            {
                bool geneticLine = IsGeneticRouteLine(normalized, lines[i]);
                CountToken(lines[i], "new Material(", geneticLine, ref counts.NewMaterialCount, ref counts.GeneticNewMaterialCount);
                CountToken(lines[i], ".material", geneticLine, ref counts.MaterialPropertyCount, ref counts.GeneticMaterialPropertyCount);
                CountToken(lines[i], ".color =", geneticLine, ref counts.ColorAssignmentCount, ref counts.GeneticColorAssignmentCount);
                CountToken(lines[i], "SetColor(", geneticLine, ref counts.SetColorCount, ref counts.GeneticSetColorCount);
                CountToken(lines[i], "Random.ColorHSV", geneticLine, ref counts.RandomColorCount, ref counts.GeneticRandomColorCount);
                CountToken(lines[i], "Random.Range", geneticLine, ref counts.RandomRangeCount, ref counts.GeneticRandomRangeCount);
                if (!shinobuBalancer)
                    continue;

                CountCompilerToken(lines[i], "math.asuint(aup.Local", ref counts.DivergentAupFloatHashCount);
                CountCompilerToken(lines[i], "uint low = random.NextUInt()", ref counts.RandomLowHighMaskCompilerCount);
                CountCompilerToken(lines[i], "uint high = random.NextUInt()", ref counts.RandomLowHighMaskCompilerCount);
                CountCompilerToken(lines[i], "PackFaunaGeneticMask", ref counts.LocalPackHelperCount);
                CountCompilerToken(lines[i], "FoldFnv32(", ref counts.LocalPackHelperCount);
                CountCompilerToken(lines[i], "QuantizeMetersToMillimeters(", ref counts.LocalPackHelperCount);
                CountCompilerToken(lines[i], "return Hecton8.Ecosystem.FaunaGenome64.", ref counts.WrapperDelegateReturnCount);
                CountCompilerToken(lines[i], "Hecton8.Ecosystem.FaunaGenome64.BuildAupSeed(", ref counts.DirectFaunaGenome64CallCount);
                CountCompilerToken(lines[i], "Hecton8.Ecosystem.FaunaGenome64.BuildStableEntitySeed(", ref counts.DirectFaunaGenome64CallCount);
                CountCompilerToken(lines[i], "Hecton8.Ecosystem.FaunaGenome64.CompileGeneticMaskFromSeed(", ref counts.DirectFaunaGenome64CallCount);
            }
        }

        private static bool IsGeneticRouteLine(string normalizedPath, string line)
        {
            return normalizedPath.IndexOf("FaunaGenome", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   normalizedPath.IndexOf("FaunaGenetic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Genetic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Genome", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("_H8FaunaGenetic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("BoidCustomDataDTO", StringComparison.Ordinal) >= 0;
        }

        private static void CountToken(
            string text,
            string token,
            bool geneticLine,
            ref int totalCount,
            ref int geneticCount)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return;

            int cursor = 0;
            while (cursor < text.Length)
            {
                int index = text.IndexOf(token, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                totalCount++;
                if (geneticLine)
                    geneticCount++;
                cursor = index + token.Length;
            }
        }

        private static void CountCompilerToken(string text, string token, ref int totalCount)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return;

            int cursor = 0;
            while (cursor < text.Length)
            {
                int index = text.IndexOf(token, cursor, StringComparison.Ordinal);
                if (index < 0)
                    break;

                totalCount++;
                cursor = index + token.Length;
            }
        }

        private static void WriteSidecarReport(string projectRoot, in ScannerCounts counts)
        {
            string path = Path.Combine(projectRoot, ReportRelativePath);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, BuildJson(in counts));
        }

        private static void UpsertRenderingReport(string projectRoot, in ScannerCounts counts)
        {
            string path = Path.Combine(projectRoot, RenderingReportRelativePath);
            if (!File.Exists(path))
            {
                File.WriteAllText(path, "{\n  \"shinobu_306_oop_variant_scanner\": " + BuildJson(in counts) + "\n}\n");
                return;
            }

            string report = File.ReadAllText(path);
            string json = BuildJson(in counts);
            if (TryReplaceExistingSection(report, json, out string replacedReport))
            {
                File.WriteAllText(path, replacedReport);
                return;
            }

            int insert = report.LastIndexOf('}');
            if (insert < 0)
                return;

            string prefix = report.Substring(0, insert).TrimEnd();
            string suffix = report.Substring(insert);
            string separator = prefix.EndsWith("{", StringComparison.Ordinal) ? "\n" : ",\n";
            File.WriteAllText(path, prefix + separator + "  \"shinobu_306_oop_variant_scanner\": " + json + "\n" + suffix);
        }

        private static bool TryReplaceExistingSection(string report, string json, out string replacedReport)
        {
            replacedReport = report;
            int key = report.IndexOf("\"shinobu_306_oop_variant_scanner\"", StringComparison.Ordinal);
            if (key < 0)
                return false;

            int colon = report.IndexOf(':', key);
            if (colon < 0)
                return false;

            int objectStart = report.IndexOf('{', colon);
            if (objectStart < 0)
                return false;

            int objectEnd = FindMatchingBrace(report, objectStart);
            if (objectEnd < objectStart)
                return false;

            replacedReport = report.Substring(0, objectStart) + json + report.Substring(objectEnd + 1);
            return true;
        }

        private static int FindMatchingBrace(string text, int objectStart)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                        continue;
                    }

                    if (c == '\\')
                    {
                        escaped = true;
                        continue;
                    }

                    if (c == '"')
                        inString = false;
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
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return i;
            }

            return -1;
        }

        private static string BuildJson(in ScannerCounts counts)
        {
            bool geneticRouteEradicated = counts.GeneticNewMaterialCount == 0 &&
                                          counts.GeneticMaterialPropertyCount == 0 &&
                                          counts.GeneticColorAssignmentCount == 0 &&
                                          counts.GeneticSetColorCount == 0 &&
                                          counts.GeneticRandomColorCount == 0 &&
                                          counts.GeneticRandomRangeCount == 0;
            bool compilerRouteClean = counts.DivergentAupFloatHashCount == 0 &&
                                      counts.RandomLowHighMaskCompilerCount == 0 &&
                                      counts.LocalPackHelperCount == 0;
            int nonGeneticResidueCount =
                counts.NewMaterialCount +
                counts.MaterialPropertyCount +
                counts.ColorAssignmentCount +
                counts.SetColorCount +
                counts.RandomColorCount +
                counts.RandomRangeCount -
                counts.GeneticNewMaterialCount -
                counts.GeneticMaterialPropertyCount -
                counts.GeneticColorAssignmentCount -
                counts.GeneticSetColorCount -
                counts.GeneticRandomColorCount -
                counts.GeneticRandomRangeCount;
            StringBuilder builder = new StringBuilder(512);
            builder.Append("{\n");
            builder.Append("    \"agent\": \"SHINOBU_306\",\n");
            builder.Append("    \"scanner\": \"OOP_Variant_Scanner_STATIC_PS\",\n");
            builder.Append("    \"evidenceClass\": \"STATIC_SOURCE_TARGETED_EDITOR_COMPANION\",\n");
            builder.Append("    \"fileCount\": ").Append(counts.FileCount).Append(",\n");
            builder.Append("    \"newMaterialCount\": ").Append(counts.NewMaterialCount).Append(",\n");
            builder.Append("    \"materialPropertyCount\": ").Append(counts.MaterialPropertyCount).Append(",\n");
            builder.Append("    \"colorAssignmentCount\": ").Append(counts.ColorAssignmentCount).Append(",\n");
            builder.Append("    \"setColorCount\": ").Append(counts.SetColorCount).Append(",\n");
            builder.Append("    \"randomColorCount\": ").Append(counts.RandomColorCount).Append(",\n");
            builder.Append("    \"randomRangeCount\": ").Append(counts.RandomRangeCount).Append(",\n");
            builder.Append("    \"geneticNewMaterialCount\": ").Append(counts.GeneticNewMaterialCount).Append(",\n");
            builder.Append("    \"geneticMaterialPropertyCount\": ").Append(counts.GeneticMaterialPropertyCount).Append(",\n");
            builder.Append("    \"geneticColorAssignmentCount\": ").Append(counts.GeneticColorAssignmentCount).Append(",\n");
            builder.Append("    \"geneticSetColorCount\": ").Append(counts.GeneticSetColorCount).Append(",\n");
            builder.Append("    \"geneticRandomColorCount\": ").Append(counts.GeneticRandomColorCount).Append(",\n");
            builder.Append("    \"geneticRandomRangeCount\": ").Append(counts.GeneticRandomRangeCount).Append(",\n");
            builder.Append("    \"divergentAupFloatHashCount\": ").Append(counts.DivergentAupFloatHashCount).Append(",\n");
            builder.Append("    \"randomLowHighMaskCompilerCount\": ").Append(counts.RandomLowHighMaskCompilerCount).Append(",\n");
            builder.Append("    \"localPackHelperCount\": ").Append(counts.LocalPackHelperCount).Append(",\n");
            builder.Append("    \"wrapperDelegateReturnCount\": ").Append(counts.WrapperDelegateReturnCount).Append(",\n");
            builder.Append("    \"directFaunaGenome64CallCount\": ").Append(counts.DirectFaunaGenome64CallCount).Append(",\n");
            builder.Append("    \"nonGeneticResidueCount\": ").Append(nonGeneticResidueCount).Append(",\n");
            builder.Append("    \"summary\": \"Genetic OOP Material Mutations ").Append(geneticRouteEradicated ? "Eradicated" : "Residue Detected");
            if (nonGeneticResidueCount > 0)
                builder.Append("; non-genetic presentation residue counted separately");
            builder.Append("; Genetic Compiler Route ").Append(compilerRouteClean ? "Clean" : "Drift Detected");
            builder.Append("\"\n");
            builder.Append("  }");
            return builder.ToString();
        }

        private struct ScannerCounts
        {
            public int FileCount;
            public int NewMaterialCount;
            public int MaterialPropertyCount;
            public int ColorAssignmentCount;
            public int SetColorCount;
            public int RandomColorCount;
            public int RandomRangeCount;
            public int GeneticNewMaterialCount;
            public int GeneticMaterialPropertyCount;
            public int GeneticColorAssignmentCount;
            public int GeneticSetColorCount;
            public int GeneticRandomColorCount;
            public int GeneticRandomRangeCount;
            public int DivergentAupFloatHashCount;
            public int RandomLowHighMaskCompilerCount;
            public int LocalPackHelperCount;
            public int WrapperDelegateReturnCount;
            public int DirectFaunaGenome64CallCount;
        }
    }
}
#endif
