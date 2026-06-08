using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonApiNotificationBridgeEditTests
    {
        [Test]
        public void ModUiNotificationsReportQueueRefusalsWithModContext()
        {
            string source = ReadScript("ModdingAPI", "HectonAPI.cs");
            string reset = ExtractMethodBody(source, "internal static void ResetRegistryCacheCold()");
            string showInfo = ExtractMethodBody(source, "public static void ShowInfo(string message)");
            string showWarning = ExtractMethodBody(source, "public static void ShowWarning(string message)");
            string showCritical = ExtractMethodBody(source, "public static void ShowCritical(string message)");
            string push = ExtractMethodBody(source, "private static void TryPushNotification(");
            string report = ExtractMethodBody(source, "private static void ReportNotificationMiss(");
            string clear = ExtractMethodBody(source, "internal static void ResetNotificationDiagnostics()");

            StringAssert.Contains("private static readonly uint NotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint NotificationContextHash", source);
            StringAssert.Contains("public static int NotificationMissCount =>", source);
            StringAssert.Contains("UI.ResetNotificationDiagnostics();", reset);
            StringAssert.Contains("TryPushNotification(messageSpan, severity: 0);", showInfo);
            StringAssert.Contains("TryPushNotification(messageSpan, severity: 1);", showWarning);
            StringAssert.Contains("TryPushNotification(messageSpan, severity: 2);", showCritical);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(messageSpan);", showInfo);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(messageSpan);", showWarning);
            StringAssert.DoesNotContain("NotificationEvents.TryPushCritical(messageSpan);", showCritical);
            StringAssert.Contains("2 => NotificationEvents.TryPushCritical(message)", push);
            StringAssert.Contains("1 => NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains("_ => NotificationEvents.TryPushInfo(message)", push);
            StringAssert.Contains("ReportNotificationMiss(severity);", push);
            StringAssert.Contains("s_notificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("NotificationMissWarningHash", report);
            StringAssert.Contains("NotificationContextHash ^ ModExecutionScope.CurrentModHash ^ unchecked((uint)severity)", report);
            StringAssert.Contains("Mathf.Max(1, s_notificationMissCount)", report);
            StringAssert.Contains("s_notificationMissCount = 0;", clear);
        }

        private static string ReadScript(string folder, string fileName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", folder, fileName));
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
    }
}
