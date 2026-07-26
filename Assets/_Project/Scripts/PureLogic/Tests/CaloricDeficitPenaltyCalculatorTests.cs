using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class CaloricDeficitPenaltyCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float threshold = 100f;
            float maxPenalty = 0.5f;

            // Positive balance: no penalty
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(50f, threshold, maxPenalty));

            // Halfway deficit
            Assert.AreEqual(0.25f, CaloricDeficitPenaltyCalculator.Compute(-50f, threshold, maxPenalty));

            // Max deficit
            Assert.AreEqual(0.5f, CaloricDeficitPenaltyCalculator.Compute(-100f, threshold, maxPenalty));

            // Beyond max deficit (clamped)
            Assert.AreEqual(0.5f, CaloricDeficitPenaltyCalculator.Compute(-150f, threshold, maxPenalty));

            // Exactly 0 balance
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(0f, threshold, maxPenalty));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)

            // maxPenalty greater than 1f should clamp to 1f
            Assert.AreEqual(1f, CaloricDeficitPenaltyCalculator.Compute(-100f, 100f, 2.5f));

            // maxPenalty less than 0f should clamp to 0f
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(-100f, 100f, -0.5f));

            // Deficit threshold negative (should be treated as absolute magnitude)
            Assert.AreEqual(0.25f, CaloricDeficitPenaltyCalculator.Compute(-50f, -100f, 0.5f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float maxPenalty = 0.5f;

            // Zero threshold and 0 balance
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(0f, 0f, maxPenalty));

            // Zero threshold and negative balance
            Assert.AreEqual(0.5f, CaloricDeficitPenaltyCalculator.Compute(-10f, 0f, maxPenalty));

            // Zero threshold and positive balance
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(10f, 0f, maxPenalty));

            // Max penalty is zero
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(-100f, 100f, 0f));

            // Everything is zero
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(0f, 0f, 0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // ALready covered most of it, but testing negative threshold logic more thoroughly

            float negativeThreshold = -200f;
            float maxPenalty = 0.8f;

            // Deficit of -100 with negative threshold of -200 should result in half penalty (0.4)
            Assert.AreEqual(0.4f, CaloricDeficitPenaltyCalculator.Compute(-100f, negativeThreshold, maxPenalty));

            // Same with positive threshold
            Assert.AreEqual(0.4f, CaloricDeficitPenaltyCalculator.Compute(-100f, 200f, maxPenalty));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float threshold = 100f;
            float maxPenalty = 0.5f;

            // NaN inputs
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(float.NaN, threshold, maxPenalty));
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(-50f, float.NaN, maxPenalty));
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(-50f, threshold, float.NaN));

            // Infinity inputs
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(float.PositiveInfinity, threshold, maxPenalty));
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(float.NegativeInfinity, threshold, maxPenalty));
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(-50f, float.PositiveInfinity, maxPenalty));
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(-50f, threshold, float.PositiveInfinity));

            // Extremely large numbers
            Assert.AreEqual(0.5f, CaloricDeficitPenaltyCalculator.Compute(-1e30f, 100f, maxPenalty));
            // The continuous model gives maxPenalty * (100 / 1e30) = 5e-29, not an exact 0f
            // (and the exact bits depend on the backend's intermediate precision), so this
            // asserts "negligible", not bit-exact zero.
            Assert.AreEqual(0f, CaloricDeficitPenaltyCalculator.Compute(-100f, 1e30f, maxPenalty), 1e-6f);
        }
    }
}
