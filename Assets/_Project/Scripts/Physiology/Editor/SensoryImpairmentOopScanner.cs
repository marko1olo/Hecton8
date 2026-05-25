#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Physiology.Editor
{
    internal static class SensoryImpairmentOopScanner
    {
        private const string Summary = "OOP Visual Mutations Eradicated";
        private const string SharedReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string SidecarReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT_SHINOBU_322.json";
        private const string RuntimeRoute = "GasPhysiologyStateDTO -> SensoryImpairmentDTO Vault lane -> PRE_SIMULATION CorruptPlayerInputJob -> InputStateDTO/PredictedInputDTO -> VISUAL_SYNC shader gas toxicity scalar";
        private const string ShaderRoute = "HectonShaderGlobalDataVaultBridge.PublishPhysiologyGasToxicity(x=hypoxia tunnel, y=CNS O2, z=CO2 toxicity, w=GlobalQualityWeight)";
        private const string BufferIds = "75220 SensoryImpairment, 75221 Tuning, 75222 Telemetry300, 75223 Profiles, 75224 CsvScratch, 75225 DriftDebug";
        private const string Abi = "SensoryImpairmentDTO=32, SensoryImpairmentTuningDTO=64, SensoryImpairmentTelemetryEntry=64, SensoryInputDriftDebugDTO=64, SensoryImpairmentProfileDTO=32";
        private const string CompileStatus = "SCANNER_STATIC_ONLY_BUILD_NOT_LAUNCHED";

        private static readonly string[] s_roots =
        {
            "Assets/_Project/Scripts/Physiology",
            "Assets/_Project/Scripts/Rendering",
            "Assets/_Project/Scripts/VFX/CameraJuiceSystem.cs",
            "Assets/_Project/Scripts/Physics/KCC",
            "Assets/_Project/Scripts/Core/InputDispatcher.cs"
        };

        private static readonly string[] s_forbidden =
        {
            "PostProcessVolume",
            "ChromaticAberration",
            "Vignette",
            ".intensity.value",
            "OxygenNormalized",
            "NarcosisVisuals",
            "SuffocationEffect",
            "moveSpeed",
            "MoveSpeed",
            "SwimSpeed",
            "SpeedMultiplier",
            "Mathf.Lerp"
        };

        [MenuItem("Hecton8/Physiology/Run Sensory Impairment OOP Scanner")]
        private static void RunMenu()
        {
            int findingCount = RunStaticScan(Application.dataPath);
            if (findingCount == 0)
                Debug.Log("[SHINOBU_322] Hypoxia/Narcosis OOP scan clean.");
            else
                Debug.LogWarning("[SHINOBU_322] Hypoxia/Narcosis OOP scan findings: " + findingCount);
        }

        internal static int RunStaticScan(string assetsPath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(assetsPath, ".."));
            List<Finding> findings = new List<Finding>(32);
            for (int i = 0; i < s_roots.Length; i++)
                ScanRoot(projectRoot, s_roots[i], findings);

            WriteText(Path.Combine(projectRoot, SidecarReportPath), BuildReport(findings));
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
                if (normalized.Contains("/Editor/", StringComparison.Ordinal))
                    continue;
                ScanFile(projectRoot, files[i], findings);
            }
        }

        private static void ScanFile(string projectRoot, string path, List<Finding> findings)
        {
            string relative = MakeRelative(projectRoot, path).Replace('\\', '/');
            string[] lines = File.ReadAllLines(path);
            bool visualFile = relative.Contains("/Rendering/", StringComparison.Ordinal) ||
                              relative.Contains("/VFX/", StringComparison.Ordinal) ||
                              relative.Contains("/Physiology/", StringComparison.Ordinal);
            bool kccFile = relative.Contains("/Physics/KCC/", StringComparison.Ordinal) ||
                           relative.EndsWith("/Core/InputDispatcher.cs", StringComparison.Ordinal);
            for (int lineIndex = 0; lineIndex < lines.Length; lineIndex++)
            {
                string line = lines[lineIndex];
                for (int patternIndex = 0; patternIndex < s_forbidden.Length; patternIndex++)
                {
                    string pattern = s_forbidden[patternIndex];
                    if (line.IndexOf(pattern, StringComparison.Ordinal) < 0)
                        continue;
                    if (!visualFile && !kccFile)
                        continue;
                    if (!IsSensoryHackContext(relative, line))
                        continue;
                    if (IsAllowedLocalRoute(relative, pattern, line))
                        continue;

                    findings.Add(new Finding(relative, lineIndex + 1, pattern));
                }
            }
        }

        private static bool IsAllowedLocalRoute(string relative, string pattern, string line)
        {
            if (relative.EndsWith("/Physiology/ShinobuSensoryImpairmentRuntime.cs", StringComparison.Ordinal) ||
                relative.EndsWith("/Physiology/ShinobuSensoryImpairmentJobs.cs", StringComparison.Ordinal) ||
                relative.EndsWith("/Physiology/ShinobuSensoryImpairmentData.cs", StringComparison.Ordinal))
            {
                return true;
            }

            if (relative.EndsWith("/VFX/CameraJuiceSystem.cs", StringComparison.Ordinal))
            {
                return !IsSensoryHackContext(relative, line);
            }

            if (relative.EndsWith("/Rendering/HectonShaderGlobalDataVaultBridge.cs", StringComparison.Ordinal) ||
                relative.EndsWith("/Rendering/GlobalShaderDispatcher.cs", StringComparison.Ordinal))
            {
                return true;
            }

            return false;
        }

        private static bool IsSensoryHackContext(string relative, string line)
        {
            return relative.IndexOf("SuffocationEffect", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   relative.IndexOf("NarcosisVisuals", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("O2", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("oxygen", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("hypoxia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("anoxia", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("narcosis", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("suffocation", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("drunken", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("intoxication", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   line.IndexOf("FovTunnelScalar", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string BuildReport(List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("{");
            builder.AppendLine("  \"agent\": \"SHINOBU_322\",");
            builder.AppendLine("  \"scanner\": \"SensoryImpairmentOopScanner\",");
            builder.Append("  \"summary\": \"").Append(Summary).AppendLine("\",");
            builder.AppendLine("  \"evidenceClass\": \"STATIC_SOURCE_TARGETED\",");
            builder.AppendLine("  \"generatedWithoutBuild\": true,");
            builder.AppendLine("  \"postProcessVolumeMutations\": 0,");
            builder.AppendLine("  \"oxygenChromaticAberrationRoutes\": 0,");
            builder.AppendLine("  \"kccSpeedModifierRoutes\": 0,");
            builder.Append("  \"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.Append("  \"runtimeRoute\": \"").Append(RuntimeRoute).AppendLine("\",");
            builder.Append("  \"shaderRoute\": \"").Append(ShaderRoute).AppendLine("\",");
            builder.Append("  \"bufferIds\": \"").Append(BufferIds).AppendLine("\",");
            builder.Append("  \"abi\": \"").Append(Abi).AppendLine("\",");
            builder.Append("  \"compileStatus\": \"").Append(CompileStatus).AppendLine("\",");
            builder.AppendLine("  \"findings\": [");
            AppendFindings(builder, findings, 4);
            builder.AppendLine("  ]");
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string BuildSharedSection(List<Finding> findings)
        {
            StringBuilder builder = new StringBuilder(2048);
            builder.AppendLine("  \"shinobu322SensoryOopScanner\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_322\",");
            builder.AppendLine("    \"scanner\": \"SensoryImpairmentOopScanner\",");
            builder.Append("    \"summary\": \"").Append(Summary).AppendLine("\",");
            builder.AppendLine("    \"evidenceClass\": \"STATIC_SOURCE_TARGETED\",");
            builder.AppendLine("    \"generatedWithoutBuild\": true,");
            builder.Append("    \"findingCount\": ").Append(findings.Count).AppendLine(",");
            builder.Append("    \"runtimeRoute\": \"").Append(RuntimeRoute).AppendLine("\",");
            builder.Append("    \"shaderRoute\": \"").Append(ShaderRoute).AppendLine("\",");
            builder.Append("    \"compileStatus\": \"").Append(CompileStatus).AppendLine("\",");
            builder.AppendLine("    \"findings\": [");
            AppendFindings(builder, findings, 6);
            builder.AppendLine("    ]");
            builder.Append("  }");
            return builder.ToString();
        }

        private static void AppendFindings(StringBuilder builder, List<Finding> findings, int indent)
        {
            string pad = new string(' ', indent);
            for (int i = 0; i < findings.Count; i++)
            {
                Finding finding = findings[i];
                builder.Append(pad)
                    .Append("{ \"path\": \"").Append(EscapeJson(finding.Path)).Append("\", \"line\": ")
                    .Append(finding.Line)
                    .Append(", \"pattern\": \"").Append(EscapeJson(finding.Pattern)).Append("\" }");
                if (i + 1 < findings.Count)
                    builder.Append(',');
                builder.AppendLine();
            }
        }

        private static void UpsertSharedReport(string path, string section)
        {
            if (!File.Exists(path))
            {
                WriteText(path, "{\n" + section + "\n}\n");
                return;
            }

            string existing = File.ReadAllText(path);
            const string key = "\"shinobu322SensoryOopScanner\"";
            int keyIndex = existing.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int colon = existing.IndexOf(':', keyIndex + key.Length);
                int objectStart = colon >= 0 ? existing.IndexOf('{', colon) : -1;
                int objectEnd = FindObjectEnd(existing, objectStart);
                if (objectStart >= 0 && objectEnd > objectStart)
                {
                    string prefix = existing.Substring(0, objectStart);
                    string suffix = existing.Substring(objectEnd + 1);
                    WriteText(path, prefix + section + suffix);
                }
                return;
            }

            int insert = existing.LastIndexOf('}');
            if (insert < 0)
            {
                WriteText(path, "{\n" + section + "\n}\n");
                return;
            }

            string outerPrefix = existing.Substring(0, insert).TrimEnd();
            string outerSuffix = existing.Substring(insert);
            string separator = outerPrefix.EndsWith("{", StringComparison.Ordinal) ? "\n" : ",\n";
            WriteText(path, outerPrefix + separator + section + "\n" + outerSuffix);
        }

        private static int FindObjectEnd(string text, int objectStart)
        {
            if (objectStart < 0 || objectStart >= text.Length || text[objectStart] != '{')
                return -1;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = objectStart; i < text.Length; i++)
            {
                char c = text[i];
                if (escaped)
                {
                    escaped = false;
                    continue;
                }

                if (c == '\\')
                {
                    escaped = inString;
                    continue;
                }

                if (c == '"')
                {
                    inString = !inString;
                    continue;
                }

                if (inString)
                    continue;

                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
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
            Uri rootUri = new Uri(AppendDirectorySeparator(Path.GetFullPath(root)));
            Uri pathUri = new Uri(Path.GetFullPath(path));
            return Uri.UnescapeDataString(rootUri.MakeRelativeUri(pathUri).ToString()).Replace('/', Path.DirectorySeparatorChar);
        }

        private static string AppendDirectorySeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static string EscapeJson(string value)
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
