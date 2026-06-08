using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HudNotificationFailureVisibilityEditTests
    {
        [Test]
        public void HudNotification_DirectEntrypointFailuresAreTelemetryVisibleAndLifecycleCleared()
        {
            string source = File.ReadAllText(Path.Combine(Application.dataPath, "_Project/Scripts/HUDNotification.cs"));
            string lateFrame = ExtractMethodBody(source, "public void LateFrameTick()");
            string drainSignals = ExtractMethodBody(source, "private void DrainHudNotificationSignalLane()");
            string pushSignal = ExtractMethodBody(source, "private void PushHudNotificationSignal(");
            string writeSignalMessage = ExtractMethodBody(source, "private bool TryWriteHudSignalMessage(");
            string writeSignalFallback = ExtractMethodBody(source, "private static bool TryWriteHudSignalFallback(");
            string reportSignalMiss = ExtractMethodBody(source, "private void ReportHudSignalMessageMiss(");
            string enqueueSpan = ExtractMethodBody(source, "private void Enqueue(ReadOnlySpan<char> message,");
            string enqueueFixed = ExtractMethodBody(source, "private void Enqueue(in FixedCharBuffer messageBuffer,");
            string enqueueHash = ExtractMethodBody(source, "private void Enqueue(uint messageHash,");
            string pushBack = ExtractMethodBody(source, "private bool PushQueueBack(");
            string insertFront = ExtractMethodBody(source, "private bool InsertQueueFront(");
            string reportRegistration = ExtractMethodBody(source, "private void ReportMessageRegistrationMiss(");
            string reportDrop = ExtractMethodBody(source, "private void ReportQueueDrop(");
            string reportWrite = ExtractMethodBody(source, "private void ReportQueueWriteMiss(");
            string clear = ExtractMethodBody(source, "private void ClearNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("public int MessageRegistrationMissCount => _messageRegistrationMissCount;", source);
            StringAssert.Contains("public int QueueDropCount => _queueDropCount;", source);
            StringAssert.Contains("public int QueueWriteMissCount => _queueWriteMissCount;", source);
            StringAssert.Contains("public int HudSignalMessageMissCount => _hudSignalMessageMissCount;", source);

            StringAssert.Contains("HudNotificationRegistrationMissWarningHash", source);
            StringAssert.Contains("HudNotificationQueueDropWarningHash", source);
            StringAssert.Contains("HudNotificationQueueWriteMissWarningHash", source);
            StringAssert.Contains("HudNotificationSignalMissWarningHash", source);
            StringAssert.Contains("HudNotificationContextHash", source);

            StringAssert.Contains("using Hecton8.Core.Contracts.Signals;", source);
            StringAssert.Contains("DrainHudNotificationSignalLane();", lateFrame);
            AssertTextBefore(lateFrame, "DrainHudNotificationSignalLane();", "if (!_presentationDirty && !_visualStyleDirty && !_textDirty)");
            StringAssert.Contains("SignalBus<HUDNotificationSignal>.TryConsumeFrame(out HUDNotificationSignal signal)", drainSignals);
            StringAssert.Contains("PushHudNotificationSignal(in signal);", drainSignals);
            StringAssert.Contains("TryWriteHudSignalMessage(in signal, out int length)", pushSignal);
            StringAssert.Contains("Enqueue(_hudSignalDecodeCharacters.AsSpan(0, length), ResolveSignalSeverity(signal.Severity));", pushSignal);
            StringAssert.Contains("LocRegistry.TryWriteVisualSpanFromUtf8(", writeSignalMessage);
            StringAssert.Contains("stripRichText: true", writeSignalMessage);
            StringAssert.Contains("ReportHudSignalMessageMiss(in signal);", writeSignalMessage);
            AssertTextBefore(writeSignalMessage, "ReportHudSignalMessageMiss(in signal);", "return false;");
            StringAssert.Contains("TryWriteHudSignalFallback(signal.MessageHash", writeSignalMessage);
            StringAssert.Contains("HudSignalFallbackPrefix.AsSpan().CopyTo(target);", writeSignalFallback);
            StringAssert.Contains("ToUpperHexNibble", writeSignalFallback);

            StringAssert.Contains("ReportMessageRegistrationMiss(severity);", enqueueSpan);
            AssertTextBefore(enqueueSpan, "ReportMessageRegistrationMiss(severity);", "return;");
            StringAssert.Contains("ReportMessageRegistrationMiss(severity);", enqueueFixed);
            AssertTextBefore(enqueueFixed, "ReportMessageRegistrationMiss(severity);", "return;");
            StringAssert.Contains("ReportMessageRegistrationMiss(severity);", enqueueHash);
            AssertTextBefore(enqueueHash, "ReportMessageRegistrationMiss(severity);", "return;");

            StringAssert.Contains("ReportQueueDrop(severity, messageHash);", enqueueHash);
            AssertTextBefore(enqueueHash, "ReportQueueDrop(severity, messageHash);", "return;");
            StringAssert.Contains("if (!PushQueueBack(new NotificationRequest", enqueueHash);
            StringAssert.Contains("ReportQueueWriteMiss(severity, messageHash);", enqueueHash);
            StringAssert.Contains("if (!InsertQueueFront(new NotificationRequest", enqueueHash);
            StringAssert.Contains("ReportQueueWriteMiss(_currentSeverity, _currentMessageHash);", enqueueHash);

            StringAssert.Contains("return false;", pushBack);
            StringAssert.Contains("return true;", pushBack);
            StringAssert.Contains("return false;", insertFront);
            StringAssert.Contains("return true;", insertFront);

            StringAssert.Contains("_messageRegistrationMissCount++;", reportRegistration);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", reportRegistration);
            StringAssert.Contains("HudNotificationRegistrationMissWarningHash", reportRegistration);
            StringAssert.Contains("math.max(1, _messageRegistrationMissCount)", reportRegistration);
            StringAssert.Contains("_queueDropCount++;", reportDrop);
            StringAssert.Contains("HudNotificationQueueDropWarningHash", reportDrop);
            StringAssert.Contains("math.max(1, _queueDropCount)", reportDrop);
            StringAssert.Contains("_queueWriteMissCount++;", reportWrite);
            StringAssert.Contains("HudNotificationQueueWriteMissWarningHash", reportWrite);
            StringAssert.Contains("math.max(1, _queueWriteMissCount)", reportWrite);
            StringAssert.Contains("_hudSignalMessageMissCount++;", reportSignalMiss);
            StringAssert.Contains("HudNotificationSignalMissWarningHash", reportSignalMiss);
            StringAssert.Contains("signal.MessageHash ^ signal.ContextHash", reportSignalMiss);
            StringAssert.Contains("math.max(1, _hudSignalMessageMissCount)", reportSignalMiss);

            StringAssert.Contains("_messageRegistrationMissCount = 0;", clear);
            StringAssert.Contains("_queueDropCount = 0;", clear);
            StringAssert.Contains("_queueWriteMissCount = 0;", clear);
            StringAssert.Contains("_hudSignalMessageMissCount = 0;", clear);
            StringAssert.Contains("ClearNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearNotificationDiagnostics();", onDestroy);
        }

        private static void AssertTextBefore(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, beforeIndex >= 0 ? beforeIndex : 0, StringComparison.Ordinal);
            Assert.GreaterOrEqual(beforeIndex, 0, "Missing token: " + before);
            Assert.Greater(afterIndex, beforeIndex, "Expected token order: " + before + " before " + after);
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
                if (source[i] == '{')
                    depth++;
                else if (source[i] == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(bodyStart, i - bodyStart + 1);
                }
            }

            Assert.Fail("Unclosed method body: " + signature);
            return string.Empty;
        }
    }
}
