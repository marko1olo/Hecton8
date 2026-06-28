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
            Assert.AreEqual(5f, result.X, 0.0001f);
            Assert.AreEqual(0f, result.Y, 0.0001f);
            Assert.AreEqual(2.5f, result.Z, 0.0001f);
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
            Assert.AreEqual(5f, resultSurface.X, 0.0001f);

            // Boundary 2: Deep depth where current drops to zero
            Vector3 resultDeep = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 50f, 0.02f);
            // decayFactor = 50 * 0.02 = 1, surfaceAdvection = 0, force = 0
            Assert.AreEqual(0f, resultDeep.X, 0.0001f);

            // Boundary 3: Very deep depth where current stays zero
            Vector3 resultVeryDeep = SurfaceCurrentWindshearVector.Calculate(windVector, windStrength, 100f, 0.02f);
            // decayFactor = 100 * 0.02 = 2 clamped to 1, surfaceAdvection = 0, force = 0
            Assert.AreEqual(0f, resultVeryDeep.X, 0.0001f);
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
            Assert.AreEqual(0f, result.X, 0.0001f);
            Assert.AreEqual(0f, result.Y, 0.0001f);
            Assert.AreEqual(0f, result.Z, 0.0001f);
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
            Assert.AreEqual(5f, result.X, 0.0001f);
            Assert.AreEqual(5f, result.Z, 0.0001f);
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
            Assert.AreEqual(Vector3.Zero, resultNan);
            Assert.AreEqual(Vector3.Zero, resultInf);
        }
    }
}
