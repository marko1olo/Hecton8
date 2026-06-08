using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Crafting;
using Hecton8.Items;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class CraftingEventsBackpressureEditTests
    {
        [Test]
        public void CraftingEventPayloadLayoutKeepsReservedGenerationSlot()
        {
            StructLayoutAttribute layout = typeof(CraftingEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(64, UnsafeUtility.SizeOf<CraftingEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<CraftingEventPayload>() & 7);
            Assert.AreEqual(44, (int)Marshal.OffsetOf<CraftingEventPayload>(nameof(CraftingEventPayload.ReferenceSlot)));
            Assert.AreEqual(48, (int)Marshal.OffsetOf<CraftingEventPayload>(nameof(CraftingEventPayload.EventType)));
            Assert.AreEqual(50, (int)Marshal.OffsetOf<CraftingEventPayload>(nameof(CraftingEventPayload.Reserved)));
        }

        [Test]
        public void CraftOutputSynthesizedEnqueuesAndResolvesItemSidecar()
        {
            InvokeResetStaticState();
            ItemData item = null;
            try
            {
                item = CreateItemData("CraftingEventsBackpressureEditTests.ValidOutput");
                RecordingCraftingEventListener recorder = new RecordingCraftingEventListener();
                CraftingEvents.Register(recorder);

                Assert.IsTrue(CraftingEvents.TryRaiseCraftOutputSynthesized(new CraftedItemSynthesisEvent(
                    item,
                    3,
                    Vector3.one,
                    Vector3.up)));
                Assert.AreEqual(1, CraftingEvents.PendingCount);
                Assert.AreEqual(1, GetPrivateStaticInt("_referencePendingCount"));
                Assert.AreEqual(0, CraftingEvents.DroppedReferenceSlotCount);

                CraftingEvents.FlushPending();

                Assert.AreEqual(1, recorder.ReceivedCount);
                Assert.AreEqual((ushort)CraftingEventType.CraftOutputSynthesized, recorder.LastEventType);
                Assert.AreNotEqual(0, recorder.LastPayload.Reserved);
                Assert.AreSame(item, recorder.LastItem);
                Assert.AreEqual(3, recorder.LastQuantity);
                Assert.AreEqual(0, CraftingEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                AssertNoOccupiedReferenceSlots();
            }
            finally
            {
                if (item != null)
                    UnityEngine.Object.DestroyImmediate(item);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void ReleasedPayloadDoesNotResolveAfterReferenceSlotReuse()
        {
            InvokeResetStaticState();
            ItemData first = null;
            ItemData second = null;
            try
            {
                first = CreateItemData("CraftingEventsBackpressureEditTests.FirstOutput");
                second = CreateItemData("CraftingEventsBackpressureEditTests.SecondOutput");
                RecordingCraftingEventListener recorder = new RecordingCraftingEventListener();
                CraftingEvents.Register(recorder);

                Assert.IsTrue(CraftingEvents.TryRaiseCraftOutputSynthesized(new CraftedItemSynthesisEvent(
                    first,
                    1,
                    Vector3.zero,
                    Vector3.zero)));
                CraftingEvents.FlushPending();

                CraftingEventPayload stalePayload = recorder.LastPayload;
                int staleSlot = stalePayload.ReferenceSlot;
                Assert.AreNotEqual(0, stalePayload.Reserved);
                Assert.IsFalse(CraftingEvents.TryResolveItem(in stalePayload, out ItemData releasedItem));
                Assert.IsNull(releasedItem);

                SetPrivateStaticInt("_referenceWriteIndex", staleSlot);
                Assert.IsTrue(CraftingEvents.TryRaiseCraftOutputSynthesized(new CraftedItemSynthesisEvent(
                    second,
                    2,
                    Vector3.zero,
                    Vector3.zero)));

                Assert.IsFalse(CraftingEvents.TryResolveItem(in stalePayload, out ItemData reusedItem));
                Assert.IsNull(reusedItem);

                CraftingEvents.FlushPending();

                Assert.AreSame(second, recorder.LastItem);
                Assert.AreEqual(2, recorder.LastQuantity);
                Assert.AreEqual(staleSlot, recorder.LastPayload.ReferenceSlot);
                Assert.AreNotEqual(stalePayload.Reserved, recorder.LastPayload.Reserved);
                Assert.AreEqual(0, CraftingEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                AssertNoOccupiedReferenceSlots();
            }
            finally
            {
                if (first != null)
                    UnityEngine.Object.DestroyImmediate(first);

                if (second != null)
                    UnityEngine.Object.DestroyImmediate(second);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void FlushPendingDrainsReferenceSlotsWhenNoListenersRegistered()
        {
            InvokeResetStaticState();
            ItemData item = null;
            try
            {
                item = CreateItemData("CraftingEventsBackpressureEditTests.NoListenerOutput");

                Assert.IsTrue(CraftingEvents.TryRaiseCraftOutputSynthesized(new CraftedItemSynthesisEvent(
                    item,
                    1,
                    Vector3.zero,
                    Vector3.zero)));
                Assert.AreEqual(1, CraftingEvents.PendingCount);
                Assert.AreEqual(1, GetPrivateStaticInt("_referencePendingCount"));

                CraftingEvents.FlushPending();

                Assert.AreEqual(0, CraftingEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                AssertNoOccupiedReferenceSlots();
            }
            finally
            {
                if (item != null)
                    UnityEngine.Object.DestroyImmediate(item);

                InvokeResetStaticState();
            }
        }

        private sealed class RecordingCraftingEventListener : ICraftingEventListener
        {
            public int ReceivedCount;
            public ushort LastEventType;
            public ItemData LastItem;
            public int LastQuantity;
            public CraftingEventPayload LastPayload;

            public void OnCraftingEvent(in CraftingEventPayload payload)
            {
                ReceivedCount++;
                LastEventType = payload.EventType;
                LastQuantity = payload.Quantity;
                LastPayload = payload;
                CraftingEvents.TryResolveItem(in payload, out LastItem);
            }
        }

        private static ItemData CreateItemData(string stableId)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = stableId;
            SetPrivateInstanceField(item, "stableId", stableId);
            InvokePrivateInstanceMethod(item, "RefreshPersistentHash");
            return item;
        }

        private static int GetPrivateStaticInt(string fieldName)
        {
            FieldInfo field = typeof(CraftingEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing CraftingEvents field: " + fieldName);
            return (int)field.GetValue(null);
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(CraftingEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing CraftingEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static bool[] GetPrivateStaticBoolArray(string fieldName)
        {
            FieldInfo field = typeof(CraftingEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing CraftingEvents field: " + fieldName);
            return (bool[])field.GetValue(null);
        }

        private static void AssertNoOccupiedReferenceSlots()
        {
            bool[] occupied = GetPrivateStaticBoolArray("_referenceSlotOccupied");
            for (int i = 0; i < occupied.Length; i++)
                Assert.IsFalse(occupied[i], "Reference slot remained occupied: " + i);
        }

        private static void SetPrivateInstanceField<TValue>(object target, string fieldName, TValue value)
        {
            Assert.IsNotNull(target);
            FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, fieldName);
            field.SetValue(target, value);
        }

        private static void InvokePrivateInstanceMethod(object target, string methodName)
        {
            Assert.IsNotNull(target);
            MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(method, methodName);
            method.Invoke(target, null);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(CraftingEvents).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing CraftingEvents.ResetStaticState");
            reset.Invoke(null, null);
        }
    }
}
