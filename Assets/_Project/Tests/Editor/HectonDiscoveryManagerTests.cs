using NUnit.Framework;
using UnityEngine;
using Hecton8.Gameplay;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public sealed class HectonDiscoveryManagerTests
    {
        private GameObject _go;
        private HectonDiscoveryManager _manager;

        [SetUp]
        public void Setup()
        {
            _go = new GameObject();
            _manager = _go.AddComponent<HectonDiscoveryManager>();
        }

        [TearDown]
        public void Teardown()
        {
            Object.DestroyImmediate(_go);
        }

        [Test]
        public void DiscoverBiome_ValidId_DiscoversAndUpdatesState()
        {
            int biomeId = 5;

            // Initially not discovered
            Assert.AreEqual(-1, _manager.LastDiscoveredId);
            Assert.AreEqual(0, _manager.TotalDiscovered);

            _manager.DiscoverBiome(biomeId);

            // Verify state updated
            Assert.AreEqual(biomeId, _manager.LastDiscoveredId);
            Assert.AreEqual(1, _manager.TotalDiscovered);
        }

        [Test]
        public void DiscoverBiome_InvalidId_DoesNothing()
        {
            int invalidBiomeId = 0; // Assuming 1-108 is valid

            _manager.DiscoverBiome(invalidBiomeId);

            Assert.AreEqual(-1, _manager.LastDiscoveredId);
            Assert.AreEqual(0, _manager.TotalDiscovered);
        }

        [Test]
        public void DiscoverBiome_DuplicateId_OnlyDiscoversOnce()
        {
            int biomeId = 10;

            _manager.DiscoverBiome(biomeId);
            Assert.AreEqual(1, _manager.TotalDiscovered);
            Assert.AreEqual(biomeId, _manager.LastDiscoveredId);

            // Change LastDiscoveredId to something else to verify it doesn't get updated on duplicate call
            _manager.DiscoverBiome(15);
            Assert.AreEqual(15, _manager.LastDiscoveredId);

            // Duplicate call
            _manager.DiscoverBiome(biomeId);

            // Total should remain 2, LastDiscoveredId should NOT revert to 10
            Assert.AreEqual(2, _manager.TotalDiscovered);
            Assert.AreEqual(15, _manager.LastDiscoveredId);
        }

        [Test]
        public void OnScanEvent_NullOrEmptyEntryHash_DoesNotThrow()
        {
            var payload = new ScanEventPayload
            {
                EventType = (ushort)ScanEventType.FaunaFeedingObserved,
                EntryHash = 0u
            };

            // This should not throw or modify anything
            Assert.DoesNotThrow(() => _manager.OnScanEvent(in payload));
        }

        [Test]
        public void OnScanEvent_IrrelevantEventType_DoesNotThrow()
        {
            var payload = new ScanEventPayload
            {
                EventType = (ushort)ScanEventType.NodeFound,
                EntryHash = 12345u
            };

            // This should not throw or modify anything
            Assert.DoesNotThrow(() => _manager.OnScanEvent(in payload));
        }
}
}
