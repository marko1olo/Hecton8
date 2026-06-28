using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ToxinBioaccumulationCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            float waterToxin = 2.0f;
            float factor = 1.5f;
            int level = 3;

            // Act
            float result = ToxinBioaccumulationCalculator.Compute(waterToxin, factor, level);

            // Assert
            // 2.0 * (1.5 ^ (3-1)) = 2.0 * (1.5 ^ 2) = 2.0 * 2.25 = 4.5
            Assert.AreEqual(4.5f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange
            float waterToxin = 2.0f;
            float factor = 1.5f;
            int level = 1;

            // Act
            float result = ToxinBioaccumulationCalculator.Compute(waterToxin, factor, level);

            // Assert
            // 2.0 * (1.5 ^ (1-1)) = 2.0 * (1.5 ^ 0) = 2.0 * 1 = 2.0
            Assert.AreEqual(2.0f, result, 0.001f, "Verify boundary trophic level of 1 returns the base water concentration.");

            // Testing boundary trophic level 0 which clamps to 0
            float resultLevelZero = ToxinBioaccumulationCalculator.Compute(waterToxin, factor, 0);
            Assert.AreEqual(2.0f, resultLevelZero, 0.001f, "Verify boundary trophic level of 0 acts same as 1 due to Math.Max(0, 0-1) vs Math.Max(0, 1-1). Both are 0.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange
            float waterToxin = 0f;
            float factor = 1.5f;
            int level = 3;

            // Act
            float result = ToxinBioaccumulationCalculator.Compute(waterToxin, factor, level);

            // Assert
            Assert.AreEqual(0f, result, "Verify 0f water concentration safely returns 0f.");

            float resultZeroFactor = ToxinBioaccumulationCalculator.Compute(2.0f, 0f, 3);
            Assert.AreEqual(0f, resultZeroFactor, "Verify 0f factor correctly evaluates to 0f.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange
            float waterToxin = -2.0f;
            float factor = -1.5f;
            int level = -5;

            // Act
            float result = ToxinBioaccumulationCalculator.Compute(waterToxin, factor, level);

            // Assert
            Assert.AreEqual(0f, result, "Verify negative inputs clamp and return 0f (or 1.0 multiplier).");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange
            float waterToxin = float.MaxValue;
            float factor = 2.0f;
            int level = 10;

            // Act
            float result = ToxinBioaccumulationCalculator.Compute(waterToxin, factor, level);

            // Assert
            Assert.AreEqual(float.MaxValue, result, "Verify extremely large inputs safely handle overflow by returning float.MaxValue instead of Infinity without throwing an exception.");

            float resultNaN = ToxinBioaccumulationCalculator.Compute(float.NaN, factor, level);
            Assert.AreEqual(0f, resultNaN, "Verify NaN inputs safely return 0f.");

            float resultInfinity = ToxinBioaccumulationCalculator.Compute(float.PositiveInfinity, factor, level);
            Assert.AreEqual(float.MaxValue, resultInfinity, "Verify Infinity inputs safely return float.MaxValue.");
        }
    }
}
