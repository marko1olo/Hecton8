#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Thermodynamics
{
    public static class OOP_Thermal_Scanner
    {
        [MenuItem("Hecton8/Thermodynamics/Run OOP Thermal Scanner")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string scripts = Path.Combine(root, "Assets", "_Project", "Scripts");
            string vehicles = Path.Combine(scripts, "Vehicles");
            string environment = Path.Combine(scripts, "Environment");
            string power = Path.Combine(scripts, "Power");
            string habitat = Path.Combine(scripts, "Habitat");
            string thermodynamics = Path.Combine(scripts, "Thermodynamics");
            int triggerHits = 0;
            int managedHeatListHits = 0;
            int managedHeatClassHits = 0;
            int legacyGeneratorHits = 0;
            StringBuilder findings = new StringBuilder(2048);
            ScanDirectory(vehicles, ref triggerHits, ref managedHeatListHits, ref managedHeatClassHits, findings);
            ScanDirectory(environment, ref triggerHits, ref managedHeatListHits, ref managedHeatClassHits, findings);
            ScanDirectory(power, ref triggerHits, ref managedHeatListHits, ref managedHeatClassHits, findings);
            ScanDirectory(habitat, ref triggerHits, ref managedHeatListHits, ref managedHeatClassHits, findings);
            ScanDirectory(thermodynamics, ref triggerHits, ref managedHeatListHits, ref managedHeatClassHits, findings);
            legacyGeneratorHits = CountLegacyGenerators(power) + CountLegacyGenerators(habitat) + CountLegacyGenerators(thermodynamics);

            StringBuilder json = new StringBuilder(4096);
            json.AppendLine("{");
            json.AppendLine("  \"agent\": \"SHINOBU_342\",");
            json.AppendLine("  \"scanner\": \"OOP_Thermal_Scanner.StaticMirror.Lexical\",");
            json.AppendLine("  \"analysisMode\": \"lexical token scanner; not a Roslyn AST parser; scans Power/Habitat/Thermodynamics legacy generator patterns\",");
            json.AppendLine("  \"sharedReportKey\": \"shinobu342NuclearThermalScanner\",");
            json.AppendLine("  \"dedicatedReport\": \"Docs/Reports/PHYSICS_OPTIMIZATION_REPORT_SHINOBU_342.json\",");
            json.AppendLine("  \"summary\": \"Nuclear reactor thermodynamics route scanned\",");
            json.AppendLine("  \"scannedRoots\": [\"Assets/_Project/Scripts/Vehicles\", \"Assets/_Project/Scripts/Environment\", \"Assets/_Project/Scripts/Power\", \"Assets/_Project/Scripts/Habitat\", \"Assets/_Project/Scripts/Thermodynamics\"],");
            json.AppendLine("  \"triggerThermalHits\": " + triggerHits + ",");
            json.AppendLine("  \"managedHeatListHits\": " + managedHeatListHits + ",");
            json.AppendLine("  \"managedHeatClassHits\": " + managedHeatClassHits + ",");
            json.AppendLine("  \"legacyGeneratorHits\": " + legacyGeneratorHits + ",");
            json.AppendLine("  \"route\": \"BaseReactorStateDTO -> EvaluateFissionReactionJob -> CalculateThermoelectricPowerJob -> PowerNodeDTO/airlock/fluid atomics -> SignalBus meltdown lanes\",");
            json.AppendLine("  \"forbiddenRuntimeRoute\": \"OnTriggerStay/SphereCollider/List<HeatSource>/Update timer generator\",");
            json.AppendLine("  \"findings\": [");
            json.Append(findings);
            json.AppendLine();
            json.AppendLine("  ]");
            json.AppendLine("}");
            WriteReports(root, json.ToString());
            AssetDatabase.Refresh();
        }

        private static void ScanDirectory(string directory, ref int triggerHits, ref int managedHeatListHits, ref int managedHeatClassHits, StringBuilder findings)
        {
            if (!Directory.Exists(directory))
                return;

            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i];
                string text = File.ReadAllText(path);
                bool thermal = text.IndexOf("Heat", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               text.IndexOf("Thermal", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                               text.IndexOf("Temperature", System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (thermal && (text.Contains("OnTriggerStay") || text.Contains("SphereCollider") || text.Contains("isTrigger")))
                {
                    triggerHits++;
                    AppendFinding(findings, path, "thermal trigger/collider route");
                }

                if (text.Contains("List<HeatSource>") || text.Contains("List <HeatSource>"))
                {
                    managedHeatListHits++;
                    AppendFinding(findings, path, "managed List<HeatSource>");
                }

                if (text.Contains("class HeatSource") || text.Contains("struct HeatSource"))
                {
                    managedHeatClassHits++;
                    AppendFinding(findings, path, "managed HeatSource type");
                }
            }
        }

        private static int CountLegacyGenerators(string directory)
        {
            if (!Directory.Exists(directory))
                return 0;

            int hits = 0;
            string[] files = Directory.GetFiles(directory, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string text = File.ReadAllText(files[i]);
                if ((text.Contains("Update()") || text.Contains("InvokeRepeating") || text.Contains("Coroutine")) &&
                    (text.Contains("Generator") || text.Contains("PowerNode") || text.Contains("reactor")))
                {
                    hits++;
                }
            }

            return hits;
        }

        private static void AppendFinding(StringBuilder findings, string path, string pattern)
        {
            if (findings.Length > 0)
                findings.AppendLine(",");
            string relative = path.Replace(Path.GetFullPath(Path.Combine(Application.dataPath, "..")) + Path.DirectorySeparatorChar, string.Empty).Replace('\\', '/');
            findings.Append("    { \"path\": \"").Append(relative).Append("\", \"pattern\": \"").Append(pattern).Append("\" }");
        }

        private static void WriteReports(string root, string reportJson)
        {
            string reportDirectory = Path.Combine(root, "Docs", "Reports");
            Directory.CreateDirectory(reportDirectory);

            string dedicatedPath = Path.Combine(reportDirectory, "PHYSICS_OPTIMIZATION_REPORT_SHINOBU_342.json");
            File.WriteAllText(dedicatedPath, reportJson);

            string sharedPath = Path.Combine(reportDirectory, "PHYSICS_OPTIMIZATION_REPORT.json");
            AppendSharedReportSection(sharedPath, "shinobu342NuclearThermalScanner", reportJson);
        }

        private static void AppendSharedReportSection(string sharedPath, string key, string reportJson)
        {
            string entry = "  \"" + key + "\": " + IndentNestedJson(reportJson.Trim(), 2);
            if (!File.Exists(sharedPath))
            {
                File.WriteAllText(sharedPath, "{\n" + entry + "\n}\n");
                return;
            }

            string existing = File.ReadAllText(sharedPath).Trim();
            if (existing.Length < 2 || existing[0] != '{' || existing[existing.Length - 1] != '}')
            {
                File.WriteAllText(sharedPath, "{\n" + entry + "\n}\n");
                return;
            }

            if (existing.IndexOf("\"" + key + "\"", System.StringComparison.Ordinal) >= 0)
                return;

            string body = existing.Substring(1, existing.Length - 2).TrimEnd();
            string comma = body.Length > 0 ? ",\n" : string.Empty;
            File.WriteAllText(sharedPath, "{\n" + body + comma + entry + "\n}\n");
        }

        private static string IndentNestedJson(string json, int spaces)
        {
            string indent = new string(' ', spaces);
            string[] lines = json.Split('\n');
            StringBuilder builder = new StringBuilder(json.Length + lines.Length * spaces);
            for (int i = 0; i < lines.Length; i++)
            {
                if (i > 0)
                    builder.Append(indent);
                builder.Append(lines[i].TrimEnd('\r'));
                if (i + 1 < lines.Length)
                    builder.AppendLine();
            }

            return builder.ToString();
        }
    }
}
#endif
