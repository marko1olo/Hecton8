using NUnit.Framework;
using UnityEngine;
using Unity.Mathematics;
using System.Reflection;
using Hecton8.Gameplay;

namespace Hecton8.Tests
{
    public class HectonPlayerMovementTests
    {
        private GameObject _playerGameObject;
        private HectonPlayerMovement _playerMovement;

        [SetUp]
        public void Setup()
        {
            _playerGameObject = new GameObject("PlayerTest");
            _playerGameObject.AddComponent<Rigidbody>();
            _playerMovement = _playerGameObject.AddComponent<HectonPlayerMovement>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_playerGameObject != null)
            {
                Object.DestroyImmediate(_playerGameObject);
            }
        }

        [Test]
        public void ApplyParasiteLatchInfluence_UpdatesInternalStateCorrectly()
        {
            // Arrange
            int expectedLatchedCount = 5;
            Vector3 expectedCenterOfMass = new Vector3(1, 2, 3);
            Vector3 expectedHarvesterPull = new Vector3(4, 5, 6);

            // Act
            _playerMovement.ApplyParasiteLatchInfluence(expectedLatchedCount, expectedCenterOfMass, expectedHarvesterPull);

            // Assert
            var latchedCountField = typeof(HectonPlayerMovement).GetField("_parasiteLatchedRequestedCount", BindingFlags.NonPublic | BindingFlags.Instance);
            var centerOfMassField = typeof(HectonPlayerMovement).GetField("_parasiteCenterOfMassRequestedLS", BindingFlags.NonPublic | BindingFlags.Instance);
            var harvesterPullField = typeof(HectonPlayerMovement).GetField("_parasiteHarvesterPullRequestedWS", BindingFlags.NonPublic | BindingFlags.Instance);
            var requestedThisStepField = typeof(HectonPlayerMovement).GetField("_parasiteLatchRequestedThisStep", BindingFlags.NonPublic | BindingFlags.Instance);
            var holdTimerField = typeof(HectonPlayerMovement).GetField("_parasiteLatchHoldTimer", BindingFlags.NonPublic | BindingFlags.Instance);
            var influenceHoldTimeField = typeof(HectonPlayerMovement).GetField("parasiteLatchInfluenceHoldTime", BindingFlags.NonPublic | BindingFlags.Instance);

            Assert.IsNotNull(latchedCountField, "Field _parasiteLatchedRequestedCount not found.");
            Assert.IsNotNull(centerOfMassField, "Field _parasiteCenterOfMassRequestedLS not found.");
            Assert.IsNotNull(harvesterPullField, "Field _parasiteHarvesterPullRequestedWS not found.");
            Assert.IsNotNull(requestedThisStepField, "Field _parasiteLatchRequestedThisStep not found.");
            Assert.IsNotNull(holdTimerField, "Field _parasiteLatchHoldTimer not found.");
            Assert.IsNotNull(influenceHoldTimeField, "Field parasiteLatchInfluenceHoldTime not found.");

            int actualLatchedCount = (int)latchedCountField.GetValue(_playerMovement);
            Vector3 actualCenterOfMass = (Vector3)centerOfMassField.GetValue(_playerMovement);
            Vector3 actualHarvesterPull = (Vector3)harvesterPullField.GetValue(_playerMovement);
            bool actualRequestedThisStep = (bool)requestedThisStepField.GetValue(_playerMovement);
            float actualHoldTimer = (float)holdTimerField.GetValue(_playerMovement);
            float actualInfluenceHoldTime = (float)influenceHoldTimeField.GetValue(_playerMovement);

            Assert.AreEqual(expectedLatchedCount, actualLatchedCount, "Latched count was not updated correctly.");
            Assert.AreEqual(expectedCenterOfMass, actualCenterOfMass, "Center of mass was not updated correctly.");
            Assert.AreEqual(expectedHarvesterPull, actualHarvesterPull, "Harvester pull was not updated correctly.");
            Assert.IsTrue(actualRequestedThisStep, "Requested this step flag was not set to true.");
            Assert.AreEqual(actualInfluenceHoldTime, actualHoldTimer, "Hold timer was not set to influence hold time.");
        }

        [Test]
        public void ApplyParasiteLatchInfluence_ClampsNegativeCountToZero()
        {
            // Arrange
            int negativeLatchedCount = -3;
            Vector3 dummyVector = Vector3.zero;

            // Act
            _playerMovement.ApplyParasiteLatchInfluence(negativeLatchedCount, dummyVector, dummyVector);

            // Assert
            var latchedCountField = typeof(HectonPlayerMovement).GetField("_parasiteLatchedRequestedCount", BindingFlags.NonPublic | BindingFlags.Instance);
            int actualLatchedCount = (int)latchedCountField.GetValue(_playerMovement);

            Assert.AreEqual(0, actualLatchedCount, "Negative latched count was not clamped to zero.");
        }

        [Test]
        public void ApplyParasiteLatchInfluence_UpdatesCountOnlyIfGreaterWhenAlreadyRequested()
        {
            // Arrange
            Vector3 dummyVector = Vector3.zero;

            // First call sets requested flag to true and count to 3
            _playerMovement.ApplyParasiteLatchInfluence(3, dummyVector, dummyVector);

            // Second call with smaller count
            _playerMovement.ApplyParasiteLatchInfluence(1, dummyVector, dummyVector);

            // Assert
            var latchedCountField = typeof(HectonPlayerMovement).GetField("_parasiteLatchedRequestedCount", BindingFlags.NonPublic | BindingFlags.Instance);
            int actualLatchedCount = (int)latchedCountField.GetValue(_playerMovement);

            // Expected to remain 3 because 1 is not greater than 3 and it was already requested
            Assert.AreEqual(3, actualLatchedCount, "Latched count should not decrease within the same step.");

            // Third call with greater count
            _playerMovement.ApplyParasiteLatchInfluence(5, dummyVector, dummyVector);

            actualLatchedCount = (int)latchedCountField.GetValue(_playerMovement);
            Assert.AreEqual(5, actualLatchedCount, "Latched count should update to higher value within the same step.");
        }
    }
}
