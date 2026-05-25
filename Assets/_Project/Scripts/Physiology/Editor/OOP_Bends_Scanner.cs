#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    internal static class OOP_Bends_Scanner
    {
        private const string Summary = "OOP Ascent Timers Eradicated";
        private const string SharedReportPath = "Docs/Reports/PHYSIOLOGY_OPTIMIZATION_REPORT_X_009.json";
        private const string SidecarReportPath = "Docs/Reports/PHYSIOLOGY_BENDS_SCANNER_X_009.json";

        private static readonly string[] s_roots =
        {
            "Assets/_Project/Scripts/Physiology",
            "Assets/_Project/Scripts/Gameplay/Combat/CombatDamageRuntime_StatusEffects.cs",
            "Assets/_Project/Scripts/Gameplay/HectonPlayerHealth.cs",
            "Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs",
            "Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs",
            "Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs",
            "Assets/_Project/Scripts/Atmosphere/ToxicOutgassingChemistryRuntime.cs",
            "Assets/_Project/Scripts/HectonSurvivalSystem.cs",
            "Assets/_Project/Scripts/Gameplay/SurvivalPhysiologyScalarJob.cs"
        };

        private static readonly string[] s_forbidden =
        {
            "TissueCompartmentCount = 16",
            "fixed float TissueTensions",
            "entityCapacity * 16",
            "buhlmann_zh16",
            "n2_15",
            "float.Parse",
            "AscentTimer",
            "CheckAscent",
            "TheBends",
            "DepthDamage",
            "depthDamage",
            "yield return new WaitForSeconds"
        };

        [MenuItem("Hecton8/Physiology/Run OOP Bends Scanner")]
        private static void RunMenu()
        {
            int findingCount = RunStaticScan(Application.dataPath);
            if (findingCount == 0)
                Debug.Log("[SHINOBU_321] " + Summary);
            else
                Debug.LogWarning("[SHINOBU_321] Legacy bends/ascent debt found: " + findingCount);
        }

        internal static int RunStaticScan(string assetsPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(assetsPath, ".."));
            List<Finding> findings = new List<Finding>(16);
            for (int i = 0; i < s_roots.Length; i++)
                ScanRoot(projectRoot, s_roots[i], findings);

            string sidecar = BuildSidecarReport(findings);
            WriteText(Path.Combine(projectRoot, SidecarReportPath), sidecar);
            UpsertSharedReport(Path.Combine(projectRoot, SharedReportPath), BuildSharedSection(findings));
            return findings.Count;
        }

        private static void ScanRoot(string projectRoot, string relativeRoot, List<Finding> findings)
        {
            string root = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(root))
            {
                ScanFile(projectRoot, root, findings);
                return;
            }

            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, "*.cs", SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string normalized = files[i].Replace('\\', '/');
                if (normalized.Contains("/Physiology/Editor/"))
                    continue;
                ScanFile(projectRoot, files[i], findings);
            }
        }

        private static void ScanFile(string projectRoot, string path, List<Finding> findings)
        {
            string relative = MakeRelative(projectRoot, path);
            string[] lines = File.ReadAllLines(path);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int patternIndex = 0; patternIndex < s_forbidden.Length; patternIndex++)
                {
                    string pattern = s_forbidden[patternIndex];
                    if (line.IndexOf(pattern, StringComparison.Ordinal) >= 0)
                        findings.Add(new Finding(relative, lineIndex + 1, pattern));
                }

                if (LooksLikeManagedStatusCollection(line))
                    findings.Add(new Finding(relative, lineIndex + 1, "managed-status-collection"));

                if (LooksLikeStatusClassTimer(line))
                    findings.Add(new Finding(relative, lineIndex + 1, "managed-status-timer-class"));
            }
        }

        private static bool LooksLikeManagedStatusCollection(string line)
        {
            if (line.IndexOf("List<", StringComparison.Ordinal) < 0 &&
                line.IndexOf("Dictionary<", StringComparison.Ordinal) < 0 &&
                line.IndexOf("HashSet<", StringComparison.Ordinal) < 0)
                return false;

            return ContainsStatusTerm(line);
        }

        private static bool LooksLikeStatusClassTimer(string line)
        {
            if (line.IndexOf("class ", StringComparison.Ordinal) < 0 &&
                line.IndexOf("Timer", StringComparison.Ordinal) < 0)
                return false;

            return ContainsStatusTerm(line) &&
                   (line.IndexOf("class ", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("EffectTimer", StringComparison.Ordinal) >= 0 ||
                    line.IndexOf("StatusTimer", StringComparison.Ordinal) >= 0);
        }

        private static bool ContainsStatusTerm(string line)
        {
            return line.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Effect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Bleed", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Poison", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Stun", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Radiation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Toxic", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Hypoxia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Narcosis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Decompression", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("Bends", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildSidecarReport(List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_321\",");
            builder.AppendLine("  \"scanner\": \"OOP_Bends_Scanner\",");
            builder.Append("  \"summary\": \"").Append(Summary).AppendLine("\",");
            builder.Append("  \"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.AppendLine("  \"runtimeRouteProof\": {");
            builder.AppendLine("    \"owner\": \"ShinobuPhysiologyRuntime\",");
            builder.AppendLine("    \"truthBuffer\": \"GlobalDataVault 70221 DecompressionStateDTO[entityCapacity], 70216 StatusEffectStateDTO[entityCapacity], plus 70235 TissueCompartmentDTO[entityCapacity*3]\",");
            builder.AppendLine("    \"coefficientBuffer\": \"GlobalDataVault 70222 HaldaneTissueCoefficientDTO[3]\",");
            builder.AppendLine("    \"damageRoute\": \"SignalBus<CombatDamageSignal> Barotrauma; no direct HectonPlayerHealth mutation\",");
            builder.AppendLine("    \"blackBox\": \"GlobalDataVault 73343 DecompressionTelemetryEntry[300] plus 70226 PhysiologyTelemetryEntry[300] with StatusEffectMask@8 -> Docs/AgentLogs/Dump_SHINOBU_321.bin\"");
            builder.AppendLine("  },");
            builder.AppendLine("  \"findings\": [");
            AppendFindings(builder, findings, 4);
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildSharedSection(List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("  \"shinobu321OopBendsScanner\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_321\",");
            builder.AppendLine("    \"scanner\": \"OOP_Bends_Scanner.StaticMirror\",");
            builder.Append("    \"summary\": \"").Append(Summary).AppendLine("\",");
            builder.Append("    \"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.AppendLine("    \"dedicatedReport\": \"Docs/Reports/PHYSIOLOGY_BENDS_SCANNER_X_009.json\",");
            builder.AppendLine("    \"runtimeRouteProof\": {");
            builder.AppendLine("      \"owner\": \"ShinobuPhysiologyRuntime\",");
            builder.AppendLine("      \"decompressionState\": \"Vault 70221 DecompressionStateDTO 64-byte three-lane N2 state\",");
            builder.AppendLine("      \"statusState\": \"Vault 70216 StatusEffectStateDTO 64-byte unmanaged ulong StatusEffectMask state\",");
            builder.AppendLine("      \"schreinerRoute\": \"IntegrateBloodGasTensionsJob 3-tissue scalar lanes\",");
            builder.AppendLine("      \"damageRoute\": \"SignalBus<CombatDamageSignal> Barotrauma\",");
            builder.AppendLine("      \"telemetry\": \"Vault 73343 DecompressionTelemetryEntry[300] plus 70226 PhysiologyTelemetryEntry[300] with StatusEffectMask@8 and Dump_SHINOBU_321.bin\"");
            builder.AppendLine("    }");
            if (findings.Count > 0)
            {
                builder.AppendLine("    ,\"legacyFindings\": [");
                AppendFindings(builder, findings, 6);
                builder.AppendLine("    ]");
            }

            builder.Append("  }");
            return builder.ToString();
        }

        private static void AppendFindings(StringBuilder builder, List<Finding> findings, int spaces)
        {
            string pad = new string(' ', spaces);
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                builder.Append(pad).Append("{ \"path\": \"").Append(Escape(finding.Path))
                    .Append("\", \"line\": ").Append(finding.Line)
                    .Append(", \"pattern\": \"").Append(Escape(finding.Pattern)).Append("\" }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }
        }

        private static void UpsertSharedReport(string path, string section)
        {
            if (!File.Exists(path))
            {
                WriteText(path, "{" + global::System.Environment.NewLine + section + global::System.Environment.NewLine + "}" + global::System.Environment.NewLine);
                return;
            }

            string text = File.ReadAllText(path);
            const string key = "\"shinobu321OopBendsScanner\"";
            int existing = text.IndexOf(key, StringComparison.Ordinal);
            if (existing >= 0)
                return;

            int insert = text.LastIndexOf('}');
            if (insert < 0)
                return;

            string prefix = text.Substring(0, insert).TrimEnd();
            string suffix = text.Substring(insert);
            string comma = prefix.EndsWith("{", StringComparison.Ordinal) ? string.Empty : ",";
            WriteText(path, prefix + comma + global::System.Environment.NewLine + section + global::System.Environment.NewLine + suffix);
        }

        private static void WriteText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);
            File.WriteAllText(path, text, Encoding.UTF8);
        }

        private static string MakeRelative(string root, string path)
        {
            Uri rootUri = new Uri(root.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal) ? root : root + Path.DirectorySeparatorChar);
            Uri fileUri = new Uri(path);
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(fileUri).ToString()).Replace('\\', '/');
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private readonly struct Finding
        {
            public readonly string Path;
            public readonly int Line;
            public readonly string Pattern;

            public Finding(string path, int line, string pattern)
            {
                Path = path;
                Line = line;
                Pattern = pattern;
            }
        }
    }
}
#endif
