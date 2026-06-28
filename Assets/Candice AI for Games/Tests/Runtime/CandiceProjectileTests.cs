using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceProjectileTests
    {
        private GameObject _projectileObject;
        private CandiceProjectile _projectile;

        [SetUp]
        public void SetUp()
        {
            _projectileObject = new GameObject("TestProjectile");

            // Add required components
            _projectileObject.AddComponent<Rigidbody>();

            _projectile = _projectileObject.AddComponent<CandiceProjectile>();

            // OnEnable needs to happen or manually register
            _projectileObject.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            if (_projectileObject != null)
            {
                Object.DestroyImmediate(_projectileObject);
            }
        }

        [UnityTest]
        public IEnumerator ScheduleDeactivate_PositiveDelay_DeactivatesAfterDelay()
        {
            // Arrange
            float delay = 0.5f;
            _projectileObject.SetActive(true);

            // Act
            _projectile.ScheduleDeactivate(delay);

            // Assert: Should remain active initially
            Assert.That(_projectileObject.activeSelf, Is.True);

            // Wait less than the delay, should still be active
            yield return new WaitForSeconds(delay * 0.5f);
            Assert.That(_projectileObject.activeSelf, Is.True);

            // Wait for the remaining delay plus a tiny buffer for the frame check
            yield return new WaitForSeconds((delay * 0.5f) + 0.1f);

            // Assert: Should be deactivated
            Assert.That(_projectileObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ScheduleDeactivate_NegativeDelay_DeactivatesNextFrame()
        {
            // Arrange
            float delay = -1f;
            _projectileObject.SetActive(true);

            // Act
            _projectile.ScheduleDeactivate(delay);

            // Assert: Should remain active this frame
            Assert.That(_projectileObject.activeSelf, Is.True);

            // Wait one frame
            yield return null;

            // Assert: Should be deactivated immediately because Mathf.Max(0f, -1f) resolves to 0 delay
            Assert.That(_projectileObject.activeSelf, Is.False);
        }

        [UnityTest]
        public IEnumerator ScheduleDeactivate_ZeroDelay_DeactivatesNextFrame()
        {
            // Arrange
            float delay = 0f;
            _projectileObject.SetActive(true);

            // Act
            _projectile.ScheduleDeactivate(delay);

            // Assert: Should remain active this frame
            Assert.That(_projectileObject.activeSelf, Is.True);

            // Wait one frame
            yield return null;

            // Assert: Should be deactivated immediately
            Assert.That(_projectileObject.activeSelf, Is.False);
        }
    }
}
