using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Optimization;

namespace Hecton8.Tests.Optimization
{
    [TestFixture]
    public class RenderTextureLifecycleTrackerTests
    {
        private GameObject _trackerGo;
        private RenderTextureLifecycleTracker _tracker;
        private GameObject _ownerGo;
        private Camera _ownerCamera;

        [SetUp]
        public void SetUp()
        {
            _trackerGo = new GameObject("RenderTextureLifecycleTracker");
            _tracker = _trackerGo.AddComponent<RenderTextureLifecycleTracker>();

            _ownerGo = new GameObject("OwnerCamera");
            _ownerCamera = _ownerGo.AddComponent<Camera>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_ownerGo != null)
            {
                Object.DestroyImmediate(_ownerGo);
            }
            if (_trackerGo != null)
            {
                Object.DestroyImmediate(_trackerGo);
            }
        }

        [Test]
        public void GetLeakedRenderTextures_IdentifiesLeakedRenderTexture()
        {
            // Arrange
            RenderTexture rt = new RenderTexture(256, 256, 16);
            rt.Create();

            // We need to bypass the time passing in the normal way
            // because waiting 10+ seconds in a test is slow.
            // Let's reflect on _allocations to set up a leaked record manually.

            EntityId rtEntityId = rt.GetEntityId();

            var record = new RenderTextureAllocationRecord
            {
                RenderTexture = rt,
                Owner = null, // Mocking that owner is destroyed
                Width = rt.width,
                Height = rt.height,
                Format = rt.format,
                AllocationTime = -20f, // Force allocation time way in the past so (now - alloc) > 10f
                IsDisposed = false
            };

            var allocationsField = typeof(RenderTextureLifecycleTracker).GetField("_allocations", BindingFlags.NonPublic | BindingFlags.Instance);
            var allocations = (Dictionary<EntityId, RenderTextureAllocationRecord>)allocationsField.GetValue(_tracker);
            allocations[rtEntityId] = record;

            // Act
            var leaks = new List<RenderTextureAllocationRecord>();
            _tracker.GetLeakedRenderTextures(leaks);

            // Assert
            Assert.AreEqual(1, leaks.Count, "Expected exactly 1 leaked render texture to be found.");
            Assert.AreEqual(rt, leaks[0].RenderTexture, "The leaked render texture should match the created one.");

            // Clean up
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        [Test]
        public void GetLeakedRenderTextures_DoesNotIdentifyValidRenderTexture()
        {
            // Arrange
            RenderTexture rt = new RenderTexture(256, 256, 16);
            rt.Create();

            EntityId rtEntityId = rt.GetEntityId();

            var record = new RenderTextureAllocationRecord
            {
                RenderTexture = rt,
                Owner = _ownerCamera, // Owner still valid
                Width = rt.width,
                Height = rt.height,
                Format = rt.format,
                AllocationTime = -20f, // Old allocation, but owner alive
                IsDisposed = false
            };

            var allocationsField = typeof(RenderTextureLifecycleTracker).GetField("_allocations", BindingFlags.NonPublic | BindingFlags.Instance);
            var allocations = (Dictionary<EntityId, RenderTextureAllocationRecord>)allocationsField.GetValue(_tracker);
            allocations[rtEntityId] = record;

            // Act
            var leaks = new List<RenderTextureAllocationRecord>();
            _tracker.GetLeakedRenderTextures(leaks);

            // Assert
            Assert.AreEqual(0, leaks.Count, "Expected 0 leaked render textures.");

            // Clean up
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        [Test]
        public void GetLeakedRenderTextures_DoesNotIdentifyDisposedRenderTexture()
        {
            // Arrange
            RenderTexture rt = new RenderTexture(256, 256, 16);
            rt.Create();

            EntityId rtEntityId = rt.GetEntityId();

            var record = new RenderTextureAllocationRecord
            {
                RenderTexture = rt,
                Owner = null, // Owner destroyed
                Width = rt.width,
                Height = rt.height,
                Format = rt.format,
                AllocationTime = -20f,
                IsDisposed = true // But RT is disposed
            };

            var allocationsField = typeof(RenderTextureLifecycleTracker).GetField("_allocations", BindingFlags.NonPublic | BindingFlags.Instance);
            var allocations = (Dictionary<EntityId, RenderTextureAllocationRecord>)allocationsField.GetValue(_tracker);
            allocations[rtEntityId] = record;

            // Act
            var leaks = new List<RenderTextureAllocationRecord>();
            _tracker.GetLeakedRenderTextures(leaks);

            // Assert
            Assert.AreEqual(0, leaks.Count, "Expected 0 leaked render textures because it is disposed.");

            // Clean up
            rt.Release();
            Object.DestroyImmediate(rt);
        }

        [Test]
        public void GetLeakedRenderTextures_DoesNotIdentifyRecentRenderTexture()
        {
            // Arrange
            RenderTexture rt = new RenderTexture(256, 256, 16);
            rt.Create();

            EntityId rtEntityId = rt.GetEntityId();

            var record = new RenderTextureAllocationRecord
            {
                RenderTexture = rt,
                Owner = null, // Owner destroyed
                Width = rt.width,
                Height = rt.height,
                Format = rt.format,
                AllocationTime = 0f, // Allocated recently, assuming ResolveLifecycleClockSeconds is ~0
                IsDisposed = false
            };

            var allocationsField = typeof(RenderTextureLifecycleTracker).GetField("_allocations", BindingFlags.NonPublic | BindingFlags.Instance);
            var allocations = (Dictionary<EntityId, RenderTextureAllocationRecord>)allocationsField.GetValue(_tracker);
            allocations[rtEntityId] = record;

            // Act
            var leaks = new List<RenderTextureAllocationRecord>();
            _tracker.GetLeakedRenderTextures(leaks);

            // Assert
            Assert.AreEqual(0, leaks.Count, "Expected 0 leaked render textures because it has not passed the 10s threshold.");

            // Clean up
            rt.Release();
            Object.DestroyImmediate(rt);
        }
    }
}
