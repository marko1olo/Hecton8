using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ShinobuPhysiologyRadiationDoseBridgeEditTests
    {
        [Test]
        public void ShinobuPhysiology_MapsRadiationDoseSignalsToBoundedIrradiatedSeverity()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Physiology/ShinobuPhysiologyRuntime.cs"));
            string ingestBody = ExtractMethodBody(source, "private static void IngestRadiationDoseSignals(");

            StringAssert.Contains("ReadOnlySpan<RadiationDoseSignal> signals = SignalBus<RadiationDoseSignal>.GetFrameSnapshot();", ingestBody);
            StringAssert.Contains("RadiationDoseSignal signal = signals[i];", ingestBody);
            StringAssert.Contains("float dose = math.max(0f, ShinobuPhysiologyJobMath.SanitizeFinite(signal.Dose, 0f));", ingestBody);
            StringAssert.Contains("float intensity = ShinobuPhysiologyJobMath.SanitizeUnit(signal.Intensity01);", ingestBody);
            StringAssert.Contains("float doseSeverity01 = RadiationDoseSignal.DoseToUnit01(dose);", ingestBody);
            StringAssert.Contains("severity01 = math.max(severity01, math.max(intensity, doseSeverity01));", ingestBody);
            StringAssert.Contains("pending.CombatStatusMask |= ShinobuCombatStatusBridgeBits.Irradiated;", ingestBody);
            AssertSourceOrder(ingestBody, "float doseSeverity01 = RadiationDoseSignal.DoseToUnit01(dose);", "severity01 = math.max(severity01, math.max(intensity, doseSeverity01));");
            StringAssert.DoesNotContain("math.saturate(dose * 0.01f)", ingestBody);
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
