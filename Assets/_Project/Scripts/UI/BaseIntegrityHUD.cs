// ============================================================================
// HECTON-8 - BaseIntegrityHUD.cs
// HUD component: nearest base-module integrity warning bridge.
// ============================================================================

using System;
using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.World;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    /// <summary>
    /// Base integrity event identifiers queued by <see cref="BaseIntegrityEvents"/>.
    /// </summary>
    public enum BaseIntegrityEventType : byte
    {
        /// <summary>Nearest module entered warning integrity range.</summary>
        IntegrityWarning = 0,
        /// <summary>Tracked module breached.</summary>
        Breached = 1,
        /// <summary>Tracked module entered cascade failure.</summary>
        Emergency = 2,
        /// <summary>Tracked inhabited module has low breathable reserve.</summary>
        AirQualityWarning = 3
    }

    /// <summary>
    /// Blittable base integrity event payload flushed during dispatcher LateUpdate.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 8)]
    public struct BaseIntegrityEventPayload
    {
        /// <summary>Integrity or air-quality value in normalized [0..1] range.</summary>
        [FieldOffset(0)]
        public float Value;
        /// <summary>Failure mode cast from <see cref="BaseModuleFailureMode"/>.</summary>
        [FieldOffset(4)]
        public byte FailureMode;
        /// <summary>Event type cast from <see cref="BaseIntegrityEventType"/>.</summary>
        [FieldOffset(5)]
        public byte EventType;
        /// <summary>Reserved padding for future payload expansion.</summary>
        [FieldOffset(6)]
        public ushort Reserved;
    }

    /// <summary>
    /// Listener contract for base integrity warning events.
    /// </summary>
    public interface IBaseIntegrityEventListener
    {
        /// <summary>
        /// Receives one base integrity event from the LateUpdate queue drain.
        /// </summary>
        /// <param name="payload">Blittable base integrity event payload.</param>
        void OnBaseIntegrityEvent(in BaseIntegrityEventPayload payload);
    }

    /// <summary>
    /// Queue-backed base integrity event lane flushed by <see cref="SystemDispatcher"/>.
    /// </summary>
    public static class BaseIntegrityEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 8;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;
        private const uint BaseIntegrityListenerRejectedWarningHash = 0x4249524Au; // BIRJ
        private const uint BaseIntegrityListenerExceptionWarningHash = 0x42494558u; // BIEX
        private const uint BaseIntegrityListenerContextHash = 0x42494C53u; // BILS

        private struct ListenerSlot
        {
            public IBaseIntegrityEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        private struct BaseIntegrityListenerRegistry
        {
            private readonly ListenerSlot[] _slots;
            private int _count;

            public BaseIntegrityListenerRegistry(int capacity)
            {
                _slots = new ListenerSlot[capacity];
                _count = 0;
            }

            public int Count => _count;

            public void Clear()
            {
                for (int i = 0; i < _count; i++)
                    _slots[i].Clear();

                _count = 0;
            }

            public bool Contains(IBaseIntegrityEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(_slots[i].Listener, listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IBaseIntegrityEventListener listener)
            {
                if (listener == null || _count >= _slots.Length)
                    return false;

                _slots[_count++].Listener = listener;
                return true;
            }

            public bool TryUnregister(IBaseIntegrityEventListener listener)
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

            public IBaseIntegrityEventListener GetAt(int index)
            {
                return (uint)index < (uint)_count ? _slots[index].Listener : null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - base integrity listeners drained by SystemDispatcher LateUpdate - owner: BaseIntegrityEvents
        private static BaseIntegrityListenerRegistry _listeners = new BaseIntegrityListenerRegistry(ListenerCapacity);
        // COLD ALLOC: ListenerSlot[8] - listener additions deferred while dispatching base integrity events - owner: BaseIntegrityEvents
        private static readonly ListenerSlot[] _deferredRegisterListeners = new ListenerSlot[ListenerCapacity];
        // COLD ALLOC: ListenerSlot[8] - listener removals deferred while dispatching base integrity events - owner: BaseIntegrityEvents
        private static readonly ListenerSlot[] _deferredUnregisterListeners = new ListenerSlot[ListenerCapacity];
        private static NativeQueue<BaseIntegrityEventPayload> _pendingEvents;
        private static NativeQueue<BaseIntegrityEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static int _deferredRegisterCount;
        private static int _deferredUnregisterCount;
        private static int _droppedListenerRegistrationCount;
        private static int _listenerExceptionCount;
        private static int _lastListenerRejectedTelemetryFrame = -1;
        private static int _lastListenerExceptionTelemetryFrame = -1;
        private static bool _isDispatching;

        /// <summary>
        /// Number of pending payloads waiting for LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        /// <summary>
        /// Number of listener register/unregister requests rejected because the fixed deferred buffer was full.
        /// </summary>
        public static int DroppedListenerRegistrationCount => _droppedListenerRegistrationCount;

        /// <summary>
        /// Number of listener callbacks that threw while the base integrity bus isolated dispatch.
        /// </summary>
        public static int ListenerExceptionCount => _listenerExceptionCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BaseIntegrityEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BaseIntegrityEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            Array.Clear(_deferredRegisterListeners, 0, _deferredRegisterCount);
            Array.Clear(_deferredUnregisterListeners, 0, _deferredUnregisterCount);
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _deferredRegisterCount = 0;
            _deferredUnregisterCount = 0;
            _droppedListenerRegistrationCount = 0;
            _listenerExceptionCount = 0;
            _lastListenerRejectedTelemetryFrame = -1;
            _lastListenerExceptionTelemetryFrame = -1;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a listener for base integrity events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Register(IBaseIntegrityEventListener listener)
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

        /// <summary>
        /// Unregisters a listener for base integrity events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IBaseIntegrityEventListener listener)
        {
            if (listener == null)
                return;

            if (_isDispatching)
            {
                QueueDeferredUnregister(listener);
                return;
            }

            _listeners.TryUnregister(listener);
        }

        /// <summary>
        /// Reports an editor/development error if a listener remains registered after teardown.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        /// <param name="ownerName">Human-readable owner name.</param>
        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        public static void AssertUnregistered(IBaseIntegrityEventListener listener, string ownerName)
        {
            if (listener == null || !IsEffectivelyRegistered(listener))
                return;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            UnityEngine.Debug.LogError("[BaseIntegrityEvents] Listener destroyed while still registered.");
#endif
        }

        /// <summary>
        /// Queues an integrity warning payload.
        /// </summary>
        /// <param name="integrity">Normalized integrity value.</param>
        public static bool TryRaiseIntegrityWarning(float integrity)
            => Enqueue(BaseIntegrityEventType.IntegrityWarning, BaseModuleFailureMode.None, integrity);

        [Obsolete("Use TryRaiseIntegrityWarning so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseIntegrityWarning(float integrity) => TryRaiseIntegrityWarning(integrity);

        /// <summary>
        /// Queues a module breached payload.
        /// </summary>
        public static bool TryRaiseBreached()
            => Enqueue(BaseIntegrityEventType.Breached, BaseModuleFailureMode.None, 0f);

        [Obsolete("Use TryRaiseBreached so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseBreached() => TryRaiseBreached();

        /// <summary>
        /// Queues a base emergency payload.
        /// </summary>
        /// <param name="failureMode">Module failure mode.</param>
        /// <param name="integrity">Normalized integrity value.</param>
        public static bool TryRaiseEmergency(BaseModuleFailureMode failureMode, float integrity)
            => Enqueue(BaseIntegrityEventType.Emergency, failureMode, integrity);

        [Obsolete("Use TryRaiseEmergency so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseEmergency(BaseModuleFailureMode failureMode, float integrity)
            => TryRaiseEmergency(failureMode, integrity);

        /// <summary>
        /// Queues an air-quality warning payload.
        /// </summary>
        /// <param name="airQualityNormalized">Normalized breathable reserve.</param>
        public static bool TryRaiseAirQualityWarning(float airQualityNormalized)
            => Enqueue(BaseIntegrityEventType.AirQualityWarning, BaseModuleFailureMode.None, airQualityNormalized);

        [Obsolete("Use TryRaiseAirQualityWarning so bounded queue refusal is visible at the producer.", true)]
        public static void RaiseAirQualityWarning(float airQualityNormalized) => TryRaiseAirQualityWarning(airQualityNormalized);

        /// <summary>
        /// Flushes queued base integrity events through registered listeners.
        /// </summary>
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

                if (!_pendingEvents.TryDequeue(out BaseIntegrityEventPayload payload))
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
                        IBaseIntegrityEventListener listener = _listeners.GetAt(i);
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
            }

            if (_pendingEvents.IsEmpty())
            {
                _pendingEventCount = 0;
                PromoteNextFrameEventsIfFrontEmpty();
            }
        }

        private static bool Enqueue(BaseIntegrityEventType eventType, BaseModuleFailureMode failureMode, float value)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return false;

            BaseIntegrityEventPayload payload = new BaseIntegrityEventPayload
            {
                Value = value,
                FailureMode = (byte)failureMode,
                EventType = (byte)eventType,
                Reserved = 0
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<BaseIntegrityEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<BaseIntegrityEventPayload>[8] — deferred base integrity lane flushed by SystemDispatcher LateUpdate — owner: BaseIntegrityEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(BaseIntegrityEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<BaseIntegrityEventPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<BaseIntegrityEventPayload>[8] — next-frame base integrity lane prevents same-frame reentrant dispatch — owner: BaseIntegrityEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(BaseIntegrityEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _nextFrameEvents, PendingEventCapacity);
            }
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
            ref NativeQueue<BaseIntegrityEventPayload> queue,
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
                {
                    pendingCount = 0;
                    break;
                }

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

            NativeQueue<BaseIntegrityEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(IBaseIntegrityEventListener listener, in BaseIntegrityEventPayload payload)
        {
            try
            {
                listener.OnBaseIntegrityEvent(in payload);
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
            UnityEngine.Debug.LogException(exception);
#endif
        }

        private static void QueueDeferredRegister(IBaseIntegrityEventListener listener)
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

        private static void QueueDeferredUnregister(IBaseIntegrityEventListener listener)
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

        private static bool CancelDeferredRegister(IBaseIntegrityEventListener listener)
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

        private static void CancelDeferredUnregister(IBaseIntegrityEventListener listener)
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

        private static bool IsDeferredRegisterPending(IBaseIntegrityEventListener listener)
        {
            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                if (ReferenceEquals(_deferredRegisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsDeferredUnregisterPending(IBaseIntegrityEventListener listener)
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                if (ReferenceEquals(_deferredUnregisterListeners[i].Listener, listener))
                    return true;
            }

            return false;
        }

        private static bool IsEffectivelyRegistered(IBaseIntegrityEventListener listener)
        {
            return (_listeners.Contains(listener) || IsDeferredRegisterPending(listener)) &&
                !IsDeferredUnregisterPending(listener);
        }

        private static void ApplyDeferredListenerMutations()
        {
            for (int i = 0; i < _deferredUnregisterCount; i++)
            {
                IBaseIntegrityEventListener listener = _deferredUnregisterListeners[i].Listener;
                _deferredUnregisterListeners[i].Clear();
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterCount = 0;

            for (int i = 0; i < _deferredRegisterCount; i++)
            {
                IBaseIntegrityEventListener listener = _deferredRegisterListeners[i].Listener;
                _deferredRegisterListeners[i].Clear();
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterCount = 0;
        }

        private static void RegisterImmediate(IBaseIntegrityEventListener listener)
        {
            if (_listeners.Contains(listener))
                return;

            if (!_listeners.TryRegister(listener))
                ReportListenerRegistrationRejected();
        }

        private static void ReportListenerRegistrationRejected()
        {
            _droppedListenerRegistrationCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerRejectedTelemetryFrame == frame)
                return;

            _lastListenerRejectedTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                BaseIntegrityListenerRejectedWarningHash,
                BaseIntegrityListenerContextHash,
                math.max(1, _droppedListenerRegistrationCount));
        }

        private static void ReportListenerDispatchException()
        {
            _listenerExceptionCount++;
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
            if (_lastListenerExceptionTelemetryFrame == frame)
                return;

            _lastListenerExceptionTelemetryFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(
                BaseIntegrityListenerExceptionWarningHash,
                BaseIntegrityListenerContextHash,
                math.max(1, _listenerExceptionCount));
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-55)]
    public sealed class BaseIntegrityHUD : MonoBehaviour, ISlowTickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
    {
        [Header("── Thresholds ─────────────────────────────")]
        [SerializeField, Range(0f, 1f)] private float warningThreshold = 0.75f;
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float dangerThreshold = 0.10f;
        [SerializeField, Range(0f, 1f)] private float airWarningThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float airCriticalThreshold = 0.12f;

        [Header("── Scan Radius ───────────────────────────")]
        [Tooltip("Search radius for the nearest base module in meters.")]
        [SerializeField] private float scanRadius = 50f;

        private Transform _playerTransform;
        private HectonPlayerMovement _playerMovement;
        private IPlayerRuntimeContext _cachedPlayerContext;
        private ILocalizationTextReadModel _cachedLocalization;
        private bool _registered;
        private bool _lateFrameRegistered;
        private bool _hotSwapListenerRegistered;
        private readonly uint[] _pendingNotificationHashes = new uint[4];
        private readonly byte[] _pendingNotificationTypes = new byte[4];
        private int _pendingNotificationCount;
        private float _lastWarningIntegrity = 1f;
        private float _nextWarningTime;
        private float _lastAirQuality = 1f;
        private float _nextAirWarningTime;
        private BaseModuleFailureMode _lastEmergencyMode = BaseModuleFailureMode.None;
        private BaseModule _lastEmergencyModule;
        private BaseModule _lastBreachedModule;
        private float _nextEmergencyTime;

        private const float WarningCooldown = 30f;
        private const float EmergencyCooldown = 12f;
        private const float AirWarningCooldown = 18f;
        private const int PercentMessageCacheSize = 101;

        // COLD ALLOC: uint[101] — cached danger notification hashes by rounded percent — owner: BaseIntegrityHUD
        private readonly uint[] _dangerNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] — cached critical notification hashes by rounded percent — owner: BaseIntegrityHUD
        private readonly uint[] _criticalNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] — cached warning notification hashes by rounded percent — owner: BaseIntegrityHUD
        private readonly uint[] _warningNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] — cached air-critical notification hashes by rounded percent — owner: BaseIntegrityHUD
        private readonly uint[] _airCriticalNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: char[512] — percent notification formatter scratch; avoids composite-format parser/boxing on warning cache misses — owner: BaseIntegrityHUD
        private readonly char[] _percentMessageBuffer = new char[512];
        private uint _dangerFormatHash;
        private uint _criticalFormatHash;
        private uint _warningFormatHash;
        private uint _airCriticalFormatHash;

        private readonly struct PercentMessageState
        {
            public readonly string Format;
            public readonly string PercentText;
            public readonly int PlaceholderIndex;
            public readonly int SuffixStart;

            public PercentMessageState(string format, string percentText, int placeholderIndex, int suffixStart)
            {
                Format = format;
                PercentText = percentText;
                PlaceholderIndex = placeholderIndex;
                SuffixStart = suffixStart;
            }
        }

        private void OnEnable()
        {
            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();
            TryRegister();
            ResolvePlayerTransform();
        }

        private void OnDisable()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
        }

        public void SlowTick()
        {
            if (_playerTransform == null && !ResolvePlayerTransform())
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            float safeScanRadius = math.max(0f, scanRadius);
            double scanRadiusSq = (double)safeScanRadius * safeScanRadius;
            BaseModule nearestModule = FindNearestActiveModule(in playerAup, scanRadiusSq);

            if (nearestModule == null)
            {
                _lastEmergencyModule = null;
                _lastEmergencyMode = BaseModuleFailureMode.None;
                _lastBreachedModule = null;
                _lastAirQuality = 1f;
                return;
            }

            float integrity = nearestModule.MaxIntegrity > 0f
                ? nearestModule.CurrentIntegrity / nearestModule.MaxIntegrity
                : 0f;
            CheckIntegrityWarnings(nearestModule, integrity);
            PublishAirQualityState(nearestModule);
        }

        public void LateFrameTick()
        {
            int count = math.min(_pendingNotificationCount, _pendingNotificationHashes.Length);
            _pendingNotificationCount = 0;
            for (int i = 0; i < count; i++)
            {
                uint messageHash = _pendingNotificationHashes[i];
                byte type = _pendingNotificationTypes[i];
                _pendingNotificationHashes[i] = 0u;
                _pendingNotificationTypes[i] = 0;
                if (messageHash == 0u)
                    continue;

                if (type == 1)
                    NotificationEvents.TryPushRegisteredWarning(messageHash);
                else
                    NotificationEvents.TryPushRegisteredInfo(messageHash);
            }
        }

        private static BaseModule FindNearestActiveModule(in AbsoluteUniversePosition playerAup, double scanRadiusSq)
        {
            if (scanRadiusSq <= 0d)
                return null;

            BaseModule nearestModule = null;
            double nearestDistanceSq = scanRadiusSq;
            int count = BaseModule.ActiveModuleCount;
            for (int i = 0; i < count; i++)
            {
                BaseModule module = BaseModule.GetActiveModuleAt(i);
                if (module == null || !module.isActiveAndEnabled)
                    continue;

                if (!TryResolveModuleAup(module, out AbsoluteUniversePosition moduleAup))
                    continue;

                double distanceSq = AbsoluteUniversePosition.DistanceSq(in playerAup, in moduleAup);
                if (distanceSq > nearestDistanceSq)
                    continue;

                nearestDistanceSq = distanceSq;
                nearestModule = module;
            }

            return nearestModule;
        }

        private static bool TryResolveModuleAup(BaseModule module, out AbsoluteUniversePosition moduleAup)
        {
            moduleAup = default;
            if (module == null)
                return false;

            Transform moduleTransform = module.transform;
            if (moduleTransform == null)
                return false;

            Vector3 runtimePosition = moduleTransform.position;
            if (!math.isfinite(runtimePosition.x) ||
                !math.isfinite(runtimePosition.y) ||
                !math.isfinite(runtimePosition.z))
            {
                return false;
            }

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            moduleAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return moduleAup.IsFinite();
        }

        private void CheckIntegrityWarnings(BaseModule module, float integrity)
        {
            PublishEmergencyState(module, integrity);

            if (Time.time < _nextWarningTime)
                return;

            if (math.abs(integrity - _lastWarningIntegrity) < 0.05f)
                return;

            _lastWarningIntegrity = integrity;
            int integrityPercent = NormalizedPercent(integrity);

            if (integrity <= dangerThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.3f;
                uint messageHash = GetPercentNotificationHash(
                    _dangerNotificationHashes,
                    ref _dangerFormatHash,
                    ResolveLocalizedSpan(LocalizationKeys.BASE_INTEGRITY_DANGER, "BASE CRITICAL: {0}% - BREACH IMMINENT!"),
                    integrityPercent);
                QueueNotification(messageHash, warning: true);
                BaseIntegrityEvents.TryRaiseIntegrityWarning(integrity);
                return;
            }

            if (integrity <= criticalThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.5f;
                uint messageHash = GetPercentNotificationHash(
                    _criticalNotificationHashes,
                    ref _criticalFormatHash,
                    ResolveLocalizedSpan(LocalizationKeys.BASE_INTEGRITY_CRITICAL, "HECTON-OS: MODULE INTEGRITY {0}% - REPAIRS REQUIRED."),
                    integrityPercent);
                QueueNotification(messageHash, warning: true);
                BaseIntegrityEvents.TryRaiseIntegrityWarning(integrity);
                return;
            }

            if (integrity <= warningThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown;
                uint messageHash = GetPercentNotificationHash(
                    _warningNotificationHashes,
                    ref _warningFormatHash,
                    ResolveLocalizedSpan(LocalizationKeys.BASE_INTEGRITY_WARNING, "BASE MODULE: INTEGRITY {0}%."),
                    integrityPercent);
                QueueNotification(messageHash, warning: false);
            }
        }

        private void PublishEmergencyState(BaseModule module, float integrity)
        {
            if (module == null)
                return;

            if (module.IsBreached && !ReferenceEquals(_lastBreachedModule, module))
            {
                _lastBreachedModule = module;
                BaseIntegrityEvents.TryRaiseBreached();
            }

            BaseModuleFailureMode failureMode = module.CurrentFailureMode;
            if (failureMode == BaseModuleFailureMode.None)
            {
                if (ReferenceEquals(_lastEmergencyModule, module))
                    _lastEmergencyMode = BaseModuleFailureMode.None;
                return;
            }

            bool moduleChanged = !ReferenceEquals(_lastEmergencyModule, module);
            bool modeChanged = _lastEmergencyMode != failureMode;
            if (!moduleChanged && !modeChanged && Time.time < _nextEmergencyTime)
                return;

            _lastEmergencyModule = module;
            _lastEmergencyMode = failureMode;
            _nextEmergencyTime = Time.time + EmergencyCooldown;
            BaseIntegrityEvents.TryRaiseEmergency(failureMode, integrity);
        }

        private void PublishAirQualityState(BaseModule module)
        {
            if (module == null || !module.IsPlayerInsideInterior || module.IsFlooded)
            {
                _lastAirQuality = 1f;
                return;
            }

            float airQuality = module.AirReserveNormalized;
            if (airQuality > airWarningThreshold)
            {
                _lastAirQuality = 1f;
                return;
            }

            if (Time.time < _nextAirWarningTime && math.abs(airQuality - _lastAirQuality) < 0.05f)
                return;

            _lastAirQuality = airQuality;
            _nextAirWarningTime = Time.time + AirWarningCooldown;
            BaseIntegrityEvents.TryRaiseAirQualityWarning(airQuality);

            if (airQuality <= airCriticalThreshold)
            {
                uint messageHash = GetPercentNotificationHash(
                    _airCriticalNotificationHashes,
                    ref _airCriticalFormatHash,
                    "BASE AIR QUALITY CRITICAL: {0}%".AsSpan(),
                    NormalizedPercent(airQuality));
                QueueNotification(messageHash, warning: true);
            }
        }

        private void QueueNotification(uint messageHash, bool warning)
        {
            if (messageHash == 0u)
                return;

            if (_pendingNotificationCount >= _pendingNotificationHashes.Length)
                _pendingNotificationCount = _pendingNotificationHashes.Length - 1;

            _pendingNotificationHashes[_pendingNotificationCount] = messageHash;
            _pendingNotificationTypes[_pendingNotificationCount] = warning ? (byte)1 : (byte)2;
            _pendingNotificationCount++;
        }

        private bool ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return true;

            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerTransform != null)
            {
                _playerTransform = playerContext.PlayerTransform;
                _playerMovement = playerContext.PlayerMovement;
                return true;
            }

            if (!GameBootstrapper.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            _playerTransform = playerTransform;
            _playerMovement = ResolvePlayerMovement(playerTransform);
            return true;
        }

        private bool TryResolvePlayerAup(out AbsoluteUniversePosition playerAup)
        {
            playerAup = default;
            HectonPlayerMovement playerMovement = _playerMovement;
            if (playerMovement == null)
            {
                playerMovement = ResolvePlayerMovement(_playerTransform);
                _playerMovement = playerMovement;
            }

            if (playerMovement == null)
                return false;

            playerAup = playerMovement.CurrentAup;
            return playerAup.IsFinite();
        }

        private HectonPlayerMovement ResolvePlayerMovement(Transform playerTransform)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null && playerContext.PlayerMovement != null)
                return playerContext.PlayerMovement;

            return playerTransform != null && playerTransform.TryGetComponent(out HectonPlayerMovement movement)
                ? movement
                : null;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Player)
            {
                _cachedPlayerContext = currentService as IPlayerRuntimeContext;
                ApplyCachedPlayerContext(forceAssign: true);
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.LocalizationRuntime)
                _cachedLocalization = currentService as ILocalizationTextReadModel;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapListenerRegistered || !Application.isPlaying)
                return;

            _hotSwapListenerRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapListenerRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapListenerRegistered = false;
        }

        private void CacheRegistryServicesCold()
        {
            _cachedPlayerContext = Hecton8.Core.PlayerRuntimeContextService.ActiveRuntimeContext;
            _cachedLocalization = GlobalRegistry.LocalizationText;
        }

        private void ApplyCachedPlayerContext(bool forceAssign)
        {
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext == null)
            {
                if (forceAssign)
                {
                    _playerTransform = null;
                    _playerMovement = null;
                }

                return;
            }

            if (forceAssign || _playerTransform == null)
                _playerTransform = playerContext.PlayerTransform;

            if (forceAssign || _playerMovement == null)
                _playerMovement = playerContext.PlayerMovement;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.UI);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.UI);
                _lateFrameRegistered = false;
            }

            if (_registered)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
                _registered = false;
            }
        }

        private uint GetPercentNotificationHash(uint[] cache, ref uint cachedFormatHash, ReadOnlySpan<char> format, int percent)
        {
            if (cache == null || cache.Length != PercentMessageCacheSize || format.IsEmpty)
                return 0u;

            uint currentFormatHash = NotificationEvents.ComputeMessageHash(format);
            if (cachedFormatHash != currentFormatHash)
            {
                System.Array.Clear(cache, 0, cache.Length);
                cachedFormatHash = currentFormatHash;
            }

            int clampedPercent = math.clamp(percent, 0, PercentMessageCacheSize - 1);
            uint messageHash = cache[clampedPercent];
            if (messageHash != 0u)
                return messageHash;

            if (!TryWritePercentMessage(format, clampedPercent, _percentMessageBuffer, out int length))
                return 0u;

            messageHash = NotificationEvents.RegisterMessage(_percentMessageBuffer.AsSpan(0, length));
            cache[clampedPercent] = messageHash;
            return messageHash;
        }

        private static bool TryWritePercentMessage(
            ReadOnlySpan<char> format,
            int percent,
            char[] buffer,
            out int length)
        {
            length = 0;
            if (buffer == null || buffer.Length <= 0)
                return false;

            int placeholder = IndexOfPercentPlaceholder(format);
            if (placeholder < 0)
            {
                length = math.min(format.Length, buffer.Length);
                format.Slice(0, length).CopyTo(buffer.AsSpan(0, length));
                return length > 0;
            }

            if (placeholder > buffer.Length)
                return false;

            format.Slice(0, placeholder).CopyTo(buffer.AsSpan(0, placeholder));
            length = placeholder;

            if (!percent.TryFormat(buffer.AsSpan(length), out int percentLength))
                return false;

            length += percentLength;
            int suffixStart = placeholder + 3;
            ReadOnlySpan<char> suffix = suffixStart < format.Length
                ? format.Slice(suffixStart)
                : ReadOnlySpan<char>.Empty;
            if (length + suffix.Length > buffer.Length)
                return false;

            suffix.CopyTo(buffer.AsSpan(length, suffix.Length));
            length += suffix.Length;
            return length > 0;
        }

        private static int IndexOfPercentPlaceholder(ReadOnlySpan<char> format)
        {
            for (int i = 0; i <= format.Length - 3; i++)
            {
                if (format[i] == '{' && format[i + 1] == '0' && format[i + 2] == '}')
                    return i;
            }

            return -1;
        }

        private static int NormalizedPercent(float value)
        {
            return math.clamp((int)math.round(value * 100f), 0, PercentMessageCacheSize - 1);
        }

        private ReadOnlySpan<char> ResolveLocalizedSpan(string key, string fallback)
        {
            ILocalizationTextReadModel manager = _cachedLocalization;
            return manager != null
                ? manager.GetRawSpanOrFallback(LocHash.Compute(key), fallback.AsSpan())
                : fallback.AsSpan();
        }
    }
}
