using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;

namespace Hecton8.Tests.PlayMode
{
    public class HectonPlayerMovementThermalUpdraftTests
    {
        private GameObject _playerGameObject;
        private HectonPlayerMovement _playerMovement;
        private FieldInfo _externalThermalUpdraftVelocityChangeField;
        private FieldInfo _externalThermalUpdraftRequestedThisStepField;

        [SetUp]
        public void Setup()
        {
            _playerGameObject = new GameObject("PlayerTest");
            _playerMovement = _playerGameObject.AddComponent<HectonPlayerMovement>();

            _externalThermalUpdraftVelocityChangeField = typeof(HectonPlayerMovement).GetField("_externalThermalUpdraftVelocityChange", BindingFlags.NonPublic | BindingFlags.Instance);
            _externalThermalUpdraftRequestedThisStepField = typeof(HectonPlayerMovement).GetField("_externalThermalUpdraftRequestedThisStep", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [TearDown]
        public void Teardown()
        {
            if (_playerGameObject != null)
            {
                Object.DestroyImmediate(_playerGameObject);
            }
        }

        [Test]
        public void ApplyExternalThermalUpdraft_InvalidVectors_AreIgnored()
        {
            // Initial valid vector
            _playerMovement.ApplyExternalThermalUpdraft(new Vector3(0, 1, 0));
            var velocityBefore = (Vector3)_externalThermalUpdraftVelocityChangeField.GetValue(_playerMovement);

            // NaN
            _playerMovement.ApplyExternalThermalUpdraft(new Vector3(float.NaN, 2f, 0));
            Assert.AreEqual(velocityBefore, (Vector3)_externalThermalUpdraftVelocityChangeField.GetValue(_playerMovement));

            // Infinity
            _playerMovement.ApplyExternalThermalUpdraft(new Vector3(0, float.PositiveInfinity, 0));
            Assert.AreEqual(velocityBefore, (Vector3)_externalThermalUpdraftVelocityChangeField.GetValue(_playerMovement));

            // Negative Y
            _playerMovement.ApplyExternalThermalUpdraft(new Vector3(0, -1f, 0));
            Assert.AreEqual(velocityBefore, (Vector3)_externalThermalUpdraftVelocityChangeField.GetValue(_playerMovement));

            // Near zero Y
            _playerMovement.ApplyExternalThermalUpdraft(new Vector3(0, 0.00005f, 0));
            Assert.AreEqual(velocityBefore, (Vector3)_externalThermalUpdraftVelocityChangeField.GetValue(_playerMovement));
        }

        [Test]
        public void ApplyExternalThermalUpdraft_ValidVectors_UpdatesCorrectly()
        {
            // Initial valid vector
            _playerMovement.ApplyExternalThermalUpdraft(new Vector3(0, 1, 0));
            var requested = (bool)_externalThermalUpdraftRequestedThisStepField.GetValue(_playerMovement);
            var velocity = (Vector3)_externalThermalUpdraftVelocityChangeField.GetValue(_playerMovement);

            Assert.IsTrue(requested);
            Assert.AreEqual(new Vector3(0, 1, 0), velocity);

            // Larger magnitude vector should overwrite
            _playerMovement.ApplyExternalThermalUpdraft(new Vector3(0, 2, 0));
            velocity = (Vector3)_externalThermalUpdraftVelocityChangeField.GetValue(_playerMovement);
            Assert.AreEqual(new Vector3(0, 2, 0), velocity);

            // Smaller magnitude vector should NOT overwrite
            _playerMovement.ApplyExternalThermalUpdraft(new Vector3(0, 0.5f, 0));
            velocity = (Vector3)_externalThermalUpdraftVelocityChangeField.GetValue(_playerMovement);
            Assert.AreEqual(new Vector3(0, 2, 0), velocity);
        }
    }
}
