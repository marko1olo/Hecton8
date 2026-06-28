using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ThrusterEfficiencyVsPressureCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float baseThrust = 100f;
            float depthPressureBar = 10f;
            float optimalPressureBar = 10f;
            float decayRate = 0.1f;

            // Act
            float result = ThrusterEfficiencyVsPressureCalculator.Compute(baseThrust, depthPressureBar, optimalPressureBar, decayRate);

            // Assert: Verify expected output behaviour
            // Diff = 0, exp(0) = 1, output = 100
            Assert.AreEqual(100f, result, 0.001f, "Maximum efficiency at optimal pressure.");

            // Sub-case: Slight deviation
            float result2 = ThrusterEfficiencyVsPressureCalculator.Compute(100f, 15f, 10f, 0.1f);
            // Diff = 5, exp(-0.1 * 5) = exp(-0.5) ≈ 0.60653
            Assert.AreEqual(60.653f, result2, 0.001f, "Efficiency decays when pressure deviates from optimal.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float baseThrust = 100f;
            float depthPressureBar = 1000f; // extreme depth
            float optimalPressureBar = 10f;
            float decayRate = 0.5f;

            // Act
            float result = ThrusterEfficiencyVsPressureCalculator.Compute(baseThrust, depthPressureBar, optimalPressureBar, decayRate);

            // Assert
            // Diff = 990, exp(-0.5 * 990) = exp(-495) which is essentially 0
            Assert.AreEqual(0f, result, 0.001f, "Extreme depth results in ~0 efficiency.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            float baseThrust = 0f;
            float depthPressureBar = 0f;
            float optimalPressureBar = 0f;
            float decayRate = 0f;

            // Act
            float result = ThrusterEfficiencyVsPressureCalculator.Compute(baseThrust, depthPressureBar, optimalPressureBar, decayRate);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Zero input values handled correctly.");

            // Sub-case: baseThrust > 0, other zeroes
            float result2 = ThrusterEfficiencyVsPressureCalculator.Compute(50f, 0f, 0f, 0f);
            Assert.AreEqual(50f, result2, 0.001f, "Zero pressure differences give full base thrust.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float baseThrust = -50f;
            float depthPressureBar = -10f;
            float optimalPressureBar = -10f;
            float decayRate = -0.5f;

            // Act
            float result = ThrusterEfficiencyVsPressureCalculator.Compute(baseThrust, depthPressureBar, optimalPressureBar, decayRate);

            // Assert
            // baseThrust clamped to 0, output is 0.
            Assert.AreEqual(0f, result, 0.001f, "Negative base thrust clamped to 0.");

            // Sub-case: positive base thrust, negative decay rate
            float result2 = ThrusterEfficiencyVsPressureCalculator.Compute(100f, 20f, 10f, -0.5f);
            // Decay rate clamped to 0, diff = 10. exp(0 * 10) = 1. output = 100.
            Assert.AreEqual(100f, result2, 0.001f, "Negative decay rate clamped to 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float baseThrust = float.PositiveInfinity;
            float depthPressureBar = float.NaN;
            float optimalPressureBar = float.MaxValue;
            float decayRate = float.MinValue;

            // Act
            float result = ThrusterEfficiencyVsPressureCalculator.Compute(baseThrust, depthPressureBar, optimalPressureBar, decayRate);

            // Assert
            Assert.AreEqual(0f, result, "NaNs and infinities return 0f.");

            float result2 = ThrusterEfficiencyVsPressureCalculator.Compute(float.NaN, 10f, 10f, 0.1f);
            Assert.AreEqual(0f, result2, "NaN base thrust returns 0f.");
        }
    }
}
