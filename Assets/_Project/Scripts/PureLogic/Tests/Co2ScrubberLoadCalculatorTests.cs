using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class Co2ScrubberLoadCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // 1 crew resting, full capacity: net negative
            float result1 = Co2ScrubberLoadCalculator.Compute(1f, 0f, 10f, 1f);
            Assert.AreEqual(-9f, result1, 0.0001f);

            // 5 active, underpowered: net positive (CO2 rising)
            float result2 = Co2ScrubberLoadCalculator.Compute(5f, 1f, 5f, 1f);
            Assert.AreEqual(5f, result2, 0.0001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Exact balance
            float result = Co2ScrubberLoadCalculator.Compute(10f, 0f, 10f, 1f);
            Assert.AreEqual(0f, result, 0.0001f);

            // Activity level clamps to 1
            float result2 = Co2ScrubberLoadCalculator.Compute(1f, 5f, 0f, 1f);
            Assert.AreEqual(2f, result2, 0.0001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = Co2ScrubberLoadCalculator.Compute(0f, 0f, 0f, 0f);
            Assert.AreEqual(0f, result, 0.0001f);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Negative inputs clamp to 0
            float result = Co2ScrubberLoadCalculator.Compute(-5f, -1f, -10f, -1f);
            Assert.AreEqual(0f, result, 0.0001f);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // NaN inputs
            float result = Co2ScrubberLoadCalculator.Compute(float.NaN, float.NaN, float.NaN, float.NaN);
            Assert.AreEqual(0f, result, 0.0001f);

            // Infinity handling
            float result2 = Co2ScrubberLoadCalculator.Compute(float.MaxValue, 1f, 0f, 1f);
            Assert.AreEqual(float.MaxValue, result2);
        }
    }
}
