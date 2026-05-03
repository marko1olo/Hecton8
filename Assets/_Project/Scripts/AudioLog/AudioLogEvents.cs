using System.Diagnostics;
using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;

namespace Hecton8.Narrative
{
    public enum AudioLogEventType : byte
    {
        Discovered = 0,
        PlaybackStarted = 1,
        PlaybackStopped = 2,
        PlaybackCompleted = 3
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct AudioLogEventPayload
    {
        public AudioLogEventType Type;
        public ulong TimestampTicks;
        public uint LogHash;
        public int ReferenceSlot;
        public float DurationSeconds;
    }

    public interface IAudioLogEventListener
    {
        void OnAudioLogEvent(in AudioLogEventPayload payload);
    }

    public static class AudioLogEvents
    {
        // COLD ALLOC: RegistryBucket<IAudioLogEventListener>[16] - audio log event listener registry drained on dispatcher LateUpdate - owner: AudioLogEvents
        private static readonly RegistryBucket<IAudioLogEventListener> _listeners = new RegistryBucket<IAudioLogEventListener>(16);
        private const int PendingEventCapacity = 16;
        private const int ReferenceSlotCapacity = 128;
        private struct AudioLogReferenceSlot
        {
            public AudioLogData LogData;

            public void Clear()
            {
                LogData = null;
            }
        }

        // COLD ALLOC: AudioLogReferenceSlot[128] - managed audio-log data sidecar resolved only during dispatch - owner: AudioLogEvents
        private static readonly AudioLogReferenceSlot[] _referenceSlots = new AudioLogReferenceSlot[ReferenceSlotCapacity];
        // COLD ALLOC: bool[128] - audio-log sidecar occupancy map - owner: AudioLogEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        private static NativeQueue<AudioLogEventPayload> _pendingEvents;
        private static NativeQueue<AudioLogEventPayload> _nextFrameEvents;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AudioLogEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(AudioLogEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        public static void Register(IAudioLogEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            _listeners.Register(listener);
        }

        public static void Unregister(IAudioLogEventListener listener)
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

                if (!_pendingEvents.TryDequeue(out AudioLogEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IAudioLogEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IAudioLogEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnAudioLogEvent(in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                }

                ReleaseReferenceSlot(payload.ReferenceSlot);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        public static bool TryResolveLogData(in AudioLogEventPayload payload, out AudioLogData data)
        {
            data = null;
            if (!IsValidReferenceSlot(payload.ReferenceSlot))
                return false;

            data = _referenceSlots[payload.ReferenceSlot].LogData;
            return data != null;
        }

        public static void RaiseLogDiscovered(uint logHash, AudioLogData data = null)
        {
            Enqueue(AudioLogEventType.Discovered, logHash, 0f, data);
        }

        public static void RaisePlaybackStarted(uint logHash, float durationSeconds, AudioLogData data = null)
        {
            Enqueue(AudioLogEventType.PlaybackStarted, logHash, durationSeconds, data);
        }

        public static void RaisePlaybackStopped(uint logHash, AudioLogData data = null)
        {
            Enqueue(AudioLogEventType.PlaybackStopped, logHash, 0f, data);
        }

        public static void RaisePlaybackCompleted(uint logHash, AudioLogData data = null)
        {
            Enqueue(AudioLogEventType.PlaybackCompleted, logHash, 0f, data);
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<AudioLogEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioLogEventPayload>[16] - deferred audio-log event lane flushed by SystemDispatcher LateUpdate - owner: AudioLogEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(AudioLogEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<AudioLogEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<AudioLogEventPayload>[16] - next-frame audio-log event lane prevents same-frame reentrant dispatch - owner: AudioLogEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(AudioLogEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void Enqueue(AudioLogEventType type, uint logHash, float durationSeconds, AudioLogData data)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

            int referenceSlot = -1;
            if (data != null)
            {
                if (!TryReserveReferenceSlot(out referenceSlot))
                    return;

                _referenceSlots[referenceSlot].LogData = data;
            }

            AudioLogEventPayload payload = new AudioLogEventPayload
            {
                Type = type,
                TimestampTicks = unchecked((ulong)Stopwatch.GetTimestamp()),
                LogHash = logHash,
                ReferenceSlot = referenceSlot,
                DurationSeconds = durationSeconds
            };

            if (_isDispatching)
            {
                _nextFrameEvents.Enqueue(payload);
                _nextFrameEventCount++;
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
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
            ref NativeQueue<AudioLogEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return true;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return false;

                if (!queue.TryDequeue(out AudioLogEventPayload payload))
                    break;

                if (pendingCount > 0)
                    pendingCount--;

                ReleaseReferenceSlot(payload.ReferenceSlot);
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

            NativeQueue<AudioLogEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static bool TryReserveReferenceSlot(out int slot)
        {
            slot = -1;
            if (_referencePendingCount >= ReferenceSlotCapacity)
                return false;

            for (int attempt = 0; attempt < ReferenceSlotCapacity; attempt++)
            {
                int candidate = _referenceWriteIndex;
                _referenceWriteIndex = (_referenceWriteIndex + 1) % ReferenceSlotCapacity;
                if (_referenceSlotOccupied[candidate])
                    continue;

                _referenceSlotOccupied[candidate] = true;
                _referencePendingCount++;
                slot = candidate;
                return true;
            }

            return false;
        }

        private static bool IsValidReferenceSlot(int slot)
        {
            return (uint)slot < ReferenceSlotCapacity && _referenceSlotOccupied[slot];
        }

        private static void ReleaseReferenceSlot(int slot)
        {
            if (!IsValidReferenceSlot(slot))
                return;

            _referenceSlots[slot].Clear();
            _referenceSlotOccupied[slot] = false;
            if (_referencePendingCount > 0)
                _referencePendingCount--;
        }

        private static void ClearReferenceSlots()
        {
            for (int i = 0; i < ReferenceSlotCapacity; i++)
            {
                _referenceSlots[i].Clear();
                _referenceSlotOccupied[i] = false;
            }
        }
    }
}
