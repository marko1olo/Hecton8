using System;

namespace Hecton8.Meta
{
    /// <summary>
    /// Global progression payload stored outside slot saves.
    /// This file survives new-game starts and exposes hall-of-fame style meta progress.
    /// </summary>
    [Serializable]
    public sealed class GlobalProfileData
    {
        /// <summary>Current file schema version.</summary>
        public const int CurrentVersion = 2;

        /// <summary>Maximum number of globally tracked unlocked achievements.</summary>
        public const int MaxUnlockedAchievements = 64;

        /// <summary>Maximum number of fastest-achievement records stored in the profile.</summary>
        public const int MaxFastestAchievementRecords = 64;

        /// <summary>Maximum number of purchased permanent upgrade records.</summary>
        public const int MaxPurchasedUpgrades = 16;

        /// <summary>Maximum number of marathon-goal progress records.</summary>
        public const int MaxMarathonGoals = 16;

        /// <summary>Serialized schema version for migration and repair.</summary>
        public int version = CurrentVersion;

        /// <summary>Persistent meta currency earned from first-time achievement unlocks.</summary>
        public int explorerPoints;

        /// <summary>Deepest reached depth across every completed and active run.</summary>
        public float maxDepthMeters;

        /// <summary>Longest recorded life duration across all runs.</summary>
        public float longestLifeWithoutDeathSeconds;

        /// <summary>Highest biome discovery count reached within a single run.</summary>
        public int highestBiomeDiscoveriesInSingleRun;

        /// <summary>Total number of biome discovery events recorded globally.</summary>
        public int totalBiomesDiscoveredAllTime;

        /// <summary>Number of globally unlocked achievements stored in <see cref="unlockedAchievementIds"/>.</summary>
        public int unlockedAchievementCount;

        /// <summary>Stable global achievement identifiers unlocked at least once.</summary>
        public string[] unlockedAchievementIds = new string[MaxUnlockedAchievements];

        /// <summary>Number of fastest-achievement records stored in <see cref="fastestAchievementRecords"/>.</summary>
        public int fastestAchievementRecordCount;

        /// <summary>Best known achievement acquisition times keyed by stable achievement identifier.</summary>
        public FastestAchievementRecord[] fastestAchievementRecords = new FastestAchievementRecord[MaxFastestAchievementRecords];

        /// <summary>Number of stored permanent-upgrade level records.</summary>
        public int purchasedUpgradeCount;

        /// <summary>Owned permanent-upgrade levels keyed by stable upgrade identifier.</summary>
        public MetaUpgradeLevelRecord[] purchasedUpgradeLevels = new MetaUpgradeLevelRecord[MaxPurchasedUpgrades];

        /// <summary>Number of stored marathon-goal progress records.</summary>
        public int marathonGoalCount;

        /// <summary>All-time marathon-goal progress entries keyed by stable goal identifier.</summary>
        public MarathonGoalProgressRecord[] marathonGoals = new MarathonGoalProgressRecord[MaxMarathonGoals];

        /// <summary>
        /// Repairs array capacities after load or migration.
        /// </summary>
        public void EnsureCapacity()
        {
            if (unlockedAchievementIds == null || unlockedAchievementIds.Length != MaxUnlockedAchievements)
            {
                string[] replacement = new string[MaxUnlockedAchievements];
                if (unlockedAchievementIds != null)
                {
                    int copyCount = unlockedAchievementIds.Length < replacement.Length ? unlockedAchievementIds.Length : replacement.Length;
                    Array.Copy(unlockedAchievementIds, replacement, copyCount);
                }

                unlockedAchievementIds = replacement;
            }

            if (fastestAchievementRecords == null || fastestAchievementRecords.Length != MaxFastestAchievementRecords)
            {
                FastestAchievementRecord[] replacement = new FastestAchievementRecord[MaxFastestAchievementRecords];
                if (fastestAchievementRecords != null)
                {
                    int copyCount = fastestAchievementRecords.Length < replacement.Length ? fastestAchievementRecords.Length : replacement.Length;
                    Array.Copy(fastestAchievementRecords, replacement, copyCount);
                }

                fastestAchievementRecords = replacement;
            }

            if (purchasedUpgradeLevels == null || purchasedUpgradeLevels.Length != MaxPurchasedUpgrades)
            {
                MetaUpgradeLevelRecord[] replacement = new MetaUpgradeLevelRecord[MaxPurchasedUpgrades];
                if (purchasedUpgradeLevels != null)
                {
                    int copyCount = purchasedUpgradeLevels.Length < replacement.Length ? purchasedUpgradeLevels.Length : replacement.Length;
                    Array.Copy(purchasedUpgradeLevels, replacement, copyCount);
                }

                purchasedUpgradeLevels = replacement;
            }

            if (marathonGoals == null || marathonGoals.Length != MaxMarathonGoals)
            {
                MarathonGoalProgressRecord[] replacement = new MarathonGoalProgressRecord[MaxMarathonGoals];
                if (marathonGoals != null)
                {
                    int copyCount = marathonGoals.Length < replacement.Length ? marathonGoals.Length : replacement.Length;
                    Array.Copy(marathonGoals, replacement, copyCount);
                }

                marathonGoals = replacement;
            }
        }
    }

    /// <summary>
    /// Best known time-to-unlock entry for a specific internal achievement.
    /// </summary>
    [Serializable]
    public struct FastestAchievementRecord
    {
        /// <summary>Stable achievement identifier.</summary>
        public string achievementId;

        /// <summary>Player-facing title at the moment the record was captured.</summary>
        public string title;

        /// <summary>Fastest known acquisition time in seconds.</summary>
        public float bestTimeSeconds;
    }

    /// <summary>
    /// Stored ownership level for one permanent global upgrade.
    /// </summary>
    [Serializable]
    public struct MetaUpgradeLevelRecord
    {
        /// <summary>Stable permanent-upgrade identifier.</summary>
        public string upgradeId;

        /// <summary>Purchased level owned by the profile.</summary>
        public int level;
    }

    /// <summary>
    /// Stored all-time progress state for one marathon goal.
    /// </summary>
    [Serializable]
    public struct MarathonGoalProgressRecord
    {
        /// <summary>Stable marathon-goal identifier.</summary>
        public string goalId;

        /// <summary>Player-facing title used in meta menus.</summary>
        public string title;

        /// <summary>Current cumulative progress across all runs.</summary>
        public int progress;

        /// <summary>Goal completion target.</summary>
        public int target;

        /// <summary>Explorer-point reward paid once on first completion.</summary>
        public int rewardExplorerPoints;

        /// <summary>True after the reward has already been paid.</summary>
        public bool claimed;
    }
}
