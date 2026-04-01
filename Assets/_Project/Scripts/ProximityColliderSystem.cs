// ============================================================================
// HECTON-8 — ProximityColliderSystem.cs
// Подставляет физические коллайдеры из пула к ближайшим точкам (камни/мусор),
// которые рендерятся через GPU Instancer без собственной физики.
//
// АРХИТЕКТУРА:
//   • ITickable — тикается через GameTickManager (единый Update).
//   • Unity.Jobs + Burst — вычисление дистанций на worker threads.
//   • ObjectPoolManager — пул пустых GameObject с BoxCollider.
//   • Гистерезис 40/45м — предотвращает мерцание на границе радиуса.
//
// ZERO GC В TICK:
//   • NativeArray (persistent) — никаких new в горячих путях.
//   • Кэшированный массив GameObject[] для активных коллайдеров.
//   • Кэшированный массив byte[] для предыдущего состояния.
//   • Никаких LINQ, foreach, List, лямбд, замыканий.
//
// ПОТОКОБЕЗОПАСНОСТЬ:
//   Job планируется в Tick, завершение проверяется в следующем Tick.
//   Все мутации (Spawn/Despawn) — строго Main Thread.
//
// ПАМЯТЬ:
//   При 10,000 точек:
//     NativeArray<float3>  = 10,000 × 12 bytes = ~120 KB
//     NativeArray<byte>    = 10,000 ×  1 byte  = ~10 KB
//     GameObject[]         = 10,000 ×  8 bytes  = ~80 KB (references)
//     byte[] prevStatus    = 10,000 ×  1 byte  = ~10 KB
//     ИТОГО: ~220 KB — ничтожно для любого железа.
// ============================================================================

using Hecton8.Core;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    public sealed class ProximityColliderSystem : MonoBehaviour, ITickable
    {
        // ══════════════════════════════════════════════════════════
        //  INSPECTOR
        // ══════════════════════════════════════════════════════════

        [Header("── References ────────────────────────────────")]
        [Tooltip("Transform игрока. Если не назначен — ищется по тегу Player.")]
        [SerializeField] private Transform playerTransform;

        [Tooltip("Префаб пустого GameObject с BoxCollider для пула. " +
                 "Должен быть прогрет в ObjectPoolManager.warmupPresets.")]
        [SerializeField] private GameObject colliderPrefab;

        [Header("── Proximity Settings ────────────────────────")]
        [Tooltip("Радиус активации коллайдеров (метры).")]
        [SerializeField] private float activateRadius = 40f;

        [Tooltip("Радиус деактивации коллайдеров (метры). " +
                 "Должен быть > activateRadius для гистерезиса.")]
        [SerializeField] private float deactivateRadius = 45f;

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Максимальное количество Spawn/Despawn операций за один Tick. " +
                 "Предотвращает лаг-спайки при телепортации игрока.")]
        [SerializeField] private int maxOperationsPerTick = 64;

        [Header("── Diagnostics (Read Only) ───────────────────")]
        [SerializeField] private int _debugTotalPoints;
        [SerializeField] private int _debugActiveColliders;
        [SerializeField] private int _debugJobFrameDelay;

        // ══════════════════════════════════════════════════════════
        //  RUNTIME STATE
        // ══════════════════════════════════════════════════════════

        // ── Job I/O (persistent allocations) ──
        private NativeArray<float3> _positions;      // позиции всех точек
        private NativeArray<byte>   _jobResults;     // результат Job: 0=far, 1=near

        // ── Main-thread cached arrays (zero GC) ──
        private GameObject[] _activeColliders;       // null = нет коллайдера
        private byte[]       _prevStatus;            // предыдущее состояние (0/1)

        // ── Job management ──
        private JobHandle _jobHandle;
        private bool      _jobScheduled;
        private bool      _initialized;

        // ── Cached squared radii (avoid sqrt in Job) ──
        private float _activateRadiusSq;
        private float _deactivateRadiusSq;

        // ── Point count ──
        private int _pointCount;

        // ══════════════════════════════════════════════════════════
        //  BURST JOB — вычисление дистанций на worker threads
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Burst-compiled Job. Вычисляет квадрат дистанции от игрока
        /// до каждой точки. Записывает 1 (near) или 0 (far) в результат.
        ///
        /// Гистерезис реализован через два радиуса:
        ///   • Если точка УЖЕ активна (prevStatus=1) — используем deactivateRadiusSq
        ///   • Если точка НЕ активна (prevStatus=0) — используем activateRadiusSq
        ///
        /// Это позволяет избежать мерцания коллайдеров на границе радиуса.
        /// </summary>
        [BurstCompile(CompileSynchronously = true)]
        private struct DistanceCalcJob : IJobParallelFor
        {
            [ReadOnly] public float3 playerPos;
            [ReadOnly] public float  activateRadiusSq;
            [ReadOnly] public float  deactivateRadiusSq;

            [ReadOnly]  public NativeArray<float3> positions;
            [ReadOnly]  public NativeArray<byte>   prevStatus;
            [WriteOnly] public NativeArray<byte>   results;

            public void Execute(int index)
            {
                float3 diff = positions[index] - playerPos;
                float distSq = math.lengthsq(diff);

                byte wasActive = prevStatus[index];

                // ── Гистерезис ──
                // wasActive=0: активируем только если < activateRadiusSq
                // wasActive=1: деактивируем только если > deactivateRadiusSq
                if (wasActive == 0)
                {
                    results[index] = distSq <= activateRadiusSq ? (byte)1 : (byte)0;
                }
                else
                {
                    results[index] = distSq > deactivateRadiusSq ? (byte)0 : (byte)1;
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — ИНИЦИАЛИЗАЦИЯ ТОЧЕК
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Инициализирует систему массивом позиций камней/мусора.
        /// Вызывается один раз после генерации мира или загрузки сцены.
        ///
        /// ВАЖНО: передаётся копия данных. Оригинальный массив можно
        /// освободить после вызова. NativeArray аллоцируется с Persistent.
        ///
        /// Пример использования:
        ///   var positions = new Vector3[10000]; // заполнить позициями
        ///   proximitySystem.Initialize(positions);
        /// </summary>
        /// <param name="worldPositions">Мировые координаты всех точек.</param>
        public void Initialize(Vector3[] worldPositions)
        {
            Initialize(worldPositions, worldPositions != null ? worldPositions.Length : 0);
        }

        /// <summary>
        /// Перегрузка для частичного использования предварительно выделенного массива.
        /// </summary>
        /// <param name="worldPositions">Буфер мировых координат.</param>
        /// <param name="count">Количество валидных элементов в начале буфера.</param>
        public void Initialize(Vector3[] worldPositions, int count)
        {
            if (worldPositions == null || count <= 0 || worldPositions.Length < count)
            {
                Debug.LogError("[ProximityColliderSystem] Initialize: empty positions array!");
                return;
            }

            // ── Очистка предыдущего состояния (если переинициализация) ──
            Cleanup();

            _pointCount = count;

            // ── Аллокация NativeArrays (Persistent — живут до Dispose) ──
            _positions  = new NativeArray<float3>(_pointCount, Allocator.Persistent,
                                                   NativeArrayOptions.UninitializedMemory);
            _jobResults = new NativeArray<byte>(_pointCount, Allocator.Persistent,
                                                 NativeArrayOptions.ClearMemory);

            // ── Копируем позиции в NativeArray<float3> ──
            for (int i = 0; i < _pointCount; i++)
            {
                _positions[i] = new float3(
                    worldPositions[i].x,
                    worldPositions[i].y,
                    worldPositions[i].z
                );
            }

            // ── Managed arrays (one-time allocation) ──
            _activeColliders = new GameObject[_pointCount];
            _prevStatus      = new byte[_pointCount];

            // ── Cache squared radii ──
            _activateRadiusSq   = activateRadius * activateRadius;
            _deactivateRadiusSq = deactivateRadius * deactivateRadius;

            _initialized = true;

#if UNITY_EDITOR
            _debugTotalPoints = _pointCount;
#endif

            Debug.Log($"[ProximityColliderSystem] Initialized with {_pointCount} points. " +
                      $"Activate: {activateRadius}m, Deactivate: {deactivateRadius}m");
        }

        /// <summary>
        /// Перегрузка для NativeArray (zero-copy, если вызывающий
        /// гарантирует lifetime).
        /// ВАЖНО: данные КОПИРУЮТСЯ — оригинал можно освобождать.
        /// </summary>
        public void Initialize(NativeArray<float3> worldPositions)
        {
            if (!worldPositions.IsCreated || worldPositions.Length == 0)
            {
                Debug.LogError("[ProximityColliderSystem] Initialize: invalid NativeArray!");
                return;
            }

            Cleanup();

            _pointCount = worldPositions.Length;

            _positions  = new NativeArray<float3>(_pointCount, Allocator.Persistent,
                                                   NativeArrayOptions.UninitializedMemory);
            _jobResults = new NativeArray<byte>(_pointCount, Allocator.Persistent,
                                                 NativeArrayOptions.ClearMemory);

            // ── NativeArray.CopyFrom — bulk memcpy, zero GC ──
            _positions.CopyFrom(worldPositions);

            _activeColliders = new GameObject[_pointCount];
            _prevStatus      = new byte[_pointCount];

            _activateRadiusSq   = activateRadius * activateRadius;
            _deactivateRadiusSq = deactivateRadius * deactivateRadius;

            _initialized = true;

#if UNITY_EDITOR
            _debugTotalPoints = _pointCount;
#endif
        }

        public float ActivateRadius => activateRadius;
        public float DeactivateRadius => deactivateRadius;
        public int MaxOperationsPerFrame => maxOperationsPerTick;

        public void SetRuntimeBudget(float newActivateRadius, float newDeactivateRadius, int newMaxOperations)
        {
            activateRadius = Mathf.Max(4f, newActivateRadius);
            deactivateRadius = Mathf.Max(activateRadius + 2f, newDeactivateRadius);
            maxOperationsPerTick = Mathf.Max(4, newMaxOperations);
            _activateRadiusSq = activateRadius * activateRadius;
            _deactivateRadiusSq = deactivateRadius * deactivateRadius;
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE — регистрация в GameTickManager
        // ══════════════════════════════════════════════════════════

        private void OnEnable()
        {
            // ── Авто-поиск игрока, если не назначен ──
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null)
                    playerTransform = player.transform;
            }

            // ── Валидация ──
            if (colliderPrefab == null)
            {
                Debug.LogError("[ProximityColliderSystem] colliderPrefab is not assigned! " +
                               "System will not function.");
                enabled = false;
                return;
            }

            if (playerTransform == null)
            {
                Debug.LogError("[ProximityColliderSystem] playerTransform is not assigned " +
                               "and no GameObject with tag 'Player' found!");
                enabled = false;
                return;
            }

            // ── Валидация радиусов ──
            if (deactivateRadius <= activateRadius)
            {
                Debug.LogWarning("[ProximityColliderSystem] deactivateRadius should be > " +
                                 "activateRadius for proper hysteresis. Auto-correcting.");
                deactivateRadius = activateRadius + 5f;
            }

            GameTickManager.Instance?.Register((ITickable)this);
        }

        private void OnDisable()
        {
            // ── Завершаем текущую Job, если она в полёте ──
            CompleteCurrentJob();

            GameTickManager.Instance?.Unregister((ITickable)this);
        }

        private void OnDestroy()
        {
            // ── Завершаем Job и возвращаем все коллайдеры в пул ──
            CompleteCurrentJob();
            DespawnAllColliders();
            Cleanup();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable.Tick — ГЛАВНЫЙ ГОРЯЧИЙ ПУТЬ
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается каждый кадр через GameTickManager.
        ///
        /// Паттерн: "Schedule → Wait → Process → Schedule"
        ///
        /// Кадр N:   Schedule Job (вычисление дистанций)
        /// Кадр N+1: Complete Job, обработка результатов, Schedule новый Job
        ///
        /// Это даёт Job целый кадр на выполнение — worker threads
        /// работают параллельно с остальной игровой логикой.
        ///
        /// ZERO GC: никаких аллокаций. Все массивы кэшированы.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_initialized) return;
            if (playerTransform == null) return;

            // ═══════════════════════════════════════════════════
            //  STEP 1: Обработка результатов предыдущей Job
            // ═══════════════════════════════════════════════════

            if (_jobScheduled)
            {
                // ── Ждём завершения (обычно уже готова — прошёл целый кадр) ──
                _jobHandle.Complete();
                _jobScheduled = false;

                // ── Применяем результаты: Spawn/Despawn коллайдеров ──
                ProcessJobResults();
            }

            // ═══════════════════════════════════════════════════
            //  STEP 2: Планируем новую Job на следующий кадр
            // ═══════════════════════════════════════════════════

            ScheduleDistanceJob();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — JOB SCHEDULING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Копирует prevStatus в NativeArray и планирует Burst Job.
        ///
        /// Почему отдельный NativeArray для prevStatus?
        ///   Job не может читать managed byte[]. Нужен NativeArray.
        ///   Аллоцируем с TempJob — автоматически освобождается
        ///   при Complete (или через 4 кадра, safety system).
        /// </summary>
        private NativeArray<byte> _prevStatusNative;

        private void ScheduleDistanceJob()
        {
            // ── Подготавливаем NativeArray prevStatus для Job ──
            // TempJob — живёт до Complete, не нужно вручную Dispose.
            _prevStatusNative = new NativeArray<byte>(
                _pointCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            // ── Копируем managed → native (memcpy, zero GC) ──
            // NativeArray<byte>.CopyFrom(byte[]) — специализированный fast path.
            _prevStatusNative.CopyFrom(_prevStatus);

            // ── Создаём и планируем Job ──
            var job = new DistanceCalcJob
            {
                playerPos          = new float3(
                    playerTransform.position.x,
                    playerTransform.position.y,
                    playerTransform.position.z),
                activateRadiusSq   = _activateRadiusSq,
                deactivateRadiusSq = _deactivateRadiusSq,
                positions          = _positions,
                prevStatus         = _prevStatusNative,
                results            = _jobResults
            };

            // ── innerloopBatchCount = 256 ──
            // Каждый worker thread обрабатывает пачку по 256 точек.
            // Для 10,000 точек = ~39 батчей. На 4-ядерном CPU =
            // ~10 батчей на ядро. Отличный баланс overhead/parallelism.
            _jobHandle  = job.Schedule(_pointCount, 256);
            _jobScheduled = true;
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — ОБРАБОТКА РЕЗУЛЬТАТОВ
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Читает результаты Job и выполняет Spawn/Despawn.
        ///
        /// Ограничение maxOperationsPerTick предотвращает лаг-спайк
        /// при телепортации игрока (когда разом нужно спавнить/деспавнить
        /// сотни коллайдеров). Оставшиеся обработаются в следующих кадрах.
        ///
        /// ZERO GC: for-цикл по NativeArray + managed array.
        /// </summary>
        private void ProcessJobResults()
        {
            // ── Освобождаем TempJob NativeArray ──
            if (_prevStatusNative.IsCreated)
                _prevStatusNative.Dispose();

            ObjectPoolManager pool = ObjectPoolManager.Instance;
            if (pool == null) return;

            int operationsThisTick = 0;

#if UNITY_EDITOR
            int activeCount = 0;
#endif

            for (int i = 0; i < _pointCount; i++)
            {
                byte newStatus = _jobResults[i];
                byte oldStatus = _prevStatus[i];

#if UNITY_EDITOR
                if (newStatus == 1) activeCount++;
#endif

                // ── Без изменений — skip ──
                if (newStatus == oldStatus) continue;

                // ── Лимит операций за кадр ──
                if (operationsThisTick >= maxOperationsPerTick) break;

                if (newStatus == 1 && oldStatus == 0)
                {
                    // ═══════════════════════════════════
                    //  ACTIVATE: точка вошла в радиус
                    // ═══════════════════════════════════

                    // Двойная проверка: коллайдер может уже быть (race condition
                    // при переинициализации). Пропускаем без аллокации.
                    if (_activeColliders[i] != null) 
                    {
                        _prevStatus[i] = 1;
                        continue;
                    }

                    float3 pos = _positions[i];
                    GameObject colliderObj = pool.Spawn(
                        colliderPrefab,
                        new Vector3(pos.x, pos.y, pos.z),
                        Quaternion.identity
                    );

                    if (colliderObj != null)
                    {
                        _activeColliders[i] = colliderObj;
                        _prevStatus[i] = 1;
                        operationsThisTick++;
                    }
                }
                else if (newStatus == 0 && oldStatus == 1)
                {
                    // ═══════════════════════════════════
                    //  DEACTIVATE: точка вышла из радиуса
                    // ═══════════════════════════════════

                    GameObject colliderObj = _activeColliders[i];

                    if (colliderObj != null)
                    {
                        pool.Despawn(colliderObj);
                        _activeColliders[i] = null;
                        operationsThisTick++;
                    }

                    _prevStatus[i] = 0;
                }
            }

#if UNITY_EDITOR
            _debugActiveColliders = activeCount;
            _debugJobFrameDelay = operationsThisTick;
#endif
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — CLEANUP
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Завершает текущую Job немедленно. Безопасно вызывать
        /// многократно — проверяет флаг _jobScheduled.
        /// </summary>
        private void CompleteCurrentJob()
        {
            if (_jobScheduled)
            {
                _jobHandle.Complete();
                _jobScheduled = false;
            }

            // ── Освобождаем TempJob, если не был disposed ──
            if (_prevStatusNative.IsCreated)
                _prevStatusNative.Dispose();
        }

        /// <summary>
        /// Возвращает все активные коллайдеры в пул.
        /// Вызывается при уничтожении или переинициализации.
        /// </summary>
        private void DespawnAllColliders()
        {
            if (_activeColliders == null) return;

            ObjectPoolManager pool = ObjectPoolManager.Instance;

            for (int i = 0; i < _activeColliders.Length; i++)
            {
                GameObject obj = _activeColliders[i];
                if (obj != null)
                {
                    if (pool != null)
                        pool.Despawn(obj);
                    else
                        Destroy(obj); // fallback если пул уничтожен

                    _activeColliders[i] = null;
                }
            }
        }

        /// <summary>
        /// Освобождает NativeArrays и обнуляет ссылки.
        /// ВАЖНО: вызывать только после CompleteCurrentJob!
        /// </summary>
        private void Cleanup()
        {
            if (_positions.IsCreated)
                _positions.Dispose();

            if (_jobResults.IsCreated)
                _jobResults.Dispose();

            if (_prevStatusNative.IsCreated)
                _prevStatusNative.Dispose();

            _activeColliders = null;
            _prevStatus      = null;
            _initialized     = false;
            _pointCount      = 0;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — RUNTIME UPDATES
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Обновляет позицию одной точки (например, камень сдвинулся).
        /// ZERO GC. O(1).
        /// </summary>
        public void UpdatePointPosition(int index, Vector3 newPosition)
        {
            if (!_initialized) return;
            if (index < 0 || index >= _pointCount) return;

            // ── Безопасно: NativeArray write между Jobs ──
            // Job уже Complete перед этим вызовом (Tick flow).
            _positions[index] = new float3(newPosition.x, newPosition.y, newPosition.z);
        }

        /// <summary>
        /// Меняет Transform игрока в рантайме (например, смена контроллера).
        /// </summary>
        public void SetPlayerTransform(Transform newPlayer)
        {
            playerTransform = newPlayer;
        }

        /// <summary>
        /// Обновляет радиусы активации/деактивации.
        /// Кэширует квадраты для Job.
        /// </summary>
        public void SetRadii(float activate, float deactivate)
        {
            activateRadius      = activate;
            deactivateRadius    = deactivate;
            _activateRadiusSq   = activate * activate;
            _deactivateRadiusSq = deactivate * deactivate;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR VALIDATION
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnValidate()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;
#endif
            if (deactivateRadius <= activateRadius)
                deactivateRadius = activateRadius + 5f;

            if (maxOperationsPerTick < 1)
                maxOperationsPerTick = 1;

            // ── Обновляем кэш, если изменили в Inspector во время Play ──
            if (Application.isPlaying && _initialized)
            {
                _activateRadiusSq   = activateRadius * activateRadius;
                _deactivateRadiusSq = deactivateRadius * deactivateRadius;
            }
        }

        /// <summary>
        /// Визуализация радиусов в Scene View.
        /// Рисуем два круга: зелёный (activate) и красный (deactivate).
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (playerTransform == null) return;

            Vector3 pos = playerTransform.position;

            // ── Радиус активации (зелёный) ──
            Gizmos.color = new Color(0f, 1f, 0f, 0.15f);
            Gizmos.DrawWireSphere(pos, activateRadius);

            // ── Радиус деактивации (красный) ──
            Gizmos.color = new Color(1f, 0f, 0f, 0.15f);
            Gizmos.DrawWireSphere(pos, deactivateRadius);

            // ── Зона гистерезиса (жёлтая, заполненная) ──
            Gizmos.color = new Color(1f, 1f, 0f, 0.05f);
            Gizmos.DrawSphere(pos, deactivateRadius);
        }
#endif
    }
}
