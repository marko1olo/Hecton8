#if UNITY_EDITOR && HECTON8_ENABLE_PLAYMODE_TESTS
using System.Collections;
using Hecton8.World;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.World
{
    public class WorldProceduralStateRegistryTests
    {
        private GameObject _gameObject;
        private WorldProceduralStateRegistry _registry;

        [SetUp]
        public void Setup()
        {
            _gameObject = new GameObject("WorldProceduralStateRegistry");
            _registry = _gameObject.AddComponent<WorldProceduralStateRegistry>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_gameObject != null)
            {
                Object.DestroyImmediate(_gameObject);
            }
        }

        [Test]
        public void IsPlacementSuppressed_WhenRuntimeKeyIsZero_ReturnsFalse()
        {
            bool isSuppressed = _registry.IsPlacementSuppressed(0L);
            Assert.IsFalse(isSuppressed);
        }

        [Test]
        public void IsPlacementSuppressed_WhenKeyIsNotSuppressed_ReturnsFalse()
        {
            _registry.SuppressPlacement(12345L);
            bool isSuppressed = _registry.IsPlacementSuppressed(54321L);
            Assert.IsFalse(isSuppressed);
        }

        [Test]
        public void IsPlacementSuppressed_WhenKeyIsSuppressed_ReturnsTrue()
        {
            _registry.SuppressPlacement(12345L);
            bool isSuppressed = _registry.IsPlacementSuppressed(12345L);
            Assert.IsTrue(isSuppressed);
        }
    }
}
#endif
