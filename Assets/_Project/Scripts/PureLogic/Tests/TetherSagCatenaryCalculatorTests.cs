using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class TetherSagCatenaryCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float sag = TetherSagCatenaryCalculator.Compute(10f, 0f, 12f, 1f);
            Assert.IsTrue(sag > 0f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float sag = TetherSagCatenaryCalculator.Compute(10f, 0f, 10f, 1f);
            Assert.AreEqual(0f, sag, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float sag = TetherSagCatenaryCalculator.Compute(0f, 0f, 0f, 0f);
            Assert.AreEqual(0f, sag, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float sag = TetherSagCatenaryCalculator.Compute(-10f, -5f, -12f, -1f);
            Assert.AreEqual(0f, sag, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float sag = TetherSagCatenaryCalculator.Compute(float.MaxValue, float.MaxValue, float.MaxValue, float.MaxValue);
            Assert.IsTrue(sag == 0f || sag == float.MaxValue, "Verify robust calculation and overflow protection.");
        }
    }
}
