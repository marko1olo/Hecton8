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
//   • IFixedTickable — физическое перемещение (AddForce).
//   • IPoolable — интеграция с ObjectPoolManager.
//   • Zero GC в Tick: кэшированные массивы, struct math, no LINQ.
//   • Настройки в Inspector (Data-Driven).
//
// ОПТИМИЗАЦИИ (v2.1):
//   • Obstacle Avoidance троттлинг: рейкасты пускаются каждые 0.15с
//     вместо каждого кадра. Случайный начальный сдвиг таймера
//     предотвращает синхронный рейкаст-спам при массовом спавне.
//     Снижение CPU нагрузки на Raycast ~85%.
//   • Мягкое ограничение скорости: сила применяется ТОЛЬКО если
//     текущая скорость вперёд < лимита. Velocity НЕ перезаписывается.
//     Это позволяет HectonFluidEngine (течения) толкать рыбу быстрее
//     собственного лимита, но сама рыба не превысит его.
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
        //  INSPECTOR — OBSTACLE AVOIDANCE
        // ══════════════════════════════════════════════════════════

        [Header("── Obstacle Avoidance ────────────────────────")]
        [Tooltip("Дальность рейкастов для обнаружения препятствий (метры).")]
        [SerializeField] private float avoidanceRange = 8f;

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

        // ── Obstacle Avoidance: pre-allocated + throttled ──

        /// <summary>
        /// Кэшированные локальные направления рейкастов.
        /// Вычисляются один раз в Awake. Struct array — zero GC.
        /// </summary>
        private Vector3[] _rayDirectionsLocal;

        /// <summary>
        /// Pre-allocated буфер для результатов рейкастов.
        /// RaycastHit — struct. Переиспользуется каждый Tick.
        /// </summary>
        private RaycastHit[] _rayHits;

        /// <summary>
        /// Флаги попадания для каждого луча.
        /// Позволяет избежать проверки distance==0 на RaycastHit.
        /// </summary>
        private bool[] _rayDidHit;

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

            // ── Pre-allocate raycast arrays ──
            _rayDirectionsLocal = new Vector3[RayCount];
            _rayHits            = new RaycastHit[RayCount];
            _rayDidHit          = new bool[RayCount];

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
            _cachedAvoidance = Vector3.zero;
            _avoidanceTimer  = 0f;

            // ── Сброс кэша survival системы игрока ──
            // (при следующем спавне игрок может быть другим объектом)
            _playerSurvival = null;
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
        ///   7. Smooth rotation (Slerp к желаемому направлению).
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
                UpdateDiagnostics(distSqrToPlayer);
                return;
            }

            if (distSqrToPlayer > _sleepDistanceSqr)
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
            //  5. OBSTACLE AVOIDANCE (THROTTLED)
            //
            //  Рейкасты пересчитываются каждые AVOIDANCE_UPDATE_INTERVAL.
            //  Между пересчётами используется кэшированный результат.
            //  Это снижает CPU нагрузку на Physics.Raycast ~85%
            //  при сохранении визуально плавного избегания препятствий.
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
            //  6. SMOOTH ROTATION
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

                _transform.rotation = Quaternion.Slerp(
                    _transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-currentTurnSpeed * deltaTime));
            }

            UpdateDiagnostics(distSqrToPlayer);
        }

        // ══════════════════════════════════════════════════════════
        //  IFixedTickable — PHYSICS (фиксированный шаг)
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Применяет физическую силу движения.
        /// Вызывается в FixedUpdate через GameTickManager.
        ///
        /// Разделение Tick/FixedTick:
        ///   • Tick — мозг (направление, состояния, рейкасты).
        ///   • FixedTick — мышцы (AddForce, мягкое ограничение скорости).
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
            // Если спим, мертвы или в Idle — не прикладываем силу
            if (_isSleeping || _isDead || _currentState == AIState.Idle)
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
            switch (_currentState)
            {
                // ─────────────────────────────────────────────────
                case AIState.Idle:
                {
                    // Агрессия
                    if (isAggressive && distSqrToPlayer < _aggroDistanceSqr)
                    {
                        TransitionTo(AIState.Aggressive);
                        return;
                    }

                    // Побег
                    if (!isAggressive && distSqrToPlayer < _escapeDistanceSqr)
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
                    if (isAggressive && distSqrToPlayer < _aggroDistanceSqr)
                    {
                        TransitionTo(AIState.Aggressive);
                        return;
                    }

                    // Побег
                    if (!isAggressive && distSqrToPlayer < _escapeDistanceSqr)
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
                    if (distSqrToPlayer > _escapeSafeDistanceSqr)
                    {
                        TransitionTo(AIState.Wander);
                    }

                    break;
                }

                // ─────────────────────────────────────────────────
                case AIState.Aggressive:
                {
                    // Потеря агрессии (leash distance)
                    if (distSqrToPlayer > _deaggroDistanceSqr)
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
        //  OBSTACLE AVOIDANCE
        // ══════════════════════════════════════════════════════════

        /// <summary>
        /// Вычисляет avoidance-вектор на основе веера рейкастов.
        ///
        /// АЛГОРИТМ:
        ///   1. Пускаем 7 лучей из позиции существа по кэшированным
        ///      направлениям (в мировых координатах через TransformDirection).
        ///   2. Для каждого попадания — добавляем обратный вектор,
        ///      взвешенный обратно пропорционально дистанции.
        ///      Чем ближе препятствие, тем сильнее отталкивание.
        ///   3. Результат — ненормализованный вектор отклонения.
        ///      Нормализуется в вызывающем коде при blend с desired direction.
        ///
        /// ТРОТТЛИНГ:
        ///   Этот метод вызывается НЕ каждый кадр, а каждые
        ///   AVOIDANCE_UPDATE_INTERVAL (0.15с). Результат кэшируется
        ///   в _cachedAvoidance и применяется каждый кадр.
        ///   Снижение CPU нагрузки на Physics.Raycast ~85%.
        ///
        /// ZERO GC:
        ///   • Кэшированные _rayDirectionsLocal (Vector3[]).
        ///   • Pre-allocated _rayHits (RaycastHit[]).
        ///   • Pre-allocated _rayDidHit (bool[]).
        ///   • Physics.Raycast — zero GC (single hit, struct out).
        ///   • Все вычисления — struct math.
        /// </summary>
        private Vector3 ComputeAvoidanceVector()
        {
            Vector3 avoidance = Vector3.zero;
            Vector3 position  = _transform.position;

            for (int i = 0; i < RayCount; i++)
            {
                // Конвертируем локальное направление в мировое
                Vector3 worldDir = _transform.TransformDirection(_rayDirectionsLocal[i]);

                _rayDidHit[i] = UnityEngine.Physics.Raycast(
                    position,
                    worldDir,
                    out _rayHits[i],
                    avoidanceRange,
                    obstacleMask,
                    QueryTriggerInteraction.Ignore);

                if (_rayDidHit[i])
                {
                    float hitDistance = _rayHits[i].distance;

                    // Вес: обратно пропорционален расстоянию.
                    // Ближе = сильнее отталкивание.
                    // (1 - distance/range) даёт 1.0 при distance=0, 0.0 при distance=range.
                    float weight = 1f - (hitDistance / avoidanceRange);

                    // Отталкивающий вектор: нормаль поверхности × вес
                    // Нормаль указывает ОТ препятствия — именно то, что нужно.
                    avoidance += _rayHits[i].normal * weight;
                }
            }

            return avoidance;
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
                _playerSurvival  = null;
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
            _currentHealth       = maxHealth;
            _attackCooldownTimer = 0f;
            _isDead              = false;
            _isSleeping          = false;
            _wanderTimer         = 0f;

            // ── Avoidance throttle: случайный начальный сдвиг ──
            _avoidanceTimer  = Random.Range(0f, AVOIDANCE_UPDATE_INTERVAL);
            _cachedAvoidance = Vector3.zero;
        }

        /// <summary>
        /// Вычисляет локальные направления рейкастов (один раз в Awake).
        ///
        /// Расположение: веер в горизонтальной + вертикальной плоскостях.
        ///   Луч 0: forward (центральный).
        ///   Лучи 1-2: горизонтально влево/вправо (1/3 угла).
        ///   Лучи 3-4: горизонтально влево/вправо (2/3 угла).
        ///   Лучи 5-6: вертикально вверх/вниз (1/2 угла).
        ///
        /// Направления нормализованы. Хранятся в локальном пространстве,
        /// конвертируются в мировое через TransformDirection в Tick.
        /// </summary>
        private void ComputeRayDirections()
        {
            float step = spreadAngle / 3f;

            // Центральный луч
            _rayDirectionsLocal[0] = Vector3.forward;

            // Горизонтальные — внутренняя пара
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

            // Рейкасты (только в Play Mode)
            if (Application.isPlaying && _rayDirectionsLocal != null)
            {
                for (int i = 0; i < RayCount; i++)
                {
                    Vector3 worldDir = transform.TransformDirection(_rayDirectionsLocal[i]);

                    if (_rayDidHit != null && _rayDidHit[i])
                    {
                        // Попадание — красный
                        Gizmos.color = Color.red;
                        Gizmos.DrawLine(transform.position,
                                        transform.position + worldDir * _rayHits[i].distance);

                        Gizmos.color = Color.yellow;
                        Gizmos.DrawSphere(_rayHits[i].point, 0.1f);
                    }
                    else
                    {
                        // Свободно — зелёный
                        Gizmos.color = new Color(0f, 1f, 0f, 0.3f);
                        Gizmos.DrawLine(transform.position,
                                        transform.position + worldDir * avoidanceRange);
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
            if (swimForce       < 0f) swimForce       = 0f;
            if (maxSpeed        < 0.1f) maxSpeed       = 0.1f;
            if (maxEscapeSpeed  < maxSpeed) maxEscapeSpeed = maxSpeed;
            if (maxAggressiveSpeed < maxSpeed) maxAggressiveSpeed = maxSpeed;
            if (turnSpeed       < 0.1f) turnSpeed      = 0.1f;
            if (avoidanceRange  < 0.5f) avoidanceRange = 0.5f;
            if (spreadAngle     < 5f) spreadAngle      = 5f;
            if (spreadAngle     > 85f) spreadAngle     = 85f;
            if (wanderRadius    < 1f) wanderRadius     = 1f;
            if (escapeDistance   < 1f) escapeDistance   = 1f;
            if (sleepDistance    < escapeDistance) sleepDistance = escapeDistance * 2f;
            if (attackDamage    < 0f) attackDamage     = 0f;
            if (attackRange     < 0.1f) attackRange    = 0.1f;
            if (attackCooldown  < 0.1f) attackCooldown = 0.1f;
            if (maxHealth       < 1f) maxHealth        = 1f;

            if (escapeSafeDistance < escapeDistance)
                escapeSafeDistance = escapeDistance * 2f;

            // Пересчёт квадратов при изменении в Inspector
            if (Application.isPlaying)
                CacheSquaredDistances();
        }
#endif
    }
}