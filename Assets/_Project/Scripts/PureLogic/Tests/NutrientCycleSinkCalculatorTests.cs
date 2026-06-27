using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class NutrientCycleSinkCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float deadBiomass = 100f;
            float decompositionRate = 0.1f;
            float nutrientPool = 50f;
            float deltaTime = 2f;

            // Act
            float newNutrientPool = NutrientCycleSinkCalculator.Compute(deadBiomass, decompositionRate, nutrientPool, deltaTime);

            // Assert: Verify expected output behaviour
            // Converted = 100 * 0.1 * 2 = 20. new pool = 50 + 20 = 70.
            Assert.AreEqual(70f, newNutrientPool, 0.0001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Test that we cannot convert more than the dead biomass available
            float deadBiomass = 10f;
            float decompositionRate = 1f; // 100% per sec
            float nutrientPool = 20f;
            float deltaTime = 5f; // would be 10 * 1 * 5 = 50 converted

            // Act
            float newNutrientPool = NutrientCycleSinkCalculator.Compute(deadBiomass, decompositionRate, nutrientPool, deltaTime);

            // Assert
            // Converted capped at 10. new pool = 20 + 10 = 30.
            Assert.AreEqual(30f, newNutrientPool, 0.0001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float deadBiomass = 0f;
            float decompositionRate = 0.5f;
            float nutrientPool = 100f;
            float deltaTime = 1f;

            // Act
            float newNutrientPool = NutrientCycleSinkCalculator.Compute(deadBiomass, decompositionRate, nutrientPool, deltaTime);

            // Assert
            Assert.AreEqual(100f, newNutrientPool, 0.0001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float deadBiomass = -50f;
            float decompositionRate = -0.1f;
            float nutrientPool = -10f; // should clamp to 0 based on implementation Math.Max(0f, nutrientPool) when deadBiomass <= 0
            float deltaTime = -2f;

            // Act
            float newNutrientPool = NutrientCycleSinkCalculator.Compute(deadBiomass, decompositionRate, nutrientPool, deltaTime);

            // Assert
            Assert.AreEqual(0f, newNutrientPool, 0.0001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float deadBiomass = float.PositiveInfinity;
            float decompositionRate = 0.5f;
            float nutrientPool = float.NaN;
            float deltaTime = 1f;

            // Act
            float newNutrientPool = NutrientCycleSinkCalculator.Compute(deadBiomass, decompositionRate, nutrientPool, deltaTime);

            // Assert
            // Infinity/NaN converted to 0. So no nutrient gain, nutrient pool reset to 0.
            Assert.AreEqual(0f, newNutrientPool, 0.0001f);
        }
    }
}
