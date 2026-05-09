using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Atmosphere;
using Hecton8.Core;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Acoustic payload emitted when electrolysis boils surrounding water.
    /// </summary>
    public readonly struct ElectrolysisAcousticEvent
    {
        public ElectrolysisAcousticEvent(Vector3 position, float dumpedPowerWatts, float oxygenUnits, float threatStrength, float radiusMeters)
        {
            Position = position;
            DumpedPowerWatts = dumpedPowerWatts;
            OxygenUnits = oxygenUnits;
            ThreatStrength = threatStrength;
            RadiusMeters = radiusMeters;
        }

        public Vector3 Position { get; }
        public float DumpedPowerWatts { get; }
        public float OxygenUnits { get; }
        public float ThreatStrength { get; }
        public float RadiusMeters { get; }
    }

    /// <summary>
    /// NativeQueue-backed electrolysis acoustic event lane.
    /// </summary>
    public static class ElectrolysisAcousticEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 32;

        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("ElectrolysisAcousticEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("ElectrolysisAcousticEvents"));

        // COLD ALLOC: RegistryBucket<IElectrolysisAcousticEventListener>[8] - electrolysis acoustic listeners drained by SystemDispatcher LateUpdate - owner: ElectrolysisAcousticEvents
        private static readonly RegistryBucket<IElectrolysisAcousticEventListener> _listeners = new RegistryBucket<IElectrolysisAcousticEventListener>(ListenerCapacity);

        private static NativeQueue<ElectrolysisAcousticPayload> _pendingEvents;
        private static NativeQueue<ElectrolysisAcousticPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;
        private static int _lastOverflowWarningFrame = -1;

        /// <summary>
        /// Number of pending electrolysis acoustic payloads waiting for late-frame dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ElectrolysisAcousticEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(ElectrolysisAcousticEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
            _lastOverflowWarningFrame = -1;
        }

        /// <summary>
        /// Registers an electrolysis acoustic listener.
        /// </summary>
        public static void Register(IElectrolysisAcousticEventListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters an electrolysis acoustic listener.
        /// </summary>
        public static void Unregister(IElectrolysisAcousticEventListener listener)
        {
            if (listener == null)
                return;

            if (!_listeners.Contains(listener))
                return;

            _listeners.Unregister(listener);
            if (_listeners.Count <= 0)
                DropQueuedPayloads();
        }

        /// <summary>
        /// Publishes one electrolysis acoustic payload to the deferred event lane.
        /// </summary>
        public static void Notify(in ElectrolysisAcousticEvent acousticEvent)
        {
            if (_listeners.Count <= 0)
                return;

            Enqueue(new ElectrolysisAcousticPayload
            {
                Position = acousticEvent.Position,
                DumpedPowerWatts = acousticEvent.DumpedPowerWatts,
                OxygenUnits = acousticEvent.OxygenUnits,
                ThreatStrength = acousticEvent.ThreatStrength,
                RadiusMeters = acousticEvent.RadiusMeters
            });
        }

        /// <summary>
        /// Flushes pending electrolysis acoustic events to registered listeners.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            if (_listeners.Count <= 0)
            {
                DropQueuedPayloads();
                return;
            }

            PromoteNextFrameEventsIfFrontEmpty();
            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : PendingEventCapacity;
            while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
            {
                if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                    return;

                if (!_pendingEvents.TryDequeue(out ElectrolysisAcousticPayload payload))
                    break;

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                ElectrolysisAcousticEvent acousticEvent = new ElectrolysisAcousticEvent(
                    payload.Position,
                    payload.DumpedPowerWatts,
                    payload.OxygenUnits,
                    payload.ThreatStrength,
                    payload.RadiusMeters);

                IElectrolysisAcousticEventListener[] rawArray = _listeners.RawArray;
                int count = _listeners.Count;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IElectrolysisAcousticEventListener listener = rawArray[i];
                        if (listener != null)
                            listener.OnElectrolysisAcoustic(in acousticEvent);
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

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<ElectrolysisAcousticPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ElectrolysisAcousticPayload>[32] - deferred electrolysis acoustic lane flushed by SystemDispatcher LateUpdate - owner: ElectrolysisAcousticEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    PendingEventCapacity,
                    nameof(ElectrolysisAcousticEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingEvents, PendingEventCapacity);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<ElectrolysisAcousticPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<ElectrolysisAcousticPayload>[32] - next-frame electrolysis acoustic lane prevents same-frame reentrant dispatch - owner: ElectrolysisAcousticEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    PendingEventCapacity,
                    nameof(ElectrolysisAcousticEvents),
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

        private static bool Enqueue(in ElectrolysisAcousticPayload payload)
        {
            if (_pendingEventCount + _nextFrameEventCount >= PendingEventCapacity)
            {
                ReportOverflowOncePerFrame();
                return false;
            }

            EnsureInitialized();
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

        private static void ReportOverflowOncePerFrame()
        {
            int frame = Time.frameCount;
            if (_lastOverflowWarningFrame == frame)
                return;

            _lastOverflowWarningFrame = frame;
            GlobalTelemetryBus.PublishPerformanceWarning(_overflowWarningHash, _queueHash, PendingEventCapacity);
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

            NativeQueue<ElectrolysisAcousticPayload> swap = _pendingEvents;
            _pendingEvents = _nextFrameEvents;
            _nextFrameEvents = swap;
            _pendingEventCount = _nextFrameEventCount;
            _nextFrameEventCount = 0;
        }

        private static void DropQueuedPayloads()
        {
            if (_pendingEvents.IsCreated)
            {
                while (_pendingEvents.TryDequeue(out _))
                {
                }
            }

            if (_nextFrameEvents.IsCreated)
            {
                while (_nextFrameEvents.TryDequeue(out _))
                {
                }
            }

            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
        }
    }

    /// <summary>
    /// Listener for deferred electrolysis acoustic events.
    /// </summary>
    public interface IElectrolysisAcousticEventListener
    {
        /// <summary>
        /// Receives one late-frame electrolysis acoustic payload.
        /// </summary>
        void OnElectrolysisAcoustic(in ElectrolysisAcousticEvent acousticEvent);
    }

    /// <summary>
    /// Blittable queued payload for electrolysis acoustic events.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ElectrolysisAcousticPayload
    {
        public Vector3 Position;
        public float DumpedPowerWatts;
        public float OxygenUnits;
        public float ThreatStrength;
        public float RadiusMeters;
    }

    /// <summary>
    /// Grid-powered electrolysis stack that converts local seawater into breathable oxygen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton/Gameplay/Submarine Electrolysis Module")]
    public sealed class SubmarineElectrolysisModule : MonoBehaviour, ISlowTickable, IPowerComponent
    {
        private const float SlowTickDeltaTime = 0.5f;

        [Header("References")]
        [SerializeField] private SubmarineAtmosphereSystem atmosphereSystem;
        [SerializeField] private SubmarineFluidDynamics fluidDynamics;
        [SerializeField] private BaseModule hostModule;
        [SerializeField] private PowerNode powerNode;

        [Header("Process")]
        [Tooltip("Target room index that receives generated oxygen.")]
        [SerializeField, Min(0)] private int targetRoomIndex;

        [Tooltip("Continuous electrical draw while the electrolysis stack is active.")]
        [SerializeField, Min(0f)] private float powerDrawWatts = 500000f;

        [Tooltip("Priority used when industrial loads start getting shed by the grid.")]
        [SerializeField, Range(0, 100)] private int powerPriority = 12;

        [Tooltip("Minimum local flood volume required before the stack can source internal water.")]
        [SerializeField, Min(0f)] private float minimumFloodWaterVolumeCubicMeters = 0.05f;

        [Tooltip("If true, the module may run while dry as long as the external ocean runtime is available.")]
        [SerializeField] private bool allowOceanWaterFallback = true;

        [Tooltip("Reference-gas-volume oxygen units produced per kilowatt-second of electrical input.")]
        [SerializeField, Min(0f)] private float oxygenUnitsPerKilowattSecond = 0.02f;

        [Tooltip("Direct room-temperature rise applied each SlowTick while electrolysis is active.")]
        [SerializeField, Min(0f)] private float temperatureRisePerSlowTickCelsius = 10f;

        [Header("Consequence")]
        [Tooltip("Threat radius applied to the local ocean threat grid when electrolysis boils hard.")]
        [SerializeField, Min(1f)] private float threatRadiusMeters = 55f;

        [Tooltip("Threat-grid strength injected each SlowTick while electrolysis is active.")]
        [SerializeField, Min(0f)] private float threatStrength = 90f;

        [Tooltip("How long the threat pulse persists after each electrolysis step.")]
        [SerializeField, Min(0.1f)] private float threatHoldSeconds = 2.5f;

        [Tooltip("Upward convection speed injected into the abyssal flow field.")]
        [SerializeField, Min(0f)] private float thermalUpdraftMetersPerSecond = 4f;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugHasPower = true;
        [SerializeField] private bool _debugHasWaterSource;
        [SerializeField] private bool _debugIsOperating;
        [SerializeField] private float _debugLastDumpedPowerWatts;
        [SerializeField] private float _debugLastOxygenUnits;
        [SerializeField] private float _debugLastThreatStrength;

        private Transform _cachedTransform;
        private bool _hasPower = true;
        private bool _hasWaterSource;
        private bool _isOperating;
        private bool _registered;

        /// <inheritdoc />
        public float PowerRating => _isOperating ? -math.max(0f, powerDrawWatts) : 0f;

        /// <inheritdoc />
        public int PowerPriority => powerPriority;

        /// <inheritdoc />
        public bool HasPower => _hasPower;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            TryStartRuntimeLifecycle();
        }

        private void Start()
        {
            TryStartRuntimeLifecycle();
        }

        private void TryStartRuntimeLifecycle()
        {
            CacheReferences();
            if (!CanUseRuntimeDispatcher())
                return;

            TryRegister();
            _hasWaterSource = ResolveWaterSourceAvailability();
            _debugHasWaterSource = _hasWaterSource;
            _debugHasPower = _hasPower;
            _debugIsOperating = _isOperating;
        }

        private void OnDisable()
        {
            TryUnregister();
        }

        private void OnDestroy()
        {
            TryUnregister();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!CanUseRuntimeDispatcher())
                return;

            if (atmosphereSystem == null || powerNode == null)
                return;

            bool nextWaterSource = ResolveWaterSourceAvailability();
            _hasWaterSource = nextWaterSource;
            _debugHasWaterSource = nextWaterSource;

            bool nextOperating = _hasPower && nextWaterSource;
            if (_isOperating != nextOperating)
            {
                _isOperating = nextOperating;
                _debugIsOperating = nextOperating;
                NotifyGridBalanceChanged();
            }

            if (!_isOperating)
                return;

            float consumedPowerWatts = FiniteNonNegativeOrZero(powerDrawWatts);
            float oxygenUnits = (consumedPowerWatts * SlowTickDeltaTime * 0.001f) * FiniteNonNegativeOrZero(oxygenUnitsPerKilowattSecond);
            if (!math.isfinite(oxygenUnits) || oxygenUnits <= 0f)
                return;

            atmosphereSystem.InjectOxygenUnits(targetRoomIndex, oxygenUnits);
            atmosphereSystem.InjectRoomTemperatureDeltaCelsius(targetRoomIndex, FiniteNonNegativeOrZero(temperatureRisePerSlowTickCelsius));

            Vector3 position = ResolveCinematicPulsePosition();
            float safeThreatRadius = FiniteAtLeast(threatRadiusMeters, 1f);
            float safeThreatStrength = FiniteNonNegativeOrZero(threatStrength);
            float safeThreatHoldSeconds = FiniteAtLeast(threatHoldSeconds, 0.1f);
            float safeThermalUpdraft = FiniteNonNegativeOrZero(thermalUpdraftMetersPerSecond);
            HectonMapMagicVegetationBridge bridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (bridge != null)
            {
                bridge.ApplyExternalThreatPulse(position, safeThreatRadius, safeThreatStrength, safeThreatHoldSeconds);
                bridge.RegisterSwarmWakeImpulse(position, Vector3.up * safeThermalUpdraft, safeThreatRadius, safeThreatHoldSeconds);
            }

            ElectrolysisAcousticEvent acousticEvent = new ElectrolysisAcousticEvent(
                position,
                consumedPowerWatts,
                oxygenUnits,
                safeThreatStrength,
                safeThreatRadius);
            ElectrolysisAcousticEvents.Notify(in acousticEvent);

            _debugLastDumpedPowerWatts = consumedPowerWatts;
            _debugLastOxygenUnits = oxygenUnits;
            _debugLastThreatStrength = safeThreatStrength;
        }

        /// <inheritdoc />
        public void OnPowerStatusChanged(bool hasPower)
        {
            _hasPower = hasPower;
            _debugHasPower = hasPower;

            if (hasPower || !_isOperating)
                return;

            _isOperating = false;
            _debugIsOperating = false;
            NotifyGridBalanceChanged();
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (powerNode == null)
                TryGetComponent(out powerNode);

            if (hostModule == null)
                hostModule = GetComponent<BaseModule>() ?? GetComponentInParent<BaseModule>();

            if (atmosphereSystem == null)
                atmosphereSystem = GetComponentInParent<SubmarineAtmosphereSystem>();

            if (fluidDynamics == null && atmosphereSystem != null)
                fluidDynamics = atmosphereSystem.GetComponent<SubmarineFluidDynamics>();
        }

        private bool ResolveWaterSourceAvailability()
        {
            if (fluidDynamics != null &&
                targetRoomIndex >= 0 &&
                targetRoomIndex < fluidDynamics.CompartmentCount &&
                fluidDynamics.GetCompartmentFloodVolumeCubicMeters(targetRoomIndex) >= math.max(0f, minimumFloodWaterVolumeCubicMeters))
            {
                return true;
            }

            if (hostModule != null && hostModule.IsFlooded)
                return true;

            if (!allowOceanWaterFallback)
                return false;

            IHectonOceanKinematicsService oceanService = GlobalRegistry.OceanKinematics;
            return oceanService != null && oceanService.ActiveProvider != null;
        }

        private Vector3 ResolveCinematicPulsePosition()
        {
            if (hostModule != null &&
                hostModule.TryGetInteriorAabbBounds(out Vector3 moduleCenter, out Vector3 moduleHalfExtents) &&
                IsFiniteVector(moduleCenter) &&
                moduleHalfExtents.sqrMagnitude > 0.0001f)
            {
                return moduleCenter;
            }

            if (fluidDynamics != null &&
                _cachedTransform != null &&
                targetRoomIndex >= 0 &&
                targetRoomIndex < fluidDynamics.CompartmentCount)
            {
                Vector3 roomCenter = _cachedTransform.TransformPoint(fluidDynamics.GetCompartmentCentroid(targetRoomIndex));
                if (IsFiniteVector(roomCenter))
                    return roomCenter;
            }

            if (_cachedTransform != null && IsFiniteVector(_cachedTransform.position))
                return _cachedTransform.position;

            return Vector3.zero;
        }

        private void NotifyGridBalanceChanged()
        {
            PowerGrid grid = powerNode != null ? powerNode.Grid : null;
            if (grid != null)
                grid.MarkDirty();
        }

        private void TryRegister()
        {
            if (_registered)
                return;
            if (!CanUseRuntimeDispatcher())
                return;

            GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
            _registered = GlobalRegistry.SlowTickables.Contains(this);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        private static bool CanUseRuntimeDispatcher()
        {
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
                return false;
#endif

            return true;
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float FiniteAtLeast(float value, float minimum)
        {
            return math.isfinite(value) && value > minimum ? value : minimum;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }
    }
}
