using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FaunaPatrolPathSmootherCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 p0 = new Vector3(0, 0, 0);
            Vector3 p1 = new Vector3(1, 0, 0);
            Vector3 p2 = new Vector3(2, 1, 0);
            Vector3 p3 = new Vector3(3, 0, 0);

            // Act & Assert
            Vector3 result0 = FaunaPatrolPathSmootherCalculator.Compute(p0, p1, p2, p3, 0f);
            Assert.AreEqual(p1, result0, "t=0 should return p1");

            Vector3 result1 = FaunaPatrolPathSmootherCalculator.Compute(p0, p1, p2, p3, 1f);
            Assert.AreEqual(p2, result1, "t=1 should return p2");

            Vector3 resultHalf = FaunaPatrolPathSmootherCalculator.Compute(p0, p1, p2, p3, 0.5f);
            Assert.IsTrue(resultHalf.X > 1f && resultHalf.X < 2f, "Interpolated X should be between p1 and p2");
            Assert.IsTrue(resultHalf.Y > 0f && resultHalf.Y <= 1f, "Interpolated Y should follow curve");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 p0 = new Vector3(0, 0, 0);
            Vector3 p1 = new Vector3(10, 0, 0);
            Vector3 p2 = new Vector3(20, 0, 0);
            Vector3 p3 = new Vector3(30, 0, 0);

            // Act
            Vector3 resultBelowZero = FaunaPatrolPathSmootherCalculator.Compute(p0, p1, p2, p3, -0.5f);
            Vector3 resultAboveOne = FaunaPatrolPathSmootherCalculator.Compute(p0, p1, p2, p3, 1.5f);

            // Assert
            Assert.AreEqual(p1, resultBelowZero, "t < 0 should clamp to 0 (p1)");
            Assert.AreEqual(p2, resultAboveOne, "t > 1 should clamp to 1 (p2)");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            Vector3 zero = Vector3.Zero;

            // Act
            Vector3 result = FaunaPatrolPathSmootherCalculator.Compute(zero, zero, zero, zero, 0.5f);

            // Assert
            Assert.AreEqual(Vector3.Zero, result, "Zero inputs should yield zero output safely");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 p0 = new Vector3(-10, -5, -2);
            Vector3 p1 = new Vector3(-5, -2, -1);
            Vector3 p2 = new Vector3(-2, -1, 0);
            Vector3 p3 = new Vector3(0, 0, 1);

            Vector3 result = FaunaPatrolPathSmootherCalculator.Compute(p0, p1, p2, p3, 0.5f);

            // Just verifying it computes successfully and roughly between p1 and p2
            Assert.IsTrue(result.X > -5 && result.X < -2);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // NaN handling
            Vector3 p1 = new Vector3(1, 1, 1);
            Vector3 nanVec = new Vector3(float.NaN, float.NaN, float.NaN);
            Vector3 resultNan = FaunaPatrolPathSmootherCalculator.Compute(nanVec, p1, nanVec, nanVec, float.NaN);

            // If p1 is not NaN, and others are NaN, our implementation clamps NaNs to Zero.
            // If t is NaN, t clamps to 0. So it evaluates at t=0, which should return the sanitized p1.
            Assert.AreEqual(p1, resultNan, "NaN inputs should be sanitized, yielding fallback (p1)");

            // Extreme large values
            Vector3 large = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            Vector3 resultLarge = FaunaPatrolPathSmootherCalculator.Compute(Vector3.Zero, large, large, Vector3.Zero, 0.5f);
            // Result will likely overflow to Infinity, which our sanitize method catches and returns p1
            Assert.AreEqual(large, resultLarge, "Overflow inputs should fallback to p1");
        }
    }
}
