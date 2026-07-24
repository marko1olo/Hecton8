using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.World;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Hecton8.Modding
{
    /// <summary>
    /// Runtime owner for persistent mod-spawned world prefabs.
    /// Records are serialized into the official mod save dictionary and restored after bootstrap.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7900)]
    internal sealed class ModWorldPersistenceManager : MonoBehaviour, ISaveable, ISaveEventListener, IModRegistryEventListener, IGameBootstrapperEventListener, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const string SaveKey = "hecton.internal.mod_world_spawns";

        // COLD ALLOC: List<ModWorldSpawnRecord>[32] — persistent mod world spawn records — owner: ModWorldPersistenceManager
        private readonly List<ModWorldSpawnRecord> _records = new List<ModWorldSpawnRecord>(32);
        // COLD ALLOC: List<ModWorldSpawnRecord>[32] - load rollback records - owner: ModWorldPersistenceManager
        private readonly List<ModWorldSpawnRecord> _loadRollbackRecords = new List<ModWorldSpawnRecord>(32);
        // COLD ALLOC: Dictionary<uint,int>[32] — spawn hash to record index lookup — owner: ModWorldPersistenceManager
        private readonly Dictionary<uint, int> _recordIndexByHash = new Dictionary<uint, int>(32);
        // COLD ALLOC: Dictionary<uint,ModSpawnedEntity>[32] — live scene instances indexed by spawn hash — owner: ModWorldPersistenceManager
        private readonly Dictionary<uint, ModSpawnedEntity> _liveEntitiesByHash = new Dictionary<uint, ModSpawnedEntity>(32);

        private int _nextSpawnSequence = 1;
        private bool _saveRegistered;
        private bool _restorePending;
        private bool _loadApplyPending;
        private int _loadRollbackNextSpawnSequence = 1;
        private bool _serviceRegistered;
        private bool _serviceShuttingDown;
        private bool _serviceShutdownComplete;
        private bool _bootstrapListenerRegistered;
        private bool _modRegistryListenerRegistered;
        private bool _hotSwapRegistered;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private IObjectPoolService _objectPoolService;

        /// <summary>
        /// Save order for mod world payloads.
        /// </summary>
        public int SavePriority => 56;

        /// <summary>
        /// Load order for mod world payloads.
        /// </summary>
        public int LoadPriority => 56;

        public ServiceHeartbeatState HeartbeatState =>
            _serviceShuttingDown
                ? ServiceHeartbeatState.Shutdown
                : _serviceRegistered
                    ? ServiceHeartbeatState.Ready
                    : ServiceHeartbeatState.NotStarted;

        public bool IsServiceReady => _serviceRegistered && !_serviceShuttingDown;

        private void Awake()
        {
            Debug.Log($"[ModWorldPersistenceManager] Awake: this={this.GetHashCode()}, GlobalRegistry.ModWorldPersistence={GlobalRegistry.ModWorldPersistence?.GetHashCode()}");
            if (TryAbortForUsableExistingRuntime())
                return;

            InitializeService();
        }

        private void OnEnable()
        {
            Debug.Log($"[ModWorldPersistenceManager] OnEnable: this={this.GetHashCode()}, _serviceShuttingDown={_serviceShuttingDown}");
            if (_serviceShuttingDown)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            if (!_bootstrapListenerRegistered)
            {
                GameBootstrapper.Register(this);
                _bootstrapListenerRegistered = true;
            }

            SaveEvents.Register(this);
            TryRegisterModRegistryListener();
            InitializeService();
        }

        private void OnDisable()
        {
            Debug.Log($"[ModWorldPersistenceManager] OnDisable: this={this.GetHashCode()}, _serviceRegistered={_serviceRegistered}");
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_bootstrapListenerRegistered)
            {
                GameBootstrapper.Unregister(this);
                _bootstrapListenerRegistered = false;
            }

            SaveEvents.Unregister(this);
            TryUnregisterModRegistryListener();
            RollbackLoadApplyIfPending();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            if (_serviceRegistered && _serviceShuttingDown)
            {
                if (ReferenceEquals(GlobalRegistry.ModWorldPersistence, this))
                    GlobalRegistry.UnregisterModWorldPersistenceRuntime(this);

                _serviceRegistered = false;
            }
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void InitializeService()
        {
            Debug.Log($"[ModWorldPersistenceManager] InitializeService: this={this.GetHashCode()}, _serviceShuttingDown={_serviceShuttingDown}, GlobalRegistry.ModWorldPersistence={GlobalRegistry.ModWorldPersistence?.GetHashCode()}");
            if (_serviceShuttingDown || !Application.isPlaying)
                return;

            if (TryAbortForUsableExistingRuntime())
                return;

            if (!ReferenceEquals(GlobalRegistry.ModWorldPersistence, this))
                GlobalRegistry.RegisterModWorldPersistenceRuntime(this);

            _serviceRegistered = ReferenceEquals(GlobalRegistry.ModWorldPersistence, this);
            Debug.Log($"[ModWorldPersistenceManager] InitializeService finished: _serviceRegistered={_serviceRegistered}, GlobalRegistry.ModWorldPersistence={GlobalRegistry.ModWorldPersistence?.GetHashCode()}");
            TryRegisterHotSwapListener();
            RefreshColdRegistryDependencies();
            TryRegisterWithSaveManager();
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            ModWorldPersistenceManager registered = GlobalRegistry.ModWorldPersistence;
            Debug.Log($"[ModWorldPersistenceManager] TryAbortForUsableExistingRuntime: this={this.GetHashCode()}, registered={registered?.GetHashCode()}, ReferenceEquals(registered, this)={ReferenceEquals(registered, this)}");
            if (ReferenceEquals(registered, null) || ReferenceEquals(registered, this))
                return false;

            if (IsModWorldPersistenceRuntimeUsable(registered))
            {
                Debug.Log($"[ModWorldPersistenceManager] TryAbortForUsableExistingRuntime -> ABORTING (registered is usable)");
                Destroy(gameObject);
                return true;
            }

            Debug.Log($"[ModWorldPersistenceManager] TryAbortForUsableExistingRuntime -> registered is NOT usable, unregistering registered");
            GlobalRegistry.UnregisterModWorldPersistenceRuntime(registered);
            return false;
        }

        private static bool IsModWorldPersistenceRuntimeUsable(ModWorldPersistenceManager manager)
        {
            bool usable = manager != null &&
                   manager._serviceRegistered &&
                   !manager._serviceShuttingDown;
            Debug.Log($"[ModWorldPersistenceManager] IsModWorldPersistenceRuntimeUsable: manager={manager?.GetHashCode()}, usable={usable} (manager!=null={manager!=null}, _serviceRegistered={manager?._serviceRegistered}, !_serviceShuttingDown={manager != null && !manager._serviceShuttingDown})");
            return usable;
        }

        public void OnServiceShutdown()
        {
            Debug.Log($"[ModWorldPersistenceManager] OnServiceShutdown: this={this.GetHashCode()}");
            if (_serviceShutdownComplete)
                return;

            _serviceShuttingDown = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            if (_bootstrapListenerRegistered)
            {
                GameBootstrapper.Unregister(this);
                _bootstrapListenerRegistered = false;
            }

            SaveEvents.Unregister(this);
            TryUnregisterModRegistryListener();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            _records.Clear();
            _loadRollbackRecords.Clear();
            _recordIndexByHash.Clear();
            _liveEntitiesByHash.Clear();
            _restorePending = false;
            _loadApplyPending = false;
            _loadRollbackNextSpawnSequence = 1;
            _objectPoolService = null;
            if (_serviceRegistered)
            {
                if (ReferenceEquals(GlobalRegistry.ModWorldPersistence, this))
                    GlobalRegistry.UnregisterModWorldPersistenceRuntime(this);

                _serviceRegistered = false;
            }

            _serviceShutdownComplete = true;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (serviceSlot == GlobalRegistryServiceSlot.ObjectPool)
            {
                CacheObjectPoolService(currentService as ObjectPoolManager);
                IObjectPoolService pool = null;
                if (_restorePending &&
                    TryResolveCachedObjectPool(out pool) &&
                    isActiveAndEnabled &&
                    !_serviceShuttingDown)
                {
                    RestoreActiveSceneRecords();
                }

                return;
            }

            if (serviceSlot != GlobalRegistryServiceSlot.Save)
                return;

            UnregisterFromSaveManager();
            _saveService = currentService as ISaveService;

            if (Application.isPlaying && isActiveAndEnabled && !_serviceShuttingDown)
                TryRegisterWithSaveManager();
        }

        /// <summary>
        /// Spawns a persistent prefab instance owned by a mod package.
        /// </summary>
        internal GameObject SpawnPersistentPrefab(string modId, string assetName, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(assetName))
                return null;

            GameObject prefab = ModAssetManager.LoadPrefab(modId, assetName);
            if (prefab == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning($"[ModWorldPersistenceManager] Prefab '{assetName}' for mod '{modId}' could not be resolved.");
#endif
                return null;
            }

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning("[ModWorldPersistenceManager] ObjectPool runtime owner is unavailable. Persistent mod spawn was rejected.");
#endif
                return null;
            }

            if (!TryResolveAupFromRuntimeOrigin(position, out AbsoluteUniversePosition aup))
                return null;

            GameObject instance = pool.Spawn(prefab, position, rotation);
            if (instance == null)
                return null;

            string sceneName = SaveMetadata.NormalizeSceneName(SceneManager.GetActiveScene().name);
            string spawnId = BuildSpawnId(modId);
            uint spawnHash = ModCommandDispatcher.ComputeModHash(spawnId);
            uint sceneHash = ModCommandDispatcher.ComputeModHash(sceneName);

            ModWorldSpawnRecord record = new ModWorldSpawnRecord
            {
                SpawnId = spawnId,
                SpawnHash = spawnHash,
                ModId = modId,
                AssetName = assetName,
                SceneName = sceneName,
                SceneHash = sceneHash,
                Position = position,
                GridX = aup.GridX,
                GridY = aup.GridY,
                GridZ = aup.GridZ,
                LocalX = aup.LocalX,
                LocalY = aup.LocalY,
                LocalZ = aup.LocalZ,
                Rotation = rotation,
                Scale = instance.transform.localScale
            };

            AddOrReplaceRecord(record);

            ModSpawnedEntity marker = AttachMarker(instance, record);
            _liveEntitiesByHash[spawnHash] = marker;
            return instance;
        }

        /// <summary>
        /// Removes a previously spawned persistent mod instance from the save registry and despawns it through the pool owner.
        /// </summary>
        internal bool DespawnPersistentInstance(GameObject instance)
        {
            if (instance == null || !instance.TryGetComponent(out ModSpawnedEntity marker))
                return false;

            RemoveRecord(marker.SpawnId);
            _liveEntitiesByHash.Remove(marker.SpawnHash);

            TryResolveCachedObjectPool(out IObjectPoolService pool);
            if (pool != null)
                pool.Despawn(instance);
            else
                Destroy(instance);

            return true;
        }

        /// <summary>
        /// Writes the current record set into the official mod save dictionary.
        /// </summary>
        public void PopulateSaveData(SaveData data)
        {
            SyncLiveTransforms();

            ModWorldSavePayload payload = new ModWorldSavePayload
            {
                Records = _records,
                NextSpawnSequence = _nextSpawnSequence
            };

            ModSaveStateStore.SetEngineString(SaveKey, JsonUtility.ToJson(payload));
        }

        /// <summary>
        /// Restores the saved record set and defers actual world respawn until bootstrap announces readiness.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            CaptureLoadRollbackSnapshot();
            _records.Clear();
            _recordIndexByHash.Clear();
            _liveEntitiesByHash.Clear();
            _nextSpawnSequence = 1;
            _restorePending = false;

            string json = ModSaveStateStore.GetEngineString(SaveKey, string.Empty);
            if (string.IsNullOrWhiteSpace(json))
            {
                _restorePending = false;
                return;
            }

            ModWorldSavePayload payload;
            try
            {
                payload = JsonUtility.FromJson<ModWorldSavePayload>(json);
            }
            catch (Exception exception)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Hecton8.Core.H8Debug.LogWarning($"[ModWorldPersistenceManager] Failed to parse mod world payload: {exception.Message}");
#endif
                RollbackLoadApplyIfPending();
                return;
            }

            if (payload.Records != null)
            {
                for (int i = 0; i < payload.Records.Count; i++)
                {
                    ModWorldSpawnRecord record = payload.Records[i];
                    if (string.IsNullOrWhiteSpace(record.SpawnId) ||
                        string.IsNullOrWhiteSpace(record.ModId) ||
                        string.IsNullOrWhiteSpace(record.AssetName))
                    {
                        continue;
                    }

                    AddOrReplaceRecord(record);
                }
            }

            _nextSpawnSequence = Mathf.Max(1, payload.NextSpawnSequence);
            _restorePending = _records.Count > 0;
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _liveEntitiesByHash.Clear();
            _restorePending = _records.Count > 0;
            TryRegisterWithSaveManager();
            if (_restorePending && !_serviceShuttingDown && TryResolveCachedObjectPool(out IObjectPoolService pool))
                RestoreSceneRecords(SaveMetadata.NormalizeSceneName(scene.name));
        }

        public void OnSaveEvent(in SaveEventPayload payload)
        {
            switch (payload.Type)
            {
                case SaveEventType.LoadStarted:
                    _restorePending = false;
                    return;

                case SaveEventType.LoadCompleted:
                    CommitLoadApply();
                    _restorePending = _records.Count > 0;
                    return;

                case SaveEventType.LoadFailed:
                    RollbackLoadApplyIfPending();
                    _restorePending = false;
                    return;

                default:
                    return;
            }
        }

        public void OnGameBootstrapperEvent(in GameBootstrapperEventPayload payload)
        {
            if ((GameBootstrapperEventType)payload.EventType == GameBootstrapperEventType.GameReady)
                HandleGameReady();
        }

        public void OnModRegistryEvent(in ModRegistryEventPayload payload)
        {
            if ((ModRegistryEventType)payload.EventType != ModRegistryEventType.RuntimeRegistryChanged)
                return;

            if (!_restorePending || _serviceShuttingDown || !isActiveAndEnabled)
                return;

            RestoreActiveSceneRecords();
        }

        private void HandleGameReady()
        {
            TryRegisterWithSaveManager();

            if (!_restorePending)
                return;

            RestoreActiveSceneRecords();
        }

        private void RestoreActiveSceneRecords()
        {
            TryRegisterModRegistryListener();
            string activeSceneName = SaveMetadata.NormalizeSceneName(SceneManager.GetActiveScene().name);
            RestoreSceneRecords(activeSceneName);
        }

        private void RestoreSceneRecords(string sceneName)
        {
            string activeSceneName = SaveMetadata.NormalizeSceneName(sceneName);
            if (string.IsNullOrWhiteSpace(activeSceneName))
            {
                _restorePending = _records.Count > 0;
                return;
            }

            uint activeSceneHash = ModCommandDispatcher.ComputeModHash(activeSceneName);
            if (!TryResolveCachedObjectPool(out IObjectPoolService pool))
            {
                _restorePending = _records.Count > 0;
                return;
            }

            bool restoreStillPending = false;

            for (int i = 0; i < _records.Count; i++)
            {
                ModWorldSpawnRecord record = _records[i];
                EnsureSpatialFields(ref record);
                _records[i] = record;
                if (record.SceneHash != activeSceneHash)
                    continue;

                if (!string.Equals(record.SceneName, activeSceneName, StringComparison.Ordinal))
                    continue;

                if (_liveEntitiesByHash.TryGetValue(record.SpawnHash, out ModSpawnedEntity liveMarker))
                {
                    if (liveMarker != null)
                        continue;

                    _liveEntitiesByHash.Remove(record.SpawnHash);
                }

                GameObject prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);
                if (prefab == null)
                {
                    restoreStillPending = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Hecton8.Core.H8Debug.LogWarning(
                        $"[ModWorldPersistenceManager] Restore skipped '{record.SpawnId}': prefab '{record.AssetName}' for mod '{record.ModId}' was not found.");
#endif
                    continue;
                }

                GameObject instance = pool.Spawn(prefab, ResolveRuntimePosition(in record), record.Rotation);
                if (instance == null)
                {
                    restoreStillPending = true;
                    continue;
                }

                instance.transform.localScale = record.Scale;
                ModSpawnedEntity marker = AttachMarker(instance, record);
                _liveEntitiesByHash[record.SpawnHash] = marker;
            }

            _restorePending = restoreStillPending;
        }

        private void SyncLiveTransforms()
        {
            Dictionary<uint, ModSpawnedEntity>.Enumerator enumerator = _liveEntitiesByHash.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ModSpawnedEntity marker = enumerator.Current.Value;
                if (marker == null)
                    continue;

                if (!_recordIndexByHash.TryGetValue(marker.SpawnHash, out int index))
                    continue;

                Transform cachedTransform = marker.transform;
                Vector3 runtimePosition = cachedTransform.position;
                if (!TryResolveAupFromRuntimeOrigin(runtimePosition, out AbsoluteUniversePosition aup))
                    continue;

                ModWorldSpawnRecord record = _records[index];
                record.Position = runtimePosition;
                record.GridX = aup.GridX;
                record.GridY = aup.GridY;
                record.GridZ = aup.GridZ;
                record.LocalX = aup.LocalX;
                record.LocalY = aup.LocalY;
                record.LocalZ = aup.LocalZ;
                record.Rotation = cachedTransform.rotation;
                record.Scale = cachedTransform.localScale;
                _records[index] = record;
            }
        }

        private ModSpawnedEntity AttachMarker(GameObject instance, ModWorldSpawnRecord record)
        {
            if (!instance.TryGetComponent(out ModSpawnedEntity marker))
            {
                marker = instance.AddComponent<ModSpawnedEntity>(); // COLD ALLOC: Component[1] — marker for persistent mod world instance — owner: ModWorldPersistenceManager
            }

            marker.Initialize(record.SpawnId, record.SpawnHash, record.ModId, record.AssetName, record.SceneName);
            return marker;
        }

        private void AddOrReplaceRecord(ModWorldSpawnRecord record)
        {
            EnsureSpatialFields(ref record);
            if (_recordIndexByHash.TryGetValue(record.SpawnHash, out int existingIndex))
            {
                _records[existingIndex] = record;
                return;
            }

            _recordIndexByHash.Add(record.SpawnHash, _records.Count);
            _records.Add(record);
        }

        private void CaptureLoadRollbackSnapshot()
        {
            if (_loadApplyPending)
                return;

            _loadRollbackRecords.Clear();
            for (int i = 0; i < _records.Count; i++)
                _loadRollbackRecords.Add(_records[i]);

            _loadRollbackNextSpawnSequence = _nextSpawnSequence;
            _loadApplyPending = true;
        }

        private void CommitLoadApply()
        {
            _loadRollbackRecords.Clear();
            _loadRollbackNextSpawnSequence = 1;
            _loadApplyPending = false;
        }

        private void RollbackLoadApplyIfPending()
        {
            if (!_loadApplyPending)
                return;

            _records.Clear();
            _recordIndexByHash.Clear();
            for (int i = 0; i < _loadRollbackRecords.Count; i++)
                AddOrReplaceRecord(_loadRollbackRecords[i]);

            _nextSpawnSequence = Mathf.Max(1, _loadRollbackNextSpawnSequence);
            _loadRollbackRecords.Clear();
            _loadRollbackNextSpawnSequence = 1;
            _loadApplyPending = false;
            _restorePending = false;
            RebuildLiveEntityLookupFromScene();
        }

        private void RebuildLiveEntityLookupFromScene()
        {
            _liveEntitiesByHash.Clear();
            ModSpawnedEntity[] entities = UnityEngine.Object.FindObjectsByType<ModSpawnedEntity>(
                UnityEngine.FindObjectsInactive.Include);
            for (int i = 0; i < entities.Length; i++)
            {
                ModSpawnedEntity marker = entities[i];
                if (marker == null)
                    continue;

                if (_recordIndexByHash.ContainsKey(marker.SpawnHash))
                    _liveEntitiesByHash[marker.SpawnHash] = marker;
            }
        }

        private void RemoveRecord(string spawnId)
        {
            if (string.IsNullOrWhiteSpace(spawnId))
                return;

            uint spawnHash = ModCommandDispatcher.ComputeModHash(spawnId);
            if (!_recordIndexByHash.TryGetValue(spawnHash, out int index))
                return;

            int lastIndex = _records.Count - 1;
            if (index != lastIndex)
            {
                ModWorldSpawnRecord moved = _records[lastIndex];
                _records[index] = moved;
                _recordIndexByHash[moved.SpawnHash] = index;
            }

            _records.RemoveAt(lastIndex);
            _recordIndexByHash.Remove(spawnHash);
        }

        private string BuildSpawnId(string modId)
        {
            string spawnId = modId + ":" + _nextSpawnSequence.ToString();
            _nextSpawnSequence++;
            return spawnId;
        }

        private static void EnsureSpatialFields(ref ModWorldSpawnRecord record)
        {
            if (!string.IsNullOrWhiteSpace(record.SpawnId))
            {
                uint spawnHash = ModCommandDispatcher.ComputeModHash(record.SpawnId);
                if (record.SpawnHash != spawnHash)
                    record.SpawnHash = spawnHash;
            }

            record.SceneName = SaveMetadata.NormalizeSceneName(record.SceneName);
            uint sceneHash = ModCommandDispatcher.ComputeModHash(record.SceneName);
            if (record.SceneHash != sceneHash)
                record.SceneHash = sceneHash;

            if (record.GridX != 0L ||
                record.GridY != 0L ||
                record.GridZ != 0L ||
                !Mathf.Approximately(record.LocalX, 0f) ||
                !Mathf.Approximately(record.LocalY, 0f) ||
                !Mathf.Approximately(record.LocalZ, 0f))
            {
                return;
            }

            if (!TryResolveAupFromRuntimeOrigin(record.Position, out AbsoluteUniversePosition aup))
                return;

            record.GridX = aup.GridX;
            record.GridY = aup.GridY;
            record.GridZ = aup.GridZ;
            record.LocalX = aup.LocalX;
            record.LocalY = aup.LocalY;
            record.LocalZ = aup.LocalZ;
        }

        private static Vector3 ResolveRuntimePosition(in ModWorldSpawnRecord record)
        {
            AbsoluteUniversePosition aup = new AbsoluteUniversePosition
            {
                GridX = record.GridX,
                GridY = record.GridY,
                GridZ = record.GridZ,
                LocalX = record.LocalX,
                LocalY = record.LocalY,
                LocalZ = record.LocalZ
            };

            float3 runtime = aup.ToRuntimeFloat3();
            return new Vector3(runtime.x, runtime.y, runtime.z);
        }

        private static bool TryResolveAupFromRuntimeOrigin(Vector3 runtimePosition, out AbsoluteUniversePosition aup)
        {
            aup = default;
            if (!MathGuard.IsFinite(runtimePosition))
                return false;

            AbsoluteUniversePosition originAup = RuntimeOriginRoute.CurrentRuntimeOriginAup();
            if (!AbsoluteUniversePosition.IsFinite(in originAup))
                return false;

            aup = AbsoluteUniversePosition.OffsetMeters(
                in originAup,
                new double3(runtimePosition.x, runtimePosition.y, runtimePosition.z));
            return AbsoluteUniversePosition.IsFinite(in aup);
        }

        private void TryRegisterWithSaveManager()
        {
            if (_saveRegistered)
                return;

            ISaveService saveService = _saveService;
            if (!IsSaveServiceUsable(saveService))
            {
                saveService = GlobalRegistry.Save;
                _saveService = saveService;
            }

            if (!IsSaveServiceUsable(saveService))
                return;

            saveService.Register(this);
            _registeredSaveService = saveService;
            _saveRegistered = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_saveRegistered && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _saveRegistered = false;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void RefreshColdRegistryDependencies()
        {
            _saveService = GlobalRegistry.Save;
            CacheObjectPoolService(null);
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate))
            {
                _objectPoolService = candidate;
                return;
            }

            ObjectPoolManager pool = null;
            _objectPoolService = ObjectPoolManager.TryResolveActiveRuntime(ref pool)
                ? pool
                : null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPoolService as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = null;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPoolService = resolved;
                pool = resolved;
                return true;
            }

            _objectPoolService = null;
            pool = null;
            return false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_hotSwapRegistered || !Application.isPlaying)
                return;

            _hotSwapRegistered = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_hotSwapRegistered)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _hotSwapRegistered = false;
        }

        private void TryRegisterModRegistryListener()
        {
            if (_modRegistryListenerRegistered || !Application.isPlaying)
                return;

            _modRegistryListenerRegistered = ModRegistryEvents.Register(this);
        }

        private void TryUnregisterModRegistryListener()
        {
            if (!_modRegistryListenerRegistered)
                return;

            ModRegistryEvents.Unregister(this);
            _modRegistryListenerRegistered = false;
        }

        [Serializable]
        private struct ModWorldSpawnRecord
        {
            public string SpawnId;
            public uint SpawnHash;
            public string ModId;
            public string AssetName;
            public string SceneName;
            public uint SceneHash;
            public Vector3 Position;
            public long GridX;
            public long GridY;
            public long GridZ;
            public float LocalX;
            public float LocalY;
            public float LocalZ;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        [Serializable]
        private struct ModWorldSavePayload
        {
            public List<ModWorldSpawnRecord> Records;
            public int NextSpawnSequence;
        }
    }

    /// <summary>
    /// Marker stored on persistent mod-spawned instances so the manager can map live objects back to saved records.
    /// </summary>
    [DisallowMultipleComponent]
    internal sealed class ModSpawnedEntity : MonoBehaviour
    {
        /// <summary>
        /// Stable persistent spawn identifier.
        /// </summary>
        public string SpawnId { get; private set; }

        /// <summary>
        /// Stable FNV hash of the persistent spawn identifier.
        /// </summary>
        public uint SpawnHash { get; private set; }

        /// <summary>
        /// Owning mod identifier.
        /// </summary>
        public string ModId { get; private set; }

        /// <summary>
        /// Asset name used to restore this instance.
        /// </summary>
        public string AssetName { get; private set; }

        /// <summary>
        /// Scene that owns this persistent instance.
        /// </summary>
        public string SceneName { get; private set; }

        internal void Initialize(string spawnId, uint spawnHash, string modId, string assetName, string sceneName)
        {
            SpawnId = spawnId;
            SpawnHash = spawnHash;
            ModId = modId;
            AssetName = assetName;
            SceneName = SaveMetadata.NormalizeSceneName(sceneName);
        }
    }
}
