using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EcosystemHazardReadModelBridgeEditTests
    {
        [Test]
        public void EcosystemDirector_ConsumesHazardExposureThroughReadModelInterfaceAcrossColdCacheAndHotSwap()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "World",
                "EcosystemDirector.cs"));
            string sampleMutationScalars = ExtractMethodBody(source, "private void SampleMutationScalars(");
            string cacheColdRegistryReferences = ExtractMethodBody(source, "private void CacheColdRegistryReferences()");
            string serviceReplaced = ExtractMethodBody(source, "public void OnGlobalRegistryServiceReplaced(");

            StringAssert.Contains("private IHazardZoneReadModel _cachedHazardZones;", source);
            StringAssert.Contains("IHazardZoneReadModel hazardZoneManager = _cachedHazardZones;", sampleMutationScalars);
            StringAssert.Contains("hazardZoneManager.GetHazardIntensity(runtimePosition, HazardType.Toxicity)", sampleMutationScalars);
            StringAssert.Contains("_cachedHazardZones = GlobalRegistry.HazardZoneReadModel;", cacheColdRegistryReferences);
            Assert.IsTrue(ContainsTokensInOrder(
                serviceReplaced,
                "case GlobalRegistryServiceSlot.HazardZoneRuntime:",
                "_cachedHazardZones = currentService as IHazardZoneReadModel;",
                "break;"));

            StringAssert.DoesNotContain("private HazardZoneManager _cachedHazardZones;", source);
            StringAssert.DoesNotContain("HazardZoneManager hazardZoneManager = _cachedHazardZones;", sampleMutationScalars);
            StringAssert.DoesNotContain("_cachedHazardZones = GlobalRegistry.HazardZones;", cacheColdRegistryReferences);
            StringAssert.DoesNotContain("_cachedHazardZones = currentService as HazardZoneManager;", serviceReplaced);
        }

        [Test]
        public void EcosystemDirector_PlayerStressUsesRuntimeReadModelsAndRequiresPlayerRoot()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "World",
                "EcosystemDirector.cs"));
            string stress = ExtractMethodBody(source, "private static bool TryResolveDirectorPlayerStress01(out float stress01)");

            StringAssert.Contains("IPlayerRuntimeContext runtimeContext = PlayerRuntimeContextService.ActiveRuntimeContext;", stress);
            StringAssert.Contains("runtimeContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState)", stress);
            StringAssert.Contains("runtimeContext.TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState stressState)", stress);
            StringAssert.Contains("(stressState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasMovement) != 0u", stress);
            StringAssert.Contains("(stressState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u", stress);
            StringAssert.Contains("math.isfinite(stressState.UnderwaterStressIntensity01)", stress);
            StringAssert.Contains("stress01 = math.max(stress01, math.saturate(stressState.UnderwaterStressIntensity01));", stress);
            Assert.IsTrue(ContainsTokensInOrder(
                stress,
                "runtimeContext.TryGetSurvivalRuntimeState(out PlayerSurvivalRuntimeState survivalState)",
                "runtimeContext.TryGetMovementStressRuntimeState(out PlayerMovementStressRuntimeState stressState)",
                "(stressState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u",
                "stress01 = math.max(stress01, math.saturate(stressState.UnderwaterStressIntensity01));",
                "return resolved;",
                "IPlayerRuntimeContext playerContext = ActiveRuntimeInstance != null"));
            StringAssert.DoesNotContain("PlayerRuntimeContextService.TryGetActiveRuntimeContext", stress);
            StringAssert.DoesNotContain("runtimeContext.MovementState", stress);
            StringAssert.DoesNotContain("runtimeContext.SurvivalState", stress);
        }

        [Test]
        public void EcosystemDirector_HostilityNotificationRefusalIsDiagnosticAfterTierCommit()
        {
            string source = File.ReadAllText(Path.Combine(
                Application.dataPath,
                "_Project",
                "Scripts",
                "World",
                "EcosystemDirector.cs"));
            string refresh = ExtractMethodBody(source, "private void RefreshHostilityTier()");
            string push = ExtractMethodBody(source, "private void TryPushHostilityNotification(");
            string report = ExtractMethodBody(source, "private void ReportHostilityNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearHostilityNotificationDiagnostics()");
            string shutdown = ExtractMethodBody(source, "private void ShutdownServiceState()");
            string dispose = ExtractMethodBody(source, "private void DisposeRuntimeState()");

            StringAssert.Contains("private static readonly uint _HostilityNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _HostilityNotificationContextHash", source);
            StringAssert.Contains("public int HostilityNotificationMissCount =>", source);
            Assert.IsTrue(ContainsTokensInOrder(
                refresh,
                "_hostilityTier = tier;",
                "switch (tier)",
                "TryPushHostilityNotification("));
            StringAssert.Contains("\"BIOME HOSTILITY: EXTREME. THE ABYSS HATES YOU.\".AsSpan(), tier", refresh);
            StringAssert.Contains("\"BIOME HOSTILITY: ELEVATED. PREDATOR PEAK EXTENDED.\".AsSpan(), tier", refresh);
            StringAssert.Contains("\"BIOME HOSTILITY: RISING.\".AsSpan(), tier", refresh);
            StringAssert.DoesNotContain("NotificationEvents.TryPushCritical(\"BIOME HOSTILITY", refresh);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(\"BIOME HOSTILITY", refresh);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(\"BIOME HOSTILITY", refresh);

            StringAssert.Contains("NotificationEvents.TryPushCritical(message)", push);
            StringAssert.Contains("NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains("NotificationEvents.TryPushInfo(message)", push);
            StringAssert.Contains("ReportHostilityNotificationMiss(tier);", push);
            StringAssert.Contains("_hostilityNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_HostilityNotificationMissWarningHash", report);
            StringAssert.Contains("_EcosystemDirectorContextHash ^ _HostilityNotificationContextHash ^ unchecked((uint)tier)", report);
            StringAssert.Contains("math.max(1, _hostilityNotificationMissCount)", report);
            StringAssert.Contains("_hostilityNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearHostilityNotificationDiagnostics();", shutdown);
            StringAssert.Contains("ClearHostilityNotificationDiagnostics();", dispose);
        }

        private static bool ContainsTokensInOrder(string text, params string[] tokens)
        {
            int index = 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                int found = text.IndexOf(tokens[i], index, StringComparison.Ordinal);
                if (found < 0)
                    return false;

                index = found + tokens[i].Length;
            }

            return true;
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
                char c = source[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c != '}')
                    continue;

                depth--;
                if (depth == 0)
                    return source.Substring(bodyStart, i - bodyStart + 1);
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
