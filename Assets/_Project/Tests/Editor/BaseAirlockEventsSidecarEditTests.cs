using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Gameplay;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class BaseAirlockEventsSidecarEditTests
    {
        [Test]
        public void BaseAirlockPayloadLayoutKeepsReservedGenerationSlot()
        {
            StructLayoutAttribute layout = typeof(BaseAirlockEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(32, UnsafeUtility.SizeOf<BaseAirlockEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<BaseAirlockEventPayload>() & 7);
            Assert.AreEqual(12, (int)Marshal.OffsetOf<BaseAirlockEventPayload>(nameof(BaseAirlockEventPayload.ReferenceSlot)));
            Assert.AreEqual(16, (int)Marshal.OffsetOf<BaseAirlockEventPayload>(nameof(BaseAirlockEventPayload.StatusFlags)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<BaseAirlockEventPayload>(nameof(BaseAirlockEventPayload.Reserved)));
            Assert.AreEqual(22, (int)Marshal.OffsetOf<BaseAirlockEventPayload>(nameof(BaseAirlockEventPayload.Reserved0)));
        }

        [Test]
        public void ResolvedInteractorPayloadRequiresCurrentGenerationToken()
        {
            InvokeResetStaticState();
            GameObject interactorOwner = null;
            try
            {
                interactorOwner = new GameObject("BaseAirlockEventsSidecarEditTests.CurrentInteractor");
                ReservedSlot reserved = ReserveReferenceSlot();
                SetReferenceSlotInteractor(reserved.ReferenceSlot, interactorOwner.transform);

                BaseAirlockEventPayload currentPayload = CreatePayload(reserved);
                Assert.AreNotEqual(0, currentPayload.Reserved);
                Assert.IsTrue(BaseAirlockEvents.TryResolveInteractor(in currentPayload, out Transform resolved));
                Assert.AreSame(interactorOwner.transform, resolved);

                BaseAirlockEventPayload noGenerationPayload = currentPayload;
                noGenerationPayload.Reserved = 0;
                Assert.IsFalse(BaseAirlockEvents.TryResolveInteractor(in noGenerationPayload, out Transform missingGenerationInteractor));
                Assert.IsNull(missingGenerationInteractor);

                BaseAirlockEventPayload wrongGenerationPayload = currentPayload;
                wrongGenerationPayload.Reserved = unchecked((ushort)(wrongGenerationPayload.Reserved + 1));
                if (wrongGenerationPayload.Reserved == 0)
                    wrongGenerationPayload.Reserved = 1;

                Assert.IsFalse(BaseAirlockEvents.TryResolveInteractor(in wrongGenerationPayload, out Transform staleInteractor));
                Assert.IsNull(staleInteractor);
            }
            finally
            {
                if (interactorOwner != null)
                    UnityEngine.Object.DestroyImmediate(interactorOwner);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void ReleasedPayloadDoesNotResolveAfterReferenceSlotReuse()
        {
            InvokeResetStaticState();
            GameObject firstOwner = null;
            GameObject secondOwner = null;
            try
            {
                firstOwner = new GameObject("BaseAirlockEventsSidecarEditTests.FirstInteractor");
                secondOwner = new GameObject("BaseAirlockEventsSidecarEditTests.SecondInteractor");

                ReservedSlot firstSlot = ReserveReferenceSlot();
                SetReferenceSlotInteractor(firstSlot.ReferenceSlot, firstOwner.transform);
                BaseAirlockEventPayload stalePayload = CreatePayload(firstSlot);
                Assert.IsTrue(BaseAirlockEvents.TryResolveInteractor(in stalePayload, out Transform resolvedFirst));
                Assert.AreSame(firstOwner.transform, resolvedFirst);

                ReleaseReferenceSlot(firstSlot.ReferenceSlot);
                Assert.IsFalse(BaseAirlockEvents.TryResolveInteractor(in stalePayload, out Transform releasedInteractor));
                Assert.IsNull(releasedInteractor);

                SetPrivateStaticInt("_referenceWriteIndex", firstSlot.ReferenceSlot);
                ReservedSlot secondSlot = ReserveReferenceSlot();
                Assert.AreEqual(firstSlot.ReferenceSlot, secondSlot.ReferenceSlot);
                Assert.AreNotEqual(firstSlot.ReferenceGeneration, secondSlot.ReferenceGeneration);
                SetReferenceSlotInteractor(secondSlot.ReferenceSlot, secondOwner.transform);

                Assert.IsFalse(BaseAirlockEvents.TryResolveInteractor(in stalePayload, out Transform reusedInteractor));
                Assert.IsNull(reusedInteractor);

                BaseAirlockEventPayload currentPayload = CreatePayload(secondSlot);
                Assert.IsTrue(BaseAirlockEvents.TryResolveInteractor(in currentPayload, out Transform resolvedSecond));
                Assert.AreSame(secondOwner.transform, resolvedSecond);
            }
            finally
            {
                if (firstOwner != null)
                    UnityEngine.Object.DestroyImmediate(firstOwner);

                if (secondOwner != null)
                    UnityEngine.Object.DestroyImmediate(secondOwner);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void BaseAirlockEventsSourceKeepsGenerationBridgeOnProducerResolverAndReleasePaths()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/BaseAirlockEvents.cs"));

            StringAssert.Contains("[FieldOffset(20)] public ushort Reserved;", source);
            StringAssert.Contains("private static readonly ushort[] _referenceSlotGenerations", source);
            StringAssert.Contains("private static bool TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration)", source);
            StringAssert.Contains("referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot)", source);
            StringAssert.Contains("Reserved = referenceGeneration", source);
            StringAssert.Contains("private static bool IsReferenceSlotPayloadCurrent(in BaseAirlockEventPayload payload)", source);
            StringAssert.Contains("payload.Reserved != 0", source);
            StringAssert.Contains("_referenceSlotGenerations[referenceSlot] == payload.Reserved", source);
            StringAssert.Contains("private static void ReleaseReferenceSlotForPayload(in BaseAirlockEventPayload payload)", source);
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

        private static BaseAirlockEventPayload CreatePayload(in ReservedSlot reserved)
        {
            return new BaseAirlockEventPayload
            {
                ReferenceSlot = reserved.ReferenceSlot,
                Reserved = reserved.ReferenceGeneration,
                StatusFlags = BaseAirlockEventPayload.BuildStatusFlags(
                    BaseAirlockEventType.EnvironmentChanged,
                    isDry: false,
                    lockedDown: false,
                    overrideBlocked: false)
            };
        }

        private static ReservedSlot ReserveReferenceSlot()
        {
            MethodInfo method = typeof(BaseAirlockEvents).GetMethod(
                "TryReserveReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing BaseAirlockEvents.TryReserveReferenceSlot");

            object[] args = { -1, (ushort)0 };
            Assert.IsTrue((bool)method.Invoke(null, args));
            return new ReservedSlot((int)args[0], (ushort)args[1]);
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            MethodInfo method = typeof(BaseAirlockEvents).GetMethod(
                "ReleaseReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing BaseAirlockEvents.ReleaseReferenceSlot");
            method.Invoke(null, new object[] { referenceSlot });
        }

        private static void SetReferenceSlotInteractor(int referenceSlot, Transform interactor)
        {
            FieldInfo slotsField = typeof(BaseAirlockEvents).GetField(
                "_referenceSlots",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(slotsField, "Missing BaseAirlockEvents._referenceSlots");

            Array slots = (Array)slotsField.GetValue(null);
            object slot = slots.GetValue(referenceSlot);
            FieldInfo interactorField = slot.GetType().GetField(
                "Interactor",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(interactorField, "Missing AirlockReferenceSlot.Interactor");
            interactorField.SetValue(slot, interactor);
            slots.SetValue(slot, referenceSlot);
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(BaseAirlockEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing BaseAirlockEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(BaseAirlockEvents).GetMethod(
                "ResetStaticState",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing BaseAirlockEvents.ResetStaticState");
            reset.Invoke(null, null);
        }
    }
}
