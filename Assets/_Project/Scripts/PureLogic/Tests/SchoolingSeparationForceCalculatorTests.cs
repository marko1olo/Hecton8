using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SchoolingSeparationForceCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Vector3 selfPos = new Vector3(0, 0, 0);
            Vector3[] neighbors = new Vector3[] { new Vector3(2, 0, 0) };
            float radius = 5f;
            float force = 10f;

            Vector3 result = SchoolingSeparationForceCalculator.Compute(selfPos, neighbors, radius, force);

            Assert.That(result.X, Is.EqualTo(-6f).Within(0.001f));
            Assert.That(result.Y, Is.EqualTo(0f));
            Assert.That(result.Z, Is.EqualTo(0f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 selfPos = new Vector3(0, 0, 0);
            Vector3[] neighbors = new Vector3[] { new Vector3(5, 0, 0) };
            float radius = 5f;
            float force = 10f;

            Vector3 result = SchoolingSeparationForceCalculator.Compute(selfPos, neighbors, radius, force);

            Assert.That(result.Length(), Is.LessThan(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 selfPos = Vector3.Zero;
            Vector3[] neighbors = new Vector3[] { Vector3.Zero };

            Vector3 result = SchoolingSeparationForceCalculator.Compute(selfPos, neighbors, 5f, 10f);

            Assert.That(result.Length(), Is.GreaterThan(0f));
            Assert.That(float.IsNaN(result.X), Is.False);

            Vector3 noNeighborsResult = SchoolingSeparationForceCalculator.Compute(selfPos, new Vector3[0], 5f, 10f);
            Assert.That(noNeighborsResult, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 selfPos = new Vector3(0, 0, 0);
            Vector3[] neighbors = new Vector3[] { new Vector3(1, 1, 1) };

            Vector3 resultNegativeRadius = SchoolingSeparationForceCalculator.Compute(selfPos, neighbors, -5f, 10f);
            Vector3 resultNegativeForce = SchoolingSeparationForceCalculator.Compute(selfPos, neighbors, 5f, -10f);

            Assert.That(resultNegativeRadius, Is.EqualTo(Vector3.Zero));
            Assert.That(resultNegativeForce, Is.EqualTo(Vector3.Zero));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 selfPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3[] neighbors = new Vector3[] { new Vector3(float.MaxValue, float.MaxValue, float.MaxValue) };

            Vector3 result = SchoolingSeparationForceCalculator.Compute(selfPos, neighbors, float.MaxValue, float.MaxValue);

            Assert.That(float.IsNaN(result.X), Is.False);
            Assert.That(float.IsInfinity(result.X), Is.False);

            Vector3 nanSelf = SchoolingSeparationForceCalculator.Compute(new Vector3(float.NaN, 0, 0), neighbors, 5f, 10f);
            Assert.That(nanSelf, Is.EqualTo(Vector3.Zero));
        }
    }
}