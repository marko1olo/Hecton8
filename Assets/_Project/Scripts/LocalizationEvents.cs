using System.Runtime.InteropServices;
using Hecton8.Core;
using Unity.Collections;
using UnityEngine;

namespace Hecton.Localization
{
    /// <summary>
    /// Localization event discriminator for <see cref="LocalizationEventPayload"/>.
    /// </summary>
    public enum LocalizationEventType : ushort
    {
        LanguageChanged = 1,
        CorruptionVisualStateChanged = 2
    }

    /// <summary>
    /// Deferred unmanaged localization event payload flushed by <see cref="SystemDispatcher"/>.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct LocalizationEventPayload
    {
        public uint Frame;
        public ushort EventType;
        public ushort Language;
        public ushort VisualBucket;
        public ushort StatusBits;
    }

    /// <summary>
    /// Listener contract for deferred language-change events.
    /// </summary>
    public interface ILocalizationLanguageChangedListener
    {
        /// <summary>
        /// Called during the dispatcher late-frame event flush.
        /// </summary>
        /// <param name="payload">Unmanaged localization payload.</param>
        void OnLocalizationLanguageChanged(in LocalizationEventPayload payload);
    }

    /// <summary>
    /// Listener contract for deferred localization corruption visual events.
    /// </summary>
    public interface ILocalizationCorruptionVisualStateListener
    {
        /// <summary>
        /// Called during the dispatcher late-frame event flush.
        /// </summary>
        /// <param name="payload">Unmanaged localization payload.</param>
        void OnLocalizationCorruptionVisualStateChanged(in LocalizationEventPayload payload);
    }

    /// <summary>
    /// NativeQueue-backed localization event lane. Replaces legacy direct static callbacks.
    /// </summary>
    public static class LocalizationEvents
    {
        private const int LanguageListenerCapacity = 128;
        private const int CorruptionListenerCapacity = 64;
        private const int PendingEventCapacity = 128;
        private static readonly uint _OverflowWarningHash = unchecked((uint)LocHash.Compute("LocalizationEvents.Overflow"));

        // COLD ALLOC: RegistryBucket<ILocalizationLanguageChangedListener>[128] - language listeners drained by SystemDispatcher - owner: LocalizationEvents
        private static readonly RegistryBucket<ILocalizationLanguageChangedListener> _languageListeners = new RegistryBucket<ILocalizationLanguageChangedListener>(LanguageListenerCapacity);
        // COLD ALLOC: RegistryBucket<ILocalizationCorruptionVisualStateListener>[64] - corruption visual listeners drained by SystemDispatcher - owner: LocalizationEvents
        private static readonly RegistryBucket<ILocalizationCorruptionVisualStateListener> _corruptionListeners = new RegistryBucket<ILocalizationCorruptionVisualStateListener>(CorruptionListenerCapacity);
        private static NativeQueue<LocalizationEventPayload> _pendingEvents;
        private static NativeQueue<LocalizationEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static bool _overflowWarningQueued;

        /// <summary>
        /// Pending payload count in the localization event lane.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(LocalizationEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(LocalizationEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _languageListeners.Clear();
            _corruptionListeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _overflowWarningQueued = false;
        }

        /// <summary>
        /// Registers a language-change listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterLanguageListener(ILocalizationLanguageChangedListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_languageListeners.Contains(listener))
                _languageListeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a language-change listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterLanguageListener(ILocalizationLanguageChangedListener listener)
        {
            if (listener == null)
                return;

            if (_languageListeners.Contains(listener))
                _languageListeners.Unregister(listener);
        }

        /// <summary>
        /// Registers a corruption-visual listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterCorruptionVisualStateListener(ILocalizationCorruptionVisualStateListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (!_corruptionListeners.Contains(listener))
                _corruptionListeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a corruption-visual listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterCorruptionVisualStateListener(ILocalizationCorruptionVisualStateListener listener)
        {
            if (listener == null)
                return;

            if (_corruptionListeners.Contains(listener))
                _corruptionListeners.Unregister(listener);
        }

        /// <summary>
        /// Enqueues a deferred language-change event.
        /// </summary>
        /// <param name="language">Resolved active language.</param>
        public static void PublishLanguageChanged(GameLanguage language)
        {
            Enqueue(LocalizationEventType.LanguageChanged, language, ushort.MaxValue, 0);
        }

        /// <summary>
        /// Enqueues a deferred localization corruption visual refresh event.
        /// </summary>
        /// <param name="language">Resolved active language.</param>
        /// <param name="visualBucket">Current visual corruption bucket, or a negative value when unknown.</param>
        public static void PublishCorruptionVisualStateChanged(GameLanguage language, int visualBucket)
        {
            ushort bucket = visualBucket < 0
                ? ushort.MaxValue
                : (ushort)Mathf.Min(ushort.MaxValue - 1, visualBucket);
            Enqueue(LocalizationEventType.CorruptionVisualStateChanged, language, bucket, 0);
        }

        /// <summary>
        /// Flushes pending localization events under the dispatcher late-frame budget.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_languageListeners.Count <= 0 && _corruptionListeners.Count <= 0)
            {
                DrainPendingEventsWithoutDispatch();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out LocalizationEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                _isDispatching = true;
                try
                {
                    Dispatch(in payload);
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
                if (_pendingEventCount + _nextFrameEventCount <= 0)
                    _overflowWarningQueued = false;
            }
        }

        private static void DrainPendingEventsWithoutDispatch()
        {
            DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount);

            if (_pendingEventCount <= 0)
            {
                PromoteNextFrameEventsIfFrontEmpty();
                DrainQueueWithoutDispatch(ref _pendingEvents, ref _pendingEventCount);
            }

            if (_nextFrameEvents.IsCreated)
                DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);

            if (_pendingEventCount + _nextFrameEventCount <= 0)
            {
                _pendingEventCount = 0;
                _nextFrameEventCount = 0;
                _overflowWarningQueued = false;
            }
        }

        private static void Enqueue(LocalizationEventType eventType, GameLanguage language, ushort visualBucket, ushort statusBits)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                if (!_overflowWarningQueued)
                {
                    _overflowWarningQueued = true;
                    GlobalTelemetryBus.PublishPerformanceWarning(_OverflowWarningHash, (uint)eventType, PendingCount);
                }
                return;
            }

            LocalizationEventPayload payload = new LocalizationEventPayload
            {
                Frame = unchecked((uint)Mathf.Max(0, Time.frameCount)),
                EventType = (ushort)eventType,
                Language = (ushort)language,
                VisualBucket = visualBucket,
                StatusBits = statusBits
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

        private static void Dispatch(in LocalizationEventPayload payload)
        {
            switch ((LocalizationEventType)payload.EventType)
            {
                case LocalizationEventType.LanguageChanged:
                {
                    ILocalizationLanguageChangedListener[] rawArray = _languageListeners.RawArray;
                    int count = _languageListeners.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ILocalizationLanguageChangedListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnLocalizationLanguageChanged(in payload);
                    }
                    break;
                }

                case LocalizationEventType.CorruptionVisualStateChanged:
                {
                    ILocalizationCorruptionVisualStateListener[] rawArray = _corruptionListeners.RawArray;
                    int count = _corruptionListeners.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ILocalizationCorruptionVisualStateListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnLocalizationCorruptionVisualStateChanged(in payload);
                    }
                    break;
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<LocalizationEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<LocalizationEventPayload>[128] - deferred localization event lane flushed by SystemDispatcher LateUpdate - owner: LocalizationEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(LocalizationEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<LocalizationEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<LocalizationEventPayload>[128] - next-frame localization lane prevents same-frame reentrant dispatch - owner: LocalizationEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(LocalizationEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void DrainQueueWithoutDispatch(
            ref NativeQueue<LocalizationEventPayload> queue,
            ref int pendingCount)
        {
            if (!queue.IsCreated)
                return;

            int scanBudget = pendingCount > 0 ? pendingCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !queue.IsEmpty())
            {
                if (!queue.TryDequeue(out _))
                    break;

                if (pendingCount > 0)
                    pendingCount--;
            }

            if (queue.IsEmpty())
                pendingCount = 0;
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

            NativeQueue<LocalizationEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
