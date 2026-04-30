using System;
using Hecton8.Caves;
using UnityEngine;
using UnityEngine.Serialization;
using Hecton8.Core;
using Hecton8.Gameplay;
using Hecton8.Interaction;
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
    public partial class FaunaBrain : MonoBehaviour, IUpdatable, ITickable, IFixedTickable, ISlowTickable, IPoolable, ISerializationCallbackReceiver, ICuttable
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
        [SerializeField] private FaunaDataTemplate _faunaDataTemplate;
        [SerializeField] private FaunaSensorSuite _sensorSuite = new FaunaSensorSuite();
        [SerializeField] private FaunaSteeringEngine _steeringEngine = new FaunaSteeringEngine();
        [SerializeField] private FaunaStateMachine _stateMachine = FaunaStateMachine.CreateDefault();

        public AIState CurrentState => _stateMachine.currentState;
        public FaunaSpeciesProfile SpeciesProfile => _speciesProfile;
        public FaunaDataTemplate DataTemplate => _faunaDataTemplate;
        public int SpeciesId => ComputeStableSpeciesId();
        public bool HasActiveApexIntimidation => _apexIntimidationUntilTime > _cognitionTimeSeconds;
        public bool IsFlankingManeuverDetected => _flankingManeuverDetected;
        /// <summary>
        /// True while this predator is publishing a false PDA distress-beacon signal.
        /// </summary>
        public bool HasActiveEcholocationMimicry => _mimicSignalActive;
        public uint ThreatPredictionLoreHash => _faunaDataTemplate != null ? _faunaDataTemplate.FullLoreHash : 0u;

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
        private ScannableTarget _scannableTarget;
        private PredatorPackRole _currentPackRole;
        private bool _flankingManeuverDetected;
        
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
        private const float DynamicDodgeDistanceScale = 2.25f;
        private const float DynamicDodgeForceMultiplier = 2.75f;
        private const float DynamicDodgeSpeedMultiplier = 1.3f;
        private const float DynamicDodgeTurnMultiplier = 3.25f;
        private const float WallSlideTurnMultiplier = 2.1f;
        private const float WallSlideSpeedMultiplier = 1.1f;
        private const float DamageFearPheromoneFloor = 0.85f;
        private const float DamageFearPheromoneBoost = 1.35f;
        private const float DamageFlinchVelocityFloor = 6f;
        private const float DamageFlinchVelocityCeiling = 18f;
        private const float HerbivoreSatedDurationSeconds = 16f;
        private const float CleanerFormationMinRadius = 1.6f;
        private const float CleanerFormationMaxRadius = 4.1f;
        private const float CleanerForwardBias = 0.45f;
        private const float CleanerVerticalBiasMin = -0.85f;
        private const float CleanerVerticalBiasMax = 1.15f;
        private const float DefaultApexTerritoryRadiusMeters = 500f;
        private const float DefaultApexIntimidationRadiusMeters = 100f;
        private const float DefaultApexIntimidationDurationSeconds = 24f;
        private const float DefaultApexForcedRetreatDurationSeconds = 18f;
        private const float ParentalDefenseIntensityThreshold = 0.1f;
        private const float DefaultEmpAttackRadiusMeters = 18f;
        private const float DefaultDazzleLockDurationSeconds = 0.35f;
        private const float FeedingObservationCooldownSeconds = 6f;
        private const float FeedingObservationRadiusMeters = 80f;
        private const float FeedingObservationRadiusMetersSqr = FeedingObservationRadiusMeters * FeedingObservationRadiusMeters;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static float _nextSlowTickWatchdogLogTime;
#endif
        
        // --- LOD & Stagger ---
        private bool _lodDisabled;
        private FaunaLogicalLodTier _logicalLodTier = FaunaLogicalLodTier.FullSim;
        private uint _uniqueInstanceUid;
        private Renderer _renderer;
        private Unity.Mathematics.Random _runtimeRandom;
        private int _tickStaggerShift;
        private Vector3 _cachedDesiredDirection;
        private AIState _currentStateCache;
        private Transform _currentCullingPlayerTransform;
        
        // --- Buffers ---
        private static readonly SpatialQueryHit[] _panicBuffer = new SpatialQueryHit[10];
        // COLD ALLOC: SpatialQueryHit[12] - reusable cleaner host lookup buffer over fauna spatial registry - owner: FaunaBrain
        private static readonly SpatialQueryHit[] _cleanerHostBuffer = new SpatialQueryHit[12];
        // COLD ALLOC: SpatialQueryHit[16] - reusable apex rivalry and intimidation lookup buffer over fauna spatial registry - owner: FaunaBrain
        private static readonly SpatialQueryHit[] _apexContactBuffer = new SpatialQueryHit[16];
        // COLD ALLOC: SpatialQueryHit[16] - reusable same-species parental-defense response lookup buffer - owner: FaunaBrain
        private static readonly SpatialQueryHit[] _parentalDefenseBuffer = new SpatialQueryHit[16];
        // COLD ALLOC: Vector3[16] - reusable 3D cave-voxel guidance route for predator steering - owner: FaunaBrain
        private readonly Vector3[] _voxelRouteWaypoints = new Vector3[MaxVoxelRouteWaypointCount];

        // --- Event Hooks ---
        public Action<AIState> OnStateChanged;
        
        [Header("── Audio Hooks ─────────────────────────────────")]
        [Tooltip("Triggered when a Panic Pulse occurs. Hook audio agents here for zero-GC sound dispatch.")]
        public UnityEngine.Events.UnityEvent OnPanicTriggered;
        public UnityEngine.Events.UnityEvent OnBurrowBreach;

        private float _slowTickAccumulator;
        private int _voxelRouteWaypointCount;
        private float _nextVoxelRouteRefreshTime;
        private Vector3 _voxelRouteTargetPosition;
        private bool _hasVoxelRouteTarget;
        private Transform _apexRivalTarget;
        private Transform _baitFeedingTarget;
        private Vector3 _forcedMigrationTarget;
        private float _apexIntimidationUntilTime;
        private float _forcedMigrationUntilTime;
        private float _nextBurrowBreachTime;
        private float _nextBestiaryObservationTime;
        private float _nextMimicPingTime;
        private float _mimicPingExpireTime;
        private bool _hasForcedMigrationTarget;
        private bool _mimicSignalActive;
        private uint _cachedScanEntryHash;

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
            if (!ValidatePrimitiveColliderRig())
            {
                enabled = false;
                return;
            }

            _renderer = Hecton8.Core.ComponentReferenceUtility.ResolveOwnedComponent<Renderer>(transform);
            TryGetComponent(out _animator);
            TryGetComponent(out _proceduralLeviathanSpineIk);
            TryGetComponent(out _scannableTarget);
            ResolveFoveatedBindings();
            _runtimeRandom = CreateDeterministicRandom();
            _tickStaggerShift = _runtimeRandom.NextInt(0, 10);

            // Inject profile into subsystems
            _steeringEngine.Init(_rb, transform, _speciesProfile);
            _sensorSuite.Init(this, transform, _speciesProfile);
            _utilityBrain.Initialize(transform.position, _speciesProfile, _archetype, _faunaDataTemplate);
            if (_archetype != null)
                ApplyArchetype(_archetype);
            else if (_faunaDataTemplate != null)
                ApplyFaunaDataTemplate(_faunaDataTemplate);
            ConfigureFaunaScanMetadata();
            ResetStateCache();
            _cognitionTimeSeconds = 0f;
            EnsureLeviathanPresentationOwner();
        }

        private void OnEnable()
        {
            if (!Application.isPlaying)
                return;

            if (GlobalRegistry.Dispatcher == null)
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
            ClearEcholocationMimicSignal();
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
            ClearEcholocationMimicSignal();
        }

        public void OnSpawn()
        {
            _isDead = false;
            _logicalLodTier = FaunaLogicalLodTier.FullSim;
            _runtimeAggressionScale = 1f;
            ClearGeneticTraits();
            SetInfectedState(false, 0f);
            _currentHealth = _maxHealth;
            _utilityBrain.ResetRuntimeState(transform.position);
            _utilityBrain.SetRuntimeActive(true);
            ResetStateCache();
            _cognitionTimeSeconds = 0f;
            ConfigureFaunaScanMetadata();
            RefreshRuntimeEcosystemState();
            RegisterSpatialHandle();
            ResetDispatcherCadence();
            ClearProceduralStrikeIntent();
        }

        public void OnDespawn()
        {
            _isDead = true;
            _logicalLodTier = FaunaLogicalLodTier.Hibernating;
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
            ClearEcholocationMimicSignal();
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
            ResolveLogicalLodTier();
            if (_logicalLodTier != FaunaLogicalLodTier.FullSim && !forceAggroTick)
            {
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                ClearEcholocationMimicSignal();
                if (_logicalLodTier == FaunaLogicalLodTier.DataOnly && _rb != null && !_rb.IsSleeping())
                    _rb.Sleep();

                return;
            }

            if (_foveatedTickRate == FoveatedTickRate.CulledEcosystemOnly && !forceAggroTick)
            {
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                ClearEcholocationMimicSignal();
                return;
            }

            _sensorSuite.Tick(dt, _rb.linearVelocity, _cognitionTimeSeconds, forceAggroTick);
            _lodDisabled = _sensorSuite.lodDisabled;

            if (_lodDisabled || _sensorSuite.isSleeping)
            {
                FixedTick(dt);
                AdvanceSlowTickCadence(dt);
                ClearProceduralStrikeIntent();
                ClearEcholocationMimicSignal();
                return;
            }

            AIState oldState = _currentStateCache;
            float3 selfPosition = transform.position;
            CreatureUtilityEvaluation utilityEvaluation = EvaluateCognitionBrain(Time.frameCount, dt, selfPosition, out Transform attackTarget);
            ApplyCognitionEvaluation(in utilityEvaluation);
            ApplyVoxelPathGuidance(selfPosition, utilityEvaluation.LegacyState);
            bool ecologyOverrideActive = ApplyEcologyChainOverrides(selfPosition, dt);
            if (ecologyOverrideActive)
                attackTarget = null;

            UpdateBioluminescentHypnosis();
            UpdateEcholocationMimicry();
            UpdateProceduralStrikeIntent(_currentStateCache, attackTarget);
            UpdateProceduralHeadLookIntent();
            EmitLeviathanThreatPulse(in utilityEvaluation);
            if (!ecologyOverrideActive && utilityEvaluation.ShouldAttack && attackTarget != null)
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
                    int speciesId = ComputeStableSpeciesId();
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
            bool isApexPredator = IsApexPredator();
            float apexTerritoryRadius = ResolveApexTerritoryRadius();
            float apexAggressionMultiplier = ResolveApexAggressionMultiplier();
            bool hasApexRivalTarget = false;
            Vector3 apexRivalPosition = default;
            _apexRivalTarget = null;
            if (isApexPredator &&
                TryResolveNearestRivalApex(selfPosition, apexTerritoryRadius, out FaunaBrain rivalBrain, out Vector3 rivalPosition))
            {
                _apexRivalTarget = rivalBrain != null ? rivalBrain.transform : null;
                if (_apexRivalTarget != null)
                {
                    hasThreatTarget = true;
                    hasApexRivalTarget = true;
                    apexRivalPosition = rivalPosition;
                }
            }
            else if (!hasThreatTarget &&
                     TryResolveApexIntimidationThreat(selfPosition, out Vector3 intimidationThreatPosition))
            {
                hasThreatTarget = true;
                fearPressure01 += 0.2f;
                scatterDirection = ((Vector3)selfPosition - intimidationThreatPosition).normalized;
                hasHazardScatterDirection = math.lengthsq(scatterDirection) > 0.0001f;
            }

            CreatureUtilityContext context = new CreatureUtilityContext(
                (Vector3)selfPosition,
                (Vector3)selfVelocity,
                (Vector3)selfForward,
                hasPlayerTarget ? playerPosition : default,
                hasDirectPlayerTransform ? directPlayerTransform.forward : selfForward,
                hasPlayerVelocity ? playerVelocity : default,
                hasThreatTarget
                    ? (hasApexRivalTarget
                        ? apexRivalPosition
                        : (_sensorSuite.currentThreat != null ? _sensorSuite.currentThreat.position : default))
                    : default,
                hasApexRivalTarget ? apexRivalPosition : default,
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
                ResolveFleeHealthThreshold(),
                _stateMachine.escapeDistance,
                _stateMachine.escapeSafeDistance,
                wanderRadius,
                patrolRadius,
                apexTerritoryRadius,
                apexAggressionMultiplier,
                math.saturate(_foveatedImportanceScore),
                _sensorSuite.flockCount,
                canFlee,
                _sensorSuite.hasVisualPlayerContact,
                hasPlayerTarget,
                hasThreatTarget,
                hasApexRivalTarget,
                hasPreyTarget,
                hasScavengeTarget,
                _stateMachine.useTerritory,
                _stateMachine.isFlockingFish,
                hasHazardScatterDirection,
                isAggressive,
                isApexPredator);

            CreatureUtilityEvaluation evaluation = _utilityBrain.Evaluate(frameId, dt, _cognitionTimeSeconds, in context);
            attackTarget = _apexRivalTarget ??
                           _sensorSuite.currentScavengeTarget ??
                           _baitFeedingTarget ??
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

            if (TryApplyBurrowAmbushPathGuidance(selfPosition, playerPosition))
                return;

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

        private bool TryApplyBurrowAmbushPathGuidance(float3 selfPosition, Vector3 playerPosition)
        {
            if (_faunaDataTemplate == null ||
                !_faunaDataTemplate.CanBurrowAmbush ||
                HectonVoxelEngine.ActiveRuntimeInstance == null ||
                !HectonVoxelEngine.ActiveRuntimeInstance.TryGetNearestActiveVolume(playerPosition, out HectonVoxelVolume volume) ||
                volume == null)
            {
                return false;
            }

            if (!volume.TryResolveBurrowAmbushRoute(
                    (Vector3)selfPosition,
                    playerPosition,
                    _faunaDataTemplate.BurrowSeabedTriggerDistanceMeters,
                    _faunaDataTemplate.BurrowBreachDistanceMeters,
                    out Vector3 solidAnchorWorldPosition,
                    out Vector3 breachWorldPosition))
            {
                return false;
            }

            _voxelRouteWaypoints[0] = (Vector3)selfPosition;
            _voxelRouteWaypoints[1] = solidAnchorWorldPosition;
            _voxelRouteWaypoints[2] = breachWorldPosition;
            _voxelRouteWaypointCount = 3;
            _voxelRouteTargetPosition = breachWorldPosition;
            _hasVoxelRouteTarget = true;
            _nextVoxelRouteRefreshTime = _cognitionTimeSeconds + VoxelRouteRefreshIntervalSeconds;

            Vector3 guidePoint = (solidAnchorWorldPosition - (Vector3)selfPosition).sqrMagnitude > VoxelRouteWaypointReachDistanceSqr
                ? solidAnchorWorldPosition
                : breachWorldPosition;
            _cachedDesiredDirection = (Vector3)math.normalizesafe((float3)(guidePoint - (Vector3)selfPosition), (float3)_cachedDesiredDirection);

            if (_cognitionTimeSeconds >= _nextBurrowBreachTime)
                TryTriggerBurrowAmbushGrab(playerPosition, breachWorldPosition);

            return true;
        }

        private void TryTriggerBurrowAmbushGrab(Vector3 playerPosition, Vector3 breachWorldPosition)
        {
            if (_faunaDataTemplate == null ||
                _cognitionTimeSeconds < _nextBurrowBreachTime ||
                (breachWorldPosition - playerPosition).sqrMagnitude >
                (_faunaDataTemplate.BurrowBreachDistanceMeters * _faunaDataTemplate.BurrowBreachDistanceMeters))
            {
                return;
            }

            if (!PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                runtimeContext.PlayerMovement == null)
            {
                return;
            }

            runtimeContext.PlayerMovement.ApplyFaunaHypnosisPull(
                breachWorldPosition,
                _faunaDataTemplate.BurrowPullAcceleration,
                _faunaDataTemplate.BurrowLockDurationSeconds);
            _nextBurrowBreachTime = _cognitionTimeSeconds + math.max(2f, _faunaDataTemplate.BurrowLockDurationSeconds);
            OnBurrowBreach?.Invoke();
        }

        private void UpdateBioluminescentHypnosis()
        {
            if (_isDead ||
                _faunaDataTemplate == null ||
                !_faunaDataTemplate.CanDazzleHypnotize ||
                !PlayerRuntimeContextService.TryGetActiveRuntimeContext(out PlayerRuntimeContext runtimeContext) ||
                runtimeContext == null ||
                runtimeContext.PlayerMovement == null ||
                runtimeContext.PlayerCamera == null)
            {
                return;
            }

            Vector3 toFauna = transform.position - runtimeContext.PlayerCamera.transform.position;
            float maxRange = _faunaDataTemplate.DazzleRangeMeters;
            if (toFauna.sqrMagnitude > maxRange * maxRange)
                return;

            Vector3 lookDirection = runtimeContext.PlayerCamera.transform.forward;
            float gazeDot = Vector3.Dot(lookDirection.normalized, toFauna.normalized);
            if (gazeDot < _faunaDataTemplate.DazzleLookDotThreshold)
                return;

            runtimeContext.PlayerMovement.ApplyFaunaHypnosisPull(
                transform.position,
                _faunaDataTemplate.DazzlePullAcceleration,
                DefaultDazzleLockDurationSeconds);
        }

        private void TryDispatchEmpAttack(Transform target)
        {
            if (_faunaDataTemplate == null ||
                target == null ||
                (!SupportsAttackPattern(FaunaAttackPattern.SonicPulse) && !SupportsAttackPattern(FaunaAttackPattern.Emp)))
            {
                return;
            }

            float radiusMeters = _sensorSuite != null
                ? Mathf.Max(DefaultEmpAttackRadiusMeters, _sensorSuite.aggroDistance * 0.45f)
                : DefaultEmpAttackRadiusMeters;
            PhysicsEventBus.NotifyElectromagneticPulse(new ElectromagneticPulseEvent(
                transform.position,
                radiusMeters,
                _faunaDataTemplate.EmpBlindDurationSeconds,
                _faunaDataTemplate.EmpClaritySuppression01,
                (uint)DamageTypeMask.Emp,
                DamageSourceIds.FaunaEmp));
        }

        private void UpdateEcholocationMimicry()
        {
            if (_isDead ||
                !ShouldUseEcholocationMimicry() ||
                !TryResolveMimicPlayerPosition(out Vector3 playerPosition) ||
                _currentStateCache == AIState.Retreat ||
                _currentStateCache == AIState.Sated)
            {
                ClearEcholocationMimicSignal();
                return;
            }

            Vector3 selfPosition = transform.position;
            float vanishDistance = _faunaDataTemplate.MimicPingVanishDistanceMeters;
            float vanishDistanceSqr = vanishDistance * vanishDistance;
            float playerDistanceSqr = (playerPosition - selfPosition).sqrMagnitude;

            if (_mimicSignalActive)
            {
                if (playerDistanceSqr <= vanishDistanceSqr)
                {
                    CommitEcholocationMimicAmbush(playerPosition);
                    _nextMimicPingTime = _cognitionTimeSeconds + _faunaDataTemplate.MimicPingCooldownSeconds;
                    ClearEcholocationMimicSignal();
                    return;
                }

                if (_cognitionTimeSeconds >= _mimicPingExpireTime || _currentStateCache == AIState.Aggressive)
                {
                    ClearEcholocationMimicSignal();
                    return;
                }

                return;
            }

            if (_currentStateCache == AIState.Aggressive || _cognitionTimeSeconds < _nextMimicPingTime)
                return;

            float pingRadius = _faunaDataTemplate.MimicPingRadiusMeters;
            if (playerDistanceSqr <= vanishDistanceSqr || playerDistanceSqr > pingRadius * pingRadius)
                return;

            EmitEcholocationMimicPing(selfPosition);
        }

        private bool ShouldUseEcholocationMimicry()
        {
            if (_faunaDataTemplate == null || !_faunaDataTemplate.CanEmitMimicDistressPing)
                return false;

            return IsApexPredator() || _faunaDataTemplate.FoodChainTier == FaunaFoodChainTier.Leviathan;
        }

        private bool TryResolveMimicPlayerPosition(out Vector3 playerPosition)
        {
            if (_sensorSuite.TryGetPerceivedPlayerPosition(out playerPosition))
                return true;

            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _currentCullingPlayerTransform);
            if (_currentCullingPlayerTransform == null)
            {
                playerPosition = default;
                return false;
            }

            playerPosition = _currentCullingPlayerTransform.position;
            return true;
        }

        private void EmitEcholocationMimicPing(Vector3 selfPosition)
        {
            if (_faunaDataTemplate == null)
                return;

            _mimicSignalActive = true;
            _mimicPingExpireTime = _cognitionTimeSeconds + _faunaDataTemplate.MimicPingLifetimeSeconds;
            _nextMimicPingTime = _cognitionTimeSeconds + _faunaDataTemplate.MimicPingCooldownSeconds;

            PhysicsEventBus.NotifyAcousticPing(new AcousticPingEvent(
                selfPosition,
                _faunaDataTemplate.MimicPingRadiusMeters,
                _faunaDataTemplate.MimicPingIntensity01,
                _faunaDataTemplate.MimicPingLifetimeSeconds,
                FieldTargetRole.DistressBeacon,
                ComputeStableSpeciesId()));
        }

        private void CommitEcholocationMimicAmbush(Vector3 playerPosition)
        {
            Vector3 attackDirection = playerPosition - transform.position;
            if (attackDirection.sqrMagnitude <= 0.0001f)
                attackDirection = transform.forward;
            else
                attackDirection.Normalize();

            _cachedDesiredDirection = attackDirection;
            _utilityBrain.RecordAuditoryStimulus(playerPosition, _cognitionTimeSeconds);
            _utilityBrain.ApplyExternalState(AIState.Aggressive, _cognitionTimeSeconds);
            _stateMachine.currentState = AIState.Aggressive;
            _currentStateCache = AIState.Aggressive;
        }

        private void ClearEcholocationMimicSignal()
        {
            if (_mimicSignalActive)
                WorldSpatialHashGrid.ClearTransientSignal(FieldTargetRole.DistressBeacon, ComputeStableSpeciesId());
            _mimicSignalActive = false;
            _mimicPingExpireTime = 0f;
        }

        private void EmitParentalDefenseSignal(Vector3 sourcePosition, float normalizedDamage)
        {
            if (_faunaDataTemplate == null ||
                !_faunaDataTemplate.EmitsParentalDefenseSignal ||
                normalizedDamage < ParentalDefenseIntensityThreshold)
            {
                return;
            }

            int speciesId = ComputeStableSpeciesId();
            if (speciesId == 0)
                return;

            ChemicalInfluenceGrid.QueueFearPheromone(sourcePosition, Mathf.Clamp01(normalizedDamage));

            int hitCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                sourcePosition,
                _faunaDataTemplate.ParentalDefenseRadiusMeters,
                SpatialTargetKind.Bioform,
                _parentalDefenseBuffer);
            for (int i = 0; i < hitCount; i++)
            {
                if (!(_parentalDefenseBuffer[i].Owner is FaunaBrain alliedBrain) ||
                    ReferenceEquals(alliedBrain, this) ||
                    alliedBrain.IsDead ||
                    alliedBrain.ComputeStableSpeciesId() != speciesId ||
                    alliedBrain._faunaDataTemplate == null ||
                    !alliedBrain._faunaDataTemplate.RespondsToParentalDefenseSignal)
                {
                    continue;
                }

                alliedBrain._utilityBrain.ApplyExternalState(AIState.Aggressive, alliedBrain._cognitionTimeSeconds);
                alliedBrain._currentStateCache = AIState.Aggressive;
                alliedBrain._stateMachine.currentState = AIState.Aggressive;
                alliedBrain._utilityBrain.RecordAuditoryStimulus(sourcePosition, alliedBrain._cognitionTimeSeconds);
            }
        }

        private bool ApplyEcologyChainOverrides(float3 selfPosition, float dt)
        {
            _baitFeedingTarget = null;
            EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
            if (ecosystemDirector == null)
                return false;

            if (TryApplyForcedMigrationOverride(ecosystemDirector, selfPosition))
                return true;

            if (TryApplyCorpseScavengingOverride(ecosystemDirector, selfPosition, dt))
                return true;

            if (TryApplyBaitFeedingOverride(ecosystemDirector, selfPosition))
                return true;

            if (TryApplyHerbivoreGrazingOverride(ecosystemDirector, selfPosition))
                return true;

            return TryApplyCleanerHostOverride(ecosystemDirector, selfPosition, dt);
        }

        private bool TryApplyForcedMigrationOverride(EcosystemDirector ecosystemDirector, float3 selfPosition)
        {
            if (!_hasForcedMigrationTarget || _cognitionTimeSeconds > _forcedMigrationUntilTime)
            {
                _hasForcedMigrationTarget = false;
                return false;
            }

            ApplyDirectedStateOverride(selfPosition, _forcedMigrationTarget, AIState.Retreat);
            return true;
        }

        private bool TryApplyCorpseScavengingOverride(EcosystemDirector ecosystemDirector, float3 selfPosition, float dt)
        {
            if (_speciesProfile == null ||
                !_speciesProfile.isScavenger ||
                _utilityBrain.HungerScore < ecosystemDirector.ScavengerHungerThreshold)
            {
                return false;
            }

            Vector3 selfWorldPosition = selfPosition;
            if (!ecosystemDirector.TryResolveCorpseScavengeTarget(selfWorldPosition, out Vector3 corpsePosition, out uint corpseNodeId))
                return false;

            _baitFeedingTarget = null;
            float consumeDistance = ecosystemDirector.ScavengerConsumeDistanceMeters;
            if ((corpsePosition - selfWorldPosition).sqrMagnitude <= consumeDistance * consumeDistance &&
                ecosystemDirector.TryConsumeCorpseScavengeTarget(corpseNodeId, ecosystemDirector.ScavengerConsumeUnitsPerSecond * dt))
            {
                _utilityBrain.ForceSated(_cognitionTimeSeconds, HerbivoreSatedDurationSeconds);
                TryReportFaunaFeedingObservation();
                ApplyDirectedStateOverride(selfPosition, corpsePosition, AIState.Sated);
                return true;
            }

            ApplyDirectedStateOverride(selfPosition, corpsePosition, AIState.Wander);
            return true;
        }

        private bool TryApplyBaitFeedingOverride(EcosystemDirector ecosystemDirector, float3 selfPosition)
        {
            _baitFeedingTarget = null;
            if (!ecosystemDirector.DoesSpeciesRespondToBait(this) ||
                _sensorSuite.currentScavengeTarget == null ||
                !_sensorSuite.currentScavengeTarget.TryGetComponent(out PickupItem pickupItem) ||
                !pickupItem.IsFaunaBait)
            {
                return false;
            }

            _baitFeedingTarget = _sensorSuite.currentScavengeTarget;
            Vector3 baitPosition = _sensorSuite.currentScavengeTarget.position;
            float consumeDistance = ecosystemDirector.BaitFeedingDistanceMeters;
            if ((baitPosition - (Vector3)selfPosition).sqrMagnitude <= consumeDistance * consumeDistance)
            {
                _utilityBrain.ForceSated(_cognitionTimeSeconds, HerbivoreSatedDurationSeconds);
                ApplyDirectedStateOverride(selfPosition, baitPosition, AIState.Sated);
                return true;
            }

            ApplyDirectedStateOverride(selfPosition, baitPosition, AIState.Investigate);
            return true;
        }

        private bool TryApplyHerbivoreGrazingOverride(EcosystemDirector ecosystemDirector, float3 selfPosition)
        {
            int speciesId = ComputeStableSpeciesId();
            if (!ecosystemDirector.IsHerbivoreSpecies(speciesId) ||
                _utilityBrain.HungerScore < ecosystemDirector.HerbivoreGrazeHungerThreshold)
            {
                return false;
            }

            Vector3 selfWorldPosition = selfPosition;
            if (ecosystemDirector.TryResolveHerbivoreGrazeTarget(selfWorldPosition, out Vector3 floraPosition, out uint floraInstanceUid))
            {
                float consumeDistanceMeters = ecosystemDirector.HerbivoreConsumeDistanceMeters;
                if ((floraPosition - selfWorldPosition).sqrMagnitude <= consumeDistanceMeters * consumeDistanceMeters &&
                    ecosystemDirector.TryConsumeHerbivoreGrazeTarget(floraInstanceUid))
                {
                    _utilityBrain.ForceSated(_cognitionTimeSeconds, HerbivoreSatedDurationSeconds);
                    TryReportFaunaFeedingObservation();
                    ApplyDirectedStateOverride(selfPosition, selfWorldPosition + transform.forward, AIState.Sated);
                    return true;
                }

                ApplyDirectedStateOverride(selfPosition, floraPosition, AIState.Wander);
                return true;
            }

            if (ecosystemDirector.TryResolveMigrationTarget(speciesId, selfWorldPosition, out Vector3 migrationTarget))
            {
                ApplyDirectedStateOverride(selfPosition, migrationTarget, AIState.Return);
                return true;
            }

            return false;
        }

        private bool TryApplyCleanerHostOverride(EcosystemDirector ecosystemDirector, float3 selfPosition, float dt)
        {
            if (!ecosystemDirector.IsCleanerSpecies(ComputeStableSpeciesId()))
                return false;

            int hostCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                selfPosition,
                ecosystemDirector.CleanerHostSearchRadiusMeters,
                SpatialTargetKind.Bioform,
                _cleanerHostBuffer);
            FaunaBrain bestHost = null;
            float bestDistanceSq = float.MaxValue;
            Vector3 bestHostPosition = default;
            for (int i = 0; i < hostCount; i++)
            {
                SpatialQueryHit hit = _cleanerHostBuffer[i];
                FaunaBrain hostBrain = hit.Owner as FaunaBrain;
                if (hostBrain == null ||
                    hostBrain == this ||
                    hostBrain._isDead ||
                    !ecosystemDirector.IsCleanerHostSpecies(hostBrain))
                {
                    continue;
                }

                if (hit.DistanceSqr >= bestDistanceSq)
                    continue;

                bestDistanceSq = hit.DistanceSqr;
                bestHost = hostBrain;
                bestHostPosition = hit.Position;
            }

            if (bestHost == null)
                return false;

            Vector3 cleanerTarget = bestHostPosition + ResolveCleanerCompanionOffset(bestHost);
            ApplyDirectedStateOverride(selfPosition, cleanerTarget, AIState.Flocking);
            float symbiosisDistanceMeters = ecosystemDirector.CleanerSymbiosisDistanceMeters;
            if ((cleanerTarget - (Vector3)selfPosition).sqrMagnitude <= symbiosisDistanceMeters * symbiosisDistanceMeters)
                bestHost.ApplyCleanerSymbiosis(ecosystemDirector.CleanerFatigueReliefPerSecond * dt);

            return true;
        }

        private void ApplyDirectedStateOverride(float3 selfPosition, Vector3 targetPosition, AIState state)
        {
            float3 desiredDirection = (float3)targetPosition - selfPosition;
            if (math.lengthsq(desiredDirection) > 0.0001f)
                _cachedDesiredDirection = math.normalizesafe(desiredDirection, (float3)_cachedDesiredDirection);

            _currentStateCache = state;
            _stateMachine.currentState = state;
        }

        private Vector3 ResolveCleanerCompanionOffset(FaunaBrain hostBrain)
        {
            uint seed = _uniqueInstanceUid != 0u
                ? _uniqueInstanceUid
                : (uint)(ComputeStableSpeciesId() * 73856093);
            seed ^= (uint)(hostBrain != null ? hostBrain.SpeciesId * 19349663 : 0);
            float radius01 = ((seed >> 8) & 0xFFu) / 255f;
            float vertical01 = ((seed >> 16) & 0xFFu) / 255f;
            float angleRadians = (seed & 0xFFu) * (math.PI * 2f / 255f);
            float radius = math.lerp(CleanerFormationMinRadius, CleanerFormationMaxRadius, radius01);
            float verticalOffset = math.lerp(CleanerVerticalBiasMin, CleanerVerticalBiasMax, vertical01);

            Vector3 hostForward = hostBrain != null ? hostBrain.transform.forward : Vector3.forward;
            if (hostForward.sqrMagnitude <= 0.0001f)
                hostForward = Vector3.forward;
            hostForward.Normalize();

            Vector3 hostRight = Vector3.Cross(Vector3.up, hostForward);
            if (hostRight.sqrMagnitude <= 0.0001f)
                hostRight = Vector3.right;
            else
                hostRight.Normalize();

            Vector3 lateralOffset = (hostRight * Mathf.Cos(angleRadians) * radius) +
                                    (hostForward * Mathf.Sin(angleRadians) * radius * CleanerForwardBias);
            return lateralOffset + (Vector3.up * verticalOffset);
        }

        private bool IsApexPredator()
        {
            return (_speciesProfile != null && _speciesProfile.isLeviathan) ||
                   (_archetype != null && _archetype.roleType == CreatureRoleType.Leviathan);
        }

        private float ResolveApexTerritoryRadius()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? Mathf.Max(25f, profile.territoryRadiusMeters)
                : DefaultApexTerritoryRadiusMeters;
        }

        private float ResolveApexAggressionMultiplier()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? Mathf.Max(1f, profile.aggressionMultiplierAgainstRivals)
                : 1.35f;
        }

        private float ResolveApexIntimidationRadius()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? Mathf.Max(1f, profile.intimidationRadiusMeters)
                : DefaultApexIntimidationRadiusMeters;
        }

        private float ResolveApexIntimidationDuration()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? Mathf.Max(1f, profile.intimidationDurationSeconds)
                : DefaultApexIntimidationDurationSeconds;
        }

        private float ResolveApexForcedRetreatDuration()
        {
            ApexTerritoryProfile profile = _speciesProfile != null ? _speciesProfile.apexTerritoryProfile : null;
            return profile != null
                ? Mathf.Max(1f, profile.forcedRetreatDurationSeconds)
                : DefaultApexForcedRetreatDurationSeconds;
        }

        private bool TryResolveNearestRivalApex(float3 selfPosition, float searchRadius, out FaunaBrain rivalBrain, out Vector3 rivalPosition)
        {
            rivalBrain = null;
            rivalPosition = default;
            if (!IsApexPredator())
                return false;

            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                selfPosition,
                searchRadius,
                SpatialTargetKind.Bioform,
                _apexContactBuffer);
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < contactCount; i++)
            {
                SpatialQueryHit hit = _apexContactBuffer[i];
                FaunaBrain candidate = hit.Owner as FaunaBrain;
                if (candidate == null ||
                    candidate == this ||
                    candidate._isDead ||
                    !candidate.IsApexPredator())
                {
                    continue;
                }

                if (hit.DistanceSqr >= bestDistanceSq)
                    continue;

                bestDistanceSq = hit.DistanceSqr;
                rivalBrain = candidate;
                rivalPosition = hit.Position;
            }

            return rivalBrain != null;
        }

        private bool TryResolveApexIntimidationThreat(float3 selfPosition, out Vector3 threatPosition)
        {
            threatPosition = default;
            if (IsApexPredator())
                return false;

            int contactCount = FaunaSpatialHashRegistry.CollectContactsNonAlloc(
                selfPosition,
                DefaultApexIntimidationRadiusMeters,
                SpatialTargetKind.Bioform,
                _apexContactBuffer);
            float bestDistanceSq = float.MaxValue;
            for (int i = 0; i < contactCount; i++)
            {
                SpatialQueryHit hit = _apexContactBuffer[i];
                FaunaBrain candidate = hit.Owner as FaunaBrain;
                if (candidate == null ||
                    candidate == this ||
                    candidate._isDead ||
                    !candidate.HasActiveApexIntimidation)
                {
                    continue;
                }

                float intimidationRadius = candidate.ResolveApexIntimidationRadius();
                if (hit.DistanceSqr > intimidationRadius * intimidationRadius || hit.DistanceSqr >= bestDistanceSq)
                    continue;

                bestDistanceSq = hit.DistanceSqr;
                threatPosition = hit.Position;
            }

            return bestDistanceSq < float.MaxValue;
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
            Vector3 desiredDirection = _cachedDesiredDirection;
            float forceMultiplier = _stateMachine.currentForceMultiplier;
            float speedMultiplier = _stateMachine.currentSpeedMultiplier * runtimeSpeedScale;
            float turnMultiplier = _stateMachine.currentTurnMultiplier;
            if (TryResolveDynamicDodgeDirection(desiredDirection, out Vector3 dodgeDirection))
            {
                desiredDirection = dodgeDirection;
                forceMultiplier = Mathf.Max(forceMultiplier, DynamicDodgeForceMultiplier);
                speedMultiplier = Mathf.Max(speedMultiplier, DynamicDodgeSpeedMultiplier);
                turnMultiplier = Mathf.Max(turnMultiplier, DynamicDodgeTurnMultiplier);
            }

            if (TryResolveWallSlideDirection(desiredDirection, out Vector3 slideDirection))
            {
                desiredDirection = slideDirection;
                speedMultiplier = Mathf.Max(speedMultiplier, WallSlideSpeedMultiplier);
                turnMultiplier = Mathf.Max(turnMultiplier, WallSlideTurnMultiplier);
            }
            
            _steeringEngine.FixedTick(
                fdt, 
                desiredDirection, 
                forceMultiplier, 
                speedMultiplier,
                turnMultiplier,
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
            ClearEcholocationMimicSignal();

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

            if (!TryGetComponent(out Hecton8.AI.CreatureDamageManager creatureDamageManager))
                creatureDamageManager = gameObject.AddComponent<Hecton8.AI.CreatureDamageManager>();

            creatureDamageManager.BindFromFauna(this);
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

                Vector3 preyPosition = target.position;
                Vector3 fearBurstDirection = preyPosition - transform.position;
                if (fearBurstDirection.sqrMagnitude <= 0.0001f)
                    fearBurstDirection = transform.forward;

                ChemicalInfluenceGrid.QueueFearPheromone(preyPosition, 1f);

                SargassumMicroFaunaBoids microFaunaBoids = SargassumMicroFaunaBoids.ActiveRuntimeInstance;
                if (microFaunaBoids != null)
                {
                    microFaunaBoids.RegisterPredatorFearBurst(
                        preyPosition,
                        fearBurstDirection,
                        10f,
                        0.45f,
                        0.85f);
                }

                // Despawn/Pool the prey
                if (target.TryGetComponent<FaunaBrain>(out var preyBrain))
                {
                    preyBrain.TakeDamageFromSource(damage * 10f, transform.position); // Massive damage to ensure kill
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
                TryDispatchEmpAttack(target);

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
                otherBrain.TakeDamageFromSource(damage, transform.position);
                if (IsApexPredator() && otherBrain.IsApexPredator())
                {
                    if (otherBrain.HealthNormalized <= 0.3f)
                    {
                        otherBrain.ForceApexRetreat(transform.position);
                        GainApexIntimidation();
                    }
                    else if (otherBrain.IsDead)
                    {
                        GainApexIntimidation();
                    }
                }
            }
        }

        public void TakeDamage(float amount)
        {
            TakeDamageInternal(amount, default, false);
        }

        private void TakeDamageFromSource(float amount, Vector3 damageSourcePosition)
        {
            TakeDamageInternal(amount, damageSourcePosition, true);
        }

        private void TakeDamageInternal(float amount, Vector3 damageSourcePosition, bool hasDamageSource)
        {
            if (_isDead)
                return;

            float clampedDamage = Mathf.Max(0f, amount);
            if (clampedDamage <= 0f)
                return;

            float normalizedDamage = _maxHealth > 0.001f ? clampedDamage / _maxHealth : 0f;
            _currentHealth = Mathf.Max(0f, _currentHealth - clampedDamage);

            Vector3 resolvedSourcePosition = hasDamageSource
                ? damageSourcePosition
                : ResolveFallbackDamageSourcePosition();

            if (_currentHealth > 0.001f)
                ApplyImmediateHitReaction(resolvedSourcePosition, normalizedDamage);

            EmitParentalDefenseSignal(resolvedSourcePosition, normalizedDamage);
            if (_utilityBrain.UsesPredatorRole && normalizedDamage >= 0.3f)
            {
                HectonMapMagicVegetationBridge vegetationBridge = HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
                int speciesId = ComputeStableSpeciesId();
                if (vegetationBridge != null && speciesId != 0)
                    vegetationBridge.RegisterPredatorFearNode(speciesId, transform.position, normalizedDamage);
            }
            if (_currentHealth <= 0.001f)
                Die();
        }

        /// <summary>
        /// Applies cutter damage through the fauna interaction matrix.
        /// </summary>
        public void ApplyCutDamage(float damage, Vector3 hitPoint)
        {
            if (_isDead)
                return;

            TakeDamageFromSource(damage, hitPoint);
            ApplyFaunaInteraction(FaunaInteractionKind.Cut, hitPoint, damage);
            if (TryGetComponent(out CreatureDamageManager damageManager))
                damageManager.RegisterWoundWS(hitPoint, damage);
        }

        /// <summary>
        /// Applies one authored fauna interaction response.
        /// </summary>
        public void ApplyFaunaInteraction(FaunaInteractionKind interactionKind, Vector3 sourcePosition, float intensity)
        {
            if (_isDead || _faunaDataTemplate == null)
                return;

            if (!_faunaDataTemplate.TryGetInteractionResponse(interactionKind, out FaunaInteractionResponse response))
                return;

            if (response.ForceRetreat)
            {
                float retreatDuration = Mathf.Max(0.5f, response.RetreatDurationSeconds);
                _utilityBrain.ForceRetreat(sourcePosition, _cognitionTimeSeconds, retreatDuration);
                _stateMachine.currentState = AIState.Retreat;
                _currentStateCache = AIState.Retreat;
            }

            if (response.FearImpulse01 > 0f)
            {
                _sensorSuite.isScattering = true;
                Vector3 scatterDirection = transform.position - sourcePosition;
                if (scatterDirection.sqrMagnitude <= 0.0001f)
                    scatterDirection = transform.forward;
                _sensorSuite.scatterDirection = scatterDirection.normalized;
            }

            if (response.DamageMultiplier > 1f && intensity > 0f)
            {
                float bonusDamage = intensity * (response.DamageMultiplier - 1f);
                if (bonusDamage > 0.001f)
                    TakeDamage(bonusDamage);
            }
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

        internal void ForceApexRetreat(Vector3 rivalPosition)
        {
            if (_isDead)
                return;

            float retreatDuration = ResolveApexForcedRetreatDuration();
            _utilityBrain.ForceRetreat(rivalPosition, _cognitionTimeSeconds, retreatDuration);
            _stateMachine.currentState = AIState.Retreat;
            _currentStateCache = AIState.Retreat;

            EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
            if (ecosystemDirector != null &&
                ecosystemDirector.TryResolveMigrationTarget(ComputeStableSpeciesId(), transform.position, out Vector3 migrationTarget))
            {
                _forcedMigrationTarget = migrationTarget;
                _forcedMigrationUntilTime = _cognitionTimeSeconds + retreatDuration;
                _hasForcedMigrationTarget = true;
            }
        }

        private void GainApexIntimidation()
        {
            _apexIntimidationUntilTime = Mathf.Max(_apexIntimidationUntilTime, _cognitionTimeSeconds + ResolveApexIntimidationDuration());
        }

        private void Die()
        {
            _isDead = true;
            RegisterCorpseResourceNode();
            ReportApexPredatorKill();
            if (ObjectPoolManager.Instance != null)
                ObjectPoolManager.Instance.Despawn(gameObject);
            else
                gameObject.SetActive(false);
        }

        private void RegisterCorpseResourceNode()
        {
            EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
            if (ecosystemDirector == null)
                return;

            bool shouldSpawnCorpseNode = IsApexPredator() ||
                                         (_archetype != null && (_archetype.roleType == CreatureRoleType.Hunter || _archetype.roleType == CreatureRoleType.Territorial));
            if (!shouldSpawnCorpseNode)
                return;

            ecosystemDirector.RegisterCorpseResourceNode(transform.position, ComputeStableSpeciesId(), Mathf.Max(6f, _maxHealth * 0.2f));
        }

        private void ReportApexPredatorKill()
        {
            if (!_utilityBrain.IsActivePredator)
                return;

            IPlayerRuntimeContext playerContext = GlobalRegistry.Player;
            Transform playerTransform = playerContext != null ? playerContext.PlayerTransform : null;
            if (playerTransform == null || (playerTransform.position - transform.position).sqrMagnitude > 22500f)
                return;

            IEcosystemDirectorService ecosystemDirector = GlobalRegistry.EcosystemDirector;
            if (ecosystemDirector == null || !ecosystemDirector.IsInitialized)
                return;

            float hostilityDelta = 0.22f;
            if ((_speciesProfile != null && _speciesProfile.isLeviathan) ||
                (_archetype != null && _archetype.roleType == CreatureRoleType.Leviathan))
            {
                hostilityDelta = 0.35f;
            }

            EcosystemDirector concreteDirector = ecosystemDirector as EcosystemDirector;
            if (concreteDirector != null && _uniqueInstanceUid != 0u)
            {
                concreteDirector.RegisterApexPredatorKill(_uniqueInstanceUid, transform.position, hostilityDelta);
                return;
            }

            ecosystemDirector.ReportApexPredatorKilled(transform.position, hostilityDelta);
        }

        internal void SetLogicalIdentity(uint uniqueInstanceUid)
        {
            _uniqueInstanceUid = uniqueInstanceUid;
        }

        internal void ApplyCleanerSymbiosis(float fatigueRelief)
        {
            if (fatigueRelief <= 0f)
                return;

            _utilityBrain.ApplyFatigueRelief(fatigueRelief);
        }

        internal void ApplyHibernationCatchUp(float sleepSeconds)
        {
            if (sleepSeconds <= 0f)
                return;

            if (_utilityBrain.ApplyHibernationCatchUp(sleepSeconds, _cognitionTimeSeconds))
            {
                _stateMachine.currentState = AIState.Aggressive;
                _currentStateCache = AIState.Aggressive;
            }
        }

        internal void SetLogicalLodTier(FaunaLogicalLodTier logicalLodTier)
        {
            _logicalLodTier = logicalLodTier;
        }

        private void ResolveLogicalLodTier()
        {
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref _currentCullingPlayerTransform);
            if (_currentCullingPlayerTransform == null)
            {
                _logicalLodTier = FaunaLogicalLodTier.FullSim;
                return;
            }

            EcosystemDirector ecosystemDirector = GlobalRegistry.EcosystemDirector as EcosystemDirector;
            if (ecosystemDirector == null)
            {
                _logicalLodTier = FaunaLogicalLodTier.FullSim;
                return;
            }

            _logicalLodTier = ecosystemDirector.ResolveLogicalLodTier(_currentCullingPlayerTransform.position, transform.position);
        }

        private void ApplyCognitionEvaluation(in CreatureUtilityEvaluation evaluation)
        {
            _cachedDesiredDirection = evaluation.DesiredDirection;
            _currentStateCache = evaluation.LegacyState;
            _stateMachine.currentState = evaluation.LegacyState;
            _stateMachine.currentForceMultiplier = evaluation.ForceMultiplier;
            _stateMachine.currentSpeedMultiplier = evaluation.SpeedMultiplier;
            _stateMachine.currentTurnMultiplier = evaluation.TurnMultiplier;
            _currentPackRole = (PredatorPackRole)evaluation.PackRoleCode;
            _flankingManeuverDetected = evaluation.FlankingManeuverDetected;
        }

        private void ResetStateCache()
        {
            AIState initialState = ResolveInitialState();
            _stateMachine.ResetRuntime(initialState);
            _currentStateCache = initialState;
            _cachedDesiredDirection = transform != null ? transform.forward : Vector3.forward;
            _currentPackRole = PredatorPackRole.None;
            _flankingManeuverDetected = false;
            _apexRivalTarget = null;
            _baitFeedingTarget = null;
            _forcedMigrationTarget = default;
            _apexIntimidationUntilTime = 0f;
            _forcedMigrationUntilTime = 0f;
            _nextBurrowBreachTime = 0f;
            _nextBestiaryObservationTime = 0f;
            _nextMimicPingTime = 0f;
            _mimicPingExpireTime = 0f;
            _hasForcedMigrationTarget = false;
            ClearEcholocationMimicSignal();
            ClearVoxelPathGuidance();
        }

        public bool SupportsAttackPattern(FaunaAttackPattern attackPattern)
        {
            return _faunaDataTemplate != null && _faunaDataTemplate.SupportsAttackPattern(attackPattern);
        }

        private bool TryResolveDynamicDodgeDirection(Vector3 desiredDirection, out Vector3 dodgeDirection)
        {
            dodgeDirection = default;
            if (!_sensorSuite.TryGetDeferredObstacleAvoidance(out Vector3 avoidanceDirection, out float obstaclePressure01))
                return false;

            if (obstaclePressure01 <= 0f || avoidanceDirection.sqrMagnitude <= 0.0001f)
                return false;

            float3 incoming = math.normalizesafe((float3)desiredDirection, (float3)transform.forward);
            float3 avoidance = math.normalizesafe((float3)avoidanceDirection, incoming);
            float blend = math.saturate(obstaclePressure01);
            dodgeDirection = (Vector3)math.normalizesafe(math.lerp(incoming, avoidance, blend), avoidance);
            return dodgeDirection.sqrMagnitude > 0.0001f;
        }

        private bool TryResolveWallSlideDirection(Vector3 desiredDirection, out Vector3 slideDirection)
        {
            slideDirection = default;
            if (_rb == null || !_sensorSuite.TryGetForwardObstacleSurface(out Vector3 obstacleNormal, out float obstaclePressure01))
                return false;

            Vector3 referenceVelocity = _rb.linearVelocity.sqrMagnitude > 0.0001f
                ? _rb.linearVelocity
                : desiredDirection;
            if (referenceVelocity.sqrMagnitude <= 0.0001f)
                referenceVelocity = transform.forward;

            float3 projectedVelocity = HectonContactJob.ProjectVelocityAlongSurface(referenceVelocity, obstacleNormal);
            if (math.lengthsq(projectedVelocity) <= 0.0001f)
                return false;

            float3 incoming = math.normalizesafe((float3)desiredDirection, math.normalizesafe((float3)referenceVelocity, (float3)transform.forward));
            float3 slide = math.normalizesafe(projectedVelocity, incoming);
            float blend = math.max(0.5f, math.saturate(obstaclePressure01));
            slideDirection = (Vector3)math.normalizesafe(math.lerp(incoming, slide, blend), slide);
            return slideDirection.sqrMagnitude > 0.0001f;
        }

        private void ApplyImmediateHitReaction(Vector3 damageSourcePosition, float normalizedDamage)
        {
            if (_rb == null)
                return;

            Vector3 awayDirection = ResolveDamageEscapeDirection(damageSourcePosition);
            float retreatDuration = _speciesProfile != null
                ? Mathf.Max(1f, _speciesProfile.retreatDuration)
                : 6f;

            _utilityBrain.ForceRetreat(damageSourcePosition, _cognitionTimeSeconds, retreatDuration);
            _utilityBrain.ApplyExternalState(AIState.Retreat, _cognitionTimeSeconds);
            _stateMachine.currentState = AIState.Retreat;
            _currentStateCache = AIState.Retreat;
            _cachedDesiredDirection = awayDirection;
            _sensorSuite.isScattering = true;
            _sensorSuite.scatterDirection = awayDirection;

            float targetFlinchVelocity = Mathf.Lerp(
                Mathf.Max(DamageFlinchVelocityFloor, _steeringEngine.maxSpeed),
                Mathf.Max(DamageFlinchVelocityCeiling, _steeringEngine.maxSpeed * 2.25f),
                Mathf.Clamp01(normalizedDamage));
            Vector3 targetVelocity = awayDirection * targetFlinchVelocity;
            Vector3 velocityChange = targetVelocity - _rb.linearVelocity;
            PhysicsForceRouter.QueueForce(_rb, velocityChange, ForceMode.VelocityChange);

            float fearIntensity = Mathf.Clamp01(Mathf.Max(DamageFearPheromoneFloor, normalizedDamage * DamageFearPheromoneBoost));
            ChemicalInfluenceGrid.QueueFearPheromone(transform.position, fearIntensity);
        }

        private Vector3 ResolveDamageEscapeDirection(Vector3 damageSourcePosition)
        {
            Vector3 awayDirection = transform.position - damageSourcePosition;
            if (awayDirection.sqrMagnitude > 0.0001f)
                return awayDirection.normalized;

            if (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.0001f)
                return (-_rb.linearVelocity).normalized;

            if (_cachedDesiredDirection.sqrMagnitude > 0.0001f)
                return (-_cachedDesiredDirection).normalized;

            return -transform.forward;
        }

        private Vector3 ResolveFallbackDamageSourcePosition()
        {
            if (_rb != null && _rb.linearVelocity.sqrMagnitude > 0.0001f)
                return transform.position + _rb.linearVelocity.normalized;

            if (_cachedDesiredDirection.sqrMagnitude > 0.0001f)
                return transform.position + _cachedDesiredDirection.normalized;

            return transform.position + transform.forward;
        }

        private AIState ResolveInitialState()
        {
            if (_speciesProfile != null && _speciesProfile.isAmbusher)
                return AIState.Idle;

            return _stateMachine.isFlockingFish ? AIState.Flocking : AIState.Wander;
        }

        private int ComputeStableSpeciesId()
        {
            if (_faunaDataTemplate != null && _faunaDataTemplate.SpeciesId != 0)
                return _faunaDataTemplate.SpeciesId;

            if (_speciesProfile != null && _speciesProfile.speciesID != 0)
                return _speciesProfile.speciesID;

            if (_archetype != null && !string.IsNullOrWhiteSpace(_archetype.creatureId))
                return unchecked((int)Hecton.Localization.LocHash.Compute(_archetype.creatureId)) & int.MaxValue;

            return 0;
        }

        private void ApplyFaunaDataTemplate(FaunaDataTemplate faunaDataTemplate)
        {
            if (ReferenceEquals(_faunaDataTemplate, faunaDataTemplate))
            {
                ApplyTemplateRuntimeTuning();
                ConfigureFaunaScanMetadata();
                return;
            }

            _faunaDataTemplate = faunaDataTemplate;
            ApplyTemplateRuntimeTuning();
            _utilityBrain.BindProfile(_speciesProfile, _archetype, _faunaDataTemplate);
            ConfigureFaunaScanMetadata();
        }

        private void ApplyTemplateRuntimeTuning()
        {
            if (_faunaDataTemplate == null)
                return;

            _baseAggroDistance = _faunaDataTemplate.AggroRadius;
            _baseDeaggroDistance = Mathf.Max(_baseAggroDistance, _baseAggroDistance * 1.35f);
            _baseCruiseSpeed = _faunaDataTemplate.SwimSpeed;
            _baseBurstSpeed = Mathf.Max(_baseCruiseSpeed, _faunaDataTemplate.MaxSpeedMetersPerSecond);
            _baseTurnSpeed = _faunaDataTemplate.TurnRate;

            _sensorSuite.aggroDistance = _baseAggroDistance;
            _sensorSuite.deaggroDistance = _baseDeaggroDistance;
            _sensorSuite.visionConeAngle = _faunaDataTemplate.VisionConeAngle;

            _steeringEngine.moveSpeed = _baseCruiseSpeed;
            _steeringEngine.maxSpeed = _baseBurstSpeed;
            _steeringEngine.turnSpeed = _baseTurnSpeed;
            _steeringEngine.rotationSpeed = _baseTurnSpeed;
            _steeringEngine.swimForce = Mathf.Max(_baseCruiseSpeed, _baseBurstSpeed);
        }

        private float ResolveFleeHealthThreshold()
        {
            return _faunaDataTemplate != null
                ? _faunaDataTemplate.FleeHealthThreshold
                : 0.3f;
        }

        private bool ValidatePrimitiveColliderRig()
        {
            MeshCollider meshCollider = GetComponentInChildren<MeshCollider>(true);
            if (meshCollider != null)
            {
                Debug.LogError("FaunaBrain requires primitive collider hygiene. MeshCollider detected on fauna hierarchy.", meshCollider);
                return false;
            }

            CapsuleCollider capsuleCollider = GetComponentInChildren<CapsuleCollider>(true);
            SphereCollider sphereCollider = GetComponentInChildren<SphereCollider>(true);
            if (capsuleCollider == null && sphereCollider == null)
            {
                Debug.LogError("FaunaBrain requires a CapsuleCollider or SphereCollider on the fauna hierarchy.", this);
                return false;
            }

            return true;
        }

        internal bool IsValidPreyFor(FaunaBrain predatorBrain)
        {
            if (predatorBrain == null || predatorBrain == this || IsDead)
                return false;

            uint preyMaskBits = PreyMaskBits;
            return preyMaskBits != 0u && predatorBrain.CanConsumePrey(preyMaskBits);
        }

        internal bool CanConsumePrey(uint preyMaskBits)
        {
            return _faunaDataTemplate != null && _faunaDataTemplate.CanConsumePrey(preyMaskBits);
        }

        internal uint DietMaskBits => _faunaDataTemplate != null ? _faunaDataTemplate.DietMaskBits : 0u;

        internal uint PreyMaskBits => _faunaDataTemplate != null ? _faunaDataTemplate.PreyMaskBits : 0u;

        private void ConfigureFaunaScanMetadata()
        {
            _cachedScanEntryHash = 0u;
            if (_faunaDataTemplate == null)
                return;

            _cachedScanEntryHash = ScanEvents.ComputeEntryHash(_faunaDataTemplate.ScanEntryId);
            if (_scannableTarget == null)
                TryGetComponent(out _scannableTarget);

            FaunaScanRuntimeRegistry.Register(_faunaDataTemplate);
            if (_scannableTarget == null)
                return;

            string fallbackTitle = _archetype != null && !string.IsNullOrWhiteSpace(_archetype.displayName)
                ? _archetype.displayName
                : gameObject.name;
            string fallbackCategory = _archetype != null
                ? _archetype.roleType.ToString()
                : "Fauna";
            string fallbackSummary = _archetype != null && !string.IsNullOrWhiteSpace(_archetype.gameplayPurpose)
                ? _archetype.gameplayPurpose
                : "Passive fauna contact. Manual classification pending.";

            _scannableTarget.Configure(
                _faunaDataTemplate.ScanEntryId,
                _faunaDataTemplate.ResolveScanTitle(fallbackTitle),
                _faunaDataTemplate.ResolveScanCategory(fallbackCategory),
                _faunaDataTemplate.ResolveScanSummary(fallbackSummary));
        }

        private void TryReportFaunaFeedingObservation()
        {
            if (_cachedScanEntryHash == 0u || _cognitionTimeSeconds < _nextBestiaryObservationTime)
                return;

            Transform playerTransform = _currentCullingPlayerTransform;
            WorldRuntimeReferenceUtility.TryResolvePlayerTransform(ref playerTransform);
            _currentCullingPlayerTransform = playerTransform;
            if (playerTransform == null ||
                (playerTransform.position - transform.position).sqrMagnitude > FeedingObservationRadiusMetersSqr)
            {
                return;
            }

            _nextBestiaryObservationTime = _cognitionTimeSeconds + FeedingObservationCooldownSeconds;
            ScanEvents.RaiseFaunaFeedingObserved(_cachedScanEntryHash, transform.position);
        }

        private Unity.Mathematics.Random CreateDeterministicRandom()
        {
            int speciesId = ComputeStableSpeciesId();
            uint ownerId = unchecked((uint)EntityId.ToULong(GetEntityId()));
            uint seed = math.hash(new uint4(ownerId, unchecked((uint)speciesId), 0x7F4A7C15u, 0x3A9D2B71u));
            return new Unity.Mathematics.Random(seed == 0u ? 1u : seed);
        }
    }
}
