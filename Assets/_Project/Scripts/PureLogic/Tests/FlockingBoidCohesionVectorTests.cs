using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FlockingBoidCohesionVectorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Vector3 boidPos = new Vector3(1f, 1f, 1f);
            Vector3 neighborCenter = new Vector3(3f, 3f, 3f);
            float weight = 1f;

            Vector3 result = FlockingBoidCohesionVector.Calculate(boidPos, neighborCenter, weight);

            Vector3 expected = new Vector3(2f, 2f, 2f);
            Assert.AreEqual(expected.X, result.X, 0.001f);
            Assert.AreEqual(expected.Y, result.Y, 0.001f);
            Assert.AreEqual(expected.Z, result.Z, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 boidPos = new Vector3(0f, 0f, 0f);
            Vector3 neighborCenter = new Vector3(10f, 0f, 0f);
            // Weight above MaxBoidWeight should be clamped to MaxBoidWeight (64)
            float weight = 100f;

            Vector3 result = FlockingBoidCohesionVector.Calculate(boidPos, neighborCenter, weight);

            Vector3 expected = new Vector3(10f * 64f, 0f, 0f);
            Assert.AreEqual(expected.X, result.X, 0.001f);
            Assert.AreEqual(expected.Y, result.Y, 0.001f);
            Assert.AreEqual(expected.Z, result.Z, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 boidPos = new Vector3(5f, 5f, 5f);
            Vector3 neighborCenter = new Vector3(10f, 10f, 10f);
            float weight = 0f;

            Vector3 result = FlockingBoidCohesionVector.Calculate(boidPos, neighborCenter, weight);

            Assert.AreEqual(Vector3.Zero, result);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 boidPos = new Vector3(5f, 5f, 5f);
            Vector3 neighborCenter = new Vector3(10f, 10f, 10f);
            float weight = -1f; // Negative weight

            Vector3 result = FlockingBoidCohesionVector.Calculate(boidPos, neighborCenter, weight);

            Assert.AreEqual(Vector3.Zero, result);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 boidPos = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 neighborCenter = new Vector3(10f, 10f, 10f);
            float weight = 1f;

            Vector3 result = FlockingBoidCohesionVector.Calculate(boidPos, neighborCenter, weight);
            Assert.AreEqual(Vector3.Zero, result);

            // Expected to return zero on overflow due to float limitations

            // NaN handling tests
            Vector3 boidPosNaN = new Vector3(float.NaN, 0f, 0f);
            Vector3 resultNaN = FlockingBoidCohesionVector.Calculate(boidPosNaN, neighborCenter, weight);
            Assert.AreEqual(Vector3.Zero, resultNaN);

            Vector3 neighborCenterInf = new Vector3(float.PositiveInfinity, 0f, 0f);
            Vector3 resultInf = FlockingBoidCohesionVector.Calculate(boidPos, neighborCenterInf, weight);
            Assert.AreEqual(Vector3.Zero, resultInf);

            Vector3 resultWeightNaN = FlockingBoidCohesionVector.Calculate(new Vector3(0,0,0), neighborCenter, float.NaN);
            Assert.AreEqual(Vector3.Zero, resultWeightNaN);
        }
    }
}
