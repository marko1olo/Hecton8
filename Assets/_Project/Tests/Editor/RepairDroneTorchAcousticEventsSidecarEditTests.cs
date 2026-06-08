using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Construction;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class RepairDroneTorchAcousticEventsSidecarEditTests
    {
        [Test]
        public void RepairDroneTorchAcousticPayloadLayoutKeepsReservedGenerationSlot()
        {
            StructLayoutAttribute layout = typeof(RepairDroneTorchAcousticPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(32, UnsafeUtility.SizeOf<RepairDroneTorchAcousticPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<RepairDroneTorchAcousticPayload>() & 7);
            Assert.AreEqual(24, (int)Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.ReferenceSlot)));
            Assert.AreEqual(28, (int)Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.EventType)));
            Assert.AreEqual(30, (int)Marshal.OffsetOf<RepairDroneTorchAcousticPayload>(nameof(RepairDroneTorchAcousticPayload.Reserved)));
        }

        [Test]
        public void ClipPayloadRequiresCurrentGenerationToken()
        {
            InvokeResetStaticState();
            AudioClip clip = null;
            try
            {
                clip = CreateClip("RepairDroneTorchAcousticEventsSidecarEditTests.Current");
                ReservedSlot reserved = ReserveReferenceSlot();
                SetReferenceSlotClip(reserved.ReferenceSlot, clip);

                RepairDroneTorchAcousticPayload currentPayload = CreatePayload(reserved);
                Assert.AreNotEqual(0, currentPayload.Reserved);
                Assert.IsTrue(IsReferenceSlotPayloadCurrent(in currentPayload));

                RepairDroneTorchAcousticPayload noGenerationPayload = currentPayload;
                noGenerationPayload.Reserved = 0;
                Assert.IsFalse(IsReferenceSlotPayloadCurrent(in noGenerationPayload));

                RepairDroneTorchAcousticPayload wrongGenerationPayload = currentPayload;
                wrongGenerationPayload.Reserved = unchecked((ushort)(wrongGenerationPayload.Reserved + 1));
                if (wrongGenerationPayload.Reserved == 0)
                    wrongGenerationPayload.Reserved = 1;

                Assert.IsFalse(IsReferenceSlotPayloadCurrent(in wrongGenerationPayload));
            }
            finally
            {
                if (clip != null)
                    UnityEngine.Object.DestroyImmediate(clip);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void ReleasedPayloadIsRejectedAfterReferenceSlotReuse()
        {
            InvokeResetStaticState();
            AudioClip first = null;
            AudioClip second = null;
            try
            {
                first = CreateClip("RepairDroneTorchAcousticEventsSidecarEditTests.First");
                second = CreateClip("RepairDroneTorchAcousticEventsSidecarEditTests.Second");

                ReservedSlot firstSlot = ReserveReferenceSlot();
                SetReferenceSlotClip(firstSlot.ReferenceSlot, first);
                RepairDroneTorchAcousticPayload stalePayload = CreatePayload(firstSlot);
                Assert.IsTrue(IsReferenceSlotPayloadCurrent(in stalePayload));

                ReleaseReferenceSlot(firstSlot.ReferenceSlot);
                Assert.IsFalse(IsReferenceSlotPayloadCurrent(in stalePayload));

                SetPrivateStaticInt("_referenceWriteIndex", firstSlot.ReferenceSlot);
                ReservedSlot secondSlot = ReserveReferenceSlot();
                Assert.AreEqual(firstSlot.ReferenceSlot, secondSlot.ReferenceSlot);
                Assert.AreNotEqual(firstSlot.ReferenceGeneration, secondSlot.ReferenceGeneration);
                SetReferenceSlotClip(secondSlot.ReferenceSlot, second);

                Assert.IsFalse(IsReferenceSlotPayloadCurrent(in stalePayload));
                RepairDroneTorchAcousticPayload currentPayload = CreatePayload(secondSlot);
                Assert.IsTrue(IsReferenceSlotPayloadCurrent(in currentPayload));
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
        public void RepairDroneTorchAcousticEventsSourceKeepsGenerationBridgeOnProducerDispatchAndReleasePaths()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Construction/RepairDroneEntity.cs"));

            StringAssert.Contains("[FieldOffset(30)]", source);
            StringAssert.Contains("public ushort Reserved;", source);
            StringAssert.Contains("private static readonly ushort[] _referenceSlotGenerations", source);
            StringAssert.Contains("private static bool TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration)", source);
            StringAssert.Contains("referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot)", source);
            StringAssert.Contains("payload.Reserved = referenceGeneration", source);
            StringAssert.Contains("private static bool IsReferenceSlotPayloadCurrent(in RepairDroneTorchAcousticPayload payload)", source);
            StringAssert.Contains("payload.Reserved != 0", source);
            StringAssert.Contains("_referenceSlotGenerations[referenceSlot] == payload.Reserved", source);
            StringAssert.Contains("!IsReferenceSlotPayloadCurrent(in payload)", source);
            StringAssert.Contains("private static void ReleaseReferenceSlotForPayload(in RepairDroneTorchAcousticPayload payload)", source);
            StringAssert.Contains("ReleaseReferenceSlotForPayload(in payload);", source);
            StringAssert.Contains("AdvanceReferenceSlotGeneration(i);", source);
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

        private static AudioClip CreateClip(string name)
        {
            return AudioClip.Create(name, 8, 1, 8000, false);
        }

        private static RepairDroneTorchAcousticPayload CreatePayload(in ReservedSlot reserved)
        {
            return new RepairDroneTorchAcousticPayload
            {
                ReferenceSlot = reserved.ReferenceSlot,
                Reserved = reserved.ReferenceGeneration,
                EventType = 1
            };
        }

        private static ReservedSlot ReserveReferenceSlot()
        {
            MethodInfo method = typeof(RepairDroneTorchAcousticEvents).GetMethod(
                "TryReserveReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing RepairDroneTorchAcousticEvents.TryReserveReferenceSlot");

            object[] args = { -1, (ushort)0 };
            Assert.IsTrue((bool)method.Invoke(null, args));
            return new ReservedSlot((int)args[0], (ushort)args[1]);
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            MethodInfo method = typeof(RepairDroneTorchAcousticEvents).GetMethod(
                "ReleaseReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing RepairDroneTorchAcousticEvents.ReleaseReferenceSlot");
            method.Invoke(null, new object[] { referenceSlot });
        }

        private static bool IsReferenceSlotPayloadCurrent(in RepairDroneTorchAcousticPayload payload)
        {
            MethodInfo method = typeof(RepairDroneTorchAcousticEvents).GetMethod(
                "IsReferenceSlotPayloadCurrent",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing RepairDroneTorchAcousticEvents.IsReferenceSlotPayloadCurrent");
            return (bool)method.Invoke(null, new object[] { payload });
        }

        private static void SetReferenceSlotClip(int referenceSlot, AudioClip clip)
        {
            FieldInfo field = typeof(RepairDroneTorchAcousticEvents).GetField(
                "_clipReferenceSlots",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing RepairDroneTorchAcousticEvents._clipReferenceSlots");
            AudioClip[] slots = (AudioClip[])field.GetValue(null);
            slots[referenceSlot] = clip;
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(RepairDroneTorchAcousticEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing RepairDroneTorchAcousticEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(RepairDroneTorchAcousticEvents).GetMethod(
                "ResetStaticState",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing RepairDroneTorchAcousticEvents.ResetStaticState");
            reset.Invoke(null, null);
        }
    }
}
