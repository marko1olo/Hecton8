using NUnit.Framework;
using UnityEngine;

namespace CandiceAIforGames.Tests
{
    public class CandiceDamageReceiverTests
    {
        private GameObject _gameObject;
        private CandiceDamageReceiver _candiceDamageReceiver;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("TestDamageReceiver");
            _candiceDamageReceiver = _gameObject.AddComponent<CandiceDamageReceiver>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void CandiceDamageReceiver_ComponentExists()
        {
            // Assert
            Assert.That(_candiceDamageReceiver, Is.Not.Null);
            Assert.That(_gameObject.GetComponent<CandiceDamageReceiver>(), Is.Not.Null);
        }
    }
}
