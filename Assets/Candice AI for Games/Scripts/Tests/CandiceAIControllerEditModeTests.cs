using System;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceAIControllerEditModeTests
    {
        private GameObject _gameObject;
        private CandiceAIController _controller;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject();
            _controller = _gameObject.AddComponent<CandiceAIController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void AddRegistrationListener_AddsListenerToReadyStateListeners()
        {
            // Arrange
            bool listenerCalled = false;
            Action<bool, int> mockListener = (isRegistered, agentId) =>
            {
                listenerCalled = true;
            };

            // Act
            _controller.AddRegistrationListener(mockListener);

            // Access private readyStateListeners list via reflection to verify it was added
            var fieldInfo = typeof(CandiceAIController).GetField("readyStateListeners", BindingFlags.NonPublic | BindingFlags.Instance);
            var readyStateListeners = (List<Action<bool, int>>)fieldInfo.GetValue(_controller);

            // Assert
            Assert.That(readyStateListeners, Is.Not.Null, "The readyStateListeners list should not be null.");
            Assert.That(readyStateListeners.Count, Is.EqualTo(1), "The listener count should be 1.");
            Assert.That(readyStateListeners[0], Is.EqualTo(mockListener), "The added listener should match the mock listener.");
        }

        [Test]
        public void AttackMelee_WithAnimation_SetsIsAttackingAndDoesNotSchedule()
        {
            // Arrange
            _controller.HasAttackAnimation = true;
            _controller.IsAttacking = false;

            // Act
            _controller.AttackMelee();

            // Assert
            Assert.That(_controller.IsAttacking, Is.True, "IsAttacking should be set to true.");

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            bool pendingAttack = (bool)pendingAttackField.GetValue(_controller);
            Assert.That(pendingAttack, Is.False, "Pending attack should not be scheduled.");
        }

        [Test]
        public void AttackMelee_WithoutAnimation_SetsIsAttackingAndSchedulesPendingAttack()
        {
            // Arrange
            _controller.HasAttackAnimation = false;
            _controller.IsAttacking = false;

            // Act
            _controller.AttackMelee();

            // Assert
            Assert.That(_controller.IsAttacking, Is.True, "IsAttacking should be set to true.");

            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            bool pendingAttack = (bool)pendingAttackField.GetValue(_controller);
            Assert.That(pendingAttack, Is.True, "Pending attack should be scheduled.");

            var pendingAttackIsRangedField = typeof(CandiceAIController).GetField("_pendingAttackIsRanged", BindingFlags.NonPublic | BindingFlags.Instance);
            bool pendingAttackIsRanged = (bool)pendingAttackIsRangedField.GetValue(_controller);
            Assert.That(pendingAttackIsRanged, Is.False, "Scheduled pending attack should be melee, not ranged.");
        }

        [Test]
        public void AttackMelee_AlreadyAttacking_DoesNothing()
        {
            // Arrange
            _controller.HasAttackAnimation = false;
            _controller.IsAttacking = true;

            // Set pending attack to false to ensure it doesn't get changed to true
            var pendingAttackField = typeof(CandiceAIController).GetField("_pendingAttack", BindingFlags.NonPublic | BindingFlags.Instance);
            pendingAttackField.SetValue(_controller, false);

            // Act
            _controller.AttackMelee();

            // Assert
            Assert.That(_controller.IsAttacking, Is.True, "IsAttacking should remain true.");

            bool pendingAttack = (bool)pendingAttackField.GetValue(_controller);
            Assert.That(pendingAttack, Is.False, "Pending attack should not be scheduled if already attacking.");
        }
    }
}
