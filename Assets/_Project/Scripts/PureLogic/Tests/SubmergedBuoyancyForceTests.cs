using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SubmergedBuoyancyForceTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float volume = 2.0f; // 2 m^3
            float density = 1000f; // 1000 kg/m^3 (water)
            float submergedFrac = 0.5f; // half submerged
            Vector3 gravity = new Vector3(0, -9.81f, 0);

            // Act
            Vector3 force = SubmergedBuoyancyForce.Calculate(volume, density, submergedFrac, gravity);

            // Assert: Verify expected output behaviour
            // Displaced volume = 2.0 * 0.5 = 1.0 m^3
            // Force = 1.0 * 1000 * 9.81 = 9810 upward
            Assert.That(force.X, Is.EqualTo(0f));
            Assert.That(force.Y, Is.EqualTo(9810f));
            Assert.That(force.Z, Is.EqualTo(0f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float volume = 1.0f;
            float density = 1000f;
            float submergedFrac = 1.5f; // More than 1, should clamp to 1
            Vector3 gravity = new Vector3(0, -10f, 0);

            // Act
            Vector3 force = SubmergedBuoyancyForce.Calculate(volume, density, submergedFrac, gravity);

            // Assert
            // Should clamp submergedFrac to 1.0. 1.0 * 1000 * 10 = 10000
            Assert.That(force.Y, Is.EqualTo(10000f));

            // Submerged < 0
            Vector3 forceNegative = SubmergedBuoyancyForce.Calculate(volume, density, -0.5f, gravity);
            Assert.That(forceNegative.Y, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float volume = 0f;
            float density = 0f;
            float submergedFrac = 0f;
            Vector3 gravity = Vector3.Zero;

            // Act
            Vector3 force = SubmergedBuoyancyForce.Calculate(volume, density, submergedFrac, gravity);

            // Assert
            Assert.That(force, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float volume = -5.0f;
            float density = -100f;
            float submergedFrac = 0.5f;
            Vector3 gravity = new Vector3(0, -10f, 0);

            // Act
            Vector3 force = SubmergedBuoyancyForce.Calculate(volume, density, submergedFrac, gravity);

            // Assert
            // Negative volume and density should clamp to 0
            Assert.That(force, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float volume = float.MaxValue;
            float density = 10f;
            float submergedFrac = 1f;
            Vector3 gravity = new Vector3(0, -10f, 0);

            // Act
            Vector3 force = SubmergedBuoyancyForce.Calculate(volume, density, submergedFrac, gravity);

            // Assert
            // This will likely overflow float.MaxValue * 10, resulting in Infinity, which we guard against.
            Assert.That(force, Is.EqualTo(Vector3.Zero));

            // Test NaN
            Vector3 forceNaN = SubmergedBuoyancyForce.Calculate(float.NaN, 1000f, 1f, gravity);
            Assert.That(forceNaN, Is.EqualTo(Vector3.Zero));
        }
    }
}
