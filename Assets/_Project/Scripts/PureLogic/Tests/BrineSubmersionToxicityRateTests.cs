using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class BrineSubmersionToxicityRateTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float density = 0.5f;
            float shielding = 0.2f;
            float time = 2.0f;

            float result = BrineSubmersionToxicityRate.Calculate(density, shielding, time);

            Assert.That(result, Is.EqualTo(0.8f).Within(0.0001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // suitShielding = 1.0 blocks all toxicity
            float result1 = BrineSubmersionToxicityRate.Calculate(1.0f, 1.0f, 1.0f);
            Assert.That(result1, Is.EqualTo(0.0f).Within(0.0001f));

            // High density and 0 shielding yields max rate
            float result2 = BrineSubmersionToxicityRate.Calculate(1.0f, 0.0f, 1.0f);
            Assert.That(result2, Is.EqualTo(1.0f).Within(0.0001f));

            // Time boundary
            float result3 = BrineSubmersionToxicityRate.Calculate(1.0f, 0.0f, 0.0f);
            Assert.That(result3, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = BrineSubmersionToxicityRate.Calculate(0.0f, 0.0f, 0.0f);
            Assert.That(result, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result1 = BrineSubmersionToxicityRate.Calculate(-1.0f, -0.5f, -2.0f);
            Assert.That(result1, Is.EqualTo(0.0f).Within(0.0001f));

            float result2 = BrineSubmersionToxicityRate.Calculate(-1.0f, -1.0f, 1.0f);
            Assert.That(result2, Is.EqualTo(0.0f).Within(0.0001f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float result1 = BrineSubmersionToxicityRate.Calculate(float.NaN, 0.5f, 1.0f);
            Assert.That(result1, Is.EqualTo(0.0f).Within(0.0001f));

            // if suitShielding is NaN, it's defaulted to 0, density is 1, time is 1, so dose is 1.0f
            float result2 = BrineSubmersionToxicityRate.Calculate(1.0f, float.NaN, 1.0f);
            Assert.That(result2, Is.EqualTo(1.0f).Within(0.0001f));

            float result3 = BrineSubmersionToxicityRate.Calculate(1.0f, 0.5f, float.PositiveInfinity);
            Assert.That(result3, Is.EqualTo(0.0f).Within(0.0001f));

            float result4 = BrineSubmersionToxicityRate.Calculate(5.0f, 2.0f, 1.0f);
            Assert.That(result4, Is.EqualTo(0.0f).Within(0.0001f));

            float result5 = BrineSubmersionToxicityRate.Calculate(5.0f, 0.0f, 1.0f);
            Assert.That(result5, Is.EqualTo(1.0f).Within(0.0001f));
        }
    }
}
