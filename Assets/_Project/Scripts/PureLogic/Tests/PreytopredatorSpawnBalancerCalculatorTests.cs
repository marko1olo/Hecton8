using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class PreytopredatorSpawnBalancerCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            int preyCount = 100;
            int predatorCount = 5;
            float optimalRatio = 10f;

            // Act
            bool result = PreytopredatorSpawnBalancerCalculator.Compute(preyCount, predatorCount, optimalRatio);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.True, "Should allow spawn when prey/predator ratio is strictly greater than the optimal ratio.");

            bool resultOversaturated = PreytopredatorSpawnBalancerCalculator.Compute(10, 5, 10f);
            Assert.That(resultOversaturated, Is.False, "Should deny spawn when predator ratio is oversaturated relative to prey population.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            int preyCount = 100;
            int predatorCount = 10;
            float optimalRatio = 10f;

            // Act
            bool result = PreytopredatorSpawnBalancerCalculator.Compute(preyCount, predatorCount, optimalRatio);

            // Assert
            Assert.That(result, Is.True, "Should allow spawn when ratio is exactly the optimal ratio.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act & Assert
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(10, 0, 5f), Is.True, "Predator count 0 should always allow spawn.");
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(0, 10, 5f), Is.False, "Prey count 0 with >0 predators should deny spawn (unless optimal ratio is 0).");
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(0, 10, 0f), Is.True, "Prey count 0 with optimal ratio 0 should allow spawn.");
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(0, 0, 0f), Is.True, "All zero inputs should resolve safely without divide-by-zero, allowing spawn due to predator count 0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act & Assert
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(-50, 10, 5f), Is.False, "Negative prey count should be clamped to 0, rejecting spawn.");
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(100, -10, 5f), Is.True, "Negative predator count should be clamped to 0, allowing spawn unconditionally.");
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(10, 5, -5f), Is.True, "Negative optimal ratio should be clamped to 0, allowing spawn for any valid numbers.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act & Assert
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(int.MaxValue, 10, 5f), Is.True, "Extreme prey count should handle calculation gracefully.");
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(100, int.MaxValue, 5f), Is.False, "Extreme predator count should handle calculation gracefully.");
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(100, 10, float.NaN), Is.False, "NaN optimal ratio should be rejected.");
            Assert.That(PreytopredatorSpawnBalancerCalculator.Compute(100, 10, float.PositiveInfinity), Is.False, "Positive Infinity optimal ratio should be rejected.");
        }
    }
}
