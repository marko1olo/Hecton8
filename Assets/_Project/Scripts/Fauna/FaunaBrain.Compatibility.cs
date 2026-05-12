using System;
using Hecton8.Ecosystem;
using Hecton8.Core;
using Hecton8.World;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.AI
{
    public partial class FaunaBrain
    {
        private CreatureArchetypeData _archetype;
        private Vector3 _spawnPoint;
        private float _currentHealth = 1f;
        private float _maxHealth = 1f;

        public float CurrentHealth => _isDead ? 0f : _currentHealth;
        public float MaxHealth => _maxHealth;
        public float HealthNormalized => _maxHealth > 0.001f ? CurrentHealth / _maxHealth : 0f;
        public bool IsDead => _isDead || _currentHealth <= 0.001f;
        public bool IsSleeping => _sensorSuite.distSqrToPlayer > _sensorSuite.sleepDistance * _sensorSuite.sleepDistance;
        public bool UsesPackHuntBehavior => _archetype != null && _archetype.usePackHunt;
        public bool UsesFeintRushBehavior => _archetype != null && _archetype.useFeintRush;
        public LeviathanEncounterType LeviathanEncounter => _archetype != null
            ? _archetype.leviathanEncounterType
            : LeviathanEncounterType.PresenceCircle;

        public void ApplyArchetype(CreatureArchetypeData archetype)
        {
            _archetype = archetype;
            ApplyFaunaDataTemplate(archetype != null && archetype.faunaDataTemplate != null ? archetype.faunaDataTemplate : _faunaDataTemplate);
            _utilityBrain.BindProfile(_speciesProfile, _archetype, _faunaDataTemplate);
            if (archetype == null)
                return;

            isAggressive = archetype.isAggressive;
            canFlee = archetype.canFlee;
            _lootProfileId = archetype.lootProfileId ?? string.Empty;

            _baseMaxHealth = math.max(1f, archetype.maxHealth);
            _maxHealth = _baseMaxHealth;
            _currentHealth = math.clamp(_currentHealth, 0f, _maxHealth);
            if (_currentHealth <= 0.001f)
                _currentHealth = _maxHealth;

            _baseAggroDistance = math.max(0f, archetype.baseAggroDistance);
            _baseDeaggroDistance = math.max(_baseAggroDistance, archetype.baseDeaggroDistance);
            _baseAttackDamage = math.max(0f, archetype.attackDamage);
            _baseCruiseSpeed = math.max(0.1f, archetype.cruiseSpeed);
            _baseBurstSpeed = math.max(_baseCruiseSpeed, archetype.burstSpeed);
            _baseTurnSpeed = math.max(0.1f, archetype.turnSpeed);

            if (_faunaDataTemplate != null)
            {
                _baseAggroDistance = _faunaDataTemplate.AggroRadius;
                _baseDeaggroDistance = math.max(_baseAggroDistance, math.max(archetype.baseDeaggroDistance, _baseAggroDistance * 1.35f));
                _baseCruiseSpeed = _faunaDataTemplate.SwimSpeed;
                _baseBurstSpeed = math.max(_baseCruiseSpeed, _faunaDataTemplate.MaxSpeedMetersPerSecond);
                _baseTurnSpeed = _faunaDataTemplate.TurnRate;
            }

            _sensorSuite.aggroDistance = _baseAggroDistance;
            _sensorSuite.deaggroDistance = _baseDeaggroDistance;
            _sensorSuite.sleepDistance = math.max(1f, archetype.sleepDistance);
            if (_faunaDataTemplate != null)
                _sensorSuite.visionConeAngle = _faunaDataTemplate.VisionConeAngle;
            _sensorSuite.reactToPlayerNoise = archetype.reactToPlayerNoise;
            _sensorSuite.reactToPlayerLight = archetype.reactToPlayerLight;

            _stateMachine.escapeDistance = math.max(0f, archetype.baseEscapeDistance);
            _stateMachine.escapeSafeDistance = math.max(_stateMachine.escapeDistance, archetype.baseEscapeSafeDistance);
            _stateMachine.stalkDuration = math.max(0f, archetype.stalkDuration);
            _stateMachine.stalkRadius = math.max(1f, archetype.stalkDistance);
            _stateMachine.useTerritory = archetype.useHomeTerritory;
            _stateMachine.patrolRadius = math.max(1f, archetype.homeReturnDistance);
            _stateMachine.wanderRadius = archetype.useHomeTerritory
                ? math.max(1f, archetype.homeWanderRadius)
                : math.max(1f, _stateMachine.wanderRadius);

            _steeringEngine.moveSpeed = _baseCruiseSpeed;
            _steeringEngine.maxSpeed = _baseBurstSpeed;
            _steeringEngine.turnSpeed = _baseTurnSpeed;
            _steeringEngine.rotationSpeed = _baseTurnSpeed;
            _steeringEngine.swimForce = math.max(_baseCruiseSpeed, _baseBurstSpeed);
            ApplyRuntimeEcosystemOverlays();
            ApplyPassiveRigidbodyCastrationIfRequired();
        }

        public void SetSpawnPoint(Vector3 spawnPoint)
        {
            _spawnPoint = spawnPoint;
            AbsoluteUniversePosition spawnAup = AbsoluteUniversePosition.FromRuntimePosition(spawnPoint);
            ApplyAupPresentationPosition(in spawnAup);
            _utilityBrain.SetSpawnAnchor(spawnPoint);
        }

        public void ForceState(FaunaBrain.AIState state)
        {
            _stateMachine.currentState = state;
            _utilityBrain.ApplyExternalState(state, _cognitionTimeSeconds);
        }
    }

    /// <summary>
    /// Bitmask-driven utility evaluator used by large predator cognition.
    /// </summary>
    [Flags]
    public enum PredatorUtilityState : byte
    {
        None = 0x00,
        Prowling = 0x01,
        Stalking = 0x02,
        Attacking = 0x04,
        Fleeing = 0x08,
    }

    /// <summary>
    /// Immutable sensory snapshot consumed by <see cref="CreatureUtilityBrain"/>.
    /// This is the managed-to-native bridge payload for all fauna roles.
    /// </summary>
    public readonly struct CreatureUtilityContext
    {
        public CreatureUtilityContext(
            Vector3 selfPosition,
            Vector3 selfVelocity,
            Vector3 selfForward,
            Vector3 playerPosition,
            Vector3 playerForward,
            Vector3 playerVelocity,
            Vector3 threatPosition,
            Vector3 apexRivalPosition,
            Vector3 preyPosition,
            Vector3 scavengePosition,
            Vector3 flockCenter,
            Vector3 flockDirection,
            Vector3 flockAvoidance,
            Vector3 scatterDirection,
            float healthNormalized,
            float distanceToPlayerSqr,
            float attackRange,
            float fearPressure01,
            float fleeHealthThreshold,
            float escapeDistance,
            float escapeSafeDistance,
            float wanderRadius,
            float patrolRadius,
            float apexTerritoryRadius,
            float apexAggressionMultiplier,
            float playerLightExposure01,
            float foveatedImportanceScore,
            int flockCount,
            bool canFlee,
            bool hasVisualContact,
            bool hasPlayerTarget,
            bool hasThreatTarget,
            bool hasApexRivalTarget,
            bool hasPreyTarget,
            bool hasScavengeTarget,
            bool useHomeTerritory,
            bool isFlocking,
            bool hasScatterDirection,
            bool isAggressive,
            bool isApexPredator)
        {
            SelfPosition = selfPosition;
            SelfVelocity = selfVelocity;
            SelfForward = selfForward;
            PlayerPosition = playerPosition;
            PlayerForward = playerForward;
            PlayerVelocity = playerVelocity;
            ThreatPosition = threatPosition;
            ApexRivalPosition = apexRivalPosition;
            PreyPosition = preyPosition;
            ScavengePosition = scavengePosition;
            FlockCenter = flockCenter;
            FlockDirection = flockDirection;
            FlockAvoidance = flockAvoidance;
            ScatterDirection = scatterDirection;
            HealthNormalized = healthNormalized;
            DistanceToPlayerSqr = distanceToPlayerSqr;
            AttackRange = attackRange;
            FearPressure01 = fearPressure01;
            FleeHealthThreshold = fleeHealthThreshold;
            EscapeDistance = escapeDistance;
            EscapeSafeDistance = escapeSafeDistance;
            WanderRadius = wanderRadius;
            PatrolRadius = patrolRadius;
            ApexTerritoryRadius = apexTerritoryRadius;
            ApexAggressionMultiplier = apexAggressionMultiplier;
            PlayerLightExposure01 = math.saturate(playerLightExposure01);
            FoveatedImportanceScore = foveatedImportanceScore;
            FlockCount = flockCount;
            CanFlee = canFlee;
            HasVisualContact = hasVisualContact;
            HasPlayerTarget = hasPlayerTarget;
            HasThreatTarget = hasThreatTarget;
            HasApexRivalTarget = hasApexRivalTarget;
            HasPreyTarget = hasPreyTarget;
            HasScavengeTarget = hasScavengeTarget;
            UseHomeTerritory = useHomeTerritory;
            IsFlocking = isFlocking;
            HasScatterDirection = hasScatterDirection;
            IsAggressive = isAggressive;
            IsApexPredator = isApexPredator;
        }

        public Vector3 SelfPosition { get; }
        public Vector3 SelfVelocity { get; }
        public Vector3 SelfForward { get; }
        public Vector3 PlayerPosition { get; }
        public Vector3 PlayerForward { get; }
        public Vector3 PlayerVelocity { get; }
        public Vector3 ThreatPosition { get; }
        public Vector3 ApexRivalPosition { get; }
        public Vector3 PreyPosition { get; }
        public Vector3 ScavengePosition { get; }
        public Vector3 FlockCenter { get; }
        public Vector3 FlockDirection { get; }
        public Vector3 FlockAvoidance { get; }
        public Vector3 ScatterDirection { get; }
        public float HealthNormalized { get; }
        public float DistanceToPlayerSqr { get; }
        public float AttackRange { get; }
        public float FearPressure01 { get; }
        public float FleeHealthThreshold { get; }
        public float EscapeDistance { get; }
        public float EscapeSafeDistance { get; }
        public float WanderRadius { get; }
        public float PatrolRadius { get; }
        public float ApexTerritoryRadius { get; }
        public float ApexAggressionMultiplier { get; }
        public float PlayerLightExposure01 { get; }
        public float FoveatedImportanceScore { get; }
        public int FlockCount { get; }
        public bool CanFlee { get; }
        public bool HasVisualContact { get; }
        public bool HasPlayerTarget { get; }
        public bool HasThreatTarget { get; }
        public bool HasApexRivalTarget { get; }
        public bool HasPreyTarget { get; }
        public bool HasScavengeTarget { get; }
        public bool UseHomeTerritory { get; }
        public bool IsFlocking { get; }
        public bool HasScatterDirection { get; }
        public bool IsAggressive { get; }
        public bool IsApexPredator { get; }
    }

    /// <summary>
    /// Value-type utility evaluation result used by the fauna brain tick loop.
    /// </summary>
    public readonly struct CreatureUtilityEvaluation
    {
        public CreatureUtilityEvaluation(
            Vector3 desiredDirection,
            PredatorUtilityState stateMask,
            FaunaBrain.AIState legacyState,
            float hungerScore,
            float aggressionScore,
            float fearScore,
            float forceMultiplier,
            float speedMultiplier,
            float turnMultiplier,
            bool shouldAttack,
            bool emitThreatPulse,
            int packRoleCode,
            bool flankingManeuverDetected)
        {
            DesiredDirection = desiredDirection;
            StateMask = stateMask;
            LegacyState = legacyState;
            HungerScore = hungerScore;
            AggressionScore = aggressionScore;
            FearScore = fearScore;
            ForceMultiplier = forceMultiplier;
            SpeedMultiplier = speedMultiplier;
            TurnMultiplier = turnMultiplier;
            ShouldAttack = shouldAttack;
            EmitThreatPulse = emitThreatPulse;
            PackRoleCode = packRoleCode;
            FlankingManeuverDetected = flankingManeuverDetected;
        }

        public Vector3 DesiredDirection { get; }
        public PredatorUtilityState StateMask { get; }
        public FaunaBrain.AIState LegacyState { get; }
        public float HungerScore { get; }
        public float AggressionScore { get; }
        public float FearScore { get; }
        public float ForceMultiplier { get; }
        public float SpeedMultiplier { get; }
        public float TurnMultiplier { get; }
        public bool ShouldAttack { get; }
        public bool EmitThreatPulse { get; }
        public int PackRoleCode { get; }
        public bool FlankingManeuverDetected { get; }
    }

    /// <summary>
    /// Zero-allocation predator memory ring buffer backed by a native slot array.
    /// XYZ stores world position and W stores the authored timestamp.
    /// </summary>
    public struct PredatorMemory : IDisposable
    {
        private NativeArray<float4> _slots;
        private int _writeIndex;
        private int _count;

        /// <summary>
        /// True when the native ring buffer has been allocated.
        /// </summary>
        public bool IsCreated => _slots.IsCreated;

        /// <summary>
        /// Number of valid remembered slots currently stored.
        /// </summary>
        public int Count => _count;

        /// <summary>
        /// Allocates the native ring buffer when it has not been created yet.
        /// </summary>
        /// <param name="capacity">Maximum number of remembered positions.</param>
        public void Initialize(int capacity)
        {
            if (_slots.IsCreated)
                return;

            int safeCapacity = math.max(1, capacity);
            _slots = new NativeArray<float4>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory); // COLD ALLOC: NativeArray<float4>[safeCapacity] - predator memory ring buffer - owner: PredatorMemory
            NativeMemorySentinel.RegisterNativeArray(_slots, nameof(PredatorMemory), nameof(_slots), NativeAllocationLifetime.Session);
            _writeIndex = 0;
            _count = 0;
        }

        /// <summary>
        /// Clears all remembered positions without reallocating the backing buffer.
        /// </summary>
        public void Clear()
        {
            if (!_slots.IsCreated)
                return;

            _writeIndex = 0;
            _count = 0;
        }

        /// <summary>
        /// Records one remembered position into the bounded ring buffer.
        /// Nearby consecutive stimuli refresh the newest slot instead of consuming capacity.
        /// </summary>
        /// <param name="position">World position to remember.</param>
        /// <param name="timeStamp">Authored timestamp in seconds.</param>
        public void Record(float3 position, float timeStamp)
        {
            if (!_slots.IsCreated)
                return;

            if (_count > 0)
            {
                int lastIndex = _writeIndex > 0 ? _writeIndex - 1 : _slots.Length - 1;
                float4 lastSlot = _slots[lastIndex];
                float3 delta = lastSlot.xyz - position;
                if (math.lengthsq(delta) <= 4f)
                {
                    _slots[lastIndex] = new float4(position, timeStamp);
                    return;
                }
            }

            _slots[_writeIndex] = new float4(position, timeStamp);
            _writeIndex++;
            if (_writeIndex >= _slots.Length)
                _writeIndex = 0;

            if (_count < _slots.Length)
                _count++;
        }

        /// <summary>
        /// Resolves the highest-weighted remembered position using recency and travel cost.
        /// </summary>
        /// <param name="currentTime">Current authored time in seconds.</param>
        /// <param name="currentPosition">Current predator position.</param>
        /// <param name="maxAgeSeconds">Maximum valid memory age.</param>
        /// <param name="position">Best remembered position when one exists.</param>
        /// <param name="weight">Resolved composite weight.</param>
        /// <returns>True when a valid remembered position exists.</returns>
        public bool TryGetHighestWeightedPosition(
            float currentTime,
            float3 currentPosition,
            float maxAgeSeconds,
            out float3 position,
            out float weight)
        {
            if (!_slots.IsCreated || _count <= 0 || maxAgeSeconds <= 0f)
            {
                position = default;
                weight = 0f;
                return false;
            }

            float safeMaxAge = math.max(0.01f, maxAgeSeconds);
            float bestWeight = 0f;
            float3 bestPosition = default;
            bool found = false;

            for (int i = 0; i < _count; i++)
            {
                float4 slot = _slots[i];
                float age = currentTime - slot.w;
                if (age < 0f || age > safeMaxAge)
                    continue;

                float age01 = 1f - math.saturate(age / safeMaxAge);
                float3 toMemory = slot.xyz - currentPosition;
                float3 absoluteDelta = math.abs(toMemory);
                float maxAxis = math.cmax(absoluteDelta);
                float minAxis = math.cmin(absoluteDelta);
                float midAxis = absoluteDelta.x + absoluteDelta.y + absoluteDelta.z - maxAxis - minAxis;
                float approxDistance = maxAxis + midAxis * 0.5f + minAxis * 0.25f;
                float distanceWeight = math.rcp(1f + approxDistance);
                float candidateWeight = age01 * age01 * distanceWeight;
                if (candidateWeight <= bestWeight)
                    continue;

                bestWeight = candidateWeight;
                bestPosition = slot.xyz;
                found = true;
            }

            position = bestPosition;
            weight = bestWeight;
            return found;
        }

        /// <summary>
        /// Releases the native ring buffer.
        /// </summary>
        public void Dispose()
        {
            if (!_slots.IsCreated)
                return;

            NativeMemorySentinel.UnregisterNativeArray(_slots);
            _slots.Dispose();
            _slots = default;
            _writeIndex = 0;
            _count = 0;
        }
    }

    /// <summary>
    /// Shared fauna cognition bridge. Authoring stays on the managed side; runtime decision state lives in PredatorCognitionDomain.
    /// </summary>
    public struct CreatureUtilityBrain
    {
        private const float MetabolicTickIntervalSeconds = 5f;
        private const float PredatorAcousticSightRadiusMeters = 50f;
        private const float PredatorAcousticSightRadiusMetersSqr =
            PredatorAcousticSightRadiusMeters * PredatorAcousticSightRadiusMeters;
        private const float PredatorAcousticSightThreshold01 = 0.12f;
        private const float PlayerNoiseReferenceSpeedSqr = 72.25f;
        private const uint HighTierApexCognitionSteeringMask =
            (1u << (int)HectonQualityTier.High) |
            (1u << (int)HectonQualityTier.Ultra);

        private CreatureArchetypeData _archetype;
        private FaunaSpeciesProfile _speciesProfile;
        private FaunaDataTemplate _dataTemplate;
        private int _slot;
        private bool _initialized;
        private float _metabolicTickAccumulator;

        public PredatorUtilityState CurrentStateMask { get; private set; }
        public bool UsesPredatorRole => IsPredatorArchetype(_archetype, _speciesProfile);
        public bool IsActivePredator => UsesPredatorRole;
        public float HungerScore { get; private set; }
        public float AggressionScore { get; private set; }
        public float FearScore { get; private set; }
        public bool IsRegistered => _initialized && _slot >= 0;
        public float CurrentHunger01 => _initialized ? PredatorCognitionDomain.GetHunger01(_slot) : HungerScore;

        public void Initialize(Vector3 spawnAnchor, FaunaSpeciesProfile speciesProfile, CreatureArchetypeData archetype, FaunaDataTemplate dataTemplate)
        {
            _speciesProfile = speciesProfile;
            _archetype = archetype;
            _dataTemplate = dataTemplate;
            if (!_initialized)
                _slot = PredatorCognitionDomain.Register();

            _initialized = _slot >= 0;
            if (_initialized)
            {
                RegisterSpeciesTuning();
                PredatorCognitionDomain.ResetSlot(_slot, spawnAnchor, ResolveSpeciesId());
            }
        }

        public void BindProfile(FaunaSpeciesProfile speciesProfile, CreatureArchetypeData archetype, FaunaDataTemplate dataTemplate)
        {
            _speciesProfile = speciesProfile;
            _archetype = archetype;
            _dataTemplate = dataTemplate;
            RegisterSpeciesTuning();
        }

        public void SetSpawnAnchor(Vector3 spawnAnchor)
        {
            if (_initialized)
                PredatorCognitionDomain.SetSpawnAnchor(_slot, spawnAnchor);
        }

        public void SetRuntimeActive(bool active)
        {
            if (_initialized)
                PredatorCognitionDomain.SetSlotActive(_slot, active);
        }

        public void ResetRuntimeState(Vector3 spawnAnchor)
        {
            CurrentStateMask = PredatorUtilityState.None;
            HungerScore = 0f;
            AggressionScore = 0f;
            FearScore = 0f;
            _metabolicTickAccumulator = 0f;
            if (_initialized)
            {
                RegisterSpeciesTuning();
                PredatorCognitionDomain.ResetSlot(_slot, spawnAnchor, ResolveSpeciesId());
            }
        }

        public void RecordAuditoryStimulus(Vector3 worldPosition, float timeStamp)
        {
            if (_initialized)
                PredatorCognitionDomain.RecordStimulus(_slot, worldPosition, timeStamp, 1f, CognitionStimulusType.Acoustic);
        }

        public void ApplyExternalState(FaunaBrain.AIState state, float currentTime)
        {
            if (!_initialized)
                return;

            switch (state)
            {
                case FaunaBrain.AIState.Aggressive:
                case FaunaBrain.AIState.Starving:
                    PredatorCognitionDomain.ApplyExternalState(_slot, PredatorUtilityState.Attacking, currentTime);
                    break;
                case FaunaBrain.AIState.ApexForcedRetreat:
                case FaunaBrain.AIState.Retreat:
                case FaunaBrain.AIState.Escape:
                    PredatorCognitionDomain.ApplyExternalState(_slot, PredatorUtilityState.Fleeing, currentTime);
                    break;
                case FaunaBrain.AIState.Sated:
                    PredatorCognitionDomain.ForceSated(_slot, currentTime, 4f);
                    break;
                default:
                    PredatorCognitionDomain.ApplyExternalState(_slot, PredatorUtilityState.Prowling, currentTime);
                    break;
            }
        }

        public void ForceRetreat(Vector3 threatPosition, float currentTime, float duration)
        {
            if (_initialized)
                PredatorCognitionDomain.ForceRetreat(_slot, threatPosition, currentTime, duration);
        }

        public void ForceSated(float currentTime, float duration)
        {
            if (_initialized)
                PredatorCognitionDomain.ForceSated(_slot, currentTime, duration);
        }

        public void ApplyFatigueRelief(float amount)
        {
            if (_initialized)
                PredatorCognitionDomain.ReduceFatigue(_slot, amount);
        }

        public void SetHunger01(float hunger01)
        {
            float clampedHunger = math.saturate(hunger01);
            HungerScore = clampedHunger;
            if (_initialized)
                PredatorCognitionDomain.SetHunger01(_slot, clampedHunger);
        }

        public void NotifyAttackPerformed(float currentTime, float cooldownSeconds)
        {
            if (_initialized)
                PredatorCognitionDomain.NotifyAttackPerformed(_slot, currentTime, cooldownSeconds);
        }

        public CreatureUtilityEvaluation Evaluate(int frameId, float dt, float currentTime, in CreatureUtilityContext context)
        {
            if (!_initialized)
                Initialize(context.SelfPosition, _speciesProfile, _archetype, _dataTemplate);

            float dispatcherDeltaTime = math.max(0f, dt);
            _metabolicTickAccumulator += dispatcherDeltaTime;
            float metabolicDeltaTime = 0f;
            if (_metabolicTickAccumulator >= MetabolicTickIntervalSeconds)
            {
                metabolicDeltaTime = _metabolicTickAccumulator;
                _metabolicTickAccumulator = 0f;
            }

            float3 fallbackForward = (float3)context.SelfForward;
            float acousticPingStrength01 = 0f;
            float acousticTransmission01 = 0f;
            bool hasNoisePlayerTarget = false;
            Vector3 noisePlayerPosition = default;
            AbsoluteUniversePosition noisePlayerAup = default;
            bool hasNoisePlayerAup = false;
            if (NoiseSystem.TryGetPlayerSignal(out NoiseSystem.PlayerNoiseSignal playerNoise))
            {
                noisePlayerPosition = playerNoise.Position;
                noisePlayerAup = playerNoise.PositionAup;
                hasNoisePlayerAup = true;
                float movement01 = math.saturate(math.max(0f, playerNoise.MovementSpeedSqr) / PlayerNoiseReferenceSpeedSqr);
                float tool01 = math.saturate(playerNoise.ToolUseNoise01);
                float transport01 = math.saturate(playerNoise.TransportBoost01 * math.max(1f, playerNoise.TransportSignature));
                float flashlight01 = playerNoise.FlashlightOn ? 0.2f : 0f;
                acousticPingStrength01 = math.saturate(math.max(movement01, math.max(tool01, transport01)) + flashlight01);
                acousticTransmission01 = math.saturate(playerNoise.AcousticTransmission01);
                float acousticDistanceSq = math.lengthsq((float3)(playerNoise.Position - context.SelfPosition));
                hasNoisePlayerTarget = UsesPredatorRole &&
                                       acousticPingStrength01 >= PredatorAcousticSightThreshold01 &&
                                       acousticDistanceSq <= PredatorAcousticSightRadiusMetersSqr;
            }

            Vector3 resolvedPlayerPosition = context.HasPlayerTarget ? context.PlayerPosition : noisePlayerPosition;
            bool hasAnyPlayerTarget = context.HasPlayerTarget || hasNoisePlayerTarget;
            Vector3 floatingOriginOffset = Hecton8.Core.HectonFloatingOrigin.CurrentTotalOffset;
            AbsoluteUniversePositionBlit128 playerTargetAup = default;
            if (hasAnyPlayerTarget)
            {
                playerTargetAup = !context.HasPlayerTarget && hasNoisePlayerAup
                    ? noisePlayerAup.ToAlignedBlit()
                    : AbsoluteUniversePosition.FromRuntimePosition(resolvedPlayerPosition).ToAlignedBlit();
            }

            bool hasPackTarget = _archetype != null && _archetype.usePackHunt && (hasAnyPlayerTarget || context.HasPreyTarget);
            Vector3 resolvedPackTargetPosition = hasAnyPlayerTarget
                ? resolvedPlayerPosition
                : context.PreyPosition;
            Vector3 resolvedPackTargetVelocity = hasAnyPlayerTarget
                ? context.PlayerVelocity
                : Vector3.zero;
            AbsoluteUniversePositionBlit128 packTargetAup = hasPackTarget
                ? !context.HasPlayerTarget && hasNoisePlayerAup && hasAnyPlayerTarget
                    ? noisePlayerAup.ToAlignedBlit()
                    : AbsoluteUniversePosition.FromRuntimePosition(resolvedPackTargetPosition).ToAlignedBlit()
                : default;

            float chemicalSignal01 = 0f;
            if (context.HasScavengeTarget)
            {
                float scavengeDistanceSq = math.lengthsq((float3)(context.ScavengePosition - context.SelfPosition));
                chemicalSignal01 = math.saturate(1f / (1f + (scavengeDistanceSq / (28f * 28f))));
            }

            ResolveSpeciesGenetics(
                out float chemicalSensitivity,
                out float packCoordinationRadius,
                out float packFlankDistance,
                out float packCommitDistance,
                out float aggressionMultiplier);

            Hecton8.World.HectonMapMagicVegetationBridge vegetationBridge = Hecton8.World.HectonMapMagicVegetationBridge.ActiveRuntimeInstance;
            if (vegetationBridge != null && vegetationBridge.HasPermanentThreatEcho(context.SelfPosition))
                chemicalSignal01 = math.max(chemicalSignal01, 0.35f);

            // Frame N consumes outputs produced from the fully submitted inputs of frame N-1.
            CognitionOutput output = PredatorCognitionDomain.GetOutput(_slot, fallbackForward);
            CurrentStateMask = (PredatorUtilityState)output.StateMask;
            HungerScore = output.HungerScore;
            AggressionScore = output.AggressionScore;
            FearScore = output.FearScore;

            CognitionInput input = default;
            input.Position = context.SelfPosition;
            input.Velocity = context.SelfVelocity;
            input.Forward = context.SelfForward;
            input.PlayerPosition = resolvedPlayerPosition;
            input.FloatingOriginOffset = floatingOriginOffset;
            input.PlayerTargetAup = playerTargetAup;
            input.PackTargetAup = packTargetAup;
            input.ThreatPosition = context.ThreatPosition;
            input.RivalApexPosition = context.ApexRivalPosition;
            input.PlayerForward = context.PlayerForward;
            input.PreyPosition = context.PreyPosition;
            input.ScavengePosition = context.ScavengePosition;
            input.PackTargetPosition = resolvedPackTargetPosition;
            input.PackTargetVelocity = resolvedPackTargetVelocity;
            input.FlockCenter = context.FlockCenter;
            input.FlockDirection = context.FlockDirection;
            input.FlockAvoidance = context.FlockAvoidance;
            input.ScatterDirection = context.ScatterDirection;
            input.DistanceToPlayerSqr = math.max(0f, context.DistanceToPlayerSqr);
            input.AttackRange = math.max(1f, context.AttackRange);
            input.HealthNormalized = math.saturate(context.HealthNormalized);
            input.FearPressure01 = math.saturate(context.FearPressure01);
            input.FleeHealthThreshold = math.saturate(context.FleeHealthThreshold);
            input.DeltaTime = dispatcherDeltaTime;
            input.MetabolicDeltaTime = metabolicDeltaTime;
            input.CurrentTime = currentTime;
            input.AcousticPingStrength01 = acousticPingStrength01;
            input.AcousticTransmission01 = acousticTransmission01;
            input.ChemicalSignal01 = chemicalSignal01;
            input.ChemicalSensitivity = chemicalSensitivity;
            SpeciesCognitionTuning tuning = _dataTemplate != null
                ? _dataTemplate.BuildSpeciesCognitionTuning()
                : new SpeciesCognitionTuning(
                    1f,
                    ResolveFearWeight(),
                    1f,
                    FaunaLightReactionMode.None,
                    1f,
                    0.65f,
                    1f,
                    0f);
            input.HungerWeight = tuning.HungerWeight;
            input.ThreatWeight = 1f + (ResolveAggressionWeight(aggressionMultiplier) * 0.45f);
            input.FearWeight = tuning.FearWeight;
            input.CuriosityWeight = tuning.CuriosityWeight;
            input.PlayerLightExposure01 = math.saturate(context.PlayerLightExposure01);
            input.LightReactionMode = (int)tuning.LightReactionMode;
            input.LightFrenzySpeedMultiplier = tuning.LightFrenzySpeedMultiplier;
            input.LightReactionFearBoost01 = tuning.LightReactionFearBoost01;
            input.AggressionWeight = ResolveAttackSpeedWeight(aggressionMultiplier);
            input.EscapeDistance = math.max(0f, context.EscapeDistance);
            input.EscapeSafeDistance = math.max(input.EscapeDistance, context.EscapeSafeDistance);
            input.WanderRadius = math.max(1f, context.WanderRadius);
            input.PatrolRadius = math.max(1f, context.PatrolRadius);
            input.ApexTerritoryRadius = math.max(0f, context.ApexTerritoryRadius);
            input.ApexAggressionMultiplier = math.max(1f, context.ApexAggressionMultiplier);
            input.PackCoordinationRadius = packCoordinationRadius;
            input.PackFlankDistance = packFlankDistance;
            input.PackCommitDistance = packCommitDistance;
            input.ImportanceScore = math.saturate(context.FoveatedImportanceScore);
            input.SpeciesId = ResolveSpeciesId();
            input.ClaimedBoidIndex = -1;
            input.FlockCount = math.max(0, context.FlockCount);
            input.Flags = (int)CognitionInputFlags.Active;
            if (UsesPredatorRole)
                input.Flags |= (int)CognitionInputFlags.PredatorRole;
            if (context.CanFlee)
                input.Flags |= (int)CognitionInputFlags.CanFlee;
            if (hasAnyPlayerTarget)
                input.Flags |= (int)CognitionInputFlags.HasPlayerTarget;
            if (context.HasThreatTarget)
                input.Flags |= (int)CognitionInputFlags.HasThreatTarget;
            if (context.HasApexRivalTarget)
                input.Flags |= (int)CognitionInputFlags.HasApexRivalTarget;
            if (context.HasPreyTarget)
                input.Flags |= (int)CognitionInputFlags.HasPreyTarget;
            if (context.HasScavengeTarget)
                input.Flags |= (int)CognitionInputFlags.HasScavengeTarget;
            if (context.UseHomeTerritory)
                input.Flags |= (int)CognitionInputFlags.UseHomeTerritory;
            if (context.IsFlocking)
                input.Flags |= (int)CognitionInputFlags.IsFlocking;
            if (context.HasScatterDirection)
                input.Flags |= (int)CognitionInputFlags.HasScatterDirection;
            if (context.IsAggressive)
                input.Flags |= (int)CognitionInputFlags.IsAggressive;
            if (context.HasVisualContact)
                input.Flags |= (int)CognitionInputFlags.HasVisualPlayerHint;
            if (context.IsApexPredator)
                input.Flags |= (int)CognitionInputFlags.IsApexPredator;
            if (UsesHighTierApexCognitionSteering(context.IsApexPredator))
                input.Flags |= (int)CognitionInputFlags.HighTierSmoothSteering;
            if ((_speciesProfile != null && _speciesProfile.isAmbusher) ||
                (_dataTemplate != null && _dataTemplate.CanBurrowAmbush))
            {
                input.Flags |= (int)CognitionInputFlags.IsAmbusher;
            }
            if (hasPackTarget)
                input.Flags |= (int)CognitionInputFlags.HasPackTarget;

            if (hasAnyPlayerTarget && context.HasVisualContact)
            {
                PredatorCognitionDomain.RecordStimulus(
                    _slot,
                    resolvedPlayerPosition,
                    currentTime,
                    1f,
                    CognitionStimulusType.Visual);
            }

            if (hasAnyPlayerTarget && acousticPingStrength01 > 0.01f)
            {
                PredatorCognitionDomain.RecordStimulus(
                    _slot,
                    resolvedPlayerPosition,
                    currentTime,
                    acousticPingStrength01 * math.max(0.25f, acousticTransmission01),
                    CognitionStimulusType.Acoustic);
            }

            if (context.HasScavengeTarget && chemicalSignal01 > 0.01f)
            {
                PredatorCognitionDomain.RecordStimulus(
                    _slot,
                    context.ScavengePosition,
                    currentTime,
                    chemicalSignal01,
                    CognitionStimulusType.Chemical);
            }

            PredatorCognitionDomain.SubmitInput(_slot, in input);

            Vector3 desiredDirection = new Vector3(output.DesiredDirection.x, output.DesiredDirection.y, output.DesiredDirection.z);
            return new CreatureUtilityEvaluation(
                desiredDirection,
                CurrentStateMask,
                (FaunaBrain.AIState)output.LegacyState,
                HungerScore,
                AggressionScore,
                FearScore,
                output.ForceMultiplier,
                output.SpeedMultiplier,
                output.TurnMultiplier,
                output.ShouldAttack != 0,
                output.EmitThreatPulse != 0,
                output.PackRoleCode,
                output.FlankingManeuverDetected != 0);
        }

        private static bool UsesHighTierApexCognitionSteering(bool isApexPredator)
        {
            if (!isApexPredator)
                return false;

            uint tierBit = 1u << (int)GlobalRegistry.ScalabilityTier;
            return (HighTierApexCognitionSteeringMask & tierBit) != 0u &&
                   GlobalRegistry.TargetMathPrecision == MathPrecisionLevel.High;
        }

        public void Dispose()
        {
            if (_initialized)
            {
                PredatorCognitionDomain.Unregister(_slot);
                _slot = -1;
            }

            _initialized = false;
        }

        private static bool IsPredatorArchetype(CreatureArchetypeData archetype, FaunaSpeciesProfile speciesProfile)
        {
            if (archetype != null)
            {
                return archetype.roleType == CreatureRoleType.Hunter ||
                       archetype.roleType == CreatureRoleType.Leviathan ||
                       archetype.isAggressive;
            }

            return speciesProfile != null && speciesProfile.isLeviathan;
        }

        private void ResolveSpeciesGenetics(
            out float chemicalSensitivity,
            out float packCoordinationRadius,
            out float packFlankDistance,
            out float packCommitDistance,
            out float aggressionMultiplier)
        {
            chemicalSensitivity = 1f;
            packCoordinationRadius = _archetype != null ? math.max(0f, _archetype.packSupportRadius) : 0f;
            packFlankDistance = _archetype != null ? math.max(0f, _archetype.packFlankDistance) : 0f;
            packCommitDistance = _archetype != null ? math.max(0f, _archetype.packCommitDistance) : 0f;
            aggressionMultiplier = 1f;

            CreatureGeneticsProfile geneticsProfile = _speciesProfile != null ? _speciesProfile.geneticsProfile : null;
            if (geneticsProfile == null || !geneticsProfile.TryResolveSpeciesTuning(ResolveSpeciesId(), out CreatureGeneticsProfile.SpeciesGeneticsTuning tuning))
                return;

            if (tuning.scentSensitivity > 0f)
                chemicalSensitivity = tuning.scentSensitivity;

            if (tuning.packHuntingRadius > 0f)
                packCoordinationRadius = tuning.packHuntingRadius;

            if (tuning.packFlankOffset > 0f)
                packFlankDistance = tuning.packFlankOffset;

            if (tuning.baseAggressionMultiplier > 0f)
                aggressionMultiplier = tuning.baseAggressionMultiplier;
        }

        private float ResolveAggressionWeight(float aggressionMultiplier)
        {
            float aggression = _speciesProfile != null ? math.max(0.1f, _speciesProfile.baseAggro) : 0.55f;
            if (_archetype == null)
                return aggression * math.max(0f, aggressionMultiplier);

            switch (_archetype.roleType)
            {
                case CreatureRoleType.Leviathan:
                    aggression *= 1.45f;
                    break;
                case CreatureRoleType.Hunter:
                    aggression *= 1.2f;
                    break;
            }

            if (_archetype.isAggressive)
                aggression *= 1.1f;

            return aggression * math.max(0f, aggressionMultiplier);
        }

        private float ResolveFearWeight()
        {
            if (_dataTemplate != null)
                return _dataTemplate.ResolveDriveWeight(FaunaDriveChannel.Fear, 1.5f);

            if (_speciesProfile == null)
                return 1.5f;

            return math.max(_speciesProfile.retreatSpeedMultiplier, _speciesProfile.escapeSpeedMultiplier);
        }

        private float ResolveAttackSpeedWeight(float aggressionMultiplier)
        {
            if (_speciesProfile == null)
                return 1.35f * math.max(0f, aggressionMultiplier);

            return math.max(1.15f, _speciesProfile.aggressiveSpeedMultiplier * math.max(0f, aggressionMultiplier));
        }

        private int ResolveSpeciesId()
        {
            if (_dataTemplate != null && _dataTemplate.SpeciesId != 0)
                return _dataTemplate.SpeciesId;

            if (_speciesProfile != null && _speciesProfile.speciesID != 0)
                return _speciesProfile.speciesID;

            if (_archetype != null && !string.IsNullOrWhiteSpace(_archetype.creatureId))
                return unchecked((int)Hecton.Localization.LocHash.Compute(_archetype.creatureId)) & int.MaxValue;

            return 0;
        }

        private void RegisterSpeciesTuning()
        {
            if (_dataTemplate == null)
                return;

            int speciesId = ResolveSpeciesId();
            if (speciesId == 0)
                return;

            PredatorCognitionDomain.RegisterSpeciesTuning(speciesId, _dataTemplate.BuildSpeciesCognitionTuning());
        }
    }
}
