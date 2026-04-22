using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.PDA;
using Hecton8.SaveSystem;
using Hecton8.UI;
using UnityEngine;

namespace Hecton8.Narrative
{
    /// <summary>
    /// Pushes a small number of undiscovered lore pickups toward the explored frontier so the world keeps feeding narrative leads.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Hecton8/Narrative/Procedural Lore Director")]
    public sealed class ProceduralLoreDirector : MonoBehaviour, ISlowTickable, ISaveable
    {
        private struct ActiveLorePlacement
        {
            public string discoveryId;
            public string logId;
            public long chunkKey;
            public Vector3 position;
            public GameObject instance;
        }

        private static readonly Vector2Int[] _neighborOffsets =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        [Header("Spawn Cadence")]
        [Tooltip("Seconds between frontier spawn evaluations. Heavy catalog and chunk scans stay on a cold cadence.")]
        [SerializeField, Min(30f)] private float spawnCheckIntervalSeconds = 180f;
        [Tooltip("Hard cap on simultaneously active procedural lore drops.")]
        [SerializeField, Range(1, ProceduralLoreStateDTO.MaxActivePlacements)] private int maxActiveDrops = 4;

        [Header("Placement")]
        [Tooltip("Additional lateral spread applied inside the target chunk so drops do not stack on the exact chunk center.")]
        [SerializeField, Min(0f)] private float chunkPlacementRadius = 8f;

        // COLD ALLOC: List<ActiveLorePlacement>[12] - active frontier lore placements - owner: ProceduralLoreDirector
        private readonly List<ActiveLorePlacement> _activePlacements = new List<ActiveLorePlacement>(ProceduralLoreStateDTO.MaxActivePlacements);
        // COLD ALLOC: HashSet<long>[12] - occupied frontier chunk keys - owner: ProceduralLoreDirector
        private readonly HashSet<long> _occupiedChunkKeys = new HashSet<long>();
        // COLD ALLOC: Vector2Int[ExplorationMapDTO.MaxExploredChunks] - exploration snapshot buffer - owner: ProceduralLoreDirector
        private readonly Vector2Int[] _exploredChunkBuffer = new Vector2Int[ExplorationMapDTO.MaxExploredChunks];
        // COLD ALLOC: AudioLogData[256] - PDA archive catalog snapshot - owner: ProceduralLoreDirector
        private readonly AudioLogData[] _catalogBuffer = new AudioLogData[256];

        private PlayerExplorationTracker _explorationTracker;
        private AudioLogSystem _audioLogSystem;
        private AudioLogPickup _pickupTemplate;
        private float _spawnCheckTimer;
        private int _catalogCount;
        private int _nextCatalogIndex;
        private int _nextChunkScanIndex;
        private bool _registeredToTick;
        private bool _registeredToSave;
        private bool _poolWarmed;
        private bool _needsRespawn;

        /// <inheritdoc />
        public int SavePriority => 208;

        /// <inheritdoc />
        public int LoadPriority => 208;

        private void OnEnable()
        {
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            ResolveOwners();
            _needsRespawn = true;
        }

        private void Start()
        {
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            ResolveOwners();
            _needsRespawn = true;
        }

        private void OnDisable()
        {
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
            DespawnAllInstances();
        }

        private void OnDestroy()
        {
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
            DespawnAllInstances();
        }

        /// <inheritdoc />
        public void SlowTick()
        {
            ResolveOwners();
            RefreshActivePlacements();

            if (_needsRespawn)
                TryRespawnMissingInstances();

            _spawnCheckTimer += 0.5f;
            if (_spawnCheckTimer < spawnCheckIntervalSeconds)
                return;

            _spawnCheckTimer = 0f;
            TrySpawnFrontierLore();
        }

        /// <inheritdoc />
        public void PopulateSaveData(SaveData data)
        {
            if (data == null)
                return;

            data.proceduralLore.EnsureCapacity();
            data.proceduralLore.activeCount = Mathf.Min(_activePlacements.Count, ProceduralLoreStateDTO.MaxActivePlacements);
            data.proceduralLore.nextSourceIndex = Mathf.Max(0, _nextCatalogIndex);

            for (int i = 0; i < data.proceduralLore.activeCount; i++)
            {
                ActiveLorePlacement placement = _activePlacements[i];
                data.proceduralLore.activePlacements[i] = new ProceduralLorePlacementDTO
                {
                    discoveryId = placement.discoveryId,
                    logId = placement.logId,
                    chunkKey = placement.chunkKey
                };
                data.proceduralLore.activePlacements[i].SetPosition(placement.position);
            }

            for (int i = data.proceduralLore.activeCount; i < ProceduralLoreStateDTO.MaxActivePlacements; i++)
                data.proceduralLore.activePlacements[i] = default;
        }

        /// <inheritdoc />
        public void LoadFromSaveData(SaveData data)
        {
            DespawnAllInstances();
            _activePlacements.Clear();
            _occupiedChunkKeys.Clear();
            _spawnCheckTimer = 0f;
            _needsRespawn = false;

            if (data == null)
                return;

            _nextCatalogIndex = Mathf.Max(0, data.proceduralLore.nextSourceIndex);
            int activeCount = Mathf.Clamp(data.proceduralLore.activeCount, 0, data.proceduralLore.activePlacements != null ? data.proceduralLore.activePlacements.Length : 0);
            for (int i = 0; i < activeCount; i++)
            {
                ProceduralLorePlacementDTO dto = data.proceduralLore.activePlacements[i];
                if (string.IsNullOrWhiteSpace(dto.logId))
                    continue;

                ActiveLorePlacement placement = new ActiveLorePlacement
                {
                    discoveryId = string.IsNullOrWhiteSpace(dto.discoveryId) ? dto.logId : dto.discoveryId,
                    logId = dto.logId,
                    chunkKey = dto.chunkKey,
                    position = dto.GetPosition(),
                    instance = null
                };

                _activePlacements.Add(placement);
                _occupiedChunkKeys.Add(placement.chunkKey);
            }

            _needsRespawn = _activePlacements.Count > 0;
        }

        private void TrySpawnFrontierLore()
        {
            if (_activePlacements.Count >= Mathf.Clamp(maxActiveDrops, 1, ProceduralLoreStateDTO.MaxActivePlacements))
                return;

            if (!ResolveCatalog() || !ResolvePickupTemplate() || _explorationTracker == null)
                return;

            if (!TrySelectFrontierChunk(out Vector2Int frontierChunk))
                return;

            if (!TrySelectLoreEntry(out AudioLogData logData))
                return;

            long chunkKey = PDAKeyUtility.PackChunkKey(frontierChunk.x, frontierChunk.y);
            Vector3 spawnPosition = BuildSpawnPosition(frontierChunk, chunkKey);
            ActiveLorePlacement placement = new ActiveLorePlacement
            {
                discoveryId = logData.SafeLogId,
                logId = logData.SafeLogId,
                chunkKey = chunkKey,
                position = spawnPosition,
                instance = null
            };

            if (!TrySpawnInstance(ref placement))
                return;

            _activePlacements.Add(placement);
            _occupiedChunkKeys.Add(chunkKey);
            NotificationEvents.PushInfo("PDA archive anomaly detected near the frontier. Route updated with a probable data lead.");
        }

        private void RefreshActivePlacements()
        {
            for (int i = _activePlacements.Count - 1; i >= 0; i--)
            {
                ActiveLorePlacement placement = _activePlacements[i];
                bool discovered = _audioLogSystem != null && _audioLogSystem.IsDiscovered(placement.logId);

                if (discovered)
                {
                    DespawnInstance(ref placement);
                    _activePlacements.RemoveAt(i);
                    _occupiedChunkKeys.Remove(placement.chunkKey);
                    continue;
                }

                if (placement.instance == null || !placement.instance.activeInHierarchy)
                {
                    placement.instance = null;
                    _activePlacements[i] = placement;
                    _needsRespawn = true;
                }
            }
        }

        private void TryRespawnMissingInstances()
        {
            if (!ResolveCatalog() || !ResolvePickupTemplate())
                return;

            bool stillMissingInstance = false;
            for (int i = 0; i < _activePlacements.Count; i++)
            {
                ActiveLorePlacement placement = _activePlacements[i];
                if (placement.instance != null)
                    continue;

                if (TrySpawnInstance(ref placement))
                {
                    _activePlacements[i] = placement;
                }
                else
                {
                    stillMissingInstance = true;
                }
            }

            _needsRespawn = stillMissingInstance;
        }

        private bool TrySpawnInstance(ref ActiveLorePlacement placement)
        {
            if (_pickupTemplate == null || _audioLogSystem == null || ObjectPoolManager.Instance == null)
                return false;

            AudioLogData logData = FindCatalogEntry(placement.logId);
            if (logData == null)
                return false;

            if (!_poolWarmed)
            {
                ObjectPoolManager.Instance.Warmup(_pickupTemplate.gameObject, Mathf.Clamp(maxActiveDrops, 1, ProceduralLoreStateDTO.MaxActivePlacements));
                _poolWarmed = true;
            }

            GameObject spawnedObject = ObjectPoolManager.Instance.Spawn(_pickupTemplate.gameObject, placement.position, Quaternion.identity);
            if (spawnedObject == null)
                return false;

            AudioLogPickup pickup = spawnedObject.GetComponent<AudioLogPickup>();
            if (pickup == null)
            {
                ObjectPoolManager.Instance.Despawn(spawnedObject);
                return false;
            }

            pickup.ConfigureRecoveryPickup(logData, true);
            placement.instance = spawnedObject;
            return true;
        }

        private void DespawnAllInstances()
        {
            for (int i = 0; i < _activePlacements.Count; i++)
            {
                ActiveLorePlacement placement = _activePlacements[i];
                DespawnInstance(ref placement);
                _activePlacements[i] = placement;
            }
        }

        private static void DespawnInstance(ref ActiveLorePlacement placement)
        {
            if (placement.instance == null)
                return;

            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Despawn(placement.instance);
            else
                placement.instance.SetActive(false);

            placement.instance = null;
        }

        private bool ResolveOwners()
        {
            if (_explorationTracker == null)
                _explorationTracker = PlayerExplorationTracker.Instance;

            if (_audioLogSystem == null)
                _audioLogSystem = AudioLogSystem.Instance;

            return _explorationTracker != null && _audioLogSystem != null;
        }

        private bool ResolveCatalog()
        {
            if (_catalogCount > 0)
                return true;

            PDADataLogTab[] tabs = UnityEngine.Object.FindObjectsByType<PDADataLogTab>(FindObjectsInactive.Include);
            if (tabs == null || tabs.Length == 0)
                return false;

            for (int i = 0; i < tabs.Length; i++)
            {
                PDADataLogTab tab = tabs[i];
                if (tab == null)
                    continue;

                _catalogCount = tab.CopyCatalog(_catalogBuffer);
                if (_catalogCount > 0)
                    return true;
            }

            return false;
        }

        private bool ResolvePickupTemplate()
        {
            if (_pickupTemplate != null)
                return true;

            AudioLogPickup[] pickups = UnityEngine.Object.FindObjectsByType<AudioLogPickup>(FindObjectsInactive.Include);
            if (pickups == null || pickups.Length == 0)
                return false;

            for (int i = 0; i < pickups.Length; i++)
            {
                AudioLogPickup pickup = pickups[i];
                if (pickup == null || pickup.gameObject == null)
                    continue;

                _pickupTemplate = pickup;
                return true;
            }

            return false;
        }

        private bool TrySelectFrontierChunk(out Vector2Int frontierChunk)
        {
            frontierChunk = default;
            if (_explorationTracker == null)
                return false;

            int exploredCount = _explorationTracker.CopyExploredChunks(_exploredChunkBuffer);
            if (exploredCount <= 0)
                return false;

            int startIndex = _nextChunkScanIndex % exploredCount;
            for (int i = 0; i < exploredCount; i++)
            {
                int scanIndex = (startIndex + i) % exploredCount;
                Vector2Int exploredChunk = _exploredChunkBuffer[scanIndex];

                for (int neighborIndex = 0; neighborIndex < _neighborOffsets.Length; neighborIndex++)
                {
                    Vector2Int candidate = exploredChunk + _neighborOffsets[neighborIndex];
                    long candidateKey = PDAKeyUtility.PackChunkKey(candidate.x, candidate.y);
                    if (_explorationTracker.IsChunkExplored(candidate) || _occupiedChunkKeys.Contains(candidateKey))
                        continue;

                    _nextChunkScanIndex = scanIndex + 1;
                    frontierChunk = candidate;
                    return true;
                }
            }

            return false;
        }

        private bool TrySelectLoreEntry(out AudioLogData logData)
        {
            logData = null;
            if (_catalogCount <= 0)
                return false;

            int startIndex = _nextCatalogIndex % _catalogCount;
            for (int i = 0; i < _catalogCount; i++)
            {
                int candidateIndex = (startIndex + i) % _catalogCount;
                AudioLogData candidate = _catalogBuffer[candidateIndex];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.SafeLogId))
                    continue;

                if (_audioLogSystem != null && _audioLogSystem.IsDiscovered(candidate.SafeLogId))
                    continue;

                if (IsAlreadyActive(candidate.SafeLogId))
                    continue;

                _nextCatalogIndex = candidateIndex + 1;
                logData = candidate;
                return true;
            }

            return false;
        }

        private bool IsAlreadyActive(string logId)
        {
            for (int i = 0; i < _activePlacements.Count; i++)
            {
                if (string.Equals(_activePlacements[i].logId, logId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private AudioLogData FindCatalogEntry(string logId)
        {
            if (string.IsNullOrWhiteSpace(logId) || _catalogCount <= 0)
                return null;

            for (int i = 0; i < _catalogCount; i++)
            {
                AudioLogData candidate = _catalogBuffer[i];
                if (candidate == null)
                    continue;

                if (string.Equals(candidate.SafeLogId, logId, StringComparison.Ordinal))
                    return candidate;
            }

            return null;
        }

        private Vector3 BuildSpawnPosition(Vector2Int chunkCoordinates, long chunkKey)
        {
            float chunkSize = _explorationTracker != null ? Mathf.Max(4f, _explorationTracker.ChunkWorldSize) : 32f;
            Vector3 basePosition = new Vector3(
                (chunkCoordinates.x + 0.5f) * chunkSize,
                transform.position.y,
                (chunkCoordinates.y + 0.5f) * chunkSize);

            uint hash = unchecked((uint)(chunkKey ^ (chunkKey >> 32)));
            float angle = (hash & 1023u) / 1024f * Mathf.PI * 2f;
            float radius = Mathf.Min(chunkPlacementRadius, chunkSize * 0.35f) * (((hash >> 10) & 1023u) / 1023f);
            basePosition.x += Mathf.Cos(angle) * radius;
            basePosition.z += Mathf.Sin(angle) * radius;
            return basePosition;
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager == null)
                return;

            tickManager.Register(this);
            _registeredToTick = true;
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

            GameTickManager tickManager = GameTickManager.Instance;
            if (tickManager != null)
                tickManager.Unregister(this);

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
