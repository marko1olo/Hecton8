using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonVoxelVolumeEventBridgeEditTests
    {
        [Test]
        public void VoxelCollapseSeismicEventRefusalStaysVisibleWithoutNoListenerNoise()
        {
            string source = ReadProjectFile("Assets/_Project/Scripts/HectonVoxelVolume.cs");
            string collapse = ExtractMethodBody(source, "private void ExecuteResourceCraterClusterCollapse(");
            string raise = ExtractMethodBody(source, "private void TryRaiseSeismicShockwaveEvent(");
            string report = ExtractMethodBody(source, "private void ReportSeismicShockwaveEventLaneDropIfBackpressured()");
            string clear = ExtractMethodBody(source, "private void ClearEventLaneDiagnostics()");
            string onDisable = ExtractMethodBody(source, "private void OnDisable()");
            string onDestroy = ExtractMethodBody(source, "private void OnDestroy()");

            StringAssert.Contains("public int SeismicShockwaveEventLaneDropCount =>", source);
            StringAssert.Contains("private static readonly uint _SeismicShockwaveEventLaneDropWarningHash", source);
            StringAssert.Contains("private static readonly uint _SeismicShockwaveEventLaneContextHash", source);

            StringAssert.Contains("TryRaiseSeismicShockwaveEvent(in shockwaveEvent);", collapse);
            StringAssert.DoesNotContain("RandomEventEvents.TryRaiseSeismicShockwave(in shockwaveEvent);", collapse);
            AssertTextBefore(collapse, "ApplyCollapseImpulse(runtimeCenter, halfExtents, impulseRadius, impulseMagnitude);", "TryRaiseSeismicShockwaveEvent(in shockwaveEvent);");

            StringAssert.Contains("if (RandomEventEvents.TryRaiseSeismicShockwave(in shockwaveEvent))", raise);
            StringAssert.Contains("ReportSeismicShockwaveEventLaneDropIfBackpressured();", raise);

            StringAssert.Contains("if (RandomEventEvents.PendingCount <= 0)", report);
            StringAssert.Contains("_seismicShockwaveEventLaneDropCount++;", report);
            StringAssert.Contains("GlobalTelemetryBus.PublishPerformanceWarning(", report);
            StringAssert.Contains("_SeismicShockwaveEventLaneDropWarningHash", report);
            StringAssert.Contains("_SeismicShockwaveEventLaneContextHash ^ unchecked((uint)_runtimeStamp)", report);
            StringAssert.Contains("math.max(1, _seismicShockwaveEventLaneDropCount)", report);
            AssertTextBefore(report, "if (RandomEventEvents.PendingCount <= 0)", "_seismicShockwaveEventLaneDropCount++;");

            StringAssert.Contains("_seismicShockwaveEventLaneDropCount = 0;", clear);
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
