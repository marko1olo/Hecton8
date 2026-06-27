using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class VerletCableSimulatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 currentPos = new Vector3(0, 10, 0);
            Vector3 prevPos = new Vector3(0, 10, 0); // At rest initially
            float segmentRestLength = 1.0f;
            Vector3 gravity = new Vector3(0, -9.8f, 0);
            float dampingFactor = 0.99f;
            float deltaTime = 0.02f;

            // Act
            Vector3 result = VerletCableSimulator.Calculate(currentPos, prevPos, segmentRestLength, gravity, dampingFactor, deltaTime);

            // Assert
            Assert.That(result.X, Is.EqualTo(0));
            Assert.That(result.Z, Is.EqualTo(0));
            Assert.That(result.Y, Is.LessThan(10.0f));

            // Expected distance = 0.5 * g * t^2 (roughly, ignoring damping of velocity 0)
            // But since our formula is: velocity + acceleration * dt^2
            // velocity is 0. step = gravity * dt^2 = -9.8 * 0.0004 = -0.00392
            Assert.That(result.Y, Is.EqualTo(10.0f - 0.00392f).Within(0.0001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 currentPos = new Vector3(0, 10, 0);
            Vector3 prevPos = new Vector3(0, 10, 0);
            float segmentRestLength = 1.0f;
            Vector3 gravity = new Vector3(0, -9.8f, 0);
            float lowDeltaTime = -0.1f;
            float highDamping = 2.0f;
            float lowDamping = -0.5f;

            Vector3 resultLowDt = VerletCableSimulator.Calculate(currentPos, prevPos, segmentRestLength, gravity, 0.95f, lowDeltaTime);

            // dt should clamp to 0
            Assert.That(resultLowDt.Y, Is.EqualTo(10.0f).Within(0.0001f));

            currentPos = new Vector3(0, 10, 0);
            prevPos = new Vector3(0, 9, 0);

            Vector3 movingResultLowDamping = VerletCableSimulator.Calculate(currentPos, prevPos, segmentRestLength, Vector3.Zero, lowDamping, 0.02f);
            Vector3 movingResultHighDamping = VerletCableSimulator.Calculate(currentPos, prevPos, segmentRestLength, Vector3.Zero, highDamping, 0.02f);

            // damping should clamp to [0, 1]
            // velocity = 1, damping = 0 -> step = 0
            Assert.That(movingResultLowDamping.Y, Is.EqualTo(10.0f).Within(0.0001f));
            // velocity = 1, damping = 1 -> step = 1
            Assert.That(movingResultHighDamping.Y, Is.EqualTo(11.0f).Within(0.0001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 currentPos = Vector3.Zero;
            Vector3 prevPos = Vector3.Zero;
            float segmentRestLength = 0.0f;
            Vector3 gravity = Vector3.Zero;
            float dampingFactor = 0.0f;
            float deltaTime = 0.0f;

            Vector3 result = VerletCableSimulator.Calculate(currentPos, prevPos, segmentRestLength, gravity, dampingFactor, deltaTime);

            Assert.That(result, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 currentPos = new Vector3(-10, -20, -30);
            Vector3 prevPos = new Vector3(-10, -20, -30);
            float segmentRestLength = -5.0f;
            Vector3 gravity = new Vector3(0, 9.8f, 0);
            float dampingFactor = -1.0f;
            float deltaTime = -0.5f;

            Vector3 result = VerletCableSimulator.Calculate(currentPos, prevPos, segmentRestLength, gravity, dampingFactor, deltaTime);

            // dt clamps to 0, damping clamps to 0, no movement
            Assert.That(result.X, Is.EqualTo(-10.0f));
            Assert.That(result.Z, Is.EqualTo(-30.0f));
            Assert.That(result.Y, Is.EqualTo(-20.0f).Within(0.000001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 currentPos = new Vector3(1, 1, 1);
            Vector3 prevPos = new Vector3(0, 0, 0);
            float segmentRestLength = 1.0f;
            Vector3 gravity = new Vector3(float.NaN, float.NaN, float.NaN);

            Vector3 resultNaN = VerletCableSimulator.Calculate(currentPos, prevPos, segmentRestLength, gravity, 0.9f, 0.02f);

            Vector3 resultInf = VerletCableSimulator.Calculate(
                new Vector3(float.PositiveInfinity, 0, 0),
                prevPos, segmentRestLength, Vector3.Zero, 0.9f, 0.02f);

            Assert.That(resultNaN, Is.EqualTo(new Vector3(1, 1, 1)));
            Assert.That(resultInf, Is.EqualTo(Vector3.Zero));

            Vector3 extremeCurrent = new Vector3(10000, 10000, 10000);
            Vector3 extremePrev = new Vector3(0, 0, 0);
            Vector3 resultHuge = VerletCableSimulator.Calculate(extremeCurrent, extremePrev, 1.0f, Vector3.Zero, 0.99f, 0.02f);

            // step = 10000 * 0.99 = 9900
            // result = extremeCurrent + step = 19900
            Assert.That(resultHuge.X, Is.EqualTo(19900f).Within(0.001f));
        }
    }
}
