using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Hecton.Localization;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Generates hash-only localization key constants from the authoritative English JSON table.
    /// </summary>
    public static class LocKeysGenerator
    {
        private const string DefaultOutputPath = "Assets/_Project/Scripts/LocKeys.Generated.cs";
        private const string DefaultEnglishJsonPath = "Assets/_Project/Scripts/English.json";

        [MenuItem("Hecton/Localization/Generate LocKeys From English JSON")]
        public static void GenerateFromEnglishJson()
        {
            WriteGeneratedFileFromJson(DefaultEnglishJsonPath, DefaultOutputPath);
            AssetDatabase.Refresh();
            Debug.Log($"[LocKeysGenerator] Generated {DefaultOutputPath}.");
        }

        internal static void WriteGeneratedFileFromJson(string jsonPath, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(jsonPath))
                throw new ArgumentException("Source JSON path is required.", nameof(jsonPath));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));

            if (!File.Exists(jsonPath))
                throw new FileNotFoundException("Localization source JSON was not found.", jsonPath);

            Dictionary<string, string> table = LocalizationManager.ParseFlatJsonTable(File.ReadAllText(jsonPath, Encoding.UTF8));
            var keys = new List<string>(table.Keys.Count);
            foreach (KeyValuePair<string, string> entry in table)
            {
                if (!string.IsNullOrWhiteSpace(entry.Key))
                    keys.Add(entry.Key);
            }

            keys.Sort(StringComparer.Ordinal);
            WriteGeneratedFile(keys, outputPath);
        }

        internal static void WriteGeneratedFile(IReadOnlyList<string> keys, string outputPath)
        {
            if (keys == null)
                throw new ArgumentNullException(nameof(keys));

            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));

            string directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            StringBuilder builder = new StringBuilder(Math.Max(1024, keys.Count * 96));
            builder.AppendLine("// AUTO-GENERATED. DO NOT EDIT.");
            builder.AppendLine("namespace Hecton.Localization");
            builder.AppendLine("{");
            builder.AppendLine("    public static class LocKeys");
            builder.AppendLine("    {");

            var usedSymbols = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[i];
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                string symbol = ToSymbol(key);
                if (!usedSymbols.Add(symbol))
                {
                    symbol = symbol + "_" + unchecked((uint)LocHash.Compute(key)).ToString("X8");
                    usedSymbols.Add(symbol);
                }

                builder.Append("        public static readonly int ");
                builder.Append(symbol);
                builder.Append(" = LocHash.Compute(\"");
                builder.Append(EscapeStringLiteral(key));
                builder.AppendLine("\");");
            }

            builder.AppendLine("    }");
            builder.AppendLine("}");
            File.WriteAllText(outputPath, builder.ToString(), Encoding.UTF8);
        }

        private static string ToSymbol(string key)
        {
            StringBuilder builder = new StringBuilder(key.Length + 1);
            char first = key[0];
            if (IsIdentifierStart(first))
                builder.Append(first);
            else
                builder.Append('_');

            for (int i = 1; i < key.Length; i++)
            {
                char value = key[i];
                builder.Append(IsIdentifierPart(value) ? value : '_');
            }

            return builder.ToString();
        }

        private static bool IsIdentifierStart(char value)
        {
            return value == '_' || (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z');
        }

        private static bool IsIdentifierPart(char value)
        {
            return IsIdentifierStart(value) || (value >= '0' && value <= '9');
        }

        private static string EscapeStringLiteral(string value)
        {
            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char symbol = value[i];
                if (symbol == '\\' || symbol == '"')
                    builder.Append('\\');

                builder.Append(symbol);
            }

            return builder.ToString();
        }

        [Obsolete("Use WriteGeneratedFileFromJson so generated keys match the shipped localization table.")]
        internal static void WriteGeneratedFile(string csvContent, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(csvContent))
                throw new ArgumentException("CSV content is required.", nameof(csvContent));

            var keys = new List<string>(128);
            string[] lines = csvContent.Replace("\r", string.Empty).Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                ReadOnlySpan<char> line = lines[i].AsSpan().Trim();
                if (line.Length == 0)
                    continue;

                int commaIndex = line.IndexOf(',');
                if (commaIndex <= 0 || commaIndex >= line.Length - 1)
                    continue;

                string key = line.Slice(commaIndex + 1).Trim().ToString();
                if (string.IsNullOrWhiteSpace(key))
                    continue;

                keys.Add(key);
            }

            keys.Sort(StringComparer.Ordinal);
            WriteGeneratedFile(keys, outputPath);
        }
    }
}
