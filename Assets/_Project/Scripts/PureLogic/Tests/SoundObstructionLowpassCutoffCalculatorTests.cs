using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SoundObstructionLowpassCutoffCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float baseCutoffHz = 22000f;
            float thickness = 10f; // 10cm
            float density = 2f;

            // Act
            float result = SoundObstructionLowpassCutoffCalculator.Compute(baseCutoffHz, thickness, density);

            // Assert
            Assert.Less(result, 22000f, "Cutoff should drop with obstruction.");
            Assert.Greater(result, 0f, "Cutoff should not go below 0.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float baseCutoffHz = 22000f;

            // Act
            float result1 = SoundObstructionLowpassCutoffCalculator.Compute(baseCutoffHz, 0f, 10f);
            float result2 = SoundObstructionLowpassCutoffCalculator.Compute(baseCutoffHz, 10f, 0f);

            // Assert
            Assert.AreEqual(22000f, result1, "0 thickness should return base cutoff.");
            Assert.AreEqual(22000f, result2, "0 density should return base cutoff.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange & Act
            float result = SoundObstructionLowpassCutoffCalculator.Compute(0f, 0f, 0f);

            // Assert
            Assert.AreEqual(0f, result, "All zero inputs should return 0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float baseCutoffHz = -1000f;
            float thickness = -10f;
            float density = -5f;

            // Act
            float result = SoundObstructionLowpassCutoffCalculator.Compute(baseCutoffHz, thickness, density);

            // Assert
            Assert.AreEqual(0f, result, "Negative base cutoff should clamp to 0.");

            float result2 = SoundObstructionLowpassCutoffCalculator.Compute(22000f, -10f, 5f);
            Assert.AreEqual(22000f, result2, "Negative thickness should clamp to 0, resulting in base cutoff.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Thick, high-density materials drop cutoff frequency to sub-100Hz muffles.
            float baseCutoffHz = 22000f;
            float thickness = 500f; // 500cm
            float density = 10f;

            // Act
            float result = SoundObstructionLowpassCutoffCalculator.Compute(baseCutoffHz, thickness, density);

            // Assert
            Assert.Less(result, 100f, "Thick, high-density materials should drop cutoff frequency to sub-100Hz.");
            Assert.GreaterOrEqual(result, 0f, "Should clamp to >= 0.");

            // Infinite
            float infResult = SoundObstructionLowpassCutoffCalculator.Compute(float.PositiveInfinity, 10f, 10f);
            Assert.AreEqual(0f, infResult, "Infinite base cutoff should return 0.");

            float infThickness = SoundObstructionLowpassCutoffCalculator.Compute(22000f, float.PositiveInfinity, 10f);
            Assert.AreEqual(0f, infThickness, "Infinite thickness should clamp to 0 cutoff (full obstruction).");
        }
    }
}
