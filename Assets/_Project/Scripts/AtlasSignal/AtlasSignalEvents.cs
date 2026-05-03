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
        private const int PendingEventCapacity = 16;

        // COLD ALLOC: RegistryBucket<IAtlasSignalEventListener>[16] - Atlas signal listeners drained on dispatcher LateUpdate - owner: AtlasSignalEvents
        private static readonly RegistryBucket<IAtlasSignalEventListener> _listeners = new RegistryBucket<IAtlasSignalEventListener>(16);
        // COLD ALLOC: Dictionary<uint,string>[16] - decoded Atlas message IDs keyed by FNV-1a hash for cold-path listener resolution - owner: AtlasSignalEvents
        private static readonly Dictionary<uint, string> _decodedMessageIdsByHash = new Dictionary<uint, string>(16);
        private static NativeQueue<AtlasSignalEventPayload> _pendingEvents;
        private static NativeQueue<AtlasSignalEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AtlasSignalEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AtlasSignalEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _decodedMessageIdsByHash.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
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

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out AtlasSignalEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IAtlasSignalEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IAtlasSignalEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnAtlasSignalEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
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

            if (!Enqueue(new AtlasSignalEventPayload
            {
                SourcePosition = default,
                SignalStrength = 0f,
                MessageHash = messageHash,
                EventType = (ushort)AtlasSignalEventType.Decoded,
                Reserved = 0
            }))
            {
                return;
            }

            if (!_decodedMessageIdsByHash.ContainsKey(messageHash))
                _decodedMessageIdsByHash.Add(messageHash, messageId);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<AtlasSignalEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AtlasSignalEventPayload>[16] - deferred Atlas signal lane flushed by SystemDispatcher LateUpdate - owner: AtlasSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(AtlasSignalEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<AtlasSignalEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AtlasSignalEventPayload>[16] - next-frame Atlas signal lane prevents same-frame reentrant dispatch - owner: AtlasSignalEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(AtlasSignalEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static bool Enqueue(in AtlasSignalEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return true;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
            return true;
        }

        private static void DrainWithoutDispatch()
        {
            if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                return;

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                if (!DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount))
                    return;
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref NativeQueue<AtlasSignalEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out _))
                    break;

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;

            return true;
        }

        private static void PromoteNextFrameEventsIfFrontEmpty()
        {
            if (!_pendingEvents.IsCreated ||
                !_nextFrameEvents.IsCreated ||
                _pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            NativeQueue<AtlasSignalEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
