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
            Assert.That(force.X, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(force.Y, Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(force.Z, Is.EqualTo(0.0f).Within(0.001f));
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
            Assert.That(force.X, Is.EqualTo(1000.0f).Within(0.001f));
            Assert.That(force.Y, Is.EqualTo(0.0f).Within(0.001f));
            Assert.That(force.Z, Is.EqualTo(0.0f).Within(0.001f));
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
            Assert.That(force.LengthSquared(), Is.EqualTo(0f));

            // Also test zero energy / density
            force = CavitationBurstShockwaveForce.Calculate(new Vector3(1, 0, 0), center, 0f, 1000f);
            Assert.That(force.LengthSquared(), Is.EqualTo(0f));

            force = CavitationBurstShockwaveForce.Calculate(new Vector3(1, 0, 0), center, 100f, 0f);
            Assert.That(force.LengthSquared(), Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 center = new Vector3(0, 0, 0);
            Vector3 body = new Vector3(1, 0, 0);

            // Act
            Vector3 force = CavitationBurstShockwaveForce.Calculate(body, center, -100f, 1000f);
            Assert.That(force.LengthSquared(), Is.EqualTo(0f), "Negative energy should return 0");

            force = CavitationBurstShockwaveForce.Calculate(body, center, 100f, -1000f);
            Assert.That(force.LengthSquared(), Is.EqualTo(0f), "Negative density should return 0");
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
            Assert.That(force.LengthSquared(), Is.EqualTo(0f));

            body = new Vector3(float.PositiveInfinity, 0, 0);
            force = CavitationBurstShockwaveForce.Calculate(body, center, 100f, 1000f);
            Assert.That(force.LengthSquared(), Is.EqualTo(0f));

            body = new Vector3(1, 0, 0);
            force = CavitationBurstShockwaveForce.Calculate(body, center, float.PositiveInfinity, 1000f);
            Assert.That(force.LengthSquared(), Is.EqualTo(0f));
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

            Assert.That(mag4, Is.EqualTo(mag2 / 8f).Within(0.001f), "Inverse cube rule failed");

            // Base case energy = 8000
            // Double energy = 16000 -> force should be double
            Vector3 forceDoubleEnergy = CavitationBurstShockwaveForce.Calculate(new Vector3(2, 0, 0), center, energy * 2f, density);
            float magDoubleEnergy = forceDoubleEnergy.Length();

            Assert.That(magDoubleEnergy, Is.EqualTo(mag2 * 2f).Within(0.001f), "Linear energy scale failed");
        }

        [Test]
        public void Test_CalculateBatch_NullArrays()
        {
            Vector3 center = new Vector3(0, 0, 0);
            float energy = 8000f;
            float density = 1000f;

            // Should not throw
            Assert.DoesNotThrow(() => CavitationBurstShockwaveForce.CalculateBatch(null, center, energy, density, new Vector3[1]));
            Assert.DoesNotThrow(() => CavitationBurstShockwaveForce.CalculateBatch(new Vector3[1], center, energy, density, null));
            Assert.DoesNotThrow(() => CavitationBurstShockwaveForce.CalculateBatch(null, center, energy, density, null));
        }

        [Test]
        public void Test_CalculateBatch_EmptyArrays()
        {
            Vector3 center = new Vector3(0, 0, 0);
            float energy = 8000f;
            float density = 1000f;

            Vector3[] bodies = new Vector3[0];
            Vector3[] results = new Vector3[0];

            // Should not throw and do nothing
            Assert.DoesNotThrow(() => CavitationBurstShockwaveForce.CalculateBatch(bodies, center, energy, density, results));

            // Mismatched lengths
            Vector3[] results2 = new Vector3[5];
            Assert.DoesNotThrow(() => CavitationBurstShockwaveForce.CalculateBatch(bodies, center, energy, density, results2));
            Assert.That(results2[0], Is.EqualTo(Vector3.Zero)); // Should remain uninitialized

            Vector3[] bodies2 = new Vector3[5];
            Assert.DoesNotThrow(() => CavitationBurstShockwaveForce.CalculateBatch(bodies2, center, energy, density, results));
        }

        [Test]
        public void Test_CalculateBatch_ValidArrays()
        {
            Vector3 center = new Vector3(0, 0, 0);
            float energy = 8000f;
            float density = 1000f;

            Vector3[] bodies = new Vector3[] {
                new Vector3(2, 0, 0),
                new Vector3(4, 0, 0),
                new Vector3(0, 0, 0)
            };

            Vector3[] results = new Vector3[3];

            CavitationBurstShockwaveForce.CalculateBatch(bodies, center, energy, density, results);

            // Validate results match individual Calculate calls
            Assert.That(results[0].X, Is.EqualTo(1.0f).Within(0.001f));
            Assert.That(results[1].X, Is.EqualTo(0.125f).Within(0.001f));
            Assert.That(results[2], Is.EqualTo(Vector3.Zero));
        }
    }
}
