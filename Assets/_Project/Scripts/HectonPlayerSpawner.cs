using System.Collections;
using UnityEngine;

/// <summary>
/// Безопасный спавнер игрока для процедурно генерируемого мира (MapMagic 2).
/// Ищет точку на поверхности воды вблизи суши, где глубина не слишком большая.
/// Игрок появляется НА воде (Y = WaterLevel) рядом с берегом.
/// </summary>
public class HectonPlayerSpawner : MonoBehaviour
{
    [Header("=== Player ===")]
    [Tooltip("Ссылка на CharacterController игрока (задаётся через Inspector)")]
    [SerializeField] private CharacterController playerController;

    [Header("=== Water Settings ===")]
    [Tooltip("Уровень моря (Water Level) — высота Y поверхности воды")]
    [SerializeField] private float waterLevel = 4900f;

    [Tooltip("Минимальная допустимая высота дна под водой (защита от слишком глубоких мест)")]
    [SerializeField] private float minSeaFloorHeight = 4800f;

    [Header("=== Raycast Settings ===")]
    [Tooltip("Высота, с которой пускается луч вниз для поиска земли")]
    [SerializeField] private float raycastOriginHeight = 10000f;

    [Tooltip("Слой(и) террейна для Raycast")]
    [SerializeField] private LayerMask terrainLayerMask = ~0; // всё по умолчанию

    [Header("=== Spawn Search Settings ===")]
    [Tooltip("Начальная точка поиска по X,Z")]
    [SerializeField] private Vector2 searchOrigin = Vector2.zero;

    [Tooltip("Шаг спирали в метрах (расстояние между витками)")]
    [SerializeField] private float spiralStep = 75f;

    [Tooltip("Максимальное количество точек спирали для проверки")]
    [SerializeField] private int maxSpiralPoints = 500;

    [Tooltip("Смещение игрока над поверхностью воды / землёй")]
    [SerializeField] private float spawnHeightOffset = 2f;

    [Tooltip("Задержка между попытками Raycast (ожидание генерации MapMagic)")]
    [SerializeField] private float retryDelay = 0.5f;

    [Tooltip("Максимальное время ожидания генерации террейна (секунды)")]
    [SerializeField] private float maxWaitTime = 60f;

    // Кешированные структуры для Zero-GC
    private Vector3 _rayOrigin;
    private Vector3 _spawnPosition;
    private RaycastHit _hitInfo;
    private WaitForSeconds _waitForRetry;

    private void Awake()
    {
        // Валидация
        if (playerController == null)
        {
            Debug.LogError("[HectonPlayerSpawner] CharacterController не назначен в Inspector! Спавн невозможен.", this);
            enabled = false;
            return;
        }

        // Кешируем WaitForSeconds один раз (Zero-GC)
        _waitForRetry = new WaitForSeconds(retryDelay);
    }

    private void Start()
    {
        StartCoroutine(SpawnPlayerCoroutine());
    }

    /// <summary>
    /// Основная корутина поиска безопасной точки спавна.
    /// Ищет точку НА поверхности воды вблизи суши.
    /// </summary>
    private IEnumerator SpawnPlayerCoroutine()
    {
        Debug.Log("[HectonPlayerSpawner] Начинаю поиск безопасной точки спавна...");

        // Отключаем CharacterController, чтобы он не блокировал перемещение
        playerController.enabled = false;

        // --- Фаза 1: Ждём, пока террейн в центре карты будет сгенерирован ---
        bool terrainReady = false;
        float waitTimer = 0f;

        _rayOrigin.Set(searchOrigin.x, raycastOriginHeight, searchOrigin.y);

        while (!terrainReady)
        {
            if (Physics.Raycast(_rayOrigin, Vector3.down, out _hitInfo, raycastOriginHeight * 2f, terrainLayerMask))
            {
                terrainReady = true;
                Debug.Log($"[HectonPlayerSpawner] Террейн обнаружен в центре карты. Высота: {_hitInfo.point.y:F1}");
            }
            else
            {
                waitTimer += retryDelay;
                if (waitTimer >= maxWaitTime)
                {
                    Debug.LogError($"[HectonPlayerSpawner] Таймаут ({maxWaitTime}с): террейн не сгенерирован. Спавню на уровне воды в центре.");
                    ForceFallbackSpawn();
                    yield break;
                }

                Debug.Log($"[HectonPlayerSpawner] Террейн ещё не готов, жду... ({waitTimer:F1}с)");
                yield return _waitForRetry;
            }
        }

        // --- Фаза 2: Поиск по спирали безопасной точки (на воде, вблизи суши, не слишком глубоко) ---

        // Сначала проверим центральную точку
        SpawnSearchResult centerResult = EvaluatePoint(searchOrigin.x, searchOrigin.y);

        if (centerResult == SpawnSearchResult.ValidShallowWater)
        {
            // Центр подходит — мелкая вода
            TeleportPlayer(_spawnPosition);
            yield break;
        }

        // Поиск по спирали (Архимедова спираль)
        // Алгоритм: angle увеличивается, radius пропорционален angle
        // Это даёт равномерное покрытие вокруг центра

        bool foundValidPoint = false;
        int spiralIndex = 0;

        // Для поиска "вблизи суши": мы ищем точку, где вода неглубокая (дно > minSeaFloorHeight)
        // и поверхность ниже waterLevel (т.е. это водная точка, а не гора)

        float angleStep = 45f; // градусов на шаг (8 направлений на первом витке)
        float currentAngle = 0f;
        float currentRadius = spiralStep;
        int pointsPerRing = 8;
        int pointInRing = 0;

        while (spiralIndex < maxSpiralPoints)
        {
            // Вычисляем X, Z по спирали
            float rad = currentAngle * Mathf.Deg2Rad;
            float testX = searchOrigin.x + Mathf.Cos(rad) * currentRadius;
            float testZ = searchOrigin.y + Mathf.Sin(rad) * currentRadius;

            // Пускаем Raycast
            _rayOrigin.Set(testX, raycastOriginHeight, testZ);

            if (Physics.Raycast(_rayOrigin, Vector3.down, out _hitInfo, raycastOriginHeight * 2f, terrainLayerMask))
            {
                SpawnSearchResult result = EvaluatePointFromHit(testX, testZ);

                if (result == SpawnSearchResult.ValidShallowWater)
                {
                    foundValidPoint = true;
                    TeleportPlayer(_spawnPosition);
                    yield break;
                }
            }
            else
            {
                // Террейн в этой точке ещё не сгенерирован — ждём
                yield return _waitForRetry;
                continue; // Повторяем ту же точку
            }

            // Переходим к следующей точке спирали
            spiralIndex++;
            pointInRing++;
            currentAngle += angleStep;

            if (pointInRing >= pointsPerRing)
            {
                // Переходим на следующее кольцо
                pointInRing = 0;
                currentRadius += spiralStep;
                // Увеличиваем количество точек пропорционально радиусу
                pointsPerRing = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * currentRadius / spiralStep));
                angleStep = 360f / pointsPerRing;
                currentAngle = 0f;
            }

            // Каждые 16 точек даём фрейм отдохнуть
            if ((spiralIndex & 15) == 0)
            {
                yield return null;
            }
        }

        // --- Фаза 3: Fallback — если мелкая вода не найдена ---
        if (!foundValidPoint)
        {
            Debug.LogWarning("[HectonPlayerSpawner] Мелкая вода вблизи суши не найдена после полного обхода спирали. Ищу любую сушу...");

            // Второй проход: ищем хотя бы сушу выше воды
            currentAngle = 0f;
            currentRadius = spiralStep;
            pointsPerRing = 8;
            pointInRing = 0;
            angleStep = 45f;
            spiralIndex = 0;

            while (spiralIndex < maxSpiralPoints)
            {
                float rad = currentAngle * Mathf.Deg2Rad;
                float testX = searchOrigin.x + Mathf.Cos(rad) * currentRadius;
                float testZ = searchOrigin.y + Mathf.Sin(rad) * currentRadius;

                _rayOrigin.Set(testX, raycastOriginHeight, testZ);

                if (Physics.Raycast(_rayOrigin, Vector3.down, out _hitInfo, raycastOriginHeight * 2f, terrainLayerMask))
                {
                    float groundY = _hitInfo.point.y;

                    // Нашли сушу — спавним рядом в воде, сдвигаясь к центру
                    if (groundY > waterLevel)
                    {
                        // Ищем ближайшую точку воды, двигаясь ОТ суши к центру
                        bool foundNearshore = TryFindNearshorePoint(testX, testZ);
                        if (foundNearshore)
                        {
                            TeleportPlayer(_spawnPosition);
                            yield break;
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
                    pointsPerRing = Mathf.Max(8, Mathf.RoundToInt(2f * Mathf.PI * currentRadius / spiralStep));
                    angleStep = 360f / pointsPerRing;
                    currentAngle = 0f;
                }

                if ((spiralIndex & 15) == 0)
                {
                    yield return null;
                }
            }

            // Абсолютный fallback
            Debug.LogWarning("[HectonPlayerSpawner] Ни суша, ни мелководье не найдены. Спавню на уровне воды в центре.");
            ForceFallbackSpawn();
        }
    }

    /// <summary>
    /// Результат проверки конкретной точки карты.
    /// </summary>
    private enum SpawnSearchResult
    {
        NoTerrain,          // Террейн не обнаружен
        DeepWater,          // Слишком глубоко (дно ниже minSeaFloorHeight)
        AboveWater,         // Суша (земля выше уровня моря)
        ValidShallowWater   // Мелкая вода вблизи суши — идеально для спавна
    }

    /// <summary>
    /// Оценивает точку, пуская Raycast. Заполняет _spawnPosition при успехе.
    /// </summary>
    private SpawnSearchResult EvaluatePoint(float x, float z)
    {
        _rayOrigin.Set(x, raycastOriginHeight, z);

        if (!Physics.Raycast(_rayOrigin, Vector3.down, out _hitInfo, raycastOriginHeight * 2f, terrainLayerMask))
        {
            return SpawnSearchResult.NoTerrain;
        }

        return EvaluatePointFromHit(x, z);
    }

    /// <summary>
    /// Оценивает точку на основе уже полученного _hitInfo.
    /// </summary>
    private SpawnSearchResult EvaluatePointFromHit(float x, float z)
    {
        float groundY = _hitInfo.point.y;

        // Суша — земля выше уровня воды
        if (groundY >= waterLevel)
        {
            return SpawnSearchResult.AboveWater;
        }

        // Вода — проверяем глубину
        if (groundY < minSeaFloorHeight)
        {
            // Слишком глубоко
            return SpawnSearchResult.DeepWater;
        }

        // Мелкая вода — идеальная точка спавна
        // Игрок появляется НА поверхности воды
        _spawnPosition.Set(x, waterLevel + spawnHeightOffset, z);
        return SpawnSearchResult.ValidShallowWater;
    }

    /// <summary>
    /// Из точки суши пытается найти ближайшую точку мелководья,
    /// двигаясь от суши в 8 направлениях с шагом 10 метров.
    /// </summary>
    private bool TryFindNearshorePoint(float landX, float landZ)
    {
        // 8 направлений
        float step = 10f;
        int maxSteps = 20; // до 200 метров от суши

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
                    return true; // _spawnPosition уже заполнен
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Телепортирует игрока в указанную позицию. Корректно обрабатывает CharacterController.
    /// </summary>
    private void TeleportPlayer(Vector3 position)
    {
        // Гарантируем что CharacterController отключен
        playerController.enabled = false;

        // Перемещаем через Transform
        playerController.transform.position = position;

        // Включаем обратно
        playerController.enabled = true;

        Debug.Log($"[HectonPlayerSpawner] ✅ Игрок успешно заспавнен!\n" +
                  $"   Координаты: ({position.x:F1}, {position.y:F1}, {position.z:F1})\n" +
                  $"   Уровень моря: {waterLevel:F1}\n" +
                  $"   Высота дна под игроком: {_hitInfo.point.y:F1}\n" +
                  $"   Глубина воды: {waterLevel - _hitInfo.point.y:F1}м");
    }

    /// <summary>
    /// Аварийный спавн на уровне воды в центре карты.
    /// </summary>
    private void ForceFallbackSpawn()
    {
        _spawnPosition.Set(searchOrigin.x, waterLevel + spawnHeightOffset, searchOrigin.y);
        TeleportPlayer(_spawnPosition);

        Debug.LogWarning($"[HectonPlayerSpawner] ⚠️ Аварийный спавн на ({_spawnPosition.x:F1}, {_spawnPosition.y:F1}, {_spawnPosition.z:F1})");
    }

#if UNITY_EDITOR
    /// <summary>
    /// Визуализация в редакторе: показывает точку поиска и уровень моря.
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
        Vector3 minDepthOrigin = new Vector3(searchOrigin.x, minSeaFloorHeight, searchOrigin.y);
        Gizmos.DrawCube(minDepthOrigin, new Vector3(500f, 0.1f, 500f));

        // Луч Raycast
        Gizmos.color = Color.yellow;
        Vector3 rayStart = new Vector3(searchOrigin.x, raycastOriginHeight, searchOrigin.y);
        Gizmos.DrawLine(rayStart, origin);
    }
#endif
}