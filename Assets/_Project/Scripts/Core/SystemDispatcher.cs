using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using System.Threading;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Construction;
using Hecton8.Core.Contracts;
using Hecton8.Core.Memory;
using Hecton8.Core.Signals;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Narrative;
using Hecton8.Optimization;
using Hecton8.Physics;
using Hecton8.Power;
using Hecton8.Quest;
using Hecton8.SaveSystem;
using Hecton8.Systems.AI;
using Hecton8.Visor;
using Hecton8.VFX;
using Hecton8.World;

namespace Hecton8.Core
{
    public readonly struct CriticalMemoryPressureEvent
    {
        public readonly int Frame;
        public readonly long ReservedMemoryBytes;
        public readonly long PhysicalMemoryBytes;
        public readonly double UsageRatio;

        public CriticalMemoryPressureEvent(
            int frame,
            long reservedMemoryBytes,
            long physicalMemoryBytes,
            double usageRatio)
        {
            Frame = frame;
            ReservedMemoryBytes = reservedMemoryBytes;
            PhysicalMemoryBytes = physicalMemoryBytes;
            UsageRatio = usageRatio;
        }
    }

    /// <summary>
    /// Priority-lane dispatcher for registry-managed <see cref="IUpdatable"/> systems.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9950)]
    public sealed class SystemDispatcher : MonoBehaviour, ITickDispatcher, IServiceHeartbeat, IServiceShutdown
    {
        private const int LaneCount = 4;
        private const double FastTickIntervalSeconds = 1.0 / 60.0;
        private const double SlowTickIntervalSeconds = 0.1;
        private const double ThermalCriticalSlowTickIntervalSeconds = 0.2;
        private const double ColdTickIntervalSeconds = 1.0;
        private const double FrostTickIntervalSeconds = 5.0;
        private const int MaxCadenceSubstepsPerFrame = 4;
        private const float TimeDilationMinimumScalar = 0f;
        private const float TimeDilationMaximumScalar = 4f;
        private const float HeadlessTimeDilationMaximumScalar = 100f;
        private const float TimeDilationPausedEpsilon = 0.0001f;
        private const float BulletTimePostScalarThreshold = 0.98f;
        private const double SlowJobCompleteWarningMilliseconds = 1.0;
        private const double SlowDispatcherPhaseWarningMilliseconds = 100.0;
        private const float JobAdmissionFrameBudgetMissThresholdSeconds = 1.0f / 60.0f;
        private const int MaxQueuedDispatcherRaycasts = 1024;
        private const int DispatcherRaycastMinCommandsPerJob = 1;
        private const double FixedStepSeconds = 0.02;
        private const int MaxFixedSubstepsPerFrame = 3;
        private const int MaxLateFrameEventsPerFrame = 1000;
        private const int MaxPdaEventsPerFrame = 30;
        private const int PdaCongestionWarningFrameThreshold = 5;
        private const int LateFrameCircuitBreakerLaneCapacity = 32;
        private const int BaseStressCascadeCircuitBreakerCapacity = 64;
        private const int MaxBaseStressCascadeEventsPerIslandPerFrame = 16;
        private const int ArteryFlushSampleCapacity = 64;
        private const double LateFrameEventFlushBudgetMilliseconds = 2.0;
        private const double LateFrameFlushPassSpikeMilliseconds = 0.5;
        private const float PauseDepthOfFieldBlendSeconds = 0.2f;
        private const float VisualStaticGlitchDurationSeconds = 1f;
        private const float SafeGcCollectFrameBudgetSeconds = 0.014f;
        private const double HomeostasisEmergencySlowTickIntervalSeconds = 0.5;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private const float AupNanInquisitorLogIntervalSeconds = 5f;
        private const float DispatcherPhaseWarningLogIntervalSeconds = 5f;
        private const string AupNanInquisitorWarningMessage = "[SystemDispatcher] AUP NaN-Inquisitor detected invalid camera-relative results.";
        private const string HeapLockGuardMessage = "[SystemDispatcher] HEAP LOCK GUARD: managed heap increased during fixed-step dispatch.";
#endif
        private static readonly ProfilerMarker _updateProfilerMarker = new ProfilerMarker("H8.Dispatcher.Update");
        private static readonly ProfilerMarker _fastTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.FastTick");
        private static readonly ProfilerMarker _fixedUpdateProfilerMarker = new ProfilerMarker("H8.Dispatcher.FixedUpdate");
        private static readonly ProfilerMarker _slowTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.SlowTick");
        private static readonly ProfilerMarker _coldTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.ColdTick");
        private static readonly ProfilerMarker _frostTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.FrostTick");
        private static readonly ProfilerMarker _memoryDefragProfilerMarker = new ProfilerMarker("H8.Dispatcher.MemoryDefrag.PreSimulation");
        private static readonly ProfilerMarker _lateFrameProfilerMarker = new ProfilerMarker("H8.Dispatcher.LateFrame");
        private static readonly ProfilerMarker _postFixedProfilerMarker = new ProfilerMarker("H8.Dispatcher.PostFixed");
        private static readonly ProfilerMarker _lateFrameCommandQueueDrainProfilerMarker = new ProfilerMarker("H8.Dispatcher.CommandQueue.Drain");
        private static readonly ProfilerMarker _foveatedCompleteProfilerMarker = new ProfilerMarker("H8.Dispatcher.Foveated.Complete");
        private static readonly ProfilerMarker _dispatcherRaycastScheduleProfilerMarker = new ProfilerMarker("H8.Dispatcher.Raycast.Schedule");
        private static readonly ProfilerMarker _dispatcherRaycastCompleteProfilerMarker = new ProfilerMarker("H8.Dispatcher.Raycast.Complete");
        private const uint _ThreadSafeCommandQueueHash = 2371163900u;
        private const uint _StorageReservationCommitResolvedQueueHash = 1402202258u;
        private const uint _ModCommandDispatcherQueueHash = 1692095755u;
        private const uint _CoreEventsArteryHash = 1115213285u;
        private const uint _EnvironmentEventsArteryHash = 907195043u;
        private const uint _PlayerEventsArteryHash = 4083807397u;
        private const uint _BaseEventsArteryHash = 1825517483u;
        private const uint _AIEventsArteryHash = 2440840446u;
        private const uint _PdaEventsQueueHash = 3608173543u;
        private const uint _LateFrameTickablesQueueHash = 1655194628u;
        private const uint _LateFrameBudgetWarningHash = 3118918745u;
        private const uint _AmbientEventsDropHash = 3299023854u;
        private const uint _CriticalPerformanceSpikeHash = 3729248491u;
        private const uint _DataVaultDefragContextHash = 0xDADA7048u;
        private const uint _HeapFragmentationRatioHash = 0xF9A60001u;
        private const uint _DataVaultMovedBytesHash = 0xDADA7049u;
        private const uint _DataVaultWatchdogHash = 0xDADA7050u;
        private const uint _DataVaultMassiveMoveHash = 0xDADA7051u;
        private const uint _DataVaultVramPressureHash = 0xDADA7052u;
        private const uint _BaseStressCascadeBreakerHash = 3838237614u;
        private static readonly int _HectonFreezeFrameDitherId = Shader.PropertyToID("_HectonFreezeFrameDither");
        private static readonly int _GamePausedId = Shader.PropertyToID("_GamePaused");
        private static readonly int _HectonVisualStaticGlitchId = Shader.PropertyToID("_HectonVisualStaticGlitch");
        private static readonly int _HectonVisualStaticGlitchSeedId = Shader.PropertyToID("_HectonVisualStaticGlitchSeed");
        private static readonly float[] _arteryFlushMilliseconds = new float[ArteryFlushSampleCapacity];
        private static int _arteryFlushSampleCursor;
        private static readonly ProfilerMarker[] _updateLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Update.Core"),
            new ProfilerMarker("H8.Dispatcher.Update.Environment"),
            new ProfilerMarker("H8.Dispatcher.Update.Player"),
            new ProfilerMarker("H8.Dispatcher.Update.UI"),
        };
        private static readonly ProfilerMarker[] _fixedLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Fixed.Core"),
            new ProfilerMarker("H8.Dispatcher.Fixed.Environment"),
            new ProfilerMarker("H8.Dispatcher.Fixed.Player"),
            new ProfilerMarker("H8.Dispatcher.Fixed.UI"),
        };
        private static readonly ProfilerMarker[] _fastLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Fast.Core"),
            new ProfilerMarker("H8.Dispatcher.Fast.Environment"),
            new ProfilerMarker("H8.Dispatcher.Fast.Player"),
            new ProfilerMarker("H8.Dispatcher.Fast.UI"),
        };
        private static readonly ProfilerMarker[] _slowLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Slow.Core"),
            new ProfilerMarker("H8.Dispatcher.Slow.Environment"),
            new ProfilerMarker("H8.Dispatcher.Slow.Player"),
            new ProfilerMarker("H8.Dispatcher.Slow.UI"),
        };
        private static readonly ProfilerMarker[] _coldLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Cold.Core"),
            new ProfilerMarker("H8.Dispatcher.Cold.Environment"),
            new ProfilerMarker("H8.Dispatcher.Cold.Player"),
            new ProfilerMarker("H8.Dispatcher.Cold.UI"),
        };
        private static readonly ProfilerMarker[] _frostLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Frost.Core"),
            new ProfilerMarker("H8.Dispatcher.Frost.Environment"),
            new ProfilerMarker("H8.Dispatcher.Frost.Player"),
            new ProfilerMarker("H8.Dispatcher.Frost.UI"),
        };
        private static readonly ProfilerMarker[] _unscaledFastLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.UnscaledFast.Core"),
            new ProfilerMarker("H8.Dispatcher.UnscaledFast.Environment"),
            new ProfilerMarker("H8.Dispatcher.UnscaledFast.Player"),
            new ProfilerMarker("H8.Dispatcher.UnscaledFast.UI"),
        };

        // COLD ALLOC: RegistryBucket<IUpdatable>[4] - fixed dispatcher lanes ordered by bootstrap layer - owner: SystemDispatcher
        private static readonly RegistryBucket<IUpdatable>[] _priorityLanes =
        {
            new RegistryBucket<IUpdatable>(256),
            new RegistryBucket<IUpdatable>(256),
            new RegistryBucket<IUpdatable>(128),
            new RegistryBucket<IUpdatable>(64),
        };
        private static readonly RegistryBucket<IFastTickable>[] _fastPriorityLanes =
        {
            new RegistryBucket<IFastTickable>(128),
            new RegistryBucket<IFastTickable>(128),
            new RegistryBucket<IFastTickable>(96),
            new RegistryBucket<IFastTickable>(32),
        };
        private static readonly RegistryBucket<IFixedTickable>[] _fixedPriorityLanes =
        {
            new RegistryBucket<IFixedTickable>(128),
            new RegistryBucket<IFixedTickable>(128),
            new RegistryBucket<IFixedTickable>(96),
            new RegistryBucket<IFixedTickable>(32),
        };
        private static readonly RegistryBucket<ISlowTickable>[] _slowPriorityLanes =
        {
            new RegistryBucket<ISlowTickable>(128),
            new RegistryBucket<ISlowTickable>(128),
            new RegistryBucket<ISlowTickable>(96),
            new RegistryBucket<ISlowTickable>(32),
        };
        private static int _bucketedSlowTickableCount;
        private static readonly RegistryBucket<IColdTickable>[] _coldPriorityLanes =
        {
            new RegistryBucket<IColdTickable>(64),
            new RegistryBucket<IColdTickable>(64),
            new RegistryBucket<IColdTickable>(48),
            new RegistryBucket<IColdTickable>(16),
        };
        private static readonly RegistryBucket<IFrostTickable>[] _frostPriorityLanes =
        {
            new RegistryBucket<IFrostTickable>(48),
            new RegistryBucket<IFrostTickable>(48),
            new RegistryBucket<IFrostTickable>(24),
            new RegistryBucket<IFrostTickable>(8),
        };
        private static readonly RegistryBucket<ILateFrameTickable>[] _lateFramePriorityLanes =
        {
            new RegistryBucket<ILateFrameTickable>(128),
            new RegistryBucket<ILateFrameTickable>(128),
            new RegistryBucket<ILateFrameTickable>(96),
            new RegistryBucket<ILateFrameTickable>(32),
        };
        private static readonly RegistryBucket<IPostFixedTickable>[] _postFixedPriorityLanes =
        {
            new RegistryBucket<IPostFixedTickable>(128),
            new RegistryBucket<IPostFixedTickable>(128),
            new RegistryBucket<IPostFixedTickable>(96),
            new RegistryBucket<IPostFixedTickable>(32),
        };
        private static readonly RegistryBucket<IUnscaledFastTickable>[] _unscaledFastPriorityLanes =
        {
            new RegistryBucket<IUnscaledFastTickable>(64),
            new RegistryBucket<IUnscaledFastTickable>(64),
            new RegistryBucket<IUnscaledFastTickable>(48),
            new RegistryBucket<IUnscaledFastTickable>(32),
        };

        private static IFoveatedDispatcher _foveatedSimulationManager = new FoveatedSimulationManager();
        private static long _homeostasisKillSwitchMaskBits;
        private static int _homeostasisPressureLevel;
        private static int _homeostasisSlowTick2Hz;
        private static int _homeostasisFoveatedTier;
        private static IModdingBridge _moddingBridgeProjectionRuntime;
        // COLD ALLOC: IDispatcherRaycastReceiver[256] - dispatcher-owned pending raycast receivers - owner: SystemDispatcher
        private static readonly IDispatcherRaycastReceiver[] _pendingDispatcherRaycastReceivers = new IDispatcherRaycastReceiver[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: int[256] - dispatcher-owned pending raycast request ids - owner: SystemDispatcher
        private static readonly int[] _pendingDispatcherRaycastRequestIds = new int[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: IDispatcherRaycastReceiver[256] - dispatcher-owned scheduled raycast receivers - owner: SystemDispatcher
        private static readonly IDispatcherRaycastReceiver[] _scheduledDispatcherRaycastReceivers = new IDispatcherRaycastReceiver[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: int[256] - dispatcher-owned scheduled raycast request ids - owner: SystemDispatcher
        private static readonly int[] _scheduledDispatcherRaycastRequestIds = new int[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: uint[32] - late-frame circuit-breaker lane hash counters - owner: SystemDispatcher
        private static readonly uint[] _lateFrameCircuitBreakerLaneHashes = new uint[LateFrameCircuitBreakerLaneCapacity];
        // COLD ALLOC: ushort[32] - late-frame circuit-breaker lane hit counters - owner: SystemDispatcher
        private static readonly ushort[] _lateFrameCircuitBreakerLaneCounts = new ushort[LateFrameCircuitBreakerLaneCapacity];
        // COLD ALLOC: int[64] - per-frame BaseEvents IslandID cascade breaker keys - owner: SystemDispatcher
        private static readonly int[] _baseStressCascadeIslandIds = new int[BaseStressCascadeCircuitBreakerCapacity];
        // COLD ALLOC: int[64] - lazy frame stamps for BaseEvents cascade breaker slots - owner: SystemDispatcher
        private static readonly int[] _baseStressCascadeSlotFrames = new int[BaseStressCascadeCircuitBreakerCapacity];
        // COLD ALLOC: ushort[64] - per-frame BaseEvents IslandID cascade event counts - owner: SystemDispatcher
        private static readonly ushort[] _baseStressCascadeIslandCounts = new ushort[BaseStressCascadeCircuitBreakerCapacity];
        // COLD ALLOC: ushort[64] - per-frame BaseEvents IslandID dropped event counts - owner: SystemDispatcher
        private static readonly ushort[] _baseStressCascadeDroppedCounts = new ushort[BaseStressCascadeCircuitBreakerCapacity];
        // COLD ALLOC: bool[64] - single telemetry gate per IslandID per frame - owner: SystemDispatcher
        private static readonly bool[] _baseStressCascadeTelemetryEmitted = new bool[BaseStressCascadeCircuitBreakerCapacity];
        private NativeArray<double> _h8Time;
        private VaultBufferHandle<double> _h8TimeHandle;
        private bool _h8TimeVaultOwned;
        private double _fastTickAccumulator;
        private double _slowTickAccumulator;
        private double _coldTickAccumulator;
        private double _frostTickAccumulator;
        private double _memoryDefragAccumulator;
        private double _unscaledFastTickAccumulator;
        private double _fixedStepAccumulator;
        private IDataVault _dataVault;
        private ISimulationBucketer _simulationBucketer;
        private float _timeDilationScalar = 1f;
        private float _prePauseTimeDilationScalar = 1f;
        private float _coreTickDilationScalar = 1f;
        private float _coreTickDilationRestoreScalar = 1f;
        private int _coreTickDilationFramesRemaining;
        private uint _coreTickDilationReasonHash;
        private bool _simulationPaused;
        private bool _thermalCriticalSlowTickActive;
        private uint _timeDilationSequence;
        private uint _lastPublishedTimeDilationSequence;
        private uint _aupPreShiftPauseSequence;
        private int _aupPreShiftPauseFrame = -1;
        private bool _coreTickDilationRestorePending;
        private H8TimeSnapshot _timeSnapshot;
        private bool _serviceRegistered;
        private int _lastMemoryDefragPressureWarningFrame = -1;
        private static int _lateFrameEventDispatchBudget;
        private static bool _lateFrameEventBudgetActive;
        private static bool _lateFrameCircuitBreakerTripped;
        private static bool _lateFrameTimeBudgetExhausted;
        private static uint _activeLateFrameEventLaneHash;
        private static uint _dominantLateFrameCircuitBreakerLaneHash;
        private static long _lateFrameEventBudgetStartTimestamp;
        private static bool _criticalPerformanceSpikeReported;
        private static bool _pauseFreezeFrameDitherActive;
        private static int _droppedEventsCounter;
        private static bool _visualStaticGlitchActive;
        private static float _visualStaticGlitchUntilTime;
        private static int _baseStressCascadeBreakerFrame = -1;
        private static int _baseStressCascadeTableOverflowTelemetryFrame = -1;
        private static int _originShiftBootstrapLockCount;
        private static int _originShiftFrameLockFrame = -1;
        private static int _criticalMemoryPressureDefragRequested;
        private static int _streamingStorageDebtMilli;
        private static int _streamingStorageDebtSequence;

        internal static float CurrentFrameDeltaTime { get; private set; }

        internal static float CurrentFrameUnscaledDeltaTime { get; private set; }

        internal static float CurrentFixedInterpolationAlpha { get; private set; }

        internal static SystemDispatcher ActiveRuntimeInstance { get; private set; }

        internal static bool IsOriginShiftBootstrapLocked => Volatile.Read(ref _originShiftBootstrapLockCount) > 0;

        internal static bool IsOriginShiftFrameLockedForCurrentFrame => Volatile.Read(ref _originShiftFrameLockFrame) == Time.frameCount;

        public float TimeDilationScalar => _timeDilationScalar;

        public static ulong KillSwitchMask => unchecked((ulong)Volatile.Read(ref _homeostasisKillSwitchMaskBits));

        internal static byte HomeostasisPressureLevel => (byte)Volatile.Read(ref _homeostasisPressureLevel);

        internal static byte HomeostasisFoveatedTier => (byte)Volatile.Read(ref _homeostasisFoveatedTier);

        public bool SimulationPaused => _simulationPaused || _timeDilationScalar <= TimeDilationPausedEpsilon;

        public double DilatedTimeSeconds => _timeSnapshot.Time;

        public double UnscaledTimeSeconds => _timeSnapshot.UnscaledTime;

        public H8TimeSnapshot TimeSnapshot => _timeSnapshot;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static object _currentPostFixedGcOwner;
        private static object _lastPostFixedGcOwner;
        private static int _lastPostFixedGcFrame = -1;
        private static int _lastPostFixedGcDelta;
        private static int _lastPostFixedGcLaneIndex = -1;
        private static int _lastPostFixedGcItemIndex = -1;
        private static float _nextAupNanInquisitorLogTime;
        private static float _nextDispatcherPhaseWarningLogTime;
        private static float _nextFoveatedFrameWarningLogTime;
#endif
        private static ushort _dominantLateFrameCircuitBreakerLaneCount;
        private static float _pauseDepthOfFieldWeight;
        private static float _pauseDepthOfFieldBlendStartTime;
        private static float _pauseDepthOfFieldBlendStartWeight;
        private static float _pauseDepthOfFieldTargetWeight;
        private static bool _temporalCompressionActive;
        private static bool _pauseMenuDepthOfFieldRequested;
        private static bool _pdaDepthOfFieldRequested;
        private static bool _pauseDepthOfFieldTargetActive;
        private static int _temporalCompressionFrameCount;
        private static int _pdaOverBudgetConsecutiveFrames;
        private static NativeQueue<RaycastCommand> _pendingDispatcherRaycastCommands;
        private static NativeList<RaycastCommand> _scheduledDispatcherRaycastCommands;
        private static NativeArray<RaycastHit> _scheduledDispatcherRaycastHits;
        private static VaultBufferHandle<RaycastHit> _scheduledDispatcherRaycastHitsHandle;
        private static bool _scheduledDispatcherRaycastHitsVaultOwned;
        private static bool _scheduledDispatcherRaycastHitsVaultLocked;
        private static JobHandle _scheduledDispatcherRaycastHandle;
        private static bool _dispatcherRaycastsScheduled;
        private static int _pendingDispatcherRaycastCount;
        private static int _scheduledDispatcherRaycastCount;

        static SystemDispatcher()
        {
            _foveatedSimulationManager.InitializeRuntime();
            ResetBaseStressCascadeCircuitBreakerState();
        }

        /// <summary>
        /// True when this frame dropped excess fixed-step catch-up time instead of exceeding the substep cap.
        /// </summary>
        public static bool IsTemporalCompressionActive => _temporalCompressionActive;

        /// <summary>
        /// Total frame count where temporal compression was entered since subsystem reset.
        /// </summary>
        public static int TemporalCompressionFrameCount => _temporalCompressionFrameCount;

        public static float StreamingStorageDebt01 => math.saturate(Volatile.Read(ref _streamingStorageDebtMilli) * 0.001f);

        public static uint StreamingStorageDebtSequence => unchecked((uint)Volatile.Read(ref _streamingStorageDebtSequence));

        public static double CurrentUnscaledTimeSeconds
        {
            get
            {
                SystemDispatcher dispatcher = ActiveRuntimeInstance;
                return dispatcher != null ? dispatcher._timeSnapshot.UnscaledTime : Time.unscaledTimeAsDouble;
            }
        }

        public static void PublishStreamingStorageDebt(float debt01)
        {
            Volatile.Write(ref _streamingStorageDebtMilli, (int)math.round(math.saturate(debt01) * 1000f));
            Volatile.Write(ref _streamingStorageDebtSequence, unchecked(Volatile.Read(ref _streamingStorageDebtSequence) + 1));
        }

        internal static void SetModdingBridgeProjectionRuntime(IModdingBridge bridge)
        {
            if (bridge != null)
                _moddingBridgeProjectionRuntime = bridge;
        }

        internal static void ClearModdingBridgeProjectionRuntime(IModdingBridge bridge)
        {
            if (ReferenceEquals(_moddingBridgeProjectionRuntime, bridge))
                _moddingBridgeProjectionRuntime = null;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static bool TryGetLastPostFixedGcAttribution(
            out string ownerName,
            out int frame,
            out int delta,
            out int laneIndex,
            out int itemIndex)
        {
            object owner = _lastPostFixedGcOwner ?? _currentPostFixedGcOwner;
            if (owner == null)
            {
                ownerName = "UNATTRIBUTED_POSTFIXED_SYSTEM";
                frame = _lastPostFixedGcFrame;
                delta = _lastPostFixedGcDelta;
                laneIndex = _lastPostFixedGcLaneIndex;
                itemIndex = _lastPostFixedGcItemIndex;
                return false;
            }

            ownerName = owner.GetType().Name;
            frame = _lastPostFixedGcFrame;
            delta = _lastPostFixedGcDelta;
            laneIndex = _lastPostFixedGcLaneIndex;
            itemIndex = _lastPostFixedGcItemIndex;
            return true;
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            HomeostasisBrain.ShutdownRuntime();
            _foveatedSimulationManager.Dispose();
            _foveatedSimulationManager = new FoveatedSimulationManager();
            _foveatedSimulationManager.InitializeRuntime();
            DisposeDispatcherRaycastBuffers();
            ThreadSafeCommandQueue.Shutdown();
            ClearAllLanes();
            _lateFrameEventDispatchBudget = 0;
            _lateFrameEventBudgetActive = false;
            _lateFrameCircuitBreakerTripped = false;
            _lateFrameTimeBudgetExhausted = false;
            _activeLateFrameEventLaneHash = 0u;
            _dominantLateFrameCircuitBreakerLaneHash = 0u;
            _lateFrameEventBudgetStartTimestamp = 0L;
            _dominantLateFrameCircuitBreakerLaneCount = 0;
            _arteryFlushSampleCursor = 0;
            System.Array.Clear(_lateFrameCircuitBreakerLaneHashes, 0, _lateFrameCircuitBreakerLaneHashes.Length);
            System.Array.Clear(_lateFrameCircuitBreakerLaneCounts, 0, _lateFrameCircuitBreakerLaneCounts.Length);
            ResetBaseStressCascadeCircuitBreakerState();
            System.Array.Clear(_arteryFlushMilliseconds, 0, _arteryFlushMilliseconds.Length);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _nextAupNanInquisitorLogTime = 0f;
            _nextDispatcherPhaseWarningLogTime = 0f;
            _nextFoveatedFrameWarningLogTime = 0f;
#endif
            _pauseDepthOfFieldWeight = 0f;
            _pauseDepthOfFieldBlendStartTime = 0f;
            _pauseDepthOfFieldBlendStartWeight = 0f;
            _pauseDepthOfFieldTargetWeight = 0f;
            _pauseMenuDepthOfFieldRequested = false;
            _pdaDepthOfFieldRequested = false;
            _pauseDepthOfFieldTargetActive = false;
            _pauseFreezeFrameDitherActive = false;
            _droppedEventsCounter = 0;
            _visualStaticGlitchActive = false;
            _visualStaticGlitchUntilTime = 0f;
            Volatile.Write(ref _streamingStorageDebtMilli, 0);
            Volatile.Write(ref _streamingStorageDebtSequence, 0);
            Volatile.Write(ref _originShiftBootstrapLockCount, 0);
            Volatile.Write(ref _originShiftFrameLockFrame, -1);
            Shader.SetGlobalFloat(_HectonFreezeFrameDitherId, 0f);
            Shader.SetGlobalFloat(_GamePausedId, 0f);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, 0f);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, 0f);
            _criticalPerformanceSpikeReported = false;
            _temporalCompressionActive = false;
            _temporalCompressionFrameCount = 0;
            _pdaOverBudgetConsecutiveFrames = 0;
            _moddingBridgeProjectionRuntime = null;
            Volatile.Write(ref _homeostasisKillSwitchMaskBits, 0L);
            Volatile.Write(ref _homeostasisPressureLevel, 0);
            Volatile.Write(ref _homeostasisSlowTick2Hz, 0);
            Volatile.Write(ref _homeostasisFoveatedTier, 0);
            ActiveRuntimeInstance = null;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _currentPostFixedGcOwner = null;
            _lastPostFixedGcOwner = null;
            _lastPostFixedGcFrame = -1;
            _lastPostFixedGcDelta = 0;
            _lastPostFixedGcLaneIndex = -1;
            _lastPostFixedGcItemIndex = -1;
#endif
        }

        internal static void ApplyHomeostasisKillSwitch(
            ulong mask,
            byte pressureLevel,
            byte foveatedTier,
            bool slowTick2Hz,
            bool forceTimeDilation08,
            uint reasonHash)
        {
            Volatile.Write(ref _homeostasisKillSwitchMaskBits, unchecked((long)mask));
            Volatile.Write(ref _homeostasisPressureLevel, pressureLevel);
            Volatile.Write(ref _homeostasisSlowTick2Hz, slowTick2Hz ? 1 : 0);
            Volatile.Write(ref _homeostasisFoveatedTier, foveatedTier);
            _foveatedSimulationManager.ApplyHomeostasisPressureTier(foveatedTier);

            if (forceTimeDilation08 && ActiveRuntimeInstance != null)
                ActiveRuntimeInstance.RequestCoreTickDilation(0.8f, 2, reasonHash);
        }

        /// <summary>
        /// Drops same-frame habitat damage/stress cascades once one IslandID exceeds the hard budget.
        /// </summary>
        /// <param name="islandId">Logistics component IslandID. Negative values are coerced to zero.</param>
        /// <param name="eventHash">Stable event hash for telemetry context.</param>
        /// <returns>True when the caller may process this cascade event.</returns>
        internal static bool TryConsumeBaseStressCascadeEvent(int islandId, uint eventHash)
        {
            int currentFrame = Time.frameCount;
            if (_baseStressCascadeBreakerFrame != currentFrame)
                _baseStressCascadeBreakerFrame = currentFrame;

            int safeIslandId = math.max(0, islandId);
            int slot = ResolveBaseStressCascadeSlot(safeIslandId);
            if (slot < 0)
            {
                if (_baseStressCascadeTableOverflowTelemetryFrame != currentFrame)
                {
                    _baseStressCascadeTableOverflowTelemetryFrame = currentFrame;
                    CrashTelemetryBuffer.ReportEventCascadeWarning();
                    GlobalTelemetryBus.PublishCatastrophicCascadePrevented(
                        unchecked((uint)safeIslandId),
                        eventHash != 0u ? eventHash : _BaseStressCascadeBreakerHash,
                        1);
                }

                return false;
            }

            ushort currentCount = _baseStressCascadeIslandCounts[slot];
            if (currentCount < MaxBaseStressCascadeEventsPerIslandPerFrame)
            {
                _baseStressCascadeIslandCounts[slot] = (ushort)(currentCount + 1);
                return true;
            }

            ushort droppedCount = _baseStressCascadeDroppedCounts[slot];
            if (droppedCount < ushort.MaxValue)
                droppedCount++;

            _baseStressCascadeDroppedCounts[slot] = droppedCount;
            if (!_baseStressCascadeTelemetryEmitted[slot])
            {
                _baseStressCascadeTelemetryEmitted[slot] = true;
                CrashTelemetryBuffer.ReportEventCascadeWarning();
                GlobalTelemetryBus.PublishCatastrophicCascadePrevented(
                    unchecked((uint)safeIslandId),
                    eventHash != 0u ? eventHash : _BaseStressCascadeBreakerHash,
                    droppedCount);
            }

            return false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static void ResetBaseStressCascadeCircuitBreakerForSmokeTest()
        {
            ResetBaseStressCascadeCircuitBreakerStateForFrame(Time.frameCount);
        }

        internal static int DebugGetBaseStressCascadeDroppedCount(int islandId)
        {
            int safeIslandId = math.max(0, islandId);
            int slot = FindBaseStressCascadeSlot(safeIslandId);
            return slot >= 0 ? _baseStressCascadeDroppedCounts[slot] : 0;
        }

        internal static int DebugGetBaseStressCascadeConsumedCount(int islandId)
        {
            int safeIslandId = math.max(0, islandId);
            int slot = FindBaseStressCascadeSlot(safeIslandId);
            return slot >= 0 ? _baseStressCascadeIslandCounts[slot] : 0;
        }

        internal static int DebugGetBaseStressCascadeActiveSlotCount()
        {
            int activeSlotCount = 0;
            for (int i = 0; i < BaseStressCascadeCircuitBreakerCapacity; i++)
            {
                if (_baseStressCascadeSlotFrames[i] == _baseStressCascadeBreakerFrame)
                    activeSlotCount++;
            }

            return activeSlotCount;
        }
#endif

        private static void ResetBaseStressCascadeCircuitBreakerState()
        {
            ResetBaseStressCascadeCircuitBreakerStateForFrame(-1);
        }

        private static void ResetBaseStressCascadeCircuitBreakerStateForFrame(int frame)
        {
            _baseStressCascadeBreakerFrame = frame;
            _baseStressCascadeTableOverflowTelemetryFrame = -1;
            for (int i = 0; i < BaseStressCascadeCircuitBreakerCapacity; i++)
            {
                _baseStressCascadeIslandIds[i] = int.MinValue;
                _baseStressCascadeSlotFrames[i] = int.MinValue;
                _baseStressCascadeIslandCounts[i] = 0;
                _baseStressCascadeDroppedCounts[i] = 0;
                _baseStressCascadeTelemetryEmitted[i] = false;
            }
        }

        private static int ResolveBaseStressCascadeSlot(int islandId)
        {
            int slot = FindBaseStressCascadeSlot(islandId);
            if (slot >= 0)
                return slot;

            int startSlot = islandId & (BaseStressCascadeCircuitBreakerCapacity - 1);
            for (int probe = 0; probe < BaseStressCascadeCircuitBreakerCapacity; probe++)
            {
                int candidateSlot = (startSlot + probe) & (BaseStressCascadeCircuitBreakerCapacity - 1);
                if (_baseStressCascadeSlotFrames[candidateSlot] == _baseStressCascadeBreakerFrame)
                    continue;

                _baseStressCascadeIslandIds[candidateSlot] = islandId;
                _baseStressCascadeSlotFrames[candidateSlot] = _baseStressCascadeBreakerFrame;
                _baseStressCascadeIslandCounts[candidateSlot] = 0;
                _baseStressCascadeDroppedCounts[candidateSlot] = 0;
                _baseStressCascadeTelemetryEmitted[candidateSlot] = false;
                return candidateSlot;
            }

            return -1;
        }

        private static int FindBaseStressCascadeSlot(int islandId)
        {
            int startSlot = islandId & (BaseStressCascadeCircuitBreakerCapacity - 1);
            for (int probe = 0; probe < BaseStressCascadeCircuitBreakerCapacity; probe++)
            {
                int candidateSlot = (startSlot + probe) & (BaseStressCascadeCircuitBreakerCapacity - 1);
                if (_baseStressCascadeSlotFrames[candidateSlot] != _baseStressCascadeBreakerFrame)
                    return -1;

                int candidateIsland = _baseStressCascadeIslandIds[candidateSlot];
                if (candidateIsland == islandId)
                    return candidateSlot;
            }

            return -1;
        }

        internal static void SetVoxelTeardownBackpressure(bool active, int pendingChunkCount)
        {
            _foveatedSimulationManager.SetVoxelTeardownBackpressure(active, pendingChunkCount);
        }

        /// <summary>
        /// Requests pause-menu depth-of-field isolation on the dispatcher cadence.
        /// </summary>
        public static void RequestPauseDepthOfField(bool active)
        {
            _pauseMenuDepthOfFieldRequested = active;
            RefreshPauseDepthOfFieldTarget();
        }

        /// <summary>
        /// Requests PDA depth-of-field isolation without overriding the pause menu request.
        /// </summary>
        public static void RequestPdaDepthOfField(bool active)
        {
            _pdaDepthOfFieldRequested = active;
            RefreshPauseDepthOfFieldTarget();
        }

        internal static int DroppedEventsCounter => _droppedEventsCounter;

        internal static bool QueueDispatcherRaycast(IDispatcherRaycastReceiver receiver, int requestId, in RaycastCommand command)
        {
            if (receiver == null)
                return false;

            SystemDispatcher dispatcher = GlobalRegistry.Dispatcher;
            if (dispatcher == null)
                return false;

            EnsureDispatcherRaycastBuffers();
            if (!_pendingDispatcherRaycastCommands.IsCreated || _pendingDispatcherRaycastCount >= MaxQueuedDispatcherRaycasts)
                return false;

            int writeIndex = _pendingDispatcherRaycastCount++;
            _pendingDispatcherRaycastReceivers[writeIndex] = receiver;
            _pendingDispatcherRaycastRequestIds[writeIndex] = requestId;
            _pendingDispatcherRaycastCommands.Enqueue(command);
            return true;
        }

        /// <summary>
        /// Returns the registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense lane bucket.</returns>
        public static RegistryBucket<IUpdatable> GetLane(PriorityLayer layer)
        {
            return _priorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the 60 Hz fast-tick registry lane for a fixed priority layer.
        /// </summary>
        public static RegistryBucket<IFastTickable> GetFastLane(PriorityLayer layer)
        {
            return _fastPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the fixed-step registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense fixed-step lane bucket.</returns>
        public static RegistryBucket<IFixedTickable> GetFixedLane(PriorityLayer layer)
        {
            return _fixedPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the slow-tick registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense slow-tick lane bucket.</returns>
        public static RegistryBucket<ISlowTickable> GetSlowLane(PriorityLayer layer)
        {
            return _slowPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the 1 Hz cold-tick registry lane for a fixed priority layer.
        /// </summary>
        public static RegistryBucket<IColdTickable> GetColdLane(PriorityLayer layer)
        {
            return _coldPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the frost maintenance registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense frost-tick lane bucket.</returns>
        public static RegistryBucket<IFrostTickable> GetFrostLane(PriorityLayer layer)
        {
            return _frostPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the late-frame registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense late-frame lane bucket.</returns>
        public static RegistryBucket<ILateFrameTickable> GetLateFrameLane(PriorityLayer layer)
        {
            return _lateFramePriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the post-fixed-step registry lane for a fixed priority layer.
        /// </summary>
        /// <param name="layer">Priority lane.</param>
        /// <returns>Dense post-fixed lane bucket.</returns>
        public static RegistryBucket<IPostFixedTickable> GetPostFixedLane(PriorityLayer layer)
        {
            return _postFixedPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Returns the unscaled UI/menu fast-tick registry lane for a fixed priority layer.
        /// </summary>
        public static RegistryBucket<IUnscaledFastTickable> GetUnscaledFastLane(PriorityLayer layer)
        {
            return _unscaledFastPriorityLanes[GetLaneIndex(layer)];
        }

        /// <summary>
        /// Registers an update owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!GetLane(layer).TryRegister(item))
                return false;

            if (item is IFoveatedSimulationTarget foveatedTarget)
                _foveatedSimulationManager.RegisterTarget(foveatedTarget);

            return true;
        }

        /// <summary>
        /// Registers a 60 Hz fast-tick owner into a fixed priority lane.
        /// </summary>
        internal static bool Register(IFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetFastLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a fixed-update owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetFixedLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a slow-tick owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            if (!GetSlowLane(layer).TryRegister(item))
                return false;

            if (item is IBucketedSlowTickable)
                _bucketedSlowTickableCount++;

            return true;
        }

        /// <summary>
        /// Registers a 1 Hz cold-tick owner into a fixed priority lane.
        /// </summary>
        internal static bool Register(IColdTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetColdLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a frost maintenance owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Frost-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(IFrostTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetFrostLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a late-frame owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetLateFrameLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers a post-fixed-step owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static bool Register(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetPostFixedLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Registers an unscaled fast-tick owner into a fixed priority lane.
        /// </summary>
        internal static bool Register(IUnscaledFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return false;

            return GetUnscaledFastLane(layer).TryRegister(item);
        }

        /// <summary>
        /// Unregisters an update owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (item is IFoveatedSimulationTarget foveatedTarget)
                _foveatedSimulationManager.UnregisterTarget(foveatedTarget);

            GetLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a fast-tick owner from a fixed priority lane.
        /// </summary>
        internal static void Unregister(IFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetFastLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a fixed-update owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetFixedLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a slow-tick owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (GetSlowLane(layer).TryUnregister(item) && item is IBucketedSlowTickable)
                _bucketedSlowTickableCount = math.max(0, _bucketedSlowTickableCount - 1);
        }

        /// <summary>
        /// Unregisters a cold-tick owner from a fixed priority lane.
        /// </summary>
        internal static void Unregister(IColdTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetColdLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a frost maintenance owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Frost-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IFrostTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetFrostLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a late-frame owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Late-frame owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(ILateFrameTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetLateFrameLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters a post-fixed-step owner from a fixed priority lane.
        /// </summary>
        /// <param name="item">Post-fixed owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Unregister(IPostFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetPostFixedLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Unregisters an unscaled fast-tick owner from a fixed priority lane.
        /// </summary>
        internal static void Unregister(IUnscaledFastTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetUnscaledFastLane(layer).TryUnregister(item);
        }

        /// <summary>
        /// Clears every dispatcher lane.
        /// </summary>
        public static void ClearAllLanes()
        {
            for (int i = 0; i < LaneCount; i++)
            {
                _priorityLanes[i].Clear();
                _fastPriorityLanes[i].Clear();
                _fixedPriorityLanes[i].Clear();
                _slowPriorityLanes[i].Clear();
                _coldPriorityLanes[i].Clear();
                _frostPriorityLanes[i].Clear();
                _lateFramePriorityLanes[i].Clear();
                _postFixedPriorityLanes[i].Clear();
                _unscaledFastPriorityLanes[i].Clear();
            }

            _bucketedSlowTickableCount = 0;
            _foveatedSimulationManager.ResetRuntimeState();
        }

        internal static void RequestOriginShiftBootstrapLock()
        {
            Interlocked.Increment(ref _originShiftBootstrapLockCount);
        }

        internal static void RequestOriginShiftFrameLock(int frame)
        {
            Volatile.Write(ref _originShiftFrameLockFrame, frame);
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            if (dispatcher != null)
                dispatcher.RequestAupPreShiftPause(unchecked((uint)frame));
        }

        public void RequestTimeDilation(float scalar, uint reasonHash = 0u)
        {
            ClearCoreTickDilationBurst();
            SetTimeDilationScalar(scalar, reasonHash, publishImmediate: true);
        }

        public void RequestHeadlessTimeDilation(float scalar, uint reasonHash = 0u)
        {
            ClearCoreTickDilationBurst();
            SetTimeDilationScalar(
                scalar,
                TimeDilationMinimumScalar,
                HeadlessTimeDilationMaximumScalar,
                reasonHash,
                publishImmediate: true);
        }

        /// <summary>
        /// Requests an exact frame-count core tick dilation burst.
        /// </summary>
        public void RequestCoreTickDilation(float scalar, int frameCount, uint reasonHash = 0u)
        {
            if (frameCount <= 0)
                return;

            if (SimulationPaused)
                return;

            float safeScalar = math.isfinite(scalar)
                ? math.clamp(scalar, TimeDilationPausedEpsilon, TimeDilationMaximumScalar)
                : 1f;

            if (_coreTickDilationRestorePending)
            {
                _coreTickDilationRestorePending = false;
            }
            else if (_coreTickDilationFramesRemaining <= 0)
            {
                _coreTickDilationRestoreScalar = _timeDilationScalar;
            }

            _coreTickDilationScalar = safeScalar;
            _coreTickDilationFramesRemaining = math.max(_coreTickDilationFramesRemaining, frameCount);
            _coreTickDilationReasonHash = reasonHash;
            SetTimeDilationScalar(safeScalar, reasonHash, publishImmediate: true);
        }

        public void SetThermalCriticalSlowTick(bool active)
        {
            _thermalCriticalSlowTickActive = active;
            if (active && _slowTickAccumulator > ThermalCriticalSlowTickIntervalSeconds)
                _slowTickAccumulator = ThermalCriticalSlowTickIntervalSeconds;
        }

        public void RequestSimulationPause(bool paused, uint reasonHash = 0u)
        {
            if (paused)
            {
                if (!_simulationPaused)
                    _prePauseTimeDilationScalar = ResolvePauseRestoreScalar();

                ClearCoreTickDilationBurst();
                _simulationPaused = true;
                SetTimeDilationScalar(0f, reasonHash, publishImmediate: true);
                return;
            }

            _simulationPaused = false;
            SetTimeDilationScalar(_prePauseTimeDilationScalar, reasonHash, publishImmediate: true);
        }

        public void RequestAupPreShiftPause(uint shiftFrameId)
        {
            RefreshDataVaultDependency();
            _dataVault?.LockAllocationsForAupShift(shiftFrameId);
            _aupPreShiftPauseSequence = shiftFrameId;
            _aupPreShiftPauseFrame = Time.frameCount;
        }

        public async Awaitable DelayDilated(float seconds, CancellationToken cancellationToken = default)
        {
            await AwaitableExtension.DelayDilated(seconds, cancellationToken);
        }

        internal static void ReleaseOriginShiftBootstrapLock()
        {
            int current = Volatile.Read(ref _originShiftBootstrapLockCount);
            while (current > 0)
            {
                int next = current - 1;
                int observed = Interlocked.CompareExchange(ref _originShiftBootstrapLockCount, next, current);
                if (observed == current)
                    return;

                current = observed;
            }
        }

        private void SetTimeDilationScalar(float scalar, uint reasonHash, bool publishImmediate)
        {
            SetTimeDilationScalar(
                scalar,
                TimeDilationMinimumScalar,
                TimeDilationMaximumScalar,
                reasonHash,
                publishImmediate);
        }

        private void SetTimeDilationScalar(
            float scalar,
            float minimumScalar,
            float maximumScalar,
            uint reasonHash,
            bool publishImmediate)
        {
            float safeScalar = math.isfinite(scalar)
                ? math.clamp(scalar, minimumScalar, maximumScalar)
                : 1f;
            if (math.abs(_timeDilationScalar - safeScalar) <= 0.0001f)
                return;

            _timeDilationScalar = safeScalar;
            _timeDilationSequence++;
            if (_timeDilationSequence == 0u)
                _timeDilationSequence = 1u;

            if (publishImmediate)
                PublishTimeDilationState(reasonHash);
        }

        private void PublishTimeDilationState(uint reasonHash)
        {
            if (_lastPublishedTimeDilationSequence == _timeDilationSequence)
                return;

            _lastPublishedTimeDilationSequence = _timeDilationSequence;
            float scalar = _timeDilationScalar;
            uint frame = unchecked((uint)Time.frameCount);
            TimeDilationSignal dilationSignal = new TimeDilationSignal
            {
                Scalar = scalar,
                UnscaledDeltaTime = CurrentFrameUnscaledDeltaTime,
                Sequence = _timeDilationSequence,
                Frame = frame,
                ReasonHash = reasonHash,
                Flags = (byte)(SimulationPaused ? 1 : 0)
            };
            GlobalSignals.Publish(in dilationSignal);

            BulletTimeVisualSignal visualSignal = new BulletTimeVisualSignal
            {
                Intensity01 = math.saturate((BulletTimePostScalarThreshold - scalar) / BulletTimePostScalarThreshold),
                Scalar = scalar,
                Frame = frame,
                Sequence = _timeDilationSequence,
                QualityTier = GlobalRegistry.ScalabilityTierProfileByte,
                Flags = (byte)(SimulationPaused ? 1 : 0)
            };
            GlobalSignals.Publish(in visualSignal);
        }

        private void DrainSimulationPauseSignals()
        {
            while (GlobalSignals.TryDequeueSimulationPause(out SimulationPauseSignal signal))
            {
                if (signal.Paused == 0 && math.isfinite(signal.RestoreScalar) && signal.RestoreScalar > 0f)
                    _prePauseTimeDilationScalar = signal.RestoreScalar;

                RequestSimulationPause(signal.Paused != 0, signal.SourceHash);
            }
        }

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            _slowTickAccumulator = 0f;
            _fixedStepAccumulator = 0f;
            _fastTickAccumulator = 0f;
            _coldTickAccumulator = 0f;
            _frostTickAccumulator = 0f;
            _memoryDefragAccumulator = 0f;
            _unscaledFastTickAccumulator = 0f;
            _timeDilationScalar = 1f;
            _prePauseTimeDilationScalar = 1f;
            _coreTickDilationScalar = 1f;
            _coreTickDilationRestoreScalar = 1f;
            _coreTickDilationFramesRemaining = 0;
            _coreTickDilationReasonHash = 0u;
            _coreTickDilationRestorePending = false;
            _simulationPaused = false;
            _thermalCriticalSlowTickActive = false;
            _timeSnapshot = default;
            CurrentFixedInterpolationAlpha = 0f;
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            if (_serviceRegistered)
            {
                HomeostasisBrain.ShutdownRuntime();
                _foveatedSimulationManager.Dispose();
                DisposeDispatcherRaycastBuffers();
                DisposeH8TimeArray();
                ThreadSafeCommandQueue.Shutdown();
                if (ReferenceEquals(GlobalRegistry.Dispatcher, this))
                    GlobalRegistry.UnregisterSystemDispatcher(this);
            }

            _slowTickAccumulator = 0f;
            _fixedStepAccumulator = 0f;
            _fastTickAccumulator = 0f;
            _coldTickAccumulator = 0f;
            _frostTickAccumulator = 0f;
            _memoryDefragAccumulator = 0f;
            _unscaledFastTickAccumulator = 0f;
            _timeDilationScalar = 1f;
            _prePauseTimeDilationScalar = 1f;
            _simulationPaused = false;
            _thermalCriticalSlowTickActive = false;
            _timeDilationSequence = 0u;
            _lastPublishedTimeDilationSequence = 0u;
            _aupPreShiftPauseSequence = 0u;
            _aupPreShiftPauseFrame = -1;
            ClearCoreTickDilationBurst();
            _dataVault = null;
            _simulationBucketer = null;
            _timeSnapshot = default;
            CurrentFrameDeltaTime = 0f;
            CurrentFrameUnscaledDeltaTime = 0f;
            CurrentFixedInterpolationAlpha = 0f;
            _serviceRegistered = false;
            _lastMemoryDefragPressureWarningFrame = -1;
            Volatile.Write(ref _homeostasisKillSwitchMaskBits, 0L);
            Volatile.Write(ref _homeostasisPressureLevel, 0);
            Volatile.Write(ref _homeostasisSlowTick2Hz, 0);
            Volatile.Write(ref _homeostasisFoveatedTier, 0);
        }

        /// <summary>
        /// Registers this dispatcher as the authoritative runtime dispatcher service.
        /// </summary>
        public void InitializeService()
        {
            if (_serviceRegistered)
                return;

            ThreadSafeCommandQueue.Initialize();
            UIStateStore.EnsureInitialized();
            BaseAirlockEvents.Prewarm();
            RefreshDataVaultDependency();
            RefreshSimulationBucketerDependency();
            EnsureDispatcherRaycastBuffers();
            EnsureH8TimeArray();
            HomeostasisBrain.InitializeRuntime();
            GlobalRegistry.RegisterSystemDispatcher(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Dispatcher, this);
            PublishTimeDilationState(0u);
        }

        private void EnsureH8TimeArray()
        {
            if (_h8Time.IsCreated)
                return;

            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            if (dataVault != null)
            {
                _h8TimeHandle = dataVault.GetBufferHandle<double>(
                    BufferID.H8Time,
                    (int)H8TimeSlot.Count,
                    SystemID.SystemDispatcher,
                    NativeArrayOptions.ClearMemory);
                _h8Time = _h8TimeHandle.Resolve(dataVault);
                if (_h8Time.IsCreated)
                {
                    _h8TimeVaultOwned = true;
                    return;
                }

                _h8TimeHandle = default;
            }

            _h8Time = H8Memory.Allocate<double>((int)H8TimeSlot.Count, SystemID.SystemDispatcher, Allocator.Persistent, NativeArrayOptions.ClearMemory); // FALLBACK COLD ALLOC: NativeArray<double>[4] - dispatcher H8 time SOA when DataVault is unavailable - owner: SystemDispatcher
            NativeMemorySentinel.RegisterNativeArray(
                _h8Time,
                nameof(SystemDispatcher),
                nameof(_h8Time),
                NativeAllocationLifetime.Session);
            _h8TimeVaultOwned = false;
        }

        private bool TryResolveH8TimeArray()
        {
            EnsureH8TimeArray();
            if (!_h8TimeVaultOwned)
                return _h8Time.IsCreated;

            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            if (dataVault == null)
                return false;

            NativeArray<double> resolved = _h8TimeHandle.Resolve(dataVault);
            if (!resolved.IsCreated)
                return false;

            _h8Time = resolved;
            return true;
        }

        private void RefreshDataVaultDependency()
        {
            IDataVault dataVault = GlobalRegistry.DataVault;
            if (dataVault != null)
                _dataVault = dataVault;
        }

        private void RefreshSimulationBucketerDependency()
        {
            _simulationBucketer = GlobalRegistry.SimulationBucketer;
        }

        private void RunPreSimulationMemoryDefrag(float unscaledDeltaTime)
        {
            IDataVault dataVault = _dataVault;
            if (dataVault == null)
            {
                RefreshDataVaultDependency();
                dataVault = _dataVault;
            }

            if (dataVault == null || unscaledDeltaTime < 0f)
                return;

            bool forcedByMemoryPressure = Interlocked.Exchange(ref _criticalMemoryPressureDefragRequested, 0) != 0;
            double cadenceSeconds = GlobalRegistry.ScalabilityTierProfileByte == 0
                ? ColdTickIntervalSeconds
                : FrostTickIntervalSeconds;
            _memoryDefragAccumulator += unscaledDeltaTime;
            if (!forcedByMemoryPressure && _memoryDefragAccumulator < cadenceSeconds)
                return;

            float elapsedSeconds = (float)(_memoryDefragAccumulator > 0d ? _memoryDefragAccumulator : cadenceSeconds);
            _memoryDefragAccumulator = 0d;
            float compactionStress01 = ResolveMemoryCompactionStress01(unscaledDeltaTime);
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            using (_memoryDefragProfilerMarker.Auto())
            {
                dataVault.FrostTickDefrag(elapsedSeconds, compactionStress01);
            }

            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0d /
                System.Diagnostics.Stopwatch.Frequency;
            PublishMemoryAddressShiftSignals(dataVault);
            PublishDataVaultDefragTelemetry(dataVault, elapsedMilliseconds);
            EmitVramPressureDefragSignalIfNeeded();
        }

        private static float ResolveMemoryCompactionStress01(float unscaledDeltaTime)
        {
            if (!math.isfinite(unscaledDeltaTime) || unscaledDeltaTime < 0f)
                return 1f;

            float frameStress = math.saturate(unscaledDeltaTime / 0.05f);
            float pressureStress = math.saturate(HomeostasisPressureLevel * 0.125f);
            return math.max(frameStress, pressureStress);
        }

        private static void PublishMemoryAddressShiftSignals(IDataVault dataVault)
        {
            int recordCount = dataVault.LastRelocationRecordCount;
            for (int i = 0; i < recordCount; i++)
            {
                if (!dataVault.TryGetLastRelocationRecord(i, out VaultRelocationRecord record))
                    break;

                MemoryAddressShiftSignal signal = default;
                signal.OldPointer = record.OldPointer;
                signal.NewPointer = record.NewPointer;
                signal.BufferId = record.BufferId;
                signal.ByteLength = record.ByteLength;
                signal.Version = record.Generation;
                signal.Flags = record.Flags;
                signal.SystemId = record.SystemId;
                GlobalSignals.Publish(in signal);
            }
        }

        private void PublishDataVaultDefragTelemetry(IDataVault dataVault, double elapsedMilliseconds)
        {
            if (dataVault.HeapFragmentationRatio > 0f)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _HeapFragmentationRatioHash,
                    _DataVaultDefragContextHash,
                    dataVault.HeapFragmentationRatio);
            }

            if (dataVault.LastDefragMovedBytes > 0L)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _DataVaultMovedBytesHash,
                    _DataVaultDefragContextHash,
                    dataVault.LastDefragMovedBytes * GlobalTelemetryBus.BytesToMegabytes);
            }

            if (dataVault.LastDefragWatchdogExceeded || elapsedMilliseconds > 1.0d)
            {
                GlobalTelemetryBus.PublishJobBarrierStall(
                    nameof(GlobalDataVault),
                    nameof(RunPreSimulationMemoryDefrag),
                    (float)elapsedMilliseconds);
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _DataVaultWatchdogHash,
                    _DataVaultDefragContextHash,
                    (float)elapsedMilliseconds);
            }

            if (dataVault.PendingMassiveMoveBytes >= 50L * 1024L * 1024L &&
                _lastMemoryDefragPressureWarningFrame != Time.frameCount)
            {
                _lastMemoryDefragPressureWarningFrame = Time.frameCount;
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _DataVaultMassiveMoveHash,
                    _DataVaultDefragContextHash,
                    dataVault.PendingMassiveMoveBytes * GlobalTelemetryBus.BytesToMegabytes);
            }
        }

        private static void EmitVramPressureDefragSignalIfNeeded()
        {
            VRAMMonitor monitor = GlobalRegistry.VRAMMonitor;
            if (monitor == null || monitor.TotalVRAMBytes <= 1800L * 1024L * 1024L)
                return;

            GlobalTelemetryBus.PublishVRAMWarningEvent(monitor.TotalVRAMBytes);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _DataVaultVramPressureHash,
                _DataVaultDefragContextHash,
                monitor.TotalVRAMBytes * GlobalTelemetryBus.BytesToMegabytes);

            VRAMPressureMonitor pressureMonitor = GlobalRegistry.VRAMPressure;
            if (pressureMonitor != null)
                pressureMonitor.ForceImmediateSampleAndResponse();
        }

        private void DisposeH8TimeArray()
        {
            if (!_h8Time.IsCreated)
                return;

            if (_h8TimeVaultOwned)
            {
                _h8Time = default;
                _h8TimeHandle = default;
                _h8TimeVaultOwned = false;
                return;
            }

            NativeMemorySentinel.UnregisterNativeArray(_h8Time);
            H8Memory.Release(ref _h8Time);
            _h8TimeHandle = default;
            _h8TimeVaultOwned = false;
        }

        private void UpdateH8TimeState(float dilatedDeltaTime, float unscaledDeltaTime)
        {
            if (!TryResolveH8TimeArray())
                return;

            double dilatedTime = _h8Time[(int)H8TimeSlot.Time] + dilatedDeltaTime;
            double unscaledTime = Time.unscaledTimeAsDouble;
            _h8Time[(int)H8TimeSlot.Time] = dilatedTime;
            _h8Time[(int)H8TimeSlot.DeltaTime] = dilatedDeltaTime;
            _h8Time[(int)H8TimeSlot.UnscaledTime] = unscaledTime;
            _h8Time[(int)H8TimeSlot.UnscaledDeltaTime] = unscaledDeltaTime;
            _timeSnapshot = new H8TimeSnapshot(dilatedTime, dilatedDeltaTime, unscaledTime, unscaledDeltaTime);
        }

        private float ResolveFrameTimeDilationScalar()
        {
            if (_simulationPaused || _timeDilationScalar <= TimeDilationPausedEpsilon)
                return 0f;

            if (_coreTickDilationFramesRemaining <= 0)
                return _timeDilationScalar;

            float scalar = _coreTickDilationScalar;
            _coreTickDilationFramesRemaining--;
            if (_coreTickDilationFramesRemaining == 0)
            {
                _coreTickDilationScalar = 1f;
                _coreTickDilationRestorePending = true;
            }

            return scalar;
        }

        private float ResolvePauseRestoreScalar()
        {
            float restoreScalar = _coreTickDilationFramesRemaining > 0 || _coreTickDilationRestorePending
                ? _coreTickDilationRestoreScalar
                : _timeDilationScalar;
            return math.isfinite(restoreScalar)
                ? math.max(TimeDilationPausedEpsilon, restoreScalar)
                : 1f;
        }

        private void ClearCoreTickDilationBurst()
        {
            _coreTickDilationScalar = 1f;
            _coreTickDilationRestoreScalar = 1f;
            _coreTickDilationFramesRemaining = 0;
            _coreTickDilationReasonHash = 0u;
            _coreTickDilationRestorePending = false;
        }

        private void ApplyPendingCoreTickDilationRestore()
        {
            if (!_coreTickDilationRestorePending)
                return;

            _coreTickDilationRestorePending = false;
            SetTimeDilationScalar(_coreTickDilationRestoreScalar, _coreTickDilationReasonHash, publishImmediate: true);
            _coreTickDilationRestoreScalar = 1f;
            _coreTickDilationReasonHash = 0u;
        }

        private void Update()
        {
            GlobalRegistry.PublishAbsoluteUniverseTime(Time.timeAsDouble);
            GlobalRegistry.TickMathPrecisionTransition(Time.frameCount);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.DispatcherUpdate);
#endif
            using (_updateProfilerMarker.Auto())
            {
#if UNITY_EDITOR
                NativeAllocationTrackerRuntimeBridge.NotifyDispatcherHeartbeat();
#endif
                ApplyPendingCoreTickDilationRestore();
                if (BootstrapStatus.TryTriggerSafeHalt())
                    BootstrapBiosErrorOverlay.Show(BootstrapStatus.SafeHaltDisplayMessage);

                long dispatcherTickStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                HectonXRRuntimeState.RefreshFrameState(Time.frameCount);
                float measuredUnscaledDeltaTime = HectonXRRuntimeState.IsXRActive ? Time.smoothDeltaTime : Time.unscaledDeltaTime;
                float unscaledDeltaTime = HectonXRRuntimeState.ResolveDispatcherDeltaTime(measuredUnscaledDeltaTime);
                bool previousFrameMissedBudget = CurrentFrameUnscaledDeltaTime > JobAdmissionFrameBudgetMissThresholdSeconds;
                CurrentFrameUnscaledDeltaTime = unscaledDeltaTime;
                HomeostasisBrain.PreSimulationTick(unscaledDeltaTime);
                GlobalRegistry.InputDeterminism?.PreSimulationInputTick(unscaledDeltaTime);
                GlobalSignals.FlushPreSimulation();
                RunPreSimulationMemoryDefrag(unscaledDeltaTime);
                IJobAdmissionService jobAdmission = GlobalRegistry.JobAdmission;
                jobAdmission?.Refill(
                    GlobalRegistry.ScalabilityTierProfileByte,
                    unscaledDeltaTime,
                    previousFrameMissedBudget);
                Hecton8.Modding.ModCommandDispatcher.DrainPreSimulation();
                DrainSimulationPauseSignals();
                float deltaTime = unscaledDeltaTime * ResolveFrameTimeDilationScalar();
                CurrentFrameDeltaTime = deltaTime;
                UpdateH8TimeState(deltaTime, unscaledDeltaTime);
                PublishTimeDilationState(0u);
                RefreshSimulationBucketerDependency();
                ISimulationBucketer simulationBucketer = _simulationBucketer;
                bool aupBarrierActive = IsOriginShiftBootstrapLocked ||
                                        IsOriginShiftFrameLockedForCurrentFrame ||
                                        _aupPreShiftPauseFrame == Time.frameCount;
                if (simulationBucketer != null && simulationBucketer.IsInitialized)
                {
                    simulationBucketer.AdvanceFrame(
                        GlobalRegistry.ScalabilityTierProfileByte,
                        unscaledDeltaTime,
                        jobAdmission != null ? jobAdmission.CriticalDebtFrameCount : 0,
                        aupBarrierActive);
                }

                if (IsOriginShiftBootstrapLocked)
                {
                    if (!HectonFloatingOrigin.TryFlushInitialSceneRebaseBeforeTicks())
                        return;

                    if (IsOriginShiftBootstrapLocked)
                        return;
                }

                if (IsOriginShiftFrameLockedForCurrentFrame)
                    return;

                if (_aupPreShiftPauseFrame == Time.frameCount)
                {
                    RunUnscaledFastTick(unscaledDeltaTime, blockGameplayLanes: false);
                    return;
                }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                long beginDispatcherTimestamp = BeginDispatcherPhaseTiming();
#endif
                _foveatedSimulationManager.BeginDispatcherFrame(deltaTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                EndDispatcherPhaseTiming(beginDispatcherTimestamp, "FoveatedSimulationManager.BeginDispatcherFrame");
#endif
                PredatorCognitionDomain.BeginDispatcherFrame(Time.frameCount);
                bool blockGameplayLanes = Application.isPlaying &&
                                          BootstrapState.HasActiveInstance &&
                                          !BootstrapState.IsGameReady;
                long bucketWorkStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<IUpdatable> lane = _priorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IUpdatable));
#endif
                    using (_updateLaneProfilerMarkers[laneIndex].Auto())
                    {
                        IUpdatable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            IUpdatable updatable = rawArray[itemIndex];
                            if (!_foveatedSimulationManager.TryResolveTick(updatable, deltaTime, out float effectiveDeltaTime))
                                continue;

                            updatable.Tick(effectiveDeltaTime);
                            _foveatedSimulationManager.NotifyTickCompleted(updatable);
                            if (IsOriginShiftFrameLockedForCurrentFrame)
                                return;
                        }
                    }
                }

                CombatDamageRuntime.FrameTick(deltaTime);
                PredatorCognitionDomain.ScheduleFrameEvaluation(Time.frameCount);
                _foveatedSimulationManager.ScheduleFrameJobs();
                RunFastTick(deltaTime, blockGameplayLanes);
                RunUnscaledFastTick(CurrentFrameUnscaledDeltaTime, blockGameplayLanes: false);
                RunFixedStepAccumulator(deltaTime, blockGameplayLanes);
                RunBucketedSlowTick(blockGameplayLanes);
                RunSlowTick(deltaTime, blockGameplayLanes);
                RunColdTick(deltaTime, blockGameplayLanes);
                RunFrostTick(deltaTime, blockGameplayLanes);
                ScheduleDispatcherRaycasts();
                IModdingBridge moddingBridge = _moddingBridgeProjectionRuntime;
                if (moddingBridge != null)
                    moddingBridge.ProjectPostSimulation();
                if (simulationBucketer != null)
                {
                    float activeBucketLoadMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - bucketWorkStartTimestamp) * 1000.0 /
                                                       System.Diagnostics.Stopwatch.Frequency);
                    simulationBucketer.ReportActiveBucketLoadMs(activeBucketLoadMs);
                    CrashTelemetryBuffer.ReportSimulationBucketFrame(simulationBucketer.CaptureFrameState());
                }

                double tickOverheadMilliseconds =
                    (System.Diagnostics.Stopwatch.GetTimestamp() - dispatcherTickStartTimestamp) * 1000.0 /
                    System.Diagnostics.Stopwatch.Frequency;
                CrashTelemetryBuffer.ReportTimeDilationState(_timeDilationScalar, tickOverheadMilliseconds);
            }
        }

        private static void RefreshPauseDepthOfFieldTarget()
        {
            bool active = _pauseMenuDepthOfFieldRequested || _pdaDepthOfFieldRequested;
            float targetWeight = active ? 1f : 0f;
            if (_pauseDepthOfFieldTargetActive == active &&
                math.abs(_pauseDepthOfFieldTargetWeight - targetWeight) <= 0.0001f)
                return;

            float now = Time.unscaledTime;
            _pauseDepthOfFieldBlendStartWeight = ResolvePauseDepthOfFieldWeight(now);
            _pauseDepthOfFieldWeight = _pauseDepthOfFieldBlendStartWeight;
            _pauseDepthOfFieldTargetWeight = targetWeight;
            _pauseDepthOfFieldBlendStartTime = now;
            _pauseDepthOfFieldTargetActive = active;
        }

        private static float ResolvePauseDepthOfFieldWeight(float unscaledTime)
        {
            float normalized = PauseDepthOfFieldBlendSeconds > 0f
                ? math.saturate((unscaledTime - _pauseDepthOfFieldBlendStartTime) / PauseDepthOfFieldBlendSeconds)
                : 1f;
            return math.lerp(_pauseDepthOfFieldBlendStartWeight, _pauseDepthOfFieldTargetWeight, normalized);
        }

        private static void TickPauseDepthOfField(float unscaledTime)
        {
            _pauseDepthOfFieldWeight = ResolvePauseDepthOfFieldWeight(unscaledTime);

            ICameraJuiceSystem cameraJuice = GlobalRegistry.CameraJuice;
            if (cameraJuice != null)
                cameraJuice.ApplyPauseDepthOfFieldWeight(_pauseDepthOfFieldWeight);
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.DispatcherLateFrame);
            long completeDispatcherTimestamp = 0L;
            bool dispatcherPhaseTimingStarted = false;
#endif
            if (IsOriginShiftBootstrapLocked)
                return;
            if (IsOriginShiftFrameLockedForCurrentFrame)
            {
                _dataVault?.UnlockAllocationsAfterAupShift(_aupPreShiftPauseSequence);
                return;
            }

            try
            {
            DispatcherJobSwap.BeginLateFrameSwapWindow();
            try
            {
                CompleteDispatcherRaycasts();
                UpdatePauseFreezeFrameDitherState();
                UpdateVisualStaticGlitchState();
                TickPauseDepthOfField(Time.unscaledTime);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                completeDispatcherTimestamp = BeginDispatcherPhaseTiming();
                dispatcherPhaseTimingStarted = true;
#endif
                CompleteFoveatedFrameJobs();
                BeginLateFrameEventBudget();
                SetActiveLateFrameEventLane(_LateFrameTickablesQueueHash);
                using (_lateFrameProfilerMarker.Auto())
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        RegistryBucket<ILateFrameTickable> lane = _lateFramePriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        lane.ValidateNoDestroyedEntriesDebug(nameof(ILateFrameTickable));
#endif
                        ILateFrameTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;
                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            rawArray[itemIndex].LateFrameTick();
                    }
                }
                AcousticOcclusionUtility.LateFrameTick();
                PredatorCognitionDomain.LateFrameTick();
                CombatDamageRuntime.LateFrameTick();
                VoxelDynamicNavGridRuntime.CompletePendingDynamicObstacleUpdates();
            }
            finally
            {
                DispatcherJobSwap.EndLateFrameSwapWindow();
            }

            using (_lateFrameCommandQueueDrainProfilerMarker.Auto())
            {
                SetActiveLateFrameEventLane(_ThreadSafeCommandQueueHash);
                ReportLateFrameQueueDepth(_ThreadSafeCommandQueueHash, ThreadSafeCommandQueue.PendingCount);
                if (!ThreadSafeCommandQueue.DrainMainThread())
                    MarkLateFrameEventDispatchDeferred();
            }
            SetActiveLateFrameEventLane(_StorageReservationCommitResolvedQueueHash);
            ReportLateFrameQueueDepth(_StorageReservationCommitResolvedQueueHash, ThreadSafeCommandQueue.PendingStorageReservationCommitResolvedCount);
            if (!ThreadSafeCommandQueue.FlushStorageReservationCommitResolvedEvents())
                MarkLateFrameEventDispatchDeferred();
            SetActiveLateFrameEventLane(_ModCommandDispatcherQueueHash);
            Hecton8.Modding.ModCommandDispatcher.DrainLateFrame();
            FlushCoreEventsArtery();
            if (!SimulationPaused)
            {
                FlushEnvironmentEventsArtery();
                FlushPlayerEventsArtery();
                FlushBaseEventsArtery();
                FlushAIEventsArtery();
            }
            }
            finally
            {
                ClearActiveLateFrameEventLane();
                if (_lateFrameEventBudgetActive)
                    EndLateFrameEventBudget();

                try
                {
                    GlobalTelemetryBus.LateFrameUpdate(Time.unscaledTime);
                    WorldSpatialHashGrid.LateFrameMaintenance(Time.frameCount);
                }
                finally
                {
                    GlobalSignals.ClearPostSimulationSnapshots();
                    NativeArenaAllocator.Reset();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (dispatcherPhaseTimingStarted)
                        EndDispatcherPhaseTiming(completeDispatcherTimestamp, "FoveatedSimulationManager.CompleteFrameJobs");
#endif
                }
            }
        }

        private static void FlushCoreEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_CoreEventsArteryHash);
                ReportLateFrameQueueDepth(
                    _CoreEventsArteryHash,
                    Hecton8.Modding.ModRegistryEvents.PendingCount +
                    BootstrapEvents.PendingCount +
                    Hecton.Localization.LocalizationEvents.PendingCount +
                    NarrativeEvents.PendingCount +
                    SaveEvents.PendingCount +
                    QuestEvents.PendingCount +
                    Hecton8.Bootstrap.GameBootstrapper.PendingEventCount +
                    ObjectPoolDiagnostics.PendingCount +
                    PerformanceEvents.PendingCount +
                    ScalabilityEvents.PendingCount +
                    GlobalRegistry.PendingServiceReboundCount);

                BootstrapEvents.FlushPending();
                Hecton8.Bootstrap.GameBootstrapper.FlushPendingEvents();
                PerformanceEvents.FlushPending();
                ObjectPoolDiagnostics.FlushPending();
                ScalabilityEvents.FlushPending();
                GlobalRegistry.FlushPendingServiceReboundEvents();
                Hecton8.Modding.ModRegistryEvents.FlushPending();
                Hecton.Localization.LocalizationEvents.FlushPending();
                NarrativeEvents.FlushPending();
                SaveEvents.FlushPending();
                QuestEvents.FlushPending();
            }
            finally
            {
                EndLateFrameFlushPass(_CoreEventsArteryHash, passStartTimestamp);
            }
        }

        private static void FlushEnvironmentEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_EnvironmentEventsArteryHash);
                ReportLateFrameQueueDepth(
                    _EnvironmentEventsArteryHash,
                    AtmosphereEvents.PendingCount +
                    HighPressureEvents.PendingCount +
                    FatalPressureImplosionEvents.PendingCount +
                    CelestialEvents.PendingCount +
                    EclipseGameplayEvents.PendingCount +
                    AcousticZoneEvents.PendingCount +
                    PhysicsEventBus.PendingCount +
                    FluidFeedbackEvents.PendingCount +
                    ElectrolysisAcousticEvents.PendingCount +
                    AudioCaptionEvents.PendingCount +
                    SpectrumEvents.PendingCount +
                    ProceduralAudioEvents.PendingCount +
                    MapMagicBiomeEvents.PendingCount +
                    BiomeMatrixEvents.PendingCount +
                    WeatherEvents.PendingCount +
                    RandomEventEvents.PendingCount +
                    DepthZoneEvents.PendingCount +
                    SoundscapeEvents.PendingCount +
                    SargassumGlobalDragManager.PendingEventCount +
                    Hecton8.AtlasSignal.AtlasSignalEvents.PendingCount +
                    Hecton8.AtlasSignal.Atlas6Events.PendingCount);

                AtmosphereEvents.FlushPending();
                HighPressureEvents.FlushPending();
                FatalPressureImplosionEvents.FlushPending();
                PhysicsEventBus.FlushPending();
                FluidFeedbackEvents.FlushPending();

                if (ShouldDropAmbientLateFrameEvents(_EnvironmentEventsArteryHash))
                {
                    DropAmbientEnvironmentEvents();
                    return;
                }

                CelestialEvents.FlushPending();
                EclipseGameplayEvents.FlushPending();
                AcousticZoneEvents.FlushPending();
                ElectrolysisAcousticEvents.FlushPending();
                AudioCaptionEvents.FlushPending();
                SpectrumEvents.FlushPending();
                ProceduralAudioEvents.FlushPending();
                MapMagicBiomeEvents.FlushPending();
                BiomeMatrixEvents.FlushPending();
                WeatherEvents.FlushPending();
                RandomEventEvents.FlushPending();
                DepthZoneEvents.FlushPending();
                SoundscapeEvents.FlushPending();
                SargassumGlobalDragManager.FlushPendingEvents();
                Hecton8.AtlasSignal.AtlasSignalEvents.FlushPending();
                Hecton8.AtlasSignal.Atlas6Events.FlushPending();
            }
            finally
            {
                EndLateFrameFlushPass(_EnvironmentEventsArteryHash, passStartTimestamp);
            }
        }

        private static void FlushPlayerEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_PlayerEventsArteryHash);
                int pdaPendingBeforeFlush = Hecton8.UI.PDAEvents.PendingCount;
                ReportLateFrameQueueDepth(
                    _PlayerEventsArteryHash,
                    Hecton8.Interaction.InteractionEvents.PendingCount +
                    Hecton8.Crafting.CraftingEvents.PendingCount +
                    ScanEvents.PendingCount +
                    FirstHourEvents.PendingCount +
                    EndingEvents.PendingCount +
                    AudioLogEvents.PendingCount +
                    HectonSubmarineOsEvents.PendingCount +
                    FlashlightEvents.PendingCount +
                    LaserCutterEvents.PendingCount +
                    SuitMeshUpdateEvents.PendingCount +
                    PlayerSignalEvents.PendingCount +
                    InventoryEvents.PendingCount +
                    PlayerExpressionEvents.PendingCount +
                    Hecton8.UI.BaseIntegrityEvents.PendingCount +
                    Hecton8.UI.NotificationEvents.PendingCount +
                    Hecton8.UI.PDAIntrusionEvents.PendingCount +
                    pdaPendingBeforeFlush);

                SuitMeshUpdateEvents.FlushPending();
                PlayerSignalEvents.FlushPending();
                Hecton8.UI.BaseIntegrityEvents.FlushPending();
                FlashlightEvents.FlushPending();
                LaserCutterEvents.FlushPending();
                InventoryEvents.FlushPending();
                Hecton8.Interaction.InteractionEvents.FlushPending();
                Hecton8.Crafting.CraftingEvents.FlushPending();
                ScanEvents.FlushPending();

                if (ShouldDropAmbientLateFrameEvents(_PlayerEventsArteryHash))
                    return;

                FirstHourEvents.FlushPending();
                EndingEvents.FlushPending();
                AudioLogEvents.FlushPending();
                HectonSubmarineOsEvents.FlushPending();
                PlayerExpressionEvents.FlushPending();
                Hecton8.UI.NotificationEvents.FlushPending();
                Hecton8.UI.PDAIntrusionEvents.FlushPending();
                Hecton8.UI.PDAEvents.FlushPending(MaxPdaEventsPerFrame);
                TrackPdaBusCongestion(pdaPendingBeforeFlush, Hecton8.UI.PDAEvents.PendingCount);
            }
            finally
            {
                EndLateFrameFlushPass(_PlayerEventsArteryHash, passStartTimestamp);
            }
        }

        private static void FlushBaseEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_BaseEventsArteryHash);
                ReportLateFrameQueueDepth(
                    _BaseEventsArteryHash,
                    PowerGridTelemetryEvents.PendingCount +
                    ModuleStatusEvents.PendingCount +
                    BaseAirlockEvents.PendingCount +
                    EmergencyServiceRelayEvents.PendingCount);

                BaseAirlockEvents.FlushPending();
                EmergencyServiceRelayEvents.FlushPending();

                if (ShouldDropAmbientLateFrameEvents(_BaseEventsArteryHash))
                    return;

                PowerGridTelemetryEvents.FlushPending();
                ModuleStatusEvents.FlushPending();
            }
            finally
            {
                EndLateFrameFlushPass(_BaseEventsArteryHash, passStartTimestamp);
            }
        }

        private static void FlushAIEventsArtery()
        {
            long passStartTimestamp = BeginLateFrameFlushPass();
            try
            {
                SetActiveLateFrameEventLane(_AIEventsArteryHash);
                ReportLateFrameQueueDepth(
                    _AIEventsArteryHash,
                    DirectorAIEvents.PendingCount +
                    HectonDroneFleetEvents.PendingCount +
                    RepairDroneTorchAcousticEvents.PendingCount);

                DirectorAIEvents.FlushPending();
                HectonDroneFleetEvents.FlushPending();
                RepairDroneTorchAcousticEvents.FlushPending();
            }
            finally
            {
                EndLateFrameFlushPass(_AIEventsArteryHash, passStartTimestamp);
            }
        }

        /// <summary>
        /// Consumes one deferred event dispatch slot from the current LateUpdate budget.
        /// </summary>
        /// <returns>True when an event queue may dispatch one payload this frame.</returns>
        public static bool TryConsumeLateFrameEventDispatch()
        {
            if (!_lateFrameEventBudgetActive)
                return true;

            if (LateFrameFlushBudgetExhausted)
            {
                _lateFrameCircuitBreakerTripped = true;
                RecordLateFrameCircuitBreakerLane(_activeLateFrameEventLaneHash);
                if (!_lateFrameTimeBudgetExhausted)
                {
                    _lateFrameTimeBudgetExhausted = true;
                    CrashTelemetryBuffer.ReportLateFrameLoadShedding(
                        _activeLateFrameEventLaneHash,
                        _lateFrameEventDispatchBudget);
                }

                return false;
            }

            if (_lateFrameEventDispatchBudget > 0)
            {
                _lateFrameEventDispatchBudget--;
                return true;
            }

            _lateFrameCircuitBreakerTripped = true;
            RecordLateFrameCircuitBreakerLane(_activeLateFrameEventLaneHash);
            return false;
        }

        /// <summary>
        /// True while the late-frame ambient event lane has crossed its time budget for this frame.
        /// </summary>
        public static bool IsLateFrameAmbientEventSheddingActive =>
            _lateFrameEventBudgetActive && LateFrameFlushBudgetExhausted;

        public static void DispatchCriticalMemoryPressure(in CriticalMemoryPressureEvent memoryPressureEvent)
        {
            RequestVisualStaticGlitch();
            MemoryPressureSignal pressureSignal = new MemoryPressureSignal
            {
                ReservedMemoryBytes = memoryPressureEvent.ReservedMemoryBytes,
                PhysicalMemoryBytes = memoryPressureEvent.PhysicalMemoryBytes,
                UsageRatio = (float)memoryPressureEvent.UsageRatio,
                Frame = unchecked((uint)memoryPressureEvent.Frame),
                Severity = 2,
                Flags = 1
            };
            GlobalSignals.Publish(in pressureSignal);
            IMacroDatabaseService macroDatabase = GlobalRegistry.MacroDatabase;
            macroDatabase?.NotifyCriticalMemoryPressure(
                memoryPressureEvent.ReservedMemoryBytes,
                memoryPressureEvent.PhysicalMemoryBytes,
                pressureSignal.UsageRatio,
                pressureSignal.Frame,
                pressureSignal.Severity);
            Interlocked.Exchange(ref _criticalMemoryPressureDefragRequested, 1);
            CrashTelemetryBuffer.ReportCriticalMemoryPressure(
                memoryPressureEvent.ReservedMemoryBytes,
                memoryPressureEvent.PhysicalMemoryBytes,
                memoryPressureEvent.UsageRatio);

            ObjectPoolManager objectPool = GlobalRegistry.ObjectPool;
            if (objectPool != null)
                objectPool.FlushInactivePoolsForMemoryPressure();

            if (CurrentFrameDeltaTime > 0f && CurrentFrameDeltaTime < SafeGcCollectFrameBudgetSeconds)
                System.GC.Collect(0, System.GCCollectionMode.Optimized, false);
        }

        internal static void MarkLateFrameEventDispatchDeferred()
        {
            if (_lateFrameEventBudgetActive)
            {
                _lateFrameCircuitBreakerTripped = true;
                RecordLateFrameCircuitBreakerLane(_activeLateFrameEventLaneHash);
            }
        }

        private static void SetActiveLateFrameEventLane(uint queueHash)
        {
            _activeLateFrameEventLaneHash = queueHash;
        }

        private static void ClearActiveLateFrameEventLane()
        {
            _activeLateFrameEventLaneHash = 0u;
        }

        private static void ReportLateFrameQueueDepth(uint queueHash, int pendingCount)
        {
            ObjectPoolDiagnostics.PublishDataBusDepth(queueHash, pendingCount);
        }

        private static long BeginLateFrameFlushPass()
        {
            return System.Diagnostics.Stopwatch.GetTimestamp();
        }

        private static void EndLateFrameFlushPass(uint laneHash, long passStartTimestamp)
        {
            if (passStartTimestamp == 0L)
                return;

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - passStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            RecordArteryFlushSample(elapsedMilliseconds);
            if (elapsedMilliseconds <= LateFrameFlushPassSpikeMilliseconds || _criticalPerformanceSpikeReported)
                return;

            _criticalPerformanceSpikeReported = true;
            uint stackHash = CaptureCriticalPerformanceStackHash(laneHash);
            CrashTelemetryBuffer.ReportCriticalPerformanceSpike(laneHash, elapsedMilliseconds, stackHash);
            GlobalTelemetryBus.PublishPerformanceWarning(
                _CriticalPerformanceSpikeHash,
                laneHash,
                (float)elapsedMilliseconds);
        }

        internal static int CopyArteryFlushMilliseconds(float[] destination)
        {
            if (destination == null || destination.Length == 0)
                return 0;

            int available = math.min(_arteryFlushSampleCursor, ArteryFlushSampleCapacity);
            int copyCount = math.min(available, destination.Length);
            int start = (_arteryFlushSampleCursor - copyCount) % ArteryFlushSampleCapacity;
            if (start < 0)
                start += ArteryFlushSampleCapacity;

            for (int i = 0; i < copyCount; i++)
                destination[i] = _arteryFlushMilliseconds[(start + i) % ArteryFlushSampleCapacity];

            return copyCount;
        }

        private static void RecordArteryFlushSample(double elapsedMilliseconds)
        {
            int writeIndex = _arteryFlushSampleCursor % ArteryFlushSampleCapacity;
            _arteryFlushMilliseconds[writeIndex] = elapsedMilliseconds > float.MaxValue
                ? float.MaxValue
                : (float)math.max(0d, elapsedMilliseconds);
            _arteryFlushSampleCursor++;
        }

        private static bool ShouldDropAmbientLateFrameEvents(uint laneHash)
        {
            if (!_lateFrameEventBudgetActive || !LateFrameFlushBudgetExhausted)
                return false;

            _lateFrameCircuitBreakerTripped = true;
            RecordLateFrameCircuitBreakerLane(laneHash);
            if (!_lateFrameTimeBudgetExhausted)
            {
                _lateFrameTimeBudgetExhausted = true;
                CrashTelemetryBuffer.ReportLateFrameLoadShedding(laneHash, _lateFrameEventDispatchBudget);
            }

            if (laneHash == _EnvironmentEventsArteryHash && _droppedEventsCounter < int.MaxValue)
                _droppedEventsCounter++;

            GlobalTelemetryBus.PublishPerformanceWarning(
                _AmbientEventsDropHash,
                laneHash,
                laneHash == _EnvironmentEventsArteryHash ? 1f : _lateFrameEventDispatchBudget);
            return true;
        }

        private static void DropAmbientEnvironmentEvents()
        {
            WeatherEvents.DropPendingAmbient();
            RandomEventEvents.DropPendingAmbient();
            SoundscapeEvents.DropPendingAmbient();
        }

        private static void UpdatePauseFreezeFrameDitherState()
        {
            SystemDispatcher dispatcher = ActiveRuntimeInstance;
            bool paused = dispatcher != null && dispatcher.SimulationPaused;
            if (_pauseFreezeFrameDitherActive == paused)
                return;

            _pauseFreezeFrameDitherActive = paused;
            Shader.SetGlobalFloat(_HectonFreezeFrameDitherId, paused ? 1f : 0f);
            Shader.SetGlobalFloat(_GamePausedId, paused ? 1f : 0f);
        }

        public static void RequestVisualStaticGlitch()
        {
            RequestVisualStaticGlitch(VisualStaticGlitchDurationSeconds);
        }

        public static void RequestVisualStaticGlitch(float durationSeconds)
        {
            float safeDuration = math.max(0.05f, durationSeconds);
            float untilTime = Time.unscaledTime + safeDuration;
            if (untilTime > _visualStaticGlitchUntilTime)
                _visualStaticGlitchUntilTime = untilTime;

            if (!_visualStaticGlitchActive)
            {
                _visualStaticGlitchActive = true;
                Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, 1f);
            }

            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, Time.frameCount & 1023);
        }

        private static void UpdateVisualStaticGlitchState()
        {
            if (!_visualStaticGlitchActive)
                return;

            if (Time.unscaledTime < _visualStaticGlitchUntilTime)
            {
                Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, Time.frameCount & 1023);
                return;
            }

            _visualStaticGlitchActive = false;
            _visualStaticGlitchUntilTime = 0f;
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchId, 0f);
            Shader.SetGlobalFloat(_HectonVisualStaticGlitchSeedId, 0f);
        }

        private static uint CaptureCriticalPerformanceStackHash(uint laneHash)
        {
            uint frameHash = unchecked((uint)Time.frameCount * 747796405u);
            uint budgetHash = unchecked((uint)_lateFrameEventDispatchBudget * 2891336453u);
            return laneHash ^ _CriticalPerformanceSpikeHash ^ frameHash ^ budgetHash;
        }

        private static void TrackPdaBusCongestion(int pendingBeforeFlush, int pendingAfterFlush)
        {
            if (pendingBeforeFlush <= MaxPdaEventsPerFrame && pendingAfterFlush <= 0)
            {
                _pdaOverBudgetConsecutiveFrames = 0;
                return;
            }

            _pdaOverBudgetConsecutiveFrames++;
            if (_pdaOverBudgetConsecutiveFrames != PdaCongestionWarningFrameThreshold)
                return;

            CrashTelemetryBuffer.ReportBusCongestionWarning(
                _PdaEventsQueueHash,
                math.max(pendingBeforeFlush, pendingAfterFlush),
                WorldSpatialHashGrid.ActiveEntityCount);
        }

        private static void BeginLateFrameEventBudget()
        {
            _lateFrameEventDispatchBudget = MaxLateFrameEventsPerFrame;
            _lateFrameCircuitBreakerTripped = false;
            _lateFrameTimeBudgetExhausted = false;
            _lateFrameEventBudgetActive = true;
            _activeLateFrameEventLaneHash = 0u;
            _dominantLateFrameCircuitBreakerLaneHash = 0u;
            _dominantLateFrameCircuitBreakerLaneCount = 0;
            _lateFrameEventBudgetStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            System.Array.Clear(_lateFrameCircuitBreakerLaneHashes, 0, _lateFrameCircuitBreakerLaneHashes.Length);
            System.Array.Clear(_lateFrameCircuitBreakerLaneCounts, 0, _lateFrameCircuitBreakerLaneCounts.Length);
        }

        private static void EndLateFrameEventBudget()
        {
            bool circuitBreakerTripped = _lateFrameCircuitBreakerTripped;
            _lateFrameEventBudgetActive = false;
            _lateFrameEventDispatchBudget = 0;
            _lateFrameCircuitBreakerTripped = false;
            _lateFrameTimeBudgetExhausted = false;
            _lateFrameEventBudgetStartTimestamp = 0L;

            if (circuitBreakerTripped)
            {
                CrashTelemetryBuffer.ReportEventCascadeWarning();
                GlobalTelemetryBus.PublishPerformanceWarning(
                    _LateFrameBudgetWarningHash,
                    _dominantLateFrameCircuitBreakerLaneHash,
                    _dominantLateFrameCircuitBreakerLaneCount);
            }
        }

        private static bool LateFrameFlushBudgetExhausted
        {
            get
            {
                if (_lateFrameEventBudgetStartTimestamp == 0L)
                    return false;

                long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _lateFrameEventBudgetStartTimestamp;
                double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
                return elapsedMilliseconds >= LateFrameEventFlushBudgetMilliseconds;
            }
        }

        private static void RecordLateFrameCircuitBreakerLane(uint queueHash)
        {
            if (queueHash == 0u)
                return;

            for (int i = 0; i < LateFrameCircuitBreakerLaneCapacity; i++)
            {
                uint recordedHash = _lateFrameCircuitBreakerLaneHashes[i];
                if (recordedHash == queueHash)
                {
                    ushort nextCount = _lateFrameCircuitBreakerLaneCounts[i] == ushort.MaxValue
                        ? ushort.MaxValue
                        : (ushort)(_lateFrameCircuitBreakerLaneCounts[i] + 1);
                    _lateFrameCircuitBreakerLaneCounts[i] = nextCount;
                    if (nextCount >= _dominantLateFrameCircuitBreakerLaneCount)
                    {
                        _dominantLateFrameCircuitBreakerLaneHash = queueHash;
                        _dominantLateFrameCircuitBreakerLaneCount = nextCount;
                    }

                    return;
                }

                if (recordedHash != 0u)
                    continue;

                _lateFrameCircuitBreakerLaneHashes[i] = queueHash;
                _lateFrameCircuitBreakerLaneCounts[i] = 1;
                if (_dominantLateFrameCircuitBreakerLaneCount == 0)
                {
                    _dominantLateFrameCircuitBreakerLaneHash = queueHash;
                    _dominantLateFrameCircuitBreakerLaneCount = 1;
                }

                return;
            }
        }

        private void RunFixedStepAccumulator(float dilatedDeltaTime, bool blockGameplayLanes)
        {
            if (dilatedDeltaTime <= 0f)
            {
                _temporalCompressionActive = false;
                RefreshFixedInterpolationAlpha();
                return;
            }

            double maxAccumulatedTime = FixedStepSeconds * MaxFixedSubstepsPerFrame;
            double requestedAccumulatedTime = _fixedStepAccumulator + dilatedDeltaTime;
            bool compressionActive = requestedAccumulatedTime > maxAccumulatedTime;
            _temporalCompressionActive = compressionActive;
            if (compressionActive)
            {
                _temporalCompressionFrameCount++;
                CrashTelemetryBuffer.ReportTemporalCompression();
            }

            _fixedStepAccumulator = System.Math.Min(requestedAccumulatedTime, maxAccumulatedTime);

            int substepCount = 0;
            while (_fixedStepAccumulator >= FixedStepSeconds && substepCount < MaxFixedSubstepsPerFrame)
            {
                DispatchFixedStep((float)FixedStepSeconds, blockGameplayLanes);
                _fixedStepAccumulator -= FixedStepSeconds;
                substepCount++;
            }

            if (substepCount >= MaxFixedSubstepsPerFrame && _fixedStepAccumulator >= FixedStepSeconds)
                _fixedStepAccumulator = 0d;

            RefreshFixedInterpolationAlpha();
        }

        private void RefreshFixedInterpolationAlpha()
        {
            CurrentFixedInterpolationAlpha = math.saturate((float)(_fixedStepAccumulator / FixedStepSeconds));
        }

        private void DispatchFixedStep(float fixedDeltaTime, bool blockGameplayLanes)
        {
#if UNITY_EDITOR && HECTON_HEAP_LOCK_GUARD
            long heapBefore = Profiler.GetMonoUsedSizeLong();
#endif
            using (_fixedUpdateProfilerMarker.Auto())
            {
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<IFixedTickable> lane = _fixedPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IFixedTickable));
#endif
                    using (_fixedLaneProfilerMarkers[laneIndex].Auto())
                    {
                        IFixedTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            rawArray[itemIndex].FixedTick(fixedDeltaTime);
                    }
                }

                using (_postFixedProfilerMarker.Auto())
                {
                    DispatcherJobSwap.BeginPostFixedSwapWindow();
                    try
                    {
                        for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                        {
                            if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                                continue;

                            RegistryBucket<IPostFixedTickable> lane = _postFixedPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                            lane.ValidateNoDestroyedEntriesDebug(nameof(IPostFixedTickable));
#endif
                            IPostFixedTickable[] rawArray = lane.RawArray;
                            int count = lane.Count;
                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                                IPostFixedTickable postFixedTickable = rawArray[itemIndex];
                                int gen0Before = System.GC.CollectionCount(0);
                                _currentPostFixedGcOwner = postFixedTickable;
                                try
                                {
                                    postFixedTickable.PostFixedTick(fixedDeltaTime);
                                }
                                finally
                                {
                                    int gen0After = System.GC.CollectionCount(0);
                                    if (gen0After != gen0Before)
                                    {
                                        _lastPostFixedGcOwner = postFixedTickable;
                                        _lastPostFixedGcFrame = Time.frameCount;
                                        _lastPostFixedGcDelta = gen0After - gen0Before;
                                        _lastPostFixedGcLaneIndex = laneIndex;
                                        _lastPostFixedGcItemIndex = itemIndex;
                                    }

                                    _currentPostFixedGcOwner = null;
                                }
#else
                                rawArray[itemIndex].PostFixedTick(fixedDeltaTime);
#endif
                            }
                        }
                    }
                    finally
                    {
                        DispatcherJobSwap.EndPostFixedSwapWindow();
                    }
                }

                DrainAupNanInquisitor();
            }
#if UNITY_EDITOR && HECTON_HEAP_LOCK_GUARD
            long heapAfter = Profiler.GetMonoUsedSizeLong();
            if (heapAfter > heapBefore)
            {
                Debug.LogError(HeapLockGuardMessage);
                throw new System.InvalidOperationException(HeapLockGuardMessage);
            }
#endif
        }

        private static void DrainAupNanInquisitor()
        {
            int invalidResultCount = AUPMath.ConsumeInvalidResultCount();
            if (invalidResultCount <= 0)
                return;

            CrashTelemetryBuffer.ReportNanPhysicsRecovery();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            float now = Time.unscaledTime;
            if (now < _nextAupNanInquisitorLogTime)
                return;

            _nextAupNanInquisitorLogTime = now + AupNanInquisitorLogIntervalSeconds;
            Debug.LogError(AupNanInquisitorWarningMessage);
#endif
        }

        private void RunFastTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f)
                return;

            _fastTickAccumulator += deltaTime;
            int substeps = 0;
            while (_fastTickAccumulator >= FastTickIntervalSeconds && substeps < MaxCadenceSubstepsPerFrame)
            {
                _fastTickAccumulator -= FastTickIntervalSeconds;
                substeps++;

                using (_fastTickProfilerMarker.Auto())
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                            continue;

                        RegistryBucket<IFastTickable> lane = _fastPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        lane.ValidateNoDestroyedEntriesDebug(nameof(IFastTickable));
#endif
                        using (_fastLaneProfilerMarkers[laneIndex].Auto())
                        {
                            IFastTickable[] rawArray = lane.RawArray;
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                                rawArray[itemIndex].FastTick((float)FastTickIntervalSeconds);
                        }
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _fastTickAccumulator >= FastTickIntervalSeconds)
                _fastTickAccumulator = FastTickIntervalSeconds;
        }

        private void RunUnscaledFastTick(float unscaledDeltaTime, bool blockGameplayLanes)
        {
            if (unscaledDeltaTime <= 0f)
                return;

            _unscaledFastTickAccumulator += unscaledDeltaTime;
            int substeps = 0;
            while (_unscaledFastTickAccumulator >= FastTickIntervalSeconds && substeps < MaxCadenceSubstepsPerFrame)
            {
                _unscaledFastTickAccumulator -= FastTickIntervalSeconds;
                substeps++;

                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<IUnscaledFastTickable> lane = _unscaledFastPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IUnscaledFastTickable));
#endif
                    using (_unscaledFastLaneProfilerMarkers[laneIndex].Auto())
                    {
                        IUnscaledFastTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            rawArray[itemIndex].UnscaledFastTick((float)FastTickIntervalSeconds);
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _unscaledFastTickAccumulator >= FastTickIntervalSeconds)
                _unscaledFastTickAccumulator = FastTickIntervalSeconds;
        }

        private void RunSlowTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f)
                return;

            double slowTickIntervalSeconds = ResolveSlowTickIntervalSeconds();
            _slowTickAccumulator += deltaTime;
            int substeps = 0;
            while (_slowTickAccumulator >= slowTickIntervalSeconds && substeps < MaxCadenceSubstepsPerFrame)
            {
                _slowTickAccumulator -= slowTickIntervalSeconds;
                substeps++;

                using (_slowTickProfilerMarker.Auto())
                {
                    HectonXRRuntimeState.SlowTickHeadAupCache();

                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                            continue;

                        RegistryBucket<ISlowTickable> lane = _slowPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        lane.ValidateNoDestroyedEntriesDebug(nameof(ISlowTickable));
#endif
                        using (_slowLaneProfilerMarkers[laneIndex].Auto())
                        {
                            ISlowTickable[] rawArray = lane.RawArray;
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            {
                                ISlowTickable tickable = rawArray[itemIndex];
                                if (tickable is IBucketedSlowTickable)
                                    continue;

                                tickable.SlowTick();
                            }
                        }
                    }

                    float slowTickDeltaSeconds = (float)slowTickIntervalSeconds;
                    WorldSpatialHashGrid.SlowTickMaintenance(slowTickDeltaSeconds);
                    CombatDamageRuntime.SlowTick(slowTickDeltaSeconds);
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _slowTickAccumulator >= slowTickIntervalSeconds)
                _slowTickAccumulator = slowTickIntervalSeconds;
        }

        private void RunBucketedSlowTick(bool blockGameplayLanes)
        {
            ISimulationBucketer bucketer = _simulationBucketer;
            if (_bucketedSlowTickableCount <= 0 || bucketer == null || !bucketer.IsInitialized)
                return;

            using (_slowTickProfilerMarker.Auto())
            {
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<ISlowTickable> lane = _slowPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(ISlowTickable));
#endif
                    using (_slowLaneProfilerMarkers[laneIndex].Auto())
                    {
                        ISlowTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                        {
                            if (rawArray[itemIndex] is IBucketedSlowTickable bucketedTickable &&
                                bucketer.IsSlowBucketActive(bucketedTickable.SimulationBucketId))
                            {
                                bucketedTickable.SlowTick();
                            }
                        }
                    }
                }
            }
        }

        private double ResolveSlowTickIntervalSeconds()
        {
            if (Volatile.Read(ref _homeostasisSlowTick2Hz) != 0)
                return HomeostasisEmergencySlowTickIntervalSeconds;

            ISimulationBucketer bucketer = _simulationBucketer;
            if (bucketer != null &&
                bucketer.IsInitialized &&
                bucketer.SlowBucketCount == SimulationBucketConstants.StandardSlowBucketCount &&
                bucketer.ActiveSlowBucketCount <= SimulationBucketConstants.MinimumActiveSlowBucketCount)
            {
                return math.max(
                    _thermalCriticalSlowTickActive ? ThermalCriticalSlowTickIntervalSeconds : SlowTickIntervalSeconds,
                    SlowTickIntervalSeconds * 2.0);
            }

            return _thermalCriticalSlowTickActive ? ThermalCriticalSlowTickIntervalSeconds : SlowTickIntervalSeconds;
        }

        private void RunColdTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f)
                return;

            _coldTickAccumulator += deltaTime;
            int substeps = 0;
            while (_coldTickAccumulator >= ColdTickIntervalSeconds && substeps < MaxCadenceSubstepsPerFrame)
            {
                _coldTickAccumulator -= ColdTickIntervalSeconds;
                substeps++;

                using (_coldTickProfilerMarker.Auto())
                {
                    for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                    {
                        if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                            continue;

                        RegistryBucket<IColdTickable> lane = _coldPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        lane.ValidateNoDestroyedEntriesDebug(nameof(IColdTickable));
#endif
                        using (_coldLaneProfilerMarkers[laneIndex].Auto())
                        {
                            IColdTickable[] rawArray = lane.RawArray;
                            int count = lane.Count;

                            for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                                rawArray[itemIndex].ColdTick();
                        }
                    }
                }
            }

            if (substeps == MaxCadenceSubstepsPerFrame && _coldTickAccumulator >= ColdTickIntervalSeconds)
                _coldTickAccumulator = ColdTickIntervalSeconds;
        }

        private void RunFrostTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f)
                return;

            _frostTickAccumulator += deltaTime;
            if (_frostTickAccumulator < FrostTickIntervalSeconds)
                return;

            _frostTickAccumulator -= FrostTickIntervalSeconds;

            using (_frostTickProfilerMarker.Auto())
            {
                for (int laneIndex = 0; laneIndex < LaneCount; laneIndex++)
                {
                    if (ShouldSkipLaneDuringBootstrap(laneIndex, blockGameplayLanes))
                        continue;

                    RegistryBucket<IFrostTickable> lane = _frostPriorityLanes[laneIndex];
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    lane.ValidateNoDestroyedEntriesDebug(nameof(IFrostTickable));
#endif
                    using (_frostLaneProfilerMarkers[laneIndex].Auto())
                    {
                        IFrostTickable[] rawArray = lane.RawArray;
                        int count = lane.Count;

                        for (int itemIndex = count - 1; itemIndex >= 0; itemIndex--)
                            rawArray[itemIndex].FrostTick();
                    }
                }
            }
        }

        private static void EnsureDispatcherRaycastBuffers()
        {
            if (!_pendingDispatcherRaycastCommands.IsCreated)
            {
                _pendingDispatcherRaycastCommands = new NativeQueue<RaycastCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RaycastCommand>[256] - dispatcher-owned global deferred physics request lane - owner: SystemDispatcher
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingDispatcherRaycastCommands,
                    MaxQueuedDispatcherRaycasts,
                    nameof(SystemDispatcher),
                    nameof(_pendingDispatcherRaycastCommands),
                    NativeAllocationLifetime.Session);
                PrewarmQueue(ref _pendingDispatcherRaycastCommands, MaxQueuedDispatcherRaycasts);
            }

            if (!_scheduledDispatcherRaycastCommands.IsCreated)
            {
                _scheduledDispatcherRaycastCommands = new NativeList<RaycastCommand>(MaxQueuedDispatcherRaycasts, Allocator.Persistent); // COLD ALLOC: NativeList<RaycastCommand>[1024] - dispatcher-owned scheduled deferred raycast commands - owner: SystemDispatcher
                NativeMemorySentinel.RegisterNativeList(
                    _scheduledDispatcherRaycastCommands,
                    nameof(SystemDispatcher),
                    nameof(_scheduledDispatcherRaycastCommands),
                    NativeAllocationLifetime.Session);
            }

            if (!_scheduledDispatcherRaycastHits.IsCreated)
            {
                IDataVault dataVault = GlobalRegistry.DataVault;
                if (dataVault != null)
                {
                    _scheduledDispatcherRaycastHitsHandle = dataVault.GetBufferHandle<RaycastHit>(
                        BufferID.DispatcherRaycastHits,
                        MaxQueuedDispatcherRaycasts,
                        SystemID.SystemDispatcher,
                        NativeArrayOptions.ClearMemory);
                    _scheduledDispatcherRaycastHits = _scheduledDispatcherRaycastHitsHandle.Resolve(dataVault);
                    if (_scheduledDispatcherRaycastHits.IsCreated)
                    {
                        _scheduledDispatcherRaycastHitsVaultOwned = true;
                        return;
                    }

                    _scheduledDispatcherRaycastHitsHandle = default;
                }

                _scheduledDispatcherRaycastHits = H8Memory.Allocate<RaycastHit>(MaxQueuedDispatcherRaycasts, SystemID.SystemDispatcher, Allocator.Persistent, NativeArrayOptions.ClearMemory); // FALLBACK COLD ALLOC: NativeArray<RaycastHit>[1024] - deferred raycast hit lane when DataVault is unavailable - owner: SystemDispatcher
                NativeMemorySentinel.RegisterNativeArray(
                    _scheduledDispatcherRaycastHits,
                    nameof(SystemDispatcher),
                    nameof(_scheduledDispatcherRaycastHits),
                    NativeAllocationLifetime.Session);
                _scheduledDispatcherRaycastHitsVaultOwned = false;
            }
        }

        private static bool TryResolveDispatcherRaycastHits()
        {
            EnsureDispatcherRaycastBuffers();
            if (!_scheduledDispatcherRaycastHitsVaultOwned)
                return _scheduledDispatcherRaycastHits.IsCreated;

            IDataVault dataVault = GlobalRegistry.DataVault;
            if (dataVault == null)
                return false;

            NativeArray<RaycastHit> resolved = _scheduledDispatcherRaycastHitsHandle.Resolve(dataVault);
            if (!resolved.IsCreated)
                return false;

            _scheduledDispatcherRaycastHits = resolved;
            return true;
        }

        private static bool TryLockDispatcherRaycastHitsVaultBuffer()
        {
            if (!_scheduledDispatcherRaycastHitsVaultOwned || _scheduledDispatcherRaycastHitsVaultLocked)
                return true;

            IDataVault dataVault = GlobalRegistry.DataVault;
            if (dataVault == null || !dataVault.TryLockBuffer(BufferID.DispatcherRaycastHits))
                return false;

            _scheduledDispatcherRaycastHitsVaultLocked = true;
            return true;
        }

        private static void UnlockDispatcherRaycastHitsVaultBuffer()
        {
            if (!_scheduledDispatcherRaycastHitsVaultLocked)
                return;

            IDataVault dataVault = GlobalRegistry.DataVault;
            if (dataVault != null)
                dataVault.TryUnlockBuffer(BufferID.DispatcherRaycastHits);

            _scheduledDispatcherRaycastHitsVaultLocked = false;
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

        private static void ScheduleDispatcherRaycasts()
        {
            if (_dispatcherRaycastsScheduled || _pendingDispatcherRaycastCount <= 0)
                return;

            EnsureDispatcherRaycastBuffers();
            if (!_pendingDispatcherRaycastCommands.IsCreated ||
                !_scheduledDispatcherRaycastCommands.IsCreated ||
                !TryResolveDispatcherRaycastHits())
            {
                return;
            }

            if (!TryLockDispatcherRaycastHitsVaultBuffer())
                return;

            using (_dispatcherRaycastScheduleProfilerMarker.Auto())
            {
                _scheduledDispatcherRaycastCommands.Clear();
                int pendingCount = _pendingDispatcherRaycastCount;
                int scheduledCount = 0;
                while (scheduledCount < pendingCount &&
                       _pendingDispatcherRaycastCommands.TryDequeue(out RaycastCommand command))
                {
                    _scheduledDispatcherRaycastCommands.AddNoResize(command);
                    _scheduledDispatcherRaycastReceivers[scheduledCount] = _pendingDispatcherRaycastReceivers[scheduledCount];
                    _scheduledDispatcherRaycastRequestIds[scheduledCount] = _pendingDispatcherRaycastRequestIds[scheduledCount];
                    _pendingDispatcherRaycastReceivers[scheduledCount] = null;
                    _pendingDispatcherRaycastRequestIds[scheduledCount] = 0;
                    scheduledCount++;
                }

                for (int clearIndex = scheduledCount; clearIndex < pendingCount; clearIndex++)
                {
                    _pendingDispatcherRaycastReceivers[clearIndex] = null;
                    _pendingDispatcherRaycastRequestIds[clearIndex] = 0;
                }

                _pendingDispatcherRaycastCount = 0;
                if (scheduledCount <= 0)
                {
                    UnlockDispatcherRaycastHitsVaultBuffer();
                    return;
                }

                _scheduledDispatcherRaycastCount = scheduledCount;
                _scheduledDispatcherRaycastHandle = RaycastCommand.ScheduleBatch(
                    _scheduledDispatcherRaycastCommands.AsDeferredJobArray(),
                    _scheduledDispatcherRaycastHits,
                    DispatcherRaycastMinCommandsPerJob,
                    default);
                _dispatcherRaycastsScheduled = true;
            }
        }

        private static void CompleteDispatcherRaycasts()
        {
            if (!_dispatcherRaycastsScheduled)
                return;

            using (_dispatcherRaycastCompleteProfilerMarker.Auto())
            {
                if (!DispatcherJobSwap.TryComplete(ref _scheduledDispatcherRaycastHandle, forceComplete: false))
                    return;

                _dispatcherRaycastsScheduled = false;

                for (int i = 0; i < _scheduledDispatcherRaycastCount; i++)
                {
                    IDispatcherRaycastReceiver receiver = _scheduledDispatcherRaycastReceivers[i];
                    if (receiver == null)
                        continue;

                    receiver.ConsumeDispatcherRaycastHit(_scheduledDispatcherRaycastRequestIds[i], _scheduledDispatcherRaycastHits[i]);
                    _scheduledDispatcherRaycastReceivers[i] = null;
                    _scheduledDispatcherRaycastRequestIds[i] = 0;
                }

                _scheduledDispatcherRaycastCount = 0;
                UnlockDispatcherRaycastHitsVaultBuffer();
            }
        }

        private static void DisposeDispatcherRaycastBuffers()
        {
            if (_dispatcherRaycastsScheduled)
            {
                DispatcherJobSwap.TryComplete(ref _scheduledDispatcherRaycastHandle, forceComplete: true);
                _dispatcherRaycastsScheduled = false;
                UnlockDispatcherRaycastHitsVaultBuffer();
            }
            else
            {
                _scheduledDispatcherRaycastHandle = default;
                UnlockDispatcherRaycastHitsVaultBuffer();
            }

            if (_pendingDispatcherRaycastCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SystemDispatcher), nameof(_pendingDispatcherRaycastCommands));
                _pendingDispatcherRaycastCommands.Dispose();
                _pendingDispatcherRaycastCommands = default;
            }

            if (_scheduledDispatcherRaycastCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeList(nameof(SystemDispatcher), nameof(_scheduledDispatcherRaycastCommands));
                _scheduledDispatcherRaycastCommands.Dispose();
                _scheduledDispatcherRaycastCommands = default;
            }

            if (_scheduledDispatcherRaycastHits.IsCreated)
            {
                if (_scheduledDispatcherRaycastHitsVaultOwned)
                {
                    _scheduledDispatcherRaycastHits = default;
                    _scheduledDispatcherRaycastHitsHandle = default;
                    _scheduledDispatcherRaycastHitsVaultOwned = false;
                }
                else
                {
                    NativeMemorySentinel.UnregisterNativeArray(_scheduledDispatcherRaycastHits);
                    H8Memory.Release(ref _scheduledDispatcherRaycastHits);
                    _scheduledDispatcherRaycastHitsHandle = default;
                }
            }

            _pendingDispatcherRaycastCount = 0;
            _scheduledDispatcherRaycastCount = 0;
            System.Array.Clear(_pendingDispatcherRaycastReceivers, 0, _pendingDispatcherRaycastReceivers.Length);
            System.Array.Clear(_pendingDispatcherRaycastRequestIds, 0, _pendingDispatcherRaycastRequestIds.Length);
            System.Array.Clear(_scheduledDispatcherRaycastReceivers, 0, _scheduledDispatcherRaycastReceivers.Length);
            System.Array.Clear(_scheduledDispatcherRaycastRequestIds, 0, _scheduledDispatcherRaycastRequestIds.Length);
        }

        private static bool ShouldSkipLaneDuringBootstrap(int laneIndex, bool blockGameplayLanes)
        {
            if (!blockGameplayLanes)
                return false;

            // Bootstrap gates the player lane only. World/environment systems must keep
            // ticking so startup queues, residency, and spawn drains can complete.
            return laneIndex == GetLaneIndex(PriorityLayer.Player);
        }

        private static int GetLaneIndex(PriorityLayer layer)
        {
            switch (layer)
            {
                case PriorityLayer.Core:
                    return 0;
                case PriorityLayer.Environment:
                    return 1;
                case PriorityLayer.Player:
                    return 2;
                case PriorityLayer.UI:
                    return 3;
                default:
                    return 0;
            }
        }

        private static long BeginDispatcherPhaseTiming()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return System.Diagnostics.Stopwatch.GetTimestamp();
#else
            return 0L;
#endif
        }

        private static void EndDispatcherPhaseTiming(long startTimestamp, string phaseName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowDispatcherPhaseWarningMilliseconds)
            {
                float now = Time.unscaledTime;
                if (now >= _nextDispatcherPhaseWarningLogTime)
                {
                    _nextDispatcherPhaseWarningLogTime = now + DispatcherPhaseWarningLogIntervalSeconds;
                    GlobalTelemetryBus.PublishJobBarrierStall(
                        nameof(SystemDispatcher),
                        phaseName,
                        (float)elapsedMilliseconds);
                }
            }
#endif
        }

        private void CompleteFoveatedFrameJobs()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            using (_foveatedCompleteProfilerMarker.Auto())
            {
                _foveatedSimulationManager.TryCompleteFrameJobs();
            }

            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowJobCompleteWarningMilliseconds)
            {
                float now = Time.unscaledTime;
                if (now >= _nextFoveatedFrameWarningLogTime)
                {
                    _nextFoveatedFrameWarningLogTime = now + DispatcherPhaseWarningLogIntervalSeconds;
                    GlobalTelemetryBus.PublishJobBarrierStall(
                        "FoveatedSimulationManager",
                        "LateFrameComplete",
                        (float)elapsedMilliseconds);
                }
            }
#else
            using (_foveatedCompleteProfilerMarker.Auto())
            {
                _foveatedSimulationManager.TryCompleteFrameJobs();
            }
#endif
        }

    }

    /// <summary>
    /// Per-camera SRP callback context exposed to registry-managed render owners.
    /// </summary>
    public static class GlobalRenderContext
    {
        private static Camera _currentCamera;
        private static ScriptableRenderContext _currentContext;

        /// <summary>
        /// Camera currently being rendered by the SRP dispatcher.
        /// </summary>
        public static Camera CurrentCamera => _currentCamera;

        /// <summary>
        /// Scriptable render context currently being processed.
        /// </summary>
        public static ScriptableRenderContext CurrentContext => _currentContext;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _currentCamera = null;
            _currentContext = default;
        }

        internal static void SetCurrent(ScriptableRenderContext context, Camera camera)
        {
            _currentContext = context;
            _currentCamera = camera;
        }

        internal static void Clear()
        {
            _currentContext = default;
            _currentCamera = null;
        }
    }

    /// <summary>
    /// Bootstrap-owned SRP callback fan-out for registry-managed render owners.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9940)]
    public sealed class RenderDispatcher : MonoBehaviour, IServiceHeartbeat, IServiceShutdown
    {
        internal static RenderDispatcher ActiveRuntimeInstance { get; private set; }

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        private struct RenderSettingsSnapshot
        {
            public bool Fog;
            public FogMode FogMode;
            public Color FogColor;
            public float FogDensity;
            public Material Skybox;
            public AmbientMode AmbientMode;
            public Color AmbientLight;
            public Color AmbientSkyColor;
            public Color AmbientEquatorColor;
            public Color AmbientGroundColor;
            public float AmbientIntensity;
            public float ReflectionIntensity;

            public static RenderSettingsSnapshot Capture()
            {
                return new RenderSettingsSnapshot
                {
                    Fog = RenderSettings.fog,
                    FogMode = RenderSettings.fogMode,
                    FogColor = RenderSettings.fogColor,
                    FogDensity = RenderSettings.fogDensity,
                    Skybox = AtmosphereDirector.Skybox,
                    AmbientMode = RenderSettings.ambientMode,
                    AmbientLight = RenderSettings.ambientLight,
                    AmbientSkyColor = RenderSettings.ambientSkyColor,
                    AmbientEquatorColor = RenderSettings.ambientEquatorColor,
                    AmbientGroundColor = RenderSettings.ambientGroundColor,
                    AmbientIntensity = RenderSettings.ambientIntensity,
                    ReflectionIntensity = RenderSettings.reflectionIntensity
                };
            }

            public void Restore()
            {
                RenderSettings.fog = Fog;
                RenderSettings.fogMode = FogMode;
                RenderSettings.fogColor = FogColor;
                RenderSettings.fogDensity = FogDensity;
                AtmosphereDirector.SetSkybox(Skybox);
                IGIRelaySystem giRelay = GlobalRegistry.GIRelay;
                bool giRelayAmbientAuthority = giRelay != null && giRelay.IsAmbientProbeAuthorityActive;
                if (!giRelayAmbientAuthority)
                {
                    RenderSettings.ambientMode = AmbientMode;
                    RenderSettings.ambientLight = AmbientLight;
                    RenderSettings.ambientSkyColor = AmbientSkyColor;
                    RenderSettings.ambientEquatorColor = AmbientEquatorColor;
                    RenderSettings.ambientGroundColor = AmbientGroundColor;
                    RenderSettings.ambientIntensity = AmbientIntensity;
                }
                RenderSettings.reflectionIntensity = ReflectionIntensity;
            }
        }

        private bool _hasPendingRenderSettingsRestore;
        private Camera _pendingRenderSettingsCamera;
        private RenderSettingsSnapshot _pendingRenderSettingsSnapshot;
        private bool _serviceRegistered;

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            _hasPendingRenderSettingsRestore = false;
            _pendingRenderSettingsCamera = null;
        }

        /// <summary>
        /// Registers this dispatcher as the authoritative SRP render dispatcher.
        /// </summary>
        public void InitializeService()
        {
            if (_serviceRegistered)
                return;

            RenderDispatcher registeredDispatcher = GlobalRegistry.RenderDispatcher;
            if (registeredDispatcher != null && !ReferenceEquals(registeredDispatcher, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterRenderDispatcher(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.RenderDispatcher, this);

        }

        private void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.beginCameraRendering += HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RenderPipelineManager.endCameraRendering += HandleEndCameraRendering;
        }

        private void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= HandleBeginCameraRendering;
            RenderPipelineManager.endCameraRendering -= HandleEndCameraRendering;
            RestorePendingRenderSettings();
        }

        private void OnDestroy()
        {
            ShutdownServiceState();
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState();
        }

        private void ShutdownServiceState()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            OnDisable();
            GlobalRenderContext.Clear();
            _pendingRenderSettingsCamera = null;
            _hasPendingRenderSettingsRestore = false;

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.RenderDispatcher, this))
                GlobalRegistry.UnregisterRenderDispatcher(this);

            _serviceRegistered = false;
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            HectonFloatingOrigin.PublishCurrentGlobalOffsetsForRenderLoop();
            RestorePendingRenderSettings();

            RegistryBucket<IRenderable> renderables = GlobalRegistry.Renderables;
            int count = renderables.Count;
            if (count <= 0)
                return;

            IRenderable[] rawArray = renderables.RawArray;
            float deltaTime = SystemDispatcher.CurrentFrameDeltaTime;
            _pendingRenderSettingsSnapshot = RenderSettingsSnapshot.Capture();
            _pendingRenderSettingsCamera = camera;
            _hasPendingRenderSettingsRestore = true;
            GlobalRenderContext.SetCurrent(context, camera);

            try
            {
                for (int i = count - 1; i >= 0; i--)
                {
                    IRenderable renderable = rawArray[i];
                    if (renderable == null)
                        continue;

                    renderable.Render(deltaTime);
                }
            }
            finally
            {
                GlobalRenderContext.Clear();
            }
        }

        private void HandleEndCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            if (!_hasPendingRenderSettingsRestore)
            {
                InputLatencyTracker.MarkRenderCompleted();
                return;
            }

            if (_pendingRenderSettingsCamera != null && camera != _pendingRenderSettingsCamera)
                return;

            RestorePendingRenderSettings();
            InputLatencyTracker.MarkRenderCompleted();
        }

        private void RestorePendingRenderSettings()
        {
            if (!_hasPendingRenderSettingsRestore)
                return;

            _pendingRenderSettingsSnapshot.Restore();
            _pendingRenderSettingsSnapshot = default;
            _pendingRenderSettingsCamera = null;
            _hasPendingRenderSettingsRestore = false;
        }
    }

    /// <summary>
    /// LockBufferForWrite upload helpers used by owned runtime graphics buffers.
    /// </summary>
    internal static class GraphicsBufferUploadUtility
    {
        /// <summary>
        /// Creates a structured buffer configured for standard SetData uploads.
        /// </summary>
        public static GraphicsBuffer CreateStructuredBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Creates a structured buffer configured for direct CPU writes.
        /// </summary>
        public static GraphicsBuffer CreateStructuredLockBuffer<T>(int count) where T : struct
        {
            return new GraphicsBuffer(
                GraphicsBuffer.Target.Structured,
                GraphicsBuffer.UsageFlags.LockBufferForWrite,
                count,
                UnsafeUtility.SizeOf<T>());
        }

        /// <summary>
        /// Uploads a blittable native array into a graphics buffer using one memcpy.
        /// </summary>
        public static void UploadNativeArray<T>(GraphicsBuffer destination, NativeArray<T> source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source.IsCreated ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            unsafe
            {
                void* sourcePtr = NativeArrayUnsafeUtility.GetUnsafeReadOnlyPtr(source);
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<T>() * mapped.Length;
                if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SystemDispatcher));
            }

            destination.UnlockBufferAfterWrite<T>(safeCount);
        }

        /// <summary>
        /// Uploads a blittable managed array into a graphics buffer using one memcpy.
        /// </summary>
        public static unsafe void UploadArray<T>(GraphicsBuffer destination, T[] source, int count) where T : unmanaged
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source != null ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            NativeArray<T> mapped = destination.LockBufferForWrite<T>(0, safeCount);
            fixed (T* sourcePtr = source)
            {
                void* destinationPtr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(mapped);
                long copyBytes = (long)UnsafeUtility.SizeOf<T>() * safeCount;
                long destinationBytes = (long)UnsafeUtility.SizeOf<T>() * mapped.Length;
                if (!UnsafeMemoryCopyGuard.TryMemCpy(destinationPtr, destinationBytes, sourcePtr, copyBytes))
                    UnsafeMemoryCopyGuard.ReportRejectedCopy(nameof(SystemDispatcher));
            }

            destination.UnlockBufferAfterWrite<T>(safeCount);
        }

        /// <summary>
        /// Uploads a blittable managed array into a graphics buffer using SetData.
        /// Use for infrequent uploads where avoiding lock-contention stalls matters more than raw memcpy throughput.
        /// </summary>
        public static void UploadArraySetData<T>(GraphicsBuffer destination, T[] source, int count) where T : struct
        {
            int safeCount = ResolveSafeWriteCount<T>(destination, source != null ? source.Length : 0, count);
            if (safeCount <= 0)
                return;

            destination.SetData(source, 0, 0, safeCount);
        }

        private static int ResolveSafeWriteCount<T>(GraphicsBuffer destination, int sourceLength, int requestedCount) where T : struct
        {
            if (destination == null || requestedCount <= 0 || sourceLength <= 0 || destination.count <= 0)
                return 0;

            int stride = UnsafeUtility.SizeOf<T>();
            if (destination.stride != stride)
                return 0;

            return math.min(math.min(requestedCount, sourceLength), destination.count);
        }
    }
}
