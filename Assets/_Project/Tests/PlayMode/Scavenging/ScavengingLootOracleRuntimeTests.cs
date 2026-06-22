using System;
using System.Reflection;
using System.Collections;
using Hecton8.Scavenging;
using Hecton8.World;
using Hecton8.Core.Memory;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;

namespace Hecton8.Tests.PlayMode.Scavenging
{
    public class ScavengingLootOracleRuntimeTests
    {
        private GameObject _hostGo;
        private ScavengingLootOracleRuntime _host;
        private FieldInfo _hostStaticField;
        private FieldInfo _vaultReadyField;
        private FieldInfo _vaultField;
        private FieldInfo _lootTableHydratedField;
        private FieldInfo _publishPendingField;
        private FieldInfo _nativeScratchField;
        private FieldInfo _queuedCountField;

        private Type _simulationNativeScratchType;
        private FieldInfo _scratchRequestsField;

        [SetUp]
        public void SetUp()
        {
            _hostGo = new GameObject("ScavengingLootOracleRuntimeTest");
            _host = _hostGo.AddComponent<ScavengingLootOracleRuntime>();

            _hostStaticField = typeof(ScavengingLootOracleRuntime).GetField("_host", BindingFlags.Static | BindingFlags.NonPublic);
            _vaultReadyField = typeof(ScavengingLootOracleRuntime).GetField("_vaultReady", BindingFlags.Instance | BindingFlags.NonPublic);
            _vaultField = typeof(ScavengingLootOracleRuntime).GetField("_vault", BindingFlags.Instance | BindingFlags.NonPublic);
            _lootTableHydratedField = typeof(ScavengingLootOracleRuntime).GetField("_lootTableHydrated", BindingFlags.Instance | BindingFlags.NonPublic);
            _publishPendingField = typeof(ScavengingLootOracleRuntime).GetField("_publishPending", BindingFlags.Instance | BindingFlags.NonPublic);
            _nativeScratchField = typeof(ScavengingLootOracleRuntime).GetField("_nativeScratch", BindingFlags.Instance | BindingFlags.NonPublic);
            _queuedCountField = typeof(ScavengingLootOracleRuntime).GetField("_queuedCount", BindingFlags.Instance | BindingFlags.NonPublic);

            _simulationNativeScratchType = typeof(ScavengingLootOracleRuntime).GetNestedType("SimulationNativeScratch", BindingFlags.NonPublic);
            _scratchRequestsField = _simulationNativeScratchType.GetField("Requests");

            // Reset static host
            _hostStaticField.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            if (_hostGo != null)
            {
                UnityEngine.Object.DestroyImmediate(_hostGo);
            }
            _hostStaticField.SetValue(null, null);
        }

        [Test]
        public void TryQueueResourceNodeLoot_WhenHostNotPrepared_ReturnsFalse()
        {
            AbsoluteUniversePosition nodeAup = new AbsoluteUniversePosition();
            bool result = ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(
                nodeAup,
                oreHash: 123,
                forcedItemHash: 0,
                quantity: 1,
                toolMask: ScavengingLootOracleConstants.ToolMaskDrill,
                inventoryCapacityAvailable: true,
                emitDepletionDelta: true
            );

            Assert.IsFalse(result, "Expected TryQueueResourceNodeLoot to return false when no prepared host exists.");
        }

        [Test]
        public void TryQueueResourceNodeLoot_WhenPublishPending_ReturnsFalse()
        {
            // Set up the host
            _hostStaticField.SetValue(null, _host);
            _vaultReadyField.SetValue(_host, true);
            _lootTableHydratedField.SetValue(_host, true);
            // Mock IDataVault is required
            var mockVault = new MockDataVault();
            _vaultField.SetValue(_host, mockVault);

            _publishPendingField.SetValue(_host, true);

            AbsoluteUniversePosition nodeAup = new AbsoluteUniversePosition();
            bool result = ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(
                nodeAup,
                oreHash: 123,
                forcedItemHash: 0,
                quantity: 1,
                toolMask: ScavengingLootOracleConstants.ToolMaskDrill,
                inventoryCapacityAvailable: true,
                emitDepletionDelta: true
            );

            Assert.IsFalse(result, "Expected TryQueueResourceNodeLoot to return false when _publishPending is true.");
        }

        [Test]
        public void TryQueueResourceNodeLoot_WhenRequestsNotCreated_ReturnsFalse()
        {
            // Set up the host
            _hostStaticField.SetValue(null, _host);
            _vaultReadyField.SetValue(_host, true);
            _lootTableHydratedField.SetValue(_host, true);
            var mockVault = new MockDataVault();
            _vaultField.SetValue(_host, mockVault);

            _publishPendingField.SetValue(_host, false);

            // _nativeScratch.Requests is not created by default
            AbsoluteUniversePosition nodeAup = new AbsoluteUniversePosition();
            bool result = ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(
                nodeAup,
                oreHash: 123,
                forcedItemHash: 0,
                quantity: 1,
                toolMask: ScavengingLootOracleConstants.ToolMaskDrill,
                inventoryCapacityAvailable: true,
                emitDepletionDelta: true
            );

            Assert.IsFalse(result, "Expected TryQueueResourceNodeLoot to return false when Requests array is not created.");
        }

        [Test]
        public void TryQueueResourceNodeLoot_WhenQueueFull_ReturnsFalse()
        {
            // Set up the host
            _hostStaticField.SetValue(null, _host);
            _vaultReadyField.SetValue(_host, true);
            _lootTableHydratedField.SetValue(_host, true);
            var mockVault = new MockDataVault();
            _vaultField.SetValue(_host, mockVault);

            _publishPendingField.SetValue(_host, false);

            // Create _nativeScratch
            object nativeScratch = Activator.CreateInstance(_simulationNativeScratchType);
            NativeArray<ScavengingHarvestRequestDTO> requests = new NativeArray<ScavengingHarvestRequestDTO>(2, Allocator.Temp);
            _scratchRequestsField.SetValue(nativeScratch, requests);
            _nativeScratchField.SetValue(_host, nativeScratch);

            // Set queuedCount to >= length
            _queuedCountField.SetValue(_host, 2);

            try
            {
                AbsoluteUniversePosition nodeAup = new AbsoluteUniversePosition();
                bool result = ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(
                    nodeAup,
                    oreHash: 123,
                    forcedItemHash: 0,
                    quantity: 1,
                    toolMask: ScavengingLootOracleConstants.ToolMaskDrill,
                    inventoryCapacityAvailable: true,
                    emitDepletionDelta: true
                );

                Assert.IsFalse(result, "Expected TryQueueResourceNodeLoot to return false when queue is full.");
            }
            finally
            {
                requests.Dispose();
            }
        }

        [Test]
        public void TryQueueResourceNodeLoot_WhenValidStateAndCapacityAvailable_ReturnsTrue()
        {
            // Set up the host
            _hostStaticField.SetValue(null, _host);
            _vaultReadyField.SetValue(_host, true);
            _lootTableHydratedField.SetValue(_host, true);
            var mockVault = new MockDataVault();
            _vaultField.SetValue(_host, mockVault);

            _publishPendingField.SetValue(_host, false);

            // Create _nativeScratch
            object nativeScratch = Activator.CreateInstance(_simulationNativeScratchType);
            NativeArray<ScavengingHarvestRequestDTO> requests = new NativeArray<ScavengingHarvestRequestDTO>(2, Allocator.Temp);
            _scratchRequestsField.SetValue(nativeScratch, requests);
            _nativeScratchField.SetValue(_host, nativeScratch);

            // Set queuedCount to < length
            _queuedCountField.SetValue(_host, 0);

            try
            {
                AbsoluteUniversePosition nodeAup = new AbsoluteUniversePosition();
                bool result = ScavengingLootOracleRuntime.TryQueueResourceNodeLoot(
                    nodeAup,
                    oreHash: 123,
                    forcedItemHash: 0,
                    quantity: 1,
                    toolMask: ScavengingLootOracleConstants.ToolMaskDrill,
                    inventoryCapacityAvailable: true,
                    emitDepletionDelta: true
                );

                Assert.IsTrue(result, "Expected TryQueueResourceNodeLoot to return true when queued successfully.");

                int newQueuedCount = (int)_queuedCountField.GetValue(_host);
                Assert.AreEqual(1, newQueuedCount, "Expected queued count to increment.");

                var updatedRequests = (NativeArray<ScavengingHarvestRequestDTO>)_scratchRequestsField.GetValue(_nativeScratchField.GetValue(_host));
                Assert.AreEqual(123u, updatedRequests[0].OreHash, "Expected OreHash to be set correctly.");
            }
            finally
            {
                requests.Dispose();
            }
        }

        // Mock IDataVault
        private class MockDataVault : IDataVault
        {
            public bool IsAllocationLocked => false;
            public bool IsCompactionFenceActive => false;

            public VaultGenerationHandle<T> EnsureGenerationHandle<T>(BufferID bufferId, int requiredLength, Hecton8.Core.SystemID ownerSystem, NativeArrayOptions options = NativeArrayOptions.ClearMemory) where T : struct
            {
                return default;
            }

            public bool TryGetGenerationHandle<T>(BufferID bufferId, out VaultGenerationHandle<T> handle) where T : struct
            {
                handle = default;
                return false;
            }

            public bool TryResolveHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T> buffer) where T : struct
            {
                buffer = default;
                return false;
            }

            public bool TryReadOnlyHandle<T>(in VaultGenerationHandle<T> handle, out NativeArray<T>.ReadOnly buffer) where T : struct
            {
                buffer = default;
                return false;
            }

            public bool TryAcquireWriteLock<T>(in VaultGenerationHandle<T> handle, Hecton8.Core.SystemID systemId, out NativeArray<T> buffer) where T : struct
            {
                buffer = default;
                return false;
            }

            public void ReleaseWriteLock<T>(in VaultGenerationHandle<T> handle, Hecton8.Core.SystemID systemId) where T : struct
            {
            }

            public bool ReleaseBuffer<T>(in VaultGenerationHandle<T> handle) where T : struct
            {
                return false;
            }
        }
    }
}
