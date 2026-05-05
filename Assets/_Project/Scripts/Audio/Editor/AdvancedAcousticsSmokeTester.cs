#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only source smoke test for advanced acoustic propagation and DSP producer features.
    /// </summary>
    public static class AdvancedAcousticsSmokeTester
    {
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string SpatialAudioPath = "Assets/_Project/Scripts/SpatialAudioManager.cs";
        private const string OcclusionPath = "Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs";
        private const string RingBufferPath = "Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs";
        private const string TelemetryPath = "Assets/_Project/Scripts/CrashTelemetryBuffer.cs";
        private const string EventsPath = "Assets/_Project/Scripts/Audio/ProceduralAudioEvents.cs";

        [MenuItem("Hecton8/Audio/Run Advanced Acoustics Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string report);
            if (passed)
                Debug.Log(report);
            else
                Debug.LogError(report);
        }

        public static bool Run(out string report)
        {
            int failureCount = 0;
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("[AdvancedAcousticsSmokeTester]");

            string renderer = ReadAssetText(RendererPath, builder, ref failureCount);
            string spatial = ReadAssetText(SpatialAudioPath, builder, ref failureCount);
            string occlusion = ReadAssetText(OcclusionPath, builder, ref failureCount);
            string ringBuffer = ReadAssetText(RingBufferPath, builder, ref failureCount);
            string telemetry = ReadAssetText(TelemetryPath, builder, ref failureCount);
            string eventsSource = ReadAssetText(EventsPath, builder, ref failureCount);

            if (spatial.Length > 0)
            {
                AssertContains(spatial, "ResolveThermalSoundSpeedMetersPerSecond", "Thermal sound speed resolver exists", builder, ref failureCount);
                AssertContains(spatial, "1440f + (4.6f * temperatureCelsius) - (0.05f * temperatureCelsius * temperatureCelsius)", "Water sound speed uses c = 1440 + 4.6T - 0.05T^2", builder, ref failureCount);
                AssertContains(spatial, "GlobalRegistry.ThermodynamicsService", "Spatial propagation samples AbyssalThermalManager through thermodynamics service", builder, ref failureCount);
                AssertContains(spatial, "TryTraceVoxelDensityOcclusion", "Delayed world events apply voxel density occlusion", builder, ref failureCount);
                AssertContains(spatial, "PlayAtPointWithLowPass", "Delayed events route resolved low-pass cutoff into source filter", builder, ref failureCount);
                AssertContains(spatial, "ThermalShimmerMaximumPitchRatio", "Thermal plume shimmer pitch modulation exists", builder, ref failureCount);
            }

            if (occlusion.Length > 0)
            {
                AssertContains(occlusion, "internal struct AcousticVoxelOcclusionResult", "Voxel occlusion payload exists", builder, ref failureCount);
                AssertContains(occlusion, "TryTraceVoxelDensityOcclusion", "Voxel density trace API exists", builder, ref failureCount);
                AssertContains(occlusion, "NativeArray<byte> signedDistanceVoxels", "Voxel trace reads SDF NativeArray", builder, ref failureCount);
                AssertContains(occlusion, "ResolveCaveVoxelDensity01", "SDF byte samples are converted to acoustic density", builder, ref failureCount);
                AssertContains(occlusion, "tMax <= tMax.yzx", "Voxel trace uses DDA axis stepping", builder, ref failureCount);
                AssertContains(occlusion, "OpenLowPassCutoffHertz / (1f +", "Accumulated density drives heavy low-pass cutoff", builder, ref failureCount);
            }

            if (renderer.Length > 0)
            {
                string onAudioFilterRead = ExtractMethodBody(renderer, "private void OnAudioFilterRead(float[] data, int channels)");
                AssertContains(renderer, "ResolveDirectionalDopplerRatio", "Directional Doppler resolver exists", builder, ref failureCount);
                AssertContains(renderer, "math.dot((float3)sourceVelocity", "Doppler pitch direction uses source velocity dot listener direction", builder, ref failureCount);
                AssertContains(renderer, "RenderLeviathanGranularRoarSample", "Leviathan granular synthesis kernel exists", builder, ref failureCount);
                AssertContains(renderer, "NativeArray<float> baseRoarClip", "Granular kernel consumes native base roar data", builder, ref failureCount);
                AssertContains(renderer, "LeviathanRoarAggro", "Aggro is synchronized through audio parameter snapshot", builder, ref failureCount);
                AssertContains(renderer, "RenderInteriorFdnReverbSample", "Dry interior FDN reverb exists", builder, ref failureCount);
                AssertContains(renderer, "TinnitusCarrierHertz = 8000f", "O2 deprivation tinnitus carrier is 8000 Hz", builder, ref failureCount);
                AssertContains(renderer, "TinnitusLowPassCutoffHertz", "O2 deprivation lowers master LPF cutoff", builder, ref failureCount);
                AssertContains(renderer, "ResolveImpactMaterialBlend", "Impact synthesis blends both AudioMaterialID values", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "RenderLeviathanGranularRoarSample", "Leviathan synth is not in OnAudioFilterRead", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "TryTraceVoxelDensityOcclusion", "Voxel trace is not in OnAudioFilterRead", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "new ", "OnAudioFilterRead has no explicit allocation", builder, ref failureCount);
            }

            if (eventsSource.Length > 0)
                AssertContains(eventsSource, "LeviathanRoar", "Procedural audio event kind routes Leviathan roar", builder, ref failureCount);

            if (ringBuffer.Length > 0)
            {
                AssertContains(ringBuffer, "CrashTelemetryBuffer.ReportAudioOverflowDropWarning", "SPSC overflow drop emits crash telemetry", builder, ref failureCount);
                AssertContains(ringBuffer, "_lastTelemetryOverflowDropCount", "SPSC overflow telemetry is rate-gated", builder, ref failureCount);
            }

            if (telemetry.Length > 0)
            {
                AssertContains(telemetry, "AudioOverflowDropWarning", "Crash telemetry stores audio overflow fault bit", builder, ref failureCount);
                AssertContains(telemetry, "WriteAudioOverflowDropTelemetry", "Crash telemetry writes audio overflow ring entry", builder, ref failureCount);
                AssertContains(telemetry, "SystemBits.Audio", "Crash telemetry tags audio subsystem rows", builder, ref failureCount);
            }

            builder.Append("STATUS: ");
            builder.AppendLine(failureCount == 0 ? "PASS" : "FAIL");
            report = builder.ToString();
            return failureCount == 0;
        }

        private static string ReadAssetText(string assetPath, StringBuilder builder, ref int failureCount)
        {
            string root = Directory.GetParent(Application.dataPath)?.FullName;
            string absolutePath = root == null
                ? assetPath
                : Path.Combine(root, assetPath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(absolutePath))
            {
                AppendFailure(builder, ref failureCount, "Missing asset: " + assetPath);
                return string.Empty;
            }

            return File.ReadAllText(absolutePath);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
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

        private static void AssertContains(string source, string needle, string message, StringBuilder builder, ref int failureCount)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) >= 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failureCount, message + " :: missing `" + needle + "`");
        }

        private static void AssertNotContains(string source, string needle, string message, StringBuilder builder, ref int failureCount)
        {
            if (source.IndexOf(needle, StringComparison.Ordinal) < 0)
            {
                builder.Append("[PASS] ").AppendLine(message);
                return;
            }

            AppendFailure(builder, ref failureCount, message + " :: found forbidden `" + needle + "`");
        }

        private static void AppendFailure(StringBuilder builder, ref int failureCount, string message)
        {
            failureCount++;
            builder.Append("[FAIL] ").AppendLine(message);
        }
    }
}
#endif
