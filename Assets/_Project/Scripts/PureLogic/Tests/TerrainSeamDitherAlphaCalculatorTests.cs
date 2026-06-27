using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class TerrainSeamDitherAlphaCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs. At blend = 0.5, some thresholds pass, some fail.
            // 1/17 (pass), 9/17 (fail - 0.529), 3/17 (pass), 11/17 (fail - 0.647)
            // (0,0) -> index 0 -> threshold 1/17 = ~0.0588 -> 0.5 >= 0.0588 -> 1.0f
            // (1,0) -> index 4 -> threshold 13/17 = ~0.764 -> 0.5 >= 0.764 -> 0.0f
            // Act
            float alpha00 = TerrainSeamDitherAlphaCalculator.Compute(0, 0, 0.5f);
            float alpha10 = TerrainSeamDitherAlphaCalculator.Compute(1, 0, 0.5f);
            float alpha01 = TerrainSeamDitherAlphaCalculator.Compute(0, 1, 0.5f); // index 1 -> 9/17 = 0.529 -> 0.0f

            // Assert: Verify expected output behaviour
            Assert.AreEqual(1.0f, alpha00);
            Assert.AreEqual(0.0f, alpha10);
            Assert.AreEqual(0.0f, alpha01);
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Blend < 0 should result in 0 alpha everywhere.
            // Blend > 1 should result in 1 alpha everywhere.
            // Act
            float minAlpha = TerrainSeamDitherAlphaCalculator.Compute(2, 2, -0.5f);
            float maxAlpha = TerrainSeamDitherAlphaCalculator.Compute(3, 1, 1.5f);

            // Assert
            Assert.AreEqual(0.0f, minAlpha);
            Assert.AreEqual(1.0f, maxAlpha);
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            // Act
            float zeroAlpha = TerrainSeamDitherAlphaCalculator.Compute(0, 0, 0.0f);

            // Assert
            // At blend = 0.0, it should be strictly less than all thresholds (min threshold is 1/17)
            Assert.AreEqual(0.0f, zeroAlpha);
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative coordinate inputs
            // -1 & 3 = 3. -1 % 4 conceptually maps to index 3 in a wrapped sense.
            // (x=-1, y=-1) maps to (3, 3) which is index 15: 6/17 = ~0.352
            // Act
            float negCoordAlphaLow = TerrainSeamDitherAlphaCalculator.Compute(-1, -1, 0.2f);
            float negCoordAlphaHigh = TerrainSeamDitherAlphaCalculator.Compute(-1, -1, 0.4f);

            // Assert
            Assert.AreEqual(0.0f, negCoordAlphaLow);
            Assert.AreEqual(1.0f, negCoordAlphaHigh);
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float nanAlpha = TerrainSeamDitherAlphaCalculator.Compute(0, 0, float.NaN);
            float posInfAlpha = TerrainSeamDitherAlphaCalculator.Compute(1, 1, float.PositiveInfinity);
            float negInfAlpha = TerrainSeamDitherAlphaCalculator.Compute(2, 2, float.NegativeInfinity);
            float largeCoordAlpha = TerrainSeamDitherAlphaCalculator.Compute(int.MaxValue, int.MinValue, 0.5f);

            // Assert
            Assert.AreEqual(0.0f, nanAlpha, "NaN should clamp to 0");
            Assert.AreEqual(1.0f, posInfAlpha, "+Infinity should clamp to 1");
            Assert.AreEqual(0.0f, negInfAlpha, "-Infinity should clamp to 0");
            // large coord is (int.MaxValue & 3, int.MinValue & 3) => (3, 0)
            // index: 3*4 + 0 = 12 => threshold 16/17 = 0.941
            Assert.AreEqual(0.0f, largeCoordAlpha, "Extreme coords should wrap and evaluate deterministically");
            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
