using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class RockAlignmentSplineNormalCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            Vector3 tangent = new Vector3(0f, 0f, 1f);
            Vector3 normal = new Vector3(0f, 1f, 0f);
            float[] result = RockAlignmentSplineNormalCalculator.Compute(tangent, normal);

            Assert.AreEqual(4, result.Length);
            // Identity quaternion is (0, 0, 0, 1) when looking forward (+Z) with up (+Y)
            Assert.AreEqual(0f, result[0], 0.001f);
            Assert.AreEqual(0f, result[1], 0.001f);
            Assert.AreEqual(0f, result[2], 0.001f);
            Assert.AreEqual(1f, result[3], 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 tangent = new Vector3(1f, 1f, 1f);
            Vector3 normal = new Vector3(0f, 1f, 0f);
            float[] result = RockAlignmentSplineNormalCalculator.Compute(tangent, normal);

            Assert.AreEqual(4, result.Length);
            Quaternion q = new Quaternion(result[0], result[1], result[2], result[3]);
            Assert.IsTrue(Math.Abs(q.LengthSquared() - 1f) < 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 tangent = Vector3.Zero;
            Vector3 normal = Vector3.Zero;
            float[] result = RockAlignmentSplineNormalCalculator.Compute(tangent, normal);

            Assert.AreEqual(4, result.Length);
            Assert.AreEqual(0f, result[0], 0.001f);
            Assert.AreEqual(0f, result[1], 0.001f);
            Assert.AreEqual(0f, result[2], 0.001f);
            Assert.AreEqual(1f, result[3], 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 tangent = new Vector3(-1f, 0f, 0f);
            Vector3 normal = new Vector3(0f, -1f, 0f);
            float[] result = RockAlignmentSplineNormalCalculator.Compute(tangent, normal);

            Assert.AreEqual(4, result.Length);
            Quaternion q = new Quaternion(result[0], result[1], result[2], result[3]);
            Assert.IsTrue(Math.Abs(q.LengthSquared() - 1f) < 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 tangent = new Vector3(float.MaxValue, 0f, float.MaxValue);
            Vector3 normal = new Vector3(0f, float.MaxValue, 0f);
            float[] result = RockAlignmentSplineNormalCalculator.Compute(tangent, normal);

            Assert.AreEqual(4, result.Length);
            Quaternion q = new Quaternion(result[0], result[1], result[2], result[3]);
            Assert.IsTrue(Math.Abs(q.LengthSquared() - 1f) < 0.001f );
        }

        [Test]
        public void Test_CollinearInputs_Case06()
        {
            Vector3 tangent = new Vector3(0f, 1f, 0f);
            Vector3 normal = new Vector3(0f, 1f, 0f);
            float[] result = RockAlignmentSplineNormalCalculator.Compute(tangent, normal);

            Assert.AreEqual(4, result.Length);
            Quaternion q = new Quaternion(result[0], result[1], result[2], result[3]);
            Assert.IsTrue(Math.Abs(q.LengthSquared() - 1f) < 0.001f);
        }
    }
}
