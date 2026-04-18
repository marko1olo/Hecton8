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
            if (archetype == null)
                return;

            isAggressive = archetype.isAggressive;
            canFlee = archetype.canFlee;

            _maxHealth = Mathf.Max(1f, archetype.maxHealth);
            _currentHealth = Mathf.Clamp(_currentHealth, 0f, _maxHealth);
            if (_currentHealth <= 0.001f)
                _currentHealth = _maxHealth;

            _sensorSuite.aggroDistance = Mathf.Max(0f, archetype.baseAggroDistance);
            _sensorSuite.deaggroDistance = Mathf.Max(_sensorSuite.aggroDistance, archetype.baseDeaggroDistance);
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

            _steeringEngine.maxSpeed = Mathf.Max(0.1f, archetype.cruiseSpeed);
            _steeringEngine.turnSpeed = Mathf.Max(0.1f, archetype.turnSpeed);
            _steeringEngine.swimForce = Mathf.Max(_steeringEngine.maxSpeed, archetype.burstSpeed);
        }

        public void SetSpawnPoint(Vector3 spawnPoint)
        {
            _spawnPoint = spawnPoint;
            transform.position = spawnPoint;
        }

        public void ForceState(AIState state)
        {
            _stateMachine.currentState = state;
        }
    }
}
