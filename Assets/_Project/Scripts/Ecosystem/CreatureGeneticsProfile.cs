using System;
using UnityEngine;

namespace Hecton8.Ecosystem
{
    /// <summary>
    /// Authored predator-species cognition tuning consumed by the fauna utility bridge.
    /// </summary>
    [CreateAssetMenu(fileName = "CreatureGeneticsProfile", menuName = "Hecton8/Ecosystem/Creature Genetics Profile")]
    public sealed class CreatureGeneticsProfile : ScriptableObject
    {
        [Serializable]
        public struct SpeciesGeneticsTuning
        {
            [Tooltip("Stable species identifier matching FaunaSpeciesProfile.speciesID.")]
            public int speciesId;

            [Min(0f)]
            [Tooltip("Multiplier applied to chemical attractant scoring and scent-trail confidence.")]
            public float scentSensitivity;

            [Min(0f)]
            [Tooltip("Pack-coordination radius in meters used before predators commit to a strike.")]
            public float packHuntingRadius;

            [Min(0f)]
            [Tooltip("Flank offset in meters used when a same-species partner is already pressing the target.")]
            public float packFlankOffset;

            [Min(0f)]
            [Tooltip("Multiplier applied to the species base aggression when cognition weights are assembled.")]
            public float baseAggressionMultiplier;
        }

        [Header("── Species Tunings ─────────────")]
        [SerializeField, Tooltip("Authored species tuning rows. Seed at least four predator species for the current hunt roster.")]
        private SpeciesGeneticsTuning[] speciesTunings = Array.Empty<SpeciesGeneticsTuning>();

        /// <summary>
        /// Resolves the authored cognition tuning row for the supplied species identifier.
        /// </summary>
        public bool TryResolveSpeciesTuning(int speciesId, out SpeciesGeneticsTuning tuning)
        {
            if (speciesId != 0 && speciesTunings != null)
            {
                for (int i = 0; i < speciesTunings.Length; i++)
                {
                    if (speciesTunings[i].speciesId != speciesId)
                        continue;

                    tuning = Sanitize(speciesTunings[i]);
                    return true;
                }
            }

            tuning = default;
            return false;
        }

        private void OnValidate()
        {
            if (speciesTunings == null)
            {
                speciesTunings = Array.Empty<SpeciesGeneticsTuning>();
                return;
            }

            for (int i = 0; i < speciesTunings.Length; i++)
                speciesTunings[i] = Sanitize(speciesTunings[i]);
        }

        private static SpeciesGeneticsTuning Sanitize(SpeciesGeneticsTuning tuning)
        {
            tuning.scentSensitivity = Mathf.Max(0f, tuning.scentSensitivity);
            tuning.packHuntingRadius = Mathf.Max(0f, tuning.packHuntingRadius);
            tuning.packFlankOffset = Mathf.Max(0f, tuning.packFlankOffset);
            tuning.baseAggressionMultiplier = Mathf.Max(0f, tuning.baseAggressionMultiplier);
            return tuning;
        }
    }
}
