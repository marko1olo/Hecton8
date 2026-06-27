using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Optimization;

namespace Hecton8.Tests.PlayMode.Optimization
{
    public class RenderTextureLifecycleTrackerPlayTests
    {
        private RenderTextureLifecycleTracker _tracker;
        private GameObject _trackerGameObject;

        [SetUp]
        public void Setup()
        {
            _trackerGameObject = new GameObject("LifecycleTrackerTest");
            _tracker = _trackerGameObject.AddComponent<RenderTextureLifecycleTracker>();
        }

        [TearDown]
        public void Teardown()
        {
            if (_trackerGameObject != null)
            {
                Object.DestroyImmediate(_trackerGameObject);
            }
        }

        [UnityTest]
        public IEnumerator DetectsLeak_WhenOwnerDestroyedAndNotDisposed()
        {
            // Create a fake owner
            var ownerGo = new GameObject("FakeOwner");
            var owner = ownerGo.AddComponent<Camera>();

            // Create a RT
            var rt = new RenderTexture(256, 256, 16);

            // Register it
            _tracker.RegisterAllocation(rt, owner, "stack");

            // Should be no leaks initially
            var leaks = new List<RenderTextureAllocationRecord>();
            _tracker.GetLeakedRenderTextures(leaks);
            Assert.AreEqual(0, leaks.Count, "No leaks should exist immediately after registration.");

            // Destroy owner
            Object.DestroyImmediate(ownerGo);

            // Force time advance by reflecting the inner state
            var allocationsField = typeof(RenderTextureLifecycleTracker).GetField("_allocations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allocations = allocationsField.GetValue(_tracker) as Dictionary<EntityId, RenderTextureAllocationRecord>;

            // Get the record and modify its AllocationTime
            // Use reflection to find GetEntityId() or we can just iterate the dictionary.
            var keys = new List<EntityId>(allocations.Keys);
            Assert.AreEqual(1, keys.Count, "Should have 1 registered allocation.");

            var rtId = keys[0];
            var record = allocations[rtId];
            record.AllocationTime -= 11f; // Force it to be more than 10 seconds old
            allocations[rtId] = record;

            // Now check for leaks
            _tracker.GetLeakedRenderTextures(leaks);
            Assert.AreEqual(1, leaks.Count, "Should detect 1 leak after owner destroyed and 10 seconds elapsed.");
            Assert.AreEqual(rt, leaks[0].RenderTexture);

            // Disposing it should clear the leak
            _tracker.RegisterDisposal(rt);
            _tracker.GetLeakedRenderTextures(leaks);
            Assert.AreEqual(0, leaks.Count, "Should detect 0 leaks after disposal.");

            // Cleanup
            rt.Release();
            Object.DestroyImmediate(rt);

            yield return null;
        }

        [UnityTest]
        public IEnumerator IgnoresLeak_WhenOwnerAliveAndNotDisposed()
        {
            // Create a fake owner
            var ownerGo = new GameObject("FakeOwner2");
            var owner = ownerGo.AddComponent<Camera>();

            // Create a RT
            var rt = new RenderTexture(256, 256, 16);

            // Register it
            _tracker.RegisterAllocation(rt, owner, "stack");

            // Force time advance
            var allocationsField = typeof(RenderTextureLifecycleTracker).GetField("_allocations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allocations = allocationsField.GetValue(_tracker) as Dictionary<EntityId, RenderTextureAllocationRecord>;
            var keys = new List<EntityId>(allocations.Keys);
            var rtId = keys[0];
            var record = allocations[rtId];
            record.AllocationTime -= 11f;
            allocations[rtId] = record;

            // Check for leaks (owner still alive)
            var leaks = new List<RenderTextureAllocationRecord>();
            _tracker.GetLeakedRenderTextures(leaks);
            Assert.AreEqual(0, leaks.Count, "Should not detect leak if owner is still alive.");

            // Cleanup
            Object.DestroyImmediate(ownerGo);
            rt.Release();
            Object.DestroyImmediate(rt);

            yield return null;
        }

        [UnityTest]
        public IEnumerator CheckForLeaks_LogsError_WhenLeakDetected()
        {
            // Note: Since we don't easily mock H8Debug, we'll just invoke CheckForLeaks to ensure it doesn't crash
            // when it processes the leaky records.

            var ownerGo = new GameObject("FakeOwner3");
            var owner = ownerGo.AddComponent<Camera>();
            var rt = new RenderTexture(256, 256, 16);

            _tracker.RegisterAllocation(rt, owner, "stack");
            Object.DestroyImmediate(ownerGo);

            var allocationsField = typeof(RenderTextureLifecycleTracker).GetField("_allocations", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var allocations = allocationsField.GetValue(_tracker) as Dictionary<EntityId, RenderTextureAllocationRecord>;
            var keys = new List<EntityId>(allocations.Keys);
            var rtId = keys[0];
            var record = allocations[rtId];
            record.AllocationTime -= 11f;
            allocations[rtId] = record;

            // Use reflection to invoke private method CheckForLeaks
            var checkForLeaksMethod = typeof(RenderTextureLifecycleTracker).GetMethod("CheckForLeaks", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            checkForLeaksMethod.Invoke(_tracker, null);

            // Verify internal state: should have found 1 leak if we could inspect _leakQueryResults.
            var leakQueryResultsField = typeof(RenderTextureLifecycleTracker).GetField("_leakQueryResults", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var leakQueryResults = leakQueryResultsField.GetValue(_tracker) as List<RenderTextureAllocationRecord>;
            Assert.AreEqual(1, leakQueryResults.Count, "CheckForLeaks should populate _leakQueryResults.");

            rt.Release();
            Object.DestroyImmediate(rt);

            yield return null;
        }
    }
}
