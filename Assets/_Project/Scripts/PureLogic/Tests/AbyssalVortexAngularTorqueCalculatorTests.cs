using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AbyssalVortexAngularTorqueCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 hullPos = new Vector3(10f, 0f, 0f);
            Vector3 vortexCenter = Vector3.Zero;
            Vector3 vortexAxis = new Vector3(0f, 1f, 0f);
            float angularVelocity = 2f;
            float hullMass = 100f;

            // Act
            Vector3 torque = AbyssalVortexAngularTorqueCalculator.Compute(hullPos, vortexCenter, vortexAxis, angularVelocity, hullMass);

            // Assert: Distance is 10, magnitude scales with distance (10) * velocity (2) * mass (100) = 2000
            // Torque aligns with axis (0, 1, 0)
            Assert.AreEqual(0f, torque.X, 0.001f, "Torque X should be 0.");
            Assert.AreEqual(2000f, torque.Y, 0.001f, "Torque Y should scale with distance.");
            Assert.AreEqual(0f, torque.Z, 0.001f, "Torque Z should be 0.");
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: hullPos is exactly at vortexCenter
            Vector3 hullPos = new Vector3(5f, 5f, 5f);
            Vector3 vortexCenter = new Vector3(5f, 5f, 5f);
            Vector3 vortexAxis = new Vector3(0f, 1f, 0f);
            float angularVelocity = 10f;
            float hullMass = 50f;

            // Act
            Vector3 torque = AbyssalVortexAngularTorqueCalculator.Compute(hullPos, vortexCenter, vortexAxis, angularVelocity, hullMass);

            // Assert: Distance is 0, so torque should be Vector3.Zero
            Assert.AreEqual(Vector3.Zero, torque, "Torque should be zero at vortex center.");
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Zero axis and mass
            Vector3 hullPos = new Vector3(10f, 0f, 0f);
            Vector3 vortexCenter = Vector3.Zero;
            Vector3 vortexAxis = Vector3.Zero;
            float angularVelocity = 0f;
            float hullMass = 0f;

            // Act
            Vector3 torque = AbyssalVortexAngularTorqueCalculator.Compute(hullPos, vortexCenter, vortexAxis, angularVelocity, hullMass);

            // Assert
            Assert.AreEqual(Vector3.Zero, torque, "Torque should handle zero inputs safely.");
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Negative mass and negative velocity
            Vector3 hullPos = new Vector3(-10f, -5f, -10f);
            Vector3 vortexCenter = Vector3.Zero;
            Vector3 vortexAxis = new Vector3(0f, -1f, 0f);
            float angularVelocity = -2f;
            float hullMass = -100f; // negative mass should be clamped/ignored

            // Act
            Vector3 torque = AbyssalVortexAngularTorqueCalculator.Compute(hullPos, vortexCenter, vortexAxis, angularVelocity, hullMass);

            // Assert: Negative mass clamped to 0 -> torque 0
            Assert.AreEqual(Vector3.Zero, torque, "Negative mass should clamp and result in zero torque.");
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Extreme values
            Vector3 hullPos = new Vector3(float.MaxValue, 0f, 0f);
            Vector3 vortexCenter = Vector3.Zero;
            Vector3 vortexAxis = new Vector3(0f, 1f, 0f);
            float angularVelocity = 10f;
            float hullMass = 10f;

            // Act
            Vector3 torque = AbyssalVortexAngularTorqueCalculator.Compute(hullPos, vortexCenter, vortexAxis, angularVelocity, hullMass);

            // Assert
            Assert.AreEqual(Vector3.Zero, torque, "Overflow should return zero safely.");
            Assert.Pass("Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_NanInputs_Case06()
        {
            // Arrange
            Vector3 hullPos = new Vector3(10f, 0f, float.NaN);
            Vector3 vortexCenter = Vector3.Zero;
            Vector3 vortexAxis = new Vector3(0f, 1f, 0f);
            float angularVelocity = float.NaN;
            float hullMass = 100f;

            // Act
            Vector3 torque = AbyssalVortexAngularTorqueCalculator.Compute(hullPos, vortexCenter, vortexAxis, angularVelocity, hullMass);

            // Assert
            Assert.AreEqual(Vector3.Zero, torque, "NaN inputs should be guarded safely.");
        }
    }
}
