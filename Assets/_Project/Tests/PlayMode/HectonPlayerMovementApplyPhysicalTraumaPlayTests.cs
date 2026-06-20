using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Gameplay;

namespace Hecton8.Tests.PlayMode
{
    public class HectonPlayerMovementApplyPhysicalTraumaPlayTests
    {
        private GameObject _playerGo;
        private HectonPlayerMovement _movement;

        [SetUp]
        public void SetUp()
        {
            _playerGo = new GameObject("Player");
            _playerGo.AddComponent<Rigidbody>();
            _movement = _playerGo.AddComponent<HectonPlayerMovement>();

            // Set up physicalTraumaCollisionHoldTime to a default value so calculation doesn't multiply by 0 if it's default 0 in tests without Unity's serialized defaults
            var holdTimeField = typeof(HectonPlayerMovement).GetField("physicalTraumaCollisionHoldTime", BindingFlags.NonPublic | BindingFlags.Instance);
            if (holdTimeField != null)
            {
                holdTimeField.SetValue(_movement, 0.24f);
            }
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerGo != null)
            {
                Object.DestroyImmediate(_playerGo);
            }
        }

        [Test]
        public void ApplyPhysicalTrauma_ValidWeight_UpdatesInternalWeightAndTimer()
        {
            // Act
            _movement.ApplyPhysicalTrauma(new Vector3(1f, 0f, 0f), 0.5f);

            // Assert
            var weightField = typeof(HectonPlayerMovement).GetField("_physicalTraumaCollisionWeight", BindingFlags.NonPublic | BindingFlags.Instance);
            float weight = (float)weightField.GetValue(_movement);

            Assert.AreEqual(0.5f, weight, 0.001f);

            var timerField = typeof(HectonPlayerMovement).GetField("_physicalTraumaCollisionHoldTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            float timer = (float)timerField.GetValue(_movement);

            Assert.IsTrue(timer > 0f, "Hold timer should be greater than zero");
        }

        [Test]
        public void ApplyPhysicalTrauma_ZeroOrNegativeWeight_EarlyReturnsWithoutUpdatingState()
        {
            // Act
            _movement.ApplyPhysicalTrauma(new Vector3(1f, 0f, 0f), -0.5f);

            // Assert
            var weightField = typeof(HectonPlayerMovement).GetField("_physicalTraumaCollisionWeight", BindingFlags.NonPublic | BindingFlags.Instance);
            float weight = (float)weightField.GetValue(_movement);

            Assert.AreEqual(0f, weight, 0.001f);
        }

        [Test]
        public void ApplyPhysicalTrauma_ValidWeight_CallsSwimPresentationController()
        {
            // Arrange
            var swimGo = new GameObject("SwimController");
            swimGo.transform.SetParent(_playerGo.transform);
            var swimController = swimGo.AddComponent<PlayerSwimPresentationController>();

            var fieldInfo = typeof(HectonPlayerMovement).GetField("_swimPresentationController", BindingFlags.NonPublic | BindingFlags.Instance);
            fieldInfo.SetValue(_movement, swimController);

            // Act
            _movement.ApplyPhysicalTrauma(new Vector3(1f, 0f, 0f), 0.5f);

            // Assert
            var traumaBlendTargetField = typeof(PlayerSwimPresentationController).GetField("_physicalTraumaBlendTarget", BindingFlags.NonPublic | BindingFlags.Instance);
            float traumaBlendTarget = (float)traumaBlendTargetField.GetValue(swimController);
            Assert.AreEqual(0.5f, traumaBlendTarget, 0.001f, "Swim presentation controller internal state should be updated by the trauma.");

            var traumaHoldTimerField = typeof(PlayerSwimPresentationController).GetField("_physicalTraumaHoldTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            float traumaHoldTimer = (float)traumaHoldTimerField.GetValue(swimController);
            Assert.IsTrue(traumaHoldTimer > 0f, "Swim presentation controller trauma hold timer should be greater than zero.");
        }

        [Test]
        public void ApplyPhysicalTrauma_WeightHigherThanCurrent_UpdatesWeight()
        {
            // Arrange
            var weightField = typeof(HectonPlayerMovement).GetField("_physicalTraumaCollisionWeight", BindingFlags.NonPublic | BindingFlags.Instance);
            weightField.SetValue(_movement, 0.2f);

            // Act
            _movement.ApplyPhysicalTrauma(new Vector3(1f, 0f, 0f), 0.8f);

            // Assert
            float weight = (float)weightField.GetValue(_movement);
            Assert.AreEqual(0.8f, weight, 0.001f);
        }

        [Test]
        public void ApplyPhysicalTrauma_WeightLowerThanCurrent_DoesNotUpdateWeight()
        {
            // Arrange
            var weightField = typeof(HectonPlayerMovement).GetField("_physicalTraumaCollisionWeight", BindingFlags.NonPublic | BindingFlags.Instance);
            weightField.SetValue(_movement, 0.8f);

            // Act
            _movement.ApplyPhysicalTrauma(new Vector3(1f, 0f, 0f), 0.2f);

            // Assert
            float weight = (float)weightField.GetValue(_movement);
            Assert.AreEqual(0.8f, weight, 0.001f);
        }
    }
}
