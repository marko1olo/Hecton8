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
    public sealed class PlayerExpressionEventsSidecarEditTests
    {
        [Test]
        public void PlayerExpressionPayloadLayoutKeepsReservedGenerationSlot()
        {
            StructLayoutAttribute layout = typeof(PlayerExpressionEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(8, UnsafeUtility.SizeOf<PlayerExpressionEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<PlayerExpressionEventPayload>() & 7);
            Assert.AreEqual(0, (int)Marshal.OffsetOf<PlayerExpressionEventPayload>(nameof(PlayerExpressionEventPayload.ReferenceSlot)));
            Assert.AreEqual(4, (int)Marshal.OffsetOf<PlayerExpressionEventPayload>(nameof(PlayerExpressionEventPayload.EventType)));
            Assert.AreEqual(6, (int)Marshal.OffsetOf<PlayerExpressionEventPayload>(nameof(PlayerExpressionEventPayload.Reserved)));
        }

        [Test]
        public void ResolvedProfilePayloadRequiresCurrentGenerationToken()
        {
            InvokeResetStaticState();
            PlayerExpressionProfile profile = null;
            try
            {
                profile = CreateProfile("PlayerExpressionEventsSidecarEditTests.Current");
                ReservedSlot reserved = ReserveReferenceSlot();
                SetReferenceSlotProfile(reserved.ReferenceSlot, profile);

                PlayerExpressionEventPayload currentPayload = CreatePayload(reserved);
                Assert.AreNotEqual(0, currentPayload.Reserved);
                Assert.IsTrue(PlayerExpressionEvents.TryResolveProfile(in currentPayload, out PlayerExpressionProfile resolved));
                Assert.AreSame(profile, resolved);

                PlayerExpressionEventPayload noGenerationPayload = currentPayload;
                noGenerationPayload.Reserved = 0;
                Assert.IsFalse(PlayerExpressionEvents.TryResolveProfile(in noGenerationPayload, out PlayerExpressionProfile missingGenerationProfile));
                Assert.IsNull(missingGenerationProfile);

                PlayerExpressionEventPayload wrongGenerationPayload = currentPayload;
                wrongGenerationPayload.Reserved = unchecked((ushort)(wrongGenerationPayload.Reserved + 1));
                if (wrongGenerationPayload.Reserved == 0)
                    wrongGenerationPayload.Reserved = 1;

                Assert.IsFalse(PlayerExpressionEvents.TryResolveProfile(in wrongGenerationPayload, out PlayerExpressionProfile staleProfile));
                Assert.IsNull(staleProfile);
            }
            finally
            {
                if (profile != null)
                    UnityEngine.Object.DestroyImmediate(profile);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void ReleasedPayloadDoesNotResolveAfterReferenceSlotReuse()
        {
            InvokeResetStaticState();
            PlayerExpressionProfile first = null;
            PlayerExpressionProfile second = null;
            try
            {
                first = CreateProfile("PlayerExpressionEventsSidecarEditTests.First");
                second = CreateProfile("PlayerExpressionEventsSidecarEditTests.Second");

                ReservedSlot firstSlot = ReserveReferenceSlot();
                SetReferenceSlotProfile(firstSlot.ReferenceSlot, first);
                PlayerExpressionEventPayload stalePayload = CreatePayload(firstSlot);
                Assert.IsTrue(PlayerExpressionEvents.TryResolveProfile(in stalePayload, out PlayerExpressionProfile resolvedFirst));
                Assert.AreSame(first, resolvedFirst);

                ReleaseReferenceSlot(firstSlot.ReferenceSlot);
                Assert.IsFalse(PlayerExpressionEvents.TryResolveProfile(in stalePayload, out PlayerExpressionProfile releasedProfile));
                Assert.IsNull(releasedProfile);

                SetPrivateStaticInt("_referenceWriteIndex", firstSlot.ReferenceSlot);
                ReservedSlot secondSlot = ReserveReferenceSlot();
                Assert.AreEqual(firstSlot.ReferenceSlot, secondSlot.ReferenceSlot);
                Assert.AreNotEqual(firstSlot.ReferenceGeneration, secondSlot.ReferenceGeneration);
                SetReferenceSlotProfile(secondSlot.ReferenceSlot, second);

                Assert.IsFalse(PlayerExpressionEvents.TryResolveProfile(in stalePayload, out PlayerExpressionProfile reusedProfile));
                Assert.IsNull(reusedProfile);

                PlayerExpressionEventPayload currentPayload = CreatePayload(secondSlot);
                Assert.IsTrue(PlayerExpressionEvents.TryResolveProfile(in currentPayload, out PlayerExpressionProfile resolvedSecond));
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
        public void PlayerExpressionEventsSourceKeepsGenerationBridgeOnProducerResolverAndReleasePaths()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/Gameplay/PlayerExpressionManager.cs"));

            StringAssert.Contains("[FieldOffset(6)] public ushort Reserved;", source);
            StringAssert.Contains("private static readonly ushort[] _referenceSlotGenerations", source);
            StringAssert.Contains("private static bool TryReserveReferenceSlot(out int referenceSlot, out ushort referenceGeneration)", source);
            StringAssert.Contains("referenceGeneration = AdvanceReferenceSlotGeneration(referenceSlot)", source);
            StringAssert.Contains("Reserved = referenceGeneration", source);
            StringAssert.Contains("private static bool IsReferenceSlotPayloadCurrent(in PlayerExpressionEventPayload payload)", source);
            StringAssert.Contains("payload.Reserved != 0", source);
            StringAssert.Contains("_referenceSlotGenerations[referenceSlot] == payload.Reserved", source);
            StringAssert.Contains("private static void ReleaseReferenceSlotForPayload(in PlayerExpressionEventPayload payload)", source);
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

        private static PlayerExpressionProfile CreateProfile(string name)
        {
            PlayerExpressionProfile profile = ScriptableObject.CreateInstance<PlayerExpressionProfile>();
            profile.name = name;
            return profile;
        }

        private static PlayerExpressionEventPayload CreatePayload(in ReservedSlot reserved)
        {
            return new PlayerExpressionEventPayload
            {
                ReferenceSlot = reserved.ReferenceSlot,
                Reserved = reserved.ReferenceGeneration,
                EventType = (ushort)PlayerExpressionEventType.ProfileChanged
            };
        }

        private static ReservedSlot ReserveReferenceSlot()
        {
            MethodInfo method = typeof(PlayerExpressionEvents).GetMethod(
                "TryReserveReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing PlayerExpressionEvents.TryReserveReferenceSlot");

            object[] args = { -1, (ushort)0 };
            Assert.IsTrue((bool)method.Invoke(null, args));
            return new ReservedSlot((int)args[0], (ushort)args[1]);
        }

        private static void ReleaseReferenceSlot(int referenceSlot)
        {
            MethodInfo method = typeof(PlayerExpressionEvents).GetMethod(
                "ReleaseReferenceSlot",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(method, "Missing PlayerExpressionEvents.ReleaseReferenceSlot");
            method.Invoke(null, new object[] { referenceSlot });
        }

        private static void SetReferenceSlotProfile(int referenceSlot, PlayerExpressionProfile profile)
        {
            FieldInfo slotsField = typeof(PlayerExpressionEvents).GetField(
                "_referenceSlots",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(slotsField, "Missing PlayerExpressionEvents._referenceSlots");

            Array slots = (Array)slotsField.GetValue(null);
            object slot = slots.GetValue(referenceSlot);
            FieldInfo profileField = slot.GetType().GetField(
                "Profile",
                BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(profileField, "Missing PlayerExpressionReferenceSlot.Profile");
            profileField.SetValue(slot, profile);
            slots.SetValue(slot, referenceSlot);
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(PlayerExpressionEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing PlayerExpressionEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(PlayerExpressionEvents).GetMethod(
                "ResetStaticState",
                BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing PlayerExpressionEvents.ResetStaticState");
            reset.Invoke(null, null);
        }
    }
}
