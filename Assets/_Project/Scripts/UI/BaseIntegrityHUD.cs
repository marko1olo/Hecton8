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
        private static int _pendingEventCount;

        /// <summary>
        /// Number of pending payloads waiting for LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEvents.IsCreated ? _pendingEventCount : 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(BaseIntegrityEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
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
                for (int i = count - 1; i >= 0; i--)
                    rawArray[i].OnBaseIntegrityEvent(in payload);
            }

            if (_pendingEvents.IsEmpty())
                _pendingEventCount = 0;
        }

        private static void Enqueue(BaseIntegrityEventType eventType, BaseModuleFailureMode failureMode, float value)
        {
            EnsureInitialized();
            if (_pendingEventCount >= PendingEventCapacity)
                return;

            _pendingEvents.Enqueue(new BaseIntegrityEventPayload
            {
                Value = value,
                FailureMode = (byte)failureMode,
                EventType = (byte)eventType,
                Reserved = 0
            });
            _pendingEventCount++;
        }

        private static void EnsureInitialized()
        {
            if (_pendingEvents.IsCreated)
                return;

            _pendingEvents = new NativeQueue<BaseIntegrityEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<BaseIntegrityEventPayload>[8] - deferred base integrity lane flushed by SystemDispatcher LateUpdate - owner: BaseIntegrityEvents
            NativeMemorySentinel.RegisterNativeQueue(
                _pendingEvents,
                PendingEventCapacity,
                nameof(BaseIntegrityEvents),
                nameof(_pendingEvents),
                NativeAllocationLifetime.Session);
        }

        private static void DrainWithoutDispatch()
        {
            if (!_pendingEvents.IsCreated)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out _))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;
            }

            if (_pendingEvents.IsEmpty())
                _pendingEventCount = 0;
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

        public static BaseIntegrityHUD Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticInstance() => Instance = null;

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

        // COLD ALLOC: Collider[8] - nearest-module overlap scan buffer - owner: BaseIntegrityHUD
        private readonly Collider[] _scanBuffer = new Collider[8];

        private const float WarningCooldown = 30f;
        private const float EmergencyCooldown = 12f;
        private const float AirWarningCooldown = 18f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
        }

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

            if (Instance == this)
                Instance = null;
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
            float integrityPercent = integrity * 100f;

            if (integrity <= dangerThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.3f;
                NotificationEvents.PushWarning(string.Format(
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_DANGER, "BASE CRITICAL: {0}% - BREACH IMMINENT!"),
                    integrityPercent));
                BaseIntegrityEvents.RaiseIntegrityWarning(integrity);
                return;
            }

            if (integrity <= criticalThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown * 0.5f;
                NotificationEvents.PushWarning(string.Format(
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_CRITICAL, "HECTON-OS: MODULE INTEGRITY {0}% - REPAIRS REQUIRED."),
                    integrityPercent));
                BaseIntegrityEvents.RaiseIntegrityWarning(integrity);
                return;
            }

            if (integrity <= warningThreshold)
            {
                _nextWarningTime = Time.time + WarningCooldown;
                NotificationEvents.PushInfo(string.Format(
                    ResolveLocalized(LocalizationKeys.BASE_INTEGRITY_WARNING, "BASE MODULE: INTEGRITY {0}%."),
                    integrityPercent));
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
                NotificationEvents.PushWarning(string.Format("BASE AIR QUALITY CRITICAL: {0}%", Mathf.RoundToInt(airQuality * 100f)));
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
            _registered = true;
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.UI);
            _registered = false;
        }

        private static string ResolveLocalized(string key, string fallback)
        {
            LocalizationManager manager = Hecton8.Core.GlobalRegistry.Localization;
            return manager != null ? manager.GetOrFallback(manager.CurrentLanguage, key, fallback) : fallback;
        }
    }
}
