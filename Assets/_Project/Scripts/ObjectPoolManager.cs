using System.Collections.Generic;
using Hecton8.Dev;
using UnityEngine;

namespace Hecton8.Core
{
    /// <summary>
    /// Scene-owned runtime object pool service registered through <see cref="GlobalRegistry"/>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-9000)]
    public sealed class ObjectPoolManager : MonoBehaviour
    {
        private const string PrefabRegistryRuntimeName = "[PrefabRegistry]";
        private const uint PoolExhaustedReasonMissingPool = 1u;
        private const uint PoolExhaustedReasonExpandRejected = 2u;
        private const uint PoolExhaustedReasonEmptyPool = 3u;

        [Header("── Warmup Presets ────────────────────────────")]
        [Tooltip("Automatic warmup entries executed during Start.")]
        [SerializeField] private WarmupEntry[] warmupPresets;

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugPoolCount;
        [SerializeField] private int _debugTotalPooled;
        [SerializeField] private int _debugTotalExpands;

        // COLD ALLOC: List<IPoolable>[8] — pooled component dispatch scratch — owner: ObjectPoolManager
        private static readonly List<IPoolable> s_poolableCache = new List<IPoolable>(8);

        private Dictionary<int, Pool> _pools;
        private bool _serviceRegistered;

        /// <summary>
        /// Runtime pool service resolved through <see cref="GlobalRegistry"/>.
        /// </summary>
        public static ObjectPoolManager Instance => GlobalRegistry.ObjectPool;

        [System.Serializable]
        private struct WarmupEntry
        {
            [Tooltip("Prefab to prewarm into the runtime pool.")]
            public GameObject prefab;

            [Tooltip("How many inactive instances to allocate during warmup.")]
            public int count;
        }

        private sealed class Pool
        {
            public Queue<GameObject> available;
            public Transform container;
            public GameObject prefab;
            public int prefabId;
        }

        private void Awake()
        {
            EnsurePrefabRegistry();

            // COLD ALLOC: Dictionary<int, Pool>[32] — prefab id to pool lookup — owner: ObjectPoolManager
            _pools = new Dictionary<int, Pool>(32);
        }

        private void Start()
        {
            if (warmupPresets == null)
                return;

            for (int i = 0; i < warmupPresets.Length; i++)
            {
                WarmupEntry entry = warmupPresets[i];
                if (entry.prefab != null && entry.count > 0)
                    Warmup(entry.prefab, entry.count);
            }
        }

        private void OnDestroy()
        {
            if (_pools != null)
            {
                ClearAllPools();
                _pools = null;
            }

            if (_serviceRegistered && ReferenceEquals(GlobalRegistry.ObjectPool, this))
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

            GlobalRegistry.RegisterObjectPoolService(this);
            GlobalTelemetryBus.Initialize();
            _serviceRegistered = true;
        }

        /// <summary>
        /// Pre-allocates a pool for the given prefab.
        /// </summary>
        public void Warmup(GameObject prefab, int count)
        {
            if (prefab == null)
            {
                Debug.LogError("[ObjectPoolManager] Warmup: prefab is null!");
                return;
            }

            if (count <= 0 || !TryGetPrefabRegistry(out PrefabRegistry registry))
                return;

            int prefabId = registry.GetOrRegisterPrefab(prefab);
            if (!_pools.TryGetValue(prefabId, out Pool pool))
                pool = CreatePool(prefab, prefabId);

            for (int i = 0; i < count; i++)
            {
                GameObject instance = InstantiatePooled(prefab, prefabId, pool);
                instance.SetActive(false);
                pool.available.Enqueue(instance);
            }

            UpdateDiagnostics();
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
            if (prefab == null)
            {
                Debug.LogError("[ObjectPoolManager] Spawn: prefab is null!");
                return null;
            }

            if (!TryGetPrefabRegistry(out PrefabRegistry registry))
                return null;

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
                if (instance == null)
                    continue;

                Transform instanceTransform = instance.transform;
                instanceTransform.SetParent(null, false);
                instanceTransform.SetPositionAndRotation(position, rotation);
                instance.SetActive(true);
                NotifySpawn(instance);
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
        /// Returns an instance to its originating pool.
        /// </summary>
        public void Despawn(GameObject instance)
        {
            if (instance == null)
                return;

            if (!instance.TryGetComponent(out PoolItemMarker marker))
            {
                Debug.LogWarning(
                    $"[ObjectPoolManager] Despawn: '{instance.name}' has no PoolItemMarker. Destroying instead.");
                Destroy(instance);
                return;
            }

            int prefabId = marker.PrefabId;
            if (!_pools.TryGetValue(prefabId, out Pool pool))
            {
                Debug.LogWarning(
                    $"[ObjectPoolManager] Despawn: Pool for '{instance.name}' not found. Destroying.");
                Destroy(instance);
                return;
            }

            NotifyDespawn(instance);
            instance.SetActive(false);

            Transform instanceTransform = instance.transform;
            instanceTransform.SetParent(pool.container, false);
            instanceTransform.localPosition = Vector3.zero;
            instanceTransform.localRotation = Quaternion.identity;

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

            if (instance.TryGetComponent(out DespawnTimer timer))
            {
                timer.StartTimer(delaySeconds);
                return;
            }

            Debug.LogWarning(
                $"[ObjectPoolManager] Prefab '{instance.name}' is missing a DespawnTimer component. Despawning immediately.");
            Despawn(instance);
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
            if (prefab == null || !TryGetPrefabRegistry(out PrefabRegistry registry))
                return 0;

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

        /// <summary>
        /// Returns whether a pool already exists for the given prefab.
        /// </summary>
        public bool HasPool(GameObject prefab)
        {
            if (prefab == null || !TryGetPrefabRegistry(out PrefabRegistry registry))
                return false;

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
            if (prefab == null || !TryGetPrefabRegistry(out PrefabRegistry registry))
                return;

            int prefabId = registry.GetOrRegisterPrefab(prefab);
            if (!_pools.TryGetValue(prefabId, out Pool pool))
                return;

            while (pool.available.Count > 0)
            {
                GameObject instance = pool.available.Dequeue();
                if (instance != null)
                    Destroy(instance);
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
            foreach (KeyValuePair<int, Pool> pair in _pools)
            {
                Pool pool = pair.Value;
                while (pool.available.Count > 0)
                {
                    GameObject instance = pool.available.Dequeue();
                    if (instance != null)
                        Destroy(instance);
                }

                if (pool.container != null)
                    Destroy(pool.container.gameObject);
            }

            _pools.Clear();
            UpdateDiagnostics();
        }

        private static bool TryGetPrefabRegistry(out PrefabRegistry registry)
        {
            registry = EnsurePrefabRegistry();
            return registry != null;
        }

        private static PrefabRegistry EnsurePrefabRegistry()
        {
            PrefabRegistry registry = PrefabRegistry.Instance;
            if (registry != null)
                return registry;

            if (!Application.isPlaying)
                return null;

            // COLD ALLOC: GameObject[1] — runtime prefab registry bootstrap fallback — owner: ObjectPoolManager
            GameObject registryRoot = new GameObject(PrefabRegistryRuntimeName);
            return registryRoot.AddComponent<PrefabRegistry>();
        }

        private Pool CreatePool(GameObject prefab, int prefabId)
        {
            GameObject containerObject = new GameObject($"[Pool] {prefab.name}");
            containerObject.transform.SetParent(transform, false);

            Pool pool = new Pool
            {
                // COLD ALLOC: Queue<GameObject>[32] — inactive pooled instances for one prefab — owner: ObjectPoolManager
                available = new Queue<GameObject>(32),
                container = containerObject.transform,
                prefab = prefab,
                prefabId = prefabId
            };

            _pools.Add(prefabId, pool);
            return pool;
        }

        private GameObject InstantiatePooled(GameObject prefab, int prefabId, Pool pool)
        {
            GameObject instance = Instantiate(prefab, pool.container);
            instance.SetActive(false);

            if (!instance.TryGetComponent(out PoolItemMarker marker))
                marker = instance.AddComponent<PoolItemMarker>();

            marker.Initialize(prefabId);
            return instance;
        }

        private static void NotifySpawn(GameObject instance)
        {
            instance.GetComponents(s_poolableCache);
            int count = s_poolableCache.Count;
            for (int i = 0; i < count; i++)
                s_poolableCache[i].OnSpawn();
            s_poolableCache.Clear();
        }

        private static void NotifyDespawn(GameObject instance)
        {
            instance.GetComponents(s_poolableCache);
            int count = s_poolableCache.Count;
            for (int i = 0; i < count; i++)
                s_poolableCache[i].OnDespawn();
            s_poolableCache.Clear();
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

            string prefabName = prefab != null ? prefab.name : "NullPrefab";
            string report = $"[ObjectPoolManager] '{prefabName}': {reason} Consider increasing Warmup count.";
            RuntimeDiagnosticsTrace.WriteEvent("pool", report);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(report);
#endif
        }

        /// <summary>
        /// Marker component attached to pooled instances so they can return to the correct pool.
        /// </summary>
        [DisallowMultipleComponent]
        [AddComponentMenu("")]
        public sealed class PoolItemMarker : MonoBehaviour
        {
            private int _prefabId;
            private bool _initialized;

            /// <summary>
            /// Registered prefab identifier.
            /// </summary>
            public int PrefabId => _prefabId;

            /// <summary>
            /// Assigns the prefab id once for the pooled instance.
            /// </summary>
            public void Initialize(int prefabId)
            {
                if (_initialized)
                    return;

                _prefabId = prefabId;
                _initialized = true;
            }
        }

        /// <summary>
        /// Delayed despawn timer implemented through <see cref="ITickable"/>.
        /// </summary>
        [DisallowMultipleComponent]
        [AddComponentMenu("")]
        public sealed class DespawnTimer : MonoBehaviour, ITickable
        {
            private float _timer;
            private bool _active;
            private bool _registeredToTickManager;

            /// <summary>
            /// Starts the countdown and registers the timer into the dispatcher.
            /// </summary>
            public void StartTimer(float delaySeconds)
            {
                if (delaySeconds <= 0f)
                {
                    ObjectPoolManager pool = ObjectPoolManager.Instance;
                    if (pool != null)
                        pool.Despawn(gameObject);
                    return;
                }

                if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                {
                    ObjectPoolManager pool = ObjectPoolManager.Instance;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogError(
                        "[ObjectPoolManager] DespawnTimer requested before SystemDispatcher was ready. Falling back to immediate despawn.",
                        this);
#endif
                    if (pool != null)
                        pool.Despawn(gameObject);
                    return;
                }

                _timer = delaySeconds;
                _active = true;

                if (!_registeredToTickManager)
                {
                    GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Core);
                    _registeredToTickManager = true;
                }
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

                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool != null)
                    pool.Despawn(gameObject);
            }

            private void OnDisable()
            {
                _active = false;

                if (_registeredToTickManager)
                {
                    GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Core);
                    _registeredToTickManager = false;
                }
            }
        }
    }
}
