using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class ArmReachIkSolverTests
    {
        private const float Epsilon = 0.0001f;
        private const float CollinearThreshold = 0.99f;

        [Test]
        public void Test_HappyPath_Case01()
        {
            // Arrange
            Vector3 shoulder = new Vector3(0, 0, 0);
            Vector3 target = new Vector3(0, 10, 0);
            float upper = 5f;
            float lower = 5f;

            // Act
            var result = ArmReachIkSolver.Solve(shoulder, target, upper, lower, Epsilon, CollinearThreshold);

            // Assert
            Assert.IsTrue(result.canReach);
            Assert.AreEqual(target, result.handPos);
        }

        [Test]
        public void Test_HappyPath_Triangle_Case02()
        {
            // Arrange
            Vector3 shoulder = new Vector3(0, 0, 0);
            Vector3 target = new Vector3(3, 4, 0); // distance 5
            float upper = 3f;
            float lower = 4f;

            // Act
            var result = ArmReachIkSolver.Solve(shoulder, target, upper, lower, Epsilon, CollinearThreshold);

            // Assert
            Assert.IsTrue(result.canReach);
            Assert.AreEqual(target.X, result.handPos.X, 0.001f);
            Assert.AreEqual(target.Y, result.handPos.Y, 0.001f);
            Assert.AreEqual(target.Z, result.handPos.Z, 0.001f);
            Assert.AreEqual(3f, (result.elbowPos - shoulder).Length(), 0.001f);
            Assert.AreEqual(4f, (result.handPos - result.elbowPos).Length(), 0.001f);
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            // Arrange: Max extension exactly
            Vector3 shoulder = new Vector3(0, 0, 0);
            Vector3 target = new Vector3(0, 10, 0);
            float upper = 4f;
            float lower = 6f;

            // Act
            var result = ArmReachIkSolver.Solve(shoulder, target, upper, lower, Epsilon, CollinearThreshold);

            // Assert
            Assert.IsTrue(result.canReach);
            Assert.AreEqual(target, result.handPos);
            Assert.AreEqual(new Vector3(0, 4, 0), result.elbowPos);
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            // Arrange: Zero lengths
            Vector3 shoulder = new Vector3(1, 1, 1);
            Vector3 target = new Vector3(2, 2, 2);
            float upper = 0f;
            float lower = 0f;

            // Act
            var result = ArmReachIkSolver.Solve(shoulder, target, upper, lower, Epsilon, CollinearThreshold);

            // Assert
            Assert.IsTrue(result.canReach);
            Assert.AreEqual(shoulder, result.handPos);
            Assert.AreEqual(shoulder, result.elbowPos);
        }

        [Test]
        public void Test_ZeroInputs_TargetAtShoulder()
        {
            // Arrange: Target exactly at shoulder
            Vector3 shoulder = new Vector3(1, 1, 1);
            Vector3 target = new Vector3(1, 1, 1);
            float upper = 5f;
            float lower = 5f;

            // Act
            var result = ArmReachIkSolver.Solve(shoulder, target, upper, lower, Epsilon, CollinearThreshold);

            // Assert
            Assert.IsTrue(result.canReach);
            Assert.AreEqual(shoulder, result.handPos);
            Assert.AreEqual(shoulder, result.elbowPos);
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            // Arrange: Negative lengths
            Vector3 shoulder = new Vector3(0, 0, 0);
            Vector3 target = new Vector3(0, 5, 0);
            float upper = -5f;
            float lower = -2f;

            // Act
            var result = ArmReachIkSolver.Solve(shoulder, target, upper, lower, Epsilon, CollinearThreshold);

            // Assert
            // Should be clamped to 0 length
            Assert.IsTrue(result.canReach);
            Assert.AreEqual(shoulder, result.handPos);
            Assert.AreEqual(shoulder, result.elbowPos);
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            // Arrange: Extremely far target
            Vector3 shoulder = new Vector3(0, 0, 0);
            Vector3 target = new Vector3(0, 1000000, 0);
            float upper = 10f;
            float lower = 15f;

            // Act
            var result = ArmReachIkSolver.Solve(shoulder, target, upper, lower, Epsilon, CollinearThreshold);

            // Assert
            // Too far to reach
            Assert.IsFalse(result.canReach);
            Assert.AreEqual(new Vector3(0, 25, 0), result.handPos);
            Assert.AreEqual(new Vector3(0, 10, 0), result.elbowPos);
        }

        [Test]
        public void Test_NaNAndInfinity()
        {
            // Arrange: NaN and Infinity inputs
            Vector3 shoulder = new Vector3(float.NaN, 0, 0);
            Vector3 target = new Vector3(0, float.PositiveInfinity, 0);
            float upper = float.NaN;
            float lower = float.PositiveInfinity;

            // Act
            var result = ArmReachIkSolver.Solve(shoulder, target, upper, lower, Epsilon, CollinearThreshold);

            // Assert
            // Should fallback to safe values. shoulder=(0,0,0), target=(0,0,0), upper=0, lower=0
            Assert.IsTrue(result.canReach);
            Assert.AreEqual(new Vector3(0, 0, 0), result.handPos);
            Assert.AreEqual(new Vector3(0, 0, 0), result.elbowPos);
        }
    }
}
