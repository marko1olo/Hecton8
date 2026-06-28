using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FloodFillRoomVolumeCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            bool[,,] grid = new bool[3, 3, 3];
            // Setup a 2x2x2 cube of connected rooms
            for (int x = 0; x < 2; x++)
            {
                for (int y = 0; y < 2; y++)
                {
                    for (int z = 0; z < 2; z++)
                    {
                        grid[x, y, z] = true;
                    }
                }
            }
            float voxelSizeM = 2.0f; // Voxel volume = 8.0f

            // Act
            float result = FloodFillRoomVolumeCalculator.Compute(grid, 0, 0, 0, voxelSizeM);

            // Assert: Verify expected output behaviour
            // 8 connected rooms * 8.0f = 64.0f
            Assert.AreEqual(64.0f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            bool[,,] grid = new bool[1, 1, 1];
            grid[0, 0, 0] = true;

            // Act
            float resultOutBounds = FloodFillRoomVolumeCalculator.Compute(grid, 1, 0, 0, 1.0f);
            float resultNegativeBounds = FloodFillRoomVolumeCalculator.Compute(grid, -1, 0, 0, 1.0f);

            // Assert
            Assert.AreEqual(0.0f, resultOutBounds, "Verify boundary constraints clamp correctly (out of bounds returns 0).");
            Assert.AreEqual(0.0f, resultNegativeBounds, "Verify boundary constraints clamp correctly (negative index returns 0).");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            bool[,,] grid = new bool[2, 2, 2];
            grid[0, 0, 0] = true;

            // Act
            float resultZeroSize = FloodFillRoomVolumeCalculator.Compute(grid, 0, 0, 0, 0.0f);
            float resultNullGrid = FloodFillRoomVolumeCalculator.Compute(null, 0, 0, 0, 1.0f);

            // Assert
            Assert.AreEqual(0.0f, resultZeroSize, "Verify zero inputs are handled without divide-by-zero or exception.");
            Assert.AreEqual(0.0f, resultNullGrid, "Verify null grid is handled safely.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            bool[,,] grid = new bool[2, 2, 2];
            grid[0, 0, 0] = true;

            // Act
            float resultNegativeSize = FloodFillRoomVolumeCalculator.Compute(grid, 0, 0, 0, -1.0f);

            // Assert
            Assert.AreEqual(0.0f, resultNegativeSize, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            bool[,,] grid = new bool[2, 2, 2];
            grid[0, 0, 0] = true;

            // Act
            float resultInfinity = FloodFillRoomVolumeCalculator.Compute(grid, 0, 0, 0, float.PositiveInfinity);
            float resultNaN = FloodFillRoomVolumeCalculator.Compute(grid, 0, 0, 0, float.NaN);

            // Extreme large voxel size to test overflow prevention
            // Max float is ~3.4e38. Voxel size = 1e13 -> volume = 1e39 > float.MaxValue -> should clamp to float.MaxValue
            float resultOverflow = FloodFillRoomVolumeCalculator.Compute(grid, 0, 0, 0, 1e15f);

            // Assert
            Assert.AreEqual(0.0f, resultInfinity, "Verify infinity is handled.");
            Assert.AreEqual(0.0f, resultNaN, "Verify NaN is handled.");
            Assert.AreEqual(float.MaxValue, resultOverflow, "Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_PathBlocked_Case06()
        {
            // Arrange
            bool[,,] grid = new bool[3, 1, 1];
            grid[0, 0, 0] = true;
            grid[1, 0, 0] = false; // blocked
            grid[2, 0, 0] = true;

            // Act
            float result = FloodFillRoomVolumeCalculator.Compute(grid, 0, 0, 0, 1.0f);
            float result2 = FloodFillRoomVolumeCalculator.Compute(grid, 2, 0, 0, 1.0f);

            // Assert
            Assert.AreEqual(1.0f, result, "Verify separate regions are correctly isolated.");
            Assert.AreEqual(1.0f, result2, "Verify separate regions are correctly isolated.");
        }
    }
}
