using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BaseModuleCascadeNotificationBridgeEditTests
    {
        [Test]
        public void CascadeFailureNotificationRefusalStaysDiagnosticAndDoesNotGateFailureEffects()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "BaseModule.cs"));
            string cascade = ExtractMethodBody(source, "private void TriggerCascadeFailure(BaseModuleFailureMode failureMode)");
            string push = ExtractMethodBody(source, "private void TryPushCascadeFailureNotification(");
            string report = ExtractMethodBody(source, "private void ReportCascadeFailureNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearCascadeNotificationDiagnostics()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string destroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("s_baseCascadeNotificationMissWarningHash", source);
            StringAssert.Contains("s_baseCascadeNotificationContextHash", source);
            StringAssert.Contains("public int CascadeNotificationMissCount =>", source);

            AssertTextBefore(cascade, "_integrityComponent.TriggerCascadeFailure(failureMode);", "TryPushCascadeFailureNotification(");
            StringAssert.Contains("RecordCascadeFailure(\"MODULE FIRE\"", cascade);
            StringAssert.Contains("RecordCascadeFailure(\"SHORT CIRCUIT\"", cascade);
            StringAssert.Contains("RecordCascadeFailure(\"OXYGEN LEAK\"", cascade);
            StringAssert.Contains("TryPushCascadeFailureNotification(\"BASE MODULE FIRE // SERVICE NOW\".AsSpan(), BaseModuleFailureMode.Fire);", cascade);
            StringAssert.Contains("TryPushCascadeFailureNotification(\"BASE SHORT CIRCUIT // POWER LOCKOUT\".AsSpan(), BaseModuleFailureMode.ShortCircuit);", cascade);
            StringAssert.Contains("TryPushCascadeFailureNotification(\"BASE OXYGEN LEAK // COMPARTMENT BREACHED\".AsSpan(), BaseModuleFailureMode.OxygenLeak);", cascade);
            AssertTextBefore(cascade, "TryPushCascadeFailureNotification(", "SetLeakActive(ShouldLeakBeActive());");
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(\"BASE MODULE FIRE // SERVICE NOW\".AsSpan());", cascade);

            StringAssert.Contains("NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains("ReportCascadeFailureNotificationMiss(failureMode);", push);
            StringAssert.Contains("_cascadeNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("s_baseCascadeNotificationMissWarningHash", report);
            StringAssert.Contains("s_baseCascadeNotificationContextHash ^ moduleHash ^ (uint)failureMode", report);
            StringAssert.Contains("math.max(1, _cascadeNotificationMissCount)", report);
            StringAssert.Contains("_cascadeNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearCascadeNotificationDiagnostics();", disable);
            StringAssert.Contains("ClearCascadeNotificationDiagnostics();", destroy);
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
