using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ToxicBioluminescenceBridgeEditTests
    {
        [Test]
        public void BiolumPulseSyncRuntime_ConsumesToxicBioluminescenceSignalsIntoSyncPulse()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/VFX/Bioluminescence/BiolumPulseSyncRuntime.cs"));
            string asmdef = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/VFX/Bioluminescence/Hecton8.VFX.Bioluminescence.Runtime.asmdef"));
            string tickBody = ExtractMethodBody(source, "private void AdvanceSimulationFrame(float deltaTime)");
            string consumeBody = ExtractMethodBody(source, "private void ConsumeToxicBioluminescenceSignalsToPulse()");
            string scoreBody = ExtractMethodBody(source, "private static bool TryResolveToxicBioluminescencePulseScore(");
            string aupBody = ExtractMethodBody(source, "private static bool IsUsableToxicBioluminescenceAup(");
            string releaseBody = ExtractMethodBody(source, "private void ReleaseVaultHandlesOnly(");

            StringAssert.Contains("using Hecton8.Atmosphere;", source);
            StringAssert.Contains("\"Hecton8.Core.Contracts\"", asmdef);
            StringAssert.Contains("\"Hecton8.Core\"", asmdef);
            AssertSourceOrder(tickBody, "ConsumeAcousticPingSignals();", "ConsumeToxicBioluminescenceSignalsToPulse();");
            AssertSourceOrder(tickBody, "ConsumeToxicBioluminescenceSignalsToPulse();", "AdvanceStrobe(dt);");
            StringAssert.Contains("private int _lastToxicBiolumSnapshotGeneration;", source);
            StringAssert.Contains("_lastToxicBiolumSnapshotGeneration = SignalBus<ToxicBioluminescenceSignal>.SnapshotGeneration;", releaseBody);
            StringAssert.Contains("_lastPulseOriginAUP = double3.zero;", releaseBody);
            StringAssert.Contains("_activeSyncPulseCount = 0;", releaseBody);
            StringAssert.Contains("_pendingTelemetryFlags = 0;", releaseBody);

            StringAssert.Contains("int snapshotGeneration = SignalBus<ToxicBioluminescenceSignal>.SnapshotGeneration;", consumeBody);
            StringAssert.Contains("snapshotGeneration == 0 || snapshotGeneration == _lastToxicBiolumSnapshotGeneration", consumeBody);
            StringAssert.Contains("ReadOnlySpan<ToxicBioluminescenceSignal> signals = SignalBus<ToxicBioluminescenceSignal>.GetFrameSnapshot();", consumeBody);
            StringAssert.Contains("TryResolveToxicBioluminescencePulseScore(in signal, out float score)", consumeBody);
            StringAssert.Contains("score > strongestScore || (score == strongestScore && signal.ToxicDensity > strongest.ToxicDensity)", consumeBody);
            StringAssert.Contains("_lastToxicBiolumSnapshotGeneration = snapshotGeneration;", consumeBody);
            StringAssert.Contains("TryAcquireBiolumGuard(vault, SyncPulseGuardMask)", consumeBody);
            StringAssert.Contains("TryResolveBiolumVaultBuffer(vault, in _syncPulsesHandle, BufferID.BiolumSyncPulses, SyncPulseCapacity, out NativeArray<SyncPulseDTO> pulses)", consumeBody);
            StringAssert.Contains("TryResolveBiolumVaultBuffer(vault, in _syncPulseAgesHandle, BufferID.BiolumSyncPulseAges, SyncPulseCapacity, out NativeArray<float> ages)", consumeBody);
            StringAssert.Contains("pulses[slot] = new SyncPulseDTO", consumeBody);
            StringAssert.Contains("OriginAUP = strongest.AUP", consumeBody);
            StringAssert.Contains("WaveSpeed = ResolveToxicBioluminescenceWaveSpeed(strongestScore, strongest.ToxicDensity)", consumeBody);
            StringAssert.Contains("ColorOverride = ResolveToxicBioluminescenceColor(strongestScore, strongest.ToxicDensity)", consumeBody);
            StringAssert.Contains("ages[slot] = 0f;", consumeBody);
            StringAssert.Contains("_lastPulseOriginAUP = strongest.AUP;", consumeBody);
            StringAssert.Contains("_activeSyncPulseCount = math.min(_activeSyncPulseCount + 1, count);", consumeBody);
            StringAssert.Contains("private const byte TelemetryFlagToxicPulse = 8;", source);
            StringAssert.Contains("_pendingTelemetryFlags |= TelemetryFlagToxicPulse;", consumeBody);
            StringAssert.Contains("_forceSchedule = true;", consumeBody);
            StringAssert.Contains("ReleaseBiolumGuard(vault, SyncPulseGuardMask)", consumeBody);
            StringAssert.DoesNotContain("SignalBus<ToxicBioluminescenceSignal>.Configure", source);
            StringAssert.DoesNotContain("SignalBus<ToxicBioluminescenceSignal>.EnsureInitialized", source);

            StringAssert.Contains("(signal.Flags & ToxicBioluminescenceSignal.FlagActive) == 0", scoreBody);
            StringAssert.Contains("IsUsableToxicBioluminescenceAup(signal.AUP)", scoreBody);
            StringAssert.Contains("math.isfinite(signal.Intensity01) ? math.saturate(signal.Intensity01) : 0f", scoreBody);
            StringAssert.Contains("math.isfinite(signal.ToxicDensity) ? math.max(0f, signal.ToxicDensity) : 0f", scoreBody);
            StringAssert.Contains("intensity <= 0.0001f || density <= 0.0001f", scoreBody);
            StringAssert.Contains("score = math.saturate(intensity * 0.72f + density01 * 0.28f);", scoreBody);

            StringAssert.Contains("math.all(math.isfinite(aup))", aupBody);
            StringAssert.Contains("math.lengthsq(aup) > 0.000001d", aupBody);
            StringAssert.Contains("math.abs(aup.x) <= ToxicityExposureSignal.MaxSourceAupExtentMeters", aupBody);
            StringAssert.Contains("math.abs(aup.y) <= ToxicityExposureSignal.MaxSourceAupExtentMeters", aupBody);
            StringAssert.Contains("math.abs(aup.z) <= ToxicityExposureSignal.MaxSourceAupExtentMeters", aupBody);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static void AssertSourceOrder(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beforeIndex, 0, "Missing source token: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing source token: " + after);
            Assert.Less(beforeIndex, afterIndex);
        }
    }
}
