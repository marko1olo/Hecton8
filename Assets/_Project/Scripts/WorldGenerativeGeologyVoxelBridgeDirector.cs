using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Dev;
using Hecton8.Gameplay;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldGenerativeGeologyVoxelRuntime : MonoBehaviour
    {
        private const int MaxActiveVoxelRuntimeRegistry = 64;

        // COLD ALLOC: RegistryBucket<WorldGenerativeGeologyVoxelRuntime>[64] - active geology voxel runtimes for validation without scene scans - owner: WorldGenerativeGeologyVoxelRuntime
        private static readonly RegistryBucket<WorldGenerativeGeologyVoxelRuntime> _activeVoxelRuntimes = new RegistryBucket<WorldGenerativeGeologyVoxelRuntime>(MaxActiveVoxelRuntimeRegistry);
        private static int _activeRuntimeCount;
        private static int _activeColliderCount;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _activeVoxelRuntimes.Clear();
            _activeRuntimeCount = 0;
            _activeColliderCount = 0;
        }

        [SerializeField] private long runtimeKey;
        [SerializeField] private int requestSignature;
        [SerializeField] private int resolvedResolution;
        [SerializeField] private int detailBand;
        [SerializeField] private string familyId = "world.family.generic";
        [SerializeField] private string geologyProfileId = "geology.generic";
        [SerializeField] private bool colliderEnabled;

        private bool _registeredInActiveSet;

        public long RuntimeKey => runtimeKey;
        public int RequestSignature => requestSignature;
        public int ResolvedResolution => resolvedResolution;
        public int DetailBand => detailBand;
        public string FamilyId => familyId;
        public string GeologyProfileId => geologyProfileId;
        public bool ColliderEnabled => colliderEnabled;
        public static int ActiveRuntimeCount => Mathf.Max(0, _activeRuntimeCount);
        public static int ActiveColliderCount => Mathf.Max(0, _activeColliderCount);

        /// <summary>
        /// Resolves an active voxel runtime by runtime key without a scene scan.
        /// </summary>
        /// <param name="targetRuntimeKey">Stable procedural runtime key.</param>
        /// <param name="runtime">Matching active voxel runtime, when present.</param>
        /// <returns>True when an active runtime was found.</returns>
        public static bool TryGetActiveRuntime(long targetRuntimeKey, out WorldGenerativeGeologyVoxelRuntime runtime)
        {
            runtime = null;
            if (targetRuntimeKey == 0L)
                return false;

            WorldGenerativeGeologyVoxelRuntime[] rawArray = _activeVoxelRuntimes.RawArray;
            int count = _activeVoxelRuntimes.Count;
            for (int i = 0; i < count; i++)
            {
                WorldGenerativeGeologyVoxelRuntime candidate = rawArray[i];
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.runtimeKey != targetRuntimeKey)
                    continue;

                runtime = candidate;
                return true;
            }

            return false;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (_registeredInActiveSet)
                return;

            _registeredInActiveSet = _activeVoxelRuntimes.TryRegister(this);
            if (!_registeredInActiveSet)
                return;

            _activeRuntimeCount++;
            if (colliderEnabled)
                _activeColliderCount++;
        }

        private void OnDisable()
        {
            if (!_registeredInActiveSet)
                return;

            _registeredInActiveSet = false;
            _activeVoxelRuntimes.Unregister(this);
            _activeRuntimeCount = Mathf.Max(0, _activeRuntimeCount - 1);
            if (colliderEnabled)
                _activeColliderCount = Mathf.Max(0, _activeColliderCount - 1);
        }

        public void Configure(
            long configuredRuntimeKey,
            int configuredSignature,
            int configuredResolution,
            int configuredDetailBand,
            string configuredFamilyId,
            string configuredProfileId,
            bool configuredColliderEnabled)
        {
            if (_registeredInActiveSet && colliderEnabled != configuredColliderEnabled)
            {
                _activeColliderCount += configuredColliderEnabled ? 1 : -1;
                if (_activeColliderCount < 0)
                    _activeColliderCount = 0;
            }

            runtimeKey = configuredRuntimeKey;
            requestSignature = configuredSignature;
            resolvedResolution = Mathf.Max(32, configuredResolution);
            detailBand = Mathf.Clamp(configuredDetailBand, 0, 2);
            familyId = string.IsNullOrWhiteSpace(configuredFamilyId) ? "world.family.generic" : configuredFamilyId;
            geologyProfileId = string.IsNullOrWhiteSpace(configuredProfileId) ? "geology.generic" : configuredProfileId;
            colliderEnabled = configuredColliderEnabled;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4028)]
    public sealed class WorldGenerativeGeologyVoxelBridgeDirector : MonoBehaviour, ISlowTickable, ITickable, IUpdatable, IRandomEventListener, IGlobalRegistryHotSwapListener
    {
        private const string NativeMemoryOwner = nameof(WorldGenerativeGeologyVoxelBridgeDirector);
        private const string EmptyNodesLabel = "emptyCaveNodes";
        private const string EmptyTunnelsLabel = "emptyCaveTunnels";
        private const string EntrancesLabel = "caveEntrances";
        private const string StructuresLabel = "caveStructures";
        private const int RuntimeKeySetCapacity = 64;
        private const NativeAllocationLifetime NativeMemoryLifetime = NativeAllocationLifetime.Scene;

        internal static WorldGenerativeGeologyVoxelBridgeDirector ActiveRuntimeInstance => GlobalRegistry.GeologyVoxelBridge;

        private static float RuntimeNowSeconds()
        {
            return Application.isPlaying ? (float)SystemDispatcher.CurrentUnscaledTimeSeconds : 0f;
        }

        private sealed class PendingRequestState : System.IDisposable
        {
            public int Signature;
            public CancellationTokenSource Cancellation;

            public void Dispose()
            {
                if (Cancellation == null)
                    return;

                Cancellation.Dispose();
                Cancellation = null;
            }
        }

        [Header("References")]
        [SerializeField] private WorldGenerativeGeologySeamExecutionDirector seamExecutionDirector;
        [SerializeField] private HectonVoxelEngine voxelEngine;

        [Header("Generation")]
        [SerializeField] private int maxRuntimeVolumes = 12;
        [SerializeField] private int maxSpawnOperationsPerTick = 2;
        [SerializeField] private int maxAsyncLaunchesPerFrame = 1;
        [SerializeField] private float missingRequestGraceSeconds = 6f;
        [SerializeField] private bool prewarmVoxelPool = true;
        [SerializeField] private int voxelPoolWarmPadding = 3;
        [SerializeField] private int maxVoxelPoolWarmupPerTick = 1;
        [SerializeField] private float minBlendWeight = 0.28f;
        [SerializeField] private float minVoxelSize = 18f;
        [SerializeField] private float maxVoxelSize = 72f;
        [SerializeField] private float voxelPadding = 8f;
        [SerializeField] private float minVoxelStep = 0.75f;
        [SerializeField] private float maxVoxelStep = 2f;
        [SerializeField] private float structureNoiseAmount = 0.12f;
        [SerializeField] private int nearFieldTargetResolution = 56;
        [SerializeField] private int farFieldTargetResolution = 38;
        [SerializeField] private float nearFieldDistance = 58f;
        [SerializeField] private float farFieldDistance = 168f;
        [SerializeField] private float maxRequestDistance = 96f;
        [SerializeField] private float requestRetentionDistancePadding = 14f;
        [SerializeField] private float colliderBuildDistance = 42f;
        [SerializeField] private float colliderBuildHysteresis = 8f;
        [SerializeField, Range(0.4f, 1f)] private float mediumDistanceResolutionScale = 0.82f;
        [SerializeField, Range(0.4f, 1f)] private float farDistanceResolutionScale = 0.68f;
        [SerializeField, Range(0.4f, 1f)] private float lowWeightResolutionScale = 0.85f;
        [SerializeField] private int maxRuntimeGridDimension = 56;
        [SerializeField] private float resolutionBandHysteresis = 12f;

        [Header("Seismic Trench")]
        [SerializeField] private float seismicTrenchLengthMin = 28f;
        [SerializeField] private float seismicTrenchLengthMax = 96f;
        [SerializeField] private float seismicTrenchDepthScale = 2.6f;
        [SerializeField] private float seismicTrenchDepthBias = 6f;
        [SerializeField] private float seismicTrenchSlope = 0.85f;
        [SerializeField] private float seismicTrenchSampleSpacing = 3.5f;
        [SerializeField] private OrganicDebrisProfile seismicRockDebrisProfile;
        [SerializeField, Range(0, 8)] private int seismicMaxDebrisBursts = 6;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugActiveVolumes;
        [SerializeField] private int _debugActiveColliders;
        [SerializeField] private int _debugPendingVolumes;
        [SerializeField] private int _debugQueuedLaunches;
        [SerializeField] private int _debugSpawnBudgetUsed;
        [SerializeField] private int _debugWarmedPoolTarget;
        [SerializeField] private string _debugTopVolume = string.Empty;

        private readonly Dictionary<long, GameObject> _activeVolumes = new Dictionary<long, GameObject>(32);
        private readonly Dictionary<long, WorldGenerativeGeologyVoxelRuntime> _activeRuntimes = new Dictionary<long, WorldGenerativeGeologyVoxelRuntime>(32);
        private readonly Dictionary<long, int> _activeSignatures = new Dictionary<long, int>(32);
        private readonly Dictionary<long, float> _lastSeenTimes = new Dictionary<long, float>(32);
        private readonly Dictionary<long, int> _desiredSignatures = new Dictionary<long, int>(32);
        private readonly Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest> _requestLookupByKey = new Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest>(64);
        private readonly Dictionary<long, PendingRequestState> _pendingRequests = new Dictionary<long, PendingRequestState>(32);
        private readonly Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest> _queuedLaunchRequests = new Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest>(32);
        private readonly Dictionary<long, float> _queuedLaunchTimes = new Dictionary<long, float>(32);
        private readonly HashSet<long> _pendingRuntimeKeys = new HashSet<long>(RuntimeKeySetCapacity);
        private readonly HashSet<long> _queuedLaunchKeys = new HashSet<long>(RuntimeKeySetCapacity);
        private readonly HashSet<long> _desiredRuntimeKeys = new HashSet<long>(RuntimeKeySetCapacity);
        private readonly List<long> _desiredRuntimeKeyOrder = new List<long>(32);
        private readonly List<long> _retainedDesiredRuntimeKeyOrder = new List<long>(32);
        private readonly List<long> _queuedLaunchOrder = new List<long>(32);
        private readonly List<long> _removalBuffer = new List<long>(32);
        private readonly List<long> _pendingCancellationBuffer = new List<long>(32);
        private readonly List<WorldGenerativeGeologyVoxelBlendRequest> _sortedRequests = new List<WorldGenerativeGeologyVoxelBlendRequest>(64);
        private bool _registeredToFrameTickManager;
        private bool _registeredToSlowTickManager;
        private bool _startupReconcilePending = true;
        private int _estimatedWarmedPoolCount;
        private int _nextQueueTelemetryFrame;
        private int _nextFaultTelemetryFrame;
        private CancellationTokenSource _lifetimeCancellation;
        private bool _randomEventHooksRegistered;
        private bool _runtimeRegistered;
        private bool _registeredHotSwap;
        private bool _runtimeDispatcherReady;

        private void OnEnable()
        {
            ResolveReferences();
            RefreshColdRegistryDependencies();
            TryRegisterRuntimeCallbacks();
        }

        private void Start()
        {
            ResolveReferences();
            RefreshColdRegistryDependencies();
            TryRegisterRuntimeCallbacks();
        }

        private void OnDisable()
        {
            UnregisterRandomEventHooks();
            if (_registeredToFrameTickManager)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToFrameTickManager = false;
            }

            if (_registeredToSlowTickManager)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredToSlowTickManager = false;
            }

            CancelLifetimeCancellation();
            CancelAllPendingRequests();
            ClearAllVolumes();

            TryUnregisterRuntimeService();
            TryUnregisterHotSwapListener();
        }

        private void OnDestroy()
        {
            TryUnregisterRuntimeService();
            TryUnregisterHotSwapListener();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher &&
                serviceSlot != GlobalRegistryServiceSlot.TickManager)
            {
                return;
            }

            if (serviceSlot == GlobalRegistryServiceSlot.Dispatcher)
                _runtimeDispatcherReady = currentService != null;
            else if (currentService != null)
                _runtimeDispatcherReady = true;

            _registeredToFrameTickManager = false;
            _registeredToSlowTickManager = false;
            TryRegisterRuntimeCallbacks();
        }

        private void RefreshColdRegistryDependencies()
        {
            _runtimeDispatcherReady = GlobalRegistry.Dispatcher != null;
            TryRegisterHotSwapListener();
        }

        private void TryRegisterHotSwapListener()
        {
            if (!Application.isPlaying || _registeredHotSwap)
                return;

            _registeredHotSwap = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwap)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwap = false;
        }

        private void TryRegisterRuntimeCallbacks()
        {
            if (!CanUseRuntimeDispatcher())
                return;

            TryRegisterRuntimeService();
            EnsureLifetimeCancellation();
            RegisterRandomEventHooks();
            QueueStartupReconcile();
            if (!_registeredToFrameTickManager)
                _registeredToFrameTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Environment);

            if (!_registeredToSlowTickManager)
                _registeredToSlowTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
        }

        public void Tick(float deltaTime)
        {
            if (!CanUseRuntimeDispatcher())
                return;

            TryRunStartupReconcile();
            FlushQueuedLaunches();
        }

        public void SlowTick()
        {
            if (!CanUseRuntimeDispatcher())
                return;

            if (TryRunStartupReconcile())
                return;

            ReconcileVoxelRequests();
        }

        public void SetSeamExecutionDirector(WorldGenerativeGeologySeamExecutionDirector director)
        {
            seamExecutionDirector = director;
        }

        public void SetVoxelEngine(HectonVoxelEngine engine)
        {
            voxelEngine = engine;
        }

        private void RegisterRandomEventHooks()
        {
            if (_randomEventHooksRegistered)
                return;

            RandomEventEvents.Register(this);
            _randomEventHooksRegistered = true;
        }

        private void UnregisterRandomEventHooks()
        {
            if (!_randomEventHooksRegistered)
                return;

            RandomEventEvents.Unregister(this);
            _randomEventHooksRegistered = false;
        }

        private void TryRegisterRuntimeService()
        {
            if (_runtimeRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterGeologyVoxelBridgeRuntime(this);
            _runtimeRegistered = ReferenceEquals(GlobalRegistry.GeologyVoxelBridge, this);
        }

        private void TryUnregisterRuntimeService()
        {
            if (!_runtimeRegistered && !ReferenceEquals(GlobalRegistry.GeologyVoxelBridge, this))
                return;

            GlobalRegistry.UnregisterGeologyVoxelBridgeRuntime(this);
            _runtimeRegistered = false;
        }

        void IRandomEventListener.OnRandomEventStarted(RandomEventType type, float intensity)
        {
        }

        void IRandomEventListener.OnRandomEventEnded(RandomEventType type)
        {
        }

        void IRandomEventListener.OnSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            HandleSeismicShockwave(in payload);
        }

        private void HandleSeismicShockwave(in SeismicShockwaveEvent payload)
        {
            _ = payload;
            _ = seismicTrenchLengthMin;
            _ = seismicTrenchLengthMax;
            _ = seismicTrenchDepthScale;
            _ = seismicTrenchDepthBias;
            _ = seismicTrenchSlope;
            _ = seismicTrenchSampleSpacing;
            _ = seismicRockDebrisProfile;
            _ = seismicMaxDebrisBursts;
            // SHINOBU_241: macroscopic trench CSG is offline-only. Runtime seismic events may shake or emit local debris,
            // but they must not register terrain trenches or stamp voxel trench lines during gameplay.
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in aup);
        }

        private static bool IsFinite(Vector3 value)
        {
            return math.isfinite(value.x) && math.isfinite(value.y) && math.isfinite(value.z);
        }

        private void QueueStartupReconcile()
        {
            _startupReconcilePending = true;
        }

        private bool TryRunStartupReconcile()
        {
            if (!_startupReconcilePending)
                return false;

            _startupReconcilePending = false;
            ReconcileVoxelRequests();
            return true;
        }

        public void ReconcileVoxelRequests()
        {
            long reconcileStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            ResolveReferences();
            _debugTopVolume = string.Empty;

            if (seamExecutionDirector == null || voxelEngine == null)
            {
                ClearAllVolumes();
                return;
            }

            RemoveStaleActiveVolumes();
            IReadOnlyList<WorldGenerativeGeologyVoxelBlendRequest> requests = seamExecutionDirector.ActiveVoxelRequests;
            float now = RuntimeNowSeconds();
            CaptureRetainedDesiredRuntimeKeyOrder();
            _sortedRequests.Clear();
            _desiredSignatures.Clear();
            _requestLookupByKey.Clear();
            _desiredRuntimeKeys.Clear();
            _desiredRuntimeKeyOrder.Clear();
            long requestFilterEndTimestamp = reconcileStartTimestamp;

            for (int i = 0; i < requests.Count; i++)
            {
                WorldGenerativeGeologyVoxelBlendRequest request = requests[i];
                if (request.weight < minBlendWeight)
                    continue;

                bool alreadyTracked =
                    _activeVolumes.ContainsKey(request.runtimeKey) ||
                    _pendingRuntimeKeys.Contains(request.runtimeKey);
                float distanceLimit = alreadyTracked
                    ? Mathf.Max(nearFieldDistance, maxRequestDistance) + Mathf.Max(0f, requestRetentionDistancePadding)
                    : Mathf.Max(nearFieldDistance, maxRequestDistance);
                if (request.playerDistance > distanceLimit)
                    continue;

                _sortedRequests.Add(request);
                _requestLookupByKey[request.runtimeKey] = request;
            }

            requestFilterEndTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            EnsureVoxelPoolWarm(_sortedRequests.Count);
            long poolWarmEndTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            _sortedRequests.Sort(CompareRequestsByPriority);
            int spawnBudgetUsed = 0;
            long sortEndTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

            int capacity = Mathf.Max(1, maxRuntimeVolumes);

            for (int i = 0; i < _retainedDesiredRuntimeKeyOrder.Count && _desiredRuntimeKeys.Count < capacity; i++)
            {
                long runtimeKey = _retainedDesiredRuntimeKeyOrder[i];
                if (!ShouldRetainActiveVolume(runtimeKey, now))
                    continue;

                AddDesiredRuntimeKey(runtimeKey);
            }

            Dictionary<long, GameObject>.Enumerator activeVolumeEnumerator = _activeVolumes.GetEnumerator();
            while (activeVolumeEnumerator.MoveNext())
            {
                long runtimeKey = activeVolumeEnumerator.Current.Key;
                if (_desiredRuntimeKeys.Count >= capacity)
                    break;

                if (_desiredRuntimeKeys.Contains(runtimeKey))
                    continue;

                if (!ShouldRetainActiveVolume(runtimeKey, now))
                    continue;

                AddDesiredRuntimeKey(runtimeKey);
            }

            HashSet<long>.Enumerator pendingRuntimeEnumerator = _pendingRuntimeKeys.GetEnumerator();
            while (pendingRuntimeEnumerator.MoveNext())
            {
                if (_desiredRuntimeKeys.Count >= capacity)
                    break;

                AddDesiredRuntimeKey(pendingRuntimeEnumerator.Current);
            }

            // Keep already-active volumes first to avoid constant top-N churn when
            // player distance and plan weights fluctuate slightly between slow ticks.
            for (int i = 0; i < _sortedRequests.Count && _desiredRuntimeKeys.Count < capacity; i++)
            {
                WorldGenerativeGeologyVoxelBlendRequest request = _sortedRequests[i];
                if (!_activeVolumes.ContainsKey(request.runtimeKey))
                    continue;

                AccumulateDesiredRequest(request, _desiredRuntimeKeys, ref spawnBudgetUsed);
            }

            for (int i = 0; i < _sortedRequests.Count && _desiredRuntimeKeys.Count < capacity; i++)
            {
                WorldGenerativeGeologyVoxelBlendRequest request = _sortedRequests[i];
                if (_desiredRuntimeKeys.Contains(request.runtimeKey))
                    continue;

                AccumulateDesiredRequest(request, _desiredRuntimeKeys, ref spawnBudgetUsed);
            }

            _removalBuffer.Clear();
            activeVolumeEnumerator = _activeVolumes.GetEnumerator();
            while (activeVolumeEnumerator.MoveNext())
            {
                long runtimeKey = activeVolumeEnumerator.Current.Key;
                if (_desiredRuntimeKeys.Contains(runtimeKey))
                    continue;

                if (_pendingRuntimeKeys.Contains(runtimeKey))
                    continue;

                if (_lastSeenTimes.TryGetValue(runtimeKey, out float lastSeenTime) &&
                    now - lastSeenTime < Mathf.Max(0.25f, missingRequestGraceSeconds))
                {
                    AddDesiredRuntimeKey(runtimeKey);
                    continue;
                }

                if (!_desiredRuntimeKeys.Contains(runtimeKey))
                    _removalBuffer.Add(runtimeKey);
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
                RemoveVolume(_removalBuffer[i]);

            CancelStalePendingRequests();
            long reconcileEndTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            TraceReconcile(
                requests.Count,
                _sortedRequests.Count,
                _desiredRuntimeKeys.Count,
                _activeVolumes.Count,
                _pendingRuntimeKeys.Count,
                _queuedLaunchOrder.Count,
                spawnBudgetUsed,
                GetElapsedMilliseconds(reconcileStartTimestamp, requestFilterEndTimestamp),
                GetElapsedMilliseconds(requestFilterEndTimestamp, poolWarmEndTimestamp),
                GetElapsedMilliseconds(poolWarmEndTimestamp, sortEndTimestamp),
                GetElapsedMilliseconds(sortEndTimestamp, reconcileEndTimestamp),
                GetElapsedMilliseconds(reconcileStartTimestamp, reconcileEndTimestamp));

            _debugActiveVolumes = _activeVolumes.Count;
            _debugActiveColliders = WorldGenerativeGeologyVoxelRuntime.ActiveColliderCount;
            _debugPendingVolumes = _pendingRuntimeKeys.Count;
            _debugQueuedLaunches = _queuedLaunchOrder.Count;
            _debugSpawnBudgetUsed = spawnBudgetUsed;
            _debugReady = _activeVolumes.Count > 0 || _pendingRuntimeKeys.Count > 0;
        }

        private bool ShouldRetainActiveVolume(long runtimeKey, float now)
        {
            if (!IsTrackedVolumeAlive(runtimeKey))
                return false;

            if (_pendingRuntimeKeys.Contains(runtimeKey))
                return true;

            if (_requestLookupByKey.ContainsKey(runtimeKey))
                return true;

            return _lastSeenTimes.TryGetValue(runtimeKey, out float lastSeenTime) &&
                   now - lastSeenTime < Mathf.Max(0.25f, missingRequestGraceSeconds);
        }

        private void AccumulateDesiredRequest(
            in WorldGenerativeGeologyVoxelBlendRequest request,
            HashSet<long> desiredKeys,
            ref int spawnBudgetUsed)
        {
            AddDesiredRuntimeKey(request.runtimeKey);
            _lastSeenTimes[request.runtimeKey] = RuntimeNowSeconds();
            int signature = ComputeRequestSignature(request);
            _desiredSignatures[request.runtimeKey] = signature;
            if (IsTrackedVolumeAlive(request.runtimeKey) &&
                _activeSignatures.TryGetValue(request.runtimeKey, out int existingSignature) &&
                existingSignature == signature)
            {
                if (string.IsNullOrEmpty(_debugTopVolume))
                    _debugTopVolume = request.familyId ?? string.Empty;
                return;
            }

            if (_pendingRequests.TryGetValue(request.runtimeKey, out PendingRequestState pendingState))
            {
                if (pendingState != null && pendingState.Signature == signature)
                    return;

                CancelPendingRequest(request.runtimeKey, true);
            }

            if (_pendingRuntimeKeys.Contains(request.runtimeKey))
                return;

            if (spawnBudgetUsed >= Mathf.Max(1, maxSpawnOperationsPerTick))
                return;

            PendingRequestState state = CreatePendingRequestState(signature);
            _pendingRuntimeKeys.Add(request.runtimeKey);
            _pendingRequests[request.runtimeKey] = state;
            TraceRequestScheduled(
                request.runtimeKey,
                request.familyId,
                request.geologyProfileId,
                request.weight,
                request.playerDistance,
                _activeVolumes.Count,
                _pendingRuntimeKeys.Count);
            QueueLaunchRequest(request);
            spawnBudgetUsed++;
        }

        private void QueueLaunchRequest(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            _queuedLaunchRequests[request.runtimeKey] = request;
            if (_queuedLaunchKeys.Add(request.runtimeKey))
            {
                _queuedLaunchOrder.Add(request.runtimeKey);
                _queuedLaunchTimes[request.runtimeKey] = RuntimeNowSeconds();
            }

            _debugQueuedLaunches = _queuedLaunchOrder.Count;
            WorldGenerativeGeologyTelemetry.TryPublishVoxelQueuePressureIfNeeded(
                _debugQueuedLaunches,
                ref _nextQueueTelemetryFrame);
        }

        private void FlushQueuedLaunches()
        {
            if (!isActiveAndEnabled || _queuedLaunchOrder.Count == 0)
                return;

            int launchBudget = Mathf.Max(1, maxAsyncLaunchesPerFrame);
            for (int i = 0; i < launchBudget && _queuedLaunchOrder.Count > 0; i++)
            {
                long runtimeKey = _queuedLaunchOrder[0];
                _queuedLaunchOrder.RemoveAt(0);
                _queuedLaunchKeys.Remove(runtimeKey);

                if (!_queuedLaunchRequests.TryGetValue(runtimeKey, out WorldGenerativeGeologyVoxelBlendRequest request) ||
                    !_pendingRequests.TryGetValue(runtimeKey, out PendingRequestState pendingState) ||
                    pendingState == null ||
                    pendingState.Cancellation == null ||
                    pendingState.Cancellation.IsCancellationRequested)
                {
                    _queuedLaunchRequests.Remove(runtimeKey);
                    _queuedLaunchTimes.Remove(runtimeKey);
                    continue;
                }

                float queuedAt = 0f;
                _queuedLaunchTimes.TryGetValue(runtimeKey, out queuedAt);
                _queuedLaunchRequests.Remove(runtimeKey);
                _queuedLaunchTimes.Remove(runtimeKey);

                float queuedMs = Application.isPlaying
                    ? Mathf.Max(0f, (RuntimeNowSeconds() - queuedAt) * 1000f)
                    : 0f;
                TraceLaunchStart(
                    runtimeKey,
                    request.familyId,
                    queuedMs,
                    _activeVolumes.Count,
                    _pendingRuntimeKeys.Count);
                _ = SpawnOrRefreshVolumeAsync(request, pendingState.Signature, pendingState);
            }

            _debugQueuedLaunches = _queuedLaunchOrder.Count;
        }

        private void CaptureRetainedDesiredRuntimeKeyOrder()
        {
            _retainedDesiredRuntimeKeyOrder.Clear();
            _retainedDesiredRuntimeKeyOrder.AddRange(_desiredRuntimeKeyOrder);
        }

        private void AddDesiredRuntimeKey(long runtimeKey)
        {
            if (!_desiredRuntimeKeys.Add(runtimeKey))
                return;

            _desiredRuntimeKeyOrder.Add(runtimeKey);
        }

        private async Awaitable SpawnOrRefreshVolumeAsync(
            WorldGenerativeGeologyVoxelBlendRequest request,
            int signature,
            PendingRequestState pendingState)
        {
            long requestStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            CancellationToken token = pendingState != null && pendingState.Cancellation != null
                ? pendingState.Cancellation.Token
                : EnsureLifetimeCancellation().Token;
            try
            {
                if (voxelEngine == null)
                {
                    WorldGenerativeGeologyTelemetry.TryPublishVoxelFaultIfNeeded(
                        WorldGenerativeGeologyTelemetry.VoxelEngineMissingWarningHash,
                        1f,
                        ref _nextFaultTelemetryFrame);
                    return;
                }

                long buildDataStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                BuildVoxelRequestData(
                    request,
                    out int gridDimension,
                    out float voxelStep,
                    out int voxelLodLevel,
                    out bool buildCollider,
                    out int resolvedResolution,
                    out int detailBand,
                    out CaveGenerationParams generationParams,
                    out NativeArray<CaveEntrance> entranceArray,
                    out NativeArray<CaveStructure> structureArray);
                float buildDataMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - buildDataStartTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
                WorldGenerativeGeologyTelemetry.TryPublishVoxelBuildDataBudgetIfNeeded(buildDataMs);
                NativeArray<CaveNode> nodes = AllocateTrackedNativeArray<CaveNode>(0, EmptyNodesLabel, NativeArrayOptions.UninitializedMemory);
                NativeArray<CaveTunnel> tunnels = AllocateTrackedNativeArray<CaveTunnel>(0, EmptyTunnelsLabel, NativeArrayOptions.UninitializedMemory);
                NativeArray<CaveEntrance> entrances = entranceArray;
                TraceRequestPrepared(
                    request.runtimeKey,
                    request.familyId,
                    request.geologyProfileId,
                    gridDimension,
                    voxelStep,
                    voxelLodLevel,
                    buildCollider,
                    buildDataMs);

                try
                {
                    // Entrances are forwarded into HectonVoxelEngine, which owns terrain-hole registration
                    // through RegisterEntranceTerrainHoles -> RegisterTerrainHoleHandle.
                    GameObject volume = request.hasAbsoluteUniverseCenterAup
                        ? await voxelEngine.GenerateVolumeFromDataAsync(
                            request.absoluteUniverseCenterAup,
                            gridDimension,
                            voxelStep,
                            nodes,
                            tunnels,
                            entrances,
                            structureArray,
                            generationParams,
                            voxelLodLevel,
                            buildCollider,
                            token)
                        : await voxelEngine.GenerateVolumeFromDataAsync(
                            request.RuntimeCenter,
                            gridDimension,
                            voxelStep,
                            nodes,
                            tunnels,
                            entrances,
                            structureArray,
                            generationParams,
                            lodLevel: voxelLodLevel,
                            buildCollider: buildCollider,
                            ct: token);

                    if (volume == null)
                    {
                        WorldGenerativeGeologyTelemetry.TryPublishVoxelFaultIfNeeded(
                            WorldGenerativeGeologyTelemetry.VoxelVolumeNullWarningHash,
                            gridDimension,
                            ref _nextFaultTelemetryFrame);
                        return;
                    }

                    if (!isActiveAndEnabled ||
                        !_desiredSignatures.TryGetValue(request.runtimeKey, out int desiredSignature) ||
                        desiredSignature != signature)
                    {
                        voxelEngine.DespawnVolume(volume);
                        return;
                    }

                    if (!volume.TryGetComponent(out WorldGenerativeGeologyVoxelRuntime runtime))
                        runtime = volume.AddComponent<WorldGenerativeGeologyVoxelRuntime>();
                    runtime.Configure(
                        request.runtimeKey,
                        signature,
                        resolvedResolution,
                        detailBand,
                        request.familyId,
                        request.geologyProfileId,
                        buildCollider);
                    RegisterHydrothermalVent(request);

                    if (_activeVolumes.TryGetValue(request.runtimeKey, out GameObject previousVolume) &&
                        previousVolume != null &&
                        !ReferenceEquals(previousVolume, volume))
                    {
                        voxelEngine.DespawnVolume(previousVolume);
                    }

                    _activeVolumes[request.runtimeKey] = volume;
                    _activeRuntimes[request.runtimeKey] = runtime;
                    _activeSignatures[request.runtimeKey] = signature;
                    _lastSeenTimes[request.runtimeKey] = RuntimeNowSeconds();
                    if (string.IsNullOrEmpty(_debugTopVolume))
                        _debugTopVolume = request.familyId ?? string.Empty;

                    float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - requestStartTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
                    TraceRequestComplete(
                        request.runtimeKey,
                        request.familyId,
                        request.geologyProfileId,
                        gridDimension,
                        voxelStep,
                        voxelLodLevel,
                        buildCollider,
                        elapsedMs,
                        _activeVolumes.Count,
                        _pendingRuntimeKeys.Count);
                }
                finally
                {
                    DisposeTrackedNativeArray(ref nodes);
                    DisposeTrackedNativeArray(ref tunnels);
                    DisposeTrackedNativeArray(ref entrances);
                    DisposeTrackedNativeArray(ref structureArray);
                }
            }
            catch (OperationCanceledException)
            {
                float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - requestStartTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
                TraceRequestCanceled(
                    request.runtimeKey,
                    request.familyId,
                    request.geologyProfileId,
                    elapsedMs);
            }
            catch (Exception ex)
            {
                float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - requestStartTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
                TraceRequestFault(
                    request.runtimeKey,
                    request.familyId,
                    request.geologyProfileId,
                    elapsedMs,
                    ex);
            }
            finally
            {
                if (_pendingRequests.TryGetValue(request.runtimeKey, out PendingRequestState currentState) &&
                    ReferenceEquals(currentState, pendingState))
                {
                    _pendingRequests.Remove(request.runtimeKey);
                    _pendingRuntimeKeys.Remove(request.runtimeKey);
                }

                _queuedLaunchRequests.Remove(request.runtimeKey);
                _queuedLaunchTimes.Remove(request.runtimeKey);
                _queuedLaunchKeys.Remove(request.runtimeKey);
                pendingState?.Dispose();

                _debugActiveVolumes = _activeVolumes.Count;
                _debugActiveColliders = WorldGenerativeGeologyVoxelRuntime.ActiveColliderCount;
                _debugPendingVolumes = _pendingRuntimeKeys.Count;
                _debugQueuedLaunches = _queuedLaunchOrder.Count;
                _debugReady = _activeVolumes.Count > 0 || _pendingRuntimeKeys.Count > 0;
            }
        }

        private PendingRequestState CreatePendingRequestState(int signature)
        {
            CancellationTokenSource lifetime = EnsureLifetimeCancellation();
            CancellationTokenSource linked = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
            return new PendingRequestState
            {
                Signature = signature,
                Cancellation = linked
            };
        }

        private void BuildVoxelRequestData(
            in WorldGenerativeGeologyVoxelBlendRequest request,
            out int gridDimension,
            out float voxelStep,
            out int voxelLodLevel,
            out bool buildCollider,
            out int resolvedResolution,
            out int detailBand,
            out CaveGenerationParams generationParams,
            out NativeArray<CaveEntrance> entrances,
            out NativeArray<CaveStructure> structures)
        {
            float dominantSize = Mathf.Clamp(math.cmax((float3)request.size), minVoxelSize, maxVoxelSize) + voxelPadding;
            ResolveRequestBuildSettings(request, out resolvedResolution, out voxelLodLevel, out buildCollider, out detailBand);
            float stabilizedWeight = Quantize01(request.weight, 0.05f);
            voxelStep = Mathf.Clamp(dominantSize / Mathf.Max(24f, resolvedResolution), minVoxelStep, maxVoxelStep);
            gridDimension = Mathf.Clamp(
                Mathf.CeilToInt(dominantSize / voxelStep),
                32,
                Mathf.Clamp(maxRuntimeGridDimension, 32, 96));

            uint seed = unchecked((uint)(request.runtimeKey * 92821L + 15731L));
            CavePreset preset = voxelEngine != null && voxelEngine.defaultPreset != null
                ? voxelEngine.defaultPreset
                : CavePresetLibrary.Create(CavePresetType.Grotto);
            generationParams = preset.ToGenerationParams(seed);
            generationParams.structureOnlyMode = 1;
            generationParams.structureBlendK = Mathf.Clamp(stabilizedWeight * 10f, 3.5f, 12f);
            generationParams.shellThickness = Mathf.Clamp(request.size.y * 0.16f, 2f, 10f);
            generationParams.wallNoiseAmplitude = Mathf.Max(generationParams.wallNoiseAmplitude, structureNoiseAmount * stabilizedWeight * 4f);
            generationParams.spawnContext = SpawnContext.CaveShallow;

            Vector3 upOffset = Vector3.up * Mathf.Max(0.75f, request.size.y * 0.08f);
            entrances = BuildEntrances(request);
            structures = AllocateTrackedNativeArray<CaveStructure>(
                ResolveStructureCount(request.archetype),
                StructuresLabel,
                NativeArrayOptions.UninitializedMemory);

            switch (request.archetype)
            {
                case WorldGenerativeGeologyProfile.ShapeArchetype.Arch:
                    BuildArchStructures(request, upOffset, structures, false);
                    break;

                case WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge:
                    BuildArchStructures(request, upOffset, structures, true);
                    break;

                case WorldGenerativeGeologyProfile.ShapeArchetype.Canopy:
                    BuildCanopyStructures(request, upOffset, structures);
                    break;

                default:
                    BuildRockStructures(request, structures);
                    break;
            }
        }

        private static int ResolveStructureCount(WorldGenerativeGeologyProfile.ShapeArchetype archetype)
        {
            return archetype switch
            {
                WorldGenerativeGeologyProfile.ShapeArchetype.Arch => 7,
                WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge => 7,
                WorldGenerativeGeologyProfile.ShapeArchetype.Canopy => 4,
                _ => 5
            };
        }

        private void BuildArchStructures(
            in WorldGenerativeGeologyVoxelBlendRequest request,
            Vector3 upOffset,
            NativeArray<CaveStructure> structures,
            bool bridgeBias)
        {
            float span = Mathf.Max(request.size.x, request.size.z) * (bridgeBias ? 0.68f : 0.62f);
            float radius = Mathf.Max(1.8f, span * 0.14f);
            float height = Mathf.Max(6f, request.size.y * (bridgeBias ? 0.66f : 0.58f));
            Vector3 lateral = RotateOffset(request.rotation, Vector3.right);
            Vector3 forward = RotateOffset(request.rotation, Vector3.forward);
            Vector3 center = request.RuntimeCenter;
            Vector3 left = center + upOffset - lateral * (span * 0.36f);
            Vector3 right = center + upOffset + lateral * (span * 0.36f);
            Vector3 crown = center + upOffset + Vector3.up * (height * 0.7f);
            Vector3 archStart = crown - lateral * (span * 0.28f);
            Vector3 archEnd = crown + lateral * (span * 0.28f);
            Vector3 bridgeStart = crown - forward * (request.size.z * 0.18f);
            Vector3 bridgeEnd = crown + forward * (request.size.z * 0.18f);
            float buttressBias = HashSigned(request.runtimeKey, 17) * request.size.z * 0.09f;

            structures[0] = CreateColumn(left, radius * 0.9f, height);
            structures[1] = CreateColumn(right, radius * 0.84f, height * 0.96f);
            structures[2] = CreateArch(archStart, archEnd, radius, request.weight);
            structures[3] = CreateBridge(
                bridgeStart + lateral * buttressBias,
                bridgeEnd - lateral * buttressBias,
                radius * 0.55f,
                request.weight * 0.78f);
            structures[4] = CreateBoulder(left + forward * (request.size.z * 0.12f), radius * 0.8f, request.weight * 0.72f);
            structures[5] = CreateBoulder(right - forward * (request.size.z * 0.1f), radius * 0.7f, request.weight * 0.68f);
            structures[6] = CreateBlock(
                center + upOffset + Vector3.up * (height * 0.22f),
                new Vector3(span * 0.1f, height * 0.1f, request.size.z * 0.14f),
                request.weight * 0.55f);
        }

        private void BuildCanopyStructures(
            in WorldGenerativeGeologyVoxelBlendRequest request,
            Vector3 upOffset,
            NativeArray<CaveStructure> structures)
        {
            float height = Mathf.Max(4f, request.size.y * 0.44f);
            Vector3 forward = RotateOffset(request.rotation, Vector3.forward);
            Vector3 lateral = RotateOffset(request.rotation, Vector3.right);
            Vector3 root = request.RuntimeCenter + upOffset;
            Vector3 canopyA = root + Vector3.up * (height * 0.62f) - forward * (request.size.z * 0.12f);
            Vector3 canopyB = root + Vector3.up * (height * 0.62f) + forward * (request.size.z * 0.18f);
            structures[0] = CreateColumn(root - lateral * (request.size.x * 0.06f), Mathf.Max(1.6f, request.size.x * 0.1f), height);
            structures[1] = CreateWall(
                root + Vector3.up * (height * 0.62f),
                new Vector3(request.size.x * 0.28f, height * 0.18f, request.size.z * 0.18f),
                request.weight);
            structures[2] = CreateBridge(canopyA, canopyB, Mathf.Max(1f, request.size.y * 0.08f), request.weight * 0.72f);
            structures[3] = CreateBoulder(
                root + lateral * (request.size.x * 0.18f) + Vector3.up * (height * 0.24f),
                Mathf.Max(1.6f, request.size.x * 0.08f),
                request.weight * 0.64f);
        }

        private void BuildRockStructures(in WorldGenerativeGeologyVoxelBlendRequest request, NativeArray<CaveStructure> structures)
        {
            Vector3 forward = RotateOffset(request.rotation, Vector3.forward);
            Vector3 lateral = RotateOffset(request.rotation, Vector3.right);
            Vector3 center = request.RuntimeCenter;
            structures[0] = CreateBlock(center, request.size * 0.22f, request.weight);
            structures[1] = CreateBlock(
                center + lateral * (request.size.x * 0.12f) + Vector3.up * (request.size.y * 0.1f) - forward * (request.size.z * 0.08f),
                request.size * 0.16f,
                request.weight * 0.8f);
            structures[2] = CreateBlock(
                center - lateral * (request.size.x * 0.08f) + Vector3.up * (request.size.y * 0.18f) + forward * (request.size.z * 0.06f),
                request.size * 0.12f,
                request.weight * 0.62f);
            structures[3] = CreateBoulder(
                center - lateral * (request.size.x * 0.1f) + Vector3.up * (request.size.y * 0.06f) + forward * (request.size.z * 0.1f),
                Mathf.Max(2f, request.size.x * 0.12f),
                request.weight * 0.9f);
            structures[4] = CreateBoulder(
                center + lateral * (request.size.x * 0.16f) + Vector3.up * (request.size.y * 0.03f) - forward * (request.size.z * 0.12f),
                Mathf.Max(1.6f, request.size.x * 0.08f),
                request.weight * 0.7f);
        }

        private CaveStructure CreateColumn(Vector3 position, float radius, float height)
        {
            return new CaveStructure
            {
                position = position,
                size = new float3(radius, height, 0f),
                pointB = position,
                blendRadius = Mathf.Clamp(radius * 1.4f, 2f, 10f),
                noiseAmount = structureNoiseAmount,
                structureType = CaveStructureType.Column
            };
        }

        private CaveStructure CreateArch(Vector3 pointA, Vector3 pointB, float radius, float weight)
        {
            return new CaveStructure
            {
                position = pointA,
                pointB = pointB,
                size = new float3(radius, radius * 0.8f, radius * 0.8f),
                blendRadius = Mathf.Clamp(radius * 1.8f, 3f, 12f),
                noiseAmount = structureNoiseAmount * Mathf.Clamp(weight, 0.5f, 1.2f),
                structureType = CaveStructureType.Arch
            };
        }

        private CaveStructure CreateBridge(Vector3 pointA, Vector3 pointB, float radius, float weight)
        {
            return new CaveStructure
            {
                position = pointA,
                pointB = pointB,
                size = new float3(radius, radius, radius),
                blendRadius = Mathf.Clamp(radius * 1.65f, 2f, 10f),
                noiseAmount = structureNoiseAmount * Mathf.Clamp(weight, 0.45f, 1.15f),
                structureType = CaveStructureType.Bridge
            };
        }

        private CaveStructure CreateBlock(Vector3 position, Vector3 halfExtents, float weight)
        {
            return new CaveStructure
            {
                position = position,
                pointB = position,
                size = halfExtents,
                blendRadius = Mathf.Clamp(math.cmax((float3)halfExtents) * 1.2f, 2f, 10f),
                noiseAmount = structureNoiseAmount * Mathf.Clamp(weight, 0.5f, 1.3f),
                structureType = CaveStructureType.Block
            };
        }

        private CaveStructure CreateWall(Vector3 position, Vector3 halfExtents, float weight)
        {
            return new CaveStructure
            {
                position = position,
                pointB = position,
                size = halfExtents,
                blendRadius = Mathf.Clamp(math.cmax((float3)halfExtents) * 1.15f, 2f, 10f),
                noiseAmount = structureNoiseAmount * Mathf.Clamp(weight, 0.5f, 1.1f),
                structureType = CaveStructureType.Wall
            };
        }

        private CaveStructure CreateBoulder(Vector3 position, float radius, float weight)
        {
            return new CaveStructure
            {
                position = position,
                pointB = position,
                size = new float3(radius, 0f, 0f),
                blendRadius = Mathf.Clamp(radius * 1.5f, 2f, 10f),
                noiseAmount = structureNoiseAmount * Mathf.Clamp(weight, 0.6f, 1.2f),
                structureType = CaveStructureType.Boulder
            };
        }

        private int ComputeRequestSignature(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            ResolveRequestBuildSettings(request, out int resolvedResolution, out int voxelLodLevel, out bool buildCollider, out _);
            unchecked
            {
                int hash = (int)request.runtimeKey;
                hash = (hash * 397) ^ (int)request.caveBlendMode;
                hash = (hash * 397) ^ (int)request.archetype;
                hash = (hash * 397) ^ resolvedResolution;
                hash = (hash * 397) ^ voxelLodLevel;
                hash = (hash * 397) ^ (buildCollider ? 1 : 0);
                hash = (hash * 397) ^ (request.hasTerrainSample ? 1 : 0);
                hash = (hash * 397) ^ Mathf.RoundToInt(request.slopeDegrees * 10f);
                hash = (hash * 397) ^ Mathf.RoundToInt(request.seamBlendRadius * 100f);
                hash = (hash * 397) ^ Mathf.RoundToInt(request.suggestedTerrainCut * 100f);
                return hash;
            }
        }

        private NativeArray<CaveEntrance> BuildEntrances(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            if (!VoxelSeamDirector.ShouldCreateCaveMouth(request.hasTerrainSample, request.slopeDegrees, request.caveBlendMode))
                return AllocateTrackedNativeArray<CaveEntrance>(0, EntrancesLabel, NativeArrayOptions.UninitializedMemory);

            NativeArray<CaveEntrance> entrances = AllocateTrackedNativeArray<CaveEntrance>(1, EntrancesLabel, NativeArrayOptions.UninitializedMemory);
            Vector3 terrainNormal = ResolveCaveEntranceTerrainNormal(request);
            entrances[0] = VoxelSeamDirector.BuildCaveEntrance(
                request.RuntimeTerrainContactPosition,
                request.RuntimeCenter,
                request.size,
                request.weight,
                request.seamBlendRadius,
                request.suggestedTerrainCut,
                terrainNormal,
                request.absoluteTerrainContactPosition);
            return entrances;
        }

        private static NativeArray<T> AllocateTrackedNativeArray<T>(
            int length,
            string label,
            NativeArrayOptions allocationNativeArrayOptions)
            where T : struct
        {
            NativeArray<T> array = new NativeArray<T>(length, Allocator.Persistent, allocationNativeArrayOptions);
            RegisterTrackedNativeArray(array, label);
            return array;
        }

        private static void RegisterTrackedNativeArray<T>(NativeArray<T> array, string label)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.RegisterNativeArray(array, NativeMemoryOwner, label, NativeMemoryLifetime);
        }

        private static void DisposeTrackedNativeArray<T>(ref NativeArray<T> array)
            where T : struct
        {
            if (!array.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(array);
            array.Dispose();
            array = default;
        }

        private static Vector3 ResolveCaveEntranceTerrainNormal(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            if (!request.hasTerrainSample)
                return default;

            return VoxelSeamDirector.ResolveTerrainNormalAtSeam(
                request.absoluteTerrainContactPosition,
                request.seamBlendRadius);
        }

        private void RegisterHydrothermalVent(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            if (!ShouldRegisterHydrothermalVent(request))
                return;

            AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;
            PersistentWorldRegistry persistentRegistry = GlobalRegistry.PersistentWorldRegistry;
            if (thermalManager == null && persistentRegistry == null)
                return;

            Vector3 ventPosition = request.hasTerrainSample
                ? request.RuntimeTerrainContactPosition
                : request.RuntimeCenter;
            ventPosition.y -= Mathf.Min(2.5f, request.size.y * 0.08f);

            float radius = Mathf.Clamp(Mathf.Min(request.size.x, request.size.z) * 0.12f, 3f, 10f);
            float height = Mathf.Clamp(request.size.y * 0.62f, 8f, 26f);
            float updraft = Mathf.Lerp(8f, 18f, Mathf.Clamp01(request.weight));
            float heat = Mathf.Lerp(12f, 24f, Mathf.Clamp01(request.weight));
            float smokeDensity = Mathf.Lerp(0.75f, 1.35f, Mathf.Clamp01(request.planWeight));
            float cableRadius = Mathf.Max(radius * 1.8f, request.size.x * 0.22f);

            persistentRegistry?.RegisterActiveThermalVent(
                request.runtimeKey,
                ventPosition,
                radius,
                height,
                updraft,
                heat,
                smokeDensity,
                cableRadius);

            thermalManager?.RegisterRuntimeVent(
                request.runtimeKey,
                ventPosition,
                radius,
                height,
                updraft,
                heat,
                smokeDensity,
                cableRadius);

            ResourceDistributionDirector resourceDirector = ResourceDistributionDirector.ActiveRuntimeInstance;
            if (resourceDirector != null)
            {
                if (!TryResolveAupFromRuntimeOrigin(ventPosition, out AbsoluteUniversePosition ventAup))
                    return;

                resourceDirector.TrySpawnDeepMantleGeodeAtAup(
                    ventAup,
                    radius,
                    unchecked((uint)request.runtimeKey));
            }
        }

        private static bool ShouldRegisterHydrothermalVent(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            if (!request.hasTerrainSample)
                return false;

            if (!AbyssalThermalManager.IsThermalBiomeFamilyId(request.familyId))
                return false;

            return request.slopeDegrees <= 18f &&
                   request.weight >= 0.42f &&
                   Mathf.Max(request.size.x, request.size.z) >= 18f;
        }

        private void ResolveRequestBuildSettings(
            in WorldGenerativeGeologyVoxelBlendRequest request,
            out int resolvedResolution,
            out int voxelLodLevel,
            out bool buildCollider,
            out int detailBand)
        {
            int previousBand = -1;
            int previousResolution = 0;
            bool currentColliderEnabled = false;

            if (_activeRuntimes.TryGetValue(request.runtimeKey, out WorldGenerativeGeologyVoxelRuntime runtime) &&
                runtime != null)
            {
                previousBand = runtime.DetailBand;
                previousResolution = runtime.ResolvedResolution;
                currentColliderEnabled = runtime.ColliderEnabled;
            }

            detailBand = ResolveDetailBand(request.playerDistance, previousBand);
            resolvedResolution = ResolveTargetResolution(request, detailBand, previousResolution);
            voxelLodLevel = ResolveVoxelLodLevel(detailBand);
            buildCollider = ShouldBuildCollider(request.playerDistance, currentColliderEnabled);
        }

        private static int ResolveVoxelLodLevel(int detailBand)
        {
            return detailBand switch
            {
                2 => 2,
                1 => 1,
                _ => 0
            };
        }

        private int ResolveTargetResolution(
            in WorldGenerativeGeologyVoxelBlendRequest request,
            int detailBand,
            int previousResolution)
        {
            float dominantSize = Mathf.Max(request.size.x, request.size.y, request.size.z);
            int baseResolution = request.archetype switch
            {
                WorldGenerativeGeologyProfile.ShapeArchetype.Arch => 48,
                WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge => 50,
                WorldGenerativeGeologyProfile.ShapeArchetype.Canopy => 40,
                _ => 42
            };

            if (dominantSize >= 30f)
                baseResolution += 6;
            else if (dominantSize >= 24f)
                baseResolution += 3;

            int nearResolution = Mathf.Max(36, nearFieldTargetResolution);
            int farResolution = Mathf.Clamp(farFieldTargetResolution, 32, nearResolution);
            int mediumResolution = Mathf.RoundToInt((nearResolution + farResolution) * 0.5f);
            int distanceCap = detailBand switch
            {
                0 => nearResolution,
                2 => farResolution,
                _ => mediumResolution
            };
            float resolutionScale = 1f;
            if (detailBand == 2)
                resolutionScale *= farDistanceResolutionScale;
            else if (detailBand == 1)
                resolutionScale *= mediumDistanceResolutionScale;

            float priority = Mathf.Clamp01(request.planWeight * 0.65f + request.weight * 0.35f);
            if (priority < 0.45f)
                resolutionScale *= lowWeightResolutionScale;

            int scaledResolution = Mathf.RoundToInt(baseResolution * resolutionScale);
            int resolvedResolution = Mathf.Clamp(Mathf.Min(distanceCap, scaledResolution), 32, nearResolution);
            if (previousResolution > 0 && Mathf.Abs(previousResolution - resolvedResolution) <= 4)
                return previousResolution;

            return resolvedResolution;
        }

        private int ResolveDetailBand(float playerDistance, int previousBand)
        {
            float nearThreshold = Mathf.Max(0f, nearFieldDistance);
            float farThreshold = Mathf.Max(nearThreshold + 1f, farFieldDistance);
            float hysteresis = Mathf.Max(0f, resolutionBandHysteresis);
            float distance = Mathf.Max(0f, playerDistance);

            switch (previousBand)
            {
                case 0:
                    if (distance <= nearThreshold + hysteresis)
                        return 0;

                    return distance >= farThreshold + hysteresis ? 2 : 1;

                case 1:
                    if (distance <= Mathf.Max(0f, nearThreshold - hysteresis))
                        return 0;

                    if (distance >= farThreshold + hysteresis)
                        return 2;

                    return 1;

                case 2:
                    if (distance >= Mathf.Max(0f, farThreshold - hysteresis))
                        return 2;

                    return distance <= Mathf.Max(0f, nearThreshold - hysteresis) ? 0 : 1;
            }

            if (distance <= nearThreshold)
                return 0;

            return distance >= farThreshold ? 2 : 1;
        }

        private bool ShouldBuildCollider(float playerDistance, bool currentColliderEnabled)
        {
            float threshold = Mathf.Max(8f, colliderBuildDistance);
            float hysteresis = Mathf.Clamp(colliderBuildHysteresis, 0f, threshold * 0.5f);
            float distance = Mathf.Max(0f, playerDistance);
            if (currentColliderEnabled)
                return distance <= threshold + hysteresis;

            return distance <= Mathf.Max(0f, threshold - hysteresis);
        }

        private static float Quantize01(float value, float step)
        {
            float safeStep = Mathf.Max(0.01f, step);
            return Mathf.Clamp01(Mathf.Round(Mathf.Clamp01(value) / safeStep) * safeStep);
        }

        private static int CompareRequestsByPriority(
            WorldGenerativeGeologyVoxelBlendRequest a,
            WorldGenerativeGeologyVoxelBlendRequest b)
        {
            return ComputeRequestPriority(b).CompareTo(ComputeRequestPriority(a));
        }

        private static float ComputeRequestPriority(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            float proximity = 1f - Mathf.Clamp01(request.playerDistance / 180f);
            float archetypeBias = request.archetype switch
            {
                WorldGenerativeGeologyProfile.ShapeArchetype.Arch => 0.08f,
                WorldGenerativeGeologyProfile.ShapeArchetype.CaveBridge => 0.1f,
                WorldGenerativeGeologyProfile.ShapeArchetype.Canopy => 0.04f,
                _ => 0f
            };

            return request.planWeight * 0.5f + request.weight * 0.35f + proximity * 0.15f + archetypeBias;
        }

        private static Vector3 RotateOffset(Quaternion rotation, Vector3 vector)
        {
            return rotation == default ? vector : rotation * vector;
        }

        private static float HashSigned(long runtimeKey, int salt)
        {
            unchecked
            {
                uint value = (uint)(runtimeKey * 1103515245L + salt * 12345L);
                value ^= value >> 16;
                return ((value & 0xFFFFu) / 32767.5f) - 1f;
            }
        }

        private void RemoveVolume(long runtimeKey)
        {
            RemoveVolume(runtimeKey, despawnOwnedVolume: true);
        }

        private void RemoveVolume(long runtimeKey, bool despawnOwnedVolume)
        {
            AbyssalThermalManager thermalManager = GlobalRegistry.Thermodynamics;
            if (thermalManager != null)
                thermalManager.UnregisterRuntimeVent(runtimeKey);
            PersistentWorldRegistry persistentRegistry = GlobalRegistry.PersistentWorldRegistry;
            persistentRegistry?.UnregisterActiveThermalVent(runtimeKey);

            if (!_activeVolumes.TryGetValue(runtimeKey, out GameObject volume))
            {
                _activeRuntimes.Remove(runtimeKey);
                _activeSignatures.Remove(runtimeKey);
                _lastSeenTimes.Remove(runtimeKey);
                return;
            }

            if (despawnOwnedVolume && volume != null && voxelEngine != null && IsTrackedVolumeAlive(runtimeKey))
                voxelEngine.DespawnVolume(volume);

            _activeVolumes.Remove(runtimeKey);
            _activeRuntimes.Remove(runtimeKey);
            _activeSignatures.Remove(runtimeKey);
            _lastSeenTimes.Remove(runtimeKey);
        }

        private void RemoveStaleActiveVolumes()
        {
            _removalBuffer.Clear();
            Dictionary<long, GameObject>.Enumerator activeVolumeEnumerator = _activeVolumes.GetEnumerator();
            while (activeVolumeEnumerator.MoveNext())
            {
                long runtimeKey = activeVolumeEnumerator.Current.Key;
                if (IsTrackedVolumeAlive(runtimeKey))
                    continue;

                _removalBuffer.Add(runtimeKey);
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
                RemoveVolume(_removalBuffer[i], despawnOwnedVolume: false);
        }

        private bool IsTrackedVolumeAlive(long runtimeKey)
        {
            if (!_activeVolumes.TryGetValue(runtimeKey, out GameObject volume) || volume == null)
                return false;

            if (!volume.activeInHierarchy)
                return false;

            if (!_activeRuntimes.TryGetValue(runtimeKey, out WorldGenerativeGeologyVoxelRuntime runtime) ||
                runtime == null)
            {
                return false;
            }

            if (!ReferenceEquals(runtime.gameObject, volume))
                return false;

            if (runtime.RuntimeKey != runtimeKey)
                return false;

            if (_activeSignatures.TryGetValue(runtimeKey, out int signature) &&
                runtime.RequestSignature != signature)
            {
                return false;
            }

            return true;
        }

        private void CancelStalePendingRequests()
        {
            _pendingCancellationBuffer.Clear();
            Dictionary<long, PendingRequestState>.Enumerator pendingEnumerator = _pendingRequests.GetEnumerator();
            while (pendingEnumerator.MoveNext())
            {
                long runtimeKey = pendingEnumerator.Current.Key;
                if (_desiredRuntimeKeys.Contains(runtimeKey))
                    continue;

                _pendingCancellationBuffer.Add(runtimeKey);
            }

            for (int i = 0; i < _pendingCancellationBuffer.Count; i++)
                CancelPendingRequest(_pendingCancellationBuffer[i], true);
        }

        private void CancelPendingRequest(long runtimeKey, bool removeRegistration)
        {
            if (!_pendingRequests.TryGetValue(runtimeKey, out PendingRequestState state))
                return;

            if (state != null && state.Cancellation != null && !state.Cancellation.IsCancellationRequested)
            {
                TraceCancelRequest(runtimeKey, removeRegistration);
                state.Cancellation.Cancel();
            }

            if (removeRegistration)
            {
                _pendingRequests.Remove(runtimeKey);
                _pendingRuntimeKeys.Remove(runtimeKey);
                _queuedLaunchRequests.Remove(runtimeKey);
                _queuedLaunchTimes.Remove(runtimeKey);
                if (_queuedLaunchKeys.Remove(runtimeKey))
                    _queuedLaunchOrder.Remove(runtimeKey);
            }
        }

        private void CancelAllPendingRequests()
        {
            _pendingCancellationBuffer.Clear();
            Dictionary<long, PendingRequestState>.Enumerator pendingEnumerator = _pendingRequests.GetEnumerator();
            while (pendingEnumerator.MoveNext())
                _pendingCancellationBuffer.Add(pendingEnumerator.Current.Key);

            for (int i = 0; i < _pendingCancellationBuffer.Count; i++)
                CancelPendingRequest(_pendingCancellationBuffer[i], true);
        }

        private void ClearAllVolumes()
        {
            _removalBuffer.Clear();
            Dictionary<long, GameObject>.Enumerator activeVolumeEnumerator = _activeVolumes.GetEnumerator();
            while (activeVolumeEnumerator.MoveNext())
                _removalBuffer.Add(activeVolumeEnumerator.Current.Key);

            for (int i = 0; i < _removalBuffer.Count; i++)
                RemoveVolume(_removalBuffer[i]);

            _queuedLaunchRequests.Clear();
            _queuedLaunchTimes.Clear();
            _queuedLaunchKeys.Clear();
            _queuedLaunchOrder.Clear();
            _pendingRuntimeKeys.Clear();
            _lastSeenTimes.Clear();
            _debugActiveVolumes = 0;
            _debugActiveColliders = 0;
            _debugPendingVolumes = 0;
            _debugQueuedLaunches = 0;
            _debugSpawnBudgetUsed = 0;
            _activeRuntimes.Clear();
            _debugTopVolume = string.Empty;
            _debugReady = false;
        }

        private void ResolveReferences()
        {
            WorldRuntimeReferenceUtility.TryResolveWorldGenerativeGeologySeamExecutionDirector(ref seamExecutionDirector);
            WorldRuntimeReferenceUtility.TryResolveVoxelEngine(ref voxelEngine);
        }

        private void EnsureVoxelPoolWarm(int requestCount)
        {
            if (!prewarmVoxelPool || voxelEngine == null || voxelEngine.voxelVolumePrefab == null)
            {
                _debugWarmedPoolTarget = _estimatedWarmedPoolCount;
                return;
            }

            IObjectPoolService pool = GlobalRegistry.ObjectPoolService;
            if (pool == null)
            {
                _debugWarmedPoolTarget = _estimatedWarmedPoolCount;
                return;
            }

            int desiredTarget = Mathf.Clamp(
                Mathf.Max(
                    _activeVolumes.Count + _pendingRuntimeKeys.Count + 1,
                    Mathf.Min(requestCount, Mathf.Max(1, maxRuntimeVolumes)) + Mathf.Max(0, voxelPoolWarmPadding)),
                1,
                Mathf.Max(1, maxRuntimeVolumes + Mathf.Max(0, voxelPoolWarmPadding)));

            if (desiredTarget <= _estimatedWarmedPoolCount)
            {
                _debugWarmedPoolTarget = _estimatedWarmedPoolCount;
                return;
            }

            int delta = desiredTarget - _estimatedWarmedPoolCount;
            int warmupBatch = Mathf.Min(
                delta,
                Mathf.Max(1, maxVoxelPoolWarmupPerTick));
            pool.Warmup(voxelEngine.voxelVolumePrefab, warmupBatch);
            _estimatedWarmedPoolCount += warmupBatch;
            _debugWarmedPoolTarget = _estimatedWarmedPoolCount;
        }

        private static float GetElapsedMilliseconds(long startTimestamp, long endTimestamp)
        {
            if (endTimestamp <= startTimestamp)
                return 0f;

            return (float)((endTimestamp - startTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void TraceReconcile(
            int requestCount,
            int keptCount,
            int desiredCount,
            int activeCount,
            int pendingCount,
            int queuedCount,
            int spawnBudgetUsed,
            float filterMs,
            float warmMs,
            float sortMs,
            float restMs,
            float totalMs)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.reconcile",
                $"requests={requestCount} kept={keptCount} desired={desiredCount} active={activeCount} " +
                $"pending={pendingCount} queued={queuedCount} spawnBudget={spawnBudgetUsed} " +
                $"filter={filterMs:0.00}ms warm={warmMs:0.00}ms sort={sortMs:0.00}ms rest={restMs:0.00}ms total={totalMs:0.00}ms");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void TraceRequestScheduled(
            long runtimeKey,
            string familyId,
            string profileId,
            float weight,
            float playerDistance,
            int activeCount,
            int pendingCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.request",
                $"schedule key={runtimeKey} family={familyId} profile={profileId} weight={weight:0.00} dist={playerDistance:0.0} active={activeCount} pending={pendingCount}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void TraceLaunchStart(
            long runtimeKey,
            string familyId,
            float queuedMs,
            int activeCount,
            int pendingCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.launch",
                $"start key={runtimeKey} family={familyId} queued={queuedMs:0.00}ms active={activeCount} pending={pendingCount}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void TraceRequestPrepared(
            long runtimeKey,
            string familyId,
            string profileId,
            int gridDimension,
            float voxelStep,
            int voxelLodLevel,
            bool buildCollider,
            float buildDataMs)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.request",
                $"prepared key={runtimeKey} family={familyId} profile={profileId} grid={gridDimension} voxel={voxelStep:0.00} lod={voxelLodLevel} collider={buildCollider} buildData={buildDataMs:0.00}ms");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void TraceRequestComplete(
            long runtimeKey,
            string familyId,
            string profileId,
            int gridDimension,
            float voxelStep,
            int voxelLodLevel,
            bool buildCollider,
            float elapsedMs,
            int activeCount,
            int pendingCount)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.request",
                $"complete key={runtimeKey} family={familyId} profile={profileId} grid={gridDimension} voxel={voxelStep:0.00} lod={voxelLodLevel} collider={buildCollider} took={elapsedMs:0.00}ms active={activeCount} pending={pendingCount}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void TraceRequestCanceled(
            long runtimeKey,
            string familyId,
            string profileId,
            float elapsedMs)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.request",
                $"cancel key={runtimeKey} family={familyId} profile={profileId} took={elapsedMs:0.00}ms");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void TraceRequestFault(
            long runtimeKey,
            string familyId,
            string profileId,
            float elapsedMs,
            Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.request",
                $"fault key={runtimeKey} family={familyId} profile={profileId} took={elapsedMs:0.00}ms error={exception.GetType().Name}:{exception.Message}");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        [System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void TraceCancelRequest(long runtimeKey, bool removeRegistration)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!RuntimeDiagnosticsTrace.IsActive)
                return;

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.request",
                $"cancel-request key={runtimeKey} removeRegistration={removeRegistration}");
#endif
        }

        private CancellationTokenSource EnsureLifetimeCancellation()
        {
            if (_lifetimeCancellation == null || _lifetimeCancellation.IsCancellationRequested)
                _lifetimeCancellation = new CancellationTokenSource();

            return _lifetimeCancellation;
        }

        private void CancelLifetimeCancellation()
        {
            if (_lifetimeCancellation == null)
                return;

            if (!_lifetimeCancellation.IsCancellationRequested)
                _lifetimeCancellation.Cancel();

            _lifetimeCancellation.Dispose();
            _lifetimeCancellation = null;
        }

        private bool CanUseRuntimeDispatcher()
        {
            if (!Application.isPlaying || !_runtimeDispatcherReady)
                return false;

#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling || UnityEditor.EditorApplication.isUpdating)
                return false;
#endif

            return true;
        }
    }
}
