#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Power.Editor
{
    public static class Charger_OOP_Scanner
    {
        private const string ReportRelativePath = "Docs/Reports/EQUIPMENT_OPTIMIZATION_REPORT.json";

        [MenuItem("Hecton/Power/Run Charger OOP Scanner")]
        public static void RunMenu()
        {
            string reportPath = RunScan();
            Debug.Log("Charger OOP scanner wrote " + reportPath);
        }

        public static string RunScan()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project/Scripts");
            string reportPath = Path.GetFullPath(Path.Combine(projectRoot, ReportRelativePath));
            Directory.CreateDirectory(Path.GetDirectoryName(reportPath));

            int chargerFiles = 0;
            int updateLoops = 0;
            int coroutineLoops = 0;
            int managedBatteryLists = 0;
            int managedBatteryArrays = 0;
            int slowTickRegistrations = 0;
            int legacySlotFacades = 0;

            string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
            StringBuilder findings = new StringBuilder(1024);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string fileName = Path.GetFileName(path);
                string text = File.ReadAllText(path);
                bool charger = fileName.IndexOf("BatteryCharger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               fileName.IndexOf("PowerCellCharger", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               text.IndexOf("class BatteryCharger", StringComparison.Ordinal) >= 0 ||
                               text.IndexOf("class PowerCellCharger", StringComparison.Ordinal) >= 0;
                if (!charger)
                    continue;

                chargerFiles++;
                int fileUpdate = CountToken(text, "void Update(");
                int fileCoroutine = CountToken(text, "StartCoroutine(") + CountToken(text, "IEnumerator ");
                int fileLists = CountToken(text, "List<Battery") + CountToken(text, "List<PowerCell");
                int fileArrays = CountToken(text, "Battery[]") + CountToken(text, "PowerCell[]");
                int fileSlow = CountToken(text, "RegisterSlowTickable(") + CountToken(text, "ISlowTickable");
                int fileFacade = CountToken(text, "BatterySlot[]") + CountToken(text, "class BatterySlot");

                updateLoops += fileUpdate;
                coroutineLoops += fileCoroutine;
                managedBatteryLists += fileLists;
                managedBatteryArrays += fileArrays;
                slowTickRegistrations += fileSlow;
                legacySlotFacades += fileFacade;

                if (fileUpdate + fileCoroutine + fileLists + fileArrays + fileSlow > 0)
                {
                    findings.Append("    { \"path\": \"");
                    findings.Append(Escape(Path.GetRelativePath(projectRoot, path)));
                    findings.Append("\", \"updateLoops\": ");
                    findings.Append(fileUpdate);
                    findings.Append(", \"coroutines\": ");
                    findings.Append(fileCoroutine);
                    findings.Append(", \"managedBatteryLists\": ");
                    findings.Append(fileLists);
                    findings.Append(", \"managedBatteryArrays\": ");
                    findings.Append(fileArrays);
                    findings.Append(", \"slowTickRegistrations\": ");
                    findings.Append(fileSlow);
                    findings.Append(" },\n");
                }
            }

            bool eradicated = updateLoops == 0 &&
                              coroutineLoops == 0 &&
                              managedBatteryLists == 0 &&
                              managedBatteryArrays == 0 &&
                              slowTickRegistrations == 0;

            if (findings.Length >= 2)
                findings.Length -= 2;

            StringBuilder json = new StringBuilder(2048);
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_230\",\n");
            json.Append("  \"summary\": \"");
            json.Append(eradicated ? "Managed Charging Scripts Eradicated" : "Managed Charging Scripts Still Present");
            json.Append("\",\n");
            json.Append("  \"chargerFilesScanned\": ");
            json.Append(chargerFiles);
            json.Append(",\n");
            json.Append("  \"forbiddenPatterns\": {\n");
            json.Append("    \"updateLoops\": ");
            json.Append(updateLoops);
            json.Append(",\n");
            json.Append("    \"coroutineLoops\": ");
            json.Append(coroutineLoops);
            json.Append(",\n");
            json.Append("    \"managedBatteryLists\": ");
            json.Append(managedBatteryLists);
            json.Append(",\n");
            json.Append("    \"managedBatteryArrays\": ");
            json.Append(managedBatteryArrays);
            json.Append(",\n");
            json.Append("    \"slowTickRegistrations\": ");
            json.Append(slowTickRegistrations);
            json.Append("\n  },\n");
            json.Append("  \"legacyFacadePatterns\": {\n");
            json.Append("    \"batterySlotFacadeTokens\": ");
            json.Append(legacySlotFacades);
            json.Append("\n  },\n");
            json.Append("  \"findings\": [\n");
            json.Append(findings);
            json.Append("\n  ]\n");
            json.Append("}\n");
            WriteSharedReport(reportPath, json.ToString());
            return reportPath;
        }

        private static void WriteSharedReport(string reportPath, string entryJson)
        {
            string entry = entryJson.Trim();
            if (!File.Exists(reportPath))
            {
                File.WriteAllText(reportPath, "{\n  \"reports\": [\n" + Indent(entry, 4) + "\n  ]\n}\n", Encoding.UTF8);
                return;
            }

            string existing = File.ReadAllText(reportPath).Trim();
            int reportsKey = existing.IndexOf("\"reports\"", StringComparison.Ordinal);
            int arrayEnd = reportsKey >= 0 ? existing.LastIndexOf(']') : -1;
            if (reportsKey >= 0 && arrayEnd >= 0)
            {
                string head = existing.Substring(0, arrayEnd).TrimEnd();
                bool emptyArray = head.EndsWith("[", StringComparison.Ordinal);
                string separator = emptyArray ? "\n" : ",\n";
                string merged = head + separator + Indent(entry, 4) + "\n  ]\n}\n";
                File.WriteAllText(reportPath, merged, Encoding.UTF8);
                return;
            }

            string wrapped = "{\n  \"reports\": [\n" +
                             Indent(existing, 4) +
                             ",\n" +
                             Indent(entry, 4) +
                             "\n  ]\n}\n";
            File.WriteAllText(reportPath, wrapped, Encoding.UTF8);
        }

        private static string Indent(string value, int spaces)
        {
            string prefix = new string(' ', spaces);
            return prefix + value.Replace("\r\n", "\n").Replace("\n", "\n" + prefix);
        }

        private static int CountToken(string text, string token)
        {
            int count = 0;
            int index = 0;
            while (index < text.Length)
            {
                int found = text.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    break;
                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
#endif
