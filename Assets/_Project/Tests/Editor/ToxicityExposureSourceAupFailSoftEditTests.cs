using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ToxicityExposureSourceAupFailSoftEditTests
    {
        [Test]
        public void PlayerTargetedToxicityProducersTreatSourceAupAsOptional()
        {
            string traumaDispatcher = ReadProjectFile("Assets/_Project/Scripts/Gameplay/TraumaDispatcher.cs");
            string survivalSystem = ReadProjectFile("Assets/_Project/Scripts/HectonSurvivalSystem.cs");
            string floraInteractionManager = ReadProjectFile("Assets/_Project/Scripts/World/FloraInteractionManager.cs");
            string gasDynamicsSolver = ReadProjectFile("Assets/_Project/Scripts/Atmosphere/GasDynamicsSolver.cs");
            string shinobuMetabolismRuntime = ReadProjectFile("Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs");

            AssertOptionalSourceAup(
                ExtractMethodBody(traumaDispatcher, "private void PublishParasiteSporePoisonStatus(float hazardIntensity, int intervals)"),
                "bool hasSourceAup = _playerMovement != null && _playerMovement.CurrentAup.IsFinite();",
                "if (_playerMovement == null || !_playerMovement.CurrentAup.IsFinite())");

            AssertOptionalSourceAup(
                ExtractMethodBody(survivalSystem, "private void PublishNutritionalToxicityStatus(float severity01, float durationSeconds)"),
                "bool hasSourceAup = TryResolveSurvivalAbsoluteAup(out double3 playerAup);",
                "if (!TryResolveSurvivalAbsoluteAup(out double3 playerAup))");

            AssertOptionalSourceAup(
                ExtractMethodBody(survivalSystem, "private void PublishEnvironmentalToxicityStatus(float toxicity01, float exposureScale, float dt)"),
                "bool hasSourceAup = TryResolveSurvivalAbsoluteAup(out double3 playerAup);",
                "if (!TryResolveSurvivalAbsoluteAup(out double3 playerAup))");

            AssertOptionalSourceAup(
                ExtractMethodBody(floraInteractionManager, "private void PublishToxicSporeToxicityExposure(uint signalEntityId, Vector3 playerPositionWS, float exposure01)"),
                "bool hasSourceAup = TryResolveToxicSporePlayerAup(playerPositionWS, out AbsoluteUniversePosition playerAup);",
                "if (!TryResolveToxicSporePlayerAup(playerPositionWS, out AbsoluteUniversePosition playerAup))");

            AssertOptionalSourceAup(
                ExtractMethodBody(gasDynamicsSolver, "private void PublishActiveRoomToxicitySignal(float deltaTime)"),
                "bool hasSourceAup = TryResolvePlayerAup(out AbsoluteUniversePosition playerAup);",
                "!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)");

            AssertOptionalSourceAup(
                ExtractMethodBody(shinobuMetabolismRuntime, "private void PublishStagedSignals(IDataVault vault, int scheduledCount)"),
                "bool hasSourceAup = math.all(math.isfinite(signal.AUP)) && math.lengthsq(signal.AUP) > 0.000001d;",
                "!math.all(math.isfinite(signal.AUP)) ||");
        }

        private static void AssertOptionalSourceAup(string source, string resolverToken, string forbiddenEarlyReturnToken)
        {
            StringAssert.Contains(resolverToken, source);
            StringAssert.Contains("if (hasSourceAup)", source);
            StringAssert.Contains("Flags = ToxicityExposureSignal.FlagHasSourceAup;", source);
            Assert.That(source, Does.Not.Contain(forbiddenEarlyReturnToken));
            AssertSourceOrder(source, resolverToken, "if (hasSourceAup)");
            AssertSourceOrder(source, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
        }

        private static string ReadProjectFile(string relativePath)
        {
            return File.ReadAllText(Path.Combine(Directory.GetCurrentDirectory(), relativePath));
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
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
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
