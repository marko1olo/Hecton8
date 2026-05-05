using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Hecton8.AI;
using Hecton8.Atmosphere;
using Hecton8.Audio;
using Hecton8.Bootstrap;
using Hecton8.Celestial;
using Hecton8.Construction;
using Hecton8.Environment;
using Hecton8.Gameplay;
using Hecton8.Inventory;
using Hecton8.Narrative;
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
    public sealed class SystemDispatcher : MonoBehaviour
    {
        private const int LaneCount = 4;
        private const float DefaultSlowTickIntervalSeconds = 0.5f;
        private const double SlowJobCompleteWarningMilliseconds = 1.0;
        private const double SlowDispatcherPhaseWarningMilliseconds = 100.0;
        private const int MaxQueuedDispatcherRaycasts = 256;
        private const int DispatcherRaycastMinCommandsPerJob = 1;
        private const float FixedStepSeconds = 0.02f;
        private const int MaxFixedSubstepsPerFrame = 3;
        private const int MaxLateFrameEventsPerFrame = 1000;
        private const int MaxPdaEventsPerFrame = 30;
        private const int PdaCongestionWarningFrameThreshold = 5;
        private const int LateFrameCircuitBreakerLaneCapacity = 32;
        private const double LateFrameEventFlushBudgetMilliseconds = 2.0;
        private const float PauseDepthOfFieldBlendSeconds = 0.15f;
        private const float AupNanInquisitorLogIntervalSeconds = 5f;
        private const float DispatcherPhaseWarningLogIntervalSeconds = 5f;
        private const string AupNanInquisitorWarningMessage = "[SystemDispatcher] AUP NaN-Inquisitor detected invalid camera-relative results.";
        private static readonly ProfilerMarker _updateProfilerMarker = new ProfilerMarker("H8.Dispatcher.Update");
        private static readonly ProfilerMarker _fixedUpdateProfilerMarker = new ProfilerMarker("H8.Dispatcher.FixedUpdate");
        private static readonly ProfilerMarker _slowTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.SlowTick");
        private static readonly ProfilerMarker _lateFrameProfilerMarker = new ProfilerMarker("H8.Dispatcher.LateFrame");
        private static readonly ProfilerMarker _postFixedProfilerMarker = new ProfilerMarker("H8.Dispatcher.PostFixed");
        private static readonly ProfilerMarker _lateFrameCommandQueueDrainProfilerMarker = new ProfilerMarker("H8.Dispatcher.CommandQueue.Drain");
        private static readonly ProfilerMarker _foveatedCompleteProfilerMarker = new ProfilerMarker("H8.Dispatcher.Foveated.Complete");
        private static readonly ProfilerMarker _dispatcherRaycastScheduleProfilerMarker = new ProfilerMarker("H8.Dispatcher.Raycast.Schedule");
        private static readonly ProfilerMarker _dispatcherRaycastCompleteProfilerMarker = new ProfilerMarker("H8.Dispatcher.Raycast.Complete");
        private static readonly uint _ThreadSafeCommandQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ThreadSafeCommandQueue"));
        private static readonly uint _StorageReservationCommitResolvedQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ThreadSafeCommandQueue.StorageReservationCommit"));
        private static readonly uint _ModCommandDispatcherQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("ModCommandDispatcher"));
        private static readonly uint _CoreEventsArteryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SystemDispatcher.CoreEvents"));
        private static readonly uint _EnvironmentEventsArteryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SystemDispatcher.EnvironmentEvents"));
        private static readonly uint _PlayerEventsArteryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SystemDispatcher.PlayerEvents"));
        private static readonly uint _BaseEventsArteryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SystemDispatcher.BaseEvents"));
        private static readonly uint _AIEventsArteryHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SystemDispatcher.AIEvents"));
        private static readonly uint _PdaEventsQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("PDAEvents"));
        private static readonly uint _LateFrameTickablesQueueHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SystemDispatcher.LateFrameTickables"));
        private static readonly uint _LateFrameBudgetWarningHash = unchecked((uint)Hecton.Localization.LocHash.Compute("SystemDispatcher.LateFrameEventBudget"));
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
        private static readonly ProfilerMarker[] _slowLaneProfilerMarkers =
        {
            new ProfilerMarker("H8.Dispatcher.Slow.Core"),
            new ProfilerMarker("H8.Dispatcher.Slow.Environment"),
            new ProfilerMarker("H8.Dispatcher.Slow.Player"),
            new ProfilerMarker("H8.Dispatcher.Slow.UI"),
        };

        // COLD ALLOC: RegistryBucket<IUpdatable>[4] — fixed dispatcher lanes ordered by bootstrap layer — owner: SystemDispatcher
        private static readonly RegistryBucket<IUpdatable>[] _priorityLanes =
        {
            new RegistryBucket<IUpdatable>(256),
            new RegistryBucket<IUpdatable>(256),
            new RegistryBucket<IUpdatable>(128),
            new RegistryBucket<IUpdatable>(64),
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

        private static IFoveatedDispatcher _foveatedSimulationManager = new FoveatedSimulationManager();
        // COLD ALLOC: IDispatcherRaycastReceiver[256] — dispatcher-owned pending raycast receivers — owner: SystemDispatcher
        private static readonly IDispatcherRaycastReceiver[] _pendingDispatcherRaycastReceivers = new IDispatcherRaycastReceiver[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: int[256] — dispatcher-owned pending raycast request ids — owner: SystemDispatcher
        private static readonly int[] _pendingDispatcherRaycastRequestIds = new int[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: IDispatcherRaycastReceiver[256] — dispatcher-owned scheduled raycast receivers — owner: SystemDispatcher
        private static readonly IDispatcherRaycastReceiver[] _scheduledDispatcherRaycastReceivers = new IDispatcherRaycastReceiver[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: int[256] — dispatcher-owned scheduled raycast request ids — owner: SystemDispatcher
        private static readonly int[] _scheduledDispatcherRaycastRequestIds = new int[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: uint[32] - late-frame circuit-breaker lane hash counters - owner: SystemDispatcher
        private static readonly uint[] _lateFrameCircuitBreakerLaneHashes = new uint[LateFrameCircuitBreakerLaneCapacity];
        // COLD ALLOC: ushort[32] - late-frame circuit-breaker lane hit counters - owner: SystemDispatcher
        private static readonly ushort[] _lateFrameCircuitBreakerLaneCounts = new ushort[LateFrameCircuitBreakerLaneCapacity];
        private float _slowTickAccumulator;
        private float _fixedStepAccumulator;
        private bool _serviceRegistered;
        private static int _lateFrameEventDispatchBudget;
        private static bool _lateFrameEventBudgetActive;
        private static bool _lateFrameCircuitBreakerTripped;
        private static bool _lateFrameTimeBudgetExhausted;
        private static uint _activeLateFrameEventLaneHash;
        private static uint _dominantLateFrameCircuitBreakerLaneHash;
        private static long _lateFrameEventBudgetStartTimestamp;

        internal static float CurrentFrameDeltaTime { get; private set; }

        internal static float CurrentFrameUnscaledDeltaTime { get; private set; }

        internal static SystemDispatcher ActiveRuntimeInstance { get; private set; }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static object _currentPostFixedGcOwner;
        private static object _lastPostFixedGcOwner;
        private static int _lastPostFixedGcFrame = -1;
        private static int _lastPostFixedGcDelta;
        private static int _lastPostFixedGcLaneIndex = -1;
        private static int _lastPostFixedGcItemIndex = -1;
#endif
        private static ushort _dominantLateFrameCircuitBreakerLaneCount;
        private static float _nextAupNanInquisitorLogTime;
        private static float _nextDispatcherPhaseWarningLogTime;
        private static float _nextFoveatedFrameWarningLogTime;
        private static float _nextJobHandleWarningLogTime;
        private static float _pauseDepthOfFieldWeight;
        private static bool _temporalCompressionActive;
        private static bool _pauseDepthOfFieldTargetActive;
        private static int _temporalCompressionFrameCount;
        private static int _pdaOverBudgetConsecutiveFrames;
        private static NativeQueue<RaycastCommand> _pendingDispatcherRaycastCommands;
        private static NativeList<RaycastCommand> _scheduledDispatcherRaycastCommands;
        private static NativeArray<RaycastHit> _scheduledDispatcherRaycastHits;
        private static JobHandle _scheduledDispatcherRaycastHandle;
        private static bool _dispatcherRaycastsScheduled;
        private static int _pendingDispatcherRaycastCount;
        private static int _scheduledDispatcherRaycastCount;

        static SystemDispatcher()
        {
            _foveatedSimulationManager.InitializeRuntime();
        }

        /// <summary>
        /// True when this frame dropped excess fixed-step catch-up time instead of exceeding the substep cap.
        /// </summary>
        public static bool IsTemporalCompressionActive => _temporalCompressionActive;

        /// <summary>
        /// Total frame count where temporal compression was entered since subsystem reset.
        /// </summary>
        public static int TemporalCompressionFrameCount => _temporalCompressionFrameCount;

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
            System.Array.Clear(_lateFrameCircuitBreakerLaneHashes, 0, _lateFrameCircuitBreakerLaneHashes.Length);
            System.Array.Clear(_lateFrameCircuitBreakerLaneCounts, 0, _lateFrameCircuitBreakerLaneCounts.Length);
            _nextAupNanInquisitorLogTime = 0f;
            _nextDispatcherPhaseWarningLogTime = 0f;
            _nextFoveatedFrameWarningLogTime = 0f;
            _nextJobHandleWarningLogTime = 0f;
            _pauseDepthOfFieldWeight = 0f;
            _pauseDepthOfFieldTargetActive = false;
            _temporalCompressionActive = false;
            _temporalCompressionFrameCount = 0;
            _pdaOverBudgetConsecutiveFrames = 0;
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

        internal static void SetVoxelTeardownBackpressure(bool active, int pendingChunkCount)
        {
            _foveatedSimulationManager.SetVoxelTeardownBackpressure(active, pendingChunkCount);
        }

        /// <summary>
        /// Requests pause-menu depth-of-field isolation on the dispatcher cadence.
        /// </summary>
        public static void RequestPauseDepthOfField(bool active)
        {
            _pauseDepthOfFieldTargetActive = active;
        }

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

            return GetSlowLane(layer).TryRegister(item);
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

            GetSlowLane(layer).TryUnregister(item);
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
        /// Clears every dispatcher lane.
        /// </summary>
        public static void ClearAllLanes()
        {
            for (int i = 0; i < LaneCount; i++)
            {
                _priorityLanes[i].Clear();
                _fixedPriorityLanes[i].Clear();
                _slowPriorityLanes[i].Clear();
                _lateFramePriorityLanes[i].Clear();
                _postFixedPriorityLanes[i].Clear();
            }

            _foveatedSimulationManager.ResetRuntimeState();
        }

        private void Awake()
        {
            if (ActiveRuntimeInstance == null)
                ActiveRuntimeInstance = this;

            _slowTickAccumulator = 0f;
            _fixedStepAccumulator = 0f;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.Dispatcher, this))
            {
                DisposeDispatcherRaycastBuffers();
                ThreadSafeCommandQueue.Shutdown();
                GlobalRegistry.UnregisterSystemDispatcher(this);
            }

            _serviceRegistered = false;
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
            GlobalRegistry.RegisterSystemDispatcher(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.Dispatcher, this);
        }

        private void Update()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.DispatcherUpdate);
#endif
            using (_updateProfilerMarker.Auto())
            {
#if UNITY_EDITOR
                NativeAllocationTrackerRuntimeBridge.NotifyDispatcherHeartbeat();
#endif
                if (BootstrapStatus.TryTriggerSafeHalt())
                    BootstrapBiosErrorOverlay.Show(BootstrapStatus.SafeHaltDisplayMessage);

                float deltaTime = Time.deltaTime;
                CurrentFrameDeltaTime = deltaTime;
                CurrentFrameUnscaledDeltaTime = Time.unscaledDeltaTime;
                TickPauseDepthOfField(CurrentFrameUnscaledDeltaTime);
                long beginDispatcherTimestamp = BeginDispatcherPhaseTiming();
                _foveatedSimulationManager.BeginDispatcherFrame(deltaTime);
                EndDispatcherPhaseTiming(beginDispatcherTimestamp, "FoveatedSimulationManager.BeginDispatcherFrame");
                PredatorCognitionDomain.BeginDispatcherFrame(Time.frameCount);
                bool blockGameplayLanes = Application.isPlaying &&
                                          BootstrapState.HasActiveInstance &&
                                          !BootstrapState.IsGameReady;

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
                        }
                    }
                }

                PredatorCognitionDomain.ScheduleFrameEvaluation(Time.frameCount);
                _foveatedSimulationManager.ScheduleFrameJobs();
                RunFixedStepAccumulator(CurrentFrameUnscaledDeltaTime, blockGameplayLanes);
                RunSlowTick(deltaTime, blockGameplayLanes);
                ScheduleDispatcherRaycasts();
            }
        }

        private static void TickPauseDepthOfField(float unscaledDeltaTime)
        {
            float targetWeight = _pauseDepthOfFieldTargetActive ? 1f : 0f;
            float step = PauseDepthOfFieldBlendSeconds > 0f
                ? Mathf.Max(0f, unscaledDeltaTime) / PauseDepthOfFieldBlendSeconds
                : 1f;
            _pauseDepthOfFieldWeight = Mathf.MoveTowards(_pauseDepthOfFieldWeight, targetWeight, step);

            CameraJuiceSystem cameraJuice = GlobalRegistry.CameraJuice;
            if (cameraJuice != null)
                cameraJuice.ApplyPauseDepthOfFieldWeight(_pauseDepthOfFieldWeight);
        }

        private void LateUpdate()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            RuntimeWatchdog.Signal(RuntimeWatchdog.RuntimeWatchdogLane.DispatcherLateFrame);
#endif
            long completeDispatcherTimestamp = 0L;
            bool dispatcherPhaseTimingStarted = false;
            try
            {
            DispatcherJobSwap.BeginLateFrameSwapWindow();
            try
            {
                CompleteDispatcherRaycasts();
                completeDispatcherTimestamp = BeginDispatcherPhaseTiming();
                dispatcherPhaseTimingStarted = true;
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
            FlushEnvironmentEventsArtery();
            FlushPlayerEventsArtery();
            FlushBaseEventsArtery();
            FlushAIEventsArtery();
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
                    NativeArenaAllocator.Reset();

                    if (dispatcherPhaseTimingStarted)
                        EndDispatcherPhaseTiming(completeDispatcherTimestamp, "FoveatedSimulationManager.CompleteFrameJobs");
                }
            }
        }

        private static void FlushCoreEventsArtery()
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
                Hecton8.Bootstrap.SceneBootstrap.PendingEventCount +
                ObjectPoolDiagnostics.PendingCount +
                PerformanceEvents.PendingCount +
                GlobalRegistry.PendingServiceReboundCount);

            Hecton8.Modding.ModRegistryEvents.FlushPending();
            BootstrapEvents.FlushPending();
            Hecton.Localization.LocalizationEvents.FlushPending();
            NarrativeEvents.FlushPending();
            SaveEvents.FlushPending();
            QuestEvents.FlushPending();
            Hecton8.Bootstrap.SceneBootstrap.FlushPendingEvents();
            ObjectPoolDiagnostics.FlushPending();
            PerformanceEvents.FlushPending();
            GlobalRegistry.FlushPendingServiceReboundEvents();
        }

        private static void FlushEnvironmentEventsArtery()
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
            CelestialEvents.FlushPending();
            EclipseGameplayEvents.FlushPending();
            AcousticZoneEvents.FlushPending();
            PhysicsEventBus.FlushPending();
            FluidFeedbackEvents.FlushPending();
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

        private static void FlushPlayerEventsArtery()
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
                PlayerSignalEvents.PendingCount +
                InventoryEvents.PendingCount +
                PlayerExpressionEvents.PendingCount +
                Hecton8.UI.BaseIntegrityEvents.PendingCount +
                Hecton8.UI.NotificationEvents.PendingCount +
                Hecton8.UI.PDAIntrusionEvents.PendingCount +
                pdaPendingBeforeFlush);

            Hecton8.Interaction.InteractionEvents.FlushPending();
            Hecton8.Crafting.CraftingEvents.FlushPending();
            ScanEvents.FlushPending();
            FirstHourEvents.FlushPending();
            EndingEvents.FlushPending();
            AudioLogEvents.FlushPending();
            HectonSubmarineOsEvents.FlushPending();
            FlashlightEvents.FlushPending();
            LaserCutterEvents.FlushPending();
            PlayerSignalEvents.FlushPending();
            InventoryEvents.FlushPending();
            PlayerExpressionEvents.FlushPending();
            Hecton8.UI.BaseIntegrityEvents.FlushPending();
            Hecton8.UI.NotificationEvents.FlushPending();
            Hecton8.UI.PDAIntrusionEvents.FlushPending();
            Hecton8.UI.PDAEvents.FlushPending(MaxPdaEventsPerFrame);
            TrackPdaBusCongestion(pdaPendingBeforeFlush, Hecton8.UI.PDAEvents.PendingCount);
        }

        private static void FlushBaseEventsArtery()
        {
            SetActiveLateFrameEventLane(_BaseEventsArteryHash);
            ReportLateFrameQueueDepth(
                _BaseEventsArteryHash,
                PowerGridTelemetryEvents.PendingCount +
                ModuleStatusEvents.PendingCount +
                BaseAirlockEvents.PendingCount +
                EmergencyServiceRelayEvents.PendingCount);

            PowerGridTelemetryEvents.FlushPending();
            ModuleStatusEvents.FlushPending();
            BaseAirlockEvents.FlushPending();
            EmergencyServiceRelayEvents.FlushPending();
        }

        private static void FlushAIEventsArtery()
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

        /// <summary>
        /// Consumes one deferred event dispatch slot from the current LateUpdate budget.
        /// </summary>
        /// <returns>True when an event queue may dispatch one payload this frame.</returns>
        public static bool TryConsumeLateFrameEventDispatch()
        {
            if (!_lateFrameEventBudgetActive)
                return true;

            if (IsLateFrameEventFlushTimeBudgetExhausted())
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

        public static void DispatchCriticalMemoryPressure(in CriticalMemoryPressureEvent memoryPressureEvent)
        {
            CrashTelemetryBuffer.ReportCriticalMemoryPressure(
                memoryPressureEvent.ReservedMemoryBytes,
                memoryPressureEvent.PhysicalMemoryBytes,
                memoryPressureEvent.UsageRatio);

            ObjectPoolManager objectPool = GlobalRegistry.ObjectPool;
            if (objectPool != null)
                objectPool.FlushInactivePoolsForMemoryPressure();

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
                Mathf.Max(pendingBeforeFlush, pendingAfterFlush),
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

        private static bool IsLateFrameEventFlushTimeBudgetExhausted()
        {
            if (_lateFrameEventBudgetStartTimestamp == 0L)
                return false;

            long elapsedTicks = System.Diagnostics.Stopwatch.GetTimestamp() - _lateFrameEventBudgetStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            return elapsedMilliseconds >= LateFrameEventFlushBudgetMilliseconds;
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

        private void RunFixedStepAccumulator(float unscaledDeltaTime, bool blockGameplayLanes)
        {
            if (unscaledDeltaTime <= 0f)
            {
                _temporalCompressionActive = false;
                return;
            }

            float maxAccumulatedTime = FixedStepSeconds * MaxFixedSubstepsPerFrame;
            float requestedAccumulatedTime = _fixedStepAccumulator + unscaledDeltaTime;
            bool compressionActive = requestedAccumulatedTime > maxAccumulatedTime;
            _temporalCompressionActive = compressionActive;
            if (compressionActive)
            {
                _temporalCompressionFrameCount++;
                CrashTelemetryBuffer.ReportTemporalCompression();
            }

            _fixedStepAccumulator = Mathf.Min(requestedAccumulatedTime, maxAccumulatedTime);

            int substepCount = 0;
            while (_fixedStepAccumulator >= FixedStepSeconds && substepCount < MaxFixedSubstepsPerFrame)
            {
                DispatchFixedStep(FixedStepSeconds, blockGameplayLanes);
                _fixedStepAccumulator -= FixedStepSeconds;
                substepCount++;
            }

            if (substepCount >= MaxFixedSubstepsPerFrame && _fixedStepAccumulator >= FixedStepSeconds)
                _fixedStepAccumulator = 0f;
        }

        private void DispatchFixedStep(float fixedDeltaTime, bool blockGameplayLanes)
        {
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

        private void RunSlowTick(float deltaTime, bool blockGameplayLanes)
        {
            if (deltaTime <= 0f)
                return;

            _slowTickAccumulator += deltaTime;
            if (_slowTickAccumulator < DefaultSlowTickIntervalSeconds)
                return;

            _slowTickAccumulator -= DefaultSlowTickIntervalSeconds;

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
                            rawArray[itemIndex].SlowTick();
                    }
                }

                WorldSpatialHashGrid.SlowTickMaintenance(DefaultSlowTickIntervalSeconds);
            }
        }

        private static void EnsureDispatcherRaycastBuffers()
        {
            if (!_pendingDispatcherRaycastCommands.IsCreated)
            {
                _pendingDispatcherRaycastCommands = new NativeQueue<RaycastCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RaycastCommand>[256] — dispatcher-owned global deferred physics request lane — owner: SystemDispatcher
                NativeMemorySentinel.RegisterNativeQueue(
                    _pendingDispatcherRaycastCommands,
                    MaxQueuedDispatcherRaycasts,
                    nameof(SystemDispatcher),
                    nameof(_pendingDispatcherRaycastCommands),
                    NativeAllocationLifetime.Session);
            }

            if (!_scheduledDispatcherRaycastCommands.IsCreated)
            {
                _scheduledDispatcherRaycastCommands = new NativeList<RaycastCommand>(MaxQueuedDispatcherRaycasts, Allocator.Persistent); // COLD ALLOC: NativeList<RaycastCommand>[256] — dispatcher-owned scheduled deferred raycast commands — owner: SystemDispatcher
                NativeMemorySentinel.RegisterNativeList(
                    _scheduledDispatcherRaycastCommands,
                    nameof(SystemDispatcher),
                    nameof(_scheduledDispatcherRaycastCommands),
                    NativeAllocationLifetime.Session);
            }

            if (!_scheduledDispatcherRaycastHits.IsCreated)
            {
                _scheduledDispatcherRaycastHits = new NativeArray<RaycastHit>(MaxQueuedDispatcherRaycasts, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[256] — dispatcher-owned deferred raycast hit lane — owner: SystemDispatcher
                NativeMemorySentinel.RegisterNativeArray(
                    _scheduledDispatcherRaycastHits,
                    nameof(SystemDispatcher),
                    nameof(_scheduledDispatcherRaycastHits),
                    NativeAllocationLifetime.Session);
            }
        }

        private static void ScheduleDispatcherRaycasts()
        {
            if (_dispatcherRaycastsScheduled || _pendingDispatcherRaycastCount <= 0)
                return;

            EnsureDispatcherRaycastBuffers();
            if (!_pendingDispatcherRaycastCommands.IsCreated || !_scheduledDispatcherRaycastCommands.IsCreated)
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
                    return;

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
                CompleteJobHandleWithWarning(ref _scheduledDispatcherRaycastHandle, "SystemDispatcher.DispatcherRaycasts");
                _scheduledDispatcherRaycastHandle = default;
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
            }
        }

        private static void DisposeDispatcherRaycastBuffers()
        {
            JobHandle scheduledRaycastDependency = default;
            if (_dispatcherRaycastsScheduled)
            {
                scheduledRaycastDependency = _scheduledDispatcherRaycastHandle;
                _scheduledDispatcherRaycastHandle = default;
                _dispatcherRaycastsScheduled = false;
            }

            if (_pendingDispatcherRaycastCommands.IsCreated)
            {
                NativeMemorySentinel.UnregisterNativeQueue(nameof(SystemDispatcher), nameof(_pendingDispatcherRaycastCommands));
                _pendingDispatcherRaycastCommands.Dispose();
                _pendingDispatcherRaycastCommands = default;
            }

            DisposeNativeList(ref _scheduledDispatcherRaycastCommands, scheduledRaycastDependency);
            DisposeNativeArray(ref _scheduledDispatcherRaycastHits, scheduledRaycastDependency);
            JobHandle.ScheduleBatchedJobs();

            _pendingDispatcherRaycastCount = 0;
            _scheduledDispatcherRaycastCount = 0;
            System.Array.Clear(_pendingDispatcherRaycastReceivers, 0, _pendingDispatcherRaycastReceivers.Length);
            System.Array.Clear(_pendingDispatcherRaycastRequestIds, 0, _pendingDispatcherRaycastRequestIds.Length);
            System.Array.Clear(_scheduledDispatcherRaycastReceivers, 0, _scheduledDispatcherRaycastReceivers.Length);
            System.Array.Clear(_scheduledDispatcherRaycastRequestIds, 0, _scheduledDispatcherRaycastRequestIds.Length);
        }

        private static void DisposeNativeList<T>(ref NativeList<T> list, JobHandle dependency) where T : unmanaged
        {
            if (!list.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeList(nameof(SystemDispatcher), nameof(_scheduledDispatcherRaycastCommands));
            list.Dispose(dependency);
            list = default;
        }

        private static void DisposeNativeArray<T>(ref NativeArray<T> array, JobHandle dependency) where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose(dependency);
            array = default;
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
                _foveatedSimulationManager.CompleteFrameJobs();
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
                _foveatedSimulationManager.CompleteFrameJobs();
            }
#endif
        }

        private static void CompleteJobHandleWithWarning(ref JobHandle handle, string systemName)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            long startTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
            double elapsedMilliseconds =
                (System.Diagnostics.Stopwatch.GetTimestamp() - startTimestamp) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            if (elapsedMilliseconds > SlowJobCompleteWarningMilliseconds)
            {
                float now = Time.unscaledTime;
                if (now >= _nextJobHandleWarningLogTime)
                {
                    _nextJobHandleWarningLogTime = now + DispatcherPhaseWarningLogIntervalSeconds;
                    GlobalTelemetryBus.PublishJobBarrierStall(
                        systemName,
                        "LateFrameComplete",
                        (float)elapsedMilliseconds);
                }
            }
#else
            DispatcherJobSwap.TryComplete(ref handle, forceComplete: true);
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
    public sealed class RenderDispatcher : MonoBehaviour
    {
        internal static RenderDispatcher ActiveRuntimeInstance { get; private set; }

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
                RenderSettings.ambientMode = AmbientMode;
                RenderSettings.ambientLight = AmbientLight;
                RenderSettings.ambientSkyColor = AmbientSkyColor;
                RenderSettings.ambientEquatorColor = AmbientEquatorColor;
                RenderSettings.ambientGroundColor = AmbientGroundColor;
                RenderSettings.ambientIntensity = AmbientIntensity;
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
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.RenderDispatcher, this))
                GlobalRegistry.UnregisterRenderDispatcher(this);

            _serviceRegistered = false;
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
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
                return;

            if (_pendingRenderSettingsCamera != null && camera != _pendingRenderSettingsCamera)
                return;

            RestorePendingRenderSettings();
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

            return Mathf.Min(requestedCount, sourceLength, destination.count);
        }
    }
}
