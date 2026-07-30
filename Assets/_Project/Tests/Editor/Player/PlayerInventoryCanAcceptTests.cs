using System;
using NUnit.Framework;
using UnityEngine;
using Hecton8.Inventory;
using Object = UnityEngine.Object;

namespace Hecton8.Tests.Editor
{
    public class PlayerInventoryCanAcceptTests
    {
        private GameObject _go;
        private PlayerInventory _inventory;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject("TestInventory");
            _inventory = _go.AddComponent<PlayerInventory>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null)
            {
                Object.DestroyImmediate(_go);
            }
        }

        [Test]
        public void CanAcceptItemQuantity_Uninitialized_ReturnsFalse()
        {
            // By default _grid is null because we haven't initialized the inventory.
            bool result = _inventory.CanAcceptItemQuantity(12345, 1);
            Assert.IsFalse(result, "Expected uninitialized inventory to return false for CanAcceptItemQuantity.");
        }

        [Test]
        public void CanAcceptItemQuantity_ZeroQuantity_ReturnsFalse()
        {
            bool result = _inventory.CanAcceptItemQuantity(12345, 0);
            Assert.IsFalse(result, "Expected 0 quantity to return false.");
        }

        [Test]
        public void CanAcceptItemQuantity_NegativeQuantity_ReturnsFalse()
        {
            bool result = _inventory.CanAcceptItemQuantity(12345, -1);
            Assert.IsFalse(result, "Expected negative quantity to return false.");
        }

        [Test]
        public void CanAcceptItemQuantityBatch_Uninitialized_ReturnsFalse()
        {
            int[] hashes = new[] { 12345 };
            int[] quantities = new[] { 1 };

            bool result = _inventory.CanAcceptItemQuantityBatch(hashes.AsSpan(), quantities.AsSpan(), 1);
            Assert.IsFalse(result, "Expected uninitialized inventory to return false for CanAcceptItemQuantityBatch.");
        }

        [Test]
        public void CanAcceptItemQuantityBatch_NegativeCount_ReturnsFalse()
        {
            int[] hashes = new[] { 12345 };
            int[] quantities = new[] { 1 };

            bool result = _inventory.CanAcceptItemQuantityBatch(hashes.AsSpan(), quantities.AsSpan(), -1);
            Assert.IsFalse(result, "Expected negative count to return false.");
        }

        [Test]
        public void CanAcceptItemQuantityBatch_CountExceedsArrays_ReturnsFalse()
        {
            int[] hashes = new[] { 12345 };
            int[] quantities = new[] { 1 };

            // Testing the early exit where count > length
            bool result = _inventory.CanAcceptItemQuantityBatch(hashes.AsSpan(), quantities.AsSpan(), 2);
            Assert.IsFalse(result, "Expected count exceeding array length to return false.");
        }
    }
}
