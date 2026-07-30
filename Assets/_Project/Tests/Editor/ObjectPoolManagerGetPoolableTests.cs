using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using System.Reflection;

namespace Hecton8.Tests.Editor
{
    public sealed class ObjectPoolManagerGetPoolableTests
    {
        private GameObject _go;
        private ObjectPoolManager.PoolItemMarker _marker;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("TestGo");
            _marker = _go.AddComponent<ObjectPoolManager.PoolItemMarker>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
                Object.DestroyImmediate(_go);
        }

        private class MockPoolable : IPoolable
        {
            public void OnSpawn() { }
            public void OnDespawn() { }
        }

        [Test]
        public void GetPoolable_ValidIndex_ReturnsExpectedInstance()
        {
            // Arrange
            var expected1 = new MockPoolable();
            var expected2 = new MockPoolable();
            IPoolable[] mockArray = { expected1, expected2 };

            typeof(ObjectPoolManager.PoolItemMarker)
                .GetField("_poolables", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_marker, mockArray);

            // Act
            var result1 = _marker.GetPoolable(0);
            var result2 = _marker.GetPoolable(1);

            // Assert
            Assert.AreSame(expected1, result1, "Should return the correct instance at index 0.");
            Assert.AreSame(expected2, result2, "Should return the correct instance at index 1.");
        }

        [Test]
        public void GetPoolable_IndexOutOfRange_ThrowsIndexOutOfRangeException()
        {
            // Arrange
            IPoolable[] mockArray = { new MockPoolable() };

            typeof(ObjectPoolManager.PoolItemMarker)
                .GetField("_poolables", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(_marker, mockArray);

            // Act & Assert
            Assert.Throws<System.IndexOutOfRangeException>(() => _marker.GetPoolable(1), "Accessing index 1 on a 1-element array should throw.");
            Assert.Throws<System.IndexOutOfRangeException>(() => _marker.GetPoolable(-1), "Accessing negative index should throw.");
        }
    }
}
