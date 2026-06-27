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
    }
}