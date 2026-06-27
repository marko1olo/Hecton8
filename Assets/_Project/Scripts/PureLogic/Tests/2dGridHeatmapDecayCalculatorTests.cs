using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class _2dGridHeatmapDecayCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            byte[] initialGrid = new byte[] { 100, 200, 50, 0 };
            float decayRate = 0.5f;
            float deltaSeconds = 1f;

            // Act
            byte[] resultGrid = _2dGridHeatmapDecayCalculator.Compute(initialGrid, decayRate, deltaSeconds);

            // Assert: Verify expected output behaviour
            // 1 - (0.5 * 1.0) = 0.5 factor. 100 -> 50, 200 -> 100, 50 -> 25, 0 -> 0
            Assert.AreEqual(50, resultGrid[0]);
            Assert.AreEqual(100, resultGrid[1]);
            Assert.AreEqual(25, resultGrid[2]);
            Assert.AreEqual(0, resultGrid[3]);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            byte[] initialGrid = new byte[] { 255, 128 };

            // Should clamp at 0 and return all 0s
            float decayRate = 2f;
            float deltaSeconds = 1f;

            // Act
            byte[] resultGrid = _2dGridHeatmapDecayCalculator.Compute(initialGrid, decayRate, deltaSeconds);

            // Assert
            Assert.AreEqual(0, resultGrid[0]);
            Assert.AreEqual(0, resultGrid[1]);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            byte[] initialGrid = new byte[] { 100, 255 };
            float decayRate = 0f;
            float deltaSeconds = 0f;

            // Act
            byte[] resultGrid = _2dGridHeatmapDecayCalculator.Compute(initialGrid, decayRate, deltaSeconds);

            // Assert
            Assert.AreEqual(100, resultGrid[0]);
            Assert.AreEqual(255, resultGrid[1]);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            byte[] initialGrid = new byte[] { 50, 100 };
            float decayRate = -0.5f;
            float deltaSeconds = -1f;

            // Act
            byte[] resultGrid = _2dGridHeatmapDecayCalculator.Compute(initialGrid, decayRate, deltaSeconds);

            // Assert
            // Negative inputs clamp to 0, decay factor = 1 - (0 * 0) = 1
            Assert.AreEqual(50, resultGrid[0]);
            Assert.AreEqual(100, resultGrid[1]);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            byte[] initialGrid = new byte[] { 255 };
            float decayRate = float.PositiveInfinity;
            float deltaSeconds = 1f;

            // Act
            byte[] resultGrid = _2dGridHeatmapDecayCalculator.Compute(initialGrid, decayRate, deltaSeconds);

            // Assert
            // Infinity defaults/clamps to 0 for decay rate based on our logic, resulting in factor 1
            Assert.AreEqual(255, resultGrid[0]);

            // Test NaN
            resultGrid = _2dGridHeatmapDecayCalculator.Compute(initialGrid, float.NaN, float.NaN);
            Assert.AreEqual(255, resultGrid[0]);
        }
    }
}
