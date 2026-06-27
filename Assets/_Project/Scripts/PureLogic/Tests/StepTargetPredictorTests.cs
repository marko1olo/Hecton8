using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class StepTargetPredictorTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange: Setup standard test inputs
            Vector3 bodyPos = new Vector3(10f, 0f, 5f);
            Vector3 bodyVel = new Vector3(2f, 0f, 0f);
            float stepAhead = 0.5f;
            float radius = 1f;
            int legIdx = 0; // index 0 implies left side (-1), pair 0 (pairT 0 -> Z = 0.52f)
            int totalLegs = 4; // 2 pairs

            // Act
            Vector3 target = StepTargetPredictor.Calculate(bodyPos, bodyVel, stepAhead, radius, legIdx, totalLegs);

            // Assert
            // homeLocal = (-0.42f, 0f, 0.52f) * 1f = (-0.42f, 0f, 0.52f)
            // predicted = bodyPos(10, 0, 5) + homeLocal(-0.42, 0, 0.52) + bodyVel(2, 0, 0) * 0.5
            // predicted = (10 - 0.42 + 1, 0 + 0 + 0, 5 + 0.52 + 0) = (10.58f, 0f, 5.52f)
            Assert.AreEqual(10.58f, target.X, 0.001f);
            Assert.AreEqual(0f, target.Y, 0.001f);
            Assert.AreEqual(5.52f, target.Z, 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Setup boundary inputs (clamping thresholds, min/max values)
            Vector3 bodyPos = Vector3.Zero;
            Vector3 bodyVel = new Vector3(-1f, 1f, -1f);

            // Act
            // Total legs 1, leg index 5 -> should clamp legIdx to 0
            Vector3 target = StepTargetPredictor.Calculate(bodyPos, bodyVel, 1f, 2f, 5, 1);

            // Assert
            // pairCount = max(1, 1 >> 1) = max(1, 0) = 1
            // legIdx 0 -> pairIndex = min(0, 0) = 0
            // pairCount <= 1 -> pairT = 0.5
            // side = 0 ? -1 : 1 -> -1
            // homeLocal = (-0.42, 0, 0.52 + 0.5 * -1.04) = (-0.42, 0, 0) * 2 = (-0.84, 0, 0)
            // predicted = 0 + (-0.84, 0, 0) + (-1, 1, -1) * 1 = (-1.84, 1, -1)
            Assert.AreEqual(-1.84f, target.X, 0.001f);
            Assert.AreEqual(1f, target.Y, 0.001f);
            Assert.AreEqual(-1f, target.Z, 0.001f);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Setup zero values (zero speed, zero volume, zero duration)
            Vector3 bodyPos = Vector3.Zero;
            Vector3 bodyVel = Vector3.Zero;

            // Act
            Vector3 target = StepTargetPredictor.Calculate(bodyPos, bodyVel, 0f, 0f, 0, 0);

            // Assert
            // legs 0 -> clamps to 1. Leg index 0 -> clamps to 0.
            // homeLocal radius is 0, so homeLocal = 0.
            // velocity * stepAhead = 0.
            // Result should be exactly Vector3.Zero.
            Assert.AreEqual(Vector3.Zero, target);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Setup negative or out-of-range inputs
            Vector3 bodyPos = new Vector3(-100f, -50f, -10f);
            Vector3 bodyVel = new Vector3(-5f, -2f, -1f);

            // Act
            // stepAheadFraction = -1.5f (should clamp to 0)
            // radius = -5f (should clamp to 0)
            Vector3 target = StepTargetPredictor.Calculate(bodyPos, bodyVel, -1.5f, -5f, -1, -5);

            // Assert
            // Both scale and stepAhead are clamped to 0, so result is just bodyPos
            Assert.AreEqual(bodyPos, target);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Setup extreme or infinity values
            Vector3 bodyPos = new Vector3(float.MaxValue / 10f, 0f, 0f);
            Vector3 bodyVel = new Vector3(float.MaxValue / 10f, 0f, 0f);

            // Act
            Vector3 target = StepTargetPredictor.Calculate(bodyPos, bodyVel, 2f, 1f, 100, 1000000);

            // Assert
            // Just verifying no division by zero or weird overflow crash since inputs are valid floats
            Assert.IsTrue(float.IsFinite(target.X));
            Assert.IsTrue(float.IsFinite(target.Y));
            Assert.IsTrue(float.IsFinite(target.Z));
        }
    }
}
