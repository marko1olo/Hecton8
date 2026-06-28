using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VoxelMeshHeightSeamBlendCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float voxelVertexY = 10f;
            float terrainHeightY = 10f;
            float blendWidth = 5f;

            // Act
            float result = VoxelMeshHeightSeamBlendCalculator.Compute(voxelVertexY, terrainHeightY, blendWidth);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(1.0f, result, 0.001f, "Verify standard calculations return expected results.");

            result = VoxelMeshHeightSeamBlendCalculator.Compute(10f, 12f, 5f);
            Assert.AreEqual(0.6f, result, 0.001f);

            result = VoxelMeshHeightSeamBlendCalculator.Compute(10f, 8f, 5f);
            Assert.AreEqual(0.6f, result, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float voxelVertexY = 10f;
            float terrainHeightY = 15f;
            float blendWidth = 5f;

            // Act
            float result1 = VoxelMeshHeightSeamBlendCalculator.Compute(voxelVertexY, terrainHeightY, blendWidth);
            float result2 = VoxelMeshHeightSeamBlendCalculator.Compute(voxelVertexY, 5f, blendWidth);

            // Assert
            Assert.AreEqual(0.0f, result1, 0.001f, "Verify boundary constraints clamp correctly.");
            Assert.AreEqual(0.0f, result2, 0.001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float voxelVertexY = 10f;
            float terrainHeightY = 10f;
            float blendWidth = 0f;

            // Act
            float result1 = VoxelMeshHeightSeamBlendCalculator.Compute(voxelVertexY, terrainHeightY, blendWidth);
            float result2 = VoxelMeshHeightSeamBlendCalculator.Compute(voxelVertexY, 11f, blendWidth);

            // Assert
            Assert.AreEqual(1.0f, result1, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
            Assert.AreEqual(0.0f, result2, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float voxelVertexY = -10f;
            float terrainHeightY = -8f;
            float blendWidth = 5f;
            float negBlendWidth = -5f;

            // Act
            float result1 = VoxelMeshHeightSeamBlendCalculator.Compute(voxelVertexY, terrainHeightY, blendWidth);
            float result2 = VoxelMeshHeightSeamBlendCalculator.Compute(voxelVertexY, terrainHeightY, negBlendWidth);

            // Assert
            Assert.AreEqual(0.6f, result1, 0.001f, "Verify negative inputs clamp gracefully or throw.");
            Assert.AreEqual(0.0f, result2, 0.001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float result1 = VoxelMeshHeightSeamBlendCalculator.Compute(float.PositiveInfinity, 10f, 5f);
            float result2 = VoxelMeshHeightSeamBlendCalculator.Compute(10f, float.NaN, 5f);
            float result3 = VoxelMeshHeightSeamBlendCalculator.Compute(10000000000f, 10000000000f, 5f);

            // Assert
            Assert.AreEqual(0.0f, result1, 0.001f, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(0.0f, result2, 0.001f, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(1.0f, result3, 0.001f, "Verify robust calculation and overflow protection.");
        }
    }
}
