using UnityEngine;
using Hecton8.Core;
using Hecton8.Quest;

namespace Hecton8.Meta
{
    /// <summary>
    /// Shared cold-path helpers for resolving meta-profile state outside the global profile owner.
    /// </summary>
    internal static class MetaProfileUtility
    {
        internal static bool TryResolveProfile(out GlobalProfileData profile)
        {
            IProfileService profileService = GlobalRegistry.Profile;
            if (profileService != null)
            {
                profile = profileService.GetSnapshot();
                return profile != null;
            }

            return GlobalProfileManager.TryLoadSnapshot(out profile);
        }

        internal static int ResolveUpgradeLevel(string upgradeId)
        {
            GlobalProfileData profile;
            if (!TryResolveProfile(out profile))
                return 0;

            return ResolveUpgradeLevel(profile, upgradeId);
        }

        internal static int ResolveUpgradeLevel(GlobalProfileData profile, string upgradeId)
        {
            if (profile == null || string.IsNullOrWhiteSpace(upgradeId) || profile.purchasedUpgradeLevels == null)
                return 0;

            uint upgradeHash = QuestFlagHashKernel.ComputeStableHash(upgradeId);
            if (upgradeHash == 0u)
                return 0;

            int count = Mathf.Clamp(profile.purchasedUpgradeCount, 0, profile.purchasedUpgradeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                MetaUpgradeLevelRecord record = profile.purchasedUpgradeLevels[i];
                uint recordHash = record.upgradeHash != 0u ? record.upgradeHash : QuestFlagHashKernel.ComputeStableHash(record.upgradeId);
                if (recordHash == upgradeHash)
                    return Mathf.Max(0, record.level);
            }

            return 0;
        }
    }
}
