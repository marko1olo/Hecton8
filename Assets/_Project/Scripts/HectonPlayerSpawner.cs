// ============================================================================
// HECTON-8 — HectonPlayerSpawner.cs (v3.1)
//
// Безопасный асинхронный спавнер игрока для процедурно генерируемого мира
// (MapMagic 2). Unity 6 Awaitable API — Zero-GC в асинхронных циклах.
//
// КРИТИЧЕСКИЙ ФИКС v3.1:
//   • [BUG] В v3.0 Фаза 2: если Raycast не находит террейн в точке спирали,
//     цикл делает `continue` без инкремента spiralIndex. Точка проверяется
//     бесконечно, если чанк MapMagic никогда не сгенерируется (за пределами
//     карты, ошибка генерации, OOM). Результат: вечный экран загрузки.
//
//   • [FIX] Добавлен per-point retry counter (maxRetriesPerPoint).
//     Каждая точка спирали получает ограниченное количество попыток.
//     При исчерпании — точка пропускается (spiralIndex++).
//     Дедлок математически невозможен.
//
//   • [FIX] Добавлен глобальный таймаут операции (globalTimeoutSec).
//     Time.realtimeSinceStartup проверяется на каждой итерации.
//     При превышении — немедленный fallback спавн.
//     Защита от ВСЕХ возможных зависаний, включая edge cases.
//
//   • [FIX] Фаза 3 получила тот же глобальный таймаут.
//     Хотя в v3.0 Фаза 3 не имела `continue`-бага, глобальный
//     таймаут защищает от медленных nearshore-поисков на огромных картах.
//
// СОХРАНЕНО БЕЗ ИЗМЕНЕНИЙ:
//   • Архимедова спираль, fallback, nearshore search
//   • Zero-GC Raycast: предаллоцированные _rayOrigin, _hitInfo
//   • Rigidbody телепорт: isKinematic, interpolation, velocity reset
//   • Unity 6 Awaitable API (zero-GC awaiter)
//
// АЛГОРИТМ:
//   Фаза 1 — Ожидание генерации террейна в центре карты (таймаут: maxWaitTime).
//   Фаза 2 — Поиск мелководья по Архимедовой спирали (per-point retries + global timeout).
//   Фаза 3 — Fallback: поиск суши → nearshore мелководье (global timeout).
//   Фаза 4 — Аварийный спавн на уровне воды в центре (гарантированно достигается).
//
// ЦЕЛЕВОЕ ЖЕЛЕЗО: NVIDIA GeForce MX350.
// ============================================================================

using System;
using System.Threading;
using UnityEngine;

/// <summary>
/// Безопасный асинхронный спавнер игрока для процедурно генерируемого мира.
/// <para>
/// Ищет точку на поверхности воды вблизи суши, где глубина не слишком большая.
/// Игрок появляется НА воде (Y = WaterLevel) рядом с берегом.
/// </para>
/// <para>
/// <b>Использование из SceneBootstrap:</b>
/// <code>
/// var spawner = FindObjectOfType&lt;HectonPlayerSpawner&gt;();
/// await spawner.SpawnPlayerAsync(ct);
/// </code>
/// </para>
/// </summary>
public class HectonPlayerSpawner : MonoBehaviour
{
    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — PLAYER
    // ══════════════════════════════════════════════════════════════

    [Header("=== Player ===")]
    [Tooltip("Ссылка на Rigidbody игрока (задаётся через Inspector)")]
    [SerializeField] private Rigidbody playerRigidbody;

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — WATER SETTINGS
    // ══════════════════════════════════════════════════════════════

    [Header("=== Water Settings ===")]
    [Tooltip("Уровень моря (Water Level) — высота Y поверхности воды")]
    [SerializeField] private float waterLevel = 4900f;

    [Tooltip("Минимальная допустимая высота дна под водой (защита от слишком глубоких мест)")]
    [SerializeField] private float minSeaFloorHeight = 4800f;

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — RAYCAST SETTINGS
    // ══════════════════════════════════════════════════════════════

    [Header("=== Raycast Settings ===")]
    [Tooltip("Высота, с которой пускается луч вниз для поиска земли")]
    [SerializeField] private float raycastOriginHeight = 10000f;

    [Tooltip("Слой(и) террейна для Raycast")]
    [SerializeField] private LayerMask terrainLayerMask = ~0;

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — SPAWN SEARCH SETTINGS
    // ══════════════════════════════════════════════════════════════

    [Header("=== Spawn Search Settings ===")]
    [Tooltip("Начальная точка поиска по X,Z")]
    [SerializeField] private Vector2 searchOrigin = Vector2.zero;

    [Tooltip("Шаг спирали в метрах (расстояние между витками)")]
    [SerializeField] private float spiralStep = 75f;

    [Tooltip("Максимальное количество точек спирали для проверки")]
    [SerializeField] private int maxSpiralPoints = 500;

    [Tooltip("Смещение игрока над поверхностью воды / землёй")]
    [SerializeField] private float spawnHeightOffset = 2f;

    [Tooltip("Задержка между попытками Raycast (ожидание генерации MapMagic), секунды")]
    [SerializeField] private float retryDelay = 0.5f;

    [Tooltip("Максимальное время ожидания генерации террейна в Фазе 1 (секунды)")]
    [SerializeField] private float maxWaitTime = 60f;

    // ══════════════════════════════════════════════════════════════
    //  INSPECTOR — DEADLOCK PROTECTION (v3.1)
    // ══════════════════════════════════════════════════════════════

    [Header("=== Deadlock Protection (v3.1) ===")]
    [Tooltip("Максимальное количество повторных попыток Raycast для одной точки спирали.\n" +
             "Если террейн не появился за maxRetriesPerPoint × retryDelay секунд — " +
             "точка пропускается.\n" +
             "10 попыток × 0.5с = 5 секунд максимум на точку.")]
    [SerializeField] private int maxRetriesPerPoint = 10;

    [Tooltip("Глобальный таймаут всей операции SpawnPlayerAsync (секунды).\n" +
             "Включает все фазы. При превышении — аварийный спавн.\n" +
             "Защита от ВСЕХ возможных зависаний.")]
    [SerializeField] private float globalTimeoutSec = 120f;

    // ══════════════════════════════════════════════════════════════
    //  CACHED FIELDS — Zero-GC
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Предаллоцированная точка начала луча Raycast.
    /// Переиспользуется во всех вызовах Physics.Raycast
    /// без создания новых Vector3.
    /// </summary>
    private Vector3 _rayOrigin;

    /// <summary>
    /// Предаллоцированная позиция спавна.
    /// Заполняется при нахождении валидной точки.
    /// </summary>
    private Vector3 _spawnPosition;

    /// <summary>
    /// Предаллоцированная структура результата Raycast.
    /// Unity перезаписывает поля при каждом вызове Physics.Raycast.
    /// </summary>
    private RaycastHit _hitInfo;

    /// <summary>
    /// Время начала операции SpawnPlayerAsync (realtimeSinceStartup).
    /// Используется для проверки глобального таймаута.
    /// </summary>
    private float _operationStartTime;

    // ══════════════════════════════════════════════════════════════
    //  ENUMS
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Результат проверки конкретной точки карты.
    /// </summary>
    private enum SpawnSearchResult
    {
        /// <summary>Террейн не обнаружен (чанк ещё не сгенерирован).</summary>
        NoTerrain,

        /// <summary>Слишком глубоко (дно ниже <see cref="minSeaFloorHeight"/>).</summary>
        DeepWater,

        /// <summary>Суша (земля выше уровня моря).</summary>
        AboveWater,

        /// <summary>Мелкая вода вблизи суши — идеально для спавна.</summary>
        ValidShallowWater
    }

    // ══════════════════════════════════════════════════════════════
    //  LIFECYCLE
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Валидация ссылок с автоматическим поиском игрока в сцене.
    /// </summary>
    private void Awake()
    {
        // ── Попытка автоматического поиска, если Inspector-ссылка не задана ──
        if (playerRigidbody == null)
        {
            Debug.Log(
                "[HectonPlayerSpawner] Rigidbody не назначен в Inspector. " +
                "Ищу GameObject с тегом \"Player\" в сцене...");

            GameObject playerGO = GameObject.FindWithTag("Player");

            if (playerGO != null)
            {
                playerRigidbody = playerGO.GetComponent<Rigidbody>();

                if (playerRigidbody != null)
                {
                    Debug.Log(
                        $"[HectonPlayerSpawner] Rigidbody найден автоматически " +
                        $"на объекте \"{playerGO.name}\".");
                }
                else
                {
                    Debug.LogWarning(
                        $"[HectonPlayerSpawner] GameObject \"{playerGO.name}\" " +
                        "найден по тегу \"Player\", но на нём нет компонента Rigidbody.");
                }
            }
            else
            {
                Debug.LogWarning(
                    "[HectonPlayerSpawner] GameObject с тегом \"Player\" " +
                    "не найден в сцене.");
            }
        }

        // ── Финальная проверка после всех попыток поиска ──
        if (playerRigidbody == null)
        {
            Debug.LogError(
                "[HectonPlayerSpawner] Rigidbody игрока не найден! " +
                "Ни Inspector-ссылка, ни поиск по тегу \"Player\" не дали результата. " +
                "Спавн невозможен.",
                this);
            enabled = false;
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  PUBLIC API
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Асинхронный поиск безопасной точки спавна и телепортация игрока.
    ///
    /// v3.1: Защита от дедлока:
    ///   • Per-point retry limit (maxRetriesPerPoint) — каждая точка спирали
    ///     может быть проверена не более N раз. При исчерпании — пропускается.
    ///   • Глобальный таймаут (globalTimeoutSec) — если вся операция
    ///     превышает лимит — немедленный fallback спавн.
    ///   • Бесконечный цикл на одной точке МАТЕМАТИЧЕСКИ НЕВОЗМОЖЕН:
    ///     retryCount инкрементируется при каждой неудаче, spiralIndex++
    ///     гарантированно вызывается при retryCount >= maxRetriesPerPoint.
    /// </summary>
    public async Awaitable SpawnPlayerAsync(CancellationToken ct)
    {
        Debug.Log("[HectonPlayerSpawner] Начинаю поиск безопасной точки спавна...");

        // ── Засекаем глобальный таймер (v3.1) ──
        _operationStartTime = Time.realtimeSinceStartup;

        // ── Подготовка Rigidbody к телепорту ──
        PrepareRigidbodyForTeleport();

        // ══════════════════════════════════════════════════════════
        //  ФАЗА 1: Ожидание генерации террейна в центре карты
        // ══════════════════════════════════════════════════════════

        bool terrainReady = false;
        float waitTimer = 0f;

        _rayOrigin.Set(searchOrigin.x, raycastOriginHeight, searchOrigin.y);

        while (!terrainReady)
        {
            ct.ThrowIfCancellationRequested();

            // ── Глобальный таймаут (v3.1) ──
            if (IsGlobalTimeoutExceeded())
            {
                Debug.LogError(
                    $"[HectonPlayerSpawner] Глобальный таймаут ({globalTimeoutSec}с) " +
                    "в Фазе 1. Аварийный спавн.");
                ForceFallbackSpawn();
                return;
            }

            if (Physics.Raycast(
                    _rayOrigin, Vector3.down, out _hitInfo,
                    raycastOriginHeight * 2f, terrainLayerMask))
            {
                terrainReady = true;
                Debug.Log(
                    $"[HectonPlayerSpawner] Террейн обнаружен в центре карты. " +
                    $"Высота: {_hitInfo.point.y:F1}");
            }
            else
            {
                waitTimer += retryDelay;

                if (waitTimer >= maxWaitTime)
                {
                    Debug.LogError(
                        $"[HectonPlayerSpawner] Таймаут ({maxWaitTime}с): " +
                        "террейн не сгенерирован. Спавню на уровне воды в центре.");
                    ForceFallbackSpawn();
                    return;
                }

                Debug.Log(
                    $"[HectonPlayerSpawner] Террейн ещё не готов, жду... " +
                    $"({waitTimer:F1}с)");
                await Awaitable.WaitForSecondsAsync(retryDelay, cancellationToken: ct);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ФАЗА 2: Поиск мелководья по Архимедовой спирали
        //
        //  v3.1 FIX: Per-point retry counter.
        //
        //  БЫЛО (v3.0, ДЕДЛОК):
        //    if (!Raycast) { await delay; continue; }
        //    // continue пропускает spiralIndex++ → вечный цикл.
        //
        //  СТАЛО (v3.1, БЕЗОПАСНО):
        //    if (!Raycast) {
        //        retryCount++;
        //        if (retryCount >= maxRetriesPerPoint) {
        //            retryCount = 0;
        //            → AdvanceSpiral() (spiralIndex++ guaranteed)
        //        } else {
        //            await delay;
        //        }
        //        continue;
        //    }
        //    retryCount = 0;  // reset on successful raycast
        //    → evaluate point
        //    → AdvanceSpiral()
        //
        //  Максимальное время на одну точку:
        //    maxRetriesPerPoint × retryDelay = 10 × 0.5 = 5 секунд.
        //  После этого — гарантированный переход к следующей точке.
        // ══════════════════════════════════════════════════════════

        // Сначала проверяем центральную точку
        SpawnSearchResult centerResult = EvaluatePoint(searchOrigin.x, searchOrigin.y);

        if (centerResult == SpawnSearchResult.ValidShallowWater)
        {
            TeleportPlayer(_spawnPosition);
            return;
        }

        // ── Инициализация спирали ──
        int spiralIndex = 0;
        float angleStep = 45f;
        float currentAngle = 0f;
        float currentRadius = spiralStep;
        int pointsPerRing = 8;
        int pointInRing = 0;
        int retryCount = 0;  // v3.1: per-point retry counter

        while (spiralIndex < maxSpiralPoints)
        {
            ct.ThrowIfCancellationRequested();

            // ── Глобальный таймаут (v3.1) ──
            if (IsGlobalTimeoutExceeded())
            {
                Debug.LogError(
                    $"[HectonPlayerSpawner] Глобальный таймаут ({globalTimeoutSec}с) " +
                    $"в Фазе 2 на точке {spiralIndex}/{maxSpiralPoints}. Аварийный спавн.");
                ForceFallbackSpawn();
                return;
            }

            // Вычисляем X, Z по спирали
            float rad = currentAngle * Mathf.Deg2Rad;
            float testX = searchOrigin.x + Mathf.Cos(rad) * currentRadius;
            float testZ = searchOrigin.y + Mathf.Sin(rad) * currentRadius;

            _rayOrigin.Set(testX, raycastOriginHeight, testZ);

            if (Physics.Raycast(
                    _rayOrigin, Vector3.down, out _hitInfo,
                    raycastOriginHeight * 2f, terrainLayerMask))
            {
                // ── Raycast успешен — сбрасываем счётчик попыток ──
                retryCount = 0;

                SpawnSearchResult result = EvaluatePointFromHit(testX, testZ);

                if (result == SpawnSearchResult.ValidShallowWater)
                {
                    TeleportPlayer(_spawnPosition);
                    return;
                }

                // Точка не подходит (суша/глубоко) — переходим к следующей
            }
            else
            {
                // ── Террейн не найден — per-point retry (v3.1) ──
                retryCount++;

                if (retryCount < maxRetriesPerPoint)
                {
                    // Ещё есть попытки — ждём и повторяем ТУ ЖЕ точку
                    await Awaitable.WaitForSecondsAsync(retryDelay, cancellationToken: ct);
                    continue; // НЕ инкрементируем spiralIndex — повторяем точку
                }

                // ── Попытки исчерпаны — пропускаем точку (v3.1) ──
                // spiralIndex++ произойдёт ниже через AdvanceSpiral.
                retryCount = 0;

                Debug.Log(
                    $"[HectonPlayerSpawner] Точка спирали #{spiralIndex} " +
                    $"({testX:F0}, {testZ:F0}): террейн не сгенерирован " +
                    $"после {maxRetriesPerPoint} попыток. Пропускаю.");
            }

            // ── Переходим к следующей точке спирали ──
            // Этот код ГАРАНТИРОВАННО выполняется при каждом проходе,
            // кроме случая continue выше (который имеет лимит retryCount).
            spiralIndex++;
            pointInRing++;
            currentAngle += angleStep;

            if (pointInRing >= pointsPerRing)
            {
                pointInRing = 0;
                currentRadius += spiralStep;

                pointsPerRing = Mathf.Max(
                    8,
                    Mathf.RoundToInt(2f * Mathf.PI * currentRadius / spiralStep));
                angleStep = 360f / pointsPerRing;
                currentAngle = 0f;
            }

            // Каждые 16 точек — отдаём кадр Unity (плавный loading screen)
            if ((spiralIndex & 15) == 0)
            {
                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ФАЗА 3: Fallback — поиск суши → nearshore мелководье
        //
        //  v3.1: Добавлен глобальный таймаут. Фаза 3 в v3.0
        //  не имела continue-бага, но nearshore search (8 направлений
        //  × 20 шагов = 160 рейкастов на точку) может быть медленным
        //  на огромных картах. Глобальный таймаут защищает от этого.
        // ══════════════════════════════════════════════════════════

        Debug.LogWarning(
            "[HectonPlayerSpawner] Мелкая вода вблизи суши не найдена " +
            "после полного обхода спирали. Ищу любую сушу...");

        // Сброс параметров спирали для второго прохода
        currentAngle = 0f;
        currentRadius = spiralStep;
        pointsPerRing = 8;
        pointInRing = 0;
        angleStep = 45f;
        spiralIndex = 0;

        while (spiralIndex < maxSpiralPoints)
        {
            ct.ThrowIfCancellationRequested();

            // ── Глобальный таймаут (v3.1) ──
            if (IsGlobalTimeoutExceeded())
            {
                Debug.LogError(
                    $"[HectonPlayerSpawner] Глобальный таймаут ({globalTimeoutSec}с) " +
                    $"в Фазе 3 на точке {spiralIndex}/{maxSpiralPoints}. Аварийный спавн.");
                ForceFallbackSpawn();
                return;
            }

            float rad = currentAngle * Mathf.Deg2Rad;
            float testX = searchOrigin.x + Mathf.Cos(rad) * currentRadius;
            float testZ = searchOrigin.y + Mathf.Sin(rad) * currentRadius;

            _rayOrigin.Set(testX, raycastOriginHeight, testZ);

            if (Physics.Raycast(
                    _rayOrigin, Vector3.down, out _hitInfo,
                    raycastOriginHeight * 2f, terrainLayerMask))
            {
                float groundY = _hitInfo.point.y;

                // Нашли сушу — ищем ближайшую точку мелководья
                if (groundY > waterLevel)
                {
                    bool foundNearshore = TryFindNearshorePoint(testX, testZ);
                    if (foundNearshore)
                    {
                        TeleportPlayer(_spawnPosition);
                        return;
                    }
                }
            }
            // v3.1: Фаза 3 НЕ ждёт генерации террейна — просто пропускает.
            // Если террейна нет — переходим к следующей точке немедленно.

            // ── Всегда продвигаем спираль (нет continue → нет дедлока) ──
            spiralIndex++;
            pointInRing++;
            currentAngle += angleStep;

            if (pointInRing >= pointsPerRing)
            {
                pointInRing = 0;
                currentRadius += spiralStep;
                pointsPerRing = Mathf.Max(
                    8,
                    Mathf.RoundToInt(2f * Mathf.PI * currentRadius / spiralStep));
                angleStep = 360f / pointsPerRing;
                currentAngle = 0f;
            }

            // Каждые 16 точек — yield для плавности
            if ((spiralIndex & 15) == 0)
            {
                await Awaitable.NextFrameAsync(cancellationToken: ct);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  ФАЗА 4: Абсолютный fallback
        // ══════════════════════════════════════════════════════════

        Debug.LogWarning(
            "[HectonPlayerSpawner] Ни суша, ни мелководье не найдены. " +
            "Спавню на уровне воды в центре.");
        ForceFallbackSpawn();
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — GLOBAL TIMEOUT CHECK (v3.1)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Проверяет, превышен ли глобальный таймаут операции.
    /// Использует Time.realtimeSinceStartup — не зависит от Time.timeScale.
    /// Zero GC: float comparison.
    /// </summary>
    /// <returns>true если операция выполняется дольше globalTimeoutSec.</returns>
    private bool IsGlobalTimeoutExceeded()
    {
        return (Time.realtimeSinceStartup - _operationStartTime) >= globalTimeoutSec;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — EVALUATION
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Оценивает точку карты, пуская Raycast вниз.
    /// При нахождении валидного мелководья заполняет _spawnPosition.
    /// Zero-GC: предаллоцированные _rayOrigin и _hitInfo.
    /// </summary>
    private SpawnSearchResult EvaluatePoint(float x, float z)
    {
        _rayOrigin.Set(x, raycastOriginHeight, z);

        if (!Physics.Raycast(
                _rayOrigin, Vector3.down, out _hitInfo,
                raycastOriginHeight * 2f, terrainLayerMask))
        {
            return SpawnSearchResult.NoTerrain;
        }

        return EvaluatePointFromHit(x, z);
    }

    /// <summary>
    /// Оценивает точку на основе уже заполненного _hitInfo.
    /// Вызывается после успешного Raycast.
    /// </summary>
    private SpawnSearchResult EvaluatePointFromHit(float x, float z)
    {
        float groundY = _hitInfo.point.y;

        // Суша — земля выше уровня воды
        if (groundY >= waterLevel)
        {
            return SpawnSearchResult.AboveWater;
        }

        // Слишком глубоко — дно ниже порога
        if (groundY < minSeaFloorHeight)
        {
            return SpawnSearchResult.DeepWater;
        }

        // Мелкая вода — идеальная точка, игрок появляется НА поверхности
        _spawnPosition.Set(x, waterLevel + spawnHeightOffset, z);
        return SpawnSearchResult.ValidShallowWater;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — NEARSHORE SEARCH
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Из точки суши пытается найти ближайшую точку мелководья,
    /// двигаясь от суши в 8 направлениях с шагом 10 метров (до 200м).
    /// При успехе — заполняет _spawnPosition.
    /// </summary>
    private bool TryFindNearshorePoint(float landX, float landZ)
    {
        const float step = 10f;
        const int maxSteps = 20;

        for (int dir = 0; dir < 8; dir++)
        {
            float angle = dir * 45f * Mathf.Deg2Rad;
            float dirX = Mathf.Cos(angle);
            float dirZ = Mathf.Sin(angle);

            for (int s = 1; s <= maxSteps; s++)
            {
                float testX = landX + dirX * step * s;
                float testZ = landZ + dirZ * step * s;

                SpawnSearchResult result = EvaluatePoint(testX, testZ);

                if (result == SpawnSearchResult.ValidShallowWater)
                {
                    return true;
                }
            }
        }

        return false;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — RIGIDBODY TELEPORT
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Подготавливает Rigidbody к безопасному телепорту.
    /// </summary>
    private void PrepareRigidbodyForTeleport()
    {
        playerRigidbody.isKinematic = true;
        playerRigidbody.interpolation = RigidbodyInterpolation.None;
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// Телепортирует игрока в указанную позицию.
    /// Безопасный порядок операций для Rigidbody.
    /// </summary>
    private void TeleportPlayer(Vector3 position)
    {
        playerRigidbody.isKinematic = true;
        playerRigidbody.interpolation = RigidbodyInterpolation.None;

        playerRigidbody.position = position;
        playerRigidbody.transform.position = position;

        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;

        playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        playerRigidbody.isKinematic = false;

        float elapsed = Time.realtimeSinceStartup - _operationStartTime;

        Debug.Log(
            $"[HectonPlayerSpawner] ✅ Игрок успешно заспавнен!\n" +
            $"   Координаты: ({position.x:F1}, {position.y:F1}, {position.z:F1})\n" +
            $"   Уровень моря: {waterLevel:F1}\n" +
            $"   Высота дна под игроком: {_hitInfo.point.y:F1}\n" +
            $"   Глубина воды: {waterLevel - _hitInfo.point.y:F1}м\n" +
            $"   Время поиска: {elapsed:F1}с");
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — FALLBACK
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Аварийный спавн на уровне воды в центре карты.
    /// Гарантированно завершает операцию без дедлока.
    /// </summary>
    private void ForceFallbackSpawn()
    {
        _spawnPosition.Set(
            searchOrigin.x,
            waterLevel + spawnHeightOffset,
            searchOrigin.y);

        TeleportPlayer(_spawnPosition);

        float elapsed = Time.realtimeSinceStartup - _operationStartTime;

        Debug.LogWarning(
            $"[HectonPlayerSpawner] ⚠️ Аварийный спавн на " +
            $"({_spawnPosition.x:F1}, {_spawnPosition.y:F1}, {_spawnPosition.z:F1})\n" +
            $"   Время до fallback: {elapsed:F1}с");
    }

    // ══════════════════════════════════════════════════════════════
    //  EDITOR — GIZMOS
    // ══════════════════════════════════════════════════════════════

#if UNITY_EDITOR
    /// <summary>
    /// Визуализация в редакторе: точка поиска, уровень моря,
    /// минимальная глубина и луч Raycast.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        // Точка начала поиска
        Gizmos.color = Color.cyan;
        Vector3 origin = new Vector3(searchOrigin.x, waterLevel, searchOrigin.y);
        Gizmos.DrawWireSphere(origin, 5f);

        // Плоскость уровня моря
        Gizmos.color = new Color(0f, 0.5f, 1f, 0.15f);
        Gizmos.DrawCube(origin, new Vector3(500f, 0.1f, 500f));

        // Уровень минимальной глубины
        Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
        Vector3 minDepthOrigin = new Vector3(
            searchOrigin.x, minSeaFloorHeight, searchOrigin.y);
        Gizmos.DrawCube(minDepthOrigin, new Vector3(500f, 0.1f, 500f));

        // Луч Raycast
        Gizmos.color = Color.yellow;
        Vector3 rayStart = new Vector3(
            searchOrigin.x, raycastOriginHeight, searchOrigin.y);
        Gizmos.DrawLine(rayStart, origin);
    }

    private void OnValidate()
    {
        if (UnityEditor.EditorApplication.isCompiling ||
            UnityEditor.EditorApplication.isUpdating ||
            UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (maxRetriesPerPoint < 1) maxRetriesPerPoint = 1;
        if (globalTimeoutSec < 10f) globalTimeoutSec = 10f;
        if (retryDelay < 0.1f) retryDelay = 0.1f;
        if (maxWaitTime < 5f) maxWaitTime = 5f;
    }
#endif
}
