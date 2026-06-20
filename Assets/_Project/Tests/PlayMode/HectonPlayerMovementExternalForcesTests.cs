using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Gameplay;

namespace Hecton8.Tests.PlayMode
{
    public class HectonPlayerMovementExternalForcesTests
    {
        private GameObject _playerObject;
        private HectonPlayerMovement _playerMovement;
        private Rigidbody _rigidbody;
        private HectonPlayerMotor _playerMotor;

        [SetUp]
        public void Setup()
        {
            _playerObject = new GameObject("PlayerTest");
            _rigidbody = _playerObject.AddComponent<Rigidbody>();
            _rigidbody.useGravity = false;

            // Setting up mass to a realistic value to ensure forces have a reasonable effect
            _rigidbody.mass = 80f;

            _playerMotor = _playerObject.AddComponent<HectonPlayerMotor>();
            _playerMovement = _playerObject.AddComponent<HectonPlayerMovement>();

            // AddComponent automatically calls Awake
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerObject != null)
            {
                Object.DestroyImmediate(_playerObject);
            }
        }

        [UnityTest]
        public IEnumerator QueueExternalAcceleration_AppliesVelocityToRigidbody()
        {
            yield return null; // Wait a frame for physics to stabilize if needed

            Vector3 startVelocity = _rigidbody.velocity;
            Vector3 testAcceleration = new Vector3(0, 10, 0);

            // Queue external acceleration
            _playerMovement.QueueExternalAcceleration(testAcceleration);

            // Wait a few fixed updates to let unity call FixedUpdate on the Monobehaviour.
            // HectonPlayerMovement updates kinematic forces here and pushes to Motor, which pushes to Rigidbody
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreNotEqual(startVelocity, _rigidbody.velocity, "Velocity should have changed after applying queued acceleration.");
        }

        [UnityTest]
        public IEnumerator QueueExternalVelocityChange_AppliesVelocityToRigidbody()
        {
            yield return null;

            Vector3 startVelocity = _rigidbody.velocity;
            Vector3 testVelocityChange = new Vector3(5, 0, 0);

            _playerMovement.QueueExternalVelocityChange(testVelocityChange);

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.AreNotEqual(startVelocity, _rigidbody.velocity, "Velocity should have changed after applying queued velocity change.");
        }
    }
}
