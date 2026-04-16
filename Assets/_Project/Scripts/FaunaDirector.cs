// ============================================================================
// HECTON-8 — FaunaDirector.cs
// Директор фауны — управляет спавном и деспавном подводных существ.
//
// ОТВЕТСТВЕННОСТИ:
//   1. Периодическая проверка позиции игрока (ISlowTickable).
//   2. Определение текущего биома через MapMagicBridge (с троттлингом).
//   3. Спавн существ в кольце вокруг игрока через ObjectPoolManager.
//   4. Culling: деспавн существ, уплывших за пределы killDistance.
//   5. Управление лимитами (глобальный max + per-type max).
//   6. Внешнее управление: ForceSpawnHorde, SetPredatorPressure
//      (оркестровка от HectonDirectorAI).
//
// АРХИТЕКТУРА:
//   • ISlowTickable — вызывается GameTickManager каждые ~0.5-1 сек.
//   • Pre-allocated List<ActiveCreature> — zero GC при итерации.
//   • Swap-remove при деспавне — O(1) без сдвига массива.
//   • Все distance-проверки через sqrMagnitude — без sqrt.
//   • Stateful counters — инкрементальный подсчёт O(1) вместо O(n).
//   • Biome throttling — TryGetBiomeIndex вызывается раз в 2 сек.
//
// СПАВН КОЛЬЦО:
//   • Внутренний радиус: 50м (не спавнить слишком близко).
//   • Внешний радиус: 150м (не спавнить слишком далеко).
//   • Высота: между дном + offset и поверхностью воды.
//
// HORDE SPAWN (ForceSpawnHorde):
//   • Вызывается HectonDirectorAI при Peak-событии.
//   • Спавнит 3-5 агрессивных существ в радиусе 10-15м от worldCenter.
//   • Игнорирует внутренние кулдауны — это приказ Директора.
//   • Немедленно устанавливает ForceState(Aggressive) на всех спавнов.
//   • Уважает _pressureEnabled флаг (Relax-фаза блокирует орды).
//
// PREDATOR PRESSURE (SetPredatorPressure):
//   • false: все активные существа переводятся в Wander (отступление).
//   • true: восстановление штатного AI behaviour.
//   • Управляется HectonDirectorAI при смене фаз.
//
// ZERO GC:
//   • ActiveCreature — struct (44 байта на стеке).
//   • List<ActiveCreature> — pre-allocated, без boxing.
//   • Mathf.Sin/Cos — returns float (struct).
//   • Random.Range — returns float/int (struct).
//   • Никаких foreach, никаких LINQ.
//   • Biome check throttled — GetAlphamaps аллокация раз в 2 сек.
// ============================================================================

using System.Collections.Generic;
using Hecton8.Core;
using Hecton8.Environment;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.AI
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-5000)]
    public sealed class FaunaDirector : MonoBehaviour, ISlowTickable
    {
        private const int CreaturePoolMinimumReserve = 8;
        private const int CreaturePoolBurstReserveMultiplier = 2;
        private const int SmallPassiveProxyMinimumReserve = 24;
        private const int SmallPassiveProxyBurstReserveMultiplier = 3;
        private const string SmallPassiveProxyPrefabName = "SmallPassiveProxy";
        private const float PlayerResolveRetryInterval = 1f;
        private const float RuntimeSettingsRefreshInterval = 5f;

        // ══════════════════════════════════════════════════════════
        //  ACTIVE CREATURE — struct tracker
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Запись об активном существе. Struct — zero GC при хранении в List.
        /// Хранит минимум данных для culling и accounting.
        /// </summary>
        private struct ActiveCreature
        {
            /// <summary>Ссылка на GameObject (из пула).</summary>
            public GameObject gameObject;

            /// <summary>Кэшированный Transform (avoid GetComponent per frame).</summary>
            public Transform transform;

            /// <summary>Индекс в FaunaBiomeData.possibleCreatures (для counting).</summary>
            public int creatureTypeIndex;

            /// <summary>Индекс биома, в котором был заспавнен.</summary>
            public int biomeIndex;

            /// <summary>Префаб-источник (для пула, если понадобится идентификация).</summary>
            public GameObject prefabSource;

            /// <summary>Чанк, в котором существо было заспавнено.</summary>
            public WorldChunkCoordinate chunkCoord;

            /// <summary>Большой участок воды, к которому привязана крупная угроза.</summary>
            public WorldMacroZoneCoordinate macroZoneCoord;

            /// <summary>Является ли это существо крупной угрозой большого участка воды.</summary>
            public bool isLargeThreat;
        }

        private struct ResolvedFaunaEntry
        {
            public GameObject prefab;
            public CreatureArchetypeData archetype;
            public float spawnWeight;
            public int maxAlive;
            public int creatureTypeIndex;
            public bool isLargeThreat;
            public bool blockedWhenPressureDisabled;
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DATASETS
        // ══════════════════════════════════════════════════════════

        [Header("── Biome Datasets ────────────────────────────")]
        [Tooltip("Данные фауны для каждого биома. " +
                 "Индексы biomeIndex должны соответствовать MapMagic Biomes Set.")]
        [SerializeField] private FaunaBiomeData[] biomeDatasets;

        [Header("── Chunk Streaming ───────────────────────────")]
        [Tooltip("Общий профиль чанкового мира. Если задан, фауна берёт из него размеры чанка, радиусы жизни и вместимость.")]
        [SerializeField] private WorldChunkStreamingProfile chunkStreamingProfile;
        [SerializeField] private WorldFaunaSpawnRegistry spawnRegistry;
        [SerializeField] private WorldProceduralStateRegistry proceduralStateRegistry;
        [SerializeField] private float ordinaryAnchorReuseCooldownSeconds = 90f;
        [SerializeField] private float largeThreatZoneReuseCooldownSeconds = 300f;
        [SerializeField] private BiomeMatrixDirector biomeMatrixDirector;
        [SerializeField] private WorldZoneDirector worldZoneDirector;

        [Header("── Ecology Cadence ─────────────────────")]
        [Tooltip("Global fauna budget scale when the active matrix biome reads as calm.")]
        [SerializeField, Range(0.5f, 1.5f)] private float calmFaunaBudgetScale = 0.9f;
        [Tooltip("Global fauna budget scale when the active matrix biome reads as lively.")]
        [SerializeField, Range(0.5f, 1.5f)] private float livelyFaunaBudgetScale = 1.2f;
        [Tooltip("Global fauna budget scale when the active matrix biome reads as mixed.")]
        [SerializeField, Range(0.5f, 1.5f)] private float mixedFaunaBudgetScale = 1f;
        [Tooltip("Global fauna budget scale when the active matrix biome reads as hostile.")]
        [SerializeField, Range(0.5f, 1.5f)] private float hostileFaunaBudgetScale = 0.78f;
        [Tooltip("Per-biome fauna cap scale when the active matrix biome reads as calm.")]
        [SerializeField, Range(0.5f, 1.5f)] private float calmBiomeCapScale = 0.92f;
        [Tooltip("Per-biome fauna cap scale when the active matrix biome reads as lively.")]
        [SerializeField, Range(0.5f, 1.5f)] private float livelyBiomeCapScale = 1.25f;
        [Tooltip("Per-biome fauna cap scale when the active matrix biome reads as mixed.")]
        [SerializeField, Range(0.5f, 1.5f)] private float mixedBiomeCapScale = 1f;
        [Tooltip("Per-biome fauna cap scale when the active matrix biome reads as hostile.")]
        [SerializeField, Range(0.5f, 1.5f)] private float hostileBiomeCapScale = 0.82f;

        [Header("── Ecology Composition ────────────────────")]
        [Tooltip("Passive fauna weight multiplier when the active matrix biome reads as calm.")]
        [SerializeField, Range(0.25f, 2f)] private float calmPassiveSelectionScale = 1.25f;
        [Tooltip("Aggressive fauna weight multiplier when the active matrix biome reads as calm.")]
        [SerializeField, Range(0.1f, 2f)] private float calmAggressiveSelectionScale = 0.58f;
        [Tooltip("Passive fauna weight multiplier when the active matrix biome reads as lively.")]
        [SerializeField, Range(0.25f, 2f)] private float livelyPassiveSelectionScale = 1.15f;
        [Tooltip("Aggressive fauna weight multiplier when the active matrix biome reads as lively.")]
        [SerializeField, Range(0.1f, 2f)] private float livelyAggressiveSelectionScale = 0.82f;
        [Tooltip("Passive fauna weight multiplier when the active matrix biome reads as mixed.")]
        [SerializeField, Range(0.25f, 2f)] private float mixedPassiveSelectionScale = 1f;
        [Tooltip("Aggressive fauna weight multiplier when the active matrix biome reads as mixed.")]
        [SerializeField, Range(0.1f, 2f)] private float mixedAggressiveSelectionScale = 1f;
        [Tooltip("Passive fauna weight multiplier when the active matrix biome reads as hostile.")]
        [SerializeField, Range(0.25f, 2f)] private float hostilePassiveSelectionScale = 0.72f;
        [Tooltip("Aggressive fauna weight multiplier when the active matrix biome reads as hostile.")]
        [SerializeField, Range(0.1f, 2f)] private float hostileAggressiveSelectionScale = 1.35f;
        [Tooltip("Passive fauna weight multiplier inside fabrication/service/support safe pockets.")]
        [SerializeField, Range(0.25f, 2f)] private float safePocketPassiveSelectionScale = 1.4f;
        [Tooltip("Aggressive fauna weight multiplier inside fabrication/service/support safe pockets.")]
        [SerializeField, Range(0.05f, 2f)] private float safePocketAggressiveSelectionScale = 0.4f;
        [Tooltip("Large-threat weight multiplier inside fabrication/service/support safe pockets.")]
        [SerializeField, Range(0f, 2f)] private float safePocketLargeThreatSelectionScale = 0.08f;
        [Tooltip("Passive fauna weight multiplier inside combat/trial water.")]
        [SerializeField, Range(0.1f, 2f)] private float hostileZonePassiveSelectionScale = 0.68f;
        [Tooltip("Aggressive fauna weight multiplier inside combat/trial water.")]
        [SerializeField, Range(0.1f, 2f)] private float hostileZoneAggressiveSelectionScale = 1.4f;
        [Tooltip("Large-threat weight multiplier inside combat/trial water.")]
        [SerializeField, Range(0f, 2f)] private float hostileZoneLargeThreatSelectionScale = 1.45f;
        [Tooltip("Large-threat weight multiplier while the player is on a route-critical lane. Keeps navigation legible.")]
        [SerializeField, Range(0f, 2f)] private float routeCriticalLargeThreatSelectionScale = 0.72f;

        [Header("── Adaptive Perf Budget ───────────────────")]
        [Tooltip("Scales fauna density from DynamicResolutionScaler render scale so weak devices shed live-world pressure before frame time collapses.")]
        [SerializeField] private bool enableAdaptivePerfBudget = true;
        [Tooltip("Render scale floor where fauna density reaches its lowest adaptive budget.")]
        [SerializeField, Range(0.5f, 1f)] private float adaptiveBudgetFloorRenderScale = 0.72f;
        [Tooltip("Minimum global fauna budget scale when adaptive perf response is fully engaged.")]
        [SerializeField, Range(0.2f, 1f)] private float adaptiveGlobalFaunaBudgetFloor = 0.58f;
        [Tooltip("Minimum per-biome fauna cap scale when adaptive perf response is fully engaged.")]
        [SerializeField, Range(0.2f, 1f)] private float adaptiveBiomeCapBudgetFloor = 0.62f;
        [Tooltip("Minimum spawn-burst scale when adaptive perf response is fully engaged.")]
        [SerializeField, Range(0.2f, 1f)] private float adaptiveSpawnBurstBudgetFloor = 0.4f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — LIMITS
        // ══════════════════════════════════════════════════════════

        [Header("── Global Limits ─────────────────────────────")]
        [Tooltip("Максимальное общее количество существ в мире.")]
        [SerializeField] private int globalMaxCount = 30;

        [Tooltip("Максимальное количество спавнов за один SlowTick. " +
                 "Предотвращает spike при входе в новый биом.")]
        [SerializeField] private int maxSpawnsPerTick = 3;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SPAWN RING
        // ══════════════════════════════════════════════════════════

        [Header("── Spawn Ring ────────────────────────────────")]
        [Tooltip("Минимальная дистанция спавна от игрока (метры).")]
        [SerializeField] private float spawnRingInner = 50f;

        [Tooltip("Максимальная дистанция спавна от игрока (метры).")]
        [SerializeField] private float spawnRingOuter = 150f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CULLING
        // ══════════════════════════════════════════════════════════

        [Header("── Culling ───────────────────────────────────")]
        [Tooltip("Дистанция от игрока, после которой существо деспавнится.")]
        [SerializeField] private float killDistance = 200f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — HORDE SETTINGS
        // ══════════════════════════════════════════════════════════

        [Header("── Horde Spawn (Director Command) ────────────")]
        [Tooltip("Минимальное количество существ в орде.")]
        [SerializeField] private int hordeCountMin = 3;

        [Tooltip("Максимальное количество существ в орде.")]
        [SerializeField] private int hordeCountMax = 5;

        [Tooltip("Минимальный радиус спавна орды от центра (метры).")]
        [SerializeField] private float hordeRadiusInner = 10f;

        [Tooltip("Максимальный радиус спавна орды от центра (метры).")]
        [SerializeField] private float hordeRadiusOuter = 15f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private int _debugActiveCount;
        [SerializeField] private int _debugCurrentBiome = -1;
        [SerializeField] private int _debugSpawnAttempts;
        [SerializeField] private int _debugCullCount;
        [SerializeField] private bool _debugPressureEnabled = true;
        [SerializeField] private int _debugLastHordeSpawned;
        [SerializeField] private int _debugActiveChunks;
        [SerializeField] private int _debugActiveMacroZones;
        [SerializeField] private int _debugRegistryFaunaAnchors;
        [SerializeField] private int _debugRegistryLargeThreatZones;
        [SerializeField] private string _debugCurrentChunk = "(0,0)";
        [SerializeField] private string _debugCurrentMacroZone = "(0,0)";
        [SerializeField] private float _debugRuntimeChunkSize = 192f;
        [SerializeField] private float _debugRuntimeMacroZoneSize = 768f;
        [SerializeField] private int _debugRuntimeGlobalMaxCount = 30;
        [SerializeField] private int _debugRuntimePerChunkMaxCount = 6;
        [SerializeField] private float _debugRuntimeSpawnOuter = 150f;
        [SerializeField] private float _debugRuntimeLargeThreatSpawnOuter = 420f;
        [SerializeField] private float _debugRuntimeCullDistance = 200f;
        [SerializeField] private float _debugRuntimeLargeThreatCullDistance = 900f;
        [SerializeField] private int _debugSpawnValidationAttempts;
        [SerializeField] private int _debugSpawnValidationSuccesses;
        [SerializeField] private int _debugAnchorBasedSpawns;
        [SerializeField] private int _debugFallbackRingSpawns;
        [SerializeField] private string _debugMatrixFaunaMood = "None";
        [SerializeField] private int _debugEffectiveGlobalMaxCount = 30;
        [SerializeField] private int _debugEffectiveSpawnsPerTick = 3;
        [SerializeField] private int _debugEffectiveBiomeMaxCount = 10;
        [SerializeField] private float _debugAdaptiveRenderScale = 1f;
        [SerializeField] private float _debugAdaptiveBudgetNormalized = 1f;
        [SerializeField] private float _debugAdaptiveGlobalBudgetScale = 1f;
        [SerializeField] private float _debugAdaptiveBiomeBudgetScale = 1f;
        [SerializeField] private float _debugAdaptiveSpawnBudgetScale = 1f;
        [SerializeField] private string _debugCurrentZone = "None";
        [SerializeField] private bool _debugCurrentZoneRouteCritical;
        [SerializeField] private bool _debugCurrentZoneSafePocket;
        [SerializeField] private float _debugPassiveSelectionScale = 1f;
        [SerializeField] private float _debugAggressiveSelectionScale = 1f;
        [SerializeField] private float _debugLargeThreatSelectionScale = 1f;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Pre-allocated список активных существ.
        /// Capacity = globalMaxCount. Никогда не превышает.
        /// Swap-remove при деспавне — порядок не важен.
        /// </summary>
        private List<ActiveCreature> _activeCreatures;

        /// <summary>Кэшированный Transform игрока.</summary>
        private Transform _playerTransform;

        /// <summary>Квадрат killDistance для sqrMagnitude.</summary>
        private float _killDistanceSqr;

        /// <summary>
        /// Lookup: biomeIndex → FaunaBiomeData.
        /// Pre-built в Awake. Dictionary&lt;int, FaunaBiomeData&gt;.
        /// Одна аллокация при старте.
        /// </summary>
        private Dictionary<int, FaunaBiomeData> _biomeLookup;
        private Dictionary<long, int> _countsPerChunk;
        private Dictionary<long, int> _largeThreatCountsPerMacroZone;
        private float _runtimeSpawnRingInner;
        private float _runtimeSpawnRingOuter;
        private float _runtimeLargeThreatSpawnInner;
        private float _runtimeLargeThreatSpawnOuter;
        private float _runtimeKillDistance;
        private float _runtimeKillDistanceSqr;
        private float _runtimeChunkSize = 192f;
        private float _runtimeMacroZoneSize = 768f;
        private float _runtimeLargeThreatKillDistance;
        private float _runtimeLargeThreatKillDistanceSqr;
        private int _runtimeGlobalMaxCount;
        private int _runtimeMaxSpawnsPerTick;
        private int _runtimePerChunkMaxCount;
        private int _runtimeMaxNearbyLargeThreats = 1;
        private int _runtimeFaunaAnchorChunkDistance = 2;
        private int _runtimeLargeThreatMacroZoneDistance = 1;

        // ══════════════════════════════════════════════════════════
        //  BIOME THROTTLING — снижение частоты GetAlphamaps
        // ══════════════════════════════════════════════════════════

        /// <summary>Absolute time gate for the next biome probe.</summary>
        private float _nextBiomeCheckTime = float.NegativeInfinity;

        /// <summary>Интервал проверки биома (секунды). Снижает GC от GetAlphamaps.</summary>
        private const float BiomeCheckInterval = 2.0f;

        /// <summary>Кэшированный результат последней проверки биома. -1 = не определён.</summary>
        private int _cachedBiomeIndex = -1;

        // ══════════════════════════════════════════════════════════
        //  STATEFUL COUNTERS — инкрементальный подсчёт O(1)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Счётчик существ по индексу биома.
        /// Размер = maxBiomeIndex + 1. Инкремент/декремент при спавне/деспавне.
        /// Заменяет O(n) CountBiomeCreatures.
        /// </summary>
        private int[] _countsPerBiome;

        /// <summary>
        /// Счётчик существ по типам для каждого биома.
        /// Ключ = FaunaBiomeData, Значение = int[possibleCreatures.Count].
        /// Заменяет O(n) CountCreatureTypes.
        /// </summary>
        private Dictionary<FaunaBiomeData, int[]> _countsPerTypePerBiome;
        private Dictionary<FaunaBiomeData, ResolvedFaunaEntry[]> _resolvedEntriesPerBiome;
        private Dictionary<FaunaBiomeData, int[]> _availablePoolCountsPerBiome;
        private Dictionary<FaunaBiomeData, Dictionary<GameObject, int>> _prefabTypeIndexLookup;
        private float _nextPlayerResolveTime = float.NegativeInfinity;
        private float _nextRuntimeSettingsRefreshTime = float.NegativeInfinity;
        private bool _runtimeSettingsDirty = true;
        private WorldProceduralFaunaMood _currentMatrixFaunaMood;
        private int _currentEffectiveGlobalMaxCount = 30;
        private int _currentEffectiveSpawnsPerTick = 3;
        private int _currentEffectiveBiomeMaxCount = 10;
        private float _adaptiveBudgetNormalized = 1f;
        private float _adaptiveGlobalBudgetScale = 1f;
        private float _adaptiveBiomeBudgetScale = 1f;
        private float _adaptiveSpawnBudgetScale = 1f;
        private float _currentPassiveSelectionScale = 1f;
        private float _currentAggressiveSelectionScale = 1f;
        private float _currentLargeThreatSelectionScale = 1f;
        private WorldZoneAnchor _currentZone;
        private bool _currentZoneIsSafePocket;

        // ══════════════════════════════════════════════════════════
        //  PREDATOR PRESSURE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Флаг разрешения хищного давления.
        /// false = Relax-фаза: существа переведены в Wander, орды запрещены.
        /// true  = штатный режим: нормальный AI behaviour, орды разрешены.
        /// Управляется через SetPredatorPressure() из HectonDirectorAI.
        /// Default = true (давление разрешено при старте).
        /// </summary>
        private bool _pressureEnabled = true;
        private bool _creaturePoolsWarmed;
        private int _defaultCreaturePoolWarmupReserve;
        private int _smallPassiveCreaturePoolWarmupReserve;
        private readonly HashSet<GameObject> _warmupPrefabs = new HashSet<GameObject>(128); // COLD ALLOC: dedupe fauna pool warmup prefabs across biome datasets
        internal static FaunaDirector ActiveRuntimeInstance { get; private set; }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            ActiveRuntimeInstance = this;
            EnsureRuntimeStateInitialized();
            ResolveSpawnRegistry();
            ResolveBiomeMatrixDirector();
            ResolveWorldZoneDirector();
            RefreshRuntimeStreamingSettings();
            TryWarmupCreaturePools();
            _runtimeSettingsDirty = false;
            _nextRuntimeSettingsRefreshTime = Time.time + RuntimeSettingsRefreshInterval;
        }

        private void OnEnable()
        {
            GameTickManager.Instance?.Register((ISlowTickable)this);

            if (_playerTransform == null)
                FindPlayer(true);

            if (spawnRegistry == null)
                ResolveSpawnRegistry();

            if (biomeMatrixDirector == null)
                ResolveBiomeMatrixDirector();

            if (worldZoneDirector == null)
                ResolveWorldZoneDirector();

            if (!_creaturePoolsWarmed)
                TryWarmupCreaturePools();
        }

        private void OnDisable()
        {
            GameTickManager.Instance?.Unregister((ISlowTickable)this);
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
        }

        // ══════════════════════════════════════════════════════════
        //  ISlowTickable — MAIN LOOP (~раз в 0.5-1 секунду)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Главный цикл Директора. Порядок:
        ///   1. Проверка наличия игрока.
        ///   2. Culling (деспавн далёких существ).
        ///   3. Определение биома (с троттлингом).
        ///   4. Спавн новых существ (если есть слоты).
        ///
        /// ZERO GC: struct math, pre-allocated collections, no LINQ.
        /// Biome check throttled — GetAlphamaps вызывается раз в BiomeCheckInterval.
        /// </summary>
        public void SlowTick()
        {
            EnsureRuntimeStateInitialized();
            ResolveBiomeMatrixDirector();
            ResolveWorldZoneDirector();
            if (!_creaturePoolsWarmed)
                TryWarmupCreaturePools();
            if (_runtimeSettingsDirty || Time.time >= _nextRuntimeSettingsRefreshTime)
            {
                RefreshRuntimeStreamingSettings();
                _runtimeSettingsDirty = false;
                _nextRuntimeSettingsRefreshTime = Time.time + RuntimeSettingsRefreshInterval;
            }
            // ══════════════════════════════════════════════════════
            //  1. PLAYER CHECK
            // ══════════════════════════════════════════════════════

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null)
                    return;
            }

            // Unity null check (player could be destroyed)
            if (_playerTransform == null)
            {
                _playerTransform = null;
                _nextPlayerResolveTime = 0f;
                return;
            }

            Vector3 playerPos = _playerTransform.position;
            int spawnValidationAttempts = 0;
            int spawnValidationSuccesses = 0;
            int anchorBasedSpawns = 0;
            int fallbackRingSpawns = 0;

            // ══════════════════════════════════════════════════════
            //  2. CULLING — деспавн далёких существ
            // ══════════════════════════════════════════════════════

            int cullCount = CullDistantCreatures(playerPos);

            // ══════════════════════════════════════════════════════
            //  3. BIOME DETECTION (THROTTLED)
            //     GetAlphamaps вызывает GC-аллокацию, поэтому
            //     проверяем биом раз в BiomeCheckInterval секунд.
            // ══════════════════════════════════════════════════════

            MapMagicBridge bridge = MapMagicBridge.Instance;
            if (bridge == null)
            {
                UpdateDiagnostics(cullCount, 0, spawnValidationAttempts, spawnValidationSuccesses, anchorBasedSpawns, fallbackRingSpawns);
                return;
            }

            if (Time.time >= _nextBiomeCheckTime)
            {
                _nextBiomeCheckTime = Time.time + BiomeCheckInterval;
                if (bridge.TryGetBiomeIndex(playerPos.x, playerPos.z, out int biome))
                {
                    _cachedBiomeIndex = biome;
                }
            }

            int currentBiome = _cachedBiomeIndex;
            if (currentBiome == -1)
            {
                // Биом ещё не определён — пропускаем спавн
                UpdateDiagnostics(cullCount, 0, spawnValidationAttempts, spawnValidationSuccesses, anchorBasedSpawns, fallbackRingSpawns);
                return;
            }

            // ══════════════════════════════════════════════════════
            //  4. SPAWN — если есть свободные слоты
            // ══════════════════════════════════════════════════════

            int spawnAttempts = 0;
            _currentMatrixFaunaMood = biomeMatrixDirector != null && biomeMatrixDirector.CurrentProfile != null
                ? biomeMatrixDirector.CurrentProfile.faunaMood
                : WorldProceduralFaunaMood.None;
            _currentZone = worldZoneDirector != null ? worldZoneDirector.CurrentZone : null;
            _currentZoneIsSafePocket = IsSafePocketZone(_currentZone);

            RefreshAdaptiveBudgetResponse();
            RefreshEcologyCompositionResponse(_currentMatrixFaunaMood, _currentZone);
            _currentEffectiveGlobalMaxCount = ResolveEffectiveGlobalMaxCount(_currentMatrixFaunaMood);
            _currentEffectiveSpawnsPerTick = ResolveEffectiveSpawnsPerTick(_currentMatrixFaunaMood);
            _currentEffectiveBiomeMaxCount = 0;

            if (_activeCreatures.Count < _currentEffectiveGlobalMaxCount)
            {
                // Ищем данные биома
                if (_biomeLookup.TryGetValue(currentBiome, out FaunaBiomeData biomeData))
                {
                    _currentEffectiveBiomeMaxCount = ResolveEffectiveBiomeMaxCount(biomeData, _currentMatrixFaunaMood);
                    spawnAttempts = TrySpawnCreatures(
                        biomeData,
                        playerPos,
                        bridge,
                        ref spawnValidationAttempts,
                        ref spawnValidationSuccesses,
                        ref anchorBasedSpawns,
                        ref fallbackRingSpawns);
                }
            }

            UpdateDiagnostics(cullCount, spawnAttempts, spawnValidationAttempts, spawnValidationSuccesses, anchorBasedSpawns, fallbackRingSpawns);
        }

        // ══════════════════════════════════════════════════════════
        //  CULLING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проходит по всем активным существам. Если расстояние
        /// до игрока > killDistance — деспавнит через пул.
        ///
        /// Обратный for-цикл + swap-remove: O(n), zero GC,
        /// не пропускает элементы, не сдвигает массив.
        ///
        /// При удалении декрементирует stateful-счётчики
        /// (_countsPerBiome, _countsPerTypePerBiome).
        ///
        /// Дополнительно проверяет null (объект мог быть уничтожен
        /// внешней системой).
        /// </summary>
        /// <param name="playerPos">Текущая позиция игрока.</param>
        /// <returns>Количество деспавненных существ.</returns>
        private int CullDistantCreatures(Vector3 playerPos)
        {
            if (_activeCreatures == null || _activeCreatures.Count == 0)
                return 0;

            int culled = 0;
            ObjectPoolManager pool = ObjectPoolManager.Instance;

            for (int i = _activeCreatures.Count - 1; i >= 0; i--)
            {
                ActiveCreature creature = _activeCreatures[i];

                // ── Null check (destroyed externally) ──
                if (creature.gameObject == null || creature.transform == null)
                {
                    DecrementCreatureCounters(in creature);
                    SwapRemoveAt(i);
                    culled++;
                    continue;
                }

                // ── Deactivated externally (e.g. by AI self-despawn) ──
                if (!creature.gameObject.activeInHierarchy)
                {
                    DecrementCreatureCounters(in creature);
                    SwapRemoveAt(i);
                    culled++;
                    continue;
                }

                // ── Distance check ──
                Vector3 diff = creature.transform.position - playerPos;
                float cullDistanceSqr = creature.isLargeThreat
                    ? _runtimeLargeThreatKillDistanceSqr
                    : _runtimeKillDistanceSqr;

                if (diff.sqrMagnitude > cullDistanceSqr)
                {
                    // Деспавн через пул
                    if (pool != null)
                    {
                        pool.Despawn(creature.gameObject);
                    }

                    DecrementCreatureCounters(in creature);
                    SwapRemoveAt(i);
                    culled++;
                }
            }

            return culled;
        }

        // ══════════════════════════════════════════════════════════
        //  SPAWN
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Пытается заспавнить существ в текущем биоме.
        ///
        /// Алгоритм:
        ///   1. Получить текущие счётчики из stateful-структур (O(1)).
        ///   2. Цикл до maxSpawnsPerTick (или пока не заполнен globalMaxCount).
        ///   3. Выбрать случайную точку в кольце вокруг игрока.
        ///   4. Проверить высоту дна через MapMagicBridge.
        ///   5. Проверить что точка под водой.
        ///   6. Выбрать тип существа через weighted random.
        ///   7. Спавн через ObjectPoolManager.
        ///   8. Инкрементировать stateful-счётчики.
        ///
        /// ZERO GC: Mathf.Sin/Cos → float, Random.Range → float,
        /// stateful counters (no per-tick O(n) scan), struct ActiveCreature.
        /// </summary>
        private int TrySpawnCreatures(
            FaunaBiomeData biomeData,
            Vector3 playerPos,
            MapMagicBridge bridge,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses,
            ref int anchorBasedSpawns,
            ref int fallbackRingSpawns)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null) return 0;

            int biomeIdx = biomeData.biomeIndex;

            // ── Получение счётчиков из stateful-структур (O(1)) ──
            int biomeAlive = (biomeIdx >= 0 && biomeIdx < _countsPerBiome.Length)
                ? _countsPerBiome[biomeIdx]
                : 0;

            if (biomeAlive >= _currentEffectiveBiomeMaxCount)
                return 0;

            // Массив per-type counts для этого биома (ссылка, не копия)
            if (!_countsPerTypePerBiome.TryGetValue(biomeData, out int[] creatureTypeCounts))
                return 0;
            if (_resolvedEntriesPerBiome == null ||
                !_resolvedEntriesPerBiome.TryGetValue(biomeData, out ResolvedFaunaEntry[] resolvedEntries) ||
                resolvedEntries == null ||
                resolvedEntries.Length == 0)
            {
                return 0;
            }
            if (_availablePoolCountsPerBiome == null ||
                !_availablePoolCountsPerBiome.TryGetValue(biomeData, out int[] availablePoolCounts) ||
                availablePoolCounts == null ||
                availablePoolCounts.Length < resolvedEntries.Length)
            {
                return 0;
            }

            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                GameObject prefab = resolvedEntries[i].prefab;
                availablePoolCounts[i] = prefab != null ? pool.GetAvailableCount(prefab) : 0;
            }

            int spawned = 0;

            for (int attempt = 0; attempt < _currentEffectiveSpawnsPerTick; attempt++)
            {
                // Global limit
                if (_activeCreatures.Count >= _currentEffectiveGlobalMaxCount)
                    break;

                // Biome limit
                if (biomeAlive >= _currentEffectiveBiomeMaxCount)
                    break;

                if (!TrySelectResolvedEntry(
                    resolvedEntries,
                    creatureTypeCounts,
                    availablePoolCounts,
                    _pressureEnabled,
                    _currentPassiveSelectionScale,
                    _currentAggressiveSelectionScale,
                    _currentLargeThreatSelectionScale,
                    out ResolvedFaunaEntry selectedEntry))
                {
                    // Все типы на лимите — прекращаем
                    break;
                }

                bool isLargeThreat = selectedEntry.isLargeThreat;
                Vector3 spawnPos;
                WorldMacroZoneCoordinate spawnMacroZone = default;
                WorldFaunaSpawnRegistry.Anchor sourceAnchor = default;
                bool usedRegistryAnchor = false;
                bool hasSpawnPoint = isLargeThreat
                    ? TryResolveLargeThreatSpawnLocation(
                        playerPos,
                        bridge,
                        biomeData,
                        out spawnPos,
                        out spawnMacroZone,
                        out sourceAnchor,
                        out usedRegistryAnchor,
                        ref spawnValidationAttempts,
                        ref spawnValidationSuccesses,
                        ref anchorBasedSpawns,
                        ref fallbackRingSpawns)
                    : TryResolveOrdinarySpawnLocation(
                        playerPos,
                        bridge,
                        biomeData,
                        out spawnPos,
                        out sourceAnchor,
                        out usedRegistryAnchor,
                        ref spawnValidationAttempts,
                        ref spawnValidationSuccesses,
                        ref anchorBasedSpawns,
                        ref fallbackRingSpawns);
                if (!hasSpawnPoint)
                    continue;

                Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                WorldChunkCoordinate spawnChunk = WorldChunkCoordinate.FromWorldPosition(spawnPos, _runtimeChunkSize);

                if (GetChunkCreatureCount(spawnChunk) >= _runtimePerChunkMaxCount)
                    continue;

                if (isLargeThreat)
                {
                    if (!CanSpawnLargeThreatNearPlayer(spawnMacroZone, playerPos))
                        continue;
                }

                GameObject resolvedPrefab = selectedEntry.prefab;
                if (resolvedPrefab == null)
                    continue;

                GameObject instance = pool.Spawn(resolvedPrefab, spawnPos, spawnRot, false);

                if (instance == null)
                    continue;
                if (usedRegistryAnchor && sourceAnchor.runtimeKey != 0L)
                {
                    ResolveProceduralStateRegistry();
                    proceduralStateRegistry?.MarkFaunaAnchorUsed(
                        sourceAnchor.runtimeKey,
                        sourceAnchor.isLargeThreatZone,
                        isLargeThreat
                            ? Mathf.Max(0f, largeThreatZoneReuseCooldownSeconds)
                            : Mathf.Max(0f, ordinaryAnchorReuseCooldownSeconds));
                }


                // ── Настройка спавн-поинта для AI ──
                if (instance.TryGetComponent(out HectonBaseAI ai))
                {
                    ai.ApplyArchetype(selectedEntry.archetype);
                    ai.SetSpawnPoint(spawnPos);
                }

                int typeIndex = selectedEntry.creatureTypeIndex;

                // ── Регистрация в трекере ──
                ActiveCreature record = new ActiveCreature
                {
                    gameObject        = instance,
                    transform         = instance.transform,
                    creatureTypeIndex  = typeIndex,
                    biomeIndex        = biomeIdx,
                    prefabSource      = resolvedPrefab,
                    chunkCoord        = spawnChunk,
                    macroZoneCoord    = spawnMacroZone,
                    isLargeThreat     = isLargeThreat
                };

                _activeCreatures.Add(record);

                // ── Инкремент stateful-счётчиков ──
                if (biomeIdx >= 0 && biomeIdx < _countsPerBiome.Length)
                    _countsPerBiome[biomeIdx]++;

                if (typeIndex >= 0 && typeIndex < creatureTypeCounts.Length)
                    creatureTypeCounts[typeIndex]++;
                if (typeIndex >= 0 && typeIndex < availablePoolCounts.Length && availablePoolCounts[typeIndex] > 0)
                    availablePoolCounts[typeIndex]--;

                IncrementChunkCount(spawnChunk);
                if (isLargeThreat)
                    IncrementMacroZoneCount(spawnMacroZone);

                biomeAlive++;
                spawned++;
            }

            return spawned;
        }

        // ══════════════════════════════════════════════════════════
        //  STATEFUL COUNTER HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Декрементирует stateful-счётчики при удалении существа.
        /// Вызывается из CullDistantCreatures перед SwapRemoveAt.
        /// O(1). Zero GC.
        /// </summary>
        private bool TryResolveOrdinarySpawnLocation(
            Vector3 playerPos,
            MapMagicBridge bridge,
            FaunaBiomeData biomeData,
            out Vector3 spawnPos,
            out WorldFaunaSpawnRegistry.Anchor registryAnchor,
            out bool usedRegistryAnchor,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses,
            ref int anchorBasedSpawns,
            ref int fallbackRingSpawns)
        {
            ResolveSpawnRegistry();

            if (spawnRegistry != null)
            {
                WorldChunkCoordinate playerChunk = WorldChunkCoordinate.FromWorldPosition(playerPos, _runtimeChunkSize);
                if (spawnRegistry.TryGetOrdinaryAnchor(playerPos, playerChunk, _runtimeFaunaAnchorChunkDistance, out WorldFaunaSpawnRegistry.Anchor anchor) &&
                    TryBuildSpawnPointAroundAnchor(anchor.position, anchor.radius, biomeData, bridge, out spawnPos, ref spawnValidationAttempts, ref spawnValidationSuccesses))
                {
                    registryAnchor = anchor;
                    usedRegistryAnchor = true;
                    anchorBasedSpawns++;
                    return true;
                }
            }

            registryAnchor = default;
            usedRegistryAnchor = false;
            bool builtInRing = TryBuildSpawnPointInRing(
                playerPos,
                _runtimeSpawnRingInner,
                _runtimeSpawnRingOuter,
                biomeData,
                bridge,
                out spawnPos,
                ref spawnValidationAttempts,
                ref spawnValidationSuccesses);
            if (builtInRing)
                fallbackRingSpawns++;

            return builtInRing;
        }

        private bool TryResolveLargeThreatSpawnLocation(
            Vector3 playerPos,
            MapMagicBridge bridge,
            FaunaBiomeData biomeData,
            out Vector3 spawnPos,
            out WorldMacroZoneCoordinate macroZoneCoord,
            out WorldFaunaSpawnRegistry.Anchor registryAnchor,
            out bool usedRegistryAnchor,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses,
            ref int anchorBasedSpawns,
            ref int fallbackRingSpawns)
        {
            ResolveSpawnRegistry();

            if (spawnRegistry != null)
            {
                WorldMacroZoneCoordinate playerMacroZone = WorldMacroZoneCoordinate.FromWorldPosition(playerPos, _runtimeMacroZoneSize);
                if (spawnRegistry.TryGetLargeThreatZone(playerPos, playerMacroZone, _runtimeLargeThreatMacroZoneDistance, out WorldFaunaSpawnRegistry.Anchor zoneAnchor) &&
                    CanSpawnLargeThreatNearPlayer(zoneAnchor.macroZoneCoord, playerPos) &&
                    TryBuildSpawnPointAroundAnchor(zoneAnchor.position, zoneAnchor.radius, biomeData, bridge, out spawnPos, ref spawnValidationAttempts, ref spawnValidationSuccesses))
                {
                    macroZoneCoord = zoneAnchor.macroZoneCoord;
                    registryAnchor = zoneAnchor;
                    usedRegistryAnchor = true;
                    anchorBasedSpawns++;
                    return true;
                }
            }

            if (TryBuildSpawnPointInRing(
                    playerPos,
                    _runtimeLargeThreatSpawnInner,
                    _runtimeLargeThreatSpawnOuter,
                    biomeData,
                    bridge,
                    out spawnPos,
                    ref spawnValidationAttempts,
                    ref spawnValidationSuccesses))
            {
                macroZoneCoord = WorldMacroZoneCoordinate.FromWorldPosition(spawnPos, _runtimeMacroZoneSize);
                registryAnchor = default;
                usedRegistryAnchor = false;
                fallbackRingSpawns++;
                return CanSpawnLargeThreatNearPlayer(macroZoneCoord, playerPos);
            }

            spawnPos = default;
            macroZoneCoord = default;
            registryAnchor = default;
            usedRegistryAnchor = false;
            return false;
        }

        private static bool TryBuildSpawnPointInRing(
            Vector3 center,
            float innerRadius,
            float outerRadius,
            FaunaBiomeData biomeData,
            MapMagicBridge bridge,
            out Vector3 spawnPos,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses)
        {
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(innerRadius, outerRadius);
                Vector3 candidateCenter = new Vector3(
                    center.x + Mathf.Cos(angle) * distance,
                    center.y,
                    center.z + Mathf.Sin(angle) * distance);

                if (TryBuildValidatedSpawnPoint(candidateCenter, biomeData, bridge, out spawnPos, ref spawnValidationAttempts, ref spawnValidationSuccesses))
                    return true;
            }

            spawnPos = default;
            return false;
        }

        private static bool TryBuildSpawnPointAroundAnchor(
            Vector3 anchorPosition,
            float anchorRadius,
            FaunaBiomeData biomeData,
            MapMagicBridge bridge,
            out Vector3 spawnPos,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses)
        {
            float safeRadius = Mathf.Max(6f, anchorRadius);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(0f, safeRadius);
                Vector3 candidateCenter = new Vector3(
                    anchorPosition.x + Mathf.Cos(angle) * distance,
                    anchorPosition.y,
                    anchorPosition.z + Mathf.Sin(angle) * distance);

                if (TryBuildValidatedSpawnPoint(candidateCenter, biomeData, bridge, out spawnPos, ref spawnValidationAttempts, ref spawnValidationSuccesses))
                    return true;
            }

            spawnPos = default;
            return false;
        }

        private static bool TryBuildValidatedSpawnPoint(
            Vector3 candidateCenter,
            FaunaBiomeData biomeData,
            MapMagicBridge bridge,
            out Vector3 spawnPos,
            ref int spawnValidationAttempts,
            ref int spawnValidationSuccesses)
        {
            spawnValidationAttempts++;
            if (!bridge.TryGetHeight(candidateCenter.x, candidateCenter.z, out float bottomHeight))
            {
                spawnPos = default;
                return false;
            }

            float spawnY = biomeData.GetRandomSpawnHeight(bottomHeight);
            if (spawnY >= bridge.WaterSurfaceLevel || spawnY <= bottomHeight)
            {
                spawnPos = default;
                return false;
            }

            spawnPos = new Vector3(candidateCenter.x, spawnY, candidateCenter.z);
            spawnValidationSuccesses++;
            return true;
        }

        private static bool TrySelectResolvedEntry(
            ResolvedFaunaEntry[] resolvedEntries,
            int[] currentCounts,
            int[] availablePoolCounts,
            bool pressureEnabled,
            float passiveSelectionScale,
            float aggressiveSelectionScale,
            float largeThreatSelectionScale,
            out ResolvedFaunaEntry selectedEntry)
        {
            selectedEntry = default;
            if (resolvedEntries == null || resolvedEntries.Length == 0 || availablePoolCounts == null || availablePoolCounts.Length < resolvedEntries.Length)
                return false;

            float availableWeight = 0f;
            int fallbackIndex = -1;
            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry entry = resolvedEntries[i];
                if (entry.prefab == null || entry.spawnWeight <= 0f)
                    continue;
                if (!pressureEnabled && entry.blockedWhenPressureDisabled)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (currentCounts != null &&
                    typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[i] <= 0)
                    continue;

                float selectionWeight = ResolveSelectionWeight(entry, passiveSelectionScale, aggressiveSelectionScale, largeThreatSelectionScale);
                if (selectionWeight <= 0f)
                    continue;

                availableWeight += selectionWeight;
                fallbackIndex = i;
            }

            if (availableWeight <= 0f)
                return false;

            float roll = Random.Range(0f, availableWeight);
            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                ResolvedFaunaEntry entry = resolvedEntries[i];
                if (entry.prefab == null || entry.spawnWeight <= 0f)
                    continue;
                if (!pressureEnabled && entry.blockedWhenPressureDisabled)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (currentCounts != null &&
                    typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[i] <= 0)
                    continue;

                float selectionWeight = ResolveSelectionWeight(entry, passiveSelectionScale, aggressiveSelectionScale, largeThreatSelectionScale);
                if (selectionWeight <= 0f)
                    continue;

                roll -= selectionWeight;
                if (roll <= 0f)
                {
                    selectedEntry = entry;
                    return true;
                }
            }

            if (fallbackIndex >= 0)
            {
                selectedEntry = resolvedEntries[fallbackIndex];
                return true;
            }

            return false;
        }

        private static bool TrySelectResolvedHordeEntry(
            ResolvedFaunaEntry[] resolvedEntries,
            int[] currentCounts,
            int[] availablePoolCounts,
            out ResolvedFaunaEntry selectedEntry)
        {
            selectedEntry = default;
            if (resolvedEntries == null || resolvedEntries.Length == 0 || availablePoolCounts == null || availablePoolCounts.Length < resolvedEntries.Length)
                return false;

            int startIndex = Random.Range(0, resolvedEntries.Length);
            for (int search = 0; search < resolvedEntries.Length; search++)
            {
                int index = startIndex + search;
                if (index >= resolvedEntries.Length)
                    index -= resolvedEntries.Length;

                ResolvedFaunaEntry entry = resolvedEntries[index];
                if (entry.prefab == null || entry.isLargeThreat)
                    continue;

                int typeIndex = entry.creatureTypeIndex;
                if (currentCounts != null &&
                    typeIndex >= 0 &&
                    typeIndex < currentCounts.Length &&
                    currentCounts[typeIndex] >= entry.maxAlive)
                {
                    continue;
                }

                if (availablePoolCounts[index] <= 0)
                    continue;

                selectedEntry = entry;
                return true;
            }

            return false;
        }

        private void DecrementCreatureCounters(in ActiveCreature creature)
        {
            if (_countsPerBiome == null || _biomeLookup == null || _countsPerTypePerBiome == null)
                return;

            int bi = creature.biomeIndex;
            if (bi >= 0 && bi < _countsPerBiome.Length)
                _countsPerBiome[bi]--;

            if (_biomeLookup.TryGetValue(bi, out FaunaBiomeData biomeData) &&
                _countsPerTypePerBiome.TryGetValue(biomeData, out int[] typeCounts))
            {
                int ti = creature.creatureTypeIndex;
                if (ti >= 0 && ti < typeCounts.Length)
                    typeCounts[ti]--;
            }

            DecrementChunkCount(creature.chunkCoord);
            if (creature.isLargeThreat)
                DecrementMacroZoneCount(creature.macroZoneCoord);
        }

        private void EnsureRuntimeStateInitialized()
        {
            if (_biomeLookup != null &&
                _countsPerTypePerBiome != null &&
                _resolvedEntriesPerBiome != null &&
                _availablePoolCountsPerBiome != null &&
                _prefabTypeIndexLookup != null &&
                _countsPerChunk != null &&
                _largeThreatCountsPerMacroZone != null &&
                _activeCreatures != null &&
                _countsPerBiome != null)
            {
                return;
            }

            int capacity = biomeDatasets != null ? biomeDatasets.Length : 4;
            _biomeLookup ??= new Dictionary<int, FaunaBiomeData>(capacity);
            _countsPerTypePerBiome ??= new Dictionary<FaunaBiomeData, int[]>(capacity);
            _resolvedEntriesPerBiome ??= new Dictionary<FaunaBiomeData, ResolvedFaunaEntry[]>(capacity);
            _availablePoolCountsPerBiome ??= new Dictionary<FaunaBiomeData, int[]>(capacity);
            _prefabTypeIndexLookup ??= new Dictionary<FaunaBiomeData, Dictionary<GameObject, int>>(capacity);
            _countsPerChunk ??= new Dictionary<long, int>(32);
            _largeThreatCountsPerMacroZone ??= new Dictionary<long, int>(16);

            _biomeLookup.Clear();
            _countsPerTypePerBiome.Clear();
            _resolvedEntriesPerBiome.Clear();
            _availablePoolCountsPerBiome.Clear();
            _prefabTypeIndexLookup.Clear();

            int maxBiomeIndex = 0;
            if (biomeDatasets != null)
            {
                for (int i = 0; i < biomeDatasets.Length; i++)
                {
                    FaunaBiomeData data = biomeDatasets[i];
                    if (data == null)
                        continue;

                    _biomeLookup[data.biomeIndex] = data;
                    if (data.biomeIndex > maxBiomeIndex)
                        maxBiomeIndex = data.biomeIndex;

                    int creatureCount = data.possibleCreatures != null ? data.possibleCreatures.Count : 0;
                    _countsPerTypePerBiome[data] = new int[creatureCount];
                    ResolvedFaunaEntry[] resolvedEntries = new ResolvedFaunaEntry[creatureCount];
                    _availablePoolCountsPerBiome[data] = new int[creatureCount];

                    Dictionary<GameObject, int> prefabLookup = new Dictionary<GameObject, int>(Mathf.Max(1, creatureCount));
                    if (data.possibleCreatures != null)
                    {
                        for (int creatureIndex = 0; creatureIndex < creatureCount; creatureIndex++)
                        {
                            FaunaEntry faunaEntry = data.possibleCreatures[creatureIndex];
                            GameObject resolvedPrefab = faunaEntry.GetResolvedPrefab();
                            resolvedEntries[creatureIndex] = new ResolvedFaunaEntry
                            {
                                prefab = resolvedPrefab,
                                archetype = faunaEntry.archetype,
                                spawnWeight = faunaEntry.GetResolvedSpawnWeight(),
                                maxAlive = Mathf.Max(1, faunaEntry.GetResolvedMaxAlive()),
                                creatureTypeIndex = creatureIndex,
                                isLargeThreat = IsLargeThreatEntry(data, faunaEntry),
                                blockedWhenPressureDisabled = ShouldBlockEntryWhenPressureDisabled(faunaEntry.archetype)
                            };

                            if (resolvedPrefab == null || prefabLookup.ContainsKey(resolvedPrefab))
                                continue;

                            prefabLookup.Add(resolvedPrefab, creatureIndex);
                        }
                    }

                    _resolvedEntriesPerBiome[data] = resolvedEntries;
                    _prefabTypeIndexLookup[data] = prefabLookup;
                }
            }

            _activeCreatures ??= new List<ActiveCreature>(Mathf.Max(4, globalMaxCount));
            if (_countsPerBiome == null || _countsPerBiome.Length < maxBiomeIndex + 1)
                _countsPerBiome = new int[maxBiomeIndex + 1];
        }

        /// <summary>
        /// Инкрементирует stateful-счётчики при добавлении существа.
        /// Используется ForceSpawnHorde для корректного accounting.
        /// O(1). Zero GC.
        /// </summary>
        private void IncrementCreatureCounters(int biomeIdx, int typeIndex,
                                                FaunaBiomeData biomeData)
        {
            if (biomeIdx >= 0 && biomeIdx < _countsPerBiome.Length)
                _countsPerBiome[biomeIdx]++;

            if (biomeData != null &&
                _countsPerTypePerBiome.TryGetValue(biomeData, out int[] typeCounts))
            {
                if (typeIndex >= 0 && typeIndex < typeCounts.Length)
                    typeCounts[typeIndex]++;
            }
        }

        /// <summary>
        /// Находит индекс существа в possibleCreatures по префабу.
        /// ReferenceEquals — zero GC. O(n) по типам (обычно 3-5).
        /// </summary>
        private int FindCreatureTypeIndex(FaunaBiomeData biomeData, GameObject prefab)
        {
            if (biomeData == null || prefab == null)
                return -1;

            if (_prefabTypeIndexLookup != null &&
                _prefabTypeIndexLookup.TryGetValue(biomeData, out Dictionary<GameObject, int> prefabLookup) &&
                prefabLookup != null &&
                prefabLookup.TryGetValue(prefab, out int cachedIndex))
            {
                return cachedIndex;
            }

            return -1;
        }

        // ══════════════════════════════════════════════════════════
        //  SWAP REMOVE — O(1) удаление из List
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Swap-Remove: меняет элемент с последним, удаляет последний.
        /// O(1) вместо O(n). Порядок не сохраняется (не важно для нас).
        /// Zero GC: List.RemoveAt(last) не сдвигает массив.
        /// </summary>
        private void SwapRemoveAt(int index)
        {
            int lastIndex = _activeCreatures.Count - 1;

            if (index < lastIndex)
            {
                _activeCreatures[index] = _activeCreatures[lastIndex];
            }

            _activeCreatures.RemoveAt(lastIndex);
        }

        // ══════════════════════════════════════════════════════════
        //  PLAYER LOOKUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ленивый поиск игрока по тегу "Player".
        /// Вызывается один раз при OnEnable или если ссылка потеряна.
        /// </summary>
        private void FindPlayer(bool force = false)
        {
            if (_playerTransform != null)
                return;

            if (!force && Time.time < _nextPlayerResolveTime)
                return;

            _nextPlayerResolveTime = Time.time + PlayerResolveRetryInterval;
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _playerTransform);

            if (_playerTransform != null)
                _nextPlayerResolveTime = float.NegativeInfinity;
        }

        private void ResolveSpawnRegistry()
        {
            if (spawnRegistry == null)
                WorldRuntimeReferenceUtility.TryResolveWorldFaunaSpawnRegistry(ref spawnRegistry);

            if (spawnRegistry != null && proceduralStateRegistry != null)
                spawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        private void ResolveProceduralStateRegistry()
        {
            if (proceduralStateRegistry == null)
                WorldRuntimeReferenceUtility.TryResolveWorldProceduralStateRegistry(ref proceduralStateRegistry);

            if (spawnRegistry != null && proceduralStateRegistry != null)
                spawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        private void ResolveBiomeMatrixDirector()
        {
            if (biomeMatrixDirector == null)
                WorldRuntimeReferenceUtility.TryResolveBiomeMatrixDirector(ref biomeMatrixDirector);
        }

        private void ResolveWorldZoneDirector()
        {
            if (worldZoneDirector == null)
                WorldRuntimeReferenceUtility.TryResolveWorldZoneDirector(ref worldZoneDirector);
        }

        private int ResolveEffectiveGlobalMaxCount(WorldProceduralFaunaMood faunaMood)
        {
            return Mathf.Max(1, Mathf.RoundToInt(_runtimeGlobalMaxCount * ResolveGlobalBudgetScale(faunaMood) * _adaptiveGlobalBudgetScale));
        }

        private int ResolveEffectiveSpawnsPerTick(WorldProceduralFaunaMood faunaMood)
        {
            return Mathf.Max(1, Mathf.RoundToInt(_runtimeMaxSpawnsPerTick * ResolveGlobalBudgetScale(faunaMood) * _adaptiveSpawnBudgetScale));
        }

        private int ResolveEffectiveBiomeMaxCount(FaunaBiomeData biomeData, WorldProceduralFaunaMood faunaMood)
        {
            if (biomeData == null)
                return 0;

            return Mathf.Max(1, Mathf.RoundToInt(biomeData.biomeMaxCreatures * ResolveBiomeCapScale(faunaMood) * _adaptiveBiomeBudgetScale));
        }

        private void RefreshAdaptiveBudgetResponse()
        {
            if (!enableAdaptivePerfBudget)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f);
                return;
            }

            DynamicResolutionScaler scaler = DynamicResolutionScaler.Instance;
            if (scaler == null || !scaler.Enabled)
            {
                ApplyAdaptiveBudgetResponse(1f, 1f);
                return;
            }

            float renderScale = Mathf.Clamp01(scaler.CurrentRenderScale);
            float normalized = Mathf.Clamp01(
                (renderScale - adaptiveBudgetFloorRenderScale) /
                Mathf.Max(0.0001f, 1f - adaptiveBudgetFloorRenderScale));

            ApplyAdaptiveBudgetResponse(renderScale, normalized);
        }

        private void ApplyAdaptiveBudgetResponse(float renderScale, float normalized)
        {
            _adaptiveBudgetNormalized = normalized;
            _adaptiveGlobalBudgetScale = Mathf.Lerp(adaptiveGlobalFaunaBudgetFloor, 1f, normalized);
            _adaptiveBiomeBudgetScale = Mathf.Lerp(adaptiveBiomeCapBudgetFloor, 1f, normalized);
            _adaptiveSpawnBudgetScale = Mathf.Lerp(adaptiveSpawnBurstBudgetFloor, 1f, normalized);

#if UNITY_EDITOR
            _debugAdaptiveRenderScale = renderScale;
            _debugAdaptiveBudgetNormalized = normalized;
            _debugAdaptiveGlobalBudgetScale = _adaptiveGlobalBudgetScale;
            _debugAdaptiveBiomeBudgetScale = _adaptiveBiomeBudgetScale;
            _debugAdaptiveSpawnBudgetScale = _adaptiveSpawnBudgetScale;
#endif
        }

        private void RefreshEcologyCompositionResponse(WorldProceduralFaunaMood faunaMood, WorldZoneAnchor currentZone)
        {
            _currentPassiveSelectionScale = ResolvePassiveSelectionScale(faunaMood, currentZone);
            _currentAggressiveSelectionScale = ResolveAggressiveSelectionScale(faunaMood, currentZone);
            _currentLargeThreatSelectionScale = ResolveLargeThreatSelectionScale(currentZone);
        }

        private float ResolvePassiveSelectionScale(WorldProceduralFaunaMood faunaMood, WorldZoneAnchor currentZone)
        {
            float scale;
            switch (faunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    scale = calmPassiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Lively:
                    scale = livelyPassiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Hostile:
                    scale = hostilePassiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Mixed:
                default:
                    scale = mixedPassiveSelectionScale;
                    break;
            }

            if (IsSafePocketZone(currentZone))
                scale *= safePocketPassiveSelectionScale;
            else if (IsHostileZone(currentZone))
                scale *= hostileZonePassiveSelectionScale;

            return Mathf.Max(0f, scale);
        }

        private float ResolveAggressiveSelectionScale(WorldProceduralFaunaMood faunaMood, WorldZoneAnchor currentZone)
        {
            float scale;
            switch (faunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    scale = calmAggressiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Lively:
                    scale = livelyAggressiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Hostile:
                    scale = hostileAggressiveSelectionScale;
                    break;
                case WorldProceduralFaunaMood.Mixed:
                default:
                    scale = mixedAggressiveSelectionScale;
                    break;
            }

            if (IsSafePocketZone(currentZone))
                scale *= safePocketAggressiveSelectionScale;
            else if (IsHostileZone(currentZone))
                scale *= hostileZoneAggressiveSelectionScale;

            return Mathf.Max(0f, scale);
        }

        private float ResolveLargeThreatSelectionScale(WorldZoneAnchor currentZone)
        {
            float scale = 1f;

            if (IsSafePocketZone(currentZone))
                scale *= safePocketLargeThreatSelectionScale;
            else if (IsHostileZone(currentZone))
                scale *= hostileZoneLargeThreatSelectionScale;

            if (currentZone != null && currentZone.RouteCritical)
                scale *= routeCriticalLargeThreatSelectionScale;

            scale *= _adaptiveSpawnBudgetScale;
            return Mathf.Max(0f, scale);
        }

        private static float ResolveSelectionWeight(
            in ResolvedFaunaEntry entry,
            float passiveSelectionScale,
            float aggressiveSelectionScale,
            float largeThreatSelectionScale)
        {
            float weight = entry.spawnWeight;
            if (weight <= 0f)
                return 0f;

            CreatureArchetypeData archetype = entry.archetype;
            bool aggressive = (archetype != null && archetype.isAggressive) ||
                              (archetype != null && (archetype.roleType == CreatureRoleType.Hunter || archetype.roleType == CreatureRoleType.Leviathan));

            weight *= aggressive ? aggressiveSelectionScale : passiveSelectionScale;
            if (entry.isLargeThreat)
                weight *= largeThreatSelectionScale;

            return Mathf.Max(0f, weight);
        }

        private static bool IsSafePocketZone(WorldZoneAnchor currentZone)
        {
            if (currentZone == null)
                return false;

            switch (currentZone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Fabrication:
                case WorldZoneAnchor.ZoneKind.Service:
                case WorldZoneAnchor.ZoneKind.Construction:
                case WorldZoneAnchor.ZoneKind.Power:
                    return true;
                default:
                    return false;
            }
        }

        private static bool IsHostileZone(WorldZoneAnchor currentZone)
        {
            if (currentZone == null)
                return false;

            switch (currentZone.Kind)
            {
                case WorldZoneAnchor.ZoneKind.Combat:
                case WorldZoneAnchor.ZoneKind.Trial:
                    return true;
                default:
                    return false;
            }
        }

        private float ResolveGlobalBudgetScale(WorldProceduralFaunaMood faunaMood)
        {
            switch (faunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    return calmFaunaBudgetScale;
                case WorldProceduralFaunaMood.Lively:
                    return livelyFaunaBudgetScale;
                case WorldProceduralFaunaMood.Mixed:
                    return mixedFaunaBudgetScale;
                case WorldProceduralFaunaMood.Hostile:
                    return hostileFaunaBudgetScale;
                default:
                    return 1f;
            }
        }

        private float ResolveBiomeCapScale(WorldProceduralFaunaMood faunaMood)
        {
            switch (faunaMood)
            {
                case WorldProceduralFaunaMood.Calm:
                    return calmBiomeCapScale;
                case WorldProceduralFaunaMood.Lively:
                    return livelyBiomeCapScale;
                case WorldProceduralFaunaMood.Mixed:
                    return mixedBiomeCapScale;
                case WorldProceduralFaunaMood.Hostile:
                    return hostileBiomeCapScale;
                default:
                    return 1f;
            }
        }

        private void TryWarmupCreaturePools()
        {
            if (_creaturePoolsWarmed || biomeDatasets == null || biomeDatasets.Length == 0)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            _warmupPrefabs.Clear();

            for (int biomeIndex = 0; biomeIndex < biomeDatasets.Length; biomeIndex++)
            {
                FaunaBiomeData biomeData = biomeDatasets[biomeIndex];
                if (biomeData == null || biomeData.possibleCreatures == null)
                    continue;

                List<FaunaEntry> possibleCreatures = biomeData.possibleCreatures;
                int creatureCount = possibleCreatures.Count;

                for (int creatureIndex = 0; creatureIndex < creatureCount; creatureIndex++)
                {
                    GameObject resolvedPrefab = possibleCreatures[creatureIndex].GetResolvedPrefab();
                    if (resolvedPrefab == null)
                        continue;
                    _warmupPrefabs.Add(resolvedPrefab);
                }
            }

            foreach (GameObject warmupPrefab in _warmupPrefabs)
            {
                int requiredReserve = GetRequiredCreaturePoolWarmupReserve(warmupPrefab);
                int availableCount = pool.GetAvailableCount(warmupPrefab);
                if (availableCount >= requiredReserve)
                    continue;

                pool.Warmup(warmupPrefab, requiredReserve - availableCount);
            }

            _creaturePoolsWarmed = true;
        }

        private int GetRequiredCreaturePoolWarmupReserve(GameObject prefab)
        {
            return IsSmallPassiveProxyPrefab(prefab)
                ? _smallPassiveCreaturePoolWarmupReserve
                : _defaultCreaturePoolWarmupReserve;
        }

        private static bool IsSmallPassiveProxyPrefab(GameObject prefab)
        {
            return prefab != null && prefab.name == SmallPassiveProxyPrefabName;
        }

        private void RefreshCreaturePoolWarmupTargets()
        {
            int maxAllowedReserve = Mathf.Max(CreaturePoolMinimumReserve, _runtimeGlobalMaxCount);
            int defaultReserve = Mathf.Clamp(
                Mathf.Max(CreaturePoolMinimumReserve, _runtimeMaxSpawnsPerTick * CreaturePoolBurstReserveMultiplier),
                CreaturePoolMinimumReserve,
                maxAllowedReserve);
            int smallPassiveReserve = Mathf.Clamp(
                Mathf.Max(defaultReserve, Mathf.Max(SmallPassiveProxyMinimumReserve, _runtimeMaxSpawnsPerTick * SmallPassiveProxyBurstReserveMultiplier)),
                CreaturePoolMinimumReserve,
                maxAllowedReserve);

            if (defaultReserve == _defaultCreaturePoolWarmupReserve &&
                smallPassiveReserve == _smallPassiveCreaturePoolWarmupReserve)
            {
                return;
            }

            _defaultCreaturePoolWarmupReserve = defaultReserve;
            _smallPassiveCreaturePoolWarmupReserve = smallPassiveReserve;
            _creaturePoolsWarmed = false;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество активных существ в мире.</summary>
        public int ActiveCreatureCount => _activeCreatures != null ? _activeCreatures.Count : 0;

        /// <summary>
        /// Принудительный деспавн ВСЕХ существ.
        /// Используется при смене зоны, загрузке сейва, телепорте.
        /// Очищает stateful-счётчики.
        /// </summary>
        public void DespawnAll()
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;

            for (int i = _activeCreatures.Count - 1; i >= 0; i--)
            {
                ActiveCreature creature = _activeCreatures[i];

                if (creature.gameObject != null && pool != null)
                {
                    pool.Despawn(creature.gameObject);
                }
            }

            _activeCreatures.Clear();
            _countsPerChunk.Clear();
            _largeThreatCountsPerMacroZone.Clear();

            // ── Очистка stateful-счётчиков ──
            System.Array.Clear(_countsPerBiome, 0, _countsPerBiome.Length);

            // Очистка per-type counts без foreach (избегаем GC от Dictionary enumerator)
            if (biomeDatasets != null)
            {
                for (int i = 0; i < biomeDatasets.Length; i++)
                {
                    FaunaBiomeData data = biomeDatasets[i];
                    if (data != null &&
                        _countsPerTypePerBiome.TryGetValue(data, out int[] typeCounts))
                    {
                        System.Array.Clear(typeCounts, 0, typeCounts.Length);
                    }
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — DIRECTOR ORCHESTRATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Управление хищным давлением. Вызывается HectonDirectorAI
        /// при смене фаз (BuildUp/Peak → true, Relax → false).
        ///
        /// При enabled == false:
        ///   • Все активные существа с HectonBaseAI переводятся
        ///     в состояние Wander (отступление от игрока).
        ///   • ForceSpawnHorde блокируется до повторного включения.
        ///
        /// При enabled == true:
        ///   • Восстанавливается штатный AI behaviour.
        ///   • ForceSpawnHorde разрешается.
        ///
        /// ZERO GC: for-цикл по pre-allocated List, TryGetComponent
        /// не аллоцирует (generic constrained). Никаких LINQ/foreach.
        /// </summary>
        /// <param name="enabled">true = давление разрешено, false = отступление.</param>
        public void SetPredatorPressure(bool enabled)
        {
            _pressureEnabled = enabled;

            // ── При отключении давления — заставляем всех отступить ──
            if (!enabled)
            {
                int count = _activeCreatures.Count;
                for (int i = 0; i < count; i++)
                {
                    ActiveCreature creature = _activeCreatures[i];

                    // Пропускаем уничтоженные/деактивированные объекты
                    if (creature.gameObject == null)
                        continue;
                    if (!creature.gameObject.activeInHierarchy)
                        continue;

                    if (creature.gameObject.TryGetComponent(out HectonBaseAI ai))
                    {
                        ai.ForceState(HectonBaseAI.AIState.Wander);
                    }
                }
            }

#if UNITY_EDITOR
            _debugPressureEnabled = _pressureEnabled;
#endif
        }

        /// <summary>
        /// Принудительный спавн орды существ по команде Директора.
        /// Игнорирует внутренние кулдауны FaunaDirector — это приказ.
        ///
        /// Алгоритм:
        ///   1. Если _pressureEnabled == false — выход (Relax блокирует орды).
        ///   2. Выбрать FaunaBiomeData: используем _cachedBiomeIndex
        ///      (текущий биом игрока), fallback на первый доступный dataset.
        ///   3. В цикле (hordeCountMin..hordeCountMax итераций):
        ///      • Генерация позиции в радиусе hordeRadiusInner..hordeRadiusOuter
        ///        от worldCenter.
        ///      • Спавн через ObjectPoolManager.Spawn.
        ///      • Регистрация в _activeCreatures + инкремент счётчиков.
        ///      • Немедленный ForceState(Aggressive) для атаки.
        ///   4. Global limit уважается — если слоты кончились, спавн прерывается.
        ///
        /// ZERO GC: struct math, pre-allocated List, TryGetComponent.
        /// </summary>
        /// <param name="worldCenter">Центр спавна орды (мировые координаты).</param>
        public void ForceSpawnHorde(Vector3 worldCenter)
        {
            // ── Relax-фаза блокирует орды ──
            if (!_pressureEnabled)
                return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null)
                return;

            // ══════════════════════════════════════════════════════
            //  Выбор FaunaBiomeData
            // ══════════════════════════════════════════════════════
            //  Приоритет: текущий биом игрока (_cachedBiomeIndex).
            //  Fallback: первый доступный dataset из biomeDatasets.

            FaunaBiomeData biomeData = null;

            // Попробовать текущий биом игрока
            if (_cachedBiomeIndex >= 0)
            {
                _biomeLookup.TryGetValue(_cachedBiomeIndex, out biomeData);
            }

            // Fallback: первый доступный dataset
            if (biomeData == null && biomeDatasets != null)
            {
                for (int i = 0; i < biomeDatasets.Length; i++)
                {
                    if (biomeDatasets[i] != null)
                    {
                        biomeData = biomeDatasets[i];
                        break;
                    }
                }
            }

            if (biomeData == null)
                return;

            // Проверяем наличие существ в биоме
            if (_resolvedEntriesPerBiome == null ||
                !_resolvedEntriesPerBiome.TryGetValue(biomeData, out ResolvedFaunaEntry[] resolvedEntries) ||
                resolvedEntries == null ||
                resolvedEntries.Length == 0)
            {
                return;
            }

            if (!_countsPerTypePerBiome.TryGetValue(biomeData, out int[] creatureTypeCounts))
                return;

            if (_availablePoolCountsPerBiome == null ||
                !_availablePoolCountsPerBiome.TryGetValue(biomeData, out int[] availablePoolCounts) ||
                availablePoolCounts == null ||
                availablePoolCounts.Length < resolvedEntries.Length)
            {
                return;
            }

            for (int i = 0; i < resolvedEntries.Length; i++)
            {
                GameObject prefab = resolvedEntries[i].prefab;
                availablePoolCounts[i] = prefab != null ? pool.GetAvailableCount(prefab) : 0;
            }

            int biomeIdx = biomeData.biomeIndex;

            // ══════════════════════════════════════════════════════
            //  Спавн орды
            // ══════════════════════════════════════════════════════

            int hordeSize = Random.Range(hordeCountMin, hordeCountMax + 1);
            int spawned = 0;

            for (int h = 0; h < hordeSize; h++)
            {
                // ── Global limit check ──
                if (_activeCreatures.Count >= _runtimeGlobalMaxCount)
                    break;

                // ── Выбор случайного типа существа из биома ──
                // Для орды используем равномерный выбор из possibleCreatures
                // (weighted random через TrySelectCreature не обязателен —
                //  Директор хочет любую угрозу, а не balanced population).
                if (!TrySelectResolvedHordeEntry(resolvedEntries, creatureTypeCounts, availablePoolCounts, out ResolvedFaunaEntry selectedEntry))
                    break;

                GameObject resolvedPrefab = selectedEntry.prefab;
                if (resolvedPrefab == null)
                    continue;

                // ── Позиция в кольце вокруг worldCenter ──
                float angle    = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(hordeRadiusInner, hordeRadiusOuter);

                float spawnX = worldCenter.x + Mathf.Cos(angle) * distance;
                float spawnZ = worldCenter.z + Mathf.Sin(angle) * distance;
                float spawnY = worldCenter.y; // Используем высоту центра события

                Vector3    spawnPos = new Vector3(spawnX, spawnY, spawnZ);
                Quaternion spawnRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
                WorldChunkCoordinate spawnChunk = WorldChunkCoordinate.FromWorldPosition(spawnPos, _runtimeChunkSize);

                if (GetChunkCreatureCount(spawnChunk) >= _runtimePerChunkMaxCount)
                    continue;

                // ── Спавн через пул ──
                GameObject instance = pool.Spawn(resolvedPrefab, spawnPos, spawnRot, false);
                if (instance == null)
                    continue;

                // ── Определяем typeIndex ──
                int typeIndex = selectedEntry.creatureTypeIndex;

                // ── Регистрация в трекере ──
                ActiveCreature record = new ActiveCreature
                {
                    gameObject       = instance,
                    transform        = instance.transform,
                    creatureTypeIndex = typeIndex,
                    biomeIndex       = biomeIdx,
                    prefabSource     = resolvedPrefab,
                    chunkCoord       = spawnChunk,
                    macroZoneCoord   = default,
                    isLargeThreat    = false
                };

                _activeCreatures.Add(record);

                // ── Инкремент stateful-счётчиков ──
                IncrementCreatureCounters(biomeIdx, typeIndex, biomeData);
                IncrementChunkCount(spawnChunk);

                // ── Настройка AI: спавн-поинт + принудительное Aggressive ──
                if (instance.TryGetComponent(out HectonBaseAI ai))
                {
                    ai.ApplyArchetype(selectedEntry.archetype);
                    ai.SetSpawnPoint(spawnPos);
                    ai.ForceState(HectonBaseAI.AIState.Aggressive);
                }

                if (typeIndex >= 0 && typeIndex < availablePoolCounts.Length && availablePoolCounts[typeIndex] > 0)
                    availablePoolCounts[typeIndex]--;

                spawned++;
            }

#if UNITY_EDITOR
            _debugLastHordeSpawned = spawned;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(
            int cullCount,
            int spawnAttempts,
            int spawnValidationAttempts,
            int spawnValidationSuccesses,
            int anchorBasedSpawns,
            int fallbackRingSpawns)
        {
            _debugActiveCount    = _activeCreatures.Count;
            _debugCurrentBiome   = _cachedBiomeIndex;
            _debugCullCount      = cullCount;
            _debugSpawnAttempts  = spawnAttempts;
            _debugPressureEnabled = _pressureEnabled;
            _debugActiveChunks = _countsPerChunk != null ? _countsPerChunk.Count : 0;
            _debugActiveMacroZones = _largeThreatCountsPerMacroZone != null ? _largeThreatCountsPerMacroZone.Count : 0;
            _debugRegistryFaunaAnchors = spawnRegistry != null ? spawnRegistry.OrdinaryAnchorCount : 0;
            _debugRegistryLargeThreatZones = spawnRegistry != null ? spawnRegistry.LargeThreatZoneCount : 0;
            _debugRuntimeChunkSize = _runtimeChunkSize;
            _debugRuntimeMacroZoneSize = _runtimeMacroZoneSize;
            _debugRuntimeGlobalMaxCount = _runtimeGlobalMaxCount;
            _debugRuntimePerChunkMaxCount = _runtimePerChunkMaxCount;
            _debugRuntimeSpawnOuter = _runtimeSpawnRingOuter;
            _debugRuntimeLargeThreatSpawnOuter = _runtimeLargeThreatSpawnOuter;
            _debugRuntimeCullDistance = _runtimeKillDistance;
            _debugRuntimeLargeThreatCullDistance = _runtimeLargeThreatKillDistance;
            _debugSpawnValidationAttempts = spawnValidationAttempts;
            _debugSpawnValidationSuccesses = spawnValidationSuccesses;
            _debugAnchorBasedSpawns = anchorBasedSpawns;
            _debugFallbackRingSpawns = fallbackRingSpawns;
            _debugMatrixFaunaMood = _currentMatrixFaunaMood.ToString();
            _debugEffectiveGlobalMaxCount = _currentEffectiveGlobalMaxCount;
            _debugEffectiveSpawnsPerTick = _currentEffectiveSpawnsPerTick;
            _debugEffectiveBiomeMaxCount = _cachedBiomeIndex >= 0 ? _currentEffectiveBiomeMaxCount : 0;
            _debugAdaptiveRenderScale = DynamicResolutionScaler.Instance != null ? DynamicResolutionScaler.Instance.CurrentRenderScale : 1f;
            _debugAdaptiveBudgetNormalized = _adaptiveBudgetNormalized;
            _debugAdaptiveGlobalBudgetScale = _adaptiveGlobalBudgetScale;
            _debugAdaptiveBiomeBudgetScale = _adaptiveBiomeBudgetScale;
            _debugAdaptiveSpawnBudgetScale = _adaptiveSpawnBudgetScale;
            _debugCurrentZone = _currentZone != null ? _currentZone.ZoneLabel : "None";
            _debugCurrentZoneRouteCritical = _currentZone != null && _currentZone.RouteCritical;
            _debugCurrentZoneSafePocket = _currentZoneIsSafePocket;
            _debugPassiveSelectionScale = _currentPassiveSelectionScale;
            _debugAggressiveSelectionScale = _currentAggressiveSelectionScale;
            _debugLargeThreatSelectionScale = _currentLargeThreatSelectionScale;

            if (_playerTransform != null)
            {
                WorldChunkCoordinate playerChunk = WorldChunkCoordinate.FromWorldPosition(_playerTransform.position, _runtimeChunkSize);
                _debugCurrentChunk = playerChunk.ToString();
                WorldMacroZoneCoordinate playerMacroZone = WorldMacroZoneCoordinate.FromWorldPosition(_playerTransform.position, _runtimeMacroZoneSize);
                _debugCurrentMacroZone = playerMacroZone.ToString();
            }
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR — GIZMOS
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 center = Application.isPlaying && _playerTransform != null
                ? _playerTransform.position
                : transform.position;
            float innerRadius = Application.isPlaying ? _runtimeSpawnRingInner : spawnRingInner;
            float outerRadius = Application.isPlaying ? _runtimeSpawnRingOuter : spawnRingOuter;
            float cullRadius = Application.isPlaying ? _runtimeKillDistance : killDistance;
            float largeThreatOuter = Application.isPlaying ? _runtimeLargeThreatSpawnOuter : spawnRingOuter;
            float largeThreatCull = Application.isPlaying ? _runtimeLargeThreatKillDistance : killDistance;

            // Spawn ring — inner
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.1f);
            DrawWireCircle(center, innerRadius, 32);

            // Spawn ring — outer
            Gizmos.color = new Color(0f, 1f, 0.5f, 0.2f);
            DrawWireCircle(center, outerRadius, 48);

            // Kill distance
            Gizmos.color = new Color(1f, 0.2f, 0f, 0.08f);
            DrawWireCircle(center, cullRadius, 64);

            // Large threats
            Gizmos.color = new Color(1f, 0.7f, 0.1f, 0.14f);
            DrawWireCircle(center, largeThreatOuter, 72);

            Gizmos.color = new Color(1f, 0.4f, 0f, 0.08f);
            DrawWireCircle(center, largeThreatCull, 80);

            // Active creatures (в Play Mode)
            if (Application.isPlaying && _activeCreatures != null)
            {
                Gizmos.color = Color.cyan;
                int count = _activeCreatures.Count;
                for (int i = 0; i < count; i++)
                {
                    ActiveCreature c = _activeCreatures[i];
                    if (c.transform != null)
                    {
                        Gizmos.DrawWireSphere(c.transform.position, 0.5f);
                    }
                }
            }
        }

        /// <summary>
        /// Рисует горизонтальный wireframe-круг (XZ плоскость).
        /// </summary>
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
            if (globalMaxCount    < 1)   globalMaxCount    = 1;
            if (maxSpawnsPerTick  < 1)   maxSpawnsPerTick  = 1;
            if (spawnRingInner    < 10f) spawnRingInner    = 10f;
            if (spawnRingOuter    < spawnRingInner + 10f)
                spawnRingOuter = spawnRingInner + 10f;
            if (killDistance      < spawnRingOuter)
                killDistance = spawnRingOuter + 50f;

            RefreshRuntimeStreamingSettings();

            if (hordeCountMin < 1) hordeCountMin = 1;
            if (hordeCountMax < hordeCountMin) hordeCountMax = hordeCountMin;
            if (hordeRadiusInner < 1f) hordeRadiusInner = 1f;
            if (hordeRadiusOuter < hordeRadiusInner + 1f)
                hordeRadiusOuter = hordeRadiusInner + 1f;
        }
#endif

        public void SetChunkStreamingProfile(WorldChunkStreamingProfile profile)
        {
            chunkStreamingProfile = profile;
            _runtimeSettingsDirty = true;
            RefreshRuntimeStreamingSettings();
            _runtimeSettingsDirty = false;
            _nextRuntimeSettingsRefreshTime = Time.time + RuntimeSettingsRefreshInterval;
        }

        public void SetSpawnRegistry(WorldFaunaSpawnRegistry registry)
        {
            spawnRegistry = registry;
            if (spawnRegistry != null && proceduralStateRegistry != null)
                spawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        public void SetProceduralStateRegistry(WorldProceduralStateRegistry registry)
        {
            proceduralStateRegistry = registry;
            if (spawnRegistry != null && proceduralStateRegistry != null)
                spawnRegistry.SetProceduralStateRegistry(proceduralStateRegistry);
        }

        private void RefreshRuntimeStreamingSettings()
        {
            _runtimeSpawnRingInner = spawnRingInner;
            _runtimeSpawnRingOuter = spawnRingOuter;
            _runtimeKillDistance = killDistance;
            _runtimeChunkSize = 192f;
            _runtimeMacroZoneSize = 768f;
            _runtimeLargeThreatSpawnInner = Mathf.Max(_runtimeSpawnRingOuter + 60f, _runtimeSpawnRingInner + 120f);
            _runtimeLargeThreatSpawnOuter = Mathf.Max(_runtimeLargeThreatSpawnInner + 120f, _runtimeKillDistance);
            _runtimeLargeThreatKillDistance = Mathf.Max(_runtimeLargeThreatSpawnOuter + 120f, _runtimeKillDistance * 1.5f);
            _runtimeGlobalMaxCount = Mathf.Max(1, globalMaxCount);
            _runtimeMaxSpawnsPerTick = Mathf.Max(1, maxSpawnsPerTick);
            _runtimePerChunkMaxCount = Mathf.Max(4, Mathf.CeilToInt(_runtimeGlobalMaxCount / 5f));
            _runtimeMaxNearbyLargeThreats = 1;
            _runtimeFaunaAnchorChunkDistance = Mathf.Max(1, Mathf.CeilToInt(_runtimeSpawnRingOuter / Mathf.Max(1f, _runtimeChunkSize)));
            _runtimeLargeThreatMacroZoneDistance = 1;

            if (chunkStreamingProfile != null)
            {
                WorldChunkStreamingProfile.LayerProfile faunaLayer =
                    chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.Fauna);
                WorldChunkStreamingProfile.LayerProfile largeThreatLayer =
                    chunkStreamingProfile.GetLayerProfileOrDefault(WorldStreamingLayer.LargeThreats);

                _runtimeChunkSize = Mathf.Max(32f, chunkStreamingProfile.chunkSizeMeters);
                _runtimeMacroZoneSize = Mathf.Max(_runtimeChunkSize, chunkStreamingProfile.macroZoneSizeMeters);

                float fullRadius = Mathf.Max(60f, chunkStreamingProfile.fullSimulationRadius * Mathf.Max(0.5f, faunaLayer.nearRadiusScale));
                float midRadius = Mathf.Max(fullRadius + 30f, chunkStreamingProfile.midSimulationRadius * Mathf.Max(0.5f, faunaLayer.midRadiusScale));
                float largeThreatNear = Mathf.Max(fullRadius + 60f, chunkStreamingProfile.fullSimulationRadius * Mathf.Max(0.75f, largeThreatLayer.nearRadiusScale) + 40f);
                float largeThreatMid = Mathf.Max(largeThreatNear + 120f, chunkStreamingProfile.midSimulationRadius * Mathf.Max(0.85f, largeThreatLayer.midRadiusScale));
                float largeThreatFar = Mathf.Max(largeThreatMid + 120f, chunkStreamingProfile.visualResidencyRadius * Mathf.Max(0.9f, largeThreatLayer.farRadiusScale));

                _runtimeSpawnRingInner = Mathf.Clamp(fullRadius * 0.35f, 24f, fullRadius - 10f);
                _runtimeSpawnRingOuter = fullRadius;
                _runtimeKillDistance = midRadius;
                _runtimeLargeThreatSpawnInner = largeThreatNear;
                _runtimeLargeThreatSpawnOuter = largeThreatMid;
                _runtimeLargeThreatKillDistance = largeThreatFar;

                int estimatedLoadedChunks = EstimateChunkCoverage(midRadius, _runtimeChunkSize);
                _runtimeGlobalMaxCount = Mathf.Max(globalMaxCount, estimatedLoadedChunks * 6);
                _runtimeMaxSpawnsPerTick = Mathf.Max(maxSpawnsPerTick, Mathf.Clamp(faunaLayer.maxActivationsPerTick / 2, 4, 16));
                _runtimePerChunkMaxCount = Mathf.Clamp(Mathf.CeilToInt(_runtimeGlobalMaxCount / (float)Mathf.Max(1, estimatedLoadedChunks)), 4, 12);
                _runtimeMaxNearbyLargeThreats = Mathf.Clamp(Mathf.Max(1, largeThreatLayer.maxActivationsPerTick / 2), 1, 2);
                _runtimeFaunaAnchorChunkDistance = Mathf.Max(1, Mathf.CeilToInt(midRadius / Mathf.Max(1f, _runtimeChunkSize)));
                _runtimeLargeThreatMacroZoneDistance = Mathf.Clamp(Mathf.CeilToInt(largeThreatMid / Mathf.Max(1f, _runtimeMacroZoneSize)), 1, 2);
            }

            if (_runtimeSpawnRingOuter < _runtimeSpawnRingInner + 10f)
                _runtimeSpawnRingOuter = _runtimeSpawnRingInner + 10f;

            if (_runtimeKillDistance < _runtimeSpawnRingOuter + 10f)
                _runtimeKillDistance = _runtimeSpawnRingOuter + 10f;
            if (_runtimeLargeThreatSpawnOuter < _runtimeLargeThreatSpawnInner + 60f)
                _runtimeLargeThreatSpawnOuter = _runtimeLargeThreatSpawnInner + 60f;
            if (_runtimeLargeThreatKillDistance < _runtimeLargeThreatSpawnOuter + 120f)
                _runtimeLargeThreatKillDistance = _runtimeLargeThreatSpawnOuter + 120f;

            _runtimeKillDistanceSqr = _runtimeKillDistance * _runtimeKillDistance;
            _runtimeLargeThreatKillDistanceSqr = _runtimeLargeThreatKillDistance * _runtimeLargeThreatKillDistance;
            _killDistanceSqr = _runtimeKillDistanceSqr;
            RefreshCreaturePoolWarmupTargets();
        }

        private static int EstimateChunkCoverage(float radius, float chunkSize)
        {
            float safeRadius = Mathf.Max(1f, radius);
            float safeChunkSize = Mathf.Max(1f, chunkSize);
            float coverage = (Mathf.PI * safeRadius * safeRadius) / (safeChunkSize * safeChunkSize);
            return Mathf.Max(1, Mathf.CeilToInt(coverage));
        }

        private bool IsLargeThreatEntry(FaunaBiomeData biomeData, in FaunaEntry entry)
        {
            CreatureArchetypeData archetype = entry.archetype;
            if (archetype == null)
                return false;

            if (biomeData != null && biomeData.CountsAsLargeThreat(archetype))
                return true;

            return archetype.roleType == CreatureRoleType.Leviathan;
        }

        private static bool ShouldBlockEntryWhenPressureDisabled(CreatureArchetypeData archetype)
        {
            if (archetype == null)
                return false;

            if (archetype.isAggressive)
                return true;

            return archetype.roleType == CreatureRoleType.Hunter ||
                   archetype.roleType == CreatureRoleType.Leviathan;
        }

        private bool CanSpawnLargeThreatNearPlayer(WorldMacroZoneCoordinate spawnMacroZone, Vector3 playerPos)
        {
            if (_currentLargeThreatSelectionScale <= 0.01f || _currentZoneIsSafePocket)
                return false;

            if (GetMacroZoneLargeThreatCount(spawnMacroZone) > 0)
                return false;

            WorldMacroZoneCoordinate playerMacroZone = WorldMacroZoneCoordinate.FromWorldPosition(playerPos, _runtimeMacroZoneSize);
            if (spawnMacroZone.ChebyshevDistanceTo(playerMacroZone) > 1)
                return false;

            return CountNearbyLargeThreats(playerMacroZone) < _runtimeMaxNearbyLargeThreats;
        }

        private int CountNearbyLargeThreats(WorldMacroZoneCoordinate playerMacroZone)
        {
            if (_largeThreatCountsPerMacroZone == null || _largeThreatCountsPerMacroZone.Count == 0)
                return 0;

            int total = 0;
            // ZERO GC: Dictionary<long,int>.Enumerator is a struct — GetEnumerator() returns by value, no heap alloc.
            // foreach on Dictionary<K,V> is FORBIDDEN (boxes to IEnumerator). Explicit struct enumerator is ALLOWED.
            Dictionary<long, int>.Enumerator enumerator = _largeThreatCountsPerMacroZone.GetEnumerator();
            while (enumerator.MoveNext())
            {
                WorldMacroZoneCoordinate zone = DecomposeMacroZoneKey(enumerator.Current.Key);
                if (zone.ChebyshevDistanceTo(playerMacroZone) <= 1)
                    total += enumerator.Current.Value;
            }
            enumerator.Dispose();

            return total;
        }

        private int GetChunkCreatureCount(WorldChunkCoordinate chunkCoord)
        {
            if (_countsPerChunk == null)
                return 0;

            long key = ComposeChunkKey(chunkCoord);
            return _countsPerChunk.TryGetValue(key, out int count) ? count : 0;
        }

        private int GetMacroZoneLargeThreatCount(WorldMacroZoneCoordinate macroZoneCoord)
        {
            if (_largeThreatCountsPerMacroZone == null)
                return 0;

            long key = ComposeMacroZoneKey(macroZoneCoord);
            return _largeThreatCountsPerMacroZone.TryGetValue(key, out int count) ? count : 0;
        }

        private void IncrementChunkCount(WorldChunkCoordinate chunkCoord)
        {
            if (_countsPerChunk == null)
                return;

            long key = ComposeChunkKey(chunkCoord);
            if (_countsPerChunk.TryGetValue(key, out int count))
                _countsPerChunk[key] = count + 1;
            else
                _countsPerChunk.Add(key, 1);
        }

        private void DecrementChunkCount(WorldChunkCoordinate chunkCoord)
        {
            if (_countsPerChunk == null)
                return;

            long key = ComposeChunkKey(chunkCoord);
            if (!_countsPerChunk.TryGetValue(key, out int count))
                return;

            count--;
            if (count <= 0)
                _countsPerChunk.Remove(key);
            else
                _countsPerChunk[key] = count;
        }

        private void IncrementMacroZoneCount(WorldMacroZoneCoordinate macroZoneCoord)
        {
            if (_largeThreatCountsPerMacroZone == null)
                return;

            long key = ComposeMacroZoneKey(macroZoneCoord);
            if (_largeThreatCountsPerMacroZone.TryGetValue(key, out int count))
                _largeThreatCountsPerMacroZone[key] = count + 1;
            else
                _largeThreatCountsPerMacroZone.Add(key, 1);
        }

        private void DecrementMacroZoneCount(WorldMacroZoneCoordinate macroZoneCoord)
        {
            if (_largeThreatCountsPerMacroZone == null)
                return;

            long key = ComposeMacroZoneKey(macroZoneCoord);
            if (!_largeThreatCountsPerMacroZone.TryGetValue(key, out int count))
                return;

            count--;
            if (count <= 0)
                _largeThreatCountsPerMacroZone.Remove(key);
            else
                _largeThreatCountsPerMacroZone[key] = count;
        }

        private static long ComposeChunkKey(WorldChunkCoordinate chunkCoord)
        {
            return ((long)chunkCoord.x << 32) ^ (uint)chunkCoord.z;
        }

        private static long ComposeMacroZoneKey(WorldMacroZoneCoordinate macroZoneCoord)
        {
            return ((long)macroZoneCoord.x << 32) ^ (uint)macroZoneCoord.z;
        }

        private static WorldMacroZoneCoordinate DecomposeMacroZoneKey(long key)
        {
            int x = (int)(key >> 32);
            int z = (int)key;
            return new WorldMacroZoneCoordinate(x, z);
        }
    }
}
