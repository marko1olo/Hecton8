#if UNITY_EDITOR
namespace Hecton8.Editor
{
    using System;
    using System.IO;
    using System.Text;
    using UnityEditor;
    using UnityEngine;

    public static class OOP_Battery_Scanner
    {
        private const string ScriptsRoot = "Assets/_Project/Scripts";
        private const string ReportPath = "Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json";

        [MenuItem("Hecton8/Tools/OOP Battery Scanner")]
        public static void RunScannerFromMenu()
        {
            int findings = RunScan();
            Debug.Log("[OOP_Battery_Scanner] Findings: " + findings);
        }

        public static int RunScan()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(projectRoot, ScriptsRoot);
            string reportPath = Path.Combine(projectRoot, ReportPath);
            int findingCount = 0;
            StringBuilder builder = new StringBuilder(8192);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_327\",");
            builder.AppendLine("  \"scanner\": \"OOP_Battery_Scanner\",");
            builder.AppendLine("  \"findings\": [");

            if (Directory.Exists(scriptsRoot))
            {
                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    ScanFile(projectRoot, files[i], builder, ref findingCount);
                }
            }

            builder.AppendLine();
            builder.AppendLine("  ],");
            builder.Append("  \"findingCount\": ").Append(findingCount).AppendLine(",");
            builder.Append("  \"summary\": \"")
                .Append(findingCount == 0 ? "OOP Equipment Timers Eradicated" : "OOP Equipment Timer Relapse Detected")
                .AppendLine("\"");
            builder.AppendLine("}");

            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(reportPath, builder.ToString());
            AssetDatabase.Refresh();
            return findingCount;
        }

        private static void ScanFile(string projectRoot, string path, StringBuilder builder, ref int findingCount)
        {
            string relativePath = NormalizePath(Path.GetRelativePath(projectRoot, path));
            if (relativePath.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                return;

            string text = File.ReadAllText(path);
            bool equipmentContext =
                relativePath.IndexOf("/Equipment/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                relativePath.IndexOf("/Tools/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                relativePath.IndexOf("Flashlight", StringComparison.OrdinalIgnoreCase) >= 0 ||
                text.IndexOf("IBatteryTool", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("ActiveEquipment", StringComparison.Ordinal) >= 0 ||
                text.IndexOf("BatteryDrain", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!equipmentContext)
                return;

            AddIfFound(builder, ref findingCount, relativePath, text, "Mathf.PerlinNoise", "CPU flicker noise in equipment context");
            AddIfFound(builder, ref findingCount, relativePath, text, "yield return new WaitForSeconds", "Coroutine cadence in equipment context");
            AddIfFound(builder, ref findingCount, relativePath, text, "StartCoroutine", "Coroutine ownership in equipment context");
            AddIfFound(builder, ref findingCount, relativePath, text, "void Update(", "Unity Update loop candidate in equipment context");
            AddIfFound(builder, ref findingCount, relativePath, text, "FlashlightController", "Legacy object controller in equipment context");
            AddIfFound(builder, ref findingCount, relativePath, text, ".intensity =", "Managed Light intensity writer in handheld-lighting context");
            AddIfFound(builder, ref findingCount, relativePath, text, "Time.deltaTime", "Frame delta in battery or light context");
        }

        private static void AddIfFound(StringBuilder builder, ref int findingCount, string path, string text, string pattern, string reason)
        {
            int index = text.IndexOf(pattern, StringComparison.Ordinal);
            if (index < 0)
                return;

            if (findingCount > 0)
                builder.AppendLine(",");

            int line = ResolveLine(text, index);
            builder.Append("    { \"path\": \"").Append(EscapeJson(path)).Append("\", \"line\": ")
                .Append(line)
                .Append(", \"pattern\": \"").Append(EscapeJson(pattern))
                .Append("\", \"reason\": \"").Append(EscapeJson(reason)).Append("\" }");
            findingCount++;
        }

        private static int ResolveLine(string text, int index)
        {
            int line = 1;
            int limit = Mathf.Clamp(index, 0, text.Length);
            for (int i = 0; i < limit; i++)
            {
                if (text[i] == '\n')
                    line++;
            }

            return line;
        }

        private static string NormalizePath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string EscapeJson(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
