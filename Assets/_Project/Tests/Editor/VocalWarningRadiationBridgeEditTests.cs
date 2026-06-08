using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class VocalWarningRadiationBridgeEditTests
    {
        [Test]
        public void VocalWarningSystem_NormalizesRadiationDoseSeverityBeforeQueueing()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Audio/VocalWarningSystem.cs"));
            string warningJobBody = ExtractMethodBody(source, "private struct EvaluateWarningPrioritiesJob");
            string evaluateBody = ExtractMethodBody(warningJobBody, "public unsafe void Execute()");
            string radiationSeverityBody = ExtractMethodBody(source, "private static float ResolveRadiationWarningSeverity01(");

            StringAssert.Contains("RadiationSignals = SignalBus<RadiationDoseSignal>.GetFrameSnapshotArray(),", source);
            StringAssert.Contains("[ReadOnly, NoAlias] public NativeArray<RadiationDoseSignal>.ReadOnly RadiationSignals;", source);
            StringAssert.Contains("for (int i = 0; i < RadiationSignals.Length && evaluations < MaxEvaluations; i++)", evaluateBody);
            StringAssert.Contains("RadiationDoseSignal signal = RadiationSignals[i];", evaluateBody);
            StringAssert.Contains("float severity = ResolveRadiationWarningSeverity01(in signal);", evaluateBody);
            StringAssert.Contains("if (severity <= 0f)", evaluateBody);
            StringAssert.Contains("continue;", evaluateBody);
            StringAssert.Contains("ResolveCompassDirectionHash(in ListenerAup, in signal.PositionAup)", evaluateBody);
            StringAssert.Contains("TryQueue(VocalWarningHashes.Radiation, (byte)VocalWarningId.Radiation, severity", evaluateBody);
            StringAssert.Contains("float intensity = ResolveSeverity01(signal.Intensity01);", radiationSeverityBody);
            StringAssert.Contains("float dose = RadiationDoseSignal.DoseToUnit01(signal.Dose);", radiationSeverityBody);
            StringAssert.Contains("return math.max(intensity, dose);", radiationSeverityBody);
            AssertSourceOrder(evaluateBody, "float severity = ResolveRadiationWarningSeverity01(in signal);", "ResolveCompassDirectionHash(in ListenerAup, in signal.PositionAup)");
            AssertSourceOrder(evaluateBody, "if (severity <= 0f)", "TryQueue(VocalWarningHashes.Radiation, (byte)VocalWarningId.Radiation, severity");
            StringAssert.DoesNotContain("TryQueue(VocalWarningHashes.Radiation, (byte)VocalWarningId.Radiation, signal.Intensity01", evaluateBody);
            StringAssert.DoesNotContain("float severity = ResolveSeverity01(signal.Intensity01);", evaluateBody);
            StringAssert.DoesNotContain("ResolveSeverity01(signal.Dose * 0.01f)", radiationSeverityBody);
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
