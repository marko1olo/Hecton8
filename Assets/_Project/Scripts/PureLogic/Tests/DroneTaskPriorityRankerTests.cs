using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class DroneTaskPriorityRankerTests
    {
        private float[] defaultWeights = new float[] { 1f, 1f, 1f };

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float urgency = 10f;
            float proximity01 = 0.5f;
            float resourceAvailability01 = 0.5f;
            float[] weights = new float[] { 2f, 1f, 0.5f };

            // Act
            float score = DroneTaskPriorityRanker.Calculate(urgency, proximity01, resourceAvailability01, weights);

            // Assert: Verify expected output behaviour
            Assert.AreEqual((10f * 2f) + (0.5f * 1f) + (0.5f * 0.5f), score);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float urgency = 0f;
            float proximity01 = 1f;
            float resourceAvailability01 = 0f;

            // Act
            float score = DroneTaskPriorityRanker.Calculate(urgency, proximity01, resourceAvailability01, defaultWeights);

            // Assert
            Assert.AreEqual(1f, score);

            float score2 = DroneTaskPriorityRanker.Calculate(100f, 1.5f, -0.5f, defaultWeights);
            Assert.AreEqual(100f + 1f + 0f, score2); // Proximity clamped to 1, resource clamped to 0
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float urgency = 0f;
            float proximity01 = 0f;
            float resourceAvailability01 = 0f;

            // Act
            float score = DroneTaskPriorityRanker.Calculate(urgency, proximity01, resourceAvailability01, defaultWeights);

            // Assert
            Assert.AreEqual(0f, score);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float urgency = -10f;
            float proximity01 = -0.5f;
            float resourceAvailability01 = -0.5f;

            // Act
            float score = DroneTaskPriorityRanker.Calculate(urgency, proximity01, resourceAvailability01, defaultWeights);

            // Assert
            Assert.AreEqual(0f, score);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float[] weights = new float[] { 1f, 1f, 1f };

            // Act & Assert
            Assert.AreEqual(0f, DroneTaskPriorityRanker.Calculate(float.NaN, 0.5f, 0.5f, weights));
            Assert.AreEqual(0f, DroneTaskPriorityRanker.Calculate(10f, float.PositiveInfinity, 0.5f, weights));
            Assert.AreEqual(0f, DroneTaskPriorityRanker.Calculate(10f, 0.5f, float.NegativeInfinity, weights));

            Assert.AreEqual(0f, DroneTaskPriorityRanker.Calculate(10f, 0.5f, 0.5f, null));
            Assert.AreEqual(0f, DroneTaskPriorityRanker.Calculate(10f, 0.5f, 0.5f, new float[] { 1f, 1f }));
        }
    }
}
