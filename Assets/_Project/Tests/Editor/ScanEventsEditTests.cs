#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using NUnit.Framework;
using Unity.Mathematics;
using Hecton8.Gameplay;
using Hecton8.Core;

namespace Hecton8.Tests.Editor
{
    public class MockScanEventListener : IScanEventListener
    {
        public int CallCount { get; private set; }
        public ScanEventPayload LastPayload { get; private set; }

        public void OnScanEvent(in ScanEventPayload payload)
        {
            CallCount++;
            LastPayload = payload;
        }

        public void Clear()
        {
            CallCount = 0;
            LastPayload = default;
        }
    }

    [TestFixture]
    public class ScanEventsEditTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetScanEventsState();
                var field = typeof(SystemDispatcher).GetField("_lateFrameEventBudgetActive", BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null) field.SetValue(null, false);
            TimeSliceScheduler.BeginFrame(1.0f, 1);

            var field = typeof(SystemDispatcher).GetField("_lateFrameEventBudgetActive", BindingFlags.NonPublic | BindingFlags.Static);
            if (field != null)
            {
                field.SetValue(null, true);
            }
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                var method = typeof(ScanEvents).GetMethod("ReleaseNativeQueues", BindingFlags.NonPublic | BindingFlags.Static);
                if (method != null)
                {
                    method.Invoke(null, null);
                }
            }
            catch (TargetInvocationException ex)
            {
                if (ex.InnerException != null) throw ex.InnerException;
            }
            finally
            {
                ResetScanEventsState();
                var field = typeof(SystemDispatcher).GetField("_lateFrameEventBudgetActive", BindingFlags.NonPublic | BindingFlags.Static);
                if (field != null) field.SetValue(null, false);
            }
        }

        private void ResetScanEventsState()
        {
            typeof(ScanEvents).GetField("_pendingEventCount", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 0);
            typeof(ScanEvents).GetField("_nextFrameEventCount", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, 0);
            typeof(ScanEvents).GetField("_isDispatching", BindingFlags.NonPublic | BindingFlags.Static)?.SetValue(null, false);

            var listenersField = typeof(ScanEvents).GetField("_listeners", BindingFlags.NonPublic | BindingFlags.Static);
            if (listenersField != null)
            {
                var listeners = listenersField.GetValue(null);
                var clearMethod = listeners.GetType().GetMethod("Clear");
                if (clearMethod != null)
                {
                    clearMethod.Invoke(listeners, null);
                }
                listenersField.SetValue(null, listeners);
            }
        }

        [Test]
        public void TryRaiseScanTriggered_ReturnsTrue_AndQueuesEvent()
        {
            var listener = new MockScanEventListener();
            ScanEvents.Register(listener);

            float3 center = new float3(1f, 2f, 3f);
            float radius = 10f;

            bool result = ScanEvents.TryRaiseScanTriggered(center, radius);
            Assert.IsTrue(result);

            ScanEvents.FlushPending();

            Assert.AreEqual(1, listener.CallCount);
            Assert.AreEqual((ushort)ScanEventType.ScanTriggered, listener.LastPayload.EventType);
            Assert.AreEqual(center, listener.LastPayload.Position);
            Assert.AreEqual(radius, listener.LastPayload.Radius);
            Assert.AreEqual((byte)ScanEntryKind.Scannable, listener.LastPayload.EntryKind);
        }

        [Test]
        public void TryRaiseNodeFound_ReturnsTrue_AndQueuesEvent()
        {
            var listener = new MockScanEventListener();
            ScanEvents.Register(listener);

            float3 pos = new float3(5f, 6f, 7f);

            bool result = ScanEvents.TryRaiseNodeFound(pos);
            Assert.IsTrue(result);

            ScanEvents.FlushPending();

            Assert.AreEqual(1, listener.CallCount);
            Assert.AreEqual((ushort)ScanEventType.NodeFound, listener.LastPayload.EventType);
            Assert.AreEqual(pos, listener.LastPayload.Position);
            Assert.AreEqual((byte)ScanEntryKind.ResourceNode, listener.LastPayload.EntryKind);
        }

        [Test]
        public void TryRaiseEntryDiscovered_ValidHash_ReturnsTrue_AndQueuesEvent()
        {
            var listener = new MockScanEventListener();
            ScanEvents.Register(listener);

            uint entryHash = 1234u;
            uint titleHash = 5678u;
            uint categoryHash = 9012u;
            uint summaryHash = 3456u;
            ScanEntryKind kind = ScanEntryKind.Item;

            bool result = ScanEvents.TryRaiseEntryDiscovered(entryHash, titleHash, categoryHash, summaryHash, kind);
            Assert.IsTrue(result);

            ScanEvents.FlushPending();

            Assert.AreEqual(1, listener.CallCount);
            Assert.AreEqual((ushort)ScanEventType.EntryDiscovered, listener.LastPayload.EventType);
            Assert.AreEqual(entryHash, listener.LastPayload.EntryHash);
            Assert.AreEqual(titleHash, listener.LastPayload.TitleHash);
            Assert.AreEqual(categoryHash, listener.LastPayload.CategoryHash);
            Assert.AreEqual(summaryHash, listener.LastPayload.SummaryHash);
            Assert.AreEqual((byte)kind, listener.LastPayload.EntryKind);
        }

        [Test]
        public void TryRaiseEntryDiscovered_ZeroHash_ReturnsFalse_AndDoesNotQueue()
        {
            var listener = new MockScanEventListener();
            ScanEvents.Register(listener);

            bool result = ScanEvents.TryRaiseEntryDiscovered(0u, 1u, 2u, 3u);
            Assert.IsFalse(result);

            ScanEvents.FlushPending();

            Assert.AreEqual(0, listener.CallCount);
        }

        [Test]
        public void TryRaiseFaunaFeedingObserved_ValidHash_ReturnsTrue_AndQueuesEvent()
        {
            var listener = new MockScanEventListener();
            ScanEvents.Register(listener);

            uint entryHash = 1234u;
            float3 pos = new float3(1f, 1f, 1f);

            bool result = ScanEvents.TryRaiseFaunaFeedingObserved(entryHash, pos);
            Assert.IsTrue(result);

            ScanEvents.FlushPending();

            Assert.AreEqual(1, listener.CallCount);
            Assert.AreEqual((ushort)ScanEventType.FaunaFeedingObserved, listener.LastPayload.EventType);
            Assert.AreEqual(entryHash, listener.LastPayload.EntryHash);
            Assert.AreEqual(pos, listener.LastPayload.Position);
            Assert.AreEqual((byte)ScanEntryKind.Scannable, listener.LastPayload.EntryKind);
        }

        [Test]
        public void TryRaiseFaunaFeedingObserved_ZeroHash_ReturnsFalse()
        {
            bool result = ScanEvents.TryRaiseFaunaFeedingObserved(0u, float3.zero);
            Assert.IsFalse(result);
        }

        [Test]
        public void TryRaiseFaunaMatingObserved_ValidHash_ReturnsTrue_AndQueuesEvent()
        {
            var listener = new MockScanEventListener();
            ScanEvents.Register(listener);

            uint entryHash = 5678u;
            float3 pos = new float3(2f, 2f, 2f);

            bool result = ScanEvents.TryRaiseFaunaMatingObserved(entryHash, pos);
            Assert.IsTrue(result);

            ScanEvents.FlushPending();

            Assert.AreEqual(1, listener.CallCount);
            Assert.AreEqual((ushort)ScanEventType.FaunaMatingObserved, listener.LastPayload.EventType);
            Assert.AreEqual(entryHash, listener.LastPayload.EntryHash);
            Assert.AreEqual(pos, listener.LastPayload.Position);
            Assert.AreEqual((byte)ScanEntryKind.Scannable, listener.LastPayload.EntryKind);
        }

        [Test]
        public void TryRaiseFaunaMatingObserved_ZeroHash_ReturnsFalse()
        {
            bool result = ScanEvents.TryRaiseFaunaMatingObserved(0u, float3.zero);
            Assert.IsFalse(result);
        }

        [Test]
        public void Register_And_Unregister_ManageListenerSuccessfully()
        {
            var listener = new MockScanEventListener();

            // Register and test it gets called
            ScanEvents.Register(listener);
            ScanEvents.TryRaiseNodeFound(float3.zero);
            ScanEvents.FlushPending();
            Assert.AreEqual(1, listener.CallCount);

            listener.Clear();

            // Unregister and test it doesn't get called
            ScanEvents.Unregister(listener);
            ScanEvents.TryRaiseNodeFound(float3.zero);
            ScanEvents.FlushPending();
            Assert.AreEqual(0, listener.CallCount);
        }

        [Test]
        public void FlushPending_WithoutListeners_DrainsQueue()
        {
            ScanEvents.TryRaiseNodeFound(float3.zero);
            Assert.AreEqual(1, ScanEvents.PendingCount);

            ScanEvents.FlushPending();
            Assert.AreEqual(0, ScanEvents.PendingCount);
        }
    }
}
#endif
