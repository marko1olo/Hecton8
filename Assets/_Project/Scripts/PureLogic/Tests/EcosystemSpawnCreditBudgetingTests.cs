using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class EcosystemSpawnCreditBudgetingTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float currentCredits = 10f;
            float maxCredits = 24f;
            float regenRate = 2.5f;
            float deltaSeconds = 1f;

            // Act
            float result = EcosystemSpawnCreditBudgeting.Calculate(currentCredits, maxCredits, regenRate, deltaSeconds);

            // Assert: Verify expected output behaviour
            Assert.AreEqual(12.5f, result, 0.001f, "Verify standard calculations return expected results.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float currentCredits = 23.5f;
            float maxCredits = 24f;
            float regenRate = 2.5f;
            float deltaSeconds = 1f;

            // Act
            float result = EcosystemSpawnCreditBudgeting.Calculate(currentCredits, maxCredits, regenRate, deltaSeconds);

            // Assert
            Assert.AreEqual(24f, result, "Verify boundary constraints clamp correctly.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float currentCredits = 0f;
            float maxCredits = 0f;
            float regenRate = 0f;
            float deltaSeconds = 0f;

            // Act
            float result = EcosystemSpawnCreditBudgeting.Calculate(currentCredits, maxCredits, regenRate, deltaSeconds);

            // Assert
            Assert.AreEqual(0f, result, "Verify zero inputs are handled without divide-by-zero or exception.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float currentCredits = -10f;
            float maxCredits = -5f;
            float regenRate = -2.5f;
            float deltaSeconds = -1f;

            // Act
            float result = EcosystemSpawnCreditBudgeting.Calculate(currentCredits, maxCredits, regenRate, deltaSeconds);

            // Assert
            Assert.AreEqual(0f, result, "Verify negative inputs clamp gracefully or throw.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float currentCredits = float.MaxValue - 1000f;
            float maxCredits = float.MaxValue;
            float regenRate = float.MaxValue / 100f;
            float deltaSeconds = 1f;

            // Act
            float result = EcosystemSpawnCreditBudgeting.Calculate(currentCredits, maxCredits, regenRate, deltaSeconds);

            // Assert
            Assert.AreEqual(float.MaxValue, result, "Verify robust calculation and overflow protection.");
        }

        [Test]
        public void Test_NegativeRegenAllowed_Case06()
        {
            // Arrange: Regen rate can be negative (draining budget)
            float currentCredits = 10f;
            float maxCredits = 24f;
            float regenRate = -2.5f;
            float deltaSeconds = 1f;

            // Act
            float result = EcosystemSpawnCreditBudgeting.Calculate(currentCredits, maxCredits, regenRate, deltaSeconds);

            // Assert
            Assert.AreEqual(7.5f, result, 0.001f, "Verify negative regeneration rate lowers budget.");
        }

        [Test]
        public void Test_InfinityMaxCredits_Case07()
        {
            // Arrange: maxCredits is infinity, deltaSeconds is positive
            float currentCredits = 10f;
            float maxCredits = float.PositiveInfinity;
            float regenRate = 2.5f;
            float deltaSeconds = 1f;

            // Act
            float result = EcosystemSpawnCreditBudgeting.Calculate(currentCredits, maxCredits, regenRate, deltaSeconds);

            // Assert
            Assert.AreEqual(12.5f, result, 0.001f, "Verify positive infinity maxCredits is handled safely.");
        }
    }
}
