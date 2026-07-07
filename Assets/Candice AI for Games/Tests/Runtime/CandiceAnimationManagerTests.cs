using System;
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using CandiceAIforGames.AI;

namespace CandiceAIforGames.AI.Tests
{
    public class CandiceAnimationManagerTests
    {
        private GameObject _go;
        private CandiceAnimationManager _manager;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject();
            _manager = _go.AddComponent<CandiceAnimationManager>();
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(_go);
        }

        [Test]
        public void EvaluateInput_IsKey_Down_ThrowsArgumentExceptionForInvalidKey()
        {
            // Input.GetKeyDown throws ArgumentException for invalid key strings
            Assert.Throws<ArgumentException>(() => _manager.EvaluateInput("invalid_key_123", true, true, false));
        }

        [Test]
        public void EvaluateInput_IsKey_Up_ThrowsArgumentExceptionForInvalidKey()
        {
            // Input.GetKeyUp throws ArgumentException for invalid key strings
            Assert.Throws<ArgumentException>(() => _manager.EvaluateInput("invalid_key_123", true, false, true));
        }

        [Test]
        public void EvaluateInput_IsKey_Held_ThrowsArgumentExceptionForInvalidKey()
        {
            // Input.GetKey throws ArgumentException for invalid key strings
            Assert.Throws<ArgumentException>(() => _manager.EvaluateInput("invalid_key_123", true, false, false));
        }

        [Test]
        public void EvaluateInput_IsKey_Down_DoesNotThrowForValidKey()
        {
            // Valid key string
            Assert.DoesNotThrow(() => _manager.EvaluateInput("space", true, true, false));
        }

        [Test]
        public void EvaluateInput_IsKey_Up_DoesNotThrowForValidKey()
        {
            // Valid key string
            Assert.DoesNotThrow(() => _manager.EvaluateInput("space", true, false, true));
        }

        [Test]
        public void EvaluateInput_IsKey_Held_DoesNotThrowForValidKey()
        {
            // Valid key string
            Assert.DoesNotThrow(() => _manager.EvaluateInput("space", true, false, false));
        }

        [Test]
        public void EvaluateInput_IsButton_Down_ThrowsArgumentExceptionForInvalidButton()
        {
            // Input.GetButtonDown throws ArgumentException for undefined axis/button names
            Assert.Throws<UnityException>(() => _manager.EvaluateInput("invalid_button_123", false, true, false));
        }

        [Test]
        public void EvaluateInput_IsButton_Up_ThrowsArgumentExceptionForInvalidButton()
        {
            Assert.Throws<UnityException>(() => _manager.EvaluateInput("invalid_button_123", false, false, true));
        }

        [Test]
        public void EvaluateInput_IsButton_Held_ThrowsArgumentExceptionForInvalidButton()
        {
            Assert.Throws<UnityException>(() => _manager.EvaluateInput("invalid_button_123", false, false, false));
        }

        [Test]
        public void StandardInputCall_ThrowsUnityExceptionForInvalidButton()
        {
            Assert.Throws<UnityException>(() => _manager.StandardInputCall("invalid_button_123"));
        }

        [Test]
        public void StandardInputCall_DoesNotThrowForValidButton()
        {
            Assert.DoesNotThrow(() => _manager.StandardInputCall("Jump"));
        }

        [Test]
        public void StandardInputCall_ReturnsFalseWhenNotPressed()
        {
            // Assuming we aren't currently simulating an input press, this should be false.
            Assert.IsFalse(_manager.StandardInputCall("Jump"));
        }
    }
}
