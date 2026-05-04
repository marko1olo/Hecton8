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

    public enum LeviathanEncounterType
    {
        PresenceCircle,
        AmbushBurst,
        SentinelPressure
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

        [Header("Home Territory")]
        public bool useHomeTerritory;
        public float homeWanderRadius = 30f;
        public float homeReturnDistance = 45f;
        public float territoryProtectRadius = 22f;
        public float warningDuration = 3.5f;
        public float warningStandOffDistance = 8f;
        public float stalkDuration = 4.5f;
        public float stalkDistance = 10f;

        [Header("Nest And Group")]
        public bool defendNest;
        public float nestProtectRadius = 12f;
        public bool callNearbyAllies;
        public float allyAlertRadius = 18f;
        public float allyAlertCooldown = 2.5f;
        public int allyAlertMaxCount = 3;
        public bool alliesRequireSameArchetype = true;
        public bool usePackHunt;
        public float packSupportRadius = 20f;
        public float packFlankDistance = 6f;
        public float packCommitDistance = 7f;

        [Header("Ecology Traits")]
        public bool thermophilic;
        public bool parasiteAttachCapable;
        public bool laysEggClutches;
        public float eggIncubationSeconds = 1800f;
        public float parasiteDrainPerSlowTick = 0.02f;

        [Header("Leviathan Presence")]
        public bool useLeviathanPresence;
        public LeviathanEncounterType leviathanEncounterType = LeviathanEncounterType.PresenceCircle;
        public float loomingDuration = 6f;
        public float loomingDistance = 18f;
        public float loomingCommitDistance = 12f;
        public bool useFeintRush;
        public float feintDuration = 2.1f;
        public float feintTriggerDistance = 14f;
        public float feintBreakDistance = 6f;
        public float feintCooldown = 5f;

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
        public FaunaDataTemplate faunaDataTemplate;
        public string lootProfileId = string.Empty;
        [TextArea(1, 3)] public string biomeNotes = string.Empty;

        [Header("World Guidance")]
        public string[] recommendedFaunaFamilyIds;
        public string[] recommendedBiomeFamilyIds;

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
            if (homeWanderRadius < 1f) homeWanderRadius = 1f;
            if (homeReturnDistance < homeWanderRadius) homeReturnDistance = homeWanderRadius;
            if (territoryProtectRadius < 0f) territoryProtectRadius = 0f;
            if (warningDuration < 0f) warningDuration = 0f;
            if (warningStandOffDistance < 1f) warningStandOffDistance = 1f;
            if (stalkDuration < 0f) stalkDuration = 0f;
            if (stalkDistance < 1f) stalkDistance = 1f;
            if (nestProtectRadius < 0f) nestProtectRadius = 0f;
            if (allyAlertRadius < 0f) allyAlertRadius = 0f;
            if (allyAlertCooldown < 0f) allyAlertCooldown = 0f;
            if (allyAlertMaxCount < 0) allyAlertMaxCount = 0;
            if (packSupportRadius < 0f) packSupportRadius = 0f;
            if (packFlankDistance < 0f) packFlankDistance = 0f;
            if (packCommitDistance < 0f) packCommitDistance = 0f;
            if (eggIncubationSeconds < 1f) eggIncubationSeconds = 1f;
            if (parasiteDrainPerSlowTick < 0f) parasiteDrainPerSlowTick = 0f;
            if (loomingDuration < 0f) loomingDuration = 0f;
            if (loomingDistance < 0f) loomingDistance = 0f;
            if (loomingCommitDistance < 0f) loomingCommitDistance = 0f;
            if (feintDuration < 0f) feintDuration = 0f;
            if (feintTriggerDistance < 0f) feintTriggerDistance = 0f;
            if (feintBreakDistance < 0f) feintBreakDistance = 0f;
            if (feintCooldown < 0f) feintCooldown = 0f;
            if (maxAliveGlobal < 0) maxAliveGlobal = 0;
            if (maxAlivePerBiome < 0) maxAlivePerBiome = 0;
            if (spawnWeight < 0) spawnWeight = 0;

            TrimGuidanceArray(ref recommendedFaunaFamilyIds);
            TrimGuidanceArray(ref recommendedBiomeFamilyIds);
        }

        private static void TrimGuidanceArray(ref string[] values)
        {
            if (values == null || values.Length == 0)
                return;

            int writeIndex = 0;
            for (int i = 0; i < values.Length; i++)
            {
                string value = values[i];
                if (string.IsNullOrWhiteSpace(value))
                    continue;

                values[writeIndex++] = value.Trim();
            }

            if (writeIndex == values.Length)
                return;

            if (writeIndex == 0)
            {
                values = System.Array.Empty<string>();
                return;
            }

            string[] trimmed = new string[writeIndex];
            for (int i = 0; i < writeIndex; i++)
                trimmed[i] = values[i];
            values = trimmed;
        }
#endif
    }
}
