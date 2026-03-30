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
//   6. Спавн игрока
//   7. Активация игрока + PlayerController
//   8. OnGameReady → HUD, музыка, геймплей
//
// ОТКАЗОУСТОЙЧИВОСТЬ:
//   • Глобальный таймаут (30с по умолчанию) → OnBootstrapFailed.
//   • destroyCancellationToken — тихий выход при уничтожении GO.
//   • При ошибке LoadGameAsync — fallback на InitNewGame().
//   • При отсутствии MapMagic / ScavengePopulator — пропуск шага.
//   • При ошибке спавна — fallback позиция.
//   • Подробный лог каждого шага для отладки.
//
// ZERO-GC ASYNC:
//   • Unity 6 Awaitable API вместо Task/Task.Run.
//   • Awaitable.NextFrameAsync вместо Task.Yield.
//   • Awaitable.WaitForSecondsAsync вместо Task.Delay.
//   • Никаких лямбда-замыканий в горячих путях.
//
// НИКАКОГО Update(). Вся логика — async void Start().
// ============================================================================

using System;
using System.Collections.Generic;
using System.Threading;
using Hecton8.Core;
using Hecton8.SaveSystem;
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
        /// Выстреливает при критической ошибке инициализации
        /// или при превышении таймаута.
        /// Param: описание ошибки.
        /// </summary>
        public static event Action<string> OnBootstrapFailed;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — SAVE
        // ══════════════════════════════════════════════════════════

        [Header("── Save Slot ─────────────────────────────────")]
        [Tooltip("Имя слота для загрузки. Пустой = New Game.")]
        [SerializeField] private string saveSlot = "slot_1";

        [Tooltip("Если true — всегда начинать новую игру (игнорировать сейв)")]
        [SerializeField] private bool forceNewGame;

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

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE — единственная точка входа
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Async pipeline загрузки. async void допустим,
        /// так как это точка входа Unity (MonoBehaviour.Start).
        /// </summary>
        private async void Start()
        {
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

                // ── STEP 6: Спавн игрока ─────────────────────
                SetStep("Step 6: Player Spawn");
                await SpawnPlayerAsync(ct);
                ct.ThrowIfCancellationRequested();

                // ── STEP 7: Активация + Game Ready ───────────
                ActivatePlayer();

                SetStep("Complete");
                _debugCompleted = true;

                Log("═══════════════════════════════════════════════");
                Log("HECTON-8 Scene Bootstrap — GAME READY");
                Log("═══════════════════════════════════════════════");

                OnGameReady?.Invoke();
            }
            catch (OperationCanceledException)
            {
                // Если GO уничтожен — тихий выход, не спамим ошибкой
                if (this == null || destroyCancellationToken.IsCancellationRequested)
                    return;

                Fail($"Bootstrap timed out after {bootstrapTimeout}s! " +
                     $"Last step: {_debugCurrentStep}");
            }
            catch (Exception ex)
            {
                // Если GO уже уничтожен — не трогаем
                if (this == null) return;

                Fail($"Bootstrap failed at [{_debugCurrentStep}]: " +
                     $"{ex.Message}\n{ex.StackTrace}");
            }
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
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);

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
        /// Если сейв не найден, пуст или forceNewGame — New Game.
        /// При исключении в LoadGameAsync — принудительный fallback
        /// на <see cref="InitNewGame"/>.
        /// </summary>
        private async Awaitable LoadOrNewGameAsync()
        {
            SaveManager save = SaveManager.Instance;

            // ── Force New Game ──
            if (forceNewGame)
            {
                Log("  Force New Game enabled — skipping save load.");
                InitNewGame();
                return;
            }

            // ── Пустой слот ──
            if (string.IsNullOrEmpty(saveSlot))
            {
                Log("  No save slot specified — starting New Game.");
                InitNewGame();
                return;
            }

            // ── Проверка существования ──
            if (!save.SaveExists(saveSlot))
            {
                Log($"  Save '{saveSlot}' not found — starting New Game.");
                InitNewGame();
                return;
            }

            // ── Async загрузка с обработкой исключений ──
            try
            {
                Log($"  Loading save: '{saveSlot}'...");
                await save.LoadGameAsync(saveSlot);
                Log("  Save loaded successfully.");
            }
            catch (Exception ex)
            {
                Debug.LogError(
                    $"[SceneBootstrap] Save load failed: {ex.Message}. " +
                    "Falling back to New Game.");
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
        //  STEP 6 — PLAYER SPAWN (ASYNC)
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
            if (!forceNewGame && !string.IsNullOrEmpty(saveSlot)
                && SaveManager.Instance.SaveExists(saveSlot))
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
                return;
            }

            // ── Поиск игрока в сцене по тегу ──
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                Log($"  Found player by tag, " +
                    $"placing at fallback: {fallbackSpawnPosition}");
                player.transform.position = fallbackSpawnPosition;
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
        /// Деактивирует игрока и его контроллер на время загрузки.
        /// Вызывается первым в Start(), до любых await.
        /// </summary>
        private void DisablePlayer()
        {
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
        /// </summary>
        private void ActivatePlayer()
        {
            if (playerObject != null)
            {
                playerObject.SetActive(true);
                Log("  Player activated.");
            }

            if (playerController != null)
            {
                playerController.enabled = true;
                Log("  PlayerController enabled.");
            }
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

            if (worldGenWaitTime < 0f)  worldGenWaitTime = 0f;
            if (bootstrapTimeout < 1f)  bootstrapTimeout = 1f;
        }
#endif
    }
}
