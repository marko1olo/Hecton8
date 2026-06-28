using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FlockingBoidAlignmentVectorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 boidVel = new Vector3(1f, 0f, 0f);
            Vector3 neighborVel = new Vector3(0f, 1f, 0f);
            float maxSteer = 5f;

            // Act
            Vector3 steer = FlockingBoidAlignmentVector.Calculate(boidVel, neighborVel, maxSteer);

            // Assert: Verify expected output behaviour
            // steer = neighborVel - boidVel = (-1, 1, 0)
            Assert.AreEqual(new Vector3(-1f, 1f, 0f), steer);
            Assert.Pass("Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 boidVel = new Vector3(10f, 0f, 0f);
            Vector3 neighborVel = new Vector3(0f, 10f, 0f);
            float maxSteer = 1f;

            // Act
            Vector3 steer = FlockingBoidAlignmentVector.Calculate(boidVel, neighborVel, maxSteer);

            // Assert
            // steer = (-10, 10, 0), length = sqrt(200) = 14.14
            // clamped to 1f -> normalize(-10, 10, 0) * 1 = (-0.707, 0.707, 0)
            Assert.AreEqual(1f, steer.Length(), 0.0001f);
            Assert.Pass("Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector3 boidVel = Vector3.Zero;
            Vector3 neighborVel = Vector3.Zero;
            float maxSteer = 1f;

            // Act
            Vector3 steer = FlockingBoidAlignmentVector.Calculate(boidVel, neighborVel, maxSteer);

            // Assert
            Assert.AreEqual(Vector3.Zero, steer);
            Assert.Pass("Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 boidVel = new Vector3(1f, 1f, 1f);
            Vector3 neighborVel = new Vector3(2f, 2f, 2f);
            float maxSteer = -5f; // invalid steer

            // Act
            Vector3 steer = FlockingBoidAlignmentVector.Calculate(boidVel, neighborVel, maxSteer);

            // Assert
            Assert.AreEqual(Vector3.Zero, steer);
            Assert.Pass("Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 boidVel = new Vector3(float.NaN, 1f, 1f);
            Vector3 neighborVel = new Vector3(2f, 2f, 2f);
            float maxSteer = 5f;

            // Act
            Vector3 steer1 = FlockingBoidAlignmentVector.Calculate(boidVel, neighborVel, maxSteer);

            Vector3 boidVel2 = new Vector3(1f, 1f, 1f);
            Vector3 neighborVel2 = new Vector3(float.PositiveInfinity, 2f, 2f);
            Vector3 steer2 = FlockingBoidAlignmentVector.Calculate(boidVel2, neighborVel2, maxSteer);

            Vector3 boidVel3 = new Vector3(1e20f, 1e20f, 1e20f);
            Vector3 neighborVel3 = new Vector3(-1e20f, -1e20f, -1e20f);
            Vector3 steer3 = FlockingBoidAlignmentVector.Calculate(boidVel3, neighborVel3, 10f);


            // Assert
            Assert.AreEqual(Vector3.Zero, steer1);
            Assert.AreEqual(Vector3.Zero, steer2);
            // Even if extreme numbers, clamping should protect the output length
            Assert.IsTrue(steer3.Length() <= 10.001f);

            Assert.Pass("Verify robust calculation and overflow protection.");
        }
    }
}
