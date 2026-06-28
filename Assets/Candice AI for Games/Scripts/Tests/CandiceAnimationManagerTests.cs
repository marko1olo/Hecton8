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
        private CandiceAnimationManager _animationManager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestAnimationManager");
            _animationManager = _go.AddComponent<CandiceAnimationManager>();
        }

        [TearDown]
        public void TearDown()
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
            Assert.Throws<ArgumentException>(() => _animationManager.StandardInputCall("NonExistentButtonThatShouldThrow"));
        }

        [Test]
        public void StandardInputCall_NullInput_ThrowsArgumentException()
        {
            // Providing a null input throws ArgumentException.
            Assert.Throws<ArgumentException>(() => _animationManager.StandardInputCall(null));
        }

        [Test]
        public void StandardInputCall_EmptyInput_ThrowsArgumentException()
        {
            // Providing an empty input throws ArgumentException.
            Assert.Throws<ArgumentException>(() => _animationManager.StandardInputCall(""));
        }

        [Test]
        public void StandardInputCall_ExistingButtonNotPressed_ReturnsFalse()
        {
            // A standard Unity input configuration typically includes "Jump"
            // Testing this happy path to ensure it returns false when not pressed and does not throw.
            bool result = _animationManager.StandardInputCall("Jump");
            Assert.That(result, Is.False);
        }
    }
}
