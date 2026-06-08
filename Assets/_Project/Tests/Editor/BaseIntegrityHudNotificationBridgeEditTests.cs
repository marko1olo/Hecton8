using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BaseIntegrityHudNotificationBridgeEditTests
    {
        [Test]
        public void BaseIntegrityHudNotificationQueueRefusalAndOverflowStayVisibleAndClearOnLifecycle()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs");
            string lateFrame = ExtractMethodBody(source, "public void LateFrameTick()");
            string queue = ExtractMethodBody(source, "private void QueueNotification(");
            string tryPush = ExtractMethodBody(source, "private void TryPushPendingNotification(");
            string pushMiss = ExtractMethodBody(source, "private void ReportNotificationPushMiss(");
            string overflow = ExtractMethodBody(source, "private void ReportPendingNotificationOverflow(");
            string clear = ExtractMethodBody(source, "private void ClearNotificationRuntimeState()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("private static readonly uint BaseIntegrityHudNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint BaseIntegrityHudNotificationOverflowWarningHash", source);
            StringAssert.Contains("private static readonly uint BaseIntegrityHudNotificationContextHash", source);
            StringAssert.Contains("public int NotificationPushMissCount =>", source);
            StringAssert.Contains("public int NotificationOverflowCount =>", source);
            StringAssert.Contains("TryPushPendingNotification(messageHash, warning: true);", lateFrame);
            StringAssert.Contains("TryPushPendingNotification(messageHash, warning: false);", lateFrame);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredWarning(messageHash);", lateFrame);
            StringAssert.DoesNotContain("NotificationEvents.TryPushRegisteredInfo(messageHash);", lateFrame);
            StringAssert.Contains("ReportNotificationPushMiss(0u);", queue);
            AssertTextBefore(queue, "ReportNotificationPushMiss(0u);", "return;");
            StringAssert.Contains("if (_pendingNotificationCount >= _pendingNotificationHashes.Length)", queue);
            AssertTextBefore(queue, "ReportPendingNotificationOverflow(messageHash);", "_pendingNotificationCount = _pendingNotificationHashes.Length - 1;");
            StringAssert.Contains("? NotificationEvents.TryPushRegisteredWarning(messageHash)", tryPush);
            StringAssert.Contains(": NotificationEvents.TryPushRegisteredInfo(messageHash)", tryPush);
            StringAssert.Contains("ReportNotificationPushMiss(messageHash);", tryPush);
            StringAssert.Contains("_notificationPushMissCount++;", pushMiss);
            StringAssert.Contains("BaseIntegrityHudNotificationMissWarningHash", pushMiss);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", pushMiss);
            StringAssert.Contains("math.max(1, _notificationPushMissCount)", pushMiss);
            StringAssert.Contains("_notificationOverflowCount++;", overflow);
            StringAssert.Contains("BaseIntegrityHudNotificationOverflowWarningHash", overflow);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", overflow);
            StringAssert.Contains("math.max(1, _notificationOverflowCount)", overflow);
            StringAssert.Contains("_pendingNotificationCount = 0;", clear);
            StringAssert.Contains("Array.Clear(_pendingNotificationHashes, 0, _pendingNotificationHashes.Length);", clear);
            StringAssert.Contains("Array.Clear(_pendingNotificationTypes, 0, _pendingNotificationTypes.Length);", clear);
            StringAssert.Contains("_notificationPushMissCount = 0;", clear);
            StringAssert.Contains("_notificationOverflowCount = 0;", clear);
            StringAssert.Contains("ClearNotificationRuntimeState();", onDisable);
            StringAssert.Contains("ClearNotificationRuntimeState();", onDestroy);
        }

        [Test]
        public void BaseIntegrityHudEventLaneRefusalsReportBackpressureWithoutNoListenerNoise()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/UI/BaseIntegrityHUD.cs");
            string integrityWarnings = ExtractMethodBody(source, "private void CheckIntegrityWarnings(");
            string emergencyState = ExtractMethodBody(source, "private void PublishEmergencyState(");
            string airQualityState = ExtractMethodBody(source, "private void PublishAirQualityState(");
            string integrity = ExtractMethodBody(source, "private void TryRaiseIntegrityWarningEvent(");
            string breached = ExtractMethodBody(source, "private void TryRaiseBreachedEvent()");
            string emergency = ExtractMethodBody(source, "private void TryRaiseEmergencyEvent(");
            string airQuality = ExtractMethodBody(source, "private void TryRaiseAirQualityWarningEvent(");
            string report = ExtractMethodBody(source, "private void ReportBaseIntegrityEventLaneDropIfBackpressured(");
            string clear = ExtractMethodBody(source, "private void ClearEventLaneDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("public int EventLaneDropCount =>", source);
            StringAssert.Contains("private static readonly uint BaseIntegrityHudEventLaneDropWarningHash", source);
            StringAssert.Contains("private static readonly uint BaseIntegrityHudEventLaneContextHash", source);
            StringAssert.Contains("private static readonly uint BaseIntegrityHudIntegrityWarningEventContextHash", source);
            StringAssert.Contains("private static readonly uint BaseIntegrityHudBreachedEventContextHash", source);
            StringAssert.Contains("private static readonly uint BaseIntegrityHudEmergencyEventContextHash", source);
            StringAssert.Contains("private static readonly uint BaseIntegrityHudAirQualityEventContextHash", source);

            StringAssert.Contains("TryRaiseIntegrityWarningEvent(integrity);", integrityWarnings);
            StringAssert.DoesNotContain("BaseIntegrityEvents.TryRaiseIntegrityWarning(integrity);", integrityWarnings);
            StringAssert.Contains("TryRaiseBreachedEvent();", emergencyState);
            StringAssert.DoesNotContain("BaseIntegrityEvents.TryRaiseBreached();", emergencyState);
            StringAssert.Contains("TryRaiseEmergencyEvent(failureMode, integrity);", emergencyState);
            StringAssert.DoesNotContain("BaseIntegrityEvents.TryRaiseEmergency(failureMode, integrity);", emergencyState);
            StringAssert.Contains("TryRaiseAirQualityWarningEvent(airQuality);", airQualityState);
            StringAssert.DoesNotContain("BaseIntegrityEvents.TryRaiseAirQualityWarning(airQuality);", airQualityState);

            StringAssert.Contains("if (BaseIntegrityEvents.TryRaiseIntegrityWarning(integrity))", integrity);
            StringAssert.Contains("ReportBaseIntegrityEventLaneDropIfBackpressured(BaseIntegrityHudIntegrityWarningEventContextHash);", integrity);
            StringAssert.Contains("if (BaseIntegrityEvents.TryRaiseBreached())", breached);
            StringAssert.Contains("ReportBaseIntegrityEventLaneDropIfBackpressured(BaseIntegrityHudBreachedEventContextHash);", breached);
            StringAssert.Contains("if (BaseIntegrityEvents.TryRaiseEmergency(failureMode, integrity))", emergency);
            StringAssert.Contains("BaseIntegrityHudEmergencyEventContextHash ^ unchecked((uint)failureMode)", emergency);
            StringAssert.Contains("if (BaseIntegrityEvents.TryRaiseAirQualityWarning(airQualityNormalized))", airQuality);
            StringAssert.Contains("ReportBaseIntegrityEventLaneDropIfBackpressured(BaseIntegrityHudAirQualityEventContextHash);", airQuality);

            StringAssert.Contains("if (BaseIntegrityEvents.PendingCount <= 0)", report);
            StringAssert.Contains("_eventLaneDropCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("BaseIntegrityHudEventLaneDropWarningHash", report);
            StringAssert.Contains("BaseIntegrityHudEventLaneContextHash ^ contextHash", report);
            StringAssert.Contains("math.max(1, _eventLaneDropCount)", report);
            AssertTextBefore(report, "if (BaseIntegrityEvents.PendingCount <= 0)", "_eventLaneDropCount++;");

            StringAssert.Contains("_eventLaneDropCount = 0;", clear);
            StringAssert.Contains("ClearEventLaneDiagnostics();", onDisable);
            StringAssert.Contains("ClearEventLaneDiagnostics();", onDestroy);
        }

        private static string ReadProjectFile(string relativePath)
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return File.ReadAllText(Path.Combine(projectRoot, relativePath));
        }

        private static void AssertTextBefore(string text, string before, string after)
        {
            int beforeIndex = text.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = text.IndexOf(after, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing token: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing token: " + after);
            Assert.Less(beforeIndex, afterIndex, before + " should appear before " + after);
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
