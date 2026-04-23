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
            _stateMachine.wanderRadius = archetype.useHomeTerritory
                ? Mathf.Max(1f, archetype.homeWanderRadius)
                : _stateMachine.wanderRadius;

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
            _utilityBrain.ApplyExternalState(state, Time.time);
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
    /// Immutable input snapshot consumed by <see cref="CreatureUtilityBrain"/>.
    /// </summary>
    public readonly struct CreatureUtilityContext
    {
        public CreatureUtilityContext(
            Vector3 selfPosition,
            Vector3 selfForward,
            float healthNormalized,
            bool canFlee,
            bool hasVisualContact,
            bool hasPerceivedPlayerPosition,
            Vector3 perceivedPlayerPosition,
            float distanceToPlayerSqr,
            float attackRange,
            float fearPressure01)
        {
            SelfPosition = selfPosition;
            SelfForward = selfForward;
            HealthNormalized = healthNormalized;
            CanFlee = canFlee;
            HasVisualContact = hasVisualContact;
            HasPerceivedPlayerPosition = hasPerceivedPlayerPosition;
            PerceivedPlayerPosition = perceivedPlayerPosition;
            DistanceToPlayerSqr = distanceToPlayerSqr;
            AttackRange = attackRange;
            FearPressure01 = fearPressure01;
        }

        public Vector3 SelfPosition { get; }
        public Vector3 SelfForward { get; }
        public float HealthNormalized { get; }
        public bool CanFlee { get; }
        public bool HasVisualContact { get; }
        public bool HasPerceivedPlayerPosition { get; }
        public Vector3 PerceivedPlayerPosition { get; }
        public float DistanceToPlayerSqr { get; }
        public float AttackRange { get; }
        public float FearPressure01 { get; }
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
    /// Large-predator utility kernel with native spatial memory.
    /// </summary>
    public sealed class CreatureUtilityBrain
    {
        private const int MemoryCapacity = 16;
        private const float MemoryLifetimeSeconds = 45f;
        private const float LostVisualMemoryDelaySeconds = 10f;
        private const float MinimumDistanceMeters = 1.25f;
        private const float ProwlTargetRefreshSeconds = 4.5f;
        private const float AttackStateBias = 1.25f;
        private const float OverrideScoreBias = 1000f;
        private const float MinimumAttackCooldown = 0.35f;
        private const float MinimumProwlRadius = 10f;
        private const float MaximumProwlRadius = 24f;
        private const float MaximumProwlVerticalOffset = 6f;

        private PredatorMemory _memory;
        private CreatureArchetypeData _archetype;
        private FaunaSpeciesProfile _speciesProfile;
        private float3 _spawnAnchor;
        private float3 _prowlTarget;
        private float3 _overrideThreatPosition;
        private float _lastVisualContactTime = float.NegativeInfinity;
        private float _overrideUntilTime = float.NegativeInfinity;
        private float _nextProwlTargetRefreshTime = float.NegativeInfinity;
        private float _nextAttackAllowedTime = float.NegativeInfinity;
        private int _prowlSequence;
        private bool _initialized;
        private bool _hasProwlTarget;
        private bool _hasOverrideThreatPosition;
        private PredatorUtilityState _overrideStateMask;

        /// <summary>
        /// Current active predator-state bitmask.
        /// </summary>
        public PredatorUtilityState CurrentStateMask { get; private set; }

        /// <summary>
        /// True when the currently bound archetype should use this utility kernel.
        /// </summary>
        public bool IsActivePredator => IsPredatorArchetype(_archetype, _speciesProfile);

        /// <summary>
        /// Current hunger utility score.
        /// </summary>
        public float HungerScore { get; private set; }

        /// <summary>
        /// Current aggression utility score.
        /// </summary>
        public float AggressionScore { get; private set; }

        /// <summary>
        /// Current fear utility score.
        /// </summary>
        public float FearScore { get; private set; }

        /// <summary>
        /// Initializes the native memory store and binds the initial world anchor.
        /// </summary>
        /// <param name="spawnAnchor">Initial patrol anchor.</param>
        /// <param name="speciesProfile">Current species profile.</param>
        /// <param name="archetype">Current archetype.</param>
        public void Initialize(Vector3 spawnAnchor, FaunaSpeciesProfile speciesProfile, CreatureArchetypeData archetype)
        {
            if (!_initialized)
            {
                _memory.Initialize(MemoryCapacity);
                _initialized = true;
            }

            _speciesProfile = speciesProfile;
            _archetype = archetype;
            _spawnAnchor = spawnAnchor;
            _prowlTarget = spawnAnchor;
        }

        /// <summary>
        /// Rebinds authored predator data after archetype changes.
        /// </summary>
        /// <param name="speciesProfile">Current species profile.</param>
        /// <param name="archetype">Current archetype.</param>
        public void BindProfile(FaunaSpeciesProfile speciesProfile, CreatureArchetypeData archetype)
        {
            _speciesProfile = speciesProfile;
            _archetype = archetype;
        }

        /// <summary>
        /// Updates the authored spawn anchor used for prowling when no memory target is active.
        /// </summary>
        /// <param name="spawnAnchor">World-space spawn anchor.</param>
        public void SetSpawnAnchor(Vector3 spawnAnchor)
        {
            _spawnAnchor = spawnAnchor;
        }

        /// <summary>
        /// Clears transient utility state while preserving the allocated native memory buffer.
        /// </summary>
        /// <param name="spawnAnchor">Fresh runtime spawn anchor.</param>
        public void ResetRuntimeState(Vector3 spawnAnchor)
        {
            _spawnAnchor = spawnAnchor;
            _prowlTarget = spawnAnchor;
            _memory.Clear();
            _prowlSequence = 0;
            _hasProwlTarget = false;
            _hasOverrideThreatPosition = false;
            _lastVisualContactTime = float.NegativeInfinity;
            _overrideUntilTime = float.NegativeInfinity;
            _nextProwlTargetRefreshTime = float.NegativeInfinity;
            _nextAttackAllowedTime = float.NegativeInfinity;
            _overrideStateMask = PredatorUtilityState.None;
            CurrentStateMask = PredatorUtilityState.None;
            HungerScore = 0f;
            AggressionScore = 0f;
            FearScore = 0f;
        }

        /// <summary>
        /// Records an auditory player stimulus into predator memory.
        /// </summary>
        /// <param name="worldPosition">Stimulus position.</param>
        /// <param name="timeStamp">Authored timestamp in seconds.</param>
        public void RecordAuditoryStimulus(Vector3 worldPosition, float timeStamp)
        {
            if (!_initialized || !IsActivePredator)
                return;

            _memory.Record(worldPosition, timeStamp);
        }

        /// <summary>
        /// Applies a legacy external state override from older fauna orchestration.
        /// </summary>
        /// <param name="state">Legacy fauna state.</param>
        /// <param name="currentTime">Current authored time in seconds.</param>
        public void ApplyExternalState(FaunaBrain.AIState state, float currentTime)
        {
            if (!IsActivePredator)
                return;

            switch (state)
            {
                case FaunaBrain.AIState.Aggressive:
                    _overrideStateMask = PredatorUtilityState.Attacking;
                    _overrideUntilTime = currentTime + 4f;
                    break;
                case FaunaBrain.AIState.Retreat:
                case FaunaBrain.AIState.Escape:
                    _overrideStateMask = PredatorUtilityState.Fleeing;
                    _overrideUntilTime = currentTime + 4f;
                    break;
                default:
                    _overrideStateMask = PredatorUtilityState.Prowling;
                    _overrideUntilTime = currentTime + 4f;
                    break;
            }
        }

        /// <summary>
        /// Applies a forced flee order from gameplay pressure systems.
        /// </summary>
        /// <param name="threatPosition">World-space threat position.</param>
        /// <param name="currentTime">Current authored time in seconds.</param>
        /// <param name="duration">Override duration in seconds.</param>
        public void ForceRetreat(Vector3 threatPosition, float currentTime, float duration)
        {
            if (!IsActivePredator)
                return;

            _overrideStateMask = PredatorUtilityState.Fleeing;
            _overrideThreatPosition = threatPosition;
            _hasOverrideThreatPosition = true;
            _overrideUntilTime = currentTime + math.max(0.1f, duration);
        }

        /// <summary>
        /// Starts the attack cooldown after an attack is executed.
        /// </summary>
        /// <param name="currentTime">Current authored time in seconds.</param>
        /// <param name="cooldownSeconds">Attack cooldown duration.</param>
        public void NotifyAttackPerformed(float currentTime, float cooldownSeconds)
        {
            _nextAttackAllowedTime = currentTime + math.max(MinimumAttackCooldown, cooldownSeconds);
        }

        /// <summary>
        /// Evaluates the current utility scores and resolves the winning state bit.
        /// </summary>
        /// <param name="dt">Dispatcher delta time.</param>
        /// <param name="currentTime">Current authored time in seconds.</param>
        /// <param name="context">Current sensory context.</param>
        /// <returns>Resolved evaluation result.</returns>
        public CreatureUtilityEvaluation Evaluate(float dt, float currentTime, in CreatureUtilityContext context)
        {
            if (!IsActivePredator)
            {
                return new CreatureUtilityEvaluation(
                    context.SelfForward,
                    PredatorUtilityState.None,
                    FaunaBrain.AIState.Wander,
                    0f,
                    0f,
                    0f,
                    1f,
                    1f,
                    1f,
                    false);
            }

            if (context.HasVisualContact && context.HasPerceivedPlayerPosition)
            {
                _lastVisualContactTime = currentTime;
                _memory.Record(context.PerceivedPlayerPosition, currentTime);
            }

            float3 selfPosition = context.SelfPosition;
            float3 targetPosition = selfPosition + ((float3)context.SelfForward * 4f);
            bool hasTarget = false;

            if (context.HasPerceivedPlayerPosition)
            {
                targetPosition = context.PerceivedPlayerPosition;
                hasTarget = true;
            }
            else if (currentTime - _lastVisualContactTime >= LostVisualMemoryDelaySeconds &&
                     _memory.TryGetHighestWeightedPosition(currentTime, selfPosition, MemoryLifetimeSeconds, out float3 memoryPosition, out float memoryWeight) &&
                     memoryWeight > 0f)
            {
                targetPosition = memoryPosition;
                hasTarget = true;
            }
            else
            {
                RefreshProwlTarget(currentTime, selfPosition);
                if (_hasProwlTarget)
                {
                    targetPosition = _prowlTarget;
                    hasTarget = true;
                }
            }

            float targetDistanceSqr = math.lengthsq(targetPosition - selfPosition);
            float referenceDistance = context.HasPerceivedPlayerPosition
                ? math.sqrt(math.max(context.DistanceToPlayerSqr, MinimumDistanceMeters * MinimumDistanceMeters))
                : math.sqrt(math.max(targetDistanceSqr, MinimumDistanceMeters * MinimumDistanceMeters));

            float aggressionFactor = ResolveAggressionFactor();
            AggressionScore = math.pow(referenceDistance, -2.0f) * aggressionFactor;

            float hungerInput = math.saturate(0.45f + aggressionFactor * 0.35f + (hasTarget ? 0.15f : 0f));
            HungerScore = math.pow(hungerInput, 2f);
            if (currentTime - _lastVisualContactTime >= LostVisualMemoryDelaySeconds && hasTarget)
                HungerScore *= 1.2f;

            float fearThreshold = _speciesProfile != null ? math.max(0.05f, _speciesProfile.fearThreshold) : 0.2f;
            float injury01 = 1f - math.saturate(context.HealthNormalized);
            float fearInput = math.saturate((injury01 / fearThreshold) + context.FearPressure01);
            FearScore = context.CanFlee ? math.pow(fearInput, 2f) : 0f;

            float attackCommit01 = hasTarget
                ? math.pow(math.saturate(1f - (math.sqrt(math.max(targetDistanceSqr, 0f)) / math.max(context.AttackRange, 1f))), 2f)
                : 0f;

            float prowlingScore = HungerScore;
            float stalkingScore = AggressionScore;
            float attackingScore = AggressionScore * attackCommit01 * AttackStateBias;
            float fleeingScore = FearScore;

            if (_overrideUntilTime > currentTime)
            {
                switch (_overrideStateMask)
                {
                    case PredatorUtilityState.Prowling:
                        prowlingScore += OverrideScoreBias;
                        break;
                    case PredatorUtilityState.Stalking:
                        stalkingScore += OverrideScoreBias;
                        break;
                    case PredatorUtilityState.Attacking:
                        attackingScore += OverrideScoreBias;
                        break;
                    case PredatorUtilityState.Fleeing:
                        fleeingScore += OverrideScoreBias;
                        break;
                }
            }

            PredatorUtilityState stateMask = PredatorUtilityState.Prowling;
            float winningScore = prowlingScore;
            if (stalkingScore > winningScore)
            {
                stateMask = PredatorUtilityState.Stalking;
                winningScore = stalkingScore;
            }

            if (attackingScore > winningScore)
            {
                stateMask = PredatorUtilityState.Attacking;
                winningScore = attackingScore;
            }

            if (fleeingScore > winningScore)
                stateMask = PredatorUtilityState.Fleeing;

            CurrentStateMask = stateMask;

            float3 desiredDirection = ResolveDesiredDirection(stateMask, selfPosition, targetPosition, context.SelfForward, hasTarget, currentTime);
            FaunaBrain.AIState legacyState = MapLegacyState(stateMask);
            float forceMultiplier = 1f;
            float speedMultiplier = 1f;
            float turnMultiplier = 1f;
            bool shouldAttack = false;

            switch (stateMask)
            {
                case PredatorUtilityState.Prowling:
                    forceMultiplier = 1.05f;
                    speedMultiplier = 0.95f;
                    turnMultiplier = 0.9f;
                    break;
                case PredatorUtilityState.Stalking:
                    forceMultiplier = 1.35f;
                    speedMultiplier = 1.15f;
                    turnMultiplier = 1.1f;
                    break;
                case PredatorUtilityState.Attacking:
                    forceMultiplier = 2.15f;
                    speedMultiplier = _speciesProfile != null ? math.max(1.15f, _speciesProfile.aggressiveSpeedMultiplier) : 1.35f;
                    turnMultiplier = 1.2f;
                    shouldAttack = context.HasVisualContact &&
                                   hasTarget &&
                                   targetDistanceSqr <= math.max(1f, context.AttackRange * context.AttackRange) &&
                                   currentTime >= _nextAttackAllowedTime;
                    break;
                case PredatorUtilityState.Fleeing:
                    forceMultiplier = 2.4f;
                    speedMultiplier = _speciesProfile != null
                        ? math.max(_speciesProfile.retreatSpeedMultiplier, _speciesProfile.escapeSpeedMultiplier)
                        : 1.75f;
                    turnMultiplier = 1.15f;
                    break;
            }

            return new CreatureUtilityEvaluation(
                desiredDirection,
                stateMask,
                legacyState,
                HungerScore,
                AggressionScore,
                FearScore,
                forceMultiplier,
                speedMultiplier,
                turnMultiplier,
                shouldAttack);
        }

        /// <summary>
        /// Releases the native memory ring buffer.
        /// </summary>
        public void Dispose()
        {
            _memory.Dispose();
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

        private float ResolveAggressionFactor()
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

        private void RefreshProwlTarget(float currentTime, float3 selfPosition)
        {
            if (_hasProwlTarget &&
                currentTime < _nextProwlTargetRefreshTime &&
                math.lengthsq(_prowlTarget - selfPosition) > 9f)
            {
                return;
            }

            float phase = currentTime * 0.73f + (_prowlSequence * 2.39996323f);
            float radiusT = math.frac(_prowlSequence * 0.61803398875f);
            float radius = math.lerp(MinimumProwlRadius, MaximumProwlRadius, radiusT);
            float verticalT = math.frac(_prowlSequence * 0.41421356f) - 0.5f;
            float verticalOffset = verticalT * MaximumProwlVerticalOffset;

            _prowlTarget = _spawnAnchor + new float3(
                math.cos(phase) * radius,
                verticalOffset,
                math.sin(phase) * radius);
            _prowlSequence++;
            _hasProwlTarget = true;
            _nextProwlTargetRefreshTime = currentTime + ProwlTargetRefreshSeconds;
        }

        private float3 ResolveDesiredDirection(
            PredatorUtilityState stateMask,
            float3 selfPosition,
            float3 targetPosition,
            Vector3 fallbackForward,
            bool hasTarget,
            float currentTime)
        {
            float3 fallbackDirection = math.normalizesafe((float3)fallbackForward, new float3(0f, 0f, 1f));
            if (!hasTarget)
                return fallbackDirection;

            float3 toTarget = math.normalizesafe(targetPosition - selfPosition, fallbackDirection);
            if (stateMask == PredatorUtilityState.Fleeing)
            {
                float3 fleeFrom = _hasOverrideThreatPosition && _overrideUntilTime > currentTime
                    ? _overrideThreatPosition
                    : targetPosition;
                return math.normalizesafe(selfPosition - fleeFrom, -fallbackDirection);
            }

            return toTarget;
        }

        private static FaunaBrain.AIState MapLegacyState(PredatorUtilityState stateMask)
        {
            switch (stateMask)
            {
                case PredatorUtilityState.Stalking:
                    return FaunaBrain.AIState.Stalk;
                case PredatorUtilityState.Attacking:
                    return FaunaBrain.AIState.Aggressive;
                case PredatorUtilityState.Fleeing:
                    return FaunaBrain.AIState.Retreat;
                default:
                    return FaunaBrain.AIState.Wander;
            }
        }
    }
}
