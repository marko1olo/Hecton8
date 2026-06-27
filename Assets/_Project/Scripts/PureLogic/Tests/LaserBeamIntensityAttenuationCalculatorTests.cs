using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LaserBeamIntensityAttenuationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float initial = 100f;
            float distance = 10f;
            float waterCoeff = 0.05f;
            float density = 0.02f; // total = 0.07 * 10 = 0.7 -> exp(-0.7)

            // Act
            float result = LaserBeamIntensityAttenuationCalculator.Compute(initial, distance, waterCoeff, density);

            // Assert
            Assert.That(result, Is.EqualTo(100f * Math.Exp(-0.7f)).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float initial = 100f;
            float distance = 0f;
            float waterCoeff = 0.05f;
            float density = 0.02f;

            // Act
            float result = LaserBeamIntensityAttenuationCalculator.Compute(initial, distance, waterCoeff, density);

            // Assert
            Assert.That(result, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float initial = 0f;
            float distance = 10f;
            float waterCoeff = 0f;
            float density = 0f;

            // Act
            float result = LaserBeamIntensityAttenuationCalculator.Compute(initial, distance, waterCoeff, density);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float initial = -50f;
            float distance = -10f;
            float waterCoeff = -0.05f;
            float density = -0.02f;

            // Act
            float result = LaserBeamIntensityAttenuationCalculator.Compute(initial, distance, waterCoeff, density);

            // Assert (Expect clamped to 0 distance/intensity etc.)
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float initial = float.MaxValue;
            float distance = 1000f;
            float waterCoeff = 10f;
            float density = 10f;

            // Act
            float result = LaserBeamIntensityAttenuationCalculator.Compute(initial, distance, waterCoeff, density);

            // Assert (Huge attenuation makes intensity ~0)
            Assert.That(result, Is.EqualTo(0f));

            // Arrange NaN
            float resultNan = LaserBeamIntensityAttenuationCalculator.Compute(float.NaN, 10f, 0.1f, 0.1f);
            Assert.That(resultNan, Is.EqualTo(0f));
        }
    }
}
