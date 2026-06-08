using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class BioReactorMeltdownSignalEditTests
    {
        [Test]
        public void BioReactorMeltdownSignalsUseSingleFiniteSeverityBeforeFanOut()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/BioReactor.cs"));
            string helperBody = ExtractMethodBody(source, "private static float ResolveFiniteSeverity01(");
            string gasLeakBody = ExtractMethodBody(source, "private void PublishReactorGasLeak(");
            string statusBody = ExtractMethodBody(source, "private void QueueMeltdownPlayerStatus(");
            string radiationBody = ExtractMethodBody(source, "private void PublishMeltdownRadiationDose(");

            StringAssert.Contains("return math.isfinite(severity01) ? math.saturate(severity01) : 0f;", helperBody);
            StringAssert.Contains("float safeSeverity01 = ResolveFiniteSeverity01(severity01);", gasLeakBody);
            StringAssert.Contains("Damage01 = safeSeverity01,", gasLeakBody);
            StringAssert.Contains("ToxinLeak01 = safeSeverity01,", gasLeakBody);
            StringAssert.Contains("float severity01 = ResolveFiniteSeverity01(damage01);", statusBody);
            StringAssert.Contains("CombatStatusBits.Burning64", statusBody);
            StringAssert.Contains("CombatStatusBits.Irradiated64", statusBody);
            StringAssert.Contains("float severity01 = ResolveFiniteSeverity01(damage01);", radiationBody);
            StringAssert.Contains("SignalBus<RadiationDoseSignal>.TryPushTracked(in signal, ref s_x001BioReactorSignalPushDropCount);", radiationBody);
            AssertSourceOrder(statusBody, "float severity01 = ResolveFiniteSeverity01(damage01);", "CombatDamageRuntime.TryQueueStatusEffect(");
            AssertSourceOrder(radiationBody, "float severity01 = ResolveFiniteSeverity01(damage01);", "RadiationDoseSignal signal = default;");
            StringAssert.DoesNotContain("math.saturate(damage01)", statusBody);
            StringAssert.DoesNotContain("math.saturate(damage01)", radiationBody);
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
