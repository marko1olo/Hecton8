using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class CorporateOrderNotificationBridgeEditTests
    {
        [Test]
        public void CorporateOrderNotificationsReportRefusalWithoutGatingDeliveryConflictOrSaveState()
        {
            string source = ReadScript("Narrative", "CorporateOrderSystem.cs");
            string deliver = ExtractMethodBody(source, "private void DeliverOrder(");
            string push = ExtractMethodBody(source, "private void TryPushOrderNotification(");
            string report = ExtractMethodBody(source, "private void ReportOrderNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearOrderNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string shutdown = ExtractMethodBody(source, "public void OnServiceShutdown()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");

            StringAssert.Contains("private static readonly uint _OrderNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _OrderNotificationContextHash", source);
            StringAssert.Contains("public int OrderNotificationMissCount =>", source);

            AssertTextBefore(deliver, "_receivedOrders.Add(orderId);", "uint orderHash = ComputeStableHash(orderId);");
            AssertTextBefore(deliver, "NarrativeEvents.TryRaiseDiscoveryMade(orderHash);", "TryPushOrderNotification(IncomingOrderWarningMessage.AsSpan(), orderHash);");
            AssertTextBefore(deliver, "TryRegisterConflictForOrder(orderId, in order)", "TryPushOrderNotification(");
            StringAssert.Contains("ComputeConflictHash(orderId, order.conflictsWithOrderId)", deliver);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(IncomingOrderWarningMessage.AsSpan());", deliver);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(ResolveLocalizedSpan(", deliver);

            StringAssert.Contains("NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains("ReportOrderNotificationMiss(contextHash);", push);
            StringAssert.Contains("_orderNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_OrderNotificationMissWarningHash", report);
            StringAssert.Contains("_CorporateOrderContextHash ^ _OrderNotificationContextHash ^ contextHash", report);
            StringAssert.Contains("Mathf.Max(1, _orderNotificationMissCount)", report);
            StringAssert.Contains("_orderNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearOrderNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearOrderNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearOrderNotificationDiagnostics();", shutdown);
            StringAssert.Contains("ClearOrderNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_orderNotificationMissCount", populate);
            StringAssert.DoesNotContain("_orderNotificationMissCount", load);
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
