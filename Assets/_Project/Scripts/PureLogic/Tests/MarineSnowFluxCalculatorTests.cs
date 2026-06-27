#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class MarineSnowFluxCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float surfaceProd = 100f;
            float depth = 100f;
            float sinkingSpeed = 30f;
            float reminRate = 0.858f;

            // Act
            float flux = MarineSnowFluxCalculator.Compute(surfaceProd, depth, sinkingSpeed, reminRate);

            // Assert: Verify expected output behaviour
            Assert.That(MarineSnowFluxCalculator.Compute(surfaceProd, 0f, sinkingSpeed, reminRate), Is.EqualTo(surfaceProd).Within(0.001f));
            Assert.That(flux, Is.EqualTo(28.418f).Within(0.01f), "Verify standard calculations return expected results.");

            float deepFlux = MarineSnowFluxCalculator.Compute(surfaceProd, 1000f, sinkingSpeed, reminRate);
            Assert.That(deepFlux, Is.EqualTo(4.812f).Within(0.01f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float flux = MarineSnowFluxCalculator.Compute(100f, 0f, 10f, 0.858f);

            // Act

            // Assert
            Assert.That(flux, Is.EqualTo(100f).Within(0.001f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float flux = MarineSnowFluxCalculator.Compute(0f, 100f, 0f, 0f);

            // Act

            // Assert
            Assert.That(flux, Is.EqualTo(0f).Within(0.001f), "Verify zero inputs are handled without divide-by-zero or exception.");

            float flux2 = MarineSnowFluxCalculator.Compute(100f, 100f, 0f, 0.858f);
            Assert.That(flux2, Is.GreaterThanOrEqualTo(0f));
            Assert.That(flux2, Is.LessThan(1f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float flux = MarineSnowFluxCalculator.Compute(-100f, -50f, -10f, -0.5f);

            // Act

            // Assert
            Assert.That(flux, Is.EqualTo(0f).Within(0.001f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float flux1 = MarineSnowFluxCalculator.Compute(float.PositiveInfinity, 100f, 10f, 0.858f);
            float flux2 = MarineSnowFluxCalculator.Compute(100f, float.NaN, 10f, 0.858f);

            // Act

            // Assert
            Assert.That(flux1, Is.EqualTo(0f).Within(0.001f), "Verify robust calculation and overflow protection.");
            Assert.That(flux2, Is.EqualTo(100f).Within(0.001f));
        }
    }
}
#endif
