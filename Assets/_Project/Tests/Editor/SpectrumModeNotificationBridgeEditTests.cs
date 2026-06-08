using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class SpectrumModeNotificationBridgeEditTests
    {
        [Test]
        public void SpectrumModeNotificationRefusalIsDiagnosticAfterModeShaderAndEventCommit()
        {
            string source = ReadScript("Visor", "SpectrumSystem.cs");
            string setMode = ExtractMethodBody(source, "public void SetMode(");
            string push = ExtractMethodBody(source, "private void TryPushModeNotification(");
            string report = ExtractMethodBody(source, "private void ReportModeNotificationMiss(");
            string clear = ExtractMethodBody(source, "private void ClearModeNotificationDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("private static readonly uint _ModeNotificationMissWarningHash", source);
            StringAssert.Contains("private static readonly uint _ModeNotificationContextHash", source);
            StringAssert.Contains("public int ModeNotificationMissCount =>", source);
            AssertTextBefore(setMode, "_currentMode = mode;", "ApplyShaderMode();");
            AssertTextBefore(setMode, "ApplyShaderMode();", "SpectrumEvents.TryRaiseModeChanged(mode);");
            AssertTextBefore(setMode, "SpectrumEvents.TryRaiseModeChanged(mode);", "VisorHUDController.PulseActiveControllers(0.2f, 4);");
            AssertTextBefore(setMode, "VisorHUDController.PulseActiveControllers(0.2f, 4);", "TryPushModeNotification(mode);");
            StringAssert.DoesNotContain("NotificationEvents.TryPushInfo(ResolveLocalizedModeNotification(mode));", setMode);

            StringAssert.Contains("NotificationEvents.TryPushInfo(ResolveLocalizedModeNotification(mode))", push);
            StringAssert.Contains("ReportModeNotificationMiss(mode);", push);
            StringAssert.Contains("_modeNotificationMissCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_ModeNotificationMissWarningHash", report);
            StringAssert.Contains("_SpectrumSystemContextHash ^ _ModeNotificationContextHash ^ unchecked((uint)mode)", report);
            StringAssert.Contains("math.max(1, _modeNotificationMissCount)", report);
            StringAssert.Contains("_modeNotificationMissCount = 0;", clear);
            StringAssert.Contains("ClearModeNotificationDiagnostics();", onDisable);
            StringAssert.Contains("ClearModeNotificationDiagnostics();", onDestroy);
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
