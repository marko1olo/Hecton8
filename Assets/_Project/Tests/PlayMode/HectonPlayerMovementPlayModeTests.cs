using System.Collections;
using System.Reflection;
using Hecton8.Core;
using Hecton8.Gameplay;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.PlayMode
{
    public class HectonPlayerMovementPlayModeTests
    {
        private GameObject _playerObj;

        [TearDown]
        public void TearDown()
        {
            if (_playerObj != null)
            {
                Object.DestroyImmediate(_playerObj);
                _playerObj = null;
            }
        }

        private class MockTransportOwner : MonoBehaviour, IPlayerTransportLifecycleOwner
        {
            public bool IsTransportBroken => false;
            public float TransportIntegrityNormalized => 1f;
            public float TransportChargeNormalized => 1f;
            public bool CanReceiveTransportCharge => false;

            public float LastImpactSpeed { get; private set; }
            public Vector3 LastHitPoint { get; private set; }
            public Vector3 LastHitNormal { get; private set; }

            public void RechargeTransport(float normalizedChargeDelta) { }
            public void ApplyTransportCollisionImpact(float impactSpeed, Vector3 hitPoint, Vector3 hitNormal)
            {
                LastImpactSpeed = impactSpeed;
                LastHitPoint = hitPoint;
                LastHitNormal = hitNormal;
            }
        }

        private class MockTransportCoordinator : MonoBehaviour, IPlayerTransportLifecycleResolver
        {
            public MockTransportOwner TransportOwner;

            public bool IsTransportActive() => TransportOwner != null;

            public bool TryResolveTransportLifecycleOwner(out IPlayerTransportLifecycleOwner lifecycleOwner)
            {
                lifecycleOwner = TransportOwner;
                return TransportOwner != null;
            }
        }

        [UnityTest]
        public IEnumerator ForceTransportBailout_TriggersWipeoutState_AndDetachesTransport()
        {
            _playerObj = new GameObject("PlayerMovement");
            var movement = _playerObj.AddComponent<HectonPlayerMovement>();

            // Add required coordinator dependency
            var mockCoordinator = _playerObj.AddComponent<MockTransportCoordinator>();
            var mockOwner = _playerObj.AddComponent<MockTransportOwner>();
            mockCoordinator.TransportOwner = mockOwner;

            // Use reflection to set the coordinator
            FieldInfo coordinatorField = typeof(HectonPlayerMovement).GetField("_playerTransportCoordinator", BindingFlags.NonPublic | BindingFlags.Instance);
            if (coordinatorField != null)
            {
                coordinatorField.SetValue(movement, mockCoordinator);
            }

            yield return null;

            Assert.IsFalse(movement.IsInWipeoutState, "Player should not start in wipeout state.");
            Assert.AreEqual(0f, mockOwner.LastImpactSpeed, "Transport should not have collision impact applied yet.");

            // Force bailout
            Vector3 worldImpulse = Vector3.up * 10f;
            float severity = 0.8f;
            movement.ForceTransportBailout(worldImpulse, severity);

            // Assert Wipeout
            Assert.IsTrue(movement.IsInWipeoutState, "ForceTransportBailout should trigger wipeout state.");

            // Assert Transport is detached / wiped out by checking if collision impact was sent to the mocked owner
            Assert.IsTrue(mockOwner.LastImpactSpeed > 0f, "ForceTransportBailout should detach transport and apply wipeout impact logic.");
        }
    }
}
