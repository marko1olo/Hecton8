using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class RadiationLeadShieldingCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float rawLevel = 100f;
            float thickness = 2f;
            float quality = 0.5f;

            // Act
            float result = RadiationLeadShieldingCalculator.Compute(rawLevel, thickness, quality);

            // Assert: Verify expected output behaviour
            float expected = 100f * (float)Math.Exp(-2f * 0.5f); // 100 * e^-1 ~ 36.78794f
            Assert.That(Math.Abs(result - expected), Is.LessThan(0.001f), "Standard calculation failed.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float rawLevel = 50f;

            // Act
            float resultZeroThickness = RadiationLeadShieldingCalculator.Compute(rawLevel, 0f, 1f);
            float resultZeroQuality = RadiationLeadShieldingCalculator.Compute(rawLevel, 5f, 0f);

            // Assert
            Assert.That(resultZeroThickness, Is.EqualTo(50f), "Zero thickness should result in no reduction.");
            Assert.That(resultZeroQuality, Is.EqualTo(50f), "Zero quality should result in no reduction.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float result = RadiationLeadShieldingCalculator.Compute(0f, 0f, 0f);

            // Assert
            Assert.That(result, Is.EqualTo(0f), "Zero inputs should yield 0 dose.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float negativeRaw = RadiationLeadShieldingCalculator.Compute(-50f, 1f, 1f);
            float negativeThickness = RadiationLeadShieldingCalculator.Compute(100f, -5f, 1f);
            float negativeQuality = RadiationLeadShieldingCalculator.Compute(100f, 5f, -1f);

            // Assert
            Assert.That(negativeRaw, Is.EqualTo(0f), "Negative raw radiation should clamp to 0.");
            Assert.That(negativeThickness, Is.EqualTo(100f), "Negative thickness should clamp to 0 (no reduction).");
            Assert.That(negativeQuality, Is.EqualTo(100f), "Negative quality should clamp to 0 (no reduction).");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float largeThicknessResult = RadiationLeadShieldingCalculator.Compute(1000000f, 1000f, 100f);
            float nanRaw = RadiationLeadShieldingCalculator.Compute(float.NaN, 1f, 1f);
            float infinityThickness = RadiationLeadShieldingCalculator.Compute(100f, float.PositiveInfinity, 1f);

            // Assert
            Assert.That(largeThicknessResult, Is.EqualTo(0f).Within(0.0001f), "Extreme shielding should reduce dose to 0.");
            Assert.That(nanRaw, Is.EqualTo(0f), "NaN raw level should return 0.");
            Assert.That(infinityThickness, Is.EqualTo(0f), "Infinity thickness should safely return 0.");
        }
    }
}
