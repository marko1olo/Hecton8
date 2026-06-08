using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class HazardProducerSignalDropResetEditTests
    {
        [Test]
        public void HazardToxicityProducersResetSignalDropCountersOnSubsystemRegistration()
        {
            string hazardZoneManager = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs");
            string environmentalHazard = ReadProjectFile("Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs");
            string hazardZoneReset = ExtractMethodBody(hazardZoneManager, "private static void ResetStaticRuntimeState()");
            string environmentalReset = ExtractMethodBody(environmentalHazard, "private static void ResetStaticState()");

            StringAssert.Contains("using System.Threading;", hazardZoneManager);
            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]", hazardZoneManager);
            StringAssert.Contains("Volatile.Write(ref s_x001HazardZoneManagerSignalPushDropCount, 0);", hazardZoneReset);

            StringAssert.Contains("using System.Threading;", environmentalHazard);
            StringAssert.Contains("[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]", environmentalHazard);
            StringAssert.Contains("PlayerLayerIndex = -1;", environmentalReset);
            StringAssert.Contains("Volatile.Write(ref s_x001EnvironmentalHazardSignalPushDropCount, 0);", environmentalReset);
            AssertSourceOrder(environmentalReset, "PlayerLayerIndex = -1;", "Volatile.Write(ref s_x001EnvironmentalHazardSignalPushDropCount, 0);");
        }

        [Test]
        public void EnvironmentalHazardUnregistersRadiationSourceWhenTypeStopsBeingRadiation()
        {
            string environmentalHazard = ReadProjectFile("Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs");
            string registerBody = ExtractMethodBody(environmentalHazard, "private void TryRegisterRadiationSource()");

            StringAssert.Contains("hazardType != HazardType.Radiation || _cachedTransform == null", registerBody);
            StringAssert.Contains("TryUnregisterRadiationSource();", registerBody);
            AssertSourceOrder(registerBody, "hazardType != HazardType.Radiation || _cachedTransform == null", "TryUnregisterRadiationSource();");
            AssertSourceOrder(registerBody, "TryUnregisterRadiationSource();", "float intensity = baseDamagePerSecond * 10f;");
            StringAssert.Contains("RadiationHazardGrid.RegisterSource(_radiationSourceId, in sourceAup, safeIntensity, safeRadius);", registerBody);
        }

        [Test]
        public void ToxicityExposureSignalsRemainPublishableWhenSourceAupIsUnavailable()
        {
            string hazardZoneManager = ReadProjectFile("Assets/_Project/Scripts/Gameplay/HazardZoneManager.cs");
            string environmentalHazard = ReadProjectFile("Assets/_Project/Scripts/Gameplay/EnvironmentalHazard.cs");
            string hazardZoneBody = ExtractMethodBody(hazardZoneManager, "private void PublishToxicityExposureSignal(float damageMagnitude, float currentIntensity)");
            string environmentalBody = ExtractMethodBody(environmentalHazard, "private void ApplyToxicityExposure(HectonPlayerHealth playerHealth, float damageMagnitude)");

            StringAssert.Contains("bool hasSourceAup = TryResolvePlayerPredictedAup(out AbsoluteUniversePosition playerAup) ||", hazardZoneBody);
            StringAssert.Contains("(_playerTransform != null && TryResolveAupFromRuntimeOrigin(_playerTransform.position, out playerAup));", hazardZoneBody);
            StringAssert.Contains("if (hasSourceAup)", hazardZoneBody);
            StringAssert.Contains("signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;", hazardZoneBody);
            Assert.That(hazardZoneBody, Does.Not.Contain("(_playerTransform == null || !TryResolveAupFromRuntimeOrigin(_playerTransform.position, out playerAup)))"));
            AssertSourceOrder(hazardZoneBody, "if (exposure01 <= 0.0001f && toxemiaDelta <= 0f)", "bool hasSourceAup");
            AssertSourceOrder(hazardZoneBody, "ToxicityExposureSignal signal = default;", "if (hasSourceAup)");
            AssertSourceOrder(hazardZoneBody, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");

            StringAssert.Contains("bool hasSourceAup = TryResolveAupFromRuntimeOrigin(playerTransform.position, out AbsoluteUniversePosition playerAup);", environmentalBody);
            StringAssert.Contains("if (hasSourceAup)", environmentalBody);
            StringAssert.Contains("signal.Flags = ToxicityExposureSignal.FlagHasSourceAup;", environmentalBody);
            Assert.That(environmentalBody, Does.Not.Contain("if (!TryResolveAupFromRuntimeOrigin(playerTransform.position, out AbsoluteUniversePosition playerAup))"));
            AssertSourceOrder(environmentalBody, "if (exposure01 <= 0.0001f && toxemiaDelta <= 0f)", "bool hasSourceAup");
            AssertSourceOrder(environmentalBody, "ToxicityExposureSignal signal = default;", "if (hasSourceAup)");
            AssertSourceOrder(environmentalBody, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
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
