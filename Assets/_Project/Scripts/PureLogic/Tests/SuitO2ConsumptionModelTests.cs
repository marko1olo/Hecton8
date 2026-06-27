using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SuitO2ConsumptionModelTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float baseRate = 10f;
            float depth = 1f;

            // Act: Rest (exertion 0) + intact suit (seal 1) -> base rate
            float result1 = SuitO2ConsumptionModel.Evaluate(0f, 1f, baseRate, depth);

            // Act: Max exertion (exertion 1) + broken seal (seal 0) -> 5x base
            float result2 = SuitO2ConsumptionModel.Evaluate(1f, 0f, baseRate, depth);

            // Act: Half exertion (0.5) + half seal (0.5) -> (1 + 1 + 1)x = 3x base
            float result3 = SuitO2ConsumptionModel.Evaluate(0.5f, 0.5f, baseRate, depth);

            // Assert: Verify expected output behaviour
            Assert.That(result1, Is.EqualTo(10f).Within(0.001f), "Rest and intact suit should equal base rate.");
            Assert.That(result2, Is.EqualTo(50f).Within(0.001f), "Max exertion and broken seal should equal 5x base rate.");
            Assert.That(result3, Is.EqualTo(30f).Within(0.001f), "Half exertion and half seal should equal 3x base rate.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float baseRate = 10f;
            float depth = 2f; // Check depth multiplier works at boundary

            // Act: Exertion > 1, Seal < 0 should clamp to 1 and 0
            float result1 = SuitO2ConsumptionModel.Evaluate(1.5f, -0.5f, baseRate, depth);

            // Act: Exertion < 0, Seal > 1 should clamp to 0 and 1
            float result2 = SuitO2ConsumptionModel.Evaluate(-0.5f, 1.5f, baseRate, depth);

            // Assert
            Assert.That(result1, Is.EqualTo(100f).Within(0.001f), "Upper boundary exertion and lower boundary seal should clamp and yield 5x * depth(2).");
            Assert.That(result2, Is.EqualTo(20f).Within(0.001f), "Lower boundary exertion and upper boundary seal should clamp and yield 1x * depth(2).");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            float zeroBaseRate = 0f;

            // Act
            float result1 = SuitO2ConsumptionModel.Evaluate(1f, 0f, zeroBaseRate, 5f);
            float result2 = SuitO2ConsumptionModel.Evaluate(0f, 1f, 10f, 0f); // Depth < 1 should clamp to 1

            // Assert
            Assert.That(result1, Is.EqualTo(0f).Within(0.001f), "Zero base rate should return 0.");
            Assert.That(result2, Is.EqualTo(10f).Within(0.001f), "Zero depth should clamp to 1 atm, returning base rate.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative inputs
            float negativeBaseRate = -5f;
            float negativeDepth = -2f;

            // Act
            float result = SuitO2ConsumptionModel.Evaluate(-1f, -1f, negativeBaseRate, negativeDepth);

            // Assert
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Negative base rate should clamp to 0, returning 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float infinityRate = float.PositiveInfinity;
            float nanExertion = float.NaN;

            // Act
            float result1 = SuitO2ConsumptionModel.Evaluate(nanExertion, 1f, 10f, 1f); // NaN exertion becomes 0
            float result2 = SuitO2ConsumptionModel.Evaluate(1f, 0f, 10f, float.NaN);   // NaN depth becomes 1
            float result3 = SuitO2ConsumptionModel.Evaluate(1f, 0f, infinityRate, 1f); // Inf base rate becomes 0

            // Assert
            Assert.That(result1, Is.EqualTo(10f).Within(0.001f), "NaN exertion should clamp to 0.");
            Assert.That(result2, Is.EqualTo(50f).Within(0.001f), "NaN depth should clamp to 1.");
            Assert.That(result3, Is.EqualTo(0f).Within(0.001f), "Infinity base rate should clamp to 0.");
        }
    }
}
