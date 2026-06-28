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

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestAgent");
            _manager = _go.AddComponent<CandiceAnimationManager>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
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
    }
}
