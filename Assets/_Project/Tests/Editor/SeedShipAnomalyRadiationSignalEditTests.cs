using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class SeedShipAnomalyRadiationSignalEditTests
    {
        [Test]
        public void SeedShipAnomalyRadiationExportFailsClosedBeforeSignalBus()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/World/SeedShipAnomaly/SeedShipAnomalyRuntime.cs"));
            string publishBody = ExtractMethodBody(source, "private void PublishLateFrameSignals(");
            string removeBody = ExtractMethodBody(source, "private void PublishRadiationSourceRemove()");
            string saturateBody = ExtractMethodBody(source, "private static float SaturateFinite01(");
            string positiveBody = ExtractMethodBody(source, "private static float PositiveFiniteOrZero(");

            StringAssert.Contains("float radiation01 = SaturateFinite01(globals.Radiation01);", publishBody);
            StringAssert.Contains("float sourceIntensity01 = SaturateFinite01(thermo.Radiation01);", publishBody);
            StringAssert.Contains("float sourceRadiusMeters = PositiveFiniteOrZero(thermo.RadiusMeters);", publishBody);
            StringAssert.Contains("if (sourceIntensity01 > 0.0001f && sourceRadiusMeters > 0f)", publishBody);
            StringAssert.Contains("RadiusMeters = sourceRadiusMeters,", publishBody);
            StringAssert.Contains("_radiationSourceActive = true;", publishBody);
            StringAssert.Contains("else if (_radiationSourceActive)", publishBody);
            StringAssert.Contains("PublishRadiationSourceRemove();", publishBody);
            StringAssert.Contains("if (radiation01 > 0.0001f)", publishBody);
            StringAssert.Contains("Dose = radiation01 * RadiationDosePerSecondScale * RadiationExportSlowTickSeconds,", publishBody);
            StringAssert.Contains("return math.isfinite(value) ? math.saturate(value) : 0f;", saturateBody);
            StringAssert.Contains("return math.isfinite(value) && value > 0f ? value : 0f;", positiveBody);
            StringAssert.Contains("Operation = RadiationSourceSignal.OperationRemove,", removeBody);
            StringAssert.Contains("_radiationSourceActive = false;", removeBody);
            AssertSourceOrder(publishBody, "float sourceRadiusMeters = PositiveFiniteOrZero(thermo.RadiusMeters);", "SignalBus<RadiationSourceSignal>.TryPushTracked(new RadiationSourceSignal");
            AssertSourceOrder(publishBody, "if (sourceIntensity01 > 0.0001f && sourceRadiusMeters > 0f)", "RadiusMeters = sourceRadiusMeters,");
            StringAssert.DoesNotContain("float radiation01 = math.saturate(globals.Radiation01);", publishBody);
            StringAssert.DoesNotContain("float sourceIntensity01 = math.saturate(thermo.Radiation01);", publishBody);
            StringAssert.DoesNotContain("RadiusMeters = thermo.RadiusMeters,", publishBody);
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
