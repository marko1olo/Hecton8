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
using Hecton8.Bootstrap;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Hecton8.Core
{
    [DisallowMultipleComponent]
    public sealed class ProximityColliderSystem : MonoBehaviour, ITickable, IUpdatable, ILateFrameTickable
    {
        internal static ProximityColliderSystem ActiveRuntimeInstance { get; private set; }
#if UNITY_EDITOR
        private static bool _assemblyReloadHookRegistered;
#endif
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
        private bool      _registeredToDispatcher;
        private bool      _registeredLateFrame;
        private int       _jobPendingFrameCount;

        // ── Cached squared radii (avoid sqrt in Job) ──
        private float _activateRadiusSq;
        private float _deactivateRadiusSq;
        private float _nextPlayerResolveWarningTime;

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
        [BurstCompile(FloatMode = FloatMode.Fast, FloatPrecision = FloatPrecision.Standard)]
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

                // Branchless hysteresis keeps Burst on a compare/select path instead of a divergent branch.
                bool wasActive = prevStatus[index] != 0;
                float radiusSq = math.select(activateRadiusSq, deactivateRadiusSq, wasActive);
                results[index] = (byte)math.select(0, 1, distSq <= radiusSq);
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
            if (worldPositions == null)
            {
                Debug.LogError("[ProximityColliderSystem] Initialize: worldPositions is null!");
                return;
            }

            if (count <= 0)
            {
                ClearRuntimeData();
                return;
            }

            if (worldPositions.Length < count)
            {
                Debug.LogError("[ProximityColliderSystem] Initialize: count exceeds buffer length!");
                return;
            }

            PrepareForReinitialize();

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
            if (!worldPositions.IsCreated)
            {
                Debug.LogError("[ProximityColliderSystem] Initialize: invalid NativeArray!");
                return;
            }

            if (worldPositions.Length == 0)
            {
                ClearRuntimeData();
                return;
            }

            PrepareForReinitialize();

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

        /// <summary>
        /// Полностью очищает runtime-состояние системы.
        /// </summary>
        /// <remarks>
        /// Безопасно завершает активную Job, возвращает все выданные collider proxy
        /// обратно в пул и освобождает внутренние буферы. Используется, когда
        /// в мире больше не осталось точек для ближней физики или требуется
        /// переинициализировать систему новым набором позиций.
        /// </remarks>
        public void ClearRuntimeData()
        {
            PrepareForReinitialize();
        }

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
            ActiveRuntimeInstance = this;
#if UNITY_EDITOR
            EnsureAssemblyReloadHook();
#endif
            // ── Авто-resolve игрока через bootstrap, если ссылка не задана ──
            TryResolvePlayerTransform();

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
                Debug.LogWarning("[ProximityColliderSystem] playerTransform is not ready during OnEnable. Runtime retry will continue.");
            }

            // ── Валидация радиусов ──
            if (deactivateRadius <= activateRadius)
            {
                Debug.LogWarning("[ProximityColliderSystem] deactivateRadius should be > " +
                                 "activateRadius for proper hysteresis. Auto-correcting.");
                deactivateRadius = activateRadius + 5f;
            }

            if (Application.isPlaying && GlobalRegistry.Dispatcher != null)
            {
                if (!_registeredToDispatcher)
                {
                    GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                    _registeredToDispatcher = true;
                }

                if (!_registeredLateFrame)
                {
                    GlobalRegistry.RegisterLateFrameTickable(this, PriorityLayer.Environment);
                    _registeredLateFrame = true;
                }
            }
        }

        private void OnDisable()
        {
            // ── Завершаем текущую Job, если она в полёте ──
            if (_registeredToDispatcher)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToDispatcher = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }
#if UNITY_EDITOR
            ReleaseAssemblyReloadHook();
#endif
        }

        private void OnDestroy()
        {
            // ── Завершаем Job и возвращаем все коллайдеры в пул ──
            JobHandle teardownDependency = CancelScheduledJobForTeardown();
            DespawnAllColliders();
            Cleanup(teardownDependency);
            if (ReferenceEquals(ActiveRuntimeInstance, this))
                ActiveRuntimeInstance = null;
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
#if UNITY_EDITOR
        private static void EnsureAssemblyReloadHook()
        {
            if (_assemblyReloadHookRegistered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload += HandleBeforeAssemblyReload;
            EditorApplication.quitting += HandleEditorQuitting;
            _assemblyReloadHookRegistered = true;
        }

        private static void ReleaseAssemblyReloadHook()
        {
            if (!_assemblyReloadHookRegistered)
                return;

            AssemblyReloadEvents.beforeAssemblyReload -= HandleBeforeAssemblyReload;
            EditorApplication.quitting -= HandleEditorQuitting;
            _assemblyReloadHookRegistered = false;
        }

        private static void HandleBeforeAssemblyReload()
        {
            TeardownActiveRuntimeInstanceForEditorReload();
        }

        private static void HandleEditorQuitting()
        {
            TeardownActiveRuntimeInstanceForEditorReload();
        }

        private static void TeardownActiveRuntimeInstanceForEditorReload()
        {
            if (ActiveRuntimeInstance == null)
                return;

            ActiveRuntimeInstance.PrepareForReinitialize();
            ActiveRuntimeInstance = null;
        }
#endif

        public void Tick(float deltaTime)
        {
            if (!_initialized) return;
            if (playerTransform == null)
            {
                TryResolvePlayerTransform();
                if (playerTransform == null)
                {
                    if (Time.unscaledTime >= _nextPlayerResolveWarningTime)
                    {
                        _nextPlayerResolveWarningTime = Time.unscaledTime + 5f;
                        Debug.LogWarning("[ProximityColliderSystem] playerTransform still unresolved after runtime retry.");
                    }

                    return;
                }
            }

            // ═══════════════════════════════════════════════════
            //  STEP 1: Обработка результатов предыдущей Job
            // ═══════════════════════════════════════════════════

            if (_jobScheduled)
            {
                _jobPendingFrameCount++;
                return;
            }

            // ═══════════════════════════════════════════════════
            //  STEP 2: Планируем новую Job на следующий кадр
            // ═══════════════════════════════════════════════════

            ScheduleDistanceJob();
        }

        public void LateFrameTick()
        {
            if (!_initialized || !_jobScheduled)
                return;

            if (!_jobHandle.IsCompleted)
                return;

            _jobHandle.Complete();
            _jobScheduled = false;
            _jobPendingFrameCount = 0;

            ProcessJobResults();
        }

        // ══════════════════════════════════════════════════════════
        //  PRIVATE — JOB SCHEDULING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Копирует prevStatus в NativeArray и планирует Burst Job.
        /// Persistent buffer avoids TempJob lifetime warnings when a distance job spans multiple frames.
        /// </summary>
        private NativeArray<byte> _prevStatusNative;

        private void ScheduleDistanceJob()
        {
            if (!_prevStatusNative.IsCreated || _prevStatusNative.Length != _pointCount)
            {
                if (_prevStatusNative.IsCreated)
                    _prevStatusNative.Dispose();

                // COLD ALLOC: NativeArray<byte>[pointCount] - persistent previous proximity state mirror for async distance jobs - owner: ProximityColliderSystem
                _prevStatusNative = new NativeArray<byte>(
                    _pointCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            }

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
            _jobPendingFrameCount = 0;
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
        /// Detaches the active job handle for teardown without blocking the main thread.
        /// </summary>
        private JobHandle CancelScheduledJobForTeardown()
        {
            if (!_jobScheduled)
                return default;

            JobHandle dependency = _jobHandle;
            _jobHandle = default;
            _jobScheduled = false;
            _jobPendingFrameCount = 0;
            return dependency;
        }

        /// <summary>
        /// Готовит систему к безопасной переинициализации.
        /// </summary>
        /// <remarks>
        /// Важно вызывать этот путь перед освобождением массивов. Иначе можно
        /// dispose-нуть данные, пока Job еще работает, или оставить активные
        /// collider proxy висеть после смены world-данных.
        /// </remarks>
        private void PrepareForReinitialize()
        {
            JobHandle teardownDependency = CancelScheduledJobForTeardown();
            if (_registeredToDispatcher)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _registeredToDispatcher = false;
            }

            if (_registeredLateFrame)
            {
                GlobalRegistry.UnregisterLateFrameTickable(this, PriorityLayer.Environment);
                _registeredLateFrame = false;
            }

            DespawnAllColliders();
            Cleanup(teardownDependency);
#if UNITY_EDITOR
            _debugTotalPoints = 0;
            _debugActiveColliders = 0;
            _debugJobFrameDelay = 0;
#endif
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
        /// Releases NativeArrays with deferred disposal and clears managed ownership.
        /// </summary>
        private void Cleanup(JobHandle dependency)
        {
            JobHandle disposeDependency = dependency;

            if (_positions.IsCreated)
            {
                disposeDependency = _positions.Dispose(disposeDependency);
                _positions = default;
            }

            if (_jobResults.IsCreated)
            {
                disposeDependency = _jobResults.Dispose(disposeDependency);
                _jobResults = default;
            }

            if (_prevStatusNative.IsCreated)
            {
                disposeDependency = _prevStatusNative.Dispose(disposeDependency);
                _prevStatusNative = default;
            }

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
            if (_jobScheduled) return;

            // ── Безопасно: NativeArray write между Jobs ──
            // Job completion is owned by LateFrameTick; writes are skipped while a job reads this buffer.
            _positions[index] = new float3(newPosition.x, newPosition.y, newPosition.z);
        }

        /// <summary>
        /// Меняет Transform игрока в рантайме (например, смена контроллера).
        /// </summary>
        public void SetPlayerTransform(Transform newPlayer)
        {
            playerTransform = newPlayer;
        }

        private void TryResolvePlayerTransform()
        {
            if (playerTransform != null)
                return;

            SceneBootstrap.TryGetCurrentPlayerTransform(out playerTransform);
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
