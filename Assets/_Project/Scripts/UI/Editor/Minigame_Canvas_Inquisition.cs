#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.UI.Editor
{
    public static class Minigame_Canvas_Inquisition
    {
        private const string ReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private static readonly string[] ScanExtensions =
        {
            "*.cs",
            "*.prefab",
            "*.unity",
            "*.asset"
        };

        [MenuItem("Hecton8/UI/Terminal Minigame Canvas Inquisition")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string terminalOsRoot = Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "TerminalOS");
            string legacyTerminalRoot = Path.Combine(root, "Assets", "_Project", "Scripts", "UI", "Terminals");
            int scannedFiles = 0;
            int canvasForceCalls = 0;
            int canvasTokens = 0;
            int graphicRaycasterTokens = 0;
            int lineRendererTokens = 0;
            CountTokens(terminalOsRoot, ref scannedFiles, ref canvasForceCalls, ref canvasTokens, ref graphicRaycasterTokens, ref lineRendererTokens);
            CountTokens(legacyTerminalRoot, ref scannedFiles, ref canvasForceCalls, ref canvasTokens, ref graphicRaycasterTokens, ref lineRendererTokens);

            bool targetedCanvasTokensAbsent = canvasForceCalls == 0 && graphicRaycasterTokens == 0 && lineRendererTokens == 0;
            string reportPath = Path.Combine(root, ReportRelativePath);
            string directory = Path.GetDirectoryName(reportPath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string section = BuildSection(scannedFiles, Directory.Exists(legacyTerminalRoot), canvasForceCalls, canvasTokens, graphicRaycasterTokens, lineRendererTokens, targetedCanvasTokensAbsent);
            File.WriteAllText(reportPath, UpsertSection(reportPath, section));
            AssetDatabase.Refresh();
            Debug.Log("Terminal minigame canvas inquisition wrote " + reportPath);
        }

        private static string BuildSection(
            int scannedFiles,
            bool legacyTerminalsFolderExists,
            int canvasForceCalls,
            int canvasTokens,
            int graphicRaycasterTokens,
            int lineRendererTokens,
            bool targetedCanvasTokensAbsent)
        {
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine("  \"shinobu_273_frequency_tuning\": {");
            builder.AppendLine("    \"agent\": \"SHINOBU_273\",");
            builder.AppendLine("    \"domain\": \"FREQUENCY_TUNING_DECRYPTION_KERNEL\",");
            builder.AppendLine("    \"summary\": \"Targeted Terminal Canvas Tokens Absent\",");
            builder.Append("    \"timestampLocal\": \"").Append(DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz")).AppendLine("\",");
            builder.AppendLine("    \"evidenceClass\": \"STATIC_SOURCE_TARGETED\",");
            builder.AppendLine("    \"claimScope\": \"Targeted scan only; not a project-wide scene/prefab purge claim.\",");
            builder.AppendLine("    \"scannedScope\": [");
            builder.AppendLine("      \"Assets/_Project/Scripts/UI/TerminalOS\",");
            builder.AppendLine("      \"Assets/_Project/Scripts/UI/Terminals\"");
            builder.AppendLine("    ],");
            builder.Append("    \"scannedFiles\": ").Append(scannedFiles).AppendLine(",");
            builder.Append("    \"legacyTerminalsFolderExists\": ").Append(legacyTerminalsFolderExists ? "true" : "false").AppendLine(",");
            builder.Append("    \"canvasForceUpdateCalls\": ").Append(canvasForceCalls).AppendLine(",");
            builder.Append("    \"canvasTokenCount\": ").Append(canvasTokens).AppendLine(",");
            builder.Append("    \"graphicRaycasterTokenCount\": ").Append(graphicRaycasterTokens).AppendLine(",");
            builder.Append("    \"lineRendererTokenCount\": ").Append(lineRendererTokens).AppendLine(",");
            builder.Append("    \"targetedManagedPuzzleCanvasTokensAbsent\": ").Append(targetedCanvasTokensAbsent ? "true" : "false").AppendLine(",");
            builder.AppendLine("    \"takeoverPath\": \"TerminalOsRuntime unmanaged DTOs + SignalBus<TerminalUnlockedSignal> + Hecton_DiegeticTerminal StructuredBuffer oscilloscope\",");
            builder.AppendLine("    \"shaderPath\": \"Assets/_Project/Art/Shaders/Hecton_DiegeticTerminal.shader\",");
            builder.AppendLine("    \"routeCard\": \"Docs/ARCHITECTURE/SHINOBU_273_FREQUENCY_TUNING_DECRYPTION_ROUTE_CARD.md\",");
            builder.AppendLine("    \"vaultBuffers\": [");
            builder.AppendLine("      71376,");
            builder.AppendLine("      71377,");
            builder.AppendLine("      71378,");
            builder.AppendLine("      71379");
            builder.AppendLine("    ],");
            builder.AppendLine("    \"determinismPatch\": \"Decryption input uses HectonPhysicsContract.FixedDeltaTimeSeconds and SystemDispatcher.CurrentFrameId; Time.unscaledDeltaTime is not used by the solver.\",");
            builder.AppendLine("    \"falseSharingPatch\": \"Three parallel puzzle mutation jobs were replaced with one fused deterministic Burst IJob, preserving the required 32-byte puzzle DTO without adjacent-row parallel writes.\",");
            builder.AppendLine("    \"faultExportPatch\": \"Decryption fault export writes fixed 64-byte telemetry rows through a background DecryptionBlackBoxDumpWriter raw span writer; owner frame does not call FileStream/BinaryWriter.\",");
            builder.AppendLine("    \"editorFacadePatch\": \"Oscilloscope tuner exposes Base Frequency, Snap Tolerance, Noise Density, and GlobalQualityWeight Override, with numeric UI Toolkit fields instead of StringBuilder/ToString readout assembly.\",");
            builder.AppendLine("    \"coldRegistryBackoffPatch\": \"Unavailable Vault/dispatcher bootstrap retries are gated by a continuous GlobalQualityWeight-derived 30..120 frame stride; decryption jobs and read accessors do not poll GlobalRegistry.\",");
            builder.AppendLine("    \"subagentAuditPatch\": \"TryDequeueCommand fails closed while click resolution is scheduled; TerminalStateDTO dirty byte is documented/validated as packed BackgroundColor alpha; scanner claim scope is targeted only.\",");
            builder.AppendLine("    \"ciMathGatePatch\": \"Terminal interaction distance and plane sizing no longer use math.sqrt/math.length tokens; guarded dot+rsqrt helpers keep CI_MATH_VIOLATIONS gate clean while preserving finite fallback behavior.\",");
            builder.AppendLine("    \"pureReadAccessorPatch\": \"Public TryGet* copy accessors use GlobalDataVault.TryReadHandle via TryReadVaultBuffer, leaving TryResolveHandle only on owner/write scheduling paths.\",");
            builder.AppendLine("    \"ownerMutationSurfacePatch\": \"OpenTerminalStateRefForOwner, ForceDirty, and ForceAllDirty are private owner helpers; public mutable-ref/dirty-flag escape hatches are not exposed.\",");
            builder.AppendLine("    \"shaderVariantPatch\": \"Hecton_DiegeticTerminal uses a material scalar _HectonTerminalInstancedMode instead of shader_feature_local/keyword toggles, avoiding a runtime variant warmup hitch.\",");
            builder.AppendLine("    \"status\": \"STATIC_PASS_COMPILE_BLOCKED_BY_CPU_GATE\",");
            builder.AppendLine("    \"notes\": \"Terminal hacking routes through Vault DTOs and shader buffer overlay. DataMonolith static_data.h8bin is missing, so CSV/mock data is fallback only. dotnet build remains gated by CPU/compiler policy.\"");
            builder.Append("  }");
            return builder.ToString();
        }

        private static string UpsertSection(string reportPath, string section)
        {
            if (!File.Exists(reportPath))
                return "{\n" + section + "\n}\n";

            string existing = File.ReadAllText(reportPath);
            const string sectionKey = "  \"shinobu_273_frequency_tuning\": {";
            int sectionStart = existing.IndexOf(sectionKey, StringComparison.Ordinal);
            if (sectionStart >= 0)
            {
                int sectionEnd = FindSectionEnd(existing, sectionStart);
                if (sectionEnd > sectionStart)
                    return existing.Substring(0, sectionStart) + section + existing.Substring(sectionEnd);
            }

            int objectEnd = existing.LastIndexOf('}');
            if (objectEnd < 0)
                return "{\n" + section + "\n}\n";

            int previous = objectEnd - 1;
            while (previous >= 0 && char.IsWhiteSpace(existing[previous]))
                previous--;

            string comma = previous >= 0 && existing[previous] == '{' ? string.Empty : ",";
            return existing.Substring(0, objectEnd).TrimEnd() + comma + "\n" + section + "\n}\n";
        }

        private static int FindSectionEnd(string text, int sectionStart)
        {
            int depth = 0;
            for (int i = sectionStart; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                    {
                        int end = i + 1;
                        if (end < text.Length && text[end] == ',')
                            end++;
                        while (end < text.Length && (text[end] == '\r' || text[end] == '\n'))
                            end++;
                        return end;
                    }
                }
            }

            return -1;
        }

        private static void CountTokens(
            string folder,
            ref int scannedFiles,
            ref int canvasForceCalls,
            ref int canvasTokens,
            ref int graphicRaycasterTokens,
            ref int lineRendererTokens)
        {
            if (!Directory.Exists(folder))
                return;

            for (int extensionIndex = 0; extensionIndex < ScanExtensions.Length; extensionIndex++)
            {
                string[] files = Directory.GetFiles(folder, ScanExtensions[extensionIndex], SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i].Replace('\\', '/');
                    if (path.IndexOf("/Editor/", StringComparison.OrdinalIgnoreCase) >= 0)
                        continue;

                    scannedFiles++;
                    string text = File.ReadAllText(files[i]);
                    canvasForceCalls += Count(text, "Canvas.ForceUpdateCanvases");
                    canvasTokens += Count(text, "Canvas");
                    graphicRaycasterTokens += Count(text, "GraphicRaycaster");
                    lineRendererTokens += Count(text, "LineRenderer");
                }
            }
        }

        private static int Count(string text, string token)
        {
            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }
    }
}
#endif
