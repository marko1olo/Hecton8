using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class DepthGaugeNonlinearCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float maxDisplayDepth = 500f;
            float minAngleDeg = 0f;
            float maxAngleDeg = 180f;

            // Act
            float angle0 = DepthGaugeNonlinearCalculator.Compute(0f, maxDisplayDepth, minAngleDeg, maxAngleDeg);
            float angle10 = DepthGaugeNonlinearCalculator.Compute(10f, maxDisplayDepth, minAngleDeg, maxAngleDeg);
            float angle20 = DepthGaugeNonlinearCalculator.Compute(20f, maxDisplayDepth, minAngleDeg, maxAngleDeg);
            float angle500 = DepthGaugeNonlinearCalculator.Compute(500f, maxDisplayDepth, minAngleDeg, maxAngleDeg);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(0f, angle0, 0.001f);
            Assert.AreEqual(180f, angle500, 0.001f);

            float delta10 = angle10 - angle0;
            float delta20 = angle20 - angle10;
            Assert.IsTrue(delta10 > delta20, "Log scale: first 10m uses more arc than next 10m");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float maxDisplayDepth = 100f;
            float minAngleDeg = 10f;
            float maxAngleDeg = 100f;

            // Act
            float angleExceed = DepthGaugeNonlinearCalculator.Compute(150f, maxDisplayDepth, minAngleDeg, maxAngleDeg);

            // Assert
            Assert.AreEqual(100f, angleExceed, 0.001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float angleZeroMaxDepth = DepthGaugeNonlinearCalculator.Compute(50f, 0f, 0f, 180f);

            // Assert
            Assert.AreEqual(0f, angleZeroMaxDepth, 0.001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Act
            float angleNegativeDepth = DepthGaugeNonlinearCalculator.Compute(-50f, 100f, 0f, 180f);
            float angleNegativeMaxDepth = DepthGaugeNonlinearCalculator.Compute(50f, -100f, 0f, 180f);

            // Assert
            Assert.AreEqual(0f, angleNegativeDepth, 0.001f, "Verify negative inputs clamp gracefully or throw.");
            Assert.AreEqual(0f, angleNegativeMaxDepth, 0.001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            float angleNaN = DepthGaugeNonlinearCalculator.Compute(float.NaN, 100f, 0f, 180f);
            float angleInfinity = DepthGaugeNonlinearCalculator.Compute(float.PositiveInfinity, 100f, 0f, 180f);

            // Assert
            Assert.AreEqual(0f, angleNaN, 0.001f, "Verify robust calculation and overflow protection.");
            Assert.AreEqual(180f, angleInfinity, 0.001f, "Verify robust calculation and overflow protection.");
        }
    }
}
