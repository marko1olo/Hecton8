using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class GlobalShaderDispatcherRadiationSignalEditTests
    {
        [Test]
        public void GlobalShaderDispatcher_NormalizesRadiationDoseSignalsBeforeShaderPulse()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Rendering/GlobalShaderDispatcher.cs"));
            string exposureBody = ExtractMethodBody(source, "private static float ResolveRadiationExposureFromSignals()");
            string signalBody = ExtractMethodBody(source, "private static float ResolveRadiationSignalExposure01(");

            StringAssert.Contains("ReadOnlySpan<RadiationDoseSignal> snapshot = SignalBus<RadiationDoseSignal>.GetFrameSnapshot();", exposureBody);
            StringAssert.Contains("RadiationDoseSignal signal = snapshot[i];", exposureBody);
            StringAssert.Contains("float signalExposure = ResolveRadiationSignalExposure01(in signal);", exposureBody);
            StringAssert.Contains("exposure = math.max(exposure, signalExposure);", exposureBody);

            StringAssert.Contains("float intensity01 = math.saturate(math.select(0f, signal.Intensity01, math.isfinite(signal.Intensity01)));", signalBody);
            StringAssert.Contains("float dose01 = RadiationDoseSignal.DoseToUnit01(signal.Dose);", signalBody);
            StringAssert.Contains("return math.max(intensity01, dose01);", signalBody);
            AssertSourceOrder(signalBody, "math.isfinite(signal.Intensity01)", "return math.max(intensity01, dose01);");
            AssertSourceOrder(signalBody, "RadiationDoseSignal.DoseToUnit01(signal.Dose)", "return math.max(intensity01, dose01);");
            StringAssert.DoesNotContain("math.saturate(math.max(signal.Intensity01, signal.Dose))", exposureBody);
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
