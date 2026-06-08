using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class AbyssalThermalSourceSignalEditTests
    {
        [Test]
        public void AbyssalThermalManagerRejectsBadThermalSourceInputsBeforeSignalPublish()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/World/AbyssalThermalManager.cs"));
            string resetBody = ExtractMethodBody(source, "private static void ResetStaticRuntimeState()");
            string spatialBody = ExtractMethodBody(source, "private void RegisterThermalSpatialEvent(");
            string publishBody = ExtractMethodBody(source, "private static void PublishThermalSourceSignal(");

            StringAssert.Contains("Volatile.Write(ref s_x001AbyssalThermalManagerSignalPushDropCount, 0);", resetBody);
            StringAssert.Contains("!MathGuard.IsFinite(positionWS)", spatialBody);
            StringAssert.Contains("!math.isfinite(radiusWS)", spatialBody);
            StringAssert.Contains("radiusWS <= 0f", spatialBody);
            StringAssert.Contains("!math.isfinite(heatIntensity)", spatialBody);
            StringAssert.Contains("heatIntensity <= 0f", spatialBody);
            AssertSourceOrder(spatialBody, "!MathGuard.IsFinite(positionWS)", "PublishThermalSourceSignal(positionWS, radiusWS, heatIntensity, sourceId);");
            AssertSourceOrder(spatialBody, "heatIntensity <= 0f", "PublishThermalSourceSignal(positionWS, radiusWS, heatIntensity, sourceId);");

            StringAssert.Contains("!MathGuard.IsFinite(positionWS)", publishBody);
            StringAssert.Contains("!math.isfinite(radiusWS)", publishBody);
            StringAssert.Contains("radiusWS <= 0f", publishBody);
            StringAssert.Contains("!math.isfinite(heatIntensity)", publishBody);
            StringAssert.Contains("heatIntensity <= 0f", publishBody);
            StringAssert.Contains("SignalBus<ThermalSourceSignal>.TryPushTracked(in signal, ref s_x001AbyssalThermalManagerSignalPushDropCount);", publishBody);
            StringAssert.Contains("signal.RadiusMeters = radiusWS;", publishBody);
            StringAssert.Contains("signal.IntensityCelsiusPerSecond = heatIntensity;", publishBody);
            AssertSourceOrder(publishBody, "!MathGuard.IsFinite(positionWS)", "TryResolveAupFromRuntimeOrigin(positionWS");
            AssertSourceOrder(publishBody, "heatIntensity <= 0f", "ThermalSourceSignal signal = default;");
            StringAssert.DoesNotContain("signal.RadiusMeters = math.max(0f, radiusWS);", publishBody);
            StringAssert.DoesNotContain("signal.IntensityCelsiusPerSecond = math.max(0f, heatIntensity);", publishBody);
        }

        private static string ExtractMethodBody(string source, string signature)
        {
            int signatureIndex = source.IndexOf(signature, StringComparison.Ordinal);
            Assert.GreaterOrEqual(signatureIndex, 0, "Missing method signature: " + signature);
            int open = source.IndexOf('{', signatureIndex);
            Assert.GreaterOrEqual(open, 0, "Missing method open brace: " + signature);

            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{')
                    depth++;
                else if (c == '}')
                {
                    depth--;
                    if (depth == 0)
                        return source.Substring(open, i - open + 1);
                }
            }

            Assert.Fail("Missing method close brace: " + signature);
            return string.Empty;
        }

        private static void AssertSourceOrder(string source, string before, string after)
        {
            int beforeIndex = source.IndexOf(before, StringComparison.Ordinal);
            int afterIndex = source.IndexOf(after, StringComparison.Ordinal);

            Assert.GreaterOrEqual(beforeIndex, 0, "Missing source token: " + before);
            Assert.GreaterOrEqual(afterIndex, 0, "Missing source token: " + after);
            Assert.Less(beforeIndex, afterIndex);
        }
    }
}
