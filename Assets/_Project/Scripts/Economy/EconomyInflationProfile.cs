using System;
using Hecton8.Items;
using Hecton.Localization;
using UnityEngine;

namespace Hecton8.Economy
{
    /// <summary>
    /// Sector-local extraction deflation profile used by scarcity-aware economy systems.
    /// </summary>
    [CreateAssetMenu(fileName = "EconomyInflationProfile", menuName = "Hecton8/Economy/Inflation Profile", order = 41)]
    public sealed class EconomyInflationProfile : ScriptableObject
    {
        [Serializable]
        public struct ResourceInflationRule
        {
            [Tooltip("Resource item that receives sector-local deflation when over-mined.")]
            public ItemData item;

            [Tooltip("Collected units required to advance one deflation step inside a sector.")]
            [Min(1)] public int unitsPerStep;

            [Tooltip("Spawn-rate scalar removed per sector step.")]
            [Range(0f, 1f)] public float spawnRateDropPerStep;

            [Tooltip("Value scalar removed per sector step.")]
            [Range(0f, 1f)] public float valueDropPerStep;

            [Tooltip("Lower bound for spawn-rate scalar after repeated over-mining.")]
            [Range(0.05f, 1f)] public float minSpawnRateScalar;

            [Tooltip("Lower bound for value scalar after repeated over-mining.")]
            [Range(0.05f, 1f)] public float minValueScalar;
        }

        [Header("── Inflation Rules ──────────────────")]
        [Tooltip("Per-resource over-mining response curves.")]
        [SerializeField] private ResourceInflationRule[] resourceRules = Array.Empty<ResourceInflationRule>();

        /// <summary>
        /// Returns the spawn-rate scalar for one sector-local extraction total.
        /// </summary>
        public float EvaluateSpawnRateScalar(int itemHashId, int sectorExtractedUnits)
        {
            return TryResolveRule(itemHashId, out ResourceInflationRule rule)
                ? EvaluateScalar(sectorExtractedUnits, rule.unitsPerStep, rule.spawnRateDropPerStep, rule.minSpawnRateScalar)
                : 1f;
        }

        /// <summary>
        /// Returns the value scalar for one sector-local extraction total.
        /// </summary>
        public float EvaluateValueScalar(int itemHashId, int sectorExtractedUnits)
        {
            return TryResolveRule(itemHashId, out ResourceInflationRule rule)
                ? EvaluateScalar(sectorExtractedUnits, rule.unitsPerStep, rule.valueDropPerStep, rule.minValueScalar)
                : 1f;
        }

        /// <summary>
        /// Returns the crafting surcharge ratio driven by sector-local over-mining.
        /// </summary>
        public float EvaluateCraftInflationScalar(int itemHashId, int sectorExtractedUnits)
        {
            float valueScalar = EvaluateValueScalar(itemHashId, sectorExtractedUnits);
            return Mathf.Clamp01(1f - valueScalar);
        }

        private bool TryResolveRule(int itemHashId, out ResourceInflationRule rule)
        {
            rule = default;
            if (itemHashId == 0 || resourceRules == null)
                return false;

            for (int i = 0; i < resourceRules.Length; i++)
            {
                ResourceInflationRule candidate = resourceRules[i];
                if (candidate.item == null)
                    continue;

                if (LocHash.Compute(candidate.item.PersistentId) != itemHashId)
                    continue;

                rule = candidate;
                return true;
            }

            return false;
        }

        private static float EvaluateScalar(int extractedUnits, int unitsPerStep, float dropPerStep, float minScalar)
        {
            if (extractedUnits <= 0 || unitsPerStep <= 0 || dropPerStep <= 0f)
                return 1f;

            int steps = extractedUnits / unitsPerStep;
            if (steps <= 0)
                return 1f;

            return Mathf.Clamp(1f - steps * dropPerStep, minScalar, 1f);
        }
    }
}
