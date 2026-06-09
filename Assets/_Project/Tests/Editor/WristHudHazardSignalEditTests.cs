using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class WristHudHazardSignalEditTests
    {
        [Test]
        public void WristHud_DecaysTransientHazardsAndFiltersPlayerScopedSignals()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/UI/WristHologramHudRuntime.cs"));
            string drainQueuesBody = ExtractMethodBody(source, "private void DrainSignalQueues(");
            string decayBody = ExtractMethodBody(source, "private void DecayTransientHazardVitals(");
            string decayValueBody = ExtractMethodBody(source, "private static float DecayTransientHazardVital01(");
            string drainSnapshotsBody = ExtractMethodBody(source, "private void DrainGlobalSignalSnapshots()");
            string onEnableBody = ExtractMethodBody(source, "private void OnEnable()");
            string registrySwapBody = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");
            string refreshBody = ExtractMethodBody(source, "private void RefreshCachedRegistryServices()");
            string targetRefreshBody = ExtractMethodBody(source, "private void RefreshPlayerToxicityTargetHash(");
            string survivalSourceRefreshBody = ExtractMethodBody(source, "private void RefreshPlayerSurvivalVitalsSourceId(");

            StringAssert.Contains("using ToxicityExposureSignal = Hecton8.Atmosphere.ToxicityExposureSignal;", source);
            StringAssert.Contains("private const float TransientHazardVitalsDecayPerSecond = 2.75f;", source);
            StringAssert.Contains("private const float TransientHazardVitalsClearThreshold = 0.001f;", source);
            StringAssert.Contains("private const uint PlayerToxicityFallbackEntityHash = ToxicityExposureSignal.PlayerEntityFallbackHash;", source);
            StringAssert.Contains("private GameObject _playerToxicityTargetObject;", source);
            StringAssert.Contains("private uint _playerToxicityTargetHash = PlayerToxicityFallbackEntityHash;", source);
            StringAssert.Contains("private uint _playerSurvivalVitalsSourceId;", source);
            StringAssert.Contains("private int _lastToxicityExposureSnapshotGeneration;", source);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", onEnableBody);

            int decayIndex = drainQueuesBody.IndexOf("DecayTransientHazardVitals(deltaTime);", StringComparison.Ordinal);
            int playerTargetRefreshIndex = drainQueuesBody.IndexOf("RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);", StringComparison.Ordinal);
            int survivalSourceRefreshIndex = drainQueuesBody.IndexOf("RefreshPlayerSurvivalVitalsSourceId(GlobalRegistry.Player);", StringComparison.Ordinal);
            int drainIndex = drainQueuesBody.IndexOf("DrainGlobalSignalSnapshots();", StringComparison.Ordinal);
            Assert.GreaterOrEqual(decayIndex, 0, drainQueuesBody);
            Assert.Greater(playerTargetRefreshIndex, decayIndex, drainQueuesBody);
            Assert.Greater(survivalSourceRefreshIndex, playerTargetRefreshIndex, drainQueuesBody);
            Assert.Greater(drainIndex, survivalSourceRefreshIndex, drainQueuesBody);

            StringAssert.Contains("math.isfinite(deltaTime) ? deltaTime : 0f", decayBody);
            StringAssert.Contains("1f - safeDelta * TransientHazardVitalsDecayPerSecond", decayBody);
            StringAssert.Contains("_latestVitals.Radiation01 = DecayTransientHazardVital01(_latestVitals.Radiation01, decay01);", decayBody);
            StringAssert.Contains("_latestVitals.Toxemia01 = DecayTransientHazardVital01(_latestVitals.Toxemia01, decay01);", decayBody);
            StringAssert.Contains("FiniteSaturate(value) * math.saturate(decay01)", decayValueBody);
            StringAssert.Contains("TransientHazardVitalsClearThreshold ? 0f : next", decayValueBody);

            StringAssert.Contains("SignalBus<RadiationDoseSignal>.GetFrameSnapshot()", drainSnapshotsBody);
            StringAssert.Contains("uint survivalVitalsSourceId = _playerSurvivalVitalsSourceId;", drainSnapshotsBody);
            StringAssert.Contains("if (survivalVitalsSourceId != 0u)", drainSnapshotsBody);
            StringAssert.Contains("SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot()", drainSnapshotsBody);
            StringAssert.Contains("if (signal.SourceId != survivalVitalsSourceId)", drainSnapshotsBody);
            StringAssert.Contains("continue;", drainSnapshotsBody);
            StringAssert.Contains("_latestVitals.Oxygen01 = FiniteSaturate(signal.Oxygen01);", drainSnapshotsBody);
            StringAssert.Contains("_latestVitals.Power01 = FiniteSaturate(signal.Energy01);", drainSnapshotsBody);
            StringAssert.Contains("_latestVitals.Health01 = FiniteSaturate(signal.Integrity01);", drainSnapshotsBody);
            StringAssert.Contains("float radiationIntensity01 = FiniteSaturate(signal.Intensity01);", drainSnapshotsBody);
            StringAssert.Contains("float radiationDoseToxemia01 = RadiationDoseSignal.DoseToUnit01(signal.Dose);", drainSnapshotsBody);
            StringAssert.Contains("_latestVitals.Radiation01 = math.max(_latestVitals.Radiation01, radiationIntensity01);", drainSnapshotsBody);
            StringAssert.Contains("_latestVitals.Toxemia01 = math.max(_latestVitals.Toxemia01, radiationDoseToxemia01);", drainSnapshotsBody);
            StringAssert.DoesNotContain("FiniteSaturate(signal.Dose * 0.01f)", drainSnapshotsBody);
            StringAssert.Contains("int toxicitySnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", drainSnapshotsBody);
            StringAssert.Contains("if (toxicitySnapshotGeneration != _lastToxicityExposureSnapshotGeneration)", drainSnapshotsBody);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = toxicitySnapshotGeneration;", drainSnapshotsBody);
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.GetFrameSnapshot()", drainSnapshotsBody);
            StringAssert.Contains("uint playerToxicityTargetHash = _playerToxicityTargetHash != 0u ? _playerToxicityTargetHash : PlayerToxicityFallbackEntityHash;", drainSnapshotsBody);
            StringAssert.Contains("if (signal.EntityId == 0u)", drainSnapshotsBody);
            StringAssert.Contains("continue;", drainSnapshotsBody);
            StringAssert.Contains("if (signal.EntityId != playerToxicityTargetHash && signal.EntityId != PlayerToxicityFallbackEntityHash)", drainSnapshotsBody);
            StringAssert.Contains("float exposure01 = FiniteSaturate(signal.Exposure01);", drainSnapshotsBody);
            StringAssert.Contains("float toxemiaDelta01 = FiniteSaturate(signal.ToxemiaDelta);", drainSnapshotsBody);
            StringAssert.Contains("_latestVitals.Toxemia01 = math.max(_latestVitals.Toxemia01, math.max(exposure01, toxemiaDelta01));", drainSnapshotsBody);
            StringAssert.DoesNotContain("FlagHasSourceAup", drainSnapshotsBody);
            int entityGuardIndex = drainSnapshotsBody.IndexOf("if (signal.EntityId == 0u)", StringComparison.Ordinal);
            int playerGuardIndex = drainSnapshotsBody.IndexOf("if (signal.EntityId != playerToxicityTargetHash && signal.EntityId != PlayerToxicityFallbackEntityHash)", StringComparison.Ordinal);
            int survivalSourceIdIndex = drainSnapshotsBody.IndexOf("uint survivalVitalsSourceId = _playerSurvivalVitalsSourceId;", StringComparison.Ordinal);
            int survivalSnapshotIndex = drainSnapshotsBody.IndexOf("SignalBus<SurvivalVitalsChangedSignal>.GetFrameSnapshot()", StringComparison.Ordinal);
            int survivalSourceGuardIndex = drainSnapshotsBody.IndexOf("if (signal.SourceId != survivalVitalsSourceId)", StringComparison.Ordinal);
            int survivalReadIndex = drainSnapshotsBody.IndexOf("_latestVitals.Oxygen01 = FiniteSaturate(signal.Oxygen01);", StringComparison.Ordinal);
            int toxicityGenerationIndex = drainSnapshotsBody.IndexOf("int toxicitySnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", StringComparison.Ordinal);
            int toxicityGenerationGateIndex = drainSnapshotsBody.IndexOf("if (toxicitySnapshotGeneration != _lastToxicityExposureSnapshotGeneration)", StringComparison.Ordinal);
            int toxicityReadIndex = drainSnapshotsBody.IndexOf("float exposure01 = FiniteSaturate(signal.Exposure01);", StringComparison.Ordinal);
            Assert.GreaterOrEqual(entityGuardIndex, 0, drainSnapshotsBody);
            Assert.GreaterOrEqual(survivalSourceIdIndex, 0, drainSnapshotsBody);
            Assert.Greater(survivalSnapshotIndex, survivalSourceIdIndex, drainSnapshotsBody);
            Assert.Greater(survivalSourceGuardIndex, survivalSnapshotIndex, drainSnapshotsBody);
            Assert.Greater(survivalReadIndex, survivalSourceGuardIndex, drainSnapshotsBody);
            Assert.GreaterOrEqual(toxicityGenerationIndex, 0, drainSnapshotsBody);
            Assert.Greater(toxicityGenerationGateIndex, toxicityGenerationIndex, drainSnapshotsBody);
            Assert.Greater(entityGuardIndex, toxicityGenerationGateIndex, drainSnapshotsBody);
            Assert.Greater(playerGuardIndex, entityGuardIndex, drainSnapshotsBody);
            Assert.Greater(toxicityReadIndex, entityGuardIndex, drainSnapshotsBody);
            Assert.Greater(toxicityReadIndex, playerGuardIndex, drainSnapshotsBody);

            StringAssert.Contains("if (serviceSlot == GlobalRegistryServiceSlot.Player)", registrySwapBody);
            StringAssert.Contains("IPlayerRuntimeContext playerContext = currentService as IPlayerRuntimeContext;", registrySwapBody);
            StringAssert.Contains("RefreshPlayerToxicityTargetHash(playerContext);", registrySwapBody);
            StringAssert.Contains("RefreshPlayerSurvivalVitalsSourceId(playerContext);", registrySwapBody);
            StringAssert.Contains("_lastToxicityExposureSnapshotGeneration = SignalBus<ToxicityExposureSignal>.SnapshotGeneration;", registrySwapBody);
            StringAssert.Contains("PdaProjectorRebindPlayerRuntimeContext(playerContext);", registrySwapBody);
            StringAssert.Contains("RefreshPlayerToxicityTargetHash(GlobalRegistry.Player);", refreshBody);
            StringAssert.Contains("RefreshPlayerSurvivalVitalsSourceId(GlobalRegistry.Player);", refreshBody);
            StringAssert.Contains("playerObject = BootstrapState.CurrentPlayerObject;", targetRefreshBody);
            StringAssert.Contains("ReferenceEquals(playerObject, _playerToxicityTargetObject)", targetRefreshBody);
            StringAssert.Contains("_playerToxicityTargetObject = playerObject;", targetRefreshBody);
            StringAssert.Contains("EntityId.ToULong(playerObject.GetEntityId())", targetRefreshBody);
            StringAssert.Contains("_playerToxicityTargetHash = targetHash != 0u ? targetHash : PlayerToxicityFallbackEntityHash;", targetRefreshBody);
            StringAssert.Contains("playerContext != null && playerContext.IsInitialized", survivalSourceRefreshBody);
            StringAssert.Contains("playerContext.SurvivalSystem", survivalSourceRefreshBody);
            StringAssert.Contains("RuntimeOriginRoute.FoldEntityIdToSourceId(EntityId.ToULong(survival.GetEntityId()))", survivalSourceRefreshBody);
            StringAssert.Contains(": 0u", survivalSourceRefreshBody);
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
    }
}
