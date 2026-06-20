using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Hecton8.Items;

namespace Hecton8.Tests.Gameplay
{
    [TestFixture]
    public class StorageCrateEditTests
    {
        private GameObject _crateGo;
        private StorageCrate _storageCrate;

        [SetUp]
        public void SetUp()
        {
            _crateGo = new GameObject("TestStorageCrate");
            _storageCrate = _crateGo.AddComponent<StorageCrate>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_crateGo != null)
            {
                Object.DestroyImmediate(_crateGo);
            }
        }

        [Test]
        public void CountItemByHash_WithUnknownHashId_ReturnsZero()
        {
            // Arrange
            int unknownHashId = 123456789;

            // Act
            int count = _storageCrate.CountItemByHash(unknownHashId);

            // Assert
            Assert.AreEqual(0, count);
        }

        [Test]
        public void CountItemByHash_WithZeroHashId_ReturnsZero()
        {
            // Arrange
            int zeroHashId = 0;

            // Act
            int count = _storageCrate.CountItemByHash(zeroHashId);

            // Assert
            Assert.AreEqual(0, count);
        }

        [Test]
        public void CountItem_WithUnknownItem_ReturnsZero()
        {
            // Arrange
            ItemData unknownItem = ScriptableObject.CreateInstance<ItemData>();

            // Act
            int count = _storageCrate.CountItem(unknownItem);

            // Assert
            Assert.AreEqual(0, count);

            Object.DestroyImmediate(unknownItem);
        }

        [Test]
        public void CountItem_WithNullItem_ReturnsZero()
        {
            // Arrange
            ItemData nullItem = null;

            // Act
            int count = _storageCrate.CountItem(nullItem);

            // Assert
            Assert.AreEqual(0, count);
        }

        [Test]
        public void TryConsumeItem_WithUnknownItem_ReturnsFalse()
        {
            // Arrange
            ItemData unknownItem = ScriptableObject.CreateInstance<ItemData>();

            // Act
            bool result = _storageCrate.TryConsumeItem(unknownItem);

            // Assert
            Assert.IsFalse(result);

            Object.DestroyImmediate(unknownItem);
        }

        [Test]
        public void TryConsumeItem_WithNullItem_ReturnsFalse()
        {
            // Arrange
            ItemData nullItem = null;

            // Act
            bool result = _storageCrate.TryConsumeItem(nullItem);

            // Assert
            Assert.IsFalse(result);
        }
    }
}
