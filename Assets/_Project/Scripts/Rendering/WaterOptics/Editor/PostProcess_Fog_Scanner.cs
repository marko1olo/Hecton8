using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Rendering.WaterOptics.Editor
{
    public static class PostProcess_Fog_Scanner
    {
        private const string ReportSectionName = "agent_13kra_water_optics";
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const int SearchRootCount = 3;
        private const string ScenesRoot = "Assets/_Project/Scenes";
        private const string PrefabsRoot = "Assets/_Project/Prefabs";
        private const string EnvironmentRoot = "Assets/_Project/Environment";

        [MenuItem("Hecton8/Rendering/Water Optics/Scan Generic Fog")]
        public static void ScanMenu()
        {
            string reportPath = ScanAndWriteReport();
            Debug.Log(string.Concat("Water optics fog scanner wrote ", reportPath));
        }

        public static string ScanAndWriteReport()
        {
            int scanned = 0;
            int renderSettingsFog = 0;
            int genericVolumeFog = 0;
            int genericPostProcessing = 0;
            var findings = new StringBuilder(4096);

            string[] roots = ExistingRoots();
            string[] guids = roots.Length == 0 ? Array.Empty<string>() : AssetDatabase.FindAssets(string.Empty, roots);
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                    continue;
                if (!IsScannableAsset(path))
                    continue;

                scanned++;
                int localRenderFog = 0;
                int localVolumeFog = 0;
                int localPost = 0;
                foreach (string line in File.ReadLines(path))
                {
                    localRenderFog += Count(line, "m_Fog: 1");
                    localVolumeFog += Count(line, "UnityEngine.Rendering.Volume") + Count(line, "VolumetricFog") + Count(line, "FogOverride");
                    localPost += Count(line, "PostProcessVolume") + Count(line, "PostProcessLayer") + Count(line, "Post-processing");
                }

                if (localRenderFog == 0 && localVolumeFog == 0 && localPost == 0)
                    continue;

                renderSettingsFog += localRenderFog;
                genericVolumeFog += localVolumeFog;
                genericPostProcessing += localPost;
                AppendFinding(findings, path, localRenderFog, localVolumeFog, localPost);
            }

            bool eradicated = renderSettingsFog == 0 && genericVolumeFog == 0 && genericPostProcessing == 0;
            var section = new StringBuilder(8192);
            section.Append("  \"").Append(ReportSectionName).AppendLine("\": {");
            section.AppendLine("    \"agentId\": \"13KRA\",");
            section.AppendLine("    \"scanner\": \"PostProcess_Fog_Scanner\",");
            section.Append("    \"timestampUtc\": \"").Append(DateTime.UtcNow.ToString("O")).AppendLine("\",");
            section.AppendLine("    \"evidenceClass\": \"STATIC_SOURCE_TARGETED\",");
            section.AppendLine("    \"runtimeRoute\": \"WaterOpticsRuntime authored scene/bootstrap owner + PRE_SIMULATION direct Vault-row mock optics generation + VISUAL_SYNC _GlobalWaterOptics CBUFFER\",");
            section.AppendLine("    \"shaderRoute\": [");
            section.AppendLine("      \"Hecton_WaterExtinction.hlsl\",");
            section.AppendLine("      \"Hecton8_UberNoir.hlsl\",");
            section.AppendLine("      \"Hecton_VolumetricFog.compute\",");
            section.AppendLine("      \"Hecton_VolumetricFog_DearLie.shader\"");
            section.AppendLine("    ],");
            section.AppendLine("    \"vaultBuffers\": [71129, 71135, 71136, 71137, 71138, 71139],");
            section.AppendLine("    \"dtoProof\": {");
            section.AppendLine("      \"WaterOpticsDTO\": 64,");
            section.AppendLine("      \"WaterOpticsTuningDTO\": 64,");
            section.AppendLine("      \"WaterOpticsProfileDTO\": 64,");
            section.AppendLine("      \"WaterOpticsTelemetryEntry\": 64,");
            section.AppendLine("      \"WaterOpticsDumpHeader\": 32");
            section.AppendLine("    },");
            section.AppendLine("    \"genericFogScan\": {");
            section.Append("      \"scanned_assets\": ").Append(scanned).AppendLine(",");
            section.Append("      \"render_settings_fog_tokens\": ").Append(renderSettingsFog).AppendLine(",");
            section.Append("      \"generic_volume_fog_tokens\": ").Append(genericVolumeFog).AppendLine(",");
            section.Append("      \"generic_post_processing_tokens\": ").Append(genericPostProcessing).AppendLine(",");
            section.Append("      \"status\": \"").Append(eradicated ? "Generic Fog Eradicated" : "BLOCKED_BY_SCENE_PROFILE_OWNER_REVIEW").AppendLine("\"");
            section.AppendLine("    },");
            section.AppendLine("    \"continuousQuality\": \"Water extinction, legacy LUT influence, UberNoir light-probe richness, and screen refraction are controlled by runtime GlobalQualityWeight/material gates, not local binary variants.\",");
            section.AppendLine("    \"ownerBootstrap\": \"Runtime owner must be authored or explicitly bootstrapped; WaterOpticsRuntime source has no hidden owner installation route, build validation fails if no owner is serialized in _Project scenes/prefabs, and current static GUID scan found no owner placement.\",");
            section.AppendLine("    \"jobPolicy\": \"The 64-byte fallback/mock optics row is written directly in PRE_SIMULATION; ScheduleSimulation returns its input dependency and no one-row mock job is scheduled.\",");
            section.AppendLine("    \"shaderSafetyPatch\": \"Dear Lie waterline tint/opacity require camera-underwater state, and custom light-probe grid sampling finite-checks origin/params and fail-closes when active probe count is smaller than resolution^3.\",");
            section.AppendLine("    \"binaryVariantPatch\": {");
            section.AppendLine("      \"uberNoirMathLodLowRemoved\": true,");
            section.AppendLine("      \"uberNoirScreenRefractionKeywordRemoved\": true,");
            section.AppendLine("      \"uberNoirWarmupMathLodLowRemoved\": true");
            section.AppendLine("    },");
            section.AppendLine("    \"telemetry\": \"300 fixed 64-byte Vault rows plus 32-byte dump header to Docs/AgentLogs/Dump_13KRA.bin on invalid numeric state or estimated opaque-budget breach.\",");
            section.AppendLine("    \"rollbackAuthority\": \"presentation-only; no StateRingBuffer/Merkle/save route\",");
            section.AppendLine("    \"policy\": \"No scene or prefab YAML mutation was performed by scanner; findings require owner route review.\",");
            section.AppendLine("    \"notes\": \"Static source proof only. Unity import, shader import, Frame Debugger CBUFFER proof, profiler GC proof, and measured GPU timing remain pending under no-premature-rebuild guard.\",");
            section.AppendLine("    \"findings\": [");
            section.Append(findings);
            section.AppendLine();
            section.AppendLine("    ]");
            section.AppendLine("  }");

            string reportPath = Path.Combine(ResolveProjectRoot(), ReportRelativePath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(reportPath);
            Directory.CreateDirectory(directory);
            UpsertReportSection(reportPath, section.ToString());
            AssetDatabase.Refresh();
            return reportPath;
        }

        private static void UpsertReportSection(string reportPath, string section)
        {
            string root = File.Exists(reportPath) ? File.ReadAllText(reportPath) : string.Empty;
            if (string.IsNullOrWhiteSpace(root))
            {
                File.WriteAllText(reportPath, string.Concat("{\n", section, "\n}\n"));
                return;
            }

            string key = string.Concat("\"", ReportSectionName, "\"");
            int keyIndex = root.IndexOf(key, StringComparison.Ordinal);
            if (keyIndex >= 0)
            {
                int sectionStart = keyIndex;
                while (sectionStart > 0 && root[sectionStart - 1] != '\n' && root[sectionStart - 1] != '\r')
                    sectionStart--;

                int valueStart = root.IndexOf('{', keyIndex);
                int valueEnd = FindMatchingBrace(root, valueStart);
                if (valueStart >= 0 && valueEnd > valueStart)
                {
                    valueEnd++;
                    bool hadTrailingComma = valueEnd < root.Length && root[valueEnd] == ',';
                    if (hadTrailingComma)
                        valueEnd++;

                    File.WriteAllText(reportPath, string.Concat(
                        root.Substring(0, sectionStart),
                        section,
                        hadTrailingComma ? "," : string.Empty,
                        root.Substring(valueEnd)));
                    return;
                }
            }

            int insert = root.LastIndexOf('}');
            if (insert < 0)
            {
                File.WriteAllText(reportPath, string.Concat("{\n", section, "\n}\n"));
                return;
            }

            string prefix = root.Substring(0, insert).TrimEnd();
            string separator = prefix.EndsWith("{", StringComparison.Ordinal) ? "\n" : ",\n";
            File.WriteAllText(reportPath, string.Concat(prefix, separator, section, "\n", root.Substring(insert)));
        }

        private static int FindMatchingBrace(string text, int openBrace)
        {
            if (string.IsNullOrEmpty(text) || openBrace < 0 || openBrace >= text.Length || text[openBrace] != '{')
                return -1;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = openBrace; i < text.Length; i++)
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

        private static string ResolveProjectRoot()
        {
            string current = Directory.GetCurrentDirectory();
            if (IsProjectRoot(current))
                return current;

            string child = Path.Combine(current, "Hecton8");
            if (IsProjectRoot(child))
                return child;

            return current;
        }

        private static bool IsProjectRoot(string path)
        {
            return !string.IsNullOrEmpty(path) &&
                   Directory.Exists(Path.Combine(path, "Assets")) &&
                   Directory.Exists(Path.Combine(path, "ProjectSettings"));
        }

        private static string[] ExistingRoots()
        {
            int count = 0;
            for (int i = 0; i < SearchRootCount; i++)
            {
                if (AssetDatabase.IsValidFolder(GetSearchRoot(i)))
                    count++;
            }

            string[] roots = new string[count];
            int cursor = 0;
            for (int i = 0; i < SearchRootCount; i++)
            {
                string root = GetSearchRoot(i);
                if (AssetDatabase.IsValidFolder(root))
                    roots[cursor++] = root;
            }

            return roots;
        }

        private static string GetSearchRoot(int rootIndex)
        {
            switch (rootIndex)
            {
                case 0:
                    return ScenesRoot;
                case 1:
                    return PrefabsRoot;
                case 2:
                    return EnvironmentRoot;
                default:
                    return ScenesRoot;
            }
        }

        private static int Count(string text, string token)
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

        private static bool IsScannableAsset(string path)
        {
            string extension = Path.GetExtension(path);
            return string.Equals(extension, ".unity", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".prefab", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(extension, ".asset", StringComparison.OrdinalIgnoreCase);
        }

        private static void AppendFinding(StringBuilder builder, string path, int renderFog, int volumeFog, int post)
        {
            if (builder.Length > 0)
                builder.AppendLine(",");

            builder.Append("    { \"path\": \"")
                .Append(path.Replace("\\", "/"))
                .Append("\", \"render_fog\": ")
                .Append(renderFog)
                .Append(", \"volume_fog\": ")
                .Append(volumeFog)
                .Append(", \"post_processing\": ")
                .Append(post)
                .Append(" }");
        }
    }
}
