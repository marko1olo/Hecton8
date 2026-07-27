#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only forensic source smoke test for audio-domain autonomy hardening.
    /// </summary>
    public static class AudioOmegaAutonomySmokeTester
    {
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string MetallicGrainBankPath = "Assets/_Project/Scripts/Audio/PlayerCriticalMetallicGrainBank.cs";
        private const string SpatialAudioPath = "Assets/_Project/Scripts/SpatialAudioManager.cs";
        private const string OcclusionPath = "Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs";
        private const string TelemetryPath = "Assets/_Project/Scripts/CrashTelemetryBuffer.cs";
        private const string MusicDirectorPath = "Assets/_Project/Scripts/Audio/HectonMusicDirector.cs";
        private const string GlobalRegistryPath = "Assets/_Project/Scripts/Core/GlobalRegistry.cs";
        private const string GlobalRegistryContractsPath = "Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs";

        [MenuItem("Hecton8/Audio/Run Omega Autonomy Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string jsonReport);
            if (passed)
                Hecton8.Core.H8Debug.Log(jsonReport);
            else
                Hecton8.Core.H8Debug.LogError(jsonReport);
        }

        public static bool Run(out string jsonReport)
        {
            int passedCount = 0;
            int failedCount = 0;
            StringBuilder checks = new StringBuilder(8192);

            string renderer = ReadAssetText(RendererPath, ref checks, ref failedCount);
            string metallicGrainBank = ReadAssetText(MetallicGrainBankPath, ref checks, ref failedCount);
            string spatialAudio = ReadAssetText(SpatialAudioPath, ref checks, ref failedCount);
            string occlusion = ReadAssetText(OcclusionPath, ref checks, ref failedCount);
            string telemetry = ReadAssetText(TelemetryPath, ref checks, ref failedCount);
            string musicDirector = ReadAssetText(MusicDirectorPath, ref checks, ref failedCount);
            string registry = ReadAssetText(GlobalRegistryPath, ref checks, ref failedCount);
            string registryContracts = ReadAssetText(GlobalRegistryContractsPath, ref checks, ref failedCount);

            AppendCheck("renderer authority moved to GlobalRegistry", renderer.IndexOf("s_activeInstance", StringComparison.Ordinal) < 0 && renderer.Contains("GlobalRegistry.PlayerCriticalAudio"), ref passedCount, ref failedCount, checks);
            AppendCheck("crash telemetry authority moved to GlobalRegistry", telemetry.IndexOf("_instance", StringComparison.Ordinal) < 0 && telemetry.Contains("GlobalRegistry.CrashTelemetry"), ref passedCount, ref failedCount, checks);
            AppendCheck("music director authority moved to GlobalRegistry", musicDirector.IndexOf("_instance", StringComparison.Ordinal) < 0 && musicDirector.Contains("GlobalRegistry.MusicDirector"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer runtime registration is outside Awake", !ExtractMethodBody(renderer, "private void Awake()").Contains("RegisterPlayerCriticalAudioRuntime"), ref passedCount, ref failedCount, checks);
            AppendCheck("crash telemetry runtime registration is outside Awake", !ExtractMethodBody(telemetry, "private void Awake()").Contains("RegisterCrashTelemetryRuntime"), ref passedCount, ref failedCount, checks);
            AppendCheck("music director runtime registration is outside Awake", !ExtractMethodBody(musicDirector, "private void Awake()").Contains("RegisterMusicDirectorRuntime"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer duplicate owner aborts OnEnable", ExtractMethodBody(renderer, "private void OnEnable()").Contains("if (!TryRegisterRuntimeService())") && renderer.Contains("private bool TryRegisterRuntimeService()"), ref passedCount, ref failedCount, checks);
            AppendCheck("crash telemetry duplicate owner aborts OnEnable", ExtractMethodBody(telemetry, "private void OnEnable()").Contains("if (!TryRegisterRuntimeService())") && telemetry.Contains("private bool TryRegisterRuntimeService()"), ref passedCount, ref failedCount, checks);
            AppendCheck("music director duplicate owner aborts lifecycle", ExtractMethodBody(musicDirector, "private void OnEnable()").Contains("if (!TryRegisterToGlobalRegistry())") && ExtractMethodBody(musicDirector, "private void Start()").Contains("if (!TryRegisterToGlobalRegistry())") && musicDirector.Contains("private bool TryRegisterToGlobalRegistry()"), ref passedCount, ref failedCount, checks);
            AppendCheck("crash telemetry owns no DDOL call", telemetry.IndexOf("DontDestroyOnLoad", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("GlobalRegistry service slots are unique", EnumValuesAreUnique(registryContracts, "GlobalRegistryServiceSlot"), ref passedCount, ref failedCount, checks);
            AppendCheck("GlobalRegistry exposes crash telemetry runtime", registry.Contains("CrashTelemetry => _crashTelemetryRuntime") && registryContracts.Contains("CrashTelemetryRuntime"), ref passedCount, ref failedCount, checks);
            AppendCheck("GlobalRegistry exposes player critical audio runtime", registry.Contains("PlayerCriticalAudio => _playerCriticalAudioRuntime") && registryContracts.Contains("PlayerCriticalAudioRuntime"), ref passedCount, ref failedCount, checks);
            AppendCheck("GlobalRegistry resolves audio runtime slots", ContainsAll(registry, "typeof(CrashTelemetryBuffer)) return GlobalRegistryServiceSlot.CrashTelemetryRuntime", "typeof(PlayerCriticalProceduralAudioRenderer)) return GlobalRegistryServiceSlot.PlayerCriticalAudioRuntime", "typeof(HectonMusicDirector)) return GlobalRegistryServiceSlot.MusicDirectorRuntime"), ref passedCount, ref failedCount, checks);
            AppendCheck("GlobalRegistry reset clears audio runtime slots", ContainsAll(registry, "_crashTelemetryRuntime = null", "_playerCriticalAudioRuntime = null", "_musicDirectorRuntime = null"), ref passedCount, ref failedCount, checks);
            AppendCheck("acoustic occlusion NativeArrays registered or absent", !occlusion.Contains("NativeArray") || ContainsAll(occlusion, "RegisterNative" + "Array", "_queryResults", "_enclosureResults", "_forwardEchoCommands", "_forwardEchoResults"), ref passedCount, ref failedCount, checks);
            AppendCheck("acoustic occlusion NativeLists registered or absent", !occlusion.Contains("NativeList") || ContainsAll(occlusion, "RegisterNativeList", "_queryCommands", "_enclosureCommands"), ref passedCount, ref failedCount, checks);
            AppendCheck("spatial audio radar NativeCollections registered", ContainsAll(spatialAudio, "_acousticRadarIntensityBins", "_acousticRadarGrid", "_pendingDelayedAudioEvents", "BufferID.SpatialAudioRadarIntensityBins", "BufferID.SpatialAudioRadarGrid", "EnsureVaultBackedArray", "RegisterNativeList"), ref passedCount, ref failedCount, checks);
            AppendCheck("grain bank cold bake is deterministic and job-free", metallicGrainBank.Contains("GenerateGranularStressEmission") && metallicGrainBank.Contains("LFO_TriangleOscillator") && metallicGrainBank.Contains("SoftClipSaturation") && metallicGrainBank.IndexOf("Complete", StringComparison.Ordinal) < 0 && metallicGrainBank.IndexOf("IJobParallelFor", StringComparison.Ordinal) < 0 && renderer.Contains("PlayerCriticalMetallicGrainBank.Generate"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer no longer owns metallic grain-bank job", renderer.IndexOf("MetallicGrainBankBuildJob", StringComparison.Ordinal) < 0 && renderer.IndexOf("private static void GenerateMetallicGrainBank", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("audio overflow emits performance telemetry on main thread", telemetry.Contains("GlobalTelemetryBus.PublishPerformanceWarning") && telemetry.Contains("_audioOverflowDropWarningHash"), ref passedCount, ref failedCount, checks);
            AppendCheck("managed audio callback fallback is absent", renderer.IndexOf("OnAudioFilterRead", StringComparison.Ordinal) < 0 && renderer.IndexOf("MixInterleavedInto", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("hot DSP block has no JobHandle completion", !ExtractMethodBody(renderer, "private void MixAndFilterBlock").Contains(".Complete()"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer Tick has no string formatting", HasNoHotStringFormatting(ExtractMethodBody(renderer, "public void Tick(float deltaTime)")), ref passedCount, ref failedCount, checks);
            AppendCheck("crash telemetry Tick has no string formatting", HasNoHotStringFormatting(ExtractMethodBody(telemetry, "public void Tick(float dt)")), ref passedCount, ref failedCount, checks);
            AppendCheck("music director Tick has no string formatting", HasNoHotStringFormatting(ExtractMethodBody(musicDirector, "public void Tick(float deltaTime)")), ref passedCount, ref failedCount, checks);
            AppendCheck("spatial audio Tick has no string formatting", HasNoHotStringFormatting(ExtractMethodBody(spatialAudio, "public void Tick(float deltaTime)")), ref passedCount, ref failedCount, checks);

            bool passed = failedCount == 0;
            StringBuilder json = new StringBuilder(12288);
            json.Append("{\n");
            json.Append("  \"tester\": \"AudioOmegaAutonomySmokeTester\",\n");
            json.Append("  \"status\": \"").Append(passed ? "PASS" : "FAIL").Append("\",\n");
            json.Append("  \"passed\": ").Append(passedCount).Append(",\n");
            json.Append("  \"failed\": ").Append(failedCount).Append(",\n");
            json.Append("  \"checks\": [\n");
            json.Append(checks);
            json.Append("\n  ]\n");
            json.Append('}');
            jsonReport = json.ToString();
            return passed;
        }

        private static string ReadAssetText(string assetPath, ref StringBuilder checks, ref int failedCount)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string absolutePath = root == null
                ? assetPath
                : Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                AppendRawCheck(assetPath, false, ref checks);
                failedCount++;
                return string.Empty;
            }

            return File.ReadAllText(absolutePath);
        }

        private static bool ContainsAll(string source, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (source.IndexOf(needles[i], StringComparison.Ordinal) < 0)
                    return false;
            }

            return true;
        }

        private static bool EnumValuesAreUnique(string source, string enumName)
        {
            Match enumMatch = Regex.Match(
                source,
                "enum\\s+" + Regex.Escape(enumName) + "\\s*:\\s*byte\\s*\\{(?<body>.*?)\\n\\s*\\}",
                RegexOptions.Singleline);
            if (!enumMatch.Success)
                return false;

            MatchCollection matches = Regex.Matches(
                enumMatch.Groups["body"].Value,
                "(?<name>[A-Za-z_][A-Za-z0-9_]*)\\s*=\\s*(?<value>\\d+)");
            HashSet<int> values = new HashSet<int>(16);
            for (int i = 0; i < matches.Count; i++)
            {
                int value = int.Parse(matches[i].Groups["value"].Value);
                if (!values.Add(value))
                    return false;
            }

            return matches.Count > 0;
        }

        private static bool HasNoHotStringFormatting(string body)
        {
            return body.IndexOf(".ToString(", StringComparison.Ordinal) < 0 &&
                   body.IndexOf("string" + ".Format", StringComparison.Ordinal) < 0 &&
                   body.IndexOf("$\"", StringComparison.Ordinal) < 0;
        }

        private static string ExtractMethodBody(string source, string signaturePrefix)
        {
            int signatureIndex = source.IndexOf(signaturePrefix, StringComparison.Ordinal);
            if (signatureIndex < 0)
                return string.Empty;

            int braceStart = source.IndexOf('{', signatureIndex);
            if (braceStart < 0)
                return string.Empty;

            int depth = 0;
            for (int i = braceStart; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                    depth--;

                if (depth == 0)
                    return source.Substring(braceStart, i - braceStart + 1);
            }

            return string.Empty;
        }

        private static void AppendCheck(string name, bool passed, ref int passedCount, ref int failedCount, StringBuilder checks)
        {
            if (passed)
                passedCount++;
            else
                failedCount++;

            AppendRawCheck(name, passed, ref checks);
        }

        private static void AppendRawCheck(string name, bool passed, ref StringBuilder checks)
        {
            if (checks.Length > 0)
                checks.Append(",\n");

            checks.Append("    { \"name\": \"");
            AppendJsonEscaped(checks, name);
            checks.Append("\", \"passed\": ");
            checks.Append(passed ? "true" : "false");
            checks.Append(" }");
        }

        private static void AppendJsonEscaped(StringBuilder builder, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c == '"' || c == '\\')
                    builder.Append('\\');
                builder.Append(c);
            }
        }
    }
}
#endif
