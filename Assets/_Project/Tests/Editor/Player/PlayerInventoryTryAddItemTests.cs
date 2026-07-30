#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.World;
using Unity.Collections;

namespace Hecton8.Tests.Player
{
    [TestFixture]
    public class PlayerInventoryTryAddItemTests
    {
        private PlayerInventory _inventory;
        private GameObject _gameObject;
        private ItemCatalog _itemCatalog;
        private ItemData _testItemData;

        [SetUp]
        public void SetUp()
        {
            _gameObject = new GameObject("PlayerInventoryTest");
            _inventory = _gameObject.AddComponent<PlayerInventory>();

            // Set up ItemCatalog
            _itemCatalog = ScriptableObject.CreateInstance<ItemCatalog>();

            // Set up test ItemData
            _testItemData = ScriptableObject.CreateInstance<ItemData>();
            _testItemData.name = "TestItem";

            // Force hash refresh on item data
            MethodInfo refreshHash = typeof(ItemData).GetMethod("RefreshPersistentHash", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(refreshHash, "Missing ItemData.RefreshPersistentHash method");
            refreshHash.Invoke(_testItemData, null);

            // Register item data with catalog so it resolves correctly
            FieldInfo hashLookupField = typeof(ItemCatalog).GetField("_hashLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(hashLookupField, "Missing ItemCatalog._hashLookup field");
            var dictionary = new System.Collections.Generic.Dictionary<int, ItemData>();
            dictionary.Add(_testItemData.PersistentHashId, _testItemData);
            hashLookupField.SetValue(_itemCatalog, dictionary);

            // Also need to register a runtime descriptor for it
            FieldInfo runtimeLookupField = typeof(ItemCatalog).GetField("_runtimeDescriptorLookup", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(runtimeLookupField, "Missing ItemCatalog._runtimeDescriptorLookup field");
            var dict = new System.Collections.Generic.Dictionary<int, ItemCatalog.ItemRuntimeDescriptor>();
            var desc = new ItemCatalog.ItemRuntimeDescriptor
            {
                HashId = _testItemData.PersistentHashId,
                Width = 1,
                Height = 1,
                MaxStack = 10,
                Weight = 1.0f,
                CategoryId = 1,
                Stackable = 1,
                StateFlags = 0
            };
            dict.Add(_testItemData.PersistentHashId, desc);
            runtimeLookupField.SetValue(_itemCatalog, dict);

            // Inject catalog into inventory
            FieldInfo catalogField = typeof(PlayerInventory).GetField("itemCatalog", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(catalogField, "Missing PlayerInventory.itemCatalog field");
            catalogField.SetValue(_inventory, _itemCatalog);

            // Manually initialize the grid and stack counts to mock a bound inventory
            FieldInfo gridField = typeof(PlayerInventory).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(gridField, "Missing PlayerInventory._grid field");
            object grid = Activator.CreateInstance(typeof(InventoryGrid), 4, 4);
            gridField.SetValue(_inventory, grid);

            FieldInfo stackCountsField = typeof(PlayerInventory).GetField("_stackCounts", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(stackCountsField, "Missing PlayerInventory._stackCounts field");
            stackCountsField.SetValue(_inventory, new NativeArray<ushort>(16, Allocator.Temp));

            FieldInfo scavengeField = typeof(PlayerInventory).GetField("_scavengeSimStackCounts", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(scavengeField, "Missing PlayerInventory._scavengeSimStackCounts field");
            scavengeField.SetValue(_inventory, new NativeArray<ushort>(16, Allocator.Temp));

            FieldInfo simOccupiedField = typeof(PlayerInventory).GetField("_simulationOccupiedCells", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(simOccupiedField, "Missing PlayerInventory._simulationOccupiedCells field");
            simOccupiedField.SetValue(_inventory, new NativeArray<byte>(16, Allocator.Temp));

            // Need _itemStateFlags, _itemGenetics, _qualityMilli, _durabilities, _lastUpdateUnixSeconds
            FieldInfo stateFlagsField = typeof(PlayerInventory).GetField("_itemStateFlags", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(stateFlagsField, "Missing PlayerInventory._itemStateFlags field");
            stateFlagsField.SetValue(_inventory, new NativeArray<ushort>(16, Allocator.Temp));

            FieldInfo geneticsField = typeof(PlayerInventory).GetField("_itemGenetics", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(geneticsField, "Missing PlayerInventory._itemGenetics field");
            geneticsField.SetValue(_inventory, new NativeArray<byte>(16, Allocator.Temp));

            FieldInfo qualityField = typeof(PlayerInventory).GetField("_qualityMilli", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(qualityField, "Missing PlayerInventory._qualityMilli field");
            qualityField.SetValue(_inventory, new NativeArray<ushort>(16, Allocator.Temp));

            FieldInfo durabilitiesField = typeof(PlayerInventory).GetField("_durabilities", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(durabilitiesField, "Missing PlayerInventory._durabilities field");
            durabilitiesField.SetValue(_inventory, new NativeArray<byte>(16, Allocator.Temp));

            FieldInfo lastUpdateField = typeof(PlayerInventory).GetField("_lastUpdateUnixSeconds", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(lastUpdateField, "Missing PlayerInventory._lastUpdateUnixSeconds field");
            lastUpdateField.SetValue(_inventory, new NativeArray<uint>(16, Allocator.Temp));

            // Also _sortBuffer which is ItemPlacement[]
            FieldInfo sortBufferField = typeof(PlayerInventory).GetField("_sortBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(sortBufferField, "Missing PlayerInventory._sortBuffer field");
            sortBufferField.SetValue(_inventory, new PlayerInventory.ItemPlacement[16]);

            // And _placementBuffer
            FieldInfo placementBufferField = typeof(PlayerInventory).GetField("_placementBuffer", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(placementBufferField, "Missing PlayerInventory._placementBuffer field");
            placementBufferField.SetValue(_inventory, new PlayerInventory.ItemPlacement[16]);
        }

        [TearDown]
        public void TearDown()
        {
            // Dispose all the NativeArrays we created
            var fields = typeof(PlayerInventory).GetFields(BindingFlags.NonPublic | BindingFlags.Instance);
            foreach (var field in fields)
            {
                if (field.FieldType.IsGenericType && field.FieldType.GetGenericTypeDefinition() == typeof(NativeArray<>))
                {
                    object val = field.GetValue(_inventory);
                    if (val != null)
                    {
                        var disposeMethod = val.GetType().GetMethod("Dispose", Type.EmptyTypes);
                        if (disposeMethod != null)
                        {
                            bool isCreated = (bool)val.GetType().GetProperty("IsCreated").GetValue(val);
                            if (isCreated)
                                disposeMethod.Invoke(val, null);
                        }
                    }
                }
            }

            if (_gameObject != null)
            {
                UnityEngine.Object.DestroyImmediate(_gameObject);
            }
            if (_testItemData != null)
            {
                UnityEngine.Object.DestroyImmediate(_testItemData);
            }
            if (_itemCatalog != null)
            {
                UnityEngine.Object.DestroyImmediate(_itemCatalog);
            }
        }

        [Test]
        public void TryAddItem_ValidItemWithSpace_ReturnsTrue()
        {
            // Ensure CanServiceItemAdds() returns true by enabling the component
            _inventory.enabled = true;

            // Try to add the item
            int hashId = _testItemData.PersistentHashId;
            bool result = _inventory.TryAddItem(hashId, 1);

            // Assert it returns true since inventory is empty and has space
            Assert.IsTrue(result, "TryAddItem should return true when there is space in the inventory for the item.");
        }

        [Test]
        public void TryAddItem_InvalidItemHash_ReturnsFalse()
        {
            _inventory.enabled = true;
            bool result = _inventory.TryAddItem(0, 1);
            Assert.IsFalse(result, "TryAddItem should return false for invalid item hash ID (0).");
        }

        [Test]
        public void TryAddItem_NegativeQuantity_ReturnsFalse()
        {
            _inventory.enabled = true;
            int hashId = _testItemData.PersistentHashId;
            bool result = _inventory.TryAddItem(hashId, -1);
            Assert.IsFalse(result, "TryAddItem should return false for negative quantity.");
        }

        [Test]
        public void TryAddItem_UninitializedInventory_ReturnsFalse()
        {
            // Manually set itemCatalog to null to simulate uninitialized inventory
            FieldInfo catalogField = typeof(PlayerInventory).GetField("itemCatalog", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(catalogField, "Missing PlayerInventory.itemCatalog field");
            catalogField.SetValue(_inventory, null);

            _inventory.enabled = true;
            int hashId = _testItemData.PersistentHashId;
            bool result = _inventory.TryAddItem(hashId, 1);

            Assert.IsFalse(result, "TryAddItem should return false if inventory is not fully initialized (missing itemCatalog).");
        }
    }
}
#endif
