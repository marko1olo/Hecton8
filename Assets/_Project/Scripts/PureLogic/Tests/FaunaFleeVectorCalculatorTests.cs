using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FaunaFleeVectorCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Vector3 selfPos = Vector3.Zero;
            Vector3 threatPos = new Vector3(10f, 0f, 0f);
            Vector3[] obstacles = new Vector3[0];
            float avoidRadius = 5f;
            float bias = 1f;

            Vector3 result = FaunaFleeVectorCalculator.Compute(selfPos, threatPos, obstacles, avoidRadius, bias);
            Assert.AreEqual(new Vector3(-1f, 0f, 0f), result);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 selfPos = Vector3.Zero;
            Vector3 threatPos = new Vector3(0f, 10f, 0f);
            Vector3[] obstacles = new[] { new Vector3(5f, 0f, 0f) };
            float avoidRadius = 5f;
            float bias = 1f;

            Vector3 result = FaunaFleeVectorCalculator.Compute(selfPos, threatPos, obstacles, avoidRadius, bias);
            Assert.AreEqual(new Vector3(0f, -1f, 0f), result);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 selfPos = Vector3.Zero;
            Vector3 threatPos = Vector3.Zero;
            Vector3[] obstacles = null;
            float avoidRadius = 0f;
            float bias = 0f;

            Vector3 result = FaunaFleeVectorCalculator.Compute(selfPos, threatPos, obstacles, avoidRadius, bias);
            Assert.AreEqual(Vector3.UnitX, result);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 selfPos = Vector3.Zero;
            Vector3 threatPos = new Vector3(10f, 0f, 0f);
            Vector3[] obstacles = new[] { new Vector3(1f, 0f, 0f) };
            float avoidRadius = -5f;
            float bias = -1f;

            Vector3 result = FaunaFleeVectorCalculator.Compute(selfPos, threatPos, obstacles, avoidRadius, bias);
            Assert.AreEqual(new Vector3(-1f, 0f, 0f), result);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 selfPos = new Vector3(float.MaxValue * 0.1f, 0f, 0f);
            Vector3 threatPos = new Vector3(float.MaxValue * 0.1f, float.MaxValue * 0.1f, 0f);
            Vector3[] obstacles = new[] { new Vector3(float.NaN, float.PositiveInfinity, 0f) };
            float avoidRadius = float.PositiveInfinity;
            float bias = float.NaN;

            Vector3 result = FaunaFleeVectorCalculator.Compute(selfPos, threatPos, obstacles, avoidRadius, bias);
            Assert.AreEqual(Vector3.UnitX, result);
        }
    }
}
