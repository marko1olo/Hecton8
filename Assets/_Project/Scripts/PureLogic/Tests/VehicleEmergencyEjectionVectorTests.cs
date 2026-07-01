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
        public void Test_CalculateImpulse_HappyPath_CustomParameters()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 0.5f;

            // Custom params
            float hScale = 10f;
            float vScale = 5f;
            float hLerpMin = 1.0f;
            float hLerpMax = 2.0f;
            float vLerpMin = 1.0f;
            float vLerpMax = 2.0f;

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity, hScale, vScale, hLerpMin, hLerpMax, vLerpMin, vLerpMax);

            // Assert
            // severity 0.5 -> hLerp = 1.5, vLerp = 1.5
            // horizontal impulse = 10 * 1.5 = 15
            // vertical impulse = 5 * 1.5 = 7.5
            // lateral dir should be -X -> (-15, 7.5, 0)
            Assert.AreEqual(-15f, result.X, 0.01f);
            Assert.AreEqual(7.5f, result.Y, 0.01f);
            Assert.AreEqual(0f, result.Z, 0.01f);
        }

        [Test]
        public void Test_CalculateImpulse_Boundary_CustomEpsilon()
        {
            // Arrange
            Vector3 fwd = new Vector3(0f, 0f, 1f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 1f;

            // Custom epsilon
            float epsilonSq = 4f;

            // Velocity below custom epsilon: length sq = 1, should fallback to -fwd
            Vector3 velBelow = new Vector3(1f, 0f, 0f);
            Vector3 resultBelow = VehicleEmergencyEjectionVector.CalculateImpulse(
                velBelow, fwd, up, severity, 10f, 10f, 1f, 1f, 1f, 1f, epsilonSq);

            // Assert below: lateral is (0, 0, -1)
            Assert.AreEqual(0f, resultBelow.X, 0.01f);
            Assert.AreEqual(10f, resultBelow.Y, 0.01f);
            Assert.AreEqual(-10f, resultBelow.Z, 0.01f);

            // Velocity above custom epsilon: length sq = 9, lateral = -velBelow
            Vector3 velAbove = new Vector3(3f, 0f, 0f);
            Vector3 resultAbove = VehicleEmergencyEjectionVector.CalculateImpulse(
                velAbove, fwd, up, severity, 10f, 10f, 1f, 1f, 1f, 1f, epsilonSq);

            // Assert above: lateral is (-1, 0, 0)
            Assert.AreEqual(-10f, resultAbove.X, 0.01f);
            Assert.AreEqual(10f, resultAbove.Y, 0.01f);
            Assert.AreEqual(0f, resultAbove.Z, 0.01f);
        }

        [Test]
        public void Test_CalculateImpulse_ZeroScales()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 1f;

            // Act
            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity, 0f, 0f, 1f, 1f, 1f, 1f);

            // Assert
            Assert.AreEqual(Vector3.Zero, result);
        }

        [Test]
        public void Test_CalculateImpulse_NegativeLerps()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 1f;

            // Act: Using severity = 1 -> lerps should be the max values (-2f, -3f)
            Vector3 result = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity, 5f, 5f, -1f, -2f, -1f, -3f);

            // Assert
            // hLerp = -2, vLerp = -3
            // horizontal impulse = 5 * -2 = -10
            // vertical impulse = 5 * -3 = -15
            // lateral dir is -X -> (-(-10), -15, 0) -> (10, -15, 0)
            Assert.AreEqual(10f, result.X, 0.01f);
            Assert.AreEqual(-15f, result.Y, 0.01f);
            Assert.AreEqual(0f, result.Z, 0.01f);
        }

        [Test]
        public void Test_CalculateImpulse_Extreme_NaNParameters()
        {
            // Arrange
            Vector3 vel = new Vector3(10f, 0f, 0f);
            Vector3 fwd = new Vector3(1f, 0f, 0f);
            Vector3 up = new Vector3(0f, 1f, 0f);
            float severity = 1f;

            // Act
            Vector3 result1 = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity, float.NaN, 5f, 1f, 1f, 1f, 1f);
            Vector3 result2 = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity, 5f, float.NaN, 1f, 1f, 1f, 1f);
            Vector3 result3 = VehicleEmergencyEjectionVector.CalculateImpulse(
                vel, fwd, up, severity, 5f, 5f, float.NaN, 1f, 1f, 1f);

            // Assert
            Assert.AreEqual(Vector3.Zero, result1);
            Assert.AreEqual(Vector3.Zero, result2);
            Assert.AreEqual(Vector3.Zero, result3);
        }

    }
}