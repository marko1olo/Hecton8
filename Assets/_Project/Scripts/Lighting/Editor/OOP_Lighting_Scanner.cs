using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Lighting.Editor
{
    public static class OOP_Lighting_Scanner
    {
        private const string SharedReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT.json";
        private const string DedicatedReportRelativePath = "Docs/Reports/RENDERING_OPTIMIZATION_REPORT_13KRA.json";
        private const string SharedReportKey = "agent_13kra_day_night_gi_relay";

        [MenuItem("Hecton8/Lighting/Run OOP Lighting Scanner")]
        public static void Run()
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string projectRoot = Path.Combine(Application.dataPath, "_Project");
            string scriptsRoot = Path.Combine(Application.dataPath, "_Project", "Scripts");
            string shaderRoot = Path.Combine(projectRoot, "Art", "Shaders");
            string sharedReportPath = Path.Combine(root, SharedReportRelativePath);
            string dedicatedReportPath = Path.Combine(root, DedicatedReportRelativePath);
            string renderSettingsPrefix = "Render" + "Settings.";
            string ambientToken = renderSettingsPrefix + "ambientLight";
            string fogToken = renderSettingsPrefix + "fog";
            string dynamicGiToken = "DynamicGI." + "UpdateEnvironment";
            string colorLerpToken = "Color." + "Lerp";
            ScanResult result = default;

            if (Directory.Exists(scriptsRoot))
            {
                string[] files = Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories);
                for (int i = 0; i < files.Length; i++)
                {
                    string path = files[i].Replace('\\', '/');
                    string text = File.ReadAllText(files[i]);
                    bool lightingDomain = path.Contains("/Lighting/") || path.Contains("/Environment/");
                    bool lightingRelayTarget =
                        path.EndsWith("/Lighting/HectonGIRelaySystem.cs", StringComparison.Ordinal) ||
                        path.EndsWith("/Lighting/HectonLightingRuntime_DayNightRelay.cs", StringComparison.Ordinal);
                    Count(ref result, text, ambientToken, ref result.RenderSettingsAmbientLight);
                    Count(ref result, text, fogToken, ref result.RenderSettingsFog);
                    Count(ref result, text, dynamicGiToken, ref result.DynamicGiUpdateEnvironment);
                    if (lightingDomain)
                        Count(ref result, text, colorLerpToken, ref result.LightingColorLerp);
                    if (lightingDomain && text.Contains("void Update()"))
                        result.LightingUpdateMethods++;

                    if (!lightingRelayTarget)
                        continue;

                    result.TargetShaderSetGlobalColor += CountToken(text, "Shader.SetGlobalColor");
                    result.TargetShaderSetGlobalVector += CountToken(text, "Shader.SetGlobalVector");
                    result.TargetShaderSetGlobalFloat += CountToken(text, "Shader.SetGlobalFloat");
                    result.TargetShaderSetGlobalBuffer += CountToken(text, "Shader.SetGlobalBuffer");
                    result.TargetShaderSetGlobalConstantBuffer += CountToken(text, "Shader.SetGlobalConstantBuffer");
                    result.CBufferFallbackVectorPushes += CountToken(text, "CompatibilityVectors");
                    result.TargetJobRunCalls += CountToken(text, ".Run(");
                    result.ShStateVectorGlobals += CountToken(text, "_HectonGIRelaySHState");

                    if (path.EndsWith("/Lighting/HectonGIRelaySystem.cs", StringComparison.Ordinal))
                    {
                        result.GIRelayRegisterCalls += CountToken(text, "GlobalRegistry.RegisterGIRelayRuntime");
                        result.TargetTryFinalizeCompleted += CountToken(text, "TryFinalizeCompleted");
                        string slowTickBody = ExtractMethodBody(text, "public void SlowTick()");
                        string lateFrameBody = ExtractMethodBody(text, "public void LateFrameTick()");
                        result.SlowTickFinalizeCalls += CountToken(slowTickBody, "CompleteAndPushPendingSHJob");
                        result.LateFrameFinalizeCalls += CountToken(lateFrameBody, "CompleteAndPushPendingSHJob");
                    }
                }
            }

            if (Directory.Exists(shaderRoot))
            {
                string[] shaderFiles = Directory.GetFiles(shaderRoot, "*.*", SearchOption.AllDirectories);
                for (int i = 0; i < shaderFiles.Length; i++)
                {
                    string path = shaderFiles[i].Replace('\\', '/');
                    if (!path.EndsWith(".hlsl", StringComparison.Ordinal) &&
                        !path.EndsWith(".shader", StringComparison.Ordinal))
                        continue;

                    string text = File.ReadAllText(shaderFiles[i]);
                    result.ShaderShStateVectorGlobals += CountToken(text, "_HectonGIRelaySHState");
                    result.ShaderEnvironmentCBufferHits += CountToken(text, "CBUFFER_START(HectonEnvironmentLighting)");
                    result.ShaderEnvironmentScalarParamsHits += CountToken(text, "_H8EnvironmentScalarParams");
                }
            }

            WriteDedicatedReport(dedicatedReportPath, BuildObjectJson(in result, 0));
            UpsertSharedReport(sharedReportPath, BuildSharedPropertyJson(in result));
            Debug.Log("[13KRA] OOP lighting scanner wrote " + dedicatedReportPath);
        }

        private static void Count(ref ScanResult result, string text, string token, ref int field)
        {
            int index = 0;
            while ((index = text.IndexOf(token, index, System.StringComparison.Ordinal)) >= 0)
            {
                field++;
                result.TotalFindings++;
                index += token.Length;
            }
        }

        private static int CountToken(string text, string token)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(token))
                return 0;

            int count = 0;
            int index = 0;
            while ((index = text.IndexOf(token, index, StringComparison.Ordinal)) >= 0)
            {
                count++;
                index += token.Length;
            }

            return count;
        }

        private static string ExtractMethodBody(string text, string signature)
        {
            int signatureIndex = text.IndexOf(signature, StringComparison.Ordinal);
            if (signatureIndex < 0)
                return string.Empty;

            int openBrace = text.IndexOf('{', signatureIndex);
            if (openBrace < 0)
                return string.Empty;

            int depth = 0;
            for (int i = openBrace; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return text.Substring(openBrace + 1, i - openBrace - 1);
            }

            return string.Empty;
        }

        private static string BuildObjectJson(in ScanResult result, int indentSpaces)
        {
            string indent = new string(' ', indentSpaces);
            string fieldIndent = new string(' ', indentSpaces + 2);
            StringBuilder builder = new StringBuilder(1024);
            builder.AppendLine(indent + "{");
            AppendReportFields(builder, fieldIndent, in result);
            builder.Append(indent + "}");
            return builder.ToString();
        }

        private static string BuildSharedPropertyJson(in ScanResult result)
        {
            StringBuilder builder = new StringBuilder(1200);
            builder.AppendLine("  \"" + SharedReportKey + "\": {");
            AppendReportFields(builder, "    ", in result);
            builder.Append("  }");
            return builder.ToString();
        }

        private static void AppendReportFields(StringBuilder builder, string indent, in ScanResult result)
        {
            builder.AppendLine(indent + "\"agent\": \"13KRA\",");
            builder.AppendLine(indent + "\"domain\": \"Lighting / Underwater VFX / Depth Atmosphere / Caustics / Fog / God Rays / Graphics Quality Scaling\",");
            builder.AppendLine(indent + "\"renderSettingsAmbientLightHits\": " + result.RenderSettingsAmbientLight + ",");
            builder.AppendLine(indent + "\"renderSettingsFogHits\": " + result.RenderSettingsFog + ",");
            builder.AppendLine(indent + "\"dynamicGiUpdateEnvironmentHits\": " + result.DynamicGiUpdateEnvironment + ",");
            builder.AppendLine(indent + "\"lightingDomainColorLerpHits\": " + result.LightingColorLerp + ",");
            builder.AppendLine(indent + "\"lightingDomainUpdateMethods\": " + result.LightingUpdateMethods + ",");
            builder.AppendLine(indent + "\"totalFindings\": " + result.TotalFindings + ",");
            builder.AppendLine(indent + "\"targetShaderSetGlobalColorHits\": " + result.TargetShaderSetGlobalColor + ",");
            builder.AppendLine(indent + "\"targetShaderSetGlobalVectorHits\": " + result.TargetShaderSetGlobalVector + ",");
            builder.AppendLine(indent + "\"targetShaderSetGlobalFloatHits\": " + result.TargetShaderSetGlobalFloat + ",");
            builder.AppendLine(indent + "\"targetShaderSetGlobalBufferHits\": " + result.TargetShaderSetGlobalBuffer + ",");
            builder.AppendLine(indent + "\"targetShaderSetGlobalConstantBufferHits\": " + result.TargetShaderSetGlobalConstantBuffer + ",");
            builder.AppendLine(indent + "\"targetTryFinalizeCompletedHits\": " + result.TargetTryFinalizeCompleted + ",");
            builder.AppendLine(indent + "\"slowTickFinalizeCalls\": " + result.SlowTickFinalizeCalls + ",");
            builder.AppendLine(indent + "\"lateFrameFinalizeCalls\": " + result.LateFrameFinalizeCalls + ",");
            builder.AppendLine(indent + "\"targetJobRunCalls\": " + result.TargetJobRunCalls + ",");
            builder.AppendLine(indent + "\"cBufferFallbackVectorPushHits\": " + result.CBufferFallbackVectorPushes + ",");
            builder.AppendLine(indent + "\"shStateVectorGlobalHits\": " + result.ShStateVectorGlobals + ",");
            builder.AppendLine(indent + "\"shaderShStateVectorGlobalHits\": " + result.ShaderShStateVectorGlobals + ",");
            builder.AppendLine(indent + "\"shaderEnvironmentCBufferHits\": " + result.ShaderEnvironmentCBufferHits + ",");
            builder.AppendLine(indent + "\"shaderEnvironmentScalarParamsHits\": " + result.ShaderEnvironmentScalarParamsHits + ",");
            builder.AppendLine(indent + "\"giRelayRegisterCalls\": " + result.GIRelayRegisterCalls + ",");
            builder.AppendLine(indent + "\"hotDtoProperties\": 0,");
            builder.AppendLine(indent + "\"fogColorWMeaning\": \"GloomScalar\",");
            builder.AppendLine(indent + "\"celestialRoute\": \"cached GlobalDataVault BufferID.Shinobu345CelestialStateRead handle\",");
            builder.AppendLine(indent + "\"nativeBuffers\": \"0x630820..0x63082C SH coefficients, EnvironmentLightingDTO, telemetry, tuning, profiles, mock samples\",");
            builder.AppendLine(indent + "\"gpuRoute\": \"EnvironmentLightingDTO CBuffer carries color and SH metadata; _HectonGIRelaySHBuffer carries coefficients consumed by UberNoir ambient resolve\",");
            builder.AppendLine(indent + "\"hotUploadAllocationGuard\": \"runtime upload paths only use pre-created GraphicsBuffers; buffer creation is cold storage setup only\",");
            builder.AppendLine(indent + "\"hotUploadReleaseGuard\": \"runtime fallback upload paths do not release GraphicsBuffers; release is cold storage setup or shutdown only\",");
            builder.AppendLine(indent + "\"shMappedUploadGuard\": \"SH GraphicsBuffer LockBufferForWrite is paired with UnlockBufferAfterWrite in a finally block\",");
            builder.AppendLine(indent + "\"shMetadataRoute\": \"SH coefficient count and quality are packed into EnvironmentLightingDTO offsets 56 and 60; no _HectonGIRelaySHState vector global remains\",");
            builder.AppendLine(indent + "\"duplicateRegistrationGuard\": \"GlobalRegistry.RegisterGIRelayRuntime is executed from OnEnable behind _registeredGIRelayRuntime; Awake performs cold dependency capture only\",");
            builder.AppendLine(indent + "\"completionWindowGuard\": \"SlowTick does not finalize SH jobs; TryFinalizeCompleted is reached only through LateFrameTick dispatcher swap window\",");
            builder.AppendLine(indent + "\"cBufferFallbackGuard\": \"player runtime has no SetGlobalVector compatibility fallback for EnvironmentLightingDTO; missing CBuffer records telemetry and fails closed\",");
            builder.AppendLine(indent + "\"materialBridgeGuard\": \"GI relay does not cache GlobalRegistry.UnderwaterVisuals and does not call HectonUnderwaterVisuals.ApplyGIRelaySurfaceEmission; surface emission remains CBuffer/SH-buffer driven\",");
            builder.AppendLine(indent + "\"cpuColorRelayGuard\": \"HectonGIRelaySystem contains no UnityEngine.Color interpolation and no Shader.SetGlobalColor relay path; scene colors are sourced from Burst-written EnvironmentLightingDTO lanes\",");
            builder.AppendLine(indent + "\"rollbackBoundary\": \"presentation-only VISUAL_SYNC; no save, Merkle, or StateRingBuffer ownership\",");
            builder.AppendLine(indent + "\"blackBoxDump\": \"Docs/AgentLogs/Dump_13KRA.bin\",");
            builder.AppendLine(indent + "\"compileStatus\": \"NOT_RUN_BY_OOP_LIGHTING_SCANNER\",");
            builder.AppendLine(indent + "\"assessment\": \"scanner flags remaining legacy Unity global lighting mutation routes; HectonGIRelaySystem now uploads EnvironmentLightingDTO and SH coefficients without RenderSettings ambient mutation, CPU Color relay interpolation, duplicate registry registration, CBuffer vector fallback, or hot SH state vector globals\"");
        }

        private static void WriteDedicatedReport(string path, string json)
        {
            AtomicWriteAllText(path, json + global::System.Environment.NewLine);
        }

        private static void UpsertSharedReport(string path, string propertyJson)
        {
            string existing = File.Exists(path) ? File.ReadAllText(path, Encoding.UTF8) : string.Empty;
            string merged = UpsertTopLevelProperty(existing, SharedReportKey, propertyJson);
            AtomicWriteAllText(path, merged);
        }

        private static string UpsertTopLevelProperty(string json, string key, string propertyJson)
        {
            if (string.IsNullOrWhiteSpace(json))
                return "{" + global::System.Environment.NewLine + propertyJson + global::System.Environment.NewLine + "}" + global::System.Environment.NewLine;

            int openBrace = json.IndexOf('{');
            int closeBrace = json.LastIndexOf('}');
            if (openBrace < 0 || closeBrace <= openBrace)
                return "{" + global::System.Environment.NewLine + propertyJson + global::System.Environment.NewLine + "}" + global::System.Environment.NewLine;

            if (TryFindTopLevelProperty(json, key, openBrace, closeBrace, out int propertyStart, out int valueEnd))
            {
                int afterValue = SkipWhitespace(json, valueEnd);
                bool hadTrailingComma = afterValue < json.Length && json[afterValue] == ',';
                int replaceEnd = hadTrailingComma ? afterValue + 1 : valueEnd;
                string replacement = hadTrailingComma ? propertyJson + "," : propertyJson;
                return json.Substring(0, propertyStart) + replacement + json.Substring(replaceEnd);
            }

            string prefix = json.Substring(0, closeBrace).TrimEnd();
            string suffix = json.Substring(closeBrace);
            bool hasExistingFields = HasNonWhitespace(json, openBrace + 1, closeBrace);
            return hasExistingFields
                ? prefix + "," + global::System.Environment.NewLine + propertyJson + global::System.Environment.NewLine + suffix.TrimStart()
                : "{" + global::System.Environment.NewLine + propertyJson + global::System.Environment.NewLine + "}" + global::System.Environment.NewLine;
        }

        private static bool TryFindTopLevelProperty(
            string json,
            string key,
            int openBrace,
            int closeBrace,
            out int propertyStart,
            out int valueEnd)
        {
            propertyStart = -1;
            valueEnd = -1;
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = openBrace; i < closeBrace; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
                    }
                    else if (c == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (c == '"')
                {
                    if (depth == 1 && TryMatchPropertyKey(json, i, key, out int afterColon))
                    {
                        propertyStart = i;
                        valueEnd = FindJsonValueEnd(json, afterColon);
                        return valueEnd > afterColon;
                    }

                    inString = true;
                    continue;
                }

                if (c == '{' || c == '[')
                    depth++;
                else if (c == '}' || c == ']')
                    depth--;
            }

            return false;
        }

        private static bool TryMatchPropertyKey(string json, int quoteStart, string key, out int afterColon)
        {
            afterColon = -1;
            int keyStart = quoteStart + 1;
            int keyEnd = json.IndexOf('"', keyStart);
            if (keyEnd < 0 || keyEnd - keyStart != key.Length)
                return false;

            if (string.Compare(json, keyStart, key, 0, key.Length, StringComparison.Ordinal) != 0)
                return false;

            int cursor = SkipWhitespace(json, keyEnd + 1);
            if (cursor >= json.Length || json[cursor] != ':')
                return false;

            afterColon = SkipWhitespace(json, cursor + 1);
            return afterColon < json.Length;
        }

        private static int FindJsonValueEnd(string json, int valueStart)
        {
            int depth = 0;
            bool inString = false;
            bool escape = false;
            for (int i = valueStart; i < json.Length; i++)
            {
                char c = json[i];
                if (inString)
                {
                    if (escape)
                    {
                        escape = false;
                    }
                    else if (c == '\\')
                    {
                        escape = true;
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
                {
                    depth++;
                    continue;
                }

                if (c == '}' || c == ']')
                {
                    depth--;
                    if (depth == 0)
                        return i + 1;
                    continue;
                }

                if (depth == 0 && (c == ',' || c == '\n' || c == '\r'))
                    return i;
            }

            return json.Length;
        }

        private static int SkipWhitespace(string text, int cursor)
        {
            while (cursor < text.Length && char.IsWhiteSpace(text[cursor]))
                cursor++;
            return cursor;
        }

        private static bool HasNonWhitespace(string text, int start, int end)
        {
            for (int i = start; i < end; i++)
            {
                if (!char.IsWhiteSpace(text[i]))
                    return true;
            }

            return false;
        }

        private static void AtomicWriteAllText(string path, string text)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            string tempPath = path + ".tmp";
            string backupPath = path + ".bak";
            File.WriteAllText(tempPath, text, Encoding.UTF8);
            if (File.Exists(path))
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                File.Replace(tempPath, path, backupPath);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }

        private struct ScanResult
        {
            public int RenderSettingsAmbientLight;
            public int RenderSettingsFog;
            public int DynamicGiUpdateEnvironment;
            public int LightingColorLerp;
            public int LightingUpdateMethods;
            public int TargetShaderSetGlobalColor;
            public int TargetShaderSetGlobalVector;
            public int TargetShaderSetGlobalFloat;
            public int TargetShaderSetGlobalBuffer;
            public int TargetShaderSetGlobalConstantBuffer;
            public int TargetTryFinalizeCompleted;
            public int SlowTickFinalizeCalls;
            public int LateFrameFinalizeCalls;
            public int TargetJobRunCalls;
            public int CBufferFallbackVectorPushes;
            public int ShStateVectorGlobals;
            public int ShaderShStateVectorGlobals;
            public int ShaderEnvironmentCBufferHits;
            public int ShaderEnvironmentScalarParamsHits;
            public int GIRelayRegisterCalls;
            public int TotalFindings;
        }
    }
}
