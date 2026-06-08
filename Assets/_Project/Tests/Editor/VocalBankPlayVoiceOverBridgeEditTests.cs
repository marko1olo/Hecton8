using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class VocalBankPlayVoiceOverBridgeEditTests
    {
        [Test]
        public void BabelLinkedAudio_PlayVoiceOverSignalIsConsumedByVocalBankRuntime()
        {
            string babelStore = ReadProjectFile("Assets", "_Project", "Scripts", "Core", "Data", "BabelDictionaryStore.cs");
            string vocalRuntime = ReadProjectFile("Assets", "_Project", "Scripts", "Audio", "Synthesis", "VocalBankPlaybackRuntime.cs");

            string tryPublishLinkedAudio = ExtractMethodBody(babelStore, "private static void TryPublishLinkedAudio(");
            string reportLinkedAudioDrop = ExtractMethodBody(babelStore, "private static void ReportLinkedAudioSignalDrop(");
            string resetBabelDiagnostics = ExtractMethodBody(babelStore, "private static void ResetStaticDiagnostics()");
            string ensureLanes = ExtractMethodBody(vocalRuntime, "private static void EnsureVocalCueLaneCold()");
            string drain = ExtractMethodBody(vocalRuntime, "private void DrainVocalCueSignals(");
            string tryStart = ExtractMethodBody(vocalRuntime, "private bool TryStartVocalCue(");
            string buildFromVoiceOver = ExtractMethodBody(vocalRuntime, "private bool TryBuildVocalCueFromPlayVoiceOver(");
            string reportMiss = ExtractMethodBody(vocalRuntime, "private void ReportPlayVoiceOverSignalMiss(");
            string clearDiagnostics = ExtractMethodBody(vocalRuntime, "private void ClearPlayVoiceOverSignalDiagnostics()");
            string publishSubtitle = ExtractMethodBody(vocalRuntime, "private bool TryPublishPlayVoiceOverSubtitleCue(");
            string reportSubtitleDrop = ExtractMethodBody(vocalRuntime, "private void ReportPlayVoiceOverSubtitleDrop(");
            string resolveSubtitleDuration = ExtractMethodBody(vocalRuntime, "private static ushort ResolveActiveVocalSubtitleDurationMilliseconds(");
            string recordBankMiss = ExtractMethodBody(vocalRuntime, "private void RecordVocalBankMiss(");
            string reportBankMiss = ExtractMethodBody(vocalRuntime, "private void ReportVocalBankMiss(");
            string unregister = ExtractMethodBody(vocalRuntime, "private void UnregisterRuntime()");

            StringAssert.Contains("using Hecton8.Core.Data;", vocalRuntime);
            StringAssert.Contains("public static int LinkedAudioSignalPushDropCount => System.Threading.Volatile.Read(ref s_x001BabelDictionaryStoreSignalPushDropCount);", babelStore);
            StringAssert.Contains("public static int LinkedAudioSignalDropTelemetryCount => System.Threading.Volatile.Read(ref s_linkedAudioSignalDropTelemetryCount);", babelStore);
            StringAssert.Contains("public int PlayVoiceOverSignalConsumedCount => _playVoiceOverSignalConsumedCount;", vocalRuntime);
            StringAssert.Contains("public int PlayVoiceOverSignalMissCount => _playVoiceOverSignalMissCount;", vocalRuntime);
            StringAssert.Contains("public uint LastPlayVoiceOverTextHash => _lastPlayVoiceOverTextHash;", vocalRuntime);
            StringAssert.Contains("public uint LastPlayVoiceOverVoiceHash => _lastPlayVoiceOverVoiceHash;", vocalRuntime);
            StringAssert.Contains("public int VocalBankMissTelemetryCount => _vocalBankMissTelemetryCount;", vocalRuntime);
            StringAssert.Contains("public int PlayVoiceOverSubtitleCuePublishedCount => _playVoiceOverSubtitleCuePublishedCount;", vocalRuntime);
            StringAssert.Contains("public int PlayVoiceOverSubtitleCueDropCount => _playVoiceOverSubtitleCueDropCount;", vocalRuntime);

            StringAssert.Contains("SignalBus<PlayVoiceOverSignal>.Configure(expectedCapacity: 32, maxFrameSignals: 32, lowTierFrameSignals: 8);", ensureLanes);
            StringAssert.Contains("SignalBus<PlayVoiceOverSignal>.EnsureInitialized();", ensureLanes);
            StringAssert.Contains("SignalBus<SubtitleCueSignal>.Configure(", ensureLanes);
            StringAssert.Contains("laneHash: SubtitleCueSignal.LaneHash", ensureLanes);
            StringAssert.Contains("SignalBus<SubtitleCueSignal>.EnsureInitialized();", ensureLanes);
            StringAssert.Contains("SignalBus<VocalCueSignal>.EnsureInitialized();", ensureLanes);

            StringAssert.Contains("uint voiceHash = linkedAudioHashes[entryIndex];", tryPublishLinkedAudio);
            StringAssert.Contains("if (voiceHash == 0u)", tryPublishLinkedAudio);
            StringAssert.Contains("PlayVoiceOverSignal signal = new PlayVoiceOverSignal", tryPublishLinkedAudio);
            StringAssert.Contains("TextHash = textHash", tryPublishLinkedAudio);
            StringAssert.Contains("VoiceHash = voiceHash", tryPublishLinkedAudio);
            StringAssert.Contains("if (!SignalBus<PlayVoiceOverSignal>.TryPushTracked(in signal", tryPublishLinkedAudio);
            StringAssert.Contains("ReportLinkedAudioSignalDrop(textHash, voiceHash);", tryPublishLinkedAudio);

            StringAssert.Contains("System.Threading.Interlocked.Increment(ref s_linkedAudioSignalDropTelemetryCount)", reportLinkedAudioDrop);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportLinkedAudioDrop);
            StringAssert.Contains("LinkedAudioSignalDropWarningHash", reportLinkedAudioDrop);
            StringAssert.Contains("LinkedAudioSignalDropContextHash ^ textHash ^ voiceHash", reportLinkedAudioDrop);
            StringAssert.Contains("math.max(1, count)", reportLinkedAudioDrop);

            StringAssert.Contains("Volatile.Write(ref s_x001BabelDictionaryStoreSignalPushDropCount, 0);", resetBabelDiagnostics);
            StringAssert.Contains("Volatile.Write(ref s_linkedAudioSignalDropTelemetryCount, 0);", resetBabelDiagnostics);
            StringAssert.Contains("Volatile.Write(ref s_lastLinkedAudioSignalDropTelemetryFrame, -1);", resetBabelDiagnostics);

            StringAssert.Contains("ReadOnlySpan<PlayVoiceOverSignal> playVoiceOverSignals = SignalBus<PlayVoiceOverSignal>.GetFrameSnapshot();", drain);
            StringAssert.Contains("PlayVoiceOverSignal signal = playVoiceOverSignals[i];", drain);
            StringAssert.Contains("TryBuildVocalCueFromPlayVoiceOver(in signal, out VocalCueSignal cue)", drain);
            StringAssert.Contains("bool startedPlayVoiceOverCue = TryStartVocalCue(", drain);
            StringAssert.Contains("TryPublishPlayVoiceOverSubtitleCue(in signal, ref views);", drain);
            StringAssert.Contains("startedCue |= startedPlayVoiceOverCue;", drain);
            StringAssert.Contains("ReadOnlySpan<VocalCueSignal> signals = SignalBus<VocalCueSignal>.GetFrameSnapshot();", drain);
            AssertTextBefore(drain, "ReadOnlySpan<PlayVoiceOverSignal>", "ReadOnlySpan<VocalCueSignal>");
            AssertTextBefore(drain, "bool startedPlayVoiceOverCue = TryStartVocalCue(", "TryPublishPlayVoiceOverSubtitleCue(in signal, ref views);");

            StringAssert.Contains("VocalBankReader.TryFindRecord(bank, bankByteLength, signal.PhraseHashID", tryStart);
            StringAssert.Contains("RecordVocalBankMiss(ref views, signal.PhraseHashID);", tryStart);
            StringAssert.Contains("TryResolveCanonicalVocalWarningFallbackRecord(", tryStart);
            StringAssert.Contains("VocalBankConstants.StateFlagVorbisUnsupported", tryStart);
            StringAssert.Contains("codec.Priority = signal.Priority != 0 ? signal.Priority", tryStart);
            StringAssert.Contains("codec.SpatialGain = ResolveSpatialGain(in signal);", tryStart);

            StringAssert.Contains("ReportVocalBankMiss(requestedPhraseHash);", recordBankMiss);
            StringAssert.Contains("counters.MissCount++;", recordBankMiss);
            StringAssert.Contains("counters.LastFaultFlags = VocalBankConstants.StateFlagBankMiss;", recordBankMiss);
            StringAssert.Contains("counters.LastPhraseHashID = requestedPhraseHash;", recordBankMiss);

            StringAssert.Contains("if (signal.VoiceHash == 0u)", buildFromVoiceOver);
            StringAssert.Contains("ReportPlayVoiceOverSignalMiss(in signal);", buildFromVoiceOver);
            StringAssert.Contains("cue.PhraseHashID = signal.VoiceHash;", buildFromVoiceOver);
            StringAssert.Contains("cue.Priority = DefaultPlayVoiceOverPriority;", buildFromVoiceOver);
            StringAssert.Contains("cue.VolumeScalar = 1f;", buildFromVoiceOver);
            StringAssert.Contains("cue.PlaybackSpeed = 1f;", buildFromVoiceOver);
            StringAssert.Contains("cue.Flags = 0u;", buildFromVoiceOver);
            StringAssert.Contains("_lastPlayVoiceOverTextHash = signal.TextHash;", buildFromVoiceOver);
            StringAssert.Contains("_lastPlayVoiceOverVoiceHash = signal.VoiceHash;", buildFromVoiceOver);
            StringAssert.Contains("_playVoiceOverSignalConsumedCount++;", buildFromVoiceOver);

            StringAssert.Contains("_playVoiceOverSignalMissCount++;", reportMiss);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportMiss);
            StringAssert.Contains("PlayVoiceOverSignalMissWarningHash", reportMiss);
            StringAssert.Contains("signal.TextHash ^ signal.VoiceHash", reportMiss);
            StringAssert.Contains("math.max(1, _playVoiceOverSignalMissCount)", reportMiss);

            StringAssert.Contains("if (signal.TextHash == 0u)", publishSubtitle);
            StringAssert.Contains("subtitle.TokenHash = signal.TextHash;", publishSubtitle);
            StringAssert.Contains("subtitle.SourceHash = PlayVoiceOverSubtitleSourceHash;", publishSubtitle);
            StringAssert.Contains("subtitle.DurationMilliseconds = ResolveActiveVocalSubtitleDurationMilliseconds(ref views);", publishSubtitle);
            StringAssert.Contains("SignalBus<SubtitleCueSignal>.TryPushTracked(in subtitle, ref _playVoiceOverSubtitleCueDropCount)", publishSubtitle);
            StringAssert.Contains("_playVoiceOverSubtitleCuePublishedCount++;", publishSubtitle);
            StringAssert.Contains("ReportPlayVoiceOverSubtitleDrop(in signal);", publishSubtitle);

            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportSubtitleDrop);
            StringAssert.Contains("PlayVoiceOverSubtitleDropWarningHash", reportSubtitleDrop);
            StringAssert.Contains("PlayVoiceOverSubtitleContextHash ^ signal.TextHash ^ signal.VoiceHash", reportSubtitleDrop);
            StringAssert.Contains("math.max(1, _playVoiceOverSubtitleCueDropCount)", reportSubtitleDrop);

            StringAssert.Contains("state.TotalSamples", resolveSubtitleDuration);
            StringAssert.Contains("codec.SampleRate", resolveSubtitleDuration);
            StringAssert.Contains("DefaultPlayVoiceOverSubtitleDurationMilliseconds", resolveSubtitleDuration);
            StringAssert.Contains("ResolveSubtitleDurationMilliseconds(durationSeconds)", resolveSubtitleDuration);

            StringAssert.Contains("_vocalBankMissTelemetryCount++;", reportBankMiss);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportBankMiss);
            StringAssert.Contains("VocalBankMissWarningHash", reportBankMiss);
            StringAssert.Contains("VocalBankMissContextHash ^ requestedPhraseHash", reportBankMiss);
            StringAssert.Contains("math.max(1, _vocalBankMissTelemetryCount)", reportBankMiss);

            StringAssert.Contains("_lastPlayVoiceOverTextHash = 0u;", clearDiagnostics);
            StringAssert.Contains("_lastPlayVoiceOverVoiceHash = 0u;", clearDiagnostics);
            StringAssert.Contains("_playVoiceOverSignalConsumedCount = 0;", clearDiagnostics);
            StringAssert.Contains("_playVoiceOverSignalMissCount = 0;", clearDiagnostics);
            StringAssert.Contains("_vocalBankMissTelemetryCount = 0;", clearDiagnostics);
            StringAssert.Contains("_lastVocalBankMissTelemetryFrame = -1;", clearDiagnostics);
            StringAssert.Contains("_playVoiceOverSubtitleCuePublishedCount = 0;", clearDiagnostics);
            StringAssert.Contains("_playVoiceOverSubtitleCueDropCount = 0;", clearDiagnostics);
            StringAssert.Contains("_lastPlayVoiceOverSubtitleDropTelemetryFrame = -1;", clearDiagnostics);
            StringAssert.Contains("ClearPlayVoiceOverSignalDiagnostics();", unregister);
        }

        private static string ReadProjectFile(params string[] relativeParts)
        {
            string path = Path.Combine(Application.dataPath, "..");
            for (int i = 0; i < relativeParts.Length; i++)
                path = Path.Combine(path, relativeParts[i]);

            return File.ReadAllText(Path.GetFullPath(path));
        }

        private static void AssertTextBefore(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, beforeIndex >= 0 ? beforeIndex : 0, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing token: " + before);
            Assert.Greater(afterIndex, beforeIndex, "Expected token order: " + before + " before " + after);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);

            int bodyStart = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(bodyStart, 0, "Missing method body: " + signature);

            int depth = 0;
            for (int i = bodyStart; i < source.Length; i++)
            {
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(bodyStart, i - bodyStart + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
