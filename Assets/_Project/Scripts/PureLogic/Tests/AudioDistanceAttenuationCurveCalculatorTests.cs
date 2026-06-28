using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AudioDistanceAttenuationCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float initialDb = 100f;
            float distance = 10f;
            float absorptionRateDbPerMeter = 0.5f;

            // Act
            float result = AudioDistanceAttenuationCurveCalculator.Compute(initialDb, distance, absorptionRateDbPerMeter);

            // Assert: Verify expected output behaviour
            // 20 * log10(10) + 0.5 * 10 = 20 * 1 + 5 = 25
            // 100 - 25 = 75
            Assert.That(result, Is.EqualTo(75f).Within(0.01f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float initialDb = 100f;
            float distance = 1f; // log10(1) = 0
            float absorptionRateDbPerMeter = 0.5f;

            // Act
            float result = AudioDistanceAttenuationCurveCalculator.Compute(initialDb, distance, absorptionRateDbPerMeter);

            // Assert
            // 20 * 0 + 0.5 * 1 = 0.5
            // 100 - 0.5 = 99.5
            Assert.That(result, Is.EqualTo(99.5f).Within(0.01f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float initialDb = 80f;
            float distance = 0f;
            float absorptionRateDbPerMeter = 0f;

            // Act
            float result = AudioDistanceAttenuationCurveCalculator.Compute(initialDb, distance, absorptionRateDbPerMeter);

            // Assert
            Assert.That(result, Is.EqualTo(80f).Within(0.01f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float initialDb = 50f;
            float distance = -10f;
            float absorptionRateDbPerMeter = -0.1f;

            // Act
            float result = AudioDistanceAttenuationCurveCalculator.Compute(initialDb, distance, absorptionRateDbPerMeter);

            // Assert
            // negative distance clamped to 0 -> returns initialDb
            Assert.That(result, Is.EqualTo(50f).Within(0.01f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float initialDb = float.PositiveInfinity;
            float distance = 100f;
            float absorptionRateDbPerMeter = 0.1f;

            // Act
            float result = AudioDistanceAttenuationCurveCalculator.Compute(initialDb, distance, absorptionRateDbPerMeter);

            // Assert
            Assert.That(result, Is.EqualTo(0f)); // handles infinity
        }

        [Test]
        public void Test_NaNInputs()
        {
            // Arrange
            float initialDb = float.NaN;
            float distance = 100f;
            float absorptionRateDbPerMeter = 0.1f;

            // Act
            float result = AudioDistanceAttenuationCurveCalculator.Compute(initialDb, distance, absorptionRateDbPerMeter);

            // Assert
            Assert.That(result, Is.EqualTo(0f)); // handles NaN
        }
    }
}
