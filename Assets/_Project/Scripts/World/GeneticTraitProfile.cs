using System;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Shared flora trait-bit authoring used by cultivation, UI, and inventory persistence.
    /// </summary>
    [CreateAssetMenu(fileName = "GeneticTraitProfile_", menuName = "Hecton8/World/Genetic Trait Profile", order = 113)]
    public sealed class GeneticTraitProfile : ScriptableObject
    {
        [Flags]
        public enum GeneticTraitMask : uint
        {
            None = 0u,
            Bioluminescent = 1u << 0,
            OxygenProducing = 1u << 1,
            Toxic = 1u << 2,
            FastGrowing = 1u << 3,
            Medicinal = 1u << 4,
            Conductive = 1u << 5,
            Cryogenic = 1u << 6,
            Explosive = 1u << 7
        }

        [Serializable]
        public struct TraitDefinition
        {
            [Tooltip("Bit index in the cultivation genetics mask.")]
            [Range(0, 31)]
            public int bitIndex;

            [Tooltip("Editor-facing label used by diagnostics and UI.")]
            public string label;

            [Tooltip("Signed oxygen contribution applied per slow tick while the trait is active on a mature plant.")]
            public float oxygenUnitsPerSlowTick;

            [Tooltip("Additional scrubber power draw in watts required when the trait is active on a mature plant.")]
            public float scrubberPowerWatts;

            [Tooltip("Growth-rate multiplier contributed by this trait.")]
            public float growthRateMultiplier;

            [Tooltip("Normalized hazard intensity contributed while the trait is active without enough scrubber power.")]
            [Range(0f, 1f)]
            public float hazardIntensity;

            [Tooltip("World-space hazard radius in meters contributed while the trait is active without enough scrubber power.")]
            [Min(0f)]
            public float hazardRadiusMeters;

            [Tooltip("Signed utility score used by downstream buff/debuff systems.")]
            public float utilityScore;
        }

        [Header("Trait Rows")]
        [SerializeField, Tooltip("Authored gameplay rows keyed by trait bit. Eight rows cover the current cultivation roster.")]
        private TraitDefinition[] traitDefinitions = Array.Empty<TraitDefinition>();

        /// <summary>Returns the authored row for a specific bit index.</summary>
        public bool TryResolveTrait(int bitIndex, out TraitDefinition definition)
        {
            if (traitDefinitions != null)
            {
                for (int i = 0; i < traitDefinitions.Length; i++)
                {
                    if (traitDefinitions[i].bitIndex != bitIndex)
                        continue;

                    definition = Sanitize(traitDefinitions[i]);
                    return true;
                }
            }

            definition = BuildDefaultTraitDefinition(bitIndex);
            return definition.bitIndex == bitIndex;
        }

        /// <summary>Resolves cumulative oxygen injection for the supplied active trait mask.</summary>
        public float ResolveOxygenUnitsPerSlowTick(uint traitMask)
        {
            float total = 0f;
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                uint bit = 1u << bitIndex;
                if ((traitMask & bit) == 0u || !TryResolveTrait(bitIndex, out TraitDefinition definition))
                    continue;

                total += definition.oxygenUnitsPerSlowTick;
            }

            return total;
        }

        /// <summary>Resolves cumulative scrubber power draw for the supplied active trait mask.</summary>
        public float ResolveScrubberPowerWatts(uint traitMask)
        {
            float total = 0f;
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                uint bit = 1u << bitIndex;
                if ((traitMask & bit) == 0u || !TryResolveTrait(bitIndex, out TraitDefinition definition))
                    continue;

                total += definition.scrubberPowerWatts;
            }

            return total;
        }

        /// <summary>Resolves a multiplicative growth-rate modifier for the supplied trait mask.</summary>
        public float ResolveGrowthRateMultiplier(uint traitMask)
        {
            float multiplier = 1f;
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                uint bit = 1u << bitIndex;
                if ((traitMask & bit) == 0u || !TryResolveTrait(bitIndex, out TraitDefinition definition))
                    continue;

                multiplier *= definition.growthRateMultiplier > 0f ? definition.growthRateMultiplier : 1f;
            }

            return Mathf.Max(0.1f, multiplier);
        }

        /// <summary>Resolves the peak toxicity hazard profile emitted by the supplied trait mask.</summary>
        public void ResolveHazardProfile(uint traitMask, out float intensity, out float radiusMeters)
        {
            intensity = 0f;
            radiusMeters = 0f;

            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
            {
                uint bit = 1u << bitIndex;
                if ((traitMask & bit) == 0u || !TryResolveTrait(bitIndex, out TraitDefinition definition))
                    continue;

                intensity = Mathf.Max(intensity, definition.hazardIntensity);
                radiusMeters = Mathf.Max(radiusMeters, definition.hazardRadiusMeters);
            }
        }

        private void OnValidate()
        {
            if (traitDefinitions == null)
            {
                traitDefinitions = Array.Empty<TraitDefinition>();
                return;
            }

            for (int i = 0; i < traitDefinitions.Length; i++)
                traitDefinitions[i] = Sanitize(traitDefinitions[i]);
        }

        private static TraitDefinition Sanitize(TraitDefinition definition)
        {
            definition.bitIndex = Mathf.Clamp(definition.bitIndex, 0, 31);
            definition.oxygenUnitsPerSlowTick = Mathf.Clamp(definition.oxygenUnitsPerSlowTick, -32f, 32f);
            definition.scrubberPowerWatts = Mathf.Max(0f, definition.scrubberPowerWatts);
            definition.growthRateMultiplier = Mathf.Max(0.1f, definition.growthRateMultiplier);
            definition.hazardIntensity = Mathf.Clamp01(definition.hazardIntensity);
            definition.hazardRadiusMeters = Mathf.Max(0f, definition.hazardRadiusMeters);
            return definition;
        }

        private static TraitDefinition BuildDefaultTraitDefinition(int bitIndex)
        {
            return bitIndex switch
            {
                0 => new TraitDefinition { bitIndex = 0, label = "Bioluminescent", oxygenUnitsPerSlowTick = 0f, scrubberPowerWatts = 0f, growthRateMultiplier = 1f, hazardIntensity = 0f, hazardRadiusMeters = 0f, utilityScore = 0.35f },
                1 => new TraitDefinition { bitIndex = 1, label = "Oxygen-Producing", oxygenUnitsPerSlowTick = 0.45f, scrubberPowerWatts = 0f, growthRateMultiplier = 1f, hazardIntensity = 0f, hazardRadiusMeters = 0f, utilityScore = 0.9f },
                2 => new TraitDefinition { bitIndex = 2, label = "Toxic", oxygenUnitsPerSlowTick = 0f, scrubberPowerWatts = 8f, growthRateMultiplier = 1f, hazardIntensity = 0.72f, hazardRadiusMeters = 2.6f, utilityScore = -0.8f },
                3 => new TraitDefinition { bitIndex = 3, label = "Fast-Growing", oxygenUnitsPerSlowTick = 0f, scrubberPowerWatts = 0f, growthRateMultiplier = 1.8f, hazardIntensity = 0f, hazardRadiusMeters = 0f, utilityScore = 0.4f },
                4 => new TraitDefinition { bitIndex = 4, label = "Medicinal", oxygenUnitsPerSlowTick = 0.05f, scrubberPowerWatts = 0f, growthRateMultiplier = 0.95f, hazardIntensity = 0f, hazardRadiusMeters = 0f, utilityScore = 0.7f },
                5 => new TraitDefinition { bitIndex = 5, label = "Conductive", oxygenUnitsPerSlowTick = 0f, scrubberPowerWatts = 1.5f, growthRateMultiplier = 1.05f, hazardIntensity = 0.08f, hazardRadiusMeters = 1.2f, utilityScore = -0.15f },
                6 => new TraitDefinition { bitIndex = 6, label = "Cryogenic", oxygenUnitsPerSlowTick = 0f, scrubberPowerWatts = 2f, growthRateMultiplier = 0.8f, hazardIntensity = 0.18f, hazardRadiusMeters = 1.6f, utilityScore = 0.2f },
                7 => new TraitDefinition { bitIndex = 7, label = "Explosive", oxygenUnitsPerSlowTick = 0f, scrubberPowerWatts = 3f, growthRateMultiplier = 0.9f, hazardIntensity = 0.44f, hazardRadiusMeters = 2f, utilityScore = -0.45f },
                _ => default
            };
        }
    }
}
