using NUnit.Framework;
using Hecton8.Bootstrap;
using Hecton8.Core;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BootstrapEventsEditTests
    {
        private class MockListener : IBootstrapEventListener
        {
            public int InvocationCount;
            public BootstrapEventPayload LastPayload;

            public void OnBootstrapEvent(in BootstrapEventPayload payload)
            {
                InvocationCount++;
                LastPayload = payload;
            }
        }

        private class MutatingListener : IBootstrapEventListener
        {
            public readonly MockListener AddedListener = new MockListener();
            public readonly MockListener RemovedListener;

            public MutatingListener(MockListener removedListener = null)
            {
                RemovedListener = removedListener;
            }

            public void OnBootstrapEvent(in BootstrapEventPayload payload)
            {
                BootstrapEvents.Register(AddedListener);
                if (RemovedListener != null)
                    BootstrapEvents.Unregister(RemovedListener);

                BootstrapEvents.Unregister(this);
            }
        }

        [SetUp]
        public void SetUp()
        {
            BootstrapEvents.ResetStaticState();

            var activeRuntimeInstanceField = typeof(SystemDispatcher).GetProperty("ActiveRuntimeInstance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            if (activeRuntimeInstanceField != null)
                activeRuntimeInstanceField.SetValue(null, null);
        }

        [TearDown]
        public void TearDown()
        {
            BootstrapEvents.ResetStaticState();
        }

        [Test]
        public void Register_NullListener_DoesNothing()
        {
            BootstrapEvents.Register(null);

            Assert.IsTrue(BootstrapEvents.TryNotifyBootstrapComplete());
            BootstrapEvents.FlushPending();

            Assert.Pass();
        }

        [Test]
        public void Unregister_NullListener_DoesNothing()
        {
            BootstrapEvents.Unregister(null);
            Assert.Pass();
        }

        [Test]
        public void RegisterAndFlush_NotifiesListener()
        {
            MockListener listener = new MockListener();
            BootstrapEvents.Register(listener);

            Assert.IsTrue(BootstrapEvents.TryNotifyBootstrapComplete());

            GameObject dummyDispatcher = new GameObject("DummyDispatcher");
            SystemDispatcher dispatcher = dummyDispatcher.AddComponent<SystemDispatcher>();
            SetLateFrameEventBudget(true, 100);

            BootstrapEvents.FlushPending();

            SetLateFrameEventBudget(false, 0);
            GameObject.DestroyImmediate(dummyDispatcher);

            Assert.AreEqual(1, listener.InvocationCount);
            Assert.IsTrue(BootstrapEventPayload.IsCompleteEvent(listener.LastPayload));
        }

        [Test]
        public void Unregister_RemovesListener()
        {
            MockListener listener = new MockListener();
            BootstrapEvents.Register(listener);
            BootstrapEvents.Unregister(listener);

            Assert.IsTrue(BootstrapEvents.TryNotifyBootstrapComplete());

            GameObject dummyDispatcher = new GameObject("DummyDispatcher");
            SystemDispatcher dispatcher = dummyDispatcher.AddComponent<SystemDispatcher>();
            SetLateFrameEventBudget(true, 100);

            BootstrapEvents.FlushPending();

            SetLateFrameEventBudget(false, 0);
            GameObject.DestroyImmediate(dummyDispatcher);

            Assert.AreEqual(0, listener.InvocationCount);
        }

        [Test]
        public void Register_DuplicateListener_OnlyAddedOnce()
        {
            MockListener listener = new MockListener();
            BootstrapEvents.Register(listener);
            BootstrapEvents.Register(listener);

            Assert.IsTrue(BootstrapEvents.TryNotifyBootstrapComplete());

            GameObject dummyDispatcher = new GameObject("DummyDispatcher");
            SystemDispatcher dispatcher = dummyDispatcher.AddComponent<SystemDispatcher>();
            SetLateFrameEventBudget(true, 100);

            BootstrapEvents.FlushPending();

            SetLateFrameEventBudget(false, 0);
            GameObject.DestroyImmediate(dummyDispatcher);

            Assert.AreEqual(1, listener.InvocationCount);
        }

        [Test]
        public void FlushPending_DeferredMutations_AppliedAfterFlush()
        {
            MockListener removedListener = new MockListener();
            MutatingListener mutatingListener = new MutatingListener(removedListener);

            BootstrapEvents.Register(removedListener);
            BootstrapEvents.Register(mutatingListener);

            GameObject dummyDispatcher = new GameObject("DummyDispatcher");
            SystemDispatcher dispatcher = dummyDispatcher.AddComponent<SystemDispatcher>();
            SetLateFrameEventBudget(true, 100);

            Assert.IsTrue(BootstrapEvents.TryNotifyBootstrapComplete());
            BootstrapEvents.FlushPending();

            removedListener.InvocationCount = 0;

            Assert.IsTrue(BootstrapEvents.TryNotifyBootstrapComplete());
            BootstrapEvents.FlushPending();

            SetLateFrameEventBudget(false, 0);
            GameObject.DestroyImmediate(dummyDispatcher);

            Assert.AreEqual(1, mutatingListener.AddedListener.InvocationCount);
            Assert.AreEqual(0, removedListener.InvocationCount);
        }

        [Test]
        public void Register_CapacityExceeded_ReportsOverflowTelemetry()
        {
            for (int i = 0; i < 20; i++)
            {
                BootstrapEvents.Register(new MockListener());
            }

            Assert.Greater(BootstrapEvents.DroppedListenerRegistrationCount, 0);
        }

        private void SetLateFrameEventBudget(bool active, int amount)
        {
            var activeBudgetField = typeof(SystemDispatcher).GetField("_lateFrameEventBudgetActive", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var budgetField = typeof(SystemDispatcher).GetField("_lateFrameEventDispatchBudget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);

            if(activeBudgetField != null && budgetField != null)
            {
                activeBudgetField.SetValue(null, active);
                budgetField.SetValue(null, amount);
            }
        }
    }
}
