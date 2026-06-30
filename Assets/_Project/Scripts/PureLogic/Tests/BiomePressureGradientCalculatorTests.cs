using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BiomePressureGradientCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float[] biomePressures = { 10f, 5f };
            int[] adjacencyMap = { 0, 1, 1, 0 }; // 2x2 matrix, adjacent to each other
            float migrationRate = 0.5f;

            // Act
            float[] flows = BiomePressureGradientCalculator.Compute(biomePressures, adjacencyMap, migrationRate);

            // Assert: Verify expected output behaviour
            // gradient = 10 - 5 = 5. flow = 5 * 0.5 = 2.5
            // biome 0 outflow = 2.5, biome 1 inflow = 2.5
            Assert.AreEqual(2, flows.Length);
            Assert.AreEqual(-2.5f, flows[0], 0.001f);
            Assert.AreEqual(2.5f, flows[1], 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float[] biomePressuresEqual = { 10f, 10f };
            int[] adjacencyMap = { 0, 1, 1, 0 };
            float migrationRate = 0.5f;

            // Act
            float[] flowsEqual = BiomePressureGradientCalculator.Compute(biomePressuresEqual, adjacencyMap, migrationRate);

            // Assert equal pressures result in 0 flow
            Assert.AreEqual(0f, flowsEqual[0], 0.001f);
            Assert.AreEqual(0f, flowsEqual[1], 0.001f);

            // Test outflow clamping so biome pressure doesn't drop below 0
            float[] biomePressuresExtreme = { 2f, 0f };
            float highMigrationRate = 10f; // gradient = 2. expected flow = 20. But clamped to 2 (biome 0 has max 2)
            float[] flowsExtreme = BiomePressureGradientCalculator.Compute(biomePressuresExtreme, adjacencyMap, highMigrationRate);

            Assert.AreEqual(-2f, flowsExtreme[0], 0.001f);
            Assert.AreEqual(2f, flowsExtreme[1], 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float[] biomePressures = { 10f, 5f };
            int[] adjacencyMap = { 0, 1, 1, 0 };

            // Act
            float[] flowsZeroRate = BiomePressureGradientCalculator.Compute(biomePressures, adjacencyMap, 0f);

            // Assert
            Assert.AreEqual(0f, flowsZeroRate[0], 0.001f);
            Assert.AreEqual(0f, flowsZeroRate[1], 0.001f);

            // Empty arrays test
            float[] flowsEmpty = BiomePressureGradientCalculator.Compute(new float[0], new int[0], 0.5f);
            Assert.AreEqual(0, flowsEmpty.Length);

            // Zero pressures
            float[] flowsZeroPressures = BiomePressureGradientCalculator.Compute(new float[] { 0f, 0f }, adjacencyMap, 0.5f);
            Assert.AreEqual(0f, flowsZeroPressures[0], 0.001f);
            Assert.AreEqual(0f, flowsZeroPressures[1], 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float[] biomePressures = { 10f, 5f };
            int[] adjacencyMap = { 0, 1, 1, 0 };

            // Act - negative migration rate should be ignored/clamped to 0
            float[] flowsNegRate = BiomePressureGradientCalculator.Compute(biomePressures, adjacencyMap, -0.5f);

            // Assert
            Assert.AreEqual(0f, flowsNegRate[0], 0.001f);
            Assert.AreEqual(0f, flowsNegRate[1], 0.001f);

            // Act - negative pressures shouldn't generate outflow
            float[] biomePressuresNeg = { -10f, -20f };
            float[] flowsNegPressures = BiomePressureGradientCalculator.Compute(biomePressuresNeg, adjacencyMap, 0.5f);

            Assert.AreEqual(0f, flowsNegPressures[0], 0.001f); // Gradient is 10, but since -10 has max 0 outflow, it should scale to 0
            Assert.AreEqual(0f, flowsNegPressures[1], 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float[] biomePressures = { float.PositiveInfinity, float.NaN, 10f, 5f };
            int[] adjacencyMap = {
                0, 1, 1, 1,
                1, 0, 1, 1,
                1, 1, 0, 1,
                1, 1, 1, 0
            };

            // Act
            float[] flows = BiomePressureGradientCalculator.Compute(biomePressures, adjacencyMap, 0.5f);

            // Assert: NaN and Infinity are ignored, valid entries compute normally
            Assert.AreEqual(0f, flows[0], 0.001f); // infinity ignored
            Assert.AreEqual(0f, flows[1], 0.001f); // NaN ignored

            // For valid elements (indices 2 and 3), gradient is 10 - 5 = 5. Flow = 2.5
            Assert.AreEqual(-2.5f, flows[2], 0.001f);
            Assert.AreEqual(2.5f, flows[3], 0.001f);

            // NaN migration rate
            float[] flowsNaNRate = BiomePressureGradientCalculator.Compute(new float[] { 10f, 5f }, new int[] { 0, 1, 1, 0 }, float.NaN);
            Assert.AreEqual(0f, flowsNaNRate[0], 0.001f);
            Assert.AreEqual(0f, flowsNaNRate[1], 0.001f);
        }
    }
}
