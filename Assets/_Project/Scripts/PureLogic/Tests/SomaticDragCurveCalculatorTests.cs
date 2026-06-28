using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SomaticDragCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float drag = SomaticDragCurveCalculator.Compute(2f, 0f, 1f, 0.5f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(0.5f * 4f, drag, 0.01f, "Verify standard calculations return expected results.");

            float dragDeep = SomaticDragCurveCalculator.Compute(2f, 100f, 1f, 0.5f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(0.5f * 8f, dragDeep, 0.01f, "Deep drag uses cubic speed");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float drag = SomaticDragCurveCalculator.Compute(10f, 200f, 0f, 1f, 100f, 2f, 100000f, 0.0001f);
            // Depth > 100 => 100% cubic. Speed=10 => cubic=1000.
            // Integrity=0 => multiplier=3. Coeff=1. -> Total=3000.
            Assert.AreEqual(3000f, drag, 0.01f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float drag = SomaticDragCurveCalculator.Compute(0f, 10f, 1f, 1f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(0f, drag, "Verify zero inputs are handled without divide-by-zero or exception.");

            float dragZeroCoeff = SomaticDragCurveCalculator.Compute(10f, 10f, 1f, 0f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(0f, dragZeroCoeff);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float drag = SomaticDragCurveCalculator.Compute(-5f, -10f, -1f, -1f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(0f, drag, "Verify negative inputs clamp gracefully or throw.");

            float dragSpeedOnlyNeg = SomaticDragCurveCalculator.Compute(-5f, 10f, 1f, 1f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(0f, dragSpeedOnlyNeg);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float dragNaN = SomaticDragCurveCalculator.Compute(float.NaN, 10f, 1f, 1f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(0f, dragNaN, "Verify robust calculation and overflow protection.");

            float dragInf = SomaticDragCurveCalculator.Compute(float.PositiveInfinity, 10f, 1f, 1f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(0f, dragInf);

            float dragHuge = SomaticDragCurveCalculator.Compute(1000f, 1000f, 0f, 10f, 100f, 2f, 100000f, 0.0001f);
            Assert.AreEqual(100000f, dragHuge, 0.01f, "Clamps to max drag limit");
        }
    }
}
