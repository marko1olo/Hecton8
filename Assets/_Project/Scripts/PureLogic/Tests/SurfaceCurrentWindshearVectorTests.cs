using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SurfaceCurrentWindshearVectorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector2 windVector = new Vector2(1f, 0.5f);
            float windStrength = 10f;
            float depth = 25f;
            float decayRate = 1f / 50f; // 0.02

            // Act
            Vector3 result = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, depth, decayRate);

            // Assert: Verify expected output behaviour
            // decayFactor = 25 * 0.02 = 0.5
            // surfaceAdvection01 = 1 - 0.5 = 0.5
            // forceX = 1 * 10 * 0.5 = 5
            // forceZ = 0.5 * 10 * 0.5 = 2.5
            Assert.That(result.X, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.Z, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector2 windVector = new Vector2(1f, 1f);
            float windStrength = 5f;

            // Boundary 1: Surface depth = 0
            Vector3 resultSurface = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 0f, 0.02f);
            // decayFactor = 0, surfaceAdvection = 1, force = 5
            Assert.That(resultSurface.X, Is.EqualTo(5f).Within(0.0001f));

            // Boundary 2: Deep depth where current drops to zero
            Vector3 resultDeep = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 50f, 0.02f);
            // decayFactor = 50 * 0.02 = 1, surfaceAdvection = 0, force = 0
            Assert.That(resultDeep.X, Is.EqualTo(0f).Within(0.0001f));

            // Boundary 3: Very deep depth where current stays zero
            Vector3 resultVeryDeep = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 100f, 0.02f);
            // decayFactor = 100 * 0.02 = 2 clamped to 1, surfaceAdvection = 0, force = 0
            Assert.That(resultVeryDeep.X, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector2 zeroWind = Vector2.Zero;
            float zeroStrength = 0f;
            float zeroDepth = 0f;
            float zeroDecay = 0f;

            // Act
            Vector3 result = SurfaceCurrentWindshearVector.Calculate(zeroWind, zeroStrength, zeroDepth, zeroDecay);

            // Assert
            Assert.That(result.X, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.Y, Is.EqualTo(0f).Within(0.0001f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.0001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector2 windVector = new Vector2(-1f, -1f);
            float windStrength = -5f;
            float negativeDepth = -10f;
            float negativeDecay = -0.02f;

            // Act
            Vector3 result = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, negativeDepth, negativeDecay);

            // Assert
            // negative depth clamped to 0, negative decay clamped to 0
            // surfaceAdvection = 1
            // forceX = -1 * -5 * 1 = 5
            Assert.That(result.X, Is.EqualTo(5f).Within(0.0001f));
            Assert.That(result.Z, Is.EqualTo(5f).Within(0.0001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector2 windVector = new Vector2(1f, 1f);

            // Act
            Vector3 resultNan = SurfaceCurrentWindshearVector.Calculate(windVector, float.NaN, 10f, 0.02f);
            Vector3 resultInf = SurfaceCurrentWindshearVector.Calculate(windVector, float.PositiveInfinity, 10f, 0.02f);

            // Assert
            Assert.That(resultNan, Is.EqualTo(Vector3.Zero));
            Assert.That(resultInf, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ClampedBounds_EdgeCases()
        {
            Vector2 windVector = new Vector2(1f, 1f);
            float windStrength = 10f;
            float decayRate = 0.02f;

            // decayFactor = depth * decayRate
            // surfaceAdvection01 = 1f - Math.Min(Math.Max(decayFactor, 0f), 1f)
            // forceX = windVector.X * windStrength * surfaceAdvection01

            // Case A: Just below 1 (decayFactor = 0.99)
            Vector3 resultBelow = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 49.5f, decayRate);
            Assert.That(resultBelow.X, Is.EqualTo(0.1f).Within(0.0001f));

            // Case B: Exactly 1 (decayFactor = 1.0)
            Vector3 resultExact = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 50f, decayRate);
            Assert.That(resultExact.X, Is.EqualTo(0f).Within(0.0001f));

            // Case C: Just above 1 (decayFactor = 1.01)
            Vector3 resultAbove = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 50.5f, decayRate);
            Assert.That(resultAbove.X, Is.EqualTo(0f).Within(0.0001f));

            // Case D: Just above 0 (decayFactor = 0.01)
            Vector3 resultAboveZero = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 0.5f, decayRate);
            Assert.That(resultAboveZero.X, Is.EqualTo(9.9f).Within(0.0001f));
        }
    }
}
