using System;
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
            _utilityBrain.BindProfile(_speciesProfile, _archetype);
            if (archetype == null)
                return;

            isAggressive = archetype.isAggressive;
            canFlee = archetype.canFlee;
            _lootProfileId = archetype.lootProfileId ?? string.Empty;

            _baseMaxHealth = Mathf.Max(1f, archetype.maxHealth);
            _maxHealth = _baseMaxHealth;
            _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
            if (_currentHealth <= 0.001f)
                _currentHealth = _maxHealth;

            _baseAggroDistance = Mathf.Max(0f, archetype.baseAggroDistance);
            _baseDeaggroDistance = Mathf.Max(_baseAggroDistance, archetype.baseDeaggroDistance);
            _baseAttackDamage = Mathf.Max(0f, archetype.attackDamage);
            _baseCruiseSpeed = Mathf.Max(0.1f, archetype.cruiseSpeed);
            _baseBurstSpeed = Mathf.Max(_baseCruiseSpeed, archetype.burstSpeed);
            _baseTurnSpeed = Mathf.Max(0.1f, archetype.turnSpeed);

            _sensorSuite.aggroDistance = _baseAggroDistance;
            _sensorSuite.deaggroDistance = _baseDeaggroDistance;
            _sensorSuite.sleepDistance = Mathf.Max(1f, archetype.sleepDistance);
            _sensorSuite.reactToPlayerNoise = archetype.reactToPlayerNoise;
            _sensorSuite.reactToPlayerLight = archetype.reactToPlayerLight;

            _stateMachine.escapeDistance = Mathf.Max(0f, archetype.baseEscapeDistance);
            _stateMachine.escapeSafeDistance = Mathf.Max(_stateMachine.escapeDistance, archetype.baseEscapeSafeDistance);
            _stateMachine.stalkDuration = Mathf.Max(0f, archetype.stalkDuration);
            _stateMachine.stalkRadius = Mathf.Max(1f, archetype.stalkDistance);
            _stateMachine.useTerritory = archetype.useHomeTerritory;
            _stateMachine.patrolRadius = Mathf.Max(1f, archetype.homeReturnDistance);
            _stateMachine.wanderRadius = archetype.useHomeTerritory
                ? Mathf.Max(1f, archetype.homeWanderRadius)
                : Mathf.Max(1f, _stateMachine.wanderRadius);

            _steeringEngine.moveSpeed = _baseCruiseSpeed;
            _steeringEngine.maxSpeed = _baseCruiseSpeed;
            _steeringEngine.turnSpeed = _baseTurnSpeed;
            _steeringEngine.swimForce = Mathf.Max(_baseCruiseSpeed, _baseBurstSpeed);
            ApplyRuntimeEcosystemOverlays();
        }

        public void SetSpawnPoint(Vector3 spawnPoint)
        {
            _spawnPoint = spawnPoint;
            transform.position = spawnPoint;
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
            Vector3 threatPosition,
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
            float escapeDistance,
            float escapeSafeDistance,
            float wanderRadius,
            float patrolRadius,
            float foveatedImportanceScore,
            int flockCount,
            bool canFlee,
            bool hasVisualContact,
            bool hasPlayerTarget,
            bool hasThreatTarget,
            bool hasPreyTarget,
            bool hasScavengeTarget,
            bool useHomeTerritory,
            bool isFlocking,
            bool hasScatterDirection,
            bool isAggressive)
        {
            SelfPosition = selfPosition;
            SelfVelocity = selfVelocity;
            SelfForward = selfForward;
            PlayerPosition = playerPosition;
            ThreatPosition = threatPosition;
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
            EscapeDistance = escapeDistance;
            EscapeSafeDistance = escapeSafeDistance;
            WanderRadius = wanderRadius;
            PatrolRadius = patrolRadius;
            FoveatedImportanceScore = foveatedImportanceScore;
            FlockCount = flockCount;
            CanFlee = canFlee;
            HasVisualContact = hasVisualContact;
            HasPlayerTarget = hasPlayerTarget;
            HasThreatTarget = hasThreatTarget;
            HasPreyTarget = hasPreyTarget;
            HasScavengeTarget = hasScavengeTarget;
            UseHomeTerritory = useHomeTerritory;
            IsFlocking = isFlocking;
            HasScatterDirection = hasScatterDirection;
            IsAggressive = isAggressive;
        }

        public Vector3 SelfPosition { get; }
        public Vector3 SelfVelocity { get; }
        public Vector3 SelfForward { get; }
        public Vector3 PlayerPosition { get; }
        public Vector3 ThreatPosition { get; }
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
        public float EscapeDistance { get; }
        public float EscapeSafeDistance { get; }
        public float WanderRadius { get; }
        public float PatrolRadius { get; }
        public float FoveatedImportanceScore { get; }
        public int FlockCount { get; }
        public bool CanFlee { get; }
        public bool HasVisualContact { get; }
        public bool HasPlayerTarget { get; }
        public bool HasThreatTarget { get; }
        public bool HasPreyTarget { get; }
        public bool HasScavengeTarget { get; }
        public bool UseHomeTerritory { get; }
        public bool IsFlocking { get; }
        public bool HasScatterDirection { get; }
        public bool IsAggressive { get; }
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
            bool shouldAttack)
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
            _slots = new NativeArray<float4>(safeCapacity, Allocator.Persistent, NativeArrayOptions.ClearMemory);
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
                float distanceWeight = math.rsqrt(math.max(math.lengthsq(toMemory), 1f));
                float candidateWeight = math.pow(age01, 2f) * distanceWeight;
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
        private CreatureArchetypeData _archetype;
        private FaunaSpeciesProfile _speciesProfile;
        private int _slot;
        private bool _initialized;

        public PredatorUtilityState CurrentStateMask { get; private set; }
        public bool UsesPredatorRole => IsPredatorArchetype(_archetype, _speciesProfile);
        public bool IsActivePredator => UsesPredatorRole;
        public float HungerScore { get; private set; }
        public float AggressionScore { get; private set; }
        public float FearScore { get; private set; }
        public bool IsRegistered => _initialized && _slot >= 0;

        public void Initialize(Vector3 spawnAnchor, FaunaSpeciesProfile speciesProfile, CreatureArchetypeData archetype)
        {
            _speciesProfile = speciesProfile;
            _archetype = archetype;
            if (!_initialized)
                _slot = PredatorCognitionDomain.Register();

            _initialized = _slot >= 0;
            if (_initialized)
                PredatorCognitionDomain.ResetSlot(_slot, spawnAnchor, ResolveSpeciesId());
        }

        public void BindProfile(FaunaSpeciesProfile speciesProfile, CreatureArchetypeData archetype)
        {
            _speciesProfile = speciesProfile;
            _archetype = archetype;
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
            if (_initialized)
                PredatorCognitionDomain.ResetSlot(_slot, spawnAnchor, ResolveSpeciesId());
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
                    PredatorCognitionDomain.ApplyExternalState(_slot, PredatorUtilityState.Attacking, currentTime);
                    break;
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

        public void NotifyAttackPerformed(float currentTime, float cooldownSeconds)
        {
            if (_initialized)
                PredatorCognitionDomain.NotifyAttackPerformed(_slot, currentTime, cooldownSeconds);
        }

        public CreatureUtilityEvaluation Evaluate(int frameId, float dt, float currentTime, in CreatureUtilityContext context)
        {
            if (!_initialized)
                Initialize(context.SelfPosition, _speciesProfile, _archetype);

            float3 fallbackForward = (float3)context.SelfForward;
            float acousticPingStrength01 = 0f;
            float acousticTransmission01 = 0f;
            bool hasNoisePlayerTarget = false;
            Vector3 noisePlayerPosition = default;
            if (NoiseSystem.TryGetPlayerSignal(out NoiseSystem.PlayerNoiseSignal playerNoise))
            {
                hasNoisePlayerTarget = true;
                noisePlayerPosition = playerNoise.Position;
                float movementSpeed = math.sqrt(math.max(0f, playerNoise.MovementSpeedSqr));
                float movement01 = math.saturate(movementSpeed / 8.5f);
                float tool01 = math.saturate(playerNoise.ToolUseNoise01);
                float transport01 = math.saturate(playerNoise.TransportBoost01 * math.max(1f, playerNoise.TransportSignature));
                float flashlight01 = playerNoise.FlashlightOn ? 0.2f : 0f;
                acousticPingStrength01 = math.saturate(math.max(movement01, math.max(tool01, transport01)) + flashlight01);
                acousticTransmission01 = math.saturate(playerNoise.AcousticTransmission01);
            }

            Vector3 resolvedPlayerPosition = context.HasPlayerTarget ? context.PlayerPosition : noisePlayerPosition;
            bool hasAnyPlayerTarget = context.HasPlayerTarget || hasNoisePlayerTarget;

            float chemicalSignal01 = 0f;
            if (context.HasScavengeTarget)
            {
                float scavengeDistanceSq = math.lengthsq((float3)(context.ScavengePosition - context.SelfPosition));
                chemicalSignal01 = math.saturate(1f / (1f + (scavengeDistanceSq / (28f * 28f))));
            }

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
            input.ThreatPosition = context.ThreatPosition;
            input.PreyPosition = context.PreyPosition;
            input.ScavengePosition = context.ScavengePosition;
            input.FlockCenter = context.FlockCenter;
            input.FlockDirection = context.FlockDirection;
            input.FlockAvoidance = context.FlockAvoidance;
            input.ScatterDirection = context.ScatterDirection;
            input.DistanceToPlayerSqr = math.max(0f, context.DistanceToPlayerSqr);
            input.AttackRange = math.max(1f, context.AttackRange);
            input.HealthNormalized = math.saturate(context.HealthNormalized);
            input.FearPressure01 = math.saturate(context.FearPressure01);
            input.DeltaTime = math.max(0f, dt);
            input.CurrentTime = currentTime;
            input.AcousticPingStrength01 = acousticPingStrength01;
            input.AcousticTransmission01 = acousticTransmission01;
            input.ChemicalSignal01 = chemicalSignal01;
            input.HungerWeight = 1f;
            input.ThreatWeight = 1f + (ResolveAggressionWeight() * 0.45f);
            input.FearWeight = ResolveFearWeight();
            input.AggressionWeight = ResolveAttackSpeedWeight();
            input.EscapeDistance = math.max(0f, context.EscapeDistance);
            input.EscapeSafeDistance = math.max(input.EscapeDistance, context.EscapeSafeDistance);
            input.WanderRadius = math.max(1f, context.WanderRadius);
            input.PatrolRadius = math.max(1f, context.PatrolRadius);
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
                output.ShouldAttack != 0);
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

        private float ResolveAggressionWeight()
        {
            float aggression = _speciesProfile != null ? math.max(0.1f, _speciesProfile.baseAggro) : 0.55f;
            if (_archetype == null)
                return aggression;

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

            return aggression;
        }

        private float ResolveFearWeight()
        {
            if (_speciesProfile == null)
                return 1.5f;

            return math.max(_speciesProfile.retreatSpeedMultiplier, _speciesProfile.escapeSpeedMultiplier);
        }

        private float ResolveAttackSpeedWeight()
        {
            if (_speciesProfile == null)
                return 1.35f;

            return math.max(1.15f, _speciesProfile.aggressiveSpeedMultiplier);
        }

        private int ResolveSpeciesId()
        {
            if (_speciesProfile != null && _speciesProfile.speciesID != 0)
                return _speciesProfile.speciesID;

            if (_archetype != null)
                return ((int)_archetype.roleType << 8) | (_archetype.isAggressive ? 1 : 0);

            return 0;
        }
    }
}
