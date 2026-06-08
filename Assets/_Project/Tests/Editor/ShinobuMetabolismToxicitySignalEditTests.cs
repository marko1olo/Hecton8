using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ShinobuMetabolismToxicitySignalEditTests
    {
        [Test]
        public void ShinobuMetabolism_StagesAndPublishesOnlyFiniteToxicityExposure()
        {
            string runtime = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Physiology/ShinobuMetabolismRuntime.cs"));
            string jobs = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Physiology/ShinobuMetabolismJobs.cs"));
            string publishBody = ExtractMethodBody(runtime, "private void PublishStagedSignals(");
            string stageBody = ExtractMethodBody(jobs, "private void StageToxicityExposure(");

            Assert.That(runtime, Does.Not.Contain("private const uint ToxicityExposureLaneHash"));
            StringAssert.Contains("!math.all(math.isfinite(entityAup))", stageBody);
            StringAssert.Contains("!math.isfinite(magnitude)", stageBody);
            StringAssert.Contains("magnitude <= 0f", stageBody);
            StringAssert.Contains("return;", stageBody);
            StringAssert.Contains("exposure.AUP = entityAup;", stageBody);
            StringAssert.Contains("exposure.Exposure01 = math.saturate(magnitude);", stageBody);
            StringAssert.Contains("exposure.ToxemiaDelta = math.saturate(magnitude);", stageBody);
            AssertSourceOrder(stageBody, "!math.all(math.isfinite(entityAup))", "exposure.AUP = entityAup;");

            StringAssert.Contains("signal.EntityHash == 0u", publishBody);
            StringAssert.Contains("signal.Frame == 0u", publishBody);
            Assert.That(publishBody, Does.Not.Contain("!math.all(math.isfinite(signal.AUP)) ||"));
            StringAssert.Contains("!math.isfinite(signal.Exposure01)", publishBody);
            StringAssert.Contains("!math.isfinite(signal.ToxemiaDelta)", publishBody);
            StringAssert.Contains("signal.ToxemiaDelta <= 0f", publishBody);
            StringAssert.Contains("float exposure01 = math.saturate(signal.Exposure01);", publishBody);
            StringAssert.Contains("float toxemiaDelta = math.saturate(math.max(0f, signal.ToxemiaDelta));", publishBody);
            StringAssert.Contains("if (exposure01 <= 0.0001f && toxemiaDelta <= 0f)", publishBody);
            StringAssert.Contains("bool hasSourceAup = math.all(math.isfinite(signal.AUP)) && math.lengthsq(signal.AUP) > 0.000001d;", publishBody);
            StringAssert.Contains("if (hasSourceAup)", publishBody);
            StringAssert.Contains("exposure.AUP = signal.AUP;", publishBody);
            StringAssert.Contains("exposure.Exposure01 = exposure01;", publishBody);
            StringAssert.Contains("exposure.ToxemiaDelta = toxemiaDelta;", publishBody);
            StringAssert.Contains("exposure.ChemicalHash = signal.ChemicalHash != 0u ? signal.ChemicalHash : MetabolicToxicChemicalHash;", publishBody);
            StringAssert.Contains("exposure.Flags = ToxicityExposureSignal.FlagHasSourceAup;", publishBody);
            StringAssert.Contains("SignalBus<ToxicityExposureSignal>.TryPushTracked(in exposure, ref s_x001ShinobuMetabolismRuntimeSignalPushDropCount);", publishBody);
            AssertSourceOrder(publishBody, "!math.isfinite(signal.Exposure01)", "float exposure01 = math.saturate(signal.Exposure01);");
            AssertSourceOrder(publishBody, "!math.isfinite(signal.ToxemiaDelta)", "float toxemiaDelta = math.saturate(math.max(0f, signal.ToxemiaDelta));");
            AssertSourceOrder(publishBody, "bool hasSourceAup = math.all(math.isfinite(signal.AUP))", "ToxicityExposureSignal exposure = default;");
            AssertSourceOrder(publishBody, "exposure.AUP = signal.AUP;", "exposure.Flags = ToxicityExposureSignal.FlagHasSourceAup;");
            AssertSourceOrder(publishBody, "if (hasSourceAup)", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
            AssertSourceOrder(publishBody, "exposure.ToxemiaDelta = toxemiaDelta;", "SignalBus<ToxicityExposureSignal>.TryPushTracked");
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
