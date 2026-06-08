using System;
using System.IO;
using NUnit.Framework;

namespace Hecton8.Tests.Editor
{
    public sealed class RadiationHazardGridIodineDoseSignalEditTests
    {
        [Test]
        public void IodineReductionDoesNotPublishNegativeHazardDoseSignal()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/RadiationHazardGrid.cs"));
            string iodineBody = ExtractMethodBody(source, "private void ApplyIodineDoseReduction(");
            string pendingBody = ExtractMethodBody(source, "private void ApplyPendingIodineDoseReduction(");
            string drainBody = ExtractMethodBody(source, "private void DrainItemAcquiredSignals(");
            string publishBody = ExtractMethodBody(source, "private void PublishDoseSignal(");
            string geigerBody = ExtractMethodBody(source, "private void EmitGeigerIfNeeded(");

            StringAssert.Contains("_pendingExternalDoseRad = math.max(0f, pendingDose - pendingReduction);", iodineBody);
            StringAssert.Contains("_accumulatedRadiationDose = math.max(0f, accumulatedDose - doseReduction);", iodineBody);
            StringAssert.Contains("ApplyDoseToPlayerContext(playerContext, _accumulatedRadiationDose, _lastGridIntensity01);", iodineBody);
            StringAssert.Contains("ApplyIodineDoseReduction(playerContext, pendingReduction);", pendingBody);
            StringAssert.Contains("ApplyIodineDoseReduction(playerContext, IodineDoseReduction * quantity);", drainBody);
            AssertSourceOrder(iodineBody, "_accumulatedRadiationDose = math.max(0f, accumulatedDose - doseReduction);", "ApplyDoseToPlayerContext(playerContext, _accumulatedRadiationDose, _lastGridIntensity01);");
            StringAssert.DoesNotContain("PublishDoseSignal(in doseAup, -doseReductionRad", iodineBody);
            StringAssert.DoesNotContain("-doseReductionRad", iodineBody);
            StringAssert.DoesNotContain("AbsoluteUniversePosition doseAup", pendingBody);

            StringAssert.Contains("float safeDose = SanitizeNonNegative(dose);", publishBody);
            StringAssert.Contains("!AbsoluteUniversePosition.IsFinite(in positionAup)", publishBody);
            StringAssert.Contains("DumpBlackBox();", publishBody);
            StringAssert.Contains("Dose = safeDose", publishBody);
            AssertSourceOrder(publishBody, "!AbsoluteUniversePosition.IsFinite(in positionAup)", "RadiationDoseSignal signal = new RadiationDoseSignal");
            AssertSourceOrder(publishBody, "float safeDose = SanitizeNonNegative(dose);", "RadiationDoseSignal signal = new RadiationDoseSignal");

            StringAssert.Contains("!AbsoluteUniversePosition.IsFinite(in playerAup)", geigerBody);
            StringAssert.Contains("_geigerPhase = 0f;", geigerBody);
            StringAssert.Contains("DumpBlackBox();", geigerBody);
            AssertSourceOrder(geigerBody, "!AbsoluteUniversePosition.IsFinite(in playerAup)", "AcousticPingSignal signal = new AcousticPingSignal");
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
