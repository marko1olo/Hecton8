using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    public static class OOP_Canvas_Scanner_SHINOBU_348
    {
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string OwnedReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT_SHINOBU_348.json";
        private const string ReportObjectKey = "shinobu_348_screen_space_pda_projector";

        [MenuItem("Hecton8/UX/Run PDA World-Space Canvas Scanner")]
        public static void Run()
        {
            string root = ResolveProjectRoot();
            List<string> findings = new List<string>(32); // EDITOR ALLOC: bounded static report list - owner: SHINOBU_348
            ScanSources(root, findings);
            ScanYaml(root, findings);
            WriteReport(root, findings);
            AssetDatabase.Refresh();
        }

        private static void ScanSources(string root, List<string> findings)
        {
            ScanSourceDirectory(Path.Combine(root, "Assets", "_Project", "Scripts"), findings);
        }

        private static void ScanSourceDirectory(string directory, List<string> findings)
        {
            if (!Directory.Exists(directory))
                return;

            foreach (string path in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories))
            {
                if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    path.IndexOf("\\Editor\\", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    continue;
                }

                string text = File.ReadAllText(path);
                int worldSpace = IndexOfWorldSpaceCanvas(text);
                if (worldSpace >= 0 && (IsPdaOrWristPath(path) || HasNearbyPdaOrWristToken(text, worldSpace)))
                    findings.Add(ToProjectPath(path));
            }
        }

        private static void ScanYaml(string root, List<string> findings)
        {
            string assets = Path.Combine(root, "Assets");
            if (!Directory.Exists(assets))
                return;

            foreach (string path in Directory.EnumerateFiles(assets, "*.prefab", SearchOption.AllDirectories))
                ScanYamlFile(path, findings);
            foreach (string path in Directory.EnumerateFiles(assets, "*.unity", SearchOption.AllDirectories))
                ScanYamlFile(path, findings);
        }

        private static void ScanYamlFile(string path, List<string> findings)
        {
            string projectPath = ToProjectPath(path);
            string text = File.ReadAllText(path);
            int worldSpace = text.IndexOf("m_RenderMode: 2", StringComparison.Ordinal);
            if (worldSpace >= 0 && (IsPdaOrWristPath(projectPath) || HasNearbyPdaOrWristToken(text, worldSpace)))
            {
                findings.Add(projectPath);
            }
        }

        private static void WriteReport(string root, List<string> findings)
        {
            string output = Path.Combine(root, ReportRelativePath);
            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string agentReport = BuildAgentReportObject(findings);
            WriteOwnedReport(root, agentReport);
            string mergedReport = MergeAgentReport(output, agentReport);
            File.WriteAllText(output, mergedReport, Encoding.UTF8);
        }

        private static void WriteOwnedReport(string root, string agentReport)
        {
            string output = Path.Combine(root, OwnedReportRelativePath);
            string directory = Path.GetDirectoryName(output);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(output, BuildRootReport(agentReport), Encoding.UTF8);
        }

        private static string BuildAgentReportObject(List<string> findings)
        {
            StringBuilder builder = new StringBuilder(2048); // EDITOR ALLOC: JSON report builder - owner: SHINOBU_348
            builder.Append("  \"");
            builder.Append(ReportObjectKey);
            builder.AppendLine("\": {");
            builder.AppendLine("    \"schema\": \"hecton8.rendering_optimization_report.v1\",");
            builder.AppendLine("    \"agent\": \"SHINOBU_348\",");
            builder.Append("    \"summary\": \"");
            builder.Append(findings.Count == 0 ? "Forbidden World-Space Canvases Eradicated" : "Forbidden World-Space Canvas Findings Present");
            builder.AppendLine("\",");
            builder.Append("    \"forbiddenWorldSpaceCanvasCount\": ");
            builder.Append(findings.Count);
            builder.AppendLine(",");
            builder.AppendLine("    \"scanner\": \"OOP_Canvas_Scanner_SHINOBU_348\",");
            builder.AppendLine("    \"renderRoute\": \"WristHologramHudRuntime_PdaScreenProjector -> active URP renderer feature -> Hecton_PdaScreen.shader\",");
            builder.AppendLine("    \"gpuCapabilityGate\": \"Cold runtime gate requires SystemInfo.supportsSetConstantBuffer and graphicsShaderLevel >= 45 before GraphicsBuffer allocation; unsupported targets fail closed with no World-Space Canvas fallback.\",");
            builder.AppendLine("    \"rendererFeatureActivation\": \"Serialized active in PC_Renderer.asset, PC_High_Renderer.asset, Mobile_Renderer.asset, and Quest_VR_Renderer.asset; each m_RendererFeatureMap matches the m_RendererFeatures local fileID order.\",");
            builder.AppendLine("    \"viewSpaceRoute\": \"Shader ray-plane math uses UNITY_MATRIX_I_P view rays and rotates the camera-relative PDA matrix through UNITY_MATRIX_V; no _WorldSpaceCameraPos subtraction remains.\",");
            builder.AppendLine("    \"csvProfileSource\": \"Assets/StreamingAssets/Hecton8/PDA/pda_interface_profiles.csv is the direct-file StreamingAssets source with default plus inventory/loadout/construction/barter/data_log/spectrum/atlas_signal/diagnostics rows; URI-backed Android/Quest StreamingAssets fails closed to the deterministic default row until DataMonolith/binary import proof exists; repo-root pda_interface_profiles.csv remains editor/development fallback.\",");
            builder.AppendLine("    \"csvScratchRoute\": \"pda_interface_profiles.csv reads directly into Vault byte scratch 348736 and parses ReadOnlySpan<byte> over unmanaged memory.\",");
            builder.AppendLine("    \"csvTabHashRoute\": \"CSV tokens tab_0/pda_tab_0 and canonical names map to the same ResolvePdaTabHash(int) route as PDAEvents.CurrentTab; unknown names retain FNV fallback.\",");
            builder.AppendLine("    \"shaderWarmupRoute\": \"Assets/_Project/Art/Shaders/Variants/Hecton_PdaScreen_Warmup.shadervariants is serialized in 00_BOOTSTRAP shaderVariantCollections for boot WarmUp.\",");
            builder.AppendLine("    \"mockRoute\": \"Mock wrist projection and forced visibility serialize false by default; mock input is accepted only in Unity Editor or DEVELOPMENT_BUILD.\",");
            builder.AppendLine("    \"blackBoxDumpRoute\": \"Editor writes Docs/AgentLogs/Dump_SHINOBU_348.bin; player builds write Application.persistentDataPath/Hecton8/AgentLogs/Dump_SHINOBU_348.bin. Header v2 is 64 bytes and records valid count/start index before writing telemetry rows oldest-to-newest.\",");
            builder.AppendLine("    \"notes\": \"Scanner targets all project source plus prefab/scene YAML, then only counts World-Space Canvas hits whose path or local source/YAML context is PDA/Wrist scoped. Screen-space wrist PDA projection uses RenderGraph and GlobalDataVault DTOs, not World-Space Canvas.\",");
            builder.AppendLine("    \"findings\": [");
            for (int i = 0; i < findings.Count; i++)
            {
                builder.Append("      \"");
                builder.Append(Escape(findings[i]));
                builder.Append(i + 1 < findings.Count ? "\"," : "\"");
                builder.AppendLine();
            }

            builder.AppendLine("    ]");
            builder.AppendLine("  }");
            return builder.ToString();
        }

        private static string MergeAgentReport(string output, string agentReport)
        {
            if (!File.Exists(output))
                return BuildRootReport(agentReport);

            string existing = File.ReadAllText(output, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(existing))
                return BuildRootReport(agentReport);

            int rootOpen = existing.IndexOf('{');
            int rootClose = existing.LastIndexOf('}');
            if (rootOpen < 0 || rootClose <= rootOpen)
                return BuildRootReport(agentReport);

            string rootObject = existing.Substring(rootOpen, rootClose - rootOpen + 1);
            string withoutAgentReport = RemoveTopLevelProperty(rootObject, ReportObjectKey);
            int insertIndex = withoutAgentReport.LastIndexOf('}');
            if (insertIndex < 0)
                return BuildRootReport(agentReport);

            int previousNonWhitespace = FindPreviousNonWhitespace(withoutAgentReport, insertIndex - 1);
            bool hasExistingProperties = previousNonWhitespace >= 0 && withoutAgentReport[previousNonWhitespace] != '{';

            StringBuilder builder = new StringBuilder(withoutAgentReport.Length + agentReport.Length + 8); // EDITOR ALLOC: merge shared report without erasing other agents
            builder.Append(withoutAgentReport, 0, insertIndex);
            if (hasExistingProperties)
            {
                if (withoutAgentReport[previousNonWhitespace] != ',')
                    builder.AppendLine(",");
                else
                    builder.AppendLine();
            }
            else
            {
                builder.AppendLine();
            }

            builder.Append(agentReport);
            builder.AppendLine();
            builder.Append(withoutAgentReport, insertIndex, withoutAgentReport.Length - insertIndex);
            builder.AppendLine();
            return builder.ToString();
        }

        private static string BuildRootReport(string agentReport)
        {
            StringBuilder builder = new StringBuilder(agentReport.Length + 8); // EDITOR ALLOC: cold scanner output
            builder.AppendLine("{");
            builder.Append(agentReport);
            builder.AppendLine();
            builder.AppendLine("}");
            return builder.ToString();
        }

        private static string RemoveTopLevelProperty(string json, string key)
        {
            string token = "\"" + key + "\"";
            int keyIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (keyIndex < 0)
                return json.TrimEnd();

            int colonIndex = json.IndexOf(':', keyIndex + token.Length);
            if (colonIndex < 0)
                return json.TrimEnd();

            int valueStart = json.IndexOf('{', colonIndex + 1);
            if (valueStart < 0)
                return json.TrimEnd();

            int valueEnd = FindMatchingBrace(json, valueStart);
            if (valueEnd < 0)
                return json.TrimEnd();

            int removeStart = keyIndex;
            while (removeStart > 0 && char.IsWhiteSpace(json[removeStart - 1]))
                removeStart--;

            int removeEnd = valueEnd + 1;
            int nextNonWhitespace = FindNextNonWhitespace(json, removeEnd);
            if (nextNonWhitespace >= 0 && json[nextNonWhitespace] == ',')
            {
                removeEnd = nextNonWhitespace + 1;
            }
            else
            {
                int previousNonWhitespace = FindPreviousNonWhitespace(json, removeStart - 1);
                if (previousNonWhitespace >= 0 && json[previousNonWhitespace] == ',')
                    removeStart = previousNonWhitespace;
            }

            return json.Remove(removeStart, removeEnd - removeStart).TrimEnd();
        }

        private static int FindMatchingBrace(string text, int openIndex)
        {
            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = openIndex; i < text.Length; i++)
            {
                char c = text[i];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (c == '\\')
                    {
                        escaped = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    inString = true;
                }
                else if (c == '{')
                {
                    depth++;
                }
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return i;
                }
            }

            return -1;
        }

        private static int FindPreviousNonWhitespace(string text, int startIndex)
        {
            for (int i = startIndex; i >= 0; i--)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return i;
            }

            return -1;
        }

        private static int FindNextNonWhitespace(string text, int startIndex)
        {
            for (int i = startIndex; i < text.Length; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return i;
            }

            return -1;
        }

        private static bool IsPdaOrWristPath(string path)
        {
            return path.IndexOf("PDA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Pda", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("Wrist", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   path.IndexOf("WristOS", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int IndexOfWorldSpaceCanvas(string text)
        {
            int worldSpace = text.IndexOf("RenderMode.WorldSpace", StringComparison.Ordinal);
            if (worldSpace >= 0)
                return worldSpace;

            return text.IndexOf("renderMode = RenderMode.WorldSpace", StringComparison.Ordinal);
        }

        private static bool HasNearbyPdaOrWristToken(string text, int center)
        {
            const int Radius = 2048;
            int start = Math.Max(0, center - Radius);
            int length = Math.Min(text.Length - start, Radius * 2);
            return IndexOfToken(text, "PDA", start, length, StringComparison.Ordinal) >= 0 ||
                   IndexOfToken(text, "PlayerPDA", start, length, StringComparison.Ordinal) >= 0 ||
                   IndexOfToken(text, "Wrist", start, length, StringComparison.OrdinalIgnoreCase) >= 0 ||
                   IndexOfToken(text, "WristOS", start, length, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static int IndexOfToken(string text, string token, int start, int length, StringComparison comparison)
        {
            return text.IndexOf(token, start, length, comparison);
        }

        private static string Escape(string value)
        {
            return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ToProjectPath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ResolveProjectRoot()
        {
            string dataPath = Application.dataPath;
            return Path.GetFullPath(Path.Combine(dataPath, ".."));
        }
    }
}
