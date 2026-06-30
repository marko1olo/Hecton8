using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LunarPhaseCalculatorTests
    {
        private float GetIllumination(float angleDegrees)
        {
            return 0.5f - 0.5f * (float)Math.Cos(angleDegrees * Math.PI / 180f);
        }

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float cycle = 100f;

            // Act
            float phase0 = LunarPhaseCalculator.Compute(0f, cycle);
            float phaseHalf = LunarPhaseCalculator.Compute(50f, cycle);
            float phaseQuarter = LunarPhaseCalculator.Compute(25f, cycle);
            float phaseFullWrap = LunarPhaseCalculator.Compute(100f, cycle);
            float phaseOverWrap = LunarPhaseCalculator.Compute(150f, cycle);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(0f, phase0, 0.001f);
            Assert.AreEqual(0f, GetIllumination(phase0), 0.001f); // New moon: illumination ~0

            Assert.AreEqual(180f, phaseHalf, 0.001f);
            Assert.AreEqual(1.0f, GetIllumination(phaseHalf), 0.001f); // Full moon: 1.0

            Assert.AreEqual(90f, phaseQuarter, 0.001f);
            Assert.AreEqual(0.5f, GetIllumination(phaseQuarter), 0.001f); // Half cycle: 0.5

            Assert.AreEqual(0f, phaseFullWrap, 0.001f);
            Assert.AreEqual(0f, GetIllumination(phaseFullWrap), 0.001f);

            Assert.AreEqual(180f, phaseOverWrap, 0.001f);
            Assert.AreEqual(1.0f, GetIllumination(phaseOverWrap), 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float cycle = 10f;

            // Act
            float phaseJustBeforeWrap = LunarPhaseCalculator.Compute(9.999f, cycle);
            float verySmallTime = LunarPhaseCalculator.Compute(0.0001f, cycle);

            // Assert
            Assert.AreEqual(359.964f, phaseJustBeforeWrap, 0.1f);
            Assert.AreEqual(0.0036f, verySmallTime, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float cycle = 0f;

            // Act
            float phaseZeroCycle = LunarPhaseCalculator.Compute(10f, cycle);

            // Assert
            Assert.AreEqual(0f, phaseZeroCycle, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float cycle = 10f;

            // Act
            float phaseNegativeTime = LunarPhaseCalculator.Compute(-5f, cycle);
            float phaseNegativeCycle = LunarPhaseCalculator.Compute(5f, -10f);

            // Assert
            Assert.AreEqual(0f, phaseNegativeTime, 0.001f);
            Assert.AreEqual(0f, phaseNegativeCycle, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float cycle = 10f;

            // Act
            float phaseNanTime = LunarPhaseCalculator.Compute(float.NaN, cycle);
            float phaseNanCycle = LunarPhaseCalculator.Compute(10f, float.NaN);
            float phaseInfTime = LunarPhaseCalculator.Compute(float.PositiveInfinity, cycle);
            float phaseInfCycle = LunarPhaseCalculator.Compute(10f, float.PositiveInfinity);

            // Assert
            Assert.AreEqual(0f, phaseNanTime, 0.001f);
            Assert.AreEqual(0f, phaseNanCycle, 0.001f);
            Assert.AreEqual(0f, phaseInfTime, 0.001f);
            Assert.AreEqual(0f, phaseInfCycle, 0.001f);
        }
    }
}
