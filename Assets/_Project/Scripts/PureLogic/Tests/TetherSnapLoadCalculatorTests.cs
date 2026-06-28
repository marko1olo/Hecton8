using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class TetherSnapLoadCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Below breaking: no snap.
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(100f, 1f, 150f), "Expected no snap when load is below breaking strength.");

            // At 1.5x: definite snap.
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(100f, 1.6f, 150f), "Expected snap when dynamic spike pushes load past breaking strength.");

            // Dynamic spike can snap at lower static.
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(50f, 4f, 150f), "Expected snap when significant dynamic spike applied to low static load.");
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Exactly at breaking strength (should not snap, strictly greater than)
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(150f, 1f, 150f), "Expected no snap exactly at breaking strength limit.");

            // Just above breaking strength
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(150.001f, 1f, 150f), "Expected snap when marginally above breaking strength.");

            // Dynamic multiplier defaults to 1.0f if less than 1.0f
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(150f, 0.5f, 150f), "Expected no snap when multiplier < 1.0f defaults to 1.0f at limit.");
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(151f, 0.5f, 150f), "Expected snap when multiplier < 1.0f defaults to 1.0f and base load is above limit.");
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Zero strength, any load snaps except 0
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(1f, 1f, 0f), "Expected snap with 0 breaking strength and positive load.");
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(0f, 1f, 0f), "Expected no snap with 0 breaking strength and 0 load.");

            // Zero multiplier acts as 1f
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(100f, 0f, 50f), "Expected zero dynamic multiplier to clamp to 1 and snap.");
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(40f, 0f, 50f), "Expected zero dynamic multiplier to clamp to 1 and not snap.");
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Negative load clamped to 0
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(-50f, 1f, 10f), "Expected negative static load to clamp to 0 and not snap.");

            // Negative strength clamped to 0
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(10f, 1f, -10f), "Expected negative strength to clamp to 0 and snap with positive load.");
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(-10f, 1f, -10f), "Expected negative strength and load to both clamp to 0 and not snap.");
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // NaN handling
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(float.NaN, 1f, 100f), "Expected false when load is NaN.");
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(100f, float.NaN, 100f), "Expected false when multiplier is NaN.");
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(100f, 1f, float.NaN), "Expected false when strength is NaN.");

            // Infinity handling
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(float.PositiveInfinity, 1f, 100f), "Expected true with infinite static load.");
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(100f, float.PositiveInfinity, 100f), "Expected true with infinite multiplier.");
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(100f, 1f, float.PositiveInfinity), "Expected false with infinite strength.");
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(float.PositiveInfinity, 1f, float.PositiveInfinity), "Expected false with infinite load and infinite strength.");

            // Negative infinity
            Assert.IsFalse(TetherSnapLoadCalculator.Compute(float.NegativeInfinity, 1f, 10f), "Expected negative infinity load to clamp to 0.");
            Assert.IsTrue(TetherSnapLoadCalculator.Compute(10f, 1f, float.NegativeInfinity), "Expected negative infinity strength to clamp to 0.");
        }
    }
}
