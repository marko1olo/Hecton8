using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class VocalWarningToxicityBridgeEditTests
    {
        private const uint VocalBankMagic = 0x42563848u; // H8VB.
        private const uint DefaultMockPhraseHash = 0x05203E88u; // FNV1a("VO_SHINOBU_MOCK").

        [Test]
        public void ToxicityExposureSignal_MapsToCanonicalVocalWarning()
        {
            string contracts = ReadSource("Assets/_Project/Scripts/Core/GlobalRegistryContracts.cs");

            StringAssert.Contains("Toxicity = 6", contracts);
            StringAssert.Contains("public const uint Toxicity = 0x544F5849u; // TOXI", contracts);
            StringAssert.Contains("public const byte LowestCanonicalWarningId = (byte)VocalWarningId.CrushDepth;", contracts);
            StringAssert.Contains("public const byte HighestCanonicalWarningId = (byte)VocalWarningId.Toxicity;", contracts);
            StringAssert.Contains("public const int CanonicalWarningCount = HighestCanonicalWarningId - LowestCanonicalWarningId + 1;", contracts);
            StringAssert.Contains("case Toxicity: return (byte)VocalWarningId.Toxicity;", contracts);
            StringAssert.Contains("case VocalWarningId.Toxicity: return Toxicity;", contracts);
            AssertSourceOrder(contracts, "PowerLow = 5", "Toxicity = 6");
            AssertSourceOrder(contracts, "public const uint PowerLow", "public const uint Toxicity");
            AssertSourceOrder(contracts, "public const uint Toxicity", "public const byte LowestCanonicalWarningId");
        }

        [Test]
        public void VocalWarningSystem_ConsumesToxicityExposureSignalFromSignalBus()
        {
            string source = ReadSource("Assets/_Project/Scripts/Audio/VocalWarningSystem.cs");
            string defaultProfiles = ExtractMethodBody(source, "private static void InitializeDefaultProfiles(");
            string priorityScore = ExtractMethodBody(source, "private static float ResolvePriorityScore(");
            string expiration = ExtractMethodBody(source, "private static float ResolveExpirationSeconds(");
            string toxicitySeverity = ExtractMethodBody(source, "private static float ResolveToxicityWarningSeverity01(");
            string toxicitySourceAup = ExtractMethodBody(source, "private static bool TryResolveToxicitySignalSourceAup(");
            string evaluateJob = ExtractMethodBody(source, "public unsafe void Execute()");
            string refreshCached = ExtractMethodBody(source, "private void RefreshCachedServicesCold()");
            string refreshPlayerTarget = ExtractMethodBody(source, "private void RefreshPlayerToxicityTargetHash(");
            string rebindDataVault = ExtractMethodBody(source, "private void RebindDataVault(");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string serviceRebound = ExtractMethodBody(source, "public void OnGlobalRegistryServiceRebound(");

            StringAssert.Contains("using Hecton8.Atmosphere;", source);
            StringAssert.Contains("private const int WarningStateLength = VocalWarningHashes.CanonicalWarningCount + 1;", source);
            StringAssert.Contains("private const float ToxicityWarningMinSeverity01 = 0.08f;", source);
            StringAssert.Contains("private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;", source);
            StringAssert.Contains("private GameObject _playerToxicityTargetObject;", source);
            StringAssert.Contains("private uint _playerToxicityTargetHash = PlayerToxicityFallbackEntityHash;", source);
            StringAssert.Contains("private int _lastToxicityExposureSnapshotGeneration;", source);
            StringAssert.Contains("private const int LowestCanonicalWarningId = VocalWarningHashes.LowestCanonicalWarningId;", source);
            StringAssert.Contains("private const int HighestCanonicalWarningId = VocalWarningHashes.HighestCanonicalWarningId;", source);
            StringAssert.Contains("private const int CanonicalWarningCount = VocalWarningHashes.CanonicalWarningCount;", source);
            StringAssert.Contains("NativeArray<ToxicityExposureSignal>.ReadOnly toxicitySignals = default;", source);
            StringAssert.Contains("int toxicitySnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", source);
            StringAssert.Contains("if (toxicitySnapshotGeneration != _lastToxicityExposureSnapshotGeneration)", source);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = toxicitySnapshotGeneration;", source);
            StringAssert.Contains("toxicitySignals = SignalBus<ToxicityExposureSignal>.GetFrameSnapshotArray();", source);
            StringAssert.Contains("ToxicitySignals = toxicitySignals,", source);
            StringAssert.Contains("[ReadOnly, NoAlias] public NativeArray<ToxicityExposureSignal>.ReadOnly ToxicitySignals;", source);
            StringAssert.Contains("PlayerToxicityTargetHash = _playerToxicityTargetHash != 0u ? _playerToxicityTargetHash : PlayerToxicityFallbackEntityHash,", source);
            StringAssert.Contains("public uint PlayerToxicityTargetHash;", source);
            StringAssert.Contains("for (int i = 0; i < ToxicitySignals.Length && evaluations < MaxEvaluations; i++)", source);
            StringAssert.Contains("ToxicityExposureSignal signal = ToxicitySignals[i];", source);
            StringAssert.Contains("if (signal.EntityId == 0u)", source);
            StringAssert.Contains("uint playerToxicityTargetHash = PlayerToxicityTargetHash != 0u ? PlayerToxicityTargetHash : PlayerToxicityFallbackEntityHash;", source);
            StringAssert.Contains("if (signal.EntityId != playerToxicityTargetHash && signal.EntityId != PlayerToxicityFallbackEntityHash)", source);
            StringAssert.Contains("float severity = ResolveToxicityWarningSeverity01(in signal);", source);
            StringAssert.Contains("if (severity <= ToxicityWarningMinSeverity01)", source);
            StringAssert.Contains("AbsoluteUniversePosition sourceAup = default;", source);
            StringAssert.Contains("ushort direction = TryResolveToxicitySignalSourceAup(in signal, out sourceAup)", source);
            StringAssert.Contains("TryQueue(VocalWarningHashes.Toxicity, (byte)VocalWarningId.Toxicity, severity", source);
            AssertSourceOrder(source, "int toxicitySnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", "toxicitySignals = SignalBus<ToxicityExposureSignal>.GetFrameSnapshotArray();");
            AssertSourceOrder(source, "toxicitySignals = SignalBus<ToxicityExposureSignal>.GetFrameSnapshotArray();", "ToxicitySignals = toxicitySignals,");
            AssertSourceOrder(source, "RadiationSignals = SignalBus<RadiationDoseSignal>.GetFrameSnapshotArray(),", "ToxicitySignals = toxicitySignals,");
            AssertSourceOrder(source, "for (int i = 0; i < RadiationSignals.Length", "for (int i = 0; i < ToxicitySignals.Length");
            AssertSourceOrder(source, "for (int i = 0; i < ToxicitySignals.Length", "for (int i = 0; i < BatterySignals.Length");
            AssertSourceOrder(source, "if (signal.EntityId != playerToxicityTargetHash && signal.EntityId != PlayerToxicityFallbackEntityHash)", "float severity = ResolveToxicityWarningSeverity01(in signal);");
            AssertSourceOrder(source, "RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);", "PlayerToxicityTargetHash = _playerToxicityTargetHash != 0u ? _playerToxicityTargetHash : PlayerToxicityFallbackEntityHash,");

            StringAssert.Contains("RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);", refreshCached);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", refreshCached);
            StringAssert.Contains("GameObject playerObject = playerContext != null ? playerContext.PlayerObject : null;", refreshPlayerTarget);
            StringAssert.Contains("playerObject = BootstrapState.CurrentPlayerObject;", refreshPlayerTarget);
            StringAssert.Contains("ReferenceEquals(playerObject, _playerToxicityTargetObject)", refreshPlayerTarget);
            StringAssert.Contains("_playerToxicityTargetObject = playerObject;", refreshPlayerTarget);
            StringAssert.Contains("EntityId.ToULong(playerObject.GetEntityId())", refreshPlayerTarget);
            StringAssert.Contains("_playerToxicityTargetHash = targetHash != 0u ? targetHash : PlayerToxicityFallbackEntityHash;", refreshPlayerTarget);
            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.Player)", serviceReplaced);
            StringAssert.Contains("RefreshPlayerToxicityTargetHash(currentService as IPlayerRuntimeContext);", serviceReplaced);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", serviceReplaced);
            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.Player)", serviceRebound);
            StringAssert.Contains("RefreshPlayerToxicityTargetHash(currentService as IPlayerRuntimeContext);", serviceRebound);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", serviceRebound);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", rebindDataVault);

            StringAssert.Contains("(byte)VocalWarningId.Toxicity", defaultProfiles);
            StringAssert.Contains("VocalWarningHashes.Toxicity", defaultProfiles);
            StringAssert.Contains("360f", defaultProfiles);
            StringAssert.Contains("4.5f", defaultProfiles);

            StringAssert.Contains("case VocalWarningHashes.Toxicity:", priorityScore);
            StringAssert.Contains("resolved.BasePriorityRadiation * 0.65f", priorityScore);
            StringAssert.Contains("case VocalWarningHashes.Toxicity:", expiration);
            StringAssert.Contains("math.lerp(4f, 7.5f, severity)", expiration);

            StringAssert.Contains("math.select(0f, signal.Exposure01, math.isfinite(signal.Exposure01))", toxicitySeverity);
            StringAssert.Contains("math.select(0f, signal.ToxemiaDelta, math.isfinite(signal.ToxemiaDelta))", toxicitySeverity);
            StringAssert.Contains("return math.max(exposure, toxemia);", toxicitySeverity);

            StringAssert.Contains("(signal.Flags & ToxicityExposureSignal.FlagHasSourceAup) == 0", toxicitySourceAup);
            StringAssert.Contains("math.lengthsq(signal.AUP) <= 0.000001d", toxicitySourceAup);
            StringAssert.Contains("sourceAup = AbsoluteUniversePosition.FromAbsolutePosition(signal.AUP);", toxicitySourceAup);
            StringAssert.Contains("return AbsoluteUniversePosition.IsFinite(in sourceAup);", toxicitySourceAup);
            AssertSourceOrder(toxicitySourceAup, "ToxicityExposureSignal.FlagHasSourceAup", "AbsoluteUniversePosition.FromAbsolutePosition(signal.AUP)");

            StringAssert.Contains("uint playerToxicityTargetHash = PlayerToxicityTargetHash != 0u ? PlayerToxicityTargetHash : PlayerToxicityFallbackEntityHash;", evaluateJob);
            StringAssert.Contains("if (signal.EntityId != playerToxicityTargetHash && signal.EntityId != PlayerToxicityFallbackEntityHash)", evaluateJob);
            StringAssert.Contains("float severity = ResolveToxicityWarningSeverity01(in signal);", evaluateJob);
            StringAssert.Contains("AbsoluteUniversePosition sourceAup = default;", evaluateJob);
            StringAssert.Contains(": (ushort)0;", evaluateJob);
            StringAssert.Contains("TryQueue(VocalWarningHashes.Toxicity, (byte)VocalWarningId.Toxicity, severity", evaluateJob);
            AssertSourceOrder(evaluateJob, "if (signal.EntityId != playerToxicityTargetHash && signal.EntityId != PlayerToxicityFallbackEntityHash)", "float severity = ResolveToxicityWarningSeverity01(in signal);");
            AssertSourceOrder(evaluateJob, "float severity = ResolveToxicityWarningSeverity01(in signal);", "AbsoluteUniversePosition sourceAup = default;");
            AssertSourceOrder(evaluateJob, "AbsoluteUniversePosition sourceAup = default;", "TryQueue(VocalWarningHashes.Toxicity, (byte)VocalWarningId.Toxicity, severity");
            Assert.That(evaluateJob, Does.Not.Contain("if ((signal.Flags & ToxicityExposureSignal.FlagHasSourceAup) == 0)"));
        }

        [Test]
        public void AudioTooling_KnowsToxicityWarningRoute()
        {
            string musicDirector = ReadSource("Assets/_Project/Scripts/Audio/HectonMusicDirector.cs");
            string stormTorture = ReadSource("Assets/_Project/Scripts/Audio/Editor/VocalWarningStormTorture_X_011.cs");
            string tuner = ReadSource("Assets/_Project/Scripts/Audio/Editor/VocalWarningQueueTunerWindow.cs");

            string warningDuck = ExtractMethodBody(musicDirector, "private static float ResolveVocalWarningMusicDuck01(");
            StringAssert.Contains("case VocalWarningId.Toxicity:", warningDuck);
            AssertSourceOrder(warningDuck, "case VocalWarningId.PowerLow:", "case VocalWarningId.Toxicity:");
            AssertSourceOrder(warningDuck, "case VocalWarningId.Toxicity:", "return VocalWarningMusicDuckDefault01;");

            StringAssert.Contains("using Hecton8.Core;", stormTorture);
            StringAssert.Contains("private const int CanonicalWarningCount = VocalWarningHashes.CanonicalWarningCount;", stormTorture);
            StringAssert.Contains("byte warningId = (byte)((i % CanonicalWarningCount) + 1);", stormTorture);
            StringAssert.Contains("CountBits64(activeAlarmsMask) == CanonicalWarningCount", stormTorture);
            StringAssert.Contains("slots[CanonicalWarningCount - 1].WarningId == CanonicalWarningCount", stormTorture);
            StringAssert.Contains("warningId >= 1 && warningId <= CanonicalWarningCount", stormTorture);

            StringAssert.Contains("InjectWarning(VocalWarningId.Toxicity, 0.55f)", tuner);
            StringAssert.Contains("text = \"Inject Toxicity\"", tuner);
        }

        [Test]
        public void SubmarineOsVoiceAlarmBridge_AcceptsFullCanonicalWarningRange()
        {
            string submarineOs = ReadSource("Assets/_Project/Scripts/Gameplay/HectonSubmarineOS.cs");
            string queueVoiceAlarm = ExtractMethodBody(submarineOs, "private void QueueVoiceAlarm(");

            StringAssert.Contains("warningId >= (byte)VocalWarningId.CrushDepth", queueVoiceAlarm);
            StringAssert.Contains("warningId <= (byte)VocalWarningId.Toxicity", queueVoiceAlarm);
            StringAssert.Contains("VocalWarningHashes.FromWarningId(normalizedWarningId)", queueVoiceAlarm);
            string stalePowerLowUpperBound = "warningId <= (byte)" + "VocalWarningId.PowerLow";
            Assert.That(queueVoiceAlarm, Does.Not.Contain(stalePowerLowUpperBound));
        }

        [Test]
        public void VocalBankPlaybackRuntime_FallsBackCanonicalWarningsToLoadedMockPhrase()
        {
            string playback = ReadSource("Assets/_Project/Scripts/Audio/Synthesis/VocalBankPlaybackRuntime.cs");
            string drainCueSignals = ExtractMethodBody(playback, "private void DrainVocalCueSignals(");
            string fallback = ExtractMethodBody(playback, "private static bool TryResolveCanonicalVocalWarningFallbackRecord(");
            string canonicalHashSwitch = ExtractMethodBody(playback, "private static bool IsCanonicalVocalWarningPhraseHash(");
            string recordMiss = ExtractMethodBody(playback, "private static void RecordVocalBankMiss(");

            StringAssert.Contains("private const uint DefaultMockPhraseHash = 0x05203E88u; // FNV1a(\"VO_SHINOBU_MOCK\").", playback);
            StringAssert.Contains("uint playbackPhraseHash = signal.PhraseHashID;", drainCueSignals);
            StringAssert.Contains("bool usedCanonicalFallback = false;", drainCueSignals);
            StringAssert.Contains("RecordVocalBankMiss(ref views, signal.PhraseHashID);", drainCueSignals);
            StringAssert.Contains("TryResolveCanonicalVocalWarningFallbackRecord(", drainCueSignals);
            StringAssert.Contains("out playbackPhraseHash", drainCueSignals);
            StringAssert.Contains("usedCanonicalFallback = true;", drainCueSignals);
            StringAssert.Contains("TryFindMetadata(playbackPhraseHash", drainCueSignals);
            StringAssert.Contains("next.PhraseHashID = signal.PhraseHashID;", drainCueSignals);
            StringAssert.Contains("if (usedCanonicalFallback)", drainCueSignals);
            StringAssert.Contains("nextFlags |= VocalBankConstants.StateFlagBankMiss;", drainCueSignals);
            StringAssert.Contains("codec.FaultFlags = usedCanonicalFallback ? VocalBankConstants.StateFlagBankMiss : 0u;", drainCueSignals);
            AssertSourceOrder(drainCueSignals, "RecordVocalBankMiss(ref views, signal.PhraseHashID);", "TryResolveCanonicalVocalWarningFallbackRecord(");
            AssertSourceOrder(drainCueSignals, "uint playbackPhraseHash = signal.PhraseHashID;", "next.PhraseHashID = signal.PhraseHashID;");
            AssertSourceOrder(drainCueSignals, "usedCanonicalFallback = true;", "nextFlags |= VocalBankConstants.StateFlagBankMiss;");
            string staleFallbackStateHash = "next.PhraseHashID = " + "playbackPhraseHash;";
            Assert.That(drainCueSignals, Does.Not.Contain(staleFallbackStateHash));

            StringAssert.Contains("!IsCanonicalVocalWarningPhraseHash(requestedPhraseHash)", fallback);
            StringAssert.Contains("requestedPhraseHash == DefaultMockPhraseHash", fallback);
            StringAssert.Contains("VocalBankReader.TryFindRecord(bank, bankByteLength, DefaultMockPhraseHash, out record)", fallback);
            StringAssert.Contains("playbackPhraseHash = DefaultMockPhraseHash;", fallback);
            AssertSourceOrder(fallback, "requestedPhraseHash == DefaultMockPhraseHash", "playbackPhraseHash = DefaultMockPhraseHash;");

            StringAssert.Contains("case VocalWarningHashes.Radiation:", canonicalHashSwitch);
            StringAssert.Contains("case VocalWarningHashes.PowerLow:", canonicalHashSwitch);
            StringAssert.Contains("case VocalWarningHashes.Toxicity:", canonicalHashSwitch);
            AssertSourceOrder(canonicalHashSwitch, "case VocalWarningHashes.PowerLow:", "case VocalWarningHashes.Toxicity:");

            StringAssert.Contains("counters.MissCount++;", recordMiss);
            StringAssert.Contains("counters.LastFaultFlags = VocalBankConstants.StateFlagBankMiss;", recordMiss);
            StringAssert.Contains("counters.LastPhraseHashID = requestedPhraseHash;", recordMiss);
        }

        [Test]
        public void VocalBankAsset_ContainsPlayableMockFallbackRecord()
        {
            string bankPath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/StreamingAssets/Hecton8/Audio/vocal_banks.h8bin");
            Assert.That(File.Exists(bankPath), Is.True, "Missing vocal fallback bank asset.");

            byte[] bytes = File.ReadAllBytes(bankPath);
            Assert.GreaterOrEqual(bytes.Length, 96, "Vocal bank must contain a 64-byte header and at least one 32-byte record.");

            uint magic = ReadUInt32(bytes, 0);
            uint headerSize = ReadUInt32(bytes, 8);
            uint recordSize = ReadUInt32(bytes, 12);
            uint recordCount = ReadUInt32(bytes, 16);
            ulong payloadOffset = ReadUInt64(bytes, 24);
            ulong payloadBytes = ReadUInt64(bytes, 32);

            Assert.AreEqual(VocalBankMagic, magic);
            Assert.AreEqual(64u, headerSize);
            Assert.AreEqual(32u, recordSize);
            Assert.Greater(recordCount, 0u);
            ulong indexBytes = (ulong)recordCount * recordSize;
            Assert.LessOrEqual(headerSize + indexBytes, payloadOffset);
            Assert.LessOrEqual(headerSize + indexBytes, (ulong)bytes.Length);
            Assert.LessOrEqual(payloadOffset, (ulong)bytes.Length);
            Assert.LessOrEqual(payloadBytes, (ulong)bytes.Length - payloadOffset);

            bool found = false;
            for (uint i = 0; i < recordCount; i++)
            {
                int offset = checked((int)(headerSize + (i * recordSize)));
                Assert.LessOrEqual(offset + 32, bytes.Length);

                uint hash = ReadUInt32(bytes, offset);
                if (hash != DefaultMockPhraseHash)
                    continue;

                uint byteLength = ReadUInt32(bytes, offset + 4);
                ulong byteOffset = ReadUInt64(bytes, offset + 8);
                uint totalSamples = ReadUInt32(bytes, offset + 16);
                uint sampleRate = ReadUInt32(bytes, offset + 20);
                byte codec = bytes[offset + 24];
                byte channels = bytes[offset + 25];

                Assert.Greater(byteLength, 0u);
                Assert.Greater(totalSamples, 0u);
                Assert.GreaterOrEqual(sampleRate, 8000u);
                Assert.GreaterOrEqual(channels, 1);
                Assert.That(codec, Is.Not.EqualTo((byte)2), "Mock fallback cannot use unsupported Vorbis codec.");
                Assert.GreaterOrEqual(byteOffset, payloadOffset);
                Assert.LessOrEqual(byteOffset + byteLength, payloadOffset + payloadBytes);
                Assert.LessOrEqual(byteOffset + byteLength, (ulong)bytes.Length);
                found = true;
                break;
            }

            Assert.That(found, Is.True, "Missing VO_SHINOBU_MOCK fallback phrase record.");
        }

        [Test]
        public void SubtitleManager_ShowsFallbackTextForCanonicalVocalWarningTokens()
        {
            string subtitleManager = ReadSource("Assets/_Project/Scripts/UI/SubtitleManager.cs");
            string displaySubtitleResolved = ExtractMethodBody(subtitleManager, "private bool DisplaySubtitleResolved(");
            string showSubtitleCommand = ExtractMethodBody(subtitleManager, "private bool ShowSubtitleCommand(");
            string fallback = ExtractMethodBody(subtitleManager, "private static bool TryResolveVocalWarningFallbackSubtitle(");

            StringAssert.Contains("TryResolveVocalWarningFallbackSubtitle(textHash, out ReadOnlySpan<char> vocalWarningFallback)", displaySubtitleResolved);
            StringAssert.Contains("EnqueueBuffered(vocalWarningFallback, duration, SubtitleSource.Generic, false)", displaySubtitleResolved);
            StringAssert.Contains("if (!found && allowFallback && fallback.Length > 0)", displaySubtitleResolved);
            StringAssert.Contains("length = CopyFallbackSpanToBabelLease(textHash, fallback, lease.Span);", displaySubtitleResolved);
            StringAssert.Contains("else if (!found && TryResolveVocalWarningFallbackSubtitle(textHash, out ReadOnlySpan<char> vocalWarningFallback))", displaySubtitleResolved);
            StringAssert.Contains("length = CopyFallbackSpanToBabelLease(textHash, vocalWarningFallback, lease.Span);", displaySubtitleResolved);
            StringAssert.Contains("BabelSubtitleSyncRuntime.RecordDecode(textHash, length, !found, decodeMs);", displaySubtitleResolved);
            AssertSourceOrder(displaySubtitleResolved, "length = CopyFallbackSpanToBabelLease(textHash, fallback, lease.Span);", "length = CopyFallbackSpanToBabelLease(textHash, vocalWarningFallback, lease.Span);");
            AssertSourceOrder(displaySubtitleResolved, "length = CopyFallbackSpanToBabelLease(textHash, vocalWarningFallback, lease.Span);", "BabelSubtitleSyncRuntime.RecordDecode(textHash");

            StringAssert.Contains("if (!found && TryResolveVocalWarningFallbackSubtitle(command.TextHash, out ReadOnlySpan<char> fallback))", showSubtitleCommand);
            StringAssert.Contains("textLength = CopyFallbackSpanToBabelLease(command.TextHash, fallback, textDestination);", showSubtitleCommand);
            StringAssert.Contains("BabelSubtitleSyncRuntime.RecordDecode(command.TextHash, textLength, !found, decodeMs);", showSubtitleCommand);
            AssertSourceOrder(showSubtitleCommand, "TryResolveVocalWarningFallbackSubtitle(command.TextHash", "BabelSubtitleSyncRuntime.RecordDecode(command.TextHash");
            AssertSourceOrder(showSubtitleCommand, "BabelSubtitleSyncRuntime.RecordDecode(command.TextHash", "if (textLength <= 0)");

            StringAssert.Contains("case VocalWarningHashes.CrushDepth:", fallback);
            StringAssert.Contains("case VocalWarningHashes.HullBreach:", fallback);
            StringAssert.Contains("case VocalWarningHashes.HullTempCritical:", fallback);
            StringAssert.Contains("case VocalWarningHashes.OxygenLow:", fallback);
            StringAssert.Contains("case VocalWarningHashes.Radiation:", fallback);
            StringAssert.Contains("case VocalWarningHashes.PowerLow:", fallback);
            StringAssert.Contains("case VocalWarningHashes.Toxicity:", fallback);
            StringAssert.Contains("fallback = \"TOXIC EXPOSURE\".AsSpan();", fallback);
            AssertSourceOrder(fallback, "case VocalWarningHashes.PowerLow:", "case VocalWarningHashes.Toxicity:");
        }

        private static string ReadSource(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
        }

        private static uint ReadUInt32(byte[] bytes, int offset)
        {
            return BitConverter.ToUInt32(bytes, offset);
        }

        private static ulong ReadUInt64(byte[] bytes, int offset)
        {
            return BitConverter.ToUInt64(bytes, offset);
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
