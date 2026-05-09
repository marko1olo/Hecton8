// ============================================================================
// HECTON-8 — ScavengePopulator.cs  (Refactored — Direct API Mode)
// Система заселения мира ресурсными узлами (ResourceNode).
//
// ОТВЕТСТВЕННОСТИ:
//   1. Приём данных генерации от HectonScatterOutput (Custom MapMagic Node).
//   2. Спавн ResourceNode через ObjectPoolManager (zero-allocation pool).
//   3. Генерация детерминированных Unique ID для системы сохранений.
//   4. Проверка WorldStateManager — пропуск уже собранных узлов.
//   5. Time-sliced спавн — без фризов при загрузке чанка (500+ узлов).
//   6. Culling: деспавн узлов при выгрузке чанка.
//   7. Реестр активных узлов по чанкам (ActiveNodesPerChunk).
//   8. Подсветка ближайшего ресурса по запросу Директора
//      (HighlightNearbyResource).
//
// АРХИТЕКТУРА (v2 — Direct API):
//   • Registry service — custom MapMagic node resolves via GlobalRegistry.ScavengePopulator.
//   • ISlowTickable — для time-sliced спавна (не блокирует основной поток).
//   • HectonScatterOutput → RegisterSpawnPoint() — прямые вызовы, zero GC.
//   • ObjectPoolManager — спавн/деспавн всех ResourceNode.
//   • WorldStateManager — проверка depleted состояния.
//   • Deterministic ID: hash(chunkCoord, localIndex) → StringBuilder → string.
//
// ЧТО УДАЛЕНО (v1 → v2):
//   ✗ MapMagicObject ссылка и поле.
//   ✗ SubscribeMapMagicEvents / UnsubscribeMapMagicEvents.
//   ✗ HandleTileApplied — больше не перехватываем событие.
//   ✗ ExtractScatterData — больше не читаем TerrainData.treeInstances.
//   ✗ RegisterSpawnPoints(Vector3[], Quaternion[]) — массивные перегрузки.
//   Всё заменено единым RegisterSpawnPoint(pos, rot, scale, coord, idx).
//
// DOUBLE DESPAWN PROTECTION (v2.1):
//   DespawnChunk проверяет activeInHierarchy перед возвратом в пул.
//   Если объект уже неактивен — значит он был уничтожен игроком
//   и уже возвращён в пул самим ResourceNode. Повторный Despawn пропускается.
//
// HIGHLIGHT (HighlightNearbyResource):
//   • Ищет ближайший ActiveNode по sqrMagnitude во всех загруженных чанках.
//   • Итерация: foreach по Dictionary (KeyValuePair), for по List<ActiveNode>.
//   • Без LINQ. Без аллокаций (struct math only).
//   • Активирует InteractionHighlighter на найденном узле.
//   • Fallback: Debug.Log если компонент подсветки не найден.
//
// ZERO GC:
//   • StringBuilder кэширован — одна аллокация навсегда.
//   • SpawnRequest — struct (stack allocated).
//   • Queue<SpawnRequest> — pre-allocated, Enqueue/Dequeue = 0 GC.
//   • Dictionary<Vector2Int, ChunkData> — аллокация при первом чанке.
//   • List<ActiveNode> — pre-allocated per chunk.
//   • Никаких Find, LINQ, foreach в горячих путях.
//
// TIME-SLICING:
//   Спавн распределён по нескольким SlowTick-ам:
//     • maxSpawnsPerTick = 20 (настраиваемо).
//     • 500 узлов = 25 тиков × 0.5с = ~12.5 секунд полной загрузки.
//     • Но игрок видит узлы появляющимися от ближних к дальним.
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
    public sealed class ScavengePopulator : MonoBehaviour, ISlowTickable, IServiceHeartbeat, IServiceShutdown
    {
        // ══════════════════════════════════════════════════════════
        //  REGISTRY SERVICE
        // ══════════════════════════════════════════════════════════

        /// Глобальный доступ. Используется из HectonScatterOutput
        /// для регистрации спавн-точек без промежуточных аллокаций.
        // ══════════════════════════════════════════════════════════
        //  DATA STRUCTURES — all structs for zero GC
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Запрос на спавн узла. Struct — zero GC при Enqueue/Dequeue.
        /// Хранит всё необходимое для отложенного спавна.
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
        /// Запись об активном узле. Struct — zero GC в List.
        /// </summary>
        private struct ActiveNode
        {
            public GameObject gameObject;
            public Transform  transform;
            public string     uniqueId;
        }

        /// <summary>
        /// Данные чанка: координаты + список активных узлов.
        /// Class (reference type) т.к. хранится в Dictionary value
        /// и содержит List (reference type).
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
        [Tooltip("Таблицы ресурсов по контексту спавна.\n" +
                 "Surface = поверхность дна (трубы, титан).\n" +
                 "CaveShallow = неглубокие пещеры (кварц, грибы).\n" +
                 "CaveDeep = глубокие пещеры (уран, кристаллы).\n" +
                 "Если контекст не найден — используется первая таблица.")]
        [SerializeField] private LootTableEntry[] lootTables;

        [Header("── Spawn Settings ────────────────────────────")]
        [Tooltip("Общий профиль чанкового мира. Если задан, ресурсы берут из него размер чанка и дальность жизни.")]
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;

        [Tooltip("Размер тайла MapMagic (метры). " +
                 "Должен совпадать с MapMagic Tile Size. " +
                 "Используется для координатной конвертации.")]
        [SerializeField] private float tileSize = 512f;

        [Tooltip("Максимальное количество спавнов за один SlowTick. " +
                 "500 узлов / 20 per tick / 0.5s interval = ~12.5s full load.")]
        [SerializeField] private int maxSpawnsPerTick = 20;

        [Tooltip("Расстояние от игрока, после которого чанк выгружается.")]
        [SerializeField] private float unloadDistance = 300f;

        [Tooltip("Радиус от игрока для приоритетной загрузки (зарезервировано).")]
        [SerializeField] private float priorityLoadRadius = 150f;

        [Header("── ID Generation ─────────────────────────────")]
        [Tooltip("Префикс для уникальных ID узлов. " +
                 "Формат: \"{prefix}_{chunkX}_{chunkZ}_{localIndex}\"")]
        [SerializeField] private string idPrefix = "rn";

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugActiveChunks;
        [SerializeField] private int _debugTotalActiveNodes;
        [SerializeField] private int _debugPendingSpawns;
        [SerializeField] private int _debugSkippedDepleted;
        [SerializeField] private string _debugLastHighlightedId;
        [SerializeField] private float _debugRuntimeChunkSize = 512f;
        [SerializeField] private float _debugRuntimeUnloadDistance = 300f;
        [SerializeField] private float _debugRuntimePriorityRadius = 150f;
        [SerializeField] private int _debugRuntimeMaxSpawnsPerTick = 20;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Реестр активных чанков.
        /// Key = chunk coordinate (Vector2Int = tile grid position).
        /// Value = ChunkData (список активных узлов).
        /// </summary>
        private Dictionary<Vector2Int, ChunkData> _chunks;

        /// <summary>
        /// Очередь отложенных спавнов (time-slicing).
        /// Pre-allocated. Enqueue/Dequeue — zero GC.
        /// </summary>
        private Queue<SpawnRequest> _spawnQueue;

        /// <summary>
        /// Кэшированный StringBuilder для генерации unique ID.
        /// Одна аллокация навсегда. Clear() + Append() — zero GC.
        /// .ToString() аллоцирует string — но только при спавне.
        /// </summary>
        private StringBuilder _idBuilder;

        /// <summary>Кэшированный Transform игрока.</summary>
        private Transform _playerTransform;

        /// <summary>Квадрат unloadDistance — для sqrMagnitude сравнений.</summary>
        private float _unloadDistanceSqr;
        private float _runtimeTileSize = 512f;
        private float _runtimeUnloadDistance = 300f;
        private float _runtimeUnloadDistanceSqr;
        private float _runtimePriorityLoadRadius = 150f;
        private int _runtimeMaxSpawnsPerTick = 20;

        /// <summary>Счётчик пропущенных depleted узлов (диагностика).</summary>
        private int _skippedDepletedCount;

        /// <summary>
        /// Кэшированный список координат чанков для деспавна.
        /// Переиспользуется каждый SlowTick — предотвращает
        /// Dictionary modification during iteration.
        /// </summary>
        private List<Vector2Int> _chunksToUnload;
        private bool _initialized;
        private bool _isDuplicateInstance;
        private bool _registeredToSlowTickManager;
        private bool _serviceRegistered;

        public ServiceHeartbeatState HeartbeatState
        {
            get
            {
                if (_isDuplicateInstance)
                    return ServiceHeartbeatState.Failed;
                if (!_initialized)
                    return ServiceHeartbeatState.NotStarted;
                if (!_serviceRegistered)
                    return ServiceHeartbeatState.NotStarted;
                return enabled ? ServiceHeartbeatState.Ready : ServiceHeartbeatState.Shutdown;
            }
        }

        public bool IsServiceReady => _initialized && !_isDuplicateInstance && _serviceRegistered && enabled;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Local allocation only ──
            // ── Pre-allocate collections ──
            _chunks         = new Dictionary<Vector2Int, ChunkData>(32);
            _spawnQueue     = new Queue<SpawnRequest>(512);
            _idBuilder      = new StringBuilder(64);
            _chunksToUnload = new List<Vector2Int>(16);
            _initialized    = true;

            RefreshRuntimeStreamingSettings();
        }

        private void OnEnable()
        {
            if (_isDuplicateInstance || !_initialized)
                return;

            ScavengePopulator activeRuntime = GlobalRegistry.ScavengePopulator;
            if (activeRuntime != null && !ReferenceEquals(activeRuntime, this))
            {
                _isDuplicateInstance = true;
                enabled = false;
                return;
            }

            if (!_registeredToSlowTickManager && Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                GlobalRegistry.RegisterSlowTickable(this, PriorityLayer.Environment);
                _registeredToSlowTickManager = GlobalRegistry.SlowTickables.Contains(this);
            }

            if (!_serviceRegistered && Application.isPlaying)
            {
                GlobalRegistry.RegisterScavengePopulatorRuntime(this);
                _serviceRegistered = ReferenceEquals(GlobalRegistry.ScavengePopulator, this);
            }

            if (_playerTransform == null)
                FindPlayer();
        }

        private void OnDisable()
        {
            if (_isDuplicateInstance || !_initialized)
                return;

            if (_registeredToSlowTickManager)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredToSlowTickManager = false;
            }

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterScavengePopulatorRuntime(this);
                _serviceRegistered = false;
            }

            DespawnAllChunks();
        }

        public void OnServiceShutdown()
        {
            if (_registeredToSlowTickManager)
            {
                GlobalRegistry.UnregisterSlowTickable(this, PriorityLayer.Environment);
                _registeredToSlowTickManager = false;
            }

            if (_serviceRegistered)
            {
                GlobalRegistry.UnregisterScavengePopulatorRuntime(this);
                _serviceRegistered = false;
            }

            DespawnAllChunks();
            _chunks?.Clear();
            _spawnQueue?.Clear();
            _chunksToUnload?.Clear();
            _idBuilder?.Clear();
        }

        private void OnDestroy()
        {
            OnServiceShutdown();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — Called from HectonScatterOutput
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Регистрирует одну точку спавна ресурсного узла.
        ///
        /// Вызывается из HectonScatterOutput.ApplyData.Apply()
        /// на главном потоке. Данные ставятся в очередь для
        /// time-sliced спавна через ProcessSpawnQueue().
        ///
        /// ZERO GC: SpawnRequest — struct, Enqueue — zero GC.
        /// Единственная аллокация — ChunkData при первом чанке.
        /// </summary>
        /// <param name="position">Мировая позиция спавна.</param>
        /// <param name="rotation">Поворот (обычно только Y-axis).</param>
        /// <param name="scale">Масштаб из scatter-данных.</param>
        /// <param name="chunkCoord">Координата чанка (tile grid).</param>
        /// <param name="localIndex">Индекс внутри чанка (для детерминированного ID).</param>
        /// <param name="context">
        /// Контекст спавна для выбора таблицы ресурсов.
        /// По умолчанию Surface для обратной совместимости с существующим scatter-пайплайном.
        /// </param>
        public void RegisterSpawnPoint(
            Vector3      position,
            Quaternion   rotation,
            Vector3      scale,
            Vector2Int   chunkCoord,
            int          localIndex,
            SpawnContext  context = SpawnContext.Surface)
        {
            // Ensure chunk tracking entry exists
            GetOrCreateChunk(chunkCoord, 256);

            SpawnRequest request = new SpawnRequest
            {
                position   = position,
                rotation   = rotation,
                scale      = scale,
                chunkCoord = chunkCoord,
                localIndex = localIndex,
                context    = context
            };

            _spawnQueue.Enqueue(request);
        }

        /// <summary>
        /// Подготавливает чанк к перезагрузке.
        /// Если чанк уже содержит узлы — деспавнит их.
        ///
        /// Вызывается из HectonScatterOutput ПЕРЕД серией
        /// RegisterSpawnPoint() вызовов для данного чанка.
        /// Это обрабатывает случай re-generate в MapMagic.
        /// </summary>
        /// <param name="chunkCoord">Координата чанка.</param>
        /// <param name="expectedCount">Ожидаемое количество узлов (для pre-alloc).</param>
        public void PrepareChunkForReload(Vector2Int chunkCoord, int expectedCount)
        {
            if (_chunks.TryGetValue(chunkCoord, out ChunkData existing))
            {
                if (existing.activeNodes.Count > 0)
                {
                    DespawnChunk(chunkCoord);
                }
            }

            // Удаляем pending spawns для этого чанка из очереди
            // (edge case: если предыдущая генерация ещё не была обработана)
            PurgePendingForChunk(chunkCoord);

            GetOrCreateChunk(chunkCoord, expectedCount);
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — TIME-SLICED PROCESSING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается GameTickManager каждые ~0.5 секунды.
        ///
        /// Порядок:
        ///   1. Обработка очереди спавна (time-sliced).
        ///   2. Culling далёких чанков.
        ///
        /// ZERO GC в горячем пути (Dequeue, struct math).
        /// StringBuilder.ToString() аллоцирует string — но только
        /// при фактическом спавне (не per-frame).
        /// </summary>
        public void SlowTick()
        {
            if (!_initialized || _spawnQueue == null || _chunks == null || _chunksToUnload == null)
                return;

            RefreshRuntimeStreamingSettings();
            ProcessSpawnQueue();
            CullDistantChunks();
            UpdateDiagnostics();
        }

        // ══════════════════════════════════════════════════════════
        //  SPAWN PROCESSING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обрабатывает до maxSpawnsPerTick запросов из очереди.
        ///
        /// Для каждого запроса:
        ///   1. Генерирует deterministic unique ID.
        ///   2. Проверяет WorldStateManager.IsNodeDepleted.
        ///   3. Если жив — спавнит через ObjectPoolManager.
        ///   4. Применяет scale из scatter-данных.
        ///   5. Настраивает ResourceNode.uniqueId.
        ///   6. Регистрирует в ChunkData.activeNodes.
        /// </summary>
        private void ProcessSpawnQueue()
        {
            if (_spawnQueue.Count == 0) return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;
            WorldStateManager wsm  = Hecton8.Core.GlobalRegistry.WorldState;

            if (pool == null) return;

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
                if (prefab == null) continue;

                // ── Spawn via pool ──
                GameObject instance = pool.Spawn(
                    prefab,
                    request.position,
                    request.rotation);

                if (instance == null) continue;

                // ── Apply scale from scatter data ──
                instance.transform.localScale = request.scale;

                // ── Configure ResourceNode ──
                ConfigureResourceNode(instance, uniqueId);

                // ── Register in chunk ──
                if (_chunks.TryGetValue(request.chunkCoord, out ChunkData chunk))
                {
                    ActiveNode node = new ActiveNode
                    {
                        gameObject = instance,
                        transform  = instance.transform,
                        uniqueId   = uniqueId
                    };

                    chunk.activeNodes.Add(node);
                }

                spawned++;
            }
        }

        /// <summary>
        /// Настраивает компонент ResourceNode на заспавненном объекте.
        /// Устанавливает uniqueId через публичный метод SetUniqueId().
        /// </summary>
        private static void ConfigureResourceNode(GameObject instance, string uniqueId)
        {
            if (instance.TryGetComponent(out ResourceNode node))
            {
                node.SetUniqueId(uniqueId);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  CULLING — DISTANT CHUNKS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет все загруженные чанки. Если центр чанка
        /// дальше unloadDistance от игрока — деспавнит все узлы чанка.
        ///
        /// Использует кэшированный _chunksToUnload для сбора ключей
        /// перед модификацией Dictionary.
        ///
        /// ZERO GC: Vector2Int — struct. sqrMagnitude — no sqrt.
        /// </summary>
        private void CullDistantChunks()
        {
            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null) return;
            }

            Vector3 playerPos = _playerTransform.position;
            Vector2 playerXZ  = new Vector2(playerPos.x, playerPos.z);

            _chunksToUnload.Clear();

            // ── Collect chunks to unload ──
            Dictionary<Vector2Int, ChunkData>.Enumerator enumerator = _chunks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<Vector2Int, ChunkData> kvp = enumerator.Current;
                ChunkData chunk = kvp.Value;
                if (!chunk.isLoaded) continue;
                if (chunk.activeNodes.Count == 0) continue;

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
        /// Получает или создаёт ChunkData для указанных координат.
        /// </summary>
        private ChunkData GetOrCreateChunk(Vector2Int coord, int expectedNodeCount)
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
        /// Деспавнит все узлы чанка. Возвращает объекты в пул.
        /// Сбрасывает масштаб перед возвратом.
        /// Помечает чанк как выгруженный (isLoaded = false).
        ///
        /// DOUBLE DESPAWN PROTECTION:
        ///   Объект возвращается в пул ТОЛЬКО если он ещё активен
        ///   в иерархии (activeInHierarchy == true).
        ///   Если объект уже неактивен — значит он был уничтожен
        ///   игроком (ResourceNode.TakeDamage → pool.Despawn),
        ///   и повторный Despawn вызовет ошибку / повреждение пула.
        /// </summary>
        private void DespawnChunk(Vector2Int coord)
        {
            if (!_chunks.TryGetValue(coord, out ChunkData chunk))
                return;

            ObjectPoolManager pool = GlobalRegistry.ObjectPool;

            List<ActiveNode> nodes = chunk.activeNodes;
            int count = nodes.Count;

            for (int i = count - 1; i >= 0; i--)
            {
                ActiveNode node = nodes[i];

                if (node.gameObject != null)
                {
                    // Защита от Double Despawn:
                    // Если объект уже выключен, значит он уже в пуле
                    // (уничтожен игроком через ResourceNode → pool.Despawn).
                    // Повторный Despawn пропускается.
                    if (node.gameObject.activeInHierarchy)
                    {
                        // Сбрасываем масштаб перед возвратом в пул
                        node.gameObject.transform.localScale = Vector3.one;

                        if (pool != null)
                        {
                            pool.Despawn(node.gameObject);
                        }
                        else
                        {
                            node.gameObject.SetActive(false);
                        }
                    }
                }
            }

            nodes.Clear();
            chunk.isLoaded = false;
        }

        /// <summary>
        /// Деспавнит ВСЕ чанки. Вызывается при OnDisable / смене сцены.
        /// </summary>
        private void DespawnAllChunks()
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
            }

            _chunks.Clear();
        }

        /// <summary>
        /// Удаляет из очереди спавна все pending-запросы для указанного чанка.
        /// 
        /// Используется при re-generate (MapMagic пересоздаёт тайл):
        /// старые pending-запросы должны быть отменены, иначе они
        /// заспавнятся поверх новых данных.
        ///
        /// GC NOTE: Создаёт временную очередь при наличии pending items.
        /// Вызывается редко (только при re-generate), поэтому допустимо.
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
        /// Генерирует детерминированный Unique ID для ResourceNode.
        ///
        /// Формат: "{prefix}_{chunkX}_{chunkZ}_{localIndex}"
        /// Пример: "rn_3_-2_47"
        ///
        /// ДЕТЕРМИНИЗМ: при одинаковых chunkCoord + localIndex
        /// всегда генерируется одинаковый ID. Гарантирует корректное
        /// восстановление depleted-состояния после save/load.
        ///
        /// GC: StringBuilder.ToString() аллоцирует string (~40 bytes).
        /// Вызывается ТОЛЬКО при спавне (не per-frame).
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
        /// Конвертирует мировую позицию в координату чанка (grid position).
        /// Deterministic: floor division.
        /// </summary>
        private Vector2Int WorldToChunkCoord(Vector3 worldPos)
        {
            int cx = Mathf.FloorToInt(worldPos.x / _runtimeTileSize);
            int cz = Mathf.FloorToInt(worldPos.z / _runtimeTileSize);
            return new Vector2Int(cx, cz);
        }

        /// <summary>
        /// Конвертирует координату чанка в центр чанка (world XZ).
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
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — QUERIES & CONTROL
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество загруженных чанков с активными узлами.</summary>
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

        /// <summary>Общее количество активных узлов во всех чанках.</summary>
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

        /// <summary>Количество запросов в очереди спавна.</summary>
        public int PendingSpawnCount => _spawnQueue.Count;

        public float UnloadDistance => _runtimeUnloadDistance;
        public float PriorityLoadRadius => _runtimePriorityLoadRadius;
        public int MaxSpawnsPerSlowTick => _runtimeMaxSpawnsPerTick;

        public void SetRuntimeBudget(float newUnloadDistance, float newPriorityLoadRadius, int newMaxSpawnsPerTick)
        {
            unloadDistance = Mathf.Max(50f, newUnloadDistance);
            priorityLoadRadius = Mathf.Max(10f, newPriorityLoadRadius);
            maxSpawnsPerTick = Mathf.Max(1, newMaxSpawnsPerTick);
            RefreshRuntimeStreamingSettings();
        }

        /// <summary>
        /// Принудительная перезагрузка чанка.
        /// Деспавнит все узлы и помечает для повторного заполнения.
        /// </summary>
        public void ReloadChunk(Vector2Int coord)
        {
            DespawnChunk(coord);
        }

        /// <summary>
        /// Принудительная выгрузка ВСЕХ чанков.
        /// Используется при телепорте, смене зоны.
        /// </summary>
        public void UnloadAll()
        {
            DespawnAllChunks();
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — DIRECTOR ORCHESTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Находит и подсвечивает ближайший ресурсный узел к указанной
        /// мировой позиции. Вызывается HectonDirectorAI при RareDiscovery.
        ///
        /// Алгоритм:
        ///   1. Итерация по всем записям Dictionary _chunks через foreach
        ///      (KeyValuePair — struct enumerator для Dictionary, допустимо).
        ///   2. Для каждого загруженного чанка — for-цикл по List&lt;ActiveNode&gt;.
        ///   3. sqrMagnitude сравнение — без sqrt.
        ///   4. Запоминаем узел с минимальным sqrMagnitude.
        ///   5. Если узел найден — TryGetComponent&lt;InteractionHighlighter&gt;
        ///      для включения подсветки.
        ///   6. Fallback: Debug.Log если визуальная система не готова.
        ///
        /// ZERO GC: struct math, no LINQ, no allocations.
        /// foreach по Dictionary допускается здесь, так как метод вызывается
        /// редко (раз в 30+ секунд по решению Директора), а не per-frame.
        /// </summary>
        /// <param name="worldHint">Мировая позиция подсказки (центр поиска).</param>
        public void HighlightNearbyResource(Vector3 worldHint)
        {
            // ── Поиск ближайшего активного узла ──
            float bestDistSqr       = float.MaxValue;
            GameObject bestNodeGO   = null;
            string bestNodeId       = null;

            Dictionary<Vector2Int, ChunkData>.Enumerator enumerator = _chunks.GetEnumerator();
            while (enumerator.MoveNext())
            {
                KeyValuePair<Vector2Int, ChunkData> kvp = enumerator.Current;
                ChunkData chunk = kvp.Value;

                // Пропускаем выгруженные чанки
                if (!chunk.isLoaded)
                    continue;

                List<ActiveNode> nodes = chunk.activeNodes;
                int nodeCount = nodes.Count;

                for (int i = 0; i < nodeCount; i++)
                {
                    ActiveNode node = nodes[i];

                    // Пропускаем уничтоженные/деактивированные узлы
                    if (node.gameObject == null)
                        continue;
                    if (!node.gameObject.activeInHierarchy)
                        continue;
                    if (node.transform == null)
                        continue;

                    // ── sqrMagnitude — без sqrt ──
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

            // ── Результат поиска ──
            if (bestNodeGO == null)
            {
#if UNITY_EDITOR
                Debug.Log("[ScavengePopulator] HighlightNearbyResource: " +
                          "no active nodes found near hint position.");
#endif
                return;
            }

            // ── Включение подсветки ──
            if (bestNodeGO.TryGetComponent(out InteractionHighlighter highlighter))
            {
                highlighter.SetHighlight(true);
            }
            else
            {
                // Визуальная система подсветки не до конца готова — логируем
                Debug.Log(
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
                Vector3 next = center + new Vector3(
                    Mathf.Cos(angle) * radius, 0f,
                    Mathf.Sin(angle) * radius);
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
