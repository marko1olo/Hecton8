#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only source smoke test for SHINOBU_351 hull-stress granular DSP invariants.
    /// </summary>
    public static class Shinobu351HullStressDspSmokeTester
    {
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string KernelPath = "Assets/_Project/Scripts/Audio/Synthesis/HullStressGranularDspKernel.cs";
        private const string AcousticZonePath = "Assets/_Project/Scripts/AcousticZoneController.cs";
        private const string SubmarineOsPath = "Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs";
        private const string RingBufferPath = "Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs";
        private const string SynthesisAsmdefPath = "Assets/_Project/Scripts/Audio/Synthesis/Hecton8.Audio.Synthesis.asmdef";
        private const string OopScannerPath = "Assets/_Project/Scripts/Audio/Editor/OOP_AudioSource_Scanner.cs";
        private const string PipelineDocPath = "Docs/ARCHITECTURE/AUDIO_DSP_PIPELINE.md";
        private const string LedgerPath = "Docs/ARCHITECTURE/BINARY_PAYLOAD_INTEGRATION_LEDGER.md";
        private const string ReportPath = "Docs/Reports/AUDIO_SHINOBU_351_STATIC_SMOKE.json";

        [MenuItem("Hecton8/Audio/Run SHINOBU 351 Hull Stress DSP Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string jsonReport);
            WriteReport(jsonReport);
            if (passed)
                Hecton8.Core.H8Debug.Log(jsonReport);
            else
                Hecton8.Core.H8Debug.LogError(jsonReport);
        }

        /// <summary>
        /// Runs source-level assertions for the SHINOBU_351 SignalBus, Vault, SPSC, and Burst-pointer route.
        /// </summary>
        /// <param name="jsonReport">JSON report body suitable for Docs/Reports persistence.</param>
        /// <returns>True when every static invariant passes.</returns>
        public static bool Run(out string jsonReport)
        {
            int passedCount = 0;
            int failedCount = 0;
            StringBuilder checks = new StringBuilder(12288);

            string renderer = ReadAssetText(RendererPath, ref checks, ref failedCount);
            string kernel = ReadAssetText(KernelPath, ref checks, ref failedCount);
            string acousticZone = ReadAssetText(AcousticZonePath, ref checks, ref failedCount);
            string submarineOs = ReadAssetText(SubmarineOsPath, ref checks, ref failedCount);
            string ringBuffer = ReadAssetText(RingBufferPath, ref checks, ref failedCount);
            string synthesisAsmdef = ReadAssetText(SynthesisAsmdefPath, ref checks, ref failedCount);
            string oopScanner = ReadAssetText(OopScannerPath, ref checks, ref failedCount);
            string pipelineDoc = ReadAssetText(PipelineDocPath, ref checks, ref failedCount);
            string ledger = ReadAssetText(LedgerPath, ref checks, ref failedCount);

            AppendCheck("renderer consumes BaseStructuralWarningSignal SignalBus snapshot", renderer.Contains("SignalBus<BaseStructuralWarningSignal>.GetFrameSnapshot()"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer maps structural warning signal into owner audio state", renderer.Contains("HandleBaseStructuralWarningSignal"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer writes final block through SPSC native audio ring", renderer.Contains("_sampleRingBuffer.TryWriteInterleaved"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer registers native sensory kernel bridge", renderer.Contains("HectonSensoryKernelNativeBridge.TryRegister"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer keeps managed OnAudioFilterRead synthesis absent", renderer.IndexOf("OnAudioFilterRead", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("renderer has no AudioSource type usage", renderer.IndexOf("AudioSource", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("renderer has no AudioClip type usage", renderer.IndexOf("AudioClip", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("renderer has no managed PlayAtPoint fallback", renderer.IndexOf("PlayAtPoint(", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("renderer has no managed metal-stress AudioClip source", renderer.IndexOf("metalStressGrainClip", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("renderer has no AudioClip.GetData metal-stress importer", renderer.IndexOf("TryLoadMetalStressGrainClip", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("renderer has no residual hull groan loop hook", renderer.IndexOf("UpdateHullGroanLoop", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("renderer builds deterministic procedural metal PCM bank", renderer.Contains("PlayerCriticalMetallicGrainBank.Generate"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer structural voice mixer is granular", renderer.Contains("RenderStructuralGranularVoices("), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer requests granular Vault memory as uninitialized", renderer.Contains("NativeArrayOptions.UninitializedMemory"), ref passedCount, ref failedCount, checks);
            AppendCheck("renderer exposes SHINOBU_351 forensic dump path", renderer.Contains("Dump_SHINOBU_351.bin"), ref passedCount, ref failedCount, checks);

            AppendCheck("kernel declares 64-byte explicit GranularVoiceDTO", ContainsAll(kernel, "public struct GranularVoiceDTO", "[StructLayout(LayoutKind.Explicit, Size = 64)]"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel keeps double3 AUP at offset zero", kernel.Contains("[FieldOffset(0)] public double3 EpicenterAUP;"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel keeps AudioBankHashID at offset 24", kernel.Contains("[FieldOffset(24)] public uint AudioBankHashID;"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel keeps playhead/grain/pitch/amplitude at offsets 28/32/36/40", ContainsAll(kernel, "[FieldOffset(28)] public float PlayheadPosition;", "[FieldOffset(32)] public float GrainLength;", "[FieldOffset(36)] public float PitchMultiplier;", "[FieldOffset(40)] public float Amplitude;"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel pads GranularVoiceDTO through offset 60", kernel.Contains("[FieldOffset(60)] private uint _pad4;"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel exposes callback-compatible unmanaged delegate", kernel.Contains("EvaluateHullStressGranularAudioDelegate"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel compiles Burst function pointer", kernel.Contains("BurstCompiler.CompileFunctionPointer<EvaluateHullStressGranularAudioDelegate>"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel callback has MonoPInvokeCallback bridge", kernel.Contains("[MonoPInvokeCallback(typeof(EvaluateHullStressGranularAudioDelegate))]"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel bounds raw callback writes by output sample capacity", ContainsAll(kernel, "HullStressAudioBlockParamsDTO", "[StructLayout(LayoutKind.Explicit, Size = 96)]", "OutputSampleCapacity", "TelemetryFlagOutputCapacityInvalid", "outputSampleCapacity / outputStride"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel uses double AUP subtraction before float audio localization", kernel.Contains("AupPrecisionMath.LocalDeltaFloat3Clamped"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel scales polyphony continuously by quality", kernel.Contains("ResolvePolyphonyLimit") && kernel.Contains("math.lerp(8f, 64f"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel blends nearest-to-linear sampling by smooth quality curve", kernel.Contains("math.smoothstep(0.18f, 0.72f, quality)"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel uses stereo bit shift for common interleaved layout", kernel.Contains("frame << 1"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel deterministically writes channels above stereo", ContainsAll(kernel, "if (outputStride > 2)", "channelIndex < outputStride", "channelSampleIndex = outputIndex + channelIndex"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel guards pitch multiplier against nonfinite input", kernel.Contains("FiniteOrDefault(voice.PitchMultiplier, 1f)"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel rejects nonfinite voice amplitude before mixing", kernel.Contains("!math.isfinite(voice.Amplitude)") && kernel.Contains("FiniteOrZero(voice.Amplitude)"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel declares NoAlias on Burst memory lanes", kernel.Contains("[NoAlias]"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel contains no Unity AudioSource/AudioClip path", !ContainsAny(kernel, "AudioSource", "AudioClip", "PlayClipAtPoint", "PlayOneShot", "Resources.Load"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel contains no UnityEngine.Random path", kernel.IndexOf("UnityEngine.Random", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);
            AppendCheck("kernel contains no compact runtime packing", !ContainsAny(kernel, "Pack=1", "Pack = 1"), ref passedCount, ref failedCount, checks);
            AppendCheck("kernel hot DTOs expose no get/set properties", kernel.IndexOf("{ get; set; }", StringComparison.Ordinal) < 0 && kernel.IndexOf("{ get; private set; }", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);

            AppendCheck("acoustic zone fatal pressure routes structural stress procedurally", ContainsAll(acousticZone, "UpdateFatalPressureStressAudio", "ProceduralAudioEvents.TryRaiseStructuralStressTriggered", "fatalPressureStressIntervalMax") && !ContainsAny(acousticZone, "fatalPressureNoisePrimary", "fatalPressureNoiseSecondary", "fatalPressureNoiseVolume"), ref passedCount, ref failedCount, checks);
            AppendCheck("submarine OS hull warnings use VWS signals instead of managed hull clips", ContainsAll(submarineOs, "VocalWarningSignal", "VocalWarningHashes.FromWarningId", "hullBreachWarningEventId", "hullStressWarningEventId") && !ContainsAny(submarineOs, "hullBreachWarningClip", "hullStressWarningClip"), ref passedCount, ref failedCount, checks);

            AppendCheck("ring buffer exposes TryWriteInterleaved producer path", ringBuffer.Contains("TryWriteInterleaved"), ref passedCount, ref failedCount, checks);
            AppendCheck("ring buffer uses volatile SPSC index discipline", ringBuffer.Contains("Volatile.Read") && ringBuffer.Contains("Volatile.Write"), ref passedCount, ref failedCount, checks);
            AppendCheck("ring buffer keeps managed float[] consumer absent", ringBuffer.IndexOf("MixInterleavedInto(float[]", StringComparison.Ordinal) < 0, ref passedCount, ref failedCount, checks);

            AppendCheck("synthesis asmdef has no sibling audio runtime reference", !ContainsAny(synthesisAsmdef, "Hecton8.Audio.Propagation", "Hecton8.Audio.Virtualization", "Hecton8.Audio.Echolocation", "Hecton8.Audio.Prologue"), ref passedCount, ref failedCount, checks);
            AppendCheck("SHINOBU kernel source uses contracts only for cross-domain input", kernel.Contains("using Hecton8.Core.Contracts;") && !ContainsAny(kernel, "using Hecton8.Core;", "using Hecton8.Core.Memory;"), ref passedCount, ref failedCount, checks);
            AppendCheck("OOP scanner catches serialized AudioSource fields and PlayAtPoint", ContainsAll(oopScanner, "AudioSource variable", "AudioClip variable", "memberName == \"PlayAtPoint\""), ref passedCount, ref failedCount, checks);
            AppendCheck("audio pipeline doc defines OnAudioFilterRead as transfer bridge only", pipelineDoc.Contains("OnAudioFilterRead(float[] data, int channels)") && pipelineDoc.Contains("remains a transfer bridge only"), ref passedCount, ref failedCount, checks);
            AppendCheck("binary payload ledger records SHINOBU_351 route card", ledger.Contains("SHINOBU_351") && ledger.Contains("GranularVoiceDTO=64") && ledger.Contains("HullStressAudioBlockParamsDTO=96") && ledger.Contains("OutputSampleCapacity") && ledger.Contains("SignalBus<BaseStructuralWarningSignal>"), ref passedCount, ref failedCount, checks);

            bool passed = failedCount == 0;
            StringBuilder json = new StringBuilder(16384);
            json.Append("{\n");
            json.Append("  \"agent\": \"SHINOBU_351\",\n");
            json.Append("  \"tester\": \"Shinobu351HullStressDspSmokeTester\",\n");
            json.Append("  \"status\": \"").Append(passed ? "PASS" : "FAIL").Append("\",\n");
            json.Append("  \"evidence\": \"STATIC_SOURCE_UNITY_MENU_PENDING\",\n");
            json.Append("  \"passed\": ").Append(passedCount).Append(",\n");
            json.Append("  \"failed\": ").Append(failedCount).Append(",\n");
            json.Append("  \"checks\": [\n");
            json.Append(checks);
            json.Append("\n  ]\n");
            json.Append("}\n");
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
                AppendRawCheck("missing asset " + assetPath, false, ref checks);
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

        private static bool ContainsAny(string source, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
            {
                if (source.IndexOf(needles[i], StringComparison.Ordinal) >= 0)
                    return true;
            }

            return false;
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

        private static void WriteReport(string jsonReport)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string absolutePath = root == null
                ? ReportPath
                : Path.Combine(root, ReportPath.Replace('/', Path.DirectorySeparatorChar));
            string directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(absolutePath, jsonReport);
        }
    }
}
#endif
