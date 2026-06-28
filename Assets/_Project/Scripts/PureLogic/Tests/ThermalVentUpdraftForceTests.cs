using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ThermalVentUpdraftForceTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 ventCenter = new Vector3(0, 0, 0);
            Vector3 playerPosCenter = new Vector3(0, 10, 0);
            Vector3 playerPosEdge = new Vector3(10, 10, 0);
            float ventRadius = 20f;
            float coreForce = 100f;
            float decayFactor = 1f; // Linear decay

            // Act
            Vector3 forceCenter = ThermalVentUpdraftForce.Calculate(playerPosCenter, ventCenter, ventRadius, coreForce, decayFactor);
            Vector3 forceEdge = ThermalVentUpdraftForce.Calculate(playerPosEdge, ventCenter, ventRadius, coreForce, decayFactor);

            // Assert
            Assert.AreEqual(new Vector3(0, 100f, 0), forceCenter, "Force at center should be equal to core force.");
            Assert.AreEqual(new Vector3(0, 50f, 0), forceEdge, "Force at mid-radius with linear decay should be half.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            Vector3 ventCenter = new Vector3(0, 0, 0);
            Vector3 playerPosOutside = new Vector3(30, 10, 0);
            Vector3 playerPosBelow = new Vector3(0, -10, 0);
            float ventRadius = 20f;
            float coreForce = 100f;
            float decayFactor = 2f;

            // Act
            Vector3 forceOutside = ThermalVentUpdraftForce.Calculate(playerPosOutside, ventCenter, ventRadius, coreForce, decayFactor);
            Vector3 forceBelow = ThermalVentUpdraftForce.Calculate(playerPosBelow, ventCenter, ventRadius, coreForce, decayFactor);

            // Assert
            Assert.AreEqual(Vector3.Zero, forceOutside, "Force outside radius should be zero.");
            Assert.AreEqual(Vector3.Zero, forceBelow, "Force below vent center should be zero.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            Vector3 ventCenter = new Vector3(0, 0, 0);
            Vector3 playerPos = new Vector3(0, 10, 0);

            // Act
            Vector3 forceZeroRadius = ThermalVentUpdraftForce.Calculate(playerPos, ventCenter, 0f, 100f, 1f);
            Vector3 forceZeroForce = ThermalVentUpdraftForce.Calculate(playerPos, ventCenter, 20f, 0f, 1f);

            // Assert
            Assert.AreEqual(Vector3.Zero, forceZeroRadius, "Zero radius should return zero force.");
            Assert.AreEqual(Vector3.Zero, forceZeroForce, "Zero core force should return zero force.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            Vector3 ventCenter = new Vector3(0, 0, 0);
            Vector3 playerPos = new Vector3(10, 10, 0);
            float ventRadius = 20f;
            float coreForce = -100f;
            float decayFactor = -1f;

            // Act
            Vector3 force = ThermalVentUpdraftForce.Calculate(playerPos, ventCenter, ventRadius, coreForce, decayFactor);

            // Assert
            Assert.AreEqual(new Vector3(0, -100f, 0), force, "Negative decay factor should clamp to 0 and apply full force.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            Vector3 ventCenter = new Vector3(0, 0, 0);
            Vector3 playerPosNaN = new Vector3(float.NaN, 10, 0);
            float ventRadius = 20f;
            float coreForce = 100f;
            float decayFactor = 1f;

            // Act
            Vector3 forceNaN = ThermalVentUpdraftForce.Calculate(playerPosNaN, ventCenter, ventRadius, coreForce, decayFactor);

            // Assert
            Assert.AreEqual(Vector3.Zero, forceNaN, "NaN input should be handled gracefully, returning zero vector.");
        }
    }
}
