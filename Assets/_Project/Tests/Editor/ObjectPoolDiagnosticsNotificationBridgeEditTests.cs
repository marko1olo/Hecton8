using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolDiagnosticsNotificationBridgeEditTests
    {
        [Test]
        public void DataBusSaturationNotificationRefusalIsDiagnosticAndKeepsFrameThrottle()
        {
            string source = ReadScript("ObjectPoolDiagnostics.cs");
            string publish = ExtractMethodBody(source, "private static void PublishDataBusSaturationWarning()");
            string push = ExtractMethodBody(source, "private static void TryPushDataBusSaturationNotification()");
            string report = ExtractMethodBody(source, "private static void ReportDataBusSaturationNotificationMiss()");
            string reset = ExtractMethodBody(source, "internal static void ResetStaticState()");

            StringAssert.Contains("DataBusSaturationNotificationMissWarningHash", source);
            StringAssert.Contains("DataBusSaturationNotificationContextHash", source);
            StringAssert.Contains("public static int DataBusSaturationNotificationMissCount =>", source);
            AssertTextBefore(publish, "_lastDataBusSaturationWarningFrame = frame;", "TryPushDataBusSaturationNotification();");
            StringAssert.DoesNotContain("Hecton8.UI.NotificationEvents.TryPushWarning(\"DATA_BUS_SATURATED\".AsSpan());", publish);
            StringAssert.Contains("Hecton8.UI.NotificationEvents.TryPushWarning(\"DATA_BUS_SATURATED\".AsSpan())", push);
            StringAssert.Contains("ReportDataBusSaturationNotificationMiss();", push);
            StringAssert.Contains("_dataBusSaturationNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("DataBusSaturationNotificationMissWarningHash", report);
            StringAssert.Contains("DataBusSaturationNotificationContextHash", report);
            StringAssert.Contains("Mathf.Max(1, _dataBusSaturationNotificationMissCount)", report);
            StringAssert.Contains("_dataBusSaturationNotificationMissCount = 0;", reset);
        }

        private static string ReadScript(string fileName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", fileName));
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
