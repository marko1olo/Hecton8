using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FlockingBoidSeparationVectorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 boidPos = new Vector3(2f, 0f, 0f);
            Vector3 obstaclePos = new Vector3(0f, 0f, 0f);
            float minDistance = 4f;

            // Act
            Vector3 force = FlockingBoidSeparationVector.Calculate(boidPos, obstaclePos, minDistance);

            // Assert: Distance is 2. Force magnitude = (4 - 2) / 4 = 0.5. Dir = +X.
            Assert.AreEqual(0.5f, force.X, 0.001f);
            Assert.AreEqual(0f, force.Y);
            Assert.AreEqual(0f, force.Z);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            Vector3 boidPos = new Vector3(4f, 0f, 0f);
            Vector3 obstaclePos = new Vector3(0f, 0f, 0f);
            float minDistance = 4f;

            // Act
            Vector3 force = FlockingBoidSeparationVector.Calculate(boidPos, obstaclePos, minDistance);

            // Assert: Distance equals minDistance, force should be zero.
            Assert.AreEqual(Vector3.Zero, force);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            Vector3 boidPos = Vector3.Zero;
            Vector3 obstaclePos = Vector3.Zero;
            float minDistance = 5f;

            // Act
            Vector3 force = FlockingBoidSeparationVector.Calculate(boidPos, obstaclePos, minDistance);

            // Assert: Should push arbitrarily (UnitY * minDistance) if directly on top
            Assert.AreEqual(new Vector3(0f, 5f, 0f), force);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            Vector3 boidPos = new Vector3(1f, 1f, 1f);
            Vector3 obstaclePos = new Vector3(2f, 2f, 2f);
            float minDistance = -1f;

            // Act
            Vector3 force = FlockingBoidSeparationVector.Calculate(boidPos, obstaclePos, minDistance);

            // Assert: Negative min distance should return zero
            Assert.AreEqual(Vector3.Zero, force);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            Vector3 boidPos = new Vector3(float.MaxValue, 0f, 0f);
            Vector3 obstaclePos = new Vector3(0f, 0f, 0f);
            float minDistance = 10f;

            // Act
            Vector3 force = FlockingBoidSeparationVector.Calculate(boidPos, obstaclePos, minDistance);

            // Assert: Extreme distance > minDistance, so zero force
            Assert.AreEqual(Vector3.Zero, force);

            // NaN input test
            force = FlockingBoidSeparationVector.Calculate(new Vector3(float.NaN, 0, 0), Vector3.Zero, 10f);
            Assert.AreEqual(Vector3.Zero, force);
        }
    }
}
