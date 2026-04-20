using UnityEngine;

namespace Hecton8.AI
{
    [System.Serializable]
    public struct FaunaProfile
    {
        [Range(0f, 1f)] public float aggressionLevel;
        [Range(-1f, 1f)] public float curiosity; 
        [Range(0f, 1f)] public float braveThreshold;
    }

    [System.Serializable]
    public class FaunaStateMachine
    {
        [Header("── State Configuration ────────────────────────────")]
        public FaunaProfile profile = new FaunaProfile { aggressionLevel = 0.5f, curiosity = 0f, braveThreshold = 0.2f };
        public float wanderRadius = 15f;
        public float waypointReachDistance = 2f;
        public float escapeDistance = 30f;
        public float escapeSafeDistance = 50f;
        
        [Header("── Tactical Behaviors ─────────────────────────────")]
        public float stalkDuration = 10f;
        public float stalkRadius = 18f;
        public float attackRadius = 3f;
        public float attackDamage = 15f;
        public float retreatDuration = 6f;

        [Header("── Territory ─────────────────────────────────────")]
        public bool useTerritory = false;
        public float patrolRadius = 50f;

        [Header("── Flocking ──────────────────────────────────────")]
        public bool isFlockingFish = false;
        public float cohesionWeight = 1f;
        public float alignmentWeight = 1f;
        public float separationWeight = 1.5f;

        [HideInInspector] public FaunaBrain.AIState currentState = FaunaBrain.AIState.Idle;
        [HideInInspector] public float currentForceMultiplier = 1f;
        [HideInInspector] public float currentSpeedMultiplier = 1f;
        [HideInInspector] public float currentTurnMultiplier = 1f;

        private Transform _selfTransform;
        private FaunaSpeciesProfile _speciesProfile;
        private Vector3 _homePosition;
        private Vector3 _currentWanderTarget;
        private float _stateTimer;
        private float _retreatTimer;
        private float _satedTimer;
        private float _scatterTimer;
        private float _cumulativeStalkAngle;
        private Vector3 _prevStalkDir;
        private Vector3 _forcedThreatPos;
        private bool _isAmbusher;

        public event System.Action<Transform> OnAttackPerform;

        public void Init(Transform self, FaunaSpeciesProfile profile = null)
        {
            _selfTransform = self;
            _speciesProfile = profile;
            _homePosition = self.position;
            _satedTimer = 0f;
            _isAmbusher = _speciesProfile != null && _speciesProfile.isAmbusher;

            if (_isAmbusher)
                currentState = FaunaBrain.AIState.Idle;
            else
                currentState = isFlockingFish ? FaunaBrain.AIState.Flocking : FaunaBrain.AIState.Wander;

            if (currentState == FaunaBrain.AIState.Wander && !isFlockingFish) PickNewWanderTarget();
        }

        /// <summary>
        /// [REQ] Explicit API to force a retreat state, used by Provoke().
        /// </summary>
        public void ForceRetreat(Vector3 threatPos, float duration)
        {
            _forcedThreatPos = threatPos;
            _retreatTimer = duration;
            currentState = FaunaBrain.AIState.Retreat;
            _stateTimer = 0f;
        }

        public void ForceSated(float duration)
        {
            _satedTimer = duration;
            currentState = FaunaBrain.AIState.Sated;
            PickNewWanderTarget();
        }

        public Vector3 EvaluateAndGetDirection(float dt, FaunaSensorSuite sensors, bool isAggressive, bool canFlee, float healthNormalized, Vector3 selfPosition)
        {
            _stateTimer += dt;
            if (_retreatTimer > 0) _retreatTimer -= dt;
            if (_satedTimer > 0) _satedTimer -= dt;
            if (_scatterTimer > 0) _scatterTimer -= dt;

            FaunaBrain.AIState nextState = currentState;
            bool hasDirectPlayerTransform = sensors.TryGetDirectPlayerTransform(out Transform directPlayerTransform);
            bool hasPerceivedPlayerPosition = sensors.TryGetPerceivedPlayerPosition(out Vector3 perceivedPlayerPosition);

            // 1. Evaluate Transitions
            if (currentState == FaunaBrain.AIState.Sated)
            {
                if (_satedTimer <= 0) nextState = FaunaBrain.AIState.Wander;
            }
            else if (currentState == FaunaBrain.AIState.Retreat)
            {
                if (_retreatTimer <= 0) nextState = FaunaBrain.AIState.Wander;
            }
            else if (sensors.isThreatened && currentState != FaunaBrain.AIState.Aggressive && currentState != FaunaBrain.AIState.Escape)
            {
                nextState = FaunaBrain.AIState.ThreatDisplay;
            }
            else if (canFlee && sensors.canSeePlayer && (sensors.distSqrToPlayer < escapeDistance * escapeDistance || healthNormalized < profile.braveThreshold))
            {
                nextState = FaunaBrain.AIState.Escape;
            }
            else if (_isAmbusher && currentState == FaunaBrain.AIState.Idle)
            {
                if (sensors.canSeePlayer && sensors.distSqrToPlayer < _speciesProfile.ambushTriggerRange * _speciesProfile.ambushTriggerRange)
                    nextState = FaunaBrain.AIState.Aggressive;
            }
            else if (isAggressive && (sensors.canSeePlayer || sensors.currentDistractor != null || sensors.currentPrey != null || sensors.currentScavengeTarget != null))
            {
                // [REQ] Scavenger > Player > Prey
                Transform targetToEvaluate = sensors.currentScavengeTarget ?? (sensors.currentDistractor ?? directPlayerTransform);
                if (targetToEvaluate == null) targetToEvaluate = sensors.currentPrey;

                bool inTerritory = !useTerritory || (sensors.currentDistractor != null) || (sensors.currentPrey != null) ||
                                 (hasPerceivedPlayerPosition && (perceivedPlayerPosition - _homePosition).sqrMagnitude <= (patrolRadius * patrolRadius));

                float atkRad = _speciesProfile != null ? _speciesProfile.attackRadius : attackRadius;
                int stalkPat = _speciesProfile != null ? _speciesProfile.stalkingPatience : 3;

                if (inTerritory && (targetToEvaluate != null || hasPerceivedPlayerPosition))
                {
                    if (currentState != FaunaBrain.AIState.Aggressive && currentState != FaunaBrain.AIState.Stalk)
                    {
                        nextState = FaunaBrain.AIState.Stalk;
                        _cumulativeStalkAngle = 0f;
                        _prevStalkDir = Vector3.zero;
                    }
                    else if (currentState == FaunaBrain.AIState.Stalk)
                    {
                        bool patienceDepleted = (_cumulativeStalkAngle >= (stalkPat * 360f));
                        if (stalkPat <= 0 && _stateTimer > stalkDuration) patienceDepleted = true;
                        
                        // Prey results in immediate aggression (hunger)
                        if (sensors.currentPrey != null && !hasDirectPlayerTransform) patienceDepleted = true;

                        if (patienceDepleted && sensors.currentDistractor == null)
                            nextState = FaunaBrain.AIState.Aggressive;
                    }
                }
                else
                    nextState = useTerritory ? FaunaBrain.AIState.Return : FaunaBrain.AIState.Wander;

                if (currentState == FaunaBrain.AIState.Aggressive)
                {
                    Transform target = sensors.currentScavengeTarget ?? (sensors.currentDistractor ?? (directPlayerTransform ?? sensors.currentPrey));
                    if (target != null && Vector3.SqrMagnitude(target.position - selfPosition) <= atkRad * atkRad)
                    {
                        float retDur = _speciesProfile != null ? _speciesProfile.retreatDuration : retreatDuration;
                        OnAttackPerform?.Invoke(target);
                        _retreatTimer = retDur;
                        nextState = FaunaBrain.AIState.Retreat;
                    }
                }
            }
            else if (currentState == FaunaBrain.AIState.Escape && sensors.distSqrToPlayer > escapeSafeDistance * escapeSafeDistance)
            {
                nextState = isFlockingFish ? FaunaBrain.AIState.Flocking : FaunaBrain.AIState.Wander;
            }
            else if (useTerritory && currentState != FaunaBrain.AIState.Escape && currentState != FaunaBrain.AIState.Aggressive && currentState != FaunaBrain.AIState.Stalk && currentState != FaunaBrain.AIState.Sated)
            {
                if ((selfPosition - _homePosition).sqrMagnitude > patrolRadius * patrolRadius)
                    nextState = FaunaBrain.AIState.Return;
            }

            if (nextState != currentState)
            {
                currentState = nextState;
                _stateTimer = 0f;
                if (currentState == FaunaBrain.AIState.Wander || currentState == FaunaBrain.AIState.Sated) PickNewWanderTarget();
            }

            // 2. State Logic
            Vector3 desiredDir = _selfTransform.forward;
            currentForceMultiplier = 1f; currentSpeedMultiplier = 1f; currentTurnMultiplier = 1f;

            switch (currentState)
            {
                case FaunaBrain.AIState.Sated:
                    currentSpeedMultiplier = 0.6f;
                    currentTurnMultiplier = 0.5f;
                    if (Vector3.SqrMagnitude(_currentWanderTarget - selfPosition) < waypointReachDistance * waypointReachDistance) PickNewWanderTarget();
                    desiredDir = (_currentWanderTarget - selfPosition).normalized;
                    break;
                case FaunaBrain.AIState.Wander:
                    if (Vector3.SqrMagnitude(_currentWanderTarget - selfPosition) < waypointReachDistance * waypointReachDistance) PickNewWanderTarget();
                    desiredDir = (_currentWanderTarget - selfPosition).normalized;
                    break;
                case FaunaBrain.AIState.Threaten:
                    currentSpeedMultiplier = 0.5f;
                    if (hasPerceivedPlayerPosition)
                    {
                        Vector3 toPlayer = (perceivedPlayerPosition - selfPosition).normalized;
                        // Face the player but maintain distance
                        desiredDir = Vector3.Lerp(-toPlayer, Vector3.Cross(Vector3.up, toPlayer), 0.5f);
                    }
                    else desiredDir = _selfTransform.forward;
                    break;
                case FaunaBrain.AIState.ThreatDisplay:
                    // [REQ] Carnivore Disputes: Apply Repulsion Force
                    currentForceMultiplier = 1.5f;
                    currentSpeedMultiplier = 0.8f;
                    if (sensors.currentThreat != null)
                    {
                        Vector3 toThreat = (sensors.currentThreat.position - selfPosition).normalized;
                        // Repulsion: Fleeing direction with a slight sideways "posturing" component
                        desiredDir = Vector3.Lerp(-toThreat, Vector3.Cross(Vector3.up, toThreat), 0.3f).normalized;
                    }
                    break;
                case FaunaBrain.AIState.Stalk:
                    Transform t = sensors.currentScavengeTarget ?? (sensors.currentDistractor ?? (directPlayerTransform ?? sensors.currentPrey));
                    if (t != null)
                    {
                        Vector3 toT = (t.position - selfPosition).normalized;
                        desiredDir = Vector3.Cross(Vector3.up, toT).normalized; // Circling
                        desiredDir = Vector3.Lerp(desiredDir, toT * (Vector3.Distance(t.position, selfPosition) > stalkRadius ? 1 : -1), 0.5f).normalized;

                        if (_prevStalkDir != Vector3.zero)
                        {
                            float angleDelta = Vector3.Angle(_prevStalkDir, toT);
                            _cumulativeStalkAngle += angleDelta;
                        }
                        _prevStalkDir = toT;
                    }
                    else if (hasPerceivedPlayerPosition)
                    {
                        Vector3 toT = (perceivedPlayerPosition - selfPosition).normalized;
                        desiredDir = Vector3.Cross(Vector3.up, toT).normalized;
                        desiredDir = Vector3.Lerp(desiredDir, toT * (Vector3.Distance(perceivedPlayerPosition, selfPosition) > stalkRadius ? 1 : -1), 0.5f).normalized;
                    }
                    break;
                case FaunaBrain.AIState.Aggressive:
                    currentForceMultiplier = 2f; 
                    currentSpeedMultiplier = _speciesProfile != null ? _speciesProfile.aggressiveSpeedMultiplier : 1.3f;
                    Transform target = sensors.currentScavengeTarget ?? (sensors.currentDistractor ?? (directPlayerTransform ?? sensors.currentPrey));
                    if (target != null)
                        desiredDir = (target.position - selfPosition).normalized;
                    else if (hasPerceivedPlayerPosition)
                        desiredDir = (perceivedPlayerPosition - selfPosition).normalized;
                    break;
                case FaunaBrain.AIState.Retreat:
                    currentForceMultiplier = 2.5f; 
                    currentSpeedMultiplier = _speciesProfile != null ? _speciesProfile.retreatSpeedMultiplier : 1.5f; 
                    Transform retreatTr = sensors.currentDistractor ?? directPlayerTransform;
                    Vector3 fleeFromPos = retreatTr != null ? retreatTr.position : hasPerceivedPlayerPosition ? perceivedPlayerPosition : _forcedThreatPos;
                    desiredDir = (selfPosition - fleeFromPos).normalized; 
                    break;
                case FaunaBrain.AIState.Escape:
                    currentForceMultiplier = 2.5f; 
                    currentSpeedMultiplier = _speciesProfile != null ? _speciesProfile.escapeSpeedMultiplier : 2f;
                    if (hasPerceivedPlayerPosition)
                        desiredDir = (selfPosition - perceivedPlayerPosition).normalized;
                    break;
                case FaunaBrain.AIState.Flocking:
                    if (sensors.isScattering)
                    {
                        _scatterTimer = 3f; // [REQ] 3 seconds evasion
                        sensors.isScattering = false; // Signal consumed
                    }

                    if (_scatterTimer > 0)
                    {
                        currentForceMultiplier = 4f;
                        currentSpeedMultiplier = 2f;
                        desiredDir = sensors.scatterDirection;
                    }
                    else if (sensors.flockCount > 1)
                    {
                        desiredDir = (sensors.flockCenter - selfPosition).normalized * cohesionWeight + sensors.flockDirection * alignmentWeight + sensors.flockAvoidance * separationWeight;
                    }
                    break;
                case FaunaBrain.AIState.Return:
                    desiredDir = (_homePosition - selfPosition).normalized;
                    break;
            }

            return desiredDir.normalized;
        }

        private void PickNewWanderTarget()
        {
            Vector3 center = useTerritory ? _homePosition : _selfTransform.position;
            float rad = useTerritory ? patrolRadius : wanderRadius;
            _currentWanderTarget = center + Random.onUnitSphere * rad;
        }
    }
}
