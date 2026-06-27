using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CavitationBurstShockwaveForceTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 center = new Vector3(0, 0, 0);
            Vector3 body = new Vector3(2, 0, 0); // distance = 2
            float energy = 8000f;
            float density = 1000f;

            // Act
            Vector3 force = CavitationBurstShockwaveForce.Calculate(body, center, energy, density);

            // Assert: Verify expected output behaviour
            // Distance = 2. Cube = 8.
            // Magnitude = 8000 / (8 * 1000) = 8000 / 8000 = 1.0f
            Assert.AreEqual(1.0f, force.X, 0.001f);
            Assert.AreEqual(0.0f, force.Y, 0.001f);
            Assert.AreEqual(0.0f, force.Z, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Distance so small that cube is < 0.0001f. Let's use distance = 0.01f -> cube = 0.000001f. This triggers cube clamp to 0.0001f.
            Vector3 center = new Vector3(0, 0, 0);
            Vector3 body = new Vector3(0.01f, 0, 0); // distance = 0.01
            float energy = 100f;
            float density = 1000f;

            // Act
            Vector3 force = CavitationBurstShockwaveForce.Calculate(body, center, energy, density);

            // Assert
            // cube clamped to 0.0001
            // magnitude = 100 / (0.0001 * 1000) = 100 / 0.1 = 1000
            Assert.AreEqual(1000.0f, force.X, 0.001f);
            Assert.AreEqual(0.0f, force.Y, 0.001f);
            Assert.AreEqual(0.0f, force.Z, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector3 center = new Vector3(0, 0, 0);
            Vector3 body = new Vector3(0, 0, 0); // exact center
            float energy = 100f;
            float density = 1000f;

            // Act
            Vector3 force = CavitationBurstShockwaveForce.Calculate(body, center, energy, density);

            // Assert
            Assert.AreEqual(0f, force.LengthSquared());

            // Also test zero energy / density
            force = CavitationBurstShockwaveForce.Calculate(new Vector3(1, 0, 0), center, 0f, 1000f);
            Assert.AreEqual(0f, force.LengthSquared());

            force = CavitationBurstShockwaveForce.Calculate(new Vector3(1, 0, 0), center, 100f, 0f);
            Assert.AreEqual(0f, force.LengthSquared());
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 center = new Vector3(0, 0, 0);
            Vector3 body = new Vector3(1, 0, 0);

            // Act
            Vector3 force = CavitationBurstShockwaveForce.Calculate(body, center, -100f, 1000f);
            Assert.AreEqual(0f, force.LengthSquared(), "Negative energy should return 0");

            force = CavitationBurstShockwaveForce.Calculate(body, center, 100f, -1000f);
            Assert.AreEqual(0f, force.LengthSquared(), "Negative density should return 0");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 center = new Vector3(0, 0, 0);
            Vector3 body = new Vector3(float.NaN, 0, 0);

            // Act
            Vector3 force = CavitationBurstShockwaveForce.Calculate(body, center, 100f, 1000f);

            // Assert
            Assert.AreEqual(0f, force.LengthSquared());

            body = new Vector3(float.PositiveInfinity, 0, 0);
            force = CavitationBurstShockwaveForce.Calculate(body, center, 100f, 1000f);
            Assert.AreEqual(0f, force.LengthSquared());

            body = new Vector3(1, 0, 0);
            force = CavitationBurstShockwaveForce.Calculate(body, center, float.PositiveInfinity, 1000f);
            Assert.AreEqual(0f, force.LengthSquared());
        }

        [Test]
        public void Test_Rules_InverseCubeAndLinearScale()
        {
            // Validate inverse-cube falloff over distance. Energy scales impulse linearly.
            Vector3 center = new Vector3(0, 0, 0);
            float energy = 8000f;
            float density = 1000f;

            // Base case distance = 2
            Vector3 forceDist2 = CavitationBurstShockwaveForce.Calculate(new Vector3(2, 0, 0), center, energy, density);
            float mag2 = forceDist2.Length();

            // Case distance = 4 (double distance) -> cube should be 8x smaller
            Vector3 forceDist4 = CavitationBurstShockwaveForce.Calculate(new Vector3(4, 0, 0), center, energy, density);
            float mag4 = forceDist4.Length();

            Assert.AreEqual(mag2 / 8f, mag4, 0.001f, "Inverse cube rule failed");

            // Base case energy = 8000
            // Double energy = 16000 -> force should be double
            Vector3 forceDoubleEnergy = CavitationBurstShockwaveForce.Calculate(new Vector3(2, 0, 0), center, energy * 2f, density);
            float magDoubleEnergy = forceDoubleEnergy.Length();

            Assert.AreEqual(mag2 * 2f, magDoubleEnergy, 0.001f, "Linear energy scale failed");
        }
    }
}
