using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Interaction;
using Hecton8.SaveSystem;
using Hecton8.Scavenging;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.World
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-6000)]
    public sealed class WorldStateManager : MonoBehaviour, ISaveable
    {
        private struct PickupPersistenceEntry
        {
            public long Key;
            public long ChunkKey;
        }

        private static WorldStateManager _instance;
        private static readonly Comparison<PickupPersistenceEntry> PickupPersistenceEntryCompare = ComparePickupPersistenceEntries;

        [Header("── Settings ───────────────────────────────")]
        [Tooltip("Initial capacity for world-state persistence sets.")]
        [SerializeField] private int initialCapacity = 128;

        [Header("── Diagnostics ───────────────────────────")]
        [SerializeField] private int _debugDepletedCount;
        [SerializeField] private int _debugDepletedPickupCount;

        private HashSet<string> _depletedNodeIds;
        private HashSet<long> _depletedPickupKeys;
        private bool _serviceRegistered;

        // COLD ALLOC: Dictionary<long,long>[128] — pickup key-to-chunk lookup for save/load persistence — owner: WorldStateManager
        private readonly Dictionary<long, long> _depletedPickupChunkKeysByPickupKey = new Dictionary<long, long>(128);
        // COLD ALLOC: List<PickupPersistenceEntry>[512] — pickup persistence sort buffer for save/load packing — owner: WorldStateManager
        private readonly List<PickupPersistenceEntry> _pickupPersistenceEntries = new List<PickupPersistenceEntry>(512);
        // COLD ALLOC: List<long>[128] — packed pickup chunk keys during save — owner: WorldStateManager
        private readonly List<long> _packedPickupChunkKeys = new List<long>(128);
        // COLD ALLOC: List<int>[128] — packed pickup chunk word-start offsets during save — owner: WorldStateManager
        private readonly List<int> _packedPickupChunkWordStarts = new List<int>(128);
        // COLD ALLOC: List<int>[128] — packed pickup chunk word-counts during save — owner: WorldStateManager
        private readonly List<int> _packedPickupChunkWordCounts = new List<int>(128);
        // COLD ALLOC: List<long>[256] — packed pickup depletion words during save — owner: WorldStateManager
        private readonly List<long> _packedPickupWords = new List<long>(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            _instance = null;
        }

        /// <summary>
        /// Global world-state manager instance.
        /// </summary>
        public static WorldStateManager Instance
        {
            get
            {
#if UNITY_EDITOR
                if (_instance == null && !Application.isPlaying)
                    return null;
#endif
                return _instance;
            }
        }

        /// <summary>
        /// Total persisted depleted resource node count.
        /// </summary>
        public int DepletedCount => _depletedNodeIds != null ? _depletedNodeIds.Count : 0;

        /// <summary>
        /// Total persisted depleted authored pickup count.
        /// </summary>
        public int DepletedPickupCount => _depletedPickupKeys != null ? _depletedPickupKeys.Count : 0;

        /// <summary>
        /// Current player transform from bootstrap.
        /// </summary>
        public Transform PlayerTransform => SceneBootstrap.CurrentPlayerTransform;

        /// <summary>
        /// Save order for world-state persistence.
        /// </summary>
        public int SavePriority => 50;

        /// <summary>
        /// Load order for world-state persistence.
        /// </summary>
        public int LoadPriority => 50;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            GameBootstrapper.PersistRuntimeService(this);

            // COLD ALLOC: HashSet<string>[initialCapacity] — depleted node persistence set — owner: WorldStateManager
            _depletedNodeIds = new HashSet<string>(initialCapacity);
            // COLD ALLOC: HashSet<long>[initialCapacity] — depleted pickup persistence set — owner: WorldStateManager
            _depletedPickupKeys = new HashSet<long>(math.max(64, initialCapacity));
            _depletedPickupChunkKeysByPickupKey.Clear();
        }

        private void OnEnable()
        {
            TryRegisterService();
            GlobalRegistry.Save?.Register(this);
        }

        private void OnDisable()
        {
            GlobalRegistry.Save?.Unregister(this);
            TryUnregisterService();
        }

        private void OnDestroy()
        {
            TryUnregisterService();

            if (_instance == this)
                _instance = null;
        }

        /// <summary>
        /// Checks whether a resource node was already depleted.
        /// </summary>
        /// <param name="uniqueId">Stable resource node id.</param>
        /// <returns>true when the node is already depleted.</returns>
        public bool IsNodeDepleted(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId) || _depletedNodeIds == null)
                return false;

            return _depletedNodeIds.Contains(uniqueId);
        }

        /// <summary>
        /// Checks whether an authored pickup was already collected.
        /// </summary>
        /// <param name="persistenceKey">Stable pickup persistence key.</param>
        /// <returns>true when the pickup is already depleted.</returns>
        public bool IsPickupDepleted(long persistenceKey)
        {
            if (persistenceKey == 0L || _depletedPickupKeys == null)
                return false;

            return _depletedPickupKeys.Contains(persistenceKey);
        }

        /// <summary>
        /// Marks a resource node as depleted.
        /// </summary>
        /// <param name="uniqueId">Stable resource node id.</param>
        public void RegisterDepletedNode(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId) || _depletedNodeIds == null)
                return;

            _depletedNodeIds.Add(uniqueId);
            UpdateDiagnostics();
        }

        /// <summary>
        /// Alias for resource-node depletion registration.
        /// </summary>
        /// <param name="uniqueId">Stable resource node id.</param>
        public void MarkNodeDepleted(string uniqueId)
        {
            RegisterDepletedNode(uniqueId);
        }

        /// <summary>
        /// Marks an authored pickup as collected.
        /// </summary>
        /// <param name="persistenceKey">Stable pickup persistence key.</param>
        /// <param name="chunkKey">Stable world chunk key for grouping in save data.</param>
        public void RegisterCollectedPickup(long persistenceKey, long chunkKey)
        {
            if (persistenceKey == 0L || _depletedPickupKeys == null)
                return;

            _depletedPickupKeys.Add(persistenceKey);
            _depletedPickupChunkKeysByPickupKey[persistenceKey] = chunkKey != 0L ? chunkKey : persistenceKey;
            UpdateDiagnostics();
        }

        /// <summary>
        /// Backward-compatible pickup registration when only persistence key is available.
        /// </summary>
        /// <param name="persistenceKey">Stable pickup persistence key.</param>
        public void RegisterCollectedPickup(long persistenceKey)
        {
            RegisterCollectedPickup(persistenceKey, persistenceKey);
        }

        /// <summary>
        /// Removes a resource node from depleted persistence.
        /// </summary>
        /// <param name="uniqueId">Stable resource node id.</param>
        public void UnregisterDepletedNode(string uniqueId)
        {
            if (string.IsNullOrEmpty(uniqueId) || _depletedNodeIds == null)
                return;

            _depletedNodeIds.Remove(uniqueId);
            UpdateDiagnostics();
        }

        /// <summary>
        /// Clears all persisted world depletion state.
        /// </summary>
        public void ClearAll()
        {
            _depletedNodeIds?.Clear();
            _depletedPickupKeys?.Clear();
            _depletedPickupChunkKeysByPickupKey.Clear();
            UpdateDiagnostics();
        }

        /// <summary>
        /// Writes world-state persistence into save data.
        /// </summary>
        /// <param name="data">Save container to populate.</param>
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            ref WorldStateDTO dto = ref data.worldState;
            dto.EnsureCapacity();

            int nodeIndex = 0;
            foreach (string id in _depletedNodeIds)
            {
                if (nodeIndex >= WorldStateDTO.MaxNodes)
                {
                    Debug.LogWarning(
                        $"[WorldStateManager] Max depleted nodes ({WorldStateDTO.MaxNodes}) reached. " +
                        $"Truncating: {_depletedNodeIds.Count - nodeIndex} nodes not saved.");
                    break;
                }

                dto.depletedNodeIds[nodeIndex] = id;
                nodeIndex++;
            }

            dto.depletedCount = nodeIndex;
            PopulatePackedPickupState(ref dto);
        }

        /// <summary>
        /// Restores world-state persistence from save data.
        /// </summary>
        /// <param name="data">Save container to read.</param>
        public void LoadFromSaveData(SaveData data)
        {
            if (data == null)
                return;

            WorldStateDTO dto = data.worldState;

            _depletedNodeIds.Clear();
            _depletedPickupKeys.Clear();
            _depletedPickupChunkKeysByPickupKey.Clear();

            if (dto.depletedNodeIds != null && dto.depletedCount > 0)
            {
                int nodeCount = math.min(dto.depletedCount, dto.depletedNodeIds.Length);
                for (int i = 0; i < nodeCount; i++)
                {
                    string id = dto.depletedNodeIds[i];
                    if (!string.IsNullOrEmpty(id))
                        _depletedNodeIds.Add(id);
                }
            }

            LoadPackedPickupState(in dto);
            ApplyToScene();
            UpdateDiagnostics();

            Debug.Log(
                $"[WorldStateManager] Loaded {_depletedNodeIds.Count} depleted nodes and {_depletedPickupKeys.Count} depleted pickups.");
        }

        /// <summary>
        /// Applies persisted depletion state to the currently loaded scene.
        /// </summary>
        public void ApplyToScene()
        {
            if (_depletedNodeIds.Count == 0 && (_depletedPickupKeys == null || _depletedPickupKeys.Count == 0))
                return;

            int deactivatedNodes = 0;

            int nodeCount = ResourceNode.WorldStateRegistryCount;
            for (int i = 0; i < nodeCount; i++)
            {
                ResourceNode node = ResourceNode.GetWorldStateRegistryAt(i);
                if (node == null)
                    continue;

                if (node.TryGetComponent<ObjectPoolManager.PoolItemMarker>(out _))
                    continue;

                string nodeId = node.UniqueId;
                if (string.IsNullOrEmpty(nodeId))
                    continue;

                if (_depletedNodeIds.Contains(nodeId))
                {
                    if (node.gameObject.activeSelf)
                    {
                        node.gameObject.SetActive(false);
                        deactivatedNodes++;
                    }
                }
                else if (!node.gameObject.activeSelf)
                {
                    node.gameObject.SetActive(true);
                }
            }

            if (deactivatedNodes > 0)
            {
                Debug.Log(
                    $"[WorldStateManager] Deactivated {deactivatedNodes} depleted nodes in scene.");
            }

            ApplyPickupStateToScene();
        }

        private static int ComparePickupPersistenceEntries(PickupPersistenceEntry left, PickupPersistenceEntry right)
        {
            int chunkCompare = left.ChunkKey.CompareTo(right.ChunkKey);
            if (chunkCompare != 0)
                return chunkCompare;

            return left.Key.CompareTo(right.Key);
        }

        private void TryRegisterService()
        {
            if (_serviceRegistered || !Application.isPlaying)
                return;

            GlobalRegistry.RegisterWorldStateRuntime(this);
            _serviceRegistered = true;
        }

        private void TryUnregisterService()
        {
            if (!_serviceRegistered)
                return;

            GlobalRegistry.UnregisterWorldStateRuntime(this);
            _serviceRegistered = false;
        }

        private void PopulatePackedPickupState(ref WorldStateDTO dto)
        {
            _pickupPersistenceEntries.Clear();
            _packedPickupChunkKeys.Clear();
            _packedPickupChunkWordStarts.Clear();
            _packedPickupChunkWordCounts.Clear();
            _packedPickupWords.Clear();

            if (_depletedPickupKeys == null || _depletedPickupKeys.Count == 0)
            {
                dto.depletedPickupChunkCount = 0;
                dto.depletedPickupWordCount = 0;
                return;
            }

            foreach (long pickupKey in _depletedPickupKeys)
            {
                long chunkKey;
                if (!_depletedPickupChunkKeysByPickupKey.TryGetValue(pickupKey, out chunkKey) || chunkKey == 0L)
                    chunkKey = pickupKey;

                _pickupPersistenceEntries.Add(new PickupPersistenceEntry
                {
                    Key = pickupKey,
                    ChunkKey = chunkKey
                });
            }

            _pickupPersistenceEntries.Sort(PickupPersistenceEntryCompare);

            bool hasActiveChunk = false;
            long activeChunkKey = 0L;
            int activeChunkCount = 0;

            for (int i = 0; i < _pickupPersistenceEntries.Count; i++)
            {
                if (_packedPickupWords.Count >= WorldStateDTO.MaxPickupWords)
                {
                    Debug.LogWarning(
                        $"[WorldStateManager] Max depleted pickup words ({WorldStateDTO.MaxPickupWords}) reached. " +
                        $"Truncating: {_pickupPersistenceEntries.Count - i} pickups not saved.");
                    break;
                }

                PickupPersistenceEntry entry = _pickupPersistenceEntries[i];
                if (!hasActiveChunk || entry.ChunkKey != activeChunkKey)
                {
                    if (_packedPickupChunkKeys.Count >= WorldStateDTO.MaxPickupChunks)
                    {
                        Debug.LogWarning(
                            $"[WorldStateManager] Max depleted pickup chunks ({WorldStateDTO.MaxPickupChunks}) reached. " +
                            "Truncating remaining pickup persistence.");
                        break;
                    }

                    if (hasActiveChunk)
                        _packedPickupChunkWordCounts.Add(activeChunkCount);

                    hasActiveChunk = true;
                    activeChunkKey = entry.ChunkKey;
                    activeChunkCount = 0;

                    _packedPickupChunkKeys.Add(activeChunkKey);
                    _packedPickupChunkWordStarts.Add(_packedPickupWords.Count);
                }

                _packedPickupWords.Add(entry.Key);
                activeChunkCount++;
            }

            if (hasActiveChunk)
                _packedPickupChunkWordCounts.Add(activeChunkCount);

            int chunkCount = _packedPickupChunkKeys.Count;
            for (int i = 0; i < chunkCount; i++)
            {
                dto.depletedPickupChunkKeys[i] = _packedPickupChunkKeys[i];
                dto.depletedPickupChunkWordStarts[i] = _packedPickupChunkWordStarts[i];
                dto.depletedPickupChunkWordCounts[i] = _packedPickupChunkWordCounts[i];
            }

            int pickupWordCount = _packedPickupWords.Count;
            for (int i = 0; i < pickupWordCount; i++)
                dto.depletedPickupWords[i] = _packedPickupWords[i];

            dto.depletedPickupChunkCount = chunkCount;
            dto.depletedPickupWordCount = pickupWordCount;
        }

        private void LoadPackedPickupState(in WorldStateDTO dto)
        {
            if (dto.depletedPickupWords == null || dto.depletedPickupWordCount <= 0)
                return;

            int wordCount = math.min(dto.depletedPickupWordCount, dto.depletedPickupWords.Length);
            int chunkCount = dto.depletedPickupChunkKeys != null
                ? math.min(dto.depletedPickupChunkCount, dto.depletedPickupChunkKeys.Length)
                : 0;

            if (chunkCount > 0 &&
                dto.depletedPickupChunkWordStarts != null &&
                dto.depletedPickupChunkWordCounts != null)
            {
                chunkCount = math.min(chunkCount, math.min(dto.depletedPickupChunkWordStarts.Length, dto.depletedPickupChunkWordCounts.Length));

                for (int chunkIndex = 0; chunkIndex < chunkCount; chunkIndex++)
                {
                    long chunkKey = dto.depletedPickupChunkKeys[chunkIndex];
                    int wordStart = math.clamp(dto.depletedPickupChunkWordStarts[chunkIndex], 0, wordCount);
                    int availableWordCount = wordCount - wordStart;
                    int chunkWordCount = math.clamp(dto.depletedPickupChunkWordCounts[chunkIndex], 0, availableWordCount);

                    for (int wordIndex = 0; wordIndex < chunkWordCount; wordIndex++)
                    {
                        long pickupKey = dto.depletedPickupWords[wordStart + wordIndex];
                        if (pickupKey == 0L)
                            continue;

                        _depletedPickupKeys.Add(pickupKey);
                        _depletedPickupChunkKeysByPickupKey[pickupKey] = chunkKey != 0L ? chunkKey : pickupKey;
                    }
                }

                return;
            }

            for (int wordIndex = 0; wordIndex < wordCount; wordIndex++)
            {
                long pickupKey = dto.depletedPickupWords[wordIndex];
                if (pickupKey == 0L)
                    continue;

                _depletedPickupKeys.Add(pickupKey);
                _depletedPickupChunkKeysByPickupKey[pickupKey] = pickupKey;
            }
        }

        private void ApplyPickupStateToScene()
        {
            if (_depletedPickupKeys == null || _depletedPickupKeys.Count == 0)
                return;

            int deactivatedPickups = 0;

            int pickupCount = PickupItem.WorldStateRegistryCount;
            for (int i = 0; i < pickupCount; i++)
            {
                PickupItem pickup = PickupItem.GetWorldStateRegistryAt(i);
                if (pickup == null)
                    continue;

                if (pickup.TryGetComponent<ObjectPoolManager.PoolItemMarker>(out _))
                    continue;

                long persistenceKey;
                long chunkKey;
                if (!pickup.TryGetWorldStatePersistenceIdentity(out persistenceKey, out chunkKey))
                    continue;

                if (!_depletedPickupKeys.Contains(persistenceKey))
                    continue;

                _depletedPickupChunkKeysByPickupKey[persistenceKey] = chunkKey != 0L ? chunkKey : persistenceKey;

                if (!pickup.gameObject.activeSelf)
                    continue;

                pickup.gameObject.SetActive(false);
                deactivatedPickups++;
            }

            if (deactivatedPickups > 0)
            {
                Debug.Log(
                    $"[WorldStateManager] Deactivated {deactivatedPickups} depleted pickups in scene.");
            }
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugDepletedCount = _depletedNodeIds != null ? _depletedNodeIds.Count : 0;
            _debugDepletedPickupCount = _depletedPickupKeys != null ? _depletedPickupKeys.Count : 0;
        }
    }
}
