using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class NitrogenNarcosisCriticalDepthCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float resultAt30m = NitrogenNarcosisCriticalDepthCalculator.Compute(30f, 0.21f, 0.79f);
            float resultAt60m = NitrogenNarcosisCriticalDepthCalculator.Compute(60f, 0.21f, 0.79f);

            Assert.AreEqual(0f, resultAt30m, 0.01f, "Verify narcosis onset at 30m standard air.");
            Assert.AreEqual(1f, resultAt60m, 0.01f, "Verify max narcosis around 60m standard air.");
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float resultBelow30m = NitrogenNarcosisCriticalDepthCalculator.Compute(29.9f, 0.21f, 0.79f);
            float resultNoN2 = NitrogenNarcosisCriticalDepthCalculator.Compute(100f, 1.0f, 0.0f);

            // (1.0, 1.0) must behave exactly as its normalized mix (0.5, 0.5).
            float resultExcessFractions = NitrogenNarcosisCriticalDepthCalculator.Compute(40f, 1.0f, 1.0f);
            float resultNormalizedMix = NitrogenNarcosisCriticalDepthCalculator.Compute(40f, 0.5f, 0.5f);

            // 50/50 mix at 40 m: PN2 = (1 + 40/10) * 0.5 = 2.5 atm, below the 3.16 atm onset,
            // so zero narcosis is the physically correct result at this depth.
            // At 70 m: PN2 = 8 * 0.5 = 4.0 atm, above onset -> intensity (4.0-3.16)/(5.53-3.16) ~ 0.354.
            float deepExcessFractions = NitrogenNarcosisCriticalDepthCalculator.Compute(70f, 1.0f, 1.0f);

            Assert.AreEqual(0f, resultBelow30m, "Below 30m with standard air should have 0 narcosis.");
            Assert.AreEqual(0f, resultNoN2, "100% O2 should result in 0 nitrogen narcosis regardless of depth.");
            Assert.AreEqual(resultNormalizedMix, resultExcessFractions, "Excess gas fractions must behave as their normalized mix.");
            Assert.AreEqual(0f, resultExcessFractions, "Normalized 50/50 mix at 40 m is below the narcosis onset partial pressure.");
            Assert.AreEqual(0.354f, deepExcessFractions, 0.005f, "Normalized 50/50 mix at 70 m must produce narcosis above onset.");
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float resultZeroDepth = NitrogenNarcosisCriticalDepthCalculator.Compute(0f, 0.21f, 0.79f);
            float resultAllZero = NitrogenNarcosisCriticalDepthCalculator.Compute(0f, 0f, 0f);

            Assert.AreEqual(0f, resultZeroDepth, "Zero depth should result in 0 narcosis.");
            Assert.AreEqual(0f, resultAllZero, "All zero inputs should return 0 narcosis.");
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float resultNegDepth = NitrogenNarcosisCriticalDepthCalculator.Compute(-50f, 0.21f, 0.79f);
            float resultNegGas = NitrogenNarcosisCriticalDepthCalculator.Compute(40f, -0.5f, -1.0f);

            Assert.AreEqual(0f, resultNegDepth, "Negative depth should clamp to 0 resulting in 0 narcosis.");
            Assert.AreEqual(0f, resultNegGas, "Negative gas fractions should clamp to 0 resulting in 0 narcosis.");
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float resultExtremeDepth = NitrogenNarcosisCriticalDepthCalculator.Compute(10000f, 0.21f, 0.79f);
            float resultNaNInput = NitrogenNarcosisCriticalDepthCalculator.Compute(float.NaN, float.NaN, float.NaN);
            float resultPosInfDepth = NitrogenNarcosisCriticalDepthCalculator.Compute(float.PositiveInfinity, 0.21f, 0.79f);
            float resultNegInfDepth = NitrogenNarcosisCriticalDepthCalculator.Compute(float.NegativeInfinity, 0.21f, 0.79f);

            Assert.AreEqual(1f, resultExtremeDepth, "Extreme depth should clamp output to max narcosis (1.0).");
            Assert.AreEqual(0f, resultNaNInput, "NaN inputs should be handled gracefully and produce 0 narcosis.");
            Assert.AreEqual(1f, resultPosInfDepth, "Positive infinity depth should result in max narcosis (1.0).");
            Assert.AreEqual(0f, resultNegInfDepth, "Negative infinity depth should result in 0 narcosis.");
            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
