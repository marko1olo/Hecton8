using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BaseAirlockEventBridgeEditTests
    {
        [Test]
        public void BaseAirlockEventLaneRefusalsReportBackpressureWithoutNoListenerNoise()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/Gameplay/BaseAirlock.cs");
            string startCycle = ExtractMethodBody(source, "private void StartCycle(");
            string finalizeCycle = ExtractMethodBody(source, "private void FinalizeCompletedCycle(");
            string environment = ExtractMethodBody(source, "private void ApplyCompletedEnvironmentTransition(");
            string manualOverride = ExtractMethodBody(source, "private void CompleteEmergencyOverride()");
            string lockdown = ExtractMethodBody(source, "public void SetEmergencyLockdown(");
            string overrideBlocked = ExtractMethodBody(source, "public void SetEmergencyLockdownOverrideBlocked(");
            string started = ExtractMethodBody(source, "private void TryRaiseAirlockCycleStarted(");
            string completed = ExtractMethodBody(source, "private void TryRaiseAirlockCycleCompleted(");
            string environmentChanged = ExtractMethodBody(source, "private void TryRaiseAirlockEnvironmentChanged(");
            string lockdownChanged = ExtractMethodBody(source, "private void TryRaiseAirlockEmergencyLockdownChanged()");
            string overrideBlockedChanged = ExtractMethodBody(source, "private void TryRaiseAirlockManualOverrideBlockedChanged()");
            string overrideCompleted = ExtractMethodBody(source, "private void TryRaiseAirlockManualOverrideCompleted()");
            string report = ExtractMethodBody(source, "private void ReportAirlockEventLaneDropIfBackpressured(");
            string clear = ExtractMethodBody(source, "private void ClearEventLaneDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("public int EventLaneDropCount =>", source);
            StringAssert.Contains("private static readonly uint AirlockEventLaneDropWarningHash", source);
            StringAssert.Contains("private static readonly uint AirlockEventLaneContextHash", source);

            StringAssert.Contains("TryRaiseAirlockCycleStarted(player);", startCycle);
            StringAssert.DoesNotContain("BaseAirlockEvents.TryRaiseCycleStarted(this, player);", startCycle);
            StringAssert.Contains("TryRaiseAirlockCycleCompleted(completedInteractor);", finalizeCycle);
            StringAssert.DoesNotContain("BaseAirlockEvents.TryRaiseCycleCompleted(this, completedInteractor);", finalizeCycle);
            StringAssert.Contains("TryRaiseAirlockEnvironmentChanged(player);", environment);
            StringAssert.DoesNotContain("BaseAirlockEvents.TryRaiseEnvironmentChanged(this, player);", environment);
            StringAssert.Contains("TryRaiseAirlockManualOverrideCompleted();", manualOverride);
            StringAssert.DoesNotContain("BaseAirlockEvents.TryRaiseManualOverrideCompleted(this);", manualOverride);
            StringAssert.Contains("TryRaiseAirlockEmergencyLockdownChanged();", lockdown);
            StringAssert.DoesNotContain("BaseAirlockEvents.TryRaiseEmergencyLockdownChanged(this);", lockdown);
            StringAssert.Contains("TryRaiseAirlockManualOverrideBlockedChanged();", overrideBlocked);
            StringAssert.DoesNotContain("BaseAirlockEvents.TryRaiseManualOverrideBlockedChanged(this);", overrideBlocked);

            StringAssert.Contains("if (BaseAirlockEvents.TryRaiseCycleStarted(this, interactor))", started);
            StringAssert.Contains("ReportAirlockEventLaneDropIfBackpressured(BaseAirlockEventType.CycleStarted);", started);
            StringAssert.Contains("if (BaseAirlockEvents.TryRaiseCycleCompleted(this, interactor))", completed);
            StringAssert.Contains("ReportAirlockEventLaneDropIfBackpressured(BaseAirlockEventType.CycleCompleted);", completed);
            StringAssert.Contains("if (BaseAirlockEvents.TryRaiseEnvironmentChanged(this, interactor))", environmentChanged);
            StringAssert.Contains("ReportAirlockEventLaneDropIfBackpressured(BaseAirlockEventType.EnvironmentChanged);", environmentChanged);
            StringAssert.Contains("if (BaseAirlockEvents.TryRaiseEmergencyLockdownChanged(this))", lockdownChanged);
            StringAssert.Contains("ReportAirlockEventLaneDropIfBackpressured(BaseAirlockEventType.EmergencyLockdownChanged);", lockdownChanged);
            StringAssert.Contains("if (BaseAirlockEvents.TryRaiseManualOverrideBlockedChanged(this))", overrideBlockedChanged);
            StringAssert.Contains("ReportAirlockEventLaneDropIfBackpressured(BaseAirlockEventType.ManualOverrideBlockedChanged);", overrideBlockedChanged);
            StringAssert.Contains("if (BaseAirlockEvents.TryRaiseManualOverrideCompleted(this))", overrideCompleted);
            StringAssert.Contains("ReportAirlockEventLaneDropIfBackpressured(BaseAirlockEventType.ManualOverrideCompleted);", overrideCompleted);

            StringAssert.Contains("if (BaseAirlockEvents.PendingCount <= 0)", report);
            StringAssert.Contains("_eventLaneDropCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("AirlockEventLaneDropWarningHash", report);
            StringAssert.Contains("AirlockEventLaneContextHash ^ unchecked((uint)eventType)", report);
            StringAssert.Contains("math.max(1, _eventLaneDropCount)", report);
            AssertTextBefore(report, "if (BaseAirlockEvents.PendingCount <= 0)", "_eventLaneDropCount++;");

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
