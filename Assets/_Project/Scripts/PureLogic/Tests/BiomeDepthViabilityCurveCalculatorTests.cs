using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BiomeDepthViabilityCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = BiomeDepthViabilityCurveCalculator.Compute(100f, 100f, 50f);
            Assert.AreEqual(1f, result, 0.001f, "At optimal depth, suitability should be 1.0");

            result = BiomeDepthViabilityCurveCalculator.Compute(120f, 100f, 50f);
            Assert.IsTrue(result > 0.1f && result < 1f, "Suitability should drop off within tolerance.");

            result = BiomeDepthViabilityCurveCalculator.Compute(80f, 100f, 50f);
            Assert.IsTrue(result > 0.1f && result < 1f, "Suitability should drop off symmetrically.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result = BiomeDepthViabilityCurveCalculator.Compute(200f, 100f, 20f);
            Assert.AreEqual(0f, result, 0.001f, "Outside tolerance dropping below 0.1 should clamp to 0.");

            result = BiomeDepthViabilityCurveCalculator.Compute(0f, 100f, 20f);
            Assert.AreEqual(0f, result, 0.001f, "Outside tolerance dropping below 0.1 should clamp to 0.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = BiomeDepthViabilityCurveCalculator.Compute(0f, 0f, 0f);
            Assert.AreEqual(1f, result, 0.001f, "Zero tolerance should still return 1.0 at optimal depth.");

            result = BiomeDepthViabilityCurveCalculator.Compute(10f, 0f, 0f);
            Assert.AreEqual(0f, result, 0.001f, "Zero tolerance should return 0.0 outside optimal depth.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = BiomeDepthViabilityCurveCalculator.Compute(-50f, 100f, 50f);
            Assert.AreEqual(0.135f, result, 0.01f, "Negative depth should be clamped to 0.");

            result = BiomeDepthViabilityCurveCalculator.Compute(100f, -50f, 50f);
            Assert.AreEqual(0.135f, result, 0.01f, "Negative optimal depth should be clamped to 0.");

            result = BiomeDepthViabilityCurveCalculator.Compute(100f, 100f, -50f);
            Assert.AreEqual(1f, result, 0.001f, "Negative tolerance should be clamped to a small positive value.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result = BiomeDepthViabilityCurveCalculator.Compute(float.NaN, 100f, 50f);
            Assert.AreEqual(0f, result, "NaN input should return 0.");

            result = BiomeDepthViabilityCurveCalculator.Compute(100f, float.PositiveInfinity, 50f);
            Assert.AreEqual(0f, result, "Infinity input should return 0.");

            result = BiomeDepthViabilityCurveCalculator.Compute(1000000f, 100f, 50f);
            Assert.AreEqual(0f, result, "Extreme values should gracefully drop to 0.");
        }
    }
}
