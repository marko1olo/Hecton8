using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class UpwellingNutrientFluxCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float velocity = 5f;
            float deep = 100f;
            float shallow = 20f;
            float mixingDepth = 10f;

            // Act
            float result = UpwellingNutrientFluxCalculator.Compute(velocity, deep, shallow, mixingDepth);

            // Assert: Verify expected output behaviour
            // 5 * (100 - 20) = 400
            Assert.AreEqual(400f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float velocity = 10f;
            float deep = 50f;
            float shallow = 50f; // Already mixed
            float mixingDepth = 100f;

            // Act
            float result = UpwellingNutrientFluxCalculator.Compute(velocity, deep, shallow, mixingDepth);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify already mixed returns 0.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float result1 = UpwellingNutrientFluxCalculator.Compute(0f, 100f, 10f, 50f);
            float result2 = UpwellingNutrientFluxCalculator.Compute(10f, 100f, 10f, 0f);

            // Assert
            Assert.AreEqual(0f, result1, 0.001f, "No upwelling means zero flux.");
            Assert.AreEqual(0f, result2, 0.001f, "Zero mixing depth should gracefully return 0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float result = UpwellingNutrientFluxCalculator.Compute(-5f, -100f, -20f, -10f);

            // Assert
            Assert.AreEqual(0f, result, 0.001f, "Verify negative inputs clamp gracefully.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float resultNaN = UpwellingNutrientFluxCalculator.Compute(float.NaN, 100f, 10f, 50f);
            float resultInf = UpwellingNutrientFluxCalculator.Compute(float.PositiveInfinity, 100f, 10f, 50f);
            float resultLarge = UpwellingNutrientFluxCalculator.Compute(float.MaxValue, float.MaxValue, 0f, 50f);

            // Assert
            Assert.AreEqual(0f, resultNaN, "Verify NaN inputs are handled safely.");
            Assert.AreEqual(0f, resultInf, "Verify Infinity inputs are handled safely.");
            Assert.AreEqual(float.MaxValue, resultLarge, "Verify large values are capped or robustly calculated.");
        }
    }
}
