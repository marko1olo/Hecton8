// ============================================================================
// HECTON-8 — SceneBootstrap.cs
// Асинхронный координатор инициализации сцены.
//
// Единственная точка входа для запуска всех игровых систем.
// Гарантирует правильный порядок инициализации через async pipeline.
//
// ПОРЯДОК ИНИЦИАЛИЗАЦИИ:
//   1. Проверка синглтонов (TickManager первым, Pool, Save)
//   2. Async warmup пулов (zero-hitch, yield каждые N инстансов)
//   3. Запуск генерации мира (MapMagic, если есть)
//   4. Async загрузка сохранения (или New Game с fallback)
//   5. World-Ready Check (ожидание ScavengePopulator)
//   6. Ground-Ready Check (ожидание коллайдера под игроком — только при Load)
//   7. Спавн игрока (fallback позиционирование если не из сейва)
//   8. Активация игрока + PlayerController (isKinematic → false)
//   9. OnGameReady → HUD, музыка, геймплей
//
// ОТКАЗОУСТОЙЧИВОСТЬ:
//   • Глобальный таймаут (30с по умолчанию) → OnBootstrapFailed.
//   • destroyCancellationToken — тихий выход при уничтожении GO.
//   • При ошибке LoadGameAsync — fallback на InitNewGame().
//   • При отсутствии MapMagic / ScavengePopulator — пропуск шага.
//   • При ошибке спавна — fallback позиция.
//   • Ground-Ready таймаут (15с) → Warning + активация anyway.
//   • Подробный лог каждого шага для отладки.
//
// ZERO-GC ASYNC:
//   • Unity 6 Awaitable API вместо Task/Task.Run.
//   • Awaitable.NextFrameAsync вместо Task.Yield.
//   • Awaitable.WaitForSecondsAsync вместо Task.Delay.
//   • Никаких лямбда-замыканий в горячих путях.
//
// v2.0 CHANGES:
//   [ADD] Step 6: Ground-Ready Check — Raycast вниз от позиции игрока.
//         Ждёт появления коллайдера (террейн, воксель, статика) перед
//         активацией. Решает race condition с асинхронной генерацией пещер.
//   [ADD] isKinematic safety — Rigidbody переводится в kinematic на время
//         загрузки, предотвращая провал даже при преждевременной активации.
//   [ADD] _isLoadingSave flag — Ground check выполняется только при загрузке
//         сохранения, не при New Game (fallback на поверхности).
//
// НИКАКОГО Update(). Вся логика — async void Start().
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Core;
using Hecton8.SaveSystem;
using Hecton8.World;
using UnityEngine;

namespace Hecton8.Bootstrap
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-20000)] // Раньше ВСЕХ игровых систем
    public sealed class SceneBootstrap : MonoBehaviour
    {
        // ══════════════════════════════════════════════════════════
        //  STATIC EVENTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Выстреливает ОДИН РАЗ, когда все системы инициализированы,
        /// игрок заспавнен, мир готов, PlayerController включён.
        ///
        /// Подписчики:
        ///   • HUD — включить отрисовку
        ///   • Music Manager — начать фоновый трек
        ///   • Tutorial — показать первую подсказку
        ///   • Input System — разблокировать управление
        /// </summary>
        public static event Action OnGameReady;

        /// <summary>
        /// Indicates that bootstrap fully completed and heavyweight runtime world
        /// systems may start reacting to the final player state.
        /// </summary>
        public static bool IsGameReady { get; private set; }

        /// <summary>
        /// Indicates that a live bootstrap instance currently owns scene startup.
        /// Runtime systems may use this to avoid premature player searches while
        /// bootstrap intentionally keeps the player inactive.
        /// </summary>
        public static bool HasActiveInstance { get; private set; }

        /// <summary>
        /// Last known player GameObject managed by bootstrap.
        /// </summary>
        public static GameObject CurrentPlayerObject { get; private set; }

        /// <summary>
        /// Fast-access player transform for runtime systems that should avoid
        /// repeated scene-wide lookups.
        /// </summary>
        public static Transform CurrentPlayerTransform =>
            CurrentPlayerObject != null ? CurrentPlayerObject.transform : null;

        /// <summary>
        /// Выстреливает при критической ошибке инициализации
        /// или при превышении таймаута.
        /// Param: описание ошибки.
        /// </summary>
        public static event Action<string> OnBootstrapFailed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            OnGameReady = null;
            OnBootstrapFailed = null;
            IsGameReady = false;
            HasActiveInstance = false;
            CurrentPlayerObject = null;
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SAVE
        // ══════════════════════════════════════════════════════════

        [Header("Start Overrides")]
        [Tooltip("If true, always start a new game and ignore the handoff context.")]
        [SerializeField] private bool forceNewGame;

        [Header("Runtime World Prime")]
        [Tooltip("If enabled, SceneBootstrap runs the first scatter rebuild passes before the player gets control.")]
        [SerializeField] private bool prewarmProceduralScatterBeforePlayerActivation = true;
        [Tooltip("How many bootstrap passes are allowed for scatter prewarm before ActivatePlayer.")]
        [SerializeField, Range(1, 4)] private int scatterBootstrapPrimePasses = 2;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — POOL WARMUP
        // ══════════════════════════════════════════════════════════

        [Header("── Pool Warmup ───────────────────────────────")]
        [Tooltip("Список префабов для предсоздания в пуле при старте сцены")]
        [SerializeField] private List<WarmupEntry> warmupEntries = new List<WarmupEntry>();

        /// <summary>
        /// Одна запись прогрева пула: префаб + количество.
        /// </summary>
        [Serializable]
        public struct WarmupEntry
        {
            [Tooltip("Префаб для пулирования")]
            public GameObject prefab;

            [Tooltip("Количество предсозданных экземпляров")]
            [Min(1)]
            public int count;

            [Tooltip("Метка для лога (опционально)")]
            public string label;
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PLAYER
        // ══════════════════════════════════════════════════════════

        [Header("── Player ────────────────────────────────────")]
        [Tooltip("Ссылка на спавнер игрока (опционально, если в сцене)")]
        [SerializeField] private MonoBehaviour playerSpawner;

        [Tooltip("Fallback позиция спавна, если спавнер не найден")]
        [SerializeField] private Vector3 fallbackSpawnPosition = new Vector3(0f, 10f, 0f);

        [Tooltip("GameObject игрока (деактивируется на время загрузки, " +
                 "включается только после полной готовности)")]
        [SerializeField] private GameObject playerObject;

        [Tooltip("Контроллер игрока (будет включён в самом конце загрузки)")]
        [SerializeField] private MonoBehaviour playerController;

        [Tooltip("Rigidbody игрока. Переводится в isKinematic на время загрузки. " +
                 "Если не назначен — ищется автоматически на playerObject.")]
        [SerializeField] private Rigidbody playerRigidbody;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — TIMING
        // ══════════════════════════════════════════════════════════

        [Header("── Timing ────────────────────────────────────")]
        [Tooltip("Время ожидания генерации мира (секунды). " +
                 "0 = не ждать.")]
        [SerializeField] private float worldGenWaitTime = 2f;

        [Tooltip("Максимальное время на весь bootstrap (секунды). " +
                 "При превышении — OnBootstrapFailed.")]
        [SerializeField] private float bootstrapTimeout = 30f;

        [Tooltip("Максимальное время ожидания коллайдера под ногами игрока " +
                 "при загрузке сохранения (секунды). При превышении — Warning " +
                 "и активация anyway.")]
        [SerializeField] private float groundReadyTimeout = 15f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DEBUG
        // ══════════════════════════════════════════════════════════

        [Header("── Debug ─────────────────────────────────────")]
        [SerializeField] private bool verboseLogging = true;
        [SerializeField] private string _debugCurrentStep = "Not started";
#pragma warning disable CS0414 // Inspector-only diagnostic
        [SerializeField] private bool _debugCompleted;
#pragma warning restore CS0414

        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Количество инстансов в одном batch перед yield.
        /// Баланс между скоростью прогрева и плавностью
        /// загрузочного экрана. Диапазон 5-10 оптимален.
        /// </summary>
        private const int WarmupBatchSize = 8;

        /// <summary>Интервал проверки ScavengePopulator (секунды).</summary>
        private const float WorldReadyPollIntervalSec = 0.1f;

        /// <summary>
        /// Порог PendingSpawnCount ниже которого мир считается готовым.
        /// </summary>
        private const int WorldReadyThreshold = 100;

        /// <summary>Интервал проверки коллайдера под игроком (секунды).</summary>
        private const float GroundCheckPollIntervalSec = 0.2f;

        /// <summary>
        /// Высота над позицией игрока, откуда бросается raycast вниз.
        /// 2м — достаточный запас чтобы луч не начинался внутри коллайдера.
        /// </summary>
        private const float GroundCheckRayOffset = 2f;

        /// <summary>
        /// Максимальная длина raycast вниз. 1000м покрывает любую глубину пещеры.
        /// </summary>
        private const float GroundCheckRayLength = 1000f;

        /// <summary>
        /// Интервал диагностического лога при ожидании коллайдера (секунды).
        /// </summary>
        private const float GroundCheckLogIntervalSec = 5f;

        // ══════════════════════════════════════════════════════════
        //  PRIVATE STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// true если в Step 4 был успешно загружен сейв.
        /// Используется для определения нужен ли Ground-Ready Check.
        /// При New Game — false, ground check пропускается.
        /// </summary>
        private bool _isLoadingSave;
        private WorldProceduralScatterDirector _worldProceduralScatterDirector;

        private void Awake()
        {
            HasActiveInstance = true;
            PublishPlayerRuntimeReference();
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE — единственная точка входа
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Async pipeline загрузки. async void допустим,
        /// так как это точка входа Unity (MonoBehaviour.Start).
        /// </summary>
        private async void Start()
        {
            IsGameReady = false;

            // ── Деактивируем игрока на время загрузки ──
            DisablePlayer();

            Log("═══════════════════════════════════════════════");
            Log("HECTON-8 Scene Bootstrap — Initializing...");
            Log("═══════════════════════════════════════════════");

            // ── Глобальный таймаут + привязка к жизни GO ──
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(
                destroyCancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(bootstrapTimeout));
            CancellationToken ct = cts.Token;

            try
            {
                // ── STEP 1: Проверка синглтонов ──────────────
                SetStep("Step 1: Verifying Singletons");
                if (!VerifySingletons())
                {
                    Fail("Critical singletons missing! Bootstrap aborted.");
                    return;
                }
                await Awaitable.NextFrameAsync(cancellationToken: ct);
                ct.ThrowIfCancellationRequested();

                // ── STEP 2: Async Warmup пулов ───────────────
                SetStep("Step 2: Pool Warmup");
                await WarmupPoolsAsync(ct);

                // ── STEP 3: Генерация мира ────────────────────
                SetStep("Step 3: World Generation");
                StartWorldGeneration();
                if (worldGenWaitTime > 0f)
                {
                    Log($"  Waiting {worldGenWaitTime}s for world generation...");
                    await Awaitable.WaitForSecondsAsync(
                        worldGenWaitTime, cancellationToken: ct);
                }
                else
                {
                    await Awaitable.NextFrameAsync(cancellationToken: ct);
                    ct.ThrowIfCancellationRequested();
                }

                // ── STEP 4: Загрузка сохранения ──────────────
                SetStep("Step 4: Save/Load");
                await LoadOrNewGameAsync();
                ct.ThrowIfCancellationRequested();

                // ── STEP 5: World-Ready Check ────────────────
                SetStep("Step 5: World-Ready Check");
                await WaitForWorldReadyAsync(ct);

                // ── STEP 6: Ground-Ready Check ───────────────
                SetStep("Step 6: Ground-Ready Check");
                await WaitForGroundReadyAsync(ct);

                // ── STEP 7: Спавн игрока ─────────────────────
                SetStep("Step 7: Player Spawn");
                await SpawnPlayerAsync(ct);
                ct.ThrowIfCancellationRequested();

                // ── STEP 8: Активация + Game Ready ───────────
                SetStep("Step 8: Runtime World Prime");
                await PrimeRuntimeWorldAsync(ct);
                ct.ThrowIfCancellationRequested();

                ActivatePlayer();

                SetStep("Complete");
                _debugCompleted = true;

                Log("═══════════════════════════════════════════════");
                Log("HECTON-8 Scene Bootstrap — GAME READY");
                Log("═══════════════════════════════════════════════");

                IsGameReady = true;
                OnGameReady?.Invoke();
            }
            catch (OperationCanceledException)
            {
                IsGameReady = false;
                // Если GO уничтожен — тихий выход, не спамим ошибкой
                if (this == null || destroyCancellationToken.IsCancellationRequested)
                    return;

                Fail($"Bootstrap timed out after {bootstrapTimeout}s! " +
                     $"Last step: {_debugCurrentStep}");
            }
            catch (Exception ex)
            {
                IsGameReady = false;
                // Если GO уже уничтожен — не трогаем
                if (this == null) return;

                Fail($"Bootstrap failed at [{_debugCurrentStep}]: " +
                     $"{ex.Message}\n{ex.StackTrace}");
            }
        }

        private void OnDestroy()
        {
            if (CurrentPlayerObject == playerObject)
                CurrentPlayerObject = null;

            HasActiveInstance = false;

            if (Application.isPlaying)
                IsGameReady = false;
        }

        /// <summary>
        /// Tries to return the current player transform without forcing callers
        /// to query the scene.
        /// </summary>
        public static bool TryGetCurrentPlayerTransform(out Transform playerTransform)
        {
            playerTransform = CurrentPlayerTransform;
            return playerTransform != null;
        }

        // ══════════════════════════════════════════════════════════
        //  STEP 1 — VERIFY SINGLETONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Проверяет наличие критических синглтонов.
        /// GameTickManager проверяется первым — он должен проснуться
        /// раньше всех (DefaultExecutionOrder на нём ещё ниже).
        ///
        /// Некритические системы проверяются с Warning, не блокируют.
        /// </summary>
        /// <returns>true если все критические синглтоны на месте.</returns>
        private bool VerifySingletons()
        {
            bool allCritical = true;

            // ── Критические (блокируют запуск) ──

            // GameTickManager — ПЕРВЫМ: все тиковые системы зависят от него
            if (GameTickManager.Instance == null)
            {
                Debug.LogError("[SceneBootstrap] GameTickManager NOT FOUND! " +
                    "Create a GameObject with GameTickManager component.");
                allCritical = false;
            }
            else
            {
                Log("  ✓ GameTickManager (awakened first)");
            }

            if (ObjectPoolManager.Instance == null)
            {
                Debug.LogError("[SceneBootstrap] ObjectPoolManager NOT FOUND! " +
                    "Create a GameObject with ObjectPoolManager component.");
                allCritical = false;
            }
            else
            {
                Log("  ✓ ObjectPoolManager");
            }

            if (SaveManager.Instance == null)
            {
                Debug.LogError("[SceneBootstrap] SaveManager NOT FOUND! " +
                    "Create a GameObject with SaveManager component.");
                allCritical = false;
            }
            else
            {
                Log("  ✓ SaveManager");
            }

            // ── Некритические (Warning, не блокируют) ──

            if (Hecton8.World.WorldStateManager.Instance == null)
            {
                Debug.LogWarning("[SceneBootstrap] WorldStateManager not found. " +
                    "Resource node persistence will be disabled.");
            }
            else
            {
                Log("  ✓ WorldStateManager");
            }

            if (Hecton8.Construction.ConstructionManager.Instance == null)
            {
                Debug.LogWarning("[SceneBootstrap] ConstructionManager not found. " +
                    "Base construction persistence will be disabled.");
            }
            else
            {
                Log("  ✓ ConstructionManager");
            }

            return allCritical;
        }

        // ══════════════════════════════════════════════════════════
        //  STEP 2 — ASYNC WARMUP POOLS (ZERO-HITCH)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Предсоздаёт объекты в пуле для устранения хитчей
        /// при первом спавне. Работает batch'ами по
        /// <see cref="WarmupBatchSize"/> инстансов, вызывая
        /// <c>await Awaitable.NextFrameAsync()</c> между ними — это позволяет
        /// Unity отрисовать кадр загрузочного экрана.
        /// </summary>
        private async Awaitable WarmupPoolsAsync(CancellationToken ct)
        {
            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null) return;

            int totalWarmed = 0;

            for (int i = 0, entryCount = warmupEntries.Count; i < entryCount; i++)
            {
                WarmupEntry entry = warmupEntries[i];

                if (entry.prefab == null)
                {
                    Debug.LogWarning(
                        $"[SceneBootstrap] Warmup entry [{i}] has null prefab. Skipping.");
                    continue;
                }

                if (entry.count <= 0) continue;

                string label = string.IsNullOrEmpty(entry.label)
                    ? entry.prefab.name
                    : entry.label;

                // ── Прогрев batch'ами с yield между ними ──
                for (int created = 0; created < entry.count; )
                {
                    int batch = Mathf.Min(WarmupBatchSize, entry.count - created);
                    pool.Warmup(entry.prefab, batch);
                    created += batch;
                    totalWarmed += batch;

                    SetStep($"Warming Pool: {label} ({created}/{entry.count})");

                    // Отдаём кадр Unity для отрисовки loading screen
                    await Awaitable.NextFrameAsync(cancellationToken: ct);
                    ct.ThrowIfCancellationRequested();
                }

                Log($"  Pool: {label} × {entry.count}");
            }

            Log($"  Total warmed: {totalWarmed} objects");
        }

        // ══════════════════════════════════════════════════════════
        //  STEP 3 — WORLD GENERATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Запускает генерацию мира.
        ///
        /// Приоритет:
        ///   1. MapMagic — текущий основной источник мира/террейна.
        ///   2. HectonWorldGenerator — legacy/side-path fallback, если он всё ещё активен.
        ///   3. Ни один не найден — статическая сцена.
        ///
        /// После этого метода pipeline ждёт <see cref="worldGenWaitTime"/>
        /// секунд, чтобы генератор успел создать начальные чанки.
        /// </summary>
        private void StartWorldGeneration()
        {
            var allBehaviours = FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Exclude);

            MonoBehaviour hectonWorldGen = null;
            MonoBehaviour mapMagic = null;

            for (int i = 0, len = allBehaviours.Length; i < len; i++)
            {
                string typeName = allBehaviours[i].GetType().Name;

                if (hectonWorldGen == null && typeName == "HectonWorldGenerator")
                {
                    hectonWorldGen = allBehaviours[i];
                }

                if (mapMagic == null &&
                    (typeName == "MapMagicObject" || typeName == "MapMagic"))
                {
                    mapMagic = allBehaviours[i];
                }
            }

            if (mapMagic != null)
            {
                Log($"  MapMagic active: {mapMagic.gameObject.name}");
                return;
            }

            if (hectonWorldGen != null)
            {
                Log($"  HectonWorldGenerator active (legacy path): {hectonWorldGen.gameObject.name}");
                return;
            }

            Log("  No world generator found — using static scene geometry.");
        }

        // ══════════════════════════════════════════════════════════
        //  STEP 4 — ASYNC SAVE / LOAD
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Загружает сохранение из указанного слота через
        /// <c>SaveManager.LoadGameAsync</c>.
        ///
        /// Если handoff context отсутствует, слот пуст, сейв не найден
        /// или forceNewGame включён — New Game.
        /// При исключении в LoadGameAsync — принудительный fallback
        /// на <see cref="InitNewGame"/>.
        ///
        /// Устанавливает <see cref="_isLoadingSave"/> = true при успешной
        /// загрузке сейва, что активирует Ground-Ready Check в Step 6.
        /// </summary>
        private async Awaitable LoadOrNewGameAsync()
        {
            SaveManager save = SaveManager.Instance;

            // ── Читаем контекст игровой сессии ──
            GameStartContext context;

            // ── Fallback на PlayerPrefs если контекст пуст (domain reload) ──
            if (!GameStartContextHolder.TryGetCurrentOrRestore(out context))
            {
                context = GameStartContext.CreateNewGame();

                Log($"  GameStartContext was empty — restored from PlayerPrefs. " +
                    string.Empty);
            }
            else
            {
                Log($"  GameStartContext loaded: {context}");
            }

            GameStartContextHolder.ClearPersistedHandoff();

            // ── Force New Game из Inspector (legacy) ──
            if (forceNewGame)
            {
                Log("  Force New Game enabled (Inspector) — overriding context.");
                context = GameStartContext.CreateNewGame();
            }

            GameStartContextHolder.Current = context;

            // ── NewGame режим ──
            if (context.StartMode == GameStartMode.NewGame)
            {
                Log("  Starting New Game.");
                _isLoadingSave = false;
                InitNewGame();
                return;
            }

            // ── LoadGame / Resume режим: ищем сейв ──
            string targetSlot = context.TargetSaveSlot;

            if (string.IsNullOrEmpty(targetSlot))
            {
                Log("  No save slot in context — starting New Game.");
                _isLoadingSave = false;
                InitNewGame();
                return;
            }

            // ── Проверка существования ──
            if (!save.SaveExists(targetSlot))
            {
                Log($"  Save '{targetSlot}' not found — starting New Game.");
                _isLoadingSave = false;
                InitNewGame();
                return;
            }

            // ── Async загрузка с обработкой исключений ──
            try
            {
                Log($"  Loading save: '{targetSlot}' (Mode={context.StartMode})...");
                await save.LoadGameAsync(targetSlot);

                if (!save.LastOperationSucceeded)
                {
                    string reason = string.IsNullOrEmpty(save.LastOperationError)
                        ? "unknown load failure"
                        : save.LastOperationError;
                    Debug.LogError(
                        $"[SceneBootstrap] Save load reported failure: {reason}. " +
                        "Falling back to New Game.");
                    _isLoadingSave = false;
                    InitNewGame();
                    return;
                }

                string sourceLabel = save.LastLoadUsedBackup ? "backup" : "primary";
                string repairLabel = save.LastLoadSelfRepaired ? " with self-repair" : string.Empty;
                string compressionLabel = save.LastLoadUsedLegacyCompression ? " using legacy compression" : string.Empty;
                Log($"  Save loaded successfully from {sourceLabel}{repairLabel}{compressionLabel}.");

                _isLoadingSave = true;
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[SceneBootstrap] Save load failed: {ex.Message}. " +
                    "Falling back to New Game.");
                _isLoadingSave = false;
                InitNewGame();
            }
        }

        /// <summary>
        /// Инициализация новой игры.
        /// Очищает все персистентные данные.
        /// </summary>
        private void InitNewGame()
        {
            Log("  Initializing New Game...");

            // Очистка состояния мира
            Hecton8.World.WorldStateManager wsm =
                Hecton8.World.WorldStateManager.Instance;
            if (wsm != null)
                wsm.ClearAll();

            // Очистка построек
            Hecton8.Construction.ConstructionManager cm =
                Hecton8.Construction.ConstructionManager.Instance;
            if (cm != null)
                cm.ClearAllModules();

            Log("  New Game initialized.");
        }

        // ══════════════════════════════════════════════════════════
        //  STEP 5 — WORLD-READY CHECK
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ожидает, пока <c>ScavengePopulator.PendingSpawnCount</c>
        /// опустится ниже <see cref="WorldReadyThreshold"/>.
        ///
        /// Гарантирует, что ресурсы вокруг игрока заспавнятся
        /// до того, как упадёт штора загрузочного экрана.
        ///
        /// Если ScavengePopulator отсутствует — пропускает шаг.
        /// </summary>
        private async Awaitable WaitForWorldReadyAsync(CancellationToken ct)
        {
            var populator = Hecton8.Core.ScavengePopulator.Instance;

            if (populator == null)
            {
                Log("  ScavengePopulator not found — skipping world-ready check.");
                return;
            }

            Log($"  Waiting for world population " +
                $"(threshold: ≤{WorldReadyThreshold})...");

            while (populator.PendingSpawnCount > WorldReadyThreshold)
            {
                SetStep($"Populating World: " +
                        $"{populator.PendingSpawnCount} remaining");
                await Awaitable.WaitForSecondsAsync(
                    WorldReadyPollIntervalSec, cancellationToken: ct);
            }

            Log($"  World ready. Pending: {populator.PendingSpawnCount}");
        }

        // ══════════════════════════════════════════════════════════
        //  STEP 6 — GROUND-READY CHECK (v2.0)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Ожидает появления коллайдера под ногами игрока.
        ///
        /// Решает race condition между загрузкой сохранения (игрок на -500м
        /// в пещере) и асинхронной генерацией вокселей (MeshCollider ещё
        /// не существует). Без этой проверки Rigidbody провалит игрока
        /// в бездну при активации.
        ///
        /// КОГДА ВЫПОЛНЯЕТСЯ:
        ///   Только при загрузке сохранения (_isLoadingSave == true).
        ///   При New Game — пропускается (fallback на поверхности).
        ///
        /// КАК РАБОТАЕТ:
        ///   Raycast вниз от позиции игрока (+2м вверх для запаса).
        ///   Если под ногами есть ЛЮБОЙ коллайдер — ground ready.
        ///   Если нет — ждёт, проверяя каждые 0.2 секунды.
        ///
        /// ТАЙМАУТ:
        ///   Максимум groundReadyTimeout секунд. При превышении —
        ///   Warning и активация anyway (лучше провалиться чем зависнуть).
        ///
        /// ZERO GC:
        ///   Physics.Raycast(Ray, float) без out RaycastHit — zero allocation.
        ///   Нет строковых операций в цикле ожидания.
        /// </summary>
        private async Awaitable WaitForGroundReadyAsync(CancellationToken ct)
        {
            // ── Пропуск при New Game ──
            if (!_isLoadingSave)
            {
                Log("  New Game — skipping ground-ready check.");
                return;
            }

            // ── Получить целевую позицию ──
            // Игрок уже позиционирован через ISaveable в Step 4.
            // playerObject деактивирован, но Transform доступен.
            if (playerObject == null)
            {
                Log("  No playerObject — skipping ground-ready check.");
                return;
            }

            Vector3 playerPos = playerObject.transform.position;
            Log($"  Checking ground at saved position: {playerPos}");

            // ── Raycast loop ──
            float elapsed = 0f;
            float lastLogTime = 0f;

            while (elapsed < groundReadyTimeout)
            {
                ct.ThrowIfCancellationRequested();

                // Луч: от позиции игрока + offset вверх, направлен вниз
                Vector3 rayOrigin = playerPos + Vector3.up * GroundCheckRayOffset;
                Ray ray = new Ray(rayOrigin, Vector3.down);

                if (UnityEngine.Physics.Raycast(ray, GroundCheckRayLength))
                {
                    Log($"  Ground found after {elapsed:F1}s. Safe to activate.");
                    return;
                }

                // ── Диагностический лог каждые N секунд ──
                if (elapsed - lastLogTime >= GroundCheckLogIntervalSec)
                {
                    lastLogTime = elapsed;
                    Log($"  Waiting for ground collider... ({elapsed:F0}s elapsed)");
                }

                await Awaitable.WaitForSecondsAsync(
                    GroundCheckPollIntervalSec, cancellationToken: ct);

                elapsed += GroundCheckPollIntervalSec;
            }

            // ── Таймаут ──
            Debug.LogWarning(
                $"[SceneBootstrap] Ground-ready timed out after {groundReadyTimeout}s " +
                $"at position {playerPos}. Activating player without ground confirmation. " +
                "Player may fall through ungenerated geometry.");
        }

        // ══════════════════════════════════════════════════════════
        //  STEP 7 — PLAYER SPAWN (ASYNC)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Асинхронно спавнит или позиционирует игрока.
        ///
        /// Приоритет:
        ///   1. Сейв загружен → позиция восстановлена через ISaveable
        ///   2. playerSpawner с HectonPlayerSpawner →
        ///      await spawner.SpawnPlayerAsync(ct)
        ///   3. playerObject → перемещаем на fallback
        ///   4. Поиск по тегу "Player" → fallback
        ///   5. Ничего нет → Warning
        ///
        /// Активация playerObject и playerController происходит
        /// ПОЗЖЕ, в <see cref="ActivatePlayer"/>, после Game Ready.
        /// </summary>
        private async Awaitable SpawnPlayerAsync(CancellationToken ct)
        {
            // ── Если сейв загружен — позиция уже установлена ──
            if (_isLoadingSave)
            {
                Log("  Player position restored from save.");
                return;
            }

            // ── Попытка через строго типизированный спавнер ──
            if (playerSpawner != null)
            {
                if (playerSpawner.TryGetComponent(out HectonPlayerSpawner spawner))
                {
                    Log("  Using HectonPlayerSpawner (async)...");
                    await spawner.SpawnPlayerAsync(ct);
                    Log("  HectonPlayerSpawner completed.");
                    PublishPlayerRuntimeReference();
                    return;
                }

                Log("  playerSpawner assigned but has no HectonPlayerSpawner component. " +
                    "Falling through to fallback.");
            }

            // ── Fallback: прямое позиционирование ──
            if (playerObject != null)
            {
                Log($"  Placing player at fallback: {fallbackSpawnPosition}");
                playerObject.transform.position = fallbackSpawnPosition;
                PublishPlayerRuntimeReference();
                return;
            }

            // ── Поиск игрока в сцене по тегу ──
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Log($"  Found player by tag, " +
                    $"placing at fallback: {fallbackSpawnPosition}");
                playerObject = player;
                player.transform.position = fallbackSpawnPosition;
                PublishPlayerRuntimeReference();
                return;
            }

            Debug.LogWarning(
                "[SceneBootstrap] No player spawner, no player object, " +
                "no 'Player' tag found. Player spawn skipped!");
        }

        // ══════════════════════════════════════════════════════════
        //  PLAYER ACTIVATION / DEACTIVATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Выполняет скрытый предзапуск тяжёлого scatter rebuild до того, как
        /// игрок получит управление. Это убирает первый стартовый burst из
        /// первых секунд плавания и не меняет сами правила появления мира.
        /// </summary>
        private async Awaitable PrimeRuntimeWorldAsync(CancellationToken ct)
        {
            if (!prewarmProceduralScatterBeforePlayerActivation)
            {
                Log("  Runtime world prime disabled.");
                return;
            }

            _worldProceduralScatterDirector ??=
                FindAnyObjectByType<WorldProceduralScatterDirector>(FindObjectsInactive.Include);

            if (_worldProceduralScatterDirector == null)
            {
                Log("  WorldProceduralScatterDirector not found — skipping runtime world prime.");
                return;
            }

            int passCount = Mathf.Clamp(scatterBootstrapPrimePasses, 1, 4);
            Log($"  Priming procedural scatter before player activation ({passCount} pass(es))...");

            for (int i = 0; i < passCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                SetStep($"Step 8: Runtime World Prime ({i + 1}/{passCount})");
                if (!_worldProceduralScatterDirector.TryPrimeBootstrapScatterPass())
                {
                    Log("  Scatter prime skipped — runtime prerequisites are not ready yet.");
                    return;
                }

                if (!_worldProceduralScatterDirector.HasPendingStartupPlacements)
                {
                    Log($"  Scatter prime completed in {i + 1} pass(es).");
                    return;
                }

                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }

            Log("  Scatter prime reached pass limit. Remaining startup placements will finish after activation.");
        }

        /// <summary>
        /// Деактивирует игрока и его контроллер на время загрузки.
        /// Вызывается первым в Start(), до любых await.
        ///
        /// v2.0: Дополнительно переводит Rigidbody в isKinematic = true.
        /// Это страховка: даже если объект активируется раньше времени,
        /// физика не потянет его вниз. isKinematic снимается только
        /// в <see cref="ActivatePlayer"/> после всех проверок.
        /// </summary>
        private void DisablePlayer()
        {
            PublishPlayerRuntimeReference();

            // ── Кэширование Rigidbody ──
            if (playerRigidbody == null && playerObject != null)
            {
                playerRigidbody = playerObject.GetComponent<Rigidbody>();
            }

            // ── isKinematic safety (ПЕРЕД деактивацией) ──
            // Устанавливаем на АКТИВНОМ объекте, чтобы Rigidbody
            // не обработал ни одного кадра физики при пробуждении.
            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = true;
                Log("  Rigidbody → isKinematic (physics frozen).");
            }

            if (playerObject != null && playerObject.activeSelf)
            {
                playerObject.SetActive(false);
                Log("  Player deactivated for loading.");
            }

            if (playerController != null)
                playerController.enabled = false;
        }

        /// <summary>
        /// Включает playerObject и playerController.
        /// Вызывается ТОЛЬКО после прохождения всех шагов pipeline,
        /// непосредственно перед OnGameReady.
        ///
        /// v2.0: Снимает isKinematic, возвращая Rigidbody в нормальный
        /// режим. К этому моменту Ground-Ready Check уже подтвердил
        /// наличие коллайдера под ногами (или истёк таймаут).
        /// </summary>
        private void ActivatePlayer()
        {
            PublishPlayerRuntimeReference();

            if (playerObject != null)
            {
                playerObject.SetActive(true);
                Log("  Player activated.");
            }

            // ── Снимаем isKinematic ПОСЛЕ активации объекта ──
            // Rigidbody нужен активный объект для корректной работы.
            if (playerRigidbody != null)
            {
                playerRigidbody.isKinematic = false;
                Log("  Rigidbody → dynamic (physics resumed).");
            }

            if (playerController != null)
            {
                playerController.enabled = true;
                Log("  PlayerController enabled.");
            }
        }

        private void PublishPlayerRuntimeReference()
        {
            if (playerObject == null && playerRigidbody != null)
                playerObject = playerRigidbody.gameObject;

            if (playerObject == null && playerController != null)
                playerObject = playerController.gameObject;

            if (playerObject == null)
            {
                GameObject taggedPlayer = GameObject.FindWithTag("Player");
                if (taggedPlayer != null)
                    playerObject = taggedPlayer;
            }

            CurrentPlayerObject = playerObject;
        }

        // ══════════════════════════════════════════════════════════
        //  UTILITIES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Логирует критическую ошибку и стреляет OnBootstrapFailed.
        /// </summary>
        private void Fail(string error)
        {
            Debug.LogError($"[SceneBootstrap] {error}");
            OnBootstrapFailed?.Invoke(error);
        }

        /// <summary>
        /// Условный лог. Только при verboseLogging = true.
        /// Не используем [Conditional] — логи нужны и в билде для QA.
        /// </summary>
        private void Log(string message)
        {
            if (verboseLogging)
                Debug.Log($"[SceneBootstrap] {message}");
        }

        /// <summary>
        /// Обновляет текущий шаг в Inspector для отладки + лог.
        /// </summary>
        private void SetStep(string step)
        {
            _debugCurrentStep = step;
            Log($"── {step} ──");
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (worldGenWaitTime   < 0f)  worldGenWaitTime   = 0f;
            if (bootstrapTimeout   < 1f)  bootstrapTimeout   = 1f;
            if (groundReadyTimeout < 1f)  groundReadyTimeout = 1f;
        }
#endif
    }
}
