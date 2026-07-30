using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.World;

namespace Hecton8.Tests.PlayMode
{
    public class WorldProceduralStateRegistryTests
    {
        private GameObject _go;
        private WorldProceduralStateRegistry _registry;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("WorldProceduralStateRegistry");
            _registry = _go.AddComponent<WorldProceduralStateRegistry>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [UnityTest]
        public IEnumerator RestoreFaunaAnchor_ZeroRuntimeKey_ReturnsFalse()
        {
            yield return null;

            bool result = _registry.RestoreFaunaAnchor(0L);
            Assert.IsFalse(result);
        }

        [UnityTest]
        public IEnumerator RestoreFaunaAnchor_KeyDoesNotExist_ReturnsFalse()
        {
            yield return null;

            bool result = _registry.RestoreFaunaAnchor(12345L);
            Assert.IsFalse(result);
        }

        [UnityTest]
        public IEnumerator RestoreFaunaAnchor_KeyExists_RemovesKeyAndReturnsTrue()
        {
            yield return null;

            _registry.BlockFaunaAnchor(12345L, true);

            bool result = _registry.RestoreFaunaAnchor(12345L);
            Assert.IsTrue(result);

            bool result2 = _registry.RestoreFaunaAnchor(12345L);
            Assert.IsFalse(result2);
        }
    }
}
