using UnityEngine;

namespace Hecton8.AI
{
    public enum CreatureRoleType
    {
        Ambient,
        Territorial,
        Hunter,
        Leviathan,
        DroneTrader
    }

    public enum CreatureLocomotionType
    {
        SteeringSolo,
        GpuBoidSchool,
        AstarPatrol,
        CandiceActor
    }

    [CreateAssetMenu(
        fileName = "CreatureArchetype",
        menuName = "Hecton8/AI/Creature Archetype",
        order = 102)]
    public sealed class CreatureArchetypeData : ScriptableObject
    {
        [Header("Identity")]
        public string creatureId = "creature.unknown";
        public string displayName = "Unknown Creature";
        [TextArea(2, 4)] public string gameplayPurpose = "Ambient fauna";

        [Header("Role")]
        public CreatureRoleType roleType = CreatureRoleType.Ambient;
        public CreatureLocomotionType locomotionType = CreatureLocomotionType.SteeringSolo;

        [Header("Combat")]
        public bool isAggressive;
        public bool canFlee = true;
        public float maxHealth = 50f;
        public float attackDamage = 15f;
        public float attackCooldown = 2f;

        [Header("Movement")]
        public float cruiseSpeed = 4f;
        public float burstSpeed = 7f;
        public float turnSpeed = 3f;
        public float sleepDistance = 200f;
        public float cullDistance = 240f;

        [Header("Perception")]
        public float baseAggroDistance = 20f;
        public float baseEscapeDistance = 15f;
        public float baseEscapeSafeDistance = 30f;
        public float baseDeaggroDistance = 35f;
        public bool reactToPlayerNoise = true;
        public float noiseDetectionBonus = 10f;
        public float noiseEscapeBonus = 8f;
        public bool reactToPlayerLight = true;
        public float lightDetectionBonus = 12f;
        public float lightEscapeBonus = 10f;
        public float stimulusMemoryDuration = 2.5f;

        [Header("AI Stack")]
        public bool useCandiceBehaviorTree;
        public bool useAstarPathing;
        public bool useGpuBoids;
        [TextArea(1, 3)] public string behaviorTreeHint = string.Empty;

        [Header("Budget")]
        public int maxAliveGlobal = 8;
        public int maxAlivePerBiome = 4;
        public int spawnWeight = 10;

        [Header("Content")]
        public GameObject prefab;
        public string lootProfileId = string.Empty;
        [TextArea(1, 3)] public string biomeNotes = string.Empty;

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isUpdating ||
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                return;

            if (maxHealth < 1f) maxHealth = 1f;
            if (attackDamage < 0f) attackDamage = 0f;
            if (attackCooldown < 0.1f) attackCooldown = 0.1f;
            if (cruiseSpeed < 0.1f) cruiseSpeed = 0.1f;
            if (burstSpeed < cruiseSpeed) burstSpeed = cruiseSpeed;
            if (turnSpeed < 0.1f) turnSpeed = 0.1f;
            if (sleepDistance < 1f) sleepDistance = 1f;
            if (cullDistance < sleepDistance) cullDistance = sleepDistance;
            if (baseAggroDistance < 0f) baseAggroDistance = 0f;
            if (baseEscapeDistance < 0f) baseEscapeDistance = 0f;
            if (baseEscapeSafeDistance < baseEscapeDistance) baseEscapeSafeDistance = baseEscapeDistance;
            if (baseDeaggroDistance < baseAggroDistance) baseDeaggroDistance = baseAggroDistance;
            if (noiseDetectionBonus < 0f) noiseDetectionBonus = 0f;
            if (noiseEscapeBonus < 0f) noiseEscapeBonus = 0f;
            if (lightDetectionBonus < 0f) lightDetectionBonus = 0f;
            if (lightEscapeBonus < 0f) lightEscapeBonus = 0f;
            if (stimulusMemoryDuration < 0f) stimulusMemoryDuration = 0f;
            if (maxAliveGlobal < 0) maxAliveGlobal = 0;
            if (maxAlivePerBiome < 0) maxAlivePerBiome = 0;
            if (spawnWeight < 0) spawnWeight = 0;
        }
#endif
    }
}
