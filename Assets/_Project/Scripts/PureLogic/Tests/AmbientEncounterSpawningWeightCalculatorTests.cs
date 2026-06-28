using NUnit.Framework;
using System;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class AmbientEncounterSpawningWeightCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float baseWeight = 10f;
            float playerStress01 = 0.5f;
            float cooldownRemaining = 0f;
            float result = AmbientEncounterSpawningWeightCalculator.Compute(baseWeight, playerStress01, cooldownRemaining);
            Assert.That(result, Is.EqualTo(15f).Within(0.001f), "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float baseWeight = 10f;
            float playerStress01 = 1.5f;
            float cooldownRemaining = 0f;
            float result = AmbientEncounterSpawningWeightCalculator.Compute(baseWeight, playerStress01, cooldownRemaining);
            Assert.That(result, Is.EqualTo(20f).Within(0.001f), "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float baseWeight = 0f;
            float playerStress01 = 0f;
            float cooldownRemaining = 0f;
            float result = AmbientEncounterSpawningWeightCalculator.Compute(baseWeight, playerStress01, cooldownRemaining);
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            float baseWeight = -10f;
            float playerStress01 = -0.5f;
            float cooldownRemaining = -1f;
            float result = AmbientEncounterSpawningWeightCalculator.Compute(baseWeight, playerStress01, cooldownRemaining);
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            float baseWeight = float.NaN;
            float playerStress01 = float.PositiveInfinity;
            float cooldownRemaining = float.NaN;
            float result = AmbientEncounterSpawningWeightCalculator.Compute(baseWeight, playerStress01, cooldownRemaining);
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_CooldownActive_Case06()
        {
            float baseWeight = 10f;
            float playerStress01 = 1f;
            float cooldownRemaining = 0.5f;
            float result = AmbientEncounterSpawningWeightCalculator.Compute(baseWeight, playerStress01, cooldownRemaining);
            Assert.That(result, Is.EqualTo(0f).Within(0.001f), "If cooldown > 0, weight must be 0.");
        }
    }
}
