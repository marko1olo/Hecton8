using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class PersistentWorldRegistryNotificationBridgeEditTests
    {
        [Test]
        public void SectorCorruptionNotificationRefusalIsDiagnosticAndDoesNotGateIndexedSectorRestore()
        {
            string source = ReadWorldScript("PersistentWorldRegistry.cs");
            string paging = ExtractMethodBody(source, "private async Awaitable RunIndexedSectorPagingAsync(");
            string push = ExtractMethodBody(source, "private void TryPushSectorCorruptionNotification()");
            string report = ExtractMethodBody(source, "private void ReportSectorCorruptionNotificationMiss()");
            string clear = ExtractMethodBody(source, "private void ClearSectorCorruptionNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("private static readonly uint _sectorCorruptionNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _sectorCorruptionNotificationContextHash", source);
            StringAssert.Contains("public int SectorCorruptionNotificationMissCount =>", source);

            AssertTextBefore(paging, "if (quarantinedSectorResetApplied)", "TryPushSectorCorruptionNotification();");
            AssertTextBefore(paging, "TryPushSectorCorruptionNotification();", "await AwaitSectorPrefabPrewarmAsync(stagedRecords);");
            AssertTextBefore(paging, "await AwaitSectorPrefabPrewarmAsync(stagedRecords);", "RestoreFromLoadedRecords(stagedRecords, scheduleHydration: false);");
            StringAssert.DoesNotContain("Hecton8.UI.NotificationEvents.TryPushCritical(LocalizedSectorCorruptionMessage.AsSpan());", paging);

            StringAssert.Contains("Hecton8.UI.NotificationEvents.TryPushCritical(LocalizedSectorCorruptionMessage.AsSpan())", push);
            StringAssert.Contains("ReportSectorCorruptionNotificationMiss();", push);
            StringAssert.Contains("_sectorCorruptionNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_sectorCorruptionNotificationMissWarningHash", report);
            StringAssert.Contains("_sectorCorruptionNotificationContextHash", report);
            StringAssert.Contains("math.max(1, _sectorCorruptionNotificationMissCount)", report);
            StringAssert.Contains("_sectorCorruptionNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearSectorCorruptionNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearSectorCorruptionNotificationDiagnostics();", onDestroy);
        }

        private static string ReadWorldScript(string fileName)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "_Project", "Scripts", "World", fileName));
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
