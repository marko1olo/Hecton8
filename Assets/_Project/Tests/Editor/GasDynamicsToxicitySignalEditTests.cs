using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class GasDynamicsToxicitySignalEditTests
    {
        [Test]
        public void GasDynamicsSolver_PublishesBoundedToxicityExposureSignal()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs"));
            string configureBody = ExtractMethodBody(source, "private static void ConfigureColdSignalLanes()");
            string publishBody = ExtractMethodBody(source, "private void PublishActiveRoomToxicitySignal(");
            string targetBody = ExtractMethodBody(source, "private static uint ResolvePlayerToxicitySignalEntityId()");
            string clearBody = ExtractMethodBody(source, "private void PublishToxicityClearSignalIfNeeded(");
            string resetBody = ExtractMethodBody(source, "private void DisposeNativeStateDeferred()");

            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.Configure(", configureBody);
            StringAssert.Contains("ToxicityExposureSignal.ExpectedCapacity", configureBody);
            StringAssert.Contains("ToxicityExposureSignal.MaxFrameSignals", configureBody);
            StringAssert.Contains("ToxicityExposureSignal.LowTierFrameSignals", configureBody);
            StringAssert.Contains("ToxicityExposureSignal.LaneHash", configureBody);
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.EnsureInitialized();", configureBody);
            StringAssert.Contains("private const uint PlayerTargetHash = ToxicityExposureSignal.PlayerEntityFallbackHash;", source);

            StringAssert.Contains("int roomId = _activePlayerRoom;", publishBody);
            StringAssert.Contains("uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;", publishBody);
            StringAssert.Contains("!TryGetRoomSnapshot(roomId, out GasRoomSnapshot snapshot)", publishBody);
            StringAssert.Contains("PublishToxicityClearSignalIfNeeded(roomId, 0f, 0f, frame);", publishBody);
            StringAssert.Contains("float toxicity01 = math.saturate(FiniteNonNegativeOrZero(snapshot.Toxicity01));", publishBody);
            StringAssert.Contains("float narcosis01 = math.saturate(FiniteNonNegativeOrZero(snapshot.Narcosis01));", publishBody);
            StringAssert.Contains("float carbonDioxideKPa = FiniteNonNegativeOrZero(snapshot.CarbonDioxideKPa);", publishBody);
            StringAssert.Contains("float pressureAtm = FiniteNonNegativeOrZero(snapshot.PressureKPa) * math.rcp(KPaPerAtmosphere);", publishBody);
            StringAssert.Contains("if (toxicity01 <= ToxicitySignalEpsilon && narcosis01 <= ToxicitySignalEpsilon)", publishBody);
            StringAssert.Contains("PublishToxicityClearSignalIfNeeded(roomId, carbonDioxideKPa, pressureAtm, frame);", publishBody);
            StringAssert.Contains("return;", publishBody);
            StringAssert.Contains("_latestToxicitySignal = new ToxicitySignal(", publishBody);
            StringAssert.Contains("AdvanceToxicitySignalSequence();", publishBody);
            StringAssert.Contains("!SignalBus<ToxicityExposureSignal>.HasNativeStorage", publishBody);
            StringAssert.Contains("bool hasSourceAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);", publishBody);
            StringAssert.Contains("if (hasSourceAup)", publishBody);
            Assert.That(publishBody, Does.Not.Contain("!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)"));
            StringAssert.Contains("ToxicityExposureSignal exposure = default;", publishBody);
            StringAssert.Contains("exposure.AUP = playerAup.ToAbsoluteDouble3();", publishBody);
            StringAssert.Contains("exposure.Exposure01 = toxicity01;", publishBody);
            StringAssert.Contains("float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);", publishBody);
            StringAssert.Contains("exposure.ToxemiaDelta = math.saturate(toxicity01 * safeDeltaTime * ToxicityExposureDeltaScalePerSecond);", publishBody);
            StringAssert.Contains("exposure.EntityId = ResolvePlayerToxicitySignalEntityId();", publishBody);
            StringAssert.Contains("exposure.ChemicalHash = GasCarbonDioxideChemicalHash;", publishBody);
            StringAssert.Contains("exposure.Frame = frame;", publishBody);
            StringAssert.Contains("exposure.Flags = ToxicityExposureSignal.FlagHasSourceAup;", publishBody);
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.TryPushTracked(in exposure, ref _toxicityExposureSignalDropCount);", publishBody);
            AssertSourceOrder(publishBody, "uint frame = Hecton8.Core.SystemDispatcher.CurrentFrameId;", "!TryGetRoomSnapshot(roomId, out GasRoomSnapshot snapshot)");
            AssertSourceOrder(publishBody, "PublishToxicityClearSignalIfNeeded(roomId, 0f, 0f, frame);", "float toxicity01 = math.saturate");
            AssertSourceOrder(publishBody, "PublishToxicityClearSignalIfNeeded(roomId, carbonDioxideKPa, pressureAtm, frame);", "ushort flags =");
            AssertSourceOrder(publishBody, "float pressureAtm = FiniteNonNegativeOrZero(snapshot.PressureKPa) * math.rcp(KPaPerAtmosphere);", "_latestToxicitySignal = new ToxicitySignal(");
            AssertSourceOrder(publishBody, "_latestToxicitySignal = new ToxicitySignal(", "AdvanceToxicitySignalSequence();");
            AssertSourceOrder(publishBody, "bool hasSourceAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);", "ToxicityExposureSignal exposure = default;");
            AssertSourceOrder(publishBody, "AdvanceToxicitySignalSequence();", "ToxicityExposureSignal exposure = default;");
            AssertSourceOrder(publishBody, "float safeDeltaTime = FiniteNonNegativeOrZero(deltaTime);", "exposure.ToxemiaDelta");
            AssertSourceOrder(publishBody, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            AssertSourceOrder(publishBody, "exposure.ToxemiaDelta", "SignalBus<ToxicityExposureSignal>.TryPushTracked");

            StringAssert.Contains("IPlayerRuntimeContext playerContext = GlobalRegistry.Player;", targetBody);
            StringAssert.Contains("playerObject = playerContext.PlayerObject;", targetBody);
            StringAssert.Contains("playerObject = BootstrapState.CurrentPlayerObject;", targetBody);
            StringAssert.Contains("EntityId.ToULong(playerObject.GetEntityId())", targetBody);
            StringAssert.Contains("return entityHash != 0u ? entityHash : PlayerTargetHash;", targetBody);

            StringAssert.Contains("ToxicitySignal previous = _latestToxicitySignal;", clearBody);
            StringAssert.Contains("_latestToxicitySignalSequence != 0", clearBody);
            StringAssert.Contains("previous.RoomId == roomId", clearBody);
            StringAssert.Contains("previous.Toxicity01 <= ToxicitySignalEpsilon", clearBody);
            StringAssert.Contains("previous.Narcosis01 <= ToxicitySignalEpsilon", clearBody);
            StringAssert.Contains("previous.Flags == 0", clearBody);
            StringAssert.Contains("if (_latestToxicitySignalSequence == 0)", clearBody);
            StringAssert.Contains("_latestToxicitySignal = new ToxicitySignal(", clearBody);
            StringAssert.Contains("carbonDioxideKPa", clearBody);
            StringAssert.Contains("pressureAtm", clearBody);
            StringAssert.Contains("0f,", clearBody);
            StringAssert.Contains("0);", clearBody);
            StringAssert.Contains("AdvanceToxicitySignalSequence();", clearBody);

            StringAssert.Contains("_latestToxicitySignal = default;", resetBody);
            StringAssert.Contains("_latestToxicitySignalSequence = 0;", resetBody);
            StringAssert.Contains("_toxicitySignalReadSequence = 0;", resetBody);
            StringAssert.Contains("_toxicityExposureSignalDropCount = 0;", resetBody);
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
