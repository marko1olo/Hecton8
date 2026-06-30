using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FabricatorBuildProgressCurveCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = FabricatorBuildProgressCurveCalculator.Compute(0f, 10f, 1f, 1f, 1f);
            Assert.AreEqual(0.1f, result, 0.0001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result = FabricatorBuildProgressCurveCalculator.Compute(0.95f, 10f, 1f, 1f, 1f);
            Assert.AreEqual(1f, result, 0.0001f, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = FabricatorBuildProgressCurveCalculator.Compute(0.5f, 0f, 1f, 1f, 1f);
            Assert.AreEqual(1f, result, 0.0001f, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = FabricatorBuildProgressCurveCalculator.Compute(-0.5f, -10f, -1f, -1f, -1f);
            Assert.AreEqual(0f, result, 0.0001f, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result = FabricatorBuildProgressCurveCalculator.Compute(0f, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);
            Assert.AreEqual(0f, result, 0.0001f, "Verify robust calculation and overflow protection.");

            float result2 = FabricatorBuildProgressCurveCalculator.Compute(float.NaN, float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.AreEqual(0f, result2, 0.0001f, "Verify robust calculation and overflow protection.");
        }
    }
}
