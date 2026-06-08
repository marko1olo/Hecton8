using System;
using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.PDA;
using Hecton8.Quest;
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
    public sealed class ProceduralLoreDirector : MonoBehaviour, ISlowTickable, ISaveable, IGlobalRegistryHotSwapListener
    {
        private struct ActiveLorePlacement
        {
            public string discoveryId;
            public string logId;
            public uint logHash;
            public long chunkKey;
            public Vector3 position;
            public GameObject instance;
            public IObjectPoolService owningPool;
        }

        private static readonly Vector2Int[] _neighborOffsets =
        {
            new Vector2Int(1, 0),
            new Vector2Int(-1, 0),
            new Vector2Int(0, 1),
            new Vector2Int(0, -1)
        };

        private const int InstalledDirectorCapacity = 8;
        private static readonly GameObject[] s_installedOwners = new GameObject[InstalledDirectorCapacity];
        private static readonly ProceduralLoreDirector[] s_installedInstances = new ProceduralLoreDirector[InstalledDirectorCapacity];
        private static int s_installedCount;
        private static readonly uint s_frontierLoreNotificationMissWarningHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralLoreDirector.FrontierNotificationMiss"));
        private static readonly uint s_proceduralLoreDirectorContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralLoreDirector"));
        private static readonly uint s_frontierLoreNotificationContextHash =
            unchecked((uint)Hecton.Localization.LocHash.Compute("ProceduralLoreDirector.FrontierNotification"));

        [Header("Spawn Cadence")]
        [Tooltip("Seconds between frontier spawn evaluations. Heavy catalog and chunk scans stay on a cold cadence.")]
        [SerializeField, Min(30f)] private float spawnCheckIntervalSeconds = 180f;
        [Tooltip("Hard cap on simultaneously active procedural lore drops.")]
        [SerializeField, Range(1, ProceduralLoreStateDTO.MaxActivePlacements)] private int maxActiveDrops = 4;

        [Header("Placement")]
        [Tooltip("Additional lateral spread applied inside the target chunk so drops do not stack on the exact chunk center.")]
        [SerializeField, Min(0f)] private float chunkPlacementRadius = 8f;

        [Header("Runtime References")]
        [Tooltip("Preferred PDA data-log catalog source. When unset, active PDA catalog registries are used.")]
        [SerializeField] private PDADataLogTab catalogSource;
        [Tooltip("Preferred audio-log pickup template for procedural frontier drops. When unset, active pickup registries are used.")]
        [SerializeField] private AudioLogPickup pickupTemplate;

        // COLD ALLOC: List<ActiveLorePlacement>[12] - active frontier lore placements - owner: ProceduralLoreDirector
        private readonly List<ActiveLorePlacement> _activePlacements = new List<ActiveLorePlacement>(ProceduralLoreStateDTO.MaxActivePlacements);
        // COLD ALLOC: HashSet<long>[12] - occupied frontier chunk keys - owner: ProceduralLoreDirector
        private readonly HashSet<long> _occupiedChunkKeys = new HashSet<long>(ProceduralLoreStateDTO.MaxActivePlacements);
        // COLD ALLOC: long[ExplorationMapDTO.MaxExploredChunks] - exploration chunk key snapshot buffer - owner: ProceduralLoreDirector
        private readonly long[] _exploredChunkKeyBuffer = new long[ExplorationMapDTO.MaxExploredChunks];
        // COLD ALLOC: HashSet<long>[ExplorationMapDTO.MaxExploredChunks] - explored frontier membership cache - owner: ProceduralLoreDirector
        private readonly HashSet<long> _exploredChunkKeys = new HashSet<long>(ExplorationMapDTO.MaxExploredChunks);
        // COLD ALLOC: AudioLogData[256] - PDA archive catalog snapshot - owner: ProceduralLoreDirector
        private readonly AudioLogData[] _catalogBuffer = new AudioLogData[256];

        private IPlayerExplorationChunkReadModel _explorationTracker;
        private AudioLogSystem _audioLogSystem;
        private IObjectPoolService _objectPool;
        private ISaveService _saveService;
        private ISaveService _registeredSaveService;
        private float _spawnCheckTimer;
        private int _catalogCount;
        private int _nextCatalogIndex;
        private int _nextChunkScanIndex;
        private bool _registeredToTick;
        private bool _registeredToSave;
        private bool _registeredHotSwapListener;
        private bool _poolWarmed;
        private bool _needsRespawn;
        private int _frontierLoreNotificationMissCount;
        /// <inheritdoc />
        public int SavePriority => 208;

        /// <inheritdoc />
        public int LoadPriority => 208;
        public int FrontierLoreNotificationMissCount => _frontierLoreNotificationMissCount;

        private void OnEnable()
        {
            RegisterInstalledOwner();
            TryRegisterHotSwapListener();
            RefreshCachedOwners();
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            _needsRespawn = true;
        }

        private void Start()
        {
            RegisterInstalledOwner();
            TryRegisterHotSwapListener();
            RefreshCachedOwners();
            TryRegisterWithTickManager();
            TryRegisterWithSaveManager();
            _needsRespawn = true;
        }

        private void OnDisable()
        {
            UnregisterInstalledOwner();
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            DespawnAllInstances();
            ClearFrontierLoreNotificationDiagnostics();
            ClearCachedRuntimeServices();
        }

        private void OnDestroy()
        {
            UnregisterInstalledOwner();
            UnregisterFromTickManager();
            UnregisterFromSaveManager();
            TryUnregisterHotSwapListener();
            DespawnAllInstances();
            ClearFrontierLoreNotificationDiagnostics();
            ClearCachedRuntimeServices();
        }

        internal static bool IsInstalledOn(GameObject owner)
        {
            if (owner == null)
                return false;

            for (int i = 0; i < s_installedCount; i++)
            {
                if (ReferenceEquals(s_installedOwners[i], owner))
                    return true;
            }

            return false;
        }

        private void RegisterInstalledOwner()
        {
            GameObject owner = gameObject;
            for (int i = 0; i < s_installedCount; i++)
            {
                if (ReferenceEquals(s_installedInstances[i], this))
                {
                    s_installedOwners[i] = owner;
                    return;
                }

                if (ReferenceEquals(s_installedOwners[i], owner))
                {
                    s_installedInstances[i] = this;
                    return;
                }
            }

            if (s_installedCount >= InstalledDirectorCapacity)
            {
                H8Debug.LogWarning("[ProceduralLoreDirector] Installed director registry capacity exceeded; cold installer cannot prove duplicate state without component lookup.", this);
                return;
            }

            s_installedOwners[s_installedCount] = owner;
            s_installedInstances[s_installedCount] = this;
            s_installedCount++;
        }

        private void UnregisterInstalledOwner()
        {
            for (int i = 0; i < s_installedCount; i++)
            {
                if (!ReferenceEquals(s_installedInstances[i], this))
                    continue;

                s_installedCount--;
                s_installedOwners[i] = s_installedOwners[s_installedCount];
                s_installedInstances[i] = s_installedInstances[s_installedCount];
                s_installedOwners[s_installedCount] = null;
                s_installedInstances[s_installedCount] = null;
                return;
            }
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
            ClearFrontierLoreNotificationDiagnostics();
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
                    logHash = ComputeAudioLogHash(dto.logId),
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
                logHash = ComputeAudioLogHash(logData.SafeLogId),
                chunkKey = chunkKey,
                position = spawnPosition,
                instance = null
            };

            if (!TrySpawnInstance(ref placement))
                return;

            _activePlacements.Add(placement);
            _occupiedChunkKeys.Add(chunkKey);
            TryPushFrontierLoreNotification(placement.logHash != 0u ? placement.logHash : unchecked((uint)chunkKey));
        }

        private void TryPushFrontierLoreNotification(uint contextHash)
        {
            if (NotificationEvents.TryPushInfo("PDA archive anomaly detected near the frontier. Route updated with a probable data lead.".AsSpan()))
                return;

            ReportFrontierLoreNotificationMiss(contextHash);
        }

        private void ReportFrontierLoreNotificationMiss(uint contextHash)
        {
            _frontierLoreNotificationMissCount++;
            GlobalTelemetryBus.PublishPerformanceWarning(
                s_frontierLoreNotificationMissWarningHash,
                s_proceduralLoreDirectorContextHash ^ s_frontierLoreNotificationContextHash ^ contextHash,
                Mathf.Max(1, _frontierLoreNotificationMissCount));
        }

        private void ClearFrontierLoreNotificationDiagnostics()
        {
            _frontierLoreNotificationMissCount = 0;
        }

        private void RefreshActivePlacements()
        {
            AudioLogSystem audioLogSystem = ResolveAudioLogSystem();
            for (int i = _activePlacements.Count - 1; i >= 0; i--)
            {
                ActiveLorePlacement placement = _activePlacements[i];
                if (placement.logHash == 0u)
                {
                    placement.logHash = ComputeAudioLogHash(placement.logId);
                    _activePlacements[i] = placement;
                }

                bool discovered = audioLogSystem != null && audioLogSystem.IsDiscovered(placement.logHash);

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
            AudioLogSystem audioLogSystem = ResolveAudioLogSystem();
            if (pickupTemplate == null ||
                audioLogSystem == null ||
                !TryResolveCachedObjectPool(out IObjectPoolService pool))
            {
                return false;
            }

            AudioLogData logData = FindCatalogEntry(placement.logId);
            if (logData == null)
                return false;

            if (!_poolWarmed)
            {
                pool.Warmup(pickupTemplate.gameObject, Mathf.Clamp(maxActiveDrops, 1, ProceduralLoreStateDTO.MaxActivePlacements));
                _poolWarmed = true;
            }

            GameObject spawnedObject = pool.Spawn(pickupTemplate.gameObject, placement.position, Quaternion.identity);
            if (spawnedObject == null)
                return false;

            if (!TryResolvePooledAudioLogPickup(pool, spawnedObject, out AudioLogPickup pickup))
            {
                DespawnPooledLoreOrDeactivate(pool, spawnedObject);
                return false;
            }

            pickup.ConfigureRecoveryPickup(logData, true);
            placement.instance = spawnedObject;
            placement.owningPool = pool;
            return true;
        }

        private static bool TryResolvePooledAudioLogPickup(
            IObjectPoolService pool,
            GameObject instance,
            out AudioLogPickup pickup)
        {
            pickup = null;
            return pool != null &&
                   instance != null &&
                   pool.TryGetPooledComponent(instance, out pickup);
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

            ObjectPoolManager.DespawnOrDeactivate(placement.instance, placement.owningPool);

            placement.instance = null;
            placement.owningPool = null;
        }

        private bool ResolveOwners()
        {
            return _explorationTracker != null && ResolveAudioLogSystem() != null;
        }

        private void RefreshCachedOwners()
        {
            _explorationTracker = GlobalRegistry.PlayerExplorationReadModel;
            CacheAudioLogSystem(Hecton8.Core.GlobalRegistry.AudioLogs);
            if (CacheObjectPoolService(null))
                _poolWarmed = false;

            _saveService = GlobalRegistry.Save;
        }

        private void ClearCachedRuntimeServices()
        {
            _explorationTracker = null;
            _audioLogSystem = null;
            _objectPool = null;
            _saveService = null;
            _poolWarmed = false;
        }

        private void CacheAudioLogSystem(AudioLogSystem audioLogSystem)
        {
            _audioLogSystem = IsAudioLogSystemUsable(audioLogSystem) ? audioLogSystem : null;
        }

        private AudioLogSystem ResolveAudioLogSystem()
        {
            AudioLogSystem audioLogSystem = _audioLogSystem;
            if (IsAudioLogSystemUsable(audioLogSystem))
                return audioLogSystem;

            _audioLogSystem = null;
            return null;
        }

        private static bool IsAudioLogSystemUsable(AudioLogSystem audioLogSystem)
        {
            return audioLogSystem != null && audioLogSystem.IsAudioLogRuntimeReady;
        }

        private bool ResolveCatalog()
        {
            if (_catalogCount > 0)
                return true;

            if (catalogSource != null)
            {
                _catalogCount = catalogSource.CopyCatalog(_catalogBuffer);
                if (_catalogCount > 0)
                {
                    return true;
                }
            }

            if (PDADataLogTab.TryCopyRegisteredCatalog(_catalogBuffer, out _catalogCount))
                return true;

            return false;
        }

        private bool ResolvePickupTemplate()
        {
            if (pickupTemplate != null)
                return true;

            if (AudioLogPickup.TryGetRegisteredTemplate(out pickupTemplate))
                return true;

            return false;
        }

        private bool TrySelectFrontierChunk(out Vector2Int frontierChunk)
        {
            frontierChunk = default;
            if (_explorationTracker == null)
                return false;

            int exploredCount = _explorationTracker.CopyExploredChunkKeys(_exploredChunkKeyBuffer);
            if (exploredCount <= 0)
                return false;

            _exploredChunkKeys.Clear();
            for (int i = 0; i < exploredCount; i++)
                _exploredChunkKeys.Add(_exploredChunkKeyBuffer[i]);

            int startIndex = _nextChunkScanIndex % exploredCount;
            for (int i = 0; i < exploredCount; i++)
            {
                int scanIndex = (startIndex + i) % exploredCount;
                Vector2Int exploredChunk = PDAKeyUtility.UnpackChunkKey(_exploredChunkKeyBuffer[scanIndex]);

                for (int neighborIndex = 0; neighborIndex < _neighborOffsets.Length; neighborIndex++)
                {
                    Vector2Int candidate = exploredChunk + _neighborOffsets[neighborIndex];
                    long candidateKey = PDAKeyUtility.PackChunkKey(candidate.x, candidate.y);
                    if (_exploredChunkKeys.Contains(candidateKey) || _occupiedChunkKeys.Contains(candidateKey))
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

            AudioLogSystem audioLogSystem = ResolveAudioLogSystem();
            int startIndex = _nextCatalogIndex % _catalogCount;
            for (int i = 0; i < _catalogCount; i++)
            {
                int candidateIndex = (startIndex + i) % _catalogCount;
                AudioLogData candidate = _catalogBuffer[candidateIndex];
                if (candidate == null || string.IsNullOrWhiteSpace(candidate.SafeLogId))
                    continue;

                uint candidateHash = ComputeAudioLogHash(candidate.SafeLogId);
                if (candidateHash == 0u)
                    continue;

                if (audioLogSystem != null && audioLogSystem.IsDiscovered(candidateHash))
                    continue;

                if (IsAlreadyActive(candidateHash))
                    continue;

                _nextCatalogIndex = candidateIndex + 1;
                logData = candidate;
                return true;
            }

            return false;
        }

        private bool IsAlreadyActive(uint logHash)
        {
            if (logHash == 0u)
                return false;

            for (int i = 0; i < _activePlacements.Count; i++)
            {
                if (_activePlacements[i].logHash == logHash)
                    return true;
            }

            return false;
        }

        private static uint ComputeAudioLogHash(string logId)
        {
            return QuestFlagHashKernel.ComputeStableHash(logId);
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
            float chunkSize = ExplorationMapDTO.DenseChunkSizeMeters;
            Vector3 basePosition = new Vector3(
                (chunkCoordinates.x + 0.5f) * chunkSize,
                transform.position.y,
                (chunkCoordinates.y + 0.5f) * chunkSize);

            uint hash = unchecked((uint)(chunkKey ^ (chunkKey >> 32)));
            float angle = (hash & 1023u) / 1024f * Mathf.PI * 2f;
            float radius = Mathf.Min(chunkPlacementRadius, chunkSize * 0.35f) * (((hash >> 10) & 1023u) / 1023f);
            MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
            basePosition.x += cos * radius;
            basePosition.z += sin * radius;
            return basePosition;
        }

        private void TryRegisterWithTickManager()
        {
            if (_registeredToTick)
                return;
            if (!Application.isPlaying || GlobalRegistry.Dispatcher == null)
                return;


            _registeredToTick = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Player);
        }

        private void UnregisterFromTickManager()
        {
            if (!_registeredToTick)
                return;

                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Player);

            _registeredToTick = false;
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.Save:
                    UnregisterFromSaveManager();
                    _saveService = currentService as ISaveService;
                    TryRegisterWithSaveManager();
                    break;
                case GlobalRegistryServiceSlot.PlayerExplorationRuntime:
                    _explorationTracker = currentService as IPlayerExplorationChunkReadModel;
                    break;
                case GlobalRegistryServiceSlot.AudioLogRuntime:
                    CacheAudioLogSystem(currentService as AudioLogSystem);
                    break;
                case GlobalRegistryServiceSlot.ObjectPool:
                    if (CacheObjectPoolService(currentService as ObjectPoolManager))
                        _poolWarmed = false;
                    break;
            }
        }

        private bool CacheObjectPoolService(ObjectPoolManager candidate)
        {
            ObjectPoolManager pool = candidate;
            if (!ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(pool) &&
                !ObjectPoolManager.TryResolveActiveRuntime(ref pool))
            {
                pool = null;
            }

            if (ReferenceEquals(_objectPool, pool))
                return false;

            _objectPool = pool;
            return true;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = cached;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                if (!ReferenceEquals(_objectPool, resolved))
                    _poolWarmed = false;

                _objectPool = resolved;
                pool = resolved;
                return true;
            }

            if (_objectPool != null)
                _poolWarmed = false;

            _objectPool = null;
            pool = null;
            return false;
        }

        private static void DespawnPooledLoreOrDeactivate(IObjectPoolService pool, GameObject instance)
        {
            ObjectPoolManager.DespawnOrDeactivate(instance, pool);
        }

        private void TryRegisterHotSwapListener()
        {
            if (_registeredHotSwapListener || !Application.isPlaying)
                return;

            _registeredHotSwapListener = GlobalRegistry.TryRegisterHotSwapListener(this);
        }

        private void TryUnregisterHotSwapListener()
        {
            if (!_registeredHotSwapListener)
                return;

            GlobalRegistry.TryUnregisterHotSwapListener(this);
            _registeredHotSwapListener = false;
        }

        private static bool IsSaveServiceUsable(ISaveService saveService)
        {
            return saveService != null && saveService.IsInitialized;
        }

        private void TryRegisterWithSaveManager()
        {
            if (_registeredToSave || !Application.isPlaying || !isActiveAndEnabled)
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
            _registeredToSave = true;
        }

        private void UnregisterFromSaveManager()
        {
            if (!_registeredToSave && _registeredSaveService == null)
                return;

            ISaveService saveService = _registeredSaveService != null ? _registeredSaveService : _saveService;
            if (saveService != null)
                saveService.Unregister(this);

            _registeredSaveService = null;
            _registeredToSave = false;
        }
    }
}
