using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Rendering;
using Hecton8.AI;
using Hecton8.Bootstrap;
using Hecton8.World;

namespace Hecton8.Core
{
    /// <summary>
    /// Priority-lane dispatcher for registry-managed <see cref="IUpdatable"/> systems.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9950)]
    public sealed class SystemDispatcher : MonoBehaviour
    {
        private const int LaneCount = 4;
        private const float DefaultSlowTickIntervalSeconds = 0.5f;
        private const double SlowDispatcherPhaseWarningMilliseconds = 100.0;
        private const int MaxQueuedDispatcherRaycasts = 256;
        private const int DispatcherRaycastMinCommandsPerJob = 1;
        private static readonly ProfilerMarker _updateProfilerMarker = new ProfilerMarker("H8.Dispatcher.Update");
        private static readonly ProfilerMarker _fixedUpdateProfilerMarker = new ProfilerMarker("H8.Dispatcher.FixedUpdate");
        private static readonly ProfilerMarker _slowTickProfilerMarker = new ProfilerMarker("H8.Dispatcher.SlowTick");
        private static readonly ProfilerMarker _dispatcherRaycastScheduleProfilerMarker = new ProfilerMarker("H8.Dispatcher.Raycast.Schedule");
        private static readonly ProfilerMarker _dispatcherRaycastCompleteProfilerMarker = new ProfilerMarker("H8.Dispatcher.Raycast.Complete");
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

        private static IFoveatedDispatcher _foveatedSimulationManager = new FoveatedSimulationManager();
        // COLD ALLOC: IDispatcherRaycastReceiver[256] — dispatcher-owned pending raycast receivers — owner: SystemDispatcher
        private static readonly IDispatcherRaycastReceiver[] _pendingDispatcherRaycastReceivers = new IDispatcherRaycastReceiver[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: int[256] — dispatcher-owned pending raycast request ids — owner: SystemDispatcher
        private static readonly int[] _pendingDispatcherRaycastRequestIds = new int[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: IDispatcherRaycastReceiver[256] — dispatcher-owned scheduled raycast receivers — owner: SystemDispatcher
        private static readonly IDispatcherRaycastReceiver[] _scheduledDispatcherRaycastReceivers = new IDispatcherRaycastReceiver[MaxQueuedDispatcherRaycasts];
        // COLD ALLOC: int[256] — dispatcher-owned scheduled raycast request ids — owner: SystemDispatcher
        private static readonly int[] _scheduledDispatcherRaycastRequestIds = new int[MaxQueuedDispatcherRaycasts];
        private float _slowTickAccumulator;
        private static NativeQueue<RaycastCommand> _pendingDispatcherRaycastCommands;
        private static NativeList<RaycastCommand> _scheduledDispatcherRaycastCommands;
        private static NativeArray<RaycastHit> _scheduledDispatcherRaycastHits;
        private static JobHandle _scheduledDispatcherRaycastHandle;
        private static bool _dispatcherRaycastsScheduled;
        private static int _pendingDispatcherRaycastCount;
        private static int _scheduledDispatcherRaycastCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _foveatedSimulationManager.Dispose();
            _foveatedSimulationManager = new FoveatedSimulationManager();
            DisposeDispatcherRaycastBuffers();
            ClearAllLanes();
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
        /// Registers an update owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Register(IUpdatable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            if (item is IFoveatedSimulationTarget foveatedTarget)
                _foveatedSimulationManager.RegisterTarget(foveatedTarget);

            GetLane(layer).Register(item);
        }

        /// <summary>
        /// Registers a fixed-update owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Fixed-update owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Register(IFixedTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetFixedLane(layer).Register(item);
        }

        /// <summary>
        /// Registers a slow-tick owner into a fixed priority lane.
        /// </summary>
        /// <param name="item">Slow-tick owner.</param>
        /// <param name="layer">Priority lane.</param>
        internal static void Register(ISlowTickable item, PriorityLayer layer)
        {
            if (item == null)
                return;

            GetSlowLane(layer).Register(item);
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

            GetLane(layer).Unregister(item);
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

            GetFixedLane(layer).Unregister(item);
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

            GetSlowLane(layer).Unregister(item);
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
            }

            _foveatedSimulationManager.ResetRuntimeState();
        }

        private void Awake()
        {
            SystemDispatcher registeredDispatcher = GlobalRegistry.Dispatcher;
            if (registeredDispatcher != null && !ReferenceEquals(registeredDispatcher, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterSystemDispatcher(this);
            _slowTickAccumulator = 0f;

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(GlobalRegistry.Dispatcher, this))
            {
                DisposeDispatcherRaycastBuffers();
                GlobalRegistry.UnregisterSystemDispatcher(this);
            }
        }

        private void Update()
        {
            using (_updateProfilerMarker.Auto())
            {
#if UNITY_EDITOR
                NativeAllocationTrackerRuntimeBridge.NotifyDispatcherHeartbeat();
#endif
                if (BootstrapStatus.TryTriggerSafeHalt())
                    BootstrapBiosErrorOverlay.Show(BootstrapStatus.SafeHaltDisplayMessage);

                float deltaTime = Time.deltaTime;
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
                RunSlowTick(deltaTime, blockGameplayLanes);
                ScheduleDispatcherRaycasts();
            }
        }

        private void LateUpdate()
        {
            CompleteDispatcherRaycasts();
            long completeDispatcherTimestamp = BeginDispatcherPhaseTiming();
            _foveatedSimulationManager.CompleteFrameJobs();
            WorldSpatialHashGrid.LateFrameMaintenance(Time.frameCount);
            UnsafeArenaAllocator.ResetFrame();
            EndDispatcherPhaseTiming(completeDispatcherTimestamp, "FoveatedSimulationManager.CompleteFrameJobs");
        }

        private void FixedUpdate()
        {
            using (_fixedUpdateProfilerMarker.Auto())
            {
                float fixedDeltaTime = Time.fixedDeltaTime;
                bool blockGameplayLanes = Application.isPlaying &&
                                          BootstrapState.HasActiveInstance &&
                                          !BootstrapState.IsGameReady;

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
            }
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
            }
        }

        private static void EnsureDispatcherRaycastBuffers()
        {
            if (!_pendingDispatcherRaycastCommands.IsCreated)
            {
                _pendingDispatcherRaycastCommands = new NativeQueue<RaycastCommand>(Allocator.Persistent); // COLD ALLOC: NativeQueue<RaycastCommand>[256] — dispatcher-owned global deferred physics request lane — owner: SystemDispatcher
            }

            if (!_scheduledDispatcherRaycastCommands.IsCreated)
            {
                _scheduledDispatcherRaycastCommands = new NativeList<RaycastCommand>(MaxQueuedDispatcherRaycasts, Allocator.Persistent); // COLD ALLOC: NativeList<RaycastCommand>[256] — dispatcher-owned scheduled deferred raycast commands — owner: SystemDispatcher
            }

            if (!_scheduledDispatcherRaycastHits.IsCreated)
            {
                _scheduledDispatcherRaycastHits = new NativeArray<RaycastHit>(MaxQueuedDispatcherRaycasts, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<RaycastHit>[256] — dispatcher-owned deferred raycast hit lane — owner: SystemDispatcher
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
                int scheduledCount = 0;
                while (scheduledCount < _pendingDispatcherRaycastCount &&
                       _pendingDispatcherRaycastCommands.TryDequeue(out RaycastCommand command))
                {
                    _scheduledDispatcherRaycastCommands.Add(command);
                    _scheduledDispatcherRaycastReceivers[scheduledCount] = _pendingDispatcherRaycastReceivers[scheduledCount];
                    _scheduledDispatcherRaycastRequestIds[scheduledCount] = _pendingDispatcherRaycastRequestIds[scheduledCount];
                    _pendingDispatcherRaycastReceivers[scheduledCount] = null;
                    _pendingDispatcherRaycastRequestIds[scheduledCount] = 0;
                    scheduledCount++;
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
                _scheduledDispatcherRaycastHandle.Complete();
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
            if (_dispatcherRaycastsScheduled)
            {
                _scheduledDispatcherRaycastHandle.Complete();
                _scheduledDispatcherRaycastHandle = default;
                _dispatcherRaycastsScheduled = false;
            }

            if (_pendingDispatcherRaycastCommands.IsCreated)
            {
                _pendingDispatcherRaycastCommands.Dispose();
                _pendingDispatcherRaycastCommands = default;
            }

            if (_scheduledDispatcherRaycastCommands.IsCreated)
            {
                _scheduledDispatcherRaycastCommands.Dispose();
                _scheduledDispatcherRaycastCommands = default;
            }

            if (_scheduledDispatcherRaycastHits.IsCreated)
            {
                _scheduledDispatcherRaycastHits.Dispose();
                _scheduledDispatcherRaycastHits = default;
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
                Debug.LogWarning(
                    $"[SystemDispatcher] {phaseName} exceeded {SlowDispatcherPhaseWarningMilliseconds:F0}ms ({elapsedMilliseconds:F2}ms).");
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
    public sealed class RenderDispatcher : MonoBehaviour
    {
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
                    Skybox = RenderSettings.skybox,
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
                RenderSettings.skybox = Skybox;
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

        private void Awake()
        {
            RenderDispatcher registeredDispatcher = GlobalRegistry.RenderDispatcher;
            if (registeredDispatcher != null && !ReferenceEquals(registeredDispatcher, this))
            {
                Destroy(gameObject);
                return;
            }

            GlobalRegistry.RegisterRenderDispatcher(this);

            if (Application.isPlaying)
            {
                if (transform.parent != null)
                    transform.SetParent(null, true);

                DontDestroyOnLoad(gameObject);
            }
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
            GlobalRegistry.UnregisterRenderDispatcher(this);
        }

        private void HandleBeginCameraRendering(ScriptableRenderContext context, Camera camera)
        {
            RestorePendingRenderSettings();

            RegistryBucket<IRenderable> renderables = GlobalRegistry.Renderables;
            int count = renderables.Count;
            if (count <= 0)
                return;

            IRenderable[] rawArray = renderables.RawArray;
            float deltaTime = Time.deltaTime;
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
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<T>() * safeCount);
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
                UnsafeUtility.MemCpy(destinationPtr, sourcePtr, (long)UnsafeUtility.SizeOf<T>() * safeCount);
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
