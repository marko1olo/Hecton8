using System;
using UnityEngine;

namespace Hecton8.World
{
    /// <summary>
    /// Authored biome-level predator/prey balance and scent diffusion settings.
    /// </summary>
    [CreateAssetMenu(fileName = "EcosystemBalanceProfile", menuName = "Hecton8/World/Ecosystem Balance Profile")]
    public sealed class EcosystemBalanceProfile : ScriptableObject
    {
        private static readonly BiomeChemicalBalance DefaultBalanceTemplate = new BiomeChemicalBalance
        {
            biomeId = "default",
            predatorToPreyRatio = 0.18f,
            bloodDecayRate = 0.08f,
            exhaustDecayRate = 0.05f,
            fearDecayRate = 0.12f,
            bloodDiffusionRate = 0.32f,
            exhaustDiffusionRate = 0.24f,
            fearDiffusionRate = 0.40f
        };
        [Serializable]
        public struct BiomeChemicalBalance
        {
            [Tooltip("Stable biome identifier used by runtime selectors. Leave empty for the default row.")]
            public string biomeId;
            [Min(0f)]
            [Tooltip("Target predator-to-prey pressure ratio for this biome.")]
            public float predatorToPreyRatio;
            [Range(0f, 1f)]
            [Tooltip("Per-diffusion-pass decay applied to blood scent.")]
            public float bloodDecayRate;
            [Range(0f, 1f)]
            [Tooltip("Per-diffusion-pass decay applied to exhaust scent.")]
            public float exhaustDecayRate;
            [Range(0f, 1f)]
            [Tooltip("Per-diffusion-pass decay applied to fear pheromone.")]
            public float fearDecayRate;
            [Range(0f, 1f)]
            [Tooltip("Six-neighbor diffusion weight applied to blood scent.")]
            public float bloodDiffusionRate;
            [Range(0f, 1f)]
            [Tooltip("Six-neighbor diffusion weight applied to exhaust scent.")]
            public float exhaustDiffusionRate;
            [Range(0f, 1f)]
            [Tooltip("Six-neighbor diffusion weight applied to fear pheromone.")]
            public float fearDiffusionRate;
        }

        [Header("── Default Balance ──────────────────")]
        [SerializeField, Tooltip("Fallback scent balance used when no biome-specific row matches the runtime biome.")]
        private BiomeChemicalBalance defaultBiomeBalance = DefaultBalanceTemplate;

        [Header("── Per-Biome Overrides ──────────────────")]
        [SerializeField, Tooltip("Optional biome-specific overrides for predator/prey pressure and scent tuning.")]
        private BiomeChemicalBalance[] biomeBalances = Array.Empty<BiomeChemicalBalance>();

        /// <summary>
        /// Fallback balance used when no biome-specific row resolves.
        /// </summary>
        public BiomeChemicalBalance DefaultBiomeBalance => defaultBiomeBalance;

        /// <summary>
        /// Deterministic fallback balance used when no asset is assigned.
        /// </summary>
        public static BiomeChemicalBalance DefaultBalance => DefaultBalanceTemplate;

        /// <summary>
        /// Resolves the authored chemical-balance row for a biome identifier.
        /// </summary>
        public bool TryResolveBiomeBalance(string biomeId, out BiomeChemicalBalance balance)
        {
            if (!string.IsNullOrWhiteSpace(biomeId) && biomeBalances != null)
            {
                for (int i = 0; i < biomeBalances.Length; i++)
                {
                    if (!string.Equals(biomeBalances[i].biomeId, biomeId, StringComparison.OrdinalIgnoreCase))
                        continue;

                    balance = biomeBalances[i];
                    return true;
                }
            }

            balance = defaultBiomeBalance;
            return false;
        }

        private void OnValidate()
        {
            defaultBiomeBalance = Sanitize(defaultBiomeBalance);

            if (biomeBalances == null)
            {
                biomeBalances = Array.Empty<BiomeChemicalBalance>();
                return;
            }

            for (int i = 0; i < biomeBalances.Length; i++)
                biomeBalances[i] = Sanitize(biomeBalances[i]);
        }

        private static BiomeChemicalBalance Sanitize(BiomeChemicalBalance balance)
        {
            if (string.IsNullOrWhiteSpace(balance.biomeId))
                balance.biomeId = "default";

            balance.predatorToPreyRatio = Mathf.Max(0f, balance.predatorToPreyRatio);
            balance.bloodDecayRate = Mathf.Clamp01(balance.bloodDecayRate);
            balance.exhaustDecayRate = Mathf.Clamp01(balance.exhaustDecayRate);
            balance.fearDecayRate = Mathf.Clamp01(balance.fearDecayRate);
            balance.bloodDiffusionRate = Mathf.Clamp01(balance.bloodDiffusionRate);
            balance.exhaustDiffusionRate = Mathf.Clamp01(balance.exhaustDiffusionRate);
            balance.fearDiffusionRate = Mathf.Clamp01(balance.fearDiffusionRate);
            return balance;
        }
    }
}
