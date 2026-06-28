using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VisibilityTurbidityCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float turbidity = 2f;
            float biolum = 10f;
            float baseVis = 100f;
            // Act
            float result = VisibilityTurbidityCalculator.Compute(turbidity, biolum, baseVis);
            // Assert: Verify expected output behaviour
            // clearWater = 100 / 2 = 50
            // biolum = 10
            // result = 50 + 10 = 60
            Assert.That(result, Is.EqualTo(60f).Within(0.001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxTurbidity = 1000f; // high turbidity
            float biolum = 20000f; // super bright
            float baseVis = 100f;

            // Act
            float result = VisibilityTurbidityCalculator.Compute(maxTurbidity, biolum, baseVis);

            // Assert:
            // clearWater = 100 / 1000 = 0.1
            // biolum = 20000
            // result = 20000.1
            Assert.That(result, Is.EqualTo(20000.1f).Within(0.001f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float turbidity = 0f;
            float biolum = 0f;
            float baseVis = 0f;

            // Act
            float result = VisibilityTurbidityCalculator.Compute(turbidity, biolum, baseVis);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float turbidity = -10f;
            float biolum = -5f;
            float baseVis = -100f;

            // Act
            float result = VisibilityTurbidityCalculator.Compute(turbidity, biolum, baseVis);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Verify negative inputs clamp gracefully.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float infinityValue = float.PositiveInfinity;
            float nanValue = float.NaN;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => VisibilityTurbidityCalculator.Compute(infinityValue, 0f, 100f));
            Assert.Throws<ArgumentException>(() => VisibilityTurbidityCalculator.Compute(0f, nanValue, 100f));
            Assert.Throws<ArgumentException>(() => VisibilityTurbidityCalculator.Compute(0f, 0f, infinityValue));

            // Also test extreme normal inputs to ensure no overflow on standard math
            float result = VisibilityTurbidityCalculator.Compute(float.MaxValue, 0f, 10f);
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Extremely high turbidity should result in 0 base visibility (ignoring biolum).");
        }
    }
}
