using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class RandomEventNotificationBridgeEditTests
    {
        [Test]
        public void RandomEventNotificationsReportQueueRefusalWithoutGatingEventEffects()
        {
            string source = ReadScript("Gameplay", "RandomEventSystem.cs");
            string biolum = ExtractMethodBody(source, "private void TryTriggerBiolumStorm(");
            string thermal = ExtractMethodBody(source, "private void TryTriggerThermalEruption(");
            string fauna = ExtractMethodBody(source, "private void TryTriggerFaunaMigration()");
            string glitch = ExtractMethodBody(source, "private void TryTriggerGlitch(");
            string cave = ExtractMethodBody(source, "private void TryTriggerCaveCollapse(");
            string meteor = ExtractMethodBody(source, "private void TryTriggerMeteorShower()");
            string solar = ExtractMethodBody(source, "private void TryTriggerSolarFlare()");
            string push = ExtractMethodBody(source, "private void TryPushEventNotification(");
            string report = ExtractMethodBody(source, "private void ReportEventNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearEventNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("private static readonly uint _EventNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _EventNotificationContextHash", source);
            StringAssert.Contains("public int EventNotificationMissCount =>", source);

            AssertTextBefore(biolum, "StartEvent(RandomEventType.BiolumStorm", "PublishBiolumStormGlobal(1f);");
            AssertTextBefore(biolum, "PublishBiolumStormGlobal(1f);", "TryPushEventNotification(");
            AssertTextBefore(thermal, "StartEvent(RandomEventType.ThermalEruption", "TryPushEventNotification(");
            AssertTextBefore(thermal, "TryPushEventNotification(", "QueueThermalEruptionBurnStatus();");
            AssertTextBefore(fauna, "StartEvent(RandomEventType.FaunaMigration", "TryPushEventNotification(");
            AssertTextBefore(glitch, "StartEvent(RandomEventType.HectonOSGlitch", "PublishGlitchGlobal(1f);");
            AssertTextBefore(glitch, "PublishGlitchGlobal(1f);", "TryPushEventNotification(");
            AssertTextBefore(cave, "StartEvent(RandomEventType.CaveCollapse", "TryRaiseRandomEventSeismicShockwave(in seismicEvent);");
            AssertTextBefore(cave, "TryRaiseRandomEventSeismicShockwave(in seismicEvent);", "TryPushEventNotification(");
            AssertTextBefore(meteor, "BeginMeteorShower();", "StartEvent(RandomEventType.MeteorShower");
            AssertTextBefore(meteor, "StartEvent(RandomEventType.MeteorShower", "TryPushEventNotification(");
            AssertTextBefore(solar, "StartEvent(RandomEventType.SolarFlare", "TryPushEventNotification(");

            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(ResolveLocalizedSpan(", biolum);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(ResolveLocalizedSpan(", thermal);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(ResolveLocalizedSpan(", fauna);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(ResolveLocalizedSpan(", glitch);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(ResolveLocalizedSpan(", cave);
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(ResolveLocalizedSpan(", meteor);
            StringAssert.DoesNotContain("NotificationEvents.TryPushWarning(\"SOLAR FLARE", solar);

            StringAssert.Contains("? NotificationEvents.TryPushWarning(message)", push);
            StringAssert.Contains(": NotificationEvents.TryPushInfo(message)", push);
            StringAssert.Contains("ReportEventNotificationMiss(eventType);", push);
            StringAssert.Contains("_eventNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_EventNotificationMissWarningHash", report);
            StringAssert.Contains("_RandomEventSystemContextHash ^ _EventNotificationContextHash ^ unchecked((uint)eventType)", report);
            StringAssert.Contains("math.max(1, _eventNotificationMissCount)", report);
            StringAssert.Contains("_eventNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearEventNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearEventNotificationDiagnostics();", onDestroy);
        }

        [Test]
        public void RandomEventGameplayEventRefusalsReportBackpressureWithoutNoListenerNoise()
        {
            string source = ReadScript("Gameplay", "RandomEventSystem.cs");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");
            string startEvent = ExtractMethodBody(source, "private void StartEvent(");
            string onEventEnd = ExtractMethodBody(source, "private void OnEventEnd(");
            string cave = ExtractMethodBody(source, "private void TryTriggerCaveCollapse(");
            string started = ExtractMethodBody(source, "private void TryRaiseRandomEventStarted(");
            string ended = ExtractMethodBody(source, "private void TryRaiseRandomEventEnded(");
            string seismic = ExtractMethodBody(source, "private void TryRaiseRandomEventSeismicShockwave(");
            string report = ExtractMethodBody(source, "private void ReportRandomEventLaneDropIfBackpressured(");
            string clear = ExtractMethodBody(source, "private void ClearEventLaneDiagnostics()");

            StringAssert.Contains("public int PendingCount", source);
            StringAssert.Contains("public int EventLaneDropCount =>", source);
            StringAssert.Contains("private static readonly uint _EventLaneDropWarningHash", source);
            StringAssert.Contains("private static readonly uint _EventStartedContextHash", source);
            StringAssert.Contains("private static readonly uint _EventEndedContextHash", source);
            StringAssert.Contains("private static readonly uint _SeismicShockwaveContextHash", source);

            StringAssert.Contains("TryRaiseRandomEventStarted(type, intensity);", startEvent);
            StringAssert.DoesNotContain("RandomEventEvents.TryRaiseStarted(type, intensity);", startEvent);
            StringAssert.Contains("TryRaiseRandomEventEnded(type);", onEventEnd);
            StringAssert.DoesNotContain("RandomEventEvents.TryRaiseEnded(type);", onEventEnd);
            StringAssert.Contains("TryRaiseRandomEventEnded((RandomEventType)i);", onDisable);
            StringAssert.DoesNotContain("RandomEventEvents.TryRaiseEnded((RandomEventType)i);", onDisable);
            StringAssert.Contains("TryRaiseRandomEventSeismicShockwave(in seismicEvent);", cave);
            StringAssert.DoesNotContain("RandomEventEvents.TryRaiseSeismicShockwave(in seismicEvent);", cave);

            StringAssert.Contains("if (RandomEventEvents.TryRaiseStarted(type, intensity))", started);
            StringAssert.Contains("ReportRandomEventLaneDropIfBackpressured(_EventStartedContextHash ^ unchecked((uint)type));", started);
            StringAssert.Contains("if (RandomEventEvents.TryRaiseEnded(type))", ended);
            StringAssert.Contains("ReportRandomEventLaneDropIfBackpressured(_EventEndedContextHash ^ unchecked((uint)type));", ended);
            StringAssert.Contains("if (RandomEventEvents.TryRaiseSeismicShockwave(in payload))", seismic);
            StringAssert.Contains("ReportRandomEventLaneDropIfBackpressured(_SeismicShockwaveContextHash);", seismic);

            StringAssert.Contains("if (RandomEventEvents.PendingCount <= 0)", report);
            StringAssert.Contains("_eventLaneDropCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_EventLaneDropWarningHash", report);
            StringAssert.Contains("_RandomEventSystemContextHash ^ contextHash", report);
            StringAssert.Contains("math.max(1, _eventLaneDropCount)", report);
            AssertTextBefore(report, "if (RandomEventEvents.PendingCount <= 0)", "_eventLaneDropCount++;");

            StringAssert.Contains("_eventLaneDropCount = 0;", clear);
            StringAssert.Contains("ClearEventLaneDiagnostics();", onDisable);
            StringAssert.Contains("ClearEventLaneDiagnostics();", onDestroy);
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
