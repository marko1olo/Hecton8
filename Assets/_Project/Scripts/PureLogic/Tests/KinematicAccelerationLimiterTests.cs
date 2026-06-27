using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class KinematicAccelerationLimiterTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 currentVelocity = new Vector3(0f, 0f, 0f);
            Vector3 targetVelocity = new Vector3(10f, 0f, 0f);
            float maxAcceleration = 2f;
            float deltaTime = 1f;

            // Act
            Vector3 result = KinematicAccelerationLimiter.Calculate(currentVelocity, targetVelocity, maxAcceleration, deltaTime);

            // Assert: Verify expected output behaviour
            // maxDelta = 2 * 1 = 2.
            // delta = (10,0,0) -> clamped to (2,0,0)
            Assert.AreEqual(2f, result.X, 0.0001f);
            Assert.AreEqual(0f, result.Y, 0.0001f);
            Assert.AreEqual(0f, result.Z, 0.0001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 currentVelocity = new Vector3(5f, 0f, 0f);
            Vector3 targetVelocity = new Vector3(6f, 0f, 0f);
            float maxAcceleration = 2f;
            float deltaTime = 1f;

            // Act
            Vector3 result = KinematicAccelerationLimiter.Calculate(currentVelocity, targetVelocity, maxAcceleration, deltaTime);

            // Assert
            // delta = (1,0,0), maxDelta = 2, so it shouldn't be clamped
            Assert.AreEqual(1f, result.X, 0.0001f);
            Assert.AreEqual(0f, result.Y, 0.0001f);
            Assert.AreEqual(0f, result.Z, 0.0001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector3 currentVelocity = new Vector3(0f, 0f, 0f);
            Vector3 targetVelocity = new Vector3(0f, 0f, 0f);
            float maxAcceleration = 0f;
            float deltaTime = 0f;

            // Act
            Vector3 result = KinematicAccelerationLimiter.Calculate(currentVelocity, targetVelocity, maxAcceleration, deltaTime);

            // Assert
            Assert.AreEqual(0f, result.X, 0.0001f);
            Assert.AreEqual(0f, result.Y, 0.0001f);
            Assert.AreEqual(0f, result.Z, 0.0001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 currentVelocity = new Vector3(0f, 0f, 0f);
            Vector3 targetVelocity = new Vector3(-10f, -5f, 0f);
            float maxAcceleration = -5f;
            float deltaTime = 1f;

            // Act
            Vector3 result = KinematicAccelerationLimiter.Calculate(currentVelocity, targetVelocity, maxAcceleration, deltaTime);

            // Assert
            Assert.AreEqual(0f, result.X, 0.0001f);
            Assert.AreEqual(0f, result.Y, 0.0001f);
            Assert.AreEqual(0f, result.Z, 0.0001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 currentVelocity = new Vector3(float.PositiveInfinity, 0f, 0f);
            Vector3 targetVelocity = new Vector3(0f, 0f, 0f);
            float maxAcceleration = 5f;
            float deltaTime = 1f;

            // Act
            Vector3 result = KinematicAccelerationLimiter.Calculate(currentVelocity, targetVelocity, maxAcceleration, deltaTime);

            // Assert
            Assert.AreEqual(0f, result.X, 0.0001f);
            Assert.AreEqual(0f, result.Y, 0.0001f);
            Assert.AreEqual(0f, result.Z, 0.0001f);

            // Arrange NaN
            Vector3 currentVelocityNaN = new Vector3(float.NaN, 0f, 0f);

            // Act
            Vector3 resultNaN = KinematicAccelerationLimiter.Calculate(currentVelocityNaN, targetVelocity, maxAcceleration, deltaTime);

            // Assert
            Assert.AreEqual(0f, resultNaN.X, 0.0001f);
        }
    }
}
