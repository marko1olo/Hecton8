using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VoxelSdfTrilinearInterpolationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float[] corners = new float[] { 0f, 10f, 0f, 10f, 0f, 10f, 0f, 10f };

            // Act
            // tx=0.5 -> x midpoint -> 5f
            // ty=0.5 -> y midpoint -> 5f
            // tz=0.5 -> z midpoint -> 5f
            float result = VoxelSdfTrilinearInterpolationCalculator.Compute(corners, 0.5f, 0.5f, 0.5f);

            // Assert
            Assert.That(result, Is.EqualTo(5f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float[] corners = new float[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };

            // Act
            float r000 = VoxelSdfTrilinearInterpolationCalculator.Compute(corners, 0f, 0f, 0f);
            float r111 = VoxelSdfTrilinearInterpolationCalculator.Compute(corners, 1f, 1f, 1f);

            // Assert
            Assert.That(r000, Is.EqualTo(1f));
            Assert.That(r111, Is.EqualTo(8f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float[] corners = new float[] { 0f, 0f, 0f, 0f, 0f, 0f, 0f, 0f };

            // Act
            float result = VoxelSdfTrilinearInterpolationCalculator.Compute(corners, 0.5f, 0.5f, 0.5f);

            // Assert
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float[] corners = new float[] { 1f, 2f, 3f, 4f, 5f, 6f, 7f, 8f };

            // Act - negative weights should be clamped to 0
            float resultNegative = VoxelSdfTrilinearInterpolationCalculator.Compute(corners, -1f, -2f, -3f);

            // Act - weights > 1 should be clamped to 1
            float resultOverOne = VoxelSdfTrilinearInterpolationCalculator.Compute(corners, 1.5f, 2f, 5f);

            // Assert
            Assert.That(resultNegative, Is.EqualTo(1f)); // c000
            Assert.That(resultOverOne, Is.EqualTo(8f)); // c111
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float[] corners = new float[] { 1f, float.NaN, float.PositiveInfinity, 4f, 5f, float.NegativeInfinity, 7f, 8f };

            // Act
            float result = VoxelSdfTrilinearInterpolationCalculator.Compute(corners, float.NaN, float.PositiveInfinity, float.NegativeInfinity);

            // Assert
            // localX=NaN->0, localY=Infinity->0, localZ=-Infinity->0. Expect c000 (1f).
            Assert.That(result, Is.EqualTo(1f));

            // localX=1, localY=0, localZ=0 -> expecting to access c100, which was NaN, so it's treated as 0f.
            float resultCorner1 = VoxelSdfTrilinearInterpolationCalculator.Compute(corners, 1f, 0f, 0f);
            Assert.That(resultCorner1, Is.EqualTo(0f));

            // Verify original array is intact
            Assert.That(float.IsNaN(corners[1]), Is.True);
            Assert.That(float.IsPositiveInfinity(corners[2]), Is.True);
        }

        [Test]
        public void Test_ExceptionOnInvalidCornerArray()
        {
            // Assert
            Assert.Throws<ArgumentException>(() =>
            {
                VoxelSdfTrilinearInterpolationCalculator.Compute(new float[] { 1f }, 0.5f, 0.5f, 0.5f);
            });
        }
    }
}
