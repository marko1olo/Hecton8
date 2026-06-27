using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class InertiaTransferCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 vehicleVel = new Vector3(10f, 0f, 0f);
            Vector3 playerVel = new Vector3(0f, 0f, 0f);
            float playerMass = 100f;
            float vehicleMass = 900f;

            // Act - 100% transfer
            Vector3 result1 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 1.0f, playerMass, vehicleMass);
            // target velocity = (0*100 + 10*900) / 1000 = 9. So with 100% transfer, result is 9

            // Assert
            Assert.AreEqual(9f, result1.X, 0.001f);
            Assert.AreEqual(0f, result1.Y, 0.001f);
            Assert.AreEqual(0f, result1.Z, 0.001f);

            // Act - 50% transfer
            Vector3 result2 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 0.5f, playerMass, vehicleMass);
            // target is 9, lerp from 0 to 9 by 0.5 = 4.5
            Assert.AreEqual(4.5f, result2.X, 0.001f);
            Assert.AreEqual(0f, result2.Y, 0.001f);
            Assert.AreEqual(0f, result2.Z, 0.001f);

            // Act - 0% transfer
            Vector3 result3 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 0.0f, playerMass, vehicleMass);
            Assert.AreEqual(0f, result3.X, 0.001f);

            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 vehicleVel = new Vector3(10f, 5f, 2f);
            Vector3 playerVel = new Vector3(2f, 1f, 0f);

            // Act - transfer > 1
            Vector3 result1 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 2.0f, 100f, 900f);
            Vector3 expected1 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 1.0f, 100f, 900f);
            Assert.AreEqual(expected1, result1);

            // Act - transfer < 0
            Vector3 result2 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, -1.0f, 100f, 900f);
            Vector3 expected2 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 0.0f, 100f, 900f);
            Assert.AreEqual(expected2, result2);

            // Act - negative masses
            Vector3 result3 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 1.0f, -100f, -900f);
            Vector3 result4 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 1.0f, 0f, 0f); // acts as 0 mass
            Assert.AreEqual(result4, result3);

            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // All zeros
            Vector3 result1 = InertiaTransferCalculator.Compute(Vector3.Zero, Vector3.Zero, 0f, 0f, 0f);
            Assert.AreEqual(Vector3.Zero, result1);

            // Zero mass division protection
            Vector3 vehicleVel = new Vector3(10f, 0f, 0f);
            Vector3 playerVel = new Vector3(2f, 0f, 0f);
            Vector3 result2 = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 1.0f, 0f, 0f);
            // For zero mass, it targets vehicle velocity
            Assert.AreEqual(10f, result2.X);

            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 vehicleVel = new Vector3(-10f, -20f, -30f);
            Vector3 playerVel = new Vector3(-2f, -4f, -6f);

            Vector3 result = InertiaTransferCalculator.Compute(vehicleVel, playerVel, 1.0f, 100f, 100f);
            // Equal mass means average velocity (-6, -12, -18)
            Assert.AreEqual(-6f, result.X, 0.001f);
            Assert.AreEqual(-12f, result.Y, 0.001f);
            Assert.AreEqual(-18f, result.Z, 0.001f);

            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Max float protection
            Vector3 maxVel = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 result = InertiaTransferCalculator.Compute(maxVel, maxVel, 1.0f, float.MaxValue, float.MaxValue);
            Assert.IsTrue(float.IsFinite(result.X));
            Assert.AreEqual(float.MaxValue, result.X);

            // NaN checks
            Vector3 resultNan = InertiaTransferCalculator.Compute(new Vector3(float.NaN, 0, 0), new Vector3(1, 2, 3), 1.0f, 100f, 100f);
            Assert.AreEqual(new Vector3(1, 2, 3), resultNan);

            Vector3 resultNanPlayer = InertiaTransferCalculator.Compute(new Vector3(1, 2, 3), new Vector3(float.NaN, 0, 0), 1.0f, 100f, 100f);
            Assert.AreEqual(Vector3.Zero, resultNanPlayer);

            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
