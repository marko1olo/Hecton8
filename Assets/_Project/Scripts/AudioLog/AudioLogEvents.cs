using System;
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

    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct AudioLogEventPayload
    {
        [FieldOffset(0)] public ulong TimestampTicks;
        [FieldOffset(8)] public uint LogHash;
        [FieldOffset(12)] public int ReferenceSlot;
        [FieldOffset(16)] public float DurationSeconds;
        [FieldOffset(20)] public AudioLogEventType Type;
        [FieldOffset(21)] private byte _pad0;
        [FieldOffset(22)] public ushort Reserved;
        [FieldOffset(24)] public AudioGlitchParametersDTO Glitch;
    }

    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct AudioGlitchParametersDTO
    {
        public const byte FlagBitCrush = 1 << 0;
        public const byte FlagPitchShift = 1 << 1;
        public const byte FlagBandPass = 1 << 2;
        public const byte FlagDepthDerived = 1 << 3;
        public const byte FlagEncryptedPreview = 1 << 4;
        public const byte KnownFlagMask =
            FlagBitCrush |
            FlagPitchShift |
            FlagBandPass |
            FlagDepthDerived |
            FlagEncryptedPreview;
        private const ushort MaxPermille = 1000;
        private const short MinPitchShiftCents = -1200;
        private const short MaxPitchShiftCents = 1200;

        [FieldOffset(0)] public ushort CorruptionPermille;
        [FieldOffset(2)] public ushort BitCrushPermille;
        [FieldOffset(4)] public short PitchShiftCents;
        [FieldOffset(6)] public byte BandPassByte;
        [FieldOffset(7)] public byte Flags;

        public static AudioGlitchParametersDTO Sanitize(in AudioGlitchParametersDTO source)
        {
            AudioGlitchParametersDTO sanitized = source;
            if (sanitized.CorruptionPermille > MaxPermille)
                sanitized.CorruptionPermille = MaxPermille;
            if (sanitized.BitCrushPermille > MaxPermille)
                sanitized.BitCrushPermille = MaxPermille;
            if (sanitized.PitchShiftCents < MinPitchShiftCents)
                sanitized.PitchShiftCents = MinPitchShiftCents;
            else if (sanitized.PitchShiftCents > MaxPitchShiftCents)
                sanitized.PitchShiftCents = MaxPitchShiftCents;
            sanitized.Flags = (byte)(sanitized.Flags & KnownFlagMask);
            return sanitized;
        }
    }

    public interface IAudioLogEventListener
    {
        void OnAudioLogEvent(in AudioLogEventPayload payload);
    }

    public static class AudioLogEvents
    {
        private const int ListenerCapacity = 16;
        private const int PendingEventCapacity = 16;
        private const int ReferenceSlotCapacity = 128;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const uint AudioLogQueueOverflowWarningHash = 0x414C514Fu; // ALQO
        private const uint AudioLogReferenceSlotOverflowWarningHash = 0x414C5253u; // ALRS
        private const uint AudioLogListenerRejectedWarningHash = 0x414C524Au; // ALRJ
        private const uint AudioLogListenerExceptionWarningHash = 0x414C4558u; // ALEX
        private const uint AudioLogQueueContextHash = 0x414C5155u; // ALQU
        private const uint AudioLogReferenceSlotContextHash = 0x414C5246u; // ALRF
        private const uint AudioLogListenerContextHash = 0x414C4953u; // ALIS
        private const float MaxEventDurationSeconds = 86400f;
        private struct ListenerSlot
        {
            public IAudioLogEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct AudioLogListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public AudioLogListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity]; // COLD ALLOC: ListenerSlot[16] - fixed audio-log listener slots drained on dispatcher LateUpdate - owner: AudioLogEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IAudioLogEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IAudioLogEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IAudioLogEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return true;
                }

                return false;
            }

            public IAudioLogEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static AudioLogListenerRegistry _listeners = new AudioLogListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[16] - listener additions deferred while dispatching audio-log events - owner: AudioLogEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[16] - listener removals deferred while dispatching audio-log events - owner: AudioLogEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private struct AudioLogReferenceSlot
        {
            public AudioLogData LogData;

            public void Clear()
            {
                LogData = null;
            }
        }

        // COLD ALLOC: AudioLogReferenceSlot[128] — managed audio-log data sidecar resolved only during dispatch — owner: AudioLogEvents
        private static readonly AudioLogReferenceSlot[] _referenceSlots = new AudioLogReferenceSlot[ReferenceSlotCapacity];
        // COLD ALLOC: bool[128] — audio-log sidecar occupancy map — owner: AudioLogEvents
        private static readonly bool[] _referenceSlotOccupied = new bool[ReferenceSlotCapacity];
        // COLD ALLOC: ushort[128] - reference slot generations invalidate stale payload handles after sidecar reuse - owner: AudioLogEvents
        private static readonly ushort[] _referenceSlotGenerations = new ushort[ReferenceSlotCapacity];
        private static NativeQueue<AudioLogEventPayload> _pendingEvents;
        private static NativeQueue<AudioLogEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _referenceWriteIndex;
        private static int _referencePendingCount;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedReferenceSlotCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastQueueOverflowTelemetryFrame = -1;
        private static int _lastReferenceSlotOverflowTelemetryFrame = -1;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;
        public static int DroppedEventCount => _droppedEventCount;
        public static int DroppedReferenceSlotCount => _droppedReferenceSlotCount;
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [UnityEngine.RuntimeInitializeOnLoadMethod(
            UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ReleaseNativeQueues();

            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            ClearReferenceSlots();
            _referenceWriteIndex = 0;
            _referencePendingCount = 0;
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedReferenceSlotCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastQueueOverflowTelemetryFrame = -1;
            _lastReferenceSlotOverflowTelemetryFrame = -1;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        public static void Register(IAudioLogEventListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredRegister(listener);
                return;
            }

            RegisterImmediate(listener);
        }

        public static void Unregister(IAudioLogEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            if (!_listeners.TryUnregister(listener))
                return;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
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
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IAudioLogEventListener listener = _listeners.GetAt(i);
                        if (listener == null || IsDeferredUnregisterPending(listener))
                            continue;

                        DispatchToListener(listener, in payload);
                    }
                }
                finally
                {
                    _isDispatching = false;
                    ApplyDeferredListenerMutations();
                }

                ReleaseReferenceSlotForPayload(in payload);
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
            if (!IsReferenceSlotPayloadCurrent(in payload))
                return false;

            data = _referenceSlots[payload.ReferenceSlot].LogData;
            return data != null;
        }

        public static bool TryRaiseLogDiscovered(uint logHash, AudioLogData data = null)
        {
            AudioGlitchParametersDTO glitch = default;
            return Enqueue(AudioLogEventType.Discovered, logHash, 0f, in glitch, data);
        }

        public static bool TryRaisePlaybackStarted(uint logHash, float durationSeconds, AudioLogData data = null)
        {
            AudioGlitchParametersDTO glitch = default;
            return Enqueue(AudioLogEventType.PlaybackStarted, logHash, durationSeconds, in glitch, data);
        }

        public static bool TryRaisePlaybackStarted(
            uint logHash,
            float durationSeconds,
            in AudioGlitchParametersDTO glitch,
            AudioLogData data = null)
        {
            return Enqueue(AudioLogEventType.PlaybackStarted, logHash, durationSeconds, in glitch, data);
        }

        public static bool TryRaisePlaybackStopped(uint logHash, AudioLogData data = null)
        {
            AudioGlitchParametersDTO glitch = default;
            return Enqueue(AudioLogEventType.PlaybackStopped, logHash, 0f, in glitch, data);
        }

        public static bool TryRaisePlaybackCompleted(uint logHash, AudioLogData data = null)
        {
            AudioGlitchParametersDTO glitch = default;
            return Enqueue(AudioLogEventType.PlaybackCompleted, logHash, 0f, in glitch, data);
        }

        private static bool Enqueue(
            AudioLogEventType type,
            uint logHash,
            float durationSeconds,
            in AudioGlitchParametersDTO glitch,
            AudioLogData data)
        {
            EnsureInitialized();

            if (_isDispatching)
            {
                if (_nextFrameEventCount >= PendingEventCapacity)
                {
                    ReportQueueOverflow(type);
                    return false;
                }
            }
            else if (_pendingEventCount >= PendingEventCapacity)
            {
                ReportQueueOverflow(type);
                return false;
            }

            int referenceSlot = -1;
            ushort referenceGeneration = 0;
            if (data != null)
            {
                if (!TryReserveReferenceSlot(out referenceSlot, out referenceGeneration))
                {
                    ReportReferenceSlotOverflow(type);
                    return false;
                }

                _referenceSlots[referenceSlot].LogData = data;
            }

            float safeDuration = durationSeconds;
            if (float.IsNaN(safeDuration) || safeDuration < 0f)
                safeDuration = 0f;
            else if (safeDuration > MaxEventDurationSeconds)
                safeDuration = MaxEventDurationSeconds;

            AudioLogEventPayload payload = new AudioLogEventPayload
            {
                TimestampTicks = unchecked((ulong)Stopwatch.GetTimestamp()),
                LogHash = logHash,
                ReferenceSlot = referenceSlot,
                DurationSeconds = safeDuration,
                Type = type,
                Reserved = referenceGeneration,
                Glitch = AudioGlitchParametersDTO.Sanitize(in glitch)
            };

            try
            {
                if (_isDispatching)
                {
                    _nextFrameEvents.Enqueue(payload);
                    _nextFrameEventCount++;
                }
                else
                {
                    _pendingEvents.Enqueue(payload);
                    _pendingEventCount++;
                }
            }
            catch
            {
                if (referenceSlot >= 0)
                    ReleaseReferenceSlot(referenceSlot);
                throw;
            }

            return true;
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<AudioLogEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<AudioLogEventPayload>[16] — deferred audio-log event lane flushed by SystemDispatcher LateUpdate — owner: AudioLogEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<AudioLogEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<AudioLogEventPayload>[16] — next-frame audio-log event lane prevents same-frame reentrant dispatch — owner: AudioLogEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
                ClearReferenceSlots();
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                throw;
            }
        }

        private static void RegisterNativeQueue<T>(
            ref NativeQueue<T> queue,
            int capacity,
            string label,
            out int sentinelId)
            where T : unmanaged
        {
            sentinelId = 0;
            sentinelId = NativeMemorySentinel.RegisterNativeQueueInstance(
                queue,
                capacity,
                nameof(AudioLogEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            ReleaseNativeQueue(ref queue, ref sentinelId);
            throw new InvalidOperationException($"Native memory sentinel registration failed for {label}.");
        }

        private static void ReleaseNativeQueues()
        {
            ReleaseNativeQueue(ref _pendingEvents, ref _pendingEventsSentinelId);
            ReleaseNativeQueue(ref _nextFrameEvents, ref _nextFrameEventsSentinelId);
        }

        private static void ReleaseNativeQueue<T>(ref NativeQueue<T> queue, ref int sentinelId)
            where T : unmanaged
        {
            Exception firstException = null;

            if (sentinelId > 0)
            {
                try
                {
                    NativeMemorySentinel.Unregister(sentinelId);
                }
                catch (Exception exception)
                {
                    firstException = exception;
                }
                finally
                {
                    sentinelId = 0;
                }
            }

            if (queue.IsCreated)
            {
                try
                {
                    queue.Dispose();
                }
                catch (Exception exception)
                {
                    if (firstException == null)
                        firstException = exception;
                }
                finally
                {
                    queue = default;
                }
            }
            else
            {
                queue = default;
            }

            if (firstException != null)
                throw firstException;
        }

        private static void PrewarmQueue<T>(ref NativeQueue<T> queue, int capacity)
            where T : unmanaged
        {
            if (!queue.IsCreated || capacity <= 0)
                return;

            for (int i = 0; i < capacity; i++)
                queue.Enqueue(default);

            while (queue.TryDequeue(out _))
            {
            }
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
                {
                    pendingCount = 0;
                    break;
                }

                if (pendingCount > 0)
                    pendingCount--;

                ReleaseReferenceSlotForPayload(in payload);
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
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(IAudioLogEventListener listener, in AudioLogEventPayload payload)
        {
            try
            {
                listener.OnAudioLogEvent(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IAudioLogEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (_deferredRegisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredRegisterListeners[_deferredRegisterCount++].Listener = listener;
        }

        private static void QueueDeferredUnregister(IAudioLogEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (_deferredUnregisterCount >= ListenerCapacity)
            {
                ReportListenerRegistrationRejected();
                return;
            }

            _deferredUnregisterListeners[_deferredUnregisterCount++].Listener = listener;
        }

        private static bool CancelDeferredRegister(IAudioLogEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (!ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    continue;

                _deferredRegisterCount--;
                _deferredRegisterListeners[i] = _deferredRegisterListeners[_deferredRegisterCount];
                _deferredRegisterListeners[_deferredRegisterCount].Clear();
                return true;
            }

            return false;
        }

        private static void CancelDeferredUnregister(IAudioLogEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (!ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    continue;

                _deferredUnregisterCount--;
                _deferredUnregisterListeners[i] = _deferredUnregisterListeners[_deferredUnregisterCount];
                _deferredUnregisterListeners[_deferredUnregisterCount].Clear();
                return;
            }
        }

        private static bool IsDeferredRegisterPending(IAudioLogEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IAudioLogEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IAudioLogEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IAudioLogEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;

            if (_listeners.Count <= 0)
                DropQueuedEvents();
        }

        private static void RegisterImmediate(IAudioLogEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportQueueOverflow(AudioLogEventType type)
        {
            _droppedEventCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastQueueOverflowTelemetryFrame == frame)
                return;

            _lastQueueOverflowTelemetryFrame = frame;
            uint contextHash = AudioLogQueueContextHash ^ ((uint)type << 24);
            GlobalTelemetryBus.PublishPerformanceWarning(
                AudioLogQueueOverflowWarningHash,
                contextHash,
                PositiveCount(_droppedEventCount));
        }

        private static void ReportReferenceSlotOverflow(AudioLogEventType type)
        {
            _droppedReferenceSlotCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastReferenceSlotOverflowTelemetryFrame == frame)
                return;

            _lastReferenceSlotOverflowTelemetryFrame = frame;
            uint contextHash = AudioLogReferenceSlotContextHash ^ ((uint)type << 24);
            GlobalTelemetryBus.PublishPerformanceWarning(
                AudioLogReferenceSlotOverflowWarningHash,
                contextHash,
                PositiveCount(_droppedReferenceSlotCount));
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                AudioLogListenerRejectedWarningHash,
                AudioLogListenerContextHash,
                PositiveCount(_droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                AudioLogListenerExceptionWarningHash,
                AudioLogListenerContextHash,
                PositiveCount(_listenerExceptionCount));
        }

        private static int PositiveCount(int count)
        {
            return count > 0 ? count : 1;
        }

        private static void DropQueuedEvents()
        {
            if (_pendingEvents.IsCreated)
            {
                while (_pendingEvents.TryDequeue(out AudioLogEventPayload payload))
                    ReleaseReferenceSlotForPayload(in payload);
            }

            if (_nextFrameEvents.IsCreated)
            {
                while (_nextFrameEvents.TryDequeue(out AudioLogEventPayload payload))
                    ReleaseReferenceSlotForPayload(in payload);
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
        }

        private static bool TryReserveReferenceSlot(out int slot, out ushort referenceGeneration)
        {
            slot = -1;
            referenceGeneration = 0;
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
                referenceGeneration = AdvanceReferenceSlotGeneration(slot);
                return true;
            }

            return false;
        }

        private static bool IsValidReferenceSlot(int slot)
        {
            return (uint)slot < ReferenceSlotCapacity;
        }

        private static ushort AdvanceReferenceSlotGeneration(int slot)
        {
            ushort generation = unchecked((ushort)(_referenceSlotGenerations[slot] + 1));
            if (generation == 0)
                generation = 1;

            _referenceSlotGenerations[slot] = generation;
            return generation;
        }

        private static bool IsReferenceSlotPayloadCurrent(in AudioLogEventPayload payload)
        {
            int slot = payload.ReferenceSlot;
            return IsValidReferenceSlot(slot) &&
                   _referenceSlotOccupied[slot] &&
                   payload.Reserved != 0 &&
                   _referenceSlotGenerations[slot] == payload.Reserved;
        }

        private static void ReleaseReferenceSlotForPayload(in AudioLogEventPayload payload)
        {
            if (IsReferenceSlotPayloadCurrent(in payload))
                ReleaseReferenceSlot(payload.ReferenceSlot);
        }

        private static void ReleaseReferenceSlot(int slot)
        {
            if (!IsValidReferenceSlot(slot))
                return;

            if (!_referenceSlotOccupied[slot])
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
                AdvanceReferenceSlotGeneration(i);
            }
        }
    }
}
