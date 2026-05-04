#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    /// <summary>
    /// Editor-only source smoke test for the critical procedural audio thread contract.
    /// </summary>
    public static class DSPThreadSafetySmokeTester
    {
        private const string RendererPath = "Assets/_Project/Scripts/Audio/PlayerCriticalProceduralAudioRenderer.cs";
        private const string RingBufferPath = "Assets/_Project/Scripts/Audio/NativeAudioFrameRingBuffer.cs";
        private const string SpatialAudioPath = "Assets/_Project/Scripts/SpatialAudioManager.cs";
        private const string OcclusionPath = "Assets/_Project/Scripts/World/AcousticOcclusionUtility.cs";

        [MenuItem("Hecton8/Audio/Run DSP Thread Safety Smoke Test")]
        public static void RunMenuItem()
        {
            bool passed = Run(out string report);
            if (passed)
                Debug.Log(report);
            else
                Debug.LogError(report);
        }

        /// <summary>
        /// Runs source-level assertions for SPSC, callback purity, Hermite wrapping, Sabine RT60, and bubble math.
        /// </summary>
        public static bool Run(out string report)
        {
            int failureCount = 0;
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("[DSPThreadSafetySmokeTester]");

            string renderer = ReadAssetText(RendererPath, builder, ref failureCount);
            string ringBuffer = ReadAssetText(RingBufferPath, builder, ref failureCount);
            string spatialAudio = ReadAssetText(SpatialAudioPath, builder, ref failureCount);
            string occlusion = ReadAssetText(OcclusionPath, builder, ref failureCount);

            if (renderer.Length > 0)
            {
                string onAudioFilterRead = ExtractMethodBody(renderer, "private void OnAudioFilterRead(float[] data, int channels)");
                string produceAudioBlock = ExtractMethodBody(renderer, "private void ProduceAudioBlock(int frameCount)");
                string publishSnapshot = ExtractMethodBody(renderer, "private void PublishAudioParameterSnapshot()");
                string hermiteSampleRing = ExtractMethodBody(renderer, "private static float HermiteSampleRing(NativeArray<float> buffer, double cursor, int mask)");
                string disposeBuffers = ExtractMethodBody(renderer, "private void DisposeBuffers(bool disposeSabineReverbDelay)");
                string onDisable = ExtractMethodBody(renderer, "private void OnDisable()");
                string onDestroy = ExtractMethodBody(renderer, "private void OnDestroy()");

                AssertContains(renderer, "private struct AudioParameterSnapshot", "AudioParameterSnapshot value struct exists", builder, ref failureCount);
                AssertContains(renderer, "internal struct AudioThreadDiagnostics", "Audio thread diagnostic snapshot exists", builder, ref failureCount);
                AssertContains(renderer, "TryGetAudioThreadDiagnostics(out AudioThreadDiagnostics diagnostics)", "Audio diagnostics expose SPSC counters without touching callback", builder, ref failureCount);
                AssertContains(renderer, "diagnostics.UnderrunCount = sampleRingBuffer.UnderrunCount", "Audio diagnostics include underrun count", builder, ref failureCount);
                AssertContains(renderer, "diagnostics.OverflowDropCount = sampleRingBuffer.OverflowDropCount", "Audio diagnostics include overflow drop count", builder, ref failureCount);
                AssertOccurrenceCount(produceAudioBlock, "Volatile.Read(ref _audioParameterSnapshotReadIndex)", 1, "Snapshot read occurs once per produced DSP block", builder, ref failureCount);
                AssertContains(publishSnapshot, "Interlocked.Exchange(ref _audioParameterSnapshotReadIndex", "Main thread publishes inactive snapshot with Interlocked.Exchange", builder, ref failureCount);

                AssertContains(onAudioFilterRead, "sampleRingBuffer.MixInterleavedInto(data, channels)", "OnAudioFilterRead remains SPSC transfer bridge", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "new ", "OnAudioFilterRead has no explicit allocation", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, ".ToList(", "OnAudioFilterRead has no LINQ ToList", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, ".Where(", "OnAudioFilterRead has no LINQ Where", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "lock (", "OnAudioFilterRead has no lock statement", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "lock(", "OnAudioFilterRead has no compact lock statement", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "Complete(", "OnAudioFilterRead has no JobHandle.Complete", builder, ref failureCount);
                AssertNotContains(onAudioFilterRead, "ResolveCriticalSidechainDuckingGain", "Compressor is outside managed callback", builder, ref failureCount);

                AssertContains(renderer, "CriticalSidechainDuckedGain = 0.25118864f", "Critical sidechain gain is -12 dB", builder, ref failureCount);
                AssertContains(renderer, "CriticalSidechainAttackSeconds = 0.05f", "Critical sidechain attack is 0.05 s", builder, ref failureCount);
                AssertContains(renderer, "CriticalSidechainReleaseSeconds = 0.3f", "Critical sidechain release is 0.3 s", builder, ref failureCount);
                AssertContains(renderer, "ResolveCriticalSidechainDuckingGain", "Producer-side sidechain compressor exists", builder, ref failureCount);

                AssertContains(hermiteSampleRing, "WrapRingCursor(cursor, capacity)", "Hermite read pointer is explicitly wrapped", builder, ref failureCount);
                AssertContains(hermiteSampleRing, "HermiteFractionMaximum", "Hermite fractional phase is clamped", builder, ref failureCount);
                AssertContains(hermiteSampleRing, "buffer[(baseIndex - 1) & mask]", "Hermite xm1 tap uses bitwise mask", builder, ref failureCount);
                AssertContains(hermiteSampleRing, "buffer[(baseIndex + 2) & mask]", "Hermite x2 tap uses bitwise mask", builder, ref failureCount);
                AssertContains(renderer, "SonarEchoMaximumDopplerRatio = 4f", "Sonar Doppler supports >3.0 guarded ratio", builder, ref failureCount);

                AssertContains(renderer, "HullStressFmBaseCarrierHertz = 80f", "Hull FM carrier base is 80 Hz", builder, ref failureCount);
                AssertContains(renderer, "HullStressFmModulationIndexMinimum = 0.1f", "Hull FM modulation index minimum is 0.1", builder, ref failureCount);
                AssertContains(renderer, "HullStressFmModulationIndexMaximum = 12f", "Hull FM modulation index maximum is 12", builder, ref failureCount);
                AssertContains(renderer, "HullStressFmBaseCarrierHertz + modulator", "Hull FM carrier is modulated by stress/noise", builder, ref failureCount);

                AssertContains(renderer, "PsychoacousticPressureReferenceDepthMeters = 500f", "Depth LPF uses 500 m pressure reference", builder, ref failureCount);
                AssertContains(renderer, "PsychoacousticPressureMinimumCutoffHertz", "Depth LPF has intelligibility floor", builder, ref failureCount);
                AssertContains(renderer, "openCutoff / math.max(pressureScalar, 1f)", "Depth LPF follows openCutoff / (1 + depth/reference)", builder, ref failureCount);

                AssertContains(renderer, "private static void RenderMinnaertBubbleBurstKernel(", "Minnaert bubble burst producer kernel exists", builder, ref failureCount);
                AssertContains(renderer, "_bubbleScratch", "Producer path injects bubble bursts into the reusable native scratch buffer", builder, ref failureCount);
                AssertContains(renderer, "RenderMinnaertBubbleBurstKernel(", "Producer path injects bubble bursts through the Minnaert kernel", builder, ref failureCount);
                AssertContains(renderer, "ResolveMinnaertFrequency", "Minnaert bubble frequency kernel exists", builder, ref failureCount);
                AssertContains(renderer, "math.rcp(2f * math.PI * safeRadius) * root", "Minnaert formula uses 1/(2*pi*R)*sqrt term", builder, ref failureCount);
                AssertContains(renderer, "RenderMinnaertBubbleSample", "Bubble burst sample kernel exists", builder, ref failureCount);

                AssertContains(renderer, "BinauralWaterItdDelayRatio = 0.2326f", "Water/air ITD blend ratio exists", builder, ref failureCount);
                AssertContains(renderer, "math.lerp(airItdSeconds, airItdSeconds * BinauralWaterItdDelayRatio", "Renderer blends ITD using WaterDensityMul", builder, ref failureCount);

                AssertContains(renderer, "_sabineReverbDelay = new NativeArray<float>(SabineReverbDelayCapacity, Allocator.AudioKernel", "Sabine delay cache is persistent native audio memory", builder, ref failureCount);
                AssertNotContains(onDisable, "DisposeBuffers", "OnDisable does not dispose Sabine cache", builder, ref failureCount);
                AssertContains(onDestroy, "DisposeBuffers(disposeSabineReverbDelay: true)", "OnDestroy owns final Sabine cache disposal", builder, ref failureCount);
                AssertContains(disposeBuffers, "disposeSabineReverbDelay && _sabineReverbDelay.IsCreated", "Sabine dispose is gated to destroy path", builder, ref failureCount);
            }

            if (ringBuffer.Length > 0)
            {
                string mixInterleavedInto = ExtractMethodBody(ringBuffer, "public void MixInterleavedInto(float[] destination, int channels)");
                string tryWriteInterleaved = ExtractMethodBody(ringBuffer, "public bool TryWriteInterleaved(NativeArray<float> source, int frameCount, int sourceChannels)");

                AssertContains(ringBuffer, "public int UnderrunCount => Volatile.Read(ref _underrunCount)", "SPSC bridge exposes underrun diagnostic counter", builder, ref failureCount);
                AssertContains(ringBuffer, "public int OverflowDropCount => Volatile.Read(ref _overflowDropCount)", "SPSC bridge exposes overflow diagnostic counter", builder, ref failureCount);
                AssertContains(mixInterleavedInto, "Array.Clear(destination, 0, destination.Length)", "SPSC underrun path clears Unity output buffer", builder, ref failureCount);
                AssertContains(mixInterleavedInto, "Interlocked.Increment(ref _underrunCount)", "SPSC underrun path records atomic counter", builder, ref failureCount);
                AssertContains(mixInterleavedInto, "bool hasFrame = frameIndex < framesToConsume", "Partial underrun tail is explicitly zeroed", builder, ref failureCount);
                AssertContains(tryWriteInterleaved, "Interlocked.Increment(ref _overflowDropCount)", "Producer overflow drop is recorded atomically", builder, ref failureCount);
                AssertNotContains(mixInterleavedInto, "new ", "SPSC consumer bridge has no explicit allocation", builder, ref failureCount);
                AssertNotContains(mixInterleavedInto, ".ToList(", "SPSC consumer bridge has no LINQ ToList", builder, ref failureCount);
                AssertNotContains(mixInterleavedInto, ".Where(", "SPSC consumer bridge has no LINQ Where", builder, ref failureCount);
                AssertNotContains(mixInterleavedInto, "lock (", "SPSC consumer bridge has no lock statement", builder, ref failureCount);
                AssertNotContains(mixInterleavedInto, "lock(", "SPSC consumer bridge has no compact lock statement", builder, ref failureCount);
                AssertNotContains(mixInterleavedInto, "Complete(", "SPSC consumer bridge has no JobHandle.Complete", builder, ref failureCount);
            }

            if (spatialAudio.Length > 0)
            {
                AssertContains(spatialAudio, "ThreatBusDuckMaximumDb = -12f", "Threat/Bed mixer duck target is -12 dB", builder, ref failureCount);
                AssertContains(spatialAudio, "ThreatBusDuckAttackSeconds = 0.05f", "Threat/Bed mixer duck attack is 0.05 s", builder, ref failureCount);
                AssertContains(spatialAudio, "ThreatBusDuckReleaseSeconds = 0.3f", "Threat/Bed mixer duck release is 0.3 s", builder, ref failureCount);
                AssertContains(spatialAudio, "WaterDensityMul = waterDensityMul", "Binaural telemetry publishes WaterDensityMul", builder, ref failureCount);
                AssertContains(spatialAudio, "ItdSeconds = airItdSeconds", "Spatial telemetry publishes air ITD for renderer-side blend", builder, ref failureCount);
            }

            if (occlusion.Length > 0)
            {
                AssertContains(occlusion, "EnclosureProbeSliceCount = EnclosureProbeCount", "Enclosure dispatch uses all six orthogonal rays per SlowTick", builder, ref failureCount);
                AssertContains(occlusion, "RaycastCommand.ScheduleBatch", "Enclosure/occlusion probes use RaycastCommand batches", builder, ref failureCount);
                AssertContains(occlusion, "SabineConstant * volume", "Sabine RT60 uses 0.161 * volume / absorption", builder, ref failureCount);
                AssertContains(occlusion, "EquivalentAbsorptionArea", "Sabine total absorption is carried in enclosure result", builder, ref failureCount);
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

        private static void AssertContains(string source, string token, string description, StringBuilder builder, ref int failureCount)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) >= 0)
            {
                AppendPass(builder, description);
                return;
            }

            AppendFailure(builder, ref failureCount, description);
        }

        private static void AssertNotContains(string source, string token, string description, StringBuilder builder, ref int failureCount)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0)
            {
                AppendPass(builder, description);
                return;
            }

            AppendFailure(builder, ref failureCount, description);
        }

        private static void AssertOccurrenceCount(string source, string token, int expectedCount, string description, StringBuilder builder, ref int failureCount)
        {
            int count = CountOccurrences(source, token);
            if (count == expectedCount)
            {
                AppendPass(builder, description);
                return;
            }

            AppendFailure(builder, ref failureCount, description + " count=" + count + " expected=" + expectedCount);
        }

        private static int CountOccurrences(string source, string token)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                int found = source.IndexOf(token, index, StringComparison.Ordinal);
                if (found < 0)
                    return count;

                count++;
                index = found + token.Length;
            }

            return count;
        }

        private static void AppendPass(StringBuilder builder, string description)
        {
            builder.Append("PASS: ");
            builder.AppendLine(description);
        }

        private static void AppendFailure(StringBuilder builder, ref int failureCount, string description)
        {
            failureCount++;
            builder.Append("FAIL: ");
            builder.AppendLine(description);
        }
    }
}
#endif
