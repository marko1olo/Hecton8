using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Systems;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class GameStateTensionScorerTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs (Safe surface, full O2+HP)
            float threat = 0f;
            float depth = 0f;
            float oxygen = 1f;
            float hp = 1f;

            // Act
            float result = GameStateTensionScorer.Calculate(threat, depth, oxygen, hp);

            // Assert: Verify expected output behaviour
            Assert.That(result, Is.EqualTo(0f));

            // Active threat + low O2 + deep: near 1. Factors contribute proportionally.
            threat = 1f;
            depth = 1f;
            oxygen = 0f;
            hp = 0f;

            result = GameStateTensionScorer.Calculate(threat, depth, oxygen, hp);
            Assert.That(result, Is.EqualTo(1f));

            // Partial
            result = GameStateTensionScorer.Calculate(0.5f, 0.5f, 0.5f, 0.5f);
            Assert.That(result, Is.EqualTo(0.5f));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            float threat = 1f;
            float depth = 1f;
            float oxygen = 1f;
            float hp = 1f;

            // Act
            float result = GameStateTensionScorer.Calculate(threat, depth, oxygen, hp);

            // Assert
            Assert.That(result, Is.EqualTo(0.5f)); // (1+1+0+0)/4 = 0.5
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            float threat = 0f;
            float depth = 0f;
            float oxygen = 0f;
            float hp = 0f;

            // Act
            float result = GameStateTensionScorer.Calculate(threat, depth, oxygen, hp);

            // Assert
            Assert.That(result, Is.EqualTo(0.5f)); // (0+0+1+1)/4 = 0.5
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            float threat = -1f;
            float depth = -2f;
            float oxygen = -5f;
            float hp = -10f;

            // Act
            float result = GameStateTensionScorer.Calculate(threat, depth, oxygen, hp);

            // Assert: Should clamp everything
            // threat=0, depth=0, oxygen=0, hp=0 -> 0.5
            Assert.That(result, Is.EqualTo(0.5f));

            result = GameStateTensionScorer.Calculate(2f, 3f, 5f, 10f);
            // threat=1, depth=1, oxygen=1, hp=1 -> 0.5
            Assert.That(result, Is.EqualTo(0.5f));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            float threat = float.PositiveInfinity;
            float depth = float.NegativeInfinity;
            float oxygen = float.NaN;
            float hp = float.MaxValue;

            // Act
            float result = GameStateTensionScorer.Calculate(threat, depth, oxygen, hp);

            // Assert: Verify robust calculation and overflow protection.
            // NaN or Inf threat => 0
            // NaN or Inf depth => 0
            // NaN or Inf oxygen => 1
            // MaxValue hp => 1
            // (0 + 0 + (1-1) + (1-1))/4 = 0
            Assert.That(result, Is.EqualTo(0f));

            result = GameStateTensionScorer.Calculate(float.NaN, float.NaN, float.NaN, float.NaN);
            // 0 + 0 + 0 + 0 = 0
            Assert.That(result, Is.EqualTo(0f));
        }
    }
}
