using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class OceanCurrentDragCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float dragCoeff = 1.0f;
            float area = 2.0f;

            Vector3 current = new Vector3(2f, 0f, 0f);

            // Player with current
            Vector3 playerWith = new Vector3(2f, 0f, 0f);
            Vector3 resWith = OceanCurrentDragCalculator.Compute(current, playerWith, dragCoeff, area);
            Assert.That(resWith.Length(), Is.EqualTo(0f).Within(0.0001f));

            // Against current
            Vector3 playerAgainst = new Vector3(-2f, 0f, 0f);
            Vector3 resAgainst = OceanCurrentDragCalculator.Compute(current, playerAgainst, dragCoeff, area);
            Assert.That(resAgainst.X, Is.EqualTo(16f).Within(0.001f));

            // Perpendicular
            Vector3 playerPerp = new Vector3(0f, 2f, 0f);
            Vector3 resPerp = OceanCurrentDragCalculator.Compute(current, playerPerp, dragCoeff, area);
            Assert.That(resPerp.X, Is.EqualTo(5.65685f).Within(0.001f));
            Assert.That(resPerp.Y, Is.EqualTo(-5.65685f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            Vector3 oceanCurrent = new Vector3(5f, 0f, 0f);
            Vector3 playerVel = new Vector3(5f, 0f, 0f);
            Vector3 result = OceanCurrentDragCalculator.Compute(oceanCurrent, playerVel, 1f, 1f);
            Assert.That(result.X, Is.EqualTo(0f));
            Assert.That(result.Y, Is.EqualTo(0f));
            Assert.That(result.Z, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            Vector3 oceanCurrent = new Vector3(1f, 1f, 1f);
            Vector3 playerVel = Vector3.Zero;

            Vector3 res1 = OceanCurrentDragCalculator.Compute(oceanCurrent, playerVel, 0f, 1f);
            Assert.That(res1.Length(), Is.EqualTo(0f));

            Vector3 res2 = OceanCurrentDragCalculator.Compute(oceanCurrent, playerVel, 1f, 0f);
            Assert.That(res2.Length(), Is.EqualTo(0f));

            Vector3 res3 = OceanCurrentDragCalculator.Compute(Vector3.Zero, Vector3.Zero, 1f, 1f);
            Assert.That(res3.Length(), Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Vector3 oceanCurrent = new Vector3(2f, 0f, 0f);
            Vector3 playerVel = Vector3.Zero;
            Vector3 result = OceanCurrentDragCalculator.Compute(oceanCurrent, playerVel, -1f, -2f);
            Assert.That(result.X, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Vector3 oceanCurrent = new Vector3(float.NaN, 0f, 0f);
            Vector3 resultNaN = OceanCurrentDragCalculator.Compute(oceanCurrent, Vector3.Zero, 1f, 1f);
            Assert.That(resultNaN.X, Is.EqualTo(0f));

            Vector3 resultInf = OceanCurrentDragCalculator.Compute(new Vector3(float.PositiveInfinity, 0, 0), Vector3.Zero, 1f, 1f);
            Assert.That(resultInf.X, Is.EqualTo(0f));

            Vector3 largeVel = new Vector3(1000f, 0f, 0f);
            Vector3 largeRes = OceanCurrentDragCalculator.Compute(largeVel, Vector3.Zero, 1f, 1f);
            Assert.That(float.IsFinite(largeRes.X), Is.True);
        }
    }
}
