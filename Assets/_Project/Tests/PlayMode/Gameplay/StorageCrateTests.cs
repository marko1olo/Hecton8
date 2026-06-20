using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Hecton8.Items;
using System.Reflection;

namespace Hecton8.Tests.PlayMode.Gameplay
{
    public class StorageCrateTests
    {
        private GameObject _crateGo;
        private StorageCrate _crate;
        private ItemData _testItem;
        private int _testItemHash;

        [SetUp]
        public void Setup()
        {
            _crateGo = new GameObject("TestCrate");
            var collider = _crateGo.AddComponent<BoxCollider>();
            _crate = _crateGo.AddComponent<StorageCrate>();

            _testItem = ScriptableObject.CreateInstance<ItemData>();
            _testItem.name = "TestItemName";

            // ItemData.stableId is private, and we need a non-empty string to avoid a hash of 0
            // but the testItem's name being set might be enough if stableId is empty.
            typeof(ItemData).GetField("stableId", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(_testItem, "TestItemName");

            // Initialize the persistent hash
            _testItemHash = ItemData.ResolvePersistentHashId(_testItem);
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_crateGo);
            Object.DestroyImmediate(_testItem);
        }

        [Test]
        public void TryConsumeItemByHash_WithValidHash_ConsumesItemAndReturnsTrue()
        {
            _crate.AddItem(_testItem);

            Assert.AreEqual(1, _crate.CountItemByHash(_testItemHash));

            bool result = _crate.TryConsumeItemByHash(_testItemHash);

            Assert.IsTrue(result);
            Assert.AreEqual(0, _crate.CountItemByHash(_testItemHash));
            Assert.IsTrue(_crate.IsEmpty());
        }

        [Test]
        public void TryConsumeItemByHash_WithInvalidHash_ReturnsFalseAndItemRemains()
        {
            _crate.AddItem(_testItem);

            int invalidHash = 12345;

            bool result = _crate.TryConsumeItemByHash(invalidHash);

            Assert.IsFalse(result);
            Assert.AreEqual(1, _crate.CountItemByHash(_testItemHash));
            Assert.IsFalse(_crate.IsEmpty());
        }

        [Test]
        public void TryConsumeItemByHash_WithEmptyCrate_ReturnsFalse()
        {
            Assert.IsTrue(_crate.IsEmpty());

            bool result = _crate.TryConsumeItemByHash(_testItemHash);

            Assert.IsFalse(result);
        }

        [Test]
        public void TryConsumeItemByHash_WithReservedSlot_ReturnsFalseAndItemRemains()
        {
            _crate.AddItem(_testItem);

            _crate.TryReserveItemByHash(_testItemHash, 1);

            bool result = _crate.TryConsumeItemByHash(_testItemHash);

            Assert.IsFalse(result);
            // CountItemByHash doesn't count reserved slots, but the item is technically still there and the crate isn't empty
            Assert.AreEqual(0, _crate.CountItemByHash(_testItemHash));
            Assert.IsFalse(_crate.IsEmpty());
        }
    }
}
