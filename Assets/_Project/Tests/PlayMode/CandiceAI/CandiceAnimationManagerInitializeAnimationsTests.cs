using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;
using System.Collections;
using UnityEngine.TestTools;

namespace Hecton8.Tests.PlayMode.CandiceAI
{
    public class CandiceAnimationManagerInitializeAnimationsTests
    {
        private GameObject _testObject;
        private CandiceAnimationManager _manager;
        private Animator _animator;

        [SetUp]
        public void Setup()
        {
            _testObject = new GameObject("TestAgent");
            // Setup required components
            _testObject.AddComponent<Rigidbody>();
            _animator = _testObject.AddComponent<Animator>();

            // Need these to avoid null refs during InitializeAnimations
            _testObject.AddComponent<CandiceAIController>();
            _testObject.AddComponent<CandiceAIPlayerController>();

            _manager = _testObject.AddComponent<CandiceAnimationManager>();
            _manager.TemplateAnimator = _animator;
        }

        [UnityTearDown]
        public IEnumerator Teardown()
        {
            if (_testObject != null)
            {
                Object.Destroy(_testObject);
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator InitializeAnimations_CreatesNewModules_WhenNull()
        {
            // Ensure modules are null initially
            _manager.CandiceModuleAnimations = null;

            // Act
            _manager.InitializeAnimations();

            // Assert
            Assert.That(_manager.CandiceModuleAnimations, Is.Not.Null);
            Assert.That(_manager.CandiceModuleAnimations.TemplateAnimator, Is.EqualTo(_manager.TemplateAnimator));

            yield return null;
        }

        [UnityTest]
        public IEnumerator InitializeAnimations_PreservesExistingModules_WhenNotNull()
        {
            // Arrange
            var existingModule = new CandiceModuleAnimations();
            _manager.CandiceModuleAnimations = existingModule;

            // Act
            _manager.InitializeAnimations();

            // Assert
            Assert.That(_manager.CandiceModuleAnimations, Is.EqualTo(existingModule));

            yield return null;
        }
    }
}
