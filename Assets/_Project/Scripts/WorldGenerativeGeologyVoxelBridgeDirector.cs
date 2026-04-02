using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Caves;
using Hecton8.Core;
using Hecton8.Dev;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    public sealed class WorldGenerativeGeologyVoxelRuntime : MonoBehaviour
    {
        private static int _activeRuntimeCount;
        private static int _activeColliderCount;

        [SerializeField] private long runtimeKey;
        [SerializeField] private int requestSignature;
        [SerializeField] private string familyId = "world.family.generic";
        [SerializeField] private string geologyProfileId = "geology.generic";
        [SerializeField] private bool colliderEnabled;

        private bool _registeredInActiveSet;

        public long RuntimeKey => runtimeKey;
        public int RequestSignature => requestSignature;
        public string FamilyId => familyId;
        public string GeologyProfileId => geologyProfileId;
        public bool ColliderEnabled => colliderEnabled;
        public static int ActiveRuntimeCount => Mathf.Max(0, _activeRuntimeCount);
        public static int ActiveColliderCount => Mathf.Max(0, _activeColliderCount);

        private void OnEnable()
        {
            if (_registeredInActiveSet)
                return;

            _registeredInActiveSet = true;
            _activeRuntimeCount++;
            if (colliderEnabled)
                _activeColliderCount++;
        }

        private void OnDisable()
        {
            if (!_registeredInActiveSet)
                return;

            _registeredInActiveSet = false;
            _activeRuntimeCount = Mathf.Max(0, _activeRuntimeCount - 1);
            if (colliderEnabled)
                _activeColliderCount = Mathf.Max(0, _activeColliderCount - 1);
        }

        public void Configure(
            long configuredRuntimeKey,
            int configuredSignature,
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
            familyId = string.IsNullOrWhiteSpace(configuredFamilyId) ? "world.family.generic" : configuredFamilyId;
            geologyProfileId = string.IsNullOrWhiteSpace(configuredProfileId) ? "geology.generic" : configuredProfileId;
            colliderEnabled = configuredColliderEnabled;
        }
    }

    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4028)]
    public sealed class WorldGenerativeGeologyVoxelBridgeDirector : MonoBehaviour, ISlowTickable, ITickable
    {
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
        [SerializeField, Range(0.4f, 1f)] private float mediumDistanceResolutionScale = 0.82f;
        [SerializeField, Range(0.4f, 1f)] private float farDistanceResolutionScale = 0.68f;
        [SerializeField, Range(0.4f, 1f)] private float lowWeightResolutionScale = 0.85f;
        [SerializeField] private int maxRuntimeGridDimension = 56;

        [Header("Diagnostics")]
        [SerializeField] private bool _debugReady;
        [SerializeField] private int _debugActiveVolumes;
        [SerializeField] private int _debugActiveColliders;
        [SerializeField] private int _debugPendingVolumes;
        [SerializeField] private int _debugQueuedLaunches;
        [SerializeField] private int _debugSpawnBudgetUsed;
        [SerializeField] private int _debugWarmedPoolTarget;
        [SerializeField] private string _debugTopVolume = "None";

        private readonly Dictionary<long, GameObject> _activeVolumes = new Dictionary<long, GameObject>(32);
        private readonly Dictionary<long, int> _activeSignatures = new Dictionary<long, int>(32);
        private readonly Dictionary<long, float> _lastSeenTimes = new Dictionary<long, float>(32);
        private readonly Dictionary<long, int> _desiredSignatures = new Dictionary<long, int>(32);
        private readonly Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest> _requestLookupByKey = new Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest>(64);
        private readonly Dictionary<long, PendingRequestState> _pendingRequests = new Dictionary<long, PendingRequestState>(32);
        private readonly Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest> _queuedLaunchRequests = new Dictionary<long, WorldGenerativeGeologyVoxelBlendRequest>(32);
        private readonly Dictionary<long, float> _queuedLaunchTimes = new Dictionary<long, float>(32);
        private readonly HashSet<long> _pendingRuntimeKeys = new HashSet<long>();
        private readonly HashSet<long> _queuedLaunchKeys = new HashSet<long>();
        private readonly HashSet<long> _desiredRuntimeKeys = new HashSet<long>();
        private readonly List<long> _desiredRuntimeKeyOrder = new List<long>(32);
        private readonly List<long> _retainedDesiredRuntimeKeyOrder = new List<long>(32);
        private readonly List<long> _queuedLaunchOrder = new List<long>(32);
        private readonly List<long> _removalBuffer = new List<long>(32);
        private readonly List<long> _pendingCancellationBuffer = new List<long>(32);
        private readonly List<WorldGenerativeGeologyVoxelBlendRequest> _sortedRequests = new List<WorldGenerativeGeologyVoxelBlendRequest>(64);
        private bool _registeredToFrameTickManager;
        private bool _registeredToSlowTickManager;
        private int _estimatedWarmedPoolCount;
        private CancellationTokenSource _lifetimeCancellation;

        private void Awake()
        {
            ResolveReferences();
            ReconcileVoxelRequests();
        }

        private void OnEnable()
        {
            ResolveReferences();
            EnsureLifetimeCancellation();
            if (GameTickManager.Instance != null)
            {
                if (!_registeredToFrameTickManager)
                {
                    GameTickManager.Instance.Register((ITickable)this);
                    _registeredToFrameTickManager = true;
                }

                if (!_registeredToSlowTickManager)
                {
                    GameTickManager.Instance.Register((ISlowTickable)this);
                    _registeredToSlowTickManager = true;
                }
            }
        }

        private void Start()
        {
            if (GameTickManager.Instance != null)
            {
                if (!_registeredToFrameTickManager)
                {
                    GameTickManager.Instance.Register((ITickable)this);
                    _registeredToFrameTickManager = true;
                }

                if (!_registeredToSlowTickManager)
                {
                    GameTickManager.Instance.Register((ISlowTickable)this);
                    _registeredToSlowTickManager = true;
                }
            }

            ReconcileVoxelRequests();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance != null)
            {
                if (_registeredToFrameTickManager)
                {
                    GameTickManager.Instance.Unregister((ITickable)this);
                    _registeredToFrameTickManager = false;
                }

                if (_registeredToSlowTickManager)
                {
                    GameTickManager.Instance.Unregister((ISlowTickable)this);
                    _registeredToSlowTickManager = false;
                }
            }

            CancelLifetimeCancellation();
            CancelAllPendingRequests();
            ClearAllVolumes();
        }

        public void Tick(float deltaTime)
        {
            FlushQueuedLaunches();
        }

        public void SlowTick()
        {
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

        public void ReconcileVoxelRequests()
        {
            long reconcileStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
            ResolveReferences();
            _debugTopVolume = "None";

            if (seamExecutionDirector == null || voxelEngine == null)
            {
                ClearAllVolumes();
                return;
            }

            IReadOnlyList<WorldGenerativeGeologyVoxelBlendRequest> requests = seamExecutionDirector.ActiveVoxelRequests;
            float now = Application.isPlaying ? Time.unscaledTime : 0f;
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

            foreach (KeyValuePair<long, GameObject> pair in _activeVolumes)
            {
                if (_desiredRuntimeKeys.Count >= capacity)
                    break;

                if (_desiredRuntimeKeys.Contains(pair.Key))
                    continue;

                if (!ShouldRetainActiveVolume(pair.Key, now))
                    continue;

                AddDesiredRuntimeKey(pair.Key);
            }

            foreach (long runtimeKey in _pendingRuntimeKeys)
            {
                if (_desiredRuntimeKeys.Count >= capacity)
                    break;

                AddDesiredRuntimeKey(runtimeKey);
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
            foreach (KeyValuePair<long, GameObject> pair in _activeVolumes)
            {
                if (_desiredRuntimeKeys.Contains(pair.Key))
                    continue;

                if (_pendingRuntimeKeys.Contains(pair.Key))
                    continue;

                if (_lastSeenTimes.TryGetValue(pair.Key, out float lastSeenTime) &&
                    now - lastSeenTime < Mathf.Max(0.25f, missingRequestGraceSeconds))
                {
                    AddDesiredRuntimeKey(pair.Key);
                    continue;
                }

                if (!_desiredRuntimeKeys.Contains(pair.Key))
                    _removalBuffer.Add(pair.Key);
            }

            for (int i = 0; i < _removalBuffer.Count; i++)
                RemoveVolume(_removalBuffer[i]);

            CancelStalePendingRequests();
            long reconcileEndTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();

            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.reconcile",
                $"requests={requests.Count} kept={_sortedRequests.Count} desired={_desiredRuntimeKeys.Count} active={_activeVolumes.Count} " +
                $"pending={_pendingRuntimeKeys.Count} queued={_queuedLaunchOrder.Count} spawnBudget={spawnBudgetUsed} " +
                $"filter={GetElapsedMilliseconds(reconcileStartTimestamp, requestFilterEndTimestamp):0.00}ms " +
                $"warm={GetElapsedMilliseconds(requestFilterEndTimestamp, poolWarmEndTimestamp):0.00}ms " +
                $"sort={GetElapsedMilliseconds(poolWarmEndTimestamp, sortEndTimestamp):0.00}ms " +
                $"rest={GetElapsedMilliseconds(sortEndTimestamp, reconcileEndTimestamp):0.00}ms total={GetElapsedMilliseconds(reconcileStartTimestamp, reconcileEndTimestamp):0.00}ms");

            _debugActiveVolumes = _activeVolumes.Count;
            _debugActiveColliders = WorldGenerativeGeologyVoxelRuntime.ActiveColliderCount;
            _debugPendingVolumes = _pendingRuntimeKeys.Count;
            _debugQueuedLaunches = _queuedLaunchOrder.Count;
            _debugSpawnBudgetUsed = spawnBudgetUsed;
            _debugReady = _activeVolumes.Count > 0 || _pendingRuntimeKeys.Count > 0;
        }

        private bool ShouldRetainActiveVolume(long runtimeKey, float now)
        {
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
            _lastSeenTimes[request.runtimeKey] = Application.isPlaying ? Time.unscaledTime : 0f;
            int signature = ComputeRequestSignature(request);
            _desiredSignatures[request.runtimeKey] = signature;
            if (_activeSignatures.TryGetValue(request.runtimeKey, out int existingSignature) && existingSignature == signature)
            {
                if (_debugTopVolume == "None")
                    _debugTopVolume = request.familyId;
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
            RuntimeDiagnosticsTrace.WriteEvent(
                "voxel.request",
                $"schedule key={request.runtimeKey} family={request.familyId} profile={request.geologyProfileId} " +
                $"weight={request.weight:0.00} dist={request.playerDistance:0.0} active={_activeVolumes.Count} pending={_pendingRuntimeKeys.Count}");
            QueueLaunchRequest(request);
            spawnBudgetUsed++;
        }

        private void QueueLaunchRequest(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            _queuedLaunchRequests[request.runtimeKey] = request;
            if (_queuedLaunchKeys.Add(request.runtimeKey))
            {
                _queuedLaunchOrder.Add(request.runtimeKey);
                _queuedLaunchTimes[request.runtimeKey] = Application.isPlaying ? Time.unscaledTime : 0f;
            }

            _debugQueuedLaunches = _queuedLaunchOrder.Count;
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
                    ? Mathf.Max(0f, (Time.unscaledTime - queuedAt) * 1000f)
                    : 0f;
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.launch",
                    $"start key={runtimeKey} family={request.familyId} queued={queuedMs:0.00}ms active={_activeVolumes.Count} pending={_pendingRuntimeKeys.Count}");
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
                    return;

                long buildDataStartTimestamp = System.Diagnostics.Stopwatch.GetTimestamp();
                BuildVoxelRequestData(request, out int gridDimension, out float voxelStep, out CaveGenerationParams generationParams, out NativeArray<CaveStructure> structureArray);
                float buildDataMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - buildDataStartTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
                NativeArray<CaveNode> nodes = new NativeArray<CaveNode>(0, Allocator.Persistent);
                NativeArray<CaveTunnel> tunnels = new NativeArray<CaveTunnel>(0, Allocator.Persistent);
                NativeArray<CaveEntrance> entrances = new NativeArray<CaveEntrance>(0, Allocator.Persistent);
                bool buildCollider = ShouldBuildCollider(request);
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.request",
                    $"prepared key={request.runtimeKey} family={request.familyId} profile={request.geologyProfileId} " +
                    $"grid={gridDimension} voxel={voxelStep:0.00} collider={buildCollider} buildData={buildDataMs:0.00}ms");

                try
                {
                    GameObject volume = await voxelEngine.GenerateVolumeFromDataAsync(
                        request.center,
                        gridDimension,
                        voxelStep,
                        nodes,
                        tunnels,
                        entrances,
                        structureArray,
                        generationParams,
                        buildCollider,
                        token);

                    if (volume == null)
                        return;

                    if (!isActiveAndEnabled ||
                        !_desiredSignatures.TryGetValue(request.runtimeKey, out int desiredSignature) ||
                        desiredSignature != signature)
                    {
                        voxelEngine.DespawnVolume(volume);
                        return;
                    }

                    volume.name = $"GeoVoxel_{request.familyId}_{request.runtimeKey}";
                    WorldGenerativeGeologyVoxelRuntime runtime = volume.GetComponent<WorldGenerativeGeologyVoxelRuntime>();
                    if (runtime == null)
                        runtime = volume.AddComponent<WorldGenerativeGeologyVoxelRuntime>();
                    runtime.Configure(request.runtimeKey, signature, request.familyId, request.geologyProfileId, buildCollider);

                    if (_activeVolumes.TryGetValue(request.runtimeKey, out GameObject previousVolume) &&
                        previousVolume != null &&
                        !ReferenceEquals(previousVolume, volume))
                    {
                        voxelEngine.DespawnVolume(previousVolume);
                    }

                    _activeVolumes[request.runtimeKey] = volume;
                    _activeSignatures[request.runtimeKey] = signature;
                    _lastSeenTimes[request.runtimeKey] = Application.isPlaying ? Time.unscaledTime : 0f;
                    if (_debugTopVolume == "None")
                        _debugTopVolume = request.familyId;

                    float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - requestStartTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
                    RuntimeDiagnosticsTrace.WriteEvent(
                        "voxel.request",
                        $"complete key={request.runtimeKey} family={request.familyId} profile={request.geologyProfileId} " +
                        $"grid={gridDimension} voxel={voxelStep:0.00} collider={buildCollider} took={elapsedMs:0.00}ms " +
                        $"active={_activeVolumes.Count} pending={_pendingRuntimeKeys.Count}");
                }
                finally
                {
                    if (nodes.IsCreated) nodes.Dispose();
                    if (tunnels.IsCreated) tunnels.Dispose();
                    if (entrances.IsCreated) entrances.Dispose();
                    if (structureArray.IsCreated) structureArray.Dispose();
                }
            }
            catch (OperationCanceledException)
            {
                float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - requestStartTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.request",
                    $"cancel key={request.runtimeKey} family={request.familyId} profile={request.geologyProfileId} took={elapsedMs:0.00}ms");
            }
            catch (Exception ex)
            {
                float elapsedMs = (float)((System.Diagnostics.Stopwatch.GetTimestamp() - requestStartTimestamp) * 1000.0d / System.Diagnostics.Stopwatch.Frequency);
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.request",
                    $"fault key={request.runtimeKey} family={request.familyId} profile={request.geologyProfileId} " +
                    $"took={elapsedMs:0.00}ms error={ex.GetType().Name}:{ex.Message}");
                throw;
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
            out CaveGenerationParams generationParams,
            out NativeArray<CaveStructure> structures)
        {
            float dominantSize = Mathf.Clamp(math.cmax((float3)request.size), minVoxelSize, maxVoxelSize) + voxelPadding;
            int targetResolution = ResolveTargetResolution(request);
            float stabilizedWeight = Quantize01(request.weight, 0.05f);
            voxelStep = Mathf.Clamp(dominantSize / Mathf.Max(24f, targetResolution), minVoxelStep, maxVoxelStep);
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
            structures = new NativeArray<CaveStructure>(
                ResolveStructureCount(request.archetype),
                Allocator.Persistent,
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
            Vector3 left = request.center + upOffset - lateral * (span * 0.36f);
            Vector3 right = request.center + upOffset + lateral * (span * 0.36f);
            Vector3 crown = request.center + upOffset + Vector3.up * (height * 0.7f);
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
                request.center + upOffset + Vector3.up * (height * 0.22f),
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
            Vector3 root = request.center + upOffset;
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
            structures[0] = CreateBlock(request.center, request.size * 0.22f, request.weight);
            structures[1] = CreateBlock(
                request.center + lateral * (request.size.x * 0.12f) + Vector3.up * (request.size.y * 0.1f) - forward * (request.size.z * 0.08f),
                request.size * 0.16f,
                request.weight * 0.8f);
            structures[2] = CreateBlock(
                request.center - lateral * (request.size.x * 0.08f) + Vector3.up * (request.size.y * 0.18f) + forward * (request.size.z * 0.06f),
                request.size * 0.12f,
                request.weight * 0.62f);
            structures[3] = CreateBoulder(
                request.center - lateral * (request.size.x * 0.1f) + Vector3.up * (request.size.y * 0.06f) + forward * (request.size.z * 0.1f),
                Mathf.Max(2f, request.size.x * 0.12f),
                request.weight * 0.9f);
            structures[4] = CreateBoulder(
                request.center + lateral * (request.size.x * 0.16f) + Vector3.up * (request.size.y * 0.03f) - forward * (request.size.z * 0.12f),
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
            unchecked
            {
                int hash = (int)request.runtimeKey;
                hash = (hash * 397) ^ request.caveBlendMode.GetHashCode();
                hash = (hash * 397) ^ request.archetype.GetHashCode();
                hash = (hash * 397) ^ ResolveTargetResolution(request);
                hash = (hash * 397) ^ (ShouldBuildCollider(request) ? 1 : 0);
                return hash;
            }
        }

        private int ResolveTargetResolution(in WorldGenerativeGeologyVoxelBlendRequest request)
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
            float resolvedFarDistance = Mathf.Max(nearFieldDistance + 1f, farFieldDistance);
            float distanceT = Mathf.InverseLerp(
                Mathf.Max(0f, nearFieldDistance),
                resolvedFarDistance,
                Mathf.Max(0f, request.playerDistance));
            int distanceCap = Mathf.RoundToInt(Mathf.Lerp(nearResolution, farResolution, distanceT));
            float resolutionScale = 1f;
            if (request.playerDistance >= resolvedFarDistance)
                resolutionScale *= farDistanceResolutionScale;
            else if (request.playerDistance > Mathf.Max(0f, nearFieldDistance))
                resolutionScale *= mediumDistanceResolutionScale;

            float priority = Mathf.Clamp01(request.planWeight * 0.65f + request.weight * 0.35f);
            if (priority < 0.45f)
                resolutionScale *= lowWeightResolutionScale;

            int scaledResolution = Mathf.RoundToInt(baseResolution * resolutionScale);
            return Mathf.Clamp(Mathf.Min(distanceCap, scaledResolution), 32, nearResolution);
        }

        private bool ShouldBuildCollider(in WorldGenerativeGeologyVoxelBlendRequest request)
        {
            float threshold = Mathf.Max(8f, colliderBuildDistance);
            return request.playerDistance <= threshold;
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
            if (!_activeVolumes.TryGetValue(runtimeKey, out GameObject volume))
                return;

            if (volume != null && voxelEngine != null)
                voxelEngine.DespawnVolume(volume);

            _activeVolumes.Remove(runtimeKey);
            _activeSignatures.Remove(runtimeKey);
            _lastSeenTimes.Remove(runtimeKey);
        }

        private void CancelStalePendingRequests()
        {
            _pendingCancellationBuffer.Clear();
            foreach (KeyValuePair<long, PendingRequestState> pair in _pendingRequests)
            {
                if (_desiredRuntimeKeys.Contains(pair.Key))
                    continue;

                _pendingCancellationBuffer.Add(pair.Key);
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
                RuntimeDiagnosticsTrace.WriteEvent(
                    "voxel.request",
                    $"cancel-request key={runtimeKey} removeRegistration={removeRegistration}");
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
            foreach (KeyValuePair<long, PendingRequestState> pair in _pendingRequests)
                _pendingCancellationBuffer.Add(pair.Key);

            for (int i = 0; i < _pendingCancellationBuffer.Count; i++)
                CancelPendingRequest(_pendingCancellationBuffer[i], true);
        }

        private void ClearAllVolumes()
        {
            _removalBuffer.Clear();
            foreach (KeyValuePair<long, GameObject> pair in _activeVolumes)
                _removalBuffer.Add(pair.Key);

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
            _debugTopVolume = "None";
            _debugReady = false;
        }

        private void ResolveReferences()
        {
            if (seamExecutionDirector == null)
                seamExecutionDirector = FindAnyObjectByType<WorldGenerativeGeologySeamExecutionDirector>();

            if (voxelEngine == null)
                voxelEngine = FindAnyObjectByType<HectonVoxelEngine>();
        }

        private void EnsureVoxelPoolWarm(int requestCount)
        {
            if (!prewarmVoxelPool || voxelEngine == null || voxelEngine.voxelVolumePrefab == null)
            {
                _debugWarmedPoolTarget = _estimatedWarmedPoolCount;
                return;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
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
    }
}
