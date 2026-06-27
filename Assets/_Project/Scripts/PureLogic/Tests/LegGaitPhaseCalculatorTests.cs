using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LegGaitPhaseCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            int totalLegs = 4;
            float gaitCycleTime = 2.0f;
            float currentTime = 0.5f;

            // Act
            float phase0 = LegGaitPhaseCalculator.Compute(0, totalLegs, gaitCycleTime, currentTime); // (0/4 + 0.5/2) = 0.25
            float phase1 = LegGaitPhaseCalculator.Compute(1, totalLegs, gaitCycleTime, currentTime); // (1/4 + 0.5/2) = 0.5
            float phase2 = LegGaitPhaseCalculator.Compute(2, totalLegs, gaitCycleTime, currentTime); // (2/4 + 0.5/2) = 0.75
            float phase3 = LegGaitPhaseCalculator.Compute(3, totalLegs, gaitCycleTime, currentTime); // (3/4 + 0.5/2) = 1.0 -> 0.0

            // Assert: Verify expected output behaviour
            Assert.AreEqual(0.25f, phase0, 0.0001f);
            Assert.AreEqual(0.50f, phase1, 0.0001f);
            Assert.AreEqual(0.75f, phase2, 0.0001f);
            Assert.AreEqual(0.00f, phase3, 0.0001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            int totalLegs = 4;
            float gaitCycleTime = 1.0f;
            float currentTime = 1.0f;

            // Act
            float phase = LegGaitPhaseCalculator.Compute(0, totalLegs, gaitCycleTime, currentTime);

            // Assert
            Assert.AreEqual(0.0f, phase, 0.0001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            int totalLegs = 4;
            float gaitCycleTime = 0.0f;
            float currentTime = 1.0f;

            // Act
            float phase = LegGaitPhaseCalculator.Compute(0, totalLegs, gaitCycleTime, currentTime);

            // Assert
            Assert.AreEqual(0.0f, phase, 0.0001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            int totalLegs = 4;
            float gaitCycleTime = 1.0f;
            float currentTime = -0.5f;

            // Act
            float phase = LegGaitPhaseCalculator.Compute(0, totalLegs, gaitCycleTime, currentTime);

            // Assert
            Assert.AreEqual(0.5f, phase, 0.0001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            int totalLegs = 4;
            float gaitCycleTime = float.PositiveInfinity;
            float currentTime = 1.0f;

            // Act
            float phase = LegGaitPhaseCalculator.Compute(0, totalLegs, gaitCycleTime, currentTime);

            // Assert
            Assert.AreEqual(0.0f, phase, 0.0001f);

            phase = LegGaitPhaseCalculator.Compute(0, totalLegs, 1.0f, float.NaN);
            Assert.AreEqual(0.0f, phase, 0.0001f);
        }

        [Test]
        public void Test_PhaseOffsets_Case06()
        {
            float p0 = LegGaitPhaseCalculator.Compute(0, 4, 1.0f, 0.0f);
            float p1 = LegGaitPhaseCalculator.Compute(1, 4, 1.0f, 0.0f);
            float p2 = LegGaitPhaseCalculator.Compute(2, 4, 1.0f, 0.0f);

            // 4 leg configuration: offset of 0.25 between legs
            Assert.AreEqual(0.25f, p1 - p0, 0.0001f);

            // Opposite legs (0 and 2 for a 4-leg setup) are 0.5 out of phase
            Assert.AreEqual(0.5f, p2 - p0, 0.0001f);
        }
    }
}
