using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SargassumKelpGrowthCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentSize = 10f;
            float maxClusterSize = 100f;
            float growthRate = 0.5f;
            float deltaHours = 2f;

            // Act
            float newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert: Verify expected output behaviour
            // Using logistic growth: K / (1 + ((K - S0)/S0) * e^(-rt))
            // 100 / (1 + ((100-10)/10) * e^(-0.5 * 2)) = 100 / (1 + 9 * e^-1) = 100 / (1 + 9 * 0.367879) = 100 / 4.310911 = 23.197f
            Assert.IsTrue(newSize > currentSize);
            Assert.IsTrue(newSize < maxClusterSize);
            Assert.AreEqual(23.196f, newSize, 0.01f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float currentSize = 100f;
            float maxClusterSize = 100f;
            float growthRate = 0.5f;
            float deltaHours = 2f;

            // Act
            float newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(100f, newSize, "Verify boundary constraints clamp correctly (already at max).");

            // Arrange another boundary: Current size greater than max
            currentSize = 120f;

            // Act
            newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(100f, newSize, "Verify current size greater than max clamps down to max.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float currentSize = 0f;
            float maxClusterSize = 100f;
            float growthRate = 0.5f;
            float deltaHours = 2f;

            // Act
            float newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(0f, newSize, "Verify zero current size remains zero.");

            // Arrange another: Zero max cluster size
            currentSize = 10f;
            maxClusterSize = 0f;

            // Act
            newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(0f, newSize, "Verify zero max size returns zero.");

            // Arrange another: Zero growth rate
            currentSize = 10f;
            maxClusterSize = 100f;
            growthRate = 0f;

            // Act
            newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(10f, newSize, "Verify zero growth rate maintains current size.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentSize = -10f;
            float maxClusterSize = 100f;
            float growthRate = 0.5f;
            float deltaHours = 2f;

            // Act
            float newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(0f, newSize, "Verify negative current size clamps to 0.");

            // Arrange another: Negative max size
            currentSize = 10f;
            maxClusterSize = -100f;

            // Act
            newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(0f, newSize, "Verify negative max size clamps to 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float currentSize = 10f;
            float maxClusterSize = 100f;
            float growthRate = float.PositiveInfinity;
            float deltaHours = 2f;

            // Act
            float newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(100f, newSize, "Verify robust calculation and overflow protection (growth rate +inf means it jumps to max).");

            // Arrange another: Huge delta time
            currentSize = 10f;
            growthRate = 0.5f;
            deltaHours = 1000f;

            // Act
            newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(100f, newSize, "Verify huge delta time approaches max size smoothly.");

            // Arrange another: NaN inputs
            currentSize = float.NaN;
            deltaHours = 2f;

            // Act
            newSize = SargassumKelpGrowthCurveCalculator.Compute(currentSize, maxClusterSize, growthRate, deltaHours);

            // Assert
            Assert.AreEqual(0f, newSize, "Verify NaN inputs are handled correctly (NaN current size -> 0).");
        }
    }
}
