using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts;
using Hecton8.Logistics;
using Hecton8.Power;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Serialization;
using SubmarineFluidDynamics = Hecton8.Physics.SubmarineFluidDynamics;

namespace Hecton8.Gameplay
{
    /// <summary>
    /// Acoustic payload emitted when electrolysis boils surrounding water.
    /// </summary>
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public readonly struct ElectrolysisAcousticEvent
    {
        [FieldOffset(0)] public readonly Vector3 Position;
        [FieldOffset(12)] public readonly float DumpedPowerWatts;
        [FieldOffset(16)] public readonly float OxygenUnits;
        [FieldOffset(20)] public readonly float ThreatStrength;
        [FieldOffset(24)] public readonly float RadiusMeters;
        [FieldOffset(28)] private readonly uint _pad0;

        public ElectrolysisAcousticEvent(Vector3 position, float dumpedPowerWatts, float oxygenUnits, float threatStrength, float radiusMeters)
        {
            Position = IsFiniteVector(position) ? position : Vector3.zero;
            DumpedPowerWatts = FiniteNonNegativeOrZero(dumpedPowerWatts);
            OxygenUnits = FiniteNonNegativeOrZero(oxygenUnits);
            ThreatStrength = FiniteNonNegativeOrZero(threatStrength);
            RadiusMeters = FiniteAtLeast(radiusMeters, 1f);
            _pad0 = 0u;
        }

        private static bool IsFiniteVector(Vector3 value)
        {
            return math.isfinite(value.x) &&
                   math.isfinite(value.y) &&
                   math.isfinite(value.z);
        }

        private static float FiniteNonNegativeOrZero(float value)
        {
            return math.isfinite(value) && value > 0f ? value : 0f;
        }

        private static float FiniteAtLeast(float value, float minimum)
        {
            return math.isfinite(value) ? math.max(minimum, value) : minimum;
        }
    }

    /// <summary>
    /// NativeQueue-backed electrolysis acoustic event lane.
    /// </summary>
    public static class ElectrolysisAcousticEvents
    {
        private const int ListenerCapacity = 8;
        private const int PendingEventCapacity = 32;
        private const Allocator DataVaultExemptSignalLaneAllocator = Allocator.Persistent;

        private static readonly uint _overflowWarningHash = unchecked((uint)LocHash.Compute("ElectrolysisAcousticEvents.Overflow"));
        private static readonly uint _queueHash = unchecked((uint)LocHash.Compute("ElectrolysisAcousticEvents"));

        private struct ListenerSlot
        {
            public IElectrolysisAcousticEventListener Listener;

            public void Clear()
            {
                Listener = null;
            }
        }

        // COLD ALLOC: ListenerSlot[8] - electrolysis acoustic listeners drained by SystemDispatcher LateUpdate - owner: ElectrolysisAcousticEvents
        private static readonly ListenerSlot[] _listeners = new ListenerSlot[ListenerCapacity];

        private static NativeQueue<ElectrolysisAcousticPayload> _pendingEvents;
        private static NativeQueue<ElectrolysisAcousticPayload> _nextFrameEvents;
        private static int _listenerCount;
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

            for (int i = 0; i < ListenerCapacity; i++)
                _listeners[i].Clear();

            _listenerCount = 0;
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

            for (int i = 0; i < _listenerCount; i++)
            {
                if (ReferenceEquals(_listeners[i].Listener, listener))
                    return;
            }

            if (_listenerCount >= ListenerCapacity)
                return;

            _listeners[_listenerCount++].Listener = listener;
        }

        /// <summary>
        /// Unregisters an electrolysis acoustic listener.
        /// </summary>
        public static void Unregister(IElectrolysisAcousticEventListener listener)
        {
            if (listener == null)
                return;

            for (int i = 0; i < _listenerCount; i++)
            {
                if (!ReferenceEquals(_listeners[i].Listener, listener))
                    continue;

                int lastIndex = --_listenerCount;
                if (i != lastIndex)
                    _listeners[i].Listener = _listeners[lastIndex].Listener;

                _listeners[lastIndex].Clear();
                if (_listenerCount <= 0)
                    DropQueuedPayloads();
                return;
            }
        }

        /// <summary>
        /// Publishes one electrolysis acoustic payload to the deferred event lane.
        /// </summary>
        [System.Obsolete("Use TryNotify(in ElectrolysisAcousticEvent) so bounded queue rejection stays visible at the producer.", true)]
        public static void Notify(in ElectrolysisAcousticEvent acousticEvent)
        {
            TryNotify(in acousticEvent);
        }

        public static bool TryNotify(in ElectrolysisAcousticEvent acousticEvent)
        {
            if (_listenerCount <= 0)
                return false;

            return Enqueue(new ElectrolysisAcousticPayload
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

            if (_listenerCount <= 0)
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
                {
                    _pendingEventCount = 0;
                    break;
                }

                if (_pendingEventCount > 0)
                    _pendingEventCount--;

                ElectrolysisAcousticEvent acousticEvent = new ElectrolysisAcousticEvent(
                    payload.Position,
                    payload.DumpedPowerWatts,
                    payload.OxygenUnits,
                    payload.ThreatStrength,
                    payload.RadiusMeters);

                int count = _listenerCount;
                _isDispatching = true;
                try
                {
                    for (int i = count - 1; i >= 0; i--)
                    {
                        IElectrolysisAcousticEventListener listener = _listeners[i].Listener;
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
                _pendingEvents = new NativeQueue<ElectrolysisAcousticPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ElectrolysisAcousticPayload>[32] - deferred electrolysis acoustic lane flushed by SystemDispatcher LateUpdate - owner: ElectrolysisAcousticEvents
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
                _nextFrameEvents = new NativeQueue<ElectrolysisAcousticPayload>(DataVaultExemptSignalLaneAllocator); // COLD ALLOC: NativeQueue<ElectrolysisAcousticPayload>[32] - next-frame electrolysis acoustic lane prevents same-frame reentrant dispatch - owner: ElectrolysisAcousticEvents
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
            int frame = Hecton8.Core.SystemDispatcher.CurrentFrameIndex;
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
    [StructLayout(LayoutKind.Explicit, Size = 32)]
    public struct ElectrolysisAcousticPayload
    {
        [FieldOffset(0)] public Vector3 Position;
        [FieldOffset(12)] public float DumpedPowerWatts;
        [FieldOffset(16)] public float OxygenUnits;
        [FieldOffset(20)] public float ThreatStrength;
        [FieldOffset(24)] public float RadiusMeters;
        [FieldOffset(28)] private uint _pad0;
    }

    /// <summary>
    /// Grid-powered electrolysis stack that converts local seawater into breathable oxygen.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PowerNode))]
    [AddComponentMenu("Hecton/Gameplay/Submarine Electrolysis Module")]
    public sealed class SubmarineElectrolysisModule : MonoBehaviour, ISlowTickable, IPowerComponent, IGlobalRegistryHotSwapListener
    {
        private const float SlowTickDeltaTime = 0.5f;
        private const int InitialElectrolysisCapacity = 8;

        [Header("References")]
        [SerializeField, FormerlySerializedAs("atmosphereSystem")] private MonoBehaviour atmosphereSystemSource;
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

        [Header("Pipe Graph")]
        [SerializeField, Min(0.001f)] private float oxygenPipeCapacityUnits = 25f;
        [SerializeField, Min(0.1f)] private float oxygenPipeMaxPressureKPa = 120f;

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

        // COLD ALLOC: List<SubmarineElectrolysisModule>[8] - active electrolysis registry consumed by FluidPipeGraphRuntime - owner: SubmarineElectrolysisModule
        private static readonly List<SubmarineElectrolysisModule> s_activeModules = new List<SubmarineElectrolysisModule>(InitialElectrolysisCapacity);

        private Transform _cachedTransform;
        private IFluidPipeGraphService _pipeGraphService;
        private IHectonOceanKinematicsService _oceanKinematicsService;
        private bool _hasPower = true;
        private bool _hasWaterSource;
        private bool _isOperating;
        private bool _registered;
        private SystemDispatcher _cachedDispatcher;
        private int _oxygenPipeNodeIndex = -1;
        private float _pendingPipeOxygenUnits;
        private ISubmarineAtmosphereRoomMutationSink _atmosphereSystem;

        /// <inheritdoc />
        public float PowerRating => _isOperating ? -math.max(0f, powerDrawWatts) : 0f;

        /// <inheritdoc />
        public int PowerPriority => powerPriority;

        /// <inheritdoc />
        public bool HasPower => _hasPower;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetModuleStaticState()
        {
            s_activeModules.Clear();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            RegisterActiveModule();
            if (Application.isPlaying)
                GlobalRegistry.TryRegisterHotSwapListener(this);

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
            DisableOxygenPipeNode(forgetNode: false);
            UnregisterActiveModule();
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
        }

        private void OnDestroy()
        {
            DisableOxygenPipeNode(forgetNode: true);
            UnregisterActiveModule();
            TryUnregister();
            GlobalRegistry.TryUnregisterHotSwapListener(this);
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            if (!CanUseRuntimeDispatcher())
                return;

            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive || powerNode == null)
            {
                ResetOxygenPipeDemand();
                return;
            }

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
            {
                ResetOxygenPipeDemand();
                return;
            }

            float consumedPowerWatts = FiniteNonNegativeOrZero(powerDrawWatts);
            float oxygenUnits = (consumedPowerWatts * SlowTickDeltaTime * 0.001f) * FiniteNonNegativeOrZero(oxygenUnitsPerKilowattSecond);
            if (!math.isfinite(oxygenUnits) || oxygenUnits <= 0f)
            {
                ResetOxygenPipeDemand();
                return;
            }

            if (!TryQueuePipeOxygen(oxygenUnits))
                _atmosphereSystem.InjectOxygenUnits(targetRoomIndex, oxygenUnits);
            _atmosphereSystem.InjectRoomTemperatureDeltaCelsius(targetRoomIndex, FiniteNonNegativeOrZero(temperatureRisePerSlowTickCelsius));

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
            ElectrolysisAcousticEvents.TryNotify(in acousticEvent);

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
            ResetOxygenPipeDemand();
            NotifyGridBalanceChanged();
        }

        private void CacheReferences()
        {
            if (_cachedTransform == null)
                _cachedTransform = transform;

            if (powerNode == null)
                TryGetComponent(out powerNode);

            if (hostModule == null)
            {
                if (!TryGetComponent(out hostModule))
                    hostModule = GetComponentInParent<BaseModule>();
            }

            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
            {
                _atmosphereSystem = atmosphereSystemSource as ISubmarineAtmosphereRoomMutationSink;
                if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                    _atmosphereSystem = ComponentReferenceUtility.ResolveParentService<ISubmarineAtmosphereRoomMutationSink>(this);
            }

            if (fluidDynamics == null)
            {
                if (!TryGetComponent(out fluidDynamics))
                    fluidDynamics = ComponentReferenceUtility.ResolveParentService<SubmarineFluidDynamics>(this);
            }

            if (_pipeGraphService == null)
                _pipeGraphService = GlobalRegistry.FluidPipeGraph;
            if (_oceanKinematicsService == null)
                _oceanKinematicsService = GlobalRegistry.OceanKinematics;
            if (_cachedDispatcher == null)
                _cachedDispatcher = GlobalRegistry.Dispatcher;
        }

        internal static int ActiveElectrolysisCount => s_activeModules.Count;

        internal static SubmarineElectrolysisModule GetActiveElectrolysis(int index)
        {
            return index >= 0 && index < s_activeModules.Count ? s_activeModules[index] : null;
        }

        internal bool TryConsumePipeOxygenForGraph(IFluidPipeGraphService graph, out int nodeIndex, out float oxygenUnits)
        {
            nodeIndex = -1;
            oxygenUnits = 0f;
            if (_pendingPipeOxygenUnits <= 0f || !math.isfinite(_pendingPipeOxygenUnits))
                return false;

            if (!TryEnsureOxygenPipeNode(graph, out nodeIndex))
                return false;

            oxygenUnits = _pendingPipeOxygenUnits;
            _pendingPipeOxygenUnits = 0f;
            return true;
        }

        internal void RestorePipeOxygen(float oxygenUnits)
        {
            if (!math.isfinite(oxygenUnits) || oxygenUnits <= 0f)
                return;

            float next = _pendingPipeOxygenUnits + oxygenUnits;
            _pendingPipeOxygenUnits = math.isfinite(next) ? next : oxygenUnits;
        }

        private bool TryQueuePipeOxygen(float oxygenUnits)
        {
            if (!math.isfinite(oxygenUnits) || oxygenUnits <= 0f)
                return false;

            IFluidPipeGraphService graph = _pipeGraphService;
            if (graph == null || !graph.IsInitialized)
            {
                FlushPendingPipeOxygenToAtmosphere();
                return false;
            }

            if (!TryEnsureOxygenPipeNode(graph, out _))
            {
                FlushPendingPipeOxygenToAtmosphere();
                return false;
            }

            RestorePipeOxygen(oxygenUnits);
            return true;
        }

        private bool TryEnsureOxygenPipeNode(IFluidPipeGraphService graph, out int nodeIndex)
        {
            nodeIndex = -1;
            if (graph == null || !graph.IsInitialized)
                return false;

            if (!ReferenceEquals(_pipeGraphService, graph))
            {
                _pipeGraphService = graph;
                _oxygenPipeNodeIndex = -1;
            }

            if (_oxygenPipeNodeIndex >= 0 &&
                graph.TryReadPipeNode(_oxygenPipeNodeIndex, out _, out _, out byte cachedFlags))
            {
                byte requiredFlags = (byte)(FluidPipeFlags.Active | FluidPipeFlags.OxygenSource | FluidPipeFlags.RoomCoupled);
                if ((cachedFlags & (byte)FluidPipeFlags.Disabled) == 0 &&
                    (cachedFlags & (byte)FluidPipeFlags.Ruptured) == 0 &&
                    (cachedFlags & requiredFlags) == requiredFlags)
                {
                    nodeIndex = _oxygenPipeNodeIndex;
                    return true;
                }

                if ((cachedFlags & (byte)FluidPipeFlags.Ruptured) == 0 &&
                    graph.TrySetPipeNodeFlags(
                        _oxygenPipeNodeIndex,
                        requiredFlags,
                        (byte)FluidPipeFlags.Disabled))
                {
                    nodeIndex = _oxygenPipeNodeIndex;
                    return true;
                }
            }

            if (!TryResolveRuntimeAup(ResolveCinematicPulsePosition(), out AbsoluteUniversePosition nodeAup))
                return false;

            if (!graph.TryRegisterPipeNode(
                    ResolvePipeNetworkId(),
                    targetRoomIndex,
                    (byte)FluidPipeContentKind.Oxygen,
                    nodeAup,
                    math.max(0.001f, oxygenPipeCapacityUnits),
                    math.max(0.1f, oxygenPipeMaxPressureKPa),
                    out nodeIndex))
            {
                return false;
            }

            _oxygenPipeNodeIndex = nodeIndex;
            graph.TrySetPipeNodeFlags(
                nodeIndex,
                (byte)(FluidPipeFlags.Active | FluidPipeFlags.OxygenSource | FluidPipeFlags.RoomCoupled),
                (byte)FluidPipeFlags.Disabled);
            return true;
        }

        private static bool TryResolveRuntimeAup(Vector3 runtimePosition, out AbsoluteUniversePosition positionAup)
        {
            positionAup = default;
            float3 localRuntime = new float3(runtimePosition.x, runtimePosition.y, runtimePosition.z);
            if (!math.all(math.isfinite(localRuntime)))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!originAup.IsFinite())
                return false;

            positionAup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return positionAup.IsFinite();
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

            return _oceanKinematicsService != null && _oceanKinematicsService.ActiveProvider != null;
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

        private int ResolvePipeNetworkId()
        {
            if (_atmosphereSystem != null && _atmosphereSystem.IsAtmosphereRuntimeActive)
                return _atmosphereSystem.RuntimeEntityIdHash;
            if (hostModule != null)
                return unchecked((int)EntityId.ToULong(hostModule.GetEntityId()));

            return unchecked((int)EntityId.ToULong(GetEntityId()));
        }

        private void RegisterActiveModule()
        {
            if (!s_activeModules.Contains(this))
                s_activeModules.Add(this);
        }

        internal static void BindPipeGraphToActiveModules(IFluidPipeGraphService graph)
        {
            if (graph == null)
                return;

            for (int i = 0; i < s_activeModules.Count; i++)
            {
                SubmarineElectrolysisModule module = s_activeModules[i];
                if (module != null)
                    module.BindPipeGraphService(graph);
            }
        }

        internal static void ClearPipeGraphFromActiveModules(IFluidPipeGraphService graph)
        {
            if (graph == null)
                return;

            for (int i = 0; i < s_activeModules.Count; i++)
            {
                SubmarineElectrolysisModule module = s_activeModules[i];
                if (module != null)
                    module.ClearPipeGraphService(graph);
            }
        }

        private void UnregisterActiveModule()
        {
            for (int i = s_activeModules.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(s_activeModules[i], this))
                    s_activeModules.RemoveAt(i);
            }
        }

        private void BindPipeGraphService(IFluidPipeGraphService graph)
        {
            if (graph == null || ReferenceEquals(_pipeGraphService, graph))
                return;

            _pipeGraphService = graph;
            _oxygenPipeNodeIndex = -1;
        }

        private void ClearPipeGraphService(IFluidPipeGraphService graph)
        {
            if (!ReferenceEquals(_pipeGraphService, graph))
                return;

            FlushPendingPipeOxygenToAtmosphere();
            _pipeGraphService = null;
            _oxygenPipeNodeIndex = -1;
        }

        private void DisableOxygenPipeNode(bool forgetNode)
        {
            FlushPendingPipeOxygenToAtmosphere();
            if (_pipeGraphService != null && _oxygenPipeNodeIndex >= 0)
            {
                _pipeGraphService.TrySetPipeSourceRate(_oxygenPipeNodeIndex, 0f);
                _pipeGraphService.TrySetPipeDemandRate(_oxygenPipeNodeIndex, 0f);
                _pipeGraphService.TrySetPipeNodeFlags(
                    _oxygenPipeNodeIndex,
                    (byte)FluidPipeFlags.Disabled,
                    (byte)(FluidPipeFlags.OxygenSource | FluidPipeFlags.RoomCoupled));
            }

            if (forgetNode)
            {
                _pipeGraphService = null;
                _oxygenPipeNodeIndex = -1;
            }
        }

        private void ResetOxygenPipeDemand()
        {
            if (_pipeGraphService == null || _oxygenPipeNodeIndex < 0)
                return;

            _pipeGraphService.TrySetPipeDemandRate(_oxygenPipeNodeIndex, 0f);
        }

        internal void FlushPendingPipeOxygenToAtmosphere()
        {
            if (_atmosphereSystem == null || !_atmosphereSystem.IsAtmosphereRuntimeActive)
                CacheReferences();

            if (_pendingPipeOxygenUnits <= 0f ||
                !math.isfinite(_pendingPipeOxygenUnits) ||
                _atmosphereSystem == null ||
                !_atmosphereSystem.IsAtmosphereRuntimeActive)
            {
                _pendingPipeOxygenUnits = 0f;
                return;
            }

            _atmosphereSystem.InjectOxygenUnits(targetRoomIndex, _pendingPipeOxygenUnits);
            _pendingPipeOxygenUnits = 0f;
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

            _registered = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        private void TryUnregister()
        {
            if (!_registered)
                return;

            GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
            _registered = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
            {
                _cachedDispatcher = currentService as SystemDispatcher;
                _registered = false;
                if (currentService != null && isActiveAndEnabled)
                    TryStartRuntimeLifecycle();
            }
        }

        private bool CanUseRuntimeDispatcher()
        {
            if (!Application.isPlaying || _cachedDispatcher == null)
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
