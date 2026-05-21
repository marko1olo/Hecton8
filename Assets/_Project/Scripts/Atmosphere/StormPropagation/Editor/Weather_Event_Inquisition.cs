#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Atmosphere.Editor
{
    public static class Weather_Event_Inquisition
    {
        private static readonly string[] SearchRoots =
        {
            "Assets/_Project/Scripts/Environment",
            "Assets/_Project/Scripts/AI"
        };

        private static readonly string[] KnownExternalBridgeFiles =
        {
            "Assets/_Project/Scripts/HectonCelestialEngine.cs",
            "Assets/_Project/Scripts/Lighting/HectonGIRelaySystem.cs",
            "Assets/_Project/Scripts/Atmosphere/HectonSurfaceWeatherDirector.cs"
        };

        private static readonly string[] WeatherListenerPatterns =
        {
            "IWeatherEventListener",
            "IWeatherListener",
            "OnWeatherChanged",
            "OnWeatherStateChanged",
            "WeatherEvents.Register",
            "WeatherEvents.RaiseSnapshotUpdated",
            "WeatherEvents.RaiseLightning"
        };

        private static readonly string[] ForceApplicationPatterns =
        {
            ".AddForce(",
            ".AddForceAtPosition("
        };

        private static readonly string[] PhysicsReferencePatterns =
        {
            "Rigidbody",
            "ForceMode"
        };

        [MenuItem("Hecton8/Reports/Weather Event Inquisition")]
        public static void Run()
        {
            string root = BuildProjectRootPathCold();
            if (string.IsNullOrEmpty(root))
                return;

            int weatherListenerHits = 0;
            int weatherBridgeHits = 0;
            int knownExternalBridgeHits = 0;
            int forceApplicationHits = 0;
            int physicsReferenceHits = 0;
            StringBuilder findings = new StringBuilder(2048);
            StringBuilder knownExternalFindings = new StringBuilder(1024);
            for (int r = 0; r < SearchRoots.Length; r++)
            {
                string absoluteRoot = Path.Combine(root, SearchRoots[r]);
                if (!Directory.Exists(absoluteRoot))
                    continue;

                string[] files = Directory.GetFiles(absoluteRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string normalized = files[i].Replace('\\', '/');
                    if (normalized.IndexOf("/Editor/", StringComparison.Ordinal) >= 0 ||
                        normalized.EndsWith("/Environment/WeatherEvents.cs", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    ScanFile(root, files[i], findings, ref weatherListenerHits, ref weatherBridgeHits, ref forceApplicationHits, ref physicsReferenceHits);
                }
            }

            for (int i = 0; i < KnownExternalBridgeFiles.Length; i++)
                ScanKnownExternalBridgeFile(root, KnownExternalBridgeFiles[i], knownExternalFindings, ref knownExternalBridgeHits);

            string reportPath = Path.Combine(root, "Docs/Reports/ENVIRONMENT_OPTIMIZATION_REPORT.json");
            string reportDirectory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(reportDirectory) && !Directory.Exists(reportDirectory))
                Directory.CreateDirectory(reportDirectory);

            StringBuilder json = new StringBuilder(4096);
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_234\",\n");
            json.Append("  \"summary\": \"SHINOBU Storm Propagation Isolated; Legacy WeatherEvents Bridge Active\",\n");
            json.Append("  \"status\": \"STATIC_SOURCE_ONLY_TASK01_BLOCKED_LEGACY_BRIDGE\",\n");
            json.Append("  \"scanRoots\": [\n");
            for (int i = 0; i < SearchRoots.Length; i++)
            {
                if (i > 0)
                    json.Append(",\n");
                json.Append("    \"").Append(Escape(SearchRoots[i])).Append("\"");
            }
            json.Append("\n  ],\n");
            json.Append("  \"excludedColdBridges\": [\n");
            json.Append("    \"Assets/_Project/Scripts/Environment/WeatherEvents.cs\",\n");
            json.Append("    \"*/Editor/*\"\n");
            json.Append("  ],\n");
            json.Append("  \"weatherListenerHits\": ").Append(weatherListenerHits).Append(",\n");
            json.Append("  \"weatherBridgeHits\": ").Append(weatherBridgeHits).Append(",\n");
            json.Append("  \"knownExternalBridgeHits\": ").Append(knownExternalBridgeHits).Append(",\n");
            json.Append("  \"deepWaterForceHits\": ").Append(forceApplicationHits).Append(",\n");
            json.Append("  \"physicsReferenceHits\": ").Append(physicsReferenceHits).Append(",\n");
            json.Append("  \"policy\": \"SHINOBU_234 storm propagation consumes an optional DataVault weather row or its SHINOBU-owned emergency mock hurricane row, then publishes SHINOBU-owned storm scalar buffers. Legacy WeatherEvents fan-out remains active for Celestial/GI until those consumers migrate.\",\n");
            json.Append("  \"task01\": \"BLOCKED_LEGACY_BRIDGE_RESTORED_FOR_ACTIVE_CELESTIAL_GI_CONSUMERS\",\n");
            json.Append("  \"replacementRoute\": \"Optional ShinobuOceanWeatherState or MockHurricaneStateDTO -> CalculateStormAttenuationJob -> StormPropagationDTO -> SHINOBU-owned fog/flow/biolum/audio scalar buffers\",\n");
            json.Append("  \"knownExternalBridgeFindings\": [\n");
            json.Append(knownExternalFindings);
            json.Append("\n  ],\n");
            json.Append("  \"findings\": [\n");
            json.Append(findings);
            json.Append("\n  ]\n");
            json.Append("}\n");
            WriteReportAtomic(reportPath, json);
            AssetDatabase.Refresh();
            Debug.Log("SHINOBU_234 Weather Event Inquisition wrote Docs/Reports/ENVIRONMENT_OPTIMIZATION_REPORT.json");
        }

        private static void ScanFile(
            string root,
            string file,
            StringBuilder findings,
            ref int weatherListenerHits,
            ref int weatherBridgeHits,
            ref int forceApplicationHits,
            ref int physicsReferenceHits)
        {
            string[] lines = File.ReadAllLines(file);
            string relative = file.Substring(root.Length + 1).Replace('\\', '/');
            for (int line = 0; line < lines.Length; line++)
            {
                string text = lines[line];
                ScanPatterns(relative, line, text, WeatherListenerPatterns, "weather_listener_or_bridge", findings, ref weatherListenerHits, ref weatherBridgeHits);
                ScanPatterns(relative, line, text, ForceApplicationPatterns, "force_application", findings, ref forceApplicationHits);
                ScanPatterns(relative, line, text, PhysicsReferencePatterns, "physics_reference", findings, ref physicsReferenceHits);
            }
        }

        private static void ScanKnownExternalBridgeFile(
            string root,
            string relativePath,
            StringBuilder findings,
            ref int bridgeHits)
        {
            string file = Path.Combine(root, relativePath);
            if (!File.Exists(file))
                return;

            string[] lines = File.ReadAllLines(file);
            for (int line = 0; line < lines.Length; line++)
            {
                string text = lines[line];
                for (int p = 0; p < WeatherListenerPatterns.Length; p++)
                {
                    string pattern = WeatherListenerPatterns[p];
                    if (!pattern.StartsWith("WeatherEvents.", StringComparison.Ordinal) ||
                        text.IndexOf(pattern, StringComparison.Ordinal) < 0)
                    {
                        continue;
                    }

                    bridgeHits++;
                    AppendFinding(relativePath, line, "known_external_legacy_bridge", pattern, findings);
                }
            }
        }

        private static void ScanPatterns(
            string relative,
            int line,
            string text,
            string[] patterns,
            string category,
            StringBuilder findings,
            ref int hitCount)
        {
            int ignoredBridgeHits = 0;
            ScanPatterns(relative, line, text, patterns, category, findings, ref hitCount, ref ignoredBridgeHits);
        }

        private static void ScanPatterns(
            string relative,
            int line,
            string text,
            string[] patterns,
            string category,
            StringBuilder findings,
            ref int primaryHits,
            ref int bridgeHits)
        {
            for (int p = 0; p < patterns.Length; p++)
            {
                string pattern = patterns[p];
                if (text.IndexOf(pattern, StringComparison.Ordinal) < 0)
                    continue;

                bool bridge = pattern.StartsWith("WeatherEvents.", StringComparison.Ordinal);
                if (bridge)
                    bridgeHits++;
                else
                    primaryHits++;

                AppendFinding(relative, line, category, pattern, findings);
            }
        }

        private static void AppendFinding(
            string relative,
            int line,
            string category,
            string pattern,
            StringBuilder findings)
        {
            if (findings.Length > 0)
                findings.Append(",\n");

            findings.Append("    { \"path\": \"")
                .Append(Escape(relative))
                .Append("\", \"line\": ")
                .Append(line + 1)
                .Append(", \"category\": \"")
                .Append(Escape(category))
                .Append("\", \"pattern\": \"")
                .Append(Escape(pattern))
                .Append("\" }");
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void WriteReportAtomic(string reportPath, StringBuilder json)
        {
            string tempPath = reportPath + ".tmp";
            string backupPath = reportPath + ".bak";
            File.WriteAllText(tempPath, json.ToString());
            if (File.Exists(reportPath))
                File.Replace(tempPath, reportPath, backupPath, true);
            else
                File.Move(tempPath, reportPath);
        }

        private static string BuildProjectRootPathCold()
        {
            string dataPath = Application.dataPath;
            if (string.IsNullOrEmpty(dataPath))
                return null;

            DirectoryInfo parent = Directory.GetParent(dataPath);
            return parent != null ? parent.FullName : null;
        }
    }
}
#endif
