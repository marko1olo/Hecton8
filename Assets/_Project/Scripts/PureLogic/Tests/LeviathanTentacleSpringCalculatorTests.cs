using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LeviathanTentacleSpringCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Vector3 current = new Vector3(1, 1, 1);
            Vector3 prev = new Vector3(0, 0, 0);
            Vector3 anchor = new Vector3(2, 2, 2);
            float spring = 10f;
            float damping = 0.9f;
            float dt = 0.02f;

            Vector3 result = LeviathanTentacleSpringCalculator.Compute(current, prev, anchor, spring, damping, dt);

            Assert.That(result.X, Is.EqualTo(1.904f).Within(0.0001f));
            Assert.That(result.Y, Is.EqualTo(1.904f).Within(0.0001f));
            Assert.That(result.Z, Is.EqualTo(1.904f).Within(0.0001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 current = new Vector3(1, 0, 0);
            Vector3 prev = new Vector3(0, 0, 0);
            Vector3 anchor = new Vector3(1, 0, 0);
            Vector3 result = LeviathanTentacleSpringCalculator.Compute(current, prev, anchor, 0f, 2f, 0.02f);

            Assert.That(result.X, Is.EqualTo(2f).Within(0.0001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 result = LeviathanTentacleSpringCalculator.Compute(Vector3.Zero, Vector3.Zero, Vector3.Zero, 0f, 0f, 0f);
            Assert.That(result, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 current = new Vector3(1, 0, 0);
            Vector3 prev = new Vector3(0, 0, 0);
            Vector3 anchor = new Vector3(2, 0, 0);

            Vector3 result = LeviathanTentacleSpringCalculator.Compute(current, prev, anchor, -10f, -0.5f, -0.1f);

            Assert.That(result.X, Is.EqualTo(1f).Within(0.0001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 result = LeviathanTentacleSpringCalculator.Compute(
                new Vector3(float.NaN, float.NaN, float.NaN),
                new Vector3(float.NaN, float.NaN, float.NaN),
                new Vector3(float.NaN, float.NaN, float.NaN),
                float.NaN, float.NaN, float.NaN);

            Assert.That(float.IsNaN(result.X), Is.False);
            Assert.That(float.IsNaN(result.Y), Is.False);
            Assert.That(float.IsNaN(result.Z), Is.False);

            result = LeviathanTentacleSpringCalculator.Compute(
                new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity),
                new Vector3(0,0,0),
                new Vector3(0,0,0),
                1f, 1f, 0.02f);

            Assert.That(float.IsInfinity(result.X), Is.False);
        }
    }
}
