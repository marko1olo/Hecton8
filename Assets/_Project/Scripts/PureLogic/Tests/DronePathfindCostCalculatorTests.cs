using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class DronePathfindCostCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 fromNode = Vector3.Zero;
            Vector3 toNode = new Vector3(3f, 4f, 0f); // Distance is 5
            float[] hazards = new float[] { 1.5f };
            float baseMoveCost = 2f;

            // Act
            float cost = DronePathfindCostCalculator.Compute(fromNode, toNode, hazards, baseMoveCost);

            // Assert: Verify expected output behaviour (5 * 2 + 1.5 = 11.5)
            Assert.AreEqual(11.5f, cost, 0.0001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 fromNode = new Vector3(1f, 1f, 1f);
            Vector3 toNode = new Vector3(1f, 1f, 1f); // Distance is 0
            float[] hazards = new float[] { 0.000001f };
            float baseMoveCost = 0.000001f;

            // Act
            float cost = DronePathfindCostCalculator.Compute(fromNode, toNode, hazards, baseMoveCost);

            // Assert
            Assert.AreEqual(0.000001f, cost, 0.0000001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector3 fromNode = Vector3.Zero;
            Vector3 toNode = Vector3.Zero;
            float[] hazards = new float[] { 0f, 0f };
            float baseMoveCost = 0f;

            // Act
            float cost = DronePathfindCostCalculator.Compute(fromNode, toNode, hazards, baseMoveCost);

            // Assert
            Assert.AreEqual(0f, cost, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 fromNode = Vector3.Zero;
            Vector3 toNode = new Vector3(3f, 4f, 0f);
            float[] hazards = new float[] { -5f, 2f }; // -5 should be clamped to 0
            float baseMoveCost = -10f; // Clamped to 0

            // Act
            float cost = DronePathfindCostCalculator.Compute(fromNode, toNode, hazards, baseMoveCost);

            // Assert
            Assert.AreEqual(2f, cost, 0.0001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 fromNode = Vector3.Zero;
            Vector3 toNode = new Vector3(1f, 0f, 0f);
            float[] hazards = new float[] { float.MaxValue };
            float baseMoveCost = float.MaxValue;

            // Act
            float cost1 = DronePathfindCostCalculator.Compute(fromNode, toNode, hazards, baseMoveCost);
            float cost2 = DronePathfindCostCalculator.Compute(new Vector3(float.PositiveInfinity, 0f, 0f), toNode, null, 1f);
            float cost3 = DronePathfindCostCalculator.Compute(fromNode, toNode, new float[] { float.NaN }, 1f);

            // Assert
            Assert.AreEqual(float.MaxValue, cost1, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(float.MaxValue, cost2, "Verify robust calculation handles Infinity correctly.");
            Assert.AreEqual(float.MaxValue, cost3, "Verify robust calculation handles NaN correctly.");
        }
    }
}
