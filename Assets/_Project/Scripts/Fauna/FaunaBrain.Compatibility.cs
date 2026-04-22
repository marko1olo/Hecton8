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
        }

        public void ForceState(FaunaBrain.AIState state)
        {
            _stateMachine.currentState = state;
        }
    }
}
