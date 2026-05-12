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
        private const string BufferJobsPath = "Assets/_Project/Scripts/Audio/PlayerCriticalBufferJobs.cs";
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
        /// Runs source-level assertions for SPSC, callback purity, cheap sonar echo sampling, fake cave reverb, and critical psychoacoustic kernels.
        /// </summary>
        public static bool Run(out string report)
        {
            int failureCount = 0;
            StringBuilder builder = new StringBuilder(4096);
            builder.AppendLine("[DSPThreadSafetySmokeTester]");

            string renderer = ReadAssetText(RendererPath, builder, ref failureCount);
            string ringBuffer = ReadAssetText(RingBufferPath, builder, ref failureCount);
            string bufferJobs = ReadAssetText(BufferJobsPath, builder, ref failureCount);
            string spatialAudio = ReadAssetText(SpatialAudioPath, builder, ref failureCount);
            string occlusion = ReadAssetText(OcclusionPath, builder, ref failureCount);

            if (renderer.Length > 0)
            {
                string produceAudioBlock = ExtractMethodBody(renderer, "private void ProduceAudioBlock(int frameCount)");
                string publishSnapshot = ExtractMethodBody(renderer, "private void PublishAudioParameterSnapshot()");
                string tryConsumePendingSonarTrigger = ExtractMethodBody(renderer, "private void TryConsumePendingSonarTrigger(long blockStartFrame, int frameCount)");
                string updateCaveReverb = ExtractMethodBody(renderer, "private void UpdateCaveReverb(float deltaTime)");
                string handleSonarPingSent = ExtractMethodBody(renderer, "private void HandleSonarPingSent(float intensity)");
                string renderBubbleBlock = ExtractMethodBody(renderer, "private void RenderBubbleBlock(");
                string renderTinnitusSample = ExtractMethodBody(renderer, "private static float RenderTinnitusSample(");
                string renderHullStressBlock = ExtractMethodBody(renderer, "private void RenderHullStressBlock(");
                string renderThrusterBlock = ExtractMethodBody(renderer, "private void RenderThrusterBlock(");
                string renderSonarBlock = ExtractMethodBody(renderer, "private void RenderSonarBlock(int frameCount, long blockStartFrame, double invSampleRate)");
                string linearSampleRing = ExtractMethodBody(renderer, "private static float LinearSampleRing(NativeArray<float> buffer, float cursor, int mask)");
                string disposeBuffers = ExtractMethodBody(renderer, "private void DisposeBuffers(bool disposeSabineReverbDelay)");
                string onDisable = ExtractMethodBody(renderer, "private void OnDisable()");
                string onDestroy = ExtractMethodBody(renderer, "private void OnDestroy()");

                AssertContains(renderer, "private struct AudioParameterSnapshot", "AudioParameterSnapshot value struct exists", builder, ref failureCount);
                AssertContains(renderer, "StructLayout(LayoutKind.Explicit, Size = 128)", "Audio parameter snapshot slots include 128-byte cache-line padding", builder, ref failureCount);
                AssertContains(renderer, "internal struct AudioThreadDiagnostics", "Audio thread diagnostic snapshot exists", builder, ref failureCount);
                AssertContains(renderer, "TryGetAudioThreadDiagnostics(out AudioThreadDiagnostics diagnostics)", "Audio diagnostics expose SPSC counters through native bridge state", builder, ref failureCount);
                AssertContains(renderer, "diagnostics.OverflowDropCount = sampleRingBuffer.OverflowDropCount", "Audio diagnostics include overflow drop count", builder, ref failureCount);
                AssertOccurrenceCount(produceAudioBlock, "Volatile.Read(ref _audioParameterSnapshotReadIndex)", 1, "Snapshot read occurs once per produced DSP block", builder, ref failureCount);
                AssertContains(publishSnapshot, "Interlocked.Exchange(ref _audioParameterSnapshotReadIndex", "Main thread publishes inactive snapshot with Interlocked.Exchange", builder, ref failureCount);
                AssertContains(renderer, "_workerSonarEchoTaps = new NativeArray<SonarEchoTap>", "Sonar echo taps have a worker-owned snapshot buffer", builder, ref failureCount);
                AssertContains(tryConsumePendingSonarTrigger, "_workerSonarEchoTaps[tapIndex] = sourceTapBuffer[tapIndex]", "Sonar tap payload is copied once before block rendering", builder, ref failureCount);
                AssertContains(renderSonarBlock, "NativeArray<SonarEchoTap> activeTapBuffer = _workerSonarEchoTaps", "Sonar render reads the worker tap snapshot, not the publish buffer", builder, ref failureCount);

                AssertNotContains(renderer, "OnAudioFilterRead", "Renderer has no managed Unity audio callback fallback", builder, ref failureCount);
                AssertNotContains(renderer, "MixInterleavedInto", "Renderer has no managed float[] consumer bridge", builder, ref failureCount);

                AssertContains(renderer, "CriticalSidechainDuckedGain = 0.25118864f", "Critical sidechain gain is -12 dB", builder, ref failureCount);
                AssertContains(renderer, "CriticalSidechainAttackSeconds = 0.05f", "Critical sidechain attack is 0.05 s", builder, ref failureCount);
                AssertContains(renderer, "CriticalSidechainReleaseSeconds = 0.3f", "Critical sidechain release is 0.3 s", builder, ref failureCount);
                AssertContains(renderer, "ResolveCriticalSidechainDuckingGain", "Producer-side sidechain compressor exists", builder, ref failureCount);
                AssertContains(renderThrusterBlock, "float blockBandPassCenter = math.lerp(200f, 1200f, blockThrottle)", "Thruster band-pass center is resolved once per block", builder, ref failureCount);
                AssertContains(renderThrusterBlock, "int bladeDelaySamples = math.clamp(", "Thruster blade comb delay is resolved once per block", builder, ref failureCount);
                AssertOccurrenceCount(renderThrusterBlock, "ComputeBandPassCoefficients(", 1, "Thruster block computes band-pass coefficients once", builder, ref failureCount);
                AssertNotContains(renderThrusterBlock, "math.round(_sampleRate / math.max(1f, bladePassHz))", "Thruster render loop has no per-sample blade delay rounding", builder, ref failureCount);

                AssertContains(renderSonarBlock, "LinearSampleRing(_sonarEchoDelay", "Sonar echo uses cheap linear delay sampling", builder, ref failureCount);
                AssertContains(renderer, "public int DelaySamples;", "Sonar tap payload stores precomputed delay samples", builder, ref failureCount);
                AssertContains(renderSonarBlock, "int echoDelaySamples = tap.DelaySamples;", "Sonar render loop reads precomputed delay samples", builder, ref failureCount);
                AssertNotContains(renderSonarBlock, "math.round(tap.DelaySeconds", "Sonar render loop does not round delay per sample", builder, ref failureCount);
                AssertContains(linearSampleRing, "float x0 = buffer[baseIndex & mask]", "Linear sonar sampler reads current tap with bitwise mask", builder, ref failureCount);
                AssertContains(linearSampleRing, "float x1 = buffer[(baseIndex + 1) & mask]", "Linear sonar sampler reads next tap with bitwise mask", builder, ref failureCount);
                AssertContains(linearSampleRing, "return math.lerp(x0, x1, t)", "Linear sonar sampler blends with one lerp", builder, ref failureCount);
                AssertContains(renderer, "SonarEchoMaximumDopplerRatio = 4f", "Sonar Doppler supports >3.0 guarded ratio", builder, ref failureCount);
                AssertContains(renderer, "SonarGhostEchoTapCount = 3", "Synthetic sonar ghost echo count is fixed at three taps", builder, ref failureCount);
                AssertNotContains(handleSonarPingSent, "Raycast", "Sonar ghost echo generation has no raycast call", builder, ref failureCount);
                AssertContains(renderSonarBlock, "tap.LeftPanDeltaGain", "Sonar ghost echo stereo panning uses precomputed hash pan gains", builder, ref failureCount);

                AssertContains(renderer, "HullGroanLoopPitchMinimum = 0.8f", "Hull stress loop minimum pitch is 0.8", builder, ref failureCount);
                AssertContains(renderer, "HullGroanLoopPitchMaximum = 1.2f", "Hull stress loop maximum pitch is 1.2", builder, ref failureCount);
                AssertContains(renderer, "UpdateHullGroanLoop(true, math.saturate", "Hull continuous groan is driven by authored 2D loop", builder, ref failureCount);
                AssertNotContains(renderHullStressBlock, "CarrierAPhase", "Hull DSP block has no FM carrier A chain", builder, ref failureCount);
                AssertNotContains(renderHullStressBlock, "ModulatorAPhase", "Hull DSP block has no FM modulator A chain", builder, ref failureCount);
                AssertNotContains(renderHullStressBlock, "lowCarrierFm", "Hull DSP block has no low-carrier FM branch", builder, ref failureCount);

                AssertContains(renderer, "PsychoacousticPressureReferenceDepthMeters = 500f", "Depth LPF uses 500 m pressure reference", builder, ref failureCount);
                AssertContains(renderer, "PsychoacousticPressureMinimumCutoffHertz", "Depth LPF has intelligibility floor", builder, ref failureCount);
                AssertContains(renderer, "openCutoff / math.max(pressureScalar, 1f)", "Depth LPF follows openCutoff / (1 + depth/reference)", builder, ref failureCount);

                AssertContains(updateCaveReverb, "targetWetMix = insideCaveVolume ? FakeCaveReverbMix01 : FakeOpenWaterReverbMix01", "Cave reverb wet mix is the 0.8/0.2 fake volume switch", builder, ref failureCount);
                AssertNotContains(updateCaveReverb, "TryGetCachedEnclosureSample", "Cave reverb does not use enclosure raycast fallback", builder, ref failureCount);
                AssertNotContains(updateCaveReverb, "Raycast", "Cave reverb update has no raycast path", builder, ref failureCount);
                AssertContains(renderer, "bool nativeReverbActive = parameters.ReverbDspTier != (int)ReverbDspTier.UnityProfileOnly", "Low tier keeps native interior FDN disabled", builder, ref failureCount);
                AssertContains(renderer, "float interiorFdnSend = nativeReverbActive", "Interior FDN send is gated by native reverb tier", builder, ref failureCount);

                AssertContains(renderBubbleBlock, "ToolCavitationMaximumGain", "Tool cavitation injects into the reusable bubble scratch buffer", builder, ref failureCount);
                AssertContains(renderBubbleBlock, "XorShiftSigned(sampleIndex, 0x7E5A3C91u)", "Tool cavitation uses deterministic XorShift white noise", builder, ref failureCount);
                AssertNotContains(renderer, "ResolveMinnaertFrequency", "Minnaert frequency formula is absent from critical renderer", builder, ref failureCount);
                AssertNotContains(renderer, "RenderMinnaert", "Minnaert bubble render kernel is absent from critical renderer", builder, ref failureCount);

                AssertContains(renderer, "TinnitusCarrierHertz = 8000f", "O2 deprivation tinnitus carrier is 8 kHz", builder, ref failureCount);
                AssertContains(renderTinnitusSample, "ApproximateOneMinusExpNegPositive(TinnitusPlayerStressExponentialSharpness * playerStress)", "O2 tinnitus gain uses Padé exponential approximation", builder, ref failureCount);
                AssertContains(renderer, "120f - (60f * clamped) + (12f * x2) - x3", "Padé exp(-x) numerator is present", builder, ref failureCount);

                AssertContains(renderer, "BinauralMaximumMicroDelaySeconds = 0.0007f", "Binaural fake ITD caps micro-delay at 0.7 ms", builder, ref failureCount);
                AssertContains(renderer, "math.abs(rightDot) * maxDelaySamples", "Renderer derives fake ITD delay from head-right dot", builder, ref failureCount);

                AssertContains(renderer, "_sabineReverbDelay = new NativeArray<float>(SabineReverbDelayCapacity, Allocator.AudioKernel", "Sabine delay cache is persistent native audio memory", builder, ref failureCount);
                AssertNotContains(onDisable, "DisposeBuffers", "OnDisable does not dispose Sabine cache", builder, ref failureCount);
                AssertContains(onDestroy, "DisposeBuffers(disposeSabineReverbDelay: true)", "OnDestroy owns final Sabine cache disposal", builder, ref failureCount);
                AssertContains(disposeBuffers, "disposeSabineReverbDelay && _sabineReverbDelay.IsCreated", "Sabine dispose is gated to destroy path", builder, ref failureCount);
                AssertNotContains(renderer, "UnityEngine.Random", "Critical renderer has no UnityEngine.Random call", builder, ref failureCount);
            }

            if (ringBuffer.Length > 0)
            {
                string tryWriteInterleaved = ExtractMethodBody(ringBuffer, "public bool TryWriteInterleaved(NativeArray<float> source, int frameCount, int sourceChannels)");

                AssertContains(ringBuffer, "public int OverflowDropCount => Volatile.Read(ref _overflowDropCount)", "SPSC bridge exposes overflow diagnostic counter", builder, ref failureCount);
                AssertNotContains(ringBuffer, "MixInterleavedInto(float[]", "SPSC bridge has no managed float[] consumer", builder, ref failureCount);
                AssertContains(tryWriteInterleaved, "Interlocked.Increment(ref _overflowDropCount)", "Producer overflow drop is recorded atomically", builder, ref failureCount);
                AssertContains(tryWriteInterleaved, "sourceChannels < 1 || sourceChannels > 2", "Producer rejects invalid channel counts instead of clamping them", builder, ref failureCount);
                AssertContains(tryWriteInterleaved, "if (safeChannels == 2)", "Producer has a stereo fast path for the shipped output layout", builder, ref failureCount);
                AssertContains(tryWriteInterleaved, "((writeIndex + i) & _capacityMask) << 1", "Stereo fast path wraps by ring mask and scales with a shift", builder, ref failureCount);
                AssertContains(tryWriteInterleaved, "int frameSourceIndex = i << 1", "Stereo source indexing avoids a per-channel inner loop", builder, ref failureCount);
            }

            if (bufferJobs.Length > 0)
            {
                AssertContains(bufferJobs, "public static void Clear(NativeArray<float> buffer, int count)", "PlayerCriticalBufferJobs exposes cold Clear entry point", builder, ref failureCount);
                AssertContains(bufferJobs, "UnsafeUtility.MemClear", "PlayerCriticalBufferJobs.Clear uses a single native memset", builder, ref failureCount);
                AssertContains(bufferJobs, "GetUnsafeBufferPointerWithoutChecks", "PlayerCriticalBufferJobs.Clear writes the native buffer directly", builder, ref failureCount);
                AssertContains(bufferJobs, "COLD NATIVE CLEAR", "PlayerCriticalBufferJobs.Clear documents the cold native clear boundary", builder, ref failureCount);
                AssertNotContains(bufferJobs, ".Complete(", "PlayerCriticalBufferJobs.Clear has no JobHandle.Complete barrier", builder, ref failureCount);
                AssertNotContains(bufferJobs, ".Run(", "PlayerCriticalBufferJobs.Clear has no synchronous job Run barrier", builder, ref failureCount);
            }

            if (spatialAudio.Length > 0)
            {
                AssertContains(spatialAudio, "ThreatBusDuckMaximumDb = -12f", "Threat/Bed mixer duck target is -12 dB", builder, ref failureCount);
                AssertContains(spatialAudio, "ThreatBusDuckAttackSeconds = 0.05f", "Threat/Bed mixer duck attack is 0.05 s", builder, ref failureCount);
                AssertContains(spatialAudio, "ThreatBusDuckReleaseSeconds = 0.3f", "Threat/Bed mixer duck release is 0.3 s", builder, ref failureCount);
                AssertContains(spatialAudio, "WaterDensityMul = waterDensityMul", "Binaural telemetry publishes WaterDensityMul", builder, ref failureCount);
                AssertContains(spatialAudio, "RightDot = earAxisDot", "Spatial telemetry publishes head-right dot for fake ITD", builder, ref failureCount);
                AssertContains(spatialAudio, "ItdSeconds = 0f", "Spatial telemetry does not publish true ITD delay", builder, ref failureCount);
                AssertContains(spatialAudio, "RefreshListenerCaveState", "Listener cave state is resolved by SpatialAudioManager", builder, ref failureCount);
                AssertContains(spatialAudio, "HectonVoxelVolume", "Cave reverb state uses voxel-volume records", builder, ref failureCount);
                AssertContains(spatialAudio, "localBounds.Contains", "Cave interior checks use local AABB bounds", builder, ref failureCount);
            }

            if (occlusion.Length > 0)
            {
                AssertContains(occlusion, "SdfOcclusionTransmission01 = 0.18f", "Cinematic SDF occlusion resolves to the authored hard-shadow transmission", builder, ref failureCount);
                AssertContains(occlusion, "SdfOcclusionLowPassHertz = 800f", "Cinematic SDF occlusion resolves to 800 Hz LPF", builder, ref failureCount);
                AssertNotContains(occlusion, "RaycastNonAlloc", "Cinematic voxel occlusion has no synchronous physics query", builder, ref failureCount);
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
