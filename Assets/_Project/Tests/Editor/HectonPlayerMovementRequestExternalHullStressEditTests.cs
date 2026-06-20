using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using Hecton8.Gameplay;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonPlayerMovementRequestExternalHullStressEditTests
    {
        private GameObject _playerGameObject;
        private HectonPlayerMovement _playerMovement;

        // Reflection fields
        private FieldInfo _requestedThisStepField;
        private FieldInfo _requestedIntensityField;

        [SetUp]
        public void Setup()
        {
            _playerGameObject = new GameObject("TestPlayer");
            _playerMovement = _playerGameObject.AddComponent<HectonPlayerMovement>();

            // Setup reflection
            var type = typeof(HectonPlayerMovement);
            _requestedThisStepField = type.GetField("_externalHullStressRequestedThisStep", BindingFlags.NonPublic | BindingFlags.Instance);
            _requestedIntensityField = type.GetField("_externalHullStressRequestedIntensity", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [TearDown]
        public void Teardown()
        {
            if (_playerGameObject != null)
            {
                Object.DestroyImmediate(_playerGameObject);
            }
        }

        private void SetRequestedThisStep(bool value)
        {
            _requestedThisStepField.SetValue(_playerMovement, value);
        }

        private bool GetRequestedThisStep()
        {
            return (bool)_requestedThisStepField.GetValue(_playerMovement);
        }

        private void SetRequestedIntensity(float value)
        {
            _requestedIntensityField.SetValue(_playerMovement, value);
        }

        private float GetRequestedIntensity()
        {
            return (float)_requestedIntensityField.GetValue(_playerMovement);
        }

        [Test]
        public void RequestExternalHullStress_WhenValueIsTooSmall_DoesNothing()
        {
            // Arrange
            SetRequestedThisStep(false);
            SetRequestedIntensity(0f);

            // Act
            _playerMovement.RequestExternalHullStress(0.0001f);
            _playerMovement.RequestExternalHullStress(-1f);

            // Assert
            Assert.IsFalse(GetRequestedThisStep());
            Assert.AreEqual(0f, GetRequestedIntensity());
        }

        [Test]
        public void RequestExternalHullStress_WhenValueIsValid_SetsFlagAndIntensity()
        {
            // Arrange
            SetRequestedThisStep(false);
            SetRequestedIntensity(0f);

            // Act
            _playerMovement.RequestExternalHullStress(0.5f);

            // Assert
            Assert.IsTrue(GetRequestedThisStep());
            Assert.AreEqual(0.5f, GetRequestedIntensity());
        }

        [Test]
        public void RequestExternalHullStress_WhenValueExceedsOne_ClampsToOne()
        {
            // Arrange
            SetRequestedThisStep(false);
            SetRequestedIntensity(0f);

            // Act
            _playerMovement.RequestExternalHullStress(1.5f);

            // Assert
            Assert.IsTrue(GetRequestedThisStep());
            Assert.AreEqual(1f, GetRequestedIntensity());
        }

        [Test]
        public void RequestExternalHullStress_WhenCalledMultipleTimes_KeepsHighestValue()
        {
            // Arrange
            SetRequestedThisStep(false);
            SetRequestedIntensity(0f);

            // Act
            _playerMovement.RequestExternalHullStress(0.3f);
            _playerMovement.RequestExternalHullStress(0.8f);
            _playerMovement.RequestExternalHullStress(0.5f);

            // Assert
            Assert.IsTrue(GetRequestedThisStep());
            Assert.AreEqual(0.8f, GetRequestedIntensity());
        }
    }
}
