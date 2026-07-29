using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BallastTankControllerTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentFillLevel = 0.5f;
            float targetFillLevel = 1.0f;
            float fillRate = 0.1f;
            float ventRate = 0.2f;
            float deltaTime = 2.0f;

            // Act
            float result = BallastTankController.Calculate(currentFillLevel, targetFillLevel, fillRate, ventRate, deltaTime);

            // Assert: Verify expected output behaviour
            // 0.5 + (0.1 * 2) = 0.7
            Assert.AreEqual(0.7f, result, 0.0001f);

            // Reverse happy path (venting)
            result = BallastTankController.Calculate(0.7f, 0.0f, fillRate, ventRate, deltaTime);
            // 0.7 - (0.2 * 2) = 0.3
            Assert.AreEqual(0.3f, result, 0.0001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float fillRate = 0.5f;
            float ventRate = 0.5f;
            float deltaTime = 1.0f;

            // Act
            float resultFillOvershoot = BallastTankController.Calculate(0.8f, 1.0f, fillRate, ventRate, deltaTime);
            float resultVentOvershoot = BallastTankController.Calculate(0.2f, 0.0f, fillRate, ventRate, deltaTime);
            float resultStable = BallastTankController.Calculate(1.0f, 1.0f, fillRate, ventRate, deltaTime);

            // Assert
            Assert.AreEqual(1.0f, resultFillOvershoot, 0.0001f);
            Assert.AreEqual(0.0f, resultVentOvershoot, 0.0001f);
            Assert.AreEqual(1.0f, resultStable, 0.0001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            //
            // There was a `float targetFillLevel = 0.5f;` here, unused, raising CS0219. It is REMOVED rather
            // than wired in, and that direction matters: the calls below deliberately pass a target of 1.0f,
            // i.e. a real fill demand that the zero deltaTime and the zero rates must then refuse to act on.
            // Substituting targetFillLevel would have made target == current, so both assertions would pass
            // trivially because there was nothing to do - weakening the test while silencing the warning.
            // The sibling CS0219s in LufsNormalizationCalculatorTests needed the OPPOSITE fix, which is why
            // this warning class cannot be cleared mechanically.
            float currentFillLevel = 0.5f;

            // Act
            float resultZeroTime = BallastTankController.Calculate(currentFillLevel, 1.0f, 0.1f, 0.1f, 0.0f);
            float resultZeroRates = BallastTankController.Calculate(currentFillLevel, 1.0f, 0.0f, 0.0f, 1.0f);

            // Assert
            Assert.AreEqual(0.5f, resultZeroTime, 0.0001f);
            Assert.AreEqual(0.5f, resultZeroRates, 0.0001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float resultNegativeCurrent = BallastTankController.Calculate(-0.5f, 1.0f, 0.1f, 0.1f, 1.0f);
            float resultNegativeTarget = BallastTankController.Calculate(0.5f, -1.0f, 0.1f, 0.1f, 1.0f);
            float resultNegativeRates = BallastTankController.Calculate(0.5f, 1.0f, -0.1f, -0.1f, 1.0f);
            float resultNegativeTime = BallastTankController.Calculate(0.5f, 1.0f, 0.1f, 0.1f, -1.0f);
            float resultAboveOne = BallastTankController.Calculate(1.5f, 2.0f, 0.1f, 0.1f, 1.0f);

            // Assert
            Assert.AreEqual(0.1f, resultNegativeCurrent, 0.0001f); // 0.0 (clamped) + 0.1 * 1.0 = 0.1
            Assert.AreEqual(0.4f, resultNegativeTarget, 0.0001f); // 0.5 - 0.1 * 1.0 = 0.4 (target clamped to 0)
            Assert.AreEqual(0.5f, resultNegativeRates, 0.0001f); // rates clamped to 0
            Assert.AreEqual(0.5f, resultNegativeTime, 0.0001f); // time clamped to >0 returns current clamped
            Assert.AreEqual(1.0f, resultAboveOne, 0.0001f); // current clamped to 1.0, target clamped to 1.0 -> 1.0
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float resultInfinityCurrent = BallastTankController.Calculate(float.PositiveInfinity, 1.0f, 0.1f, 0.1f, 1.0f);
            float resultNaNTarget = BallastTankController.Calculate(0.5f, float.NaN, 0.1f, 0.1f, 1.0f);
            float resultHugeDeltaTime = BallastTankController.Calculate(0.1f, 1.0f, 0.1f, 0.1f, 1000000.0f);

            // Assert
            // Infinity current -> clamped to 0 -> fills to 0.1
            Assert.AreEqual(0.1f, resultInfinityCurrent, 0.0001f);
            // NaN target -> clamped to 0 -> vents from 0.5 to 0.4
            Assert.AreEqual(0.4f, resultNaNTarget, 0.0001f);
            // Huge delta time -> caps at target 1.0
            Assert.AreEqual(1.0f, resultHugeDeltaTime, 0.0001f);
        }
    }
}