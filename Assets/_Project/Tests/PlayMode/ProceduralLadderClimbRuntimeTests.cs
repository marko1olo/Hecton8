using NUnit.Framework;
using UnityEngine;
using System.Reflection;
using Hecton8.Animation.Locomotion;

namespace Hecton8.Tests.PlayMode
{
    public class ProceduralLadderClimbRuntimeTests
    {
        private GameObject _gameObject;
        private ProceduralLadderClimbRuntime _runtime;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject("ProceduralLadderClimbRuntimeTest");
            _runtime = _gameObject.AddComponent<ProceduralLadderClimbRuntime>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void FastTick_NotActive_DoesNothing()
        {
            // Arrange
            var activeField = typeof(ProceduralLadderClimbRuntime).GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);
            activeField.SetValue(_runtime, false);

            var climbProgressMetersField = typeof(ProceduralLadderClimbRuntime).GetField("_climbProgressMeters", BindingFlags.NonPublic | BindingFlags.Instance);
            climbProgressMetersField.SetValue(_runtime, 1.0f);

            // Act
            _runtime.FastTick(0.1f);

            // Assert
            Assert.AreEqual(1.0f, (float)climbProgressMetersField.GetValue(_runtime), "Progress should not change when not active.");
        }

        [Test]
        public void FastTick_SolveScheduled_DoesNothing()
        {
            // Arrange
            var activeField = typeof(ProceduralLadderClimbRuntime).GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);
            activeField.SetValue(_runtime, true);

            var solveScheduledField = typeof(ProceduralLadderClimbRuntime).GetField("_solveScheduled", BindingFlags.NonPublic | BindingFlags.Instance);
            solveScheduledField.SetValue(_runtime, true);

            var climbProgressMetersField = typeof(ProceduralLadderClimbRuntime).GetField("_climbProgressMeters", BindingFlags.NonPublic | BindingFlags.Instance);
            climbProgressMetersField.SetValue(_runtime, 1.0f);

            // Act
            _runtime.FastTick(0.1f);

            // Assert
            Assert.AreEqual(1.0f, (float)climbProgressMetersField.GetValue(_runtime), "Progress should not change when solve is scheduled.");
        }

        [Test]
        public void FastTick_StaminaDepletedAndMoving_SetsPendingSlipAndFinish()
        {
            // Arrange
            var activeField = typeof(ProceduralLadderClimbRuntime).GetField("_active", BindingFlags.NonPublic | BindingFlags.Instance);
            activeField.SetValue(_runtime, true);

            var solveScheduledField = typeof(ProceduralLadderClimbRuntime).GetField("_solveScheduled", BindingFlags.NonPublic | BindingFlags.Instance);
            solveScheduledField.SetValue(_runtime, false);

            var vrGripRequiredField = typeof(ProceduralLadderClimbRuntime).GetField("_vrGripRequired", BindingFlags.NonPublic | BindingFlags.Instance);
            vrGripRequiredField.SetValue(_runtime, false); // PC Mode

            var climbDirectionField = typeof(ProceduralLadderClimbRuntime).GetField("_climbDirection", BindingFlags.NonPublic | BindingFlags.Instance);
            climbDirectionField.SetValue(_runtime, 1.0f);

            var staminaField = typeof(ProceduralLadderClimbRuntime).GetField("_stamina01", BindingFlags.NonPublic | BindingFlags.Instance);
            staminaField.SetValue(_runtime, 0.0f); // Depleted stamina

            var climbProgressMetersField = typeof(ProceduralLadderClimbRuntime).GetField("_climbProgressMeters", BindingFlags.NonPublic | BindingFlags.Instance);
            climbProgressMetersField.SetValue(_runtime, 1.0f);

            var climbHeightMetersField = typeof(ProceduralLadderClimbRuntime).GetField("_climbHeightMeters", BindingFlags.NonPublic | BindingFlags.Instance);
            climbHeightMetersField.SetValue(_runtime, 5.0f);

            var speedField = typeof(ProceduralLadderClimbRuntime).GetField("pcSlideSpeedMetersPerSecond", BindingFlags.NonPublic | BindingFlags.Instance);
            if (speedField != null) speedField.SetValue(_runtime, 1.0f);

            // Act
            _runtime.FastTick(0.1f);

            // Assert
            var pendingSlipField = typeof(ProceduralLadderClimbRuntime).GetField("_pendingSlip", BindingFlags.NonPublic | BindingFlags.Instance);
            var pendingFinishField = typeof(ProceduralLadderClimbRuntime).GetField("_pendingFinish", BindingFlags.NonPublic | BindingFlags.Instance);

            // We expect the stamina block to be evaluated because vrGripRequired is false, which sets speed > 0, driving progress delta
            Assert.IsTrue((bool)pendingSlipField.GetValue(_runtime), "Pending slip should be true when stamina is depleted and moving.");
            Assert.IsTrue((bool)pendingFinishField.GetValue(_runtime), "Pending finish should be true when stamina is depleted and moving.");
        }
    }
}
