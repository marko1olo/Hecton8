using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Hecton8.Narrative;
using NUnit.Framework;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace Hecton8.Tests.Editor
{
    public sealed class AudioLogEventsSidecarEditTests
    {
        [Test]
        public void AudioLogPayloadLayoutKeepsReservedGenerationSlot()
        {
            StructLayoutAttribute layout = typeof(AudioLogEventPayload).StructLayoutAttribute;
            Assert.IsNotNull(layout);
            Assert.AreEqual(LayoutKind.Explicit, layout.Value);
            Assert.AreEqual(32, UnsafeUtility.SizeOf<AudioLogEventPayload>());
            Assert.AreEqual(0, UnsafeUtility.SizeOf<AudioLogEventPayload>() & 7);
            Assert.AreEqual(12, (int)Marshal.OffsetOf<AudioLogEventPayload>(nameof(AudioLogEventPayload.ReferenceSlot)));
            Assert.AreEqual(20, (int)Marshal.OffsetOf<AudioLogEventPayload>(nameof(AudioLogEventPayload.Type)));
            Assert.AreEqual(22, (int)Marshal.OffsetOf<AudioLogEventPayload>(nameof(AudioLogEventPayload.Reserved)));
        }

        [Test]
        public void LogDiscoveredEnqueuesAndResolvesAudioLogSidecar()
        {
            InvokeResetStaticState();
            AudioLogData data = null;
            try
            {
                data = CreateAudioLogData("AudioLogEventsSidecarEditTests.Valid");
                RecordingAudioLogEventListener recorder = new RecordingAudioLogEventListener();
                AudioLogEvents.Register(recorder);

                Assert.IsTrue(AudioLogEvents.TryRaiseLogDiscovered(0xA11D1001u, data));
                Assert.AreEqual(1, AudioLogEvents.PendingCount);
                Assert.AreEqual(1, GetPrivateStaticInt("_referencePendingCount"));
                Assert.AreEqual(0, AudioLogEvents.DroppedReferenceSlotCount);

                AudioLogEvents.FlushPending();

                Assert.AreEqual(1, recorder.ReceivedCount);
                Assert.AreEqual(AudioLogEventType.Discovered, recorder.LastType);
                Assert.AreNotEqual(0, recorder.LastPayload.Reserved);
                Assert.AreSame(data, recorder.LastData);
                Assert.AreEqual(0, AudioLogEvents.PendingCount);
                Assert.AreEqual(0, GetPrivateStaticInt("_referencePendingCount"));
                AssertNoOccupiedReferenceSlots();
            }
            finally
            {
                if (data != null)
                    UnityEngine.Object.DestroyImmediate(data);

                InvokeResetStaticState();
            }
        }

        [Test]
        public void ReleasedPayloadDoesNotResolveAfterReferenceSlotReuse()
        {
            InvokeResetStaticState();
            AudioLogData first = null;
            AudioLogData second = null;
            try
            {
                first = CreateAudioLogData("AudioLogEventsSidecarEditTests.First");
                second = CreateAudioLogData("AudioLogEventsSidecarEditTests.Second");
                RecordingAudioLogEventListener recorder = new RecordingAudioLogEventListener();
                AudioLogEvents.Register(recorder);

                Assert.IsTrue(AudioLogEvents.TryRaiseLogDiscovered(0xA11D2001u, first));
                AudioLogEvents.FlushPending();

                AudioLogEventPayload stalePayload = recorder.LastPayload;
                int staleSlot = stalePayload.ReferenceSlot;
                Assert.AreNotEqual(0, stalePayload.Reserved);
                Assert.IsFalse(AudioLogEvents.TryResolveLogData(in stalePayload, out AudioLogData releasedData));
                Assert.IsNull(releasedData);

                SetPrivateStaticInt("_referenceWriteIndex", staleSlot);
                Assert.IsTrue(AudioLogEvents.TryRaiseLogDiscovered(0xA11D2002u, second));
                Assert.IsFalse(AudioLogEvents.TryResolveLogData(in stalePayload, out AudioLogData reusedData));
                Assert.IsNull(reusedData);

                AudioLogEvents.FlushPending();

                Assert.AreSame(second, recorder.LastData);
                Assert.AreEqual(staleSlot, recorder.LastPayload.ReferenceSlot);
                Assert.AreNotEqual(stalePayload.Reserved, recorder.LastPayload.Reserved);
                Assert.AreEqual(0, AudioLogEvents.PendingCount);
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
        public void AudioLogEventsSourceKeepsGenerationBridgeOnProducerResolverAndReleasePaths()
        {
            string source = File.ReadAllText(Path.Combine(
                Directory.GetCurrentDirectory(),
                "Assets/_Project/Scripts/AudioLog/AudioLogEvents.cs"));

            StringAssert.Contains("[FieldOffset(22)] public ushort Reserved;", source);
            StringAssert.Contains("private static readonly ushort[] _referenceSlotGenerations", source);
            StringAssert.Contains("private static bool TryReserveReferenceSlot(out int slot, out ushort referenceGeneration)", source);
            StringAssert.Contains("referenceGeneration = AdvanceReferenceSlotGeneration(slot)", source);
            StringAssert.Contains("Reserved = referenceGeneration", source);
            StringAssert.Contains("private static bool IsReferenceSlotPayloadCurrent(in AudioLogEventPayload payload)", source);
            StringAssert.Contains("payload.Reserved != 0", source);
            StringAssert.Contains("_referenceSlotGenerations[slot] == payload.Reserved", source);
            StringAssert.Contains("private static void ReleaseReferenceSlotForPayload(in AudioLogEventPayload payload)", source);
            StringAssert.Contains("ReleaseReferenceSlotForPayload(in payload);", source);
        }

        private sealed class RecordingAudioLogEventListener : IAudioLogEventListener
        {
            public int ReceivedCount;
            public AudioLogEventType LastType;
            public AudioLogData LastData;
            public AudioLogEventPayload LastPayload;

            public void OnAudioLogEvent(in AudioLogEventPayload payload)
            {
                ReceivedCount++;
                LastType = payload.Type;
                LastPayload = payload;
                AudioLogEvents.TryResolveLogData(in payload, out LastData);
            }
        }

        private static AudioLogData CreateAudioLogData(string name)
        {
            AudioLogData data = ScriptableObject.CreateInstance<AudioLogData>();
            data.name = name;
            return data;
        }

        private static int GetPrivateStaticInt(string fieldName)
        {
            FieldInfo field = typeof(AudioLogEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing AudioLogEvents field: " + fieldName);
            return (int)field.GetValue(null);
        }

        private static void SetPrivateStaticInt(string fieldName, int value)
        {
            FieldInfo field = typeof(AudioLogEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing AudioLogEvents field: " + fieldName);
            field.SetValue(null, value);
        }

        private static bool[] GetPrivateStaticBoolArray(string fieldName)
        {
            FieldInfo field = typeof(AudioLogEvents).GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(field, "Missing AudioLogEvents field: " + fieldName);
            return (bool[])field.GetValue(null);
        }

        private static void AssertNoOccupiedReferenceSlots()
        {
            bool[] occupied = GetPrivateStaticBoolArray("_referenceSlotOccupied");
            for (int i = 0; i < occupied.Length; i++)
                Assert.IsFalse(occupied[i], "Reference slot remained occupied: " + i);
        }

        private static void InvokeResetStaticState()
        {
            MethodInfo reset = typeof(AudioLogEvents).GetMethod("ResetStaticState", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.IsNotNull(reset, "Missing AudioLogEvents.ResetStaticState");
            reset.Invoke(null, null);
        }
    }
}
