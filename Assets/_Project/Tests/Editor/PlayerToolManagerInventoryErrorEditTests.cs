#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Items;
using Hecton8.Tools;

namespace Hecton8.Tests.Editor
{
    public class PlayerToolManagerInventoryErrorEditTests
    {
        private PlayerToolManager toolManager;
        private PlayerInventory playerInventory;
        private GameObject go;
        private FieldInfo suppressHandlingField;

        [SetUp]
        public void Setup()
        {
            go = new GameObject("Tester");
            toolManager = go.AddComponent<PlayerToolManager>();

            playerInventory = go.AddComponent<PlayerInventory>();

            // Set the private field playerInventory
            var field = typeof(PlayerToolManager).GetField("playerInventory", BindingFlags.NonPublic | BindingFlags.Instance);
            field.SetValue(toolManager, playerInventory);

            suppressHandlingField = typeof(PlayerToolManager).GetField("_suppressInventoryChangedHandling", BindingFlags.NonPublic | BindingFlags.Instance);
        }

        [TearDown]
        public void TearDown()
        {
            if (go != null)
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void TryForceDropCurrentToolFromHands_WhenInventoryThrows_RestoresSuppressFlag()
        {
            // Set up a current tool that will pass the initial checks
            var toolGo = new GameObject("MockTool");
            var mockTool = toolGo.AddComponent<MockPlayerTool>();
            mockTool.testToolData = ScriptableObject.CreateInstance<ItemData>();
            // Force ResolvePersistentHashId to return non-zero
            var hashField = typeof(ItemData).GetField("_persistentHashId", BindingFlags.NonPublic | BindingFlags.Instance);
            hashField.SetValue(mockTool.testToolData, 1234);

            var currentToolField = typeof(PlayerToolManager).GetField("_currentTool", BindingFlags.NonPublic | BindingFlags.Instance);
            currentToolField.SetValue(toolManager, mockTool);

            var worldRegistryField = typeof(PlayerToolManager).GetField("_persistentWorldRegistry", BindingFlags.NonPublic | BindingFlags.Instance);
            // Assign a mock or dummy registry
            worldRegistryField.SetValue(toolManager, new DummyRegistry());

            // Corrupt the grid to throw an exception
            var gridField = typeof(PlayerInventory).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance);
            var grid = (InventoryGrid)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(InventoryGrid));
            gridField.SetValue(playerInventory, grid);

            var stackCountsField = typeof(PlayerInventory).GetField("_stackCounts", BindingFlags.NonPublic | BindingFlags.Instance);
            var stacks = new Unity.Collections.NativeArray<ushort>(1, Unity.Collections.Allocator.Persistent);
            stackCountsField.SetValue(playerInventory, stacks);

            try
            {
                Assert.Throws<NullReferenceException>(() => toolManager.TryForceDropCurrentToolFromHands(Vector3.zero));

                bool suppressFlag = (bool)suppressHandlingField.GetValue(toolManager);
                Assert.IsFalse(suppressFlag, "_suppressInventoryChangedHandling was not restored to false after an exception in TryForceDropCurrentToolFromHands");
            }
            finally
            {
                stacks.Dispose();
            }
        }

        [Test]
        public void ConsumeBrokenToolInventoryEntry_WhenInventoryThrows_RestoresSuppressFlag()
        {
            // ConsumeBrokenToolInventoryEntry is private and takes an int. We can invoke it via reflection.
            var consumeMethod = typeof(PlayerToolManager).GetMethod("ConsumeBrokenToolInventoryEntry", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(consumeMethod, "ConsumeBrokenToolInventoryEntry method not found");

            // Corrupt the grid to throw an exception
            var gridField = typeof(PlayerInventory).GetField("_grid", BindingFlags.NonPublic | BindingFlags.Instance);
            var grid = (InventoryGrid)System.Runtime.Serialization.FormatterServices.GetUninitializedObject(typeof(InventoryGrid));
            gridField.SetValue(playerInventory, grid);

            var stackCountsField = typeof(PlayerInventory).GetField("_stackCounts", BindingFlags.NonPublic | BindingFlags.Instance);
            var stacks = new Unity.Collections.NativeArray<ushort>(1, Unity.Collections.Allocator.Persistent);
            stackCountsField.SetValue(playerInventory, stacks);

            try
            {
                var ex = Assert.Throws<TargetInvocationException>(() => consumeMethod.Invoke(toolManager, new object[] { 1234 }));
                Assert.That(ex.InnerException, Is.InstanceOf<NullReferenceException>());

                bool suppressFlag = (bool)suppressHandlingField.GetValue(toolManager);
                Assert.IsFalse(suppressFlag, "_suppressInventoryChangedHandling was not restored to false after an exception in ConsumeBrokenToolInventoryEntry");
            }
            finally
            {
                stacks.Dispose();
            }
        }

        private class MockPlayerTool : PlayerTool
        {
            public ItemData testToolData;
            public override ItemData ToolData => testToolData;
            public override string GetToolInternalId() => "MockTool";
            public override string GetToolDisplayName() => "MockTool";
            public override void UsePrimary(float deltaTime) {}
            public override void UseSecondary(float deltaTime) {}
            public override void OnHotSwapRefSync(Hecton8.Core.GlobalRegistry.HotSwapRefEvent evt) {}
            public override void OnHotSwap(Hecton8.Core.GlobalRegistry.HotSwapEvent evt) {}
            public override void OnSpawn() {}
            public override void OnDespawn() {}
        }

        private class DummyRegistry : IPersistentDroppedItemRegistry
        {
            public bool CanRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition) => true;
            public bool CanRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, int quantity, Vector3 runtimePosition) => true;
            public bool TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition) => true;
            public bool TryRegisterDroppedItem(ItemData itemData, int quantity, Vector3 runtimePosition, Vector3 initialImpulse) => true;
            public bool TryRegisterDroppedItem(int itemHashId, ItemCatalog itemCatalog, ushort stateFlags, ushort qualityMilli, ulong geneticsMask, int quantity, Vector3 runtimePosition, Vector3 initialImpulse) => true;
            public bool TryRegisterDroppedItemWithCondition(ItemData itemData, float normalizedCondition, Vector3 runtimePosition, Vector3 initialImpulse) => true;
            public bool TryRegisterDroppedItemWithPhysics(ItemData itemData, int quantity, Vector3 runtimePosition, Vector3 initialImpulse, Vector3 initialAngularVelocity) => true;
            public bool TryRegisterBulkDroppedItems(ItemData itemData, int quantity, Vector3 runtimePosition, float scatterRadius) => true;
            public bool TryRegisterBulkDroppedItems(ReadOnlySpan<int> itemHashIds, ReadOnlySpan<int> quantities, ItemCatalog itemCatalog, Vector3 runtimePosition, float scatterRadius, float velocityMultiplier) => true;
            public bool TrySpawnDroppedItemLocal(ItemData itemData, int quantity, Vector3 runtimePosition, out GameObject spawnedItem) { spawnedItem = null; return true; }
            public bool TrySpawnDroppedItemWithStateLocal(int itemHashId, ItemCatalog itemCatalog, ushort stateFlags, ushort qualityMilli, ulong geneticsMask, int quantity, Vector3 runtimePosition, out GameObject spawnedItem) { spawnedItem = null; return true; }
            public void MarkForRuntimeInitialization(GameObject itemInstance) {}
            public void UnregisterDroppedItem(GameObject item) {}
            public void OnSystemInitialize() {}
            public void OnSystemShutdown() {}
            public void ProvideSystemStatus(SystemStatusReport report) {}
        }
    }
}
#endif
