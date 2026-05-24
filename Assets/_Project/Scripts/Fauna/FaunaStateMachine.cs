using UnityEngine;

namespace Hecton8.AI
{
    [System.Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FaunaProfile
    {
        [Range(0f, 1f)] public float aggressionLevel;
        [Range(-1f, 1f)] public float curiosity;
        [Range(0f, 1f)] public float braveThreshold;
    }

    /// <summary>
    /// Serialized fauna cognition configuration and runtime output cache.
    /// Legacy managed state execution has been removed; runtime decisions are owned by PredatorCognitionDomain.
    /// </summary>
    [System.Serializable]
    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct FaunaStateMachine
    {
        [Header("── State Configuration ────────────────────────────")]
        public FaunaProfile profile;
        public float wanderRadius;
        public float waypointReachDistance;
        public float escapeDistance;
        public float escapeSafeDistance;

        [Header("── Tactical Behaviors ─────────────────────────────")]
        public float stalkDuration;
        public float stalkRadius;
        public float attackRadius;
        public float attackDamage;
        public float retreatDuration;

        [Header("── Territory ──────────────────────────────────────")]
        public bool useTerritory;
        public float patrolRadius;

        [Header("── Flocking ───────────────────────────────────────")]
        public bool isFlockingFish;
        public float cohesionWeight;
        public float alignmentWeight;
        public float separationWeight;

        [HideInInspector] public FaunaBrain.AIState currentState;
        [HideInInspector] public float currentForceMultiplier;
        [HideInInspector] public float currentSpeedMultiplier;
        [HideInInspector] public float currentTurnMultiplier;

        public static FaunaStateMachine CreateDefault()
        {
            FaunaStateMachine stateMachine = default;
            stateMachine.profile = new FaunaProfile
            {
                aggressionLevel = 0.5f,
                curiosity = 0f,
                braveThreshold = 0.2f
            };
            stateMachine.wanderRadius = 15f;
            stateMachine.waypointReachDistance = 2f;
            stateMachine.escapeDistance = 30f;
            stateMachine.escapeSafeDistance = 50f;
            stateMachine.stalkDuration = 10f;
            stateMachine.stalkRadius = 18f;
            stateMachine.attackRadius = 3f;
            stateMachine.attackDamage = 15f;
            stateMachine.retreatDuration = 6f;
            stateMachine.useTerritory = false;
            stateMachine.patrolRadius = 50f;
            stateMachine.isFlockingFish = false;
            stateMachine.cohesionWeight = 1f;
            stateMachine.alignmentWeight = 1f;
            stateMachine.separationWeight = 1.5f;
            stateMachine.currentState = FaunaBrain.AIState.Idle;
            stateMachine.currentForceMultiplier = 1f;
            stateMachine.currentSpeedMultiplier = 1f;
            stateMachine.currentTurnMultiplier = 1f;
            return stateMachine;
        }

        public void ResetRuntime(FaunaBrain.AIState initialState)
        {
            currentState = initialState;
            currentForceMultiplier = 1f;
            currentSpeedMultiplier = 1f;
            currentTurnMultiplier = 1f;
        }
    }
}
