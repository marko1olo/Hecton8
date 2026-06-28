using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ScooterThrustCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = ScooterThrustCurveCalculator.Compute(0.5f, 10f, 20f, 100f, 2f);
            // Throttle = 0.5
            // maxSpeed = 20
            // currentSpeed = 10 -> (1 - 10/20) = 0.5
            // thrustLimit = 100 * 0.5 = 50
            // dragForce = 2 * 10 = 20
            // NetForce = 0.5 * 50 - 0.5 * 20 = 25 - 10 = 15
            Assert.AreEqual(15f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Max speed case (throttle 1)
            float resultMax = ScooterThrustCurveCalculator.Compute(1f, 20f, 20f, 100f, 2f);
            Assert.AreEqual(0f, resultMax, 0.001f, "At maxSpeed: net force zero under full throttle.");

            // Full throttle zero speed case
            float resultZero = ScooterThrustCurveCalculator.Compute(1f, 0f, 20f, 100f, 2f);
            Assert.AreEqual(100f, resultZero, 0.001f, "Full throttle zero speed: maxThrust.");

            // Zero throttle only drag
            float resultDrag = ScooterThrustCurveCalculator.Compute(0f, 10f, 20f, 100f, 2f);
            Assert.AreEqual(-20f, resultDrag, 0.001f, "Zero throttle: only drag.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = ScooterThrustCurveCalculator.Compute(0f, 0f, 0f, 0f, 0f);
            Assert.AreEqual(0f, result, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");

            float resultZeroMaxSpeed = ScooterThrustCurveCalculator.Compute(1f, 10f, 0f, 100f, 2f);
            // safeMaxSpeed = 0.0001f
            // 1 - 10/0.0001 = negative -> Max(0, negative) = 0
            // netForce = 1 * 0 - 0 * dragForce = 0
            Assert.AreEqual(0f, resultZeroMaxSpeed, 0.001f, "Verify zero maxSpeed is handled safely.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Negative throttle should clamp to 0
            float result = ScooterThrustCurveCalculator.Compute(-0.5f, 10f, 20f, 100f, 2f);
            Assert.AreEqual(-20f, result, 0.001f, "Verify negative inputs clamp gracefully or throw.");

            // Negative speed
            float resultNegSpeed = ScooterThrustCurveCalculator.Compute(1f, -10f, 20f, 100f, 2f);
            // thrustLimit = 100 * Max(0, 1 - (-10/20)) = 100 * 1.5 = 150
            // netForce = 1 * 150 - 0 = 150
            Assert.AreEqual(150f, resultNegSpeed, 0.001f, "Negative speed provides forward thrust.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float resultNaN = ScooterThrustCurveCalculator.Compute(float.NaN, 10f, 20f, 100f, 2f);
            Assert.AreEqual(0f, resultNaN, "Verify robust calculation and overflow protection.");

            float resultInfinity = ScooterThrustCurveCalculator.Compute(1f, float.PositiveInfinity, 20f, 100f, 2f);
            Assert.AreEqual(0f, resultInfinity, "Verify Infinity is handled safely.");

            float resultLarge = ScooterThrustCurveCalculator.Compute(1f, 0f, 20f, 1e20f, 2f);
            Assert.AreEqual(1e20f, resultLarge, "Verify very large thrust works.");
        }
    }
}
