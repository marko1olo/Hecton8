using System;
using System.Diagnostics;
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
    [StructLayout(LayoutKind.Explicit, Size = 16)]
    public struct LocalizationEventPayload
    {
        [FieldOffset(0)] public uint Frame;
        [FieldOffset(4)] public ushort EventType;
        [FieldOffset(6)] public ushort Language;
        [FieldOffset(8)] public ushort VisualBucket;
        [FieldOffset(10)] public ushort StatusBits;
        [FieldOffset(12)] private uint _pad0;
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
        private const uint ListenerOverflowWarningHash = 0x4C45564Cu; // LEVL
        private const uint LanguageListenerContextHash = 0x4C454C47u; // LELG
        private const uint CorruptionListenerContextHash = 0x4C454C43u; // LELC
        private const uint ListenerExceptionWarningHash = 0x4C455645u; // LEVE
        private const uint ListenerExceptionLanguageContextHash = 0x4C455847u; // LEXG
        private const uint ListenerExceptionCorruptionContextHash = 0x4C455843u; // LEXC
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private static readonly uint _OverflowWarningHash = unchecked((uint)LocHash.Compute("LocalizationEvents.Overflow"));

        private struct LanguageListenerSlot
        {
            public ILocalizationLanguageChangedListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct CorruptionListenerSlot
        {
            public ILocalizationCorruptionVisualStateListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct LanguageListenerRegistry
        {
            private readonly LanguageListenerSlot[] _slots;
            private int _count;

            public LanguageListenerRegistry(int capacity)
            {
                _slots = new LanguageListenerSlot[capacity]; // COLD ALLOC: LanguageListenerSlot[128] - fixed language listeners drained by SystemDispatcher - owner: LocalizationEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(ILocalizationLanguageChangedListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(ILocalizationLanguageChangedListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void TryUnregister(ILocalizationLanguageChangedListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public ILocalizationLanguageChangedListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private struct CorruptionListenerRegistry
        {
            private readonly CorruptionListenerSlot[] _slots;
            private int _count;

            public CorruptionListenerRegistry(int capacity)
            {
                _slots = new CorruptionListenerSlot[capacity]; // COLD ALLOC: CorruptionListenerSlot[64] - fixed corruption listeners drained by SystemDispatcher - owner: LocalizationEvents
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(ILocalizationCorruptionVisualStateListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(ILocalizationCorruptionVisualStateListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public void TryUnregister(ILocalizationCorruptionVisualStateListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(_slots[i].Listener, listener))
                        continue;

                    _count--;
                    _slots[i] = _slots[_count];
                    _slots[_count].Clear();
                    return;
                }
            }

            public ILocalizationCorruptionVisualStateListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        private static LanguageListenerRegistry _languageListeners = new LanguageListenerRegistry(LanguageListenerCapacity);
        private static CorruptionListenerRegistry _corruptionListeners = new CorruptionListenerRegistry(CorruptionListenerCapacity);
        // COLD ALLOC: LanguageListenerSlot[128] - language listener additions deferred during localization dispatch - owner: LocalizationEvents
        private static readonly LanguageListenerSlot[] _deferredLanguageRegisterListeners = new LanguageListenerSlot[LanguageListenerCapacity];
        // COLD ALLOC: LanguageListenerSlot[128] - language listener removals deferred during localization dispatch - owner: LocalizationEvents
        private static readonly LanguageListenerSlot[] _deferredLanguageUnregisterListeners = new LanguageListenerSlot[LanguageListenerCapacity];
        // COLD ALLOC: CorruptionListenerSlot[64] - corruption listener additions deferred during localization dispatch - owner: LocalizationEvents
        private static readonly CorruptionListenerSlot[] _deferredCorruptionRegisterListeners = new CorruptionListenerSlot[CorruptionListenerCapacity];
        // COLD ALLOC: CorruptionListenerSlot[64] - corruption listener removals deferred during localization dispatch - owner: LocalizationEvents
        private static readonly CorruptionListenerSlot[] _deferredCorruptionUnregisterListeners = new CorruptionListenerSlot[CorruptionListenerCapacity];
        private static NativeQueue<LocalizationEventPayload> _pendingEvents;
        private static NativeQueue<LocalizationEventPayload> _nextFrameEvents;
        private static int _pendingEventsSentinelId;
        private static int _nextFrameEventsSentinelId;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredLanguageRegisterCount;
        private static int _deferredLanguageUnregisterCount;
        private static int _deferredCorruptionRegisterCount;
        private static int _deferredCorruptionUnregisterCount;
        private static int _droppedEventCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerOverflowTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;
        private static bool _overflowWarningQueued;

        /// <summary>
        /// Pending payload count in the localization event lane.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        public static int DroppedEventCount => _droppedEventCount;

        /// <summary>
        /// Listener mutations dropped because fixed deferred buffers were saturated.
        /// </summary>
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        /// <summary>
        /// Listener callback exceptions isolated during late-frame dispatch.
        /// </summary>
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        internal static void ResetStaticState()
        {
            ReleaseNativeQueues();

            _languageListeners.Clear();
            _corruptionListeners.Clear();
            Array.Clear(_deferredLanguageRegisterListeners, 0, _deferredLanguageRegisterCount);
            Array.Clear(_deferredLanguageUnregisterListeners, 0, _deferredLanguageUnregisterCount);
            Array.Clear(_deferredCorruptionRegisterListeners, 0, _deferredCorruptionRegisterCount);
            Array.Clear(_deferredCorruptionUnregisterListeners, 0, _deferredCorruptionUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredLanguageRegisterCount = 0;
            _deferredLanguageUnregisterCount = 0;
            _deferredCorruptionRegisterCount = 0;
            _deferredCorruptionUnregisterCount = 0;
            _droppedEventCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerOverflowTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
            _overflowWarningQueued = false;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorTeardownHooks()
        {
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= ResetStaticState;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ResetStaticState;
            UnityEditor.EditorApplication.quitting -= ResetStaticState;
            UnityEditor.EditorApplication.quitting += ResetStaticState;
        }
#endif

        /// <summary>
        /// Registers a language-change listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void RegisterLanguageListener(ILocalizationLanguageChangedListener listener)
        {
            if (listener == null)
                return;

            EnsureInitialized();
            if (_isDispatching)
            {
                QueueDeferredLanguageRegister(listener);
                return;
            }

            RegisterLanguageListenerImmediate(listener);
        }

        /// <summary>
        /// Unregisters a language-change listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterLanguageListener(ILocalizationLanguageChangedListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredLanguageUnregister(listener);
                return;
            }

            _languageListeners.TryUnregister(listener);
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
            if (_isDispatching)
            {
                QueueDeferredCorruptionRegister(listener);
                return;
            }

            RegisterCorruptionListenerImmediate(listener);
        }

        /// <summary>
        /// Unregisters a corruption-visual listener.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void UnregisterCorruptionVisualStateListener(ILocalizationCorruptionVisualStateListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredCorruptionUnregister(listener);
                return;
            }

            _corruptionListeners.TryUnregister(listener);
        }

        /// <summary>
        /// Enqueues a deferred language-change event.
        /// </summary>
        /// <param name="language">Resolved active language.</param>
        [Obsolete("Use TryPublishLanguageChanged(GameLanguage) so bounded enqueue refusal is visible.", true)]
        public static void PublishLanguageChanged(GameLanguage language)
        {
            TryPublishLanguageChanged(language);
        }

        public static bool TryPublishLanguageChanged(GameLanguage language)
        {
            return Enqueue(LocalizationEventType.LanguageChanged, language, ushort.MaxValue, 0);
        }

        /// <summary>
        /// Enqueues a deferred localization corruption visual refresh event.
        /// </summary>
        /// <param name="language">Resolved active language.</param>
        /// <param name="visualBucket">Current visual corruption bucket, or a negative value when unknown.</param>
        [Obsolete("Use TryPublishCorruptionVisualStateChanged(GameLanguage,int) so bounded enqueue refusal is visible.", true)]
        public static void PublishCorruptionVisualStateChanged(GameLanguage language, int visualBucket)
        {
            TryPublishCorruptionVisualStateChanged(language, visualBucket);
        }

        public static bool TryPublishCorruptionVisualStateChanged(GameLanguage language, int visualBucket)
        {
            ushort bucket = visualBucket < 0
                ? ushort.MaxValue
                : (ushort)Mathf.Min(ushort.MaxValue - 1, visualBucket);
            return Enqueue(LocalizationEventType.CorruptionVisualStateChanged, language, bucket, 0);
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
                {
                    _pendingEventCount = 0;
                    break;
                }

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
                    ApplyDeferredListenerMutations();
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

        private static bool Enqueue(LocalizationEventType eventType, GameLanguage language, ushort visualBucket, ushort statusBits)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                _droppedEventCount++;
                if (!_overflowWarningQueued)
                {
                    _overflowWarningQueued = true;
                    GlobalTelemetryBus.PublishPerformanceWarning(_OverflowWarningHash, (uint)eventType, PendingCount);
                }
                return false;
            }

            LocalizationEventPayload payload = new LocalizationEventPayload
            {
                Frame = SystemDispatcher.CurrentFrameId,
                EventType = (ushort)eventType,
                Language = (ushort)language,
                VisualBucket = visualBucket,
                StatusBits = statusBits
            };

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

        private static void Dispatch(in LocalizationEventPayload payload)
        {
            switch ((LocalizationEventType)payload.EventType)
            {
                case LocalizationEventType.LanguageChanged:
                {
                    int count = _languageListeners.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ILocalizationLanguageChangedListener listener = _languageListeners.GetAt(i);
                        if (listener == null || IsDeferredLanguageUnregisterPending(listener))
                            continue;

                        DispatchToLanguageListener(listener, in payload);
                    }
                    break;
                }

                case LocalizationEventType.CorruptionVisualStateChanged:
                {
                    int count = _corruptionListeners.Count;
                    for (int i = count - 1; i >= 0; i--)
                    {
                        ILocalizationCorruptionVisualStateListener listener = _corruptionListeners.GetAt(i);
                        if (listener == null || IsDeferredCorruptionUnregisterPending(listener))
                            continue;

                        DispatchToCorruptionListener(listener, in payload);
                    }
                    break;
                }
            }
        }

        private static void RegisterLanguageListenerImmediate(ILocalizationLanguageChangedListener listener)
        {
            if (_languageListeners.Contains(listener))
                return;

            if (!_languageListeners.TryRegister(listener))
                ReportListenerOverflow(LanguageListenerContextHash);
        }

        private static void RegisterCorruptionListenerImmediate(ILocalizationCorruptionVisualStateListener listener)
        {
            if (_corruptionListeners.Contains(listener))
                return;

            if (!_corruptionListeners.TryRegister(listener))
                ReportListenerOverflow(CorruptionListenerContextHash);
        }

        private static void QueueDeferredLanguageRegister(ILocalizationLanguageChangedListener listener)
        {
            if (_languageListeners.Contains(listener))
            {
                RemoveDeferredListener(_deferredLanguageUnregisterListeners, ref _deferredLanguageUnregisterCount, listener);
                return;
            }

            RemoveDeferredListener(_deferredLanguageUnregisterListeners, ref _deferredLanguageUnregisterCount, listener);
            if (!TryAppendDeferredListener(_deferredLanguageRegisterListeners, ref _deferredLanguageRegisterCount, listener))
                ReportListenerOverflow(LanguageListenerContextHash);
        }

        private static void QueueDeferredLanguageUnregister(ILocalizationLanguageChangedListener listener)
        {
            if (RemoveDeferredListener(_deferredLanguageRegisterListeners, ref _deferredLanguageRegisterCount, listener))
                return;

            if (!_languageListeners.Contains(listener))
                return;

            if (!TryAppendDeferredListener(_deferredLanguageUnregisterListeners, ref _deferredLanguageUnregisterCount, listener))
                ReportListenerOverflow(LanguageListenerContextHash);
        }

        private static void QueueDeferredCorruptionRegister(ILocalizationCorruptionVisualStateListener listener)
        {
            if (_corruptionListeners.Contains(listener))
            {
                RemoveDeferredListener(_deferredCorruptionUnregisterListeners, ref _deferredCorruptionUnregisterCount, listener);
                return;
            }

            RemoveDeferredListener(_deferredCorruptionUnregisterListeners, ref _deferredCorruptionUnregisterCount, listener);
            if (!TryAppendDeferredListener(_deferredCorruptionRegisterListeners, ref _deferredCorruptionRegisterCount, listener))
                ReportListenerOverflow(CorruptionListenerContextHash);
        }

        private static void QueueDeferredCorruptionUnregister(ILocalizationCorruptionVisualStateListener listener)
        {
            if (RemoveDeferredListener(_deferredCorruptionRegisterListeners, ref _deferredCorruptionRegisterCount, listener))
                return;

            if (!_corruptionListeners.Contains(listener))
                return;

            if (!TryAppendDeferredListener(_deferredCorruptionUnregisterListeners, ref _deferredCorruptionUnregisterCount, listener))
                ReportListenerOverflow(CorruptionListenerContextHash);
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredLanguageUnregisterCount; i++)
                _languageListeners.TryUnregister(_deferredLanguageUnregisterListeners[i].Listener);

            for (int i = 0; i < _deferredCorruptionUnregisterCount; i++)
                _corruptionListeners.TryUnregister(_deferredCorruptionUnregisterListeners[i].Listener);

            for (int i = 0; i < _deferredLanguageRegisterCount; i++)
                RegisterLanguageListenerImmediate(_deferredLanguageRegisterListeners[i].Listener);

            for (int i = 0; i < _deferredCorruptionRegisterCount; i++)
                RegisterCorruptionListenerImmediate(_deferredCorruptionRegisterListeners[i].Listener);

            ClearDeferredListeners(_deferredLanguageRegisterListeners, ref _deferredLanguageRegisterCount);
            ClearDeferredListeners(_deferredLanguageUnregisterListeners, ref _deferredLanguageUnregisterCount);
            ClearDeferredListeners(_deferredCorruptionRegisterListeners, ref _deferredCorruptionRegisterCount);
            ClearDeferredListeners(_deferredCorruptionUnregisterListeners, ref _deferredCorruptionUnregisterCount);
        }

        private static bool IsDeferredLanguageUnregisterPending(ILocalizationLanguageChangedListener listener)
        {
            return ContainsDeferredListener(_deferredLanguageUnregisterListeners, _deferredLanguageUnregisterCount, listener);
        }

        private static bool IsDeferredCorruptionUnregisterPending(ILocalizationCorruptionVisualStateListener listener)
        {
            return ContainsDeferredListener(_deferredCorruptionUnregisterListeners, _deferredCorruptionUnregisterCount, listener);
        }

        private static void DispatchToLanguageListener(
            ILocalizationLanguageChangedListener listener,
            in LocalizationEventPayload payload)
        {
            try
            {
                listener.OnLocalizationLanguageChanged(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerException(ListenerExceptionLanguageContextHash);
                LogListenerDispatchException(exception);
            }
        }

        private static void DispatchToCorruptionListener(
            ILocalizationCorruptionVisualStateListener listener,
            in LocalizationEventPayload payload)
        {
            try
            {
                listener.OnLocalizationCorruptionVisualStateChanged(in payload);
            }
            catch (Exception exception)
            {
                ReportListenerException(ListenerExceptionCorruptionContextHash);
                LogListenerDispatchException(exception);
            }
        }

        [Conditional("UNITY_EDITOR")]
        [Conditional("DEVELOPMENT_BUILD")]
        private static void LogListenerDispatchException(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogException(exception);
#endif
        }

        private static bool TryAppendDeferredListener(
            LanguageListenerSlot[] listeners,
            ref int count,
            ILocalizationLanguageChangedListener listener)
        {
            if (listener == null)
                return true;

            if (ContainsDeferredListener(listeners, count, listener))
                return true;

            if (count >= listeners.Length)
                return false;

            listeners[count++].Listener = listener;
            return true;
        }

        private static bool TryAppendDeferredListener(
            CorruptionListenerSlot[] listeners,
            ref int count,
            ILocalizationCorruptionVisualStateListener listener)
        {
            if (listener == null)
                return true;

            if (ContainsDeferredListener(listeners, count, listener))
                return true;

            if (count >= listeners.Length)
                return false;

            listeners[count++].Listener = listener;
            return true;
        }

        private static bool RemoveDeferredListener(
            LanguageListenerSlot[] listeners,
            ref int count,
            ILocalizationLanguageChangedListener listener)
        {
            for (int i = 0; i < count; i++)
            {
                if (!ReferenceEquals(listeners[i].Listener, listener))
                    continue;

                count--;
                listeners[i] = listeners[count];
                listeners[count].Clear();
                return true;
            }

            return false;
        }

        private static bool RemoveDeferredListener(
            CorruptionListenerSlot[] listeners,
            ref int count,
            ILocalizationCorruptionVisualStateListener listener)
        {
            for (int i = 0; i < count; i++)
            {
                if (!ReferenceEquals(listeners[i].Listener, listener))
                    continue;

                count--;
                listeners[i] = listeners[count];
                listeners[count].Clear();
                return true;
            }

            return false;
        }

        private static bool ContainsDeferredListener(
            LanguageListenerSlot[] listeners,
            int count,
            ILocalizationLanguageChangedListener listener)
        {
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool ContainsDeferredListener(
            CorruptionListenerSlot[] listeners,
            int count,
            ILocalizationCorruptionVisualStateListener listener)
        {
            for (int i = 0; i < count; i++)
            {
                if (ReferenceEquals(listeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static void ClearDeferredListeners(LanguageListenerSlot[] listeners, ref int count)
        {
            for (int i = 0; i < count; i++)
                listeners[i].Clear();

            count = 0;
        }

        private static void ClearDeferredListeners(CorruptionListenerSlot[] listeners, ref int count)
        {
            for (int i = 0; i < count; i++)
                listeners[i].Clear();

            count = 0;
        }

        private static void ReportListenerOverflow(uint contextHash)
        {
            _droppedListenerRegistrationCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerOverflowTelemetryFrame == frame)
                return;

            _lastListenerOverflowTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerOverflowWarningHash,
                contextHash,
                _droppedListenerRegistrationCount);
        }

        private static void ReportListenerException(uint contextHash)
        {
            _listenerExceptionCount++;
            int frame = SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                ListenerExceptionWarningHash,
                contextHash,
                _listenerExceptionCount);
        }

        private static void EnsureInitialized()
        {
            try
            {
                if (!_pendingEvents.IsCreated)
                {
                    _pendingEvents = new NativeQueue<LocalizationEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<LocalizationEventPayload>[128] - deferred localization event lane flushed by SystemDispatcher LateUpdate - owner: LocalizationEvents
                    RegisterNativeQueue(ref _pendingEvents, PendingEventCapacity, nameof(_pendingEvents), out _pendingEventsSentinelId);
                    PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
                }

                if (!_nextFrameEvents.IsCreated)
                {
                    _nextFrameEvents = new NativeQueue<LocalizationEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<LocalizationEventPayload>[128] - next-frame localization lane prevents same-frame reentrant dispatch - owner: LocalizationEvents
                    RegisterNativeQueue(ref _nextFrameEvents, PendingEventCapacity, nameof(_nextFrameEvents), out _nextFrameEventsSentinelId);
                    PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
                }
            }
            catch
            {
                ReleaseNativeQueues();
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
                nameof(LocalizationEvents),
                label,
                NativeAllocationLifetime.Session);
            if (sentinelId > 0)
                return;

            queue.Dispose();
            queue = default;
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
                {
                    pendingCount = 0;
                    break;
                }

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
            int sentinelIdSwap = _pendingEventsSentinelId;
            _pendingEventsSentinelId = _nextFrameEventsSentinelId;
            _nextFrameEventsSentinelId = sentinelIdSwap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }
}
