#if UNITY_EDITOR && HECTON8_ENABLE_EDITMODE_TESTS
using System;
using System.Reflection;
using Hecton8.Building;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class ModuleStatusEventsEditTests
    {
        [SetUp]
        public void SetUp()
        {
            InvokeResetStaticState();
        }

        [TearDown]
        public void TearDown()
        {
            InvokeResetStaticState();
        }

        [Test]
        public void Flags_CorrectlyIdentifyStatusBits()
        {
            ModuleStatusEventPayload flooded = new ModuleStatusEventPayload { StatusFlags = 1u << 0 };
            Assert.IsTrue(ModuleStatusEvents.IsFlooded(in flooded));
            Assert.IsFalse(ModuleStatusEvents.IsBreached(in flooded));

            ModuleStatusEventPayload breached = new ModuleStatusEventPayload { StatusFlags = 1u << 1 };
            Assert.IsTrue(ModuleStatusEvents.IsBreached(in breached));
            Assert.IsFalse(ModuleStatusEvents.HasPower(in breached));

            ModuleStatusEventPayload power = new ModuleStatusEventPayload { StatusFlags = 1u << 2 };
            Assert.IsTrue(ModuleStatusEvents.HasPower(in power));
            Assert.IsFalse(ModuleStatusEvents.IsPlayerInsideInterior(in power));

            ModuleStatusEventPayload playerInside = new ModuleStatusEventPayload { StatusFlags = 1u << 3 };
            Assert.IsTrue(ModuleStatusEvents.IsPlayerInsideInterior(in playerInside));
            Assert.IsFalse(ModuleStatusEvents.IsAirQualityLow(in playerInside));

            ModuleStatusEventPayload airQualityLow = new ModuleStatusEventPayload { StatusFlags = 1u << 4 };
            Assert.IsTrue(ModuleStatusEvents.IsAirQualityLow(in airQualityLow));
            Assert.IsFalse(ModuleStatusEvents.HasCascadeFailure(in airQualityLow));

            ModuleStatusEventPayload cascadeFailure = new ModuleStatusEventPayload { StatusFlags = 1u << 5 };
            Assert.IsTrue(ModuleStatusEvents.HasCascadeFailure(in cascadeFailure));
            Assert.IsFalse(ModuleStatusEvents.IsFlooded(in cascadeFailure));

        }

        [Test]
        public void IsEnterEvent_ReturnsTrue_OnlyForEnterEventType()
        {
            ModuleStatusEventPayload enter = new ModuleStatusEventPayload { EventType = (ushort)ModuleStatusEventType.Enter };
            Assert.IsTrue(ModuleStatusEvents.IsEnterEvent(in enter));

            ModuleStatusEventPayload exit = new ModuleStatusEventPayload { EventType = (ushort)ModuleStatusEventType.Exit };
            Assert.IsFalse(ModuleStatusEvents.IsEnterEvent(in exit));

            ModuleStatusEventPayload invalid = new ModuleStatusEventPayload { EventType = 255 };
            Assert.IsFalse(ModuleStatusEvents.IsEnterEvent(in invalid));
        }

        [Test]
        public void ListenerRegistry_RegistersAndUnregistersCorrectly()
        {
            InvokeEnsureInitialized();

            IModuleStatusEventListener listener = Substitute.For<IModuleStatusEventListener>();

            object registryObj = GetPrivateStaticField("_listeners");
            Type registryType = registryObj.GetType();

            MethodInfo tryRegister = registryType.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo unregister = registryType.GetMethod("Unregister", BindingFlags.Public | BindingFlags.Instance);
            MethodInfo contains = registryType.GetMethod("Contains", BindingFlags.Public | BindingFlags.Instance);
            PropertyInfo countProp = registryType.GetProperty("Count", BindingFlags.Public | BindingFlags.Instance);

            // Register
            bool registered = (bool)tryRegister.Invoke(registryObj, new object[] { listener });
            Assert.IsTrue(registered);

            bool isContained = (bool)contains.Invoke(registryObj, new object[] { listener });
            Assert.IsTrue(isContained);

            int count = (int)countProp.GetValue(registryObj);
            Assert.AreEqual(1, count);

            // Set back modified struct value
            SetPrivateStaticField("_listeners", registryObj);

            // Enqueue test payload directly to bypass Application.isPlaying
            ModuleStatusEventPayload payload = new ModuleStatusEventPayload { EventType = (ushort)ModuleStatusEventType.Enter };
            InvokeEnqueue(in payload);

            Assert.AreEqual(1, ModuleStatusEvents.PendingCount);

            ModuleStatusEvents.FlushPending();

            Assert.AreEqual(0, ModuleStatusEvents.PendingCount);
            listener.Received(1).OnModuleStatusEvent(Arg.Any<ModuleStatusEventPayload>());

            // Unregister
            registryObj = GetPrivateStaticField("_listeners");
            unregister.Invoke(registryObj, new object[] { listener });
            SetPrivateStaticField("_listeners", registryObj);

            isContained = (bool)contains.Invoke(registryObj, new object[] { listener });
            Assert.IsFalse(isContained);

            count = (int)countProp.GetValue(registryObj);
            Assert.AreEqual(0, count);
        }

        [Test]
        public void Enqueue_DropsEvents_WhenExceedingCapacity()
        {
            InvokeEnsureInitialized();

            int pendingEventCapacity = 128;

            for (int i = 0; i < pendingEventCapacity; i++)
            {
                ModuleStatusEventPayload payload = new ModuleStatusEventPayload { EventType = (ushort)ModuleStatusEventType.Enter };
                bool enqueued = InvokeEnqueue(in payload);
                Assert.IsTrue(enqueued);
            }

            Assert.AreEqual(pendingEventCapacity, ModuleStatusEvents.PendingCount);
            Assert.AreEqual(0, ModuleStatusEvents.DroppedEventCount);

            ModuleStatusEventPayload dropPayload = new ModuleStatusEventPayload { EventType = (ushort)ModuleStatusEventType.Enter };
            bool dropped = InvokeEnqueue(in dropPayload);

            Assert.IsFalse(dropped);
            Assert.AreEqual(pendingEventCapacity, ModuleStatusEvents.PendingCount);
            Assert.AreEqual(1, ModuleStatusEvents.DroppedEventCount);
        }

        [Test]
        public void ReentrantEnqueue_RoutesToNextFrameQueue()
        {
            InvokeEnsureInitialized();

            IModuleStatusEventListener reentrantListener = Substitute.For<IModuleStatusEventListener>();

            object registryObj = GetPrivateStaticField("_listeners");
            Type registryType = registryObj.GetType();
            MethodInfo tryRegister = registryType.GetMethod("TryRegister", BindingFlags.Public | BindingFlags.Instance);
            tryRegister.Invoke(registryObj, new object[] { reentrantListener });
            SetPrivateStaticField("_listeners", registryObj);

            reentrantListener.When(x => x.OnModuleStatusEvent(Arg.Any<ModuleStatusEventPayload>()))
                .Do(callInfo =>
                {
                    ModuleStatusEventPayload nextPayload = new ModuleStatusEventPayload { EventType = (ushort)ModuleStatusEventType.Exit };
                    InvokeEnqueue(in nextPayload);
                });

            ModuleStatusEventPayload payload = new ModuleStatusEventPayload { EventType = (ushort)ModuleStatusEventType.Enter };
            InvokeEnqueue(in payload);

            // First flush processes Enter
            ModuleStatusEvents.FlushPending();

            reentrantListener.Received(1).OnModuleStatusEvent(Arg.Is<ModuleStatusEventPayload>(p => ModuleStatusEvents.IsEnterEvent(in p)));

            // Reentrant enqueue went to next frame, pending count is now 1
            Assert.AreEqual(1, ModuleStatusEvents.PendingCount);

            // Second flush processes Exit
            ModuleStatusEvents.FlushPending();

            reentrantListener.Received(1).OnModuleStatusEvent(Arg.Is<ModuleStatusEventPayload>(p => !ModuleStatusEvents.IsEnterEvent(in p)));
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(ModuleStatusEvents).GetMethod(
                "ResetStaticState",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing ModuleStatusEvents.ResetStaticState");
            reset.Invoke(null, null);
        }

        private static void InvokeEnsureInitialized()
        {
            MethodInfo ensure = typeof(ModuleStatusEvents).GetMethod(
                "EnsureInitialized",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(ensure, "Missing ModuleStatusEvents.EnsureInitialized");
            ensure.Invoke(null, null);
        }

        private static bool InvokeEnqueue(in ModuleStatusEventPayload payload)
        {
            MethodInfo enqueue = typeof(ModuleStatusEvents).GetMethod(
                "Enqueue",
                BindingFlags.NonPublic | BindingFlags.Static,
                null,
                new Type[] { typeof(ModuleStatusEventPayload).MakeByRefType() },
                null);

            Assert.IsNotNull(enqueue, "Missing ModuleStatusEvents.Enqueue(in ModuleStatusEventPayload)");
            object[] args = new object[] { payload };
            bool result = (bool)enqueue.Invoke(null, args);
            return result;
        }

        private static object GetPrivateStaticField(string fieldName)
        {
            FieldInfo field = typeof(ModuleStatusEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing ModuleStatusEvents field: " + fieldName);
            return field.GetValue(null);
        }

        private static void SetPrivateStaticField(string fieldName, object value)
        {
            FieldInfo field = typeof(ModuleStatusEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing ModuleStatusEvents field: " + fieldName);
            field.SetValue(null, value);
        }
    }
}
#endif
