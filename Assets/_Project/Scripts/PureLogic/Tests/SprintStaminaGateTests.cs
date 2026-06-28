using NUnit.Framework;
using System;
using System.Numerics;
using Hecton8.PureLogic.Kinematics;

namespace Hecton8.PureLogic.Tests
{
    [TestFixture]
    public class SprintStaminaGateTests
    {
        [Test]
        public void Test_HappyPath_Case01()
        {
            float sprintEnterThreshold = 50f;
            float sprintExitThreshold = 10f;
            Assert.IsFalse(SprintStaminaGate.EvaluateGate(40f, sprintEnterThreshold, sprintExitThreshold, false));
            Assert.IsTrue(SprintStaminaGate.EvaluateGate(60f, sprintEnterThreshold, sprintExitThreshold, false));
            Assert.IsTrue(SprintStaminaGate.EvaluateGate(40f, sprintEnterThreshold, sprintExitThreshold, true));
            Assert.IsFalse(SprintStaminaGate.EvaluateGate(5f, sprintEnterThreshold, sprintExitThreshold, true));
        }

        [Test]
        public void Test_Boundary_Case02()
        {
            float sprintEnterThreshold = 50f;
            float sprintExitThreshold = 10f;
            Assert.IsTrue(SprintStaminaGate.EvaluateGate(50f, sprintEnterThreshold, sprintExitThreshold, false));
            Assert.IsFalse(SprintStaminaGate.EvaluateGate(10f, sprintEnterThreshold, sprintExitThreshold, true));
        }

        [Test]
        public void Test_ZeroInputs_Case03()
        {
            float zeroEnter = 0f;
            float zeroExit = 0f;
            Assert.IsTrue(SprintStaminaGate.EvaluateGate(0f, zeroEnter, zeroExit, false));
            Assert.IsFalse(SprintStaminaGate.EvaluateGate(0f, zeroEnter, zeroExit, true));
        }

        [Test]
        public void Test_NegativeInputs_Case04()
        {
            Assert.IsTrue(SprintStaminaGate.EvaluateGate(-10f, -50f, -100f, false));
            Assert.IsFalse(SprintStaminaGate.EvaluateGate(-10f, 50f, -10f, true));
        }

        [Test]
        public void Test_ExtremeInputs_Case05()
        {
            Assert.IsTrue(SprintStaminaGate.EvaluateGate(float.MaxValue, 50f, 10f, false));
            Assert.IsFalse(SprintStaminaGate.EvaluateGate(float.NaN, 50f, 10f, true));
            Assert.IsFalse(SprintStaminaGate.EvaluateGate(float.PositiveInfinity, 50f, 10f, true));
            Assert.IsFalse(SprintStaminaGate.EvaluateGate(float.NegativeInfinity, 50f, 10f, false));
        }
    }
}
