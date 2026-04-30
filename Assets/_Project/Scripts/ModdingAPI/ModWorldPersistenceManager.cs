using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.SaveSystem;
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
    public sealed class ModWorldPersistenceManager : MonoBehaviour, ISaveable, ISaveEventListener, ISceneBootstrapEventListener
    {
        private const string ManagerRuntimeName = "[ModWorldPersistenceManager]";
        private const string SaveKey = "hecton.internal.mod_world_spawns";

        private static ModWorldPersistenceManager _instance;

        // COLD ALLOC: List<ModWorldSpawnRecord>[32] — persistent mod world spawn records — owner: ModWorldPersistenceManager
        private readonly List<ModWorldSpawnRecord> _records = new List<ModWorldSpawnRecord>(32);
        // COLD ALLOC: Dictionary<string,int>[32] — spawnId to record index lookup — owner: ModWorldPersistenceManager
        private readonly Dictionary<string, int> _recordIndexById = new Dictionary<string, int>(32);
        // COLD ALLOC: Dictionary<string,ModSpawnedEntity>[32] — live scene instances indexed by spawnId — owner: ModWorldPersistenceManager
        private readonly Dictionary<string, ModSpawnedEntity> _liveEntities = new Dictionary<string, ModSpawnedEntity>(32);

        private int _nextSpawnSequence = 1;
        private bool _saveRegistered;
        private bool _restorePending;

        /// <summary>
        /// Save order for mod world payloads.
        /// </summary>
        public int SavePriority => 56;

        /// <summary>
        /// Load order for mod world payloads.
        /// </summary>
        public int LoadPriority => 56;

        /// <summary>
        /// Active runtime singleton when the manager has been created.
        /// </summary>
        public static ModWorldPersistenceManager Instance => _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Ensures that the runtime persistence owner exists.
        /// </summary>
        public static ModWorldPersistenceManager EnsureRuntimeInstance()
        {
            if (_instance != null)
                return _instance;

            GameObject gameObject = new GameObject(ManagerRuntimeName); // COLD ALLOC: GameObject[1] — persistent mod world owner — owner: ModWorldPersistenceManager
            DontDestroyOnLoad(gameObject);
            _instance = gameObject.AddComponent<ModWorldPersistenceManager>();
            return _instance;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            TryRegisterWithSaveManager();
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += HandleSceneLoaded;
            SceneBootstrap.Register(this);
            SaveEvents.Register(this);
            TryRegisterWithSaveManager();
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneBootstrap.Unregister(this);
            SaveEvents.Unregister(this);
            UnregisterFromSaveManager();
        }

        private void OnDestroy()
        {
            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Spawns a persistent prefab instance owned by a mod package.
        /// </summary>
        public GameObject SpawnPersistentPrefab(string modId, string assetName, Vector3 position, Quaternion rotation)
        {
            if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(assetName))
                return null;

            GameObject prefab = ModAssetManager.LoadPrefab(modId, assetName);
            if (prefab == null)
            {
                Debug.LogWarning($"[ModWorldPersistenceManager] Prefab '{assetName}' for mod '{modId}' could not be resolved.");
                return null;
            }

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
            {
                Debug.LogWarning("[ModWorldPersistenceManager] ObjectPoolManager is unavailable. Persistent mod spawn was rejected.");
                return null;
            }

            GameObject instance = pool.Spawn(prefab, position, rotation);
            if (instance == null)
                return null;

            string sceneName = SceneManager.GetActiveScene().name;
            string spawnId = BuildSpawnId(modId);

            ModWorldSpawnRecord record = new ModWorldSpawnRecord
            {
                SpawnId = spawnId,
                ModId = modId,
                AssetName = assetName,
                SceneName = sceneName,
                Position = position,
                Rotation = rotation,
                Scale = instance.transform.localScale
            };

            AddOrReplaceRecord(record);

            ModSpawnedEntity marker = AttachMarker(instance, record);
            _liveEntities[spawnId] = marker;
            return instance;
        }

        /// <summary>
        /// Removes a previously spawned persistent mod instance from the save registry and despawns it through the pool owner.
        /// </summary>
        public bool DespawnPersistentInstance(GameObject instance)
        {
            if (instance == null || !instance.TryGetComponent(out ModSpawnedEntity marker))
                return false;

            RemoveRecord(marker.SpawnId);
            _liveEntities.Remove(marker.SpawnId);

            ObjectPoolManager pool = ObjectPoolManager.Instance;
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
                Records = _records.ToArray(),
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
            _recordIndexById.Clear();
            _liveEntities.Clear();
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
                Debug.LogWarning($"[ModWorldPersistenceManager] Failed to parse mod world payload: {exception.Message}");
                _restorePending = false;
                return;
            }

            if (payload.Records != null)
            {
                for (int i = 0; i < payload.Records.Length; i++)
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
            _liveEntities.Clear();
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
            _restorePending = false;

            for (int i = 0; i < _records.Count; i++)
            {
                ModWorldSpawnRecord record = _records[i];
                if (!string.Equals(record.SceneName, activeSceneName, StringComparison.Ordinal))
                    continue;

                if (_liveEntities.ContainsKey(record.SpawnId))
                    continue;

                GameObject prefab = ModAssetManager.LoadPrefab(record.ModId, record.AssetName);
                if (prefab == null)
                {
                    Debug.LogWarning(
                        $"[ModWorldPersistenceManager] Restore skipped '{record.SpawnId}': prefab '{record.AssetName}' for mod '{record.ModId}' was not found.");
                    continue;
                }

                ObjectPoolManager pool = ObjectPoolManager.Instance;
                if (pool == null)
                    continue;

                GameObject instance = pool.Spawn(prefab, record.Position, record.Rotation);
                if (instance == null)
                    continue;

                instance.transform.localScale = record.Scale;
                ModSpawnedEntity marker = AttachMarker(instance, record);
                _liveEntities[record.SpawnId] = marker;
            }
        }

        private void SyncLiveTransforms()
        {
            Dictionary<string, ModSpawnedEntity>.Enumerator enumerator = _liveEntities.GetEnumerator();
            while (enumerator.MoveNext())
            {
                ModSpawnedEntity marker = enumerator.Current.Value;
                if (marker == null)
                    continue;

                if (!_recordIndexById.TryGetValue(marker.SpawnId, out int index))
                    continue;

                Transform cachedTransform = marker.transform;
                ModWorldSpawnRecord record = _records[index];
                record.Position = cachedTransform.position;
                record.Rotation = cachedTransform.rotation;
                record.Scale = cachedTransform.localScale;
                _records[index] = record;
            }
        }

        private ModSpawnedEntity AttachMarker(GameObject instance, ModWorldSpawnRecord record)
        {
            ModSpawnedEntity marker = instance.GetComponent<ModSpawnedEntity>();
            if (marker == null)
            {
                marker = instance.AddComponent<ModSpawnedEntity>(); // COLD ALLOC: Component[1] — marker for persistent mod world instance — owner: ModWorldPersistenceManager
            }

            marker.Initialize(record.SpawnId, record.ModId, record.AssetName, record.SceneName);
            return marker;
        }

        private void AddOrReplaceRecord(ModWorldSpawnRecord record)
        {
            if (_recordIndexById.TryGetValue(record.SpawnId, out int existingIndex))
            {
                _records[existingIndex] = record;
                return;
            }

            _recordIndexById.Add(record.SpawnId, _records.Count);
            _records.Add(record);
        }

        private void RemoveRecord(string spawnId)
        {
            if (string.IsNullOrWhiteSpace(spawnId) || !_recordIndexById.TryGetValue(spawnId, out int index))
                return;

            int lastIndex = _records.Count - 1;
            if (index != lastIndex)
            {
                ModWorldSpawnRecord moved = _records[lastIndex];
                _records[index] = moved;
                _recordIndexById[moved.SpawnId] = index;
            }

            _records.RemoveAt(lastIndex);
            _recordIndexById.Remove(spawnId);
        }

        private string BuildSpawnId(string modId)
        {
            string spawnId = modId + ":" + _nextSpawnSequence.ToString();
            _nextSpawnSequence++;
            return spawnId;
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
            public string ModId;
            public string AssetName;
            public string SceneName;
            public Vector3 Position;
            public Quaternion Rotation;
            public Vector3 Scale;
        }

        [Serializable]
        private struct ModWorldSavePayload
        {
            public ModWorldSpawnRecord[] Records;
            public int NextSpawnSequence;
        }
    }
}

    /// <summary>
    /// Marker stored on persistent mod-spawned instances so the manager can map live objects back to saved records.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ModSpawnedEntity : MonoBehaviour
    {
        /// <summary>
        /// Stable persistent spawn identifier.
        /// </summary>
        public string SpawnId { get; private set; }

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

        internal void Initialize(string spawnId, string modId, string assetName, string sceneName)
        {
            SpawnId = spawnId;
            ModId = modId;
            AssetName = assetName;
            SceneName = sceneName;
        }
    }
