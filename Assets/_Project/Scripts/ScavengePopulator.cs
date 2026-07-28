// ============================================================================
// HECTON-8 — ScavengePopulator.cs  (Refactored — Direct API Mode)
// System for populating the world with resource nodes (ResourceNode).
//
// RESPONSIBILITIES:
//   1. Receiving generation data from HectonScatterOutput (Custom MapMagic Node).
//   2. Spawning ResourceNode via ObjectPoolManager (zero-allocation pool).
//   3. Generating deterministic Unique IDs for the save system.
//   4. Checking WorldStateManager — skipping already collected nodes.
//   5. Time-sliced spawning — without freezes during chunk loading (500+ nodes).
//   6. Culling: despawning nodes upon chunk unloading.
//   7. Registry of active nodes per chunk (ActiveNodesPerChunk).
//   8. Highlighting the closest resource upon Director's request
//      (HighlightNearbyResource).
//
// PREFAB SOURCES (in priority order):
//   1. lootTables[] — per-context arrays of authored ResourceNode prefabs, filled in the Inspector.
//   2. ResourceDistributionDirector.TryResolveExternalProducerNodeSpawn — the author-authored fallback
//      ResourceNode prefab the director already owns and warms, plus the ResourceNodeTemplate whose
//      authored depth/temperature/slope envelope matches the scatter point. Used when (1) is empty for
//      the requested context, so an unauthored loot table degrades the resource lane's variety instead
//      of deleting it. A scatter point is only discarded when BOTH sources come up empty, and that
//      discard is counted (DroppedNoPrefabCount) and reported once.
//
// ARCHITECTURE (v2 — Direct API):
//   • Registry service — custom MapMagic node resolves through WorldRuntimeReferenceUtility.
//   • ISlowTickable — for time-sliced spawning (does not block the main thread).
//   • HectonScatterOutput → live ScavengePopulator → RegisterSpawnPoint() — direct calls, zero GC.
//   • ObjectPoolManager — spawning/despawning of all ResourceNodes.
//   • WorldStateManager — checking the depleted state.
//   • Deterministic ID: hash(chunkCoord, localIndex) → StringBuilder → string.
//
// WHAT WAS REMOVED (v1 → v2):
//   ✗ MapMagicObject reference and field.
//   ✗ SubscribeMapMagicEvents / UnsubscribeMapMagicEvents.
//   ✗ HandleTileApplied — no longer intercepting the event.
//   ✗ ExtractScatterData — no longer reading TerrainData.treeInstances.
//   ✗ RegisterSpawnPoints(Vector3[], Quaternion[]) — massive overloads.
//   Everything is replaced by a single RegisterSpawnPoint(pos, rot, scale, coord, idx).
//
// DOUBLE DESPAWN PROTECTION (v2.1):
//   DespawnChunk checks activeInHierarchy before returning to the pool.
//   If the object is already inactive, it means it was destroyed by the player
//   and already returned to the pool by ResourceNode itself. Repeated Despawn is skipped.
//
// HIGHLIGHT (HighlightNearbyResource):
//   • Finds the closest ActiveNode by sqrMagnitude in all loaded chunks.
//   • Iteration: foreach over Dictionary (KeyValuePair), for over List<ActiveNode>.
//   • No LINQ. No allocations (struct math only).
//   • Activates InteractionHighlighter on the found node.
//   • Fallback: Debug.Log if the highlight component is not found.
//
// ZERO GC:
//   • StringBuilder is cached — one allocation forever.
//   • SpawnRequest — struct (stack allocated).
//   • Queue<SpawnRequest> — pre-allocated, Enqueue/Dequeue = 0 GC.
//   • Dictionary<Vector2Int, ChunkData> — allocation on the first chunk.
//   • List<ActiveNode> — pre-allocated per chunk.
//   • No Find, LINQ, or foreach in hot paths.
//
// TIME-SLICING:
//   Spawning is distributed across several SlowTicks:
//     • maxSpawnsPerTick = 20 (configurable).
//     • 500 nodes = 25 ticks × 0.5s = ~12.5 seconds for a full load.
//     • But the player sees the nodes appearing from nearest to farthest.
// ============================================================================

using System.Collections.Generic;
using System.Text;
using Hecton8.Core;
using Hecton8.Scavenging;
using Hecton8.World;
using Hecton8.Interaction;
using Hecton8.Caves;
using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-4000)]
    public sealed class ScavengePopulator : MonoBehaviour, ISlowTickable, ILateFrameTickable, IServiceHeartbeat, IServiceShutdown, IGlobalRegistryHotSwapListener
    {
        private const int SpawnQueueCapacity = 512;
        private const int PendingPresentationOperationCapacity = 8192;
        private const uint ScavengePopulatorTelemetryContextHash = 0x53435050u; // SCPP
        private const uint PresentationQueueOverflowWarningHash = 0x53435051u; // SCPQ

        internal bool IsRuntimeOwnerUsable =>
            _serviceRegistered &&
            isActiveAndEnabled &&
            !_runtimeOwnerAborted &&
            !_isDuplicateInstance;

        // ══════════════════════════════════════════════════════════
        //  REGISTRY SERVICE
        // ══════════════════════════════════════════════════════════

        /// Globalnyy dostup. Ispolzuetsya iz HectonScatterOutput
        /// dlya registratsii spavn-tochek bez promezhutochnyh allokatsiy.
        // ══════════════════════════════════════════════════════════
        //  DATA STRUCTURES — all structs for zero GC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Zapros na spavn uzla. Struct — zero GC pri Enqueue/Dequeue.
        /// Hranit vse neobhodimoe dlya otlozhennogo spavna.
        /// </summary>
        private struct SpawnRequest
        {
            public Vector3      position;
            public Quaternion    rotation;
            public Vector3      scale;
            public Vector2Int   chunkCoord;
            public int          localIndex;
            public SpawnContext  context;
        }

        private enum PendingPresentationOperationKind : byte
        {
            None = 0,
            ApplyScale = 1,
            DespawnOrDeactivate = 2
        }

        private struct PendingPresentationOperation
        {
            public PendingPresentationOperationKind kind;
            public GameObject gameObject;
            public Transform transform;
            public Vector3 scale;
        }
        /// <summary>
        /// Maps a SpawnContext to an array of resource prefabs.
        /// Configured in Inspector. ScavengePopulator selects from
        /// the matching table when spawning a ResourceNode.
        ///
        /// If no table matches the requested context, the first
        /// table in the list is used as fallback (typically Surface).
        /// </summary>
        [System.Serializable]
        public struct LootTableEntry
        {
            [Tooltip("Which spawn context this table covers.")]
            public SpawnContext context;

            [Tooltip("ResourceNode prefabs for this context.\n" +
                     "Selected deterministically: localIndex % count.")]
            public GameObject[] resourcePrefabs;
        }
        /// <summary>
        /// Zapis ob aktivnom uzle. Struct — zero GC v List.
        /// </summary>
        private struct ActiveNode
        {
            public GameObject gameObject;
            public Transform  transform;
            public string     uniqueId;
        }

        /// <summary>
        /// Dannye chanka: koordinaty + spisok aktivnyh uzlov.
        /// Class (reference type) t.k. hranitsya v Dictionary value
        /// i soderzhit List (reference type).
        /// </summary>
        private sealed class ChunkData
        {
            public readonly Vector2Int       coord;
            public readonly List<ActiveNode> activeNodes;
            public bool isLoaded;

            public ChunkData(Vector2Int coord, int initialCapacity)
            {
                this.coord       = coord;
                this.activeNodes = new List<ActiveNode>(initialCapacity);
                this.isLoaded    = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── Loot Tables ───────────────────────────────")]
        [Tooltip("Tablitsy resursov po kontekstu spavna.\n" +
                 "Surface = poverhnost dna (truby, titan).\n" +
                 "CaveShallow = neglubokie peschery (kvarts, griby).\n" +
                 "CaveDeep = glubokie peschery (uran, kristally).\n" +
                 "Esli kontekst ne nayden — ispolzuetsya pervaya tablitsa.")]
        [SerializeField] private LootTableEntry[] lootTables;

        [Header("── Spawn Settings ────────────────────────────")]
        [Tooltip("Obschiy profil chankovogo mira. Esli zadan, resursy berut iz nego razmer chanka i dalnost zhizni.")]
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

        [Tooltip("Razmer tayla MapMagic (metry). " +
                 "Dolzhen sovpadat s MapMagic Tile Size. " +
                 "Ispolzuetsya dlya koordinatnoy konvertatsii.")]
        [SerializeField] private float tileSize = 512f;

        [Tooltip("Maksimalnoe kolichestvo spavnov za odin SlowTick. " +
                 "500 uzlov / 20 per tick / 0.5s interval = ~12.5s full load.")]
        [SerializeField] private int maxSpawnsPerTick = 20;

        [Tooltip("Rasstoyanie ot igroka, posle kotorogo chank vygruzhaetsya.")]
        [SerializeField] private float unloadDistance = 300f;

        [Tooltip("Radius ot igroka dlya prioritetnoy zagruzki (zarezervirovano).")]
        [SerializeField] private float priorityLoadRadius = 150f;

        [Header("── ID Generation ─────────────────────────────")]
        [Tooltip("Prefiks dlya unikalnyh ID uzlov. " +
                 "Format: \"{prefix}_{chunkX}_{chunkZ}_{localIndex}\"")]
        [SerializeField] private string idPrefix = "rn";

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugActiveChunks;
        [SerializeField] private int _debugTotalActiveNodes;
        [SerializeField] private int _debugPendingSpawns;
        [SerializeField] private int _debugSkippedDepleted;
        [SerializeField] private int _debugDroppedNoPrefab;
        [SerializeField] private int _debugFallbackSpawns;
        [SerializeField] private int _debugFallbackSpawnsWithoutTemplate;
        [SerializeField] private string _debugLastHighlightedId;
        [SerializeField] private float _debugRuntimeChunkSize = 512f;
        [SerializeField] private float _debugRuntimeUnloadDistance = 300f;
        [SerializeField] private float _debugRuntimePriorityRadius = 150f;
        [SerializeField] private int _debugRuntimeMaxSpawnsPerTick = 20;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Reestr aktivnyh chankov.
        /// Key = chunk coordinate (Vector2Int = tile grid position).
        /// Value = ChunkData (spisok aktivnyh uzlov).
        /// </summary>
        private Dictionary<Vector2Int, ChunkData> _chunks;

        /// <summary>
        /// Ochered otlozhennyh spavnov (time-slicing).
        /// Pre-allocated. Enqueue/Dequeue — zero GC.
        /// </summary>
        private Queue<SpawnRequest> _spawnQueue;
        private readonly PendingPresentationOperation[] _pendingPresentationOperations = new PendingPresentationOperation[PendingPresentationOperationCapacity];
        private int _pendingPresentationOperationCount;

        /// <summary>
        /// Keshirovannyy StringBuilder dlya generatsii unique ID.
        /// Odna allokatsiya navsegda. Clear() + Append() — zero GC.
        /// .ToString() allotsiruet string — no tolko pri spavne.
        /// </summary>
        private StringBuilder _idBuilder;

        /// <summary>Keshirovannyy Transform igroka.</summary>
        private Transform _playerTransform;
        private IPlayerRuntimeContext _playerRuntimeContext;
        private IObjectPoolService _objectPool;
        private WorldStateManager _worldState;

        /// <summary>Kvadrat unloadDistance — dlya sqrMagnitude sravneniy.</summary>
        private float _unloadDistanceSqr;
        private float _runtimeTileSize = 512f;
        private float _runtimeUnloadDistance = 300f;
        private float _runtimeUnloadDistanceSqr;
        private float _runtimePriorityLoadRadius = 150f;
        private int _runtimeMaxSpawnsPerTick = 20;

        /// <summary>Schetchik propuschennyh depleted uzlov (diagnostika).</summary>
        private int _skippedDepletedCount;

        /// <summary>
        /// Scatter points dropped because neither the authored loot tables nor the
        /// ResourceDistributionDirector fallback could supply a ResourceNode prefab.
        /// A non-zero value with zero active nodes is the unauthored-content signature.
        /// </summary>
        private int _droppedNoPrefabCount;

        /// <summary>
        /// Scatter points whose prefab was resolved through the director's author-authored fallback because
        /// the scene's loot tables are empty for their context. Counted at resolution, so it can exceed the
        /// number of nodes that actually reached the world if the pool is exhausted. Non-zero here means the
        /// Resource lane is running on the fallback, not on authored per-context loot tables.
        /// </summary>
        private int _fallbackSpawnCount;

        /// <summary>
        /// Fallback resolutions that matched no ResourceNodeTemplate envelope, so the node carries only
        /// whatever the prefab itself authors and yields no template loot on depletion.
        /// </summary>
        private int _fallbackSpawnWithoutTemplateCount;

        private bool _reportedMissingResourcePrefab;

        /// <summary>
        /// Keshirovannyy spisok koordinat chankov dlya despavna.
        /// Pereispolzuetsya kazhdyy SlowTick — predotvraschaet
        /// Dictionary modification during iteration.
        /// </summary>
        private List<Vector2Int> _chunksToUnload;
        private bool _initialized;
        private bool _isDuplicateInstance;
        private bool _registeredToSlowTickManager;
        private bool _registeredToLateFrame;
        private bool _pendingScavengeVisualSync;
        private bool _serviceRegistered;
        private bool _hotSwapRegistered;
        private bool _runtimeOwnerAborted;

        public ServiceHeartbeatState HeartbeatState
        {
            get
            {
                if (_runtimeOwnerAborted || _isDuplicateInstance)
                    return ServiceHeartbeatState.Failed;
                if (!_initialized)
                    return ServiceHeartbeatState.NotStarted;
                if (!_serviceRegistered)
                    return ServiceHeartbeatState.NotStarted;
                return enabled ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.Shutdown;
            }
        }

        public bool IsServiceReady => _initialized && !_runtimeOwnerAborted && !_isDuplicateInstance && _serviceRegistered && enabled;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Local allocation only ──
            // ── Pre-allocate collections ──
            _chunks         = new Dictionary<Vector2Int, ChunkData>(32);
            _spawnQueue     = new Queue<SpawnRequest>(SpawnQueueCapacity);
            _idBuilder      = new StringBuilder(64);
            _chunksToUnload = new List<Vector2Int>(16);
            _initialized    = true;

            RefreshRuntimeStreamingSettings();
            CacheRegistryServicesCold();
        }

        private void OnEnable()
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized)
                return;

            if (!TryRegisterService())
                return;

            CacheRegistryServicesCold();
            TryRegisterHotSwapListener();

            if (!_registeredToSlowTickManager && Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                _registeredToSlowTickManager = GlobalRegistry.TryRegisterSlowTickable(this, PriorityLayer.Environment);
            }

            if (!_registeredToLateFrame && Application.isPlaying && GlobalRegistry.Dispatcher != null)
                _registeredToLateFrame = GlobalRegistry.TryRegisterLateFrameTickable(this, PriorityLayer.Environment);

            if (_playerTransform == null)
                FindPlayer();
        }

        private void OnDisable()
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized)
                return;

            if (_registeredToSlowTickManager)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredToSlowTickManager = false;
            }

            if (_registeredToLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredToLateFrame = false;
                _pendingScavengeVisualSync = false;
            }

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterScavengePopulatorRuntime(this);
                _serviceRegistered = false;
                WorldRuntimeReferenceUtility.InvalidateScavengePopulatorCache(this);
            }

            DespawnAllChunks(flushPresentationImmediately: true);
            FlushPendingPresentationOperations();
            ClearPendingPresentationOperations();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
        }

        public void OnServiceShutdown()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_registeredToSlowTickManager)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredToSlowTickManager = false;
            }

            if (_registeredToLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredToLateFrame = false;
                _pendingScavengeVisualSync = false;
            }

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterScavengePopulatorRuntime(this);
                _serviceRegistered = false;
                WorldRuntimeReferenceUtility.InvalidateScavengePopulatorCache(this);
            }

            DespawnAllChunks(flushPresentationImmediately: true);
            FlushPendingPresentationOperations();
            ClearPendingPresentationOperations();
            TryUnregisterHotSwapListener();
            ClearCachedRegistryServices();
            _chunks?.Clear();
            _spawnQueue?.Clear();
            _chunksToUnload?.Clear();
            _idBuilder?.Clear();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        public void OnGlobalRegistryServiceReplaced(
            GlobalRegistryServiceSlot serviceSlot,
            object previousService,
            object currentService)
        {
            if (_runtimeOwnerAborted)
                return;

            switch (serviceSlot)
            {
                case GlobalRegistryServiceSlot.ObjectPool:
                    CacheObjectPoolService(currentService as ObjectPoolManager);
                    break;
                case GlobalRegistryServiceSlot.WorldStateRuntime:
                    _worldState = currentService as WorldStateManager;
                    break;
                case GlobalRegistryServiceSlot.Player:
                    _playerRuntimeContext = currentService as IPlayerRuntimeContext;
                    FindPlayer();
                    break;
            }
        }

        private void CacheRegistryServicesCold()
        {
            if (_runtimeOwnerAborted)
                return;

            if (_objectPool == null)
                CacheObjectPoolService(null);

            if (_worldState == null)
                _worldState = GlobalRegistry.WorldState;

            if (_playerRuntimeContext == null)
                _playerRuntimeContext = GlobalRegistry.Player;

            if (_playerTransform == null)
                FindPlayer();
        }

        private void ClearCachedRegistryServices()
        {
            _objectPool = null;
            _worldState = null;
            _playerRuntimeContext = null;
            _playerTransform = null;
        }

        private void CacheObjectPoolService(ObjectPoolManager candidate)
        {
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(candidate))
            {
                _objectPool = candidate;
                return;
            }

            ObjectPoolManager pool = null;
            _objectPool = ObjectPoolManager.TryResolveActiveRuntime(ref pool)
                ? pool
                : null;
        }

        private bool TryResolveCachedObjectPool(out IObjectPoolService pool)
        {
            ObjectPoolManager cached = _objectPool as ObjectPoolManager;
            if (ObjectPoolManager.IsRuntimeOwnerUsableForRegistry(cached))
            {
                pool = cached;
                return true;
            }

            ObjectPoolManager resolved = null;
            if (ObjectPoolManager.TryResolveActiveRuntime(ref resolved))
            {
                _objectPool = resolved;
                pool = resolved;
                return true;
            }

            _objectPool = null;
            pool = null;
            return false;
        }

        private void TryRegisterHotSwapListener()
        {
            if (_runtimeOwnerAborted || _hotSwapRegistered || !Application.isPlaying)
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

        private bool TryRegisterService()
        {
            if (_runtimeOwnerAborted)
                return false;

            if (_serviceRegistered || !Application.isPlaying)
                return true;

            if (TryAbortForUsableExistingRuntime())
                return false;

            ScavengePopulator registeredRuntime = GlobalRegistry.ScavengePopulator;
            if (IsScavengePopulatorRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            if (!ReferenceEquals(registeredRuntime, null) && !ReferenceEquals(registeredRuntime, this))
                GlobalRegistry.UnregisterScavengePopulatorRuntime(registeredRuntime);

            GlobalRegistry.RegisterScavengePopulatorRuntime(this);
            _serviceRegistered = ReferenceEquals(GlobalRegistry.ScavengePopulator, this);
            if (!_serviceRegistered)
            {
                AbortDuplicateRuntimeOwner();
                return false;
            }

            return _serviceRegistered;
        }

        private bool TryAbortForUsableExistingRuntime()
        {
            ScavengePopulator registeredRuntime = GlobalRegistry.ScavengePopulator;
            if (ReferenceEquals(registeredRuntime, this))
                return false;

            if (IsScavengePopulatorRuntimeUsable(registeredRuntime))
            {
                AbortDuplicateRuntimeOwner();
                return true;
            }

            if (!ReferenceEquals(registeredRuntime, null))
                GlobalRegistry.UnregisterScavengePopulatorRuntime(registeredRuntime);

            return false;
        }

        private void AbortDuplicateRuntimeOwner()
        {
            _runtimeOwnerAborted = true;
            _isDuplicateInstance = true;
            _registeredToSlowTickManager = false;
            _registeredToLateFrame = false;
            _serviceRegistered = false;
            _hotSwapRegistered = false;
            _pendingScavengeVisualSync = false;
            ClearPendingPresentationOperations();
            ClearCachedRegistryServices();
            enabled = false;
        }

        private static bool IsScavengePopulatorRuntimeUsable(ScavengePopulator populator)
        {
            return !ReferenceEquals(populator, null) &&
                   populator != null &&
                   populator.IsRuntimeOwnerUsable;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — Called from HectonScatterOutput
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Registriruet odnu tochku spavna resursnogo uzla.
        ///
        /// Vyzyvaetsya iz HectonScatterOutput.ApplyData.Apply()
        /// na glavnom potoke. Dannye stavyatsya v ochered dlya
        /// time-sliced spavna cherez ProcessSpawnQueue().
        ///
        /// ZERO GC: SpawnRequest — struct, Enqueue — zero GC.
        /// Edinstvennaya allokatsiya — ChunkData pri pervom chanke.
        /// </summary>
        /// <param name="position">Mirovaya pozitsiya spavna.</param>
        /// <param name="rotation">Povorot (obychno tolko Y-axis).</param>
        /// <param name="scale">Masshtab iz scatter-dannyh.</param>
        /// <param name="chunkCoord">Koordinata chanka (tile grid).</param>
        /// <param name="localIndex">Indeks vnutri chanka (dlya determinirovannogo ID).</param>
        /// <param name="context">
        /// Kontekst spavna dlya vybora tablitsy resursov.
        /// Po umolchaniyu Surface dlya obratnoy sovmestimosti s suschestvuyuschim scatter-payplaynom.
        /// </param>
        public void RegisterSpawnPoint(
            Vector3      position,
            Quaternion   rotation,
            Vector3      scale,
            Vector2Int   chunkCoord,
            int          localIndex,
            SpawnContext  context = SpawnContext.Surface)
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized)
                return;

            // Ensure chunk tracking entry exists
            EnsureChunk(chunkCoord, 256);

            SpawnRequest request = new SpawnRequest
            {
                position   = position,
                rotation   = rotation,
                scale      = scale,
                chunkCoord = chunkCoord,
                localIndex = localIndex,
                context    = context
            };

            if (_spawnQueue.Count < SpawnQueueCapacity)
                _spawnQueue.Enqueue(request);
        }

        /// <summary>
        /// Podgotavlivaet chank k perezagruzke.
        /// Esli chank uzhe soderzhit uzly — despavnit ih.
        ///
        /// Vyzyvaetsya iz HectonScatterOutput PERED seriey
        /// RegisterSpawnPoint() vyzovov dlya dannogo chanka.
        /// Eto obrabatyvaet sluchay re-generate v terrain generator.
        /// </summary>
        /// <param name="chunkCoord">Koordinata chanka.</param>
        /// <param name="expectedCount">Ozhidaemoe kolichestvo uzlov (dlya pre-alloc).</param>
        public void PrepareChunkForReload(Vector2Int chunkCoord, int expectedCount)
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized)
                return;

            if (_chunks.TryGetValue(chunkCoord, out ChunkData existing))
            {
                if (existing.activeNodes.Count > 0)
                {
                    DespawnChunk(chunkCoord);
                }
            }

            // Udalyaem pending spawns dlya etogo chanka iz ocheredi
            // (edge case: esli predyduschaya generatsiya esche ne byla obrabotana)
            PurgePendingForChunk(chunkCoord);

            EnsureChunk(chunkCoord, expectedCount);
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — TIME-SLICED PROCESSING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Vyzyvaetsya GameTickManager kazhdye ~0.5 sekundy.
        ///
        /// Poryadok:
        ///   1. Obrabotka ocheredi spavna (time-sliced).
        ///   2. Culling dalekih chankov.
        ///
        /// ZERO GC v goryachem puti (Dequeue, struct math).
        /// StringBuilder.ToString() allotsiruet string — no tolko
        /// pri fakticheskom spavne (ne per-frame).
        /// </summary>
        public void SlowTick()
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance || !_initialized || _spawnQueue == null || _chunks == null || _chunksToUnload == null)
                return;

            RefreshRuntimeStreamingSettings();
            ProcessSpawnQueue();
            CullDistantChunks();
            _pendingScavengeVisualSync = true;
        }

        public void LateFrameTick()
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            if (!_pendingScavengeVisualSync && _pendingPresentationOperationCount == 0)
                return;

            _pendingScavengeVisualSync = false;
            FlushPendingPresentationOperations();
            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  SPAWN PROCESSING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Obrabatyvaet do maxSpawnsPerTick zaprosov iz ocheredi.
        ///
        /// Dlya kazhdogo zaprosa:
        ///   1. Generiruet deterministic unique ID.
        ///   2. Proveryaet WorldStateManager.IsNodeDepleted.
        ///   3. Esli zhiv — spavnit cherez ObjectPoolManager.
        ///   4. Primenyaet scale iz scatter-dannyh.
        ///   5. Nastraivaet ResourceNode.uniqueId.
        ///   6. Registriruet v ChunkData.activeNodes.
        /// </summary>
        private void ProcessSpawnQueue()
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            if (_spawnQueue.Count == 0) return;

            WorldStateManager wsm  = _worldState;

            if (!TryResolveCachedObjectPool(out IObjectPoolService pool)) return;

            int spawned = 0;

            while (_spawnQueue.Count > 0 && spawned < _runtimeMaxSpawnsPerTick)
            {
                SpawnRequest request = _spawnQueue.Dequeue();

                // ── Generate deterministic unique ID ──
                string uniqueId = GenerateUniqueId(
                    request.chunkCoord, request.localIndex);

                // ── Check if already depleted ──
                if (wsm != null && wsm.IsNodeDepleted(uniqueId))
                {
                    _skippedDepletedCount++;
                    continue; // Skip — already harvested
                }

                // ── Select prefab from context-appropriate loot table ──
                GameObject prefab = SelectResourcePrefab(request.localIndex, request.context);
                ResourceNodeTemplate runtimeTemplate = null;

                // A loot-table prefab with no warm pool reserve is as unusable as no prefab at all:
                // runtime pool expansion is forbidden by mandate, so pool.Spawn fails closed and only
                // emits a warning. Treat a cold or exhausted pool exactly like a missing entry, so the
                // authored fallback — whose pool the director warms itself — takes over instead of the
                // scatter point being lost. GetAvailableCount is a dictionary probe: no allocation, no log.
                if (prefab != null && pool.GetAvailableCount(prefab) <= 0)
                    prefab = null;

                // ── Authored fallback ──
                // The loot tables are a per-context designer convenience, not the only source of a node
                // prefab. ResourceDistributionDirector owns the author-authored fallback ResourceNode
                // prefab plus the authored template set, and it is the owner that warms that prefab's
                // pool, so an empty loot table must not silently drop every scatter point on the floor.
                if (prefab == null &&
                    !TryResolveAuthoredFallbackSpawn(in request, out prefab, out runtimeTemplate))
                {
                    _droppedNoPrefabCount++;
                    ReportMissingResourcePrefabOnce();
                    continue;
                }

                // ── Spawn via pool ──
                GameObject instance = pool.Spawn(
                    prefab,
                    request.position,
                    request.rotation);

                if (instance == null) continue;

                Transform instanceTransform = instance.transform;

                // ── Configure ResourceNode ──
                // Runs before the presentation queue so a node that returned itself to the pool during
                // template application never gets queued work or a chunk registration.
                if (!ConfigureResourceNode(pool, instance, uniqueId, runtimeTemplate))
                    continue;

                // ── Apply scale from scatter data ──
                EnqueuePresentationScale(instanceTransform, request.scale);

                // ── Register in chunk ──
                if (_chunks.TryGetValue(request.chunkCoord, out ChunkData chunk))
                {
                    ActiveNode node = new ActiveNode
                    {
                        gameObject = instance,
                        transform  = instanceTransform,
                        uniqueId   = uniqueId
                    };

                    chunk.activeNodes.Add(node);
                }

                spawned++;
            }
        }

        /// <summary>
        /// Nastraivaet komponent ResourceNode na zaspavnennom obekte.
        /// Ustanavlivaet uniqueId cherez publichnyy metod SetUniqueId().
        ///
        /// When the spawn came from the ResourceDistributionDirector fallback lane it also carries an
        /// authored ResourceNodeTemplate; stamping it is what gives the node its integrity, collider shape,
        /// presentation and — decisively — its LootPickupPrefab, so depleting it actually drops something.
        /// ApplyRuntimeTemplate tolerates null fallback mesh/material and keeps the prefab's own authored
        /// presentation, which is exactly how ResourceDistributionDirector.ProcessPendingSpawns calls it.
        /// The scatter scale is queued after this call and flushed on the LateFrameTick, so it stays the
        /// final word on node scale, matching the populator's documented scatter contract.
        ///
        /// Returns false when the node removed itself from the world during configuration —
        /// RefreshRuntimeSpatialRegistration re-derives the persistent identity from the new template and
        /// despawns the node when that identity is already tombstoned as harvested. Registering such a node
        /// would make TotalActiveNodes over-report and let HighlightNearbyResource hand a dead node to the
        /// interaction lane.
        /// </summary>
        private static bool ConfigureResourceNode(
            IObjectPoolService pool,
            GameObject instance,
            string uniqueId,
            ResourceNodeTemplate runtimeTemplate)
        {
            if (pool == null || !pool.TryGetPooledComponent(instance, out ResourceNode node))
                return true;

            node.SetUniqueId(uniqueId);

            if (runtimeTemplate == null)
                return true;

            node.ApplyRuntimeTemplate(runtimeTemplate, null, null);
            node.RefreshRuntimeSpatialRegistration();

            return !node.IsDepleted && instance.activeSelf;
        }

        /// <summary>
        /// Asks the live ResourceDistributionDirector for an authored ResourceNode prefab, and the
        /// environment-matched template to stamp on it, for a scatter point the loot tables could not
        /// serve. The director is the owner of both the author-authored fallback prefab and the warmed
        /// pool for it, so this is the only correct place to get one — the populator must never invent a
        /// prefab of its own.
        ///
        /// A null template is a valid outcome: the node still spawns and is interactable, it simply
        /// carries no authored yield for that position. That case is counted separately so a probe can
        /// tell "spawned but yields nothing" apart from "did not spawn".
        ///
        /// ZERO GC: static property read, struct math and array indexing inside the director; no
        /// allocation, no LINQ, no delegate.
        /// </summary>
        private bool TryResolveAuthoredFallbackSpawn(
            in SpawnRequest request,
            out GameObject prefab,
            out ResourceNodeTemplate template)
        {
            prefab = null;
            template = null;

            ResourceDistributionDirector director = ResourceDistributionDirector.ActiveRuntimeInstance;
            if (director == null)
                return false;

            if (!director.TryResolveExternalProducerNodeSpawn(
                    request.position,
                    request.localIndex,
                    out prefab,
                    out template))
            {
                return false;
            }

            _fallbackSpawnCount++;
            if (template == null)
                _fallbackSpawnWithoutTemplateCount++;

            return true;
        }

        /// <summary>
        /// One-shot cold report so an unauthored resource lane cannot fail silently on the SlowTick
        /// cadence. Guarded by a latch, so it never logs per tick.
        /// </summary>
        private void ReportMissingResourcePrefabOnce()
        {
            if (_reportedMissingResourcePrefab)
                return;

            _reportedMissingResourcePrefab = true;
            Hecton8.Core.H8Debug.LogError(
                "[ScavengePopulator] No ResourceNode prefab available for a scatter point: the scene loot " +
                "tables are empty for its context and ResourceDistributionDirector supplied no authored " +
                "fallback prefab. Author ScavengePopulator.lootTables, or assign " +
                "ResourceDistributionDirector._authoredOrePrefab and let its pool warm.",
                this);
        }

        private bool EnqueuePresentationScale(Transform target, Vector3 scale)
        {
            if (target == null)
                return true;

            if (!TryReservePendingPresentationOperation(out int index))
                return false;

            _pendingPresentationOperations[index] = new PendingPresentationOperation
            {
                kind = PendingPresentationOperationKind.ApplyScale,
                transform = target,
                scale = scale
            };

            return true;
        }

        private bool EnqueuePresentationDespawn(GameObject instance, Transform instanceTransform)
        {
            if (instance == null)
                return true;

            if (!TryReservePendingPresentationOperation(out int index))
                return false;

            _pendingPresentationOperations[index] = new PendingPresentationOperation
            {
                kind = PendingPresentationOperationKind.DespawnOrDeactivate,
                gameObject = instance,
                transform = instanceTransform
            };

            return true;
        }

        private bool TryReservePendingPresentationOperation(out int index)
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
            {
                index = -1;
                return false;
            }

            index = _pendingPresentationOperationCount;
            if ((uint)index >= (uint)_pendingPresentationOperations.Length)
            {
                GlobalTelemetryBus.PublishPerformanceWarning(
                    PresentationQueueOverflowWarningHash,
                    ScavengePopulatorTelemetryContextHash,
                    _pendingPresentationOperations.Length);
                return false;
            }

            _pendingPresentationOperationCount = index + 1;
            _pendingScavengeVisualSync = true;
            return true;
        }

        private void FlushPendingPresentationOperations()
        {
            int count = _pendingPresentationOperationCount;
            if (count <= 0)
                return;

            _pendingPresentationOperationCount = 0;
            TryResolveCachedObjectPool(out IObjectPoolService pool);

            for (int i = 0; i < count; i++)
            {
                PendingPresentationOperation operation = _pendingPresentationOperations[i];
                _pendingPresentationOperations[i] = default;

                switch (operation.kind)
                {
                    case PendingPresentationOperationKind.ApplyScale:
                        if (operation.transform != null)
                            operation.transform.localScale = operation.scale;
                        break;

                    case PendingPresentationOperationKind.DespawnOrDeactivate:
                        GameObject instance = operation.gameObject;
                        if (instance == null || !instance.activeInHierarchy)
                            break;

                        Transform instanceTransform = operation.transform;
                        if (instanceTransform != null)
                            instanceTransform.localScale = Vector3.one;

                        if (pool != null)
                            pool.Despawn(instance);
                        else
                            instance.SetActive(false);
                        break;
                }
            }
        }

        private void ClearPendingPresentationOperations()
        {
            int count = _pendingPresentationOperationCount;
            for (int i = 0; i < count; i++)
                _pendingPresentationOperations[i] = default;

            _pendingPresentationOperationCount = 0;
            _pendingScavengeVisualSync = false;
        }

        // ══════════════════════════════════════════════════════════
        //  CULLING — DISTANT CHUNKS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Proveryaet vse zagruzhennye chanki. Esli tsentr chanka
        /// dalshe unloadDistance ot igroka — despavnit vse uzly chanka.
        ///
        /// Ispolzuet keshirovannyy _chunksToUnload dlya sbora klyuchey
        /// pered modifikatsiey Dictionary.
        ///
        /// ZERO GC: Vector2Int — struct. sqrMagnitude — no sqrt.
        /// </summary>
        private void CullDistantChunks()
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            Vector3 playerPos = _playerTransform.position;
            Vector2 playerXZ  = new Vector2(playerPos.x, playerPos.z);

            // The cull test below measures player-to-chunk-CENTRE distance, but a tile's half-diagonal is
            // _runtimeTileSize * 0.7071 — 362m at the authored 512m tile, larger than the 300m default
            // unloadDistance. That makes the chunk the player is standing in eligible for unload whenever
            // the player is near its corner, which despawns nodes from under the player's feet. The chunk
            // the player currently occupies is resident by definition, so it is excluded outright.
            Vector2Int playerChunkCoord = WorldToChunkCoord(playerPos);

            _chunksToUnload.Clear();

            // ── Collect chunks to unload ──
            Dictionary<Vector2Int, ChunkData>.Enumerator enumerator = _chunks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<Vector2Int, ChunkData> kvp = enumerator.Current;
                ChunkData chunk = kvp.Value;
                if (!chunk.isLoaded) continue;
                if (chunk.activeNodes.Count == 0) continue;
                if (kvp.Key.Equals(playerChunkCoord)) continue;

                Vector2 chunkCenter = ChunkCoordToWorldCenter(kvp.Key);
                Vector2 diff = chunkCenter - playerXZ;

                if (diff.sqrMagnitude > _runtimeUnloadDistanceSqr)
                {
                    _chunksToUnload.Add(kvp.Key);
                }
            }

            // ── Unload collected chunks ──
            int unloadCount = _chunksToUnload.Count;
            for (int i = 0; i < unloadCount; i++)
            {
                DespawnChunk(_chunksToUnload[i]);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CHUNK MANAGEMENT
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Poluchaet ili sozdaet ChunkData dlya ukazannyh koordinat.
        /// </summary>
        private ChunkData EnsureChunk(Vector2Int coord, int expectedNodeCount)
        {
            if (_chunks.TryGetValue(coord, out ChunkData existing))
            {
                existing.isLoaded = true;
                return existing;
            }

            int capacity = expectedNodeCount > 0 ? expectedNodeCount : 32;
            ChunkData chunk = new ChunkData(coord, capacity);
            _chunks.Add(coord, chunk);
            return chunk;
        }

        /// <summary>
        /// Despavnit vse uzly chanka. Vozvraschaet obekty v pul.
        /// Sbrasyvaet masshtab pered vozvratom.
        /// Pomechaet chank kak vygruzhennyy (isLoaded = false).
        ///
        /// DOUBLE DESPAWN PROTECTION:
        ///   Obekt vozvraschaetsya v pul TOLKO esli on esche aktiven
        ///   v ierarhii (activeInHierarchy == true).
        ///   Esli obekt uzhe neaktiven — znachit on byl unichtozhen
        ///   igrokom (ResourceNode.TakeDamage → pool.Despawn),
        ///   i povtornyy Despawn vyzovet oshibku / povrezhdenie pula.
        /// </summary>
        private void DespawnChunk(Vector2Int coord)
        {
            if (!_chunks.TryGetValue(coord, out ChunkData chunk))
                return;

            List<ActiveNode> nodes = chunk.activeNodes;
            int count = nodes.Count;

            for (int i = count - 1; i >= 0; i--)
            {
                ActiveNode node = nodes[i];

                if (node.gameObject == null)
                {
                    // Zaschita ot Double Despawn:
                    // Esli obekt uzhe vyklyuchen, znachit on uzhe v pule
                    // (unichtozhen igrokom cherez ResourceNode → pool.Despawn).
                    // Povtornyy Despawn propuskaetsya.
                    nodes.RemoveAt(i);
                    continue;
                }

                if (!node.gameObject.activeInHierarchy)
                {
                    nodes.RemoveAt(i);
                    continue;
                }

                if (EnqueuePresentationDespawn(node.gameObject, node.transform))
                    nodes.RemoveAt(i);
            }

            chunk.isLoaded = nodes.Count > 0;
        }

        /// <summary>
        /// Despavnit VSE chanki. Vyzyvaetsya pri OnDisable / smene stseny.
        /// </summary>
        private void DespawnAllChunks(bool flushPresentationImmediately = false)
        {
            if (_spawnQueue == null || _chunksToUnload == null || _chunks == null)
                return;

            _spawnQueue.Clear();
            _chunksToUnload.Clear();

            Dictionary<Vector2Int, ChunkData>.Enumerator enumerator = _chunks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<Vector2Int, ChunkData> kvp = enumerator.Current;
                _chunksToUnload.Add(kvp.Key);
            }

            int count = _chunksToUnload.Count;
            for (int i = 0; i < count; i++)
            {
                DespawnChunk(_chunksToUnload[i]);
                if (flushPresentationImmediately)
                    FlushPendingPresentationOperations();
            }

            _chunks.Clear();
        }

        /// <summary>
        /// Udalyaet iz ocheredi spavna vse pending-zaprosy dlya ukazannogo chanka.
        ///
        /// Ispolzuetsya pri re-generate (MapMagic peresozdaet tayl):
        /// starye pending-zaprosy dolzhny byt otmeneny, inache oni
        /// zaspavnyatsya poverh novyh dannyh.
        ///
        /// GC NOTE: Sozdaet vremennuyu ochered pri nalichii pending items.
        /// Vyzyvaetsya redko (tolko pri re-generate), poetomu dopustimo.
        /// </summary>
        private void PurgePendingForChunk(Vector2Int chunkCoord)
        {
            if (_spawnQueue.Count == 0) return;

            int originalCount = _spawnQueue.Count;
            int keptCount = 0;

            for (int i = 0; i < originalCount; i++)
            {
                SpawnRequest request = _spawnQueue.Dequeue();

                if (request.chunkCoord.x != chunkCoord.x ||
                    request.chunkCoord.y != chunkCoord.y)
                {
                    // Keep — belongs to different chunk
                    _spawnQueue.Enqueue(request);
                    keptCount++;
                }
                // else: discard — belongs to the chunk being purged
            }
        }

        // ══════════════════════════════════════════════════════════
        //  UNIQUE ID GENERATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Generiruet determinirovannyy Unique ID dlya ResourceNode.
        ///
        /// Format: "{prefix}_{chunkX}_{chunkZ}_{localIndex}"
        /// Primer: "rn_3_-2_47"
        ///
        /// DETERMINIZM: pri odinakovyh chunkCoord + localIndex
        /// vsegda generiruetsya odinakovyy ID. Garantiruet korrektnoe
        /// vosstanovlenie depleted-sostoyaniya posle save/load.
        ///
        /// GC: StringBuilder.ToString() allotsiruet string (~40 bytes).
        /// Vyzyvaetsya TOLKO pri spavne (ne per-frame).
        /// </summary>
        private string GenerateUniqueId(Vector2Int chunkCoord, int localIndex)
        {
            _idBuilder.Clear();
            _idBuilder.Append(idPrefix);
            _idBuilder.Append('_');
            _idBuilder.Append(chunkCoord.x);
            _idBuilder.Append('_');
            _idBuilder.Append(chunkCoord.y);
            _idBuilder.Append('_');
            _idBuilder.Append(localIndex);

            return _idBuilder.ToString();
        }

        // ══════════════════════════════════════════════════════════
        //  COORDINATE CONVERSION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Konvertiruet mirovuyu pozitsiyu v koordinatu chanka (grid position).
        /// Deterministic: floor division.
        /// </summary>
        private Vector2Int WorldToChunkCoord(Vector3 worldPos)
        {
            int cx = Mathf.FloorToInt(worldPos.x / _runtimeTileSize);
            int cz = Mathf.FloorToInt(worldPos.z / _runtimeTileSize);
            return new Vector2Int(cx, cz);
        }

        /// <summary>
        /// Konvertiruet koordinatu chanka v tsentr chanka (world XZ).
        /// </summary>
        private Vector2 ChunkCoordToWorldCenter(Vector2Int coord)
        {
            float cx = (coord.x + 0.5f) * _runtimeTileSize;
            float cz = (coord.y + 0.5f) * _runtimeTileSize;
            return new Vector2(cx, cz);
        }

        // ══════════════════════════════════════════════════════════
        //  PREFAB SELECTION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Selects a ResourceNode prefab from the loot table matching the given context.
        ///
        /// Lookup: linear scan over lootTables[] (typically 2-3 entries — negligible).
        /// Fallback: if no matching context found, uses first table (index 0).
        /// Deterministic: same localIndex + same table = same prefab.
        ///
        /// ZERO GC: array access only, no LINQ, no Dictionary.
        /// </summary>
        private GameObject SelectResourcePrefab(int localIndex, SpawnContext context)
        {
            if (lootTables == null || lootTables.Length == 0)
                return null;

            // ── Find matching loot table ──
            GameObject[] prefabs = null;

            for (int i = 0; i < lootTables.Length; i++)
            {
                if (lootTables[i].context == context)
                {
                    prefabs = lootTables[i].resourcePrefabs;
                    break;
                }
            }

            // ── Fallback: use first table ──
            if (prefabs == null || prefabs.Length == 0)
            {
                prefabs = lootTables[0].resourcePrefabs;
            }

            if (prefabs == null || prefabs.Length == 0)
                return null;

            // ── Deterministic selection ──
            int prefabIndex = localIndex % prefabs.Length;

            // Handle negative localIndex (hashId can be any positive int,
            // but defensive coding for edge cases)
            if (prefabIndex < 0)
                prefabIndex += prefabs.Length;

            return prefabs[prefabIndex];
        }

        // ══════════════════════════════════════════════════════════
        //  PLAYER LOOKUP
        // ══════════════════════════════════════════════════════════

        private void FindPlayer()
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            IPlayerRuntimeContext playerRuntimeContext = _playerRuntimeContext;
            Transform playerTransform = playerRuntimeContext != null ? playerRuntimeContext.PlayerTransform : null;
            if (playerTransform != null)
                _playerTransform = playerTransform;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES & CONTROL
        // ══════════════════════════════════════════════════════════

        /// <summary>Kolichestvo zagruzhennyh chankov s aktivnymi uzlami.</summary>
        public int ActiveChunkCount
        {
            get
            {
                int count = 0;
                Dictionary<Vector2Int, ChunkData>.Enumerator enumerator = _chunks.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<Vector2Int, ChunkData> kvp = enumerator.Current;
                    if (kvp.Value.isLoaded && kvp.Value.activeNodes.Count > 0)
                        count++;
                }
                return count;
            }
        }

        /// <summary>Obschee kolichestvo aktivnyh uzlov vo vseh chankah.</summary>
        public int TotalActiveNodes
        {
            get
            {
                int total = 0;
                Dictionary<Vector2Int, ChunkData>.Enumerator enumerator = _chunks.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    KeyValuePair<Vector2Int, ChunkData> kvp = enumerator.Current;
                    total += kvp.Value.activeNodes.Count;
                }
                return total;
            }
        }

        /// <summary>Kolichestvo zaprosov v ocheredi spavna.</summary>
        public int PendingSpawnCount => _spawnQueue.Count;

        /// <summary>
        /// Scatter points discarded because no ResourceNode prefab could be resolved at all — neither from
        /// the scene loot tables nor from the ResourceDistributionDirector authored fallback.
        /// </summary>
        public int DroppedNoPrefabCount => _droppedNoPrefabCount;

        /// <summary>
        /// Scatter points whose prefab was resolved through the ResourceDistributionDirector authored
        /// fallback because the scene loot tables were empty for their context.
        /// </summary>
        public int FallbackSpawnCount => _fallbackSpawnCount;

        /// <summary>
        /// Fallback resolutions that matched no authored ResourceNodeTemplate envelope, so they carry no
        /// template yield and drop nothing on depletion.
        /// </summary>
        public int FallbackSpawnWithoutTemplateCount => _fallbackSpawnWithoutTemplateCount;

        public float UnloadDistance => _runtimeUnloadDistance;
        public float PriorityLoadRadius => _runtimePriorityLoadRadius;
        public int MaxSpawnsPerSlowTick => _runtimeMaxSpawnsPerTick;

        public void SetRuntimeBudget(float newUnloadDistance, float newPriorityLoadRadius, int newMaxSpawnsPerTick)
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            unloadDistance = Mathf.Max(50f, newUnloadDistance);
            priorityLoadRadius = Mathf.Max(10f, newPriorityLoadRadius);
            maxSpawnsPerTick = Mathf.Max(1, newMaxSpawnsPerTick);
            RefreshRuntimeStreamingSettings();
        }

        /// <summary>
        /// Prinuditelnaya perezagruzka chanka.
        /// Despavnit vse uzly i pomechaet dlya povtornogo zapolneniya.
        /// </summary>
        public void ReloadChunk(Vector2Int coord)
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            DespawnChunk(coord);
        }

        /// <summary>
        /// Prinuditelnaya vygruzka VSEH chankov.
        /// Ispolzuetsya pri teleporte, smene zony.
        /// </summary>
        public void UnloadAll()
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            DespawnAllChunks();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — DIRECTOR ORCHESTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Nahodit i podsvechivaet blizhayshiy resursnyy uzel k ukazannoy
        /// mirovoy pozitsii. Vyzyvaetsya HectonDirectorAI pri RareDiscovery.
        ///
        /// Algoritm:
        ///   1. Iteratsiya po vsem zapisyam Dictionary _chunks cherez foreach
        ///      (KeyValuePair — struct enumerator dlya Dictionary, dopustimo).
        ///   2. Dlya kazhdogo zagruzhennogo chanka — for-tsikl po List&lt;ActiveNode&gt;.
        ///   3. sqrMagnitude sravnenie — bez sqrt.
        ///   4. Zapominaem uzel s minimalnym sqrMagnitude.
        ///   5. Esli uzel nayden — TryGetComponent&lt;InteractionHighlighter&gt;
        ///      dlya vklyucheniya podsvetki.
        ///   6. Fallback: Debug.Log esli vizualnaya sistema ne gotova.
        ///
        /// ZERO GC: struct math, no LINQ, no allocations.
        /// foreach po Dictionary dopuskaetsya zdes, tak kak metod vyzyvaetsya
        /// redko (raz v 30+ sekund po resheniyu Direktora), a ne per-frame.
        /// </summary>
        /// <param name="worldHint">Mirovaya pozitsiya podskazki (tsentr poiska).</param>
        public void HighlightNearbyResource(Vector3 worldHint)
        {
            // ── Poisk blizhayshego aktivnogo uzla ──
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            float bestDistSqr       = float.MaxValue;
            GameObject bestNodeGO   = null;
            string bestNodeId       = null;

            Dictionary<Vector2Int, ChunkData>.Enumerator enumerator = _chunks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<Vector2Int, ChunkData> kvp = enumerator.Current;
                ChunkData chunk = kvp.Value;

                // Propuskaem vygruzhennye chanki
                if (!chunk.isLoaded)
                    continue;

                List<ActiveNode> nodes = chunk.activeNodes;
                int nodeCount = nodes.Count;

                for (int i = 0; i < nodeCount; i++)
                {
                    ActiveNode node = nodes[i];

                    // Propuskaem unichtozhennye/deaktivirovannye uzly
                    if (node.gameObject == null)
                        continue;
                    if (!node.gameObject.activeInHierarchy)
                        continue;
                    if (node.transform == null)
                        continue;

                    // ── sqrMagnitude — bez sqrt ──
                    Vector3 diff = node.transform.position - worldHint;
                    float distSqr = diff.sqrMagnitude;

                    if (distSqr < bestDistSqr)
                    {
                        bestDistSqr = distSqr;
                        bestNodeGO  = node.gameObject;
                        bestNodeId  = node.uniqueId;
                    }
                }
            }

            // ── Rezultat poiska ──
            if (bestNodeGO == null)
            {
#if UNITY_EDITOR
                Hecton8.Core.H8Debug.Log("[ScavengePopulator] HighlightNearbyResource: " +
                          "no active nodes found near hint position.");
#endif
                return;
            }

            // ── Vklyuchenie podsvetki ──
            if (bestNodeGO.TryGetComponent(out InteractionHighlighter highlighter))
            {
                highlighter.SetHighlight(true);
            }
            else
            {
                // Vizualnaya sistema podsvetki ne do kontsa gotova — logiruem
                Hecton8.Core.H8Debug.Log(
                    "[ScavengePopulator] Resource Highlighted: " +
                    (bestNodeId ?? "unknown") +
                    " (InteractionHighlighter not found on node)");
            }

#if UNITY_EDITOR
            _debugLastHighlightedId = bestNodeId;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugActiveChunks     = ActiveChunkCount;
            _debugTotalActiveNodes = TotalActiveNodes;
            _debugPendingSpawns    = _spawnQueue.Count;
            _debugSkippedDepleted  = _skippedDepletedCount;
            _debugDroppedNoPrefab  = _droppedNoPrefabCount;
            _debugFallbackSpawns   = _fallbackSpawnCount;
            _debugFallbackSpawnsWithoutTemplate = _fallbackSpawnWithoutTemplateCount;
            _debugRuntimeChunkSize = _runtimeTileSize;
            _debugRuntimeUnloadDistance = _runtimeUnloadDistance;
            _debugRuntimePriorityRadius = _runtimePriorityLoadRadius;
            _debugRuntimeMaxSpawnsPerTick = _runtimeMaxSpawnsPerTick;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR — GIZMOS
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying || _chunks == null) return;

            Dictionary<Vector2Int, ChunkData>.Enumerator enumerator = _chunks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<Vector2Int, ChunkData> kvp = enumerator.Current;
                ChunkData chunk = kvp.Value;
                Vector2 center  = ChunkCoordToWorldCenter(kvp.Key);

                if (chunk.isLoaded && chunk.activeNodes.Count > 0)
                {
                    Gizmos.color = new Color(0f, 1f, 0.5f, 0.1f);
                    Gizmos.DrawWireCube(
                        new Vector3(center.x, 0f, center.y),
                        new Vector3(_runtimeTileSize, 10f, _runtimeTileSize));

                    UnityEditor.Handles.Label(
                        new Vector3(center.x, 5f, center.y),
                        $"Chunk {kvp.Key}\n{chunk.activeNodes.Count} nodes",
                        new GUIStyle
                        {
                            fontSize = 10,
                            normal = { textColor = Color.green }
                        });
                }
                else
                {
                    Gizmos.color = new Color(0.5f, 0.5f, 0.5f, 0.05f);
                    Gizmos.DrawWireCube(
                        new Vector3(center.x, 0f, center.y),
                        new Vector3(_runtimeTileSize, 5f, _runtimeTileSize));
                }
            }

            if (_playerTransform != null)
            {
                Gizmos.color = new Color(1f, 0.5f, 0f, 0.08f);
                DrawWireCircle(_playerTransform.position, _runtimeUnloadDistance, 48);
            }
        }

        private static void DrawWireCircle(Vector3 center, float radius, int segments)
        {
            float step = Mathf.PI * 2f / segments;
            Vector3 prev = center + new Vector3(radius, 0f, 0f);

            for (int i = 1; i <= segments; i++)
            {
                float angle = step * i;
                MathLodApproximation.ApproxSinCosBhaskara(angle, out float sin, out float cos);
                Vector3 next = center + new Vector3(
                    cos * radius, 0f,
                    sin * radius);
                Gizmos.DrawLine(prev, next);
                prev = next;
            }
        }

        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            if (tileSize < 32f) tileSize = 32f;
            if (maxSpawnsPerTick < 1) maxSpawnsPerTick = 1;
            if (unloadDistance < 50f) unloadDistance = 50f;
            if (priorityLoadRadius < 10f) priorityLoadRadius = 10f;

            RefreshRuntimeStreamingSettings();
        }
#endif

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            if (_runtimeOwnerAborted || _isDuplicateInstance)
                return;

            chunkStreamingProfile = profile;
            RefreshRuntimeStreamingSettings();
        }

        private void RefreshRuntimeStreamingSettings()
        {
            _runtimeTileSize = Mathf.Max(32f, tileSize);
            _runtimeUnloadDistance = Mathf.Max(50f, unloadDistance);
            _runtimePriorityLoadRadius = Mathf.Max(10f, priorityLoadRadius);
            _runtimeMaxSpawnsPerTick = Mathf.Max(1, maxSpawnsPerTick);

            if (chunkStreamingProfile != null)
            {
                WorldChunkStreamingProfile.LayerProfile resourcesLayer =
                    chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Resources);

                _runtimeTileSize = Mathf.Max(32f, chunkStreamingProfile.chunkSizeMeters);
                _runtimePriorityLoadRadius = Mathf.Max(24f, chunkStreamingProfile.fullSimulationRadius * Mathf.Max(0.5f, resourcesLayer.nearRadiusScale));
                _runtimeUnloadDistance = Mathf.Max(_runtimePriorityLoadRadius + 24f, chunkStreamingProfile.midSimulationRadius * Mathf.Max(0.5f, resourcesLayer.midRadiusScale));
                _runtimeMaxSpawnsPerTick = Mathf.Max(maxSpawnsPerTick, Mathf.Clamp(resourcesLayer.maxActivationsPerTick, 8, 64));
            }

            _runtimeUnloadDistanceSqr = _runtimeUnloadDistance * _runtimeUnloadDistance;
            _unloadDistanceSqr = _runtimeUnloadDistanceSqr;
        }
    }
}
