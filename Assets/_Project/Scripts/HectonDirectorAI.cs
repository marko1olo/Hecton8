using Hecton.Localization;
using Hecton8.AI;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.Visor;
using Hecton8.World;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Systems.AI
{
    /// <summary>
    /// Main-thread listener for deferred director encounter events.
    /// </summary>
    public interface IDirectorAIEventListener
    {
        /// <summary>Called when the director requests a swarm/horde response.</summary>
        void OnDirectorSpawnHordeRequested(Vector3 position);

        /// <summary>Called when the director requests an equipment glitch.</summary>
        void OnDirectorEquipmentGlitchRequested(float intensity);

        /// <summary>Called when the director requests rare-discovery routing.</summary>
        void OnDirectorRareDiscoveryRequested(Vector3 position);

        /// <summary>Called when the director requests a weather-pressure shift.</summary>
        void OnDirectorWeatherShiftRequested(float intensity);

        /// <summary>Called when the director requests mission routing.</summary>
        void OnDirectorMissionTriggerRequested(Vector3 position);

        /// <summary>Called when predator pressure enters or exits active pressure.</summary>
        void OnDirectorPredatorPressureChanged(bool pressureEnabled);

        /// <summary>Called when a predator enters an aggro spike that should hard-cut the combat mix.</summary>
        void OnDirectorThreatSpike(Vector3 position, float intensity);
    }

    /// <summary>
    /// Queue-backed event lane for DirectorAI outputs.
    /// </summary>
    public static class DirectorAIEvents
    {
        private struct DirectorAIEventPayload
        {
            public byte EventType;
            public Vector3 Position;
            public float Value;
            public byte BoolValue;
        }

        private const byte SpawnHordeEventType = 1;
        private const byte EquipmentGlitchEventType = 2;
        private const byte RareDiscoveryEventType = 3;
        private const byte WeatherShiftEventType = 4;
        private const byte MissionTriggerEventType = 5;
        private const byte PredatorPressureEventType = 6;
        private const byte ThreatSpikeEventType = 7;
        private const int ExpectedPendingEventCapacity = 24;
        private const int ListenerCapacity = 16;

        private static readonly RegistryBucket<IDirectorAIEventListener> _listeners = new RegistryBucket<IDirectorAIEventListener>(ListenerCapacity);
        private static NativeQueue<DirectorAIEventPayload> _pendingEvents;
        private static NativeQueue<DirectorAIEventPayload> _nextFrameEvents;
        private static int _pendingEventCount;
        private static int _nextFrameEventCount;
        private static bool _isDispatching;

        /// <summary>
        /// Number of queued director events awaiting LateUpdate dispatch.
        /// </summary>
        public static int PendingCount => _pendingEventCount + _nextFrameEventCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            if (_pendingEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(DirectorAIEvents), nameof(_pendingEvents));
                _pendingEvents.Dispose();
                _pendingEvents = default;
            }

            if (_nextFrameEvents.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(DirectorAIEvents), nameof(_nextFrameEvents));
                _nextFrameEvents.Dispose();
                _nextFrameEvents = default;
            }

            _listeners.Clear();
            _pendingEventCount = 0;
            _nextFrameEventCount = 0;
            _isDispatching = false;
        }

        /// <summary>
        /// Registers a main-thread DirectorAI event listener.
        /// </summary>
        public static void Register(IDirectorAIEventListener listener)
        {
            if (listener != null && !_listeners.Contains(listener))
                _listeners.Register(listener);
        }

        /// <summary>
        /// Unregisters a main-thread DirectorAI event listener.
        /// </summary>
        public static void Unregister(IDirectorAIEventListener listener)
        {
            if (listener != null && _listeners.Contains(listener))
                _listeners.Unregister(listener);
        }

        /// <summary>Queues a horde request.</summary>
        public static void RaiseSpawnHordeRequested(Vector3 position)
        {
            EnqueuePosition(SpawnHordeEventType, position);
        }

        /// <summary>Queues an equipment glitch request.</summary>
        public static void RaiseEquipmentGlitchRequested(float intensity)
        {
            EnqueueValue(EquipmentGlitchEventType, intensity);
        }

        /// <summary>Queues a rare-discovery request.</summary>
        public static void RaiseRareDiscoveryRequested(Vector3 position)
        {
            EnqueuePosition(RareDiscoveryEventType, position);
        }

        /// <summary>Queues a weather shift request.</summary>
        public static void RaiseWeatherShiftRequested(float intensity)
        {
            EnqueueValue(WeatherShiftEventType, intensity);
        }

        /// <summary>Queues a mission trigger request.</summary>
        public static void RaiseMissionTriggerRequested(Vector3 position)
        {
            EnqueuePosition(MissionTriggerEventType, position);
        }

        /// <summary>Queues a predator pressure state change.</summary>
        public static void RaisePredatorPressureChanged(bool pressureEnabled)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return;

            Enqueue(new DirectorAIEventPayload
            {
                EventType = PredatorPressureEventType,
                BoolValue = pressureEnabled ? (byte)1 : (byte)0
            });
        }

        /// <summary>Queues a predator threat spike.</summary>
        public static void RaiseThreatSpike(Vector3 position, float intensity)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return;

            Enqueue(new DirectorAIEventPayload
            {
                EventType = ThreatSpikeEventType,
                Position = position,
                Value = Mathf.Clamp01(intensity)
            });
        }

        /// <summary>
        /// Flushes queued director events on the main thread.
        /// </summary>
        public static void FlushPending()
        {
            if (!_pendingEvents.IsCreated)
                return;

            int scanBudget = _pendingEventCount > 0 ? _pendingEventCount : ExpectedPendingEventCapacity;
            _isDispatching = true;
            try
            {
                while (scanBudget-- > 0 && !_pendingEvents.IsEmpty())
                {
                    if (!SystemDispatcher.TryConsumeLateFrameEventDispatch())
                        return;

                    if (!_pendingEvents.TryDequeue(out DirectorAIEventPayload payload))
                        break;

                    if (_pendingEventCount > 0)
                        _pendingEventCount--;

                    Dispatch(in payload);
                }
            }
            finally
            {
                _isDispatching = false;
            }

            if (!_pendingEvents.IsEmpty())
                return;

            _pendingEventCount = 0;
            PromoteNextFrameEvents();
        }

        private static void EnqueuePosition(byte eventType, Vector3 position)
        {
            Enqueue(new DirectorAIEventPayload
            {
                EventType = eventType,
                Position = position
            });
        }

        private static void EnqueueValue(byte eventType, float value)
        {
            Enqueue(new DirectorAIEventPayload
            {
                EventType = eventType,
                Value = value
            });
        }

        private static bool Enqueue(in DirectorAIEventPayload payload)
        {
            EnsureInitialized();
            if (_pendingEventCount + _nextFrameEventCount >= ExpectedPendingEventCapacity)
                return false;

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

            return true;
        }

        private static void PromoteNextFrameEvents()
        {
            if (!_nextFrameEvents.IsCreated || _nextFrameEventCount <= 0)
                return;

            while (_nextFrameEventCount > 0 && _nextFrameEvents.TryDequeue(out DirectorAIEventPayload payload))
            {
                _nextFrameEventCount--;
                _pendingEvents.Enqueue(payload);
                _pendingEventCount++;
            }
        }

        private static void Dispatch(in DirectorAIEventPayload payload)
        {
            IDirectorAIEventListener[] rawListeners = _listeners.RawArray;
            int listenerCount = _listeners.Count;
            for (int i = listenerCount - 1; i >= 0; i--)
            {
                IDirectorAIEventListener listener = rawListeners[i];
                if (listener == null)
                    continue;

                switch (payload.EventType)
                {
                    case SpawnHordeEventType:
                        listener.OnDirectorSpawnHordeRequested(payload.Position);
                        break;
                    case EquipmentGlitchEventType:
                        listener.OnDirectorEquipmentGlitchRequested(payload.Value);
                        break;
                    case RareDiscoveryEventType:
                        listener.OnDirectorRareDiscoveryRequested(payload.Position);
                        break;
                    case WeatherShiftEventType:
                        listener.OnDirectorWeatherShiftRequested(payload.Value);
                        break;
                    case MissionTriggerEventType:
                        listener.OnDirectorMissionTriggerRequested(payload.Position);
                        break;
                    case PredatorPressureEventType:
                        listener.OnDirectorPredatorPressureChanged(payload.BoolValue != 0);
                        break;
                    case ThreatSpikeEventType:
                        listener.OnDirectorThreatSpike(payload.Position, payload.Value);
                        break;
                }
            }
        }

        private static void EnsureInitialized()
        {
            if (!_pendingEvents.IsCreated)
            {
                _pendingEvents = new NativeQueue<DirectorAIEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<DirectorAIEventPayload>[24] - deferred DirectorAI event lane flushed by SystemDispatcher - owner: DirectorAIEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingEvents,
                    ExpectedPendingEventCapacity,
                    nameof(DirectorAIEvents),
                    nameof(_pendingEvents),
                    NativeAllocationLifetime.Session);
            }

            if (!_nextFrameEvents.IsCreated)
            {
                _nextFrameEvents = new NativeQueue<DirectorAIEventPayload>(Allocator.Persistent); // COLD ALLOC: NativeQueue<DirectorAIEventPayload>[24] - next-frame DirectorAI events raised by listeners - owner: DirectorAIEvents
                NativeMemorySentinel.RegisterNativeQueue(
                    _nextFrameEvents,
                    ExpectedPendingEventCapacity,
                    nameof(DirectorAIEvents),
                    nameof(_nextFrameEvents),
                    NativeAllocationLifetime.Session);
            }
        }
    }

    /// <summary>
    /// One Burst-built predator sight ray input. Managed brain references stay outside this native lane.
    /// </summary>
    internal struct PredatorSightRaycastInput
    {
        public float3 Origin;
        public float3 Target;
        public float MaxDistance;
        public int LayerMask;
    }

    [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
    internal struct PredatorSightRaycastBuildJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PredatorSightRaycastInput> Inputs;
        public NativeArray<RaycastCommand> Commands;
        public int RequestCount;

        public void Execute(int index)
        {
            if (index >= RequestCount)
            {
                Commands[index] = new RaycastCommand(
                    Vector3.zero,
                    Vector3.forward,
                    new QueryParameters(HectonLayerMasks.NoLayers, false, QueryTriggerInteraction.Ignore, false),
                    0f);
                return;
            }

            PredatorSightRaycastInput input = Inputs[index];
            float3 delta = input.Target - input.Origin;
            float length = math.max(0.001f, math.length(delta));
            float3 direction = delta / length;
            Commands[index] = new RaycastCommand(
                new Vector3(input.Origin.x, input.Origin.y, input.Origin.z),
                new Vector3(direction.x, direction.y, direction.z),
                new QueryParameters(input.LayerMask, false, QueryTriggerInteraction.Ignore, false),
                math.min(input.MaxDistance, length));
        }
    }

    /// <summary>
    /// Scene-facing compatibility owner for the encounter pacing director.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4500)]
    public sealed class HectonDirectorAI : MonoBehaviour, IUpdatable, ILateFrameTickable, IEncounterDirectorService, ISonarPingEventListener, IAcousticPingEventListener
    {
        internal static HectonDirectorAI ActiveRuntimeInstance => GlobalRegistry.EncounterDirector as HectonDirectorAI;

        [Header("â”€â”€ References â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Authoritative player transform. Resolved from bootstrap when left null.")]
        [SerializeField] private Transform playerTransform;
        [Tooltip("Optional explicit gameplay camera. Resolved from the player hierarchy when left null.")]
        [SerializeField] private Camera playerCamera;
        [Tooltip("Player survival system used to feed the director stress inputs.")]
        [SerializeField] private HectonSurvivalSystem survivalSystem;
        [Tooltip("Fauna spawn owner. Resolved from the runtime reference utility when left null.")]
        [SerializeField] private FaunaDirector faunaDirector;
        [Tooltip("Optional authored encounter pacing profile. When assigned, spawn thresholds and critical-health suppression become data-driven.")]
        [SerializeField] private EncounterProfile encounterProfile;
        [Tooltip("Optional authored threat token cost table. When assigned, encounter token costs and simultaneous caps become data-driven.")]
        [SerializeField] private ThreatCostTable threatCostTable;

        [Header("â”€â”€ Event Output â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [Tooltip("Deterministic offset radius used for non-spawn director event hints.")]
        [SerializeField, Range(8f, 48f)] private float eventOffsetRadius = 25f;

#if UNITY_EDITOR
        [Header("â”€â”€ Diagnostics â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€")]
        [SerializeField] private float _debugStressLevel;
        [SerializeField] private float _debugIntensityLevel;
        [SerializeField] private float _debugTokenBudget;
        [SerializeField] private float _debugAverageFrameTimeMs;
        [SerializeField] private int _debugActiveEnemyCount;
        [SerializeField] private string _debugPhaseName;
#endif

        // COLD ALLOC: EncounterDirector[1] â€” dispatcher-driven encounter kernel â€” owner: HectonDirectorAI
        private readonly EncounterDirector _encounterDirector = new EncounterDirector();
        // COLD ALLOC: Plane[6] â€” reusable frustum plane scratch for zero-allocation camera extraction â€” owner: HectonDirectorAI
        private readonly Plane[] _frustumPlaneScratch = new Plane[EncounterDirector.FrustumPlaneCount];
        // COLD ALLOC: FrameTiming[1] â€” reusable frame-timing sample buffer â€” owner: HectonDirectorAI
        private readonly FrameTiming[] _frameTimingScratch = new FrameTiming[1];
        // COLD ALLOC: float[8] â€” rolling frame-time history for shed hysteresis â€” owner: HectonDirectorAI
        private readonly float[] _frameTimeHistory = new float[8];
        // COLD ALLOC: SpatialQueryHit[64] - AUP-filtered active-sonar leviathan aggro contacts - owner: HectonDirectorAI
        private readonly SpatialQueryHit[] _acousticPingPredatorContacts = new SpatialQueryHit[AcousticPingPredatorContactCapacity];
        // COLD ALLOC: SpatialQueryHit[10] - capped director LOS predator query contacts - owner: HectonDirectorAI
        private readonly SpatialQueryHit[] _predatorSightContacts = new SpatialQueryHit[PredatorSightMaxRaysPerFrame];
        // COLD ALLOC: FaunaBrain[10] - managed owner mirror for completed predator LOS rays - owner: HectonDirectorAI
        private readonly FaunaBrain[] _predatorSightBrains = new FaunaBrain[PredatorSightMaxRaysPerFrame];
        // COLD ALLOC: Vector3[10] - player-position mirror for completed predator LOS rays - owner: HectonDirectorAI
        private readonly Vector3[] _predatorSightPlayerPositions = new Vector3[PredatorSightMaxRaysPerFrame];
        // COLD ALLOC: Vector3[10] - player-forward mirror for completed predator LOS rays - owner: HectonDirectorAI
        private readonly Vector3[] _predatorSightPlayerForwards = new Vector3[PredatorSightMaxRaysPerFrame];
        private NativeArray<PredatorSightRaycastInput> _predatorSightInputs;
        private NativeArray<RaycastCommand> _predatorSightCommands;
        private NativeArray<RaycastHit> _predatorSightHits;
        private JobHandle _predatorSightRaycastHandle;
        private const float SonarStressDecayPerSecond = 0.18f;
        private const float ActiveSonarLeviathanAggroRadiusMeters = 1000f;
        private const double ActiveSonarLeviathanAggroRadiusMetersSqr =
            ActiveSonarLeviathanAggroRadiusMeters * ActiveSonarLeviathanAggroRadiusMeters;
        private const float ActiveSonarLeviathanAggroDurationSeconds = 10f;
        private const float ActiveSonarBoidScatterRadiusMeters = 90f;
        private const float ActiveSonarBoidScatterDurationSeconds = 0.85f;
        private const int AcousticPingPredatorContactCapacity = 64;
        private const int PredatorSightMaxRaysPerFrame = 10;
        private const float PredatorSightScanRadiusMeters = 220f;
        private const float PredatorSightProbeOffsetMeters = 0.75f;
        private const float PredatorSightHitToleranceMeters = 0.75f;
        private const float PredatorSightIntervalSeconds = 0.1f;
        private const float PredatorSightImmediateRevealRadiusMeters = 18f;
        private const double PredatorSightImmediateRevealRadiusMetersSqr =
            PredatorSightImmediateRevealRadiusMeters * PredatorSightImmediateRevealRadiusMeters;
        private const double DirectorSolveBudgetMilliseconds = 0.2d;
        private const float DirectorSolveWarningCooldownSeconds = 1f;
        private static readonly uint _DirectorSolveBudgetWarningHash =
            unchecked((uint)LocHash.Compute("DirectorAI.SolveBudgetExceeded"));
        private static readonly uint _DirectorTelemetryContextHash =
            unchecked((uint)LocHash.Compute("HectonDirectorAI"));
        private static readonly int PredatorSightLayerMask =
            HectonLayerMasks.TerrainLayerMask |
            HectonLayerMasks.BaseModuleLayerMask |
            HectonLayerMasks.VehicleLayerMask |
            HectonLayerMasks.VoxelCaveLayerMask |
            HectonLayerMasks.DebrisLayerMask;

        private HectonPlayerMovement _playerMovement;
        private bool _encounterDirectorServiceRegistered;
        private bool _dispatcherRegistered;
        private bool _lateFrameRegistered;
        private bool _acousticPingSubscribed;
        private bool _predatorSightBuffersRegistered;
        private bool _predatorSightJobScheduled;
        private float _resolveRetryTimer;
        private float _hunterSquadCooldown;
        private float _predatorSightCooldown;
        private float _nextDirectorSolveWarningTime;
        private int _frameTimeHistoryCount;
        private int _frameTimeHistoryIndex;
        private int _predatorSightScheduledCount;
        private Vector3 _previousPlayerPosition;
        private bool _hasPreviousPlayerPosition;
        private float _recentSonarStress;
        private float _externalPeakPressure01;
        private float _externalPeakHoldSeconds;
        private const float HunterSquadHostilityThreshold = 0.8f;
        private const float HunterSquadCooldownSeconds = 9f;
        private const int HunterSquadSize = 3;

        /// <summary>
        /// Current normalized director tension score in the legacy 0..100 presentation range.
        /// </summary>
        public float TensionScore => _encounterDirector.StressLevel * 100f;

        internal int CurrentPhaseIndex => (int)_encounterDirector.CurrentPhase;
        internal float CurrentStress01 => _encounterDirector.StressLevel;
        internal float CurrentIntensity01 => _encounterDirector.IntensityLevel;

        /// <summary>
        /// True while the director is in the Relax phase.
        /// </summary>
        public bool IsRelaxPhase => _encounterDirector.CurrentPhase == EncounterPhase.Relax;

        /// <summary>
        /// True while predator pressure is allowed to escalate.
        /// </summary>
        public bool IsPredatorPressureEnabled => _encounterDirector.CurrentPhase != EncounterPhase.Relax;

        /// <summary>
        /// Human-readable current phase name for legacy diagnostics consumers.
        /// </summary>
        public string CurrentPhaseName => _encounterDirector.CurrentPhaseName;

        /// <summary>
        /// True once the encounter director is registered in the global registry.
        /// </summary>
        public bool IsInitialized => ReferenceEquals(GlobalRegistry.EncounterDirector, this);

        private void Awake()
        {
            _encounterDirector.ApplyAuthoring(encounterProfile, threatCostTable);
            ResolveDependencies(force: true);
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!_encounterDirectorServiceRegistered)
            {
                GlobalRegistry.RegisterEncounterDirectorService(this);
                _encounterDirectorServiceRegistered = ReferenceEquals(GlobalRegistry.EncounterDirector, this);
            }

            if (GlobalRegistry.Dispatcher == null)
                return;

            if (!_dispatcherRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
                _dispatcherRegistered = GlobalRegistry.Updatables.Contains(this);
            }

            if (!_lateFrameRegistered)
            {
                GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = SystemDispatcher.GetLateFrameLane(PriorityLayer.Core).Contains(this);
            }

            _encounterDirector.Reset();
            _hasPreviousPlayerPosition = false;
            _recentSonarStress = 0f;
            _externalPeakPressure01 = 0f;
            _externalPeakHoldSeconds = 0f;
            _hunterSquadCooldown = 0f;
            _predatorSightCooldown = 0f;
            EnsurePredatorSightBuffersAllocated();
            SpectrumEvents.RegisterSonarPingListener(this);
            SubscribeAcousticPingEvents();
            PublishPredatorPressure(true);
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (_encounterDirectorServiceRegistered)
            {
                GlobalRegistry.UnregisterEncounterDirectorService(this);
                _encounterDirectorServiceRegistered = false;
            }

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                _dispatcherRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }

            SpectrumEvents.UnregisterSonarPingListener(this);
            UnsubscribeAcousticPingEvents();
            CompletePredatorSightBatch(forceComplete: true);
            _recentSonarStress = 0f;
            _externalPeakPressure01 = 0f;
            _externalPeakHoldSeconds = 0f;
            _hunterSquadCooldown = 0f;
            _predatorSightCooldown = 0f;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            _encounterDirector.ApplyAuthoring(encounterProfile, threatCostTable);
        }
#endif

        private void OnDestroy()
        {
            SpectrumEvents.UnregisterSonarPingListener(this);
            UnsubscribeAcousticPingEvents();

            if (_encounterDirectorServiceRegistered && ReferenceEquals(GlobalRegistry.EncounterDirector, this))
            {
                GlobalRegistry.UnregisterEncounterDirectorService(this);
                _encounterDirectorServiceRegistered = false;
            }

            if (_lateFrameRegistered)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                _lateFrameRegistered = false;
            }

            CompletePredatorSightBatch(forceComplete: true);
            ReleasePredatorSightBuffers();
            _encounterDirector.Dispose();
        }

        /// <summary>
        /// Executes one dispatcher step.
        /// </summary>
        /// <param name="deltaTime">Scaled frame delta supplied by the dispatcher.</param>
        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
                return;

            long solveStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
            ResolveDependencies(force: false);
            if (playerTransform == null)
                return;

            FrameTimingManager.CaptureFrameTimings();
            float averageFrameTimeMs = UpdateFrameTimeAverage(deltaTime);

            Vector3 playerPosition = playerTransform.position;
            Vector3 playerVelocity = ResolvePlayerVelocity(playerPosition, deltaTime);
            Vector3 playerForward = ResolvePlayerForward();
            float surfaceWorldY = ResolveSurfaceWorldY(playerPosition);
            float healthNormalized = survivalSystem != null ? Mathf.Clamp01(survivalSystem.IntegrityNormalized) : 1f;
            float oxygenNormalized = survivalSystem != null ? Mathf.Clamp01(survivalSystem.OxygenNormalized) : 1f;
            float sonarStress = UpdateSonarStress(deltaTime);
            float internalStress = ResolveInternalStress(healthNormalized, oxygenNormalized, sonarStress);
            float acousticThreatLevel = 0f;
            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null)
                acousticThreatLevel = Mathf.Clamp01(vegetationBridge.GetThreatLevel(playerPosition));
            acousticThreatLevel = Mathf.Max(acousticThreatLevel, sonarStress);
            ApplyExternalPeakPressure(deltaTime, ref internalStress, ref acousticThreatLevel);
            UpdateHunterSquadPressure(deltaTime);

            if (playerCamera != null)
                GeometryUtility.CalculateFrustumPlanes(playerCamera, _frustumPlaneScratch);
            else
                EncounterDirector.FillFallbackFrustumPlanes(playerPosition, playerForward, _frustumPlaneScratch);

            _encounterDirector.CopyFrustumPlanes(_frustumPlaneScratch);

            EncounterFrameContext frameContext = new EncounterFrameContext
            {
                DeltaTime = deltaTime,
                PlayerPosition = playerPosition,
                PlayerVelocity = playerVelocity,
                PlayerForward = playerForward,
                PlayerHealthNormalized = healthNormalized,
                PlayerOxygenNormalized = oxygenNormalized,
                PlayerInternalStress = internalStress,
                AcousticThreatLevel = acousticThreatLevel,
                PlayerDepth = ResolvePlayerDepth(playerPosition, surfaceWorldY),
                AvgFrameTimeMs = averageFrameTimeMs,
                SurfaceWorldY = surfaceWorldY
            };

            _encounterDirector.Advance(frameContext, faunaDirector, this);
            SchedulePredatorSightChecks(deltaTime, playerPosition, playerForward);

#if UNITY_EDITOR
            _debugStressLevel = _encounterDirector.StressLevel;
            _debugIntensityLevel = _encounterDirector.IntensityLevel;
            _debugTokenBudget = _encounterDirector.TokenBudget;
            _debugAverageFrameTimeMs = averageFrameTimeMs;
            _debugActiveEnemyCount = _encounterDirector.ActiveEnemyCount;
            _debugPhaseName = _encounterDirector.CurrentPhaseName;
#endif
            PublishDirectorSolveBudgetIfNeeded(solveStartTicks);
        }

        public void LateFrameTick()
        {
            CompletePredatorSightBatch(forceComplete: false);
        }

        public void OnAcousticPing(in AcousticPingEvent pingEvent)
        {
            HandleAcousticPing(in pingEvent);
        }

        private void SubscribeAcousticPingEvents()
        {
            if (_acousticPingSubscribed || !Application.isPlaying)
                return;

            PhysicsEventBus.Register((IAcousticPingEventListener)this);
            _acousticPingSubscribed = true;
        }

        private void UnsubscribeAcousticPingEvents()
        {
            if (!_acousticPingSubscribed)
                return;

            PhysicsEventBus.Unregister((IAcousticPingEventListener)this);
            _acousticPingSubscribed = false;
        }

        private void HandleAcousticPing(in AcousticPingEvent pingEvent)
        {
            if (pingEvent.RadiusMeters <= 0f || pingEvent.Intensity01 <= 0f)
                return;

            AbsoluteUniversePosition pingAup = AbsoluteUniversePosition.FromRuntimePosition(pingEvent.RuntimePosition);
            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in pingAup,
                ActiveSonarLeviathanAggroRadiusMeters,
                SpatialTargetKind.Bioform,
                _acousticPingPredatorContacts);

            bool raisedThreatSpike = false;
            for (int i = 0; i < contactCount; i++)
            {
                FaunaBrain brain = _acousticPingPredatorContacts[i].Owner as FaunaBrain;
                if (brain == null ||
                    brain.IsDead ||
                    !brain.IsApexPredatorRuntime)
                {
                    continue;
                }

                AbsoluteUniversePosition brainAup = AbsoluteUniversePosition.FromRuntimePosition(_acousticPingPredatorContacts[i].Position);
                if (AbsoluteUniversePosition.DistanceSq(in brainAup, in pingAup) > ActiveSonarLeviathanAggroRadiusMetersSqr)
                    continue;

                brain.ApplyAcousticPingAggro(
                    pingEvent.RuntimePosition,
                    pingEvent.Intensity01,
                    ActiveSonarLeviathanAggroDurationSeconds);

                SargassumMicroFaunaBoids boidSystem = GlobalRegistry.SargassumMicroFauna;
                if (boidSystem != null)
                {
                    Vector3 direction = _acousticPingPredatorContacts[i].Position - pingEvent.RuntimePosition;
                    if (direction.sqrMagnitude <= 0.0001f)
                        direction = brain.transform.forward;

                    boidSystem.RegisterLeviathanThreatPulse(
                        _acousticPingPredatorContacts[i].Position,
                        direction,
                        ActiveSonarBoidScatterRadiusMeters,
                        ActiveSonarBoidScatterDurationSeconds);
                }

                if (!raisedThreatSpike)
                {
                    DirectorAIEvents.RaiseThreatSpike(_acousticPingPredatorContacts[i].Position, pingEvent.Intensity01);
                    raisedThreatSpike = true;
                }
            }
        }

        private void SchedulePredatorSightChecks(float deltaTime, Vector3 playerPosition, Vector3 playerForward)
        {
            if (_predatorSightJobScheduled)
                return;

            if (_predatorSightCooldown > 0f)
            {
                _predatorSightCooldown = Mathf.Max(0f, _predatorSightCooldown - deltaTime);
                return;
            }

            EnsurePredatorSightBuffersAllocated();
            if (!_predatorSightInputs.IsCreated)
                return;

            _predatorSightCooldown = PredatorSightIntervalSeconds;
            AbsoluteUniversePosition playerAup = AbsoluteUniversePosition.FromRuntimePosition(playerPosition);
            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                in playerAup,
                PredatorSightScanRadiusMeters,
                SpatialTargetKind.Bioform,
                _predatorSightContacts);

            int requestCount = 0;
            Vector3 safePlayerForward = playerForward.sqrMagnitude > 0.0001f ? playerForward.normalized : Vector3.forward;
            Vector3 playerProbe = playerPosition + Vector3.up * PredatorSightProbeOffsetMeters;
            for (int i = 0; i < contactCount && requestCount < PredatorSightMaxRaysPerFrame; i++)
            {
                FaunaBrain brain = _predatorSightContacts[i].Owner as FaunaBrain;
                if (brain == null ||
                    brain.IsDead ||
                    (!brain.isAggressive && !brain.IsApexPredatorRuntime && !brain.UsesPackHuntBehavior))
                {
                    continue;
                }

                AbsoluteUniversePosition predatorAup =
                    AbsoluteUniversePosition.FromRuntimePosition(_predatorSightContacts[i].Position);
                if (AbsoluteUniversePosition.DistanceSq(in predatorAup, in playerAup) <= PredatorSightImmediateRevealRadiusMetersSqr)
                {
                    brain.ApplyDirectorLineOfSight(true, playerPosition, safePlayerForward);
                    continue;
                }

                Vector3 predatorProbe = _predatorSightContacts[i].Position + Vector3.up * PredatorSightProbeOffsetMeters;
                Vector3 toPlayer = playerProbe - predatorProbe;
                float distanceSqr = toPlayer.sqrMagnitude;
                if (distanceSqr <= 0.25f)
                    continue;

                float distance = Mathf.Sqrt(distanceSqr);
                _predatorSightInputs[requestCount] = new PredatorSightRaycastInput
                {
                    Origin = (float3)predatorProbe,
                    Target = (float3)playerProbe,
                    MaxDistance = distance,
                    LayerMask = PredatorSightLayerMask
                };
                _predatorSightBrains[requestCount] = brain;
                _predatorSightPlayerPositions[requestCount] = playerPosition;
                _predatorSightPlayerForwards[requestCount] = safePlayerForward;
                requestCount++;
            }

            if (requestCount <= 0)
                return;

            PredatorSightRaycastBuildJob buildJob = new PredatorSightRaycastBuildJob
            {
                Inputs = _predatorSightInputs,
                Commands = _predatorSightCommands,
                RequestCount = requestCount
            };

            JobHandle buildHandle = buildJob.Schedule(PredatorSightMaxRaysPerFrame, 1);
            _predatorSightRaycastHandle = RaycastCommand.ScheduleBatch(
                _predatorSightCommands,
                _predatorSightHits,
                1,
                buildHandle);
            _predatorSightScheduledCount = requestCount;
            _predatorSightJobScheduled = true;
        }

        private bool CompletePredatorSightBatch(bool forceComplete)
        {
            if (!_predatorSightJobScheduled)
                return true;

            if (!DispatcherJobSwap.TryComplete(ref _predatorSightRaycastHandle, forceComplete))
                return false;

            int count = Mathf.Min(_predatorSightScheduledCount, PredatorSightMaxRaysPerFrame);
            for (int i = 0; i < count; i++)
            {
                FaunaBrain brain = _predatorSightBrains[i];
                if (brain == null || brain.IsDead)
                    continue;

                RaycastHit hit = _predatorSightHits[i];
                PredatorSightRaycastInput input = _predatorSightInputs[i];
                bool blocked = hit.collider != null &&
                               hit.distance > 0f &&
                               hit.distance + PredatorSightHitToleranceMeters < input.MaxDistance;
                brain.ApplyDirectorLineOfSight(
                    !blocked,
                    _predatorSightPlayerPositions[i],
                    _predatorSightPlayerForwards[i]);
            }

            ClearPredatorSightMirrors(count);
            _predatorSightScheduledCount = 0;
            _predatorSightJobScheduled = false;
            return true;
        }

        private void ClearPredatorSightMirrors(int count)
        {
            int clampedCount = Mathf.Min(count, PredatorSightMaxRaysPerFrame);
            for (int i = 0; i < clampedCount; i++)
            {
                _predatorSightBrains[i] = null;
                _predatorSightPlayerPositions[i] = default;
                _predatorSightPlayerForwards[i] = default;
                _predatorSightInputs[i] = default;
                _predatorSightHits[i] = default;
            }
        }

        private void EnsurePredatorSightBuffersAllocated()
        {
            if (_predatorSightInputs.IsCreated)
                return;

            _predatorSightInputs = new NativeArray<PredatorSightRaycastInput>(PredatorSightMaxRaysPerFrame, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<PredatorSightRaycastInput>[10] - director predator sight build inputs - owner: HectonDirectorAI
            _predatorSightCommands = new NativeArray<RaycastCommand>(PredatorSightMaxRaysPerFrame, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastCommand>[10] - scheduled director predator sight commands - owner: HectonDirectorAI
            _predatorSightHits = new NativeArray<RaycastHit>(PredatorSightMaxRaysPerFrame, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[10] - scheduled director predator sight results - owner: HectonDirectorAI
            if (!_predatorSightBuffersRegistered)
            {
                NativeMemorySentinel.RegisterNativeArray(_predatorSightInputs, nameof(HectonDirectorAI), nameof(_predatorSightInputs), NativeAllocationLifetime.Scene);
                NativeMemorySentinel.RegisterNativeArray(_predatorSightCommands, nameof(HectonDirectorAI), nameof(_predatorSightCommands), NativeAllocationLifetime.Scene);
                NativeMemorySentinel.RegisterNativeArray(_predatorSightHits, nameof(HectonDirectorAI), nameof(_predatorSightHits), NativeAllocationLifetime.Scene);
                _predatorSightBuffersRegistered = true;
            }
        }

        private void ReleasePredatorSightBuffers()
        {
            if (_predatorSightInputs.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_predatorSightInputs);
                _predatorSightInputs.Dispose();
                _predatorSightInputs = default;
            }

            if (_predatorSightCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_predatorSightCommands);
                _predatorSightCommands.Dispose();
                _predatorSightCommands = default;
            }

            if (_predatorSightHits.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeArray(_predatorSightHits);
                _predatorSightHits.Dispose();
                _predatorSightHits = default;
            }

            _predatorSightBuffersRegistered = false;
        }

        private void PublishDirectorSolveBudgetIfNeeded(long solveStartTicks)
        {
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - solveStartTicks;
            double elapsedMilliseconds = (elapsedTicks * 1000.0d) / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds <= DirectorSolveBudgetMilliseconds)
                return;

            float now = Time.unscaledTime;
            if (now < _nextDirectorSolveWarningTime)
                return;

            _nextDirectorSolveWarningTime = now + DirectorSolveWarningCooldownSeconds;
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DirectorSolveBudgetWarningHash,
                _DirectorTelemetryContextHash,
                (float)math.min(elapsedMilliseconds, float.MaxValue));
        }

        /// <summary>
        /// Forces the next completed encounter tick into the Peak phase.
        /// </summary>
        public void ForcePeak()
        {
            _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);
        }

        /// <summary>
        /// Resets the runtime encounter state.
        /// </summary>
        public void ResetDirector()
        {
            _encounterDirector.RequestReset();
        }

        /// <summary>
        /// Forces the next completed encounter tick into the Relax phase.
        /// </summary>
        public void ForceRelax()
        {
            _encounterDirector.RequestPhaseOverride(EncounterPhase.Relax);
        }

        /// <summary>
        /// Applies external hostility pressure that extends Peak pacing without allocating auxiliary state machines.
        /// </summary>
        public void ApplyExternalPeakPressure(float pressure01, float holdSeconds)
        {
            float clampedPressure = Mathf.Clamp01(pressure01);
            if (clampedPressure <= 0f || holdSeconds <= 0f)
                return;

            _externalPeakPressure01 = Mathf.Max(_externalPeakPressure01, clampedPressure);
            _externalPeakHoldSeconds = Mathf.Max(_externalPeakHoldSeconds, holdSeconds);
        }

        /// <summary>
        /// Legacy predator registration hook retained for compatibility.
        /// </summary>
        /// <param name="collider">Predator collider.</param>
        public static void RegisterPredator(Collider collider)
        {
        }

        /// <summary>
        /// Legacy predator unregistration hook retained for compatibility.
        /// </summary>
        /// <param name="collider">Predator collider.</param>
        public static void UnregisterPredator(Collider collider)
        {
        }

        /// <summary>
        /// Legacy global predator registration clear retained for compatibility.
        /// </summary>
        public static void ClearAllPredatorRegistrations()
        {
        }

        internal void HandleEncounterPhaseChanged(EncounterPhase previousPhase, EncounterPhase newPhase)
        {
            PublishPredatorPressure(newPhase != EncounterPhase.Relax);

            if (playerTransform == null)
                return;

            uint seed = EncounterDirector.BuildDeterministicSeed(playerTransform.position, _encounterDirector.FrameIndex, (int)newPhase, _encounterDirector.ActiveEnemyCount);
            Vector3 eventPosition = ResolveDeterministicOffsetPosition(playerTransform.position, seed, eventOffsetRadius);

            switch (newPhase)
            {
                case EncounterPhase.Peak:
                    DirectorAIEvents.RaiseEquipmentGlitchRequested(Mathf.Lerp(0.35f, 0.85f, _encounterDirector.IntensityLevel));
                    DirectorAIEvents.RaiseMissionTriggerRequested(eventPosition);
                    break;

                case EncounterPhase.Decay:
                    DirectorAIEvents.RaiseWeatherShiftRequested(Mathf.Lerp(0.2f, 0.6f, _encounterDirector.StressLevel));
                    break;

                case EncounterPhase.Relax:
                    DirectorAIEvents.RaiseRareDiscoveryRequested(eventPosition);
                    break;
            }
        }

        internal void HandleThreatSpawned(EncounterThreatClass threatClass, Vector3 spawnPosition)
        {
            if (threatClass == EncounterThreatClass.Swarm)
                DirectorAIEvents.RaiseSpawnHordeRequested(spawnPosition);

            if (threatClass == EncounterThreatClass.Leviathan)
                DirectorAIEvents.RaiseThreatSpike(spawnPosition, 1f);
            else if (threatClass == EncounterThreatClass.Stalker)
                DirectorAIEvents.RaiseThreatSpike(spawnPosition, 0.75f);
        }

        private void ResolveDependencies(bool force)
        {
            if (!force && _resolveRetryTimer > 0f)
            {
                _resolveRetryTimer -= SystemDispatcher.CurrentFrameUnscaledDeltaTime;
                return;
            }

            _resolveRetryTimer = 1f;

            if (playerTransform == null)
                WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);

            if (survivalSystem == null && playerTransform != null)
                playerTransform.TryGetComponent(out survivalSystem);

            if (_playerMovement == null && playerTransform != null)
                playerTransform.TryGetComponent(out _playerMovement);

            if (faunaDirector == null)
                WorldRuntimeReferenceUtility.TryResolveFaunaDirector(ref faunaDirector);

            if (playerCamera == null && playerTransform != null)
                playerCamera = ((Hecton8.Core.GlobalRegistry.Player != null && Hecton8.Core.GlobalRegistry.Player.PlayerCamera != null) ? Hecton8.Core.GlobalRegistry.Player.PlayerCamera : playerTransform.GetComponent<Camera>());
        }

        private float UpdateFrameTimeAverage(float deltaTime)
        {
            uint timingCount = FrameTimingManager.GetLatestTimings(1u, _frameTimingScratch);
            float sampleMs = timingCount > 0u ? (float)_frameTimingScratch[0].cpuFrameTime : deltaTime * 1000f;
            if (sampleMs <= 0f)
                sampleMs = deltaTime * 1000f;

            _frameTimeHistory[_frameTimeHistoryIndex] = sampleMs;
            _frameTimeHistoryIndex++;
            if (_frameTimeHistoryIndex >= _frameTimeHistory.Length)
                _frameTimeHistoryIndex = 0;

            if (_frameTimeHistoryCount < _frameTimeHistory.Length)
                _frameTimeHistoryCount++;

            float sum = 0f;
            for (int i = 0; i < _frameTimeHistoryCount; i++)
                sum += _frameTimeHistory[i];

            return _frameTimeHistoryCount > 0 ? sum / _frameTimeHistoryCount : sampleMs;
        }

        private Vector3 ResolvePlayerVelocity(Vector3 playerPosition, float deltaTime)
        {
            Vector3 velocity = Vector3.zero;
            if (_hasPreviousPlayerPosition && TryResolveSafeReciprocal(deltaTime, out float inverseDeltaTime))
                velocity = SanitizeFiniteVector((playerPosition - _previousPlayerPosition) * inverseDeltaTime);

            _previousPlayerPosition = playerPosition;
            _hasPreviousPlayerPosition = true;
            return velocity;
        }

        private static bool TryResolveSafeReciprocal(float value, out float reciprocal)
        {
            if (!float.IsFinite(value) || Mathf.Abs(value) <= 0.0001f)
            {
                reciprocal = 0f;
                return false;
            }

            reciprocal = 1f / value;
            return float.IsFinite(reciprocal);
        }

        private static Vector3 SanitizeFiniteVector(Vector3 value)
        {
            return float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z)
                ? value
                : Vector3.zero;
        }

        private Vector3 ResolvePlayerForward()
        {
            if (playerCamera != null)
                return playerCamera.transform.forward;

            if (playerTransform != null)
                return playerTransform.forward;

            return Vector3.forward;
        }

        private float ResolveSurfaceWorldY(Vector3 playerPosition)
        {
            if (survivalSystem == null)
                return 0f;

            return playerPosition.y + survivalSystem.Depth;
        }

        private float ResolvePlayerDepth(Vector3 playerPosition, float surfaceWorldY)
        {
            if (survivalSystem != null)
                return Mathf.Max(0f, survivalSystem.Depth);

            return Mathf.Max(0f, surfaceWorldY - playerPosition.y);
        }

        private float ResolveInternalStress(float healthNormalized, float oxygenNormalized, float sonarStress)
        {
            if (survivalSystem == null)
                return Mathf.Clamp01(Mathf.Max(Mathf.Max(1f - healthNormalized, 1f - oxygenNormalized), sonarStress));

            float pressureStress = Mathf.Clamp01(survivalSystem.PressureExposureSeverity01);
            float thermalStress = Mathf.Clamp01(survivalSystem.ThermalStressSeverity01);
            float healthStress = 1f - healthNormalized;
            float oxygenStress = 1f - oxygenNormalized;
            return Mathf.Clamp01(Mathf.Max(Mathf.Max(pressureStress, thermalStress), Mathf.Max(Mathf.Max(healthStress, oxygenStress), sonarStress)));
        }

        private float UpdateSonarStress(float deltaTime)
        {
            if (_recentSonarStress <= 0f)
                return 0f;

            _recentSonarStress = Mathf.MoveTowards(_recentSonarStress, 0f, SonarStressDecayPerSecond * deltaTime);
            return _recentSonarStress;
        }

        private void HandleSonarPingSent(float intensity)
        {
            float clampedIntensity = Mathf.Clamp01(intensity);
            if (clampedIntensity <= 0f)
                return;

            _recentSonarStress = Mathf.Max(_recentSonarStress, clampedIntensity);
        }

        void ISonarPingEventListener.OnSonarPingSent(float intensity)
        {
            HandleSonarPingSent(intensity);
        }

        private void ApplyExternalPeakPressure(float deltaTime, ref float internalStress, ref float acousticThreatLevel)
        {
            if (_externalPeakHoldSeconds <= 0f || _externalPeakPressure01 <= 0f)
                return;

            _externalPeakHoldSeconds = Mathf.Max(0f, _externalPeakHoldSeconds - deltaTime);
            internalStress = Mathf.Max(internalStress, _externalPeakPressure01);
            acousticThreatLevel = Mathf.Max(acousticThreatLevel, _externalPeakPressure01);

            if (_encounterDirector.CurrentPhase != EncounterPhase.Peak)
                _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);

            if (_externalPeakHoldSeconds > 0f)
                return;

            _externalPeakPressure01 = 0f;
        }

        private void UpdateHunterSquadPressure(float deltaTime)
        {
            if (_hunterSquadCooldown > 0f)
                _hunterSquadCooldown = Mathf.Max(0f, _hunterSquadCooldown - deltaTime);

            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector == null || ecosystemDirector.BiomeHostility01 < HunterSquadHostilityThreshold)
                return;

            if (_hunterSquadCooldown > 0f)
                return;

            _encounterDirector.RequestPhaseOverride(EncounterPhase.Peak);
            _encounterDirector.RequestForcedSquad(EncounterThreatClass.Stalker, HunterSquadSize);
            _hunterSquadCooldown = HunterSquadCooldownSeconds;
        }

        private void PublishPredatorPressure(bool enabled)
        {
            if (faunaDirector != null)
                faunaDirector.SetPredatorPressure(enabled);

            DirectorAIEvents.RaisePredatorPressureChanged(enabled);
        }

        private Vector3 ResolveDeterministicOffsetPosition(Vector3 origin, uint seed, float radius)
        {
            float angle = EncounterDirector.HashToUnit01(seed ^ 0xA511E9B3u) * (Mathf.PI * 2f);
            float distance = Mathf.Lerp(radius * 0.4f, radius, EncounterDirector.HashToUnit01(seed ^ 0x6C8E9CF5u));
            Vector3 offset = new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance);
            return origin + offset;
        }
    }
}
