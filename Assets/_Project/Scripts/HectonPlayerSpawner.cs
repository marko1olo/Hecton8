// ============================================================================
// HECTON-8 — HectonPlayerSpawner.cs (v3.0)
//
// Безопасный асинхронный спавнер игрока для процедурно генерируемого мира
// (MapMagic 2). Unity 6 Awaitable API — Zero-GC в асинхронных циклах.
//
// КЛЮЧЕВЫЕ ИЗМЕНЕНИЯ v3.0:
//   • async Task → async Awaitable (Unity 6 native, zero-GC awaiter)
//   • Task.Delay → Awaitable.WaitForSecondsAsync (float секунды, без int мс)
//   • Task.Yield → Awaitable.NextFrameAsync (Player Loop native)
//   • Удалено поле _retryDelayMs и его предвычисление
//   • using System.Threading.Tasks удалён
//
// СОХРАНЕНО БЕЗ ИЗМЕНЕНИЙ:
//   • Архимедова спираль, fallback, nearshore search
//   • Zero-GC Raycast: предаллоцированные _rayOrigin, _hitInfo
//   • Rigidbody телепорт: isKinematic, interpolation, velocity reset
//   • Публичный API: SpawnPlayerAsync(CancellationToken)
//
// АЛГОРИТМ:
//   Фаза 1 — Ожидание генерации террейна в центре карты.
//   Фаза 2 — Поиск мелководья по Архимедовой спирали.
//   Фаза 3 — Fallback: поиск суши → nearshore мелководье.
//   Фаза 4 — Аварийный спавн на уровне воды в центре.
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

    [Tooltip("Максимальное время ожидания генерации террейна (секунды)")]
    [SerializeField] private float maxWaitTime = 60f;

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
    /// <para>
    /// Порядок:
    /// <list type="number">
    ///   <item>Проверяет, назначен ли <see cref="playerRigidbody"/> через Inspector.</item>
    ///   <item>Если нет — ищет GameObject с тегом "Player" в сцене.</item>
    ///   <item>Если найден — получает его Rigidbody.</item>
    ///   <item>Если после всех попыток Rigidbody всё ещё null — выводит ошибку
    ///         и отключает компонент.</item>
    /// </list>
    /// </para>
    /// <para>
    /// <b>Start() удалён намеренно</b> — спавнер не запускается сам.
    /// Вызов инициируется извне через <see cref="SpawnPlayerAsync"/>.
    /// </para>
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
    /// <para>
    /// Использует Unity 6 Awaitable API — zero-GC awaiter,
    /// нативная интеграция с Player Loop.
    /// </para>
    /// <para>
    /// Вызывается из <c>SceneBootstrap</c>:
    /// <code>
    /// var spawner = FindObjectOfType&lt;HectonPlayerSpawner&gt;();
    /// if (spawner != null)
    ///     await spawner.SpawnPlayerAsync(ct);
    /// </code>
    /// </para>
    /// <para>
    /// Алгоритм:
    /// <list type="number">
    ///   <item>Ожидание генерации террейна в центре карты.</item>
    ///   <item>Поиск мелководья по Архимедовой спирали.</item>
    ///   <item>Fallback: поиск суши → nearshore мелководье.</item>
    ///   <item>Аварийный спавн на уровне воды в центре.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="ct">
    /// Токен отмены. Передаётся из <c>SceneBootstrap</c>,
    /// связан с <c>destroyCancellationToken</c> и глобальным таймаутом.
    /// При срабатывании — выбрасывается <see cref="OperationCanceledException"/>.
    /// </param>
    /// <exception cref="OperationCanceledException">
    /// Бросается при отмене через <paramref name="ct"/>
    /// (уничтожение сцены или таймаут SceneBootstrap).
    /// </exception>
    public async Awaitable SpawnPlayerAsync(CancellationToken ct)
    {
        Debug.Log("[HectonPlayerSpawner] Начинаю поиск безопасной точки спавна...");

        // ── Подготовка Rigidbody к телепорту ──
        // Отключаем интерполяцию и переводим в кинематический режим,
        // чтобы физика не «дёргала» игрока во время поиска точки.
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
        // ══════════════════════════════════════════════════════════

        // Сначала проверяем центральную точку
        SpawnSearchResult centerResult = EvaluatePoint(searchOrigin.x, searchOrigin.y);

        if (centerResult == SpawnSearchResult.ValidShallowWater)
        {
            TeleportPlayer(_spawnPosition);
            return;
        }

        // Архимедова спираль: angle увеличивается, radius ∝ angle.
        // Даёт равномерное покрытие территории вокруг центра.
        int spiralIndex = 0;
        float angleStep = 45f;       // градусов на шаг (8 направлений на первом витке)
        float currentAngle = 0f;
        float currentRadius = spiralStep;
        int pointsPerRing = 8;
        int pointInRing = 0;

        while (spiralIndex < maxSpiralPoints)
        {
            ct.ThrowIfCancellationRequested();

            // Вычисляем X, Z по спирали (Zero-GC: без new Vector3)
            float rad = currentAngle * Mathf.Deg2Rad;
            float testX = searchOrigin.x + Mathf.Cos(rad) * currentRadius;
            float testZ = searchOrigin.y + Mathf.Sin(rad) * currentRadius;

            // Пускаем Raycast через предаллоцированные структуры
            _rayOrigin.Set(testX, raycastOriginHeight, testZ);

            if (Physics.Raycast(
                    _rayOrigin, Vector3.down, out _hitInfo,
                    raycastOriginHeight * 2f, terrainLayerMask))
            {
                SpawnSearchResult result = EvaluatePointFromHit(testX, testZ);

                if (result == SpawnSearchResult.ValidShallowWater)
                {
                    TeleportPlayer(_spawnPosition);
                    return;
                }
            }
            else
            {
                // Террейн в этой точке ещё не сгенерирован — ждём
                await Awaitable.WaitForSecondsAsync(retryDelay, cancellationToken: ct);
                continue; // Повторяем ту же точку
            }

            // Переходим к следующей точке спирали
            spiralIndex++;
            pointInRing++;
            currentAngle += angleStep;

            if (pointInRing >= pointsPerRing)
            {
                // Следующее кольцо спирали
                pointInRing = 0;
                currentRadius += spiralStep;

                // Увеличиваем плотность точек пропорционально радиусу
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
    //  PRIVATE — EVALUATION
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Оценивает точку карты, пуская Raycast вниз из <paramref name="x"/>, <paramref name="z"/>.
    /// При нахождении валидного мелководья заполняет <see cref="_spawnPosition"/>.
    /// <para>
    /// Использует предаллоцированные <see cref="_rayOrigin"/> и <see cref="_hitInfo"/>
    /// для Zero-GC работы.
    /// </para>
    /// </summary>
    /// <param name="x">Координата X точки проверки.</param>
    /// <param name="z">Координата Z точки проверки.</param>
    /// <returns>Результат оценки точки.</returns>
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
    /// Оценивает точку на основе уже заполненного <see cref="_hitInfo"/>.
    /// Вызывается после успешного Raycast.
    /// <para>
    /// Логика:
    /// <list type="bullet">
    ///   <item>Земля ≥ waterLevel → <see cref="SpawnSearchResult.AboveWater"/>.</item>
    ///   <item>Дно &lt; minSeaFloorHeight → <see cref="SpawnSearchResult.DeepWater"/>.</item>
    ///   <item>Иначе → <see cref="SpawnSearchResult.ValidShallowWater"/>,
    ///         <see cref="_spawnPosition"/> заполняется.</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="x">Координата X (для записи в _spawnPosition).</param>
    /// <param name="z">Координата Z (для записи в _spawnPosition).</param>
    /// <returns>Результат оценки.</returns>
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
    /// <para>
    /// При успехе — заполняет <see cref="_spawnPosition"/>.
    /// </para>
    /// </summary>
    /// <param name="landX">X-координата найденной суши.</param>
    /// <param name="landZ">Z-координата найденной суши.</param>
    /// <returns><c>true</c> если мелководье найдено.</returns>
    private bool TryFindNearshorePoint(float landX, float landZ)
    {
        const float step = 10f;
        const int maxSteps = 20; // до 200 метров от суши

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
                    return true; // _spawnPosition уже заполнен в EvaluatePointFromHit
                }
            }
        }

        return false;
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — RIGIDBODY TELEPORT
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Подготавливает Rigidbody к безопасному телепорту:
    /// <list type="bullet">
    ///   <item>Включает isKinematic (физика не двигает тело).</item>
    ///   <item>Отключает интерполяцию (предотвращает визуальный «рывок»
    ///         между старой и новой позицией).</item>
    ///   <item>Обнуляет все скорости.</item>
    /// </list>
    /// Вызывается один раз в начале <see cref="SpawnPlayerAsync"/>,
    /// до начала поиска точки.
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
    /// <para>
    /// Безопасный порядок операций для Rigidbody:
    /// <list type="number">
    ///   <item>Гарантировать isKinematic = true (физика не вмешивается).</item>
    ///   <item>Отключить интерполяцию (нет визуального «скольжения»).</item>
    ///   <item>Установить позицию через <c>rb.position</c>
    ///         (минует Transform, мгновенный эффект для физики).</item>
    ///   <item>Продублировать через <c>transform.position</c>
    ///         (гарантия для рендера в том же кадре).</item>
    ///   <item>Обнулить velocity и angularVelocity.</item>
    ///   <item>Восстановить интерполяцию.</item>
    ///   <item>Снять isKinematic (вернуть в динамический режим).</item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="position">Целевая мировая позиция.</param>
    private void TeleportPlayer(Vector3 position)
    {
        // Гарантируем безопасный режим
        playerRigidbody.isKinematic = true;
        playerRigidbody.interpolation = RigidbodyInterpolation.None;

        // Устанавливаем позицию через Rigidbody API (мгновенно для физики)
        playerRigidbody.position = position;

        // Дублируем через Transform (гарантия для рендера в текущем кадре)
        playerRigidbody.transform.position = position;

        // Обнуляем все скорости — игрок не улетит в космос после спавна
        playerRigidbody.linearVelocity = Vector3.zero;
        playerRigidbody.angularVelocity = Vector3.zero;

        // Восстанавливаем нормальный режим работы
        playerRigidbody.interpolation = RigidbodyInterpolation.Interpolate;
        playerRigidbody.isKinematic = false;

        Debug.Log(
            $"[HectonPlayerSpawner] ✅ Игрок успешно заспавнен!\n" +
            $"   Координаты: ({position.x:F1}, {position.y:F1}, {position.z:F1})\n" +
            $"   Уровень моря: {waterLevel:F1}\n" +
            $"   Высота дна под игроком: {_hitInfo.point.y:F1}\n" +
            $"   Глубина воды: {waterLevel - _hitInfo.point.y:F1}м");
    }

    // ══════════════════════════════════════════════════════════════
    //  PRIVATE — FALLBACK
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Аварийный спавн на уровне воды в центре карты.
    /// Вызывается когда все фазы поиска исчерпаны
    /// или истёк таймаут ожидания террейна.
    /// </summary>
    private void ForceFallbackSpawn()
    {
        _spawnPosition.Set(
            searchOrigin.x,
            waterLevel + spawnHeightOffset,
            searchOrigin.y);

        TeleportPlayer(_spawnPosition);

        Debug.LogWarning(
            $"[HectonPlayerSpawner] ⚠️ Аварийный спавн на " +
            $"({_spawnPosition.x:F1}, {_spawnPosition.y:F1}, {_spawnPosition.z:F1})");
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

        // Плоскость уровня моря (визуальный квадрат)
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
#endif
}