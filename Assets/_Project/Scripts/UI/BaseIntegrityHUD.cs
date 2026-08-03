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
    /// Fixed-capacity, allocation-free UI event lane for main-thread presentation dispatch.
    /// Kept in an existing compiled Unity source file because the generated csproj has an explicit source list.
    /// </summary>
    internal struct FixedUiEventQueue<T>
        where T : unmanaged
    {
        private const int MaxCapacity = 24;

        private T _item0;
        private T _item1;
        private T _item2;
        private T _item3;
        private T _item4;
        private T _item5;
        private T _item6;
        private T _item7;
        private T _item8;
        private T _item9;
        private T _item10;
        private T _item11;
        private T _item12;
        private T _item13;
        private T _item14;
        private T _item15;
        private T _item16;
        private T _item17;
        private T _item18;
        private T _item19;
        private T _item20;
        private T _item21;
        private T _item22;
        private T _item23;

        private int _capacity;
        private int _head;
        private int _count;

        public bool IsCreated => _capacity > 0;

        public void Configure(int capacity)
        {
            _capacity = capacity > MaxCapacity ? MaxCapacity : capacity;
            _item0 = default;
            _item1 = default;
            _item2 = default;
            _item3 = default;
            _item4 = default;
            _item5 = default;
            _item6 = default;
            _item7 = default;
            _item8 = default;
            _item9 = default;
            _item10 = default;
            _item11 = default;
            _item12 = default;
            _item13 = default;
            _item14 = default;
            _item15 = default;
            _item16 = default;
            _item17 = default;
            _item18 = default;
            _item19 = default;
            _item20 = default;
            _item21 = default;
            _item22 = default;
            _item23 = default;
            _head = 0;
            _count = 0;
        }

        public bool IsEmpty()
        {
            return _count <= 0;
        }

        public bool Enqueue(in T value)
        {
            if (_capacity <= 0 || _count >= _capacity)
                return false;

            int index = _head + _count;
            if (index >= _capacity)
                index -= _capacity;

            SetSlot(index, in value);
            _count++;
            return true;
        }

        public bool TryDequeue(out T value)
        {
            if (_capacity <= 0 || _count <= 0)
            {
                value = default;
                return false;
            }

            value = GetSlot(_head);
            ClearSlot(_head);
            _head++;
            if (_head >= _capacity)
                _head = 0;

            _count--;
            return true;
        }

        public void Clear()
        {
            if (_capacity <= 0)
                return;

            for (int i = 0; i < _capacity; i++)
                ClearSlot(i);

            _head = 0;
            _count = 0;
        }

        private T GetSlot(int index)
        {
            switch (index)
            {
                case 0: return _item0;
                case 1: return _item1;
                case 2: return _item2;
                case 3: return _item3;
                case 4: return _item4;
                case 5: return _item5;
                case 6: return _item6;
                case 7: return _item7;
                case 8: return _item8;
                case 9: return _item9;
                case 10: return _item10;
                case 11: return _item11;
                case 12: return _item12;
                case 13: return _item13;
                case 14: return _item14;
                case 15: return _item15;
                case 16: return _item16;
                case 17: return _item17;
                case 18: return _item18;
                case 19: return _item19;
                case 20: return _item20;
                case 21: return _item21;
                case 22: return _item22;
                case 23: return _item23;
                default: return default;
            }
        }

        private void ClearSlot(int index)
        {
            T value = default;
            SetSlot(index, in value);
        }

        private void SetSlot(int index, in T value)
        {
            switch (index)
            {
                case 0: _item0 = value; break;
                case 1: _item1 = value; break;
                case 2: _item2 = value; break;
                case 3: _item3 = value; break;
                case 4: _item4 = value; break;
                case 5: _item5 = value; break;
                case 6: _item6 = value; break;
                case 7: _item7 = value; break;
                case 8: _item8 = value; break;
                case 9: _item9 = value; break;
                case 10: _item10 = value; break;
                case 11: _item11 = value; break;
                case 12: _item12 = value; break;
                case 13: _item13 = value; break;
                case 14: _item14 = value; break;
                case 15: _item15 = value; break;
                case 16: _item16 = value; break;
                case 17: _item17 = value; break;
                case 18: _item18 = value; break;
                case 19: _item19 = value; break;
                case 20: _item20 = value; break;
                case 21: _item21 = value; break;
                case 22: _item22 = value; break;
                case 23: _item23 = value; break;
            }
        }
    }

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
    public struct UiBaseIntegrityEventPayload
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
        void OnBaseIntegrityEvent(in UiBaseIntegrityEventPayload payload);
    }

    /// <summary>
    /// Queue-backed base integrity event lane flushed by <see cref="SystemDispatcher"/>.
    /// </summary>
    public static class BaseIntegrityEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 8;
        private const uint BaseIntegrityListenerRejectedWarningHash = 0x4249524Au; // BIRJ
        private const uint BaseIntegrityListenerExceptionWarningHash = 0x42494558u; // BIEX
        private const uint BaseIntegrityListenerContextHash = 0x42494C53u; // BILS

        private struct BaseIntegrityListenerRegistry
        {
            private int _count;
            private IBaseIntegrityEventListener _slot0;
            private IBaseIntegrityEventListener _slot1;
            private IBaseIntegrityEventListener _slot2;
            private IBaseIntegrityEventListener _slot3;
            private IBaseIntegrityEventListener _slot4;
            private IBaseIntegrityEventListener _slot5;
            private IBaseIntegrityEventListener _slot6;
            private IBaseIntegrityEventListener _slot7;

            public int Count => _count;

            public void Clear()
            {
                _slot0 = null;
                _slot1 = null;
                _slot2 = null;
                _slot3 = null;
                _slot4 = null;
                _slot5 = null;
                _slot6 = null;
                _slot7 = null;
                _count = 0;
            }

            public bool Contains(IBaseIntegrityEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (ReferenceEquals(GetAt(i), listener))
                        return true;
                }

                return false;
            }

            public bool TryRegister(IBaseIntegrityEventListener listener)
            {
                if (listener == null || _count >= ListenerCapacity)
                    return false;

                SetAt(_count, listener);
                _count++;
                return true;
            }

            public bool TryUnregister(IBaseIntegrityEventListener listener)
            {
                for (int i = 0; i < _count; i++)
                {
                    if (!ReferenceEquals(GetAt(i), listener))
                        continue;

                    _count--;
                    SetAt(i, GetAt(_count));
                    SetAt(_count, null);
                    return true;
                }

                return false;
            }

            public IBaseIntegrityEventListener GetAt(int index)
            {
                return index switch
                {
                    0 => _slot0,
                    1 => _slot1,
                    2 => _slot2,
                    3 => _slot3,
                    4 => _slot4,
                    5 => _slot5,
                    6 => _slot6,
                    7 => _slot7,
                    _ => null
                };
            }

            private void SetAt(int index, IBaseIntegrityEventListener listener)
            {
                switch (index)
                {
                    case 0:
                        _slot0 = listener;
                        break;
                    case 1:
                        _slot1 = listener;
                        break;
                    case 2:
                        _slot2 = listener;
                        break;
                    case 3:
                        _slot3 = listener;
                        break;
                    case 4:
                        _slot4 = listener;
                        break;
                    case 5:
                        _slot5 = listener;
                        break;
                    case 6:
                        _slot6 = listener;
                        break;
                    case 7:
                        _slot7 = listener;
                        break;
                }
            }
        }

        private static BaseIntegrityListenerRegistry _listeners;
        private static BaseIntegrityListenerRegistry _deferredRegisterListeners;
        private static BaseIntegrityListenerRegistry _deferredUnregisterListeners;
        // Fixed inline slots: UiBaseIntegrityEventPayload[8] - deferred lane flushed by SystemDispatcher LateUpdate - owner: BaseIntegrityEvents
        private static FixedUiEventQueue<UiBaseIntegrityEventPayload> _pendingEvents;
        // Fixed inline slots: UiBaseIntegrityEventPayload[8] - next-frame lane prevents same-frame reentrant dispatch - owner: BaseIntegrityEvents
        private static FixedUiEventQueue<UiBaseIntegrityEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
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
            _pendingEvents.Clear();
            _nextFrameEvents.Clear();
            _listeners.Clear();
            _deferredRegisterListeners.Clear();
            _deferredUnregisterListeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
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
            Hecton8.Core.H8Debug.LogError("[BaseIntegrityEvents] Listener destroyed while still registered.");
#endif
        }

        /// <summary>
        /// Queues an integrity warning payload.
        /// </summary>
        /// <param name="integrity">Normalized integrity value.</param>
        public static bool TryRaiseIntegrityWarning(float integrity)
            => Enqueue(BaseIntegrityEventType.IntegrityWarning, BaseModuleFailureMode.None, integrity);



        /// <summary>
        /// Queues a base emergency payload.
        /// </summary>
        /// <param name="failureMode">Module failure mode.</param>
        /// <param name="integrity">Normalized integrity value.</param>
        public static bool TryRaiseEmergency(BaseModuleFailureMode failureMode, float integrity)
            => Enqueue(BaseIntegrityEventType.Emergency, failureMode, integrity);

        public static bool TryRaiseBreached()
            => Enqueue(BaseIntegrityEventType.Breached, BaseModuleFailureMode.None, 0f);

        public static bool TryRaiseAirQualityWarning(float airQuality)
            => Enqueue(BaseIntegrityEventType.AirQualityWarning, BaseModuleFailureMode.None, airQuality);



        /// <summary>
        /// Flushes queued base integrity events through registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (_listeners.Count <= 0)
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

                if (!_pendingEvents.TryDequeue(out UiBaseIntegrityEventPayload payload))
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

            UiBaseIntegrityEventPayload payload = default;
            payload.Value = value;
            payload.FailureMode = (byte)failureMode;
            payload.EventType = (byte)eventType;
            payload.Reserved = 0;

            if (_isDispatching)
            {
                if (!_nextFrameEvents.Enqueue(in payload))
                    return false;

                _nextFrameEventCount++;
                return true;
            }

            if (!_pendingEvents.Enqueue(in payload))
                return false;

            _pendingEventCount++;
            return true;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
                _pendingEvents.Configure(PendingEventCapacity);

            if (!_nextFrameEvents.IsCreated)
                _nextFrameEvents.Configure(PendingEventCapacity);
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

            DrainQueueWithoutDispatch(ref _nextFrameEvents, ref _nextFrameEventCount);
        }

        private static bool DrainQueueWithoutDispatch(
            ref FixedUiEventQueue<UiBaseIntegrityEventPayload> queue,
            ref int pendingCount)
        {
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
            if (_pendingEventCount > 0 ||
                _nextFrameEventCount <= 0)
            {
                return;
            }

            FixedUiEventQueue<UiBaseIntegrityEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DispatchToListener(IBaseIntegrityEventListener listener, in UiBaseIntegrityEventPayload payload)
        {
            try
            {
                listener.OnBaseIntegrityEvent(in payload);
            }
            catch (ObjectDisposedException exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
            catch (InvalidOperationException exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
            catch (ArgumentException exception)
            {
                ReportListenerDispatchException();
                LogListenerDispatchException(exception);
            }
            catch (NotSupportedException exception)
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

        private static void QueueDeferredRegister(IBaseIntegrityEventListener listener)
        {
            if (_listeners.Contains(listener))
            {
                CancelDeferredUnregister(listener);
                return;
            }

            if (IsDeferredRegisterPending(listener))
                return;

            if (!_deferredRegisterListeners.TryRegister(listener))
            {
                ReportListenerRegistrationRejected();
                return;
            }
        }

        private static void QueueDeferredUnregister(IBaseIntegrityEventListener listener)
        {
            if (CancelDeferredRegister(listener))
                return;

            if (!_listeners.Contains(listener) || IsDeferredUnregisterPending(listener))
                return;

            if (!_deferredUnregisterListeners.TryRegister(listener))
            {
                ReportListenerRegistrationRejected();
            }
        }

        private static bool CancelDeferredRegister(IBaseIntegrityEventListener listener)
        {
            return _deferredRegisterListeners.TryUnregister(listener);
        }

        private static void CancelDeferredUnregister(IBaseIntegrityEventListener listener)
        {
            _deferredUnregisterListeners.TryUnregister(listener);
        }

        private static bool IsDeferredRegisterPending(IBaseIntegrityEventListener listener)
        {
            return _deferredRegisterListeners.Contains(listener);
        }

        private static bool IsDeferredUnregisterPending(IBaseIntegrityEventListener listener)
        {
            return _deferredUnregisterListeners.Contains(listener);
        }

        private static bool IsEffectivelyRegistered(IBaseIntegrityEventListener listener)
        {
            return (_listeners.Contains(listener) || IsDeferredRegisterPending(listener)) &&
                !IsDeferredUnregisterPending(listener);
        }

        private static void ApplyDeferredListenerMutations()
        {
            int unregisterCount = _deferredUnregisterListeners.Count;
            for (int i = 0; i < unregisterCount; i++)
            {
                IBaseIntegrityEventListener listener = _deferredUnregisterListeners.GetAt(i);
                if (listener != null)
                    _listeners.TryUnregister(listener);
            }

            _deferredUnregisterListeners.Clear();

            int registerCount = _deferredRegisterListeners.Count;
            for (int i = 0; i < registerCount; i++)
            {
                IBaseIntegrityEventListener listener = _deferredRegisterListeners.GetAt(i);
                if (listener != null)
                    RegisterImmediate(listener);
            }

            _deferredRegisterListeners.Clear();
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
        [Header("-- Thresholds -----------------------------")]
        [SerializeField, Range(0f, 1f)] private float warningThreshold = 0.75f;
        [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.25f;
        [SerializeField, Range(0f, 1f)] private float dangerThreshold = 0.10f;
        [SerializeField, Range(0f, 1f)] private float airWarningThreshold = 0.35f;
        [SerializeField, Range(0f, 1f)] private float airCriticalThreshold = 0.12f;

        [Header("-- Scan Radius ---------------------------")]
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
        private static readonly int BaseIntegrityDangerKeyHash = LocHash.Compute(LocalizationKeys.BASE_INTEGRITY_DANGER);
        private static readonly int BaseIntegrityCriticalKeyHash = LocHash.Compute(LocalizationKeys.BASE_INTEGRITY_CRITICAL);
        private static readonly int BaseIntegrityWarningKeyHash = LocHash.Compute(LocalizationKeys.BASE_INTEGRITY_WARNING);
        private static readonly uint BaseIntegrityHudNotificationMissWarningHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.NotificationMiss"));
        private static readonly uint BaseIntegrityHudNotificationOverflowWarningHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.NotificationOverflow"));
        private static readonly uint BaseIntegrityHudNotificationContextHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.Notification"));
        private static readonly uint BaseIntegrityHudEventLaneDropWarningHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.EventLaneDrop"));
        private static readonly uint BaseIntegrityHudEventLaneContextHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.EventLane"));
        private static readonly uint BaseIntegrityHudIntegrityWarningEventContextHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.IntegrityWarning"));
        private static readonly uint BaseIntegrityHudBreachedEventContextHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.Breached"));
        private static readonly uint BaseIntegrityHudEmergencyEventContextHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.Emergency"));
        private static readonly uint BaseIntegrityHudAirQualityEventContextHash = unchecked((uint)LocHash.Compute("BaseIntegrityHUD.AirQualityWarning"));

        // COLD ALLOC: uint[101] - cached danger notification hashes by rounded percent - owner: BaseIntegrityHUD
        private readonly uint[] _dangerNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] - cached critical notification hashes by rounded percent - owner: BaseIntegrityHUD
        private readonly uint[] _criticalNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] - cached warning notification hashes by rounded percent - owner: BaseIntegrityHUD
        private readonly uint[] _warningNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] - cached air-critical notification hashes by rounded percent - owner: BaseIntegrityHUD
        private readonly uint[] _airCriticalNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: char[512] - percent notification formatter scratch; avoids composite-format parser/boxing on warning cache misses - owner: BaseIntegrityHUD
        private readonly char[] _percentMessageBuffer = new char[512];
        private uint _dangerFormatHash;
        private uint _criticalFormatHash;
        private uint _warningFormatHash;
        private uint _airCriticalFormatHash;
        private int _notificationPushMissCount;
        private int _notificationOverflowCount;
        private int _eventLaneDropCount;

        public int NotificationPushMissCount => _notificationPushMissCount;
        public int NotificationOverflowCount => _notificationOverflowCount;
        public int EventLaneDropCount => _eventLaneDropCount;

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
            ClearNotificationRuntimeState();
            ClearEventLaneDiagnostics();
        }

        private void OnDestroy()
        {
            TryUnregisterHotSwapListener();
            TryUnregister();
            ClearNotificationRuntimeState();
            ClearEventLaneDiagnostics();
        }

        public void SlowTick()
        {
            // L19 hop2 LIVE: batch peel BaseIntegrityHUD.SlowTick - last_slowtick name before
            // silent Shut down after WORLDDRIVER begin (module scan / integrity UI under batch).
            if (Application.isBatchMode)
                return;

            if (_playerTransform == null)
                return;

            if (!TryResolvePlayerAup(out AbsoluteUniversePosition playerAup))
                return;

            float safeScanRadius = math.isfinite(scanRadius) ? math.max(0f, scanRadius) : 0f;
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
            // L19 hop2 LIVE: batch peel BaseIntegrityHUD.LateFrameTick - UI presentation.
            if (Application.isBatchMode)
                return;
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
                    TryPushPendingNotification(messageHash, warning: true);
                else
                    TryPushPendingNotification(messageHash, warning: false);
            }
        }

        private static BaseModule FindNearestActiveModule(in AbsoluteUniversePosition playerAup, double scanRadiusSq)
        {
            if (!IsFiniteNonNegativeDistanceSq(scanRadiusSq) || scanRadiusSq <= 0d)
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
                if (!IsFiniteNonNegativeDistanceSq(distanceSq) ||
                    distanceSq > nearestDistanceSq)
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
            if (!originAup.IsFinite())
                return false;

            double3 offsetMeters = default;
            offsetMeters.x = runtimePosition.x;
            offsetMeters.y = runtimePosition.y;
            offsetMeters.z = runtimePosition.z;
            moduleAup = AbsoluteUniversePosition.OffsetMeters(in originAup, offsetMeters);
            return moduleAup.IsFinite();
        }

        private static bool IsFiniteNonNegativeDistanceSq(double distanceSq)
        {
            return !double.IsNaN(distanceSq) &&
                   !double.IsInfinity(distanceSq) &&
                   distanceSq >= 0d;
        }

        private void CheckIntegrityWarnings(BaseModule module, float integrity)
        {
            PublishEmergencyState(module, integrity);

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextWarningTime)
                return;

            if (math.abs(integrity - _lastWarningIntegrity) < 0.05f)
                return;

            _lastWarningIntegrity = integrity;
            int integrityPercent = NormalizedPercent(integrity);

            if (integrity <= dangerThreshold)
            {
                _nextWarningTime = now + WarningCooldown * 0.3f;
                uint messageHash = GetPercentNotificationHash(
                    _dangerNotificationHashes,
                    ref _dangerFormatHash,
                    ResolveLocalizedSpan(BaseIntegrityDangerKeyHash, "BASE CRITICAL: {0}% - BREACH IMMINENT!"),
                    integrityPercent);
                QueueNotification(messageHash, warning: true);
                TryRaiseIntegrityWarningEvent(integrity);
                return;
            }

            if (integrity <= criticalThreshold)
            {
                _nextWarningTime = now + WarningCooldown * 0.5f;
                uint messageHash = GetPercentNotificationHash(
                    _criticalNotificationHashes,
                    ref _criticalFormatHash,
                    ResolveLocalizedSpan(BaseIntegrityCriticalKeyHash, "HECTON-OS: MODULE INTEGRITY {0}% - REPAIRS REQUIRED."),
                    integrityPercent);
                QueueNotification(messageHash, warning: true);
                TryRaiseIntegrityWarningEvent(integrity);
                return;
            }

            if (integrity <= warningThreshold)
            {
                _nextWarningTime = now + WarningCooldown;
                uint messageHash = GetPercentNotificationHash(
                    _warningNotificationHashes,
                    ref _warningFormatHash,
                    ResolveLocalizedSpan(BaseIntegrityWarningKeyHash, "BASE MODULE: INTEGRITY {0}%."),
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
                TryRaiseBreachedEvent();
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
            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (!moduleChanged && !modeChanged && now < _nextEmergencyTime)
                return;

            _lastEmergencyModule = module;
            _lastEmergencyMode = failureMode;
            _nextEmergencyTime = now + EmergencyCooldown;
            TryRaiseEmergencyEvent(failureMode, integrity);
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

            float now = (float)SystemDispatcher.CurrentUnscaledTimeSeconds;
            if (now < _nextAirWarningTime && math.abs(airQuality - _lastAirQuality) < 0.05f)
                return;

            _lastAirQuality = airQuality;
            _nextAirWarningTime = now + AirWarningCooldown;
            TryRaiseAirQualityWarningEvent(airQuality);

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
            {
                ReportNotificationPushMiss(0u);
                return;
            }

            if (_pendingNotificationCount >= _pendingNotificationHashes.Length)
            {
                ReportPendingNotificationOverflow(messageHash);
                _pendingNotificationCount = _pendingNotificationHashes.Length - 1;
            }

            _pendingNotificationHashes[_pendingNotificationCount] = messageHash;
            _pendingNotificationTypes[_pendingNotificationCount] = warning ? (byte)1 : (byte)2;
            _pendingNotificationCount++;
        }

        private void TryPushPendingNotification(uint messageHash, bool warning)
        {
            bool pushed = warning
                ? NotificationEvents.TryPushRegisteredWarning(messageHash)
                : NotificationEvents.TryPushRegisteredInfo(messageHash);
            if (pushed)
                return;

            ReportNotificationPushMiss(messageHash);
        }

        private void ReportNotificationPushMiss(uint messageHash)
        {
            _notificationPushMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                BaseIntegrityHudNotificationMissWarningHash,
                BaseIntegrityHudNotificationContextHash ^ messageHash,
                math.max(1, _notificationPushMissCount));
        }

        private void ReportPendingNotificationOverflow(uint messageHash)
        {
            _notificationOverflowCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                BaseIntegrityHudNotificationOverflowWarningHash,
                BaseIntegrityHudNotificationContextHash ^ messageHash,
                math.max(1, _notificationOverflowCount));
        }

        private void TryRaiseIntegrityWarningEvent(float integrity)
        {
            if (BaseIntegrityEvents.TryRaiseIntegrityWarning(integrity))
                return;

            ReportBaseIntegrityEventLaneDropIfBackpressured(BaseIntegrityHudIntegrityWarningEventContextHash);
        }

        private void TryRaiseBreachedEvent()
        {
            if (BaseIntegrityEvents.TryRaiseBreached())
                return;

            ReportBaseIntegrityEventLaneDropIfBackpressured(BaseIntegrityHudBreachedEventContextHash);
        }

        private void TryRaiseEmergencyEvent(BaseModuleFailureMode failureMode, float integrity)
        {
            if (BaseIntegrityEvents.TryRaiseEmergency(failureMode, integrity))
                return;

            ReportBaseIntegrityEventLaneDropIfBackpressured(
                BaseIntegrityHudEmergencyEventContextHash ^ unchecked((uint)failureMode));
        }

        private void TryRaiseAirQualityWarningEvent(float airQualityNormalized)
        {
            if (BaseIntegrityEvents.TryRaiseAirQualityWarning(airQualityNormalized))
                return;

            ReportBaseIntegrityEventLaneDropIfBackpressured(BaseIntegrityHudAirQualityEventContextHash);
        }

        private void ReportBaseIntegrityEventLaneDropIfBackpressured(uint contextHash)
        {
            if (BaseIntegrityEvents.PendingCount <= 0)
                return;

            _eventLaneDropCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                BaseIntegrityHudEventLaneDropWarningHash,
                BaseIntegrityHudEventLaneContextHash ^ contextHash,
                math.max(1, _eventLaneDropCount));
        }

        private void ClearEventLaneDiagnostics()
        {
            _eventLaneDropCount = 0;
        }

        private void ClearNotificationRuntimeState()
        {
            _pendingNotificationCount = 0;
            Array.Clear(_pendingNotificationHashes, 0, _pendingNotificationHashes.Length);
            Array.Clear(_pendingNotificationTypes, 0, _pendingNotificationTypes.Length);
            _notificationPushMissCount = 0;
            _notificationOverflowCount = 0;
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
            IPlayerRuntimeContext playerContext = _cachedPlayerContext;
            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetPlayerPoseSnapshot(out PlayerRuntimePoseSnapshot snapshot) &&
                (snapshot.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                snapshot.Aup.IsFinite())
            {
                playerAup = snapshot.Aup;
                return true;
            }

            if (playerContext != null &&
                playerContext.IsInitialized &&
                playerContext.TryGetMovementRuntimeState(out PlayerMovementRuntimeState movementState) &&
                (movementState.Flags & (uint)PlayerRuntimeSnapshotFlags.HasPlayerRoot) != 0u &&
                movementState.PredictedAup.IsFinite())
            {
                playerAup = movementState.PredictedAup;
                return true;
            }

            if (playerContext != null)
                return false;

            HectonPlayerMovement playerMovement = _playerMovement;
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
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                TryUnregister();
                if (currentService != null && isActiveAndEnabled)
                    TryRegister();
                return;
            }

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
            _cachedPlayerContext = GlobalRegistry.Player;
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

            _registered = SystemDispatcher.Register((ISlowTickable)this, PriorityLayer.UI);
            if (!_lateFrameRegistered)
                _lateFrameRegistered = SystemDispatcher.Register((ILateFrameTickable)this, PriorityLayer.UI);
        }

        private void TryUnregister()
        {
            if (_lateFrameRegistered)
            {
                SystemDispatcher.UnregisterLateFrameTickableDirect(this, PriorityLayer.UI);
                _lateFrameRegistered = false;
            }

            if (_registered)
            {
                SystemDispatcher.Unregister((ISlowTickable)this, PriorityLayer.UI);
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

        private ReadOnlySpan<char> ResolveLocalizedSpan(int keyHash, string fallback)
        {
            ILocalizationTextReadModel manager = _cachedLocalization;
            return manager != null && keyHash != 0
                ? manager.GetRawSpanOrFallback(keyHash, fallback.AsSpan())
                : fallback.AsSpan();
        }
    }
}
