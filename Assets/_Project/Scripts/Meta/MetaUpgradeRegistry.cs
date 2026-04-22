using System;
using UnityEngine;

namespace Hecton8.Meta
{
    /// <summary>
    /// Static definition store for permanent global upgrades purchased with explorer points.
    /// </summary>
    public static class MetaUpgradeRegistry
    {
        /// <summary>
        /// Stable upgrade definition consumed by the global profile and runtime buff injector.
        /// </summary>
        public readonly struct MetaUpgradeDefinition
        {
            /// <summary>Stable upgrade identifier.</summary>
            public readonly string Id;

            /// <summary>Player-facing title.</summary>
            public readonly string Title;

            /// <summary>Maximum supported level.</summary>
            public readonly int MaxLevel;

            /// <summary>Explorer-point cost for level 1.</summary>
            public readonly int BaseCost;

            /// <summary>Additional explorer-point cost applied per purchased level.</summary>
            public readonly int CostStep;

            /// <summary>Runtime oxygen capacity gain per level.</summary>
            public readonly float OxygenCapacityBonusPerLevel;

            /// <summary>Runtime swim-speed gain per level.</summary>
            public readonly float SwimSpeedBonusPerLevel;

            /// <summary>Whether this upgrade grants the starter-cache payload.</summary>
            public readonly bool GrantsStartingResourceCache;

            /// <summary>Pollution reduction applied per level.</summary>
            public readonly float PollutionReductionPerLevel;

            /// <summary>Recycle-yield bonus applied per level.</summary>
            public readonly float RecycleYieldBonusPerLevel;

            /// <summary>
            /// Creates a new immutable meta-upgrade definition.
            /// </summary>
            public MetaUpgradeDefinition(
                string id,
                string title,
                int maxLevel,
                int baseCost,
                int costStep,
                float oxygenCapacityBonusPerLevel,
                float swimSpeedBonusPerLevel,
                bool grantsStartingResourceCache,
                float pollutionReductionPerLevel = 0f,
                float recycleYieldBonusPerLevel = 0f)
            {
                Id = id ?? string.Empty;
                Title = title ?? string.Empty;
                MaxLevel = Mathf.Max(1, maxLevel);
                BaseCost = Mathf.Max(0, baseCost);
                CostStep = Mathf.Max(0, costStep);
                OxygenCapacityBonusPerLevel = Mathf.Max(0f, oxygenCapacityBonusPerLevel);
                SwimSpeedBonusPerLevel = Mathf.Max(0f, swimSpeedBonusPerLevel);
                GrantsStartingResourceCache = grantsStartingResourceCache;
                PollutionReductionPerLevel = Mathf.Clamp01(pollutionReductionPerLevel);
                RecycleYieldBonusPerLevel = Mathf.Max(0f, recycleYieldBonusPerLevel);
            }

            /// <summary>
            /// Resolves the purchase cost for the requested next level.
            /// </summary>
            /// <param name="currentLevel">Already owned level before the purchase.</param>
            public int GetCostForNextLevel(int currentLevel)
            {
                int clampedLevel = Mathf.Clamp(currentLevel, 0, MaxLevel);
                return BaseCost + CostStep * clampedLevel;
            }
        }

        /// <summary>Permanent oxygen-capacity upgrade identifier.</summary>
        public const string BaseOxygenCapacityId = "meta.oxygen_capacity";

        /// <summary>Permanent starter-cache upgrade identifier.</summary>
        public const string StartingResourceCacheId = "meta.starting_resource_cache";

        /// <summary>Permanent swim-speed upgrade identifier.</summary>
        public const string SwimSpeedBoostId = "meta.swim_speed_boost";

        /// <summary>Permanent pollution-reduction upgrade identifier.</summary>
        public const string GreenTechId = "meta.green_tech";

        /// <summary>Permanent recycle-efficiency upgrade identifier.</summary>
        public const string EfficiencyExpertId = "meta.efficiency_expert";

        // COLD ALLOC: MetaUpgradeDefinition[5] - fixed permanent progression catalog - owner: MetaUpgradeRegistry
        private static readonly MetaUpgradeDefinition[] _definitions =
        {
            new MetaUpgradeDefinition(
                BaseOxygenCapacityId,
                "Base Oxygen Capacity",
                5,
                25,
                15,
                0.10f,
                0f,
                false),
            new MetaUpgradeDefinition(
                StartingResourceCacheId,
                "Starting Resource Cache",
                3,
                20,
                20,
                0f,
                0f,
                true),
            new MetaUpgradeDefinition(
                SwimSpeedBoostId,
                "Swim Speed Boost",
                4,
                30,
                20,
                0f,
                0.05f,
                false),
            new MetaUpgradeDefinition(
                GreenTechId,
                "Green Tech",
                3,
                35,
                25,
                0f,
                0f,
                false,
                0.10f,
                0f),
            new MetaUpgradeDefinition(
                EfficiencyExpertId,
                "Efficiency Expert",
                3,
                35,
                25,
                0f,
                0f,
                false,
                0f,
                0.05f),
        };

        /// <summary>
        /// Total number of supported permanent upgrades.
        /// </summary>
        public static int DefinitionCount => _definitions.Length;

        /// <summary>
        /// Returns the immutable definition at the requested index.
        /// </summary>
        public static MetaUpgradeDefinition GetDefinition(int index)
        {
            return _definitions[index];
        }

        /// <summary>
        /// Tries to resolve a permanent upgrade definition by stable identifier.
        /// </summary>
        public static bool TryGetDefinition(string upgradeId, out MetaUpgradeDefinition definition)
        {
            if (!string.IsNullOrWhiteSpace(upgradeId))
            {
                for (int i = 0; i < _definitions.Length; i++)
                {
                    if (string.Equals(_definitions[i].Id, upgradeId, StringComparison.Ordinal))
                    {
                        definition = _definitions[i];
                        return true;
                    }
                }
            }

            definition = default;
            return false;
        }
    }
}
