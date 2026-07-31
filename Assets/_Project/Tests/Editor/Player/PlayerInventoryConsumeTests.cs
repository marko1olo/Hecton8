#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Inventory;
using Hecton8.Core;
using Unity.Collections;
using Hecton8.Gameplay;

namespace Hecton8.Tests
{
    public class PlayerInventoryConsumeTests
    {
        private PlayerInventory _inventory;
        private HectonSurvivalSystem _survival;
        private GameObject _go;
        private ItemCatalog _itemCatalog;
        private InventoryGrid _grid;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("Player");
            _inventory = _go.AddComponent<PlayerInventory>();
            _survival = _go.AddComponent<HectonSurvivalSystem>();

            var survivalField = typeof(PlayerInventory).GetField("survival", BindingFlags.NonPublic | BindingFlags.Instance);
            survivalField.SetValue(_inventory, _survival);

            _itemCatalog = ScriptableObject.CreateInstance<ItemCatalog>();
            var itemCatalogField = typeof(PlayerInventory).GetField("itemCatalog", BindingFlags.NonPublic | BindingFlags.Instance);
            itemCatalogField.SetValue(_inventory, _itemCatalog);

            _grid = new InventoryGrid(4, 4);
            var gridField = typeof(PlayerInventory).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance);
            gridField.SetValue(_inventory, _grid);

            var stackCountsField = typeof(PlayerInventory).GetField("_stackCounts", BindingFlags.NonPublic | BindingFlags.Instance);
            stackCountsField.SetValue(_inventory, new NativeArray<int>(16, Allocator.Persistent));
        }

        [TearDown]
        public void TearDown()
        {
            var stackCountsField = typeof(PlayerInventory).GetField("_stackCounts", BindingFlags.NonPublic | BindingFlags.Instance);
            var arr = (NativeArray<int>)stackCountsField.GetValue(_inventory);
            if (arr.IsCreated) arr.Dispose();

            if (_go != null)
                GameObject.DestroyImmediate(_go);

            if (_itemCatalog != null)
                ScriptableObject.DestroyImmediate(_itemCatalog);
        }

        [Test]
        public void ConsumeOneItem_ReturnsFalse_WhenGridIsNull()
        {
            var gridField = typeof(PlayerInventory).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance);
            gridField.SetValue(_inventory, null);

            bool result = _inventory.ConsumeOneItem(0, 0);
            Assert.IsFalse(result, "Should return false if grid is null.");
        }

        [Test]
        public void ConsumeOneItem_ReturnsFalse_WhenNoItemAtAnchor()
        {
            bool result = _inventory.ConsumeOneItem(0, 0);
            Assert.IsFalse(result, "Should return false if no item is at the specified anchor.");
        }

        [Test]
        public void ConsumeOneItem_ReturnsFalse_WhenItemIsNotConsumable()
        {
            int itemHash = 1234;
            var descriptor = new InventoryGrid.InventoryItemDescriptor(itemHash, 1, 1);
            _grid.PlaceAt(in descriptor, 0, 0);

            bool result = _inventory.ConsumeOneItem(0, 0);
            Assert.IsFalse(result, "Should return false if item is not consumable or not in catalog.");
        }

        [Test]
        public void ConsumeOneItem_ReturnsTrue_AndRemovesItem_WhenConsumable()
        {
            int itemHash = 5678;
            var descriptor = new InventoryGrid.InventoryItemDescriptor(itemHash, 1, 1);
            _grid.PlaceAt(in descriptor, 0, 0);

            var lookupField = typeof(ItemCatalog).GetField("_runtimeDescriptorLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            var lookup = new System.Collections.Generic.Dictionary<int, ItemCatalog.ItemRuntimeDescriptor>();

            var runtimeDescriptor = new ItemCatalog.ItemRuntimeDescriptor(
                itemHash,
                1,
                1,
                1,
                0,
                1.0f,
                0,
                0,
                0,
                0,
                1.0f,
                1.0f,
                0f,
                true,
                1, // isConsumable
                10f,
                10f,
                10f,
                10f,
                10f,
                1f
            );

            lookup.Add(itemHash, runtimeDescriptor);
            lookupField.SetValue(_itemCatalog, lookup);

            var stackCountsField = typeof(PlayerInventory).GetField("_stackCounts", BindingFlags.NonPublic | BindingFlags.Instance);
            var stackCounts = (NativeArray<int>)stackCountsField.GetValue(_inventory);
            stackCounts[0] = 1;
            stackCountsField.SetValue(_inventory, stackCounts);

            bool result = _inventory.ConsumeOneItem(0, 0);

            Assert.IsTrue(result, "Should return true for a consumable item.");

            bool hasItemAfter = _grid.TryGetAnchorDescriptor(0, out _);
            Assert.IsFalse(hasItemAfter, "Item should be removed from grid.");
            Assert.AreEqual(0, stackCounts[0], "Stack count should be zero after consuming single item.");
        }
    }
}
#endif
