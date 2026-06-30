using NUnit.Framework;
using UnityEngine;
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
        public void InputManager2Call_AlwaysReturnsFalse()
        {
            // Act
            bool result = _manager.InputManager2Call("AnyInput");

            // Assert
            Assert.That(result, Is.False, "InputManager2Call should currently return false as it is a stub for upcoming input system support.");
        }
    }
}
