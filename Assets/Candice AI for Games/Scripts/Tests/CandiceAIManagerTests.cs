using System;
using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceAIManagerTests
    {
        private GameObject _gameObject;
        private CandiceAIManager _manager;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject();
            _manager = _gameObject.AddComponent<CandiceAIManager>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void CharacterDead_InvokesOnCharacterDeadEvent()
        {
            // Arrange
            GameObject testAgent = new GameObject();
            GameObject deadCharacter = null;
            bool eventInvoked = false;

            _manager.OnCharacterDead += (go) =>
            {
                eventInvoked = true;
                deadCharacter = go;
            };

            // Act
            _manager.CharacterDead(testAgent);

            // Assert
            Assert.That(eventInvoked, Is.True, "The OnCharacterDead event should be invoked.");
            Assert.That(deadCharacter, Is.EqualTo(testAgent), "The event should pass the correct GameObject.");

            // Cleanup
            UnityEngine.Object.DestroyImmediate(testAgent);
        }
    }
}
