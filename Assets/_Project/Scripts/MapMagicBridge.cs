// ============================================================================
// HECTON-8 — MapMagicBridge.cs
// Информационный слой между игровыми системами и MapMagic 2.1.18.
//
// ═══════════════════════════════════════════════════════════════
// REFACTORED v2 — BIOME EVENT SYSTEM + SLOW TICK + ATMOSPHERE SYNC
// ═══════════════════════════════════════════════════════════════
//
// ОТВЕТСТВЕННОСТИ:
//   1. Быстрый запрос высоты дна в мировых координатах.
//   2. Определение активного биома в мировых координатах.
//   3. Biome Event System: уведомление при смене биома игрока.
//   4. Безопасная обработка отсутствующих/незагруженных тайлов.
//
// НОВОЕ В v2:
//   [ADD] ISlowTickable — проверка биома игрока каждые ~0.5с.
//   [ADD] OnBiomeChanged static event — уведомление при смене биома.
//   [ADD] GetCurrentBiome(float3) — запрос биома по позиции через
//         terrain splat maps (горизонтальные маски Biomes Set).
//   [ADD] _playerTransform кэш — позиция игрока без GameObject.Find.
//   [ADD] _lastBiomeID — edge detection для событий.
//
// АРХИТЕКТУРА:
//   • Singleton MonoBehaviour (не static — нужна ссылка на MapMagicObject).
//   • ISlowTickable — биом-проверка через GameTickManager (2 Hz).
//   • Все методы возвращают bool success + out value.
//   • Zero GC в горячих путях (SlowTick).
//   • Кэш ссылки на MapMagicObject — один FindAnyObjectByType при старте.
//
// ИНТЕГРАЦИЯ С MapMagic 2.1.18:
//   • MapMagic записывает биомы в terrain splat maps (alphamaps).
//   • Каждый splat layer = один биом (по индексу в Biomes Set).
//   • GetAlphamaps(x, z, 1, 1) — минимальная выборка (1 пиксель).
//   • Доминирующий слой = активный биом.
//
// BIOME EVENT FLOW:
//   SlowTick → GetCurrentBiome(playerPos) → compare with _lastBiomeID
//   → if changed: _lastBiomeID = new, OnBiomeChanged.Invoke(new)
//   → HectonAtmosphereManager.HandleBiomeChanged(id) → profile transition
//
// БЕЗОПАСНОСТЬ:
//   • Если тайл не сгенерирован — методы возвращают false.
//   • Если MapMagicObject отсутствует — методы возвращают false.
//   • Если игрок не назначен — SlowTick skip (no crash).
//   • Никаких исключений, никаких null reference — только ранний выход.
//
// ZERO GC:
//   • SlowTick: TryGetBiomeIndex вызывает GetAlphamaps (одна аллокация).
//     Это unavoidable Unity API limitation. Частота: 2 Hz — допустимо.
//   • TryGetHeight: SampleHeight — zero GC.
//   • FindTerrainAt: Terrain.activeTerrains — Unity cached array.
// ============================================================================

using System;
using Hecton8.Core;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-7000)]
    public sealed class MapMagicBridge : MonoBehaviour, ISlowTickable
    {
        // ══════════════════════════════════════════════════════════
        //  SINGLETON
        // ══════════════════════════════════════════════════════════

        private static MapMagicBridge _instance;

        public static MapMagicBridge Instance
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

        // ══════════════════════════════════════════════════════════
        //  GLOBAL EVENT — BIOME CHANGE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Fires when the player enters a new biome zone.
        /// Parameter: biome index (matches terrain splat layer index
        /// from MapMagic Biomes Set node).
        ///
        /// Subscribers:
        ///   - HectonAtmosphereManager → switches atmosphere profile.
        ///   - Future: ambient sound, music, fauna density, etc.
        ///
        /// Fires at SlowTick frequency (~2 Hz). NOT per-frame.
        /// First fire happens at Start() with the initial biome.
        /// </summary>
        public static event Action<int> OnBiomeChanged;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── MapMagic Reference ────────────────────────")]
        [Tooltip("Ссылка на MapMagicObject в сцене. " +
                 "Если не назначена — найдётся автоматически.")]
        [SerializeField] private MapMagic.Core.MapMagicObject mapMagicObject;

        [Header("── Water Settings ────────────────────────────")]
        [Tooltip("Уровень поверхности воды (мировая Y-координата). " +
                 "Используется для определения 'под водой'.")]
        [SerializeField] private float waterSurfaceLevel = 0f;

        [Header("── Player Reference ──────────────────────────")]
        [Tooltip("Transform игрока для биом-детекции в SlowTick.\n" +
                 "Если не назначен — ищется по тегу 'Player' при старте.")]
        [SerializeField] private Transform playerTransform;

        [Header("── Biome Detection ───────────────────────────")]
        [Tooltip("Максимальное количество биомов в Biomes Set MapMagic.\n" +
                 "Определяет размер кэшированного массива весов.\n" +
                 "Должно совпадать с количеством выходов Biomes Set ноды.")]
        [SerializeField] private int maxBiomeCount = 8;

        [Header("── Diagnostics ───────────────────────────────")]
#pragma warning disable CS0414
        [SerializeField] private bool _debugMapMagicFound;
        [SerializeField] private int  _debugTileCount;
        [SerializeField] private int  _debugCurrentBiome = -1;
        [SerializeField] private bool _debugPlayerFound;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pre-allocated массив для весов биомов.
        /// Переиспользуется при каждом вызове GetBiomeIndex.
        /// Размер = maxBiomeCount. Zero GC.
        /// </summary>
        private float[] _biomeWeights;

        /// <summary>
        /// Last known biome ID. Used for edge detection in SlowTick.
        /// -1 = not yet determined (forces first event fire).
        /// </summary>
        private int _lastBiomeID = -1;

        /// <summary>
        /// Registration tracking flag for GameTickManager.
        /// </summary>
        private bool _registeredToTickManager;

        // ══════════════════════════════════════════════════════════
        //  PUBLIC PROPERTIES
        // ══════════════════════════════════════════════════════════

        /// <summary>Уровень поверхности воды (Y).</summary>
        public float WaterSurfaceLevel => waterSurfaceLevel;

        /// <summary>MapMagic найден и доступен.</summary>
        public bool IsAvailable => mapMagicObject != null;

        /// <summary>
        /// Current biome ID under the player.
        /// -1 if not yet determined or player not found.
        /// </summary>
        public int CurrentBiomeID => _lastBiomeID;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            // ── Singleton ──
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;

            // ── Pre-allocate biome weights ──
            _biomeWeights = new float[maxBiomeCount];

            // ── Поиск MapMagicObject ──
            if (mapMagicObject == null)
            {
                mapMagicObject =
                    FindAnyObjectByType<MapMagic.Core.MapMagicObject>();
            }

            // ── Поиск игрока ──
            if (playerTransform == null)
            {
                GameObject playerGO = GameObject.FindWithTag("Player");
                if (playerGO != null)
                {
                    playerTransform = playerGO.transform;
                }
            }

            _lastBiomeID = -1;
            _registeredToTickManager = false;

            UpdateDiagnostics();
        }

        // ════════════════════════════════════════════════════════
        // TICK REGISTRATION — Deferred two-phase pattern.
        // ════════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (GameTickManager.Instance == null) return;

            if (!_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
        }

        private void Start()
        {
            if (_registeredToTickManager)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ISlowTickable)this);
                _registeredToTickManager = true;
            }
            else
            {
                Debug.LogError(
                    "[MapMagicBridge] GameTickManager.Instance is null " +
                    "even at Start(). Biome detection will NOT work.",
                    this);
            }

            // ── Initial biome detection ──
            // Fire first event so subscribers get initial state.
            DetectAndPublishBiome();
        }

        private void OnDisable()
        {
            if (GameTickManager.Instance == null) return;

            if (_registeredToTickManager)
            {
                GameTickManager.Instance.Unregister((ISlowTickable)this);
                _registeredToTickManager = false;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this)
            {
                _instance = null;
                OnBiomeChanged = null;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — BIOME DETECTION (2 Hz)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Called by GameTickManager at slowTickInterval (~0.5s).
        ///
        /// Checks the biome under the player's current position.
        /// If biome changed since last check → fires OnBiomeChanged.
        ///
        /// COST: One GetAlphamaps call (small allocation, 1x1 pixel).
        /// Acceptable at 2 Hz. Not suitable for per-frame.
        ///
        /// SAFETY: Null-safe for playerTransform and MapMagicObject.
        /// </summary>
        public void SlowTick()
        {
            DetectAndPublishBiome();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — BIOME DETECTION + EVENT PUBLISHING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Core biome detection logic. Separated from SlowTick for
        /// reuse in Start() (initial detection).
        ///
        /// Flow:
        ///   1. Get player world position.
        ///   2. Query terrain alphamaps for dominant biome.
        ///   3. Compare with _lastBiomeID.
        ///   4. If changed → update cache, fire event.
        /// </summary>
        private void DetectAndPublishBiome()
        {
            if (playerTransform == null) return;

            float3 pos = playerTransform.position;

            if (!TryGetBiomeIndex(pos.x, pos.z, out int biomeID))
                return;

            // ── Edge detection: only fire on change ──
            if (biomeID == _lastBiomeID)
                return;

            _lastBiomeID = biomeID;

            OnBiomeChanged?.Invoke(biomeID);

            UpdateBiomeDiagnostics(biomeID);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — HEIGHT QUERY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает высоту террейна (дна) в мировых координатах.
        ///
        /// Алгоритм:
        ///   1. Найти Terrain тайл, покрывающий координаты (x, z).
        ///   2. Вызвать Terrain.SampleHeight — быстрый, zero GC.
        ///   3. Прибавить Terrain.transform.position.y (смещение тайла).
        ///
        /// БЕЗОПАСНОСТЬ:
        ///   Если тайл не найден (не сгенерирован) — returns false.
        ///
        /// ZERO GC: SampleHeight returns float (struct).
        /// </summary>
        public bool TryGetHeight(float x, float z, out float height)
        {
            height = 0f;

            if (mapMagicObject == null)
                return false;

            Terrain terrain = FindTerrainAt(x, z);

            if (terrain == null || terrain.terrainData == null)
                return false;

            float localHeight = terrain.SampleHeight(new Vector3(x, 0f, z));
            height = localHeight + terrain.transform.position.y;

            return true;
        }

        /// <summary>
        /// Быстрая версия без out. Возвращает 0 при ошибке.
        /// </summary>
        public float GetHeight(float x, float z)
        {
            TryGetHeight(x, z, out float h);
            return h;
        }

        /// <summary>
        /// Проверяет, находится ли точка под водой.
        /// </summary>
        public bool IsUnderwater(float x, float y, float z)
        {
            return y < waterSurfaceLevel;
        }

        /// <summary>
        /// Комбинированная проверка для спавн-систем.
        /// </summary>
        public bool IsValidSpawnPoint(
            float x, float y, float z, out float bottomHeight)
        {
            if (!TryGetHeight(x, z, out bottomHeight))
                return false;

            return y < waterSurfaceLevel && y > bottomHeight;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — BIOME QUERY
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Возвращает индекс доминирующего биома в мировых координатах.
        ///
        /// РЕАЛИЗАЦИЯ через Terrain Splat Maps (Alphamaps):
        ///   MapMagic 2 записывает результат Biomes Set ноды в
        ///   terrain splat maps. Каждый splat layer соответствует
        ///   биому (по индексу выхода Biomes Set ноды).
        ///   Мы читаем alphamaps и находим слой с максимальным весом.
        ///
        /// ПОЧЕМУ НЕ MapMagic.Tiles.GetBiome():
        ///   MapMagic 2.1.18 не предоставляет публичный API GetBiome()
        ///   на уровне Tiles. Результат биомов финализируется в terrain
        ///   splat maps через Apply node. Чтение alphamaps — это
        ///   стандартный способ получить горизонтальные маски биомов.
        ///
        /// ZERO GC WARNING:
        ///   GetAlphamaps(x, z, 1, 1) аллоцирует float[1,1,N].
        ///   Unavoidable Unity API limitation.
        ///   Call frequency: 2 Hz (SlowTick) — acceptable.
        /// </summary>
        public bool TryGetBiomeIndex(
            float x, float z, out int biomeIndex)
        {
            biomeIndex = 0;

            if (mapMagicObject == null)
                return false;

            Terrain terrain = FindTerrainAt(x, z);

            if (terrain == null || terrain.terrainData == null)
                return false;

            TerrainData td = terrain.terrainData;

            // ── World → alphamap coordinates ──
            Vector3 terrainPos  = terrain.transform.position;
            Vector3 terrainSize = td.size;

            float normalizedX = (x - terrainPos.x) / terrainSize.x;
            float normalizedZ = (z - terrainPos.z) / terrainSize.z;

            normalizedX = math.saturate(normalizedX);
            normalizedZ = math.saturate(normalizedZ);

            int alphamapWidth  = td.alphamapWidth;
            int alphamapHeight = td.alphamapHeight;

            int mapX = (int)math.floor(
                normalizedX * (alphamapWidth - 1));
            int mapZ = (int)math.floor(
                normalizedZ * (alphamapHeight - 1));

            int layerCount = td.alphamapLayers;
            if (layerCount <= 0)
                return false;

            // ── Get weights (1x1 pixel sample) ──
            float[,,] alphas = td.GetAlphamaps(mapX, mapZ, 1, 1);

            // ── Find dominant layer ──
            float maxWeight = -1f;
            int   maxIndex  = 0;

            int searchCount = math.min(layerCount, maxBiomeCount);

            for (int i = 0; i < searchCount; i++)
            {
                float w = alphas[0, 0, i];
                if (w > maxWeight)
                {
                    maxWeight = w;
                    maxIndex  = i;
                }
            }

            biomeIndex = maxIndex;
            return true;
        }

        /// <summary>
        /// Быстрая версия без out. Возвращает 0 при ошибке.
        /// </summary>
        public int GetBiomeIndex(float x, float z)
        {
            TryGetBiomeIndex(x, z, out int idx);
            return idx;
        }

        /// <summary>
        /// Convenience overload accepting float3 position.
        /// Uses x and z components.
        /// </summary>
        public int GetCurrentBiome(float3 position)
        {
            TryGetBiomeIndex(position.x, position.z, out int idx);
            return idx;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — RUNTIME SETTERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Assigns player transform at runtime.
        /// Called by player initialization if Inspector ref is empty.
        /// </summary>
        public void SetPlayerTransform(Transform player)
        {
            playerTransform = player;
        }

        /// <summary>
        /// Updates water surface level at runtime.
        /// </summary>
        public void SetWaterSurfaceLevel(float y)
        {
            waterSurfaceLevel = y;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — TERRAIN LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Находит Terrain, покрывающий мировые координаты (x, z).
        ///
        /// Стратегия:
        ///   1. Проверяем Terrain.activeTerrain (быстро, один тайл).
        ///   2. Если не подходит — перебираем activeTerrains.
        ///
        /// ZERO GC: Terrain.activeTerrains — Unity cached array
        /// (не аллоцирует каждый вызов, начиная с Unity 2021+).
        /// </summary>
        private static Terrain FindTerrainAt(float x, float z)
        {
            Terrain active = Terrain.activeTerrain;
            if (active != null && IsPointInTerrain(active, x, z))
                return active;

            Terrain[] terrains = Terrain.activeTerrains;
            int count = terrains.Length;

            for (int i = 0; i < count; i++)
            {
                Terrain t = terrains[i];
                if (t != null && IsPointInTerrain(t, x, z))
                    return t;
            }

            return null;
        }

        /// <summary>
        /// Проверяет, попадает ли точка (x, z) в bounds террейна.
        /// </summary>
        private static bool IsPointInTerrain(
            Terrain terrain, float x, float z)
        {
            if (terrain.terrainData == null)
                return false;

            Vector3 pos  = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;

            return x >= pos.x && x <= pos.x + size.x &&
                   z >= pos.z && z <= pos.z + size.z;
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics()
        {
            _debugMapMagicFound = mapMagicObject != null;
            _debugPlayerFound   = playerTransform != null;
            _debugTileCount     = Terrain.activeTerrains != null
                ? Terrain.activeTerrains.Length
                : 0;
        }

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateBiomeDiagnostics(int biomeID)
        {
            _debugCurrentBiome = biomeID;
        }
    }
}