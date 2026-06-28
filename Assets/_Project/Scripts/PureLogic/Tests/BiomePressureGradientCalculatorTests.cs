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
            // Arrange
            float[] pressures = new float[] { 10f, 5f, 5f };
            int[] adjacency = new int[] {
                0, 1, 1,
                1, 0, 0,
                1, 0, 0
            };
            float rate = 0.5f;

            // Act
            float[] flows = BiomePressureGradientCalculator.Compute(pressures, adjacency, rate);

            // Assert: Verify expected output behaviour
            // Biome 0: (10 - 5)*1*0.5 + (10 - 5)*1*0.5 = 2.5 + 2.5 = 5.0
            // Biome 1: (5 - 10)*1*0.5 = -2.5
            // Biome 2: (5 - 10)*1*0.5 = -2.5
            Assert.AreEqual(3, flows.Length);
            Assert.AreEqual(5.0f, flows[0], 0.001f);
            Assert.AreEqual(-2.5f, flows[1], 0.001f);
            Assert.AreEqual(-2.5f, flows[2], 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float[] pressures = new float[] { 5000000f, -5000000f };
            int[] adjacency = new int[] { 0, 1, 1, 0 };
            float rate = 1.0f;

            // Act
            float[] flows = BiomePressureGradientCalculator.Compute(pressures, adjacency, rate);

            // Assert: Verify boundary constraints clamp correctly
            // Max allowed flow is +/- 1000000f
            Assert.AreEqual(2, flows.Length);
            Assert.AreEqual(1000000f, flows[0]);
            Assert.AreEqual(-1000000f, flows[1]);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float[] pressures = new float[] { 0f, 0f };
            int[] adjacency = new int[] { 0, 1, 1, 0 };
            float rate = 0f;

            // Act
            float[] flows = BiomePressureGradientCalculator.Compute(pressures, adjacency, rate);

            // Assert: Verify zero inputs are handled without divide-by-zero or exception.
            Assert.AreEqual(2, flows.Length);
            Assert.AreEqual(0f, flows[0]);
            Assert.AreEqual(0f, flows[1]);

            // Test null
            float[] flowsNull = BiomePressureGradientCalculator.Compute(null, null, rate);
            Assert.AreEqual(0, flowsNull.Length);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float[] pressures = new float[] { 10f, 20f };
            int[] adjacency = new int[] { 0, -1, -5, 0 }; // negative adjacency should be ignored
            float rate = -0.5f; // negative rate should be clamped to 0

            // Act
            float[] flows = BiomePressureGradientCalculator.Compute(pressures, adjacency, rate);

            // Assert
            Assert.AreEqual(2, flows.Length);
            Assert.AreEqual(0f, flows[0]);
            Assert.AreEqual(0f, flows[1]);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float[] pressures = new float[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity };
            int[] adjacency = new int[] {
                0, 1, 1,
                1, 0, 1,
                1, 1, 0
            };
            float rate = float.NaN;

            // Act
            float[] flows = BiomePressureGradientCalculator.Compute(pressures, adjacency, rate);

            // Assert: Verify robust calculation and overflow protection.
            Assert.AreEqual(3, flows.Length);
            Assert.AreEqual(0f, flows[0]);
            Assert.AreEqual(0f, flows[1]);
            Assert.AreEqual(0f, flows[2]);
        }
    }
}
