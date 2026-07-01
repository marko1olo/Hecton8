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
            Assert.That(result.X, Is.EqualTo(-8.25f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(3.315f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.01f));
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
            Assert.That(result.X, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(4.08f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(-10.125f).Within(0.01f));
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
            Assert.That(result.X, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(2.55f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(-6.375f).Within(0.01f));
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
            Assert.That(result.X, Is.EqualTo(6.375f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(2.55f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.01f));
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
            Assert.That(result, Is.EqualTo(Vector3.Zero));

            // Testing infinity
            vel = new Vector3(float.PositiveInfinity, 0f, 0f);
            result = VehicleEmergencyEjectionVector.Calculate(vel, fwd, up, severity);
            Assert.That(result, Is.EqualTo(Vector3.Zero));
        }
        [Test]
        public void Test_CalculateImpulse_HappyPath()
        {
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(0f, 0f, 1f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 0.5f;

            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                horizontalImpulseScale: 10f, verticalImpulseScale: 5f,
                horizontalLerpMin: 0.5f, horizontalLerpMax: 1.5f,
                verticalLerpMin: 0.5f, verticalLerpMax: 1.5f,
                epsilonSquared: 0.0001f);

            Assert.That(result.X, Is.EqualTo(-10f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(5f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Test_CalculateImpulse_Boundary()
        {
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(0f, 0f, 1f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 1.5f;

            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                horizontalImpulseScale: 10f, verticalImpulseScale: 5f,
                horizontalLerpMin: 0f, horizontalLerpMax: 2f,
                verticalLerpMin: 0f, verticalLerpMax: 2f,
                epsilonSquared: 0.0001f);

            Assert.That(result.X, Is.EqualTo(-20f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(10f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Test_CalculateImpulse_ZeroInputs()
        {
            Vector3 vel = new Vector3(0f, 0f, 0f);
            Vector3 fwd = new Vector3(0f, 0f, 1f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 0f;

            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                horizontalImpulseScale: 8f, verticalImpulseScale: 4f,
                horizontalLerpMin: 0.5f, horizontalLerpMax: 1f,
                verticalLerpMin: 0.5f, verticalLerpMax: 1f,
                epsilonSquared: 0.0001f);

            Assert.That(result.X, Is.EqualTo(0f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(2f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(-4f).Within(0.01f));
        }

        [Test]
        public void Test_CalculateImpulse_NegativeInputs()
        {
            Vector3 vel = new Vector3(0f, -50f, 0f);
            Vector3 fwd = new Vector3(-1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = -1f;

            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                horizontalImpulseScale: 8f, verticalImpulseScale: 4f,
                horizontalLerpMin: 0.5f, horizontalLerpMax: 1f,
                verticalLerpMin: 0.5f, verticalLerpMax: 1f,
                epsilonSquared: 0.0001f);

            Assert.That(result.X, Is.EqualTo(4f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(2f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Test_CalculateImpulse_ExtremeInputs()
        {
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(0f, 0f, 1f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 0.5f;

            Vector3 result1 = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                horizontalImpulseScale: float.NaN, verticalImpulseScale: 5f,
                horizontalLerpMin: 0.5f, horizontalLerpMax: 1.5f,
                verticalLerpMin: 0.5f, verticalLerpMax: 1.5f,
                epsilonSquared: 0.0001f);

            Assert.That(result1, Is.EqualTo(Vector3.Zero));

            Vector3 result2 = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity,
                horizontalImpulseScale: float.PositiveInfinity, verticalImpulseScale: 5f,
                horizontalLerpMin: 0.5f, horizontalLerpMax: 1.5f,
                verticalLerpMin: 0.5f, verticalLerpMax: 1.5f,
                epsilonSquared: 0.0001f);

            Assert.That(result2, Is.EqualTo(Vector3.Zero));
        }

    }
}