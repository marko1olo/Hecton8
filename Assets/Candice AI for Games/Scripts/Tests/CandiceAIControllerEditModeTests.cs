using System;
using System.Reflection;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.Tests
{
    public class CandiceAIControllerEditModeTests
    {
        private GameObject _gameObject;
        private CandiceAIController _controller;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject();
            _controller = _gameObject.AddComponent<CandiceAIController>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_gameObject);
        }

        [Test]
        public void AddRegistrationListener_AddsListenerToReadyStateListeners()
        {
            // Arrange
            bool listenerCalled = false;
            Action<bool, int> mockListener = (isRegistered, agentId) =>
            {
                listenerCalled = true;
            };

            // Act
            _controller.AddRegistrationListener(mockListener);

            // Access private readyStateListeners list via reflection to verify it was added
            var fieldInfo = typeof(CandiceAIController).GetField("readyStateListeners", BindingFlags.NonPublic | BindingFlags.Instance);
            var readyStateListeners = (List<Action<bool, int>>)fieldInfo.GetValue(_controller);

            // Assert
            Assert.That(readyStateListeners, Is.Not.Null, "The readyStateListeners list should not be null.");
            Assert.That(readyStateListeners.Count, Is.EqualTo(1), "The listener count should be 1.");
            Assert.That(readyStateListeners[0], Is.EqualTo(mockListener), "The added listener should match the mock listener.");
        }
    }
}
