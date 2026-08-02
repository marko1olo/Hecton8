using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Dev;
using UnityEngine;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace Hecton8.Core
{
    /// <summary>
    /// Scene-owned runtime object pool service registered through <see cref="GlobalRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)]
    public sealed class ObjectPoolManager : MonoBehaviour, IObjectPoolService, IServiceHeartbeat, IServiceShutdown
    {
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
        internal static Action s_testHook_BeforeAwaitableDebtMonitorNextFrameAsync;
#endif
        private const string PrefabRegistryRuntimeName = "[PrefabRegistry]";
        private const string PoolContainerRuntimeName = "[Pool]";
        private const uint PoolExhaustedReasonMissingPool = 1u;
        private const uint PoolExhaustedReasonExpandRejected = 2u;
        private const uint PoolExhaustedReasonEmptyPool = 3u;
        private const double DefaultWarmupFrameBudgetMilliseconds = 8.0d;
        private const float MemoryPressureInactiveTrimFraction = 0.5f;

        [Header("── Warmup Presets ────────────────────────────")]
        [Tooltip("Automatic warmup entries executed during Start.")]
        [SerializeField] private WarmupEntry[] warmupPresets;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugPoolCount;
        [SerializeField] private int _debugTotalPooled;

        // COLD ALLOC: List<IPoolable>[8] — pooled component dispatch scratch — owner: ObjectPoolManager
        private static readonly List<IPoolable> s_poolableCache = new List<IPoolable>(8);
        // COLD ALLOC: List<Component>[8] - pooled root component cache scratch - owner: ObjectPoolManager
        private static readonly List<Component> s_componentCache = new List<Component>(8);

        private Dictionary<int, Pool> _pools;
        // COLD ALLOC: Dictionary<GameObject, PoolItemMarker>[256] - pooled instance metadata cache - owner: ObjectPoolManager
        private readonly Dictionary<GameObject, PoolItemMarker> _poolMarkerCache = new Dictionary<GameObject, PoolItemMarker>(256);
        private bool _serviceRegistered;
        private bool _warmupPresetsStarted;
        private bool _warmupPresetsCompleted;
        private bool _serviceShuttingDown;

        internal static ObjectPoolManager ActiveRuntimeInstance { get; private set; }

        /// <summary>
        /// Runtime pool service owned by the active scene instance.
        /// </summary>
        public static ObjectPoolManager Instance
        {
            get
            {
                ObjectPoolManager instance = null;
                TryResolveActiveRuntime(ref instance);
                return instance;
            }
        }

        public static bool TryResolveActiveRuntime(ref ObjectPoolManager target)
        {
            ObjectPoolManager registered = GlobalRegistry.ObjectPoolRuntimeMirror;
            if (IsRuntimeOwnerUsable(registered))
            {
                target = registered;
                if (!IsRuntimeOwnerUsable(ActiveRuntimeInstance))
                    ActiveRuntimeInstance = registered;
                return true;
            }

            ClearRuntimeMirrorIfOwnedBy(registered);

            ObjectPoolManager active = ActiveRuntimeInstance;
            if (IsRuntimeOwnerUsable(active))
            {
                target = active;
                return true;
            }

            ClearRuntimeMirrorIfOwnedBy(active);
            target = null;
            return false;
        }

        private static bool IsRuntimeOwnerUsable(ObjectPoolManager runtime)
        {
            return IsRuntimeOwnerUsableForRegistry(runtime);
        }

        public static bool IsRuntimeOwnerUsableForRegistry(ObjectPoolManager runtime)
        {
            return runtime != null &&
                   runtime.isActiveAndEnabled &&
                   !runtime._serviceShuttingDown;
        }

        internal static bool CanDespawnWithPool(IObjectPoolService pool, GameObject instance)
        {
            return instance != null &&
                   pool is ObjectPoolManager manager &&
                   IsRuntimeOwnerUsableForRegistry(manager) &&
                   manager.CanDespawnWithoutDestroy(instance);
        }

        internal static bool TryResolvePoolForInstance(
            GameObject instance,
            IObjectPoolService preferredPool,
            out IObjectPoolService pool)
        {
            if (CanDespawnWithPool(preferredPool, instance))
            {
                pool = preferredPool;
                return true;
            }

            if (instance != null &&
                instance.TryGetComponent(out PoolItemMarker marker) &&
                marker.Owner != null &&
                IsRuntimeOwnerUsableForRegistry(marker.Owner) &&
                marker.Owner.CanDespawnWithoutDestroy(instance))
            {
                pool = marker.Owner;
                return true;
            }

            ObjectPoolManager active = null;
            if (instance != null &&
                TryResolveActiveRuntime(ref active) &&
                active.CanDespawnWithoutDestroy(instance))
            {
                pool = active;
                return true;
            }

            pool = null;
            return false;
        }

        internal static void DespawnOrDeactivate(GameObject instance, IObjectPoolService preferredPool)
        {
            if (instance == null)
                return;

            if (TryResolvePoolForInstance(instance, preferredPool, out IObjectPoolService pool))
            {
                pool.Despawn(instance);
                return;
            }

            instance.SetActive(false);
        }

        private static void ClearRuntimeMirrorIfOwnedBy(ObjectPoolManager runtime)
        {
            if (ReferenceEquals(runtime, null))
                return;

            if (ReferenceEquals(ActiveRuntimeInstance, runtime))
                ActiveRuntimeInstance = null;

            GlobalRegistry.UnregisterObjectPoolService(runtime);
        }

        /// <summary>
        /// True after scene-authored warmup presets have finished their frame-budgeted allocation pass.
        /// </summary>
        public bool AreWarmupPresetsCompleted => _warmupPresetsCompleted;

        /// <inheritdoc />
        public ServiceHeartbeatState HeartbeatState => _serviceRegistered ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.NotStarted;

        /// <inheritdoc />
        public bool IsServiceReady => _serviceRegistered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            ActiveRuntimeInstance = null;
        }

        [System.Serializable]
#pragma warning disable 0649 // Unity serializes warmup presets from scene/prefab authoring data.
        private struct WarmupEntry
        {
            [Tooltip("Prefab to prewarm into the runtime pool.")]
            public GameObject prefab;

            [Tooltip("How many inactive instances to allocate during warmup.")]
            public int count;
        }
#pragma warning restore 0649

        private sealed class Pool
        {
            public Queue<GameObject> available;
            public Transform container;
            public GameObject prefab;
            public int prefabId;
            public int capacity;

            public int markerIndex = -1;
            public int rendererIndex = -1;
            public int rigidbodyIndex = -1;
            public int despawnTimerIndex = -1;
            public int[] poolableIndices = Array.Empty<int>();
            public bool hasDynamicMarker = false;
        }

        private void Awake()
        {
            if (!IsRuntimeOwnerUsable(ActiveRuntimeInstance))
                ActiveRuntimeInstance = this;

            EnsurePrefabRegistry();

            // COLD ALLOC: Dictionary<int, Pool>[32] — prefab id to pool lookup — owner: ObjectPoolManager
            EnsurePoolDictionary();
            _serviceShuttingDown = false;
            _warmupPresetsCompleted = CountWarmupPresetInstances() <= 0;
        }

        private void Start()
        {
            if (_serviceShuttingDown || _warmupPresetsCompleted || _warmupPresetsStarted)
                return;

            _ = WarmupPresetsAsync(
                DefaultWarmupFrameBudgetMilliseconds,
                destroyCancellationToken);
        }

        private void OnDestroy()
        {
            ShutdownServiceState(releasePoolLookup: true);
        }

        public void OnServiceShutdown()
        {
            ShutdownServiceState(releasePoolLookup: false);
        }

        private void ShutdownServiceState(bool releasePoolLookup)
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;

            _serviceShuttingDown = true;
            _warmupPresetsStarted = false;
            _warmupPresetsCompleted = true;
            s_poolableCache.Clear();
            s_componentCache.Clear();

            if (_pools != null)
            {
                ClearAllPools();
                if (releasePoolLookup)
                    _pools = null;
            }

            _poolMarkerCache.Clear();

            if (_serviceRegistered)
                GlobalRegistry.UnregisterObjectPoolService(this);

            _serviceRegistered = false;
        }

        /// <summary>
        /// Registers the runtime pool service into <see cref="GlobalRegistry"/>.
        /// </summary>
        public void InitializeService()
        {
            if (_serviceRegistered)
                return;

            if (!IsRuntimeOwnerUsable(ActiveRuntimeInstance))
                ActiveRuntimeInstance = this;

            EnsurePoolDictionary();
            _serviceShuttingDown = false;
            if (_pools.Count == 0)
            {
                _warmupPresetsStarted = false;
                _warmupPresetsCompleted = CountWarmupPresetInstances() <= 0;
            }

            GlobalRegistry.RegisterObjectPoolService(this);
            GlobalTelemetryBus.Initialize();
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ObjectPoolRuntimeMirror, this);
            if (_serviceRegistered)
                ActiveRuntimeInstance = this;
        }

        /// <summary>
        /// Pre-allocates a pool for the given prefab.
        /// </summary>
        public void Warmup(GameObject prefab, int count)
        {
            if (_serviceShuttingDown)
                return;

            if (prefab == null)
            {
                LogWarmupNullPrefab();
                return;
            }

            if (count <= 0 || !TryGetPrefabRegistry(out PrefabRegistry registry))
                return;

            EnsurePoolDictionary();
            Pool pool = PreparePool(prefab, registry);

            for (int i = 0; i < count; i++)
            {
                GameObject instance = InstantiatePooled(prefab, pool.prefabId, pool);
                instance.SetActive(false);
                pool.available.Enqueue(instance);
            }

            UpdateDiagnostics();
        }

        /// <summary>
        /// Allocates scene-authored pool presets over a hard main-thread time budget.
        /// </summary>
        /// <param name="frameBudgetMilliseconds">Main-thread time budget before yielding a frame.</param>
        /// <param name="cancellationToken">Bootstrap cancellation token.</param>
        /// <returns>True when every configured preset is complete.</returns>
        public async Awaitable<bool> WarmupPresetsAsync(
            double frameBudgetMilliseconds,
            CancellationToken cancellationToken)
        {
            if (_serviceShuttingDown)
                return false;

            if (_warmupPresetsCompleted)
                return true;

            if (_warmupPresetsStarted)
            {
                float watchdogStartTime = UnityEngine.Time.realtimeSinceStartup;
                while (!_warmupPresetsCompleted)
                {
                    if (_serviceShuttingDown)
                        return false;

                    if (UnityEngine.Time.realtimeSinceStartup - watchdogStartTime > 5f)
                    {
                        UnityEngine.Debug.LogError("[ObjectPoolManager] WarmupPresetsAsync wait timed out. Original task likely crashed.");
                        _warmupPresetsCompleted = true;
                        return false;
                    }

                    cancellationToken.ThrowIfCancellationRequested();
                    await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                }

                return true;
            }

            _warmupPresetsStarted = true;
            double frameBudget = Math.Max(0.1d, frameBudgetMilliseconds);
            long frameStartTimestamp = Stopwatch.GetTimestamp();

            try
            {
                if (warmupPresets == null || warmupPresets.Length == 0 || !TryGetPrefabRegistry(out PrefabRegistry registry))
                {
                    _warmupPresetsCompleted = true;
                    return true;
                }

                EnsurePoolDictionary();
                for (int entryIndex = 0; entryIndex < warmupPresets.Length; entryIndex++)
                {
                    if (_serviceShuttingDown)
                        return false;

                    cancellationToken.ThrowIfCancellationRequested();

                    WarmupEntry entry = warmupPresets[entryIndex];
                    if (entry.prefab == null || entry.count <= 0)
                        continue;

                    Pool pool = PreparePool(entry.prefab, registry);
                    for (int instanceIndex = 0; instanceIndex < entry.count; instanceIndex++)
                    {
                        if (_serviceShuttingDown)
                            return false;

                        cancellationToken.ThrowIfCancellationRequested();

                        GameObject instance = InstantiatePooled(entry.prefab, pool.prefabId, pool);
                        instance.SetActive(false);
                        pool.available.Enqueue(instance);

                        if (!HasWarmupFrameBudgetElapsed(frameStartTimestamp, frameBudget))
                            continue;

                        UpdateDiagnostics();
#if UNITY_EDITOR || UNITY_INCLUDE_TESTS
                        s_testHook_BeforeAwaitableDebtMonitorNextFrameAsync?.Invoke();
#endif
                        await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                        if (_serviceShuttingDown)
                            return false;

                        frameStartTimestamp = Stopwatch.GetTimestamp();
                    }
                }

                UpdateDiagnostics();
                _warmupPresetsCompleted = true;
                return true;
            }
            catch (OperationCanceledException)
            {
                _warmupPresetsStarted = false;
                return false;
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"[ObjectPoolManager] WarmupPresetsAsync crashed: {ex}");
                _warmupPresetsCompleted = true;
                return false;
            }
        }

        /// <summary>
        /// Pre-allocates one runtime pool over the same frame-budgeted path used by bootstrap presets.
        /// </summary>
        public async Awaitable<bool> WarmupPrefabAsync(
            GameObject prefab,
            int count,
            double frameBudgetMilliseconds,
            CancellationToken cancellationToken)
        {
            if (_serviceShuttingDown)
                return false;

            if (prefab == null)
            {
                LogWarmupNullPrefab();
                return true;
            }

            if (count <= 0)
                return true;

            if (!TryGetPrefabRegistry(out PrefabRegistry registry))
                return false;

            EnsurePoolDictionary();
            double frameBudget = Math.Max(0.1d, frameBudgetMilliseconds);
            long frameStartTimestamp = Stopwatch.GetTimestamp();
            Pool pool = PreparePool(prefab, registry);
            int existingCount = pool.available.Count;
            int missingCount = count > existingCount ? count - existingCount : 0;
            for (int instanceIndex = 0; instanceIndex < missingCount; instanceIndex++)
            {
                if (_serviceShuttingDown)
                    return false;

                cancellationToken.ThrowIfCancellationRequested();

                GameObject instance = InstantiatePooled(prefab, pool.prefabId, pool);
                instance.SetActive(false);
                pool.available.Enqueue(instance);

                if (!HasWarmupFrameBudgetElapsed(frameStartTimestamp, frameBudget))
                    continue;

                UpdateDiagnostics();
                await AwaitableDebtMonitor.NextFrameAsync(cancellationToken);
                if (_serviceShuttingDown)
                    return false;

                frameStartTimestamp = Stopwatch.GetTimestamp();
            }

            UpdateDiagnostics();
            return true;
        }

        private static bool HasWarmupFrameBudgetElapsed(long frameStartTimestamp, double frameBudgetMilliseconds)
        {
            long elapsedTicks = Stopwatch.GetTimestamp() - frameStartTimestamp;
            double elapsedMilliseconds = elapsedTicks * 1000.0d / Stopwatch.Frequency;
            return elapsedMilliseconds > frameBudgetMilliseconds;
        }

        /// <summary>
        /// Compatibility warmup overload for systems that still pass a Component instead of its root GameObject.
        /// </summary>
        public void Warmup(Component prefab, int count)
        {
            Warmup(prefab != null ? prefab.gameObject : null, count);
        }

        /// <summary>
        /// Spawns an instance from the pool using the provided transform.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation)
        {
            return Spawn(prefab, position, rotation, false);
        }

        /// <summary>
        /// Compatibility spawn overload for systems that still pass a Component instead of its root GameObject.
        /// </summary>
        public GameObject Spawn(Component prefab, Vector3 position, Quaternion rotation)
        {
            return Spawn(prefab != null ? prefab.gameObject : null, position, rotation, false);
        }

        /// <summary>
        /// Legacy compatibility overload. Runtime pool expansion is forbidden.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, bool allowExpand)
        {
            if (_serviceShuttingDown)
                return null;

            if (prefab == null)
            {
                LogSpawnNullPrefab();
                return null;
            }

            if (!TryGetPrefabRegistry(out PrefabRegistry registry))
                return null;

            EnsurePoolDictionary();
            int prefabId = registry.GetOrRegisterPrefab(prefab);
            if (!_pools.TryGetValue(prefabId, out Pool pool))
            {
                WarnExpand(
                    prefab,
                    prefabId,
                    PoolExhaustedReasonMissingPool,
                    "Pool missing. Pre-allocate via Warmup during bootstrap.");
                return null;
            }

            while (pool.available.Count > 0)
            {
                GameObject instance = pool.available.Dequeue();
                // Unity fake-null: destroyed objects compare equal to null.
                if (instance == null)
                    continue;

                if (!_poolMarkerCache.TryGetValue(instance, out PoolItemMarker marker) || marker == null)
                {
                    LogSpawnMissingMarker(instance);
                    _poolMarkerCache.Remove(instance);
                    pool.capacity = Mathf.Max(0, pool.capacity - 1);
                    if (instance != null)
                        Destroy(instance);
                    continue;
                }

                // L19 hop2 LIVE: ACCESS_VIOLATION inside Transform::SetParent during scatter
                // reconcile spawn. Managed GO can survive queue dequeue while native Transform
                // is already torn down (domain churn / parent container destroyed).
                // try/catch cannot stop native Crash!!! — avoid SetParent when already free,
                // and drop the instance if transform/container looks unusable.
                if (pool.container == null)
                {
                    // Pool container destroyed → entire inactive queue is untrusted.
                    _poolMarkerCache.Remove(instance);
                    pool.capacity = Mathf.Max(0, pool.capacity - 1);
                    while (pool.available.Count > 0)
                    {
                        GameObject stale = pool.available.Dequeue();
                        if (stale != null)
                        {
                            _poolMarkerCache.Remove(stale);
                            Destroy(stale);
                        }
                    }
                    return null;
                }

                Transform instanceTransform = instance.transform;
                if (instanceTransform == null)
                {
                    _poolMarkerCache.Remove(instance);
                    pool.capacity = Mathf.Max(0, pool.capacity - 1);
                    continue;
                }

                // L19 hop2: never call SetParent on spawn. Transform::SetParent was the native
                // Crash!!! site. SetPositionAndRotation writes world pose regardless of parent;
                // inactive instances already live under pool.container (identity local under a
                // stationary OPM root). Avoiding detach eliminates the AV without changing pose.
                instanceTransform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);

                NotifySpawn(marker);
                return instance;


            }


            if (allowExpand)
            {
                WarnExpand(
                    prefab,
                    prefabId,
                    PoolExhaustedReasonExpandRejected,
                    "Pool exhausted. Expansion disabled by mandate; returning null.");
            }
            else
            {
                WarnExpand(
                    prefab,
                    prefabId,
                    PoolExhaustedReasonEmptyPool,
                    "Pool exhausted. Pre-allocate a larger warmup count.");
            }

            return null;
        }

        /// <summary>
        /// Compatibility spawn overload for systems that still pass a Component instead of its root GameObject.
        /// </summary>
        public GameObject Spawn(Component prefab, Vector3 position, Quaternion rotation, bool allowExpand)
        {
            return Spawn(prefab != null ? prefab.gameObject : null, position, rotation, allowExpand);
        }

        /// <summary>
        /// Convenience overload that uses identity rotation.
        /// </summary>
        public GameObject Spawn(GameObject prefab, Vector3 position)
        {
            return Spawn(prefab, position, Quaternion.identity);
        }

        /// <summary>
        /// Compatibility spawn overload that uses identity rotation for Component-prefab callers.
        /// </summary>
        public GameObject Spawn(Component prefab, Vector3 position)
        {
            return Spawn(prefab != null ? prefab.gameObject : null, position, Quaternion.identity, false);
        }

        /// <summary>
        /// Returns true when <see cref="Despawn(GameObject)"/> can return this instance to a known pool without falling back to Destroy.
        /// </summary>
        public bool CanDespawnWithoutDestroy(GameObject instance)
        {
            return instance != null &&
                   _pools != null &&
                   _poolMarkerCache.TryGetValue(instance, out PoolItemMarker marker) &&
                   marker != null &&
                   _pools.ContainsKey(marker.PrefabId);
        }

        /// <summary>
        /// Returns an instance to its originating pool.
        /// </summary>
        public void Despawn(GameObject instance)
        {
            if (_serviceShuttingDown)
                return;

            if (instance == null)
                return;

            if (!_poolMarkerCache.TryGetValue(instance, out PoolItemMarker marker) || marker == null)
            {
                LogDespawnMissingMarker(instance);
                Destroy(instance);
                return;
            }

            int prefabId = marker.PrefabId;
            if (!_pools.TryGetValue(prefabId, out Pool pool))
            {
                LogDespawnMissingPool(instance);
                _poolMarkerCache.Remove(instance);
                Destroy(instance);
                return;
            }

            NotifyDespawn(marker);
            instance.SetActive(false);

            Transform instanceTransform = instance.transform;
            // L19 hop2: destroyed container/instance → drop. Spawn no longer detaches, so most
            // despawns are already under pool.container — skip SetParent when parent matches
            // (avoids Transform::SetParent native AV on half-dead hierarchy).
            if (instanceTransform == null || pool.container == null)
            {
                _poolMarkerCache.Remove(instance);
                Destroy(instance);
                return;
            }

            if (!ReferenceEquals(instanceTransform.parent, pool.container))
                instanceTransform.SetParent(pool.container, false);

            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;

            if (pool.available.Count >= pool.capacity)

            {
                _poolMarkerCache.Remove(instance);
                Destroy(instance);
                return;
            }

            pool.available.Enqueue(instance);
        }


        /// <summary>
        /// Compatibility despawn overload for systems that still pass a Component instead of its owning GameObject.
        /// </summary>
        public void Despawn(Component instance)
        {
            Despawn(instance != null ? instance.gameObject : null);
        }

        /// <summary>
        /// Returns an instance to its pool after a delay.
        /// </summary>
        public void Despawn(GameObject instance, float delaySeconds)
        {
            if (instance == null)
                return;

            if (delaySeconds <= 0f)
            {
                Despawn(instance);
                return;
            }

            if (_poolMarkerCache.TryGetValue(instance, out PoolItemMarker marker) &&
                marker != null &&
                marker.RootDespawnTimer != null)
            {
                DespawnTimer timer = marker.RootDespawnTimer;
                timer.StartTimer(delaySeconds);
                return;
            }

            LogMissingDespawnTimer(instance);
            Despawn(instance);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDespawnMissingMarker(GameObject instance)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string instanceName = instance != null ? instance.name : "<null>";
            Hecton8.Core.H8Debug.LogWarning(
                $"[ObjectPoolManager] Despawn: '{instanceName}' has no PoolItemMarker. Destroying instead.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogDespawnMissingPool(GameObject instance)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string instanceName = instance != null ? instance.name : "<null>";
            Hecton8.Core.H8Debug.LogWarning(
                $"[ObjectPoolManager] Despawn: Pool for '{instanceName}' not found. Destroying.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogMissingDespawnTimer(GameObject instance)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string instanceName = instance != null ? instance.name : "<null>";
            Hecton8.Core.H8Debug.LogWarning(
                $"[ObjectPoolManager] Prefab '{instanceName}' is missing a DespawnTimer component. Despawning immediately.");
#endif
        }

        /// <summary>
        /// Compatibility delayed-despawn overload for systems that still pass a Component instead of its owning GameObject.
        /// </summary>
        public void Despawn(Component instance, float delaySeconds)
        {
            Despawn(instance != null ? instance.gameObject : null, delaySeconds);
        }

        /// <summary>
        /// Returns the number of available inactive instances for a prefab.
        /// </summary>
        public int GetAvailableCount(GameObject prefab)
        {
            if (_serviceShuttingDown)
                return 0;

            if (prefab == null || !TryGetPrefabRegistry(out PrefabRegistry registry))
                return 0;

            EnsurePoolDictionary();
            int prefabId = registry.GetOrRegisterPrefab(prefab);
            return _pools.TryGetValue(prefabId, out Pool pool) ? pool.available.Count : 0;
        }

        /// <summary>
        /// Compatibility query overload for systems that still pass a Component instead of its root GameObject.
        /// </summary>
        public int GetAvailableCount(Component prefab)
        {
            return GetAvailableCount(prefab != null ? prefab.gameObject : null);
        }

        internal int GetAvailableCountByPrefabId(int prefabId)
        {
            if (prefabId == 0 || _pools == null)
                return 0;

            return _pools.TryGetValue(prefabId, out Pool pool) ? pool.available.Count : 0;
        }

        /// <summary>
        /// Reads inactive reserve count for a pooled runtime instance without exposing pool marker internals to consumers.
        /// </summary>
        public bool TryGetAvailableCountForPooledInstance(GameObject instance, out int availableCount)
        {
            availableCount = 0;

            if (instance == null || !_poolMarkerCache.TryGetValue(instance, out PoolItemMarker marker) || marker == null)
                return false;

            availableCount = GetAvailableCountByPrefabId(marker.PrefabId);
            return true;
        }

        /// <summary>
        /// Reads the renderer cached during pool instantiation. This avoids component probing during spawn users' hot paths.
        /// </summary>
        public bool TryGetPooledRootRenderer(GameObject instance, out Renderer renderer)
        {
            renderer = null;
            if (instance == null || !_poolMarkerCache.TryGetValue(instance, out PoolItemMarker marker) || marker == null)
                return false;

            renderer = marker.RootRenderer;
            return renderer != null;
        }

        public bool TryGetPooledRootRigidbody(GameObject instance, out Rigidbody rigidbody)
        {
            rigidbody = null;
            if (instance == null || !_poolMarkerCache.TryGetValue(instance, out PoolItemMarker marker) || marker == null)
                return false;

            rigidbody = marker.RootRigidbody;
            return rigidbody != null;
        }

        public bool TryGetPooledComponent<T>(GameObject instance, out T component) where T : class
        {
            component = null;
            if (instance == null || !_poolMarkerCache.TryGetValue(instance, out PoolItemMarker marker) || marker == null)
                return false;

            return marker.TryGetCachedComponent(out component) || marker.TryGetPoolable(out component);
        }

        /// <summary>
        /// Returns whether a pool already exists for the given prefab.
        /// </summary>
        public bool HasPool(GameObject prefab)
        {
            if (_serviceShuttingDown)
                return false;

            if (prefab == null || !TryGetPrefabRegistry(out PrefabRegistry registry))
                return false;

            EnsurePoolDictionary();
            int prefabId = registry.GetPrefabId(prefab);
            return prefabId != 0 && _pools.ContainsKey(prefabId);
        }

        /// <summary>
        /// Compatibility query overload for systems that still pass a Component instead of its root GameObject.
        /// </summary>
        public bool HasPool(Component prefab)
        {
            return HasPool(prefab != null ? prefab.gameObject : null);
        }

        /// <summary>
        /// Destroys a single prefab pool and its inactive instances.
        /// </summary>
        public void ClearPool(GameObject prefab)
        {
            if (_serviceShuttingDown)
                return;

            if (prefab == null || !TryGetPrefabRegistry(out PrefabRegistry registry))
                return;

            EnsurePoolDictionary();
            int prefabId = registry.GetOrRegisterPrefab(prefab);
            if (!_pools.TryGetValue(prefabId, out Pool pool))
                return;

            while (pool.available.Count > 0)
            {
                GameObject instance = pool.available.Dequeue();
                if (instance != null)
                {
                    _poolMarkerCache.Remove(instance);
                    Destroy(instance);
                }
            }

            if (pool.container != null)
                Destroy(pool.container.gameObject);

            _pools.Remove(prefabId);
            UpdateDiagnostics();
        }

        /// <summary>
        /// Compatibility clear overload for systems that still pass a Component instead of its root GameObject.
        /// </summary>
        public void ClearPool(Component prefab)
        {
            ClearPool(prefab != null ? prefab.gameObject : null);
        }

        /// <summary>
        /// Destroys every inactive instance owned by the manager.
        /// </summary>
        public void ClearAllPools()
        {
            Dictionary<int, Pool>.Enumerator enumerator = _pools.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Pool pool = enumerator.Current.Value;
                while (pool.available.Count > 0)
                {
                    GameObject instance = pool.available.Dequeue();
                    if (instance != null)
                    {
                        _poolMarkerCache.Remove(instance);
                        Destroy(instance);
                    }
                }

                if (pool.container != null)
                    Destroy(pool.container.gameObject);
            }

            _poolMarkerCache.Clear();
            _pools.Clear();
            UpdateDiagnostics();
        }

        /// <summary>
        /// Releases half of inactive pooled instances under critical memory pressure while preserving pool registrations.
        /// </summary>
        public void FlushInactivePoolsForMemoryPressure()
        {
            TrimInactivePoolsForMemoryPressure(MemoryPressureInactiveTrimFraction);
        }

        /// <summary>
        /// Releases a bounded fraction of inactive pooled instances and trims queue backing storage.
        /// </summary>
        public void TrimInactivePoolsForMemoryPressure(float releaseFraction)
        {
            if (_pools == null)
                return;

            float safeFraction = Mathf.Clamp01(releaseFraction);
            Dictionary<int, Pool>.Enumerator enumerator = _pools.GetEnumerator();
            while (enumerator.MoveNext())
            {
                Pool pool = enumerator.Current.Value;
                if (pool.available == null || pool.available.Count == 0)
                    continue;

                int releaseCount = Mathf.CeilToInt(pool.available.Count * safeFraction);
                while (releaseCount > 0 && pool.available.Count > 0)
                {
                    releaseCount--;
                    GameObject instance = pool.available.Dequeue();
                    if (instance != null)
                    {
                        _poolMarkerCache.Remove(instance);
                        Destroy(instance);
                        pool.capacity = Mathf.Max(0, pool.capacity - 1);
                    }
                }

                pool.available.TrimExcess();
            }

            UpdateDiagnostics();
        }

        private void EnsurePoolDictionary()
        {
            if (_pools != null)
                return;

            // COLD ALLOC: Dictionary<int, Pool>[32] - prefab id to pool lookup - owner: ObjectPoolManager
            _pools = new Dictionary<int, Pool>(32);
        }

        private static bool TryGetPrefabRegistry(out PrefabRegistry registry)
        {
            registry = EnsurePrefabRegistry();
            return registry != null;
        }

        private static PrefabRegistry EnsurePrefabRegistry()
        {
            PrefabRegistry registry = null;
            if (PrefabRegistry.TryResolveActiveRuntime(ref registry))
                return registry;

            if (!Application.isPlaying)
                return null;

            // COLD ALLOC: GameObject[1] — runtime prefab registry bootstrap fallback — owner: ObjectPoolManager
            GameObject registryRoot = new GameObject(PrefabRegistryRuntimeName);
            return registryRoot.AddComponent<PrefabRegistry>();
        }

        private Pool CreatePool(GameObject prefab, int prefabId)
        {
            GameObject containerObject = new GameObject(PoolContainerRuntimeName);
            containerObject.transform.SetParent(transform, false);

            prefab.GetComponents(s_componentCache);
            int markerIdx = -1, rendererIdx = -1, rigidbodyIdx = -1, despawnTimerIdx = -1;
            s_poolableCache.Clear();
            List<int> poolableIdxList = new List<int>();
            for (int i = 0; i < s_componentCache.Count; i++)
            {
                Component c = s_componentCache[i];
                if (markerIdx == -1 && c is PoolItemMarker) markerIdx = i;
                else if (rendererIdx == -1 && c is Renderer) rendererIdx = i;
                else if (rigidbodyIdx == -1 && c is Rigidbody) rigidbodyIdx = i;
                else if (despawnTimerIdx == -1 && c is DespawnTimer) despawnTimerIdx = i;
                if (c is IPoolable) poolableIdxList.Add(i);
            }
            bool hasDynamicMarker = markerIdx == -1;
            if (hasDynamicMarker)
            {
                markerIdx = s_componentCache.Count; // It will be added at the end
            }
            s_poolableCache.Clear();
            s_componentCache.Clear();

            Pool pool = new Pool
            {
                // COLD ALLOC: Queue<GameObject>[32] — inactive pooled instances for one prefab — owner: ObjectPoolManager
                available = new Queue<GameObject>(32),
                container = containerObject.transform,
                prefab = prefab,
                prefabId = prefabId,
                capacity = 0,
                markerIndex = markerIdx,
                rendererIndex = rendererIdx,
                rigidbodyIndex = rigidbodyIdx,
                despawnTimerIndex = despawnTimerIdx,
                poolableIndices = poolableIdxList.ToArray(),
                hasDynamicMarker = hasDynamicMarker
            };

            _pools.Add(prefabId, pool);
            return pool;
        }

        private Pool PreparePool(GameObject prefab, PrefabRegistry registry)
        {
            int prefabId = registry.GetOrRegisterPrefab(prefab);
            if (!_pools.TryGetValue(prefabId, out Pool pool))
                pool = CreatePool(prefab, prefabId);

            return pool;
        }

        private int CountWarmupPresetInstances()
        {
            if (warmupPresets == null)
                return 0;

            int count = 0;
            for (int i = 0; i < warmupPresets.Length; i++)
            {
                WarmupEntry entry = warmupPresets[i];
                if (entry.prefab != null && entry.count > 0)
                    count += entry.count;
            }

            return count;
        }

        private GameObject InstantiatePooled(GameObject prefab, int prefabId, Pool pool)
        {
            GameObject instance = Instantiate(prefab, pool.container);
            instance.SetActive(false);

            instance.GetComponents(s_componentCache);

            PoolItemMarker marker;
            if (pool.hasDynamicMarker)
            {
                marker = instance.AddComponent<PoolItemMarker>();
                s_componentCache.Add(marker);
            }
            else
            {
                marker = (PoolItemMarker)s_componentCache[pool.markerIndex];
            }

            Renderer rootRenderer = pool.rendererIndex >= 0 ? (Renderer)s_componentCache[pool.rendererIndex] : null;
            Rigidbody rootRigidbody = pool.rigidbodyIndex >= 0 ? (Rigidbody)s_componentCache[pool.rigidbodyIndex] : null;
            DespawnTimer rootDespawnTimer = pool.despawnTimerIndex >= 0 ? (DespawnTimer)s_componentCache[pool.despawnTimerIndex] : null;

            s_poolableCache.Clear();
            for (int i = 0; i < pool.poolableIndices.Length; i++)
            {
                s_poolableCache.Add((IPoolable)s_componentCache[pool.poolableIndices[i]]);
            }

            marker.Initialize(this, prefabId, rootRenderer, rootRigidbody, rootDespawnTimer, s_poolableCache, s_componentCache);
            s_poolableCache.Clear();
            s_componentCache.Clear();
            _poolMarkerCache[instance] = marker;
            pool.capacity++;
            return instance;
        }

        private static void NotifySpawn(PoolItemMarker marker)
        {
            int count = marker.PoolableCount;
            for (int i = 0; i < count; i++)
            {
                IPoolable poolable = marker.GetPoolable(i);
                if (IsValidPoolable(poolable))
                    poolable.OnSpawn();
            }
        }

        private static void NotifyDespawn(PoolItemMarker marker)
        {
            int count = marker.PoolableCount;
            for (int i = 0; i < count; i++)
            {
                IPoolable poolable = marker.GetPoolable(i);
                if (IsValidPoolable(poolable))
                    poolable.OnDespawn();
            }
        }

        private static bool IsValidPoolable(IPoolable poolable)
        {
            if (poolable == null)
                return false;

            UnityEngine.Object unityObject = poolable as UnityEngine.Object;
            return unityObject == null ? !(poolable is UnityEngine.Object) : true;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugPoolCount = _pools.Count;
            int total = 0;
            Dictionary<int, Pool>.Enumerator enumerator = _pools.GetEnumerator();
            while (enumerator.MoveNext())
                total += enumerator.Current.Value.available.Count;
            _debugTotalPooled = total;
        }

        private static void WarnExpand(GameObject prefab, int prefabId, uint reasonCode, string reason)
        {
            if (Application.isPlaying)
                GlobalTelemetryBus.PublishPoolExhausted(prefabId, reasonCode);

            LogPoolExhausted(prefab, reason);
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogWarmupNullPrefab()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[ObjectPoolManager] Warmup: prefab is null!");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSpawnNullPrefab()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Hecton8.Core.H8Debug.LogError("[ObjectPoolManager] Spawn: prefab is null!");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogSpawnMissingMarker(GameObject instance)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string instanceName = instance != null ? instance.name : "<null>";
            Hecton8.Core.H8Debug.LogWarning(
                $"[ObjectPoolManager] Spawn: '{instanceName}' has no cached PoolItemMarker. Destroying instead.");
#endif
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR"), System.Diagnostics.Conditional("DEVELOPMENT_BUILD")]
        private static void LogPoolExhausted(GameObject prefab, string reason)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            string prefabName = prefab != null ? prefab.name : "NullPrefab";
            string report = $"[ObjectPoolManager] '{prefabName}': {reason} Consider increasing Warmup count.";
            RuntimeDiagnosticsTrace.WriteEvent("pool", report);
            Hecton8.Core.H8Debug.LogWarning(report);
#endif
        }

        /// <summary>
        /// Marker component attached to pooled instances so they can return to the correct pool.
        /// </summary>
        [DisallowMultipleComponent]
        [AddComponentMenu("")]
        public sealed class PoolItemMarker : MonoBehaviour
        {
            private ObjectPoolManager _owner;
            private int _prefabId;
            private bool _initialized;
            private Renderer _rootRenderer;
            private Rigidbody _rootRigidbody;
            private DespawnTimer _rootDespawnTimer;
            private IPoolable[] _poolables = Array.Empty<IPoolable>();
            private Component[] _rootComponents = Array.Empty<Component>();

            /// <summary>
            /// Registered prefab identifier.
            /// </summary>
            public int PrefabId => _prefabId;

            public ObjectPoolManager Owner => _owner;

            /// <summary>
            /// Root renderer captured during pool warmup/instantiation.
            /// </summary>
            public Renderer RootRenderer => _rootRenderer;

            public Rigidbody RootRigidbody => _rootRigidbody;

            /// <summary>
            /// Root delayed-despawn timer captured during pool warmup/instantiation.
            /// </summary>
            public DespawnTimer RootDespawnTimer => _rootDespawnTimer;

            public int PoolableCount => _poolables.Length;

            public IPoolable GetPoolable(int index)
            {
                return _poolables[index];
            }

            public bool TryGetPoolable<T>(out T poolable) where T : class
            {
                int count = _poolables.Length;
                for (int i = 0; i < count; i++)
                {
                    IPoolable candidate = _poolables[i];
                    if (!IsValidPoolable(candidate))
                        continue;

                    if (candidate is T typed)
                    {
                        poolable = typed;
                        return true;
                    }
                }

                poolable = null;
                return false;
            }

            public bool TryGetCachedComponent<T>(out T component) where T : class
            {
                int count = _rootComponents.Length;
                for (int i = 0; i < count; i++)
                {
                    Component candidate = _rootComponents[i];
                    if (candidate == null)
                        continue;

                    if (candidate is T typed)
                    {
                        component = typed;
                        return true;
                    }
                }

                component = null;
                return false;
            }

            /// <summary>
            /// Assigns the prefab id once for the pooled instance.
            /// </summary>
            public void Initialize(
                ObjectPoolManager owner,
                int prefabId,
                Renderer rootRenderer,
                Rigidbody rootRigidbody,
                DespawnTimer rootDespawnTimer,
                List<IPoolable> poolables,
                List<Component> rootComponents)
            {
                _owner = owner;

                if (!_initialized)
                {
                    _prefabId = prefabId;
                    _initialized = true;

                    int count = poolables != null ? poolables.Count : 0;
                    _poolables = count > 0 ? new IPoolable[count] : Array.Empty<IPoolable>();
                    for (int i = 0; i < count; i++)
                        _poolables[i] = poolables[i];

                    count = rootComponents != null ? rootComponents.Count : 0;
                    _rootComponents = count > 0 ? new Component[count] : Array.Empty<Component>();
                    for (int i = 0; i < count; i++)
                        _rootComponents[i] = rootComponents[i];
                }

                if (_rootRenderer == null)
                    _rootRenderer = rootRenderer;
                if (_rootRigidbody == null)
                    _rootRigidbody = rootRigidbody;
                if (_rootDespawnTimer == null)
                    _rootDespawnTimer = rootDespawnTimer;
            }
        }

        /// <summary>
        /// Delayed despawn timer implemented through <see cref="ITickable"/>.
        /// </summary>
        [DisallowMultipleComponent]
        [AddComponentMenu("")]
        public sealed class DespawnTimer : MonoBehaviour, ITickable, ILateFrameTickable, IGlobalRegistryHotSwapListener
        {
            private float _timer;
            private bool _active;
            private bool _pendingDespawn;
            private bool _registeredToTickManager;
            private bool _registeredToLateFrame;
            private bool _registeredHotSwap;

            /// <summary>
            /// Starts the countdown and registers the timer into the dispatcher.
            /// </summary>
            public void StartTimer(float delaySeconds)
            {
                if (delaySeconds <= 0f)
                {
                    DespawnNowOrDestroy();
                    return;
                }

                if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogError(
                        "[ObjectPoolManager] DespawnTimer requested before SystemDispatcher was ready. Falling back to immediate despawn.",
                        this);
#endif
                    DespawnNowOrDestroy();
                    return;
                }

                _timer = delaySeconds;
                _active = true;
                _pendingDespawn = false;

                TryRegisterHotSwapListener();
                TryRegisterLanes();
            }

            public void OnGlobalRegistryServiceReplaced(
                GlobalRegistryServiceSlot serviceSlot,
                object previousService,
                object currentService)
            {
                if (serviceSlot != GlobalRegistryServiceSlot.Dispatcher)
                    return;

                bool shouldRestore = _active || _pendingDespawn || _registeredToTickManager || _registeredToLateFrame;
                TryUnregisterLanes();
                if (shouldRestore && currentService != null && isActiveAndEnabled)
                    TryRegisterLanes();
            }

            /// <summary>
            /// Decrements the delayed despawn timer.
            /// </summary>
            public void Tick(float deltaTime)
            {
                if (!_active)
                    return;

                _timer -= deltaTime;
                if (_timer > 0f)
                    return;

                _active = false;
                _pendingDespawn = true;
            }

            public void LateFrameTick()
            {
                if (!_pendingDespawn)
                    return;

                _pendingDespawn = false;
                DespawnNowOrDestroy();
            }

            private void DespawnNowOrDestroy()
            {
                if (TryResolveOwningPool(out ObjectPoolManager pool))
                {
                    pool.Despawn(gameObject);
                    return;
                }

                Destroy(gameObject);
            }

            private bool TryResolveOwningPool(out ObjectPoolManager pool)
            {
                pool = null;
                if (TryGetComponent(out PoolItemMarker marker))
                {
                    ObjectPoolManager owner = marker.Owner;
                    if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(owner) &&
                        owner.CanDespawnWithoutDestroy(gameObject))
                    {
                        pool = owner;
                        return true;
                    }
                }

                ObjectPoolManager active = null;
                if (ObjectPoolManager.TryResolveActiveRuntime(ref active) &&
                    active.CanDespawnWithoutDestroy(gameObject))
                {
                    pool = active;
                    return true;
                }

                return false;
            }

            private void OnDisable()
            {
                _active = false;
                _pendingDespawn = false;
                TryUnregisterLanes();
                TryUnregisterHotSwapListener();
            }

            private void TryRegisterLanes()
            {
                if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                    return;

                if (!_registeredToTickManager)
                    _registeredToTickManager = GlobalRegistry.TryRegisterUpdatable(this, PriorityLayer.Core);
                if (!_registeredToLateFrame)
                    _registeredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Core);
            }

            private void TryUnregisterLanes()
            {
                if (_registeredToTickManager)
                {
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                    _registeredToTickManager = false;
                }

                if (_registeredToLateFrame)
                {
                    GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Core);
                    _registeredToLateFrame = false;
                }
            }

            private void TryRegisterHotSwapListener()
            {
                if (_registeredHotSwap || !Application.isPlaying)
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
        }
    }
}
