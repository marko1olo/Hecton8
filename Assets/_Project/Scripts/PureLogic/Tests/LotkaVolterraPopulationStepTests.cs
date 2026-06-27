using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Ecosystem;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class LotkaVolterraPopulationStepTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            float prey = 100f;
            float pred = 10f;
            float alpha = 0.1f; // Prey growth
            float beta = 0.01f; // Predation rate
            float gamma = 0.05f; // Predator death
            float delta = 0.005f; // Conversion efficiency
            float dt = 1.0f;

            // Expected:
            // preyNext = 100 + (0.1 * 100 - 0.01 * (100*10)) * 1 = 100 + (10 - 10) = 100
            // predNext = 10 + (0.005 * 1000 - 0.05 * 10) * 1 = 10 + (5 - 0.5) = 14.5

            // Act
            var result = LotkaVolterraPopulationStep.Step(prey, pred, alpha, beta, gamma, delta, dt);

            // Assert: Verify expected output behaviour
            Assert.That(result.newPreyPop, Is.EqualTo(100f).Within(0.001f));
            Assert.That(result.newPredatorPop, Is.EqualTo(14.5f).Within(0.001f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            // No predators: prey grows
            float prey = 10f;
            float pred = 0f;
            float alpha = 0.1f;
            float beta = 0.01f;
            float gamma = 0.05f;
            float delta = 0.005f;
            float dt = 1.0f;

            // Expected prey = 10 + 0.1*10 = 11

            // Act
            var result = LotkaVolterraPopulationStep.Step(prey, pred, alpha, beta, gamma, delta, dt);

            // Assert
            Assert.That(result.newPreyPop, Is.EqualTo(11f).Within(0.001f));
            Assert.That(result.newPredatorPop, Is.EqualTo(0f).Within(0.001f));

            // No prey: predators die
            prey = 0f;
            pred = 10f;

            // Expected pred = 10 - 0.05*10 = 9.5
            result = LotkaVolterraPopulationStep.Step(prey, pred, alpha, beta, gamma, delta, dt);
            Assert.That(result.newPreyPop, Is.EqualTo(0f).Within(0.001f));
            Assert.That(result.newPredatorPop, Is.EqualTo(9.5f).Within(0.001f));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            // Act
            var result = LotkaVolterraPopulationStep.Step(0f, 0f, 0f, 0f, 0f, 0f, 0f);

            // Assert
            Assert.That(result.newPreyPop, Is.EqualTo(0f));
            Assert.That(result.newPredatorPop, Is.EqualTo(0f));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            // Should clamp populations to 0
            // Act
            var result = LotkaVolterraPopulationStep.Step(-10f, -5f, 0.1f, 0.1f, 0.1f, 0.1f, 1.0f);

            // Assert
            Assert.That(result.newPreyPop, Is.EqualTo(0f));
            Assert.That(result.newPredatorPop, Is.EqualTo(0f));

            // Negative dt should clamp to 0 dt, so no change
            result = LotkaVolterraPopulationStep.Step(10f, 5f, 0.1f, 0.1f, 0.1f, 0.1f, -1.0f);
            Assert.That(result.newPreyPop, Is.EqualTo(10f));
            Assert.That(result.newPredatorPop, Is.EqualTo(5f));

            // Huge negative drop should not result in negative pop, clamps to 0
            result = LotkaVolterraPopulationStep.Step(10f, 100f, 0.1f, 10f, 10f, 0.1f, 1.0f);
            Assert.That(result.newPreyPop, Is.EqualTo(0f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            // Act
            var result = LotkaVolterraPopulationStep.Step(float.PositiveInfinity, 10f, 0.1f, 0.1f, 0.1f, 0.1f, 1.0f);

            // Assert
            Assert.That(result.newPreyPop, Is.EqualTo(0f));
            Assert.That(result.newPredatorPop, Is.EqualTo(0f));

            result = LotkaVolterraPopulationStep.Step(10f, float.NaN, 0.1f, 0.1f, 0.1f, 0.1f, 1.0f);
            Assert.That(result.newPreyPop, Is.EqualTo(0f));
            Assert.That(result.newPredatorPop, Is.EqualTo(0f));

            result = LotkaVolterraPopulationStep.Step(10f, 10f, float.PositiveInfinity, 0.1f, 0.1f, 0.1f, 1.0f);
            Assert.That(result.newPreyPop, Is.EqualTo(10f));
            Assert.That(result.newPredatorPop, Is.EqualTo(10f));

            // Extreme large valid numbers that might cause overflow
            result = LotkaVolterraPopulationStep.Step(float.MaxValue, float.MaxValue, 0.1f, 0.1f, 0.1f, 0.1f, 1.0f);
            Assert.That(result.newPreyPop, Is.EqualTo(0f)); // Overflows to infinity, clamps to 0
        }
    }
}
