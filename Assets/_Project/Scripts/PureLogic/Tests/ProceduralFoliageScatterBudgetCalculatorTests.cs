using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ProceduralFoliageScatterBudgetCalculatorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            // Full FPS, 100% Quality
            int budget1 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 60f, 100, 1.0f);
            Assert.That(budget1, Is.EqualTo(100));

            // Half FPS, 100% Quality
            int budget2 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 30f, 100, 1.0f);
            Assert.That(budget2, Is.EqualTo(50));

            // Full FPS, 50% Quality
            int budget3 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 60f, 100, 0.5f);
            Assert.That(budget3, Is.EqualTo(50));

            // Half FPS, 50% Quality
            int budget4 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 30f, 100, 0.5f);
            Assert.That(budget4, Is.EqualTo(25));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // Quality weight > 1 should clamp to 1
            int budget1 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 60f, 100, 1.5f);
            Assert.That(budget1, Is.EqualTo(100));

            // FPS > target should clamp ratio to 1
            int budget2 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 120f, 100, 1.0f);
            Assert.That(budget2, Is.EqualTo(100));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Zero target FPS defaults to ratio 1, zero base budget -> 0
            int budget1 = ProceduralFoliageScatterBudgetCalculator.Compute(0f, 60f, 100, 1.0f);
            Assert.That(budget1, Is.EqualTo(100)); // targetfps becomes 0, ratio 1

            int budget2 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 0f, 100, 1.0f);
            Assert.That(budget2, Is.EqualTo(0)); // 0 fps means 0 budget

            int budget3 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 60f, 0, 1.0f);
            Assert.That(budget3, Is.EqualTo(0)); // 0 base budget
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Negative target FPS (defaults to ratio 1)
            int budget1 = ProceduralFoliageScatterBudgetCalculator.Compute(-10f, 60f, 100, 1.0f);
            Assert.That(budget1, Is.EqualTo(100));

            // Negative current FPS (clamps to 0)
            int budget2 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, -30f, 100, 1.0f);
            Assert.That(budget2, Is.EqualTo(0));

            // Negative quality (clamps to 0)
            int budget3 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 60f, 100, -0.5f);
            Assert.That(budget3, Is.EqualTo(0));

            // Negative base budget (returns 0)
            int budget4 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 60f, -100, 1.0f);
            Assert.That(budget4, Is.EqualTo(0));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            int budget1 = ProceduralFoliageScatterBudgetCalculator.Compute(float.NaN, 60f, 100, 1.0f);
            Assert.That(budget1, Is.EqualTo(100)); // NaN target FPS becomes 0, ratio 1

            int budget2 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, float.PositiveInfinity, 100, 1.0f);
            Assert.That(budget2, Is.EqualTo(0)); // Infinity current FPS is trapped, treated as 0

            int budget3 = ProceduralFoliageScatterBudgetCalculator.Compute(60f, 60f, int.MaxValue, 1.0f);
            Assert.That(budget3, Is.EqualTo(int.MaxValue));
        }
    }
}
