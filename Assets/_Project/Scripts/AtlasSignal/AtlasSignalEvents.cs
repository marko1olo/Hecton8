using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton8.AtlasSignal
{
    public enum AtlasSignalEventType : byte
    {
        Pulse = 0,
        Detected = 1,
        StrengthChanged = 2,
        Decoded = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AtlasSignalEventPayload
    {
        public Vector3 SourcePosition;
        public float SignalStrength;
        public uint MessageHash;
        public ushort EventType;
        public ushort Reserved;
    }

    public interface IAtlasSignalEventListener
    {
        void OnAtlasSignalEvent(in AtlasSignalEventPayload payload);
    }

    public static class AtlasSignalEvents
    {
        // COLD ALLOC: RegistryBucket<IAtlasSignalEventListener>[16] - Atlas signal listeners drained on dispatcher LateUpdate - owner: AtlasSignalEvents
        private static readonly RegistryBucket<IAtlasSignalEventListener> _listeners = new RegistryBucket<IAtlasSignalEventListener>(16);
        // COLD ALLOC: Dictionary<uint,string>[16] - decoded Atlas message IDs keyed by FNV-1a hash for cold-path listener resolution - owner: AtlasSignalEvents
        private static readonly Dictionary<uint, string> _decodedMessageIdsByHash = new Dictionary<uint, string>(16);
        private static NativeQueue<AtlasSignalEventPayload> _pendingEvents;

        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEvents.Count : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _decodedMessageIdsByHash.Clear();
        }

        public static void Register(IAtlasSignalEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(IAtlasSignalEventListener listener)
        {
            if (listener == null)
                return;

            _listeners.Unregister(listener);
        }

        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated || _listeners.Count <= 0)
            {
                DrainWithoutDispatch();
                return;
            }

            while (!_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out AtlasSignalEventPayload payload))
                    return;

                IAtlasSignalEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnAtlasSignalEvent(in payload);
            }
        }

        public static uint ComputeMessageHash(string messageId)
        {
            return string.IsNullOrWhiteSpace(messageId)
                ? 0u
                : unchecked((uint)LocHash.Compute(messageId));
        }

        public static bool TryResolveMessageId(uint messageHash, out string messageId)
        {
            return _decodedMessageIdsByHash.TryGetValue(messageHash, out messageId);
        }

        public static void RaisePulse(float intensity)
        {
            Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = default,
                SignalStrength = intensity,
                MessageHash = 0u,
                EventType = (ushort)AtlasSignalEventType.Pulse,
                Reserved = 0
            });
        }

        public static void RaiseDetected(Vector3 sourcePos)
        {
            Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = sourcePos,
                SignalStrength = 0f,
                MessageHash = 0u,
                EventType = (ushort)AtlasSignalEventType.Detected,
                Reserved = 0
            });
        }

        public static void RaiseStrengthChanged(float strength)
        {
            Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = default,
                SignalStrength = strength,
                MessageHash = 0u,
                EventType = (ushort)AtlasSignalEventType.StrengthChanged,
                Reserved = 0
            });
        }

        public static void RaiseDecoded(string messageId)
        {
            uint messageHash = ComputeMessageHash(messageId);
            if (messageHash == 0u)
                return;

            if (!_decodedMessageIdsByHash.ContainsKey(messageHash))
                _decodedMessageIdsByHash.Add(messageHash, messageId);

            Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = default,
                SignalStrength = 0f,
                MessageHash = messageHash,
                EventType = (ushort)AtlasSignalEventType.Decoded,
                Reserved = 0
            });
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<AtlasSignalEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AtlasSignalEventPayload>[16] - deferred Atlas signal lane flushed by SystemDispatcher LateUpdate - owner: AtlasSignalEvents
            }
        }

        private static void Enqueue(in AtlasSignalEventPayload payload)
        {
            EnsureInitialized();
            _pendingEvents.Enqueue(payload);
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            while (_pendingEvents.TryDequeue(out _))
            {
            }
        }
    }
}
