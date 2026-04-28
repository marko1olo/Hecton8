using System;
using System.IO;
using System.Text;
using Hecton.Localization;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Generates hash-only localization key constants from a mocked CSV source.
    /// </summary>
    public static class LocKeysGenerator
    {
        private const string DefaultOutputPath = "Assets/_Project/Scripts/LocKeys.Generated.cs";
        private const string DefaultMockCsvPath = "Assets/_Project/Data/Localization/loc_keys_mock.csv";
        private const string MockCsvContent =
@"symbol,key
HUD_HEALTH,hud.health
HUD_OXYGEN,hud.oxygen
HUD_DEPTH,hud.depth
HUD_POWER,hud.power
HUD_STATUS,hud.status";

        [MenuItem("Hecton/Localization/Generate LocKeys From Mock CSV")]
        public static void GenerateFromMockCsv()
        {
            string csv = File.Exists(DefaultMockCsvPath)
                ? File.ReadAllText(DefaultMockCsvPath)
                : MockCsvContent;

            WriteGeneratedFile(csv, DefaultOutputPath);
            AssetDatabase.Refresh();
            Debug.Log($"[LocKeysGenerator] Generated {DefaultOutputPath}.");
        }

        internal static void WriteGeneratedFile(string csvContent, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(512);
            builder.AppendLine("// AUTO-GENERATED. DO NOT EDIT.");
            builder.AppendLine("namespace Hecton.Localization");
            builder.AppendLine("{");
            builder.AppendLine("    public static class LocKeys");
            builder.AppendLine("    {");

            string[] lines = csvContent.Replace("\r", string.Empty).Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                ReadOnlySpan<char> line = lines[i].AsSpan().Trim();
                if (line.Length == 0)
                    continue;

                int commaIndex = line.IndexOf(',');
                if (commaIndex <= 0 || commaIndex >= line.Length - 1)
                    continue;

                string symbol = line.Slice(0, commaIndex).Trim().ToString();
                string key = line.Slice(commaIndex + 1).Trim().ToString();
                if (string.IsNullOrWhiteSpace(symbol) || string.IsNullOrWhiteSpace(key))
                    continue;

                builder.Append("        public static readonly int ");
                builder.Append(symbol);
                builder.Append(" = LocHash.Compute(\"");
                builder.Append(key);
                builder.AppendLine("\");");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");
            File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);
        }
    }
}
