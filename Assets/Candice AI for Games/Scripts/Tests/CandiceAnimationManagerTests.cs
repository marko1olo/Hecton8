using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceAnimationManagerTests
    {
        private GameObject _managerObj;
        private CandiceAnimationManager _manager;
        private CollisionCapturer _capturer;

        // Helper MonoBehaviour to capture a valid Unity Collision object
        public class CollisionCapturer : MonoBehaviour
        {
            public Collision CapturedCollision;

            public void OnCollisionEnter(Collision col)
            {
                CapturedCollision = col;
            }
        }

        [SetUp]
        public void Setup()
        {
            _managerObj = new GameObject("AnimationManager");

            // To generate a collision, we need rigidbodies and colliders
            var rb = _managerObj.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var col = _managerObj.AddComponent<BoxCollider>();
            col.isTrigger = false;

            _capturer = _managerObj.AddComponent<CollisionCapturer>();
            _manager = _managerObj.AddComponent<CandiceAnimationManager>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_managerObj != null)
            {
                Object.DestroyImmediate(_managerObj);
            }
        }

        [UnityTest]
        public IEnumerator IveHitSomething_WhenHitByProjectile_SetsHitAndAttackDamageAndReturnsTrue()
        {
            // Arrange
            GameObject projectileObj = new GameObject("Projectile");
            try
            {
                projectileObj.tag = "Projectile";
            }
            catch (UnityException) {
                // In Edit mode tag may not exist, but these are PlayMode tests now so they should
                Assert.Inconclusive("Missing 'Projectile' tag in project settings, cannot run this test properly.");
            }

            var projRb = projectileObj.AddComponent<Rigidbody>();
            projRb.useGravity = false;
            projRb.isKinematic = false;
            var projCol = projectileObj.AddComponent<BoxCollider>();
            projCol.isTrigger = false;

            var candiceProj = projectileObj.AddComponent<CandiceProjectile>();
            candiceProj.attackDamage = 42f;

            _manager.hit = false;
            _manager.atkDamage = 0f;

            // Act - Intersect the physics bodies to trigger a collision
            projectileObj.transform.position = _managerObj.transform.position;

            // Yield to allow physics simulation to process the collision and trigger OnCollisionEnter
            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_capturer.CapturedCollision, Is.Not.Null, "Collision should have been captured. Physics simulation might not be running or bodies didn't collide.");

            // Directly invoke the method under test
            bool result = _manager.IveHitSomething(_capturer.CapturedCollision);

            // Assert
            Assert.That(result, Is.True, "Method should return true.");
            Assert.That(_manager.hit, Is.True, "hit should be true when hit by a projectile.");
            Assert.That(_manager.atkDamage, Is.EqualTo(42f), "atkDamage should be set to the projectile's attack damage.");

            Object.DestroyImmediate(projectileObj);
        }

        [UnityTest]
        public IEnumerator IveHitSomething_WhenHitByPlayer_SetsHitButNotAttackDamageAndReturnsTrue()
        {
            // Arrange
            GameObject playerObj = new GameObject("Player");
            try
            {
                playerObj.tag = "Player";
            }
            catch (UnityException) {
                Assert.Inconclusive("Missing 'Player' tag in project settings, cannot run this test properly.");
            }

            var playerRb = playerObj.AddComponent<Rigidbody>();
            playerRb.useGravity = false;
            playerRb.isKinematic = false;
            var playerCol = playerObj.AddComponent<BoxCollider>();
            playerCol.isTrigger = false;

            _manager.hit = false;
            _manager.atkDamage = 0f;

            // Act
            playerObj.transform.position = _managerObj.transform.position;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_capturer.CapturedCollision, Is.Not.Null, "Collision should have been captured. Physics simulation might not be running or bodies didn't collide.");

            bool result = _manager.IveHitSomething(_capturer.CapturedCollision);

            // Assert
            Assert.That(result, Is.True, "Method should return true.");
            Assert.That(_manager.hit, Is.True, "hit should be true when hit by a player.");
            Assert.That(_manager.atkDamage, Is.EqualTo(0f), "atkDamage should not be modified when hit by a player.");

            Object.DestroyImmediate(playerObj);
        }

        [UnityTest]
        public IEnumerator IveHitSomething_WhenHitByOther_DoesNotSetHitAndReturnsFalse()
        {
            // Arrange
            GameObject otherObj = new GameObject("Other");
            try
            {
                otherObj.tag = "Untagged";
            }
            catch (UnityException) {
                Assert.Inconclusive("Missing 'Untagged' tag in project settings, cannot run this test properly.");
            }

            var otherRb = otherObj.AddComponent<Rigidbody>();
            otherRb.useGravity = false;
            otherRb.isKinematic = false;
            var otherCol = otherObj.AddComponent<BoxCollider>();
            otherCol.isTrigger = false;

            _manager.hit = false;
            _manager.atkDamage = 0f;

            // Act
            otherObj.transform.position = _managerObj.transform.position;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_capturer.CapturedCollision, Is.Not.Null, "Collision should have been captured. Physics simulation might not be running or bodies didn't collide.");

            bool result = _manager.IveHitSomething(_capturer.CapturedCollision);

            // Assert
            Assert.That(result, Is.False, "Method should return false.");
            Assert.That(_manager.hit, Is.False, "hit should remain false when hit by uninteresting tag.");
            Assert.That(_manager.atkDamage, Is.EqualTo(0f), "atkDamage should remain unmodified.");

            Object.DestroyImmediate(otherObj);
        }
    }
}
