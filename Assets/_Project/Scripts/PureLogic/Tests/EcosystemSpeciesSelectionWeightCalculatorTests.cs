using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class EcosystemSpeciesSelectionWeightCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float baseWeight = 2.0f;
            float creditCost = 10.0f;
            float currentAvailableCredits = 20.0f;

            // Act
            float result = EcosystemSpeciesSelectionWeightCalculator.Compute(baseWeight, creditCost, currentAvailableCredits);

            // Assert
            Assert.AreEqual(2.0f, result, 0.00001f, "When available credits exceed cost, base weight should be returned.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float baseWeight = 1.0f;
            float creditCost = 10.0f;
            float currentAvailableCredits1 = 9.9999f;
            float currentAvailableCredits2 = 9.9998f;

            // Act
            float result1 = EcosystemSpeciesSelectionWeightCalculator.Compute(baseWeight, creditCost, currentAvailableCredits1);
            float result2 = EcosystemSpeciesSelectionWeightCalculator.Compute(baseWeight, creditCost, currentAvailableCredits2);

            // Assert
            Assert.AreEqual(1.0f, result1, 0.00001f, "Margin of 0.0001f should allow selection.");
            Assert.AreEqual(0.0f, result2, 0.00001f, "Should return 0 when available credits fall below cost and margin.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float baseWeight = 1.0f;
            float creditCost = 0.0f;
            float currentAvailableCredits = 0.0f;

            // Act
            float result = EcosystemSpeciesSelectionWeightCalculator.Compute(baseWeight, creditCost, currentAvailableCredits);

            // Assert
            Assert.AreEqual(1.0f, result, 0.00001f, "Zero cost should return base weight.");

            Assert.AreEqual(0.0f, EcosystemSpeciesSelectionWeightCalculator.Compute(0.0f, 10.0f, 100.0f), 0.00001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float baseWeight = 1.5f;
            float creditCost = -5.0f;
            float currentAvailableCredits = -10.0f;

            // Act
            float result1 = EcosystemSpeciesSelectionWeightCalculator.Compute(baseWeight, creditCost, currentAvailableCredits);
            float result2 = EcosystemSpeciesSelectionWeightCalculator.Compute(-1.0f, 10.0f, 100.0f);

            // Assert
            Assert.AreEqual(1.5f, result1, 0.00001f, "Negative cost is treated as free, returning base weight.");
            Assert.AreEqual(0.0f, result2, 0.00001f, "Negative base weight should clamp/return 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float baseWeight = float.MaxValue;
            float creditCost = float.MaxValue;
            float currentAvailableCredits = float.MaxValue;

            // Act
            float result1 = EcosystemSpeciesSelectionWeightCalculator.Compute(baseWeight, creditCost, currentAvailableCredits);
            float result2 = EcosystemSpeciesSelectionWeightCalculator.Compute(float.NaN, 10.0f, 100.0f);
            float result3 = EcosystemSpeciesSelectionWeightCalculator.Compute(1.0f, float.PositiveInfinity, 100.0f);

            // Assert
            Assert.AreEqual(baseWeight, result1, 0.00001f, "Extreme values should handle correctly without exception.");
            Assert.AreEqual(0.0f, result2, 0.00001f, "NaN inputs should return 0.");
            Assert.AreEqual(0.0f, result3, 0.00001f, "Infinity inputs should return 0.");
        }
    }
}
