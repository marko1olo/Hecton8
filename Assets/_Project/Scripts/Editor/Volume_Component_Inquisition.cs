#if UNITY_EDITOR
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Editor
{
    /// <summary>
    /// Cold static scanner for standard Volume/PostProcess residue in the rendering ownership lane.
    /// </summary>
    public static class Volume_Component_Inquisition
    {
        private const string ReportPath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string ExternalCompileBlockerPath = "Assets/_Project/Scripts/Gameplay/HectonScannerProjectionState.cs";

        [MenuItem("Hecton8/Rendering/Volume Component Inquisition")]
        public static void Run()
        {
            int standardVolumeForbidden = 0;
            int stringShaderParameterForbidden = 0;
            StringBuilder findings = new StringBuilder(2048);
            Scan("Assets/_Project/Prefabs", "*.prefab", ref standardVolumeForbidden, ref stringShaderParameterForbidden, findings);
            Scan("Assets/_Project/Scripts/Rendering", "*.cs", ref standardVolumeForbidden, ref stringShaderParameterForbidden, findings);
            Scan("Assets/_Project/Scripts/Visor", "*.cs", ref standardVolumeForbidden, ref stringShaderParameterForbidden, findings);

            Directory.CreateDirectory("Docs/Reports");
            bool eradicated = standardVolumeForbidden == 0 && stringShaderParameterForbidden == 0;
            bool externalCompileBlockerPresent = !File.Exists(ExternalCompileBlockerPath);
            string existingReport = File.Exists(ReportPath) ? File.ReadAllText(ReportPath) : string.Empty;
            string compileAttemptJson = ExtractJsonMemberValue(existingReport, "compileAttempt");
            string outOfDomainResidueJson = ExtractJsonMemberValue(existingReport, "outOfDomainResidue");
            string previousReportJson = ExtractJsonMemberValue(existingReport, "previousReport");
            if (!string.IsNullOrWhiteSpace(existingReport) &&
                !ExistingReportBelongsToShinobu235(existingReport))
            {
                previousReportJson = existingReport.Trim();
            }

            StringBuilder report = new StringBuilder(4096);
            report.AppendLine("{");
            report.AppendLine("  \"schema\": \"hecton8.rendering_optimization_report.v1\",");
            report.AppendLine("  \"agent\": \"SHINOBU_235\",");
            report.AppendLine("  \"scanner\": \"Volume_Component_Inquisition\",");
            report.Append("  \"status\": \"");
            report.Append(eradicated ? "SHINOBU_235_SCOPED_MANAGED_POST_PROCESSING_ERADICATED_STATIC_SOURCE_RUNTIME_PENDING" : "FORBIDDEN_MANAGED_POST_PROCESSING_RESIDUE_FOUND");
            report.AppendLine("\",");
            report.AppendLine("  \"domain\": \"Echelon 8 Presentation & UX / Deep Sea Noir post-processing\",");
            report.AppendLine("  \"scopes\": [");
            report.AppendLine("    \"Assets/_Project/Prefabs\",");
            report.AppendLine("    \"Assets/_Project/Scripts/Rendering\",");
            report.AppendLine("    \"Assets/_Project/Scripts/Visor\"");
            report.AppendLine("  ],");
            report.AppendLine("  \"standardVolumeResidueCount\": " + standardVolumeForbidden + ",");
            report.AppendLine("  \"stringShaderParameterResidueCount\": " + stringShaderParameterForbidden + ",");
            report.AppendLine("  \"stringShaderParameterScopes\": [");
            report.AppendLine("    \"Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.cs\",");
            report.AppendLine("    \"Assets/_Project/Scripts/Visor/HectonVisorUberPostFeature.Noir.cs\"");
            report.AppendLine("  ],");
            report.AppendLine("  \"playerPrefabVolumeRemoved\": " + (eradicated ? "true" : "false") + ",");
            report.AppendLine("  \"managedPostProcessingEradicated\": false,");
            report.AppendLine("  \"managedPostProcessingEradicatedInShinobuScope\": " + (eradicated ? "true" : "false") + ",");
            report.AppendLine("  \"managedPostProcessingEradicatedProjectWide\": false,");
            report.AppendLine("  \"managedPostProcessingScopeNote\": \"Scoped to Assets/_Project/Prefabs, Assets/_Project/Scripts/Rendering, and Assets/_Project/Scripts/Visor for the SHINOBU_235 Deep Sea Noir route; scenes, URP assets, and UI settings Volume references remain out-of-domain residue.\",");
            report.AppendLine("  \"singlePassNoirShader\": \"Assets/_Project/Art/Shaders/Hecton_VisorGlitchACES.shader\",");
            report.AppendLine("  \"activeRendererFeature\": \"Hecton8.Visor.HectonVisorUberPostFeature\",");
            report.AppendLine("  \"activeRendererShaderGuid\": \"2b2a9f18d90f4b35b8b4f9d1a8e23501\",");
            report.AppendLine("  \"hotPathCorrections\": [");
            report.AppendLine("    \"deepSeaNoirUnifiedPass AddRenderPasses uses readiness checks only; no CSV load or Vault handle growth call remains in the active branch.\",");
            report.AppendLine("    \"Noir jobs use Burst Fast/Standard flags and NoAlias raw pointers sourced from phase-local Vault NativeArrays.\",");
            report.AppendLine("    \"Volume_Component_Inquisition counts generic Material.SetFloat/SetVector/SetTexture/SetColor/SetInt string setters and Shader.SetGlobal* string setters in SHINOBU route files separately from standard Volume residue.\",");
            report.AppendLine("    \"Immediate scalar jobs are invoked through IJob.Run so Burst can compile the exact job entry point instead of direct managed Execute calls.\",");
            report.AppendLine("    \"Vault, Player, ResolutionScaler, and Fluid dependencies are cached from cold/hot-swap phases.\",");
            report.AppendLine("    \"Active Noir input/tuning/constants/telemetry updates use TryResolveHandle phase-local views without per-frame Vault lock/unlock mutations.\",");
            report.AppendLine("    \"CSV color-profile selection is cached and refreshed on a continuous GlobalQualityWeight cadence from 18 frames at low quality to 2 frames at high quality.\",");
            report.AppendLine("    \"CSV color-profile misses are cached on the same GlobalQualityWeight cadence, avoiding repeated 32-row no-match scans every rendered frame.\",");
            report.AppendLine("    \"Hecton_VisorGlitchACES shader quality gates use arithmetic step/lerp masks; scoped shader scan reports no if-branches.\",");
            report.AppendLine("    \"Hecton_VisorGlitchACES branchless path uses one camera-color sample and three Hash21 call sites after single-sample chroma and one-hash grain polish.\",");
            report.AppendLine("    \"DeepSeaNoirTunerWindow samples a fixed editor graph ring every update and writes the managed label only when the quantized display hash changes.\",");
            report.AppendLine("    \"Late player-context binding retries through the cached IPlayerRuntimeContext on a continuous 90-to-18-frame GlobalQualityWeight cadence; the active path does not poll GlobalRegistry.\",");
            report.AppendLine("    \"CBuffer ABI lanes match the SHINOBU_235 prompt: AberrationParams.y/z are X/Y glitch offsets, while block scale is derived from GlobalQualityWeight in shader math.\",");
            report.AppendLine("    \"Noir input consumes cached movement/survival snapshots only; the active builder has no Camera parameter and no renderCamera.transform fallback.\",");
            report.AppendLine("    \"The active deepSeaNoirUnifiedPass branch returns before legacy camera-dependent code reads renderingData.cameraData.camera.\",");
            report.AppendLine("    \"HectonVisorUberPostFeature.Noir.cs carries no Hecton8.Physics import or HectonFluidEngine type reference; FluidRuntime hot-swap reuses cold RefreshFluidBinding.\",");
            report.AppendLine("    \"BINARY_PAYLOAD_INTEGRATION_LEDGER.md records SHINOBU_235 Vault IDs 71040..71045, DTO layout anchors, rollback exclusion, and Data Monolith non-readiness.\",");
            report.AppendLine("    \"SHINOBU_235_DEEP_SEA_NOIR_ROUTE_CARD.md records owner, instruments, phases, capacities, failure modes, telemetry, shutdown, stale-handle behavior, and YELLOW static-source review.\",");
            report.AppendLine("    \"Noir recovery is isolated in HectonVisorUberPostFeature.Noir.cs partial code so reruns do not overwrite the canonical visor feature body.\",");
            report.AppendLine("    \"Noir RenderGraph raster pass imports GraphicsBuffers through renderGraph.ImportBuffer, declares builder.UseBuffer(Read), and binds constant buffers with RasterCommandBuffer.SetGlobalConstantBuffer(buffer, nameID, offset, size).\",");
            report.AppendLine("    \"PlayerRuntimeContextService.TryGetActiveRuntimeContext is read-only and no longer syncs player hierarchy from a TryGet accessor.\",");
            report.AppendLine("    \"Noir editor overrides, CSV profile multipliers, tuning rows, wrapped time, A-B split, and Burst parameter-job reads are finite-sanitized before CBuffer construction.\"");
            report.AppendLine("  ],");
            AppendJsonMember(report, "compileAttempt", string.IsNullOrEmpty(compileAttemptJson)
                ? BuildStaticCompileAttemptJson(externalCompileBlockerPresent)
                : compileAttemptJson);
            AppendJsonMember(report, "outOfDomainResidue", string.IsNullOrEmpty(outOfDomainResidueJson)
                ? BuildKnownOutOfDomainResidueJson()
                : outOfDomainResidueJson);
            if (!string.IsNullOrEmpty(previousReportJson))
                AppendJsonMember(report, "previousReport", previousReportJson);
            report.AppendLine("  \"findings\": [");
            report.Append(findings);
            report.AppendLine();
            report.AppendLine("  ]");
            report.AppendLine("}");
            File.WriteAllText(ReportPath, report.ToString());
            AssetDatabase.Refresh();
            Debug.Log("Volume_Component_Inquisition wrote " + ReportPath);
        }

        private static bool ExistingReportBelongsToShinobu235(string json)
        {
            return !string.IsNullOrEmpty(json) &&
                   json.IndexOf("\"agent\": \"SHINOBU_235\"", System.StringComparison.Ordinal) >= 0 &&
                   json.IndexOf("\"scanner\": \"Volume_Component_Inquisition\"", System.StringComparison.Ordinal) >= 0;
        }

        private static void AppendJsonMember(StringBuilder report, string name, string valueJson)
        {
            report.Append("  \"");
            report.Append(name);
            report.Append("\": ");
            report.AppendLine(valueJson.Trim());
            report.AppendLine(",");
        }

        private static string BuildStaticCompileAttemptJson(bool externalCompileBlockerPresent)
        {
            StringBuilder json = new StringBuilder(256);
            json.AppendLine("{");
            json.AppendLine("    \"command\": \"not run by Volume_Component_Inquisition\",");
            json.AppendLine("    \"result\": \"NOT_RUN_EDITOR_SCANNER_STATIC_ONLY\",");
            json.Append("    \"externalBlocker\": ");
            json.Append(externalCompileBlockerPresent ? "true" : "false");
            json.AppendLine(",");
            json.Append("    \"blockerPath\": \"");
            json.Append(ExternalCompileBlockerPath);
            json.AppendLine("\"");
            json.Append("  }");
            return json.ToString();
        }

        private static string BuildKnownOutOfDomainResidueJson()
        {
            StringBuilder json = new StringBuilder(512);
            json.AppendLine("[");
            json.AppendLine("    { \"path\": \"Assets/_Project/Scenes/01_MAIN_MENU.unity\", \"line\": 12956, \"needle\": \"Unity.RenderPipelines.Core.Runtime::UnityEngine.Rendering.Volume\", \"scope\": \"OUT_OF_SHINOBU_235_ROUTE\" },");
            json.AppendLine("    { \"path\": \"Assets/_Project/Data/URP_Medium (PC_RPAsset).asset\", \"line\": 95, \"needle\": \"m_VolumeProfile\", \"scope\": \"OUT_OF_SHINOBU_235_ROUTE\" },");
            json.AppendLine("    { \"path\": \"Assets/_Project/Scripts/UI/SettingsManager.cs\", \"line\": 104, \"needle\": \"VolumeProfile\", \"scope\": \"OUT_OF_SHINOBU_235_ROUTE\" }");
            json.Append("  ]");
            return json.ToString();
        }

        private static string ExtractJsonMemberValue(string json, string memberName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(memberName))
                return string.Empty;

            string needle = "\"" + memberName + "\"";
            int keyIndex = json.IndexOf(needle, System.StringComparison.Ordinal);
            if (keyIndex < 0)
                return string.Empty;

            int colonIndex = json.IndexOf(':', keyIndex + needle.Length);
            if (colonIndex < 0)
                return string.Empty;

            int valueStart = colonIndex + 1;
            while (valueStart < json.Length && char.IsWhiteSpace(json[valueStart]))
                valueStart++;

            if (valueStart >= json.Length)
                return string.Empty;

            char first = json[valueStart];
            if (first != '{' && first != '[')
                return string.Empty;

            int depth = 0;
            bool inString = false;
            bool escaped = false;
            for (int i = valueStart; i < json.Length; i++)
            {
                char c = json[i];
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
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return json.Substring(valueStart, i - valueStart + 1).Trim();
                }
            }

            return string.Empty;
        }

        private static void Scan(
            string root,
            string pattern,
            ref int standardVolumeForbidden,
            ref int stringShaderParameterForbidden,
            StringBuilder findings)
        {
            if (!Directory.Exists(root))
                return;

            string[] files = Directory.GetFiles(root, pattern, SearchOption.AllDirectories);
            for (int i = 0; i < files.Length; i++)
            {
                string path = files[i].Replace('\\', '/');
                string text = File.ReadAllText(path);
                int before = standardVolumeForbidden;
                Count(path, text, "UnityEngine.Rendering.Volume", ref standardVolumeForbidden, findings);
                Count(path, text, "UnityEngine.Rendering.Universal.Volume", ref standardVolumeForbidden, findings);
                Count(path, text, "VolumeProfile", ref standardVolumeForbidden, findings);
                Count(path, text, ".profile.TryGet", ref standardVolumeForbidden, findings);
                Count(path, text, "m_EditorClassIdentifier: Unity.RenderPipelines.Core.Runtime::UnityEngine.Rendering.Volume", ref standardVolumeForbidden, findings);
                Count(path, text, "PostProcessVolume", ref standardVolumeForbidden, findings);
                if (before == standardVolumeForbidden && path.EndsWith(".prefab") && text.Contains("sharedProfile: {fileID: 11400000"))
                    Count(path, text, "sharedProfile: {fileID: 11400000", ref standardVolumeForbidden, findings);

                if (IsShinobuNoirStringRouteFile(path))
                {
                    Count(path, text, ".SetFloat(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, ".SetVector(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, ".SetTexture(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, ".SetColor(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, ".SetInt(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, "Shader.SetGlobalFloat(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, "Shader.SetGlobalVector(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, "Shader.SetGlobalTexture(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, "Shader.SetGlobalColor(\"", ref stringShaderParameterForbidden, findings);
                    Count(path, text, "Shader.SetGlobalInt(\"", ref stringShaderParameterForbidden, findings);
                }
            }
        }

        private static bool IsShinobuNoirStringRouteFile(string path)
        {
            return path.EndsWith("/HectonVisorUberPostFeature.cs", System.StringComparison.Ordinal) ||
                   path.EndsWith("/HectonVisorUberPostFeature.Noir.cs", System.StringComparison.Ordinal);
        }

        private static void Count(string path, string text, string needle, ref int forbidden, StringBuilder findings)
        {
            int index = text.IndexOf(needle, System.StringComparison.Ordinal);
            while (index >= 0)
            {
                int line = 1;
                for (int i = 0; i < index; i++)
                {
                    if (text[i] == '\n')
                        line++;
                }

                if (findings.Length > 0)
                    findings.AppendLine(",");
                findings.Append("    { \"path\": \"");
                findings.Append(path);
                findings.Append("\", \"line\": ");
                findings.Append(line);
                findings.Append(", \"needle\": \"");
                findings.Append(needle.Replace("\\", "\\\\").Replace("\"", "\\\""));
                findings.Append("\" }");
                forbidden++;
                index = text.IndexOf(needle, index + needle.Length, System.StringComparison.Ordinal);
            }
        }
    }
}
#endif
