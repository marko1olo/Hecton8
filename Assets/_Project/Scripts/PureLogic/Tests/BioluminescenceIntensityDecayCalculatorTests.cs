using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BioluminescenceIntensityDecayCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float emittedIntensity = 100f;
            float wavelengthNm = 475f; // Cyan-blue
            float distanceMeters = 50f;
            float waterClarity = 0.8f;

            // Act
            float result = BioluminescenceIntensityDecayCalculator.Compute(emittedIntensity, wavelengthNm, distanceMeters, waterClarity);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.GreaterThan(0f));
            Assert.That(result, Is.LessThan(emittedIntensity));
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Distance is 10 m, not 100 m: with clarity clamped to 0.01 and wavelength to
            // 750 the coefficient is k = 0.01 * 100 * 2 = 2, so 100 m gives e^-200 which
            // underflows float (min denormal ~1.4e-45) to an exact 0. At 10 m the result is
            // 100 * e^-20 ~ 2e-7 — still a rapid falloff, but representable and testable.
            float emittedIntensity = 100f;
            float wavelengthNm = 800f; // Beyond red
            float distanceMeters = 10f;
            float waterClarity = -1f; // Invalid clarity

            // Act
            float result = BioluminescenceIntensityDecayCalculator.Compute(emittedIntensity, wavelengthNm, distanceMeters, waterClarity);

            // Assert
            Assert.That(result, Is.GreaterThan(0f));
            Assert.That(result, Is.LessThan(emittedIntensity));

            // Wavelength should clamp to 750f, water clarity to 0.01f (very murky)
            // Expect rapid falloff for murky water and red wavelength
            Assert.That(result, Is.LessThan(1f));
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float emittedIntensity = 10f;
            float wavelengthNm = 450f;
            float distanceMeters = 0f;
            float waterClarity = 1f;

            // Act
            float result = BioluminescenceIntensityDecayCalculator.Compute(emittedIntensity, wavelengthNm, distanceMeters, waterClarity);

            // Assert
            // Zero distance: emitted == perceived
            Assert.That(result, Is.EqualTo(emittedIntensity).Within(0.0001f));
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float emittedIntensity = -50f;
            float wavelengthNm = -400f;
            float distanceMeters = -100f;
            float waterClarity = -0.5f;

            // Act
            float result = BioluminescenceIntensityDecayCalculator.Compute(emittedIntensity, wavelengthNm, distanceMeters, waterClarity);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float emittedIntensity = float.MaxValue;
            float wavelengthNm = 450f;
            float distanceMeters = float.MaxValue;
            float waterClarity = 1f;

            // Act
            float result = BioluminescenceIntensityDecayCalculator.Compute(emittedIntensity, wavelengthNm, distanceMeters, waterClarity);

            // Assert
            // Should not overflow, extreme distance means zero intensity
            Assert.That(result, Is.EqualTo(0f));

            float resultInf = BioluminescenceIntensityDecayCalculator.Compute(float.PositiveInfinity, wavelengthNm, 10f, 1f);
            Assert.That(resultInf, Is.EqualTo(0f));
            Assert.Pass("Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_WavelengthDependent_Case06()
        {
            // Arrange
            float emittedIntensity = 100f;
            float distanceMeters = 100f;
            float waterClarity = 1f;

            // Act
            float blueResult = BioluminescenceIntensityDecayCalculator.Compute(emittedIntensity, 450f, distanceMeters, waterClarity); // Blue
            float redResult = BioluminescenceIntensityDecayCalculator.Compute(emittedIntensity, 700f, distanceMeters, waterClarity); // Red

            // Assert: Blue travels further than red
            Assert.That(blueResult, Is.GreaterThan(redResult));
        }
    }
}
