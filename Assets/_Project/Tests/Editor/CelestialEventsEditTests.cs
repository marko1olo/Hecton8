#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using NUnit.Framework;
using NSubstitute;
using UnityEngine;
using System.Reflection;
using Hecton8.Core;
using Hecton8.Celestial;
using System;

namespace Hecton8.Tests.Editor
{
    public class CelestialEventsEditTests
    {
        [SetUp]
        public void SetUp()
        {
            ResetStaticState();
        }

        [TearDown]
        public void TearDown()
        {
            ResetStaticState();
        }

        private void ResetStaticState()
        {
            MethodInfo resetMethod = typeof(CelestialEvents).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            resetMethod.Invoke(null, null);
        }

        [Test]
        public void TryRaiseEclipseStarted_NoListeners_ReturnsFalse()
        {
            bool result = CelestialEvents.TryRaiseEclipseStarted();
            Assert.IsFalse(result);
            Assert.AreEqual(0, CelestialEvents.PendingCount);
        }

        [Test]
        public void TryRaiseEclipseStarted_WithListener_QueuesEvent()
        {
            ICelestialEventListener listener = Substitute.For<ICelestialEventListener>();
            CelestialEvents.Register(listener);

            bool result = CelestialEvents.TryRaiseEclipseStarted();

            Assert.IsTrue(result);
            Assert.AreEqual(1, CelestialEvents.PendingCount);
        }

        [Test]
        public void TryRaiseEclipseEnded_WithListener_QueuesEvent()
        {
            ICelestialEventListener listener = Substitute.For<ICelestialEventListener>();
            CelestialEvents.Register(listener);

            bool result = CelestialEvents.TryRaiseEclipseEnded();

            Assert.IsTrue(result);
            Assert.AreEqual(1, CelestialEvents.PendingCount);
        }

        [Test]
        public void TryRaiseSunAngleChanged_CoalescesMultipleEvents()
        {
            ICelestialEventListener listener = Substitute.For<ICelestialEventListener>();
            CelestialEvents.Register(listener);

            bool r1 = CelestialEvents.TryRaiseSunAngleChanged(45f);
            bool r2 = CelestialEvents.TryRaiseSunAngleChanged(90f);

            Assert.IsTrue(r1);
            Assert.IsTrue(r2); // True because it successfully processed (coalesced)
            Assert.AreEqual(1, CelestialEvents.PendingCount); // Should only have one event queued

            CelestialEvents.FlushPending();

            listener.Received(1).OnCelestialSunAngleChanged(90f);
            listener.DidNotReceive().OnCelestialSunAngleChanged(45f);
        }

        [Test]
        public void TryRaisePlanetPhaseChanged_CoalescesMultipleEvents()
        {
            ICelestialEventListener listener = Substitute.For<ICelestialEventListener>();
            CelestialEvents.Register(listener);

            bool r1 = CelestialEvents.TryRaisePlanetPhaseChanged(0.5f);
            bool r2 = CelestialEvents.TryRaisePlanetPhaseChanged(0.8f);

            Assert.IsTrue(r1);
            Assert.IsTrue(r2);
            Assert.AreEqual(1, CelestialEvents.PendingCount);

            CelestialEvents.FlushPending();

            listener.Received(1).OnCelestialPlanetPhaseChanged(0.8f);
            listener.DidNotReceive().OnCelestialPlanetPhaseChanged(0.5f);
        }

        [Test]
        public void FlushPending_DispatchesEclipseStartedToListeners()
        {
            ICelestialEventListener listener = Substitute.For<ICelestialEventListener>();
            CelestialEvents.Register(listener);

            CelestialEvents.TryRaiseEclipseStarted();
            CelestialEvents.FlushPending();

            listener.Received(1).OnCelestialEclipseStarted();
            Assert.AreEqual(0, CelestialEvents.PendingCount);
        }

        [Test]
        public void FlushPending_DispatchesEclipseEndedToListeners()
        {
            ICelestialEventListener listener = Substitute.For<ICelestialEventListener>();
            CelestialEvents.Register(listener);

            CelestialEvents.TryRaiseEclipseEnded();
            CelestialEvents.FlushPending();

            listener.Received(1).OnCelestialEclipseEnded();
        }

        [Test]
        public void Register_WhileDispatching_QueuesDeferredRegistration()
        {
            ICelestialEventListener existingListener = Substitute.For<ICelestialEventListener>();
            ICelestialEventListener newListener = Substitute.For<ICelestialEventListener>();

            CelestialEvents.Register(existingListener);

            existingListener.When(x => x.OnCelestialEclipseStarted()).Do(_ =>
            {
                CelestialEvents.Register(newListener);
            });

            CelestialEvents.TryRaiseEclipseStarted();
            CelestialEvents.FlushPending();

            // At this point, newListener is registered (deferred mutations applied after dispatching)

            CelestialEvents.TryRaiseEclipseEnded();
            CelestialEvents.FlushPending();

            newListener.Received(1).OnCelestialEclipseEnded();
        }

        [Test]
        public void Unregister_WhileDispatching_QueuesDeferredUnregistration()
        {
            ICelestialEventListener listener = Substitute.For<ICelestialEventListener>();
            CelestialEvents.Register(listener);

            listener.When(x => x.OnCelestialEclipseStarted()).Do(_ =>
            {
                CelestialEvents.Unregister(listener);
            });

            CelestialEvents.TryRaiseEclipseStarted();
            CelestialEvents.TryRaiseEclipseEnded();

            CelestialEvents.FlushPending();

            listener.Received(1).OnCelestialEclipseStarted();
            // In the same FlushPending, EclipseEnded would NOT be sent if the unregister took immediate effect during dispatch,
            // however, since deferred unregister check prevents it, it should not receive the second event.
            listener.DidNotReceive().OnCelestialEclipseEnded();
        }

        [Test]
        public void ListenerException_DoesNotStopOtherListeners()
        {
            ICelestialEventListener badListener = Substitute.For<ICelestialEventListener>();
            badListener.When(x => x.OnCelestialEclipseStarted()).Throw(new Exception("Bad listener exception"));

            ICelestialEventListener goodListener = Substitute.For<ICelestialEventListener>();

            CelestialEvents.Register(badListener);
            CelestialEvents.Register(goodListener);

            CelestialEvents.TryRaiseEclipseStarted();
            CelestialEvents.FlushPending();

            goodListener.Received(1).OnCelestialEclipseStarted();
        }
    }
}
#endif
