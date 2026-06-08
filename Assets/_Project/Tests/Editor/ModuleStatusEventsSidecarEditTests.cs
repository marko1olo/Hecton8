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
    public sealed class ModuleStatusEventsSidecarEditTests
    {
        [Test]
        public void ModuleStatusPayloadLayoutKeepsReservedGenerationSlot()
        {
            StructLayoutAttribute layout = typeof(ModuleStatusEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(64, UnsafeUtility.SizeOf<ModuleStatusEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<ModuleStatusEventPayload>() & 7);
            Assert.AreEqual(12, (int)Marshal.OffsetOf<ModuleStatusEventPayload>(nameof(ModuleStatusEventPayload.ReferenceSlot)));
            Assert.AreEqual(32, (int)Marshal.OffsetOf<ModuleStatusEventPayload>(nameof(ModuleStatusEventPayload.EventType)));
            Assert.AreEqual(34, (int)Marshal.OffsetOf<ModuleStatusEventPayload>(nameof(ModuleStatusEventPayload.Reserved)));
        }

        [Test]
        public void ResolvedModulePayloadRequiresCurrentGenerationToken()
        {
            InvokeResetStaticState();
            GameObject owner = null;
            try
            {
                BaseModule module = CreateModule("ModuleStatusEventsSidecarEditTests.Current");
                owner = module.gameObject;

                ReservedSlot reserved = ReserveReferenceSlot();
                SetReferenceSlotModule(reserved.ReferenceSlot, module);

                ModuleStatusEventPayload currentPayload = CreatePayload(reserved);
                Assert.AreNotEqual(0, currentPayload.Reserved);
                Assert.IsTrue(ModuleStatusEvents.TryResolveModule(in currentPayload, out BaseModule resolved));
                Assert.AreSame(module, resolved);

                ModuleStatusEventPayload noGenerationPayload = currentPayload;
                noGenerationPayload.Reserved = 0;
                Assert.IsFalse(ModuleStatusEvents.TryResolveModule(in noGenerationPayload, out BaseModule missingGenerationModule));
                Assert.IsNull(missingGenerationModule);

                ModuleStatusEventPayload wrongGenerationPayload = currentPayload;
                wrongGenerationPayload.Reserved = unchecked((ushort)(wrongGenerationPayload.Reserved + 1));
                if (wrongGenerationPayload.Reserved == 0)
                    wrongGenerationPayload.Reserved = 1;

                Assert.IsFalse(ModuleStatusEvents.TryResolveModule(in wrongGenerationPayload, out BaseModule staleModule));
                Assert.IsNull(staleModule);
            }
            finally
            {
                if (owner != null)
                    UnityEngine.Object.DestroyImmediate(owner);

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
                BaseModule first = CreateModule("ModuleStatusEventsSidecarEditTests.First");
                BaseModule second = CreateModule("ModuleStatusEventsSidecarEditTests.Second");
                firstOwner = first.gameObject;
                secondOwner = second.gameObject;

                ReservedSlot firstSlot = ReserveReferenceSlot();
                SetReferenceSlotModule(firstSlot.ReferenceSlot, first);
                ModuleStatusEventPayload stalePayload = CreatePayload(firstSlot);
                Assert.IsTrue(ModuleStatusEvents.TryResolveModule(in stalePayload, out BaseModule resolvedFirst));
                Assert.AreSame(first, resolvedFirst);

                ReleaseReferenceSlot(firstSlot.ReferenceSlot);
                Assert.IsFalse(ModuleStatusEvents.TryResolveModule(in stalePayload, out BaseModule releasedModule));
                Assert.IsNull(releasedModule);

                SetPrivateStaticInt("_referenceWriteIndex", firstSlot.ReferenceSlot);
                ReservedSlot secondSlot = ReserveReferenceSlot();
                Assert.AreEqual(firstSlot.ReferenceSlot, secondSlot.ReferenceSlot);
                Assert.AreNotEqual(firstSlot.ReferenceGeneration, secondSlot.ReferenceGeneration);
                SetReferenceSlotModule(secondSlot.ReferenceSlot, second);

                Assert.IsFalse(ModuleStatusEvents.TryResolveModule(in stalePayload, out BaseModule reusedModule));
                Assert.IsNull(reusedModule);

                ModuleStatusEventPayload currentPayload = CreatePayload(secondSlot);
                Assert.IsTrue(ModuleStatusEvents.TryResolveModule(in currentPayload, out BaseModule resolvedSecond));
                Assert.AreSame(second, resolvedSecond);
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
        public void ModuleStatusEventsSourceKeepsGenerationBridgeOnProducerResolverAndReleasePaths()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/ModuleStatusEvents.cs"));

            StringAssert.Contains("private static readonly ushort[] _referenceSlotGenerations", source);
            StringAssert.Contains("private static bool TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration)", source);
            StringAssert.Contains("referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot)", source);
            StringAssert.Contains("Reserved = referenceGeneration", source);
            StringAssert.Contains("private static bool IsReferenceSlotPayloadCurrent(in ModuleStatusEventPayload payload)", source);
            StringAssert.Contains("payload.Reserved != 0", source);
            StringAssert.Contains("_referenceSlotGenerations[referenceSlot] == payload.Reserved", source);
            StringAssert.Contains("private static void ReleaseReferenceSlotForPayload(in ModuleStatusEventPayload payload)", source);
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

        private static BaseModule CreateModule(string name)
        {
            GameObject owner = new GameObject(name);
            return owner.AddComponent<BaseModule>();
        }

        private static ModuleStatusEventPayload CreatePayload(in ReservedSlot reserved)
        {
            return new ModuleStatusEventPayload
            {
                ReferenceSlot = reserved.ReferenceSlot,
                Reserved = reserved.ReferenceGeneration,
                EventType = (ushort)ModuleStatusEventType.Enter
            };
        }

        private static ReservedSlot ReserveReferenceSlot()
        {
            MethodInfo method = typeof(ModuleStatusEvents).GetMethod(
                "TryReserveReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing ModuleStatusEvents.TryReserveReferenceSlot");

            object[] args = { -1, (ushort)0 };
            Assert.IsTrue((bool)method.Invoke(null, args));
            return new ReservedSlot((int)args[0], (ushort)args[1]);
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            MethodInfo method = typeof(ModuleStatusEvents).GetMethod(
                "ReleaseReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing ModuleStatusEvents.ReleaseReferenceSlot");
            method.Invoke(null, new object[] { referenceSlot });
        }

        private static void SetReferenceSlotModule(int referenceSlot, BaseModule module)
        {
            FieldInfo slotsField = typeof(ModuleStatusEvents).GetField(
                "_referenceSlots",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(slotsField, "Missing ModuleStatusEvents._referenceSlots");

            Array slots = (Array)slotsField.GetValue(null);
            object slot = slots.GetValue(referenceSlot);
            FieldInfo moduleField = slot.GetType().GetField(
                "Module",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(moduleField, "Missing ModuleReferenceSlot.Module");
            moduleField.SetValue(slot, module);
            slots.SetValue(slot, referenceSlot);
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(ModuleStatusEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing ModuleStatusEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(ModuleStatusEvents).GetMethod(
                "ResetStaticState",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing ModuleStatusEvents.ResetStaticState");
            reset.Invoke(null, null);
        }
    }
}
