using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VoxelCellDirtystateBitHashingCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            int cellX = 10;
            int cellY = 20;
            int cellZ = 30;
            int gridDim = 1024; // Power of two usually

            // Act
            uint hash = VoxelCellDirtystateBitHashingCalculator.Compute(cellX, cellY, cellZ, gridDim);

            // Assert: Verify expected output behaviour
            Assert.That(hash, Is.GreaterThanOrEqualTo(0u));
            Assert.That(hash, Is.LessThan((uint)gridDim));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs
            int cellX = 0;
            int cellY = 0;
            int cellZ = 0;
            int gridDim = 16;

            // Act
            uint hash1 = VoxelCellDirtystateBitHashingCalculator.Compute(cellX, cellY, cellZ, gridDim);
            uint hash2 = VoxelCellDirtystateBitHashingCalculator.Compute(cellX, cellY, cellZ, gridDim);

            // Assert: verify identical inputs give identical outputs
            Assert.That(hash1, Is.EqualTo(hash2));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            int cellX = 5;
            int cellY = 5;
            int cellZ = 5;
            int gridDim = 0;

            // Act
            uint hash = VoxelCellDirtystateBitHashingCalculator.Compute(cellX, cellY, cellZ, gridDim);

            // Assert
            Assert.That(hash, Is.EqualTo(0u));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            int cellX = -10;
            int cellY = -20;
            int cellZ = -30;
            int gridDim = 256;

            // Act
            uint hash1 = VoxelCellDirtystateBitHashingCalculator.Compute(cellX, cellY, cellZ, gridDim);
            uint hash2 = VoxelCellDirtystateBitHashingCalculator.Compute(cellX + 1, cellY, cellZ, gridDim);

            // Assert: negative inputs produce valid hashes within range, and adjacent ones differ
            Assert.That(hash1, Is.LessThan((uint)gridDim));
            Assert.That(hash1, Is.Not.EqualTo(hash2));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            int cellX = int.MaxValue;
            int cellY = int.MinValue;
            int cellZ = int.MaxValue;
            int gridDim = 4096;

            // Act
            uint hash = VoxelCellDirtystateBitHashingCalculator.Compute(cellX, cellY, cellZ, gridDim);

            // Assert
            Assert.That(hash, Is.LessThan((uint)gridDim));
        }
    }
}
