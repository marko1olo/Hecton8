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
        private static int _pendingEventCount;
        private static bool _overflowWarningQueued;

        /// <summary>
        /// Pending payload count in the localization event lane.
        /// </summary>
        public static int PendingCount => _pendingEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(LocalizationEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _languageListeners.Clear();
            _corruptionListeners.Clear();
            _pendingEventCount = 0;
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

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out LocalizationEventPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                Dispatch(in payload);
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                _overflowWarningQueued = false;
            }
        }

        private static void DrainPendingEventsWithoutDispatch()
        {
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!_pendingEvents.TryDequeue(out _))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                _overflowWarningQueued = false;
            }
        }

        private static void Enqueue(LocalizationEventType eventType, GameLanguage language, ushort visualBucket, ushort statusBits)
        {
            EnsureInitialized();
            if (_pendingEventCount >= PendingEventCapacity)
            {
                if (!_overflowWarningQueued)
                {
                    _overflowWarningQueued = true;
                    GlobalTelemetryBus.PublishPerformanceWarning(_OverflowWarningHash, (uint)eventType, _pendingEventCount);
                }
                return;
            }

            _pendingEvents.Enqueue(new LocalizationEventPayload
            {
                Frame = unchecked((uint)Mathf.Max(0, Time.frameCount)),
                EventType = (ushort)eventType,
                Language = (ushort)language,
                VisualBucket = visualBucket,
                StatusBits = statusBits
            });
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
                        rawArray[i].OnLocalizationLanguageChanged(in payload);
                    break;
                }

                case LocalizationEventType.CorruptionVisualStateChanged:
                {
                    ILocalizationCorruptionVisualStateListener[] rawArray = _corruptionListeners.RawArray;
                    int count = _corruptionListeners.Count;
                    for (int i = count - 1; i >= 0; i--)
                        rawArray[i].OnLocalizationCorruptionVisualStateChanged(in payload);
                    break;
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (_pendingEvents.IsCreated)
                return;

            _pendingEvents = new NativeQueue<LocalizationEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<LocalizationEventPayload>[128] - deferred localization event lane flushed by SystemDispatcher LateUpdate - owner: LocalizationEvents
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingEvents,
                PendingEventCapacity,
                nameof(LocalizationEvents),
                nameof(_pendingEvents),
                NativeAllocationLifetime.Session);
        }
    }
}
