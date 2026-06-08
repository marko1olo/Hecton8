using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ShinobuPhysiologyToxicitySignalEditTests
    {
        [Test]
        public void ShinobuPhysiology_IngestsToxicityExposureSignalsAsBoundedPlayerToxemia()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs"));
            string constants = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Physiology/ShinobuPhysiologyData.cs"));
            string onEnableBody = ExtractMethodBody(source, "private void OnEnable()");
            string scheduleBody = ExtractMethodBody(source, "private void SchedulePhysiologyTick(");
            string refreshTargetBody = ExtractMethodBody(source, "private uint RefreshPlayerToxicityTargetHash()");
            string ingestBody = ExtractMethodBody(source, "private static void IngestAtmosphereToxicitySignals(");

            StringAssert.Contains("using Hecton8.Atmosphere;", constants);
            StringAssert.Contains("public const uint PlayerTargetHash = ToxicityExposureSignal.PlayerEntityFallbackHash;", constants);
            Assert.That(constants, Does.Not.Contain("public const uint PlayerTargetHash = 0x504C5952u;"));
            Assert.That(source, Does.Not.Contain("private const uint ToxicityExposureLaneHash"));
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.Configure(", onEnableBody);
            StringAssert.Contains("private const float ToxicityExposureFallbackDeltaScalePerSecond = 0.08f;", source);
            StringAssert.Contains("ToxicityExposureSignal.ExpectedCapacity", onEnableBody);
            StringAssert.Contains("maxFrameSignals: ToxicityExposureSignal.MaxFrameSignals", onEnableBody);
            StringAssert.Contains("lowTierFrameSignals: ToxicityExposureSignal.LowTierFrameSignals", onEnableBody);
            StringAssert.Contains("laneHash: ToxicityExposureSignal.LaneHash", onEnableBody);
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.EnsureInitialized();", onEnableBody);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", onEnableBody);

            StringAssert.Contains("uint playerTargetHash = RefreshPlayerToxicityTargetHash();", scheduleBody);
            StringAssert.Contains("private int _lastToxicityExposureSnapshotGeneration;", source);
            StringAssert.Contains("int toxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", scheduleBody);
            StringAssert.Contains("if (toxicityExposureSnapshotGeneration != _lastToxicityExposureSnapshotGeneration)", scheduleBody);
            StringAssert.Contains("IngestAtmosphereToxicitySignals(toxemia, playerTargetHash, frame, deltaTime);", scheduleBody);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = toxicityExposureSnapshotGeneration;", scheduleBody);
            AssertSourceOrder(scheduleBody, "uint playerTargetHash = RefreshPlayerToxicityTargetHash();", "int toxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;");
            AssertSourceOrder(scheduleBody, "if (toxicityExposureSnapshotGeneration != _lastToxicityExposureSnapshotGeneration)", "IngestAtmosphereToxicitySignals(toxemia, playerTargetHash, frame, deltaTime);");
            AssertSourceOrder(scheduleBody, "IngestAtmosphereToxicitySignals(toxemia, playerTargetHash, frame, deltaTime);", "_lastToxicityExposureSnapshotGeneration = toxicityExposureSnapshotGeneration;");

            StringAssert.Contains("GameObject playerObject = player != null ? player.PlayerObject : null;", refreshTargetBody);
            StringAssert.Contains("EntityId.ToULong(playerObject.GetEntityId())", refreshTargetBody);
            StringAssert.Contains("_playerToxicityTargetHash = entityHash;", refreshTargetBody);
            StringAssert.Contains("return _playerToxicityTargetHash != 0u", refreshTargetBody);
            StringAssert.Contains(": ShinobuPhysiologyConstants.PlayerTargetHash;", refreshTargetBody);
            Assert.That(source, Does.Not.Contain("RefreshPlayerCombatTargetHash"));
            Assert.That(source, Does.Not.Contain("_playerDamageTargetHash"));

            StringAssert.Contains("if (!toxemia.IsCreated || toxemia.Length <= 0)", ingestBody);
            StringAssert.Contains("ReadOnlySpan<ToxicityExposureSignal> signals = SignalBus<ToxicityExposureSignal>.GetFrameSnapshot();", ingestBody);
            StringAssert.Contains("if (signals.Length <= 0)", ingestBody);
            StringAssert.Contains("ToxicityExposureSignal signal = signals[i];", ingestBody);
            StringAssert.Contains("float safeDeltaTime = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(deltaTime, 0f));", ingestBody);
            StringAssert.Contains("uint entityId = signal.EntityId;", ingestBody);
            StringAssert.Contains("if (entityId == 0u)", ingestBody);
            StringAssert.Contains("if (entityId != playerTargetHash && entityId != ShinobuPhysiologyConstants.PlayerTargetHash)", ingestBody);
            StringAssert.Contains("float exposure = ShinobuPhysiologyJobMath.SanitizeUnit(signal.Exposure01);", ingestBody);
            StringAssert.Contains("float explicitDelta = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(signal.ToxemiaDelta, 0f));", ingestBody);
            StringAssert.Contains("float fallbackDelta = exposure * safeDeltaTime * ToxicityExposureFallbackDeltaScalePerSecond;", ingestBody);
            StringAssert.Contains("float delta = math.saturate(explicitDelta > 0f ? explicitDelta : fallbackDelta);", ingestBody);
            StringAssert.Contains("if (exposure <= 0.0001f && delta <= 0f)", ingestBody);
            StringAssert.Contains("toxemiaDelta = math.saturate(toxemiaDelta + delta);", ingestBody);
            AssertSourceOrder(ingestBody, "float safeDeltaTime = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(deltaTime, 0f));", "float fallbackDelta = exposure * safeDeltaTime * ToxicityExposureFallbackDeltaScalePerSecond;");
            AssertSourceOrder(ingestBody, "float explicitDelta = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(signal.ToxemiaDelta, 0f));", "float delta = math.saturate(explicitDelta > 0f ? explicitDelta : fallbackDelta);");
            StringAssert.Contains("MockToxemiaSignal pending = toxemia[0];", ingestBody);
            StringAssert.Contains("if ((pending.Flags & 2u) != 0u)", ingestBody);
            StringAssert.Contains("pending.Absolute01 = math.saturate(", ingestBody);
            StringAssert.Contains("ShinobuPhysiologyJobMath.SanitizeUnit(pending.Absolute01) + toxemiaDelta", ingestBody);
            StringAssert.Contains("pending.Flags |= 3u;", ingestBody);
            StringAssert.Contains("pending.Delta01 = math.saturate(", ingestBody);
            StringAssert.Contains("ShinobuPhysiologyJobMath.SanitizeFinite(pending.Delta01, 0f)) + toxemiaDelta", ingestBody);
            StringAssert.Contains("pending.Flags = 1u;", ingestBody);
            StringAssert.Contains("pending.Frame = frame;", ingestBody);
            StringAssert.Contains("toxemia[0] = pending;", ingestBody);
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
