using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AtmosphereLeakRateCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float internalPressure = 101325f; // 1 atm
            float externalPressure = 0f;      // Vacuum
            float breachArea = 0.5f;          // 0.5 m^2
            float dischargeCoeff = 0.6f;      // Typical orifice
            float density = 1.225f;           // Standard air

            // Expected: 0.6 * 0.5 * sqrt(2 * 1.225 * 101325) = 0.3 * sqrt(248246.25) = 0.3 * 498.243 = 149.47
            float expected = 0.6f * 0.5f * MathF.Sqrt(2f * density * internalPressure);

            // Act
            float result = AtmosphereLeakRateCalculator.Compute(internalPressure, externalPressure, breachArea, dischargeCoeff, density);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.EqualTo(expected).Within(0.01f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Equal pressure -> Zero flow
            float internalPressure = 50000f;
            float externalPressure = 50000f;
            float breachArea = 1.0f;
            float dischargeCoeff = 1.0f;

            // Act
            float result = AtmosphereLeakRateCalculator.Compute(internalPressure, externalPressure, breachArea, dischargeCoeff);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            float internalPressure = 100000f;
            float externalPressure = 0f;

            // Act
            float result1 = AtmosphereLeakRateCalculator.Compute(internalPressure, externalPressure, 0f, 0.6f);
            float result2 = AtmosphereLeakRateCalculator.Compute(internalPressure, externalPressure, 1f, 0f);

            // Assert
            Assert.That(result1, Is.EqualTo(0f));
            Assert.That(result2, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // External pressure is higher than internal pressure -> flow is reversed or stopped (we return 0 for positive flow rate from internal -> external)
            float internalPressure = 50000f;
            float externalPressure = 100000f;

            // Act
            float result1 = AtmosphereLeakRateCalculator.Compute(internalPressure, externalPressure, 1.0f, 1.0f);
            float result2 = AtmosphereLeakRateCalculator.Compute(100000f, 0f, -1.0f, 0.5f);
            float result3 = AtmosphereLeakRateCalculator.Compute(100000f, 0f, 1.0f, -0.5f);

            // Assert
            Assert.That(result1, Is.EqualTo(0f));
            Assert.That(result2, Is.EqualTo(0f));
            Assert.That(result3, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float internalPressure = float.PositiveInfinity;
            float externalPressure = float.NaN;
            float breachArea = float.NegativeInfinity;
            float dischargeCoeff = float.MaxValue;
            float density = float.NaN;

            // Act
            float result = AtmosphereLeakRateCalculator.Compute(internalPressure, externalPressure, breachArea, dischargeCoeff, density);

            // Assert: Verify robust calculation and overflow protection (NaNs/Inf handle correctly, returning safely)
            // NaN handling replaces internal pressure with 0, external with 0, area with 0 -> Result should be 0.
            Assert.That(result, Is.EqualTo(0f));

            // Valid inputs but extreme size
            float result2 = AtmosphereLeakRateCalculator.Compute(1e10f, 0f, 1e5f, 1.0f);
            Assert.That(result2, Is.GreaterThan(0f));
            Assert.That(float.IsInfinity(result2) || float.IsNaN(result2), Is.False);
        }
    }
}
