using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BiomassResourceGradientWeightCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float result = BiomassResourceGradientWeightCalculator.Compute(1.0f, 0.5f, 10.0f);
            Assert.AreEqual(20.0f, result, 0.0001f, "Higher food heat value yields multiplier > 1.0 for herbivores");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float result = BiomassResourceGradientWeightCalculator.Compute(0.5f, 0.5f, 10.0f);
            Assert.AreEqual(10.0f, result, 0.0001f, "Equal values should yield multiplier of 1.0");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = BiomassResourceGradientWeightCalculator.Compute(0f, 0f, 10.0f);
            Assert.AreEqual(10.0f, result, 0.0001f, "Zero threshold should avoid divide by zero and return base weight");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = BiomassResourceGradientWeightCalculator.Compute(-1.0f, -0.5f, -10.0f);
            Assert.AreEqual(0f, result, 0.0001f, "Negative inputs clamp gracefully");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result = BiomassResourceGradientWeightCalculator.Compute(float.PositiveInfinity, float.NaN, 10.0f);
            Assert.AreEqual(10f, result, 0.0001f, "Verify robust calculation and overflow protection.");
        }
    }
}
