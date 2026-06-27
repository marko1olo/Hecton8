using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SplashEntryAngleCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float entryAngleDeg = 45f;
            float projectileMass = 10f;
            float velocity = 100f;
            float waterSurfaceTension = 50f;

            // Act
            float result = SplashEntryAngleCalculator.Compute(entryAngleDeg, projectileMass, velocity, waterSurfaceTension);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.GreaterThan(0f).And.LessThan(1f), "Expected a ricochet probability strictly between 0 and 1 for 45 degrees.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float angle90 = 90f;
            float angleUnder10 = 5f;

            // Act
            float prob90 = SplashEntryAngleCalculator.Compute(angle90, 10f, 100f, 50f);
            float probUnder10 = SplashEntryAngleCalculator.Compute(angleUnder10, 10f, 100f, 50f);

            // Assert
            Assert.That(prob90, Is.EqualTo(0f), "90 degrees should result in zero ricochet.");
            Assert.That(probUnder10, Is.GreaterThanOrEqualTo(0.8f), "Under 10 degrees should result in high ricochet (>= 0.8f).");

            // Heavier less deflection (ricochet prob is lower)
            float probLight = SplashEntryAngleCalculator.Compute(45f, 1f, 100f, 50f);
            float probHeavy = SplashEntryAngleCalculator.Compute(45f, 1000f, 100f, 50f);
            Assert.That(probHeavy, Is.LessThan(probLight), "Heavier mass should result in lower ricochet probability.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float probZeroMass = SplashEntryAngleCalculator.Compute(45f, 0f, 100f, 50f);
            float probZeroVelocity = SplashEntryAngleCalculator.Compute(45f, 10f, 0f, 50f);
            float probZeroTension = SplashEntryAngleCalculator.Compute(45f, 10f, 100f, 0f);

            // Assert
            Assert.DoesNotThrow(() => SplashEntryAngleCalculator.Compute(0f, 0f, 0f, 0f), "Zero inputs should not throw exceptions.");
            Assert.That(probZeroVelocity, Is.EqualTo(0f), "Zero velocity should yield 0 ricochet probability.");
            // Mass is clamped to 1f, tension of 0 means base tension influence.
            Assert.That(probZeroMass, Is.GreaterThan(0f), "Zero mass should be clamped and evaluated normally.");
            Assert.That(probZeroTension, Is.GreaterThan(0f), "Zero tension should be clamped and evaluated normally.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float resultNegAngle = SplashEntryAngleCalculator.Compute(-10f, 10f, 100f, 50f);
            float resultNegMass = SplashEntryAngleCalculator.Compute(45f, -10f, 100f, 50f);
            float resultNegVelocity = SplashEntryAngleCalculator.Compute(45f, 10f, -100f, 50f);
            float resultNegTension = SplashEntryAngleCalculator.Compute(45f, 10f, 100f, -50f);

            // Assert
            Assert.That(resultNegAngle, Is.GreaterThanOrEqualTo(0.8f), "Negative angle clamped to 0, which is under 10 degrees (high ricochet).");
            Assert.That(resultNegVelocity, Is.EqualTo(0f), "Negative velocity should clamp to 0 resulting in 0 probability.");
            Assert.That(resultNegMass, Is.GreaterThan(0f), "Negative mass should be sanitized to 1f.");
            Assert.That(resultNegTension, Is.GreaterThan(0f), "Negative tension should be sanitized to 0f.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float probNaNAll = SplashEntryAngleCalculator.Compute(float.NaN, float.NaN, float.NaN, float.NaN);
            float probInfAll = SplashEntryAngleCalculator.Compute(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            float probLargeVelocity = SplashEntryAngleCalculator.Compute(5f, 10f, float.MaxValue, 50f);

            // Assert
            Assert.DoesNotThrow(() => SplashEntryAngleCalculator.Compute(float.NaN, float.NaN, float.NaN, float.NaN));
            Assert.That(probNaNAll, Is.EqualTo(0f), "NaN angle becomes 90 which yields 0 probability.");
            Assert.That(probInfAll, Is.EqualTo(0f), "Inf angle becomes 90 which yields 0 probability.");

            Assert.That(float.IsNaN(probLargeVelocity), Is.False, "Calculation should not overflow to NaN.");
            Assert.That(probLargeVelocity, Is.GreaterThanOrEqualTo(0f).And.LessThanOrEqualTo(1f), "Large values should clamp probability correctly.");
        }
    }
}
