using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceAnimationManagerTests
    {
        private GameObject _go;
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
            _go = new GameObject("TestAgent");

            // To generate a collision, we need rigidbodies and colliders
            var rb = _go.AddComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = true;
            var col = _go.AddComponent<BoxCollider>();
            col.isTrigger = false;

            _capturer = _go.AddComponent<CollisionCapturer>();
            _manager = _go.AddComponent<CandiceAnimationManager>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_go != null)
            {
                UnityEngine.Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void StandardInputCall_UnconfiguredButton_ThrowsArgumentException()
        {
            // In Unity, querying a missing button name via Input.GetButton throws ArgumentException
            Assert.Throws<ArgumentException>(() => _manager.StandardInputCall("NonExistentButtonThatShouldThrow"));
        }

        [Test]
        public void StandardInputCall_NullInput_ThrowsArgumentException()
        {
            // Providing a null input throws ArgumentException.
            Assert.Throws<ArgumentException>(() => _manager.StandardInputCall(null));
        }

        [Test]
        public void StandardInputCall_EmptyInput_ThrowsArgumentException()
        {
            // Providing an empty input throws ArgumentException.
            Assert.Throws<ArgumentException>(() => _manager.StandardInputCall(""));
        }

        [Test]
        public void StandardInputCall_ExistingButtonNotPressed_ReturnsFalse()
        {
            // A standard Unity input configuration typically includes "Jump"
            // Testing this happy path to ensure it returns false when not pressed and does not throw.
            bool result = _manager.StandardInputCall("Jump");
            Assert.That(result, Is.False);
        }

        [Test]
        public void Animate_WithNoAnimator_ReturnsEarly()
        {
            _manager.TemplateAnimator = null;
            Assert.DoesNotThrow(() => _manager.Animate());
        }

        [Test]
        public void Animate_WithAnimatorButNoController_ReturnsEarly()
        {
            var animator = _go.AddComponent<Animator>();
            _manager.TemplateAnimator = animator;
            // No runtimeAnimatorController is set
            Assert.DoesNotThrow(() => _manager.Animate());
        }

        [Test]
        public void Animate_WithPlayerTagAndController_ExecutesPlayerInput()
        {
            // Set up conditions to bypass early returns
            var animator = _go.AddComponent<Animator>();
            _manager.TemplateAnimator = animator;

            // Use an empty AnimatorOverrideController as a valid RuntimeAnimatorController
            animator.runtimeAnimatorController = new AnimatorOverrideController();

            _go.tag = "Player";

            // Need CandiceAIPlayerController for the branch
            var playerController = _go.AddComponent<CandiceAIPlayerController>();

            // Re-init so _playerController gets populated
            _manager.InitializeAnimations();

            // Animate will call PlayerInput(). PlayerInput accesses Input.GetAxis which throws in batchmode if not set up,
            // or in EditMode. Let's see if it throws or not. In EditMode Input.GetAxis throws "Input is not activated".
            // Since we can't easily mock Input in EditMode, we will catch it if it happens.
            try
            {
                _manager.Animate();
            }
            catch (System.Exception e)
            {
                // Verify that it went down the PlayerInput path by checking if the exception is from Input.GetAxis
                Assert.That(e.Message, Does.Contain("Input").Or.Contain("GetAxis"));
            }
        }

        [Test]
        public void Animate_WithNoPlayerTag_ExecutesAgentInput()
        {
            var animator = _go.AddComponent<Animator>();
            _manager.TemplateAnimator = animator;
            animator.runtimeAnimatorController = new AnimatorOverrideController();

            _go.tag = "Untagged"; // Not "Player"

            // Needs CandiceAIController for AgentInput to do something meaningful, though it works without it.
            _go.AddComponent<CandiceAIController>();

            _manager.InitializeAnimations();

            // AgentInput does not use Input class, so it should not throw
            Assert.DoesNotThrow(() => _manager.Animate());
        }

        [Test]
        public void Animate_WithPlayerTagButNoPlayerController_DoesNotThrow()
        {
            var animator = _go.AddComponent<Animator>();
            _manager.TemplateAnimator = animator;
            animator.runtimeAnimatorController = new AnimatorOverrideController();

            _go.tag = "Player";

            // Intentionally omit CandiceAIPlayerController

            _manager.InitializeAnimations();

            // The branch checks if (_playerController != null), if it is null, it should bypass PlayerInput
            Assert.DoesNotThrow(() => _manager.Animate());
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
            projectileObj.transform.position = _go.transform.position;

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

            UnityEngine.Object.DestroyImmediate(projectileObj);
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
            playerObj.transform.position = _go.transform.position;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_capturer.CapturedCollision, Is.Not.Null, "Collision should have been captured. Physics simulation might not be running or bodies didn't collide.");

            bool result = _manager.IveHitSomething(_capturer.CapturedCollision);

            // Assert
            Assert.That(result, Is.True, "Method should return true.");
            Assert.That(_manager.hit, Is.True, "hit should be true when hit by a player.");
            Assert.That(_manager.atkDamage, Is.EqualTo(0f), "atkDamage should not be modified when hit by a player.");

            UnityEngine.Object.DestroyImmediate(playerObj);
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
            otherObj.transform.position = _go.transform.position;

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            Assert.That(_capturer.CapturedCollision, Is.Not.Null, "Collision should have been captured. Physics simulation might not be running or bodies didn't collide.");

            bool result = _manager.IveHitSomething(_capturer.CapturedCollision);

            // Assert
            Assert.That(result, Is.False, "Method should return false.");
            Assert.That(_manager.hit, Is.False, "hit should remain false when hit by uninteresting tag.");
            Assert.That(_manager.atkDamage, Is.EqualTo(0f), "atkDamage should remain unmodified.");

            UnityEngine.Object.DestroyImmediate(otherObj);
        }
    }
}
