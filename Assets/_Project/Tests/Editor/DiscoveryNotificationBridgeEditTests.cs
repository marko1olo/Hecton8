using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class DiscoveryNotificationBridgeEditTests
    {
        [Test]
        public void BiomeDiscoveryNotificationRefusalStaysDiagnosticAndDoesNotGateDiscoveryState()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "HectonDiscoveryManager.cs"));
            string discover = ExtractMethodBody(source, "public void DiscoverBiome(");
            string push = ExtractMethodBody(source, "private void PushBiomeDiscoveredNotification(");
            string report = ExtractMethodBody(source, "private void ReportBiomeDiscoveryNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearBiomeDiscoveryNotificationDiagnostics()");
            string disable = ExtractMethodBody(source, "private void OnDisable()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");

            StringAssert.Contains("private static readonly uint _BiomeDiscoveryNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _BiomeDiscoveryNotificationContextHash", source);
            StringAssert.Contains("public int BiomeDiscoveryNotificationMissCount =>", source);

            AssertTextBefore(discover, "_discoveredBiomeIds.Add(biomeId)", "PushBiomeDiscoveredNotification(biomeId);");
            AssertTextBefore(discover, "LastDiscoveredId = biomeId;", "PushBiomeDiscoveredNotification(biomeId);");
            AssertTextBefore(discover, "ProgressionMetaSignalRoute.TryPublishBiomeDiscovered(biomeId);", "PushBiomeDiscoveredNotification(biomeId);");

            StringAssert.Contains("if (NotificationEvents.TryPushInfo(_notificationBuffer.AsSpan()))", push);
            StringAssert.Contains("ReportBiomeDiscoveryNotificationMiss(biomeId);", push);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(_notificationBuffer.AsSpan());", push);
            StringAssert.Contains("_biomeDiscoveryNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_BiomeDiscoveryNotificationMissWarningHash", report);
            StringAssert.Contains("_BiomeDiscoveryNotificationContextHash ^ unchecked((uint)biomeId)", report);
            StringAssert.Contains("Mathf.Max(1, _biomeDiscoveryNotificationMissCount)", report);
            StringAssert.Contains("_biomeDiscoveryNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearBiomeDiscoveryNotificationDiagnostics();", disable);
            StringAssert.Contains("ClearBiomeDiscoveryNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_biomeDiscoveryNotificationMissCount", populate);
            StringAssert.DoesNotContain("_biomeDiscoveryNotificationMissCount", load);
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
