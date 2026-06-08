using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ProceduralLoreNotificationBridgeEditTests
    {
        [Test]
        public void FrontierLoreNotificationRefusalIsDiagnosticAfterPlacementCommit()
        {
            string source = ReadScript("Narrative", "ProceduralLoreDirector.cs");
            string spawn = ExtractMethodBody(source, "private void TrySpawnFrontierLore()");
            string push = ExtractMethodBody(source, "private void TryPushFrontierLoreNotification(");
            string report = ExtractMethodBody(source, "private void ReportFrontierLoreNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearFrontierLoreNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string populate = ExtractMethodBody(source, "public void PopulateSaveData(");
            string load = ExtractMethodBody(source, "public void LoadFromSaveData(");

            StringAssert.Contains("s_frontierLoreNotificationMissWarningHash", source);
            StringAssert.Contains("s_frontierLoreNotificationContextHash", source);
            StringAssert.Contains("public int FrontierLoreNotificationMissCount =>", source);

            AssertTextBefore(spawn, "if (!TrySpawnInstance(ref placement))", "_activePlacements.Add(placement);");
            AssertTextBefore(spawn, "_activePlacements.Add(placement);", "_occupiedChunkKeys.Add(chunkKey);");
            AssertTextBefore(spawn, "_occupiedChunkKeys.Add(chunkKey);", "TryPushFrontierLoreNotification(");
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(\"PDA archive anomaly detected near the frontier", spawn);

            StringAssert.Contains("NotificationEvents.TryPushInfo(\"PDA archive anomaly detected near the frontier. Route updated with a probable data lead.\".AsSpan())", push);
            StringAssert.Contains("ReportFrontierLoreNotificationMiss(contextHash);", push);
            StringAssert.Contains("_frontierLoreNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("s_frontierLoreNotificationMissWarningHash", report);
            StringAssert.Contains("s_proceduralLoreDirectorContextHash ^ s_frontierLoreNotificationContextHash ^ contextHash", report);
            StringAssert.Contains("Mathf.Max(1, _frontierLoreNotificationMissCount)", report);
            StringAssert.Contains("_frontierLoreNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearFrontierLoreNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearFrontierLoreNotificationDiagnostics();", onDestroy);
            StringAssert.Contains("ClearFrontierLoreNotificationDiagnostics();", load);
            StringAssert.DoesNotContain("_frontierLoreNotificationMissCount", populate);
            StringAssert.DoesNotContain("_frontierLoreNotificationMissCount", load);
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
