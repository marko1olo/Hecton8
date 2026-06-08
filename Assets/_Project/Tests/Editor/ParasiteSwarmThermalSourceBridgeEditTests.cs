using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class ParasiteSwarmThermalSourceBridgeEditTests
    {
        [Test]
        public void ParasiteSwarm_SkipsInertThermalSourceSignalsBeforeCandidateCreation()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/VFX/Parasites/ParasiteSwarmGpuRuntime.cs"));
            string stageBody = ExtractMethodBody(source, "private static int StageThermalSourceSignals(");

            StringAssert.Contains("ReadOnlySpan<ThermalSourceSignal> signals = SignalBus<ThermalSourceSignal>.GetFrameSnapshot();", stageBody);
            StringAssert.Contains("ThermalSourceSignal signal = signals[i];", stageBody);
            StringAssert.Contains("!signal.PositionAup.IsFinite()", stageBody);
            StringAssert.Contains("!math.isfinite(signal.RadiusMeters)", stageBody);
            StringAssert.Contains("signal.RadiusMeters <= 0f", stageBody);
            StringAssert.Contains("!math.isfinite(signal.IntensityCelsiusPerSecond)", stageBody);
            StringAssert.Contains("signal.IntensityCelsiusPerSecond < tuning.ParasiteAttractionThreshold", stageBody);
            StringAssert.Contains("eligibleSignalCount++;", stageBody);
            StringAssert.Contains("candidate.AttractionRadius = math.max(0.25f, signal.RadiusMeters);", stageBody);
            AssertSourceOrder(stageBody, "signal.RadiusMeters <= 0f", "eligibleSignalCount++;");
            AssertSourceOrder(stageBody, "signal.RadiusMeters <= 0f", "candidate.AttractionRadius = math.max(0.25f, signal.RadiusMeters);");
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
