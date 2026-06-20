using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;

namespace Hecton8.Tests.Editor
{
    public class HectonPlayerMovementEditTests
    {
        private GameObject _playerGo;
        private Rigidbody _rb;
        private HectonPlayerMovement _movement;

        [SetUp]
        public void Setup()
        {
            _playerGo = new GameObject("TestPlayer");
            _rb = _playerGo.AddComponent<Rigidbody>();
            _movement = _playerGo.AddComponent<HectonPlayerMovement>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_playerGo != null)
            {
                Object.DestroyImmediate(_playerGo);
            }
        }

        [Test]
        public void ApplyFaunaHypnosisPull_WithValidInput_BeginsWipeout()
        {
            // Arrange
            Vector3 sourcePosition = new Vector3(10f, 0f, 0f);
            float acceleration = 5f;
            float lockDuration = 2f;

            // Pre-condition
            Assert.IsFalse(_movement.IsInWipeoutState);

            // Act
            _movement.ApplyFaunaHypnosisPull(sourcePosition, acceleration, lockDuration);

            // Assert
            Assert.IsTrue(_movement.IsInWipeoutState);
        }

        [Test]
        public void ApplyFaunaHypnosisPull_WithoutRigidbody_DoesNothing()
        {
            // Arrange
            Object.DestroyImmediate(_rb); // Remove Rigidbody

            Vector3 sourcePosition = new Vector3(10f, 0f, 0f);
            float acceleration = 5f;
            float lockDuration = 2f;

            // Act
            _movement.ApplyFaunaHypnosisPull(sourcePosition, acceleration, lockDuration);

            // Assert
            Assert.IsFalse(_movement.IsInWipeoutState);
        }

        [Test]
        public void ApplyFaunaHypnosisPull_WithTinyAcceleration_DoesNotBeginWipeout()
        {
            // Arrange
            Vector3 sourcePosition = new Vector3(10f, 0f, 0f);
            float acceleration = 0.00005f; // Less than 0.0001f
            float lockDuration = 2f;

            // Act
            _movement.ApplyFaunaHypnosisPull(sourcePosition, acceleration, lockDuration);

            // Assert
            Assert.IsFalse(_movement.IsInWipeoutState);
        }

        [Test]
        public void ApplyFaunaHypnosisPull_WithZeroLockDuration_DoesNotBeginWipeout()
        {
            // Arrange
            Vector3 sourcePosition = new Vector3(10f, 0f, 0f);
            float acceleration = 5f;
            float lockDuration = 0f; // Less than or equal to 0.0001f

            // Act
            _movement.ApplyFaunaHypnosisPull(sourcePosition, acceleration, lockDuration);

            // Assert
            Assert.IsFalse(_movement.IsInWipeoutState);
        }
    }
}
