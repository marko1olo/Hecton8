using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FaunaObstacleAvoidanceVectorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 forwardDirection = new Vector3(0, 0, 1);
            Vector3 hitNormal = new Vector3(0, 1, 0); // Obstacle above
            float distanceToObstacle = 5f;
            float avoidanceRadius = 10f;

            // Act
            Vector3 result = FaunaObstacleAvoidanceVector.Calculate(forwardDirection, hitNormal, distanceToObstacle, avoidanceRadius);

            // Assert
            // Intensity should be (10 - 5) / 10 = 0.5
            // Vector should point towards hitNormal (0, 1, 0)
            Assert.That(result.X, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(0.5f).Within(0.001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            Vector3 forwardDirection = new Vector3(1, 0, 0);
            Vector3 hitNormal = new Vector3(-1, 0, 0);

            // Act 1: Distance = Radius
            Vector3 result1 = FaunaObstacleAvoidanceVector.Calculate(forwardDirection, hitNormal, 10f, 10f);

            // Act 2: Distance > Radius
            Vector3 result2 = FaunaObstacleAvoidanceVector.Calculate(forwardDirection, hitNormal, 15f, 10f);

            // Assert
            Assert.That(result1, Is.EqualTo(Vector3.Zero));
            Assert.That(result2, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            Vector3 forwardDirection = Vector3.Zero;
            Vector3 hitNormal = Vector3.Zero;

            // Act
            Vector3 result1 = FaunaObstacleAvoidanceVector.Calculate(forwardDirection, hitNormal, 5f, 10f);

            Vector3 result2 = FaunaObstacleAvoidanceVector.Calculate(new Vector3(1, 0, 0), new Vector3(1, 0, 0), 0f, 0f);

            // Assert
            Assert.That(result1, Is.EqualTo(Vector3.Zero));
            Assert.That(result2, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            Vector3 forwardDirection = new Vector3(0, 0, 1);
            Vector3 hitNormal = new Vector3(1, 0, 0);

            // Act
            Vector3 result1 = FaunaObstacleAvoidanceVector.Calculate(forwardDirection, hitNormal, -5f, 10f);
            Vector3 result2 = FaunaObstacleAvoidanceVector.Calculate(forwardDirection, hitNormal, 5f, -10f);

            // Assert
            // Negative distance should clamp to 0, intensity = (10-0)/10 = 1.0
            Assert.That(result1.X, Is.EqualTo(1f).Within(0.001f));
            Assert.That(result1.Y, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result1.Z, Is.EqualTo(0f).Within(0.001f));

            // Negative radius should clamp to 0, returning Zero
            Assert.That(result2, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            Vector3 forwardDirection = new Vector3(float.NaN, 1f, 1f);
            Vector3 hitNormal = new Vector3(1f, float.PositiveInfinity, 1f);

            // Act
            Vector3 result1 = FaunaObstacleAvoidanceVector.Calculate(forwardDirection, new Vector3(1, 0, 0), 5f, 10f);
            Vector3 result2 = FaunaObstacleAvoidanceVector.Calculate(new Vector3(1, 0, 0), hitNormal, 5f, 10f);
            Vector3 result3 = FaunaObstacleAvoidanceVector.Calculate(new Vector3(1, 0, 0), new Vector3(1, 0, 0), float.NaN, float.PositiveInfinity);

            // Assert
            Assert.That(result1, Is.EqualTo(Vector3.Zero));
            Assert.That(result2, Is.EqualTo(Vector3.Zero));
            Assert.That(result3, Is.EqualTo(Vector3.Zero));
        }
    }
}
