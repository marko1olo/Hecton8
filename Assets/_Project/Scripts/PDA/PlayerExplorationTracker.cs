using System;
using System.Collections.Generic;
using Hecton8.Bootstrap;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.SaveSystem;
using UnityEngine;

namespace Hecton8.PDA
{
    /// <summary>
    /// Tracks player movement across a sparse world-space chunk grid for PDA map reveal and fog-of-war queries.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/PDA/Player Exploration Tracker")]
    public sealed class PlayerExplorationTracker : MonoBehaviour, ITickable, ISaveable
    {
        [Header("References")]
        [Tooltip("Optional explicit player transform. When empty, the tracker resolves the current bootstrap player.")]
        [SerializeField] private Transform playerTransform;

        [Header("Exploration Grid")]
        [Tooltip("World size, in meters, represented by one explored PDA chunk.")]
        [SerializeField, Min(4f)] private float chunkWorldSize = 32f;
        [Tooltip("Minimum movement distance before the tracker re-evaluates chunk membership.")]
        [SerializeField, Min(0.25f)] private float movementSampleDistance = 4f;
        [Tooltip("When enabled, biome changes from MapMagic automatically feed the discovery registry.")]
        [SerializeField] private bool forwardBiomeDiscovery = true;

        // COLD ALLOC: HashSet<long>[dynamic] - sparse explored chunk registry - owner: PlayerExplorationTracker
        private readonly HashSet<long> _exploredChunkKeys = new HashSet<long>();
        private bool _registeredToTick;
        private bool _registeredToSave;
        private Vector3 _lastSampledPosition;
        private long _lastChunkKey = long.MinValue;

        /// <summary>Live singleton instance for PDA map systems.</summary>
        public static PlayerExplorationTracker Instance { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Instance = null;
        }

        /// <summary>Raised when a previously unexplored PDA chunk becomes visible.</summary>
        public event Action<Vector2Int> ChunkExplored;

        /// <summary>Total explored chunk count currently held in memory.</summary>
        public int ExploredChunkCount => _exploredChunkKeys.Count;

        /// <summary>World-space size represented by one persisted exploration chunk.</summary>
        public float ChunkWorldSize => chunkWorldSize;

        /// <inheritdoc />
        public int SavePriority => 21;

        /// <inheritdoc />
        public int LoadPriority => 21;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }

            Instance = this;
            chunkWorldSize = Mathf.Max(4f, chunkWorldSize);
            movementSampleDistance = Mathf.Max(0.25f, movementSampleDistance);
        }

        private void OnEnable()
        {
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            MapMagicBridge.OnBiomeChanged += HandleBiomeChanged;
            ResolvePlayerTransform(force: true);
        }

        private void Start()
        {
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            ResolvePlayerTransform(force: true);
            SampleCurrentChunk(force: true);
        }

        private void OnDisable()
        {
            MapMagicBridge.OnBiomeChanged -= HandleBiomeChanged;
            UnregisterFromTickManager();
            UnregisterFromSaveManager();

            if (Instance == this)
                Instance = null;
        }

        private void OnDestroy()
        {
            MapMagicBridge.OnBiomeChanged -= HandleBiomeChanged;
            UnregisterFromTickManager();
            UnregisterFromSaveManager();

            if (Instance == this)
                Instance = null;
        }

        /// <inheritdoc />
        public void Tick(float deltaTime)
        {
            if (!ResolvePlayerTransform(force: false))
                return;

            Vector3 currentPosition = playerTransform.position;
            float requiredDistance = movementSampleDistance;
            if ((currentPosition - _lastSampledPosition).sqrMagnitude < requiredDistance * requiredDistance)
                return;

            _lastSampledPosition = currentPosition;
            SampleCurrentChunk(force: false);
        }

        /// <summary>
        /// Returns true when the requested PDA chunk has already been explored in the current save.
        /// </summary>
        public bool IsChunkExplored(Vector2Int chunkCoordinates)
        {
            return _exploredChunkKeys.Contains(PDAKeyUtility.PackChunkKey(chunkCoordinates.x, chunkCoordinates.y));
        }

        /// <summary>
        /// Returns true when the requested PDA chunk has already been explored in the current save.
        /// </summary>
        public bool IsChunkExplored(int chunkX, int chunkY)
        {
            return _exploredChunkKeys.Contains(PDAKeyUtility.PackChunkKey(chunkX, chunkY));
        }

        /// <summary>
        /// Converts a world-space position into PDA exploration chunk coordinates.
        /// </summary>
        public bool TryWorldToChunk(Vector3 worldPosition, out Vector2Int chunkCoordinates)
        {
            float safeChunkSize = Mathf.Max(4f, chunkWorldSize);
            chunkCoordinates = new Vector2Int(
                Mathf.FloorToInt(worldPosition.x / safeChunkSize),
                Mathf.FloorToInt(worldPosition.z / safeChunkSize));
            return true;
        }

        /// <summary>
        /// Copies explored chunk coordinates into a caller-owned buffer.
        /// </summary>
        public int CopyExploredChunks(Vector2Int[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _exploredChunkKeys.Count == 0)
                return 0;

            int count = 0;
            HashSet<long>.Enumerator enumerator = _exploredChunkKeys.GetEnumerator();
            while (enumerator.MoveNext() && count < buffer.Length)
            {
                buffer[count] = PDAKeyUtility.UnpackChunkKey(enumerator.Current);
                count++;
            }

            return count;
        }

        internal int CopyExploredChunkKeys(long[] buffer)
        {
            if (buffer == null || buffer.Length == 0 || _exploredChunkKeys.Count == 0)
                return 0;

            int count = 0;
            HashSet<long>.Enumerator enumerator = _exploredChunkKeys.GetEnumerator();
            while (enumerator.MoveNext() && count < buffer.Length)
            {
                buffer[count] = enumerator.Current;
                count++;
            }

            return count;
        }

        /// <summary>
        /// Marks a chunk as explored. Repeated calls are ignored.
        /// </summary>
        public bool MarkChunkExplored(Vector2Int chunkCoordinates)
        {
            long chunkKey = PDAKeyUtility.PackChunkKey(chunkCoordinates.x, chunkCoordinates.y);
            if (!_exploredChunkKeys.Add(chunkKey))
                return false;

            _lastChunkKey = chunkKey;
            ChunkExplored?.Invoke(chunkCoordinates);
            return true;
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.explorationMap.EnsureCapacity();

            int writeCount = 0;
            HashSet<long>.Enumerator enumerator = _exploredChunkKeys.GetEnumerator();
            while (enumerator.MoveNext() && writeCount < ExplorationMapDTO.MaxExploredChunks)
            {
                data.explorationMap.exploredChunkKeys[writeCount] = enumerator.Current;
                writeCount++;
            }

            data.explorationMap.exploredChunkCount = writeCount;
            for (int i = writeCount; i < ExplorationMapDTO.MaxExploredChunks; i++)
                data.explorationMap.exploredChunkKeys[i] = 0L;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            _exploredChunkKeys.Clear();
            _lastChunkKey = long.MinValue;

            if (data == null)
                return;

            ExplorationMapDTO dto = data.explorationMap;
            int count = Mathf.Clamp(dto.exploredChunkCount, 0, dto.exploredChunkKeys != null ? dto.exploredChunkKeys.Length : 0);
            for (int i = 0; i < count; i++)
                _exploredChunkKeys.Add(dto.exploredChunkKeys[i]);

            SampleCurrentChunk(force: true);
        }

        private void SampleCurrentChunk(bool force)
        {
            if (!ResolvePlayerTransform(force: false))
                return;

            if (!TryWorldToChunk(playerTransform.position, out Vector2Int currentChunk))
                return;

            long currentChunkKey = PDAKeyUtility.PackChunkKey(currentChunk.x, currentChunk.y);
            if (!force && currentChunkKey == _lastChunkKey)
                return;

            _lastChunkKey = currentChunkKey;
            MarkChunkExplored(currentChunk);
        }

        private bool ResolvePlayerTransform(bool force)
        {
            if (!force && playerTransform != null)
                return true;

            if (SceneBootstrap.TryGetCurrentPlayerTransform(out Transform bootstrapPlayerTransform) && bootstrapPlayerTransform != null)
            {
                playerTransform = bootstrapPlayerTransform;
                _lastSampledPosition = bootstrapPlayerTransform.position;
                return true;
            }

            return playerTransform != null;
        }

        private void HandleBiomeChanged(int biomeId)
        {
            if (!forwardBiomeDiscovery || biomeId <= 0)
                return;

            HectonDiscoveryManager discoveryManager = HectonDiscoveryManager.Instance;
            if (discoveryManager != null)
                discoveryManager.DiscoverBiome(biomeId);
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick)
                return;


            GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Player);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Player);

            _registeredToTick = false;
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave)
                return;

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager == null)
                return;

            saveManager.Register(this);
            _registeredToSave = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave)
                return;

            SaveManager saveManager = SaveManager.Instance;
            if (saveManager != null)
                saveManager.Unregister(this);

            _registeredToSave = false;
        }
    }
}
