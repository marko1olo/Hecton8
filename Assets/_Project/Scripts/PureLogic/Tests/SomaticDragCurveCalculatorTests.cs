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

        /// <summary>
        /// The configuration parameters (depthScaleMax, brokenSuitMultiplier, maxDragClamp,
        /// epsilon) must be validated as strictly as the state parameters. Cases 01-05 only
        /// ever passed valid tuning values, so these leaks went undetected: each case below
        /// returned NaN before the guards were added, and NaN drag corrupts player velocity.
        /// </summary>
        [Test]
        public void Test_ConfigParametersNeverLeakNaN_Case06()
        {
            // depthMeters == depthScaleMax == 0 evaluated 0f/0f.
            float zeroScale = SomaticDragCurveCalculator.Compute(3f, 0f, 1f, 0.5f, 0f, 2f, 50f, 0.001f);
            Assert.That(float.IsNaN(zeroScale), Is.False, "zero depthScaleMax must not divide by zero");
            Assert.That(zeroScale, Is.EqualTo(4.5f).Within(0.01f), "falls back to surface behaviour");

            float nanMultiplier = SomaticDragCurveCalculator.Compute(3f, 0f, 0.5f, 0.5f, 100f, float.NaN, 50f, 0.001f);
            Assert.That(float.IsNaN(nanMultiplier), Is.False, "NaN brokenSuitMultiplier must not leak");

            float nanClamp = SomaticDragCurveCalculator.Compute(3f, 0f, 1f, 0.5f, 100f, 2f, float.NaN, 0.001f);
            Assert.That(float.IsNaN(nanClamp), Is.False, "NaN maxDragClamp must not leak");

            float nanEpsilon = SomaticDragCurveCalculator.Compute(3f, 0f, 1f, 0.5f, 100f, 2f, 50f, float.NaN);
            Assert.That(float.IsNaN(nanEpsilon), Is.False, "NaN epsilon must not leak");

            float infScale = SomaticDragCurveCalculator.Compute(3f, 10f, 1f, 0.5f, float.PositiveInfinity, 2f, 50f, 0.001f);
            Assert.That(float.IsNaN(infScale), Is.False, "infinite depthScaleMax must not leak");
        }

        /// <summary>
        /// Drag is a deceleration magnitude. A negative tuning value must never invert the
        /// sign, which would push the player forward instead of slowing them down.
        /// </summary>
        [Test]
        public void Test_DragIsNeverNegative_Case07()
        {
            float negativeClamp = SomaticDragCurveCalculator.Compute(3f, 0f, 1f, 0.5f, 100f, 2f, -5f, 0.001f);
            Assert.That(negativeClamp, Is.GreaterThanOrEqualTo(0f), "negative maxDragClamp must not accelerate");

            float negativeMultiplier = SomaticDragCurveCalculator.Compute(3f, 0f, 0f, 0.5f, 100f, -3f, 50f, 0.001f);
            Assert.That(negativeMultiplier, Is.GreaterThanOrEqualTo(0f), "negative brokenSuitMultiplier must not invert drag");

            float negativeScale = SomaticDragCurveCalculator.Compute(3f, 10f, 1f, 0.5f, -100f, 2f, 50f, 0.001f);
            Assert.That(negativeScale, Is.GreaterThanOrEqualTo(0f), "negative depthScaleMax must stay non-negative");
        }

        /// <summary>
        /// Guard additions must not alter results for valid configurations. These vectors
        /// mirror Cases 01-05 and pin the unchanged behaviour of the normal operating range.
        /// </summary>
        [Test]
        public void Test_ValidConfigBehaviourUnchanged_Case08()
        {
            Assert.That(SomaticDragCurveCalculator.Compute(2f, 0f, 1f, 0.5f, 100f, 2f, 100000f, 0.0001f),
                Is.EqualTo(2f).Within(0.01f));
            Assert.That(SomaticDragCurveCalculator.Compute(2f, 100f, 1f, 0.5f, 100f, 2f, 100000f, 0.0001f),
                Is.EqualTo(4f).Within(0.01f));
            Assert.That(SomaticDragCurveCalculator.Compute(10f, 200f, 0f, 1f, 100f, 2f, 100000f, 0.0001f),
                Is.EqualTo(3000f).Within(0.01f));
            Assert.That(SomaticDragCurveCalculator.Compute(3f, 50f, 0.5f, 0.5f, 100f, 2f, 100000f, 0.0001f),
                Is.EqualTo(0.5f * ((9f * 0.5f) + (27f * 0.5f)) * 2f).Within(0.01f));
        }
    }
}
