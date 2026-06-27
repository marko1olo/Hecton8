using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class FabricationRecipeYieldRollTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float yield1 = FabricationRecipeYieldRoll.Calculate(1f, 10f, 5f, 42f);
            float yield2 = FabricationRecipeYieldRoll.Calculate(1f, 10f, 5f, 42f);
            Assert.That(yield1, Is.EqualTo(yield2));
            Assert.That(yield1, Is.GreaterThanOrEqualTo(10f));
            Assert.That(yield1, Is.LessThanOrEqualTo(15f));

            float yieldSkill0 = FabricationRecipeYieldRoll.Calculate(0f, 10f, 5f, 42f);
            Assert.That(yieldSkill0, Is.EqualTo(10f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float yieldSkill2 = FabricationRecipeYieldRoll.Calculate(2f, 10f, 5f, 42f);
            Assert.That(yieldSkill2, Is.LessThanOrEqualTo(15f));

            float yieldSkillNeg = FabricationRecipeYieldRoll.Calculate(-1f, 10f, 5f, 42f);
            Assert.That(yieldSkillNeg, Is.EqualTo(10f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float result = FabricationRecipeYieldRoll.Calculate(0f, 0f, 0f, 0f);
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float result = FabricationRecipeYieldRoll.Calculate(-1f, -10f, -5f, -42f);
            Assert.That(result, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float resultNaN = FabricationRecipeYieldRoll.Calculate(float.NaN, 10f, 5f, 42f);
            Assert.That(resultNaN, Is.EqualTo(0f));

            float resultInf = FabricationRecipeYieldRoll.Calculate(float.PositiveInfinity, 10f, 5f, 42f);
            Assert.That(resultInf, Is.EqualTo(0f));

            float resultMax = FabricationRecipeYieldRoll.Calculate(1f, float.MaxValue / 2, float.MaxValue / 2, 42f);
            Assert.That(resultMax, Is.Not.NaN);
        }
    }
}
