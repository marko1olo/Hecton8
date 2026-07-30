using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Unity.Collections;
using System.Reflection;

#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
namespace Hecton8.Tests
{
    public class PlayerInventoryCountTests
    {
        private GameObject _go;
        private PlayerInventory _inventory;
        private InventoryGrid _grid;
        private NativeArray<ushort> _stackCounts;
        private NativeArray<ushort> _craftLockedCounts;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestInventory");
            _inventory = _go.AddComponent<PlayerInventory>();

            _grid = new InventoryGrid(10, 10);
            typeof(PlayerInventory).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_inventory, _grid);

            _stackCounts = new NativeArray<ushort>(100, Allocator.Persistent);
            typeof(PlayerInventory).GetField("_stackCounts", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_inventory, _stackCounts);

            _craftLockedCounts = new NativeArray<ushort>(100, Allocator.Persistent);
            typeof(PlayerInventory).GetField("_craftLockedCounts", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_inventory, _craftLockedCounts);
        }

        [TearDown]
        public void Teardown()
        {
            if (_stackCounts.IsCreated) _stackCounts.Dispose();
            if (_craftLockedCounts.IsCreated) _craftLockedCounts.Dispose();
            if (_grid != null) _grid.Dispose(default);
            if (_go != null) Object.DestroyImmediate(_go);
        }

        [Test]
        public void CountTotal_ReturnsCorrectSum_WhenMultipleStacksExist()
        {
            int itemHashId = 12345;
            int otherItemHashId = 67890;

            var anchorHashIds = (NativeArray<int>)typeof(InventoryGrid).GetField("_anchorHashIds", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(_grid);

            anchorHashIds[0] = itemHashId;
            _stackCounts[0] = 5;

            anchorHashIds[2] = itemHashId;
            _stackCounts[2] = 10;

            anchorHashIds[5] = otherItemHashId;
            _stackCounts[5] = 100;

            anchorHashIds[7] = itemHashId;
            _stackCounts[7] = 0;

            // Bypass SOA fast-path logic for our test by leaving itemHashes uncreated

            int total = _inventory.CountTotal(itemHashId);
            Assert.AreEqual(15, total);

            int otherTotal = _inventory.CountTotal(otherItemHashId);
            Assert.AreEqual(100, otherTotal);
        }

        [Test]
        public void CountTotal_ReturnsZero_WhenItemHashIsZero()
        {
            int total = _inventory.CountTotal(0);
            Assert.AreEqual(0, total);
        }

        [Test]
        public void CountTotal_ReturnsZero_WhenGridIsNull()
        {
            typeof(PlayerInventory).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_inventory, null);
            int total = _inventory.CountTotal(12345);
            Assert.AreEqual(0, total);
        }

        [Test]
        public void CountTotal_ReturnsZero_WhenStackCountsNotCreated()
        {
            _stackCounts.Dispose();
            typeof(PlayerInventory).GetField("_stackCounts", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(_inventory, default(NativeArray<ushort>));

            int total = _inventory.CountTotal(12345);
            Assert.AreEqual(0, total);
        }
    }
}
#endif
