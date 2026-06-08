using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class MountableTransportNotificationBridgeEditTests
    {
        [Test]
        public void EntanglementNotificationRefusalIsDiagnosticAndDoesNotGateHapticsOrStructuralFeedback()
        {
            string source = ReadGameplayScript("MountablePlayerTransport.cs");
            string notify = ExtractMethodBody(source, "private void NotifyEntanglementCritical()");
            string flush = ExtractMethodBody(source, "private void FlushQueuedEntanglementFeedback()");
            string push = ExtractMethodBody(source, "private void TryPushEntanglementCriticalNotification()");
            string report = ExtractMethodBody(source, "private void ReportEntanglementNotificationMiss()");
            string clear = ExtractMethodBody(source, "private void ClearQueuedEntanglementFeedback()");
            string unregister = ExtractMethodBody(source, "private void TryUnregister(bool clearQueuedPresentation = true)");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("private static readonly uint EntanglementNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint EntanglementNotificationContextHash", source);
            StringAssert.Contains("public int EntanglementNotificationMissCount =>", source);

            StringAssert.Contains("_pendingEntanglementCriticalNotification = true;", notify);
            StringAssert.Contains("_pendingEntanglementCriticalHapticDirty = true;", notify);

            AssertTextBefore(flush, "_pendingEntanglementCriticalNotification = false;", "TryPushEntanglementCriticalNotification();");
            AssertTextBefore(flush, "TryPushEntanglementCriticalNotification();", "if (_pendingEntanglementStressHapticDirty)");
            AssertTextBefore(flush, "TryPushEntanglementCriticalNotification();", "if (_pendingEntanglementCriticalHapticDirty)");
            AssertTextBefore(flush, "TryPushEntanglementCriticalNotification();", "if (_pendingEntanglementStructuralStressDirty)");
            StringAssert.DoesNotContain("NotificationEvents.TryPushCritical(EntanglementCriticalNotification.AsSpan());", flush);

            StringAssert.Contains("NotificationEvents.TryPushCritical(EntanglementCriticalNotification.AsSpan())", push);
            StringAssert.Contains("ReportEntanglementNotificationMiss();", push);
            StringAssert.Contains("_entanglementNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("EntanglementNotificationMissWarningHash", report);
            StringAssert.Contains("MountablePlayerTransportContextHash ^ EntanglementNotificationContextHash", report);
            StringAssert.Contains("math.max(1, _entanglementNotificationMissCount)", report);

            StringAssert.Contains("_pendingEntanglementCriticalNotification = false;", clear);
            StringAssert.Contains("ClearEntanglementNotificationDiagnostics();", clear);
            StringAssert.Contains("ClearQueuedEntanglementFeedback();", unregister);
            StringAssert.Contains("TryUnregister();", onDisable);
            StringAssert.Contains("TryUnregister();", onDestroy);
        }

        private static string ReadGameplayScript(string fileName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "Gameplay", fileName));
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            Assert.IsNotNull(source);
            int start = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(start, 0, signature);
            int brace = source.IndexOf('{', start);
            Assert.GreaterOrEqual(brace, 0, signature);

            int depth = 0;
            for (int i = brace; i < source.Length; i++)
            {
                if (source[i] == '{')
                {
                    depth++;
                }
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(brace, i - brace + 1);
                }
            }

            Assert.Fail("Method body not closed: " + signature);
            return string.Empty;
        }

        private static void AssertTextBefore(string body, string expectedEarlier, string expectedLater)
        {
            int earlierIndex = body.IndexOf(expectedEarlier, StringComparison.Ordinal);
            int laterIndex = body.IndexOf(expectedLater, StringComparison.Ordinal);
            Assert.GreaterOrEqual(earlierIndex, 0, "Missing earlier text: " + expectedEarlier);
            Assert.GreaterOrEqual(laterIndex, 0, "Missing later text: " + expectedLater);
            Assert.Less(earlierIndex, laterIndex, expectedEarlier + " should appear before " + expectedLater);
        }
    }
}
