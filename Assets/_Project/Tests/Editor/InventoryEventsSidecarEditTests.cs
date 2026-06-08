using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Inventory;
using Hecton8.Items;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class InventoryEventsSidecarEditTests
    {
        [Test]
        public void InventoryPayloadLayoutKeepsReservedGenerationSlot()
        {
            StructLayoutAttribute layout = typeof(InventoryEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(32, UnsafeUtility.SizeOf<InventoryEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<InventoryEventPayload>() & 7);
            Assert.AreEqual(16, (int)Marshal.OffsetOf<InventoryEventPayload>(nameof(InventoryEventPayload.ReferenceSlot)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<InventoryEventPayload>(nameof(InventoryEventPayload.EventType)));
            Assert.AreEqual(22, (int)Marshal.OffsetOf<InventoryEventPayload>(nameof(InventoryEventPayload.Reserved)));
        }

        [Test]
        public void ResolvedItemPayloadRequiresCurrentGenerationToken()
        {
            InvokeResetStaticState();
            ItemData item = null;
            try
            {
                item = CreateItemData("InventoryEventsSidecarEditTests.Current");
                ReservedSlot reserved = ReserveReferenceSlot();
                SetReferenceSlotItem(reserved.ReferenceSlot, item);

                InventoryEventPayload currentPayload = CreatePayload(reserved);
                Assert.AreNotEqual(0, currentPayload.Reserved);
                Assert.IsTrue(InventoryEvents.TryResolveItem(in currentPayload, out ItemData resolved));
                Assert.AreSame(item, resolved);

                InventoryEventPayload noGenerationPayload = currentPayload;
                noGenerationPayload.Reserved = 0;
                Assert.IsFalse(InventoryEvents.TryResolveItem(in noGenerationPayload, out ItemData missingGenerationItem));
                Assert.IsNull(missingGenerationItem);

                InventoryEventPayload wrongGenerationPayload = currentPayload;
                wrongGenerationPayload.Reserved = unchecked((ushort)(wrongGenerationPayload.Reserved + 1));
                if (wrongGenerationPayload.Reserved == 0)
                    wrongGenerationPayload.Reserved = 1;

                Assert.IsFalse(InventoryEvents.TryResolveItem(in wrongGenerationPayload, out ItemData staleItem));
                Assert.IsNull(staleItem);
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
                first = CreateItemData("InventoryEventsSidecarEditTests.First");
                second = CreateItemData("InventoryEventsSidecarEditTests.Second");

                ReservedSlot firstSlot = ReserveReferenceSlot();
                SetReferenceSlotItem(firstSlot.ReferenceSlot, first);
                InventoryEventPayload stalePayload = CreatePayload(firstSlot);
                Assert.IsTrue(InventoryEvents.TryResolveItem(in stalePayload, out ItemData resolvedFirst));
                Assert.AreSame(first, resolvedFirst);

                ReleaseReferenceSlot(firstSlot.ReferenceSlot);
                Assert.IsFalse(InventoryEvents.TryResolveItem(in stalePayload, out ItemData releasedItem));
                Assert.IsNull(releasedItem);

                SetPrivateStaticInt("_referenceWriteIndex", firstSlot.ReferenceSlot);
                ReservedSlot secondSlot = ReserveReferenceSlot();
                Assert.AreEqual(firstSlot.ReferenceSlot, secondSlot.ReferenceSlot);
                Assert.AreNotEqual(firstSlot.ReferenceGeneration, secondSlot.ReferenceGeneration);
                SetReferenceSlotItem(secondSlot.ReferenceSlot, second);

                Assert.IsFalse(InventoryEvents.TryResolveItem(in stalePayload, out ItemData reusedItem));
                Assert.IsNull(reusedItem);

                InventoryEventPayload currentPayload = CreatePayload(secondSlot);
                Assert.IsTrue(InventoryEvents.TryResolveItem(in currentPayload, out ItemData resolvedSecond));
                Assert.AreSame(second, resolvedSecond);
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
        public void InventoryEventsSourceKeepsGenerationBridgeOnProducerResolverAndReleasePaths()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/InventoryEvents.cs"));

            StringAssert.Contains("[FieldOffset(22)] public ushort Reserved;", source);
            StringAssert.Contains("private static readonly ushort[] _referenceSlotGenerations", source);
            StringAssert.Contains("private static bool TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration)", source);
            StringAssert.Contains("referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot)", source);
            StringAssert.Contains("Reserved = referenceGeneration", source);
            StringAssert.Contains("private static bool IsReferenceSlotPayloadCurrent(in InventoryEventPayload payload)", source);
            StringAssert.Contains("payload.Reserved != 0", source);
            StringAssert.Contains("_referenceSlotGenerations[referenceSlot] == payload.Reserved", source);
            StringAssert.Contains("private static void ReleaseReferenceSlotForPayload(in InventoryEventPayload payload)", source);
            StringAssert.Contains("ReleaseReferenceSlotForPayload(in payload);", source);
        }

        private readonly struct ReservedSlot
        {
            public ReservedSlot(int referenceSlot, ushort referenceGeneration)
            {
                ReferenceSlot = referenceSlot;
                ReferenceGeneration = referenceGeneration;
            }

            public int ReferenceSlot { get; }
            public ushort ReferenceGeneration { get; }
        }

        private static ItemData CreateItemData(string name)
        {
            ItemData item = ScriptableObject.CreateInstance<ItemData>();
            item.name = name;
            return item;
        }

        private static InventoryEventPayload CreatePayload(in ReservedSlot reserved)
        {
            return new InventoryEventPayload
            {
                ReferenceSlot = reserved.ReferenceSlot,
                Reserved = reserved.ReferenceGeneration,
                EventType = (ushort)InventoryEventType.InventoryFull
            };
        }

        private static ReservedSlot ReserveReferenceSlot()
        {
            MethodInfo method = typeof(InventoryEvents).GetMethod(
                "TryReserveReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing InventoryEvents.TryReserveReferenceSlot");

            object[] args = { -1, (ushort)0 };
            Assert.IsTrue((bool)method.Invoke(null, args));
            return new ReservedSlot((int)args[0], (ushort)args[1]);
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            MethodInfo method = typeof(InventoryEvents).GetMethod(
                "ReleaseReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing InventoryEvents.ReleaseReferenceSlot");
            method.Invoke(null, new object[] { referenceSlot });
        }

        private static void SetReferenceSlotItem(int referenceSlot, ItemData item)
        {
            FieldInfo slotsField = typeof(InventoryEvents).GetField(
                "_referenceSlots",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(slotsField, "Missing InventoryEvents._referenceSlots");

            Array slots = (Array)slotsField.GetValue(null);
            object slot = slots.GetValue(referenceSlot);
            FieldInfo itemField = slot.GetType().GetField(
                "Item",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(itemField, "Missing InventoryReferenceSlot.Item");
            itemField.SetValue(slot, item);
            slots.SetValue(slot, referenceSlot);
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(InventoryEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing InventoryEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(InventoryEvents).GetMethod(
                "ResetStaticState",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing InventoryEvents.ResetStaticState");
            reset.Invoke(null, null);
        }
    }
}
