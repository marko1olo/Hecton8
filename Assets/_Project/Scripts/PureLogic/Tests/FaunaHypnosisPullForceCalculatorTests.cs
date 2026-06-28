using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FaunaHypnosisPullForceCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Vector3 playerPos = new Vector3(0, 0, 0);
            Vector3 sourcePos = new Vector3(3, 4, 0); // distance 5, sqrMagnitude 25
            float acceleration = 2f;
            float playerMass = 80f;
            float lockDuration = 2f;

            Vector3 result = FaunaHypnosisPullForceCalculator.Compute(playerPos, sourcePos, acceleration, playerMass, lockDuration);

            // direction = (3/5, 4/5, 0) = (0.6, 0.8, 0)
            // inverse square falloff = 1 / sqrMagnitude = 1 / 25 = 0.04
            // multiplier = 0.04 * 80 * 2 = 6.4
            // Expected force = (0.6 * 6.4, 0.8 * 6.4, 0) = (3.84, 5.12, 0)
            Assert.That(result.X, Is.EqualTo(3.84f).Within(0.01f));
            Assert.That(result.Y, Is.EqualTo(5.12f).Within(0.01f));
            Assert.That(result.Z, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 playerPos = new Vector3(0, 0, 0);
            Vector3 sourcePos = new Vector3(0.015f, 0, 0); // distance > 0.01 (sqrMagnitude > 0.0001)
            float acceleration = 1f;
            float playerMass = 1f;
            float lockDuration = 1f;

            Vector3 result = FaunaHypnosisPullForceCalculator.Compute(playerPos, sourcePos, acceleration, playerMass, lockDuration);
            Assert.That(result.LengthSquared(), Is.GreaterThan(0));

            // Boundary condition: exactly at threshold or slightly below
            Vector3 sourcePosLow = new Vector3(0.005f, 0, 0); // sqr = 0.000025 <= 0.0001
            Vector3 resultLow = FaunaHypnosisPullForceCalculator.Compute(playerPos, sourcePosLow, acceleration, playerMass, lockDuration);
            Assert.That(resultLow, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 playerPos = new Vector3(0, 0, 0);
            Vector3 sourcePos = new Vector3(0, 0, 0);

            Assert.That(FaunaHypnosisPullForceCalculator.Compute(playerPos, sourcePos, 1f, 1f, 1f), Is.EqualTo(Vector3.Zero));
            Assert.That(FaunaHypnosisPullForceCalculator.Compute(new Vector3(1,1,1), new Vector3(2,2,2), 0f, 1f, 1f), Is.EqualTo(Vector3.Zero));
            Assert.That(FaunaHypnosisPullForceCalculator.Compute(new Vector3(1,1,1), new Vector3(2,2,2), 1f, 0f, 1f), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 playerPos = new Vector3(1, 1, 1);
            Vector3 sourcePos = new Vector3(2, 2, 2);

            Assert.That(FaunaHypnosisPullForceCalculator.Compute(playerPos, sourcePos, -1f, 1f, 1f), Is.EqualTo(Vector3.Zero));
            Assert.That(FaunaHypnosisPullForceCalculator.Compute(playerPos, sourcePos, 1f, -1f, 1f), Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 playerPos = new Vector3(1, 1, 1);
            Vector3 sourcePos = new Vector3(2, 2, 2);

            Assert.That(FaunaHypnosisPullForceCalculator.Compute(playerPos, sourcePos, float.NaN, 1f, 1f), Is.EqualTo(Vector3.Zero));
            Assert.That(FaunaHypnosisPullForceCalculator.Compute(playerPos, sourcePos, float.PositiveInfinity, 1f, 1f), Is.EqualTo(Vector3.Zero));
            Assert.That(FaunaHypnosisPullForceCalculator.Compute(new Vector3(float.NaN, 0, 0), sourcePos, 1f, 1f, 1f), Is.EqualTo(Vector3.Zero));
        }
    }
}
