using Hecton8.Core;
using Hecton8.Ecosystem;
using Hecton8.VFX;
using UnityEngine;

namespace Hecton8.AI
{
    /// <summary>
    /// Species-level behavior and reaction tuning for fauna AI.
    /// </summary>
    [CreateAssetMenu(fileName = "FaunaSpeciesProfile_", menuName = "Hecton8/Fauna/Species Profile")]
    public sealed class FaunaSpeciesProfile : ScriptableObject
    {
        [Header("Personality")]
        [Range(0f, 1f), Tooltip("Default aggression level (0 = passive, 1 = hyper-aggressive).")]
        public float baseAggro = 0.5f;

        [Tooltip("Number of stalk loops before the creature commits to an attack.")]
        public int stalkingPatience = 3;

        [Range(0f, 1f), Tooltip("Health threshold at which the creature starts fleeing.")]
        public float fearThreshold = 0.2f;

        [Header("Tactical Stats")]
        [Tooltip("Attack range in meters.")]
        public float attackRadius = 3f;

        [Tooltip("Damage dealt on attack.")]
        public float attackDamage = 15f;

        [Tooltip("Cooldown between attacks.")]
        public float attackCooldown = 1f;

        [Tooltip("How long the creature stays in retreat state.")]
        public float retreatDuration = 6f;

        [Header("Movement")]
        [Tooltip("Aggressive state speed multiplier.")]
        public float aggressiveSpeedMultiplier = 1.3f;

        [Tooltip("Retreat state speed multiplier.")]
        public float retreatSpeedMultiplier = 1.5f;

        [Tooltip("Escape state speed multiplier.")]
        public float escapeSpeedMultiplier = 2f;

        [Tooltip("Banking intensity while turning at speed.")]
        public float turnBankingIntensity = 1f;

        [Tooltip("Reduces linear speed when turning sharply. 1.0 = default water resistance.")]
        public float centripetalLimit = 1f;

        [Header("Impact")]
        [Tooltip("Camera shake profile triggered on attack hit.")]
        public ShakeProfile attackShakeProfile;

        [Tooltip("Physical impulse applied to the player on hit.")]
        public float impactForceToPlayer = 500f;

        [Header("Mega-Fauna")]
        [Tooltip("If true, this creature can damage vehicles and structures.")]
        public bool isLeviathan = false;

        [Tooltip("Optional authored territory profile used by apex rivalry and intimidation logic.")]
        public ApexTerritoryProfile apexTerritoryProfile;

        [Header("Ecology")]
        [Tooltip("Unique species identifier used for AI recognition.")]
        public int speciesID = 0;

        [Tooltip("Optional species genetics asset that injects scent sensitivity and pack-hunt tuning into the Burst cognition path.")]
        public CreatureGeneticsProfile geneticsProfile;

        [Tooltip("If true, this creature can be hunted by predators.")]
        public bool isPrey = false;

        [Tooltip("Distance at which another predator species is treated as a territory threat.")]
        public float territoryThreatRadius = 15f;

        [Tooltip("How long the creature stays non-aggressive after eating.")]
        public float satedDuration = 45f;

        [Header("Physics Masks")]
        [Tooltip("Mask used to identify prey targets.")]
        public LayerMask preyMask;

        [Tooltip("Mask used to identify predator threats.")]
        public LayerMask predatorMask;

        [Header("Behavioral Quirks")]
        [Tooltip("If true, creature targets active tools and dropped items.")]
        public bool isScavenger;

        [Tooltip("If true, creature remains still until the player gets close.")]
        public bool isAmbusher;

        [Tooltip("Distance at which an ambusher bursts into aggression.")]
        public float ambushTriggerRange = 5f;

        [Header("Visuals")]
        [Tooltip("How much the head follows tracked targets.")]
        [Range(0f, 1f)] public float eyeTrackWeight = 0.5f;

        [Tooltip("Maximum distance for eye tracking.")]
        public float eyeTrackRange = 25f;

        [Tooltip("Detection multiplier while the player's flashlight is on.")]
        public float sensoryWeightFlashlight = 2f;

        [Tooltip("Detection multiplier while the player is using a scooter.")]
        public float sensoryWeightScooter = 1.5f;

#if UNITY_EDITOR
        private void OnValidate()
        {
            SanitizeAuthoringLayerMask(ref preyMask, nameof(preyMask));
            SanitizeAuthoringLayerMask(ref predatorMask, nameof(predatorMask));
        }

        private void SanitizeAuthoringLayerMask(ref LayerMask mask, string fieldName)
        {
            int originalMask = mask.value;
            if (!HectonLayerMasks.IsEverythingLayerMask(originalMask))
                return;

            mask = HectonLayerMasks.AllDefinedProjectLayersMask;
            Hecton8.Core.H8Debug.LogWarning(
                "[FaunaSpeciesProfile] " + fieldName + " was Everything (-1). Replaced with HectonLayerMasks.AllDefinedProjectLayersMask.",
                this);
        }
#endif
    }
}
