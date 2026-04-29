using System;
using UnityEngine;
using UnityEngine.Serialization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Physics;
using Hecton8.VFX;
using Hecton8.World;
using Unity.Mathematics;

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
        [SerializeField] private FaunaStateMachine _stateMachine = FaunaStateMachine.CreateDefault();

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
        private int _faunaSpatialHandle;
        private CreatureUtilityBrain _utilityBrain;
        private ProceduralLeviathanSpineIK _proceduralLeviathanSpineIk;
        
        // --- Animator Hashes (Prime Directive #18) ---
        private static readonly int _HashSwimSpeed = Animator.StringToHash("SwimSpeed");
        private const float SlowTickIntervalSeconds = 0.5f;
        private const int MaxSlowTicksPerDispatcherTick = 2;
        private const float AmbientCurrentInfluence = 0.22f;
        private const float AmbientCurrentMaxVelocity = 3.8f;
        private const float AmbientCurrentCullDistance = 100f;
        private const float AmbientCurrentCullDistanceSqr = AmbientCurrentCullDistance * AmbientCurrentCullDistance;
        private const float PredatorHazardAvoidanceRadius = 14f;
        private const float PredatorHazardFearThreshold = 0.5f;
        private const int MaxVoxelRouteWaypointCount = 16;
        private const float VoxelRouteRefreshIntervalSeconds = 0.25f;
        private const float VoxelRouteRetargetDistanceSqr = 16f;
        private const float VoxelRouteWaypointReachDistanceSqr = 4f;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextSlowTickWatchdogLogTime;
#endif
        
        // --- LOD & Stagger ---
        private bool _lodDisabled;
        private Renderer _renderer;
        private Unity.Mathematics.Random _runtimeRandom;
        private int _tickStaggerShift;
        private Vector3 _cachedDesiredDirection;
        private AIState _currentStateCache;
        private Transform _currentCullingPlayerTransform;
        
        // --- Buffers ---
        private static readonly SpatialQueryHit[] _panicBuffer = new SpatialQueryHit[10];
        // COLD ALLOC: Vector3[16] - reusable 3D cave-voxel guidance route for predator steering - owner: FaunaBrain
        private readonly Vector3[] _voxelRouteWaypoints = new Vector3[MaxVoxelRouteWaypointCount];

        // --- Event Hooks ---
        public Action<AIState> OnStateChanged;
        
        [Header("── Audio Hooks ─────────────────────────────────")]
        [Tooltip("Triggered when a Panic Pulse occurs. Hook audio agents here for zero-GC sound dispatch.")]
        public UnityEngine.Events.UnityEvent OnPanicTriggered;

        private float _slowTickAccumulator;
        private int _voxelRouteWaypointCount;
        private float _nextVoxelRouteRefreshTime;
        private Vector3 _voxelRouteTargetPosition;
        private bool _hasVoxelRouteTarget;

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
            _renderer = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);
            TryGetComponent(out _animator);
            TryGetComponent(out _proceduralLeviathanSpineIk);
            ResolveFoveatedBindings();
            _runtimeRandom = CreateDeterministicRandom();
            _tickStaggerShift = _runtimeRandom.NextInt(0, 10);

            // Inject profile into subsystems
            _steeringEngine.Init(_rb, transform, _speciesProfile);
            _sensorSuite.Init(transform, _speciesProfile);
            _utilityBrain.Initialize(transform.position, _speciesProfile, _archetype);
            ResetStateCache();
            _cognitionTimeSeconds = 0f;
            EnsureLeviathanPresentationOwner();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (!_dispatcherRegistered)
            {
                GlobalRegistry.RegisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = true;
            }

            RegisterSpatialHandle();
            _utilityBrain.SetRuntimeActive(true);
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
            _utilityBrain.SetRuntimeActive(false);
            ResetDispatcherCadence();
            ClearVoxelPathGuidance();
        }

        private void OnDestroy()
        {
            if (_dispatcherRegistered)
            {
                GlobalRegistry.UnregisterUpdatable(this, PriorityLayer.Environment);
                _dispatcherRegistered = false;
            }

            UnregisterSpatialHandle();
            ClearInfectionHazardRegistration();
            _utilityBrain.Dispose();
            ClearVoxelPathGuidance();
        }

        public void OnSpawn()
        {
            _isDead = false;
            _runtimeAggressionScale = 1f;
            ClearGeneticTraits();
            SetInfectedState(false, 0f);
            _currentHealth = _maxHealth;
            _utilityBrain.ResetRuntimeState(transform.position);
            _utilityBrain.SetRuntimeActive(true);
            ResetStateCache();
            _cognitionTimeSeconds = 0f;
            RefreshRuntimeEcosystemState();
            RegisterSpatialHandle();
            ResetDispatcherCadence();
            ClearProceduralStrikeIntent();
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
            _utilityBrain.SetRuntimeActive(false);
            ResetStateCache();
            _cognitionTimeSeconds = 0f;
            ClearInfectionHazardRegistration();
            UnregisterSpatialHandle();
            ResetDispatcherCadence();
            ClearProceduralStrikeIntent();
            ClearVoxelPathGuidance();
        }

        // ══════════════════════════════════════════════════════════
        //  TICK PIPELINE (Absolute Zero GC)
        // ══════════════════════════════════════════════════════════
        public void Tick(float dt)
        {
            if (dt <= 0f)
                return;

            _cognitionTimeSeconds += dt;
            bool forceAggroTick = ShouldForceAggroCognitionTick();
            if (_foveatedTickRate == FoveatedTickRate.CulledEcosystemOnly && !forceAggroTick)
            {
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                return;
            }

            _sensorSuite.Tick(dt, _rb.linearVelocity, _cognitionTimeSeconds, forceAggroTick);
            _lodDisabled = _sensorSuite.lodDisabled;

            if (_lodDisabled || _sensorSuite.isSleeping)
            {
                FixedTick(dt);
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                return;
            }

            AIState oldState = _currentStateCache;
            float3 selfPosition = transform.position;
            CreatureUtilityEvaluation utilityEvaluation = EvaluateCognitionBrain(Time.frameCount, dt, selfPosition, out Transform attackTarget);
            ApplyCognitionEvaluation(in utilityEvaluation);
            ApplyVoxelPathGuidance(selfPosition, utilityEvaluation.LegacyState);
            UpdateProceduralStrikeIntent(utilityEvaluation.LegacyState, attackTarget);
            UpdateProceduralHeadLookIntent();
            EmitLeviathanThreatPulse(in utilityEvaluation);
            if (utilityEvaluation.ShouldAttack && attackTarget != null)
            {
                HandleAttackPerform(attackTarget);
                float attackCooldown = _speciesProfile != null ? _speciesProfile.attackCooldown : 1f;
                _utilityBrain.NotifyAttackPerformed(_cognitionTimeSeconds, attackCooldown);
            }

            if (_currentStateCache != oldState)
            {
                OnStateChanged?.Invoke(_currentStateCache);
            }

            if (_sensorSuite.isAvoidingObstacle)
            {
                float3 blendedAvoidanceDirection = math.normalizesafe(
                    math.lerp((float3)_cachedDesiredDirection, (float3)_sensorSuite.bestFreeDirection, 0.7f),
                    (float3)_cachedDesiredDirection);
                _cachedDesiredDirection = (Vector3)blendedAvoidanceDirection;
                if (_sensorSuite.IsStuck && _sensorSuite.hasEscapePOI)
                {
                    float3 poiDir = math.normalizesafe((float3)_sensorSuite.currentEscapePOI - selfPosition, blendedAvoidanceDirection);
                    _cachedDesiredDirection = (Vector3)math.normalizesafe(math.lerp(blendedAvoidanceDirection, poiDir, 0.6f), poiDir);
                }
            }

            if (_animator != null &&
                _steeringEngine.maxSpeed > 0f &&
                (_proceduralLeviathanSpineIk == null || !_proceduralLeviathanSpineIk.isActiveAndEnabled))
            {
                float movementIntensity = _rb.linearVelocity.magnitude / _steeringEngine.maxSpeed;
                _animator.SetFloat(_HashSwimSpeed, movementIntensity);
            }

            // [REQ] Procedural Eye Tracking (The "Stare")
            UpdateEyeTracking(dt, (Vector3)selfPosition);
            FixedTick(dt);
            AdvanceSlowTickCadence(dt);
        }

        private void UpdateEyeTracking(float dt, Vector3 selfPosition)
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
                float distSqr = (targetPos - selfPosition).sqrMagnitude;
                if (distSqr < _speciesProfile.eyeTrackRange * _speciesProfile.eyeTrackRange)
                {
                    Vector3 toTarget = (targetPos - selfPosition).normalized;
                    // Apply LookAt weight
                    LookDirection = Vector3.Slerp(transform.forward, toTarget, _speciesProfile.eyeTrackWeight);
                    return;
                }
            }

            // Default to forward
            LookDirection = Vector3.Slerp(LookDirection, transform.forward, 5f * dt);
        }

        private CreatureUtilityEvaluation EvaluateCognitionBrain(
            int frameId,
            float dt,
            float3 selfPosition,
            out Transform attackTarget)
        {
            bool hasPlayerTarget = _sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 playerPosition);
            bool hasPlayerVelocity = _sensorSuite.TryGetPerceivedPlayerVelocity(out Vector3 playerVelocity);
            bool hasDirectPlayerTransform = _sensorSuite.TryGetDirectPlayerTransform(out Transform directPlayerTransform);
            bool hasThreatTarget = _sensorSuite.currentThreat != null;
            bool hasPreyTarget = _sensorSuite.currentPrey != null;
            bool hasScavengeTarget = _sensorSuite.currentScavengeTarget != null;
            float fearPressure01 = _sensorSuite.isThreatened ? 0.35f : 0f;
            bool hasHazardScatterDirection = _sensorSuite.isScattering;
            float3 scatterDirection = _sensorSuite.scatterDirection;
            if (hasThreatTarget)
                fearPressure01 += 0.2f;
            if (_utilityBrain.UsesPredatorRole)
            {
                HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
                if (vegetationBridge != null)
                {
                    int speciesId = _speciesProfile != null ? _speciesProfile.speciesID : 0;
                    fearPressure01 += vegetationBridge.SamplePredatorFearPressure(selfPosition, speciesId);
                }

                HazardZoneManager hazardZoneManager = HazardZoneManager.Instance;
                if (hazardZoneManager != null &&
                    hazardZoneManager.TrySampleHazardAvoidance((Vector3)selfPosition, PredatorHazardAvoidanceRadius, out Vector3 hazardFleeDirection, out float hazardPressure01))
                {
                    fearPressure01 += hazardPressure01;
                    if (hazardPressure01 > PredatorHazardFearThreshold)
                    {
                        hasHazardScatterDirection = true;
                        scatterDirection = hazardFleeDirection;
                    }
                }
            }

            float3 selfVelocity = _rb != null ? _rb.linearVelocity : float3.zero;
            float3 selfForward = transform.forward;
            float attackRange = _speciesProfile != null ? _speciesProfile.attackRadius : math.max(1f, _stateMachine.attackRadius);
            float wanderRadius = math.max(1f, _stateMachine.wanderRadius);
            float patrolRadius = math.max(1f, _stateMachine.patrolRadius);

            CreatureUtilityContext context = new CreatureUtilityContext(
                (Vector3)selfPosition,
                (Vector3)selfVelocity,
                (Vector3)selfForward,
                hasPlayerTarget ? playerPosition : default,
                hasPlayerVelocity ? playerVelocity : default,
                hasThreatTarget ? _sensorSuite.currentThreat.position : default,
                hasPreyTarget ? _sensorSuite.currentPrey.position : default,
                hasScavengeTarget ? _sensorSuite.currentScavengeTarget.position : default,
                _sensorSuite.flockCenter,
                _sensorSuite.flockDirection,
                _sensorSuite.flockAvoidance,
                scatterDirection,
                HealthNormalized,
                _sensorSuite.distSqrToPlayer,
                attackRange,
                math.saturate(fearPressure01),
                _stateMachine.escapeDistance,
                _stateMachine.escapeSafeDistance,
                wanderRadius,
                patrolRadius,
                math.saturate(_foveatedImportanceScore),
                _sensorSuite.flockCount,
                canFlee,
                _sensorSuite.hasVisualPlayerContact,
                hasPlayerTarget,
                hasThreatTarget,
                hasPreyTarget,
                hasScavengeTarget,
                _stateMachine.useTerritory,
                _stateMachine.isFlockingFish,
                hasHazardScatterDirection,
                isAggressive);

            CreatureUtilityEvaluation evaluation = _utilityBrain.Evaluate(frameId, dt, _cognitionTimeSeconds, in context);
            attackTarget = _sensorSuite.currentScavengeTarget ??
                           _sensorSuite.currentDistractor ??
                           directPlayerTransform ??
                           _sensorSuite.currentPrey;
            return evaluation;
        }

        private bool ShouldForceAggroCognitionTick()
        {
            if (_isDead || !_utilityBrain.IsActivePredator)
                return false;

            if (_sensorSuite.hasVisualPlayerContact || _sensorSuite.hasNoisePlayerContact)
                return true;

            PredatorUtilityState stateMask = _utilityBrain.CurrentStateMask;
            return stateMask == PredatorUtilityState.Stalking ||
                   stateMask == PredatorUtilityState.Attacking ||
                   _currentStateCache == AIState.Stalk ||
                   _currentStateCache == AIState.Aggressive;
        }

        private void ApplyVoxelPathGuidance(float3 selfPosition, AIState resolvedState)
        {
            if (!_utilityBrain.IsActivePredator ||
                _isDead ||
                (resolvedState != AIState.Stalk && resolvedState != AIState.Aggressive) ||
                !_sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 playerPosition))
            {
                ClearVoxelPathGuidance();
                return;
            }

            HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge == null)
            {
                ClearVoxelPathGuidance();
                return;
            }

            Vector3 targetPosition = ResolvePredictedPlayerGuidanceTarget(selfPosition, playerPosition);
            bool requiresRefresh = !_hasVoxelRouteTarget ||
                                   (_voxelRouteTargetPosition - targetPosition).sqrMagnitude > VoxelRouteRetargetDistanceSqr ||
                                   _cognitionTimeSeconds >= _nextVoxelRouteRefreshTime;
            if (requiresRefresh)
            {
                if (vegetationBridge.TryBuildImmediateAbyssalVoxelRoute((Vector3)selfPosition, targetPosition, _voxelRouteWaypoints, out int waypointCount))
                {
                    _voxelRouteWaypointCount = waypointCount;
                    _voxelRouteTargetPosition = targetPosition;
                    _hasVoxelRouteTarget = waypointCount >= 2;
                    _nextVoxelRouteRefreshTime = _cognitionTimeSeconds + VoxelRouteRefreshIntervalSeconds;
                }
                else
                {
                    ClearVoxelPathGuidance();
                    return;
                }
            }

            if (_voxelRouteWaypointCount < 2)
                return;

            int waypointIndex = 1;
            while (waypointIndex < _voxelRouteWaypointCount - 1 &&
                   (_voxelRouteWaypoints[waypointIndex] - (Vector3)selfPosition).sqrMagnitude <= VoxelRouteWaypointReachDistanceSqr)
            {
                waypointIndex++;
            }

            float3 toWaypoint = (float3)_voxelRouteWaypoints[waypointIndex] - selfPosition;
            if (math.lengthsq(toWaypoint) <= 0.0001f)
                return;

            _cachedDesiredDirection = math.normalizesafe(toWaypoint, (float3)_cachedDesiredDirection);
        }

        private Vector3 ResolvePredictedPlayerGuidanceTarget(float3 selfPosition, Vector3 playerPosition)
        {
            if (!_sensorSuite.TryGetPerceivedPlayerVelocity(out Vector3 playerVelocity))
                return playerPosition;

            float predatorSpeed = math.max(
                1f,
                math.max(_steeringEngine.maxSpeed, _rb != null ? _rb.linearVelocity.magnitude : 0f));
            float distance = math.distance(selfPosition, (float3)playerPosition);
            float interceptTime = math.clamp(distance / predatorSpeed, 0f, 3f);
            return playerPosition + (playerVelocity * interceptTime);
        }

        private void ClearVoxelPathGuidance()
        {
            _voxelRouteWaypointCount = 0;
            _hasVoxelRouteTarget = false;
            _nextVoxelRouteRefreshTime = 0f;
            _voxelRouteTargetPosition = default;
        }

        public void FixedTick(float fdt)
        {
            if (_spatialHandle != 0)
                WorldSpatialHashGrid.Refresh(_spatialHandle);
            if (_faunaSpatialHandle != 0)
                FaunaSpatialHashRegistry.Refresh(_faunaSpatialHandle);

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
            int whileWatchdog = 0;
            while (_slowTickAccumulator >= SlowTickIntervalSeconds &&
                   iterationCount < MaxSlowTicksPerDispatcherTick)
            {
                if (whileWatchdog++ > 10000)
                    break;

                _slowTickAccumulator -= SlowTickIntervalSeconds;
                SlowTick();
                iterationCount++;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (_slowTickAccumulator >= SlowTickIntervalSeconds &&
                iterationCount >= MaxSlowTicksPerDispatcherTick &&
                Time.time >= _nextSlowTickWatchdogLogTime)
            {
                _nextSlowTickWatchdogLogTime = Time.time + 5f;
                Debug.LogError("FaunaBrain slow-tick watchdog tripped. Cadence backlog was clamped.", this);
            }
#endif

            if (_slowTickAccumulator > SlowTickIntervalSeconds)
                _slowTickAccumulator = SlowTickIntervalSeconds;
        }

        private void ResetDispatcherCadence()
        {
            _slowTickAccumulator = 0f;
        }

        private void RegisterSpatialHandle()
        {
            if (!isActiveAndEnabled)
                return;

            if (_spatialHandle == 0)
                _spatialHandle = WorldSpatialHashGrid.RegisterBioform(this);

            if (_faunaSpatialHandle == 0)
                _faunaSpatialHandle = FaunaSpatialHashRegistry.RegisterBioform(this);
        }

        private void UnregisterSpatialHandle()
        {
            if (_spatialHandle != 0)
            {
                WorldSpatialHashGrid.Unregister(_spatialHandle);
                _spatialHandle = 0;
            }

            if (_faunaSpatialHandle != 0)
            {
                FaunaSpatialHashRegistry.Unregister(_faunaSpatialHandle);
                _faunaSpatialHandle = 0;
            }
        }

        private void EnsureLeviathanPresentationOwner()
        {
            if (!ShouldUseProceduralLeviathanPresentation())
                return;

            if (_proceduralLeviathanSpineIk == null)
                _proceduralLeviathanSpineIk = gameObject.AddComponent<ProceduralLeviathanSpineIK>();

            _proceduralLeviathanSpineIk.BindFromFauna(this, _rb, _animator);
        }

        private void UpdateProceduralStrikeIntent(AIState resolvedState, Transform strikeTarget)
        {
            if (_proceduralLeviathanSpineIk == null)
                return;

            bool strikeActive = resolvedState == AIState.Aggressive && strikeTarget != null && !_isDead;
            float strikeRange = _speciesProfile != null ? _speciesProfile.attackRadius : math.max(1f, _stateMachine.attackRadius);
            _proceduralLeviathanSpineIk.SetStrikeIntent(strikeTarget, strikeRange, strikeActive);
        }

        private void ClearProceduralStrikeIntent()
        {
            if (_proceduralLeviathanSpineIk == null)
                return;

            float strikeRange = _speciesProfile != null ? _speciesProfile.attackRadius : math.max(1f, _stateMachine.attackRadius);
            _proceduralLeviathanSpineIk.SetStrikeIntent(null, strikeRange, false);
            _proceduralLeviathanSpineIk.SetHeadLookTarget(default, false);
        }

        private void UpdateProceduralHeadLookIntent()
        {
            if (_proceduralLeviathanSpineIk == null)
                return;

            bool hasPlayerTarget = _sensorSuite.TryGetPerceivedPlayerPosition(out Vector3 playerPosition) && !_isDead;
            _proceduralLeviathanSpineIk.SetHeadLookTarget(playerPosition, hasPlayerTarget);
        }

        private void EmitLeviathanThreatPulse(in CreatureUtilityEvaluation evaluation)
        {
            if (!evaluation.EmitThreatPulse ||
                _isDead ||
                !ShouldUseProceduralLeviathanPresentation())
            {
                return;
            }

            SargassumMicroFaunaBoids boidSystem = SargassumMicroFaunaBoids.ActiveRuntimeInstance;
            if (boidSystem == null)
                return;

            Vector3 pulseDirection = _rb != null && _rb.linearVelocity.sqrMagnitude > 0.0001f
                ? _rb.linearVelocity.normalized
                : transform.forward;
            if (pulseDirection.sqrMagnitude <= 0.0001f)
                pulseDirection = Vector3.forward;

            boidSystem.RegisterLeviathanThreatPulse(
                transform.position,
                pulseDirection,
                40f,
                0.4f);
        }

        private bool ShouldUseProceduralLeviathanPresentation()
        {
            if (_speciesProfile != null && _speciesProfile.isLeviathan)
                return true;

            return _archetype != null && _archetype.roleType == CreatureRoleType.Leviathan;
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
                _utilityBrain.ForceSated(_cognitionTimeSeconds, satedDur);
                _stateMachine.currentState = AIState.Sated;
                _currentStateCache = AIState.Sated;
                
                // [REQ] SHOAL SCATTERING (Panic Pulse)
                // Trigger panic in all nearby prey within 10m
                int count = FaunaSpatialHashRegistry.CollectContactsNonAlloc(target.position, 10f, SpatialTargetKind.Bioform, _panicBuffer);
                for (int i = 0; i < count; i++)
                {
                    SpatialQueryHit panicHit = _panicBuffer[i];
                    Transform neighborTransform = panicHit.Transform;
                    if (neighborTransform == null ||
                        neighborTransform == target ||
                        !neighborTransform.CompareTag("Prey") ||
                        !(panicHit.Owner is FaunaBrain neighborBrain))
                    {
                        continue;
                    }

                    neighborBrain.TriggerPanicPulse(transform.position);
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

                IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
                if (ecosystemDirector != null && ecosystemDirector.IsInitialized)
                    ecosystemDirector.ReportPredation(target.position, 1);

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
                    if (target.TryGetComponent<MantaScooter>(out _))
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
            float normalizedDamage = _maxHealth > 0.001f ? Mathf.Max(0f, amount) / _maxHealth : 0f;
            _currentHealth = Mathf.Max(0f, _currentHealth - amount);
            if (_utilityBrain.UsesPredatorRole && normalizedDamage >= 0.3f)
            {
                HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
                int speciesId = _speciesProfile != null ? _speciesProfile.speciesID : 0;
                if (vegetationBridge != null && speciesId != 0)
                    vegetationBridge.RegisterPredatorFearNode(speciesId, transform.position, normalizedDamage);
            }
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
            float3 randomDirection = new float3(
                _runtimeRandom.NextFloat(-1f, 1f),
                _runtimeRandom.NextFloat(-1f, 1f),
                _runtimeRandom.NextFloat(-1f, 1f));
            Vector3 randomOffset = (Vector3)(math.normalizesafe(randomDirection, new float3(0f, 1f, 0f)) * 0.2f);
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

            _utilityBrain.ForceRetreat(threatPosition, _cognitionTimeSeconds, 8f);
            _stateMachine.currentState = AIState.Retreat;
            _currentStateCache = AIState.Retreat;
        }

        private void Die()
        {
            _isDead = true;
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Despawn(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void ApplyCognitionEvaluation(in CreatureUtilityEvaluation evaluation)
        {
            _cachedDesiredDirection = evaluation.DesiredDirection;
            _currentStateCache = evaluation.LegacyState;
            _stateMachine.currentState = evaluation.LegacyState;
            _stateMachine.currentForceMultiplier = evaluation.ForceMultiplier;
            _stateMachine.currentSpeedMultiplier = evaluation.SpeedMultiplier;
            _stateMachine.currentTurnMultiplier = evaluation.TurnMultiplier;
        }

        private void ResetStateCache()
        {
            AIState initialState = ResolveInitialState();
            _stateMachine.ResetRuntime(initialState);
            _currentStateCache = initialState;
            _cachedDesiredDirection = transform != null ? transform.forward : Vector3.forward;
            ClearVoxelPathGuidance();
        }

        private AIState ResolveInitialState()
        {
            if (_speciesProfile != null && _speciesProfile.isAmbusher)
                return AIState.Idle;

            return _stateMachine.isFlockingFish ? AIState.Flocking : AIState.Wander;
        }

        private Unity.Mathematics.Random CreateDeterministicRandom()
        {
            int speciesId = _speciesProfile != null ? _speciesProfile.speciesID : 0;
            uint ownerId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            uint seed = math.hash(new uint4(ownerId, unchecked((uint)speciesId), 0x7F4A7C15u, 0x3A9D2B71u));
            return new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
        }
    }
}
