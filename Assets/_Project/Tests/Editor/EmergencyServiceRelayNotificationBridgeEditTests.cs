using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class EmergencyServiceRelayNotificationBridgeEditTests
    {
        [Test]
        public void RelayInteractionNotificationsReportQueueRefusalWithoutGatingRelayState()
        {
            string source = ReadWorldScript("EmergencyServiceRelay.cs");
            string interact = ExtractMethodBody(source, "public void Interact(");
            string rewards = ExtractMethodBody(source, "private void TryGrantRewards(");
            string push = ExtractMethodBody(source, "private void TryPushRelayNotification(");
            string report = ExtractMethodBody(source, "private void ReportRelayNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearRelayNotificationDiagnostics()");

            StringAssert.Contains("private static readonly uint _RelayNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _RelayNotificationContextHash", source);
            StringAssert.Contains("public int RelayNotificationMissCount =>", source);

            StringAssert.Contains("TryPushRelayNotification(resolvedLoreMessage, _RelayLoreNotificationContextHash, warning: false);", interact);
            StringAssert.Contains("audioLogSystem.TryPlayAudioLogByHash(_linkedAudioLogHash);", interact);
            StringAssert.Contains("TryGrantRewards(interactor);", interact);
            StringAssert.Contains("EmergencyServiceRelayEvents.TryRaiseRelayActivated(this, firstActivation);", interact);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(resolvedLoreMessage);", interact);

            StringAssert.Contains("TryPushRelayNotification(", rewards);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(BuildRewardGrantedMessageSpan());", rewards);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(", rewards);
            StringAssert.Contains("NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains("NotificationEvents.TryPushInfo(message)", push);
            StringAssert.Contains("ReportRelayNotificationMiss(contextHash);", push);

            StringAssert.Contains("_relayNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_RelayNotificationMissWarningHash", report);
            StringAssert.Contains("_RelayNotificationContextHash ^ contextHash ^ _relayHash", report);
            StringAssert.Contains("math.max(1, _relayNotificationMissCount)", report);
            StringAssert.Contains("_relayNotificationMissCount = 0;", clear);
        }

        [Test]
        public void RelayDirectorRouteNotificationRefusalStaysDiagnosticAndKeepsRouteTarget()
        {
            string source = ReadWorldScript("EmergencyServiceRelayDirector.cs");
            string handle = ExtractMethodBody(source, "private void HandleRelayActivated(");
            string push = ExtractMethodBody(source, "private void TryPushRouteNotification(");
            string report = ExtractMethodBody(source, "private void ReportRouteNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearRouteNotificationDiagnostics()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string abort = ExtractMethodBody(source, "private void AbortDuplicateRuntimeOwner()");

            StringAssert.Contains("private static readonly uint _RouteNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _RouteNotificationContextHash", source);
            StringAssert.Contains("public int RouteNotificationMissCount =>", source);

            StringAssert.Contains("_currentRouteTarget = nextRelay;", handle);
            AssertTextBefore(handle, "_currentRouteTarget = nextRelay;", "TryPushRouteNotification(routeMessage, relay.RelayHash);");
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(routeMessage);", handle);
            StringAssert.Contains("NotificationEvents.TryPushInfo(message)", push);
            StringAssert.Contains("ReportRouteNotificationMiss(relayHash);", push);

            StringAssert.Contains("_routeNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_RouteNotificationMissWarningHash", report);
            StringAssert.Contains("_RouteNotificationContextHash ^ relayHash", report);
            StringAssert.Contains("Mathf.Max(1, _routeNotificationMissCount)", report);
            StringAssert.Contains("_routeNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearRouteNotificationDiagnostics();", disable);
            StringAssert.Contains("ClearRouteNotificationDiagnostics();", abort);
        }

        private static string ReadWorldScript(string fileName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "World", fileName));
        }

        private static void AssertTextBefore(string body, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = body.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = body.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, "Missing earlier text: " + expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, "Missing later text: " + expectedLater);
            Assert.Less(earlierIndex, laterIndex, expectedEarlier + " should appear before " + expectedLater);
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
