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
            Vector2 wind = new Vector2(10f, 5f);
            float strength = 2f;
            float depth = 10f;
            float decay = 50f;

            // Act
            Vector3 result = SurfaceCurrentWindshearVector.Calculate(wind, strength, depth, decay);

            // Assert: Verify expected output behaviour
            // surfaceAdvection01 = 1 - 10/50 = 0.8
            // 0.8 * 2 = 1.6
            // result = (16, 0, 8)
            Assert.AreEqual(16f, result.X, 0.001f);
            Assert.AreEqual(0f, result.Y, 0.001f);
            Assert.AreEqual(8f, result.Z, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector2 wind = new Vector2(1f, 1f);
            float strength = 1f;
            float depth = 50f;
            float decay = 50f;

            // Act
            Vector3 result = SurfaceCurrentWindshearVector.Calculate(wind, strength, depth, decay);

            // Assert
            // surfaceAdvection01 = 1 - 50/50 = 0
            Assert.AreEqual(0f, result.X, 0.001f);
            Assert.AreEqual(0f, result.Y, 0.001f);
            Assert.AreEqual(0f, result.Z, 0.001f);

            // Over max depth
            Vector3 result2 = SurfaceCurrentWindshearVector.Calculate(wind, strength, 100f, decay);
            Assert.AreEqual(0f, result2.X, 0.001f);
            Assert.AreEqual(0f, result2.Y, 0.001f);
            Assert.AreEqual(0f, result2.Z, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values
            Vector2 wind = Vector2.Zero;
            float strength = 0f;
            float depth = 0f;
            float decay = 0f;

            // Act
            Vector3 result = SurfaceCurrentWindshearVector.Calculate(wind, strength, depth, decay);

            // Assert
            Assert.AreEqual(0f, result.X, 0.001f);
            Assert.AreEqual(0f, result.Y, 0.001f);
            Assert.AreEqual(0f, result.Z, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector2 wind = new Vector2(-10f, -5f);
            float strength = -2f; // clamped to 0
            float depth = -10f;   // clamped to 0
            float decay = -50f;   // clamped to 0.0001f

            // Act
            Vector3 result = SurfaceCurrentWindshearVector.Calculate(wind, strength, depth, decay);

            // Assert
            Assert.AreEqual(0f, result.X, 0.001f);
            Assert.AreEqual(0f, result.Y, 0.001f);
            Assert.AreEqual(0f, result.Z, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector2 wind = new Vector2(float.PositiveInfinity, float.NaN);
            float strength = float.NaN;
            float depth = float.PositiveInfinity;
            float decay = 50f;

            // Act
            Vector3 result = SurfaceCurrentWindshearVector.Calculate(wind, strength, depth, decay);

            // Assert
            Assert.AreEqual(0f, result.X, 0.001f);
            Assert.AreEqual(0f, result.Y, 0.001f);
            Assert.AreEqual(0f, result.Z, 0.001f);
        }
    }
}
