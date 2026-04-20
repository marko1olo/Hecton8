using UnityEngine;

namespace Hecton8.Meta
{
    /// <summary>
    /// Shared cold-path helpers for resolving meta-profile state outside the global profile owner.
    /// </summary>
    internal static class MetaProfileUtility
    {
        internal static bool TryResolveProfile(out GlobalProfileData profile)
        {
            GlobalProfileManager manager = GlobalProfileManager.Instance;
            if (manager != null)
            {
                profile = manager.GetSnapshot();
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

            int count = Mathf.Clamp(profile.purchasedUpgradeCount, 0, profile.purchasedUpgradeLevels.Length);
            for (int i = 0; i < count; i++)
            {
                MetaUpgradeLevelRecord record = profile.purchasedUpgradeLevels[i];
                if (string.Equals(record.upgradeId, upgradeId, System.StringComparison.Ordinal))
                    return Mathf.Max(0, record.level);
            }

            return 0;
        }
    }
}
