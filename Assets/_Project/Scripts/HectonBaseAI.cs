// ============================================================================
// HECTON-8 — HectonBaseAI.cs
// Базовый контроллер подводного существа.
//
// ОТВЕТСТВЕННОСТИ:
//   1. Плавное перемещение в 3D с инерцией (Rigidbody + steering).
//   2. Obstacle Avoidance: веер из 7 рейкастов (троттлинг 0.15с).
//   3. FSM: Idle/Wander → Escape → Aggressive (полная реализация атаки).
//   4. Sleep-оптимизация: ранний выход при distance > sleepDistance.
//   5. Совместимость с BuoyancyObject (плавучесть, течения).
//   6. IPoolable: корректный сброс состояния при спавне/деспавне из пула.
//   7. Health System: существо имеет HP, может получать урон и деспавниться.
//
// АРХИТЕКТУРА:
//   • ITickable — регистрация в GameTickManager. Нет Update().
//   • IFixedTickable — физическое перемещение (AddForce + MoveRotation).
//   • IPoolable — интеграция с ObjectPoolManager.
//   • Zero GC в Tick: кэшированные массивы, struct math, no LINQ.
//   • Настройки в Inspector (Data-Driven).
//
// ОПТИМИЗАЦИИ (v2.2):
//   • Obstacle Avoidance троттлинг: рейкасты пускаются каждые 0.15с
//     вместо каждого кадра. Случайный начальный сдвиг таймера
//     предотвращает синхронный рейкаст-спам при массовом спавне.
//     Снижение CPU нагрузки на Raycast ~85%.
//   • Мягкое ограничение скорости: сила применяется ТОЛЬКО если
//     текущая скорость вперёд < лимита. Velocity НЕ перезаписывается.
//     Это позволяет HectonFluidEngine (течения) толкать рыбу быстрее
//     собственного лимита, но сама рыба не превысит его.
//   • RaycastNonAlloc вместо Physics.Raycast — строго Zero-GC policy.
//   • Динамическая длина лучей: avoidanceRange + velocity × lookAheadFactor.
//     Быстрая рыба (Escape) смотрит дальше и успевает среагировать.
//   • Алгоритм "лучший свободный луч" вместо суммы нормалей.
//     Корректно работает в узких проходах (пещеры, каньоны между скалами).
//   • Поворот через Rigidbody.MoveRotation (физически корректный).
//     Коллайдер всегда синхронизирован с визуалом.
//
// СОВМЕСТИМОСТЬ:
//   На том же GameObject должен быть:
//     • Rigidbody (useGravity=false, isKinematic=false)
//     • Collider (для взаимодействия с миром)
//     • BuoyancyObject (плавучесть + течения от HectonFluidEngine)
//
// РАСШИРЕНИЕ:
//   Наследники могут переопределить:
//     • OnStateEnter/OnStateExit — для VFX, звуков, анимаций.
//     • EvaluateStateTransitions — для кастомных условий перехода.
//     • GetSteeringDirection — для кастомного поведения движения.
// ============================================================================

using Hecton8.Core;
using Hecton8.Gameplay;
using UnityEngine;

namespace Hecton8.AI
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public class HectonBaseAI : MonoBehaviour, ITickable, IFixedTickable, IPoolable
    {
        // ══════════════════════════════════════════════════════════
        //  AI STATE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Состояния конечного автомата существа.
        /// Переходы оцениваются каждый Tick.
        /// </summary>
        public enum AIState
        {
            /// <summary>Существо неподвижно, ожидает (пауза между wander-точками).</summary>
            Idle,

            /// <summary>Существо плавно перемещается к случайной точке.</summary>
            Wander,

            /// <summary>Существо убегает от игрока (distance &lt; escapeDistance).</summary>
            Escape,

            /// <summary>Существо преследует и атакует игрока.</summary>
            Aggressive
        }

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — MOVEMENT
        // ══════════════════════════════════════════════════════════

        [Header("── Movement ──────────────────────────────────")]
        [Tooltip("Сила движения вперёд (Ньютоны). Применяется к Rigidbody.")]
        [SerializeField] private float swimForce = 15f;

        [Tooltip("Множитель силы при побеге (Escape state).")]
        [SerializeField] private float escapeForceMultiplier = 2.5f;

        [Tooltip("Множитель силы при агрессии (Aggressive state).")]
        [SerializeField] private float aggressiveForceMultiplier = 1.8f;

        [Tooltip("Максимальная скорость (м/с). Ограничивает собственную тягу, " +
                 "но НЕ внешние силы (течения, HectonFluidEngine).")]
        [SerializeField] private float maxSpeed = 6f;

        [Tooltip("Максимальная скорость при побеге (м/с).")]
        [SerializeField] private float maxEscapeSpeed = 12f;

        [Tooltip("Максимальная скорость при агрессии (м/с).")]
        [SerializeField] private float maxAggressiveSpeed = 9f;

        [Tooltip("Скорость поворота (Slerp factor per second). " +
                 "Больше = резче повороты.")]
        [SerializeField] private float turnSpeed = 3f;

        [Tooltip("Множитель скорости поворота при побеге.")]
        [SerializeField] private float escapeTurnMultiplier = 2f;

        [Tooltip("Множитель скорости поворота при агрессии.")]
        [SerializeField] private float aggressiveTurnMultiplier = 2.5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — OBSTACLE AVOIDANCE (v2.2)
        // ══════════════════════════════════════════════════════════

        [Header("── Obstacle Avoidance ────────────────────────")]
        [Tooltip("Базовая дальность рейкастов для обнаружения препятствий (метры). " +
                 "К ней прибавляется velocity × lookAheadFactor.")]
        [SerializeField] private float avoidanceRange = 8f;

        [Tooltip("Множитель скорости для удлинения лучей. " +
                 "0.5 = при скорости 10 м/с луч удлиняется на 5м.")]
        [SerializeField] private float lookAheadFactor = 0.5f;

        [Tooltip("Максимальная длина луча (метры). Предотвращает чрезмерное " +
                 "удлинение при высокой скорости.")]
        [SerializeField] private float maxRayLength = 20f;

        [Tooltip("Максимальный угол веера рейкастов от forward (градусы). " +
                 "Общий угол = spreadAngle × 2.")]
        [SerializeField] private float spreadAngle = 45f;

        [Tooltip("Слои, считающиеся препятствиями.")]
        [SerializeField] private LayerMask obstacleMask = ~0;

        [Tooltip("Вес avoidance-вектора относительно целевого направления. " +
                 "Больше = сильнее уклоняется.")]
        [SerializeField] private float avoidanceWeight = 1.5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — WANDER
        // ══════════════════════════════════════════════════════════

        [Header("── Wander Behavior ───────────────────────────")]
        [Tooltip("Радиус зоны блуждания от точки спавна (метры).")]
        [SerializeField] private float wanderRadius = 30f;

        [Tooltip("Расстояние до wander-точки, при котором она считается " +
                 "достигнутой (метры).")]
        [SerializeField] private float waypointReachDistance = 3f;

        [Tooltip("Минимальное время паузы в Idle (секунды).")]
        [SerializeField] private float idleTimeMin = 1f;

        [Tooltip("Максимальное время паузы в Idle (секунды).")]
        [SerializeField] private float idleTimeMax = 4f;

        [Tooltip("Таймаут для достижения wander-точки (секунды). " +
                 "Если не достигнута — генерируется новая.")]
        [SerializeField] private float wanderTimeout = 15f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — REACTIONS
        // ══════════════════════════════════════════════════════════

        [Header("── Player Reactions ──────────────────────────")]
        [Tooltip("Расстояние, на котором существо начинает убегать.")]
        [SerializeField] private float escapeDistance = 15f;

        [Tooltip("Расстояние, на котором существо прекращает побег " +
                 "и возвращается к блужданию.")]
        [SerializeField] private float escapeSafeDistance = 30f;

        [Tooltip("Время побега перед возвратом к Wander (секунды). " +
                 "Действует как минимальный таймер, даже если расстояние > safe.")]
        [SerializeField] private float escapeMinDuration = 3f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — AGGRESSION
        // ══════════════════════════════════════════════════════════

        [Header("── Aggression ────────────────────────────────")]
        [Tooltip("Если true — существо агрессивно (атакует вместо побега).")]
        [SerializeField] private bool isAggressive;

        [Tooltip("Дистанция обнаружения для агрессии.")]
        [SerializeField] private float aggroDistance = 20f;

        [Tooltip("Дистанция потери агрессии (leash distance).")]
        [SerializeField] private float deaggroDistance = 35f;

        [Tooltip("Урон за одну атаку (наносится integrity костюма игрока).")]
        [SerializeField] private float attackDamage = 15f;

        [Tooltip("Дистанция, на которой существо может атаковать игрока (метры).")]
        [SerializeField] private float attackRange = 3f;

        [Tooltip("Время перезарядки атаки (секунды).")]
        [SerializeField] private float attackCooldown = 2f;

        [Header("── Player Stimulus ───────────────────────────────")]
        [Tooltip("Если true — существо слышит шум игрока и раньше реагирует на него.")]
        [SerializeField] private bool reactToPlayerNoise = true;

        [Tooltip("Насколько шум игрока расширяет дистанцию обнаружения.")]
        [SerializeField] private float noiseDetectionBonus = 10f;

        [Tooltip("Насколько шум игрока расширяет дистанцию побега у мирных существ.")]
        [SerializeField] private float noiseEscapeBonus = 8f;

        [Tooltip("Если true — существо реагирует на луч фонаря игрока.")]
        [SerializeField] private bool reactToPlayerLight = true;

        [Tooltip("Насколько свет фонаря расширяет дистанцию обнаружения.")]
        [SerializeField] private float lightDetectionBonus = 12f;

        [Tooltip("Насколько свет фонаря расширяет дистанцию побега у мирных существ.")]
        [SerializeField] private float lightEscapeBonus = 10f;

        [Tooltip("Как долго существо помнит недавний шум или свет игрока.")]
        [SerializeField] private float stimulusMemoryDuration = 2.5f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — CREATURE HEALTH
        // ══════════════════════════════════════════════════════════

        [Header("── Creature Health ───────────────────────────")]
        [Tooltip("Максимальное здоровье существа.")]
        [SerializeField] private float maxHealth = 50f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — PERFORMANCE
        // ══════════════════════════════════════════════════════════

        [Header("── Performance ───────────────────────────────")]
        [Tooltip("Расстояние до игрока, после которого AI засыпает. " +
                 "Существо продолжает дрейфовать по физике, но не думает.")]
        [SerializeField] private float sleepDistance = 200f;

        // ══════════════════════════════════════════════════════════
        //  INSPECTOR — DEBUG
        // ══════════════════════════════════════════════════════════

        [Header("── Diagnostics ───────────────────────────────")]
        [SerializeField] private AIState _debugCurrentState;
        [SerializeField] private bool _debugIsSleeping;
        [SerializeField] private float _debugDistanceToPlayer;
        [SerializeField] private float _debugCurrentHealth;
        [SerializeField] private float _debugCurrentRayLength;
        [SerializeField] private int _debugBestRayIndex;
        [SerializeField] private float _debugNoiseStimulus;
        [SerializeField] private float _debugLightStimulus;
        [SerializeField] private float _debugStimulusMemory;
        [SerializeField] private float _debugAggroTriggerDistance;
        [SerializeField] private float _debugEscapeTriggerDistance;

        // ══════════════════════════════════════════════════════════
        //  CONSTANTS
        // ══════════════════════════════════════════════════════════

        /// <summary>Количество рейкастов для obstacle avoidance.</summary>
        private const int RayCount = 7;

        /// <summary>
        /// Интервал обновления obstacle avoidance (секунды).
        /// Рейкасты пускаются каждые 0.15с вместо каждого кадра.
        /// При 60 FPS: 60 / 0.15 = ~6.7 вызовов/сек вместо 60.
        /// Снижение CPU нагрузки на Raycast ~85%.
        ///
        /// Случайный начальный сдвиг _avoidanceTimer предотвращает
        /// синхронный рейкаст-шторм при массовом спавне 50+ рыб.
        /// </summary>
        private const float AVOIDANCE_UPDATE_INTERVAL = 0.15f;

        /// <summary>
        /// Размер буфера для RaycastNonAlloc.
        /// 1 = нам нужен только ближайший хит на каждый луч.
        /// </summary>
        private const int RAYCAST_BUFFER_SIZE = 1;

        // ══════════════════════════════════════════════════════════
        //  CACHED STATE — zero GC
        // ══════════════════════════════════════════════════════════

        /// <summary>Кэшированный Rigidbody.</summary>
        private Rigidbody _rb;

        /// <summary>Кэшированный Transform.</summary>
        private Transform _transform;

        /// <summary>Точка спавна (центр зоны блуждания).</summary>
        private Vector3 _spawnPoint;

        /// <summary>Текущая целевая точка wander.</summary>
        private Vector3 _wanderTarget;

        /// <summary>Текущее состояние FSM.</summary>
        private AIState _currentState;

        /// <summary>Таймер для текущего состояния (секунды, обратный отсчёт).</summary>
        private float _stateTimer;

        /// <summary>Таймер wander timeout.</summary>
        private float _wanderTimer;

        /// <summary>Кэшированная ссылка на Transform игрока.</summary>
        private Transform _playerTransform;

        /// <summary>
        /// Кэшированная ссылка на HectonSurvivalSystem игрока.
        /// Устанавливается один раз при обнаружении игрока. Zero-GC: нет TryGetComponent каждый удар.
        /// </summary>
        private HectonSurvivalSystem _playerSurvival;
        private Rigidbody _playerRigidbody;
        private PlayerFlashlight _playerFlashlight;

        /// <summary>Текущее здоровье существа.</summary>
        private float _currentHealth;

        /// <summary>Таймер кулдауна атаки (обратный отсчёт).</summary>
        private float _attackCooldownTimer;

        /// <summary>Квадрат attackRange (для sqrMagnitude сравнения).</summary>
        private float _attackRangeSqr;

        /// <summary>Квадрат sleepDistance (для sqrMagnitude сравнения).</summary>
        private float _sleepDistanceSqr;

        /// <summary>Квадрат escapeDistance.</summary>
        private float _escapeDistanceSqr;

        /// <summary>Квадрат escapeSafeDistance.</summary>
        private float _escapeSafeDistanceSqr;

        /// <summary>Квадрат aggroDistance.</summary>
        private float _aggroDistanceSqr;

        /// <summary>Квадрат deaggroDistance.</summary>
        private float _deaggroDistanceSqr;

        /// <summary>Квадрат waypointReachDistance.</summary>
        private float _waypointReachSqr;

        /// <summary>Текущее желаемое направление движения (нормализованный).</summary>
        private Vector3 _desiredDirection;

        /// <summary>Флаг: AI спит (слишком далеко от игрока).</summary>
        private bool _isSleeping;

        /// <summary>Флаг: существо мертво (HP ≤ 0, ожидает деспавна).</summary>
        private bool _isDead;
        private float _stimulusMemoryTimer;
        private float _stimulusAggroDistanceSqr;
        private float _stimulusEscapeDistanceSqr;
        private float _stimulusEscapeSafeDistanceSqr;
        private float _stimulusDeaggroDistanceSqr;
        private float _stimulusWakeDistanceSqr;

        // ── Obstacle Avoidance: pre-allocated + throttled (v2.2) ──

        /// <summary>
        /// Кэшированные локальные направления рейкастов.
        /// Вычисляются один раз в Awake. Struct array — zero GC.
        /// </summary>
        private Vector3[] _rayDirectionsLocal;

        /// <summary>
        /// Pre-allocated буфер для RaycastNonAlloc.
        /// Один элемент — нам нужен только ближайший хит.
        /// Переиспользуется каждый вызов ComputeAvoidanceVector.
        ///
        /// ВАЖНО: RaycastHit — struct. Array аллоцирован один раз в Awake.
        /// RaycastNonAlloc записывает в него без аллокаций.
        /// </summary>
        private RaycastHit[] _nonAllocBuffer;

        /// <summary>
        /// Расстояния попадания для каждого луча.
        /// -1 = луч свободен (ничего не задел).
        /// >0 = дистанция до ближайшего хита.
        /// Используется алгоритмом "лучший свободный луч" и Gizmos.
        /// </summary>
        private float[] _rayHitDistances;

        /// <summary>
        /// Таймер троттлинга obstacle avoidance.
        /// Обратный отсчёт: при ≤0 пересчитываем рейкасты.
        /// Начальное значение рандомизировано для десинхронизации
        /// между экземплярами (предотвращает raycast storm).
        /// </summary>
        private float _avoidanceTimer;

        /// <summary>
        /// Кэшированный результат последнего ComputeAvoidanceVector().
        /// Применяется каждый кадр, пересчитывается каждые AVOIDANCE_UPDATE_INTERVAL.
        /// </summary>
        private Vector3 _cachedAvoidance;

        /// <summary>
        /// Текущая динамическая длина лучей.
        /// Вычисляется в ComputeAvoidanceVector, используется в Gizmos.
        /// </summary>
        private float _currentRayLength;

        /// <summary>
        /// Целевая ротация, вычисленная в Tick.
        /// Применяется в FixedTick через Rigidbody.MoveRotation
        /// для физически корректного поворота (коллайдер синхронизирован).
        /// </summary>
        private Quaternion _targetRotation;

        /// <summary>
        /// Флаг: целевая ротация обновлена в этом кадре.
        /// Предотвращает применение устаревшей ротации в FixedTick.
        /// </summary>
        private bool _rotationDirty;

        /// <summary>
        /// Tracks whether this component successfully registered
        /// with GameTickManager. Prevents double-register (OnEnable +
        /// Start both succeeding) and orphan unregister during teardown.
        /// </summary>
        private bool _registeredToTickManager;

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════

        private void Awake()
        {
            _rb        = GetComponent<Rigidbody>();
            _transform = transform;
            _spawnPoint = _transform.position;

            // ── Pre-compute squared distances ──
            CacheSquaredDistances();

            // ── Pre-allocate raycast arrays (v2.2) ──
            _rayDirectionsLocal = new Vector3[RayCount];
            _nonAllocBuffer     = new RaycastHit[RAYCAST_BUFFER_SIZE];
            _rayHitDistances    = new float[RayCount];

            ComputeRayDirections();

            // ── Initial state ──
            ResetInternalState();

            _registeredToTickManager = false;
        }

        private void OnEnable()
        {
            // Phase 1: early registration attempt.
            // GameTickManager may not have initialized yet — that's OK.
            // Start() will retry if this fails.
            if (GameTickManager.Instance == null) goto skipRegister;

            if (!_registeredToTickManager)
            {
                GameTickManager.Instance.Register((ITickable)this);
                GameTickManager.Instance.Register((IFixedTickable)this);
                _registeredToTickManager = true;
            }

            skipRegister:

            // ── Поиск игрока (ленивый, один раз) ──
            if (_playerTransform == null)
            {
                FindPlayer();
            }
        }

        /// <summary>
        /// Phase 2: deferred registration fallback.
        /// All Awake() calls have completed by now.
        /// </summary>
        private void Start()
        {
            if (_registeredToTickManager)
                return;

            if (GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ITickable)this);
                GameTickManager.Instance.Register((IFixedTickable)this);
                _registeredToTickManager = true;
            }
            else
            {
                Debug.LogError(
                    "[HectonBaseAI] GameTickManager.Instance is null " +
                    "even at Start(). AI will NOT tick. " +
                    "Ensure GameTickManager exists in the scene.",
                    this);
            }
        }

        private void OnDisable()
        {
            // Guard: singleton may be destroyed before this component.
            if (GameTickManager.Instance == null) return;

            if (_registeredToTickManager)
            {
                GameTickManager.Instance.Unregister((ITickable)this);
                GameTickManager.Instance.Unregister((IFixedTickable)this);
                _registeredToTickManager = false;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  IPoolable — ИНТЕГРАЦИЯ С ПУЛОМ
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается при извлечении из пула (после SetActive(true) и установки позиции).
        /// Сбрасывает FSM в Idle, обновляет spawn point, обнуляет таймеры и HP.
        /// Рандомизирует _avoidanceTimer для десинхронизации рейкастов.
        /// </summary>
        public void OnSpawn()
        {
            // ── Обновляем точку спавна на текущую позицию ──
            _spawnPoint = _transform.position;

            // ── Пересчёт квадратов (на случай если параметры менялись) ──
            CacheSquaredDistances();

            // ── Сброс внутреннего состояния ──
            ResetInternalState();

            // ── Re-register with tick manager if needed ──
            // Pooled objects go through OnDisable → OnEnable cycle,
            // but OnEnable may fire before GameTickManager is ready.
            if (!_registeredToTickManager && GameTickManager.Instance != null)
            {
                GameTickManager.Instance.Register((ITickable)this);
                GameTickManager.Instance.Register((IFixedTickable)this);
                _registeredToTickManager = true;
            }

            // ── Ленивый поиск игрока (если ещё не найден) ──
            if (_playerTransform == null)
            {
                FindPlayer();
            }
        }

        /// <summary>
        /// Вызывается при возврате в пул (перед SetActive(false)).
        /// Обнуляет физическую инерцию, чтобы при следующем спавне
        /// объект не «помнил» предыдущие скорости.
        /// </summary>
        public void OnDespawn()
        {
            // ── Обнуление инерции ──
            _rb.linearVelocity  = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;

            // ── Сброс логического состояния ──
            _isDead     = false;
            _isSleeping = false;
            _currentState = AIState.Idle;
            _attackCooldownTimer = 0f;

            // ── Сброс кэша avoidance ──
            _cachedAvoidance  = Vector3.zero;
            _avoidanceTimer   = 0f;
            _currentRayLength = avoidanceRange;

            // ── Сброс ротации (v2.2) ──
            _rotationDirty = false;

            // ── Сброс кэша survival системы игрока ──
            // (при следующем спавне игрок может быть другим объектом)
            _playerSurvival = null;
            _playerRigidbody = null;
            _playerFlashlight = null;
            ResetStimulusDebug();
        }

        // ══════════════════════════════════════════════════════════
        //  ITickable — BRAIN (каждый кадр)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Основной цикл ИИ. Порядок:
        ///   1. Dead check (ранний выход если мертв).
        ///   2. Sleep check (ранний выход если далеко).
        ///   3. Attack cooldown tick.
        ///   4. State transitions (оценка условий перехода).
        ///   5. State behavior (обновление целевого направления).
        ///   6. Obstacle avoidance (троттлинг: пересчёт каждые 0.15с,
        ///      применение кэша каждый кадр).
        ///   7. Compute target rotation (применяется в FixedTick
        ///      через MoveRotation для физической корректности).
        ///
        /// ZERO GC: все вычисления на struct'ах. Нет аллокаций.
        /// </summary>
        public void Tick(float deltaTime)
        {
            // ══════════════════════════════════════════════════════
            //  0. DEAD CHECK
            // ══════════════════════════════════════════════════════

            if (_isDead) return;

            // ══════════════════════════════════════════════════════
            //  1. SLEEP CHECK
            // ══════════════════════════════════════════════════════

            if (!CheckPlayerDistance(out float distSqrToPlayer))
            {
                // Игрок не найден — спим
                _isSleeping = true;
                ResetStimulusDebug();
                UpdateDiagnostics(distSqrToPlayer);
                return;
            }

            UpdatePlayerStimulus(deltaTime);

            float wakeDistanceSqr = Mathf.Max(_sleepDistanceSqr, _stimulusWakeDistanceSqr);
            if (distSqrToPlayer > wakeDistanceSqr)
            {
                // Слишком далеко — AI засыпает
                _isSleeping = true;
                UpdateDiagnostics(distSqrToPlayer);
                return;
            }

            _isSleeping = false;

            // ══════════════════════════════════════════════════════
            //  2. ATTACK COOLDOWN TICK
            // ══════════════════════════════════════════════════════

            if (_attackCooldownTimer > 0f)
            {
                _attackCooldownTimer -= deltaTime;
            }

            // ══════════════════════════════════════════════════════
            //  3. STATE TRANSITIONS
            // ══════════════════════════════════════════════════════

            EvaluateStateTransitions(distSqrToPlayer, deltaTime);

            // ══════════════════════════════════════════════════════
            //  4. STATE BEHAVIOR — вычисление желаемого направления
            // ══════════════════════════════════════════════════════

            switch (_currentState)
            {
                case AIState.Idle:
                    TickIdle(deltaTime);
                    break;

                case AIState.Wander:
                    TickWander(deltaTime);
                    break;

                case AIState.Escape:
                    TickEscape();
                    break;

                case AIState.Aggressive:
                    TickAggressive(distSqrToPlayer);
                    break;
            }

            // ══════════════════════════════════════════════════════
            //  5. OBSTACLE AVOIDANCE (THROTTLED, v2.2)
            //
            //  Рейкасты пересчитываются каждые AVOIDANCE_UPDATE_INTERVAL.
            //  Между пересчётами используется кэшированный результат.
            //  Это снижает CPU нагрузку на Physics.RaycastNonAlloc ~85%
            //  при сохранении визуально плавного избегания препятствий.
            //
            //  v2.2: RaycastNonAlloc, динамическая длина лучей,
            //  алгоритм "лучший свободный луч".
            // ══════════════════════════════════════════════════════

            _avoidanceTimer -= deltaTime;
            if (_avoidanceTimer <= 0f)
            {
                _cachedAvoidance = ComputeAvoidanceVector();
                _avoidanceTimer  = AVOIDANCE_UPDATE_INTERVAL;
            }

            if (_cachedAvoidance.sqrMagnitude > 0.001f)
            {
                _desiredDirection = (_desiredDirection + _cachedAvoidance * avoidanceWeight).normalized;
            }

            // ══════════════════════════════════════════════════════
            //  6. COMPUTE TARGET ROTATION (v2.2)
            //
            //  Вместо прямой записи transform.rotation мы вычисляем
            //  целевую ротацию здесь и применяем через MoveRotation
            //  в FixedTick. Это физически корректно — Rigidbody знает
            //  о повороте, коллайдер синхронизирован.
            //
            //  Exponential Slerp: 1 - exp(-speed * dt) обеспечивает
            //  frame-rate-independent плавность. При 30 FPS и 120 FPS
            //  существо поворачивается с одинаковой визуальной скоростью.
            // ══════════════════════════════════════════════════════

            if (_desiredDirection.sqrMagnitude > 0.001f)
            {
                float currentTurnSpeed = _currentState switch
                {
                    AIState.Escape     => turnSpeed * escapeTurnMultiplier,
                    AIState.Aggressive => turnSpeed * aggressiveTurnMultiplier,
                    _                  => turnSpeed
                };

                Quaternion targetRotation = Quaternion.LookRotation(_desiredDirection, Vector3.up);

                _targetRotation = Quaternion.Slerp(
                    _transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-currentTurnSpeed * deltaTime));

                _rotationDirty = true;
            }

            UpdateDiagnostics(distSqrToPlayer);
        }

        // ══════════════════════════════════════════════════════════
        //  IFixedTickable — PHYSICS (фиксированный шаг)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Применяет физическую силу движения и ротацию.
        /// Вызывается в FixedUpdate через GameTickManager.
        ///
        /// Разделение Tick/FixedTick:
        ///   • Tick — мозг (направление, состояния, рейкасты).
        ///   • FixedTick — мышцы (AddForce, MoveRotation).
        ///
        /// v2.2: РОТАЦИЯ ЧЕРЕЗ MoveRotation:
        ///   Вместо прямой записи transform.rotation (которая является
        ///   "телепортацией" с точки зрения физики) используем
        ///   Rigidbody.MoveRotation. Rigidbody корректно интерполирует
        ///   поворот, коллайдер остаётся синхронизированным с визуалом.
        ///
        /// МЯГКОЕ ОГРАНИЧЕНИЕ СКОРОСТИ:
        ///   Сила применяется ТОЛЬКО если текущая скорость существа
        ///   вперёд (dot(velocity, forward)) меньше лимита.
        ///   Velocity НИКОГДА не перезаписывается напрямую.
        ///
        ///   Это обеспечивает совместимость с HectonFluidEngine:
        ///     • Течения (внешние силы) могут толкать рыбу быстрее
        ///       её собственного лимита — velocity не clamp-ится.
        ///     • Рыба не превысит свой maxSpeed собственной тягой,
        ///       но может быть ускорена средой.
        ///     • Естественное замедление после выхода из течения
        ///       обеспечивается Rigidbody.drag (настроенным через
        ///       BuoyancyObject).
        ///
        /// BuoyancyObject на том же Rigidbody добавляет
        /// плавучесть и сопротивление воды автоматически.
        /// </summary>
        public void FixedTick(float fixedDeltaTime)
        {
            // Если спим или мертвы — ничего не делаем
            if (_isSleeping || _isDead)
                return;

            // ── Применяем ротацию через MoveRotation (v2.2) ──
            // Физически корректный поворот: Rigidbody знает о нём,
            // коллайдер синхронизирован, интерполяция работает.
            if (_rotationDirty)
            {
                _rb.MoveRotation(_targetRotation);
                _rotationDirty = false;
            }

            // Если в Idle — не прикладываем тягу (дрейф по инерции)
            if (_currentState == AIState.Idle)
                return;

            // ── Вычисляем силу и лимит скорости по состоянию ──
            float force;
            float speedLimit;

            switch (_currentState)
            {
                case AIState.Escape:
                    force      = swimForce * escapeForceMultiplier;
                    speedLimit = maxEscapeSpeed;
                    break;

                case AIState.Aggressive:
                    force      = swimForce * aggressiveForceMultiplier;
                    speedLimit = maxAggressiveSpeed;
                    break;

                default:
                    force      = swimForce;
                    speedLimit = maxSpeed;
                    break;
            }

            // ── Мягкое ограничение: сила вперёд только если ниже лимита ──
            // Vector3.Dot(velocity, forward) = проекция скорости на ось forward.
            // Положительное значение = движение вперёд.
            // Если уже быстрее лимита (например, от течения) — не добавляем тягу.
            float currentForwardSpeed = Vector3.Dot(_rb.linearVelocity, _transform.forward);

            if (currentForwardSpeed < speedLimit)
            {
                _rb.AddForce(_transform.forward * force, ForceMode.Force);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE TRANSITIONS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Оценивает условия перехода между состояниями.
        /// Вызывается каждый Tick. Порядок приоритетов:
        ///   1. Aggressive (если isAggressive)
        ///   2. Escape (если не агрессивный и игрок близко)
        ///   3. Wander / Idle (по умолчанию)
        ///
        /// Виртуальный — наследники могут расширить логику.
        /// </summary>
        protected virtual void EvaluateStateTransitions(float distSqrToPlayer, float deltaTime)
        {
            float aggroDistanceSqr = Mathf.Max(_aggroDistanceSqr, _stimulusAggroDistanceSqr);
            float escapeDistanceSqr = Mathf.Max(_escapeDistanceSqr, _stimulusEscapeDistanceSqr);
            float escapeSafeDistanceSqr = Mathf.Max(_escapeSafeDistanceSqr, _stimulusEscapeSafeDistanceSqr);
            float deaggroDistanceSqr = Mathf.Max(_deaggroDistanceSqr, _stimulusDeaggroDistanceSqr);

            _debugAggroTriggerDistance = Mathf.Sqrt(aggroDistanceSqr);
            _debugEscapeTriggerDistance = Mathf.Sqrt(escapeDistanceSqr);

            switch (_currentState)
            {
                // ─────────────────────────────────────────────────
                case AIState.Idle:
                {
                    // Агрессия
                    if (isAggressive && distSqrToPlayer < aggroDistanceSqr)
                    {
                        TransitionTo(AIState.Aggressive);
                        return;
                    }

                    // Побег
                    if (!isAggressive && distSqrToPlayer < escapeDistanceSqr)
                    {
                        TransitionTo(AIState.Escape);
                        return;
                    }

                    // Таймер Idle истёк → Wander
                    _stateTimer -= deltaTime;
                    if (_stateTimer <= 0f)
                    {
                        TransitionTo(AIState.Wander);
                    }

                    break;
                }

                // ─────────────────────────────────────────────────
                case AIState.Wander:
                {
                    // Агрессия
                    if (isAggressive && distSqrToPlayer < aggroDistanceSqr)
                    {
                        TransitionTo(AIState.Aggressive);
                        return;
                    }

                    // Побег
                    if (!isAggressive && distSqrToPlayer < escapeDistanceSqr)
                    {
                        TransitionTo(AIState.Escape);
                        return;
                    }

                    // Достигли точки → Idle
                    Vector3 toTarget = _wanderTarget - _transform.position;
                    if (toTarget.sqrMagnitude < _waypointReachSqr)
                    {
                        TransitionTo(AIState.Idle);
                        return;
                    }

                    // Timeout → новая точка
                    _wanderTimer -= deltaTime;
                    if (_wanderTimer <= 0f)
                    {
                        GenerateWanderTarget();
                        _wanderTimer = wanderTimeout;
                    }

                    break;
                }

                // ─────────────────────────────────────────────────
                case AIState.Escape:
                {
                    _stateTimer -= deltaTime;

                    // Минимальное время побега не истекло — остаёмся
                    if (_stateTimer > 0f)
                        return;

                    // Убежали достаточно далеко → Wander
                    if (distSqrToPlayer > escapeSafeDistanceSqr)
                    {
                        TransitionTo(AIState.Wander);
                    }

                    break;
                }

                // ─────────────────────────────────────────────────
                case AIState.Aggressive:
                {
                    // Потеря агрессии (leash distance)
                    if (distSqrToPlayer > deaggroDistanceSqr)
                    {
                        TransitionTo(AIState.Wander);
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// Выполняет переход в новое состояние.
        /// Вызывает OnStateExit/OnStateEnter для расширения наследниками.
        /// </summary>
        private void TransitionTo(AIState newState)
        {
            AIState oldState = _currentState;

            OnStateExit(oldState);

            _currentState = newState;

            // ── Инициализация нового состояния ──
            switch (newState)
            {
                case AIState.Idle:
                    _stateTimer = Random.Range(idleTimeMin, idleTimeMax);
                    break;

                case AIState.Wander:
                    GenerateWanderTarget();
                    _wanderTimer = wanderTimeout;
                    break;

                case AIState.Escape:
                    _stateTimer = escapeMinDuration;
                    break;

                case AIState.Aggressive:
                    // Сброс кулдауна атаки при входе в агрессию,
                    // чтобы первая атака происходила не мгновенно
                    _attackCooldownTimer = 0f;

                    // Кэшируем HectonSurvivalSystem игрока если ещё не закэширован
                    CachePlayerSurvival();
                    CachePlayerStimulusSources();
                    break;
            }

            OnStateEnter(newState);
        }

        // ══════════════════════════════════════════════════════════
        //  STATE BEHAVIORS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Idle: существо «зависает». Плавно замедляется
        /// (сила не применяется в FixedTick). Лёгкое покачивание
        /// реализуется через BuoyancyObject + drag.
        /// </summary>
        private void TickIdle(float deltaTime)
        {
            // Направление не меняется — существо дрейфует по инерции
        }

        /// <summary>
        /// Wander: существо плывёт к случайной точке.
        /// _desiredDirection обновляется каждый кадр для плавности.
        /// </summary>
        private void TickWander(float deltaTime)
        {
            Vector3 toTarget = _wanderTarget - _transform.position;

            if (toTarget.sqrMagnitude > 0.01f)
            {
                _desiredDirection = toTarget.normalized;
            }
        }

        /// <summary>
        /// Escape: существо плывёт ОТ игрока.
        /// Направление = вектор от игрока к существу (нормализованный).
        /// </summary>
        private void TickEscape()
        {
            if (_playerTransform == null) return;

            Vector3 awayFromPlayer = _transform.position - _playerTransform.position;

            if (awayFromPlayer.sqrMagnitude > 0.01f)
            {
                _desiredDirection = awayFromPlayer.normalized;
            }
        }

        /// <summary>
        /// Aggressive: существо преследует игрока и наносит урон при сближении.
        ///
        /// Логика:
        ///   1. Вычисляет полный 3D-вектор к игроку (включая высоту Y).
        ///   2. Если дистанция ≤ attackRange и кулдаун прошёл — наносит урон.
        ///   3. Всегда корректирует направление для преследования в 3D.
        ///
        /// Урон наносится через кэшированный HectonSurvivalSystem.TakeDamage().
        /// Zero-GC: TryGetComponent вызывается один раз (при входе в Aggressive),
        /// результат кэшируется в _playerSurvival.
        /// </summary>
        /// <param name="distSqrToPlayer">Квадрат дистанции до игрока (уже вычислен в Tick).</param>
        private void TickAggressive(float distSqrToPlayer)
        {
            if (_playerTransform == null) return;

            // ── Полный 3D-вектор к игроку (включая вертикальную составляющую) ──
            Vector3 toPlayer = _playerTransform.position - _transform.position;

            if (toPlayer.sqrMagnitude > 0.01f)
            {
                _desiredDirection = toPlayer.normalized;
            }

            // ── Проверка атаки ──
            if (distSqrToPlayer <= _attackRangeSqr && _attackCooldownTimer <= 0f)
            {
                PerformAttack();
                _attackCooldownTimer = attackCooldown;
            }
        }

        /// <summary>
        /// Выполняет атаку: наносит урон HectonSurvivalSystem игрока.
        /// Вызывается из TickAggressive когда дистанция и кулдаун позволяют.
        /// </summary>
        private void PerformAttack()
        {
            // ── Убеждаемся что survival закэширован ──
            if (_playerSurvival == null)
            {
                CachePlayerSurvival();
            }

            // ── Наносим урон ──
            if (_playerSurvival != null)
            {
                _playerSurvival.TakeDamage(attackDamage);
            }
        }

        // ══════════════════════════════════════════════════════════
        //  STATE HOOKS — для наследников
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вызывается при входе в новое состояние.
        /// Переопредели для VFX, звуков, анимаций.
        /// </summary>
        protected virtual void OnStateEnter(AIState state) { }

        /// <summary>
        /// Вызывается при выходе из текущего состояния.
        /// Переопредели для остановки VFX, звуков.
        /// </summary>
        protected virtual void OnStateExit(AIState state) { }

        // ══════════════════════════════════════════════════════════
        //  OBSTACLE AVOIDANCE (v2.2 — "ЛУЧШИЙ СВОБОДНЫЙ ЛУЧ")
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вычисляет avoidance-вектор через алгоритм "лучший свободный луч".
        ///
        /// АЛГОРИТМ (v2.2):
        ///   1. Вычисляем динамическую длину лучей:
        ///      rayLength = clamp(avoidanceRange + speed × lookAheadFactor,
        ///                        avoidanceRange, maxRayLength)
        ///      → Быстрая рыба (Escape, 12 м/с) смотрит дальше (8+6=14м).
        ///      → Медленная рыба (Wander, 3 м/с) смотрит ближе (8+1.5=9.5м).
        ///
        ///   2. Пускаем 7 лучей через RaycastNonAlloc (строго zero GC).
        ///      Записываем дистанцию хита или -1 (свободен) в _rayHitDistances.
        ///
        ///   3. Если центральный луч (индекс 0) свободен → return Vector3.zero.
        ///      Препятствий впереди нет — уклоняться незачем.
        ///
        ///   4. Если центральный заблокирован — ищем лучший боковой:
        ///      • Приоритет 1: свободные лучи (distance = -1).
        ///        Берём первый свободный — он ближе к forward (минимальный доворот).
        ///      • Приоритет 2: лучи с максимальной дистанцией хита
        ///        (больше пространства для манёвра).
        ///
        ///   5. Результат = мировое направление лучшего луча × urgency.
        ///      Urgency = (1 - centerHitDist / rayLength). Ближе стена — сильнее доворот.
        ///
        /// ПОЧЕМУ НЕ СУММА НОРМАЛЕЙ (v2.1):
        ///   В узком проходе (пещера, каньон между скалами MapMagic)
        ///   нормали с двух стен компенсируют друг друга → вектор ≈ 0
        ///   → рыба застревает, тычась носом в стену.
        ///   "Лучший свободный луч" всегда находит выход, даже если
        ///   свободно только одно направление.
        ///
        /// ТРОТТЛИНГ:
        ///   Этот метод вызывается НЕ каждый кадр, а каждые
        ///   AVOIDANCE_UPDATE_INTERVAL (0.15с). Результат кэшируется
        ///   в _cachedAvoidance и применяется каждый кадр.
        ///   Снижение CPU нагрузки на Physics.Raycast ~85%.
        ///
        /// ZERO GC:
        ///   • RaycastNonAlloc → pre-allocated _nonAllocBuffer[1].
        ///   • _rayHitDistances[7] — pre-allocated float array.
        ///   • _rayDirectionsLocal[7] — pre-allocated Vector3 array.
        ///   • TransformDirection — struct math, zero alloc.
        ///   • Никаких List, LINQ, лямбд, замыканий.
        /// </summary>
        private Vector3 ComputeAvoidanceVector()
        {
            Vector3 position = _transform.position;

            // ═══════════════════════════════════════════════════
            //  STEP 1: Динамическая длина лучей (v2.2)
            //
            //  Быстрая рыба в Escape-режиме (12 м/с) не успевает
            //  среагировать на стену, если луч всего 8м.
            //  С lookAheadFactor=0.5: 8 + 12*0.5 = 14м — достаточно.
            // ═══════════════════════════════════════════════════

            float speed = _rb.linearVelocity.magnitude;
            float rayLength = avoidanceRange + speed * lookAheadFactor;

            // Clamp: не меньше базы, не больше максимума
            if (rayLength > maxRayLength) rayLength = maxRayLength;
            if (rayLength < avoidanceRange) rayLength = avoidanceRange;

            _currentRayLength = rayLength;

            // ═══════════════════════════════════════════════════
            //  STEP 2: Пускаем 7 лучей (RaycastNonAlloc)
            //
            //  RaycastNonAlloc записывает результат в pre-allocated
            //  буфер _nonAllocBuffer[1]. Возвращает количество хитов
            //  (0 или 1 при буфере размера 1).
            //  ZERO GC: нет аллокации массива, RaycastHit — struct.
            // ═══════════════════════════════════════════════════

            for (int i = 0; i < RayCount; i++)
            {
                // Конвертируем локальное направление в мировое
                Vector3 worldDir = _transform.TransformDirection(_rayDirectionsLocal[i]);

                int hitCount = UnityEngine.Physics.RaycastNonAlloc(
                    position,
                    worldDir,
                    _nonAllocBuffer,
                    rayLength,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore);

                if (hitCount > 0)
                {
                    _rayHitDistances[i] = _nonAllocBuffer[0].distance;
                }
                else
                {
                    _rayHitDistances[i] = -1f; // Свободен
                }
            }

            // ═══════════════════════════════════════════════════
            //  STEP 3: Центральный луч свободен? → Нет проблем
            //
            //  Если впереди чисто — avoidance не нужен.
            //  Существо продолжает плыть по _desiredDirection.
            // ═══════════════════════════════════════════════════

            if (_rayHitDistances[0] < 0f)
            {
#if UNITY_EDITOR
                _debugBestRayIndex = -1;
#endif
                return Vector3.zero;
            }

            // ═══════════════════════════════════════════════════
            //  STEP 4: Ищем лучший боковой луч
            //
            //  Критерий "лучший":
            //    1. Свободный луч (distance = -1) всегда лучше
            //       заблокированного.
            //    2. Среди свободных — первый по порядку (ближе к
            //       forward = минимальный доворот).
            //    3. Если ВСЕ заблокированы — тот, у которого
            //       distance максимальна (больше пространства).
            //
            //  Порядок лучей (из ComputeRayDirections):
            //    [0] forward (центральный — уже проверен)
            //    [1] вправо 15°  ← внутренняя пара (минимальный доворот)
            //    [2] влево 15°
            //    [3] вправо 30°  ← внешняя пара
            //    [4] влево 30°
            //    [5] вверх 22.5° ← вертикальные
            //    [6] вниз 22.5°
            // ═══════════════════════════════════════════════════

            int bestIndex = -1;
            float bestScore = -1f;

            // Начинаем с 1 — пропускаем центральный (индекс 0)
            for (int i = 1; i < RayCount; i++)
            {
                float dist = _rayHitDistances[i];

                if (dist < 0f)
                {
                    // Свободный луч — лучший кандидат.
                    // Берём первый свободный и прекращаем поиск.
                    // (Лучи упорядочены: внутренние пары первыми,
                    //  т.е. предпочитаем минимальный доворот.)
                    bestIndex = i;
                    break;
                }

                // Все заблокированы — ищем максимальную дистанцию
                if (dist > bestScore)
                {
                    bestScore = dist;
                    bestIndex = i;
                }
            }

            // ═══════════════════════════════════════════════════
            //  STEP 5: Формируем avoidance вектор
            // ═══════════════════════════════════════════════════

            if (bestIndex < 0)
            {
                // Все лучи заблокированы, даже боковые.
                // Аварийный манёвр: разворот назад + вверх.
                // Это крайне редкая ситуация (существо зажато в яме).
#if UNITY_EDITOR
                _debugBestRayIndex = -1;
#endif
                return (_transform.up - _transform.forward).normalized;
            }

            // Направление лучшего луча в мировых координатах
            Vector3 bestDir = _transform.TransformDirection(_rayDirectionsLocal[bestIndex]);

            // Вес avoidance: чем ближе центральный хит, тем сильнее уклонение.
            // (1 - distance/rayLength) → 1.0 при distance=0, 0.0 при distance=rayLength.
            float urgency = 1f - (_rayHitDistances[0] / rayLength);

            // Масштабируем направление urgency-ем.
            // Результат НЕ нормализуется здесь — нормализация в Tick
            // после blend с _desiredDirection.

#if UNITY_EDITOR
            _debugBestRayIndex = bestIndex;
#endif

            return bestDir * urgency;
        }

        // ══════════════════════════════════════════════════════════
        //  WANDER TARGET GENERATION
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Генерирует случайную точку внутри сферы блуждания.
        /// Random.insideUnitSphere — returns struct (zero GC).
        /// Точка ограничена wanderRadius от _spawnPoint.
        /// </summary>
        private void GenerateWanderTarget()
        {
            _wanderTarget = _spawnPoint + Random.insideUnitSphere * wanderRadius;
        }

        // ══════════════════════════════════════════════════════════
        //  PLAYER DETECTION & CACHING
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вычисляет sqrMagnitude до игрока.
        /// Если игрок не найден — пытается найти (ленивый поиск).
        /// Returns false если игрок так и не найден.
        ///
        /// Использует sqrMagnitude — без sqrt, zero GC.
        /// </summary>
        private bool CheckPlayerDistance(out float distSqr)
        {
            distSqr = float.MaxValue;

            if (_playerTransform == null)
            {
                FindPlayer();
                if (_playerTransform == null)
                    return false;
            }

            // Unity null check для destroyed objects
            if ((object)_playerTransform == null || _playerTransform == null)
            {
                _playerTransform = null;
                _playerSurvival = null;
                _playerRigidbody = null;
                _playerFlashlight = null;
                ResetStimulusDebug();
                return false;
            }

            Vector3 diff = _playerTransform.position - _transform.position;
            distSqr = diff.sqrMagnitude;
            return true;
        }

        /// <summary>
        /// Ленивый поиск игрока по тегу "Player".
        /// Вызывается один раз при первом OnEnable или если ссылка потеряна.
        /// GameObject.FindWithTag — аллокация только если тег не найден (null return).
        /// При нахождении — сразу кэширует HectonSurvivalSystem.
        /// </summary>
        private void FindPlayer()
        {
            GameObject playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
            {
                _playerTransform = playerGO.transform;
                CachePlayerSurvival();
                CachePlayerStimulusSources();
            }
        }

        /// <summary>
        /// Кэширует HectonSurvivalSystem с Transform игрока.
        /// Вызывается один раз при обнаружении игрока и при входе в Aggressive.
        /// TryGetComponent — zero GC (no boxing, no allocation).
        /// </summary>
        private void CachePlayerSurvival()
        {
            if (_playerTransform == null) return;

            // Не кэшируем повторно если уже есть валидная ссылка
            if (_playerSurvival != null) return;

            _playerTransform.TryGetComponent(out _playerSurvival);
        }

        private void CachePlayerStimulusSources()
        {
            if (_playerTransform == null) return;

            if (_playerRigidbody == null)
            {
                _playerTransform.TryGetComponent(out _playerRigidbody);
            }

            if (_playerFlashlight == null)
            {
                _playerTransform.TryGetComponent(out _playerFlashlight);
                if (_playerFlashlight == null)
                {
                    _playerFlashlight = _playerTransform.GetComponentInChildren<PlayerFlashlight>();
                }
            }
        }

        private void UpdatePlayerStimulus(float deltaTime)
        {
            if (_playerTransform == null)
            {
                ResetStimulusDebug();
                return;
            }

            CachePlayerStimulusSources();

            float noiseStimulus = reactToPlayerNoise
                ? NoiseSystem.EvaluatePlayerNoise01(_transform.position, _playerTransform, _playerRigidbody)
                : 0f;

            float lightStimulus = reactToPlayerLight
                ? LightDetectionSystem.EvaluatePlayerLight01(_transform.position, _playerTransform, _playerFlashlight)
                : 0f;

            float strongestStimulus = Mathf.Max(noiseStimulus, lightStimulus);
            if (strongestStimulus > 0.01f)
            {
                _stimulusMemoryTimer = stimulusMemoryDuration;
            }
            else if (_stimulusMemoryTimer > 0f)
            {
                _stimulusMemoryTimer = Mathf.Max(0f, _stimulusMemoryTimer - deltaTime);
            }

            float noiseAggroBonus = noiseStimulus * noiseDetectionBonus;
            float lightAggroBonus = lightStimulus * lightDetectionBonus;
            float noiseEscapeBonusValue = noiseStimulus * noiseEscapeBonus;
            float lightEscapeBonusValue = lightStimulus * lightEscapeBonus;
            float strongestDetectionBonus = Mathf.Max(noiseAggroBonus, lightAggroBonus);
            float strongestEscapeBonus = Mathf.Max(noiseEscapeBonusValue, lightEscapeBonusValue);

            float stimulusAggroDistance = aggroDistance + strongestDetectionBonus;
            float stimulusEscapeDistance = escapeDistance + strongestEscapeBonus;
            float stimulusEscapeSafeDistance = escapeSafeDistance + strongestEscapeBonus * 0.75f;
            float stimulusDeaggroDistance = deaggroDistance + strongestDetectionBonus * 0.5f;
            float stimulusWakeDistance = sleepDistance + strongestDetectionBonus;

            if (_stimulusMemoryTimer <= 0f)
            {
                _stimulusAggroDistanceSqr = 0f;
                _stimulusEscapeDistanceSqr = 0f;
                _stimulusEscapeSafeDistanceSqr = 0f;
                _stimulusDeaggroDistanceSqr = 0f;
                _stimulusWakeDistanceSqr = 0f;
            }
            else
            {
                _stimulusAggroDistanceSqr = stimulusAggroDistance * stimulusAggroDistance;
                _stimulusEscapeDistanceSqr = stimulusEscapeDistance * stimulusEscapeDistance;
                _stimulusEscapeSafeDistanceSqr = stimulusEscapeSafeDistance * stimulusEscapeSafeDistance;
                _stimulusDeaggroDistanceSqr = stimulusDeaggroDistance * stimulusDeaggroDistance;
                _stimulusWakeDistanceSqr = stimulusWakeDistance * stimulusWakeDistance;
            }

            _debugNoiseStimulus = noiseStimulus;
            _debugLightStimulus = lightStimulus;
            _debugStimulusMemory = _stimulusMemoryTimer;
        }

        private void ResetStimulusDebug()
        {
            _stimulusMemoryTimer = 0f;
            _stimulusAggroDistanceSqr = 0f;
            _stimulusEscapeDistanceSqr = 0f;
            _stimulusEscapeSafeDistanceSqr = 0f;
            _stimulusDeaggroDistanceSqr = 0f;
            _stimulusWakeDistanceSqr = 0f;
            _debugNoiseStimulus = 0f;
            _debugLightStimulus = 0f;
            _debugStimulusMemory = 0f;
            _debugAggroTriggerDistance = aggroDistance;
            _debugEscapeTriggerDistance = escapeDistance;
        }

        // ══════════════════════════════════════════════════════════
        //  CREATURE HEALTH SYSTEM
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Наносит урон существу.
        /// Вызывается извне (оружие игрока, ловушки, другие системы).
        ///
        /// При получении урона:
        ///   1. Если не в Aggressive — автоматически переходит в Aggressive
        ///      (агрессивная реакция на атаку).
        ///   2. Если HP ≤ 0 — деспавнит себя через ObjectPoolManager.
        ///
        /// <param name="amount">Абсолютное значение урона (положительное число).</param>
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_isDead)    return;
            if (amount <= 0f) return;

            _currentHealth -= amount;

            // ── Автоматическая агрессия при получении урона ──
            if (_currentState != AIState.Aggressive && _playerTransform != null)
            {
                // Существо становится агрессивным при атаке, независимо от isAggressive флага
                isAggressive = true;
                CacheSquaredDistances(); // Пересчёт на случай если aggroDistance не был релевантен
                TransitionTo(AIState.Aggressive);
            }

            // ── Смерть ──
            if (_currentHealth <= 0f)
            {
                _currentHealth = 0f;
                _isDead        = true;

                // Деспавн через пул
                ObjectPoolManager poolManager = ObjectPoolManager.Instance;
                if (poolManager != null)
                {
                    poolManager.Despawn(gameObject);
                }
                else
                {
                    // Fallback: если пул-менеджер не найден — деактивируем
                    gameObject.SetActive(false);
                }
            }
        }

        // ══════════════════════════════════════════════════════════
        //  INITIALIZATION HELPERS
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Сбрасывает всё внутреннее состояние к «новорождённому».
        /// Вызывается из Awake и OnSpawn.
        ///
        /// _avoidanceTimer рандомизирован: разные экземпляры
        /// стартуют с разным сдвигом, предотвращая синхронный
        /// Raycast-шторм при массовом спавне (50+ рыб одновременно).
        /// </summary>
        private void ResetInternalState()
        {
            _currentState        = AIState.Idle;
            _stateTimer          = Random.Range(idleTimeMin, idleTimeMax);
            _desiredDirection    = _transform.forward;
            _targetRotation      = _transform.rotation;
            _rotationDirty       = false;
            _currentHealth       = maxHealth;
            _attackCooldownTimer = 0f;
            _isDead              = false;
            _isSleeping          = false;
            _wanderTimer         = 0f;
            _currentRayLength    = avoidanceRange;

            // ── Avoidance throttle: случайный начальный сдвиг ──
            _avoidanceTimer  = Random.Range(0f, AVOIDANCE_UPDATE_INTERVAL);
            _cachedAvoidance = Vector3.zero;
            ResetStimulusDebug();
        }

        /// <summary>
        /// Вычисляет локальные направления рейкастов (один раз в Awake).
        ///
        /// Расположение: веер в горизонтальной + вертикальной плоскостях.
        ///   Луч 0: forward (центральный).
        ///   Лучи 1-2: горизонтально вправо/влево (1/3 угла) — внутренняя пара.
        ///   Лучи 3-4: горизонтально вправо/влево (2/3 угла) — внешняя пара.
        ///   Лучи 5-6: вертикально вверх/вниз (1/2 угла).
        ///
        /// Порядок важен (v2.2): алгоритм "лучший свободный луч"
        /// предпочитает лучи с меньшим индексом при равных условиях,
        /// т.е. минимальный доворот от forward.
        ///
        /// Направления нормализованы. Хранятся в локальном пространстве,
        /// конвертируются в мировое через TransformDirection в Tick.
        /// </summary>
        private void ComputeRayDirections()
        {
            float step = spreadAngle / 3f;

            // Центральный луч
            _rayDirectionsLocal[0] = Vector3.forward;

            // Горизонтальные — внутренняя пара (минимальный доворот)
            _rayDirectionsLocal[1] = Quaternion.Euler(0f, step, 0f)  * Vector3.forward;
            _rayDirectionsLocal[2] = Quaternion.Euler(0f, -step, 0f) * Vector3.forward;

            // Горизонтальные — внешняя пара
            _rayDirectionsLocal[3] = Quaternion.Euler(0f, step * 2f, 0f)  * Vector3.forward;
            _rayDirectionsLocal[4] = Quaternion.Euler(0f, -step * 2f, 0f) * Vector3.forward;

            // Вертикальные — вверх/вниз
            float vertAngle = spreadAngle * 0.5f;
            _rayDirectionsLocal[5] = Quaternion.Euler(-vertAngle, 0f, 0f) * Vector3.forward;
            _rayDirectionsLocal[6] = Quaternion.Euler(vertAngle, 0f, 0f)  * Vector3.forward;

            // Нормализация (Quaternion * Vector3 уже нормализован, но для ясности)
            for (int i = 0; i < RayCount; i++)
            {
                _rayDirectionsLocal[i] = _rayDirectionsLocal[i].normalized;
            }
        }

        /// <summary>
        /// Кэширует квадраты расстояний для sqrMagnitude-сравнений.
        /// Вызывается один раз в Awake и при OnSpawn.
        /// Исключает sqrt из per-frame кода.
        /// </summary>
        private void CacheSquaredDistances()
        {
            _sleepDistanceSqr       = sleepDistance * sleepDistance;
            _escapeDistanceSqr      = escapeDistance * escapeDistance;
            _escapeSafeDistanceSqr  = escapeSafeDistance * escapeSafeDistance;
            _aggroDistanceSqr       = aggroDistance * aggroDistance;
            _deaggroDistanceSqr     = deaggroDistance * deaggroDistance;
            _waypointReachSqr       = waypointReachDistance * waypointReachDistance;
            _attackRangeSqr         = attackRange * attackRange;
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API — для внешних систем
        // ══════════════════════════════════════════════════════════

        /// <summary>Текущее состояние FSM.</summary>
        public AIState CurrentState => _currentState;

        /// <summary>AI спит (далеко от игрока).</summary>
        public bool IsSleeping => _isSleeping;

        /// <summary>Существо мертво (HP ≤ 0).</summary>
        public bool IsDead => _isDead;

        /// <summary>Точка спавна (центр зоны блуждания).</summary>
        public Vector3 SpawnPoint => _spawnPoint;

        /// <summary>Текущее здоровье существа.</summary>
        public float CurrentHealth => _currentHealth;

        /// <summary>Максимальное здоровье существа.</summary>
        public float MaxHealth => maxHealth;

        /// <summary>Нормализованное здоровье (0..1).</summary>
        public float HealthNormalized => _currentHealth / maxHealth;

        /// <summary>
        /// Принудительная установка точки спавна.
        /// Используй при спавне из пула в новой позиции.
        /// </summary>
        public void SetSpawnPoint(Vector3 point)
        {
            _spawnPoint = point;
        }

        /// <summary>
        /// Принудительный переход в состояние.
        /// Используй из внешних систем (события, скрипты, тригеры).
        /// </summary>
        public void ForceState(AIState state)
        {
            TransitionTo(state);
        }

        // ══════════════════════════════════════════════════════════
        //  DIAGNOSTICS
        // ══════════════════════════════════════════════════════════

        [System.Diagnostics.Conditional("UNITY_EDITOR")]
        private void UpdateDiagnostics(float distSqrToPlayer)
        {
            _debugCurrentState     = _currentState;
            _debugIsSleeping       = _isSleeping;
            _debugDistanceToPlayer = Mathf.Sqrt(distSqrToPlayer);
            _debugCurrentHealth    = _currentHealth;
            _debugCurrentRayLength = _currentRayLength;
        }

        // ══════════════════════════════════════════════════════════
        //  EDITOR — GIZMOS
        // ══════════════════════════════════════════════════════════

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Vector3 pos = Application.isPlaying ? _spawnPoint : transform.position;

            // Зона блуждания
            Gizmos.color = new Color(0f, 0.7f, 1f, 0.08f);
            Gizmos.DrawWireSphere(pos, wanderRadius);

            // Дистанция побега
            Gizmos.color = new Color(1f, 0.8f, 0f, 0.15f);
            Gizmos.DrawWireSphere(transform.position, escapeDistance);

            // Безопасная дистанция
            Gizmos.color = new Color(0f, 1f, 0f, 0.05f);
            Gizmos.DrawWireSphere(transform.position, escapeSafeDistance);

            // Агрессия (если включена)
            if (isAggressive)
            {
                Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
                Gizmos.DrawWireSphere(transform.position, aggroDistance);

                // Радиус атаки
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.25f);
                Gizmos.DrawWireSphere(transform.position, attackRange);
            }

            // Рейкасты (только в Play Mode, v2.2)
            if (Application.isPlaying && _rayDirectionsLocal != null && _rayHitDistances != null)
            {
                float drawLength = _currentRayLength > 0f ? _currentRayLength : avoidanceRange;

                for (int i = 0; i < RayCount; i++)
                {
                    Vector3 worldDir = transform.TransformDirection(_rayDirectionsLocal[i]);

                    if (_rayHitDistances[i] >= 0f)
                    {
                        // Попадание — красный до хита
                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(transform.position,
                                        transform.position + worldDir * _rayHitDistances[i]);

                        // Лучший луч — подсвечиваем циан, остальные — жёлтым
                        if (i == _debugBestRayIndex)
                        {
                            Gizmos.color = Color.cyan;
                            Gizmos.DrawSphere(
                                transform.position + worldDir * _rayHitDistances[i], 0.15f);
                        }
                        else
                        {
                            Gizmos.color = Color.yellow;
                            Gizmos.DrawSphere(
                                transform.position + worldDir * _rayHitDistances[i], 0.1f);
                        }
                    }
                    else
                    {
                        // Свободно — лучший в циан, остальные зелёные
                        Gizmos.color = (i == _debugBestRayIndex)
                            ? Color.cyan
                            : new Color(0f, 1f, 0f, 0.3f);

                        Gizmos.DrawLine(transform.position,
                                        transform.position + worldDir * drawLength);
                    }
                }
            }

            // Wander target
            if (Application.isPlaying && _currentState == AIState.Wander)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawSphere(_wanderTarget, 0.3f);
                Gizmos.DrawLine(transform.position, _wanderTarget);
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
            if (swimForce              < 0f)    swimForce              = 0f;
            if (maxSpeed               < 0.1f)  maxSpeed               = 0.1f;
            if (maxEscapeSpeed         < maxSpeed) maxEscapeSpeed      = maxSpeed;
            if (maxAggressiveSpeed     < maxSpeed) maxAggressiveSpeed  = maxSpeed;
            if (turnSpeed              < 0.1f)  turnSpeed              = 0.1f;
            if (avoidanceRange         < 0.5f)  avoidanceRange         = 0.5f;
            if (lookAheadFactor        < 0f)    lookAheadFactor        = 0f;
            if (maxRayLength           < avoidanceRange) maxRayLength  = avoidanceRange;
            if (spreadAngle            < 5f)    spreadAngle            = 5f;
            if (spreadAngle            > 85f)   spreadAngle            = 85f;
            if (wanderRadius           < 1f)    wanderRadius           = 1f;
            if (escapeDistance          < 1f)    escapeDistance         = 1f;
            if (sleepDistance           < escapeDistance) sleepDistance = escapeDistance * 2f;
            if (attackDamage           < 0f)    attackDamage           = 0f;
            if (attackRange            < 0.1f)  attackRange            = 0.1f;
            if (attackCooldown         < 0.1f)  attackCooldown         = 0.1f;
            if (maxHealth              < 1f)    maxHealth              = 1f;
            if (noiseDetectionBonus    < 0f)    noiseDetectionBonus    = 0f;
            if (noiseEscapeBonus       < 0f)    noiseEscapeBonus       = 0f;
            if (lightDetectionBonus    < 0f)    lightDetectionBonus    = 0f;
            if (lightEscapeBonus       < 0f)    lightEscapeBonus       = 0f;
            if (stimulusMemoryDuration < 0f)    stimulusMemoryDuration = 0f;

            if (escapeSafeDistance < escapeDistance)
                escapeSafeDistance = escapeDistance * 2f;

            // Пересчёт квадратов при изменении в Inspector
            if (Application.isPlaying)
                CacheSquaredDistances();
        }
#endif
    }
}
