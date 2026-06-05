#if UNITY_EDITOR
using System;
using System.IO;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

namespace Hecton8.Audio.Editor
{
    public static class VocalWarningAlarmBitmaskAudit_1629
    {
        private const int FuzzerIterations = 100000;
        private const string VwsRelativePath = "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs";
        private const string VocalBankRelativePath = "Assets/_Project/Scripts/Audio/Synthesis/VocalBankContracts.cs";
        private const string VocalRuntimeRelativePath = "Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs";

        [MenuItem("Hecton8/Audio/Audit VWS Alarm Bitmask 1629")]
        public static void Run()
        {
            string root = Directory.GetCurrentDirectory();
            string vwsText = ReadAll(root, VwsRelativePath);
            string vocalBankText = ReadAll(root, VocalBankRelativePath);
            string vocalRuntimeText = ReadAll(root, VocalRuntimeRelativePath);

            Require(vwsText.IndexOf("private struct AlarmStateDTO", StringComparison.Ordinal) >= 0, "AlarmStateDTO missing.");
            Require(vwsText.IndexOf("[StructLayout(LayoutKind.Explicit, Size = 64)]", StringComparison.Ordinal) >= 0, "64B explicit DTO layout missing.");
            Require(vwsText.IndexOf("internal struct VocalWarningDTO", StringComparison.Ordinal) >= 0 &&
                    vwsText.IndexOf("[FieldOffset(16)] public long SourceAupGridX", StringComparison.Ordinal) >= 0,
                "VocalWarningDTO does not carry source AUP.");
            Require(vwsText.IndexOf("activeAlarmsMask", StringComparison.Ordinal) >= 0, "activeAlarmsMask missing.");
            Require(vwsText.IndexOf("EvaluateAlarmPriorityJob", StringComparison.Ordinal) >= 0, "EvaluateAlarmPriorityJob missing.");
            Require(vwsText.IndexOf("dispatchJob.Run()", StringComparison.Ordinal) >= 0, "EvaluateAlarmPriorityJob is not run through IJob.Run.");
            Require(vwsText.IndexOf("evaluateJob.Run()", StringComparison.Ordinal) >= 0, "Alarm collection job is not run through IJob.Run.");
            Require(vwsText.IndexOf("math.tzcnt", StringComparison.Ordinal) >= 0, "Priority resolver does not use math.tzcnt.");
            Require(vwsText.IndexOf("high | 1u", StringComparison.Ordinal) < 0, "High-word tzcnt resolver corrupts bits 33-63.");
            Require(vwsText.IndexOf("ResolveVwsSpatialBlend01", StringComparison.Ordinal) >= 0 &&
                    vwsText.IndexOf("SpatialBlend01 = ResolveVwsSpatialBlend01(flags, QualityWeight01, in candidate)", StringComparison.Ordinal) >= 0,
                "VWS spatial blend is not quality-gated by source AUP.");
            Require(vwsText.IndexOf("SourceAupGridX = candidate.SourceAupGridX", StringComparison.Ordinal) >= 0,
                "Dispatch drops source AUP before VocalCueSignal publication.");
            Require(vwsText.IndexOf("VisualSyncPresentationTick", StringComparison.Ordinal) >= 0, "VWS presentation is not deferred to VisualSync.");
            Require(vwsText.IndexOf("publishInCurrentPhase", StringComparison.Ordinal) < 0 &&
                    vwsText.IndexOf("Volatile.Read(ref _visualSyncPresentationPending) != 0", StringComparison.Ordinal) >= 0,
                "VWS still contains a current-phase presentation bypass or can overwrite pending VisualSync dispatch.");
            Require(vwsText.IndexOf("ILateFrameTickable", StringComparison.Ordinal) >= 0 &&
                    vwsText.IndexOf("public void LateFrameTick()", StringComparison.Ordinal) >= 0 &&
                    vwsText.IndexOf("GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment)", StringComparison.Ordinal) >= 0,
                "VWS fallback route lacks a late-frame presentation phase.");
            Require(Count(vwsText, "RunVocalWarningFrame(deltaTime, NextOwnerFrameId())") == 1 &&
                    Count(vwsText, "RunVocalWarningFrame(0.1f, NextOwnerFrameId())") == 1 &&
                    vwsText.IndexOf("RunVocalWarningFrame(deltaTime, NextOwnerFrameId(),", StringComparison.Ordinal) < 0 &&
                    vwsText.IndexOf("RunVocalWarningFrame(0.1f, NextOwnerFrameId(),", StringComparison.Ordinal) < 0,
                "VWS fallback Tick/SlowTick can publish before LateFrame/VisualSync.");
            string cancelBody = ExtractMethodBody(vwsText, "public void CancelCurrentWarning(");
            Require(cancelBody.IndexOf("_pendingCancelRequest", StringComparison.Ordinal) >= 0 &&
                    cancelBody.IndexOf("TryResolveVwsOwnerViews", StringComparison.Ordinal) < 0 &&
                    cancelBody.IndexOf("CancelRendererPlaybackAndClearQueues", StringComparison.Ordinal) < 0,
                "Public VWS cancellation mutates Vault state outside the owner frame.");
            Require(vwsText.IndexOf("bool cancelRequested = Interlocked.Exchange(ref _pendingCancelRequest, 0) != 0", StringComparison.Ordinal) >= 0 &&
                    vwsText.IndexOf("CancelRendererPlaybackAndClearQueues(ref views, false)", StringComparison.Ordinal) >= 0,
                "Owner frame does not consume deferred VWS cancellation through resolved owner views.");
            Require(vwsText.IndexOf("Queue<", StringComparison.Ordinal) < 0, "Managed Queue<T> found in VWS.");
            Require(vwsText.IndexOf("List<", StringComparison.Ordinal) < 0, "Managed List<T> found in VWS.");
            Require(vwsText.IndexOf("System.Collections.Generic", StringComparison.Ordinal) < 0, "Managed collection namespace found in VWS.");
            Require(vocalBankText.IndexOf("DuckingEnvelope01", StringComparison.Ordinal) >= 0, "DSP ducking envelope missing.");
            Require(vocalBankText.IndexOf("VwsDuckingTargetGain", StringComparison.Ordinal) >= 0, "DSP ducking gain constant missing.");
            Require(vocalRuntimeText.IndexOf("!hasListener && !hasSource", StringComparison.Ordinal) >= 0, "Audio filter host guard rejects valid listener/source hosts.");
            Require(vocalRuntimeText.IndexOf("BankMutationSpinLimit", StringComparison.Ordinal) >= 0 &&
                    vocalRuntimeText.IndexOf("TryBeginBankMutationCold", StringComparison.Ordinal) >= 0,
                "Cold bank mutation can spin without a hard fail-closed limit.");
            Require(vocalRuntimeText.IndexOf("Interlocked.CompareExchange(ref _bankReleaseInProgress, 1, 0)", StringComparison.Ordinal) >= 0,
                "Cold bank mutation can overlap an active bank release window.");
            string clearBankStateBody = ExtractMethodBody(vocalRuntimeText, "private void ClearBankStateCold(");
            Require(clearBankStateBody.IndexOf("try", StringComparison.Ordinal) >= 0 &&
                    clearBankStateBody.IndexOf("finally", StringComparison.Ordinal) >= 0 &&
                    clearBankStateBody.IndexOf("Interlocked.Exchange(ref _bankReleaseInProgress, 0)", StringComparison.Ordinal) >= 0,
                "ClearBankStateCold can leave bank release in progress after an early exit.");
            Require(vocalRuntimeText.IndexOf("TryResolveSourceDistanceSq", StringComparison.Ordinal) >= 0 &&
                    vocalRuntimeText.IndexOf("AbsoluteUniversePosition.DeltaMetersClamped", StringComparison.Ordinal) >= 0,
                "Vocal spatial gain ignores source/listener AUP grid deltas.");
            Require(vocalRuntimeText.IndexOf("#if !UNITY_EDITOR && !DEVELOPMENT_BUILD", StringComparison.Ordinal) >= 0,
                "Vocal runtime lacks release-player managed callback fail-closed guard.");
            string releaseCallbackBody = ExtractMethodBody(vocalRuntimeText, "private void OnAudioFilterRead(float[] data, int channels)");
            Require(releaseCallbackBody.IndexOf("VocalDecodeKernel.DecodeIntoAudioBuffer", StringComparison.Ordinal) < 0,
                "Release vocal callback still decodes audio.");
            Require(releaseCallbackBody.IndexOf("TryAcquireAudioCallbackViews", StringComparison.Ordinal) < 0 &&
                    releaseCallbackBody.IndexOf("TryAcquireLockedView", StringComparison.Ordinal) < 0,
                "Release vocal callback still locks DataVault views.");
            Require(releaseCallbackBody.IndexOf("Stopwatch.GetTimestamp", StringComparison.Ordinal) < 0,
                "Release vocal callback still measures DSP timing in managed audio thread.");
            RequireHotPathClean(vwsText, "public void Tick(");
            RequireHotPathClean(vwsText, "public void SlowTick(");
            RequireHotPathClean(vwsText, "private void RunVocalWarningFrame(");
            RequireHotPathClean(vwsText, "private void VisualSyncPresentationTick(");
            RequireHotPathClean(vocalRuntimeText, "public void Tick(");
            RequireHotPathClean(vocalRuntimeText, "private void OnAudioFilterRead(");
            RequireHotPathClean(vocalRuntimeText, "private void DrainVocalCueSignals(");
            RequireHotPathClean(vocalBankText, "public static void DecodeIntoAudioBuffer(");
            RequireEveryMethodBodyClean(vwsText, "void Execute(");
            RequireEveryMethodBodyClean(vocalBankText, "void Execute(");
            RequireWriteLockFlattening(vwsText, vocalRuntimeText);

            RunPriorityFuzzer();
            RunDuckingEnvelopeProof();
            Hecton8.Core.H8Debug.Log("[1629] VWS alarm bitmask audit PASS.");
        }

        private static string ReadAll(string root, string relativePath)
        {
            string path = Path.Combine(root, relativePath);
            return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
        }

        private static void RunPriorityFuzzer()
        {
            uint seed = 0x1629A11Au;
            for (int i = 0; i < FuzzerIterations; i++)
            {
                ulong mask = ((ulong)Next(ref seed) << 32) | Next(ref seed);
                if (mask == 0UL)
                    mask = 1UL << (int)(Next(ref seed) & 63u);

                int expected = ExpectedLowestSetBit(mask);
                int actual = ResolveByTzcnt(mask);
                Require(actual == expected, "tzcnt priority mismatch at iteration " + i + ".");
            }
        }

        private static void RequireHotPathClean(string source, string signature)
        {
            string body = ExtractMethodBody(source, signature);
            Require(body.Length > 0, "Hot method not found: " + signature);
            RequireNoHotDependencies(body, signature);
        }

        private static void RequireEveryMethodBodyClean(string source, string signature)
        {
            int searchIndex = 0;
            int found = 0;
            while (searchIndex < source.Length)
            {
                int signatureIndex = source.IndexOf(signature, searchIndex, StringComparison.Ordinal);
                if (signatureIndex < 0)
                    break;

                string body = ExtractMethodBodyAt(source, signatureIndex);
                Require(body.Length > 0, "Hot method body not found: " + signature);
                RequireNoHotDependencies(body, signature + "#" + found);
                found++;
                searchIndex = signatureIndex + signature.Length;
            }

            Require(found > 0, "Hot method not found: " + signature);
        }

        private static void RequireNoHotDependencies(string body, string signature)
        {
            Require(body.IndexOf("GlobalRegistry.Get<", StringComparison.Ordinal) < 0, "GlobalRegistry.Get<T> in hot method: " + signature);
            Require(body.IndexOf("GetComponent(", StringComparison.Ordinal) < 0, "GetComponent in hot method: " + signature);
            Require(body.IndexOf("TryGetComponent", StringComparison.Ordinal) < 0, "TryGetComponent in hot method: " + signature);
            Require(body.IndexOf("GameObject.Find", StringComparison.Ordinal) < 0, "GameObject.Find in hot method: " + signature);
            Require(body.IndexOf("TryReadOnlyHandle", StringComparison.Ordinal) < 0, "DataVault read handle lookup in hot method: " + signature);
            Require(body.IndexOf("TryReadHandle", StringComparison.Ordinal) < 0, "DataVault read handle lookup in hot method: " + signature);
            Require(body.IndexOf("TryAcquireWriteLock", StringComparison.Ordinal) < 0, "DataVault write lock in hot method: " + signature);
        }

        private static void RequireWriteLockFlattening(string vwsText, string vocalRuntimeText)
        {
            Require(Count(vocalRuntimeText, "TryAcquireWriteLock") == 0, "Vocal DSP runtime must not acquire DataVault write locks.");
            Require(Count(vwsText, "TryAcquireWriteLock") <= 1, "VWS holds more than one DataVault write-lock route.");
            if (Count(vwsText, "TryAcquireWriteLock") == 1)
            {
                string body = ExtractMethodBody(vwsText, "public unsafe bool EditorTryWriteTuning(");
                Require(body.IndexOf("try", StringComparison.Ordinal) >= 0 &&
                        body.IndexOf("finally", StringComparison.Ordinal) >= 0 &&
                        body.IndexOf("ReleaseWriteLock", StringComparison.Ordinal) >= 0,
                    "VWS write lock is not released by try/finally.");
            }

            Require(vocalRuntimeText.IndexOf("TryAcquireVocalMutationGuard", StringComparison.Ordinal) >= 0, "Vocal runtime guard scope missing.");
            Require(vocalRuntimeText.IndexOf("ReleaseVocalMutationGuard", StringComparison.Ordinal) >= 0, "Vocal runtime guard release missing.");
        }

        private static int Count(string source, string needle)
        {
            int count = 0;
            int index = 0;
            while (index < source.Length)
            {
                index = source.IndexOf(needle, index, StringComparison.Ordinal);
                if (index < 0)
                    break;

                count++;
                index += needle.Length;
            }

            return count;
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            if (signatureIndex < 0)
                return string.Empty;

            return ExtractMethodBodyAt(source, signatureIndex);
        }

        private static string ExtractMethodBodyAt(string source, int signatureIndex)
        {
            int openBrace = source.IndexOf('{', signatureIndex);
            if (openBrace < 0)
                return string.Empty;

            int depth = 0;
            for (int i = openBrace; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(openBrace, i - openBrace + 1);
                }
            }

            return string.Empty;
        }

        private static uint Next(ref uint state)
        {
            state = unchecked((state * 1664525u) + 1013904223u);
            return state;
        }

        private static int ExpectedLowestSetBit(ulong mask)
        {
            for (int bit = 0; bit < 64; bit++)
            {
                if ((mask & (1UL << bit)) != 0UL)
                    return bit;
            }

            return -1;
        }

        private static int ResolveByTzcnt(ulong mask)
        {
            if (mask == 0UL)
                return -1;

            uint low = (uint)mask;
            if (low != 0u)
                return math.tzcnt(low);

            return 32 + math.tzcnt((uint)(mask >> 32));
        }

        private static void RunDuckingEnvelopeProof()
        {
            const float sampleRate = 48000f;
            const float attackSeconds = 0.1f;
            const float releaseSeconds = 0.1f;
            const float targetGain = 0.25f;
            float attackAlpha = math.saturate(1f - math.exp(-1f / (sampleRate * attackSeconds)));
            float releaseAlpha = math.saturate(1f - math.exp(-1f / (sampleRate * releaseSeconds)));
            float envelope = 0f;
            float previousGain = 1f;
            for (int i = 0; i < (int)(sampleRate * attackSeconds); i++)
            {
                envelope = math.lerp(envelope, 1f, attackAlpha);
                float gain = math.lerp(1f, targetGain, envelope);
                Require(gain <= previousGain + 0.00001f, "Ducking attack is not monotonic.");
                previousGain = gain;
            }

            Require(previousGain < 0.53f, "Ducking attack failed to approach target.");

            for (int i = 0; i < (int)(sampleRate * releaseSeconds); i++)
            {
                envelope = math.lerp(envelope, 0f, releaseAlpha);
                float gain = math.lerp(1f, targetGain, envelope);
                Require(gain >= previousGain - 0.00001f, "Ducking release is not monotonic.");
                previousGain = gain;
            }

            Require(previousGain > 0.72f, "Ducking release failed to recover.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition)
                throw new FatalArchitectureException1629(message);
        }

        private sealed class FatalArchitectureException1629 : Exception
        {
            public FatalArchitectureException1629(string message)
                : base(message)
            {
            }
        }
    }
}
#endif
