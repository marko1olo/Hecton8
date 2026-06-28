using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class GroundSnapDistanceCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(GroundSnapDistanceCalculator.Compute(0.1f, 0.5f, 10f, 45f), "Valid inputs within thresholds should snap.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(GroundSnapDistanceCalculator.Compute(0.5f, 0.5f, 45f, 45f), "Exact boundary thresholds should snap.");
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(0.6f, 0.5f, 10f, 45f), "Exceeding max step height should not snap.");
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(0.1f, 0.5f, 46f, 45f), "Exceeding max slope should not snap.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange & Act & Assert
            Assert.IsTrue(GroundSnapDistanceCalculator.Compute(0f, 0f, 0f, 0f), "Zero inputs should be handled safely and snap if they meet thresholds.");
            Assert.IsTrue(GroundSnapDistanceCalculator.Compute(0f, 0.5f, 0f, 45f), "Zero distance and slope should snap.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange & Act & Assert
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(-1.0f, 0.5f, 10f, 45f), "Negative distance should not snap.");
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(0.1f, 0.5f, -10f, 45f), "Negative slope should not snap.");
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(0.1f, -0.5f, 10f, 45f), "Negative max step height should safely return false.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange & Act & Assert
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(float.NaN, 0.5f, 10f, 45f), "NaN distance should safely return false.");
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(0.1f, float.NaN, 10f, 45f), "NaN max step height should safely return false.");
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(0.1f, 0.5f, float.PositiveInfinity, 45f), "Infinity slope should safely return false.");
            Assert.IsFalse(GroundSnapDistanceCalculator.Compute(float.PositiveInfinity, 0.5f, 10f, 45f), "Infinity distance should safely return false.");
        }
    }
}
