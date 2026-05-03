// ============================================================================
// HECTON-8 - BaseIntegrityHUD.cs
// HUD component: nearest base-module integrity warning bridge.
// ============================================================================

using Hecton.Localization;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using System.Runtime.InteropServices;
using Unity.Collections;
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
    [StructLayout(LayoutKind.Sequential)]
    public struct BaseIntegrityEventPayload
    {
        /// <summary>Integrity or air-quality value in normalized [0..1] range.</summary>
        public float Value;
        /// <summary>Failure mode cast from <see cref="BaseModuleFailureMode"/>.</summary>
        public byte FailureMode;
        /// <summary>Event type cast from <see cref="BaseIntegrityEventType"/>.</summary>
        public byte EventType;
        /// <summary>Reserved padding for future payload expansion.</summary>
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
        private const int PendingEventCapacity = 8;

        // COLD ALLOC: RegistryBucket<IBaseIntegrityEventListener>[8] - base integrity listeners drained by SystemDispatcher LateUpdate - owner: BaseIntegrityEvents
        private static readonly RegistryBucket<IBaseIntegrityEventListener> _listeners = new RegistryBucket<IBaseIntegrityEventListener>(8);
        private static NativeQueue<BaseIntegrityEventPayload> _pendingEvents;
        private static NativeQueue<BaseIntegrityEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of pending payloads waiting for LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

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
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
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
            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a listener for base integrity events.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        public static void Unregister(IBaseIntegrityEventListener listener)
        {
            if (listener == null)
                return;

            if (_listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        /// <summary>
        /// Reports an editor/development error if a listener remains registered after teardown.
        /// </summary>
        /// <param name="listener">Listener instance.</param>
        /// <param name="ownerName">Human-readable owner name.</param>
        public static void AssertUnregistered(IBaseIntegrityEventListener listener, string ownerName)
        {
            if (listener == null || !_listeners.Contains(listener))
                return;

            Debug.LogError($"[BaseIntegrityEvents] {ownerName} was destroyed while still registered as an IBaseIntegrityEventListener.");
        }

        /// <summary>
        /// Queues an integrity warning payload.
        /// </summary>
        /// <param name="integrity">Normalized integrity value.</param>
        public static void RaiseIntegrityWarning(float integrity)
            => Enqueue(BaseIntegrityEventType.IntegrityWarning, BaseModuleFailureMode.None, integrity);

        /// <summary>
        /// Queues a module breached payload.
        /// </summary>
        public static void RaiseBreached()
            => Enqueue(BaseIntegrityEventType.Breached, BaseModuleFailureMode.None, 0f);

        /// <summary>
        /// Queues a base emergency payload.
        /// </summary>
        /// <param name="failureMode">Module failure mode.</param>
        /// <param name="integrity">Normalized integrity value.</param>
        public static void RaiseEmergency(BaseModuleFailureMode failureMode, float integrity)
            => Enqueue(BaseIntegrityEventType.Emergency, failureMode, integrity);

        /// <summary>
        /// Queues an air-quality warning payload.
        /// </summary>
        /// <param name="airQualityNormalized">Normalized breathable reserve.</param>
        public static void RaiseAirQualityWarning(float airQualityNormalized)
            => Enqueue(BaseIntegrityEventType.AirQualityWarning, BaseModuleFailureMode.None, airQualityNormalized);

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
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                IBaseIntegrityEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IBaseIntegrityEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnBaseIntegrityEvent(in payload);
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

        private static void Enqueue(BaseIntegrityEventType eventType, BaseModuleFailureMode failureMode, float value)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
                return;

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
                return;
            }

            _pendingEvents.Enqueue(payload);
            _pendingEventCount++;
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<BaseIntegrityEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BaseIntegrityEventPayload>[8] - deferred base integrity lane flushed by SystemDispatcher LateUpdate - owner: BaseIntegrityEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(BaseIntegrityEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<BaseIntegrityEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BaseIntegrityEventPayload>[8] - next-frame base integrity lane prevents same-frame reentrant dispatch - owner: BaseIntegrityEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(BaseIntegrityEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
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

            NativeQueue<BaseIntegrityEventPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-55)]
    public sealed class BaseIntegrityHUD : MonoBehaviour, ISlowTickable
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

        [SerializeField] private LayerMask moduleLayerMask = Hecton8.Core.HectonLayerMasks.StrictInteractionLayerMask;

        private Transform _playerTransform;
        private bool _registered;
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

        // COLD ALLOC: Collider[8] - nearest-module overlap scan buffer - owner: BaseIntegrityHUD
        private readonly Collider[] _scanBuffer = new Collider[8];
        // COLD ALLOC: uint[101] — cached danger notification hashes by rounded percent — owner: BaseIntegrityHUD
        private readonly uint[] _dangerNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] — cached critical notification hashes by rounded percent — owner: BaseIntegrityHUD
        private readonly uint[] _criticalNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] — cached warning notification hashes by rounded percent — owner: BaseIntegrityHUD
        private readonly uint[] _warningNotificationHashes = new uint[PercentMessageCacheSize];
        // COLD ALLOC: uint[101] — cached air-critical notification hashes by rounded percent — owner: BaseIntegrityHUD
        private readonly uint[] _airCriticalNotificationHashes = new uint[PercentMessageCacheSize];
        private uint _dangerFormatHash;
        private uint _criticalFormatHash;
        private uint _warningFormatHash;
        private uint _airCriticalFormatHash;

        private void OnEnable()
        {
            TryRegister();
            ResolvePlayerTransform();
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        public void SlowTick()
        {
            if (_playerTransform == null && !ResolvePlayerTransform())
                return;

            int count = UnityEngine.Physics.OverlapSphereNonAlloc(
                _playerTransform.position,
                scanRadius,
                _scanBuffer,
                moduleLayerMask,
                QueryTriggerInteraction.Ignore);

            BaseModule nearestModule = null;
            float nearestDist = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider hit = _scanBuffer[i];
                if (hit == null || !hit.TryGetComponent(out BaseModule module))
                    continue;

                float sqrDist = (_playerTransform.position - hit.transform.position).sqrMagnitude;
                if (sqrDist >= nearestDist)
                    continue;

                nearestDist = sqrDist;
                nearestModule = module;
            }

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

        private void CheckIntegrityWarnings(BaseModule module, float integrity)
        {
            PublishEmergencyState(module, integrity);

            if (Time.time < _nextWarningTime)
                return;

            if (Mathf.Abs(integrity - _lastWarningIntegrity) < 0.05f)
                return;

            _lastWarningIntegrity = integrity;
            int integrityPercent = NormalizedPercent(integrity);

            if (integrity <= dangerThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.3f;
                uint messageHash = GetPercentNotificationHash(
                    _dangerNotificationHashes,
                    ref _dangerFormatHash,
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_DANGER, "BASE CRITICAL: {0}% - BREACH IMMINENT!"),
                    integrityPercent);
                NotificationEvents.PushRegisteredWarning(messageHash);
                BaseIntegrityEvents.RaiseIntegrityWarning(integrity);
                return;
            }

            if (integrity <= criticalThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.5f;
                uint messageHash = GetPercentNotificationHash(
                    _criticalNotificationHashes,
                    ref _criticalFormatHash,
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_CRITICAL, "HECTON-OS: MODULE INTEGRITY {0}% - REPAIRS REQUIRED."),
                    integrityPercent);
                NotificationEvents.PushRegisteredWarning(messageHash);
                BaseIntegrityEvents.RaiseIntegrityWarning(integrity);
                return;
            }

            if (integrity <= warningThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown;
                uint messageHash = GetPercentNotificationHash(
                    _warningNotificationHashes,
                    ref _warningFormatHash,
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_WARNING, "BASE MODULE: INTEGRITY {0}%."),
                    integrityPercent);
                NotificationEvents.PushRegisteredInfo(messageHash);
            }
        }

        private void PublishEmergencyState(BaseModule module, float integrity)
        {
            if (module == null)
                return;

            if (module.IsBreached && !ReferenceEquals(_lastBreachedModule, module))
            {
                _lastBreachedModule = module;
                BaseIntegrityEvents.RaiseBreached();
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
            BaseIntegrityEvents.RaiseEmergency(failureMode, integrity);
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

            if (Time.time < _nextAirWarningTime && Mathf.Abs(airQuality - _lastAirQuality) < 0.05f)
                return;

            _lastAirQuality = airQuality;
            _nextAirWarningTime = Time.time + AirWarningCooldown;
            BaseIntegrityEvents.RaiseAirQualityWarning(airQuality);

            if (airQuality <= airCriticalThreshold)
            {
                uint messageHash = GetPercentNotificationHash(
                    _airCriticalNotificationHashes,
                    ref _airCriticalFormatHash,
                    "BASE AIR QUALITY CRITICAL: {0}%",
                    NormalizedPercent(airQuality));
                NotificationEvents.PushRegisteredWarning(messageHash);
            }
        }

        private bool ResolvePlayerTransform()
        {
            if (_playerTransform != null)
                return true;

            if (!SceneBootstrap.TryGetCurrentPlayerTransform(out Transform playerTransform) ||
                playerTransform == null)
            {
                return false;
            }

            _playerTransform = playerTransform;
            return true;
        }

        private void TryRegister()
        {
            if (_registered || !Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.UI);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        private uint GetPercentNotificationHash(uint[] cache, ref uint cachedFormatHash, string format, int percent)
        {
            if (cache == null || cache.Length != PercentMessageCacheSize || string.IsNullOrEmpty(format))
                return 0u;

            uint currentFormatHash = NotificationEvents.ComputeMessageHash(format);
            if (cachedFormatHash != currentFormatHash)
            {
                System.Array.Clear(cache, 0, cache.Length);
                cachedFormatHash = currentFormatHash;
            }

            int clampedPercent = Mathf.Clamp(percent, 0, PercentMessageCacheSize - 1);
            uint messageHash = cache[clampedPercent];
            if (messageHash != 0u)
                return messageHash;

            string message = string.Format(format, clampedPercent);
            messageHash = NotificationEvents.RegisterMessage(message);
            cache[clampedPercent] = messageHash;
            return messageHash;
        }

        private static int NormalizedPercent(float value)
        {
            return Mathf.Clamp(Mathf.RoundToInt(value * 100f), 0, PercentMessageCacheSize - 1);
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }
    }
}
