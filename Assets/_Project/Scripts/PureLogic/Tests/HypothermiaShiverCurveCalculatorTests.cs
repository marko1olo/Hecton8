using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class HypothermiaShiverCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float normalTemp = 37.0f;
            float shiversOnset = 35.0f;
            float incapacitationTemp = 28.0f;

            // Act & Assert
            // Above shiversOnset
            Assert.AreEqual(0f, HypothermiaShiverCurveCalculator.Compute(36.0f, normalTemp, shiversOnset, incapacitationTemp), 0.001f);

            // At shiversOnset
            Assert.AreEqual(0f, HypothermiaShiverCurveCalculator.Compute(35.0f, normalTemp, shiversOnset, incapacitationTemp), 0.001f);

            // Midpoint
            Assert.AreEqual(0.5f, HypothermiaShiverCurveCalculator.Compute(31.5f, normalTemp, shiversOnset, incapacitationTemp), 0.001f);

            // At incapacitation
            Assert.AreEqual(1f, HypothermiaShiverCurveCalculator.Compute(28.0f, normalTemp, shiversOnset, incapacitationTemp), 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float normalTemp = 37.0f;
            float shiversOnset = 35.0f;
            float incapacitationTemp = 28.0f;

            // Act
            // Below incapacitation (should clamp to 1)
            float belowIncap = HypothermiaShiverCurveCalculator.Compute(20.0f, normalTemp, shiversOnset, incapacitationTemp);

            // High temp (should clamp to 0)
            float highTemp = HypothermiaShiverCurveCalculator.Compute(45.0f, normalTemp, shiversOnset, incapacitationTemp);

            // Assert
            Assert.AreEqual(1f, belowIncap, 0.001f);
            Assert.AreEqual(0f, highTemp, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            float result1 = HypothermiaShiverCurveCalculator.Compute(0f, 0f, 0f, 0f);
            float result2 = HypothermiaShiverCurveCalculator.Compute(0f, 37f, 35f, 28f);

            // Assert
            // If shiversOnset == incapacitationTemp, range is 0. Math.Max(0.01f, range) handles division by zero.
            // When core=0, onset=0, incap=0: 0/0.01 = 0, clamped to 0.
            Assert.AreEqual(0f, result1, 0.001f);

            // core=0, onset=35, incap=28 -> factor = 35 / 7 = 5, clamped to 1.
            Assert.AreEqual(1f, result2, 0.001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float normalTemp = 37.0f;
            float shiversOnset = -10.0f;
            float incapacitationTemp = -20.0f;

            // Act
            float negativeCore = HypothermiaShiverCurveCalculator.Compute(-15.0f, normalTemp, shiversOnset, incapacitationTemp);
            float veryNegativeCore = HypothermiaShiverCurveCalculator.Compute(-30.0f, normalTemp, shiversOnset, incapacitationTemp);

            // Assert
            Assert.AreEqual(0.5f, negativeCore, 0.001f);
            Assert.AreEqual(1f, veryNegativeCore, 0.001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float normalTemp = 37.0f;
            float shiversOnset = 35.0f;
            float incapacitationTemp = 28.0f;

            // Act
            float resultNan = HypothermiaShiverCurveCalculator.Compute(float.NaN, normalTemp, shiversOnset, incapacitationTemp);
            float resultInf = HypothermiaShiverCurveCalculator.Compute(float.PositiveInfinity, normalTemp, shiversOnset, incapacitationTemp);
            float resultNegInf = HypothermiaShiverCurveCalculator.Compute(float.NegativeInfinity, normalTemp, shiversOnset, incapacitationTemp);

            float resultExtremeCore = HypothermiaShiverCurveCalculator.Compute(-1000000f, normalTemp, shiversOnset, incapacitationTemp);
            float resultExtremeOnset = HypothermiaShiverCurveCalculator.Compute(30f, normalTemp, 1000000f, incapacitationTemp);

            // Assert
            Assert.AreEqual(0f, resultNan);
            Assert.AreEqual(0f, resultInf);
            Assert.AreEqual(0f, resultNegInf); // Note: Since it's negative infinity, float.IsInfinity check catches it, returns 0
            Assert.AreEqual(1f, resultExtremeCore, 0.001f); // Range is 7, factor is huge, clamps to 1
            Assert.AreEqual(1f, resultExtremeOnset, 0.001f);
        }
    }
}
