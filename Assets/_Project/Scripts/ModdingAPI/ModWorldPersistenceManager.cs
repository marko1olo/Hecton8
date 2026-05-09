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
    public sealed class ModWorldPersistenceManager : MonoBehaviour, ISaveable, ISaveEventListener, ISceneBootstrapEventListener, IServiceHeartbeat, IServiceShutdown
    {
        private const string SaveKey = "hecton.internal.mod_world_spawns";

        // COLD ALLOC: List<ModWorldSpawnRecord>[32] — persistent mod world spawn records — owner: ModWorldPersistenceManager
        private readonly List<ModWorldSpawnRecord> _records = new List<ModWorldSpawnRecord>(32);
        // COLD ALLOC: Dictionary<uint,int>[32] — spawn hash to record index lookup — owner: ModWorldPersistenceManager
        private readonly Dictionary<uint, int> _recordIndexByHash = new Dictionary<uint, int>(32);
        // COLD ALLOC: Dictionary<uint,ModSpawnedEntity>[32] — live scene instances indexed by spawn hash — owner: ModWorldPersistenceManager
        private readonly Dictionary<uint, ModSpawnedEntity> _liveEntitiesByHash = new Dictionary<uint, ModSpawnedEntity>(32);

        private int _nextSpawnSequence = 1;
        private bool _saveRegistered;
        private bool _restorePending;
        private bool _serviceRegistered;
        private bool _serviceShuttingDown;
        private bool _serviceShutdownComplete;

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
            ModWorldPersistenceManager registered = GlobalRegistry.ModWorldPersistence;
            if (registered != null && registered != this)
            {
                Destroy(gameObject);
                return;
            }

            InitializeService();
        }

        private void OnEnable()
        {
            if (_serviceShuttingDown)
                return;

            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneBootstrap.Register(this);
            SaveEvents.Register(this);
            InitializeService();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneBootstrap.Unregister(this);
            SaveEvents.Unregister(this);
            UnregisterFromSaveManager();
            if (_serviceRegistered && !_serviceShuttingDown)
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
            if (_serviceShuttingDown || !Application.isPlaying)
                return;

            if (!ReferenceEquals(GlobalRegistry.ModWorldPersistence, this))
                GlobalRegistry.RegisterModWorldPersistenceRuntime(this);

            _serviceRegistered = ReferenceEquals(GlobalRegistry.ModWorldPersistence, this);
            TryRegisterWithSaveManager();
        }

        public void OnServiceShutdown()
        {
            if (_serviceShutdownComplete)
                return;

            _serviceShuttingDown = true;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneBootstrap.Unregister(this);
            SaveEvents.Unregister(this);
            UnregisterFromSaveManager();
            _records.Clear();
            _recordIndexByHash.Clear();
            _liveEntitiesByHash.Clear();
            _restorePending = false;
            if (_serviceRegistered)
            {
                if (ReferenceEquals(GlobalRegistry.ModWorldPersistence, this))
                    GlobalRegistry.UnregisterModWorldPersistenceRuntime(this);

                _serviceRegistered = false;
            }

            _serviceShutdownComplete = true;
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
                Debug.LogWarning($"[ModWorldPersistenceManager] Prefab '{assetName}' for mod '{modId}' could not be resolved.");
#endif
                return null;
            }

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            if (pool == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.LogWarning("[ModWorldPersistenceManager] GlobalRegistry.ObjectPool is unavailable. Persistent mod spawn was rejected.");
#endif
                return null;
            }

            GameObject instance = pool.Spawn(prefab, position, rotation);
            if (instance == null)
                return null;

            string sceneName = SceneManager.GetActiveScene().name;
            string spawnId = BuildSpawnId(modId);
            uint spawnHash = ModCommandDispatcher.ComputeModHash(spawnId);
            uint sceneHash = ModCommandDispatcher.ComputeModHash(sceneName);
            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(position);

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

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
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

            ModSaveStateStore.SetModString(SaveKey, JsonUtility.ToJson(payload));
        }

        /// <summary>
        /// Restores the saved record set and defers actual world respawn until bootstrap announces readiness.
        /// </summary>
        public void LoadFromSaveData(SaveData data)
        {
            _records.Clear();
            _recordIndexByHash.Clear();
            _liveEntitiesByHash.Clear();
            _nextSpawnSequence = 1;

            string json = ModSaveStateStore.GetModString(SaveKey, string.Empty);
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
                Debug.LogWarning($"[ModWorldPersistenceManager] Failed to parse mod world payload: {exception.Message}");
#endif
                _restorePending = false;
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
            TryRegisterWithSaveManager();
        }

        public void OnSaveEvent(in SaveEventPayload payload)
        {
            if (payload.Type != SaveEventType.LoadCompleted)
                return;

            _restorePending = _records.Count > 0;
        }

        public void OnSceneBootstrapEvent(in SceneBootstrapEventPayload payload)
        {
            if ((SceneBootstrapEventType)payload.EventType == SceneBootstrapEventType.GameReady)
                HandleGameReady();
        }

        private void HandleGameReady()
        {
            if (!_restorePending)
                return;

            RestoreActiveSceneRecords();
        }

        private void RestoreActiveSceneRecords()
        {
            string activeSceneName = SceneManager.GetActiveScene().name;
            uint activeSceneHash = ModCommandDispatcher.ComputeModHash(activeSceneName);
            _restorePending = false;

            for (int i = 0; i < _records.Count; i++)
            {
                ModWorldSpawnRecord record = _records[i];
                EnsureSpatialFields(ref record);
                _records[i] = record;
                if (record.SceneHash != activeSceneHash)
                    continue;

                if (!string.Equals(record.SceneName, activeSceneName, StringComparison.Ordinal))
                    continue;

                if (_liveEntitiesByHash.ContainsKey(record.SpawnHash))
                    continue;

                GameObject prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);
                if (prefab == null)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning(
                        $"[ModWorldPersistenceManager] Restore skipped '{record.SpawnId}': prefab '{record.AssetName}' for mod '{record.ModId}' was not found.");
#endif
                    continue;
                }

                ObjectPoolManager pool = GlobalRegistry.ObjectPool;
                if (pool == null)
                    continue;

                GameObject instance = pool.Spawn(prefab, ResolveRuntimePosition(in record), record.Rotation);
                if (instance == null)
                    continue;

                instance.transform.localScale = record.Scale;
                ModSpawnedEntity marker = AttachMarker(instance, record);
                _liveEntitiesByHash[record.SpawnHash] = marker;
            }
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
                ModWorldSpawnRecord record = _records[index];
                record.Position = runtimePosition;
                AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(runtimePosition);
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
            if (record.SpawnHash == 0u && !string.IsNullOrWhiteSpace(record.SpawnId))
                record.SpawnHash = ModCommandDispatcher.ComputeModHash(record.SpawnId);

            if (record.SceneHash == 0u && !string.IsNullOrWhiteSpace(record.SceneName))
                record.SceneHash = ModCommandDispatcher.ComputeModHash(record.SceneName);

            if (record.GridX != 0L ||
                record.GridY != 0L ||
                record.GridZ != 0L ||
                !Mathf.Approximately(record.LocalX, 0f) ||
                !Mathf.Approximately(record.LocalY, 0f) ||
                !Mathf.Approximately(record.LocalZ, 0f))
            {
                return;
            }

            AbsoluteUniversePosition aup = AbsoluteUniversePosition.FromRuntimePosition(record.Position);
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

        private void TryRegisterWithSaveManager()
        {
            if (_saveRegistered || Hecton8.Core.GlobalRegistry.SaveRuntime == null)
                return;

            Hecton8.Core.GlobalRegistry.SaveRuntime.Register(this);
            _saveRegistered = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_saveRegistered || Hecton8.Core.GlobalRegistry.SaveRuntime == null)
                return;

            Hecton8.Core.GlobalRegistry.SaveRuntime.Unregister(this);
            _saveRegistered = false;
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
            SceneName = sceneName;
        }
    }
}
