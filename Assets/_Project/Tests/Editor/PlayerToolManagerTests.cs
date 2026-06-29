#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Hecton8.Inventory;

namespace Hecton8.Tests.Editor
{
    [TestFixture]
    public class PlayerToolManagerTests
    {
        private class MockThrowingPlayerInventory : PlayerInventory
        {
            public bool shouldThrow = false;

            internal override bool TryRemoveFirstMatchingItemByHash(int itemHashId)
            {
                if (shouldThrow)
                {
                    throw new InvalidOperationException("Mock Exception");
                }
                return base.TryRemoveFirstMatchingItemByHash(itemHashId);
            }
        }

        [Test]
        public void ConsumeBrokenToolInventoryEntry_WhenRemoveThrowsException_RestoresSuppressFlag()
        {
            // Arrange
            var go = new GameObject("Tester");
            var manager = go.AddComponent<PlayerToolManager>();
            var inventoryGo = new GameObject("Inventory");
            var inventory = inventoryGo.AddComponent<MockThrowingPlayerInventory>();
            inventory.shouldThrow = true;

            var playerInventoryField = typeof(PlayerToolManager).GetField("playerInventory", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            if (playerInventoryField != null)
            {
                playerInventoryField.SetValue(manager, inventory);
            }

            var suppressField = typeof(PlayerToolManager).GetField("_suppressInventoryChangedHandling", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(suppressField, "Field _suppressInventoryChangedHandling not found");

            var consumeMethod = typeof(PlayerToolManager).GetMethod("ConsumeBrokenToolInventoryEntry", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(consumeMethod, "Method ConsumeBrokenToolInventoryEntry not found");

            // Act & Assert
            try
            {
                consumeMethod.Invoke(manager, new object[] { 12345 });
            }
            catch (TargetInvocationException e)
            {
                Assert.IsInstanceOf<InvalidOperationException>(e.InnerException);
            }

            var suppressValue = (bool)suppressField.GetValue(manager);
            Assert.IsFalse(suppressValue, "_suppressInventoryChangedHandling should be false after exception");

            UnityEngine.Object.DestroyImmediate(go);
            UnityEngine.Object.DestroyImmediate(inventoryGo);
        }
    }
}
#endif
