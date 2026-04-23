using System;
using UnityEngine;
using UnityEngine.Serialization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.VFX;
using Hecton8.World;

namespace Hecton8.AI
{
    /// <summary>
    /// Master controller for HECTON-8 Fauna AI.
    /// Handles subsystem lifecycle, Brain LOD, and legacy property migration.
    /// [RULE] ZERO GC IN HOT PATHS.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Rigidbody))]
    public partial class FaunaBrain : MonoBehaviour, IUpdatable, ITickable, IFixedTickable, ISlowTickable, IPoolable, ISerializationCallbackReceiver
    {
        /// <summary>
        /// Global state definition for all fauna.
        /// [REQ] Restored as nested enum for legacy tool compatibility.
        /// </summary>
        public enum AIState
        {
            Idle,
            Wander,
            Investigate,
            Threaten,
            Stalk,
            Loom,
            Feint,
            Escape,
            Aggressive,
            Flocking,
            Return,
            Retreat,
            Sated,
            ThreatDisplay
        }

        [Header("── Core Identity ────────────────────────────────")]
        public bool isAggressive = false;
        public bool canFlee = true;

        [Header("── Brain LOD ──────────────────────────────────────")]
        public bool enableBrainLOD = true;
        public float brainDisableDistance = 150f;
        public float brainOptimizationDistance = 80f;

        [Header("── Subsystems ───────────────────────────────────")]
        [SerializeField] private FaunaSpeciesProfile _speciesProfile;
        [SerializeField] private FaunaSensorSuite _sensorSuite = new FaunaSensorSuite();
        [SerializeField] private FaunaSteeringEngine _steeringEngine = new FaunaSteeringEngine();
        [SerializeField] private FaunaStateMachine _stateMachine = new FaunaStateMachine();

        public AIState CurrentState => _stateMachine.currentState;
        public FaunaSpeciesProfile SpeciesProfile => _speciesProfile;

        /// <summary>
        /// [REQ] Eye Tracking vector for Animator/Bones.
        /// Feed this to a procedural head-look system or animator layer.
        /// </summary>
        public Vector3 LookDirection { get; private set; }

        // --- INTERNAL ---
        private Rigidbody _rb;
        private Animator _animator;
        private bool _isDead;
        private bool _dispatcherRegistered;
        private int _spatialHandle;
        private readonly CreatureUtilityBrain _utilityBrain = new CreatureUtilityBrain();
        
        // --- Animator Hashes (Prime Directive #18) ---
        private static readonly int _HashSwimSpeed = Animator.StringToHash("SwimSpeed");
        private const float SlowTickIntervalSeconds = 0.5f;
        private const int MaxSlowTicksPerDispatcherTick = 2;
        private const float AmbientCurrentInfluence = 0.22f;
        private const float AmbientCurrentMaxVelocity = 3.8f;
        private const float AmbientCurrentCullDistance = 100f;
        private const float AmbientCurrentCullDistanceSqr = AmbientCurrentCullDistance * AmbientCurrentCullDistance;
        
        // --- LOD & Stagger ---
        private bool _lodDisabled;
        private Renderer _renderer;
        private int _tickStaggerShift;
        private Vector3 _cachedDesiredDirection;
        private AIState _currentStateCache;
        private Transform _currentCullingPlayerTransform;
        
        // --- Buffers ---
        private static readonly Collider[] _panicBuffer = new Collider[10];

        // --- Event Hooks ---
        public Action<AIState> OnStateChanged;
        
        [Header("── Audio Hooks ─────────────────────────────────")]
        [Tooltip("Triggered when a Panic Pulse occurs. Hook audio agents here for zero-GC sound dispatch.")]
        public UnityEngine.Events.UnityEvent OnPanicTriggered;

        private float _slowTickAccumulator;

        // ══════════════════════════════════════════════════════════
        //  SERIALIZATION MIGRATION (Option B Data Preservation)
        // ══════════════════════════════════════════════════════════
        [SerializeField, HideInInspector] private bool _migratedV2;

        [SerializeField, HideInInspector, FormerlySerializedAs("swimForce")] private float le_swimForce = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("maxSpeed")] private float le_maxSpeed = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("turnSpeed")] private float le_turnSpeed = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("avoidanceRange")] private float le_avoidanceRange = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("lookAheadFactor")] private float le_lookAheadFactor = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("maxRayLength")] private float le_maxRayLength = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("spreadAngle")] private float le_spreadAngle = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("wanderRadius")] private float le_wanderRadius = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("waypointReachDistance")] private float le_waypointReachDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("escapeDistance")] private float le_escapeDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("escapeSafeDistance")] private float le_escapeSafeDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("sleepDistance")] private float le_sleepDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("aggroDistance")] private float le_aggroDistance = -1f;
        [SerializeField, HideInInspector, FormerlySerializedAs("deaggroDistance")] private float le_deaggroDistance = -1f;

        public void OnBeforeSerialize() { }

        public void OnAfterDeserialize()
        {
            if (!_migratedV2)
            {
                if (le_swimForce >= 0) _steeringEngine.swimForce = le_swimForce;
                if (le_maxSpeed >= 0) _steeringEngine.maxSpeed = le_maxSpeed;
                if (le_turnSpeed >= 0) _steeringEngine.turnSpeed = le_turnSpeed;
                if (le_avoidanceRange >= 0) _sensorSuite.avoidanceRange = le_avoidanceRange;
                if (le_lookAheadFactor >= 0) _sensorSuite.lookAheadFactor = le_lookAheadFactor;
                if (le_maxRayLength >= 0) _sensorSuite.maxRayLength = le_maxRayLength;
                if (le_spreadAngle >= 0) _sensorSuite.spreadAngle = le_spreadAngle;
                if (le_wanderRadius >= 0) _stateMachine.wanderRadius = le_wanderRadius;
                if (le_waypointReachDistance >= 0) _stateMachine.waypointReachDistance = le_waypointReachDistance;
                if (le_escapeDistance >= 0) _stateMachine.escapeDistance = le_escapeDistance;
                if (le_escapeSafeDistance >= 0) _stateMachine.escapeSafeDistance = le_escapeSafeDistance;
                if (le_sleepDistance >= 0) _sensorSuite.sleepDistance = le_sleepDistance;
                if (le_aggroDistance >= 0) _sensorSuite.aggroDistance = le_aggroDistance;
                if (le_deaggroDistance >= 0) _sensorSuite.deaggroDistance = le_deaggroDistance;

                le_swimForce = -1f; le_maxSpeed = -1f; le_turnSpeed = -1f;
                le_avoidanceRange = -1f; le_lookAheadFactor = -1f; le_maxRayLength = -1f; le_spreadAngle = -1f;
                le_wanderRadius = -1f; le_waypointReachDistance = -1f; le_escapeDistance = -1f;
                le_escapeSafeDistance = -1f; le_sleepDistance = -1f; le_aggroDistance = -1f; le_deaggroDistance = -1f;

                _migratedV2 = true;
            }
        }

        // ══════════════════════════════════════════════════════════
        //  LIFECYCLE
        // ══════════════════════════════════════════════════════════
        private void Awake()
        {
            _rb = GetComponent<Rigidbody>();
            _renderer = GetComponentInChildren<Renderer>();
            TryGetComponent(out _animator);
            ResolveFoveatedBindings();
            _tickStaggerShift = UnityEngine.Random.Range(0, 10);

            // Inject profile into subsystems
            _steeringEngine.Init(_rb, transform, _speciesProfile);
            _sensorSuite.Init(transform, _speciesProfile);
            _stateMachine.Init(transform, _speciesProfile);
            _utilityBrain.Initialize(transform.position, _speciesProfile, _archetype);
            _cognitionTimeSeconds = 0f;
            
            _stateMachine.OnAttackPerform += HandleAttackPerform;
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            SystemDispatcher.EnsureRuntimeInstance();
            if (!_dispatcherRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = true;
            }

            RegisterSpatialHandle();
            ResetDispatcherCadence();
        }

        private void OnDisable()
        {
            if (!Application.isPlaying)
                return;

            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            ClearInfectionHazardRegistration();
            UnregisterSpatialHandle();
            ResetDispatcherCadence();
        }

        private void OnDestroy()
        {
            ClearInfectionHazardRegistration();
            if (_stateMachine != null)
                _stateMachine.OnAttackPerform -= HandleAttackPerform;

            _utilityBrain.Dispose();
        }

        public void OnSpawn()
        {
            _isDead = false;
            _runtimeAggressionScale = 1f;
            ClearGeneticTraits();
            SetInfectedState(false, 0f);
            _currentHealth = _maxHealth;
            _stateMachine.Init(transform, _speciesProfile);
            _utilityBrain.ResetRuntimeState(transform.position);
            _cognitionTimeSeconds = 0f;
            RefreshRuntimeEcosystemState();
            RegisterSpatialHandle();
            ResetDispatcherCadence();
        }

        public void OnDespawn()
        {
            _isDead = true;
            _runtimeAggressionScale = 1f;
            ClearGeneticTraits();
            SetInfectedState(false, 0f);
            _rb.linearVelocity = Vector3.zero;
            _rb.angularVelocity = Vector3.zero;
            _utilityBrain.ResetRuntimeState(transform.position);
            _cognitionTimeSeconds = 0f;
            ClearInfectionHazardRegistration();
            UnregisterSpatialHandle();
            ResetDispatcherCadence();
        }

        // ══════════════════════════════════════════════════════════
        //  TICK PIPELINE (Absolute Zero GC)
        // ══════════════════════════════════════════════════════════
        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;

            _cognitionTimeSeconds += dt;
            _sensorSuite.Tick(dt, _rb.linearVelocity, _cognitionTimeSeconds);
            _lodDisabled = _sensorSuite.lodDisabled;

            if (_lodDisabled || _sensorSuite.isSleeping)
            {
                FixedTick(dt);
                AdvanceSlowTickCadence(dt);
                return;
            }

            AIState oldState = _currentStateCache;
            Vector3 selfPosition = transform.position;
            if (TryEvaluateUtilityPredatorBrain(dt, selfPosition, out CreatureUtilityEvaluation utilityEvaluation, out Transform attackTarget))
            {
                _cachedDesiredDirection = utilityEvaluation.DesiredDirection;
                _currentStateCache = utilityEvaluation.LegacyState;
                _stateMachine.currentState = utilityEvaluation.LegacyState;
                _stateMachine.currentForceMultiplier = utilityEvaluation.ForceMultiplier;
                _stateMachine.currentSpeedMultiplier = utilityEvaluation.SpeedMultiplier;
                _stateMachine.currentTurnMultiplier = utilityEvaluation.TurnMultiplier;

                if (utilityEvaluation.ShouldAttack && attackTarget != null)
                {
                    HandleAttackPerform(attackTarget);
                    float attackCooldown = _speciesProfile != null ? _speciesProfile.attackCooldown : 1f;
                    _utilityBrain.NotifyAttackPerformed(_cognitionTimeSeconds, attackCooldown);
                }
            }
            else
            {
                _cachedDesiredDirection = _stateMachine.EvaluateAndGetDirection(dt, _sensorSuite, isAggressive, canFlee, HealthNormalized, selfPosition);
                _currentStateCache = _stateMachine.currentState;
            }

            if (_currentStateCache != oldState)
            {
                OnStateChanged?.Invoke(_currentStateCache);
            }

            if (_sensorSuite.isAvoidingObstacle)
            {
                _cachedDesiredDirection = Vector3.Lerp(_cachedDesiredDirection, _sensorSuite.bestFreeDirection, 0.7f).normalized;
                if (_sensorSuite.IsStuck && _sensorSuite.hasEscapePOI)
                {
                    Vector3 poiDir = (_sensorSuite.currentEscapePOI - transform.position).normalized;
                    _cachedDesiredDirection = Vector3.Lerp(_cachedDesiredDirection, poiDir, 0.6f).normalized;
                }
            }

            if (_animator != null && _steeringEngine.maxSpeed > 0f)
            {
                float movementIntensity = _rb.linearVelocity.magnitude / _steeringEngine.maxSpeed;
                _animator.SetFloat(_HashSwimSpeed, movementIntensity);
            }

            // [REQ] Procedural Eye Tracking (The "Stare")
            UpdateEyeTracking(dt);
            FixedTick(dt);
            AdvanceSlowTickCadence(dt);
        }

        private void UpdateEyeTracking(float dt)
        {
            if (_speciesProfile == null || _speciesProfile.eyeTrackWeight <= 0.01f)
            {
                LookDirection = transform.forward;
                return;
            }

            Vector3 targetPos = Vector3.zero;
            bool hasTarget = false;

            // Priority: Threat > Distractor > Player > Prey
            if (_sensorSuite.currentThreat != null) { targetPos = _sensorSuite.currentThreat.position; hasTarget = true; }
            else if (_sensorSuite.currentDistractor != null) { targetPos = _sensorSuite.currentDistractor.position; hasTarget = true; }
            else if (_sensorSuite.canSeePlayer && _sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 playerTargetPos)) { targetPos = playerTargetPos; hasTarget = true; }
            else if (_sensorSuite.currentPrey != null) { targetPos = _sensorSuite.currentPrey.position; hasTarget = true; }

            if (hasTarget)
            {
                float distSqr = (targetPos - transform.position).sqrMagnitude;
                if (distSqr < _speciesProfile.eyeTrackRange * _speciesProfile.eyeTrackRange)
                {
                    Vector3 toTarget = (targetPos - transform.position).normalized;
                    // Apply LookAt weight
                    LookDirection = Vector3.Slerp(transform.forward, toTarget, _speciesProfile.eyeTrackWeight);
                    return;
                }
            }

            // Default to forward
            LookDirection = Vector3.Slerp(LookDirection, transform.forward, 5f * dt);
        }

        private bool TryEvaluateUtilityPredatorBrain(
            float dt,
            Vector3 selfPosition,
            out CreatureUtilityEvaluation evaluation,
            out Transform attackTarget)
        {
            if (!_utilityBrain.IsActivePredator)
            {
                evaluation = default;
                attackTarget = null;
                return false;
            }

            bool hasPerceivedPlayerPosition = _sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 perceivedPlayerPosition);
            bool hasDirectPlayerTransform = _sensorSuite.TryGetDirectPlayerTransform(out Transform directPlayerTransform);
            float fearPressure01 = _sensorSuite.isThreatened ? 0.35f : 0f;
            if (_sensorSuite.currentThreat != null)
                fearPressure01 += 0.2f;

            CreatureUtilityContext context = new CreatureUtilityContext(
                selfPosition,
                transform.forward,
                HealthNormalized,
                canFlee,
                _sensorSuite.hasVisualPlayerContact,
                hasPerceivedPlayerPosition,
                perceivedPlayerPosition,
                _sensorSuite.distSqrToPlayer,
                _speciesProfile != null ? _speciesProfile.attackRadius : 3f,
                Mathf.Clamp01(fearPressure01));
            evaluation = _utilityBrain.Evaluate(dt, _cognitionTimeSeconds, in context);
            attackTarget = hasDirectPlayerTransform ? directPlayerTransform : null;
            return true;
        }

        public void FixedTick(float fdt)
        {
            if (_spatialHandle != 0)
                WorldSpatialHashGrid.Refresh(_spatialHandle);

            if (_isDead || _lodDisabled) return;
            Vector3 playerTargetPosition = default;
            if (_sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 perceivedPlayerPosition))
                playerTargetPosition = perceivedPlayerPosition;

            float runtimeSpeedScale = ResolveRuntimeSpeedMultiplierForState(_stateMachine.currentState);
            
            _steeringEngine.FixedTick(
                fdt, 
                _cachedDesiredDirection, 
                _stateMachine.currentForceMultiplier, 
                _stateMachine.currentSpeedMultiplier * runtimeSpeedScale,
                _stateMachine.currentTurnMultiplier,
                _stateMachine.currentState == AIState.Retreat,
                playerTargetPosition
            );

            ApplyAmbientCurrentDrift(fdt);
        }

        public void SlowTick()
        {
            RefreshRuntimeEcosystemState();
        }

        private void AdvanceSlowTickCadence(float dt)
        {
            _slowTickAccumulator += dt;

            int iterationCount = 0;
            while (_slowTickAccumulator >= SlowTickIntervalSeconds &&
                   iterationCount < MaxSlowTicksPerDispatcherTick)
            {
                _slowTickAccumulator -= SlowTickIntervalSeconds;
                SlowTick();
                iterationCount++;
            }

            if (_slowTickAccumulator > SlowTickIntervalSeconds)
                _slowTickAccumulator = SlowTickIntervalSeconds;
        }

        private void ResetDispatcherCadence()
        {
            _slowTickAccumulator = 0f;
        }

        private void RegisterSpatialHandle()
        {
            if (_spatialHandle != 0 || !isActiveAndEnabled)
                return;

            _spatialHandle = WorldSpatialHashGrid.RegisterBioform(this);
        }

        private void UnregisterSpatialHandle()
        {
            if (_spatialHandle == 0)
                return;

            WorldSpatialHashGrid.Unregister(_spatialHandle);
            _spatialHandle = 0;
        }

        private void ApplyAmbientCurrentDrift(float fdt)
        {
            if (_rb == null || fdt <= 0f)
                return;

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _currentCullingPlayerTransform);
            if (_currentCullingPlayerTransform != null)
            {
                Vector3 toPlayer = _currentCullingPlayerTransform.position - _rb.worldCenterOfMass;
                if (toPlayer.sqrMagnitude > AmbientCurrentCullDistanceSqr)
                {
                    if (_rb.linearVelocity.sqrMagnitude <= 0.04f && !_rb.IsSleeping())
                        _rb.Sleep();

                    return;
                }
            }

            Vector3 sampledCurrent = CurrentVolume.SampleCombinedCurrent(_rb.worldCenterOfMass);
            if (sampledCurrent.sqrMagnitude <= 0.0001f)
                return;

            Vector3 velocityChange = Vector3.ClampMagnitude(sampledCurrent, AmbientCurrentMaxVelocity) * (AmbientCurrentInfluence * fdt);
            PhysicsForceRouter.QueueForce(_rb, velocityChange, ForceMode.VelocityChange);
        }

        // ══════════════════════════════════════════════════════════
        //  PUBLIC API
        // ══════════════════════════════════════════════════════════
        internal void ReceivePlayerNoiseSignal(NoiseSystem.PlayerNoiseSignal signal)
        {
            if (_isDead || !isActiveAndEnabled)
                return;

            _sensorSuite.ReceivePlayerNoiseSignal(signal);
            _utilityBrain.RecordAuditoryStimulus(signal.Position, _cognitionTimeSeconds);
        }

        private void HandleAttackPerform(Transform target)
        {
            if (target == null || _isDead) return;
            
            float damage = ResolveCurrentAttackDamage();

            // 1. PREY INTERACTION (Food Chain)
            if (target.CompareTag("Prey"))
            {
                // [RULE] Predators entering Sated state after eating
                float satedDur = _speciesProfile != null ? _speciesProfile.satedDuration : 45f;
                _stateMachine.ForceSated(satedDur);
                
                // [REQ] SHOAL SCATTERING (Panic Pulse)
                // Trigger panic in all nearby prey within 10m
                int count = UnityEngine.Physics.OverlapSphereNonAlloc(target.position, 10f, _panicBuffer, _sensorSuite.preyMask);
                for (int i = 0; i < count; i++)
                {
                    if (_panicBuffer[i].TryGetComponent<FaunaBrain>(out var neighborBrain))
                    {
                        neighborBrain.TriggerPanicPulse(transform.position);
                    }
                }

                // Despawn/Pool the prey
                if (target.TryGetComponent<FaunaBrain>(out var preyBrain))
                {
                    preyBrain.TakeDamage(damage * 10f); // Massive damage to ensure kill
                }
                else
                {
                    // Fallback for non-brain prey (e.g. static/simple pooled objects)
                    if (ObjectPoolManager.Instance != null)
                        ObjectPoolManager.Instance.Despawn(target.gameObject);
                    else
                        target.gameObject.SetActive(false);
                }

                #if UNITY_EDITOR || DEVELOPMENT_BUILD
                Debug.Log($"[FAUNA] {gameObject.name} fed on {target.name}. Entering SATED state for {satedDur}s.");
                #endif
                return;
            }

            // 2. PLAYER / VEHICLE INTERACTION
            if (target.CompareTag("Player"))
            {
                // Resolve HectonPlayerHealth lookup (Fixed as per REQ)
                if (target.TryGetComponent<HectonPlayerHealth>(out var playerHealth))
                {
                    playerHealth.TakeDamage(damage);
                }

                // LEVIATHAN SPECIALIZATION: Vehicle Impact
                bool isLeviathan = _speciesProfile != null && _speciesProfile.isLeviathan;
                if (isLeviathan)
                {
                    // Check if player is using MantaScooter
                    // Scooter is typically a child of the player or held tool
                    // We check for components on the player or children
                    var scooter = target.GetComponentInChildren<MantaScooter>();
                    if (scooter != null)
                    {
                        // In Subnautica, damage to scooter/seaglide is often handled via player health 
                        // or tool durability. Since scooter has no health, we apply physical force + noise.
                        #if UNITY_EDITOR || DEVELOPMENT_BUILD
                        Debug.Log($"[LEVIATHAN] {gameObject.name} struck Player's Manta Scooter!");
                        #endif
                    }
                }

                // 3. JUICE (User REQ: Camera Shake + Physical Force)
                if (_speciesProfile != null && _speciesProfile.attackShakeProfile != null)
                {
                    if (CameraJuiceSystem.Instance != null)
                    {
                        CameraJuiceSystem.Instance.TriggerShake(_speciesProfile.attackShakeProfile);
                    }
                }

                if (_speciesProfile != null && _speciesProfile.impactForceToPlayer > 0f)
                {
                    if (target.TryGetComponent(out Rigidbody playerRb))
                    {
                        Vector3 impactDir = (target.position - transform.position).normalized;
                        PhysicsForceRouter.QueueForce(
                            playerRb,
                            impactDir * _speciesProfile.impactForceToPlayer,
                            ForceMode.Impulse);
                    }
                }
            }
            else if (target.TryGetComponent(out FaunaBrain otherBrain))
            {
                otherBrain.TakeDamage(damage);
            }
        }

        public void TakeDamage(float amount)
        {
            if (_isDead) return;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            if (_currentHealth <= 0.001f) Die();
        }

        /// <summary>
        /// Instantly breaks boids alignment and applies evasion vector.
        /// [REQ] Zero-GC Panic Effect.
        /// </summary>
        public void TriggerPanicPulse(Vector3 predatorPos)
        {
            if (_isDead) return;
            
            _sensorSuite.isScattering = true;
            Vector3 baseDir = (transform.position - predatorPos).normalized;
            // [REQ] "randomly away from the predator" - add slight jitter
            Vector3 randomOffset = UnityEngine.Random.onUnitSphere * 0.2f;
            _sensorSuite.scatterDirection = (baseDir + randomOffset).normalized;
            
            // [REQ] Audio Linking (Sound of Panic)
            OnPanicTriggered?.Invoke();
            
            // StateMachine will handle the timer via _scatterTimer if it's in Flocking state
        }

        /// <summary>
        /// [REQ] Final API Exposure for external tools (Propulsion Cannon, Flashlight).
        /// Forces the AI into a Retreat state strictly away from the threatPosition.
        /// </summary>
        public void Provoke(Vector3 threatPosition)
        {
            if (_isDead) return;

            if (_utilityBrain.IsActivePredator)
            {
                _utilityBrain.ForceRetreat(threatPosition, _cognitionTimeSeconds, 8f);
                _stateMachine.currentState = AIState.Retreat;
                return;
            }

            _stateMachine.ForceRetreat(threatPosition, 8.0f); // Default 8s retreat
        }

        private void Die()
        {
            _isDead = true;
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Despawn(gameObject);
            else
                gameObject.SetActive(false);
        }
    }
}
