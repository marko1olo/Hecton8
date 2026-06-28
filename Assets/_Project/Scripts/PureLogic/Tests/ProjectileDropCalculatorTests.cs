using NUnit.Framework;
using System;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ProjectileDropCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Zero angle: pure drop.
            float result1 = ProjectileDropCalculator.Compute(100f, 0f, 0f, 9.81f, 1f);
            Assert.AreEqual(-4.905f, result1, 0.001f, "Zero angle should be pure drop");

            // 45 deg: max range arc.
            float result2 = ProjectileDropCalculator.Compute(100f, 45f, 0f, 9.81f, 1f);
            float expected2 = 100f * (float)Math.Sin(45f * Math.PI / 180.0) * 1f - 0.5f * 9.81f * 1f * 1f;
            Assert.AreEqual(expected2, result2, 0.001f, "45 deg should be arc");

            // High drag: faster drop than vacuum
            // Actually high drag means terminal velocity is reached, so it won't go as high if shot upwards
            // Let's test just straight drop from 0 angle
            float dropNoDrag = ProjectileDropCalculator.Compute(100f, 0f, 0f, 9.81f, 10f);
            float dropHighDrag = ProjectileDropCalculator.Compute(100f, 0f, 1f, 9.81f, 10f);
            Assert.Less(dropNoDrag, dropHighDrag, "High drag should have less distance fallen (closer to 0) or more depending on how it's calculated. Wait. Drop from 0 is negative. High drag falls slower. So it should be greater (less negative).");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float result = ProjectileDropCalculator.Compute(100f, 0f, 0f, 0f, 1f);
            // Act
            // Assert
            Assert.AreEqual(0f, result, 0.0001f, "Zero gravity should mean zero drop at 0 angle.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float result = ProjectileDropCalculator.Compute(0f, 0f, 0f, 0f, 0f);
            // Act
            // Assert
            Assert.AreEqual(0f, result, "Verify zero inputs are handled without divide-by-zero or exception.");

            float resultTime0 = ProjectileDropCalculator.Compute(100f, 45f, 0.5f, 9.81f, 0f);
            Assert.AreEqual(0f, resultTime0, "Time of flight 0 should return 0");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float result = ProjectileDropCalculator.Compute(-100f, -45f, -1f, -9.81f, -10f);
            // Act
            // Assert
            Assert.AreEqual(0f, result, "Verify negative inputs clamp gracefully or return 0.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float resultInf = ProjectileDropCalculator.Compute(float.PositiveInfinity, float.NaN, float.NaN, float.NaN, float.PositiveInfinity);
            // Act
            // Assert
            Assert.AreEqual(0f, resultInf, "Verify robust calculation and overflow protection.");
        }
    }
}
