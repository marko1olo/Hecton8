using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ProjectileDamageFalloffCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float distance = 10f;
            float effectiveRange = 10f;
            float maxDamage = 100f;
            float minDamage = 10f;
            float exponent = 1f;

            // Act
            float resultZero = ProjectileDamageFalloffCalculator.Compute(0f, effectiveRange, maxDamage, minDamage, exponent);
            float resultMid = ProjectileDamageFalloffCalculator.Compute(distance, effectiveRange, maxDamage, minDamage, exponent);
            float resultMax = ProjectileDamageFalloffCalculator.Compute(distance * 2f, effectiveRange, maxDamage, minDamage, exponent);
            float resultBeyond = ProjectileDamageFalloffCalculator.Compute(distance * 3f, effectiveRange, maxDamage, minDamage, exponent);

            // Assert
            Assert.That(resultZero, Is.EqualTo(100f).Within(0.01f), "Zero range should yield maxDamage.");
            Assert.That(resultMid, Is.EqualTo(55f).Within(0.01f), "At effectiveRange, should yield midpoint.");
            Assert.That(resultMax, Is.EqualTo(10f).Within(0.01f), "At 2x effectiveRange, should yield minDamage.");
            Assert.That(resultBeyond, Is.EqualTo(10f).Within(0.01f), "Beyond 2x effectiveRange, should yield minDamage.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float effectiveRange = 100f;
            float maxDamage = 50f;
            float minDamage = 50f;

            // Act
            float result = ProjectileDamageFalloffCalculator.Compute(50f, effectiveRange, maxDamage, minDamage, 2f);

            // Assert
            Assert.That(result, Is.EqualTo(50f).Within(0.01f), "Damage should remain constant if min and max are equal.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange & Act
            float result = ProjectileDamageFalloffCalculator.Compute(0f, 0f, 100f, 10f, 1f);

            // Assert
            Assert.That(result, Is.EqualTo(100f).Within(0.01f), "Should return maxDamage when distance is 0, even if effectiveRange is 0.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float distance = -10f;
            float effectiveRange = -5f;
            float maxDamage = 100f;
            float minDamage = 10f;

            // Act
            float result = ProjectileDamageFalloffCalculator.Compute(distance, effectiveRange, maxDamage, minDamage, 1f);

            // Assert
            Assert.That(result, Is.EqualTo(100f).Within(0.01f), "Negative distance and range clamped safely, result should be maxDamage at 0 distance.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float maxFloat = float.MaxValue;

            // Act
            float resultNaN = ProjectileDamageFalloffCalculator.Compute(float.NaN, 10f, 100f, 10f, 1f);
            float resultInf = ProjectileDamageFalloffCalculator.Compute(float.PositiveInfinity, 10f, 100f, 10f, 1f);
            float resultExtreme = ProjectileDamageFalloffCalculator.Compute(maxFloat, 10f, 100f, 10f, 1f);

            // Assert
            Assert.That(resultNaN, Is.EqualTo(0f), "NaN input should return 0.");
            Assert.That(resultInf, Is.EqualTo(0f), "Infinity input should return 0.");
            Assert.That(resultExtreme, Is.EqualTo(10f), "Extreme distance should clamp to minDamage.");
        }
    }
}
