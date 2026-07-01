using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VehicleEmergencyEjectionVectorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 0.5f;

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.Calculate(vel, fwd, up, severity);

            // Assert
            // severity 0.5 -> horizontalLerp = 1.1, verticalLerp = 0.975
            // horizontal impulse = 7.5 * 1.1 = 8.25
            // vertical impulse = 3.4 * 0.975 = 3.315
            // lateral dir should be -X -> (-8.25, 3.315, 0)
            Assert.AreEqual(-8.25f, result.X, 0.01f);
            Assert.AreEqual(3.315f, result.Y, 0.01f);
            Assert.AreEqual(0f, result.Z, 0.01f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, 0f, 0f); // Fallback to fwd
            Vector3 fwd = new Vector3(0f, 0f, 1f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 1.5f; // Clamped to 1

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.Calculate(vel, fwd, up, severity);

            // Assert
            // severity 1 -> horizontalLerp = 1.35, verticalLerp = 1.2
            // horizontal impulse = 7.5 * 1.35 = 10.125
            // vertical impulse = 3.4 * 1.2 = 4.08
            // lateral dir should be -fwd -> (0, 0, -1)
            Assert.AreEqual(0f, result.X, 0.01f);
            Assert.AreEqual(4.08f, result.Y, 0.01f);
            Assert.AreEqual(-10.125f, result.Z, 0.01f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, 0f, 0f);
            Vector3 fwd = new Vector3(0f, 0f, 0f);
            Vector3 up = new Vector3(0f, 0f, 0f);
            float severity = 0f; // Clamped to 0

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.Calculate(vel, fwd, up, severity);

            // Assert
            // Severity 0 -> horizontalLerp = 0.85, verticalLerp = 0.75
            // Fallback since vel and fwd are zero -> (0, 0, -1)
            // horizontal impulse = 7.5 * 0.85 = 6.375
            // vertical impulse = 3.4 * 0.75 = 2.55
            Assert.AreEqual(0f, result.X, 0.01f);
            Assert.AreEqual(2.55f, result.Y, 0.01f);
            Assert.AreEqual(-6.375f, result.Z, 0.01f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, -50f, 0f); // purely vertical velocity should act like 0 planar
            Vector3 fwd = new Vector3(-1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = -1f; // Clamped to 0

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.Calculate(vel, fwd, up, severity);

            // Assert
            // Planar velocity is 0. Fallback lateral is -fwd = (1, 0, 0)
            // horizontal impulse = 7.5 * 0.85 = 6.375
            Assert.AreEqual(6.375f, result.X, 0.01f);
            Assert.AreEqual(2.55f, result.Y, 0.01f);
            Assert.AreEqual(0f, result.Z, 0.01f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            Vector3 vel = new Vector3(float.NaN, 0f, 0f);
            Vector3 fwd = new Vector3(0f, 1f, 0f);
            Vector3 up = new Vector3(0f, 0f, 1f);
            float severity = 0.5f;

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.Calculate(vel, fwd, up, severity);

            // Assert
            // NaN handling should return Zero
            Assert.AreEqual(Vector3.Zero, result);

            // Testing infinity
            vel = new Vector3(float.PositiveInfinity, 0f, 0f);
            result = VehicleEmergencyEjectionVector.Calculate(vel, fwd, up, severity);
            Assert.AreEqual(Vector3.Zero, result);
        }

        [Test]
        public void Test_CalculateImpulse_HappyPath()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 0.5f;

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                10f, 5f, 0.5f, 1.5f, 0.5f, 1.5f, 0.001f);

            // Assert
            // severity 0.5 -> horizontalLerp = 1.0, verticalLerp = 1.0
            // horizontal impulse = 10 * 1.0 = 10
            // vertical impulse = 5 * 1.0 = 5
            // lateral dir should be -X -> (-10, 5, 0)
            Assert.AreEqual(-10f, result.X, 0.01f);
            Assert.AreEqual(5f, result.Y, 0.01f);
            Assert.AreEqual(0f, result.Z, 0.01f);
        }

        [Test]
        public void Test_CalculateImpulse_BoundaryConstraints()
        {
            // Arrange
            Vector3 vel = new Vector3(0.01f, 0f, 0f); // just above default epsilon (sqrt(0.0001) = 0.01)
            Vector3 fwd = new Vector3(1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 1f;

            // Act
            // using epsilon 0.00001f so vel.LengthSquared (0.0001) > epsilonSquared
            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                10f, 5f, 0.5f, 1.5f, 0.5f, 1.5f, 0.00001f);

            // Assert
            // severity 1 -> horizontalLerp = 1.5, verticalLerp = 1.5
            // horizontal impulse = 10 * 1.5 = 15
            // vertical impulse = 5 * 1.5 = 7.5
            // lateral dir should be -X -> (-15, 7.5, 0)
            Assert.AreEqual(-15f, result.X, 0.01f);
            Assert.AreEqual(7.5f, result.Y, 0.01f);
            Assert.AreEqual(0f, result.Z, 0.01f);
        }

        [Test]
        public void Test_CalculateImpulse_ZeroInputs()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, 0f, 0f);
            Vector3 fwd = new Vector3(0f, 0f, 0f);
            Vector3 up = new Vector3(0f, 0f, 0f);
            float severity = 0f;

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                0f, 0f, 0f, 0f, 0f, 0f, 0f);

            // Assert
            // severity 0, all lerps 0, scales 0 -> impulse should be 0
            Assert.AreEqual(0f, result.X, 0.01f);
            Assert.AreEqual(0f, result.Y, 0.01f);
            Assert.AreEqual(0f, result.Z, 0.01f);
        }

        [Test]
        public void Test_CalculateImpulse_NegativeInputs()
        {
            // Arrange
            Vector3 vel = new Vector3(0f, 0f, 0f);
            Vector3 fwd = new Vector3(-1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = -5f; // Clamped to 0

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                -10f, -5f, -2f, -1f, -2f, -1f, 0.001f);

            // Assert
            // planarVelocity = 0, fallbackDir = -fwd = (1, 0, 0)
            // severity clamped to 0 -> horizontalLerp = -2, verticalLerp = -2
            // horizontal impulse = -10 * -2 = 20
            // vertical impulse = -5 * -2 = 10
            // lateral dir = (1, 0, 0)
            // impulse = (20, 10, 0)
            Assert.AreEqual(20f, result.X, 0.01f);
            Assert.AreEqual(10f, result.Y, 0.01f);
            Assert.AreEqual(0f, result.Z, 0.01f);
        }

        [Test]
        public void Test_CalculateImpulse_ExtremeInputs()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);

            // Act & Assert NaN Severity
            Vector3 resultNaN = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, float.NaN,
                10f, 5f, 0.5f, 1.5f, 0.5f, 1.5f, 0.001f);
            Assert.AreEqual(Vector3.Zero, resultNaN);

            // Act & Assert Infinity scale
            Vector3 resultInf = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, 0.5f,
                float.PositiveInfinity, 5f, 0.5f, 1.5f, 0.5f, 1.5f, 0.001f);
            Assert.AreEqual(Vector3.Zero, resultInf);
        }
    }
}
